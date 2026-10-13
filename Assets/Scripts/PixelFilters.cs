// ---- System notes ----
// PixelFilters.cs
//   Screen filters (Settings > Display > 'Screen filter' drop-down + 'Filter strength' slider; saved per profile through PixelSettingsSync: PlayerPrefs
//   PixelClicker.Setting.Filter / FilterStrength; component added by PixelClicker.Awake, PixelFilters.Instance). A filter is a Preset (Inspector list `presets`, index 0 = Off;
//   defaults CreateDefaults: CRT TV, VHS, Wild West, Noir, Night Vision, Neon Dream) with TWO parts, both scaled by the strength slider:
//   1) URP post-processing (only with the Universal RP package, #if PIXEL_URP and a URP pipeline asset): a runtime global Volume with ColorAdjustments, WhiteBalance, Vignette,
//      FilmGrain, ChromaticAberration, LensDistortion and Bloom, blended from neutral to the preset by the strength; the cameras get renderPostProcessing switched on (re-checked every
//      cameraCheckSeconds). Without URP this part is skipped and only the overlay works.
//   2) A screen-space overlay canvas (sortingOrder overlaySortingOrder, above the UI so it also covers windows like a real screen): colour tint, vignette, CRT rounded corners
//      (mask), scanlines (tiled 4 px texture, rolling), animated static noise, a VHS tracking band, film dust / scratches. All textures are generated in code once.
//   Off = the volume is disabled and the overlay canvas hidden, so it costs nothing.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if PIXEL_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class PixelFilters : MonoBehaviour
{
    [Serializable]
    public class Preset
    {
        public string name = "Off";
        [Header("Colour grading (URP)")]
        public float saturation;          // -100..100
        public float contrast;            // -100..100
        public float exposure;            // EV
        public Color colorFilter = Color.white;
        public float temperature;         // -100..100 (warm +)
        [Header("Lens (URP)")]
        [Range(0f, 1f)] public float vignette;
        [Range(0f, 1f)] public float grain;
        [Range(0f, 1f)] public float chromatic;
        [Range(-1f, 1f)] public float lens;
        [Range(0f, 3f)] public float bloom;
        [Header("Overlay (alpha 0..1)")]
        [Range(0f, 1f)] public float scanlines;
        [Range(0f, 1f)] public float crtCorners;
        [Range(0f, 1f)] public float noise;
        [Range(0f, 1f)] public float band;
        [Range(0f, 1f)] public float dust;
        [Range(0f, 1f)] public float overlayVignette;
        public Color tint = new Color(0f, 0f, 0f, 0f);   // alpha = strength of the colour wash
    }

    public static PixelFilters Instance { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; choice = -1; strength = -1f; }

    [Header("Filters")]
    [Tooltip("The filters (index 0 must be 'Off'). Edit or add your own.")]
    [SerializeField] private List<Preset> presets = new List<Preset>();
    [Tooltip("Sorting order of the overlay canvas (high = also covers windows and menus).")]
    [SerializeField] private int overlaySortingOrder = 9000;
    [Min(0.1f)]
    [Tooltip("Seconds between checks that every camera has post-processing switched on.")]
    [SerializeField] private float cameraCheckSeconds = 1.5f;
    [Tooltip("Switch the whole filter system off.")]
    [SerializeField] private bool disableFilters = false;

    private const string PrefChoice = "PixelClicker.Setting.Filter", PrefStrength = "PixelClicker.Setting.FilterStrength";
    private static int choice = -1;
    private static float strength = -1f;

    /// <summary>Index of the chosen filter (0 = Off).</summary>
    public static int Choice
    {
        get { if (choice < 0) choice = PlayerPrefs.GetInt(PrefChoice, 0); return choice; }
        set { choice = Mathf.Max(0, value); PlayerPrefs.SetInt(PrefChoice, choice); PlayerPrefs.Save(); if (Instance != null) Instance.Refresh(); }
    }

    /// <summary>How strong the filter is (0..1).</summary>
    public static float Strength
    {
        get { if (strength < 0f) strength = PlayerPrefs.GetFloat(PrefStrength, 1f); return strength; }
        set { strength = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefStrength, strength); PlayerPrefs.Save(); if (Instance != null) Instance.Refresh(); }
    }

    public static void ReloadFromPrefs() { choice = -1; strength = -1f; if (Instance != null) Instance.Refresh(); }

    private static readonly string[] fallbackNames = { "Off", "CRT TV", "VHS", "Wild West", "Noir", "Night Vision", "Neon Dream" };
    public static string[] Names
    {
        get
        {
            if (Instance == null || Instance.presets.Count == 0) return fallbackNames;
            string[] n = new string[Instance.presets.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Instance.presets[i].name;
            return n;
        }
    }

    // ---- overlay ----
    private GameObject canvasRoot;
    private Image tintImage;
    private RawImage vignetteImage, maskImage, scanImage, noiseImage, dustImage, bandImage;
    private RectTransform bandRect;
    private float nextNoise, nextDust, nextCameraCheck;
    private Preset active;

#if PIXEL_URP
    private Volume volume;
    private VolumeProfile profile;
    private ColorAdjustments colorAdj;
    private WhiteBalance whiteBalance;
    private Vignette vignetteFx;
    private FilmGrain grainFx;
    private ChromaticAberration chromaFx;
    private LensDistortion lensFx;
    private Bloom bloomFx;
    private bool urpReady;
#endif

    private void Awake()
    {
        Instance = this;
        if (presets == null || presets.Count == 0) presets = CreateDefaults();
    }

    private void Start() { Refresh(); }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private Preset Chosen()
    {
        if (disableFilters || presets == null || presets.Count == 0) return null;
        int i = Mathf.Clamp(Choice, 0, presets.Count - 1);
        return i == 0 ? null : presets[i];
    }

    /// <summary>Applies the current choice and strength.</summary>
    public void Refresh()
    {
        active = Chosen();
        float k = Strength;
        bool on = active != null && k > 0.001f;
        ApplyOverlay(on ? active : null, k);
        ApplyVolume(on ? active : null, k);
        enabled = on;
    }

    private void Update()
    {
        if (active == null) return;
        float dt = Time.unscaledDeltaTime, time = Time.unscaledTime;
        float k = Strength;

        if (canvasRoot != null)
        {
            // Pixel-exact tiling whatever the resolution.
            float sw = Screen.width, sh = Screen.height;
            if (scanImage != null && active.scanlines > 0f) scanImage.uvRect = new Rect(0f, -(time * 0.05f) % 1f, 1f, sh / 4f);
            if (time >= nextNoise && noiseImage != null && active.noise > 0f)
            {
                nextNoise = time + 0.06f;
                noiseImage.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, sw / 256f, sh / 256f);
            }
            if (time >= nextDust && dustImage != null && active.dust > 0f)
            {
                nextDust = time + UnityEngine.Random.Range(0.08f, 0.2f);
                float flipX = UnityEngine.Random.value < 0.5f ? -1f : 1f, flipY = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                dustImage.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, flipX, flipY);
                Color c = dustImage.color; c.a = active.dust * k * UnityEngine.Random.Range(0.5f, 1f); dustImage.color = c;
            }
            if (bandImage != null && bandRect != null && active.band > 0f)
            {
                float f = (time / 6.5f) % 1f;
                float h = canvasRoot.GetComponent<RectTransform>().rect.height;
                bandRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(h * 0.5f + 80f, -h * 0.5f - 80f, f));
                Color c = bandImage.color; c.a = active.band * k * (0.7f + 0.3f * Mathf.Sin(time * 31f)); bandImage.color = c;
            }
        }

