using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Combo meter (bought as an upgrade in the shop). Every quick manual click builds the combo; each combo step adds a
/// bit to the click multiplier, up to a maximum that the upgrade's level raises. If you stop clicking for a moment
/// the combo drops back to zero. A small meter above the bottom-centre of the screen shows the combo, the multiplier
/// and the time left before it breaks.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelCombo : MonoBehaviour
{
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
    [Tooltip("The highest multiplier the combo can reach. The shop's upgrade levels set this.")]
    [SerializeField] private float maxMultiplier = 1.5f;

    [Header("Combo Rules")]
    [Min(0.1f)]
    [Tooltip("Seconds you have after a click to click again before the combo breaks.")]
    [SerializeField] private float comboWindowSeconds = 1.2f;

    [Min(0f)]
    [Tooltip("How much each combo click adds to the multiplier (0.05 = +5% per click).")]
    [SerializeField] private float multiplierPerClick = 0.05f;

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

    [Tooltip("Text. {0} = combo count, {1} = multiplier.")]
    [SerializeField] private string meterFormat = "Combo {0}   x{1}";

    [Tooltip("Text when the combo is at its maximum. {0} = combo count, {1} = multiplier.")]
    [SerializeField] private string maxFormat = "MAX Combo {0}   x{1}";

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

    [Header("Combo Effects (by combo count)")]
    [Tooltip("Master switch for the glow / shake / fire effects below.")]
    [SerializeField] private bool effectsEnabled = true;

    [Min(0)]
    [Tooltip("Combo count at which the meter starts to glow (0 = never).")]
    [SerializeField] private int glowAtCombo = 100;

    [Tooltip("Colour of the glow around the meter.")]
    [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.25f, 1f);

    [Min(1f)]
    [Tooltip("How far the glow reaches past the meter (canvas units).")]
    [SerializeField] private float glowSize = 34f;

    [Range(0f, 1f)]
    [Tooltip("Strongest the glow gets while pulsing.")]
    [SerializeField] private float glowMaxAlpha = 0.9f;

    [Min(0f)]
    [Tooltip("How fast the glow pulses (0 = steady).")]
    [SerializeField] private float glowPulseSpeed = 4f;

    [Min(0)]
    [Tooltip("Combo count at which the meter starts to shake (0 = never).")]
    [SerializeField] private int shakeAtCombo = 500;

    [Min(0f)]
    [Tooltip("How far the meter jitters (canvas units).")]
    [SerializeField] private float shakeAmount = 4f;

    [Min(0f)]
    [Tooltip("How far the meter tilts while shaking (degrees).")]
    [SerializeField] private float shakeTilt = 1.5f;

    [Min(0)]
    [Tooltip("Combo count at which the meter catches fire (0 = never).")]
    [SerializeField] private int fireAtCombo = 1000;

    [Range(4, 120)]
    [Tooltip("How many flames burn at once.")]
    [SerializeField] private int flameCount = 44;

    [Min(0.1f)]
    [Tooltip("How long a flame lives (seconds).")]
    [SerializeField] private float flameLifeSeconds = 0.8f;

    [Min(1f)]
    [Tooltip("How high a flame rises over its life (canvas units).")]
    [SerializeField] private float flameRise = 90f;

    [Min(4f)]
    [Tooltip("Size of a new flame (canvas units); it shrinks as it rises.")]
    [SerializeField] private float flameSize = 44f;

    [Tooltip("Colour of a new flame (hot).")]
    [SerializeField] private Color flameStartColor = new Color(1f, 0.95f, 0.4f, 1f);

    [Tooltip("Colour of a flame at the end of its life (cool).")]
    [SerializeField] private Color flameEndColor = new Color(1f, 0.2f, 0.05f, 1f);

    /// <summary>Raised every time the combo grows (the new count). Used by the stats.</summary>
    public static event System.Action<int> ComboReached;

    private int combo;
    private float timeLeft;
    private float pop;
    private GameObject canvasRoot;
    private CanvasGroup group;
    private TMP_Text label;
    private Image barFill;
    private RectTransform barFillRect;

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

    /// <summary>Current multiplier (1 when there is no combo or the meter is off).</summary>
    public double Multiplier => Working ? System.Math.Min(maxMultiplier, 1d + combo * multiplierPerClick) : 1d;

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
        if (glowSprite != null) Destroy(glowSprite);
        if (flameSprite != null) Destroy(flameSprite);
        if (glowTexture != null) Destroy(glowTexture);
        if (flameTexture != null) Destroy(flameTexture);
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

    private void AddClick(bool automatic)
    {
        if (!Working || (automatic && !autoClicksCount)) return;
        combo++;
        ComboReached?.Invoke(combo);
        timeLeft = comboWindowSeconds;
        pop = 1f;
    }

    private void Update()
    {
        // The manual-click bonus the clicker applies to every payout.
        clicker.ManualClickBonus = Multiplier;

        if (combo > 0)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) combo = 0;
        }
        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime * 6f);
        idleSeconds += Time.deltaTime;
        Apply();
    }

    private int shownCombo = -1;
    private double shownMultiplier = -1d;
    private bool shownAtMax;

    private void Apply()
    {
        if (canvasRoot == null) return;
        bool visible = Working && showMeter;
        if (canvasRoot.activeSelf != visible) canvasRoot.SetActive(visible);
        if (!visible) return;

        bool atMax = Multiplier >= maxMultiplier - 0.0001f && combo > 0;
        double multiplierNow = Multiplier;
        if (combo != shownCombo || multiplierNow != shownMultiplier || atMax != shownAtMax)
        {
            shownCombo = combo;
            shownMultiplier = multiplierNow;
            shownAtMax = atMax;
            label.text = string.Format(atMax ? maxFormat : meterFormat, combo, multiplierNow.ToString("0.##"));
        }
        label.color = atMax ? maxColor : textColor;
        label.rectTransform.localScale = Vector3.one * (1f + popScale * pop);

        float fill = combo > 0 ? Mathf.Clamp01(timeLeft / comboWindowSeconds) : 0f;
        barFillRect.anchorMax = new Vector2(fill, 1f);
        barFill.color = atMax ? maxColor : barColor;

        // Fully visible while a combo runs, dimmed when idle, then faded out after a while without clicks.
        bool hidden = combo <= 0 && hideAfterSeconds > 0f && idleSeconds >= hideAfterSeconds;
        float targetAlpha = combo > 0 ? 1f : hidden ? 0f : idleAlpha;
        group.alpha = targetAlpha > group.alpha ? targetAlpha : Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime / fadeSeconds);
        // Always sit just above the bottom black bar (it may not have existed yet when the meter was built, or its size may change).
        boxBasePosition = new Vector2(horizontalOffset, meterBottomMargin + (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f));
        UpdateEffects();
    }

    private void BuildMeter()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelCombo Canvas", sortingOrder, referenceResolution, false);
        group = canvasRoot.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        GameObject box = new GameObject("Combo Meter", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(canvasRoot.transform, false);
        Image bg = box.GetComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = meterSize;
        br.anchoredPosition = new Vector2(horizontalOffset, meterBottomMargin + (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f)); // above the bottom bar
        boxRect = br;
        boxBasePosition = br.anchoredPosition;

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
    // Effects: glow (soft halo), shake (jitter + tilt), fire (rising flames)
    // ------------------------------------------------------------------

    private RectTransform boxRect;
    private Vector2 boxBasePosition;
    private Image glowImage;
    private RectTransform glowRect;
    private Texture2D glowTexture, flameTexture;
    private Sprite glowSprite, flameSprite;
    private RectTransform flameRoot;
    private Image[] flameImages;
    private float[] flameAge, flameLife, flameX, flameSway, flameScale;
    private bool fireWasOn;

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

        GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(canvasRoot.transform, false);
        glowImage = glow.GetComponent<Image>();
        glowImage.sprite = glowSprite;
        glowImage.raycastTarget = false;
        glowRect = glow.GetComponent<RectTransform>();
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.sizeDelta = new Vector2(w, h);
        glow.SetActive(false);

        // Flames: a pool of soft round sprites that rise from the top of the meter.
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

        GameObject rootGo = new GameObject("Flames", typeof(RectTransform));
        rootGo.transform.SetParent(canvasRoot.transform, false);
        flameRoot = rootGo.GetComponent<RectTransform>();
        flameRoot.anchorMin = flameRoot.anchorMax = new Vector2(0.5f, 0f);
        flameRoot.pivot = new Vector2(0.5f, 0f);
        flameRoot.sizeDelta = Vector2.zero;
        flameRoot.anchoredPosition = Vector2.zero;

        int count = Mathf.Max(1, flameCount);
        flameImages = new Image[count];
        flameAge = new float[count]; flameLife = new float[count]; flameX = new float[count];
        flameSway = new float[count]; flameScale = new float[count];
        for (int i = 0; i < count; i++)
        {
            GameObject f = new GameObject("Flame", typeof(RectTransform), typeof(Image));
            f.transform.SetParent(flameRoot, false);
            Image img = f.GetComponent<Image>();
            img.sprite = flameSprite;
            img.raycastTarget = false;
            RectTransform fr = f.GetComponent<RectTransform>();
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0f);
            fr.pivot = new Vector2(0.5f, 0.5f);
            flameImages[i] = img;
            f.SetActive(false);
        }

        // Behind the meter itself: halo first, then flames, then the meter on top.
        glow.transform.SetSiblingIndex(0);
        rootGo.transform.SetSiblingIndex(1);
    }

    private void RespawnFlame(int i, bool stagger)
    {
        flameLife[i] = flameLifeSeconds * Random.Range(0.6f, 1.1f);
        flameAge[i] = stagger ? Random.value * flameLife[i] : 0f;
        flameX[i] = Random.Range(-0.46f, 0.46f) * meterSize.x;
        flameSway[i] = Random.Range(0f, 6.28f);
        flameScale[i] = Random.Range(0.7f, 1.2f);
    }

    /// <summary>Runs every frame while the meter shows.</summary>
    private void UpdateEffects()
    {
        if (glowImage == null) return;
        bool on = effectsEnabled && combo > 0;
        bool glowOn = on && glowAtCombo > 0 && combo >= glowAtCombo;
        bool shakeOn = on && shakeAtCombo > 0 && combo >= shakeAtCombo;
        bool fireOn = on && fireAtCombo > 0 && combo >= fireAtCombo;
        bool paused = PixelPauseMenu.IsPaused;
        float t = Time.unscaledTime;

        // Shake the meter (the halo follows it).
        Vector2 offset = Vector2.zero;
        float tilt = 0f;
        if (shakeOn && !paused)
        {
            offset = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * shakeAmount;
            tilt = Random.Range(-1f, 1f) * shakeTilt;
        }
        else if (shakeOn)
        {
            offset = boxRect.anchoredPosition - boxBasePosition;
            tilt = boxRect.localEulerAngles.z > 180f ? boxRect.localEulerAngles.z - 360f : boxRect.localEulerAngles.z;
        }
        boxRect.anchoredPosition = boxBasePosition + offset;
        boxRect.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // Glow: a pulsing halo behind the meter.
        if (glowImage.gameObject.activeSelf != glowOn) glowImage.gameObject.SetActive(glowOn);
        if (glowOn)
        {
            float pulse = glowPulseSpeed > 0f ? 0.7f + 0.3f * Mathf.Sin(t * glowPulseSpeed) : 1f;
            Color c = glowColor;
            c.a = glowColor.a * glowMaxAlpha * pulse;
            glowImage.color = c;
            glowRect.anchoredPosition = boxBasePosition + offset + new Vector2(0f, meterSize.y * 0.5f);
            glowRect.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // Fire: flames rise from the top edge of the meter.
        if (fireOn != fireWasOn)
        {
            fireWasOn = fireOn;
            for (int i = 0; i < flameImages.Length; i++)
            {
                flameImages[i].gameObject.SetActive(fireOn);
                if (fireOn) RespawnFlame(i, true);
            }
        }
        if (!fireOn || paused) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        Vector2 top = boxBasePosition + offset + new Vector2(0f, meterSize.y - 4f);
        for (int i = 0; i < flameImages.Length; i++)
        {
            flameAge[i] += dt;
            if (flameAge[i] >= flameLife[i]) RespawnFlame(i, false);

            float k = flameAge[i] / flameLife[i]; // 0 = just lit, 1 = gone
            float sway = Mathf.Sin(flameAge[i] * 9f + flameSway[i]) * 10f * k;
            RectTransform fr = flameImages[i].rectTransform;
            fr.anchoredPosition = top + new Vector2(flameX[i] + sway, k * flameRise);
            float size = flameSize * flameScale[i] * (1f - 0.75f * k);
            fr.sizeDelta = new Vector2(size, size);

            Color c = Color.Lerp(flameStartColor, flameEndColor, k);
            c.a *= Mathf.Clamp01(1f - k) * 0.9f;
            flameImages[i].color = c;
        }
    }
}
