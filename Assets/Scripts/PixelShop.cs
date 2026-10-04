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

    /// <summary>Something purchasable: a price and the tiers it unlocks.</summary>
    [Serializable]
    public class ShopPack
    {
        [Tooltip("Name shown in the shop.")]
        public string displayName = "Pack";

        [TextArea(1, 3)]
        [Tooltip("Short description shown under the name. Use {interval} to show the auto clicker's interval in seconds.")]
        public string description = "";

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
    }

    // ------------------------------------------------------------------
    // References
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker to use. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The auto clicker switched on by the Auto Clicker pack. Taken from this GameObject (or found in the scene) if empty. Edit its interval on that component.")]
    [SerializeField] private PixelAutoClicker autoClicker;

    [Tooltip("Font for the shop UI. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    [Header("Packs")]
    [Tooltip("The shop's items. Add more entries here later.")]
    [SerializeField] private ShopPack[] packs = { CreateRgbPack(), CreateAutoClickerPack() };

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

    /// <summary>Default Auto Clicker pack: all six pixel types, available after the RGB pack (index 0).</summary>
    private static ShopPack CreateAutoClickerPack()
    {
        return new ShopPack
        {
            displayName = "Auto Clicker",
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
        public TMP_Text descLabel;
        public Button buyButton;
        public Image buyImage;
        public TMP_Text buyLabel;
        public TMP_Text costLabel;
    }

    private RectTransform panelRect;
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

        // Make sure every reward tier exists in PixelClicker (added locked, unlocked on purchase).
        foreach (ShopPack pack in packs)
        {
            if (pack.rewardTiers == null) continue;
            foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                clicker.EnsureTier(reward);
        }
    }

    private void Start()
    {
        EnsureEventSystem();
        BuildUI();
        builtOk = true;
    }

    private void OnDestroy()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void Update()
    {
        // Packs ticked as "Purchased" in the Inspector (before or during Play) take effect too.
        foreach (ShopPack pack in packs)
            if (pack.purchased) ApplyPackEffects(pack);

        if (!builtOk) return;

        bool visible = alwaysShowButton || clicker.IsUnlocked(requiredTier);
        if (shopButtonObject.activeSelf != visible) shopButtonObject.SetActive(visible);

        if (visible && !wasButtonVisible && openWhenFirstAvailable) panelObject.SetActive(true);
        wasButtonVisible = visible;

        if (!visible && panelObject.activeSelf) panelObject.SetActive(false);

        if (panelObject.activeSelf) RefreshRows();
    }

    // ------------------------------------------------------------------
    // Shop logic
    // ------------------------------------------------------------------

    /// <summary>True when every reward tier of the pack is already unlocked.</summary>
    public bool IsPurchased(int packIndex)
    {
        ShopPack pack = packs[packIndex];
        if (pack.purchased) return true;
        if (pack.rewardTiers == null || pack.rewardTiers.Length == 0) return false;
        foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
            if (!clicker.IsUnlocked(reward.type)) return false;
        return true;
    }

    /// <summary>True when the pack's required pack (if any) has been bought.</summary>
    public bool IsRequirementMet(int packIndex)
    {
        int req = packs[packIndex].requiresPackIndex;
        if (req < 0 || req >= packs.Length || req == packIndex) return true;
        return IsPurchased(req);
    }

    /// <summary>True when the player holds enough of every currency in the pack's cost.</summary>
    public bool CanAfford(int packIndex)
    {
        PackCost[] costs = packs[packIndex].costs;
        if (costs == null) return true;
        foreach (PackCost cost in costs)
            if (clicker.GetCount(cost.type) < cost.amount) return false;
        return true;
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

        if (pack.costs != null)
            foreach (PackCost cost in pack.costs)
                clicker.TrySpend(cost.type, cost.amount);

        pack.purchased = true;
        ApplyPackEffects(pack);

        if (purchaseSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(purchaseSound, soundVolume);
        }

        onPackPurchased?.Invoke(packIndex);
        RefreshRows();
        return true;
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
        float height = headerHeight + panelPadding + packs.Length * (rowHeight + rowSpacing) + panelPadding - rowSpacing;

        panelObject = new GameObject("Shop Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelWidth, height);
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

        // Pack rows
        rows = new PackRow[packs.Length];
        for (int i = 0; i < packs.Length; i++)
            rows[i] = BuildRow(panelObject.transform, i);

        panelObject.SetActive(false);
    }

    private PackRow BuildRow(Transform parent, int index)
    {
        ShopPack pack = packs[index];
        PackRow row = new PackRow();

        GameObject rowGo = new GameObject("Pack " + index, typeof(RectTransform), typeof(Image));
        rowGo.transform.SetParent(parent, false);
        rowGo.GetComponent<Image>().color = rowColor;

        RectTransform rr = rowGo.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(-panelPadding * 2f, rowHeight);
        rr.anchoredPosition = new Vector2(0f, -(headerHeight + index * (rowHeight + rowSpacing))); // re-laid out in RefreshRows
        row.rect = rr;

        float textRightInset = buyButtonSize.x + 40f; // keep text clear of the Buy button

        TMP_Text name = CreateText(rowGo.transform, "Name", pack.displayName, nameFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetBand(name.rectTransform, 0.70f, 1f, textRightInset);

        TMP_Text desc = CreateText(rowGo.transform, "Description", pack.description, descriptionFontSize,
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
        row.buyButton.onClick.AddListener(() => TryBuy(captured));

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
        float y = headerHeight;
        int visibleCount = 0;

        for (int i = 0; i < packs.Length && i < rows.Length; i++)
        {
            PackRow row = rows[i];
            ShopPack pack = packs[i];

            bool owned = IsPurchased(i);
            bool requirementMet = IsRequirementMet(i);
            bool visible = requirementMet || showLockedPacks || owned;

            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            // Stack visible rows from the top.
            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight + rowSpacing;
            visibleCount++;

            row.descLabel.text = ResolveDescription(pack);

            bool affordable = CanAfford(i);
            bool canBuy = !owned && requirementMet && affordable;

            if (owned) row.costLabel.text = "";
            else if (!requirementMet) row.costLabel.text = string.Format(requiresFormat, RequirementName(pack));
            else row.costLabel.text = BuildCostText(pack);

            row.buyButton.interactable = canBuy;
            row.buyLabel.text = owned ? ownedText : (requirementMet ? buyText : lockedText);
            row.buyImage.color = canBuy ? buyColor : disabledColor;
        }

        // Panel height follows however many packs are visible.
        float height = y - (visibleCount > 0 ? rowSpacing : 0f) + panelPadding;
        panelRect.sizeDelta = new Vector2(panelWidth, height);
    }

    private string ResolveDescription(ShopPack pack)
    {
        string text = pack.description ?? "";
        if (autoClicker != null) text = text.Replace("{interval}", autoClicker.Interval.ToString("0.##"));
        return text;
    }

    private string RequirementName(ShopPack pack)
    {
        int req = pack.requiresPackIndex;
        return req >= 0 && req < packs.Length ? packs[req].displayName : "?";
    }

    /// <summary>"Cost: 100 White Pixels  100 Gray Pixels ..." with each part green/red by affordability.</summary>
    private string BuildCostText(ShopPack pack)
    {
        if (pack.costs == null || pack.costs.Length == 0) return costPrefix + "Free";

        StringBuilder sb = new StringBuilder(costPrefix);
        for (int i = 0; i < pack.costs.Length; i++)
        {
            PackCost cost = pack.costs[i];
            int tierIndex = clicker.IndexOf(cost.type);
            string name = tierIndex >= 0 ? clicker.Tiers[tierIndex].displayName : cost.type.ToString();
            bool enough = clicker.GetCount(cost.type) >= cost.amount;

            string part = string.Format(costEntryFormat, PixelClicker.FormatNumber(cost.amount), name);
            sb.Append("<color=#")
              .Append(ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor))
              .Append('>').Append(part).Append("</color>");

            if (i < pack.costs.Length - 1) sb.Append("   ");
        }
        return sb.ToString();
    }
}
