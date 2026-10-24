using UnityEngine;

/// <summary>
/// The Wizard (id "wizard", Minigames tab, cheap): a visitor who stops time and sells POTIONS at a big discount (every potion whose
/// pixel type you have unlocked; combo potions can only be crafted). All the behaviour is in <see cref="PixelVisitorMinigame"/>.
/// Sell tab (the shared Sell view): he buys Ectoplasm, Magic Wood and Stardust as specialty goods (PixelMarket) and TRADES them (see trades: 20 Ectoplasm -> Ghost Bait, 5 Stardust -> 1 Ultra pixel).
/// </summary>
public class PixelWizardMinigame : PixelVisitorMinigame
{
    [Header("Stock")]
    [Min(1)]
    [Tooltip("How many different potions he brings each visit (a random selection of the ones he could sell).")]
    [SerializeField] private int potionsOffered = 3;

    [Min(1)]
    [Tooltip("Fewest bottles of each potion he has.")]
    [SerializeField] private int stockMin = 1;

    [Min(1)]
    [Tooltip("Most bottles of each potion he has (so you can't stock up).")]
    [SerializeField] private int stockMax = 3;

    // ---- Trades for minigame materials (beside selling them: the shared Sell view's trade buttons) ----

    public enum TradeReward { Device, Ultra }

    /// <summary>One of his trades: 'cost' of a minigame material for something only he has.</summary>
    [System.Serializable]
    public class MaterialTrade
    {
        [Tooltip("The material: a minigame id (ghost = Ectoplasm, eviltree = Magic Wood, stardust) or a material id (PixelMaterials.CostHeld).")]
        public string goodId = "ghost";
        [Min(1f)]
        [Tooltip("How much of it the trade takes.")]
        public double cost = 20;
        [Tooltip("What you get: a consumable device (Device kind below) or Ultra pixels.")]
        public TradeReward reward = TradeReward.Device;
        [Tooltip("The consumable you get (reward Device).")]
        public PixelConsumables.DeviceKind device = PixelConsumables.DeviceKind.GhostBait;
        [Min(1)]
        [Tooltip("How many you get.")]
        public int amount = 1;
        [Tooltip("Name on the button ('Trade 20 Ectoplasm for <this>'). Empty = the item's name.")]
        public string label = "";
    }

    [Header("Trades (worth more than selling the material)")]
    [Tooltip("His trades for minigame materials, shown beside the sell price. Missing built-in ones (20 Ectoplasm -> Ghost Bait, 5 Stardust -> 1 Ultra pixel) are added back at start.")]
    [SerializeField] private System.Collections.Generic.List<MaterialTrade> trades = new System.Collections.Generic.List<MaterialTrade>();

    private static MaterialTrade[] BuiltInTrades() => new[]
    {
        new MaterialTrade { goodId = "ghost", cost = 20, reward = TradeReward.Device, device = PixelConsumables.DeviceKind.GhostBait, amount = 1, label = "Ghost Bait" },
        new MaterialTrade { goodId = "stardust", cost = 5, reward = TradeReward.Ultra, amount = 1, label = "1 Ultra pixel" },
    };

    private void EnsureTrades()
    {
        if (trades == null) trades = new System.Collections.Generic.List<MaterialTrade>();
        foreach (MaterialTrade d in BuiltInTrades())
            if (!trades.Exists(t => t != null && t.goodId == d.goodId && t.reward == d.reward && t.device == d.device)) trades.Add(d);
    }

    private void OnValidate() => EnsureTrades();

    protected override void CollectTradeOffers(SellGood good, System.Collections.Generic.List<TradeOffer> list)
    {
        EnsureTrades();
        foreach (MaterialTrade t in trades)
        {
            if (t == null || PixelMarket.MaterialKey(t.goodId) != good.marketKey) continue;
            MaterialTrade trade = t;
            string name = !string.IsNullOrEmpty(t.label) ? t.label : t.reward == TradeReward.Ultra ? t.amount + " Ultra pixel" + (t.amount == 1 ? "" : "s") : t.device.ToString();
            list.Add(new TradeOffer
            {
                cost = t.cost, label = name,
                available = () => trade.reward == TradeReward.Ultra || RoomFor(trade.device) >= trade.amount,
                give = () =>
                {
                    if (trade.reward == TradeReward.Ultra) { clicker.AddUltra(trade.amount); return true; }
                    return consumables != null && consumables.GiveDevice(trade.device, trade.amount);
                },
            });
        }
    }

