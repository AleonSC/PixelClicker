// ---- System notes ----
// PixelSortingHopper.cs
//   SORTING HOPPER (PixelConsumables DeviceKind.SortingHopper 22, Utilities; design: docs/GameDesign.md section 6, 'The early husk setup'):
//   the Recycler's early partner. A wide, slightly sloped tray on legs (built here by Place / CreateGhost / CreateDisplay with PixelFactoryKit;
//   the placed device is the permanent PixelChipperDevice stub on the same object - bought, kept, placed, saved, removed like every device).
//   - SNAPS ONTO A RECYCLER'S BACK: placed within 'radius' (the Device entry's Reach, 6 world units) of a free Recycler it latches on behind it
//     (TrySnap, also while placing: the ghost jumps there), turned like the bin, scaled to its Bin Size (PixelRecycler.SizeFactor), its exit
//     edge at the bin's back (BackEdgeLocalZ). It follows the bin when the bin is carried, and keeps the bin's back hatch open (SetHatchOpen).
//     One hopper per Recycler. When the Recycler's time runs out the hopper stays where it is and latches onto the next free Recycler that
//     stands within reach (checked every half second).
//   - THE FILTER: one husk type (filterTier), shown by the coloured flag and the little spinning block on its pole. Click the flag (or scroll
//     over it) to step through the types of the husks lying IN THE TRAY right now (TrayTypes; the current one stays in the list). With nothing
//     chosen yet, the first husk that lands in the tray sets it. Hovering the flag shows a small card (PixelBlockTooltip.ShowFor: the block,
//     its name and what the filter does); there is no floating text over the hopper. The flag and the eject button use
//     PixelPointerClaim.ClaimButton (the cube and Pixel Grabbing leave that click alone). Saved in PlacedState.extra ('F,Type').
//   - EJECT BUTTON (orange, on the right rim): click it and every husk lying in the tray is popped out over the back wall (away from the
//     Recycler), each thrown just high and far enough to clear it (Eject, ejectClearance). Stat hopper.ejected.
//   - FEEDING: every feedInterval (0.35 s), while the Recycler can take a husk (HoseCanTake, not working or carried, hatch open), the husk of the
//     filter type lying in the tray nearest the exit is RESERVED (PixelHuskHolder.Reserve: kinematic, timer held, no other machine takes it) and
//     slid over the exit lip, through the hatch and let fall into the bin (Slide) - a steady same-type stream, so the Recycler's streak builds.
//     The bin's own Capture takes it from there.
//   - EVERY OTHER HUSK just lies in the tray and fades like any husk (no keep-alive): the wrong colours take room until they go - that is the loss.
//   - WITHOUT A RECYCLER the exit stays shut (nothing slides out): it is just a tray. The flag's card says so.
//   Unlock: vendorOnly + shopSkillId 'standard' / level 3 = it appears in the shop at Standard level 3 (no vendor sells it); bought for White.
//   SOCKETS (PixelSockets.cs): it OCCUPIES its Recycler's 'hatch' socket while latched (and will not latch onto a Recycler with a coupling on the hatch); its own 'back'
//   socket (In) takes belts / connectors / hoses: AcceptHusk drops the husk into the tray (refused when trayRoom husks lie there).
//   Stat hopper.husks. Tip device_SortingHopper.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelSortingHopper : MonoBehaviour, IPixelSocketDevice, IPixelHuskRoom, IPixelStrayTarget
{
    [Header("Sorting Hopper")]
    [Tooltip("Seconds between two husks of the chosen type slid into the Recycler (a steady stream).")]
    [SerializeField] private float feedInterval = 0.35f;
    [Tooltip("Seconds one husk takes to slide from the tray over the exit lip and through the Recycler's back hatch.")]
    [SerializeField] private float slideSeconds = 0.45f;
    [Tooltip("How hard a husk is pushed into the bin when it leaves the hatch (world units per second).")]
    [SerializeField] private float dropPush = 1.2f;
    [Tooltip("Eject button: how far (in husk widths) above the back wall the ejected husks' arc tops out.")]
    [SerializeField] private float ejectClearance = 0.8f;
    [Tooltip("Colour the eject button and the flag brighten towards while the pointer is on them.")]
    [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.55f, 1f);

    private static readonly List<PixelSortingHopper> all = new List<PixelSortingHopper>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private PixelPlacedDevice device;
    private PixelRecycler recycler;          // the bin it is latched onto (null = none: the exit stays shut)
    private float snapRange = 6f;
    private Vector3 baseScale = Vector3.one;
    private int filterTier = -1;
    private Transform flagTop;
    private Renderer flagRenderer;
    private GameObject flagModel;
    private Transform ejectButton;
    private Collider ejectCollider;
    private Renderer ejectRenderer;
    private Vector3 ejectScale;
    private float ejectPress;               // 0..1: the button dips when pressed
    private float tick, nextFeed, nextLook;
    private Rigidbody sliding;               // the husk on its way into the bin (one at a time)

    // ---- Building ----

    // The tray, in husk widths (u): width, depth, floor height at the exit and at the back, wall height, the exit gap's width.
    private const float WidthHusks = 5.5f, DepthHusks = 4.5f, ExitFloorHusks = 1f, BackFloorHusks = 1.5f, WallHusks = 1f, GapHusks = 1.7f;
    private static readonly Color BodyColor = new Color(0.3f, 0.48f, 0.6f, 1f), Metal = new Color(0.72f, 0.75f, 0.78f, 1f), Dark = new Color(0.12f, 0.14f, 0.15f, 1f);
    private static readonly Color Stripe = new Color(0.35f, 1f, 0.55f, 1f), EjectColor = new Color(1f, 0.55f, 0.15f, 1f);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    /// <summary>Builds a placed hopper at 'point' (with its behaviour). 'reach' = how far from a Recycler's back it latches on.</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw, float reach)
    {
        GameObject root = Build(clicker, false, true);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelSortingHopper h = root.AddComponent<PixelSortingHopper>();
        h.Init(clicker, reach);
        return root;
    }

    /// <summary>A see-through copy that follows the mouse while placing.</summary>
    public static GameObject CreateGhost(PixelClicker clicker) => Build(clicker, true, false);

    /// <summary>A model for the shop / inventory pictures (no colliders, no behaviour).</summary>
    public static GameObject CreateDisplay(PixelClicker clicker) => Build(clicker, false, false);

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders)
    {
        float u = Unit(clicker);
        float w = WidthHusks * u, d = DepthHusks * u, yE = ExitFloorHusks * u, yB = BackFloorHusks * u, wh = WallHusks * u, gap = GapHusks * u;
        float t = 0.2f * u, ft = 0.2f * u;
        PixelFactoryKit kit = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Sorting Hopper (Preview)" : "Sorting Hopper");
        Transform tr = root.transform;

        // The tray floor: sloping down towards the exit (-z, the Recycler's side), so husks gather at the gate.
        float slope = Mathf.Atan2(yB - yE, d) * Mathf.Rad2Deg, len = Mathf.Sqrt(d * d + (yB - yE) * (yB - yE));
        kit.Box("Tray Floor", tr, new Vector3(0f, (yE + yB) * 0.5f - ft * 0.5f, 0f), new Vector3(w, ft, len), BodyColor, new Vector3(-slope, 0f, 0f), colliders, 0.3f, 0.5f);
        // Side and back walls.
        float sideBottom = yE - ft, sideTop = yB + wh;
        kit.Box("Wall Left", tr, new Vector3(-(w + t) * 0.5f, (sideBottom + sideTop) * 0.5f, 0f), new Vector3(t, sideTop - sideBottom, d), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Right", tr, new Vector3((w + t) * 0.5f, (sideBottom + sideTop) * 0.5f, 0f), new Vector3(t, sideTop - sideBottom, d), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Back", tr, new Vector3(0f, (yB - ft + sideTop) * 0.5f, (d + t) * 0.5f), new Vector3(w + 2f * t, sideTop - (yB - ft), t), BodyColor, default, colliders, 0.3f, 0.55f);
        // The exit wall on the Recycler's side: two pieces round the gap, and a low lip across the gap (husks pile against it; the chosen ones are slid over).
        float frontTop = yE + wh * 0.8f, sideW = (w + 2f * t - gap) * 0.5f, zf = -(d + t) * 0.5f;
        kit.Box("Wall Front L", tr, new Vector3(-(gap + sideW) * 0.5f, (sideBottom + frontTop) * 0.5f, zf), new Vector3(sideW, frontTop - sideBottom, t), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Front R", tr, new Vector3((gap + sideW) * 0.5f, (sideBottom + frontTop) * 0.5f, zf), new Vector3(sideW, frontTop - sideBottom, t), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Exit Lip", tr, new Vector3(0f, yE + u * 0.15f, zf), new Vector3(gap, u * 0.3f + ft, t), Metal, default, colliders, 0.85f, 0.7f);
        kit.Box("Exit Chute", tr, new Vector3(0f, yE - ft * 0.5f, zf - t * 1.2f), new Vector3(gap, ft * 0.6f, t * 1.6f), Metal, default, false, 0.85f, 0.7f);
        // Metal rim along the top edges and a green stripe (the Recycler's glow colour) along the sides.
        kit.Box("Rim Left", tr, new Vector3(-(w + t) * 0.5f, sideTop + t * 0.15f, 0f), new Vector3(t * 1.4f, t * 0.3f, d + t), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Right", tr, new Vector3((w + t) * 0.5f, sideTop + t * 0.15f, 0f), new Vector3(t * 1.4f, t * 0.3f, d + t), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Back", tr, new Vector3(0f, sideTop + t * 0.15f, (d + t) * 0.5f), new Vector3(w + 2f * t, t * 0.3f, t * 1.4f), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Stripe Left", tr, new Vector3(-(w + 2f * t) * 0.5f - t * 0.05f, (yE + yB) * 0.5f + wh * 0.4f, 0f), new Vector3(t * 0.1f, t * 0.6f, d), Stripe, default, false, 0f, 0.8f, Stripe);
        kit.Box("Stripe Right", tr, new Vector3((w + 2f * t) * 0.5f + t * 0.05f, (yE + yB) * 0.5f + wh * 0.4f, 0f), new Vector3(t * 0.1f, t * 0.6f, d), Stripe, default, false, 0f, 0.8f, Stripe);
        // Four legs down to the floor (the back ones longer: the tray slopes).
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float top = sz < 0 ? yE - ft : yB - ft;
                kit.Box("Leg", tr, new Vector3(sx * (w * 0.5f - t), top * 0.5f, sz * (d * 0.5f - t)), new Vector3(t * 1.2f, top, t * 1.2f), Metal, default, false, 0.85f, 0.7f);
                kit.Box("Foot", tr, new Vector3(sx * (w * 0.5f - t), t * 0.1f, sz * (d * 0.5f - t)), new Vector3(t * 2f, t * 0.2f, t * 2f), Dark, default, false, 0.5f, 0.6f);
            }
        // The EJECT button on the right rim (orange, on a dark base): pops every husk in the tray out over the back wall.
        kit.Cyl("Eject Base", tr, new Vector3((w + t) * 0.5f, sideTop + t * 0.35f, d * 0.15f), u * 0.32f, t * 0.6f, Dark);
        kit.Cyl("Eject Button", tr, new Vector3((w + t) * 0.5f, sideTop + t * 0.9f, d * 0.15f), u * 0.22f, t * 0.8f, EjectColor, default, 0.1f, 0.6f, colliders);
        // The filter flag on a pole at the back-left corner: tinted the chosen type's colour, with a little block of that type spinning on top.
        float poleH = u * 2.2f;
        Vector3 poleBase = new Vector3(-(w + t) * 0.5f, sideTop, (d + t) * 0.5f);
        kit.Box("Flag Pole", tr, poleBase + new Vector3(0f, poleH * 0.5f, 0f), new Vector3(t * 0.5f, poleH, t * 0.5f), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Flag", tr, poleBase + new Vector3(u * 0.55f, poleH - u * 0.35f, 0f), new Vector3(u * 1f, u * 0.6f, t * 0.25f), Color.white, default, false, 0.1f, 0.5f);
        GameObject flagTop = new GameObject("Flag Top");
        flagTop.transform.SetParent(tr, false);
        flagTop.transform.localPosition = poleBase + new Vector3(0f, poleH + u * 0.45f, 0f);
        return root;
    }

    private void Init(PixelClicker owner, float reach)
    {
        clicker = owner;
        snapRange = Mathf.Max(0.5f, reach);
        float su = Unit(clicker);
        sockets = new[] { new PixelSocket("back", new Vector3(0f, 0f, DepthHusks * su * 0.5f + 0.35f * su), Vector3.forward, PixelSocketFlow.Intake) };
        baseScale = transform.localScale;
        flagTop = transform.Find("Flag Top");
        Transform flag = transform.Find("Flag");
        flagRenderer = flag != null ? flag.GetComponent<Renderer>() : null;
        ejectButton = transform.Find("Eject Button");
        ejectCollider = ejectButton != null ? ejectButton.GetComponent<Collider>() : null;
        ejectRenderer = ejectButton != null ? ejectButton.GetComponent<Renderer>() : null;
        if (ejectButton != null) ejectScale = ejectButton.localScale;
        SetFilter(-1);   // (nothing chosen: the first husk that lands in the tray sets it)
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); PixelCouplings.Register(this); }
    private void OnDisable() { all.Remove(this); PixelCouplings.Unregister(this); }

    // ---- Socket (PixelSockets.cs): the BACK wall, an In socket - a belt / connector / hose ending at a coupling there drops husks into the tray ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => BodyColor;

    [Tooltip("The most husks lying in the tray before the back coupling refuses more (a belt there then throws them on).")]
    [SerializeField] private int trayRoom = 12;

    public int HuskRoom(PixelSocket socket)
    {
        int n = 0;
        IReadOnlyList<Rigidbody> pixels = clicker != null ? clicker.OldPixels : null;
        if (pixels != null) for (int i = 0; i < pixels.Count; i++) if (pixels[i] != null && InTray(pixels[i].position, out _)) n++;
        return Mathf.Max(0, Mathf.Max(1, trayRoom) - n);
    }

    /// <summary>A husk comes in through the back coupling: it drops into the tray near the back wall and slides down like any other. Refused when the tray is full.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk)
    {
        if (husk == null || clicker == null || HuskRoom(socket) <= 0) return false;
        float u = Unit(clicker);
        husk.isKinematic = false;
        husk.position = transform.TransformPoint(new Vector3(0f, (BackFloorHusks + 0.8f) * u, DepthHusks * u * 0.3f));
        PixelHuskHolder.SetVelocity(husk, transform.TransformDirection(Vector3.back) * 0.8f);
        return true;
    }

    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;

    // (PixelHuskPool: the husk is going away and may be reused - let go of it; the Slide coroutine sees it and stops)
    private void OnHuskReleasing(Rigidbody body)
    {
        if (body == sliding) sliding = null;
    }

    private void OnDestroy()
    {
        PixelHuskPool.HuskReleasing -= OnHuskReleasing;
        PixelCouplings.Unregister(this);
        if (recycler != null) PixelCouplings.Vacate(recycler, PixelRecycler.HatchSocket, this);
        if (sliding != null) PixelHuskHolder.Unreserve(sliding, Vector3.zero);   // (a husk half way through the hatch is let fall)
        sliding = null;
    }

    // ---- Snapping onto a Recycler ----

    /// <summary>Is this Recycler free for a hopper (none latched on, not ending)? 'except' = the asking hopper.</summary>
    private static bool Free(PixelRecycler r, PixelSortingHopper except)
    {
        if (r == null || Ending(r) || PixelCoupling.On(r, PixelRecycler.HatchSocket) != null) return false;   // (a coupling on the hatch takes the hopper's place)
        foreach (PixelSortingHopper h in all) if (h != null && h != except && h.recycler == r) return false;
        return true;
    }

    private static bool Ending(PixelRecycler r)
    {
        PixelPlacedDevice d = r.GetComponent<PixelPlacedDevice>();
        return d != null && d.IsEnding;
    }

    /// <summary>Where a hopper sits on this Recycler's back (position, turn and size).</summary>
    private static void SpotOn(PixelClicker clicker, PixelRecycler r, out Vector3 point, out Quaternion rotation, out float size)
    {
        float u = Unit(clicker);
        point = r.transform.TransformPoint(new Vector3(0f, 0f, r.BackEdgeLocalZ + (DepthHusks * u + 0.6f * u) * 0.5f));
        rotation = r.transform.rotation;
        size = r.SizeFactor;
    }

    /// <summary>
    /// While placing: the free Recycler whose back spot is within 'reach' of the pointer's floor point. The ghost jumps there (position, turn, size).
    /// False = none near: the hopper is placed where it is (it latches on later if a Recycler is put down near it).
    /// </summary>
    public static bool TrySnap(PixelClicker clicker, Vector3 point, float reach, out Vector3 snapPoint, out float snapYaw, out float size)
    {
        snapPoint = point; snapYaw = 0f; size = 1f;
        PixelRecycler best = Nearest(clicker, point, reach, null);
        if (best == null) return false;
        SpotOn(clicker, best, out snapPoint, out Quaternion rot, out size);
        snapYaw = rot.eulerAngles.y;
        return true;
    }

    private static PixelRecycler Nearest(PixelClicker clicker, Vector3 point, float reach, PixelSortingHopper except)
    {
        PixelRecycler best = null;
        float bestD = reach * reach;
        foreach (PixelRecycler r in PixelRecycler.All)
        {
            if (!Free(r, except) || r.Fading) continue;
            SpotOn(clicker, r, out Vector3 spot, out _, out _);
            Vector3 dv = spot - point;
            dv.y = 0f;
            if (dv.sqrMagnitude <= bestD) { bestD = dv.sqrMagnitude; best = r; }
        }
        return best;
    }

    // ---- Every frame ----

    private void Update()
    {
        if (clicker == null) return;
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        if (device != null && device.IsEnding) return;   // being removed (shrinking away)
        Camera cam = clicker.ViewCamera;
        if (flagModel != null) flagModel.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

        bool fading = (transform.localScale - baseScale).sqrMagnitude > 0.0001f;   // the floor's props fade (main menu, after a load): wait
        if (fading || PixelCalm.InMenu || Time.timeScale <= 0f) return;

        HandleFlag(cam);
        HandleEject(cam);
        KeepLatched();

        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.1f;
        if (filterTier < 0) { List<int> inTray = TrayTypes(); if (inTray.Count > 0) SetFilter(inTray[0]); }   // (nothing chosen yet: the first type in the tray)
        Feed();
    }

    /// <summary>Latches onto a free Recycler within reach (every half second when it has none), follows it when it is carried, keeps its back hatch open.</summary>
    private void KeepLatched()
    {
        if (recycler != null && (Ending(recycler) || !recycler.isActiveAndEnabled)) { PixelCouplings.Vacate(recycler, PixelRecycler.HatchSocket, this); recycler = null; }
        if (recycler == null)
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + 0.5f;
            recycler = Nearest(clicker, transform.position, snapRange, this);
            if (recycler == null) return;
            PixelCouplings.Occupy(recycler, PixelRecycler.HatchSocket, this);   // (the hatch socket is ours now: no coupling goes there)
            PixelAudio.Play("device_place");
        }
        if (recycler.Fading) return;
        SpotOn(clicker, recycler, out Vector3 spot, out Quaternion rot, out float size);
        if ((transform.position - spot).sqrMagnitude > 0.000001f || Quaternion.Angle(transform.rotation, rot) > 0.05f) transform.SetPositionAndRotation(spot, rot);
        if (Mathf.Abs(baseScale.x - size) > 0.0001f) { baseScale = Vector3.one * size; transform.localScale = baseScale; }
        if (!recycler.Busy && !recycler.HatchIsOpen) recycler.SetHatchOpen(true);
    }

    /// <summary>Slides the next husk of the chosen type into the Recycler, if it can take one.</summary>
    private void Feed()
    {
        if (recycler == null || sliding != null || filterTier < 0 || Time.time < nextFeed) return;
        if (recycler.Busy || recycler.Fading || !recycler.HoseCanTake || !recycler.HatchIsOpen) return;
        Rigidbody pick = null;
        float bestZ = float.MaxValue;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody b = pixels[i];
            if (b == null || PixelHuskQuery.Get(b).Tier != filterTier || !InTray(b.position, out float z)) continue;
            if (!PixelHuskHolder.Takeable(clicker, b)) continue;
            if (z < bestZ) { bestZ = z; pick = b; }   // the one nearest the exit
        }
        if (pick == null) return;
        nextFeed = Time.time + Mathf.Max(0.05f, feedInterval);
        StartCoroutine(Slide(pick, recycler));
    }

    /// <summary>Is this point in the tray (its own units)? 'z' = how far along it, the exit being the lowest.</summary>
    private bool InTray(Vector3 world, out float z)
    {
        float u = Unit(clicker);
        Vector3 p = transform.InverseTransformPoint(world);
        z = p.z;
        return Mathf.Abs(p.x) <= WidthHusks * u * 0.5f && Mathf.Abs(p.z) <= DepthHusks * u * 0.5f
            && p.y >= (ExitFloorHusks - 0.3f) * u && p.y <= (BackFloorHusks + WallHusks + 1f) * u;
    }

    /// <summary>One husk: over the exit lip, through the hatch, and let fall into the bin (its own Capture takes it from there).</summary>
    private IEnumerator Slide(Rigidbody body, PixelRecycler bin)
    {
        sliding = body;
        PixelHuskHolder.Reserve(body);
        float u = Unit(clicker);
        Vector3 start = body.position;
        Vector3 lip = transform.TransformPoint(new Vector3(0f, (ExitFloorHusks + 0.6f) * u, -(DepthHusks * u) * 0.5f));
        float length = Mathf.Max(0.1f, slideSeconds), t = 0f;
        while (t < 1f)
        {
            if (body == null) { sliding = null; yield break; }
            if (sliding != body) yield break;   // (it went away mid-slide: PixelHuskPool)
            if (bin == null || bin.Busy || PixelRecycler.Holds(body)) break;   // (the bin went or started working: let it fall where it is)
            t += Time.deltaTime / length;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            // Along three legs: to the lip (first half), out to the hatch, then in behind the back wall.
            Vector3 p = k < 0.5f ? Vector3.Lerp(start, lip, k / 0.5f)
                      : k < 0.75f ? Vector3.Lerp(lip, bin.HatchOutside, (k - 0.5f) / 0.25f)
                      : Vector3.Lerp(bin.HatchOutside, bin.HatchInside, (k - 0.75f) / 0.25f);
            body.transform.position = p;
            yield return null;
        }
        sliding = null;
        if (body == null) yield break;
        Vector3 push = bin != null ? (bin.transform.TransformDirection(Vector3.back) + Vector3.down * 0.5f) * dropPush : Vector3.zero;
        if (PixelRecycler.Holds(body)) { body.isKinematic = false; PixelHuskHolder.SetVelocity(body, push); yield break; }   // (the bin already holds its timer)
        PixelHuskHolder.Unreserve(body, push);
        if (t >= 1f) PixelStats.Count("hopper.husks");
    }

    // ---- The filter flag ----

    /// <summary>The types of the husks lying in the tray right now (in tier order), plus the chosen one: what the flag steps through.</summary>
    private List<int> TrayTypes()
    {
        List<int> list = new List<int>();
        if (clicker == null) return list;
        if (filterTier >= 0) list.Add(filterTier);
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody b = pixels[i];
            if (b == null || !InTray(b.position, out _)) continue;
            PixelHuskQuery.Husk h = PixelHuskQuery.Get(b);
            if (h.Fading || !clicker.IsValidTierIndex(h.Tier) || clicker.Tiers[h.Tier].flyAway || list.Contains(h.Tier)) continue;
            list.Add(h.Tier);
        }
        list.Sort();
        return list;
    }

    /// <summary>Is the pointer within a screen circle round this world point ('radiusWorld' wide)? For the flag (the button uses its collider).</summary>
    private static bool PointerNear(Camera cam, Vector3 centre, float radiusWorld)
    {
        Vector3 sp = cam.WorldToScreenPoint(centre);
        if (sp.z <= 0f) return false;
        Vector3 edge = cam.WorldToScreenPoint(centre + cam.transform.right * radiusWorld);
        float radius = Mathf.Max(20f, Mathf.Abs(edge.x - sp.x));
        return Vector2.Distance(PixelInput.PointerPosition(), new Vector2(sp.x, sp.y)) <= radius;
    }

    /// <summary>Pointer on the flag: claims the click and shows the filter's small card; left-click / scroll steps through the types in the tray.</summary>
    private void HandleFlag(Camera cam)
    {
        bool hot = cam != null && flagTop != null && !PixelInput.PointerOverUI() && !PixelCarryHandles.Hot
                   && PointerNear(cam, flagTop.position - Vector3.up * Unit(clicker) * transform.lossyScale.x * 0.5f, Unit(clicker) * transform.lossyScale.x * 0.9f);
        TintFlag(hot);
        if (!hot) return;
        PixelPointerClaim.ClaimButton();
        if (filterTier >= 0) PixelBlockTooltip.ShowFor(filterTier, FilterNote());
        float scroll = PixelInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.01f) StepFilter(scroll > 0f ? 1 : -1);
        else if (PixelInput.LeftPressed()) StepFilter(1);
    }

    /// <summary>What the flag's card says under the block's name.</summary>
    private string FilterNote()
    {
        int others = TrayTypes().Count - 1;
        string change = others > 0 ? "Click or scroll to pick another type lying in the tray (" + others + " more)." : "Only this type is in the tray - drop others in to choose them.";
        return recycler != null
            ? "<b>Being filtered.</b> Only this type slides into the Recycler, one after another, so its streak grows. Other husks stay in the tray and fade.\n<color=#8f9bb5>" + change + "</color>"
            : "<b>Being filtered</b> - but there is no Recycler behind the hopper, so its exit is shut.\n<color=#8f9bb5>" + change + "</color>";
    }

    private void StepFilter(int step)
    {
        List<int> options = TrayTypes();
        if (options.Count <= 1) { if (filterTier < 0 && options.Count == 1) SetFilter(options[0]); return; }
        int at = options.IndexOf(filterTier);
        at = at < 0 ? 0 : (at + step + options.Count) % options.Count;
        SetFilter(options[at]);
        PixelAudio.Play("ui_click");
    }

    private void SetFilter(int tier)
    {
        filterTier = clicker != null && clicker.IsValidTierIndex(tier) ? tier : -1;
        if (flagRenderer != null)
        {
            Color c = filterTier >= 0 ? clicker.Tiers[filterTier].UIColor : Color.gray;
            Material m = flagRenderer.material;
            m.color = c;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.35f); }
        }
        if (flagModel != null) Destroy(flagModel);
        flagModel = null;
        if (filterTier >= 0 && flagTop != null) flagModel = clicker.CreateDisplayPixel(filterTier, flagTop, Unit(clicker) * 0.6f);
        if (flagModel != null) flagModel.transform.localPosition = Vector3.zero;
        flagBase = filterTier >= 0 ? clicker.Tiers[filterTier].UIColor : Color.gray;
        flagGlow = -1f;   // (re-tint at once)
    }

    private Color flagBase = Color.gray;
    private float flagGlow;

    /// <summary>The flag brightens while the pointer is on it.</summary>
    private void TintFlag(bool hot)
    {
        float target = hot ? 1f : 0f;
        if (Mathf.Approximately(flagGlow, target) || flagRenderer == null) return;
        flagGlow = flagGlow < 0f ? target : Mathf.MoveTowards(flagGlow, target, Time.unscaledDeltaTime / 0.12f);
        Color c = Color.Lerp(flagBase, hoverColor, flagGlow * 0.35f);
        Material m = flagRenderer.material;
        m.color = c;
        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * (0.35f + 0.6f * flagGlow)); }
    }

    // ---- The eject button ----

    /// <summary>Pointer on the eject button: claims the click (cube and Pixel Grabbing step aside), glows; a click pops the tray's husks out.</summary>
    private void HandleEject(Camera cam)
    {
        if (ejectButton == null) return;
        bool hot = cam != null && ejectCollider != null && !PixelInput.PointerOverUI() && !PixelCarryHandles.Hot
                   && ejectCollider.Raycast(cam.ScreenPointToRay(PixelInput.PointerPosition()), out _, 500f);
        if (hot)
        {
            PixelPointerClaim.ClaimButton();
            if (PixelInput.LeftPressed()) { ejectPress = 1f; Eject(); }
        }
        ejectPress = Mathf.MoveTowards(ejectPress, 0f, Time.unscaledDeltaTime / 0.18f);
        ejectButton.localScale = new Vector3(ejectScale.x, ejectScale.y * (1f - 0.45f * ejectPress), ejectScale.z);
        if (ejectRenderer != null)
        {
            Material m = ejectRenderer.material;
            Color c = hot ? Color.Lerp(EjectColor, hoverColor, 0.4f) : EjectColor;
            if (m.color != c)
            {
                m.color = c;
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", hot ? c * 0.8f : Color.black); }
            }
        }
    }

    /// <summary>Every husk lying in the tray is thrown out over the back wall (away from the Recycler), just high and far enough to clear it.</summary>
    private void Eject()
    {
        float u = Unit(clicker), scale = transform.lossyScale.x, g = Mathf.Max(1f, -Physics.gravity.y);
        float wallTop = (BackFloorHusks + WallHusks) * u, wallBack = DepthHusks * u * 0.5f + 0.2f * u;   // (the back wall, in the hopper's own units)
        int count = 0;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody b = pixels[i];
            if (b == null || b == sliding || b.isKinematic || !InTray(b.position, out _)) continue;
            PixelHuskQuery.Husk h = PixelHuskQuery.Get(b);
            if (h.Fading || h.HeldByPlayer) continue;
            Vector3 local = transform.InverseTransformPoint(b.position);
            // Up so the arc tops out ejectClearance above the wall, and back so it is past the wall at the top of the arc.
            float rise = Mathf.Max(0.2f * u, wallTop + Mathf.Max(0.3f, ejectClearance) * u - local.y) * scale;
            float vy = Mathf.Sqrt(2f * g * rise), timeUp = vy / g;
            float run = Mathf.Max(0.3f * u, wallBack + 0.6f * u - local.z) * scale;
            Vector3 v = transform.TransformDirection(new Vector3(Random.Range(-0.15f, 0.15f) * run / timeUp, 0f, run / timeUp)) + Vector3.up * vy;
            PixelHuskHolder.SetVelocity(b, v);
            count++;
        }
        PixelAudio.Play(count > 0 ? "device_place" : "ui_click");
        if (count > 0) PixelStats.Count("hopper.ejected", count);
    }

    // ---- Saving (PixelConsumables puts this in PlacedState.extra) ----

    /// <summary>The chosen type, as 'F,Type' (by pixel type name, so it survives tier list changes).</summary>
    public string ExportState() => filterTier >= 0 && clicker != null ? "F," + clicker.Tiers[filterTier].type.ToString() : "";

    public void ImportState(string data)
    {
        if (string.IsNullOrEmpty(data) || clicker == null) return;
        string[] f = data.Split(',');
        if (f.Length < 2 || f[0] != "F" || !System.Enum.TryParse(f[1], out PixelClicker.PixelType type)) return;
        int tier = clicker.IndexOf(type);
        if (tier >= 0) SetFilter(tier);
    }
}
