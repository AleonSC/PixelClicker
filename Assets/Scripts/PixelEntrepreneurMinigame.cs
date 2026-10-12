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
            if (tiers[i].rareDrop || PixelClicker.IsStandalone(tiers[i].type)) continue;
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
        // A navy cube in a suit: slicked hair, a smug smile, a red tie down the front, a briefcase and a green coin cube held up.
        Color face = new Color(0.2f, 0.28f, 0.45f), top = new Color(0.36f, 0.46f, 0.66f), side = new Color(0.12f, 0.17f, 0.3f);
        Color dark = new Color(0.06f, 0.07f, 0.12f), white = new Color(0.97f, 0.97f, 1f), tie = new Color(0.85f, 0.2f, 0.25f), gold = new Color(1f, 0.82f, 0.25f);
        Color leather = new Color(0.45f, 0.28f, 0.14f), green = new Color(0.4f, 0.95f, 0.5f);
        Transform a = art;
        CubeBody(a, face, top, side, dark, arms: false);
        // Briefcase in the left hand.
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        Block(a, "Briefcase", leather, new Vector2(80f, 60f), new Vector2(-132f, 50f));
        Block(a, "Briefcase Strap", dark, new Vector2(80f, 8f), new Vector2(-132f, 58f));
        Block(a, "Briefcase Lock", gold, new Vector2(14f, 14f), new Vector2(-132f, 58f));
        // The coin cube raised in the right hand.
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 170f), -26f);
        Block(a, "Coin Cube", green, new Vector2(48f, 48f), new Vector2(150f, 260f), 25f);
        Block(a, "Coin Shine", white, new Vector2(12f, 12f), new Vector2(160f, 274f), 25f);
        // Face: confident eyes, a wide smile, a collar and tie.
        CubeEyes(a, white, dark, 208f, 42f, 8f, 0.9f);
        Block(a, "Brow L", dark, new Vector2(58f, 8f), new Vector2(-42f, 238f), -6f);
        Block(a, "Brow R", dark, new Vector2(58f, 8f), new Vector2(42f, 238f), 8f);
        Block(a, "Smile", dark, new Vector2(80f, 8f), new Vector2(0f, 156f));
        Block(a, "Smile Edge L", dark, new Vector2(8f, 14f), new Vector2(-40f, 162f));
        Block(a, "Smile Edge R", dark, new Vector2(8f, 14f), new Vector2(40f, 162f));
        Block(a, "Collar L", white, new Vector2(50f, 30f), new Vector2(-24f, 112f), 30f);
        Block(a, "Collar R", white, new Vector2(50f, 30f), new Vector2(24f, 112f), -30f);
        Block(a, "Tie Knot", tie, new Vector2(24f, 22f), new Vector2(0f, 118f));
        Block(a, "Tie", tie, new Vector2(20f, 70f), new Vector2(0f, 84f));
        // Slicked-back hair and a gold pin on the lapel.
        Block(a, "Hair", dark, new Vector2(204f, 26f), new Vector2(0f, 300f));
        Block(a, "Hair Quiff", dark, new Vector2(110f, 22f), new Vector2(-24f, 322f), 4f);
        Block(a, "Pin", gold, new Vector2(12f, 12f), new Vector2(-70f, 120f), 45f);
    }
}
