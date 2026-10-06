using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shop for Pixel Clicker.
///
/// - A "Shop" button appears once a chosen tier (default: Black) is unlocked.
/// - Clicking it opens a panel listing the packs you can buy.
/// - Buying a pack spends its cost and unlocks its reward tiers, which then join the
///   random spawn pool of PixelClicker (weighted by each tier's Spawn Weight).
///
/// The first pack is "RGB": costs 100 White + 100 Gray + 100 Black and unlocks Red, Green, Blue.
/// Reward tiers are added to PixelClicker automatically, so you don't have to edit its Tiers list.
///
/// Add this to any GameObject (e.g. the cube). The UI builds itself at runtime.
/// </summary>
[RequireComponent(typeof(PixelAutoClicker))]
public partial class PixelShop : MonoBehaviour
{
    public enum ButtonCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ------------------------------------------------------------------
    // Data types
    // ------------------------------------------------------------------

    /// <summary>One line of a pack's price.</summary>
    [Serializable]
    public class PackCost
    {
        [Tooltip("Which currency is spent.")]
        public PixelClicker.PixelType type = PixelClicker.PixelType.White;

        [Tooltip("How much of it is spent.")]
        public double amount = 100;
    }

    /// <summary>What a leveled (upgrade) pack changes on the auto clicker.</summary>
    public enum UpgradeEffect
    {
        None = 0,
        /// <summary>Each level sets the auto clicker's interval (seconds) to the level's Value.</summary>
        AutoClickerInterval = 1,
        /// <summary>Each level sets the auto clicker's clicks per tick to the level's Value.</summary>
        AutoClickerClicks = 2,
    }

    /// <summary>The shop tabs. 'Automatic' picks Pixels or Upgrades from what the pack does.</summary>
    public enum ShopTab
    {
        /// <summary>Packs that unlock pixels go to Pixels; auto clicker / upgrade packs go to Upgrades.</summary>
        Automatic = 0,
        Pixels = 1,
        Upgrades = 2,
        Consumables = 3,
        Minigames = 4,
    }

    /// <summary>One purchasable level of an upgrade pack.</summary>
    [Serializable]
    public class PackLevel
    {
        [Tooltip("Price of reaching this level.")]
        public PackCost[] costs;

        [Tooltip("Value this level sets. Interval upgrade: seconds between clicks. Clicks upgrade: clicks per tick.")]
        public float value = 1f;
    }

    /// <summary>Something purchasable: a price and the tiers it unlocks.</summary>
    [Serializable]
    public class ShopPack
    {
        [Tooltip("Name shown in the shop.")]
        public string displayName = "Pack";

        [TextArea(1, 3)]
        [Tooltip("Short description shown under the name. Use {interval} to show the auto clicker's interval in seconds.")]
        public string description = "";

        [Tooltip("Which shop tab this item is listed on. Automatic = Pixels for packs that unlock pixels, " +
                 "Upgrades for the auto clicker and its upgrades.")]
        public ShopTab tab = ShopTab.Automatic;

        [Tooltip("Everything the player must pay. All costs are paid together.")]
        public PackCost[] costs;

        [Tooltip("Tiers unlocked by this pack. Added to PixelClicker automatically if it doesn't have them. " +
                 "Set Spawn Weight to control how often each appears.")]
        public PixelClicker.PixelTier[] rewardTiers;

        [Tooltip("Index (in the Packs list, starting at 0) of a pack that must be bought first. -1 = no requirement.")]
        public int requiresPackIndex = -1;

        [Tooltip("Buying this pack switches on the auto clicker.")]
        public bool unlocksAutoClicker = false;

        [Tooltip("Id of a minigame that buying this pack switches on (e.g. ghost, blackhole). Empty = none.")]
        public string unlocksMinigame = "";

        [Tooltip("Id of a minigame whose goal must be reached before this pack can be bought " +
                 "(e.g. ghost = enough ghosts caught, blackhole = singularity goal). Empty = none.")]
        public string requiresMinigameGoal = "";

        // Older versions used one tick box per minigame. They are read once and turned into the ids above.
        [HideInInspector] public bool unlocksGhostMinigame;
        [HideInInspector] public bool unlocksBlackholeMinigame;
        [HideInInspector] public bool requiresSingularity;
        [HideInInspector] public bool requiresGhosts;

        [Tooltip("Runtime: has this pack been bought? (Packs with reward tiers also count as bought once all their tiers are unlocked.)")]
        public bool purchased = false;

        [Header("Upgrade levels (leave empty for a one-time pack)")]
        [Tooltip("What each level of this upgrade changes.")]
        public UpgradeEffect upgradeEffect = UpgradeEffect.None;

