using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple HUD for Pixel Clicker. Shows how many pixels of each tier you have.
///
/// Two ways to use it:
///  A) MANUAL: create TextMeshPro texts in your own Canvas and drag them into "Tier Labels"
///     (element 0 = first tier, element 1 = second tier, ...).
///  B) AUTOMATIC: leave "Tier Labels" empty and keep "Auto Create Labels" on. The script builds
///     its own canvas, panel and one text per tier when the game starts.
///
/// It only reads PixelClicker.Tiers and refreshes every frame, so there is nothing to wire up
/// besides the PixelClicker reference (found automatically if left empty).
/// </summary>
public class PixelUI : MonoBehaviour
{
    public enum PanelCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ------------------------------------------------------------------
    // References
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker to display. Found automatically in the scene if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("MANUAL MODE: one TextMeshPro text per tier (element 0 = first tier, 1 = second...). Leave empty for automatic mode.")]
    [SerializeField] private TMP_Text[] tierLabels;

    [Tooltip("Font for auto-created texts. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------
    // Automatic mode
    // ------------------------------------------------------------------

    [Header("Automatic Mode")]
    [Tooltip("Build a canvas + one text per tier at startup when 'Tier Labels' is empty.")]
    [SerializeField] private bool autoCreateLabels = true;

    [Tooltip("Which screen corner the auto-created panel sits in.")]
    [SerializeField] private PanelCorner corner = PanelCorner.TopLeft;

    [Tooltip("Distance of the panel from the screen edge, in pixels.")]
    [SerializeField] private Vector2 margin = new Vector2(30f, 30f);

    [Tooltip("Width of the panel.")]
    [SerializeField] private float panelWidth = 520f;

    [Tooltip("Height of each text line.")]
    [SerializeField] private float lineHeight = 60f;

    [Tooltip("Padding between the panel edge and the text.")]
    [SerializeField] private float panelPadding = 16f;

    [Tooltip("Draw a background behind the text.")]
    [SerializeField] private bool showBackground = true;

    [Tooltip("Background colour (alpha = transparency).")]
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.75f);

    [Tooltip("Sorting order of the auto-created canvas.")]
    [SerializeField] private int sortingOrder = 100;

    [Tooltip("Reference resolution used by the auto-created canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    // ------------------------------------------------------------------
    // Text
    // ------------------------------------------------------------------

    [Header("Text")]
    [Tooltip("Text size for auto-created texts.")]
    [SerializeField] private float fontSize = 40f;

    [Tooltip("Text colour (when 'Color Text By Tier' is off).")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Colour each line with its tier's colour. Black pixels will be hard to read on a dark background, so use the outline below.")]
    [SerializeField] private bool colorTextByTier = false;

    [Tooltip("Outline colour (used for auto-created texts).")]
    [SerializeField] private Color outlineColor = Color.black;

    [Range(0f, 1f)]
    [Tooltip("Outline thickness for auto-created texts. 0 = none.")]
    [SerializeField] private float outlineWidth = 0f;

    [Tooltip("Line format. {0} = tier name, {1} = amount.")]
    [SerializeField] private string lineFormat = "{0}: {1}";

    [Tooltip("Compact numbers (1.2K, 3.4M). Off = full number.")]
    [SerializeField] private bool abbreviateNumbers = true;

    [Header("Locked Tiers")]
    [Tooltip("Show tiers that aren't unlocked yet.")]
    [SerializeField] private bool showLockedTiers = false;

    [Tooltip("Text shown as the amount of a locked tier (only when 'Show Locked Tiers' is on).")]
    [SerializeField] private string lockedText = "???";

    [Tooltip("Text colour for locked tiers.")]
    [SerializeField] private Color lockedColor = new Color(1f, 1f, 1f, 0.35f);

    // ------------------------------------------------------------------
    // Gain popups
    // ------------------------------------------------------------------

    [Header("Gain Popups (+N at the cursor)")]
    [Tooltip("Show a floating '+N' when you gain currency.")]
    [SerializeField] private bool showGainPopups = true;

    [Tooltip("Popup keeps following the mouse cursor while it rises. Off = it stays where you clicked.")]
    [SerializeField] private bool followCursor = true;

    [Tooltip("Popup text. {0} = amount gained.")]
    [SerializeField] private string popupFormat = "+{0}";

    [Tooltip("Popup text size.")]
    [SerializeField] private float popupFontSize = 56f;

    [Tooltip("Use the tier's colour for the popup. Off = use 'Popup Color'.")]
    [SerializeField] private bool popupUsesTierColor = true;

    [Tooltip("Popup colour when 'Popup Uses Tier Color' is off.")]
    [SerializeField] private Color popupColor = Color.white;

    [Tooltip("Outline colour so the popup reads on any background.")]
    [SerializeField] private Color popupOutlineColor = Color.black;

    [Range(0f, 1f)]
    [Tooltip("Outline thickness. 0 = none.")]
    [SerializeField] private float popupOutlineWidth = 0.25f;

    [Tooltip("Seconds a popup lives.")]
    [SerializeField] private float popupDuration = 0.9f;

    [Tooltip("How far (canvas units) the popup rises over its life.")]
    [SerializeField] private float popupRise = 120f;

    [Tooltip("Offset from the cursor where the popup starts (canvas units).")]
    [SerializeField] private Vector2 popupStartOffset = new Vector2(0f, 50f);

    [Tooltip("Random horizontal offset (+/-) so rapid clicks don't stack exactly.")]
    [SerializeField] private float popupRandomX = 30f;

    [Tooltip("Popup opacity over its life (time 0..1, alpha 0..1).")]
    [SerializeField] private AnimationCurve popupAlpha = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

    [Tooltip("Popup scale over its life (time 0..1, scale multiplier).")]
    [SerializeField] private AnimationCurve popupScale = new AnimationCurve(
        new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f));

    [Tooltip("Sorting order of the popup canvas (keep above the panel).")]
    [SerializeField] private int popupSortingOrder = 200;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private GameObject autoRoot;
    private bool built;
    private RectTransform popupCanvasRect;
    private double[] lastTotals;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (clicker == null)
        {
#if UNITY_2023_1_OR_NEWER
            clicker = FindFirstObjectByType<PixelClicker>();
#else
            clicker = FindObjectOfType<PixelClicker>();
#endif
        }

        if (clicker == null)
        {
            Debug.LogError("PixelUI: no PixelClicker found in the scene. Add PixelClicker to your cube first, " +
                           "or drag it into the 'Clicker' field.", this);
            enabled = false;
            return;
        }

        bool hasManualLabels = tierLabels != null && tierLabels.Length > 0 && tierLabels[0] != null;
        if (!hasManualLabels)
        {
            if (!autoCreateLabels)
            {
                Debug.LogError("PixelUI: 'Tier Labels' is empty and 'Auto Create Labels' is off, so there is nothing to show.", this);
                enabled = false;
                return;
            }
            BuildAutomaticUI();
        }

        BuildPopupCanvas();
        lastTotals = new double[clicker.Tiers.Length];
        for (int i = 0; i < lastTotals.Length; i++) lastTotals[i] = clicker.Tiers[i].totalCollected;

        built = true;
    }

    private void OnDestroy()
    {
        if (autoRoot != null) Destroy(autoRoot);
        if (popupRoot != null) Destroy(popupRoot);
    }

    private void Update()
    {
        if (!built) return;
        Refresh();
        DetectGains();
    }

    // ------------------------------------------------------------------
    // Popups
    // ------------------------------------------------------------------

    private void BuildPopupCanvas()
    {
        // Separate overlay canvas so popups work in both automatic and manual mode.
        GameObject go = new GameObject("PixelUI Popups");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = popupSortingOrder;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        popupCanvasRect = go.GetComponent<RectTransform>();
        popupRoot = go;
    }

    private GameObject popupRoot;

    /// <summary>Compares lifetime totals each frame; any increase spawns a popup.</summary>
    private void DetectGains()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        if (lastTotals.Length != tiers.Length) lastTotals = new double[tiers.Length];

        for (int i = 0; i < tiers.Length; i++)
        {
            double gained = tiers[i].totalCollected - lastTotals[i];
            lastTotals[i] = tiers[i].totalCollected;

            if (gained > 0d && showGainPopups) SpawnPopup(tiers[i], gained);
        }
    }

    private Vector2 CursorScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    /// <summary>Cursor position in the popup canvas's local space.</summary>
    private Vector2 CursorLocal()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRect, CursorScreenPosition(), null, out Vector2 local);
        return local;
    }

    private void SpawnPopup(PixelClicker.PixelTier tier, double amount)
    {
        Color color = popupUsesTierColor ? tier.color : popupColor;
        string text = string.Format(popupFormat, FormatAmount(amount));
        Vector2 jitter = new Vector2(UnityEngine.Random.Range(-popupRandomX, popupRandomX), 0f);
        StartCoroutine(PopupRoutine(text, color, CursorLocal(), jitter));
    }

    private IEnumerator PopupRoutine(string text, Color color, Vector2 startCursor, Vector2 jitter)
    {
        GameObject go = new GameObject("GainPopup", typeof(RectTransform));
        go.transform.SetParent(popupCanvasRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 150f); // wide enough that the text never needs to wrap

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = popupFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        if (popupOutlineWidth > 0f)
        {
            tmp.outlineColor = popupOutlineColor;
            tmp.outlineWidth = popupOutlineWidth;
        }

        float t = 0f;
        while (t < popupDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = popupDuration > 0f ? Mathf.Clamp01(t / popupDuration) : 1f;

            Vector2 anchor = followCursor ? CursorLocal() : startCursor;
            rt.anchoredPosition = anchor + popupStartOffset + jitter + Vector2.up * (popupRise * k);
            rt.localScale = Vector3.one * popupScale.Evaluate(k);

            Color c = color;
            c.a = color.a * popupAlpha.Evaluate(k);
            tmp.color = c;

            yield return null;
        }

        Destroy(go);
    }

    // ------------------------------------------------------------------
    // Automatic UI
    // ------------------------------------------------------------------

    private void BuildAutomaticUI()
    {
        if (TMP_Settings.instance == null && font == null)
        {
            Debug.LogError("PixelUI: TextMeshPro Essential Resources are missing. " +
                           "Use Window > TextMeshPro > Import TMP Essential Resources, then press Play again.", this);
        }

        int count = clicker.Tiers.Length;

        // Canvas
        autoRoot = new GameObject("PixelUI Canvas");
        Canvas canvas = autoRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = autoRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        // Panel (fixed size, anchored to the chosen corner)
        float height = panelPadding * 2f + lineHeight * count;
        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(autoRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();

        Vector2 anchor = new Vector2(
            corner == PanelCorner.TopRight || corner == PanelCorner.BottomRight ? 1f : 0f,
            corner == PanelCorner.TopLeft || corner == PanelCorner.TopRight ? 1f : 0f);
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = anchor;
        panelRect.sizeDelta = new Vector2(panelWidth, height);
        panelRect.anchoredPosition = new Vector2(
            anchor.x > 0.5f ? -margin.x : margin.x,
            anchor.y > 0.5f ? -margin.y : margin.y);

        if (showBackground)
        {
            Image bg = panel.AddComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;
        }

        // One text per tier, stacked from the top of the panel.
        tierLabels = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Tier " + i, typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-panelPadding * 2f, lineHeight);
            rt.anchoredPosition = new Vector2(0f, -panelPadding - lineHeight * i);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = anchor.x > 0.5f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            if (outlineWidth > 0f)
            {
                tmp.outlineColor = outlineColor;
                tmp.outlineWidth = outlineWidth;
            }

            tierLabels[i] = tmp;
        }

        Debug.Log("PixelUI: created " + count + " text lines.", this);
    }

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    /// <summary>Writes the current amounts into the texts. Runs every frame.</summary>
    public void Refresh()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;

        for (int i = 0; i < tierLabels.Length && i < tiers.Length; i++)
        {
            TMP_Text label = tierLabels[i];
            if (label == null) continue;

            PixelClicker.PixelTier tier = tiers[i];
            bool visible = tier.unlocked || showLockedTiers;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;

            string amount = tier.unlocked ? FormatAmount(tier.count) : lockedText;
            label.text = string.Format(lineFormat, tier.displayName, amount);

            if (!tier.unlocked) label.color = lockedColor;
            else label.color = colorTextByTier ? tier.color : textColor;
        }
    }

    private string FormatAmount(double value)
    {
        if (!abbreviateNumbers || value < 1000d) return Math.Floor(value).ToString("0");

        string[] suffix = { "", "K", "M", "B", "T" };
        int s = 0;
        while (value >= 1000d && s < suffix.Length - 1) { value /= 1000d; s++; }
        return value.ToString("0.##") + suffix[s];
    }
}
