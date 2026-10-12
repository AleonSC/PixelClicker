using UnityEngine;

/// <summary>
/// The Wizard (id "wizard", Minigames tab, cheap): a visitor who stops time and sells POTIONS at a big discount (every potion whose
/// pixel type you have unlocked; combo potions can only be crafted). All the behaviour is in <see cref="PixelVisitorMinigame"/>.
/// </summary>
public class PixelWizardMinigame : PixelVisitorMinigame
{
    [Header("Stock")]
    [Min(1)]
    [Tooltip("How many different potions he brings each visit (a random selection of the ones he could sell).")]
    [SerializeField] private int potionsOffered = 3;

    [Min(1)]
    [Tooltip("Fewest bottles of each potion he has.")]
    [SerializeField] private int stockMin = 1;

    [Min(1)]
    [Tooltip("Most bottles of each potion he has (so you can't stock up).")]
    [SerializeField] private int stockMax = 3;

    protected override int OfferedWares => potionsOffered;
    protected override int StockMin => stockMin;
    protected override int StockMax => stockMax;

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
        // A purple cube wizard: pointy star hat, a long white beard down the front, a glowing staff.
        Color face = new Color(0.45f, 0.3f, 0.78f), top = new Color(0.64f, 0.5f, 0.92f), side = new Color(0.28f, 0.18f, 0.55f);
        Color dark = new Color(0.1f, 0.07f, 0.2f), white = new Color(0.97f, 0.97f, 1f), beard = new Color(0.93f, 0.93f, 0.97f);
        Color gold = new Color(1f, 0.82f, 0.25f), wood = new Color(0.45f, 0.3f, 0.15f), glow = new Color(0.55f, 0.95f, 1f);
        Transform a = art;
        CubeBody(a, face, top, side, new Color(0.15f, 0.1f, 0.2f), arms: false);
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        // The staff in his right hand.
        Block(a, "Staff", wood, new Vector2(12f, 330f), new Vector2(148f, 175f), -4f);
        Block(a, "Staff Orb Glow", new Color(glow.r, glow.g, glow.b, 0.35f), new Vector2(66f, 66f), new Vector2(140f, 350f), 45f);
        Block(a, "Staff Orb", glow, new Vector2(38f, 38f), new Vector2(140f, 350f), 45f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        // Eyes under bushy brows, a long beard over the lower face.
        CubeEyes(a, white, dark, 214f, 42f, 0f, 0.8f);
        Block(a, "Brow L", beard, new Vector2(64f, 14f), new Vector2(-42f, 240f), 8f);
        Block(a, "Brow R", beard, new Vector2(64f, 14f), new Vector2(42f, 240f), -8f);
        Block(a, "Beard", beard, new Vector2(176f, 92f), new Vector2(0f, 124f));
        Block(a, "Beard Tip", beard, new Vector2(110f, 44f), new Vector2(0f, 62f));
        Block(a, "Beard Point", beard, new Vector2(54f, 30f), new Vector2(0f, 38f), 0f);
        Block(a, "Mustache", beard, new Vector2(100f, 20f), new Vector2(0f, 178f));
        // Pointy hat with a star.
        Block(a, "Hat Brim", side, new Vector2(196f, 16f), new Vector2(0f, 304f));
        Block(a, "Hat Mid", face, new Vector2(130f, 52f), new Vector2(0f, 336f));
        Block(a, "Hat Top", face, new Vector2(70f, 44f), new Vector2(8f, 376f), -8f);
        Block(a, "Hat Band", gold, new Vector2(130f, 12f), new Vector2(0f, 318f));
        Block(a, "Hat Star", gold, new Vector2(20f, 20f), new Vector2(0f, 340f), 45f);
    }
}