#if PIXEL_URP
        if (urpReady && time >= nextCameraCheck)
        {
            nextCameraCheck = time + cameraCheckSeconds;
            EnableCameras();
        }
#endif
    }

    // ------------------------------------------------------------------
    // URP post-processing
    // ------------------------------------------------------------------

#if PIXEL_URP
    private void EnableCameras()
    {
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null || cam.cameraType != CameraType.Game) continue;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
        }
    }

    private bool EnsureVolume()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return false;
        if (volume != null) return true;
        GameObject go = new GameObject("Filter Volume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 500f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        colorAdj = profile.Add<ColorAdjustments>(true);
        whiteBalance = profile.Add<WhiteBalance>(true);
        vignetteFx = profile.Add<Vignette>(true);
        grainFx = profile.Add<FilmGrain>(true);
        chromaFx = profile.Add<ChromaticAberration>(true);
        lensFx = profile.Add<LensDistortion>(true);
        bloomFx = profile.Add<Bloom>(true);
        grainFx.type.value = FilmGrainLookup.Thin1;
        vignetteFx.color.value = Color.black;
        vignetteFx.smoothness.value = 0.55f;
        bloomFx.threshold.value = 0.85f;
        bloomFx.scatter.value = 0.6f;
        volume.sharedProfile = profile;
        return true;
    }

    private void ApplyVolume(Preset p, float k)
    {
        urpReady = false;
        if (p == null) { if (volume != null) volume.enabled = false; return; }
        if (!EnsureVolume()) { Debug.LogWarning("PixelFilters: the render pipeline asset in Project Settings > Graphics is not a Universal RP asset, so the bulge / grain / colour effects are skipped (only the overlay shows).", this); return; }
        volume.enabled = true;
        urpReady = true;
        colorAdj.saturation.value = p.saturation * k;
        colorAdj.contrast.value = p.contrast * k;
        colorAdj.postExposure.value = p.exposure * k;
        colorAdj.colorFilter.value = Color.Lerp(Color.white, p.colorFilter, k);
        whiteBalance.temperature.value = p.temperature * k;
        vignetteFx.intensity.value = p.vignette * k;
        grainFx.intensity.value = p.grain * k;
        chromaFx.intensity.value = p.chromatic * k;
        lensFx.intensity.value = p.lens * k;
        bloomFx.intensity.value = p.bloom * k;
        EnableCameras();
        Debug.Log("PixelFilters: URP post-processing volume active for '" + p.name + "' (strength " + k.ToString("0.00") + "). If the picture shows no bulge / grain, check the Universal Renderer asset has Post-processing enabled and the camera's Post Processing box is ticked.", this);
    }
