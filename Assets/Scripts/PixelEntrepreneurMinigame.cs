using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Pixel Entrepreneur (id "entrepreneur", Minigames tab, cheap): a visitor who stops time and offers a few pixel-for-pixel
/// deals. Each offer asks for a random amount of a pixel type you hold and gives a random amount of another unlocked type. The
/// exchange rate follows how rare each pixel is, multiplied by a random luck factor, so some deals are great and some are bad.
/// All the behaviour (sliding in, greeting, accept / reject, the offers window) is in <see cref="PixelVisitorMinigame"/>.
/// </summary>
public class PixelEntrepreneurMinigame : PixelVisitorMinigame
{
    [Header("Deals")]
    [Min(1)]
    [Tooltip("How many offers he brings.")]
    [SerializeField] private int offerCount = 3;

    [Range(0.01f, 1f)]
    [Tooltip("Fewest fraction of the pixels you hold of one type that an offer asks for.")]
    [SerializeField] private float minPayFraction = 0.1f;

    [Range(0.01f, 1f)]
    [Tooltip("Most fraction of the pixels you hold of one type that an offer asks for.")]
    [SerializeField] private float maxPayFraction = 0.5f;

    [Min(0.05f)]
    [Tooltip("Worst luck factor on the exchange rate (1 = a fair swap by rarity, below 1 = he cheats you).")]
    [SerializeField] private float minLuck = 0.6f;

    [Min(0.05f)]
    [Tooltip("Best luck factor on the exchange rate (above 1 = a bargain for you).")]
    [SerializeField] private float maxLuck = 1.8f;

    [Min(1f)]
    [Tooltip("You must hold at least this many of a pixel type for him to ask for it.")]
    [SerializeField] private double minHeld = 10;

    public override string Id => "entrepreneur";
    public override string DisplayName => "Pixel Entrepreneur";

    protected override bool IsWare(int item) => false;
    protected override string DefaultName => "Pixel Entrepreneur";
    protected override string DefaultGreeting => "Pleasure! I'm the Pixel Entrepreneur. I buy and sell pixels, and today I have a few deals with your name on them. Some are bargains, some are not. Care to look?";
    protected override string DefaultWaresTitle => "Today's Deals";
    protected override string SaleFormat => "Take it or leave it!";
    protected override string DefaultArrivedLog => "The Pixel Entrepreneur is here!";
    protected override float StartDelayOffset => 60f;
    protected override Color TitleColor => new Color(0.5f, 0.95f, 0.6f);
    protected override string TradeText => "Trade";

    /// <summary>Roughly how many of this pixel one unit is worth: the rarer (earned slower), the more valuable.</summary>
    private static double UnitValue(PixelClicker.PixelTier t)
    {
        double earnRate = Mathf.Max(0.0001f, t.spawnWeight) * System.Math.Max(0.0001, t.amountPerClick) / System.Math.Max(1, t.clicksToCollect);
        return 1d / earnRate;
    }

