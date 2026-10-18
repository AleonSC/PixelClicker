// ---- System notes (moved here from CLAUDE.md) ----
// PixelDayNight.cs
//   Day / night cycle (added by PixelClicker.Awake; disableDayNight): TimeOfDay 0..1 (0 midnight, 0.5 noon; a new game starts at 0.38)
//   advances with SCALED time (stops while paused / Time Stop) over LengthMinutes (Settings > Interface 'Length of a day': 4 / 8 / 15
//   / 30, default 8; switch 'Day / night cycle', default on; PlayerPrefs PixelClicker.Setting.DayNight / DayNightLength, per save) and
//   drives the scene's sun (RenderSettings.sun or the first directional light: it rises on one side of the scene, passes OVERHEAD at noon and sets on the other (an arc in the vertical plane turned by the scene sun's own yaw, sunLean 0 = 0.18 tilts it a little); the moon takes the same arc back, a bit lower; warm near the
//   horizon, moonColor at night, intensity down to minSunIntensity 0.3), the ambient light (minAmbient 0.55) and the sky material's
//   _Tint / _SkyTint (nightSkyColor, duskSkyColor); everything is restored when switched off. Night never goes fully dark.
//   PixelDayNight.Level (0 night .. 1 day) is there for other systems (not used yet). The time of day is the saved stat counter
//   daynight.time (x10000).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Day / night cycle (added by <c>PixelClicker.Awake</c>): a time of day (0 = midnight, 0.5 = noon) advances while the game runs (so it stops when the
/// game is paused or Time Stop is on) and drives the scene's sun (it swings across the sky, turns warm at dawn and dusk and bluish at night, and
/// gets dimmer), the ambient light and the sky's tint. Night never goes fully dark (<c>minSunIntensity</c> / <c>minAmbient</c>) so the game stays readable.
/// The time of day is saved with the game (stat counter <c>daynight.time</c>). Settings > Interface: switch it off (the scene goes back to how it was) and
/// choose how long a full day lasts. <see cref="DayLevel"/> (0 night .. 1 day) is there for other systems.
/// </summary>
public class PixelDayNight : MonoBehaviour
{
    [Header("Day / night")]
    [Tooltip("Turns the whole cycle off for good (the Settings switch is then ignored).")]
    [SerializeField] private bool disableDayNight = false;

    [Range(0f, 1f)]
    [Tooltip("Time of day a brand-new game starts at (0 = midnight, 0.5 = noon). 0 = the coded default (0.38, morning).")]
    [SerializeField] private float startTimeOfDay = 0f;

    [Min(0f)]
    [Tooltip("The sun's weakest strength at night, as a fraction of its normal strength. 0 = the coded default (0.3).")]
    [SerializeField] private float minSunIntensity = 0f;

    [Min(0f)]
    [Tooltip("The ambient light's weakest strength at night, as a fraction of normal. 0 = the coded default (0.55).")]
    [SerializeField] private float minAmbient = 0f;

    [Min(0f)]
    [Tooltip("How far the sun's path leans to one side, so noon shadows are not dead straight (0 = straight overhead, 1 = leaning as far as it is high). 0 = the coded default (0.18).")]
    [SerializeField] private float sunLean = 0f;

    [Tooltip("Colour multiplier of the sky at night (0.5 grey sky tint x this).")]
    [SerializeField] private Color nightSkyColor = new Color(0.4f, 0.45f, 0.85f, 1f);

    [Tooltip("Colour multiplier of the sky at dawn and dusk.")]
    [SerializeField] private Color duskSkyColor = new Color(1.3f, 0.85f, 0.7f, 1f);

    [Tooltip("Colour of the sun at night (moonlight) and at dawn / dusk.")]
    [SerializeField] private Color moonColor = new Color(0.55f, 0.65f, 1f, 1f);
    [SerializeField] private Color duskSunColor = new Color(1f, 0.6f, 0.35f, 1f);

    private const string PrefOn = "PixelClicker.Setting.DayNight", PrefLength = "PixelClicker.Setting.DayNightLength";
    private const string TimeKey = "daynight.time";

    /// <summary>Minutes a full day lasts, by the Settings choice.</summary>
    public static readonly float[] LengthMinutes = { 4f, 8f, 15f, 30f };
    public static readonly string[] LengthNames = { "4 minutes", "8 minutes", "15 minutes", "30 minutes" };

    private static int onCache = -1, lengthCache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { onCache = lengthCache = -1; Level = 1f; Instance = null; }

    public static PixelDayNight Instance { get; private set; }

    /// <summary>Settings > Interface: the cycle is on (default on).</summary>
    public static bool Enabled
    {
        get { if (onCache < 0) onCache = PixelPrefs.GetInt(PrefOn, 1); return onCache != 0; }
        set { onCache = value ? 1 : 0; PixelPrefs.SetInt(PrefOn, onCache); }
    }

    /// <summary>Settings > Interface: index into <see cref="LengthMinutes"/> (default 8 minutes).</summary>
    public static int LengthChoice
    {
        get { if (lengthCache < 0) lengthCache = Mathf.Clamp(PixelPrefs.GetInt(PrefLength, 1), 0, LengthMinutes.Length - 1); return lengthCache; }
        set { lengthCache = Mathf.Clamp(value, 0, LengthMinutes.Length - 1); PixelPrefs.SetInt(PrefLength, lengthCache); }
    }

    /// <summary>After a save file's settings were loaded.</summary>
    public static void ReloadFromPrefs() { onCache = lengthCache = -1; }

    /// <summary>0 = deep night, 1 = full day (1 while the cycle is off).</summary>
    public static float Level { get; private set; } = 1f;

    /// <summary>The time of day, 0..1 (0 = midnight, 0.5 = noon).</summary>
    public static float TimeOfDay { get; private set; } = 0.38f;

