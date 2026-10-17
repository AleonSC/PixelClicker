// ---- System notes ----
// PixelConveyor.cs
//   HEIGHT / RAMPS: belt ends (a, b) are full 3D points - the y is the belt's underside. While placing, the scroll wheel raises / lowers the point being placed (PixelFeatureTester.UpdateLine, steps of half a cube,
//   ClampEnd keeps the slope under about 37 degrees); posts (BuildSupports) stand on the floor (FloorY) under a raised belt. Riders measure their place along the GROUND (Length = horizontal length), PointAt gives the
//   sloped height, RunSpeed / LiftFactor adjust speed and rest height for the slope. Snap() matches belt ends and elevator tops by SCREEN distance, so raised ends can be snapped to; FindNext only chains belts
//   whose heights match. The Conveyor Elevator (PixelElevator.cs) lifts pixels between heights.
//   CORNERS: where belts chain at an angle the gap on the outside of the turn is filled automatically (PixelConveyorJoint.cs).
//   WIDTH: PixelConveyor.LaneCount = 1 cube wide, 2 at Electrician level 10, 3 at level 20 (every belt, PixelConveyorSystem.Update rebuilds them when it changes). Each rider travels in a LANE (belt-local x, PixelConveyor.LaneOffset / NearestLane): a pixel is
//   captured into the lane it landed in, riders keep their gap per lane, and a hand-on to the next belt picks the nearest lane there.
//   CONVEYOR BELT (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Belt'). Placing: click a point, then click another: a belt is built between the two (the second point
//   starts the next belt, so a series of short straight pieces makes a curve; right click ends the chain, a second right click stops placing). Clicks near the end of an existing belt
//   snap to it. RIGHT CLICK a placed belt (not while placing) to reverse its direction (PixelConveyorSystem). A belt is a low solid slab with side rails and moving chevrons that show
//   the way. LOCKED RIDING (no physics): an old pixel that lands on a belt (not held, not glued into a build, not flying, and there is room: riders keep a gap of 1.1 cube widths)
//   is CAPTURED by PixelConveyorSystem - made kinematic, centred on the line and carried along it at beltSpeedCubes (0 = 1.6 cube widths per second) by position, like the Storage Crate
//   locks pixels in. At the end of a belt it is handed on to a belt that STARTS there (a chained piece, ends within 0.45 cube widths and not head-on); when there is no belt to hand it
//   to, the end is detected: a machine whose opening is there (IPixelBeltAcceptor: the Kiln mouth, the Storage Crate) takes the pixel straight in, otherwise it is EJECTED: physics back on, thrown on a little along the belt's direction (so it can fly into a Kiln mouth or onto the floor). Riders queue
//   when the one ahead is stopped. Flipping a belt flips its riders; grabbing a rider with Pixel Grabbing frees it. Things spat out of a machine or dropped by the player simply land on a
//   belt and get captured. Belts are saved with the tester save (shape incl. heights, riders). Built with PixelFactoryKit. SPEED: each belt can have its own speed - in FIRST PERSON a right click on a belt opens a small window (PixelConveyorSystem
//   speed editor: a number box in cube widths per second, Set / Reverse / Close; the mouse is freed while it is open); in the normal view a right click still reverses the belt.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Something a belt can hand a pixel to at its end (the Kiln's mouth, the Storage Crate): no ejecting, the pixel goes straight in.</summary>
public interface IPixelBeltAcceptor
{
    /// <summary>Is this world point (just past the end of a belt) at the acceptor's opening?</summary>
    bool AcceptsAt(Vector3 world);
    /// <summary>Takes the pixel (it is kinematic and no longer owned by the belt). False if it cannot (full): the belt then ejects it.</summary>
    bool AcceptFromBelt(Rigidbody pixel);
}

public class PixelConveyor : MonoBehaviour
{
    private static readonly List<PixelConveyor> all = new List<PixelConveyor>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    [Min(0f)]
    [Tooltip("How fast the belt carries things, in cube widths per second. 0 = the coded default (1.6).")]
    [SerializeField] private float beltSpeedCubes = 0f;

    /// <summary>How many cubes wide every belt is: 1, 2 at Electrician level 10, 3 at level 20 (each belt rebuilds itself when this changes).</summary>
    public static int LaneCount
    {
        get
        {
            int level = PixelSkills.LevelOfId(PixelSkills.ElectricianId);
            return 1 + (level >= 10 ? 1 : 0) + (level >= 20 ? 1 : 0);
        }
    }

