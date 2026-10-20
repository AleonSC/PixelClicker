// ---- System notes ----
// PixelAimLaserPorts.cs
//   Put on a placing ghost by PixelAimLaser.Attach: every frame the ghost is shown it asks for a red aim beam out of each of its coupling holes.
// ---------------------------------------------------
using UnityEngine;

/// <summary>On a placing ghost: asks for its coupling-hole beams every frame it is shown (PixelAimLaser.Attach).</summary>
public sealed class PixelAimLaserPorts : MonoBehaviour
{
    [HideInInspector] public PixelSocket[] sockets;

    private void Update() => PixelAimLaser.Ports(transform, sockets);
}
