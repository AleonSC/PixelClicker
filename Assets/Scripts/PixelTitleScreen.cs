using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The start screen. When the game launches the world is frozen behind a blurred picture of itself with a black bar across
/// the middle of the screen and a big Play button. Clicking Play: the button shrinks to nothing, the bar thins away while
/// the picture comes back into focus, and after a short delay the game starts.
///
/// The blur is made at start-up by rendering the main camera into a small texture (a few bilinear down-scales), so it needs
/// no post-processing. It shows again after the pause menu's Restart, like a fresh launch.
/// Added automatically by PixelClicker.
/// </summary>
public class PixelTitleScreen : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Showing = false;
    }

    [Header("Screen")]
    [Tooltip("Show the start screen when the game launches.")]
    [SerializeField] private bool showTitleScreen = true;

    [Tooltip("Sorting order of the screen's canvas (above every other window).")]
    [SerializeField] private int sortingOrder = 1000;

    [Header("Bar and Button")]
    [Min(0f)]
    [Tooltip("Height of the black bar across the middle of the screen (canvas units).")]
    [SerializeField] private float barHeight = 260f;

    [Tooltip("Colour of the bar.")]
    [SerializeField] private Color barColor = new Color(0f, 0f, 0f, 0.92f);

    [Tooltip("Size of the Play button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(420f, 150f);

    [Tooltip("Text on the Play button.")]
    [SerializeField] private string playText = "Play";

    [Tooltip("Font size of the Play button.")]
    [SerializeField] private float playFontSize = 80f;

    [Tooltip("Colour of the Play button.")]
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.65f, 0.35f, 1f);

    [Tooltip("Colour of the Play button's text.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Tooltip("Optional title shown above the Play button inside the bar. Empty = none.")]
    [SerializeField] private string titleText = "";

    [Tooltip("Font size of the title.")]
    [SerializeField] private float titleFontSize = 54f;

    [Header("Animation")]
    [Min(0.05f)]
    [Tooltip("Seconds the Play button takes to shrink to nothing.")]
    [SerializeField] private float buttonShrinkSeconds = 0.35f;

    [Min(0.05f)]
    [Tooltip("Seconds the bar takes to thin to nothing.")]
    [SerializeField] private float barThinSeconds = 0.8f;

    [Min(0.05f)]
    [Tooltip("Seconds the picture takes to come into focus.")]
    [SerializeField] private float focusSeconds = 1f;

    [Min(0f)]
    [Tooltip("Seconds to wait after the animation before the game starts.")]
    [SerializeField] private float startDelay = 0.3f;

    [Header("Blur")]
    [Range(2, 16)]
    [Tooltip("The light blur is the picture shrunk by this much and stretched back (bigger = blurrier).")]
    [SerializeField] private int lightBlurDivisor = 8;

    [Range(8, 64)]
    [Tooltip("The strong blur is the picture shrunk by this much (bigger = blurrier).")]
    [SerializeField] private int strongBlurDivisor = 32;

    [Tooltip("Darkening laid over the blur (alpha = how dark).")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.25f);

    // ------------------------------------------------------------------

    /// <summary>True while the start screen is up (the game is frozen).</summary>
    public static bool Showing { get; private set; }

    private GameObject canvasRoot;
    private Image cover, bar, dim;
    private RawImage lightImage, strongImage;
    private RectTransform barRect, buttonRect;
    private TMP_Text titleLabel;
    private Button playButton;
    private RenderTexture lightTexture, strongTexture;
    private float timeScaleBefore = 1f;
    private bool playing;

    private void Awake()
    {
        if (!showTitleScreen) { Destroy(this); return; }

        Showing = true;
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        Build();
        PixelWindows.Register(this, 1000, () => Showing, () => { }); // Escape does nothing here
    }

    private void Start()
    {
        if (Showing) StartCoroutine(CaptureRoutine());
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (Showing && playing == false && Time.timeScale == 0f) Time.timeScale = timeScaleBefore;
        Showing = false;
        ReleaseTextures();
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void ReleaseTextures()
    {
        if (lightTexture != null) { lightTexture.Release(); Destroy(lightTexture); lightTexture = null; }
        if (strongTexture != null) { strongTexture.Release(); Destroy(strongTexture); strongTexture = null; }
    }

    // ------------------------------------------------------------------

    private static RectTransform Stretched(GameObject go)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("Pixel Title Screen", sortingOrder, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);

        // Opaque cover until the blurred picture is ready (so the sharp game never flashes).
        GameObject coverGo = new GameObject("Cover", typeof(RectTransform), typeof(Image));
        coverGo.transform.SetParent(canvasRoot.transform, false);
        cover = coverGo.GetComponent<Image>();
        cover.color = Color.black;
        Stretched(coverGo);

        lightImage = MakeRaw("Light Blur");
        strongImage = MakeRaw("Strong Blur");

        GameObject dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dimGo.transform.SetParent(canvasRoot.transform, false);
        dim = dimGo.GetComponent<Image>();
        dim.color = dimColor;
        dim.raycastTarget = false;
        Stretched(dimGo);

        GameObject barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(canvasRoot.transform, false);
        bar = barGo.GetComponent<Image>();
        bar.color = barColor;
        bar.raycastTarget = false;
        barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, barHeight);

        if (!string.IsNullOrEmpty(titleText))
        {
            titleLabel = PixelUIKit.CreateText(font, canvasRoot.transform, "Title", titleText, titleFontSize,
                                               TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            RectTransform tr = titleLabel.rectTransform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(1200f, titleFontSize * 1.5f);
            tr.anchoredPosition = new Vector2(0f, buttonSize.y * 0.5f + titleFontSize * 0.2f + 10f);
        }

        playButton = PixelUIKit.CreateButton(font, canvasRoot.transform, "Play Button", playText, buttonSize, buttonColor,
                                             buttonTextColor, playFontSize);
        buttonRect = playButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = titleLabel != null ? new Vector2(0f, -titleFontSize * 0.5f) : Vector2.zero;
        playButton.onClick.AddListener(OnPlay);
    }

    private RawImage MakeRaw(string objectName)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(canvasRoot.transform, false);
        RawImage raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.enabled = false;
        Stretched(go);
        return raw;
    }

    // ------------------------------------------------------------------
    // The blurred picture
    // ------------------------------------------------------------------

    private IEnumerator CaptureRoutine()
    {
        yield return null; // let the first frame of the scene exist
        try { BuildBlur(); }
        catch (System.Exception e) { Debug.LogWarning("PixelTitleScreen: could not make the blurred background (" + e.Message + ").", this); }

        if (lightTexture != null && strongTexture != null)
        {
            lightImage.texture = lightTexture;
            strongImage.texture = strongTexture;
            lightImage.enabled = strongImage.enabled = true;
        }
        else
        {
            cover.color = new Color(0f, 0f, 0f, 0.85f); // no picture: just a dark cover
        }
    }

    private static RenderTexture Shrink(RenderTexture source, int divisor)
    {
        int w = Mathf.Max(2, source.width / divisor), h = Mathf.Max(2, source.height / divisor);
        RenderTexture target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        target.Create();
        Graphics.Blit(source, target);
        return target;
    }

    private void BuildBlur()
    {
        Camera cam = Camera.main != null ? Camera.main : PixelFind.First<Camera>();
        if (cam == null) return;

        RenderTexture full = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = cam.targetTexture;
        try
        {
            cam.targetTexture = full;
            cam.Render();
        }
        finally
        {
            cam.targetTexture = previous;
        }

        // Shrink in steps (smoother than one big jump) and keep the two sizes we want.
        RenderTexture step = Shrink(full, 2);
        RenderTexture.ReleaseTemporary(full);
        int size = 2;
        RenderTexture light = null, strong = null;
        while (size < strongBlurDivisor)
        {
            RenderTexture next = Shrink(step, 2);
            if (step != light) { step.Release(); Destroy(step); }
            step = next;
            size *= 2;
            if (light == null && size >= lightBlurDivisor) light = step;
            if (size >= strongBlurDivisor) strong = step;
        }
        if (light == null) light = step;
        if (strong == null) strong = step;
        lightTexture = light;
        strongTexture = strong;
    }

    // ------------------------------------------------------------------
    // Play
    // ------------------------------------------------------------------

    private void OnPlay()
    {
        if (playing) return;
        playing = true;
        playButton.interactable = false;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        PixelAudio.Play("game_start");
        cover.color = new Color(0f, 0f, 0f, 0f); // from now on the blur layers do the covering (without a picture it fades below)
        bool hasPicture = strongImage.enabled;
        Color coverStart = hasPicture ? Color.clear : new Color(0f, 0f, 0f, 0.85f);
        cover.color = coverStart;

        float total = Mathf.Max(focusSeconds, Mathf.Max(barThinSeconds, buttonShrinkSeconds));
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            float button = Mathf.Clamp01(t / buttonShrinkSeconds);
            float s = 1f - button * button; // shrinks faster and faster
            buttonRect.localScale = Vector3.one * Mathf.Max(0f, s);
            if (titleLabel != null) titleLabel.color = new Color(1f, 1f, 1f, 1f - button);

            float thin = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / barThinSeconds));
            barRect.sizeDelta = new Vector2(0f, barHeight * (1f - thin));

            float focus = Mathf.Clamp01(t / focusSeconds);
            SetAlpha(strongImage, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(focus / 0.7f)));
            SetAlpha(lightImage, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((focus - 0.3f) / 0.7f)));
            Color d = dimColor;
            d.a *= 1f - focus;
            dim.color = d;
            if (!hasPicture) cover.color = new Color(0f, 0f, 0f, coverStart.a * (1f - focus));
            yield return null;
        }

        barRect.sizeDelta = Vector2.zero;
        yield return new WaitForSecondsRealtime(startDelay);

        Time.timeScale = timeScaleBefore;
        Showing = false;
        Destroy(this);
    }

    private static void SetAlpha(RawImage image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
