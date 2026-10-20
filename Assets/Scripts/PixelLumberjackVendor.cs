// ---- System notes (moved here from CLAUDE.md) ----
// PixelLumberjackVendor.cs
//   The Lumberjack (id lumberjack, 'Big Tim the Lumberjack'; a cube like Cubie in a red plaid shirt with a big beard, a green beanie
//   and an axe): BUY / SELL TABS (PixelVisitorMinigame.BuySellTabs). BUY tab = his wares: the WOOD CHIPPER (a placed device, PixelConsumables DeviceKind.WoodChipper, costs wood
//   and bark), plain POTIONS of the Lumberjack pack's pixels (Wood, Bark, Sap, Leaf, Heartwood, Twig, Acorn, Whetstone, Pesticide) and NOW AND THEN (focusChance 35% a visit) the
//   LUMBERJACK'S FOCUS item (DeviceKind.FocusPotion, 1-2 in stock), and every visit the PESTICIDE SPRAY (DeviceKind.PesticideSpray: right-click = +3 Pesticide charges;
//   1-3 in stock, costs 3-8 Pesticide + 50-150 of one random pure pixel). PRICES (RollVisitPrice, once per visit): a potion costs some of its own material
//   (potionMaterialRange 10-30) + ONE random pure pixel (potionPureFraction 30-60% of its normal price); Focus costs focusHiveCost (3) Termite Hives + TWO
//   different random pure pixels (focusPureRange 100-300 each) + some of one random woodcutting pixel you have unlocked (focusWoodRange 10-30). Each potion comes 0-2 times (potionStockLow / High) - 0 = not on sale this visit. SELL tab (2026-10-16, its own panel - PixelVisitorMinigame.CustomSellPanel): pick ANY woodcutting currency you hold
//   (drop-down) - or (2026-10-20) CHARCOAL / WOOD CHIPS HUSKS lying loose on the floor (the Kiln's and the Wood Chipper's products; HuskWorthInWood:
//   charcoalWorthInWood 3 / woodChipsWorthInWood 4 Wood each; on Accept he takes that many off the floor, TakeHusks) - the AMOUNT (slider + 10% / 25% / 50% / All), what to be paid in (a pure pixel you have unlocked), then 'Ask for an offer' - he names a RANDOM price; Accept or Decline.
//   OFFER FORMULA (Offer): fair = amount x UnitValue(wood) / UnitValue(pure)   [UnitValue = 1 / (spawn weight x pay per click / clicks) - the rarer, the more it is worth]
//     x sellRate (0.8, his cut)  x bulk = 1 - bulkDiscountMax (0.3) x amount / (amount + bulkHalfAmount 400)  [big piles get a worse rate per unit]
//     x mood (rolled once per visit, moodRange 0.85-1.15)  x roll = the average of two randoms in offerRollRange 0.7-1.25 (usually near the middle, rarely an extreme)
//     (the roll is made ONCE per wood / pay pair per visit - asking again or changing the amount never rerolls it; a new visit rolls again)
//     x (1 + Woodcutting level x levelBonusPerLevel 1%, max 30%), rounded down, at least 1.
//   Only visits while Woodcutting is the skill in training (CanVisit); started by PixelGuideVendor.StartVendors as soon as Woodcutting is available; counters lumberjack.bought / lumberjack.sold.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Lumberjack (id "lumberjack"): a cube-shaped vendor like Cubie who BUYS wood goods for pure currency. He only visits while Woodcutting
/// is the skill in training (<see cref="CanVisit"/>), brings a few offers (each asks for some of a wood block you hold and pays a pure pixel type)
/// and takes a cut: the rate follows how rare each pixel is (see <c>UnitValue</c>), times <see cref="sellRate"/> and a little luck.
/// For now this is the whole skill economy for Woodcutting - selling goods for pure currency. All the visiting behaviour is in
/// <see cref="PixelVisitorMinigame"/>. Started by <see cref="PixelGuideVendor"/> once Woodcutting is available.
/// </summary>
public class PixelLumberjackVendor : PixelVisitorMinigame
{
    [Header("Buying your wood (Sell tab)")]
    [Min(0.05f)]
    [Tooltip("His cut: the offer is the fair rate (by rarity) times this (0 = 0.8).")]
    [SerializeField] private float sellRate = 0f;
    [Tooltip("Random roll on each offer (min, max); the offer uses the average of two rolls, so it is usually near the middle.")]
    [SerializeField] private Vector2 offerRollRange = new Vector2(0.7f, 1.25f);
    [Tooltip("His mood, rolled once per visit (min, max): a good day pays better.")]
    [SerializeField] private Vector2 moodRange = new Vector2(0.85f, 1.15f);
    [Range(0f, 0.9f)]
    [Tooltip("Most the per-unit price drops for a huge pile (0.3 = up to 30% less per unit).")]
    [SerializeField] private float bulkDiscountMax = 0.3f;
    [Min(1f)]
    [Tooltip("Amount at which half of the bulk discount applies.")]
    [SerializeField] private float bulkHalfAmount = 400f;
    [Range(0f, 0.05f)]
    [Tooltip("Better prices per Woodcutting level (0.01 = +1% a level, at most +30%).")]
    [SerializeField] private float levelBonusPerLevel = 0.01f;

