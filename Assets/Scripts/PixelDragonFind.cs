using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "you found a Dragon Cube" show (added by <c>PixelClicker.Awake</c>). Played when a Dragon Cube turns up from a click or a grown
/// Dragon Seed (never from offline progress): the screen goes dark, the cube zooms up towards the camera from where it was, dances about
/// playfully in a rain of golden sparkles, then dives into the backpack icon (the one the Ghost's potion goes into) to show it is yours.
/// The cube is drawn by a hidden studio camera into a RenderTexture shown on a UI image, so nothing in the scene can hide or dim it.
/// All timing uses unscaled time. Several finds queue up.
/// </summary>
public class PixelDragonFind : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Turn the whole show off (the cube is still added, only the animation is skipped).")]
    [SerializeField] private bool disableShow = false;

    [Header("Timing (seconds)")]
    [Tooltip("The screen darkens and the cube flies up towards the camera.")]
    [SerializeField] private float zoomInSeconds = 0.8f;
    [Tooltip("The cube dances about.")]
    [SerializeField] private float danceSeconds = 2.4f;
    [Tooltip("The cube dives into the backpack.")]
    [SerializeField] private float stashSeconds = 0.75f;
    [Tooltip("The screen brightens again.")]
    [SerializeField] private float fadeOutSeconds = 0.45f;

    [Header("Look")]
    [Tooltip("How dark the screen gets (0 = not at all, 1 = black).")]
    [SerializeField] [Range(0f, 1f)] private float darkness = 0.85f;
    [Tooltip("Size of the cube at its biggest, in canvas units (the canvas is 1080 high).")]
    [SerializeField] private float cubeSize = 520f;
    [Tooltip("How far the cube wanders from the middle while dancing, as a fraction of the canvas size.")]
    [SerializeField] private Vector2 danceRange = new Vector2(0.17f, 0.12f);
    [Tooltip("Size of the backpack icon (canvas units); matches the Ghost's.")]
    [SerializeField] private float backpackSize = 130f;
    [Tooltip("Distance of the backpack icon from the right edge and the bottom bar (canvas units); matches the Ghost's.")]
    [SerializeField] private Vector2 backpackMargin = new Vector2(40f, 40f);
    [Tooltip("Sparkle colour.")]
    [SerializeField] private Color sparkleColor = new Color(1f, 0.78f, 0.25f, 1f);
    [Tooltip("Sparkles shed per second while the cube dances.")]
    [SerializeField] private float sparklesPerSecond = 70f;
    [Tooltip("Heading text. {0} = the cube's name.")]
    [SerializeField] private string foundFormat = "You found a {0}!";
    [Tooltip("Text under the heading while the cube goes into the backpack.")]
    [SerializeField] private string stashedText = "It's yours now";

    private struct Spark
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos, vel;
        public float life, maxLife, size, spin;
        public bool alive;
    }

    private struct Request { public int tier; public Vector2 screenPos; }

    private static PixelDragonFind instance;
    private readonly Queue<Request> queue = new Queue<Request>();
    private bool playing;
    private PixelClicker clicker;

    private GameObject canvasRoot;
    private RectTransform canvasRect;
    private Image dim;
    private Image backpack;
    private RectTransform backpackRect;
    private RawImage cubeImage;
    private RectTransform cubeRect;
    private TMP_Text title, subtitle;
    private Spark[] sparks;
    private Sprite sparkSprite;
    private float emitCarry;

    // The studio (the cube's 3D picture).
    private GameObject studioRoot;
    private Transform pivot;
    private Camera studioCam;
    private RenderTexture rt;

    /// <summary>True while a show is playing.</summary>
    public static bool Playing => instance != null && instance.playing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private bool skipRequested;

    private void Awake()
    {
        instance = this;
        // Escape skips the show (the cube is already yours); it must not open the pause menu while one plays.
        PixelWindows.Register(this, 165, () => playing, () => skipRequested = true);
    }
    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelWindows.Unregister(this);
        Cleanup();
    }

    /// <summary>Starts (or queues) the show for the Dragon Cube tier at 'worldPosition' (where the cube was found).</summary>
    public static void Play(int tierIndex, Vector3 worldPosition)
    {
        if (instance == null || instance.disableShow) return;
        instance.Enqueue(tierIndex, worldPosition);
    }

    private void Enqueue(int tierIndex, Vector3 worldPosition)
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null || !clicker.IsValidTierIndex(tierIndex)) return;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        Vector2 screen = cam != null ? (Vector2)cam.WorldToScreenPoint(worldPosition) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        queue.Enqueue(new Request { tier = tierIndex, screenPos = screen });
        if (!playing) StartCoroutine(Show());
    }

    // ------------------------------------------------------------------
    // The show
    // ------------------------------------------------------------------

    private System.Collections.IEnumerator Show()
    {
        playing = true;
        while (queue.Count > 0)
        {
            Request r = queue.Dequeue();
            yield return PlayOne(r);
        }
        playing = false;
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

    private System.Collections.IEnumerator PlayOne(Request req)
    {
        Build(req.tier);
        if (cubeImage == null) { Cleanup(); yield break; }
        yield return null;   // the canvas scaler sets the canvas size on the first frame

        Vector2 size = canvasRect.rect.size;
        Vector2 start = new Vector2(req.screenPos.x / Screen.width * size.x, req.screenPos.y / Screen.height * size.y);
        start.x = Mathf.Clamp(start.x, 0f, size.x); start.y = Mathf.Clamp(start.y, 0f, size.y);
        Vector2 centre = size * 0.5f + new Vector2(0f, size.y * 0.04f);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 pack = new Vector2(size.x - backpackMargin.x - backpackSize * 0.5f, bar + backpackMargin.y + backpackSize * 0.5f);
        float startSize = cubeSize * 0.16f;
        backpackRect.anchoredPosition = pack;

        string tierName = clicker.Tiers[req.tier].displayName;
        title.text = string.Format(foundFormat, tierName);
        subtitle.text = "";
        PixelAudio.Play("dragon_find");

        float t = 0f;
        Vector2 pos = start;
        float scale = startSize;
        float zoomT = Mathf.Max(0.05f, zoomInSeconds), danceT = Mathf.Max(0.05f, danceSeconds), stashT = Mathf.Max(0.05f, stashSeconds), fadeT = Mathf.Max(0.05f, fadeOutSeconds);
        float total = zoomT + danceT + stashT;
        bool burstedArrival = false, stashed = false;
        float bump = 0f;

        while (t < total + fadeT)
        {
            float dt = Time.unscaledDeltaTime;
            if (skipRequested)
            {
                skipRequested = false;
                queue.Clear();                      // skip every waiting show too
                if (t < total) t = total;           // jump straight to the cube landing in the backpack and the screen brightening
                burstedArrival = true;
            }
            t += dt;
            float darkAmount;
            float packAlpha, titleAlpha;
            Vector3 euler;

            if (t < zoomT)
            {
                float k = EaseOut(t / zoomT);
                pos = Vector2.Lerp(start, centre, k);
                scale = Mathf.Lerp(startSize, cubeSize, k * k * (3f - 2f * k) * 0.35f + k * 0.65f);
                darkAmount = Smooth(t / (zoomT * 0.8f));
                packAlpha = 0f; titleAlpha = 0f;
                euler = new Vector3(20f + 360f * k, 540f * k, 0f);
            }
            else if (t < zoomT + danceT)
            {
                float d = t - zoomT;
                if (!burstedArrival) { burstedArrival = true; Burst(centre, 55, 150f, 520f, 1f); PixelAudio.Play("pixel_land"); }
                // A playful wander: loops, a bounce and a little spin.
                float a = d * 2.1f, b = d * 3.1f + 1.3f;
                Vector2 wander = new Vector2(Mathf.Sin(a) * danceRange.x * size.x, Mathf.Sin(b) * danceRange.y * size.y + Mathf.Abs(Mathf.Sin(d * 5f)) * 22f);
                pos = centre + wander;
                scale = cubeSize * (1f + 0.08f * Mathf.Sin(d * 7f) + 0.05f * Mathf.Sin(d * 3.3f));
                darkAmount = 1f; packAlpha = Smooth(d / 0.5f); titleAlpha = Smooth(d / 0.4f);
                euler = new Vector3(20f + Mathf.Sin(d * 2.3f) * 35f, 540f + d * 150f + Mathf.Sin(d * 4f) * 40f, Mathf.Sin(d * 3f) * 25f);
                Emit(pos, scale, dt);
            }
            else if (t < total)
            {
                float d = (t - zoomT - danceT) / stashT;
                float k = d * d * (3f - 2f * d);
                Vector2 from = centre + new Vector2(Mathf.Sin(danceT * 2.1f) * danceRange.x * size.x, Mathf.Sin(danceT * 3.1f + 1.3f) * danceRange.y * size.y);
                pos = Vector2.Lerp(from, pack, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * size.y * 0.14f);   // a little hop on the way
                scale = Mathf.Lerp(cubeSize, backpackSize * 0.35f, k);
                darkAmount = 1f; packAlpha = 1f; titleAlpha = 1f;
                euler = new Vector3(20f + 360f * k, 700f + 720f * k, 0f);
                subtitle.text = d > 0.3f ? stashedText : "";
                Emit(pos, scale * 0.8f, dt);
            }
            else
            {
                if (!stashed) { stashed = true; Burst(pack, 40, 90f, 380f, 0.8f); PixelAudio.Play("dragon_stash"); bump = 1f; }
                float f = (t - total) / fadeT;
                darkAmount = 1f - Smooth(f); packAlpha = 1f - Smooth(f * 1.2f); titleAlpha = 1f - Smooth(f * 1.6f);
                scale = 0f;
                euler = Vector3.zero;
                pos = pack;
            }

            bump = Mathf.Max(0f, bump - dt * 3.5f);
            ApplyFrame(pos, scale, euler, darkAmount, packAlpha, titleAlpha, bump);
            UpdateSparks(dt);
            yield return null;
        }

        // Let the last sparkles die out.
        float tail = 0.6f;
        while (tail > 0f) { tail -= Time.unscaledDeltaTime; UpdateSparks(Time.unscaledDeltaTime); yield return null; }
        Cleanup();
    }

    private void ApplyFrame(Vector2 pos, float scale, Vector3 euler, float darkAmount, float packAlpha, float titleAlpha, float bump)
    {
        Color c = dim.color; c.a = darkness * darkAmount; dim.color = c;
        c = backpack.color; c.a = packAlpha; backpack.color = c;
        backpackRect.localScale = Vector3.one * (1f + 0.35f * bump);
        title.alpha = titleAlpha; subtitle.alpha = titleAlpha;

        bool show = scale > 1f;
        if (cubeImage.enabled != show) cubeImage.enabled = show;
        if (show)
        {
            cubeRect.anchoredPosition = pos;
            cubeRect.sizeDelta = new Vector2(scale, scale);
            pivot.localRotation = Quaternion.Euler(euler);
            studioCam.Render();
        }
    }

    // ------------------------------------------------------------------
    // Sparkles
    // ------------------------------------------------------------------

    private void Emit(Vector2 at, float cubePixels, float dt)
    {
        emitCarry += sparklesPerSecond * dt;
        while (emitCarry >= 1f)
        {
            emitCarry -= 1f;
            Vector2 dir = Random.insideUnitCircle;
            Spawn(at + dir * cubePixels * 0.5f, dir.normalized * Random.Range(30f, 150f) + Vector2.up * Random.Range(10f, 60f),
                  Random.Range(0.5f, 1.2f), Random.Range(14f, 34f));
        }
    }

    private void Burst(Vector2 at, int count, float minSpeed, float maxSpeed, float sizeScale)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            Spawn(at, dir * Random.Range(minSpeed, maxSpeed), Random.Range(0.6f, 1.4f), Random.Range(16f, 44f) * sizeScale);
        }
    }

    private void Spawn(Vector2 pos, Vector2 vel, float life, float size)
    {
        if (sparks == null) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i].alive) continue;
            sparks[i].alive = true;
            sparks[i].pos = pos; sparks[i].vel = vel;
            sparks[i].life = sparks[i].maxLife = life;
            sparks[i].size = size;
            sparks[i].spin = Random.Range(-180f, 180f);
            sparks[i].image.color = Color.Lerp(sparkleColor, Color.white, Random.value * 0.6f);
            sparks[i].rect.gameObject.SetActive(true);
            return;
        }
    }

    private void UpdateSparks(float dt)
    {
        if (sparks == null) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (!sparks[i].alive) continue;
            sparks[i].life -= dt;
            if (sparks[i].life <= 0f) { sparks[i].alive = false; sparks[i].rect.gameObject.SetActive(false); continue; }
            sparks[i].vel *= 1f - Mathf.Min(1f, dt * 1.6f);
            sparks[i].vel += Vector2.down * 40f * dt;
            sparks[i].pos += sparks[i].vel * dt;
            float k = sparks[i].life / sparks[i].maxLife;
            float s = sparks[i].size * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 0.5f + 0.2f);
            sparks[i].rect.anchoredPosition = sparks[i].pos;
            sparks[i].rect.sizeDelta = new Vector2(s, s);
            sparks[i].rect.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * sparks[i].spin);
            Color col = sparks[i].image.color; col.a = Mathf.Clamp01(k * 1.6f); sparks[i].image.color = col;
        }
    }

    private Sprite BuildSparkSprite()
    {
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float glow = Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                float cross = Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy)) * 7f) * Mathf.Clamp01(1f - r);   // four-pointed star
                float a = Mathf.Clamp01(glow * 0.6f + cross);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------
    // Building and cleaning up
    // ------------------------------------------------------------------

    private void Build(int tierIndex)
    {
        Cleanup();
        TMP_FontAsset font = clicker.UIFont;

        canvasRoot = PixelUIKit.CreateCanvas("Dragon Find", 690, new Vector2(1920f, 1080f), true);
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        dim = NewImage("Dark", canvasRoot.transform, new Color(0f, 0f, 0f, 0f));
        PixelUIKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // blocks clicks on the cube and the UI while it plays

        backpack = NewImage("Backpack", canvasRoot.transform, new Color(1f, 1f, 1f, 0f));
        backpack.sprite = PixelGhostMinigame.BuildBackpackSprite();
        backpack.preserveAspect = true;
        backpackRect = backpack.rectTransform;
        backpackRect.anchorMin = backpackRect.anchorMax = new Vector2(0f, 0f);
        backpackRect.pivot = new Vector2(0.5f, 0.5f);
        backpackRect.sizeDelta = new Vector2(backpackSize, backpackSize);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;

        GameObject raw = new GameObject("Dragon Cube", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(canvasRoot.transform, false);
        cubeImage = raw.GetComponent<RawImage>();
        cubeImage.raycastTarget = false;
        cubeRect = raw.GetComponent<RectTransform>();
        cubeRect.anchorMin = cubeRect.anchorMax = new Vector2(0f, 0f);
        cubeRect.pivot = new Vector2(0.5f, 0.5f);

        title = PixelUIKit.CreateText(font, canvasRoot.transform, "Title", "", 64f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.82f, 0.3f, 1f));
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 100f); tr.anchoredPosition = new Vector2(0f, -(bar + 40f));
        subtitle = PixelUIKit.CreateText(font, canvasRoot.transform, "Subtitle", "", 40f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform sr = subtitle.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(0f, 70f); sr.anchoredPosition = new Vector2(0f, -(bar + 140f));

        if (sparkSprite == null) sparkSprite = BuildSparkSprite();
        sparks = new Spark[160];
        for (int i = 0; i < sparks.Length; i++)
        {
            Image img = NewImage("Spark", canvasRoot.transform, Color.white);
            img.sprite = sparkSprite;
            RectTransform sr2 = img.rectTransform;
            sr2.anchorMin = sr2.anchorMax = new Vector2(0f, 0f);
            sr2.pivot = new Vector2(0.5f, 0.5f);
            img.gameObject.SetActive(false);
            sparks[i] = new Spark { rect = sr2, image = img };
        }
        emitCarry = 0f;

        BuildStudio(tierIndex);
        cubeImage.texture = rt;
        cubeImage.enabled = false;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void BuildStudio(int tierIndex)
    {
        studioRoot = new GameObject("Dragon Find Studio");
        studioRoot.transform.position = new Vector3(0f, -4300f, 0f);
        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(studioRoot.transform, false);
        GameObject model = clicker.CreateDisplayPixel(tierIndex, pivot, 1f);
        if (model == null)
        {
            // No model (should not happen): a plain cube so the show still works.
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(model.GetComponent<Collider>());
            model.transform.SetParent(pivot, false);
        }
        foreach (Collider col in model.GetComponentsInChildren<Collider>()) Destroy(col);
        model.transform.localPosition = Vector3.zero;

        rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "Dragon Find Picture" };
        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(studioRoot.transform, false);
        studioCam = camGo.AddComponent<Camera>();
        studioCam.enabled = false;
        studioCam.clearFlags = CameraClearFlags.SolidColor;
        studioCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        studioCam.orthographic = true;
        studioCam.orthographicSize = 0.98f;   // a cube's diagonal is 1.73, so it never clips while it turns
        studioCam.nearClipPlane = 0.1f;
        studioCam.farClipPlane = 30f;
        studioCam.allowHDR = false;
        studioCam.targetTexture = rt;
        camGo.transform.localPosition = new Vector3(0f, 0f, -6f);

        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(studioRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(2.5f, 3f, -5f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 30f;
        light.intensity = 6f;
        light.shadows = LightShadows.None;
    }

    private void Cleanup()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        if (studioRoot != null) Destroy(studioRoot);
        if (rt != null) { rt.Release(); Destroy(rt); }
        canvasRoot = null; studioRoot = null; rt = null; sparks = null; cubeImage = null;
    }
}
