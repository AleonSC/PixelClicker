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
///  - Each achievement has a Kind (what is measured), optional parameters (e.g. which pixel type) and a Target.
///  - Progress is measured by <see cref="GetProgress"/>; an achievement unlocks the moment progress reaches its target.
///  - Unlocked achievements are saved by PixelSaveGame and listed in the Log's Achievements tab.
///  - A small popup announces each new achievement.
///
/// ADDING MORE
///  - In the Inspector: add an entry to 'Achievements' (a unique Id, title, description, kind, target).
///  - New kind of goal: add a value to <see cref="Kind"/> and a matching case in <see cref="GetProgress"/>. Done.
///  - From other scripts: call <see cref="Register"/> with a function that returns the current progress.
///
/// Titles and descriptions may use {pixel} (the pixel type) and {target}.
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

        [Min(1)]
        [Tooltip("Progress needed to unlock.")]
        public double target = 1000;

        [Tooltip("Colour of the spinning cube icon when the kind has no pixel type (e.g. Collect Any Pixel).")]
        public Color iconColor = Color.white;

        [Tooltip("Runtime: has this been earned? (Saved with the game. Tick it to test.)")]
        public bool unlocked = false;
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
    [Tooltip("All achievements. 'Collect 1K' for every pixel type is created for you; add your own below.")]
    [SerializeField] private List<Achievement> achievements = CreateDefaultAchievements();

    [Tooltip("Add a 'Collect 1K' achievement for any pixel type that has none (e.g. when a new pixel type is added to the game).")]
    [SerializeField] private bool addDefaultAchievements = true;

    [Min(1)]
    [Tooltip("Target of the automatically created 'Collect' achievements.")]
    [SerializeField] private double defaultTarget = 1000;

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

    [Tooltip("Distance of the popup from the top of the screen (canvas units).")]
    [SerializeField] private float popupTopMargin = 230f;

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

    private static Achievement CreateCollectAchievement(PixelClicker.PixelType type, double target)
    {
        return new Achievement
        {
            id = "collect_" + type.ToString().ToLowerInvariant(),
            title = "{pixel} Collector",
            description = "Collect {target} {pixel} pixels in total.",
            kind = Kind.CollectPixelType,
            pixelType = type,
            target = target,
        };
    }

    private static List<Achievement> CreateDefaultAchievements()
    {
        List<Achievement> list = new List<Achievement>();
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            list.Add(CreateCollectAchievement(type, 1000));
        return list;
    }

    private bool EnsureDefaultAchievements()
    {
        if (!addDefaultAchievements) return false;
        if (achievements == null) achievements = new List<Achievement>();

        bool added = false;
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            // Skip a type that already has any 'Collect Pixel Type' achievement.
            if (achievements.Exists(a => a != null && a.kind == Kind.CollectPixelType && a.pixelType == type)) continue;
            achievements.Add(CreateCollectAchievement(type, defaultTarget));
            added = true;
        }
        return added;
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

    public bool IsUnlocked(int index) => achievements[index].unlocked;

    /// <summary>How many have been earned.</summary>
    public int UnlockedCount
    {
        get
        {
            int n = 0;
            foreach (Achievement a in achievements) if (a.unlocked) n++;
            return n;
        }
    }

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
            Debug.LogError("PixelAchievements: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (clicker.UIFont != null) font = clicker.UIFont; // one shared font
        EnsureDefaultAchievements();
    }

    private void Start()
    {
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

    /// <summary>Progress as 0..1.</summary>
    public float GetFraction(int index)
    {
        double target = achievements[index].target;
        return target > 0d ? (float)Math.Min(1d, GetProgress(index) / target) : 1f;
    }

    /// <summary>Checks every achievement and unlocks the ones that reached their target.</summary>
    private void Evaluate(bool silent)
    {
        for (int i = 0; i < achievements.Count; i++)
        {
            Achievement a = achievements[i];
            if (a.unlocked || GetProgress(i) < a.target) continue;
            Unlock(i, silent);
        }
    }

    private void Unlock(int index, bool silent)
    {
        achievements[index].unlocked = true;
        if (silent) return;

        onAchievementUnlocked?.Invoke(index);
        if (showPopup) ShowPopup(index);
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

    /// <summary>Replaces {pixel} and {target} in a title or description.</summary>
    public string Format(int index, string text)
    {
        Achievement a = achievements[index];
        return (text ?? "")
            .Replace("{pixel}", a.kind == Kind.CollectPixelType ? a.pixelType.ToString() : "")
            .Replace("{target}", PixelClicker.FormatNumber(a.target));
    }

    public string GetTitle(int index) => Format(index, achievements[index].title);

    public string GetDescription(int index) => Format(index, achievements[index].description);

    /// <summary>Colour of the achievement's cube icon (the pixel's colour for pixel-type achievements).</summary>
    public Color GetIconColor(int index)
    {
        Achievement a = achievements[index];
        if (a.kind == Kind.CollectPixelType)
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

    /// <summary>Ids of the earned achievements (for the save file).</summary>
    public string[] GetUnlockedIds()
    {
        List<string> ids = new List<string>();
        foreach (Achievement a in achievements) if (a.unlocked) ids.Add(a.id);
        return ids.ToArray();
    }

    /// <summary>Restores earned achievements from a save. Anything not listed (but already met) is re-earned quietly.</summary>
    public void SetUnlockedIds(string[] ids)
    {
        foreach (Achievement a in achievements)
            a.unlocked = ids != null && Array.IndexOf(ids, a.id) >= 0;
        Evaluate(true);
    }

    // ------------------------------------------------------------------
    // Popup
    // ------------------------------------------------------------------

    private void BuildPopup()
    {
        popupCanvas = new GameObject("PixelAchievements Popup");
        Canvas canvas = popupCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = popupSortingOrder;

        CanvasScaler scaler = popupCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Popup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(popupCanvas.transform, false);
        panel.GetComponent<Image>().color = popupColor;
        panel.GetComponent<Image>().raycastTarget = false;
        popupGroup = panel.GetComponent<CanvasGroup>();
        popupGroup.blocksRaycasts = false;

        popupRect = panel.GetComponent<RectTransform>();
        popupRect.anchorMin = popupRect.anchorMax = popupRect.pivot = new Vector2(0.5f, 1f);
        popupRect.sizeDelta = popupSize;
        popupRect.anchoredPosition = new Vector2(0f, -popupTopMargin);

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
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private void ShowPopup(int index)
    {
        if (popupRect == null) return;
        popupTitleLabel.text = GetTitle(index);
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