    [Header("Buying your Charcoal and Wood Chips husks (Sell tab)")]
    [Min(0f)]
    [Tooltip("What one CHARCOAL husk (from the Kiln) is worth to him, in Wood (his fair rate: x the Wood block's value, then his cut, mood and roll). 0 = he doesn't buy them.")]
    [SerializeField] private float charcoalWorthInWood = 3f;
    [Min(0f)]
    [Tooltip("What one WOOD CHIPS husk (from the Wood Chipper) is worth to him, in Wood. 0 = he doesn't buy them.")]
    [SerializeField] private float woodChipsWorthInWood = 4f;

    [Header("Focus potions")]
    [Range(0f, 1f)]
    [Tooltip("Chance he brings Lumberjack's Focus on a visit.")]
    [SerializeField] private float focusChance = 0.35f;

    [Header("Potions he sells")]
    [Min(0)]
    [Tooltip("Fewest bottles of each potion he brings (0 = sometimes that potion doesn't come at all).")]
    [SerializeField] private int potionStockLow = 0;
    [Min(0)]
    [Tooltip("Most bottles of each potion he brings.")]
    [SerializeField] private int potionStockHigh = 2;
    [Tooltip("A potion's price in its own material (e.g. Wood for a Wood potion): random between min and max.")]
    [SerializeField] private Vector2 potionMaterialRange = new Vector2(10f, 30f);
    [Tooltip("A potion's price in one random pure pixel: this fraction (min, max) of the potion's normal shop price.")]
    [SerializeField] private Vector2 potionPureFraction = new Vector2(0.3f, 0.6f);
    [Tooltip("Pesticide Spray (3 Pesticide charges): fewest / most he brings each visit.")]
    [SerializeField] private int sprayStockLow = 1;
    [SerializeField, Tooltip("Pesticide Spray: most he brings each visit.")] private int sprayStockHigh = 3;
    [Tooltip("Pesticide Spray: its price in Pesticide (min, max).")]
    [SerializeField] private Vector2 sprayPesticideRange = new Vector2(3f, 8f);
    [Tooltip("Pesticide Spray: its price in one random pure pixel (min, max).")]
    [SerializeField] private Vector2 sprayPureRange = new Vector2(50f, 150f);
    [Tooltip("Lumberjack's Focus: how many Termite Hives it costs.")]
    [SerializeField] private int focusHiveCost = 3;
    [Tooltip("Lumberjack's Focus: the amount (min, max) of its random woodcutting pixel.")]
    [SerializeField] private Vector2 focusWoodRange = new Vector2(10f, 30f);
    [Tooltip("Lumberjack's Focus: the amount (min, max) of EACH of its two random pure pixels.")]
    [SerializeField] private Vector2 focusPureRange = new Vector2(100f, 300f);

    [Min(1)]
    [Tooltip("Most Wood Chippers he has this visit.")]
    [SerializeField] private int chipperStockMax = 2;

    public override string Id => "lumberjack";
    public override string DisplayName => "Lumberjack";

    protected override bool BuySellTabs => true;
    protected override int StockMin => 0;
    protected override int StockMax => Mathf.Max(1, Mathf.Max(potionStockHigh, chipperStockMax));

    /// <summary>The Lumberjack pack's pixels (the ones he deals in).</summary>
    private static bool IsPackPixel(PixelClicker.PixelType t) =>
        (PixelClicker.IsWoodBlock(t) && t != PixelClicker.PixelType.GreatOak) || t == PixelClicker.PixelType.Acorn || t == PixelClicker.PixelType.Whetstone || t == PixelClicker.PixelType.Pesticide;

