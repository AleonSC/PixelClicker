using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Lumberjack (id "lumberjack"): a cube-shaped vendor like Cubie who BUYS wood goods for pure currency. He only visits while Woodcutting
/// is the skill in training (<see cref="CanVisit"/>), brings a few offers (each asks for some of a wood block you hold and pays a pure pixel type)
/// and takes a cut: the rate follows how rare each pixel is (see <c>UnitValue</c>), times <see cref="sellRate"/> and a little luck.
/// For now this is the whole skill economy for Woodcutting - selling goods for pure currency. All the visiting behaviour is in
/// <see cref="PixelVisitorMinigame"/>. Started by <see cref="PixelGuideVendor"/> once Woodcutting is available.
/// </summary>
public class PixelLumberjackVendor : PixelVisitorMinigame
{
    [Header("Offers")]
    [Min(1)]
    [Tooltip("How many offers he brings (a different wood block in each).")]
    [SerializeField] private int offerCount = 3;

    [Range(0.01f, 1f)]
    [Tooltip("Fewest fraction of the wood block you hold of one type that an offer asks for.")]
    [SerializeField] private float minSellFraction = 0.3f;

    [Range(0.01f, 1f)]
    [Tooltip("Most fraction of the wood block you hold of one type that an offer asks for.")]
    [SerializeField] private float maxSellFraction = 1f;

    [Min(0.05f)]
    [Tooltip("His cut: the pay is the fair rate (by rarity) times this (0 = 0.8).")]
    [SerializeField] private float sellRate = 0f;

    [Min(0.05f)]
    [Tooltip("Worst luck factor on a deal (1 = the plain rate).")]
    [SerializeField] private float minLuck = 0.85f;

    [Min(0.05f)]
    [Tooltip("Best luck factor on a deal.")]
    [SerializeField] private float maxLuck = 1.25f;

    [Min(1f)]
    [Tooltip("You must hold at least this many of a wood block for him to ask for it.")]
    [SerializeField] private double minHeld = 5;

    public override string Id => "lumberjack";
    public override string DisplayName => "Lumberjack";

    protected override bool IsWare(int item) => false;
    protected override string DefaultName => "Big Tim the Lumberjack";
    protected override string DefaultGreeting => "Timber! A fellow chopper, I see. I buy logs, bark, sap, leaves, and I pay in good, honest pixels. My prices are fair-ish. Got any wood to sell?";
    protected override string DefaultWaresTitle => "Today's Offers";
    protected override string SaleFormat => "Fair-ish prices!";
    protected override string DefaultArrivedLog => "The Lumberjack is here!";
    protected override float StartDelayOffset => 30f;
    protected override Color TitleColor => new Color(0.9f, 0.4f, 0.3f);
    protected override string TradeText => "Sell";
    protected override string TradePayWord => "Sell";

    /// <summary>He only comes while you are training Woodcutting.</summary>
    protected override bool CanVisit
    {
        get
        {
            PixelSkills s = PixelSkills.Instance;
            return s != null && s.TrainingId == PixelSkills.WoodcuttingId;
        }
    }

    /// <summary>Roughly how many of this pixel one unit is worth: the rarer (earned slower), the more valuable.</summary>
    private static double UnitValue(PixelClicker.PixelTier t)
    {
        double earnRate = Mathf.Max(0.0001f, t.spawnWeight) * System.Math.Max(0.0001, t.amountPerClick) / System.Math.Max(1, t.clicksToCollect);
        return 1d / earnRate;
    }

    private static bool IsPure(PixelClicker.PixelType type) => type >= PixelClicker.PixelType.White && type <= PixelClicker.PixelType.Blue;

    protected override List<Trade> RollTrades()
    {
        List<Trade> result = new List<Trade>();
        if (clicker == null) return result;
        PixelClicker.PixelTier[] tiers = clicker.Tiers;

        List<int> sellable = new List<int>();   // wood blocks you hold enough of
        List<int> pure = new List<int>();       // pure pixel types he pays in
        for (int i = 0; i < tiers.Length; i++)
        {
            if (PixelClicker.IsWoodBlock(tiers[i].type) && tiers[i].type != PixelClicker.PixelType.Twig && tiers[i].count >= minHeld) sellable.Add(i);
            if (IsPure(tiers[i].type) && tiers[i].unlocked) pure.Add(i);
        }
        if (sellable.Count == 0 || pure.Count == 0) return result;

        double rate = sellRate > 0f ? sellRate : 0.8f;
        for (int n = 0; n < offerCount && sellable.Count > 0; n++)
        {
            int si = Random.Range(0, sellable.Count);
            int oreTier = sellable[si];
            sellable.RemoveAt(si);   // a different block in every offer

            int payTier = pure[Random.Range(0, pure.Count)];
            double sell = System.Math.Max(1d, System.Math.Floor(tiers[oreTier].count * Random.Range(minSellFraction, maxSellFraction)));
            double luck = Random.Range(minLuck, maxLuck);
            double get = System.Math.Max(1d, System.Math.Floor(sell * UnitValue(tiers[oreTier]) / UnitValue(tiers[payTier]) * rate * luck));
            // payTier/getTier: you hand over the wood ('pay') and receive the pure pixels ('get').
            result.Add(new Trade { payTier = oreTier, getTier = payTier, pay = sell, get = get });
        }
        return result;
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
