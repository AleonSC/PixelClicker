// ---- System notes ----
// PixelVisitorMinigameGoods.cs
//   Part of PixelVisitorMinigame: the GOODS BUYERS and CALLING a vendor.
//   - A goods buyer (BuysGoods; the Prospector, Forager, Angler and the Farmer's Sell tab) buys one skill's goods (GoodsSkillId: the types of
//     that skill pack that pay something) for pure currency. RollGoodsTrades makes its offers from its GoodsOffers settings (how many offers,
//     what share of your stock each asks for, luck), priced by the shared rule PixelMarket.Quote (effort x 1.2, less as his market for that
//     good fills up) and paid 1:1 in a random unlocked pure colour. Accepting a row records the sale (RecordGoodsSale) and every row shows
//     his market price and trend (GoodsNote). The Lumberjack sells through his own Sell panel but uses the same market.
//   - CALLING (the Toggles window's Vendors rows, 'Call'): a vendor that buys from you (BuysFromPlayer) can be called once per shared
//     cooldown (PixelGuideVendor.vendorCallCooldownMinutes, 30 by default, in PLAY time - saved through the stat 'vendor.callReady').
//     A called vendor comes at the next calm moment, even when his skill is not in training and without waiting for the gap after
//     another vendor; the normal random visits carry on as before.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class PixelVisitorMinigame
{
    /// <summary>How a goods buyer makes his offers (each vendor has its own block in the Inspector).</summary>
    [Serializable]
    public class GoodsOffers
    {
        [Min(1)]
        [Tooltip("How many offers he brings (a different good in each).")]
        public int offerCount = 3;

        [Range(0.01f, 1f)]
        [Tooltip("Fewest fraction of what you hold of one good that an offer asks for.")]
        public float minSellFraction = 0.3f;

        [Range(0.01f, 1f)]
        [Tooltip("Most fraction of what you hold of one good that an offer asks for.")]
        public float maxSellFraction = 1f;

        [Min(0.05f)]
        [Tooltip("Worst luck factor on a deal (1 = the market price).")]
        public float minLuck = 0.85f;

        [Min(0.05f)]
        [Tooltip("Best luck factor on a deal.")]
        public float maxLuck = 1.25f;

        [Min(1f)]
        [Tooltip("You must hold at least this many of a good for him to ask for it.")]
        public double minHeld = 5;
    }

    /// <summary>True for a vendor whose trade rows buy one skill's goods (see <see cref="GoodsSkillId"/>).</summary>
    protected virtual bool BuysGoods => false;

    /// <summary>The skill whose goods a goods buyer takes (its skill pack's pixel types).</summary>
    protected virtual string GoodsSkillId => null;

    /// <summary>A goods buyer's offer settings (its own serialized block).</summary>
    protected virtual GoodsOffers Offers => null;

    /// <summary>Does this vendor buy anything from the player (so it can be called from the Toggles window)?</summary>
    public virtual bool BuysFromPlayer => BuysGoods;

    /// <summary>Is this pixel type one of the goods he buys? (One of his skill's types that pays something.)</summary>
    protected bool IsGood(int tier)
    {
        PixelSkills skills = PixelSkills.Instance;
        PixelSkills.SkillPack pack = skills != null && GoodsSkillId != null ? skills.Find(GoodsSkillId) : null;
        PixelMarket market = PixelMarket.Instance;
        if (pack == null || pack.types == null || market == null) return false;
        return Array.IndexOf(pack.types, clicker.Tiers[tier].type) >= 0 && market.ClicksPerUnit(tier) > 0d;
    }

    /// <summary>A goods buyer's offers: up to offerCount goods you hold, each a share of your stock, paid in a random unlocked pure colour at the market price x luck.</summary>
    protected List<Trade> RollGoodsTrades()
    {
        List<Trade> result = new List<Trade>();
        GoodsOffers o = Offers ?? new GoodsOffers();
        PixelMarket market = PixelMarket.Instance;
        if (clicker == null || market == null) return result;
        PixelClicker.PixelTier[] tiers = clicker.Tiers;

        List<int> sellable = new List<int>(), pure = new List<int>();
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].count >= o.minHeld && IsGood(i)) sellable.Add(i);
            if (PixelMarket.IsPure(tiers[i].type) && tiers[i].unlocked) pure.Add(i);
        }
        if (sellable.Count == 0 || pure.Count == 0) return result;

        for (int n = 0; n < o.offerCount && sellable.Count > 0; n++)
        {
            int si = UnityEngine.Random.Range(0, sellable.Count);
            int good = sellable[si];
            sellable.RemoveAt(si);   // a different good in every offer
            int payTier = pure[UnityEngine.Random.Range(0, pure.Count)];
            float lo = Mathf.Min(o.minSellFraction, o.maxSellFraction), hi = Mathf.Max(o.minSellFraction, o.maxSellFraction);
            double sell = System.Math.Max(1d, System.Math.Floor(tiers[good].count * UnityEngine.Random.Range(lo, hi)));
            double luck = UnityEngine.Random.Range(Mathf.Min(o.minLuck, o.maxLuck), Mathf.Max(o.minLuck, o.maxLuck));
            double get = System.Math.Max(1d, System.Math.Floor(market.Quote(Id, good, sell) * luck));
            // payTier / getTier: you hand over the good ('pay') and receive the pure pixels ('get').
            result.Add(new Trade { payTier = good, getTier = payTier, pay = sell, get = get });
        }
        return result;
    }

    private void RecordGoodsSale(Trade t)
    {
        PixelMarket market = PixelMarket.Instance;
        if (BuysGoods && market != null && t != null) market.RecordSale(Id, t.payTier, t.pay);
    }

    private string GoodsNote(Trade t)
    {
        PixelMarket market = PixelMarket.Instance;
        return BuysGoods && market != null && t != null ? market.TrendText(Id, t.payTier) : "";
    }

    // ------------------------------------------------------------------
    // Calling a vendor (Toggles window)
    // ------------------------------------------------------------------

    private const string CallReadyKey = "vendor.callReady";   // play seconds when the next call is allowed (only ever rises, so PixelStats.Best keeps it)
    private bool called;

    private static double PlaySeconds { get { PixelStats s = PixelFind.Cached<PixelStats>(); return s != null ? s.PlaySeconds : Time.time; } }

    /// <summary>Seconds of play until any vendor can be called again (0 = now).</summary>
    public static double CallReadyIn => System.Math.Max(0d, PixelStats.Total(CallReadyKey) - PlaySeconds);

    /// <summary>Can this vendor be called right now? (He buys from you, he has started visiting, he is not here or on his way, and the shared cooldown is over.)</summary>
    public bool CanCall => BuysFromPlayer && Running && !visiting && !called && CallReadyIn <= 0d;

    /// <summary>Is he on his way after a call?</summary>
    public bool OnHisWay => called;

    /// <summary>Calls him: he comes at the next calm moment, whatever skill is training. Starts the shared cooldown.</summary>
    public bool Call()
    {
        if (!CanCall) return false;
        PixelGuideVendor guide = PixelFind.Cached<PixelGuideVendor>();
        float minutes = guide != null ? guide.VendorCallCooldownMinutes : 30f;
        PixelStats.Best(CallReadyKey, PlaySeconds + minutes * 60d);
        called = true;
        spawnTimer = 0f;
        PixelAudio.Play("ui_click");
        PixelHints.Announce(Pick(visitorName, DefaultName) + " is on the way.");
        return true;
    }
}
