// ---- System notes ----
// PixelConnector.cs
//   CONVEYOR CONNECTOR (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Connector'): a short boxed channel for gaps in turns or for joining a belt to a machine. Two clicks, like a belt, but it is a single piece (no chaining):
//   the first click snaps to a belt end / coupling port (elevator top, splitter side, a coupling on a machine...), the second click snaps to the other thing, and a box (floor, side walls, top ribs) forms between them.
//   SOCKETS (PixelSockets.cs, built in and BARE - no coupling model, the box shows the way): 'in' at the first point, 'out' at the far end (+ the adapter's 'right' / 'left').
//   BEHAVIOUR: it FORCES old pixels along it. A belt that ends at the first point hands its pixels in (AcceptHusk), and any loose old pixel (not held, not already carried / riding) that is inside the box volume is captured as well - its
//   own path is thrown away and it is carried from where it entered to the far end (PixelPathCarrier, speedCubes cube widths per second, one gap behind the pixel ahead). At the far end the pixel goes out through its coupling
//   (PixelCouplings.Emit: onto a belt that STARTS there, or into an In coupling there - a machine or another connector), otherwise it is thrown on along the connector's direction. Width matches the belts (PixelConveyor.LaneCount). Saved with the tester save (the two points).
//   HOSE ADAPTER (the same class, adapter = true; tester feature 'Hose Adapter'): a longer trough (3.2 cubes) that SNAPS onto a working Funnel's spout (PixelFunnel.SpoutEndFloor / SpoutYaw, TrySnapAdapter) and turns the spout's way. Its side walls only cover
//   the entry part; the open part has teal port strips and two SIDE PORTS (SidePort(+1 right / -1 left)): a belt placed so its first point snaps to a port (PixelConveyor.Snap / PixelConnector.Snap know them) is fed from the side, else the far end is the exit
//   (UpdateExit, every 0.25 s, picks a port that has a belt starting at it). The funnel's pixels simply fall into the trough's box and are carried to the chosen exit like any connector's.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>A machine's opening a connector can magnetize to (the Kiln mouth, the Storage Crate). PortWorld = the point at floor level of the opening.</summary>
public interface IPixelPort
{
    Vector3 PortWorld { get; }
}

public class PixelConnector : MonoBehaviour, IPixelSocketDevice
{
    [Min(0f)]
    [Tooltip("How fast pixels travel through the connector, in cube widths per second. 0 = the coded default (2.2).")]
    [SerializeField] private float speedCubes = 0f;

    [Min(0)]
    [Tooltip("The most pixels inside at once (a full connector refuses more; a belt then ejects its pixel). 0 = the coded default (8).")]
    [SerializeField] private int maxInside = 0;

    private float Speed => speedCubes > 0f ? speedCubes : 2.2f;
    private int Cap => maxInside > 0 ? maxInside : 8;

    private const float DeckCubes = 0.1f;

    private static readonly List<PixelConnector> all = new List<PixelConnector>();
    public static IReadOnlyList<PixelConnector> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f, width, length;
    private Vector3 a, b;
    private readonly PixelPathCarrier carrier = new PixelPathCarrier();
    private Vector3[] path;
    private float[] cum;
    private int lanesBuilt = 1;
    private bool adapter;               // the Hose Adapter: a longer trough made for a funnel spout, with side ports for belts
    private float AdapterLenCubes => 3.2f;
    private Vector3 exitPoint;          // where pixels leave now: the far end, or a side port that has a belt
    private bool exitSide;
    private string exitId = "out";      // the socket they leave through: "out" (the far end), "right" / "left" (the adapter's side ports)

    // ---- Sockets (PixelSockets.cs, built in, bare - the box itself shows the way): 'in' at the first point (not on the adapter), 'out' at the far end,
    // and the adapter's 'right' / 'left' side ports ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.55f, 0.58f, 0.64f, 1f);

    private void MakeSockets()
    {
        PixelCoupling.RemoveAll(this);
        List<PixelSocket> list = new List<PixelSocket>();
        if (!adapter) list.Add(new PixelSocket("in", new Vector3(0f, 0f, -length * 0.5f), Vector3.back, PixelSocketFlow.Intake, true) { bare = true });
        list.Add(new PixelSocket("out", new Vector3(0f, 0f, length * 0.5f), Vector3.forward, PixelSocketFlow.Output, true) { bare = true });
        if (adapter)
            for (int side = 1; side >= -1; side -= 2)
                list.Add(new PixelSocket(side > 0 ? "right" : "left", new Vector3(side * (width * 0.5f + 0.12f * unit), 0f, -length * 0.5f + length * 0.7f), side > 0 ? Vector3.right : Vector3.left, PixelSocketFlow.Output, true) { bare = true });
        sockets = list.ToArray();
        PixelCoupling.FitBuiltIn(this);
    }
    private float nextExitCheck;

