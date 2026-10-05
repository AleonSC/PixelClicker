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
public class PixelShop : MonoBehaviour
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
        CreateLuminescentPack(6)
    };

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

    /// <summary>Default RGB pack: 100 White + 100 Gray + 100 Black for Red, Green and Blue.</summary>
    private static ShopPack CreateRgbPack()
    {
        return new ShopPack
        {
            displayName = "RGB Pack",
            tab = ShopTab.Pixels,
            description = "Adds Red, Green and Blue pixels to the random spawn pool.",
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.White, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Gray,  amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Black, amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Red, displayName = "Red Pixels", color = Color.red,
                    amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Green, displayName = "Green Pixels", color = Color.green,
                    amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Blue, displayName = "Blue Pixels", color = Color.blue,
                    amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Glass pack: unlocks see-through Glass pixels for 100 Red + 100 Green + 100 Blue. Needs the RGB pack first.</summary>
    private static ShopPack CreateGlassPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Glass Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the see-through Glass pixel to the random spawn pool.",
            requiresPackIndex = requiresRgbIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Glass, displayName = "Glass Pixels",
                    color = new Color(0.7f, 0.92f, 1f, 0.35f), translucent = true,
                    amountPerClick = 1, spawnWeight = 0.5f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Vacuum pack: a rare pixel that re-collects every old pixel. Needs the Glass pack first.</summary>
    private static ShopPack CreateVacuumPack(int requiresGlassIndex)
    {
        return new ShopPack
        {
            displayName = "Vacuum Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the rare Vacuum pixel. Clicking it sucks up every old pixel and collects them again.",
            requiresPackIndex = requiresGlassIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Vacuum, displayName = "Vacuum Pixels",
                    color = new Color(0.65f, 0.3f, 0.95f, 1f), vacuum = true,
                    amountPerClick = 1, spawnWeight = 0.2f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Obsidian pack: a tough pixel that takes several clicks to collect but pays more. Needs the Glass pack first.</summary>
    private static ShopPack CreateObsidianPack(int requiresGlassIndex)
    {
        return new ShopPack
        {
            displayName = "Obsidian Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the tough Obsidian pixel. It takes {clicks} clicks to collect, but pays more.",
            requiresPackIndex = requiresGlassIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Black, amount = 500 },
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Obsidian, displayName = "Obsidian Pixels",
                    color = new Color(0.22f, 0.1f, 0.35f, 1f),
                    amountPerClick = 5, clicksToCollect = 5, spawnWeight = 0.4f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Luminescent pack: a glowing pixel that pays more. Needs the Obsidian pack first.</summary>
    private static ShopPack CreateLuminescentPack(int requiresObsidianIndex)
    {
        return new ShopPack
        {
            displayName = "Luminescent Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the glowing Luminescent pixel, which pays more per click.",
            requiresPackIndex = requiresObsidianIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 200 },
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 25 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Luminescent, displayName = "Luminescent Pixels",
                    color = new Color(0.4f, 1f, 0.7f, 1f), glow = true, glowIntensity = 2.5f,
                    amountPerClick = 3, spawnWeight = 0.5f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    private static PackCost[] AllSix(double amount)
    {
        return new[]
        {
            new PackCost { type = PixelClicker.PixelType.White, amount = amount },
            new PackCost { type = PixelClicker.PixelType.Gray,  amount = amount },
            new PackCost { type = PixelClicker.PixelType.Black, amount = amount },
            new PackCost { type = PixelClicker.PixelType.Red,   amount = amount },
            new PackCost { type = PixelClicker.PixelType.Green, amount = amount },
            new PackCost { type = PixelClicker.PixelType.Blue,  amount = amount },
        };
    }

    /// <summary>Default "quicker interval" upgrade: 5 levels, each costing more of all six pixel types.</summary>
    private static ShopPack CreateIntervalUpgradePack(int requiresAutoClickerIndex)
    {
        return new ShopPack
        {
            displayName = "Faster Clicking",
            tab = ShopTab.Upgrades,
            description = "Auto clicker interval: {current}s  →  {next}s",
            requiresPackIndex = requiresAutoClickerIndex,
            upgradeEffect = UpgradeEffect.AutoClickerInterval,
            levels = new[]
            {
                new PackLevel { value = 0.8f,  costs = AllSix(200) },
                new PackLevel { value = 0.6f,  costs = AllSix(400) },
                new PackLevel { value = 0.45f, costs = AllSix(800) },
                new PackLevel { value = 0.3f,  costs = AllSix(1600) },
                new PackLevel { value = 0.2f,  costs = AllSix(3200) },
            }
        };
    }

    /// <summary>Default "extra clicks" upgrade: 4 levels, +1 click per tick each.</summary>
    private static ShopPack CreateClicksUpgradePack(int requiresAutoClickerIndex)
    {
        return new ShopPack
        {
            displayName = "Multi-Click",
            tab = ShopTab.Upgrades,
            description = "Clicks per auto click: {current}  →  {next}",
            requiresPackIndex = requiresAutoClickerIndex,
            upgradeEffect = UpgradeEffect.AutoClickerClicks,
            levels = new[]
            {
                new PackLevel { value = 2f, costs = AllSix(300) },
                new PackLevel { value = 3f, costs = AllSix(600) },
                new PackLevel { value = 4f, costs = AllSix(1200) },
                new PackLevel { value = 5f, costs = AllSix(2400) },
            }
        };
    }

    /// <summary>Default Auto Clicker pack: all six pixel types, available after the RGB pack (index 0).</summary>
    private static ShopPack CreateAutoClickerPack()
    {
        return new ShopPack
        {
            displayName = "Auto Clicker",
            tab = ShopTab.Upgrades,
            description = "Clicks the cube for you every {interval} seconds.",
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.White, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Gray,  amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Black, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 100 },
            },
            rewardTiers = new PixelClicker.PixelTier[0],
            requiresPackIndex = 0,
            unlocksAutoClicker = true,
        };
    }

#if UNITY_EDITOR
    /// <summary>Right-click the component header > Add Default Auto Clicker Pack, to make it editable in the list.</summary>
    [ContextMenu("Add Default Vacuum Pack To List")]
    private void AddVacuumPackToList()
    {
        int glass = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Glass));
        UnityEditor.Undo.RecordObject(this, "Add Vacuum Pack");
        Array.Resize(ref packs, packs.Length + 1);
        packs[packs.Length - 1] = CreateVacuumPack(glass);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Default Glass Pack To List")]
    private void AddGlassPackToList()
    {
        int rgb = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                              Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Red));
        UnityEditor.Undo.RecordObject(this, "Add Glass Pack");
        Array.Resize(ref packs, packs.Length + 1);
        packs[packs.Length - 1] = CreateGlassPack(rgb);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Default Auto Clicker Upgrade Packs To List")]
    private void AddUpgradePacksToList()
    {
        int auto = Array.FindIndex(packs, p => p.unlocksAutoClicker);
        UnityEditor.Undo.RecordObject(this, "Add Upgrade Packs");
        Array.Resize(ref packs, packs.Length + 2);
        packs[packs.Length - 2] = CreateIntervalUpgradePack(auto);
        packs[packs.Length - 1] = CreateClicksUpgradePack(auto);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Default Auto Clicker Pack To List")]
    private void AddAutoClickerPackToList()
    {
        UnityEditor.Undo.RecordObject(this, "Add Auto Clicker Pack");
        Array.Resize(ref packs, packs.Length + 1);
        packs[packs.Length - 1] = CreateAutoClickerPack();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

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
    // Runtime
    // ------------------------------------------------------------------

    private class PackRow
    {
        public RectTransform rect;
        public TMP_Text nameLabel;
        public TMP_Text descLabel;
        public Button buyButton;
        public Image buyImage;
        public TMP_Text buyLabel;
        public TMP_Text costLabel;
        public Button arrowButton;
        public bool isPotion;
    }

    private RectTransform panelRect;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private TMP_Text emptyLabel;
    private Image[] tabImages;
    private ShopTab currentTab = ShopTab.Pixels;
    private GameObject subPanelObject;
    private RectTransform subContentRect;
    private ScrollRect subScrollRect;
    private TMP_Text subEmptyLabel;
    private TMP_Text subTitle;
    private int openParent = -1;
    private PackRow[] potionRows;
    private GameObject canvasRoot;
    private GameObject shopButtonObject;
    private GameObject panelObject;
    private PackRow[] rows;
    private bool builtOk;
    private bool wasButtonVisible;
    private AudioSource audioSource;

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

    /// <summary>
    /// Adds any default pack (Auto Clicker, upgrades, Glass, Vacuum) missing from the Packs list.
    /// Runs at startup and, in the Editor, when the component is loaded - so older components get the
    /// new packs as real, editable entries in the Inspector. Returns true if anything was added.
    /// </summary>
    private bool EnsureDefaultPacks()
    {
        int before = packs.Length;
        bool renamed = false;

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

            // Make sure the potions exist as an editable component on the scene.
#if UNITY_2023_1_OR_NEWER
            bool hasConsumables = FindFirstObjectByType<PixelConsumables>() != null;
#else
            bool hasConsumables = FindObjectOfType<PixelConsumables>() != null;
#endif
            if (!hasConsumables)
            {
                UnityEditor.Undo.AddComponent<PixelConsumables>(gameObject);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
        };
    }
#endif

    private void Start()
    {
        EnsureEventSystem();
        BuildUI();
        builtOk = true;

        // Diagnostic: where each pack is listed and what it is waiting for.
        StringBuilder log = new StringBuilder("PixelShop: " + packs.Length + " packs.");
        for (int i = 0; i < packs.Length; i++)
            log.Append("\n  [").Append(i).Append("] ").Append(packs[i].displayName)
               .Append(" -> ").Append(TabOf(packs[i]))
               .Append(IsRequirementMet(i) ? "" : "  (hidden until \"" + RequirementName(packs[i]) + "\" is bought)");
        Debug.Log(log.ToString(), this);
    }

    private void OnDestroy()
    {
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
    public bool IsRequirementMet(int packIndex)
    {
        int req = packs[packIndex].requiresPackIndex;
        if (req < 0 || req >= packs.Length || req == packIndex) return true;
        return HasPack(req);
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

    /// <summary>Buys one potion: spends its price and adds it to the inventory. Returns false if you can't afford it.</summary>
    public bool TryBuyPotion(int potionIndex)
    {
        if (consumables == null || potionIndex < 0 || potionIndex >= consumables.Count) return false;

        PixelConsumables.Potion potion = consumables.Get(potionIndex);
        if (!CanAffordCosts(potion.costs)) return false;

        if (potion.costs != null)
            foreach (PackCost cost in potion.costs)
                clicker.TrySpend(cost.type, cost.amount);

        consumables.Add(potionIndex, 1);
        Debug.Log("PixelShop: bought " + potion.displayName + " - you now own " + potion.owned + ".", this);
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

    // ------------------------------------------------------------------
    // UI building
    // ------------------------------------------------------------------

    private static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (FindObjectOfType<EventSystem>() != null) return;
#endif
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildUI()
    {
        canvasRoot = new GameObject("PixelShop Canvas");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        BuildShopButton(canvasRoot.transform);
        BuildPanel(canvasRoot.transform);
    }

    private void BuildShopButton(Transform parent)
    {
        Vector2 anchor = new Vector2(
            buttonCorner == ButtonCorner.TopRight || buttonCorner == ButtonCorner.BottomRight ? 1f : 0f,
            buttonCorner == ButtonCorner.TopLeft || buttonCorner == ButtonCorner.TopRight ? 1f : 0f);

        Button button = CreateButton(parent, "Shop Button", buttonText, buttonSize, buttonColor,
                                     buttonTextColor, buttonFontSize, out _, out _);
        shopButtonObject = button.gameObject;

        RectTransform rt = shopButtonObject.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = new Vector2(anchor.x > 0.5f ? -buttonMargin.x : buttonMargin.x,
                                          anchor.y > 0.5f ? -buttonMargin.y : buttonMargin.y);

        button.onClick.AddListener(() =>
        {
            panelObject.SetActive(!panelObject.activeSelf);
            if (panelObject.activeSelf) RefreshRows();
        });
        shopButtonObject.SetActive(false); // Update() reveals it once the required tier is unlocked.
    }

    private void BuildPanel(Transform parent)
    {
        currentTab = startTab == ShopTab.Automatic ? ShopTab.Pixels : startTab;

        panelObject = new GameObject("Shop Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        panelObject.GetComponent<Image>().color = panelColor;

        // Title
        TMP_Text title = CreateText(panelObject.transform, "Title", panelTitle, titleFontSize,
                                    TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-(panelPadding * 2f + 160f), headerHeight);
        tr.anchoredPosition = Vector2.zero;

        // Close button
        Button close = CreateButton(panelObject.transform, "Close", "X", new Vector2(80f, 80f),
                                    disabledColor, textColor, 40f, out _, out _);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
        close.onClick.AddListener(() => panelObject.SetActive(false));

        BuildTabs(panelObject.transform);
        BuildScrollArea(panelObject.transform, headerHeight + tabHeight + panelPadding * 0.5f,
                        out contentRect, out scrollRect, out emptyLabel);
        BuildUpgradesWindow(parent);

        // Pack rows live inside the scrolling content.
        rows = new PackRow[packs.Length];
        for (int i = 0; i < packs.Length; i++)
            rows[i] = BuildRow(IsChild(i) ? subContentRect : contentRect, i);

        // One row per potion, listed on the Consumables tab.
        int potionCount = consumables != null ? consumables.Count : 0;
        potionRows = new PackRow[potionCount];
        for (int i = 0; i < potionCount; i++)
            potionRows[i] = BuildRow(contentRect, i, true);

        panelObject.SetActive(false);
        subPanelObject.SetActive(false);
    }

    private void BuildTabs(Transform parent)
    {
        string[] names = { pixelsTabText, upgradesTabText, consumablesTabText };
        ShopTab[] tabs = { ShopTab.Pixels, ShopTab.Upgrades, ShopTab.Consumables };
        tabImages = new Image[tabs.Length];

        GameObject bar = new GameObject("Tabs", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(-panelPadding * 2f, tabHeight);
        barRect.anchoredPosition = new Vector2(0f, -headerHeight);

        float slice = 1f / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            Button tabButton = CreateButton(bar.transform, "Tab " + names[i], names[i], Vector2.zero,
                                            tabInactiveColor, textColor, tabFontSize, out _, out tabImages[i]);
            RectTransform rt = tabButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(i * slice, 0f);
            rt.anchorMax = new Vector2((i + 1) * slice, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(i == 0 ? 0f : tabSpacing * 0.5f, 0f);
            rt.offsetMax = new Vector2(i == tabs.Length - 1 ? 0f : -tabSpacing * 0.5f, 0f);

            ShopTab captured = tabs[i];
            tabButton.onClick.AddListener(() => SelectTab(captured));
        }
    }

    /// <summary>Builds a scrolling list (view, content, scrollbar, empty message) below 'top' units from the panel's top edge.</summary>
    private void BuildScrollArea(Transform parent, float top, out RectTransform content, out ScrollRect scroll, out TMP_Text empty)
    {
        // Scroll view (also the viewport; the transparent image lets empty space take wheel/drag input).
        GameObject scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(Image),
                                             typeof(RectMask2D), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        RectTransform sr = scrollGo.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.offsetMin = new Vector2(panelPadding, panelPadding);
        sr.offsetMax = new Vector2(-(panelPadding + scrollbarWidth + 8f), -top);

        // Content
        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(scrollGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        // Scrollbar
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(parent, false);
        barGo.GetComponent<Image>().color = scrollbarTrackColor;
        RectTransform br = barGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.offsetMin = new Vector2(-(panelPadding + scrollbarWidth), panelPadding);
        br.offsetMax = new Vector2(-panelPadding, -top);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(barGo.transform, false);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = scrollbarHandleColor;
        RectTransform hr = handleGo.GetComponent<RectTransform>();
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = hr;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Scroll behaviour
        scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = scrollSpeed;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // "Nothing here yet" message for empty tabs.
        empty = CreateText(scrollGo.transform, "Empty", emptyTabText, descriptionFontSize + 6f,
                                TextAlignmentOptions.Center, FontStyles.Italic);
        empty.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        RectTransform er = empty.rectTransform;
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = er.offsetMax = Vector2.zero;
        empty.gameObject.SetActive(false);
    }

    /// <summary>The second window: lists the upgrade packs that belong to the pack whose arrow was pressed.</summary>
    private void BuildUpgradesWindow(Transform parent)
    {
        subPanelObject = new GameObject("Upgrades Window", typeof(RectTransform), typeof(Image));
        subPanelObject.transform.SetParent(parent, false);
        RectTransform pr = subPanelObject.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(panelWidth, panelHeight);
        pr.anchoredPosition = Vector2.zero;
        subPanelObject.GetComponent<Image>().color = panelColor;

        subTitle = CreateText(subPanelObject.transform, "Title", "", titleFontSize,
                              TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform tr = subTitle.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-(panelPadding * 2f + 200f), headerHeight);
        tr.anchoredPosition = Vector2.zero;

        // Back arrow (top-left) and close X (top-right) both return to the shop.
        Button back = CreateButton(subPanelObject.transform, "Back", backText, new Vector2(80f, 80f),
                                   disabledColor, textColor, 40f, out _, out _);
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
        br.anchoredPosition = new Vector2(panelPadding, -panelPadding * 0.5f);
        back.onClick.AddListener(CloseUpgradesWindow);

        Button close = CreateButton(subPanelObject.transform, "Close", "X", new Vector2(80f, 80f),
                                    disabledColor, textColor, 40f, out _, out _);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
        close.onClick.AddListener(CloseUpgradesWindow);

        BuildScrollArea(subPanelObject.transform, headerHeight + panelPadding * 0.5f,
                        out subContentRect, out subScrollRect, out subEmptyLabel);
        subEmptyLabel.text = emptyUpgradesText;
    }

    /// <summary>Opens the upgrades window for the pack at the given index.</summary>
    public void OpenUpgradesWindow(int parentIndex)
    {
        openParent = parentIndex;
        subContentRect.anchoredPosition = Vector2.zero;
        subScrollRect.StopMovement();
        subPanelObject.SetActive(true);
        RefreshRows();
    }

    public void CloseUpgradesWindow()
    {
        openParent = -1;
        subPanelObject.SetActive(false);
    }

    /// <summary>Upgrade packs (leveled packs that require another pack) are listed in that pack's upgrades window, not in a tab.</summary>
    private bool IsChild(int index)
    {
        ShopPack pack = packs[index];
        int req = pack.requiresPackIndex;
        return IsLeveled(pack) && req >= 0 && req < packs.Length && req != index;
    }

    private bool HasChildren(int index)
    {
        for (int i = 0; i < packs.Length; i++)
            if (IsChild(i) && packs[i].requiresPackIndex == index) return true;
        return false;
    }

    /// <summary>Switches to a tab and scrolls back to the top.</summary>
    public void SelectTab(ShopTab tab)
    {
        if (tab == ShopTab.Automatic) tab = ShopTab.Pixels;
        currentTab = tab;
        if (contentRect != null) contentRect.anchoredPosition = Vector2.zero;
        if (scrollRect != null) scrollRect.StopMovement();
        if (rows != null) RefreshRows();
    }

    /// <summary>The tab a pack is listed on (resolves 'Automatic').</summary>
    private ShopTab TabOf(ShopPack pack)
    {
        if (pack.tab != ShopTab.Automatic) return pack.tab;
        bool upgrade = IsLeveled(pack) || pack.unlocksAutoClicker || pack.upgradeEffect != UpgradeEffect.None;
        return upgrade ? ShopTab.Upgrades : ShopTab.Pixels;
    }

    private PackRow BuildRow(Transform parent, int index, bool potion = false)
    {
        string rowName = potion ? consumables.Get(index).displayName : packs[index].displayName;
        string rowDescription = potion ? consumables.Describe(index) : packs[index].description;
        PackRow row = new PackRow { isPotion = potion };

        GameObject rowGo = new GameObject((potion ? "Potion " : "Pack ") + index, typeof(RectTransform), typeof(Image));
        rowGo.transform.SetParent(parent, false);
        rowGo.GetComponent<Image>().color = rowColor;

        RectTransform rr = rowGo.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, rowHeight);
        rr.anchoredPosition = new Vector2(0f, -(index * (rowHeight + rowSpacing))); // re-laid out in RefreshRows
        row.rect = rr;

        bool hasChildren = !potion && HasChildren(index);
        float textRightInset = buyButtonSize.x + 40f; // keep text clear of the Buy button
        if (hasChildren) textRightInset += upgradesArrowSize.x + 10f;

        TMP_Text name = CreateText(rowGo.transform, "Name", rowName, nameFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        name.richText = true;
        row.nameLabel = name;
        SetBand(name.rectTransform, 0.70f, 1f, textRightInset);

        TMP_Text desc = CreateText(rowGo.transform, "Description", rowDescription, descriptionFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        desc.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        SetBand(desc.rectTransform, 0.42f, 0.70f, textRightInset);
        row.descLabel = desc;

        row.costLabel = CreateText(rowGo.transform, "Cost", "", costFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        row.costLabel.richText = true;
        // Long cost lists (six currencies) shrink to fit instead of overflowing.
        row.costLabel.enableAutoSizing = true;
        row.costLabel.fontSizeMax = costFontSize;
        row.costLabel.fontSizeMin = Mathf.Min(14f, costFontSize);
        row.costLabel.alignment = TextAlignmentOptions.TopLeft;
        SetBand(row.costLabel.rectTransform, 0.04f, 0.42f, textRightInset);

        row.buyButton = CreateButton(rowGo.transform, "Buy", buyText, buyButtonSize, buyColor, textColor,
                                     buyFontSize, out row.buyLabel, out row.buyImage);
        RectTransform br = row.buyButton.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.anchoredPosition = new Vector2(-20f, 0f);

        int captured = index;
        if (potion) row.buyButton.onClick.AddListener(() => TryBuyPotion(captured));
        else row.buyButton.onClick.AddListener(() => TryBuy(captured));

        if (hasChildren)
        {
            row.arrowButton = CreateButton(rowGo.transform, "Upgrades Arrow", upgradesArrowText, upgradesArrowSize,
                                           upgradesArrowColor, textColor, buyFontSize, out _, out _);
            RectTransform ar = row.arrowButton.GetComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1f, 0.5f);
            ar.anchoredPosition = new Vector2(-(20f + buyButtonSize.x + 10f), 0f);
            row.arrowButton.onClick.AddListener(() => OpenUpgradesWindow(captured));
        }

        return row;
    }

    /// <summary>Stretches a text across a horizontal band of its parent (anchors are 0..1 vertically).</summary>
    private void SetBand(RectTransform rt, float yMin, float yMax, float rightInset)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(20f, 0f);
        rt.offsetMax = new Vector2(-rightInset, 0f);
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float size,
                                TextAlignmentOptions alignment, FontStyles style)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, Color color,
                                Color labelColor, float labelSize, out TMP_Text labelText, out Image image)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        labelText = CreateText(go.transform, "Label", label, labelSize, TextAlignmentOptions.Center, FontStyles.Bold);
        labelText.color = labelColor;
        RectTransform lr = labelText.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        return button;
    }

    // ------------------------------------------------------------------
    // Refreshing
    // ------------------------------------------------------------------

    private void RefreshRows()
    {
        float y = 0f;      // main list
        float subY = 0f;   // upgrades window list
        int visibleCount = 0, subVisibleCount = 0;

        // Tab buttons: highlight the open one.
        if (tabImages != null)
        {
            ShopTab[] order = { ShopTab.Pixels, ShopTab.Upgrades, ShopTab.Consumables };
            for (int t = 0; t < tabImages.Length; t++)
                tabImages[t].color = order[t] == currentTab ? tabActiveColor : tabInactiveColor;
        }

        // The upgrades window only makes sense while its pack is owned.
        if (openParent >= 0 && !HasPack(openParent)) CloseUpgradesWindow();
        if (openParent >= 0) subTitle.text = string.Format(upgradesWindowTitle, packs[openParent].displayName);

        for (int i = 0; i < packs.Length && i < rows.Length; i++)
        {
            PackRow row = rows[i];
            ShopPack pack = packs[i];
            bool leveled = IsLeveled(pack);
            bool child = IsChild(i);

            bool owned = IsPurchased(i); // one-time: bought. upgrade: max level.
            bool requirementMet = IsRequirementMet(i);
            bool listed = requirementMet || showLockedPacks || HasPack(i);
            bool visible = child
                ? openParent >= 0 && pack.requiresPackIndex == openParent && listed
                : TabOf(pack) == currentTab && listed;

            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            // Stack visible rows from the top of their list.
            if (child)
            {
                row.rect.anchoredPosition = new Vector2(0f, -subY);
                subY += rowHeight + rowSpacing;
                subVisibleCount++;
            }
            else
            {
                row.rect.anchoredPosition = new Vector2(0f, -y);
                y += rowHeight + rowSpacing;
                visibleCount++;
            }

            row.nameLabel.text = BuildNameText(pack);
            row.descLabel.text = ResolveDescription(pack);

            bool canBuy = !owned && requirementMet && CanAfford(i);

            if (owned) row.costLabel.text = "";
            else if (!requirementMet) row.costLabel.text = string.Format(requiresFormat, RequirementName(pack));
            else row.costLabel.text = BuildCostText(CurrentCosts(pack));

            row.buyButton.interactable = canBuy;
            if (owned) row.buyLabel.text = leveled ? maxedText : ownedText;
            else if (!requirementMet) row.buyLabel.text = lockedText;
            else row.buyLabel.text = leveled ? upgradeText : buyText;
            row.buyImage.color = canBuy ? buyColor : disabledColor;

            // Arrow to the upgrades window: only once the pack is owned.
            if (row.arrowButton != null)
            {
                bool showArrow = HasPack(i);
                if (row.arrowButton.gameObject.activeSelf != showArrow) row.arrowButton.gameObject.SetActive(showArrow);
            }
        }

        // Potions: listed after the Consumables-tab packs.
        if (potionRows != null && consumables != null)
        {
            for (int p = 0; p < potionRows.Length && p < consumables.Count; p++)
            {
                PackRow row = potionRows[p];
                PixelConsumables.Potion potion = consumables.Get(p);

                bool listed = !potionsNeedUnlockedPixel || clicker.IsUnlocked(potion.type) || potion.owned > 0;
                bool visible = currentTab == ShopTab.Consumables && listed;
                row.rect.gameObject.SetActive(visible);
                if (!visible) continue;

                row.rect.anchoredPosition = new Vector2(0f, -y);
                y += rowHeight + rowSpacing;
                visibleCount++;

                row.nameLabel.text = potion.displayName + "   <size=65%><color=#" + ColorUtility.ToHtmlStringRGB(levelColor) +
                                     ">" + string.Format(potionOwnedFormat, potion.owned) + "</color></size>";
                row.descLabel.text = consumables.Describe(p);
                row.costLabel.text = BuildCostText(potion.costs);

                bool canBuy = CanAffordCosts(potion.costs);
                row.buyButton.interactable = canBuy;
                row.buyLabel.text = buyText;
                row.buyImage.color = canBuy ? buyColor : disabledColor;
            }
        }

        // The scroll content is as tall as the visible rows; the panel itself stays a fixed size.
        ApplyContentHeight(contentRect, emptyLabel, y, visibleCount);
        ApplyContentHeight(subContentRect, subEmptyLabel, subY, subVisibleCount);
    }

    private void ApplyContentHeight(RectTransform content, TMP_Text empty, float y, int count)
    {
        float contentHeight = count > 0 ? y - rowSpacing : 0f;
        if (!Mathf.Approximately(content.sizeDelta.y, contentHeight))
            content.sizeDelta = new Vector2(0f, contentHeight);

        if (empty != null && empty.gameObject.activeSelf != (count == 0))
            empty.gameObject.SetActive(count == 0);
    }

    /// <summary>Pack name, plus "Level 2/5" for upgrade packs.</summary>
    private string BuildNameText(ShopPack pack)
    {
        if (!IsLeveled(pack)) return pack.displayName;

        bool maxed = pack.level >= pack.levels.Length;
        string levelText = string.Format(maxed ? maxedLevelFormat : levelFormat, pack.level, pack.levels.Length);
        return pack.displayName + "   <size=65%><color=#" + ColorUtility.ToHtmlStringRGB(levelColor) + ">" +
               levelText + "</color></size>";
    }

    private string ResolveDescription(ShopPack pack)
    {
        string text = pack.description ?? "";
        if (pack.rewardTiers != null && pack.rewardTiers.Length > 0)
            text = text.Replace("{clicks}", pack.rewardTiers[0].clicksToCollect.ToString());
        if (autoClicker != null) text = text.Replace("{interval}", autoClicker.Interval.ToString("0.##"));

        if (IsLeveled(pack))
        {
            float current = 0f;
            if (autoClicker != null)
                current = pack.upgradeEffect == UpgradeEffect.AutoClickerClicks ? autoClicker.ClicksPerTick : autoClicker.Interval;

            float next = pack.level < pack.levels.Length ? pack.levels[Mathf.Max(0, pack.level)].value : current;
            text = text.Replace("{level}", pack.level.ToString())
                       .Replace("{max}", pack.levels.Length.ToString())
                       .Replace("{current}", current.ToString("0.##"))
                       .Replace("{next}", next.ToString("0.##"));
        }
        return text;
    }

    private string RequirementName(ShopPack pack)
    {
        int req = pack.requiresPackIndex;
        return req >= 0 && req < packs.Length ? packs[req].displayName : "?";
    }

    /// <summary>"Cost: 100 White Pixels  100 Gray Pixels ..." with each part green/red by affordability.</summary>
    private string BuildCostText(PackCost[] costs)
    {
        if (costs == null || costs.Length == 0) return costPrefix + "Free";

        StringBuilder sb = new StringBuilder(costPrefix);
        for (int i = 0; i < costs.Length; i++)
        {
            PackCost cost = costs[i];
            int tierIndex = clicker.IndexOf(cost.type);
            string name = tierIndex >= 0 ? clicker.Tiers[tierIndex].displayName : cost.type.ToString();
            bool enough = clicker.GetCount(cost.type) >= cost.amount;

            string part = string.Format(costEntryFormat, PixelClicker.FormatNumber(cost.amount), name);
            sb.Append("<color=#")
              .Append(ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor))
              .Append('>').Append(part).Append("</color>");

            if (i < costs.Length - 1) sb.Append("   ");
        }
        return sb.ToString();
    }
}
