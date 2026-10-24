// ---- System notes ----
// PixelMarket.cs
//   ONE price rule for every vendor that BUYS goods for pure currency (the value ladder, GameDesign section 6 'Cubie's bill'):
//   - "1x" = the pure currency one click earns on pure blocks right now (PurePerClick: the pure types' spawn shares x their pay per
//     click x Value / Ultra / wish; the six pure colours count as equal units).
//   - A good is worth the clicks it took: ClicksPerUnit(tier) = clicks to collect / (pay per click x its multipliers). So selling the
//     goods of a few minutes of skill clicking pays rawGoodsRate (1.2) x what the same clicks on pure blocks would have paid.
//     Routes (Route): RawGoods 1.2x (built), Products 2x and Orders 3x (rates ready for when those exist).
//   - SATURATION instead of caps: each vendor + good has a 'pressure' (the clicks-worth sold to them). The price factor is
//     1 / (1 + pressure / saturationClicks) (selling saturationClicks of work halves it), never below minFactor; pressure halves every
//     recoveryHalfLifeMinutes (real time, so a paused / time-stopped game still recovers). Quote averages the factor over the whole
//     amount sold (a big dump drives its own price down as it goes). RecordSale adds the pressure when a deal is accepted.
//   - Not saved: the market recovers anyway, a reload just starts it fresh.
//   Used by the Lumberjack's Sell tab and the Prospector's offers; the Entrepreneur's pixel-for-pixel deals use RarityValue.
//   Husks are never currency: a vendor buys skill CURRENCY (or a husk good he names, valued as currency); the Recycler stays the only
//   husk-to-currency route.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>The shared rule for what vendors pay for goods in pure currency: value by effort, plus market saturation.</summary>
public class PixelMarket : MonoBehaviour
{
    /// <summary>How the goods were made: more effort pays more.</summary>
    public enum Route { RawGoods, Products, Orders }

    [Header("Value ladder (x what clicking pure blocks earns)")]
    [Min(0.1f)]
    [Tooltip("Selling raw skill currency to a vendor pays this many times what the same clicks would have earned on pure blocks (1.2 = 20% better than ignoring the skill).")]
    [SerializeField] private float rawGoodsRate = 1.2f;

    [Min(0.1f)]
    [Tooltip("Selling products (goods made from skill currency) pays this many times what the clicks behind them would have earned. Not used yet: products aren't built.")]
    [SerializeField] private float productsRate = 2f;

    [Min(0.1f)]
    [Tooltip("Filling a vendor's order pays this many times what the clicks behind it would have earned. Not used yet: orders aren't built (they will be limited).")]
    [SerializeField] private float ordersRate = 3f;

    [Header("Saturation (a vendor pays less for a good you sell a lot of)")]
    [Min(1f)]
    [Tooltip("Selling this many clicks' worth of one good to one vendor halves his price for it (900 clicks = about 6 minutes of clicking at 2.5 a second).")]
    [SerializeField] private float saturationClicks = 900f;

    [Range(0.01f, 1f)]
    [Tooltip("His price for a good never drops below this fraction of the full price.")]
    [SerializeField] private float minFactor = 0.25f;

    [Min(0.1f)]
    [Tooltip("Real minutes for half of the saturation to wear off (his price recovers over time).")]
    [SerializeField] private float recoveryHalfLifeMinutes = 10f;

    [Min(0.001f)]
    [Tooltip("Saturation below this fraction of a full halving counts as 'steady' (the sell window shows no trend).")]
    [SerializeField] private float steadyThreshold = 0.03f;

    private PixelClicker clicker;
    private struct Pressure { public double clicks; public float time; }
    private readonly Dictionary<string, Pressure> pressure = new Dictionary<string, Pressure>();   // "vendor|tier" -> clicks-worth sold

    /// <summary>The scene's market (null if there is none).</summary>
    public static PixelMarket Instance => PixelFind.Cached<PixelMarket>();

