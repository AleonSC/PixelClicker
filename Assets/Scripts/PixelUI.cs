using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple self-building HUD for Pixel Clicker.
///
/// - A panel listing every unlocked currency with a colour swatch and its amount.
/// - A little "punch" animation on a row when its amount changes.
/// - Floating "+N" popups above the pixel when you gain currency.
///
/// Add this to any GameObject (e.g. the same cube as PixelClicker). It creates its own
/// Canvas and TextMeshPro objects at runtime, so no manual UI setup is required.
/// </summary>
public class PixelUI : MonoBehaviour
{
    public enum PanelCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker to display. Auto-found in the scene if empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Existing Canvas to build the UI inside. Leave empty to create a new Screen Space - Overlay canvas.")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("Font for all text. Leave empty to use the TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Canvas (only used if a canvas is created)")]
    [Tooltip("Reference resolution the UI scales from.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Range(0f, 1f)]
    [Tooltip("0 = match width, 1 = match height when the screen aspect differs from the reference.")]
    [SerializeField] private float matchWidthOrHeight = 0.5f;

    [Tooltip("Sorting order of the created canvas.")]
    [SerializeField] private int sortingOrder = 10;

    [Header("Panel")]
    [Tooltip("Which screen corner the panel sits in.")]
    [SerializeField] private PanelCorner corner = PanelCorner.TopLeft;

    [Tooltip("Distance from the screen edges.")]
    [SerializeField] private Vector2 margin = new Vector2(30f, 30f);

    [Tooltip("Inner spacing between the panel edge and its rows. X = left, Y = right, Z = top, W = bottom.")]
    [SerializeField] private Vector4 padding = new Vector4(20f, 20f, 14f, 14f);

    [Tooltip("Vertical gap between rows.")]
    [SerializeField] private float rowSpacing = 8f;

    [Tooltip("Panel background colour (alpha = transparency).")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.1f, 0.75f);

    [Tooltip("Show the panel background at all.")]
    [SerializeField] private bool showPanelBackground = true;

    [Header("Rows")]
    [Tooltip("Text size for each currency line.")]
    [SerializeField] private float fontSize = 40f;

    [Tooltip("Text colour for the amounts. Swatches already show each tier's colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Text format. {0} = tier name, {1} = amount.")]
    [SerializeField] private string rowFormat = "{0}: {1}";

    [Tooltip("Size of the colour swatch next to each line. Set 0 to hide swatches.")]
    [SerializeField] private float swatchSize = 36f;

    [Tooltip("Gap between the swatch and the text.")]
    [SerializeField] private float swatchSpacing = 12f;

    [Tooltip("Colour of the border drawn around a swatch (helps white/black swatches stay visible).")]
    [SerializeField] private Color swatchBorderColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Thickness of the swatch border.")]
    [SerializeField] private float swatchBorderSize = 3f;

    [Tooltip("Also show tiers that aren't unlocked yet (as locked text).")]
    [SerializeField] private bool showLockedTiers = false;

    [Tooltip("Text shown for locked tiers when 'Show Locked Tiers' is on.")]
    [SerializeField] private string lockedText = "???";

    [Header("Change Animation")]
    [Tooltip("Scale-up of a row when its amount changes (0.2 = +20%).")]
    [SerializeField] private float punchScale = 0.2f;

    [Tooltip("Seconds the punch animation takes.")]
    [SerializeField] private float punchDuration = 0.2f;

    [Header("Gain Popups (+N)")]
    [Tooltip("Show floating '+N' text above the pixel when currency is gained.")]
    [SerializeField] private bool showGainPopups = true;

    [Tooltip("Popup text. {0} = amount gained.")]
    [SerializeField] private string popupFormat = "+{0}";

    [Tooltip("Popup text size.")]
    [SerializeField] private float popupFontSize = 56f;

    [Tooltip("Use the tier's colour for the popup (with an outline so it's visible on any background).")]
    [SerializeField] private bool popupUsesTierColor = true;

    [Tooltip("Popup colour when 'Popup Uses Tier Color' is off.")]
    [SerializeField] private Color popupColor = Color.white;

    [Tooltip("Outline colour for popups.")]
    [SerializeField] private Color popupOutlineColor = Color.black;

    [Range(0f, 1f)]
    [Tooltip("Outline thickness for popups.")]
    [SerializeField] private float popupOutlineWidth = 0.25f;

    [Tooltip("Seconds a popup lives.")]
    [SerializeField] private float popupDuration = 0.9f;

    [Tooltip("How far (canvas units) the popup rises.")]
    [SerializeField] private float popupRise = 140f;

    [Tooltip("Offset from the pixel's screen position where popups start.")]
    [SerializeField] private Vector2 popupStartOffset = new Vector2(0f, 90f);

    [Tooltip("Random horizontal offset (+/-) so rapid clicks don't stack exactly.")]
    [SerializeField] private float popupRandomX = 40f;

    [Tooltip("Popup opacity over its life (0..1 time, 0..1 alpha).")]
    [SerializeField] private AnimationCurve popupAlpha = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));

