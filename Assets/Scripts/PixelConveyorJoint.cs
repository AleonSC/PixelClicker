// ---- System notes ----
// PixelConveyorJoint.cs
//   CORNER FILLER for chained conveyor belts: where a belt hands on to the belt that starts at its end (PixelConveyor.FindNext) and the two are not in a straight line, the square-ish gap on the OUTSIDE of the turn is filled with a
//   slab mesh shaped by the angle (polygon: this belt's end, its outer corner, the point where the two outer edges meet, the next belt's outer corner, the next belt's start; with a sharp turn where the edges meet far away it is a
//   simple wedge) plus matching outer rails. Built by PixelConveyorJoint.Refresh (called by each belt every 0.5 s and after it is rebuilt; it only rebuilds when the two belts' ends / heights / widths change) as a child of the belt, so
//   it goes with the belt. Purely visual: pixels are handed from belt to belt as before. Straight chains and head-on ends get no joint.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class PixelConveyorJoint
{
    /// <summary>Makes sure 'belt' has the right corner filler (or none). 'key' remembers what was built; 'joint' is the built object.</summary>
    public static void Refresh(PixelConveyor belt, PixelClicker clicker, ref string key, ref GameObject joint)
    {
        PixelConveyor next = belt.IsGhost ? null : belt.FindNext();
        Vector3 dA = belt.Direction, dB = next != null ? next.Direction : dA;
        float turn = dA.x * dB.z - dA.z * dB.x;
        if (next == null || (Mathf.Abs(turn) < 0.05f && Vector3.Dot(dA, dB) > 0f))
        {
            if (joint != null) Object.Destroy(joint);
            joint = null; key = null;
            return;
        }
        Vector3 eA = belt.ExitPoint, sB = next.StartPoint;
        float w = belt.Width, wB = next.Width, h = belt.SurfaceHeight;
        string k = eA.ToString("F2") + sB.ToString("F2") + w.ToString("F2") + wB.ToString("F2") + h.ToString("F2");
        if (joint != null && k == key) return;
        key = k;
        if (joint != null) Object.Destroy(joint);

        float unit = belt.Unit;
        float side = turn > 0f ? 1f : -1f;   // which side of the belts is the OUTSIDE of the turn (+1 = their right)
        Vector3 rA = new Vector3(dA.z, 0f, -dA.x), rB = new Vector3(dB.z, 0f, -dB.x);
        Vector3 oA = eA + rA * (side * w * 0.5f), oB = sB + rB * (side * wB * 0.5f);
        // Where the two outer edges (A's forward, B's backward) meet.
        List<Vector3> poly = new List<Vector3> { eA, oA };
        float det = -turn;
        bool miter = false;
        if (Mathf.Abs(det) > 0.01f)
        {
            Vector3 ob = oB - oA;
            float t = (-ob.x * dB.z + dB.x * ob.z) / det, s = (dA.x * ob.z - dA.z * ob.x) / det;
            if (t > -0.02f * w && s < 0.02f * w && t < 3f * w && -s < 3f * w)
            {
                Vector3 m = oA + dA * t;
                m.y = (oA.y + oB.y) * 0.5f;
                poly.Add(m);
                miter = true;
            }
        }
        poly.Add(oB);
        poly.Add(sB);

        joint = new GameObject("Corner Joint");
        joint.transform.SetParent(belt.transform, false);
        Transform tr = belt.transform;
        List<Vector3> local = new List<Vector3>();
        foreach (Vector3 p in poly) local.Add(tr.InverseTransformPoint(p));
        Vector3 centre = Vector3.zero;
        foreach (Vector3 p in local) centre += p;
        centre /= local.Count;

        PixelFactoryKit kit = new PixelFactoryKit(clicker, false);
        Color rubber = new Color(0.16f, 0.17f, 0.2f, 1f), steel = new Color(0.55f, 0.58f, 0.64f, 1f);

        // The slab: fan top and bottom plus a skirt round the edges (each triangle faced to match its own normal).
        List<Vector3> verts = new List<Vector3>(); List<Vector3> norms = new List<Vector3>(); List<int> tris = new List<int>();
        void Tri(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) < 0f) { Vector3 tmp = p1; p1 = p2; p2 = tmp; }
            int i0 = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2);
            norms.Add(n); norms.Add(n); norms.Add(n);
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        }
        Vector3 up = tr.InverseTransformDirection(Vector3.up);
        Vector3 hv = up * h;
        for (int i = 1; i + 1 < local.Count; i++)
        {
            Tri(local[0] + hv, local[i] + hv, local[i + 1] + hv, up);
            Tri(local[0], local[i], local[i + 1], -up);
        }
        for (int i = 0; i < local.Count; i++)
        {
            Vector3 p = local[i], q = local[(i + 1) % local.Count];
            Vector3 edge = q - p;
            if (edge.sqrMagnitude < 1e-8f) continue;
            Vector3 n = Vector3.Cross(edge, up).normalized;
            if (Vector3.Dot(n, (p + q) * 0.5f - centre) < 0f) n = -n;
            Tri(p, q, q + hv, n); Tri(p, q + hv, p + hv, n);
        }
        Mesh mesh = new Mesh { name = "Conveyor Corner" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        GameObject slab = new GameObject("Slab", typeof(MeshFilter), typeof(MeshRenderer));
        slab.transform.SetParent(joint.transform, false);
        slab.GetComponent<MeshFilter>().sharedMesh = mesh;
        slab.GetComponent<MeshRenderer>().sharedMaterial = kit.Mat(rubber, 0.1f, 0.3f);

        // Outer rails along the free edges of the corner (A's outer corner to the meeting point to B's outer corner).
        int first = 1, last = miter ? 3 : 2;
        for (int i = first; i < last; i++) Rail(kit, joint.transform, tr, poly[i], poly[i + 1], centre, unit, steel);
    }

    private static void Rail(PixelFactoryKit kit, Transform parent, Transform belt, Vector3 p, Vector3 q, Vector3 centreLocal, float unit, Color c)
    {
        Vector3 d = q - p; d.y = 0f;
        float len = d.magnitude;
        if (len < 0.02f * unit) return;
        float thick = 0.13f * unit, railH = 0.34f * unit;
        Vector3 dir = d / len;
        Vector3 mid = (p + q) * 0.5f;
        Vector3 outward = new Vector3(dir.z, 0f, -dir.x);
        Vector3 centreWorld = belt.TransformPoint(centreLocal);
        if (Vector3.Dot(outward, mid - centreWorld) < 0f) outward = -outward;
        Vector3 worldPos = mid + outward * (thick * 0.5f) + Vector3.up * (railH * 0.5f);
        Quaternion localRot = Quaternion.Inverse(belt.rotation) * Quaternion.LookRotation(dir, Vector3.up);
        kit.Box("Corner Rail", parent, belt.InverseTransformPoint(worldPos), new Vector3(thick, railH, len + thick), c, localRot.eulerAngles, false, 0.7f, 0.5f);
    }
}
