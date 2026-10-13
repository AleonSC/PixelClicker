// ---- System notes ----
// PixelConveyor.cs
//   CONVEYOR BELT (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Belt'). Placing: click a point, then click another: a belt is built between the two (the second point
//   starts the next belt, so a series of short straight pieces makes a curve; right click ends the chain, a second right click stops placing). Clicks near the end of an existing belt
//   snap to it. RIGHT CLICK a placed belt (not while placing) to reverse its direction (PixelConveyorSystem). A belt is a low solid slab with side rails and moving chevrons that show
//   the way; every old pixel standing on it (not held, not kinematic, not glued into a build) is carried along at beltSpeedCubes (0 = 1.6 cube widths per second) by
//   PixelConveyorSystem.FixedUpdate (one place for all belts): its sideways speed is damped so it stays on the line. Things spat out of a machine (Wood Chipper chips blocks,
//   Kiln charcoal) or dropped by the player simply land on it. Belts are NOT saved. Built with PixelFactoryKit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelConveyor : MonoBehaviour
{
    private static readonly List<PixelConveyor> all = new List<PixelConveyor>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); system = null; }

    [Min(0f)]
    [Tooltip("How fast the belt carries things, in cube widths per second. 0 = the coded default (1.6).")]
    [SerializeField] private float beltSpeedCubes = 0f;

    private PixelClicker clicker;
    private Vector3 a, b;
    private bool reversed;
    private float length, width, height, unit;
    private Transform ribsRoot;
    private readonly List<Transform> ribs = new List<Transform>();
    private float ribOffset, ribSpacing;

    public Vector3 EndA => a;
    public Vector3 EndB => b;
    public float Speed => unit * (beltSpeedCubes > 0f ? beltSpeedCubes : 1.6f);

    /// <summary>The way things move along the belt (flat, world space).</summary>
    public Vector3 Direction
    {
        get
        {
            Vector3 d = b - a; d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return Vector3.forward;
            return (reversed ? -d : d).normalized;
        }
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    // ---------------- placing ----------------

    /// <summary>A small marker disc that follows the mouse while the first point is chosen.</summary>
    public static GameObject CreateMarker(PixelClicker c)
    {
        PixelFactoryKit k = new PixelFactoryKit(c, true);
        float u = c.OldPixelWorldSize;
        GameObject root = new GameObject("Conveyor Marker");
        k.Cyl("Disc", root.transform, new Vector3(0f, 0.05f * u, 0f), 0.45f * u, 0.1f * u, new Color(0.4f, 0.9f, 1f, 1f));
        k.Cyl("Dot", root.transform, new Vector3(0f, 0.12f * u, 0f), 0.12f * u, 0.12f * u, Color.white);
        return root;
    }

    /// <summary>The see-through belt that stretches from the first point to the mouse.</summary>
    public static GameObject CreatePreview(PixelClicker c)
    {
        GameObject root = new GameObject("Conveyor Preview");
        PixelConveyor belt = root.AddComponent<PixelConveyor>();
        belt.clicker = c;
        belt.unit = c.OldPixelWorldSize;
        belt.ghostMode = true;
        belt.Rebuild(Vector3.zero, Vector3.forward * belt.unit);
        return root;
    }

    private bool ghostMode;
    public bool IsGhost => ghostMode;

    /// <summary>Re-shapes a preview belt between two points.</summary>
    public static void UpdatePreview(GameObject preview, Vector3 from, Vector3 to)
    {
        if (preview == null) return;
        PixelConveyor belt = preview.GetComponent<PixelConveyor>();
        if (belt == null) return;
        Vector3 d = to - from; d.y = 0f;
        if (d.magnitude < 0.2f * belt.unit) { preview.SetActive(false); return; }
        preview.SetActive(true);
        if ((belt.a - from).sqrMagnitude > 1e-4f || (belt.b - to).sqrMagnitude > 1e-4f) belt.Rebuild(from, to);
    }

    /// <summary>A working belt between two ground points.</summary>
    public static GameObject Place(PixelClicker c, Vector3 from, Vector3 to)
    {
        Vector3 d = to - from; d.y = 0f;
        float unit = c.OldPixelWorldSize;
        if (d.magnitude < 0.5f * unit) return null;
        GameObject root = new GameObject("Conveyor Belt");
        PixelConveyor belt = root.AddComponent<PixelConveyor>();
        belt.clicker = c;
        belt.unit = unit;
        belt.Rebuild(from, to);
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    /// <summary>If the point is close to the end of a placed belt, returns that end (so belts chain neatly).</summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p)
    {
        float reach = c.OldPixelWorldSize * 0.9f;
        float best = reach * reach;
        Vector3 result = p;
        foreach (PixelConveyor belt in all)
        {
            if (belt == null || belt.ghostMode) continue;
            foreach (Vector3 end in new[] { belt.a, belt.b })
            {
                float d = (new Vector3(end.x, p.y, end.z) - p).sqrMagnitude;
                if (d < best) { best = d; result = new Vector3(end.x, p.y, end.z); }
            }
        }
        return result;
    }

    // ---------------- building ----------------

    private void Rebuild(Vector3 from, Vector3 to)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        ribs.Clear();
        a = from; b = to;
        Vector3 d = b - a; d.y = 0f;
        length = d.magnitude;
        width = 1.35f * unit;
        height = 0.16f * unit;
        transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up));

        PixelFactoryKit k = new PixelFactoryKit(clicker, ghostMode);
        Color rubber = new Color(0.16f, 0.17f, 0.2f, 1f), steel = new Color(0.55f, 0.58f, 0.64f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f);
        // The belt slab (its top is the surface things stand on) and the side rails.
        k.Box("Belt", transform, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, length), rubber, default, true, 0.1f, 0.3f);
        float rail = 0.13f * unit, railH = 0.34f * unit;
        k.Box("Rail L", transform, new Vector3(-(width * 0.5f + rail * 0.5f), railH * 0.5f, 0f), new Vector3(rail, railH, length + rail * 2f), steel, default, true, 0.7f, 0.5f);
        k.Box("Rail R", transform, new Vector3(width * 0.5f + rail * 0.5f, railH * 0.5f, 0f), new Vector3(rail, railH, length + rail * 2f), steel, default, true, 0.7f, 0.5f);
        // Rollers at both ends.
        k.Cyl("Roller A", transform, new Vector3(0f, height * 0.5f, -length * 0.5f), height * 0.6f, width * 1.02f, steel, new Vector3(0f, 0f, 90f), 0.8f, 0.5f);
        k.Cyl("Roller B", transform, new Vector3(0f, height * 0.5f, length * 0.5f), height * 0.6f, width * 1.02f, steel, new Vector3(0f, 0f, 90f), 0.8f, 0.5f);

        // Chevrons that travel along the belt and show the direction.
        GameObject rr = new GameObject("Ribs");
        rr.transform.SetParent(transform, false);
        ribsRoot = rr.transform;
        ribSpacing = 0.55f * unit;
        int count = Mathf.Max(1, Mathf.CeilToInt(length / ribSpacing));
        for (int i = 0; i < count; i++)
        {
            GameObject chev = new GameObject("Chevron");
            chev.transform.SetParent(ribsRoot, false);
            float half = width * 0.5f * 0.8f;
            for (int s = -1; s <= 1; s += 2)
            {
                // A V that points the way the belt runs (+Z): two bars swept back from the middle.
                float bx = s * half * 0.5f;
                k.Box("Bar", chev.transform, new Vector3(bx, height + 0.004f * unit, -Mathf.Abs(bx) * Mathf.Tan(35f * Mathf.Deg2Rad)), new Vector3(half * 1.05f, 0.012f * unit, 0.07f * unit), accent, new Vector3(0f, s * 35f, 0f), 0.1f, 0.3f);
            }
            ribs.Add(chev.transform);
        }
        PlaceRibs();
    }

    private void PlaceRibs()
    {
        if (ribsRoot == null || ribs.Count == 0) return;
        ribsRoot.localRotation = reversed ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        for (int i = 0; i < ribs.Count; i++)
        {
            float t = Mathf.Repeat(ribOffset + i * ribSpacing, ribs.Count * ribSpacing);
            float z = -length * 0.5f + t;
            if (z > length * 0.5f) z -= ribs.Count * ribSpacing;
            bool visible = z > -length * 0.5f + 0.05f * unit && z < length * 0.5f - 0.05f * unit;
            ribs[i].gameObject.SetActive(visible);
            ribs[i].localPosition = new Vector3(0f, 0f, reversed ? -z : z);
        }
    }

    private void Update()
    {
        if (ghostMode) return;
        ribOffset += Time.deltaTime * Speed;
        PlaceRibs();
    }

    /// <summary>Right click: the belt runs the other way.</summary>
    public void Flip()
    {
        reversed = !reversed;
        PlaceRibs();
        PixelAudio.Play("device_place", 1.3f);
    }

    // ---------------- carrying (called by PixelConveyorSystem.FixedUpdate) ----------------

    /// <summary>Is this world point standing on the belt (inside its footprint, just above the slab)?</summary>
    public bool Carries(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world);
        return Mathf.Abs(l.x) < width * 0.5f && Mathf.Abs(l.z) < length * 0.5f + 0.1f * unit && l.y > height - 0.1f * unit && l.y < height + 1.1f * unit;
    }

    public static IReadOnlyList<PixelConveyor> All => all;
}