        [Tooltip("The levels, in order. Level 1 is Element 0. If this has entries, 'Costs' above is ignored and each level has its own price.")]
        public PackLevel[] levels;

        [Tooltip("Runtime: current level (0 = not bought). You can set a starting level here.")]
        public int level = 0;

        [NonSerialized] public int appliedLevel = -1;
    }

    // ------------------------------------------------------------------
    // References
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker to use. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The auto clicker switched on by the Auto Clicker pack. Taken from this GameObject (or found in the scene) if empty. Edit its interval on that component.")]
    [SerializeField] private PixelAutoClicker autoClicker;

    [Tooltip("Holds the potions sold in the Consumables tab. Found in the scene (or added to this GameObject) if empty. Edit prices and durations on that component.")]
    [SerializeField] private PixelConsumables consumables;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    [Header("Packs")]
    [Tooltip("The shop's items. Add more entries here later.")]
    [SerializeField] private ShopPack[] packs =
    {
        CreateRgbPack(), CreateAutoClickerPack(), CreateGlassPack(0), CreateVacuumPack(2),
        CreateIntervalUpgradePack(1), CreateClicksUpgradePack(1), CreateObsidianPack(2),
        CreateLuminescentPack(6), CreateGhostPack(2), CreateBlackholePack(3), CreateSingularityPack(9), CreateGhostPixelPack(8)
    };

    [Tooltip("If none of the packs unlocks the Ghost pixel, add the default Ghost Pixel pack at startup.")]
    [SerializeField] private bool addDefaultGhostPixelPack = true;

    [Tooltip("If none of the packs unlocks the black hole minigame (e.g. this component was added before it existed), " +
             "add the default Black Hole pack at startup.")]
    [SerializeField] private bool addDefaultBlackholePack = true;

    [Tooltip("If none of the packs unlocks the Singularity pixel, add the default Singularity Pixel pack at startup.")]
    [SerializeField] private bool addDefaultSingularityPack = true;

    [Tooltip("If none of the packs unlocks the ghost minigame (e.g. this component was added before it existed), " +
             "add the default Ghost Hunt pack at startup.")]
    [SerializeField] private bool addDefaultGhostPack = true;

    [Tooltip("If none of the packs unlocks the Luminescent pixel (e.g. this component was added before it existed), " +
             "add the default Luminescent pack at startup.")]
    [SerializeField] private bool addDefaultLuminescentPack = true;

    [Tooltip("If none of the packs unlocks the Obsidian pixel (e.g. this component was added before it existed), " +
             "add the default Obsidian pack at startup.")]
    [SerializeField] private bool addDefaultObsidianPack = true;

    [Tooltip("If none of the packs unlocks the Vacuum pixel (e.g. this component was added before it existed), " +
             "add the default Vacuum pack at startup.")]
    [SerializeField] private bool addDefaultVacuumPack = true;

    [Tooltip("If none of the packs unlocks Glass pixels (e.g. this component was added before glass existed), " +
             "add the default Glass pack at startup.")]
    [SerializeField] private bool addDefaultGlassPack = true;

    [Tooltip("If none of the packs is an auto clicker upgrade (e.g. this component was added before upgrades existed), " +
             "add the default upgrade packs at startup.")]
    [SerializeField] private bool addDefaultUpgradePacks = true;

    [Tooltip("Level display for upgrade packs. {0} = current level, {1} = max level.")]
    [SerializeField] private string levelFormat = "Level {0}/{1}";

    [Tooltip("Level display when an upgrade pack is at its max level.")]
    [SerializeField] private string maxedLevelFormat = "Level {0}/{1} (MAX)";

