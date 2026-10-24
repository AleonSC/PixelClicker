// ---- System notes ----
// PixelForagerVendor.cs
//   The Forager (id forager, 'Hazel the Forager'; a mossy green cube with a red mushroom-cap hat and a wicker basket): a goods buyer -
//   she BUYS Foraging's goods (berries, mushrooms, herbs, truffles, weeds...) for pure currency through the shared goods buyer
//   (PixelVisitorMinigameGoods.cs: offers from her GoodsOffers block, priced by PixelMarket, market price and trend on each row). She only
//   visits while Foraging is the skill in training (VisitSkillId) and is started by PixelGuideVendor.StartVendors once Foraging is
//   available; she can also be called from the Toggles window (shared cooldown). Texts: Dialogue.txt [Visitor: forager].
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>The Forager (id "forager"): buys foraged goods for pure currency while Foraging is trained. All the visiting is in <see cref="PixelVisitorMinigame"/>.</summary>
public class PixelForagerVendor : PixelVisitorMinigame
{
    [Header("Offers")]
    [Tooltip("How she makes her offers (the price itself is PixelMarket's shared rule).")]
    [SerializeField] private GoodsOffers offers = new GoodsOffers();

    public override string Id => "forager";
    public override string DisplayName => "Forager";

    protected override bool IsWare(int item) => false;
    protected override string DefaultName => "Hazel the Forager";
    protected override string DefaultGreeting => "Ooh, you've been out in the brambles too! I'll take berries, mushrooms, herbs - truffles most of all. Pure pixels for the lot. Shall we trade?";
    protected override string DefaultWaresTitle => "Hazel's Basket";
    protected override string SaleFormat => "Fresh-picked prices!";
    protected override string DefaultArrivedLog => "The Forager is here!";
    protected override float StartDelayOffset => 30f;
    protected override Color TitleColor => new Color(0.95f, 0.45f, 0.55f);
    protected override string TradeText => "Sell";
    protected override string TradePayWord => "Sell";
    protected override string VisitSkillId => PixelSkills.ForagingId;   // only visits while it is in training (and says so in Toggles)

    protected override bool BuysGoods => true;
    protected override string GoodsSkillId => PixelSkills.ForagingId;
    protected override GoodsOffers Offers => offers;
    protected override List<Trade> RollTrades() => RollGoodsTrades();

    /// <summary>A mossy green cube with a red, white-spotted mushroom cap for a hat and a wicker basket of berries.</summary>
    protected override void DrawVisitor(Transform art)
    {
        Color face = new Color(0.42f, 0.62f, 0.3f), top = new Color(0.6f, 0.78f, 0.42f), side = new Color(0.27f, 0.42f, 0.19f);
        Color dark = new Color(0.1f, 0.08f, 0.06f), white = new Color(0.97f, 0.97f, 0.92f);
        Color cap = new Color(0.82f, 0.18f, 0.2f), capDark = new Color(0.6f, 0.1f, 0.13f), wicker = new Color(0.72f, 0.52f, 0.28f), wickerDark = new Color(0.5f, 0.35f, 0.16f);
        Color berry = new Color(0.5f, 0.15f, 0.55f), leaf = new Color(0.25f, 0.55f, 0.2f), cheek = new Color(0.85f, 0.45f, 0.45f);
        Transform a = art;
        CubeBody(a, face, top, side, dark);
        // The mushroom-cap hat.
        Block(a, "Cap", cap, new Vector2(220f, 60f), new Vector2(0f, 322f));
        Block(a, "Cap Top", cap, new Vector2(150f, 34f), new Vector2(0f, 360f));
        Block(a, "Cap Rim", capDark, new Vector2(226f, 12f), new Vector2(0f, 294f));
        Block(a, "Spot 1", white, new Vector2(26f, 22f), new Vector2(-62f, 328f));
        Block(a, "Spot 2", white, new Vector2(20f, 18f), new Vector2(10f, 358f));
        Block(a, "Spot 3", white, new Vector2(24f, 20f), new Vector2(70f, 322f));
        // A wicker basket of berries on the right arm.
        Block(a, "Basket Handle", wickerDark, new Vector2(80f, 12f), new Vector2(150f, 160f));
        Block(a, "Basket", wicker, new Vector2(96f, 60f), new Vector2(150f, 112f));
        Block(a, "Basket Band", wickerDark, new Vector2(96f, 8f), new Vector2(150f, 120f));
        Block(a, "Berry 1", berry, new Vector2(20f, 20f), new Vector2(128f, 148f));
        Block(a, "Berry 2", berry, new Vector2(20f, 20f), new Vector2(150f, 152f));
        Block(a, "Berry 3", berry, new Vector2(20f, 20f), new Vector2(172f, 147f));
        Block(a, "Leaf", leaf, new Vector2(26f, 12f), new Vector2(160f, 162f), 20f);
        // A kind face with rosy cheeks.
        CubeEyes(a, white, dark, 204f, 40f, 0f, 0.9f);
        Block(a, "Cheek L", cheek, new Vector2(26f, 14f), new Vector2(-62f, 168f));
        Block(a, "Cheek R", cheek, new Vector2(26f, 14f), new Vector2(62f, 168f));
        Block(a, "Smile", dark, new Vector2(56f, 10f), new Vector2(0f, 150f));
    }
}
