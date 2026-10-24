// ---- System notes ----
// PixelLooksMeshes.cs
//   Part of PixelLooks (partial class): the code-built meshes (star cubes, shine sheets, sprouts, neon edges / bevel rims, face discs and vortices, boxes). Their caches are in PixelLooks.cs. The system notes are in PixelLooks.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static partial class PixelLooks
{
    /// <summary>
    /// 1-7 small cubes inside a cube, laid out like the stars of a dragon ball (1 centre; 2 pair; 3 triangle; 4 square; 5 square +
    /// centre; 6 two columns; 7 hexagon + centre), flat-shaded with vertex colours. Cached per size / count / colour.
    /// </summary>
    private static Mesh StarCubesMesh(Vector3 size, int count, Color colour)
    {
        count = Mathf.Clamp(count, 1, 7);
        int key = count * 100003 + Mathf.RoundToInt(size.x * 1000f) + Mathf.RoundToInt(colour.r * 255f) * 7 + Mathf.RoundToInt(colour.g * 255f) * 13;
        if (starMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        Vector2[] spots;
        switch (count)
        {
            case 1: spots = new[] { Vector2.zero }; break;
            case 2: spots = new[] { new Vector2(-0.17f, 0f), new Vector2(0.17f, 0f) }; break;
            case 3: spots = new[] { new Vector2(0f, 0.17f), new Vector2(-0.18f, -0.12f), new Vector2(0.18f, -0.12f) }; break;
            case 4: spots = new[] { new Vector2(-0.16f, 0.16f), new Vector2(0.16f, 0.16f), new Vector2(-0.16f, -0.16f), new Vector2(0.16f, -0.16f) }; break;
            case 5: spots = new[] { new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f) }; break;
            case 6: spots = new[] { new Vector2(-0.16f, 0.22f), new Vector2(0.16f, 0.22f), new Vector2(-0.16f, 0f), new Vector2(0.16f, 0f), new Vector2(-0.16f, -0.22f), new Vector2(0.16f, -0.22f) }; break;
            default:
                spots = new Vector2[7];
                spots[0] = Vector2.zero;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    spots[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.22f;
                }
                break;
        }

        float unit = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float half = unit * (count >= 5 ? 0.075f : 0.09f); // half the edge of a small cube
        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        float[] shade = { 1f, 0.5f, 0.72f, 0.78f, 0.9f, 0.62f };

        foreach (Vector2 s in spots)
        {
            Vector3 centre = new Vector3(s.x * size.x, s.y * size.y, 0f);
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = normals[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(n, u);
                int b = v.Count;
                v.Add(centre + (n + u + w) * half); v.Add(centre + (n + u - w) * half);
                v.Add(centre + (n - u - w) * half); v.Add(centre + (n - u + w) * half);
                Color shaded = new Color(colour.r * shade[f], colour.g * shade[f], colour.b * shade[f], colour.a);
                for (int k = 0; k < 4; k++) c.Add(shaded);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
        }
        Mesh mesh = new Mesh { name = "Dragon Stars " + count };
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        starMeshes[key] = mesh;
        return mesh;
    }

    private static Mesh ShineMesh(Vector3 size, bool jagged = false, int seed = 0)
    {
        int key = unchecked(size.GetHashCode() * 31 + (jagged ? 977 : 0) + seed * 7919);
        if (shineMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        Mesh mesh = BuildShineMesh(size, jagged, seed);
        shineMeshes[key] = mesh;
        return mesh;
    }

    private static Mesh BuildShineMesh(Vector3 size, bool jagged, int seed)
    {
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        int n = ShineGrid;
        foreach (Vector3 normal in normals)
        {
            Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(normal, u);
            int b = v.Count;
            for (int iy = 0; iy <= n; iy++)
            {
                for (int ix = 0; ix <= n; ix++)
                {
                    float fx = ix / (float)n - 0.5f, fy = iy / (float)n - 0.5f;
                    if (jagged)
                    {
                        // Follow the chipped rock cube (same displacement as PixelRockMesh) so the sheen never sticks out past the jagged edges.
                        Vector3 rock = PixelRockMesh.Displace(normal * 0.5f + u * fx + w * fy, seed) + normal * 0.012f;
                        v.Add(Vector3.Scale(rock, size));
                    }
                    else v.Add(Vector3.Scale(normal * 0.508f + u * fx + w * fy, size));
                    c.Add(new Color(1f, 1f, 1f, 0f));
                }
            }
            for (int iy = 0; iy < n; iy++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    int i0 = b + iy * (n + 1) + ix, i1 = i0 + 1, i2 = i0 + (n + 1), i3 = i2 + 1;
                    t.Add(i0); t.Add(i2); t.Add(i1); t.Add(i1); t.Add(i2); t.Add(i3);
                    t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i1); t.Add(i3); t.Add(i2); // double-sided
                }
            }
        }
        Mesh mesh = new Mesh { name = "Shine" };
        mesh.MarkDynamic();
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A little sprout on top of a cube: a green stem and two angled leaves, flat-shaded with vertex colours.</summary>
    private static Mesh SproutMesh(Vector3 size, bool baseAtOrigin = false)
    {
        int key = Mathf.RoundToInt(size.x * 1000f) * 31 + Mathf.RoundToInt(size.y * 1000f) + (baseAtOrigin ? 1 << 24 : 0);
        if (sproutMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        float top = baseAtOrigin ? 0f : size.y * 0.5f; // on top of a cube, or standing on its own origin
        AddShadedBox(v, c, t, new Vector3(0f, top + size.y * 0.13f, 0f), new Vector3(size.x * 0.035f, size.y * 0.13f, size.x * 0.035f), Quaternion.identity, new Color(0.3f, 0.7f, 0.22f, 1f));
        Vector3 leafHalf = new Vector3(size.x * 0.15f, size.y * 0.022f, size.x * 0.075f);
        AddShadedBox(v, c, t, new Vector3(-size.x * 0.13f, top + size.y * 0.3f, 0f), leafHalf, Quaternion.Euler(0f, 0f, 24f), new Color(0.42f, 0.85f, 0.3f, 1f));
        AddShadedBox(v, c, t, new Vector3(size.x * 0.13f, top + size.y * 0.3f, 0f), leafHalf, Quaternion.Euler(0f, 0f, -24f), new Color(0.35f, 0.78f, 0.26f, 1f));

        Mesh mesh = new Mesh { name = "Sprout" };
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        sproutMeshes[key] = mesh;
        return mesh;
    }

    /// <summary>A free-standing sprout (stem + two leaves, base at the origin) as a child of 'parent'; 'unit' = the size it is built for (a pixel's edge).</summary>
    public static GameObject CreateSproutObject(Transform parent, float unit)
    {
        GameObject go = new GameObject("Sprout", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = SproutMesh(Vector3.one * Mathf.Max(0.01f, unit), true);
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = OverlayMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    /// <summary>Adds a flat-shaded box (centre, half extents, rotation) to a vertex-colour mesh under construction.</summary>
    private static void AddShadedBox(List<Vector3> v, List<Color> c, List<int> t, Vector3 centre, Vector3 half, Quaternion rot, Color colour)
    {
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        float[] shade = { 1f, 0.5f, 0.72f, 0.78f, 0.9f, 0.62f };
        for (int f = 0; f < 6; f++)
        {
            Vector3 n = normals[f];
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(n, u);
            int b = v.Count;
            Vector3 hn = Vector3.Scale(n, half), hu = Vector3.Scale(u, half), hw = Vector3.Scale(w, half);
            v.Add(centre + rot * (hn + hu + hw)); v.Add(centre + rot * (hn + hu - hw));
            v.Add(centre + rot * (hn - hu - hw)); v.Add(centre + rot * (hn - hu + hw));
            Color shaded = new Color(colour.r * shade[f], colour.g * shade[f], colour.b * shade[f], colour.a);
            for (int k = 0; k < 4; k++) c.Add(shaded);
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
    }

    // ------------------------------------------------------------------
    // Neon edge mesh: 12 thin boxes for a bright core plus 12 wider, fainter ones for the halo
    // ------------------------------------------------------------------

    private static Mesh EdgeMesh(Vector3 size, float thicknessFraction, Color colour, float strength, bool bevel = false)
    {
        int key = unchecked(size.GetHashCode() * 31 + thicknessFraction.GetHashCode() * 17 + colour.GetHashCode() * 13 + strength.GetHashCode() + (bevel ? 977 : 0));
        if (edgeMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        float t = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * thicknessFraction;
        Color halo = new Color(colour.r, colour.g, colour.b, 0.3f * strength);
        Color core = Color.Lerp(colour, Color.white, 0.5f);
        core.a = strength;

        List<Vector3> v = new List<Vector3>(400);
        List<Color> col = new List<Color>(400);
        List<int> tri = new List<int>(600);
        if (bevel)
        {
            // One thin solid strip along each edge, lying just inside the surface (its outer faces a hair outside the cube's faces).
            Color solid = colour;
            solid.a = Mathf.Clamp01(strength);
            AddBevelRims(v, col, tri, size, t, solid);
        }
        else
        {
            AddEdgeBoxes(v, col, tri, size, t * 2.6f, halo);
            AddEdgeBoxes(v, col, tri, size, t, core);
        }

        Mesh mesh = new Mesh { name = "NeonEdges" };
        mesh.SetVertices(v);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        edgeMeshes[key] = mesh;
        return mesh;
    }

    /// <summary>
    /// A smooth rim lying flat on every face along its four borders: mitred at the corners (nothing overlaps or sticks out), solid at the cube's edge
    /// and softly fading inwards, so the bevel reads as a clean chamfer line instead of a chunky frame.
    /// </summary>
    private static void AddBevelRims(List<Vector3> v, List<Color> col, List<int> tri, Vector3 size, float t, Color solid)
    {
        float gap = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.004f;
        Color clear = solid; clear.a = 0f;
        // distance in from the edge of each ring of vertices, and its colour
        float[] depth = { 0f, t * 0.55f, t * 1.5f };
        Color[] ringColour = { solid, solid, clear };
        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int start = v.Count;
                for (int r = 0; r < depth.Length; r++)
                {
                    float ha = size[a] * 0.5f - depth[r], hb = size[b] * 0.5f - depth[r];
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 p = Vector3.zero;
                        p[axis] = sign * (size[axis] * 0.5f + gap);
                        p[a] = (c == 0 || c == 3) ? -ha : ha;
                        p[b] = (c < 2) ? -hb : hb;
                        v.Add(p); col.Add(ringColour[r]);
                    }
                }
                for (int r = 0; r < depth.Length - 1; r++)
                {
                    int inner = start + (r + 1) * 4, outer = start + r * 4;
                    for (int c = 0; c < 4; c++)
                    {
                        int n = (c + 1) % 4;
                        tri.Add(outer + c); tri.Add(outer + n); tri.Add(inner + n);
                        tri.Add(outer + c); tri.Add(inner + n); tri.Add(inner + c);
                        tri.Add(outer + c); tri.Add(inner + n); tri.Add(outer + n);
                        tri.Add(outer + c); tri.Add(inner + c); tri.Add(inner + n);
                    }
                }
            }
        }
    }

    private static void AddEdgeBoxes(List<Vector3> v, List<Color> col, List<int> tri, Vector3 size, float t, Color colour, bool flush = false)
    {
        Vector3 h = size * 0.5f;
        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    Vector3 centre = Vector3.zero, ext = Vector3.zero;
                    float inset = flush ? t * 0.5f - Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.004f : 0f; // flush: pulled in so the strip lies on the faces
                    centre[a] = i * (h[a] - inset);
                    centre[b] = j * (h[b] - inset);
                    ext[axis] = h[axis] + t * 0.5f; // a little longer so the corners close up
                    ext[a] = t * 0.5f;
                    ext[b] = t * 0.5f;
                    AddBox(v, col, tri, centre, ext, colour);
                }
            }
        }
    }

    /// <summary>Six flat discs, one just outside each face of a box of this size.</summary>
    private static Mesh CircleMesh(Vector3 size, float radiusFraction, Color colour, int segments = 32, float rotationDegrees = 0f)
    {
        int key = unchecked(size.GetHashCode() * 31 + radiusFraction.GetHashCode() * 17 + colour.GetHashCode() * 13 + segments * 7 + rotationDegrees.GetHashCode() * 3);
        if (circleMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        int Segments = Mathf.Clamp(segments, 3, 64);
        float rot = rotationDegrees * Mathf.Deg2Rad;
        Mesh mesh = BuildFaceMarks("FaceMarks", size, radiusFraction, (v, col, tri, a, b, radius, centre) =>
        {
            int start = v.Count;
            v.Add(centre); col.Add(colour);
            for (int i = 0; i < Segments; i++)
            {
                float ang = rot + i * Mathf.PI * 2f / Segments;
                Vector3 p = centre;
                p[a] += Mathf.Cos(ang) * radius;
                p[b] += Mathf.Sin(ang) * radius;
                v.Add(p); col.Add(colour);
            }
            for (int i = 0; i < Segments; i++)
            {
                int p1 = start + 1 + i, p2 = start + 1 + (i + 1) % Segments;
                tri.Add(start); tri.Add(p1); tri.Add(p2);   // both windings, so it shows from either side
                tri.Add(start); tri.Add(p2); tri.Add(p1);
            }
        });
        circleMeshes[key] = mesh;
        return mesh;
    }

    /// <summary>Adds one face's mark to a mesh under construction: 'a' / 'b' = the face's two in-plane axes, 'radius' = the disc radius, 'centre' = the face's middle.</summary>
    private delegate void FaceMarkBuilder(List<Vector3> v, List<Color> col, List<int> tri, int a, int b, float radius, Vector3 centre);

    /// <summary>
    /// Shared by CircleMesh and VortexMesh: calls 'addFace' for each of the six faces of a box of this size (axis x, y, z; the - side first),
    /// with its centre just off the face so it never z-fights, then builds the vertex-colour mesh.
    /// </summary>
    private static Mesh BuildFaceMarks(string name, Vector3 size, float radiusFraction, FaceMarkBuilder addFace)
    {
        float gap = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.004f; // just off the face so it never z-fights
        List<Vector3> v = new List<Vector3>();
        List<Color> col = new List<Color>();
        List<int> tri = new List<int>();

        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            float radius = Mathf.Min(size[a], size[b]) * radiusFraction;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 centre = Vector3.zero;
                centre[axis] = sign * (size[axis] * 0.5f + gap);
                addFace(v, col, tri, a, b, radius, centre);
            }
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Six vortex marks (one per face): a dark centre fading through a glowing ring that softly fades out past the disc's edge.</summary>
    private static Mesh VortexMesh(Vector3 size, float radiusFraction, Color centre, Color ring)
    {
        int key = unchecked(size.GetHashCode() * 31 + radiusFraction.GetHashCode() * 17 + centre.GetHashCode() * 13 + ring.GetHashCode() * 7);
        if (vortexMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        const int Segments = 40;
        // radius factor (of the disc radius) and colour of each ring of vertices, from the middle out
        float[] rr = { 0f, 0.55f, 0.88f, 1f, 1.22f, 1.4f };
        Color[] cc =
        {
            centre,
            centre,
            Color.Lerp(centre, ring, 0.25f),
            new Color(ring.r, ring.g, ring.b, 0.95f),
            new Color(ring.r, ring.g, ring.b, 0.35f),
            new Color(ring.r, ring.g, ring.b, 0f),
        };
        Mesh mesh = BuildFaceMarks("FaceVortex", size, radiusFraction, (v, col, tri, a, b, radius, mid) =>
        {
            int start = v.Count;
            for (int r = 0; r < rr.Length; r++)
            {
                int count = r == 0 ? 1 : Segments;
                for (int i = 0; i < count; i++)
                {
                    float ang = i * Mathf.PI * 2f / Segments;
                    Vector3 p = mid;
                    p[a] += Mathf.Cos(ang) * radius * rr[r];
                    p[b] += Mathf.Sin(ang) * radius * rr[r];
                    v.Add(p); col.Add(cc[r]);
                }
            }
            for (int i = 0; i < Segments; i++)
            {
                int n = (i + 1) % Segments;
                int first = start + 1;
                // fan from the centre to ring 1
                tri.Add(start); tri.Add(first + i); tri.Add(first + n);
                tri.Add(start); tri.Add(first + n); tri.Add(first + i);
                // strips between the following rings
                for (int r = 1; r < rr.Length - 1; r++)
                {
                    int inner = start + 1 + (r - 1) * Segments, outer = inner + Segments;
                    tri.Add(inner + i); tri.Add(outer + i); tri.Add(outer + n);
                    tri.Add(inner + i); tri.Add(outer + n); tri.Add(inner + n);
                    tri.Add(inner + i); tri.Add(outer + n); tri.Add(outer + i);
                    tri.Add(inner + i); tri.Add(inner + n); tri.Add(outer + n);
                }
            }
        });
        vortexMeshes[key] = mesh;
        return mesh;
    }

    private static void AddBox(List<Vector3> v, List<Color> col, List<int> tri, Vector3 centre, Vector3 ext, Color colour)
    {
        int start = v.Count;
        for (int i = 0; i < 8; i++)
        {
            v.Add(centre + new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z));
            col.Add(colour);
        }
        foreach (int[] q in BoxQuads)
        {
            tri.Add(start + q[0]); tri.Add(start + q[1]); tri.Add(start + q[2]);
            tri.Add(start + q[0]); tri.Add(start + q[2]); tri.Add(start + q[3]);
        }
    }
}
