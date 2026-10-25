using Unity.Profiling;
// ---- System notes (moved here from CLAUDE.md) ----
// PixelQuantumDevice.cs
//   (extends PixelPlacedDevice) Quantum Organizer (DeviceKind.QuantumOrganizer 12, auto-added like the fan by EnsureDefaultPotions,
//   Utilities card; needs Electric unlocked, 60 Electric, hold 3, durationSeconds 600, radius 24 = reach, boxCapacity 60 stored pixels,
//   bodyDiameter 2.4 = the pad's inner side, bodyHeight 0.5 = wall height; model BuildModel (this file): a glowing pad
//   inside four low walls with corner posts). Placed like the Magnet, starts disarmed (click to switch on). While working, every 0.1 s
//   it scans the old pixels: a loose pixel the PLAYER put on the pad (OldPixelInfo.RecentlyHeld, BindHeldSeconds) binds its type (up to 3 types, MaxTypes); every loose old pixel of a bound
//   type within reach (not held / flying / despawning / kinematic, not one it just released) is taken out of the old-pixel list
//   (ReleaseOldPixel(body, false)) and dematerialized with a glitch (GlitchOut: scripts and colliders off, jittering, stretching
//   slivers, flickering, then destroyed) while a small display model of that type glitches into the box (GlitchIn, AddModel: up to
//   MaxVisible 18 models per type on a lane of the pad; the stored COUNT and total payout per type are kept in Slot). A floating label
//   says 'Drop a pixel on the pad' until a type is bound; then a small ring (PixelWorldRing) shows how full it is and the device card
//   (AddInfo) lists the types and counts. Left-click the organizer (PixelPointerClaim blocks cube clicks via
//   ExternalClickBlock) to release ReleasePerClick (6) stored pixels as ordinary old pixels with the average stored payout
//   (SpawnStoredPixel, ignored by the organizer for 10 s); when its time runs out or it is removed (Ended) everything stored comes
//   out. Saved in PlacedState.extra (ExportState/ImportState, 'Q,tier,count,total;'). It must not define its own Update / LateUpdate:
//   per-frame work runs in OnTick. Tip device_QuantumOrganizer. SOCKET (PixelSockets.cs): one 'side' socket (Both): In = a hose / belt drops husks onto the pad
//   (AcceptHusk), Out = a Container Hose pulls stored husks (TakeHuskOut / HuskRoom) and a belt starting there is fed one every 0.6 s (FeedBelt). Not part of the Operation tab or the UFO's placeable list yet.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// A placed Quantum Organizer (device <c>kind</c> = QuantumOrganizer): a pad with walls around it. Once switched on, the first old pixels that come to
/// rest on the pad bind their type to it (up to three types). From then on every loose old pixel of a bound type, anywhere within its reach, is
/// dematerialized with a glitchy effect where it lies and rematerializes (glitching in) on the pad as storage. Left-click the organizer to let some of
/// the stored pixels out again (they keep their value); when its time runs out, or it is removed, everything stored is released. The countdown, timer
/// text and shrink-away come from <see cref="PixelPlacedDevice"/>; the model is built by <see cref="PixelConsumables"/>.
/// NOTE: it must not define its own Update / LateUpdate (they would hide the base's); per-frame work runs in <c>OnTick</c>.
/// </summary>
public class PixelQuantumDevice : PixelPlacedDevice, IPixelSocketDevice, IPixelHuskSource, IPixelHuskPicker, IPixelHuskRoom
{
    private const int MaxTypes = 3;          // pixel types one organizer can hold
    private const float BindHeldSeconds = 6f; // a husk binds its type only if the player held / threw it this recently
    private const int MaxVisible = 18;       // little pixel models shown per type (the stored count can be higher)
    private const int Columns = 6;
    private const int ReleasePerClick = 6;
    private const float GlitchOutSeconds = 0.4f;
    private const float GlitchInSeconds = 0.45f;




