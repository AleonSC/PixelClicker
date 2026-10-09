using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skybox styles: replaces the scene's sky with one of several panoramas drawn in code at runtime (no art assets).
/// Picked in the pause menu's Settings ("Sky") or with the small picker left of the title screen's Play button,
/// remembered in PlayerPrefs. "Classic" puts the scene's own skybox (and camera background) back. Uses Unity's
/// Skybox/Panoramic shader (built-in and URP); the shader slot is filled in automatically in the editor so builds keep it.
/// Some skies move: slow rotation, lightning flashes. Added by PixelClicker.Awake if missing.
/// </summary>
public class PixelSkybox : MonoBehaviour, IPixelLookSource
{
    public enum SkyPattern
    {
        Classic,      // the scene's own skybox / camera background
        ClearDay,     // blue sky, sun and fluffy clouds
        Sunset,       // orange-pink horizon, low sun, streaky glowing clouds
        StarryNight,  // navy sky, stars, the Milky Way and a moon
        Nebula,       // deep space all round: coloured clouds and lots of stars
        Aurora,       // night sky with green / purple curtains of light
        Synthwave,    // retro striped sun over a neon grid
        PixelClouds,  // pixel-art sky with blocky clouds
        Storm,        // dark heavy clouds with lightning flashes
        AlienWorld    // strange-coloured sky with a big banded planet and a moon
    }

    [Serializable]
    public class SkyStyle
    {
        [Tooltip("Name shown in the menus (also what is saved).")]
        public string name = "Sky";

        [Tooltip("Which sky is drawn.")]
        public SkyPattern pattern = SkyPattern.Classic;

        [Tooltip("Colour straight up.")]
        public Color topColor = new Color(0.2f, 0.45f, 0.9f);

        [Tooltip("Colour at the horizon.")]
        public Color horizonColor = new Color(0.7f, 0.85f, 1f);

        [Tooltip("Colour below the horizon (ignored by the space skies).")]
        public Color groundColor = new Color(0.25f, 0.27f, 0.25f);

        [Tooltip("Colour of the sun / moon / planet.")]
        public Color sunColor = new Color(1f, 0.95f, 0.8f);

        [Tooltip("Lit side of the clouds.")]
        public Color cloudColor = Color.white;

        [Tooltip("Shaded side of the clouds.")]
        public Color cloudShadowColor = new Color(0.7f, 0.75f, 0.85f);

        [Tooltip("First accent colour (Milky Way, nebula, aurora, grid...).")]
        public Color accentA = new Color(0.4f, 1f, 0.6f);

        [Tooltip("Second accent colour.")]
        public Color accentB = new Color(0.7f, 0.3f, 1f);

        [Tooltip("Sun / moon height above the horizon, in degrees.")]
        public float sunElevation = 25f;

        [Tooltip("Sun / moon direction around you, in degrees (0 = straight ahead of an unrotated camera).")]
        public float sunAzimuth = 0f;

        [Tooltip("Sun / moon size, in degrees across.")]
        public float sunSize = 4f;

        [Range(0f, 1f)]
        [Tooltip("How much of the sky is covered in clouds.")]
        public float cloudAmount = 0.4f;

        [Range(0f, 1f)]
        [Tooltip("How many stars.")]
        public float starAmount = 0f;

        [Tooltip("Panorama width in texels (height is half). Bigger = sharper but slower to make.")]
        public int width = 1024;

        [Tooltip("Pixel-art look (no smoothing between texels).")]
        public bool pixelated = false;

        [Tooltip("Sky brightness.")]
        public float exposure = 1f;

        [Tooltip("How fast the sky turns, in degrees per second.")]
        public float rotateSpeed = 0.5f;

        [Tooltip("Lightning flashes per minute (0 = none).")]
        public float flashesPerMinute = 0f;

        [Tooltip("Random seed. Change it for a different variation.")]
        public int seed = 1;
    }

    [Header("Sky")]
    [Tooltip("The Skybox/Panoramic shader. Filled in automatically in the editor (so builds include it).")]
    [SerializeField] private Shader panoramicShader;

    [Tooltip("Camera whose background becomes the sky. Empty = the main camera.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Also re-light the scene from the new sky (ambient light). Off keeps the scene's lighting as it is.")]
    [SerializeField] private bool updateAmbientLight = false;

    [Tooltip("Width of the small preview pictures used by the menus.")]
    [SerializeField] private int previewWidth = 256;

    [Header("Styles")]
    [Tooltip("Add the built-in skies that are missing from the list (by name).")]
    [SerializeField] private bool addDefaultStyles = true;

