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

    protected override int OfferedWares => seedsOffered;
    protected override int StockMin => commonStock.x;
    protected override int StockMax => Mathf.Max(commonStock.y, 1);

    protected override int StockFor(int item)
    {
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
        consumables != null && clicker != null && consumables.IsSeedItem(item)
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
        Color boot = new Color(0.34f, 0.22f, 0.12f), wood = new Color(0.5f, 0.34f, 0.17f), metal = new Color(0.7f, 0.72f, 0.76f);
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

        // Pitchfork in the right hand.
        Block(art, "Fork Handle", wood, new Vector2(10f, 250f), new Vector2(136f, 215f), -6f);
        Block(art, "Fork Bar", metal, new Vector2(52f, 10f), new Vector2(130f, 338f), -6f);
        Block(art, "Fork Tine L", metal, new Vector2(8f, 44f), new Vector2(110f, 362f), -6f);
        Block(art, "Fork Tine M", metal, new Vector2(8f, 50f), new Vector2(130f, 366f), -6f);
        Block(art, "Fork Tine R", metal, new Vector2(8f, 44f), new Vector2(150f, 362f), -6f);
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
        Block(art, "Stalk", straw, new Vector2(6f, 26f), new Vector2(36f, 268f), -35f); // a piece of wheat in his mouth
    }
}
