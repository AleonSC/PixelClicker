# Sockets and couplings - one way to connect belts and hoses to devices

Agreed in the Design Discussion chat (2026-10-17), BUILT in the Devices session the same day (not yet tested in Unity). Code: `PixelSockets.cs` (sockets, kinds, the device
hooks, the registry) and `PixelCoupling.cs` (the snap-on coupling piece). Placed from Dev Hub > Tester > **Coupling**.

## The problem it replaced
- Six machines each had their own belt intake (`IPixelBeltAcceptor` in PixelConveyor.cs: Kiln, Storage Crate, Trash Can, Splitter, Elevator,
  Conveyor Connector), each with its own "is this point at my opening" zone; the Container Hose fed them through the same interface.
- The Kiln and Crate also had a separate opening marker for the Conveyor Connector (`IPixelPort`).
- Outputs were hand-wired: the Funnel's floor output, the Wood Chipper's chute (plus its placing latch onto a belt), the Elevator top, the
  Splitter sides and the Connector's far end each had their own copy of "find a belt that starts here and drop the husk on it".
- The Recycler and the Sorting Hopper could not take a belt at all.

## The design
- **Sockets (data).** Every device declares its SOCKETS: a few lines each - `id`, local position (the PORT, where a belt end / hose end meets
  it, at belt-underside height), outward facing, `flow` = Intake / Output / Both, and a `kind`. Free sockets always show a small PORT PLATE (`PixelSocketPlates`: a chevron, green In / orange Out / teal Both). Sockets sit on openings the device already has:
  Kiln mouth, Storage Crate front, Trash Can side, Recycler back hatch (shared with the Sorting Hopper: a latched hopper occupies it), Sorting
  Hopper back wall, Quantum Organizer side, Wood Chipper chute, Funnel output, Elevator entry / top, Junction Box (eight, auto direction), Belt Sorter chute.
- **Couplings (one shared piece).** ONE model (PixelFactoryKit) and ONE component. Placing it near a free socket snaps it on; it is parented to
  the device so it moves with it (carry handles, turning); removing the device removes its couplings. It is tinted a little with the device's
  colour (`SocketTint`) so it reads as a flange, not a bolt-on.
- **Direction is shown on the coupling:** a green arrow pointing INTO the device = Intake, an orange arrow pointing OUT = Output, teal arrows
  both ways = Both.
- **Fitted couplings.** The factory pieces that ARE belt fittings (Elevator, Belt Sorter; the Splitter until it was removed) come with couplings fitted on their sockets
  (`PixelSocket.fitted`): built with the piece, not saved, not removable on their own. Everything else needs a placed coupling.
- **Belts and hoses connect to a COUPLING, never to the device directly:**
  - a belt END at an Intake coupling's port hands the husk in; a belt START at an Output coupling's port is fed by it;
  - a Container Hose starts on an Output (or Both) coupling, runs through any bend points the player clicks, and ends on an Intake one; a quick right click on it reverses its flow;
  - placing snaps to coupling ports (`PixelConveyor.Snap` and `PixelContainerHose.Snap` ask the couplings).
- **ONE device hook for husks in:** `IPixelSocketDevice.AcceptHusk(socket, husk)` - the husk is kinematic and still on the husk list; the device
  takes it (true) or refuses (false, e.g. full). A refused husk is ejected by whatever brought it, like before (a belt throws it on, a hose drops it).
- **Out:** a device that emits a husk calls `PixelCouplings.Emit(device, socketId, husk)`: with an Output coupling on that socket and a belt
  starting at its port (with room), the husk goes onto the belt; otherwise it returns false and the device does its own thing (fling / drop).
  `PixelCouplings.OutputHasRoom` lets a device wait (the Funnel's backpressure).
- **Optional extras (not every device):** `IPixelHuskSource` (a hose may pull stored husks out: Storage Crate, Quantum Organizer) and
  `IPixelHuskRoom` (how many more husks fit: a hose only pumps what fits).
- **Saving:** couplings are saved WITH THEIR DEVICE - `PlacedState.couplings` for shop devices, `TesterSave.couplings` for tester machines
  (a list of socket ids); belts re-attach after a load by position as before.
- **Unchanged on purpose:** a device's own natural intake (husks falling into the Kiln mouth, the Recycler's open top, the Trash Can's suction)
  works with or without couplings - couplings are only for belts and hoses.

## Socket kinds (an open list)
The kind is an enum appended over time (`PixelSocketKind`: Husk, Power, Signal, Water, Heat, Link), NOT a yes / no "takes husks" flag. Each later
kind = a new enum value + its own coupling look + its own hook, never a new system. Built: **Husk** only (belts, hoses).
- **Power (plan next, Electrician):** today Wire / Battery / Resistor husks and Electric pixels arc to devices in their own hand-made code
  (PixelElectricLinks, `HoldPower`); that should move onto power sockets (e.g. a powered Recycler works faster).
- **Signal (plan next, the planned Automation skill):** on / off triggers between devices (a full Storage Crate starts a Recycler, a hopper opens
  when a colour arrives).
- Water, Heat and wireless Link: `Assets/Resources/Ideas.txt` ('Device connections beyond husks') for when Farming / Firemaking / the late game need them.

## Later (noted, not built)
- BOUGHT (2026-10-17): the Coupling (40 Gray + 40 Black, hold 10) and Container Hose (80 White + 80 Black, hold 3) are shop items in Utilities, right after the Recycler and
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
| Storage Crate | `in` (In, front left: belts / hoses in) and `out` (Out, front right: a hose pulls stored husks) |
| Quantum Organizer | `side` (Both, like the crate; In drops husks onto the pad) |
| Kiln | `mouth` (In, burns it), `chute` (Out, charcoal) |
| Trash Can | `front`, `back` (In) |
| Recycler | `hatch` (In, the back hatch; a latched Sorting Hopper OCCUPIES it), `f0`..`f3` (In, four holes low on the front) |
| Sorting Hopper | `back` (In, takes the husk into what it holds; refuses when full, `holdRoom`) |
| Funnel | `output` (Out, the spout end; the tube waits while its belt is full) |
| Wood Chipper | `chute` (Out, on the floor in front of the outlet) |
| Conveyor Elevator | `in`, `out` (fitted) |
| Junction Box | `f0` `f1` (front), `b0` `b1` (back; a latched Sorting Hopper OCCUPIES both), `l0` `l1`, `r0` `r1` - all Both, **auto direction** |
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
- **Hosts a Sorting Hopper** (`IPixelHopperHost`, shared with the Recycler): the hopper occupies `b0` / `b1`, its feed pipe drops into the top hatch (`TakeFeed` routes it).
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
after building (and `PixelCoupling.RemoveAll(this)` before a rebuild). Nothing else: the tester, saving, snapping and the belt system pick it up.
