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
        Reversing = false;
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

    [Tooltip("Main button text when there are saves and none has been loaded yet: pressing it loads the most recent one.")]
    [SerializeField] private string continueText = "Continue";

    [Tooltip("Main button text when there are no saves at all: a fresh game. Empty = the Play text always.")]
    [SerializeField] private string newGameText = "New Game";

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

    [Tooltip("Colour of the Quit button.")]
    [SerializeField] private Color quitColor = new Color(0.45f, 0.18f, 0.18f, 0.9f);

    [Header("Floor and Sky Pickers")]
    [Tooltip("Put the pickers in the bottom right corner of the screen (the sky above the floor). Off = either side of the Play button.")]
    [SerializeField] private bool pickersBottomRight = true;

    [Tooltip("Gap between the bottom right pickers and the screen's right / bottom edge.")]
    [SerializeField] private Vector2 pickerCornerMargin = new Vector2(48f, 40f);

    [Tooltip("Extra vertical space between the two bottom right pickers (the captions sit in it).")]
    [SerializeField] private float pickerStackGap = 52f;

    [Range(0f, 1f)]
    [Tooltip("How visible a picker is while the mouse is away (it fades up to full when the mouse comes near).")]
    [SerializeField] private float pickerRestAlpha = 0.6f;

    [Tooltip("Show the floor picker (right of Play) and sky picker (left of Play): a drop-down listing every look. A quick version of the Settings rows.")]
    [SerializeField] private bool showLookPickers = true;

    [Tooltip("Height of each picker drop-down.")]
    [SerializeField] private float lookBoxHeight = 56f;

    [Min(0f)]
    [Tooltip("Width of each picker drop-down. 0 = the default (300).")]
    [SerializeField] private float lookDropdownWidth = 0f;

    [Tooltip("Space between the Play / Load / Settings buttons and each picker.")]
    [SerializeField] private float lookGap = 40f;

    [Tooltip("Font size of the look's name in the drop-down.")]
    [SerializeField] private float lookNameFontSize = 22f;

    [Tooltip("Colour of the picker drop-down boxes.")]
    [SerializeField] private Color lookArrowColor = new Color(0.25f, 0.3f, 0.4f, 1f);

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

    /// <summary>True while the camera swings back to the close-up on Quit > Main menu (the black margins slide away meanwhile).</summary>
    public static bool Reversing { get; private set; }

    private GameObject canvasRoot, foreground;
    private Canvas canvas;
    private Image cover, bar, dim;
    private RawImage lightImage, midImage, strongImage, logoImage, fadeImage;
    private float strongPlayFactor = 1f, strongBlend = 1f;   // the Play transition's fade of the strong layers x how far the blur slider sits between two blur levels
    private RenderTexture fadeTexture;
    private bool lastLive, lastBlur, haveLastToggles;
    private float fadeAmount;
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
        public TMP_Dropdown dropdown;
        public int optionCount = -1;
        public bool refreshing;
        public string shownName = "";
        public int shownIndex = -2;
        public bool shownUsable;
        public RectTransform rect;
        public CanvasGroup group;
        public float alpha;      // 0 = hidden, 1 = shown (the picker only shows while the mouse is over it)
    }
    private readonly List<LookPicker> lookPickers = new List<LookPicker>();
    private RenderTexture liveTexture;
    private readonly List<RenderTexture> chain = new List<RenderTexture>();
    private readonly List<int> chainDivisors = new List<int>();
    private Texture2D logoTexture;
    private float logoAspect = 0.41f;
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
        // With the live background the floor fades away FIRST, then the camera swings back to the close-up.
        PixelFloor floor = PixelFloor.Instance;
        if (PixelTitleWanderers.LiveBackground && floor != null && floor.FadeOutThen(ReverseCamera)) return;
        ReverseCamera();
    }

    private void ReverseCamera()
    {
        PixelCameraIntro intro = PixelFind.First<PixelCameraIntro>();
        Reversing = true;
        if (intro != null && intro.PlayReverse(FinishReturn)) return;
        FinishReturn();
    }

    private void FinishReturn()
    {
        returning = false;
        Reversing = false;
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
        lightImage.enabled = midImage.enabled = strongImage.enabled = false;
        strongPlayFactor = 1f;
        fadeAmount = 0f;
        if (fadeImage != null) fadeImage.enabled = false;
        haveLastToggles = false;
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
        if (sharpTexture != null) { sharpTexture.Release(); Destroy(sharpTexture); sharpTexture = null; }
        if (fadeTexture != null) { fadeTexture.Release(); Destroy(fadeTexture); fadeTexture = null; }
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
        PixelBarGlow.Colorize(glowImages[0], glowImages[1], glowImages[4], PixelHud.GlowColourNow(Time.unscaledTime, glowColorSpeed, 0f, glowSaturation), a);
        PixelBarGlow.Colorize(glowImages[2], glowImages[3], glowImages[5], PixelHud.GlowColourNow(Time.unscaledTime, glowColorSpeed, PixelHud.GlowCustomColor.HasValue && !PixelHud.GlowCycle ? 0f : glowEdgeOffset, glowSaturation), a);
    }

    private int titleBarVersion = -1;
    private float nextSaveCheck;
    private bool anySave;
    private PixelSaveGame saveGame;

    /// <summary>True once Play has been pressed this run (a New Game after that has to reload the scene).</summary>
    public static bool PlayedOnce { get; private set; }

    /// <summary>Starts a brand-new game in this slot (from the Load window on an empty slot).</summary>
    public static void StartNewGame(int slot)
    {
        PixelTitleScreen title = PixelFind.First<PixelTitleScreen>();
        PixelSaveGame save = PixelFind.First<PixelSaveGame>();
        if (title == null || save == null) return;
        save.UseSlotForNewGame(slot);
        PixelSaveGame.HasLoadedSave = false;
        PixelSaveGame.AutoStartNewGame = true;   // (no Continue detour: OnPlay clears it)
        PixelSettingsSync.ResetToDefaults();   // a new game starts with the default settings
        if (!PlayedOnce) { title.OnPlay(); return; }
        // The game has been played already: reload the scene fresh, the title presses Play by itself.
        PixelHints.ResetSeen();
        PixelAchievements.ResetOnNextStart = true;
        PixelCrashLog.EndSessionCleanly();
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void Update()
    {
        if (!Showing || canvas == null) return;
        UpdateBarGlow();
        if (PixelSaveGame.AutoStartNewGame && !playing && playButton != null) { OnPlay(); return; }
        if (playButton != null)
        {
            if (Time.unscaledTime >= nextSaveCheck)
            {
                nextSaveCheck = Time.unscaledTime + 1f;
                if (saveGame == null) saveGame = PixelFind.First<PixelSaveGame>();
                anySave = saveGame != null && saveGame.TryGetMostRecentSlot(out int _);
            }
            string want = PixelSaveGame.HasLoadedSave ? playText : anySave ? continueText : string.IsNullOrEmpty(newGameText) ? playText : newGameText;
            TMP_Text playLabel = playButton.GetComponentInChildren<TMP_Text>();
            if (playLabel != null) PixelUIKit.SetText(playLabel, want);
        }
        if (bar != null && titleBarVersion != PixelHud.BarColorVersion) { titleBarVersion = PixelHud.BarColorVersion; bar.color = PixelHud.BarTint(barColor); }

        // While the pause menu's views are open on top of the title, hide the title's own widgets and drop below the menu.
        bool menuOpen = PixelPauseMenu.IsPaused;
        canvas.sortingOrder = menuOpen ? 400 : sortingOrder;
        if (foreground != null && foreground.activeSelf == menuOpen) foreground.SetActive(!menuOpen);

        if (!menuOpen) foreach (LookPicker p in lookPickers) RefreshPicker(p, false);
        UpdatePickerFade();
        UpdateLookTip(lookFont);

        if (logoRect != null && !playing)
        {
            // The logo must end above the middle bar: at a big UI scale the canvas is shorter, so it shrinks to fit.
            float top = logoTopMargin;
            RectTransform canvasRect = canvasRoot != null ? canvasRoot.GetComponent<RectTransform>() : null;
            float canvasH = canvasRect != null ? canvasRect.rect.height : 0f;
            float natural = logoWidth * logoAspect;
            if (canvasH > 100f)
            {
                float room = canvasH * 0.5f - barHeight * 0.5f - 16f;   // from the screen's top edge down to just above the bar
                top = Mathf.Min(logoTopMargin, room * 0.15f);
                float shrink = Mathf.Clamp(Mathf.Min(1f, (room - top - logoBobAmount) / natural), 0.3f, 1f);
                logoRect.sizeDelta = new Vector2(logoWidth * shrink, natural * shrink);
            }
            logoRect.anchoredPosition = new Vector2(0f, -top + Mathf.Sin(Time.unscaledTime * logoBobSpeed * Mathf.PI * 2f) * logoBobAmount);
        }
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
        PixelMenuFx.DecorateTitle(b);   // the fancy menu's hover glow and the shine that sweeps over it now and then
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
        midImage = MakeRaw("Mid Blur");   // the blur level just below the slider (the strong layer fades in over it: a smooth blur slider)
        strongImage = MakeRaw("Strong Blur");
        fadeImage = MakeRaw("Background Fade");   // the previous picture, fading away when Live / Blur background is switched

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
        // The bar (and its glow) is NOT under 'foreground': it stays visible, behind the pause menu's windows, while Settings / Load are open on the title.
        barGo.transform.SetParent(canvasRoot.transform, false);
        barGo.transform.SetSiblingIndex(foreground.transform.GetSiblingIndex());   // (just under the foreground, same drawing order as before)
        bar = barGo.GetComponent<Image>();
        bar.color = PixelHud.BarTint(barColor);
        titleBarVersion = PixelHud.BarColorVersion;
        bar.raycastTarget = false;
        barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0.5f);
        barRect.anchorMax = new Vector2(1f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0f, barHeight);

        if (barGlow) BuildBarGlow();

        // The wandering vendors and their (hover-only) toggle at the bottom left.
        PixelTitleWanderers wanderers = GetComponent<PixelTitleWanderers>();
        if (wanderers == null) wanderers = gameObject.AddComponent<PixelTitleWanderers>();
        wanderers.Setup(foreground.transform, font, canvasRoot.GetComponent<RectTransform>());

        if (showLogo) BuildLogo();

        // Play on top, Load + Settings side by side under it (together exactly as wide as Play).
        Vector2 centre = new Vector2(0.5f, 0.5f);
        // Load (a floppy disk), Settings (a cog) and Quit (an open door) share one row under Play, together as wide as Play.
        int iconCount = showQuit ? 3 : 2;
        float groupHeight = buttonSize.y + buttonGap + smallButtonHeight;
        float playY = groupHeight * 0.5f - buttonSize.y * 0.5f;
        float smallY = playY - buttonSize.y * 0.5f - buttonGap - smallButtonHeight * 0.5f;
        float iconWidth = (buttonSize.x - buttonGap * (iconCount - 1)) / iconCount;
        Vector2 iconSize = new Vector2(iconWidth, smallButtonHeight);
        float iconStep = iconWidth + buttonGap;
        float firstX = -iconStep * (iconCount - 1) * 0.5f;

        playButton = MakeButton(font, "Play Button", playText, buttonSize, buttonColor, playFontSize, centre, new Vector2(0f, playY), OnPlay);
        MakeIconButton(font, "Load Button", PixelMenuIcons.Floppy, loadText, iconSize, smallButtonColor, centre, new Vector2(firstX, smallY), OnLoad);
        MakeIconButton(font, "Settings Button", PixelMenuIcons.Cog, settingsText, iconSize, smallButtonColor, centre, new Vector2(firstX + iconStep, smallY), OnSettings);
        if (showQuit) MakeIconButton(font, "Quit Button", PixelMenuIcons.Door, quitText, iconSize, quitColor, centre, new Vector2(firstX + iconStep * 2f, smallY), OnQuit);

        // Quick floor / sky pickers either side of the button group.
        if (showLookPickers)
        {
            float pickerWidth = LookDropdownWidth;
            float pickerX = buttonSize.x * 0.5f + lookGap + pickerWidth * 0.5f;
            if (pickersBottomRight)
            {
                float x = -(pickerCornerMargin.x + pickerWidth * 0.5f), y0 = pickerCornerMargin.y + lookBoxHeight * 0.5f;
                BuildLookPicker(font, "Floor Picker", floorPickerCaption, () => PixelFloor.Instance, new Vector2(x, y0), new Vector2(1f, 0f));
                BuildLookPicker(font, "Sky Picker", skyPickerCaption, () => PixelSkybox.Instance, new Vector2(x, y0 + lookBoxHeight + pickerStackGap), new Vector2(1f, 0f));
            }
            else
            {
                BuildLookPicker(font, "Sky Picker", skyPickerCaption, () => PixelSkybox.Instance, new Vector2(-pickerX, 0f), new Vector2(0.5f, 0.5f));
                BuildLookPicker(font, "Floor Picker", floorPickerCaption, () => PixelFloor.Instance, new Vector2(pickerX, 0f), new Vector2(0.5f, 0.5f));
            }
        }

    }

    private string hoveredText;

    /// <summary>A button with a picture instead of a label; its name shows in a small tip while the mouse is over it.</summary>
    private Button MakeIconButton(TMP_FontAsset font, string objectName, Texture2D icon, string tip, Vector2 size, Color color, Vector2 anchor, Vector2 position,
                                  UnityEngine.Events.UnityAction onClick)
    {
        Button b = MakeButton(font, objectName, "", size, color, smallFontSize, anchor, position, onClick);
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
        iconGo.transform.SetParent(b.transform, false);
        RawImage raw = iconGo.GetComponent<RawImage>();
        raw.texture = icon;
        raw.raycastTarget = false;
        RectTransform ir = iconGo.GetComponent<RectTransform>();
        float side = Mathf.Min(size.x, size.y) * 0.78f;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(side, side);
        ir.anchoredPosition = Vector2.zero;
        PixelHoverTip hover = b.gameObject.AddComponent<PixelHoverTip>();
        hover.onEnter = () => hoveredText = tip;
        hover.onExit = () => { if (hoveredText == tip) hoveredText = null; };
        return b;
    }

    private float LookDropdownWidth => lookDropdownWidth > 1f ? lookDropdownWidth : 300f;

    /// <summary>A drop-down listing every look of the floor / sky (with a small caption above it).</summary>
    private void BuildLookPicker(TMP_FontAsset font, string objectName, string caption, System.Func<IPixelLookSource> source, Vector2 position, Vector2 anchor)
    {
        lookFont = font;
        float width = LookDropdownWidth;
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(foreground.transform, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = anchor;
        rr.pivot = new Vector2(0.5f, 0.5f);
        rr.sizeDelta = new Vector2(width, lookBoxHeight);
        rr.anchoredPosition = position;
        shrinkRects.Add(rr);

        LookPicker picker = new LookPicker { source = source, rect = rr, group = root.AddComponent<CanvasGroup>() };
        picker.group.alpha = 0f;
        PixelHoverTip hoverTip = root.AddComponent<PixelHoverTip>();
        hoverTip.onEnter = () => hoveredPicker = picker;
        hoverTip.onExit = () => { if (hoveredPicker == picker) hoveredPicker = null; };

        float fontSize = Mathf.Max(16f, lookNameFontSize);
        TMP_Dropdown dd = PixelUIKit.CreateDropdown(font, root.transform, "Dropdown", new Vector2(width, lookBoxHeight), lookArrowColor,
                                                    new Color(0.1f, 0.12f, 0.18f, 1f), buttonTextColor, fontSize, true);   // (true = a little preview picture beside each name)
        RectTransform dr = dd.GetComponent<RectTransform>();
        dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 0.5f);
        dr.anchoredPosition = Vector2.zero;
        picker.dropdown = dd;
        dd.onValueChanged.AddListener(value =>
        {
            if (playing || picker.refreshing) return;
            IPixelLookSource look = picker.source();
            if (look != null && look.Usable && value >= 0 && value < look.StyleCount) look.SetStyle(value);
            RefreshPicker(picker, true);
        });

        if (!string.IsNullOrEmpty(caption))
        {
            TMP_Text cap = PixelUIKit.CreateText(font, root.transform, "Caption", caption.ToUpperInvariant(), Mathf.Max(14f, fontSize * 0.7f),
                                                 TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.75f));
            cap.raycastTarget = false;
            RectTransform cr = cap.rectTransform;
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.sizeDelta = new Vector2(0f, fontSize * 1.3f);
            cr.anchoredPosition = new Vector2(0f, lookBoxHeight * 0.5f + 2f);
        }

        lookPickers.Add(picker);
        RefreshPicker(picker, true);
    }

    // ---- the picker's name shows in a small tooltip while the mouse is over it ----
    private LookPicker hoveredPicker;

    [Tooltip("Extra pixels around a floor / sky picker where the mouse still counts as over it (the pickers fade in only while hovered).")]
    [SerializeField] private float pickerHoverMargin = 28f;

    /// <summary>The floor and sky pickers are invisible until the mouse is over them: they fade in quickly and fade out slowly.</summary>
    private void UpdatePickerFade()
    {
        Vector2 pointer = PixelInput.PointerPosition();
        Vector3[] corners = new Vector3[4];
        foreach (LookPicker p in lookPickers)
        {
            if (p.rect == null || p.group == null) continue;
            p.rect.GetWorldCorners(corners);   // overlay canvas: world = screen pixels
            bool open = p.dropdown != null && p.dropdown.IsExpanded;
            bool over = !playing && (open || pointer.x >= corners[0].x - pickerHoverMargin && pointer.x <= corners[2].x + pickerHoverMargin
                        && pointer.y >= corners[0].y - pickerHoverMargin && pointer.y <= corners[2].y + pickerHoverMargin);
            p.alpha = Mathf.MoveTowards(p.alpha, over ? 1f : 0f, Time.unscaledDeltaTime / (over ? 0.12f : 0.35f));
            p.group.alpha = Mathf.Lerp(pickerRestAlpha, 1f, p.alpha);
            p.group.blocksRaycasts = p.alpha > 0.05f;
        }
    }
    private TMP_FontAsset lookFont;
    private GameObject lookTip;
    private TMP_Text lookTipLabel;

    private void UpdateLookTip(TMP_FontAsset font)
    {
        string tipText = !string.IsNullOrEmpty(hoveredText) ? hoveredText : (hoveredPicker != null ? hoveredPicker.shownName : null);
        bool show = !string.IsNullOrEmpty(tipText) && !playing && foreground != null && foreground.activeInHierarchy;
        if (!show) { if (lookTip != null && lookTip.activeSelf) lookTip.SetActive(false); return; }
        if (lookTip == null)
        {
            lookTip = new GameObject("Look Tip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            lookTip.transform.SetParent(foreground.transform, false);
            lookTip.GetComponent<CanvasGroup>().blocksRaycasts = false;
            Image bg = lookTip.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.12f, 0.95f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            lookTipLabel = PixelUIKit.CreateText(font, lookTip.transform, "Text", "", Mathf.Max(16f, lookNameFontSize), TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            lookTipLabel.raycastTarget = false;
            PixelUIKit.Stretch(lookTipLabel.rectTransform);
        }
        if (!lookTip.activeSelf) lookTip.SetActive(true);
        lookTip.transform.SetAsLastSibling();
        string name = tipText.ToUpperInvariant();
        if (lookTipLabel.text != name) lookTipLabel.text = name;
        RectTransform rt = lookTip.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(Mathf.Ceil(lookTipLabel.GetPreferredValues(name).x) + 36f, Mathf.Max(16f, lookNameFontSize) * 1.5f + 12f);
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
            rt.anchoredPosition = local + new Vector2(0f, 26f);
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

        p.shownName = usable ? look.StyleName(index) : "";
        if (p.dropdown == null) return;
        p.refreshing = true;
        int count = usable ? look.StyleCount : 0;
        if (p.optionCount != count)
        {
            p.optionCount = count;
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(count);
            for (int i = 0; i < count; i++) options.Add(new TMP_Dropdown.OptionData(look.StyleName(i), PixelUIKit.PreviewSprite(look.PreviewTexture(i), look.PreviewRect, look.PreviewColor(i))));
            p.dropdown.ClearOptions();
            p.dropdown.AddOptions(options);
            PixelDebug.Info("PixelTitleScreen: a look picker now lists " + count + " looks" + (usable ? "" : " (its source is not usable yet: " + (look == null ? "no component" : "no floor / no styles") + ")") + ".", p.dropdown);
        }
        if (usable) p.dropdown.SetValueWithoutNotify(index);
        p.dropdown.RefreshShownValue();
        p.dropdown.interactable = usable;
        p.refreshing = false;
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
        logoAspect = (float)height / width;
        logoRect.sizeDelta = new Vector2(logoWidth, logoWidth * logoAspect);
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

    private RenderTexture sharpTexture;

    /// <summary>Blur switched off: the live scene is drawn at full size, unblurred; without the live scene the floor picture is shown sharp.</summary>
    private void UpdateSharp(bool live)
    {
        if (live)
        {
            // The real scene, unblurred, drawn at full size into its own picture.
            Camera cam = Camera.main != null ? Camera.main : PixelFind.First<Camera>();
            if (cam == null) return;
            if (sharpTexture == null || sharpTexture.width != Screen.width || sharpTexture.height != Screen.height)
            {
                if (sharpTexture != null) { sharpTexture.Release(); Destroy(sharpTexture); }
                sharpTexture = NewTexture(Screen.width, Screen.height, 24, FilterMode.Bilinear);
            }
            RenderTexture previous = cam.targetTexture;
            try { cam.targetTexture = sharpTexture; cam.Render(); }
            finally { cam.targetTexture = previous; }
            strongImage.texture = sharpTexture;
            strongImage.enabled = true;
            lightImage.enabled = midImage.enabled = false;
            strongBlend = 1f;
            ApplyStrongAlpha();
        }
        else
        {
            if (sharpTexture == null || sharpTexture.width != Screen.width || sharpTexture.height != Screen.height)
            {
                if (sharpTexture != null) { sharpTexture.Release(); Destroy(sharpTexture); }
                sharpTexture = NewTexture(Screen.width, Screen.height, 24, FilterMode.Bilinear);
            }
            if (PixelFloor.Instance.TryGetTitlePicture(out Texture floorPicture, out Vector2 tiles, out Color tint) && floorPicture != null)
                Graphics.Blit(floorPicture, sharpTexture, tiles, PixelFloor.Instance.ScrollOffset);
            else
            {
                RenderTexture keep = RenderTexture.active;
                RenderTexture.active = sharpTexture;
                GL.Clear(true, true, tint);
                RenderTexture.active = keep;
            }
            strongImage.texture = sharpTexture;
            strongImage.enabled = true;
            lightImage.enabled = midImage.enabled = false;
            strongBlend = 1f;
            ApplyStrongAlpha();
        }
        pictureReady = true;
    }

    /// <summary>
    /// Switching 'Live background' or 'Blur background' changes the whole picture at once; instead the picture that was showing is kept as a snapshot
    /// on top and fades away over about a second, so the floor (or the sky) comes in gradually.
    /// </summary>
    private void UpdateBackgroundFade()
    {
        bool liveNow = PixelTitleWanderers.LiveBackground, blurNow = PixelTitleWanderers.BlurBackground;
        if (!haveLastToggles) { lastLive = liveNow; lastBlur = blurNow; haveLastToggles = true; }
        else if (liveNow != lastLive || blurNow != lastBlur)
        {
            lastLive = liveNow;
            lastBlur = blurNow;
            Texture shown = strongImage != null && strongImage.enabled ? strongImage.texture : null;
            if (shown != null && pictureReady && !playing)
            {
                if (fadeTexture == null || fadeTexture.width != shown.width || fadeTexture.height != shown.height)
                {
                    if (fadeTexture != null) { fadeTexture.Release(); Destroy(fadeTexture); }
                    fadeTexture = NewTexture(shown.width, shown.height, 0, FilterMode.Bilinear);
                }
                Graphics.Blit(shown, fadeTexture);
                fadeImage.texture = fadeTexture;
                fadeAmount = 1f;
                fadeImage.color = Color.white;
                fadeImage.enabled = true;
            }
        }
        if (fadeAmount > 0f)
        {
            fadeAmount = playing ? 0f : Mathf.MoveTowards(fadeAmount, 0f, Time.unscaledDeltaTime / 1.1f);
            float k = fadeAmount * fadeAmount * (3f - 2f * fadeAmount);
            fadeImage.color = new Color(1f, 1f, 1f, k);
            if (fadeAmount <= 0f) fadeImage.enabled = false;
        }
    }

    private void UpdateBlur()
    {
        UpdateBackgroundFade();
        bool live = PixelTitleWanderers.LiveBackground || PixelFloor.Instance == null;
        if (!PixelTitleWanderers.BlurBackground) { UpdateSharp(live); return; }
        Camera cam = Camera.main != null ? Camera.main : PixelFind.First<Camera>();
        if (live && cam == null) return;
        EnsureChain();

        if (live)
        {
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
        }
        else
        {
            // No live scene: the floor style's picture fills the whole background (then it is blurred like the scene would be).
            if (PixelFloor.Instance.TryGetTitlePicture(out Texture floorPicture, out Vector2 tiles, out Color tint) && floorPicture != null)
                Graphics.Blit(floorPicture, liveTexture, tiles, PixelFloor.Instance.ScrollOffset);
            else
            {
                RenderTexture keep = RenderTexture.active;
                RenderTexture.active = liveTexture;
                GL.Clear(true, true, tint);
                RenderTexture.active = keep;
            }
        }

        RenderTexture source = liveTexture;
        foreach (RenderTexture step in chain)
        {
            Graphics.Blit(source, step);
            source = step;
        }

        if (!pictureReady) pictureReady = true;
        // (also switches the layers back on / to the blurred pictures after the blur was off)
        lightImage.enabled = midImage.enabled = strongImage.enabled = true;
        lightImage.texture = PickChain(lightBlurDivisor);
        BlurLevelsForAmount(out int lo, out int hi, out float blend);
        midImage.texture = chain[lo];
        strongImage.texture = chain[hi];
        strongBlend = blend;
        ApplyStrongAlpha();
    }

    /// <summary>
    /// The blur slider is continuous: it sits between two of the shrunk copies (each is half the size of the one before), so the lower one is drawn fully and the higher one
    /// fades in over it. The position is measured in log space (each copy is twice as blurry as the last) so the slider feels even.
    /// </summary>
    private void BlurLevelsForAmount(out int lo, out int hi, out float blend)
    {
        lo = hi = Mathf.Max(0, chain.Count - 1);
        blend = 1f;
        if (chainDivisors.Count == 0) return;
        float a = Mathf.Clamp01(PixelTitleWanderers.BlurAmount);
        float logD = Mathf.Lerp(Mathf.Log(chainDivisors[0]), Mathf.Log(Mathf.Max(chainDivisors[0], strongBlurDivisor)), a);
        lo = 0;
        for (int i = 0; i < chainDivisors.Count; i++) if (Mathf.Log(chainDivisors[i]) <= logD + 0.0001f) lo = i;
        hi = Mathf.Min(lo + 1, chainDivisors.Count - 1);
        if (hi == lo) { blend = 0f; return; }
        blend = Mathf.Clamp01((logD - Mathf.Log(chainDivisors[lo])) / (Mathf.Log(chainDivisors[hi]) - Mathf.Log(chainDivisors[lo])));
    }

    private void ApplyStrongAlpha()
    {
        if (strongImage != null) SetAlpha(strongImage, strongPlayFactor * strongBlend);
        if (midImage != null) SetAlpha(midImage, strongPlayFactor);
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
        // 'Continue': nothing is loaded yet, so load the most recent save first. (Play after a load, a save or a visit to the game just resumes.)
        if (!PixelSaveGame.HasLoadedSave)
        {
            if (saveGame == null) saveGame = PixelFind.First<PixelSaveGame>();
            if (saveGame != null && saveGame.TryGetMostRecentSlot(out int _) && !PixelSaveGame.AutoStartNewGame) saveGame.ContinueMostRecent();
        }
        PixelSaveGame.AutoStartNewGame = false;
        PlayedOnce = true;
        playing = true;
        PixelTitleWanderers wander = GetComponent<PixelTitleWanderers>();
        if (wander != null) wander.FadeOut();   // the wandering vendors vanish at once
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
        strongPlayFactor = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(focus / 0.7f));
        ApplyStrongAlpha();
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
