// ---- System notes (moved here from CLAUDE.md) ----
// PixelHud.cs
//   Screen frame: black bar along the top and bottom (barHeight) and the docked buttons. PixelHud.Ensure(gameObject) then
//   hud.Dock(buttonRect, anchor(0|1,0|1), keepOutWhile) gives a button the standard ButtonSize and makes it slide off screen (only a
//   sliver shows) until the mouse is near (PixelDockedButton); it stays out while its window is open. Windows that open from a docked
//   button use hud.SideShownInset / hud.WindowOffset for position (both equal the gap the docked buttons keep from the black margin, ShownInset, so windows and buttons line up with the same gap on every side); top/bottom-anchored overlays add PixelHud.Instance.BarHeight. Used by
//   Inventory, Shop, Log and Crafting buttons. Menu buttons setting (PixelHud.ButtonMode, PlayerPrefs PixelClicker.Setting.ButtonMode,
//   per save, Settings > Interface 'Menu buttons'): 0 peeking (sliver shows, default), 1 always showing (SlideButtons false), 2 hidden
//   (Sliver 0 until the mouse is near). While 'always showing' (mode 1) each docked button gets its own nested canvas with
//   overrideSorting at alwaysOutSortingOrder (0 = 90, PixelDockedButton.UpdateSorting, plus a GraphicRaycaster), below every window
//   canvas (100+), so windows overlay the buttons; other modes use the window's own canvas order again. Bar colour: Settings > Display
//   'Custom bar colour' tick box + an eyedropper button beside it (PixelHud.BarColorOn / BarColorPicked / SetBarColorFromColor,
//   PlayerPrefs PixelClicker.Setting.BarColorOn + BarColor (hex); the player's own, NOT per save - see the note in PixelSettingsSync.Known_); the game's top / bottom margins
//   (PixelHud.barImages) and the main menu's middle bar (PixelTitleScreen.bar) repaint when PixelHud.BarColorVersion changes, using
//   PixelHud.BarTint(base) (the picked colour with the bar's own opacity; the menu bar stays 92% opaque). The docked buttons sit
//   towards the screen edge, not centred in the bar (buttonEdgeBias, 0 = 0.55), to stay clear of the neon bar glow. The pause menu
//   adds this component if missing.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The screen frame: a black bar along the top and bottom of the screen, and the "docked" buttons that live on them.
/// Every main UI button (Inventory, Shop, Log, Crafting) has the same size, and normally sits slid off the screen with
/// only a thin sliver showing. When the mouse comes near, the button slides out onto the bar so it can be clicked; it
/// stays out while its window is open. Windows that open next to a button start below/above the bar.
///
/// Add this to any GameObject. The UI scripts add one automatically if the scene has none.
/// </summary>
public class PixelHud : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("Gap between two docked buttons that share a corner of the bars (canvas units). 0 = the default (10).")]
    [SerializeField] private float cornerGap = 0f;

    /// <summary>Gap between two docked buttons side by side in a corner.</summary>
    public float CornerGap => cornerGap > 0f ? cornerGap : 10f;

    [Header("Black Bars")]
    [Tooltip("Show a black bar along the top and bottom of the screen.")]
    [SerializeField] private bool barsEnabled = true;

    [Min(20f)]
    [Tooltip("Height of each bar (canvas units).")]
    [SerializeField] private float barHeight = 84f;

    [Tooltip("Colour of the bars.")]
    [SerializeField] private Color barColor = Color.black;

    [Tooltip("Sorting order of the bars' canvas. It must be BELOW every other UI canvas (the buttons' canvases are 100+), so it is kept at -1 or lower whatever you type.")]
    [SerializeField] private int barsSortingOrder = -10;

    [Tooltip("Canvas sorting order the docked buttons get while 'Menu buttons' is set to always showing: below every window (the lowest window canvas is 100), so every window covers them. 0 = the default (90).")]
    [SerializeField] private int alwaysOutSortingOrder = 0;

    internal int AlwaysOutOrder => alwaysOutSortingOrder != 0 ? alwaysOutSortingOrder : 90;

    [Header("Bar Glow")]
    [Tooltip("Neon LED-strip glow along the inner edges of the top and bottom bars that slowly changes colour. The player can switch it off in Settings (Display).")]
    [SerializeField] private bool barGlowAllowed = true;

    [Tooltip("How far the glow spills past the bar edges into the play area (canvas units).")]
    [SerializeField] private float glowSize = 70f;

    [Tooltip("How far the glow leaks onto the bar itself (canvas units).")]
    [SerializeField] private float glowInnerReach = 12f;

    [Tooltip("Brightness of the glow (0 - 1).")]
    [SerializeField] private float glowStrength = 0.6f;

    [Tooltip("How fast the colour changes: full colour cycles per second (0.03 = one cycle every half minute).")]
    [SerializeField] private float glowColorSpeed = 0.03f;

    [Tooltip("How different the top and bottom bar colours are (0 = the same, 0.5 = opposite).")]
    [SerializeField] private float glowEdgeOffset = 0.12f;

    [Tooltip("Colour saturation of the glow (0 = white, 1 = vivid).")]
    [SerializeField] private float glowSaturation = 0.85f;


    [Header("Buttons (all the same size)")]
    [Tooltip("Size of every docked button (canvas units). Keep the height a little less than the bar height.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(230f, 64f);

    [Tooltip("Text size on the docked buttons (long names shrink to fit).")]
    [SerializeField] private float buttonFontSize = 30f;

    [Tooltip("Distance of a button from the left or right edge of the screen.")]
    [SerializeField] private float sideMargin = 24f;

    [Header("Sliding")]
    [Tooltip("Slide the buttons off screen until the mouse comes near. Off = buttons always stay out.")]
    [SerializeField] private bool slideButtons = true;

    [Min(1f)]
    [Tooltip("How much of a hidden button still shows at the screen edge (canvas units).")]
    [SerializeField] private float sliver = 14f;

    [Min(0f)]
    [Tooltip("The mouse counts as 'near' when it is this close (canvas units) to the button's spot, in any direction.")]
    [SerializeField] private float triggerPadding = 50f;

    [Min(0f)]
    [Tooltip("Buttons docked to the middle of the left / right edge (Bank, Toggles) slide out only when the mouse is this close (canvas units) to that screen edge. Once out, the button's own area keeps it out. Keep it small so windows near the edge (the event log) don't trigger it.")]
    [SerializeField] private float sideTriggerDepth = 30f;

    [Min(0.01f)]
    [Tooltip("Seconds a button takes to slide out or away.")]
    [SerializeField] private float slideSeconds = 0.18f;

    private static PixelHud instance;

    /// <summary>The screen frame, or null if the scene has none yet.</summary>
    public static PixelHud Instance => instance;

    /// <summary>The frame; found, or added to 'host', if the scene has none.</summary>
    public static PixelHud Ensure(GameObject host)
    {
        if (instance == null) instance = PixelFind.First<PixelHud>();
        if (instance == null) instance = host.AddComponent<PixelHud>();
        return instance;
    }

    /// <summary>The docked buttons shrink a little when the bar is too thin for them (a big UI scale makes the bar thinner in canvas units), so they never stick out past it.</summary>
    private float ButtonScale => barsEnabled ? Mathf.Clamp((BarHeight - 8f) / Mathf.Max(1f, buttonSize.y), 0.5f, 1f) : 1f;

    public Vector2 ButtonSize => buttonSize * ButtonScale;
    public float ButtonFontSize => buttonFontSize * ButtonScale;
    public float SideMargin => sideMargin;

    /// <summary>Thickness of the bar along the screen edge (0 when the bars are off).</summary>
    /// <summary>The bar's height in the units of the (scaled) UI canvases.</summary>
    public float BarHeight => barsEnabled ? barHeight / PixelDisplaySettings.UIScale : 0f;

    /// <summary>The bar's height in the units of the bars' own, unscaled canvas (what is really drawn).</summary>
    public float RawBarHeight => barsEnabled ? barHeight : 0f;

    /// <summary>
    /// How far from the screen edge a window opening from a docked button should start: below the button's band,
    /// plus the gap.
    /// </summary>
    public float WindowOffset => BandThickness + ShownInset;   // a window sits as far from the black margin as the docked button does (the same gap on both sides of the button)

    /// <summary>Thickness of the band along the screen edge that the docked buttons occupy (the bar, or the button plus margin).</summary>
    public float BandThickness => barsEnabled ? barHeight / PixelDisplaySettings.UIScale : ButtonSize.y + sideMargin;

    private GameObject canvasRoot;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("PixelHud: more than one in the scene; remove the extra one.", this);
            return;
        }
        instance = this;
        BuildBars();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private const string PrefBarGlow = "PixelClicker.Setting.BarGlow";
    private static int barGlowCache = -1;
    private readonly PixelBarEdgeFx[] glowParts = new PixelBarEdgeFx[2];   // per bar: the glow / bar style on its inner edge

    /// <summary>The player's switch for the neon bar glow (Settings > Display). On by default.</summary>
    public static bool BarGlow
    {
        get
        {
            if (barGlowCache < 0) barGlowCache = PixelPrefs.GetInt(PrefBarGlow, 1);
            return barGlowCache != 0;
        }
        set
        {
            barGlowCache = value ? 1 : 0;
            PixelPrefs.SetInt(PrefBarGlow, barGlowCache);
        }
    }

    private const string PrefGlowHue = "PixelClicker.Setting.GlowHue", PrefGlowCycle = "PixelClicker.Setting.GlowCycle";
    private static float glowHueCache = -1f;
    private static int glowCycleCache = -1;

    /// <summary>The player's chosen hue (0 - 1) of the neon bar glow. With Cycle on, the colour drifts on from it.</summary>
    public static float GlowHue
    {
        get
        {
            if (glowHueCache < 0f) glowHueCache = Mathf.Clamp01(PixelPrefs.GetFloat(PrefGlowHue, 0f));
            return glowHueCache;
        }
        set
        {
            glowHueCache = Mathf.Clamp01(value);
            PixelPrefs.SetFloat(PrefGlowHue, glowHueCache);
            if (glowColorCache != "") { glowColorCache = ""; glowColorParsed = null; PixelPrefs.SetString(PrefGlowColor, ""); }   // the slider takes over from a picked colour
        }
    }

    /// <summary>Does the glow colour slowly change by itself (on by default) or stay at the chosen hue?</summary>
    public static bool GlowCycle
    {
        get
        {
            if (glowCycleCache < 0) glowCycleCache = PixelPrefs.GetInt(PrefGlowCycle, 1);
            return glowCycleCache != 0;
        }
        set
        {
            glowCycleCache = value ? 1 : 0;
            PixelPrefs.SetInt(PrefGlowCycle, glowCycleCache);
        }
    }

    private const string PrefGlowColor = "PixelClicker.Setting.GlowColor";
    private static string glowColorCache;   // null = not read yet, "" = none
    private static Color? glowColorParsed;

    private static Color? ParseGlowColor(string hex) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : (Color?)null;

    /// <summary>An exact colour picked with the eyedropper (Settings > Display), used while the glow is NOT cycling. Null = none (the hue slider decides). Moving the hue slider clears it.</summary>
    public static Color? GlowCustomColor
    {
        get
        {
            if (glowColorCache == null) { glowColorCache = PixelPrefs.GetString(PrefGlowColor, ""); glowColorParsed = ParseGlowColor(glowColorCache); }
            return glowColorParsed;
        }
        set
        {
            glowColorCache = value.HasValue ? ColorUtility.ToHtmlStringRGB(value.Value) : "";
            glowColorParsed = ParseGlowColor(glowColorCache);
            PixelPrefs.SetString(PrefGlowColor, glowColorCache);
        }
    }

    /// <summary>Sets the glow to one exact colour: it stops cycling (cycling afterwards continues from this colour's hue).</summary>
    public static void SetGlowFromColor(Color c)
    {
        Color.RGBToHSV(c, out float h, out _, out _);
        GlowHue = h;                      // (clears any older custom colour)
        GlowCustomColor = new Color(c.r, c.g, c.b, 1f);
        GlowCycle = false;
        PixelPrefs.Save();
    }

    /// <summary>The glow colour of one edge: the picked colour while it is static, else the hue (drifting with time while cycling).</summary>
    public static Color GlowColourNow(float time, float cycleSpeed, float edgeOffset, float saturation)
    {
        Color? custom = GlowCustomColor;
        if (!GlowCycle && custom.HasValue) return custom.Value;
        return PixelBarGlow.EdgeColor(GlowHue, time, GlowCycle ? cycleSpeed : 0f, edgeOffset, saturation);
    }

    // ---- Bar colour: the black margins (and the main menu's bar) in a colour picked with the eyedropper (Settings > Display) ----

    private const string PrefBarColorOn = "PixelClicker.Setting.BarColorOn", PrefBarColor = "PixelClicker.Setting.BarColor";
    private static int barColorOnCache = -1;
    private static string barColorCache;
    private static Color barColorParsed = Color.black;

    /// <summary>Counts every change of the bar colour setting; the bars and the menu bar repaint when it moves.</summary>
    public static int BarColorVersion { get; private set; }

    private static void ReadBarColor()
    {
        if (barColorCache != null) return;
        barColorCache = PixelPrefs.GetString(PrefBarColor, "");
        barColorOnCache = PixelPrefs.GetInt(PrefBarColorOn, 0);
        barColorParsed = ParseGlowColor(barColorCache) ?? Color.black;
    }

    /// <summary>Use the picked colour for the black bars (game and main menu)?</summary>
    public static bool BarColorOn
    {
        get { ReadBarColor(); return barColorOnCache == 1; }
        set { ReadBarColor(); barColorOnCache = value ? 1 : 0; PixelPrefs.SetInt(PrefBarColorOn, barColorOnCache); BarColorVersion++; }
    }

    /// <summary>The picked bar colour (black until one is picked).</summary>
    public static Color BarColorPicked { get { ReadBarColor(); return barColorParsed; } }

    /// <summary>Picks one exact colour for the bars and switches the custom colour on.</summary>
    public static void SetBarColorFromColor(Color c)
    {
        ReadBarColor();
        barColorCache = ColorUtility.ToHtmlStringRGB(c);
        barColorParsed = new Color(c.r, c.g, c.b, 1f);
        PixelPrefs.SetString(PrefBarColor, barColorCache);
        BarColorOn = true;
        PixelPrefs.Save();
    }

    /// <summary>A bar's colour: the picked colour (keeping the bar's own opacity) while the custom colour is on, else the colour it came with.</summary>
    public static Color BarTint(Color baseColor)
    {
        if (!BarColorOn) return baseColor;
        Color p = BarColorPicked;
        return new Color(p.r, p.g, p.b, baseColor.a);
    }

    [Tooltip("The one colour every docked menu button shares (the hose and Dev Tools buttons keep their own).")]
    [SerializeField] private Color sharedButtonColor = new Color(0.2f, 0.3f, 0.45f, 1f);

    internal Color SharedButtonColor => sharedButtonColor.a < 0.01f ? new Color(0.2f, 0.3f, 0.45f, 1f) : sharedButtonColor;

    public static void ReloadGlowFromPrefs() { buttonModeCache = -1; barGlowCache = -1; glowHueCache = -1f; glowCycleCache = -1; glowColorCache = null; barColorCache = null; BarColorVersion++; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGlowStatics() { buttonModeCache = -1; barGlowCache = -1; glowHueCache = -1f; glowCycleCache = -1; glowColorCache = null; barColorCache = null; BarColorVersion = 0; }

    [Min(0f)]
    [Tooltip("Seconds the black bars (and the docked buttons) take to slide in when the game starts and out when quitting to the main menu. 0 = 0.9.")]
    [SerializeField] private float revealSeconds = 0f;

    private float reveal = -1f;
    private readonly RectTransform[] barRects = new RectTransform[2];

    /// <summary>0 = the bars and docked buttons are slid off screen (main menu), 1 = in place. Eased.</summary>
    public static float RevealEased => instance == null || instance.reveal < 0f ? 1f : Mathf.SmoothStep(0f, 1f, instance.reveal);

    private readonly List<Image> barImages = new List<Image>();
    private int appliedBarVersion = -1;

    private void Update()
    {
        if (appliedBarVersion != BarColorVersion)
        {
            appliedBarVersion = BarColorVersion;
            foreach (Image im in barImages) if (im != null) im.color = BarTint(barColor);
        }
        // The margins are away while the title screen shows and while the camera swings back to it; they slide in as the camera moves on Play.
        bool away = PixelTitleScreen.Showing || PixelTitleScreen.Reversing;
        if (reveal < 0f) reveal = away ? 0f : 1f;
        reveal = Mathf.MoveTowards(reveal, away ? 0f : 1f, Time.unscaledDeltaTime / (revealSeconds > 0.05f ? revealSeconds : 0.9f));
        float out01 = 1f - Mathf.SmoothStep(0f, 1f, reveal);
        for (int i = 0; i < 2; i++)
            if (barRects[i] != null) barRects[i].anchoredPosition = new Vector2(0f, (i == 0 ? 1f : -1f) * out01 * (barHeight + 16f));
        for (int i = 0; i < 2; i++)
        {
            PixelBarEdgeFx g = glowParts[i];
            if (g == null) continue;
            bool on = barGlowAllowed && BarGlow;
            if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
            if (!on) continue;
            float shimmer = 0.93f + 0.07f * Mathf.Sin(Time.unscaledTime * 1.3f);
            Color c = GlowColourNow(Time.unscaledTime, glowColorSpeed, GlowCustomColor.HasValue && !GlowCycle ? 0f : (i == 0 ? 0f : glowEdgeOffset), glowSaturation);
            g.Tick(c, Mathf.Clamp01(glowStrength) * shimmer);
        }
    }

    private void BuildBars()
    {
        if (!barsEnabled) return;
        canvasRoot = PixelUIKit.CreateCanvas("PixelHud Canvas", Mathf.Min(barsSortingOrder, -1), new Vector2(1920f, 1080f), true, false); // not scaled: the bars keep their size
        for (int i = 0; i < 2; i++)
        {
            bool top = i == 0;
            GameObject bar = new GameObject(top ? "Top Bar" : "Bottom Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(canvasRoot.transform, false);
            barImages.Add(bar.GetComponent<Image>());
            bar.GetComponent<Image>().color = BarTint(barColor); // raycast target on: clicks on a bar don't reach the cube behind it
            RectTransform r = bar.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, top ? 1f : 0f);
            r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 1f : 0f);
            r.sizeDelta = new Vector2(0f, barHeight);
            r.anchoredPosition = Vector2.zero;
            barRects[i] = r;

            // The glow sits on the bar's inner edge (the one facing the play area): the top bar's bottom edge, the bottom bar's top edge.
            glowParts[i] = PixelBarEdgeFx.Create(r, !top, glowSize, glowInnerReach);
        }
    }

    // ------------------------------------------------------------------
    // Docking
    // ------------------------------------------------------------------

    /// <summary>
    /// Makes a button one of the docked buttons: standard size, in the given corner (anchor x = 0 left / 1 right,
    /// anchor y = 0 bottom / 1 top), slid off screen until the mouse comes near. 'keepOut' returns true while the
    /// button's window is open, so it doesn't slide away then. 'sideOffsetY' moves a side-docked button up (positive) or
    /// down (negative) from the vertical middle, so two buttons can be docked one under the other.
    /// </summary>
    public void Dock(RectTransform button, Vector2 anchor, Func<bool> keepOut, float sideOffsetY = 0f, float widthScale = 1f, float cornerOffset = 0f)
    {
        button.anchorMin = button.anchorMax = button.pivot = anchor;
        button.sizeDelta = new Vector2(ButtonSize.x * widthScale, ButtonSize.y);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = ButtonFontSize;
            label.fontSizeMin = 12f;
            label.margin = new Vector4(12f, 0f, 12f, 0f);   // padding, so a long name (PIXEL GUIDE) shrinks to fit instead of touching the edges
            PixelUIKit.Caps(label); // every docked button (Inventory, Shop, Log, Crafting, Toggles, Bank, Hose...) reads in capitals
        }

        Canvas owner = button.GetComponentInParent<Canvas>();
        if (owner != null)
            PixelDebug.Info("PixelHud: docked '" + button.name + "' (canvas sorting order " + owner.sortingOrder + ", bars " +
                      Mathf.Min(barsSortingOrder, -1) + ").", this);

        // A button anchored at the vertical middle (anchor.y = 0.5) docks to the left / right edge instead of the top / bottom.
        PixelDockedButton dock = button.gameObject.GetComponent<PixelDockedButton>();
        if (dock == null) dock = button.gameObject.AddComponent<PixelDockedButton>();
        dock.Setup(this, button, anchor, keepOut, sideOffsetY, (ButtonSize.x - button.sizeDelta.x) * 0.5f, 0f, cornerOffset);
        PixelMenuFx.DecorateHud(button.GetComponent<Button>());   // the shine on hover
    }

    /// <summary>
    /// Docks one button of a side-by-side GROUP on the middle of a side edge (e.g. the Bank tab with the square Hose button beside it).
    /// 'leftOffset' = how far the button sits from the group's outer edge, 'groupWidth' = the group's total width; the whole group slides
    /// as one and the button nearest the screen centre is the part that stays visible as the sliver.
    /// </summary>
    public void DockInGroup(RectTransform button, Vector2 anchor, Func<bool> keepOut, float sideOffsetY, float width, float leftOffset, float groupWidth)
    {
        Dock(button, anchor, keepOut, sideOffsetY);
        button.sizeDelta = new Vector2(width, ButtonSize.y);
        if (!Mathf.Approximately(anchor.y, 0.5f)) button.pivot = new Vector2(0f, anchor.y);   // a group on the top / bottom edge is placed by its left edge
        PixelDockedButton dock = button.gameObject.GetComponent<PixelDockedButton>();
        dock.Setup(this, button, anchor, keepOut, sideOffsetY, leftOffset, groupWidth);
    }

    internal float Sliver => ButtonMode == 2 ? 0f : sliver;

    private const string PrefButtonMode = "PixelClicker.Setting.ButtonMode";
    private static int buttonModeCache = -1;

    /// <summary>How the docked buttons behave: 0 = peek (a sliver shows at the edge, the default), 1 = always showing, 2 = completely hidden until the mouse comes near.</summary>
    public static int ButtonMode
    {
        get
        {
            if (buttonModeCache < 0) buttonModeCache = Mathf.Clamp(PixelPrefs.GetInt(PrefButtonMode, 0), 0, 2);
            return buttonModeCache;
        }
        set
        {
            buttonModeCache = Mathf.Clamp(value, 0, 2);
            PixelPrefs.SetInt(PrefButtonMode, buttonModeCache);
        }
    }

    // Where the button sits when shown / hidden (its edge nearest the screen edge, measured inward from that edge).
    public float ShownInset => barsEnabled ? Mathf.Max(2f, (BarHeight - ButtonSize.y) * 0.5f) : sideMargin * 0.5f;   // exactly centred in the black margin
    internal float HiddenInset => -(ButtonSize.y - Sliver);

    // Same for a button docked to the middle of the left / right screen edge (it slides sideways).
    public float SideShownInset => barsEnabled ? ShownInset : sideMargin * 0.35f;   // the same gap as the buttons keep from the top / bottom margins, so windows and buttons line up with equal gaps all round
    internal float SideHiddenInset => -(ButtonSize.x - Sliver);
    internal bool SlideButtons => slideButtons && ButtonMode != 1;
    internal float SlideSeconds => slideSeconds;
    internal float TriggerPadding => triggerPadding;
    internal float SideTriggerDepth => sideTriggerDepth;
    internal float BandHeight => barsEnabled ? BarHeight : ButtonSize.y + sideMargin;
}

