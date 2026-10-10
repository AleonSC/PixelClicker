using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The start screen. When the game launches the world is frozen behind a live, blurred picture of itself (the cube keeps
/// spinning) with a pixel-art "Pixel Clicker" logo at the top, a black bar across the middle with a Play button, a Load and
/// a Settings button under it and a tiny Quit button at the bottom. Clicking Play: the buttons and logo shrink / fade away,
/// the bar thins away while the picture comes back into focus, and after a short delay the game starts.
///
/// The blur is made every frame by rendering the main camera into a small texture and shrinking it in a few bilinear steps,
/// so it needs no post-processing. Load / Settings open the pause menu's views above the title (Back returns here).
/// It shows again after the pause menu's Restart, like a fresh launch.
/// Added automatically by PixelClicker.
/// </summary>
[DefaultExecutionOrder(1000)]
public class PixelTitleScreen : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Showing = false;
        PlayTime = 0f;
    }

    /// <summary>Game time (Time.time) when the game last started running: Awake, then each time Play is pressed.</summary>
    public static float PlayTime { get; private set; }

    [Header("Screen")]
    [Tooltip("Show the start screen when the game launches.")]
    [SerializeField] private bool showTitleScreen = true;

    [Tooltip("Sorting order of the screen's canvas (above every other window). Lowered while Load / Settings are open so they show on top.")]
    [SerializeField] private int sortingOrder = 1000;

    [Header("Logo")]
    [Tooltip("Show the pixel-art 'Pixel Clicker' logo at the top of the screen.")]
    [SerializeField] private bool showLogo = true;

    [Min(100f)]
    [Tooltip("Width of the logo (canvas units).")]
    [SerializeField] private float logoWidth = 760f;

    [Tooltip("Distance from the top of the screen to the logo (canvas units).")]
    [SerializeField] private float logoTopMargin = 70f;

    [Tooltip("How far the logo bobs up and down (canvas units).")]
    [SerializeField] private float logoBobAmount = 8f;

    [Tooltip("How fast the logo bobs (cycles per second).")]
    [SerializeField] private float logoBobSpeed = 0.35f;

    [Tooltip("Colours of the letters of the logo, used in turn (PIXEL, then CLICKER).")]
    [SerializeField] private Color[] logoColors =
    {
        new Color(0.95f, 0.27f, 0.27f), new Color(1f, 0.58f, 0.15f), new Color(1f, 0.88f, 0.2f),
        new Color(0.3f, 0.85f, 0.4f), new Color(0.3f, 0.6f, 1f),
    };

    [Header("Bar and Buttons")]
    [Min(0f)]
    [Tooltip("Height of the black bar across the middle of the screen (canvas units).")]
    [SerializeField] private float barHeight = 250f;

    [Tooltip("Colour of the bar.")]
    [SerializeField] private Color barColor = new Color(0f, 0f, 0f, 0.92f);

    [Header("Bar Glow")]
    [Tooltip("An LED-strip style neon glow along the top and bottom edges of the bar that slowly changes colour (as if a light strip sat behind the panel).")]
    [SerializeField] private bool barGlow = true;

    [Tooltip("How far the glow spills outside the bar edges (canvas units).")]
    [SerializeField] private float glowSize = 110f;

    [Tooltip("How far the glow leaks onto the bar itself, inside its edges (canvas units).")]
    [SerializeField] private float glowInnerSize = 36f;

    [Tooltip("Brightness of the glow (0 - 1).")]
    [SerializeField] private float glowStrength = 0.7f;

    [Tooltip("How fast the colour changes: full colour cycles per second (0.03 = one cycle every half minute).")]
    [SerializeField] private float glowColorSpeed = 0.03f;

    [Tooltip("How different the top and bottom edge colours are (0 = the same, 0.5 = opposite).")]
    [SerializeField] private float glowEdgeOffset = 0.12f;

    [Tooltip("Colour saturation of the glow (0 = white, 1 = vivid).")]
    [SerializeField] private float glowSaturation = 0.85f;

    [Tooltip("Size of the Play button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(420f, 100f);

    [Tooltip("Text on the Play button.")]
    [SerializeField] private string playText = "Play";

    [Tooltip("Font size of the Play button.")]
    [SerializeField] private float playFontSize = 64f;

    [Tooltip("Colour of the Play button.")]
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.65f, 0.35f, 1f);

    [Tooltip("Colour of the Play button's text.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Min(20f)]
    [Tooltip("Height of the Load and Settings buttons (together they are as wide as the Play button).")]
    [SerializeField] private float smallButtonHeight = 58f;

    [Min(0f)]
    [Tooltip("Gap between the buttons (canvas units).")]
    [SerializeField] private float buttonGap = 12f;

    [Tooltip("Font size of the Load and Settings buttons.")]
    [SerializeField] private float smallFontSize = 32f;

    [Tooltip("Text on the Load button.")]
    [SerializeField] private string loadText = "Load";

    [Tooltip("Text on the Settings button.")]
    [SerializeField] private string settingsText = "Settings";

    [Tooltip("Colour of the Load and Settings buttons.")]
    [SerializeField] private Color smallButtonColor = new Color(0.25f, 0.3f, 0.4f, 1f);

    [Tooltip("Show the tiny Quit button at the very bottom of the screen.")]
    [SerializeField] private bool showQuit = true;

    [Tooltip("Text on the Quit button.")]
    [SerializeField] private string quitText = "Quit";

    [Tooltip("Size of the Quit button.")]
    [SerializeField] private Vector2 quitSize = new Vector2(110f, 34f);

    [Tooltip("Font size of the Quit button.")]
    [SerializeField] private float quitFontSize = 20f;

    [Tooltip("Colour of the Quit button.")]
    [SerializeField] private Color quitColor = new Color(0.45f, 0.18f, 0.18f, 0.9f);

    [Header("Floor and Sky Pickers")]
    [Tooltip("Show the small floor picker (right of Play) and sky picker (left of Play): a picture of the look with arrows either side. A quick version of the Settings rows.")]
    [SerializeField] private bool showLookPickers = true;

    [Tooltip("Size of the picture in each picker.")]
    [SerializeField] private float lookIconSize = 96f;

    [Tooltip("Width of the picker arrows (they are as tall as the picture).")]
    [SerializeField] private float lookArrowWidth = 40f;

    [Tooltip("Space between the Play / Load / Settings buttons and each picker.")]
    [SerializeField] private float lookGap = 40f;

    [Tooltip("Font size of the look's name under the picture (0 = no name).")]
    [SerializeField] private float lookNameFontSize = 22f;

    [Tooltip("Colour of the picker arrows.")]
    [SerializeField] private Color lookArrowColor = new Color(0.25f, 0.3f, 0.4f, 1f);

    [Tooltip("Colour of the thin frame around each picture.")]
    [SerializeField] private Color lookFrameColor = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("Small caption above the floor picker (empty = none).")]
    [SerializeField] private string floorPickerCaption = "Floor";

    [Tooltip("Small caption above the sky picker (empty = none).")]
    [SerializeField] private string skyPickerCaption = "Sky";

    [Header("Animation")]
    [Min(0.05f)]
    [Tooltip("Seconds the buttons take to shrink to nothing.")]
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

    [Range(2, 8)]
    [Tooltip("The live picture is rendered at 1/this of the screen size (bigger = cheaper).")]
    [SerializeField] private int liveDivisor = 4;

    [Tooltip("Darkening laid over the blur (alpha = how dark).")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.25f);

    // ------------------------------------------------------------------

    /// <summary>True while the start screen is up (the game is frozen).</summary>
    public static bool Showing { get; private set; }

    private GameObject canvasRoot, foreground;
    private Canvas canvas;
    private Image cover, bar, dim;
    private RawImage lightImage, strongImage, logoImage;
    private RectTransform barRect, logoRect;
    private readonly List<Image> glowImages = new List<Image>();   // [0,1] top outer/inner, [2,3] bottom outer/inner, [4] top line, [5] bottom line
    private float glowFade = 1f;
    private readonly List<RectTransform> shrinkRects = new List<RectTransform>();
    private readonly List<Button> buttons = new List<Button>();
    private Button playButton;

    /// <summary>One of the small floor / sky pickers beside Play.</summary>
    private class LookPicker
    {
        public System.Func<IPixelLookSource> source;
        public RawImage icon;
        public TMP_Text name;
        public int shownIndex = -2;
        public bool shownUsable;
    }
    private readonly List<LookPicker> lookPickers = new List<LookPicker>();
    private RenderTexture liveTexture;
    private readonly List<RenderTexture> chain = new List<RenderTexture>();
    private readonly List<int> chainDivisors = new List<int>();
    private Texture2D logoTexture;
    private float timeScaleBefore = 1f;
    private bool playing, pictureReady;
    private int framesShown;

    private void Awake()
    {
        PlayTime = Time.time;
        PixelWindows.Register(this, 1000, () => Showing, () => { }); // Escape does nothing here
        if (!showTitleScreen) return; // the component stays, so "Main menu" can still bring the screen back
        Open(false);
    }

    /// <summary>Pause menu > Quit > Main menu: zooms the camera back into the cube, then brings the title screen back.</summary>
    public static void ReturnToMenu()
    {
        PixelTitleScreen title = PixelFind.First<PixelTitleScreen>();
        if (title != null) title.BeginReturn();
    }

    private bool returning;

    private void BeginReturn()
    {
        if (Showing || returning) return;
        for (int i = 0; i < 10 && PixelWindows.CloseTopmost(); i++) { } // close shop / inventory / log first
        returning = true;
        PixelTimeStop.EndAll(); // no frozen / slowed time on the title screen
        PixelCameraIntro intro = PixelFind.First<PixelCameraIntro>();
        if (intro != null && intro.PlayReverse(FinishReturn)) return;
        FinishReturn();
    }

    private void FinishReturn()
    {
        returning = false;
        Open(true);
    }

    /// <summary>Shows the screen (freezing the game). 'animated' = fade it in the way Play fades it out, in reverse.</summary>
    private void Open(bool animated)
    {
        if (Showing) return;
        Showing = true;
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        if (canvasRoot == null) Build();
        canvasRoot.SetActive(true);

        playing = false;
        pictureReady = false;
        framesShown = animated ? 2 : 0;
        cover.color = animated ? Color.clear : Color.black;
        lightImage.enabled = strongImage.enabled = false;
        foreach (Button b in buttons) b.interactable = !animated;
        if (animated)
        {
            ApplyTransition(TransitionSeconds); // everything hidden, then it comes in
            StartCoroutine(ReturnRoutine());
        }
        else ApplyTransition(0f);
    }

    private IEnumerator ReturnRoutine()
    {
        float guard = 0f;
        while (!pictureReady && guard < 1f) { guard += Time.unscaledDeltaTime; yield return null; } // wait for the first blurred frame

        float t = TransitionSeconds;
        while (t > 0f)
        {
            t -= Time.unscaledDeltaTime;
            ApplyTransition(Mathf.Max(0f, t));
            yield return null;
        }
        ApplyTransition(0f);
        foreach (Button b in buttons) b.interactable = true;
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (Showing && playing == false && Time.timeScale == 0f) Time.timeScale = timeScaleBefore;
        Showing = false;
        ReleaseTextures();
        if (logoTexture != null) Destroy(logoTexture);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void ReleaseTextures()
    {
        if (liveTexture != null) { liveTexture.Release(); Destroy(liveTexture); liveTexture = null; }
        foreach (RenderTexture rt in chain) if (rt != null) { rt.Release(); Destroy(rt); }
        chain.Clear();
        chainDivisors.Clear();
    }

    private void BuildBarGlow()
    {
        glowImages.Clear();
        glowImages.Add(PixelBarGlow.AddStrip(barRect, "Glow Top", true, true, glowSize));
        glowImages.Add(PixelBarGlow.AddStrip(barRect, "Glow Top Inner", true, false, glowInnerSize));
        glowImages.Add(PixelBarGlow.AddStrip(barRect, "Glow Bottom", false, true, glowSize));
        glowImages.Add(PixelBarGlow.AddStrip(barRect, "Glow Bottom Inner", false, false, glowInnerSize));
        glowImages.Add(PixelBarGlow.AddLine(barRect, "Line Top", true));
        glowImages.Add(PixelBarGlow.AddLine(barRect, "Line Bottom", false));
    }

    private void UpdateBarGlow()
    {
        if (glowImages.Count < 6) return;
        bool on = PixelHud.BarGlow;   // the Settings switch covers every bar glow
        if (glowImages[0].gameObject.activeSelf != on) foreach (Image g in glowImages) g.gameObject.SetActive(on);
        if (!on) return;
        float shimmer = 0.93f + 0.07f * Mathf.Sin(Time.unscaledTime * 1.3f);   // a very slight breathing
        float a = Mathf.Clamp01(glowStrength) * glowFade * shimmer;
        PixelBarGlow.Colorize(glowImages[0], glowImages[1], glowImages[4], PixelBarGlow.EdgeColor(PixelHud.GlowHue, Time.unscaledTime, PixelHud.GlowCycle ? glowColorSpeed : 0f, 0f, glowSaturation), a);
        PixelBarGlow.Colorize(glowImages[2], glowImages[3], glowImages[5], PixelBarGlow.EdgeColor(PixelHud.GlowHue, Time.unscaledTime, PixelHud.GlowCycle ? glowColorSpeed : 0f, glowEdgeOffset, glowSaturation), a);
    }

    private void Update()
    {
        if (!Showing || canvas == null) return;
        UpdateBarGlow();

        // While the pause menu's views are open on top of the title, hide the title's own widgets and drop below the menu.
        bool menuOpen = PixelPauseMenu.IsPaused;
        canvas.sortingOrder = menuOpen ? 400 : sortingOrder;
        if (foreground != null && foreground.activeSelf == menuOpen) foreground.SetActive(!menuOpen);

        if (!menuOpen) foreach (LookPicker p in lookPickers) RefreshPicker(p, false);

        if (logoRect != null && !playing)
            logoRect.anchoredPosition = new Vector2(0f, -logoTopMargin + Mathf.Sin(Time.unscaledTime * logoBobSpeed * Mathf.PI * 2f) * logoBobAmount);
    }

    private void LateUpdate()
    {
        if (!Showing || canvas == null) return;
        // Keep the blurred picture live (the cube spins behind the title). Waits one frame so the scene exists.
        framesShown++;
        if (framesShown < 2) return;
        try { UpdateBlur(); }
        catch (System.Exception e)
        {
            Debug.LogWarning("PixelTitleScreen: could not make the blurred background (" + e.Message + ").", this);
            enabled = false;
            if (!pictureReady) cover.color = new Color(0f, 0f, 0f, 0.85f);
        }
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

    private Button MakeButton(TMP_FontAsset font, string objectName, string label, Vector2 size, Color color, float fontSize,
                              Vector2 anchor, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Button b = PixelUIKit.CreateButton(font, foreground.transform, objectName, label, size, color, buttonTextColor, fontSize);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.anchoredPosition = position;
        b.onClick.AddListener(onClick);
        shrinkRects.Add(r);
        buttons.Add(b);
        return b;
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("Pixel Title Screen", sortingOrder, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);
        canvas = canvasRoot.GetComponent<Canvas>();

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

        // Everything the player interacts with lives under 'foreground' (hidden while the pause menu shows on top).
        foreground = new GameObject("Foreground", typeof(RectTransform));
        foreground.transform.SetParent(canvasRoot.transform, false);
        Stretched(foreground);

        GameObject barGo = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        barGo.transform.SetParent(foreground.transform, false);
        bar = barGo.GetComponent<Image>();
        bar.color = barColor;
        bar.raycastTarget = false;
        barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, barHeight);

        if (barGlow) BuildBarGlow();

        if (showLogo) BuildLogo();

        // Play on top, Load + Settings side by side under it (together exactly as wide as Play).
        Vector2 centre = new Vector2(0.5f, 0.5f);
        float groupHeight = buttonSize.y + buttonGap + smallButtonHeight;
        float playY = groupHeight * 0.5f - buttonSize.y * 0.5f;
        float smallY = -groupHeight * 0.5f + smallButtonHeight * 0.5f;
        float smallWidth = (buttonSize.x - buttonGap) * 0.5f;
        Vector2 smallSize = new Vector2(smallWidth, smallButtonHeight);
        float smallX = (smallWidth + buttonGap) * 0.5f;

        playButton = MakeButton(font, "Play Button", playText, buttonSize, buttonColor, playFontSize, centre, new Vector2(0f, playY), OnPlay);
        MakeButton(font, "Load Button", loadText, smallSize, smallButtonColor, smallFontSize, centre, new Vector2(-smallX, smallY), OnLoad);
        MakeButton(font, "Settings Button", settingsText, smallSize, smallButtonColor, smallFontSize, centre, new Vector2(smallX, smallY), OnSettings);

        // Quick floor / sky pickers either side of the button group.
        if (showLookPickers)
        {
            float pickerWidth = lookArrowWidth * 2f + lookIconSize + 16f;
            float pickerX = buttonSize.x * 0.5f + lookGap + pickerWidth * 0.5f;
            BuildLookPicker(font, "Sky Picker", skyPickerCaption, () => PixelSkybox.Instance, new Vector2(-pickerX, 0f));
            BuildLookPicker(font, "Floor Picker", floorPickerCaption, () => PixelFloor.Instance, new Vector2(pickerX, 0f));
        }

        if (showQuit)
            MakeButton(font, "Quit Button", quitText, quitSize, quitColor, quitFontSize, new Vector2(0.5f, 0f),
                       new Vector2(0f, quitSize.y * 0.5f + 12f), OnQuit);
    }

    /// <summary>A picture of the current look with an arrow either side (and its name under it).</summary>
    private void BuildLookPicker(TMP_FontAsset font, string objectName, string caption, System.Func<IPixelLookSource> source, Vector2 position)
    {
        float width = lookArrowWidth * 2f + lookIconSize + 16f;
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(foreground.transform, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(width, lookIconSize);
        rr.anchoredPosition = position;
        shrinkRects.Add(rr);

        LookPicker picker = new LookPicker { source = source };

        // Frame, then the picture inside it.
        GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(root.transform, false);
        Image frameImage = frame.GetComponent<Image>();
        frameImage.color = lookFrameColor;
        frameImage.raycastTarget = false;
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0.5f, 0.5f);
        fr.sizeDelta = new Vector2(lookIconSize, lookIconSize);

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
        iconGo.transform.SetParent(frame.transform, false);
        picker.icon = iconGo.GetComponent<RawImage>();
        picker.icon.raycastTarget = false;
        RectTransform ir = iconGo.GetComponent<RectTransform>();
        ir.anchorMin = Vector2.zero;
        ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(3f, 3f);
        ir.offsetMax = new Vector2(-3f, -3f);

        for (int side = -1; side <= 1; side += 2)
        {
            int direction = side;
            Button arrow = PixelUIKit.CreateButton(font, root.transform, side < 0 ? "Previous" : "Next", side < 0 ? "<" : ">",
                                                   new Vector2(lookArrowWidth, lookIconSize), lookArrowColor, buttonTextColor, smallFontSize);
            RectTransform ar = arrow.GetComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(side < 0 ? 0f : 1f, 0.5f);
            ar.anchoredPosition = Vector2.zero;
            PixelUIKit.UseArrowGlyph(arrow, side < 0);
            arrow.onClick.AddListener(() =>
            {
                if (playing) return;
                IPixelLookSource look = picker.source();
                if (look != null && look.Usable) look.Step(direction);
                RefreshPicker(picker, true);
            });
            buttons.Add(arrow);
        }

        if (!string.IsNullOrEmpty(caption))
        {
            TMP_Text cap = PixelUIKit.CreateText(font, root.transform, "Caption", caption, Mathf.Max(14f, lookNameFontSize * 0.9f),
                                                 TextAlignmentOptions.Bottom, FontStyles.Bold, new Color(1f, 1f, 1f, 0.7f));
            RectTransform cr = cap.rectTransform;
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.sizeDelta = new Vector2(0f, lookNameFontSize * 1.4f);
            cr.anchoredPosition = new Vector2(0f, 2f);
            cap.raycastTarget = false;
        }

        if (lookNameFontSize > 0f)
        {
            picker.name = PixelUIKit.CreateText(font, root.transform, "Name", "", lookNameFontSize, TextAlignmentOptions.Top,
                                                FontStyles.Normal, buttonTextColor);
            RectTransform nr = picker.name.rectTransform;
            nr.anchorMin = new Vector2(0f, 0f);
            nr.anchorMax = new Vector2(1f, 0f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(40f, lookNameFontSize * 1.4f);
            nr.anchoredPosition = new Vector2(0f, -4f);
            picker.name.raycastTarget = false;
            picker.name.enableAutoSizing = true;
            picker.name.fontSizeMax = lookNameFontSize;
            picker.name.fontSizeMin = 12f;
        }

        lookPickers.Add(picker);
        RefreshPicker(picker, true);
    }

    /// <summary>Updates a picker's picture and name when the look changed (from here, Settings, or the look waking up).</summary>
    private static void RefreshPicker(LookPicker p, bool force)
    {
        IPixelLookSource look = p.source();
        bool usable = look != null && look.Usable;
        int index = usable ? look.Current : -1;
        if (!force && index == p.shownIndex && usable == p.shownUsable) return;
        p.shownIndex = index;
        p.shownUsable = usable;

        p.icon.texture = usable ? look.PreviewTexture(index) : null;
        p.icon.color = usable ? look.PreviewColor(index) : new Color(1f, 1f, 1f, 0.15f);
        p.icon.uvRect = usable ? look.PreviewRect : new Rect(0f, 0f, 1f, 1f);
        if (p.name != null) p.name.text = usable ? look.StyleName(index) : "";
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
    // The logo: a 5x7 pixel font drawn into one texture (bevelled cells with a drop shadow)
    // ------------------------------------------------------------------

    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
        { 'I', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" } },
        { 'X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" } },
        { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
        { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
        { 'C', new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" } },
        { 'K', new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" } },
        { 'R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" } },
    };

    private void BuildLogo()
    {
        const string top = "PIXEL", bottom = "CLICKER";
        const int cell = 8, margin = 12, letterCols = 5, gapCols = 1, rows = 7;
        int cols = bottom.Length * letterCols + (bottom.Length - 1) * gapCols;
        int width = cols * cell + margin * 2;
        int height = (rows * 2 + 1) * cell + margin * 2;

        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

        int colorIndex = 0;
        DrawWord(pixels, width, height, top, cols, 0, ref colorIndex, cell, margin, letterCols, gapCols, rows);
        DrawWord(pixels, width, height, bottom, cols, rows + 1, ref colorIndex, cell, margin, letterCols, gapCols, rows);

        logoTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Pixel Clicker Logo",
        };
        logoTexture.SetPixels32(pixels);
        logoTexture.Apply(false, true);

        GameObject go = new GameObject("Logo", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(foreground.transform, false);
        logoImage = go.GetComponent<RawImage>();
        logoImage.texture = logoTexture;
        logoImage.raycastTarget = false;
        logoRect = go.GetComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = logoRect.pivot = new Vector2(0.5f, 1f);
        logoRect.sizeDelta = new Vector2(logoWidth, logoWidth * height / width);
        logoRect.anchoredPosition = new Vector2(0f, -logoTopMargin);
    }

    private void DrawWord(Color32[] pixels, int width, int height, string word, int totalCols, int rowOffset, ref int colorIndex,
                          int cell, int margin, int letterCols, int gapCols, int rows)
    {
        int wordCols = word.Length * letterCols + (word.Length - 1) * gapCols;
        int startCol = (totalCols - wordCols) / 2;
        Color[] palette = logoColors != null && logoColors.Length > 0 ? logoColors : new[] { Color.white };

        for (int li = 0; li < word.Length; li++)
        {
            string[] glyph;
            if (!Glyphs.TryGetValue(word[li], out glyph)) continue;
            Color baseColor = palette[colorIndex % palette.Length];
            colorIndex++;
            int letterCol = startCol + li * (letterCols + gapCols);
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < letterCols; c++)
            {
                if (glyph[r][c] != '#') continue;
                int px = margin + (letterCol + c) * cell;
                int py = height - margin - (rowOffset + r + 1) * cell; // row 0 is the top
                // Lighter toward the top of each letter.
                Color tint = Color.Lerp(baseColor, Color.white, 0.25f * (1f - r / (float)(rows - 1)));
                FillCell(pixels, width, height, px + 3, py - 3, cell, new Color(0f, 0f, 0f, 0.55f), null); // drop shadow
                FillCell(pixels, width, height, px, py, cell, tint, tint);
            }
        }
    }

    /// <summary>Draws one square cell; with a base colour it gets a light top-left and dark bottom-right bevel.</summary>
    private static void FillCell(Color32[] pixels, int width, int height, int x0, int y0, int size, Color fill, Color? bevelBase)
    {
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int px = x0 + x, py = y0 + y;
            if (px < 0 || py < 0 || px >= width || py >= height) continue;
            Color c = fill;
            if (bevelBase.HasValue)
            {
                bool dark = y < 2 || x >= size - 2;
                bool light = y >= size - 2 || x < 2;
                if (dark) c = Color.Lerp(fill, Color.black, 0.45f);
                else if (light) c = Color.Lerp(fill, Color.white, 0.55f);
            }
            else if (pixels[py * width + px].a > 0) continue; // shadows never cover letters
            pixels[py * width + px] = c;
        }
    }

    // ------------------------------------------------------------------
    // The blurred picture (live)
    // ------------------------------------------------------------------

    private RenderTexture NewTexture(int w, int h, int depth, FilterMode filter)
    {
        RenderTexture rt = new RenderTexture(Mathf.Max(2, w), Mathf.Max(2, h), depth, RenderTextureFormat.ARGB32)
        {
            filterMode = filter,
            wrapMode = TextureWrapMode.Clamp,
        };
        rt.Create();
        return rt;
    }

    private void EnsureChain()
    {
        if (liveTexture != null) return;
        int w = Screen.width, h = Screen.height;
        liveTexture = NewTexture(w / liveDivisor, h / liveDivisor, 24, FilterMode.Bilinear);

        // A chain of ever smaller copies: /2 of the live size each step, up to the strong blur.
        int divisor = liveDivisor;
        while (divisor < strongBlurDivisor || chain.Count == 0)
        {
            divisor *= 2;
            chain.Add(NewTexture(w / divisor, h / divisor, 0, FilterMode.Bilinear));
            chainDivisors.Add(divisor);
            if (chain.Count > 8) break;
        }

        lightImage.texture = PickChain(lightBlurDivisor);
        strongImage.texture = PickChain(strongBlurDivisor);
    }

    private RenderTexture PickChain(int wanted)
    {
        for (int i = 0; i < chain.Count; i++) if (chainDivisors[i] >= wanted) return chain[i];
        return chain[chain.Count - 1];
    }

    private void UpdateBlur()
    {
        Camera cam = Camera.main != null ? Camera.main : PixelFind.First<Camera>();
        if (cam == null) return;
        EnsureChain();

        RenderTexture previous = cam.targetTexture;
        try
        {
            cam.targetTexture = liveTexture;
            cam.Render();
        }
        finally
        {
            cam.targetTexture = previous;
        }

        RenderTexture source = liveTexture;
        foreach (RenderTexture step in chain)
        {
            Graphics.Blit(source, step);
            source = step;
        }

        if (!pictureReady)
        {
            pictureReady = true;
            lightImage.enabled = strongImage.enabled = true;
        }
    }

    // ------------------------------------------------------------------
    // Buttons
    // ------------------------------------------------------------------

    private void OnLoad()
    {
        if (!playing) PixelPauseMenu.OpenLoadFromTitle();
    }

    private void OnSettings()
    {
        if (!playing) PixelPauseMenu.OpenSettingsFromTitle();
    }

    private void OnQuit()
    {
        if (playing) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnPlay()
    {
        if (playing) return;
        playing = true;
        foreach (Button b in buttons) b.interactable = false;
        StartCoroutine(PlayRoutine());
    }

    private float TransitionSeconds => Mathf.Max(focusSeconds, Mathf.Max(barThinSeconds, buttonShrinkSeconds));

    /// <summary>
    /// The look of the screen 't' seconds into the Play animation (0 = the full title screen, TransitionSeconds = gone).
    /// Play runs t upward; coming back from the game runs it downward.
    /// </summary>
    private void ApplyTransition(float t)
    {
        float button = Mathf.Clamp01(t / buttonShrinkSeconds);
        float s = 1f - button * button; // shrinks faster and faster
        foreach (RectTransform r in shrinkRects) if (r != null) r.localScale = Vector3.one * Mathf.Max(0f, s);
        if (logoImage != null) logoImage.color = new Color(1f, 1f, 1f, 1f - button);

        float thin = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / barThinSeconds));
        barRect.sizeDelta = new Vector2(0f, barHeight * (1f - thin));
        glowFade = 1f - thin;

        float focus = Mathf.Clamp01(t / focusSeconds);
        SetAlpha(strongImage, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(focus / 0.7f)));
        SetAlpha(lightImage, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((focus - 0.3f) / 0.7f)));
        Color d = dimColor;
        d.a *= 1f - focus;
        dim.color = d;
        if (!pictureReady && playing) cover.color = new Color(0f, 0f, 0f, 0.85f * (1f - focus)); // no picture: a dark cover fades instead
    }

    private IEnumerator PlayRoutine()
    {
        PixelAudio.Play("game_start");
        // The blur layers do the covering now; the black cover behind them must go or it shows through as they fade (a black flash).
        cover.color = pictureReady ? Color.clear : new Color(0f, 0f, 0f, 0.85f);

        float total = TransitionSeconds;
        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            ApplyTransition(t);
            yield return null;
        }

        barRect.sizeDelta = Vector2.zero;
        yield return new WaitForSecondsRealtime(startDelay);

        // The screen stays built (hidden) so the pause menu's "Main menu" can bring it back.
        Time.timeScale = timeScaleBefore;
        Showing = false;
        playing = false;
        PlayTime = Time.time;
        canvasRoot.SetActive(false);
        ReleaseTextures();
        pictureReady = false;
    }

    private static void SetAlpha(RawImage image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
