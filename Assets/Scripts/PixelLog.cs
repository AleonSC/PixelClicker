using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "Log" button for Pixel Clicker.
///
/// A button sits in a screen corner (bottom-left by default). Clicking it opens a panel with three tabs (Achievements, Goals, Event log):
///  - PIXELS: every UNLOCKED pixel type with the total collected of each (lifetime total, spending
///    doesn't reduce it). Locked types stay hidden until they're unlocked.
///  - ACHIEVEMENTS: a scrolling list of achievements with progress bars and a spinning cube icon
///    (see PixelAchievements).
///
/// Add this to any GameObject (e.g. the cube). The UI builds itself at runtime.
/// The panel is a standard Fixed window (PixelUIKit.CreateWindow: title, grey X, the Collection Log book in the left header slot),
/// kept at its HUD corner instead of the centre.
/// GOALS tab (CollectGoals): the next pixel unlock, then the data-driven goals of PixelGoals (AddTableGoals: crush / recycle / streak goals with
/// their own rewards, 2026-10-18), then the hand-built shop / skill goals and the minigame trackers.
/// EVENT LOG tab (PixelLogEvents.cs, since 2026-10-25): every event line, a click shows its full tip; the event log key opens the Log on it.
/// Part files (partial class): PixelLogAchievements.cs (the Achievements tab), PixelLogGoals.cs (the Goals tab), PixelLogEvents.cs (the Event log tab).
/// </summary>
public partial class PixelLog : MonoBehaviour
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

    [Header("Performance")]
    [Tooltip("Seconds between refreshes of the open Log window (opening and tab changes refresh at once).")]
    [SerializeField] private float refreshSeconds = 0.2f;

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

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    [Header("Log Tabs")]
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

    [Tooltip("Label of the third Log tab: progress bars for things like Ghosts Caught, the Singularity count, Stardust and the next pixel unlock.")]
    [SerializeField] private string goalsTabText = "Goals";

    [Tooltip("Text when the Goals tab has nothing to show yet.")]
    [SerializeField] private string noGoalsText = "No goals yet.";

    [Header("Event Log Tab")]
    [Tooltip("Label of the Event log tab: every event line (unlocks, level-ups, tips...); a click on one shows its full text.")]
    [SerializeField] private string eventsTabText = "Event log";

    [Tooltip("Text size of the event lines (Settings > Accessibility > Event log size scales it).")]
    [SerializeField] private float eventLogFontSize = 24f;

    [Tooltip("Text when nothing has happened yet.")]
    [SerializeField] private string noEventsText = "Nothing has happened yet.";

    [Tooltip("Title of the 'next pixel unlock' goal. {0} = the pixel's name.")]
    [SerializeField] private string nextPixelGoalFormat = "Unlock {0}";

    [Tooltip("Description of the 'next pixel unlock' goal. {0} = the pixel that has to be collected.")]
    [SerializeField] private string nextPixelGoalDescription = "Collect {0} pixels.";

    [Tooltip("Goals tab: title of the 'buy the RGB Pack' goal shown once the Shop appears ({0} = the pack's name).")]
    [SerializeField] private string buyRgbGoalFormat = "Purchase the {0}";

    [Tooltip("Goals tab: description of the 'buy the RGB Pack' goal ({0} = its price).")]
    [SerializeField] private string buyRgbGoalDescription = "Open the Shop and buy it ({0}).";

    [Min(40f)]
    [Tooltip("Height of a goal row.")]
    [SerializeField] private float goalRowHeight = 100f;

    [Header("Achievements Tab")]
    [Tooltip("Placeholder of the search box above the achievements.")]
    [SerializeField] private string searchPlaceholder = "Search achievements...";

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

    [Tooltip("Inner padding of the panel.")]
    [SerializeField] private float panelPadding = 20f;



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

    [Tooltip("Row text size.")]
    [SerializeField] private float rowFontSize = 34f;

    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Tooltip("Colour of the amounts on the right.")]
    [SerializeField] private Color amountColor = new Color(1f, 0.92f, 0.5f, 1f);

    [Tooltip("Indicator text. {0} = amount gained.")]
    [SerializeField] private string deltaFormat = "+{0}";

    [Tooltip("Also show a '+X' next to a pixel's row for every click you or the auto clicker make. Clicks in quick succession add up. Same format, colour and fade as the vacuum indicator.")]
    [SerializeField] private bool showGainDeltas = true;

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

    private ScrollRect pixelsScroll;
    private RectTransform pixelsViewport;
    private RectTransform pixelsContent;
    private GameObject pixelsBar;

    private class Row
    {
        public RectTransform rect;
        public TMP_Text name;
        public TMP_Text amount;
        public Image swatch;
        public float nameLeft;
        public TMP_Text delta;
        public float deltaTimer;
        public double gainSum;
    }

    private class AchievementRow
    {
        public RectTransform rect;
        public PixelCubeIcon icon;
        public RawImage skin;
        public int skinTier = -1;
        public TMP_Text title;
        public TMP_Text description;
        public TMP_Text progress;
        public Image barFill;
        public RectTransform barFillRect;
        public Image background;
        public TMP_Text modeTag;
        public int groupIndex;
        public int achIndex = -1;
    }

    private int currentTab = 1; // 0 = Pixels (no longer reachable: moved to Settings > Stats), 1 = Achievements, 2 = Goals, 3 = Event log (PixelLogEvents.cs)
    private Image[] tabImages;
    private GameObject achievementsGroup, goalsGroup;
    private RectTransform achievementsContent, goalsContent;
    private TMP_Text noGoalsLabel;
    private TMP_Text achievementSummary;
    private TMP_InputField achievementSearchField;
    private string achievementSearch = "";
    private Image achievementSummaryFill;
    private RectTransform achievementSummaryFillRect;
    private TMP_Text noAchievementsLabel;
    private System.Collections.Generic.List<AchievementRow> achievementRows = new System.Collections.Generic.List<AchievementRow>();

    /// <summary>Where content starts: below the title bar and the two tab buttons.</summary>
    private float ContentTop => headerHeight + tabHeight + tabGap;

    /// <summary>The window is always this tall, whichever tab is open, so nothing moves when you switch tabs.</summary>
    private float FixedPanelHeight => ContentTop + rowFontSize * 1.3f + 6f + achievementsViewHeight + panelPadding;

    private GameObject canvasRoot;
    private GameObject panelObject;
    private RectTransform panelRect;
    private PixelWindowParts windowParts;   // the standard window the panel is built on (PixelUIKit.CreateWindow)

    /// <summary>Height of the standard window header (PixelUIKitWindows.cs): the tabs start below it.</summary>
    private float headerHeight => windowParts != null ? windowParts.HeaderHeight : PixelUIKit.WindowHeaderHeight;
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
            clicker = PixelFind.First<PixelClicker>();
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
            achievements = PixelFind.First<PixelAchievements>();
        }
        if (achievements == null) achievements = gameObject.AddComponent<PixelAchievements>();

        PixelUIKit.EnsureEventSystem();
        BuildUI();
        clicker.VacuumBreakdown += OnVacuumBreakdown;
        clicker.PixelCollected += OnPixelCollected;
        built = true;
        Refresh();
        panelObject.SetActive(startOpen);
        PixelWindows.Register(this, 20, () => panelObject != null && PixelPop.IsOpen(panelObject), () => PixelPop.Hide(panelObject));
        logInstance = this;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: components must not be added from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            bool has = PixelFind.First<PixelAchievements>() != null;
            if (has) return;
            UnityEditor.Undo.AddComponent<PixelAchievements>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };
    }
