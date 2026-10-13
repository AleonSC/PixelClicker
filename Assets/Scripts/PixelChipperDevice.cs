// ---- System notes ----
// PixelChipperDevice.cs
//   The placed Wood Chipper as a DEVICE (PixelConsumables DeviceKind.WoodChipper 15, sold by the Lumberjack): a thin PixelPlacedDevice added to the machine PixelWoodChipper.Place builds, so
//   the chipper is bought, kept in the backpack, placed, saved / restored (position + turn; its health and chip count start fresh), and removed (hold the right button) like every other
//   device. Permanent (no countdown, needs no switching on). All the machine's behaviour is in PixelWoodChipper.
// ---------------------------------------------------
using UnityEngine;

public class PixelChipperDevice : PixelPlacedDevice
{
    public void Init(PixelClicker owner, Camera camera, float shrinkTime)
    {
        InitCommon(owner, camera, null, "{0}", 1e9f, shrinkTime);
    }
}
