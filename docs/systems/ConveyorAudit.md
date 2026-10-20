# Conveyor belt audit (2026-10-20)

Two read-only passes: one on the code (belts, riders, couplings, the tester factory and every piece that feeds or takes from a belt), one on the player and designer side (placing, feedback, purpose, feel). The two strongest bug claims (B1, B3) were checked again by hand. Builds on `DeviceConsumableAudit.md` and doesn't repeat it. Nothing changed yet. Effort: S < 1 h, M = a few hours, L = a day.

**Also done 2026-10-20 (belt core):** B3, B4, B9, B10, the Elevator's room check, the belt panel Status line + spilling-end marker, the green / red end disc while placing, and undo while placing (right click takes the last piece of the chain back).

**Done 2026-10-20 (pieces + joins):** B1 (sorter spot from the belt's current start), B2 (sorter / combiner refunded when their belt goes, the sorter's filter block handed back, tester list tidied), B5 (sorter catches the whole stretch crossed per step), B6 (one `LinkReach` 1.3 widths, nearest belt wins), B7 (`Emit` never fills a full belt; Kiln / Wood Chipper hold their output, Belt Jump queues on its landing lip, the Belt Sorter lets the husk ride on when its chute belt is full), B8 (Belt Jump locks the husk before `HandIn`), B11 (the Storage Crate feeds a belt on its `out` coupling), polish 1 (placing hints shown in the HUD status box when placing from the shop), polish 5 (stale docs). B3, B4, B9, B10 and polish 2-4 are the belt-core half.

**Already good:**
- **Riders never fade.** Husks on a belt never despawn.
- **Corners and joins:** corners and shrouds close the gaps, and snapping is generous.
- **Belt panel:** it shows the section, speed and size, Reverse and Remove.
- **Most outputs wait when the belt is full:** the Funnel, Junction Box and Combiner hold back.
- **One way to connect:** couplings are a clean idea.

## 1. Bugs (confirmed by reading the code)

