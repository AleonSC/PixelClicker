# Special blocks and hold blocks (PixelBlockRules.cs)

One data-driven place for two kinds of skill block. A component (added by PixelClicker.Awake, `PixelBlockRules.Instance`) holds two `[Serializable]` lists; built-in rules are added by name at start if missing (context menu "Add Missing Default Rules").

## Special blocks - rare replacements
`SpecialRule`: `tier` (the block's PixelTier: name, colour, clicks, pay), `skillId` (empty = any skill in play), `replaces` (block types it can take the place of), `chance`.
`PixelClicker.PickSpawnTier` ends with `PixelBlockRules.TryReplace(clicker, pick)`; the tier is created on first use by `PixelClicker.EnsureSpecialTier` (no spawn weight, unlocked so the cube shows it).
Default: **Great Oak** (Woodcutting, replaces Wood / Bark / Sap / Leaf / Heartwood / Twig, 1% rising to 25% as Acorn old pixels lie on the field (`hasBoost`, `boostBlock`, `boostFullCount` 12, `maxChance`), 50-100 clicks, pays 150; nests: PixelGreatOak.cs).
Not migrated yet: Fishing's Whale / Bottle / Pearl (PixelFishingSpawns, their chances feed the Pixel Guide's SpawnShare) - move them here when next touched.

## Hold blocks - bonus while held
`HoldRule`: `type`, `skillId`, `extraClicks`, `burstGapSeconds`, `payoutMultiplier`, `xpMultiplier`, `clearsHazard` + `hazardType`, `label`. Held = Block Hold (right button on the cube, PixelBlockHold). While held:
- `extraClicks`: RAPID CLICKS like Overcharge - every manual click is followed by n extra full clicks, `burstGapSeconds` apart (PixelClicker.CollectInternal -> PixelBlockRules.OnManualClick -> AutoCollect; the burst never starts another burst).
- `payoutMultiplier`: every payout (CollectInternal).
- `xpMultiplier`: that skill's XP (PixelSkills.AddXp).
- `label`: shown beside the hold meter (e.g. "Clicks x3").
- `clearsHazard` / `hazardType`: a manual left click on that hazard block clears it at once, safely (PixelClicker.CollectInternal -> ClearHazardNow; no explosion, no XP penalty, no payout).
Defaults: **Pesticide** (Woodcutting base block, 3 hits, clears Hornet Nests) and **Whetstone** (Woodcutting base block, 3 hits, 2 extra rapid clicks = 3 at once).

## Adding one
1. Append a `PixelType`, give it a look (PixelLooks default + a `looksVersion` migration) and spawn data.
2. Hold block: make it a normal base block of its skill pack (shop pack reward tier + a packDataVersion migration for saved packs + the skill's `types` list). Special block: nothing else (the tier is made by the rule).
3. Add ONE rule to the list. Behaviour beyond numbers stays in its own script (listen to `PixelClicker.PixelHit` / `PixelCollected`).

## On the Pixel Guide
Special and hold blocks appear on the guide by themselves: PixelBlockRules.EnsureActiveSpecialTiers makes the tiers of the skill in play when the guide refreshes (so the Great Oak is listed from the start of Woodcutting), the row's sub line is tagged "SPECIAL, 5% replaces a block" / "HOLD block", and PixelBlockRules.GuideNotes adds the role sentences to the '!' tooltip (which blocks it replaces and the chance; what holding it does). Spawn shares include the replacements (PixelClicker.SpawnShare = AdjustShare(SpawnShareBase)): a special block shows chance x the share of what it replaces, and those blocks show a little less. Each new block still needs its own flavour case in PixelGuideTexts.Mechanics.

## Stall (a trait on any block)
`PixelTier.stallsHold` (+ `stallSeconds`, 0 = 1.5): every click on such a block freezes the Block Hold meter's drain for that long (each click restarts it; PixelClicker.CollectInternal -> PixelBlockHold.Stall). The meter turns icy white-blue while frozen. Independent of hit count and of the special / hold roles. Tagged "STALL" on the Pixel Guide with its own sentence. Default: the Great Oak (so a held Whetstone / Pesticide keeps its bonus while you work through an oak).
