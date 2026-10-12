// ---- System notes (moved here from CLAUDE.md) ----
// PixelRockMesh.cs
//   Jagged rocky pixels: PixelLook.jagged (Obsidian, Meteor, Rock, the six ores; looksVersion 36 replaced their saved looks and
//   removed the Obsidian bevel rim - rocky pixels have no outline) draws a chipped cube mesh (PixelRockMesh.Get(baseCube, type): 8x8
//   quads per face, flat shaded, only edge / corner vertices pulled inwards by a position hash so shared vertices match; faces stay
//   flat, never bigger than the cube, same per-face UVs). PixelClicker.MeshFor(look) / BaseMesh() pick the mesh: the live cube swaps
//   its MeshFilter mesh in ApplyTierLook, old pixel copies, display pixels and their extras / glow shells use MeshFor of their own
//   tier (never srcFilter.sharedMesh, which may be another type's jagged mesh). The hit box is still a cube.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A chipped, jagged version of the pixel cube for rocky pixels (Obsidian, Meteor, Rock, the ores): the faces stay flat but the edges and corners
/// are broken into an irregular saw-tooth by pulling vertices on them towards the middle. Flat-shaded, same UVs per face as a cube, never bigger
/// than the cube (so the hit box still matches). Built once per pixel type and cached.
/// </summary>
public static class PixelRockMesh
{
    private const int N = 8;   // quads along each face edge
    private static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); }

    public static Mesh Get(Mesh cube, int seed)
    {
        if (cube == null) return null;
        int key = seed * 7919 + cube.GetInstanceID();
        if (cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        Mesh m = Build(cube.bounds, seed);
        cache[key] = m;
        return m;
    }

    /// <summary>Pulls a point of the unit cube (-0.5..0.5, on the 8x8 face grid) inwards if it lies on an edge / corner; the same for every caller, so overlays can follow the jagged shape.</summary>
    public static Vector3 Displace(Vector3 p, int seed)
    {
        float ax = Mathf.Abs(p.x) * 2f, ay = Mathf.Abs(p.y) * 2f, az = Mathf.Abs(p.z) * 2f;
        float hi = Mathf.Max(ax, Mathf.Max(ay, az)), lo = Mathf.Min(ax, Mathf.Min(ay, az));
        float mid = ax + ay + az - hi - lo;                       // the second largest
        float edge = Mathf.Clamp01((mid - 0.8f) / 0.2f), corner = Mathf.Clamp01((lo - 0.85f) / 0.15f);
        if (edge <= 0f) return p;
        int qx = Mathf.RoundToInt(p.x * N), qy = Mathf.RoundToInt(p.y * N), qz = Mathf.RoundToInt(p.z * N);   // shared vertices hash the same
        float h = PixelNoise.Hash(qx * 73 + qy * 179, qz * 283 + seed, 17);
        float h2 = PixelNoise.Hash(qx * 31 + qz * 97, qy * 211 + seed, 29);
        float d = edge * (0.04f + 0.15f * h) + corner * 0.12f * h2;
        return p * (1f - d);
    }

    private static Mesh Build(Bounds b, int seed)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        Vector3 size = b.size, centre = b.center;

        for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                Vector3 outward = Vector3.zero; outward[axis] = sign;
                Vector3[,] g = new Vector3[N + 1, N + 1];
                for (int j = 0; j <= N; j++)
                    for (int i = 0; i <= N; i++)
                    {
                        Vector3 p = Vector3.zero;
                        p[axis] = sign * 0.5f;
                        p[ua] = -0.5f + (float)i / N;
                        p[va] = -0.5f + (float)j / N;
                        g[i, j] = Displace(p, seed);
                    }
                for (int j = 0; j < N; j++)
                    for (int i = 0; i < N; i++)
                    {
                        Vector3 a = g[i, j], bb = g[i + 1, j], c = g[i, j + 1], d = g[i + 1, j + 1];
                        Vector2 ta = new Vector2((float)i / N, (float)j / N), tb = new Vector2((float)(i + 1) / N, (float)j / N);
                        Vector2 tc = new Vector2((float)i / N, (float)(j + 1) / N), td = new Vector2((float)(i + 1) / N, (float)(j + 1) / N);
                        AddTri(verts, normals, uvs, tris, a, bb, c, ta, tb, tc, outward, size, centre);
                        AddTri(verts, normals, uvs, tris, bb, d, c, tb, td, tc, outward, size, centre);
                    }
            }

        Mesh mesh = new Mesh { name = "Rock Cube" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTri(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris,
                               Vector3 a, Vector3 b, Vector3 c, Vector2 ta, Vector2 tb, Vector2 tc, Vector3 outward, Vector3 size, Vector3 centre)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (n.sqrMagnitude < 1e-12f) return;   // a collapsed triangle
        if (Vector3.Dot(n, outward) < 0f) { Vector3 t = b; b = c; c = t; Vector2 tt = tb; tb = tc; tc = tt; n = -n; }
        n.Normalize();
        int start = verts.Count;
        verts.Add(Vector3.Scale(a, size) + centre); verts.Add(Vector3.Scale(b, size) + centre); verts.Add(Vector3.Scale(c, size) + centre);
        normals.Add(n); normals.Add(n); normals.Add(n);
        uvs.Add(ta); uvs.Add(tb); uvs.Add(tc);
        tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
    }
}