| # | Where | Bug | Fix | Effort |
|---|---|---|---|---|
| B1 | `PixelBeltSorter.cs:218` | The sorter is placed by `along`, measured from the belt's start when it was placed. Reversing the belt teleports it to the mirror spot, and sorting / joints then use the wrong spot. (The Combiner already does it right: `belt.Along(transform.position)`.) | Same as the Combiner. | S |
| B2 | `PixelBeltSorter.cs:214`, `PixelConveyorCombiner.cs:158` | Removing their belt silently destroys them: the sorter's filter block is lost, nothing is refunded, and dead entries stay in the tester's list. | Hand back / refund; tidy the list. | S |
| B3 | `PixelFactoryKit` (materials cached per kit) + belt `Rebuild` (`PixelConveyor.cs:402`) | A new kit is made on every rebuild and `DestroyMaterials` is never called. While placing, the preview rebuilds on every mouse move, about 8 new materials a frame, never freed. Same in highlights, shrouds, elevator troughs, corner joints (their `Mesh` is never destroyed either) and each lever's lamp. | One static material cache keyed by look; destroy the joint meshes. | S |
| B4 | `PixelConveyor.ImportState` (:773) | A reloaded reversed belt shows the wrong arrow colour (no `PaintArrows`). | Call it. | S |
| B5 | `PixelBeltSorter.TryDivert` (:357) | Diverts only within 0.2 cube of the sorter, but belts run up to 30 cubes/s (0.6 cube per physics step), so fast belts skip it. | Test the stretch crossed this step. | S |
| B6 | `PixelSockets.cs:212-232` `BeltEndingAt` / `BeltStartingAt` | Reach 1.3 cubes and the FIRST match, while `IntakeAt` / `HandIn` use 1.0 and `FindNext` 0.45. A grid-snapped end 1.0-1.3 cubes off a Junction Box shows IN, but the belt throws the husk off. | Nearest match, one shared reach. | S-M |
| B7 | `Emit` / `PutOnBelt` | Don't check room themselves. The Elevator's `PutOut`, Belt Jump `Land`, Sorter `Eject`, Kiln (:307) and Wood Chipper (:346) put husks onto full belts, where they pile up or fall off. | Check room inside `Emit` (one place). | S |
| B8 | `PixelBeltJump.Land` -> `HandIn` | Hands in a husk whose physics is already on, while `HandIn` expects a locked (kinematic) one; the Elevator and Hose lock it first. | Lock it first. | S |
| B9 | Belt ends at a machine (`PixelConveyorSystem.FixedUpdate`) | When the machine refuses (Recycler busy / full / gone, Crate full), the belt throws the husk on the floor instead of waiting. This is the biggest "why are my husks on the floor" moment. | At an In coupling, hold the front rider (as the Funnel's `OutputHasRoom` does); only a bare end spills. | S-M |
| B10 | `PixelPathCarrier.carriedAll` | Cleared only on a domain reload, never on Restart. | `PixelStatics.ResetOnRestart`. | S |
| B11 | Storage Crate `out` coupling | **Likely:** a belt can snap to it, but the crate never `Emit`s (hose only), so that belt stays empty. | Don't offer that port to belts, or make the crate emit. | S |

## 2. Player-facing polish

1. **Placing hints are invisible from the shop.** The live line ("Scroll: raise / lower... Height 2.0 cubes (snapped to an end)") goes to the Dev Hub Tester tab's label, which is closed when placing a Belt Kit / Elevator / Jump / Combiner / Sorter from the shop. Players never learn that scroll raises a belt, or whether an end snapped. Show it on a HUD hint line while placing from the shop. **S**
2. **No reason shown when a line stops or spills.** The hose has an idle reason; belts have none. Add one state per section ("end: spilling, no coupling", "Recycler full", "stopped by lever", "next belt at another height") to the belt panel, plus a small marker at a spilling end. **S-M**
3. **Ends that look joined but aren't:**
   - `FindNext` needs ends within 0.45 cube at the same height (±0.45), so head-on pieces or a small height step become spill points;
   - the 120° turn limit only says "pick a gentler angle" on click.

   Fix: tint the ghost's end green / red when it will or won't chain. **S**
4. **Undo and moving:**
   - no undo while placing (the hose has right-click to take back the last piece);
   - belts can't be moved, only deleted piece by piece (0.8 s hold each) and laid again.

   Right-click while placing removes the last piece (S). Moving comes with the shared placed-object base (section 4). **M**
5. **Stale docs:**
   - `PixelFactory.md` says "right click reverses";
   - `PixelCouplings.md` lists the Hopper's removed `back` socket;
   - the tester header and `ConnectionMap.md` say belts are "Electrician 5", but they're sold to everyone;
   - the Elevator header says it puts husks out via its top coupling, but it uses its own links (that socket is unused). **S**

## 3. Speed

- **Capture loop** (`PixelConveyor.cs:1018-1036`): every loose husk x every belt per physics step, about 5 checks + 2 lookups + `InverseTransformPoint` each. Skip sleeping bodies and keep a world bounds box per belt for a cheap first reject. `Eligible` is also a private copy of `PixelHuskHolder.Takeable`. **S-M**
- **Rider gaps are O(n²):** `RoomAt`, `RidersOn`, `StartHasRoom` (which also scans every husk on the floor) are linear; the same in `PixelPathCarrier.Step`. Fix: per-belt / per-lane rider lists sorted by position. Fine at tens of husks, slow at hundreds. **M**
- **Chevrons:** about 20 per belt are moved one by one every frame. Moving their parent by `Repeat(offset, spacing)` would mean only the end ones change. **S**
- **Polling with string building:**
  - every 0.5 s each belt rebuilds its joints and shrouds with string keys;
  - the Elevator's `RefreshLinks` is O(belts²);
  - a lever that's OFF runs `CollectSection` every 0.15 s (O(L²·B), a new array per belt pair).

  Fix: a world "topology version" number, so this reruns only when something changed. **M**
- **Holding right-click scans everything:** the tester's `ObjectUnderPointer` runs `GetComponentsInChildren` on every placed piece (~100 renderers per belt) every frame while right is held; `BeltUnder` does `RaycastAll` + a sort with a new lambda. **M**

## 4. Shared systems worth building

1. **`PixelBeltLink`:**
   - one reach / tolerance for every end check;
   - a cached belt topology (next, line, section, corner partner) rebuilt on a version bump;
   - nearest-match lookups;
   - one `Output(device, socket, husk)` that always checks room and holds or spills by one rule (fixes B6, B7, B9 and the polling).
2. **One carrier core:**
   - lane queues sorted by position, despawn keep-alive, let go when grabbed;
   - pool release and save / restore of carried husks;
   - used by belt riders, `PixelPathCarrier` and the Combiner's queue (today three copies).
3. **Static material / mesh cache in `PixelFactoryKit`**, keyed by colour, metal, smoothness, emission and ghost (fixes B3 for every factory piece).
4. **One declaration per tester piece + one placed-object base:**
   - one entry per piece gives its ghost / place / snap / save / stage (today about 15 hand-written blocks of about 6 delegates in `PixelFeatureTester`);
   - a shared "mounted on a belt" component for the Sorter / Combiner / Lever (spot from the belt, re-find after a split, refund when lost);
   - every piece goes through `PixelRightClick`, the device card and the normal removal hold.

   This is the same job as `DeviceConsumableAudit.md` section 3 item 4, so do them together. It's also what Build mode (to-do 117) needs.

**Also hand-wired:**
- **(Done 2026-10-20: sockets are the only way in; the Elevator, Belt Jump and Combiner have built-in sockets.)** Was: **Three ways into a machine:** coupling `AcceptHusk`, `IPixelAutoIntake.CatchNear` (the Elevator catches any belt end within 3.35 cubes), and the belt-end throw. Nothing in the world shows which one is active.
- **Spawn-and-grab:** 7 places "spawn a husk then take the last one in the list" because `SpawnStoredPixel` returns a bool instead of the body.
- **Missing from the system list:** `PixelConveyorSystem` isn't in `PixelSystems.All` (made from 4 places).

## 5. Design (for Design Discussion / Devices)

- **No reason to build a line yet.**
  - The kit (250 Gray + 150 Black, about 6 min in, never used up, free length) is sold beside the Coupling, Hose, Junction Box, Sorter, Elevator, Jump and Combiner.
  - What a normal player can feed a belt with: chance drops, the Wood Chipper, or sorter outputs that need a feed themselves. The Funnel, the natural feeder, is dev-only.
  - The only real end is the timed Recycler.

  The planned fixes, in order of impact:
  1. Build mode (to-do 117), with the factory parts gated together behind the first transport;
  2. vendor orders + delivery pad (118), the first reason to route particular husks;
  3. products (119-120), so machine outputs have a use.

  The Workbench (109) is a strong fourth.
- **Dead ends:**
  - (2026-10-20: Charcoal is now Biomass fuel and the Lumberjack buys it; the Trash Can stays a dev tool.) The Kiln burns any husk (no filter) into Charcoal as a pixel type (against the "machines make products" rule); keep it out of the shop until Charcoal has a use (Biomass fuel fits now);
  - cut the Trash Can;
  - the Control Lever has no job until Signal sockets exist.
- **Throughput is free (guess).** About 1.45 husks/s per lane at the default speed. The panel allows up to 8 cubes/s at no cost, which removes the one thing to tune. Belt speed could be the Electrician lever, or later run faster while powered.
- **Feel:**
  - riding is smooth and predictable;
  - the Elevator and Jump are forgiving;
  - a U-turn takes two pieces;
  - (guess) a husk landing on a full spot lies on the belt as a loose body and looks like a jam.
