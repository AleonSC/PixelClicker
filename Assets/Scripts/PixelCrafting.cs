using System;
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
/// Recipes are an Inspector list (two ingredients -> a potion or device), so new ones need no code.
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelCrafting : MonoBehaviour
{
    public enum ItemKind { Pixel = 0, Potion = 1 }
    public enum ResultKind { Potion = 0, Device = 1, Combo = 2 }

    /// <summary>One ingredient: an amount of a pixel type's currency, or of a potion.</summary>
    [Serializable]
    public class Ingredient
    {
        [Tooltip("Pixel = spends that pixel type's currency. Potion = uses up potions of that pixel type.")]
        public ItemKind kind = ItemKind.Pixel;

        [Tooltip("The pixel type (for a potion: the pixel type the potion is for).")]
        public PixelClicker.PixelType type = PixelClicker.PixelType.White;

        [Min(1)]
        [Tooltip("How many it costs.")]
        public int amount = 1;

        public Ingredient() { }

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

    [Tooltip("Button colour.")]
    [SerializeField] private Color buttonColor = new Color(0.85f, 0.55f, 0.2f, 1f);

    [Header("Window")]
    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Crafting";

    [Tooltip("Shown until two items are in the boxes.")]
    [SerializeField] private string hintText = "Drag two items from below into the boxes (or click them). Right-click a box to empty it.";

    [Tooltip("Shown when the two items don't make anything.")]
    [SerializeField] private string noRecipeText = "These two can't be crafted together.";

    [Tooltip("Shown when the recipe's result doesn't exist (e.g. no potion of that type in the Consumables list).")]
    [SerializeField] private string unavailableText = "That item isn't available.";

    [Tooltip("Heading of the list of what you own.")]
    [SerializeField] private string itemsHeading = "Your items (drag into the boxes)";

    [Tooltip("Shown in the item list when you own nothing.")]
    [SerializeField] private string emptyItemsText = "You have no items yet.";

    [Tooltip("Text of the Craft button.")]
    [SerializeField] private string craftText = "Craft";

    [Tooltip("Message after crafting. {0} = item name, {1} = amount.")]
    [SerializeField] private string craftedFormat = "Crafted {0} x{1}!";

    [Tooltip("Line for what the recipe makes. {0} = item name, {1} = amount.")]
    [SerializeField] private string makesFormat = "Makes: {0} x{1}";

    [Tooltip("Line for one ingredient. {0} = name, {1} = what you have, {2} = what is needed.")]
    [SerializeField] private string needFormat = "{0}:  {1} / {2}";

    [Tooltip("Window width (canvas units).")]
    [SerializeField] private float windowWidth = 980f;

    [Tooltip("Size of each of the two boxes.")]
    [SerializeField] private float slotSize = 150f;

    [Tooltip("Size of one item in the list (the cube plus its name).")]
    [SerializeField] private float cellSize = 120f;

    [Tooltip("Gap between items in the list.")]
    [SerializeField] private float cellGap = 10f;

    [Min(100f)]
    [Tooltip("Height of the scrolling item list. More items scroll.")]
    [SerializeField] private float listHeight = 300f;

    [Tooltip("Text size of the window.")]
    [SerializeField] private float fontSize = 30f;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 48f;

    [Tooltip("How fast the cubes spin (degrees per second).")]
    [SerializeField] private float cubeSpin = 40f;

    [Tooltip("Colour of a potion's glass.")]
    [SerializeField] private Color glassColor = new Color(0.8f, 0.92f, 1f, 0.38f);

    [Tooltip("Size of the cube inside the glass, relative to the glass cube.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float potionInnerScale = 0.5f;

    [Tooltip("Window background colour.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);

    [Tooltip("Colour of the two boxes.")]
    [SerializeField] private Color slotColor = new Color(0.2f, 0.2f, 0.26f, 1f);

    [Tooltip("Colour of one item cell.")]
    [SerializeField] private Color cellColor = new Color(0.16f, 0.16f, 0.2f, 1f);

    [Tooltip("Craft button colour while it can be pressed.")]
    [SerializeField] private Color craftColor = new Color(0.25f, 0.6f, 0.3f, 1f);

    [Tooltip("Craft button colour while it can't be pressed.")]
    [SerializeField] private Color craftDisabledColor = new Color(0.3f, 0.3f, 0.35f, 1f);

    [Tooltip("Colour of a requirement you have enough of.")]
    [SerializeField] private Color enoughColor = new Color(0.5f, 1f, 0.55f, 1f);

    [Tooltip("Colour of a requirement you are short of.")]
    [SerializeField] private Color shortColor = new Color(1f, 0.45f, 0.4f, 1f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

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

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private struct Item
    {
        public ItemKind kind;
        public PixelClicker.PixelType type;
        public bool combo;                       // a combo potion (type + second)
        public PixelClicker.PixelType second;

        public Item(ItemKind k, PixelClicker.PixelType t, bool isCombo = false, PixelClicker.PixelType secondType = PixelClicker.PixelType.White)
        {
            kind = k;
            type = t;
            combo = isCombo;
            second = secondType;
        }
    }

    private static bool SameItem(Item a, Item b) =>
        a.kind == b.kind && a.type == b.type && a.combo == b.combo && (!a.combo || a.second == b.second);

    private class Slot
    {
        public RectTransform rect;
        public GameObject iconHost;
        public GameObject icon;
        public TMP_Text label;
        public bool has;
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
    }

    /// <summary>Raised after something was crafted (used by the sound system).</summary>
    public static event Action Crafted;

    private GameObject canvasRoot;
    private GameObject buttonObject;
    private GameObject windowObject;
    private readonly Slot[] slots = new Slot[2];
    private readonly List<Cell> cells = new List<Cell>();
    private ScrollRect listScroll;
    private GameObject listBar;
    private RectTransform listContent;
    private TMP_Text recipeText, statusLabel, emptyLabel;
    private Button craftButton;
    private Image craftImage;
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
            list.Add(new Recipe
            {
                label = type + " Potion",
                a = new Ingredient(ItemKind.Pixel, type, 50),
                b = new Ingredient(ItemKind.Pixel, PixelClicker.PixelType.Glass, 20),
                resultKind = ResultKind.Potion,
                resultPotion = type,
            });
        }
        return list;
    }

    private bool EnsureDefaultRecipes()
    {
        if (!addDefaultRecipes) return false;
        if (recipes == null) recipes = new List<Recipe>();
        bool changed = false;
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (recipes.Exists(r => r != null && r.resultKind == ResultKind.Potion && r.resultPotion == type)) continue;
            recipes.Add(new Recipe
            {
                label = type + " Potion",
                a = new Ingredient(ItemKind.Pixel, type, defaultPixelCost),
                b = new Ingredient(ItemKind.Pixel, PixelClicker.PixelType.Glass, defaultGlassCost),
                resultKind = ResultKind.Potion,
                resultPotion = type,
            });
            changed = true;
        }
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

    private void Update()
    {
        if (!built) return;
        if (statusTimer > 0f)
        {
            statusTimer -= Time.unscaledDeltaTime;
            if (statusTimer <= 0f) statusLabel.text = "";
        }
        if (!windowObject.activeSelf) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.2f;
            Refresh();
        }
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
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0f);
        br.anchoredPosition = new Vector2(-buttonMargin.x, buttonMargin.y);
        PixelHud.Ensure(gameObject).Dock(br, new Vector2(1f, 0f), () => windowObject != null && windowObject.activeSelf);
        open.onClick.AddListener(Toggle);

        // --- Window
        windowObject = new GameObject("Crafting Window", typeof(RectTransform), typeof(Image));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        windowObject.GetComponent<Image>().color = panelColor;
        RectTransform wr = windowObject.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.anchoredPosition = Vector2.zero;

        float titleH = titleFontSize * 1.4f;
        float y = 20f;

        TMP_Text title = MakeLabel(windowObject.transform, "Title", windowTitle, titleFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-200f, titleH);
        tr.anchoredPosition = new Vector2(0f, -y);

        Button close = PixelUIKit.CreateButton(font, windowObject.transform, "Close", "X", new Vector2(70f, 70f),
                                               new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-20f, -14f);
        close.onClick.AddListener(Close);
        y += titleH + 14f;

        // --- The two boxes
        float gap = 70f;
        for (int i = 0; i < 2; i++)
        {
            Slot slot = new Slot();
            GameObject box = new GameObject("Slot " + (i + 1), typeof(RectTransform), typeof(Image), typeof(PixelCraftDrag));
            box.transform.SetParent(windowObject.transform, false);
            Image bi = box.GetComponent<Image>();
            bi.color = slotColor;
            slot.rect = box.GetComponent<RectTransform>();
            slot.rect.anchorMin = slot.rect.anchorMax = slot.rect.pivot = new Vector2(0.5f, 1f);
            slot.rect.sizeDelta = new Vector2(slotSize, slotSize);
            slot.rect.anchoredPosition = new Vector2((i == 0 ? -1f : 1f) * (slotSize * 0.5f + gap * 0.5f), -y);

            int captured = i;
            box.GetComponent<PixelCraftDrag>().onClick = e =>
            {
                if (e.button == PointerEventData.InputButton.Right) ClearSlot(captured); // right-click empties the box
            };

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

        TMP_Text plus = MakeLabel(windowObject.transform, "Plus", "+", fontSize * 1.6f, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform pr = plus.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(gap, slotSize);
        pr.anchoredPosition = new Vector2(0f, -y);
        y += slotSize + 16f;

        // --- Recipe text, Craft button, status
        float recipeHeight = fontSize * 1.35f * 5f;
        recipeText = MakeLabel(windowObject.transform, "Recipe", hintText, fontSize, TextAlignmentOptions.Top, FontStyles.Normal);
        recipeText.richText = true;
        RectTransform rr = recipeText.rectTransform;
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(-80f, recipeHeight);
        rr.anchoredPosition = new Vector2(0f, -y);
        y += recipeHeight + 8f;

        craftButton = PixelUIKit.CreateButton(font, windowObject.transform, "Craft Button", craftText, new Vector2(320f, 74f),
                                              craftColor, textColor, fontSize * 1.2f);
        craftImage = craftButton.GetComponent<Image>();
        RectTransform cbr = craftButton.GetComponent<RectTransform>();
        cbr.anchorMin = cbr.anchorMax = cbr.pivot = new Vector2(0.5f, 1f);
        cbr.anchoredPosition = new Vector2(0f, -y);
        craftButton.onClick.AddListener(Craft);
        y += 74f + 6f;

        statusLabel = MakeLabel(windowObject.transform, "Status", "", fontSize * 0.85f, TextAlignmentOptions.Center, FontStyles.Bold);
        statusLabel.color = enoughColor;
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f);
        sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(-80f, fontSize * 1.3f);
        sr.anchoredPosition = new Vector2(0f, -y);
        y += fontSize * 1.3f + 8f;

        // --- Item list
        TMP_Text heading = MakeLabel(windowObject.transform, "Items Heading", itemsHeading, fontSize * 0.85f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        RectTransform hr = heading.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(-80f, fontSize * 1.3f);
        hr.anchoredPosition = new Vector2(0f, -y);
        y += fontSize * 1.3f + 4f;

        listScroll = PixelUIKit.CreateScrollView(windowObject.transform, "Item List", scrollbarColor, 12f, cellSize * 0.6f,
                                                 out listContent, out listBar);
        RectTransform vr = listScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-60f, listHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        emptyLabel = MakeLabel(listContent, "Empty", emptyItemsText, fontSize * 0.9f, TextAlignmentOptions.Center, FontStyles.Italic);
        emptyLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        RectTransform er = emptyLabel.rectTransform;
        er.anchorMin = new Vector2(0f, 1f);
        er.anchorMax = new Vector2(1f, 1f);
        er.pivot = new Vector2(0.5f, 1f);
        er.sizeDelta = new Vector2(0f, fontSize * 1.5f);
        er.anchoredPosition = Vector2.zero;
        y += listHeight + 30f;

        wr.sizeDelta = new Vector2(windowWidth, y);

        windowObject.SetActive(false);
        PixelWindows.Register(this, 25, () => windowObject != null && windowObject.activeSelf, Close);
    }

    // ------------------------------------------------------------------
    // Open / close
    // ------------------------------------------------------------------

    private void Toggle()
    {
        if (windowObject.activeSelf) Close();
        else Open();
    }

    private void Open()
    {
        windowObject.SetActive(true);
        refreshTimer = 0f;
        Refresh();
    }

    private void Close()
    {
        if (dragGhost != null) Destroy(dragGhost);
        windowObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Icons
    // ------------------------------------------------------------------

    private Color TierColor(PixelClicker.PixelType type)
    {
        int i = clicker.IndexOf(type);
        if (i < 0) return Color.white;
        PixelClicker.PixelTier t = clicker.Tiers[i];
        return t.translucent ? t.color : t.UIColor;
    }

    private string ItemName(Item item)
    {
        if (item.kind == ItemKind.Pixel)
        {
            int i = clicker.IndexOf(item.type);
            return i >= 0 ? clicker.Tiers[i].displayName : item.type.ToString();
        }
        int p = consumables == null ? -1 : item.combo ? consumables.FindComboPotion(item.type, item.second) : FindPotion(item.type);
        return p >= 0 ? consumables.ItemName(p) : item.type + " Potion";
    }

    private int FindPotion(PixelClicker.PixelType type)
    {
        if (consumables == null) return -1;
        for (int i = 0; i < consumables.ItemCount && !consumables.IsDevice(i); i++)
            if (!consumables.ItemCraftOnly(i) && consumables.ItemRequiredType(i) == type) return i;
        return -1;
    }

    private int FindDevice(PixelConsumables.DeviceKind kind)
    {
        if (consumables == null) return -1;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDevice(i) && consumables.DeviceKindOf(i) == kind) return i;
        return -1;
    }

    private PixelCubeIcon MakeCube(Transform parent, string name, Color color, float fill)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        PixelCubeIcon icon = go.AddComponent<PixelCubeIcon>();
        icon.color = color;
        icon.fill = fill;
        icon.spinDegreesPerSecond = cubeSpin;
        PixelUIKit.Stretch(icon.rectTransform);
        return icon;
    }

    /// <summary>A pixel is a spinning cube; a potion is a glass cube with a smaller cube of its pixel inside.</summary>
    private GameObject BuildIcon(Transform parent, Item item, Vector2 size)
    {
        GameObject root = new GameObject("Icon", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        RectTransform rr = root.GetComponent<RectTransform>();
        if (size == Vector2.zero) PixelUIKit.Stretch(rr);
        else
        {
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = size;
        }

        Color c = TierColor(item.type);
        if (item.kind == ItemKind.Pixel)
        {
            MakeCube(root.transform, "Cube", c, 0.62f);
        }
        else
        {
            if (item.combo)
            {
                // Two small cubes side by side inside the glass.
                Color c2 = TierColor(item.second);
                for (int k = 0; k < 2; k++)
                {
                    GameObject half = new GameObject("Half " + k, typeof(RectTransform));
                    half.transform.SetParent(root.transform, false);
                    RectTransform hr = half.GetComponent<RectTransform>();
                    hr.anchorMin = new Vector2(k == 0 ? 0.08f : 0.5f, 0f);
                    hr.anchorMax = new Vector2(k == 0 ? 0.5f : 0.92f, 1f);
                    hr.offsetMin = hr.offsetMax = Vector2.zero;
                    Color ck = k == 0 ? c : c2;
                    MakeCube(half.transform, "Inner", new Color(ck.r, ck.g, ck.b, 1f), 0.8f * potionInnerScale * 1.2f);
                }
            }
            else
            {
                MakeCube(root.transform, "Inner", new Color(c.r, c.g, c.b, 1f), 0.62f * potionInnerScale);
            }
            MakeCube(root.transform, "Glass", glassColor, 0.62f);
        }
        return root;
    }

    // ------------------------------------------------------------------
    // The two boxes
    // ------------------------------------------------------------------

    private void SetSlot(int index, Item item)
    {
        Slot s = slots[index];
        if (s.icon != null) Destroy(s.icon);
        s.has = true;
        s.item = item;
        s.icon = BuildIcon(s.iconHost.transform, item, Vector2.zero);
        s.label.text = ItemName(item);
        Refresh();
    }

    private void ClearSlot(int index)
    {
        Slot s = slots[index];
        if (s.icon != null) Destroy(s.icon);
        s.icon = null;
        s.has = false;
        s.label.text = "";
        Refresh();
    }

    private void QuickPlace(Item item)
    {
        int target = !slots[0].has ? 0 : !slots[1].has ? 1 : 1; // both full: replace the second
        SetSlot(target, item);
    }

    // ------------------------------------------------------------------
    // Dragging from the list
    // ------------------------------------------------------------------

    private void BeginDrag(Item item, PointerEventData e)
    {
        dragItem = item;
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = BuildIcon(canvasRoot.transform, item, new Vector2(cellSize * 0.9f, cellSize * 0.9f));
        dragGhost.transform.SetAsLastSibling();
        dragGhost.transform.position = e.position;
    }

    private void Drag(PointerEventData e)
    {
        if (dragGhost != null) dragGhost.transform.position = e.position;
    }

    private void EndDrag(PointerEventData e)
    {
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;
        for (int i = 0; i < slots.Length; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].rect, e.position, null))
            {
                SetSlot(i, dragItem);
                return;
            }
    }

    // ------------------------------------------------------------------
    // Item list
    // ------------------------------------------------------------------

    private double Have(Item item)
    {
        if (item.kind == ItemKind.Pixel) return PixelClicker.InfiniteResources ? 1e9 : Math.Floor(clicker.GetCount(item.type));
        int p = item.combo ? consumables.FindComboPotion(item.type, item.second) : FindPotion(item.type);
        return p >= 0 ? consumables.ItemOwned(p) : 0;
    }

    private void RefreshList()
    {
        List<Item> items = new List<Item>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (Math.Floor(t.count) >= 1d) items.Add(new Item(ItemKind.Pixel, t.type));
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount && !consumables.IsDevice(i); i++)
                if (consumables.ItemOwned(i) > 0)
                    items.Add(consumables.ItemCraftOnly(i)
                        ? new Item(ItemKind.Potion, consumables.ItemRequiredType(i), true, consumables.ItemSecondType(i))
                        : new Item(ItemKind.Potion, consumables.ItemRequiredType(i)));

        int columns = Mathf.Max(1, Mathf.FloorToInt((windowWidth - 60f - 20f + cellGap) / (cellSize + cellGap)));
        while (cells.Count < items.Count) cells.Add(BuildCell());

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            bool used = i < items.Count;
            if (cell.rect.gameObject.activeSelf != used) cell.rect.gameObject.SetActive(used);
            if (!used) continue;

            Item item = items[i];
            if (!cell.built || !SameItem(cell.item, item))
            {
                if (cell.icon != null) Destroy(cell.icon);
                cell.icon = BuildIcon(cell.iconHost.transform, item, Vector2.zero);
                cell.item = item;
                cell.built = true;
                cell.nameLabel.text = ItemName(item);
            }
            cell.count.text = PixelClicker.FormatNumber(Have(item));

            int col = i % columns, row = i / columns;
            cell.rect.anchoredPosition = new Vector2(col * (cellSize + cellGap), -row * (cellSize + cellGap));
        }

        int rows = Mathf.CeilToInt(items.Count / (float)columns);
        float contentHeight = items.Count > 0 ? rows * (cellSize + cellGap) : fontSize * 1.5f;
        emptyLabel.gameObject.SetActive(items.Count == 0);
        PixelUIKit.UpdateScrollView(listScroll, listBar, contentHeight, listHeight);
    }

    private Cell BuildCell()
    {
        Cell cell = new Cell();
        GameObject go = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(PixelCraftDrag));
        go.transform.SetParent(listContent, false);
        go.GetComponent<Image>().color = cellColor;
        cell.rect = go.GetComponent<RectTransform>();
        cell.rect.anchorMin = cell.rect.anchorMax = cell.rect.pivot = new Vector2(0f, 1f);
        cell.rect.sizeDelta = new Vector2(cellSize, cellSize);

        cell.iconHost = new GameObject("Icon Host", typeof(RectTransform));
        cell.iconHost.transform.SetParent(go.transform, false);
        RectTransform ir = cell.iconHost.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 0.25f);
        ir.anchorMax = Vector2.one;
        ir.offsetMin = ir.offsetMax = Vector2.zero;

        cell.count = MakeLabel(go.transform, "Count", "", fontSize * 0.7f, TextAlignmentOptions.TopRight, FontStyles.Bold);
        RectTransform cr = cell.count.rectTransform;
        cr.anchorMin = new Vector2(0.3f, 0.7f);
        cr.anchorMax = Vector2.one;
        cr.offsetMin = Vector2.zero;
        cr.offsetMax = new Vector2(-6f, -4f);

        cell.nameLabel = MakeLabel(go.transform, "Name", "", fontSize * 0.5f, TextAlignmentOptions.Center, FontStyles.Normal);
        cell.nameLabel.enableAutoSizing = true;
        cell.nameLabel.fontSizeMax = fontSize * 0.5f;
        cell.nameLabel.fontSizeMin = 9f;
        RectTransform nr = cell.nameLabel.rectTransform;
        nr.anchorMin = Vector2.zero;
        nr.anchorMax = new Vector2(1f, 0.25f);
        nr.offsetMin = new Vector2(4f, 2f);
        nr.offsetMax = new Vector2(-4f, 0f);

        PixelCraftDrag drag = go.GetComponent<PixelCraftDrag>();
        drag.onBegin = e => BeginDrag(cell.item, e);
        drag.onDrag = Drag;
        drag.onEnd = EndDrag;
        drag.onClick = e => { if (e.button == PointerEventData.InputButton.Left) QuickPlace(cell.item); };
        return cell;
    }

    // ------------------------------------------------------------------
    // Recipes
    // ------------------------------------------------------------------

    private static bool Matches(Ingredient ing, Item item) => !item.combo && ing.kind == item.kind && ing.type == item.type;

    private Recipe FindRecipe(Item x, Item y)
    {
        foreach (Recipe r in recipes)
        {
            if (r == null) continue;
            if ((Matches(r.a, x) && Matches(r.b, y)) || (Matches(r.a, y) && Matches(r.b, x))) return r;
        }
        return comboCraftingEnabled ? FindComboRecipe(x, y) : null;
    }

    /// <summary>
    /// A potion plus a pixel that can't be toggled makes a combo potion for those two pixel types.
    /// (Built on the fly, so it isn't in the Recipes list.)
    /// </summary>
    private Recipe FindComboRecipe(Item x, Item y)
    {
        Item potion, pixel;
        if (x.kind == ItemKind.Potion && !x.combo && y.kind == ItemKind.Pixel) { potion = x; pixel = y; }
        else if (y.kind == ItemKind.Potion && !y.combo && x.kind == ItemKind.Pixel) { potion = y; pixel = x; }
        else return null;

        if (potion.type == pixel.type) return null;
        int tier = clicker.IndexOf(pixel.type);
        if (tier < 0 || clicker.Tiers[tier].CanSwitchOff) return null; // only pixels that can't be toggled

        return new Recipe
        {
            label = "Combo Potion",
            a = new Ingredient(ItemKind.Potion, potion.type, 1),
            b = new Ingredient(ItemKind.Pixel, pixel.type, comboPixelCost),
            resultKind = ResultKind.Combo,
            resultPotion = potion.type,
            resultSecond = pixel.type,
        };
    }

    private int ResultIndex(Recipe r) =>
        r.resultKind == ResultKind.Potion ? FindPotion(r.resultPotion)
        : r.resultKind == ResultKind.Combo ? (consumables != null ? consumables.FindComboPotion(r.resultPotion, r.resultSecond) : -1)
        : FindDevice(r.resultDevice);

    private static bool SameIngredient(Ingredient a, Ingredient b) => a.kind == b.kind && a.type == b.type;

    private bool CanAfford(Recipe r)
    {
        Item ia = new Item(r.a.kind, r.a.type), ib = new Item(r.b.kind, r.b.type);
        if (SameIngredient(r.a, r.b)) return Have(ia) >= r.a.amount + r.b.amount;
        return Have(ia) >= r.a.amount && Have(ib) >= r.b.amount;
    }

    private string NeedLine(Ingredient ing, double have, double need)
    {
        Color c = have >= need ? enoughColor : shortColor;
        return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" +
               string.Format(needFormat, ItemName(new Item(ing.kind, ing.type)), PixelClicker.FormatNumber(have),
                             PixelClicker.FormatNumber(need)) + "</color>";
    }

    private Recipe currentRecipe;

    private void Refresh()
    {
        // An item that's used up leaves its box.
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].has && Have(slots[i].item) < 1d)
            {
                if (slots[i].icon != null) Destroy(slots[i].icon);
                slots[i].icon = null;
                slots[i].has = false;
                slots[i].label.text = "";
            }

        RefreshList();

        currentRecipe = null;
        bool canCraft = false;

        if (!slots[0].has || !slots[1].has)
        {
            recipeText.text = hintText;
        }
        else
        {
            Recipe r = FindRecipe(slots[0].item, slots[1].item);
            int result = r != null ? ResultIndex(r) : -1;
            if (r == null) recipeText.text = noRecipeText;
            else if (result < 0) recipeText.text = unavailableText;
            else
            {
                currentRecipe = r;
                Item ia = new Item(r.a.kind, r.a.type), ib = new Item(r.b.kind, r.b.type);
                StringBuilder sb = new StringBuilder();
                sb.Append(string.Format(makesFormat, consumables.ItemName(result), r.resultAmount)).Append('\n');
                if (SameIngredient(r.a, r.b))
                    sb.Append(NeedLine(r.a, Have(ia), r.a.amount + r.b.amount));
                else
                    sb.Append(NeedLine(r.a, Have(ia), r.a.amount)).Append('\n').Append(NeedLine(r.b, Have(ib), r.b.amount));
                recipeText.text = sb.ToString();
                canCraft = CanAfford(r);
            }
        }

        craftButton.interactable = canCraft;
        craftImage.color = canCraft ? craftColor : craftDisabledColor;
    }

    private void Craft()
    {
        Recipe r = currentRecipe;
        if (r == null || !CanAfford(r)) return;
        int result = ResultIndex(r);
        if (result < 0) return;

        if (!Spend(r.a) || !Spend(r.b)) return;
        consumables.AddItem(result, r.resultAmount);

        statusLabel.text = string.Format(craftedFormat, consumables.ItemName(result), r.resultAmount);
        statusTimer = 2.5f;
        Crafted?.Invoke();
        PlaySound();
        Refresh();
    }

    private bool Spend(Ingredient ing)
    {
        if (ing.kind == ItemKind.Pixel) return clicker.TrySpend(ing.type, ing.amount);
        int p = FindPotion(ing.type);
        return p >= 0 && consumables.TryRemoveItem(p, ing.amount);
    }

    private void PlaySound()
    {
        if (craftSound == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(craftSound, soundVolume);
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
