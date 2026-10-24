// ---- System notes ----
// PixelVisitorMinigameGoods.cs
//   Part of PixelVisitorMinigame: the GOODS BUYERS and CALLING a vendor.
//   - A goods buyer (BuysGoods; Big Tim, the Prospector, Forager, Angler and the Farmer) buys one skill's goods (GoodsSkillId: the types of that
//     skill pack that pay something, IsGood). A vendor with specialty goods in PixelMarket's table (the Wizard, the Tinkerer) buys too.
//     Both sell through the shared Sell view (PixelVisitorMinigameSell.cs).
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
    /// <summary>True for a vendor who buys one skill's goods (see <see cref="GoodsSkillId"/>) in his Sell view.</summary>
    protected virtual bool BuysGoods => false;

    /// <summary>The skill whose goods a goods buyer takes (its skill pack's pixel types).</summary>
    protected virtual string GoodsSkillId => null;

    /// <summary>Does this vendor buy anything from the player (his skill's goods or specialty materials)? He gets the Sell view and can be called from the Toggles window.</summary>
    public virtual bool BuysFromPlayer
    {
        get
        {
            if (BuysGoods) return true;
            PixelMarket market = PixelMarket.Instance;
            return market != null && market.SpecialtiesOf(Id).Count > 0;
        }
    }

    /// <summary>Is this pixel type one of the goods he buys? (One of his skill's types that pays something.)</summary>
    protected bool IsGood(int tier)
    {
        PixelSkills skills = PixelSkills.Instance;
        PixelSkills.SkillPack pack = skills != null && GoodsSkillId != null ? skills.Find(GoodsSkillId) : null;
        PixelMarket market = PixelMarket.Instance;
        if (pack == null || pack.types == null || market == null) return false;
        return Array.IndexOf(pack.types, clicker.Tiers[tier].type) >= 0 && market.ClicksPerUnit(tier) > 0d;
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
