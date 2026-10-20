// ---- System notes (moved here from CLAUDE.md) ----
// PixelAchievements.cs
//   Modular achievements: Inspector list of Achievement (id, title, description, Kind, pixelType, target). New goal types = add a Kind
//   value + a case in GetProgress; or Register(achievement, Func<double>) from code. One "{Pixel} Pet" achievement per pixel type
//   (RegisterPetAchievements, ids pet_<Type>, single-tier custom, earned when PixelPets.IsOwned; addPetAchievements). Three more code-
//   registered achievements (RegisterUsageAchievements / RegisterFeedAchievements: "Event Horizon" = feed a Singularity pixel to a
//   black hole (PixelBlackholeMinigame.PixelSwallowed event -> PixelStats.SingularitiesFed); "Potion Taster" = every buyable potion
//   drunk once, "Gadgeteer" = every device kind placed once; progress from PixelStats PotionsUsed / FansUsed / VacuumDevicesUsed). Achievements have tiers (tiers[], earnedTiers); defaults per pixel type: tiered "Collect" (lifetime value,
//   totalCollected: 10, 100, 1K, 10K, 100K, 1M) and tiered "Click" (PixelTier.timesCollected, saved: 10, 50, 250, 1K, 5K, 25K;
//   Kind.ClickPixelType). The unlock POPUP sits at the MIDDLE RIGHT of the screen by default (DefaultPopupPosition, popupRightMargin), drawn at popupScale (0.68) open
//   and collapsed, and starts expanded (pref AchievementPopupCollapsed2); hold the left mouse button on it (popupHoldSeconds 0.5, a ring fills round the cursor,
//   PixelHold with useLeft) to pick it up, click to PIN it anywhere; the thin button on its far right collapses it to the icon box and expands it again with a shorter hold (popupCollapseHoldSeconds 0.3, saved);
//   Settings > 'Reset achievement popup position' (ResetPopupPin) forgets the pin. The pinned position is the popup's LEFT edge; the pin is saved PER SAVE (PlayerPrefs PixelClicker.Setting.AchievementPopupX/Y declared with PixelPrefs.DeclarePerSave, inside the black margins: a new game starts with it in the default place, ReloadPopupPin on a settings load), right-click to put it back. Saved as "id:tiers". Unlocks quietly/with popup, ids saved by PixelSaveGame. Auto-added by PixelLog.
// ---------------------------------------------------
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
public class PixelAchievements : MonoBehaviour, IPixelSaved
{
    // ---- save: this system writes its own part of the save file (PixelSaveRegistry) ----
    [System.Serializable] private class SaveState { public string[] earned; }
    string IPixelSaved.SaveId => "achievements";
    int IPixelSaved.SaveOrder => 310;
    string IPixelSaved.Export() => PixelSaveRegistry.ToJson(new SaveState { earned = GetSaveState() });
    void IPixelSaved.Import(string data) { SaveState s = PixelSaveRegistry.FromJson<SaveState>(data); SetSaveState(s != null ? s.earned : null); }

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

        /// <summary>Runtime: when each tier was earned (UTC ticks; 0 = not recorded, e.g. earned before dates were kept). Saved with the game.</summary>
        [NonSerialized] public long[] earnedAt;

        /// <summary>When a tier was earned (UTC ticks, 0 = unknown).</summary>
        public long EarnedAtOf(int tier) => earnedAt != null && tier >= 0 && tier < earnedAt.Length ? earnedAt[tier] : 0L;

        /// <summary>Records when a tier was earned.</summary>
        public void StampTier(int tier, long ticks)
        {
            if (tier < 0) return;
            if (earnedAt == null || earnedAt.Length <= tier) System.Array.Resize(ref earnedAt, Mathf.Max(tier + 1, TierCount));
            earnedAt[tier] = ticks;
        }

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

    [Tooltip("Add the two usage achievements: drink every kind of potion once, and place every kind of device once.")]
    [SerializeField] private bool addUsageAchievements = true;

    [Tooltip("Add one achievement per pet type (earned by finding that pixel's pet).")]
    [SerializeField] private bool addPetAchievements = true;

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