    [Tooltip("Colour of the level text next to the pack name.")]
    [SerializeField] private Color levelColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Tooltip("Buy button text for upgrade packs that can still be upgraded.")]
    [SerializeField] private string upgradeText = "Upgrade";

    [Tooltip("Buy button text for upgrade packs at max level.")]
    [SerializeField] private string maxedText = "Max";

    [Tooltip("If none of the packs unlocks the auto clicker (e.g. this component was added before the auto clicker existed), " +
             "add the default Auto Clicker pack at startup.")]
    [SerializeField] private bool addDefaultAutoClickerPack = true;

    [Tooltip("Show packs whose requirement isn't met yet (greyed out as 'Locked'). Off = hidden until the requirement is bought.")]
    [SerializeField] private bool showLockedPacks = false;

    [Tooltip("Buy button text for a pack whose requirement isn't met (only when 'Show Locked Packs' is on).")]
    [SerializeField] private string lockedText = "Locked";

    [Tooltip("Text shown instead of the cost for a locked pack. {0} = name of the required pack.")]
    [SerializeField] private string requiresFormat = "Requires: {0}";


    // ------------------------------------------------------------------
    // Shop button
    // ------------------------------------------------------------------

    [Header("Shop Button")]
    [Tooltip("The shop button appears once this tier is unlocked.")]
    [SerializeField] private PixelClicker.PixelType requiredTier = PixelClicker.PixelType.Black;

    [Tooltip("Show the shop button from the start, ignoring the required tier (handy for testing).")]
    [SerializeField] private bool alwaysShowButton = false;

    [Tooltip("Corner the shop button sits in.")]
    [SerializeField] private ButtonCorner buttonCorner = ButtonCorner.TopRight;

    [Tooltip("Distance of the button from the screen edge (canvas units).")]
    [SerializeField] private Vector2 buttonMargin = new Vector2(30f, 30f);

    [Tooltip("Size of the shop button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(240f, 90f);

    [Tooltip("Text on the shop button.")]
    [SerializeField] private string buttonText = "Shop";

    [Tooltip("Button text size.")]
    [SerializeField] private float buttonFontSize = 44f;

    [Tooltip("Button background colour.")]
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    // ------------------------------------------------------------------
    // Shop panel
    // ------------------------------------------------------------------

    [Header("Shop Panel")]
    [Tooltip("Title shown at the top of the panel.")]
    [SerializeField] private string panelTitle = "Shop";

    [Tooltip("Panel width (canvas units).")]
    [SerializeField] private float panelWidth = 900f;

    [Tooltip("Total height of the panel. Lists taller than the space available scroll.")]
    [SerializeField] private float panelHeight = 880f;

    [Tooltip("Height of each pack row.")]
    [SerializeField] private float rowHeight = 230f;

    [Tooltip("Gap between pack rows.")]
    [SerializeField] private float rowSpacing = 14f;

    [Tooltip("Height of the title bar.")]
    [SerializeField] private float headerHeight = 100f;

    [Tooltip("Inner padding of the panel.")]
    [SerializeField] private float panelPadding = 20f;

    [Tooltip("Panel background colour.")]
    [SerializeField] private Color panelColor = new Color(0.07f, 0.07f, 0.09f, 0.95f);

    [Tooltip("Background colour of each pack row.")]
    [SerializeField] private Color rowColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Header("Tabs")]
    [Tooltip("Tab that is open when the shop is first shown.")]
    [SerializeField] private ShopTab startTab = ShopTab.Pixels;

    [Tooltip("Label of the Pixels tab.")]
    [SerializeField] private string pixelsTabText = "Pixels";

    [Tooltip("Label of the Upgrades tab.")]
    [SerializeField] private string upgradesTabText = "Upgrades";

    [Tooltip("Label of the Consumables tab.")]
    [SerializeField] private string consumablesTabText = "Consumables";

    [Tooltip("Label of the Minigames tab.")]
    [SerializeField] private string minigamesTabText = "Minigames";

    [Tooltip("Text shown when a tab has nothing to list yet.")]
    [SerializeField] private string emptyTabText = "Nothing here yet.";

    [Tooltip("Height of the tab buttons.")]
    [SerializeField] private float tabHeight = 70f;

    [Tooltip("Gap between the tab buttons.")]
    [SerializeField] private float tabSpacing = 10f;

    [Tooltip("Tab text size.")]
    [SerializeField] private float tabFontSize = 34f;

    [Tooltip("Colour of the selected tab.")]
    [SerializeField] private Color tabActiveColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    [Tooltip("Colour of the other tabs.")]
    [SerializeField] private Color tabInactiveColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Header("Potions (Consumables tab)")]
    [Tooltip("Only list a potion once its pixel type is unlocked (or you already own some).")]
    [SerializeField] private bool potionsNeedUnlockedPixel = true;

    [Tooltip("Shown after the potion name. {0} = how many you own.")]
    [SerializeField] private string potionOwnedFormat = "Owned: {0}";

    [Header("Minigame Trackers (Minigames tab)")]
    [Tooltip("Show each minigame's tracker row (e.g. Singularity, Ghosts Caught) in the Minigames tab once the minigame is running or has progress. Titles and descriptions are set on the minigame components.")]
    [SerializeField] private bool showMinigameTrackers = true;

    [Tooltip("Tracker count while the goal has not been reached. {0} = progress so far, {1} = goal.")]
    [SerializeField] private string trackerFormat = "{0} / {1}";

    [Tooltip("Tracker count once the goal has been reached (it keeps counting). {0} = progress so far.")]
    [SerializeField] private string trackerReachedFormat = "{0}  (goal reached)";

    [Tooltip("Height of the tracker row.")]
    [SerializeField] private float trackerRowHeight = 190f;

    [Tooltip("Colour of the progress bar's empty part.")]
    [SerializeField] private Color trackerBarBackColor = new Color(0.06f, 0.06f, 0.09f, 1f);

    [Tooltip("Colour of the progress bar's filled part.")]
    [SerializeField] private Color trackerBarFillColor = new Color(0.65f, 0.3f, 0.95f, 1f);

    [Header("Upgrades Window")]
    [Tooltip("Upgrade packs (packs with levels that require another pack, e.g. the auto clicker upgrades) are not listed in a tab. " +
             "The pack they require gets an arrow that opens them in a second window.")]
    [SerializeField] private string upgradesArrowText = ">";

    [Tooltip("Size of the arrow button on a pack that has upgrades.")]
    [SerializeField] private Vector2 upgradesArrowSize = new Vector2(80f, 80f);

    [Tooltip("Arrow button colour.")]
    [SerializeField] private Color upgradesArrowColor = new Color(0.9f, 0.65f, 0.15f, 1f);

    [Tooltip("Title of the upgrades window. {0} = name of the pack the upgrades belong to.")]
    [SerializeField] private string upgradesWindowTitle = "{0} Upgrades";

    [Tooltip("Text on the back button of the upgrades window.")]
    [SerializeField] private string backText = "<";

    [Tooltip("Text shown when the upgrades window has nothing to list.")]
    [SerializeField] private string emptyUpgradesText = "No upgrades available.";

    [Header("Scrolling")]
    [Tooltip("Mouse wheel / trackpad scroll speed.")]
    [SerializeField] private float scrollSpeed = 60f;

    [Tooltip("Width of the scrollbar (it hides when the list fits).")]
    [SerializeField] private float scrollbarWidth = 24f;

    [Tooltip("Scrollbar track colour.")]
    [SerializeField] private Color scrollbarTrackColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Tooltip("Scrollbar handle colour.")]
    [SerializeField] private Color scrollbarHandleColor = new Color(0.5f, 0.5f, 0.6f, 1f);

    [Header("Panel Behaviour")]
    [Tooltip("Open the shop panel automatically the first time the button appears.")]
    [SerializeField] private bool openWhenFirstAvailable = false;

    [Header("Text")]
    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 56f;

    [Tooltip("Pack name text size.")]
    [SerializeField] private float nameFontSize = 40f;

    [Tooltip("Pack description text size.")]
    [SerializeField] private float descriptionFontSize = 26f;

    [Tooltip("Cost line text size.")]
    [SerializeField] private float costFontSize = 30f;

    [Tooltip("Normal text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Colour of a cost you can afford.")]
    [SerializeField] private Color affordableColor = new Color(0.45f, 1f, 0.5f, 1f);

    [Tooltip("Colour of a cost you can't afford yet.")]
    [SerializeField] private Color unaffordableColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Tooltip("Format of one cost entry. {0} = amount, {1} = currency name.")]
    [SerializeField] private string costEntryFormat = "{0} {1}";

    [Tooltip("Text in front of the cost list.")]
    [SerializeField] private string costPrefix = "Cost:  ";

    [Header("Buy Button")]
    [Tooltip("Size of the Buy button.")]
    [SerializeField] private Vector2 buyButtonSize = new Vector2(220f, 80f);

    [Tooltip("Text on the Buy button.")]
    [SerializeField] private string buyText = "Buy";

    [Tooltip("Text shown after a pack is bought.")]
    [SerializeField] private string ownedText = "Owned";

    [Tooltip("Buy button text size.")]
    [SerializeField] private float buyFontSize = 38f;

    [Tooltip("Buy button colour.")]
    [SerializeField] private Color buyColor = new Color(0.2f, 0.7f, 0.35f, 1f);

    [Tooltip("Colour of the Buy button when disabled (can't afford / owned).")]
    [SerializeField] private Color disabledColor = new Color(0.35f, 0.35f, 0.4f, 1f);

    // ------------------------------------------------------------------
    // Canvas
    // ------------------------------------------------------------------

    [Header("Canvas")]
    [Tooltip("Sorting order of the shop canvas.")]
    [SerializeField] private int sortingOrder = 150;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Sound / Events")]
    [Tooltip("Sound played on a successful purchase.")]
    [SerializeField] private AudioClip purchaseSound;

    [Range(0f, 1f)]
    [Tooltip("Purchase sound volume.")]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Fired after a pack is bought. Passes the pack index.")]
    public UnityEvent<int> onPackPurchased;

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
            Debug.LogError("PixelShop: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (clicker.UIFont != null) font = clicker.UIFont; // one shared font for the whole game

        if (autoClicker == null) autoClicker = GetComponent<PixelAutoClicker>();
        if (autoClicker == null)
        {
#if UNITY_2023_1_OR_NEWER
            autoClicker = FindFirstObjectByType<PixelAutoClicker>();
#else
            autoClicker = FindObjectOfType<PixelAutoClicker>();
#endif
        }
        if (autoClicker == null) autoClicker = gameObject.AddComponent<PixelAutoClicker>();

        // The built-in minigames must exist in the scene (the shop switches them on).
        EnsureMinigame<PixelGhostMinigame>();
        EnsureMinigame<PixelBlackholeMinigame>();

        if (consumables == null) consumables = GetComponent<PixelConsumables>();
        if (consumables == null)
        {
#if UNITY_2023_1_OR_NEWER
            consumables = FindFirstObjectByType<PixelConsumables>();
#else
            consumables = FindObjectOfType<PixelConsumables>();
#endif
        }
        if (consumables == null) consumables = gameObject.AddComponent<PixelConsumables>();

#if UNITY_2023_1_OR_NEWER
        if (FindObjectsByType<PixelConsumables>(FindObjectsSortMode.None).Length > 1)
#else
        if (FindObjectsOfType<PixelConsumables>().Length > 1)
#endif
            Debug.LogWarning("PixelShop: more than one PixelConsumables component exists in the scene. " +
                             "Remove the extra one so purchases and the inventory use the same potions.", this);

        EnsureDefaultPacks();

        // Make sure every reward tier exists in PixelClicker (added locked, unlocked on purchase).
        foreach (ShopPack pack in packs)
        {
            if (pack.rewardTiers == null) continue;
            foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
            {
                int tierIndex = clicker.EnsureTier(reward);
                if (reward.startingAmount > 0)
                    Debug.Log("PixelShop: " + reward.displayName + " starts at " + clicker.Tiers[tierIndex].count + ".", this);
            }
        }
    }

    private void EnsureMinigame<T>() where T : PixelMinigame
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<T>() == null) gameObject.AddComponent<T>();
#else
        if (FindObjectOfType<T>() == null) gameObject.AddComponent<T>();
