// ---- System notes ----
// PixelAimOpening.cs
//   An OPENING that shines a WIDE red aim beam while its device is placed (ghost) or carried (2026-10-23): a marker on a child at the centre of a
//   hole that husks drop into (the Junction Box's top opening first). The beam runs along the child's up (out of the hole), as wide as the hole
//   (width, local units, scaled with the device), and is cut off by the first thing in its way - so you can line the hole up under a spout, an
//   elevator's top or a Funnel. Drawn by PixelAimLaser.Openings (the placing ghost's PixelAimLaserPorts and PixelCarryHandles call it).
//   A device with an opening: add the marker in its Build (ghost and real model alike): PixelAimOpening.Add(parent, localPos, width).
// ---------------------------------------------------
using UnityEngine;

/// <summary>Marks a hole on a device: while it is placed or carried, a red aim beam as wide as the hole shines out of it (PixelAimLaser.Openings).</summary>
public sealed class PixelAimOpening : MonoBehaviour
{
    [Tooltip("Width of the hole (and of its beam), in the device's local units.")]
    [SerializeField] private float width = 1f;

    public float Width => width;

    /// <summary>Adds an opening marker under 'parent' at 'localPos', facing up (out of a hole in a lid).</summary>
    public static PixelAimOpening Add(Transform parent, Vector3 localPos, float width)
    {
        GameObject go = new GameObject("Aim Opening");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        PixelAimOpening o = go.AddComponent<PixelAimOpening>();
        o.width = width;
        return o;
    }
}
