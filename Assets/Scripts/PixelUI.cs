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

    [Tooltip("Button colour.")]
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.6f, 0.4f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Tooltip("Title shown at the top of the box.")]
    [SerializeField] private string inventoryTitle = "Inventory";

    [Tooltip("Show the header at the top of the box: the title and a close (X) button, like the Pixel Log. Off = no header, the tabs start at the top.")]
    [SerializeField] private bool showHeader = true;

    [Tooltip("Height of the header (title bar) above the tab buttons.")]
    [SerializeField] private float headerBarHeight = 90f;

    [Tooltip("Header title text size.")]
    [SerializeField] private float headerTitleSize = 48f;

    [Tooltip("Size of the close (X) button in the header. 0 = no close button.")]
    [SerializeField] private float closeButtonSize = 70f;

    [Tooltip("Colour of the header's title and of the X on the close button.")]
    [SerializeField] private Color headerTextColor = Color.white;

    [Tooltip("Colour of the close (X) button.")]
    [SerializeField] private Color closeButtonColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    [Tooltip("Show the button text in capital letters (CURRENCY).")]
    [SerializeField] private bool uppercaseButton = true;

    /// <summary>Height of the header above the tabs (0 when the header is off).</summary>
    private float headerHeight => showHeader ? headerBarHeight : 0f;

    /// <summary>Vertical distance between lines. Never smaller than the text, so lines can't overlap.</summary>
    private float LinePitch => Mathf.Max(lineHeight, fontSize * 1.3f);

    [Tooltip("Gap between the button and the box.")]
    [SerializeField] private float gapBelowButton = 12f;

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

    [Tooltip("Colour of the selected tab.")]
    [SerializeField] private Color subTabActiveColor = new Color(0.25f, 0.6f, 0.4f, 1f);

    [Tooltip("Colour of the other tab.")]
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

    [Tooltip("The kinds of consumable in the drop-down at the top of the Consumables tab. The first is potions, the second devices (fan, sorter, vacuum device...). Add names here when new kinds exist.")]
    [SerializeField] private string[] consumableCategories = { "Potions", "Devices" };

    [Min(20f)]
    [Tooltip("Height of that drop-down.")]
    [SerializeField] private float categoryDropdownHeight = 52f;

    [Tooltip("Shown on the Devices sub-tab when you own no devices.")]
    [SerializeField] private string noDevicesText = "No devices yet.";

    [Tooltip("Shown on the Seeds category when you own none.")]
    [SerializeField] private string noSeedsText = "No seeds yet.";

    [Tooltip("Hover hint on a seed.")]
    [SerializeField] private string seedHint = "Right-click a seed, then click the floor to plant it";

    [Tooltip("Hint at the top of the Potions list.")]
    [SerializeField] private string potionHint = "Right-click a potion to drink it";

    [Tooltip("Hint at the top of the Devices list.")]
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

    [Tooltip("Hint once the last guided tier is unlocked and the next one comes from the shop.")]
    [SerializeField] private string shopGuideText = "Buy the RGB Pack in the shop to progress to the next tier.";

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

    [Tooltip("Distance of the timer from the top of the screen.")]
    [SerializeField] private float activeHudTopMargin = 30f;

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

    [Tooltip("Text colour (when 'Color Text By Tier' is off).")]
    [SerializeField] private Color textColor = Color.white;

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

    [Tooltip("Show a '+X' popup over the cube when a Vacuum pixel sucks up old pixels.")]
    [SerializeField] private bool showVacuumPopup = true;

    [Tooltip("Vacuum popup text. {0} = total amount collected from the old pixels.")]
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
    [Tooltip("Width of the simplified Inventory (0 = 62% of the normal width).")]
    [SerializeField] private float compactWidth = 0f;
    private float CompactWidth => compactWidth > 0f ? compactWidth : panelWidth * 0.62f;
    private float CompactToggleSize => closeButtonSize > 0f ? closeButtonSize : 56f;
    private float CompactTop => CompactToggleSize + panelPadding * 1.5f;
    private readonly System.Collections.Generic.List<TMP_Text> materialLabels = new System.Collections.Generic.List<TMP_Text>();
    private TMP_Text noMaterialsLabel;
    private int consumableSubTab; // inside Consumables: 0 = Potions, 1 = Devices
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
    public static RectTransform WindowRect => inventoryInstance != null && inventoryInstance.boxObject != null ? inventoryInstance.boxObject.GetComponent<RectTransform>() : null;

    public static void SetInventoryOpen(bool open, bool force = false)
    {
        PixelUI ui = inventoryInstance;
        if (ui == null || ui.boxObject == null) return;
        if (!open && Compact && !force) return;   // the pinned Inventory stays up when other windows open (its own button / toggle still closes it)
        if (open) PixelPop.Show(ui.boxObject); else PixelPop.Hide(ui.boxObject);
        if (open)
        {
            ui.Refresh();
            PixelLog.SetLogOpen(false);
            PixelBank.CloseWindowIfOpen();
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

    private void Update()
    {
        if (!built) return;
        TickDeltas();
        UpdateTooltip();
        UpdateActiveHud();
        UpdateGuide();
        if (autoMode && !boxObject.activeSelf) { boxWasOpen = false; return; } // closed box: nothing to update
        if (boxWasOpen && Time.unscaledTime < nextRefreshTime) return;
        boxWasOpen = true;
        nextRefreshTime = Time.unscaledTime + refreshSeconds;
        Refresh();
    }

    private bool boxWasOpen;
    private float nextRefreshTime;

    // ------------------------------------------------------------------
    // Popups
    // ------------------------------------------------------------------

    private void BuildPopupCanvas()
    {
        // Separate overlay canvas so popups work in both automatic and manual mode.
        GameObject go = PixelUIKit.CreateCanvas("PixelUI Popups", popupCanvasOrder, referenceResolution, false);

        popupCanvasRect = go.GetComponent<RectTransform>();
        popupRoot = go;
        BuildTooltip();
    }

    private GameObject popupRoot;

    // Tooltip over a pixel's entry: its Ultra count.
    private GameObject tooltipRoot;
    private TMP_Text tooltipLabel;
    private RectTransform tooltipRect;
    private int tooltipTier = -1;
    private string tooltipCustomText;

    [Header("Ultra Tooltip")]
    [Tooltip("Tooltip shown when hovering a pixel's entry. {0} = pixel name, {1} = how many Ultra versions you own.")]
    [SerializeField] private string ultraRowTip = "Ultra pixels - earned from minigames (click to switch between the name and the number)";

    [Tooltip("Tooltip text size.")]
    [SerializeField] private float tooltipFontSize = 30f;

    [Tooltip("Tooltip background colour.")]
    [SerializeField] private Color tooltipColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);

    [Tooltip("Tooltip text colour.")]
    [SerializeField] private Color tooltipTextColor = new Color(1f, 0.88f, 0.4f, 1f);

    [Tooltip("Where the tooltip sits relative to the mouse (canvas units).")]
    [SerializeField] private Vector2 tooltipOffset = new Vector2(24f, -28f);

    private void BuildTooltip()
    {
        tooltipRoot = new GameObject("Ultra Tooltip", typeof(RectTransform), typeof(Image));
        tooltipRoot.transform.SetParent(popupCanvasRect, false);
        Image bg = tooltipRoot.GetComponent<Image>();
        bg.color = tooltipColor;
        bg.raycastTarget = false;
        tooltipRect = tooltipRoot.GetComponent<RectTransform>();
        tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0f, 1f);

        tooltipLabel = MakeText(tooltipRoot.transform, "Text", "", tooltipFontSize, TextAlignmentOptions.Center, FontStyles.Bold, tooltipTextColor);
        // The text's box is much wider than the tooltip, so it can never wrap (it is centred and simply overflows).
        RectTransform lr = tooltipLabel.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(-400f, 0f);
        lr.offsetMax = new Vector2(400f, 0f);
        tooltipLabel.overflowMode = TextOverflowModes.Overflow;
        tooltipRoot.SetActive(false);
    }

    private void ShowTooltip(int tierIndex)
    {
        tooltipTier = tierIndex;
    }

    private void HideTooltip() { tooltipTier = -1; tooltipCustomText = null; }

    private void ShowItemTooltip(int item)
    {
        tooltipCustomText = consumables != null && consumables.IsSeedItem(item) ? seedHint : consumables != null && consumables.IsDevice(item) ? deviceHint : potionHint;
    }

    private void UpdateTooltip()
    {
        if (tooltipRoot == null) return;
        bool custom = !string.IsNullOrEmpty(tooltipCustomText);
        bool show = custom || (tooltipTier >= 0 && tooltipTier < clicker.Tiers.Length);
        if (tooltipRoot.activeSelf != show) tooltipRoot.SetActive(show);
        if (!show) return;

        string text = custom ? tooltipCustomText
            : clicker.Tiers[tooltipTier].displayName;
        tooltipLabel.text = text;
        tooltipLabel.ForceMeshUpdate();
        float measured = tooltipLabel.preferredWidth;
        float width = Mathf.Max(measured, text.Length * tooltipFontSize * 0.55f) + 36f; // generous, so the text always fits
        tooltipRect.sizeDelta = new Vector2(width, tooltipFontSize * 1.8f);
        tooltipRect.anchoredPosition = CursorLocal() + tooltipOffset;
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

    /// <summary>World position of the cube in the popup canvas's local space.</summary>
    private Vector2 CubeLocal()
    {
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null || clicker.PixelTransform == null) return lastCubeLocal;

        Vector3 screen = cam.WorldToScreenPoint(clicker.PixelTransform.position);
        if (screen.z < 0f) return lastCubeLocal; // behind the camera

        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRect, screen, null, out Vector2 local);
        lastCubeLocal = local;
        return local;
    }

    private Vector2 lastCubeLocal;

    /// <summary>A click that only damaged a tough pixel: shows how many hits it has taken.</summary>
    private void OnPixelHit(int tierIndex, int hits, int needed, bool automatic)
    {
        if (!PopupsEnabled || !showHitPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

        // Tough pixels are often dark, so lighten the tier colour to keep the counter readable.
        Color color = Color.Lerp(clicker.Tiers[tierIndex].UIColor, Color.white, 0.6f);
        string text = needed < 0 ? hits.ToString() : string.Format(hitPopupFormat, hits, needed);   // a treasure chest (needed < 0) doesn't say how many it needs
        Vector2 jitter = new Vector2(UnityEngine.Random.Range(-popupRandomX, popupRandomX), 0f);

        Func<Vector2> anchor;
        Vector2 offset;
        if (automatic && autoPopupsOverCube)
        {
            anchor = CubeLocal;
            offset = cubePopupStartOffset;
        }
        else
        {
            anchor = CursorLocal;
            offset = popupStartOffset;
        }

        StartCoroutine(PopupRoutine(text, color, anchor, offset + jitter, hitPopupSize));
    }

    /// <summary>The click that breaks a tough pixel: shows the full counter (e.g. 5/5) before the payout.</summary>
    private void OnPixelFinalHit(int tierIndex, int needed, bool automatic) => OnPixelHit(tierIndex, needed, needed, automatic);

    private IEnumerator DelayedPopup(float delay, string text, Color color, Func<Vector2> anchor, Vector2 offset)
    {
        yield return new WaitForSecondsRealtime(delay);
        yield return StartCoroutine(PopupRoutine(text, color, anchor, offset, 1f));
    }

    /// <summary>Called for every collected pixel; automatic clicks pop up over the cube, manual ones at the cursor.</summary>
    private void OnPixelCollected(int tierIndex, double amount, bool automatic)
    {
        AddGainDelta(tierIndex, amount);
        if (!PopupsEnabled || !showGainPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];
        Color color = popupUsesTierColor ? tier.UIColor : popupColor;
        string text = string.Format(popupFormat, FormatAmount(amount));
        Vector2 jitter = new Vector2(UnityEngine.Random.Range(-popupRandomX, popupRandomX), 0f);

        Func<Vector2> anchor;
        Vector2 offset;

        if (automatic && autoPopupsOverCube)
        {
            Vector2 fixedPoint = CubeLocal();
            anchor = cubePopupFollowsCube ? (Func<Vector2>)CubeLocal : () => fixedPoint;
            offset = cubePopupStartOffset;
        }
        else
        {
            Vector2 fixedPoint = CursorLocal();
            anchor = followCursor ? (Func<Vector2>)CursorLocal : () => fixedPoint;
            offset = popupStartOffset;
        }

        if (tier.clicksToCollect > 1 && showHitPopups && toughPayoutDelay > 0f)
            StartCoroutine(DelayedPopup(toughPayoutDelay, text, color, anchor, offset + jitter)); // after the "5/5"
        else
            StartCoroutine(PopupRoutine(text, color, anchor, offset + jitter, 1f));
    }

    /// <summary>Shows (and adds up) "+X" next to a pixel's entry for each click's payout.</summary>
    private void AddGainDelta(int tierIndex, double amount)
    {
        if (!showGainDeltas || deltaLabels == null || tierIndex < 0 || tierIndex >= deltaLabels.Length) return;

        gainSums[tierIndex] = deltaTimers[tierIndex] > 0f && gainSums[tierIndex] > 0d ? gainSums[tierIndex] + amount : amount;
        deltaLabels[tierIndex].text = string.Format(deltaFormat, FormatAmount(gainSums[tierIndex]));
        deltaTimers[tierIndex] = deltaDuration;
        deltaLabels[tierIndex].gameObject.SetActive(true);
    }

    /// <summary>Starts a "+X" indicator next to every entry that the Vacuum just paid.</summary>
    private void OnVacuumBreakdown(double[] perTier)
    {
        if (!showVacuumDeltas || deltaLabels == null) return;

        for (int i = 0; i < perTier.Length && i < deltaLabels.Length; i++)
        {
            if (perTier[i] <= 0d) continue;
            deltaLabels[i].text = string.Format(deltaFormat, FormatAmount(perTier[i]));
            gainSums[i] = 0d; // the vacuum total replaces the click sum
            deltaTimers[i] = deltaDuration;
            deltaLabels[i].gameObject.SetActive(true);
        }
    }

    /// <summary>Fades and lifts active indicators, then hides them.</summary>
    private void TickDeltas()
    {
        if (deltaLabels == null) return;

        for (int i = 0; i < deltaLabels.Length; i++)
        {
            if (deltaTimers[i] <= 0f) continue;

            deltaTimers[i] -= Time.unscaledDeltaTime;
            float remaining = deltaDuration > 0f ? Mathf.Clamp01(deltaTimers[i] / deltaDuration) : 0f;
            float progress = 1f - remaining;

            Color c = deltaColor;
            c.a = deltaColor.a * remaining;
            deltaLabels[i].color = c;

            RectTransform dr = deltaLabels[i].rectTransform;
            dr.offsetMin = dr.offsetMax = new Vector2(0f, deltaRise * progress);

            if (deltaTimers[i] <= 0f)
            {
                deltaLabels[i].gameObject.SetActive(false);
                gainSums[i] = 0d;
            }
        }
    }

    /// <summary>"+X" popup over the cube totalling everything a Vacuum pixel just sucked up.</summary>
    private void OnPixelsVacuumed(int vacuumTierIndex, double total, int count)
    {
        if (!PopupsEnabled || !showVacuumPopup || count <= 0) return;

        Color color = clicker.Tiers[vacuumTierIndex].UIColor;
        string text = string.Format(vacuumPopupFormat, FormatAmount(total));
        StartCoroutine(PopupRoutine(text, color, CubeLocal, cubePopupStartOffset + vacuumPopupExtraOffset,
                                    vacuumPopupSize));
    }

    // ---- player settings: the "+N" popups ----

    private const string PrefPopups = "PixelClicker.Setting.Popups";
    private const string PrefPopupSize = "PixelClicker.Setting.PopupSize";
    public static readonly string[] PopupSizeNames = { "Small", "Medium", "Large", "Extra large" };
    private static readonly float[] PopupSizeScale = { 0.7f, 1f, 1.4f, 1.8f };

    /// <summary>Show the "+N" popups at the cursor / over the cube? (The inventory's "+N" next to each pixel is not affected.)</summary>
    public static bool PopupsEnabled
    {
        get => PlayerPrefs.GetInt(PrefPopups, 1) != 0;
        set { PlayerPrefs.SetInt(PrefPopups, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Popup size: 0 = small ... 3 = extra large; 1 (medium) is the size set in the Inspector.</summary>
    public static int PopupSizeChoice
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefPopupSize, 1), 0, PopupSizeNames.Length - 1);
        set { PlayerPrefs.SetInt(PrefPopupSize, Mathf.Clamp(value, 0, PopupSizeNames.Length - 1)); PlayerPrefs.Save(); }
    }

    private static float PopupScale => PopupSizeScale[PopupSizeChoice];

    private IEnumerator PopupRoutine(string text, Color color, Func<Vector2> getAnchor, Vector2 offset, float sizeMultiplier)
    {
        GameObject go = new GameObject("GainPopup", typeof(RectTransform));
        go.transform.SetParent(popupCanvasRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 150f); // wide enough that the text never needs to wrap

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = popupFontSize * sizeMultiplier * PopupScale;
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

            rt.anchoredPosition = getAnchor() + offset + Vector2.up * (popupRise * k);
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
                                   buttonTextColor, buttonFontSize);
        buttonLabel = button.GetComponentInChildren<TMP_Text>();
        if (uppercaseButton) buttonLabel.fontStyle |= FontStyles.UpperCase;
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = anchor;
        br.anchoredPosition = new Vector2(sx * margin.x, sy * margin.y);
        PixelHud hud = PixelHud.Ensure(gameObject);
        hud.Dock(br, anchor, () => boxObject != null && boxObject.activeSelf); // standard size, slides out near the mouse
        button.onClick.AddListener(() => SetInventoryOpen(!PixelPop.IsOpen(boxObject), true));

        // --- Box (sits next to the button, growing away from the screen edge)
        boxObject = new GameObject("Inventory Box", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        boxObject.transform.SetParent(autoRoot.transform, false);
        Image bg = boxObject.GetComponent<Image>();
        if (showBackground) PixelUIKit.StyleWindow(bg, new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, Mathf.Max(backgroundColor.a, 0.95f)));   // nearly solid: the stars must not show through the text
        else bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true; // clicks on the box shouldn't reach the pixel behind it

        boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = anchor;
        boxRect.sizeDelta = new Vector2(panelWidth, headerHeight + panelPadding * 2f + LinePitch * count);
        boxRect.anchoredPosition = new Vector2(sx * hud.SideMargin, sy * (hud.BandThickness + gapBelowButton)); // the inventory's own gap below the bar

        // Header: title and close button (same look as the Pixel Log)
        if (showHeader)
        {
            float closeSpace = closeButtonSize > 0f ? closeButtonSize + panelPadding : 0f;
            titleLabel = MakeText(boxObject.transform, "Title", inventoryTitle, headerTitleSize,
                                  TextAlignmentOptions.Center, FontStyles.Bold, headerTextColor);
            PixelUIKit.Caps(titleLabel);
            titleLabel.enableAutoSizing = true; // never clipped or wrapped, whatever the box width
            titleLabel.fontSizeMax = headerTitleSize;
            titleLabel.fontSizeMin = Mathf.Min(12f, headerTitleSize);
            titleLabel.overflowMode = TextOverflowModes.Overflow;

            RectTransform tr = titleLabel.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-(panelPadding * 2f + closeSpace * 2f), headerHeight); // centred, with room for the X
            tr.anchoredPosition = Vector2.zero;

            if (closeButtonSize > 0f)
            {
                Button close = MakeButton(boxObject.transform, "Close", "X", new Vector2(closeButtonSize, closeButtonSize),
                                          closeButtonColor, headerTextColor, closeButtonSize * 0.5f);
                RectTransform cr = close.GetComponent<RectTransform>();
                cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
                cr.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
                close.onClick.AddListener(() => PixelPop.Hide(boxObject));
                closeButtonObject = close.gameObject;
            }
        }

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

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Rows: a small coloured swatch, the name in plain light text, and the amount right-aligned in its own column.
    // ------------------------------------------------------------------

    // The row text is always this light colour (the scene's own 'Text Color' may be set to something else from when lines were coloured by tier).
    private static readonly Color RowTextColor = new Color(0.94f, 0.95f, 0.98f, 1f);

    private const string PrefCurrencyNames = "PixelClicker.Setting.InventoryNames";
    private static int currencyNamesCache = -1;

    /// <summary>Currency rows: true = the name beside its amount, false (the default) = just the swatch and the number (hover for the name; click to switch).</summary>
    public static bool CurrencyShowNames
    {
        get
        {
            if (currencyNamesCache < 0) currencyNamesCache = PlayerPrefs.GetInt(PrefCurrencyNames, 0);
            return currencyNamesCache != 0;
        }
        set
        {
            currencyNamesCache = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefCurrencyNames, currencyNamesCache);
        }
    }

    private const string PrefCompact = "PixelClicker.Setting.InventoryCompact";
    private static int compactCache = -1;

    /// <summary>The pinned, simplified Inventory: just swatches and numbers (currency, then the consumables you can use with a click).</summary>
    public static bool Compact
    {
        get
        {
            if (compactCache < 0) compactCache = PlayerPrefs.GetInt(PrefCompact, 0);
            return compactCache != 0;
        }
        set
        {
            compactCache = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefCompact, compactCache);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInventoryStatics() { currencyNamesCache = -1; compactCache = -1; }

    private struct RowParts { public TMP_Text amount; public Image swatch; }
    private readonly Dictionary<TMP_Text, RowParts> rowParts = new Dictionary<TMP_Text, RowParts>();

    /// <summary>The name without its "Pixels" suffix ("White Pixels" -> "White"): the Currency tab is about pixels already.</summary>
    private static string ShortName(string name)
    {
        if (name.EndsWith(" Pixels")) return name.Substring(0, name.Length - 7);
        if (name.EndsWith(" Pixel")) return name.Substring(0, name.Length - 6);
        return name;
    }

    private RowParts BuildRowParts(TMP_Text label)
    {
        float swatchSize = Mathf.Round(LinePitch * 0.8f);   // a clear colour chip
        float deltaSpace = fontSize * 1.5f;   // the "+N" indicator keeps the far right
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.margin = new Vector4(swatchSize + 18f, 0f, 110f, 0f);   // the name never runs under the amount
        label.enableAutoSizing = true;                                // a long name shrinks to fit on ONE line instead of wrapping
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Max(8f, fontSize * 0.3f);
        label.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;
#else
        label.enableWordWrapping = false;
#endif

        GameObject sw = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        sw.transform.SetParent(label.transform, false);
        Image swImage = sw.GetComponent<Image>();
        swImage.raycastTarget = false;
        RectTransform sr = sw.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(swatchSize, swatchSize);
        sr.anchoredPosition = new Vector2(4f, 0f);
        PixelUIKit.StyleButton(swImage);

        TMP_Text amount = MakeText(label.transform, "Amount", "", fontSize, TextAlignmentOptions.MidlineRight, FontStyles.Bold, RowTextColor);
        amount.raycastTarget = false;
        amount.enableAutoSizing = true;
        amount.fontSizeMax = fontSize;
        amount.fontSizeMin = Mathf.Max(8f, fontSize * 0.3f);
        amount.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        amount.textWrappingMode = TextWrappingModes.NoWrap;
#else
        amount.enableWordWrapping = false;
#endif
        RectTransform ar = amount.rectTransform;
        ar.anchorMin = new Vector2(0.55f, 0f);
        ar.anchorMax = Vector2.one;
        ar.offsetMin = Vector2.zero;
        ar.offsetMax = new Vector2(-deltaSpace, 0f);

        RowParts parts = new RowParts { amount = amount, swatch = swImage };
        rowParts[label] = parts;
        return parts;
    }

    /// <summary>Fills one row: name (left, light), amount (right) and the swatch colour.</summary>
    private void SetRow(TMP_Text label, string name, string amount, Color swatch, bool dim = false, bool animatedSwatch = false)
    {
        if (!rowParts.TryGetValue(label, out RowParts parts)) parts = BuildRowParts(label);
        PixelUIKit.SetText(label, name);
        PixelUIKit.SetText(parts.amount, amount);
        Color text = dim ? lockedColor : RowTextColor;
        label.color = text;
        parts.amount.color = text;
        if (animatedSwatch)
        {
            if (parts.swatch.GetComponent<PixelUltraSwatch>() == null) parts.swatch.gameObject.AddComponent<PixelUltraSwatch>();   // it colours itself
        }
        else parts.swatch.color = dim ? new Color(swatch.r, swatch.g, swatch.b, 0.35f) : swatch;
    }

    /// <summary>Writes the current amounts into the texts (and stacks visible lines in automatic mode).</summary>
    public void Refresh()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        if (autoMode)
        {
            if (buttonLabel != null) PixelUIKit.SetText(buttonLabel, inventoryButtonText);
            if (titleLabel != null) PixelUIKit.SetText(titleLabel, inventoryTitle);
        }
        if (autoMode) UpdateSubTabs();
        bool compact = autoMode && Compact;
        if (autoMode) ApplyCompactLook(compact);
        bool names = CurrencyShowNames && !compact;
        bool showCurrency = !autoMode || inventoryTab == 0 || compact;
        float y = 0f; // lines are laid out inside the scrolling list (its top = just below the tabs)

        // The Ultra pixel row comes first, set apart from the pixel counts by a thin line.
        bool showUltra = autoMode && showCurrency && clicker.UltraEarned > 0;
        if (ultraLabel != null)
        {
            if (ultraLabel.gameObject.activeSelf != showUltra) ultraLabel.gameObject.SetActive(showUltra);
            if (ultraDivider.gameObject.activeSelf != showUltra) ultraDivider.gameObject.SetActive(showUltra);
        }
        if (showUltra && ultraLabel != null)
        {
            ultraLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            string ultraAmount = FormatAmount(clicker.TotalUltra);
            if (names) SetRow(ultraLabel, "Ultra", ultraAmount, Color.white, false, true);
            else SetRow(ultraLabel, ultraAmount, "", Color.white, false, true);
            y += LinePitch;
            ultraDivider.rectTransform.anchoredPosition = new Vector2(0f, -(y + 4f));
            y += 12f;
        }

        for (int i = 0; i < tierLabels.Length && i < tiers.Length; i++)
        {
            TMP_Text label = tierLabels[i];
            if (label == null) continue;

            PixelClicker.PixelTier tier = tiers[i];
            bool holding = tier.count > 0d; // e.g. a Starting Amount on a tier that is still locked
            bool visible = (tier.unlocked || holding || (showLockedTiers && !tier.rareDrop)) && showCurrency;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;

            string amount = (tier.unlocked || holding) ? FormatAmount(tier.count) : lockedText;
            if (names) SetRow(label, ShortName(tier.displayName), amount, tier.UIColor, !tier.unlocked && !holding);
            else SetRow(label, amount, "", tier.UIColor, !tier.unlocked && !holding);   // swatch + number; the name is in the hover tip

            if (autoMode)
            {
                label.rectTransform.anchoredPosition = new Vector2(0f, -y);
                y += LinePitch;
            }
        }

        if (autoMode && compact)
        {
            HideMaterialWidgets();
            HideConsumableWidgets();
            y = RefreshCompactConsumables(y);
        }
        else if (autoMode)
        {
            if (inventoryTab == 1)
            {
                if (categoryDropdown != null)
                {
                    if (!categoryDropdown.gameObject.activeSelf) categoryDropdown.gameObject.SetActive(true);
                    if (categoryDropdown.value != consumableSubTab) categoryDropdown.SetValueWithoutNotify(consumableSubTab);
                }
                y = RefreshConsumables(0f);
            }
            else HideConsumableWidgets();

            if (inventoryTab == 2) y = RefreshMaterials(0f);
            else HideMaterialWidgets();
        }

        // Box height follows the number of visible lines.
        if (autoMode && boxRect != null)
        {
            float boxHeight = compact ? Mathf.Min(maxPanelHeight, ContentTop + y + panelPadding + 4f)   // the simplified one is only as tall as its lines
                                      : maxPanelHeight;   // one fixed size on every tab, so the window never changes shape
            boxRect.sizeDelta = new Vector2(compact ? CompactWidth : panelWidth, boxHeight);

            // Keep the list just below the tabs (the header height can change after the box is built).
            listViewport.offsetMin = new Vector2(0f, panelPadding);
            listViewport.offsetMax = new Vector2(0f, -ContentTop);

            // The list scrolls when it is taller than the room the box has for it.
            float viewHeight = boxHeight - ContentTop - panelPadding;
            listContent.sizeDelta = new Vector2(0f, Mathf.Max(y, 1f));
            bool scrolls = y > viewHeight + 0.5f;
            if (listBarObject != null && listBarObject.activeSelf != scrolls) listBarObject.SetActive(scrolls);
            if (!scrolls) listContent.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>Hides / shows the parts the simplified Inventory drops (title, X, tabs, category list) and tints the toggle.</summary>
    private void ApplyCompactLook(bool compact)
    {
        if (titleLabel != null && titleLabel.gameObject.activeSelf == compact) titleLabel.gameObject.SetActive(!compact);
        if (closeButtonObject != null && closeButtonObject.activeSelf == compact) closeButtonObject.SetActive(!compact);
        foreach (GameObject tab in tabObjects) if (tab != null && tab.activeSelf == compact) tab.SetActive(!compact);
        if (compact && categoryDropdown != null && categoryDropdown.gameObject.activeSelf) categoryDropdown.gameObject.SetActive(false);
        if (compactButton != null) compactButton.GetComponent<Image>().color = compact ? new Color(0.2f, 0.6f, 0.3f, 1f) : closeButtonColor;
    }

    /// <summary>The place the currency lines and potion rows live: the scrolling list in automatic mode, else the box itself.</summary>
    private Transform ListParent => listContent != null ? (Transform)listContent : boxObject.transform;

    /// <summary>A masked scroll area under the tabs (mouse wheel + a thin scroll bar) that holds all the lines.</summary>
    private void BuildListViewport()
    {
        if (!autoMode) return;

        GameObject view = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(boxObject.transform, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but catches the mouse wheel
        listViewport = view.GetComponent<RectTransform>();
        listViewport.anchorMin = Vector2.zero;
        listViewport.anchorMax = Vector2.one;
        listViewport.offsetMin = new Vector2(0f, panelPadding);
        listViewport.offsetMax = new Vector2(0f, -ContentTop);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(view.transform, false);
        listContent = content.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.sizeDelta = Vector2.zero;
        listContent.anchoredPosition = Vector2.zero;

        listScroll = view.GetComponent<ScrollRect>();
        listScroll.gameObject.AddComponent<PixelScrollSound>();
        listScroll.viewport = listViewport;
        listScroll.content = listContent;
        listScroll.horizontal = false;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = LinePitch;

        listBarObject = new GameObject("Scroll Bar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        listBarObject.transform.SetParent(view.transform, false);
        listBarObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        RectTransform br = listBarObject.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(10f, 0f);
        br.anchoredPosition = new Vector2(-2f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(listBarObject.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        PixelUIKit.StyleScrollBar(listBarObject.GetComponent<Image>(), handleImage);
        PixelUIKit.Stretch(handle.GetComponent<RectTransform>());

        Scrollbar bar = listBarObject.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.GetComponent<RectTransform>();
        bar.targetGraphic = handleImage;
        listScroll.verticalScrollbar = bar;
        listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        listBarObject.SetActive(false);
    }

    /// <summary>Where the lines start: below the title bar and (in automatic mode) the two tab buttons.</summary>
    private float ContentTop => Compact && autoMode ? CompactTop : headerHeight + (showHeader ? 0f : panelPadding * 0.5f) + (autoMode ? subTabHeight + subTabGap : 0f)
                                + (autoMode && inventoryTab == 1 ? categoryDropdownHeight + subTabGap : 0f); // room for the category drop-down

    // ------------------------------------------------------------------
    // Inventory tabs / consumables
    // ------------------------------------------------------------------

    private void BuildInventoryTabs(Vector2 anchor)
    {
        string[] names = { currencyTabText, consumablesTabText, materialsTabText };
        subTabImages = new Image[names.Length];

        float innerWidth = panelWidth - panelPadding * 2f;
        float tabWidth = (innerWidth - 8f * (names.Length - 1)) / names.Length;
        float tabTop = headerHeight + (showHeader ? 0f : panelPadding * 0.5f);

        for (int i = 0; i < names.Length; i++)
        {
            Button tab = MakeButton(boxObject.transform, "Tab " + names[i], names[i],
                                    new Vector2(tabWidth, subTabHeight), subTabInactiveColor, subTabTextColor,
                                    subTabFontSize > 0f ? subTabFontSize : 28f);
            PixelUIKit.Caps(tab);
            subTabImages[i] = tab.GetComponent<Image>();
            tabObjects.Add(tab.gameObject);

            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(tabWidth, subTabHeight);
            rt.anchoredPosition = new Vector2((i - (names.Length - 1) * 0.5f) * (tabWidth + 8f), -tabTop);

            TMP_Text label = tab.GetComponentInChildren<TMP_Text>();
            label.text = names[i];
            label.color = subTabTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; // three tabs share the width
            label.fontSizeMax = subTabFontSize > 0f ? subTabFontSize : 28f;
            label.fontSizeMin = 8f;
            label.margin = new Vector4(8f, 0f, 8f, 0f); // keeps "CONSUMABLES" off the button's edges
            label.overflowMode = TextOverflowModes.Overflow;

            int captured = i;
            tab.onClick.AddListener(() =>
            {
                inventoryTab = captured;
                if (listContent != null) listContent.anchoredPosition = Vector2.zero; // new tab starts at the top
                Refresh();
            });
        }

        // The kind-of-consumable drop-down, under the tabs (only on the Consumables tab). It sits on the box itself, not in
        // the scrolling list, so its pop-up list is never cut off.
        categoryDropdown = PixelUIKit.CreateDropdown(font, boxObject.transform, "Category Dropdown",
                                                     new Vector2(innerWidth, categoryDropdownHeight), subTabInactiveColor,
                                                     new Color(0.12f, 0.12f, 0.16f, 1f), subTabTextColor,
                                                     subTabFontSize > 0f ? subTabFontSize * 0.9f : 26f);
        RectTransform dr = categoryDropdown.GetComponent<RectTransform>();
        dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 1f);
        dr.sizeDelta = new Vector2(innerWidth, categoryDropdownHeight);
        dr.anchoredPosition = new Vector2(0f, -(tabTop + subTabHeight + subTabGap));
        categoryDropdown.options.Clear();
        foreach (string category in consumableCategories) categoryDropdown.options.Add(new TMP_Dropdown.OptionData(category));
        if (categoryDropdown.options.Count < 3) categoryDropdown.options.Add(new TMP_Dropdown.OptionData("Seeds")); // a scene saved before seeds existed has only two names
        categoryDropdown.SetValueWithoutNotify(0);
        categoryDropdown.RefreshShownValue();
        categoryDropdown.onValueChanged.AddListener(value =>
        {
            consumableSubTab = value;
            if (listContent != null) listContent.anchoredPosition = Vector2.zero;
            Refresh();
        });
        categoryDropdown.gameObject.SetActive(false);
    }

    /// <summary>The little button at the window's top left that switches the simplified (pinned) Inventory on and off.</summary>
    private void BuildCompactToggle()
    {
        if (!autoMode) return;
        float size = CompactToggleSize;
        compactButton = MakeButton(boxObject.transform, "Compact Toggle", "", new Vector2(size, size), closeButtonColor, headerTextColor, size * 0.5f);
        RectTransform rt = compactButton.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(panelPadding, -panelPadding * 0.5f);

        // Icon: four small squares (a tiny grid = "just the swatches").
        float cell = size * 0.22f, gap = size * 0.1f;
        for (int k = 0; k < 4; k++)
        {
            GameObject sq = new GameObject("Icon " + k, typeof(RectTransform), typeof(Image));
            sq.transform.SetParent(compactButton.transform, false);
            Image im = sq.GetComponent<Image>();
            im.raycastTarget = false;
            im.color = headerTextColor;
            RectTransform sr = sq.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 0.5f);
            sr.sizeDelta = new Vector2(cell, cell);
            sr.anchoredPosition = new Vector2((k % 2 == 0 ? -1f : 1f) * (cell + gap) * 0.5f, (k < 2 ? 1f : -1f) * (cell + gap) * 0.5f);
        }

        PixelHoverTip tip = compactButton.gameObject.AddComponent<PixelHoverTip>();
        tip.onEnter = () => tooltipCustomText = Compact ? compactOffTip : compactOnTip;
        tip.onExit = HideTooltip;
        compactButton.onClick.AddListener(() =>
        {
            Compact = !Compact;
            if (listContent != null) listContent.anchoredPosition = Vector2.zero;
            Refresh();
        });
    }

    [Tooltip("Hover tip on the toggle while the Inventory is in its normal form.")]
    [SerializeField] private string compactOnTip = "Simplify: just swatches and numbers, pinned on screen (click a consumable to use it)";
    [Tooltip("Hover tip on the toggle while the Inventory is simplified.")]
    [SerializeField] private string compactOffTip = "Back to the full Inventory";

    /// <summary>The simplified view's consumables: every potion / device / seed you hold as a swatch + count; a click uses it. Returns the y below.</summary>
    private float RefreshCompactConsumables(float y)
    {
        if (consumables == null) return y;
        if (potionRowObjects == null || potionRowObjects.Length != consumables.ItemCount) BuildPotionRows();
        bool first = true;
        int count = potionRowObjects.Length;
        for (int i = 0; i < count; i++)
        {
            int owned = consumables.ItemOwned(i);
            bool visible = owned > 0 && (!consumables.ItemCraftOnly(i) || consumables.Get(i).owned > 0);
            if (potionRowObjects[i] == null)
            {
                if (!visible) continue;
                CreatePotionRow(i);
            }
            if (potionRowObjects[i].activeSelf != visible) potionRowObjects[i].SetActive(visible);
            if (!visible) continue;

            if (first) { y += 10f; first = false; }   // a gap between the currency and the consumables
            potionRowObjects[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
            int tierIndex = clicker.IndexOf(consumables.ItemRequiredType(i));
            SetRow(potionLabels[i], "x" + (owned >= 99999 ? PixelConsumables.OwnedText(owned) : FormatAmount(owned)), "",
                   tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : RowTextColor);
        }
        return y;
    }

    private void UpdateSubTabs()
    {
        if (subTabImages == null) return;
        for (int i = 0; i < subTabImages.Length; i++)
            subTabImages[i].color = i == inventoryTab ? subTabActiveColor : subTabInactiveColor;
    }

    /// <summary>Builds one clickable line per potion kind (shown only while you own some), plus the status and hint texts.</summary>
    private void BuildConsumableList(Vector2 anchor)
    {
        TextAlignmentOptions align = anchor.x > 0.5f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;

        activeLabel = MakeText(ListParent, "Active Potion", "", fontSize * 0.85f, align, FontStyles.Bold, textColor);
        PlaceLine(activeLabel.rectTransform);
        activeLabel.gameObject.SetActive(false);

        consumableAlign = align;
        BuildPotionRows();

        noConsumablesLabel = MakeText(ListParent, "No Consumables", noConsumablesText.ToUpperInvariant(), fontSize,
                                      TextAlignmentOptions.Center, FontStyles.Bold, emptyMessageColor); // capitals, centred in the window
        PlaceLine(noConsumablesLabel.rectTransform);
        noConsumablesLabel.gameObject.SetActive(false);

        hintLabel = MakeText(ListParent, "Hint", potionHint, hintFontSize, align, FontStyles.Bold, hintColor);
        hintLabel.enableAutoSizing = true; // shrinks to fit the box instead of wrapping
        hintLabel.fontSizeMax = hintFontSize;
        hintLabel.fontSizeMin = 12f;
        hintLabel.overflowMode = TextOverflowModes.Ellipsis;
        PlaceLine(hintLabel.rectTransform);
        hintLabel.gameObject.SetActive(false);
    }

    private TextAlignmentOptions consumableAlign;

    /// <summary>(Re)creates one clickable line per potion kind. Lines are only shown while you own that potion.</summary>
    /// <summary>Makes the inventory row of one consumable the first time it is shown.</summary>
    private void CreatePotionRow(int i)
    {
        TextAlignmentOptions align = consumableAlign;
        GameObject row = new GameObject("Potion " + i, typeof(RectTransform), typeof(Image), typeof(PotionRowClick));
        row.transform.SetParent(ListParent, false);
        Image image = row.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        PlaceLine(row.GetComponent<RectTransform>());

        PotionRowClick click = row.GetComponent<PotionRowClick>();
        click.highlight = image;
        click.hoverColor = potionHoverColor;
        int captured = i;
        PixelHoverTip itemTip = row.AddComponent<PixelHoverTip>();
        itemTip.onEnter = () => ShowItemTooltip(captured);
        itemTip.onExit = HideTooltip;
        click.onRightClick = () => { if (consumables != null && consumables.TryUseItem(captured)) Refresh(); };
        click.onLeftClick = () => { if (Compact && consumables != null && consumables.TryUseItem(captured)) Refresh(); };   // the simplified Inventory: a plain click uses it

        TMP_Text label = MakeText(row.transform, "Label", "", fontSize, align, FontStyles.Normal, textColor);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        potionLabels[i] = label;
        potionRowObjects[i] = row;
        row.SetActive(false);
    }

    private void BuildPotionRows()
    {
        if (potionRowObjects != null)
            foreach (GameObject old in potionRowObjects)
                if (old != null) Destroy(old);

        int count = consumables != null ? consumables.ItemCount : 0;
        potionLabels = new TMP_Text[count];
        potionRowObjects = new GameObject[count];   // the rows themselves are made when an item is first held (there are over a thousand combo potions)

        PixelDebug.Info("PixelUI: inventory knows " + count + " potion kinds (PixelConsumables on '" +
                  (consumables != null ? consumables.gameObject.name : "none") + "').", this);
    }

    /// <summary>Stretches a line across the box's width, anchored to the top (Refresh sets the y position).</summary>
    private void PlaceLine(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-panelPadding * 2f, LinePitch);
    }

    /// <summary>Lays out the Materials tab: one line per minigame material you hold.</summary>
    private float RefreshMaterials(float top)
    {
        float y = top;
        int shown = 0;
        foreach (PixelMinigame m in PixelMinigame.All)
        {
            if (m == null || string.IsNullOrEmpty(m.MaterialName) || m.MaterialAmount <= 0d) continue;
            while (materialLabels.Count <= shown)
            {
                TMP_Text made = MakeText(ListParent, "Material " + materialLabels.Count, "", fontSize, consumableAlign, FontStyles.Normal, textColor);
                PlaceLine(made.rectTransform);
                materialLabels.Add(made);
            }
            TMP_Text label = materialLabels[shown];
            label.gameObject.SetActive(true);
            label.rectTransform.anchoredPosition = new Vector2(0f, -y);
            SetRow(label, m.MaterialName, FormatAmount(m.MaterialAmount), m.MaterialColor);
            y += LinePitch;
            shown++;
        }
        for (int i = shown; i < materialLabels.Count; i++) materialLabels[i].gameObject.SetActive(false);

        if (noMaterialsLabel == null)
        {
            noMaterialsLabel = MakeText(ListParent, "No Materials", noMaterialsText.ToUpperInvariant(), fontSize,
                                        TextAlignmentOptions.Center, FontStyles.Bold, emptyMessageColor);
            PlaceLine(noMaterialsLabel.rectTransform);
        }
        noMaterialsLabel.gameObject.SetActive(shown == 0);
        if (shown == 0)
        {
            noMaterialsLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }
        return y;
    }

    private void HideMaterialWidgets()
    {
        foreach (TMP_Text label in materialLabels) if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
        if (noMaterialsLabel != null && noMaterialsLabel.gameObject.activeSelf) noMaterialsLabel.gameObject.SetActive(false);
    }

    /// <summary>Does this item belong to the chosen Consumables category (Potions / Devices / Seeds)?</summary>
    private bool InConsumableCategory(int item, bool onDevices, bool onSeeds)
    {
        bool seed = consumables.IsSeedItem(item);
        if (onSeeds) return seed;
        return !seed && consumables.IsDevice(item) == onDevices;
    }

    /// <summary>Hides everything that belongs to the Consumables tab.</summary>
    private void HideConsumableWidgets()
    {
        if (activeLabel != null) activeLabel.gameObject.SetActive(false);
        if (categoryDropdown != null && categoryDropdown.gameObject.activeSelf) categoryDropdown.gameObject.SetActive(false);
        if (noConsumablesLabel != null) noConsumablesLabel.gameObject.SetActive(false);
        if (hintLabel != null) hintLabel.gameObject.SetActive(false);
        if (potionRowObjects != null)
            foreach (GameObject row in potionRowObjects)
                if (row != null) row.SetActive(false);
    }

    /// <summary>Lays out the Consumables tab starting at 'top'. Returns the y below its last line.</summary>
    private float RefreshConsumables(float top)
    {
        float y = top;

        bool onDevices = consumableSubTab == 1;
        bool onSeeds = consumableSubTab == 2;

        // The hint comes first, so it is always in view.
        bool anyOwned = false;
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount && !anyOwned; i++)
                anyOwned = consumables.ItemOwned(i) > 0 && InConsumableCategory(i, onDevices, onSeeds);
        hintLabel.text = onSeeds ? seedHint : onDevices ? deviceHint : potionHint;
        bool showHint = false; // the hint is now a hover tooltip on each item
        hintLabel.gameObject.SetActive(showHint);
        if (showHint)
        {
            hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            hintLabel.rectTransform.sizeDelta = new Vector2(-panelPadding * 2f, hintFontSize * 1.5f);
            y += hintFontSize * 1.5f + 6f;
        }

        bool active = consumables != null && consumables.IsActive && !onDevices && !onSeeds;
        activeLabel.gameObject.SetActive(active);
        if (active)
        {
            activeLabel.text = string.Format(activePotionFormat, consumables.Get(consumables.ActiveIndex).displayName,
                                             Mathf.CeilToInt(consumables.Remaining));
            activeLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }

        if (consumables != null && (potionRowObjects == null || potionRowObjects.Length != consumables.ItemCount))
            BuildPotionRows();

        int shown = 0;
        int count = potionRowObjects != null ? potionRowObjects.Length : 0;
        for (int i = 0; i < count; i++)
        {
            int owned = consumables.ItemOwned(i);
            // Combo potions only count when really held (Infinite resources would otherwise list over a thousand).
            bool visible = owned > 0 && (!consumables.ItemCraftOnly(i) || consumables.Get(i).owned > 0) && InConsumableCategory(i, onDevices, onSeeds); // this sub-tab's kind only
            if (potionRowObjects[i] == null)
            {
                if (!visible) continue;
                CreatePotionRow(i);
            }
            if (potionRowObjects[i].activeSelf != visible) potionRowObjects[i].SetActive(visible);
            if (!visible) continue;

            potionRowObjects[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
            shown++;

            int tierIndex = clicker.IndexOf(consumables.ItemRequiredType(i));
            SetRow(potionLabels[i], consumables.ItemName(i), owned >= 99999 ? PixelConsumables.OwnedText(owned) : "x" + FormatAmount(owned),
                   tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : RowTextColor);
        }

        noConsumablesLabel.gameObject.SetActive(shown == 0);
        if (shown == 0)
        {
            noConsumablesLabel.text = (onSeeds ? noSeedsText : onDevices ? noDevicesText : noConsumablesText).ToUpperInvariant();
            noConsumablesLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }

        return y;
    }

    // ------------------------------------------------------------------
    // Active potion timer (top of the screen)
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Tier guide / unlock message
    // ------------------------------------------------------------------

    private void BuildGuide()
    {
        // Own canvas, sorted below the shop (150) so the shop panel covers the guide.
        guideCanvasRoot = PixelUIKit.CreateCanvas("PixelUI Guide", guideSortingOrder, referenceResolution, false);

        // Box behind the text; resized to fit the text in UpdateGuide.
        guideRoot = new GameObject("Tier Guide", typeof(RectTransform), typeof(Image));
        guideRoot.transform.SetParent(guideCanvasRoot.transform, false);
        guideBackground = guideRoot.GetComponent<Image>();
        guideBackground.raycastTarget = false;

        RectTransform rr = guideRoot.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 1f);
        rr.anchoredPosition = new Vector2(0f, -(guideTopMargin + (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f)));

        guideLabel = MakeText(guideRoot.transform, "Text", "", guideTextSize,
                              TextAlignmentOptions.Center, FontStyles.Bold, guideColor);
        if (popupOutlineWidth > 0f)
        {
            guideLabel.outlineColor = popupOutlineColor;
            guideLabel.outlineWidth = popupOutlineWidth;
        }

        RectTransform rt = guideLabel.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        guideRoot.SetActive(false);
    }

    private void OnTierUnlocked(int index)
    {
        if (index < 0 || index >= clicker.Tiers.Length) return;

        PixelClicker.PixelTier tier = clicker.Tiers[index];
        if (!announceShopUnlocks && tier.unlockMode == PixelClicker.TierUnlockMode.ShopOnly) return;

        unlockMessage = string.Format(unlockFormat, tier.type);
        // Lightened so dark tiers (Black, Obsidian) stay readable.
        unlockColor = Color.Lerp(tier.UIColor, Color.white, 0.5f);
        unlockTimer = unlockSeconds;
    }

    /// <summary>What the player should do next, or null when there is nothing to say.</summary>
    private string BuildGuideText()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        int stages = Mathf.Min(guideTierCount, tiers.Length);

        for (int i = 1; i < stages; i++)
        {
            if (tiers[i].unlocked || tiers[i].unlockMode == PixelClicker.TierUnlockMode.ShopOnly) continue;

            double remaining = Math.Ceiling(Math.Max(0d, tiers[i].unlockThreshold - tiers[i - 1].totalCollected));
            return string.Format(guideFormat, FormatAmount(remaining), tiers[i - 1].type);
        }

        // All guided tiers are unlocked: the next one is sold in the shop (rare drops like the Dragon Cubes don't count).
        int next = stages;
        while (next < tiers.Length && tiers[next].rareDrop) next++;
        if (next < tiers.Length && !tiers[next].unlocked &&
            tiers[next].unlockMode == PixelClicker.TierUnlockMode.ShopOnly)
            return shopGuideText;

        return null;
    }

    private void UpdateGuide()
    {
        if (guideLabel == null) return;

        string text = null;
        Color color = guideColor;
        float size = guideTextSize;
        float fade = 1f;

        if (unlockTimer > 0f)
        {
            unlockTimer -= Time.unscaledDeltaTime;
            text = unlockMessage;
            size = unlockFontSize;
            color = unlockColor;
            // Fade over the last second.
            fade = Mathf.Clamp01(unlockTimer / Mathf.Min(1f, Mathf.Max(0.01f, unlockSeconds)));
        }
        else if (showTierGuide && !PixelHints.IntroHoldsGuide) // hidden until the new-game intro tips have been read
        {
            text = BuildGuideText();
        }
        PixelClicker.ClickHint = unlockTimer <= 0f && !string.IsNullOrEmpty(text) && showTierGuide; // the cube pulses while this text shows and nothing was collected yet

        bool visible = !string.IsNullOrEmpty(text);
        if (guideRoot.activeSelf != visible) guideRoot.SetActive(visible);
        if (!visible) return;

        guideLabel.text = text;
        guideLabel.fontSize = size;
        color.a *= fade;
        guideLabel.color = color;

        Color bg = guideBackgroundColor;
        bg.a = guideBackgroundOpacity * fade;
        guideBackground.color = bg;

        // Fit the box around the text.
        Vector2 preferred = guideLabel.GetPreferredValues(text);
        guideRoot.GetComponent<RectTransform>().sizeDelta =
            new Vector2(preferred.x + guideBoxPadding.x * 2f, preferred.y + guideBoxPadding.y * 2f);
    }

    private void BuildActiveHud()
    {
        if (!showActivePotionHud) return;

        hudLabel = MakeText(popupCanvasRect, "Active Potion Timer", "", activeHudFontSize,
                            TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        if (popupOutlineWidth > 0f)
        {
            hudLabel.outlineColor = popupOutlineColor;
            hudLabel.outlineWidth = popupOutlineWidth;
        }

        RectTransform rt = hudLabel.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(900f, activeHudFontSize * 1.4f);
        rt.anchoredPosition = new Vector2(0f, -(activeHudTopMargin + (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f)));
        hudLabel.gameObject.SetActive(false);
    }

    private void UpdateActiveHud()
    {
        if (hudLabel == null) return;

        // While placing a device the line tells you what to do; otherwise it shows the running potion.
        bool placing = consumables != null && consumables.IsPlacing;
        bool active = placing || (consumables != null && consumables.IsActive);
        if (hudLabel.gameObject.activeSelf != active) hudLabel.gameObject.SetActive(active);
        if (!active) return;

        if (placing)
        {
            string own = consumables.PlacingMessage;
            hudLabel.text = string.Format(string.IsNullOrEmpty(own) ? placingHudFormat : own, consumables.PlacingName);
            hudLabel.color = Color.white;
            return;
        }

        // Hold the right mouse button on the line to cancel the potion.
        PixelConsumables.Potion running = consumables.Get(consumables.ActiveIndex);
        if (PixelHold.Update(this, PointerOverHudLabel() ? hudLabel : null, consumables.RemoveHoldSeconds,
                             string.Format(consumables.CancelPotionText, running.displayName), consumables.RemoveMeterColor))
        {
            consumables.CancelActive();
            return;
        }

        PixelConsumables.Potion potion = consumables.Get(consumables.ActiveIndex);
        hudLabel.text = string.Format(activeHudFormat, potion.displayName, Mathf.CeilToInt(consumables.Remaining));

        int tierIndex = clicker.IndexOf(potion.type);
        hudLabel.color = activeHudUsesTierColor && tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : Color.white;
    }

    /// <summary>Is the mouse on the text of the active-potion line?</summary>
    private bool PointerOverHudLabel()
    {
        if (hudLabel == null || !hudLabel.gameObject.activeSelf) return false;
        RectTransform rt = hudLabel.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, PixelInput.PointerPosition(), null, out Vector2 local)) return false;
        float halfWidth = Mathf.Min(hudLabel.preferredWidth, rt.rect.width) * 0.5f + 12f;
        return Mathf.Abs(local.x - rt.rect.center.x) <= halfWidth && Mathf.Abs(local.y - rt.rect.center.y) <= rt.rect.height * 0.5f;
    }

    private string FormatAmount(double value)
    {
        return PixelClicker.FormatNumber(value);
    }
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
