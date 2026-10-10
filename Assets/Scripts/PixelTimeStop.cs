using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Time Stop (bought as an upgrade in the shop). Press T to freeze the whole game - old pixels, countdowns, timers,
/// minigames, the auto clicker - and press T again to let time run.
///
/// Stopping time costs energy, shown on a meter that only appears while time is stopped (and while it refills). The meter
/// drains slowly while time is stopped; when it runs out time starts again by itself. As soon as time runs again the
/// meter starts refilling, with no delay, and it disappears a moment after it is full. Time can only be stopped again once
/// the meter has refilled a little.
///
/// You can still click the cube while time is stopped: you are paid as normal, and the old pixels your clicks make hang in
/// the air at the cube - a stockpile - and burst out together when time resumes.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelTimeStop : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        IsStopped = false;
        IsSlowed = false;
    }

    [Header("State")]
    [Tooltip("Is Time Stop available? (The shop turns this on when the Time Stop upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool timeStopActive = false;

    [Header("Time Slow (upgrade of Time Stop)")]
    [Tooltip("Is Time Slow available? (The shop turns this on when the Time Slow upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool timeSlowOwned = false;

    [Range(0.03f, 0.9f)]
    [Tooltip("The speed of time while slowed (1 = normal). Lower = slower; meteor pixels crawl enough to be grabbed.")]
    [SerializeField] private float slowTimeScale = 0.15f;

    [Range(0.05f, 1f)]
    [Tooltip("How fast slowing time drains the shared energy meter compared with stopping time (1 = the same).")]
    [SerializeField] private float slowDrainFactor = 0.4f;

    [Tooltip("Text shown under the meter while time is slowed.")]
    [SerializeField] private string slowedText = "Time slowed  (press {key:TimeSlow})";

    [Range(0f, 1f)]
    [Tooltip("How strong the haze is while time is slowed (1 = as strong as when stopped).")]
    [SerializeField] private float slowHazeStrength = 0.4f;

    [Tooltip("Id of the looping sound played while time is slowed (set its clip in PixelAudio's Sounds list).")]
    [SerializeField] private string slowLoopSoundId = "time_slow_loop";

    [Header("Energy")]
    [Min(0.5f)]
    [Tooltip("How many seconds a full meter lets you keep time stopped.")]
    [SerializeField] private float maxStopSeconds = 10f;

    [Min(0.5f)]
    [Tooltip("How many seconds an empty meter takes to refill completely (refilling starts the moment time runs again).")]
    [SerializeField] private float refillSeconds = 30f;

    [Range(0f, 1f)]
    [Tooltip("The meter must have refilled at least this far (0 to 1) before time can be stopped again.")]
    [SerializeField] private float minStartFraction = 0.15f;

    [Min(0f)]
    [Tooltip("How long (seconds) the meter stays on screen after it has refilled, before it disappears.")]
    [SerializeField] private float hideDelaySeconds = 1.5f;

    [Header("Meter and Indicator")]
    [Tooltip("Size of the meter (canvas units).")]
    [SerializeField] private Vector2 meterSize = new Vector2(520f, 26f);

    [Tooltip("Meter fill colour.")]
    [SerializeField] private Color meterColor = new Color(0.45f, 0.8f, 1f, 1f);

    [Tooltip("Meter fill colour when it is nearly empty.")]
    [SerializeField] private Color meterLowColor = new Color(1f, 0.4f, 0.35f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Below this fill (0 to 1) the meter turns the 'low' colour.")]
    [SerializeField] private float lowFraction = 0.25f;

    [Tooltip("Meter background colour.")]
    [SerializeField] private Color meterBackColor = new Color(0f, 0f, 0f, 0.7f);

    [Tooltip("Text shown under the meter while time is stopped.")]
    [SerializeField] private string stoppedText = "Time stopped  (press T)";

    [Tooltip("Text shown under the meter while time can't be stopped yet (it is still refilling).")]
    [SerializeField] private string rechargingText = "Time Stop recharging...";

    [Tooltip("Colour of the indicator text.")]
    [SerializeField] private Color stoppedColor = new Color(0.6f, 0.85f, 1f, 1f);

    [Tooltip("Font size of the indicator text.")]
    [SerializeField] private float textSize = 30f;

    [Tooltip("Gap (reference pixels) between the top black bar and the meter.")]
    [SerializeField] private float textGap = 12f;

    [Header("Haze")]
    [Tooltip("Show a haze over the screen while time is stopped.")]
    [SerializeField] private bool showHaze = true;

    [Tooltip("Colour of the haze (alpha = strength in the middle of the screen).")]
    [SerializeField] private Color hazeColor = new Color(0.55f, 0.7f, 1f, 0.12f);

    [Tooltip("Colour of the haze at the screen edges (alpha = strength there; a vignette).")]
    [SerializeField] private Color hazeEdgeColor = new Color(0.25f, 0.35f, 0.7f, 0.5f);

    [Min(0f)]
    [Tooltip("Seconds the haze takes to fade in and out.")]
    [SerializeField] private float hazeFadeSeconds = 0.35f;

    [Tooltip("Sorting order of the haze canvas (below the other windows).")]
    [SerializeField] private int hazeSortingOrder = -50;

    [Header("Sound")]
    [Tooltip("Id of the looping sound played while time is stopped (set its clip in PixelAudio's Sounds list; it loops). Leave the clip empty for silence.")]
    [SerializeField] private string loopSoundId = "time_stop_loop";

    // ------------------------------------------------------------------

    /// <summary>True while time is stopped (read by the cube, which keeps taking clicks and animating during it).</summary>
    public static bool IsStopped { get; private set; }

    /// <summary>True while time is slowed (Time Slow upgrade, S key).</summary>
    public static bool IsSlowed { get; private set; }

    /// <summary>1 normally; the current time scale while slowed (>0). Used to keep mouse-driven things (grabbing) feeling normal.</summary>
    public static float SlowFactor => IsSlowed ? Mathf.Max(0.02f, Time.timeScale) : 1f;

    /// <summary>Ends Time Stop and Time Slow at once (e.g. before going back to the main menu).</summary>
    public static void EndAll()
    {
        PixelTimeStop ts = PixelFind.First<PixelTimeStop>();
        if (ts == null) return;
        ts.SetStopped(false);
        ts.SetSlowed(false);
    }

    private PixelClicker clicker;
    private GameObject canvasRoot;
    private RectTransform fillRect;
    private Image fillImage;
    private TMP_Text statusLabel;
    private bool stopped, slowed;
    private float baseFixedDelta = 0.02f;
    private float energy = 1f;       // 0..1
    private float fullTimer;          // counts down once the meter is full
    private float timeScaleBefore = 1f;
    private GameObject hazeRoot;
    private CanvasGroup hazeGroup;
    private Texture2D hazeTexture;

    /// <summary>Is Time Stop unlocked (bought)?</summary>
    public bool Active => timeStopActive;

    /// <summary>The player switched Time Stop off with the Toggles window. Saved.</summary>
    public bool UserDisabled
    {
        get => userDisabled;
        set
        {
            userDisabled = value;
            if (userDisabled) { SetStopped(false); SetSlowed(false); }
        }
    }

    private bool userDisabled;

    /// <summary>Is time stopped right now?</summary>
    public bool Stopped => stopped;

    /// <summary>Is time slowed right now?</summary>
    public bool Slowed => slowed;

    /// <summary>Called by the shop when the Time Slow upgrade is bought.</summary>
    public void ActivateSlow() => timeSlowOwned = true;

    /// <summary>Called by the shop (e.g. when a save without the upgrade is loaded).</summary>
    public void DeactivateSlow()
    {
        timeSlowOwned = false;
        SetSlowed(false);
    }

    /// <summary>The meter, 0 (empty) to 1 (full).</summary>
    public float Energy => energy;

    private bool Working => timeStopActive && !userDisabled;

    /// <summary>Called by the shop when the upgrade is bought.</summary>
    public void Activate() => timeStopActive = true;

    /// <summary>Called by the shop (e.g. when a save without the upgrade is loaded).</summary>
    public void Deactivate()
    {
        timeStopActive = false;
        SetStopped(false);
        SetSlowed(false);
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    private void Awake()
    {
        clicker = PixelFind.First<PixelClicker>();
        baseFixedDelta = Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        SetStopped(false);
        SetSlowed(false);
        IsStopped = false;
        IsSlowed = false;
        PixelAudio.StopLoop(slowLoopSoundId);
        if (canvasRoot != null) Destroy(canvasRoot);
        if (hazeRoot != null) Destroy(hazeRoot);
        if (hazeTexture != null) Destroy(hazeTexture);
        PixelAudio.StopLoop(loopSoundId);
    }

    private void Update()
    {
        if (!Working)
        {
            if (stopped) SetStopped(false);
            if (slowed) SetSlowed(false);
            if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false);
            return;
        }

        bool paused = PixelPauseMenu.IsPaused;

        if (!paused && StopKeyPressed())
        {
            if (stopped) SetStopped(false);
            else if (energy >= minStartFraction)
            {
                if (slowed) SetSlowed(false); // stopping replaces slowing
                SetStopped(true);
            }
            else PixelAudio.Play("time_denied");
        }

        if (!paused && timeSlowOwned && !stopped && !PixelFirstPerson.Active && PixelKeys.Pressed(PixelAction.TimeSlow)) // S is "back" in first person mode
        {
            if (slowed) SetSlowed(false);
            else if (energy >= minStartFraction) SetSlowed(true);
            else PixelAudio.Play("time_denied");
        }

        // Something else (the pause menu closing...) put the time scale back: keep time slowed.
        if (slowed && !paused && !PixelPets.PopupOpen && !PixelPauseMenu.GameStopped && !PixelTitleScreen.Showing && !Mathf.Approximately(Time.timeScale, slowTimeScale))
            Time.timeScale = slowTimeScale;

        // The pause menu restores its own saved time scale when it closes; put the freeze back if that happened.
        if (stopped && !PixelPauseMenu.GameStopped && Time.timeScale != 0f)
        {
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
        }

        // The meter drains while time is stopped and refills the moment it runs again (not while the pause menu is open).
        if (!paused)
        {
            float dt = Time.unscaledDeltaTime;
            if (stopped || slowed)
            {
                energy -= dt / maxStopSeconds * (stopped ? 1f : slowDrainFactor);
                if (energy <= 0f)
                {
                    energy = 0f;
                    SetStopped(false); // out of energy: time starts again by itself
                    SetSlowed(false);
                    PixelHints.Trigger("timestop_empty");
                }
            }
            else if (energy < 1f)
            {
                energy = Mathf.Min(1f, energy + dt / refillSeconds);
                if (energy >= 1f) fullTimer = hideDelaySeconds;
            }
            else if (fullTimer > 0f) fullTimer -= dt;
        }

        UpdateMeter();
    }

    private void LateUpdate() => UpdateHaze();

    private void SetStopped(bool value)
    {
        if (value == stopped) return;
        stopped = value;
        IsStopped = value;

        if (stopped)
        {
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else if (!PixelPauseMenu.GameStopped) // if the pause menu is freezing the game, leave that to it
        {
            Time.timeScale = timeScaleBefore;
        }

        if (!stopped && energy >= 1f) fullTimer = hideDelaySeconds;
        if (!stopped) PixelAudio.Stop("time_stop"); // the freeze sound must not play on after time resumes
        PixelAudio.Play(stopped ? "time_stop" : "time_resume");
        if (stopped) PixelHints.Trigger("timestop_first");
        if (stopped) PixelAudio.StartLoop(loopSoundId);
        else PixelAudio.StopLoop(loopSoundId);
    }

    private void SetSlowed(bool value)
    {
        if (value == slowed) return;
        slowed = value;
        IsSlowed = value;

        if (slowed)
        {
            Time.timeScale = slowTimeScale;
            Time.fixedDeltaTime = baseFixedDelta * slowTimeScale; // physics keeps its real-time smoothness while slowed
        }
        else
        {
            Time.fixedDeltaTime = baseFixedDelta;
            if (!PixelPauseMenu.GameStopped && !stopped && !PixelTitleScreen.Showing) Time.timeScale = 1f;
        }

        if (!slowed && energy >= 1f) fullTimer = hideDelaySeconds;
        if (!slowed) PixelAudio.Stop("time_slow");
        PixelAudio.Play(slowed ? "time_slow" : "time_slow_end");
        if (slowed) PixelHints.Trigger("timestop_slow");
        if (slowed) PixelAudio.StartLoop(slowLoopSoundId);
        else PixelAudio.StopLoop(slowLoopSoundId);
    }

    private bool StopKeyPressed()
    {
        return PixelKeys.Pressed(PixelAction.TimeStop); // rebindable in Settings
    }

    // ------------------------------------------------------------------
    // The haze
    // ------------------------------------------------------------------

    private void UpdateHaze()
    {
        float target = !showHaze ? 0f : stopped ? 1f : slowed ? slowHazeStrength : 0f;
        if (hazeRoot == null)
        {
            if (target <= 0f) return;
            BuildHaze();
        }

        float step = hazeFadeSeconds > 0f ? Time.unscaledDeltaTime / hazeFadeSeconds : 1f;
        hazeGroup.alpha = Mathf.MoveTowards(hazeGroup.alpha, target, step);
        bool visible = hazeGroup.alpha > 0.001f;
        if (hazeRoot.activeSelf != visible) hazeRoot.SetActive(visible);
    }

    private void BuildHaze()
    {
        // A small radial gradient (centre colour -> edge colour) stretched over the whole screen.
        const int size = 64;
        hazeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.2f);
                hazeTexture.SetPixel(x, y, Color.Lerp(hazeColor, hazeEdgeColor, d * d));
            }
        }
        hazeTexture.Apply();

        hazeRoot = PixelUIKit.CreateCanvas("Time Stop Haze", hazeSortingOrder, new Vector2(1920f, 1080f), false);
        hazeGroup = hazeRoot.AddComponent<CanvasGroup>();
        hazeGroup.alpha = 0f;
        hazeGroup.blocksRaycasts = false;
        hazeGroup.interactable = false;

        GameObject img = new GameObject("Haze", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(hazeRoot.transform, false);
        RawImage raw = img.GetComponent<RawImage>();
        raw.texture = hazeTexture;
        raw.raycastTarget = false;
        RectTransform r = raw.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------
    // The meter
    // ------------------------------------------------------------------

    private void UpdateMeter()
    {
        // Shown while time is stopped, while it refills, and for a moment after it is full.
        bool visible = stopped || slowed || energy < 1f || fullTimer > 0f;
        if (!visible)
        {
            if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false);
            return;
        }

        EnsureMeter();
        if (!canvasRoot.activeSelf) canvasRoot.SetActive(true);

        fillRect.anchorMax = new Vector2(Mathf.Clamp01(energy), 1f);
        fillImage.color = energy <= lowFraction ? meterLowColor : meterColor;

        PixelUIKit.SetText(statusLabel, stopped ? PixelKeys.Replace(stoppedText) : slowed ? PixelKeys.Replace(slowedText)
                           : energy < minStartFraction ? rechargingText : "");
    }

    private void EnsureMeter()
    {
        if (canvasRoot != null) return;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;

        canvasRoot = PixelUIKit.CreateCanvas("Time Stop Meter", 5, new Vector2(1920f, 1080f), false);

        GameObject back = new GameObject("Meter", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(canvasRoot.transform, false);
        back.GetComponent<Image>().color = meterBackColor;
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 1f);
        br.sizeDelta = meterSize;
        br.anchoredPosition = new Vector2(0f, -(bar + textGap));

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(back.transform, false);
        fillImage = fill.GetComponent<Image>();
        fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fillRect.pivot = new Vector2(0f, 0.5f);

        statusLabel = PixelUIKit.CreateText(font, canvasRoot.transform, "Status", "", textSize, TextAlignmentOptions.Top,
                                            FontStyles.Bold, stoppedColor);
        RectTransform lr = statusLabel.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.anchoredPosition = new Vector2(0f, -(bar + textGap + meterSize.y + 6f));
        lr.sizeDelta = new Vector2(0f, textSize * 1.4f);
    }
}
