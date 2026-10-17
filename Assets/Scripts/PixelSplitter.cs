// ---- System notes ----
// PixelSplitter.cs
//   SPLITTER BLOCK (a Dev Hub > Tester feature, PixelFeatureTester 'Splitter'): a low square block that SNAPS to the end of a conveyor belt (PixelSplitter.TrySnap: bring the ghost near a belt end and it
//   latches on, turned the belt's way, its entry face meeting the belt's end, at the belt's height) and shares what the belt brings out of the OTHER THREE sides (front +Z, right +X, left -X). A new
//   belt snaps to any of the three outputs. SOCKETS (PixelSockets.cs, couplings built in): 'in' (the entry) and 'out0..2' (the sides). A husk handed in (AcceptHusk) is sent to the next output in turn (round robin), but
//   only among the sides whose Out coupling has a belt starting at it (PixelCouplings.OutputConnected; Emit puts it on); if no side has a belt, all three are used and the pixels are flung out. Each side has its
//   own PixelPathCarrier (entry -> centre -> out), so a side that is full (maxQueuedPerSide) is skipped and, if all are full, the belt keeps (ejects) the pixel. Raised splitters get posts to the floor. Saved with the tester save.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelSplitter : MonoBehaviour, IPixelSocketDevice
{
    [Min(0f)]
    [Tooltip("How fast pixels cross the block, in cube widths per second. 0 = the coded default (2.5).")]
    [SerializeField] private float crossSpeedCubes = 0f;

    [Min(0)]
    [Tooltip("The most pixels waiting on one side at once (a full side is skipped). 0 = the coded default (4).")]
    [SerializeField] private int maxQueuedPerSide = 0;

    private float Speed => crossSpeedCubes > 0f ? crossSpeedCubes : 2.5f;
    private int Cap => maxQueuedPerSide > 0 ? maxQueuedPerSide : 4;

    private const float HalfCubes = 0.85f, PlateCubes = 0.16f;

    private static readonly List<PixelSplitter> all = new List<PixelSplitter>();
    public static IReadOnlyList<PixelSplitter> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f, builtY = float.MaxValue;
    private readonly PixelPathCarrier[] carriers = { new PixelPathCarrier(), new PixelPathCarrier(), new PixelPathCarrier() };
    private Vector3[][] paths;
    private float[][] cums;
    private int next;

    public bool IsGhost => ghost;

    private static Vector3 SideDir(int i) => i == 0 ? Vector3.forward : i == 1 ? Vector3.right : Vector3.left;

    // ---- Sockets (PixelSockets.cs): the entry (In) and the three sides (Out), each with a coupling built in ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.55f, 0.58f, 0.64f, 1f);
    private static string OutId(int side) => "out" + side;

    private void MakeSockets()
    {
        float half = HalfCubes * unit;
        sockets = new[]
        {
            new PixelSocket("in", new Vector3(0f, 0f, -half), Vector3.back, PixelSocketFlow.Intake, true),
            new PixelSocket(OutId(0), SideDir(0) * half, SideDir(0), PixelSocketFlow.Output, true),
            new PixelSocket(OutId(1), SideDir(1) * half, SideDir(1), PixelSocketFlow.Output, true),
            new PixelSocket(OutId(2), SideDir(2) * half, SideDir(2), PixelSocketFlow.Output, true),
        };
    }

    // ---------------- building ----------------

    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, "Splitter (Ghost)"); }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false, "Splitter");
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        root.GetComponent<PixelSplitter>().Rebuild();   // (again, now that it stands at its height: the posts reach the floor)
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, string name)
    {
        GameObject root = new GameObject(name);
        PixelSplitter s = root.AddComponent<PixelSplitter>();
        s.clicker = c;
        s.ghost = ghostMode;
        s.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        if (!ghostMode) s.MakeSockets();
        s.Rebuild();
        return root;
    }

    private void Rebuild()
    {
        PixelCoupling.RemoveAll(this);   // (its built-in couplings are children: made again below)
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        builtY = transform.position.y;
        float u = unit, half = HalfCubes * u, plate = PlateCubes * u;
        float floorLocal = Mathf.Min(0f, PixelConveyor.FloorY(clicker) - transform.position.y);
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color steel = new Color(0.55f, 0.58f, 0.64f, 1f), dark = new Color(0.22f, 0.24f, 0.28f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f), teal = new Color(0.2f, 0.85f, 0.8f, 1f);

        k.Box("Deck", transform, new Vector3(0f, plate * 0.5f, 0f), new Vector3(half * 2f, plate, half * 2f), dark, default, false, 0.4f, 0.4f);
        k.Box("Entry Strip", transform, new Vector3(0f, plate + 0.01f * u, -half + 0.2f * u), new Vector3(0.9f * u, 0.02f * u, 0.25f * u), accent);
        for (int i = 0; i < 3; i++)
        {
            Vector3 d = SideDir(i);
            k.Box("Out Strip", transform, d * (half - 0.2f * u) + Vector3.up * (plate + 0.01f * u), i == 0 ? new Vector3(0.9f * u, 0.02f * u, 0.25f * u) : new Vector3(0.25f * u, 0.02f * u, 0.9f * u), teal);
        }
        // The diverter in the middle (a diamond that shows the block shares its load) and low corner posts.
        k.Box("Diverter", transform, new Vector3(0f, plate + 0.05f * u, 0f), new Vector3(0.5f * u, 0.1f * u, 0.5f * u), accent, new Vector3(0f, 45f, 0f), false, 0.4f, 0.5f);
        float o = half - 0.07f * u;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                k.Box("Post", transform, new Vector3(sx * o, (floorLocal + 0.5f * u) * 0.5f, sz * o), new Vector3(0.14f * u, 0.5f * u - floorLocal, 0.14f * u), steel, default, false, 0.7f, 0.5f);
        paths = null;
        if (!ghost) PixelCoupling.FitBuiltIn(this);
    }

    // ---------------- placing ----------------

    /// <summary>While placing: latches onto a belt end near the mouse (turns to the belt's direction, the entry meets the belt's end, takes its height).</summary>
    public static bool TrySnap(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        PixelConveyor best = PixelConveyor.NearestExit(c, point, 3.5f * u);
        if (best == null) return false;
        Vector3 dir = best.Direction, exit = best.ExitPoint;
        snappedYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        snappedPoint = new Vector3(exit.x + dir.x * HalfCubes * u, exit.y, exit.z + dir.z * HalfCubes * u);
        return true;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; }

    public static string ExportState(GameObject root)
    {
        PixelSplitter s = root != null ? root.GetComponent<PixelSplitter>() : null;
        return s == null ? null : JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : Place(c, s.position, s.yaw);
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelCouplings.Register(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); all.Remove(this); }
    private void OnDestroy() { foreach (PixelPathCarrier c in carriers) c.ReleaseAll(); }

    private void Update()
    {
        if (ghost && Mathf.Abs(transform.position.y - builtY) > 0.01f) Rebuild();   // (the ghost's posts follow its height above the floor)
    }

    /// <summary>Does a belt start at this side's Out coupling?</summary>
    private bool Connected(int side) => PixelCouplings.OutputConnected(this, OutId(side));

    private void EnsurePaths()
    {
        if (paths != null) return;
        float ey = PlateCubes * unit + clicker.OldPixelWorldSize * 0.5f, half = HalfCubes * unit;
        paths = new Vector3[3][]; cums = new float[3][];
        for (int i = 0; i < 3; i++)
        {
            paths[i] = new[]
            {
                transform.TransformPoint(new Vector3(0f, ey, -half + 0.2f * unit)),
                transform.TransformPoint(new Vector3(0f, ey, 0f)),
                transform.TransformPoint(SideDir(i) * (half + 0.1f * unit) + Vector3.up * ey),
            };
            cums[i] = PixelPathCarrier.Cumulative(paths[i]);
        }
    }

    /// <summary>A husk comes in through the entry coupling: it goes to the next output in turn (among the sides with a belt, or all three if none has one).</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody pixel)
    {
        if (ghost || pixel == null || clicker == null) return false;
        EnsurePaths();
        bool[] use = { Connected(0), Connected(1), Connected(2) };
        if (!use[0] && !use[1] && !use[2]) use = new[] { true, true, true };
        for (int n = 0; n < 3; n++)
        {
            int side = (next + n) % 3;
            if (!use[side] || carriers[side].Count >= Cap) continue;
            carriers[side].Add(pixel);
            next = (side + 1) % 3;
            return true;
        }
        return false;
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null || paths == null) return;
        for (int i = 0; i < 3; i++)
        {
            if (carriers[i].Count == 0) continue;
            int side = i;
            carriers[i].Step(paths[i], cums[i], Speed * unit, unit, Time.fixedDeltaTime, rb => PutOut(rb, side));
        }
    }

    /// <summary>A pixel reaches the edge of its side: out through that side's coupling onto the belt there (PixelCouplings.Emit), or flung outwards.</summary>
    private void PutOut(Rigidbody rb, int side)
    {
        if (PixelCouplings.Emit(this, OutId(side), rb)) return;
        rb.position = paths[side][2];
        PixelPathCarrier.SetVelocity(rb, transform.TransformDirection(SideDir(side)) * unit * 2f);
    }
}
