# Coupler audit (sockets + couplings, 2026-10-20)

A read-only pass on `PixelSockets` / `PixelCoupling` / `PixelSocketPlates`, every device with sockets, the hose and how couplings are bought, placed, saved and shown. It skips what `ConveyorAudit.md` and `DeviceConsumableAudit.md` already cover:
- one join reach and the nearest match;
- `Emit` waiting for room;
- the Storage Crate's out port (fixed by making the crate feed a belt);
- belts spilling at a busy device;
- the material leaks;
- the shared placed-object base.

Effort: S < 1 h, M = a few hours, L = a day.

**Done 2026-10-20 (sockets as the only husk path, Devices work):** C1, C2, C3, C4, C5; polish 1 (auto-fit + snap to free ports), 2 (moot: the Coupling item is retired, couplings fit themselves), 3 (device panel port rows; no red end marker yet), 4 (tester text); design "separate purchase" (dropped); code structure 1 (moot: sockets are husk-only, power will not use ports), 2 (`FreeSockets` / `HuskWidth`), 3 (Occupy / Vacate / Occupied gone; the Elevator / Hopper items are the parallel fork's); shared systems 2 (`Attached` resolver) and 3 (`IPixelHuskFilter` / `Takes`, used by the hose). Still open: speed item 4 (`On` dictionary, cached attached belt).

## Bugs (confirmed by reading the code)

| # | Where | Bug | Fix | Effort |
|---|---|---|---|---|
| C1 | `PixelQuantumDevice.cs:104` | Its `side` socket is Both; set to Out a belt can start there, but the organizer never `Emit`s, so the belt stays empty (the same bug the crate had). | Make it emit like the crate now does, or don't offer Out on devices that never emit. | S |
| C2 | `PixelContainerHose.cs:298, :311` | The hose asks only HOW MANY a target takes, not WHICH type, and takes the husk out of its source before the target agrees. A mixed crate hosed into a Biomass Generator (wood only), the Crafting Cube or the Kiln (no room check: 6 at a time) spills the refused husks out of the hose end. | Ask the target first: `Takes(socket, tier)` on the room interface, or a "look before taking" call on the source. | S-M |
| C3 | `PixelBiomassGenerator.cs:202 / :219` | `AcceptHusk` says yes even when `Burn` bails out (`ReleaseOldPixel` failed), so the husk stays locked at the belt / hose end. | `Burn` returns a bool. | S |
| C4 | `PixelCoupling.cs:111`, `PixelSocketPlates.cs:76`, `PixelRecycler.cs:676` | Couplings and port plates set their scale to 1 / device scale once. The Recycler later rescales itself (Bin Size upgrade), so couplings fitted before (or restored on load) grow or shrink with the bin. | Rescale when the device's scale changes, or hang sockets on an unscaled root. | S |
| C5 | `PixelCoupling.cs:207-216` | A click anywhere on a Both coupling flips it (the click test uses the whole coupling's bounds, not the teal dot), and a hose already attached keeps pumping the old way (it never re-checks `IsOut` / `IsIn`). | Hit-test the dot only; the hose re-checks direction. | S |

## Player polish

1. **Free socket plates look like working ports, but belts and hoses don't connect to them.** The link queries only look at couplings, so a belt ended on a bare plate doesn't snap and spills with no message. Snap to free plates too, and either fit a coupling automatically or say "needs a Coupling". This is the biggest mistake a player can make here. **S-M**
2. **Bought couplings are lost without a refund** (40 Gray + 40 Black each) when:
   - one is removed with the right hold;
   - its device is removed (couplings are its children);
   - a timed device ends (the 5-minute Recycler).

   Give them back. **S**
3. **Nothing shows what's connected.** The device card has no coupling rows and a coupling has no hover card. Add "In: belt from X / Out: idle (no belt)" per socket, plus a red end marker when carrying a device away breaks its links. **S-M**
4. **No coupling tip or tutorial line** (the Junction Box has one). The tester card text (`PixelFeatureTester.cs:145`) still mentions the Hopper's removed back socket and "click an organizer dot". **S**

## Design

- **Should the coupling be a separate purchase? Probably not.**
  - Every socket already shows its plate and direction, and only the Quantum Organizer's Both socket is a choice (the Junction Box's are automatic).
  - Buying couplings adds cost and a step, but no decision.

  Proposal: fit the coupling automatically when a belt or hose snaps to a free socket. Drop the item, or turn it into the planned Coupling-settings upgrade (to-do 115). **M**
- **The Sorting Hopper has no sockets.** It has 1-4 sorted outputs (pipes, spout) but is outside the socket system: a 4th hand-made output path. Out sockets on its pipes would make it the early belt feeder the game lacks (see ConveyorAudit section 5). **M**
- **Planned work that matters most here:**
  - **Build mode (117)** is the natural home for the socket / connection view and the auto-fit.
  - **Power sockets:** power is already wired by hand in 4 places (Solar Panel click-link, Biomass radius, Power Bank, Electric links). The Solar Panel is sold now, so every new power source adds to the later move-over.
  - **Coupling settings (115)** overlaps the Junction Box filters; do it after Build mode.

## Code structure

1. **Not ready for Power / Signal yet.**
   - `IPixelSocketDevice` forces `AcceptHusk` on every device (:86).
   - `IntakeAt`, `NearestCoupling` (:284), `Ports`, `Emit` and the hose's `Snap` (:127) don't filter by kind, so a Power coupling would be offered to belts and handed husks.

   Add a kind filter on the shared queries and one hook interface per kind (`IPixelHuskIntake`, later `IPixelPowerIntake`). Do it before Power. **S-M**
2. **The same "free Husk socket" loop is copied 4 times** (`PixelCoupling.cs:248, :290, :330`, `PixelSocketPlates.cs:45`), and the husk-width getter 4 times. Use one `PixelCouplings.FreeSockets(kind)`. **S**
3. **Dead code:**
   - `Occupy` / `Vacate` / `Occupied` (`PixelSockets.cs:152-160`) have no callers;
   - `PixelElevator` still implements `IPixelSocketDevice` with zero sockets (:17, :137, :195);
   - the header still mentions the Hopper and connectors. **S**
4. **Speed (fine at today's scale):**
   - `PixelCoupling.On` is a linear scan;
   - every `Emit` / `OutputHasRoom` / `OutputConnected` also scans for the belt starting there, and the crate, Husk Source and Junction Box ask every tick;
   - the plates rebuild string keys every 0.3 s.

   Use a dictionary keyed by (device, socket id) and cache the attached belt per coupling on a topology version (the same idea as ConveyorAudit's `PixelBeltLink`). **S-M**

## Shared systems worth building

1. **Per-kind hook interfaces + kind-filtered queries in `PixelCouplings`**, so Power and Signal plug in without touching the belt code.
2. **One "connection" pass:**
   - auto-fit the coupling on snap;
   - one place that resolves what each coupling is attached to (belt, hose, device);
   - used by the card rows, Build mode's view, the Junction Box's auto direction and the hose's direction re-check.
3. **A type-aware intake question** (`Takes(socket, tier)`), used by the hose, Sweeper, belts and Junction Box before a husk is moved.

**Guesses:**
- How often C2 happens depends on players hosing a mixed crate into a Biomass Generator or the Crafting Cube.
- C4 may also hit devices that grow in when placed; it's only confirmed for the Recycler.