    public bool IsGhost => ghost;
    public Vector3 StartPoint => a;
    public Vector3 EndPoint => b;

    // ---------------- building ----------------

    public static GameObject CreatePreview(PixelClicker c)
    {
        GameObject root = new GameObject("Connector Preview");
        PixelConnector p = root.AddComponent<PixelConnector>();
        p.clicker = c; p.ghost = true; p.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        p.Rebuild(Vector3.zero, Vector3.forward * p.unit);
        return root;
    }

    public static void UpdatePreview(GameObject preview, Vector3 from, Vector3 to)
    {
        PixelConnector p = preview != null ? preview.GetComponent<PixelConnector>() : null;
        if (p == null) return;
        if ((to - from).magnitude < 0.3f * p.unit) { preview.SetActive(false); return; }
        preview.SetActive(true);
        if ((p.a - from).sqrMagnitude > 1e-4f || (p.b - to).sqrMagnitude > 1e-4f) p.Rebuild(from, to);
    }

    public static GameObject Place(PixelClicker c, Vector3 from, Vector3 to)
    {
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        if ((to - from).magnitude < 0.5f * u) return null;
        GameObject root = new GameObject("Conveyor Connector");
        PixelConnector p = root.AddComponent<PixelConnector>();
        p.clicker = c; p.unit = u;
        p.Rebuild(from, to);
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    /// <summary>The Hose Adapter ghost: a see-through adapter that stands at the origin pointing +Z (the tester moves / turns the wrapper).</summary>
    public static GameObject CreateAdapterGhost(PixelClicker c)
    {
        GameObject wrapper = new GameObject("Hose Adapter (Ghost)");
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        GameObject root = new GameObject("Body");
        PixelConnector p = root.AddComponent<PixelConnector>();
        p.clicker = c; p.ghost = true; p.unit = u; p.adapter = true;
        p.Rebuild(Vector3.zero, Vector3.forward * (p.AdapterLenCubes * u));
        root.transform.SetParent(wrapper.transform, true);
        return wrapper;
    }

    /// <summary>A working Hose Adapter whose entry is at 'start' (a funnel's spout end) pointing along 'yaw'.</summary>
    public static GameObject PlaceAdapter(PixelClicker c, Vector3 start, float yaw)
    {
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        GameObject root = new GameObject("Hose Adapter");
        PixelConnector p = root.AddComponent<PixelConnector>();
        p.clicker = c; p.unit = u; p.adapter = true;
        p.Rebuild(start, start + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (p.AdapterLenCubes * u));
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    /// <summary>While placing the adapter: latches onto the nearest working funnel's spout (turned the spout's way).</summary>
    public static bool TrySnapAdapter(PixelClicker c, Vector3 point, float yaw, GameObject ghostRoot, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        Camera cam = c.ViewCamera;
        Vector2 mouse = PixelInput.PointerPosition();
        PixelFunnel best = null; float bestScore = float.MaxValue;
        foreach (PixelFunnel f in PixelFunnel.Working)
        {
            if (f == null || !f.IsWorking) continue;
            Vector3 e = f.SpoutEndFloor;
            float score = float.MaxValue;
            float flat = new Vector3(e.x - point.x, 0f, e.z - point.z).magnitude;
            if (flat < 3.5f * u) score = flat / (3.5f * u);
            if (cam != null) { Vector3 sp = cam.WorldToScreenPoint(e); if (sp.z > 0f) { float px = (new Vector2(sp.x, sp.y) - mouse).magnitude; if (px < 110f) score = Mathf.Min(score, px / 110f); } }
            if (score < bestScore) { bestScore = score; best = f; }
        }
        if (best == null) return false;
        snappedPoint = best.SpoutEndFloor;
        snappedYaw = best.SpoutYaw;
        return true;
    }

    /// <summary>The place on the adapter's side (belt-world) where a belt can start: side +1 = its right, -1 = its left, a bit past the middle.</summary>
    public Vector3 SidePort(int side)
    {
        Vector3 dir = (b - a).normalized, right = new Vector3(dir.z, 0f, -dir.x);
        return a + dir * (length * 0.7f) + right * (side * (width * 0.5f + 0.12f * unit));
    }

    /// <summary>Does this connector contain the world point and have room for another pixel? (False when it is full; true when the point is not in it at all.)</summary>
    public bool HasRoomAt(Vector3 world) => ghost || InsideAt(world) < 0f || carrier.Count < Cap;

    public bool IsAdapter => adapter && !ghost;

    private void Rebuild(Vector3 from, Vector3 to)
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { GameObject old = transform.GetChild(i).gameObject; old.SetActive(false); Destroy(old); }
        a = from; b = to;
        length = (b - a).magnitude;
        lanesBuilt = PixelConveyor.LaneCount;
        width = (1.35f + (lanesBuilt - 1) * 1.1f) * unit;
        transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation((b - a).normalized, Vector3.up));
        float u = unit, deck = DeckCubes * u, wall = 0.13f * u, wallH = 0.7f * u;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color dark = new Color(0.2f, 0.22f, 0.27f, 1f), steel = new Color(0.55f, 0.58f, 0.64f, 1f), accent = new Color(1f, 0.78f, 0.15f, 1f), teal = new Color(0.2f, 0.85f, 0.8f, 1f);
        k.Box("Floor", transform, new Vector3(0f, deck * 0.5f, 0f), new Vector3(width, deck, length), dark, default, false, 0.3f, 0.4f);
        // The adapter keeps its side walls only along the entry part: the far part is open at the sides, where belts can snap on.
        float wallLen = adapter ? length * 0.62f : length, wallZ = adapter ? -length * 0.5f + wallLen * 0.5f : 0f;
        k.Box("Wall L", transform, new Vector3(-(width * 0.5f + wall * 0.5f), wallH * 0.5f, wallZ), new Vector3(wall, wallH, wallLen), steel, default, false, 0.7f, 0.5f);
        k.Box("Wall R", transform, new Vector3(width * 0.5f + wall * 0.5f, wallH * 0.5f, wallZ), new Vector3(wall, wallH, wallLen), steel, default, false, 0.7f, 0.5f);
        if (adapter)
        {
            // Teal port strips on both edges of the open part, a collar round the entry (where the hose spout sits) and a low end lip.
            float pz = length * 0.2f;
            k.Box("Port Strip L", transform, new Vector3(-(width * 0.5f - 0.1f * u), deck + 0.012f * u, pz), new Vector3(0.2f * u, 0.024f * u, length * 0.34f), teal);
            k.Box("Port Strip R", transform, new Vector3(width * 0.5f - 0.1f * u, deck + 0.012f * u, pz), new Vector3(0.2f * u, 0.024f * u, length * 0.34f), teal);
            k.Box("Collar", transform, new Vector3(0f, wallH * 0.5f, -length * 0.5f + 0.06f * u), new Vector3(width + wall * 2f, wallH, 0.12f * u), accent, default, false, 0.5f, 0.5f);
        }
        // Top ribs (a cage rather than a solid lid, so the pixels inside stay visible) and an arrow strip showing the way.
        int ribs = Mathf.Max(2, Mathf.RoundToInt(length / (1.4f * u)) + 1);
        for (int i = 0; i < ribs; i++)
        {
            float z = Mathf.Lerp(-length * 0.5f + 0.08f * u, length * 0.5f - 0.08f * u, ribs == 1 ? 0.5f : i / (float)(ribs - 1));
            k.Box("Rib", transform, new Vector3(0f, wallH + wall * 0.5f, z), new Vector3(width + wall * 2f, wall, 0.14f * u), accent, default, false, 0.5f, 0.5f);
        }
        k.Box("Arrow Strip", transform, new Vector3(0f, deck + 0.01f * u, 0f), new Vector3(0.18f * u, 0.02f * u, Mathf.Max(0.2f * u, length - 0.5f * u)), teal);
        k.Box("Head", transform, new Vector3(0f, deck + 0.012f * u, length * 0.5f - 0.2f * u), new Vector3(0.5f * u, 0.02f * u, 0.2f * u), teal);
        path = null;
        exitPoint = b; exitSide = false; exitId = "out";
        if (!ghost) MakeSockets();
    }

