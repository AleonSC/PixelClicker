# PixelGameState / GameState

Started 2026-10-23 (Phase 1: the owner, plus the Bank and the Stats as the pilot; then the Auto Clicker, the Focus potion, Time Stop and Pixel Grabbing, the Bait Bucket and Cubie's bill, the Combo switch, the event log, the UFO Helper, the pets, the achievements, the potion / device inventory, the skill choices, the minigame switches / counters, the shop packs and the currencies - PixelClicker, the last one). The player's PROGRESS moved out of the MonoBehaviours into one plain object, one system at a time; finished 2026-10-24, when the old `IPixelSaved` / `PixelSaveRegistry` per-system save parts were retired.

## Why
- **Saving:** each system saved its own part (`IPixelSaved`) in load-order bands.
- **Restart:** it needed hand-registered resets (`PixelStatics.ResetOnRestart`).
- **Startup order:** "is the save / the system there yet" checks were everywhere.
- **No simulation:** the rules couldn't run without the scene.

With one state object, saving writes it, loading replaces it and a new game makes a fresh one.

## The pieces
- **`GameState.cs`:**
  - `gameId`: which game this is (new per New Game / Restart); autosaves are tagged with it (PixelAutosaves trims / deletes per game).
  - `GameState` is plain `[Serializable]` data with no `UnityEngine.Object` references. It holds one sub-state per system (`BankState`, `StatsState`, ... `ClickerState`).
  - `GameState.MovedSystems` lists the systems' class names (the compile-check rule reads it).
  - `Repair()` fills in missing parts after a load.
- **`PixelGameState.cs` (the owner, in `PixelSystems.All`, Core):**
  - `PixelGameState.Current`: never null. It is made on first use, so early callers never wait.
  - `Replace(state)`: PixelSaveGame calls it FIRST in a load.
  - `RaiseLoaded()` / the `Loaded` event: called once tiers, shop and husks are back, but BEFORE the Dev Hub tester machines (a Solar Panel links to a placed device, belts dock on devices' couplings). Views refresh and world objects rebuild here.
  - `RaiseSaving()` / the `Saving` event: called right before a save is written. World objects that change all the time (placed devices, the running potion) copy themselves into their sub-state here instead of every frame.
  - `NewGame()`: called right before the scene reloads for Restart (`PixelPauseMenu.Restart`) and New Game (`PixelTitleScreen.StartNewGame`). These are the only two scene loads.
  - It does NOT use `PixelStatics.ResetOnRestart`, because `sceneUnloaded` can arrive after the new scene has started.
  - The component shows `Current` in the Inspector while playing ("State"), so you can read values and type them in to test.
- **Saving:** `SaveData.state` holds `Current` whole, through JsonUtility. PixelSaveGame itself still writes the husks on the floor, the per-save settings, the Dev Hub tester machines and the slot summary.

## Rules for a system with progress
- **Read the state through `PixelGameState.Current.<part>` every time.** Never keep a reference to it: a load swaps the whole object. The usual shape is `private static BankState B => PixelGameState.Current.bank;`.
- **The component keeps only display, input, per-session numbers and Inspector tuning** (`[SerializeField]` + `[Tooltip]` as always). Anything that must survive save / load / restart goes in the sub-state.
- **No `PixelStatics.ResetOnRestart` for progress in its script.** `tools/compile-check/rules.sh` checks this for every name in `GameState.MovedSystems`. A restart reset that clears something that is not progress (the old scene's event listeners) is marked `// restart-reset-not-progress: <what>` on its line.
- **Key data by stable ids (`PixelType` as int, string keys), not by tier index or list position.** Tier indices shift when pixel types are added. Turn them into tier indices in the component (see `PixelBank.CountAt`).
- **Physics objects stay outside:** husks, placed devices and tester machines are copied out when saving and rebuilt when loading, as before.

## Adding a NEW system's progress: the steps
(Every system was moved by 2026-10-24 and the old per-system save parts - `IPixelSaved` / `PixelSaveRegistry` - are gone. These are the steps for a new feature that has progress to keep.)
1. **Split its fields** into progress (must survive save / load / Restart) and the rest (UI, input, timers of the current session, Inspector tuning, caches).
2. **Add a `[Serializable] public class <Name>State`** to `GameState.cs` with the progress fields, keyed by stable ids.
   - Add `public <Name>State <name> = new <Name>State();` to `GameState`, and a line for it in `GameState.Repair()` (plus a `Repair()` on the sub-state if it has lists).
   - Unity-serializable types only: no `Dictionary`. Use a `List` of small entries plus a `[NonSerialized]` lookup, as `StatsState` does.
3. **In the component:** `private static <Name>State X => PixelGameState.Current.<name>;` and read / write only through it (or properties over it, as `PixelTier` / `ShopPack` / the minigames do).
4. **World objects** (bodies in the scene): copy what is needed to rebuild them into the state on `PixelGameState.Saving` (if they change all the time) or the moment they change (if they rarely do), and rebuild them on `PixelGameState.Loaded`.
5. **Anything else to redo after a load** (a selection, a view, a "don't announce again" flag): subscribe to `PixelGameState.Loaded` in Awake (unsubscribe in OnDestroy). **If it needs ANOTHER system's state in a fixed order during the load**, stop and ask (a core step like the shop's `ApplyLoadedPackStates` / the clicker's `ApplyLoadedTiers` is called from PixelSaveGame at its step).
6. **No `PixelStatics.ResetOnRestart` for progress** (a new game's fresh state resets it). Add its class name to `GameState.MovedSystems` so the compile check enforces that.
7. **Docs:** its script header / `docs/systems` file, a row in the table below. Then `tools/compile-check/check.sh`, a changelog line, and what to test: save -> quit -> load, Restart, New game.

## What lives where
| System | Sub-state | Notes |
|---|---|---|
| `PixelBank` | `BankState` (stored husks by pixel type: count + total value) | `Loaded` picks a stocked selection. Dev Tools' reset calls `ClearStored`. Capacity and 'bought' come from the shop (not progress). |
| `PixelStats` | `StatsState` (typed numbers, potions drunk, generic counters) | `PixelStats.Count / Best / Fastest / Total` now work without the component. `Loaded` counts the session if the 3-second count hasn't run. |
| `PixelAutoClicker` | `AutoClickerState` (bought, switched off, upgraded interval / clicks per tick) | The Inspector `intervalSeconds` / `clicksPerTick` are BASE values, used while the state holds 0 (the pattern for "an upgrade's value vs the tuning default"). The shop's load step (`SetPackState`) doesn't touch it, so there are no order issues. |
| `PixelFocus` | `FocusState` (running potion: skill id, name, seconds left) | The potion list stays on the component. `HoldsOffHazards` reads the state, so it works without the component. `Loaded` redraws the status line. |
| `PixelTimeStop` | `SwitchState` (`timeStop`: switched off in Toggles) | The old Import's side effect (a switched-off Time Stop ends any stop / slow) runs in `Loaded`. 'Bought' (`timeStopActive`, `timeSlowOwned`) still comes from the shop packs; it moves with the shop. |
| `PixelGrab` | `SwitchState` (`grab`: switched off in Toggles) | `Loaded` lets go of a held husk if it is off and re-applies `clicker.GrabEnabled`. Grabbing itself is always unlocked. |
| `PixelBaitBucket` | `BaitBucketState` (placed, spot, stored Fish / Water husks by type with their payout) | The first WORLD object moved: the bucket writes its spot into the state each frame while it stands (it can be carried) and `Loaded` rebuilds it from the state. The pattern for a placed thing: the state holds what's needed to rebuild it, the object is only the view. |
| `PixelCubieBill` | `CubieBillState` (paid per colour by type, trust tier, revealed) | The old part loaded early on purpose (Recyclers rebuilt during the load asked whether to show Cubie's price tag); the price tag is gone from the Recycler, and with `Replace` at the START of the load the state is in place early anyway. Its `ResetOnRestart` clears event listener lists (not progress), marked `// restart-reset-not-progress:` so the compile-check rule allows it. |
| `PixelCombo` | `SwitchState` (`combo`: switched off in Toggles) | `Loaded` drops the running combo if it is off and redraws the meter. |
| `PixelHints` (event log) | `EventLogState` (the log's lines: short text + the tip behind it) | Only the event log's lines moved: the 'seen' flags of first-time tips stay global PlayerPrefs (they are per machine, not per save). `Loaded` redraws the log from the top with no box. |
| `PixelRobotWorker` (UFO Helper) | `UfoState` (switched off; the job: device by display name + spot, or banking one pixel type) | `SetJob` / `SetBankJob` write the state, so the runtime job and the state never drift; `Loaded` rebuilds the job's ghost and re-applies the switch. |
| `PixelPets` | `PetsState` (owned pets in the order found: type, off, outfit, name) | Each live `Pet` keeps a reference to its state entry (`Pet.saved`; `type` / `off` / `outfit` / `customName` are properties on it), so every change is the state's at once - the pattern for a list of world objects. `Loaded` rebuilds the bodies. |
| `PixelAchievements` | `AchievementsState` (earned tiers + when, by achievement id) | The achievement list stays Inspector config; `earnedTiers` / `EarnedAtOf` / `StampTier` read and write the state by id (no stored reference). The old 'ResetOnNextStart' flag that Restart / New Game set is gone: a new game's fresh state has nothing earned. `Loaded` re-grants anything already reached quietly. |
| `PixelConsumables` (inventory) | `InventoryState` (held potions / devices by key, the ghost backpack bonus; the WORLD part: placed devices + the running potion) | `Potion.owned` / `bonusCap` and `Device.owned` are properties reading the state by key (potion "p:type:second:third", device "d:<display name>", the key cached on the item). Placed devices and the running potion change every frame, so they copy themselves in on `PixelGameState.Saving` and are rebuilt in `Loaded` - the pattern for world objects that change all the time. The last `IPixelSaved` part. |
| `PixelSkills` | `SkillsState` (the pack in play, the guest blocks switched on, the offline task) | These three were per-save SETTINGS (PlayerPrefs copied into each save by PixelSettingsSync); now plain progress, and taken off PixelSettingsSync's list. Skill XP, bought nodes, the bonus XP bank and Mastery were already `StatsState` counters, so they moved with the Stats. The hazard-penalty restart reset is marked not-progress. `Loaded` raises `Changed`. Side effect: the title screen no longer takes the newest save's skill pack with its other settings (it shows the plain rotation until Continue). |
| `PixelMinigame` (+ every subclass) | `MinigamesState` (per minigame id: switched off, goal counter, second number) | The base class has `UserDisabled`, `SavedCount` and `SavedExtra` over the state; a subclass keeps its counter as a PROPERTY over them (`private double chunks { get => SavedCount; set => SavedCount = value; }`), so its code is unchanged. `OnProgressLoaded` (virtual) fixes up after a load (goal already announced, bomb parts earned >= held); a switched-off one stops on `Loaded`. The Ultra Holes' counter is PixelClicker's Ultra-ever-earned: until PixelClicker moves, the Pad copies it in on `Saving` and back on load. PixelSaveGame no longer writes `SaveData.minigames`. |
| `PixelShop` | `ShopState` (per pack display name: bought, level) | `ShopPack.purchased` / `level` are properties over the state. Applying a pack's effects on load must stay at its FIXED point (right after the tiers, before the rest of the load: not-bought packs switch their features off, some upgrades re-apply), so PixelSaveGame still calls it there - `shop.ApplyLoadedPackStates()` - and it reads the state instead of the file. The rule for a core step whose ORDER matters: PixelSaveGame keeps calling it at its step; only where the data comes from changes. PixelSaveGame no longer writes `SaveData.packs`. |
| `PixelMaterials` | `MaterialsState` (amount held per material id, e.g. charcoal) | New 2026-10-24 (built straight on the state). The kinds of material are Inspector config on the component; nothing to redo on `Loaded` (the Inventory reads the amounts each refresh). |
| `PixelClicker` (currencies) | `ClickerState` (per pixel type: count, total, unlocked, spawn off, times collected, Ultra / Value levels; Ultra held + ever earned) | The last one. `PixelTier`'s progress fields are PROPERTIES: a LIVE tier (one in the clicker's list: `MakeLive`, called in Awake, by `EnsureTier` and as a cheap check each Update) reads its state entry by type and re-binds by itself when a load swaps the state; a tier that is only a DEFINITION (a shop pack's reward tier, a `Clone`) keeps its own values until the clicker takes it in (a type the state has no entry for starts from them: starting amounts, new shop tiers locked). `ApplyLoadedTiers()` is the fixed FIRST core step of a load (before the shop). The Ultra Holes' stopgap is gone. The save keeps a small `totalPixels` summary for the slot list. |
| `PixelRapport` | `RapportState` (per vendor id: points, the highest step reached, last deal / trickle in play seconds) | New 2026-10-24 (built straight on the state). The step table and the vendors' loves / lines are Inspector config; the trickle is applied when an entry is read (no per-frame work); nothing to redo on `Loaded`. |
| `PixelDeliveryJobs` | `DeliveryState` (the running jobs: vendor, size, start, time limit, pay, rapport, reward item, and per husk type need / have / total worth; when jobs were taken, for the per-hour cap; the vendors whose dock shows) | New 2026-10-24 (built straight on the state). The delivery CRATE is only a view: a husk going in writes the job's line at once, and the component rebuilds any missing crate from the state on its next check (so `Loaded` just clears the old crates and markers). The time limit runs on play seconds (`stats.playSeconds`). |

The remaining systems, simplest first, are on the to-do list.
