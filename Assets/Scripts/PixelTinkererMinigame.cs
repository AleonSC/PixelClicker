using UnityEngine;

/// <summary>
/// The Tinkerer (id "tinkerer", Minigames tab, very cheap): a visitor who stops time and sells every consumable DEVICE at a big
/// discount. All the behaviour (sliding in, greeting, accept / reject, wares window) is in <see cref="PixelVisitorMinigame"/>.
/// </summary>
public class PixelTinkererMinigame : PixelVisitorMinigame
{
    [Header("Stock")]
    [Min(1)]
    [Tooltip("How many different devices he brings each visit (a random selection of the ones he could sell).")]
    [SerializeField] private int devicesOffered = 3;

    [Min(1)]
    [Tooltip("Fewest of each device he has.")]
    [SerializeField] private int stockMin = 1;

    [Min(1)]
    [Tooltip("Most of each device he has (so you can't stock up).")]
    [SerializeField] private int stockMax = 3;

    protected override int OfferedWares => devicesOffered;
    protected override int StockMin => stockMin;
    protected override int StockMax => stockMax;

    public override string Id => "tinkerer";
    public override string DisplayName => "Tinkerer";

    protected override bool IsWare(int item) => consumables != null && consumables.IsDevice(item) && !consumables.IsSeedItem(item) && !consumables.ItemVendorOnly(item) && !consumables.ItemSkillGated(item);
    protected override string DefaultName => "Tinkerer";
    protected override string DefaultGreeting => "Well hello there! I'm the Tinkerer. I've got gadgets for every occasion, and for you I'll knock {0}% off every one. Interested?";
    protected override string DefaultWaresTitle => "Tinkerer's Wares";
    protected override string SaleFormat => "{0}% off every device";
    protected override string DefaultArrivedLog => "The Tinkerer is here!";

    protected override void DrawVisitor(Transform art)
    {
        // A copper cube tinkerer: goggles over his eyes, an orange cap, a backpack of gears and a wrench.
        Color face = new Color(0.72f, 0.45f, 0.22f), top = new Color(0.9f, 0.66f, 0.4f), side = new Color(0.46f, 0.27f, 0.12f);
        Color dark = new Color(0.14f, 0.09f, 0.06f), metal = new Color(0.58f, 0.62f, 0.68f), yellow = new Color(1f, 0.82f, 0.25f), orange = new Color(0.92f, 0.5f, 0.12f);
        Transform a = art;
        // Backpack behind him.
        Block(a, "Pack", new Color(0.33f, 0.36f, 0.42f), new Vector2(90f, 150f), new Vector2(-130f, 190f));
        Block(a, "Antenna", metal, new Vector2(8f, 70f), new Vector2(-142f, 295f));
        Block(a, "Antenna Knob", yellow, new Vector2(20f, 20f), new Vector2(-142f, 336f), 45f);
        Block(a, "Gear 1", orange, new Vector2(34f, 34f), new Vector2(-132f, 215f), 45f);
        Block(a, "Gear 2", yellow, new Vector2(24f, 24f), new Vector2(-126f, 160f), 20f);
        CubeBody(a, face, top, side, dark, arms: false);
        Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
        // The wrench held up in his right hand.
        Block(a, "Wrench Handle", metal, new Vector2(14f, 130f), new Vector2(150f, 210f), -10f);
        Block(a, "Wrench Head", metal, new Vector2(46f, 26f), new Vector2(160f, 285f), -10f);
        Block(a, "Wrench Gap", dark, new Vector2(14f, 14f), new Vector2(161f, 292f), -10f);
        Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        // Goggles instead of plain eyes.
        Block(a, "Strap", dark, new Vector2(204f, 16f), new Vector2(0f, 206f));
        Block(a, "Lens L Frame", dark, new Vector2(66f, 62f), new Vector2(-42f, 206f));
        Block(a, "Lens R Frame", dark, new Vector2(66f, 62f), new Vector2(42f, 206f));
        Block(a, "Lens L", new Color(0.55f, 0.88f, 1f), new Vector2(52f, 48f), new Vector2(-42f, 206f));
        Block(a, "Lens R", new Color(0.55f, 0.88f, 1f), new Vector2(52f, 48f), new Vector2(42f, 206f));
        Block(a, "Pupil L", dark, new Vector2(14f, 20f), new Vector2(-40f, 204f));
        Block(a, "Pupil R", dark, new Vector2(14f, 20f), new Vector2(44f, 204f));
        Block(a, "Glint L", Color.white, new Vector2(10f, 10f), new Vector2(-54f, 218f));
        Block(a, "Glint R", Color.white, new Vector2(10f, 10f), new Vector2(30f, 218f));
        Block(a, "Mouth", dark, new Vector2(60f, 9f), new Vector2(-4f, 140f), 4f);
        Block(a, "Smirk", dark, new Vector2(10f, 16f), new Vector2(28f, 148f));
        // Orange cap.
        Block(a, "Cap Brim", orange, new Vector2(150f, 16f), new Vector2(0f, 304f));
        Block(a, "Cap Top", orange, new Vector2(110f, 40f), new Vector2(0f, 328f));
        Block(a, "Cap Bolt", yellow, new Vector2(18f, 18f), new Vector2(0f, 330f), 45f);
    }
}
