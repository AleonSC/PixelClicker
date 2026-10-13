// ---- System notes ----
// PixelElevator.cs
//   CONVEYOR ELEVATOR (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Elevator'): a square tower that lifts pixels from the END of a belt up to a higher belt. PLACING (two stages):
//   1. the ghost follows the mouse and MAGNETIZES to a belt end that is near it (PixelElevator.TrySnap: it turns to the belt's direction, the belt's end meets its entry face, and its base
//   takes that belt's height); Q / E turn it when it is not latched; click to fix the base. 2. Move the mouse up or down to set the HEIGHT (the mouse ray is cut with a vertical plane through
//   the base, quantised to heightStepCubes) and click to confirm; right click goes back to stage 1. The tower's posts run down to the floor when the base is raised. A belt end that points
//   at the entry hands its pixels straight in (IPixelBeltAcceptor): they are drawn to the shaft, carried up by a PixelPathCarrier and put out of the top (exit face, +Z) onto a belt that STARTS
//   at OutputPoint (PixelConveyor.Snap offers that point, so a new belt snaps to the top of the tower, at its height) or flung forward. Belts can also be raised and lowered while placing
//   (scroll wheel, PixelFeatureTester) so ramps and bridges are possible. Saved with the tester save (position, turn, height). Built with PixelFactoryKit.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelElevator : MonoBehaviour, IPixelBeltAcceptor
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
    /// <summary>Where a belt starts on top of the tower: the middle of the exit face at the platform's level (a belt's bottom).</summary>
    public Vector3 OutputPoint => transform.TransformPoint(new Vector3(0f, height, HalfCubes * unit));

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
        k.Box("Entry Strip", transform, new Vector3(0f, plate + 0.01f * u, -half + 0.25f * u), new Vector3(1.0f * u, 0.02f * u, 0.3f * u), accent);
        k.Box("Top Plate", transform, new Vector3(0f, h + plate * 0.5f, 0f), new Vector3(half * 2f, plate, half * 2f), dark, default, false, 0.4f, 0.4f);
        k.Box("Exit Lip", transform, new Vector3(0f, h + plate * 0.5f, half + 0.15f * u), new Vector3(1.35f * u, plate, 0.3f * u), dark, default, false, 0.4f, 0.4f);
        k.Box("Exit Strip", transform, new Vector3(0f, h + plate + 0.01f * u, half - 0.1f * u), new Vector3(1.0f * u, 0.02f * u, 0.3f * u), accent);

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

    private void OnEnable() { if (!ghost) { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelConveyorSystem.Register(this); } if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelConveyorSystem.Unregister(this); all.Remove(this); }
    private void OnDestroy() { carrier.ReleaseAll(); }

    /// <summary>A belt's end: the pixel is at the entry when it is just in front of the entry face on the base plate.</summary>
    public bool AcceptsAt(Vector3 world)
    {
        if (ghost) return false;
        Vector3 l = transform.InverseTransformPoint(world) / Mathf.Max(0.0001f, unit);
        return Mathf.Abs(l.x) < HalfCubes + 0.3f && l.z > -HalfCubes - 0.7f && l.z < -HalfCubes + 1.0f && l.y > -0.4f && l.y < 1.5f;
    }

    public bool AcceptFromBelt(Rigidbody pixel)
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
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null || carrier.Count == 0) return;
        if (path == null)
        {
            float oldSize = clicker.OldPixelWorldSize;
            float ey = (PlateCubes * unit) + oldSize * 0.5f;
            path = new[]
            {
                transform.TransformPoint(new Vector3(0f, ey, 0f)),
                transform.TransformPoint(new Vector3(0f, ey + height, 0f)),
                transform.TransformPoint(new Vector3(0f, ey + height, HalfCubes * unit + 0.1f * unit)),
            };
            cum = PixelPathCarrier.Cumulative(path);
        }
        carrier.Step(path, cum, LiftSpeed * unit, Gap * unit, Time.fixedDeltaTime, PutOut);
    }

    /// <summary>A pixel reaches the top: onto a belt that starts there, or flung forward off the lip.</summary>
    private void PutOut(Rigidbody rb)
    {
        Vector3 end = path[path.Length - 1], fwd = transform.forward;
        float oldSize = clicker.OldPixelWorldSize;
        Vector3 velocity = fwd * unit * 2f;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            Vector3 d = belt.StartPoint - OutputPoint;
            if (new Vector3(d.x, 0f, d.z).magnitude > 1.3f * unit || Mathf.Abs(d.y) > 1.2f * unit) continue;
            Vector3 over = belt.PointAt(0.6f * oldSize);
            end = new Vector3(over.x, over.y + belt.SurfaceHeight + oldSize * 0.7f, over.z);
            velocity = belt.Direction * belt.Speed * 0.5f;
            break;
        }
        rb.position = end;
        PixelPathCarrier.SetVelocity(rb, velocity);
    }
}