#endif

    private static PixelLog logInstance;

    /// <summary>Opens or closes the Log panel. Opening it closes the Inventory (they never show together).</summary>
    /// <summary>The Log window's rectangle (null if there is none); used to place tip boxes next to it.</summary>
    public static RectTransform WindowRect => logInstance != null && logInstance.panelObject != null ? logInstance.panelObject.GetComponent<RectTransform>() : null;

    public static bool LogOpen => logInstance != null && logInstance.panelObject != null && PixelPop.IsOpen(logInstance.panelObject);

    /// <summary>Switches the Log to its Goals tab (the new-game intro box points at the goals).</summary>
    public static void ShowGoalsTab()
    {
        PixelLog log = logInstance;
        if (log == null) return;
        log.currentTab = 2;
        if (log.built) log.Refresh();
    }

    public static void SetLogOpen(bool open)
    {
        PixelLog log = logInstance;
        if (log == null || log.panelObject == null) return;
        if (open) PixelPop.Show(log.panelObject); else PixelPop.Hide(log.panelObject);
        if (open)
        {
            log.Refresh();
            PixelUI.SetInventoryOpen(false);
            PixelBank.CloseWindowIfOpen();
            PixelSkills.CloseWindowIfOpen();
            PixelShop.CloseShopWindow();
        }
    }

    private static readonly Vector3[] skinViewCorners = new Vector3[4], skinRowCorners = new Vector3[4];   // (reused every frame)

    /// <summary>Live picture of a pixel type with its real look (material, glow, extras): the shared PixelTierPicture studio of that type; null if it can't be made.</summary>
    private Texture SkinIcon(int tier)
    {
        if (tier < 0 || clicker == null) return null;
        Texture tex = PixelTierPicture.Get(clicker, tier, 160);
        if (tex != null) PixelTierPicture.Render(tier, Time.unscaledTime * achievementIconSpin);
        return tex;
    }

    /// <summary>Re-renders (turning) the pictures on the goal rows that are in view.</summary>
    private void SpinVisibleGoalSkins()
    {
        if (goalRows.Count == 0 || goalsContent == null) return;
        RectTransform viewport = goalsContent.parent as RectTransform;
        if (viewport == null) return;
        Vector3[] vc = skinViewCorners, rc = skinRowCorners;
        viewport.GetWorldCorners(vc);
        foreach (GoalRow row in goalRows)
        {
            if (row.skin == null || !row.skin.gameObject.activeInHierarchy || row.skinTier < 0) continue;
            row.rect.GetWorldCorners(rc);
            if (rc[2].y < vc[0].y || rc[0].y > vc[2].y) continue;
            PixelTierPicture.Render(row.skinTier, Time.unscaledTime * achievementIconSpin);   // (same pose as PixelCubeIcon; a type shown twice draws once)
        }
    }

    /// <summary>Re-renders (turning) the pictures of the pixel types whose achievement rows are in view.</summary>
    private void SpinVisibleSkins()
    {
        if (achievementRows.Count == 0 || achievementsContent == null) return;
        RectTransform viewport = achievementsContent.parent as RectTransform;
        if (viewport == null) return;
        Vector3[] vc = skinViewCorners, rc = skinRowCorners;
        viewport.GetWorldCorners(vc);
        for (int i = 0; i < achievementRows.Count; i++)
        {
            AchievementRow row = achievementRows[i];
            if (row.skin == null || !row.skin.gameObject.activeInHierarchy || row.skinTier < 0) continue;
            row.rect.GetWorldCorners(rc);
            if (rc[2].y < vc[0].y || rc[0].y > vc[2].y) continue; // scrolled out of view
            PixelTierPicture.Render(row.skinTier, Time.unscaledTime * achievementIconSpin);
        }
    }

    private void OnDestroy()
    {
        if (logInstance == this) logInstance = null;
        PixelWindows.Unregister(this);
        if (clicker != null)
        {
            clicker.VacuumBreakdown -= OnVacuumBreakdown;
            clicker.PixelCollected -= OnPixelCollected;
        }
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        if (!built) return;
        TickDeltas();
        if (!panelObject.activeSelf) { panelWasOpen = false; return; }
        if (currentTab == 1) SpinVisibleSkins();
        else if (currentTab == 2) SpinVisibleGoalSkins();
        UpdateAchievementTip();
        if (panelWasOpen && Time.unscaledTime < nextRefreshTime) return;
        panelWasOpen = true;
        nextRefreshTime = Time.unscaledTime + refreshSeconds;
        Refresh();
    }

    // The hover text over an achievement: says exactly what counts (collecting vs clicking).
    private int hoverAchievement = -1;
    private GameObject tipRoot;
    private RectTransform tipRect;
    private TMP_Text tipLabel;

    private string AchievementTipText(int a)
    {
        PixelAchievements.Kind kind = achievements.GetKind(a);
        double target = achievements.GetCurrentTarget(a);
        double have = Math.Min(achievements.GetProgress(a), target);
        string pixel = kind == PixelAchievements.Kind.CollectPixelType || kind == PixelAchievements.Kind.ClickPixelType
            ? PixelNameOf(achievements.GetPixelType(a)).Replace(" Pixels", "").Replace(" Pixel", "") : "";
        string head, body;
        if (kind == PixelAchievements.Kind.ClickPixelType)
        {
            head = "CLICKING";
            body = "Click " + pixel + " pixels " + FormatAmount(target) + " times. Only your clicks count - auto clicker clicks too, but not the pixels you earn (multipliers, vacuums and minigames don't add to it).";
        }
        else if (kind == PixelAchievements.Kind.CollectPixelType)
        {
            head = "COLLECTING";
            body = "Collect " + FormatAmount(target) + " " + pixel + " pixels in total. Everything you earn counts - clicks with their multipliers, the auto clicker, vacuums, minigames and rewards - and spending them does not lower it.";
        }
        else
        {
            head = "ACHIEVEMENT";
            body = achievements.GetDescription(a);
        }
        int tiers = achievements.GetTierCount(a);
        string tier = tiers > 1 ? "\nTier " + Math.Min(achievements.GetEarnedTiers(a) + 1, tiers) + " of " + tiers : "";
        // The log of what was earned and when (the exact date and time, in your own time).
        string earned = "";
        int got = Math.Min(achievements.GetEarnedTiers(a), tiers);
        if (got > 0)
        {
            System.Text.StringBuilder log = new System.Text.StringBuilder("\n\n<b>EARNED</b>");
            for (int t = 0; t < got; t++)
            {
                string when = achievements.TryGetEarnedAt(a, t, out DateTime at) ? at.ToString("yyyy-MM-dd  HH:mm:ss") : "date not recorded";
                log.Append('\n').Append(tiers > 1 ? "Tier " + (t + 1) + ":  " : "").Append(when);
            }
            earned = log.ToString();
        }
        return "<b>" + head + "</b>\n" + body + "\nProgress: " + FormatAmount(have) + " / " + FormatAmount(target) + tier + earned;
    }

    private string GoalTipText(Goal g)
    {
        double have = Math.Min(g.count, g.goal);
        double percent = g.goal > 0d ? have / g.goal * 100d : 100d;
        string status = g.count >= g.goal ? "Complete!" : "Still needed: " + FormatAmount(g.goal - g.count);
        return "<b>" + g.title + "</b>\n" + g.description + "\nProgress: " + FormatAmount(have) + " / " + FormatAmount(g.goal) + " (" + Math.Floor(percent) + "%)\n" + status;
    }

    private string PixelNameOf(PixelClicker.PixelType type)
    {
        int t = clicker != null ? clicker.IndexOf(type) : -1;
        return t >= 0 ? clicker.Tiers[t].displayName : type.ToString();
    }

    // The tip's text is built again when the hovered row changes or every 0.2 s (its progress can move); it is measured only when the text changed.
    private int tipShownTab = -1, tipShownIndex = -1;
    private float tipRebuildTimer;
    private string tipShownText;

    private void UpdateAchievementTip()
    {
        bool showAch = currentTab == 1 && hoverAchievement >= 0 && achievements != null && hoverAchievement < achievements.Count;
        bool showGoal = currentTab == 2 && hoverGoal >= 0 && hoverGoal < shownGoals.Count;
        bool show = showAch || showGoal;
        if (!show) { if (tipRoot != null && tipRoot.activeSelf) tipRoot.SetActive(false); return; }

        if (tipRoot == null)
        {
            tipRoot = new GameObject("Achievement Tip", typeof(RectTransform), typeof(Image));
            tipRoot.transform.SetParent(canvasRoot.transform, false);
            tipRoot.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.09f, 0.97f);
            tipRoot.GetComponent<Image>().raycastTarget = false;
            tipRect = tipRoot.GetComponent<RectTransform>();
            tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tipRect.pivot = new Vector2(0f, 1f);
            tipLabel = CreateText(tipRoot.transform, "Text", "", rowFontSize * 0.7f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
            tipLabel.richText = true;
            tipLabel.raycastTarget = false;
            tipLabel.rectTransform.anchorMin = Vector2.zero; tipLabel.rectTransform.anchorMax = Vector2.one;
            tipLabel.rectTransform.offsetMin = new Vector2(16f, 12f); tipLabel.rectTransform.offsetMax = new Vector2(-16f, -12f);
        }
        if (!tipRoot.activeSelf) tipRoot.SetActive(true);
        tipRoot.transform.SetAsLastSibling();

        int index = showGoal ? hoverGoal : hoverAchievement;
        tipRebuildTimer -= Time.unscaledDeltaTime;
        if (currentTab != tipShownTab || index != tipShownIndex || tipRebuildTimer <= 0f)
        {
            tipShownTab = currentTab; tipShownIndex = index; tipRebuildTimer = 0.2f;
            string text = showGoal ? GoalTipText(shownGoals[hoverGoal]) : AchievementTipText(hoverAchievement);
            if (text != tipShownText)
            {
                tipShownText = text;
                const float width = 620f;
                tipLabel.text = text;
                float h = Mathf.Ceil(tipLabel.GetPreferredValues(text, width - 32f, 0f).y) + 28f;
                tipRect.sizeDelta = new Vector2(width, h);
            }
        }

        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
        PixelUIKit.PlaceTooltip(tipRect, canvasRect, local, 22f);
    }

    private bool panelWasOpen;
    private float nextRefreshTime;

    /// <summary>Shows (and adds up) "+X" next to a pixel's row for each click's payout.</summary>
    private void OnPixelCollected(int tierIndex, double amount, bool automatic)
    {
        if (!showGainDeltas || rows == null || tierIndex < 0 || tierIndex >= rows.Length) return;
        if (panelObject == null || !panelObject.activeSelf) return;   // (runs on every click: no '+N' to update while the Log is closed)
        Row row = rows[tierIndex];
        if (row == null || row.delta == null) return;

        row.gainSum = row.deltaTimer > 0f && row.gainSum > 0d ? row.gainSum + 1d : 1d; // +1 per click, whatever the payout multiplier
        row.delta.text = string.Format(deltaFormat, FormatAmount(row.gainSum));
        row.deltaTimer = deltaDuration;
        row.delta.gameObject.SetActive(true);
    }

    /// <summary>Starts a "+X" indicator next to every entry that the Vacuum just paid.</summary>
    private void OnVacuumBreakdown(double[] perTier)
    {
        // The Pixels tab counts clicks and a Vacuum payout is not a click, so there is nothing to show here.
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

            if (row.deltaTimer <= 0f)
            {
                row.delta.gameObject.SetActive(false);
                row.gainSum = 0d;
            }
        }
    }

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelLog Canvas", sortingOrder, referenceResolution, true);

        Vector2 anchor = new Vector2(
            buttonCorner == ButtonCorner.TopRight || buttonCorner == ButtonCorner.BottomRight ? 1f : 0f,
            buttonCorner == ButtonCorner.TopLeft || buttonCorner == ButtonCorner.TopRight ? 1f : 0f);
        float sx = anchor.x > 0.5f ? -1f : 1f;
        float sy = anchor.y > 0.5f ? -1f : 1f;

        // --- Button
        Button button = CreateButton(canvasRoot.transform, "Log Button", buttonText, buttonSize, ButtonRole.Neutral,
                                     buttonTextColor, buttonFontSize);
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = anchor;
        br.anchoredPosition = new Vector2(sx * buttonMargin.x, sy * buttonMargin.y);
        PixelHud hud = PixelHud.Ensure(gameObject);
        hud.Dock(br, anchor, () => panelObject != null && panelObject.activeSelf);
        button.onClick.AddListener(() => SetLogOpen(!PixelPop.IsOpen(panelObject)));
        PixelMenuKeys.Register(PixelAction.LogWindow, button);

        // --- Panel: the standard Fixed window (title, grey X), kept next to the button, growing away from the screen edge
        windowParts = PixelUIKit.CreateWindow(font, canvasRoot.transform, "Log Panel", panelTitle, new Vector2(panelWidth, 400f),
                                              PixelUIKit.WindowKind.Fixed, () => PixelPop.Hide(panelObject));
        panelObject = windowParts.Root;
        panelRect = windowParts.Rect;
        // Fixed windows are centred by the builder: put it back at its HUD corner.
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = anchor;
        panelRect.anchoredPosition = new Vector2(sx * hud.SideShownInset, sy * hud.WindowOffset); // the same gap from the black margin as the docked button keeps
        PixelMenuFx.AddWindowLine(panelObject.transform, headerHeight - 6f, panelPadding);

        // The Collection Log: a book button in the left header slot.
        Button book = windowParts.AddHeaderButton("Collection Log Button", "", true);
        GameObject bookIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        bookIcon.transform.SetParent(book.transform, false);
        Image bookImage = bookIcon.GetComponent<Image>();
        bookImage.sprite = PixelCollectionLog.BookSprite(); bookImage.raycastTarget = false; bookImage.preserveAspect = true;
        RectTransform bir = bookIcon.GetComponent<RectTransform>();
        bir.anchorMin = bir.anchorMax = bir.pivot = new Vector2(0.5f, 0.5f);
        bir.sizeDelta = new Vector2(52f, 39f);
        book.onClick.AddListener(PixelCollectionLog.Toggle);

        BuildTabs();
        BuildAchievementsGroup();
        BuildGoalsGroup();
        BuildEventsGroup();

        // The Pixels tab's rows live in a scroll view under the tabs.
        pixelsScroll = PixelUIKit.CreateScrollView(panelObject.transform, "Pixels List", scrollbarHandleColor, 12f,
                                                   achievementScrollSpeed, out pixelsContent, out pixelsBar);
        pixelsViewport = pixelsScroll.GetComponent<RectTransform>();
        pixelsViewport.anchorMin = Vector2.zero;
        pixelsViewport.anchorMax = Vector2.one;
        pixelsViewport.offsetMin = new Vector2(0f, panelPadding);
        pixelsViewport.offsetMax = new Vector2(0f, -ContentTop);

        // One row per tier (hidden until unlocked).
        rows = new Row[clicker.Tiers.Length];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildRow(pixelsContent, "Row " + i, false);

        // Divider + overall total row
        GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        div.transform.SetParent(pixelsContent, false);
        div.GetComponent<Image>().color = dividerColor;
        div.GetComponent<Image>().raycastTarget = false;
        dividerRect = div.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.sizeDelta = new Vector2(-panelPadding * 2f, 2f);

        totalRow = BuildRow(pixelsContent, "Total Row", true);

        emptyLabel = CreateText(pixelsContent, "Empty", emptyText, rowFontSize,
                                TextAlignmentOptions.Center, FontStyles.Italic,
                                new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform er = emptyLabel.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.sizeDelta = new Vector2(-panelPadding * 2f, rowHeight);
        er.anchoredPosition = Vector2.zero;
    }

    private void BuildTabs()
    {
        string[] names = { achievementsTabText, goalsTabText, eventsTabText };   // the click counts moved to Settings > Stats
        tabImages = new Image[names.Length + 1];                  // [0] (the old Clicks tab) stays empty

        const float gap = 8f;
        float tabWidth = (panelWidth - panelPadding * 2f - gap * (names.Length - 1)) / names.Length;
        float startX = -(tabWidth + gap) * (names.Length - 1) * 0.5f;
        for (int i = 0; i < names.Length; i++)
        {
            Button tab = CreateButton(panelObject.transform, "Tab " + names[i], names[i],
                                      new Vector2(tabWidth, tabHeight), ButtonRole.Neutral, tabTextColor, tabFontSize);
            PixelUIKit.Caps(tab);
            tabImages[i + 1] = tab.GetComponent<Image>();

            TMP_Text label = tab.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMax = tabFontSize;
            label.fontSizeMin = 10f;
            label.textWrappingMode = TextWrappingModes.NoWrap;   // long names ("Achievements") shrink to fit instead of spilling out
            label.rectTransform.offsetMin = new Vector2(10f, 4f);
            label.rectTransform.offsetMax = new Vector2(-10f, -4f);

            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(startX + i * (tabWidth + gap), -headerHeight);

            int captured = i + 1;
            tab.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }
    }

    /// <summary>Summary line + scrolling list of achievements (shown only on the Achievements tab).</summary>
    private void BuildAchievementsGroup()
    {
        BuildScrollGroup("Achievements", noAchievementsText, true, out achievementsGroup, out achievementsContent,
                         out achievementSummary, out noAchievementsLabel);
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
        nr.offsetMax = Vector2.zero;
        row.name.enableAutoSizing = true;
        row.name.fontSizeMax = rowFontSize;
        row.name.fontSizeMin = Mathf.Max(8f, rowFontSize * 0.5f);
        row.name.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
            row.name.textWrappingMode = TextWrappingModes.NoWrap;
#else
            row.name.enableWordWrapping = false;
#endif
        row.nameLeft = nameLeft;

        row.amount = CreateText(go.transform, "Amount", "", rowFontSize, TextAlignmentOptions.MidlineRight,
                                FontStyles.Bold, amountColor);
        RectTransform ar = row.amount.rectTransform;
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(nameLeft + 100f, 0f);
        ar.offsetMax = Vector2.zero;
        // Big numbers shrink to fit the space next to the name instead of wrapping or running over it.
        row.amount.enableAutoSizing = true;
        row.amount.fontSizeMax = rowFontSize;
        row.amount.fontSizeMin = Mathf.Max(8f, rowFontSize * 0.3f);
        row.amount.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
            row.amount.textWrappingMode = TextWrappingModes.NoWrap;
#else
            row.amount.enableWordWrapping = false;
#endif

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
        => PixelUIKit.CreateText(font, parent, objectName, text, size, alignment, style, color);

    /// <summary>A button in a role (PixelUITheme) with its own label colour.</summary>
    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, ButtonRole role,
                      Color labelColor, float labelSize)
    {
        Button b = PixelUIKit.CreateButton(font, parent, objectName, label, size, PixelUITheme.ColorOf(role), labelColor, labelSize);
        PixelUIKit.SetRole(b, role);
        return b;
    }

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    /// <summary>Refreshes whichever tab is open and resizes the panel to fit.</summary>
    public void Refresh()
    {
        for (int i = 0; i < tabImages.Length; i++)
            if (tabImages[i] != null) PixelUIKit.SetRole(tabImages[i], i == currentTab ? ButtonRole.Selected : ButtonRole.Neutral);

        bool pixelsTab = currentTab == 0;
        achievementsGroup.SetActive(currentTab == 1);
        goalsGroup.SetActive(currentTab == 2);
        if (eventsGroup != null) eventsGroup.SetActive(currentTab == EventsTab);
        pixelsViewport.gameObject.SetActive(pixelsTab);

        if (pixelsTab) RefreshPixelsTab();
        else if (currentTab == 1) RefreshAchievementsTab();
        else if (currentTab == EventsTab) RefreshEventsTab();
        else RefreshGoalsTab();
    }

    /// <summary>Hides the Pixels tab's rows, divider, total and empty text.</summary>
    private void HidePixelsTab()
    {
        foreach (Row row in rows) row.rect.gameObject.SetActive(false);
        dividerRect.gameObject.SetActive(false);
        totalRow.rect.gameObject.SetActive(false);
        emptyLabel.gameObject.SetActive(false);
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
                bigger[i] = BuildRow(pixelsContent, "Row " + i, false);
            rows = bigger;
        }

        float y = 0f; // inside the scrolling list
        int visibleCount = 0;
        double overall = 0d;

        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            bool visible = i < tiers.Length && tiers[i].unlocked;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            PixelClicker.PixelTier tier = tiers[i];
            PixelUIKit.SetText(row.name, tier.displayName);
            // The Pixels tab counts CLICKS (pixels collected), not currency: multipliers only affect what the Inventory holds.
            PixelUIKit.SetText(row.amount, FormatAmount(tier.timesCollected));
            FitAmount(row);
            if (row.swatch != null) row.swatch.color = tier.UIColor;

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight;
            visibleCount++;
            overall += tier.timesCollected;
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
            PixelUIKit.SetText(totalRow.name, overallTotalLabel);
            PixelUIKit.SetText(totalRow.amount, FormatAmount(overall));
            FitAmount(totalRow);
            totalRow.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight;
        }

        float viewHeight = FixedPanelHeight - ContentTop - panelPadding;
        panelRect.sizeDelta = new Vector2(panelWidth, FixedPanelHeight);
        pixelsViewport.offsetMin = new Vector2(0f, panelPadding);
        pixelsViewport.offsetMax = new Vector2(0f, -ContentTop);
        PixelUIKit.UpdateScrollView(pixelsScroll, pixelsBar, y, viewHeight);
    }

    /// <summary>The amount box starts right after the name's text, so a long number can never run over the name.</summary>
    private static void FitAmount(Row row)
    {
        if (row.name == null || row.amount == null) return;
        float nameWidth = row.name.preferredWidth;
        row.amount.rectTransform.offsetMin = new Vector2(row.nameLeft + nameWidth + 12f, 0f);
    }

    private string FormatAmount(double value)
    {
        return PixelClicker.FormatNumber(value);
    }
}
