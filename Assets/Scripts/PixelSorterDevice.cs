using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A placed sorter. Created and configured by <see cref="PixelConsumables"/>.
///
/// It is a ring around the clicker cube with an output pipe sticking out of one side (facing the camera, aimed with the
/// scroll wheel while placing). While it works, every pixel you collect is not thrown out in a random direction but
/// spat out of the end of the pipe in a stream. The countdown, timer text and shrink-away come from
/// <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelSorterDevice : PixelPlacedDevice
{
    /// <summary>The sorter that is currently working (null = none). <see cref="PixelClicker"/> asks it where to send each pixel.</summary>
    public static PixelSorterDevice Current { get; private set; }

    private Transform pivot;          // turns the pipe around the ring
    private float outerRadius;
    private float pipeLength;
    private float exitSpeed;
    private float spreadDegrees;
    private float pipeAngle;

    public void Init(PixelClicker owner, Camera camera, Transform pipePivot, TextMeshPro timer, string format,
                     float duration, float ringOuterRadius, float length, float speed, float spread, float angle,
                     float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        pivot = pipePivot;
        outerRadius = ringOuterRadius;
        pipeLength = length;
        exitSpeed = speed;
        spreadDegrees = spread;
        pipeAngle = angle;
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    /// <summary>
    /// Where a freshly collected pixel should appear (the mouth of the pipe) and how fast it leaves.
    /// Returns false when the sorter is shutting down.
    /// </summary>
    public bool TryRoute(out Vector3 mouth, out Vector3 velocity)
    {
        mouth = transform.position;
        velocity = Vector3.zero;
        if (IsDying || pivot == null) return false;

        Quaternion turn = Quaternion.Euler(0f, 0f, pipeAngle);
        mouth = transform.TransformPoint(turn * new Vector3(outerRadius + pipeLength, 0f, 0f));

        Vector3 direction = transform.TransformDirection(turn * Vector3.right);
        // A little wobble so the stream isn't a perfectly straight line.
        Quaternion wobble = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), transform.forward);
        velocity = wobble * direction * exitSpeed;
        return true;
    }

    /// <summary>Flat ring (annulus) in the local XY plane, 'depth' thick along Z.</summary>
    public static Mesh BuildRingMesh(float innerRadius, float outerRadius, float depth, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<int> triangles = new List<int>();
        float h = depth * 0.5f;

        // Adds one triangle that faces 'outward' (flips itself if the winding is the wrong way round).
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
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
            Tri(oF0, oF1, iF1, Vector3.back); Tri(oF0, iF1, iF0, Vector3.back);
            Tri(oB0, oB1, iB1, Vector3.forward); Tri(oB0, iB1, iB0, Vector3.forward);
            Tri(oF0, oF1, oB1, mid); Tri(oF0, oB1, oB0, mid);
            Tri(iF0, iF1, iB1, -mid); Tri(iF0, iB1, iB0, -mid);
        }

        Mesh mesh = new Mesh { name = "Sorter Ring" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
