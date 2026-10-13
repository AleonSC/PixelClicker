// ---- System notes ----
// PixelConveyor.cs
//   CONVEYOR BELT (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Belt'). Placing: click a point, then click another: a belt is built between the two (the second point
//   starts the next belt, so a series of short straight pieces makes a curve; right click ends the chain, a second right click stops placing). Clicks near the end of an existing belt
//   snap to it. RIGHT CLICK a placed belt (not while placing) to reverse its direction (PixelConveyorSystem). A belt is a low solid slab with side rails and moving chevrons that show
//   the way. LOCKED RIDING (no physics): an old pixel that lands on a belt (not held, not glued into a build, not flying, and there is room: riders keep a gap of 1.1 cube widths)
//   is CAPTURED by PixelConveyorSystem - made kinematic, centred on the line and carried along it at beltSpeedCubes (0 = 1.6 cube widths per second) by position, like the Storage Crate
//   locks pixels in. At the end of a belt it is handed on to a belt that STARTS there (a chained piece, ends within 0.45 cube widths and not head-on); when there is no belt to hand it
//   to, the end is detected and the pixel is EJECTED: physics back on, thrown on a little along the belt's direction (so it can fly into a Kiln mouth or onto the floor). Riders queue
//   when the one ahead is stopped. Flipping a belt flips its riders; grabbing a rider with Pixel Grabbing frees it. Things spat out of a machine or dropped by the player simply land on a
//   belt and get captured. Belts are NOT saved. Built with PixelFactoryKit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelConveyor : MonoBehaviour
{
    private static readonly List<PixelConveyor> all = new List<PixelConveyor>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

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

    public float Length => length;
    public float Unit => unit;
    public float SurfaceHeight => height;
    /// <summary>The end things enter at / leave from (in the direction of travel).</summary>
    public Vector3 StartPoint => reversed ? b : a;
    public Vector3 ExitPoint => reversed ? a : b;
    /// <summary>How far along the belt (from its start, in the direction of travel) this world point is.</summary>
    public float Along(Vector3 world) { Vector3 d = Direction; Vector3 v = world - StartPoint; return v.x * d.x + v.z * d.z; }
    /// <summary>The point on the belt line (at the belt's ground height) at distance s from its start.</summary>
    public Vector3 PointAt(float s) => StartPoint + Direction * s;

    /// <summary>The belt that carries on from the end of this one: its start is at this end and it does not run back towards it. Null = this is an end.</summary>
    public PixelConveyor FindNext()
    {
        PixelConveyor best = null;
        float bestD = (unit * 0.45f) * (unit * 0.45f);
        Vector3 exit = ExitPoint;
        foreach (PixelConveyor c in all)
        {
            if (c == null || c == this || c.ghostMode) continue;
            Vector3 sp = c.StartPoint;
            float d = (new Vector3(sp.x, exit.y, sp.z) - exit).sqrMagnitude;
            if (d >= bestD) continue;
            if (Vector3.Dot(c.Direction, Direction) < -0.5f) continue;   // head-on: that is an end, not a continuation
            bestD = d; best = c;
        }
        return best;
    }

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
        k.Box("Rail L", transform, new Vector3(-(width * 0.5f + rail * 0.5f), railH * 0.5f, 0f), new Vector3(rail, railH, length + rail * 2f), steel, default, false, 0.7f, 0.5f);   // (no collider: the player can walk on and off)
        k.Box("Rail R", transform, new Vector3(width * 0.5f + rail * 0.5f, railH * 0.5f, 0f), new Vector3(rail, railH, length + rail * 2f), steel, default, false, 0.7f, 0.5f);
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
                k.Box("Bar", chev.transform, new Vector3(bx, height + 0.004f * unit, -Mathf.Abs(bx) * Mathf.Tan(35f * Mathf.Deg2Rad)), new Vector3(half * 1.05f, 0.012f * unit, 0.07f * unit), accent, new Vector3(0f, s * 35f, 0f), false, 0.1f, 0.3f);
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
        PixelConveyorSystem.OnFlipped(this);   // riders turn round with it
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

    /// <summary>Is this world point (the player's feet) on the belt, with a little give at the sides (the rails)? 'lateral' = how far off the centre line it is (+ = to the right of the travel direction).</summary>
    public bool CarriesPlayer(Vector3 feet, out float lateral)
    {
        Vector3 l = transform.InverseTransformPoint(feet);
        lateral = Vector3.Dot(feet - transform.position, Vector3.Cross(Vector3.up, Direction));
        return Mathf.Abs(l.x) < width * 0.5f + 0.2f * unit && Mathf.Abs(l.z) < length * 0.5f && l.y > -0.05f * unit && l.y < height + 0.35f * unit;
    }

    public static IReadOnlyList<PixelConveyor> All => all;
}

