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
//     over it) to step through the unlocked types (PixelPointerClaim keeps the cube out of that click). Saved in PlacedState.extra ('F,Type').
//   - FEEDING: every feedInterval (0.35 s), while the Recycler can take a husk (HoseCanTake, not working or carried, hatch open), the husk of the
//     filter type lying in the tray nearest the exit is RESERVED (PixelHuskHolder.Reserve: kinematic, timer held, no other machine takes it) and
//     slid over the exit lip, through the hatch and let fall into the bin (Slide) - a steady same-type stream, so the Recycler's streak builds.
//     The bin's own Capture takes it from there.
//   - EVERY OTHER HUSK just lies in the tray and fades like any husk (no keep-alive): the wrong colours take room until they go - that is the loss.
//   - WITHOUT A RECYCLER the exit stays shut (nothing slides out): it is just a tray. The label says so.
//   Unlock: vendorOnly + shopSkillId 'standard' / level 3 = it appears in the shop at Standard level 3 (no vendor sells it); bought for White.
//   Stat hopper.husks. Tip device_SortingHopper.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PixelSortingHopper : MonoBehaviour
{
    [Header("Sorting Hopper")]
    [Tooltip("Seconds between two husks of the chosen type slid into the Recycler (a steady stream).")]
    [SerializeField] private float feedInterval = 0.35f;
    [Tooltip("Seconds one husk takes to slide from the tray over the exit lip and through the Recycler's back hatch.")]
    [SerializeField] private float slideSeconds = 0.45f;
    [Tooltip("How hard a husk is pushed into the bin when it leaves the hatch (world units per second).")]
    [SerializeField] private float dropPush = 1.2f;
    [Tooltip("Colour of the label above the hopper.")]
    [SerializeField] private Color labelColor = new Color(0.85f, 0.95f, 1f, 1f);

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
    private TextMeshPro label;
    private float tick, nextFeed, nextLook;
    private Rigidbody sliding;               // the husk on its way into the bin (one at a time)

    // ---- Building ----

    // The tray, in husk widths (u): width, depth, floor height at the exit and at the back, wall height, the exit gap's width.
    private const float WidthHusks = 5.5f, DepthHusks = 4.5f, ExitFloorHusks = 1f, BackFloorHusks = 1.5f, WallHusks = 1f, GapHusks = 1.7f;
    private static readonly Color BodyColor = new Color(0.3f, 0.48f, 0.6f, 1f), Metal = new Color(0.72f, 0.75f, 0.78f, 1f), Dark = new Color(0.12f, 0.14f, 0.15f, 1f);
    private static readonly Color Stripe = new Color(0.35f, 1f, 0.55f, 1f);

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
        baseScale = transform.localScale;
        flagTop = transform.Find("Flag Top");
        Transform flag = transform.Find("Flag");
        flagRenderer = flag != null ? flag.GetComponent<Renderer>() : null;
        float u = Unit(clicker);

        GameObject lg = new GameObject("Label");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, (BackFloorHusks + WallHusks) * u + u * 1.6f, 0f);
        label = lg.AddComponent<TextMeshPro>();
        label.fontSize = 3.2f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = labelColor;
        label.rectTransform.sizeDelta = new Vector2(12f, 3f);
        if (clicker != null && clicker.UIFont != null) label.font = clicker.UIFont;

        List<int> options = FilterOptions();
        SetFilter(options.Count > 0 ? options[0] : -1);
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    private void OnDestroy()
    {
        if (sliding != null) PixelHuskHolder.Unreserve(sliding, Vector3.zero);   // (a husk half way through the hatch is let fall)
        sliding = null;
    }

    // ---- Snapping onto a Recycler ----

    /// <summary>Is this Recycler free for a hopper (none latched on, not ending)? 'except' = the asking hopper.</summary>
    private static bool Free(PixelRecycler r, PixelSortingHopper except)
    {
        if (r == null || Ending(r)) return false;
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
        if (label != null && cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, cam.transform.up);
        if (flagModel != null) flagModel.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

        bool fading = (transform.localScale - baseScale).sqrMagnitude > 0.0001f;   // the floor's props fade (main menu, after a load): wait
        if (fading || PixelCalm.InMenu || Time.timeScale <= 0f) return;

        HandleFlag(cam);
        KeepLatched();

        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.1f;
        Feed();
    }

    /// <summary>Latches onto a free Recycler within reach (every half second when it has none), follows it when it is carried, keeps its back hatch open.</summary>
    private void KeepLatched()
    {
        if (recycler != null && (Ending(recycler) || !recycler.isActiveAndEnabled)) { recycler = null; RefreshLabel(); }
        if (recycler == null)
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + 0.5f;
            recycler = Nearest(clicker, transform.position, snapRange, this);
            if (recycler == null) return;
            RefreshLabel();
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

    /// <summary>The types the flag can show: every unlocked pixel type that drops a normal husk (not fly-aways).</summary>
    private List<int> FilterOptions()
    {
        List<int> list = new List<int>();
        if (clicker == null) return list;
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier tier = clicker.Tiers[i];
            if (tier != null && tier.unlocked && !tier.flyAway) list.Add(i);
        }
        return list;
    }

    /// <summary>Pointer on the flag: claims the click; left-click / scroll steps through the types.</summary>
    private void HandleFlag(Camera cam)
    {
        if (cam == null || flagTop == null || PixelInput.PointerOverUI() || PixelCarryHandles.Hot) return;
        float u = Unit(clicker) * transform.lossyScale.x;
        Vector3 centre = flagTop.position - Vector3.up * u * 0.5f;
        Vector3 sp = cam.WorldToScreenPoint(centre);
        if (sp.z <= 0f) return;
        Vector3 edge = cam.WorldToScreenPoint(centre + cam.transform.right * u * 0.9f);
        float radius = Mathf.Max(20f, Mathf.Abs(edge.x - sp.x));
        if (Vector2.Distance(PixelInput.PointerPosition(), new Vector2(sp.x, sp.y)) > radius) return;
        PixelPointerClaim.Claim();
        float scroll = PixelInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.01f) StepFilter(scroll > 0f ? 1 : -1);
        else if (PixelInput.LeftPressed()) StepFilter(1);
    }

    private void StepFilter(int step)
    {
        List<int> options = FilterOptions();
        if (options.Count == 0) return;
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
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (label == null) return;
        string type = filterTier >= 0 ? clicker.Tiers[filterTier].displayName.Replace(" Pixels", "") : "nothing";
        label.text = recycler != null
            ? "Lets " + type + " through\n<size=60%>click the flag to change</size>"
            : "Sorting " + type + "\n<size=60%>no Recycler - the exit is shut</size>";
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
