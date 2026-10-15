// ---- System notes (moved here from CLAUDE.md) ----
// PixelDisplaySettings.cs
//   Player display / accessibility settings (PlayerPrefs PixelClicker.Setting.*, applied before the first scene): graphics quality
//   preset (QualitySettings), VSync, fullscreen + resolution, UI scale (SavedUIScale takes effect on the next start; UIScale = the
//   value in use: PixelUIKit.CreateCanvas divides the reference resolution by it; the black bars' canvas is not scaled, so
//   PixelHud.BarHeight is in scaled-canvas units and RawBarHeight in bar units), and colour-blind marks (ColorBlind /
//   ColorBlindChanged: PixelLook.colorBlindSides draws a triangle / square / circle on the faces of the red / green / blue pixels,
//   PixelBombMinigame adds R/G/B letters under the wires).
// ---------------------------------------------------
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
    private const string PrefDropGuide = PrefPrefix + "DropGuide";
    private static int dropGuide = -1;

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
        dropGuide = -1;
    }

    // ------------------------------------------------------------------
    // Startup
    // ------------------------------------------------------------------

    /// <summary>Applies the saved quality / VSync / screen mode / resolution once, as the first scene starts.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedOnStart()
    {
        // Only settings the player actually changed are applied; a first run keeps the project's own defaults.
        if (PixelPrefs.HasKey(PrefQuality))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PixelPrefs.GetInt(PrefQuality), 0, QualitySettings.names.Length - 1), true);
        if (PixelPrefs.HasKey(PrefVSync)) QualitySettings.vSyncCount = PixelPrefs.GetInt(PrefVSync) != 0 ? 1 : 0;

        if (PixelPrefs.HasKey(PrefFullscreen) || PixelPrefs.HasKey(PrefResW))
            ApplyScreen(PixelPrefs.GetInt(PrefResW, Screen.width), PixelPrefs.GetInt(PrefResH, Screen.height),
                        PixelPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) != 0);
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
            PixelPrefs.SetInt(PrefQuality, value);
            PixelPrefs.Save();
        }
    }

    public static bool VSync
    {
        get => QualitySettings.vSyncCount > 0;
        set
        {
            QualitySettings.vSyncCount = value ? 1 : 0;
            PixelPrefs.SetInt(PrefVSync, value ? 1 : 0);
            PixelPrefs.Save();
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
        PixelPrefs.SetInt(PrefResW, width);
        PixelPrefs.SetInt(PrefResH, height);
        PixelPrefs.SetInt(PrefFullscreen, fullscreen ? 1 : 0);
        PixelPrefs.Save();
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
        get => Mathf.Clamp(PixelPrefs.GetFloat(PrefUiScale, 1f), UIScaleChoices[0], UIScaleChoices[UIScaleChoices.Length - 1]);
        set
        {
            PixelPrefs.SetFloat(PrefUiScale, value);
            PixelPrefs.Save();
        }
    }

    // ------------------------------------------------------------------
    // Colour-blind marks
    // ------------------------------------------------------------------

    /// <summary>A save file with its own settings was loaded: read the colour-blind setting again.</summary>
    public static void ReloadFromPrefs()
    {
        dropGuide = -1;
        int before = colorBlind;
        colorBlind = -1;
        if ((ColorBlind ? 1 : 0) != before) ColorBlindChanged?.Invoke();
    }

    /// <summary>Accessibility: while an old pixel is held (Pixel Grabbing), a faint line and ring show where it would land if it were let go from a standing position (straight down). On by default.</summary>
    public static bool DropGuide
    {
        get
        {
            if (dropGuide < 0) dropGuide = PixelPrefs.GetInt(PrefDropGuide, 1);
            return dropGuide != 0;
        }
        set
        {
            dropGuide = value ? 1 : 0;
            PixelPrefs.SetInt(PrefDropGuide, dropGuide);
            PixelPrefs.Save();
        }
    }

    /// <summary>Raised when the colour-blind setting changes.</summary>
    public static event System.Action ColorBlindChanged;

    /// <summary>Colour-coded things also get shapes / letters: marks on the red, green and blue pixels, letters on the bomb's wires.</summary>
    public static bool ColorBlind
    {
        get
        {
            if (colorBlind < 0) colorBlind = PixelPrefs.GetInt(PrefColorBlind, 0);
            return colorBlind != 0;
        }
        set
        {
            colorBlind = value ? 1 : 0;
            PixelPrefs.SetInt(PrefColorBlind, colorBlind);
            PixelPrefs.Save();
            ColorBlindChanged?.Invoke();
        }
    }
}
