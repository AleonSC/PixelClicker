# Sockets and couplings - one way to connect belts and hoses to devices

Agreed in the Design Discussion chat (2026-10-17), BUILT in the Devices session the same day (not yet tested in Unity). Code: `PixelSockets.cs` (sockets, kinds, the device
hooks, the registry, auto-fit, the type filter, the connection resolver) and `PixelCoupling.cs` (the snap-on coupling piece). Since 2026-10-20
couplings FIT THEMSELVES when a belt or hose attaches (below); Dev Hub > Tester > **Coupling** still fits one by hand. Sockets carry HUSKS only:
power will be a separate mechanic and will not use sockets.

## The problem it replaced
- Six machines each had their own belt intake (`IPixelBeltAcceptor` in PixelConveyor.cs: Kiln, Storage Crate, Trash Can, Splitter, Elevator,
  Conveyor Connector), each with its own "is this point at my opening" zone; the Container Hose fed them through the same interface.
- The Kiln and Crate also had a separate opening marker for the Conveyor Connector (`IPixelPort`).
- Outputs were hand-wired: the Funnel's floor output, the Wood Chipper's chute (plus its placing latch onto a belt), the Elevator top, the
  Splitter sides and the Connector's far end each had their own copy of "find a belt that starts here and drop the husk on it".
- The Recycler and the Sorting Hopper could not take a belt at all.

## The design
- **Sockets (data).** Every device declares its SOCKETS: a few lines each - `id`, local position (the PORT, where a belt end / hose end meets
  it, at belt-underside height), outward facing, `flow` = Intake / Output / Both (and `kind`, always Husk). Free sockets always show a small PORT PLATE (`PixelSocketPlates`: a chevron, green In / orange Out / teal Both). Sockets sit on openings the device already has:
  Kiln mouth, Storage Crate front, Trash Can side, Recycler back hatch, Quantum Organizer side, Wood Chipper chute, Funnel output, Elevator entry / top, Junction Box (eight, auto direction), Belt Sorter chute.
- **Couplings (one shared piece).** ONE model (PixelFactoryKit) and ONE component. It is fitted automatically (auto-fit, below); it is parented to
  the device so it moves with it (carry handles, turning); removing the device removes its couplings. It is tinted a little with the device's
  colour (`SocketTint`) so it reads as a flange, not a bolt-on.
- **Direction is shown on the coupling:** a green arrow pointing INTO the device = Intake, an orange arrow pointing OUT = Output, teal arrows
  both ways = Both.
- **Fitted couplings.** The factory pieces that ARE belt fittings (Elevator, Belt Sorter; the Splitter until it was removed) come with couplings fitted on their sockets
  (`PixelSocket.fitted`): built with the piece, not saved, not removable on their own. Everything else gets one by auto-fit.
- **Belts and hoses connect to a COUPLING, never to the device directly:**
  - a belt END at an Intake coupling's port hands the husk in; a belt START at an Output coupling's port is fed by it;
  - a Container Hose starts on an Output (or Both) coupling, runs through any bend points the player clicks, and ends on an Intake one; a quick right click on it reverses its flow;
  - placing snaps to coupling ports AND free socket ports (`PixelCouplings.Ports`, `PixelContainerHose.Snap`).
- **ONE device hook for husks in:** `IPixelSocketDevice.AcceptHusk(socket, husk)` - the husk is kinematic and still on the husk list; the device
  takes it (true) or refuses (false, e.g. full). A refused husk is ejected by whatever brought it, like before (a belt throws it on, a hose drops it).
