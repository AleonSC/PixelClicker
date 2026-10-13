# Machines in the Tester: conveyor belt and kiln

Both are Dev Hub > Tester features (PixelFeatureTester), built from primitives in code with `PixelFactoryKit` (shared materials / boxes / cylinders / ghost versions). Not saved.

## Conveyor belt (PixelConveyor.cs)
- Place: click a point, click another: a belt between them; the second point starts the next piece (short pieces = a curve); right click ends the chain, right click again (or Esc) stops. Clicks near a belt end snap to it (`PixelConveyor.Snap`). Two-point features are `RegisterLine` in the tester (marker ghost, then a preview piece).
- Right click a placed belt (not while placing): reverse it (`PixelConveyorSystem.Update`).
- `PixelConveyorSystem.FixedUpdate` carries every old pixel standing on a belt (not held, not kinematic, not glued into a build, not flying) along the belt at `beltSpeedCubes` (0 = 1.6 cubes/s) and damps sideways speed. Anything dropped by the player or spat out of a machine just lands on it.

## Kiln (PixelKiln.cs)
- Anything inside the mouth zone (in front of the +X door) burns: `ClaimOldPixel`, drawn in while it shrinks, then `charcoalPerPixel` Charcoal old pixels (`PixelType.Charcoal 81`, `EnsureCharcoalTier`, standalone, look migration looksVersion 58) leave the back chute after `charcoalDelaySeconds`, one per `charcoalGapSeconds`. Fire light, embers, smoke (thicker while it burns).
- A belt can lead into the mouth; another can carry the charcoal away.