/// <summary>One place that captures, carries, hands on and ejects the pixels riding every belt (physics step), and turns a belt round when it is right-clicked.</summary>
public class PixelConveyorSystem : MonoBehaviour
{
    private sealed class Rider
    {
        public Rigidbody rb;
        public PixelConveyor belt;
        public float s;
        public float half;     // half the pixel's height (it rides with its bottom on the belt)
    }

    [Header("Player on a belt (first person)")]
    [Min(0f)]
    [Tooltip("How strongly a belt carries the player, as a fraction of the belt's speed (1 = the belt's own speed). 0 = the coded default (1).")]
    [SerializeField] private float playerPull = 0f;

    [Min(0f)]
    [Tooltip("The soft magnet that draws the player to the belt's middle line, in cube widths per second at most. Walking out is always stronger. 0 = the coded default (0.9).")]
    [SerializeField] private float playerMagnet = 0f;

    private PixelClicker clicker;
    private static PixelConveyorSystem system;
    private readonly List<Rider> riders = new List<Rider>();
    private readonly HashSet<Rigidbody> riding = new HashSet<Rigidbody>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSystem() { system = null; }

    public static void Ensure(PixelClicker c) { if (system == null) { GameObject go = new GameObject("Conveyor System"); system = go.AddComponent<PixelConveyorSystem>(); system.clicker = c; } }

    /// <summary>A belt was reversed: its riders now travel the other way (their distance along it is mirrored).</summary>
    public static void OnFlipped(PixelConveyor belt)
    {
        if (system == null) return;
        foreach (Rider r in system.riders) if (r.belt == belt) r.s = Mathf.Clamp(belt.Length - r.s, 0f, belt.Length);
    }

    private static void SetVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    private bool Eligible(Rigidbody rb)
    {
        if (rb == null || rb.isKinematic || riding.Contains(rb) || PixelAttachment.IsAttached(rb) || clicker.IsFlyingPixel(rb)) return false;
        OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
        if (info != null && info.heldByPlayer) return false;
        OldPixelDespawn despawn = rb.GetComponent<OldPixelDespawn>();
        if (despawn != null && (despawn.IsDespawning || despawn.Held)) return false;
        return true;
    }

    /// <summary>Is there room for another pixel at distance s on this belt (no rider closer than a gap)?</summary>
    private bool RoomAt(PixelConveyor belt, float s, Rider ignore = null)
    {
        float gap = belt.Unit * 1.1f;
        foreach (Rider r in riders) if (r != ignore && r.belt == belt && Mathf.Abs(r.s - s) < gap) return false;
        return true;
    }

    private void Capture(Rigidbody rb, PixelConveyor belt, float s)
    {
        SetVelocity(rb, Vector3.zero);
        rb.isKinematic = true;
        float half = PixelAttachment.HalfExtents(rb, belt.Unit).y;
        riders.Add(new Rider { rb = rb, belt = belt, s = Mathf.Clamp(s, 0f, belt.Length), half = half });
        riding.Add(rb);
    }

    private void Release(Rider r, bool eject)
    {
        riding.Remove(r.rb);
        if (r.rb == null) return;
        r.rb.isKinematic = false;
        if (eject && r.belt != null) SetVelocity(r.rb, r.belt.Direction * r.belt.Speed * 1.15f + Vector3.up * r.belt.Unit * 0.5f);
        else SetVelocity(r.rb, Vector3.zero);
    }

