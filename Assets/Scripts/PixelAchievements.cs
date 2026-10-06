using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Achievement system for Pixel Clicker. Built to be easy to extend.
///
/// HOW IT WORKS
///  - Each achievement has a Kind (what is measured), optional parameters (e.g. which pixel type) and one or more
///    TIERS (targets). "Collect" achievements have six tiers: 10, 100, 1K, 10K, 100K and 1M.
///  - Progress is measured by <see cref="GetProgress"/>; each tier is earned the moment progress reaches its target,
///    and the achievement then moves on to the next tier.
///  - Unlocked achievements are saved by PixelSaveGame and listed in the Log's Achievements tab.
///  - A small popup announces each new achievement.
///
/// ADDING MORE
///  - In the Inspector: add an entry to 'Achievements' (a unique Id, title, description, kind, and Tiers - or a single Target).
///  - New kind of goal: add a value to <see cref="Kind"/> and a matching case in <see cref="GetProgress"/>. Done.
///  - From other scripts: call <see cref="Register"/> with a function that returns the current progress.
///
/// Titles and descriptions may use {pixel} (the pixel type), {target} (the tier being worked on) and {tier} (its roman numeral).
/// Add this to any GameObject (e.g. the cube). PixelLog adds it automatically if it is missing.
/// </summary>
public class PixelAchievements : MonoBehaviour
{
    /// <summary>What an achievement measures. Add new kinds here, then handle them in GetProgress.</summary>
    public enum Kind
    {
        /// <summary>Lifetime total collected of one pixel type (uses 'Pixel Type').</summary>
        CollectPixelType = 0,
        /// <summary>Lifetime total collected of all pixel types together.</summary>
        CollectAnyPixel = 1,
        /// <summary>How many pixels of one type have been clicked (collected), whatever each paid (uses 'Pixel Type').</summary>
        ClickPixelType = 2,
        /// <summary>Progress comes from a function registered in code (see Register).</summary>
        Custom = 99,
    }

    [Serializable]
    public class Achievement
    {
        [Tooltip("Unique id. Used to remember the achievement in the save file, so don't change it once players have it.")]
        public string id = "achievement";

        [Tooltip("Name shown in the Log. {pixel} = the pixel type, {target} = the target amount.")]
        public string title = "Achievement";

        [TextArea(1, 3)]
        [Tooltip("Short description. {pixel} = the pixel type, {target} = the target amount.")]
        public string description = "";

        [Tooltip("What is measured.")]
        public Kind kind = Kind.CollectPixelType;

        [Tooltip("The pixel type for 'Collect Pixel Type' (and the cube icon's colour).")]
        public PixelClicker.PixelType pixelType = PixelClicker.PixelType.White;

        [Tooltip("The target of each tier, in order (e.g. 10, 100, 1000...). Leave empty for a single-tier achievement that uses 'Target'.")]
        public double[] tiers = { 10, 100, 1000, 10000, 100000, 1000000 };

        [Min(1)]
        [Tooltip("Progress needed to unlock when 'Tiers' is empty.")]
        public double target = 1000;

        [Tooltip("Colour of the spinning cube icon when the kind has no pixel type (e.g. Collect Any Pixel).")]
        public Color iconColor = Color.white;

        [Min(0)]
        [Tooltip("Runtime: how many tiers have been earned. (Saved with the game.)")]
        public int earnedTiers = 0;

        /// <summary>Number of tiers (1 for a single-target achievement).</summary>
        public int TierCount => tiers != null && tiers.Length > 0 ? tiers.Length : 1;

        /// <summary>Target of a tier (0 = first).</summary>
        public double TargetOf(int tier)
        {
            if (tiers == null || tiers.Length == 0) return target;
            return tiers[Mathf.Clamp(tier, 0, tiers.Length - 1)];
        }

        /// <summary>True once every tier has been earned.</summary>
        public bool Complete => earnedTiers >= TierCount;
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker whose totals are measured. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Achievements")]
    [Tooltip("All achievements. A tiered 'Collect' achievement (10 to 1M) for every pixel type is created for you; add your own below.")]
    [SerializeField] private List<Achievement> achievements = CreateDefaultAchievements();

