using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple HUD for Pixel Clicker. An "Inventory" box with a Currency tab (how many pixels of each tier you have)
/// and a Consumables tab (potions you own - right-click one to drink it).
///
/// Two ways to use it:
///  A) MANUAL: create TextMeshPro texts in your own Canvas and drag them into "Tier Labels"
///     (element 0 = first tier, element 1 = second tier, ...).
///  B) AUTOMATIC: leave "Tier Labels" empty and keep "Auto Create Labels" on. The script builds
///     its own canvas, panel and one text per tier when the game starts.
///
/// It only reads PixelClicker.Tiers and refreshes every frame, so there is nothing to wire up
/// besides the PixelClicker reference (found automatically if left empty).
///
/// The Inventory box is a standard Fixed window (PixelUIKit.CreateWindow: title, grey X, pin button in the left header slot), kept at its
/// HUD corner instead of the centre. The pinned (compact) mode is its own widget: it hides the title / X / tabs and keeps its own drag,
/// minimise and padlock buttons (PixelPopoutDrag), which are not part of the standard window.
///
/// Part files (partial class): PixelUIPopups.cs (popups + tooltips), PixelUIRefresh.cs (the Currency rows / Refresh), PixelUICompact.cs
/// (the pinned Inventory), PixelUIInventoryTabs.cs (tabs, Consumables / Materials lists), PixelUIGuide.cs (tier guide + potion timer).
/// </summary>
public partial class PixelUI : MonoBehaviour
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

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------
    // Automatic mode
    // ------------------------------------------------------------------

    [Header("Performance")]
    [Tooltip("Seconds between refreshes of the open Inventory box (opening, tab changes and purchases refresh at once).")]
    [SerializeField] private float refreshSeconds = 0.2f;

    [Header("Automatic Mode")]
    [Tooltip("Build a canvas + one text per tier at startup when 'Tier Labels' is empty.")]
    [SerializeField] private bool autoCreateLabels = true;

    [Tooltip("Which screen corner the auto-created panel sits in.")]
    [SerializeField] private PanelCorner corner = PanelCorner.TopLeft;

    [Tooltip("Distance of the panel from the screen edge, in pixels.")]
    [SerializeField] private Vector2 margin = new Vector2(30f, 30f);

    [Tooltip("Width of the panel.")]
    [SerializeField] private float panelWidth = 520f;

    [Min(200f)]
    [Tooltip("Tallest the inventory box can get (canvas units). A longer list scrolls (mouse wheel or the scroll bar).")]
    [SerializeField] private float maxPanelHeight = 640f;


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

    [Header("Inventory Box (automatic mode)")]
    [Tooltip("Text on the button that opens/closes the inventory box.")]
    [SerializeField] private string inventoryButtonText = "Inventory";

    [Tooltip("Size of the button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(240f, 80f);

    [Tooltip("Button text size.")]
    [SerializeField] private float buttonFontSize = 38f;

    [Tooltip("The HUD Inventory button's own colour (each HUD corner button has an identity colour of its own).")]
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.6f, 0.4f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Tooltip("Title shown at the top of the box.")]
    [SerializeField] private string inventoryTitle = "Inventory";

    [Tooltip("Show the button text in capital letters (CURRENCY).")]
    [SerializeField] private bool uppercaseButton = true;

    /// <summary>Height of the standard window header above the tabs (PixelUIKitWindows.cs).</summary>
    private float headerHeight => windowParts != null ? windowParts.HeaderHeight : PixelUIKit.WindowHeaderHeight;

    /// <summary>Vertical distance between lines. Never smaller than the text, so lines can't overlap.</summary>
    private float LinePitch => Mathf.Max(lineHeight, fontSize * 1.3f);

    [Tooltip("Start with the box open.")]
    [SerializeField] private bool startOpen = false;

    [Header("Inventory Tabs")]
    [Tooltip("Label of the tab that lists your pixel amounts.")]
    [SerializeField] private string currencyTabText = "Currency";

    [Tooltip("Label of the tab that lists your potions.")]
    [SerializeField] private string consumablesTabText = "Consumables";

    [Tooltip("Label of the Inventory's third tab (materials from minigames: stardust, meteor chunks, bomb parts, ectoplasm).")]
    [SerializeField] private string materialsTabText = "Materials";

    [Tooltip("Shown (in capitals, centred) in the Materials tab while you hold none.")]
    [SerializeField] private string noMaterialsText = "No materials yet.";

    [Tooltip("Height of the two tab buttons inside the box.")]
    [SerializeField] private float subTabHeight = 56f;

    [Tooltip("Tab text size.")]
    [SerializeField] private float subTabFontSize = 28f;

    [Tooltip("Tab text colour.")]
    [SerializeField] private Color subTabTextColor = Color.white;

    [Tooltip("Background colour of the consumables drop-down and search box (the tabs themselves use the Neutral / Selected button roles).")]
    [SerializeField] private Color subTabInactiveColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Tooltip("Gap under the tab buttons.")]
    [SerializeField] private float subTabGap = 10f;

    [Header("Consumables Tab")]
    [Tooltip("The potions. Found automatically in the scene if left empty.")]
    [SerializeField] private PixelConsumables consumables;

    [Tooltip("Colour of the \"No potions / devices yet\" message in the Consumables tab (bright, so it is easy to read).")]
    [SerializeField] private Color emptyMessageColor = new Color(0.92f, 0.94f, 1f, 1f);

    [Tooltip("Shown when you own no potions.")]
    [SerializeField] private string noConsumablesText = "No potions yet.";

    [Tooltip("The kinds of consumable in the drop-down at the top of the Consumables tab. The first is potions, the second devices (fan, magnet, vacuum device...). Add names here when new kinds exist.")]
    [SerializeField] private string[] consumableCategories = { "Potions", "Utilities" };

    [Min(20f)]
    [Tooltip("Height of that drop-down.")]
    [SerializeField] private float categoryDropdownHeight = 52f;

    [Tooltip("Placeholder of the search box beside the category drop-down on the Consumables tab.")]
    [SerializeField] private string consumableSearchPlaceholder = "Search...";

    [Tooltip("Shown on the Devices sub-tab when you own no devices.")]
    [SerializeField] private string noDevicesText = "No devices yet.";

    [Tooltip("Shown on the Seeds category when you own none.")]
    [SerializeField] private string noSeedsText = "No seeds yet.";

    [Tooltip("Hover hint on a seed.")]
    [SerializeField] private string seedHint = "Right-click a seed, then click the floor to plant it";

    [Tooltip("Hint at the top of the Potions list.")]
    [SerializeField] private string potionHint = "Right-click a potion to drink it";

    [Tooltip("Hint at the top of the Devices list.")]
    [SerializeField] private string noSuppliesText = "No supplies yet.";

    [Tooltip("Hover tip on a supply item (Combo Fuel, Ghost Bait, Pet Treat).")]
    [SerializeField] private string supplyHint = "Right-click a supply to use it";

    [Tooltip("Hint in the Inventory's device list.")]
    [SerializeField] private string deviceHint = "Right-click a device to place it";

    [Tooltip("Hint text size.")]
    [SerializeField] private float hintFontSize = 26f;

    [Tooltip("Hint text colour (bright, so it can be read).")]
    [SerializeField] private Color hintColor = new Color(1f, 0.93f, 0.55f, 1f);

    [Tooltip("Line shown at the top of the tab while a potion is active. {0} = name, {1} = seconds left.")]
    [SerializeField] private string activePotionFormat = "Active: {0}  {1}s";

    [Tooltip("Colour of a potion line while the mouse is over it.")]
    [SerializeField] private Color potionHoverColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("Tier Guide (top of the screen)")]
    [Tooltip("Show a hint telling the player what to do to reach the next tier.")]
    [SerializeField] private bool showTierGuide = true;

    [Min(2)]
    [Tooltip("How many of the first tiers get the guide. 3 = the White, Gray and Black phases.")]
    [SerializeField] private int guideTierCount = 3;

    [Tooltip("Hint while the next tier unlocks by collecting. {0} = pixels still needed, {1} = pixel type you need (White, Gray...).")]
    [SerializeField] private string guideFormat = "Click {0} more {1} pixels to progress to the next tier.";
    [Tooltip("The guide line while an early GOAL drives the game (PixelGoals: the goals ARE the unlocks). {0} = the goal's title, {1} = progress, {2} = target, {3} = its reward. Empty = '{0}  ({1} / {2})  -  {3}'.")]
    [SerializeField] private string goalGuideFormat = "";

    [Tooltip("Hint once the last guided tier is unlocked and the next one comes from the shop.")]
    [SerializeField] private string shopGuideText = "Open the Shop - Cubie has a few things to show you.";

    [Tooltip("Hint text size.")]
    [SerializeField] private float guideTextSize = 24f;

    [Tooltip("Hint text colour.")]
    [SerializeField] private Color guideColor = new Color(1f, 1f, 1f, 0.9f);

    [Range(0f, 1f)]
    [Tooltip("Opacity of the black box behind the guide text. 0 = no box, 1 = solid black.")]
    [SerializeField] private float guideBackgroundOpacity = 0.6f;

    [Tooltip("Colour of the box behind the guide text (its alpha is ignored - use Guide Background Opacity).")]
    [SerializeField] private Color guideBackgroundColor = Color.black;

    [Tooltip("Space between the guide text and the edge of its box (x = left/right, y = top/bottom).")]
    [SerializeField] private Vector2 guideBoxPadding = new Vector2(22f, 8f);

    [Tooltip("Sorting order of the guide's canvas. Keep it BELOW the shop (150) so the shop covers the guide.")]
    [SerializeField] private int guideSortingOrder = 50;

    [Tooltip("Distance of the hint from the top of the screen (canvas units). Keep it below the potion timer.")]
    [SerializeField] private float guideTopMargin = 110f;

    [Tooltip("Message shown when a new tier unlocks. {0} = pixel type (Gray, Black...).")]
    [SerializeField] private string unlockFormat = "{0} pixel unlocked";

    [Tooltip("How long the unlock message stays (seconds). It fades out at the end.")]
    [SerializeField] private float unlockSeconds = 4f;

    [Tooltip("Unlock message text size.")]
    [SerializeField] private float unlockFontSize = 52f;

    [Tooltip("Also show the unlock message for tiers bought in the shop (Red, Green, Blue, Glass...).")]
    [SerializeField] private bool announceShopUnlocks = false;

    [Header("Active Potion Timer (on screen)")]
    [Tooltip("Show the running potion and its time left at the top of the screen.")]
    [SerializeField] private bool showActivePotionHud = true;

    [Tooltip("Timer text. {0} = potion name, {1} = seconds left.")]
    [SerializeField] private string activeHudFormat = "{0}  {1}s";

    [Tooltip("Message shown while you are placing a device (a device can have its own message). {0} = device name.")]
    [SerializeField] private string placingHudFormat = "Click the floor to place the {0}  (right-click to cancel)";

    [Tooltip("Timer text size.")]
    [SerializeField] private float activeHudFontSize = 44f;


    [Tooltip("Use the potion's pixel colour for the timer. Off = white.")]
    [SerializeField] private bool activeHudUsesTierColor = true;

    [Header("Vacuum Indicator (+X next to each entry)")]
    [Tooltip("After a Vacuum pixel is clicked, show a '+X' next to each entry that gained currency.")]
    [SerializeField] private bool showVacuumDeltas = true;

    [Tooltip("Indicator text. {0} = amount gained.")]
    [SerializeField] private string deltaFormat = "+{0}";

    [Tooltip("Also show a '+X' next to a pixel's entry for every click you or the auto clicker make. Clicks in quick succession add up. Same format, colour and fade as the vacuum indicator.")]
    [SerializeField] private bool showGainDeltas = true;

    [Tooltip("Indicator colour.")]
    [SerializeField] private Color deltaColor = new Color(0.45f, 1f, 0.5f, 1f);

    [Tooltip("Indicator text size relative to the entry text.")]
    [SerializeField] private float deltaFontScale = 0.85f;

    [Tooltip("Seconds the indicator stays (it fades out over this time).")]
    [SerializeField] private float deltaDuration = 1.6f;

    [Tooltip("How far (canvas units) the indicator drifts upward while fading.")]
    [SerializeField] private float deltaRise = 10f;

    // ------------------------------------------------------------------
    // Text
    // ------------------------------------------------------------------

    [Header("Text")]
    [Tooltip("Text size for auto-created texts.")]
    [SerializeField] private float fontSize = 40f;

    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Tooltip("Outline colour (used for auto-created texts).")]
    [SerializeField] private Color outlineColor = Color.black;

    [Range(0f, 1f)]
    [Tooltip("Outline thickness for auto-created texts. 0 = none.")]
    [SerializeField] private float outlineWidth = 0f;

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

    [Tooltip("Popups from the AUTO CLICKER appear over the cube instead of at the cursor.")]
    [SerializeField] private bool autoPopupsOverCube = true;

    [Tooltip("Auto-click popups keep following the cube as it bobs and spins.")]
    [SerializeField] private bool cubePopupFollowsCube = true;

    [Tooltip("Offset from the cube's screen position where auto-click popups start (canvas units).")]
    [SerializeField] private Vector2 cubePopupStartOffset = new Vector2(0f, 110f);

    [Tooltip("Show a hit counter (e.g. 2/5) when a click only damages a tough pixel such as Obsidian.")]
    [SerializeField] private bool showHitPopups = true;

    [Tooltip("Hit popup text. {0} = hits so far, {1} = hits needed.")]
    [SerializeField] private string hitPopupFormat = "{0}/{1}";

    [Tooltip("Hit popup size relative to a normal popup.")]
    [SerializeField] private float hitPopupSize = 0.75f;

    [Min(0f)]
    [Tooltip("Tough pixels: the last click first shows the full counter (e.g. 5/5), then the payout (+7) pops up after this many seconds. 0 = both at once.")]
    [SerializeField] private float toughPayoutDelay = 0.3f;

    [Tooltip("Show a '+X' popup over the cube when a Vacuum pixel sucks up husks.")]
    [SerializeField] private bool showVacuumPopup = true;

    [Tooltip("Vacuum popup text. {0} = total amount collected from the husks.")]
    [SerializeField] private string vacuumPopupFormat = "+{0}";

    [Tooltip("Vacuum popup size relative to a normal popup.")]
    [SerializeField] private float vacuumPopupSize = 1.5f;

    [Tooltip("Extra offset added to the cube popup position for the vacuum popup (so it doesn't cover the normal +1).")]
    [SerializeField] private Vector2 vacuumPopupExtraOffset = new Vector2(0f, 70f);

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

    [Tooltip("Sorting order of the popup canvas. Above the inventory panel (100) but BELOW the Log (140) and Shop (150) windows, so popups never draw over them.")]
    [SerializeField] private int popupCanvasOrder = 120;

    [Tooltip("Most '+N' popups on screen at once. The labels are made once and reused; when all are in use the oldest one is taken for the new popup (fast auto clicking).")]
    [Min(1)]
    [SerializeField] private int maxPopups = 24;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private GameObject autoRoot;
    private GameObject boxObject;
    private RectTransform boxRect;
    private RectTransform listViewport;
    private RectTransform listContent;
    private ScrollRect listScroll;
    private GameObject listBarObject;
    private bool autoMode;
    private TMP_Text titleLabel;
    private PixelWindowParts windowParts;   // the standard window the box is built on (PixelUIKit.CreateWindow)
    private TMP_Text[] deltaLabels;
    private float[] deltaTimers;
    private double[] gainSums;
    private TMP_Text buttonLabel;
    private bool built;
    private RectTransform popupCanvasRect;
    private int inventoryTab; // 0 = Currency, 1 = Consumables, 2 = Materials
    private Button compactButton;
    private GameObject closeButtonObject;
    private readonly System.Collections.Generic.List<GameObject> tabObjects = new System.Collections.Generic.List<GameObject>();

    [Header("Compact (pinned) Inventory")]
    [Tooltip("Width of the simplified Inventory (0 = just wide enough for its widest line).")]
    [SerializeField] private float compactWidth = 0f;

    private TMP_Text measureLabel;
    private bool marginsCompact;

    /// <summary>The pinned window's width: just wide enough for the widest line (swatch + the text at full size) with a little room, never wider than the full window.</summary>
    private float FitCompactWidth()
    {
        if (compactWidth > 0f) return compactWidth;   // a width typed in the Inspector wins
        if (measureLabel == null)
        {
            measureLabel = MakeText(boxObject.transform, "Measure", "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Color.clear);
            measureLabel.raycastTarget = false;
            measureLabel.enableAutoSizing = false;
#if UNITY_2023_1_OR_NEWER
            measureLabel.textWrappingMode = TextWrappingModes.NoWrap;
#else
            measureLabel.enableWordWrapping = false;
#endif
            measureLabel.gameObject.SetActive(false);
        }
        measureLabel.fontSize = fontSize;
        float widest = 0f;
        void Consider(TMP_Text label)
        {
            if (label == null || !label.gameObject.activeSelf) return;
            measureLabel.text = label.text;
            widest = Mathf.Max(widest, measureLabel.GetPreferredValues(label.text).x);
        }
        Consider(ultraLabel);
        if (tierLabels != null) foreach (TMP_Text l in tierLabels) Consider(l);
        if (potionLabels != null) foreach (TMP_Text l in potionLabels) Consider(l);
        float swatchSize = Mathf.Round(LinePitch * 0.8f);
        float width = swatchSize + 18f + widest + 14f + panelPadding * 2f;
        float buttons = CompactToggleSize * 2f + 8f + CompactToggleSize * 0.5f + 12f + panelPadding * 2f;   // the two buttons on top (pin switch + currency / potions) always fit side by side
        return Mathf.Clamp(Mathf.Max(width, buttons), 150f, Mathf.Max(panelWidth, buttons));
    }

    /// <summary>The right margin of a row's text leaves room for the amount in the full window; the pinned one has no amount column, so the margin shrinks.</summary>
    private void ApplyRowMargins(bool compact)
    {
        if (compact == marginsCompact) return;
        marginsCompact = compact;
        float swatchSize = Mathf.Round(LinePitch * 0.8f);
        foreach (var pair in rowParts)
            if (pair.Key != null) pair.Key.margin = new Vector4(swatchSize + 18f, 0f, compact ? 8f : 110f, 0f);
    }
    private float CompactToggleSize => PixelUIKit.WindowCloseSize;   // the pin is a standard header button (same size as the X)
    private float CompactTop => CompactToggleSize + panelPadding * 1.5f;
    private readonly System.Collections.Generic.List<TMP_Text> materialLabels = new System.Collections.Generic.List<TMP_Text>();
    private TMP_Text noMaterialsLabel;
    private int consumableSubTab = 1; // inside Consumables: 0 = Potions, 1 = Devices (the default)
    private TMP_Dropdown categoryDropdown;
    private Image[] subTabImages;
    private TMP_Text activeLabel;
    private TMP_Text noConsumablesLabel;
    private TMP_Text hintLabel;
    private TMP_Text[] potionLabels;
    private GameObject[] potionRowObjects;
    private TMP_Text hudLabel;
    private TMP_Text guideLabel;
    private GameObject guideRoot;

    /// <summary>The tier guide box (the Shop nudge draws its arrows from here); null before it is built.</summary>
    public RectTransform GuideRect => guideRoot != null && guideRoot.activeInHierarchy ? guideRoot.GetComponent<RectTransform>() : null;
    private Image guideBackground;
    private GameObject guideCanvasRoot;
    private float unlockTimer;
    private string unlockMessage;
    private Color unlockColor = Color.white;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    // Start (not Awake) so tiers appended by PixelShop during Awake are included.
    private void Start()
    {
        if (shopGuideText == "Buy the RGB Pack in the shop to progress to the next tier.") shopGuideText = "Open the Shop - Cubie has a few things to show you.";   // (RGB is Cubie's unlock now, not a purchase)
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }

        if (clicker == null)
        {
            Debug.LogError("PixelUI: no PixelClicker found in the scene. Add PixelClicker to your cube first, " +
                           "or drag it into the 'Clicker' field.", this);
            enabled = false;
            return;
        }

        // One shared font for the whole game (set on PixelClicker).
        if (clicker.UIFont != null)
        {
            font = clicker.UIFont;
            if (tierLabels != null)
                foreach (TMP_Text label in tierLabels)
                    if (label != null) label.font = font;
        }

        // Use the same potions the shop sells into, so purchases always show up here.
        PixelShop shop = PixelFind.First<PixelShop>();
        if (shop != null && shop.Consumables != null) consumables = shop.Consumables;
        if (consumables == null)
        {
            consumables = PixelFind.First<PixelConsumables>();
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
            autoMode = true; // before building: the list and its offsets depend on it
            BuildAutomaticUI();
        }

        BuildPopupCanvas();
        BuildActiveHud();
        BuildGuide();
        if (clicker.onTierUnlocked != null) clicker.onTierUnlocked.AddListener(OnTierUnlocked);
        clicker.PixelCollected += OnPixelCollected;
        clicker.PixelHit += OnPixelHit;
        clicker.PixelFinalHit += OnPixelFinalHit;
        clicker.PixelsVacuumed += OnPixelsVacuumed;
        clicker.VacuumBreakdown += OnVacuumBreakdown;

        built = true;
    }

    private static PixelUI inventoryInstance;

    /// <summary>Is the Inventory box open?</summary>
    public static bool InventoryOpen => inventoryInstance != null && inventoryInstance.boxObject != null && PixelPop.IsOpen(inventoryInstance.boxObject);

    /// <summary>Opens or closes the Inventory box. Opening it closes the Log (they never show together).</summary>
    /// <summary>The Inventory window's rectangle (null if there is none); used to place tip boxes next to it.</summary>
    /// <summary>Shows one of the Inventory's tabs (0 Currency, 1 Consumables, 2 Materials), e.g. for a tip about what is in it.</summary>
    public static void ShowInventoryTab(int tab)
    {
        if (inventoryInstance == null) return;
        inventoryInstance.inventoryTab = Mathf.Clamp(tab, 0, 2);
        if (inventoryInstance.listContent != null) inventoryInstance.listContent.anchoredPosition = Vector2.zero;
        inventoryInstance.Refresh();
    }

    public static RectTransform WindowRect => inventoryInstance != null && inventoryInstance.boxObject != null ? inventoryInstance.boxObject.GetComponent<RectTransform>() : null;

    public static void SetInventoryOpen(bool open, bool force = false)
    {
        PixelUI ui = inventoryInstance;
        if (ui == null || ui.boxObject == null) return;
        if (!open && Compact && !force) return;   // the pinned Inventory is put away by Update (UpdatePinnedHierarchy) when other windows open, not here
        if (open)
        {
            // Lay the window out for the CURRENT mode before it shows (the intro switches the pinned view off right before opening it: without this
            // the pinned layout / spot showed for one frame).
            ui.UpdatePinnedPlacement();
            ui.Refresh();
        }
        if (open) PixelPop.Show(ui.boxObject); else PixelPop.Hide(ui.boxObject);
        if (open)
        {
            ui.Refresh();
            PixelLog.SetLogOpen(false);
            PixelBank.CloseWindowIfOpen();
            PixelSkills.CloseWindowIfOpen();
            PixelShop.CloseShopWindow();
        }
    }

    private void OnDestroy()
    {
        if (inventoryInstance == this) inventoryInstance = null;
        PixelWindows.Unregister(this);
        if (autoRoot != null) Destroy(autoRoot);
        if (popupRoot != null) Destroy(popupRoot);
        if (guideCanvasRoot != null) Destroy(guideCanvasRoot);
        if (clicker != null)
        {
            clicker.PixelCollected -= OnPixelCollected;
            clicker.PixelHit -= OnPixelHit;
            clicker.PixelFinalHit -= OnPixelFinalHit;
            if (clicker.onTierUnlocked != null) clicker.onTierUnlocked.RemoveListener(OnTierUnlocked);
            clicker.PixelsVacuumed -= OnPixelsVacuumed;
            clicker.VacuumBreakdown -= OnVacuumBreakdown;
        }
    }

    private float pinnedFade = 1f;

    /// <summary>
    /// The pinned Inventory obeys the same window hierarchy as the normal one: any registered window below priority 100 (Shop, Log, Bank, Skills,
    /// Crafting, Toggles, the UFO window...) puts it away, and it comes back by itself once they are all closed. (It still ignores Escape.)
    /// </summary>
    private void UpdatePinnedHierarchy()
    {
        if (!autoMode || boxObject == null) return;
        CanvasGroup group = boxObject.GetComponent<CanvasGroup>();
        if (group == null) group = boxObject.AddComponent<CanvasGroup>();
        // Fades away while another window is open and comes straight back when they are all closed (same timing as the guide text).
        // The pinned window no longer fades away: it is a movable window that simply sits beneath the others (its canvas sorts below theirs).
        pinnedFade = Mathf.MoveTowards(pinnedFade, 1f, Time.unscaledDeltaTime / 0.06f);
        group.alpha = pinnedFade;
        group.blocksRaycasts = pinnedFade > 0.5f;
        group.interactable = pinnedFade > 0.5f;
    }

    private void Update()
    {
        if (!built) return;
        TickPopups();
        UpdatePinnedHierarchy();
        UpdatePinnedPlacement();
        TickDeltas();
        UpdateTooltip();
        UpdateActiveHud();
        UpdateGuide();
        UpdatePinnedSpaceSwitch();
        if (autoMode && !boxObject.activeSelf) { boxWasOpen = false; return; } // closed box: nothing to update
        if (boxWasOpen && Time.unscaledTime < nextRefreshTime) return;
        boxWasOpen = true;
        nextRefreshTime = Time.unscaledTime + refreshSeconds;
        Refresh();
    }

    /// <summary>Space flips the pinned Inventory between the currency and the consumables without closing it (Escape is untouched). Not in first person (Space jumps there) or while paused.</summary>
    private void UpdatePinnedSpaceSwitch()
    {
        if (!autoMode || !Compact || boxObject == null || !boxObject.activeSelf || compactSwitch == null) return;
        if (PixelCalm.InMenu || PixelFirstPerson.Active) return;
        if (!PixelKeys.KeyPressed(KeyCode.Space)) return;   // (ignored while typing in a text box)
        compactSwitch.onClick.Invoke();
    }

    private bool boxWasOpen;
    private float nextRefreshTime;

    [Header("Ultra Tooltip")]
    [Tooltip("Tooltip shown when hovering a pixel's entry. {0} = pixel name, {1} = how many Ultra versions you own.")]
    [SerializeField] private string ultraRowTip = "Ultra pixels - earned from minigames (click to switch between the name and the number)";

    [Tooltip("Tooltip text size.")]
    [SerializeField] private float tooltipFontSize = 30f;

    [Tooltip("Tooltip background colour.")]
    [SerializeField] private Color tooltipColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);

    [Tooltip("Tooltip text colour.")]
    [SerializeField] private Color tooltipTextColor = new Color(1f, 0.88f, 0.4f, 1f);

    [Tooltip("Widest the Inventory's item tooltip gets before its text wraps.")]
    [SerializeField] private float itemTipMaxWidth = 460f;

    // ------------------------------------------------------------------
    // Automatic UI
    // ------------------------------------------------------------------

    private TMP_Text MakeText(Transform parent, string objectName, string text, float size,
                        TextAlignmentOptions alignment, FontStyles style, Color color)
        => PixelUIKit.CreateText(font, parent, objectName, text, size, alignment, style, color);

    private Button MakeButton(Transform parent, string objectName, string label, Vector2 size, Color color,
                      Color labelColor, float labelSize)
        => PixelUIKit.CreateButton(font, parent, objectName, label, size, color, labelColor, labelSize);

    private void BuildAutomaticUI()
    {
        if (TMP_Settings.instance == null && font == null)
        {
            Debug.LogError("PixelUI: TextMeshPro Essential Resources are missing. " +
                           "Use Window > TextMeshPro > Import TMP Essential Resources, then press Play again.", this);
        }

        int count = clicker.Tiers.Length;

        // Canvas
        autoRoot = PixelUIKit.CreateCanvas("PixelUI Canvas", sortingOrder, referenceResolution, true);
        PixelUIKit.EnsureEventSystem();

        Vector2 anchor = new Vector2(
            corner == PanelCorner.TopRight || corner == PanelCorner.BottomRight ? 1f : 0f,
            corner == PanelCorner.TopLeft || corner == PanelCorner.TopRight ? 1f : 0f);
        float sx = anchor.x > 0.5f ? -1f : 1f;
        float sy = anchor.y > 0.5f ? -1f : 1f;

        // --- Inventory button
        Button button = MakeButton(autoRoot.transform, "Inventory Button", inventoryButtonText, buttonSize, buttonColor,
                                   buttonTextColor, buttonFontSize);   // (role Custom: the HUD button's own identity colour)
        buttonLabel = button.GetComponentInChildren<TMP_Text>();
        if (uppercaseButton) buttonLabel.fontStyle |= FontStyles.UpperCase;
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = anchor;
        br.anchoredPosition = new Vector2(sx * margin.x, sy * margin.y);
        PixelHud hud = PixelHud.Ensure(gameObject);
        hud.Dock(br, anchor, () => boxObject != null && boxObject.activeSelf); // standard size, slides out near the mouse
        button.onClick.AddListener(() => SetInventoryOpen(!PixelPop.IsOpen(boxObject), true));
        PixelMenuKeys.Register(PixelAction.Inventory, button);

        // --- Box: the standard Fixed window (title, grey X), kept next to the button, growing away from the screen edge
        Color boxColor = showBackground ? new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, Mathf.Max(backgroundColor.a, 0.95f))   // nearly solid: the stars must not show through the text
                                        : new Color(0f, 0f, 0f, 0f);
        windowParts = PixelUIKit.CreateWindow(font, autoRoot.transform, "Inventory Box", inventoryTitle, new Vector2(panelWidth, 400f), boxColor,
                                              PixelUIKit.WindowKind.Fixed, () => PixelPop.Hide(boxObject), styled: showBackground);
        boxObject = windowParts.Root;
        boxObject.GetComponent<Image>().raycastTarget = true; // clicks on the box shouldn't reach the pixel behind it
        boxRect = windowParts.Rect;
        // Fixed windows are centred by the builder: put it back at its HUD corner.
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = anchor;
        boxRect.sizeDelta = new Vector2(panelWidth, headerHeight + panelPadding * 2f + LinePitch * count);
        boxRect.anchoredPosition = new Vector2(sx * hud.SideShownInset, sy * hud.WindowOffset); // the same gap from the black margin as the docked button keeps
        titleLabel = windowParts.Title;
        closeButtonObject = windowParts.Close.gameObject;
        PixelMenuFx.AddWindowLine(boxObject.transform, headerHeight - 4f, panelPadding);

        BuildInventoryTabs(anchor);
        BuildCompactToggle();
        BuildListViewport();

        // One text per tier (positions are set in Refresh so hidden tiers leave no gaps).
        tierLabels = new TMP_Text[count];
        deltaLabels = new TMP_Text[count];
        deltaTimers = new float[count];
        gainSums = new double[count];
        for (int i = 0; i < count; i++)
        {
            TMP_Text tmp = MakeText(ListParent, "Tier " + i, "", fontSize,
                                    anchor.x > 0.5f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft,
                                    FontStyles.Normal, textColor);
            RectTransform rt = tmp.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-panelPadding * 2f, LinePitch);

            // Huge numbers shrink the line to fit on one row instead of wrapping onto the next one.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = fontSize;
            tmp.fontSizeMin = Mathf.Max(8f, fontSize * 0.3f);
            tmp.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
