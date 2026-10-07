using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a round tube mesh along any 3D path (used by the pixel bank's hose). The tube is smooth shaded and open at
/// both ends; hide the ends behind something (the nozzle, the edge of the screen).
/// </summary>
public static class PixelTube
{
    private static readonly List<Vector3> vertices = new List<Vector3>();
    private static readonly List<Vector3> normals = new List<Vector3>();
    private static readonly List<int> triangles = new List<int>();

    /// <summary>Fills (or creates) a mesh with a tube of 'radius' along 'points' (in the mesh's local space).</summary>
    public static Mesh Build(Vector3[] points, float radius, int sides, Mesh reuse)
    {
        Mesh mesh = reuse != null ? reuse : new Mesh { name = "Tube" };
        mesh.Clear();
        int n = points.Length;
        if (n < 2 || sides < 3) return mesh;

        vertices.Clear();
        normals.Clear();
        triangles.Clear();

        // Rotation-minimising frames along the path, so the tube never twists.
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Vector3 tangent = (points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.right;

            if (i == 0)
            {
                normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            }
            else
            {
                normal -= tangent * Vector3.Dot(normal, tangent);
                if (normal.sqrMagnitude < 0.0001f) normal = Vector3.Cross(tangent, Vector3.up);
                normal.Normalize();
            }
            Vector3 binormal = Vector3.Cross(tangent, normal);

            for (int j = 0; j < sides; j++)
            {
                float a = Mathf.PI * 2f * j / sides;
                Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                vertices.Add(points[i] + dir * radius);
                normals.Add(dir);
            }
        }

        for (int i = 0; i < n - 1; i++)
        {
            for (int j = 0; j < sides; j++)
            {
                int k = (j + 1) % sides;
                int a = i * sides + j, b = (i + 1) * sides + j, c = (i + 1) * sides + k, d = i * sides + k;
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // Adds a triangle wound so it faces the way its vertex normals point.
    private static void AddTriangle(int a, int b, int c)
    {
        Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0f) { int t = b; b = c; c = t; }
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
    }
}
