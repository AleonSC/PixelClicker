using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Prospector (id "prospector"): a cube-shaped vendor like Cubie who BUYS mining goods for pure currency. He only visits while Mining
/// is the skill in training (<see cref="CanVisit"/>), brings a few offers (each asks for some of an ore you hold and pays a pure pixel type)
/// and takes a cut: the rate follows how rare each pixel is (see <c>UnitValue</c>), times <see cref="sellRate"/> and a little luck.
/// For now this is the whole skill economy for Mining - selling goods for pure currency. All the visiting behaviour is in
/// <see cref="PixelVisitorMinigame"/>. Started by <see cref="PixelGuideVendor"/> once Mining is available.
/// </summary>
public class PixelProspectorVendor : PixelVisitorMinigame
{
    [Header("Offers")]
    [Min(1)]
    [Tooltip("How many offers he brings (a different ore in each).")]
    [SerializeField] private int offerCount = 3;

    [Range(0.01f, 1f)]
    [Tooltip("Fewest fraction of the ore you hold of one type that an offer asks for.")]
    [SerializeField] private float minSellFraction = 0.3f;

    [Range(0.01f, 1f)]
    [Tooltip("Most fraction of the ore you hold of one type that an offer asks for.")]
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
    [Tooltip("You must hold at least this many of an ore for him to ask for it.")]
    [SerializeField] private double minHeld = 5;

    public override string Id => "prospector";
    public override string DisplayName => "Prospector";

    protected override bool IsWare(int item) => false;
    protected override string DefaultName => "Pip the Prospector";
    protected override string DefaultGreeting => "Well, look at you, a fellow digger! I buy rocks, ore and anything shiny. Pure pixels, fresh from the vault. My prices are fair-ish. Care to sell?";
    protected override string DefaultWaresTitle => "Today's Offers";
    protected override string SaleFormat => "Fair-ish prices!";
    protected override string DefaultArrivedLog => "The Prospector is here!";
    protected override float StartDelayOffset => 30f;
    protected override Color TitleColor => new Color(1f, 0.7f, 0.35f);
    protected override string TradeText => "Sell";
    protected override string TradePayWord => "Sell";

