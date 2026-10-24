// ---- System notes ----
// PixelContainerHose.cs
//   CONTAINER HOSE (a Dev Hub > Tester feature, PixelFeatureTester 'Container Hose', placed as a ROUTE: start, any bend points, end): a see-through hose like the
//   Funnel's that joins one device to another THROUGH COUPLINGS (PixelSockets.cs / PixelCoupling.cs, docs/systems/PixelCouplings.md). Click an OUT coupling on a device a
//   hose can pull from (IPixelHuskSource: Storage Crate, Quantum Organizer - the crate's 'out' socket, or an organizer coupling set to Out with its teal dot), then an IN coupling on the target
//   (Recycler back hatch, Storage Crate, Quantum Organizer, Trash Can, Kiln, Sorting Hopper...); clicks magnetize to couplings AND free sockets (Snap). An end on
//   a FREE socket fits a coupling there the way the hose runs (AUTO-FIT, PixelCouplings.FreeSocketAt); when placed, a Both coupling with nothing else attached
//   turns to match. If the player later flips an end's coupling (its teal dot), the hose FOLLOWS (runs the other way) when it can, else it stops and says why. Between them the player clicks any number of
//   BEND points (scroll sets their height; PixelFeatureTester.RegisterRoute) and the hose runs through them as a smooth Catmull-Rom curve (straight with none) - no forced arc.
//   Routed the other way round it runs the right way anyway. An idle hose says why over its middle (IdleReason). A quick RIGHT CLICK on a placed hose (PixelRightTap; a hold
//   removes it) REVERSES its flow (Reverse): only when the far end can hand husks out (a crate / organizer) and the start can take them in; Both couplings flip to match.
//   Every pumpSeconds (0.5 s) it pulls one stored husk out of the source (TakeHuskOut), spawns it at the hose's start (SpawnStoredPixel) and carries it along the tube
//   (PixelPathCarrier, can't be grabbed on the way). At the far end the target coupling hands it to its device (PixelCoupling.TakeIn -> the device's one hook AcceptHusk);
//   refused (full) = it drops out of the end. It only pumps while the target has room (IPixelHuskRoom.HuskRoom; devices without it take 6 at a time), counting what is in
//   the tube, and only takes out husks the target TAKES (IPixelHuskFilter, e.g. the Biomass Generator: wood) when the source can pick (IPixelHuskPicker). The ends follow their couplings (carried / turned devices). A coupling that is removed leaves its end where it was: the hose idles and latches onto a
//   coupling put back there (every half second). Removing the hose lets what is inside it fall out. Saved with the tester save (both end points and the bend points).
//   LOOK (2026-10-24): a see-through RIBBED tube (radiusHusks; ribDepth / ribSpacingHusks: PixelTube with a radius per ring on a finely resampled
//   line) wound with a solid reinforcing WIRE spiral (wireColor, wireTurnHusks: PixelTube.Helix). At each end the coupling wears a FORM-FITTING
//   hose adapter (PixelCoupling.SetHoseFitting: rubber sleeve, a bell flaring out to the hose's width, a clamp band) and the hose starts in its
//   mouth (PixelCoupling.HoseMouthWorld), running straight out of it for legHusks before it bends; the adapter goes when the last hose on it does.
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
    [Header("Look")]
    [Min(0.1f)]
    [Tooltip("The hose's radius, in husk widths (a husk must fit through it).")]
    [SerializeField] private float radiusHusks = 0.58f;
    [Tooltip("Colour of the reinforcing wire spiral round the hose.")]
    [SerializeField] private Color wireColor = new Color(0.88f, 0.92f, 0.96f, 1f);
    [Min(0.1f)]
    [Tooltip("One turn of the wire spiral every this many husk widths along the hose.")]
    [SerializeField] private float wireTurnHusks = 0.55f;
    [Range(0f, 0.2f)]
    [Tooltip("How deep the hose's ribs are (a share of its radius; 0 = smooth).")]
    [SerializeField] private float ribDepth = 0.06f;
    [Min(0.05f)]
    [Tooltip("Distance between two ribs, in husk widths.")]
    [SerializeField] private float ribSpacingHusks = 0.28f;
    [Min(0f)]
    [Tooltip("How far the hose runs straight out of a coupling's adapter before it bends, in husk widths.")]
    [SerializeField] private float legHusks = 0.7f;

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f;
    private Vector3 a, b;                       // the ends (world): just above the two couplings' ports
    private PixelCoupling source, target;       // an Out coupling (the device gives husks) and an In coupling (the device takes them)

    private static readonly List<PixelContainerHose> all = new List<PixelContainerHose>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }
    private void OnEnable() { if (!ghost && !all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    /// <summary>Does a placed hose PULL husks out of this coupling (it is the hose's source)? (Auto-direction couplings: Out.)</summary>
    public static bool Pulls(PixelCoupling c) { foreach (PixelContainerHose h in all) if (h != null && h.source == c) return true; return false; }

    /// <summary>Does a placed hose PUSH husks into this coupling (it is the hose's target)? (Auto-direction couplings: In.)</summary>
    public static bool Pushes(PixelCoupling c) { foreach (PixelContainerHose h in all) if (h != null && h.target == c) return true; return false; }
    private TMPro.TextMeshPro status;
    private string statusText;
    private Mesh mesh, wireMesh;
    private PixelCoupling fittedA, fittedB;   // the couplings that wear this hose's adapter
    private static readonly List<Vector3> denseScratch = new List<Vector3>(), helixScratch = new List<Vector3>();
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
        h.Latch(true);
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

    /// <summary>
    /// Finds the couplings at the two ends: an Out coupling on a device a hose can pull from, and an In coupling (clicked either way round). An end on a free
    /// socket fits a coupling there (auto-fit). 'placing' = the hose was just laid: a Both coupling with nothing else attached may turn to match.
    /// </summary>
    private void Latch(bool placing = false)
    {
        if (source == null && target == null && !CanSourceAt(LowerEnd(a), placing) && CanSourceAt(LowerEnd(b), placing))
        { Vector3 tp = a; a = b; b = tp; bends.Reverse(); }   // (routed the other way round)
        if (source == null) source = SourceAt(LowerEnd(a), placing);
        if (target == null) target = TargetAt(LowerEnd(b), placing);
        if (target != null && target == source) target = null;
    }

    private static bool IsSource(PixelCoupling k) => k != null && k.IsOut && k.Device is IPixelHuskSource;

    private float Reach => 1.5f * unit;

    /// <summary>The coupling at this end (any way), or null.</summary>
    private PixelCoupling CouplingAt(Vector3 p) => PixelCouplings.NearestCoupling(p, Reach, true, true);

    /// <summary>A Both coupling that may turn to match a hose just laid: only while nothing else is attached to it.</summary>
    private static bool MayTurn(PixelCoupling k, bool placing) => placing && k != null && k.Socket.flow == PixelSocketFlow.Both && !k.Auto && PixelCouplings.Attached(k) == PixelLink.Nothing;

    /// <summary>Could this end be the hose's source (an Out coupling / a free socket on a device a hose can pull from)?</summary>
    private bool CanSourceAt(Vector3 p, bool placing)
    {
        PixelCoupling k = CouplingAt(p);
        if (k != null) return IsSource(k) || (MayTurn(k, placing) && k.Device is IPixelHuskSource);
        return PixelCouplings.FreeSocketAt(p, false, out IPixelSocketDevice d, out PixelSocket _, Reach / PixelCouplings.HuskWidth) && d is IPixelHuskSource;
    }

    private PixelCoupling SourceAt(Vector3 p, bool placing)
    {
        PixelCoupling k = CouplingAt(p);
        if (k != null)
        {
            if (!IsSource(k) && MayTurn(k, placing) && k.Device is IPixelHuskSource) k.SetDirection(false);
            return IsSource(k) ? k : null;
        }
        if (!PixelCouplings.FreeSocketAt(p, false, out IPixelSocketDevice d, out PixelSocket _, Reach / PixelCouplings.HuskWidth) || !(d is IPixelHuskSource)) return null;
        return PixelCouplings.AutoFitAt(p, false, Reach / PixelCouplings.HuskWidth);
    }

    private PixelCoupling TargetAt(Vector3 p, bool placing)
    {
        PixelCoupling k = CouplingAt(p);
        if (k != null)
        {
            if (!k.IsIn && MayTurn(k, placing)) k.SetDirection(true);
            return k.IsIn ? k : null;
        }
        return PixelCouplings.AutoFitAt(p, true, Reach / PixelCouplings.HuskWidth);
    }

    /// <summary>Where the hose meets a coupling: in its hose adapter's mouth (or the saved point when there is none).</summary>
    private Vector3 EndAt(PixelCoupling k, Vector3 fallback) => k != null ? k.HoseMouthWorld : fallback;

    /// <summary>The end point brought back down to port height (the saved ends are at the adapter's mouth).</summary>
    private Vector3 LowerEnd(Vector3 p) => p - Vector3.up * PixelCoupling.HoseMouthUp * unit;

    /// <summary>Magnetizes a click to the nearest coupling or free socket on screen (within ~90 px), over its collar.</summary>
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
            Vector3 end = PixelCoupling.HoseMouth(k.PortWorld, k.FacingWorld, u);
            Vector3 sp = cam.WorldToScreenPoint(end);
            if (sp.z <= 0f) continue;
            float d = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude;
            if (d < best) { best = d; result = end; snapped = true; }
        }
        foreach (IPixelSocketDevice dev in PixelCouplings.Devices)   // free sockets too: a coupling is fitted there when the hose is placed
            foreach (PixelSocket s in PixelCouplings.FreeSockets(dev))
            {
                Vector3 end = PixelCoupling.HoseMouth(PixelCouplings.Port(dev, s), PixelCouplings.Facing(dev, s), u);
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
        // The reinforcing wire: a thin solid spiral wound round the see-through hose (its own mesh, a child so it moves with it).
        GameObject wire = new GameObject("Hose Wire", typeof(MeshFilter), typeof(MeshRenderer));
        wire.transform.SetParent(root.transform, false);
        h.wireMesh = new Mesh { name = "Container Hose Wire" };
        wire.GetComponent<MeshFilter>().sharedMesh = h.wireMesh;
        MeshRenderer wr = wire.GetComponent<MeshRenderer>();
        wr.sharedMaterial = new PixelFactoryKit(c, ghostMode, true).Mat(h.wireColor, 0.6f, 0.75f);
        wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return root;
    }

    /// <summary>
    /// The hose runs from one end through every routed point to the other: a smooth curve (Catmull-Rom) through them, straight when there are none.
    /// At a coupling it first runs straight out of the adapter (legHusks along the coupling's facing), so it leaves the fitting cleanly.
    /// The look: a see-through ribbed tube (PixelTube with a rib radius per ring, on a finely resampled copy of the line) wound with a solid wire spiral.
    /// </summary>
    private void Rebuild()
    {
        List<Vector3> pts = new List<Vector3>(bends.Count + 4) { a };
        if (source != null && !ghost) pts.Add(a + source.FacingWorld * legHusks * unit);
        pts.AddRange(bends);
        if (target != null && !ghost) pts.Add(b + target.FacingWorld * legHusks * unit);
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
        float r = radiusHusks * unit;
        Vector3[] dense = Resample(path, 0.07f * unit);
        float rib = Mathf.Max(0.05f, ribSpacingHusks) * unit, depth = ribDepth;
        float[] along = PixelPathCarrier.Cumulative(dense);
        PixelTube.Build(dense, r, 16, mesh, null, i => 1f - depth * 0.5f + depth * 0.5f * Mathf.Cos(along[i] / rib * Mathf.PI * 2f));
        mesh.RecalculateBounds();
        PixelTube.Helix(dense, r * (1f + depth * 0.5f) + 0.02f * unit, Mathf.Max(0.1f, wireTurnHusks) * unit, helixScratch);
        PixelTube.Build(helixScratch.ToArray(), 0.035f * unit, 6, wireMesh);
        if (!ghost) RefreshFittings();
    }

    /// <summary>The line again with points about 'step' apart (the ribs and the wire spiral need many points; the husks ride the coarse line).</summary>
    private static Vector3[] Resample(Vector3[] line, float step)
    {
        denseScratch.Clear();
        if (line == null || line.Length == 0) return denseScratch.ToArray();
        denseScratch.Add(line[0]);
        for (int i = 1; i < line.Length; i++)
        {
            Vector3 p = line[i - 1], q = line[i];
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p, q) / Mathf.Max(0.01f, step)));
            for (int k = 1; k <= n; k++) denseScratch.Add(Vector3.Lerp(p, q, k / (float)n));
        }
        return denseScratch.ToArray();
    }

    /// <summary>The couplings at both ends wear this hose's form-fitting adapter (PixelCoupling.SetHoseFitting); one it left gets its plain look back.</summary>
    private void RefreshFittings()
    {
        float r = radiusHusks * unit;
        if (fittedA != null && fittedA != source && fittedA != target) Unfit(fittedA);
        if (fittedB != null && fittedB != source && fittedB != target) Unfit(fittedB);
        fittedA = source; fittedB = target;
        if (source != null) source.SetHoseFitting(true, r);
        if (target != null) target.SetHoseFitting(true, r);
    }

    /// <summary>Takes a coupling's adapter off unless another hose still ends on it.</summary>
    private void Unfit(PixelCoupling c)
    {
        if (c == null) return;
        foreach (PixelContainerHose h in all) if (h != null && h != this && (h.source == c || h.target == c)) return;
        c.SetHoseFitting(false);
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
        if (source == null) return "No source: start the hose on an OUT coupling / free port of a Storage Crate or Quantum Organizer";
        if (target == null) return "No target: end it on an IN coupling / free port (Recycler, crate, organizer, trash can...)";
        if (Room() <= 0 && carrier.Count > 0) return "Waiting: the target is full";
        if (carrier.Count == 0 && pumpedNothing) return source.Device is IPixelHuskPicker && target.Device is IPixelHuskFilter ? "Nothing the target takes in the source" : "The source is empty";
        return "";
    }

    private bool pumpedNothing;
    private System.Predicate<int> wanted;   // "does the target take this tier?" (made once)

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
        FollowFlips();
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

    /// <summary>
    /// The player switched an end's coupling (its teal dot): the hose FOLLOWS - runs the other way - when the other end can work that way, else it lets go of
    /// that end and idles (it latches again once the coupling is switched back).
    /// </summary>
    private void FollowFlips()
    {
        bool sourceFlipped = source != null && !source.IsOut, targetFlipped = target != null && !target.IsIn;
        if (!sourceFlipped && !targetFlipped) return;
        if (source != null && target != null && sourceFlipped != targetFlipped)
        {
            bool ok = sourceFlipped ? target.Socket.CanGiveOut && target.Device is IPixelHuskSource : source.Socket.CanTakeIn;
            if (ok) { Reverse(); return; }
        }
        if (sourceFlipped) { source = null; carrier.ReleaseAll(); }
        if (targetFlipped) target = null;
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
        if (wanted == null) wanted = tier => target != null && PixelCouplings.Takes(target.Device, target.Socket, tier);
        int tier; double amount;
        bool got = source.Device is IPixelHuskPicker picker
            ? picker.TakeHuskOut(source.Socket, wanted, out tier, out amount)   // only a husk the target takes (a mixed crate into a Biomass Generator: the wood)
            : ((IPixelHuskSource)source.Device).TakeHuskOut(source.Socket, out tier, out amount);
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
        if (wireMesh != null) Destroy(wireMesh);
        if (!ghost) { Unfit(fittedA); if (fittedB != fittedA) Unfit(fittedB); }
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
