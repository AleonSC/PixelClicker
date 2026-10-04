using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Log" button for Pixel Clicker.
///
/// A button sits in a screen corner (bottom-left by default). Clicking it opens a panel that lists
/// every UNLOCKED pixel type with the total collected of each (lifetime total, spending doesn't
/// reduce it). Locked types stay hidden until they're unlocked.
///
/// Add this to any GameObject (e.g. the cube). The UI builds itself at runtime.
/// </summary>
public class PixelLog : MonoBehaviour
{
    public enum ButtonCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ------------------------------------------------------------------
    // References
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker to read from. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------
    // Button
    // ------------------------------------------------------------------

    [Header("Log Button")]
    [Tooltip("Corner the Log button sits in.")]
    [SerializeField] private ButtonCorner buttonCorner = ButtonCorner.BottomLeft;

    [Tooltip("Distance of the button from the screen edge (canvas units).")]
    [SerializeField] private Vector2 buttonMargin = new Vector2(30f, 30f);

    [Tooltip("Size of the button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(220f, 80f);

    [Tooltip("Text on the button.")]
    [SerializeField] private string buttonText = "Log";

    [Tooltip("Button text size.")]
    [SerializeField] private float buttonFontSize = 40f;

    [Tooltip("Button background colour.")]
    [SerializeField] private Color buttonColor = new Color(0.35f, 0.35f, 0.42f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    [Header("Log Panel")]
    [Tooltip("Title shown at the top of the panel.")]
    [SerializeField] private string panelTitle = "Pixel Log";

    [Tooltip("Panel width (canvas units).")]
    [SerializeField] private float panelWidth = 620f;

    [Tooltip("Height of each row.")]
    [SerializeField] private float rowHeight = 70f;

    [Tooltip("Height of the title bar.")]
    [SerializeField] private float headerHeight = 90f;

    [Tooltip("Inner padding of the panel.")]
    [SerializeField] private float panelPadding = 20f;

    [Tooltip("Gap between the Log button and the panel.")]
    [SerializeField] private float gapAboveButton = 12f;

    [Tooltip("Panel background colour.")]
    [SerializeField] private Color panelColor = new Color(0.07f, 0.07f, 0.09f, 0.95f);

    [Tooltip("Show a 'Total' line at the bottom adding up every type.")]
    [SerializeField] private bool showOverallTotal = true;

    [Tooltip("Label for the overall total line.")]
    [SerializeField] private string overallTotalLabel = "All Pixels";

    [Tooltip("Message shown when nothing is unlocked yet.")]
    [SerializeField] private string emptyText = "Nothing collected yet.";

    // ------------------------------------------------------------------
    // Rows / text
    // ------------------------------------------------------------------

    [Header("Rows")]
    [Tooltip("Size of the colour swatch next to each name. 0 = no swatch.")]
    [SerializeField] private float swatchSize = 36f;

    [Tooltip("Border colour of the swatch (keeps white/black swatches visible).")]
    [SerializeField] private Color swatchBorderColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Thickness of the swatch border.")]
    [SerializeField] private float swatchBorderSize = 3f;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 48f;

    [Tooltip("Row text size.")]
    [SerializeField] private float rowFontSize = 34f;

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Colour of the amounts on the right.")]
    [SerializeField] private Color amountColor = new Color(1f, 0.92f, 0.5f, 1f);

    [Tooltip("Compact numbers (1.2K, 3.4M). Off = full number with separators.")]
    [SerializeField] private bool abbreviateNumbers = true;

    [Tooltip("Colour of the divider line above the overall total.")]
    [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.2f);

    // ------------------------------------------------------------------
    // Canvas
    // ------------------------------------------------------------------

    [Header("Canvas")]
    [Tooltip("Sorting order of the log canvas.")]
    [SerializeField] private int sortingOrder = 140;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Tooltip("Start with the panel open.")]
    [SerializeField] private bool startOpen = false;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private class Row
    {
        public RectTransform rect;
        public TMP_Text name;
        public TMP_Text amount;
        public Image swatch;
    }

    private GameObject canvasRoot;
    private GameObject panelObject;
    private RectTransform panelRect;
    private Row[] rows;
    private Row totalRow;
    private RectTransform dividerRect;
    private TMP_Text emptyLabel;
    private bool built;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Start()
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
            Debug.LogError("PixelLog: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (clicker.UIFont != null) font = clicker.UIFont; // one shared font for the whole game

        EnsureEventSystem();
        BuildUI();
        built = true;
        Refresh();
        panelObject.SetActive(startOpen);
    }

    private void OnDestroy()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        if (built && panelObject.activeSelf) Refresh();
    }

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    private static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (FindObjectOfType<EventSystem>() != null) return;
#endif
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildUI()
    {
        canvasRoot = new GameObject("PixelLog Canvas");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        Vector2 anchor = new Vector2(
            buttonCorner == ButtonCorner.TopRight || buttonCorner == ButtonCorner.BottomRight ? 1f : 0f,
            buttonCorner == ButtonCorner.TopLeft || buttonCorner == ButtonCorner.TopRight ? 1f : 0f);
        float sx = anchor.x > 0.5f ? -1f : 1f;
        float sy = anchor.y > 0.5f ? -1f : 1f;

        // --- Button
        Button button = CreateButton(canvasRoot.transform, "Log Button", buttonText, buttonSize, buttonColor,
                                     buttonTextColor, buttonFontSize);
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = anchor;
        br.anchoredPosition = new Vector2(sx * buttonMargin.x, sy * buttonMargin.y);
        button.onClick.AddListener(() =>
        {
            panelObject.SetActive(!panelObject.activeSelf);
            if (panelObject.activeSelf) Refresh();
        });

        // --- Panel (sits next to the button, growing away from the screen edge)
        panelObject = new GameObject("Log Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasRoot.transform, false);
        panelObject.GetComponent<Image>().color = panelColor;
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = anchor;
        panelRect.anchoredPosition = new Vector2(sx * buttonMargin.x,
                                                 sy * (buttonMargin.y + buttonSize.y + gapAboveButton));

        // Title
        TMP_Text title = CreateText(panelObject.transform, "Title", panelTitle, titleFontSize,
                                    TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-(panelPadding * 2f + 160f), headerHeight);
        tr.anchoredPosition = Vector2.zero;

        // Close button
        Button close = CreateButton(panelObject.transform, "Close", "X", new Vector2(70f, 70f),
                                    new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
        close.onClick.AddListener(() => panelObject.SetActive(false));

        // One row per tier (hidden until unlocked).
        rows = new Row[clicker.Tiers.Length];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildRow(panelObject.transform, "Row " + i, false);

        // Divider + overall total row
        GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        div.transform.SetParent(panelObject.transform, false);
        div.GetComponent<Image>().color = dividerColor;
        div.GetComponent<Image>().raycastTarget = false;
        dividerRect = div.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.sizeDelta = new Vector2(-panelPadding * 2f, 2f);

        totalRow = BuildRow(panelObject.transform, "Total Row", true);

        emptyLabel = CreateText(panelObject.transform, "Empty", emptyText, rowFontSize,
                                TextAlignmentOptions.Center, FontStyles.Italic,
                                new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform er = emptyLabel.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.sizeDelta = new Vector2(-panelPadding * 2f, rowHeight);
        er.anchoredPosition = new Vector2(0f, -headerHeight);
    }

    private Row BuildRow(Transform parent, string objectName, bool isTotal)
    {
        Row row = new Row();

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        row.rect = go.GetComponent<RectTransform>();
        row.rect.anchorMin = new Vector2(0f, 1f);
        row.rect.anchorMax = new Vector2(1f, 1f);
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.sizeDelta = new Vector2(-panelPadding * 2f, rowHeight);

        float nameLeft = 0f;
        if (!isTotal && swatchSize > 0f)
        {
            GameObject border = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(go.transform, false);
            Image bi = border.GetComponent<Image>();
            bi.color = swatchBorderColor;
            bi.raycastTarget = false;
            RectTransform brt = border.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(swatchSize, swatchSize);
            brt.anchoredPosition = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(border.transform, false);
            RectTransform fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(swatchBorderSize, swatchBorderSize);
            fr.offsetMax = new Vector2(-swatchBorderSize, -swatchBorderSize);
            row.swatch = fill.GetComponent<Image>();
            row.swatch.raycastTarget = false;

            nameLeft = swatchSize + 16f;
        }

        row.name = CreateText(go.transform, "Name", "", rowFontSize, TextAlignmentOptions.MidlineLeft,
                              isTotal ? FontStyles.Bold : FontStyles.Normal, textColor);
        RectTransform nr = row.name.rectTransform;
        nr.anchorMin = Vector2.zero;
        nr.anchorMax = Vector2.one;
        nr.offsetMin = new Vector2(nameLeft, 0f);
        nr.offsetMax = new Vector2(-200f, 0f);

        row.amount = CreateText(go.transform, "Amount", "", rowFontSize, TextAlignmentOptions.MidlineRight,
                                FontStyles.Bold, amountColor);
        RectTransform ar = row.amount.rectTransform;
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(nameLeft + 100f, 0f);
        ar.offsetMax = Vector2.zero;

        return row;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float size,
                                TextAlignmentOptions alignment, FontStyles style, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, Color color,
                                Color labelColor, float labelSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        go.GetComponent<Button>().targetGraphic = image;

        TMP_Text text = CreateText(go.transform, "Label", label, labelSize, TextAlignmentOptions.Center,
                                   FontStyles.Bold, labelColor);
        RectTransform lr = text.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        return go.GetComponent<Button>();
    }

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    /// <summary>Lists unlocked tiers with their lifetime totals and resizes the panel to fit.</summary>
    public void Refresh()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;

        // The shop may add tiers after startup; keep rows in step.
        if (rows.Length < tiers.Length)
        {
            Row[] bigger = new Row[tiers.Length];
            Array.Copy(rows, bigger, rows.Length);
            for (int i = rows.Length; i < bigger.Length; i++)
                bigger[i] = BuildRow(panelObject.transform, "Row " + i, false);
            rows = bigger;
        }

        float y = headerHeight;
        int visibleCount = 0;
        double overall = 0d;

        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            bool visible = i < tiers.Length && tiers[i].unlocked;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            PixelClicker.PixelTier tier = tiers[i];
            row.name.text = tier.displayName;
            row.amount.text = FormatAmount(tier.totalCollected);
            if (row.swatch != null) row.swatch.color = tier.UIColor;

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight;
            visibleCount++;
            overall += tier.totalCollected;
        }

        emptyLabel.gameObject.SetActive(visibleCount == 0);
        if (visibleCount == 0) y += rowHeight;

        bool showTotal = showOverallTotal && visibleCount > 0;
        dividerRect.gameObject.SetActive(showTotal);
        totalRow.rect.gameObject.SetActive(showTotal);
        if (showTotal)
        {
            dividerRect.anchoredPosition = new Vector2(0f, -y);
            y += 8f;
            totalRow.name.text = overallTotalLabel;
            totalRow.amount.text = FormatAmount(overall);
            totalRow.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight;
        }

        panelRect.sizeDelta = new Vector2(panelWidth, y + panelPadding);
    }

    private string FormatAmount(double value)
    {
        if (!abbreviateNumbers) return Math.Floor(value).ToString("N0");
        if (value < 1000d) return Math.Floor(value).ToString("0");

        string[] suffix = { "", "K", "M", "B", "T" };
        int s = 0;
        while (value >= 1000d && s < suffix.Length - 1) { value /= 1000d; s++; }
        return value.ToString("0.##") + suffix[s];
    }
}
