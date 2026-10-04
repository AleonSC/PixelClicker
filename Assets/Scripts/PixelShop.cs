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

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    // ------------------------------------------------------------------
    // Packs
    // ------------------------------------------------------------------

    [Header("Packs")]
    [Tooltip("The shop's items. Add more entries here later.")]
    [SerializeField] private ShopPack[] packs =
    {
        CreateRgbPack(), CreateAutoClickerPack(),
        CreateIntervalUpgradePack(1), CreateClicksUpgradePack(1)
    };

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
        public TMP_Text nameLabel;
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
        PackCost[] costs = CurrentCosts(packs[packIndex]);
        if (costs == null) return true;
        foreach (PackCost cost in costs)
            if (clicker.GetCount(cost.type) < cost.amount) return false;
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
        name.richText = true;
        row.nameLabel = name;
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
            bool leveled = IsLeveled(pack);

            bool owned = IsPurchased(i); // one-time: bought. upgrade: max level.
            bool requirementMet = IsRequirementMet(i);
            bool visible = requirementMet || showLockedPacks || HasPack(i);

            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            // Stack visible rows from the top.
            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight + rowSpacing;
            visibleCount++;

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
        }

        // Panel height follows however many packs are visible.
        float height = y - (visibleCount > 0 ? rowSpacing : 0f) + panelPadding;
        panelRect.sizeDelta = new Vector2(panelWidth, height);
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