    private int lanesBuilt = 1;
    /// <summary>The number of lanes this belt was built with (a pixel rides in the middle of one lane).</summary>
    public int Lanes => lanesBuilt;
    private float LanePitch => 1.1f * unit;
    /// <summary>The sideways place of a lane (belt-local x, so it does not change when the belt is reversed).</summary>
    public float LaneOffset(int lane) => (lane - (lanesBuilt - 1) * 0.5f) * LanePitch;
    /// <summary>The lane nearest a world point.</summary>
    public int NearestLane(Vector3 world)
    {
        float x = Vector3.Dot(world - transform.position, transform.right);
        return Mathf.Clamp(Mathf.RoundToInt(x / LanePitch + (lanesBuilt - 1) * 0.5f), 0, lanesBuilt - 1);
    }
    /// <summary>The point of a lane at distance s from the belt's start (at the belt's ground height).</summary>
    public Vector3 LanePointAt(int lane, float s) => PointAt(s) + transform.right * LaneOffset(lane);
    /// <summary>The electrician's upgrade changed the width: build the belt again at the new width (nothing else about it changes).</summary>
    public void RefreshLanes()
    {
        if (ghostMode || LaneCount == lanesBuilt || a == b) return;
        Rebuild(a, b);
    }

    private PixelClicker clicker;
    private Vector3 a, b;
    private bool reversed;
    private float length, hLength, width, height, unit;   // length = the belt's true length (slope included), hLength = its length along the ground (what riders measure)
    private Transform ribsRoot;
    private readonly List<Transform> ribs = new List<Transform>();
    private float ribOffset, ribSpacing;

    /// <summary>The belt's length along the ground (riders measure their place along it from 0 to this).</summary>
    public float Length => hLength;
    /// <summary>Rise per metre travelled along the ground in the direction of travel (positive = the belt climbs).</summary>
    public float SlopeUp => hLength > 0.0001f ? (ExitPoint.y - StartPoint.y) / hLength : 0f;
    /// <summary>Ground speed of things riding the belt (the belt's own speed shortened by the slope, so they move at the belt's real speed).</summary>
    public float RunSpeed => Speed / Mathf.Sqrt(1f + SlopeUp * SlopeUp);
    /// <summary>1 / cos of the slope: a pixel resting on the belt sits this much higher than the slab (straight up) to keep its distance from the surface.</summary>
    public float LiftFactor => Mathf.Sqrt(1f + SlopeUp * SlopeUp);
    /// <summary>The highest point of the floor under the belt ends (the supports stand on it).</summary>
    public static float FloorY(PixelClicker c) { PixelFloor f = PixelFloor.Instance; return f != null && f.FloorRenderer != null ? f.FloorRenderer.bounds.max.y : 0f; }
    /// <summary>The belt whose exit is nearest the mouse: within groundReach of the ground point, or within about 80 screen pixels of the mouse (so raised ends can be picked). Null if none.</summary>
    public static PixelConveyor NearestExit(PixelClicker c, Vector3 groundPoint, float groundReach)
    {
        Camera cam = c.ViewCamera;
        Vector2 mouse = PixelInput.PointerPosition();
        PixelConveyor best = null;
        float bestScore = float.MaxValue;
        foreach (PixelConveyor belt in all)
        {
            if (belt == null || belt.ghostMode) continue;
            Vector3 e = belt.ExitPoint;
            float score = float.MaxValue;
            float flat = (new Vector3(e.x, groundPoint.y, e.z) - groundPoint).magnitude;
            if (flat < groundReach) score = flat / groundReach;
            if (cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(e);
                if (sp.z > 0f) { float px = (new Vector2(sp.x, sp.y) - mouse).magnitude; if (px < 80f) score = Mathf.Min(score, px / 80f); }
            }
            if (score < bestScore) { bestScore = score; best = belt; }
        }
        return best;
    }