- **Out:** a device that emits a husk calls `PixelCouplings.Emit(device, socketId, husk)`: with an Output coupling on that socket and a belt
  starting at its port (with room), the husk goes onto the belt; otherwise it returns false and the device does its own thing (fling / drop).
  `PixelCouplings.OutputHasRoom` lets a device wait (the Funnel's backpressure).
- **Optional extras (not every device):** `IPixelHuskSource` (a hose may pull stored husks out: Storage Crate, Quantum Organizer, Junction Box, Husk Source),
  `IPixelHuskPicker` (the same, but only a husk a 'want' test accepts), `IPixelHuskRoom` (how many more husks fit: a hose only pumps what fits) and
  `IPixelHuskFilter` (which types it takes: Biomass Generator = wood, Crafting Cube = what its recipe still needs). No default interface methods (Unity).
  `PixelCouplings.EmitStored` = a storing device lets one stored husk out onto what is attached (Storage Crate `out`, Quantum Organizer `side` set to Out).

## Auto-fit (2026-10-20)
- A belt END at a free socket's port fits an **In** coupling there, a belt START an **Out** one (`PixelCouplings.AutoFitAll`: run when a belt is placed and on every
  `PixelSocketPlates` refresh, 0.3 s, so a device carried next to a belt or loaded later joins too). A hose end fits one the way the hose runs (`AutoFitAt` from the hose's latch).
- An Intake socket only takes In, an Output socket only Out; a Both socket follows what attached (`SetDirection`); an auto socket (Junction Box) works it out itself.
- A just-laid hose may also turn an existing Both coupling to match, but only while nothing else is attached to it.
- Auto-fitted couplings stay (they are not taken off when the belt goes) and save with the device like before (`Export` / `Import`).
- The Coupling shop item is retired (dev-only, `consumablePriceVersion` 7); the Dev Hub's Dev items / Tester card still fit one by hand.

## Type-aware intake (2026-10-20)
- `PixelCouplings.Takes(device, socket, tier)` asks `IPixelHuskFilter` (true when the device doesn't filter).
- The Container Hose pumps only while the target has room AND, when its source is an `IPixelHuskPicker` (crate, organizer, Junction Box), takes out only a husk
  the target takes - a mixed crate hosed into a Biomass Generator sends just the wood. Its idle line says "Nothing the target takes in the source".
- The Kiln takes any amount (each husk burns on its own); the Crafting Cube's room is per type (its filter covers it).

## Connection resolver (2026-10-20)
`PixelCouplings.Attached(coupling)` = what it is joined to: `BeltIn` / `BeltOut` (a belt ending / starting at it), `HoseIn` / `HoseOut` (a hose pushing / pulling),
`DeviceOut` (another device's In coupling right in front), `Nothing`. Worked out on demand, never per frame for everything. Used by:
- the device panel's port rows (`AddPortRows` -> `Describe`: "Port side: Out: feeds a belt", "Port in: free - a belt or hose attaches here");
- the Junction Box's auto direction;
- the hose: when the player flips an end's coupling (its teal dot - only the dot is clickable now) the hose FOLLOWS (reverses) when the other end can work
  that way, else lets go of that end and idles until it is switched back.
- **Saving:** couplings are saved WITH THEIR DEVICE - `PlacedState.couplings` for shop devices, `TesterSave.couplings` for tester machines
  (a list of socket ids); belts re-attach after a load by position as before.
- **Unchanged on purpose:** a device's own natural intake (husks falling into the Kiln mouth, the Recycler's open top, the Trash Can's suction)
  works with or without couplings - couplings are only for belts and hoses.

## Husks only (2026-10-20)
Sockets carry husks only: `PixelSocketKind` keeps just `Husk = 0` (the field stays so saved data reads). The Power / Signal / Water / Heat / Link kinds
planned earlier were dropped - power will be its own mechanic, not ports. Signal / Water / Heat ideas stay in `Assets/Resources/Ideas.txt`.

## Later (noted, not built)
- BOUGHT (2026-10-17; the Coupling RETIRED 2026-10-20, couplings fit themselves): the Coupling (40 Gray + 40 Black, hold 10) and Container Hose (80 White + 80 Black, hold 3) are shop items in Utilities, right after the Recycler and
  Sorting Hopper; right-click one to start placing (PixelInstantItems -> PixelFeatureTester.StartFeature with a 'spend one' callback: one is used up per
  coupling fitted / hose laid, placing stops when none are left). The Dev Hub Tester cards still place them free. Unlocking them by progression is still open.
- A device can have more sockets than you fill (the Junction Box has eight).

## Build order (how it was moved over)
1. Sockets + coupling + hook next to the old interfaces. 2. Devices moved one at a time: Storage Crate, Kiln, Trash Can, Splitter, Elevator,
Container Hose, Connector, then the Funnel and Chipper outputs, then the Recycler + Sorting Hopper (+ Quantum Organizer for hoses) as new
intakes. 3. `IPixelBeltAcceptor`, `IPixelPort` and the hand-made output latches deleted, so there is one way to connect. 4. The Conveyor Connector
and Hose Adapter were then deleted as no longer needed, and the Container Hose became freely routed (bend points) with a right click to reverse it. The Belt Sorter's
chute (the same "find a belt that starts here" copy) moved onto a built-in bare socket too. Kiln got a second socket, its charcoal chute (Out).

## Where each device's sockets are
| Device | Sockets |
|---|---|
| Storage Crate | `in` (In, front left: belts / hoses in) and `out` (Out, front right: a hose pulls stored husks; a belt starting there is fed one stored husk at a time while it has room) |
| Quantum Organizer | `side` (Both, like the crate; In drops husks onto the pad; Out: a hose pulls stored husks, a belt starting there is fed one every 0.6 s) |
| Biomass Generator | `intake` (In, wood husks only: `IPixelHuskFilter`) |
| Crafting Cube | `front`, `back`, `left`, `right` (In, the types its recipe still needs: `IPixelHuskFilter`) |
| Kiln | `mouth` (In, burns it), `chute` (Out, charcoal) |
| Trash Can | `front`, `back` (In) |
| Recycler | `hatch` (In, the back hatch), `f0`..`f3` (In, four holes low on the front) |
| Funnel | `output` (Out, the spout end; the tube waits while its belt is full) |
| Wood Chipper | `chute` (Out, on the floor in front of the outlet) |
| Conveyor Elevator | 8 built-in bare sockets (2026-10-20): `in0`-`in3` (Intake) at the base on each side and `out0`-`out3` (Output) at the top on each side, at the belt snap points; puts husks out with `Emit`, waits while the belt is full, flings when nothing is attached. |
| Belt Jump | Built-in bare sockets: `in` (Intake, ramp start) and `out` (Output, landing end); the landing waits while the belt there is full. |
| Conveyor Combiner | Built-in bare sockets: `inL` / `inR` (Intake), one per side inlet; husks merge onto the main belt or wait (up to `queueSize`, then it refuses and the feeding belt holds). |
| Sorting Hopper | `green` / `orange` / `blue` / `pink` (Output, not built-in: a coupling fits itself when a belt starts there), one per shown output pipe, one husk below its mouth; `EmitStored`, waits while full; with nothing attached husks drop from the mouth. No inputs. |
| Husk Source (tester) | `front`, `back`, `left`, `right` (Out: endless husks of the picked type while something takes them) |
| Junction Box | `f0` `f1` (front), `b0` `b1` (back), `l0` `l1`, `r0` `r1` - all Both, **auto direction** |
| Belt Sorter | `chute` (Out, fitted, bare) |

## The Junction Box (built 2026-10-18, Devices session; design from the Design Discussion chat)
`PixelJunctionBox.cs`, DeviceKind.JunctionBox 26: a closed box like the Recycler's (carry handles) with **eight sockets, two per side, all Both + `autoDirection`**.
- **Auto direction** (`PixelSocket.autoDirection`, `PixelCoupling.SetAuto`): a coupling's way comes from what is attached - a belt ENDING at its port
  (`PixelCouplings.BeltEndingAt`) or a hose PUSHING into it (`PixelContainerHose.Pushes`) = In; a belt STARTING there (`BeltStartingAt`) or a hose PULLING
  from it (`PixelContainerHose.Pulls`) = Out; nothing = idle (no arrow; counts both ways, so belts and hoses can snap to it). Rechecked every `routeSeconds`.
- **Routing** (AcceptHusk): 1) the Out coupling whose FILTER is the husk's type (waits in the buffer while that one is full); 2) else round robin over the
  unfiltered Out couplings with a belt that has room (`Emit`); 3) else the BUFFER (`bufferSize` 8, data); full = refused. Hoses pulling from an Out coupling
  take from the buffer (`IPixelHuskSource`: the filter type there, or any type no filtered output wants).
- **In = deleted, out = new (2026-10-18):** every husk that goes in is deleted (it shrinks into the box) and its type + value come out as a NEW husk
  spawned at an output onto its belt (or wait in the buffer). **Top opening:** a square hole in the lid takes husks that drop in, with a gentle suction
  for husks just above the box (Inspector: suckRadiusHusks, suckHeightHusks, suckStrength). A Sorting Hopper's feed pipe reaches over the opening.
- **Filters**: one per coupling - drop a husk you were holding on it, or click / scroll it (unlocked types + none); a small block spins over it; hover shows the card.
  The Sweeper sees it as a container with room (buffer room).
- Saved as a placed device (`PlacedState.extra`: filters + buffer). Price / unlock: the **Shops** session (monochrome, as machines are; a placeholder Gray + Black cost is in the entry, `vendorOnly` so it is not sold yet).

### Overlap with the old tester pieces (reported 2026-10-18)
- **Splitter** (tester): round robin out of three sides - fully covered by an unfiltered Junction Box. **Removed** the same day at the user's request.
- **Belt Sorter** (tester): sends one type off to one side of a belt - covered by a Junction Box with one filtered Out coupling and one unfiltered one.
  It still differs: it sits IN a belt line (no couplings needed) and is cheaper in space. **Recommendation:** retire it once the Junction Box is on sale
  and players have it; until then keep it as the in-line option.

## Adding a device
Implement `IPixelSocketDevice` (SocketRoot, Sockets, SocketTint, AcceptHusk), build the socket list once the device knows its size, call
`PixelCouplings.Register` / `Unregister` in OnEnable / OnDisable, and put husks out with `PixelCouplings.Emit(this, id, husk)` (false = do your own thing).
A piece that is a belt fitting itself marks its sockets `fitted` (and `bare` if its own shape shows the way) and calls `PixelCoupling.FitBuiltIn(this)`
after building (and `PixelCoupling.RemoveAll(this)` before a rebuild). If it takes only some types, implement `IPixelHuskFilter`; if it stores husks a hose can
pull, `IPixelHuskSource` (+ `IPixelHuskPicker`). Nothing else: auto-fit, the port plates, the device panel rows, saving, snapping and the belt system pick it up.

**Link rules (2026-10-20, conveyor audit).** One reach for every link check: `PixelCouplings.LinkReach` (1.3 husk widths, flat): a belt end hands in to a coupling (`HandIn`) and a coupling finds the belt ending / starting at it (`BeltEndingAt` / `BeltStartingAt`, nearest wins) at the same distance, so a belt that shows as joined really is. `Emit` never puts a husk on a FULL belt (it returns false); devices that must not drop it ask `OutputHasRoom` first and hold it (Kiln, Wood Chipper, Belt Jump landing queue, Funnel, Junction Box, Husk Source, Storage Crate). The Belt Sorter lets a husk ride on when the belt off its chute is full.
