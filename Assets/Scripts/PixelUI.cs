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

    [Header("Currency Box (automatic mode)")]
    [Tooltip("Text on the button that opens/closes the currency box.")]
    [SerializeField] private string buttonText = "Currency";

    [Tooltip("Size of the button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(240f, 80f);

    [Tooltip("Button text size.")]
    [SerializeField] private float buttonFontSize = 38f;

    [Tooltip("Button colour.")]
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.6f, 0.4f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Tooltip("Title shown at the top of the box.")]
    [SerializeField] private string boxTitle = "Currency";

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
    private GameObject boxObject;
    private RectTransform boxRect;
    private bool autoMode;
    private TMP_Text titleLabel;
    private TMP_Text buttonLabel;
    private bool built;
    private RectTransform popupCanvasRect;

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    // Start (not Awake) so tiers appended by PixelShop during Awake are included.
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
            autoMode = true;
        }

        BuildPopupCanvas();
        clicker.PixelCollected += OnPixelCollected;

        built = true;
    }

    private void OnDestroy()
    {
        if (autoRoot != null) Destroy(autoRoot);
        if (popupRoot != null) Destroy(popupRoot);
        if (clicker != null) clicker.PixelCollected -= OnPixelCollected;
    }

    private void Update()
    {
        if (!built) return;
        if (autoMode && !boxObject.activeSelf) return; // closed box: nothing to update
        Refresh();
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

    /// <summary>Called for every collected pixel; automatic clicks pop up over the cube, manual ones at the cursor.</summary>
    private void OnPixelCollected(int tierIndex, double amount, bool automatic)
    {
        if (!showGainPopups || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return;

        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];
        Color color = popupUsesTierColor ? tier.color : popupColor;
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

        StartCoroutine(PopupRoutine(text, color, anchor, offset + jitter));
    }

    private IEnumerator PopupRoutine(string text, Color color, Func<Vector2> getAnchor, Vector2 offset)
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

    private static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
#else
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
#endif
        GameObject es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
    }

    private TMP_Text MakeText(Transform parent, string objectName, string text, float size,
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

    private Button MakeButton(Transform parent, string objectName, string label, Vector2 size, Color color,
                              Color labelColor, float labelSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        go.GetComponent<Button>().targetGraphic = image;

        TMP_Text text = MakeText(go.transform, "Label", label, labelSize, TextAlignmentOptions.Center,
                                 FontStyles.Bold, labelColor);
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

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

        autoRoot.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        Vector2 anchor = new Vector2(
            corner == PanelCorner.TopRight || corner == PanelCorner.BottomRight ? 1f : 0f,
            corner == PanelCorner.TopLeft || corner == PanelCorner.TopRight ? 1f : 0f);
        float sx = anchor.x > 0.5f ? -1f : 1f;
        float sy = anchor.y > 0.5f ? -1f : 1f;

        // --- Currency button
        Button button = MakeButton(autoRoot.transform, "Currency Button", buttonText, buttonSize, buttonColor,
                                   buttonTextColor, buttonFontSize);
        buttonLabel = button.GetComponentInChildren<TMP_Text>();
        if (uppercaseButton) buttonLabel.fontStyle |= FontStyles.UpperCase;
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = anchor;
        br.anchoredPosition = new Vector2(sx * margin.x, sy * margin.y);
        button.onClick.AddListener(() =>
        {
            boxObject.SetActive(!boxObject.activeSelf);
            if (boxObject.activeSelf) Refresh();
        });

        // --- Box (sits next to the button, growing away from the screen edge)
        boxObject = new GameObject("Currency Box", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(autoRoot.transform, false);
        Image bg = boxObject.GetComponent<Image>();
        bg.color = showBackground ? backgroundColor : new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true; // clicks on the box shouldn't reach the pixel behind it

        boxRect = boxObject.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = anchor;
        boxRect.sizeDelta = new Vector2(panelWidth, headerHeight + panelPadding * 2f + LinePitch * count);
        boxRect.anchoredPosition = new Vector2(sx * margin.x, sy * (margin.y + buttonSize.y + gapBelowButton));

        // Title (optional)
        if (showTitle)
        {
            titleLabel = MakeText(boxObject.transform, "Title", boxTitle, titleFontSize,
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

        // One text per tier (positions are set in Refresh so hidden tiers leave no gaps).
        tierLabels = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            TMP_Text tmp = MakeText(boxObject.transform, "Tier " + i, "", fontSize,
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
        }

        boxObject.SetActive(startOpen);
        Debug.Log("PixelUI: created the Currency box with " + count + " lines.", this);
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
            if (buttonLabel != null) buttonLabel.text = buttonText;
            if (titleLabel != null) titleLabel.text = boxTitle;
        }
        float y = headerHeight + panelPadding * 0.5f; // title bar, then the lines

        for (int i = 0; i < tierLabels.Length && i < tiers.Length; i++)
        {
            TMP_Text label = tierLabels[i];
            if (label == null) continue;

            PixelClicker.PixelTier tier = tiers[i];
            bool holding = tier.count > 0d; // e.g. a Starting Amount on a tier that is still locked
            bool visible = tier.unlocked || holding || showLockedTiers;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;

            string amount = (tier.unlocked || holding) ? FormatAmount(tier.count) : lockedText;
            label.text = string.Format(lineFormat, tier.displayName, amount);

            if (!tier.unlocked && !holding) label.color = lockedColor;
            else label.color = colorTextByTier ? tier.color : textColor;

            if (autoMode)
            {
                label.rectTransform.anchoredPosition = new Vector2(0f, -y);
                y += LinePitch;
            }
        }

        // Box height follows the number of visible lines.
        if (autoMode && boxRect != null)
            boxRect.sizeDelta = new Vector2(panelWidth, y + panelPadding);
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