    /// <summary>He sells the Wood Chipper and plain potions of his pack's pixels (once you have unlocked that pixel).</summary>
    protected override bool IsWare(int item)
    {
        if (consumables == null || clicker == null) return false;
        if (consumables.IsDevice(item))
        {
            PixelConsumables.DeviceKind k = consumables.DeviceKindOf(item);
            return k == PixelConsumables.DeviceKind.WoodChipper || k == PixelConsumables.DeviceKind.FocusPotion || k == PixelConsumables.DeviceKind.PesticideSpray;
        }
        if (consumables.ItemCraftOnly(item)) return false;
        PixelClicker.PixelType t = consumables.ItemRequiredType(item);
        return IsPackPixel(t) && (consumables.ItemOwned(item) > 0 || clicker.IsUnlocked(t));
    }

    /// <summary>Lumberjack's Focus only comes now and then (focusChance per visit).</summary>
    protected override void ModifyWares(List<int> wares)
    {
        if (consumables == null || Random.value < focusChance) return;
        wares.RemoveAll(w => consumables.IsDevice(w) && consumables.DeviceKindOf(w) == PixelConsumables.DeviceKind.FocusPotion);
    }

    protected override int StockFor(int item)
    {
        if (consumables != null && consumables.IsDevice(item) && consumables.DeviceKindOf(item) == PixelConsumables.DeviceKind.FocusPotion) return Random.Range(1, 3);
        if (consumables != null && consumables.IsDevice(item) && consumables.DeviceKindOf(item) == PixelConsumables.DeviceKind.PesticideSpray) return Random.Range(Mathf.Max(1, sprayStockLow), Mathf.Max(sprayStockLow, sprayStockHigh) + 1);
        if (consumables != null && consumables.IsDevice(item)) return Random.Range(1, Mathf.Max(1, chipperStockMax) + 1);
        return Random.Range(Mathf.Max(0, potionStockLow), Mathf.Max(potionStockLow, potionStockHigh) + 1);   // 0 = that potion stays home this visit
    }