    [Tooltip("The skies you can pick, in order. The Classic entry restores the scene's own sky.")]
    [SerializeField] private List<SkyStyle> styles = new List<SkyStyle>();

    [Tooltip("Sky used on a first launch (before the player picks one).")]
    [SerializeField] private string defaultStyle = "Classic";

    /// <summary>The skybox component in the scene (null until it wakes).</summary>
    public static PixelSkybox Instance { get; private set; }

    /// <summary>Raised when the sky changes (passes the new index).</summary>
    public static event Action<int> StyleChanged;

    private const string PrefStyle = "PixelClicker.Setting.Skybox";

    private Material originalSkybox;
    private CameraClearFlags originalClearFlags;
    private Color originalBackground;
    private bool captured;
    private Material activeMaterial;
    private readonly Dictionary<int, Texture2D> panoramas = new Dictionary<int, Texture2D>();
    private readonly Dictionary<int, Texture2D> previews = new Dictionary<int, Texture2D>();
    private int current = -1;
    private float rotation, flash;
    private bool warnedNoShader;

    public int StyleCount => styles.Count;
    public int Current => current;
    public bool Usable => styles.Count > 0;
    public string StyleName(int i) => i >= 0 && i < styles.Count ? styles[i].name : "";
    // The part of the panorama straight ahead (u = 0.25 is +z), from just below the horizon upward.
    public Rect PreviewRect => new Rect(0.125f, 0.42f, 0.25f, 0.5f);

    private void Awake()
    {
        Instance = this;
        if (addDefaultStyles) EnsureDefaultStyles();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (activeMaterial != null) Destroy(activeMaterial);
        foreach (Texture2D t in panoramas.Values) if (t != null) Destroy(t);
        foreach (Texture2D t in previews.Values) if (t != null) Destroy(t);
        panoramas.Clear();
        previews.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (panoramicShader == null)
            {
                panoramicShader = Shader.Find("Skybox/Panoramic");
                if (panoramicShader != null) UnityEditor.EditorUtility.SetDirty(this);
            }
            if (addDefaultStyles) EnsureDefaultStyles();
        };
    }

    [ContextMenu("Add Missing Default Styles")]
    private void AddMissingDefaultsMenu() => EnsureDefaultStyles();
