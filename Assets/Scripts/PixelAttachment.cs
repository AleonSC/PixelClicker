// ---- System notes ----
// PixelAttachment.cs
//   Attaching old pixels to each other (the first person BUILD controls, PixelFirstPerson): the snap maths, the glue and the prying off.
//   * PixelAttachment.TrySnap(held, pool, ...): for the cube you hold, looks at the nearby old pixels and finds where it would sit if it were stuck to one of their faces. The held cube's
//     orientation must be within maxAngle of an orientation ALIGNED with that pixel (one of the 24 cube rotations, NearestAligned); the spot is on one of the pixel's six faces (its centre pushed
//     out by both half sizes) and slides along the face in half-cube steps (-0.5, 0, +0.5), so cubes can stack straight or like bricks. It must be within snapDistance of where you hold it.
//     The result is the pose of the white ghost; clicking attaches there.
//   * PixelAttachment.Attach(a, b): a FixedJoint from a to b (unbreakable, colliders ignore each other) and a PixelAttachment component on both. Attached pixels keep their lifetime frozen
//     (OldPixelDespawn.Held is re-asserted every frame) so a build does not decay; they can't be picked up, only pried off: PixelAttachment.Detach(x) removes every joint to and from x
//     (the first person player holds the RIGHT mouse button on it). Joints are not saved: loading a save leaves the cubes loose.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelAttachment : MonoBehaviour
{
    private static readonly List<PixelAttachment> all = new List<PixelAttachment>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private Rigidbody body;
    private OldPixelDespawn despawn;
    private readonly List<FixedJoint> joints = new List<FixedJoint>();

    public static bool IsAttached(Rigidbody b) { return b != null && b.GetComponent<PixelAttachment>() != null; }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }

    private void OnDisable() { all.Remove(this); }

    private void Update()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (despawn == null) despawn = GetComponent<OldPixelDespawn>();
        if (despawn != null && !despawn.Held) despawn.Held = true;   // a build never decays while it is stuck together
    }

    private static PixelAttachment Ensure(Rigidbody b)
    {
        PixelAttachment a = b.GetComponent<PixelAttachment>();
        if (a == null) a = b.gameObject.AddComponent<PixelAttachment>();
        a.body = b;
        a.despawn = b.GetComponent<OldPixelDespawn>();
        if (a.despawn != null) a.despawn.Held = true;
        return a;
    }

    /// <summary>Glues 'a' to 'b' (both stay physical bodies, so a build can be pushed about as one piece).</summary>
    public static void Attach(Rigidbody a, Rigidbody b)
    {
        if (a == null || b == null || a == b) return;
        PixelAttachment pa = Ensure(a);
        Ensure(b);
        FixedJoint joint = a.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = b;
        joint.enableCollision = false;
        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;
        pa.joints.Add(joint);
        PixelStats.Count("old.attached");
    }

    /// <summary>Pries 'x' loose: every joint it has, and every joint of another cube that holds on to it. Cubes left with no joints become ordinary again.</summary>
    public static void Detach(Rigidbody x)
    {
        if (x == null) return;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            PixelAttachment pa = all[i];
            if (pa == null) { all.RemoveAt(i); continue; }
            bool mine = pa.body == x;
            for (int j = pa.joints.Count - 1; j >= 0; j--)
            {
                FixedJoint joint = pa.joints[j];
                if (joint == null) { pa.joints.RemoveAt(j); continue; }
                if (mine || joint.connectedBody == x) { Destroy(joint); pa.joints.RemoveAt(j); }
            }
        }
        // Anything now without any joint (and not held by another's joint) goes back to normal.
        List<PixelAttachment> snapshot = new List<PixelAttachment>(all);
        foreach (PixelAttachment pa in snapshot)
        {
            if (pa == null) continue;
            bool referenced = pa.joints.Count > 0;
            if (!referenced)
                foreach (PixelAttachment other in snapshot)
                    if (other != null && other != pa && other.joints.Exists(j => j != null && j.connectedBody == pa.body)) { referenced = true; break; }
            if (referenced) continue;
            if (pa.despawn != null) pa.despawn.Held = false;
            Destroy(pa);
        }
    }

    // ------------------------------------------------------------------
    // The snap
    // ------------------------------------------------------------------

    public struct Snap
    {
        public Vector3 position;
        public Quaternion rotation;
        public Rigidbody target;
    }

    private static readonly Vector3[] Axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
    private static Quaternion[] aligned;

    /// <summary>The 24 ways a cube can sit in a grid.</summary>
    private static Quaternion[] AlignedSet()
    {
        if (aligned != null) return aligned;
        List<Quaternion> list = new List<Quaternion>();
        foreach (Vector3 f in Axes)
            foreach (Vector3 u in Axes)
                if (Mathf.Abs(Vector3.Dot(f, u)) < 0.01f) list.Add(Quaternion.LookRotation(f, u));
        aligned = list.ToArray();
        return aligned;
    }

    /// <summary>Of the 24 grid orientations relative to 'gridRot', the one closest to 'current'.</summary>
    public static Quaternion NearestAligned(Quaternion gridRot, Quaternion current)
    {
        Quaternion best = gridRot;
        float bestAngle = float.MaxValue;
        foreach (Quaternion g in AlignedSet())
        {
            Quaternion q = gridRot * g;
            float a = Quaternion.Angle(q, current);
            if (a < bestAngle) { bestAngle = a; best = q; }
        }
        return best;
    }

    /// <summary>A pixel's half size along each of its own axes (from its box collider, which may be switched off; else a plain cube of 'fallback').</summary>
    public static Vector3 HalfExtents(Rigidbody b, float fallback)
    {
        BoxCollider box = b != null ? b.GetComponent<BoxCollider>() : null;
        if (box == null) return Vector3.one * fallback * 0.5f;
        Vector3 s = b.transform.lossyScale;
        return Vector3.Scale(box.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))) * 0.5f;
    }

    /// <summary>
    /// Where would 'held' (at its current pose, half sizes 'heldHalf') stick to one of the pixels in 'pool'? 'size' = a cube's edge (steps and distances scale with it).
    /// False when no pixel's face is close enough with the right orientation.
    /// </summary>
    public static bool TrySnap(Rigidbody held, Vector3 heldHalf, IReadOnlyList<Rigidbody> pool, float size, float maxAngle, float snapDistance, out Snap result)
    {
        result = default;
        if (held == null || pool == null) return false;
        float best = float.MaxValue;
        Vector3 heldPos = held.transform.position;
        Quaternion heldRot = held.transform.rotation;
        float near = size * 3f;
        for (int i = 0; i < pool.Count; i++)
        {
            Rigidbody b = pool[i];
            if (b == null || b == held || b.isKinematic) continue;
            Vector3 d = heldPos - b.transform.position;
            if (d.sqrMagnitude > near * near) continue;
            Quaternion q = NearestAligned(b.transform.rotation, heldRot);
            if (Quaternion.Angle(q, heldRot) > maxAngle) continue;
            Vector3 bHalf = HalfExtents(b, size);
            Quaternion br = b.transform.rotation;
            Vector3 bc = b.transform.position;
            for (int f = 0; f < 6; f++)
            {
                int axis = f / 2;
                Vector3 nb = br * Axes[f];
                if (Vector3.Dot(d, nb) <= 0f) continue;   // the held cube must be on this face's side
                float hB = axis == 0 ? bHalf.x : axis == 1 ? bHalf.y : bHalf.z;
                float hA = Mathf.Abs(Vector3.Dot(nb, q * Vector3.right)) * heldHalf.x + Mathf.Abs(Vector3.Dot(nb, q * Vector3.up)) * heldHalf.y + Mathf.Abs(Vector3.Dot(nb, q * Vector3.forward)) * heldHalf.z;
                Vector3 t1 = br * Axes[((axis + 1) % 3) * 2], t2 = br * Axes[((axis + 2) % 3) * 2];
                float step = size * 0.5f;
                float l1 = Mathf.Clamp(Mathf.Round(Vector3.Dot(d, t1) / step) * step, -step, step);
                float l2 = Mathf.Clamp(Mathf.Round(Vector3.Dot(d, t2) / step) * step, -step, step);
                Vector3 p = bc + nb * (hB + hA) + t1 * l1 + t2 * l2;
                float dist = Vector3.Distance(heldPos, p);
                if (dist < snapDistance && dist < best)
                {
                    best = dist;
                    result = new Snap { position = p, rotation = q, target = b };
                }
            }
        }
        return best < float.MaxValue;
    }
}
