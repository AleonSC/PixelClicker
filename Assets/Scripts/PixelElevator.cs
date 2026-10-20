// ---- System notes ----
// PixelElevator.cs
//   CONVEYOR ELEVATOR (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Elevator'): a square tower that lifts pixels from the END of a belt up to a higher belt. PLACING (two stages):
//   1. the ghost follows the mouse and MAGNETIZES to a belt end that is near it (PixelElevator.TrySnap: it turns to the belt's direction, the belt's end meets its entry face, and its base
//   takes that belt's height); Q / E turn it when it is not latched; click to fix the base. 2. Move the mouse up or down to set the HEIGHT (the mouse ray is cut with a vertical plane through
//   the base, quantised to heightStepCubes) and click to confirm; right click goes back to stage 1. The tower's posts run down to the floor when the base is raised.
//   SOCKETS (PixelSockets.cs; since 2026-10-20 the ONLY way husks go in and out - no more near-the-base catching): eight FITTED bare couplings (built in, not
//   saved, made again on every rebuild), one just off the middle of each side at the BASE ('in0'..'in3', Intake) and at the TOP ('out0'..'out3', Output). Their
//   ports are the belt snap points (SnapPoints), so a belt end laid against a side hands its husks in (AcceptHusk -> drawn to the shaft and carried up by a
//   PixelPathCarrier; refused while the shaft is full, so the belt waits), and so does a machine's Out coupling right beside one (Emit -> HandIn). At the top
//   the tower uses the first 'out' socket with something attached (a belt starting at it, or an In coupling right there: RefreshLinks, every half second):
//   PixelCouplings.Emit puts the husk out there; while OutputHasRoom says the belt start is full the husk WAITS at the top and the shaft queues up. With
//   nothing attached at the top it is flung forward off the top. A short trough is drawn to each link. Belts can also be raised and lowered while placing
//   (scroll wheel, PixelFeatureTester) so ramps and bridges are possible. Saved with the tester save (position, turn, height). Built with PixelFactoryKit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelElevator : MonoBehaviour, IPixelSocketDevice
{
    [Min(0f)]
    [Tooltip("How fast pixels travel up the shaft, in cube widths per second. 0 = the coded default (3).")]
    [SerializeField] private float liftSpeedCubes = 0f;

    [Min(0f)]
    [Tooltip("The space kept between two pixels in the shaft, in cube widths. 0 = the coded default (1.1).")]
    [SerializeField] private float pixelGapCubes = 0f;

    [Min(0f)]
    [Tooltip("The height changes in steps of this many cube widths while placing. 0 = the coded default (0.5).")]
    [SerializeField] private float heightStepCubes = 0f;

    [Min(0.5f)]
    [Tooltip("How far from the tower (cube widths, past its sides) a belt end, a machine's Out coupling or a Belt Sorter's chute is linked automatically - at the base it feeds the tower, at the top a belt start or an In coupling takes what comes out.")]
    [SerializeField] private float linkReachCubes = 2.5f;

    private float LiftSpeed => liftSpeedCubes > 0f ? liftSpeedCubes : 3f;
    private float Gap => pixelGapCubes > 0f ? pixelGapCubes : 1.1f;

    private const float HalfCubes = 0.85f;     // half the tower's width, in cube widths
    private const float PlateCubes = 0.16f;    // the thickness of the platforms (the same as a belt's slab)
    public const float MinHeightCubes = 1.5f, MaxHeightCubes = 40f, DefaultHeightCubes = 4f;

    private static readonly List<PixelElevator> all = new List<PixelElevator>();
    public static IReadOnlyList<PixelElevator> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f, height, builtY = float.MaxValue;
    private readonly List<Transform> pads = new List<Transform>();
    private float padOffset;
    private readonly PixelPathCarrier carrier = new PixelPathCarrier();
    private Vector3[] path;
    private float[] cum;

    public bool IsGhost => ghost;
    public float Height => height;

    // ---- Sockets (PixelSockets.cs): an In at the middle of each side of the base, an Out at each side of the top, each with a bare coupling built in ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.55f, 0.58f, 0.64f, 1f);

    // ---------------- building ----------------

    /// <summary>The model alone, for the shop / inventory pictures (no working parts: PixelConsumables.DisplayOnly strips them).</summary>
    public static GameObject CreateDisplay(PixelClicker c) => PixelConsumables.DisplayOnly(Build(c, false, DefaultHeightCubes, "Conveyor Elevator (Display)"));
    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, DefaultHeightCubes, "Conveyor Elevator (Ghost)"); }

    public static GameObject PlaceTall(PixelClicker c, Vector3 point, float yaw, float heightWorld)
    {
        GameObject root = Build(c, false, DefaultHeightCubes, "Conveyor Elevator");
        PixelElevator e = root.GetComponent<PixelElevator>();
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        e.Rebuild(Mathf.Clamp(heightWorld, MinHeightCubes * e.unit, MaxHeightCubes * e.unit));
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw) { return PlaceTall(c, point, yaw, DefaultHeightCubes * Mathf.Max(0.05f, c.OldPixelWorldSize)); }

    /// <summary>The cube-width size of one height step (so the tester can quantise the mouse height).</summary>
    public static float StepWorld(GameObject ghostRoot) { PixelElevator e = ghostRoot != null ? ghostRoot.GetComponent<PixelElevator>() : null; return e == null ? 0.5f : e.unit * (e.heightStepCubes > 0f ? e.heightStepCubes : 0.5f); }
    public static float MinWorld(GameObject root) { PixelElevator e = root != null ? root.GetComponent<PixelElevator>() : null; return e == null ? 1f : e.unit * MinHeightCubes; }
    public static float MaxWorld(GameObject root) { PixelElevator e = root != null ? root.GetComponent<PixelElevator>() : null; return e == null ? 40f : e.unit * MaxHeightCubes; }
    public static void SetHeight(GameObject root, float heightWorld) { PixelElevator e = root != null ? root.GetComponent<PixelElevator>() : null; if (e != null && Mathf.Abs(e.height - heightWorld) > 0.001f) e.Rebuild(heightWorld); }

    private static GameObject Build(PixelClicker c, bool ghostMode, float heightCubes, string name)
    {
        GameObject root = new GameObject(name);
        PixelElevator e = root.AddComponent<PixelElevator>();
        e.clicker = c;
        e.ghost = ghostMode;
        e.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        e.Rebuild(heightCubes * e.unit);
        return root;
    }

    private void Rebuild(float h)
    {
        PixelCoupling.RemoveAll(this);   // (its built-in couplings are children: made again below, the top one at the new height)
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        pads.Clear();
        path = null;
        height = h;
        builtY = transform.position.y;
        float u = unit, half = HalfCubes * u, plate = PlateCubes * u;
        float floorLocal = Mathf.Min(0f, PixelConveyor.FloorY(clicker) - transform.position.y);   // the posts reach the floor when the base is raised
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color steel = new Color(0.55f, 0.58f, 0.64f, 1f), dark = new Color(0.22f, 0.24f, 0.28f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f);

        k.Box("Base Plate", transform, new Vector3(0f, plate * 0.5f, 0f), new Vector3(half * 2f, plate, half * 2f), dark, default, false, 0.4f, 0.4f);
        k.Box("Top Plate", transform, new Vector3(0f, h + plate * 0.5f, 0f), new Vector3(half * 2f, plate, half * 2f), dark, default, false, 0.4f, 0.4f);

        float topY = h + 1.0f * u, postW = 0.14f * u, o = half - postW * 0.5f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Box("Post", transform, new Vector3(sx * o, (floorLocal + topY) * 0.5f, sz * o), new Vector3(postW, topY - floorLocal, postW), steel, default, false, 0.7f, 0.5f);
        for (float y = 2.5f * u; y < topY - 0.3f * u; y += 2.5f * u)   // cross frames
        {
            float bar = 0.06f * u;
            k.Box("Frame", transform, new Vector3(0f, y, o), new Vector3(half * 2f, bar, bar), steel);
            k.Box("Frame", transform, new Vector3(0f, y, -o), new Vector3(half * 2f, bar, bar), steel);
            k.Box("Frame", transform, new Vector3(o, y, 0f), new Vector3(bar, bar, half * 2f), steel);
            k.Box("Frame", transform, new Vector3(-o, y, 0f), new Vector3(bar, bar, half * 2f), steel);
        }
        k.Box("Header", transform, new Vector3(0f, topY, 0f), new Vector3(half * 2f, 0.08f * u, half * 2f), steel, default, false, 0.7f, 0.5f);

        // Lift pads that travel up the shaft (just for looks).
        int count = Mathf.Max(2, Mathf.CeilToInt(h / (2f * u)) + 1);
        for (int i = 0; i < count; i++) pads.Add(k.Box("Pad", transform, Vector3.zero, new Vector3(0.9f * u, 0.05f * u, 0.9f * u), accent, default, false, 0.3f, 0.4f).transform);
        PlacePads();
        if (ghost) return;
        BuildSockets();
        linkKey = null;
        nextLinkCheck = 0f;
    }

    /// <summary>The eight built-in couplings: Intake just off each side of the base, Output just off each side of the top (the belt snap points).</summary>
    private void BuildSockets()
    {
        float off = SideOffCubes * unit;
        sockets = new PixelSocket[8];
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;   // (the device's own space)
            sockets[i] = new PixelSocket("in" + i, dir * off, dir, PixelSocketFlow.Intake, true) { bare = true };
            sockets[4 + i] = new PixelSocket("out" + i, dir * off + Vector3.up * height, dir, PixelSocketFlow.Output, true) { bare = true };
        }
        PixelCoupling.FitBuiltIn(this);
    }

    private const float SideOffCubes = HalfCubes + 0.9f;   // how far from the tower's centre a side's socket port (and belt snap point) is, in cube widths

    private void PlacePads()
    {
        float u = unit, spacing = 2f * u, total = pads.Count * spacing;
        for (int i = 0; i < pads.Count; i++)
        {
            float y = Mathf.Repeat(padOffset + i * spacing, total);
            bool visible = y >= PlateCubes * u && y <= height;
            pads[i].gameObject.SetActive(visible);
            pads[i].localPosition = new Vector3(0f, y, 0f);
        }
    }

    // ---------------- placing ----------------

    /// <summary>While placing: latches onto a belt end near the mouse (turns to the belt's direction, the belt meets the entry face, the base takes the belt's height).</summary>
    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        PixelConveyor best = PixelConveyor.NearestExit(c, point, 3.5f * u);
        if (best == null) return false;
        Vector3 dir = best.Direction;
        snappedYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        Vector3 exit = best.ExitPoint;
        snappedPoint = new Vector3(exit.x + dir.x * HalfCubes * u, exit.y, exit.z + dir.z * HalfCubes * u);
        return true;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw, height; }

    public static string ExportState(GameObject root)
    {
        PixelElevator e = root != null ? root.GetComponent<PixelElevator>() : null;
        if (e == null) return null;
        return JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, height = e.height });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : PlaceTall(c, s.position, s.yaw, s.height);
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelCouplings.Register(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); all.Remove(this); }
    private void OnDestroy() { carrier.ReleaseAll(); }

    /// <summary>A husk comes in through one of the base couplings: drawn to the shaft and lifted (refused when the shaft is full - the belt waits).</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody pixel)
    {
        if (ghost || pixel == null || socket == null || !socket.CanTakeIn || carrier.Count > 14) return false;
        carrier.Add(pixel);
        return true;
    }

    private void Update()
    {
        if (ghost)
        {
            if (Mathf.Abs(transform.position.y - builtY) > 0.01f) Rebuild(height);   // (the posts follow the ghost's height above the floor)
            return;
        }
        padOffset += Time.deltaTime * LiftSpeed * unit;
        PlacePads();
        if (Time.time >= nextLinkCheck) { nextLinkCheck = Time.time + 0.5f + Random.value * 0.1f; RefreshLinks(); }
    }

    // ---------------- links (what is attached to its couplings) ----------------
    // Every half second the tower looks at its own couplings: which base sockets have a belt ending at them or a machine's Out coupling right beside them
    // (drawn as troughs), and which top socket has something attached (a belt starting at it, or an In coupling right there) - the first one found is
    // the way out (outSocket). A short TROUGH (floor + rails) is built from each linked thing to the tower, so the join looks made for it.

    private string linkKey;
    private float nextLinkCheck;
    private GameObject linkRoot;
    private PixelSocket outSocket;
    private Vector3 outDir = Vector3.forward;
    private readonly List<Vector3> inPoints = new List<Vector3>();

    private Vector3 BaseCentre => transform.position;
    private Vector3 TopCentre => transform.position + Vector3.up * height;
    private float Reach => (HalfCubes + linkReachCubes) * unit;

    /// <summary>Does any placed elevator's base take husks handed in at this point (a Belt Sorter's chute forms towards it)?</summary>
    public static bool AnyBaseNear(Vector3 p) { foreach (PixelElevator e in all) if (e != null && !e.ghost && e.NearBase(p)) return true; return false; }

    private bool NearBase(Vector3 p) => Flat(p - BaseCentre) <= Reach && Mathf.Abs(p.y - BaseCentre.y) <= 1.5f * unit;
    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    /// <summary>Where belts snap round the tower (placing): just off the middle of each side, at the base's height and at the top's (= its sockets' ports).</summary>
    public void SnapPoints(System.Action<Vector3> visit)
    {
        float off = SideOffCubes * unit;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * transform.forward;
            visit(BaseCentre + dir * off);
            visit(TopCentre + dir * off);
        }
    }

    private void RefreshLinks()
    {
        inPoints.Clear();
        outSocket = null;
        Vector3 target = Vector3.zero;
        float reach = PixelCouplings.LinkReach * unit;
        foreach (PixelSocket sk in sockets)
        {
            if (sk == null) continue;
            Vector3 port = PixelCouplings.Port(this, sk), facing = PixelCouplings.Facing(this, sk);
            if (sk.CanTakeIn)
            {
                PixelConveyor belt = PixelCouplings.BeltEndingAt(port);
                if (belt != null && !belt.IsGhost) { inPoints.Add(belt.ExitPoint + Vector3.up * belt.SurfaceHeight); continue; }
                foreach (PixelCoupling c in PixelCoupling.All)   // a machine's Out coupling right beside this side (device to device)
                {
                    if (c == null || !c.IsOut || ReferenceEquals(c.Device, this) || c.Device == null || (c.Device is Object o && o == null)) continue;
                    Vector3 mouth = c.PortWorld + c.FacingWorld * 0.5f * unit;
                    if (Flat(mouth - port) <= reach && Mathf.Abs(mouth.y - port.y) <= 1.2f * unit) { inPoints.Add(mouth + Vector3.up * 0.1f * unit); break; }
                }
            }
            else if (outSocket == null)
            {
                PixelConveyor belt = PixelCouplings.BeltStartingAt(port);
                if (belt != null && !belt.IsGhost) { outSocket = sk; outDir = facing; target = belt.StartPoint + Vector3.up * belt.SurfaceHeight; continue; }
                PixelCoupling into = PixelCouplings.IntakeAt(port + facing * 0.5f * unit, reach, this);
                if (into != null) { outSocket = sk; outDir = facing; target = into.PortWorld + Vector3.up * 0.1f * unit; }
            }
        }
        bool hasOut = outSocket != null;
        Vector3 newDir = hasOut ? outDir : transform.forward;
        if ((newDir - lastPathDir).sqrMagnitude > 1e-4f) { lastPathDir = newDir; path = null; }   // (the shaft's last leg points at the way out)
        outDir = newDir;

        System.Text.StringBuilder key = new System.Text.StringBuilder();
        foreach (Vector3 p in inPoints) key.Append(p.ToString("F2"));
        key.Append('|').Append(hasOut ? target.ToString("F2") : "-").Append(height.ToString("F2")).Append(transform.position.ToString("F2"));
        string k = key.ToString();
        if (k == linkKey && linkRoot != null) return;
        linkKey = k;
        if (linkRoot != null) Destroy(linkRoot);
        linkRoot = new GameObject("Links");
        linkRoot.transform.SetParent(transform, false);
        float half = HalfCubes * unit, plateTop = PlateCubes * unit;
        foreach (Vector3 p in inPoints)
        {
            Vector3 toP = p - BaseCentre; toP.y = 0f;
            Vector3 e = BaseCentre + (toP.sqrMagnitude > 1e-4f ? toP.normalized : -transform.forward) * half + Vector3.up * plateTop;
            Trough(p, e);
        }
        if (hasOut) Trough(TopCentre + outDir * half + Vector3.up * plateTop, target);
    }

    private Vector3 lastPathDir = Vector3.forward;

    /// <summary>A short trough (a dark floor with steel rails) between two world points, so a link looks built for it.</summary>
    private void Trough(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        if (Flat(d) < 0.05f * unit) return;
        GameObject t = new GameObject("Link Trough");
        t.transform.SetParent(linkRoot.transform, false);
        t.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up));
        float len = d.magnitude + 0.2f * unit, w = 1.1f * unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, false, true);   // (shared materials: troughs are rebuilt whenever the links change)
        Color rubber = new Color(0.16f, 0.17f, 0.2f, 1f), steel = new Color(0.55f, 0.58f, 0.64f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f);
        k.Box("Trough Floor", t.transform, new Vector3(0f, -0.05f * unit, 0f), new Vector3(w, 0.1f * unit, len), rubber, default, false, 0.1f, 0.3f);
        for (int s = -1; s <= 1; s += 2)
        {
            k.Box("Trough Rail", t.transform, new Vector3(s * (w * 0.5f + 0.04f * unit), 0.1f * unit, 0f), new Vector3(0.08f * unit, 0.3f * unit, len), steel, default, false, 0.7f, 0.5f);
            k.Box("Trough Trim", t.transform, new Vector3(s * (w * 0.5f + 0.085f * unit), 0.05f * unit, 0f), new Vector3(0.01f * unit, 0.04f * unit, len * 0.9f), accent, default, false, 0.2f, 0.5f, accent * 0.25f);
        }
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null || carrier.Count == 0) return;
        if (path == null)
        {
            float oldSize = clicker.OldPixelWorldSize;
            float ey = (PlateCubes * unit) + oldSize * 0.5f;
            Vector3 top = transform.TransformPoint(new Vector3(0f, ey + height, 0f));
            path = new[]
            {
                transform.TransformPoint(new Vector3(0f, ey, 0f)),
                top,
                top + outDir * (HalfCubes * unit + 0.1f * unit),   // (the last leg points at the linked way out)
            };
            cum = PixelPathCarrier.Cumulative(path);
        }
        carrier.Step(path, cum, LiftSpeed * unit, Gap * unit, Time.fixedDeltaTime, PutOut, 0.25f, TopHasRoom);
    }

    /// <summary>The way out at the top is free: nothing attached, or the belt there has room (else the husk waits at the top and the shaft queues up).</summary>
    private bool TopHasRoom() => outSocket == null || PixelCouplings.OutputHasRoom(this, outSocket.id);

    /// <summary>A pixel reaches the top: out through the linked top coupling (onto its belt / into the In coupling there), else flung off the top.</summary>
    private void PutOut(Rigidbody rb)
    {
        if (outSocket != null && PixelCouplings.Emit(this, outSocket.id, rb)) return;
        rb.isKinematic = false;
        rb.position = path[path.Length - 1];
        PixelPathCarrier.SetVelocity(rb, outDir * unit * 2f);
    }
}
