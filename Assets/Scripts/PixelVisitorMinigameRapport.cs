// ---- System notes ----
// PixelVisitorMinigameRapport.cs
//   Part of PixelVisitorMinigame: the vendor's side of RAPPORT (the meter, table and points are PixelRapport; progress in GameState.rapport).
//   - Earning: opening his visit (RapportVisitAccepted), dealing - a sale, a purchase, a trade - through RapportDeal (capped per visit at
//     PixelRapport.DealPointsPerVisit), and gifts from the Sell view (GiveGift: only PixelRapport.GiftsPerVisit count a visit).
//   - Gifts: the Sell view's Gift button gives the picked good (a gift = PixelRapport.GiftClicks of effort, at least 1). His LOVES (PixelRapport's
//     vendor data) count much more; a loved good he doesn't buy is listed too, as 'gift only' (AddGiftOnlyGoods).
//   - The RAPPORT BADGE (AddRapportBadge, UI map N21.4): a small heart button in the header of his greeting box and his wares / Sell window
//     (the title is just his name). Hovering it (or clicking it) shows the RAPPORT CARD under the header as a tooltip - his step in its
//     colour ('♥ Regular') and the progress ('42 / 80 to Friend') on one line, a bar of rounded segments (one per step after Stranger, each
//     filling in its step's colour, PixelRapport Step.color) and the step names under them. It refreshes whenever points come in.
//   - What his step gives here: NextVisitWait (more visits), RapportExtraWares / RapportExtraStock (bigger stock), RapportRareChance (the
//     Farmer's Dragon Seed), his step line in the greeting and the heart badge in the box headers (coloured by his step). Sale prices and market
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
    private void RapportVisitStart()
    {
        rapportDealThisVisit = 0d; giftsThisVisit = 0; rapportMeters.Clear(); rapportHearts.Clear();
        PixelRapport r = PixelRapport.Instance;
        if (r != null) r.GrantReward(Id, RapportName);   // (his relic, if your step already earned it)
    }

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

    /// <summary>One meter's parts: the step name, the progress text, and per segment its fill and the step name under it.</summary>
    private sealed class RapportMeter
    {
        public TMP_Text step, progress;
        public RectTransform[] fills;
        public Image[] fillImages;
        public TMP_Text[] names;
    }
    private readonly List<RapportMeter> rapportMeters = new List<RapportMeter>();
    private readonly List<TMP_Text> rapportHearts = new List<TMP_Text>();   // the header badges' hearts (coloured by his step)

    /// <summary>The meter card's height in a dialog box (canvas units).</summary>
    private const float RapportMeterHeight = 84f;

    /// <summary>The rapport card's width (canvas units).</summary>
    private const float RapportCardWidth = 560f;

    /// <summary>
    /// Adds the rapport BADGE to a dialog box's header: a small heart button (coloured by his step) in the top-right slot. Hovering it - or
    /// clicking it - shows the rapport card just under the header, over the box (nothing without PixelRapport).
    /// </summary>
    private void AddRapportBadge(PixelWindowParts box)
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null || box == null) return;
        Button heart = box.AddHeaderButton("Rapport", "\u2665", false, new Color(0.14f, 0.13f, 0.2f, 1f));
        TMP_Text mark = heart.GetComponentInChildren<TMP_Text>(true);
        if (mark != null) rapportHearts.Add(mark);

        RectTransform card = BuildRapportCard(box.Rect);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 1f);
        card.sizeDelta = new Vector2(RapportCardWidth, RapportMeterHeight);
        card.anchoredPosition = new Vector2(-12f, -(box.HeaderHeight + 4f));
        card.gameObject.SetActive(false);
        void Show(bool on)
        {
            if (card == null) return;
            if (on) { RefreshRapportMeters(); card.SetAsLastSibling(); }   // (drawn over the box's text)
            card.gameObject.SetActive(on);
        }
        PixelHoverTip tip = heart.gameObject.AddComponent<PixelHoverTip>();
        tip.onEnter = () => Show(true);
        tip.onExit = () => Show(false);
        heart.onClick.AddListener(() => Show(card != null && !card.gameObject.activeSelf));   // (a click toggles it too)
        RefreshRapportMeters();
    }

    /// <summary>
    /// Builds the rapport CARD (the badge's tooltip; place it yourself): his step on the left in its colour ('♥ Regular') and the progress on
    /// the right ('42 / 80 to Friend'), a bar of rounded segments (one per step after Stranger, each filling as you get there) and the step names under them.
    /// </summary>
    private RectTransform BuildRapportCard(RectTransform parent)
    {
        PixelRapport r = PixelRapport.Instance;
        TMP_FontAsset font = Font;

        RectTransform card = MeterBox(parent, "Rapport Card", new Color(0.06f, 0.06f, 0.1f, 0.98f), false);
        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -4f);
        GameObject innerGo = new GameObject("Inner", typeof(RectTransform));
        innerGo.transform.SetParent(card, false);
        RectTransform inner = innerGo.GetComponent<RectTransform>();
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(14f, 6f); inner.offsetMax = new Vector2(-14f, -6f);

        RapportMeter m = new RapportMeter();
        m.step = MeterText(font, inner, "Step", 22f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SellPlace(m.step.rectTransform, 0f, 26f, 0f, 0.5f);
        m.progress = MeterText(font, inner, "Progress", 18f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        SellPlace(m.progress.rectTransform, 0f, 26f, 0.5f, 1f);

        int segments = Mathf.Max(1, r.StepCount - 1);
        m.fills = new RectTransform[segments];
        m.fillImages = new Image[segments];
        m.names = new TMP_Text[segments];
        for (int i = 0; i < segments; i++)
        {
            float x0 = i / (float)segments, x1 = (i + 1) / (float)segments;
            RectTransform track = MeterBox(inner, "Segment " + (i + 1), new Color(0.02f, 0.02f, 0.035f, 1f), true);
            SellPlace(track, 30f, 16f, x0, x1);
            RectTransform fill = MeterBox(track, "Fill", Color.white, false);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);
            m.fills[i] = fill;
            m.fillImages[i] = fill.GetComponent<Image>();
            m.names[i] = MeterText(font, inner, "Name " + (i + 1), 15f, FontStyles.Normal, TextAlignmentOptions.Center);
            SellPlace(m.names[i].rectTransform, 48f, 22f, x0, x1);
            PixelUIKit.SetText(m.names[i], r.StepNameAt(i + 1));
        }
        rapportMeters.Add(m);
        return card;
    }

    /// <summary>A rounded box (sunken or raised, PixelUIKit.StyleBox) that takes no clicks.</summary>
    private static RectTransform MeterBox(Transform parent, string name, Color color, bool inset)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        PixelUIKit.StyleBox(img, inset);
        return go.GetComponent<RectTransform>();
    }

    private static TMP_Text MeterText(TMP_FontAsset font, Transform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
    {
        TMP_Text t = PixelUIKit.CreateText(font, parent, name, "", size, align, style, Color.white);
        t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = Mathf.Min(12f, size);
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Redraws every meter of this visit: each segment fills in its step's colour, the reached steps' names light up.</summary>
    private void RefreshRapportMeters()
    {
        PixelRapport r = PixelRapport.Instance;
        if (r == null) return;
        int step = r.StepIndex(Id);
        foreach (TMP_Text h in rapportHearts) if (h != null) h.color = r.StepColorAt(step);   // (the header badge shows his step's colour)
        int last = r.StepCount - 1;
        double points = r.Points(Id);
        Color now = r.StepColorAt(step);
        string stepText = "<color=#" + ColorUtility.ToHtmlStringRGB(now) + ">\u2665 " + r.StepNameAt(step) + "</color>";
        string progressText = step >= last ? "<color=#" + ColorUtility.ToHtmlStringRGB(now) + ">Best of friends</color>"
            : "<b>" + Mathf.FloorToInt((float)points) + "</b><color=#9a9cb4> / " + Mathf.CeilToInt(r.StepPoints(step + 1)) + "  to " + r.StepNameAt(step + 1) + "</color>";
        foreach (RapportMeter m in rapportMeters)
        {
            if (m.step == null) continue;
            PixelUIKit.SetText(m.step, stepText);
            PixelUIKit.SetText(m.progress, progressText);
            for (int i = 0; i < m.fills.Length; i++)
            {
                // Segment i leads from step i to step i + 1: full once reached, part-filled while it is the next one, else empty.
                float lo = r.StepPoints(i), hi = r.StepPoints(i + 1);
                float frac = step > i ? 1f : step == i && hi > lo ? Mathf.Clamp01(((float)points - lo) / (hi - lo)) : 0f;
                Color c = r.StepColorAt(i + 1);
                m.fills[i].gameObject.SetActive(frac > 0.01f);
                m.fills[i].anchorMax = new Vector2(frac, 1f);
                m.fillImages[i].color = step > i ? c : Color.Lerp(c, Color.white, 0.25f) * new Color(1f, 1f, 1f, 0.85f);   // (the one being filled is a little paler)
                bool reached = step >= i + 1;
                m.names[i].color = reached ? c : i == step ? new Color(0.92f, 0.92f, 0.96f) : new Color(0.5f, 0.52f, 0.6f);
                m.names[i].fontStyle = reached || i == step ? FontStyles.Bold : FontStyles.Normal;
            }
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