    /// <summary>He only comes while you are training Mining.</summary>
    protected override bool CanVisit
    {
        get
        {
            PixelSkills s = PixelSkills.Instance;
            return s != null && s.TrainingId == PixelSkills.MiningId;
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

        List<int> sellable = new List<int>();   // ores you hold enough of
        List<int> pure = new List<int>();       // pure pixel types he pays in
        for (int i = 0; i < tiers.Length; i++)
        {
            if ((PixelClicker.IsOre(tiers[i].type) || tiers[i].type == PixelClicker.PixelType.Obsidian) && tiers[i].count >= minHeld) sellable.Add(i);
            if (IsPure(tiers[i].type) && tiers[i].unlocked) pure.Add(i);
        }
        if (sellable.Count == 0 || pure.Count == 0) return result;

        double rate = sellRate > 0f ? sellRate : 0.8f;
        for (int n = 0; n < offerCount && sellable.Count > 0; n++)
        {
            int si = Random.Range(0, sellable.Count);
            int oreTier = sellable[si];
            sellable.RemoveAt(si);   // a different ore in every offer

            int payTier = pure[Random.Range(0, pure.Count)];
            double sell = System.Math.Max(1d, System.Math.Floor(tiers[oreTier].count * Random.Range(minSellFraction, maxSellFraction)));
            double luck = Random.Range(minLuck, maxLuck);
            double get = System.Math.Max(1d, System.Math.Floor(sell * UnitValue(tiers[oreTier]) / UnitValue(tiers[payTier]) * rate * luck));
            // payTier/getTier: you hand over the ore ('pay') and receive the pure pixels ('get').
            result.Add(new Trade { payTier = oreTier, getTier = payTier, pay = sell, get = get });
        }
        return result;
    }

    /// <summary>A cube like Cubie, in earth colours: a hard hat with a lamp, a pickaxe and a friendly grin.</summary>
    protected override void DrawVisitor(Transform art)
    {
        Color face = new Color(0.82f, 0.5f, 0.24f), top = new Color(0.95f, 0.7f, 0.42f), side = new Color(0.55f, 0.32f, 0.16f);
        Color dark = new Color(0.1f, 0.07f, 0.05f), white = new Color(0.97f, 0.97f, 0.9f), yellow = new Color(1f, 0.82f, 0.2f);
        Color helmet = new Color(0.98f, 0.78f, 0.15f), helmetDark = new Color(0.78f, 0.58f, 0.08f);
        Color wood = new Color(0.5f, 0.32f, 0.14f), steel = new Color(0.62f, 0.66f, 0.72f), steelDark = new Color(0.4f, 0.43f, 0.5f);
        Transform a = art;

        Block(a, "Leg L", side, new Vector2(40f, 50f), new Vector2(-45f, 28f));
        Block(a, "Leg R", side, new Vector2(40f, 50f), new Vector2(45f, 28f));
        Block(a, "Boot L", dark, new Vector2(58f, 22f), new Vector2(-48f, 11f));
        Block(a, "Boot R", dark, new Vector2(58f, 22f), new Vector2(48f, 11f));
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        // The pickaxe: a long handle held in the right hand, a steel head across its top.
        Block(a, "Pick Handle", wood, new Vector2(12f, 250f), new Vector2(150f, 190f), -14f);
        Block(a, "Pick Head", steel, new Vector2(120f, 22f), new Vector2(133f, 318f), -14f);
        Block(a, "Pick Tip L", steelDark, new Vector2(26f, 18f), new Vector2(76f, 310f), 26f);
        Block(a, "Pick Tip R", steelDark, new Vector2(26f, 18f), new Vector2(190f, 326f), -54f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        Block(a, "Side", side, new Vector2(200f, 200f), new Vector2(12f, 190f));
        Block(a, "Face", face, new Vector2(200f, 200f), new Vector2(0f, 180f));
        Block(a, "Top", top, new Vector2(200f, 26f), new Vector2(6f, 290f));
        // Hard hat with a lamp.
        Block(a, "Hat Brim", helmetDark, new Vector2(176f, 14f), new Vector2(0f, 302f));
        Block(a, "Hat Top", helmet, new Vector2(120f, 46f), new Vector2(0f, 330f));
        Block(a, "Hat Ridge", helmetDark, new Vector2(24f, 46f), new Vector2(0f, 334f));
        Block(a, "Lamp", white, new Vector2(30f, 24f), new Vector2(0f, 326f));
        Block(a, "Lamp Glow", yellow, new Vector2(18f, 14f), new Vector2(0f, 328f));
        // Face: wide friendly eyes and a big grin with a gap in it.
        Block(a, "Eye L", white, new Vector2(52f, 42f), new Vector2(-42f, 200f));
        Block(a, "Eye R", white, new Vector2(52f, 42f), new Vector2(42f, 200f));
        Block(a, "Pupil L", dark, new Vector2(18f, 26f), new Vector2(-38f, 200f));
        Block(a, "Pupil R", dark, new Vector2(18f, 26f), new Vector2(46f, 200f));
        Block(a, "Brow L", dark, new Vector2(60f, 9f), new Vector2(-42f, 232f), 8f);
        Block(a, "Brow R", dark, new Vector2(60f, 9f), new Vector2(42f, 232f), -8f);
        Block(a, "Mouth", dark, new Vector2(88f, 22f), new Vector2(0f, 142f));
        Block(a, "Tooth", white, new Vector2(16f, 14f), new Vector2(-14f, 148f));
        Block(a, "Dirt Smudge", side, new Vector2(40f, 14f), new Vector2(64f, 160f), 12f);
    }
}
