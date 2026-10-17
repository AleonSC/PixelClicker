// ---- System notes ----
// PixelContainerHose.cs
//   CONTAINER HOSE (a Dev Hub > Tester feature, PixelFeatureTester 'Container Hose', a single two-click line like the Conveyor Connector): a see-through hose like the
//   Funnel's that joins one device to another THROUGH COUPLINGS (PixelSockets.cs / PixelCoupling.cs, docs/systems/PixelCouplings.md). Click an OUT coupling on a device a
//   hose can pull from (IPixelHuskSource: Storage Crate, Quantum Organizer - set a crate / organizer coupling to Out with its teal dot), then an IN coupling on the target
//   (Recycler back hatch, Storage Crate, Quantum Organizer, Trash Can, Kiln, Sorting Hopper...); both clicks magnetize to the nearest coupling (Snap). Clicked the other
//   way round it runs the right way anyway. An idle hose says why over its middle (IdleReason). The hose arches from one coupling to the other.
//   Every pumpSeconds (0.5 s) it pulls one stored husk out of the source (TakeHuskOut), spawns it at the hose's start (SpawnStoredPixel) and carries it along the tube
//   (PixelPathCarrier, can't be grabbed on the way). At the far end the target coupling hands it to its device (PixelCoupling.TakeIn -> the device's one hook AcceptHusk);
//   refused (full) = it drops out of the end. It only pumps while the target has room (IPixelHuskRoom.HuskRoom; devices without it take 6 at a time), counting what is in
//   the tube. The ends follow their couplings (carried / turned devices). A coupling that is removed leaves its end where it was: the hose idles and latches onto a
//   coupling put back there (every half second). Removing the hose lets what is inside it fall out. Saved with the tester save (both end points).
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
    private Vector3 a, b;                       // the ends (world): just above the two couplings' ports
    private PixelCoupling source, target;       // an Out coupling (the device gives husks) and an In coupling (the device takes them)
    private TMPro.TextMeshPro status;
    private string statusText;
    private Mesh mesh;
    private Vector3[] path;
    private float[] cum;
    private readonly PixelPathCarrier carrier = new PixelPathCarrier();
    private float pumpTimer, nextRelatch;

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
        h.a = from; h.b = to;
        h.Latch();
        h.a = h.EndAt(h.source, from);
        h.b = h.EndAt(h.target, to);
        h.Rebuild();
        PixelAudio.Play("device_place");
        return root;
    }

    /// <summary>Finds the couplings at the two ends: an Out coupling on a device a hose can pull from, and an In coupling (clicked either way round).</summary>
    private void Latch()
    {
        float reach = 1.5f * unit;
        PixelCoupling first = PixelCouplings.NearestCoupling(LowerEnd(a), reach, true, true), second = PixelCouplings.NearestCoupling(LowerEnd(b), reach, true, true);
        if (second == first) second = null;
        if (!IsSource(first) && IsSource(second)) { PixelCoupling t = first; first = second; second = t; Vector3 tp = a; a = b; b = tp; }   // (clicked the other way round)
        if (source == null && IsSource(first)) source = first;
        if (target == null && second != null && second.IsIn) target = second;
    }

    private static bool IsSource(PixelCoupling k) => k != null && k.IsOut && k.Device is IPixelHuskSource;

    /// <summary>Where the hose meets a coupling: over its collar (or the saved point when there is none).</summary>
    private Vector3 EndAt(PixelCoupling k, Vector3 fallback) => k != null ? k.PortWorld + Vector3.up * 0.55f * unit + k.FacingWorld * 0.35f * unit : fallback;

    /// <summary>The end point brought back down to port height (the saved ends are over the collar).</summary>
    private Vector3 LowerEnd(Vector3 p) => p - Vector3.up * 0.55f * unit;

    /// <summary>Magnetizes a click to the nearest coupling on screen (within ~90 px), over its collar.</summary>
    public static Vector3 Snap(PixelClicker c, Vector3 p, out bool snapped)
    {
        snapped = false;
        Camera cam = c.ViewCamera;
        if (cam == null) return p;
        Vector2 mouse = PixelInput.PointerPosition();
        float best = 90f * 90f, u = Mathf.Max(0.05f, c.OldPixelWorldSize);
        Vector3 result = p;
        foreach (PixelCoupling k in PixelCoupling.All)
        {
            if (k == null || (!k.IsIn && !k.IsOut)) continue;
            Vector3 end = k.PortWorld + Vector3.up * 0.55f * u + k.FacingWorld * 0.35f * u;
            Vector3 sp = cam.WorldToScreenPoint(end);
            if (sp.z <= 0f) continue;
            float d = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            if (d < best) { best = d; result = end; snapped = true; }
        }
        return result;
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

    /// <summary>The hose arches from one coupling to the other: up, over and down (a cubic curve).</summary>
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
        if (source == null) return "No source: start the hose on an OUT coupling of a Storage Crate or Quantum Organizer";
        if (target == null) return "No target: end it on an IN coupling (Recycler, crate, organizer, trash can...)";
        if (Room() <= 0 && carrier.Count > 0) return "Waiting: the target is full";
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
        if (source != null && source.Device == null) source = null;
        if (target != null && target.Device == null) target = null;
        if ((source == null || target == null) && Time.time >= nextRelatch) { nextRelatch = Time.time + 0.5f; Latch(); }   // (a coupling put back / loaded after the hose)
        UpdateStatus();
        // The ends follow their couplings (devices carried by the handles, turned...).
        Vector3 na = EndAt(source, a), nb = EndAt(target, b);
        if ((na - a).sqrMagnitude > 0.0004f || (nb - b).sqrMagnitude > 0.0004f) { a = na; b = nb; Rebuild(); }
    }

    /// <summary>How many more husks the target takes right now (minus what is already in the tube's last stretch).</summary>
    private int Room()
    {
        if (target == null) return 0;
        return target.Device is IPixelHuskRoom room ? room.HuskRoom(target.Socket) : 6;
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
        bool got = ((IPixelHuskSource)source.Device).TakeHuskOut(source.Socket, out int tier, out double amount);
        pumpedNothing = !got;
        if (!got) return;
        if (!clicker.SpawnStoredPixel(tier, amount, path[0], Vector3.zero)) return;   // (a normal husk: a 'streamed' one passes through walls until it lands)
        IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        Rigidbody rb = list.Count > 0 ? list[list.Count - 1] : null;
        if (rb != null) carrier.Add(rb, 0f);
    }

    /// <summary>The husk reached the far end: the target coupling hands it to its device, or (refused / no target) it drops out of the end.</summary>
    private void Deliver(Rigidbody rb)
    {
        if (rb == null) return;
        rb.isKinematic = true;
        if (target != null && target.TakeIn(rb)) return;
        rb.isKinematic = false;
        rb.position = b;
        PixelPathCarrier.SetVelocity(rb, Vector3.down * 1.5f * unit);
    }

    private void OnDestroy()
    {
        carrier.ReleaseAll();   // what was in the hose falls out
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
