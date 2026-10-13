# Special blocks and hold blocks (PixelBlockRules.cs)

One data-driven place for two kinds of skill block. A component (added by PixelClicker.Awake, `PixelBlockRules.Instance`) holds two `[Serializable]` lists; built-in rules are added by name at start if missing (context menu "Add Missing Default Rules").

## Special blocks - rare replacements
`SpecialRule`: `tier` (the block's PixelTier: name, colour, clicks, pay), `skillId` (empty = any skill in play), `replaces` (block types it can take the place of), `chance`.
`PixelClicker.PickSpawnTier` ends with `PixelBlockRules.TryReplace(clicker, pick)`; the tier is created on first use by `PixelClicker.EnsureSpecialTier` (no spawn weight, unlocked so the cube shows it).
Default: **Great Oak** (Woodcutting, replaces Wood / Bark / Sap / Leaf / Heartwood / Twig, 5%, 50-100 clicks, pays 150; nests: PixelGreatOak.cs).
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
