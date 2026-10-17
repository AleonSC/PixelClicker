# Machines in the Tester: conveyor belt and kiln

Both are Dev Hub > Tester features (PixelFeatureTester), built from primitives in code with `PixelFactoryKit` (shared materials / boxes / cylinders / ghost versions). Not saved.

## Conveyor belt (PixelConveyor.cs)
- Place: click a point, click another: a belt between them; the second point starts the next piece (short pieces = a curve); right click ends the chain, right click again (or Esc) stops. Clicks near a belt end snap to it (`PixelConveyor.Snap`). Two-point features are `RegisterLine` in the tester (marker ghost, then a preview piece).
- Right click a placed belt (not while placing): reverse it (`PixelConveyorSystem.Update`).
- `PixelConveyorSystem.FixedUpdate` LOCKS every old pixel that lands on a belt (not held, not kinematic, not glued into a build, not flying, room for it) onto the belt and carries it along by position at `beltSpeedCubes` (0 = 1.6 cubes/s), no physics (like the Storage Crate). At the end it is handed to a belt that starts there, otherwise (an end is detected) it is ejected with physics back on, flying on in the belt direction. Riders keep a gap (queue when blocked). Flipping a belt flips its riders; grabbing one frees it.

## Kiln (PixelKiln.cs)
- Anything inside the mouth zone (in front of the +X door) burns: `ClaimOldPixel`, drawn in while it shrinks, then `charcoalPerPixel` Charcoal old pixels (`PixelType.Charcoal 81`, `EnsureCharcoalTier`, standalone, look migration looksVersion 58) leave the back chute after `charcoalDelaySeconds`, one per `charcoalGapSeconds`. Fire light, embers, smoke (thicker while it burns).
- A belt can lead into the mouth and another can carry the charcoal away - through couplings on its 'mouth' (In) and 'chute' (Out) sockets.

## Wood Chipper discharge arrow and chute socket
While placing, the ghost chipper shows a flat arrow from its chute outwards (PixelWoodChipper.BuildArrow; the chips shoot out along it). The chute is an Out SOCKET on the floor in front of the outlet: with a coupling there, its Wood Chips blocks go straight onto the belt that starts at it (see `docs/systems/PixelCouplings.md`). (The old placing latch onto a belt opening is gone.)

## Belt speed (first person)
Right click a belt in first person: a small window (PixelConveyorSystem speed editor) with a number box (cube widths per second, 0 stops it), Set, Reverse and Close; Esc closes it; the mouse is freed while it is open (PixelFirstPerson `playable`). In the normal view a right click still reverses the belt.

## Belt ends feed machines (couplings)
At a belt end with no belt after it, `PixelConveyorSystem` hands the pixel to an In COUPLING whose port is there (`PixelCouplings.HandIn` -> the device's one hook `AcceptHusk`); none there, or the device is full, and it is ejected. Machines declare SOCKETS and couplings are fitted to them: see `docs/systems/PixelCouplings.md` (this replaced the old `IPixelBeltAcceptor` / `IPixelPort` interfaces on 2026-10-17).

## Lumberjack: Buy / Sell tabs, Wood Chipper device, potions
PixelVisitorMinigame got `BuySellTabs` (a vendor that sells wares AND buys pixels: Buy tab = wares, Sell tab = trades). The Lumberjack sells the Wood Chipper (`DeviceKind.WoodChipper` 15, `Device.vendorOnly` = not listed in the shop or sold by the Tinkerer; costs wood + bark; placed like the Fan with the turn input, its chute is an Out socket for a coupling; thin `PixelChipperDevice : PixelPlacedDevice` on the machine `PixelWoodChipper.Place` builds, so it is saved / restored / removed like any device) and plain potions of the Lumberjack pack's pixels; and buys those pixels (Wood, Bark, Sap, Leaf, Heartwood, Twig, Acorn, Whetstone, Pesticide - not the Great Oak) for pure currency.

## Solar Panel + saving the tester machines
- `PixelSolarPanel.cs` (Dev Hub tester feature): a flat panel powered while Solar pixels lie on it; it calls `HoldPower()` on its ONE linked device (blue line). Solar pixels on it are slowly used up unless they form a complete group of 5 (the existing Solar cluster keep-alive). After placing, click a device inside the blue ring to link; right click a placed panel to re-link (`PixelFeatureTester.BeginLink`).
- All tester machines save with the save file: `SaveData.testerObjects` = `PixelFeatureTester.TesterSave[]` (feature name + JSON from each feature's `ExportState/ImportState`). Imported after the placed devices (panels re-link to the nearest restored device by position). Belt riders are saved with their belt (`PixelConveyorSystem.ExportRiders/ImportRider`) and skipped by the normal loose old-pixel save.

## Heights, ramps and the Conveyor Elevator
- Belt ends are 3D points (y = underside). Scroll while placing a belt raises / lowers the point (posts appear), so belts can slope; `Snap` finds belt ends / elevator tops by screen distance. Riders move by ground distance (`Length`), with `PointAt` / `RunSpeed` / `LiftFactor` handling the slope.
- `PixelElevator` (tester feature): magnetizes to a belt end, click for the base, mouse height, click to confirm. Entry on its -Z face and exit on the +Z top face are sockets with couplings built in (a belt ends at the entry coupling; a new belt starts at the top one). Uses `PixelPathCarrier`.

## Conveyor Connector and Hose Adapter (removed 2026-10-17)
Both were deleted once couplings existed: a belt ends at a coupling or starts at one, and the Container Hose is routed freely between couplings, so neither the boxed channel nor the funnel-spout trough is needed.

## Corner joints
`PixelConveyorJoint.cs`: belts chained at an angle get a corner filler (slab + outer rails) on the outside of the turn, built automatically by each belt every 0.5 s (see the file header).
