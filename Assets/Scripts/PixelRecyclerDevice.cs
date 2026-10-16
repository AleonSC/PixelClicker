// ---- System notes ----
// PixelRecyclerDevice.cs
//   (extends PixelPlacedDevice) The Recycler's countdown (2026-10-16: the Recycler is a TIMED consumable, 5 minutes by default, Upgrades >
//   Operation 'Run Time'): placed by PixelConsumables.SpawnDevice with the device's (upgraded) duration, it starts at once (never disarmed),
//   shows no timer of its own (PixelRecycler writes 'm:ss left' under its counter) and shrinks away when the time is up (PixelPlacedDevice);
//   the husks still in the bin go with it. Saved / restored with the time left like every placed device.
// ---------------------------------------------------
using UnityEngine;

public class PixelRecyclerDevice : PixelPlacedDevice
{
    public void Init(PixelClicker owner, Camera camera, float duration, float shrinkTime)
    {
        InitCommon(owner, camera, null, "{0}", Mathf.Max(1f, duration), shrinkTime);
    }
}
