// ---- System notes ----
// PixelFloorRing.cs
//   ONE helper for "a ring (or part of a ring) drawn with a LineRenderer": Create makes the renderer (no shadows, faces the camera, optional
//   round caps), Draw lays out a whole or partial ring round a centre on any plane (two axes) with a radius, fill 0..1, colour and width.
//   A whole ring (fill >= 0.999) is a closed loop of 'points' points; a partial one is an open arc from angle 0 of ceil(points x fill) + 1 points.
//   SharedMaterial() = one Sprites/Default material (PixelShaders.SpriteDefault: unlit, vertex colours) for rings that don't bring their own.
//   Used by: Charge Crush's charge / cooldown ring (PixelHuskCrush), the Solar Panel's link range ring (PixelSolarPanel.ShowRange),
//   Pixel Grabbing's drop-guide ring (PixelGrab). A new floor ring uses this instead of its own LineRenderer code.
// ---------------------------------------------------
using UnityEngine;

public static class PixelFloorRing
{
    private static Material shared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { shared = null; }

    /// <summary>One Sprites/Default material for rings (vertex colours carry the colour; never change it - it is shared). Null if the shader is missing.</summary>
    public static Material SharedMaterial()
    {
        if (shared != null) return shared;
        Shader shader = PixelShaders.SpriteDefault();
        if (shader != null) shared = new Material(shader) { name = "Floor Ring" };
        return shared;
    }

    /// <summary>A LineRenderer for a ring, as a child of 'parent' (world-space points unless worldSpace is false). 'material' null = SharedMaterial().</summary>
    public static LineRenderer Create(string name, Transform parent, Material material = null, bool worldSpace = true, int capVertices = 0)
    {
        GameObject go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.GetComponent<LineRenderer>();
        lr.sharedMaterial = material != null ? material : SharedMaterial();
        lr.useWorldSpace = worldSpace;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = capVertices;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    /// <summary>A ring on the floor (the XZ plane) round 'centre'.</summary>
    public static void Draw(LineRenderer lr, Vector3 centre, float radius, int points, float fill, Color color, float width)
        => Draw(lr, centre, Vector3.right, Vector3.forward, radius, points, fill, color, width);

    /// <summary>
    /// A ring round 'centre' in the plane of axisA / axisB (point at angle a = centre + (axisA cos a + axisB sin a) x radius).
    /// fill 1 = a closed loop of 'points' points; less = an open arc of that share of the ring, starting at angle 0.
    /// </summary>
    public static void Draw(LineRenderer lr, Vector3 centre, Vector3 axisA, Vector3 axisB, float radius, int points, float fill, Color color, float width)
    {
        if (lr == null) return;
        points = Mathf.Max(3, points);
        bool whole = fill >= 0.999f;
        int count = whole ? points : Mathf.Max(2, Mathf.CeilToInt(points * Mathf.Clamp01(fill)) + 1);
        lr.loop = whole;
        lr.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float a = (whole ? i / (float)points : i / (float)(count - 1) * fill) * Mathf.PI * 2f;
            lr.SetPosition(i, centre + (axisA * Mathf.Cos(a) + axisB * Mathf.Sin(a)) * radius);
        }
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = color;
    }
}