    private void EnsurePath()
    {
        if (path != null) return;
        float lift = DeckCubes * unit + clicker.OldPixelWorldSize * 0.5f + 0.02f * unit;
        Vector3 up = Vector3.up * lift;
        Vector3 start = transform.TransformPoint(new Vector3(0f, lift, -length * 0.5f));
        Vector3 dir = (b - a).normalized;
        if (exitSide)
        {
            Vector3 mid = a + dir * (length * 0.7f) + up;
            path = new[] { start, new Vector3(mid.x, start.y + (mid.y - start.y), mid.z), exitPoint + up };
        }
        else path = new[] { start, transform.TransformPoint(new Vector3(0f, lift, length * 0.5f)) };
        cum = PixelPathCarrier.Cumulative(path);
    }

    // ---------------- snapping ----------------

    /// <summary>While placing: the nearest belt end, elevator top, splitter output, machine opening or other connector end (by screen distance, at its height).</summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p, out bool snapped)
    {
        Vector3 result = PixelConveyor.Snap(c, p, out bool found);
        Camera cam = c.ViewCamera;
        Vector2 mouse = PixelInput.PointerPosition();
        float best = 70f * 70f;
        if (found && cam != null) { Vector3 sp0 = cam.WorldToScreenPoint(result); if (sp0.z > 0f) best = Mathf.Min(best, (new Vector2(sp0.x, sp0.y) - mouse).sqrMagnitude); }
        void Try(Vector3 end)
        {
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(end);
            if (sp.z <= 0f) return;
            float d = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            if (d < best) { best = d; result = end; found = true; }
        }
        foreach (PixelConnector con in all) if (con != null && !con.ghost) { Try(con.a); Try(con.b); if (con.adapter) { Try(con.SidePort(1)); Try(con.SidePort(-1)); } }
        PixelConveyorSystem.ForEachPort(Try);
        PixelCouplings.Ports(Try);   // couplings on devices
        snapped = found;
        return result;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 a, b; public bool adapter; }