/// <summary>Slides one docked button out when the mouse is near and away when it isn't.</summary>
public class PixelDockedButton : MonoBehaviour
{
    private PixelHud hud;
    private RectTransform rect;
    private Vector2 anchor;
    private Func<bool> keepOut;
    private float slide; // 0 = hidden, 1 = shown
    private Canvas canvas;

    private float sideOffsetY;
    private float sideOffsetX; // a narrower side button is centred under a full-width one

    /// <summary>While true the button is snapped to its hidden position and stays there (even with the mouse near).</summary>
    public bool ForceHidden { get; set; }

    /// <summary>When it returns true the mouse never makes this button slide out (a neighbouring window is open over its strip).</summary>
    public Func<bool> blockPointer;

    /// <summary>How far out the button is (0 = hidden, 1 = fully shown).</summary>
    public float SlideAmount => slide;

    private float cornerOffset; // a corner button sits this far along its bar from the corner, towards the middle (two buttons per corner: the second one is offset by the first one's width + the gap)
    private float groupWidth; // > 0: this button is one of a side-by-side group that slides as a whole (see PixelHud.DockInGroup)

    public void Setup(PixelHud owner, RectTransform button, Vector2 corner, Func<bool> keepOutWhile, float offsetY = 0f, float offsetX = 0f, float groupTotalWidth = 0f, float cornerAlong = 0f)
    {
        cornerOffset = cornerAlong;
        sideOffsetY = offsetY;
        sideOffsetX = offsetX;
        groupWidth = groupTotalWidth;
        hud = owner;
        rect = button;
        anchor = corner;
        keepOut = keepOutWhile;
        slide = hud.SlideButtons ? 0f : 1f;
        Apply();
    }

