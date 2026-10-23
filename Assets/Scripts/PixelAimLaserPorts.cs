// ---- System notes ----
// PixelAimLaserPorts.cs
//   Put on a placing ghost by PixelAimLaser.Attach: every frame the ghost is shown it asks for a red aim beam out of each of its coupling holes,
//   and a wide one out of each opening marked with PixelAimOpening (found once, on its first frame).
// ---------------------------------------------------
using UnityEngine;

/// <summary>On a placing ghost: asks for its coupling-hole beams every frame it is shown (PixelAimLaser.Attach).</summary>
public sealed class PixelAimLaserPorts : MonoBehaviour
{
    [HideInInspector] public PixelSocket[] sockets;

    private PixelAimOpening[] openings;

    private void Start() => openings = GetComponentsInChildren<PixelAimOpening>(true);

    private void Update()
    {
        PixelAimLaser.Ports(transform, sockets);
        PixelAimLaser.Openings(transform, openings);
    }
}
