// ---- System notes ----
// PixelPermanentDevice.cs
//   A thin PERMANENT PixelPlacedDevice (no countdown, needs no switching on) put on a machine that keeps its own behaviour in its own script, so the
//   machine is bought, kept in the backpack, placed, saved / restored (position + turn), removed (hold the right button) and shown on the device card
//   like every other device. Used by the Wood Chipper (PixelWoodChipper), the Funnel (PixelFunnelDevice), the Sweeper, the Junction Box, the Sorting
//   Hopper, the Recycler and the Biomass Generators. (Called PixelChipperDevice until 2026-10-20: the Wood Chipper was its first user.)
// ---------------------------------------------------
using UnityEngine;

public class PixelPermanentDevice : PixelPlacedDevice
{
    public void Init(PixelClicker owner, Camera camera, float shrinkTime)
    {
        InitCommon(owner, camera, null, "{0}", 1e9f, shrinkTime);
    }
}
