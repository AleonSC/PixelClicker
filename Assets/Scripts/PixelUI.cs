using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    // Runtime
    // ------------------------------------------------------------------

    private GameObject autoRoot;
    private bool built;

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

        built = true;
    }

    private void OnDestroy()
    {
        if (autoRoot != null) Destroy(autoRoot);
    }

    private void Update()
    {
        if (built) Refresh();
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
