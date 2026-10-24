// ---- System notes ----
// PixelConsumablesTypes.cs
//   Part of PixelConsumables (partial class): the nested data types (Potion, DeviceKind, Device). The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;

public partial class PixelConsumables
{
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

        /// <summary>How many of this potion you own (progress in the GameState; type a test amount in the Pixel Game State component).</summary>
        public int owned { get => PixelGameState.Current.inventory.OwnedOf(Key); set => PixelGameState.Current.inventory.SetOwned(Key, value); }

        /// <summary>Extra storage on top of the normal limit (grows when you keep a ghost's potion in the backpack while already full, and stays). Progress in the GameState.</summary>
        public int bonusCap { get => PixelGameState.Current.inventory.BonusOf(Key); set => PixelGameState.Current.inventory.SetBonus(Key, value); }

        [NonSerialized] private string key;
        [NonSerialized] private long keyFor = -1;
        /// <summary>This potion's key in the inventory state: "p:type:second:third" (made again only when the potion's types change).</summary>
        private string Key
        {
            get
            {
                int second = craftOnly ? (int)secondType + 1 : 0, third = craftOnly && hasThird ? (int)thirdType + 1 : 0;
                long k = ((long)type * 100000L + second) * 100000L + third;
                if (k != keyFor || key == null) { keyFor = k; key = "p:" + (int)type + ":" + second + ":" + third; }
                return key;
            }
        }

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
        /// <summary>RETIRED 2026-10-20 (the ring Sorter was removed): the number stays reserved so the later kinds keep theirs. Never used.</summary>
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
        /// <summary>Placed: waters nearby Seed sprouts and the Seed pet about once a second, so they grow much faster.</summary>
        Sprinkler = 9,
        /// <summary>Placed: like the Vacuum Device it pulls old pixels in, but it never consumes them: it holds them in a heap.</summary>
        Magnet = 10,
        /// <summary>RETIRED 2026-10-20 (the Magnetized Sorter was removed): the number stays reserved so the later kinds keep theirs. Never used.</summary>
        MagnetSorter = 11,
        /// <summary>Placed: a walled pad. Pixels dropped on it bind their type; loose old pixels of a bound type are dematerialized (glitch) and rematerialize in the box as storage.</summary>
        QuantumOrganizer = 12,
        /// <summary>Placed, permanent, needs no switching on: a crate with two rows of five compartments. Click it to store the loose pixels lying on it; click a stored pixel to pick it up.</summary>
        StorageCrate = 13,
        /// <summary>Not placed by hand: right-click in the inventory (Fishing only) and a bucket stands on the floor until you stop Fishing. Drop Fish / Water pixels in; stored fish make more fish spawn.</summary>
        BaitBucket = 14,
        /// <summary>Placed, permanent, sold by the Lumberjack: a trailer-mounted wood chipper (PixelWoodChipper). Turn it before placing; its chute arrow shows where the chips go and a conveyor belt opening in that area latches on.</summary>
        WoodChipper = 15,
        /// <summary>Placed, permanent, one at a time: a wide glass funnel under the clicker cube that catches falling old pixels and carries them down a tube to an output on the floor you click (PixelFunnel). A conveyor belt opening in front of the output latches on.</summary>
        Funnel = 16,
        /// <summary>Not placed: right-click in the inventory to open the Gauntlet window (PixelInfinity) and set the six Prime Cubes in a golden cube to forge the Prime Cubelette. Sold only by the Wizard once all six stones are held.</summary>
        Gauntlet = 17,
        /// <summary>Not consumed: a belt-laying tool on sale from Electrician level 5. Right-click starts placing conveyor belts (PixelFeatureTester.StartFeature); hold the right button on a belt to remove it.</summary>
        ConveyorBelt = 18,
        /// <summary>Placed for 5 minutes (PixelRecyclerDevice; Operation upgrades): an open bin (PixelRecycler) that recycles every husk landing in it (50% back + XP with a same-type streak).</summary>
        Recycler = 19,
        /// <summary>Not placed: right-click to drink a skill's Focus potion (PixelFocus: its hazard block stops spawning for a while). Sold now and then by the skill's vendor (the Lumberjack: Lumberjack's Focus).</summary>
        FocusPotion = 20,
        /// <summary>Not placed: right-click to add Pesticide charges (PixelBlockRules.AddCharges for the Termite Hive: each charge clears a hive by itself). Sold by the Lumberjack.</summary>
        PesticideSpray = 21,
        /// <summary>Placed, permanent: the Sorting Hopper (PixelSortingHopper) - a standalone funnel that holds husks and drops each filtered type out of its own pipe. On sale from Standard level 3.</summary>
        SortingHopper = 22,
        /// <summary>Dev only (2026-10-20: couplings fit themselves when a belt / hose attaches): right-click starts placing couplings (PixelCoupling) on device sockets by hand.</summary>
        Coupling = 23,
        /// <summary>Not placed by hand: right-click starts routing a Container Hose between two couplings; one is used up per hose. Listed with the Utilities.</summary>
        ContainerHose = 24,
        /// <summary>Placed, permanent: a wind-up clockwork cube (PixelSweeper) that fetches husks close to fading into the nearest Storage Crate / Sorting Hopper / Recycler with room. On sale from Standard level 5.</summary>
        Sweeper = 25,
        /// <summary>Placed, permanent: a box with eight auto-direction coupling sockets (PixelJunctionBox) that routes husks from its In couplings to its Out ones (filters, round robin, a small buffer). On sale in the shop's Utilities (2026-10-19).</summary>
        JunctionBox = 26,
        /// <summary>Placed: a small Biomass Generator (PixelBiomassGenerator) - burns Woodcutting, Charcoal and Wood Chips husks to power every device in its radius (their timers stop). Shop Consumables.</summary>
        PocketBiomassGenerator = 27,
        /// <summary>Placed: the big Biomass Generator (PixelBiomassGenerator) - more charge, a wider radius. On sale at Woodcutting level 10.</summary>
        BiomassGenerator = 28,
        /// <summary>Placed tester piece bought in the shop (2026-10-20): right-click starts placing Belt Sorters on belts (PixelInstantItems.PlacePieces, feature 'Belt Sorter'); one used up per piece.</summary>
        BeltSorter = 29,
        /// <summary>Shop piece: the Conveyor Elevator (feature 'Conveyor Elevator'), one used up per tower.</summary>
        ConveyorElevator = 30,
        /// <summary>Shop piece: the Belt Jump ramps (feature 'Belt Jump'), one used up per jump.</summary>
        BeltJump = 31,
        /// <summary>Shop piece: the Conveyor Combiner (feature 'Conveyor Combiner'), one used up per box.</summary>
        ConveyorCombiner = 32,
        /// <summary>Shop piece: the Solar Panel (feature 'Solar Panel'), one used up per panel.</summary>
        SolarPanel = 33,
        /// <summary>Placed, permanent: the Power Bank (PixelPowerBank) - stores charge drawn from Biomass Generators in its large radius and powers every device there.</summary>
        PowerBank = 34,
    }

    /// <summary>A consumable object you place in the world (the Vacuum Device, the Fan).</summary>
    [Serializable]
    public class Device
    {
        /// <summary>A shallow copy (used to apply the Operation upgrades without changing the real settings).</summary>
        public Device Clone() => (Device)MemberwiseClone();

        [Tooltip("What this device does.")]
        public DeviceKind kind = DeviceKind.Vacuum;

        [Tooltip("Name shown in the shop and the inventory.")]
        public string displayName = "Vacuum Device";

        [TextArea(1, 3)]
        [Tooltip("Shown in the shop. {radius} = reach, {duration} = seconds.")]
        public string description = "Place it on the floor. It pulls in husks within {radius} units and collects them again for {duration} seconds.";

        [Tooltip("Listed in the shop once this pixel type is unlocked (or when you already own one).")]
        public PixelClicker.PixelType requiredType = PixelClicker.PixelType.Vacuum;

        [Tooltip("Price in the shop. Pick any pixel type and amount; all costs are paid together.")]
        public PixelShop.PackCost[] costs;

        [Min(0)]
        [Tooltip("The most of this item you can hold at once (0 = no limit). The shop and crafting stop at this number.")]
        public int maxHeld = 0;

        [Min(0f)]
        [Tooltip("A rising price: every one of this item you already hold or have standing multiplies the next one's price by this (1.8 = x1.8, x3.24...). 0 or 1 = a flat price.")]
        public float priceGrowthPerOwned = 0f;

        [Min(0f)]
        [Tooltip("Sprinkler only: seconds between waterings of the seeds in reach (0 = 1).")]
        public float sprayInterval = 1f;

        [Min(1f)]
        [Tooltip("How many seconds the device works once placed.")]
        public float durationSeconds = 30f;

        [Min(0.1f)]
        [Tooltip("Reach (world units). Vacuum: how far husks are pulled in from. Fan: how long the cone is.")]
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

        [Header("Where it is sold")]
        [Tooltip("Craft-only: never sold in the shop or by visitors (you make it in the Crafting window).")]
        public bool craftOnly = false;

        [Tooltip("Not sold in the shop: only a visiting vendor (the Lumberjack) sells it - until the skill below reaches its level, then the shop sells it too.")]
        public bool vendorOnly = false;

        [Tooltip("Dev Tools only: never sold in the shop or by a visitor (whatever the settings above say); the Dev Hub's 'Dev items' widget gives it. For items taken out of the game for now.")]
        public bool devOnly = false;

        [Tooltip("Optional: the skill (id, e.g. woodcutting) whose level opens this item in the shop even though it is 'vendor only'. Empty = never.")]
        public string shopSkillId = "";
        [Min(0)]
        [Tooltip("The level of that skill that puts the item on sale in the shop.")]
        public int shopSkillLevel = 0;

        [Min(1)]
        [Tooltip("Quantum Organizer: how many pixels the box holds in total.")]
        public int boxCapacity = 60;

        [Header("Attuned (made by crafting a device with a potion)")]
        [Tooltip("Runtime: this is an attuned copy of a Vacuum / Fan / Magnet that only affects husks of 'Attune Type'.")]
        public bool attuned = false;

        [Tooltip("Runtime: the pixel type an attuned device works on.")]
        public PixelClicker.PixelType attuneType = PixelClicker.PixelType.White;

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

        [Header("Magic Beans (Seed kind only)")]
        [Tooltip("This seed item is the Magic Beans: planting it grows a beanstalk (with a Dragon Cube) instead of a sprout. Craft-only: never sold.")]
        public bool beans = false;

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

        [Min(0f)]
        [Tooltip("Biomass Generators: seconds of power when full (it comes full; it drains 1 a second). Power Bank: how much charge it holds (it comes empty).")]
        public float generatorMaxCharge = 0f;

        [Min(0f)]
        [Tooltip("Biomass Generators: seconds of power one Woodcutting husk adds.")]
        public float generatorChargePerHusk = 0f;

        /// <summary>How many of this device you own (progress in the GameState by display name; type a test amount in the Pixel Game State component).</summary>
        public int owned { get => PixelGameState.Current.inventory.OwnedOf(Key); set => PixelGameState.Current.inventory.SetOwned(Key, value); }

        [NonSerialized] private string key, keyFor;
        /// <summary>This device's key in the inventory state: "d:" + its display name (made again only when the name changes).</summary>
        private string Key
        {
            get
            {
                if (!ReferenceEquals(keyFor, displayName) || key == null) { keyFor = displayName; key = "d:" + displayName; }
                return key;
            }
        }
    }
}
