using UnityEngine;

/// <summary>
/// The Farmer (id "farmer"): a visitor who stops time and sells SEEDS at a discount. Plentiful, cheap seeds of the common pixel types
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

    protected override int OfferedWares => seedsOffered;

    protected override void ModifyWares(System.Collections.Generic.List<int> wares)
    {
        if (consumables == null || clicker == null || Random.value >= dragonSeedChance || clicker.PickMissingDragonCube() < 0) return;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (consumables.IsDragonSeed(i)) { wares.Insert(0, i); return; } // first in the list
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
        Color shirt = new Color(0.78f, 0.22f, 0.2f), denim = new Color(0.22f, 0.36f, 0.68f), denimDark = new Color(0.16f, 0.27f, 0.52f);
        Color skin = new Color(0.95f, 0.76f, 0.58f), straw = new Color(0.93f, 0.8f, 0.4f), strawDark = new Color(0.78f, 0.62f, 0.25f);
        Color boot = new Color(0.34f, 0.22f, 0.12f), wood = new Color(0.5f, 0.34f, 0.17f);
        Color sack = new Color(0.8f, 0.68f, 0.45f), leaf = new Color(0.36f, 0.78f, 0.3f), dark = new Color(0.16f, 0.1f, 0.07f);

        // Seed sack on the left, with a sprout poking out.
        Block(art, "Sack", sack, new Vector2(100f, 120f), new Vector2(-120f, 62f));
        Block(art, "Sack Tie", strawDark, new Vector2(70f, 14f), new Vector2(-120f, 118f));
        Block(art, "Sack Sprout Stem", leaf, new Vector2(8f, 36f), new Vector2(-120f, 142f));
        Block(art, "Sack Sprout Leaf", leaf, new Vector2(30f, 14f), new Vector2(-104f, 158f), 25f);

        Block(art, "Boot L", boot, new Vector2(60f, 28f), new Vector2(-34f, 14f));
        Block(art, "Boot R", boot, new Vector2(60f, 28f), new Vector2(34f, 14f));
        Block(art, "Leg L", denim, new Vector2(46f, 90f), new Vector2(-34f, 70f));
        Block(art, "Leg R", denim, new Vector2(46f, 90f), new Vector2(34f, 70f));
        Block(art, "Arm L", shirt, new Vector2(36f, 120f), new Vector2(-96f, 185f), 6f);
        Block(art, "Body", shirt, new Vector2(150f, 150f), new Vector2(0f, 190f));
        Block(art, "Overalls", denim, new Vector2(150f, 80f), new Vector2(0f, 150f));
        Block(art, "Bib", denim, new Vector2(78f, 74f), new Vector2(0f, 205f));
        Block(art, "Strap L", denimDark, new Vector2(14f, 74f), new Vector2(-40f, 225f));
        Block(art, "Strap R", denimDark, new Vector2(14f, 74f), new Vector2(40f, 225f));
        Block(art, "Pocket", denimDark, new Vector2(40f, 30f), new Vector2(0f, 190f));

        // Shotgun held across the body in the right hand, pointing up and away (axis tilted 20 degrees).
        Color gunMetal = new Color(0.2f, 0.21f, 0.24f);
        Block(art, "Gun Stock", wood, new Vector2(64f, 24f), new Vector2(52f, 219f), 20f);
        Block(art, "Gun Receiver", gunMetal, new Vector2(44f, 18f), new Vector2(86f, 231f), 20f);
        Block(art, "Gun Barrel", gunMetal, new Vector2(124f, 9f), new Vector2(153f, 256f), 20f);
        Block(art, "Gun Barrel 2", new Color(0.3f, 0.31f, 0.35f), new Vector2(124f, 4f), new Vector2(150f, 247f), 20f);
        Block(art, "Gun Pump", wood, new Vector2(40f, 16f), new Vector2(124f, 232f), 20f);
        Block(art, "Arm R", shirt, new Vector2(36f, 120f), new Vector2(98f, 185f), -22f);
        Block(art, "Hand R", skin, new Vector2(34f, 34f), new Vector2(126f, 235f));

        Block(art, "Head", skin, new Vector2(104f, 94f), new Vector2(0f, 292f));
        Block(art, "Hat Brim", straw, new Vector2(176f, 18f), new Vector2(0f, 340f));
        Block(art, "Hat Top", straw, new Vector2(94f, 46f), new Vector2(0f, 366f));
        Block(art, "Hat Band", shirt, new Vector2(96f, 12f), new Vector2(0f, 348f));
        Block(art, "Hat Stripe", strawDark, new Vector2(176f, 4f), new Vector2(0f, 332f));
        Block(art, "Eye L", dark, new Vector2(12f, 14f), new Vector2(-24f, 300f));
        Block(art, "Eye R", dark, new Vector2(12f, 14f), new Vector2(24f, 300f));
        Block(art, "Smile", dark, new Vector2(40f, 7f), new Vector2(0f, 268f));
        // A stalk of wheat sticking out of his mouth (up and to the right), with grains on the end.
        Block(art, "Wheat Stem", straw, new Vector2(5f, 64f), new Vector2(36f, 284f), -60f);
        Block(art, "Wheat Grain 1", straw, new Vector2(10f, 20f), new Vector2(70f, 304f), -60f);
        Block(art, "Wheat Grain 2", strawDark, new Vector2(10f, 18f), new Vector2(62f, 311f), -30f);
        Block(art, "Wheat Grain 3", strawDark, new Vector2(10f, 18f), new Vector2(76f, 296f), -90f);
        Block(art, "Wheat Grain 4", straw, new Vector2(9f, 16f), new Vector2(81f, 309f), -75f);
    }
}
