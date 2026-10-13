// ---- System notes ----
// PixelFunnelDevice.cs
//   The placed Funnel as a DEVICE (PixelConsumables DeviceKind.Funnel 16): the same permanent stub as the Wood Chipper (PixelChipperDevice) but only the OUTPUT parts (base plate and collar) count
//   when the game asks "is the pointer over this device" (removing it with the right button): the big cone, rim, legs and tube hang round the cube and must not catch clicks meant for it.
//   All the behaviour is in PixelFunnel.
// ---------------------------------------------------
using UnityEngine;

public class PixelFunnelDevice : PixelChipperDevice
{
    public override bool HitTest(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        bool hit = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || (r.name != "Plate" && r.name != "Collar" && r.name != "Collar Base")) continue;
            if (r.bounds.IntersectRay(ray, out float d) && d < distance) { distance = d; hit = true; }
        }
        return hit;
    }
}
