// ---- System notes ----
// PixelFunnelDevice.cs
//   The placed Funnel as a DEVICE (PixelConsumables DeviceKind.Funnel 16): the same permanent stub as the Wood Chipper (PixelPermanentDevice) but only the OUTPUT parts (base plate and collar) count
//   when the game asks "is the pointer over this device" (removing it with the right button): the big cone, rim, legs and tube hang round the cube and must not catch clicks meant for it.
//   All the behaviour is in PixelFunnel.
// ---------------------------------------------------
using UnityEngine;

public class PixelFunnelDevice : PixelPermanentDevice
{
    // (only the plate and collar count as 'on' the funnel; PixelPlacedDevice.HitTest does the rest with a cached part list)
    protected override bool CountsForHit(Renderer r) => r.name == "Plate" || r.name == "Collar" || r.name == "Collar Base";
}
