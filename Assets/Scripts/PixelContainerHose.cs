// ---- System notes ----
// PixelContainerHose.cs
//   CONTAINER HOSE (a Dev Hub > Tester feature, PixelFeatureTester 'Container Hose', placed as a ROUTE: start, any bend points, end): a see-through hose like the
//   Funnel's that joins one device to another THROUGH COUPLINGS (PixelSockets.cs / PixelCoupling.cs, docs/systems/PixelCouplings.md). Click an OUT coupling on a device a
//   hose can pull from (IPixelHuskSource: Storage Crate, Quantum Organizer - the crate's 'out' socket, or an organizer coupling set to Out with its teal dot), then an IN coupling on the target
//   (Recycler back hatch, Storage Crate, Quantum Organizer, Trash Can, Kiln, Sorting Hopper...); coupling clicks magnetize (Snap). Between them the player clicks any number of
//   BEND points (scroll sets their height; PixelFeatureTester.RegisterRoute) and the hose runs through them as a smooth Catmull-Rom curve (straight with none) - no forced arc.
//   Routed the other way round it runs the right way anyway. An idle hose says why over its middle (IdleReason). A quick RIGHT CLICK on a placed hose (PixelRightTap; a hold
//   removes it) REVERSES its flow (Reverse): only when the far end can hand husks out (a crate / organizer) and the start can take them in; Both couplings flip to match.
//   Every pumpSeconds (0.5 s) it pulls one stored husk out of the source (TakeHuskOut), spawns it at the hose's start (SpawnStoredPixel) and carries it along the tube
//   (PixelPathCarrier, can't be grabbed on the way). At the far end the target coupling hands it to its device (PixelCoupling.TakeIn -> the device's one hook AcceptHusk);
//   refused (full) = it drops out of the end. It only pumps while the target has room (IPixelHuskRoom.HuskRoom; devices without it take 6 at a time), counting what is in
//   the tube. The ends follow their couplings (carried / turned devices). A coupling that is removed leaves its end where it was: the hose idles and latches onto a
//   coupling put back there (every half second). Removing the hose lets what is inside it fall out. Saved with the tester save (both end points and the bend points).
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
    private readonly List<Vector3> bends = new List<Vector3>();   // the points the player routed the hose through (world), between the two ends
    private readonly PixelRightTap flipTap = new PixelRightTap();

    // ------------------------------------------------------------------
    // Route feature hooks (PixelFeatureTester.RegisterRoute): the hose follows the points the player clicks
    // ------------------------------------------------------------------

    public static GameObject CreatePreview(PixelClicker c) => Build(c, true, "Container Hose (Preview)");

    /// <summary>While routing: the hose through the points clicked so far and the mouse.</summary>
    public static void UpdatePreview(GameObject preview, IReadOnlyList<Vector3> points)
    {
        PixelContainerHose h = preview != null ? preview.GetComponent<PixelContainerHose>() : null;
        if (h == null || points == null || points.Count < 2) { if (preview != null) preview.SetActive(false); return; }
        preview.SetActive(true);
        h.SetRoute(points);
        h.Rebuild();
    }

    /// <summary>A placed hose along the clicked points: the first and last latch onto the couplings there (an Out one to pull from, an In one to feed).</summary>
    public static GameObject Place(PixelClicker c, IReadOnlyList<Vector3> points)
    {
        if (points == null || points.Count < 2) return null;
        GameObject root = Build(c, false, "Container Hose");
        PixelContainerHose h = root.GetComponent<PixelContainerHose>();
        h.SetRoute(points);
        h.Latch();
        h.a = h.EndAt(h.source, h.a);
        h.b = h.EndAt(h.target, h.b);
        h.Rebuild();
        PixelAudio.Play("device_place");
        return root;
    }

    private void SetRoute(IReadOnlyList<Vector3> points)
    {
        a = points[0];
        b = points[points.Count - 1];
        bends.Clear();
        for (int i = 1; i < points.Count - 1; i++) bends.Add(points[i]);
    }

    /// <summary>Finds the couplings at the two ends: an Out coupling on a device a hose can pull from, and an In coupling (clicked either way round).</summary>
    private void Latch()
    {
        float reach = 1.5f * unit;
        PixelCoupling first = PixelCouplings.NearestCoupling(LowerEnd(a), reach, true, true), second = PixelCouplings.NearestCoupling(LowerEnd(b), reach, true, true);
        if (second == first) second = null;
        if (!IsSource(first) && IsSource(second)) { PixelCoupling t = first; first = second; second = t; Vector3 tp = a; a = b; b = tp; bends.Reverse(); }   // (routed the other way round)
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

    /// <summary>The hose runs from one end through every routed point to the other: a smooth curve (Catmull-Rom) through them, straight when there are none.</summary>
    private void Rebuild()
    {
        List<Vector3> pts = new List<Vector3>(bends.Count + 2) { a };
        pts.AddRange(bends);
        pts.Add(b);
        List<Vector3> line = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            int n = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(p1, p2) / (0.4f * unit)), 2, 24);
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                line.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        line.Add(b);
        path = line.ToArray();
        cum = PixelPathCarrier.Cumulative(path);
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        PixelTube.Build(path, 0.62f * unit, 14, mesh);
        mesh.RecalculateBounds();
    }

    // ------------------------------------------------------------------
    // Right click: run the other way
    // ------------------------------------------------------------------

    /// <summary>Is the pointer on the hose (within a few screen pixels of its line)?</summary>
    private bool PointerOnHose(Camera cam)
    {
        if (path == null || cam == null) return false;
        Vector2 mouse = PixelInput.PointerPosition();
        float best = 16f * 16f;
        Vector3 prev = cam.WorldToScreenPoint(path[0]);
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 cur = cam.WorldToScreenPoint(path[i]);
            if (prev.z > 0f && cur.z > 0f)
            {
                Vector2 p = prev, q = cur, d = q - p;
                float t = d.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector2.Dot(mouse - p, d) / d.sqrMagnitude) : 0f;
                if ((p + d * t - mouse).sqrMagnitude < best) return true;
            }
            prev = cur;
        }
        return false;
    }

    /// <summary>
    /// A quick right click on the hose turns its flow round: the target end becomes the source and the other way. Couplings on a crate / organizer (Both) are
    /// switched to match; when an end cannot work the other way (the Kiln only takes husks in, a source needs storage), it says so and stays. What is in the tube falls out.
    /// </summary>
    private void Reverse()
    {
        if (source == null || target == null) { PixelHints.Announce("Connect both ends of the hose to couplings first"); return; }
        bool newSourceOk = target.Socket.CanGiveOut && target.Device is IPixelHuskSource, newTargetOk = source.Socket.CanTakeIn;
        if (!newSourceOk || !newTargetOk)
        {
            PixelHints.Announce(!newSourceOk ? "The hose can't run the other way: its far end can't hand husks out (only a Storage Crate or Quantum Organizer can)" : "The hose can't run the other way: its start can't take husks in");
            return;
        }
        carrier.ReleaseAll();
        PixelCoupling oldSource = source;
        source = target; target = oldSource;
        source.SetDirection(false);   // (an organizer coupling flips to Out, the other to In; fixed sockets keep theirs)
        target.SetDirection(true);
        Vector3 t = a; a = b; b = t;
        bends.Reverse();
        Rebuild();
        PixelAudio.Play("ui_click");
        PixelHints.Announce("Hose reversed: it now pulls from the other end");
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
        // A quick right click on the hose (not a hold - that removes it) turns its flow round.
        Camera cam = clicker.ViewCamera;
        bool hovering = !PixelCalm.InMenu && !PixelFirstPerson.Active && !PixelInput.PointerOverUI() && PixelRightClick.Current != PixelRightClick.Owner.Tool && PointerOnHose(cam);
        if (flipTap.Update(hovering ? this : null) && hovering) Reverse();
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
    private class Save { public Vector3 a, b; public List<Vector3> bends; }

    public static string ExportState(GameObject root)
    {
        PixelContainerHose h = root != null ? root.GetComponent<PixelContainerHose>() : null;
        return h == null ? null : JsonUtility.ToJson(new Save { a = h.a, b = h.b, bends = new List<Vector3>(h.bends) });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        List<Vector3> pts = new List<Vector3> { s.a };
        if (s.bends != null) pts.AddRange(s.bends);
        pts.Add(s.b);
        return Place(c, pts);
    }
}
