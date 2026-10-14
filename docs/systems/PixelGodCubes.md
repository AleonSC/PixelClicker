# PixelGodCubes

## The three Egyptian God Cubes (`PixelGodFind.cs`, `PixelGodSynth.cs`, parts of `PixelClicker.cs`)

**Slifer the Sky Dragon, Ra the Winged Dragon, Obelisk the Tormentor** (`PixelType.GodSlifer 82 / GodRa 83 / GodObelisk 84`, `PixelClicker.IsGodCube`, tiers made by `EnsureGodCubes`, switch `disableGodCubes`, weight `godCubeSpawnWeight` 0 = **0.000001** each, about 1 in 7 million pixels, 100x rarer than a Dragon Cube = the rarest thing in the game). Rare-drop tiers like the Dragon Cubes (`rareDrop`: spawn without unlocking, show in the inventory only once held, pay 1 of themselves) and kept out of potions, crafting, pets, achievements and the Value upgrades through `PixelClicker.IsRareCube` (Dragon or God). **Only one of each can be held**: `PixelTier.CanSpawn` is false while `count >= 1` and `AddCurrency` ignores a second one (and caps a payout at 1).

**Looks** (`PixelLooks`, `looksVersion` 59): Slifer = a red glass cube held together by storm lightning (the existing `lightning` look, red / yellow bolts, gold outline); Ra = polished gold with the sun's corona (`sunFx`, no hover / trickle for it) and a white-gold outline; Obelisk = deep blue stone with a cyan outline that sheds dust (`FarmAmbient.Crumbs`).

**Shows** (`PixelGodFind.Play(tierIndex, worldPos)` from `CollectInternal`, queued, canvas 691 blocks clicks, unscaled time, Escape skips to the backpack, `disableShow`, `showSpeed`, `darkness`, `cubeSize`; sounds `god_slifer / god_ra / god_obelisk` from `PixelGodSynth`, timed to the shows): Slifer = storm clouds + three lightning strikes + a lightning serpent that weaves round the screen and coils into the cube in a blast; Ra = a rising golden sphere bursts into a sun, golden wings (feather fans) beat in time with the sound and fold in with a shockwave; Obelisk = rising rumble and falling rubble, a giant blue fist lowers, cocks back and slams the screen (flash, crack lines, shockwaves, debris) and the cube rises from the crater. Then the cube hops into the backpack.

Tips: `god_first` on the first find of any. Dev Hub: 'Give next God Cube' (plays the show) and 'Play next God show (preview)'.

## Prime Cubes, Gauntlet, Cubelette (PixelInfinity.cs)
- Six stones (`PixelType.StoneSpace..StoneSoul` 85-90) reuse the God Cube machinery completely: `IsGodCube` is true for them (and for `InfinityCubelette` 91), so the one-of-each clamp, `CanSpawn`, the backpack stash and `EnsureGodCubes` apply. `IsInfinityStone` picks them out. Looks: `PixelLooks.StoneLook` (looksVersion 60).
- Shows: `PixelGodFind.DrawStone(stone, ...)` (shared timeline, climax at 3.2 s, a different signature effect per stone) and `DrawCubelette` (six colour streams, hexagon, snap at 3.4 s). Sounds `stone_*` / `stone_cubelette` in `PixelGodSynth`.
- Gauntlet: `DeviceKind.Gauntlet` 17 (vendorOnly, hold 1, costs in the Device list). The Wizard offers it (`PixelWizardMinigame.ModifyWares`) when all six stones are held and there is no Cubelette / Gauntlet. Right-click it -> `PixelInfinity.Open`: place a stone on each face of the flat golden cube, Forge uses up the Gauntlet and the stones, adds the Cubelette and plays its show (`PixelGodFind.PlayAtScreen`).
- The Cubelette is a trophy for now.
