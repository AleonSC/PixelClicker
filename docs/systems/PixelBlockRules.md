# Special blocks and hold blocks (PixelBlockRules.cs)

One data-driven place for two kinds of skill block. A component (added by PixelClicker.Awake, `PixelBlockRules.Instance`) holds two `[Serializable]` lists; built-in rules are added by name at start if missing (context menu "Add Missing Default Rules").

## Special blocks - rare replacements
`SpecialRule`: `tier` (the block's PixelTier: name, colour, clicks, pay), `skillId` (empty = any skill in play), `replaces` (block types it can take the place of), `chance`.
`PixelClicker.PickSpawnTier` ends with `PixelBlockRules.TryReplace(clicker, pick)`; the tier is created on first use by `PixelClicker.EnsureSpecialTier` (no spawn weight, unlocked so the cube shows it).
Default: **Great Oak** (Woodcutting, replaces Wood / Bark / Sap / Leaf / Heartwood / Twig, 1% rising to 25% as Acorn old pixels lie on the field (`hasBoost`, `boostBlock`, `boostFullCount` 12, `maxChance`), 50-100 clicks, pays 150; nests: PixelGreatOak.cs).
Not migrated yet: Fishing's Whale / Bottle / Pearl (PixelFishingSpawns, their chances feed the Pixel Guide's SpawnShare) - move them here when next touched.

## Hold blocks - bonus while held
`HoldRule`: `type`, `skillId`, `clickMultiplier`, `payoutMultiplier`, `xpMultiplier`, `label`. Held = Block Hold (right button on the cube, PixelBlockHold). While held:
- `clickMultiplier`: every manual click counts as n clicks (PixelBlockHold.ClickFactor -> PixelClicker.CollectInternal; damage on tough blocks and payout).
- `payoutMultiplier`: every payout (CollectInternal).
- `xpMultiplier`: that skill's XP (PixelSkills.AddXp).
- `label`: shown beside the hold meter (e.g. "Clicks x3").
Default: **Whetstone** (Woodcutting base block, 3 hits, clicks x3).

## Adding one
1. Append a `PixelType`, give it a look (PixelLooks default + a `looksVersion` migration) and spawn data.
2. Hold block: make it a normal base block of its skill pack (shop pack reward tier + a packDataVersion migration for saved packs + the skill's `types` list). Special block: nothing else (the tier is made by the rule).
3. Add ONE rule to the list. Behaviour beyond numbers stays in its own script (listen to `PixelClicker.PixelHit` / `PixelCollected`).

## On the Pixel Guide
Special and hold blocks appear on the guide by themselves: PixelBlockRules.EnsureActiveSpecialTiers makes the tiers of the skill in play when the guide refreshes (so the Great Oak is listed from the start of Woodcutting), the row's sub line is tagged "SPECIAL, 5% replaces a block" / "HOLD block", and PixelBlockRules.GuideNotes adds the role sentences to the '!' tooltip (which blocks it replaces and the chance; what holding it does). Spawn shares include the replacements (PixelClicker.SpawnShare = AdjustShare(SpawnShareBase)): a special block shows chance x the share of what it replaces, and those blocks show a little less. Each new block still needs its own flavour case in PixelGuideTexts.Mechanics.