    [Tooltip("Popup scale over its life (0..1 time, scale multiplier).")]
    [SerializeField] private AnimationCurve popupScale = new AnimationCurve(
        new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1.2f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f));

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private class Row
    {
        public GameObject root;
        public TMP_Text label;
        public Image swatch;
        public float punchTimer;
        public double lastCount;
    }

    private readonly List<Row> rows = new List<Row>();
    private RectTransform canvasRect;
    private RectTransform panelRect;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (clicker == null) clicker = FindFirstObjectByType<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogWarning("PixelUI: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        BuildCanvas();
        BuildPanel();
    }

    private void OnEnable()
    {
        if (clicker == null) return;
        clicker.CurrencyChanged += Refresh;
        clicker.CurrencyGained += OnCurrencyGained;
    }

    private void OnDisable()
    {
        if (clicker == null) return;
        clicker.CurrencyChanged -= Refresh;
        clicker.CurrencyGained -= OnCurrencyGained;
    }

    private void Start()
    {
        // PixelClicker unlocks start tiers in its Awake, so by now the data is ready.
        Refresh();
    }

    private void Update()
    {
        // Row punch animation.
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.punchTimer <= 0f) continue;

            row.punchTimer -= Time.unscaledDeltaTime;
            float k = punchDuration > 0f ? 1f - Mathf.Clamp01(row.punchTimer / punchDuration) : 1f;
            row.root.transform.localScale = Vector3.one * (1f + punchScale * Mathf.Sin(k * Mathf.PI));
            if (row.punchTimer <= 0f) row.root.transform.localScale = Vector3.one;
        }
    }

    // ------------------------------------------------------------------
    // Building the UI
    // ------------------------------------------------------------------

    private void BuildCanvas()
    {
        if (targetCanvas == null)
        {
            GameObject go = new GameObject("PixelUI Canvas");
            targetCanvas = go.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = matchWidthOrHeight;
        }

        canvasRect = targetCanvas.GetComponent<RectTransform>();
    }

    private void BuildPanel()
    {
        GameObject panel = new GameObject("Currency Panel", typeof(RectTransform));
        panel.transform.SetParent(targetCanvas.transform, false);
        panelRect = panel.GetComponent<RectTransform>();

        // Anchor + pivot in the chosen corner.
        Vector2 anchor = new Vector2(
            corner == PanelCorner.TopRight || corner == PanelCorner.BottomRight ? 1f : 0f,
            corner == PanelCorner.TopLeft || corner == PanelCorner.TopRight ? 1f : 0f);
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = anchor;
        panelRect.anchoredPosition = new Vector2(
            anchor.x > 0.5f ? -margin.x : margin.x,
            anchor.y > 0.5f ? -margin.y : margin.y);

        if (showPanelBackground)
        {
            Image bg = panel.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;
        }

        VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(
            Mathf.RoundToInt(padding.x), Mathf.RoundToInt(padding.y),
            Mathf.RoundToInt(padding.z), Mathf.RoundToInt(padding.w));
        vlg.spacing = rowSpacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = anchor.x > 0.5f ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

        ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < clicker.Tiers.Length; i++)
            rows.Add(BuildRow(i));
    }

    private Row BuildRow(int tierIndex)
    {
        Row row = new Row();

        row.root = new GameObject("Row " + tierIndex, typeof(RectTransform));
        row.root.transform.SetParent(panelRect, false);

        HorizontalLayoutGroup hlg = row.root.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = swatchSpacing;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        if (swatchSize > 0f)
        {
            // Border (outer) + fill (inner) so any colour reads against the panel.
            GameObject border = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            border.transform.SetParent(row.root.transform, false);
            Image borderImg = border.GetComponent<Image>();
            borderImg.color = swatchBorderColor;
            borderImg.raycastTarget = false;
            LayoutElement le = border.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = swatchSize;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(border.transform, false);
            RectTransform fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(swatchBorderSize, swatchBorderSize);
            fr.offsetMax = new Vector2(-swatchBorderSize, -swatchBorderSize);
            row.swatch = fill.GetComponent<Image>();
            row.swatch.raycastTarget = false;
        }

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(row.root.transform, false);
        row.label = textGo.AddComponent<TextMeshProUGUI>();
        row.label.fontSize = fontSize;
        row.label.color = textColor;
        row.label.alignment = TextAlignmentOptions.MidlineLeft;
        row.label.raycastTarget = false;
        if (font != null) row.label.font = font;

        row.lastCount = clicker.Tiers[tierIndex].count;
        return row;
    }

    // ------------------------------------------------------------------
    // Updating
    // ------------------------------------------------------------------

    /// <summary>Updates all rows from the current PixelClicker data.</summary>
    public void Refresh()
    {
        if (clicker == null) return;
        PixelClicker.PixelTier[] tiers = clicker.Tiers;

        for (int i = 0; i < rows.Count && i < tiers.Length; i++)
        {
            Row row = rows[i];
            PixelClicker.PixelTier tier = tiers[i];

            bool visible = tier.unlocked || showLockedTiers;
            row.root.SetActive(visible);
            if (!visible) continue;

            row.label.text = tier.unlocked
                ? string.Format(rowFormat, tier.displayName, PixelClicker.FormatNumber(tier.count))
                : string.Format(rowFormat, tier.displayName, lockedText);

            if (row.swatch != null) row.swatch.color = tier.unlocked ? tier.color : new Color(0.3f, 0.3f, 0.3f, 1f);

            // Punch when the amount changed (but not on the very first refresh).
            if (tier.count != row.lastCount && punchDuration > 0f && punchScale > 0f)
                row.punchTimer = punchDuration;
            row.lastCount = tier.count;
        }

        if (panelRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    // ------------------------------------------------------------------
    // Gain popups
    // ------------------------------------------------------------------

    private void OnCurrencyGained(int tierIndex, double amount)
    {
        if (!showGainPopups || canvasRect == null) return;

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null || clicker.PixelTransform == null) return;

        Vector3 screen = cam.WorldToScreenPoint(clicker.PixelTransform.position);
        if (screen.z < 0f) return; // behind the camera

        // Overlay canvases take screen coordinates directly (null camera).
        Camera uiCam = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local)) return;

        Color color = popupUsesTierColor ? clicker.Tiers[tierIndex].color : popupColor;
        string text = string.Format(popupFormat, PixelClicker.FormatNumber(amount));
        Vector2 start = local + popupStartOffset + new Vector2(Random.Range(-popupRandomX, popupRandomX), 0f);

        StartCoroutine(PopupRoutine(text, color, start));
    }

    private IEnumerator PopupRoutine(string text, Color color, Vector2 start)
    {
        GameObject go = new GameObject("GainPopup", typeof(RectTransform));
        go.transform.SetParent(canvasRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = start;
        rt.sizeDelta = new Vector2(600f, 150f); // wide enough that the text never needs to wrap

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = popupFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineColor = popupOutlineColor;
        tmp.outlineWidth = popupOutlineWidth;
        if (font != null) tmp.font = font;

        float t = 0f;
        while (t < popupDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = popupDuration > 0f ? Mathf.Clamp01(t / popupDuration) : 1f;

            rt.anchoredPosition = start + Vector2.up * (popupRise * k);
            rt.localScale = Vector3.one * popupScale.Evaluate(k);

            Color c = color;
            c.a = color.a * popupAlpha.Evaluate(k);
            tmp.color = c;

            yield return null;
        }

        Destroy(go);
    }
}
