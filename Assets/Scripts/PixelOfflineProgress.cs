using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Offline progress for Pixel Clicker.
///
/// While the game is closed, the AUTO CLICKER keeps earning. When the game starts and loads your save, this works out
/// how long you were away and pays out what the auto clicker would have collected, then shows a "Welcome back" window
/// listing what it earned. Without a running auto clicker there is nothing to earn offline.
///
/// How the amount is worked out (an average, not a replay):
///  - clicks = (time away, capped) x efficiency / auto clicker interval x clicks per tick
///  - each click is a random unlocked pixel by spawn weight; a tough pixel (Clicks To Collect above 1) needs several
///    clicks per pixel, so it pays less per click.
///
/// PixelSaveGame calls <see cref="Grant"/> after loading the save at startup (it stores when the game was last saved).
/// Add this to any GameObject; PixelSaveGame adds one if it is missing.
/// </summary>
public class PixelOfflineProgress : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    [Tooltip("The PixelClicker that is paid.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The auto clicker whose speed decides how much is earned.")]
    [SerializeField] private PixelAutoClicker autoClicker;

    [Tooltip("Play stats: offline clicks are added to the auto click count.")]
    [SerializeField] private PixelStats stats;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Rules")]
    [Tooltip("Turn offline progress on or off.")]
    [SerializeField] private bool offlineProgressEnabled = true;

    [Min(0.1f)]
    [Tooltip("The most time away that counts (hours). Anything longer is cut off.")]
    [SerializeField] private float maxOfflineHours = 8f;

    [Range(0f, 1f)]
    [Tooltip("How much of the time away counts: 1 = as if the game had been running, 0.5 = half as much.")]
    [SerializeField] private float efficiency = 0.5f;

    [Min(0f)]
    [Tooltip("Shorter absences than this (seconds) earn nothing and show no window.")]
    [SerializeField] private float minimumAwaySeconds = 60f;

    [Header("Welcome Back Window")]
    [Tooltip("Show the window listing what you earned.")]
    [SerializeField] private bool showSummary = true;

    [Tooltip("Window title.")]
    [SerializeField] private string title = "Welcome back!";

    [Tooltip("Time away line. {0} = how long you were away (the capped time is what counts).")]
    [SerializeField] private string awayFormat = "You were away for {0}.";

    [Tooltip("Line about the clicks. {0} = number of auto clicks made while you were away.")]
    [SerializeField] private string clicksFormat = "Your auto clicker made {0} clicks:";

    [Tooltip("Line for one pixel type. {0} = pixel name, {1} = amount earned.")]
    [SerializeField] private string lineFormat = "{0}   +{1}";

    [Tooltip("Text of the button that closes the window.")]
    [SerializeField] private string buttonText = "Continue";

    [Tooltip("Window width (canvas units).")]
    [SerializeField] private float windowWidth = 760f;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 60f;

    [Tooltip("Text size of the lines.")]
    [SerializeField] private float lineFontSize = 36f;

    [Tooltip("Button size.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(320f, 90f);

    [Tooltip("Window background colour.")]
    [SerializeField] private Color windowColor = new Color(0.07f, 0.07f, 0.09f, 0.97f);

    [Tooltip("Colour that dims the screen behind the window.")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Button colour.")]
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    [Tooltip("Sorting order of the window's canvas (below the pause menu).")]
    [SerializeField] private int sortingOrder = 480;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private GameObject canvasRoot;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
        if (stats == null) stats = PixelFind.First<PixelStats>();
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont; // one shared font
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    /// <summary>
    /// Pays out the auto clicker's earnings for the time since 'lastSavedUtc' and shows the window.
    /// Returns true if anything was paid.
    /// </summary>
    public bool Grant(DateTime lastSavedUtc)
    {
        if (!offlineProgressEnabled || clicker == null || autoClicker == null || !autoClicker.Running) return false;

        double away = (DateTime.UtcNow - lastSavedUtc).TotalSeconds; // clock set backwards = 0
        if (away < minimumAwaySeconds) return false;

        double counted = Math.Min(away, maxOfflineHours * 3600d) * efficiency;
        double clicks = Math.Floor(counted / Math.Max(0.02f, autoClicker.Interval)) * autoClicker.ClicksPerTick;
        if (clicks < 1d) return false;

        double[] gains = ComputeGains(clicks);

        bool any = false;
        for (int i = 0; i < gains.Length; i++)
        {
            if (gains[i] < 1d) continue;
            clicker.AddCurrency(i, gains[i]);
            any = true;
        }
        if (!any) return false;

        if (stats != null) stats.AddAutoClicks((long)clicks);
        if (showSummary) ShowSummary(away, clicks, gains);
        return true;
    }

    /// <summary>
    /// Average amount of each pixel type earned by 'clicks' automatic clicks.
    /// A tier is picked by spawn weight; a tier with Clicks To Collect N needs N clicks to pay once.
    /// </summary>
    private double[] ComputeGains(double clicks)
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        double[] chance = new double[tiers.Length]; // chance that a new pixel is this tier

        if (clicker.RandomizesSpawnTier)
        {
            double total = 0d;
            for (int i = 0; i < tiers.Length; i++)
                if (tiers[i].unlocked) total += Math.Max(0f, tiers[i].spawnWeight);

            if (total > 0d)
            {
                for (int i = 0; i < tiers.Length; i++)
                    if (tiers[i].unlocked) chance[i] = Math.Max(0f, tiers[i].spawnWeight) / total;
            }
            else
            {
                chance[clicker.FixedTierIndex] = 1d;
            }
        }
        else
        {
            chance[clicker.FixedTierIndex] = 1d;
        }

        // Average clicks one pixel takes, then how many pixels that makes.
        double clicksPerPixel = 0d;
        for (int i = 0; i < tiers.Length; i++) clicksPerPixel += chance[i] * Math.Max(1, tiers[i].clicksToCollect);
        double pixels = clicksPerPixel > 0d ? clicks / clicksPerPixel : 0d;

        double[] gains = new double[tiers.Length];
        for (int i = 0; i < tiers.Length; i++)
            gains[i] = Math.Floor(pixels * chance[i] * tiers[i].amountPerClick * clicker.ClickMultiplier);
        return gains;
    }

    // ------------------------------------------------------------------
    // Welcome back window
    // ------------------------------------------------------------------

    private void ShowSummary(double awaySeconds, double clicks, double[] gains)
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("PixelOfflineProgress Canvas", sortingOrder, referenceResolution, true);

        // Dimmer that also blocks clicks on everything behind the window.
        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasRoot.transform, false);
        dim.GetComponent<Image>().color = dimColor;
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());

        // The earnings, one coloured line per pixel type.
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        StringBuilder lines = new StringBuilder();
        int lineCount = 0;
        for (int i = 0; i < gains.Length; i++)
        {
            if (gains[i] < 1d) continue;
            if (lineCount > 0) lines.Append('\n');
            lines.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Color.Lerp(tiers[i].UIColor, Color.white, 0.4f))).Append('>')
                 .Append(string.Format(lineFormat, tiers[i].displayName, PixelClicker.FormatNumber(gains[i])))
                 .Append("</color>");
            lineCount++;
        }

        float lineHeight = lineFontSize * 1.3f;
        float height = 40f + titleFontSize * 1.4f + 2f * lineHeight + 20f + lineCount * lineHeight + 30f + buttonSize.y + 40f;

        GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image));
        window.transform.SetParent(dim.transform, false);
        window.GetComponent<Image>().color = windowColor;
        RectTransform wr = window.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(windowWidth, height);

        float y = 40f;
        AddLine(window.transform, title, titleFontSize, FontStyles.Bold, ref y, titleFontSize * 1.4f);
        AddLine(window.transform, string.Format(awayFormat, PixelStats.FormatTime(awaySeconds)), lineFontSize,
                FontStyles.Normal, ref y, lineHeight);
        AddLine(window.transform, string.Format(clicksFormat, PixelClicker.FormatNumber(clicks)), lineFontSize,
                FontStyles.Normal, ref y, lineHeight + 20f);

        TMP_Text list = AddLine(window.transform, lines.ToString(), lineFontSize, FontStyles.Bold, ref y, lineCount * lineHeight);
        list.richText = true;
        y += 30f;

        Button ok = PixelUIKit.CreateButton(font, window.transform, "Continue Button", buttonText, buttonSize, buttonColor,
                                            textColor, lineFontSize);
        RectTransform br = ok.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 1f);
        br.anchoredPosition = new Vector2(0f, -y);
        ok.onClick.AddListener(Close);

        // Escape closes it too (and before anything else, since it is on top).
        PixelWindows.Unregister(this);
        PixelWindows.Register(this, 200, () => canvasRoot != null, Close);
    }

    private TMP_Text AddLine(Transform parent, string text, float size, FontStyles style, ref float y, float height)
    {
        TMP_Text label = PixelUIKit.CreateText(font, parent, "Line", text, size, TextAlignmentOptions.Center, style, textColor);
        RectTransform rt = label.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-40f, Mathf.Max(height, 1f));
        rt.anchoredPosition = new Vector2(0f, -y);
        y += height;
        return label;
    }

    private void Close()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null;
    }
}
