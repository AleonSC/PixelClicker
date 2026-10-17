# Sockets and couplings - one way to connect belts and hoses to devices

Agreed in the Design Discussion chat (2026-10-17), built in the Devices session. Code: `PixelSockets.cs` (sockets, kinds, the device
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
  it, at belt-underside height), outward facing, `flow` = Intake / Output / Both, and a `kind`. Sockets sit on openings the device already has:
  Kiln mouth, Storage Crate front, Trash Can side, Recycler back hatch (shared with the Sorting Hopper: a latched hopper occupies it), Sorting
  Hopper back wall, Quantum Organizer side, Wood Chipper chute, Funnel output, Elevator entry / top, Splitter entry / three sides, Connector ends.
- **Couplings (one shared piece).** ONE model (PixelFactoryKit) and ONE component. Placing it near a free socket snaps it on; it is parented to
  the device so it moves with it (carry handles, turning); removing the device removes its couplings. It is tinted a little with the device's
  colour (`SocketTint`) so it reads as a flange, not a bolt-on.
- **Direction is shown on the coupling:** a green arrow pointing INTO the device = Intake, an orange arrow pointing OUT = Output, teal arrows
  both ways = Both.
- **Fitted couplings.** The factory pieces that ARE belt fittings (Elevator, Splitter, Connector) come with couplings fitted on their sockets
  (`PixelSocket.fitted`): built with the piece, not saved, not removable on their own. Everything else needs a placed coupling.
- **Belts, hoses and connectors connect to a COUPLING, never to the device directly:**
  - a belt END at an Intake coupling's port hands the husk in; a belt START at an Output coupling's port is fed by it;
  - a Connector's ends snap to coupling ports; a Container Hose's source end snaps to an Output (or Both) coupling, its target end to an Intake one;
  - placing snaps to coupling ports (`PixelConveyor.Snap`, `PixelConnector.Snap`, `PixelContainerHose.Snap` all ask `PixelCouplings.Ports`).
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
  works with or without couplings - couplings are only for belts, hoses and connectors.

## Socket kinds (an open list)
The kind is an enum appended over time (`PixelSocketKind`: Husk, Power, Signal, Water, Heat, Link), NOT a yes / no "takes husks" flag. Each later
kind = a new enum value + its own coupling look + its own hook, never a new system. Built: **Husk** only (belts, hoses, connectors).
- **Power (plan next, Electrician):** today Wire / Battery / Resistor husks and Electric pixels arc to devices in their own hand-made code
  (PixelElectricLinks, `HoldPower`); that should move onto power sockets (e.g. a powered Recycler works faster).
- **Signal (plan next, the planned Automation skill):** on / off triggers between devices (a full Storage Crate starts a Recycler, a hopper opens
  when a colour arrives).
- Water, Heat and wireless Link: `Assets/Resources/Ideas.txt` ('Device connections beyond husks') for when Farming / Firemaking / the late game need them.

## Later (noted, not built)
- Couplings could be bought / unlocked (Standard or Devices progression) instead of free in the Tester.
- A device can have more sockets than you fill (the Splitter already does).

## Build order (how it was moved over)
1. Sockets + coupling + hook next to the old interfaces. 2. Devices moved one at a time: Storage Crate, Kiln, Trash Can, Splitter, Elevator,
Container Hose, Connector, then the Funnel and Chipper outputs, then the Recycler + Sorting Hopper (+ Quantum Organizer for hoses) as new
intakes. 3. `IPixelBeltAcceptor`, `IPixelPort` and the hand-made output latches deleted, so there is one way to connect.
