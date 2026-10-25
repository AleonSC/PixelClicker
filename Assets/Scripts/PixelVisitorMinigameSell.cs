// ---- System notes ----
// PixelVisitorMinigameSell.cs
//   Part of PixelVisitorMinigame: the SELL VIEW every vendor who buys from you shows on his Sell tab (BuysFromPlayer; was Big Tim's own panel).
//   - Goods (CollectSellGoods): his skill's currencies you hold (GoodsSkillId, at the raw-goods rate 1.2x) + his SPECIALTY goods from the market's
//     one table (PixelMarket.SpecialtiesOf: minigame materials at ~2x, read / spent through PixelMaterials.CostHeld / CostSpend). A vendor can add
//     his own currencies / materials - NEVER HUSKS: vendors buy husks only through delivery jobs (docs/systems/PixelRules.md).
//   - Pick the good, the amount (slider + 10 / 25 / 50% / All) and what to be paid in (a pure colour); the big button shows the live price
//     ("Sell for 1.2K Red") and the line above it his market for that good (PixelMarket.TrendText). Price = PixelMarket.Quote (rate x what a click
//     on pure blocks earns x the effort behind the goods, less as his market fills up) x his mood (once per visit, moodRange) x a roll (once per
//     good per visit, the average of two in offerRollRange) x OfferBonus (Big Tim: + his Woodcutting level). Selling records the sale (saturation).
//   - TRADE buttons under it (CollectTradeOffers; the Wizard): swap a fixed amount of the picked good for something only he has - shown beside the
//     sell price so both choices are clear.
//   - A vendor with no wares opens straight on this view (no Buy / Sell tabs).
//   - GIFT button beside Sell (rapport, PixelVisitorMinigameRapport.cs): gives some of the picked good for rapport only; goods he LOVES are marked
//     with a heart, and a loved good he doesn't buy is listed as 'gift only'.
//   - MARKET CHARM button (only while you hold that Relic, PixelRelics): cast it and every vendor pays full price again; it shows the minutes
//     left while it recharges.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public abstract partial class PixelVisitorMinigame
{
    [Header("Sell view (vendors who buy from you)")]
    [Tooltip("His mood, rolled once per visit (min, max): a good day pays better.")]
    [SerializeField] private Vector2 moodRange = new Vector2(0.85f, 1.15f);
    [Tooltip("Random roll on each good (min, max), made once per good per visit; the price uses the average of two rolls, so it is usually near the middle.")]
    [SerializeField] private Vector2 offerRollRange = new Vector2(0.85f, 1.15f);

    /// <summary>One thing he buys in the Sell view.</summary>
    protected class SellGood
    {
        public string marketKey;           // his market (saturation) for it: PixelMarket.TierKey / MaterialKey
        public string goodId;              // the pixel type name / material id (what PixelRapport's loves lists name); null = none
        public bool giftOnly;              // a good he loves but doesn't buy: it can only be given as a gift (rapport)
        public string name;
        public Color color = Color.white;
        public double clicksPerUnit;       // effort behind one unit
        public float rate;                 // x what clicking pure blocks earns
        public Func<double> held;          // how many you can sell
        public Func<double, bool> take;    // takes them (false = not enough any more)
    }

    /// <summary>A trade beside selling (the Wizard): give 'cost' of the picked good for something only he has.</summary>
    protected class TradeOffer
    {
        public double cost;
        public string label;               // e.g. "Ghost Bait"
        public Func<bool> available;       // can he hand it over now? (null = yes; e.g. you hold the most Ghost Bait you can)
        public Func<bool> give;            // hands it over (false = could not)
    }

    /// <summary>The goods he buys: his skill's currencies you hold + his specialty materials. Override to add more (call base first).</summary>
    protected virtual void CollectSellGoods(List<SellGood> list)
    {
        PixelMarket market = PixelMarket.Instance;
        if (market == null || clicker == null) return;
        if (GoodsSkillId != null)
            for (int i = 0; i < clicker.Tiers.Length; i++)
            {
                if (!IsGood(i)) continue;
                int tier = i;
                list.Add(new SellGood
                {
                    marketKey = PixelMarket.TierKey(tier), goodId = clicker.Tiers[tier].type.ToString(), name = clicker.Tiers[tier].displayName, color = clicker.Tiers[tier].UIColor,
                    clicksPerUnit = market.ClicksPerUnit(tier), rate = market.RateOf(PixelMarket.Route.RawGoods),
                    held = () => System.Math.Floor(clicker.Tiers[tier].count), take = n => clicker.TrySpend(tier, n),
                });
            }
        foreach (PixelMarket.SpecialtyGood s in market.SpecialtiesOf(Id))
        {
            string id = s.goodId;
            list.Add(new SellGood
            {
                marketKey = PixelMarket.MaterialKey(id), goodId = id, name = PixelMaterials.CostName(id), color = PixelMaterials.CostColor(id),
                clicksPerUnit = s.effortClicksPerUnit, rate = s.rate,
                held = () => System.Math.Floor(PixelMaterials.CostHeld(id)),
                take = n => { if (!PixelMaterials.CostAffordable(id, n)) return false; PixelMaterials.CostSpend(id, n); return true; },
            });
        }
    }

    /// <summary>Trades he offers for the picked good (none by default; the Wizard has some).</summary>
    protected virtual void CollectTradeOffers(SellGood good, List<TradeOffer> list) { }

    /// <summary>An extra factor on his prices (Big Tim: his Woodcutting level).</summary>
    protected virtual double OfferBonus => 1d;

    protected virtual string NothingToSellText => "Come back when you've got something I'd buy.";
    protected virtual string SellPromptText => "Pick what you're selling and how much. That's my price on the button.";

    // ---- The view ----

    private TMP_Dropdown goodDropdown, payDropdown;
    private Slider amountSlider;
    private TMP_Text haveLabel, amountLabel, sayLabel, marketLabel, sellLabel;
    private Button sellButton, giftButton, charmButton;
    private TMP_Text giftLabel, charmLabel;
    private readonly Button[] tradeButtons = new Button[2];
    private readonly TMP_Text[] tradeLabels = new TMP_Text[2];
    private readonly List<TradeOffer> tradeOffers = new List<TradeOffer>();
    private readonly List<SellGood> sellGoods = new List<SellGood>();
    private readonly List<int> payTiers = new List<int>();
    private readonly Dictionary<string, double> visitRolls = new Dictionary<string, double>();   // good -> his roll, fixed for the whole visit
    private float mood = 1f;
    private bool sellUpdating;
    private double shownPrice;

    /// <summary>Builds the Sell view in the wares window (called by BuildWaresPanel for a vendor who buys from you).</summary>
    private void BuildGoodsSellPanel(RectTransform parent)
    {
        mood = UnityEngine.Random.Range(Mathf.Min(moodRange.x, moodRange.y), Mathf.Max(moodRange.x, moodRange.y));
        visitRolls.Clear();
        TMP_FontAsset font = Font;
        Color box = new Color(0.16f, 0.16f, 0.22f, 1f);

        SellLabel(font, parent, "Sell", 0f, 50f, 0f, 0.2f);
        goodDropdown = PixelUIKit.CreateDropdown(font, parent, "Good", new Vector2(100f, 50f), box, box, Color.white, 24f);
        SellPlace(goodDropdown.GetComponent<RectTransform>(), 0f, 50f, 0.2f, 1f);
        goodDropdown.onValueChanged.AddListener(_ => { if (!sellUpdating) RefreshSellAmount(true); });

        haveLabel = SellLabel(font, parent, "", 64f, 40f, 0f, 0.55f);
        marketLabel = SellLabel(font, parent, "", 64f, 40f, 0.55f, 1f);   // his current price for the picked good and its trend (PixelMarket)
        marketLabel.richText = true;
        marketLabel.alignment = TextAlignmentOptions.MidlineRight;

        amountSlider = PixelUIKit.CreateSlider(parent, "Amount", new Color(0.25f, 0.25f, 0.32f, 1f), new Color(0.85f, 0.45f, 0.3f, 1f), Color.white);
        amountSlider.wholeNumbers = true;
        SellPlace(amountSlider.GetComponent<RectTransform>(), 112f, 30f, 0f, 1f);
        amountSlider.onValueChanged.AddListener(_ => { if (!sellUpdating) RefreshSellPrice(); });

        string[] quick = { "10%", "25%", "50%", "All" };
        float[] frac = { 0.1f, 0.25f, 0.5f, 1f };
        for (int i = 0; i < 4; i++)
        {
            float f = frac[i];
            Button b = PixelUIKit.CreateButton(font, parent, quick[i], quick[i], new Vector2(100f, 44f), ButtonRole.Neutral, 22f);
            SellPlace(b.GetComponent<RectTransform>(), 160f, 44f, i * 0.13f, i * 0.13f + 0.12f);
            b.onClick.AddListener(() => { if (amountSlider.maxValue >= 1f) amountSlider.value = Mathf.Max(1f, Mathf.Floor(amountSlider.maxValue * f)); });
        }
        amountLabel = SellLabel(font, parent, "", 160f, 44f, 0.54f, 1f);
        amountLabel.alignment = TextAlignmentOptions.MidlineRight;

        SellLabel(font, parent, "Paid in", 222f, 50f, 0f, 0.2f);
        payDropdown = PixelUIKit.CreateDropdown(font, parent, "Pay", new Vector2(100f, 50f), box, box, Color.white, 24f);
        SellPlace(payDropdown.GetComponent<RectTransform>(), 222f, 50f, 0.2f, 1f);
        payDropdown.onValueChanged.AddListener(_ => { if (!sellUpdating) RefreshSellPrice(); });

        // The live price on the button: 'Sell for 1.2K Red'.
        sellButton = PixelUIKit.CreateButton(font, parent, "Sell", "", new Vector2(100f, 60f), ButtonRole.Success, 28f);
        sellLabel = sellButton.GetComponentInChildren<TMP_Text>(true);
        SellPlace(sellButton.GetComponent<RectTransform>(), 292f, 60f, 0f, 0.62f);
        sellButton.onClick.AddListener(SellNow);

        // The gift beside it (rapport, PixelRapport): 'Gift 12 Ectoplasm ♥'.
        giftButton = PixelUIKit.CreateButton(font, parent, "Gift", "", new Vector2(100f, 60f), ButtonRole.Primary, 22f);
        giftLabel = giftButton.GetComponentInChildren<TMP_Text>(true);
        PixelUIKit.FitOneLine(giftLabel, 12f);
        SellPlace(giftButton.GetComponent<RectTransform>(), 292f, 60f, 0.62f, 1f);
        giftButton.onClick.AddListener(GiveGift);
        giftButton.gameObject.SetActive(PixelRapport.Instance != null);

        // Trades (the Wizard): 'Trade 20 Ectoplasm for Ghost Bait'.
        for (int i = 0; i < 2; i++)
        {
            int captured = i;
            tradeButtons[i] = PixelUIKit.CreateButton(font, parent, "Trade " + i, "", new Vector2(100f, 60f), ButtonRole.Primary, 22f);
            tradeLabels[i] = tradeButtons[i].GetComponentInChildren<TMP_Text>(true);
            PixelUIKit.FitOneLine(tradeLabels[i], 12f);
            SellPlace(tradeButtons[i].GetComponent<RectTransform>(), 362f, 60f, i * 0.5f, i * 0.5f + 0.5f);
            tradeButtons[i].onClick.AddListener(() => DoTradeOffer(captured));
            tradeButtons[i].gameObject.SetActive(false);
        }

        // The Market Charm (a Relic, PixelRelics): only while you hold it, under the trades; his words move down to make room.
        charmButton = null;
        bool charm = PixelRelics.HoldsCharm;
        if (charm)
        {
            charmButton = PixelUIKit.CreateButton(font, parent, "Market Charm", "", new Vector2(100f, 48f), ButtonRole.Selected, 22f);
            charmLabel = charmButton.GetComponentInChildren<TMP_Text>(true);
            PixelUIKit.FitOneLine(charmLabel, 12f);
            SellPlace(charmButton.GetComponent<RectTransform>(), 432f, 48f, 0.1f, 0.9f);
            charmButton.onClick.AddListener(CastCharm);
        }

        sayLabel = PixelUIKit.CreateText(font, parent, "Say", "", 26f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        sayLabel.richText = true;
        sayLabel.enableAutoSizing = true; sayLabel.fontSizeMax = 26f; sayLabel.fontSizeMin = 14f;
        SellPlace(sayLabel.rectTransform, charm ? 486f : 436f, charm ? 60f : 110f, 0f, 1f);
        RefreshSellLists();
    }

    private static TMP_Text SellLabel(TMP_FontAsset font, RectTransform parent, string text, float y, float h, float x0, float x1)
    {
        TMP_Text t = PixelUIKit.CreateText(font, parent, "Label", text, 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        t.enableAutoSizing = true; t.fontSizeMax = 24f; t.fontSizeMin = 12f;
        SellPlace(t.rectTransform, y, h, x0, x1);
        return t;
    }

    /// <summary>Stretches 'r' between the fractions x0..x1 of the panel's width, 'y' down from its top, 'h' tall.</summary>
    private static void SellPlace(RectTransform r, float y, float h, float x0, float x1)
    {
        r.anchorMin = new Vector2(x0, 1f); r.anchorMax = new Vector2(x1, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.offsetMin = new Vector2(x0 > 0f ? 6f : 0f, -(y + h)); r.offsetMax = new Vector2(x1 < 1f ? -6f : 0f, -y);
    }

    /// <summary>Fills the drop-downs: the goods you hold that he buys, and the pure colours you have unlocked.</summary>
    private void RefreshSellLists()
    {
        if (goodDropdown == null || clicker == null) return;
        sellUpdating = true;
        string keep = SelectedGood != null ? SelectedGood.marketKey : null;
        int keepPay = SelectedPay;
        List<SellGood> all = new List<SellGood>();
        CollectSellGoods(all);
        AddGiftOnlyGoods(all);
        sellGoods.Clear();
        List<string> names = new List<string>();
        foreach (SellGood g in all)
        {
            double n = g.held != null ? g.held() : 0d;
            if (n < 1d) continue;
            sellGoods.Add(g);
            names.Add(g.name + "  (" + PixelClicker.FormatNumberShort(n) + ")" + (g.rate > 1.5f ? "  ★" : "") + (LovesGood(g) ? "  \u2665" : "") + (g.giftOnly ? " gift" : ""));   // (a star: one of his specialties; a heart: he loves it as a gift)
        }
        payTiers.Clear();
        List<string> payNames = new List<string>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
            if (PixelMarket.IsPure(clicker.Tiers[i].type) && clicker.Tiers[i].unlocked) { payTiers.Add(i); payNames.Add(clicker.Tiers[i].displayName); }
        if (names.Count == 0) names.Add("(nothing he buys)");
        if (payNames.Count == 0) payNames.Add("(nothing)");
        goodDropdown.ClearOptions(); goodDropdown.AddOptions(names);
        goodDropdown.SetValueWithoutNotify(Mathf.Max(0, sellGoods.FindIndex(g => g.marketKey == keep)));
        goodDropdown.RefreshShownValue();
        payDropdown.ClearOptions(); payDropdown.AddOptions(payNames);
        payDropdown.SetValueWithoutNotify(Mathf.Max(0, payTiers.IndexOf(keepPay)));
        payDropdown.RefreshShownValue();
        sellUpdating = false;
        RefreshSellAmount(false);
        PixelUIKit.SetText(sayLabel, sellGoods.Count == 0 ? NothingToSellText : (mood >= 1.05f ? "Good day today! " : mood <= 0.92f ? "Rough day... " : "") + SellPromptText);
    }

    private SellGood SelectedGood => sellGoods.Count > 0 && goodDropdown != null ? sellGoods[Mathf.Clamp(goodDropdown.value, 0, sellGoods.Count - 1)] : null;
    private int SelectedPay => payTiers.Count > 0 && payDropdown != null ? payTiers[Mathf.Clamp(payDropdown.value, 0, payTiers.Count - 1)] : -1;

    private void RefreshSellAmount(bool resetToHalf)
    {
        SellGood g = SelectedGood;
        double held = g != null ? g.held() : 0d;
        sellUpdating = true;
        float max = (float)System.Math.Min(held, 1e9);
        amountSlider.minValue = max >= 1f ? 1f : 0f;
        amountSlider.maxValue = Mathf.Max(amountSlider.minValue, max);
        amountSlider.interactable = max >= 1f;
        if (resetToHalf || amountSlider.value > max || amountSlider.value < 1f) amountSlider.SetValueWithoutNotify(Mathf.Max(amountSlider.minValue, Mathf.Floor(max * 0.5f)));
        sellUpdating = false;
        PixelUIKit.SetText(haveLabel, g != null ? "You have " + PixelClicker.FormatNumberShort(held) + " " + g.name : "");
        RefreshSellPrice();
    }

    /// <summary>His price for 'amount' of a good, paid in pure units: the market's quote x his mood x his roll for that good x OfferBonus.</summary>
    private double SellPrice(SellGood g, double amount)
    {
        PixelMarket market = PixelMarket.Instance;
        if (market == null || g == null || g.giftOnly || amount < 1d) return 0d;
        if (!visitRolls.TryGetValue(g.marketKey, out double roll))
        {
            float lo = Mathf.Min(offerRollRange.x, offerRollRange.y), hi = Mathf.Max(offerRollRange.x, offerRollRange.y);
            roll = (UnityEngine.Random.Range(lo, hi) + UnityEngine.Random.Range(lo, hi)) * 0.5;
            visitRolls[g.marketKey] = roll;
        }
        return System.Math.Max(1d, System.Math.Floor(market.Quote(Id, g.marketKey, g.clicksPerUnit, amount, g.rate) * mood * roll * OfferBonus));
    }

    private void RefreshSellPrice()
    {
        SellGood g = SelectedGood;
        int pay = SelectedPay;
        double amount = System.Math.Floor(amountSlider.value);
        PixelMarket market = PixelMarket.Instance;
        PixelUIKit.SetText(amountLabel, g != null ? "Selling " + PixelClicker.FormatNumberShort(amount) + " " + g.name : "");
        PixelUIKit.SetText(marketLabel, market != null && g != null ? market.TrendText(Id, g.marketKey) : "");
        shownPrice = g != null && pay >= 0 ? SellPrice(g, amount) : 0d;
        bool can = g != null && pay >= 0 && amount >= 1d && shownPrice >= 1d;
        PixelUIKit.SetText(sellLabel, can ? "Sell for " + PixelClicker.FormatNumberShort(shownPrice) + " " + clicker.Tiers[pay].displayName : g != null && g.giftOnly ? "Gift only" : "Sell");
        sellButton.interactable = can;
        RefreshGiftButton(g);
        RefreshCharmButton();

        // Trades for this good (the Wizard): each button says what it costs and what you get.
        tradeOffers.Clear();
        if (g != null) CollectTradeOffers(g, tradeOffers);
        double held = g != null ? g.held() : 0d;
        for (int i = 0; i < tradeButtons.Length; i++)
        {
            bool on = i < tradeOffers.Count;
            if (tradeButtons[i].gameObject.activeSelf != on) tradeButtons[i].gameObject.SetActive(on);
            if (!on) continue;
            TradeOffer t = tradeOffers[i];
            PixelUIKit.SetText(tradeLabels[i], "Trade " + PixelClicker.FormatNumberShort(t.cost) + " " + g.name + " for " + t.label);
            tradeButtons[i].interactable = held >= t.cost && (t.available == null || t.available());
        }
    }

    /// <summary>The Gift button: how many make a gift, a heart for a loved good, the gifts left this visit.</summary>
    private void RefreshGiftButton(SellGood g)
    {
        if (giftButton == null || !giftButton.gameObject.activeSelf) return;
        double amount = GiftAmount(g);
        bool loved = LovesGood(g);
        if (GiftsLeft < 1) PixelUIKit.SetText(giftLabel, "No more gifts today");
        else PixelUIKit.SetText(giftLabel, g != null ? "Gift " + PixelClicker.FormatNumberShort(amount) + " " + g.name + (loved ? " \u2665" : "") : "Gift");
        giftButton.interactable = g != null && GiftsLeft > 0 && g.held() >= amount;
    }

    /// <summary>The Market Charm button: ready to cast, or the minutes left while it recharges.</summary>
    private void RefreshCharmButton()
    {
        if (charmButton == null) return;
        double left = PixelRelics.CharmReadyIn;
        PixelUIKit.SetText(charmLabel, left <= 0d ? "\u2726 Cast the Market Charm: every vendor pays full price" : "Market Charm recharging: " + Mathf.CeilToInt((float)(left / 60d)) + " min");
        charmButton.interactable = left <= 0d;
    }

    private void CastCharm()
    {
        if (!PixelRelics.CastCharm()) return;
        RefreshSellLists();
        PixelUIKit.SetText(sayLabel, "Ooh, that charm! Fine, fine. Full price, for everything.");
    }

    private void SellNow()
    {
        SellGood g = SelectedGood;
        int pay = SelectedPay;
        double amount = System.Math.Floor(amountSlider.value);
        if (g == null || g.giftOnly || pay < 0 || amount < 1d) return;
        double price = SellPrice(g, amount);
        if (!g.take(amount)) { PixelUIKit.SetText(sayLabel, "Looks like you don't have that much any more."); RefreshSellLists(); return; }
        clicker.AddCurrency(pay, price);
        PixelMarket market = PixelMarket.Instance;
        if (market != null) market.RecordSale(Id, g.marketKey, amount * g.clicksPerUnit);
        PixelRapport rapport = PixelRapport.Instance;
        if (rapport != null) RapportDeal(rapport.SalePoints(amount * g.clicksPerUnit, g.rate > 1.5f));   // (specialty goods count more)
        PixelStats.Count(Id + ".sold");
        PixelAudio.Play("purchase");
        RefreshSellLists();
        PixelUIKit.SetText(sayLabel, "Pleasure doing business! " + PixelClicker.FormatNumber(price) + " " + clicker.Tiers[pay].displayName + " for you.");
    }

    private void DoTradeOffer(int index)
    {
        SellGood g = SelectedGood;
        if (g == null || index >= tradeOffers.Count) return;
        TradeOffer t = tradeOffers[index];
        if (g.held() < t.cost || (t.available != null && !t.available())) return;
        if (!t.give()) { PixelUIKit.SetText(sayLabel, "Hm, that didn't work out."); RefreshSellLists(); return; }   // (handed over first, so nothing is lost if he can't)
        g.take(t.cost);
        PixelRapport rapport = PixelRapport.Instance;
        if (rapport != null) RapportDeal(rapport.TradePoints);
        PixelStats.Count(Id + ".traded");
        PixelAudio.Play("purchase");
        RefreshSellLists();
        PixelUIKit.SetText(sayLabel, "A fine trade! " + t.label + " is yours.");
    }
}
