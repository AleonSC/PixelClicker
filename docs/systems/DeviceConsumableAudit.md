# Devices + consumables audit (2026-10-20)

Three read-only passes: device code, consumable code, and game design. The design pass used the design docs, the to-do / ideas lists and the real numbers in code. Key claims were checked by hand. Nothing has been changed yet. Effort: S < 1 h, M = a few hours, L = a day.

**Done 2026-10-20:** all of section 1 (B1-B9) and section 2 (quick wins), plus: `PixelClicker.DisplayNameOf` (player name of a block type, used by potions and crafting), `PixelBody` (one velocity helper), `PixelPlacedDevice.IsMarking` + marking-name constants, `IsEnding` (IsDying / IsRemoving merged), `PixelChipperDevice` renamed `PixelPermanentDevice`. Not done from section 2: 'for a minute' in the Pet Treat / Focus texts (their real durations live outside the item). Sections 3 and 4 are open.

## 1. Bugs (confirmed by reading the code)

| # | Where | Bug | Fix | Effort |
|---|---|---|---|---|
| B1 | `PixelShop.TryBuyItems` / `PixelShopConsumables.MaxAffordable` | Buying several at once multiplies the CURRENT price by the count, ignoring the rising price. 3 Recyclers cost 225 White instead of 75+150+300 = 525. | Sum the price steps, or limit laddered items to one per purchase. | S |
| B2 | Inventory save part (order 210) vs Operation levels (in PixelStats, order 320) | Held counts are clamped to their caps BEFORE the Storage upgrade levels load. 5 Combo Fuel with +2 Storage loads as 3. | Don't clamp on Import, or clamp again after the stats load. | S |
| B3 | Potion / combo / attuned names, `Describe` | Enum names show to players ("HornetNest Potion" for the Termite Hive, other CamelCase names). | Use the tier's `displayName` everywhere. | S |
| B4 | `PixelVacuumDevice` (:57), `PixelWoodChipper` (:224), `PixelFanDevice` (:62) + hand-copied subsets in MagnetSorter, Quantum, TrashCan, Kiln, Funnel, VacuumCleaner | They break the "one owner per husk" rule. The Vacuum absorbs husks in the player's hand or reserved by the Hopper / Sweeper; the Chipper claims reserved ones; the Fan pushes held ones. | Use `PixelHuskHolder.Takeable` everywhere (with an allow-held option where needed). | S |
| B5 | `PixelMagnetSorterDevice` | Its stored stock is lost when it ends (no `Ended` handler); Crate / Quantum / Hopper release theirs. | Release the stock on end. | S |
| B6 | `PixelBiomassGenerator.PowerAround` | Also "powers" permanent and disarmed devices (energy bar shows, counted, nothing to pause). | Skip `Permanent` / `!Armed`. | S |
| B7 | `PixelJunctionBox.dropsSeen`, `PixelConveyorCombiner.waiting`, `PixelRobotWorker.bankTarget` | They keep husk references with no HuskReleasing listener (pooled husks can come back). | Listen to HuskReleasing. | S |
| B8 | `RobotPlace` / `PlaceDeviceNow` | Never disarm and skip `onDevicePlaced`. `RobotPlace` uses up a Funnel even when it only moves it. | Route through the normal place path. | S |
| B9 | Bait Bucket, running Focus potion | Not saved: the Bucket (and its contents) is lost on stopping Fishing or loading; the running Focus ends on load. | Save parts (IPixelSaved). | S-M |

## 2. Quick wins (S, low risk)

- **Device card info:** the card shows only name + time for 10 of ~20 devices (Fan, Vacuum, Magnet, Sprinkler, Sorter, Magnetized Sorter, Charge Booster, Lightning Rod, Wood Chipper, Funnel). Add their radius / pull / force / stock / charge / health via `AddInfo`.
- **Inventory tooltips:** they show a generic "Right-click to drink" hint instead of the item's description (the shop and the 3D gallery do show it).
- **One name for the category:** "Devices" (Inventory, gallery) vs "Utilities" (shop).
- **Swap warning:** drinking a potion while one runs replaces it silently; warn or say so.
- **Description placeholders:** descriptions hard-code numbers ("3 Pesticide charges", "Hold up to 5/10", "For a minute"). Add `{held}` / `{charges}` / `{uses}` placeholders.
- **Stale text:** "a conveyor belt opening latches on" (Wood Chipper, Funnel, enum comments) dates from before couplings, and 6 device texts still say "old pixels".
- **Database:** `PixelDatabase` adds an entry for every combo and attuned item (thousands); filter with `ItemListable` and drop its try/catch.
- **Pointer check:** cache `DeviceUnderPointer` once per frame (it runs 2-3 times per frame).
- **Naming:**
  - rename `PixelChipperDevice` (the generic permanent stub) to `PixelPermanentDevice`;
  - merge `IsDying` / `IsEnding` / `IsRemoving`;
  - one shared velocity get/set helper (13 private copies).
- **Range markers:** replace the "Range" / "Blow Area" magic strings (matched in 3 files) with a marker component.

## 3. Shared systems worth building (the "3rd hand-wired copy" rule)