#endif
    }

    /// <summary>
    /// Adds any default pack (Auto Clicker, upgrades, Glass, Vacuum) missing from the Packs list.
    /// Runs at startup and, in the Editor, when the component is loaded - so older components get the
    /// new packs as real, editable entries in the Inspector. Returns true if anything was added.
    /// </summary>
    private bool EnsureDefaultPacks()
    {
        int before = packs.Length;
        bool renamed = false;

        // Turn the old per-minigame tick boxes into minigame ids.
        foreach (ShopPack pack in packs)
        {
            if (pack.unlocksGhostMinigame) { pack.unlocksMinigame = "ghost"; pack.unlocksGhostMinigame = false; renamed = true; }
            if (pack.unlocksBlackholeMinigame) { pack.unlocksMinigame = "blackhole"; pack.unlocksBlackholeMinigame = false; renamed = true; }
            if (pack.requiresGhosts) { pack.requiresMinigameGoal = "ghost"; pack.requiresGhosts = false; renamed = true; }
            if (pack.requiresSingularity) { pack.requiresMinigameGoal = "blackhole"; pack.requiresSingularity = false; renamed = true; }
        }

        // Older components saved the previous names; Glass and Vacuum are single pixels listed on the Pixels tab.
        foreach (ShopPack pack in packs)
        {
            if (pack.rewardTiers == null) continue;
            bool isGlass = Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Glass);
            bool isVacuum = Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Vacuum);
            if (!isGlass && !isVacuum) continue;

            if (pack.displayName == "Glass Pack") { pack.displayName = "Glass Pixel"; renamed = true; }
            if (pack.displayName == "Vacuum Pack") { pack.displayName = "Vacuum Pixel"; renamed = true; }
            if (pack.tab != ShopTab.Pixels) { pack.tab = ShopTab.Pixels; renamed = true; }
        }

        if (addDefaultAutoClickerPack)
        {
            bool hasAutoPack = false;
            foreach (ShopPack pack in packs) if (pack.unlocksAutoClicker) hasAutoPack = true;
            if (!hasAutoPack)
            {
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateAutoClickerPack();
            }
        }

        if (addDefaultUpgradePacks)
        {
            int autoIndex = Array.FindIndex(packs, p => p.unlocksAutoClicker);
            bool hasInterval = false, hasClicks = false;
            foreach (ShopPack pack in packs)
            {
                if (pack.upgradeEffect == UpgradeEffect.AutoClickerInterval) hasInterval = true;
                if (pack.upgradeEffect == UpgradeEffect.AutoClickerClicks) hasClicks = true;
            }

            if (!hasInterval)
            {
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateIntervalUpgradePack(autoIndex);
            }
            if (!hasClicks)
            {
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateClicksUpgradePack(autoIndex);
            }
        }

        if (addDefaultGlassPack)
        {
            bool hasGlass = false;
            foreach (ShopPack pack in packs)
                if (pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Glass))
                    hasGlass = true;

            if (!hasGlass)
            {
                int rgbIndex = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                           Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Red));
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateGlassPack(rgbIndex);
            }
        }

        if (addDefaultVacuumPack)
        {
            bool hasVacuum = false;
            foreach (ShopPack pack in packs)
                if (pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Vacuum))
                    hasVacuum = true;

            if (!hasVacuum)
            {
                int glassIndex = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                             Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Glass));
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateVacuumPack(glassIndex);
            }
        }

        if (addDefaultObsidianPack)
        {
            bool hasObsidian = false;
            foreach (ShopPack pack in packs)
                if (pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Obsidian))
                    hasObsidian = true;

            if (!hasObsidian)
            {
                int glassIndex = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                             Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Glass));
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateObsidianPack(glassIndex);
            }
        }

        if (addDefaultLuminescentPack)
        {
            bool hasLuminescent = false;
            foreach (ShopPack pack in packs)
                if (pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Luminescent))
                    hasLuminescent = true;

            if (!hasLuminescent)
            {
                int obsidianIndex = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                                Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Obsidian));
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateLuminescentPack(obsidianIndex);
            }
        }

        if (addDefaultGhostPack)
        {
            bool hasGhost = false;
            foreach (ShopPack pack in packs) if (pack.unlocksMinigame == "ghost") hasGhost = true;

            if (!hasGhost)
            {
                int glassIndex = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                             Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Glass));
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateGhostPack(glassIndex);
            }
        }

        if (addDefaultBlackholePack)
        {
            bool hasBlackhole = false;
            foreach (ShopPack pack in packs) if (pack.unlocksMinigame == "blackhole") hasBlackhole = true;

            if (!hasBlackhole)
            {
                int vacuumIndex = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                              Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Vacuum));
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateBlackholePack(vacuumIndex);
            }
        }

        if (addDefaultSingularityPack)
        {
            bool hasSingularity = false;
            foreach (ShopPack pack in packs)
                if (pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Singularity))
                    hasSingularity = true;

            if (!hasSingularity)
            {
                int blackholeIndex = Array.FindIndex(packs, p => p.unlocksMinigame == "blackhole");
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateSingularityPack(blackholeIndex);
            }
        }

        if (addDefaultGhostPixelPack)
        {
            bool hasGhostPixel = false;
            foreach (ShopPack pack in packs)
                if (pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Ghost))
                    hasGhostPixel = true;

            if (!hasGhostPixel)
            {
                int ghostHuntIndex = Array.FindIndex(packs, p => p.unlocksMinigame == "ghost");
                Array.Resize(ref packs, packs.Length + 1);
                packs[packs.Length - 1] = CreateGhostPixelPack(ghostHuntIndex);
            }
        }

        return renamed || packs.Length != before;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: serialized data must not be changed from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultPacks()) UnityEditor.EditorUtility.SetDirty(this);

            // Make sure the helper components exist in the scene, so their settings are editable in the Inspector.
            EditorEnsureComponent<PixelConsumables>();
            EditorEnsureComponent<PixelGhostMinigame>();
            EditorEnsureComponent<PixelBlackholeMinigame>();
        };
    }

    private void EditorEnsureComponent<T>() where T : Component
    {
#if UNITY_2023_1_OR_NEWER
        bool exists = FindFirstObjectByType<T>() != null;
#else
        bool exists = FindObjectOfType<T>() != null;
#endif
        if (exists) return;
        UnityEditor.Undo.AddComponent<T>(gameObject);
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }
#endif

    private void Start()
    {
        PixelUIKit.EnsureEventSystem();
        BuildUI();
        builtOk = true;

        // Escape closes the upgrades window first, then the shop.
        PixelWindows.Register(this, 40, () => subPanelObject != null && subPanelObject.activeSelf, CloseUpgradesWindow);
        PixelWindows.Register(this, 30, () => panelObject != null && panelObject.activeSelf, () => panelObject.SetActive(false));

        // Diagnostic: where each pack is listed and what it is waiting for.
        StringBuilder log = new StringBuilder("PixelShop: " + packs.Length + " packs.");
        for (int i = 0; i < packs.Length; i++)
            log.Append("\n  [").Append(i).Append("] ").Append(packs[i].displayName)
               .Append(" -> ").Append(TabOf(packs[i]))
               .Append(IsPackRequirementMet(i) ? "" : "  (hidden until \"" + RequirementName(packs[i]) + "\" is bought)");
        Debug.Log(log.ToString(), this);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        // Packs ticked as "Purchased" in the Inspector (before or during Play) take effect too.
        foreach (ShopPack pack in packs)
        {
            if (IsLeveled(pack))
            {
                // Levels typed into the Inspector (or bought) set the auto clicker's values.
                if (pack.level != pack.appliedLevel) ApplyUpgrade(pack);
            }
            else if (pack.purchased)
            {
                ApplyPackEffects(pack);
            }
        }

        if (!builtOk) return;

        bool visible = alwaysShowButton || clicker.IsUnlocked(requiredTier);
        if (shopButtonObject.activeSelf != visible) shopButtonObject.SetActive(visible);

        if (visible && !wasButtonVisible && openWhenFirstAvailable) panelObject.SetActive(true);
        wasButtonVisible = visible;

        if (!visible && panelObject.activeSelf) panelObject.SetActive(false);
        if (!panelObject.activeSelf && subPanelObject.activeSelf) CloseUpgradesWindow();

        if (panelObject.activeSelf) RefreshRows();
    }

    // ------------------------------------------------------------------
    // Shop logic
    // ------------------------------------------------------------------

    /// <summary>True when every reward tier of the pack is already unlocked.</summary>
    private static bool IsLeveled(ShopPack pack) => pack.levels != null && pack.levels.Length > 0;

    /// <summary>The costs for the pack's next purchase (the next level for upgrade packs). Null if nothing left to buy.</summary>
    private static PackCost[] CurrentCosts(ShopPack pack)
    {
        if (!IsLeveled(pack)) return pack.costs;
        return pack.level < pack.levels.Length ? pack.levels[Mathf.Max(0, pack.level)].costs : null;
    }

    /// <summary>One-time packs: bought. Upgrade packs: at max level (nothing left to buy).</summary>
    // --- Save / load access (used by PixelSaveGame) ---

    public int PackCount => packs.Length;

    public string GetPackName(int index) => packs[index].displayName;

    public bool GetPackPurchased(int index) => packs[index].purchased;

    public int GetPackLevel(int index) => packs[index].level;

    /// <summary>Restores a pack's bought flag and level without re-applying its effect (the save restores those separately).</summary>
    public void SetPackState(int index, bool purchased, int level)
    {
        ShopPack pack = packs[index];
        pack.purchased = purchased;
        pack.level = IsLeveled(pack) ? Mathf.Clamp(level, 0, pack.levels.Length) : 0;
        pack.appliedLevel = pack.level;

        if (!purchased && !string.IsNullOrEmpty(pack.unlocksMinigame)) PixelMinigame.Find(pack.unlocksMinigame)?.Deactivate();
    }

    /// <summary>The potions this shop sells (also used by the inventory UI so both see the same stock).</summary>
    public PixelConsumables Consumables => consumables;

    public bool IsPurchased(int packIndex)
    {
        ShopPack pack = packs[packIndex];
        if (IsLeveled(pack)) return pack.level >= pack.levels.Length;
        if (pack.purchased) return true;
        if (pack.rewardTiers == null || pack.rewardTiers.Length == 0) return false;
        foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
            if (!clicker.IsUnlocked(reward.type)) return false;
        return true;
    }

    /// <summary>Owned at all: bought (one-time packs) or at least level 1 (upgrade packs).</summary>
    private bool HasPack(int packIndex)
    {
        ShopPack pack = packs[packIndex];
        return IsLeveled(pack) ? pack.level > 0 : IsPurchased(packIndex);
    }

    /// <summary>True when the pack's required pack (if any) has been bought.</summary>
    public bool IsRequirementMet(int packIndex) => IsPackRequirementMet(packIndex) && IsMinigameGoalMet(packIndex);

    /// <summary>True when the pack that must be bought first (if any) has been bought.</summary>
    public bool IsPackRequirementMet(int packIndex)
    {
        int req = packs[packIndex].requiresPackIndex;
        if (req < 0 || req >= packs.Length || req == packIndex) return true;
        return HasPack(req);
    }

    /// <summary>True unless the pack needs a minigame's goal (ghosts caught, singularity...) and it has not been reached.</summary>
    public bool IsMinigameGoalMet(int packIndex)
    {
        string id = packs[packIndex].requiresMinigameGoal;
        if (string.IsNullOrEmpty(id)) return true;

        PixelMinigame minigame = PixelMinigame.Find(id);
        return minigame != null && minigame.GoalReached;
    }

    private string RequirementText(int packIndex)
    {
        if (!IsPackRequirementMet(packIndex)) return string.Format(requiresFormat, RequirementName(packs[packIndex]));

        PixelMinigame minigame = PixelMinigame.Find(packs[packIndex].requiresMinigameGoal);
        if (minigame == null) return "";
        return string.Format(minigame.RequirementFormat, PixelClicker.FormatNumber(minigame.TrackerCount),
                             PixelClicker.FormatNumber(minigame.TrackerGoal));
    }

    /// <summary>True when the player holds enough of every currency for the pack's next purchase.</summary>
    public bool CanAfford(int packIndex)
    {
        return CanAffordCosts(CurrentCosts(packs[packIndex]));
    }

    private bool CanAffordCosts(PackCost[] costs)
    {
        if (costs == null) return true;
        foreach (PackCost cost in costs)
            if (clicker.GetCount(cost.type) < cost.amount) return false;
        return true;
    }

    /// <summary>Buys one consumable (potion or device): spends its price and adds it to the inventory.</summary>
    public bool TryBuyPotion(int itemIndex)
    {
        if (consumables == null || itemIndex < 0 || itemIndex >= consumables.ItemCount) return false;

        PackCost[] costs = consumables.ItemCosts(itemIndex);
        if (!CanAffordCosts(costs)) return false;

        if (costs != null)
            foreach (PackCost cost in costs)
                clicker.TrySpend(cost.type, cost.amount);

        consumables.AddItem(itemIndex, 1);
        Debug.Log("PixelShop: bought " + consumables.ItemName(itemIndex) + " - you now own " +
                  consumables.ItemOwned(itemIndex) + ".", this);
        PlayPurchaseSound();
        RefreshRows();
        return true;
    }

    /// <summary>Sets the auto clicker's interval / clicks to the value of the pack's current level.</summary>
    private void ApplyUpgrade(ShopPack pack)
    {
        pack.level = Mathf.Clamp(pack.level, 0, pack.levels.Length);
        pack.appliedLevel = pack.level;
        if (pack.level <= 0 || autoClicker == null) return;

        float value = pack.levels[pack.level - 1].value;
        switch (pack.upgradeEffect)
        {
            case UpgradeEffect.AutoClickerInterval: autoClicker.Interval = value; break;
            case UpgradeEffect.AutoClickerClicks: autoClicker.ClicksPerTick = Mathf.RoundToInt(value); break;
        }
    }

    /// <summary>Unlocks a pack's reward tiers and starts the auto clicker if it has one. Safe to call repeatedly.</summary>
    private void ApplyPackEffects(ShopPack pack)
    {
        if (pack.rewardTiers != null)
        {
            foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
            {
                int index = clicker.IndexOf(reward.type);
                if (index >= 0) clicker.UnlockTier(index); // no-op if already unlocked
            }
        }

        if (pack.unlocksAutoClicker && autoClicker != null) autoClicker.Activate(); // no-op if already running
        if (!string.IsNullOrEmpty(pack.unlocksMinigame)) PixelMinigame.Find(pack.unlocksMinigame)?.Activate(); // no-op if already running
    }

    /// <summary>Buys a pack: spends all costs, unlocks the reward tiers. Returns false if not possible.</summary>
    public bool TryBuy(int packIndex)
    {
        if (packIndex < 0 || packIndex >= packs.Length) return false;
        if (IsPurchased(packIndex) || !IsRequirementMet(packIndex) || !CanAfford(packIndex)) return false;

        ShopPack pack = packs[packIndex];

        PackCost[] costs = CurrentCosts(pack);
        if (costs != null)
            foreach (PackCost cost in costs)
                clicker.TrySpend(cost.type, cost.amount);

        if (IsLeveled(pack))
        {
            pack.level++;
            ApplyUpgrade(pack);
        }
        else
        {
            pack.purchased = true;
            ApplyPackEffects(pack);
        }

        PlayPurchaseSound();

        onPackPurchased?.Invoke(packIndex);
        RefreshRows();
        return true;
    }

    private void PlayPurchaseSound()
    {
        if (purchaseSound == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(purchaseSound, soundVolume);
    }
}
