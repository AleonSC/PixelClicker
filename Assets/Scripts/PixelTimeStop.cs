using TMPro;
using UnityEngine;

/// <summary>
/// Time Stop (bought as an upgrade in the shop). Press T to freeze the whole game - old pixels, countdowns, timers,
/// minigames, the auto clicker - and press T again to let time run. Clicks are ignored while time is stopped (just like
/// the pause menu). Can be switched off in the Toggles window.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelTimeStop : MonoBehaviour
{
    [Header("State")]
    [Tooltip("Is Time Stop available? (The shop turns this on when the Time Stop upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool timeStopActive = false;

    [Header("Controls")]
    [Tooltip("Key that stops and resumes time.")]
    [SerializeField] private KeyCode stopKey = KeyCode.T;

    [Header("Indicator")]
    [Tooltip("Text shown at the top of the screen while time is stopped.")]
    [SerializeField] private string stoppedText = "Time stopped  (press T)";

    [Tooltip("Colour of the indicator text.")]
    [SerializeField] private Color stoppedColor = new Color(0.6f, 0.85f, 1f, 1f);

    [Tooltip("Font size of the indicator text.")]
    [SerializeField] private float textSize = 34f;

    [Tooltip("Gap (reference pixels) between the top black bar and the indicator.")]
    [SerializeField] private float textGap = 12f;

    private PixelClicker clicker;
    private GameObject canvasRoot;
    private bool stopped;
    private float timeScaleBefore = 1f;

    /// <summary>Is Time Stop unlocked (bought)?</summary>
    public bool Active => timeStopActive;

    /// <summary>The player switched Time Stop off with the Toggles window. Saved.</summary>
    public bool UserDisabled
    {
        get => userDisabled;
        set
        {
            userDisabled = value;
            if (userDisabled) SetStopped(false);
        }
    }

    private bool userDisabled;

    /// <summary>Is time stopped right now?</summary>
    public bool Stopped => stopped;

    private bool Working => timeStopActive && !userDisabled;

    /// <summary>Called by the shop when the upgrade is bought.</summary>
    public void Activate() => timeStopActive = true;

    /// <summary>Called by the shop (e.g. when a save without the upgrade is loaded).</summary>
    public void Deactivate()
    {
        timeStopActive = false;
        SetStopped(false);
    }

    private void Awake() => clicker = PixelFind.First<PixelClicker>();

    private void OnDestroy()
    {
        SetStopped(false);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        if (!Working)
        {
            if (stopped) SetStopped(false);
            return;
        }

        if (!PixelPauseMenu.IsPaused && StopKeyPressed()) SetStopped(!stopped);

        // The pause menu restores its own saved time scale when it closes; put the freeze back if that happened.
        if (stopped && !PixelPauseMenu.GameStopped && Time.timeScale != 0f)
        {
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void SetStopped(bool value)
    {
        if (value == stopped) return;
        stopped = value;

        if (stopped)
        {
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
        else if (!PixelPauseMenu.GameStopped) // if the pause menu is freezing the game, leave that to it
        {
            Time.timeScale = timeScaleBefore;
        }

        if (stopped) EnsureIndicator();
        if (canvasRoot != null) canvasRoot.SetActive(stopped);
        PixelAudio.Play(stopped ? "time_stop" : "time_resume");
    }

    private bool StopKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(stopKey);
#endif
    }

    private void EnsureIndicator()
    {
        if (canvasRoot != null) return;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();

        canvasRoot = PixelUIKit.CreateCanvas("Time Stop Indicator", 5, new Vector2(1920f, 1080f), false);
        TMP_Text label = PixelUIKit.CreateText(clicker != null ? clicker.UIFont : null, canvasRoot.transform, "Label", stoppedText,
                                               textSize, TextAlignmentOptions.Top, FontStyles.Bold, stoppedColor);
        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        rt.anchoredPosition = new Vector2(0f, -(bar + textGap));
        rt.sizeDelta = new Vector2(0f, textSize * 1.4f);
    }
}
