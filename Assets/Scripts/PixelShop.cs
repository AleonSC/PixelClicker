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
///
/// Part files (partial class): PixelShopTypes (data types), PixelShopPackUpgrades (EnsureDefaultPacks + its steps), PixelShopMigrate
/// (MigrateOldPackData + MovePack), PixelShopBuying (pack state, requirements, costs, buying, dev unlocks), PixelShopDefaults (the built-in
/// packs table), PixelShopUI, PixelShopTilePage, PixelShopPictures, PixelShopCurrency, PixelShopConsumables, PixelShopOperation,
/// PixelShopPixels, PixelShopSearch, PixelShopDatabase.
/// </summary>
[RequireComponent(typeof(PixelAutoClicker))]
public partial class PixelShop : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        hidePurchasedCache = -1;
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

    [Tooltip("The Robot Worker the Robot Worker upgrade unlocks. Found (or added) automatically if left empty.")]
    [SerializeField] private PixelRobotWorker robot;

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

    [Tooltip("EDITOR BUTTON: tick this to put every built-in pack's prices (and upgrade level costs/values) back to the defaults in PixelShopDefaults.cs. " +
             "It unticks itself. Use it after a balance change, because prices already saved in the scene don't follow the code. " +
             "Your own packs, names, descriptions and requirements are not touched. Same as right-click the component > Reset Built-in Pack Prices.")]
    [SerializeField] private bool resetBuiltInPricesNow = false;

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

    [Tooltip("Label of the shop header button that opens Cubie's bill (PixelCubieBill; shown once the bill is revealed).")]
    [SerializeField] private string billButtonText = "IOU";

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
    [Tooltip("The shop button appears once this tier is unlocked (Red: the shop opens when Cubie's second visit unlocks RGB).")]
    [SerializeField] private PixelClicker.PixelType requiredTier = PixelClicker.PixelType.Red;

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

    // (the button's colour is the Primary role in PixelUITheme)

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    // ------------------------------------------------------------------
    // Shop panel
    // ------------------------------------------------------------------

    [Header("Shop Panel")]
    [Tooltip("Title shown at the top of the panel.")]
    [SerializeField] private string panelTitle = "Shop";
    [Tooltip("Placeholder text of the search box at the top left of the shop window.")]
    [SerializeField] private string searchPlaceholder = "Search...";
    [Tooltip("Width of the search box at the top left of the shop window (canvas units).")]
    [SerializeField] private float searchWidth = 300f;

    [Tooltip("Panel width (canvas units).")]
    [SerializeField] private float panelWidth = 900f;

    [Tooltip("Total height of the panel. Lists taller than the space available scroll.")]
    [SerializeField] private float panelHeight = 880f;

    [Tooltip("Make the tip boxes (the \"Got it\" boxes shown after purchases and for first-time tips) fill the shop's height and sit to the right of the shop window, as wide as the currency panel (centred shop-sized when the shop is closed). Off = they size to their text at the top of the screen.")]
    [SerializeField] private bool tipBoxMatchesShop = true;

    [Tooltip("How long (seconds) the blue ? button in the shop must be held to replay the shop tutorial.")]
    [SerializeField] private float helpHoldSeconds = 0.7f;

    [Tooltip("Height of each pack row.")]
    [SerializeField] private float rowHeight = 230f;

    [Tooltip("Gap between pack rows.")]
    [SerializeField] private float rowSpacing = 14f;

    // Height of the title bar: the standard window header (PixelUIKit.WindowHeaderHeight; the shop is a standard Fixed window).
    // The currency panel beside the shop (PixelShopCurrency.cs) uses it too, so its header lines up with the shop's.
    private float headerHeight => PixelUIKit.WindowHeaderHeight;

    [Tooltip("How far the tab row and everything under it sits below the neon line under the shop title (canvas units).")]
    [SerializeField] private float tabsDrop = 12f;

    [Tooltip("Inner padding of the panel.")]
    [SerializeField] private float panelPadding = 20f;

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

    [Tooltip("Colour of that message (bright, so it is easy to read).")]
    [SerializeField] private Color emptyTabColor = new Color(0.92f, 0.94f, 1f, 1f);

    [Tooltip("Height of the tab buttons.")]
    [SerializeField] private float tabHeight = 70f;

    [Tooltip("Gap between the tab buttons.")]
    [SerializeField] private float tabSpacing = 10f;

    [Tooltip("Space between a tab button's edges and its text. Longer names shrink to fit inside.")]
    [SerializeField] private float tabTextPadding = 16f;

    [Tooltip("Tab text size.")]
    [SerializeField] private float tabFontSize = 34f;

    [Tooltip("Colour of the selected tile on a tile page (Pixels / Consumables) and of its grid's scroll bar. The tab and chip buttons use the Selected / Neutral roles in PixelUITheme.")]
    [SerializeField] private Color tabActiveColor = new Color(0.2f, 0.45f, 0.9f, 1f);

    [Tooltip("Background of the tile page's count box (between - and +). The buttons use the Neutral role in PixelUITheme.")]
    [SerializeField] private Color tabInactiveColor = new Color(0.22f, 0.22f, 0.28f, 1f);

    [Header("Potions (Consumables tab)")]
    [Tooltip("Only list a potion once its pixel type is unlocked (or you already own some).")]
    [SerializeField] private bool potionsNeedUnlockedPixel = true;

    [Header("Minigame Trackers (Minigames tab)")]
    [Tooltip("Show each minigame's tracker row (e.g. Singularity, Ghosts Caught) in the Minigames tab. Off by default: the trackers now live in the Log's Goals tab. Titles and descriptions are set on the minigame components.")]
    [SerializeField] private bool showMinigameTrackersInShop = false;

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

    [Tooltip("Arrow button colour (its own orange, not a PixelUITheme role).")]
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

    [Tooltip("Draw scroll bars in the shop's lists (off = the lists still scroll with the mouse wheel, there is just no bar).")]
    [SerializeField] private bool showScrollBars = false;

    [Tooltip("Scrollbar track colour.")]
    [SerializeField] private Color scrollbarTrackColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Tooltip("Scrollbar handle colour.")]
    [SerializeField] private Color scrollbarHandleColor = new Color(0.5f, 0.5f, 0.6f, 1f);

    [Header("Panel Behaviour")]
    [Tooltip("Open the shop panel automatically the first time the button appears.")]
    [SerializeField] private bool openWhenFirstAvailable = false;

    [Header("Text")]
    [Tooltip("Title text size of the currency panel beside the shop (it uses 0.8x this). The shop window's own title is the standard window title.")]
    [SerializeField] private float titleFontSize = 56f;

    [Tooltip("Pack name text size.")]
    [SerializeField] private float nameFontSize = 40f;

    [Tooltip("Pack description text size.")]
    [SerializeField] private float descriptionFontSize = 26f;

    [Tooltip("Cost line text size.")]
    [SerializeField] private float costFontSize = 30f;

    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Header("Pixels and Consumables Tabs (category chips, tiles, buy bar)")]
    [Tooltip("Label of the Potions category button.")]
    [SerializeField] private string potionsCardTitle = "Potions";

    [Tooltip("Label of the Utilities category button (devices you place).")]
    [SerializeField] private string utilitiesCardTitle = "Utilities";

    [Tooltip("Colour of a locked item's name and price line on its tile.")]
    [SerializeField] private Color lockedItemColor = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Tooltip("Shown instead of the cost on a locked item. {0} = the pixel type that must be unlocked first.")]
    [SerializeField] private string lockedRequirementFormat = "Locked: unlock {0} first";

    [Tooltip("Text of the Buy button. {0} = how many.")]
    [SerializeField] private string buyCountFormat = "Buy x{0}";

    [Tooltip("Buy button / tile text when you already hold the most of that potion you can.")]
    [SerializeField] private string potionFullText = "Full";

    [Tooltip("Cost line in the buy bar when that potion is full. {0} = the most you can hold.")]
    [SerializeField] private string potionFullFormat = "You can hold {0} of each potion. Drink one to buy more.";

    [Tooltip("Shown when a category has no items.")]
    [SerializeField] private string noItemsText = "Nothing to buy here.";


    [Min(80f)]
    [Tooltip("Width of one item tile in the catalog grid. 140 fits 4 to a row in the 1240-wide shop.")]
    [SerializeField] private float tileWidth = 160f;

    [Min(100f)]
    [Tooltip("Height of one item tile (picture, name and price line).")]
    [SerializeField] private float tileHeight = 176f;

    [Min(0f)]
    [Tooltip("Gap between the item tiles.")]
    [SerializeField] private float tileGap = 10f;

    [Min(10f)]
    [Tooltip("Font size of a tile's name; the price and owned count are a little smaller, the buy bar's description a little bigger.")]
    [SerializeField] private float tileFontSize = 20f;

    [Tooltip("Shown in the search results when nothing matches.")]
    [SerializeField] private string searchNoResultsText = "Nothing in the shop matches that.";

    [Tooltip("Shown in the grid when 'Only what I can buy' is on and nothing there can be bought.")]
    [SerializeField] private string nothingAffordableText = "Nothing here you can buy right now.";

    [Tooltip("Shown when hovering the Consumables Buy button.")]
    [SerializeField] private string buyMaxTip = "Shift-click to buy as many as you can.";

    [Tooltip("Label of the Pixels tab's first category button (the RGB pack and single pixels: Glass, Luminescent...).")]
    [SerializeField] private string pixelsToggleablesTitle = "Toggleables";

    [Tooltip("Label of the Pixels tab's second category button (skill packs: many pixels at once).")]
    [SerializeField] private string pixelsSkillPacksTitle = "Skill packs";

    [Tooltip("Label of the Seeds category button.")]
    [SerializeField] private string seedsCardTitle = "Seeds";

    [Tooltip("Label of the Supplies category button (Combo Fuel, Ghost Bait, Pet Treat).")]
    [SerializeField] private string suppliesCardTitle = "Supplies";

    [Min(0f)]
    [Tooltip("Gap between the category buttons, the tile grid and the buy bar.")]
    [SerializeField] private float cardGap = 20f;

    [Min(1)]
    [Tooltip("The most of one item you can buy in one go.")]
    [SerializeField] private int maxPerPurchase = 999;

    [Header("Pixel Upgrades (spend Ultra pixels)")]
    [Tooltip("Text of the sub-tab that lists the feature unlocks and their upgrades (inside the Upgrades tab). Renamed from 'Upgrades'.")]
    [SerializeField] private string featuresSubTabText = "Features";

    [Tooltip("Height of the two sub-tab buttons.")]
    [SerializeField] private float subTabHeight = 56f;


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

    [Header("Operation Upgrades (Upgrades > Operation: upgrades for the consumables)")]
    [Tooltip("Text of the sub-tab that lists the consumables and their upgrades (inside the Upgrades tab).")]
    [SerializeField] private string operationSubTabText = "Operation";

    [Tooltip("Title of a consumable's upgrade window. {0} = its name.")]
    [SerializeField] private string operationWindowTitle = "{0} Upgrades";

    [Tooltip("Shown on a consumable's row. {0} = upgrade levels bought, {1} = levels in all.")]
    [SerializeField] private string operationSummaryFormat = "Upgrades: {0} / {1}";

    [Tooltip("Cost line of an upgrade. {0} = price, {1} = pixel name,")]
    [SerializeField] private string operationCostFormat = "Cost: {0} {1}";

    [Tooltip("The consumables' upgrades. The built-in ones are added by id when missing; edit levels, effects and prices here.")]
    [SerializeField] private System.Collections.Generic.List<PixelOperationUpgrade> operationUpgrades = new System.Collections.Generic.List<PixelOperationUpgrade>();

    [Header("Value Upgrades (spend a pixel type's own currency)")]
    [Tooltip("Text of the sub-tab that lists the Value upgrades (inside the Upgrades tab).")]
    [SerializeField] private string valueSubTabText = "Value";

    [Tooltip("Name of a Value row. {0} = pixel name.")]
    [SerializeField] private string valueRowNameFormat = "{0} Value";

    [Tooltip("The orange '!' box of a Value row.")]
    [SerializeField] private string valueInfoText = "Each level multiplies this pixel's worth per click by that level.";

    [Tooltip("Shown when the mouse is over a Value row's Upgrade button.")]
    [SerializeField] private string valueUpgradeTip = "Shift-click to buy as many as you can.";

    [Tooltip("Cost line of a Value row. {0} = cost, {1} = pixel name,")]
    [SerializeField] private string valueCostFormat = "Cost: {0} {1}";

    [Tooltip("Text of a Value row's button.")]
    [SerializeField] private string valueButtonText = "Upgrade";

    [Min(0f)]
    [Tooltip("Cost of a pixel type's first Value level, per point of that pixel's base payout (25 = a pixel that pays 1 costs 25, one that pays 8 costs 200). Spawn chance is ignored on purpose: rarer pixels earn less, so their Value climbs more slowly.")]
    [SerializeField] private double valueBaseCost = 25;

    [Min(1f)]
    [Tooltip("Each Value level costs this many times the one before (1.15 = +15% per level). This is what makes later levels slow.")]
    [SerializeField] private double valueCostGrowth = 1.15;

    [Min(0)]
    [Tooltip("Highest Value level a pixel type can reach. 0 = no limit.")]
    [SerializeField] private int valueMaxLevel = 0;

    [Tooltip("A pixel type's Value level can't go above the level of the skill that owns it (pure / basic pixels: Standard; a skill's blocks: that skill), times 'Value Levels Per Skill Level'. Levels already above the cap are kept. Off = no skill cap.")]
    [SerializeField] private bool valueCapBySkill = true;

    [Min(1)]
    [Tooltip("Value levels allowed for each level of the owning skill (1 = Standard level 5 allows Value 5 on the pure pixels).")]
    [SerializeField] private int valueLevelsPerSkillLevel = 1;

    [Tooltip("Value button text while the skill cap stops the next level.")]
    [SerializeField] private string valueCappedButtonText = "Locked";

    [Tooltip("Cost line while the skill cap stops the next level. {0} = skill name, {1} = the skill level needed.")]
    [SerializeField] private string valueCappedFormat = "Needs {0} level {1}";

    [Min(1)]
    [Tooltip("Shift-click on a Value button buys up to this many levels at once.")]
    [SerializeField] private int valueMaxBulk = 1000;

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

    // (Buy buttons use the Success role in PixelUITheme, and Neutral when they can't be bought)

    [Tooltip("Background of the search box.")]
    [SerializeField] private Color disabledColor = new Color(0.35f, 0.35f, 0.4f, 1f);

    // ------------------------------------------------------------------
    // Canvas
    // ------------------------------------------------------------------

    [Header("Performance")]
    [Tooltip("Seconds between re-applying the effects of purchased packs (a safety net for packs ticked in the Inspector; normal buying applies at once).")]
    [SerializeField] private float reapplyIntervalSeconds = 0.5f;

    [Tooltip("Seconds between row refreshes while the shop is open (buying and tab changes refresh at once).")]
    [SerializeField] private float rowRefreshSeconds = 0.25f;

    [Header("Currency strip (under the shop's header)")]
    [Tooltip("Hide the strip of pixel amounts under the shop's header.")]
    [SerializeField] private bool hideCurrencyPanel = false;

    [Min(30f)]
    [Tooltip("Height of the currency strip under the shop's header (the cubes and amounts scale with it).")]
    [SerializeField] private float currencyStripHeight = 52f;

    [Tooltip("Width of a window docked beside the open shop (the Pixel Guide; it gets narrower when the screen has less room). 0 = 380.")]
    [SerializeField] private float currencyPanelWidth = 380f;

    [Tooltip("Gap between the shop window and a window docked beside it.")]
    [SerializeField] private float currencyPanelGap = 10f;

    [Header("Shop layout (2026-10-24: category rail, Saving up for card, detail panel)")]
    [Min(160f)]
    [Tooltip("Width of the category rail on the left (Pixels / Consumables / Upgrades / Minigames and their entries).")]
    [SerializeField] private float railWidth = 230f;

    [Min(240f)]
    [Tooltip("Width of the detail panel on the right (picture, description, cost, amounts, Buy).")]
    [SerializeField] private float detailPanelWidth = 330f;

    [Min(70f)]
    [Tooltip("Height of the Saving up for card above the grid.")]
    [SerializeField] private float savingCardHeight = 96f;

    [Tooltip("Width of the windows that keep the shop's older size (the pack Upgrades window, the Crafting window).")]
    [SerializeField] private float classicWindowWidth = 900f;

    [Tooltip("The quick amount buttons in the detail panel (consumables and Value levels); a MAX button follows them.")]
    [SerializeField] private int[] amountPresets = { 1, 5, 10 };

    [Tooltip("Label of the amount button that picks as many as you can afford and hold.")]
    [SerializeField] private string amountMaxText = "Max";

    [Tooltip("Colour of the thin bar along the bottom of a tile you can buy right now.")]
    [SerializeField] private Color canBuyColor = new Color(0.28f, 0.82f, 0.4f, 1f);

    [Tooltip("Background of the Saving up for card.")]
    [SerializeField] private Color savingCardColor = new Color(0.19f, 0.17f, 0.28f, 1f);

    [Tooltip("Colour of the Saving up for progress bar.")]
    [SerializeField] private Color savingBarColor = new Color(0.65f, 0.3f, 0.95f, 1f);

    [Tooltip("Label at the top of the Saving up for card.")]
    [SerializeField] private string savingUpTitle = "Saving up for";

    [Tooltip("Detail panel button that pins the selected item to the Saving up for card.")]
    [SerializeField] private string saveForThisText = "Save up for this";

    [Tooltip("The same button while the selected item is the pinned one (clicking it unpins).")]
    [SerializeField] private string savingThisText = "Saving up for this (click to stop)";

    [Tooltip("Added to the card's progress line while the item was pinned by the player (not picked by the shop).")]
    [SerializeField] private string savingPinnedText = "pinned";

    [Tooltip("The switch above the grid that hides everything you can't buy right now.")]
    [SerializeField] private string onlyAffordableText = "Only what I can buy";

    [Tooltip("The Buy button when a price can't be paid. {0} = the first currency you are short of.")]
    [SerializeField] private string needMoreFormat = "Need more {0}";

    [Tooltip("Heading above the grid while searching. {0} = the search text.")]
    [SerializeField] private string resultsForFormat = "Results for \"{0}\"";

    [Tooltip("The Buy button for several Value levels. {0} = how many.")]
    [SerializeField] private string upgradeCountFormat = "Upgrade x{0}";

    [Tooltip("The rail's entry under Minigames.")]
    [SerializeField] private string allMinigamesText = "All minigames";

    /// <summary>Width of a window docked beside the shop (the Pixel Guide copies it).</summary>
    /// <summary>Layout numbers the windows beside the shop copy so their contents line up with the shop's: where the first element under the header starts, how tall the tab row is and where the list starts.</summary>
    public float HeaderHeightValue => headerHeight;
    public float TabHeightValue => tabHeight;
    public float PanelPaddingValue => panelPadding;
    public float ListTopValue => headerHeight + tabsDrop + tabHeight + panelPadding * 0.5f;

    public float CurrencyPanelWidth => currencyPanelWidth > 40f ? currencyPanelWidth : 380f;

    /// <summary>Gap between the shop and its currency panel.</summary>
    public float CurrencyPanelGap => Mathf.Max(0f, currencyPanelGap);

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

        // The shop's systems and the built-in minigames must exist in the scene (the shop switches them on): the Shop and Minigames
        // groups of the one system list (PixelSystems.cs), added here in the list's order.
        PixelSystems.AddMissing(gameObject, PixelSystems.ShopGroups);
        if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
        if (grab == null) grab = PixelFind.First<PixelGrab>();
        if (timeStop == null) timeStop = PixelFind.First<PixelTimeStop>();
        if (bank == null) bank = PixelFind.First<PixelBank>();
        if (robot == null) robot = PixelFind.First<PixelRobotWorker>();
        if (crafting == null) crafting = PixelFind.First<PixelCrafting>();
        if (combo == null) combo = PixelFind.First<PixelCombo>();
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();

        if (PixelFind.Count<PixelConsumables>() > 1)
            Debug.LogWarning("PixelShop: more than one PixelConsumables component exists in the scene. " +
                             "Remove the extra one so purchases and the inventory use the same potions.", this);

        EnsureDefaultPacks();
        EnsureDefaultOperationUpgrades();

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
        ApplyShopText();   // names, descriptions and prices from Resources/ShopText.txt (PixelShopDefaults.cs)
    }

    [SerializeField, HideInInspector] private int tileLayoutVersion;   // 1 = 160 x 200 tiles (5 to a row), 2 = 176 tall (no name line), 3 = the 1240-wide catalog layout (140-wide named tiles)
    [SerializeField, HideInInspector] private int shopBalanceVersion; // 1 = the 2026-10-16 rebalance (new default prices, minigames need only their price), 2 = the 2026-10-24 skill pack ladder flags

    [Header("Skill pack price ladder (gathering packs; ShopPack.skillLadder)")]
    [Min(1f)]
    [Tooltip("Price of the first rung, in EACH of the six pure colours: the Lumberjack's Pack (affordable at about 10-20 min).")]
    [SerializeField] private float skillLadderStarterPrice = 250f;

    [Min(1f)]
    [Tooltip("Each rung costs this many times the one before (2.5: 250 / 630 / 1.6K / 3.9K / 9.8K of each pure colour). Targets: the 2nd pack ~25-30 min, 3rd ~45, 4th ~70.")]
    [SerializeField] private float skillLadderGrowth = 2.5f;

    [Tooltip("On: the Nth skill pack you buy costs rung N-1, whichever it is (the player's choice of skill stays free). Off: a fixed rung per pack, by its place in the pack list.")]
    [SerializeField] private bool skillLadderByPurchaseOrder = true;

    [Tooltip("On: the Lumberjack's Pack always costs the first rung and every other pack at least the second, so Woodcutting stays the cheap first skill.")]
    [SerializeField] private bool skillLadderStarterFixed = true;
    [SerializeField, HideInInspector] private int packDataVersion; // 1 = Obsidian pays 7 (was 5); 2 = Singularity spawn weight 0.08 (was 0.2); 3 = Electric pays a random 1-15; 4 = Electric pack gets its real price + Luminescent requirement; 5 = Electric becomes an early unlock (8K RGB, needs the Auto Clicker pack, listed before Vacuum)

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: serialized data must not be changed from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultPacks()) UnityEditor.EditorUtility.SetDirty(this);
            if (EnsureDefaultOperationUpgrades()) UnityEditor.EditorUtility.SetDirty(this);

            if (resetBuiltInPricesNow) EditorResetBuiltInPackPrices(true);   // (PixelShopDefaults.cs, shared with the context menu)

            // Make sure the shop's systems and minigames exist in the scene, so their settings are editable in the Inspector.
            EditorEnsureShopSystems();
        };
    }

    /// <summary>Edit mode: adds the Shop and Minigames groups' systems (PixelSystems.cs) that are missing to this object, with Undo.</summary>
    private void EditorEnsureShopSystems()
    {
        foreach (PixelSystems.Entry e in PixelSystems.All)
        {
            if (e.type == null || System.Array.IndexOf(PixelSystems.ShopGroups, e.group) < 0 || PixelFind.First(e.type) != null) continue;
            UnityEditor.Undo.AddComponent(gameObject, e.type);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
    }
#endif

    private void Start()
    {
        PixelUIKit.EnsureEventSystem();
        BuildUI();
        builtOk = true;

        // Escape closes the upgrades window first, then the shop.
        PixelWindows.Register(this, 40, () => subPanelObject != null && subPanelObject.activeSelf, CloseUpgradesWindow);   // window-built-in: PixelShopUI.cs (CreateWindow)
        PixelWindows.Register(this, 30, () => panelObject != null && PixelPop.IsOpen(panelObject), () => PixelPop.Hide(panelObject));   // window-built-in: PixelShopUI.cs (CreateWindow)

#if UNITY_EDITOR || DEBUG
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
        OnDestroyOperation();
        OnDestroyPictures();
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

        if (visible && !wasButtonVisible && openWhenFirstAvailable) PixelPop.Show(panelObject);
        wasButtonVisible = visible;

        if (!visible && panelObject.activeSelf) panelObject.SetActive(false);
        if (!panelObject.activeSelf && subPanelObject.activeSelf) CloseUpgradesWindow();

        if (panelObject.activeSelf && Time.unscaledTime >= nextRowRefreshTime)
        {
            nextRowRefreshTime = Time.unscaledTime + rowRefreshSeconds;
            RefreshRows();
            RefreshCurrency();
        }
        UpdateCurrencyAndWindows();
        UpdateOperationPictures();
        UpdatePictures();
    }

    // ------------------------------------------------------------------
    // Shop logic
    // ------------------------------------------------------------------

    /// <summary>True when every reward tier of the pack is already unlocked.</summary>
    private const string PrefHidePurchased = "PixelClicker.Setting.HidePurchased";
    private static int hidePurchasedCache = -1; // -1 = not read yet

    /// <summary>A save file with its own settings was loaded: read "hide purchased" again.</summary>
    public static void ReloadSettingsFromPrefs() { hidePurchasedCache = -1; }

    /// <summary>Player setting: hide shop items that are already bought (or maxed out). Remembered between sessions.</summary>
    public static bool HidePurchased
    {
        get
        {
            if (hidePurchasedCache < 0) hidePurchasedCache = PixelPrefs.GetInt(PrefHidePurchased, 0) != 0 ? 1 : 0;
            return hidePurchasedCache != 0;
        }
        set { hidePurchasedCache = value ? 1 : 0; PixelPrefs.SetInt(PrefHidePurchased, hidePurchasedCache); }
    }
}
