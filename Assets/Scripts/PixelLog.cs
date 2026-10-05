using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Log" button for Pixel Clicker.
///
/// A button sits in a screen corner (bottom-left by default). Clicking it opens a panel with two tabs:
///  - PIXELS: every UNLOCKED pixel type with the total collected of each (lifetime total, spending
///    doesn't reduce it). Locked types stay hidden until they're unlocked.
///  - ACHIEVEMENTS: a scrolling list of achievements with progress bars and a spinning cube icon
///    (see PixelAchievements).
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

    [Header("Log Tabs")]
    [Tooltip("Label of the tab that lists your pixel totals.")]
    [SerializeField] private string pixelsTabText = "Pixels";

    [Tooltip("Label of the tab that lists achievements.")]
    [SerializeField] private string achievementsTabText = "Achievements";

    [Tooltip("Height of the two tab buttons.")]
    [SerializeField] private float tabHeight = 56f;

    [Tooltip("Gap under the tab buttons.")]
    [SerializeField] private float tabGap = 10f;

    [Tooltip("Tab text size.")]
    [SerializeField] private float tabFontSize = 28f;

    [Tooltip("Tab text colour.")]
    [SerializeField] private Color tabTextColor = Color.white;

    [Tooltip("Colour of the selected tab.")]
    [SerializeField] private Color tabActiveColor = new Color(0.35f, 0.5f, 0.85f, 1f);

    [Tooltip("Colour of the other tab.")]
    [SerializeField] private Color tabInactiveColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Header("Achievements Tab")]
    [Tooltip("The achievements to list. Found automatically (or added to this GameObject) if left empty.")]
    [SerializeField] private PixelAchievements achievements;

    [Tooltip("Height of the scrolling list (canvas units). Longer lists scroll.")]
    [SerializeField] private float achievementsViewHeight = 560f;

    [Tooltip("Height of one achievement.")]
    [SerializeField] private float achievementRowHeight = 112f;

    [Tooltip("Gap between achievements.")]
    [SerializeField] private float achievementSpacing = 8f;

    [Tooltip("Size of the spinning cube icon.")]
    [SerializeField] private float achievementIconSize = 72f;

    [Tooltip("Line above the list. {0} = tiers earned, {1} = total tiers.")]
    [SerializeField] private string achievementSummaryFormat = "{0} / {1} earned";

    [Tooltip("Added after the description of a tiered achievement. {0} = tier being worked on, {1} = number of tiers.")]
    [SerializeField] private string achievementTierFormat = "   -   Tier {0}/{1}";

    [Tooltip("Progress text of an achievement that is not earned yet. {0} = progress, {1} = target.")]
    [SerializeField] private string achievementProgressFormat = "{0} / {1}";

    [Tooltip("Text shown instead of the progress once every tier of an achievement is earned.")]
    [SerializeField] private string achievementUnlockedText = "Complete";

    [Tooltip("Shown when there are no achievements.")]
    [SerializeField] private string noAchievementsText = "No achievements.";

    [Tooltip("Background of an achievement.")]
    [SerializeField] private Color achievementRowColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Tooltip("Title and progress colour of an earned achievement.")]
    [SerializeField] private Color achievementUnlockedColor = new Color(0.45f, 1f, 0.5f, 1f);

    [Range(0.1f, 1f)]
    [Tooltip("How bright a locked achievement's icon and text are (1 = same as earned).")]
    [SerializeField] private float achievementLockedBrightness = 0.5f;

    [Tooltip("Colour of the empty part of a progress bar.")]
    [SerializeField] private Color achievementBarBackColor = new Color(0.06f, 0.06f, 0.09f, 1f);

    [Tooltip("Colour of the filled part of a progress bar for an achievement not earned yet.")]
    [SerializeField] private Color achievementBarColor = new Color(0.4f, 0.6f, 1f, 1f);

    [Tooltip("Spin speed of the cube icons (degrees per second).")]
    [SerializeField] private float achievementIconSpin = 70f;

    [Tooltip("Mouse wheel scroll speed.")]
    [SerializeField] private float achievementScrollSpeed = 60f;

    [Tooltip("Width of the scrollbar.")]
    [SerializeField] private float scrollbarWidth = 20f;

    [Tooltip("Scrollbar track colour.")]
    [SerializeField] private Color scrollbarTrackColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Tooltip("Scrollbar handle colour.")]
    [SerializeField] private Color scrollbarHandleColor = new Color(0.5f, 0.5f, 0.6f, 1f);

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

    [Header("Vacuum Indicator (+X next to each entry)")]
    [Tooltip("After a Vacuum pixel is clicked, show a '+X' next to each entry that gained currency.")]
    [SerializeField] private bool showVacuumDeltas = true;

    [Tooltip("Indicator text. {0} = amount gained.")]
    [SerializeField] private string deltaFormat = "+{0}";

    [Tooltip("Indicator colour.")]
    [SerializeField] private Color deltaColor = new Color(0.45f, 1f, 0.5f, 1f);

    [Tooltip("Indicator text size relative to the row text.")]
    [SerializeField] private float deltaFontScale = 0.85f;

    [Tooltip("Seconds the indicator stays (it fades out over this time).")]
    [SerializeField] private float deltaDuration = 1.6f;

    [Tooltip("How far (canvas units) the indicator drifts upward while fading.")]
    [SerializeField] private float deltaRise = 8f;

    [Tooltip("Distance from the right edge where the indicator ends (leave room for the amount).")]
    [SerializeField] private float deltaRightInset = 150f;

    [Header("Misc")]
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
        public TMP_Text delta;
        public float deltaTimer;
    }

    private class AchievementRow
    {
        public RectTransform rect;
        public PixelCubeIcon icon;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text progress;
        public Image barFill;
        public RectTransform barFillRect;
        public Image background;
    }

    private int currentTab; // 0 = Pixels, 1 = Achievements
    private Image[] tabImages;
    private GameObject achievementsGroup;
    private RectTransform achievementsContent;
    private TMP_Text achievementSummary;
    private TMP_Text noAchievementsLabel;
    private System.Collections.Generic.List<AchievementRow> achievementRows = new System.Collections.Generic.List<AchievementRow>();

    /// <summary>Where content starts: below the title bar and the two tab buttons.</summary>
    private float ContentTop => headerHeight + tabHeight + tabGap;

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

        if (achievements == null)
        {
#if UNITY_2023_1_OR_NEWER
            achievements = FindFirstObjectByType<PixelAchievements>();
#else
            achievements = FindObjectOfType<PixelAchievements>();
#endif
        }
        if (achievements == null) achievements = gameObject.AddComponent<PixelAchievements>();

        EnsureEventSystem();
        BuildUI();
        clicker.VacuumBreakdown += OnVacuumBreakdown;
        built = true;
        Refresh();
        panelObject.SetActive(startOpen);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: components must not be added from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