    private class Slot
    {
        public int tier;
        public int count;
        public double total;
        public readonly List<Transform> models = new List<Transform>();
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly Dictionary<Rigidbody, float> ignoreUntil = new Dictionary<Rigidbody, float>();
    private float reach, half, wallHeight;
    private int capacity, stored;
    private float nextScan;
    private TextMeshPro label;   // (only the 'drop a pixel on the pad' hint now; the counts are on the device card)
    private PixelWorldRing ring;  // (how full it is)
    private bool labelDirty = true;
    private int sessionTaken, sessionReleased;   // this organizer since it was placed / loaded (the device panel)

    public void Init(PixelClicker owner, Camera camera, PixelConsumables.Device settings, TextMeshPro timer, string format, float duration, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        reach = Mathf.Max(1f, settings.radius);
        half = Mathf.Max(0.3f, settings.bodyDiameter * 0.5f);
        wallHeight = settings.bodyHeight;
        capacity = Mathf.Max(1, settings.boxCapacity);

        GameObject go = new GameObject("Contents");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, wallHeight + 0.55f, 0f);
        label = go.AddComponent<TextMeshPro>();
        label.fontSize = 2.4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = true;
        label.color = Color.white;
        label.rectTransform.sizeDelta = new Vector2(6f, 2f);
        if (clicker.UIFont != null) label.font = clicker.UIFont;
        ring = PixelWorldRing.Attach(transform, new Vector3(0f, wallHeight + 0.55f, 0f), 0.32f, new Color(0.55f, 0.85f, 1f, 1f));

        RefreshLabel();
        Ended += _ => { ReleaseSome(1000); PixelCouplings.Unregister(this); };   // when its time is up (or it is removed) everything stored comes out again
        tint = settings.color;
        sockets = Ports(settings);
        PixelCouplings.Register(this);   // its SIDE socket: In = hoses / belts drop husks onto the pad, Out = a hose pulls stored husks
    }

    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;
    private void OnDestroy() { PixelCouplings.Unregister(this); PixelHuskPool.HuskReleasing -= OnHuskReleasing; }
    // (PixelHuskPool: the husk is going away and may be reused - let go of it)
    private void OnHuskReleasing(Rigidbody body) => ignoreUntil.Remove(body);

    // ---- Socket (PixelSockets.cs): one 'side' socket, In or Out ----
    private PixelSocket[] sockets = new PixelSocket[0];

    /// <summary>Its side socket (the real organizer and its placing ghost's aim beam).</summary>
    public static PixelSocket[] Ports(PixelConsumables.Device d) => new[] { new PixelSocket("side", new Vector3(0f, 0f, -(Mathf.Max(0.3f, d.bodyDiameter * 0.5f) + 0.2f)), Vector3.back, PixelSocketFlow.Both) };
    private Color tint = Color.cyan;
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => tint;
    public bool TakeHuskOut(PixelSocket socket, out int tier, out double amount) => TakeOneForHose(out tier, out amount);
    public bool TakeHuskOut(PixelSocket socket, System.Predicate<int> want, out int tier, out double amount) => TakeOneForHose(out tier, out amount, want);

    private const float BeltOutSeconds = 0.6f;   // (how often a belt on the side coupling set to Out is fed one stored husk)
    private float nextBeltOut;
    private PixelCouplings.TakeStored takeAny;

    /// <summary>A belt starting at the side coupling (set to Out) is fed one stored husk when it has room at its start (as the Storage Crate does).</summary>
    private void FeedBelt()
    {
        if (takeAny == null) takeAny = (out int t, out double a) => TakeOneForHose(out t, out a);
        PixelCouplings.EmitStored(this, "side", clicker, takeAny);
    }
    public int HuskRoom(PixelSocket socket) => HoseRoom;