    public static string ExportState(GameObject root)
    {
        PixelConnector p = root != null ? root.GetComponent<PixelConnector>() : null;
        return p == null ? null : JsonUtility.ToJson(new Save { a = p.a, b = p.b, adapter = p.adapter });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        if (!s.adapter) return Place(c, s.a, s.b);
        Vector3 d = s.b - s.a;
        return PlaceAdapter(c, s.a, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelCouplings.Register(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); all.Remove(this); }
    private void OnDestroy() { carrier.ReleaseAll(); }

    /// <summary>A husk comes in through the 'in' coupling at the first point (a belt ending there): carried along from the start. Refused when full.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody pixel)
    {
        if (ghost || pixel == null || clicker == null || carrier.Count >= Cap) return false;
        EnsurePath();
        carrier.Add(pixel, 0f);
        return true;
    }

    /// <summary>Where along the box (distance from the first point) a world point is, or -1 when it is not inside the box.</summary>
    private float InsideAt(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world) / Mathf.Max(0.0001f, unit);
        float half = length / unit * 0.5f;
        if (Mathf.Abs(l.x) > width / unit * 0.5f || l.y < -0.1f || l.y > 1.3f) return -1f;
        if (l.z < -half - 0.3f || l.z > half - 0.5f) return -1f;
        return Mathf.Clamp((l.z + half) * unit, 0f, length);
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null) return;
        if (adapter && Time.time >= nextExitCheck) { nextExitCheck = Time.time + 0.25f; UpdateExit(); }
        EnsurePath();
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        if (carrier.Count < Cap)
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = list[i];
                if (rb == null || rb.isKinematic || carrier.Contains(rb)) continue;   // (kinematic = held, riding a belt, or inside another machine)
                OldPixelInfo info = PixelHuskQuery.Get(rb).info;
                if (info == null || info.heldByPlayer) continue;
                float at = InsideAt(rb.worldCenterOfMass);
                if (at < 0f) continue;
                carrier.Add(rb, at);
                if (carrier.Count >= Cap) break;
            }
        if (carrier.Count > 0) carrier.Step(path, cum, Speed * unit, unit, Time.fixedDeltaTime, PutOut);
    }

    /// <summary>The adapter's exit: a side port with a belt starting at it (the right side first), else the far end.</summary>
    private void UpdateExit()
    {
        Vector3 chosen = b; bool side = false; string id = "out";
        for (int s = 1; s >= -1 && !side; s -= 2)
        {
            string sid = s > 0 ? "right" : "left";
            if (PixelCouplings.OutputConnected(this, sid)) { chosen = SidePort(s); side = true; id = sid; }   // (a belt starts at that side port)
        }
        exitId = id;
        if (side != exitSide || (chosen - exitPoint).sqrMagnitude > 1e-4f) { exitPoint = chosen; exitSide = side; path = null; }
    }

    /// <summary>A pixel reaches the far end: out through its coupling (onto a belt that starts there, or into an In coupling there - a machine or another connector), or thrown on.</summary>
    private void PutOut(Rigidbody rb)
    {
        if (PixelCouplings.Emit(this, exitId, rb)) return;
        Vector3 dir = exitSide ? (path[path.Length - 1] - path[path.Length - 2]).normalized : (b - a).normalized;
        rb.position = path[path.Length - 1];
        PixelPathCarrier.SetVelocity(rb, dir * unit * 2f);
    }
}
