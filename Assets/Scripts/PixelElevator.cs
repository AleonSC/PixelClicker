// ---- System notes ----
// PixelElevator.cs
//   CONVEYOR ELEVATOR (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Elevator'): a square tower that lifts pixels from the END of a belt up to a higher belt. PLACING (two stages):
//   1. the ghost follows the mouse and MAGNETIZES to a belt end that is near it (PixelElevator.TrySnap: it turns to the belt's direction, the belt's end meets its entry face, and its base
//   takes that belt's height); Q / E turn it when it is not latched; click to fix the base. 2. Move the mouse up or down to set the HEIGHT (the mouse ray is cut with a vertical plane through
//   the base, quantised to heightStepCubes) and click to confirm; right click goes back to stage 1. The tower's posts run down to the floor when the base is raised. SOCKETS (PixelSockets.cs, couplings built in):
//   'in' at the entry, 'out' at the top. A belt end at the entry coupling hands its pixels straight in (AcceptHusk): they are drawn to the shaft, carried up by a PixelPathCarrier and put out of the
//   top coupling (PixelCouplings.Emit) onto a belt that STARTS there (belts snap to coupling ports, so a new belt snaps to the top of the tower, at its height) or flung forward. Belts can also be raised and lowered while placing
//   (scroll wheel, PixelFeatureTester) so ramps and bridges are possible. Saved with the tester save (position, turn, height). Built with PixelFactoryKit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelElevator : MonoBehaviour, IPixelSocketDevice, IPixelAutoIntake
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

    // ---- Sockets (PixelSockets.cs): the entry at the bottom (In) and the exit at the top (Out), each with a coupling built in ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.55f, 0.58f, 0.64f, 1f);

    // ---------------- building ----------------

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
        sockets = new PixelSocket[0];   // (no fixed holes since 2026-10-19: it links itself to whatever is near - RefreshLinks)
        linkKey = null;
        nextLinkCheck = 0f;
    }

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

    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelCouplings.Register(this); PixelCouplings.RegisterAutoIntake(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); PixelCouplings.UnregisterAutoIntake(this); all.Remove(this); }
    private void OnDestroy() { carrier.ReleaseAll(); }

    /// <summary>A husk comes in through the entry coupling: drawn to the shaft and lifted (refused when the shaft is full).</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody pixel)
    {
        if (ghost || pixel == null || carrier.Count > 14) return false;
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

    // ---------------- auto links (2026-10-19) ----------------
    // No fixed entry / exit holes: every half second the tower looks round its BASE for belt ends (with no belt or In coupling after them), machines' Out
    // couplings and Belt Sorter chutes (bare Out couplings) within linkReachCubes, and round its TOP for the nearest belt START or In coupling. Husks handed
    // in near the base are caught (IPixelAutoIntake.CatchNear, via PixelCouplings.HandIn) and lifted; at the top they go onto that belt / into that coupling.
    // A short TROUGH (floor + rails) is built from each linked thing to the tower, so the join looks made for it.

    private string linkKey;
    private float nextLinkCheck;
    private GameObject linkRoot;
    private PixelConveyor outBelt;
    private PixelCoupling outCoupling;
    private Vector3 outDir = Vector3.forward;
    private readonly List<Vector3> inPoints = new List<Vector3>();

    private Vector3 BaseCentre => transform.position;
    private Vector3 TopCentre => transform.position + Vector3.up * height;
    private float Reach => (HalfCubes + linkReachCubes) * unit;

    private bool NearBase(Vector3 p) => Flat(p - BaseCentre) <= Reach && Mathf.Abs(p.y - BaseCentre.y) <= 1.5f * unit;
    private bool NearTop(Vector3 p) => Flat(p - TopCentre) <= Reach && Mathf.Abs(p.y - TopCentre.y) <= 1.5f * unit;
    private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    /// <summary>Where belts snap round the tower (placing): just off the middle of each side, at the base's height and at the top's.</summary>
    public void SnapPoints(System.Action<Vector3> visit)
    {
        float off = (HalfCubes + 0.9f) * unit;
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 90f, 0f) * transform.forward;
            visit(BaseCentre + dir * off);
            visit(TopCentre + dir * off);
        }
    }

    /// <summary>A husk handed in near the base (a belt end, an Out coupling, a sorter chute beside it): drawn to the shaft and lifted.</summary>
    public bool CatchNear(Vector3 at, Rigidbody husk)
    {
        if (ghost || husk == null || carrier.Count > 14 || !NearBase(at)) return false;
        carrier.Add(husk);
        return true;
    }

    private void RefreshLinks()
    {
        inPoints.Clear();
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost || !NearBase(belt.ExitPoint) || belt.FindNext() != null) continue;
            if (PixelCouplings.IntakeAt(belt.ExitPoint, unit) != null) continue;   // (it feeds a machine's coupling instead)
            inPoints.Add(belt.ExitPoint + Vector3.up * belt.SurfaceHeight);
        }
        foreach (PixelCoupling c in PixelCoupling.All)
        {
            if (c == null || !c.IsOut || ReferenceEquals(c.Device, this) || c.Device == null || (c.Device is Object o && o == null)) continue;
            Vector3 mouth = c.PortWorld + c.FacingWorld * 0.5f * unit;
            if (!NearBase(mouth) || PixelCouplings.BeltStartingAt(c.PortWorld) != null) continue;
            inPoints.Add(mouth + Vector3.up * 0.1f * unit);
        }
        // The way out: the nearest belt that starts near the top, else the nearest In coupling there.
        outBelt = null; outCoupling = null;
        float best = float.MaxValue;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost || !NearTop(belt.StartPoint)) continue;
            float d = Flat(belt.StartPoint - TopCentre);
            if (d < best) { best = d; outBelt = belt; }
        }
        if (outBelt == null) outCoupling = PixelCouplings.IntakeAt(TopCentre, Reach, this);
        Vector3 target = outBelt != null ? outBelt.StartPoint + Vector3.up * outBelt.SurfaceHeight : outCoupling != null ? outCoupling.PortWorld + Vector3.up * 0.1f * unit : Vector3.zero;
        bool hasOut = outBelt != null || outCoupling != null;
        Vector3 dir = hasOut ? target - TopCentre : transform.forward;
        dir.y = 0f;
        Vector3 newDir = dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward;
        if ((newDir - outDir).sqrMagnitude > 1e-4f) { outDir = newDir; path = null; }   // (the shaft's last leg points at the way out)

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

    /// <summary>A short trough (a dark floor with steel rails) between two world points, so a link looks built for it.</summary>
    private void Trough(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        if (Flat(d) < 0.05f * unit) return;
        GameObject t = new GameObject("Link Trough");
        t.transform.SetParent(linkRoot.transform, false);
        t.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up));
        float len = d.magnitude + 0.2f * unit, w = 1.1f * unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, false);
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
        carrier.Step(path, cum, LiftSpeed * unit, Gap * unit, Time.fixedDeltaTime, PutOut);
    }

    /// <summary>A pixel reaches the top: onto the linked belt, into the linked In coupling, or flung off the top towards the way out.</summary>
    private void PutOut(Rigidbody rb)
    {
        if (outBelt != null && !outBelt.IsGhost) { PixelCouplings.PutOnBelt(outBelt, rb); return; }
        if (outCoupling != null && outCoupling.IsIn) { rb.isKinematic = true; if (outCoupling.TakeIn(rb)) return; rb.isKinematic = false; }
        rb.position = path[path.Length - 1];
        PixelPathCarrier.SetVelocity(rb, outDir * unit * 2f);
    }
}
