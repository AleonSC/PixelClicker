// ---- System notes ----
// PixelConveyorJoint.cs
//   INSIDE TRIMS (2026-10-19): the two belts' inner rails / frames / trims are cut back to where their inner edges cross (PixelConveyor.SetTrim), the end
//   rollers there are hidden and the chevrons stop short, so nothing runs across the other belt.
//   GEOMETRIC (2026-10-19): a corner is where a belt's b END meets another belt's end (PixelConveyor.CornerPartner), whichever way either runs, so a
//   single reversed piece keeps its rounded corners (pixels are still only handed on along the flow: FindNext).
//   ROUNDED (2026-10-19): the filler is a round SECTOR about the meeting point (outer edge an arc, a curved rail of short pieces along it) with a round
//   turntable plate over the meeting point, so chained belts turn in a smooth curve instead of a mitred corner.
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
        // GEOMETRY, not flow (2026-10-19): the corner is where this belt's b END meets another belt's end - so reversing one piece keeps its corners.
        bool partnerAtB = false;
        PixelConveyor next = belt.IsGhost ? null : belt.CornerPartner(out partnerAtB);
        Vector3 dA = Flat(belt.EndB - belt.EndA), dB = next != null ? (partnerAtB ? Flat(next.EndA - next.EndB) : Flat(next.EndB - next.EndA)) : dA;   // (into the corner / away from it)
        float turn = dA.x * dB.z - dA.z * dB.x;
        if (next == null || (Mathf.Abs(turn) < 0.05f && Vector3.Dot(dA, dB) > 0f) || Vector3.Dot(dA, dB) < -0.5f)
        {
            if (joint != null) Object.Destroy(joint);
            joint = null; key = null;
            ClearTrims(belt);
            return;
        }
        Vector3 eA = belt.EndB, sB = partnerAtB ? next.EndB : next.EndA;
        float w = belt.Width, wB = next.Width, h = belt.SurfaceHeight;
        string k = eA.ToString("F2") + sB.ToString("F2") + w.ToString("F2") + wB.ToString("F2") + h.ToString("F2");
        if (joint != null && k == key) return;
        key = k;
        if (joint != null) Object.Destroy(joint);

        float unit = belt.Unit;
        float side = turn > 0f ? 1f : -1f;   // which side of the belts is the OUTSIDE of the turn (+1 = their right)
        Vector3 rA = new Vector3(dA.z, 0f, -dA.x), rB = new Vector3(dB.z, 0f, -dB.x);
        // The INSIDE of the turn: each belt's inner rail would run across the other belt - cut both back to where the inner edges cross.
        {
            Vector3 nA = rA * -side, nB = rB * -side;
            Vector3 pA = eA + nA * (w * 0.5f), pB = sB + nB * (wB * 0.5f), diff = pB - pA;
            float det = -(dA.x * dB.z - dA.z * dB.x), extra = 0.13f * unit;   // (+ the rail's thickness, so the two rails meet)
            float t = Mathf.Abs(det) > 1e-4f ? (diff.x * (-dB.z) + dB.x * diff.z) / det : 0f;
            float s2 = Mathf.Abs(det) > 1e-4f ? (dA.x * diff.z - dA.z * diff.x) / det : 0f;
            if (belt.TrimmedPartner != null && (belt.TrimmedPartner != next || belt.TrimmedPartnerAtB != partnerAtB)) belt.TrimmedPartner.SetTrim(belt.TrimmedPartnerAtB, Vector3.zero, 0f);
            belt.SetTrim(true, nA, Mathf.Max(0f, -t) + extra);
            next.SetTrim(partnerAtB, nB, Mathf.Max(0f, s2) + extra);
            belt.TrimmedPartner = next;
            belt.TrimmedPartnerAtB = partnerAtB;
        }
        // ROUNDED (2026-10-19): the corner is a round sector about the point where the belts meet - its outer edge an ARC from this belt's outer corner
        // round to the next belt's - with a curved rail along it and a round turntable plate over the meeting point, so a chain turns in a smooth curve.
        Vector3 c = (eA + sB) * 0.5f;
        float radius = Mathf.Max(w, wB) * 0.5f;
        Vector3 fromDir = rA * side, toDir = rB * side;
        float a0 = Mathf.Atan2(fromDir.z, fromDir.x), a1 = Mathf.Atan2(toDir.z, toDir.x);
        float sweep = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        if (Vector3.Dot(dA, dB) < -0.99f) sweep = Mathf.PI * side;   // (a U-turn: half a circle round the outside)
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) * Mathf.Rad2Deg / 9f), 2, 24);
        List<Vector3> arc = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float ang = a0 + sweep * i / steps;
            Vector3 p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
            p.y = Mathf.Lerp(eA.y, sB.y, (float)i / steps);
            arc.Add(p);
        }
        List<Vector3> poly = new List<Vector3> { c };
        poly.AddRange(arc);

        joint = new GameObject("Corner Joint");
        joint.transform.SetParent(belt.transform, false);
        Transform tr = belt.transform;
        List<Vector3> local = new List<Vector3>();
        foreach (Vector3 p in poly) local.Add(tr.InverseTransformPoint(p));
        Vector3 centre = Vector3.zero;
        foreach (Vector3 p in local) centre += p;
        centre /= local.Count;

        PixelFactoryKit kit = new PixelFactoryKit(clicker, false);
        Color rubber = new Color(0.16f, 0.17f, 0.2f, 1f), steel = new Color(0.55f, 0.58f, 0.64f, 1f), frame = new Color(0.26f, 0.28f, 0.32f, 1f);

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
        for (int i = 1; i + 1 < local.Count; i++)   // (the skirt only round the arc: the straight sides sit inside the belts)
        {
            Vector3 p = local[i], q = local[i + 1];
            Vector3 edge = q - p;
            if (edge.sqrMagnitude < 1e-8f) continue;
            Vector3 n = Vector3.Cross(edge, up).normalized;
            if (Vector3.Dot(n, (p + q) * 0.5f - local[0]) < 0f) n = -n;
            Tri(p, q, q + hv, n); Tri(p, q + hv, p + hv, n);
        }
        Mesh mesh = new Mesh { name = "Conveyor Corner" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        GameObject slab = new GameObject("Slab", typeof(MeshFilter), typeof(MeshRenderer));
        slab.transform.SetParent(joint.transform, false);
        slab.GetComponent<MeshFilter>().sharedMesh = mesh;
        slab.GetComponent<MeshRenderer>().sharedMaterial = kit.Mat(rubber, 0.1f, 0.3f);

        // The curved outer rail (short pieces along the arc, just outside it) on a dark frame, and a round turntable plate with a steel hub over the
        // meeting point (it covers where the two straight belts overlap on the inside of the turn).
        for (int i = 0; i < arc.Count - 1; i++)
        {
            Vector3 p0 = arc[i], p1 = arc[i + 1];
            Rail(kit, joint.transform, tr, p0, p1, local[0], unit, steel);
        }
        Vector3 hubLocal = tr.InverseTransformPoint(c);
        kit.Cyl("Turntable", joint.transform, hubLocal + up * (h + 0.006f * unit), radius * 0.98f, 0.012f * unit, new Color(0.2f, 0.21f, 0.24f, 1f), default, 0.2f, 0.35f);
        kit.Cyl("Turntable Ring", joint.transform, hubLocal + up * (h + 0.01f * unit), radius * 0.62f, 0.012f * unit, frame, default, 0.5f, 0.5f);
        kit.Cyl("Turntable Hub", joint.transform, hubLocal + up * (h + 0.02f * unit), radius * 0.16f, 0.03f * unit, steel, default, 0.85f, 0.7f);
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward; }

    /// <summary>No corner at this belt's exit any more: its exit trim and the next belt's start trim go.</summary>
    private static void ClearTrims(PixelConveyor belt)
    {
        belt.SetTrim(true, Vector3.zero, 0f);
        if (belt.TrimmedPartner != null) belt.TrimmedPartner.SetTrim(belt.TrimmedPartnerAtB, Vector3.zero, 0f);
        belt.TrimmedPartner = null;
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