    /// <summary>The belt nearest to a ground point, with the distance along it: true when the point is within the belt's half width plus 'slackCubes' (default 0.5 cube widths) of it.</summary>
    public static bool NearestOn(PixelClicker c, Vector3 point, out PixelConveyor best, out float s, float slackCubes = 0.5f)
    {
        best = null; s = 0f;
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize), bestD = float.MaxValue;
        foreach (PixelConveyor belt in all)
        {
            if (belt == null || belt.ghostMode) continue;
            float along = Mathf.Clamp(belt.Along(point), 0f, belt.hLength);
            Vector3 p = belt.PointAt(along);
            float d = new Vector3(p.x - point.x, 0f, p.z - point.z).magnitude;
            if (d < belt.width * 0.5f + slackCubes * u && d < bestD) { bestD = d; best = belt; s = along; }
        }
        return best != null;
    }

    /// <summary>Keeps a belt from being steeper than about 37 degrees: returns 'to' with its height pulled in if needed.</summary>
    public static Vector3 ClampEnd(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from; d.y = 0f;
        float limit = d.magnitude * 0.75f;
        to.y = from.y + Mathf.Clamp(to.y - from.y, -limit, limit);
        return to;
    }
    public float Unit => unit;
    public float SurfaceHeight => height;
    /// <summary>The belt slab's width (all lanes).</summary>
    public float Width => width;
    /// <summary>The end things enter at / leave from (in the direction of travel).</summary>
    public Vector3 StartPoint => reversed ? b : a;
    public Vector3 ExitPoint => reversed ? a : b;
    /// <summary>How far along the belt (from its start, in the direction of travel) this world point is.</summary>
    public float Along(Vector3 world) { Vector3 d = Direction; Vector3 v = world - StartPoint; return v.x * d.x + v.z * d.z; }
    /// <summary>The point on the belt line (at the belt's ground height) at distance s from its start.</summary>
    public Vector3 PointAt(float s) { Vector3 p = StartPoint + Direction * s; p.y = StartPoint.y + SlopeUp * s; return p; }

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
            if (d >= bestD || Mathf.Abs(sp.y - exit.y) > unit * 0.45f) continue;   // (a belt at another height is not a continuation)
            if (Vector3.Dot(c.Direction, Direction) < -0.5f) continue;   // head-on: that is an end, not a continuation
            bestD = d; best = c;
        }
        return best;
    }

    public Vector3 EndA => a;
    public Vector3 EndB => b;
    private float customSpeedCubes = -1f;   // set by the player (first person right click); negative = the default
    /// <summary>The belt's speed in cube widths per second.</summary>
    public float SpeedCubes => customSpeedCubes >= 0f ? customSpeedCubes : (beltSpeedCubes > 0f ? beltSpeedCubes : 1.6f);
    public float Speed => unit * SpeedCubes;
    public void SetSpeedCubes(float cubesPerSecond) { customSpeedCubes = Mathf.Clamp(cubesPerSecond, 0f, 30f); }
    public void FlipPublic() { Flip(); }

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

    /// <summary>
    /// If the mouse is close (on screen) to the end of a placed belt or to an elevator's top output, returns that end - at ITS height - so belts chain neatly (also at different heights).
    /// 'p' = the mouse's point on the ground (plus the player's chosen height); 'snapped' says whether it latched.
    /// </summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p, out bool snapped)
    {
        bool found = false;
        Camera cam = c.ViewCamera;
        Vector2 mouse = PixelInput.PointerPosition();
        float best = 70f * 70f;                       // screen pixels
        float groundReach = c.OldPixelWorldSize * 0.9f;
        Vector3 result = p;
        void Try(Vector3 end)
        {
            float score = float.MaxValue;
            if (cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(end);
                if (sp.z > 0f) score = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            }
            float flat = (new Vector3(end.x, p.y, end.z) - p).sqrMagnitude;
            if (flat < groundReach * groundReach) score = Mathf.Min(score, 0f + flat);   // (an end right under the pointer on the ground)
            if (score < best) { best = score; result = end; found = true; }
        }
        foreach (PixelConveyor belt in all)
        {
            if (belt == null || belt.ghostMode) continue;
            Try(belt.a); Try(belt.b);
        }
        foreach (PixelElevator lift in PixelElevator.All) if (lift != null && !lift.IsGhost) Try(lift.OutputPoint);
        foreach (PixelSplitter split in PixelSplitter.All) if (split != null && !split.IsGhost) for (int i = 0; i < 3; i++) Try(split.OutputPoint(i));
        foreach (PixelConnector con in PixelConnector.All) if (con != null && !con.IsGhost) { Try(con.EndPoint); if (con.IsAdapter) { Try(con.SidePort(1)); Try(con.SidePort(-1)); } }
        PixelCouplings.Ports(Try);   // couplings on devices: a belt ends at an In coupling or starts at an Out one
        snapped = found;
        return result;
    }

    // ---------------- building ----------------

    private void Rebuild(Vector3 from, Vector3 to)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        ribs.Clear();
        jointKey = null; joint = null;   // (the corner filler was a child: gone with the rest)
        a = from; b = to;
        Vector3 d = b - a; d.y = 0f;
        hLength = d.magnitude;
        length = (b - a).magnitude;
        lanesBuilt = LaneCount;
        width = (1.35f + (lanesBuilt - 1) * 1.1f) * unit;   // one cube wide, plus a cube for every upgrade (Electrician levels 10 and 20)
        height = 0.16f * unit;
        transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation((b - a).normalized, Vector3.up));

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
        BuildSupports(k, steel);
        ApplySelection();
        PlaceRibs();
    }

    /// <summary>Posts under a raised belt, from the floor up to the belt's underside: at both ends and every few cube widths between (they stay upright however the belt slopes).</summary>
    private void BuildSupports(PixelFactoryKit k, Color steel)
    {
        float floorY = FloorY(clicker);
        Vector3 flat = b - a; flat.y = 0f;
        if (hLength < 0.01f) return;
        flat /= hLength;
        int posts = Mathf.Max(2, Mathf.CeilToInt(hLength / (5f * unit)) + 1);
        GameObject holder = new GameObject("Supports");
        holder.transform.SetParent(transform, false);
        holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // (so the posts below are placed in world coordinates)
        for (int i = 0; i < posts; i++)
        {
            float t = hLength * i / (posts - 1);
            Vector3 p = a + flat * t;
            float under = a.y + (b.y - a.y) * (t / hLength) - 0.02f * unit;   // the belt's underside
            float h = under - floorY;
            if (h < 0.3f * unit) continue;
            k.Box("Post", holder.transform, new Vector3(p.x, floorY + h * 0.5f, p.z), new Vector3(0.14f * unit, h, 0.14f * unit), steel, default, false, 0.7f, 0.5f);
            k.Box("Foot", holder.transform, new Vector3(p.x, floorY + 0.02f * unit, p.z), new Vector3(0.45f * unit, 0.04f * unit, 0.45f * unit), steel, default, false, 0.7f, 0.5f);
        }
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

    private string jointKey;
    private GameObject joint;
    private float nextJointCheck;

    private void Update()
    {
        if (ghostMode) return;
        ribOffset += Time.deltaTime * Speed;
        PlaceRibs();
        if (Time.time >= nextJointCheck) { nextJointCheck = Time.time + 0.5f + Random.value * 0.1f; PixelConveyorJoint.Refresh(this, clicker, ref jointKey, ref joint); }
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

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save
    {
        public Vector3 a, b;
        public bool reversed;
        public float customSpeed = -1f;
        public List<PixelConveyorSystem.RiderSave> riders = new List<PixelConveyorSystem.RiderSave>();
    }

    /// <summary>The belt's shape, direction, custom speed and the pixels riding it (where along the belt each is).</summary>
    public static string ExportState(GameObject root)
    {
        PixelConveyor belt = root != null ? root.GetComponent<PixelConveyor>() : null;
        if (belt == null) return null;
        Save s = new Save { a = belt.a, b = belt.b, reversed = belt.reversed, customSpeed = belt.customSpeedCubes };
        s.riders = PixelConveyorSystem.ExportRiders(belt);
        return JsonUtility.ToJson(s);
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        GameObject root = Place(c, s.a, s.b);
        if (root == null) return null;
        PixelConveyor belt = root.GetComponent<PixelConveyor>();
        if (s.reversed) { belt.reversed = true; belt.PlaceRibs(); }
        belt.customSpeedCubes = s.customSpeed;
        if (s.riders != null) foreach (PixelConveyorSystem.RiderSave r in s.riders) PixelConveyorSystem.ImportRider(belt, r);
        return root;
    }

    /// <summary>First person: where a carried cube would sit if put down on this belt (centred on the line, bottom on the belt). False if it is not over the belt.</summary>
    public bool TrySnapPlace(Vector3 held, float halfHeight, out Vector3 position, out float gap)
    {
        position = held; gap = float.MaxValue;
        if (ghostMode) return false;
        Vector3 l = transform.InverseTransformPoint(held);
        if (Mathf.Abs(l.x) > width * 0.5f + 0.2f * unit || Mathf.Abs(l.z) > length * 0.5f || l.y < height - 0.3f * unit || l.y > height + 3.2f * unit) return false;
        float targetY = height + halfHeight;
        position = transform.TransformPoint(new Vector3(0f, targetY, l.z));
        gap = Mathf.Abs(l.y - targetY);
        return true;
    }

    public static IReadOnlyList<PixelConveyor> All => all;

    // ---------------- selection (Dev Hub tester: several belts edited together) ----------------

    private bool selected;
    public bool Selected => selected;

    /// <summary>Shows or hides the teal selection glow round the belt.</summary>
    public void SetSelected(bool on)
    {
        selected = on;
        ApplySelection();
    }

    private void ApplySelection()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "Selection") { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        if (!selected || ghostMode) return;
        PixelFactoryKit k = new PixelFactoryKit(clicker, true);
        k.alpha = 0.4f;
        k.Box("Selection", transform, new Vector3(0f, height * 0.5f + 0.1f * unit, 0f), new Vector3(width + 0.5f * unit, height + 0.45f * unit, length + 0.3f * unit), new Color(0.2f, 0.9f, 1f, 1f));
    }
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
        public int lane;       // which lane of a wide belt it rides in
    }

    [Header("Player on a belt (first person)")]
    [Min(0f)]
    [Tooltip("How strongly a belt carries the player, as a fraction of the belt's speed (1 = the belt's own speed). 0 = the coded default (1).")]
    [SerializeField] private float playerPull = 0f;

    [Min(0f)]
    [Tooltip("The soft magnet that draws the player to the belt's middle line, in cube widths per second at most. Walking out is always stronger. 0 = the coded default (0.9).")]
    [SerializeField] private float playerMagnet = 0f;

    private static readonly List<IPixelBeltAcceptor> acceptors = new List<IPixelBeltAcceptor>();

    /// <summary>Machines register here (in OnEnable) so a belt end can feed them directly.</summary>
    public static void Register(IPixelBeltAcceptor a) { if (a != null && !acceptors.Contains(a)) acceptors.Add(a); }
    public static void Unregister(IPixelBeltAcceptor a) { acceptors.Remove(a); }

    /// <summary>Is there room to put a pixel on the start of this belt (no rider within a gap of the drop spot, no loose pixel lying there)? The Funnel waits when it is false.</summary>
    public static bool StartHasRoom(PixelConveyor belt)
    {
        if (system == null || belt == null) return true;
        float u = belt.Unit, oldSize = system.clicker != null ? system.clicker.OldPixelWorldSize : u;
        Vector3 over = belt.PointAt(0.6f * oldSize);
        int lane = belt.NearestLane(over);
        if (!system.RoomAt(belt, 0.6f * oldSize, lane)) return false;
        if (system.clicker != null)
        {
            Vector3 drop = new Vector3(over.x, over.y + belt.SurfaceHeight + oldSize * 0.7f, over.z);
            foreach (Rigidbody rb in system.clicker.OldPixels)
                if (rb != null && !rb.isKinematic && (rb.position - drop).sqrMagnitude < (0.9f * u) * (0.9f * u)) return false;
        }
        return true;
    }

    /// <summary>The first machine (not 'exclude') whose opening contains this world point; null if none.</summary>
    public static IPixelBeltAcceptor FindAcceptorAt(Vector3 world, IPixelBeltAcceptor exclude)
    {
        for (int i = acceptors.Count - 1; i >= 0; i--)
        {
            IPixelBeltAcceptor a = acceptors[i];
            if (a == null || (a is Object o && o == null)) { acceptors.RemoveAt(i); continue; }
            if (a != exclude && a.AcceptsAt(world)) return a;
        }
        return null;
    }

    /// <summary>Calls 'visit' with the opening point of every machine that has one (IPixelPort): the Conveyor Connector magnetizes to them.</summary>
    public static void ForEachPort(System.Action<Vector3> visit)
    {
        foreach (IPixelBeltAcceptor a in acceptors)
            if (a is IPixelPort port && !(a is Object o && o == null)) visit(port.PortWorld);
    }

    private IPixelBeltAcceptor FindAcceptor(PixelConveyor belt)
    {
        Vector3 end = belt.ExitPoint + belt.Direction * belt.Unit * 0.5f;
        end.y = belt.ExitPoint.y + belt.SurfaceHeight + belt.Unit * 0.5f;
        for (int i = acceptors.Count - 1; i >= 0; i--)
        {
            IPixelBeltAcceptor a = acceptors[i];
            if (a == null || (a is Object o && o == null)) { acceptors.RemoveAt(i); continue; }
            if (a.AcceptsAt(end) || a.AcceptsAt(belt.ExitPoint + Vector3.up * belt.Unit * 0.6f)) return a;
        }
        return null;
    }

    private PixelClicker clicker;
    private static PixelConveyorSystem system;
    private readonly List<Rider> riders = new List<Rider>();
    private readonly HashSet<Rigidbody> riding = new HashSet<Rigidbody>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSystem()
    {
        system = null; editorOpen = false;
        PixelStatics.ResetOnRestart(OnSceneUnloaded);
    }

    // (Restart reloads the scene: a speed window left open would keep editorOpen on and stop belt clicks in the new game)
    private static void OnSceneUnloaded() { editorOpen = false; }

    public static void Ensure(PixelClicker c) { if (system == null) { GameObject go = new GameObject("Conveyor System"); system = go.AddComponent<PixelConveyorSystem>(); system.clicker = c; } }

    [System.Serializable]
    public class RiderSave { public string type; public double amount; public float s; public int lane; }

    /// <summary>Is this old pixel locked onto a belt right now (it is saved with the belt, not with the loose old pixels)?</summary>
    public static bool IsRiding(Rigidbody rb) => system != null && rb != null && system.riding.Contains(rb);

    public static List<RiderSave> ExportRiders(PixelConveyor belt)
    {
        List<RiderSave> list = new List<RiderSave>();
        if (system == null || system.clicker == null) return list;
        foreach (Rider r in system.riders)
        {
            if (r.belt != belt || r.rb == null) continue;
            OldPixelInfo info = PixelHuskQuery.Get(r.rb).info;
            if (info == null || !system.clicker.IsValidTierIndex(info.tierIndex)) continue;
            list.Add(new RiderSave { type = system.clicker.Tiers[info.tierIndex].type.ToString(), amount = info.amount, s = r.s, lane = r.lane });
        }
        return list;
    }

    /// <summary>A saved rider goes back on the belt at its place (the pixel is made again and locked on).</summary>
    public static void ImportRider(PixelConveyor belt, RiderSave save)
    {
        if (system == null || system.clicker == null || save == null || belt == null) return;
        PixelClicker c = system.clicker;
        if (!System.Enum.TryParse(save.type, out PixelClicker.PixelType type)) return;
        int tier = c.IndexOf(type);
        if (tier < 0) return;
        float s = Mathf.Clamp(save.s, 0f, belt.Length);
        int lane = Mathf.Clamp(save.lane, 0, belt.Lanes - 1);
        Vector3 p = belt.LanePointAt(lane, s) + Vector3.up * (belt.SurfaceHeight + belt.Unit * 0.5f);
        int before = c.OldPixels.Count;
        if (!c.SpawnStoredPixel(tier, save.amount, p, Vector3.zero) || c.OldPixels.Count <= before) return;
        Rigidbody rb = c.OldPixels[c.OldPixels.Count - 1];
        if (rb == null) return;
        OldPixelPopIn pop = rb.GetComponent<OldPixelPopIn>();
        if (pop != null) pop.Finish();
        system.Capture(rb, belt, s, lane);
    }

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
        OldPixelInfo info = PixelHuskQuery.Get(rb).info;
        if (info != null && info.heldByPlayer) return false;
        OldPixelDespawn despawn = PixelHuskQuery.Get(rb).despawn;
        if (despawn != null && (despawn.IsDespawning || despawn.Held)) return false;
        return true;
    }

    /// <summary>Is there room for another pixel at distance s on this belt (no rider closer than a gap)?</summary>
    private bool RoomAt(PixelConveyor belt, float s, int lane, Rider ignore = null)
    {
        float gap = belt.Unit * 1.1f;
        foreach (Rider r in riders) if (r != ignore && r.belt == belt && r.lane == lane && Mathf.Abs(r.s - s) < gap) return false;
        return true;
    }

    private void Capture(Rigidbody rb, PixelConveyor belt, float s, int lane)
    {
        SetVelocity(rb, Vector3.zero);
        rb.isKinematic = true;
        float half = PixelAttachment.HalfExtents(rb, belt.Unit).y;
        riders.Add(new Rider { rb = rb, belt = belt, s = Mathf.Clamp(s, 0f, belt.Length), half = half, lane = Mathf.Clamp(lane, 0, belt.Lanes - 1) });
        riding.Add(rb);
    }

    private void Release(Rider r, bool eject)
    {
        riding.Remove(r.rb);
        if (r.rb == null) return;
        r.rb.isKinematic = false;
        if (eject && r.belt != null) SetVelocity(r.rb, r.belt.Direction * r.belt.Speed * 1.15f + Vector3.up * (r.belt.Unit * 0.5f + r.belt.SlopeUp * r.belt.Speed));
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
                    int lane = belt.NearestLane(c);
                    if (RoomAt(belt, s, lane)) Capture(rb, belt, s, lane);
                    break;
                }
            }
        }

        // 2. Drop riders that went away (despawned, grabbed by the player, belt removed).
        for (int i = riders.Count - 1; i >= 0; i--)
        {
            Rider r = riders[i];
            bool gone = r.rb == null;
            OldPixelInfo info = gone ? null : PixelHuskQuery.Get(r.rb).info;
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
            foreach (Rider o in riders) if (o != r && o.belt == belt && o.lane == r.lane && o.s > r.s) limit = Mathf.Min(limit, o.s - gap);
            float want = r.s + belt.RunSpeed * dt;
            if (PixelBeltSorter.TryDivert(clicker, belt, r.s, r.rb)) { riding.Remove(r.rb); riders.RemoveAt(i); continue; }   // a Belt Sorter kicked it off the side
            bool atEnd = want >= belt.Length - 0.001f && limit >= belt.Length - 0.001f;
            r.s = Mathf.Clamp(Mathf.Min(want, limit), 0f, belt.Length);
            if (atEnd)
            {
                PixelConveyor next = belt.FindNext();
                if (next == null)
                {
                    // An end. An In coupling at the end (PixelCouplings) hands the pixel to its device; (old way, being moved over:) a machine opening takes it; otherwise it is ejected.
                    Rigidbody taken = r.rb;
                    if (PixelCouplings.HandIn(belt.ExitPoint, taken))
                    {
                        riding.Remove(taken);
                        riders.RemoveAt(i);
                        continue;
                    }
                    IPixelBeltAcceptor acceptor = FindAcceptor(belt);
                    if (acceptor != null && acceptor.AcceptFromBelt(taken))
                    {
                        riding.Remove(taken);
                        riders.RemoveAt(i);
                        continue;
                    }
                    riders.RemoveAt(i);
                    Release(r, true);      // an end: the pixel leaves the belt
                    continue;
                }
                int nextLane = next.NearestLane(belt.LanePointAt(r.lane, belt.Length));   // (the lane of the next belt nearest to where it leaves this one)
                if (RoomAt(next, 0f, nextLane, r)) { r.belt = next; r.s = 0f; r.lane = nextLane; belt = next; }
            }
            // Position: on the line, bottom on the belt surface; glides to it (a pixel that landed from above settles onto the belt).
            Vector3 p = belt.LanePointAt(r.lane, r.s);
            p.y += (belt.SurfaceHeight + r.half) * belt.LiftFactor;
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
        PixelFirstPerson.PushPlayer(dir * best.RunSpeed * (playerPull > 0f ? playerPull : 1f) + side * pull);
    }

    private void Update()
    {
        if (editorOpen && PixelKeys.KeyPressed(KeyCode.Escape)) { CloseEditor(); return; }   // Esc backs out (the number box swallows the key for itself, so it is checked here too)
        if (clicker != null && PixelConveyor.All.Count > 0) CarryPlayer();
        laneCheckTimer -= Time.unscaledDeltaTime;
        if (laneCheckTimer <= 0f)
        {
            laneCheckTimer = 1f;
            int wanted = PixelConveyor.LaneCount;
            if (wanted != lanesShown) { lanesShown = wanted; foreach (PixelConveyor each in PixelConveyor.All) if (each != null) each.RefreshLanes(); }   // the Electrician's width upgrade
        }
        if (clicker == null || PixelPauseMenu.IsPaused || editorOpen) return;
        bool fp = PixelFirstPerson.Active;
        if (PixelClicker.ExternalClickBlock && !fp) { flipTap.Update(null); return; }
        if (fp)
        {
            if (!PixelInput.RightPressed()) return;
            if (!PixelFirstPerson.TryGetViewRay(out Ray fpRay)) return;
            PixelConveyor fpBelt = BeltUnder(fpRay, clicker.OldPixelWorldSize * 16f);
            if (fpBelt != null) OpenEditor(fpBelt);    // first person: type a custom speed
            return;
        }
        // Normal view: a quick right CLICK turns the belt round (holding the button deletes a tester object instead, see PixelFeatureTester).
        if (!(PixelInput.RightPressed() || PixelInput.RightHeld() || flipTap.Pending)) return;
        Camera cam = clicker.ViewCamera;
        PixelConveyor belt = cam != null && !PixelInput.PointerOverUI() ? BeltUnder(cam.ScreenPointToRay(PixelInput.PointerPosition()), 1000f) : null;
        if (flipTap.Update(belt) && belt != null) belt.Flip();
    }

    private float laneCheckTimer;
    private int lanesShown = 1;
    private readonly PixelRightTap flipTap = new PixelRightTap();

    /// <summary>The belt under a ray (the cube or an old pixel in front of it wins: then null).</summary>
    private PixelConveyor BeltUnder(Ray ray, float reach)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return null;   // the cube's own right click comes first
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return null;
            PixelConveyor belt = h.collider.GetComponentInParent<PixelConveyor>();
            if (belt != null) return belt;
        }
        return null;
    }

    /// <summary>A pixel being grabbed by the player leaves its belt (or other lock) and gets its physics back.</summary>
    public static void FreeRider(Rigidbody rb)
    {
        if (rb == null || !rb.isKinematic) return;
        if (system != null)
        {
            system.riding.Remove(rb);
            system.riders.RemoveAll(r => r.rb == rb);
        }
        rb.isKinematic = false;
    }

    // ---- the speed editor (first person right click on a belt) ----

    private static bool editorOpen;
    /// <summary>True while the belt speed window is open (first person gives the mouse back).</summary>
    public static bool EditorOpen => editorOpen;

    private GameObject editorRoot;
    private TMP_InputField speedInput;
    private TMP_Text speedTitle;
    private PixelConveyor editing;
    private bool windowRegistered;

    private void OpenEditor(PixelConveyor belt)
    {
        if (editorRoot == null) BuildEditor();
        if (editorRoot == null) return;
        editing = belt;
        speedInput.text = belt.SpeedCubes.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        editorRoot.SetActive(true);
        editorOpen = true;
        if (!windowRegistered) { PixelWindows.Register(this, 215, () => editorOpen, CloseEditor); windowRegistered = true; }
        speedInput.Select();
        speedInput.ActivateInputField();
    }

    private void CloseEditor()
    {
        editorOpen = false;
        editing = null;
        if (editorRoot != null) editorRoot.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void ApplySpeed()
    {
        if (editing != null && float.TryParse(speedInput.text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
        {
            editing.SetSpeedCubes(v);
            PixelAudio.Play("device_place");
            CloseEditor();
        }
    }

    private void BuildEditor()
    {
        TMP_FontAsset font = clicker.UIFont;
        if (font == null) return;
        PixelUIKit.EnsureEventSystem();
        editorRoot = PixelUIKit.CreateCanvas("Belt Speed", 330, new Vector2(1920f, 1080f), true);   // (clickable: the buttons need a GraphicRaycaster)
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(editorRoot.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(520f, 300f);
        Image bg = panel.GetComponent<Image>();
        PixelUIKit.StyleWindow(bg, new Color(0.1f, 0.12f, 0.18f, 0.96f));

        speedTitle = PixelUIKit.CreateText(font, panel.transform, "Title", "BELT SPEED", 32f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform tr = speedTitle.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(480f, 50f); tr.anchoredPosition = new Vector2(0f, -16f);

        TMP_Text hint = PixelUIKit.CreateText(font, panel.transform, "Hint", "cube widths per second (0 stops the belt)", 20f, TextAlignmentOptions.Center, FontStyles.Italic, new Color(0.75f, 0.82f, 0.95f, 1f));
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(480f, 30f); hr.anchoredPosition = new Vector2(0f, -70f);

        speedInput = PixelUIKit.CreateInputField(font, panel.transform, "Speed Input", new Vector2(300f, 56f), new Color(0.05f, 0.06f, 0.09f, 1f), Color.white, 30f, "1.6");
        RectTransform ir = speedInput.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 1f);
        ir.anchoredPosition = new Vector2(0f, -120f);
        speedInput.contentType = TMP_InputField.ContentType.DecimalNumber;
        speedInput.onSubmit.AddListener(_ => ApplySpeed());
        // The speed changes AS YOU TYPE (a valid number applies at once): no Enter needed.
        speedInput.onValueChanged.AddListener(text =>
        {
            if (editing != null && float.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) editing.SetSpeedCubes(v);
        });

        Button Make(string name, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction act)
        {
            Button b = PixelUIKit.CreateButton(font, panel.transform, name, label, new Vector2(150f, 52f), color, Color.white, 24f);
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
            r.anchoredPosition = pos;
            b.onClick.AddListener(act);
            return b;
        }
        Make("Set", "Set", new Vector2(-170f, 24f), new Color(0.2f, 0.6f, 0.3f, 1f), ApplySpeed);
        Make("Reverse", "Reverse", new Vector2(0f, 24f), new Color(0.25f, 0.45f, 0.7f, 1f), () => { if (editing != null) editing.FlipPublic(); });
        Make("Close", "Close", new Vector2(170f, 24f), new Color(0.55f, 0.22f, 0.2f, 1f), CloseEditor);
        editorRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (Rider r in riders) if (r.rb != null) { r.rb.isKinematic = false; }
        riders.Clear();
        riding.Clear();
        if (system == this) system = null;
        editorOpen = false;
        if (windowRegistered) PixelWindows.Unregister(this);
        if (editorRoot != null) Destroy(editorRoot);
    }
}
