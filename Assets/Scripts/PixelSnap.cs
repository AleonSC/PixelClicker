// ---- System notes ----
// PixelSnap.cs
//   Hold SHIFT while placing (consumables: PixelConsumables placing; Dev Hub tester machines and belts: PixelFeatureTester) to snap the spot to a GRID (gridCubes old-pixel widths, the same spacing as the
//   first person neon floor) and the turn to ANGLE steps (angleDegrees, default 15 so 45 and 90 are hit exactly). The numbers are set on PixelConsumables (Inspector: "Shift snapping") and pushed here;
//   until then the defaults apply. First person: PixelFirstPerson.SnapPoseToGrid snaps a carried block's resting spot (on the ground, not when it sticks to another cube) to cells of the same grid and its turn to the angle steps. Point() only snaps x / z (the height stays); Yaw() rounds a turn. Callers keep the un-snapped turn themselves so Q / E can still turn smoothly while Shift is held.
// ---------------------------------------------------
using UnityEngine;

public static class PixelSnap
{
    private static float gridCubes = 1f, angleDegrees = 15f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { gridCubes = 1f; angleDegrees = 15f; }

    public static void Configure(float grid, float angle)
    {
        gridCubes = grid > 0f ? grid : 1f;
        angleDegrees = angle > 0f ? angle : 15f;
    }

    public static bool Active => PixelInput.ShiftHeld();

    /// <summary>The grid spacing in old-pixel widths.</summary>
    public static float GridCubes => gridCubes;

    public static Vector3 Point(Vector3 p, float unit)
    {
        float g = Mathf.Max(0.001f, unit * gridCubes);
        p.x = Mathf.Round(p.x / g) * g;
        p.z = Mathf.Round(p.z / g) * g;
        return p;
    }

    public static float Yaw(float yaw)
    {
        float a = Mathf.Max(1f, angleDegrees);
        return Mathf.Round(yaw / a) * a;
    }
}
