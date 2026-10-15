// ---- System notes ----
// PixelArcGraphic.cs
//   A UI Graphic that draws a curved band (part of a ring) with ROUND caps at both ends and soft (feathered) edges, built as a mesh in
//   OnPopulateMesh - no sprite, no radial fill. Angles are in degrees, 0 = east (right), counter-clockwise. radius / halfWidth are fractions of
//   half the rect's smaller side. Set StartDegrees / EndDegrees to grow or shrink it (the Block Hold meter's track, glow, fill and ready pop).
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

/// <summary>A curved band with round caps (part of a ring), drawn as a UI mesh: the hold meter's arc layers.</summary>
public class PixelArcGraphic : MaskableGraphic
{
    [Tooltip("Radius of the band's centre line, as a fraction of half the rect's smaller side.")]
    [SerializeField] private float radius = 0.88f;
    [Tooltip("Half the band's thickness, same units as Radius.")]
    [SerializeField] private float halfWidth = 0.09f;
    [Tooltip("Edge fade in pixels (canvas units).")]
    [SerializeField] private float feather = 1.5f;
    [Tooltip("Where the band starts, in degrees (0 = right, counter-clockwise).")]
    [SerializeField] private float startDegrees = -55f;
    [Tooltip("Where the band ends, in degrees.")]
    [SerializeField] private float endDegrees = 55f;
    [Tooltip("Line segments per 10 degrees of arc.")]
    [SerializeField] private int segmentsPer10Degrees = 2;

    public void Setup(float radiusFraction, float halfWidthFraction, float featherPixels)
    {
        radius = radiusFraction; halfWidth = halfWidthFraction; feather = featherPixels;
        SetVerticesDirty();
    }

    public float StartDegrees { get => startDegrees; set { if (!Mathf.Approximately(startDegrees, value)) { startDegrees = value; SetVerticesDirty(); } } }
    public float EndDegrees { get => endDegrees; set { if (!Mathf.Approximately(endDegrees, value)) { endDegrees = value; SetVerticesDirty(); } } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float half = Mathf.Min(r.width, r.height) * 0.5f;
        Vector2 centre = r.center;
        float R = radius * half, hw = Mathf.Max(0.5f, halfWidth * half), f = Mathf.Max(0f, feather);
        float a0 = startDegrees, a1 = Mathf.Max(endDegrees, startDegrees + 0.01f);
        Color32 solid = color, clear = new Color32(solid.r, solid.g, solid.b, 0);

        // The band: four rings per step (outer fade, outer edge, inner edge, inner fade).
        int steps = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 10f * Mathf.Max(1, segmentsPer10Degrees)));
        float[] radii = { R + hw + f, R + hw, R - hw, R - hw - f };
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            for (int k = 0; k < 4; k++) vh.AddVert(centre + dir * radii[k], k == 0 || k == 3 ? clear : solid, Vector2.zero);
        }
        for (int i = 0; i < steps; i++)
        {
            int b = i * 4, n = b + 4;
            for (int k = 0; k < 3; k++) { vh.AddTriangle(b + k, n + k, n + k + 1); vh.AddTriangle(b + k, n + k + 1, b + k + 1); }
        }
        Cap(vh, centre, R, a0, -1f, hw, f, solid, clear);
        Cap(vh, centre, R, a1, 1f, hw, f, solid, clear);
    }

    /// <summary>A half-disc on the band's end at 'angle', bulging along the arc ('side' +1 = past the end, -1 = before the start), with a feathered rim.</summary>
    private static void Cap(VertexHelper vh, Vector2 centre, float R, float angleDeg, float side, float hw, float f, Color32 solid, Color32 clear)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 tangent = new Vector2(-radial.y, radial.x) * side;   // pointing away from the band
        Vector2 p = centre + radial * R;
        int start = vh.currentVertCount;
        vh.AddVert(p, solid, Vector2.zero);
        const int n = 10;
        for (int i = 0; i <= n; i++)
        {
            float t = Mathf.PI * i / n;   // from the outer edge round to the inner edge
            Vector2 d = radial * Mathf.Cos(t) + tangent * Mathf.Sin(t);
            vh.AddVert(p + d * hw, solid, Vector2.zero);
            vh.AddVert(p + d * (hw + f), clear, Vector2.zero);
        }
        for (int i = 0; i < n; i++)
        {
            int e = start + 1 + i * 2, eNext = e + 2;
            vh.AddTriangle(start, e, eNext);                 // the solid half-disc
            vh.AddTriangle(e, e + 1, eNext + 1);             // the feathered rim
            vh.AddTriangle(e, eNext + 1, eNext);
        }
    }
}
