using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Crafting (bought as an upgrade in the shop). A "Crafting" button appears at the bottom-right of the screen. The
/// window shows everything you hold - a spinning cube for each pixel type you have currency of, and a glass cube with a
/// smaller cube inside for each potion - in a grid. Drag two items into the two boxes at the top (or just click them)
/// and the recipe appears: what it makes and what it costs. Craft takes the cost from your inventory and puts the
/// result into your consumables.
///
/// The window is the standard Fixed window (PixelUIKit.CreateWindow; the recipe-book button sits in its left header slot);
/// the Materials and Recipe Book side panels hang off it and are built here.
///
/// Recipes are an Inspector list (two ingredients -> a potion or device), so new ones need no code.
/// The CRAFTING CUBE button (under Craft) closes the window and places a Crafting Cube (PixelCraftingCube): a machine that crafts the same
/// recipes, paying the pixel ingredients with husks (MachineRecipes / PricedRecipe / MachineCraft below).
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// Part files (partial class): PixelCraftingBook.cs (recipe book), PixelCraftingItems.cs (icons, boxes, dragging, Materials list),
/// PixelCraftingRecipes.cs (finding / pricing recipes, Craft), PixelCraftingLevel.cs (crafting level, Crafting Cube entry points, database).
/// </summary>
public partial class PixelCrafting : MonoBehaviour, IPixelDatabaseSource
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Crafted = null;
    }

    public enum ItemKind { Pixel = 0, Potion = 1, Device = 2 }
    public enum ResultKind { Potion = 0, Device = 1, Combo = 2, Triple = 3, Attuned = 4 }

    /// <summary>One ingredient: an amount of a pixel type's currency, or of a potion.</summary>
    [Serializable]
    public class Ingredient
    {
        [Tooltip("Pixel = spends that pixel type's currency. Potion = uses up potions of that pixel type.")]
        public ItemKind kind = ItemKind.Pixel;

        [Tooltip("The pixel type (for a potion: the pixel type the potion is for).")]
        public PixelClicker.PixelType type = PixelClicker.PixelType.White;

        [Tooltip("For a Device ingredient: which device (a Magnet, a Fan...).")]
        public PixelConsumables.DeviceKind device = PixelConsumables.DeviceKind.Magnet;

        [Min(1)]
        [Tooltip("How many it costs.")]
        public int amount = 1;

        /// <summary>Runtime only: a scaled amount that replaces <see cref="amount"/> (see PixelCrafting's price settings). -1 = none.</summary>
        [NonSerialized] public double amountOverride = -1d;

        /// <summary>Runtime only: the ingredient is a combo potion (made of 'type' + 'second' [+ 'third']), not a plain potion.</summary>
        [NonSerialized] public bool combo;
        [NonSerialized] public PixelClicker.PixelType second;
        [NonSerialized] public bool hasThird;
        [NonSerialized] public PixelClicker.PixelType third;

        /// <summary>What this ingredient costs right now.</summary>
        public double Amount => amountOverride >= 0d ? amountOverride : amount;

        public Ingredient() { }

        public Ingredient(PixelConsumables.DeviceKind device, int amount)
        {
            kind = ItemKind.Device;
            this.device = device;
            this.amount = amount;
        }

        public Ingredient(ItemKind kind, PixelClicker.PixelType type, int amount)
        {
            this.kind = kind;
            this.type = type;
            this.amount = amount;
        }
    }

    /// <summary>Two ingredients (in any order) make one item.</summary>
    [Serializable]
    public class Recipe
    {
        [Tooltip("Just a name for this entry in the list.")]
        public string label = "Recipe";

        [Tooltip("First ingredient. The two boxes can hold them in either order.")]
        public Ingredient a = new Ingredient();

        [Tooltip("Second ingredient.")]
        public Ingredient b = new Ingredient();

        [Tooltip("What it makes.")]
        public ResultKind resultKind = ResultKind.Potion;

        [Tooltip("For a potion result: the pixel type of the potion.")]
        public PixelClicker.PixelType resultPotion = PixelClicker.PixelType.White;

        [Tooltip("For a combo potion result: the second pixel type (the first is 'Result Potion'). Combo potions are normally made automatically (see 'Combo Crafting'), so you rarely need this.")]
        public PixelClicker.PixelType resultSecond = PixelClicker.PixelType.White;

        [Tooltip("For a 3-type combo potion result ('Triple'): the extra pixel type (the pair is 'Result Potion' + 'Result Second'). Made automatically, so you rarely need this.")]
        public PixelClicker.PixelType resultThird = PixelClicker.PixelType.White;

        [Tooltip("For a device result: which device.")]
        public PixelConsumables.DeviceKind resultDevice = PixelConsumables.DeviceKind.Vacuum;

        [Min(1)]
        [Tooltip("How many it makes.")]
        public int resultAmount = 1;
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker whose pixels are spent. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The consumables that receive the crafted items. Found automatically if left empty.")]
    [SerializeField] private PixelConsumables consumables;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("State")]
    [Tooltip("Is crafting available? (The shop turns this on when the Crafting upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool craftingActive = false;

    [Tooltip("Label of the window's button that places a Crafting Cube (a crafting machine fed with husks).")]
    [SerializeField] private string craftingCubeText = "Crafting Cube";

    [Header("Recipes")]
    [Tooltip("Every recipe. Add your own: pick two ingredients and what they make.")]
    [SerializeField] private List<Recipe> recipes = CreateDefaultRecipes();

    [Tooltip("Add the built-in recipes (each pixel's potion from that pixel + Glass; the Glass potion uses Glass in both boxes) if they are missing.")]
    [SerializeField] private bool addDefaultRecipes = true;

    [Min(1)]
    [Tooltip("Amount of the pixel needed by the built-in potion recipes.")]
    [SerializeField] private int defaultPixelCost = 50;

    [Min(1)]
    [Tooltip("Amount of Glass pixels needed by the built-in potion recipes.")]
    [SerializeField] private int defaultGlassCost = 20;

    [Header("Prices")]
    [Tooltip("Recipe costs follow the shop: a potion recipe's own pixel costs 'Craft Potion Price Fraction' of that potion's current shop price, and every other pixel ingredient is multiplied by that pixel's Value multiplier. Off = the typed amounts, always.")]
    [SerializeField] private bool recipesFollowShopPrices = true;

    [Min(0f)]
    [Tooltip("Share of a potion's current shop price that its crafting recipe costs in the potion's own pixel (0.75 = 75%; the other ingredient, usually Glass, comes on top).")]
    [SerializeField] private float craftPotionPriceFraction = 0.75f;

    [Header("Combo Potions")]
    [Tooltip("Allow combo potions: a potion plus a pixel that can't be toggled (White, Gray, Black, Red, Green, Blue, Glass, Luminescent) makes a potion that spawns BOTH pixel types. Combo potions can only be made here, never bought.")]
    [SerializeField] private bool comboCraftingEnabled = true;

    [Min(1)]
    [Tooltip("How many of the pixel a combo potion costs (the potion itself costs 1).")]
    [SerializeField] private int comboPixelCost = 100;

    [Header("Button")]
    [Tooltip("Text on the Crafting button.")]
    [SerializeField] private string buttonText = "Crafting";

    [Tooltip("Size of the button.")]
    [SerializeField] private Vector2 buttonSize = new Vector2(200f, 70f);

    [Tooltip("Distance of the button from the bottom-right corner.")]
    [SerializeField] private Vector2 buttonMargin = new Vector2(30f, 30f);

    [Tooltip("Button text size.")]
    [SerializeField] private float buttonFontSize = 34f;

    [Tooltip("Button colour (Crafting's own orange, kept as a plain colour rather than a PixelUITheme role).")]
    [SerializeField] private Color buttonColor = new Color(0.85f, 0.55f, 0.2f, 1f);

    [Header("Window")]
    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Crafting";

    [Tooltip("Tooltip shown while the mouse is over one of the two crafting boxes.")]
    [SerializeField] private string slotTipText = "Click a material or drag it here to craft. Right click to remove.";

    [Tooltip("Tooltip shown while the mouse is over a material in the list on the left.")]
    [SerializeField] private string materialTipText = "Click to use in crafting.";

    [Tooltip("Shown when the two items don't make anything.")]
    [SerializeField] private string noRecipeText = "These two can't be crafted together.";

    [Tooltip("Shown when the recipe's result doesn't exist (e.g. no potion of that type in the Consumables list).")]
    [SerializeField] private string unavailableText = "That item isn't available.";

    [Tooltip("Shown under the recipe when you already hold the most of that potion you can.")]
    [SerializeField] private string potionFullText = "You can't hold any more of this potion - drink one first.";

    [Tooltip("Title of the list of crafting materials on the left (like the shop's Currency list).")]
    [SerializeField] private string materialsTitle = "Materials";

    [Tooltip("Shown in the item list when you own nothing.")]
    [SerializeField] private string emptyItemsText = "You have no items yet.";

    [Tooltip("Text of the Craft button.")]
    [SerializeField] private string craftText = "Craft";

    [Header("Crafting Level (XP from crafting; a placeholder for now)")]
    [Min(0f)]
    [Tooltip("XP needed for level 2. 0 = 40.")]
    [SerializeField] private float xpFirstLevel = 0f;

    [Min(0f)]
    [Tooltip("Each level needs this many times the XP of the one before. 0 = 1.35.")]
    [SerializeField] private float xpGrowth = 0f;

    [Min(0)]
    [Tooltip("XP for crafting a plain potion (per craft). 0 = 5.")]
    [SerializeField] private int xpPotion = 0;

    [Min(0)]
    [Tooltip("XP for crafting a 2-type combo potion. 0 = 12.")]
    [SerializeField] private int xpCombo = 0;

    [Min(0)]
    [Tooltip("XP for crafting a 3-type combo potion. 0 = 25.")]
    [SerializeField] private int xpTriple = 0;

    [Min(0)]
    [Tooltip("XP for crafting a device. 0 = 30.")]
    [SerializeField] private int xpDevice = 0;

    [Min(0)]
    [Tooltip("Extra XP the first time you craft a recipe. 0 = 20.")]
    [SerializeField] private int xpDiscovery = 0;

    [Tooltip("Colour of the level bar's fill.")]
    [SerializeField] private Color levelBarColor = new Color(0.3f, 0.75f, 0.95f, 1f);

    [Tooltip("Colour of the '+n XP' popup.")]
    [SerializeField] private Color xpPopupColor = new Color(0.45f, 0.9f, 1f, 1f);

    [Tooltip("Message after crafting. {0} = item name, {1} = amount.")]
    [SerializeField] private string craftedFormat = "Crafted {0} x{1}!";

    [Tooltip("Line for what the recipe makes. {0} = item name, {1} = amount.")]
    [SerializeField] private string makesFormat = "Makes: {0} x{1}";

    [Tooltip("Line for one ingredient. {0} = name, {1} = what you have, {2} = what is needed.")]
    [SerializeField] private string needFormat = "{0}:  {1} / {2}";

    [Tooltip("Window width (canvas units). Used only when there is no shop to copy: the Crafting window takes the shop window's size and the Materials list is as wide as the shop's Currency list.")]
    [SerializeField] private float windowWidth = 900f;

    [Tooltip("Size of each of the two boxes.")]
    [SerializeField] private float slotSize = 150f;

    [Tooltip("Size of one item's cube in the Materials list and while dragging (a row is 0.7 of this tall).")]
    [SerializeField] private float cellSize = 120f;

    [Tooltip("Text size of the window.")]
    [SerializeField] private float fontSize = 30f;

    [Tooltip("Base title size of the Materials (x0.8) and Recipe Book (x0.7) side panels (the window's own title is the standard window title).")]
    [SerializeField] private float titleFontSize = 48f;

    [Tooltip("How fast the cubes spin (degrees per second).")]
    [SerializeField] private float cubeSpin = 40f;

    [Tooltip("Colour of a potion's glass.")]
    [SerializeField] private Color glassColor = new Color(0.8f, 0.92f, 1f, 0.38f);

    [Tooltip("Size of the cube inside the glass, relative to the glass cube.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float potionInnerScale = 0.5f;


    [Tooltip("Colour of the two boxes and the search boxes.")]
    [SerializeField] private Color slotColor = new Color(0.2f, 0.2f, 0.26f, 1f);

    [Tooltip("Colour of one item cell.")]
    [SerializeField] private Color cellColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    // (the Craft button is the Success role in PixelUITheme while it can be pressed, Neutral while not; the chosen x1 / x5 / Max is Selected)

    [Tooltip("Colour of a requirement you have enough of.")]
    [SerializeField] private Color enoughColor = new Color(0.5f, 1f, 0.55f, 1f);

    [Tooltip("Colour of a requirement you are short of.")]
    [SerializeField] private Color shortColor = new Color(1f, 0.45f, 0.4f, 1f);

    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)

    [Tooltip("Scroll bar colour.")]
    [SerializeField] private Color scrollbarColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Sound played when something is crafted.")]
    [SerializeField] private AudioClip craftSound;

    [Range(0f, 1f)]
    [Tooltip("Craft sound volume.")]
    [SerializeField] private float soundVolume = 1f;

    [Header("Canvas")]
    [Tooltip("Sorting order of the canvas (above the shop).")]
    [SerializeField] private int sortingOrder = 160;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Recipe Book")]
    [Tooltip("Tick to have the recipe book start closed when Crafting opens (the Book button in the window opens it).")]
    [SerializeField] private bool hideBookOnOpen = false;

    [Tooltip("Title of the recipe book window.")]
    [SerializeField] private string bookTitle = "Recipe Book";

    [Tooltip("Shown instead of a recipe you haven't made yet.")]
    [SerializeField] private string unknownText = "???";

    [Tooltip("Height of one recipe line in the book.")]
    [SerializeField] private float bookRowHeight = 84f;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private struct Item
    {
        public ItemKind kind;
        public PixelClicker.PixelType type;
        public bool combo;                       // a combo potion (type + second)
        public PixelClicker.PixelType second;
        public bool hasThird;                    // a 3-type combo potion (type + second + third)
        public PixelClicker.PixelType third;
        public PixelConsumables.DeviceKind device;   // for kind Device

        public Item(ItemKind k, PixelClicker.PixelType t, bool isCombo = false, PixelClicker.PixelType secondType = PixelClicker.PixelType.White,
                    bool withThird = false, PixelClicker.PixelType thirdType = PixelClicker.PixelType.White)
        {
            kind = k;
            type = t;
            device = PixelConsumables.DeviceKind.Vacuum;
            combo = isCombo;
            second = secondType;
            hasThird = withThird;
            third = thirdType;
        }
    }

    private static bool SameItem(Item a, Item b) =>
        a.kind == b.kind && (a.kind != ItemKind.Device || a.device == b.device) && a.type == b.type && a.combo == b.combo && (!a.combo || (a.second == b.second && a.hasThird == b.hasThird && (!a.hasThird || a.third == b.third)));

    /// <summary>The item an ingredient stands for (a pixel, a plain potion or a combo potion).</summary>
    private static Item ItemOf(Ingredient ing) => new Item(ing.kind, ing.type, ing.combo, ing.second, ing.hasThird, ing.third) { device = ing.device };

    /// <summary>An ingredient of this item.</summary>
    private static Ingredient IngredientOf(Item item, int amount) => new Ingredient(item.kind, item.type, amount)
    {
        device = item.device, combo = item.combo, second = item.second, hasThird = item.hasThird, third = item.third,
    };

    private class Slot
    {
        public Outline outline;
        public TMP_Text emptyMark;
        public bool hover;
        public RectTransform rect;
        public GameObject iconHost;
        public GameObject icon;
        public TMP_Text label;
        public bool has;
        public bool pinned;                      // filled by auto-fill: stays even if you don't hold the item (so you can see what is missing)
        public Item item;
    }

    private class Cell
    {
        public RectTransform rect;
        public GameObject iconHost;
        public GameObject icon;
        public TMP_Text count;
        public TMP_Text nameLabel;
        public Item item;
        public bool built;
        public bool disabled;                    // can't be combined with what is already in a box
        public CanvasGroup group;
    }

    /// <summary>Raised after something was crafted (used by the sound system).</summary>
    public static event Action Crafted;

    private GameObject canvasRoot;
    private GameObject buttonObject;
    private GameObject windowObject;
    private PixelWindowParts windowParts;   // the standard window (PixelUIKit.CreateWindow)
    private readonly Slot[] slots = new Slot[2];
    private readonly List<Cell> cells = new List<Cell>();
    private float RowHeight => cellSize * 0.7f;   // one line of the Materials list
    private RectTransform bodyRect, materialsRect;
    private float bodyHeight;
    private string HintText => "";   // the how-to text now lives in the hover tooltips
    private float fittedHeight = -1f;
    private ScrollRect listScroll;
    private GameObject listBar;
    private RectTransform listContent;
    private TMP_Text recipeText, statusLabel, emptyLabel;
    private Button craftButton;
    private TMP_Text craftLabel;
    private int craftAmount = 1;                 // 1, 5 or 0 = as many as possible
    private readonly Image[] amountImages = new Image[3];
    private Image craftImage;
    private int craftLit = -1, litAmount = -1;   // the Craft button's role last shown (1 = Success) and the amount last shown Selected
    private GameObject dragGhost;
    private Item dragItem;
    private float refreshTimer, statusTimer;
    private AudioSource audioSource;
    private bool built;

    /// <summary>Is crafting unlocked?</summary>
    public bool Active => craftingActive;

    /// <summary>Called by the shop when the Crafting upgrade is bought.</summary>
    public void Activate() { craftingActive = true; ApplyVisibility(); }

    /// <summary>Called by the shop (e.g. when a save without the upgrade is loaded).</summary>
    public void Deactivate()
    {
        craftingActive = false;
        ApplyVisibility();
    }

    // ------------------------------------------------------------------
    // Default recipes
    // ------------------------------------------------------------------

    private static List<Recipe> CreateDefaultRecipes()
    {
        List<Recipe> list = new List<Recipe>();
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (PixelClicker.IsRareCube(type)) continue;
            list.Add(new Recipe
            {
                label = PixelClicker.TypeName(type) + " Potion",   // (static default data: only a fallback, the shown name comes from the result item)
                a = new Ingredient(ItemKind.Pixel, type, 50),
                b = new Ingredient(ItemKind.Pixel, PixelClicker.PixelType.Glass, 20),
                resultKind = ResultKind.Potion,
                resultPotion = type,
            });
        }
        return list;
    }

    private static bool UsesRetired(Ingredient i) => i != null && i.kind == ItemKind.Device && PixelConsumables.IsRetiredKind(i.device);

    private bool EnsureDefaultRecipes()
    {
        if (!addDefaultRecipes) return false;
        if (recipes == null) recipes = new List<Recipe>();
        bool changed = false;
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (PixelClicker.IsRareCube(type)) continue;
            if (recipes.Exists(r => r != null && r.resultKind == ResultKind.Potion && r.resultPotion == type)) continue;
            recipes.Add(new Recipe
            {
                label = (clicker != null ? clicker.DisplayNameOf(type) : PixelClicker.TypeName(type)) + " Potion",
                a = new Ingredient(ItemKind.Pixel, type, defaultPixelCost),
                b = new Ingredient(ItemKind.Pixel, PixelClicker.PixelType.Glass, defaultGlassCost),
                resultKind = ResultKind.Potion,
                resultPotion = type,
            });
            changed = true;
        }
        // 2026-10-20: the Magnetized Sorter (Sorter + Magnet) was removed with the ring Sorter: drop any recipe that makes or uses a retired device.
        if (recipes.RemoveAll(r => r != null && ((r.resultKind == ResultKind.Device && PixelConsumables.IsRetiredKind(r.resultDevice))
                                                 || UsesRetired(r.a) || UsesRetired(r.b))) > 0) changed = true;
        return changed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultRecipes()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (clicker == null)
        {
            Debug.LogError("PixelCrafting: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (clicker.UIFont != null) font = clicker.UIFont;
        EnsureDefaultRecipes();
    }

    private void Start()
    {
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        PixelUIKit.EnsureEventSystem();
        BuildUI();
        built = true;
        ApplyVisibility();
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void ApplyVisibility()
    {
        if (!built) return;
        if (buttonObject.activeSelf != craftingActive) buttonObject.SetActive(craftingActive);
        if (!craftingActive && windowObject.activeSelf) windowObject.SetActive(false);
    }

    private bool otherWindowWasOpen;

    private void Update()
    {
        if (!built) return;
        if (statusTimer > 0f)
        {
            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f) statusLabel.text = "";
        }
        bool othersOpen = PixelWindows.AnyOpenExcept(this);
        if (PixelPop.IsOpen(windowObject) && othersOpen && !otherWindowWasOpen) Close(); // opening any other window closes Crafting
        otherWindowWasOpen = othersOpen;
        if (!windowObject.activeSelf) { craftTip = null; if (tipRoot != null && tipRoot.activeSelf) tipRoot.SetActive(false); return; }
        AnimateSlots();
        UpdateTip();

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.2f;
            Refresh();
        }
    }

    // ------------------------------------------------------------------
    // Hover tooltip and the crafting boxes' idle animation
    // ------------------------------------------------------------------

    private string craftTip;
    private GameObject tipRoot;
    private RectTransform tipRect;
    private TMP_Text tipLabel;
    private string tipMeasured;   // (the tip text last measured)
    private RectTransform tipCanvasRect;

    /// <summary>The empty boxes breathe (a faint plus and a glowing rim); a box under the mouse lights up, a filled one glows steadily.</summary>
    private void AnimateSlots()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot s = slots[i];
            if (s == null || s.emptyMark == null) continue;
            bool empty = !s.has;
            if (s.emptyMark.gameObject.activeSelf != empty) s.emptyMark.gameObject.SetActive(empty);
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.2f + i * 1.7f);
            if (empty)
            {
                Color m = textColor; m.a = 0.12f + 0.16f * breathe + (s.hover ? 0.2f : 0f);
                s.emptyMark.color = m;
                s.emptyMark.rectTransform.localScale = Vector3.one * (1f + 0.06f * breathe);
            }
            if (s.outline != null)
            {
                Color glow = empty ? new Color(0.4f, 0.8f, 1f) : new Color(0.55f, 1f, 0.6f);
                glow.a = (empty ? 0.18f + 0.22f * breathe : 0.55f) + (s.hover ? 0.25f : 0f);
                s.outline.effectColor = glow;
            }
        }
    }

    private void UpdateTip()
    {
        if (string.IsNullOrEmpty(craftTip) || dragGhost != null) { if (tipRoot != null && tipRoot.activeSelf) tipRoot.SetActive(false); return; }
        if (tipRoot == null)
        {
            tipRoot = new GameObject("Craft Tip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            tipRoot.transform.SetParent(canvasRoot.transform, false);
            tipRoot.GetComponent<CanvasGroup>().blocksRaycasts = false;
            Image bg = tipRoot.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            tipRect = tipRoot.GetComponent<RectTransform>();
            tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tipLabel = MakeLabel(tipRoot.transform, "Text", "", fontSize * 0.75f, TextAlignmentOptions.Center, FontStyles.Normal);
            tipLabel.raycastTarget = false;
            tipLabel.rectTransform.anchorMin = Vector2.zero; tipLabel.rectTransform.anchorMax = Vector2.one;
            tipLabel.rectTransform.offsetMin = new Vector2(14f, 8f); tipLabel.rectTransform.offsetMax = new Vector2(-14f, -8f);
            tipRoot.transform.SetAsLastSibling();
        }
        if (!tipRoot.activeSelf) tipRoot.SetActive(true);
        tipRoot.transform.SetAsLastSibling();
        if (tipLabel.text != craftTip || tipMeasured != craftTip)   // (measured only when the text changed)
        {
            tipLabel.text = craftTip;
            tipMeasured = craftTip;
            float width = 380f;
            float height = Mathf.Ceil(tipLabel.GetPreferredValues(craftTip, width - 28f, 0f).y) + 18f;
            tipRect.sizeDelta = new Vector2(width, height);
        }

        if (tipCanvasRect == null) tipCanvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransform canvasRect = tipCanvasRect;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local)) return;
        PixelUIKit.PlaceTooltip(tipRect, canvasRect, local);
    }

    // ------------------------------------------------------------------
    // Building the UI
    // ------------------------------------------------------------------

    private TMP_Text MakeLabel(Transform parent, string name, string text, float size, TextAlignmentOptions align, FontStyles style)
        => PixelUIKit.CreateText(font, parent, name, text, size, align, style, textColor);

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelCrafting Canvas", sortingOrder, referenceResolution, true);

        // --- Button (bottom-right)
        Button open = PixelUIKit.CreateButton(font, canvasRoot.transform, "Crafting Button", buttonText, buttonSize,
                                              buttonColor, textColor, buttonFontSize);
        buttonObject = open.gameObject;
        RectTransform br = buttonObject.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 1f);
        br.anchoredPosition = new Vector2(-buttonMargin.x, -buttonMargin.y);
        PixelHud craftHud = PixelHud.Ensure(gameObject);
        craftHud.Dock(br, new Vector2(1f, 1f), () => windowObject != null && windowObject.activeSelf, 0f, 1f, craftHud.ButtonSize.x + craftHud.CornerGap);   // top-right corner: Shop (corner), then Crafting
        open.onClick.AddListener(Toggle);
        PixelMenuKeys.Register(PixelAction.Crafting, open);

        // --- Window: the standard Fixed window (PixelUIKit.CreateWindow: title, X, header slots); FitWindow sizes it when it opens.
        windowParts = PixelUIKit.CreateWindow(font, canvasRoot.transform, "Crafting Window", windowTitle, new Vector2(windowWidth, 700f),
                                              PixelUIKit.WindowKind.Fixed, Close);
        windowObject = windowParts.Root;
        PixelMenuFx.AddWindowLine(windowObject.transform, windowParts.HeaderHeight);
        float y = windowParts.HeaderHeight + 10f;

        // Everything except the title / close / book button lives in a body that FitWindow centres a little in the free height.
        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(windowObject.transform, false);
        bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.sizeDelta = Vector2.zero;
        bodyRect.anchoredPosition = Vector2.zero;

        y = BuildCraftBoxes(body, y);   // the two boxes with a + between them

        // --- Recipe text, Craft button, status
        float recipeHeight = fontSize * 1.35f * 5f;
        recipeText = MakeLabel(body.transform, "Recipe", HintText, fontSize, TextAlignmentOptions.Top, FontStyles.Normal);
        recipeText.richText = true;
        RectTransform rr = recipeText.rectTransform;
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(-80f, recipeHeight);
        rr.anchoredPosition = new Vector2(0f, -y);
        y += recipeHeight + 8f;

        // Craft x1 / x5 / Max
        string[] amountNames = { "x1", "x5", "Max" };
        int[] amountValues = { 1, 5, 0 };
        float amountWidth = 110f, amountHeight = 54f, amountGap = 10f;
        for (int q = 0; q < 3; q++)
        {
            int value = amountValues[q], index = q;
            Button qb = PixelUIKit.CreateButton(font, body.transform, "Amount " + amountNames[q], amountNames[q], new Vector2(amountWidth, amountHeight),
                                                ButtonRole.Neutral, fontSize * 0.85f);
            amountImages[q] = qb.GetComponent<Image>();
            RectTransform qr = qb.GetComponent<RectTransform>();
            qr.anchorMin = qr.anchorMax = qr.pivot = new Vector2(0.5f, 1f);
            qr.anchoredPosition = new Vector2((index - 1) * (amountWidth + amountGap), -y);
            qb.onClick.AddListener(() => { craftAmount = value; Refresh(); });
        }
        y += amountHeight + 8f;

        craftButton = PixelUIKit.CreateButton(font, body.transform, "Craft Button", craftText, new Vector2(320f, 74f),
                                              ButtonRole.Success, fontSize * 1.2f);
        craftImage = craftButton.GetComponent<Image>();
        craftLit = litAmount = -1;
        craftLabel = craftButton.GetComponentInChildren<TMP_Text>();
        RectTransform cbr = craftButton.GetComponent<RectTransform>();
        cbr.anchorMin = cbr.anchorMax = cbr.pivot = new Vector2(0.5f, 1f);
        cbr.anchoredPosition = new Vector2(0f, -y);
        craftButton.onClick.AddListener(Craft);
        y += 74f + 6f;

        statusLabel = MakeLabel(body.transform, "Status", "", fontSize * 0.85f, TextAlignmentOptions.Center, FontStyles.Bold);
        statusLabel.color = enoughColor;
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(-80f, fontSize * 1.3f);
        sr.anchoredPosition = new Vector2(0f, -y);
        y += fontSize * 1.3f + 8f;

        // The Crafting Cube: closes this window and starts placing one (a crafting machine fed with husks, PixelCraftingCube).
        Button cubeButton = PixelUIKit.CreateButton(font, body.transform, "Crafting Cube Button", craftingCubeText, new Vector2(320f, 54f), ButtonRole.Neutral, fontSize * 0.85f);
        RectTransform cubr = cubeButton.GetComponent<RectTransform>();
        cubr.anchorMin = cubr.anchorMax = cubr.pivot = new Vector2(0.5f, 1f);
        cubr.anchoredPosition = new Vector2(0f, -y);
        cubeButton.onClick.AddListener(() =>
        {
            Close();
            if (PixelFeatureTester.StartFeature("Crafting Cube")) PixelHints.Announce("Click the floor to place the Crafting Cube. Right click stops");
        });
        y += 54f + 8f;

        bodyHeight = y;

        BuildMaterialsPanel();

        BuildLevelBar();

        BuildBookButton();
        BuildBook();

        windowObject.SetActive(false);
        PixelWindows.Register(this, 25, () => windowObject != null && PixelPop.IsOpen(windowObject), Close); // Escape closes the whole Crafting window, the recipe book with it (the book only toggles from its button)
    }

    // BuildUI, step: the two crafting boxes and the + between them, from height y in the body; returns the y under them.
    private float BuildCraftBoxes(GameObject body, float y)
    {
        // --- The two boxes
        float gap = 70f;
        for (int i = 0; i < 2; i++)
        {
            Slot slot = new Slot();
            GameObject box = new GameObject("Slot " + (i + 1), typeof(RectTransform), typeof(Image), typeof(PixelCraftDrag));
            box.transform.SetParent(body.transform, false);
            Image bi = box.GetComponent<Image>();
            bi.color = slotColor;
            PixelUIKit.StyleBox(bi, true);   // a sunken, rounded well
            slot.outline = box.AddComponent<Outline>();
            slot.outline.effectColor = new Color(0.4f, 0.8f, 1f, 0.3f);
            slot.outline.effectDistance = new Vector2(2.5f, 2.5f);
            slot.rect = box.GetComponent<RectTransform>();
            slot.rect.anchorMin = slot.rect.anchorMax = slot.rect.pivot = new Vector2(0.5f, 1f);
            slot.rect.sizeDelta = new Vector2(slotSize, slotSize);
            slot.rect.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (slotSize * 0.5f + gap * 0.5f), -y);

            int captured = i;
            box.GetComponent<PixelCraftDrag>().onClick = e =>
            {
                if (e.button == PointerEventData.InputButton.Right) ClearSlot(captured); // right-click empties the box
            };

            // A faint plus in the empty box that breathes, so it reads as somewhere to drop something.
            slot.emptyMark = MakeLabel(box.transform, "Empty Mark", "+", slotSize * 0.55f, TextAlignmentOptions.Center, FontStyles.Bold);
            slot.emptyMark.raycastTarget = false;
            RectTransform emr = slot.emptyMark.rectTransform;
            emr.anchorMin = Vector2.zero; emr.anchorMax = Vector2.one; emr.offsetMin = emr.offsetMax = Vector2.zero;
            int slotIndex = i;
            PixelHoverTip slotHover = box.AddComponent<PixelHoverTip>();
            slotHover.onEnter = () => { slots[slotIndex].hover = true; craftTip = slotTipText; };
            slotHover.onExit = () => { slots[slotIndex].hover = false; if (craftTip == slotTipText) craftTip = null; };

            slot.iconHost = new GameObject("Icon Host", typeof(RectTransform));
            slot.iconHost.transform.SetParent(box.transform, false);
            RectTransform ihr = slot.iconHost.GetComponent<RectTransform>();
            ihr.anchorMin = new Vector2(0f, 0.22f);
            ihr.anchorMax = Vector2.one;
            ihr.offsetMin = ihr.offsetMax = Vector2.zero;

            slot.label = MakeLabel(box.transform, "Label", "", fontSize * 0.55f, TextAlignmentOptions.Center, FontStyles.Normal);
            slot.label.enableAutoSizing = true;
            slot.label.fontSizeMax = fontSize * 0.55f;
            slot.label.fontSizeMin = 10f;
            RectTransform lr = slot.label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = new Vector2(1f, 0.22f);
            lr.offsetMin = new Vector2(4f, 2f);
            lr.offsetMax = new Vector2(-4f, 0f);

            slots[i] = slot;
        }

        TMP_Text plus = MakeLabel(body.transform, "Plus", "+", fontSize * 1.6f, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform pr = plus.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(gap, slotSize);
        pr.anchoredPosition = new Vector2(0f, -y);
        y += slotSize + 16f;
        return y;
    }

    private float SideWidth { get { PixelShop shop = PixelFind.Cached<PixelShop>(); return shop != null ? shop.CurrencyPanelWidth : 380f; } }
    private float SideGap { get { PixelShop shop = PixelFind.Cached<PixelShop>(); return shop != null ? shop.CurrencyPanelGap : 10f; } }

    /// <summary>The Materials list: a panel hanging off the left of the window, laid out like the shop's Currency list (a spinning cube and an amount per row).</summary>
    private void BuildMaterialsPanel()
    {
        GameObject panel = new GameObject("Materials Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(windowObject.transform, false);
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        materialsRect = panel.GetComponent<RectTransform>();
        materialsRect.anchorMin = new Vector2(0f, 0f);
        materialsRect.anchorMax = new Vector2(0f, 1f);
        materialsRect.pivot = new Vector2(1f, 0.5f);
        materialsRect.sizeDelta = new Vector2(SideWidth, 0f);
        materialsRect.anchoredPosition = new Vector2(-SideGap, 0f);

        float titleH = titleFontSize * 1.4f;
        TMP_Text title = MakeLabel(panel.transform, "Title", materialsTitle, titleFontSize * 0.8f, TextAlignmentOptions.Center, FontStyles.Bold);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-40f, titleH);
        tr.anchoredPosition = new Vector2(0f, -20f);

        // Search box under the title: filters the list by name.
        float searchH = 56f;
        materialSearchField = PixelUIKit.CreateInputField(font, panel.transform, "Search", new Vector2(0f, searchH), slotColor, textColor, fontSize * 0.8f, "Search materials...");
        PixelUIKit.AddSearchIcon(materialSearchField);
        RectTransform sr = materialSearchField.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(-40f, searchH);
        sr.anchoredPosition = new Vector2(0f, -(20f + titleH + 6f));
        materialSearchField.onValueChanged.AddListener(text =>
        {
            materialSearch = (text ?? "").Trim().ToLowerInvariant();
            RefreshList();
        });

        listScroll = PixelUIKit.CreateScrollView(panel.transform, "Item List", scrollbarColor, 12f, RowHeight * 0.6f, out listContent, out listBar);
        RectTransform vr = listScroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(20f, 20f);
        vr.offsetMax = new Vector2(-20f, -(20f + titleH + 6f + searchH + 10f));

        emptyLabel = MakeLabel(listContent, "Empty", emptyItemsText, fontSize * 0.8f, TextAlignmentOptions.Center, FontStyles.Italic);
        emptyLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        RectTransform er = emptyLabel.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.sizeDelta = new Vector2(0f, fontSize * 2.6f);
        er.anchoredPosition = Vector2.zero;
    }

    // ------------------------------------------------------------------
    // Open / close
    // ------------------------------------------------------------------

    private void Toggle()
    {
        if (PixelPop.IsOpen(windowObject)) Close();
        else Open();
    }

    private void Open()
    {
        PixelWindows.CloseAllExcept(this); // Crafting replaces every other window
        FitWindow();                       // size it BEFORE it opens, so PixelPop works out the right fit (at high UI scale it used to pop at full size, then shrink)
        PixelPop.Show(windowObject);
        refreshTimer = 0f;
        Refresh();
        if (!BookOpen && !hideBookOnOpen) OpenBook();   // the recipe book comes open with the window
    }
}

/// <summary>Forwards drag and click events on an item cell to the crafting window.</summary>
public class PixelCraftDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public Action<PointerEventData> onBegin, onDrag, onEnd, onClick;

    public void OnBeginDrag(PointerEventData e) { onBegin?.Invoke(e); }
    public void OnDrag(PointerEventData e) { onDrag?.Invoke(e); }
    public void OnEndDrag(PointerEventData e) { onEnd?.Invoke(e); }
    public void OnPointerClick(PointerEventData e) { onClick?.Invoke(e); }
}
