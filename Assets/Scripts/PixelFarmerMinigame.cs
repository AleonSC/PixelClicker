using UnityEngine;

/// <summary>
/// The Farmer (id "farmer"): a visitor who stops time and sells SEEDS at a discount (Buy tab) and buys your Farming goods (Sell tab, the shared goods buyer). Plentiful, cheap seeds of the common pixel types
/// (big stock), a few expensive ones of the rare types (small stock). Started by <see cref="PixelGuideVendor"/> like the other vendors;
/// all the behaviour (sliding in, greeting, accept / reject, wares window) is in <see cref="PixelVisitorMinigame"/>.
/// </summary>
public class PixelFarmerMinigame : PixelVisitorMinigame
{
    [Header("Stock")]
    [Min(1)]
    [Tooltip("How many different seeds he brings each visit (a random selection of the ones he could sell).")]
    [SerializeField] private int seedsOffered = 5;

    [Tooltip("Stock of a common seed (spawn weight 1 or more): fewest and most he has.")]
    [SerializeField] private Vector2Int commonStock = new Vector2Int(10, 30);

    [Tooltip("Stock of an uncommon seed (spawn weight 0.3 to 1): fewest and most he has.")]
    [SerializeField] private Vector2Int uncommonStock = new Vector2Int(4, 10);

    [Tooltip("Stock of a rare seed (spawn weight under 0.3): fewest and most he has.")]
    [SerializeField] private Vector2Int rareStock = new Vector2Int(1, 3);

    [Range(0f, 1f)]
    [Tooltip("Chance per visit that he also has a Dragon Seed (a seed that grows a Dragon Cube you don't hold). Only offered while you lack at least one Dragon Cube. Shown glowing yellow, always stock 1.")]
    [SerializeField] private float dragonSeedChance = 0.05f;

    [Tooltip("What he says (a second speech box, after you accept) when he has a Dragon Seed.")]
    [TextArea(2, 4)]
    [SerializeField] private string dragonAsideText = "Some guys with tails literally FLEW BY and dropped these. I ain't gonna say no to some extra cash though!";

    /// <summary>Dev tools: the Farmer brings a Dragon Seed on every visit (while you lack a Dragon Cube).</summary>
    public static bool DevAlwaysDragonSeeds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { DevAlwaysDragonSeeds = false; }

    protected override int OfferedWares => seedsOffered;

    [Header("Buying your farm goods (Sell tab)")]
    [Tooltip("How he makes his offers for your Farming goods on the Sell tab (the price itself is PixelMarket's shared rule).")]
    [SerializeField] private GoodsOffers offers = new GoodsOffers();

    // Buy tab = his seeds, Sell tab = he buys Farming's goods (wheat, golden fruit, dirt...) - the shared goods buyer (PixelVisitorMinigameGoods.cs).
    protected override bool BuySellTabs => true;
    protected override bool BuysGoods => true;
    protected override string GoodsSkillId => PixelSkills.FarmingId;
    protected override GoodsOffers Offers => offers;
    protected override System.Collections.Generic.List<Trade> RollTrades() => RollGoodsTrades();
    protected override string TradeText => "Sell";
    protected override string TradePayWord => "Sell";

    /// <summary>She only comes while you are training Farming (like the Lumberjack with Woodcutting).</summary>
    protected override string VisitSkillId => PixelSkills.FarmingId;   // only visits while it is in training (and says so in Toggles)