    [Tooltip("Popup size when open (canvas units): an icon box on the left, the heading and the name, and a thin collapse button on the far right.")]
    [SerializeField] private Vector2 popupExpandedSize = new Vector2(790f, 155f);

    [Range(0.4f, 1.5f)]
    [Tooltip("Size of the whole popup (open and collapsed) - 1 = the sizes above as they are.")]
    [SerializeField] private float popupScale = 0.68f;

    [Min(0f)]
    [Tooltip("Gap between the popup and the right edge of the screen in its default spot (middle right).")]
    [SerializeField] private float popupRightMargin = 24f;

    [Min(120f)]
    [Tooltip("Popup width when collapsed (canvas units): just the icon box and the button.")]
    [SerializeField] private float popupCollapsedWidth = 195f;

    [Min(10f)]
    [Tooltip("Width of the thin collapse / expand button on the popup's right edge.")]
    [SerializeField] private float popupButtonWidth = 40f;

    [Min(0.05f)]
    [Tooltip("Seconds the left mouse button is held on that button, filling the ring, to collapse or expand the popup.")]
    [SerializeField] private float popupCollapseHoldSeconds = 0.3f;

    [Tooltip("Colour of the icon box (the square the cube icon sits in) and of the collapse button.")]
    [SerializeField] private Color popupBoxColor = new Color(0.14f, 0.17f, 0.26f, 1f);

    [Min(0f)]
    [Tooltip("How long (seconds) the left mouse button must be held on the popup, filling a small ring, before it can be moved and pinned. 0 = 0.5.")]
    [SerializeField] private float popupHoldSeconds = 0f;

