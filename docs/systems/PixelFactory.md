# Machines in the Tester: conveyor belt and kiln

Both are Dev Hub > Tester features (PixelFeatureTester), built from primitives in code with `PixelFactoryKit` (shared materials / boxes / cylinders / ghost versions). Not saved.

## Conveyor belt (PixelConveyor.cs)
- Place: click a point, click another: a belt between them; the second point starts the next piece (short pieces = a curve); right click ends the chain, right click again (or Esc) stops. Clicks near a belt end snap to it (`PixelConveyor.Snap`). Two-point features are `RegisterLine` in the tester (marker ghost, then a preview piece).
- Right click a placed belt (not while placing): reverse it (`PixelConveyorSystem.Update`).
- `PixelConveyorSystem.FixedUpdate` LOCKS every old pixel that lands on a belt (not held, not kinematic, not glued into a build, not flying, room for it) onto the belt and carries it along by position at `beltSpeedCubes` (0 = 1.6 cubes/s), no physics (like the Storage Crate). At the end it is handed to a belt that starts there, otherwise (an end is detected) it is ejected with physics back on, flying on in the belt direction. Riders keep a gap (queue when blocked). Flipping a belt flips its riders; grabbing one frees it.

## Kiln (PixelKiln.cs)
- Anything inside the mouth zone (in front of the +X door) burns: `ClaimOldPixel`, drawn in while it shrinks, then `charcoalPerPixel` Charcoal old pixels (`PixelType.Charcoal 81`, `EnsureCharcoalTier`, standalone, look migration looksVersion 58) leave the back chute after `charcoalDelaySeconds`, one per `charcoalGapSeconds`. Fire light, embers, smoke (thicker while it burns).
- A belt can lead into the mouth; another can carry the charcoal away.

## Wood Chipper discharge arrow and belt latch
While placing, the ghost chipper shows a flat arrow from its chute outwards (PixelWoodChipper.BuildArrow; the chips shoot out along it). If the opening (start) of a belt lies in the arrow's area, `PixelWoodChipper.TrySnapToBelt` (the tester's `placeSnap` hook) turns the chipper so the arrow runs the way the belt runs and puts the chute over the belt's opening; the arrow turns green. A placed chipper drops its Wood Chips blocks straight onto a belt opening in that area (`BeltAtOutlet`).

## Belt speed (first person)
Right click a belt in first person: a small window (PixelConveyorSystem speed editor) with a number box (cube widths per second, 0 stops it), Set, Reverse and Close; Esc closes it; the mouse is freed while it is open (PixelFirstPerson `playable`). In the normal view a right click still reverses the belt.
