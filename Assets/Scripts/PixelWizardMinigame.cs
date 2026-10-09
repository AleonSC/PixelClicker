using UnityEngine;

/// <summary>
/// The Wizard (id "wizard", Minigames tab, cheap): a visitor who stops time and sells POTIONS at a big discount (every potion whose
/// pixel type you have unlocked; combo potions can only be crafted). All the behaviour is in <see cref="PixelVisitorMinigame"/>.
/// </summary>
public class PixelWizardMinigame : PixelVisitorMinigame
{
    public override string Id => "wizard";
    public override string DisplayName => "Wizard";

    protected override bool IsWare(int item) =>
        consumables != null && clicker != null && !consumables.IsDevice(item) && !consumables.ItemCraftOnly(item)
        && (consumables.ItemOwned(item) > 0 || clicker.IsUnlocked(consumables.ItemRequiredType(item)));

    protected override string DefaultName => "Wizard";
    protected override string DefaultGreeting => "Ah, a customer! I am the Wizard, brewer of fine potions. For you, a mere {0}% off every bottle. Shall we deal?";
    protected override string DefaultWaresTitle => "Wizard's Potions";
    protected override string SaleFormat => "{0}% off every potion";
    protected override string DefaultArrivedLog => "The Wizard is here!";
    protected override float StartDelayOffset => 120f;
    protected override Color TitleColor => new Color(0.8f, 0.65f, 1f);

    protected override void DrawVisitor(Transform art)
    {
        Color robe = new Color(0.32f, 0.2f, 0.62f), robeDark = new Color(0.22f, 0.13f, 0.45f), skin = new Color(0.95f, 0.78f, 0.62f);
        Color beard = new Color(0.93f, 0.93f, 0.97f), gold = new Color(1f, 0.82f, 0.25f), wood = new Color(0.45f, 0.3f, 0.15f);
        Color glow = new Color(0.55f, 0.95f, 1f);

        Block(art, "Cape", robeDark, new Vector2(190f, 230f), new Vector2(-10f, 150f));
        Block(art, "Boot L", new Color(0.15f, 0.1f, 0.2f), new Vector2(54f, 24f), new Vector2(-34f, 12f));
        Block(art, "Boot R", new Color(0.15f, 0.1f, 0.2f), new Vector2(54f, 24f), new Vector2(34f, 12f));
        Block(art, "Robe Skirt", robe, new Vector2(170f, 120f), new Vector2(0f, 80f));
        Block(art, "Robe Body", robe, new Vector2(140f, 150f), new Vector2(0f, 190f));
        Block(art, "Sash", gold, new Vector2(144f, 16f), new Vector2(0f, 150f));
        Block(art, "Star", gold, new Vector2(24f, 24f), new Vector2(-26f, 210f), 45f);
        Block(art, "Star 2", gold, new Vector2(16f, 16f), new Vector2(30f, 235f), 20f);
        Block(art, "Arm L", robe, new Vector2(36f, 110f), new Vector2(-94f, 175f), 12f);
        Block(art, "Arm R", robe, new Vector2(36f, 110f), new Vector2(96f, 195f), -18f);
        Block(art, "Hand R", skin, new Vector2(32f, 32f), new Vector2(116f, 250f));
        Block(art, "Staff", wood, new Vector2(12f, 330f), new Vector2(120f, 170f), -4f);
        Block(art, "Staff Orb Glow", new Color(glow.r, glow.g, glow.b, 0.35f), new Vector2(70f, 70f), new Vector2(110f, 345f), 45f);
        Block(art, "Staff Orb", glow, new Vector2(40f, 40f), new Vector2(110f, 345f), 45f);
        Block(art, "Head", skin, new Vector2(100f, 90f), new Vector2(0f, 275f));
        Block(art, "Beard", beard, new Vector2(96f, 90f), new Vector2(0f, 228f));
        Block(art, "Beard Tip", beard, new Vector2(52f, 50f), new Vector2(0f, 186f));
        Block(art, "Mustache", beard, new Vector2(70f, 18f), new Vector2(0f, 262f));
        Block(art, "Eye L", new Color(0.1f, 0.1f, 0.2f), new Vector2(12f, 12f), new Vector2(-20f, 292f));
        Block(art, "Eye R", new Color(0.1f, 0.1f, 0.2f), new Vector2(12f, 12f), new Vector2(20f, 292f));
        Block(art, "Hat Brim", robeDark, new Vector2(150f, 18f), new Vector2(0f, 318f));
        Block(art, "Hat Mid", robe, new Vector2(100f, 50f), new Vector2(0f, 346f));
        Block(art, "Hat Top", robe, new Vector2(60f, 46f), new Vector2(8f, 388f), -8f);
        Block(art, "Hat Band", gold, new Vector2(100f, 12f), new Vector2(0f, 328f));
        Block(art, "Hat Star", gold, new Vector2(18f, 18f), new Vector2(0f, 350f), 45f);
    }
}
