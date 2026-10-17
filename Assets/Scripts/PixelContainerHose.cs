// ---- System notes ----
// PixelContainerHose.cs
//   CONTAINER HOSE (a Dev Hub > Tester feature, PixelFeatureTester 'Container Hose', a single two-click line like the Conveyor Connector): a see-through hose like the
//   Funnel's that joins one container to another. Click the SOURCE (a Storage Crate or a Quantum Organizer), then the TARGET (a Recycler, another Storage Crate, a
//   Quantum Organizer or a Trash Can); both clicks magnetize to the nearest container's top (Snap: the container under the mouse, else the nearest top within 120 px). Clicked the other way round (target first) the hose runs from the crate / organizer anyway.
//   Placed devices have NO colliders, so a container's place and top come from its MESH RENDERERS' bounds (Collect / Port; not its texts or the husks in it).
//   An idle hose says why over its middle (IdleReason: no source / no target / waiting / source empty). The hose arches between their tops.
//   Every pumpSeconds (0.5 s) it pulls one stored pixel out of the source (TakeOneForHose on PixelStorageCrate / PixelQuantumDevice), spawns it as a husk at the
//   hose's start (SpawnStoredPixel) and carries it along the tube (PixelPathCarrier, can't be grabbed on the way). At the far end: a crate / trash can takes it
//   directly (IPixelBeltAcceptor.AcceptFromBelt); for a Recycler / Quantum Organizer it is let fall into the bin / onto the pad (PixelRecycler.HoseDropPoint),
//   marked Held for a moment so no other machine snatches it on the way down. It only pumps while the target has room (PixelRecycler.HoseCanTake + its
//   count, the crate's free sections, the organizer's capacity) counting what is already in the tube or falling; otherwise pixels wait in the tube.
//   The ends follow the containers when they are carried or turned (each frame: bounds of their mesh renderers -> top centre; the tube is rebuilt when an end moves).
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
    private readonly List<Renderer> sourceParts = new List<Renderer>(), targetParts = new List<Renderer>();
    private TMPro.TextMeshPro status;
    private string statusText;
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
        Component first = FindContainer(c, from), second = FindContainer(c, to);
        if (second == first) second = null;
        // The source must be a crate / organizer: clicked the other way round (a Recycler first), the hose simply runs the other way.
        if (!CanBeSource(first) && CanBeSource(second)) { Component t = first; first = second; second = t; Vector3 tp = from; from = to; to = tp; }
        h.source = CanBeSource(first) ? first : null;
        h.target = second;
        Collect(h.source, h.sourceParts);
        Collect(h.target, h.targetParts);
        h.a = h.source != null ? Port(h.sourceParts, from, h.unit) : from;
        h.b = h.target != null ? Port(h.targetParts, to, h.unit) : to;
        h.Rebuild();
        PixelAudio.Play("device_place");
        return root;
    }

    /// <summary>Magnetizes a click to the top of the nearest container on screen (within ~80 px).</summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p, out bool snapped)
    {
        snapped = false;
        Camera cam = c.ViewCamera;
        if (cam == null) return p;
        Vector2 mouse = PixelInput.PointerPosition();
        Ray ray = cam.ScreenPointToRay(mouse);
        float best = 120f * 120f, bestHit = float.MaxValue, u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        Vector3 result = p;
        List<Renderer> parts = new List<Renderer>();
        foreach (Component k in Containers())
        {
            Collect(k, parts);
            Vector3 port = Port(parts, k.transform.position, u);
            // The mouse is on the container itself: that one (the nearest such).
            if (TryBounds(parts, out Bounds bb) && bb.IntersectRay(ray, out float hit))
            {
                if (hit < bestHit) { bestHit = hit; result = port; snapped = true; }
                continue;
            }
            if (bestHit < float.MaxValue) continue;
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

    /// <summary>The container at 'p': the one whose footprint (seen from above, with a cube width to spare) holds it, else the nearest top within a few cube widths.</summary>
    private static Component FindContainer(PixelClicker c, Vector3 p)
    {
        float u = Mathf.Max(0.05f, c.OldPixelWorldSize), best = (4f * u) * (4f * u);
        Component found = null;
        List<Renderer> parts = new List<Renderer>();
        foreach (Component k in Containers())
        {
            Collect(k, parts);
            if (!TryBounds(parts, out Bounds bb)) bb = new Bounds(k.transform.position, Vector3.one * u);
            Vector3 flat = new Vector3(Mathf.Clamp(p.x, bb.min.x, bb.max.x), p.y, Mathf.Clamp(p.z, bb.min.z, bb.max.z));
            float d = new Vector2(flat.x - p.x, flat.z - p.z).sqrMagnitude * 0.25f + 0.0001f * (bb.center - p).sqrMagnitude;   // (inside the footprint = ~0; ties: the nearer centre)
            if (d < best) { best = d; found = k; }
        }
        return found;
    }

    /// <summary>A container's solid model parts (placed devices have no colliders, so the mesh renderers say where it is). Not the husks lying in it, not its texts.</summary>
    private static void Collect(Component k, List<Renderer> into)
    {
        into.Clear();
        if (k == null) return;
        foreach (MeshRenderer r in k.GetComponentsInChildren<MeshRenderer>())
            if (r != null && r.GetComponent<TMPro.TMP_Text>() == null && r.GetComponentInParent<OldPixelInfo>() == null) into.Add(r);
    }

    private static bool TryBounds(List<Renderer> parts, out Bounds bb)
    {
        bb = default;
        bool any = false;
        foreach (Renderer r in parts)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
        }
        return any;
    }

    /// <summary>The top centre of a container (its model's bounds), a little above it.</summary>
    private static Vector3 Port(List<Renderer> parts, Vector3 fallback, float unit)
    {
        return TryBounds(parts, out Bounds bb) ? new Vector3(bb.center.x, bb.max.y + 0.15f * unit, bb.center.z) : fallback;
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

    /// <summary>Why the hose is idle (shown over its middle), or "" while it works.</summary>
    private string IdleReason()
    {
        if (source == null) return "No source: start the hose on a Storage Crate or Quantum Organizer";
        if (target == null) return "No target: end it on a Recycler, crate, organizer or trash can";
        if (Room() <= 0 && carrier.Count > 0) return target is PixelRecycler ? "Waiting: the Recycler is full or working" : "Waiting: the target is full";
        if (carrier.Count == 0 && pumpedNothing) return "The source is empty";
        return "";
    }

    private bool pumpedNothing;

    private void UpdateStatus()
    {
        string want = IdleReason();
        if (want == statusText) { if (status != null && path != null) Face(); return; }
        statusText = want;
        if (status == null)
        {
            GameObject g = new GameObject("Hose Status");
            g.transform.SetParent(transform, false);
            status = g.AddComponent<TMPro.TextMeshPro>();
            status.fontSize = 2.4f * unit;
            status.alignment = TMPro.TextAlignmentOptions.Center;
            status.color = new Color(1f, 0.85f, 0.4f, 1f);
            status.rectTransform.sizeDelta = new Vector2(14f * unit, 3f * unit);
            if (clicker.UIFont != null) status.font = clicker.UIFont;
        }
        status.text = want;
        status.gameObject.SetActive(want.Length > 0);
        Face();
    }

    private void Face()
    {
        if (status == null || path == null || !status.gameObject.activeSelf) return;
        Camera cam = clicker.ViewCamera;
        status.transform.position = path[path.Length / 2] + Vector3.up * 1.2f * unit;
        if (cam != null) status.transform.rotation = Quaternion.LookRotation(status.transform.position - cam.transform.position, cam.transform.up);
    }

    private void Update()
    {
        if (ghost || clicker == null) return;
        UpdateStatus();
        // The ends follow their containers (carried by the handles, turned...).
        if (source != null || target != null)
        {
            Vector3 na = source != null ? Port(sourceParts, a, unit) : a, nb = target != null ? Port(targetParts, b, unit) : b;
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
        pumpedNothing = !got;
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