    private Canvas sortCanvas;
    private UnityEngine.UI.Image image;
    private Color appliedColor = new Color(-1f, 0f, 0f, 0f);

    /// <summary>The button keeps whatever colour its owner gives it (the Dev Tools button's gold).</summary>
    public bool useOwnColor;

    private void ApplyColor()
    {
        if (useOwnColor) return;
        if (image == null) image = GetComponent<UnityEngine.UI.Image>();
        if (image == null) return;
        Color want = hud.SharedButtonColor;
        if (want != appliedColor) { image.color = want; appliedColor = want; }
    }

    /// <summary>With the buttons always showing, each one gets its own canvas below every window, so windows overlay the buttons instead of the other windows' buttons showing on top.</summary>
    private void UpdateSorting()
    {
        if (PixelHud.ButtonMode == 1)
        {
            if (sortCanvas == null)
            {
                sortCanvas = gameObject.GetComponent<Canvas>();
                if (sortCanvas == null) sortCanvas = gameObject.AddComponent<Canvas>();
                if (gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();   // a nested canvas needs its own raycaster for clicks
            }
            if (!sortCanvas.overrideSorting) sortCanvas.overrideSorting = true;   // (only on a change: this runs every frame for every button)
            if (sortCanvas.sortingOrder != hud.AlwaysOutOrder) sortCanvas.sortingOrder = hud.AlwaysOutOrder;
        }
        else if (sortCanvas != null && sortCanvas.overrideSorting) sortCanvas.overrideSorting = false;
    }

    private void Update()
    {
        if (hud == null) return;
        UpdateSorting();
        ApplyColor();

        if (ForceHidden) { slide = 0f; Apply(); return; }
        float target = !hud.SlideButtons || (keepOut != null && keepOut()) || PointerNear() ? 1f : 0f;
        slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime / hud.SlideSeconds);
        Apply();
    }

