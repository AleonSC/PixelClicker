// ---- System notes (moved here from CLAUDE.md) ----
// PixelCombo.cs
//   Combo meter (shop upgrade "Combo Meter", UpgradeEffect.ComboMeter, 5 levels; level value = that tier's multiplier): quick manual
//   clicks build a combo (breaks after 1.2 s idle). Tiered multiplier: tierCombos {10, 50, 200, 500, 1000} / tierMultipliers {1.5, 2,
//   3, 4, 5}; the highest tier whose combo count is reached (and that the upgrade level unlocked: UnlockedTiers = tiers whose
//   multiplier <= maxMultiplier) sets Multiplier (x1 below tier 1; Tier); sets PixelClicker.ManualClickBonus every frame (manual
//   payouts only); meter at the bottom-centre shows the next milestone (nextFormat): INSIDE the bottom black bar by default (inBottomBar, barPadding: PlaceMeter scales the
//   'Meter Root' holder - meter + every tier effect - to fit the bar's height and centres it there; off = just above the bar at meterBottomMargin; MeterTopOffset follows). Fades out after hideAfterSeconds without manual
//   clicks. It is the last thing to arrive after entering the game (UpdateEntrance: waits for the title screen, the camera intro and
//   the sliding margins / buttons to finish plus entranceDelaySeconds (0 = 0.6 s), then fades in over entranceFadeSeconds (0 = 1.2 s);
//   disableEntranceFade). Tier effects (by reached tier, cumulative, runtime-drawn UI sprites): 1 mild shake, 2 yellow flame aura
//   (UpdateFlames, pooled sprites around the edges), 3 yellow glow, 4 violent shake (violentShakeFactor) + lightning bolts
//   (UpdateLightning), 5 explosion (flash + sparks, repeats every explosionRepeatSeconds) + super-saiyan hair spikes (UpdateHair);
//   when the combo breaks the effects wind down over breakSeconds (shake amplitude falls, glow / flames fade, the hair drops, the
//   meter flashes white: breakFlash*), then the meter fades out as usual; tuned in the 'Combo Effects (by tier)' Inspector group.
//   PixelShop finds/adds it and applies it via SetUpgrade.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combo meter (bought as an upgrade in the shop). Every quick manual click builds the combo. The multiplier works in
/// tiers: reaching a tier's combo count (10, 50, 200, 500, 1000 by default) switches the click multiplier to that tier's
/// value (x1.5 ... x5); each upgrade level unlocks one more tier. Every tier also gives the meter a visual effect (shake,
/// flame aura, glow, violent shake + lightning, explosion + super-saiyan hair). If you stop clicking for a moment the
/// combo drops back to zero. A small meter above the bottom-centre of the screen shows the combo, the multiplier
/// and the time left before it breaks.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelCombo : MonoBehaviour, IPixelSaved
{
    // ---- save: this system writes its own part of the save file (PixelSaveRegistry) ----
    [System.Serializable] private class SaveState { public bool disabled; }
    string IPixelSaved.SaveId => "combo";
    int IPixelSaved.SaveOrder => 140;
    string IPixelSaved.Export() => PixelSaveRegistry.ToJson(new SaveState { disabled = UserDisabled });
    void IPixelSaved.Import(string data) { SaveState s = PixelSaveRegistry.FromJson<SaveState>(data); UserDisabled = s != null && s.disabled; }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        ComboReached = null;
    }

    [Header("References")]
    [Tooltip("The PixelClicker whose clicks build the combo. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("State")]
    [Tooltip("Is the combo meter switched on? (The shop turns this on when the Combo Meter upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool comboActive = false;

    [Min(1f)]
    [Tooltip("The highest tier multiplier the player has unlocked. The shop's upgrade levels set this; it decides how many of the tiers below work.")]
    [SerializeField] private float maxMultiplier = 1.5f;

    [Header("Combo Fuel")]
    [Min(0.2f)]
    [Tooltip("When a Combo Fuel saves the combo, the timer bar rebuilds from empty to full over this many seconds.")]
    [SerializeField] private float fuelRefillSeconds = 3f;

    [Tooltip("Colour of the timer bar while it is refilling from Combo Fuel.")]
    [SerializeField] private Color fuelBarColor = new Color(0.35f, 0.8f, 1f, 1f);

    [Header("Combo Rules")]
    [Min(0.1f)]
    [Tooltip("Seconds you have after a click to click again before the combo breaks.")]
    [SerializeField] private float comboWindowSeconds = 1.2f;

    [Header("Combo Tiers")]
    [Tooltip("Combo count that switches each tier on (tier 1, 2, 3...). Needs the same number of entries as 'Tier Multipliers'. Empty = 10, 50, 200, 500, 1000.")]
    [SerializeField] private int[] tierCombos = { 10, 50, 200, 500, 1000 };

    [Tooltip("Click multiplier of each tier. Upgrade level N unlocks the first N tiers (it matches the shop's level values). Empty = 1.5, 2, 3, 4, 5.")]
    [SerializeField] private float[] tierMultipliers = { 1.5f, 2f, 3f, 4f, 5f };

    [Tooltip("Do clicks on tough pixels (several clicks to collect) that don't pay yet also build the combo?")]
    [SerializeField] private bool hitsCount = true;

    [Tooltip("Do auto clicker clicks build the combo too? (Off = only your own clicks.)")]
    [SerializeField] private bool autoClicksCount = false;

    [Header("Meter Look")]
    [Tooltip("Show the meter on screen while the combo is active.")]
    [SerializeField] private bool showMeter = true;

    [Tooltip("Meter size (canvas units).")]
    [SerializeField] private Vector2 meterSize = new Vector2(420f, 90f);

    [Tooltip("Distance of the meter's bottom edge from the bottom of the screen.")]
    [SerializeField] private float meterBottomMargin = 30f;

    [Tooltip("Horizontal shift from the centre of the screen.")]
    [SerializeField] private float horizontalOffset = 0f;

    [Tooltip("Put the meter INSIDE the bottom black bar (centred, scaled down to fit its height; the tier effects scale with it) instead of floating above it. Off = above the bar at 'Meter Bottom Margin'.")]
    [SerializeField] private bool inBottomBar = true;

    [Min(0f)]
    [Tooltip("In the bottom bar: the gap between the meter and the bar's top and bottom edges (canvas units).")]
    [SerializeField] private float barPadding = 8f;

    [Tooltip("Text. {0} = combo count, {1} = multiplier.")]
    [SerializeField] private string meterFormat = "Combo {0}   x{1}";

    [Tooltip("Text when the combo is at its maximum. {0} = combo count, {1} = multiplier.")]
    [SerializeField] private string maxFormat = "MAX Combo {0}   x{1}";

    [Tooltip("Meter text while a higher unlocked tier is still ahead. {0} combo, {1} current multiplier, {2} next multiplier, {3} combo needed for it.")]
    [SerializeField] private string nextFormat = "Combo {0}   x{1}   (x{2} at {3})";

    [Tooltip("Text size.")]
    [SerializeField] private float fontSize = 36f;

    [Tooltip("Background colour of the meter.")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("Colour of the timer bar while building.")]
    [SerializeField] private Color barColor = new Color(1f, 0.75f, 0.2f, 1f);

    [Tooltip("Colour of the text and bar at the maximum multiplier.")]
    [SerializeField] private Color maxColor = new Color(1f, 0.4f, 0.2f, 1f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Range(0f, 1f)]
    [Tooltip("How faded the meter is while there is no combo.")]
    [SerializeField] private float idleAlpha = 0.45f;

    [Min(0f)]
    [Tooltip("Seconds without clicking the pixel before the meter fades away completely (0 = never hides). It shows again on the next click.")]
    [SerializeField] private float hideAfterSeconds = 5f;

    [Header("Entrance")]
    [Tooltip("Don't fade the meter in after the game starts (it is shown at once).")]
    [SerializeField] private bool disableEntranceFade = false;

    [Min(0f)]
    [Tooltip("Seconds to wait after everything else has arrived (title screen gone, camera settled, black margins and buttons in place) before the meter starts to fade in. 0 = 0.6.")]
    [SerializeField] private float entranceDelaySeconds = 0f;

    [Min(0f)]
    [Tooltip("Seconds the meter takes to fade in. 0 = 1.2.")]
    [SerializeField] private float entranceFadeSeconds = 0f;

    [Min(0.01f)]
    [Tooltip("Seconds the meter takes to fade away.")]
    [SerializeField] private float fadeSeconds = 1f;

    [Min(0f)]
    [Tooltip("How much the text pops each time the combo grows (0 = no pop).")]
    [SerializeField] private float popScale = 0.2f;

    [Tooltip("Sorting order of the meter's canvas (below the inventory).")]
    [SerializeField] private int sortingOrder = 90;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Combo Effects (by tier)")]
    [Tooltip("Master switch for the tier effects below. Tier 1 = shake, 2 = yellow flame aura, 3 = yellow glow, 4 = violent shake + lightning, 5 = explosion + super-saiyan hair. Higher tiers keep the lower effects (except that the shake gets violent at tier 4).")]
    [SerializeField] private bool effectsEnabled = true;

    [Tooltip("Colour of the glow around the meter (tier 3).")]
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Min(1f)]
    [Tooltip("How far the glow reaches past the meter (canvas units).")]
    [SerializeField] private float glowSize = 34f;

    [Range(0f, 1f)]
    [Tooltip("Strongest the glow gets while pulsing.")]
    [SerializeField] private float glowMaxAlpha = 0.9f;

    [Min(0f)]
    [Tooltip("How fast the glow pulses (0 = steady).")]
    [SerializeField] private float glowPulseSpeed = 4f;

    [Min(0f)]
    [Tooltip("How far the meter jitters at tier 1-3 (canvas units).")]
    [SerializeField] private float shakeAmount = 3f;

    [Min(0f)]
    [Tooltip("How far the meter tilts while shaking at tier 1-3 (degrees).")]
    [SerializeField] private float shakeTilt = 1f;

    [Min(0.05f)]
    [Tooltip("When the combo breaks, how long the effects take to wind down (seconds): the shaking slows, the glow and flames fade, the hair drops and the meter flashes white.")]
    [SerializeField] private float breakSeconds = 1f;

    [Range(0f, 1f)]
    [Tooltip("How bright the white flash is when a combo with effects breaks.")]
    [SerializeField] private float breakFlashStrength = 0.85f;

    [Min(1f)]
    [Tooltip("At tier 4 and 5 the shake and tilt are multiplied by this.")]
    [SerializeField] private float violentShakeFactor = 4f;

    [Range(4, 160)]
    [Tooltip("How many aura flames burn at once (tier 2+).")]
    [SerializeField] private int flameCount = 56;

    [Min(0.1f)]
    [Tooltip("How long a flame lives (seconds).")]
    [SerializeField] private float flameLifeSeconds = 0.8f;

    [Min(1f)]
    [Tooltip("How high a flame rises over its life (canvas units).")]
    [SerializeField] private float flameRise = 80f;

    [Min(4f)]
    [Tooltip("Size of a new flame (canvas units); it shrinks as it rises.")]
    [SerializeField] private float flameSize = 40f;

    [Tooltip("Colour of a new aura flame (hot).")]
    [SerializeField] private Color auraStartColor = new Color(1f, 1f, 0.55f, 1f);

    [Tooltip("Colour of an aura flame at the end of its life.")]
    [SerializeField] private Color auraEndColor = new Color(1f, 0.7f, 0.1f, 1f);

    [Range(1, 8)]
    [Tooltip("How many lightning bolts can flash at once (tier 4+).")]
    [SerializeField] private int lightningBolts = 4;

    [Min(10f)]
    [Tooltip("Length of a lightning bolt (canvas units).")]
    [SerializeField] private float lightningLength = 150f;

    [Min(1f)]
    [Tooltip("Thickness of a lightning bolt (canvas units).")]
    [SerializeField] private float lightningThickness = 4f;

    [Tooltip("Colour of the lightning.")]
    [SerializeField] private Color lightningColor = new Color(0.85f, 0.95f, 1f, 1f);

    [Min(0.02f)]
    [Tooltip("Seconds between new lightning flashes (random between this and twice this).")]
    [SerializeField] private float lightningInterval = 0.12f;

    [Range(4, 64)]
    [Tooltip("How many sparks fly out when the meter explodes (tier 5).")]
    [SerializeField] private int explosionSparks = 32;

    [Min(0f)]
    [Tooltip("Seconds between repeat explosions while tier 5 lasts (0 = only the first one).")]
    [SerializeField] private float explosionRepeatSeconds = 4f;

    [Min(0.1f)]
    [Tooltip("How long an explosion lasts (seconds).")]
    [SerializeField] private float explosionSeconds = 0.6f;

    [Min(10f)]
    [Tooltip("How far the explosion sparks fly (canvas units).")]
    [SerializeField] private float explosionRadius = 260f;

    [Range(3, 21)]
    [Tooltip("How many hair spikes the meter grows at tier 5.")]
    [SerializeField] private int hairSpikes = 11;

    [Min(10f)]
    [Tooltip("Length of the middle hair spikes (canvas units).")]
    [SerializeField] private float hairLength = 120f;

    [Min(4f)]
    [Tooltip("Width of a hair spike at its base (canvas units).")]
    [SerializeField] private float hairWidth = 54f;

    [Tooltip("Colour of the hair.")]
    [SerializeField] private Color hairColor = new Color(1f, 0.82f, 0.15f, 1f);

    /// <summary>Raised every time the combo grows (the new count). Used by the stats.</summary>
    public static event System.Action<int> ComboReached;

    private int combo;
    private float timeLeft;
    private bool fueling;                 // Combo Fuel saved the combo: the bar is rebuilding
    private PixelConsumables fuelSource;
    private float pop;
    private GameObject canvasRoot;
    private CanvasGroup group;
    private TMP_Text label;
    private Image barFill;
    private RectTransform barFillRect;

    /// <summary>How far above the bottom of the screen the top of the meter is (canvas units); tips use it to sit just above the meter.</summary>
    public float MeterTopOffset => InBar ? BarH + 40f : meterBottomMargin + BarH + meterSize.y + 40f;   // (in the bar: boxes only need to clear the bar)

    private static float BarH => PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
    private bool InBar => inBottomBar && BarH > 20f;   // (no bars = float above the bottom edge as before)
    private RectTransform meterRoot;   // holds the meter and every effect; scaled down as one piece to fit inside the bar

    /// <summary>Is the Combo Meter bought?</summary>
    public bool Active => comboActive;

    /// <summary>The player switched the combo meter off in the Toggles window. Saved.</summary>
    public bool UserDisabled
    {
        get => userDisabled;
        set
        {
            userDisabled = value;
            if (userDisabled) combo = 0;
            Apply();
        }
    }

    private bool userDisabled;
    private bool Working => comboActive && !userDisabled;

    /// <summary>The highest multiplier the combo can reach.</summary>
    public float MaxMultiplier => maxMultiplier;

    private static readonly int[] DefaultCombos = { 10, 50, 200, 500, 1000 };
    private static readonly float[] DefaultMultipliers = { 1.5f, 2f, 3f, 4f, 5f };

    private int[] Combos => tierCombos != null && tierCombos.Length > 0 ? tierCombos : DefaultCombos;
    private float[] Mults => tierMultipliers != null && tierMultipliers.Length > 0 ? tierMultipliers : DefaultMultipliers;
    private int TierCount => Mathf.Min(Combos.Length, Mults.Length);
    private int ComboAt(int tierIndex) => Combos[tierIndex];
    private float MultAt(int tierIndex) => Mults[tierIndex];

    /// <summary>How many tiers the upgrade has unlocked (the tiers whose multiplier is within the unlocked maximum).</summary>
    public int UnlockedTiers
    {
        get
        {
            int n = 0;
            for (int i = 0; i < TierCount; i++) if (MultAt(i) <= maxMultiplier + 0.001f) n = i + 1;
            return n;
        }
    }

    /// <summary>The tier the current combo has reached (0 = none yet; never above the unlocked tiers).</summary>
    public int Tier
    {
        get
        {
            if (!Working) return 0;
            int unlocked = UnlockedTiers, t = 0;
            for (int i = 0; i < unlocked; i++) if (combo >= ComboAt(i)) t = i + 1;
            return t;
        }
    }

    /// <summary>Current multiplier (1 when the combo hasn't reached a tier, or the meter is off).</summary>
    public double Multiplier { get { int t = Tier; return t > 0 ? MultAt(t - 1) : 1d; } }

    /// <summary>Called by the shop: switches the meter on/off and sets its maximum multiplier.</summary>
    public void SetUpgrade(bool active, float max)
    {
        comboActive = active;
        if (active) maxMultiplier = Mathf.Max(1f, max);
        if (!active) combo = 0;
        Apply();
    }

    private void Awake()
    {
        PixelSaveRegistry.Register(this);
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelCombo: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (clicker.UIFont != null) font = clicker.UIFont;
        BuildMeter();
    }

    private void OnEnable()
    {
        if (clicker == null) return;
        clicker.PixelCollected += OnCollected;
        clicker.PixelHit += OnHit;
    }

    private void OnDisable()
    {
        if (clicker == null) return;
        clicker.PixelCollected -= OnCollected;
        clicker.PixelHit -= OnHit;
    }

    private void OnDestroy()
    {
        PixelSaveRegistry.Unregister(this);
        if (glowSprite != null) Destroy(glowSprite);
        if (flameSprite != null) Destroy(flameSprite);
        if (glowTexture != null) Destroy(glowTexture);
        if (flameTexture != null) Destroy(flameTexture);
        if (hairSprite != null) Destroy(hairSprite);
        if (hairTexture != null) Destroy(hairTexture);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private float idleSeconds; // time since the player last clicked the pixel (fades the meter out)

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        if (!automatic) idleSeconds = 0f;
        AddClick(automatic);
    }

    private void OnHit(int tierIndex, int hits, int needed, bool automatic)
    {
        if (!automatic) idleSeconds = 0f;
        if (hitsCount) AddClick(automatic);
    }

    private bool TryUseFuel()
    {
        if (!Working) return false;
        if (fuelSource == null) fuelSource = PixelFind.First<PixelConsumables>();
        return fuelSource != null && fuelSource.TryUseAutoItem(PixelConsumables.DeviceKind.ComboFuel);
    }

    private void AddClick(bool automatic)
    {
        if (!Working || (automatic && !autoClicksCount)) return;
        combo += Mathf.Max(1, PixelClicker.DevClickCount);
        PixelStats.Best("combo.tier", Tier);
        ComboReached?.Invoke(combo);
        timeLeft = comboWindowSeconds;
        fueling = false; // a click ends the refill: the bar is full again and runs down as usual
        pop = 1f;
    }

    private void Update()
    {
        // The manual-click bonus the clicker applies to every payout.
        clicker.ManualClickBonus = Multiplier;

        if (combo > 0)
        {
            if (fueling)
            {
                // Saved by Combo Fuel: the bar rebuilds to the top, then runs down like normal unless you click.
                timeLeft += Time.deltaTime * comboWindowSeconds / Mathf.Max(0.2f, fuelRefillSeconds);
                if (timeLeft >= comboWindowSeconds) { timeLeft = comboWindowSeconds; fueling = false; }
            }
            else
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 0f)
                {
                    if (TryUseFuel()) { fueling = true; timeLeft = 0f; PixelStats.Count("fuel.used"); }
                    else combo = 0;
                }
            }
        }
        else fueling = false;
        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime * 6f);
        idleSeconds += Time.deltaTime;
        Apply();
    }

    private int shownCombo = -1;
    private double shownMultiplier = -1d;
    private bool shownAtMax;

    private float entranceWait, entranceK = 0f, baseAlpha = -1f;

    /// <summary>The meter is the last thing to arrive after entering the game: it waits for the title screen, the camera swing and the sliding margins, then fades in.</summary>
    private float UpdateEntrance()
    {
        if (disableEntranceFade) { entranceK = 1f; return 1f; }
        bool settled = !PixelGameMode.TitleOrIntro && PixelHud.RevealEased >= 0.999f;
        if (!settled) { entranceWait = 0f; entranceK = 0f; return 0f; }
        entranceWait += Time.unscaledDeltaTime;
        float delay = entranceDelaySeconds > 0f ? entranceDelaySeconds : 0.6f;
        if (entranceWait > delay)
            entranceK = Mathf.MoveTowards(entranceK, 1f, Time.unscaledDeltaTime / (entranceFadeSeconds > 0f ? entranceFadeSeconds : 1.2f));
        return Mathf.SmoothStep(0f, 1f, entranceK);
    }

    private void Apply()
    {
        if (canvasRoot == null) return;
        float entrance = UpdateEntrance();
        bool visible = Working && showMeter;
        if (canvasRoot.activeSelf != visible) canvasRoot.SetActive(visible);
        if (!visible) return;
        // Faded out after a while without clicks and every effect wound down: nothing on it changes until the next combo, so skip the per-frame layout and effects.
        if (combo <= 0 && hideAfterSeconds > 0f && idleSeconds >= hideAfterSeconds && baseAlpha == 0f && pop <= 0f && breakTimer <= 0f && lastLiveTier <= 0)
        {
            if (group.alpha != 0f) group.alpha = 0f;
            return;
        }

        int tier = Tier, unlocked = UnlockedTiers;
        bool atMax = tier > 0 && tier >= unlocked && combo > 0;
        double multiplierNow = Multiplier;
        if (combo != shownCombo || multiplierNow != shownMultiplier || atMax != shownAtMax)
        {
            shownCombo = combo;
            shownMultiplier = multiplierNow;
            shownAtMax = atMax;
            string mult = multiplierNow.ToString("0.##");
            if (atMax) label.text = string.Format(maxFormat, combo, mult);
            else if (tier < unlocked) label.text = string.Format(nextFormat, combo, mult, MultAt(tier).ToString("0.##"), ComboAt(tier));
            else label.text = string.Format(meterFormat, combo, mult);
        }
        label.color = atMax ? maxColor : textColor;
        label.rectTransform.localScale = Vector3.one * (1f + popScale * pop);

        float fill = combo > 0 ? Mathf.Clamp01(timeLeft / comboWindowSeconds) : 0f;
        barFillRect.anchorMax = new Vector2(fill, 1f);
        barFill.color = fueling ? fuelBarColor : atMax ? maxColor : barColor;

        // Fully visible while a combo runs, dimmed when idle, then faded out after a while without clicks.
        bool hidden = combo <= 0 && hideAfterSeconds > 0f && idleSeconds >= hideAfterSeconds;
        float targetAlpha = combo > 0 ? 1f : hidden ? 0f : idleAlpha;
        if (baseAlpha < 0f) baseAlpha = group.alpha;
        baseAlpha = targetAlpha > baseAlpha ? targetAlpha : Mathf.MoveTowards(baseAlpha, targetAlpha, Time.unscaledDeltaTime / fadeSeconds);
        group.alpha = baseAlpha * entrance;
        PlaceMeter();
        UpdateEffects();
    }

    /// <summary>
    /// Inside the bottom black bar (inBottomBar): the holder is scaled so the meter fits the bar's height and the meter sits barPadding above its bottom edge.
    /// Otherwise just above the bar. Done every frame: the bar may not exist yet when the meter is built, or its size may change.
    /// </summary>
    private void PlaceMeter()
    {
        if (meterRoot == null) return;
        float bar = BarH;
        float scale = InBar ? Mathf.Clamp((bar - 2f * barPadding) / Mathf.Max(1f, meterSize.y), 0.2f, 1f) : 1f;
        if (!Mathf.Approximately(meterRoot.localScale.x, scale)) meterRoot.localScale = new Vector3(scale, scale, 1f);
        boxBasePosition = InBar ? new Vector2(horizontalOffset / scale, (bar - meterSize.y * scale) * 0.5f / scale)   // (centred in the bar, in the holder's own units)
                                : new Vector2(horizontalOffset, meterBottomMargin + bar);
    }

    private void BuildMeter()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelCombo Canvas", sortingOrder, referenceResolution, false);
        group = canvasRoot.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        GameObject holder = new GameObject("Meter Root", typeof(RectTransform));
        holder.transform.SetParent(canvasRoot.transform, false);
        meterRoot = holder.GetComponent<RectTransform>();
        PixelUIKit.Stretch(meterRoot);
        meterRoot.pivot = new Vector2(0.5f, 0f);   // (scales about the bottom middle, where the meter sits)

        GameObject box = new GameObject("Combo Meter", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(meterRoot, false);
        Image bg = box.GetComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = meterSize;
        boxRect = br;
        PlaceMeter();
        br.anchoredPosition = boxBasePosition;   // in the bottom bar, or just above it

        float barHeight = Mathf.Max(8f, meterSize.y * 0.18f);

        label = PixelUIKit.CreateText(font, box.transform, "Label", "", fontSize, TextAlignmentOptions.Center,
                                      FontStyles.Bold, textColor);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(10f, barHeight + 6f);
        lr.offsetMax = new Vector2(-10f, -4f);
        label.enableAutoSizing = true;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = 14f;

        GameObject bar = new GameObject("Timer Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(box.transform, false);
        bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        bar.GetComponent<Image>().raycastTarget = false;
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(-20f, barHeight);
        barRect.anchoredPosition = new Vector2(0f, 6f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(bar.transform, false);
        barFill = fillGo.GetComponent<Image>();
        barFill.color = barColor;
        barFill.raycastTarget = false;
        barFillRect = fillGo.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = barFillRect.offsetMax = Vector2.zero;

        BuildEffects();
        canvasRoot.SetActive(comboActive && showMeter);
    }


    // ------------------------------------------------------------------
    // Tier effects: 1 shake, 2 flame aura, 3 glow, 4 violent shake + lightning, 5 explosion + super-saiyan hair
    // ------------------------------------------------------------------

    private RectTransform boxRect;
    private Vector2 boxBasePosition;

    private Image glowImage;
    private RectTransform glowRect;
    private Texture2D glowTexture, flameTexture, hairTexture;
    private Sprite glowSprite, flameSprite, hairSprite;

    // flame aura
    private RectTransform flameRoot;
    private Image[] flameImages;
    private float[] flameAge, flameLife, flameX, flameY, flameDx, flameSway, flameScale;
    private bool fireWasOn;

    // hair
    private RectTransform hairRoot;
    private Image[] hairOuter, hairInner;
    private float[] hairFrac, hairAngle, hairLen, hairPhase;
    private bool hairWasOn;
    private float hairGrow;

    // lightning
    private RectTransform boltRoot;
    private Image[] boltSegments;
    private float[] boltAge, boltLife;
    private float boltTimer;
    private const int BoltSegmentCount = 7;

    // explosion
    private Image flashImage;
    private RectTransform flashRect;
    private Image[] burstImages;
    private float[] burstAngle, burstSpeed, burstSize;
    private float burstAge = -1f, repeatTimer;
    private int lastTier;

    // winding down after the combo breaks
    private Image breakFlashImage;
    private float breakTimer, breakFlash;
    private int lastLiveTier, decayTier;

    private void BuildEffects()
    {
        // Glow: a soft-edged halo texture the size of the meter plus the glow reach, drawn behind the meter.
        float w = meterSize.x + glowSize * 2f, h = meterSize.y + glowSize * 2f;
        int tw = Mathf.Clamp(Mathf.RoundToInt(w / 4f), 8, 256), th = Mathf.Clamp(Mathf.RoundToInt(h / 4f), 8, 256);
        glowTexture = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] pixels = new Color32[tw * th];
        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                float px = (x + 0.5f) / tw * w - w * 0.5f; // canvas units from the centre
                float py = (y + 0.5f) / th * h - h * 0.5f;
                float dx = Mathf.Max(0f, Mathf.Abs(px) - meterSize.x * 0.5f);
                float dy = Mathf.Max(0f, Mathf.Abs(py) - meterSize.y * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / glowSize; // 0 at the meter's edge, 1 at the reach
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                pixels[y * tw + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        glowTexture.SetPixels32(pixels);
        glowTexture.Apply(false, false);
        glowSprite = Sprite.Create(glowTexture, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f);

        Image glow = NewEffectImage("Glow", meterRoot, glowSprite, new Vector2(0.5f, 0.5f), new Vector2(w, h));
        glowImage = glow;
        glowRect = glow.rectTransform;
        glow.gameObject.SetActive(false);

        // Explosion flash: the same halo, scaled up for a moment.
        flashImage = NewEffectImage("Flash", meterRoot, glowSprite, new Vector2(0.5f, 0.5f), new Vector2(w, h));
        flashRect = flashImage.rectTransform;
        flashImage.gameObject.SetActive(false);

        // White flash over the meter itself when a combo with effects breaks.
        GameObject bf = new GameObject("Break Flash", typeof(RectTransform), typeof(Image));
        bf.transform.SetParent(boxRect, false);
        PixelUIKit.Stretch(bf.GetComponent<RectTransform>());
        breakFlashImage = bf.GetComponent<Image>();
        breakFlashImage.color = new Color(1f, 1f, 1f, 0f);
        breakFlashImage.raycastTarget = false;

        // Soft round sprite shared by the flames and the explosion sparks.
        const int fs = 32;
        flameTexture = new Texture2D(fs, fs, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] fp = new Color32[fs * fs];
        for (int y = 0; y < fs; y++)
        {
            for (int x = 0; x < fs; x++)
            {
                float dx = (x + 0.5f) / fs * 2f - 1f, dy = (y + 0.5f) / fs * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                fp[y * fs + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
            }
        }
        flameTexture.SetPixels32(fp);
        flameTexture.Apply(false, false);
        flameSprite = Sprite.Create(flameTexture, new Rect(0, 0, fs, fs), new Vector2(0.5f, 0.5f), 100f);

        // Aura flames: a pool of soft sprites that rise around the meter.
        flameRoot = NewEffectRoot("Flames", meterRoot);
        int count = Mathf.Max(1, flameCount);
        flameImages = new Image[count];
        flameAge = new float[count]; flameLife = new float[count]; flameX = new float[count]; flameY = new float[count];
        flameDx = new float[count]; flameSway = new float[count]; flameScale = new float[count];
        for (int i = 0; i < count; i++)
        {
            flameImages[i] = NewEffectImage("Flame", flameRoot, flameSprite, new Vector2(0.5f, 0.5f), Vector2.zero);
            flameImages[i].gameObject.SetActive(false);
        }

        // Hair: spikes of a triangle sprite fanned out over the top edge (a pale inner spike over each gold one).
        const int hw = 32, hh = 64;
        hairTexture = new Texture2D(hw, hh, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] hp = new Color32[hw * hh];
        for (int y = 0; y < hh; y++)
        {
            for (int x = 0; x < hw; x++)
            {
                float v = (y + 0.5f) / hh;
                float u = (x + 0.5f) / hw;
                float edge = (0.5f * (1f - v) - Mathf.Abs(u - 0.5f)) * hw; // pixels inside the triangle's side
                hp[y * hw + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(edge) * 255f));
            }
        }
        hairTexture.SetPixels32(hp);
        hairTexture.Apply(false, false);
        hairSprite = Sprite.Create(hairTexture, new Rect(0, 0, hw, hh), new Vector2(0.5f, 0f), 100f);

        hairRoot = NewEffectRoot("Hair", meterRoot);
        int spikes = Mathf.Max(3, hairSpikes);
        hairOuter = new Image[spikes]; hairInner = new Image[spikes];
        hairFrac = new float[spikes]; hairAngle = new float[spikes]; hairLen = new float[spikes]; hairPhase = new float[spikes];
        for (int i = 0; i < spikes; i++)
        {
            float f = spikes > 1 ? i / (float)(spikes - 1) : 0.5f;
            float centre = Mathf.Abs(f * 2f - 1f); // 0 in the middle, 1 at the sides
            hairFrac[i] = Mathf.Lerp(-0.42f, 0.42f, f);
            hairAngle[i] = Mathf.Lerp(-48f, 48f, f) + Random.Range(-6f, 6f);
            hairLen[i] = (1f - 0.4f * centre) * Random.Range(0.85f, 1.1f);
            hairPhase[i] = Random.Range(0f, 6.28f);
            hairOuter[i] = NewEffectImage("Spike", hairRoot, hairSprite, new Vector2(0.5f, 0f), Vector2.zero);
            hairInner[i] = NewEffectImage("Spike Core", hairRoot, hairSprite, new Vector2(0.5f, 0f), Vector2.zero);
            hairOuter[i].gameObject.SetActive(false);
            hairInner[i].gameObject.SetActive(false);
        }

        // Behind the meter itself: flash, halo, flames and hair first, then the meter on top.
        flashImage.transform.SetSiblingIndex(0);
        glow.transform.SetSiblingIndex(1);
        flameRoot.SetSiblingIndex(2);
        hairRoot.SetSiblingIndex(3);

        // In front of the meter: lightning and explosion sparks (created last, so they draw last).
        boltRoot = NewEffectRoot("Lightning", meterRoot);
        int bolts = Mathf.Max(1, lightningBolts);
        boltSegments = new Image[bolts * BoltSegmentCount];
        boltAge = new float[bolts]; boltLife = new float[bolts];
        for (int i = 0; i < boltSegments.Length; i++)
        {
            Image seg = NewEffectImage("Bolt", boltRoot, null, new Vector2(0.5f, 0.5f), Vector2.zero);
            seg.enabled = false;
            boltSegments[i] = seg;
        }

        RectTransform burstRoot = NewEffectRoot("Explosion", meterRoot);
        int sparks = Mathf.Max(4, explosionSparks);
        burstImages = new Image[sparks];
        burstAngle = new float[sparks]; burstSpeed = new float[sparks]; burstSize = new float[sparks];
        for (int i = 0; i < sparks; i++)
        {
            burstImages[i] = NewEffectImage("Spark", burstRoot, flameSprite, new Vector2(0.5f, 0.5f), Vector2.zero);
            burstImages[i].gameObject.SetActive(false);
        }
    }

    private static RectTransform NewEffectRoot(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = Vector2.zero;
        r.anchoredPosition = Vector2.zero;
        return r;
    }

    private static Image NewEffectImage(string name, Transform parent, Sprite sprite, Vector2 pivot, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = pivot;
        r.sizeDelta = size;
        return img;
    }

    private void RespawnFlame(int i, bool stagger)
    {
        flameLife[i] = flameLifeSeconds * Random.Range(0.6f, 1.1f);
        flameAge[i] = stagger ? Random.value * flameLife[i] : 0f;
        float edge = Random.value; // most flames start on the top edge, the rest on the sides
        if (edge < 0.5f) { flameX[i] = Random.Range(-0.5f, 0.5f) * meterSize.x; flameY[i] = meterSize.y; flameDx[i] = Random.Range(-0.15f, 0.15f); }
        else if (edge < 0.75f) { flameX[i] = -0.5f * meterSize.x; flameY[i] = Random.value * meterSize.y; flameDx[i] = -0.45f; }
        else { flameX[i] = 0.5f * meterSize.x; flameY[i] = Random.value * meterSize.y; flameDx[i] = 0.45f; }
        flameSway[i] = Random.Range(0f, 6.28f);
        flameScale[i] = Random.Range(0.7f, 1.2f);
    }

    private void Explode()
    {
        burstAge = 0f;
        for (int i = 0; i < burstImages.Length; i++)
        {
            burstAngle[i] = Random.Range(0f, 6.2832f);
            burstSpeed[i] = Random.Range(0.45f, 1f);
            burstSize[i] = Random.Range(18f, 44f);
            burstImages[i].gameObject.SetActive(true);
        }
        flashImage.gameObject.SetActive(true);
    }

    private void HideBolt(int b)
    {
        boltLife[b] = 0f;
        for (int k = 0; k < BoltSegmentCount; k++) boltSegments[b * BoltSegmentCount + k].enabled = false;
    }

    private void StartBolt(int b, Vector2 centre)
    {
        float a = Random.Range(0f, 6.2832f);
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 start = centre + new Vector2(dir.x * meterSize.x * 0.5f, dir.y * meterSize.y * 0.5f);
        Vector2 previous = start;
        for (int k = 0; k < BoltSegmentCount; k++)
        {
            Vector2 next = start + dir * (lightningLength * (k + 1) / BoltSegmentCount) + perp * Random.Range(-18f, 18f);
            Vector2 d = next - previous;
            Image seg = boltSegments[b * BoltSegmentCount + k];
            RectTransform r = seg.rectTransform;
            r.anchoredPosition = (previous + next) * 0.5f;
            r.sizeDelta = new Vector2(d.magnitude + lightningThickness * 0.5f, lightningThickness);
            r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            seg.enabled = true;
            previous = next;
        }
        boltAge[b] = 0f;
        boltLife[b] = Random.Range(0.08f, 0.16f);
    }

    /// <summary>Runs every frame while the meter shows.</summary>
    private void UpdateEffects()
    {
        if (glowImage == null) return;
        int liveTier = effectsEnabled && combo > 0 ? Tier : 0;
        bool paused = PixelPauseMenu.IsPaused;
        float t = Time.unscaledTime;
        float dt = paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // The combo just broke: wind the effects down instead of cutting them off.
        if (liveTier > 0) { breakTimer = 0f; decayTier = liveTier; }
        else if (lastLiveTier > 0 && effectsEnabled)
        {
            breakTimer = breakSeconds;
            breakFlash = 1f;
        }
        lastLiveTier = liveTier;

        int tier = liveTier;
        bool breaking = false;
        float k = 1f; // intensity: 1 while the combo runs, falls to 0 as the effects wind down
        if (liveTier == 0 && breakTimer > 0f)
        {
            breakTimer -= dt;
            breaking = true;
            tier = breakTimer > 0f ? decayTier : 0;
            k = Mathf.Clamp01(breakTimer / Mathf.Max(0.05f, breakSeconds));
        }
        bool shakeOn = tier >= 1, violent = tier >= 4;

        // The white flash.
        breakFlash = Mathf.MoveTowards(breakFlash, 0f, dt / 0.35f);
        breakFlashImage.color = new Color(1f, 1f, 1f, breakFlash * breakFlashStrength);

        // Shake the meter (every effect follows it). Violent from tier 4.
        Vector2 offset = Vector2.zero;
        float tilt = 0f;
        float factor = (violent ? violentShakeFactor : 1f) * k * k; // the shake slows gradually as k falls
        if (shakeOn && !paused)
        {
            offset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * (shakeAmount * factor);
            tilt = Random.Range(-1f, 1f) * (shakeTilt * factor);
        }
        else if (shakeOn)
        {
            offset = boxRect.anchoredPosition - boxBasePosition;
            tilt = boxRect.localEulerAngles.z > 180f ? boxRect.localEulerAngles.z - 360f : boxRect.localEulerAngles.z;
        }
        boxRect.anchoredPosition = boxBasePosition + offset;
        boxRect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        Vector2 centre = boxBasePosition + offset + new Vector2(0f, meterSize.y * 0.5f);

        // Tier 3: a pulsing yellow halo behind the meter.
        bool glowOn = tier >= 3;
        if (glowImage.gameObject.activeSelf != glowOn) glowImage.gameObject.SetActive(glowOn);
        if (glowOn)
        {
            float pulse = glowPulseSpeed > 0f ? 0.7f + 0.3f * Mathf.Sin(t * glowPulseSpeed) : 1f;
            Color c = glowColor;
            c.a = glowColor.a * glowMaxAlpha * pulse * k;
            glowImage.color = c;
            glowRect.anchoredPosition = centre;
            glowRect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        UpdateFlames(tier >= 2, offset, dt, k);
        UpdateHair(tier >= 5, offset, dt, t, breaking);
        UpdateLightning(tier >= 4 && !breaking, centre, dt);
        UpdateExplosion(liveTier, centre, dt);
        lastTier = liveTier;
    }

    // Tier 2: a yellow flame aura rising around the meter.
    private void UpdateFlames(bool on, Vector2 offset, float dt, float intensity)
    {
        if (on != fireWasOn)
        {
            fireWasOn = on;
            for (int i = 0; i < flameImages.Length; i++)
            {
                flameImages[i].gameObject.SetActive(on);
                if (on) RespawnFlame(i, true);
            }
        }
        if (!on || dt <= 0f) return;

        Vector2 origin = boxBasePosition + offset;
        for (int i = 0; i < flameImages.Length; i++)
        {
            flameAge[i] += dt;
            if (flameAge[i] >= flameLife[i]) RespawnFlame(i, false);

            float k = flameAge[i] / flameLife[i]; // 0 = just lit, 1 = gone
            float sway = Mathf.Sin(flameAge[i] * 9f + flameSway[i]) * 8f * k;
            RectTransform fr = flameImages[i].rectTransform;
            fr.anchoredPosition = origin + new Vector2(flameX[i] + sway + flameDx[i] * k * flameRise, flameY[i] + k * flameRise);
            float size = flameSize * flameScale[i] * (1f - 0.75f * k);
            fr.sizeDelta = new Vector2(size, size);

            Color c = Color.Lerp(auraStartColor, auraEndColor, k);
            c.a *= Mathf.Clamp01(1f - k) * 0.9f * intensity;
            flameImages[i].color = c;
        }
    }

    // Tier 5: golden super-saiyan hair standing up from the top edge.
    private void UpdateHair(bool on, Vector2 offset, float dt, float t, bool breaking)
    {
        if (on != hairWasOn)
        {
            hairWasOn = on;
            hairGrow = 0f;
            for (int i = 0; i < hairOuter.Length; i++)
            {
                hairOuter[i].gameObject.SetActive(on);
                hairInner[i].gameObject.SetActive(on);
            }
        }
        if (!on) return;

        hairGrow = Mathf.MoveTowards(hairGrow, breaking ? 0f : 1f, dt * (breaking ? 6f : 3f)); // the hair drops fast when the combo breaks
        Vector2 top = boxBasePosition + offset + new Vector2(0f, meterSize.y - 8f);
        Color inner = Color.Lerp(hairColor, Color.white, 0.6f);
        for (int i = 0; i < hairOuter.Length; i++)
        {
            float flicker = 1f + 0.12f * Mathf.Sin(t * 9f + hairPhase[i]);
            float len = hairLength * hairLen[i] * flicker * hairGrow;
            Vector2 pos = top + new Vector2(hairFrac[i] * meterSize.x, 0f);
            Quaternion rot = Quaternion.Euler(0f, 0f, -hairAngle[i] + Mathf.Sin(t * 7f + hairPhase[i]) * 3f);

            RectTransform o = hairOuter[i].rectTransform;
            o.anchoredPosition = pos; o.localRotation = rot; o.sizeDelta = new Vector2(hairWidth, len);
            hairOuter[i].color = hairColor;
            RectTransform c = hairInner[i].rectTransform;
            c.anchoredPosition = pos; c.localRotation = rot; c.sizeDelta = new Vector2(hairWidth * 0.5f, len * 0.7f);
            hairInner[i].color = inner;
        }
    }

    // Tier 4: random lightning bolts flashing out from the meter's edge.
    private void UpdateLightning(bool on, Vector2 centre, float dt)
    {
        int bolts = boltLife.Length;
        if (!on)
        {
            for (int b = 0; b < bolts; b++) if (boltLife[b] > 0f) HideBolt(b);
            return;
        }
        if (dt <= 0f) return;

        for (int b = 0; b < bolts; b++)
        {
            if (boltLife[b] <= 0f) continue;
            boltAge[b] += dt;
            if (boltAge[b] >= boltLife[b]) { HideBolt(b); continue; }
            Color c = lightningColor;
            c.a = Random.Range(0.55f, 1f); // flicker
            for (int k = 0; k < BoltSegmentCount; k++) boltSegments[b * BoltSegmentCount + k].color = c;
        }

        boltTimer -= dt;
        if (boltTimer > 0f) return;
        boltTimer = lightningInterval * Random.Range(1f, 2f);
        for (int b = 0; b < bolts; b++)
        {
            if (boltLife[b] > 0f) continue;
            StartBolt(b, centre);
            break;
        }
    }

    // Tier 5: the meter explodes (a flash plus sparks) when it is reached, and again every few seconds.
    private void UpdateExplosion(int tier, Vector2 centre, float dt)
    {
        if (tier >= 5)
        {
            if (lastTier < 5) { Explode(); repeatTimer = explosionRepeatSeconds; }
            else if (explosionRepeatSeconds > 0f && dt > 0f)
            {
                repeatTimer -= dt;
                if (repeatTimer <= 0f) { Explode(); repeatTimer = explosionRepeatSeconds; }
            }
        }
        if (burstAge < 0f || dt <= 0f) return;

        burstAge += dt;
        float k = burstAge / explosionSeconds;
        if (k >= 1f)
        {
            burstAge = -1f;
            for (int i = 0; i < burstImages.Length; i++) burstImages[i].gameObject.SetActive(false);
            flashImage.gameObject.SetActive(false);
            return;
        }

        float travel = 1f - (1f - k) * (1f - k); // eased out
        for (int i = 0; i < burstImages.Length; i++)
        {
            RectTransform r = burstImages[i].rectTransform;
            r.anchoredPosition = centre + new Vector2(Mathf.Cos(burstAngle[i]), Mathf.Sin(burstAngle[i])) * (explosionRadius * burstSpeed[i] * travel);
            float size = burstSize[i] * (1f - 0.8f * k);
            r.sizeDelta = new Vector2(size, size);
            Color c = Color.Lerp(auraStartColor, auraEndColor, k);
            c.a = 1f - k;
            burstImages[i].color = c;
        }
        flashRect.anchoredPosition = centre;
        flashRect.localScale = Vector3.one * Mathf.Lerp(1f, 3.4f, travel);
        Color fc = Color.Lerp(Color.white, hairColor, k);
        fc.a = (1f - k) * 0.9f;
        flashImage.color = fc;
    }
}