1. **One item definition per consumable / device kind.** Today one item is spread over about 8 places: `CreateDefault*`, ShopText overrides, the `PixelInstantItems` use rule, the `BuildItemModel` model, `SpawnDevice`, `EffectiveDevice`, the Operation rows and the shop's `UtilitiesFirst` order. There are about 124 `DeviceKind` branches in PixelConsumables and DeviceKind is used in 37 files. One table entry per kind would hold name, description template, prices, cap, category enum, use rule (placed / instant / auto / planted), feedback (sound, stat, message), model / spawn builder, needs-arming, permanent, capabilities (attune, carry, upgrade, power) and Operation stats. The device class supplies its own builders, as the newer devices already do. **L**
2. **A stable item id.** Items are matched by display name (saves, keybinds, favourites, pictures) or by an index that shifts when pixel types are added. A ShopText rename silently drops owned counts and keybinds. One `ItemId` (category, kind / type, second, third, attune) with dictionary lookup, plus combo / attuned entries made on demand instead of 3,500+ up front. **M**
3. **Split `PixelConsumables.cs` (4,062 lines)** into item data, inventory (owned / caps / use / save), the device placer (place / remove / arm input) and per-device body builders moved into each `Pixel*Device.cs` (~600 lines of `Build*Body`). Do it together with 1. **M-L**
4. **One placed-object base for consumable devices and Tester factory pieces.** Today they have two placing / removal / tint / save systems:
   - Tester removal starts on the first right press, with a fixed 0.8 s and no right-click owner check.
   - The two red tints are near-identical.
   - Saves differ: `PlacedState` vs `TesterSave`.
   - Factory pieces get no device card.

   Add an `IPixelDeviceState` (Export / Import) that both save paths loop over, which also removes the "charge rides in the aim field" hacks. **L**
5. **A husk intake helper in `PixelHuskQuery`:** `Takeable` with options, a per-husk cooldown set that cleans itself on HuskReleasing (copied 5 times today), one `FlyIn` absorb animation (copied 3 times), and optionally a once-per-step spatial bucket so ~13 devices stop scanning every husk every physics step. **M**
6. **One timed-effect runner** for the plain potion, the Focus potion and the Pet Treat: timer, status line, saving, "replaces the running X" notice. **S-M**

## 4. Design findings (decisions for you / the Design Discussion session)

**Biggest friction**
- **Recycler lifetime.** Every device chain ends in the Recycler (Sweeper, Hopper, hose, crate), but it lasts 5 minutes and vanishes with its contents. The Run Time Operation row that came with the 5-minute change (2026-10-16) has since been removed, so only a Woodcutting Biomass Generator can keep it running. Options: make it permanent with its price ladder, bring Run Time back, or keep it timed on purpose. (Its script header says "permanent" and its doubling factor is 1.8, but the data says 300 s and 2.)
- **Too much at once.** When the shop opens (~6 min) Utilities already lists Recycler, Coupling, Hose, Belt Kit and Junction Box (all gated by White), plus the Crate (Gray): four factory parts with nothing worth connecting. Gate the factory parts behind the Funnel at about Standard 6-7, using the planned shared "needs skill X level N" table (to-do line 21). Three different gating mechanisms exist today: `requiredType`, `shopSkillId`, `devOnly`.
- **Dead zone after the Sweeper (Standard 5, ~30 min).** The Magnet needs the Electrician pack, the Fan needs Glass, and the Funnel, Organizer and Sorter are dev-only. That leaves no new general husk tools for an estimated 1-3 hours.

**Items that need a clearer job**
- **Magnet:** its heap keeps ageing, so it does NOT answer despawn (despite the Connection Map), and the Recycler's suction only takes husks from above the rim. Make its settle zone pause ageing. Also, its price is about 100x below the Fan's.
- **Fan:** about 2 minutes of income for 30 s of pushing. Reprice it to around the Magnet's tier; the attuned Fan (one type only) is the interesting version.
- **Vacuum Device (dev-only):** really an automatic Husk Crush. Rework it into a permanent "Crusher" that auto-crushes for about 0.35 of a click's XP, so the Recycler automates money and the Crusher automates XP.
- **Storage Crate:** stored husks have no use until the Workbench exists (to-do lines 109-111); it costs more than the Hopper and does less. Prioritise the Workbench, or cheapen the Crate meanwhile.
- **Quantum Organizer (dev-only):** a storage device on a timer that empties it. Make it permanent at Standard 12+ (as planned).
- **Dead outputs:** Charcoal (Kiln) and Wood Chips have no use. Make them Biomass fuel (planned, to-do line 120, but it fits the existing generator now).
- **Unreachable recipes:** the Magnetized Sorter and the attuned Vacuum need the dev-only Sorter / Vacuum. Re-recipe them (for example Magnet + Junction Box).
- **Bait Bucket:** Fishing is on sale but its only tool is dev-only.

**Cut or merge candidates**
- **Trash Can (tester):** does what the despawn timer already does.
- **Sorter ring (dev-only):** dates from before husks stopped paying; Aimed Clicks and the Funnel cover it.
- **Belt Sorter (tester):** a third filter device; merge into the Junction Box filters (which also overlap the Hopper's Outputs upgrade).

**Bigger direction (already in Ideas: "Power... plan this one next")**
- **Power as the one upkeep rule:** replace the per-device timers (Fan 30 s, Magnet 30 s, Sprinkler 45 s, Organizer 600 s, Magnetized Sorter 180 s) with "runs while powered". Power would come from Biomass (wood), Solar, Electric husks / Charge Booster and Charcoal. That gives Electrician, Woodcutting and Solar outgoing links (Connection Map gaps) and replaces the pile of timers with one rule.

**Stale docs:** `ConnectionMap.md` still lists the belt as "Electrician level 5", the Vacuum Device as plain "Built" and the Recycler as "Designed" under despawn.