    /// <summary>How many more of a device kind you can hold (its capacity, Operation storage upgrades included).</summary>
    private int RoomFor(PixelConsumables.DeviceKind kind)
    {
        if (consumables == null) return 0;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDevice(i) && !consumables.ItemAttuned(i) && consumables.DeviceKindOf(i) == kind) return consumables.ItemRoom(i);
        return 0;
    }

    protected override int OfferedWares => potionsOffered;
    protected override int StockMin => stockMin;
    protected override int StockMax => stockMax;

    public override string Id => "wizard";
    public override string DisplayName => "Wizard";

    protected override bool IsWare(int item) =>
        consumables != null && clicker != null && !consumables.IsDevice(item) && !consumables.ItemCraftOnly(item)
        && (consumables.ItemOwned(item) > 0 || clicker.IsUnlocked(consumables.ItemRequiredType(item)));

    // Once all six Prime Cubes are held the Wizard also offers the Gauntlet (first in the list, with a second line of dialogue).
    private int GauntletItem()
    {
        if (consumables == null) return -1;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDevice(i) && consumables.DeviceKindOf(i) == PixelConsumables.DeviceKind.Gauntlet) return i;
        return -1;
    }

    protected override void ModifyWares(System.Collections.Generic.List<int> wares)
    {
        int g = GauntletItem();
        if (!PixelInfinity.ShouldOfferGauntlet(clicker, consumables, g)) return;
        wares.Insert(0, g);
        AsideText = PixelInfinity.WizardAside;
    }

    protected override bool Highlighted(int item) => item >= 0 && item == GauntletItem();

    protected override int StockFor(int item) => Highlighted(item) ? 1 : base.StockFor(item);

    protected override string DefaultName => "Wizard";
    protected override string DefaultGreeting => "Ah, a customer! I am the Wizard, brewer of fine potions. For you, a mere {0}% off every bottle. Shall we deal?";
    protected override string DefaultWaresTitle => "Wizard's Potions";
    protected override string SaleFormat => "{0}% off every potion";
    protected override string DefaultArrivedLog => "The Wizard is here!";
    protected override float StartDelayOffset => 120f;
    protected override Color TitleColor => new Color(0.8f, 0.65f, 1f);

    protected override void DrawVisitor(Transform art)
    {
        // A purple cube wizard: pointy star hat, a long white beard down the front, a glowing staff.
        Color face = new Color(0.45f, 0.3f, 0.78f), top = new Color(0.64f, 0.5f, 0.92f), side = new Color(0.28f, 0.18f, 0.55f);
        Color dark = new Color(0.1f, 0.07f, 0.2f), white = new Color(0.97f, 0.97f, 1f), beard = new Color(0.93f, 0.93f, 0.97f);
        Color gold = new Color(1f, 0.82f, 0.25f), wood = new Color(0.45f, 0.3f, 0.15f), glow = new Color(0.55f, 0.95f, 1f);
        Transform a = art;
        CubeBody(a, face, top, side, new Color(0.15f, 0.1f, 0.2f), arms: false);
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        // The staff in his right hand.
        Block(a, "Staff", wood, new Vector2(12f, 330f), new Vector2(148f, 175f), -4f);
        Block(a, "Staff Orb Glow", new Color(glow.r, glow.g, glow.b, 0.35f), new Vector2(66f, 66f), new Vector2(140f, 350f), 45f);
        Block(a, "Staff Orb", glow, new Vector2(38f, 38f), new Vector2(140f, 350f), 45f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        // Eyes under bushy brows, a long beard over the lower face.
        CubeEyes(a, white, dark, 214f, 42f, 0f, 0.8f);
        Block(a, "Brow L", beard, new Vector2(64f, 14f), new Vector2(-42f, 240f), 8f);
        Block(a, "Brow R", beard, new Vector2(64f, 14f), new Vector2(42f, 240f), -8f);
        Block(a, "Beard", beard, new Vector2(176f, 92f), new Vector2(0f, 124f));
        Block(a, "Beard Tip", beard, new Vector2(110f, 44f), new Vector2(0f, 62f));
        Block(a, "Beard Point", beard, new Vector2(54f, 30f), new Vector2(0f, 38f), 0f);
        Block(a, "Mustache", beard, new Vector2(100f, 20f), new Vector2(0f, 178f));
        // Pointy hat with a star.
        Block(a, "Hat Brim", side, new Vector2(196f, 16f), new Vector2(0f, 304f));
        Block(a, "Hat Mid", face, new Vector2(130f, 52f), new Vector2(0f, 336f));
        Block(a, "Hat Top", face, new Vector2(70f, 44f), new Vector2(8f, 376f), -8f);
        Block(a, "Hat Band", gold, new Vector2(130f, 12f), new Vector2(0f, 318f));
        Block(a, "Hat Star", gold, new Vector2(20f, 20f), new Vector2(0f, 340f), 45f);
    }
}
