using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds lightning bolts as mesh data: jagged lines drawn as crossed flat ribbons (so they read from any angle), in two passes
/// (a wide soft glow and a thin bright core). Used by the Electric pixel's overcharge ring and its device links. Colours are
/// vertex colours, meant for the unlit <c>PixelLooks.OverlayMaterial</c>.
/// </summary>
public static class PixelBolts
{
    /// <summary>Vertex / colour / triangle lists for one dynamic mesh.</summary>
    public class Builder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<int> tris = new List<int>();

        public void Clear() { verts.Clear(); colors.Clear(); tris.Clear(); }

        /// <summary>Writes the lists into 'mesh' (creating the mesh data fresh).</summary>
        public void Apply(Mesh mesh, Bounds bounds)
        {
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = bounds;
        }
    }

    /// <summary>Two crossed flat strips from a to b.</summary>
    public static void Ribbon(Builder b, Vector3 a, Vector3 c, float width, Color colour)
    {
        Vector3 dir = c - a;
        if (dir.sqrMagnitude < 1e-10f) return;
        dir.Normalize();
        Vector3 side1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (width * 0.5f);
        Vector3 side2 = Vector3.Cross(dir, side1).normalized * (width * 0.5f);
        Quad(b, a, c, side1, colour);
        Quad(b, a, c, side2, colour);
    }

    private static void Quad(Builder b, Vector3 a, Vector3 c, Vector3 side, Color colour)
    {
        int i = b.verts.Count;
        b.verts.Add(a - side); b.verts.Add(a + side); b.verts.Add(c + side); b.verts.Add(c - side);
        for (int k = 0; k < 4; k++) b.colors.Add(colour);
        b.tris.Add(i); b.tris.Add(i + 1); b.tris.Add(i + 2);
        b.tris.Add(i); b.tris.Add(i + 2); b.tris.Add(i + 3);
    }

    /// <summary>Draws a path of points as a flickering bolt (glow pass optional).</summary>
    public static void Strokes(Builder b, IList<Vector3> pts, float coreWidth, float glowWidth, bool withGlow, Color core, Color glow, float alpha = 1f)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            float flicker = Random.Range(0.55f, 1f) * alpha;
            if (withGlow) Ribbon(b, pts[i - 1], pts[i], glowWidth, new Color(glow.r, glow.g, glow.b, 0.22f * flicker));
            Ribbon(b, pts[i - 1], pts[i], coreWidth, new Color(core.r, core.g, core.b, flicker));
        }
    }

    /// <summary>A jagged bolt from a to c: the points wander off the straight line (not at the ends) by up to 'wander' (world units).</summary>
    public static void Jagged(Builder b, Vector3 a, Vector3 c, int segments, float wander, float coreWidth, float glowWidth,
                              bool withGlow, Color core, Color glow, float alpha = 1f)
    {
        segments = Mathf.Max(1, segments);
        List<Vector3> pts = new List<Vector3>(segments + 1) { a };
        for (int s = 1; s < segments; s++)
        {
            float t = s / (float)segments;
            Vector3 p = Vector3.Lerp(a, c, t) + Random.insideUnitSphere * (wander * Mathf.Sin(t * Mathf.PI));
            pts.Add(p);
        }
        pts.Add(c);
        Strokes(b, pts, coreWidth, glowWidth, withGlow, core, glow, alpha);
    }
}
