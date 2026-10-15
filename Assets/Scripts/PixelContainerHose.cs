// ---- System notes ----
// PixelContainerHose.cs
//   CONTAINER HOSE (a Dev Hub > Tester feature, PixelFeatureTester 'Container Hose', a single two-click line like the Conveyor Connector): a see-through hose like the
//   Funnel's that joins one container to another. Click the SOURCE (a Storage Crate or a Quantum Organizer), then the TARGET (a Recycler, another Storage Crate, a
//   Quantum Organizer or a Trash Can); both clicks magnetize to the nearest container's top (Snap, screen distance). The hose arches between their tops.
//   Every pumpSeconds (0.5 s) it pulls one stored pixel out of the source (TakeOneForHose on PixelStorageCrate / PixelQuantumDevice), spawns it as a husk at the
//   hose's start (SpawnStoredPixel) and carries it along the tube (PixelPathCarrier, can't be grabbed on the way). At the far end: a crate / trash can takes it
//   directly (IPixelBeltAcceptor.AcceptFromBelt); for a Recycler / Quantum Organizer it is let fall into the bin / onto the pad (PixelRecycler.HoseDropPoint),
//   marked Held for a moment so no other machine snatches it on the way down. It only pumps while the target has room (PixelRecycler.HoseCanTake + its
//   count, the crate's free sections, the organizer's capacity) counting what is already in the tube or falling; otherwise pixels wait in the tube.
//   The ends follow the containers when they are carried or turned (each frame: bounds of their colliders -> top centre; the tube is rebuilt when an end moves).
//   A container that is removed leaves its end where it was (the hose idles). Removing the hose lets what is inside it fall out. Saved with the tester save
//   (both end points; on load each end latches onto the container found there).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelContainerHose : MonoBehaviour
{
    [Min(0.05f)]
    [Tooltip("Seconds between two pixels pumped out of the source.")]
    [SerializeField] private float pumpSeconds = 0.5f;
    [Min(0f)]
    [Tooltip("How fast pixels travel through the hose, in cube widths per second.")]
    [SerializeField] private float speedCubes = 3f;
    [Min(1)]
    [Tooltip("Most pixels in the hose at once.")]
    [SerializeField] private int maxInHose = 6;
    [Tooltip("Colour of the see-through hose.")]
    [SerializeField] private Color hoseColor = new Color(0.6f, 0.9f, 1f, 0.28f);

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f;
    private Vector3 a, b;                       // the ends (world), the tops of the two containers
    private Component source, target;
    private readonly List<Collider> sourceCols = new List<Collider>(), targetCols = new List<Collider>();
    private Mesh mesh;
    private Vector3[] path;
    private float[] cum;
    private readonly PixelPathCarrier carrier = new PixelPathCarrier();
    private float pumpTimer;
    private readonly List<Rigidbody> falling = new List<Rigidbody>();
    private readonly Dictionary<Rigidbody, float> fallingSince = new Dictionary<Rigidbody, float>();

    // ------------------------------------------------------------------
    // Line feature hooks (PixelFeatureTester.RegisterLine)
    // ------------------------------------------------------------------

    public static GameObject CreatePreview(PixelClicker c) => Build(c, true, "Container Hose (Preview)");

    public static void UpdatePreview(GameObject preview, Vector3 from, Vector3 to)
    {
        PixelContainerHose h = preview != null ? preview.GetComponent<PixelContainerHose>() : null;
        if (h == null) return;
        h.a = from; h.b = to;
        h.Rebuild();
    }

    public static GameObject Place(PixelClicker c, Vector3 from, Vector3 to)
    {
        GameObject root = Build(c, false, "Container Hose");
        PixelContainerHose h = root.GetComponent<PixelContainerHose>();
        h.source = FindContainer(c, from, true);
        h.target = FindContainer(c, to, false);
        if (h.target == h.source) h.target = null;
        Collect(h.source, h.sourceCols);
        Collect(h.target, h.targetCols);
        h.a = h.source != null ? Port(h.sourceCols, from, h.unit) : from;
        h.b = h.target != null ? Port(h.targetCols, to, h.unit) : to;
        h.Rebuild();
        PixelAudio.Play("device_place");
        return root;
    }

    /// <summary>Magnetizes a click to the top of the nearest container on screen (within ~80 px).</summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p, out bool snapped)
    {
        snapped = false;
        Camera cam = c.TargetCamera != null ? c.TargetCamera : Camera.main;
        if (cam == null) return p;
        Vector2 mouse = PixelInput.PointerPosition();
        float best = 80f * 80f, u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        Vector3 result = p;
        List<Collider> cols = new List<Collider>();
        foreach (Component k in Containers())
        {
            Collect(k, cols);
            Vector3 port = Port(cols, k.transform.position, u);
            Vector3 sp = cam.WorldToScreenPoint(port);
            if (sp.z <= 0f) continue;
            float d = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            if (d < best) { best = d; result = port; snapped = true; }
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Containers
    // ------------------------------------------------------------------

    private static IEnumerable<Component> Containers()
    {
        foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
            if (d is PixelStorageCrate || d is PixelQuantumDevice) yield return d;
        foreach (PixelRecycler r in PixelRecycler.All) if (r != null) yield return r;
        foreach (PixelTrashCan t in PixelTrashCan.All) if (t != null) yield return t;
    }

    private static bool CanBeSource(Component k) => k is PixelStorageCrate || k is PixelQuantumDevice;

    /// <summary>The container whose top is nearest 'p' (within a few cube widths); sources are only crates / organizers.</summary>
    private static Component FindContainer(PixelClicker c, Vector3 p, bool asSource)
    {
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize), best = (3f * u) * (3f * u);
        Component found = null;
        List<Collider> cols = new List<Collider>();
        foreach (Component k in Containers())
        {
            if (asSource && !CanBeSource(k)) continue;
            Collect(k, cols);
            float d = (Port(cols, k.transform.position, u) - p).sqrMagnitude;
            if (d < best) { best = d; found = k; }
        }
        return found;
    }

    private static void Collect(Component k, List<Collider> into)
    {
        into.Clear();
        if (k == null) return;
        foreach (Collider col in k.GetComponentsInChildren<Collider>())
            if (col != null && !col.isTrigger && col.GetComponentInParent<OldPixelInfo>() == null) into.Add(col);   // (not the husks lying in it)
    }

    /// <summary>The top centre of a container (its solid colliders' bounds), a little above it.</summary>
    private static Vector3 Port(List<Collider> cols, Vector3 fallback, float unit)
    {
        bool any = false;
        Bounds bb = new Bounds(fallback, Vector3.zero);
        foreach (Collider col in cols)
        {
            if (col == null || !col.enabled) continue;
            if (!any) { bb = col.bounds; any = true; } else bb.Encapsulate(col.bounds);
        }
        return any ? new Vector3(bb.center.x, bb.max.y + 0.15f * unit, bb.center.z) : fallback;
    }

    // ------------------------------------------------------------------
    // Model
    // ------------------------------------------------------------------

    private static GameObject Build(PixelClicker c, bool ghostMode, string name)
    {
        GameObject root = new GameObject(name);
        PixelContainerHose h = root.AddComponent<PixelContainerHose>();
        h.clicker = c; h.ghost = ghostMode; h.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        h.mesh = new Mesh { name = "Container Hose" };
        root.AddComponent<MeshFilter>().sharedMesh = h.mesh;
        MeshRenderer mr = root.AddComponent<MeshRenderer>();
        Color col = ghostMode ? new Color(h.hoseColor.r, h.hoseColor.g, h.hoseColor.b, h.hoseColor.a * 0.6f) : h.hoseColor;
        mr.sharedMaterial = c.CreateVisualMaterial(col, true);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return root;
    }

    /// <summary>The hose arches from one top to the other: up, over and down (a cubic curve), so husks drop into the target from above.</summary>
    private void Rebuild()
    {
        float dist = Vector3.Distance(a, b);
        float lift = 0.8f * unit + dist * 0.22f;
        Vector3 c1 = a + Vector3.up * lift, c2 = b + Vector3.up * lift;
        const int n = 24;
        path = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1), m = 1f - t;
            path[i] = m * m * m * a + 3f * m * m * t * c1 + 3f * m * t * t * c2 + t * t * t * b;
        }
        cum = PixelPathCarrier.Cumulative(path);
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        PixelTube.Build(path, 0.62f * unit, 14, mesh);
        mesh.RecalculateBounds();
    }

    // ------------------------------------------------------------------
    // Pumping
    // ------------------------------------------------------------------

    private void Update()
    {
        if (ghost || clicker == null) return;
        // The ends follow their containers (carried by the handles, turned...).
        if (source != null || target != null)
        {
            Vector3 na = source != null ? Port(sourceCols, a, unit) : a, nb = target != null ? Port(targetCols, b, unit) : b;
            if ((na - a).sqrMagnitude > 0.0004f || (nb - b).sqrMagnitude > 0.0004f) { a = na; b = nb; Rebuild(); }
        }
        // Husks let fall into a bin: done when the container took them (they left the husk list) or after a moment.
        for (int i = falling.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = falling[i];
            bool taken = rb == null || !InHuskList(rb);
            if (taken || Time.time - fallingSince[rb] > 2.5f)
            {
                if (!taken && rb != null && !PixelRecycler.Holds(rb)) { OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>(); if (d != null) d.Held = false; }
                if (rb != null) fallingSince.Remove(rb);
                falling.RemoveAt(i);
            }
        }
    }

    private bool InHuskList(Rigidbody rb)
    {
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        for (int i = list.Count - 1; i >= 0; i--) if (list[i] == rb) return true;
        return false;
    }

    /// <summary>How many more pixels the target can take right now (minus what is falling into it).</summary>
    private int Room()
    {
        int room;
        switch (target)
        {
            case PixelRecycler r: room = r.HoseCanTake ? r.HoseRoom : 0; break;
            case PixelStorageCrate crate: room = crate.HoseRoom; break;
            case PixelQuantumDevice q: room = q.HoseRoom; break;
            case PixelTrashCan _: room = 1000; break;
            default: room = 0; break;
        }
        return room - falling.Count;
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null || path == null) return;
        float dt = Time.fixedDeltaTime;
        carrier.Step(path, cum, Mathf.Max(0.1f, speedCubes * unit), 1.05f * unit, dt, Deliver, 0.2f, () => Room() > 0);

        pumpTimer -= dt;
        if (pumpTimer > 0f) return;
        pumpTimer = pumpSeconds;
        if (source == null || target == null || carrier.Count >= maxInHose || carrier.Count >= Room()) return;
        int tier; double amount;
        bool got = source is PixelStorageCrate crate ? crate.TakeOneForHose(out tier, out amount)
                 : source is PixelQuantumDevice q ? q.TakeOneForHose(out tier, out amount)
                 : Fail(out tier, out amount);
        if (!got) return;
        if (!clicker.SpawnStoredPixel(tier, amount, path[0], Vector3.zero)) return;   // (a normal husk: a 'streamed' one passes through walls until it lands)
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        Rigidbody rb = list.Count > 0 ? list[list.Count - 1] : null;
        if (rb != null) carrier.Add(rb, 0f);
    }

    private static bool Fail(out int tier, out double amount) { tier = -1; amount = 0; return false; }

    /// <summary>The pixel reached the far end: handed straight in, or let fall into the bin / onto the pad.</summary>
    private void Deliver(Rigidbody rb)
    {
        if (rb == null) return;
        if (target is IPixelBeltAcceptor acc)
        {
            rb.isKinematic = true;
            if (acc.AcceptFromBelt(rb)) return;
            rb.isKinematic = false;
        }
        Vector3 drop = target is PixelRecycler r ? r.HoseDropPoint : b;
        rb.position = drop;
        PixelPathCarrier.SetVelocity(rb, Vector3.down * 1.5f * unit);
        if (!(target is PixelQuantumDevice))   // (the organizer must be able to take it)
        {
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null) d.Held = true;   // nothing else snatches it on the way down
        }
        falling.Add(rb);
        fallingSince[rb] = Time.time;
    }

    private void OnDestroy()
    {
        carrier.ReleaseAll();   // what was in the hose falls out
        foreach (Rigidbody rb in falling)
            if (rb != null && !PixelRecycler.Holds(rb)) { OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>(); if (d != null) d.Held = false; }
        if (mesh != null) Destroy(mesh);
    }

    // ------------------------------------------------------------------
    // Saving (PixelFeatureTester)
    // ------------------------------------------------------------------

    [System.Serializable]
    private class Save { public Vector3 a, b; }

    public static string ExportState(GameObject root)
    {
        PixelContainerHose h = root != null ? root.GetComponent<PixelContainerHose>() : null;
        return h == null ? null : JsonUtility.ToJson(new Save { a = h.a, b = h.b });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : Place(c, s.a, s.b);
    }
}