#endif

    private void Start()
    {
        string saved = PlayerPrefs.GetString(PrefStyle, defaultStyle);
        int index = styles.FindIndex(s => s.name == saved);
        SetStyle(index >= 0 ? index : 0);
    }

    // ------------------------------------------------------------------
    // Picking a sky
    // ------------------------------------------------------------------

    /// <summary>Applies sky i and remembers it.</summary>
    public void SetStyle(int index)
    {
        if (styles.Count == 0) return;
        index = ((index % styles.Count) + styles.Count) % styles.Count;
        current = index;
        PlayerPrefs.SetString(PrefStyle, styles[index].name);
        Apply(index);
        StyleChanged?.Invoke(index);
    }

    public void Step(int direction) => SetStyle((current < 0 ? 0 : current) + direction);

    private Camera Cam => targetCamera != null ? targetCamera : Camera.main;

    private void Capture()
    {
        if (captured) return;
        captured = true;
        originalSkybox = RenderSettings.skybox;
        Camera cam = Cam;
        if (cam != null)
        {
            originalClearFlags = cam.clearFlags;
            originalBackground = cam.backgroundColor;
        }
    }

    private void Apply(int index)
    {
        Capture();
        SkyStyle style = styles[index];
        Camera cam = Cam;
        rotation = 0f;
        flash = 0f;

        if (style.pattern == SkyPattern.Classic)
        {
            RenderSettings.skybox = originalSkybox;
            if (cam != null) { cam.clearFlags = originalClearFlags; cam.backgroundColor = originalBackground; }
            RefreshAmbient();
            return;
        }

        Shader shader = panoramicShader != null ? panoramicShader : Shader.Find("Skybox/Panoramic");
        if (shader == null)
        {
            // No panoramic shader in this build: at least give the camera the sky's colour.
            if (!warnedNoShader) Debug.LogWarning("PixelSkybox: the Skybox/Panoramic shader is missing. Select the Pixel Skybox component in the editor once (it fills the shader in), or add the shader to Always Included Shaders.", this);
            warnedNoShader = true;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = style.horizonColor; }
            return;
        }

        if (activeMaterial == null || activeMaterial.shader != shader)
        {
            if (activeMaterial != null) Destroy(activeMaterial);
            activeMaterial = new Material(shader) { name = "Pixel Sky" };
        }
        Material m = activeMaterial;
        m.SetTexture("_MainTex", Panorama(index));
        if (m.HasProperty("_Tint")) m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // 0.5 grey = no tint
        if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", style.exposure);
        if (m.HasProperty("_Rotation")) m.SetFloat("_Rotation", 0f);
        if (m.HasProperty("_Mapping")) m.SetFloat("_Mapping", 1f); // latitude-longitude layout
        if (m.HasProperty("_ImageType")) m.SetFloat("_ImageType", 0f); // full 360 degrees
        if (m.HasProperty("_Layout")) m.SetFloat("_Layout", 0f);
        m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        m.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");

        RenderSettings.skybox = m;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        RefreshAmbient();
    }

    private void RefreshAmbient()
    {
        if (updateAmbientLight) DynamicGI.UpdateEnvironment();
    }

    private void Update()
    {
        if (activeMaterial == null || current < 0 || current >= styles.Count) return;
        if (RenderSettings.skybox != activeMaterial) return; // Classic, or something else took over the sky
        SkyStyle style = styles[current];
        float dt = PixelTimeStop.IsStopped ? 0f : Time.unscaledDeltaTime; // turns on the title screen too, freezes in Time Stop

        if (style.rotateSpeed != 0f && activeMaterial.HasProperty("_Rotation"))
        {
            rotation = Mathf.Repeat(rotation + style.rotateSpeed * dt, 360f);
            activeMaterial.SetFloat("_Rotation", rotation);
        }

        if (style.flashesPerMinute > 0f && activeMaterial.HasProperty("_Exposure"))
        {
            if (dt > 0f && UnityEngine.Random.value < style.flashesPerMinute / 60f * dt) flash = UnityEngine.Random.Range(0.6f, 1f);
            // A quick double flicker as it fades.
            float flicker = flash > 0.35f ? (Mathf.Sin(flash * 60f) > 0f ? 1f : 0.55f) : 1f;
            activeMaterial.SetFloat("_Exposure", style.exposure * (1f + flash * flicker * 2.2f));
            flash = Mathf.Max(0f, flash - dt * 2.5f);
        }
    }

    // ------------------------------------------------------------------
    // Previews (for the menus)
    // ------------------------------------------------------------------

    public Texture PreviewTexture(int index)
    {
        if (index < 0 || index >= styles.Count) return null;
        if (styles[index].pattern == SkyPattern.Classic)
        {
            Capture();
            return null; // shown as the scene's background colour
        }
        if (!previews.TryGetValue(index, out Texture2D tex) || tex == null)
        {
            tex = Draw(styles[index], Mathf.Clamp(previewWidth, 32, 1024));
            previews[index] = tex;
        }
        return tex;
    }

    /// <summary>
    /// The colour of the current sky at the horizon (for the horizon fog). False for the Classic sky (the scene's own),
    /// whose horizon colour isn't known.
    /// </summary>
    public bool TryGetHorizonColor(out Color color)
    {
        color = Color.clear;
        if (current < 0 || current >= styles.Count || styles[current].pattern == SkyPattern.Classic) return false;
        if (activeMaterial == null || RenderSettings.skybox != activeMaterial) return false; // the sky fell back to a plain colour
        SkyStyle s = styles[current];
        color = s.horizonColor * Mathf.Max(0f, s.exposure);
        color.a = 1f;
        return true;
    }

    public Color PreviewColor(int index)
    {
        if (index >= 0 && index < styles.Count && styles[index].pattern == SkyPattern.Classic)
        {
            Capture();
            Material sky = originalSkybox;
            if (originalClearFlags == CameraClearFlags.Skybox && sky != null)
            {
                if (sky.HasProperty("_SkyTint")) return sky.GetColor("_SkyTint");
                if (sky.HasProperty("_Tint")) return sky.GetColor("_Tint");
                return new Color(0.5f, 0.7f, 0.95f);
            }
            Color bg = originalBackground;
            bg.a = 1f;
            return bg;
        }
        return Color.white;
    }

    private Texture2D Panorama(int index)
    {
        if (!panoramas.TryGetValue(index, out Texture2D tex) || tex == null)
        {
            tex = Draw(styles[index], Mathf.Clamp(styles[index].width, 64, 4096));
            panoramas[index] = tex;
        }
        return tex;
    }

    // ------------------------------------------------------------------
    // Drawing a panorama (latitude-longitude, 2:1)
    // ------------------------------------------------------------------

    private static Texture2D Draw(SkyStyle s, int w)
    {
        int h = Mathf.Max(2, w / 2);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = "Sky " + s.name,
            filterMode = s.pixelated ? FilterMode.Point : FilterMode.Bilinear,
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Clamp
        };

        Vector3 sun = Direction(s.sunElevation, s.sunAzimuth);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            // Same mapping as the Skybox/Panoramic shader: v = 1 is straight up, u = 0.25 is +z.
            float v = (y + 0.5f) / h;
            float lat = (1f - v) * Mathf.PI;
            float sinLat = Mathf.Sin(lat), cosLat = Mathf.Cos(lat);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float lon = (0.5f - u) * 2f * Mathf.PI;
                Vector3 dir = new Vector3(sinLat * Mathf.Cos(lon), cosLat, sinLat * Mathf.Sin(lon));
                Color c = Shade(s, dir, sun, sinLat, x, y, w);
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false);
        return tex;
    }

    private static Vector3 Direction(float elevationDeg, float azimuthDeg)
    {
        float el = elevationDeg * Mathf.Deg2Rad, az = azimuthDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
    }

    private static Color Shade(SkyStyle s, Vector3 dir, Vector3 sun, float sinLat, int x, int y, int w)
    {
        float d = Vector3.Dot(dir, sun);
        float res = w / 1024f; // so stars look about the same at any size

        switch (s.pattern)
        {
            case SkyPattern.ClearDay:
            {
                Color c = Gradient(s, dir.y, 0.55f);
                c += s.sunColor * (Mathf.Pow(Mathf.Max(0f, d), 24f) * 0.35f + Mathf.Pow(Mathf.Max(0f, d), 4f) * 0.08f);
                c = Disc(c, s.sunColor * 1.4f, d, s.sunSize);
                return Clouds(c, s, dir, sun, new Vector2(1f, 1f), 0.35f);
            }

            case SkyPattern.Sunset:
            {
                // Warm band hugging the horizon, strongest towards the sun.
                Color c = Gradient(s, dir.y, 0.4f);
                float towards = Mathf.Max(0f, Vector3.Dot(new Vector3(dir.x, 0f, dir.z).normalized, new Vector3(sun.x, 0f, sun.z).normalized));
                float band = Mathf.Exp(-Mathf.Abs(dir.y) * 9f) * (0.4f + 0.6f * towards);
                c = Color.Lerp(c, s.accentA, band * 0.75f);
                c += s.sunColor * (Mathf.Pow(Mathf.Max(0f, d), 10f) * 0.6f + Mathf.Pow(Mathf.Max(0f, d), 120f) * 0.6f);
                c = Disc(c, s.sunColor * 1.3f, d, s.sunSize);
                // Clouds lit pink / orange on the sun side.
                Color lit = Color.Lerp(s.cloudColor, s.accentA, towards * 0.6f);
                return Clouds(c, s, dir, sun, new Vector2(0.35f, 1.6f), 0.25f, lit);
            }

            case SkyPattern.StarryNight:
            {
                Color c = Gradient(s, dir.y, 0.5f);
                if (dir.y > -0.02f)
                {
                    // The Milky Way: a cloudy band along a tilted circle.
                    Vector3 n = new Vector3(0.45f, 0.65f, -0.6f).normalized;
                    float b = Vector3.Dot(dir, n);
                    float band = Mathf.Exp(-b * b * 28f) * Fbm3(dir * 3.5f, 5, s.seed);
                    float dust = Fbm3(dir * 9f, 3, s.seed + 4);
                    c = Color.Lerp(c, s.accentA, Mathf.Clamp01(band * 1.2f - 0.25f) * 0.7f);
                    c = Color.Lerp(c, s.accentB, Mathf.Clamp01(band * dust * 1.5f - 0.4f) * 0.5f);
                    c = Stars(c, s, x, y, sinLat, res, 1f + band * 3f);
                    c = Moon(c, s, dir, d);
                }
                return c;
            }

            case SkyPattern.Nebula:
            {
                Color c = s.topColor;
                float n1 = Fbm3(dir * 2.2f, 5, s.seed);
                float n2 = Fbm3(dir * 3.1f + Vector3.one * 7f, 5, s.seed + 9);
                float n3 = Fbm3(dir * 6f, 3, s.seed + 21);
                c = Color.Lerp(c, s.accentA, Mathf.Clamp01((n1 - 0.45f) * 2.4f) * 0.85f);
                c = Color.Lerp(c, s.accentB, Mathf.Clamp01((n2 - 0.5f) * 2.6f) * 0.75f);
                c = Color.Lerp(c, s.horizonColor, Mathf.Clamp01((n1 * n2 * n3 - 0.18f) * 5f) * 0.6f); // bright cores
                c = Stars(c, s, x, y, sinLat, res, 1f);
                return c;
            }

            case SkyPattern.Aurora:
            {
                Color c = Gradient(s, dir.y, 0.5f);
                if (dir.y > -0.02f)
                {
                    c = Stars(c, s, x, y, sinLat, res, 1f);
                    // Curtains: a wavy lower edge, rays going up, fading with height.
                    Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
                    float wave = Fbm3(flat * 1.6f, 3, s.seed) - 0.5f;
                    float edge = 0.18f + wave * 0.35f;
                    float hgt = dir.y - edge;
                    float rays = 0.55f + 0.45f * Fbm3(flat * 14f + Vector3.up * 3f, 2, s.seed + 5);
                    float glow = hgt > 0f ? Mathf.Exp(-hgt * 5f) : Mathf.Exp(hgt * 60f);
                    float patch = Mathf.Clamp01((Fbm3(flat * 2.4f + Vector3.right * 4f, 3, s.seed + 2) - 0.32f) * 2.5f);
                    float k = glow * rays * patch;
                    Color curtain = Color.Lerp(s.accentA, s.accentB, Mathf.Clamp01(hgt * 3f));
                    c += curtain * k * 0.9f;
                }
                return c;
            }

            case SkyPattern.Synthwave:
            {
                if (dir.y >= 0f)
                {
                    Color c = Gradient(s, dir.y, 0.7f);
                    c = Stars(c, s, x, y, sinLat, res, Mathf.Clamp01(dir.y * 3f));
                    c += s.accentB * Mathf.Exp(-dir.y * 14f) * 0.5f; // horizon glow
                    // Big striped sun: gradient yellow -> pink, stripes cut in the lower half.
                    float ang = Mathf.Acos(Mathf.Clamp(d, -1f, 1f)) * Mathf.Rad2Deg;
                    float r = s.sunSize * 0.5f;
                    if (ang < r)
                    {
                        float local = (dir.y - sun.y) / (r * Mathf.Deg2Rad); // -1 bottom .. 1 top
                        Color sc = Color.Lerp(s.accentA, s.sunColor, Mathf.Clamp01(local * 0.5f + 0.5f));
                        bool cut = local < 0.15f && PixelNoise.Frac((local + 2f) * 4.5f) < Mathf.Lerp(0.05f, 0.5f, Mathf.Clamp01(-local + 0.15f));
                        if (!cut) c = Color.Lerp(c, sc, Mathf.Clamp01((r - ang) * 3f));
                    }
                    else c += s.sunColor * Mathf.Exp(-(ang - r) * 0.25f) * 0.15f;
                    return c;
                }
                else
                {
                    // Neon grid on the ground plane, fading into the distance.
                    float t = 1f / Mathf.Max(0.004f, -dir.y);
                    float gx = dir.x * t * 0.5f, gz = dir.z * t * 0.5f;
                    float lw = 0.03f + t * 0.004f;
                    float lx = Mathf.Abs(PixelNoise.Frac(gx) - 0.5f), lz = Mathf.Abs(PixelNoise.Frac(gz) - 0.5f);
                    float line = Mathf.Clamp01((Mathf.Max(lx, lz) - (0.5f - lw)) / lw);
                    float fade = Mathf.Exp(-t * 0.03f);
                    Color ground = s.groundColor;
                    ground = Color.Lerp(ground, s.accentB, line * fade);
                    return Color.Lerp(s.horizonColor * 0.6f + s.accentB * 0.4f, ground, Mathf.Clamp01(-dir.y * 25f));
                }
            }

            case SkyPattern.PixelClouds:
            {
                // Banded gradient (a few flat steps), a square sun and blocky clouds.
                float step = Mathf.Floor(Mathf.Clamp01(dir.y) * 6f) / 6f;
                Color c = dir.y >= 0f ? Color.Lerp(s.horizonColor, s.topColor, Mathf.Pow(step, 0.7f)) : s.groundColor;
                Vector3 toSun = dir - sun;
                if (Mathf.Abs(toSun.x) < s.sunSize * 0.01f && Mathf.Abs(toSun.y) < s.sunSize * 0.01f && d > 0f) c = s.sunColor;
                if (dir.y > 0.02f)
                {
                    float t = 1f / dir.y;
                    float n = Fbm2(dir.x * t * 0.3f, dir.z * t * 0.3f, 4, s.seed);
                    float th = Mathf.Lerp(0.72f, 0.4f, s.cloudAmount);
                    if (n > th && dir.y > 0.06f)
                    {
                        float under = Fbm2(dir.x * t * 0.3f, dir.z * t * 0.3f - 0.25f, 4, s.seed);
                        c = under > th ? s.cloudColor : s.cloudShadowColor;
                    }
                }
                return c;
            }

            case SkyPattern.Storm:
            {
                Color c = Gradient(s, dir.y, 0.6f);
                return Clouds(c, s, dir, Vector3.up, new Vector2(0.8f, 1.2f), 0.6f);
            }

            case SkyPattern.AlienWorld:
            {
                Color c = Gradient(s, dir.y, 0.5f);
                c = Stars(c, s, x, y, sinLat, res, Mathf.Clamp01(dir.y * 2f) * 0.6f);
                // A big planet with bands, lit from one side.
                float ang = Mathf.Acos(Mathf.Clamp(d, -1f, 1f)) * Mathf.Rad2Deg;
                float r = s.sunSize * 0.5f;
                if (ang < r * 1.25f)
                {
                    Vector3 rel = dir - sun;
                    float band = Mathf.Sin((rel.y * 40f + Fbm3(dir * 25f, 2, s.seed) * 2f));
                    Color pc = Color.Lerp(s.sunColor, s.accentB, band * 0.5f + 0.5f);
                    float lit = Mathf.Clamp01(0.35f + Vector3.Dot(rel.normalized, new Vector3(-0.7f, 0.5f, 0f)) * 0.9f);
                    if (ang < r) c = Color.Lerp(c, pc * (0.35f + 0.75f * lit), Mathf.Clamp01((r - ang) * 2f));
                    else c += s.sunColor * (1f - (ang - r) / (r * 0.25f)) * 0.25f; // thin atmosphere glow
                }
                // A small moon further round.
                Vector3 moon = Direction(s.sunElevation + 14f, s.sunAzimuth - 40f);
                c = Disc(c, s.accentA, Vector3.Dot(dir, moon), s.sunSize * 0.18f);
                return Clouds(c, s, dir, sun, new Vector2(0.5f, 1.5f), 0.3f);
            }
        }
        return Gradient(s, dir.y, 0.5f);
    }

    // ---- building blocks ----

    private static Color Gradient(SkyStyle s, float up, float power)
    {
        if (up >= 0f) return Color.Lerp(s.horizonColor, s.topColor, Mathf.Pow(up, power));
        return Color.Lerp(s.horizonColor, s.groundColor, Mathf.Clamp01(-up * 8f));
    }

    /// <summary>A soft-edged round disc of 'size' degrees across in the direction whose dot product is 'd'.</summary>
    private static Color Disc(Color c, Color disc, float d, float size)
    {
        float ang = Mathf.Acos(Mathf.Clamp(d, -1f, 1f)) * Mathf.Rad2Deg;
        float r = size * 0.5f;
        float k = Mathf.Clamp01((r - ang) / Mathf.Max(0.05f, r * 0.15f));
        return Color.Lerp(c, disc, k);
    }

    private static Color Moon(Color c, SkyStyle s, Vector3 dir, float d)
    {
        float ang = Mathf.Acos(Mathf.Clamp(d, -1f, 1f)) * Mathf.Rad2Deg;
        float r = s.sunSize * 0.5f;
        c += s.sunColor * Mathf.Exp(-ang * 0.35f) * 0.12f; // halo
        if (ang < r)
        {
            float craters = Fbm3(dir * 120f, 3, s.seed + 31);
            Color m = s.sunColor * (0.8f + 0.35f * (craters - 0.5f));
            c = Color.Lerp(c, m, Mathf.Clamp01((r - ang) / (r * 0.12f)));
        }
        return c;
    }

    /// <summary>Single-texel stars; denser where texels are bigger (sinLat) so they look even across the sky.</summary>
    private static Color Stars(Color c, SkyStyle s, int x, int y, float sinLat, float res, float boost)
    {
        if (s.starAmount <= 0f) return c;
        float chance = s.starAmount * 0.012f * sinLat / Mathf.Max(0.25f, res * res) * boost;
        float h = PixelNoise.Hash(x, y, s.seed + 1000);
        if (h < chance)
        {
            float b = PixelNoise.Hash(x, y, s.seed + 2000);
            Color star = Color.Lerp(Color.white, b > 0.5f ? s.accentA : s.accentB, 0.25f);
            c = Color.Lerp(c, star, 0.35f + 0.65f * b * b);
        }
        return c;
    }

    /// <summary>Clouds on a flat layer overhead (so they shrink towards the horizon), lit from the light direction.</summary>
    private static Color Clouds(Color c, SkyStyle s, Vector3 dir, Vector3 light, Vector2 stretch, float scale, Color? litColor = null)
    {
        if (s.cloudAmount <= 0f || dir.y <= 0.01f) return c;
        float t = 1f / dir.y;
        float px = dir.x * t * scale * stretch.x, pz = dir.z * t * scale * stretch.y;
        float n = Fbm2(px, pz, 5, s.seed);
        float th = Mathf.Lerp(0.75f, 0.32f, s.cloudAmount);
        float density = Mathf.Clamp01((n - th) / 0.2f);
        if (density <= 0f) return c;

        // Fake lighting: sample a little towards the light; thinner there = brighter edge.
        Vector2 toward = new Vector2(light.x, light.z);
        toward = toward.sqrMagnitude > 0.0001f ? toward.normalized * 0.08f : Vector2.zero;
        float n2 = Fbm2(px + toward.x, pz + toward.y, 5, s.seed);
        float lit = Mathf.Clamp01(0.55f + (n - n2) * 4f);
        Color cloud = Color.Lerp(s.cloudShadowColor, litColor ?? s.cloudColor, lit);
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dir.y - 0.01f) / 0.2f));
        return Color.Lerp(c, cloud, density * fade);
    }

    // ---- non-repeating noise (the sky is drawn from directions, so it needs no wrapping) ----

    private static float Noise2(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Mathf.Lerp(PixelNoise.Hash(x0, y0, seed), PixelNoise.Hash(x0 + 1, y0, seed), fx);
        float b = Mathf.Lerp(PixelNoise.Hash(x0, y0 + 1, seed), PixelNoise.Hash(x0 + 1, y0 + 1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    private static float Fbm2(float x, float y, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise2(x, y, seed + i * 101) * amp;
            norm += amp;
            amp *= 0.5f;
            x = x * 2.03f + 17.1f;
            y = y * 2.03f + 9.7f;
        }
        return sum / norm;
    }

    private static float Hash3(int x, int y, int z, int seed) => PixelNoise.Hash(x + z * 7919, y - z * 104729, seed);

    private static float Noise3(Vector3 p, int seed)
    {
        int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
        float fx = p.x - x0, fy = p.y - y0, fz = p.z - z0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        fz = fz * fz * (3f - 2f * fz);
        float a = Mathf.Lerp(Hash3(x0, y0, z0, seed), Hash3(x0 + 1, y0, z0, seed), fx);
        float b = Mathf.Lerp(Hash3(x0, y0 + 1, z0, seed), Hash3(x0 + 1, y0 + 1, z0, seed), fx);
        float c = Mathf.Lerp(Hash3(x0, y0, z0 + 1, seed), Hash3(x0 + 1, y0, z0 + 1, seed), fx);
        float d = Mathf.Lerp(Hash3(x0, y0 + 1, z0 + 1, seed), Hash3(x0 + 1, y0 + 1, z0 + 1, seed), fx);
        return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
    }

    private static float Fbm3(Vector3 p, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise3(p, seed + i * 101) * amp;
            norm += amp;
            amp *= 0.5f;
            p = p * 2.03f + new Vector3(5.3f, 1.7f, 9.1f);
        }
        return sum / norm;
    }

    // ------------------------------------------------------------------
    // Built-in skies
    // ------------------------------------------------------------------

    private void EnsureDefaultStyles()
    {
        foreach (SkyStyle def in DefaultStyles())
            if (!styles.Exists(s => s.name == def.name)) styles.Add(def);
    }

    private static IEnumerable<SkyStyle> DefaultStyles()
    {
        yield return new SkyStyle { name = "Classic", pattern = SkyPattern.Classic, rotateSpeed = 0f };

        yield return new SkyStyle
        {
            name = "Clear Day", pattern = SkyPattern.ClearDay, seed = 11,
            topColor = new Color(0.16f, 0.4f, 0.85f), horizonColor = new Color(0.68f, 0.84f, 0.98f),
            groundColor = new Color(0.32f, 0.36f, 0.33f), sunColor = new Color(1f, 0.96f, 0.82f),
            cloudColor = Color.white, cloudShadowColor = new Color(0.68f, 0.74f, 0.86f),
            sunElevation = 32f, sunAzimuth = 25f, sunSize = 4f, cloudAmount = 0.45f, rotateSpeed = 0.4f
        };

        yield return new SkyStyle
        {
            name = "Sunset", pattern = SkyPattern.Sunset, seed = 23,
            topColor = new Color(0.12f, 0.12f, 0.35f), horizonColor = new Color(0.98f, 0.55f, 0.35f),
            groundColor = new Color(0.12f, 0.08f, 0.12f), sunColor = new Color(1f, 0.8f, 0.45f),
            cloudColor = new Color(1f, 0.62f, 0.55f), cloudShadowColor = new Color(0.35f, 0.2f, 0.38f),
            accentA = new Color(1f, 0.42f, 0.25f), accentB = new Color(0.8f, 0.3f, 0.6f),
            sunElevation = 4f, sunAzimuth = 0f, sunSize = 6f, cloudAmount = 0.5f, rotateSpeed = 0.25f
        };

        yield return new SkyStyle
        {
            name = "Starry Night", pattern = SkyPattern.StarryNight, seed = 37,
            topColor = new Color(0.01f, 0.015f, 0.05f), horizonColor = new Color(0.06f, 0.1f, 0.22f),
            groundColor = new Color(0.02f, 0.02f, 0.03f), sunColor = new Color(0.95f, 0.95f, 0.88f),
            accentA = new Color(0.5f, 0.55f, 0.75f), accentB = new Color(0.85f, 0.6f, 0.9f),
            sunElevation = 30f, sunAzimuth = -30f, sunSize = 3.5f, starAmount = 0.8f, cloudAmount = 0f, rotateSpeed = 0.3f
        };

        yield return new SkyStyle
        {
            name = "Nebula", pattern = SkyPattern.Nebula, seed = 41,
            topColor = new Color(0.005f, 0.005f, 0.02f), horizonColor = new Color(1f, 0.85f, 0.95f),
            accentA = new Color(0.55f, 0.1f, 0.65f), accentB = new Color(0.05f, 0.45f, 0.75f),
            starAmount = 1f, cloudAmount = 0f, rotateSpeed = 0.6f
        };

        yield return new SkyStyle
        {
            name = "Aurora", pattern = SkyPattern.Aurora, seed = 53,
            topColor = new Color(0.01f, 0.02f, 0.06f), horizonColor = new Color(0.04f, 0.1f, 0.16f),
            groundColor = new Color(0.02f, 0.03f, 0.04f),
            accentA = new Color(0.2f, 1f, 0.55f), accentB = new Color(0.6f, 0.25f, 0.95f),
            starAmount = 0.6f, cloudAmount = 0f, rotateSpeed = 0.35f
        };

        yield return new SkyStyle
        {
            name = "Synthwave", pattern = SkyPattern.Synthwave, seed = 67,
            topColor = new Color(0.05f, 0.01f, 0.12f), horizonColor = new Color(0.55f, 0.1f, 0.45f),
            groundColor = new Color(0.03f, 0.0f, 0.06f), sunColor = new Color(1f, 0.9f, 0.3f),
            accentA = new Color(1f, 0.2f, 0.55f), accentB = new Color(0.1f, 0.9f, 1f),
            sunElevation = 9f, sunAzimuth = 0f, sunSize = 26f, starAmount = 0.4f, cloudAmount = 0f, rotateSpeed = 0f
        };

        yield return new SkyStyle
        {
            name = "Pixel Clouds", pattern = SkyPattern.PixelClouds, seed = 71, width = 256, pixelated = true,
            topColor = new Color(0.25f, 0.5f, 0.95f), horizonColor = new Color(0.62f, 0.85f, 1f),
            groundColor = new Color(0.3f, 0.55f, 0.3f), sunColor = new Color(1f, 0.92f, 0.4f),
            cloudColor = Color.white, cloudShadowColor = new Color(0.78f, 0.84f, 0.95f),
            sunElevation = 35f, sunAzimuth = 20f, sunSize = 5f, cloudAmount = 0.5f, rotateSpeed = 0.5f
        };

        yield return new SkyStyle
        {
            name = "Storm", pattern = SkyPattern.Storm, seed = 83,
            topColor = new Color(0.12f, 0.13f, 0.16f), horizonColor = new Color(0.3f, 0.32f, 0.36f),
            groundColor = new Color(0.08f, 0.08f, 0.09f),
            cloudColor = new Color(0.4f, 0.42f, 0.47f), cloudShadowColor = new Color(0.1f, 0.1f, 0.13f),
            cloudAmount = 0.9f, rotateSpeed = 1.2f, flashesPerMinute = 6f, exposure = 0.9f
        };

        yield return new SkyStyle
        {
            name = "Alien World", pattern = SkyPattern.AlienWorld, seed = 97,
            topColor = new Color(0.05f, 0.22f, 0.25f), horizonColor = new Color(0.55f, 0.9f, 0.6f),
            groundColor = new Color(0.1f, 0.15f, 0.1f), sunColor = new Color(0.95f, 0.65f, 0.4f),
            cloudColor = new Color(0.85f, 0.6f, 0.9f), cloudShadowColor = new Color(0.35f, 0.25f, 0.45f),
            accentA = new Color(0.9f, 0.9f, 1f), accentB = new Color(0.55f, 0.3f, 0.2f),
            sunElevation = 28f, sunAzimuth = 15f, sunSize = 30f, starAmount = 0.3f, cloudAmount = 0.3f, rotateSpeed = 0.3f
        };
    }
}