    [Tooltip("Add a tiered 'Collect' achievement for any pixel type that has none (e.g. when a new pixel type is added to the game).")]
    [SerializeField] private bool addDefaultAchievements = true;

    [Tooltip("Tier targets of the automatically created 'Collect' achievements.")]
    [SerializeField] private double[] defaultTiers = { 10, 100, 1000, 10000, 100000, 1000000 };

    [Min(0.05f)]
    [Tooltip("How often (seconds) progress is checked.")]
    [SerializeField] private float checkInterval = 0.25f;

    [Header("Unlock Popup")]
    [Tooltip("Show a popup when an achievement is earned.")]
    [SerializeField] private bool showPopup = true;

    [Tooltip("Popup heading.")]
    [SerializeField] private string popupHeading = "Achievement unlocked!";

    [Min(0.5f)]
    [Tooltip("How long the popup stays (seconds). It fades out at the end.")]
    [SerializeField] private float popupSeconds = 4f;

    [Tooltip("Popup size (canvas units).")]
    [SerializeField] private Vector2 popupSize = new Vector2(640f, 120f);

    [Tooltip("Distance of the achievement popup from the right edge of the screen. It sits at the right, vertically centred.")]
    [SerializeField] private float popupRightMargin = 60f;

    [Tooltip("Move the popup up (positive) or down (negative) from the vertical centre of the screen.")]
    [SerializeField] private float popupVerticalOffset = 0f;

    [Tooltip("Popup background colour.")]
    [SerializeField] private Color popupColor = new Color(0.07f, 0.07f, 0.09f, 0.95f);

    [Tooltip("Popup heading colour.")]
    [SerializeField] private Color popupHeadingColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Tooltip("Popup title text size.")]
    [SerializeField] private float popupTitleFontSize = 36f;

    [Tooltip("Popup heading text size.")]
    [SerializeField] private float popupHeadingFontSize = 26f;

    [Tooltip("Sorting order of the popup canvas.")]
    [SerializeField] private int popupSortingOrder = 450;

    [Tooltip("Reference resolution for the popup canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Events")]
    [Tooltip("Fired when an achievement is earned. Passes its index in the list.")]
    public UnityEvent<int> onAchievementUnlocked;

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    private static readonly double[] BuiltInTiers = { 10, 100, 1000, 10000, 100000, 1000000 };

    private static readonly double[] BuiltInClickTiers = { 10, 50, 250, 1000, 5000, 25000 };

    private static Achievement CreateClickAchievement(PixelClicker.PixelType type)
    {
        return new Achievement
        {
            id = "click_" + type.ToString().ToLowerInvariant(),
            title = "{pixel} Clicker {tier}",
            description = "Click {target} {pixel} pixels.",
            kind = Kind.ClickPixelType,
            pixelType = type,
            tiers = (double[])BuiltInClickTiers.Clone(),
        };
    }

    private static Achievement CreateCollectAchievement(PixelClicker.PixelType type, double[] tiers)
    {
        return new Achievement
        {
            id = "collect_" + type.ToString().ToLowerInvariant(),
            title = "{pixel} Collector {tier}",
            description = "Collect {target} {pixel} pixels in total.",
            kind = Kind.CollectPixelType,
            pixelType = type,
            tiers = (double[])tiers.Clone(),
        };
    }

