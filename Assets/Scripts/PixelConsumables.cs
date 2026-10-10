using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using static PixelInput;

/// <summary>
/// Consumables for Pixel Clicker: potions and placeable devices.
///
/// DEVICES: the Vacuum Device is bought in the shop like a potion. Right-click it in the inventory, move the
/// mouse (a cylinder follows it), and left-click the floor to place it. It then pulls in old pixels within its
/// radius and collects them again until its timer (shown above it) runs out. Right-click cancels placing.
///
/// Each potion belongs to one pixel type. Buy potions in the shop's Consumables tab, then right-click
/// them in the Inventory's Consumables tab to drink one. While a potion is active, ONLY that type of
/// pixel spawns (PixelClicker.SetForcedSpawnTier) until the duration runs out.
///
/// A potion for every pixel type is created automatically (and added to the list in the Editor so you
/// can edit it): the price (any pixel types and amounts), the duration, the name and the amount owned.
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelConsumables : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        PotionDrunk = null;
        DevicePlaced = null;
        HoveringDisarmed = false;
    }

    /// <summary>True while the mouse is over a placed device that is still waiting for its first click (PixelClicker then ignores clicks, so the click switches the device on instead).</summary>
    public static bool HoveringDisarmed { get; private set; }

    /// <summary>One kind of potion.</summary>
    [Serializable]
    public class Potion
    {
        [Tooltip("Name shown in the shop and the inventory.")]
        public string displayName = "Potion";

        [Tooltip("The pixel type this potion makes appear.")]
        public PixelClicker.PixelType type = PixelClicker.PixelType.White;

        [TextArea(1, 3)]
        [Tooltip("Shown in the shop. {pixel} = pixel type, {duration} = seconds.")]
        public string description = "Only {pixel} pixels appear for {duration} seconds.";

        [Tooltip("Price in the shop. Pick any pixel type and amount; all costs are paid together.")]
        public PixelShop.PackCost[] costs;

        [Min(1f)]
        [Tooltip("How many seconds the effect lasts after drinking.")]
        public float durationSeconds = 30f;

        [Min(0)]
        [Tooltip("How many of this potion you own. You can type a starting amount here for testing.")]
        public int owned = 0;

        [Min(0)]
        [Tooltip("Runtime: extra storage for this potion on top of the normal limit. Grows when you keep a ghost's potion in the backpack while already full, and stays: the potion can be bought back up to the higher limit. Saved.")]
        public int bonusCap = 0;

        [Tooltip("Runtime: a combo potion (made by crafting). It makes its pixel type AND the second type appear, is never sold in the shop, and is created automatically.")]
        public bool craftOnly = false;

        [Tooltip("Runtime: the second pixel type of a combo potion.")]
        public PixelClicker.PixelType secondType = PixelClicker.PixelType.White;

        [Tooltip("Runtime: a 3-type combo potion (a 2-type combo potion crafted with one more pixel).")]
        public bool hasThird = false;

        [Tooltip("Runtime: the third pixel type of a 3-type combo potion.")]
        public PixelClicker.PixelType thirdType = PixelClicker.PixelType.White;
    }

    /// <summary>What a placeable device does.</summary>
    public enum DeviceKind
    {
        /// <summary>Pulls old pixels in and collects them again.</summary>
        Vacuum = 0,
        /// <summary>Gently blows old pixels along a cone in front of it.</summary>
        Fan = 1,
        /// <summary>A ring around the cube with a pipe that spits out every collected pixel in a stream.</summary>
        Sorter = 2,
        /// <summary>Not placed: used automatically when the combo bar would run out (it keeps the combo and refills the bar).</summary>
        ComboFuel = 3,
        /// <summary>Not placed: right-click in the inventory to call a wave of slower ghosts sooner.</summary>
        GhostBait = 4,
        /// <summary>Not placed: right-click in the inventory to make your pets hyper and glowing for a while.</summary>
        PetTreat = 5,
        /// <summary>Placed: lasts a number of auto-clicker clicks; linked Electric pixels fill its meter, then a click starts a super charge.</summary>
        ChargeBooster = 6,
        /// <summary>Placed: lasts a number of lightning strikes; each strike turns the old pixels around it into Electric pixels.</summary>
        LightningRod = 7,
        /// <summary>Not a device: a seed of one pixel type. Right-click in the inventory, then click the floor to plant it; it grows that pixel type.</summary>
        Seed = 8,
    }

    /// <summary>A consumable object you place in the world (the Vacuum Device, the Fan).</summary>
    [Serializable]
    public class Device
    {
        [Tooltip("What this device does.")]
        public DeviceKind kind = DeviceKind.Vacuum;

        [Tooltip("Name shown in the shop and the inventory.")]
        public string displayName = "Vacuum Device";

        [TextArea(1, 3)]
        [Tooltip("Shown in the shop. {radius} = reach, {duration} = seconds.")]
        public string description = "Place it on the floor. It pulls in old pixels within {radius} units and collects them again for {duration} seconds.";

        [Tooltip("Listed in the shop once this pixel type is unlocked (or when you already own one).")]
        public PixelClicker.PixelType requiredType = PixelClicker.PixelType.Vacuum;

        [Tooltip("Price in the shop. Pick any pixel type and amount; all costs are paid together.")]
        public PixelShop.PackCost[] costs;

        [Min(0)]
        [Tooltip("The most of this item you can hold at once (0 = no limit). The shop and crafting stop at this number.")]
        public int maxHeld = 0;

        [Min(1f)]
        [Tooltip("How many seconds the device works once placed.")]
        public float durationSeconds = 30f;

        [Min(0.1f)]
        [Tooltip("Reach (world units). Vacuum: how far old pixels are pulled in from. Fan: how long the cone is.")]
        public float radius = 4f;

        [Min(0f)]
        [Tooltip("How strongly pixels are pulled toward the device (acceleration). Must beat gravity (about 10) to lift them.")]
        public float pullAcceleration = 30f;

        [Min(0.05f)]
        [Tooltip("A pixel this close to the top of the device is collected.")]
        public float absorbDistance = 0.7f;

        [Header("Fan only")]
        [Range(10f, 160f)]
        [Tooltip("Fan: opening angle of the cone (degrees, measured across the whole cone).")]
        public float coneAngle = 55f;

        [Min(0.5f)]
        [Tooltip("Fan: how high above the floor the cone reaches (world units).")]
        public float coneHeight = 2.5f;

        [Min(0f)]
        [Tooltip("Fan: how hard pixels are pushed along the cone (acceleration; friction needs about 6 to get things sliding).")]
        public float blowAcceleration = 10f;

        [Min(0f)]
        [Tooltip("Fan: extra upward push so pixels hop a little instead of only sliding.")]
        public float liftAcceleration = 3f;

        [Tooltip("Fan: how fast the blades spin (degrees per second).")]
        public float bladeSpinDegrees = 900f;

        [Header("Sorter only")]
        [Min(0f)]
        [Tooltip("Sorter: how much bigger than the cube the ring is (world units added to the cube's half-diagonal).")]
        public float sorterRingPadding = 0.2f;

        [Min(0.02f)]
        [Tooltip("Sorter: thickness of the ring (world units, across its band).")]
        public float sorterRingThickness = 0.18f;

        [Min(0.2f)]
        [Tooltip("Sorter: length of the output pipe (world units).")]
        public float sorterPipeLength = 0.55f;

        [Min(0.05f)]
        [Tooltip("Sorter: width of the output pipe (world units).")]
        public float sorterPipeDiameter = 0.12f;

        [Min(0.3f)]
        [Tooltip("Sorter: how far the bend cone reaches from the pipe's base (world units).")]
        public float sorterConeLength = 1.6f;

        [Min(0f)]
        [Tooltip("Sorter: how fast pixels leave the end of the pipe (world units per second).")]
        public float sorterExitSpeed = 7f;

        [Range(0f, 30f)]
        [Tooltip("Sorter: random wobble (degrees) of the stream so it isn't a perfectly straight line.")]
        public float sorterSpreadDegrees = 4f;

        [Range(5f, 90f)]
        [Tooltip("Sorter: half-angle (degrees) of the cone you click inside to bend the pipe. The pipe's end can turn this far either way.")]
        public float sorterBendConeDegrees = 45f;

        [Tooltip("Sorter: speed multipliers of the three force buttons on the ring (left to right: 1, 2, 3).")]
        public float[] sorterForceMultipliers = { 0.5f, 1f, 1.8f };

        [Min(0)]
        [Tooltip("Sorter: which force button is selected when the sorter is placed (0 = the first).")]
        public int sorterDefaultForce = 1;

        [Min(0f)]
        [Tooltip("Sorter: the lowest force the force slider can be set to (a multiplier of the exit speed). 0 = the coded default (0.2).")]
        public float sorterSliderMin = 0f;

        [Min(0f)]
        [Tooltip("Sorter: the highest force the force slider can be set to (a multiplier of the exit speed). 0 = the coded default (3).")]
        public float sorterSliderMax = 0f;

        [Min(0.1f)]
        [Tooltip("Sorter: size of the round force buttons on the ring (world units).")]
        public float sorterButtonSize = 0.3f;

        [Range(5f, 60f)]
        [Tooltip("Sorter: angle (degrees) between neighbouring force buttons around the ring.")]
        public float sorterButtonSpacing = 24f;

        [TextArea(1, 2)]
        [Tooltip("Sorter: message while bending the pipe (second placing step). {0} = device name.")]
        public string sorterBendMessage = "Click inside the cone to bend the {0}'s pipe  (right-click to go back)";

        [Header("Look and placing")]
        [TextArea(1, 2)]
        [Tooltip("Message while placing. {0} = device name. Empty = the default message from Pixel UI.")]
        public string placingMessage = "";

        [Tooltip("Colour of the cylinder.")]
        public Color color = new Color(0.65f, 0.3f, 0.95f, 1f);

        [Min(0.1f)]
        [Tooltip("Width of the cylinder (world units).")]
        public float bodyDiameter = 0.7f;

        [Min(0.1f)]
        [Tooltip("Height of the cylinder (world units).")]
        public float bodyHeight = 1f;

        [Header("Seed only")]
        [Tooltip("Seed: the pixel type the planted seed grows into. (The price is worked out from this pixel's payout and rarity, in that pixel.)")]
        public PixelClicker.PixelType seedType = PixelClicker.PixelType.White;

        [Header("Electric devices (Charge Booster / Lightning Rod)")]
        [Min(1)]
        [Tooltip("How many uses it lasts instead of a time: auto-clicker clicks for the Charge Booster, lightning strikes for the Lightning Rod.")]
        public int uses = 100;

        [Min(1f)]
        [Tooltip("Charge Booster: how many Electric pixels fill its meter.")]
        public float chargeCapacity = 10f;

        [Min(0.5f)]
        [Tooltip("Charge Booster: seconds the super charge lasts.")]
        public float superChargeSeconds = 6f;

        [Min(1f)]
        [Tooltip("Charge Booster: extra auto-clicker clicks per second during the super charge (they don't use up the booster).")]
        public float superClicksPerSecond = 12f;

        [Min(0.2f)]
        [Tooltip("Lightning Rod: seconds between strikes (a strike only happens when there are pixels in range to convert).")]
        public float strikeSeconds = 3f;

        [Min(0)]
        [Tooltip("How many of this device you own. You can type a starting amount here for testing.")]
        public int owned = 0;
    }

    [Header("References")]
    [Tooltip("The PixelClicker whose spawning the potions control. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Potions")]
    [Tooltip("All potions. One per pixel type is created for you; edit prices and durations here.")]
    [SerializeField] private Potion[] potions = CreateDefaultPotions();

    [Tooltip("Add a potion for any pixel type that has none (e.g. when this component was added before a type existed).")]
    [SerializeField] private bool addDefaultPotions = true;

    [Min(0)]
    [Tooltip("The most of each potion you can hold (crafted combo potions too). Stops stockpiling cheap potions early and drinking them after big upgrades. 0 = no limit. Devices are not limited.")]
    [SerializeField] private int maxPotionsHeld = 10;

    [Header("Devices")]
    [Min(0f)]
    [Tooltip("How many seconds a placed Vacuum Device works (it overrides that device's own duration). 0 = the coded default (5).")]
    [SerializeField] private float vacuumDeviceSeconds = 0f;

    [Tooltip("Text above a placed device that has not been switched on yet (devices start grey and idle until you left-click them once). Empty = 'Click to start'.")]
    [SerializeField] private string disarmedText = "";

    [Min(0f)]
    [Tooltip("Seconds a grey device takes to fade into colour when it is switched on. 0 = the coded default (0.7).")]
    [SerializeField] private float armFadeSeconds = 0f;

    [Tooltip("Placeable devices. A Vacuum Device is created for you; edit its price, radius and duration here.")]
    [SerializeField] private Device[] devices = CreateDefaultDevices();

    [Tooltip("Add the default Vacuum Device if the list has none (e.g. when this component was added before devices existed).")]
    [SerializeField] private bool addDefaultDevices = true;

    [Header("Prices")]
    [Tooltip("Potion and device prices paid in a pixel type are multiplied by that pixel's Value upgrade multiplier, so they stay worth the same as its payout grows. Off = the prices typed above, always.")]
    [SerializeField] private bool pricesScaleWithValue = true;

    [Tooltip("Potion prices are worked out from what the potion is expected to make (its duration x your clicks per second x that pixel's payout, Value and Ultra included) divided by 'Potion Target Return', paid in that pixel. The typed potion costs above are then ignored. Off = the typed costs (scaled by Value if that is on).")]
    [SerializeField] private bool potionPriceFromOutput = true;

    [Min(1f)]
    [Tooltip("How many times its price a potion should pay back (3 = a potion makes about 3x what it cost). Same for every potion, early or late, so chaining stays worthwhile but none is a jackpot.")]
    [SerializeField] private float potionTargetReturn = 3f;

    [Min(0f)]
    [Tooltip("Manual clicks per second assumed when pricing potions (added to the auto clicker's rate). Higher = pricier potions for players who don't click much.")]
    [SerializeField] private float assumedManualClicksPerSecond = 4f;

    [Min(0f)]
    [Tooltip("A potion never costs less than this.")]
    [SerializeField] private double potionMinPrice = 10;

    [Min(0f)]
    [Tooltip("Seed prices: a seed costs this many times its pixel's payout per harvest (x the pixel's Value multiplier), paid in that same pixel. The typed seed costs are ignored.")]
    [SerializeField] private float seedPriceFactor = 3f;

    [Min(0f)]
    [Tooltip("Rarer pixels cost more seeds: the price is also multiplied by (1 / spawn weight) to this power (0 = no rarity scaling).")]
    [SerializeField] private float seedRarityPricePower = 0.5f;

    [Min(1)]
    [Tooltip("A seed never costs less than this.")]
    [SerializeField] private double seedMinPrice = 3;

    [Min(1)]
    [Tooltip("The most of each seed you can hold (so seeds are plentiful).")]
    [SerializeField] private int seedMaxHeld = 200;

    [Header("Placing Devices")]
    [Tooltip("Layers the mouse can place a device on (the floor).")]
    [SerializeField] private LayerMask placementLayers = ~0;

    [Tooltip("If the mouse is not over any floor collider, the device is placed on a flat plane at this height instead.")]
    [SerializeField] private float fallbackFloorY = 0f;

    [Range(0f, 1f)]
    [Tooltip("Surfaces steeper than this can't be placed on (1 = only perfectly flat, 0 = anything).")]
    [SerializeField] private float minSurfaceNormalY = 0.5f;

    [Min(1f)]
    [Tooltip("Fan: degrees turned per mouse-wheel notch while placing.")]
    [SerializeField] private float rotateStepDegrees = 15f;

    [Min(1f)]
    [Tooltip("Fan: degrees per second turned while holding Q or E.")]
    [SerializeField] private float rotateSpeedDegrees = 120f;

    [Header("Removing")]
    [Min(0.1f)]
    [Tooltip("How long (seconds) you hold the right mouse button on a placed device to remove it. The same hold cancels the running potion.")]
    [SerializeField] private float removeHoldSeconds = 0.8f;

    [Tooltip("Text above the meter while removing a placed device. {0} = device name.")]
    [SerializeField] private string removeText = "Removing {0}...";

    [Tooltip("Text above the meter while cancelling the running potion. {0} = potion name.")]
    [SerializeField] private string cancelPotionText = "Cancelling {0}...";

    [Tooltip("Colour of the filling meter.")]
    [SerializeField] private Color removeMeterColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    [Range(0.05f, 1f)]
    [Tooltip("How see-through the cylinder is while you are choosing where to put it.")]
    [SerializeField] private float previewOpacity = 0.5f;

    [Tooltip("Show a flat disc on the floor marking the device's reach.")]
    [SerializeField] private bool showRange = true;

    [Range(0.02f, 1f)]
    [Tooltip("Opacity of the range disc.")]
    [SerializeField] private float rangeOpacity = 0.18f;

    [Header("Device Timer (above the cylinder)")]
    [Tooltip("Text above the device. {0} = seconds left.")]
    [SerializeField] private string timerFormat = "{0}s";

    [Min(0.1f)]
    [Tooltip("Timer text size (3D text: about 10 per world unit, so 4 is roughly 0.4 units tall).")]
    [SerializeField] private float timerFontSize = 4f;

    [Tooltip("Gap between the top of the cylinder and the timer (world units).")]
    [SerializeField] private float timerHeightAbove = 0.6f;

    [Tooltip("Timer text colour.")]
    [SerializeField] private Color timerColor = Color.white;

    [Min(0.05f)]
    [Tooltip("How long the device takes to shrink away when its time runs out.")]
    [SerializeField] private float shrinkSeconds = 0.4f;

    [Tooltip("Fired when a device is placed. Passes the device index.")]
    public UnityEvent<int> onDevicePlaced;

    [Header("Behaviour")]
    [Tooltip("Count the effect down with real time even when the game is paused (Time.timeScale = 0).")]
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Sound / Events")]
    [Tooltip("Sound played when a potion is drunk.")]
    [SerializeField] private AudioClip drinkSound;

    [Range(0f, 1f)]
    [Tooltip("Drink sound volume.")]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Fired when a potion is drunk. Passes the potion index.")]
    public UnityEvent<int> onPotionConsumed;

    [Tooltip("Fired when the active potion runs out.")]
    public UnityEvent onPotionExpired;

    private int activeIndex = -1;
    private float remaining;
    private AudioSource audioSource;

    /// <summary>Number of potion kinds.</summary>
    public int Count => potions.Length;

    public Potion Get(int index) => potions[index];

    /// <summary>True while a potion's effect is running.</summary>
    public bool IsActive => activeIndex >= 0;

    /// <summary>Index of the running potion (-1 = none).</summary>
    public int ActiveIndex => activeIndex;

    /// <summary>Seconds left on the running potion.</summary>
    public float Remaining => remaining;

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    private static Potion CreateDefaultPotion(PixelClicker.PixelType type)
    {
        double price;
        switch (type)
        {
            case PixelClicker.PixelType.Glass: price = 50; break;
            case PixelClicker.PixelType.Vacuum: price = 10; break;
            default: price = 100; break;
        }

        return new Potion
        {
            displayName = type + " Potion",
            type = type,
            costs = new[] { new PixelShop.PackCost { type = type, amount = price } },
        };
    }

    private static Device CreateDefaultDevice()
    {
        return new Device
        {
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass,  amount = 100 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Vacuum, amount = 10 },
            },
        };
    }

    private static Device CreateDefaultFan()
    {
        return new Device
        {
            kind = DeviceKind.Fan,
            displayName = "Fan",
            description = "Place it and turn it. It gently blows old pixels along a cone up to {radius} units long for {duration} seconds.",
            requiredType = PixelClicker.PixelType.Glass,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 150 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
            },
            radius = 6f,
            color = new Color(0.4f, 0.85f, 1f, 1f),
            bodyDiameter = 0.9f,
            bodyHeight = 0.9f,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultSorter()
    {
        return new Device
        {
            kind = DeviceKind.Sorter,
            displayName = "Sorter",
            description = "Wraps around the cube. For {duration} seconds every pixel you collect is spat out of its pipe in a stream. Scroll to aim the pipe.",
            requiredType = PixelClicker.PixelType.Glass,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 200 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 300 },
            },
            durationSeconds = 45f,
            color = new Color(1f, 0.7f, 0.25f, 1f),
            placingMessage = "Scroll or Q / E to aim the {0}'s pipe, click to confirm  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultComboFuel()
    {
        return new Device
        {
            kind = DeviceKind.ComboFuel,
            displayName = "Combo Fuel",
            description = "Hold up to 3. When your combo bar would run out, one is used automatically: the combo is kept and the bar slowly refills instead. Click before it is full again to carry on.",
            requiredType = PixelClicker.PixelType.Red,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red,   amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue,  amount = 250 },
            },
            maxHeld = 3,
            color = new Color(0.35f, 0.8f, 1f, 1f),
        };
    }

    private static Device CreateDefaultGhostBait()
    {
        return new Device
        {
            kind = DeviceKind.GhostBait,
            displayName = "Ghost Bait",
            description = "Right-click to use (needs Ghost Hunt). The next ghost appears sooner, and three ghosts float by, slower than usual. You can only hold one.",
            requiredType = PixelClicker.PixelType.Glass,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 500 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 500 },
            },
            maxHeld = 1,
            color = new Color(0.8f, 0.85f, 1f, 1f),
        };
    }

    private static Device CreateDefaultPetTreat()
    {
        return new Device
        {
            kind = DeviceKind.PetTreat,
            displayName = "Pet Treat",
            description = "Right-click to use. Your pets go hyper and glow for a minute, and the extra spawn chance they give is doubled. Hold up to 5.",
            requiredType = PixelClicker.PixelType.Black,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 2000 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray,  amount = 1000 },
            },
            maxHeld = 5,
            color = new Color(1f, 0.85f, 0.4f, 1f),
        };
    }

    private static Device CreateDefaultChargeBooster()
    {
        return new Device
        {
            kind = DeviceKind.ChargeBooster,
            displayName = "Charge Booster",
            description = "Place it. Electric pixels that link to it fill its meter and are used up. When the meter is full, click it for a few seconds of super-charged auto clicking. Dissolves after 100 auto clicks.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 200 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 100 },
            },
            uses = 100, chargeCapacity = 10f, superChargeSeconds = 6f, superClicksPerSecond = 12f,
            radius = 3f,
            color = new Color(1f, 0.9f, 0.3f, 1f),
            bodyDiameter = 0.8f,
            bodyHeight = 0.8f,
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultLightningRod()
    {
        return new Device
        {
            kind = DeviceKind.LightningRod,
            displayName = "Lightning Rod",
            description = "Place it. Every few seconds lightning strikes it and turns every old pixel around it into an Electric pixel. Dissolves after 8 strikes.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Obsidian, amount = 50 },
            },
            uses = 8, strikeSeconds = 3f,
            radius = 5f,
            color = new Color(0.6f, 0.75f, 1f, 1f),
            bodyDiameter = 0.12f,
            bodyHeight = 2.5f,
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    /// <summary>True for the pixel types that have a seed: not rare drops, not fly-away types (they never land) and not the Seed pixel itself.</summary>
    private static bool HasSeed(PixelClicker.PixelType type) =>
        !PixelClicker.IsDragonCube(type) && type != PixelClicker.PixelType.Seed && type != PixelClicker.PixelType.Meteor;

    /// <summary>The Dragon Seed: a rare seed only the Farmer offers; it grows a Dragon Cube the player doesn't hold (marked by seedType = DragonCube1).</summary>
    private Device CreateDefaultDragonSeed()
    {
        return new Device
        {
            kind = DeviceKind.Seed,
            seedType = PixelClicker.PixelType.DragonCube1,
            displayName = "Dragon Seed",
            description = "A glowing seed. Plant it: it sprouts into a Dragon Cube you don't have yet.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 500000 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 250000 },
            },
            maxHeld = 3,
            color = new Color(1f, 0.85f, 0.2f, 1f),
            placingMessage = "Click the floor to plant the {0}  (right-click to stop)",
        };
    }

    private Device CreateDefaultSeed(PixelClicker.PixelType type)
    {
        return new Device
        {
            kind = DeviceKind.Seed,
            seedType = type,
            displayName = type + " Seed",
            description = "Right-click it in the inventory, then click the floor to plant it. It sprouts and grows one " + type + " pixel.",
            requiredType = type,
            costs = new PixelShop.PackCost[0],
            maxHeld = seedMaxHeld,
            color = new Color(0.45f, 0.85f, 0.35f, 1f),
            placingMessage = "Click the floor to plant a {0}  (right-click to stop)",
        };
    }

    private static Device[] CreateDefaultDevices() => new[]
    {
        CreateDefaultDevice(), CreateDefaultFan(), CreateDefaultSorter(),
        CreateDefaultComboFuel(), CreateDefaultGhostBait(), CreateDefaultPetTreat(),
        CreateDefaultChargeBooster(), CreateDefaultLightningRod(),
    };

    private static Potion[] CreateDefaultPotions()
    {
        System.Collections.Generic.List<Potion> list = new System.Collections.Generic.List<Potion>();
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            if (!PixelClicker.IsDragonCube(type)) list.Add(CreateDefaultPotion(type)); // no potions for the Dragon Cubes
        return list.ToArray();
    }

    /// <summary>Adds a potion for every pixel type that doesn't have one. Returns true if anything was added.</summary>
    private bool EnsureDefaultPotions()
    {
        bool added = false;

        if (addDefaultDevices)
        {
            // The sorter's pipe used to be 3x bigger; move untouched old values to the new size.
            if (devices != null)
                foreach (Device d in devices)
                    if (d != null && d.kind == DeviceKind.Sorter && Mathf.Approximately(d.sorterPipeLength, 1.6f) &&
                        Mathf.Approximately(d.sorterPipeDiameter, 0.35f))
                    {
                        d.sorterPipeLength = 0.55f;
                        d.sorterPipeDiameter = 0.12f;
                        added = true;
                    }

            if (devices == null || devices.Length == 0)
            {
                devices = CreateDefaultDevices();
                added = true;
            }
            else
            {
                if (!Array.Exists(devices, d => d != null && d.kind == DeviceKind.Fan))
                {
                    Array.Resize(ref devices, devices.Length + 1);
                    devices[devices.Length - 1] = CreateDefaultFan();
                    added = true;
                }
                if (!Array.Exists(devices, d => d != null && d.kind == DeviceKind.Sorter))
                {
                    Array.Resize(ref devices, devices.Length + 1);
                    devices[devices.Length - 1] = CreateDefaultSorter();
                    added = true;
                }
                foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> extra in new[]
                {
                    new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ComboFuel, CreateDefaultComboFuel),
                    new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.GhostBait, CreateDefaultGhostBait),
                    new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PetTreat, CreateDefaultPetTreat),
                    new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ChargeBooster, CreateDefaultChargeBooster),
                    new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.LightningRod, CreateDefaultLightningRod),
                })
                {
                    DeviceKind wanted = extra.Key;
                    if (Array.Exists(devices, d => d != null && d.kind == wanted)) continue;
                    Array.Resize(ref devices, devices.Length + 1);
                    devices[devices.Length - 1] = extra.Value();
                    added = true;
                }
            }
        }

        if (addDefaultDevices && devices != null && !Array.Exists(devices, d => d != null && d.kind == DeviceKind.Seed && PixelClicker.IsDragonCube(d.seedType)))
        {
            Array.Resize(ref devices, devices.Length + 1);
            devices[devices.Length - 1] = CreateDefaultDragonSeed();
            added = true;
        }

        if (addDefaultDevices && devices != null)
        {
            // One seed per pixel type that can be grown (appended at the end so placed-device indexes stay valid).
            foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            {
                if (!HasSeed(type)) continue;
                if (Array.Exists(devices, d => d != null && d.kind == DeviceKind.Seed && d.seedType == type)) continue;
                Array.Resize(ref devices, devices.Length + 1);
                devices[devices.Length - 1] = CreateDefaultSeed(type);
                added = true;
            }
        }

        if (!addDefaultPotions) return added;
        if (potions == null) potions = new Potion[0];

        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (PixelClicker.IsDragonCube(type)) continue;
            if (Array.Exists(potions, p => p != null && p.type == type)) continue;
            Array.Resize(ref potions, potions.Length + 1);
            potions[potions.Length - 1] = CreateDefaultPotion(type);
            added = true;
        }
        return added;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: serialized data must not be changed from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultPotions()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        // Escape cancels placing a device before it closes anything else.
        PixelWindows.Register(this, 100, () => IsPlacing, CancelPlacement);

        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }

        if (clicker == null)
        {
            Debug.LogError("PixelConsumables: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        EnsureDefaultPotions();
        AppendComboPotions();
    }

    /// <summary>
    /// Adds the craft-only combo potions: one for every PAIR of pixel types and one for every TRIPLE (a 2-type potion crafted with one more
    /// pixel). Vacuum is the exception: it can't be part of a combo. They exist only at runtime (not in the Inspector list), are never sold,
    /// and are saved by their types (only the ones you hold). All of them are made here, once, so item indexes never change afterwards.
    /// </summary>
    private void AppendComboPotions()
    {
        System.Collections.Generic.List<Potion> bases = new System.Collections.Generic.List<Potion>();
        foreach (Potion p in potions) if (p != null && !p.craftOnly && p.type != PixelClicker.PixelType.Vacuum) bases.Add(p);
        bases.Sort((x, y) => ((int)x.type).CompareTo((int)y.type));

        System.Collections.Generic.List<Potion> all = new System.Collections.Generic.List<Potion>(potions);
        comboLookup.Clear();
        for (int i = 0; i < potions.Length; i++)
            if (potions[i] != null && potions[i].craftOnly) comboLookup[ComboMask(potions[i])] = i;

        for (int i = 0; i < bases.Count; i++)
        for (int j = i + 1; j < bases.Count; j++)
        {
            Potion a = bases[i], b = bases[j];
            long pairMask = Bit(a.type) | Bit(b.type);
            if (comboLookup.ContainsKey(pairMask)) continue;
            comboLookup[pairMask] = all.Count;
            all.Add(new Potion
            {
                displayName = a.type + " + " + b.type + " Potion",
                type = a.type,
                secondType = b.type,
                craftOnly = true,
                description = "Only {pixel} and {pixel2} pixels appear for {duration} seconds. Made by crafting.",
                costs = new PixelShop.PackCost[0],
                durationSeconds = (a.durationSeconds + b.durationSeconds) * 0.5f,
            });

            for (int k = j + 1; k < bases.Count; k++)
            {
                Potion c = bases[k];
                long tripleMask = pairMask | Bit(c.type);
                if (comboLookup.ContainsKey(tripleMask)) continue;
                comboLookup[tripleMask] = all.Count;
                all.Add(new Potion
                {
                    displayName = a.type + " + " + b.type + " + " + c.type + " Potion",
                    type = a.type,
                    secondType = b.type,
                    hasThird = true,
                    thirdType = c.type,
                    craftOnly = true,
                    description = "Only {pixel}, {pixel2} and {pixel3} pixels appear for {duration} seconds. Made by crafting.",
                    costs = new PixelShop.PackCost[0],
                    durationSeconds = (a.durationSeconds + b.durationSeconds + c.durationSeconds) / 3f,
                });
            }
        }
        potions = all.ToArray();
    }

    private readonly System.Collections.Generic.Dictionary<long, int> comboLookup = new System.Collections.Generic.Dictionary<long, int>();
    private static long Bit(PixelClicker.PixelType type) => 1L << (int)type;
    private static long ComboMask(Potion p) => Bit(p.type) | Bit(p.secondType) | (p.hasThird ? Bit(p.thirdType) : 0L);

    /// <summary>Index of the 2-type combo potion for these two pixel types (either order), or -1.</summary>
    public int FindComboPotion(PixelClicker.PixelType a, PixelClicker.PixelType b)
        => a != b && comboLookup.TryGetValue(Bit(a) | Bit(b), out int index) ? index : -1;

    /// <summary>Index of the 3-type combo potion for these three pixel types (any order), or -1.</summary>
    public int FindComboPotion(PixelClicker.PixelType a, PixelClicker.PixelType b, PixelClicker.PixelType c)
        => a != b && b != c && a != c && comboLookup.TryGetValue(Bit(a) | Bit(b) | Bit(c), out int index) ? index : -1;

    /// <summary>True for a combo potion (craft-only, never in the shop).</summary>
    public bool ItemCraftOnly(int item) => !IsDevice(item) && potions[item].craftOnly;

    /// <summary>The second pixel type of a combo potion.</summary>
    public PixelClicker.PixelType ItemSecondType(int item) => potions[item].secondType;

    /// <summary>True for a 3-type combo potion.</summary>
    public bool ItemHasThird(int item) => !IsDevice(item) && potions[item].hasThird;

    /// <summary>The third pixel type of a 3-type combo potion.</summary>
    public PixelClicker.PixelType ItemThirdType(int item) => potions[item].thirdType;

    /// <summary>Seconds of holding the right mouse button needed to remove a device / cancel a potion.</summary>
    public float RemoveHoldSeconds => removeHoldSeconds;

    /// <summary>Colour of the hold meter.</summary>
    public Color RemoveMeterColor => removeMeterColor;

    /// <summary>Label of the hold meter while cancelling a potion.</summary>
    public string CancelPotionText => cancelPotionText;

    /// <summary>Ends the running potion on purpose (no refund).</summary>
    public void CancelActive()
    {
        if (activeIndex < 0) return;
        StopActive();
        PixelAudio.Play("device_remove");
    }

    private int placeEndFrame = -1;

    /// <summary>Hold the right mouse button on a placed device to remove it.</summary>
    private void UpdateRemoval()
    {
        PixelPlacedDevice target = null;
        if (!IsPlacing && !PixelBank.HoseOn && !PixelClicker.GodMode && Time.frameCount != placeEndFrame && (RightPressed() || RightHeld()) && !PointerOverUI())
            target = DeviceUnderPointer();

        string text = target != null ? string.Format(removeText, target.name) : "";
        if (PixelHold.Update(this, target, removeHoldSeconds, text, removeMeterColor))
        {
            target.RemoveNow();
            PixelAudio.Play("device_remove");
        }
    }

    /// <summary>The placed device under the mouse (nearest first). Looks at the device's visible parts, not its range markings. 'onlyDisarmed' = only devices still waiting for their first click.</summary>
    private PixelPlacedDevice DeviceUnderPointer(bool onlyDisarmed = false)
    {
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(PointerPosition());

        PixelPlacedDevice best = null;
        float bestDistance = float.MaxValue;
        foreach (PixelPlacedDevice device in PixelPlacedDevice.All)
        {
            if (device == null || device.IsRemoving || (onlyDisarmed && device.Armed)) continue;
            if (device.HitTest(ray, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                best = device;
            }
        }
        return best;
    }

    /// <summary>A left click on a grey (not yet switched on) device switches it on. While the mouse is over one, clicks don't reach the cube.</summary>
    private void UpdateArming()
    {
        HoveringDisarmed = false;
        if (IsPlacing || PixelBank.HoseOn || PixelClicker.GodMode || PixelFirstPerson.Active || PixelPauseMenu.IsPaused || Time.timeScale <= 0f || PointerOverUI()) return;
        PixelPlacedDevice target = DeviceUnderPointer(true);
        if (target == null) return;
        HoveringDisarmed = true;
        if (LeftPressed() && Time.frameCount != placeEndFrame) target.Arm();
    }

    private void Update()
    {
        if (IsPlacing) UpdatePlacement();
        UpdateRemoval();
        UpdateArming();

        if (activeIndex < 0) return;

        remaining -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (remaining <= 0f) Expire();
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (preview != null) Destroy(preview);
        if (IsPlacing && clicker != null) clicker.SetClicksBlocked(false);
        if (activeIndex >= 0 && clicker != null) clicker.ClearForcedSpawnTier();
    }

    // ------------------------------------------------------------------
    // Inventory
    // ------------------------------------------------------------------

    /// <summary>Gives the player potions (e.g. from a purchase), never beyond <see cref="maxPotionsHeld"/>.</summary>
    public void Add(int index, int amount = 1)
    {
        if (index < 0 || index >= potions.Length || amount <= 0) return;
        potions[index].owned = ClampHeld(index, potions[index].owned + amount);
    }

    /// <summary>
    /// A potion kept from the ghost minigame (grabbed and dropped in the backpack). It always fits: when the potion is already
    /// at its limit the limit for that potion grows by one (and stays), so a full stock can go past the normal cap.
    /// </summary>
    public void AddKept(int index)
    {
        if (index < 0 || index >= potions.Length) return;
        if (maxPotionsHeld > 0 && potions[index].owned >= ItemCapacity(index)) potions[index].bonusCap++;
        potions[index].owned++;
    }

    /// <summary>The most of an item you can hold right now (the normal limit plus a potion's kept-potion bonus; devices: no limit).</summary>
    public int ItemCapacity(int item)
    {
        if (IsDevice(item))
        {
            int held = devices[item - potions.Length].maxHeld; // Combo Fuel 3, Ghost Bait 1, Pet Treat 5, placeable devices unlimited
            return held > 0 ? held : int.MaxValue;
        }
        if (maxPotionsHeld <= 0) return int.MaxValue;
        return maxPotionsHeld + Mathf.Max(0, potions[item].bonusCap);
    }

    /// <summary>A device count cut down to its hold limit (used when loading a save).</summary>
    public int ClampDeviceHeld(int deviceIndex, int owned)
    {
        int cap = ItemCapacity(potions.Length + deviceIndex);
        return Mathf.Clamp(owned, 0, cap);
    }

    /// <summary>The most of each potion you can hold (0 = no limit).</summary>
    public int MaxPotionsHeld => maxPotionsHeld;

    /// <summary>A potion count cut down to the limit (used when loading a save).</summary>
    public int ClampHeld(int index, int owned) => maxPotionsHeld > 0 ? Mathf.Clamp(owned, 0, ItemCapacity(index)) : Mathf.Max(0, owned);

    /// <summary>How many more of an item you can hold right now (devices: no limit).</summary>
    public int ItemRoom(int item)
    {
        if (Inf) return int.MaxValue;
        int cap = ItemCapacity(item);
        if (cap == int.MaxValue) return int.MaxValue;
        return Mathf.Max(0, cap - ItemOwned(item));
    }

    /// <summary>
    /// Drinks one potion: only its pixel type spawns for its duration. Drinking another potion replaces the
    /// running one. Returns false if you have none or its pixel type isn't unlocked.
    /// </summary>
    /// <summary>Raised when the player drinks a potion (its name). Used by the stats.</summary>
    public static event Action<string> PotionDrunk;

    /// <summary>Raised when the player places a device. Used by the stats.</summary>
    public static event Action<DeviceKind> DevicePlaced;

    public bool TryConsume(int index)
    {
        if (index < 0 || index >= potions.Length) return false;

        Potion potion = potions[index];
        if ((!Inf && potion.owned <= 0) || !clicker.IsUnlocked(potion.type)) return false;
        if (potion.craftOnly && (!clicker.IsUnlocked(potion.secondType) || (potion.hasThird && !clicker.IsUnlocked(potion.thirdType)))) return false;

        if (!Inf) potion.owned--;
        ApplyPotion(index, 1f);
        PotionDrunk?.Invoke(ItemName(index));
        return true;
    }

    /// <summary>Starts a potion's effect (without using one up).</summary>
    private void ApplyPotion(int index, float durationMultiplier)
    {
        Potion potion = potions[index];
        activeIndex = index;
        remaining = potion.durationSeconds * durationMultiplier;
        if (potion.craftOnly) clicker.SetForcedSpawnTiers(potion.type, potion.secondType, potion.hasThird ? potion.thirdType : (PixelClicker.PixelType?)null);
        else clicker.SetForcedSpawnTier(potion.type);

        if (drinkSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(drinkSound, soundVolume);
        }

        onPotionConsumed?.Invoke(index);
    }

    /// <summary>
    /// A free buff: starts a random potion whose pixel type is unlocked (used by the ghost minigame).
    /// Returns the potion's index, or -1 if none can be given.
    /// </summary>
    public int GrantRandomBuff(float durationMultiplier, out string potionName)
    {
        potionName = "";
        System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < potions.Length; i++)
            if (!potions[i].craftOnly && clicker.IsUnlocked(potions[i].type)) candidates.Add(i);
        if (candidates.Count == 0) return -1;

        int pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        potionName = potions[pick].displayName;
        ApplyPotion(pick, durationMultiplier);
        return pick;
    }

    /// <summary>Picks a random buff potion (unlocked pixel type, not craft-only) without starting it. -1 if none.</summary>
    public int PickRandomBuff()
    {
        System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < potions.Length; i++)
            if (!potions[i].craftOnly && clicker.IsUnlocked(potions[i].type)) candidates.Add(i);
        return candidates.Count == 0 ? -1 : candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>Name of a potion by index.</summary>
    public string PotionName(int index) => potions[index].displayName;

    /// <summary>Pixel type of a potion by index.</summary>
    public PixelClicker.PixelType PotionType(int index) => potions[index].type;

    /// <summary>Starts a potion's effect for free (used by the ghost minigame when the dropped potion shatters).</summary>
    public void ApplyBuff(int index, float durationMultiplier)
    {
        if (index >= 0 && index < potions.Length) ApplyPotion(index, durationMultiplier);
    }

    /// <summary>Ends the running potion without firing the expired event (used when loading a save).</summary>
    public void StopActive()
    {
        activeIndex = -1;
        remaining = 0f;
        if (clicker != null) clicker.ClearForcedSpawnTier();
    }

    private void Expire()
    {
        activeIndex = -1;
        remaining = 0f;
        clicker.ClearForcedSpawnTier();
        onPotionExpired?.Invoke();
    }

    // ------------------------------------------------------------------
    // Items: potions first, then devices (one list for the shop and the inventory)
    // ------------------------------------------------------------------

    /// <summary>Potions + devices.</summary>
    public int ItemCount => potions.Length + devices.Length;

    public bool IsDevice(int item) => item >= potions.Length;

    public string ItemName(int item) => IsDevice(item) ? devices[item - potions.Length].displayName : potions[item].displayName;

    /// <summary>True while the dev tools' "Infinite resources" is on: every consumable counts as plentiful and using one costs nothing.</summary>
    private static bool Inf => PixelClicker.InfiniteResources;

    public int ItemOwned(int item) => Inf ? 99999 : IsDevice(item) ? devices[item - potions.Length].owned : potions[item].owned;

    private PixelAutoClicker autoClicker;

    /// <summary>Clicks per second used for potion prices: the auto clicker's rate (once bought) plus the assumed manual rate.</summary>
    public double ExpectedClicksPerSecond()
    {
        if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
        // Running = bought (even if switched off in Toggles), so switching it off can't make potions cheaper.
        double auto = autoClicker != null && autoClicker.Running && autoClicker.Interval > 0f
            ? autoClicker.ClicksPerTick / (double)autoClicker.Interval
            : 0d;
        return auto + assumedManualClicksPerSecond;
    }

    /// <summary>
    /// What a potion of this pixel type is expected to make over 'seconds': clicks x payout per click (Value, Ultra and
    /// multi-click included; tough pixels pay once per several clicks). A Vacuum potion also counts the old pixels its
    /// every-Nth-click vacuum re-collects.
    /// </summary>
    public double ExpectedPotionOutput(PixelClicker.PixelType type, float seconds)
    {
        if (clicker == null) return 0d;
        int t = clicker.IndexOf(type);
        if (t < 0) return 0d;

        PixelClicker.PixelTier tier = clicker.Tiers[t];
        double cps = ExpectedClicksPerSecond();
        double perClick = tier.amountPerClick * clicker.PayoutMultiplier(t) * clicker.ClickMultiplier / System.Math.Max(1, tier.clicksToCollect);
        double output = seconds * cps * perClick;
        if (tier.vacuum)
        {
            double onFloor = System.Math.Min(clicker.OldPixelCap, cps * clicker.FallingCopyLifetime);
            output *= 1d + onFloor / clicker.PotionVacuumEvery;
        }
        return output;
    }

    /// <summary>Price (in its own pixel) of a potion of this type lasting 'seconds', from its expected output.</summary>
    public double PotionPriceFromOutput(PixelClicker.PixelType type, float seconds) =>
        System.Math.Max(potionMinPrice, System.Math.Ceiling(ExpectedPotionOutput(type, seconds) / System.Math.Max(1f, potionTargetReturn)));

    /// <summary>Current shop price of the (buyable) potion of a pixel type, in that pixel. Used by Crafting.</summary>
    public double PotionShopPrice(PixelClicker.PixelType type)
    {
        for (int i = 0; i < potions.Length; i++)
        {
            Potion p = potions[i];
            if (p == null || p.craftOnly || p.type != type) continue;
            PixelShop.PackCost[] costs = ItemCosts(i);
            if (costs != null)
                foreach (PixelShop.PackCost c in costs)
                    if (c != null && string.IsNullOrEmpty(c.minigameCurrency) && c.type == type) return c.amount;
            break;
        }
        return PotionPriceFromOutput(type, 30f);
    }

    /// <summary>
    /// What one of an item costs right now. Potions: from their expected output (see <see cref="potionPriceFromOutput"/>).
    /// Devices (and potions with that off): the typed prices, scaled by each pixel's Value multiplier when that is on.
    /// </summary>
    public PixelShop.PackCost[] ItemCosts(int item)
    {
        if (IsDevice(item) && devices[item - potions.Length].kind == DeviceKind.Seed && clicker != null && !IsDragonSeed(item))
        {
            int t = clicker.IndexOf(devices[item - potions.Length].seedType);
            if (t < 0) return new PixelShop.PackCost[0];
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            double rarity = seedRarityPricePower > 0f ? System.Math.Pow(1d / System.Math.Max(0.02d, tier.spawnWeight), seedRarityPricePower) : 1d;
            rarity = System.Math.Max(1d, rarity);
            double price = tier.amountPerClick * seedPriceFactor * rarity * (pricesScaleWithValue ? clicker.ValueMultiplier(t) : 1d);
            return new[] { new PixelShop.PackCost { type = tier.type, amount = System.Math.Max(seedMinPrice, System.Math.Ceiling(price)) } };
        }

        if (!IsDevice(item) && potionPriceFromOutput && clicker != null && !potions[item].craftOnly)
        {
            Potion p = potions[item];
            return new[] { new PixelShop.PackCost { type = p.type, amount = PotionPriceFromOutput(p.type, p.durationSeconds) } };
        }

        PixelShop.PackCost[] baseCosts = IsDevice(item) ? devices[item - potions.Length].costs : potions[item].costs;
        if (!pricesScaleWithValue || clicker == null || baseCosts == null) return baseCosts;

        PixelShop.PackCost[] scaled = new PixelShop.PackCost[baseCosts.Length];
        for (int i = 0; i < baseCosts.Length; i++)
        {
            PixelShop.PackCost c = baseCosts[i];
            if (c == null) continue;
            double factor = string.IsNullOrEmpty(c.minigameCurrency) ? clicker.ValueMultiplier(clicker.IndexOf(c.type)) : 1d;
            scaled[i] = new PixelShop.PackCost { type = c.type, amount = System.Math.Ceiling(c.amount * factor), minigameCurrency = c.minigameCurrency };
        }
        return scaled;
    }

    /// <summary>The pixel type that must be unlocked before the item is listed in the shop.</summary>
    public PixelClicker.PixelType ItemRequiredType(int item) =>
        IsDevice(item) ? devices[item - potions.Length].requiredType : potions[item].type;

    /// <summary>How long a placed device lasts: the Vacuum Device uses <see cref="vacuumDeviceSeconds"/> (default 5), the others their own setting.</summary>
    private float DeviceSeconds(Device d) => d.kind == DeviceKind.Vacuum ? (vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f) : d.durationSeconds;

    public string ItemDescription(int item)
    {
        if (!IsDevice(item)) return Describe(item);

        Device d = devices[item - potions.Length];
        return (d.description ?? "")
            .Replace("{radius}", d.radius.ToString("0.##"))
            .Replace("{duration}", DeviceSeconds(d).ToString("0.##"));
    }

    /// <summary>Adds items to the inventory (a shop purchase).</summary>
    public void AddItem(int item, int amount = 1)
    {
        if (IsDevice(item))
        {
            if (amount > 0) devices[item - potions.Length].owned = ClampDeviceHeld(item - potions.Length, devices[item - potions.Length].owned + amount);
        }
        else
        {
            Add(item, amount);
        }
    }

    /// <summary>The kind of device an item index refers to (items past the potions are devices).</summary>
    public DeviceKind DeviceKindOf(int item) => devices[item - potions.Length].kind;

    /// <summary>True for a seed item (a "device" of kind Seed: planted, not placed).</summary>
    public bool IsSeedItem(int item) => IsDevice(item) && devices[item - potions.Length].kind == DeviceKind.Seed;

    /// <summary>True for the rare Dragon Seed (typed prices, offered only by the Farmer, grows a Dragon Cube the player lacks).</summary>
    public bool IsDragonSeed(int item) => IsSeedItem(item) && PixelClicker.IsDragonCube(devices[item - potions.Length].seedType);

    /// <summary>The pixel type a seed item grows.</summary>
    public PixelClicker.PixelType SeedTypeOf(int item) => devices[item - potions.Length].seedType;

    /// <summary>Takes items out of the inventory (crafting). Returns false if you don't have that many.</summary>
    public bool TryRemoveItem(int item, int amount)
    {
        if (item < 0 || item >= ItemCount || amount <= 0 || ItemOwned(item) < amount) return false;
        if (Inf) return true;
        if (IsDevice(item)) devices[item - potions.Length].owned -= amount;
        else potions[item].owned -= amount;
        return true;
    }

    /// <summary>Right-click use: drinks a potion, or starts placing a device.</summary>
    public bool TryUseItem(int item)
    {
        if (!IsDevice(item)) return TryConsume(item);
        int index = item - potions.Length;
        switch (devices[index].kind)
        {
            case DeviceKind.ComboFuel:
                PixelHints.Announce("Combo Fuel is used by itself when your combo bar runs out");
                return false;
            case DeviceKind.GhostBait:
            {
                PixelGhostMinigame ghosts = PixelFind.First<PixelGhostMinigame>();
                if (ghosts == null || !ghosts.UseBait()) { PixelHints.Announce("Ghost Bait needs the Ghost Hunt running"); return false; }
                if (!Inf) devices[index].owned--;
                PixelStats.Count("bait.used");
                PixelHints.Announce("Ghost Bait set out");
                return true;
            }
            case DeviceKind.PetTreat:
            {
                if (PixelPets.Instance == null || !PixelPets.Instance.TryUseTreat()) { PixelHints.Announce("Pet Treat needs a pet that is out"); return false; }
                if (!Inf) devices[index].owned--;
                PixelStats.Count("treat.used");
                PixelHints.Announce("Your pets are hyper!");
                return true;
            }
            default: // placeable devices, and seeds (planted with a click on the floor)
                return BeginPlacement(index);
        }
    }

    /// <summary>
    /// Uses one held item of this kind without the player doing anything (Combo Fuel when the combo bar runs out).
    /// Returns false when none is held.
    /// </summary>
    public bool TryUseAutoItem(DeviceKind kind)
    {
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i] == null || devices[i].kind != kind || (!Inf && devices[i].owned <= 0)) continue;
            if (!Inf) devices[i].owned--;
            return true;
        }
        return false;
    }

    // Device access for the save system.
    public int DeviceCount => devices.Length;

    public Device GetDevice(int index) => devices[index];

    // ------------------------------------------------------------------
    // Placing devices
    // ------------------------------------------------------------------

    private int placingIndex = -1;
    private int placeStartFrame;
    private GameObject preview;

    /// <summary>True while the player is choosing where to put a device.</summary>
    public bool IsPlacing => placingIndex >= 0;

    /// <summary>Name of the device being placed (empty when not placing).</summary>
    public string PlacingName => IsPlacing ? devices[placingIndex].displayName : "";

    /// <summary>The device's own placing message (empty = use the default one).</summary>
    public string PlacingMessage => !IsPlacing ? ""
        : PixelKeys.Replace(devices[placingIndex].kind == DeviceKind.Sorter && sorterPhase == 1 ? devices[placingIndex].sorterBendMessage
        : devices[placingIndex].placingMessage);

    private float placingYaw;
    private PixelSorterDevice.Parts previewSorter;
    private int sorterPhase;      // 0 = turning the pipe, 1 = bending it
    private float sorterBend;

    /// <summary>Starts placing a device: a cylinder follows the mouse until you click the floor. Returns false if you own none.</summary>
    public bool BeginPlacement(int deviceIndex)
    {
        if (IsPlacing || deviceIndex < 0 || deviceIndex >= devices.Length) return false;
        if (!Inf && devices[deviceIndex].owned <= 0) return false;

        placingIndex = deviceIndex;
        placeStartFrame = Time.frameCount;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        placingYaw = cam != null ? cam.transform.eulerAngles.y : 0f; // a fan starts out blowing away from the camera
        previewSorter = null;
        if (devices[deviceIndex].kind == DeviceKind.Sorter)
        {
            placingYaw = 0f; // for the sorter this is the pipe's angle around the ring (0 = pointing right on screen)
            sorterPhase = 0;
            sorterBend = 0f;
            previewSorter = PixelSorterDevice.Parts.Create(clicker, devices[deviceIndex], cam, true, previewOpacity);
            previewSorter.Apply(placingYaw, 0f, false, devices[deviceIndex].sorterDefaultForce);
            preview = previewSorter.Root;
        }
        else if (devices[deviceIndex].kind == DeviceKind.Seed)
        {
            preview = new GameObject("Seed Preview");
            PixelLooks.CreateSproutObject(preview.transform, clicker.SeedSproutWorldSize);
            preview.SetActive(false);
        }
        else
        {
            preview = devices[deviceIndex].kind == DeviceKind.Fan
                ? BuildFanObject(devices[deviceIndex], true, out _, out _)
                : BuildDeviceObject(devices[deviceIndex], true, out _, out _);
            preview.SetActive(false);
        }
        clicker.SetClicksBlocked(true); // so the placing click doesn't also hit the cube
        return true;
    }

    /// <summary>Stops placing without using up the device.</summary>
    public void CancelPlacement()
    {
        if (IsPlacing) EndPlacement();
    }

    private void EndPlacement()
    {
        placeEndFrame = Time.frameCount; // the click that ended placing must not start a removal hold
        placingIndex = -1;
        if (preview != null) Destroy(preview);
        preview = null;
        StartCoroutine(UnblockClicksNextFrame());
    }

    private System.Collections.IEnumerator UnblockClicksNextFrame()
    {
        yield return null; // the click that ended placing has been fully handled by then
        if (clicker != null) clicker.SetClicksBlocked(false);
    }

    private void UpdatePlacement()
    {
        if (Time.timeScale <= 0f) return;
        if (Time.frameCount == placeStartFrame) return; // ignore the right-click that started this

        if (devices[placingIndex].kind == DeviceKind.Sorter)
        {
            UpdateSorterPlacement();
            return;
        }

        bool hasPoint = TryGetPlacePoint(out Vector3 point);
        preview.SetActive(hasPoint);
        if (hasPoint) preview.transform.position = point;

        // A fan can be turned with the mouse wheel or Q / E.
        if (devices[placingIndex].kind == DeviceKind.Fan)
        {
            placingYaw += TurnInput();
            preview.transform.rotation = Quaternion.Euler(0f, placingYaw, 0f);
        }

        if (devices[placingIndex].kind == DeviceKind.Seed)
        {
            if (LeftPressed() && hasPoint && !PointerOverUI()) PlantSeed(point);
            else if (RightPressed()) CancelPlacement();
            return;
        }

        if (LeftPressed() && hasPoint && !PointerOverUI()) PlaceDevice(point);
        else if (RightPressed()) CancelPlacement();
    }

    /// <summary>Plants the seed being held at 'point'; keeps planting while more seeds of that kind are left.</summary>
    private void PlantSeed(Vector3 point)
    {
        Device d = devices[placingIndex];
        int tier = clicker.IndexOf(d.seedType);
        if (PixelClicker.IsDragonCube(d.seedType))
        {
            tier = clicker.PickMissingDragonCube(); // a Dragon Cube you don't hold
            if (tier < 0)
            {
                PixelHints.Announce("You already hold all seven Dragon Cubes - the Dragon Seed has nothing left to grow");
                EndPlacement();
                return;
            }
        }
        if ((!Inf && d.owned <= 0) || tier < 0) { EndPlacement(); return; }
        if (!clicker.PlantSeedSproutAt(point, tier))
        {
            PixelHints.Announce("Too many sprouts already - wait for some to finish growing");
            return;
        }
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);
        PixelStats.Count("seeds.planted");
        if (!Inf && d.owned <= 0) EndPlacement();
    }

    /// <summary>
    /// The sorter is fixed around the cube. Step 1: turn the pipe (scroll / Q / E), click to confirm. Step 2: the pipe
    /// follows the mouse inside a cone; click inside the cone to place it (right-click goes back to step 1).
    /// </summary>
    private void UpdateSorterPlacement()
    {
        Device d = devices[placingIndex];
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

        if (sorterPhase == 0)
        {
            placingYaw += TurnInput();
            sorterBend = 0f;
            previewSorter.Apply(placingYaw, 0f, false, d.sorterDefaultForce);

            if (LeftPressed() && !PointerOverUI()) sorterPhase = 1;
            else if (RightPressed()) CancelPlacement();
            return;
        }

        bool inCone = PixelSorterDevice.TryGetBend(previewSorter, cam, placingYaw, d.sorterBendConeDegrees, out float bend);
        if (!PointerOverUI()) sorterBend = bend;
        previewSorter.Apply(placingYaw, sorterBend, true, d.sorterDefaultForce);

        if (LeftPressed() && inCone && !PointerOverUI()) PlaceSorter();
        else if (RightPressed()) { sorterPhase = 0; sorterBend = 0f; }
    }

    private void PlaceSorter()
    {
        int index = placingIndex;
        Device d = devices[index];
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);

        PixelPlacedDevice placedSorter = SpawnDevice(index, Vector3.zero, 0f, d.durationSeconds, placingYaw, sorterBend, -1);
        if (placedSorter != null) placedSorter.Disarm();

        EndPlacement();
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(index);
    }

    private void PlaceDevice(Vector3 point)
    {
        int index = placingIndex;
        Device d = devices[index];
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);

        PixelPlacedDevice placed = SpawnDevice(index, point, placingYaw, UsesKind(d.kind) ? d.uses : DeviceSeconds(d), 0f, 0f, -1);
        if (placed != null) placed.Disarm();   // starts grey and idle until you left-click it

        EndPlacement();
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(index);
    }

    /// <summary>Builds a working device in the world (placing it, or restoring it from a save).</summary>
    private PixelPlacedDevice SpawnDevice(int index, Vector3 point, float yaw, float duration, float aim, float bend, int force)
    {
        Device d = devices[index];
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        PixelPlacedDevice result;
        PixelPlacedDevice.DisarmedText = string.IsNullOrEmpty(disarmedText) ? "Click to start" : disarmedText;
        PixelPlacedDevice.ArmFadeSeconds = armFadeSeconds > 0f ? armFadeSeconds : 0.7f;

        if (d.kind == DeviceKind.Fan)
        {
            GameObject fan = BuildFanObject(d, false, out Transform blades, out TextMeshPro fanTimer);
            fan.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));

            PixelFanDevice fanDevice = fan.AddComponent<PixelFanDevice>();
            fanDevice.Init(clicker, cam, blades, fanTimer, timerFormat, duration, d.radius, d.coneAngle,
                           d.coneHeight, d.blowAcceleration, d.liftAcceleration, d.bladeSpinDegrees, shrinkSeconds);
            result = fanDevice;
        }
        else if (d.kind == DeviceKind.Sorter)
        {
            PixelSorterDevice.Parts parts = PixelSorterDevice.Parts.Create(clicker, d, cam, false, 1f);

            GameObject tg = new GameObject("Timer");
            tg.transform.SetParent(parts.Root.transform, false);
            tg.transform.localPosition = new Vector3(0f, parts.Outer + timerHeightAbove, 0f);
            TextMeshPro timer = tg.AddComponent<TextMeshPro>();
            timer.text = string.Format(timerFormat, Mathf.CeilToInt(duration));
            timer.fontSize = timerFontSize;
            timer.fontStyle = FontStyles.Bold;
            timer.alignment = TextAlignmentOptions.Center;
            timer.color = timerColor;
            if (clicker.UIFont != null) timer.font = clicker.UIFont;

            PixelSorterDevice sorter = parts.Root.AddComponent<PixelSorterDevice>();
            sorter.Init(clicker, cam, parts, d, timer, timerFormat, duration, aim, bend, force, shrinkSeconds);
            result = sorter;
        }
        else if (UsesKind(d.kind))
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.position = point;
            int uses = Mathf.Max(1, Mathf.RoundToInt(duration));
            if (d.kind == DeviceKind.ChargeBooster)
            {
                PixelChargeBooster booster = root.AddComponent<PixelChargeBooster>();
                booster.Init(clicker, cam, timer, timerFormat, uses, shrinkSeconds, d, d.bodyHeight);
                booster.SetCharge(aim); // a saved booster keeps its charge (stored in the 'aim' slot)
                result = booster;
            }
            else
            {
                PixelLightningRod rod = root.AddComponent<PixelLightningRod>();
                rod.Init(clicker, cam, timer, timerFormat, uses, shrinkSeconds, d, d.bodyHeight, 0f);
                result = rod;
            }
        }
        else
        {
            GameObject root = BuildDeviceObject(d, false, out Transform suckPoint, out TextMeshPro timer);
            root.transform.position = point;

            PixelVacuumDevice device = root.AddComponent<PixelVacuumDevice>();
            device.Init(clicker, cam, suckPoint, timer, timerFormat, duration, d.radius, d.pullAcceleration,
                        d.absorbDistance, shrinkSeconds);
            result = device;
        }

        result.DeviceIndex = index;
        return result;
    }

    // ------------------------------------------------------------------
    // Saving: the running potion and the devices standing in the world
    // ------------------------------------------------------------------

    /// <summary>Devices that last a number of uses (clicks, strikes) instead of a time.</summary>
    private static bool UsesKind(DeviceKind kind) => kind == DeviceKind.ChargeBooster || kind == DeviceKind.LightningRod;

    /// <summary>A placed device as the save system stores it.</summary>
    [Serializable]
    public class PlacedState
    {
        public string device;
        public Vector3 position;
        public float yaw;
        public float remaining;
        public float aim;
        public float bend;
        public int force;
        public float forceValue;   // the exact force set with the slider (0 = use the force button index)
        public bool disarmed;   // placed but never switched on yet
    }

    /// <summary>The devices currently standing in the world (those not already shrinking away).</summary>
    public System.Collections.Generic.List<PlacedState> GetPlacedDevices()
    {
        System.Collections.Generic.List<PlacedState> list = new System.Collections.Generic.List<PlacedState>();
        foreach (PixelPlacedDevice placed in PixelPlacedDevice.All)
        {
            if (placed == null || placed.IsRemoving || placed.DeviceIndex < 0 || placed.DeviceIndex >= devices.Length) continue;
            PlacedState state = new PlacedState
            {
                device = devices[placed.DeviceIndex].displayName,
                position = placed.transform.position,
                yaw = placed.transform.eulerAngles.y,
                remaining = placed.Remaining,
                disarmed = !placed.Armed,
            };
            if (placed is PixelChargeBooster booster) state.aim = booster.Charge;
            if (placed is PixelSorterDevice sorter)
            {
                state.aim = sorter.AimDegrees;
                state.bend = sorter.BendDegrees;
                state.force = sorter.Force;
                state.forceValue = sorter.ForceValue;
            }
            list.Add(state);
        }
        return list;
    }

    /// <summary>Removes every placed device at once (before a save is loaded).</summary>
    public void ClearPlacedDevices()
    {
        foreach (PixelPlacedDevice placed in new System.Collections.Generic.List<PixelPlacedDevice>(PixelPlacedDevice.All))
            if (placed != null) Destroy(placed.gameObject);
    }

    /// <summary>Puts a saved device back in the world with the time it had left.</summary>
    public void RestorePlacedDevice(PlacedState state)
    {
        if (state == null || state.remaining <= 0f) return;
        int index = Array.FindIndex(devices, x => x != null && x.displayName == state.device);
        if (index < 0) return;
        PixelPlacedDevice restored = SpawnDevice(index, state.position, state.yaw, state.remaining, state.aim, state.bend, state.force);
        if (restored != null && state.disarmed) restored.Disarm();
        if (restored is PixelSorterDevice restoredSorter && state.forceValue > 0f) restoredSorter.SetForceValue(state.forceValue);
    }

    /// <summary>Starts a potion again with the time it had left (no sound, nothing used up). Used when loading a save.</summary>
    public void RestoreActive(int index, float secondsLeft)
    {
        if (index < 0 || index >= potions.Length || secondsLeft <= 0f) return;
        Potion potion = potions[index];
        activeIndex = index;
        remaining = secondsLeft;
        if (potion.craftOnly) clicker.SetForcedSpawnTiers(potion.type, potion.secondType, potion.hasThird ? potion.thirdType : (PixelClicker.PixelType?)null);
        else clicker.SetForcedSpawnTier(potion.type);
    }

    /// <summary>The floor point under the mouse: the first suitable collider hit, else the fallback plane.</summary>
    private bool TryGetPlacePoint(out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, placementLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue; // not on top of old pixels
            if (clicker.PixelTransform != null && hit.transform.IsChildOf(clicker.PixelTransform)) continue; // not on the cube
            if (hit.normal.y < minSurfaceNormalY) continue;                          // walls / steep slopes
            point = hit.point;
            return true;
        }

        Plane floor = new Plane(Vector3.up, new Vector3(0f, fallbackFloorY, 0f));
        if (floor.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Builds the cylinder (and range disc, and for a real device the timer text) with its origin on the floor.
    /// </summary>
    private GameObject BuildDeviceObject(Device d, bool isPreview, out Transform suckPoint, out TextMeshPro timer)
    {
        GameObject root = new GameObject(isPreview ? d.displayName + " (Preview)" : d.displayName);

        if (d.kind == DeviceKind.Vacuum)
        {
            BuildVacuumBody(root, d, isPreview);
        }
        else
        {
            // Cylinder body (Unity's cylinder is 2 units tall, hence the halved Y scale).
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>()); // never blocks clicks, placement rays or old pixels
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(d.bodyDiameter, d.bodyHeight * 0.5f, d.bodyDiameter);
            body.transform.localPosition = new Vector3(0f, d.bodyHeight * 0.5f, 0f);

            Color bodyColor = d.color;
            if (isPreview) bodyColor.a = previewOpacity;
            Material bodyMat = clicker.CreateVisualMaterial(bodyColor, isPreview);
            if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;
            else body.GetComponent<Renderer>().material.color = bodyColor;
        }

        // Flat disc showing the reach.
        if (showRange)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Range";
            Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(d.radius * 2f, 0.01f, d.radius * 2f);
            disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            Color discColor = d.color;
            discColor.a = rangeOpacity;
            Renderer dr = disc.GetComponent<Renderer>();
            Material discMat = clicker.CreateVisualMaterial(discColor, true);
            if (discMat != null) dr.sharedMaterial = discMat;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dr.receiveShadows = false;
        }

        // Where pixels are pulled to: the top of the cylinder.
        GameObject top = new GameObject("Suction Point");
        top.transform.SetParent(root.transform, false);
        top.transform.localPosition = new Vector3(0f, d.bodyHeight, 0f);
        suckPoint = top.transform;

        timer = null;
        if (!isPreview)
        {
            GameObject tg = new GameObject("Timer");
            tg.transform.SetParent(root.transform, false);
            tg.transform.localPosition = new Vector3(0f, d.bodyHeight + timerHeightAbove, 0f);
            timer = tg.AddComponent<TextMeshPro>();
            timer.text = string.Format(timerFormat, Mathf.CeilToInt(DeviceSeconds(d)));
            timer.fontSize = timerFontSize;
            timer.fontStyle = FontStyles.Bold;
            timer.alignment = TextAlignmentOptions.Center;
            timer.color = timerColor;
            if (clicker.UIFont != null) timer.font = clicker.UIFont;
        }

        return root;
    }

    /// <summary>
    /// The Vacuum Device's look: a turned (lathe) body with a flared base, a slim waist and a funnel mouth at the top, two glowing bands, a glowing
    /// core in the funnel and a small spinning turbine ("Spinner", turned by PixelVacuumDevice) above it.
    /// </summary>
    private void BuildVacuumBody(GameObject root, Device d, bool isPreview)
    {
        float R = d.bodyDiameter * 0.5f, H = d.bodyHeight;
        float opacity = isPreview ? previewOpacity : 1f;
        Color main = d.color; main.a = opacity;
        Color glow = Color.Lerp(d.color, Color.white, 0.55f); glow.a = opacity;

        // The turned body.
        GameObject body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(root.transform, false);
        body.GetComponent<MeshFilter>().sharedMesh = VacuumLatheMesh(R, H);
        MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();
        Material bodyMat = clicker.CreateVisualMaterial(main, isPreview);
        if (bodyMat != null) bodyRenderer.sharedMaterial = bodyMat; else bodyRenderer.material.color = main;

        // Two glowing bands round the body (unlit, so they read as lit from inside).
        float[] bandHeights = { 0.26f, 0.46f };
        foreach (float bh in bandHeights)
        {
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Glow Band";
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(root.transform, false);
            band.transform.localScale = new Vector3(R * 2f * 0.93f, H * 0.012f, R * 2f * 0.93f);
            band.transform.localPosition = new Vector3(0f, H * bh, 0f);
            Renderer br = band.GetComponent<Renderer>();
            br.sharedMaterial = PixelLooks.OverlayMaterial();
            br.material.color = glow;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            br.receiveShadows = false;
        }

        // The glowing core in the funnel.
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "Core";
        Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = new Vector3(R * 2f * 0.8f, H * 0.01f, R * 2f * 0.8f);
        core.transform.localPosition = new Vector3(0f, H * 0.9f, 0f);
        Renderer cr = core.GetComponent<Renderer>();
        cr.sharedMaterial = PixelLooks.OverlayMaterial();
        cr.material.color = Color.Lerp(glow, Color.white, 0.35f);
        cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cr.receiveShadows = false;

        // A little turbine over the core.
        GameObject spinner = new GameObject("Spinner");
        spinner.transform.SetParent(root.transform, false);
        spinner.transform.localPosition = new Vector3(0f, H * 0.93f, 0f);
        Color dark = new Color(d.color.r * 0.35f, d.color.g * 0.35f, d.color.b * 0.35f, opacity);
        for (int i = 0; i < 3; i++)
        {
            GameObject blade = MakePrimitive(PrimitiveType.Cube, "Blade " + i, spinner.transform, dark, isPreview);
            blade.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
            blade.transform.localScale = new Vector3(R * 1.5f, H * 0.012f, R * 0.14f);
        }
    }

    /// <summary>A surface of revolution (a turned body): flared base, straight body, slim waist, funnel mouth with a thin lip and a hollow top.</summary>
    private static Mesh VacuumLatheMesh(float R, float H)
    {
        // (radius as a multiple of R, height as a fraction of H), from the middle of the bottom, out and up, over the lip and back to the middle of the funnel.
        float[,] profile =
        {
            { 0f, 0f }, { 1.3f, 0f }, { 1.3f, 0.045f }, { 1.05f, 0.09f }, { 0.9f, 0.15f }, { 0.9f, 0.58f }, { 0.78f, 0.66f }, { 0.7f, 0.74f },
            { 0.78f, 0.83f }, { 1.0f, 0.95f }, { 1.1f, 0.99f }, { 1.08f, 1.0f }, { 0.98f, 0.98f }, { 0.85f, 0.93f }, { 0.5f, 0.88f }, { 0f, 0.87f },
        };
        int rings = profile.GetLength(0), sides = 28;
        Vector3[] vertices = new Vector3[rings * sides];
        for (int i = 0; i < rings; i++)
            for (int k = 0; k < sides; k++)
            {
                float angle = k * Mathf.PI * 2f / sides;
                float r = profile[i, 0] * R;
                vertices[i * sides + k] = new Vector3(Mathf.Cos(angle) * r, profile[i, 1] * H, Mathf.Sin(angle) * r);
            }

        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
        for (int i = 0; i < rings - 1; i++)
        {
            // Outward normal of this stretch of the profile (the solid is on its left): rotate the tangent a quarter turn clockwise.
            float dr = profile[i + 1, 0] * R - profile[i, 0] * R, dy = profile[i + 1, 1] * H - profile[i, 1] * H;
            for (int k = 0; k < sides; k++)
            {
                int n = (k + 1) % sides;
                int a = i * sides + k, b = i * sides + n, c = (i + 1) * sides + k, e = (i + 1) * sides + n;
                float angle = (k + 0.5f) * Mathf.PI * 2f / sides;
                Vector3 expected = new Vector3(Mathf.Cos(angle) * dy, -dr, Mathf.Sin(angle) * dy);
                AddOriented(triangles, vertices, a, c, b, expected);
                AddOriented(triangles, vertices, b, c, e, expected);
            }
        }

        Mesh mesh = new Mesh { name = "VacuumBody" };
        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddOriented(System.Collections.Generic.List<int> triangles, Vector3[] v, int a, int b, int c, Vector3 expectedNormal)
    {
        Vector3 geometric = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (geometric.sqrMagnitude < 1e-12f) return;   // a collapsed triangle (at the middle of the bottom / top)
        if (Vector3.Dot(geometric, expectedNormal) < 0f) { int t = b; b = c; c = t; }
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
    }

    /// <summary>
    /// Builds the fan: a post, a round housing, spinning blades, a floor wedge showing the blown area and (for a real
    /// fan) the timer. The fan blows along the object's forward (+Z) direction.
    /// </summary>
    private GameObject BuildFanObject(Device d, bool isPreview, out Transform blades, out TextMeshPro timer)
    {
        GameObject root = new GameObject(isPreview ? d.displayName + " (Preview)" : d.displayName);
        float opacity = isPreview ? previewOpacity : 1f;

        Color bodyColor = d.color;
        bodyColor.a = opacity;
        Color darkColor = new Color(d.color.r * 0.45f, d.color.g * 0.45f, d.color.b * 0.45f, opacity);

        // Post.
        GameObject post = MakePrimitive(PrimitiveType.Cylinder, "Post", root.transform, darkColor, isPreview);
        post.transform.localScale = new Vector3(0.12f, d.bodyHeight * 0.5f, 0.12f);
        post.transform.localPosition = new Vector3(0f, d.bodyHeight * 0.5f, 0f);

        // Head: a flat round housing facing forward.
        GameObject head = new GameObject("Head");
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, d.bodyHeight, 0f);

        GameObject housing = MakePrimitive(PrimitiveType.Cylinder, "Housing", head.transform, darkColor, isPreview);
        housing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        housing.transform.localScale = new Vector3(d.bodyDiameter, 0.05f, d.bodyDiameter);

        // Blades: three bars crossing at the centre = six blade tips, spun around the forward axis.
        GameObject bladeRoot = new GameObject("Blades");
        bladeRoot.transform.SetParent(head.transform, false);
        bladeRoot.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        for (int i = 0; i < 3; i++)
        {
            GameObject blade = MakePrimitive(PrimitiveType.Cube, "Blade " + i, bladeRoot.transform, bodyColor, isPreview);
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
            blade.transform.localScale = new Vector3(d.bodyDiameter * 0.9f, d.bodyDiameter * 0.16f, 0.015f);
        }
        blades = bladeRoot.transform;

        // The area that gets blown: a flat wedge on the floor.
        if (showRange)
        {
            GameObject wedge = new GameObject("Blow Area", typeof(MeshFilter), typeof(MeshRenderer));
            wedge.transform.SetParent(root.transform, false);
            wedge.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            wedge.GetComponent<MeshFilter>().sharedMesh = BuildWedgeMesh(d.radius, d.coneAngle);

            Color wedgeColor = d.color;
            wedgeColor.a = rangeOpacity;
            MeshRenderer mr = wedge.GetComponent<MeshRenderer>();
            Material mat = clicker.CreateVisualMaterial(wedgeColor, true);
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        timer = null;
        if (!isPreview)
        {
            GameObject tg = new GameObject("Timer");
            tg.transform.SetParent(root.transform, false);
            tg.transform.localPosition = new Vector3(0f, d.bodyHeight + d.bodyDiameter * 0.5f + timerHeightAbove, 0f);
            timer = tg.AddComponent<TextMeshPro>();
            timer.text = string.Format(timerFormat, Mathf.CeilToInt(DeviceSeconds(d)));
            timer.fontSize = timerFontSize;
            timer.fontStyle = FontStyles.Bold;
            timer.alignment = TextAlignmentOptions.Center;
            timer.color = timerColor;
            if (clicker.UIFont != null) timer.font = clicker.UIFont;
        }

        return root;
    }

    /// <summary>A primitive with no collider and the game's material tinted with 'color'.</summary>
    private GameObject MakePrimitive(PrimitiveType type, string objectName, Transform parent, Color color, bool transparent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = objectName;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);

        Renderer r = go.GetComponent<Renderer>();
        Material mat = clicker.CreateVisualMaterial(color, transparent);
        if (mat != null) r.sharedMaterial = mat;
        else r.material.color = color;
        return go;
    }

    /// <summary>A flat pie slice opening along +Z, lying on the XZ plane (faces up).</summary>
    private static Mesh BuildWedgeMesh(float length, float fullAngle)
    {
        const int segments = 24;
        float half = fullAngle * 0.5f * Mathf.Deg2Rad;

        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)segments);
            vertices[i + 1] = new Vector3(Mathf.Sin(a) * length, 0f, Mathf.Cos(a) * length);
        }
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        Mesh mesh = new Mesh { name = "Fan Wedge", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Degrees to turn this frame from the mouse wheel and the Q / E keys.</summary>
    private float TurnInput()
    {
        float degrees = 0f;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current != null) degrees += Mathf.Sign(Mouse.current.scroll.ReadValue().y) * (Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f ? 1f : 0f) * rotateStepDegrees;
#else
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) degrees += Mathf.Sign(wheel) * rotateStepDegrees;
#endif
        if (PixelKeys.Held(PixelAction.RotateRight)) degrees += rotateSpeedDegrees * Time.deltaTime; // rebindable (default E)
        if (PixelKeys.Held(PixelAction.RotateLeft)) degrees -= rotateSpeedDegrees * Time.deltaTime;  // (default Q)
        return degrees;
    }

    /// <summary>Description with {pixel} and {duration} filled in.</summary>
    public string Describe(int index)
    {
        Potion potion = potions[index];
        return (potion.description ?? "")
            .Replace("{pixel}", potion.type.ToString())
            .Replace("{pixel2}", potion.secondType.ToString())
            .Replace("{pixel3}", potion.thirdType.ToString())
            .Replace("{duration}", potion.durationSeconds.ToString("0.##"));
    }
}
