using System;
using System.Collections;
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

    [Tooltip("Scroll bar colour.")]
    [SerializeField] private Color scrollbarColor = new Color(1f, 1f, 1f, 0.35f);

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

    [Tooltip("Show a title inside the box. Off = no title bar, the lines start at the top.")]
    [SerializeField] private bool showTitle = true;

    [Tooltip("Show the button text in capital letters (CURRENCY).")]
    [SerializeField] private bool uppercaseButton = true;

    [Tooltip("Show the title in capital letters (CURRENCY).")]
    [SerializeField] private bool uppercaseTitle = true;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 44f;

    /// <summary>Title bar height follows the title font size (no fixed spare room).</summary>
    private float headerHeight => showTitle ? titleFontSize * 1.25f : 0f;

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

    [Tooltip("One potion line. {0} = name, {1} = amount owned.")]
    [SerializeField] private string consumableLineFormat = "{0}   x{1}";

    [Tooltip("Shown when you own no potions.")]
    [SerializeField] private string noConsumablesText = "No consumables yet.";

    [Tooltip("Hint at the bottom of the tab.")]
    [SerializeField] private string consumableHint = "Right-click an item to use it.";

    [Tooltip("Hint text size.")]
    [SerializeField] private float hintFontSize = 24f;

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
    [SerializeField] private float guideFontSize = 34f;

    [Tooltip("Hint text colour.")]
    [SerializeField] private Color guideColor = new Color(1f, 1f, 1f, 0.9f);

    [Range(0f, 1f)]
    [Tooltip("Opacity of the black box behind the guide text. 0 = no box, 1 = solid black.")]
    [SerializeField] private float guideBackgroundOpacity = 0.6f;

    [Tooltip("Colour of the box behind the guide text (its alpha is ignored - use Guide Background Opacity).")]
    [SerializeField] private Color guideBackgroundColor = Color.black;

    [Tooltip("Space between the guide text and the edge of its box (x = left/right, y = top/bottom).")]
    [SerializeField] private Vector2 guidePadding = new Vector2(36f, 14f);

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
    private int inventoryTab; // 0 = Currency, 1 = Consumables
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
        clicker.PixelsVacuumed += OnPixelsVacuumed;
        clicker.VacuumBreakdown += OnVacuumBreakdown;

        built = true;
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (autoRoot != null) Destroy(autoRoot);
        if (popupRoot != null) Destroy(popupRoot);
        if (guideCanvasRoot != null) Destroy(guideCanvasRoot);
        if (clicker != null)
        {
            clicker.PixelCollected -= OnPixelCollected;
            clicker.PixelHit -= OnPixelHit;
            if (clicker.onTierUnlocked != null) clicker.onTierUnlocked.RemoveListener(OnTierUnlocked);
            clicker.PixelsVacuumed -= OnPixelsVacuumed;
            clicker.VacuumBreakdown -= OnVacuumBreakdown;
        }
    }

    private void Update()
    {
        if (!built) return;
        TickDeltas();
        UpdateActiveHud();
        UpdateGuide();
        if (autoMode && !boxObject.activeSelf) return; // closed box: nothing to update
        Refresh();
    }

    // ------------------------------------------------------------------
    // Popups
    // ------------------------------------------------------------------

    private void BuildPopupCanvas()
    {
        // Separate overlay canvas so popups work in both automatic and manual mode.
        GameObject go = PixelUIKit.CreateCanvas("PixelUI Popups", popupCanvasOrder, referenceResolution, false);

        popupCanvasRect = go.GetComponent<RectTransform>();
        popupRoot = go;
    }

    private GameObject popupRoot;

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
        if (!showHitPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

        // Tough pixels are often dark, so lighten the tier colour to keep the counter readable.
        Color color = Color.Lerp(clicker.Tiers[tierIndex].UIColor, Color.white, 0.6f);
        string text = string.Format(hitPopupFormat, hits, needed);
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

    /// <summary>Called for every collected pixel; automatic clicks pop up over the cube, manual ones at the cursor.</summary>
    private void OnPixelCollected(int tierIndex, double amount, bool automatic)
    {
        AddGainDelta(tierIndex, amount);
        if (!showGainPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

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
        if (!showVacuumPopup || count <= 0) return;

        Color color = clicker.Tiers[vacuumTierIndex].UIColor;
        string text = string.Format(vacuumPopupFormat, FormatAmount(total));
        StartCoroutine(PopupRoutine(text, color, CubeLocal, cubePopupStartOffset + vacuumPopupExtraOffset,
                                    vacuumPopupSize));
    }

    private IEnumerator PopupRoutine(string text, Color color, Func<Vector2> getAnchor, Vector2 offset, float sizeMultiplier)
    {
        GameObject go = new GameObject("GainPopup", typeof(RectTransform));
        go.transform.SetParent(popupCanvasRect, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(600f, 150f); // wide enough that the text never needs to wrap

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = popupFontSize * sizeMultiplier;
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
        button.onClick.AddListener(() =>
        {
            boxObject.SetActive(!boxObject.activeSelf);
            if (boxObject.activeSelf) Refresh();
        });

        // --- Box (sits next to the button, growing away from the screen edge)
        boxObject = new GameObject("Inventory Box", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(autoRoot.transform, false);
        Image bg = boxObject.GetComponent<Image>();
        bg.color = showBackground ? backgroundColor : new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true; // clicks on the box shouldn't reach the pixel behind it

        boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = anchor;
        boxRect.sizeDelta = new Vector2(panelWidth, headerHeight + panelPadding * 2f + LinePitch * count);
        boxRect.anchoredPosition = new Vector2(sx * hud.SideMargin, sy * (hud.BandThickness + gapBelowButton)); // the inventory's own gap below the bar

        // Title (optional)
        if (showTitle)
        {
            titleLabel = MakeText(boxObject.transform, "Title", inventoryTitle, titleFontSize,
                                  TextAlignmentOptions.Center,
                                  uppercaseTitle ? FontStyles.Bold | FontStyles.UpperCase : FontStyles.Bold, textColor);
            titleLabel.enableAutoSizing = true; // never clipped or wrapped, whatever the box width
            titleLabel.fontSizeMax = titleFontSize;
            titleLabel.fontSizeMin = Mathf.Min(12f, titleFontSize);
            titleLabel.overflowMode = TextOverflowModes.Overflow;

            RectTransform tr = titleLabel.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-panelPadding * 2f, headerHeight);
            tr.anchoredPosition = new Vector2(0f, -panelPadding * 0.5f);
        }

        BuildInventoryTabs(anchor);
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

            if (outlineWidth > 0f)
            {
                tmp.outlineColor = outlineColor;
                tmp.outlineWidth = outlineWidth;
            }

            tierLabels[i] = tmp;

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

        boxObject.SetActive(startOpen);
        BuildConsumableList(anchor);
        PixelWindows.Register(this, 10, () => boxObject != null && boxObject.activeSelf, () => boxObject.SetActive(false));
        Debug.Log("PixelUI: created the Inventory box with " + count + " currency lines.", this);
    }

    // ------------------------------------------------------------------
    // Refresh
    // ------------------------------------------------------------------

    /// <summary>Writes the current amounts into the texts (and stacks visible lines in automatic mode).</summary>
    public void Refresh()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        if (autoMode)
        {
            if (buttonLabel != null) buttonLabel.text = inventoryButtonText;
            if (titleLabel != null) titleLabel.text = inventoryTitle;
        }
        if (autoMode) UpdateSubTabs();
        bool showCurrency = !autoMode || inventoryTab == 0;
        float y = 0f; // lines are laid out inside the scrolling list (its top = just below the tabs)

        for (int i = 0; i < tierLabels.Length && i < tiers.Length; i++)
        {
            TMP_Text label = tierLabels[i];
            if (label == null) continue;

            PixelClicker.PixelTier tier = tiers[i];
            bool holding = tier.count > 0d; // e.g. a Starting Amount on a tier that is still locked
            bool visible = (tier.unlocked || holding || showLockedTiers) && showCurrency;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;

            string amount = (tier.unlocked || holding) ? FormatAmount(tier.count) : lockedText;
            label.text = string.Format(lineFormat, tier.displayName, amount);

            if (!tier.unlocked && !holding) label.color = lockedColor;
            else label.color = colorTextByTier ? tier.UIColor : textColor;

            if (autoMode)
            {
                label.rectTransform.anchoredPosition = new Vector2(0f, -y);
                y += LinePitch;
            }
        }

        if (autoMode)
        {
            if (inventoryTab == 1) y = RefreshConsumables(0f);
            else HideConsumableWidgets();
        }

        // Box height follows the number of visible lines.
        if (autoMode && boxRect != null)
        {
            float boxHeight = Mathf.Min(ContentTop + y + panelPadding, maxPanelHeight);
            boxRect.sizeDelta = new Vector2(panelWidth, boxHeight);

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
        handleImage.color = scrollbarColor;
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
    private float ContentTop => headerHeight + panelPadding * 0.5f + (autoMode ? subTabHeight + subTabGap : 0f);

    // ------------------------------------------------------------------
    // Inventory tabs / consumables
    // ------------------------------------------------------------------

    private void BuildInventoryTabs(Vector2 anchor)
    {
        string[] names = { currencyTabText, consumablesTabText };
        subTabImages = new Image[names.Length];

        float innerWidth = panelWidth - panelPadding * 2f;
        float tabWidth = (innerWidth - 8f) * 0.5f;
        float tabTop = headerHeight + panelPadding * 0.5f;

        for (int i = 0; i < names.Length; i++)
        {
            Button tab = MakeButton(boxObject.transform, "Tab " + names[i], names[i],
                                    new Vector2(tabWidth, subTabHeight), subTabInactiveColor, subTabTextColor,
                                    subTabFontSize > 0f ? subTabFontSize : 28f);
            subTabImages[i] = tab.GetComponent<Image>();

            RectTransform rt = tab.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(tabWidth, subTabHeight);
            rt.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (tabWidth * 0.5f + 4f), -tabTop);

            TMP_Text label = tab.GetComponentInChildren<TMP_Text>();
            label.text = names[i];
            label.color = subTabTextColor;
            label.alignment = TextAlignmentOptions.Center;

            int captured = i;
            tab.onClick.AddListener(() =>
            {
                inventoryTab = captured;
                if (listContent != null) listContent.anchoredPosition = Vector2.zero; // new tab starts at the top
                Refresh();
            });
        }
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

        noConsumablesLabel = MakeText(ListParent, "No Consumables", noConsumablesText, fontSize * 0.9f, align,
                                      FontStyles.Italic, new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        PlaceLine(noConsumablesLabel.rectTransform);
        noConsumablesLabel.gameObject.SetActive(false);

        hintLabel = MakeText(ListParent, "Hint", consumableHint, hintFontSize, align, FontStyles.Italic,
                             new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        PlaceLine(hintLabel.rectTransform);
        hintLabel.gameObject.SetActive(false);
    }

    private TextAlignmentOptions consumableAlign;

    /// <summary>(Re)creates one clickable line per potion kind. Lines are only shown while you own that potion.</summary>
    private void BuildPotionRows()
    {
        if (potionRowObjects != null)
            foreach (GameObject old in potionRowObjects)
                if (old != null) Destroy(old);

        TextAlignmentOptions align = consumableAlign;
        int count = consumables != null ? consumables.ItemCount : 0;
        potionLabels = new TMP_Text[count];
        potionRowObjects = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
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
            click.onRightClick = () => { if (consumables != null && consumables.TryUseItem(captured)) Refresh(); };

            TMP_Text label = MakeText(row.transform, "Label", "", fontSize, align, FontStyles.Normal, textColor);
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            potionLabels[i] = label;
            potionRowObjects[i] = row;
            row.SetActive(false);
        }

        Debug.Log("PixelUI: inventory knows " + count + " potion kinds (PixelConsumables on '" +
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

    /// <summary>Hides everything that belongs to the Consumables tab.</summary>
    private void HideConsumableWidgets()
    {
        if (activeLabel != null) activeLabel.gameObject.SetActive(false);
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

        bool active = consumables != null && consumables.IsActive;
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
            bool visible = owned > 0;
            if (potionRowObjects[i].activeSelf != visible) potionRowObjects[i].SetActive(visible);
            if (!visible) continue;

            potionRowObjects[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
            shown++;

            potionLabels[i].text = string.Format(consumableLineFormat, consumables.ItemName(i), FormatAmount(owned));
            int tierIndex = clicker.IndexOf(consumables.ItemRequiredType(i));
            potionLabels[i].color = colorTextByTier && tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : textColor;
        }

        noConsumablesLabel.gameObject.SetActive(shown == 0);
        if (shown == 0)
        {
            noConsumablesLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += LinePitch;
        }

        bool showHint = shown > 0 && !string.IsNullOrEmpty(consumableHint);
        hintLabel.gameObject.SetActive(showHint);
        if (showHint)
        {
            hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -y);
            y += hintFontSize * 1.5f;
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

        guideLabel = MakeText(guideRoot.transform, "Text", "", guideFontSize,
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

        // All guided tiers are unlocked: the next one is sold in the shop.
        if (stages < tiers.Length && !tiers[stages].unlocked &&
            tiers[stages].unlockMode == PixelClicker.TierUnlockMode.ShopOnly)
            return shopGuideText;

        return null;
    }

    private void UpdateGuide()
    {
        if (guideLabel == null) return;

        string text = null;
        Color color = guideColor;
        float size = guideFontSize;
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
        else if (showTierGuide)
        {
            text = BuildGuideText();
        }

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
            new Vector2(preferred.x + guidePadding.x * 2f, preferred.y + guidePadding.y * 2f);
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

        PixelConsumables.Potion potion = consumables.Get(consumables.ActiveIndex);
        hudLabel.text = string.Format(activeHudFormat, potion.displayName, Mathf.CeilToInt(consumables.Remaining));

        int tierIndex = clicker.IndexOf(potion.type);
        hudLabel.color = activeHudUsesTierColor && tierIndex >= 0 ? clicker.Tiers[tierIndex].UIColor : Color.white;
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

/// <summary>
/// A row in the inventory's Consumables list: highlights on hover and reports right-clicks.
/// </summary>
public class PotionRowClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Action onRightClick;
    public Image highlight;
    public Color hoverColor = new Color(1f, 1f, 1f, 0.15f);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right) onRightClick?.Invoke();
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
