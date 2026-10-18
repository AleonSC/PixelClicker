# Device card and panel (PixelDeviceCard.cs, PixelDeviceInfo.cs, PixelWorldRing.cs)

One shared info card and panel for every PLACED device (design agreed in the Design Discussion chat, built 2026-10-18).

## How the player uses it
- **Hover** a placed device for `hoverDelay` (0.5 s): a small card beside the pointer (the hover-tooltip spot, `PixelUIKit.PlaceTooltip`) with the essentials: name, time / uses / wind left as a bar, the 2-3 key stats, contents (6 / 10). The Sorting Hopper's flag card was the model.
- **Right-click (tap)** a placed device: a small Tool window (`PixelUIKit.CreateWindow`, theme colours) with the full sheet (run time, reach, Operation upgrade levels, what it did this session) and two buttons: **Remove** (asks once: "Sure?") and **Upgrades** (opens the shop on Upgrades > Operation with that device's upgrade window, `PixelShop.OpenOperationFor`; only for devices with an Operation row). Escape (`PixelWindows`, priority 45), a click outside it, or the device going away closes it.
- **Right-hold** still removes a device straight away (`PixelConsumables.UpdateRemoval`, unchanged); a tap is a press released within `tapSeconds` (0.3 s).
- **Left-click** is unchanged (Recycler button, Crate storing, hopper flag, Sweeper winding, Organizer release, switching a device on...).

## How it fits together
- Who owns a right click: `PixelRightClick` has a **Device** owner (after Tool / Pet, before WorldPart / Cube / Husk); `PixelRightClick.Device` is the device. The card asks the same owner for its hover, so it never fights the pet menu, tools, the cube or husk crush.
- The card waits while the block / husk card is wanted (`PixelBlockTooltip.HoveringBlock`: pointing at a husk in a bin shows the husk), over a part that claimed the pointer (`PixelPointerClaim.ButtonClaimed`: the Recycler button, the hopper flag), while carrying (`PixelCarryHandles.Hot`) and while the right button is held.
- **Lines come from the device:** `PixelPlacedDevice.BuildInfo(PixelDeviceInfo)` writes the common part (name, time / uses left bar, 'Click to start', powered, attuned; Full: run time, reach, Operation levels via `PixelShop.AddOperationLevels`), then the subclass's `AddInfo` override, then every `IPixelDeviceInfo` component on the same object (behaviour next to a thin device stub). `info.Full` = panel.
  - Providers so far: Recycler (inside n / cap, last streak; panel: pay share, shake time, size, session husks / batches / paid), Sorting Hopper (filter, feeds a Recycler or not, holding n / room; panel: each type held, feed gap, session into the bin / out of the bottom spout, and its own buttons Select filter + Empty out), Sweeper (wind bar, what it is doing; panel: range, session rescues), Storage Crate (stored n / 10; panel: each section), Quantum Organizer (stored n / cap, each bound type x count; panel: bound types, session taken in / let out). Every other device shows the common lines.
  - A new device: override `AddInfo` or implement `IPixelDeviceInfo`; nothing else to wire.
- **Device actions (2026-10-18, Devices session):** in the panel (`info.Full`) a device can add buttons of its own with `info.AddAction(label, onClick, role)`; `PlaceActions` shows them two to a row above Remove / Upgrades (a last odd one full width) and refreshes the panel after a click. First user: the Sorting Hopper (Select filter opens `PixelSortingHopperWindow`, Empty out).
- Refreshed every `refreshSeconds` (0.25) while shown, not every frame.

## Floating world text trimmed (2026-10-18)
The card now carries the numbers, so the floating text over devices became a small camera-facing ring (`PixelWorldRing`, two LineRenderers, redrawn only when the fill changes):
- **Recycler:** the 'n / 25' counter, its 'full - press the button' line and the after-payout 'Type streak x1.3 pay' text -> a ring that fills with the contents (amber when full). The hover arrow's 'm:ss left' -> arrow only.
- **Quantum Organizer:** the list of bound types with counts and 'n / 60' -> a ring for how full it is (the 'Drop a pixel on the pad' hint stays until a type is bound).
- **Every timed device** (Vacuum, Fan, Sorter, Magnet, Sprinkler, Magnetized Sorter, Quantum Organizer...): the '30s' countdown (and its POWERED line) -> a ring that empties with the time (amber under 20%, blue while powered).
- Kept as text on purpose: 'Click to start' on a device not yet switched on, the Sweeper's 'click to wind', the uses-mode labels (Charge Booster CLICK! / SUPER!, Lightning Rod 'n x'): they tell the player what to do.