    /// <summary>A husk comes in through an In coupling: it is dropped onto the middle of the pad (the organizer binds / takes it like any husk lying there). Refused when full.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk)
    {
        if (husk == null || HoseRoom <= 0) return false;
        husk.isKinematic = false;
        husk.position = transform.TransformPoint(new Vector3(0f, wallHeight + 0.4f, 0f));
        PixelHuskHolder.SetVelocity(husk, Vector3.down * 1.2f);
        return true;
    }

    private static float Speed(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity.magnitude;
#else
        return body.velocity.magnitude;
#endif
    }

    private Slot SlotOf(int tier)
    {
        foreach (Slot s in slots) if (s.tier == tier) return s;
        return null;
    }

    private string TierName(int tier)
    {
        string n = clicker.Tiers[tier].displayName;
        return n != null && n.EndsWith(" Pixels") ? n.Substring(0, n.Length - 7) : n;
    }

    // ------------------------------------------------------------------
    // Per frame
    // ------------------------------------------------------------------

    protected override void OnTick()
    {
        if (cam == null) cam = PixelClicker.ViewCameraOf(clicker);
        if (label != null && cam != null) label.transform.rotation = cam.transform.rotation;
        if (labelDirty) RefreshLabel();
        if (Time.timeScale <= 0f || IsEnding) return;
        if (Time.time >= nextBeltOut) { nextBeltOut = Time.time + BeltOutSeconds; FeedBelt(); }

        // Mouse over the organizer: a click lets stored pixels out.
        if (cam != null && !PixelInput.PointerOverUI())
        {
            Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
            if (HitTest(ray, out _))
            {
                PixelPointerClaim.Claim();
                if (PixelInput.LeftPressed()) ReleaseSome(ReleasePerClick);
            }
        }

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.1f;
            using (scanMarker.Auto()) Scan();
        }
    }

    private static readonly ProfilerMarker scanMarker = new ProfilerMarker("Pixel.QuantumScan");
    private void Scan()
    {
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        int captured = 0;
        for (int i = pixels.Count - 1; i >= 0 && captured < 4; i--)
        {
            Rigidbody body = pixels[i];
            if (!PixelHuskHolder.Takeable(clicker, body)) continue;   // (one owner per husk; it now also leaves the husk in the player's hand alone)
            if (ignoreUntil.TryGetValue(body, out float until) && Time.time < until) continue;
            OldPixelInfo info = PixelHuskQuery.Get(body).info;

            Slot slot = SlotOf(info.tierIndex);
            if (slot == null)
            {
                // A pixel the PLAYER put on the pad (held / thrown in the last few seconds) binds its type; stray husks that land there, or ones a belt
                // or hose brings in, never bind a new type (2026-10-19: it used to pick up whatever fell on it).
                if (slots.Count >= MaxTypes || info == null || !info.RecentlyHeld(BindHeldSeconds)) continue;
                Vector3 local = transform.InverseTransformPoint(body.position);
                bool inside = Mathf.Abs(local.x) < half && Mathf.Abs(local.z) < half && local.y < wallHeight + 1f;
                if (!inside || Speed(body) > 2f) continue;
                slot = new Slot { tier = info.tierIndex };
                slots.Add(slot);
                labelDirty = true;
            }
            if (stored >= capacity) continue;
            if ((body.position - transform.position).sqrMagnitude > reach * reach) continue;

            Capture(body, info, slot);
            captured++;
        }
        if (ignoreUntil.Count > 60) ignoreUntil.Clear();
    }

    private void Capture(Rigidbody body, OldPixelInfo info, Slot slot)
    {
        GameObject go = body.gameObject;
        double amount = info.amount;
        if (!clicker.ReleaseOldPixel(body, false)) return;   // out of the old-pixel list: nothing else touches it
        slot.count++;
        slot.total += amount;
        stored++;
        sessionTaken++;
        labelDirty = true;
        StartCoroutine(GlitchOut(go));
        AddModel(slot, true);
    }

    // ------------------------------------------------------------------
    // The stored pixels
    // ------------------------------------------------------------------

    private Vector3 ModelPosition(int lane, int index, float cell)
    {
        float laneWidth = half * 2f / MaxTypes;
        float x = -half + ((index % Columns) + 0.5f) * cell;
        float z = -half + (lane + 0.5f) * laneWidth;
        float y = 0.09f + (index / Columns) * cell * 0.9f + cell * 0.4f;
        return new Vector3(x, y, z);
    }

    private void AddModel(Slot slot, bool glitch)
    {
        if (slot.models.Count >= MaxVisible || slot.models.Count >= slot.count) return;
        int lane = slots.IndexOf(slot);
        float cell = half * 2f / Columns;
        GameObject model = clicker.CreateDisplayPixel(slot.tier, transform, cell * 0.8f);
        if (model == null) return;
        model.transform.localPosition = ModelPosition(lane, slot.models.Count, cell);
        model.transform.localRotation = Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f);
        slot.models.Add(model.transform);
        if (glitch) StartCoroutine(GlitchIn(model.transform));
    }

    private void RefreshLabel()
    {
        labelDirty = false;
        if (label == null) return;
        // The floating text is only the first-use hint; once a type is bound a small ring shows how full it is (the device card lists the types and counts).
        label.text = slots.Count == 0 ? "<alpha=#CC>Grab a husk and drop it\non the pad to bind its type" : "";
        if (ring != null) ring.Set(slots.Count == 0 ? -1f : stored / (float)Mathf.Max(1, capacity), new Color(0.55f, 0.85f, 1f, 1f));
    }

    /// <summary>Lets up to 'count' stored pixels out (they keep their stored value and drop onto the floor round the pad).</summary>
    /// <summary>Room left in the organizer (a Container Hose only pumps in what fits).</summary>
    public int HoseRoom => IsEnding ? 0 : Mathf.Max(0, capacity - stored);

    /// <summary>A hose / belt takes one stored pixel out (the first type with any; 'want' = only a type it accepts): its type and average value; false when none.</summary>
    public bool TakeOneForHose(out int tier, out double amount, System.Predicate<int> want = null)
    {
        tier = -1; amount = 0;
        if (IsEnding) return false;
        foreach (Slot s in slots)
        {
            if (s.count <= 0 || (want != null && !want(s.tier))) continue;
            amount = s.total / s.count;
            tier = s.tier;
            s.count--;
            s.total -= amount;
            stored--;
            if (s.models.Count > s.count && s.models.Count > 0)
            {
                Transform last = s.models[s.models.Count - 1];
                s.models.RemoveAt(s.models.Count - 1);
                if (last != null) { last.SetParent(null, true); StartCoroutine(GlitchOut(last.gameObject)); }
            }
            labelDirty = true;
            return true;
        }
        return false;
    }

    private void ReleaseSome(int count)
    {
        if (clicker == null || stored <= 0) return;
        int released = 0;
        foreach (Slot s in slots)
        {
            while (s.count > 0 && released < count && released < 120)
            {
                double each = s.total / s.count;
                s.count--;
                s.total -= each;
                stored--;
                released++;
                if (s.models.Count > s.count && s.models.Count > 0)
                {
                    Transform last = s.models[s.models.Count - 1];
                    s.models.RemoveAt(s.models.Count - 1);
                    if (last != null) { last.SetParent(null, true); StartCoroutine(GlitchOut(last.gameObject)); }
                }

                Vector3 p = transform.TransformPoint(new Vector3(Random.Range(-half * 0.6f, half * 0.6f), wallHeight + 0.9f, Random.Range(-half * 0.6f, half * 0.6f)));
                Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3f), Random.Range(-1.2f, 1.2f));
                if (clicker.SpawnStoredPixel(s.tier, each, p, v))
                {
                    IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
                    if (pixels.Count > 0 && pixels[pixels.Count - 1] != null) ignoreUntil[pixels[pixels.Count - 1]] = Time.time + 10f;   // not straight back in
                }
            }
            if (released >= count) break;
        }
        if (released > 0) { labelDirty = true; sessionReleased += released; }
    }

    /// <summary>The shared device card / panel: stored / room, each bound type's count; the panel adds reach and this session's totals.</summary>
    protected override void AddInfo(PixelDeviceInfo info)
    {
        info.Add("Stored", stored + " / " + capacity);
        if (slots.Count == 0) info.Note("<color=#9FB0CC>Grab a husk and drop it on the pad to bind its type.</color>");
        foreach (Slot sl in slots)
            info.Note("<color=#" + ColorUtility.ToHtmlStringRGB(clicker.Tiers[sl.tier].color) + ">" + TierName(sl.tier) + "</color>  x" + sl.count);
        if (stored > 0) info.Note("<color=#9FB0CC>Click it to let " + ReleasePerClick + " out.</color>");
        if (!info.Full) return;
        info.Add("Bound types", slots.Count + " / " + MaxTypes);
        info.Heading("This session");
        info.Add("Taken in", sessionTaken.ToString());
        info.Add("Let out", sessionReleased.ToString());
    }

    // ------------------------------------------------------------------
    // The glitch effects
    // ------------------------------------------------------------------

    /// <summary>The old pixel stutters, stretches into slivers and flickers out where it lay.</summary>
    private IEnumerator GlitchOut(GameObject go)
    {
        if (go == null) yield break;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (mb != null) mb.enabled = false;   // nothing else may move or remove it now
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        Vector3 start = go.transform.position, scale = go.transform.localScale;

        float t = 0f;
        while (t < GlitchOutSeconds)
        {
            if (go == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / GlitchOutSeconds);
            go.transform.position = start + Random.insideUnitSphere * 0.14f * k;
            go.transform.localScale = new Vector3(scale.x * Random.Range(0.4f, 1.8f), scale.y * Random.Range(0.08f, 1.5f), scale.z * Random.Range(0.4f, 1.8f)) * (1f - k * 0.55f);
            bool on = Random.value > 0.2f + 0.6f * k;
            foreach (Renderer r in renderers) if (r != null) r.enabled = on;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>A stored pixel's model glitches into being: stretched, jittery and flickering, settling into its normal shape.</summary>
    private IEnumerator GlitchIn(Transform model)
    {
        if (model == null) yield break;
        Vector3 finalScale = model.localScale, finalPos = model.localPosition;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        float t = 0f;
        while (t < GlitchInSeconds)
        {
            if (model == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / GlitchInSeconds);
            float inv = 1f - k;
            model.localScale = new Vector3(finalScale.x * Mathf.Lerp(Random.Range(0.3f, 1.8f), 1f, k), finalScale.y * Mathf.Lerp(Random.Range(0.1f, 2.4f), 1f, k), finalScale.z * Mathf.Lerp(Random.Range(0.3f, 1.8f), 1f, k));
            model.localPosition = finalPos + Random.insideUnitSphere * 0.1f * inv;
            bool on = Random.value < 0.35f + 0.65f * k;
            foreach (Renderer r in renderers) if (r != null) r.enabled = on;
            yield return null;
        }
        if (model == null) yield break;
        model.localScale = finalScale;
        model.localPosition = finalPos;
        foreach (Renderer r in renderers) if (r != null) r.enabled = true;
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>The bound types and what is stored, as text (kept in the save file).</summary>
    public string ExportState()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        foreach (Slot s in slots) sb.Append("Q,").Append(s.tier).Append(',').Append(s.count).Append(',').Append(s.total.ToString("R", ci)).Append(';');
        return sb.ToString();
    }

    public void ImportState(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        CultureInfo ci = CultureInfo.InvariantCulture;
        foreach (string entry in text.Split(';'))
        {
            string[] f = entry.Split(',');
            if (f.Length < 4 || f[0] != "Q") continue;
            if (!int.TryParse(f[1], out int tier) || !int.TryParse(f[2], out int count) || !double.TryParse(f[3], NumberStyles.Float, ci, out double sum)) continue;
            if (!clicker.IsValidTierIndex(tier) || slots.Count >= MaxTypes || SlotOf(tier) != null) continue;
            Slot s = new Slot { tier = tier, count = Mathf.Max(0, count), total = sum };
            slots.Add(s);
            stored += s.count;
            for (int k = 0; k < Mathf.Min(s.count, MaxVisible); k++) AddModel(s, false);
        }
        labelDirty = true;
    }

    // ---- The model (moved here from PixelConsumables 2026-10-24: each device builds its own look; PixelConsumables.BuildDeviceObject calls it) ----

    /// <summary>The Quantum Organizer: a glowing pad inside four low walls with taller corner posts ('D' = the pad's inner side, 'H' = the wall height).</summary>
    public static void BuildModel(PixelClicker clicker, GameObject root, PixelConsumables.Device d, bool isPreview, float previewOpacity)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        float D = d.bodyDiameter, H = d.bodyHeight, wall = 0.12f;
        Color baseColor = new Color(0.12f, 0.1f, 0.2f, opacity);
        Color padColor = new Color(0.25f, 0.9f, 1f, opacity);
        Color wallColor = new Color(d.color.r, d.color.g, d.color.b, opacity);
        Color rimColor = new Color(0.6f, 1f, 1f, opacity);

        GameObject Part(string name, Vector3 pos, Vector3 scale, Color color) =>
            PixelFactoryKit.ModelPart(clicker, PrimitiveType.Cube, name, root.transform, pos, scale, color, isPreview);

        Part("Base", new Vector3(0f, 0.025f, 0f), new Vector3(D + wall * 2f + 0.1f, 0.05f, D + wall * 2f + 0.1f), baseColor);
        Part("Pad", new Vector3(0f, 0.06f, 0f), new Vector3(D, 0.02f, D), padColor);
        // Two lines across the pad, like a binding mark.
        Part("Pad Line", new Vector3(0f, 0.075f, 0f), new Vector3(D * 0.96f, 0.01f, 0.035f), rimColor);
        Part("Pad Line", new Vector3(0f, 0.075f, 0f), new Vector3(0.035f, 0.01f, D * 0.96f), rimColor);

        float o = D * 0.5f + wall * 0.5f;
        Part("Wall N", new Vector3(0f, H * 0.5f, o), new Vector3(D + wall * 2f, H, wall), wallColor);
        Part("Wall S", new Vector3(0f, H * 0.5f, -o), new Vector3(D + wall * 2f, H, wall), wallColor);
        Part("Wall E", new Vector3(o, H * 0.5f, 0f), new Vector3(wall, H, D), wallColor);
        Part("Wall W", new Vector3(-o, H * 0.5f, 0f), new Vector3(wall, H, D), wallColor);
        Part("Rim N", new Vector3(0f, H + 0.015f, o), new Vector3(D + wall * 2f, 0.03f, wall * 1.2f), rimColor);
        Part("Rim S", new Vector3(0f, H + 0.015f, -o), new Vector3(D + wall * 2f, 0.03f, wall * 1.2f), rimColor);
        Part("Rim E", new Vector3(o, H + 0.015f, 0f), new Vector3(wall * 1.2f, 0.03f, D), rimColor);
        Part("Rim W", new Vector3(-o, H + 0.015f, 0f), new Vector3(wall * 1.2f, 0.03f, D), rimColor);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Part("Post", new Vector3(sx * o, H * 0.65f, sz * o), new Vector3(wall * 1.7f, H * 1.3f, wall * 1.7f), rimColor);
    }
}
