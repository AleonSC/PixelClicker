using System;
using System.Collections.Generic;
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

    [Tooltip("Button background colour.")]
    [SerializeField] private Color buttonColor = new Color(0.35f, 0.35f, 0.42f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    [Header("Log Tabs")]
    [Tooltip("Label of the tab that lists how many times you clicked each pixel type (renamed from 'Pixels Tab Text' so a scene saved with the old name picks up the new label).")]
    [SerializeField] private string clicksTabText = "Clicks";

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

    [Tooltip("Label of the third Log tab: progress bars for things like Ghosts Caught, the Singularity count, Stardust and the next pixel unlock.")]
    [SerializeField] private string goalsTabText = "Goals";

    [Tooltip("Text when the Goals tab has nothing to show yet.")]
    [SerializeField] private string noGoalsText = "No goals yet.";

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

    private int currentTab; // 0 = Pixels, 1 = Achievements, 2 = Goals
    private Image[] tabImages;
    private GameObject achievementsGroup, goalsGroup;
    private RectTransform achievementsContent, goalsContent;
    private TMP_Text noGoalsLabel;
    private TMP_Text achievementSummary;
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
        PixelWindows.Register(this, 20, () => panelObject != null && panelObject.activeSelf, () => panelObject.SetActive(false));
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

    public static bool LogOpen => logInstance != null && logInstance.panelObject != null && logInstance.panelObject.activeSelf;

    public static void SetLogOpen(bool open)
    {
        PixelLog log = logInstance;
        if (log == null || log.panelObject == null) return;
        log.panelObject.SetActive(open);
        if (open)
        {
            log.Refresh();
            PixelUI.SetInventoryOpen(false);
            PixelBank.CloseWindowIfOpen();
            PixelShop.CloseShopWindow();
        }
    }

    private class SkinStudio
    {
        public RenderTexture rt;
        public Camera cam;
        public Transform model;
        public GameObject root;
    }

    private readonly Dictionary<int, SkinStudio> skinStudios = new Dictionary<int, SkinStudio>();
    private readonly HashSet<int> skinRenderedThisFrame = new HashSet<int>();
    private const float SkinStudioHeight = -3500f;

    /// <summary>Live picture of a pixel type with its real look (material, glow, extras). Each type has its own far-away studio; null if it can't be made.</summary>
    private Texture SkinIcon(int tier)
    {
        if (tier < 0 || clicker == null) return null;
        SkinStudio st;
        if (skinStudios.TryGetValue(tier, out st) && st.rt != null) return st.rt;
        skinStudios.Remove(tier);

        GameObject studio = new GameObject("Achievement Icon Studio " + tier);
        studio.transform.position = new Vector3(tier * 60f, SkinStudioHeight, 0f); // apart from the other types' studios
        GameObject model = clicker.CreateDisplayPixel(tier, studio.transform, 1f);
        if (model == null) { Destroy(studio); return null; }
        model.transform.localPosition = Vector3.zero;

        st = new SkinStudio { root = studio, model = model.transform };
        st.rt = new RenderTexture(160, 160, 24, RenderTextureFormat.ARGB32) { name = "Achievement Icon " + tier };
        GameObject camGo = new GameObject("Icon Camera");
        camGo.transform.SetParent(studio.transform, false);
        st.cam = camGo.AddComponent<Camera>();
        st.cam.enabled = false;
        st.cam.clearFlags = CameraClearFlags.SolidColor;
        st.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        st.cam.orthographic = true;
        st.cam.orthographicSize = 0.805f; // a face edge fills ~62% of the picture, like the plain cube icon
        st.cam.nearClipPlane = 0.1f;
        st.cam.farClipPlane = 30f;
        st.cam.allowHDR = false;
        st.cam.targetTexture = st.rt;
        camGo.transform.localPosition = new Vector3(0f, 0f, 4.4f);
        camGo.transform.LookAt(studio.transform.position);

        GameObject lightGo = new GameObject("Icon Light");
        lightGo.transform.SetParent(studio.transform, false);
        lightGo.transform.localPosition = new Vector3(2f, 3f, 5f);
        Light icoLight = lightGo.AddComponent<Light>();
        icoLight.type = LightType.Point;
        icoLight.range = 25f;
        icoLight.intensity = 6f;
        icoLight.shadows = LightShadows.None;

        skinStudios[tier] = st;
        RenderSkin(st);
        return st.rt;
    }

    private void RenderSkin(SkinStudio st)
    {
        st.model.localRotation = Quaternion.Euler(25f, 0f, 0f) * Quaternion.Euler(0f, Time.unscaledTime * achievementIconSpin, 0f); // same pose as PixelCubeIcon
        st.cam.Render();
    }

    /// <summary>Re-renders (turning) the pictures on the goal rows that are in view.</summary>
    private void SpinVisibleGoalSkins()
    {
        if (goalRows.Count == 0 || goalsContent == null) return;
        RectTransform viewport = goalsContent.parent as RectTransform;
        if (viewport == null) return;
        Vector3[] vc = new Vector3[4], rc = new Vector3[4];
        viewport.GetWorldCorners(vc);
        skinRenderedThisFrame.Clear();
        foreach (GoalRow row in goalRows)
        {
            if (row.skin == null || !row.skin.gameObject.activeInHierarchy || row.skinTier < 0) continue;
            row.rect.GetWorldCorners(rc);
            if (rc[2].y < vc[0].y || rc[0].y > vc[2].y) continue;
            if (!skinStudios.TryGetValue(row.skinTier, out SkinStudio st) || st.rt == null) continue;
            if (skinRenderedThisFrame.Add(row.skinTier)) RenderSkin(st);
        }
    }

    /// <summary>Re-renders (turning) the pictures of the pixel types whose achievement rows are in view.</summary>
    private void SpinVisibleSkins()
    {
        if (achievementRows.Count == 0 || achievementsContent == null) return;
        RectTransform viewport = achievementsContent.parent as RectTransform;
        if (viewport == null) return;
        Vector3[] vc = new Vector3[4], rc = new Vector3[4];
        viewport.GetWorldCorners(vc);
        skinRenderedThisFrame.Clear();
        for (int i = 0; i < achievementRows.Count; i++)
        {
            AchievementRow row = achievementRows[i];
            if (row.skin == null || !row.skin.gameObject.activeInHierarchy || row.skinTier < 0) continue;
            row.rect.GetWorldCorners(rc);
            if (rc[2].y < vc[0].y || rc[0].y > vc[2].y) continue; // scrolled out of view
            SkinStudio st;
            if (!skinStudios.TryGetValue(row.skinTier, out st) || st.rt == null) continue;
            if (skinRenderedThisFrame.Add(row.skinTier)) RenderSkin(st);
        }
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<int, SkinStudio> kv in skinStudios)
        {
            if (kv.Value.rt != null) kv.Value.rt.Release();
            if (kv.Value.root != null) Destroy(kv.Value.root);
        }
        skinStudios.Clear();
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
        return "<b>" + head + "</b>\n" + body + "\nProgress: " + FormatAmount(have) + " / " + FormatAmount(target) + tier;
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

        string text = showGoal ? GoalTipText(shownGoals[hoverGoal]) : AchievementTipText(hoverAchievement);
        const float width = 620f;
        tipLabel.text = text;
        float h = Mathf.Ceil(tipLabel.GetPreferredValues(text, width - 32f, 0f).y) + 28f;
        tipRect.sizeDelta = new Vector2(width, h);

        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
        Vector2 pos = local + new Vector2(26f, -22f);
        Rect r = canvasRect.rect;
        pos.x = Mathf.Clamp(pos.x, r.xMin + 8f, r.xMax - width - 8f);
        pos.y = Mathf.Clamp(pos.y, r.yMin + h + 8f, r.yMax - 8f);
        tipRect.anchoredPosition = pos;
    }

    private bool panelWasOpen;
    private float nextRefreshTime;

    /// <summary>Shows (and adds up) "+X" next to a pixel's row for each click's payout.</summary>
    private void OnPixelCollected(int tierIndex, double amount, bool automatic)
    {
        if (!showGainDeltas || rows == null || tierIndex < 0 || tierIndex >= rows.Length) return;
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
        Button button = CreateButton(canvasRoot.transform, "Log Button", buttonText, buttonSize, buttonColor,
                                     buttonTextColor, buttonFontSize);
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = anchor;
        br.anchoredPosition = new Vector2(sx * buttonMargin.x, sy * buttonMargin.y);
        PixelHud hud = PixelHud.Ensure(gameObject);
        hud.Dock(br, anchor, () => panelObject != null && panelObject.activeSelf);
        button.onClick.AddListener(() => SetLogOpen(!panelObject.activeSelf));

        // --- Panel (sits next to the button, growing away from the screen edge)
        panelObject = new GameObject("Log Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(panelObject.GetComponent<Image>(), panelColor);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = anchor;
        panelRect.anchoredPosition = new Vector2(sx * hud.SideMargin, sy * (hud.BandThickness + gapAboveButton)); // the log's own gap beyond the bar

        // Title
        TMP_Text title = CreateText(panelObject.transform, "Title", panelTitle, titleFontSize,
                                    TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        PixelUIKit.Caps(title);
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
        BuildGoalsGroup();

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
        string[] names = { clicksTabText, achievementsTabText, goalsTabText };
        tabImages = new Image[names.Length];

        const float gap = 8f;
        float tabWidth = (panelWidth - panelPadding * 2f - gap * (names.Length - 1)) / names.Length;
        float startX = -(tabWidth + gap) * (names.Length - 1) * 0.5f;
        for (int i = 0; i < names.Length; i++)
        {
            Button tab = CreateButton(panelObject.transform, "Tab " + names[i], names[i],
                                      new Vector2(tabWidth, tabHeight), tabInactiveColor, tabTextColor, tabFontSize);
            PixelUIKit.Caps(tab);
            tabImages[i] = tab.GetComponent<Image>();

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

            int captured = i;
            tab.onClick.AddListener(() => { currentTab = captured; Refresh(); });
        }
    }

    /// <summary>Summary line + scrolling list of achievements (shown only on the Achievements tab).</summary>
    private void BuildAchievementsGroup()
    {
        BuildScrollGroup("Achievements", noAchievementsText, true, out achievementsGroup, out achievementsContent,
                         out achievementSummary, out noAchievementsLabel);
    }

    /// <summary>The scrolling list of the Goals tab (progress bars for trackers and the next pixel unlock).</summary>
    private void BuildGoalsGroup()
    {
        BuildScrollGroup("Goals", noGoalsText, false, out goalsGroup, out goalsContent, out TMP_Text unusedSummary, out noGoalsLabel);
    }

    /// <summary>A panel-sized group with an optional summary line and a scroll view + scroll bar (a tab's content).</summary>
    private void BuildScrollGroup(string groupName, string emptyMessage, bool withSummary, out GameObject group,
                                  out RectTransform contentRect, out TMP_Text summary, out TMP_Text empty)
    {
        group = new GameObject(groupName, typeof(RectTransform));
        group.transform.SetParent(panelObject.transform, false);
        RectTransform gr = group.GetComponent<RectTransform>();
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = gr.offsetMax = Vector2.zero;

        float summaryHeight = rowFontSize * 1.3f;
        summary = null;
        if (withSummary)
        {
            // A fancy progress bar: a rounded dark trough, a coloured fill with a lighter strip along its top, and the count centred over it.
            GameObject summaryBarGo = new GameObject("Summary Bar", typeof(RectTransform), typeof(Image));
            summaryBarGo.transform.SetParent(group.transform, false);
            Image trough = summaryBarGo.GetComponent<Image>();
            trough.color = new Color(0.07f, 0.07f, 0.1f, 1f);
            PixelUIKit.StyleButton(trough);
            RectTransform sr = summaryBarGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.sizeDelta = new Vector2(-panelPadding * 2f, summaryHeight);
            sr.anchoredPosition = new Vector2(0f, -ContentTop);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(summaryBarGo.transform, false);
            achievementSummaryFill = fillGo.GetComponent<Image>();
            achievementSummaryFill.color = achievementUnlockedColor;
            achievementSummaryFill.raycastTarget = false;
            achievementSummaryFillRect = fillGo.GetComponent<RectTransform>();
            achievementSummaryFillRect.anchorMin = Vector2.zero;
            achievementSummaryFillRect.anchorMax = new Vector2(0f, 1f);
            achievementSummaryFillRect.offsetMin = new Vector2(4f, 4f);
            achievementSummaryFillRect.offsetMax = new Vector2(-4f, -4f);

            GameObject shineGo = new GameObject("Shine", typeof(RectTransform), typeof(Image));
            shineGo.transform.SetParent(fillGo.transform, false);
            Image shine = shineGo.GetComponent<Image>();
            shine.color = new Color(1f, 1f, 1f, 0.28f);
            shine.raycastTarget = false;
            RectTransform shr = shineGo.GetComponent<RectTransform>();
            shr.anchorMin = new Vector2(0f, 0.5f); shr.anchorMax = Vector2.one;
            shr.offsetMin = shr.offsetMax = Vector2.zero;

            summary = CreateText(summaryBarGo.transform, "Summary", "", rowFontSize * 0.8f,
                                 TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            summary.enableAutoSizing = true;
            summary.fontSizeMax = rowFontSize * 0.8f;
            summary.fontSizeMin = 10f;
            summary.raycastTarget = false;
            PixelUIKit.Stretch(summary.rectTransform);
        }

        float viewTop = withSummary ? ContentTop + summaryHeight + 6f : ContentTop;

        // Scroll view (also the viewport; the transparent image lets empty space take wheel/drag input).
        GameObject view = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(group.transform, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        RectTransform vr = view.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        float viewHeight = FixedPanelHeight - viewTop - panelPadding;   // fills the fixed window under the tabs (and the summary line)
        vr.sizeDelta = new Vector2(-(panelPadding * 2f), viewHeight);   // exactly as wide as the tab row above: the rows line up with the tabs
        vr.anchoredPosition = new Vector2(0f, -viewTop);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(view.transform, false);
        contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;

        // Scrollbar
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(group.transform, false);
        barGo.GetComponent<Image>().color = scrollbarTrackColor;
        RectTransform br = barGo.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 1f);
        float barWidth = Mathf.Min(scrollbarWidth, 10f);                          // slim, rounded, and centred in the margin beside the rows
        br.sizeDelta = new Vector2(barWidth, viewHeight);
        br.anchoredPosition = new Vector2(-(panelPadding - barWidth) * 0.5f, -viewTop);
        PixelUIKit.StyleButton(barGo.GetComponent<Image>());

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(barGo.transform, false);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = scrollbarHandleColor;
        PixelUIKit.StyleButton(handleImage);
        RectTransform hr = handleGo.GetComponent<RectTransform>();
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = hr;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        ScrollRect scroll = view.GetComponent<ScrollRect>();
        scroll.gameObject.AddComponent<PixelScrollSound>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = achievementScrollSpeed;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        empty = CreateText(view.transform, "Empty", emptyMessage.ToUpperInvariant(), rowFontSize, // capitals, centred in the window
                                         TextAlignmentOptions.Center, FontStyles.Bold,
                                         new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform er = empty.rectTransform;
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = er.offsetMax = Vector2.zero;
        empty.gameObject.SetActive(false);

        group.SetActive(false);
    }

    /// <summary>
    /// One achievement per row, laid out like a badge: the pixel's picture on the left, a thin vertical divider, then the title with a wide
    /// progress bar under it that carries its "60 / 100" count in the middle. The row itself has no background.
    /// </summary>
    private AchievementRow BuildAchievementRow(int index)
    {
        AchievementRow row = new AchievementRow();

        GameObject go = new GameObject("Achievement " + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(achievementsContent, false);
        row.background = go.GetComponent<Image>();
        row.background.color = achievementRowColor;   // a panel per achievement, with a gap between them
        row.rect = go.GetComponent<RectTransform>();
        row.rect.anchorMin = new Vector2(0f, 1f);
        row.rect.anchorMax = new Vector2(1f, 1f);
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.sizeDelta = new Vector2(0f, achievementRowHeight);

        float iconSize = Mathf.Min(achievementIconSize, achievementRowHeight - 16f);

        // The pixel's picture (a spinning cube icon until its real look is rendered).
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(go.transform, false);
        row.icon = iconGo.AddComponent<PixelCubeIcon>();
        row.icon.spinDegreesPerSecond = achievementIconSpin;
        RectTransform ir = row.icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(iconSize, iconSize);
        ir.anchoredPosition = new Vector2(8f, 0f);

        GameObject skinGo = new GameObject("Skin", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        skinGo.transform.SetParent(go.transform, false);
        row.skin = skinGo.GetComponent<RawImage>();
        row.skin.raycastTarget = false;
        RectTransform sr = row.skin.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(iconSize, iconSize);
        sr.anchoredPosition = new Vector2(8f, 0f);
        skinGo.SetActive(false);

        // The picture sits in its own framed box.
        float boxSize = iconSize + 12f;
        GameObject frame = new GameObject("Icon Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(go.transform, false);
        frame.transform.SetSiblingIndex(0);
        frame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
        frame.GetComponent<Image>().raycastTarget = false;
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
        fr.sizeDelta = new Vector2(boxSize, boxSize);
        fr.anchoredPosition = new Vector2(8f, 0f);
        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(frame.transform, false);
        inner.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        inner.GetComponent<Image>().raycastTarget = false;
        RectTransform inr = inner.GetComponent<RectTransform>();
        inr.anchorMin = Vector2.zero; inr.anchorMax = Vector2.one;
        inr.offsetMin = new Vector2(3f, 3f); inr.offsetMax = new Vector2(-3f, -3f);
        ir.anchoredPosition = new Vector2(8f + 6f, 0f);
        sr.anchoredPosition = new Vector2(8f + 6f, 0f);
        float dividerX = 8f + boxSize - 3f;

        float left = dividerX + 3f + 16f;

        // The title, big and bold.
        row.title = CreateText(go.transform, "Title", "", rowFontSize * 1.05f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        row.title.enableAutoSizing = true;
        row.title.fontSizeMax = rowFontSize * 1.05f;
        row.title.fontSizeMin = 14f;
        RectTransform tr = row.title.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.5f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(left, 0f);
        tr.offsetMax = new Vector2(-12f, -6f);

        // The progress bar with its count in the middle.
        GameObject back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(go.transform, false);
        back.GetComponent<Image>().color = achievementBarBackColor;
        back.GetComponent<Image>().raycastTarget = false;
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0.1f);
        br.anchorMax = new Vector2(1f, 0.46f);
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

        row.progress = CreateText(back.transform, "Progress", "", rowFontSize * 0.72f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        row.progress.enableAutoSizing = true;
        row.progress.fontSizeMax = rowFontSize * 0.72f;
        row.progress.fontSizeMin = 10f;
        row.progress.raycastTarget = false;
        PixelUIKit.Stretch(row.progress.rectTransform);

        PixelHoverTip tip = go.AddComponent<PixelHoverTip>();
        tip.onEnter = () => hoverAchievement = row.achIndex;
        tip.onExit = () => { if (hoverAchievement == row.achIndex) hoverAchievement = -1; };

        // (The description is kept only as hidden storage; the tooltip says it all.)
        row.description = CreateText(go.transform, "Description", "", 12f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        row.description.gameObject.SetActive(false);

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

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, Color color,
                      Color labelColor, float labelSize)
        => PixelUIKit.CreateButton(font, parent, objectName, label, size, color, labelColor, labelSize);

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    /// <summary>Refreshes whichever tab is open and resizes the panel to fit.</summary>
    public void Refresh()
    {
        for (int i = 0; i < tabImages.Length; i++)
            tabImages[i].color = i == currentTab ? tabActiveColor : tabInactiveColor;

        bool pixelsTab = currentTab == 0;
        achievementsGroup.SetActive(currentTab == 1);
        goalsGroup.SetActive(currentTab == 2);
        pixelsViewport.gameObject.SetActive(pixelsTab);

        if (pixelsTab) RefreshPixelsTab();
        else if (currentTab == 1) RefreshAchievementsTab();
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

    // A pixel's "Collector" and "Clicker" achievements share one row; clicking the row switches which one it shows.
    private readonly List<List<int>> achievementGroups = new List<List<int>>();
    private const string ModePrefKey = "PixelLog.AchievementMode.";

    private static bool IsPixelKind(PixelAchievements.Kind k) =>
        k == PixelAchievements.Kind.CollectPixelType || k == PixelAchievements.Kind.ClickPixelType;

    private bool ShowsClicks(PixelClicker.PixelType type) => PlayerPrefs.GetInt(ModePrefKey + (int)type, 0) == 1;

    /// <summary>Groups the achievements: one group per pixel type for the two per-pixel kinds, a group of one for the rest.</summary>
    private void BuildAchievementGroups()
    {
        achievementGroups.Clear();
        int total = achievements != null ? achievements.Count : 0;
        for (int i = 0; i < total; i++) achievementGroups.Add(new List<int> { i });   // every achievement has its own row
    }

    /// <summary>The achievement a group currently shows.</summary>
    private int ChosenAchievement(List<int> group)
    {
        if (group.Count == 1) return group[0];
        PixelClicker.PixelType type = achievements.GetPixelType(group[0]);
        PixelAchievements.Kind want = ShowsClicks(type) ? PixelAchievements.Kind.ClickPixelType : PixelAchievements.Kind.CollectPixelType;
        foreach (int i in group)
            if (achievements.GetKind(i) == want) return i;
        return group[0];
    }

    private void ToggleAchievementMode(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= achievementGroups.Count || achievementGroups[groupIndex].Count < 2) return;
        PixelClicker.PixelType type = achievements.GetPixelType(achievementGroups[groupIndex][0]);
        PlayerPrefs.SetInt(ModePrefKey + (int)type, ShowsClicks(type) ? 0 : 1);
        Refresh();
    }

    private class GoalRow
    {
        public RectTransform rect;
        public TMP_Text title, progress;
        public Image barFill;
        public RectTransform barFillRect;
        public PixelCubeIcon icon;
        public RawImage skin;
        public int skinTier = -1;
        public int goalIndex;
    }

    private readonly List<GoalRow> goalRows = new List<GoalRow>();
    private readonly List<Goal> shownGoals = new List<Goal>();
    private int hoverGoal = -1;
    /// <summary>
    /// One goal per row, laid out like an achievement badge: a framed box with a live 3D picture of the goal's item on the left,
    /// the title big and bold, and a wide bar with the count in the middle. The explanation and exact numbers show on hover.
    /// </summary>
    private GoalRow BuildGoalRow(int index)
    {
        GoalRow row = new GoalRow { goalIndex = index };
        GameObject go = new GameObject("Goal " + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(goalsContent, false);
        go.GetComponent<Image>().color = achievementRowColor;
        row.rect = go.GetComponent<RectTransform>();
        row.rect.anchorMin = new Vector2(0f, 1f);
        row.rect.anchorMax = new Vector2(1f, 1f);
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.sizeDelta = new Vector2(0f, goalRowHeight);

        // The framed picture box: a live 3D picture of the item the goal is about (a spinning cube when there is none).
        float boxSize = goalRowHeight - 24f;
        GameObject frame = new GameObject("Icon Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(go.transform, false);
        frame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
        frame.GetComponent<Image>().raycastTarget = false;
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
        fr.sizeDelta = new Vector2(boxSize, boxSize);
        fr.anchoredPosition = new Vector2(10f, 0f);
        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(frame.transform, false);
        inner.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        inner.GetComponent<Image>().raycastTarget = false;
        RectTransform inr = inner.GetComponent<RectTransform>();
        inr.anchorMin = Vector2.zero; inr.anchorMax = Vector2.one;
        inr.offsetMin = new Vector2(3f, 3f); inr.offsetMax = new Vector2(-3f, -3f);

        float iconSize = boxSize - 12f;
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(inner.transform, false);
        row.icon = iconGo.AddComponent<PixelCubeIcon>();
        row.icon.spinDegreesPerSecond = achievementIconSpin;
        RectTransform icr = row.icon.rectTransform;
        icr.anchorMin = icr.anchorMax = icr.pivot = new Vector2(0.5f, 0.5f);
        icr.sizeDelta = new Vector2(iconSize, iconSize);
        icr.anchoredPosition = Vector2.zero;

        GameObject skinGo = new GameObject("Skin", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        skinGo.transform.SetParent(inner.transform, false);
        row.skin = skinGo.GetComponent<RawImage>();
        row.skin.raycastTarget = false;
        RectTransform skr = row.skin.rectTransform;
        skr.anchorMin = skr.anchorMax = skr.pivot = new Vector2(0.5f, 0.5f);
        skr.sizeDelta = new Vector2(iconSize, iconSize);
        skr.anchoredPosition = Vector2.zero;
        skinGo.SetActive(false);

        float left = 10f + boxSize + 16f;

        row.title = CreateText(go.transform, "Title", "", rowFontSize * 1.0f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        row.title.enableAutoSizing = true;
        row.title.fontSizeMax = rowFontSize * 1.0f;
        row.title.fontSizeMin = 14f;
        row.title.raycastTarget = false;
        RectTransform tr = row.title.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.5f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(left, 0f);
        tr.offsetMax = new Vector2(-12f, -6f);

        GameObject back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(go.transform, false);
        back.GetComponent<Image>().color = achievementBarBackColor;
        back.GetComponent<Image>().raycastTarget = false;
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0.12f);
        br.anchorMax = new Vector2(1f, 0.46f);
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

        row.progress = CreateText(back.transform, "Progress", "", rowFontSize * 0.72f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        row.progress.enableAutoSizing = true;
        row.progress.fontSizeMax = rowFontSize * 0.72f;
        row.progress.fontSizeMin = 10f;
        row.progress.raycastTarget = false;
        PixelUIKit.Stretch(row.progress.rectTransform);

        PixelHoverTip tip = go.AddComponent<PixelHoverTip>();
        tip.onEnter = () => hoverGoal = row.goalIndex;
        tip.onExit = () => { if (hoverGoal == row.goalIndex) hoverGoal = -1; };
        return row;
    }

    private PixelShop goalShop;

    private struct Goal
    {
        public string title, description;
        public double count, goal;
        public int skinTier;        // tier whose 3D look is the goal's picture (-1 = a spinning cube in 'iconColor')
        public Color iconColor;
    }

    /// <summary>The pixel type that stands for a minigame goal (its tracker's item).</summary>
    private int GoalSkinTier(string minigameId)
    {
        PixelClicker.PixelType type;
        switch (minigameId)
        {
            case "ghost": type = PixelClicker.PixelType.Ghost; break;
            case "blackhole": type = PixelClicker.PixelType.Singularity; break;
            case "meteor": type = PixelClicker.PixelType.Meteor; break;
            case "stardust": type = PixelClicker.PixelType.Solar; break;
            default: return -1;
        }
        return clicker.IndexOf(type);
    }

    /// <summary>The progress bars: the next pixel unlock, then every minigame tracker that is in play (ghosts caught, singularity, stardust...).</summary>
    private List<Goal> CollectGoals()
    {
        List<Goal> goals = new List<Goal>();

        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        for (int i = 1; i < tiers.Length; i++)
        {
            PixelClicker.PixelTier t = tiers[i];
            if (t.unlocked || t.unlockMode != PixelClicker.TierUnlockMode.PreviousTierThreshold || t.unlockThreshold <= 0d) continue;
            goals.Add(new Goal
            {
                title = string.Format(nextPixelGoalFormat, t.displayName),
                description = string.Format(nextPixelGoalDescription, tiers[i - 1].displayName),
                count = tiers[i - 1].totalCollected,
                goal = t.unlockThreshold,
                skinTier = i,
                iconColor = t.color,
            });
            break; // only the next one
        }

        // Intro step: once the Shop appears, buying the RGB Pack is the next thing to do (the bar fills as its price is saved up).
        if (goalShop == null) goalShop = PixelFind.First<PixelShop>();
        PixelShop shop = goalShop;
        if (shop != null && shop.ShopAvailable &&
            shop.TryGetBuyGoal(PixelClicker.PixelType.Red, out string packName, out string costText, out double have, out double need))
        {
            goals.Add(new Goal
            {
                title = string.Format(buyRgbGoalFormat, packName),
                description = string.Format(buyRgbGoalDescription, costText),
                count = have,
                goal = need,
                skinTier = clicker.IndexOf(PixelClicker.PixelType.Red),
                iconColor = new Color(1f, 0.3f, 0.3f, 1f),
            });
        }

        foreach (PixelMinigame m in PixelMinigame.All)
        {
            if (m == null || !m.HasTracker || !(m.Running || m.TrackerCount > 0d)) continue;
            goals.Add(new Goal
            {
                title = m.TrackerTitle, description = m.TrackerDescription, count = m.TrackerCount, goal = Math.Max(1d, m.TrackerGoal),
                skinTier = GoalSkinTier(m.Id), iconColor = new Color(1f, 0.85f, 0.3f, 1f),
            });
        }
        return goals;
    }

    private void RefreshGoalsTab()
    {
        HidePixelsTab();

        List<Goal> goals = CollectGoals();
        shownGoals.Clear();
        shownGoals.AddRange(goals);
        while (goalRows.Count < goals.Count) goalRows.Add(BuildGoalRow(goalRows.Count));

        float y = 0f;
        for (int i = 0; i < goalRows.Count; i++)
        {
            GoalRow row = goalRows[i];
            bool visible = i < goals.Count;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            Goal g = goals[i];
            bool done = g.count >= g.goal;
            float fraction = Mathf.Clamp01((float)(g.count / g.goal));
            PixelUIKit.SetText(row.title, g.title);
            PixelUIKit.SetText(row.progress, done ? achievementUnlockedText : string.Format(achievementProgressFormat, FormatAmount(g.count), FormatAmount(g.goal)));
            Color accent = done ? achievementUnlockedColor : achievementBarColor;
            row.title.color = done ? achievementUnlockedColor : textColor;
            row.barFill.color = accent;
            row.skinTier = g.skinTier;
            Texture skinTex = g.skinTier >= 0 ? SkinIcon(g.skinTier) : null;
            row.skin.gameObject.SetActive(skinTex != null);
            row.icon.gameObject.SetActive(skinTex == null);
            if (skinTex != null) row.skin.texture = skinTex; else row.icon.color = g.iconColor;
            row.barFillRect.anchorMax = new Vector2(fraction, 1f);

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += goalRowHeight + achievementSpacing;
        }

        goalsContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, y - achievementSpacing));
        noGoalsLabel.gameObject.SetActive(goals.Count == 0);
        panelRect.sizeDelta = new Vector2(panelWidth, FixedPanelHeight);
    }

    private void RefreshAchievementsTab()
    {
        HidePixelsTab();

        BuildAchievementGroups();
        int count = achievementGroups.Count;
        while (achievementRows.Count < count) achievementRows.Add(BuildAchievementRow(achievementRows.Count));

        float y = 0f;
        for (int i = 0; i < achievementRows.Count; i++)
        {
            AchievementRow row = achievementRows[i];
            bool visible = i < count;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            row.groupIndex = i;
            List<int> group = achievementGroups[i];
            int a = ChosenAchievement(group);
            row.achIndex = a;

            bool unlocked = achievements.IsComplete(a);           // every tier earned
            bool started = achievements.HasAnyTier(a);            // at least one tier earned
            float dim = started ? 1f : achievementLockedBrightness;

            int tierCount = achievements.GetTierCount(a);
            string tierText = tierCount > 1
                ? string.Format(achievementTierFormat, Math.Min(achievements.GetEarnedTiers(a) + 1, tierCount), tierCount) : "";
            PixelUIKit.SetText(row.title, achievements.GetTitle(a));
            PixelUIKit.SetText(row.description, achievements.GetDescription(a) + tierText);
            row.title.color = unlocked ? achievementUnlockedColor : new Color(textColor.r * dim, textColor.g * dim, textColor.b * dim, textColor.a);

            double currentTarget = achievements.GetCurrentTarget(a);
            PixelUIKit.SetText(row.progress, unlocked
                ? achievementUnlockedText
                : string.Format(achievementProgressFormat, FormatAmount(Math.Min(achievements.GetProgress(a), currentTarget)), FormatAmount(currentTarget)));
            row.progress.color = unlocked ? achievementUnlockedColor : amountColor;

            Color icon = achievements.GetIconColor(a);
            row.icon.color = new Color(icon.r * dim, icon.g * dim, icon.b * dim, icon.a);
            row.skinTier = achievements.GetSkinTier(a);
            Texture skinTex = SkinIcon(row.skinTier);
            row.skin.gameObject.SetActive(skinTex != null);
            row.icon.gameObject.SetActive(skinTex == null);
            if (skinTex != null) { row.skin.texture = skinTex; float skinDim = Mathf.Lerp(1f, dim, 0.35f); row.skin.color = new Color(skinDim, skinDim, skinDim, 1f); }

            row.barFill.color = unlocked ? achievementUnlockedColor : achievementBarColor;
            row.barFillRect.anchorMax = new Vector2(unlocked ? 1f : achievements.GetFraction(a), 1f);

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += achievementRowHeight + achievementSpacing;
        }

        achievementsContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, y - achievementSpacing));
        noAchievementsLabel.gameObject.SetActive(count == 0);
        int earnedTotal = achievements != null ? achievements.EarnedTierTotal : 0, tierTotal = achievements != null ? achievements.TierTotal : 0;
        PixelUIKit.SetText(achievementSummary, string.Format(achievementSummaryFormat, earnedTotal, tierTotal));
        if (achievementSummaryFillRect != null) achievementSummaryFillRect.anchorMax = new Vector2(tierTotal > 0 ? Mathf.Clamp01(earnedTotal / (float)tierTotal) : 0f, 1f);

        float summaryHeight = rowFontSize * 1.3f;
        panelRect.sizeDelta = new Vector2(panelWidth, FixedPanelHeight);
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
