using UnityEngine;

/// <summary>
/// The Tinkerer (id "tinkerer", Minigames tab, very cheap): a visitor who stops time and sells every consumable DEVICE at a big
/// discount. All the behaviour (sliding in, greeting, accept / reject, wares window) is in <see cref="PixelVisitorMinigame"/>.
/// </summary>
public class PixelTinkererMinigame : PixelVisitorMinigame
{
    public override string Id => "tinkerer";
    public override string DisplayName => "Tinkerer";

    protected override bool IsWare(int item) => consumables != null && consumables.IsDevice(item);
    protected override string DefaultName => "Tinkerer";
    protected override string DefaultGreeting => "Well hello there! I'm the Tinkerer. I've got gadgets for every occasion, and for you I'll knock {0}% off every one. Interested?";
    protected override string DefaultWaresTitle => "Tinkerer's Wares";
    protected override string SaleFormat => "{0}% off every device";
    protected override string DefaultArrivedLog => "The Tinkerer is here!";

    protected override void DrawVisitor(Transform art)
    {
        Color coat = new Color(0.55f, 0.33f, 0.15f), dark = new Color(0.16f, 0.1f, 0.07f), skin = new Color(0.95f, 0.78f, 0.6f);
        Color metal = new Color(0.58f, 0.62f, 0.68f), yellow = new Color(1f, 0.82f, 0.25f), orange = new Color(0.92f, 0.5f, 0.12f);
        Transform a = art;
        Block(a, "Pack", new Color(0.33f, 0.36f, 0.42f), new Vector2(110f, 140f), new Vector2(-72f, 170f));
        Block(a, "Antenna", metal, new Vector2(8f, 60f), new Vector2(-100f, 275f));
        Block(a, "Antenna Knob", yellow, new Vector2(20f, 20f), new Vector2(-100f, 308f), 45f);
        Block(a, "Gear 1", orange, new Vector2(34f, 34f), new Vector2(-84f, 195f), 45f);
        Block(a, "Gear 2", yellow, new Vector2(24f, 24f), new Vector2(-62f, 150f), 20f);
        Block(a, "Leg L", new Color(0.25f, 0.2f, 0.28f), new Vector2(46f, 80f), new Vector2(-34f, 50f));
        Block(a, "Leg R", new Color(0.25f, 0.2f, 0.28f), new Vector2(46f, 80f), new Vector2(34f, 50f));
        Block(a, "Boot L", dark, new Vector2(62f, 26f), new Vector2(-38f, 13f));
        Block(a, "Boot R", dark, new Vector2(62f, 26f), new Vector2(38f, 13f));
        Block(a, "Arm L", coat, new Vector2(38f, 120f), new Vector2(-98f, 165f), 8f);
        Block(a, "Body", coat, new Vector2(150f, 170f), new Vector2(0f, 175f));
        Block(a, "Pocket", new Color(0.45f, 0.26f, 0.11f), new Vector2(54f, 40f), new Vector2(-30f, 150f));
        Block(a, "Belt", dark, new Vector2(150f, 20f), new Vector2(0f, 118f));
        Block(a, "Buckle", yellow, new Vector2(28f, 20f), new Vector2(0f, 118f));
        Block(a, "Arm R", coat, new Vector2(38f, 120f), new Vector2(100f, 185f), -28f);
        Block(a, "Hand R", skin, new Vector2(34f, 34f), new Vector2(128f, 238f));
        Block(a, "Wrench Handle", metal, new Vector2(14f, 100f), new Vector2(148f, 285f), -28f);
        Block(a, "Wrench Head", metal, new Vector2(46f, 26f), new Vector2(172f, 335f), -28f);
        Block(a, "Head", skin, new Vector2(112f, 100f), new Vector2(0f, 292f));
        Block(a, "Hat Brim", orange, new Vector2(138f, 20f), new Vector2(0f, 346f));
        Block(a, "Hat Top", orange, new Vector2(96f, 44f), new Vector2(0f, 372f));
        Block(a, "Strap", dark, new Vector2(118f, 16f), new Vector2(0f, 322f));
        Block(a, "Lens L Frame", dark, new Vector2(46f, 46f), new Vector2(-28f, 322f));
        Block(a, "Lens R Frame", dark, new Vector2(46f, 46f), new Vector2(28f, 322f));
        Block(a, "Lens L", new Color(0.55f, 0.88f, 1f), new Vector2(34f, 34f), new Vector2(-28f, 322f));
        Block(a, "Lens R", new Color(0.55f, 0.88f, 1f), new Vector2(34f, 34f), new Vector2(28f, 322f));
        Block(a, "Mouth", dark, new Vector2(38f, 8f), new Vector2(0f, 262f));
    }
}