    private Light sun;
    private float baseIntensity, baseYaw, baseAmbientIntensity;
    private Color baseSunColor, baseAmbientLight;
    private bool captured, applied, timeLoaded;
    private float saveTimer;
    private Material tintedSky;
    private Color tintedSkyOriginal;
    private string tintProperty;

    private void Awake() { Instance = this; }

    private void OnDestroy() { if (Instance == this) Instance = null; Restore(); }

    private void LoadTime()
    {
        timeLoaded = true;
        double saved = PixelStats.Total(TimeKey);
        TimeOfDay = saved > 0d ? Mathf.Repeat((float)(saved / 10000d), 1f) : (startTimeOfDay > 0f ? startTimeOfDay : 0.38f);
    }

    private float nextSunSearch;

    private void Capture()
    {
        sun = RenderSettings.sun;
        if (sun == null)
        {
            if (Time.unscaledTime < nextSunSearch) return;   // (a scene with no directional light: look again once a second, not every frame)
            nextSunSearch = Time.unscaledTime + 1f;
            foreach (Light l in PixelFind.All<Light>())   // (the version-safe search: FindObjectsByType alone broke older Unity)
                if (l != null && l.type == LightType.Directional) { sun = l; break; }
        }
        if (sun == null) return;
        captured = true;
        baseIntensity = sun.intensity;
        baseSunColor = sun.color;
        baseYaw = sun.transform.eulerAngles.y;
        baseAmbientIntensity = RenderSettings.ambientIntensity;
        baseAmbientLight = RenderSettings.ambientLight;
    }

    private void Restore()
    {
        if (!captured || !applied) return;
        applied = false;
        if (sun != null) { sun.intensity = baseIntensity; sun.color = baseSunColor; }
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.ambientLight = baseAmbientLight;
        RestoreSky();
        Level = 1f;
    }

    private void RestoreSky()
    {
        if (tintedSky != null && !string.IsNullOrEmpty(tintProperty)) tintedSky.SetColor(tintProperty, tintedSkyOriginal);
        tintedSky = null;
    }

    private void LateUpdate()
    {
        if (disableDayNight) return;
        if (!timeLoaded) LoadTime();
        if (!Enabled)
        {
            Restore();
            return;
        }
        if (!captured) { Capture(); if (!captured) return; }

        float minutes = LengthMinutes[LengthChoice];
        TimeOfDay = Mathf.Repeat(TimeOfDay + Time.deltaTime / (minutes * 60f), 1f);   // (scaled time: paused / Time Stop = frozen)

        float s = Mathf.Sin((TimeOfDay - 0.25f) * Mathf.PI * 2f);     // -1 midnight, 0 sunrise / sunset, 1 noon
        float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.15f, 0.35f, s));
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(s) / 0.35f);
        Level = day;

        // Sun: climbs from one horizon, passes OVERHEAD at noon and sets on the other side; the moon takes the same arc back (a little lower). Warm near the horizon, moonlight at night.
        float phase = Mathf.Repeat(TimeOfDay - 0.25f, 1f);            // 0 sunrise .. 0.5 sunset .. 1 next sunrise
        bool daytime = phase < 0.5f;
        float arc = (daytime ? phase : phase - 0.5f) * Mathf.PI * 2f;   // 0..pi along the arc
        Vector3 toSun = new Vector3((daytime ? -1f : 1f) * Mathf.Cos(arc), 0.14f + (daytime ? 1.03f : 0.8f) * Mathf.Sin(arc), sunLean > 0f ? sunLean : 0.18f).normalized;   // (never below the horizon: no light from underneath)
        sun.transform.rotation = Quaternion.Euler(0f, baseYaw, 0f) * Quaternion.LookRotation(-toSun, Vector3.up);
        Color sunColor = Color.Lerp(moonColor, baseSunColor, day);
        sunColor = Color.Lerp(sunColor, duskSunColor, dusk * 0.7f);
        sun.color = sunColor;
        sun.intensity = baseIntensity * Mathf.Lerp(minSunIntensity > 0f ? minSunIntensity : 0.3f, 1f, day);
        RenderSettings.ambientIntensity = baseAmbientIntensity * Mathf.Lerp(minAmbient > 0f ? minAmbient : 0.55f, 1f, day);
        RenderSettings.ambientLight = Color.Lerp(baseAmbientLight * new Color(0.6f, 0.65f, 1f, 1f), baseAmbientLight, day);

        ApplySkyTint(day, dusk);
        applied = true;

        saveTimer -= Time.unscaledDeltaTime;
        if (saveTimer <= 0f)
        {
            saveTimer = 5f;
            PixelStats.Clear(TimeKey);
            PixelStats.Count(TimeKey, Mathf.Round(TimeOfDay * 10000f) + 1f);
        }
    }

    private void ApplySkyTint(float day, float dusk)
    {
        Material sky = RenderSettings.skybox;
        if (sky == null) { RestoreSky(); return; }
        if (sky != tintedSky)
        {
            RestoreSky();
            tintProperty = sky.HasProperty("_Tint") ? "_Tint" : sky.HasProperty("_SkyTint") ? "_SkyTint" : null;
            if (tintProperty == null) return;
            tintedSky = sky;
            tintedSkyOriginal = sky.GetColor(tintProperty);
        }
        if (tintedSky == null) return;
        Color mul = Color.Lerp(nightSkyColor, Color.white, day);
        mul = Color.Lerp(mul, duskSkyColor, dusk * 0.8f);
        Color c = new Color(tintedSkyOriginal.r * mul.r, tintedSkyOriginal.g * mul.g, tintedSkyOriginal.b * mul.b, tintedSkyOriginal.a);
        tintedSky.SetColor(tintProperty, c);
    }
}
