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
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        hidePurchasedCache = -1;
    }

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

        [Tooltip("Spend a minigame's goal counter instead of a pixel: the minigame's id (bomb = bomb parts). Empty = spend the pixel type above.")]
        public string minigameCurrency = "";
    }

    /// <summary>What a leveled (upgrade) pack changes on the auto clicker.</summary>
    public enum UpgradeEffect
    {
        None = 0,
        /// <summary>Each level sets the auto clicker's interval (seconds) to the level's Value.</summary>
        AutoClickerInterval = 1,
        /// <summary>Each level sets the auto clicker's clicks per tick to the level's Value.</summary>
        AutoClickerClicks = 2,
        /// <summary>Level 1 switches the combo meter on; each level sets its maximum multiplier to the level's Value.</summary>
        ComboMeter = 3,
        /// <summary>Each level sets how many pixels the Pixel Bank can hold to the level's Value.</summary>
        BankCapacity = 4,
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

    /// <summary>What a requirement checks.</summary>
    public enum RequirementKind
    {
        /// <summary>Another pack must have been bought first.</summary>
        Pack = 0,
        /// <summary>A minigame's goal (e.g. ghosts caught) must have been reached.</summary>
        MinigameGoal = 1,
    }

    /// <summary>One thing that must be true before a pack can be bought. A pack can have several; all must be met.</summary>
    [Serializable]
    public class PackRequirement
    {
        [Tooltip("Pack = another pack must be bought first. Minigame Goal = a minigame's goal must be reached.")]
        public RequirementKind kind = RequirementKind.Pack;

        [Tooltip("Pack: index (in the Packs list, starting at 0) of the pack that must be bought first.")]
        public int packIndex = -1;

        [Tooltip("Minigame Goal: id of the minigame (e.g. ghost, blackhole).")]
        public string minigameId = "";
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

        [Tooltip("Everything that must be true before this pack can be bought (other packs bought, minigame goals reached). " +
                 "All of them are required. Empty = available from the start. " +
                 "For an upgrade pack (one with levels), its first Pack requirement is the pack whose upgrades window lists it.")]
        public PackRequirement[] requirements;

        /// <summary>Index of the first required pack, or -1. Upgrade packs use it to find the pack they belong to.</summary>
        public int ParentIndex
        {
            get
            {
                if (requirements != null)
                    foreach (PackRequirement r in requirements)
                        if (r.kind == RequirementKind.Pack && r.packIndex >= 0) return r.packIndex;
                return -1;
            }
        }

        [Tooltip("Buying this pack switches on the auto clicker.")]
        public bool unlocksAutoClicker = false;

        [Tooltip("Buying this pack unlocks crafting (the Crafting button and window).")]
        public bool unlocksCrafting = false;

        [Tooltip("Buying this pack lets the player click and drag old pixels.")]
        public bool unlocksGrabbing = false;

        [Tooltip("Buying this pack lets the player stop time with the T key.")]
        public bool unlocksTimeStop = false;

        [Tooltip("Buying this pack unlocks the Pixel Bank (the Bank tab, the hose and the B key).")]
        public bool unlocksBank = false;

        [Tooltip("Id of a minigame that buying this pack switches on (e.g. ghost, blackhole). Empty = none.")]
        public string unlocksMinigame = "";

        // Older versions used one tick box per minigame. They are read once and turned into the ids above.
        // Older versions had one required pack index and one minigame goal id here; they are moved into 'requirements'.
        [HideInInspector] public int requiresPackIndex = -1;
        [HideInInspector] public string requiresMinigameGoal = "";

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

    [Tooltip("The crafting window the Crafting upgrade unlocks. Found (or added) automatically if left empty.")]
    [SerializeField] private PixelCrafting crafting;

    [Tooltip("The grabbing the Pixel Grabbing upgrade unlocks. Found (or added) automatically if left empty.")]
    [SerializeField] private PixelGrab grab;

    [Tooltip("The time stop the Time Stop upgrade unlocks. Found (or added) automatically if left empty.")]
    [SerializeField] private PixelTimeStop timeStop;

    [Tooltip("The bank the Pixel Bank upgrade unlocks. Found (or added) automatically if left empty.")]
    [SerializeField] private PixelBank bank;

    [Tooltip("The combo meter the Combo Meter upgrade controls. Found (or added) automatically if left empty.")]
    [SerializeField] private PixelCombo combo;

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
    [SerializeField] private ShopPack[] packs = new ShopPack[0];

    [Tooltip("Add any built-in pack that is missing from the list (RGB, Auto Clicker, Glass, Vacuum, Obsidian...) " +
             "at startup and when the component is added in the Editor. The built-in packs are defined in PixelShopDefaults.cs. " +
             "Turn this off if you deleted a built-in pack on purpose.")]
    [SerializeField] private bool addDefaultPacks = true;

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

    [Tooltip("Make the tip boxes (the \"Got it\" boxes shown after purchases and for first-time tips) the same size as the shop panel, centred on screen. Off = they size to their text at the top of the screen.")]
    [SerializeField] private bool tipBoxMatchesShop = true;

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

    [Tooltip("Space between a tab button's edges and its text. Longer names shrink to fit inside.")]
    [SerializeField] private float tabTextPadding = 16f;

    [Tooltip("Tab text size.")]
    [SerializeField] private float tabFontSize = 34f;

    [Tooltip("Colour of the selected tab.")]
    [SerializeField] private Color tabActiveColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    [Tooltip("Colour of the other tabs.")]
    [SerializeField] private Color tabInactiveColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Header("Potions (Consumables tab)")]
    [Tooltip("Only list a potion once its pixel type is unlocked (or you already own some).")]
    [SerializeField] private bool potionsNeedUnlockedPixel = true;

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

    [Header("Consumables Tab (two purchase cards)")]
    [Tooltip("Title of the left card (potions).")]
    [SerializeField] private string potionsCardTitle = "Potions";

    [Tooltip("Title of the right card (devices you place).")]
    [SerializeField] private string utilitiesCardTitle = "Utilities";

    [Tooltip("Colour of a locked item's name in the drop-down.")]
    [SerializeField] private Color lockedItemColor = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Tooltip("Shown instead of the cost on a locked item. {0} = the pixel type that must be unlocked first.")]
    [SerializeField] private string lockedRequirementFormat = "Locked: unlock {0} first";

    [Tooltip("Text of the Buy button. {0} = how many.")]
    [SerializeField] private string buyCountFormat = "Buy x{0}";

    [Tooltip("Shown on a card that has no items.")]
    [SerializeField] private string noItemsText = "Nothing to buy here.";

    [Min(200f)]
    [Tooltip("Height of each purchase card (canvas units).")]
    [SerializeField] private float cardHeight = 440f;

    [Min(0f)]
    [Tooltip("Gap between the two cards.")]
    [SerializeField] private float cardGap = 20f;

    [Min(1)]
    [Tooltip("The most of one item you can buy in one go.")]
    [SerializeField] private int maxPerPurchase = 999;

    [Header("Pixel Upgrades (spend Ultra pixels)")]
    [Tooltip("Text of the sub-tab that lists the normal upgrades (inside the Upgrades tab).")]
    [SerializeField] private string upgradesSubTabText = "Upgrades";

    [Tooltip("Text of the sub-tab that lists the Ultra pixel boosts (inside the Upgrades tab).")]
    [SerializeField] private string pixelSubTabText = "Pixel";

    [Tooltip("Height of the two sub-tab buttons.")]
    [SerializeField] private float subTabHeight = 56f;

    [Tooltip("Name of a boost row. {0} = pixel name.")]
    [SerializeField] private string ultraRowNameFormat = "{0} Boost";

    [Tooltip("Description of a boost row. {0} = pixel name, {1} = payout multiplier now, {2} = after the next level.")]
    [SerializeField] private string ultraRowDescFormat = "Each {0} click pays x{1}  →  x{2}";

    [Tooltip("Cost line of a boost row. {0} = Ultra pixels needed, {1} = pixel name, {2} = how many you have.")]
    [SerializeField] private string ultraCostFormat = "Cost: {0} Ultra {1}   (you have {2})";

    [Tooltip("Level shown next to a boost row's name. {0} = the boost level.")]
    [SerializeField] private string ultraLevelFormat = "Level {0}";

    [Tooltip("Text of a boost row's button.")]
    [SerializeField] private string ultraButtonText = "Boost";

    [Min(1)]
    [Tooltip("Ultra pixels the first boost level of a pixel type costs.")]
    [SerializeField] private long ultraBaseCost = 1;

    [Min(0)]
    [Tooltip("Each level already bought makes the next one cost this many more Ultra pixels.")]
    [SerializeField] private long ultraCostPerLevel = 1;

    [Min(0)]
    [Tooltip("Highest boost level a pixel type can reach. 0 = no limit.")]
    [SerializeField] private int ultraMaxLevel = 20;

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

    [Header("Performance")]
    [Tooltip("Seconds between re-applying the effects of purchased packs (a safety net for packs ticked in the Inspector; normal buying applies at once).")]
    [SerializeField] private float reapplyIntervalSeconds = 0.5f;

    [Tooltip("Seconds between row refreshes while the shop is open (buying and tab changes refresh at once).")]
    [SerializeField] private float rowRefreshSeconds = 0.25f;

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
            clicker = PixelFind.First<PixelClicker>();
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
            autoClicker = PixelFind.First<PixelAutoClicker>();
        }
        if (autoClicker == null) autoClicker = gameObject.AddComponent<PixelAutoClicker>();

        // The built-in minigames must exist in the scene (the shop switches them on).
        EnsureMinigame<PixelGhostMinigame>();
        EnsureMinigame<PixelMeteorMinigame>();
        EnsureMinigame<PixelBlackholeMinigame>();
        EnsureMinigame<PixelBombMinigame>();
        EnsureMinigame<PixelPadMinigame>();

        if (grab == null) grab = PixelFind.First<PixelGrab>();
        if (grab == null) grab = gameObject.AddComponent<PixelGrab>();
        if (timeStop == null) timeStop = PixelFind.First<PixelTimeStop>();
        if (timeStop == null) timeStop = gameObject.AddComponent<PixelTimeStop>();
        if (bank == null) bank = PixelFind.First<PixelBank>();
        if (bank == null) bank = gameObject.AddComponent<PixelBank>();
        if (crafting == null) crafting = PixelFind.First<PixelCrafting>();
        if (crafting == null) crafting = gameObject.AddComponent<PixelCrafting>();
        if (combo == null) combo = PixelFind.First<PixelCombo>();
        if (combo == null) combo = gameObject.AddComponent<PixelCombo>();

        if (consumables == null) consumables = GetComponent<PixelConsumables>();
        if (consumables == null)
        {
            consumables = PixelFind.First<PixelConsumables>();
        }
        if (consumables == null) consumables = gameObject.AddComponent<PixelConsumables>();

        if (PixelFind.Count<PixelConsumables>() > 1)
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
                    PixelDebug.Info("PixelShop: " + reward.displayName + " starts at " + clicker.Tiers[tierIndex].count + ".", this);
            }
        }
    }

    private void EnsureMinigame<T>() where T : PixelMinigame
    {
        if (PixelFind.First<T>() == null) gameObject.AddComponent<T>();
    }

    /// <summary>
    /// Adds any default pack (Auto Clicker, upgrades, Glass, Vacuum) missing from the Packs list.
    /// Runs at startup and, in the Editor, when the component is loaded - so older components get the
    /// new packs as real, editable entries in the Inspector. Returns true if anything was added.
    /// </summary>
    private bool EnsureDefaultPacks()
    {
        bool changed = MigrateOldPackData();
        if (!addDefaultPacks) return changed;
        if (packs == null) packs = new ShopPack[0];

        // Built-ins come in dependency order, so each one can find the pack it requires.
        foreach (DefaultPack builtIn in BuiltInPacks)
        {
            if (Array.Exists(packs, p => builtIn.isThis(p))) continue;

            int requiredIndex = builtIn.requires != null ? Array.FindIndex(packs, p => builtIn.requires(p)) : -1;
            Array.Resize(ref packs, packs.Length + 1);
            packs[packs.Length - 1] = builtIn.create(requiredIndex);
            changed = true;
        }
        return changed;
    }

    /// <summary>Brings packs saved by older versions up to date (renames, tab, minigame ids). Returns true if anything changed.</summary>
    private bool MigrateOldPackData()
    {
        bool renamed = false;

        // Turn the old per-minigame tick boxes into minigame ids.
        foreach (ShopPack pack in packs)
        {
            if (pack.unlocksGhostMinigame) { pack.unlocksMinigame = "ghost"; pack.unlocksGhostMinigame = false; renamed = true; }
            if (pack.unlocksBlackholeMinigame) { pack.unlocksMinigame = "blackhole"; pack.unlocksBlackholeMinigame = false; renamed = true; }
            if (pack.requiresGhosts) { pack.requiresMinigameGoal = "ghost"; pack.requiresGhosts = false; renamed = true; }
            if (pack.requiresSingularity) { pack.requiresMinigameGoal = "blackhole"; pack.requiresSingularity = false; renamed = true; }
        }

        // Turn the old single requirement fields into the requirements list.
        foreach (ShopPack pack in packs)
        {
            bool hadPack = pack.requiresPackIndex >= 0;
            bool hadGoal = !string.IsNullOrEmpty(pack.requiresMinigameGoal);
            if (!hadPack && !hadGoal) continue;

            System.Collections.Generic.List<PackRequirement> list = new System.Collections.Generic.List<PackRequirement>();
            if (pack.requirements != null) list.AddRange(pack.requirements);
            if (hadPack) list.Add(new PackRequirement { kind = RequirementKind.Pack, packIndex = pack.requiresPackIndex });
            if (hadGoal) list.Add(new PackRequirement { kind = RequirementKind.MinigameGoal, minigameId = pack.requiresMinigameGoal });

            pack.requirements = list.ToArray();
            pack.requiresPackIndex = -1;
            pack.requiresMinigameGoal = "";
            renamed = true;
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

        return renamed;
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
            EditorEnsureComponent<PixelCombo>();
            EditorEnsureComponent<PixelCrafting>();
            EditorEnsureComponent<PixelGrab>();
            EditorEnsureComponent<PixelTimeStop>();
            EditorEnsureComponent<PixelBank>();
            EditorEnsureComponent<PixelGhostMinigame>();
            EditorEnsureComponent<PixelMeteorMinigame>();
            EditorEnsureComponent<PixelBlackholeMinigame>();
            EditorEnsureComponent<PixelBombMinigame>();
            EditorEnsureComponent<PixelPadMinigame>();
        };
    }

    private void EditorEnsureComponent<T>() where T : Component
    {
        bool exists = PixelFind.First<T>() != null;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnostic: where each pack is listed and what it is waiting for.
        StringBuilder log = new StringBuilder("PixelShop: " + packs.Length + " packs.");
        for (int i = 0; i < packs.Length; i++)
            log.Append("\n  [").Append(i).Append("] ").Append(packs[i].displayName)
               .Append(" -> ").Append(TabOf(packs[i]))
               .Append(IsPackRequirementMet(i) ? "" : "  (hidden until \"" + RequirementName(packs[i]) + "\" is bought)");
        PixelDebug.Info(log.ToString(), this);
#endif
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private float nextReapplyTime;
    private float nextRowRefreshTime;

    private void Update()
    {
        // Packs ticked as "Purchased" in the Inspector (before or during Play) take effect too.
        // Re-applying purchased packs is only a safety net (buying applies at once), so it runs on a timer.
        bool reapply = Time.unscaledTime >= nextReapplyTime;
        if (reapply) nextReapplyTime = Time.unscaledTime + reapplyIntervalSeconds;
        foreach (ShopPack pack in packs)
        {
            if (IsLeveled(pack))
            {
                // Levels typed into the Inspector (or bought) set the auto clicker's values.
                if (pack.level != pack.appliedLevel) ApplyUpgrade(pack);
            }
            else if (reapply && pack.purchased)
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

        if (panelObject.activeSelf && Time.unscaledTime >= nextRowRefreshTime)
        {
            nextRowRefreshTime = Time.unscaledTime + rowRefreshSeconds;
            RefreshRows();
        }
    }

    // ------------------------------------------------------------------
    // Shop logic
    // ------------------------------------------------------------------

    /// <summary>True when every reward tier of the pack is already unlocked.</summary>
    private const string PrefHidePurchased = "PixelClicker.Setting.HidePurchased";
    private static int hidePurchasedCache = -1; // -1 = not read yet

    /// <summary>Player setting: hide shop items that are already bought (or maxed out). Remembered between sessions.</summary>
    public static bool HidePurchased
    {
        get
        {
            if (hidePurchasedCache < 0) hidePurchasedCache = PlayerPrefs.GetInt(PrefHidePurchased, 0) != 0 ? 1 : 0;
            return hidePurchasedCache != 0;
        }
        set { hidePurchasedCache = value ? 1 : 0; PlayerPrefs.SetInt(PrefHidePurchased, hidePurchasedCache); }
    }

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
        if (pack.upgradeEffect == UpgradeEffect.ComboMeter || pack.upgradeEffect == UpgradeEffect.BankCapacity) ApplyUpgrade(pack); // not stored anywhere else, so re-apply on load

        if (pack.unlocksCrafting && crafting != null && !purchased) crafting.Deactivate();
        if (pack.unlocksGrabbing && grab != null && !purchased) grab.Deactivate();
        if (pack.unlocksTimeStop && timeStop != null && !purchased) timeStop.Deactivate();
        if (pack.unlocksBank && bank != null && !purchased) bank.Deactivate();
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

    /// <summary>True when every pack this pack requires (if any) has been bought.</summary>
    public bool IsPackRequirementMet(int packIndex)
    {
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return true;

        foreach (PackRequirement r in requirements)
            if (r.kind == RequirementKind.Pack && IsRequiredPackMissing(r, packIndex)) return false;
        return true;
    }

    /// <summary>True unless the pack needs a minigame's goal (ghosts caught, singularity...) and it has not been reached.</summary>
    public bool IsMinigameGoalMet(int packIndex)
    {
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return true;

        foreach (PackRequirement r in requirements)
            if (r.kind == RequirementKind.MinigameGoal && !IsGoalReached(r)) return false;
        return true;
    }

    private bool IsRequiredPackMissing(PackRequirement r, int ownIndex)
    {
        if (r.packIndex < 0 || r.packIndex >= packs.Length || r.packIndex == ownIndex) return false;
        return !HasPack(r.packIndex);
    }

    private static bool IsGoalReached(PackRequirement r)
    {
        PixelMinigame minigame = PixelMinigame.Find(r.minigameId);
        return minigame != null && minigame.GoalReached;
    }

    /// <summary>The text for the first requirement that is not met yet ("Requires: ...").</summary>
    private string RequirementText(int packIndex)
    {
        PackRequirement[] requirements = packs[packIndex].requirements;
        if (requirements == null) return "";

        foreach (PackRequirement r in requirements)
        {
            if (r.kind == RequirementKind.Pack)
            {
                if (IsRequiredPackMissing(r, packIndex)) return string.Format(requiresFormat, packs[r.packIndex].displayName);
            }
            else if (!IsGoalReached(r))
            {
                PixelMinigame minigame = PixelMinigame.Find(r.minigameId);
                if (minigame == null) return "";
                return string.Format(minigame.RequirementFormat, PixelClicker.FormatNumber(minigame.TrackerCount),
                                     PixelClicker.FormatNumber(minigame.TrackerGoal));
            }
        }
        return "";
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
            if (!CanAffordCost(cost)) return false;
        return true;
    }

    /// <summary>Can the player pay this one line of a price (a pixel type, or a minigame counter like bomb parts)?</summary>
    private bool CanAffordCost(PackCost cost)
    {
        if (string.IsNullOrEmpty(cost.minigameCurrency)) return clicker.CanAfford(cost.type, cost.amount);
        if (PixelClicker.InfiniteResources) return true;
        PixelMinigame m = PixelMinigame.Find(cost.minigameCurrency);
        return m != null && m.HasTracker && m.SpendableCount >= cost.amount;
    }

    /// <summary>Pays one line of a price.</summary>
    private void SpendCost(PackCost cost)
    {
        if (string.IsNullOrEmpty(cost.minigameCurrency)) { clicker.TrySpend(cost.type, cost.amount); return; }
        if (PixelClicker.InfiniteResources) return;
        PixelMinigame m = PixelMinigame.Find(cost.minigameCurrency);
        if (m != null) m.TrySpendTracker(cost.amount);
    }

    /// <summary>Name of what a price line is paid in (a pixel's name, or the minigame counter's title).</summary>
    private string CostName(PackCost cost)
    {
        if (!string.IsNullOrEmpty(cost.minigameCurrency))
        {
            PixelMinigame m = PixelMinigame.Find(cost.minigameCurrency);
            return m != null && !string.IsNullOrEmpty(m.TrackerTitle) ? m.TrackerTitle : cost.minigameCurrency;
        }
        int tierIndex = clicker.IndexOf(cost.type);
        return tierIndex >= 0 ? clicker.Tiers[tierIndex].displayName : cost.type.ToString();
    }

    /// <summary>Buys one of a potion / device (see <see cref="TryBuyItems"/>).</summary>
    public bool TryBuyPotion(int itemIndex) => TryBuyItems(itemIndex, 1);

    /// <summary>Buys several of a potion / device at once. Returns false if it is locked or you can't afford all of them.</summary>
    public bool TryBuyItems(int itemIndex, int count)
    {
        if (consumables == null || itemIndex < 0 || itemIndex >= consumables.ItemCount || count < 1) return false;
        if (consumables.ItemCraftOnly(itemIndex) || ItemLocked(itemIndex)) return false;

        PackCost[] total = ScaleCosts(consumables.ItemCosts(itemIndex), count);
        if (!CanAffordCosts(total)) return false;

        foreach (PackCost cost in total) SpendCost(cost);

        consumables.AddItem(itemIndex, count);
        PixelDebug.Info("PixelShop: bought " + count + " x " + consumables.ItemName(itemIndex) + " - you now own " +
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

        if (pack.upgradeEffect == UpgradeEffect.ComboMeter)
        {
            if (combo != null) combo.SetUpgrade(pack.level > 0, pack.level > 0 ? pack.levels[pack.level - 1].value : 0f);
            return;
        }
        if (pack.upgradeEffect == UpgradeEffect.BankCapacity)
        {
            if (bank != null) bank.SetCapacityOverride(pack.level > 0 ? Mathf.RoundToInt(pack.levels[pack.level - 1].value) : 0);
            return;
        }
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
        if (pack.unlocksCrafting && crafting != null) crafting.Activate();
        if (pack.unlocksGrabbing && grab != null) grab.Activate();
        if (pack.unlocksTimeStop && timeStop != null) timeStop.Activate();
        if (pack.unlocksBank && bank != null) bank.Activate();
        if (!string.IsNullOrEmpty(pack.unlocksMinigame)) ActivateMinigame(pack.unlocksMinigame); // no-op if already running
    }

    /// <summary>Ultra pixels the next boost level of a pixel type costs.</summary>
    public long UltraBoostCost(int tierIndex) =>
        ultraBaseCost + (clicker.IsValidTierIndex(tierIndex) ? clicker.Tiers[tierIndex].ultraLevel : 0) * ultraCostPerLevel;

    /// <summary>True when a pixel type has reached its highest boost level.</summary>
    public bool UltraBoostMaxed(int tierIndex) =>
        ultraMaxLevel > 0 && clicker.IsValidTierIndex(tierIndex) && clicker.Tiers[tierIndex].ultraLevel >= ultraMaxLevel;

    /// <summary>Spends Ultra pixels to boost a pixel type's payout. Returns false if it is maxed or you can't afford it.</summary>
    public bool TryBuyUltraBoost(int tierIndex)
    {
        if (UltraBoostMaxed(tierIndex)) return false;
        bool ok = clicker.TryBuyUltraBoost(tierIndex, UltraBoostCost(tierIndex));
        if (ok)
        {
            PlayPurchaseSound();
            if (clicker.IsValidTierIndex(tierIndex))
            {
                string boostLine = "Ultra boost bought: " + clicker.Tiers[tierIndex].displayName + " level " + clicker.Tiers[tierIndex].ultraLevel + "!";
                PixelHints.Announce(boostLine, boostLine);
            }
            PixelHints.Trigger("upgrade_ultra");
        }
        return ok;
    }

    /// <summary>Starts a minigame unless the player switched it off.</summary>
    private static void ActivateMinigame(string id)
    {
        PixelMinigame m = PixelMinigame.Find(id);
        if (m != null && !m.UserDisabled) m.Activate();
    }

    /// <summary>
    /// Dev tools: skips the pre-shop part of the game. Unlocks the Gray and Black pixels, and buys the RGB pack and the
    /// Auto Clicker for free (no costs, requirements ignored).
    /// </summary>
    public void DevSkipIntro()
    {
        foreach (PixelClicker.PixelType type in new[] { PixelClicker.PixelType.White, PixelClicker.PixelType.Gray, PixelClicker.PixelType.Black })
        {
            int index = clicker.IndexOf(type);
            if (index >= 0) clicker.UnlockTier(index); // no-op if already unlocked
        }

        for (int i = 0; i < packs.Length; i++)
        {
            ShopPack pack = packs[i];
            if (pack.purchased || IsLeveled(pack)) continue;

            bool givesRed = pack.rewardTiers != null && System.Array.Exists(pack.rewardTiers, t => t.type == PixelClicker.PixelType.Red);
            if (!givesRed && !pack.unlocksAutoClicker) continue;

            pack.purchased = true;
            ApplyPackEffects(pack);
            onPackPurchased?.Invoke(i);
        }
        RefreshRows();
    }

    /// <summary>Dev tools: unlocks every pack for free (leveled packs go to their top level). Ignores requirements and costs.</summary>
    public void DevUnlockAll()
    {
        for (int i = 0; i < packs.Length; i++)
        {
            ShopPack pack = packs[i];
            if (IsLeveled(pack))
            {
                if (pack.level >= pack.levels.Length) continue;
                pack.level = pack.levels.Length;
                ApplyUpgrade(pack);
            }
            else
            {
                if (pack.purchased) continue;
                pack.purchased = true;
                ApplyPackEffects(pack);
            }
            onPackPurchased?.Invoke(i);
        }
        RefreshRows();
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
                SpendCost(cost);

        if (IsLeveled(pack))
        {
            pack.level++;
            AnnouncePurchase(pack, pack.displayName + " upgraded to level " + pack.level + "!");
            ApplyUpgrade(pack);
            switch (pack.upgradeEffect)
            {
                case UpgradeEffect.AutoClickerInterval: PixelHints.Trigger("upgrade_interval"); break;
                case UpgradeEffect.AutoClickerClicks: PixelHints.Trigger("upgrade_clicks"); break;
                case UpgradeEffect.ComboMeter: PixelHints.Trigger("upgrade_combo"); break;
                case UpgradeEffect.BankCapacity: PixelHints.Trigger("upgrade_bank"); break;
            }
        }
        else
        {
            pack.purchased = true;
            AnnouncePurchase(pack, "You bought " + pack.displayName + "!");
            ApplyPackEffects(pack);

            if (pack.unlocksCrafting) PixelHints.Trigger("crafting");
            if (pack.unlocksGrabbing) PixelHints.Trigger("grab");
            if (pack.unlocksTimeStop) PixelHints.Trigger("timestop");
            if (pack.unlocksBank) PixelHints.Trigger("bank");
            if (pack.rewardTiers != null)
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                    PixelHints.Trigger("pixel_" + reward.type);

            // The auto clicker starts switched off: a box points the player to the Toggles window to turn it on.
            if (pack.unlocksAutoClicker && autoClicker != null)
            {
                autoClicker.UserDisabled = true;
                PixelToggles.ShowHint();
                PixelHints.Announce("You unlocked the Auto Clicker!", PixelToggles.HintMessage);
            }
        }

        PlayPurchaseSound();

        onPackPurchased?.Invoke(packIndex);
        RefreshRows();
        return true;
    }

    /// <summary>The bottom-left event line for a purchase; clicking it (or Enter) shows the pack's name and description.</summary>
    private void AnnouncePurchase(ShopPack pack, string line)
    {
        string info = "<b>" + pack.displayName + "</b>\n" + ResolveDescription(pack);
        PixelHints.Announce(line, info);
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
