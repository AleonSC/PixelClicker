// ---- System notes (moved here from CLAUDE.md) ----
// PixelPlacedDevice.cs
//   INFO CARD / PANEL: BuildInfo(PixelDeviceInfo) fills the shared hover card / right-click panel (PixelDeviceCard): common lines here, subclasses override
//   AddInfo, behaviour components next to a stub implement IPixelDeviceInfo (PixelDeviceInfo.cs).
//   GENERATOR POWER (2026-10-19): HoldPower(fraction, colour) from a Biomass Generator pauses the countdown like the Electric links AND shows the generator's
//   energy bar (PixelEnergyBar) over the device, tracking its charge (the time ring hides meanwhile).
//   SetRemoveGlow(0..1): while the right mouse button is held on the device (PixelConsumables.UpdateRemoval + PixelHold ring) it tints red, glows and lights up red as the ring fills.
//   Abstract base of placed devices: countdown (shown as a small RING, PixelWorldRing, once switched on - the timer text only says 'Click to start'
//   before that and the uses-mode labels like the Charge Booster's CLICK!), shrink-away (InitCommon, OnTick, IsEnding). Armed /
//   disarmed: every device you place starts DISARMED (Disarm() right after SpawnDevice; grey via per-renderer property blocks -
//   ApplyTint / ClearTint, a desaturated dull version of each material's colour, emission off; the countdown does not run, OnTick is
//   not called, the timer text shows DisarmedText 'Click to start'; subclasses must check Armed in FixedUpdate / event handlers: fan,
//   vacuum, sorter TryRoute, booster OnCollected / AddCharge, PixelElectricLinks ignores disarmed devices) until you LEFT-CLICK it
//   once (PixelConsumables.UpdateArming: DeviceUnderPointer(true) using the virtual HitTest(ray, out distance) - the sorter overrides
//   it with its ring + pipe so clicking the cube inside doesn't arm it; while the mouse is over a disarmed device
//   PixelConsumables.HoveringDisarmed makes PixelClicker.ExternalClickBlock true) -> Arm() fades the colour in over ArmFadeSeconds
//   (armFadeSeconds 0 = 0.7) and starts the countdown (sound device_arm). Saved as PlacedState.disarmed.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Shared behaviour of the devices you place in the world (vacuum, fan): a countdown with a timer text floating above
/// the device that always faces the camera, and a shrink-away when time runs out. Subclasses add what the device does
/// (FixedUpdate) and may react to every tick (OnTick).
/// </summary>
public abstract class PixelPlacedDevice : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        all.Clear();
    }

    private static readonly List<PixelPlacedDevice> all = new List<PixelPlacedDevice>();

    /// <summary>Every placed device that exists right now.</summary>
    public static IReadOnlyList<PixelPlacedDevice> All => all;

    protected PixelClicker clicker;
    protected Camera cam;

    private TextMeshPro timerText;
    private float remaining;
    private float shrinkSeconds;
    private bool dying;

    /// <summary>Which entry of the consumables' device list this is (set when it is placed; used by the save system).</summary>
    public int DeviceIndex { get; set; } = -1;

    /// <summary>Tier index of the only pixel type this device affects (an attuned device), or -1 = every type.</summary>
    public int AttuneTier { get; set; } = -1;

    /// <summary>Does this device act on that old pixel? (Always true unless the device is attuned to one pixel type.)</summary>
    public bool Accepts(Rigidbody body)
    {
        if (AttuneTier < 0) return true;
        return PixelHuskQuery.Get(body).Tier == AttuneTier;
    }

    private bool usesMode;
    private int usesLeft;

    // --- Armed / disarmed: a freshly placed device is grey and idle (no countdown, no effect) until you left-click it once.

    /// <summary>The text shown above a device that has not been switched on yet (PixelConsumables sets it).</summary>
    public static string DisarmedText = "Click to start";

    /// <summary>Seconds the grey device takes to fade into colour when it is switched on.</summary>
    public static float ArmFadeSeconds = 0.7f;

    /// <summary>False while the device is waiting for its first click: it does nothing and its timer does not run.</summary>
    public bool Armed { get; private set; } = true;

    private float tint = 1f;                       // 0 = grey, 1 = full colour
    private bool tintTouched;                      // property blocks were set, so they have to be cleared at the end
    private struct TintEntry
    {
        public Renderer renderer;
        public Color baseColor, baseEmission;
        public bool hasColor, hasEmission, hasBaseColorProp;
    }
    private List<TintEntry> tintEntries;
    private MaterialPropertyBlock tintBlock;
    // --- Being removed (hold the right mouse button on it): the device slowly glows a luminescent red as the hold fills (PixelConsumables.UpdateRemoval).
    private float removeGlow;
    private Light removeLight;
    private static readonly Color RemoveRed = new Color(1f, 0.1f, 0.06f, 1f);

    /// <summary>0..1: how far the removal hold has got. Tints the whole device red, adds a red glow and a red light; 0 puts it back.</summary>
    public void SetRemoveGlow(float k)
    {
        k = Mathf.Clamp01(k);
        if (Mathf.Approximately(k, removeGlow)) return;
        removeGlow = k;
        if (removeGlow > 0f) ApplyTint(); else if (tint >= 1f) ClearTint(); else ApplyTint();
        if (removeGlow > 0f)
        {
            if (removeLight == null)
            {
                GameObject lightGo = new GameObject("Remove Glow");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 0.6f;
                removeLight = lightGo.AddComponent<Light>();
                removeLight.type = LightType.Point;
                removeLight.color = RemoveRed;
                removeLight.range = 4f;
                removeLight.shadows = LightShadows.None;
            }
            removeLight.intensity = removeGlow * removeGlow * 4f;
        }
        else if (removeLight != null) { Destroy(removeLight.gameObject); removeLight = null; }
    }

    private static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");

    /// <summary>Puts the device to sleep: grey and idle until <see cref="Arm"/>. Called once, right after a device is placed.</summary>
    public void Disarm()
    {
        Armed = false;
        tint = 0f;
        ApplyTint();
    }

    /// <summary>Switches the device on: it fades into colour and starts working (its countdown starts now).</summary>
    public void Arm()
    {
        if (Armed || dying) return;
        Armed = true;
        PixelAudio.Play("device_arm");
    }

    /// <summary>Is this ray over the device? Returns the distance along the ray. The default looks at the visible parts' bounds (not the range markings); devices with a hollow shape override it.</summary>
    public virtual bool HitTest(Ray ray, out float distance)
    {
        distance = float.MaxValue;
        bool hit = false;
        foreach (Renderer r in HitRenderers())
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r.bounds.IntersectRay(ray, out float d) && d < distance) { distance = d; hit = true; }
        }
        return hit;
    }

    /// <summary>Does this part count as 'on the device' for HitTest? (Default: every visible part except the range markings.) Read once per part, when the list is made.</summary>
    protected virtual bool CountsForHit(Renderer r) => !IsMarking(r) && !r.name.StartsWith("Coupling");

    // The names of a device's MARKINGS (its reach disc / arc, the Fan's blow wedge, the Sorter's bend cone): they are not part of the
    // device's body (no clicks on them, hidden in shop / Operation pictures). Name a new marking with one of these and use IsMarking to test.
    public const string RangeMark = "Range", BlowAreaMark = "Blow Area", BendConeMark = "Bend Cone";

    /// <summary>Is this renderer one of a device's markings (range disc / arc, blow wedge, bend cone) rather than its body?</summary>
    public static bool IsMarking(Renderer r) => r != null && (r.name == RangeMark || r.name == BlowAreaMark || r.name == BendConeMark);   // (a coupling on the device is removed on its own)

    private Renderer[] hitRenderers;
    private float hitRenderersTime = -10f;
    private static readonly List<Renderer> hitScratch = new List<Renderer>();

    /// <summary>
    /// The parts HitTest looks at. HitTest runs every frame for every device (the mouse-over check for arming / removing), so the list is made
    /// once and refreshed only once a second (in case parts were added or swapped) instead of GetComponentsInChildren + name checks every call.
    /// </summary>
    protected Renderer[] HitRenderers()
    {
        if (hitRenderers == null || Time.unscaledTime - hitRenderersTime > 1f)
        {
            hitRenderersTime = Time.unscaledTime;
            hitScratch.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (CountsForHit(r)) hitScratch.Add(r);
            hitRenderers = hitScratch.ToArray();
        }
        return hitRenderers;
    }

    private void ApplyTint()
    {
        if (tintEntries == null)
        {
            tintEntries = new List<TintEntry>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TMP_Text>() != null) continue;      // the timer text keeps its colour
                Material m = r.sharedMaterial;
                if (m == null) continue;
                TintEntry e = new TintEntry { renderer = r, baseColor = Color.white, baseEmission = Color.black };
                if (m.HasProperty(BaseColorId)) { e.baseColor = m.GetColor(BaseColorId); e.hasColor = true; e.hasBaseColorProp = true; }
                else if (m.HasProperty(ColorId)) { e.baseColor = m.GetColor(ColorId); e.hasColor = true; }
                if (m.HasProperty(EmissionId) && m.IsKeywordEnabled("_EMISSION")) { e.baseEmission = m.GetColor(EmissionId); e.hasEmission = true; }
                tintEntries.Add(e);
            }
            tintBlock = new MaterialPropertyBlock();
        }

        tintTouched = true;
        foreach (TintEntry e in tintEntries)
        {
            if (e.renderer == null) continue;
            e.renderer.GetPropertyBlock(tintBlock);
            if (e.hasColor)
            {
                float grey = e.baseColor.r * 0.3f + e.baseColor.g * 0.59f + e.baseColor.b * 0.11f;
                Color dull = new Color(grey * 0.8f + 0.1f, grey * 0.8f + 0.1f, grey * 0.8f + 0.1f, e.baseColor.a);
                Color c = Color.Lerp(dull, e.baseColor, tint);
                if (removeGlow > 0f) c = Color.Lerp(c, new Color(RemoveRed.r, RemoveRed.g * 0.5f, RemoveRed.b * 0.5f, c.a), removeGlow * 0.85f);
                tintBlock.SetColor(e.hasBaseColorProp ? BaseColorId : ColorId, c);
                if (e.hasBaseColorProp) tintBlock.SetColor(ColorId, c);
            }
            if (e.hasEmission) tintBlock.SetColor(EmissionId, e.baseEmission * tint + RemoveRed * (removeGlow * removeGlow * 2.5f));
            e.renderer.SetPropertyBlock(tintBlock);
        }
    }

    private void ClearTint()
    {
        if (!tintTouched || tintEntries == null) return;
        tintTouched = false;
        foreach (TintEntry e in tintEntries)
            if (e.renderer != null) e.renderer.SetPropertyBlock(null);
    }

    /// <summary>Seconds left (in uses mode: the number of uses left, so the save system stores either).</summary>
    public float Remaining => usesMode ? usesLeft : remaining;

    /// <summary>True for devices that last a number of uses (clicks, lightning strikes) instead of a time.</summary>
    public bool UsesMode => usesMode;

    /// <summary>Uses left (uses mode only).</summary>
    public int UsesLeft => usesLeft;

    /// <summary>Switches the device from a countdown to a number of uses. Call after InitCommon.</summary>
    protected void InitUses(int uses)
    {
        usesMode = true;
        usesLeft = Mathf.Max(1, uses);
    }

    /// <summary>What the floating text shows in uses mode.</summary>
    protected virtual string UsesLabel() => usesLeft.ToString();

    /// <summary>Uses up some uses; the device dissolves at 0. No effect once it is shrinking away.</summary>
    protected void SpendUse(int count = 1)
    {
        if (!usesMode || dying) return;
        usesLeft -= count;
        if (usesLeft <= 0)
        {
            usesLeft = 0;
            StartCoroutine(ShrinkAway());
        }
    }

    /// <summary>True once the device has run out or is being removed and is shrinking away (it should stop working). (One name: IsEnding / IsEnding were merged into it.)</summary>
    public bool IsEnding => dying;

    /// <summary>Raised once when the device's time is up and it starts shrinking away (also when it is removed by hand).</summary>
    public event System.Action<PixelPlacedDevice> Ended;

    /// <summary>Adds (or, negative, takes) seconds to the countdown. No effect once the device is shrinking away.</summary>
    public void AddSeconds(float seconds)
    {
        if (!dying && !usesMode) remaining += seconds;
    }

    private float powerUntil = -1f;

    /// <summary>Called every frame by the Electric links while a group of charged Electric pixels powers this device: its countdown pauses (it runs on 'batteries').</summary>
    public void HoldPower() { powerUntil = Time.time + 0.3f; }

    /// <summary>Is the device being powered by Electric pixels right now (its countdown is paused)?</summary>
    public bool Powered => Time.time < powerUntil;

    private float generatorBar = -1f, generatorUntil = -1f;
    private Color generatorColor;
    private PixelEnergyBar energyBar;

    /// <summary>
    /// Called by a Biomass Generator every few frames while this device is in its radius: the countdown pauses (HoldPower) and the generator's ENERGY
    /// BAR shows over this device, 'fraction' full in 'color' - every device it powers tracks the generator's charge. Generators never power each other.
    /// </summary>
    public void HoldPower(float fraction, Color color)
    {
        HoldPower();
        generatorBar = fraction;
        generatorColor = color;
        generatorUntil = Time.time + 0.3f;
    }

    /// <summary>Is a Biomass Generator powering this device right now?</summary>
    public bool GeneratorPowered => Time.time < generatorUntil;

    /// <summary>The generator's energy bar over the device while it is powered by one (made on first use, over the timer or the top of the model).</summary>
    private void UpdateEnergyBar()
    {
        bool show = GeneratorPowered && generatorBar >= 0f && !dying;
        if (!show) { if (energyBar != null) energyBar.Set(-1f, generatorColor); return; }
        if (energyBar == null)
        {
            Vector3 at;
            Transform parent = transform;
            if (timerText != null) { parent = timerText.transform.parent != null ? timerText.transform.parent : transform; at = timerText.transform.localPosition + Vector3.up * 0.25f; }
            else
            {
                Bounds b = new Bounds(transform.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                at = transform.InverseTransformPoint(new Vector3(transform.position.x, b.max.y + 0.35f, transform.position.z));
            }
            energyBar = PixelEnergyBar.Attach(parent, at, 0.7f, generatorColor);
        }
        energyBar.Set(generatorBar, generatorColor);
    }

    /// <summary>Removes the device now (it shrinks away like when its time runs out). No refund.</summary>
    public void RemoveNow()
    {
        if (!dying) StartCoroutine(ShrinkAway());
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable() => all.Remove(this);

    /// <summary>Sets up the countdown. Called by the subclass's own Init.</summary>
    protected void InitCommon(PixelClicker owner, Camera camera, TextMeshPro timer, string format, float duration, float shrinkTime)
    {
        clicker = owner;
        cam = camera;
        timerText = timer;
        remaining = duration;
        shrinkSeconds = Mathf.Max(0.01f, shrinkTime);
    }

    /// <summary>Called every frame while the device is still working.</summary>
    protected virtual void OnTick() { }

    // --- The shared device card / panel (PixelDeviceCard, PixelDeviceInfo.cs) ---

    /// <summary>True for a device that never runs out (the Wood Chipper, Sweeper, Sorting Hopper, Funnel... stubs: a countdown of 1e9 s).</summary>
    public bool Permanent => !usesMode && remaining > 1e8f;

    private static readonly List<IPixelDeviceInfo> infoScratch = new List<IPixelDeviceInfo>();
    private static PixelConsumables infoConsumables;
    private static PixelShop infoShop;

    /// <summary>
    /// Fills the shared card / panel: the common lines here (name, time or uses left with the bar, 'Click to start', powered, attuned; Full: run time,
    /// reach, Operation levels), then this device's own AddInfo, then every IPixelDeviceInfo component on the same object (the behaviour next to a stub).
    /// </summary>
    public void BuildInfo(PixelDeviceInfo info)
    {
        if (infoConsumables == null) infoConsumables = PixelFind.Cached<PixelConsumables>();
        PixelConsumables.Device d = infoConsumables != null ? infoConsumables.EffectiveDeviceAt(DeviceIndex) : null;
        info.Title = d != null ? d.displayName : name;
        if (!Armed) info.Note("<color=#FFD27F>" + DisarmedText + "</color>");
        else if (usesMode)
        {
            info.Add("Uses left", usesLeft.ToString());
            int total = d != null ? Mathf.Max(d.uses, usesLeft) : usesLeft;
            if (total > 0) { info.Progress = usesLeft / (float)total; info.ProgressLabel = usesLeft + " / " + total + " uses"; }
        }
        else if (!Permanent)
        {
            float full = d != null && d.durationSeconds > 0f ? Mathf.Max(d.durationSeconds, remaining) : remaining;
            info.Progress = full > 0f ? Mathf.Clamp01(remaining / full) : 0f;
            info.ProgressLabel = PixelDeviceInfo.Clock(remaining) + " left";
        }
        if (Powered) info.Note("<color=#7FD4FF>Powered - the timer waits</color>");
        if (AttuneTier >= 0 && clicker != null && clicker.IsValidTierIndex(AttuneTier)) info.Add("Attuned to", clicker.Tiers[AttuneTier].displayName);

        AddInfo(info);
        GetComponents(infoScratch);
        for (int i = 0; i < infoScratch.Count; i++) if (!ReferenceEquals(infoScratch[i], this)) infoScratch[i].FillDeviceInfo(info);
        infoScratch.Clear();

        if (info.Full && d != null)
        {
            info.Heading("Device");
            info.Add("Run time", Permanent ? "permanent" : usesMode ? d.uses + " uses" : PixelDeviceInfo.Clock(d.durationSeconds));
            if (d.radius > 0f && d.kind != PixelConsumables.DeviceKind.Recycler) info.Add("Reach", d.radius.ToString("0.#") + " units");
            if (infoShop == null) infoShop = PixelFind.Cached<PixelShop>();
            if (infoShop != null) infoShop.AddOperationLevels(d.kind, info);
        }
    }

    /// <summary>A device's own lines for the card / panel (add the key stats always, the rest only when info.Full).</summary>
    protected virtual void AddInfo(PixelDeviceInfo info) { }

    /// <summary>Which kind of device this is (from its entry in the consumables' list), or null when unknown.</summary>
    public PixelConsumables.DeviceKind? Kind
    {
        get
        {
            if (infoConsumables == null) infoConsumables = PixelFind.Cached<PixelConsumables>();
            PixelConsumables.Device d = infoConsumables != null && DeviceIndex >= 0 ? infoConsumables.EffectiveDeviceAt(DeviceIndex) : null;
            return d != null ? d.kind : (PixelConsumables.DeviceKind?)null;
        }
    }

    private void Update()
    {
        if (dying) return;
        UpdateEnergyBar();

        if (!Armed)
        {
            // Waiting for the first click: no countdown, no effect.
            if (timerText != null && timerText.text != DisarmedText) timerText.text = DisarmedText;
            if (timerRing != null) timerRing.Set(-1f, RingColor);
            return;
        }

        if (usesMode)
        {
            OnTick();
            if (dying) return;
            if (timerText != null) timerText.text = UsesLabel();
            return;
        }

        if (!Powered) remaining -= Time.deltaTime;   // powered by Electric pixels: the countdown waits
        if (remaining <= 0f)
        {
            StartCoroutine(ShrinkAway());
            return;
        }

        OnTick();
        if (timerText != null && !Permanent)
        {
            // The countdown is a small ring now (time left at a glance; the exact time is on the device card, PixelDeviceCard). Blue while powered.
            if (timerText.text.Length > 0) timerText.text = "";
            if (timerRing == null)
            {
                timerRing = PixelWorldRing.Attach(timerText.transform.parent != null ? timerText.transform.parent : transform, timerText.transform.localPosition, 0.2f, RingColor);
                if (infoConsumables == null) infoConsumables = PixelFind.Cached<PixelConsumables>();
                PixelConsumables.Device d = infoConsumables != null ? infoConsumables.EffectiveDeviceAt(DeviceIndex) : null;
                ringFullSeconds = Mathf.Max(remaining, d != null ? d.durationSeconds : 0f, 1f);
            }
            float left = remaining / ringFullSeconds;
            timerRing.Set(GeneratorPowered ? -1f : left, Powered ? PoweredRingColor : left < 0.2f ? LowRingColor : RingColor);   // (a generator's energy bar shows instead while it powers the device)
        }
    }

    private PixelWorldRing timerRing;
    private float ringFullSeconds = 1f;
    private static readonly Color RingColor = new Color(0.6f, 1f, 0.7f, 1f), LowRingColor = new Color(1f, 0.7f, 0.3f, 1f), PoweredRingColor = new Color(0.5f, 0.83f, 1f, 1f);

    private void LateUpdate()
    {
        // Grey -> colour fade after the device is switched on.
        if (Armed && tint < 1f)
        {
            tint = Mathf.MoveTowards(tint, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, ArmFadeSeconds));
            if (tint >= 1f && removeGlow <= 0f) ClearTint(); else ApplyTint();
        }

        // The timer always faces the camera.
        if (timerText == null) return;
        if (cam == null) cam = Camera.main;
        if (cam != null) timerText.transform.rotation = cam.transform.rotation;
    }

    private IEnumerator ShrinkAway()
    {
        dying = true;
        Ended?.Invoke(this);
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (timerRing != null) timerRing.Set(-1f, RingColor);

        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < shrinkSeconds)
        {
            t += Time.deltaTime;
            transform.localScale = start * (1f - Mathf.Clamp01(t / shrinkSeconds));
            yield return null;
        }
        Destroy(gameObject);
    }
}
