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
    private string timerFormat;
    private float remaining;
    private float shrinkSeconds;
    private bool dying;

    /// <summary>Which entry of the consumables' device list this is (set when it is placed; used by the save system).</summary>
    public int DeviceIndex { get; set; } = -1;

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
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r.name == "Range" || r.name == "Blow Area") continue;
            if (r.bounds.IntersectRay(ray, out float d) && d < distance) { distance = d; hit = true; }
        }
        return hit;
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
                tintBlock.SetColor(e.hasBaseColorProp ? BaseColorId : ColorId, c);
                if (e.hasBaseColorProp) tintBlock.SetColor(ColorId, c);
            }
            if (e.hasEmission) tintBlock.SetColor(EmissionId, e.baseEmission * tint);
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

    /// <summary>True once time ran out and the device is shrinking away (it should stop working).</summary>
    protected bool IsDying => dying;

    /// <summary>Is the device already shrinking away?</summary>
    public bool IsRemoving => dying;

    /// <summary>Raised once when the device's time is up and it starts shrinking away (also when it is removed by hand).</summary>
    public event System.Action<PixelPlacedDevice> Ended;

    /// <summary>Adds (or, negative, takes) seconds to the countdown. No effect once the device is shrinking away.</summary>
    public void AddSeconds(float seconds)
    {
        if (!dying && !usesMode) remaining += seconds;
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
        timerFormat = format;
        remaining = duration;
        shrinkSeconds = Mathf.Max(0.01f, shrinkTime);
    }

    /// <summary>Called every frame while the device is still working.</summary>
    protected virtual void OnTick() { }

    private void Update()
    {
        if (dying) return;

        if (!Armed)
        {
            // Waiting for the first click: no countdown, no effect.
            if (timerText != null && timerText.text != DisarmedText) timerText.text = DisarmedText;
            return;
        }

        if (usesMode)
        {
            OnTick();
            if (dying) return;
            if (timerText != null) timerText.text = UsesLabel();
            return;
        }

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            StartCoroutine(ShrinkAway());
            return;
        }

        OnTick();
        if (timerText != null) timerText.text = string.Format(timerFormat, Mathf.CeilToInt(remaining));
    }

    private void LateUpdate()
    {
        // Grey -> colour fade after the device is switched on.
        if (Armed && tint < 1f)
        {
            tint = Mathf.MoveTowards(tint, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, ArmFadeSeconds));
            if (tint >= 1f) ClearTint(); else ApplyTint();
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