    protected override List<Trade> RollTrades()
    {
        List<Trade> result = new List<Trade>();
        if (clicker == null) return result;
        PixelClicker.PixelTier[] tiers = clicker.Tiers;

        List<int> payable = new List<int>();   // pixel types you hold enough of
        List<int> receivable = new List<int>(); // unlocked pixel types he can give
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].rareDrop) continue;
            if (tiers[i].count >= minHeld) payable.Add(i);
            if (tiers[i].unlocked) receivable.Add(i);
        }
        if (payable.Count == 0 || receivable.Count < 2) return result;

        for (int n = 0; n < offerCount && payable.Count > 0; n++)
        {
            int pi = Random.Range(0, payable.Count);
            int payTier = payable[pi];
            payable.RemoveAt(pi); // a different pixel type for every offer

            List<int> gets = receivable.FindAll(i => i != payTier);
            if (gets.Count == 0) continue;
            int getTier = gets[Random.Range(0, gets.Count)];

            double pay = System.Math.Max(1d, System.Math.Floor(tiers[payTier].count * Random.Range(minPayFraction, maxPayFraction)));
            double luck = Random.Range(minLuck, maxLuck);
            double get = System.Math.Max(1d, System.Math.Floor(pay * UnitValue(tiers[payTier]) / UnitValue(tiers[getTier]) * luck));
            result.Add(new Trade { payTier = payTier, getTier = getTier, pay = pay, get = get });
        }
        return result;
    }

    protected override void DrawVisitor(Transform art)
    {
        Color suit = new Color(0.16f, 0.2f, 0.3f), suitDark = new Color(0.1f, 0.13f, 0.2f), skin = new Color(0.95f, 0.78f, 0.6f);
        Color shirt = new Color(0.95f, 0.95f, 0.98f), tie = new Color(0.85f, 0.2f, 0.25f), gold = new Color(1f, 0.82f, 0.25f);
        Color hair = new Color(0.2f, 0.14f, 0.1f), leather = new Color(0.45f, 0.28f, 0.14f), green = new Color(0.4f, 0.95f, 0.5f);
        Transform a = art;
        Block(a, "Leg L", suitDark, new Vector2(48f, 90f), new Vector2(-34f, 52f));
        Block(a, "Leg R", suitDark, new Vector2(48f, 90f), new Vector2(34f, 52f));
        Block(a, "Shoe L", new Color(0.05f, 0.05f, 0.07f), new Vector2(64f, 24f), new Vector2(-38f, 12f));
        Block(a, "Shoe R", new Color(0.05f, 0.05f, 0.07f), new Vector2(64f, 24f), new Vector2(38f, 12f));
        Block(a, "Arm L", suit, new Vector2(38f, 130f), new Vector2(-98f, 170f), 6f);
        Block(a, "Body", suit, new Vector2(150f, 180f), new Vector2(0f, 180f));
        Block(a, "Shirt", shirt, new Vector2(46f, 150f), new Vector2(0f, 190f));
        Block(a, "Tie", tie, new Vector2(18f, 100f), new Vector2(0f, 180f));
        Block(a, "Tie Knot", tie, new Vector2(24f, 22f), new Vector2(0f, 236f));
        Block(a, "Pocket Square", gold, new Vector2(22f, 14f), new Vector2(-48f, 222f), 10f);
        Block(a, "Hand L", skin, new Vector2(34f, 34f), new Vector2(-104f, 100f));
        Block(a, "Briefcase", leather, new Vector2(110f, 76f), new Vector2(-110f, 62f));
        Block(a, "Briefcase Strap", suitDark, new Vector2(110f, 10f), new Vector2(-110f, 74f));
        Block(a, "Briefcase Lock", gold, new Vector2(16f, 16f), new Vector2(-110f, 74f));
        Block(a, "Arm R", suit, new Vector2(38f, 120f), new Vector2(100f, 195f), -30f);
        Block(a, "Hand R", skin, new Vector2(34f, 34f), new Vector2(130f, 250f));
        Block(a, "Coin Cube", green, new Vector2(44f, 44f), new Vector2(150f, 300f), 25f);
        Block(a, "Coin Shine", shirt, new Vector2(12f, 12f), new Vector2(160f, 312f), 25f);
        Block(a, "Head", skin, new Vector2(104f, 98f), new Vector2(0f, 296f));
        Block(a, "Hair", hair, new Vector2(112f, 30f), new Vector2(0f, 340f));
        Block(a, "Eye L", suitDark, new Vector2(12f, 14f), new Vector2(-22f, 304f));
        Block(a, "Eye R", suitDark, new Vector2(12f, 14f), new Vector2(22f, 304f));
        Block(a, "Smile", suitDark, new Vector2(44f, 8f), new Vector2(0f, 270f));
        Block(a, "Smile Edge L", suitDark, new Vector2(8f, 12f), new Vector2(-24f, 276f));
        Block(a, "Smile Edge R", suitDark, new Vector2(8f, 12f), new Vector2(24f, 276f));
    }
}