    protected override void ModifyWares(System.Collections.Generic.List<int> wares)
    {
        if (consumables == null || clicker == null || (!DevAlwaysDragonSeeds && Random.value >= dragonSeedChance) || clicker.PickMissingDragonCube() < 0) return;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDragonSeed(i)) { wares.Insert(0, i); AsideText = dragonAsideText; return; } // first in the list, with a second line of dialogue
    }

    protected override void ApplyDialogue(PixelTextFile f, string section)
    {
        f.Apply(section, "dragon aside", ref dragonAsideText);
    }

    protected override bool Highlighted(int item) => consumables != null && consumables.IsDragonSeed(item);
    protected override int StockMin => commonStock.x;
    protected override int StockMax => Mathf.Max(commonStock.y, 1);

    protected override int StockFor(int item)
    {
        if (consumables != null && consumables.IsDragonSeed(item)) return 1;
        float weight = 1f;
        if (clicker != null && consumables != null)
        {
            int t = clicker.IndexOf(consumables.SeedTypeOf(item));
            if (t >= 0) weight = clicker.Tiers[t].spawnWeight;
        }
        Vector2Int range = weight >= 1f ? commonStock : weight >= 0.3f ? uncommonStock : rareStock;
        return Random.Range(Mathf.Max(1, range.x), Mathf.Max(range.x, range.y, 1) + 1);
    }

    public override string Id => "farmer";
    public override string DisplayName => "Farmer";

    protected override bool IsWare(int item) =>
        consumables != null && clicker != null && consumables.IsSeedItem(item) && !consumables.IsDragonSeed(item)
        && (consumables.ItemOwned(item) > 0 || clicker.IsUnlocked(consumables.ItemRequiredType(item)));

    protected override string DefaultName => "Farmer";
    protected override string DefaultGreeting => "Howdy! I'm the Farmer. Seeds for every pixel under the sun, and for you a fair {0}% off. Plant 'em, water 'em, watch 'em grow. Care to take a look?";
    protected override string DefaultWaresTitle => "Farmer's Seeds";
    protected override string SaleFormat => "{0}% off every seed";
    protected override string DefaultArrivedLog => "The Farmer is here!";
    protected override float StartDelayOffset => 60f;
    protected override Color TitleColor => new Color(0.55f, 0.9f, 0.4f);

    protected override void DrawVisitor(Transform art)
    {
        // A leafy-green cube farmer: straw hat, black sideburns and wispy whiskers, a wheat stalk, the shotgun and a seed sack.
        Color face = new Color(0.48f, 0.66f, 0.3f), top = new Color(0.68f, 0.84f, 0.48f), side = new Color(0.3f, 0.45f, 0.18f);
        Color straw = new Color(0.93f, 0.8f, 0.4f), strawDark = new Color(0.78f, 0.62f, 0.25f), boot = new Color(0.34f, 0.22f, 0.12f), wood = new Color(0.5f, 0.34f, 0.17f);
        Color sack = new Color(0.8f, 0.68f, 0.45f), leaf = new Color(0.36f, 0.78f, 0.3f), dark = new Color(0.12f, 0.09f, 0.06f), white = new Color(0.97f, 0.97f, 0.9f);
        Color red = new Color(0.78f, 0.22f, 0.2f);
        Transform a = art;

        // Seed sack on the left, with a sprout poking out.
        Block(a, "Sack", sack, new Vector2(70f, 100f), new Vector2(-136f, 54f));
        Block(a, "Sack Tie", strawDark, new Vector2(50f, 12f), new Vector2(-136f, 100f));
        Block(a, "Sack Sprout Stem", leaf, new Vector2(8f, 30f), new Vector2(-136f, 122f));
        Block(a, "Sack Sprout Leaf", leaf, new Vector2(26f, 12f), new Vector2(-122f, 136f), 25f);

        CubeBody(a, face, top, side, boot, arms: false);
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        // Overalls bib on the front of the cube's lower half.
        Block(a, "Overalls", new Color(0.22f, 0.36f, 0.68f), new Vector2(200f, 56f), new Vector2(0f, 108f));
        Block(a, "Bib Pocket", new Color(0.16f, 0.27f, 0.52f), new Vector2(46f, 26f), new Vector2(0f, 108f));
        // Shotgun held upright against his side in the right hand.
        Color gunMetal = new Color(0.2f, 0.21f, 0.24f);
        Vector2 gunC = new Vector2(150f, 190f), gunD = new Vector2(Mathf.Cos(78f * Mathf.Deg2Rad), Mathf.Sin(78f * Mathf.Deg2Rad));
        Block(a, "Gun Stock", wood, new Vector2(52f, 22f), gunC - gunD * 62f, 78f);
        Block(a, "Gun Receiver", gunMetal, new Vector2(40f, 17f), gunC - gunD * 24f, 78f);
        Block(a, "Gun Barrel", gunMetal, new Vector2(100f, 9f), gunC + gunD * 48f, 78f);
        Block(a, "Gun Pump", wood, new Vector2(36f, 15f), gunC + gunD * 6f, 78f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        // Face: small eyes, black sideburns, wispy moustache, a stalk of wheat in the mouth.
        CubeEyes(a, white, dark, 204f, 42f, 0f, 0.85f);
        Block(a, "Sideburn L", dark, new Vector2(16f, 70f), new Vector2(-92f, 190f));
        Block(a, "Sideburn R", dark, new Vector2(16f, 70f), new Vector2(92f, 190f));
        Block(a, "Mustache L", dark, new Vector2(46f, 8f), new Vector2(-26f, 160f), 10f);
        Block(a, "Mustache R", dark, new Vector2(46f, 8f), new Vector2(26f, 160f), -10f);
        Block(a, "Mouth", dark, new Vector2(40f, 7f), new Vector2(0f, 142f));
        Block(a, "Wheat Stem", straw, new Vector2(5f, 70f), new Vector2(40f, 150f), -62f);
        Block(a, "Wheat Grain 1", straw, new Vector2(10f, 20f), new Vector2(74f, 172f), -62f);
        Block(a, "Wheat Grain 2", strawDark, new Vector2(10f, 18f), new Vector2(66f, 180f), -32f);
        Block(a, "Wheat Grain 3", strawDark, new Vector2(10f, 18f), new Vector2(80f, 164f), -90f);
        // Straw hat.
        Block(a, "Hat Brim", straw, new Vector2(230f, 16f), new Vector2(0f, 304f));
        Block(a, "Hat Top", straw, new Vector2(120f, 46f), new Vector2(0f, 332f));
        Block(a, "Hat Band", red, new Vector2(122f, 12f), new Vector2(0f, 314f));
        Block(a, "Hat Stripe", strawDark, new Vector2(230f, 4f), new Vector2(0f, 298f));
    }
}