    private void Apply()
    {
        if (Mathf.Approximately(anchor.y, 0.5f))
        {
            // Docked to a side edge: slides sideways, centred vertically.
            float hiddenInset = groupWidth > 0f ? -(groupWidth - hud.Sliver) : hud.SideHiddenInset;
            float sideInset = Mathf.Lerp(hiddenInset, hud.SideShownInset, Mathf.SmoothStep(0f, 1f, slide));
            float pushSide = (1f - PixelHud.RevealEased) * 420f;   // slid away with the black margins
            SetPos(new Vector2(anchor.x > 0.5f ? -(sideInset + sideOffsetX) + pushSide : sideInset + sideOffsetX - pushSide, sideOffsetY));
            return;
        }

        float shown = hud.ShownInset, hidden = hud.HiddenInset;
        float inset = Mathf.Lerp(hidden, shown, Mathf.SmoothStep(0f, 1f, slide));
        bool top = anchor.y > 0.5f;
        // anchor.x is the button's place along the edge as a fraction (0 = left corner, 1 = right corner, buttons in between are spread evenly); the corner ones share their outer edge with the side buttons.
        float f = Mathf.Clamp01(anchor.x);
        float x = Mathf.Lerp(hud.SideShownInset, -hud.SideShownInset, f);
        if (groupWidth > 0f) x += -f * groupWidth + sideOffsetX;   // one of a group: the whole group is spread like one button, this member sits at its offset
        else x += f > 0.5f ? -cornerOffset : cornerOffset;
        float push = (1f - PixelHud.RevealEased) * (hud.BandHeight + hud.ButtonSize.y + 40f);
        SetPos(new Vector2(x, top ? -inset + push : inset - push));
    }

