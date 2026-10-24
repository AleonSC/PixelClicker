// ---- System notes ----
// PixelAnglerVendor.cs
//   The Angler (id angler, 'Old Salt the Angler'; a navy cube in a yellow sou'wester with a grey beard and a fishing rod): a goods buyer -
//   he BUYS Fishing's goods (fish, seaweed, shells, pearls, water...) for pure currency through the shared goods buyer
//   (PixelVisitorMinigameGoods.cs: offers from his GoodsOffers block, priced by PixelMarket, market price and trend on each row). He only
//   visits while Fishing is the skill in training (VisitSkillId) and is started by PixelGuideVendor.StartVendors once Fishing is
//   available; he can also be called from the Toggles window (shared cooldown). Texts: Dialogue.txt [Visitor: angler].
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>The Angler (id "angler"): buys fished goods for pure currency while Fishing is trained. All the visiting is in <see cref="PixelVisitorMinigame"/>.</summary>
public class PixelAnglerVendor : PixelVisitorMinigame
{
    [Header("Offers")]
    [Tooltip("How he makes his offers (the price itself is PixelMarket's shared rule).")]
    [SerializeField] private GoodsOffers offers = new GoodsOffers();

    public override string Id => "angler";
    public override string DisplayName => "Angler";

    protected override bool IsWare(int item) => false;
    protected override string DefaultName => "Old Salt the Angler";
    protected override string DefaultGreeting => "Ahoy! Smells like a good catch on you. Fish, shells, pearls, even the seaweed - I'll buy it all for pure pixels. Let's haggle!";
    protected override string DefaultWaresTitle => "Old Salt's Offers";
    protected override string SaleFormat => "Fresh off the boat!";
    protected override string DefaultArrivedLog => "The Angler is here!";
    protected override float StartDelayOffset => 30f;
    protected override Color TitleColor => new Color(0.45f, 0.75f, 1f);
    protected override string TradeText => "Sell";
    protected override string TradePayWord => "Sell";
    protected override string VisitSkillId => PixelSkills.FishingId;   // only visits while it is in training (and says so in Toggles)

    protected override bool BuysGoods => true;
    protected override string GoodsSkillId => PixelSkills.FishingId;
    protected override GoodsOffers Offers => offers;
    protected override List<Trade> RollTrades() => RollGoodsTrades();

    /// <summary>A navy cube in a yellow sou'wester, with a bushy grey beard and a fishing rod with a bobber.</summary>
    protected override void DrawVisitor(Transform art)
    {
        Color face = new Color(0.22f, 0.36f, 0.58f), top = new Color(0.36f, 0.52f, 0.76f), side = new Color(0.13f, 0.22f, 0.38f);
        Color dark = new Color(0.06f, 0.07f, 0.1f), white = new Color(0.97f, 0.97f, 0.95f);
        Color hat = new Color(1f, 0.82f, 0.18f), hatDark = new Color(0.8f, 0.6f, 0.08f), beard = new Color(0.82f, 0.82f, 0.85f);
        Color rod = new Color(0.45f, 0.3f, 0.16f), line = new Color(0.85f, 0.85f, 0.85f), bobber = new Color(0.9f, 0.2f, 0.2f);
        Transform a = art;
        CubeBody(a, face, top, side, dark);
        // The fishing rod, held in the right hand, with its line and a red bobber.
        Block(a, "Rod", rod, new Vector2(12f, 280f), new Vector2(160f, 210f), -18f);
        Block(a, "Line", line, new Vector2(3f, 150f), new Vector2(208f, 270f));
        Block(a, "Bobber", bobber, new Vector2(20f, 20f), new Vector2(208f, 192f));
        Block(a, "Bobber Top", white, new Vector2(20f, 8f), new Vector2(208f, 204f));
        // The sou'wester: a wide brim, longer at the back.
        Block(a, "Hat", hat, new Vector2(170f, 56f), new Vector2(0f, 322f));
        Block(a, "Brim", hatDark, new Vector2(240f, 16f), new Vector2(-6f, 296f), -4f);
        // A squinty sailor's face and a bushy grey beard.
        Block(a, "Eye L", dark, new Vector2(40f, 10f), new Vector2(-42f, 222f));
        Block(a, "Eye R", dark, new Vector2(40f, 10f), new Vector2(42f, 222f));
        Block(a, "Beard", beard, new Vector2(170f, 90f), new Vector2(0f, 148f));
        Block(a, "Moustache", beard, new Vector2(110f, 22f), new Vector2(0f, 194f));
        Block(a, "Mouth", dark, new Vector2(40f, 8f), new Vector2(0f, 176f));
        Block(a, "Nose", new Color(0.85f, 0.55f, 0.5f), new Vector2(26f, 24f), new Vector2(0f, 206f));
    }
}
