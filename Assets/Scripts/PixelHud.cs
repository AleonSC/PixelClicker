using System;
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

    [Tooltip("How far the docked buttons sit towards the screen edge instead of centred in the bar, as a fraction of the free space (keeps them clear of the bar glow). 0 = the default 0.55; 1 = right against the screen edge.")]
    [SerializeField] private float buttonEdgeBias = 0f;

    [Header("Buttons (all the same size)")]
    [Tooltip("Size of every docked button (canvas units). Keep the height a little less than the bar height.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(230f, 64f);

    [Tooltip("Text size on the docked buttons (long names shrink to fit).")]
    [SerializeField] private float buttonFontSize = 30f;

    [Tooltip("Distance of a button from the left or right edge of the screen.")]
    [SerializeField] private float sideMargin = 24f;

    [Tooltip("Gap between a button and the window that opens from it.")]
    [SerializeField] private float windowGap = 12f;

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

    public Vector2 ButtonSize => buttonSize;
    public float ButtonFontSize => buttonFontSize;
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
    public float WindowOffset => BandThickness + windowGap;

    /// <summary>Thickness of the band along the screen edge that the docked buttons occupy (the bar, or the button plus margin).</summary>
    public float BandThickness => barsEnabled ? barHeight / PixelDisplaySettings.UIScale : buttonSize.y + sideMargin;

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
    private readonly UnityEngine.UI.Image[][] glowParts = new UnityEngine.UI.Image[2][];   // per bar: outer, inner, line

    /// <summary>The player's switch for the neon bar glow (Settings > Display). On by default.</summary>
    public static bool BarGlow
    {
        get
        {
            if (barGlowCache < 0) barGlowCache = PlayerPrefs.GetInt(PrefBarGlow, 1);
            return barGlowCache != 0;
        }
        set
        {
            barGlowCache = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefBarGlow, barGlowCache);
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
            if (glowHueCache < 0f) glowHueCache = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefGlowHue, 0f));
            return glowHueCache;
        }
        set
        {
            glowHueCache = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefGlowHue, glowHueCache);
        }
    }

    /// <summary>Does the glow colour slowly change by itself (on by default) or stay at the chosen hue?</summary>
    public static bool GlowCycle
    {
        get
        {
            if (glowCycleCache < 0) glowCycleCache = PlayerPrefs.GetInt(PrefGlowCycle, 1);
            return glowCycleCache != 0;
        }
        set
        {
            glowCycleCache = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefGlowCycle, glowCycleCache);
        }
    }

    public static void ReloadGlowFromPrefs() { barGlowCache = -1; glowHueCache = -1f; glowCycleCache = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGlowStatics() { barGlowCache = -1; glowHueCache = -1f; glowCycleCache = -1; }

    private void Update()
    {
        for (int i = 0; i < 2; i++)
        {
            UnityEngine.UI.Image[] g = glowParts[i];
            if (g == null) continue;
            bool on = barGlowAllowed && BarGlow;
            if (g[0].gameObject.activeSelf != on) { g[0].gameObject.SetActive(on); g[1].gameObject.SetActive(on); g[2].gameObject.SetActive(on); }
            if (!on) continue;
            float shimmer = 0.93f + 0.07f * Mathf.Sin(Time.unscaledTime * 1.3f);
            Color c = PixelBarGlow.EdgeColor(GlowHue, Time.unscaledTime, GlowCycle ? glowColorSpeed : 0f, i == 0 ? 0f : glowEdgeOffset, glowSaturation);
            PixelBarGlow.Colorize(g[0], g[1], g[2], c, Mathf.Clamp01(glowStrength) * shimmer);
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
            bar.GetComponent<Image>().color = barColor; // raycast target on: clicks on a bar don't reach the cube behind it
            RectTransform r = bar.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, top ? 1f : 0f);
            r.anchorMax = new Vector2(1f, top ? 1f : 0f);
            r.pivot = new Vector2(0.5f, top ? 1f : 0f);
            r.sizeDelta = new Vector2(0f, barHeight);
            r.anchoredPosition = Vector2.zero;

            // The glow sits on the bar's inner edge (the one facing the play area): the top bar's bottom edge, the bottom bar's top edge.
            glowParts[i] = new[]
            {
                PixelBarGlow.AddStrip(r, "Glow", !top, true, glowSize),
                PixelBarGlow.AddStrip(r, "Glow Inner", !top, false, glowInnerReach),
                PixelBarGlow.AddLine(r, "Glow Line", !top),
            };
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
    public void Dock(RectTransform button, Vector2 anchor, Func<bool> keepOut, float sideOffsetY = 0f, float widthScale = 1f)
    {
        button.anchorMin = button.anchorMax = button.pivot = anchor;
        button.sizeDelta = new Vector2(buttonSize.x * widthScale, buttonSize.y);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.enableAutoSizing = true;
            label.fontSizeMax = buttonFontSize;
            label.fontSizeMin = 12f;
            PixelUIKit.Caps(label); // every docked button (Inventory, Shop, Log, Crafting, Toggles, Bank, Hose...) reads in capitals
        }

        Canvas owner = button.GetComponentInParent<Canvas>();
        if (owner != null)
            PixelDebug.Info("PixelHud: docked '" + button.name + "' (canvas sorting order " + owner.sortingOrder + ", bars " +
                      Mathf.Min(barsSortingOrder, -1) + ").", this);

        // A button anchored at the vertical middle (anchor.y = 0.5) docks to the left / right edge instead of the top / bottom.
        PixelDockedButton dock = button.gameObject.GetComponent<PixelDockedButton>();
        if (dock == null) dock = button.gameObject.AddComponent<PixelDockedButton>();
        dock.Setup(this, button, anchor, keepOut, sideOffsetY, (buttonSize.x - button.sizeDelta.x) * 0.5f);
    }

    // Where the button sits when shown / hidden (its edge nearest the screen edge, measured inward from that edge).
    internal float ShownInset => barsEnabled ? Mathf.Max(2f, (BarHeight - buttonSize.y) * 0.5f * (1f - (buttonEdgeBias > 0f ? buttonEdgeBias : 0.55f))) : sideMargin * 0.5f;
    internal float HiddenInset => -(buttonSize.y - sliver);

    // Same for a button docked to the middle of the left / right screen edge (it slides sideways).
    internal float SideShownInset => sideMargin * 0.35f;
    internal float SideHiddenInset => -(buttonSize.x - sliver);
    internal bool SlideButtons => slideButtons;
    internal float SlideSeconds => slideSeconds;
    internal float TriggerPadding => triggerPadding;
    internal float SideTriggerDepth => sideTriggerDepth;
    internal float BandHeight => barsEnabled ? BarHeight : buttonSize.y + sideMargin;
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

    /// <summary>How far out the button is (0 = hidden, 1 = fully shown).</summary>
    public float SlideAmount => slide;

    public void Setup(PixelHud owner, RectTransform button, Vector2 corner, Func<bool> keepOutWhile, float offsetY = 0f, float offsetX = 0f)
    {
        sideOffsetY = offsetY;
        sideOffsetX = offsetX;
        hud = owner;
        rect = button;
        anchor = corner;
        keepOut = keepOutWhile;
        slide = hud.SlideButtons ? 0f : 1f;
        Apply();
    }

    private void Update()
    {
        if (hud == null) return;

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
            float sideInset = Mathf.Lerp(hud.SideHiddenInset, hud.SideShownInset, Mathf.SmoothStep(0f, 1f, slide));
            rect.anchoredPosition = new Vector2(anchor.x > 0.5f ? -(sideInset + sideOffsetX) : sideInset + sideOffsetX, sideOffsetY);
            return;
        }

        float shown = hud.ShownInset, hidden = hud.HiddenInset;
        float inset = Mathf.Lerp(hidden, shown, Mathf.SmoothStep(0f, 1f, slide));
        bool top = anchor.y > 0.5f;
        float x = Mathf.Approximately(anchor.x, 0.5f) ? 0f : anchor.x > 0.5f ? -hud.SideMargin : hud.SideMargin; // 0.5 = centred
        rect.anchoredPosition = new Vector2(x, top ? -inset : inset);
    }

    /// <summary>Is the mouse over (or close to) the spot the button slides out to?</summary>
    public bool PointerNear()
    {
        if (canvas == null) canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        float scale = Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor);
        Vector2 p = PixelInput.PointerPosition();
        if (p.x < 0f || p.y < 0f || p.x > Screen.width || p.y > Screen.height) return false;

        float pad = hud.TriggerPadding;
        if (Mathf.Approximately(anchor.y, 0.5f))
        {
            float sw = rect.sizeDelta.x + sideOffsetX, sh = hud.ButtonSize.y;
            float fromSide = anchor.x > 0.5f ? Screen.width / scale - p.x / scale : p.x / scale;
            // Narrow strip along the edge to open; once sliding out, the visible part of the button keeps it open.
            float depth = hud.SideTriggerDepth + slide * sw;
            return fromSide <= depth && Mathf.Abs(p.y / scale - (Screen.height / scale * 0.5f + sideOffsetY)) <= sh * 0.5f + pad;
        }

        float w = hud.ButtonSize.x, margin = hud.SideMargin;
        float x0 = Mathf.Approximately(anchor.x, 0.5f) ? (Screen.width / scale - w) * 0.5f
                 : anchor.x > 0.5f ? Screen.width / scale - margin - w : margin;
        float x1 = x0 + w;
        float px = p.x / scale, py = p.y / scale;
        float fromEdge = anchor.y > 0.5f ? Screen.height / scale - py : py; // distance of the mouse from the button's screen edge

        return px >= x0 - pad && px <= x1 + pad && fromEdge <= hud.BandHeight + pad;
    }
}
