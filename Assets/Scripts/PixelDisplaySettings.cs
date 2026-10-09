using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Display and accessibility settings the player can change in the pause menu: graphics quality preset, VSync, fullscreen,
/// resolution, UI scale and colour-blind marks. Everything is remembered in PlayerPrefs and applied at startup.
/// </summary>
public static class PixelDisplaySettings
{
    private const string PrefPrefix = "PixelClicker.Setting.";
    private const string PrefQuality = PrefPrefix + "Quality";
    private const string PrefVSync = PrefPrefix + "VSync";
    private const string PrefFullscreen = PrefPrefix + "Fullscreen";
    private const string PrefResW = PrefPrefix + "ResW";
    private const string PrefResH = PrefPrefix + "ResH";
    private const string PrefUiScale = PrefPrefix + "UIScale";
    private const string PrefColorBlind = PrefPrefix + "ColorBlind";

    /// <summary>The UI scales the player can pick (shown as percentages).</summary>
    public static readonly float[] UIScaleChoices = { 0.8f, 0.9f, 1f, 1.1f, 1.2f, 1.3f };

    private static float appliedUiScale = -1f;
    private static int colorBlind = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        appliedUiScale = -1f;
        colorBlind = -1;
        ColorBlindChanged = null;
    }

    // ------------------------------------------------------------------
    // Startup
    // ------------------------------------------------------------------

    /// <summary>Applies the saved quality / VSync / screen mode / resolution once, as the first scene starts.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedOnStart()
    {
        // Only settings the player actually changed are applied; a first run keeps the project's own defaults.
        if (PlayerPrefs.HasKey(PrefQuality))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(PrefQuality), 0, QualitySettings.names.Length - 1), true);
        if (PlayerPrefs.HasKey(PrefVSync)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(PrefVSync) != 0 ? 1 : 0;

        if (PlayerPrefs.HasKey(PrefFullscreen) || PlayerPrefs.HasKey(PrefResW))
            ApplyScreen(PlayerPrefs.GetInt(PrefResW, Screen.width), PlayerPrefs.GetInt(PrefResH, Screen.height),
                        PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) != 0);
    }

    // ------------------------------------------------------------------
    // Graphics quality and VSync
    // ------------------------------------------------------------------

    /// <summary>The quality presets of the project (Project Settings > Quality).</summary>
    public static string[] QualityNames => QualitySettings.names;

    public static int QualityLevel
    {
        get => QualitySettings.GetQualityLevel();
        set
        {
            value = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(value, true);
            PlayerPrefs.SetInt(PrefQuality, value);
            PlayerPrefs.Save();
        }
    }

    public static bool VSync
    {
        get => QualitySettings.vSyncCount > 0;
        set
        {
            QualitySettings.vSyncCount = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefVSync, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------------
    // Fullscreen and resolution
    // ------------------------------------------------------------------

    private static List<Vector2Int> resolutions;

    /// <summary>Every different width x height the screen supports, smallest first.</summary>
    public static List<Vector2Int> Resolutions
    {
        get
        {
            if (resolutions != null) return resolutions;
            resolutions = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
            {
                Vector2Int v = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(v)) resolutions.Add(v);
            }
            Vector2Int now = new Vector2Int(Screen.width, Screen.height);
            if (!resolutions.Contains(now)) resolutions.Add(now);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return resolutions;
        }
    }

    public static bool Fullscreen
    {
        get => Screen.fullScreen;
        set => ApplyScreen(Screen.width, Screen.height, value, true);
    }

    public static Vector2Int Resolution
    {
        get => new Vector2Int(Screen.width, Screen.height);
        set => ApplyScreen(value.x, value.y, Screen.fullScreen, true);
    }

    private static void ApplyScreen(int width, int height, bool fullscreen, bool save = false)
    {
        Screen.SetResolution(Mathf.Max(320, width), Mathf.Max(240, height),
                             fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        if (!save) return;
        PlayerPrefs.SetInt(PrefResW, width);
        PlayerPrefs.SetInt(PrefResH, height);
        PlayerPrefs.SetInt(PrefFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ------------------------------------------------------------------
    // UI scale (applies the next time the game starts, so every window is laid out for it)
    // ------------------------------------------------------------------

    /// <summary>The scale the UI is using right now (fixed when the first canvas is built).</summary>
    public static float UIScale
    {
        get
        {
            if (appliedUiScale < 0f) appliedUiScale = SavedUIScale;
            return appliedUiScale;
        }
    }

    /// <summary>The scale chosen in Settings (used from the next start).</summary>
    public static float SavedUIScale
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(PrefUiScale, 1f), UIScaleChoices[0], UIScaleChoices[UIScaleChoices.Length - 1]);
        set
        {
            PlayerPrefs.SetFloat(PrefUiScale, value);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------------
    // Colour-blind marks
    // ------------------------------------------------------------------

    /// <summary>A save file with its own settings was loaded: read the colour-blind setting again.</summary>
    public static void ReloadFromPrefs()
    {
        int before = colorBlind;
        colorBlind = -1;
        if ((ColorBlind ? 1 : 0) != before) ColorBlindChanged?.Invoke();
    }

    /// <summary>Raised when the colour-blind setting changes.</summary>
    public static event System.Action ColorBlindChanged;

    /// <summary>Colour-coded things also get shapes / letters: marks on the red, green and blue pixels, letters on the bomb's wires.</summary>
    public static bool ColorBlind
    {
        get
        {
            if (colorBlind < 0) colorBlind = PlayerPrefs.GetInt(PrefColorBlind, 0);
            return colorBlind != 0;
        }
        set
        {
            colorBlind = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefColorBlind, colorBlind);
            PlayerPrefs.Save();
            ColorBlindChanged?.Invoke();
        }
    }
}