    private void FixedUpdate()
    {
        if (clicker == null) return;
        IReadOnlyList<PixelConveyor> belts = PixelConveyor.All;

        // 1. Capture: a pixel that has landed on a belt (with room at its place) is locked onto it.
        if (belts.Count > 0)
        {
            var list = clicker.OldPixels;
            for (int i = 0; i < list.Count; i++)
            {
                Rigidbody rb = list[i];
                if (!Eligible(rb)) continue;
                Vector3 c = rb.worldCenterOfMass;
                for (int j = 0; j < belts.Count; j++)
                {
                    PixelConveyor belt = belts[j];
                    if (belt == null || belt.IsGhost || !belt.Carries(c)) continue;
                    float s = belt.Along(c);
                    if (RoomAt(belt, s)) Capture(rb, belt, s);
                    break;
                }
            }
        }

        // 2. Drop riders that went away (despawned, grabbed by the player, belt removed).
        for (int i = riders.Count - 1; i >= 0; i--)
        {
            Rider r = riders[i];
            bool gone = r.rb == null;
            OldPixelInfo info = gone ? null : r.rb.GetComponent<OldPixelInfo>();
            if (gone || (info != null && info.heldByPlayer) || r.belt == null)
            {
                if (!gone && (info == null || !info.heldByPlayer)) Release(r, false);   // (a grabbed pixel is the grab's business now)
                riding.Remove(r.rb);
                riders.RemoveAt(i);
            }
        }

        // 3. Carry them: front rider first, each one keeps a gap behind the one ahead; at the end hand on or eject.
        float dt = Time.fixedDeltaTime;
        for (int i = riders.Count - 1; i >= 0; i--)
        {
            Rider r = riders[i];
            PixelConveyor belt = r.belt;
            float gap = belt.Unit * 1.1f;
            float limit = belt.Length;
            foreach (Rider o in riders) if (o != r && o.belt == belt && o.s > r.s) limit = Mathf.Min(limit, o.s - gap);
            float want = r.s + belt.Speed * dt;
            bool atEnd = want >= belt.Length - 0.001f && limit >= belt.Length - 0.001f;
            r.s = Mathf.Clamp(Mathf.Min(want, limit), 0f, belt.Length);
            if (atEnd)
            {
                PixelConveyor next = belt.FindNext();
                if (next == null)
                {
                    riders.RemoveAt(i);
                    Release(r, true);      // an end: the pixel leaves the belt
                    continue;
                }
                if (RoomAt(next, 0f, r)) { r.belt = next; r.s = 0f; belt = next; }
            }
            // Position: on the line, bottom on the belt surface; glides to it (a pixel that landed from above settles onto the belt).
            Vector3 p = belt.PointAt(r.s);
            p.y += belt.SurfaceHeight + r.half;
            Vector3 current = r.rb.position;
            r.rb.MovePosition(Vector3.Lerp(current, p, 0.55f));
        }
    }

    /// <summary>First person: a player standing on a belt is carried along it and softly drawn to its middle line (never locked - walking out is stronger).</summary>
    private void CarryPlayer()
    {
        if (!PixelFirstPerson.TryGetPlayerPosition(out Vector3 feet)) return;
        PixelConveyor best = null;
        float bestLateral = 0f;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost || !belt.CarriesPlayer(feet, out float lat)) continue;
            if (best == null || Mathf.Abs(lat) < Mathf.Abs(bestLateral)) { best = belt; bestLateral = lat; }
        }
        if (best == null) return;
        float u = best.Unit;
        Vector3 dir = best.Direction;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float magnetMax = u * (playerMagnet > 0f ? playerMagnet : 0.9f);
        float pull = Mathf.Clamp(-bestLateral * 3f * u, -magnetMax, magnetMax);   // towards the middle, stronger the further off
        PixelFirstPerson.PushPlayer(dir * best.Speed * (playerPull > 0f ? playerPull : 1f) + side * pull);
    }

    private void Update()
    {
        if (clicker != null && PixelConveyor.All.Count > 0) CarryPlayer();
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

    private void OnDestroy()
    {
        foreach (Rider r in riders) if (r.rb != null) { r.rb.isKinematic = false; }
        riders.Clear();
        riding.Clear();
        if (system == this) system = null;
    }
}