#else
            tmp.enableWordWrapping = false;
#endif

            if (outlineWidth > 0f)
            {
                tmp.outlineColor = outlineColor;
                tmp.outlineWidth = outlineWidth;
            }

            tierLabels[i] = tmp;

            // Hovering the entry shows its Ultra count (see the Ultra Pad minigame).
            tmp.raycastTarget = true;
            PixelHoverTip tip = tmp.gameObject.AddComponent<PixelHoverTip>();
            int tierForTip = i;
            tmp.gameObject.AddComponent<PixelClickable>().onClick = () => { CurrencyShowNames = !CurrencyShowNames; Refresh(); };   // click a row: name <-> number
            tip.onEnter = () => ShowTooltip(tierForTip);
            tip.onExit = HideTooltip;

            // "+X" indicator that fills the entry's row, aligned to the opposite side of the text.
            TMP_Text delta = MakeText(tmp.transform, "Delta", "", fontSize * deltaFontScale,
                                      anchor.x > 0.5f ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                                      FontStyles.Bold, deltaColor);
            RectTransform dr = delta.rectTransform;
            dr.anchorMin = Vector2.zero;
            dr.anchorMax = Vector2.one;
            dr.offsetMin = dr.offsetMax = Vector2.zero;
            delta.gameObject.SetActive(false);
            deltaLabels[i] = delta;
        }

        BuildUltraRow(anchor);

        boxObject.SetActive(startOpen);
        BuildConsumableList(anchor);
        PixelWindows.Register(this, 10, () => boxObject != null && PixelPop.IsOpen(boxObject) && !Compact, () => PixelPop.Hide(boxObject));   // a pinned one ignores Escape
        inventoryInstance = this;
        PixelDebug.Info("PixelUI: created the Inventory box with " + count + " currency lines.", this);
    }

    private TMP_Text ultraLabel;
    private Image ultraDivider;

    /// <summary>The Ultra pixel row (an animated swatch + the number) and the thin line under it; both start hidden.</summary>
    private void BuildUltraRow(Vector2 anchor)
    {
        ultraLabel = MakeText(ListParent, "Ultra Pixels", "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, RowTextColor);
        RectTransform rt = ultraLabel.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-panelPadding * 2f, LinePitch);
        ultraLabel.raycastTarget = true;
        ultraLabel.gameObject.AddComponent<PixelClickable>().onClick = () => { CurrencyShowNames = !CurrencyShowNames; Refresh(); };
        PixelHoverTip tip = ultraLabel.gameObject.AddComponent<PixelHoverTip>();
        tip.onEnter = () => tooltipCustomText = ultraRowTip;
        tip.onExit = HideTooltip;
        ultraLabel.gameObject.SetActive(false);

        GameObject line = new GameObject("Ultra Divider", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(ListParent, false);
        ultraDivider = line.GetComponent<Image>();
        ultraDivider.color = new Color(1f, 1f, 1f, 0.28f);
        ultraDivider.raycastTarget = false;
        RectTransform lr = line.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(-panelPadding * 2f, 2f);
        line.SetActive(false);
    }

    [Tooltip("Hover tip of the square menu button between the Consumables drop-down and search box (opens the 3D consumables window).")]
    [SerializeField] private string galleryButtonTip = "Show your consumables in 3D";

    [Tooltip("Hover tip on the switch button while the simplified Inventory shows the currency.")]
    [SerializeField] private string switchToPotionsTip = "Show your consumables";
    [Tooltip("Hover tip on the switch button while the simplified Inventory shows the consumables.")]
    [SerializeField] private string switchToCurrencyTip = "Show your currency";

    [Tooltip("Hover tip on the toggle while the Inventory is in its normal form.")]
    [SerializeField] private string pinTip = "Pin this tab";
    [Tooltip("Hover tip on the toggle while the Inventory is simplified.")]
    [SerializeField] private string compactOffTip = "Unpin this tab";

}

/// <summary>
/// A row in the inventory's Consumables list: highlights on hover and reports right-clicks.
/// </summary>
public class PotionRowClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Action onRightClick, onLeftClick;
    public Image highlight;
    public Color hoverColor = new Color(1f, 1f, 1f, 0.15f);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) onRightClick?.Invoke();
        else if (eventData.button == PointerEventData.InputButton.Left) onLeftClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (highlight != null) highlight.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlight != null) highlight.color = new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0f);
    }

    private void OnDisable()
    {
        if (highlight != null) highlight.color = new Color(hoverColor.r, hoverColor.g, hoverColor.b, 0f);
    }
}

/// <summary>Reports left clicks on a UI element.</summary>
public class PixelClickable : MonoBehaviour, IPointerClickHandler
{
    public Action onClick;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) onClick?.Invoke();
    }
}

/// <summary>Reports the mouse entering / leaving a UI element (used for tooltips).</summary>
public class PixelHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onEnter, onExit;

    public void OnPointerEnter(PointerEventData eventData) { onEnter?.Invoke(); }
    public void OnPointerExit(PointerEventData eventData) { onExit?.Invoke(); }
    private void OnDisable() { onExit?.Invoke(); }
}
