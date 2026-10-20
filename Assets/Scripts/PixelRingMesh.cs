// ---- System notes ----
// PixelRingMesh.cs
//   One shared mesh helper: a flat RING (annulus) in the local XY plane with some depth along Z, flat-shaded (front, back, outer and inner walls).
//   Used by the Pixel Bank's area ring and the Fire pixel's ring. (2026-10-20: moved here from the removed ring Sorter, PixelSorterDevice.)
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PixelRingMesh
{
    /// <summary>A flat ring between 'innerRadius' and 'outerRadius' in the local XY plane, 'depth' thick along Z, in 'segments' pieces.</summary>
    public static Mesh Build(float innerRadius, float outerRadius, float depth, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        float h = depth * 0.5f;

        // One flat triangle, wound so it faces 'outward'.
        void Flat(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0f) { Vector3 t = b; b = c; c = t; n = -n; }
            n.Normalize();
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.PI * 2f * i / segments;
            float a1 = Mathf.PI * 2f * (i + 1) / segments;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
            Vector3 d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
            Vector3 mid = (d0 + d1).normalized;

            Vector3 oF0 = d0 * outerRadius + Vector3.back * h, oF1 = d1 * outerRadius + Vector3.back * h;
            Vector3 iF0 = d0 * innerRadius + Vector3.back * h, iF1 = d1 * innerRadius + Vector3.back * h;
            Vector3 oB0 = d0 * outerRadius + Vector3.forward * h, oB1 = d1 * outerRadius + Vector3.forward * h;
            Vector3 iB0 = d0 * innerRadius + Vector3.forward * h, iB1 = d1 * innerRadius + Vector3.forward * h;

            // Front (towards the camera is -Z), back, outer wall, inner wall.
            Flat(oF0, oF1, iF1, Vector3.back); Flat(oF0, iF1, iF0, Vector3.back);
            Flat(oB0, oB1, iB1, Vector3.forward); Flat(oB0, iB1, iB0, Vector3.forward);
            Flat(oF0, oF1, oB1, mid); Flat(oF0, oB1, oB0, mid);
            Flat(iF0, iF1, iB1, -mid); Flat(iF0, iB1, iB0, -mid);
        }

        Mesh mesh = new Mesh { name = "Ring" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