#if UNITY_2023_1_OR_NEWER
            bool has = FindFirstObjectByType<PixelAchievements>() != null;
#else
            bool has = FindObjectOfType<PixelAchievements>() != null;
#endif
            if (has) return;
            UnityEditor.Undo.AddComponent<PixelAchievements>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };
    }
#endif

    private void OnDestroy()
    {
        if (clicker != null) clicker.VacuumBreakdown -= OnVacuumBreakdown;
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        if (!built) return;
        TickDeltas();
        if (panelObject.activeSelf) Refresh();
    }

    /// <summary>Starts a "+X" indicator next to every entry that the Vacuum just paid.</summary>
    private void OnVacuumBreakdown(double[] perTier)
    {
        if (!showVacuumDeltas || rows == null) return;

        for (int i = 0; i < perTier.Length && i < rows.Length; i++)
        {
            Row row = rows[i];
            if (perTier[i] <= 0d || row == null || row.delta == null) continue;

            row.delta.text = string.Format(deltaFormat, FormatAmount(perTier[i]));
            row.deltaTimer = deltaDuration;
            row.delta.gameObject.SetActive(true);
        }
    }

    /// <summary>Fades and lifts active indicators, then hides them.</summary>
    private void TickDeltas()
    {
        if (rows == null) return;

        foreach (Row row in rows)
        {
            if (row == null || row.delta == null || row.deltaTimer <= 0f) continue;

            row.deltaTimer -= Time.unscaledDeltaTime;
            float remaining = deltaDuration > 0f ? Mathf.Clamp01(row.deltaTimer / deltaDuration) : 0f;

            Color c = deltaColor;
            c.a = deltaColor.a * remaining;
            row.delta.color = c;

            RectTransform dr = row.delta.rectTransform;
            dr.offsetMin = new Vector2(0f, deltaRise * (1f - remaining));
            dr.offsetMax = new Vector2(-deltaRightInset, deltaRise * (1f - remaining));

            if (row.deltaTimer <= 0f) row.delta.gameObject.SetActive(false);
        }
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

        BuildTabs();
        BuildAchievementsGroup();

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
        er.anchoredPosition = new Vector2(0f, -ContentTop);
    }

    private void BuildTabs()
    {
        string[] names = { pixelsTabText, achievementsTabText };
        tabImages = new Image[names.Length];

        float tabWidth = (panelWidth - panelPadding * 2f - 8f) * 0.5f;
        for (int i = 0; i < names.Length; i++)
        {
            Button tab = CreateButton(panelObject.transform, "Tab " + names[i], names[i],
                                      new Vector2(tabWidth, tabHeight), tabInactiveColor, tabTextColor, tabFontSize);
            tabImages[i] = tab.GetComponent<Image>();

            TMP_Text label = tab.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMax = tabFontSize;
            label.fontSizeMin = Mathf.Min(14f, tabFontSize);

            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (tabWidth * 0.5f + 4f), -headerHeight);

            int captured = i;
            tab.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }
    }

    /// <summary>Summary line + scrolling list of achievements (shown only on the Achievements tab).</summary>
    private void BuildAchievementsGroup()
    {
        achievementsGroup = new GameObject("Achievements", typeof(RectTransform));
        achievementsGroup.transform.SetParent(panelObject.transform, false);
        RectTransform gr = achievementsGroup.GetComponent<RectTransform>();
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = gr.offsetMax = Vector2.zero;

        float summaryHeight = rowFontSize * 1.3f;
        achievementSummary = CreateText(achievementsGroup.transform, "Summary", "", rowFontSize * 0.85f,
                                        TextAlignmentOptions.MidlineLeft, FontStyles.Bold, amountColor);
        RectTransform sr = achievementSummary.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(-panelPadding * 2f, summaryHeight);
        sr.anchoredPosition = new Vector2(0f, -ContentTop);

        float viewTop = ContentTop + summaryHeight + 6f;

        // Scroll view (also the viewport; the transparent image lets empty space take wheel/drag input).
        GameObject view = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(achievementsGroup.transform, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        RectTransform vr = view.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-(panelPadding * 2f + scrollbarWidth + 8f), achievementsViewHeight);
        vr.anchoredPosition = new Vector2(-(scrollbarWidth + 8f) * 0.5f, -viewTop);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(view.transform, false);
        achievementsContent = content.GetComponent<RectTransform>();
        achievementsContent.anchorMin = new Vector2(0f, 1f);
        achievementsContent.anchorMax = new Vector2(1f, 1f);
        achievementsContent.pivot = new Vector2(0.5f, 1f);
        achievementsContent.sizeDelta = Vector2.zero;
        achievementsContent.anchoredPosition = Vector2.zero;

        // Scrollbar
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(achievementsGroup.transform, false);
        barGo.GetComponent<Image>().color = scrollbarTrackColor;
        RectTransform br = barGo.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 1f);
        br.sizeDelta = new Vector2(scrollbarWidth, achievementsViewHeight);
        br.anchoredPosition = new Vector2(-panelPadding, -viewTop);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(barGo.transform, false);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = scrollbarHandleColor;
        RectTransform hr = handleGo.GetComponent<RectTransform>();
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = hr;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        ScrollRect scroll = view.GetComponent<ScrollRect>();
        scroll.content = achievementsContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = achievementScrollSpeed;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        noAchievementsLabel = CreateText(view.transform, "Empty", noAchievementsText, rowFontSize,
                                         TextAlignmentOptions.Center, FontStyles.Italic,
                                         new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform er = noAchievementsLabel.rectTransform;
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = er.offsetMax = Vector2.zero;
        noAchievementsLabel.gameObject.SetActive(false);

        achievementsGroup.SetActive(false);
    }

    private AchievementRow BuildAchievementRow(int index)
    {
        AchievementRow row = new AchievementRow();

        GameObject go = new GameObject("Achievement " + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(achievementsContent, false);
        row.background = go.GetComponent<Image>();
        row.background.color = achievementRowColor;
        row.rect = go.GetComponent<RectTransform>();
        row.rect.anchorMin = new Vector2(0f, 1f);
        row.rect.anchorMax = new Vector2(1f, 1f);
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.sizeDelta = new Vector2(0f, achievementRowHeight);

        // Spinning cube icon.
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(go.transform, false);
        row.icon = iconGo.AddComponent<PixelCubeIcon>();
        row.icon.spinDegreesPerSecond = achievementIconSpin;
        RectTransform ir = row.icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(achievementIconSize, achievementIconSize);
        ir.anchoredPosition = new Vector2(10f, 0f);

        float left = achievementIconSize + 24f;

        row.title = CreateText(go.transform, "Title", "", rowFontSize * 0.9f, TextAlignmentOptions.MidlineLeft,
                               FontStyles.Bold, textColor);
        row.title.enableAutoSizing = true;
        row.title.fontSizeMax = rowFontSize * 0.9f;
        row.title.fontSizeMin = 14f;
        RectTransform tr = row.title.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.58f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(left, 0f);
        tr.offsetMax = new Vector2(-170f, -4f);

        row.progress = CreateText(go.transform, "Progress", "", rowFontSize * 0.75f, TextAlignmentOptions.MidlineRight,
                                  FontStyles.Bold, amountColor);
        row.progress.enableAutoSizing = true;
        row.progress.fontSizeMax = rowFontSize * 0.75f;
        row.progress.fontSizeMin = 12f;
        RectTransform pr = row.progress.rectTransform;
        pr.anchorMin = new Vector2(1f, 0.58f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(1f, 0.5f);
        pr.sizeDelta = new Vector2(160f, 0f);
        pr.anchoredPosition = new Vector2(-10f, -2f);

        row.description = CreateText(go.transform, "Description", "", rowFontSize * 0.68f, TextAlignmentOptions.MidlineLeft,
                                     FontStyles.Normal, new Color(textColor.r, textColor.g, textColor.b, 0.7f));
        row.description.enableAutoSizing = true;
        row.description.fontSizeMax = rowFontSize * 0.68f;
        row.description.fontSizeMin = 12f;
        RectTransform dr = row.description.rectTransform;
        dr.anchorMin = new Vector2(0f, 0.28f);
        dr.anchorMax = new Vector2(1f, 0.58f);
        dr.offsetMin = new Vector2(left, 0f);
        dr.offsetMax = new Vector2(-12f, 0f);

        // Progress bar.
        GameObject back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(go.transform, false);
        back.GetComponent<Image>().color = achievementBarBackColor;
        back.GetComponent<Image>().raycastTarget = false;
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0.07f);
        br.anchorMax = new Vector2(1f, 0.21f);
        br.offsetMin = new Vector2(left, 0f);
        br.offsetMax = new Vector2(-12f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(back.transform, false);
        row.barFill = fill.GetComponent<Image>();
        row.barFill.raycastTarget = false;
        row.barFillRect = fill.GetComponent<RectTransform>();
        row.barFillRect.anchorMin = Vector2.zero;
        row.barFillRect.anchorMax = new Vector2(0f, 1f);
        row.barFillRect.offsetMin = row.barFillRect.offsetMax = Vector2.zero;

        return row;
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

        if (!isTotal)
        {
            row.delta = CreateText(go.transform, "Delta", "", rowFontSize * deltaFontScale,
                                   TextAlignmentOptions.MidlineRight, FontStyles.Bold, deltaColor);
            RectTransform dr = row.delta.rectTransform;
            dr.anchorMin = Vector2.zero;
            dr.anchorMax = Vector2.one;
            dr.offsetMin = Vector2.zero;
            dr.offsetMax = new Vector2(-deltaRightInset, 0f);
            row.delta.gameObject.SetActive(false);
        }

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

    /// <summary>Refreshes whichever tab is open and resizes the panel to fit.</summary>
    public void Refresh()
    {
        for (int i = 0; i < tabImages.Length; i++)
            tabImages[i].color = i == currentTab ? tabActiveColor : tabInactiveColor;

        bool pixelsTab = currentTab == 0;
        achievementsGroup.SetActive(!pixelsTab);

        if (pixelsTab) RefreshPixelsTab();
        else RefreshAchievementsTab();
    }

    /// <summary>Hides the Pixels tab's rows, divider, total and empty text.</summary>
    private void HidePixelsTab()
    {
        foreach (Row row in rows) row.rect.gameObject.SetActive(false);
        dividerRect.gameObject.SetActive(false);
        totalRow.rect.gameObject.SetActive(false);
        emptyLabel.gameObject.SetActive(false);
    }

    private void RefreshAchievementsTab()
    {
        HidePixelsTab();

        int count = achievements != null ? achievements.Count : 0;
        while (achievementRows.Count < count) achievementRows.Add(BuildAchievementRow(achievementRows.Count));

        float y = 0f;
        for (int i = 0; i < achievementRows.Count; i++)
        {
            AchievementRow row = achievementRows[i];
            bool visible = i < count;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            bool unlocked = achievements.IsComplete(i);           // every tier earned
            bool started = achievements.HasAnyTier(i);            // at least one tier earned
            float dim = started ? 1f : achievementLockedBrightness;

            int tierCount = achievements.GetTierCount(i);
            string tierText = tierCount > 1
                ? string.Format(achievementTierFormat, Math.Min(achievements.GetEarnedTiers(i) + 1, tierCount), tierCount) : "";
            row.title.text = achievements.GetTitle(i);
            row.description.text = achievements.GetDescription(i) + tierText;
            row.title.color = unlocked ? achievementUnlockedColor : new Color(textColor.r * dim, textColor.g * dim, textColor.b * dim, textColor.a);

            double currentTarget = achievements.GetCurrentTarget(i);
            row.progress.text = unlocked
                ? achievementUnlockedText
                : string.Format(achievementProgressFormat, FormatAmount(Math.Min(achievements.GetProgress(i), currentTarget)), FormatAmount(currentTarget));
            row.progress.color = unlocked ? achievementUnlockedColor : amountColor;

            Color icon = achievements.GetIconColor(i);
            row.icon.color = new Color(icon.r * dim, icon.g * dim, icon.b * dim, icon.a);

            row.barFill.color = unlocked ? achievementUnlockedColor : achievementBarColor;
            row.barFillRect.anchorMax = new Vector2(unlocked ? 1f : achievements.GetFraction(i), 1f);

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += achievementRowHeight + achievementSpacing;
        }

        achievementsContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, y - achievementSpacing));
        noAchievementsLabel.gameObject.SetActive(count == 0);
        achievementSummary.text = string.Format(achievementSummaryFormat, achievements != null ? achievements.EarnedTierTotal : 0, achievements != null ? achievements.TierTotal : 0);

        float summaryHeight = rowFontSize * 1.3f;
        panelRect.sizeDelta = new Vector2(panelWidth, ContentTop + summaryHeight + 6f + achievementsViewHeight + panelPadding);
    }

    /// <summary>Lists unlocked tiers with their lifetime totals and resizes the panel to fit.</summary>
    private void RefreshPixelsTab()
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

        float y = ContentTop;
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