    private void Awake()
    {
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private PixelClicker Clicker => clicker != null ? clicker : (clicker = PixelFind.Cached<PixelClicker>());

    public float RateOf(Route route) => route == Route.Orders ? ordersRate : route == Route.Products ? productsRate : rawGoodsRate;

    /// <summary>The six pure colours (White..Blue).</summary>
    public static bool IsPure(PixelClicker.PixelType type) => type >= PixelClicker.PixelType.White && type <= PixelClicker.PixelType.Blue;

    /// <summary>"1x": pure currency one click earns on pure blocks right now (the unlocked pure types' spawn shares x what each pays).</summary>
    public double PurePerClick()
    {
        PixelClicker c = Clicker;
        if (c == null) return 1d;
        double weight = 0d, pay = 0d;
        for (int i = 0; i < c.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = c.Tiers[i];
            if (t == null || !t.unlocked || !IsPure(t.type)) continue;
            double w = Mathf.Max(0f, t.spawnWeight);
            weight += w;
            pay += w * t.amountPerClick * c.PayoutMultiplier(i) / System.Math.Max(1, t.clicksToCollect);
        }
        return weight > 0d ? System.Math.Max(0.0001d, pay / weight) : 1d;
    }

    /// <summary>Clicks it takes to earn one unit of this currency (tough blocks take several clicks; multipliers make it fewer). 0 = it pays nothing.</summary>
    public double ClicksPerUnit(int tier)
    {
        PixelClicker c = Clicker;
        if (c == null || tier < 0 || tier >= c.Tiers.Length) return 0d;
        PixelClicker.PixelTier t = c.Tiers[tier];
        double perHarvest = t.amountPerClick * c.PayoutMultiplier(tier);
        return perHarvest > 0d ? System.Math.Max(1, t.clicksToCollect) / perHarvest : 0d;
    }

    /// <summary>Full price of one unit of a good, in pure units (paid 1:1 in any pure colour), before saturation.</summary>
    public double FullPricePerUnit(int tier, Route route) => RateOf(route) * PurePerClick() * ClicksPerUnit(tier);

    private static string Key(string vendor, int tier) => (vendor ?? "") + "|" + tier;

    /// <summary>The vendor's saturation for a good right now, in clicks-worth (decayed to now).</summary>
    private double PressureNow(string vendor, int tier)
    {
        if (!pressure.TryGetValue(Key(vendor, tier), out Pressure p)) return 0d;
        float minutes = (Time.unscaledTime - p.time) / 60f;
        return p.clicks * System.Math.Pow(0.5, minutes / System.Math.Max(0.1f, recoveryHalfLifeMinutes));
    }

    /// <summary>Price factor for the NEXT unit (1 = full price).</summary>
    public double Factor(string vendor, int tier) => System.Math.Max(minFactor, 1d / (1d + PressureNow(vendor, tier) / saturationClicks));

    /// <summary>
    /// What a vendor pays for 'amount' of a good, in pure units: the full price x the saturation factor averaged over the amount
    /// (each unit sold lowers the price of the next). Before any vendor's own luck / mood.
    /// </summary>
    public double Quote(string vendor, int tier, double amount, Route route = Route.RawGoods)
    {
        if (amount <= 0d) return 0d;
        double perUnitClicks = ClicksPerUnit(tier);
        double work = amount * perUnitClicks;   // clicks-worth sold
        double k = saturationClicks, s0 = PressureNow(vendor, tier);
        double avg = work > 0d ? k * System.Math.Log((k + s0 + work) / (k + s0)) / work : 1d;   // the average of 1 / (1 + s / k) from s0 to s0 + work
        avg = System.Math.Max(minFactor, avg);
        return amount * FullPricePerUnit(tier, route) * avg;
    }

    /// <summary>Call when a deal is accepted: the vendor's price for that good drops by the work sold.</summary>
    public void RecordSale(string vendor, int tier, double amount)
    {
        double s = PressureNow(vendor, tier) + System.Math.Max(0d, amount) * ClicksPerUnit(tier);
        pressure[Key(vendor, tier)] = new Pressure { clicks = s, time = Time.unscaledTime };
    }

    /// <summary>One line for a sell window: "Market 74% (recovering)" or "Market 100% (steady)", coloured.</summary>
    public string TrendText(string vendor, int tier)
    {
        double s = PressureNow(vendor, tier);
        double f = Factor(vendor, tier);
        bool steady = s < steadyThreshold * saturationClicks;
        Color col = steady ? new Color(0.55f, 1f, 0.6f) : f < 0.6 ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.85f, 0.4f);
        string trend = steady ? "steady" : "▲ recovering";
        return "Market <color=#" + ColorUtility.ToHtmlStringRGB(col) + ">" + Mathf.RoundToInt((float)f * 100f) + "%</color> (" + trend + ")";
    }

    /// <summary>Roughly how many of this pixel one unit is worth by rarity (1 / how fast it is earned): the Entrepreneur's pixel-for-pixel rate.</summary>
    public static double RarityValue(PixelClicker.PixelTier t)
    {
        double earnRate = Mathf.Max(0.0001f, t.spawnWeight) * System.Math.Max(0.0001, t.amountPerClick) / System.Math.Max(1, t.clicksToCollect);
        return 1d / earnRate;
    }
}
