// ---- System notes ----
// PixelVisitorMinigameRapport.cs
//   Part of PixelVisitorMinigame: the vendor's side of RAPPORT (the meter, table and points are PixelRapport; progress in GameState.rapport).
//   - Earning: opening his visit (RapportVisitAccepted), dealing - a sale, a purchase, a trade - through RapportDeal (capped per visit at
//     PixelRapport.DealPointsPerVisit), and gifts from the Sell view (GiveGift: only PixelRapport.GiftsPerVisit count a visit).
//   - Gifts: the Sell view's Gift button gives the picked good (a gift = PixelRapport.GiftClicks of effort, at least 1). His LOVES (PixelRapport's
//     vendor data) count much more; a loved good he doesn't buy is listed too, as 'gift only' (AddGiftOnlyGoods).
//   - What his step gives here: NextVisitWait (more visits), RapportExtraWares / RapportExtraStock (bigger stock), RapportRareChance (the
//     Farmer's Dragon Seed), his step line in the greeting and the step in the greeting box title (RapportTitle). Sale prices and market
//     recovery are PixelMarket's (PriceFactor / RecoverySpeed).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class PixelVisitorMinigame
{
    private double rapportDealThisVisit;
    private int giftsThisVisit;

    private string RapportName => Pick(visitorName, DefaultName);

    /// <summary>A new visit: the per-visit caps start again.</summary>
    private void RapportVisitStart() { rapportDealThisVisit = 0d; giftsThisVisit = 0; }

    /// <summary>You opened his visit (clicked him when he peeked in).</summary>
    private void RapportVisitAccepted()
    {
        PixelRapport r = PixelRapport.Instance;
        if (r != null) r.Earn(Id, RapportName, r.VisitPoints);
    }

    /// <summary>Points for dealing (a sale, a purchase, a trade), up to the per-visit cap.</summary>
    private void RapportDeal(double points)
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null) return;
        double give = System.Math.Min(points, System.Math.Max(0d, r.DealPointsPerVisit - rapportDealThisVisit));
        rapportDealThisVisit += give;
        r.Earn(Id, RapportName, give);   // (0 still counts as dealing with him: the trickle waits)
    }

    /// <summary>Seconds until his next visit: the normal random wait x his step's visit factor.</summary>
    private float NextVisitWait()
    {
        PixelRapport r = PixelRapport.Instance;
        return UnityEngine.Random.Range(visitMinSeconds, visitMaxSeconds) * (r != null ? r.VisitWait(Id) : 1f);
    }

    private int RapportExtraWares { get { PixelRapport r = PixelRapport.Instance; return r != null ? r.ExtraWares(Id) : 0; } }
    private int RapportExtraStock { get { PixelRapport r = PixelRapport.Instance; return r != null ? r.ExtraStock(Id) : 0; } }

    /// <summary>x his chance to bring a rare ware (the Farmer's Dragon Seed).</summary>
    protected float RapportRareChance { get { PixelRapport r = PixelRapport.Instance; return r != null ? r.RareChance(Id) : 1f; } }

    /// <summary>The greeting box title: his name and his step ("Big Tim  ·  Regular").</summary>
    private string RapportTitle
    {
        get
        {
            PixelRapport r = PixelRapport.Instance;
            return r != null ? RapportName + "  ·  " + r.StepName(Id) : RapportName;
        }
    }

    /// <summary>His line for the step he is at ("" = none).</summary>
    private string RapportLine { get { PixelRapport r = PixelRapport.Instance; return r != null ? r.StepLine(Id) : ""; } }

    // ---- Gifts (the Sell view's Gift button) ----

    private int GiftsLeft { get { PixelRapport r = PixelRapport.Instance; return r != null ? Mathf.Max(0, r.GiftsPerVisit - giftsThisVisit) : 0; } }

    private bool LovesGood(SellGood g)
    {
        PixelRapport r = PixelRapport.Instance;
        return r != null && g != null && !string.IsNullOrEmpty(g.goodId) && r.Loves(Id, g.goodId);
    }

    /// <summary>How many of a good make one gift (PixelRapport.GiftClicks of effort, at least 1).</summary>
    private double GiftAmount(SellGood g)
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null || g == null) return 0d;
        return g.clicksPerUnit > 0d ? System.Math.Max(1d, System.Math.Ceiling(r.GiftClicks / g.clicksPerUnit)) : 1d;
    }

    /// <summary>Adds the goods he loves that he doesn't buy, as 'gift only' (a pixel type by name, else a material / minigame id).</summary>
    private void AddGiftOnlyGoods(List<SellGood> list)
    {
        PixelRapport r = PixelRapport.Instance;
        PixelMarket market = PixelMarket.Instance;
        if (r == null || clicker == null) return;
        foreach (PixelRapport.Love love in r.LovesOf(Id))
        {
            string id = love.goodId;
            if (list.Exists(g => g.goodId == id)) continue;
            if (Enum.TryParse(id, out PixelClicker.PixelType type))
            {
                int tier = clicker.IndexOf(type);
                if (tier < 0 || !clicker.Tiers[tier].unlocked) continue;
                list.Add(new SellGood
                {
                    goodId = id, giftOnly = true, marketKey = PixelMarket.TierKey(tier), name = clicker.Tiers[tier].displayName, color = clicker.Tiers[tier].UIColor,
                    clicksPerUnit = market != null ? market.ClicksPerUnit(tier) : love.effortClicksPerUnit,
                    held = () => System.Math.Floor(clicker.Tiers[tier].count), take = n => clicker.TrySpend(tier, n),
                });
            }
            else
                list.Add(new SellGood
                {
                    goodId = id, giftOnly = true, marketKey = PixelMarket.MaterialKey(id), name = PixelMaterials.CostName(id), color = PixelMaterials.CostColor(id),
                    clicksPerUnit = love.effortClicksPerUnit,
                    held = () => System.Math.Floor(PixelMaterials.CostHeld(id)),
                    take = n => { if (!PixelMaterials.CostAffordable(id, n)) return false; PixelMaterials.CostSpend(id, n); return true; },
                });
        }
    }

    /// <summary>Gives him the picked good as a gift (for rapport only: he pays nothing).</summary>
    private void GiveGift()
    {
        PixelRapport r = PixelRapport.Instance;
        SellGood g = SelectedGood;
        if (r == null || g == null || GiftsLeft < 1) return;
        double amount = GiftAmount(g);
        if (g.held() < amount || !g.take(amount)) { PixelUIKit.SetText(sayLabel, "Looks like you don't have that much any more."); RefreshSellLists(); return; }
        bool loved = LovesGood(g);
        giftsThisVisit++;
        r.Earn(Id, RapportName, r.GiftPoints(loved));
        PixelStats.Count(Id + ".gifts");
        PixelAudio.Play("purchase");
        RefreshSellLists();
        PixelUIKit.SetText(sayLabel, loved ? "For me? I LOVE " + g.name + "! Thank you, friend." : "Oh, " + g.name + "! That's kind of you.");
    }
}
