// ---- System notes ----
// PixelBeltJump.cs
//   BELT JUMP (2026-10-19, a Dev Hub > Tester feature 'Belt Jump'): a PAIR of small ramps on a conveyor line -
//   a LAUNCH ramp that clips onto the END of a belt and a LANDING ramp straight across a gap. A husk that reaches the belt's end is caught by the launch ramp
//   (IPixelAutoIntake.CatchNear, via PixelCouplings.HandIn), sped up and thrown in a fixed ARC (a sampled parabola carried by PixelPathCarrier: the same arc
//   every time) onto the landing ramp, slides down it and is put onto the belt that STARTS at the landing ramp's end (belts snap there: SnapPoints), or set
//   down on the floor if there is none.
//   PLACING (two stages, PixelFeatureTester 'scrollStage'): 1. the ghost SNAPS to a belt end near the mouse (TrySnap, turned the belt's way) - it can only be
//   placed snapped; click to fix it. 2. the SCROLL WHEEL widens / narrows the gap (gapStepCubes steps, minGapCubes..maxGapCubes); the landing ramp always
//   sits DIRECTLY ACROSS, in line with the belt, so the arc always lands. Click to confirm, right click goes back.
//   SHOP (2026-10-20): sold in Utilities (PixelConsumables DeviceKind.BeltJump, a PixelInstantItems tool: right-click starts this placing, one used up per jump).
//   Saved with the tester save (position, turn, gap). Built with PixelFactoryKit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelBeltJump : MonoBehaviour, IPixelAutoIntake
{
    [Min(0.5f)]
    [Tooltip("How fast a husk flies through the jump, in cube widths per second (a speed boost over the belt's 1.6).")]
    [SerializeField] private float flySpeedCubes = 6f;
    [Min(0.3f)]
    [Tooltip("Space kept between two husks in the air, in cube widths.")]
    [SerializeField] private float flyGapCubes = 0.8f;
    [Tooltip("The arc's top above the ramp lips, as a share of the gap (plus half a cube).")]
    [SerializeField] private float arcHeightShare = 0.3f;

    public const float MinGapCubes = 2f, MaxGapCubes = 14f, DefaultGapCubes = 4f, GapStepCubes = 0.5f;
    private const float RampCubes = 1.2f, LipCubes = 0.55f, WidthCubes = 1.35f;

    private static readonly List<PixelBeltJump> all = new List<PixelBeltJump>();
    public static IReadOnlyList<PixelBeltJump> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f, gap;
    private readonly PixelPathCarrier carrier = new PixelPathCarrier();
    private Vector3[] path;
    private float[] cum;
    private readonly List<Transform> glows = new List<Transform>();

    public bool IsGhost => ghost;
    public float Gap => gap;

    // ---------------- building ----------------

    /// <summary>The model alone, for the shop / inventory pictures (no working parts: PixelConsumables.DisplayOnly strips them).</summary>
    public static GameObject CreateDisplay(PixelClicker c) => PixelConsumables.DisplayOnly(Build(c, false, DefaultGapCubes * U(c)));
    public static GameObject CreateGhost(PixelClicker c) => Build(c, true, DefaultGapCubes * U(c));

    /// <summary>Places a working jump with its launch ramp at 'point' (a belt's end), facing 'yaw', the landing ramp 'gapWorld' across.</summary>
    public static GameObject PlaceGap(PixelClicker c, Vector3 point, float yaw, float gapWorld)
    {
        GameObject root = Build(c, false, gapWorld);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    private static float U(PixelClicker c) => Mathf.Max(0.05f, c != null ? c.OldPixelWorldSize : 0.5f);

    public static float StepWorld(GameObject g) { PixelBeltJump j = g != null ? g.GetComponent<PixelBeltJump>() : null; return (j != null ? j.unit : 0.5f) * GapStepCubes; }
    public static float MinWorld(GameObject g) { PixelBeltJump j = g != null ? g.GetComponent<PixelBeltJump>() : null; return (j != null ? j.unit : 0.5f) * MinGapCubes; }
    public static float MaxWorld(GameObject g) { PixelBeltJump j = g != null ? g.GetComponent<PixelBeltJump>() : null; return (j != null ? j.unit : 0.5f) * MaxGapCubes; }
    public static float GapOf(GameObject g) { PixelBeltJump j = g != null ? g.GetComponent<PixelBeltJump>() : null; return j != null ? j.gap : 0f; }
    public static void SetGap(GameObject g, float gapWorld) { PixelBeltJump j = g != null ? g.GetComponent<PixelBeltJump>() : null; if (j != null && Mathf.Abs(j.gap - gapWorld) > 0.001f) j.Rebuild(gapWorld); }

    private static GameObject Build(PixelClicker c, bool ghostMode, float gapWorld)
    {
        GameObject root = new GameObject(ghostMode ? "Belt Jump (Ghost)" : "Belt Jump");
        PixelBeltJump j = root.AddComponent<PixelBeltJump>();
        j.clicker = c;
        j.ghost = ghostMode;
        j.unit = U(c);
        j.Rebuild(gapWorld);
        return root;
    }

    private void Rebuild(float gapWorld)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        glows.Clear();
        path = null;
        float u = unit;
        gap = Mathf.Clamp(gapWorld, MinGapCubes * u, MaxGapCubes * u);
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color steel = new Color(0.55f, 0.58f, 0.64f, 1f), dark = new Color(0.16f, 0.17f, 0.2f, 1f), frame = new Color(0.26f, 0.28f, 0.32f, 1f);
        Color accent = new Color(1f, 0.78f, 0.15f, 1f), boost = new Color(0.35f, 0.9f, 1f, 1f);
        float ramp = RampCubes * u, lip = LipCubes * u, w = WidthCubes * u, surface = 0.16f * u;
        // The LAUNCH ramp (local z 0..ramp): a plate rising from the belt's surface to the lip, rails, a striped lip and glowing booster arrows.
        Ramp(k, "Launch", 0f, ramp, surface, lip, w, dark, steel, frame, accent);
        for (int i = 0; i < 2; i++)
        {
            float z = ramp * (0.3f + 0.35f * i), y = surface + (lip - surface) * (z / ramp);
            Transform g = Chevron(k, "Booster", new Vector3(0f, y + 0.03f * u, z), Mathf.Atan2(lip - surface, ramp) * Mathf.Rad2Deg, w * 0.6f, boost);
            glows.Add(g);
        }
        // The LANDING ramp straight across the gap (local z ramp+gap .. 2 ramp+gap): from its lip down to the belt surface, with a back stop.
        float z0 = ramp + gap;
        Ramp(k, "Landing", z0, z0 + ramp, lip, surface, w, dark, steel, frame, accent);
        k.Box("Landing Stop", transform, new Vector3(0f, lip + 0.25f * u, z0 - 0.05f * u), new Vector3(w + 0.26f * u, 0.12f * u, 0.1f * u), frame, default, false, 0.5f, 0.5f);
        // Little legs under both lips.
        foreach (float z in new[] { ramp - 0.1f * u, z0 + 0.1f * u })
            for (int s = -1; s <= 1; s += 2)
                k.Box("Leg", transform, new Vector3(s * (w * 0.5f - 0.08f * u), lip * 0.5f, z), new Vector3(0.1f * u, lip, 0.1f * u), steel, default, false, 0.7f, 0.5f);
    }

    /// <summary>One ramp from z0 (height y0) to z1 (height y1): a sloped plate, two rails, a yellow lip stripe.</summary>
    private void Ramp(PixelFactoryKit k, string name, float z0, float z1, float y0, float y1, float w, Color plate, Color steel, Color frame, Color accent)
    {
        float u = unit, len = Mathf.Sqrt((z1 - z0) * (z1 - z0) + (y1 - y0) * (y1 - y0)), ang = -Mathf.Atan2(y1 - y0, z1 - z0) * Mathf.Rad2Deg;
        Vector3 mid = new Vector3(0f, (y0 + y1) * 0.5f - 0.05f * u, (z0 + z1) * 0.5f);
        k.Box(name + " Plate", transform, mid, new Vector3(w, 0.1f * u, len), plate, new Vector3(ang, 0f, 0f), false, 0.1f, 0.3f);
        for (int s = -1; s <= 1; s += 2)
            k.Box(name + " Rail", transform, mid + new Vector3(s * (w * 0.5f + 0.06f * u), 0.12f * u, 0f), new Vector3(0.12f * u, 0.3f * u, len), steel, new Vector3(ang, 0f, 0f), false, 0.7f, 0.5f);
        float lipZ = y1 > y0 ? z1 : z0, lipY = Mathf.Max(y0, y1);
        k.Box(name + " Lip", transform, new Vector3(0f, lipY - 0.02f * u, lipZ), new Vector3(w, 0.06f * u, 0.12f * u), accent, default, false, 0.2f, 0.5f, accent * 0.3f);
        k.Box(name + " Skirt", transform, new Vector3(0f, lipY * 0.5f, lipZ), new Vector3(w, lipY, 0.08f * u), frame, default, false, 0.5f, 0.5f);
    }

    private Transform Chevron(PixelFactoryKit k, string name, Vector3 at, float slope, float half, Color c)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(transform, false);
        g.transform.localPosition = at;
        g.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
        for (int s = -1; s <= 1; s += 2)
            k.Box("Bar", g.transform, new Vector3(s * half * 0.25f, 0f, -half * 0.18f), new Vector3(half * 0.55f, 0.015f * unit, 0.08f * unit), c, new Vector3(0f, s * 35f, 0f), false, 0f, 0.6f, c * 0.8f);
        return g.transform;
    }

    // ---------------- placing ----------------

    /// <summary>Stage 1: latches onto a belt END near the mouse (at the end, turned the belt's way). False = no belt end near: it can't be placed.</summary>
    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        PixelConveyor best = PixelConveyor.NearestExit(c, point, 3.5f * U(c));
        if (best == null) return false;
        Vector3 dir = best.Direction;
        snappedYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        snappedPoint = best.ExitPoint;
        return true;
    }

    /// <summary>Where belts snap (placing): the landing ramp's end, at belt height - a belt started there carries on after the jump.</summary>
    public void SnapPoints(System.Action<Vector3> visit) { if (!ghost) visit(LandingEnd); }

    private Vector3 LandingEnd => transform.TransformPoint(new Vector3(0f, 0f, 2f * RampCubes * unit + gap));

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw, gap; }

    public static string ExportState(GameObject root)
    {
        PixelBeltJump j = root != null ? root.GetComponent<PixelBeltJump>() : null;
        return j == null ? null : JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y, gap = j.gap });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : PlaceGap(c, s.position, s.yaw, s.gap);
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (clicker == null) clicker = PixelFind.Cached<PixelClicker>(); PixelCouplings.RegisterAutoIntake(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.UnregisterAutoIntake(this); all.Remove(this); }
    private void OnDestroy() { carrier.ReleaseAll(); }

    /// <summary>A husk handed in at the belt's end (where the launch ramp sits): it is launched.</summary>
    public bool CatchNear(Vector3 at, Rigidbody husk)
    {
        if (ghost || husk == null || carrier.Count > 20) return false;
        Vector3 d = at - transform.position;
        if (Mathf.Abs(d.y) > 0.8f * unit) return false;
        d.y = 0f;
        if (d.magnitude > 0.8f * unit) return false;
        carrier.Add(husk);
        PixelAudio.Play("ui_click", 1.6f);
        return true;
    }

    private void Update()
    {
        if (ghost) return;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 10f);
        foreach (Transform g in glows) if (g != null) g.localScale = Vector3.one * pulse;   // (the booster arrows throb)
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null || carrier.Count == 0) return;
        if (path == null) BuildPath();
        carrier.Step(path, cum, flySpeedCubes * unit, flyGapCubes * unit, Time.fixedDeltaTime, Land);
    }

    /// <summary>The fixed flight: up the launch ramp, a parabola over the gap onto the landing lip, down the landing ramp.</summary>
    private void BuildPath()
    {
        float u = unit, ramp = RampCubes * u, lip = LipCubes * u, half = clicker.OldPixelWorldSize * 0.5f, surface = 0.16f * u;
        List<Vector3> pts = new List<Vector3> { new Vector3(0f, surface + half, 0f), new Vector3(0f, lip + half, ramp) };
        float apex = arcHeightShare * gap + 0.5f * u;
        const int steps = 16;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            pts.Add(new Vector3(0f, lip + half + 4f * apex * t * (1f - t), ramp + gap * t));
        }
        pts.Add(new Vector3(0f, surface + half, 2f * ramp + gap));
        path = new Vector3[pts.Count];
        for (int i = 0; i < pts.Count; i++) path[i] = transform.TransformPoint(pts[i]);
        cum = PixelPathCarrier.Cumulative(path);
    }

    /// <summary>A husk reaches the end of the landing ramp: onto the belt that starts there, or set down on the floor.</summary>
    private void Land(Rigidbody rb)
    {
        PixelConveyor next = PixelCouplings.BeltStartingAt(LandingEnd);
        if (next != null && !next.IsGhost) { PixelCouplings.PutOnBelt(next, rb); return; }
        if (PixelCouplings.HandIn(LandingEnd, rb)) return;   // (a machine's In coupling right there)
        rb.isKinematic = false;
        PixelPathCarrier.SetVelocity(rb, transform.forward * unit * 1.5f);
    }
}
