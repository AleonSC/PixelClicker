// ---- System notes ----
// PixelConnector.cs
//   CONVEYOR CONNECTOR (a Dev Hub > Tester feature, PixelFeatureTester 'Conveyor Connector'): a short boxed channel for gaps in turns or for joining a belt to a machine. Two clicks, like a belt, but it is a single piece (no chaining):
//   the first click snaps to a belt end / elevator top / splitter output / machine opening (IPixelPort: Kiln mouth, Storage Crate), the second click snaps to the other thing, and a box (floor, side walls, top ribs) forms between them.
//   BEHAVIOUR: it FORCES old pixels along it. A belt that ends at the first point hands its pixels in (IPixelBeltAcceptor), and any loose old pixel (not held, not already carried / riding) that is inside the box volume is captured as well - its
//   own path is thrown away and it is carried from where it entered to the far end (PixelPathCarrier, speedCubes cube widths per second, one gap behind the pixel ahead). At the far end the pixel goes onto a belt that STARTS there, or into a machine whose
//   opening is there (PixelConveyorSystem.FindAcceptorAt, e.g. the Kiln or another connector), otherwise it is thrown on along the connector's direction. Width matches the belts (PixelConveyor.LaneCount). Saved with the tester save (the two points).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>A machine's opening a connector can magnetize to (the Kiln mouth, the Storage Crate). PortWorld = the point at floor level of the opening.</summary>
public interface IPixelPort
{
    Vector3 PortWorld { get; }
}

public class PixelConnector : MonoBehaviour, IPixelBeltAcceptor
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
        k.Box("Wall L", transform, new Vector3(-(width * 0.5f + wall * 0.5f), wallH * 0.5f, 0f), new Vector3(wall, wallH, length), steel, default, false, 0.7f, 0.5f);
        k.Box("Wall R", transform, new Vector3(width * 0.5f + wall * 0.5f, wallH * 0.5f, 0f), new Vector3(wall, wallH, length), steel, default, false, 0.7f, 0.5f);
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
    }

    private void EnsurePath()
    {
        if (path != null) return;
        float lift = DeckCubes * unit + clicker.OldPixelWorldSize * 0.5f + 0.02f * unit;
        path = new[] { transform.TransformPoint(new Vector3(0f, lift, -length * 0.5f)), transform.TransformPoint(new Vector3(0f, lift, length * 0.5f)) };
        cum = PixelPathCarrier.Cumulative(path);
    }

    // ---------------- snapping ----------------

    /// <summary>While placing: the nearest belt end, elevator top, splitter output, machine opening or other connector end (by screen distance, at its height).</summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p, out bool snapped)
    {
        Vector3 result = PixelConveyor.Snap(c, p, out bool found);
        Camera cam = c.TargetCamera != null ? c.TargetCamera : Camera.main;
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
        foreach (PixelConnector con in all) if (con != null && !con.ghost) { Try(con.a); Try(con.b); }
        PixelConveyorSystem.ForEachPort(Try);
        snapped = found;
        return result;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 a, b; }

    public static string ExportState(GameObject root)
    {
        PixelConnector p = root != null ? root.GetComponent<PixelConnector>() : null;
        return p == null ? null : JsonUtility.ToJson(new Save { a = p.a, b = p.b });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : Place(c, s.a, s.b);
    }

    // ---------------- behaviour ----------------

    private void OnEnable() { if (!ghost) { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelConveyorSystem.Register(this); } if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelConveyorSystem.Unregister(this); all.Remove(this); }
    private void OnDestroy() { carrier.ReleaseAll(); }

    /// <summary>A belt ending at the first point (or just inside the entrance) hands its pixel in.</summary>
    public bool AcceptsAt(Vector3 world)
    {
        if (ghost || length <= 0f) return false;
        Vector3 d = world - a; d.y = 0f;
        return d.magnitude < 1.1f * unit && world.y > a.y - 0.6f * unit && world.y < a.y + 1.6f * unit;
    }

    public bool AcceptFromBelt(Rigidbody pixel)
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
        EnsurePath();
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        if (carrier.Count < Cap)
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Rigidbody rb = list[i];
                if (rb == null || rb.isKinematic || carrier.Contains(rb)) continue;   // (kinematic = held, riding a belt, or inside another machine)
                OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
                if (info == null || info.heldByPlayer) continue;
                float at = InsideAt(rb.worldCenterOfMass);
                if (at < 0f) continue;
                carrier.Add(rb, at);
                if (carrier.Count >= Cap) break;
            }
        if (carrier.Count > 0) carrier.Step(path, cum, Speed * unit, unit, Time.fixedDeltaTime, PutOut);
    }

    /// <summary>A pixel reaches the far end: onto a belt that starts there, into a machine / connector opening there, or thrown on.</summary>
    private void PutOut(Rigidbody rb)
    {
        Vector3 dir = (b - a).normalized, flat = new Vector3(dir.x, 0f, dir.z);
        float oldSize = clicker.OldPixelWorldSize;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            Vector3 d = belt.StartPoint - b;
            if (new Vector3(d.x, 0f, d.z).magnitude > 1.3f * unit || Mathf.Abs(d.y) > 1.2f * unit) continue;
            Vector3 over = belt.PointAt(0.6f * oldSize);
            rb.position = new Vector3(over.x, over.y + belt.SurfaceHeight + oldSize * 0.7f, over.z);
            PixelPathCarrier.SetVelocity(rb, belt.Direction * belt.Speed * 0.5f);
            return;
        }
        IPixelBeltAcceptor acceptor = PixelConveyorSystem.FindAcceptorAt(b + flat * 0.4f * unit + Vector3.up * 0.5f * unit, this) ?? PixelConveyorSystem.FindAcceptorAt(b + Vector3.up * 0.4f * unit, this);
        if (acceptor != null)
        {
            rb.position = b + Vector3.up * oldSize * 0.6f;
            rb.isKinematic = true;
            if (acceptor.AcceptFromBelt(rb)) return;
            rb.isKinematic = false;
        }
        rb.position = path[1];
        PixelPathCarrier.SetVelocity(rb, dir * unit * 2f);
    }
}