    private static List<Achievement> CreateDefaultAchievements()
    {
        List<Achievement> list = new List<Achievement>();
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            list.Add(CreateCollectAchievement(type, BuiltInTiers));
            list.Add(CreateClickAchievement(type));
        }
        return list;
    }

    private bool EnsureDefaultAchievements()
    {
        if (!addDefaultAchievements) return false;
        if (achievements == null) achievements = new List<Achievement>();
        double[] tiers = defaultTiers != null && defaultTiers.Length > 0 ? defaultTiers : BuiltInTiers;

        bool changed = false;

        // Upgrade the old single-target 'Collect 1K' entries (made before tiers existed) to tiered ones.
        foreach (Achievement a in achievements)
        {
            if (a == null || a.kind != Kind.CollectPixelType || !a.id.StartsWith("collect_")) continue;
            if (a.tiers == null || a.tiers.Length == 0)
            {
                if (a.target != 1000) continue; // a custom target someone chose on purpose
                a.tiers = (double[])tiers.Clone();
                changed = true;
            }

            if (a.tiers.Length > 1 && a.title == "{pixel} Collector")
            {
                a.title = "{pixel} Collector {tier}"; // show the roman numeral of the tier
                changed = true;
            }
        }

        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            // Skip a type that already has any 'Collect Pixel Type' achievement.
            if (achievements.Exists(a => a != null && a.kind == Kind.CollectPixelType && a.pixelType == type)) continue;
            achievements.Add(CreateCollectAchievement(type, tiers));
            changed = true;
        }

        // The "times clicked" achievement of each pixel type.
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (achievements.Exists(a => a != null && a.kind == Kind.ClickPixelType && a.pixelType == type)) continue;
            achievements.Add(CreateClickAchievement(type));
            changed = true;
        }
        return changed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: serialized data must not be changed from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultAchievements()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private readonly Dictionary<string, Func<double>> customProgress = new Dictionary<string, Func<double>>();
    private float checkTimer;
    private GameObject popupCanvas;
    private RectTransform popupRect;
    private CanvasGroup popupGroup;
    private TMP_Text popupHeadingLabel, popupTitleLabel;
    private PixelCubeIcon popupIcon;
    private float popupTimer;

    /// <summary>Number of achievements.</summary>
    public int Count => achievements.Count;

    public Achievement Get(int index) => achievements[index];

    /// <summary>Total tiers earned across all achievements.</summary>
    public int EarnedTierTotal
    {
        get
        {
            int n = 0;
            foreach (Achievement a in achievements) n += Mathf.Min(a.earnedTiers, a.TierCount);
            return n;
        }
    }

    /// <summary>Total tiers that exist across all achievements.</summary>
    public int TierTotal
    {
        get
        {
            int n = 0;
            foreach (Achievement a in achievements) n += a.TierCount;
            return n;
        }
    }

    /// <summary>Tiers earned so far for one achievement.</summary>
    public int GetEarnedTiers(int index) => achievements[index].earnedTiers;

    public int GetTierCount(int index) => achievements[index].TierCount;

    /// <summary>True once every tier of the achievement has been earned.</summary>
    public bool IsComplete(int index) => achievements[index].Complete;

    /// <summary>True if at least one tier has been earned.</summary>
    public bool HasAnyTier(int index) => achievements[index].earnedTiers > 0;

    /// <summary>The target being worked on now (the last tier's target once everything is earned).</summary>
    public double GetCurrentTarget(int index)
    {
        Achievement a = achievements[index];
        return a.TargetOf(Mathf.Min(a.earnedTiers, a.TierCount - 1));
    }

    private void Awake()
    {
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }
        if (clicker == null)
        {
            Debug.LogError("PixelAchievements: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (clicker.UIFont != null) font = clicker.UIFont; // one shared font
        EnsureDefaultAchievements();
    }

    /// <summary>Set by the Restart button: the next scene start clears every earned tier, whatever else is stored.</summary>
    public static bool ResetOnNextStart;

    /// <summary>Forgets every earned tier.</summary>
    public void ResetAll()
    {
        foreach (Achievement a in achievements) a.earnedTiers = 0;
    }

    private void Start()
    {
        if (ResetOnNextStart)
        {
            ResetOnNextStart = false;
            ResetAll();
        }
        BuildPopup();
        Evaluate(true); // anything already earned (e.g. typed-in totals) is granted quietly
    }

    private void Update()
    {
        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            Evaluate(false);
        }

        TickPopup();
    }

    private void OnDestroy()
    {
        if (popupCanvas != null) Destroy(popupCanvas);
    }

    // ------------------------------------------------------------------
    // Progress
    // ------------------------------------------------------------------

    /// <summary>
    /// The current progress of an achievement, in the same units as its target.
    /// TO ADD A NEW KIND OF ACHIEVEMENT: add a value to the Kind enum and a case here.
    /// </summary>
    public double GetProgress(int index)
    {
        Achievement a = achievements[index];
        switch (a.kind)
        {
            case Kind.CollectPixelType:
            {
                int tier = clicker.IndexOf(a.pixelType);
                return tier >= 0 ? clicker.Tiers[tier].totalCollected : 0d;
            }

            case Kind.ClickPixelType:
            {
                int tier = clicker.IndexOf(a.pixelType);
                return tier >= 0 ? clicker.Tiers[tier].timesCollected : 0d;
            }

            case Kind.CollectAnyPixel:
            {
                double total = 0d;
                foreach (PixelClicker.PixelTier t in clicker.Tiers) total += t.totalCollected;
                return total;
            }

            case Kind.Custom:
                return customProgress.TryGetValue(a.id, out Func<double> provider) ? provider() : 0d;
        }
        return 0d;
    }

    /// <summary>What an achievement measures.</summary>
    public Kind GetKind(int index) => achievements[index].kind;

    /// <summary>The pixel type of an achievement (only meaningful for per-pixel kinds).</summary>
    public PixelClicker.PixelType GetPixelType(int index) => achievements[index].pixelType;

    /// <summary>Progress as 0..1.</summary>
    public float GetFraction(int index)
    {
        if (achievements[index].Complete) return 1f;
        double target = GetCurrentTarget(index);
        return target > 0d ? (float)Math.Min(1d, GetProgress(index) / target) : 1f;
    }

    /// <summary>Checks every achievement and earns every tier whose target has been reached.</summary>
    private void Evaluate(bool silent)
    {
        for (int i = 0; i < achievements.Count; i++)
        {
            Achievement a = achievements[i];
            if (a.Complete) continue;

            double progress = GetProgress(i);
            int before = a.earnedTiers;
            while (!a.Complete && progress >= a.TargetOf(a.earnedTiers)) a.earnedTiers++;

            // One popup per achievement, for the highest tier just reached (no spam if several tiers pass at once).
            if (a.earnedTiers > before && !silent)
            {
                onAchievementUnlocked?.Invoke(i);
                if (showPopup) ShowPopup(i, a.earnedTiers - 1);
            }
        }
    }

    /// <summary>
    /// Adds an achievement from code. 'progress' returns the current value (compared with the achievement's Target).
    /// Give it Kind.Custom. If an achievement with this id already exists, only the progress function is attached.
    /// </summary>
    public void Register(Achievement achievement, Func<double> progress)
    {
        if (achievement == null || string.IsNullOrEmpty(achievement.id)) return;

        achievement.kind = Kind.Custom;
        customProgress[achievement.id] = progress;
        if (!achievements.Exists(a => a.id == achievement.id)) achievements.Add(achievement);
    }

    // ------------------------------------------------------------------
    // Text / look helpers for UI
    // ------------------------------------------------------------------

    /// <summary>Replaces {pixel}, {target} and {tier} in a title or description for the given tier (0 = first).</summary>
    private string FormatForTier(int index, string text, int tier)
    {
        Achievement a = achievements[index];
        string numeral = a.TierCount > 1 ? ToRoman(tier + 1) : "";
        return (text ?? "")
            .Replace("{pixel}", a.kind == Kind.CollectPixelType || a.kind == Kind.ClickPixelType ? a.pixelType.ToString() : "")
            .Replace("{target}", PixelClicker.FormatNumber(a.TargetOf(tier)))
            .Replace("{tier}", numeral)
            .Trim();
    }

    /// <summary>Title of the tier being worked on (the last tier once everything is earned).</summary>
    public string GetTitle(int index)
    {
        Achievement a = achievements[index];
        return FormatForTier(index, a.title, Mathf.Min(a.earnedTiers, a.TierCount - 1));
    }

    /// <summary>Description of the tier being worked on.</summary>
    public string GetDescription(int index)
    {
        Achievement a = achievements[index];
        return FormatForTier(index, a.description, Mathf.Min(a.earnedTiers, a.TierCount - 1));
    }

    private static string ToRoman(int number)
    {
        string[] numerals = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return number >= 1 && number < numerals.Length ? numerals[number] : number.ToString();
    }

    /// <summary>Colour of the achievement's cube icon (the pixel's colour for pixel-type achievements).</summary>
    public Color GetIconColor(int index)
    {
        Achievement a = achievements[index];
        if (a.kind == Kind.CollectPixelType || a.kind == Kind.ClickPixelType)
        {
            int tier = clicker.IndexOf(a.pixelType);
            if (tier >= 0)
            {
                Color c = clicker.Tiers[tier].color;
                if (clicker.Tiers[tier].translucent) c.a = Mathf.Min(c.a, 0.6f); // glass stays see-through
                return c;
            }
        }
        return a.iconColor;
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>Earned tiers as "id:tiers" entries (for the save file).</summary>
    public string[] GetSaveState()
    {
        List<string> entries = new List<string>();
        foreach (Achievement a in achievements)
            if (a.earnedTiers > 0) entries.Add(a.id + ":" + a.earnedTiers);
        return entries.ToArray();
    }

    /// <summary>
    /// Restores earned tiers from a save. Because progress comes from the saved totals, anything already
    /// reached is re-earned quietly anyway (this also upgrades saves from before tiers existed).
    /// </summary>
    public void SetSaveState(string[] entries)
    {
        foreach (Achievement a in achievements)
        {
            a.earnedTiers = 0;
            if (entries == null) continue;

            foreach (string entry in entries)
            {
                string[] parts = entry.Split(':');
                if (parts.Length == 2 && parts[0] == a.id && int.TryParse(parts[1], out int tiers))
                    a.earnedTiers = Mathf.Clamp(tiers, 0, a.TierCount);
            }
        }
        Evaluate(true);
    }

    // ------------------------------------------------------------------
    // Popup
    // ------------------------------------------------------------------

    private void BuildPopup()
    {
        popupCanvas = PixelUIKit.CreateCanvas("PixelAchievements Popup", popupSortingOrder, referenceResolution, false);

        GameObject panel = new GameObject("Popup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(popupCanvas.transform, false);
        panel.GetComponent<Image>().color = popupColor;
        panel.GetComponent<Image>().raycastTarget = false;
        popupGroup = panel.GetComponent<CanvasGroup>();
        popupGroup.blocksRaycasts = false;

        popupRect = panel.GetComponent<RectTransform>();
        popupRect.anchorMin = popupRect.anchorMax = popupRect.pivot = new Vector2(1f, 0.5f); // right edge, vertically centred
        popupRect.sizeDelta = popupSize;
        popupRect.anchoredPosition = new Vector2(-popupRightMargin, popupVerticalOffset);

        float iconSize = popupSize.y * 0.75f;
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(panel.transform, false);
        popupIcon = iconGo.AddComponent<PixelCubeIcon>();
        RectTransform ir = popupIcon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(iconSize, iconSize);
        ir.anchoredPosition = new Vector2(popupSize.y * 0.15f, 0f);

        popupHeadingLabel = MakeText(panel.transform, "Heading", popupHeading, popupHeadingFontSize, FontStyles.Bold, popupHeadingColor);
        RectTransform hr = popupHeadingLabel.rectTransform;
        hr.anchorMin = new Vector2(0f, 0.55f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.offsetMin = new Vector2(popupSize.y + 10f, 0f);
        hr.offsetMax = new Vector2(-16f, -6f);

        popupTitleLabel = MakeText(panel.transform, "Title", "", popupTitleFontSize, FontStyles.Bold, Color.white);
        RectTransform tr = popupTitleLabel.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 0.58f);
        tr.offsetMin = new Vector2(popupSize.y + 10f, 6f);
        tr.offsetMax = new Vector2(-16f, 0f);
        popupTitleLabel.enableAutoSizing = true;
        popupTitleLabel.fontSizeMax = popupTitleFontSize;
        popupTitleLabel.fontSizeMin = 14f;

        panel.SetActive(false);
    }

    private TMP_Text MakeText(Transform parent, string objectName, string text, float size, FontStyles style, Color color)
        => PixelUIKit.CreateText(font, parent, objectName, text, size, TextAlignmentOptions.MidlineLeft, style, color);

    private void ShowPopup(int index, int tier)
    {
        if (popupRect == null) return;
        popupTitleLabel.text = FormatForTier(index, achievements[index].title, tier);
        popupIcon.color = GetIconColor(index);
        popupTimer = popupSeconds;
        popupRect.gameObject.SetActive(true);
    }

    private void TickPopup()
    {
        if (popupRect == null || popupTimer <= 0f) return;

        popupTimer -= Time.unscaledDeltaTime;
        popupGroup.alpha = Mathf.Clamp01(popupTimer / Mathf.Min(1f, popupSeconds));
        if (popupTimer <= 0f) popupRect.gameObject.SetActive(false);
    }
}
