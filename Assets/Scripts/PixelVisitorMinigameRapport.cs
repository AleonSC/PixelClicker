// ---- System notes ----
// PixelVisitorMinigameRapport.cs
//   Part of PixelVisitorMinigame: the vendor's side of RAPPORT (the meter, table and points are PixelRapport; progress in GameState.rapport).
//   - Earning: opening his visit (RapportVisitAccepted), dealing - a sale, a purchase, a trade - through RapportDeal (capped per visit at
//     PixelRapport.DealPointsPerVisit), and gifts from the Sell view (GiveGift: only PixelRapport.GiftsPerVisit count a visit).
//   - Gifts: the Sell view's Gift button gives the picked good (a gift = PixelRapport.GiftClicks of effort, at least 1). His LOVES (PixelRapport's
//     vendor data) count much more; a loved good he doesn't buy is listed too, as 'gift only' (AddGiftOnlyGoods).
//   - The RAPPORT METER (BuildRapportMeter): a bar in his greeting box and in the wares / Sell window - the fill is his points across the whole
//     meter (0 .. Partner), a tick at each step, and the label '♥ Regular  ·  42 / 80 to Friend'. It refreshes whenever points come in.
//   - What his step gives here: NextVisitWait (more visits), RapportExtraWares / RapportExtraStock (bigger stock), RapportRareChance (the
//     Farmer's Dragon Seed), his step line in the greeting and the step in the greeting box title (RapportTitle). Sale prices and market
//     recovery are PixelMarket's (PriceFactor / RecoverySpeed).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract partial class PixelVisitorMinigame
{
    private double rapportDealThisVisit;
    private int giftsThisVisit;

    private string RapportName => Pick(visitorName, DefaultName);

    /// <summary>A new visit: the per-visit caps start again.</summary>
    private void RapportVisitStart() { rapportDealThisVisit = 0d; giftsThisVisit = 0; rapportMeters.Clear(); }

    /// <summary>You opened his visit (clicked him when he peeked in).</summary>
    private void RapportVisitAccepted()
    {
        PixelRapport r = PixelRapport.Instance;
        if (r != null) r.Earn(Id, RapportName, r.VisitPoints);
        RefreshRapportMeters();
    }

    /// <summary>Points for dealing (a sale, a purchase, a trade), up to the per-visit cap.</summary>
    private void RapportDeal(double points)
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null) return;
        double give = System.Math.Min(points, System.Math.Max(0d, r.DealPointsPerVisit - rapportDealThisVisit));
        rapportDealThisVisit += give;
        r.Earn(Id, RapportName, give);   // (0 still counts as dealing with him: the trickle waits)
        RefreshRapportMeters();
    }

    // ---- The rapport meter (greeting box, wares / Sell window) ----

    private sealed class RapportMeter { public RectTransform fill; public TMP_Text label; }
    private readonly List<RapportMeter> rapportMeters = new List<RapportMeter>();
    private static readonly Color RapportFill = new Color(1f, 0.42f, 0.6f, 1f);

    /// <summary>The meter's height in a dialog box (canvas units).</summary>
    private const float RapportMeterHeight = 34f;

    /// <summary>Builds a rapport meter along the bottom of 'parent', 'bottom' up from its bottom edge (nothing without PixelRapport).</summary>
    private void BuildRapportMeter(RectTransform parent, float bottom, float sideInset)
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null || parent == null) return;
        Image track = PixelUIKit.CreateBottomBar(parent, "Rapport Meter", RapportFill, sideInset, bottom, RapportMeterHeight, out RectTransform fill);
        float top = Mathf.Max(1f, r.StepPoints(r.StepCount - 1));
        for (int i = 1; i < r.StepCount - 1; i++)   // a tick where each step begins (the ends need none)
        {
            GameObject tick = new GameObject("Tick " + r.StepNameAt(i), typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(track.transform, false);
            Image img = tick.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.55f);
            img.raycastTarget = false;
            RectTransform tr = tick.GetComponent<RectTransform>();
            float x = Mathf.Clamp01(r.StepPoints(i) / top);
            tr.anchorMin = new Vector2(x, 0f); tr.anchorMax = new Vector2(x, 1f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.offsetMin = new Vector2(-1.5f, 3f); tr.offsetMax = new Vector2(1.5f, -3f);
        }
        TMP_Text label = PixelUIKit.CreateText(Font, track.transform, "Label", "", 20f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        label.enableAutoSizing = true; label.fontSizeMax = 20f; label.fontSizeMin = 12f;
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(8f, 0f); lr.offsetMax = new Vector2(-8f, 0f);
        rapportMeters.Add(new RapportMeter { fill = fill, label = label });
        RefreshRapportMeters();
    }

    /// <summary>Redraws every meter of this visit: the fill across the whole meter and '♥ Regular  ·  42 / 80 to Friend'.</summary>
    private void RefreshRapportMeters()
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null || rapportMeters.Count == 0) return;
        int step = r.StepIndex(Id);
        double points = r.Points(Id);
        int last = r.StepCount - 1;
        float fraction = Mathf.Clamp01((float)(points / Mathf.Max(1f, r.StepPoints(last))));
        if (step >= last) fraction = 1f;
        string text = "\u2665 " + r.StepNameAt(step) + "  \u00B7  " + (step >= last ? "best of friends"
            : Mathf.FloorToInt((float)points) + " / " + Mathf.CeilToInt(r.StepPoints(step + 1)) + " to " + r.StepNameAt(step + 1));
        foreach (RapportMeter m in rapportMeters)
        {
            if (m.fill == null) continue;
            m.fill.anchorMax = new Vector2(fraction, 1f);
            PixelUIKit.SetText(m.label, text);
        }
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
        RefreshRapportMeters();
        PixelStats.Count(Id + ".gifts");
        PixelAudio.Play("purchase");
        RefreshSellLists();
        PixelUIKit.SetText(sayLabel, loved ? "For me? I LOVE " + g.name + "! Thank you, friend." : "Oh, " + g.name + "! That's kind of you.");
    }
}