#else
    private void ApplyVolume(Preset p, float k) { if (p != null) Debug.LogWarning("PixelFilters: PIXEL_URP is not defined, so only the overlay part of the filter works (no bulge, grain, colour grading).", this); }
#endif

    // ------------------------------------------------------------------
    // Overlay
    // ------------------------------------------------------------------

    private void ApplyOverlay(Preset p, float k)
    {
        if (p == null) { if (canvasRoot != null) canvasRoot.SetActive(false); return; }
        if (canvasRoot == null) BuildOverlay();
        canvasRoot.SetActive(true);

        tintImage.color = new Color(p.tint.r, p.tint.g, p.tint.b, p.tint.a * k);
        tintImage.gameObject.SetActive(p.tint.a > 0f);
        Show(vignetteImage, new Color(0f, 0f, 0f, p.overlayVignette * k), p.overlayVignette > 0f);
        Show(maskImage, new Color(0f, 0f, 0f, p.crtCorners * k), p.crtCorners > 0f);
        Show(scanImage, new Color(0f, 0f, 0f, p.scanlines * k), p.scanlines > 0f);
        Show(noiseImage, new Color(1f, 1f, 1f, p.noise * k), p.noise > 0f);
        Show(dustImage, new Color(1f, 1f, 1f, p.dust * k), p.dust > 0f);
        Show(bandImage, new Color(1f, 1f, 1f, p.band * k), p.band > 0f);
    }

    private static void Show(RawImage image, Color color, bool on)
    {
        image.gameObject.SetActive(on);
        image.color = color;
    }

    private RawImage Layer(string name, Texture2D tex, bool full = true)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        g.transform.SetParent(canvasRoot.transform, false);
        RawImage r = g.GetComponent<RawImage>();
        r.texture = tex;
        r.raycastTarget = false;
        if (full) PixelUIKit.Stretch(g.GetComponent<RectTransform>());
        return r;
    }

    private void BuildOverlay()
    {
        canvasRoot = PixelUIKit.CreateCanvas("Screen Filters", overlaySortingOrder, new Vector2(1920f, 1080f), false, false);

        GameObject t = new GameObject("Tint", typeof(RectTransform), typeof(Image));
        t.transform.SetParent(canvasRoot.transform, false);
        tintImage = t.GetComponent<Image>();
        tintImage.raycastTarget = false;
        PixelUIKit.Stretch(t.GetComponent<RectTransform>());

        vignetteImage = Layer("Vignette", Textures.Vignette());
        maskImage = Layer("CRT Corners", Textures.Corners());
        dustImage = Layer("Dust", Textures.Dust());
        noiseImage = Layer("Noise", Textures.Noise());
        scanImage = Layer("Scanlines", Textures.Scan());
        bandImage = Layer("VHS Band", Textures.Band(), false);
        bandRect = bandImage.GetComponent<RectTransform>();
        bandRect.anchorMin = new Vector2(0f, 0.5f);
        bandRect.anchorMax = new Vector2(1f, 0.5f);
        bandRect.pivot = new Vector2(0.5f, 0.5f);
        bandRect.sizeDelta = new Vector2(0f, 150f);
    }

    private static class Textures
    {
        private static Texture2D vignette, corners, dust, noise, scan, band;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { vignette = corners = dust = noise = scan = band = null; }

        private static Texture2D Tex(int w, int h, bool repeat, FilterMode mode, Func<int, int, Color32> f)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = mode };
            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        public static Texture2D Vignette() => vignette != null ? vignette : vignette = Tex(128, 128, false, FilterMode.Bilinear, (x, y) =>
        {
            float dx = (x / 127f - 0.5f) * 2f, dy = (y / 127f - 0.5f) * 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.2f;
            float a = Mathf.SmoothStep(0.45f, 1f, d);
            return new Color32(0, 0, 0, (byte)Mathf.RoundToInt(255f * a));
        });

        // Dark rounded-rectangle frame: transparent inside, black in the corners / outside, softly.
        public static Texture2D Corners() => corners != null ? corners : corners = Tex(256, 256, false, FilterMode.Bilinear, (x, y) =>
        {
            float dx = Mathf.Abs(x / 255f - 0.5f) * 2f, dy = Mathf.Abs(y / 255f - 0.5f) * 2f;
            const float half = 0.9f, r = 0.3f;   // a TV-tube shaped window: big round corners
            float qx = Mathf.Max(0f, dx - (half - r)), qy = Mathf.Max(0f, dy - (half - r));
            float dist = Mathf.Sqrt(qx * qx + qy * qy) - r;   // <0 inside the rounded rect
            float a = Mathf.Clamp01(dist / 0.08f + 0.5f);
            a = Mathf.Max(a, Mathf.Clamp01((dist + 0.1f) / 0.1f) * 0.35f);   // a soft dark rim inside the edge, like the glass curving away
            return new Color32(0, 0, 0, (byte)Mathf.RoundToInt(255f * a));
        });

        public static Texture2D Scan() => scan != null ? scan : scan = Tex(1, 4, true, FilterMode.Point, (x, y) => new Color32(0, 0, 0, (byte)(y == 0 ? 255 : y == 1 ? 90 : 0)));

        public static Texture2D Noise() => noise != null ? noise : noise = Tex(128, 128, true, FilterMode.Point, (x, y) =>
        {
            byte v = (byte)UnityEngine.Random.Range(0, 256);
            return new Color32(v, v, v, (byte)UnityEngine.Random.Range(0, 120));
        });

        public static Texture2D Band() => band != null ? band : band = Tex(4, 128, false, FilterMode.Bilinear, (x, y) =>
        {
            float v = y / 127f;
            float a = Mathf.Sin(v * Mathf.PI);
            a = a * a * (0.5f + 0.5f * UnityEngine.Random.value);
            return new Color32(235, 235, 255, (byte)Mathf.RoundToInt(255f * a * 0.9f));
        });

        // Film dust and scratches on a transparent sheet.
        public static Texture2D Dust()
        {
            if (dust != null) return dust;
            const int n = 512;
            Color32[] px = new Color32[n * n];
            System.Random rng = new System.Random(17);
            void Blob(int cx, int cy, int rad, byte a, byte shade)
            {
                for (int y = -rad; y <= rad; y++) for (int x = -rad; x <= rad; x++)
                {
                    int px2 = cx + x, py = cy + y;
                    if (px2 < 0 || py < 0 || px2 >= n || py >= n || x * x + y * y > rad * rad) continue;
                    px[py * n + px2] = new Color32(shade, shade, shade, a);
                }
            }
            for (int i = 0; i < 70; i++) Blob(rng.Next(n), rng.Next(n), rng.Next(1, 4), (byte)rng.Next(110, 230), (byte)(rng.NextDouble() < 0.5 ? 20 : 235));
            for (int s = 0; s < 4; s++)
            {
                int x0 = rng.Next(n), y0 = rng.Next(n), len = rng.Next(60, 260);
                for (int y = 0; y < len; y++) { int xx = x0 + (int)(Mathf.Sin(y * 0.05f + s) * 2f), yy = (y0 + y) % n; if (xx >= 0 && xx < n) px[yy * n + xx] = new Color32(235, 235, 235, (byte)rng.Next(60, 150)); }
            }
            dust = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            dust.SetPixels32(px);
            dust.Apply();
            return dust;
        }
    }

    // ------------------------------------------------------------------
    // Default presets
    // ------------------------------------------------------------------

    public static List<Preset> CreateDefaults() => new List<Preset>
    {
        new Preset { name = "Off" },
        new Preset { name = "CRT TV", saturation = 12f, contrast = 14f, vignette = 0.3f, grain = 0.12f, chromatic = 0.3f, lens = -0.3f, bloom = 0.6f,
                     scanlines = 0.28f, crtCorners = 0.95f, noise = 0.05f, overlayVignette = 0.25f },
        new Preset { name = "VHS", saturation = -22f, contrast = -6f, temperature = 8f, vignette = 0.3f, grain = 0.5f, chromatic = 0.7f, bloom = 0.3f,
                     scanlines = 0.1f, noise = 0.12f, band = 0.22f, overlayVignette = 0.15f },
        new Preset { name = "Wild West", saturation = -58f, contrast = 12f, colorFilter = new Color(1f, 0.86f, 0.62f), temperature = 30f, vignette = 0.55f, grain = 0.35f,
                     dust = 0.5f, overlayVignette = 0.35f, tint = new Color(0.45f, 0.28f, 0.1f, 0.14f) },
        new Preset { name = "Noir", saturation = -100f, contrast = 32f, vignette = 0.5f, grain = 0.4f, dust = 0.18f, overlayVignette = 0.3f },
        new Preset { name = "Night Vision", saturation = -50f, exposure = 0.6f, colorFilter = new Color(0.45f, 1f, 0.5f), vignette = 0.6f, grain = 0.6f, bloom = 0.9f,
                     scanlines = 0.14f, crtCorners = 0.45f, noise = 0.08f, overlayVignette = 0.4f, tint = new Color(0.1f, 0.6f, 0.15f, 0.1f) },
        new Preset { name = "Neon Dream", saturation = 55f, contrast = 12f, vignette = 0.2f, chromatic = 0.18f, bloom = 1.3f, overlayVignette = 0.15f },
    };
}
