// ---- System notes ----
// PixelContrast.cs
//   Two accessibility aids that make the pixels stand out from busy backgrounds (Settings > Accessibility; both OFF by default; saved per profile through PixelSettingsSync:
//   PlayerPrefs PixelClicker.Setting.ContrastRim / Spotlight / SpotlightStrength; component added by PixelClicker.Awake, PixelContrast.Instance):
//   1) AUTO-CONTRAST OUTLINE ('Auto-contrast outline'): a thin bevelled rim (PixelLooks.ContrastRimMesh, child 'Contrast Rim' of the live cube and of every old pixel, added when a copy is
//      spawned - PixelContrast.TryAttachRim from PixelClicker.SpawnFallingCopy - or when the setting is switched on) drawn with ONE shared Sprites/Default material whose colour follows the
//      background: every sampleSeconds (0.25) at the end of the frame the screen is copied (ScreenCapture.CaptureScreenshotIntoRenderTexture), the area round the cube (sampleScale x the
//      cube's size) is shrunk into a 16x16 texture and read back asynchronously (AsyncGPUReadback), the brightness of the ring around the cube (the cube itself is masked out) is smoothed
//      and the rim fades between light (dark background) and dark (bright background). Needs async GPU readback support (otherwise the rim stays light).
//   2) CUBE SPOTLIGHT ('Cube spotlight' + 'Spotlight strength'): a soft dark pool (generated radial texture on a quad lying just above the floor, radius spotlightWidths cube widths) under the
//      cube that fades the ground behind it; old pixels lying there stay bright because they are above the decal. The floor's height comes from PixelFloor.FloorRenderer bounds (else a
//      raycast down from the cube).
// ---------------------------------------------------
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class PixelContrast : MonoBehaviour
{
    public static PixelContrast Instance { get; private set; }

    [Header("Auto-contrast outline")]
    [Min(0.05f)]
    [Tooltip("Seconds between background brightness checks.")]
    [SerializeField] private float sampleSeconds = 0.25f;
    [Min(1f)]
    [Tooltip("How far round the cube the background is measured, in cube widths (the cube itself is left out).")]
    [SerializeField] private float sampleScale = 3.2f;
    [Tooltip("Colour of the rim over a dark background.")]
    [SerializeField] private Color lightRim = new Color(0.95f, 0.97f, 1f, 1f);
    [Tooltip("Colour of the rim over a bright background.")]
    [SerializeField] private Color darkRim = new Color(0.04f, 0.04f, 0.07f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Background brightness (0..1) where the rim starts to switch from light to dark.")]
    [SerializeField] private float switchLow = 0.38f;
    [Range(0f, 1f)]
    [Tooltip("Background brightness where the rim is fully dark.")]
    [SerializeField] private float switchHigh = 0.62f;

    [Header("Cube spotlight")]
    [Min(0.5f)]
    [Tooltip("Radius of the dark pool under the cube, in cube widths.")]
    [SerializeField] private float spotlightWidths = 2.8f;
    [Tooltip("Colour the ground fades to inside the spotlight.")]
    [SerializeField] private Color spotlightColor = new Color(0f, 0f, 0f, 1f);

    private const string PrefRim = "PixelClicker.Setting.ContrastRim", PrefSpot = "PixelClicker.Setting.Spotlight", PrefStrength = "PixelClicker.Setting.SpotlightStrength";
    private static int rimOn = -1, spotOn = -1;
    private static float strength = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; rimOn = spotOn = -1; strength = -1f; }

    public static bool RimEnabled
    {
        get { if (rimOn < 0) rimOn = PlayerPrefs.GetInt(PrefRim, 0); return rimOn != 0; }
        set { rimOn = value ? 1 : 0; PlayerPrefs.SetInt(PrefRim, rimOn); PlayerPrefs.Save(); if (Instance != null) Instance.ApplyRimSetting(); }
    }

    public static bool SpotlightEnabled
    {
        get { if (spotOn < 0) spotOn = PlayerPrefs.GetInt(PrefSpot, 0); return spotOn != 0; }
        set { spotOn = value ? 1 : 0; PlayerPrefs.SetInt(PrefSpot, spotOn); PlayerPrefs.Save(); }
    }

    public static float SpotlightStrength
    {
        get { if (strength < 0f) strength = PlayerPrefs.GetFloat(PrefStrength, 0.6f); return strength; }
        set { strength = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefStrength, strength); PlayerPrefs.Save(); }
    }

    public static void ReloadFromPrefs() { rimOn = spotOn = -1; strength = -1f; if (Instance != null) Instance.ApplyRimSetting(); }

    private PixelClicker clicker;
    private Material rimMaterial, spotMaterial;
    private Mesh rimMesh;
    private float background = 0.2f;       // smoothed brightness of the background round the cube (0..1)
    private RenderTexture fullCapture, small;
    private Transform spot;
    private float floorY, nextFloorCheck;
    private bool floorKnown;
    private static Texture2D spotTexture;

    private void Awake() { Instance = this; clicker = GetComponent<PixelClicker>(); }

    private void Start()
    {
        StartCoroutine(SampleLoop());
        ApplyRimSetting();
    }

    private void OnDestroy()
    {
        if (spot != null) Destroy(spot.gameObject);
        if (fullCapture != null) fullCapture.Release();
        if (small != null) small.Release();
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // The rim
    // ------------------------------------------------------------------

    private Material RimMaterial()
    {
        if (rimMaterial == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            if (sh == null) return null;
            rimMaterial = new Material(sh) { name = "Contrast Rim", hideFlags = HideFlags.HideAndDontSave, color = lightRim };
        }
        return rimMaterial;
    }

    /// <summary>Called for every fresh old pixel (and the live cube): gives it the rim when the setting is on.</summary>
    public static void TryAttachRim(Transform target)
    {
        if (Instance == null || !RimEnabled || target == null) return;
        Instance.AddRim(target);
    }

    private void AddRim(Transform target)
    {
        if (target.Find("Contrast Rim") != null) return;
        Material m = RimMaterial();
        if (m == null) return;
        if (rimMesh == null) rimMesh = PixelLooks.ContrastRimMesh();
        GameObject g = new GameObject("Contrast Rim", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(target, false);
        g.layer = target.gameObject.layer;
        g.GetComponent<MeshFilter>().sharedMesh = rimMesh;
        MeshRenderer r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void ApplyRimSetting()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        bool on = RimEnabled;
        System.Collections.Generic.List<Transform> targets = new System.Collections.Generic.List<Transform>();
        if (clicker.PixelTransform != null) targets.Add(clicker.PixelTransform);
        foreach (Rigidbody b in clicker.OldPixels) if (b != null) targets.Add(b.transform);
        foreach (Transform t in targets)
        {
            if (on) AddRim(t);
            else { Transform rim = t.Find("Contrast Rim"); if (rim != null) Destroy(rim.gameObject); }
        }
    }

    private System.Collections.IEnumerator SampleLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(sampleSeconds);
            if (!RimEnabled || clicker == null || PixelTitleScreen.Showing || PixelPauseMenu.IsPaused || !SystemInfo.supportsAsyncGPUReadback) continue;
            yield return new WaitForEndOfFrame();
            Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            if (cam == null || clicker.PixelTransform == null || clicker.CubeHidden) continue;
            int w = Screen.width, h = Screen.height;
            if (w < 16 || h < 16) continue;
            if (fullCapture == null || fullCapture.width != w || fullCapture.height != h)
            {
                if (fullCapture != null) fullCapture.Release();
                fullCapture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear };
                fullCapture.Create();
            }
            if (small == null) { small = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear }; small.Create(); }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(fullCapture);

            Vector3 centreWorld = clicker.LiveWorldPosition;
            float size = clicker.PixelBaseSize;
            Vector3 sc = cam.WorldToScreenPoint(centreWorld);
            if (sc.z <= 0f) continue;
            Vector3 top = cam.WorldToScreenPoint(centreWorld + cam.transform.up * size * 0.5f);
            float half = Mathf.Max(24f, Mathf.Abs(top.y - sc.y) * 2f * sampleScale * 0.5f);   // half the sampled square, in pixels
            float x0 = Mathf.Clamp(sc.x - half, 0f, w - 2f), y0 = Mathf.Clamp(sc.y - half, 0f, h - 2f);
            float x1 = Mathf.Clamp(sc.x + half, x0 + 2f, w), y1 = Mathf.Clamp(sc.y + half, y0 + 2f, h);
            Vector2 scale = new Vector2((x1 - x0) / w, (y1 - y0) / h);
            Vector2 offset = new Vector2(x0 / w, y0 / h);
            if (SystemInfo.graphicsUVStartsAtTop) { offset.y = 1f - offset.y - scale.y; scale.y = scale.y; }   // (the screen copy is stored top first on these APIs)
            Graphics.Blit(fullCapture, small, scale, offset);
            float centreX = ((sc.x - x0) / (x1 - x0)), centreY = ((sc.y - y0) / (y1 - y0));
            float maskRadius = Mathf.Clamp01((Mathf.Abs(top.y - sc.y) * 1.5f) / (y1 - y0));   // the cube (and its rim) is left out
            AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, request => OnReadback(request, centreX, centreY, maskRadius, SystemInfo.graphicsUVStartsAtTop));
        }
    }

    private void OnReadback(AsyncGPUReadbackRequest request, float cx, float cy, float maskRadius, bool topFirst)
    {
        if (request.hasError || this == null) return;
        NativeArray<Color32> data = request.GetData<Color32>();
        if (data.Length < 256) return;
        float sum = 0f; int count = 0;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float u = (x + 0.5f) / 16f, v = (y + 0.5f) / 16f;
                float vv = topFirst ? 1f - v : v;   // (rows come back top first on those APIs)
                if ((u - cx) * (u - cx) + (vv - cy) * (vv - cy) < maskRadius * maskRadius) continue;
                Color32 c = data[y * 16 + x];
                sum += (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                count++;
            }
        if (count == 0) return;
        background = Mathf.Lerp(background, sum / count, 0.45f);
    }

    // ------------------------------------------------------------------
    // Per frame: rim colour, spotlight
    // ------------------------------------------------------------------

    private void LateUpdate()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;

        if (rimMaterial != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(switchLow, Mathf.Max(switchLow + 0.01f, switchHigh), background));
            rimMaterial.color = Color.Lerp(lightRim, darkRim, t);
        }
        UpdateSpotlight();
    }

    private static Texture2D SpotTexture()
    {
        if (spotTexture != null) return spotTexture;
        const int n = 128;
        spotTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.SmoothStep(0.25f, 1f, r);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
        spotTexture.SetPixels32(px);
        spotTexture.Apply();
        return spotTexture;
    }

    private void UpdateSpotlight()
    {
        bool on = SpotlightEnabled && SpotlightStrength > 0.01f && !PixelTitleScreen.Showing && clicker.PixelTransform != null && !clicker.CubeHidden;
        if (!on) { if (spot != null && spot.gameObject.activeSelf) spot.gameObject.SetActive(false); return; }

        if (Time.unscaledTime >= nextFloorCheck)
        {
            nextFloorCheck = Time.unscaledTime + 1f;
            floorKnown = false;
            PixelFloor floor = PixelFloor.Instance;
            if (floor != null && floor.FloorRenderer != null) { floorY = floor.FloorRenderer.bounds.center.y; floorKnown = true; }
            else if (Physics.Raycast(clicker.LiveWorldPosition, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) { floorY = hit.point.y; floorKnown = true; }
        }
        if (!floorKnown) { if (spot != null) spot.gameObject.SetActive(false); return; }

        if (spot == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            if (sh == null) return;
            spotMaterial = new Material(sh) { name = "Cube Spotlight", hideFlags = HideFlags.HideAndDontSave, mainTexture = SpotTexture() };
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Cube Spotlight";
            Collider c = q.GetComponent<Collider>();
            if (c != null) Destroy(c);
            MeshRenderer r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = spotMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lying flat, facing up
            spot = q.transform;
        }
        spot.gameObject.SetActive(true);
        Vector3 p = clicker.LiveWorldPosition;
        float radius = clicker.PixelBaseSize * spotlightWidths;
        spot.position = new Vector3(p.x, floorY + 0.012f, p.z);
        spot.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        Color col = spotlightColor; col.a = SpotlightStrength;
        spotMaterial.color = col;
    }
}