    /// <summary>His prices this visit: a potion costs some of its own material + a random pure pixel; Lumberjack's Focus costs Termite Hives + two random pure pixels.</summary>
    protected override PixelShop.PackCost[] RollVisitPrice(int item)
    {
        if (consumables == null || clicker == null) return null;
        List<PixelClicker.PixelType> pure = new List<PixelClicker.PixelType>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers) if (IsPure(t.type) && t.unlocked) pure.Add(t.type);
        if (pure.Count == 0) return null;
        if (consumables.IsDevice(item))
        {
            if (consumables.DeviceKindOf(item) == PixelConsumables.DeviceKind.PesticideSpray)
            {
                // Pesticide Spray: some Pesticide + one random pure pixel.
                return new[]
                {
                    new PixelShop.PackCost { type = PixelClicker.PixelType.Pesticide, amount = Mathf.Round(Random.Range(Mathf.Min(sprayPesticideRange.x, sprayPesticideRange.y), Mathf.Max(sprayPesticideRange.x, sprayPesticideRange.y))) },
                    new PixelShop.PackCost { type = pure[Random.Range(0, pure.Count)], amount = Mathf.Round(Random.Range(Mathf.Min(sprayPureRange.x, sprayPureRange.y), Mathf.Max(sprayPureRange.x, sprayPureRange.y))) },
                };
            }
            if (consumables.DeviceKindOf(item) != PixelConsumables.DeviceKind.FocusPotion) return null;   // (the Wood Chipper keeps its normal price)
            List<PixelShop.PackCost> focus = new List<PixelShop.PackCost> { new PixelShop.PackCost { type = PixelClicker.PixelType.HornetNest, amount = Mathf.Max(1, focusHiveCost) } };
            // ...plus some of one random woodcutting pixel you have unlocked (not the hive itself)
            List<PixelClicker.PixelType> wood = new List<PixelClicker.PixelType>();
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
                if (t.unlocked && IsPackPixel(t.type) && t.type != PixelClicker.PixelType.HornetNest) wood.Add(t.type);
            if (wood.Count > 0)
                focus.Add(new PixelShop.PackCost { type = wood[Random.Range(0, wood.Count)], amount = Mathf.Round(Random.Range(Mathf.Min(focusWoodRange.x, focusWoodRange.y), Mathf.Max(focusWoodRange.x, focusWoodRange.y))) });
            for (int n = 0; n < 2 && pure.Count > 0; n++)
            {
                int k = Random.Range(0, pure.Count);
                focus.Add(new PixelShop.PackCost { type = pure[k], amount = Mathf.Round(Random.Range(Mathf.Min(focusPureRange.x, focusPureRange.y), Mathf.Max(focusPureRange.x, focusPureRange.y))) });
                pure.RemoveAt(k);   // two DIFFERENT pure pixels
            }
            return focus.ToArray();
        }
        // A plain potion: its own material + one random pure pixel at a share of its normal price.
        double normal = 0d;
        PixelShop.PackCost[] baseCosts = consumables.ItemCosts(item);
        if (baseCosts != null) foreach (PixelShop.PackCost c in baseCosts) if (c != null) normal += c.amount;
        double pureAmount = System.Math.Max(1d, System.Math.Round(normal * Random.Range(Mathf.Min(potionPureFraction.x, potionPureFraction.y), Mathf.Max(potionPureFraction.x, potionPureFraction.y))));
        float material = Mathf.Round(Random.Range(Mathf.Min(potionMaterialRange.x, potionMaterialRange.y), Mathf.Max(potionMaterialRange.x, potionMaterialRange.y)));
        return new[]
        {
            new PixelShop.PackCost { type = consumables.ItemRequiredType(item), amount = Mathf.Max(1f, material) },
            new PixelShop.PackCost { type = pure[Random.Range(0, pure.Count)], amount = pureAmount },
        };
    }
    protected override string DefaultName => "Big Tim the Lumberjack";
    protected override string DefaultGreeting => "Timber! A fellow chopper! I sell wood chippers and potions, and I'll buy any wood you've got - tell me how much and I'll name a fair-ish price.";
    protected override string DefaultWaresTitle => "Big Tim's Yard";
    protected override string SaleFormat => "Fair-ish prices!";
    protected override string DefaultArrivedLog => "The Lumberjack is here!";
    protected override float StartDelayOffset => 30f;
    protected override Color TitleColor => new Color(0.9f, 0.4f, 0.3f);
    protected override string TradeText => "Sell";
    protected override string TradePayWord => "Sell";

    /// <summary>He only comes while you are training Woodcutting.</summary>
    protected override string VisitSkillId => PixelSkills.WoodcuttingId;   // only visits while it is in training (and says so in Toggles)

    /// <summary>Roughly how many of this pixel one unit is worth: the rarer (earned slower), the more valuable.</summary>
    private static double UnitValue(PixelClicker.PixelTier t)
    {
        double earnRate = Mathf.Max(0.0001f, t.spawnWeight) * System.Math.Max(0.0001, t.amountPerClick) / System.Math.Max(1, t.clicksToCollect);
        return 1d / earnRate;
    }

    /// <summary>Husk goods he buys straight off the floor (Charcoal, Wood Chips: they only exist as husks): what one is worth in Wood, 0 = not a husk good.</summary>
    private float HuskWorthInWood(PixelClicker.PixelType t) =>
        t == PixelClicker.PixelType.Charcoal ? charcoalWorthInWood : t == PixelClicker.PixelType.WoodChips ? woodChipsWorthInWood : 0f;

    /// <summary>How many of the thing in this tier you can sell him: your currency for a wood block, the loose husks on the floor for a husk good.</summary>
    private double HeldToSell(int tier)
    {
        PixelClicker.PixelTier t = clicker.Tiers[tier];
        if (HuskWorthInWood(t.type) <= 0f) return System.Math.Floor(t.count);
        int n = 0;
        foreach (Rigidbody b in clicker.OldPixels)
            if (b != null && PixelHuskQuery.Get(b).Tier == tier && PixelHuskHolder.Takeable(clicker, b)) n++;
        return n;
    }

    /// <summary>He takes 'amount' loose husks of this tier off the floor (they shrink away upwards). False if fewer are left.</summary>
    private bool TakeHusks(int tier, double amount)
    {
        if (HeldToSell(tier) < amount) return false;
        List<Rigidbody> take = new List<Rigidbody>();
        foreach (Rigidbody b in clicker.OldPixels)
            if (take.Count < amount && b != null && PixelHuskQuery.Get(b).Tier == tier && PixelHuskHolder.Takeable(clicker, b)) take.Add(b);
        foreach (Rigidbody b in take)
            if (clicker.ReleaseOldPixel(b, false)) StartCoroutine(PixelHuskHolder.FlyIn(b.gameObject, null, b.position + Vector3.up * 1.5f, false, 0.4f));
        return true;
    }

    private static bool IsPure(PixelClicker.PixelType type) => type >= PixelClicker.PixelType.White && type <= PixelClicker.PixelType.Blue;

    // ------------------------------------------------------------------
    // Sell tab: choose what and how much, he names a price
    // ------------------------------------------------------------------

    protected override List<Trade> RollTrades() => new List<Trade>();   // (no fixed offers: the Sell tab is the panel below)
    protected override bool CustomSellPanel => true;
    protected override float SellPanelHeight => 560f;

    private TMP_Dropdown woodDropdown, payDropdown;
    private Slider amountSlider;
    private TMP_Text haveLabel, amountLabel, offerLabel;
    private Button askButton, acceptButton, declineButton;
    private readonly List<int> woodTiers = new List<int>();
    private readonly List<int> payTiers = new List<int>();
    private float mood = 1f;
    private int asks;
    private readonly Dictionary<long, double> visitRolls = new Dictionary<long, double>();   // (wood tier, pay tier) -> his random roll, fixed for the whole visit
    private double offerAmount, offerGet;
    private int offerWood = -1, offerPay = -1;
    private bool panelUpdating;

    protected override void BuildSellPanel(RectTransform parent)
    {
        mood = Random.Range(Mathf.Min(moodRange.x, moodRange.y), Mathf.Max(moodRange.x, moodRange.y));
        asks = 0;
        visitRolls.Clear();
        offerWood = -1;
        TMP_FontAsset font = VendorFont;
        Color box = new Color(0.16f, 0.16f, 0.22f, 1f);

        Label(font, parent, "Sell", 0f, 50f, 0f, 0.2f);
        woodDropdown = PixelUIKit.CreateDropdown(font, parent, "Wood", new Vector2(100f, 50f), box, box, Color.white, 24f);
        Place(woodDropdown.GetComponent<RectTransform>(), 0f, 50f, 0.2f, 1f);
        woodDropdown.onValueChanged.AddListener(_ => { if (!panelUpdating) { RefreshAmount(true); ClearOffer(); } });

        haveLabel = Label(font, parent, "", 64f, 40f, 0f, 1f);
        amountSlider = PixelUIKit.CreateSlider(parent, "Amount", new Color(0.25f, 0.25f, 0.32f, 1f), new Color(0.85f, 0.45f, 0.3f, 1f), Color.white);
        amountSlider.wholeNumbers = true;
        Place(amountSlider.GetComponent<RectTransform>(), 112f, 30f, 0f, 1f);
        amountSlider.onValueChanged.AddListener(_ => { if (!panelUpdating) { UpdateAmountLabel(); ClearOffer(); } });

        string[] quick = { "10%", "25%", "50%", "All" };
        float[] frac = { 0.1f, 0.25f, 0.5f, 1f };
        for (int i = 0; i < 4; i++)
        {
            float f = frac[i];
            Button b = PixelUIKit.CreateButton(font, parent, quick[i], quick[i], new Vector2(100f, 44f), ButtonRole.Neutral, 22f);
            Place(b.GetComponent<RectTransform>(), 160f, 44f, i * 0.13f, i * 0.13f + 0.12f);
            b.onClick.AddListener(() => { if (amountSlider.maxValue >= 1f) { amountSlider.value = Mathf.Max(1f, Mathf.Floor(amountSlider.maxValue * f)); } });
        }
        amountLabel = Label(font, parent, "", 160f, 44f, 0.54f, 1f);
        amountLabel.alignment = TextAlignmentOptions.MidlineRight;

        Label(font, parent, "Paid in", 222f, 50f, 0f, 0.2f);
        payDropdown = PixelUIKit.CreateDropdown(font, parent, "Pay", new Vector2(100f, 50f), box, box, Color.white, 24f);
        Place(payDropdown.GetComponent<RectTransform>(), 222f, 50f, 0.2f, 1f);
        payDropdown.onValueChanged.AddListener(_ => { if (!panelUpdating) ClearOffer(); });

        // (the Lumberjack's own orange - his theme colour, like the slider - so it stays a plain colour, not a role)
        askButton = PixelUIKit.CreateButton(font, parent, "Ask", "Ask for an offer", new Vector2(100f, 60f), new Color(0.65f, 0.35f, 0.2f, 1f), Color.white, 28f);
        Place(askButton.GetComponent<RectTransform>(), 292f, 60f, 0.2f, 0.8f);
        askButton.onClick.AddListener(AskOffer);

        offerLabel = PixelUIKit.CreateText(font, parent, "Offer", "", 28f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        offerLabel.richText = true;
        offerLabel.enableAutoSizing = true; offerLabel.fontSizeMax = 28f; offerLabel.fontSizeMin = 14f;
        offerLabel.textWrappingMode = TextWrappingModes.Normal;
        Place(offerLabel.rectTransform, 366f, 110f, 0f, 1f);

        acceptButton = PixelUIKit.CreateButton(font, parent, "Accept", "Accept", new Vector2(100f, 60f), ButtonRole.Success, 28f);
        Place(acceptButton.GetComponent<RectTransform>(), 486f, 60f, 0.12f, 0.46f);
        acceptButton.onClick.AddListener(AcceptOffer);
        declineButton = PixelUIKit.CreateButton(font, parent, "Decline", "Decline", new Vector2(100f, 60f), ButtonRole.Danger, 28f);
        Place(declineButton.GetComponent<RectTransform>(), 486f, 60f, 0.54f, 0.88f);
        declineButton.onClick.AddListener(() => { ClearOffer("Suit yourself. Ask again if you change your mind."); });
        RefreshLists();
    }

    protected override void OnSellPanelShown() => RefreshLists();

    private static TMP_Text Label(TMP_FontAsset font, RectTransform parent, string text, float y, float h, float x0, float x1)
    {
        TMP_Text t = PixelUIKit.CreateText(font, parent, "Label", text, 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        t.enableAutoSizing = true; t.fontSizeMax = 24f; t.fontSizeMin = 12f;
        Place(t.rectTransform, y, h, x0, x1);
        return t;
    }

    /// <summary>Stretches 'r' between the fractions x0..x1 of the panel's width, 'y' down from its top, 'h' tall.</summary>
    private static void Place(RectTransform r, float y, float h, float x0, float x1)
    {
        r.anchorMin = new Vector2(x0, 1f); r.anchorMax = new Vector2(x1, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(x0 > 0f ? 6f : 0f, -(y + h)); r.offsetMax = new Vector2(x1 < 1f ? -6f : 0f, -y);
    }

    /// <summary>Fills the drop-downs: every woodcutting currency you hold, and the pure pixels you have unlocked.</summary>
    private void RefreshLists()
    {
        if (woodDropdown == null || clicker == null) return;
        panelUpdating = true;
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        int keepWood = woodDropdown.value < woodTiers.Count ? woodTiers[woodDropdown.value] : -1;
        int keepPay = payDropdown.value < payTiers.Count ? payTiers[payDropdown.value] : -1;
        woodTiers.Clear(); payTiers.Clear();
        List<string> woodNames = new List<string>(), payNames = new List<string>();
        for (int i = 0; i < tiers.Length; i++)
        {
            bool husks = HuskWorthInWood(tiers[i].type) > 0f;
            if ((IsPackPixel(tiers[i].type) || husks) && HeldToSell(i) >= 1d)
            {
                woodTiers.Add(i);
                woodNames.Add(tiers[i].displayName + (husks ? " husks  (" + HeldToSell(i) + " on the floor)" : "  (" + PixelClicker.FormatNumberShort(HeldToSell(i)) + ")"));
            }
            if (IsPure(tiers[i].type) && tiers[i].unlocked) { payTiers.Add(i); payNames.Add(tiers[i].displayName); }
        }
        if (woodNames.Count == 0) woodNames.Add("(no wood to sell)");
        if (payNames.Count == 0) payNames.Add("(nothing)");
        woodDropdown.ClearOptions(); woodDropdown.AddOptions(woodNames);
        woodDropdown.SetValueWithoutNotify(Mathf.Max(0, woodTiers.IndexOf(keepWood)));
        woodDropdown.RefreshShownValue();
        payDropdown.ClearOptions(); payDropdown.AddOptions(payNames);
        payDropdown.SetValueWithoutNotify(Mathf.Max(0, payTiers.IndexOf(keepPay)));
        payDropdown.RefreshShownValue();
        panelUpdating = false;
        RefreshAmount(false);
        if (offerWood < 0) ClearOffer(woodTiers.Count == 0 ? "Come back when you've chopped something." : "Pick what you're selling and how much, then ask me for an offer.");
    }

    private int SelectedWood => woodTiers.Count > 0 ? woodTiers[Mathf.Clamp(woodDropdown.value, 0, woodTiers.Count - 1)] : -1;
    private int SelectedPay => payTiers.Count > 0 ? payTiers[Mathf.Clamp(payDropdown.value, 0, payTiers.Count - 1)] : -1;

    private void RefreshAmount(bool resetToHalf)
    {
        int wood = SelectedWood;
        double held = wood >= 0 ? HeldToSell(wood) : 0d;
        panelUpdating = true;
        float max = (float)System.Math.Min(held, 1e9);
        amountSlider.minValue = max >= 1f ? 1f : 0f;
        amountSlider.maxValue = Mathf.Max(amountSlider.minValue, max);
        amountSlider.interactable = max >= 1f;
        if (resetToHalf || amountSlider.value > max || amountSlider.value < 1f) amountSlider.SetValueWithoutNotify(Mathf.Max(amountSlider.minValue, Mathf.Floor(max * 0.5f)));
        panelUpdating = false;
        PixelUIKit.SetText(haveLabel, wood >= 0 ? "You have " + PixelClicker.FormatNumberShort(held) + " " + clicker.Tiers[wood].displayName : "You have no wood to sell");
        UpdateAmountLabel();
    }

    private void UpdateAmountLabel()
    {
        int wood = SelectedWood;
        PixelUIKit.SetText(amountLabel, wood >= 0 ? "Selling " + PixelClicker.FormatNumberShort(amountSlider.value) + " " + clicker.Tiers[wood].displayName : "");
        askButton.interactable = wood >= 0 && SelectedPay >= 0 && amountSlider.value >= 1f;
    }

    /// <summary>His price for 'amount' of tier 'wood', paid in tier 'pay' (the formula is in the header).</summary>
    private double Offer(int wood, int pay, double amount)
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        float huskWorth = HuskWorthInWood(tiers[wood].type);   // (a husk good is valued as that much Wood)
        int woodBlock = clicker.IndexOf(PixelClicker.PixelType.Wood);
        double unit = huskWorth > 0f ? (woodBlock >= 0 ? huskWorth * UnitValue(tiers[woodBlock]) : huskWorth) : UnitValue(tiers[wood]);
        double fair = amount * unit / UnitValue(tiers[pay]);
        double rate = sellRate > 0f ? sellRate : 0.8f;
        double bulk = 1d - bulkDiscountMax * amount / (amount + System.Math.Max(1d, bulkHalfAmount));
        // His random roll is made ONCE per wood / pay pair per visit, so asking again (or changing the amount) never rerolls the price.
        long key = (long)wood * 100000L + pay;
        if (!visitRolls.TryGetValue(key, out double roll))
        {
            float lo = Mathf.Min(offerRollRange.x, offerRollRange.y), hi = Mathf.Max(offerRollRange.x, offerRollRange.y);
            roll = (Random.Range(lo, hi) + Random.Range(lo, hi)) * 0.5;
            visitRolls[key] = roll;
        }
        double level = 1d + System.Math.Min(0.3, levelBonusPerLevel * PixelSkills.LevelOfId(PixelSkills.WoodcuttingId));
        return System.Math.Max(1d, System.Math.Floor(fair * rate * bulk * mood * roll * level));
    }

    private void AskOffer()
    {
        int wood = SelectedWood, pay = SelectedPay;
        double amount = System.Math.Floor(amountSlider.value);
        if (wood < 0 || pay < 0 || amount < 1d) return;
        offerWood = wood; offerPay = pay; offerAmount = amount;
        offerGet = Offer(wood, pay, amount);
        asks++;
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        string say = asks <= 1 ? (mood >= 1.05f ? "Good day today! " : mood <= 0.92f ? "Rough morning... " : "")
                   : "Same as before, friend - my price is my price. ";
        string gold = ColorUtility.ToHtmlStringRGB(new Color(1f, 0.85f, 0.4f));
        PixelUIKit.SetText(offerLabel, say + "I'll give you <b><color=#" + gold + ">" + PixelClicker.FormatNumber(offerGet) + " " + tiers[pay].displayName + "</color></b> for "
                                       + PixelClicker.FormatNumberShort(amount) + " " + tiers[wood].displayName + ".");
        acceptButton.gameObject.SetActive(true);
        declineButton.gameObject.SetActive(true);
        PixelAudio.Play("ui_click");
    }

    private void AcceptOffer()
    {
        if (offerWood < 0 || clicker == null) return;
        bool paid = HuskWorthInWood(clicker.Tiers[offerWood].type) > 0f ? TakeHusks(offerWood, offerAmount) : clicker.TrySpend(offerWood, offerAmount);
        if (!paid) { ClearOffer("Looks like you don't have that much any more."); RefreshLists(); return; }
        clicker.AddCurrency(offerPay, offerGet);
        PixelStats.Count(Id + ".sold");
        PixelAudio.Play("purchase");
        string done = "Pleasure doing business! " + PixelClicker.FormatNumber(offerGet) + " " + clicker.Tiers[offerPay].displayName + " for you.";
        offerWood = -1;
        RefreshLists();
        ClearOffer(done);
    }

    private void ClearOffer(string message = null)
    {
        offerWood = -1;
        if (acceptButton != null) acceptButton.gameObject.SetActive(false);
        if (declineButton != null) declineButton.gameObject.SetActive(false);
        if (offerLabel != null && message != null) PixelUIKit.SetText(offerLabel, message);
        else if (offerLabel != null) PixelUIKit.SetText(offerLabel, "");
    }

    /// <summary>A cube like Cubie in a red plaid shirt: a big beard, a green beanie, an axe and friendly eyes.</summary>
    protected override void DrawVisitor(Transform art)
    {
        Color face = new Color(0.78f, 0.2f, 0.18f), top = new Color(0.92f, 0.42f, 0.36f), side = new Color(0.5f, 0.12f, 0.12f), plaid = new Color(0.28f, 0.06f, 0.06f, 0.65f);
        Color dark = new Color(0.1f, 0.07f, 0.05f), white = new Color(0.97f, 0.97f, 0.9f), beard = new Color(0.36f, 0.22f, 0.12f), beardDark = new Color(0.26f, 0.15f, 0.08f);
        Color beanie = new Color(0.2f, 0.5f, 0.3f), beanieDark = new Color(0.14f, 0.36f, 0.22f);
        Color wood = new Color(0.55f, 0.36f, 0.16f), steel = new Color(0.7f, 0.74f, 0.8f), steelDark = new Color(0.45f, 0.48f, 0.55f);
        Transform a = art;

        CubeBody(a, face, top, side, dark, arms: false);
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        // Plaid stripes over the front.
        for (int i = 0; i < 3; i++) Block(a, "Plaid H" + i, plaid, new Vector2(200f, 12f), new Vector2(0f, 120f + i * 60f));
        for (int i = 0; i < 3; i++) Block(a, "Plaid V" + i, plaid, new Vector2(12f, 200f), new Vector2(-60f + i * 60f, 180f));
        // The axe in his right hand: a long handle, a steel blade on top.
        Block(a, "Axe Handle", wood, new Vector2(12f, 250f), new Vector2(150f, 190f), -12f);
        Block(a, "Axe Blade", steel, new Vector2(64f, 56f), new Vector2(168f, 318f), -12f);
        Block(a, "Axe Edge", steelDark, new Vector2(14f, 56f), new Vector2(196f, 316f), -12f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        // Eyes and a big beard.
        CubeEyes(a, white, dark, 214f, 42f, 0f, 0.9f);
        Block(a, "Brow L", beardDark, new Vector2(60f, 12f), new Vector2(-42f, 240f), 6f);
        Block(a, "Brow R", beardDark, new Vector2(60f, 12f), new Vector2(42f, 240f), -6f);
        Block(a, "Beard", beard, new Vector2(196f, 96f), new Vector2(0f, 130f));
        Block(a, "Beard Low", beard, new Vector2(150f, 40f), new Vector2(0f, 76f));
        Block(a, "Moustache", beardDark, new Vector2(120f, 22f), new Vector2(0f, 168f));
        Block(a, "Smile", dark, new Vector2(46f, 8f), new Vector2(0f, 148f));
        // Green beanie.
        Block(a, "Beanie Band", beanieDark, new Vector2(206f, 22f), new Vector2(0f, 304f));
        Block(a, "Beanie", beanie, new Vector2(180f, 44f), new Vector2(0f, 332f));
        Block(a, "Beanie Pom", beanieDark, new Vector2(34f, 26f), new Vector2(0f, 366f));
    }
}