/// <summary>One place that moves things on every belt (physics step), and turns a belt round when it is right-clicked.</summary>
public class PixelConveyorSystem : MonoBehaviour
{
    private PixelClicker clicker;

    public static void Ensure(PixelClicker c) { if (system == null) { GameObject go = new GameObject("Conveyor System"); system = go.AddComponent<PixelConveyorSystem>(); system.clicker = c; } }

    private static PixelConveyorSystem system;

    private static void SetFlat(Rigidbody rb, Vector3 flatTarget, float k)
    {
#if UNITY_6000_0_OR_NEWER
        Vector3 v = rb.linearVelocity;
#else
        Vector3 v = rb.velocity;
#endif
        v.x += (flatTarget.x - v.x) * k;
        v.z += (flatTarget.z - v.z) * k;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    private void FixedUpdate()
    {
        if (clicker == null) return;
        var list = clicker.OldPixels;
        IReadOnlyList<PixelConveyor> belts = PixelConveyor.All;
        if (belts.Count == 0) return;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody rb = list[i];
            if (rb == null || rb.isKinematic || PixelAttachment.IsAttached(rb) || clicker.IsFlyingPixel(rb)) continue;
            for (int j = 0; j < belts.Count; j++)
            {
                PixelConveyor belt = belts[j];
                if (belt == null || belt.IsGhost || !belt.Carries(rb.worldCenterOfMass)) continue;
                Vector3 dir = belt.Direction;
                SetFlat(rb, dir * belt.Speed, 0.3f);
                // Keep it on the line: damp what points sideways.
                Vector3 side = Vector3.Cross(Vector3.up, dir);
#if UNITY_6000_0_OR_NEWER
                Vector3 v = rb.linearVelocity;
#else
                Vector3 v = rb.velocity;
#endif
                float s = Vector3.Dot(v, side);
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = v - side * s * 0.4f;
#else
                rb.velocity = v - side * s * 0.4f;
#endif
                break;   // one belt at a time
            }
        }
    }

    private void Update()
    {
        if (clicker == null || !PixelInput.RightPressed() || PixelClicker.ExternalClickBlock || PixelPauseMenu.IsPaused || PixelInput.PointerOverUI() || PixelFirstPerson.Active) return;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(PixelInput.PointerPosition()), 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return;   // the cube's own right click comes first
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return;
            PixelConveyor belt = h.collider.GetComponentInParent<PixelConveyor>();
            if (belt != null) { belt.Flip(); return; }
        }
    }
}