    [Tooltip("Colour of the ring that fills while the popup is held.")]
    [SerializeField] private Color popupRingColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Tooltip("Shown instead of the heading while the popup is being placed.")]
    [SerializeField] private string popupPlaceText = "Click to place it   (right-click: back to the middle)";

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

    /// <summary>Pixel types without a Collect / Click achievement: the Dragon Cubes (their own find show) and the hazards (they pay nothing and are not collected).</summary>
    private static bool NoPixelAchievements(PixelClicker.PixelType type) => PixelClicker.IsRareCube(type);

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
            if (NoPixelAchievements(type)) continue; // only the rare collectible cubes have no achievements of their own
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
            if (NoPixelAchievements(type)) continue;
            // Skip a type that already has any 'Collect Pixel Type' achievement.
            if (achievements.Exists(a => a != null && a.kind == Kind.CollectPixelType && a.pixelType == type)) continue;
            achievements.Add(CreateCollectAchievement(type, tiers));
            changed = true;
        }

        // The "times clicked" achievement of each pixel type.
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (NoPixelAchievements(type)) continue;
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
    private Image popupImage;
    private RectTransform popupCanvasRect;
    private bool popupPinned, popupPlacing, popupCollapsed;
    // Collapsed: 15 margin + icon box + 12 gap + button + 15 margin, so the box and the button sit side by side with the same top and bottom (never narrower than popupCollapsedWidth).
    private float CollapsedWidth => Mathf.Max(popupCollapsedWidth, 15f + (popupExpandedSize.y - 30f) + 12f + popupButtonWidth + 15f);
    private float popupWidth;
    private RectTransform popupButtonRect, popupTextRoot;
    private TMP_Text popupArrow;
    private const string PrefPopupCollapsed = "PixelClicker.Setting.AchievementPopupCollapsed2";   // (2: everyone starts expanded again, 2026-10-19)
    private Vector2 popupPinnedPos;
    private int popupPlaceFrame;
    private const string PrefPopupX = "PixelClicker.Setting.AchievementPopupX", PrefPopupY = "PixelClicker.Setting.AchievementPopupY";

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

    /// <summary>When a tier of an achievement was earned, in the player's local time (false = no date was recorded for it).</summary>
    public bool TryGetEarnedAt(int index, int tier, out DateTime local)
    {
        long ticks = achievements[index].EarnedAtOf(tier);
        local = default;
        if (ticks <= 0L) return false;
        local = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
        return true;
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
        PixelSaveRegistry.Register(this);
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
        foreach (Achievement a in achievements) { a.earnedTiers = 0; a.earnedAt = null; }
    }

    private void Start()
    {
        if (ResetOnNextStart)
        {
            ResetOnNextStart = false;
            ResetAll();
        }
        RegisterUsageAchievements();
        RegisterPetAchievements();
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
        PixelSaveRegistry.Unregister(this);
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

    /// <summary>The achievement's id (used to sort them into sections).</summary>
    public string GetId(int index) => achievements[index].id;

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
            while (!a.Complete && progress >= a.TargetOf(a.earnedTiers)) { a.StampTier(a.earnedTiers, silent ? 0L : DateTime.UtcNow.Ticks); a.earnedTiers++; }   // (a quiet re-earn on load has no date to give)

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

    /// <summary>
    /// "Potion Taster" (drink every kind of potion once) and "Gadgeteer" (place every kind of device once). Progress is
    /// how many different ones you have used so far; combo potions made in Crafting don't count.
    /// </summary>
    private void RegisterUsageAchievements()
    {
        if (!addUsageAchievements) return;
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        PixelStats stats = PixelFind.First<PixelStats>();
        if (consumables == null || stats == null) return;
        RegisterFeedAchievements(stats);

        // The potions that can be bought (not the craft-only combos).
        List<string> potionNames = new List<string>();
        for (int i = 0; i < consumables.Count; i++)
            if (!consumables.Get(i).craftOnly) potionNames.Add(consumables.ItemName(i));

        Register(new Achievement
        {
            id = "use_all_potions",
            title = "Potion Taster",
            description = "Drink every kind of potion at least once.",
            tiers = new double[0],
            target = Math.Max(1, potionNames.Count),
            iconColor = new Color(0.75f, 0.45f, 0.95f, 1f),
        }, () =>
        {
            int used = 0;
            foreach (string name in potionNames)
                foreach (KeyValuePair<string, long> p in stats.PotionsUsed)
                    if (p.Key == name && p.Value > 0) { used++; break; }
            return used;
        });

        // The kinds of device that exist.
        List<PixelConsumables.DeviceKind> kinds = new List<PixelConsumables.DeviceKind>();
        for (int i = 0; i < consumables.DeviceCount; i++)
            if (PixelConsumables.IsPlaceableKind(consumables.GetDevice(i).kind) && !kinds.Contains(consumables.GetDevice(i).kind)) kinds.Add(consumables.GetDevice(i).kind);   // only the devices you can place

        Register(new Achievement
        {
            id = "use_all_devices",
            title = "Gadgeteer",
            description = "Place every kind of device at least once.",
            tiers = new double[0],
            target = Math.Max(1, kinds.Count),
            iconColor = new Color(0.4f, 0.85f, 1f, 1f),
        }, () =>
        {
            int used = 0;
            foreach (PixelConsumables.DeviceKind kind in kinds)
            {
                long placed = kind == PixelConsumables.DeviceKind.Fan ? stats.FansUsed
                            : kind == PixelConsumables.DeviceKind.Vacuum ? stats.VacuumDevicesUsed : (long)PixelStats.Total("device.placed." + kind);
                if (placed > 0) used++;
            }
            return used;
        });
    }

    // ------------------------------------------------------------------
    // (continued above: usage achievements)

    private static string SingularPixel(string name) => name.EndsWith(" Pixels") ? name.Substring(0, name.Length - 1) : name;

    /// <summary>One achievement per pet type: "{Pixel} Pet" is earned by finding that pixel's pet (PixelPets).</summary>
    private void RegisterPetAchievements()
    {
        if (!addPetAchievements || clicker == null) return;
        // One for every pixel type that can have a pet (all of them, also the ones whose block is not in the game's tier list yet: they get a plain name and a grey badge until it is).
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (!PixelPets.CanHavePet(type)) continue;
            PixelClicker.PixelType petType = type;
            int index = clicker.IndexOf(type);
            string name = index >= 0 ? SingularPixel(clicker.Tiers[index].displayName) : System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(?<=[a-z])(?=[A-Z0-9])", " ");
            Color tint = index >= 0 ? clicker.Tiers[index].color : new Color(0.7f, 0.72f, 0.78f, 1f);
            Register(new Achievement
            {
                id = "pet_" + type,
                title = name + " Pet",
                description = "Find the " + name + " Pet. Clicking a pixel very rarely gives you its pet.",
                pixelType = type,
                tiers = new double[0],
                target = 1,
                iconColor = new Color(tint.r, tint.g, tint.b, 1f),
            }, () => PixelPets.Instance != null && PixelPets.Instance.IsOwned(petType) ? 1d : 0d);
        }
    }

    private void RegisterFeedAchievements(PixelStats stats)
    {
        Register(new Achievement
        {
            id = "feed_singularity",
            title = "Event Horizon",
            description = "Feed a Singularity pixel to a black hole.",
            tiers = new double[0],
            target = 1,
            iconColor = new Color(0.55f, 0.2f, 0.9f, 1f),
        }, () => stats.SingularitiesFed);
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

    /// <summary>Tier index whose real look the achievement's icon should show, or -1 (no pixel type: use the plain cube).</summary>
    public int GetSkinTier(int index)
    {
        Achievement a = achievements[index];
        bool perPixel = a.kind == Kind.CollectPixelType || a.kind == Kind.ClickPixelType
                        || (a.id != null && a.id.StartsWith("pet_"));
        return perPixel ? clicker.IndexOf(a.pixelType) : -1;
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
            if (a.earnedTiers > 0)
            {
                // "id:tiers:ticks,ticks,..." (the dates of the tiers; older saves have only "id:tiers")
                List<string> times = new List<string>();
                for (int t = 0; t < a.earnedTiers; t++) times.Add(a.EarnedAtOf(t).ToString());
                entries.Add(a.id + ":" + a.earnedTiers + ":" + string.Join(",", times));
            }
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
            a.earnedAt = null;
            if (entries == null) continue;

            foreach (string entry in entries)
            {
                string[] parts = entry.Split(':');
                if (parts.Length >= 2 && parts[0] == a.id && int.TryParse(parts[1], out int tiers))
                {
                    a.earnedTiers = Mathf.Clamp(tiers, 0, a.TierCount);
                    if (parts.Length >= 3)
                    {
                        string[] times = parts[2].Split(',');
                        for (int t = 0; t < times.Length && t < a.earnedTiers; t++)
                            if (long.TryParse(times[t], out long ticks) && ticks > 0L) a.StampTier(t, ticks);
                    }
                }
            }
        }
        Evaluate(true);
    }

    // ------------------------------------------------------------------
    // Popup
    // ------------------------------------------------------------------

    private void BuildPopup()
    {
        popupCanvas = PixelUIKit.CreateCanvas("PixelAchievements Popup", popupSortingOrder, referenceResolution, true);
        popupCanvasRect = popupCanvas.GetComponent<RectTransform>();

        GameObject panel = new GameObject("Popup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(popupCanvas.transform, false);
        popupImage = panel.GetComponent<Image>();
        popupImage.color = popupColor;
        popupImage.raycastTarget = true;   // the popup can be held and moved (and the click does not reach the cube behind it)
        PixelUIKit.StyleBox(popupImage);
        popupGroup = panel.GetComponent<CanvasGroup>();
        popupGroup.blocksRaycasts = true;

        popupRect = panel.GetComponent<RectTransform>();
        popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0f, 0.5f);   // positions are the popup's LEFT edge, measured from the middle of the screen (so collapsing it does not move the icon)
        popupCollapsed = PixelPrefs.GetInt(PrefPopupCollapsed, 0) != 0;
        popupWidth = popupCollapsed ? CollapsedWidth : popupExpandedSize.x;
        popupRect.sizeDelta = new Vector2(popupWidth, popupExpandedSize.y);
        popupRect.localScale = Vector3.one * Scale;   // (the whole popup, open or collapsed, at popupScale)
        popupPinned = PixelPrefs.HasKey(PrefPopupX) && PixelPrefs.HasKey(PrefPopupY);
        popupPinnedPos = new Vector2(PixelPrefs.GetFloat(PrefPopupX, 0f), PixelPrefs.GetFloat(PrefPopupY, 0f));
        PositionPopup();

        // The icon box: a dark rounded square on the left with the spinning cube in it.
        float h = popupExpandedSize.y, boxSize = h - 30f;
        GameObject iconBox = new GameObject("Icon Box", typeof(RectTransform), typeof(Image));
        iconBox.transform.SetParent(panel.transform, false);
        Image boxImage = iconBox.GetComponent<Image>();
        boxImage.color = popupBoxColor;
        boxImage.raycastTarget = false;
        PixelUIKit.StyleBox(boxImage, true);
        RectTransform br = iconBox.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0.5f);
        br.sizeDelta = new Vector2(boxSize, boxSize);
        br.anchoredPosition = new Vector2(15f, 0f);

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(iconBox.transform, false);
        popupIcon = iconGo.AddComponent<PixelCubeIcon>();
        RectTransform ir = popupIcon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(boxSize * 0.78f, boxSize * 0.78f);
        ir.anchoredPosition = Vector2.zero;

        // The collapse / expand button: thin, on the far right, full height (hold it for a moment).
        GameObject button = new GameObject("Collapse Button", typeof(RectTransform), typeof(Image));
        button.transform.SetParent(panel.transform, false);
        Image buttonImage = button.GetComponent<Image>();
        buttonImage.color = popupBoxColor;
        buttonImage.raycastTarget = true;
        PixelUIKit.StyleBox(buttonImage);
        popupButtonRect = button.GetComponent<RectTransform>();
        popupButtonRect.anchorMin = new Vector2(1f, 0f);
        popupButtonRect.anchorMax = new Vector2(1f, 1f);
        popupButtonRect.pivot = new Vector2(1f, 0.5f);
        popupButtonRect.sizeDelta = new Vector2(popupButtonWidth, -30f);
        popupButtonRect.anchoredPosition = new Vector2(-15f, 0f);
        popupArrow = MakeText(button.transform, "Arrow", popupCollapsed ? ">" : "<", 40f, FontStyles.Bold, popupHeadingColor);
        popupArrow.richText = false;   // (a lone < or > must not be read as a tag)
        popupArrow.alignment = TextAlignmentOptions.Center;
        popupArrow.raycastTarget = false;
        PixelUIKit.Stretch(popupArrow.rectTransform);

        // The heading and the name sit between the icon box and the button.
        GameObject texts = new GameObject("Texts", typeof(RectTransform));
        texts.transform.SetParent(panel.transform, false);
        popupTextRoot = texts.GetComponent<RectTransform>();
        popupTextRoot.anchorMin = Vector2.zero;
        popupTextRoot.anchorMax = Vector2.one;
        popupTextRoot.offsetMin = new Vector2(15f + boxSize + 18f, 0f);
        popupTextRoot.offsetMax = new Vector2(-(15f + popupButtonWidth + 14f), 0f);

        popupHeadingLabel = MakeText(texts.transform, "Heading", popupHeading, popupHeadingFontSize, FontStyles.Bold, popupHeadingColor);
        popupHeadingLabel.raycastTarget = false;
        RectTransform hr = popupHeadingLabel.rectTransform;
        hr.anchorMin = new Vector2(0f, 0.55f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.offsetMin = Vector2.zero;
        hr.offsetMax = new Vector2(0f, -10f);

        popupTitleLabel = MakeText(texts.transform, "Title", "", popupTitleFontSize, FontStyles.Bold, Color.white);
        popupTitleLabel.raycastTarget = false;
        RectTransform tr = popupTitleLabel.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 0.58f);
        tr.offsetMin = new Vector2(0f, 10f);
        tr.offsetMax = Vector2.zero;
        popupTitleLabel.enableAutoSizing = true;
        popupTitleLabel.fontSizeMax = popupTitleFontSize;
        popupTitleLabel.fontSizeMin = 14f;

        popupTextRoot.gameObject.SetActive(!popupCollapsed);
        panel.SetActive(false);
    }

    private TMP_Text MakeText(Transform parent, string objectName, string text, float size, FontStyles style, Color color)
        => PixelUIKit.CreateText(font, parent, objectName, text, size, TextAlignmentOptions.MidlineLeft, style, color);

    /// <summary>Dev Events tab: shows this achievement's popup (nothing is unlocked).</summary>
    public void DevShowPopup(int index)
    {
        if (index >= 0 && index < achievements.Count) ShowPopup(index, 0);
    }

    private void ShowPopup(int index, int tier)
    {
        if (popupRect == null) return;
        popupTitleLabel.text = FormatForTier(index, achievements[index].title, tier);
        popupIcon.color = GetIconColor(index);
        if (!popupPlacing) popupTimer = popupSeconds;
        popupRect.gameObject.SetActive(true);
        popupTextRoot.gameObject.SetActive(!popupCollapsed);
    }

    private float Scale => Mathf.Clamp(popupScale, 0.4f, 1.5f);

    /// <summary>The standard spot: the MIDDLE RIGHT of the screen, its right edge popupRightMargin from the edge (it follows the width as it collapses /
    /// opens, so the right edge stays put). Positions are the popup's left edge / middle height, in canvas units from the middle of the screen.</summary>
    private Vector2 DefaultPopupPosition()
    {
        float width = popupCanvasRect != null && popupCanvasRect.rect.width > 10f ? popupCanvasRect.rect.width : 1920f;
        return new Vector2(width * 0.5f - popupRightMargin - popupWidth * Scale, 0f);
    }

    private Vector2 ClampToCanvas(Vector2 p)
    {
        if (popupCanvasRect == null) return p;
        Rect r = popupCanvasRect.rect;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float minX = -r.width * 0.5f, maxX = Mathf.Max(minX, r.width * 0.5f - popupWidth * Scale);
        float hy = Mathf.Max(0f, r.height * 0.5f - bar - popupExpandedSize.y * Scale * 0.5f);   // (inside the black margins)
        return new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, -hy, hy));
    }

    private void PositionPopup()
    {
        if (popupRect != null) popupRect.anchoredPosition = popupPinned ? ClampToCanvas(popupPinnedPos) : DefaultPopupPosition();
    }

    // The pinned spot is a PER-SAVE setting (it travels with the save file): a new game starts with the popup in its default place,
    // the game you are playing keeps your spot between sessions, and loading a save brings back that save's spot.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void DeclarePopupPinPerSave()
    {
        PixelPrefs.DeclarePerSave(PrefPopupX, PixelPrefs.Kind.Float);
        PixelPrefs.DeclarePerSave(PrefPopupY, PixelPrefs.Kind.Float);
    }

    /// <summary>A save's settings were loaded (or a new game reset them): read the popup's pinned spot again.</summary>
    public static void ReloadPopupPin()
    {
        PixelAchievements a = PixelFind.Cached<PixelAchievements>();
        if (a == null) return;
        a.popupPinned = PixelPrefs.HasKey(PrefPopupX) && PixelPrefs.HasKey(PrefPopupY);
        a.popupPinnedPos = new Vector2(PixelPrefs.GetFloat(PrefPopupX, 0f), PixelPrefs.GetFloat(PrefPopupY, 0f));
        a.PositionPopup();
    }

    /// <summary>Settings > 'Reset achievement popup position': forgets the pin, the popup goes back to the middle above the combo meter.</summary>
    public void ResetPopupPin()
    {
        popupPinned = false;
        PixelPrefs.DeleteKey(PrefPopupX);
        PixelPrefs.DeleteKey(PrefPopupY);
        PositionPopup();
    }

    private void SetCollapsed(bool collapsed)
    {
        popupCollapsed = collapsed;
        PixelPrefs.SetInt(PrefPopupCollapsed, collapsed ? 1 : 0);
        if (popupArrow != null) popupArrow.text = collapsed ? ">" : "<";
        PixelAudio.Play("ui_click");
    }

    private void TickPopup()
    {
        if (popupRect == null || !popupRect.gameObject.activeSelf) return;
        if (PixelPauseMenu.IsPaused) return;

        // The width eases between the open and the collapsed size; the texts show only while it is open enough for them.
        float targetWidth = popupCollapsed ? CollapsedWidth : popupExpandedSize.x;
        popupWidth = Mathf.Lerp(popupWidth, targetWidth, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        if (Mathf.Abs(popupWidth - targetWidth) < 0.5f) popupWidth = targetWidth;
        popupRect.sizeDelta = new Vector2(popupWidth, popupExpandedSize.y);
        bool textsOn = !popupCollapsed && popupWidth > CollapsedWidth + 220f;
        if (popupTextRoot.gameObject.activeSelf != textsOn) popupTextRoot.gameObject.SetActive(textsOn);
        if (!popupPinned && !popupPlacing) PositionPopup();   // (the default spot is centred: it follows the width)
        else if (!popupPlacing) popupRect.anchoredPosition = ClampToCanvas(popupPinnedPos);

        if (popupPlacing) { TickPlacing(); return; }

        // Hold the left mouse button for a moment (a small ring fills round the cursor): on the thin button at the right it collapses / expands the popup,
        // anywhere else on the popup it can be moved and pinned.
        Vector2 pointer = PixelInput.PointerPosition();
        bool over = popupGroup.alpha > 0.2f && RectTransformUtility.RectangleContainsScreenPoint(popupRect, pointer, null);
        bool overButton = over && RectTransformUtility.RectangleContainsScreenPoint(popupButtonRect, pointer, null);
        RectTransform target = overButton ? popupButtonRect : over ? popupRect : null;
        float seconds = overButton ? popupCollapseHoldSeconds : (popupHoldSeconds > 0f ? popupHoldSeconds : 0.5f);
        if (PixelHold.Update(this, target, seconds, "", popupRingColor, true, null, true))
        {
            if (target == popupButtonRect) SetCollapsed(!popupCollapsed);
            else
            {
                popupPlacing = true;
                popupPlaceFrame = Time.frameCount;
                popupGroup.alpha = 1f;
                popupHeadingLabel.text = popupPlaceText;
                popupTextRoot.gameObject.SetActive(true);
            }
            popupTimer = Mathf.Max(popupTimer, 1.5f);
            return;
        }
        if (over || PixelHold.ProgressOf(popupRect) > 0f || PixelHold.ProgressOf(popupButtonRect) > 0f) popupTimer = Mathf.Max(popupTimer, Mathf.Min(1f, popupSeconds));   // it stays while the mouse is on it

        popupTimer -= Time.unscaledDeltaTime;
        popupGroup.alpha = Mathf.Clamp01(popupTimer / Mathf.Min(1f, popupSeconds));
        if (popupTimer <= 0f) popupRect.gameObject.SetActive(false);
    }

    /// <summary>The popup follows the mouse; a left click pins it there, a right click puts it back to the middle.</summary>
    private void TickPlacing()
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(popupCanvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
            popupRect.anchoredPosition = ClampToCanvas(local - new Vector2(popupWidth * Scale * 0.5f, 0f));   // (centred on the mouse)
        popupImage.color = Color.Lerp(popupColor, new Color(0.35f, 0.3f, 0.1f, 0.98f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));

        bool place = PixelInput.LeftPressed() && Time.frameCount > popupPlaceFrame + 1;
        bool reset = PixelInput.RightPressed();
        if (!place && !reset) return;

        if (place)
        {
            popupPinned = true;
            popupPinnedPos = popupRect.anchoredPosition;
            PixelPrefs.SetFloat(PrefPopupX, popupPinnedPos.x);
            PixelPrefs.SetFloat(PrefPopupY, popupPinnedPos.y);
            PixelAudio.Play("ui_click");
        }
        else ResetPopupPin();
        popupPlacing = false;
        popupImage.color = popupColor;
        popupHeadingLabel.text = popupHeading;
        PositionPopup();
        popupTimer = 2.5f;   // it stays a moment where it was put
    }
}