    // (a settled button kept writing the same position every frame, which makes the HUD canvas redo its layout: write only on a change)
    private void SetPos(Vector2 pos)
    {
        if (rect.anchoredPosition != pos) rect.anchoredPosition = pos;
    }

    /// <summary>Is the mouse over (or close to) the spot the button slides out to?</summary>
    public bool PointerNear()
    {
        if (blockPointer != null && blockPointer()) return false;
        if (canvas == null) canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        float scale = Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
        Vector2 p = PixelInput.PointerPosition();
        if (p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height) return false;

        float pad = hud.TriggerPadding;
        if (Mathf.Approximately(anchor.y, 0.5f))
        {
            float sw = groupWidth > 0f ? groupWidth : rect.sizeDelta.x + sideOffsetX, sh = hud.ButtonSize.y;
            float fromSide = anchor.x > 0.5f ? Screen.width / scale - p.x / scale : p.x / scale;
            // Narrow strip along the edge to open; once sliding out, the visible part of the button keeps it open.
            float depth = hud.SideTriggerDepth + slide * sw;
            return fromSide <= depth && Mathf.Abs(p.y / scale - (Screen.height / scale * 0.5f + sideOffsetY)) <= sh * 0.5f + pad;
        }

        float w = rect.sizeDelta.x;
        float f = Mathf.Clamp01(anchor.x), screenW = Screen.width / scale;
        float x0 = (groupWidth > 0f ? f * (screenW - groupWidth) + sideOffsetX : f * (screenW - w) + (f > 0.5f ? -cornerOffset : cornerOffset)) + Mathf.Lerp(hud.SideShownInset, -hud.SideShownInset, f);
        float x1 = x0 + w;
        float px = p.x / scale, py = p.y / scale;
        float fromEdge = anchor.y > 0.5f ? Screen.height / scale - py : py; // distance of the mouse from the button's screen edge

        return px >= x0 - pad && px <= x1 + pad && fromEdge <= hud.BandHeight + pad;
    }
}
