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
public class PixelConsumables : MonoBehaviour, IPixelSaved
{
    // ---- save: this system writes its own part of the save file (PixelSaveRegistry) ----
    // Two parts: what you HOLD (potions, devices; loads with the other inventories) and what is out IN THE WORLD (placed devices and the
    // running potion; loads after the husks are back, since FinishLoad resets the spawn choice and devices may hold husks).
    [System.Serializable] private class PotionSave { public int type, owned, second, third, bonus; }   // second / third = (pixel type + 1) of a combo potion, 0 = none; bonus = ghost backpack storage
    [System.Serializable] private class DeviceSave { public string name; public int owned; }
    [System.Serializable] private class HeldState { public PotionSave[] potions; public DeviceSave[] devices; }
    [System.Serializable] private class WorldState { public PlacedState[] placed; public bool hasActive; public int activeType, activeSecond, activeThird; public float activeRemaining; }

    private static int SecondOf(Potion p) => p.craftOnly ? (int)p.secondType + 1 : 0;
    private static int ThirdOf(Potion p) => p.craftOnly && p.hasThird ? (int)p.thirdType + 1 : 0;

    string IPixelSaved.SaveId => "inventory";
    int IPixelSaved.SaveOrder => 210;

    string IPixelSaved.Export()
    {
        HeldState s = new HeldState();
        System.Collections.Generic.List<PotionSave> potionSaves = new System.Collections.Generic.List<PotionSave>();
        for (int i = 0; i < Count; i++)
        {
            Potion p = Get(i);
            if (p.craftOnly && p.owned <= 0 && p.bonusCap <= 0) continue;   // (of the many combo potions only the ones you hold)
            potionSaves.Add(new PotionSave { type = (int)p.type, owned = p.owned, bonus = p.bonusCap, second = SecondOf(p), third = ThirdOf(p) });
        }
        s.potions = potionSaves.ToArray();
        System.Collections.Generic.List<DeviceSave> deviceSaves = new System.Collections.Generic.List<DeviceSave>();
        for (int i = 0; i < DeviceCount; i++)
        {
            Device d = GetDevice(i);
            if (d.attuned && d.owned <= 0) continue;   // (there is an attuned device for every pixel type: only the ones you hold)
            deviceSaves.Add(new DeviceSave { name = d.displayName, owned = d.owned });
        }
        s.devices = deviceSaves.ToArray();
        return PixelSaveRegistry.ToJson(s);
    }

    void IPixelSaved.Import(string data)
    {
        HeldState s = PixelSaveRegistry.FromJson<HeldState>(data);
        StopActive();
        for (int i = 0; i < Count; i++)
        {
            Potion p = Get(i);
            int second = SecondOf(p), third = ThirdOf(p);
            PotionSave saved = s != null && s.potions != null ? System.Array.Find(s.potions, x => x.type == (int)p.type && x.second == second && x.third == third) : null;
            p.bonusCap = saved != null ? Mathf.Max(0, saved.bonus) : 0;
            p.owned = saved != null ? ClampHeld(i, saved.owned) : 0;
        }
        CancelPlacement();
        for (int i = 0; i < DeviceCount; i++)
        {
            string name = GetDevice(i).displayName;
            DeviceSave saved = s != null && s.devices != null ? System.Array.Find(s.devices, x => x.name == name) : null;
            GetDevice(i).owned = saved != null ? ClampDeviceHeld(i, saved.owned) : 0;
        }
    }

    /// <summary>The second save part: placed devices and the running potion (after the husks are back on the field).</summary>
    private class WorldPart : IPixelSaved
    {
        private readonly PixelConsumables owner;
        public WorldPart(PixelConsumables owner) { this.owner = owner; }
        public string SaveId => "inventoryWorld";
        public int SaveOrder => PixelSaveRegistry.WorldBand + 120;

        public string Export()
        {
            WorldState s = new WorldState { placed = owner.GetPlacedDevices().ToArray() };
            if (owner.IsActive)
            {
                Potion running = owner.Get(owner.ActiveIndex);
                s.hasActive = true;
                s.activeType = (int)running.type;
                s.activeSecond = SecondOf(running);
                s.activeThird = ThirdOf(running);
                s.activeRemaining = owner.Remaining;
            }
            return PixelSaveRegistry.ToJson(s);
        }

        public void Import(string data)
        {
            WorldState s = PixelSaveRegistry.FromJson<WorldState>(data);
            owner.ClearPlacedDevices();
            if (s == null) return;
            if (s.placed != null) foreach (PlacedState placed in s.placed) owner.RestorePlacedDevice(placed);
            if (!s.hasActive) return;
            for (int i = 0; i < owner.Count; i++)
            {
                Potion p = owner.Get(i);
                if ((int)p.type != s.activeType || SecondOf(p) != s.activeSecond || ThirdOf(p) != s.activeThird) continue;
                owner.RestoreActive(i, s.activeRemaining);
                break;
            }
        }
    }

    private WorldPart worldPart;

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
        /// <summary>Placed: waters nearby Seed sprouts and the Seed pet about once a second, so they grow much faster.</summary>
        Sprinkler = 9,
        /// <summary>Placed: like the Vacuum Device it pulls old pixels in, but it never consumes them: it holds them in a heap.</summary>
        Magnet = 10,
        /// <summary>Placed, craft-only (Sorter + Magnet): sucks pixels from an arc in front into a box and shoots chosen types out of three movable pipes.</summary>
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
        /// <summary>Placed, permanent: a sloped tray that latches onto a Recycler's back (PixelSortingHopper) and slides ONE chosen husk type through the back hatch into the bin (a steady streak); other husks lie in it and fade. On sale from Standard level 3.</summary>
        SortingHopper = 22,
        /// <summary>Not placed by hand: right-click starts placing couplings (PixelCoupling) on device sockets; one is used up per coupling fitted. Listed with the Utilities.</summary>
        Coupling = 23,
        /// <summary>Not placed by hand: right-click starts routing a Container Hose between two couplings; one is used up per hose. Listed with the Utilities.</summary>
        ContainerHose = 24,
        /// <summary>Placed, permanent: a wind-up clockwork cube (PixelSweeper) that fetches husks close to fading into the nearest Storage Crate / Sorting Hopper / Recycler with room. On sale from Standard level 5.</summary>
        Sweeper = 25,
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
        public string description = "Place it on the floor. It pulls in old pixels within {radius} units and collects them again for {duration} seconds.";

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

        [Header("Magnetized Sorter only")]
        [Tooltip("Craft-only: never sold in the shop or by visitors (you make it in the Crafting window).")]
        public bool craftOnly = false;

        [Tooltip("Not sold in the shop: only a visiting vendor (the Lumberjack) sells it - until the skill below reaches its level, then the shop sells it too.")]
        public bool vendorOnly = false;

        [Tooltip("Optional: the skill (id, e.g. woodcutting) whose level opens this item in the shop even though it is 'vendor only'. Empty = never.")]
        public string shopSkillId = "";
        [Min(0)]
        [Tooltip("The level of that skill that puts the item on sale in the shop.")]
        public int shopSkillLevel = 0;

        [Range(20f, 170f)]
        [Tooltip("Magnetized Sorter: opening angle (degrees) of the arc in front of the magnet that pulls pixels in. Its length is 'Radius'.")]
        public float arcDegrees = 110f;

        [Min(1)]
        [Tooltip("Magnetized Sorter: how many pixels the box holds in total.")]
        public int boxCapacity = 60;

        [Min(0.05f)]
        [Tooltip("Magnetized Sorter: seconds between two pixels shot from one exit pipe.")]
        public float shootInterval = 0.45f;

        [Min(2f)]
        [Tooltip("Magnetized Sorter: how far from the box an exit pipe's end can be put down (world units).")]
        public float pipeReach = 7f;

        [Header("Attuned (made by crafting a device with a potion)")]
        [Tooltip("Runtime: this is an attuned copy of a Vacuum / Fan / Magnet that only affects old pixels of 'Attune Type'.")]
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
    [Tooltip("Pesticide charges one Pesticide Spray adds (each clears a Termite Hive by itself).")]
    [SerializeField] private int pesticideSprayCharges = 3;
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

    [SerializeField, HideInInspector] private int consumablePriceVersion;   // 1 = the 2026-10-17 repricing (2-3 pure colours per item); see ResetDefaultDevicePrices

    [Header("Prices")]
    [Tooltip("Potion and device prices paid in a pixel type are multiplied by that pixel's Value upgrade multiplier, so they stay worth the same as its payout grows. Off = the prices typed above, always.")]
    [SerializeField] private bool scalePricesWithValue = false;   // (renamed from pricesScaleWithValue so it starts off: Value is capped by skill levels now, prices stay as typed)

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

    [Header("Shift snapping")]
    [Min(0f)]
    [Tooltip("Placing snaps the spot to a grid of this many old-pixel widths (also used by the Dev Hub tester); hold Shift for free placement. 0 = the coded default (1).")]
    [SerializeField] private float shiftSnapGridCubes = 0f;

    [Min(0f)]
    [Tooltip("Placing and turning snap the turn to steps of this many degrees; hold Shift for a free turn. 0 = the coded default (15).")]
    [SerializeField] private float shiftSnapAngleDegrees = 0f;

    [Header("Removing")]
    [Min(0.1f)]
    [Tooltip("How long (seconds) you hold the right mouse button on a placed device to remove it. The same hold cancels the running potion.")]
    [SerializeField] private float removeHoldSeconds = 0.8f;
    [Tooltip("Seconds the right button must already be held on a device before the remove ring starts (a shorter right TAP opens the device panel instead, PixelDeviceCard).")]
    [SerializeField] private float removeStartDelay = 0.25f;
    private float rightDownAt = -10f;

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
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 150 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 80 },
            },
            radius = 6f,
            color = new Color(0.4f, 0.85f, 1f, 1f),
            bodyDiameter = 0.9f,
            bodyHeight = 0.9f,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultMagnet()
    {
        return new Device
        {
            kind = DeviceKind.Magnet,
            displayName = "Magnet",
            description = "Place it near the cube. For {duration} seconds it pulls every loose old pixel within {radius} units into a heap around it, without collecting them.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 20 },
            },
            durationSeconds = 30f,
            radius = 5f,
            pullAcceleration = 24f,
            absorbDistance = 0.9f,   // inside this distance a pixel just settles (nothing is collected)
            color = new Color(0.85f, 0.2f, 0.2f, 1f),
            bodyDiameter = 0.8f,
            bodyHeight = 0.9f,
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultStorageCrate()
    {
        return new Device
        {
            kind = DeviceKind.StorageCrate,
            displayName = "Storage Crate",
            description = "A sectioned crate with room for 10 pixels (2 rows of 5). It needs no switching on and stays until you remove it. Drag pixels onto it (Pixel Grabbing) and click the crate to store them in the next free sections; click a stored pixel to pick it up again.",
            requiredType = PixelClicker.PixelType.Gray,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 200 },
            },
            maxHeld = 3,
            durationSeconds = 1e9f,
            radius = 1f,
            boxCapacity = 10,
            color = new Color(0.62f, 0.45f, 0.28f, 1f),
            bodyDiameter = 2.5f,   // the crate's width (five compartments)
            bodyHeight = 0.3f,     // wall height
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultRecycler()
    {
        return new Device
        {
            kind = DeviceKind.Recycler,
            displayName = "Recycler",
            description = "A bin that recycles every husk that lands in it - or that its suction ring draws in from close by - for half its value as currency (same colour in a row = a streak that raises the pay, never above a click). Works for {duration} s; Speed / Size upgrades in the shop's Operation tab. Several can stand at once; each extra one costs more.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 75 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 50 },
            },
            maxHeld = 3,
            priceGrowthPerOwned = 2f,   // each Recycler you hold or have standing doubles the next one's price (Cubie's free one makes the 2nd 150 White + 100 Green)
            durationSeconds = 300f,   // 5 minutes (Operation: Run Time)
            radius = PixelRecycler.DefaultSize,   // the bin's size factor (1.8 = nearly twice as wide and deep as the first bin; Operation: Size adds to it)
            color = new Color(0.25f, 0.55f, 0.32f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 2f,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultSortingHopper()
    {
        return new Device
        {
            kind = DeviceKind.SortingHopper,
            displayName = "Sorting Hopper",
            description = "A big funnel that stands behind a Recycler and holds up to 20 husks dropped into it. Right-click it and press Select filter: that type goes into the Recycler, one after another (so the streak builds); every other husk drops out of its bottom spout. It stays until you remove it.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 200 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 150 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 6f,   // how far from a Recycler's back it latches on (world units)
            color = new Color(0.3f, 0.48f, 0.6f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 1.5f,
            vendorOnly = true,          // not in the shop until...
            shopSkillId = "standard",   // ...Standard level 3 puts it on sale (GameDesign section 6: the first general husk tool); no vendor sells it
            shopSkillLevel = 3,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it - near a Recycler it latches onto its back  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultFunnel()
    {
        return new Device
        {
            kind = DeviceKind.Funnel,
            displayName = "Funnel",
            description = "A wide glass funnel that hangs under the clicker cube and catches the pixels that fall off it. They are carried down a see-through tube and come out at an output on the floor - click where you want it. Fit a coupling on its output and start a conveyor belt there to feed it. Only one stands at a time: placing it again moves it.",
            requiredType = PixelClicker.PixelType.Gray,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 350 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 200 },
            },
            maxHeld = 1,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.55f, 0.8f, 1f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 3f,
            placingMessage = "Click the floor where the {0}'s output should be. Scroll or Q / E turns the spout a little; a conveyor belt opening in front of it latches on  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultWoodChipper()
    {
        return new Device
        {
            kind = DeviceKind.WoodChipper,
            vendorOnly = true,
            shopSkillId = "woodcutting",   // Woodcutting level 2 puts it on sale in the shop as well
            shopSkillLevel = 2,
            displayName = "Wood Chipper",
            description = "A trailer-mounted wood chipper, bought from the Lumberjack. Wood pixels dropped into its hopper are shredded for Woodcutting XP and every few of them make a Wood Chips block that shoots out of its chute (a conveyor belt opening in front of the chute latches on). Anything that is not wood damages it, and enough of that breaks it for good.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Wood, amount = 400 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Bark, amount = 150 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.9f, 0.45f, 0.1f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 3f,
            placingMessage = "Scroll or Q / E to turn the {0}. The arrow shows where its chips go (a conveyor belt opening latches on). Click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultQuantumOrganizer()
    {
        return new Device
        {
            kind = DeviceKind.QuantumOrganizer,
            displayName = "Quantum Organizer",
            description = "A pad with walls. Switch it on, then drop a pixel onto the pad to bind that pixel type to it (up to three types). Every loose old pixel of a bound type is dematerialized with a glitch and rematerializes inside the box as storage (up to 60 pixels). Click the box to let some out again; everything comes out when its {duration} seconds run out.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 60 },
            },
            maxHeld = 3,
            durationSeconds = 600f,
            radius = 9f,          // how far from the pad it reaches for pixels of its bound types
            boxCapacity = 60,
            color = new Color(0.45f, 0.3f, 0.9f, 1f),
            bodyDiameter = 2.4f,  // the pad's inner side
            bodyHeight = 0.5f,    // wall height
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultMagnetSorter()
    {
        return new Device
        {
            kind = DeviceKind.MagnetSorter,
            displayName = "Magnetized Sorter",
            description = "Craft it from a Sorter and a Magnet. Its magnet pulls loose old pixels from an arc in front of it into a box. Click an exit pipe to grab it and click again to put it down anywhere; scroll over a pipe to choose which stored pixel type it shoots out.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new PixelShop.PackCost[0],
            craftOnly = true,
            maxHeld = 3,
            durationSeconds = 180f,
            radius = 5.5f,
            pullAcceleration = 26f,
            absorbDistance = 0.9f,
            sorterExitSpeed = 6f,
            color = new Color(0.55f, 0.35f, 0.85f, 1f),
            bodyDiameter = 1.2f,
            bodyHeight = 0.9f,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultMagicBeans()
    {
        return new Device
        {
            kind = DeviceKind.Seed,
            displayName = "Magic Beans",
            description = "Plant them (right-click in the Inventory, then click the floor). A tall beanstalk grows with a handful of random cubes along it, and one of them is a Dragon Cube you don't hold yet.",
            requiredType = PixelClicker.PixelType.White,
            seedType = PixelClicker.PixelType.White,
            beans = true,
            craftOnly = true,
            maxHeld = 1,
            costs = new PixelShop.PackCost[0],
            color = new Color(0.45f, 0.8f, 0.3f, 1f),
            placingMessage = "Click the floor to plant the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultSprinkler()
    {
        return new Device
        {
            kind = DeviceKind.Sprinkler,
            displayName = "Sprinkler",
            description = "Place it near your seeds. For {duration} seconds it waters every growing sprout and Seed pet within {radius} units once a second, so they grow far faster.",
            requiredType = PixelClicker.PixelType.Water,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Water, amount = 40 },
            },
            durationSeconds = 45f,
            radius = 4.5f,
            sprayInterval = 1f,
            color = new Color(0.3f, 0.7f, 1f, 1f),
            bodyDiameter = 0.5f,
            bodyHeight = 0.7f,
            placingMessage = "Click the floor near your seeds to place the {0}  (right-click to cancel)",
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
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 160 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 200 },
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
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 300 },
            },
            maxHeld = 1,
            color = new Color(0.8f, 0.85f, 1f, 1f),
        };
    }

    private static Device CreateDefaultBaitBucket()
    {
        return new Device
        {
            kind = DeviceKind.BaitBucket,
            displayName = "Bait Bucket",
            description = "Right-click to set it out (needs Fishing as the skill in play). A bucket stands on the floor until you stop Fishing. Drag Fish and Water pixels into it (Pixel Grabbing): they are stored with their value instead of splashing or fading, and every stored fish makes more fish show up. Click the bucket to take a pixel out again. You can only hold one.",
            requiredType = PixelClicker.PixelType.Fish,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Water, amount = 150 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Seaweed, amount = 25 },
            },
            maxHeld = 1,
            color = new Color(0.8f, 0.22f, 0.22f, 1f),
        };
    }

    private static Device CreateDefaultGauntlet()
    {
        return new Device
        {
            kind = DeviceKind.Gauntlet,
            displayName = "Prime Gauntlet",
            description = "Right-click to use. A golden cube with a socket on every face: set the six Prime Cubes in it and forge the Prime Cubelette. The Gauntlet and the cubes are used up. You can only hold one.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 2000000 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 1000000 },
            },
            maxHeld = 1,
            vendorOnly = true,
            color = new Color(1f, 0.82f, 0.22f, 1f),
        };
    }

    private static Device CreateDefaultSweeper()
    {
        return new Device
        {
            kind = DeviceKind.Sweeper,
            displayName = "Sweeper",
            description = "A little wind-up clockwork cube with a broom. It rolls out from its pad and rescues husks that are about to fade, carrying each one to the NEAREST Storage Crate, Sorting Hopper or Recycler with room - so where you put it decides where strays go. It runs about a minute, then slows to a stop: click it to wind it again. It stays until you remove it.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 400 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 200 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.86f, 0.66f, 0.3f, 1f),
            bodyDiameter = 1.5f,
            bodyHeight = 1f,
            vendorOnly = true,          // not in the shop until...
            shopSkillId = "standard",   // ...Standard level 5 puts it on sale (GameDesign section 6 'The Sweeper'); no vendor sells it
            shopSkillLevel = 5,
            placingMessage = "Click the floor to put the {0}'s pad down - it sweeps strays to the nearest crate, hopper or Recycler  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultCoupling()
    {
        return new Device
        {
            kind = DeviceKind.Coupling,
            displayName = "Coupling",
            description = "The piece that joins belts and hoses to a machine. Right-click to fit couplings: bring one near a device and it snaps onto a free socket (teal rings) - the Recycler's back hatch, the Storage Crate's in / out ports, the Funnel's output... Its arrow shows the way husks go (green in, orange out; click an organizer's coupling dot to switch). One is used up per coupling fitted.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 40 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 40 },
            },
            maxHeld = 10,
            color = new Color(0.62f, 0.66f, 0.7f, 1f),
        };
    }

    private static Device CreateDefaultContainerHose()
    {
        return new Device
        {
            kind = DeviceKind.ContainerHose,
            displayName = "Container Hose",
            description = "A see-through hose that pumps stored husks from a Storage Crate or Quantum Organizer (an Out coupling) into any In coupling - the Recycler, another crate, a Trash Can... Right-click to lay one: click the start coupling, click bend points to lead it round things, then click the end coupling. A quick right click on a laid hose turns its flow round. One is used up per hose.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 80 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 80 },
            },
            maxHeld = 3,
            color = new Color(0.6f, 0.9f, 1f, 1f),
        };
    }

    private static Device CreateDefaultConveyorBelt()
    {
        return new Device
        {
            kind = DeviceKind.ConveyorBelt,
            vendorOnly = true,
            shopSkillId = "electrician",   // Electrician level 5 puts it on sale in the shop
            shopSkillLevel = 5,
            displayName = "Conveyor Belt Kit",
            description = "Right-click to lay conveyor belts: click a point, then another, and a belt is built between them (short pieces make curves; scroll raises or lowers the end). Old pixels that land on a belt are carried along it. Right click ends a chain; hold the right button on a belt to remove it. The kit is not used up. You can only hold one.",
            requiredType = PixelClicker.PixelType.Wire,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 400 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Wire, amount = 150 },
            },
            maxHeld = 1,
            color = new Color(0.95f, 0.8f, 0.2f, 1f),
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
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 1500 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 1800 },
            },
            maxHeld = 5,
            color = new Color(1f, 0.85f, 0.4f, 1f),
        };
    }

    private static Device CreateDefaultFocusPotion()
    {
        return new Device
        {
            kind = DeviceKind.FocusPotion,
            displayName = "Lumberjack's Focus",
            description = "Right-click to drink. For a minute no Termite Hive turns up while you train Woodcutting. Sold now and then by the Lumberjack. Hold up to 5.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.HornetNest, amount = 3 },   // (the Lumberjack adds two random pure pixels each visit)
            },
            maxHeld = 5,
            vendorOnly = true,
            color = new Color(0.45f, 0.85f, 0.35f, 1f),
        };
    }

    private static Device CreateDefaultPesticideSpray()
    {
        return new Device
        {
            kind = DeviceKind.PesticideSpray,
            displayName = "Pesticide Spray",
            description = "Right-click to use. Adds 3 Pesticide charges: each one clears a Termite Hive by itself. Sold by the Lumberjack. Hold up to 10.",
            requiredType = PixelClicker.PixelType.Pesticide,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Pesticide, amount = 5 },   // (the Lumberjack adds a random pure pixel each visit)
            },
            maxHeld = 10,
            vendorOnly = true,
            color = new Color(0.75f, 0.85f, 0.25f, 1f),
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
        !PixelClicker.IsRareCube(type) && !PixelClicker.IsStandalone(type) && type != PixelClicker.PixelType.Seed && type != PixelClicker.PixelType.Meteor;

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
        CreateDefaultComboFuel(), CreateDefaultGhostBait(), CreateDefaultPetTreat(), CreateDefaultBaitBucket(),
        CreateDefaultChargeBooster(), CreateDefaultLightningRod(),
    };

    private static Potion[] CreateDefaultPotions()
    {
        System.Collections.Generic.List<Potion> list = new System.Collections.Generic.List<Potion>();
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            if (!PixelClicker.IsRareCube(type)) list.Add(CreateDefaultPotion(type)); // a potion for every pixel (only the Dragon Cubes have none)
        return list.ToArray();
    }

    /// <summary>
    /// Every built-in device kind and the method that makes its default (one entry per kind; seeds are made per pixel type
    /// elsewhere). Used to add missing devices to a saved list and by the price reset (consumablePriceVersion).
    /// </summary>
    private static readonly System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>[] DefaultDeviceMakers =
    {
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Vacuum, CreateDefaultDevice),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Fan, CreateDefaultFan),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Sorter, CreateDefaultSorter),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ComboFuel, CreateDefaultComboFuel),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.GhostBait, CreateDefaultGhostBait),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PetTreat, CreateDefaultPetTreat),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ChargeBooster, CreateDefaultChargeBooster),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.LightningRod, CreateDefaultLightningRod),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Sprinkler, CreateDefaultSprinkler),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Magnet, CreateDefaultMagnet),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.MagnetSorter, CreateDefaultMagnetSorter),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.QuantumOrganizer, CreateDefaultQuantumOrganizer),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.StorageCrate, CreateDefaultStorageCrate),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.BaitBucket, CreateDefaultBaitBucket),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.WoodChipper, CreateDefaultWoodChipper),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Funnel, CreateDefaultFunnel),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Gauntlet, CreateDefaultGauntlet),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ConveyorBelt, CreateDefaultConveyorBelt),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Recycler, CreateDefaultRecycler),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.FocusPotion, CreateDefaultFocusPotion),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PesticideSpray, CreateDefaultPesticideSpray),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.SortingHopper, CreateDefaultSortingHopper),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Coupling, CreateDefaultCoupling),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ContainerHose, CreateDefaultContainerHose),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Sweeper, CreateDefaultSweeper),
    };

    /// <summary>
    /// One-time price reset: every built-in device in the saved list takes its coded default price (costs and the rising-price
    /// factor), since the Inspector list keeps the old typed prices otherwise. Bump consumablePriceVersion to run it again after a
    /// repricing. Seeds and potions are priced from their pixel at run time, so they are not touched.
    /// </summary>
    private bool ResetDefaultDevicePrices()
    {
        if (devices == null) return false;
        bool changed = false;
        foreach (Device d in devices)
        {
            if (d == null || d.attuned || d.kind == DeviceKind.Seed) continue;
            foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> maker in DefaultDeviceMakers)
            {
                if (maker.Key != d.kind) continue;
                Device fresh = maker.Value();
                d.costs = fresh.costs;
                d.priceGrowthPerOwned = fresh.priceGrowthPerOwned;
                changed = true;
                break;
            }
        }
        return changed;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Built-in Device Prices")]
    private void ResetDevicePricesMenu()
    {
        UnityEditor.Undo.RecordObject(this, "Reset Built-in Device Prices");
        if (ResetDefaultDevicePrices()) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

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
                foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> extra in DefaultDeviceMakers)
                {
                    DeviceKind wanted = extra.Key;
                    if (Array.Exists(devices, d => d != null && d.kind == wanted)) continue;
                    Array.Resize(ref devices, devices.Length + 1);
                    devices[devices.Length - 1] = extra.Value();
                    added = true;
                }
            }
        }

        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.Gauntlet && d.displayName != null && d.displayName.StartsWith("Infinity")) { Device fresh = CreateDefaultGauntlet(); d.displayName = fresh.displayName; d.description = fresh.description; }   // renamed (Prime Gauntlet)

        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.Recycler && d.description != null && d.description.Contains("up to 12")) d.description = CreateDefaultRecycler().description;   // 10 husks / 50% now

        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.Recycler && d.durationSeconds >= 1e8f) { Device fresh = CreateDefaultRecycler(); d.durationSeconds = fresh.durationSeconds; d.description = fresh.description; }   // (2026-10-16: the Recycler lasts 5 minutes)

        if (devices != null)
            foreach (Device d in devices)
            {
                if (d != null && d.kind == DeviceKind.Recycler && (d.radius < 1.5f || Mathf.Approximately(d.radius, 2f))) d.radius = PixelRecycler.DefaultSize;   // (2026-10-18: 2 -> 1.8, 10% smaller)   // (2026-10-18: the Recycler is twice as wide and deep by default)
                if (d != null && d.kind == DeviceKind.Recycler && d.description != null && d.description.StartsWith("A big bin that catches")) d.description = CreateDefaultRecycler().description;   // (2026-10-18: auto recycling back)
                if (d != null && d.kind == DeviceKind.SortingHopper && d.description != null && (d.description.StartsWith("A wide tray") || d.description.Contains("side tube"))) d.description = CreateDefaultSortingHopper().description;   // (2026-10-18: the funnel hopper)
                if (d != null && d.kind == DeviceKind.Recycler && d.priceGrowthPerOwned <= 0f) { Device fresh = CreateDefaultRecycler(); d.priceGrowthPerOwned = fresh.priceGrowthPerOwned; d.maxHeld = Mathf.Max(d.maxHeld, fresh.maxHeld); d.description = fresh.description; }   // (2026-10-17 rework: currency only, suction, a rising price for more Recyclers)
            }

        if (consumablePriceVersion < 1 && devices != null)
        {
            // Shop consumable repricing (2026-10-17, GameDesign section 8): every pure-currency item costs 2-3 of the six pure colours.
            ResetDefaultDevicePrices();
            consumablePriceVersion = 1;
            added = true;
        }

        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.WoodChipper && string.IsNullOrEmpty(d.shopSkillId)) { d.shopSkillId = "woodcutting"; d.shopSkillLevel = 2; }   // saved scenes: the chipper opens at Woodcutting 2

        if (addDefaultDevices && devices != null && !Array.Exists(devices, d => d != null && d.beans))
        {
            Array.Resize(ref devices, devices.Length + 1);
            devices[devices.Length - 1] = CreateDefaultMagicBeans();
            added = true;
        }

        if (addDefaultDevices && devices != null && !Array.Exists(devices, d => d != null && d.kind == DeviceKind.Seed && !d.beans && PixelClicker.IsDragonCube(d.seedType)))
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
                if (Array.Exists(devices, d => d != null && d.kind == DeviceKind.Seed && !d.beans && d.seedType == type)) continue;
                Array.Resize(ref devices, devices.Length + 1);
                devices[devices.Length - 1] = CreateDefaultSeed(type);
                added = true;
            }
        }

        if (!addDefaultPotions) return added;
        if (potions == null) potions = new Potion[0];

        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (PixelClicker.IsRareCube(type)) continue;
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
        PixelSaveRegistry.Register(this);
        PixelSaveRegistry.Register(worldPart = new WorldPart(this));
        // Escape cancels placing a device before it closes anything else.
        PixelWindows.Register(this, 100, () => IsPlacing, CancelPlacement);   // escape-only: cancels placing a device

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
        AppendAttunedDevices();
    }

    /// <summary>
    /// Adds the craft-only combo potions: one for every PAIR of pixel types and one for every TRIPLE (a 2-type potion crafted with one more
    /// pixel). Vacuum is the exception: it can't be part of a combo. They exist only at runtime (not in the Inspector list), are never sold,
    /// and are saved by their types (only the ones you hold). All of them are made here, once, so item indexes never change afterwards.
    /// </summary>
    private void AppendComboPotions()
    {
        System.Collections.Generic.List<Potion> bases = new System.Collections.Generic.List<Potion>();
        // Every block can be in a 2-type combo. 3-type combos are only made from non-standalone types: with the skill blocks in too it would be
        // ~75,000 potions built up front (they are all made here, once, so item indexes never shift).
        foreach (Potion p in potions) if (p != null && !p.craftOnly && p.type != PixelClicker.PixelType.Vacuum && !PixelClicker.IsRareCube(p.type)) bases.Add(p);
        bases.Sort((x, y) => ((int)x.type).CompareTo((int)y.type));

        System.Collections.Generic.List<Potion> all = new System.Collections.Generic.List<Potion>(potions);
        comboLookup.Clear();
        for (int i = 0; i < potions.Length; i++)
            if (potions[i] != null && potions[i].craftOnly) comboLookup[ComboKey(potions[i])] = i;

        for (int i = 0; i < bases.Count; i++)
        for (int j = i + 1; j < bases.Count; j++)
        {
            Potion a = bases[i], b = bases[j];
            long pairMask = Key(a.type, b.type);
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

            if (PixelClicker.IsStandalone(a.type) || PixelClicker.IsStandalone(b.type)) continue;   // (no 3-type combos with skill blocks, see above)
            for (int k = j + 1; k < bases.Count; k++)
            {
                Potion c = bases[k];
                if (PixelClicker.IsStandalone(c.type)) continue;
                long tripleMask = Key(a.type, b.type, c.type);
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

    /// <summary>The device kinds that can be attuned to a pixel type by crafting them with a potion.</summary>
    public static bool CanAttune(DeviceKind kind) => kind == DeviceKind.Vacuum || kind == DeviceKind.Fan || kind == DeviceKind.Magnet;

    /// <summary>
    /// Adds the craft-only ATTUNED devices: a Vacuum Device, Fan and Magnet for every pixel type that has a potion. They only affect old
    /// pixels of that one type. Runtime only (not in the Inspector list), appended after every normal device so indexes never shift;
    /// saved by name like every device.
    /// </summary>
    private void AppendAttunedDevices()
    {
        System.Collections.Generic.List<Device> all = new System.Collections.Generic.List<Device>(devices);
        foreach (Potion p in potions)
        {
            if (p == null || p.craftOnly) continue;
            int tierIndex = clicker.IndexOf(p.type);
            Color tint = tierIndex >= 0 ? clicker.Tiers[tierIndex].color : Color.white;
            foreach (DeviceKind kind in new[] { DeviceKind.Vacuum, DeviceKind.Fan, DeviceKind.Magnet })
            {
                Device src = FindDeviceOfKind(kind);
                if (src == null) continue;
                string name = "Attuned " + src.displayName + " (" + p.type + ")";
                if (all.Exists(x => x != null && x.displayName == name)) continue;
                Device d = src.Clone();
                d.attuned = true;
                d.attuneType = p.type;
                d.displayName = name;
                d.description = "Works like the " + src.displayName + ", but only affects " + p.type + " pixels. Made by crafting a " + src.displayName + " with a " + p.type + " potion.";
                d.craftOnly = true;
                d.costs = new PixelShop.PackCost[0];
                d.owned = 0;
                d.maxHeld = 0;
                d.color = Color.Lerp(src.color, tint, 0.7f);
                all.Add(d);
            }
        }
        devices = all.ToArray();
    }

    /// <summary>Item index of the attuned version of a device kind for a pixel type, or -1.</summary>
    public int FindAttunedDevice(DeviceKind kind, PixelClicker.PixelType type)
    {
        for (int i = 0; i < devices.Length; i++)
            if (devices[i] != null && devices[i].attuned && devices[i].kind == kind && devices[i].attuneType == type) return potions.Length + i;
        return -1;
    }

    private readonly System.Collections.Generic.Dictionary<long, int> comboLookup = new System.Collections.Generic.Dictionary<long, int>();
    /// <summary>Lookup key of a set of 2 or 3 pixel types (order doesn't matter): the sorted enum values packed 16 bits each, so any enum value fits.</summary>
    private static long Key(PixelClicker.PixelType a, PixelClicker.PixelType b, PixelClicker.PixelType? c = null)
    {
        int x = (int)a + 1, y = (int)b + 1, z = c.HasValue ? (int)c.Value + 1 : 0;
        if (x > y) { int t = x; x = y; y = t; }
        if (z != 0) { if (y > z) { int t = y; y = z; z = t; } if (x > y) { int t = x; x = y; y = t; } }
        return ((long)x << 32) | ((long)y << 16) | (long)z;
    }
    private static long ComboKey(Potion p) => p.hasThird ? Key(p.type, p.secondType, p.thirdType) : Key(p.type, p.secondType);

    /// <summary>Index of the 2-type combo potion for these two pixel types (either order), or -1.</summary>
    public int FindComboPotion(PixelClicker.PixelType a, PixelClicker.PixelType b)
        => a != b && comboLookup.TryGetValue(Key(a, b), out int index) ? index : -1;

    /// <summary>Index of the 3-type combo potion for these three pixel types (any order), or -1.</summary>
    public int FindComboPotion(PixelClicker.PixelType a, PixelClicker.PixelType b, PixelClicker.PixelType c)
        => a != b && b != c && a != c && comboLookup.TryGetValue(Key(a, b, c), out int index) ? index : -1;

    /// <summary>True for a combo potion (craft-only, never in the shop).</summary>
    public bool ItemCraftOnly(int item) => !IsDevice(item) && potions[item].craftOnly;

    /// <summary>True for a device that is only made by crafting (never sold in the shop or by a visitor).</summary>
    /// <summary>True for an attuned device (only affects one pixel type; made by crafting).</summary>
    public bool ItemAttuned(int item) => IsDevice(item) && devices[item - potions.Length].attuned;

    /// <summary>False for the craft-only combo potions you don't hold (there are about a thousand of them): the player's window only lists the rest.</summary>
    public bool ItemListable(int item)
    {
        if (item < 0 || item >= ItemCount) return false;
        if (IsDevice(item)) { Device d = devices[item - potions.Length]; return d != null && (!d.craftOnly || d.owned > 0); }
        return potions[item] != null && (!potions[item].craftOnly || potions[item].owned > 0);
    }

    public bool ItemNotSold(int item) => IsDevice(item) && devices[item - potions.Length].craftOnly;

    /// <summary>A device only a visiting vendor sells (not listed in the shop).</summary>
    public bool ItemVendorOnly(int item)
    {
        if (!IsDevice(item)) return false;
        Device d = devices[item - potions.Length];
        if (!d.vendorOnly) return false;
        // A skill level can open a vendor-only item in the shop (Woodcutting 2 -> the Wood Chipper).
        return string.IsNullOrEmpty(d.shopSkillId) || d.shopSkillLevel <= 0 || PixelSkills.LevelOfId(d.shopSkillId) < d.shopSkillLevel;
    }

    /// <summary>True for an item whose shop sale is tied to a skill level (the Tinkerer does not sell those).</summary>
    public bool ItemSkillGated(int item) => IsDevice(item) && !string.IsNullOrEmpty(devices[item - potions.Length].shopSkillId);

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
        if (RightPressed()) rightDownAt = Time.unscaledTime;
        if (!IsPlacing && !PixelBank.HoseOn && !PixelClicker.GodMode && Time.frameCount != placeEndFrame && RightHeld() && Time.unscaledTime - rightDownAt >= removeStartDelay && !PointerOverUI())
            target = DeviceUnderPointer();   // (only after removeStartDelay: a quick tap is the device panel's, PixelDeviceCard)

        // Only a ring fills round the cursor (no text), a buildup sound rises, and the device glows red as it fills.
        bool done = PixelHold.Update(this, target, removeHoldSeconds, "", removeMeterColor, true, "remove_buildup");
        if (glowDevice != null && glowDevice != target) glowDevice.SetRemoveGlow(0f);
        if (done)
        {
            RemoveDevice(target);
            glowDevice = null;
            return;
        }
        if (target != null) target.SetRemoveGlow(PixelHold.ProgressOf(target));
        glowDevice = target;
    }

    private PixelPlacedDevice glowDevice;

    /// <summary>Removes a placed device the way the right-button hold does (it shrinks away glowing red; sound device_remove). Also the device panel's Remove button.</summary>
    public void RemoveDevice(PixelPlacedDevice target)
    {
        if (target == null || target.IsRemoving) return;
        target.SetRemoveGlow(1f);   // (it shrinks away glowing)
        target.RemoveNow();
        PixelAudio.Play("device_remove");
    }

    /// <summary>The placed device under the pointer (nearest, and only when nothing of the clicker cube is in front of it; null over UI or while placing). Used by PixelRightClick and the device card.</summary>
    public PixelPlacedDevice DeviceAtPointer()
    {
        if (IsPlacing || PixelBank.HoseOn || PixelClicker.GodMode || PointerOverUI() || clicker == null) return null;
        return DeviceUnderPointer();
    }
    private static readonly RaycastHit[] cubeHits = new RaycastHit[64];   // (reused by DeviceUnderPointer)

    /// <summary>The placed device under the mouse (nearest first). Looks at the device's visible parts, not its range markings. 'onlyDisarmed' = only devices still waiting for their first click.</summary>
    private PixelPlacedDevice DeviceUnderPointer(bool onlyDisarmed = false)
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null) return null;
        // (this runs every frame: with no devices, or none under the mouse, nothing else is done - the ray against the cube is only cast when a device was hit)
        System.Collections.Generic.IReadOnlyList<PixelPlacedDevice> all = PixelPlacedDevice.All;
        if (all.Count == 0) return null;
        Ray ray = cam.ScreenPointToRay(PointerPosition());

        PixelPlacedDevice best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < all.Count; i++)
        {
            PixelPlacedDevice device = all[i];
            if (device == null || device.IsRemoving || (onlyDisarmed && device.Armed)) continue;
            if (device.HitTest(ray, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                best = device;
            }
        }
        if (best == null) return null;

        // The clicker cube in front of a device wins: a right click / hold on the cube (Block Hold) must not remove the device under it.
        if (clicker.PixelTransform != null)
        {
            int n = Physics.RaycastNonAlloc(ray, cubeHits, 1000f, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                if (cubeHits[i].distance <= bestDistance && cubeHits[i].transform.IsChildOf(clicker.PixelTransform)) return null;   // (a device only counts when it is nearer than the cube)
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
        PixelSaveRegistry.Unregister(this);
        PixelSaveRegistry.Unregister(worldPart);
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
            Device dev = devices[item - potions.Length];
            int held = dev.maxHeld; // Combo Fuel 3, Ghost Bait 1, Pet Treat 5, placeable devices unlimited
            if (held > 0) held += Mathf.RoundToInt(OperationBonus(dev.kind, PixelOperationStat.Storage)); // Operation upgrades
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
            if (BuffPotionAllowed(i)) candidates.Add(i);
        if (candidates.Count == 0) return -1;

        int pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        potionName = potions[pick].displayName;
        ApplyPotion(pick, durationMultiplier);
        return pick;
    }

    /// <summary>Can this potion be a free buff (the Ghost minigame)? Its pixel is unlocked, it is not craft-only and its pixel is in the rotation being played: with a skill pack in play only that pack's own blocks, otherwise the standard rotation (not the extra shop pixels while a pack is in play).</summary>
    private bool BuffPotionAllowed(int i) => !potions[i].craftOnly && clicker.IsUnlocked(potions[i].type) && PixelSkills.InCurrentRotation(potions[i].type);

    /// <summary>Picks a random buff potion (unlocked pixel type in the rotation in play, not craft-only) without starting it. -1 if none.</summary>
    public int PickRandomBuff()
    {
        System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < potions.Length; i++)
            if (BuffPotionAllowed(i)) candidates.Add(i);
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

    /// <summary>How an owned count is shown: an infinity sign under Infinite resources (the dev tools' 99999), else the number.</summary>
    public static string OwnedText(int owned) => PixelClicker.InfiniteResources && owned >= 99999 ? "\u221E" : owned.ToString();

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
    /// <summary>A device with a rising price (Device.priceGrowthPerOwned): its costs times growth ^ (how many are held + standing).</summary>
    private PixelShop.PackCost[] Laddered(int item, PixelShop.PackCost[] costs)
    {
        if (!IsDevice(item) || costs == null) return costs;
        int index = item - potions.Length;
        Device d = devices[index];
        if (d == null || d.priceGrowthPerOwned <= 1f) return costs;
        int have = d.owned;
        foreach (PixelPlacedDevice p in PixelPlacedDevice.All) if (p != null && !p.IsRemoving && p.DeviceIndex == index) have++;
        if (have <= 0) return costs;
        double factor = System.Math.Pow(d.priceGrowthPerOwned, have);
        PixelShop.PackCost[] up = new PixelShop.PackCost[costs.Length];
        for (int i = 0; i < costs.Length; i++)
        {
            PixelShop.PackCost c = costs[i];
            up[i] = c == null ? null : new PixelShop.PackCost { type = c.type, amount = System.Math.Ceiling(c.amount * factor), minigameCurrency = c.minigameCurrency };
        }
        return up;
    }

    public PixelShop.PackCost[] ItemCosts(int item)
    {
        if (IsDevice(item) && devices[item - potions.Length].kind == DeviceKind.Seed && clicker != null && !IsDragonSeed(item))
        {
            int t = clicker.IndexOf(devices[item - potions.Length].seedType);
            if (t < 0) return new PixelShop.PackCost[0];
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            double rarity = seedRarityPricePower > 0f ? System.Math.Pow(1d / System.Math.Max(0.02d, tier.spawnWeight), seedRarityPricePower) : 1d;
            rarity = System.Math.Max(1d, rarity);
            double price = tier.amountPerClick * seedPriceFactor * rarity * (scalePricesWithValue ? clicker.ValueMultiplier(t) : 1d);
            return new[] { new PixelShop.PackCost { type = tier.type, amount = System.Math.Max(seedMinPrice, System.Math.Ceiling(price)) } };
        }

        if (!IsDevice(item) && potionPriceFromOutput && clicker != null && !potions[item].craftOnly)
        {
            Potion p = potions[item];
            return new[] { new PixelShop.PackCost { type = p.type, amount = PotionPriceFromOutput(p.type, p.durationSeconds) } };
        }

        PixelShop.PackCost[] baseCosts = IsDevice(item) ? devices[item - potions.Length].costs : potions[item].costs;
        baseCosts = Laddered(item, baseCosts);
        if (!scalePricesWithValue || clicker == null || baseCosts == null) return baseCosts;

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
    private float DeviceSeconds(Device d) => d.kind == DeviceKind.Vacuum ? (vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f) + OperationBonus(DeviceKind.Vacuum, PixelOperationStat.Duration) : d.durationSeconds;

    // ------------------------------------------------------------------
    // Operation upgrades (bought in the shop's Upgrades > Operation sub-tab)
    // ------------------------------------------------------------------

    private PixelShop opShop;

    /// <summary>How much the bought Operation upgrades add to a stat of a consumable (0 when there are none).</summary>
    public float OperationBonus(DeviceKind kind, PixelOperationStat stat)
    {
        if (opShop == null) opShop = PixelFind.First<PixelShop>();
        return opShop != null ? opShop.OperationBonus(kind, stat) : 0f;
    }

    /// <summary>The device entry at this index with its Operation upgrades applied (null for a bad index). The device info panel reads run time / reach from it.</summary>
    public Device EffectiveDeviceAt(int index) => devices != null && index >= 0 && index < devices.Length && devices[index] != null ? EffectiveDevice(devices[index]) : null;

    /// <summary>A copy of the device with the Operation upgrades applied (duration, reach, strength, uses...). Used whenever a device is placed, previewed or described.</summary>
    private Device EffectiveDevice(Device d)
    {
        Device e = d.Clone();
        float B(PixelOperationStat st) => OperationBonus(d.kind, st);
        switch (d.kind)
        {
            case DeviceKind.Vacuum:
                e.durationSeconds = (vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f) + B(PixelOperationStat.Duration);
                e.radius += B(PixelOperationStat.Radius);
                e.pullAcceleration += B(PixelOperationStat.Pull);
                break;
            case DeviceKind.Fan:
                e.durationSeconds += B(PixelOperationStat.Duration);
                e.radius += B(PixelOperationStat.Radius);
                e.blowAcceleration += B(PixelOperationStat.Blow);
                break;
            case DeviceKind.Sorter:
                e.durationSeconds += B(PixelOperationStat.Duration);
                e.sorterSliderMax = (d.sorterSliderMax > 0f ? d.sorterSliderMax : 3f) + B(PixelOperationStat.ForceMax);
                break;
            case DeviceKind.ChargeBooster:
                e.uses += Mathf.RoundToInt(B(PixelOperationStat.Uses));
                e.chargeCapacity = Mathf.Max(2f, e.chargeCapacity + B(PixelOperationStat.ChargeNeeded));
                e.superChargeSeconds += B(PixelOperationStat.SuperSeconds);
                break;
            case DeviceKind.Recycler:
                e.durationSeconds += B(PixelOperationStat.Duration);
                e.radius = Mathf.Max(0.5f, e.radius + B(PixelOperationStat.Radius));   // (the bin's size factor)
                break;
            case DeviceKind.LightningRod:
                e.uses += Mathf.RoundToInt(B(PixelOperationStat.Uses));
                e.strikeSeconds = Mathf.Max(0.6f, e.strikeSeconds + B(PixelOperationStat.StrikeInterval));
                e.radius += B(PixelOperationStat.Radius);
                break;
        }
        return e;
    }

    /// <summary>The first device of a kind (null if there is none).</summary>
    public Device FindDeviceOfKind(DeviceKind kind)
    {
        foreach (Device x in devices) if (x != null && !x.attuned && x.kind == kind) return x;
        return null;
    }

    /// <summary>The value of a stat before any upgrade (the number the upgrade tooltips start from).</summary>
    public float OperationBase(DeviceKind kind, PixelOperationStat stat)
    {
        Device d = FindDeviceOfKind(kind);
        if (d == null) return 0f;
        switch (stat)
        {
            case PixelOperationStat.Duration:
                if (kind == DeviceKind.Vacuum) return vacuumDeviceSeconds > 0f ? vacuumDeviceSeconds : 5f;
                if (kind == DeviceKind.PetTreat) return PixelPets.Instance != null ? PixelPets.Instance.TreatSeconds : 60f;
                return d.durationSeconds;
            case PixelOperationStat.Radius: return d.radius;
            case PixelOperationStat.Pull: return d.pullAcceleration;
            case PixelOperationStat.Blow: return d.blowAcceleration;
            case PixelOperationStat.ForceMax: return d.sorterSliderMax > 0f ? d.sorterSliderMax : 3f;
            case PixelOperationStat.Uses: return d.uses;
            case PixelOperationStat.ChargeNeeded: return d.chargeCapacity;
            case PixelOperationStat.SuperSeconds: return d.superChargeSeconds;
            case PixelOperationStat.StrikeInterval: return d.strikeSeconds;
            case PixelOperationStat.Storage: return d.maxHeld;
            case PixelOperationStat.RecyclerStreak: return PixelRecycler.DefaultStreakCap;
            case PixelOperationStat.RecyclerSuction: return PixelRecycler.DefaultSuctionRadius;
            case PixelOperationStat.Speed: return kind == DeviceKind.Recycler ? PixelRecycler.DefaultRecycleDelay : kind == DeviceKind.SortingHopper ? PixelSortingHopper.DefaultFeedInterval : 0f;
            case PixelOperationStat.Size: return kind == DeviceKind.SortingHopper ? 1f : 0f;
            case PixelOperationStat.BaitGhosts:
            {
                PixelGhostMinigame ghosts = PixelFind.First<PixelGhostMinigame>();
                return ghosts != null ? ghosts.BaitGhostCount : 3f;
            }
        }
        return 0f;
    }

    /// <summary>The pixel type an upgrade of this consumable is paid in (the pixel the consumable itself needs).</summary>
    public PixelClicker.PixelType OperationCostType(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        return d != null ? d.requiredType : PixelClicker.PixelType.White;
    }

    /// <summary>Is a consumable of this kind available to show in the Operation tab (its pixel type is unlocked, or you hold one)?</summary>
    public bool OperationListed(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        return d != null && (clicker.IsUnlocked(d.requiredType) || d.owned > 0);
    }

    /// <summary>The name of the first consumable of a kind.</summary>
    public string OperationName(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        return d != null ? d.displayName : kind.ToString();
    }

    /// <summary>The description of the first consumable of a kind (with its upgrades applied).</summary>
    public string OperationDescription(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        if (d == null) return "";
        Device e = EffectiveDevice(d);
        return (e.description ?? "").Replace("{radius}", e.radius.ToString("0.##")).Replace("{duration}", DeviceSeconds(e).ToString("0.##"));
    }

    // --- A 3D picture of each consumable for the Operation tab ---

    private bool displayOnly;   // building a model for the shop: no range markings, no timer

    /// <summary>Builds a model of a consumable at the origin (no timer, range disc or collider), or null if there is none. The caller owns it.</summary>
    public GameObject CreateDisplayModel(DeviceKind kind)
    {
        Device d = FindDeviceOfKind(kind);
        if (d == null || clicker == null) return null;
        displayOnly = true;
        GameObject root = null;
        try
        {
            Camera cam = clicker.ViewCamera;
            switch (kind)
            {
                case DeviceKind.Fan:
                    root = BuildFanObject(d, false, out _, out _);
                    break;
                case DeviceKind.Sorter:
                {
                    PixelSorterDevice.Parts parts = PixelSorterDevice.Parts.Create(clicker, d, cam, false, 1f);
                    parts.Apply(0f, 25f, false, 1, 0.55f, "x1.00");
                    root = parts.Root;
                    break;
                }
                case DeviceKind.Vacuum:
                case DeviceKind.Sprinkler:
                case DeviceKind.Magnet:
                case DeviceKind.MagnetSorter:
                case DeviceKind.QuantumOrganizer:
                case DeviceKind.StorageCrate:
                    root = BuildDeviceObject(d, false, out _, out _);
                    break;
                case DeviceKind.ChargeBooster:
                    root = BuildDeviceObject(d, false, out _, out _);
                    DecorateBoosterModel(root, d);
                    break;
                case DeviceKind.LightningRod:
                    root = BuildDeviceObject(d, false, out _, out _);
                    DecorateRodModel(root, d);
                    break;
                case DeviceKind.WoodChipper:
                    root = PixelWoodChipper.CreateDisplay(clicker);
                    break;
                case DeviceKind.Funnel:
                    root = PixelFunnel.CreateDisplay(clicker);
                    break;
                case DeviceKind.Recycler:
                    root = PixelRecycler.CreateDisplay(clicker);
                    break;
                case DeviceKind.SortingHopper:
                    root = PixelSortingHopper.CreateDisplay(clicker);
                    break;
                case DeviceKind.Sweeper:
                    root = PixelSweeper.CreateDisplay(clicker);
                    break;
                default:
                    if (PixelInstantItems.Find(kind) != null) root = BuildItemModel(kind, d);   // instant-use items (PixelInstantItems) have small item models
                    break;
            }
        }
        finally { displayOnly = false; }
        return root;
    }

    private void DecorateBoosterModel(GameObject root, Device d)
    {
        Color bright = Color.Lerp(d.color, Color.white, 0.5f);
        GameObject meter = MakePrimitive(PrimitiveType.Cube, "Meter", root.transform, new Color(0.1f, 0.1f, 0.14f, 1f), false);
        meter.transform.localScale = new Vector3(0.14f, d.bodyHeight * 1.1f, 0.14f);
        meter.transform.localPosition = new Vector3(d.bodyDiameter * 0.75f, d.bodyHeight * 0.55f, 0f);
        GameObject fill = MakePrimitive(PrimitiveType.Cube, "Fill", root.transform, bright, false);
        fill.transform.localScale = new Vector3(0.1f, d.bodyHeight * 0.7f, 0.1f);
        fill.transform.localPosition = new Vector3(d.bodyDiameter * 0.75f, d.bodyHeight * 0.4f, 0f);
        GameObject spark = MakePrimitive(PrimitiveType.Sphere, "Spark", root.transform, bright, false);
        spark.transform.localScale = Vector3.one * (d.bodyDiameter * 0.45f);
        spark.transform.localPosition = new Vector3(0f, d.bodyHeight + d.bodyDiameter * 0.3f, 0f);
    }

    private void DecorateRodModel(GameObject root, Device d)
    {
        Color bright = Color.Lerp(d.color, Color.white, 0.6f);
        GameObject tip = MakePrimitive(PrimitiveType.Sphere, "Tip", root.transform, bright, false);
        tip.transform.localScale = Vector3.one * Mathf.Max(0.3f, d.bodyDiameter * 2.4f);
        tip.transform.localPosition = new Vector3(0f, d.bodyHeight, 0f);
        GameObject foot = MakePrimitive(PrimitiveType.Cylinder, "Foot", root.transform, new Color(d.color.r * 0.4f, d.color.g * 0.4f, d.color.b * 0.4f, 1f), false);
        foot.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
        foot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
    }

    /// <summary>Simple models for the consumables that are never placed: a fuel canister, a ghost-bait orb and a pet-treat bone.</summary>
    private GameObject BuildItemModel(DeviceKind kind, Device d)
    {
        GameObject root = new GameObject(d.displayName + " (Model)");
        Color main = d.color;
        Color dark = new Color(main.r * 0.4f, main.g * 0.4f, main.b * 0.4f, 1f);
        Color bright = Color.Lerp(main, Color.white, 0.55f);
        if (kind == DeviceKind.ComboFuel)
        {
            GameObject can = MakePrimitive(PrimitiveType.Cylinder, "Canister", root.transform, main, false);
            can.transform.localScale = new Vector3(0.5f, 0.45f, 0.5f);
            can.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            GameObject band = MakePrimitive(PrimitiveType.Cylinder, "Band", root.transform, bright, false);
            band.transform.localScale = new Vector3(0.52f, 0.06f, 0.52f);
            band.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            GameObject cap = MakePrimitive(PrimitiveType.Cylinder, "Cap", root.transform, dark, false);
            cap.transform.localScale = new Vector3(0.28f, 0.07f, 0.28f);
            cap.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            GameObject flame = MakePrimitive(PrimitiveType.Sphere, "Flame", root.transform, bright, false);
            flame.transform.localScale = new Vector3(0.22f, 0.34f, 0.22f);
            flame.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        }
        else if (kind == DeviceKind.PesticideSpray)
        {
            // A spray can: a tall can, a dark cap and a little nozzle.
            GameObject can = MakePrimitive(PrimitiveType.Cylinder, "Can", root.transform, new Color(main.r, main.g, main.b, 1f), false);
            can.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            can.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            GameObject label = MakePrimitive(PrimitiveType.Cylinder, "Label", root.transform, bright, false);
            label.transform.localScale = new Vector3(0.44f, 0.14f, 0.44f);
            label.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            GameObject cap = MakePrimitive(PrimitiveType.Cylinder, "Cap", root.transform, dark, false);
            cap.transform.localScale = new Vector3(0.3f, 0.08f, 0.3f);
            cap.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            GameObject nozzle = MakePrimitive(PrimitiveType.Cube, "Nozzle", root.transform, dark, false);
            nozzle.transform.localScale = new Vector3(0.08f, 0.08f, 0.2f);
            nozzle.transform.localPosition = new Vector3(0f, 0.98f, -0.12f);
        }
        else if (kind == DeviceKind.FocusPotion)
        {
            // A round bottle with a long neck, a cork and a bright band.
            GameObject body = MakePrimitive(PrimitiveType.Sphere, "Bottle", root.transform, new Color(main.r, main.g, main.b, 1f), false);
            body.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            GameObject neck = MakePrimitive(PrimitiveType.Cylinder, "Neck", root.transform, bright, false);
            neck.transform.localScale = new Vector3(0.22f, 0.2f, 0.22f);
            neck.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            GameObject cork = MakePrimitive(PrimitiveType.Cylinder, "Cork", root.transform, new Color(0.55f, 0.38f, 0.22f, 1f), false);
            cork.transform.localScale = new Vector3(0.18f, 0.08f, 0.18f);
            cork.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            GameObject band = MakePrimitive(PrimitiveType.Cylinder, "Band", root.transform, dark, false);
            band.transform.localScale = new Vector3(0.72f, 0.04f, 0.72f);
            band.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        }
        else if (kind == DeviceKind.GhostBait)
        {
            GameObject orb = MakePrimitive(PrimitiveType.Sphere, "Orb", root.transform, new Color(main.r, main.g, main.b, 1f), false);
            orb.transform.localScale = Vector3.one * 0.8f;
            orb.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject eye = MakePrimitive(PrimitiveType.Sphere, "Eye", root.transform, new Color(0.05f, 0.05f, 0.08f, 1f), false);
                eye.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);
                eye.transform.localPosition = new Vector3(i * 0.17f, 0.58f, -0.34f);
            }
            GameObject tail = MakePrimitive(PrimitiveType.Cylinder, "Tail", root.transform, bright, false);
            tail.transform.localScale = new Vector3(0.4f, 0.12f, 0.4f);
            tail.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        }
        else if (kind == DeviceKind.Coupling)
        {
            // A flange (a short wide disc) with a collar and a green arrow on top.
            GameObject flange = MakePrimitive(PrimitiveType.Cylinder, "Flange", root.transform, main, false);
            flange.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);
            flange.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            flange.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            GameObject collar = MakePrimitive(PrimitiveType.Cylinder, "Collar", root.transform, new Color(0.25f, 0.27f, 0.3f, 1f), false);
            collar.transform.localScale = new Vector3(0.62f, 0.18f, 0.62f);
            collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            collar.transform.localPosition = new Vector3(0f, 0.5f, 0.15f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                GameObject arm = MakePrimitive(PrimitiveType.Cube, "Arrow", root.transform, new Color(0.35f, 1f, 0.45f, 1f), false);
                arm.transform.localScale = new Vector3(0.1f, 0.08f, 0.4f);
                arm.transform.localRotation = Quaternion.Euler(0f, sx * 40f, 0f);
                arm.transform.localPosition = new Vector3(sx * 0.13f, 1.02f, 0.1f);
            }
        }
        else if (kind == DeviceKind.ContainerHose)
        {
            // A coil of see-through hose: a few short tube pieces round a ring.
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                GameObject piece = MakePrimitive(PrimitiveType.Cylinder, "Hose", root.transform, main, false);
                piece.transform.localScale = new Vector3(0.18f, 0.2f, 0.18f);
                piece.transform.localRotation = Quaternion.Euler(0f, -i * 36f, 90f);
                piece.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.5f, 0.25f + i * 0.02f, Mathf.Sin(a) * 0.5f);
            }
        }
        else if (kind == DeviceKind.ConveyorBelt)
        {
            // A short belt: a dark slab with a bright stripe and a roller at each end.
            GameObject slab = MakePrimitive(PrimitiveType.Cube, "Belt", root.transform, new Color(0.14f, 0.14f, 0.17f, 1f), false);
            slab.transform.localScale = new Vector3(1.4f, 0.14f, 0.55f);
            slab.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            GameObject stripe = MakePrimitive(PrimitiveType.Cube, "Stripe", root.transform, main, false);
            stripe.transform.localScale = new Vector3(1.2f, 0.02f, 0.12f);
            stripe.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                GameObject roller = MakePrimitive(PrimitiveType.Cylinder, "Roller", root.transform, bright, false);
                roller.transform.localScale = new Vector3(0.2f, 0.28f, 0.2f);
                roller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                roller.transform.localPosition = new Vector3(sx * 0.7f, 0.2f, 0f);
            }
        }
        else if (kind == DeviceKind.Gauntlet)
        {
            // A golden cube with a gem socket on each of its faces (the six stone colours).
            GameObject cube = MakePrimitive(PrimitiveType.Cube, "Gold Cube", root.transform, main, false);
            cube.transform.localScale = Vector3.one * 0.8f;
            cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            for (int i = 0; i < 6; i++)
            {
                GameObject gem = MakePrimitive(PrimitiveType.Cube, "Socket", root.transform, PixelInfinity.StoneColor(i), false);
                gem.transform.localScale = Vector3.one * 0.26f;
                gem.transform.localPosition = new Vector3(0f, 0.5f, 0f) + dirs[i] * 0.42f;
                gem.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            }
        }
        else if (kind == DeviceKind.BaitBucket)
        {
            GameObject body = MakePrimitive(PrimitiveType.Cylinder, "Bucket", root.transform, main, false);
            body.transform.localScale = new Vector3(0.7f, 0.3f, 0.7f);
            body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            GameObject rim = MakePrimitive(PrimitiveType.Cylinder, "Rim", root.transform, bright, false);
            rim.transform.localScale = new Vector3(0.76f, 0.025f, 0.76f);
            rim.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            GameObject water = MakePrimitive(PrimitiveType.Cylinder, "Water", root.transform, new Color(0.3f, 0.62f, 1f, 1f), false);
            water.transform.localScale = new Vector3(0.64f, 0.02f, 0.64f);
            water.transform.localPosition = new Vector3(0f, 0.56f, 0f);
            GameObject handle = MakePrimitive(PrimitiveType.Cube, "Handle", root.transform, dark, false);
            handle.transform.localScale = new Vector3(0.74f, 0.03f, 0.03f);
            handle.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        }
        else
        {
            GameObject shaft = MakePrimitive(PrimitiveType.Cylinder, "Shaft", root.transform, main, false);
            shaft.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
            shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            shaft.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    GameObject knob = MakePrimitive(PrimitiveType.Sphere, "Knob", root.transform, bright, false);
                    knob.transform.localScale = Vector3.one * 0.3f;
                    knob.transform.localPosition = new Vector3(sx * 0.52f, 0.4f, sz * 0.12f);
                }
        }
        return root;
    }

    public string ItemDescription(int item)
    {
        if (!IsDevice(item)) return Describe(item);

        Device d = EffectiveDevice(devices[item - potions.Length]);
        return (d.description ?? "")
            .Replace("{radius}", d.radius.ToString("0.##"))
            .Replace("{duration}", DeviceSeconds(d).ToString("0.##"));
    }

    /// <summary>Gives the player devices of a kind (a gift, e.g. Cubie's Recycler). False if there is no such device.</summary>
    public bool GiveDevice(DeviceKind kind, int amount = 1)
    {
        if (devices == null || amount <= 0) return false;
        int index = Array.FindIndex(devices, d => d != null && d.kind == kind);
        if (index < 0) return false;
        AddItem(potions.Length + index, amount);
        return true;
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
    /// <summary>The colour of a device item (used for crafting icons).</summary>
    public Color DeviceColorOf(int item) => devices[item - potions.Length].color;

    public DeviceKind DeviceKindOf(int item) => devices[item - potions.Length].kind;

    /// <summary>True for a seed item (a "device" of kind Seed: planted, not placed).</summary>
    /// <summary>Instant-use items (Combo Fuel, Ghost Bait, Pet Treat): they are not placed, so they live in the Supplies category.</summary>
    public bool IsSupplyItem(int item)
    {
        if (!IsDevice(item)) return false;
        DeviceKind k = devices[item - potions.Length].kind;
        return PixelInstantItems.Find(k) != null;   // (the list of instant-use items: PixelInstantItems)
    }

    /// <summary>0 = potion, 1 = placeable device (Utilities), 2 = seed, 3 = supply (instant-use item).</summary>
    public int CategoryOf(int item) => IsSeedItem(item) ? 2 : IsSupplyItem(item) && !UtilityTool(item) ? 3 : IsDevice(item) ? 1 : 0;

    /// <summary>An instant-use item listed with the Utilities (the Coupling and Container Hose sit next to the machines they connect).</summary>
    private bool UtilityTool(int item) { PixelInstantItems.Entry e = PixelInstantItems.Find(devices[item - potions.Length].kind); return e != null && e.withUtilities; }

    /// <summary>How many items of this kind the player holds (huge with infinite resources).</summary>
    public int OwnedOfKind(DeviceKind kind)
    {
        if (Inf) return 99999;
        int n = 0;
        foreach (Device d in devices) if (d != null && d.kind == kind) n += d.owned;
        return n;
    }

    public bool IsSeedItem(int item) => IsDevice(item) && devices[item - potions.Length].kind == DeviceKind.Seed;

    /// <summary>True for the rare Dragon Seed (typed prices, offered only by the Farmer, grows a Dragon Cube the player lacks).</summary>
    public bool IsDragonSeed(int item) => IsSeedItem(item) && !devices[item - potions.Length].beans && PixelClicker.IsDragonCube(devices[item - potions.Length].seedType);

    /// <summary>The item index of the Magic Beans, or -1.</summary>
    public int BeansItem()
    {
        for (int i = 0; i < devices.Length; i++) if (devices[i] != null && devices[i].beans) return potions.Length + i;
        return -1;
    }

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
        // Instant-use items (one data-driven list: PixelInstantItems); everything else is placed, or planted (seeds).
        PixelInstantItems.Entry instant = PixelInstantItems.Find(devices[index].kind);
        if (instant == null) return BeginPlacement(index);
        bool ok = instant.use(this, item, out string message);
        if (ok)
        {
            if (instant.usedUp && !Inf) devices[index].owned--;
            if (!string.IsNullOrEmpty(instant.stat)) PixelStats.Count(instant.stat);
            if (!string.IsNullOrEmpty(instant.sound)) PixelAudio.Play(instant.sound);
        }
        if (!string.IsNullOrEmpty(message)) PixelHints.Announce(message);
        return ok;
    }

    /// <summary>Pesticide charges one Pesticide Spray adds (PixelInstantItems).</summary>
    public int PesticideSprayCharges => Mathf.Max(1, pesticideSprayCharges);

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
    private Action<int, Vector3, float, float> robotPicked;   // set while the Robot Worker's owner is choosing a spot (nothing is used up)
    private int placeStartFrame;
    private GameObject preview;

    /// <summary>True while the player is choosing where to put a device.</summary>
    public bool IsPlacing => placingIndex >= 0;

    /// <summary>Name of the device being placed (empty when not placing).</summary>
    public string PlacingName => IsPlacing ? devices[placingIndex].displayName : "";

    /// <summary>The device's own placing message (empty = use the default one).</summary>
    public string PlacingMessage => !IsPlacing ? ""
        : devices[placingIndex].kind == DeviceKind.StorageCrate ? PixelKeys.Replace("Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)")
        : PixelKeys.Replace(devices[placingIndex].kind == DeviceKind.Sorter && sorterPhase == 1 ? devices[placingIndex].sorterBendMessage
        : devices[placingIndex].placingMessage);

    private float placingYaw, rawYaw;   // rawYaw = the turn before Shift snapping
    private bool chipperLatched;
    private Vector3 chipperLatchPoint;
    private float chipperLatchYaw;
    private PixelSorterDevice.Parts previewSorter;
    private int sorterPhase;      // 0 = turning the pipe, 1 = bending it
    private float sorterBend;

    // --- Robot Worker support ---

    /// <summary>The placeable kinds (the ones that stand in the world): the Robot Worker can keep any of these going.</summary>
    public static bool IsPlaceableKind(DeviceKind kind) =>
        kind == DeviceKind.Vacuum || kind == DeviceKind.Fan || kind == DeviceKind.Sorter ||
        kind == DeviceKind.ChargeBooster || kind == DeviceKind.LightningRod || kind == DeviceKind.Sprinkler || kind == DeviceKind.Magnet; // (the craft-only Magnetized Sorter is not robot-placed)

    /// <summary>How many of a device you hold (huge with infinite resources).</summary>
    public int DeviceOwned(int deviceIndex) => Inf ? 99999 : devices[deviceIndex].owned;

    /// <summary>Starts choosing a spot for the Robot Worker: a ghost follows the mouse like when placing, but nothing is used up. 'picked' gets (device, point, yaw, bend).</summary>
    public bool BeginRobotPlacement(int deviceIndex, Action<int, Vector3, float, float> picked)
    {
        if (picked == null || deviceIndex < 0 || deviceIndex >= devices.Length || !IsPlaceableKind(devices[deviceIndex].kind)) return false;
        if (!BeginPlacement(deviceIndex, true)) return false;
        robotPicked = picked;
        return true;
    }

    /// <summary>A see-through ghost of a device at a spot (the Robot Worker's job marker). The caller owns it.</summary>
    public GameObject CreateGhost(int deviceIndex, Vector3 point, float yaw, float bend)
    {
        Device d = EffectiveDevice(devices[deviceIndex]);
        Camera cam = clicker.ViewCamera;
        if (d.kind == DeviceKind.Sorter)
        {
            PixelSorterDevice.Parts parts = PixelSorterDevice.Parts.Create(clicker, d, cam, true, previewOpacity);
            parts.Apply(yaw, bend, true, devices[deviceIndex].sorterDefaultForce);
            return parts.Root;
        }
        GameObject ghost = d.kind == DeviceKind.Fan ? BuildFanObject(d, true, out _, out _) : BuildDeviceObject(d, true, out _, out _);
        ghost.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return ghost;
    }

    /// <summary>Puts a working device in the world for the Robot Worker (uses one up). Null if you have none.</summary>
    public PixelPlacedDevice RobotPlace(int deviceIndex, Vector3 point, float yaw, float bend)
    {
        if (deviceIndex < 0 || deviceIndex >= devices.Length) return null;
        Device d = devices[deviceIndex];
        if (!Inf && d.owned <= 0) return null;
        Device effective = EffectiveDevice(d);
        PixelPlacedDevice placed = d.kind == DeviceKind.Sorter
            ? SpawnDevice(deviceIndex, Vector3.zero, 0f, effective.durationSeconds, yaw, bend, -1)
            : SpawnDevice(deviceIndex, point, yaw, UsesKind(d.kind) ? effective.uses : DeviceSeconds(effective), 0f, 0f, -1);
        if (placed == null) return null;
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);
        PixelAudio.Play("device_place");
        return placed;
    }

    /// <summary>Starts placing a device: a cylinder follows the mouse until you click the floor. Returns false if you own none.</summary>
    public bool BeginPlacement(int deviceIndex) => BeginPlacement(deviceIndex, false);

    private bool BeginPlacement(int deviceIndex, bool forRobot)
    {
        if (IsPlacing || deviceIndex < 0 || deviceIndex >= devices.Length) return false;
        if (!forRobot && !Inf && devices[deviceIndex].owned <= 0) return false;

        placingIndex = deviceIndex;
        placeStartFrame = Time.frameCount;
        Camera cam = clicker.ViewCamera;
        placingYaw = cam != null ? cam.transform.eulerAngles.y : 0f; // a fan starts out blowing away from the camera
        if (devices[deviceIndex].kind == DeviceKind.Funnel) placingYaw = 0f;   // (the funnel's turn is only a nudge on top of "away from the cube")
        rawYaw = placingYaw;
        previewSorter = null;
        if (devices[deviceIndex].kind == DeviceKind.Sorter)
        {
            placingYaw = 0f; // for the sorter this is the pipe's angle around the ring (0 = pointing right on screen)
            sorterPhase = 0;
            sorterBend = 0f;
            previewSorter = PixelSorterDevice.Parts.Create(clicker, EffectiveDevice(devices[deviceIndex]), cam, true, previewOpacity);
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
                ? BuildFanObject(EffectiveDevice(devices[deviceIndex]), true, out _, out _)
                : BuildDeviceObject(EffectiveDevice(devices[deviceIndex]), true, out _, out _);
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
        robotPicked = null;
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

        PixelSnap.Configure(shiftSnapGridCubes, shiftSnapAngleDegrees);
        bool hasPoint = TryGetPlacePoint(out Vector3 point);
        if (hasPoint && PixelSnap.Active) point = PixelSnap.Point(point, clicker.OldPixelWorldSize);   // snap to the grid (hold Shift for free placement)
        preview.SetActive(hasPoint);
        if (hasPoint) preview.transform.position = point;

        // A fan can be turned with the mouse wheel or Q / E.
        if (devices[placingIndex].kind == DeviceKind.Fan || devices[placingIndex].kind == DeviceKind.MagnetSorter || devices[placingIndex].kind == DeviceKind.StorageCrate || devices[placingIndex].kind == DeviceKind.WoodChipper || devices[placingIndex].kind == DeviceKind.Funnel || devices[placingIndex].kind == DeviceKind.Recycler || devices[placingIndex].kind == DeviceKind.SortingHopper)
        {
            rawYaw += TurnInput();
            placingYaw = PixelSnap.Active ? PixelSnap.Yaw(rawYaw) : rawYaw;   // snap the turn to angle steps unless Shift is held (the raw turn keeps counting so Q / E stay smooth)
            preview.transform.rotation = Quaternion.Euler(0f, placingYaw, 0f);
        }
        chipperLatched = false;

        if (hasPoint && devices[placingIndex].kind == DeviceKind.Funnel)
        {
            // The funnel's output goes where you point (kept within reach, the spout turned away from the cube plus your turn); a belt opening in front of it latches on.
            PixelFunnel funnel = preview.GetComponent<PixelFunnel>();
            if (funnel != null)
            {
                funnel.Adjust(point, placingYaw, out Vector3 funnelPoint, out float funnelYaw);
                chipperLatched = true; chipperLatchPoint = funnelPoint; chipperLatchYaw = funnelYaw;
                preview.transform.SetPositionAndRotation(funnelPoint, Quaternion.Euler(0f, funnelYaw, 0f));
            }
        }

        if (devices[placingIndex].kind == DeviceKind.SortingHopper)
        {
            // Near a free Recycler the hopper's ghost latches onto its back (turned and sized like the bin); elsewhere it stands where you point.
            preview.transform.localScale = Vector3.one * PixelSortingHopper.UnlatchedSize;   // (sized for a default Recycler until it latches)
            if (hasPoint && PixelSortingHopper.TrySnap(clicker, point, devices[placingIndex].radius, out Vector3 hopperPoint, out float hopperYaw, out float hopperSize))
            {
                chipperLatched = true; chipperLatchPoint = hopperPoint; chipperLatchYaw = hopperYaw;
                preview.transform.SetPositionAndRotation(hopperPoint, Quaternion.Euler(0f, hopperYaw, 0f));
                preview.transform.localScale = Vector3.one * hopperSize;
            }
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
        if (d.beans)
        {
            // Magic Beans: a beanstalk with a Dragon Cube (and a handful of other cubes) instead of a sprout.
            if (PixelBeanstalk.Current != null) { PixelHints.Announce("A beanstalk is already growing - pick its cubes first"); return; }
            int dragonTier = clicker.PickMissingDragonCube();
            if (dragonTier < 0 && Inf) dragonTier = clicker.PickAnyDragonCube();
            if (dragonTier < 0)
            {
                PixelHints.Announce("You already hold all seven Dragon Cubes - the beans have nothing to grow");
                EndPlacement();
                return;
            }
            PixelBeanstalk.Plant(clicker, point, dragonTier, false);
            if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);
            PixelStats.Count("beans.planted");
            EndPlacement();
            return;
        }
        int tier = clicker.IndexOf(d.seedType);
        if (PixelClicker.IsDragonCube(d.seedType))
        {
            tier = clicker.PickMissingDragonCube(); // a Dragon Cube you don't hold
            if (tier < 0 && Inf) tier = clicker.PickAnyDragonCube();   // infinite resources (testing): any of the seven, even one you hold
            if (tier < 0)
            {
                PixelHints.Announce("You already hold all seven Dragon Cubes - the Dragon Seed has nothing left to grow");
                EndPlacement();
                return;
            }
        }
        if ((!Inf && d.owned <= 0) || tier < 0) { EndPlacement(); return; }
        if (!clicker.SeedSpotFree(point))
        {
            PixelHints.Announce("There is already a seed there - plant it a little further away");
            return;
        }
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
        Camera cam = clicker.ViewCamera;

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
        if (robotPicked != null)
        {
            Action<int, Vector3, float, float> picked = robotPicked;
            float yaw = placingYaw, bend = sorterBend;
            EndPlacement();
            picked(index, Vector3.zero, yaw, bend);
            return;
        }
        if (!Inf) d.owned = Mathf.Max(0, d.owned - 1);

        PixelPlacedDevice placedSorter = SpawnDevice(index, Vector3.zero, 0f, EffectiveDevice(d).durationSeconds, placingYaw, sorterBend, -1);
        if (placedSorter != null) placedSorter.Disarm();

        EndPlacement();
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(index);
    }

    private void PlaceDevice(Vector3 point)
    {
        int index = placingIndex;
        Device d = devices[index];
        if (chipperLatched && (d.kind == DeviceKind.Funnel || d.kind == DeviceKind.SortingHopper)) { point = chipperLatchPoint; placingYaw = chipperLatchYaw; }
        if (robotPicked != null)
        {
            Action<int, Vector3, float, float> picked = robotPicked;
            float yaw = placingYaw;
            EndPlacement();
            picked(index, point, yaw, 0f);
            return;
        }
        bool movingFunnel = d.kind == DeviceKind.Funnel && PixelFunnel.Exists;   // placing the funnel again just moves it
        if (!Inf && !movingFunnel) d.owned = Mathf.Max(0, d.owned - 1);

        Device effective = EffectiveDevice(d);
        PixelPlacedDevice placed = SpawnDevice(index, point, placingYaw, UsesKind(d.kind) ? effective.uses : DeviceSeconds(effective), 0f, 0f, -1);
        if (placed != null && d.kind != DeviceKind.StorageCrate && d.kind != DeviceKind.WoodChipper && d.kind != DeviceKind.Funnel && d.kind != DeviceKind.Recycler && d.kind != DeviceKind.SortingHopper && d.kind != DeviceKind.Sweeper) placed.Disarm();   // (the crate needs no switching on) starts grey and idle until you left-click it

        EndPlacement();
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(index);
    }

    /// <summary>Builds a working device in the world (placing it, or restoring it from a save).</summary>
    private PixelPlacedDevice SpawnDevice(int index, Vector3 point, float yaw, float duration, float aim, float bend, int force)
    {
        Device d = EffectiveDevice(devices[index]);
        Camera cam = clicker.ViewCamera;
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
        else if (d.kind == DeviceKind.MagnetSorter)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            PixelMagnetSorterDevice sorter = root.AddComponent<PixelMagnetSorterDevice>();
            sorter.Init(clicker, cam, d, timer, timerFormat, duration, shrinkSeconds, root.transform.Find("Mouth"), d.bodyDiameter, d.bodyHeight);
            result = sorter;
        }
        else if (d.kind == DeviceKind.WoodChipper)
        {
            GameObject root = PixelWoodChipper.Place(clicker, point, yaw);
            PixelChipperDevice chipperDevice = root.AddComponent<PixelChipperDevice>();
            chipperDevice.Init(clicker, cam, shrinkSeconds);
            result = chipperDevice;
        }
        else if (d.kind == DeviceKind.Recycler)
        {
            GameObject root = PixelRecycler.Place(clicker, point, yaw, d.radius);   // (d.radius = the bin's size factor, Operation: Bin Size)
            PixelRecyclerDevice recyclerDevice = root.AddComponent<PixelRecyclerDevice>();   // (the countdown; the behaviour is in PixelRecycler)
            recyclerDevice.Init(clicker, cam, duration, shrinkSeconds);
            result = recyclerDevice;
        }
        else if (d.kind == DeviceKind.Sweeper)
        {
            GameObject root = PixelSweeper.Place(clicker, point, yaw);
            PixelChipperDevice sweeperDevice = root.AddComponent<PixelChipperDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelSweeper)
            sweeperDevice.Init(clicker, cam, shrinkSeconds);
            result = sweeperDevice;
        }
        else if (d.kind == DeviceKind.SortingHopper)
        {
            GameObject root = PixelSortingHopper.Place(clicker, point, yaw, d.radius);   // (d.radius = how far from a Recycler's back it latches on)
            PixelChipperDevice hopperDevice = root.AddComponent<PixelChipperDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelSortingHopper)
            hopperDevice.Init(clicker, cam, shrinkSeconds);
            result = hopperDevice;
        }
        else if (d.kind == DeviceKind.Funnel)
        {
            GameObject root = PixelFunnel.Place(clicker, point, yaw);
            PixelFunnelDevice funnelDevice = root.AddComponent<PixelFunnelDevice>();   // (the thin permanent-device stub; all the behaviour is in PixelFunnel)
            funnelDevice.Init(clicker, cam, shrinkSeconds);
            result = funnelDevice;
        }
        else if (d.kind == DeviceKind.StorageCrate)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro crateTimer);
            if (crateTimer != null) crateTimer.gameObject.SetActive(false);
            root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            PixelStorageCrate crate = root.AddComponent<PixelStorageCrate>();
            crate.Init(clicker, cam, d, shrinkSeconds);
            result = crate;
        }
        else if (d.kind == DeviceKind.QuantumOrganizer)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.position = point;
            PixelQuantumDevice organizer = root.AddComponent<PixelQuantumDevice>();
            organizer.Init(clicker, cam, d, timer, timerFormat, duration, shrinkSeconds);
            result = organizer;
        }
        else if (d.kind == DeviceKind.Magnet)
        {
            GameObject root = BuildDeviceObject(d, false, out Transform topPoint, out TextMeshPro timer);
            root.transform.position = point;
            PixelMagnetDevice magnet = root.AddComponent<PixelMagnetDevice>();
            magnet.Init(clicker, cam, topPoint, timer, timerFormat, duration, d.radius, d.pullAcceleration, d.absorbDistance, shrinkSeconds, d.bodyHeight);
            result = magnet;
        }
        else if (d.kind == DeviceKind.Sprinkler)
        {
            GameObject root = BuildDeviceObject(d, false, out _, out TextMeshPro timer);
            root.transform.position = point;
            PixelSprinklerDevice sprinkler = root.AddComponent<PixelSprinklerDevice>();
            sprinkler.Init(clicker, cam, timer, timerFormat, duration, shrinkSeconds, d.radius, d.sprayInterval);
            result = sprinkler;
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
        result.AttuneTier = d.attuned ? clicker.IndexOf(d.attuneType) : -1;
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
        public string extra;    // device-specific text (the Magnetized Sorter's box contents and pipes)
        public string couplings;   // the couplings fitted on the device's sockets (PixelCouplings.Export)
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
            state.couplings = PixelCouplings.Export(placed.gameObject);
            if (placed is PixelChargeBooster booster) state.aim = booster.Charge;
            if (placed is PixelMagnetSorterDevice magSorter) state.extra = magSorter.ExportState();
            if (placed is PixelQuantumDevice organizer) state.extra = organizer.ExportState();
            if (placed is PixelStorageCrate storageCrate) state.extra = storageCrate.ExportState();
            PixelRecycler recycler = placed.GetComponent<PixelRecycler>();
            if (recycler != null) state.extra = recycler.ExportState();   // the husks waiting in a Recycler
            PixelSortingHopper hopper = placed.GetComponent<PixelSortingHopper>();
            if (hopper != null) state.extra = hopper.ExportState();       // the Sorting Hopper's chosen type
            PixelSweeper sweeper = placed.GetComponent<PixelSweeper>();
            if (sweeper != null) state.extra = sweeper.ExportState();     // the Sweeper's wind left
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
        if (restored != null && state.disarmed && !(restored is PixelStorageCrate) && !(restored is PixelChipperDevice) && !(restored is PixelRecyclerDevice)) restored.Disarm();
        if (restored is PixelSorterDevice restoredSorter && state.forceValue > 0f) restoredSorter.SetForceValue(state.forceValue);
        if (restored is PixelMagnetSorterDevice restoredMag) restoredMag.ImportState(state.extra);
        if (restored is PixelQuantumDevice restoredOrganizer) restoredOrganizer.ImportState(state.extra);
        PixelRecycler restoredRecycler = restored != null ? restored.GetComponent<PixelRecycler>() : null;
        if (restoredRecycler != null) restoredRecycler.ImportState(state.extra);
        PixelSortingHopper restoredHopper = restored != null ? restored.GetComponent<PixelSortingHopper>() : null;
        if (restoredHopper != null) restoredHopper.ImportState(state.extra);
        PixelSweeper restoredSweeper = restored != null ? restored.GetComponent<PixelSweeper>() : null;
        if (restoredSweeper != null) restoredSweeper.ImportState(state.extra);
        if (restored is PixelStorageCrate restoredCrate) restoredCrate.ImportState(state.extra);
        if (restored != null) PixelCouplings.Import(restored.gameObject, state.couplings);   // its couplings go back on its sockets
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
    private bool TryGetPlacePoint(out Vector3 point) => TryGetFloorPoint(PointerPosition(), out point);

    /// <summary>The spot on the floor under a screen position (not on the cube or an old pixel). Also used by the Robot Worker.</summary>
    public bool TryGetFloorPoint(Vector2 screenPosition, out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = clicker.ViewCamera;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPosition);
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
        if (d.kind == DeviceKind.Funnel)
        {
            suckPoint = null; timer = null;
            return isPreview ? PixelFunnel.CreateGhost(clicker) : displayOnly ? PixelFunnel.CreateDisplay(clicker) : PixelFunnel.Place(clicker, Vector3.zero, 0f);
        }
        if (d.kind == DeviceKind.WoodChipper)
        {
            suckPoint = null; timer = null;
            return isPreview ? PixelWoodChipper.CreateGhost(clicker) : displayOnly ? PixelWoodChipper.CreateDisplay(clicker) : PixelWoodChipper.Place(clicker, Vector3.zero, 0f);
        }
        if (d.kind == DeviceKind.Recycler)
        {
            suckPoint = null; timer = null;
            if (isPreview)
            {
                GameObject ghost = PixelRecycler.CreateGhost(clicker);
                ghost.transform.localScale = Vector3.one * Mathf.Max(0.5f, d.radius > 0f ? d.radius : 1f);   // the placing ghost shows the real size (Operation: Bin Size), like Place does
                return ghost;
            }
            return displayOnly ? PixelRecycler.CreateDisplay(clicker) : PixelRecycler.Place(clicker, Vector3.zero, 0f);
        }
        if (d.kind == DeviceKind.Sweeper)
        {
            suckPoint = null; timer = null;
            return isPreview ? PixelSweeper.CreateGhost(clicker) : displayOnly ? PixelSweeper.CreateDisplay(clicker) : PixelSweeper.Place(clicker, Vector3.zero, 0f);
        }
        if (d.kind == DeviceKind.SortingHopper)
        {
            suckPoint = null; timer = null;
            return isPreview ? PixelSortingHopper.CreateGhost(clicker) : displayOnly ? PixelSortingHopper.CreateDisplay(clicker) : PixelSortingHopper.Place(clicker, Vector3.zero, 0f, d.radius);
        }

        GameObject root = new GameObject(isPreview ? d.displayName + " (Preview)" : d.displayName);

        if (d.kind == DeviceKind.Vacuum)
        {
            BuildVacuumBody(root, d, isPreview);
        }
        else if (d.kind == DeviceKind.Sprinkler)
        {
            BuildSprinklerBody(root, d, isPreview);
        }
        else if (d.kind == DeviceKind.Magnet)
        {
            BuildMagnetBody(root, d, isPreview);
        }
        else if (d.kind == DeviceKind.QuantumOrganizer)
        {
            BuildQuantumBody(root, d, isPreview);
        }
        else if (d.kind == DeviceKind.StorageCrate)
        {
            BuildCrateBody(root, d, isPreview);
        }
        else if (d.kind == DeviceKind.MagnetSorter)
        {
            BuildMagnetSorterBody(root, d, isPreview);
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
        if (showRange && !displayOnly && d.kind != DeviceKind.MagnetSorter && d.kind != DeviceKind.QuantumOrganizer && d.kind != DeviceKind.StorageCrate)
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
        if (!isPreview && !displayOnly)
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
    /// <summary>A flat base, a short post and a spinning head with two nozzles ("Spinner" with "Nozzle 0/1" children, used by <see cref="PixelSprinklerDevice"/>).</summary>
    private void BuildSprinklerBody(GameObject root, Device d, bool isPreview)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        Color main = d.color; main.a = opacity;
        Color metal = new Color(0.62f, 0.66f, 0.72f, opacity);
        float H = d.bodyHeight;

        GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Material m = clicker.CreateVisualMaterial(color, isPreview);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(d.bodyDiameter * 1.4f, 0.04f, d.bodyDiameter * 1.4f), metal);
        Part(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, H * 0.5f, 0f), new Vector3(d.bodyDiameter * 0.28f, H * 0.5f, d.bodyDiameter * 0.28f), main);
        Transform spinner = new GameObject("Spinner").transform;
        spinner.SetParent(root.transform, false);
        spinner.localPosition = new Vector3(0f, H, 0f);
        Part(PrimitiveType.Sphere, "Hub", spinner, Vector3.zero, Vector3.one * d.bodyDiameter * 0.55f, main);
        Part(PrimitiveType.Cube, "Arm", spinner, Vector3.zero, new Vector3(d.bodyDiameter * 1.5f, d.bodyDiameter * 0.14f, d.bodyDiameter * 0.14f), metal);
        for (int i = 0; i < 2; i++)
        {
            GameObject nozzle = Part(PrimitiveType.Sphere, "Nozzle " + i, spinner, new Vector3((i == 0 ? 1f : -1f) * d.bodyDiameter * 0.75f, 0f, 0f), Vector3.one * d.bodyDiameter * 0.2f, metal);
        }
    }

    /// <summary>An upright horseshoe magnet standing on its two silver tips: a red arch, two legs and a small base plate.</summary>
    private void BuildMagnetBody(GameObject root, Device d, bool isPreview)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        Color red = d.color; red.a = opacity;
        Color silver = new Color(0.78f, 0.8f, 0.86f, opacity);
        Color dark = new Color(0.25f, 0.27f, 0.32f, opacity);
        float D = d.bodyDiameter, H = d.bodyHeight, leg = D * 0.26f;

        GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Material m = clicker.CreateVisualMaterial(color, isPreview);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Part(PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(D * 1.3f, 0.025f, D * 1.3f), dark);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * D * 0.4f;
            Part(PrimitiveType.Cube, "Leg", new Vector3(x, H * 0.5f, 0f), new Vector3(leg, H * 0.78f, leg), red);
            Part(PrimitiveType.Cube, "Tip", new Vector3(x, H * 0.12f, 0f), new Vector3(leg * 1.04f, H * 0.24f, leg * 1.04f), silver);
        }
        Part(PrimitiveType.Cube, "Arch", new Vector3(0f, H - leg * 0.5f, 0f), new Vector3(D * 0.8f + leg, leg, leg), red);
        Part(PrimitiveType.Cube, "Arch Cap", new Vector3(0f, H + leg * 0.02f, 0f), new Vector3(D * 0.8f + leg * 0.6f, leg * 0.25f, leg * 0.7f), silver);
    }

    /// <summary>A grey box, a short tube and a red horseshoe magnet head at the front (+Z, with an empty 'Mouth' child at its gap) and, unless it is only a picture, the translucent arc it pulls from.</summary>
    /// <summary>The Quantum Organizer: a glowing pad inside four low walls with taller corner posts ('D' = the pad's inner side, 'H' = the wall height).</summary>
    private void BuildQuantumBody(GameObject root, Device d, bool isPreview)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        float D = d.bodyDiameter, H = d.bodyHeight, wall = 0.12f;
        Color baseColor = new Color(0.12f, 0.1f, 0.2f, opacity);
        Color padColor = new Color(0.25f, 0.9f, 1f, opacity);
        Color wallColor = new Color(d.color.r, d.color.g, d.color.b, opacity);
        Color rimColor = new Color(0.6f, 1f, 1f, opacity);

        GameObject Part(string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Material m = clicker.CreateVisualMaterial(color, isPreview);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Part("Base", new Vector3(0f, 0.025f, 0f), new Vector3(D + wall * 2f + 0.1f, 0.05f, D + wall * 2f + 0.1f), baseColor);
        Part("Pad", new Vector3(0f, 0.06f, 0f), new Vector3(D, 0.02f, D), padColor);
        // Two lines across the pad, like a binding mark.
        Part("Pad Line", new Vector3(0f, 0.075f, 0f), new Vector3(D * 0.96f, 0.01f, 0.035f), rimColor);
        Part("Pad Line", new Vector3(0f, 0.075f, 0f), new Vector3(0.035f, 0.01f, D * 0.96f), rimColor);

        float o = D * 0.5f + wall * 0.5f;
        Part("Wall N", new Vector3(0f, H * 0.5f, o), new Vector3(D + wall * 2f, H, wall), wallColor);
        Part("Wall S", new Vector3(0f, H * 0.5f, -o), new Vector3(D + wall * 2f, H, wall), wallColor);
        Part("Wall E", new Vector3(o, H * 0.5f, 0f), new Vector3(wall, H, D), wallColor);
        Part("Wall W", new Vector3(-o, H * 0.5f, 0f), new Vector3(wall, H, D), wallColor);
        Part("Rim N", new Vector3(0f, H + 0.015f, o), new Vector3(D + wall * 2f, 0.03f, wall * 1.2f), rimColor);
        Part("Rim S", new Vector3(0f, H + 0.015f, -o), new Vector3(D + wall * 2f, 0.03f, wall * 1.2f), rimColor);
        Part("Rim E", new Vector3(o, H + 0.015f, 0f), new Vector3(wall * 1.2f, 0.03f, D), rimColor);
        Part("Rim W", new Vector3(-o, H + 0.015f, 0f), new Vector3(wall * 1.2f, 0.03f, D), rimColor);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Part("Post", new Vector3(sx * o, H * 0.65f, sz * o), new Vector3(wall * 1.7f, H * 1.3f, wall * 1.7f), rimColor);
    }

    private void BuildCrateBody(GameObject root, Device d, bool isPreview)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        float W = d.bodyDiameter, cell = W / 5f, Dp = cell * 2f, H = d.bodyHeight, t = 0.06f;
        Color wood = new Color(d.color.r, d.color.g, d.color.b, opacity);
        Color dark = new Color(d.color.r * 0.55f, d.color.g * 0.55f, d.color.b * 0.55f, opacity);
        Color trim = new Color(0.85f, 0.75f, 0.45f, opacity);

        void Part(string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Material m = clicker.CreateVisualMaterial(color, isPreview);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Part("Floor", new Vector3(0f, 0.03f, 0f), new Vector3(W + t * 2f, 0.06f, Dp + t * 2f), dark);
        float ox = W * 0.5f + t * 0.5f, oz = Dp * 0.5f + t * 0.5f;
        Part("Wall N", new Vector3(0f, H * 0.5f, oz), new Vector3(W + t * 2f, H, t), wood);
        Part("Wall S", new Vector3(0f, H * 0.5f, -oz), new Vector3(W + t * 2f, H, t), wood);
        Part("Wall E", new Vector3(ox, H * 0.5f, 0f), new Vector3(t, H, Dp), wood);
        Part("Wall W", new Vector3(-ox, H * 0.5f, 0f), new Vector3(t, H, Dp), wood);
        Part("Divider", new Vector3(0f, H * 0.45f, 0f), new Vector3(W, H * 0.9f, t * 0.7f), trim);
        for (int i = 1; i < 5; i++)
            Part("Divider", new Vector3(-W * 0.5f + i * cell, H * 0.45f, 0f), new Vector3(t * 0.7f, H * 0.9f, Dp), trim);
        // A carrying handle on each short end (the +X / -X sides): two posts and a grip bar (shared PixelCarryHandles). Click and drag one to move the crate.
        PixelCarryHandles.BuildParts(Part, W * 0.5f + t, Dp, H * 0.55f, 0.34f, opacity);
        Part("Rim N", new Vector3(0f, H + 0.012f, oz), new Vector3(W + t * 2f, 0.025f, t * 1.4f), trim);
        Part("Rim S", new Vector3(0f, H + 0.012f, -oz), new Vector3(W + t * 2f, 0.025f, t * 1.4f), trim);
        Part("Rim E", new Vector3(ox, H + 0.012f, 0f), new Vector3(t * 1.4f, 0.025f, Dp), trim);
        Part("Rim W", new Vector3(-ox, H + 0.012f, 0f), new Vector3(t * 1.4f, 0.025f, Dp), trim);
    }

    private void BuildMagnetSorterBody(GameObject root, Device d, bool isPreview)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        Color boxColor = d.color; boxColor.a = opacity;
        Color metal = new Color(0.6f, 0.63f, 0.7f, opacity);
        Color red = new Color(0.85f, 0.2f, 0.2f, opacity);
        Color silver = new Color(0.82f, 0.84f, 0.9f, opacity);
        float D = d.bodyDiameter, H = d.bodyHeight;

        GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Material m = clicker.CreateVisualMaterial(color, isPreview);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Part(PrimitiveType.Cube, "Box", new Vector3(0f, H * 0.5f, 0f), new Vector3(D, H, D), boxColor);
        Part(PrimitiveType.Cube, "Lid", new Vector3(0f, H + 0.03f, 0f), new Vector3(D * 1.06f, 0.07f, D * 1.06f), metal);
        for (int i = 0; i < 3; i++)   // the three exit ports on the back
            Part(PrimitiveType.Cylinder, "Port", new Vector3((i - 1) * D * 0.3f, H * 0.55f, -D * 0.5f), new Vector3(0.2f, 0.04f, 0.2f), metal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Tube from the box to the magnet head.
        float tubeLen = D * 0.55f;
        Part(PrimitiveType.Cylinder, "Intake Tube", new Vector3(0f, H * 0.45f, D * 0.5f + tubeLen * 0.5f), new Vector3(0.24f, tubeLen * 0.5f, 0.24f), metal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Horseshoe head (lying on its side, opening forward): two legs reaching forward with silver tips, an arch behind them.
        float z = D * 0.5f + tubeLen;
        float y = H * 0.45f, gap = 0.34f, leg = 0.2f;
        for (int s = -1; s <= 1; s += 2)
        {
            Part(PrimitiveType.Cube, "Magnet Leg", new Vector3(s * gap, y, z + 0.2f), new Vector3(leg, leg, 0.55f), red);
            Part(PrimitiveType.Cube, "Magnet Tip", new Vector3(s * gap, y, z + 0.52f), new Vector3(leg * 1.05f, leg * 1.05f, 0.14f), silver);
        }
        Part(PrimitiveType.Cube, "Magnet Arch", new Vector3(0f, y, z - 0.1f), new Vector3(gap * 2f + leg, leg, leg), red);

        GameObject mouth = new GameObject("Mouth");
        mouth.transform.SetParent(root.transform, false);
        mouth.transform.localPosition = new Vector3(0f, y, z + 0.55f);

        if (displayOnly) return;

        // The pull arc on the floor: a flat fan in front of the magnet.
        GameObject arc = new GameObject("Range", typeof(MeshFilter), typeof(MeshRenderer));
        arc.transform.SetParent(root.transform, false);
        arc.transform.localPosition = new Vector3(0f, 0.03f, z + 0.55f);
        Mesh mesh = new Mesh { name = "Pull Arc" };
        int steps = 24;
        float half = Mathf.Clamp(d.arcDegrees, 20f, 170f) * 0.5f * Mathf.Deg2Rad;
        Vector3[] v = new Vector3[steps + 2];
        int[] t = new int[steps * 3];
        v[0] = Vector3.zero;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(-half, half, i / (float)steps);
            v[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * d.radius;
        }
        for (int i = 0; i < steps; i++) { t[i * 3] = 0; t[i * 3 + 1] = i + 2; t[i * 3 + 2] = i + 1; }
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        arc.GetComponent<MeshFilter>().sharedMesh = mesh;
        Color fan = d.color; fan.a = rangeOpacity;
        Renderer ar = arc.GetComponent<Renderer>();
        Material fanMat = clicker.CreateVisualMaterial(fan, true);
        if (fanMat != null) ar.sharedMaterial = fanMat;
        ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ar.receiveShadows = false;
    }

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
        if (showRange && !displayOnly)
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
        if (!isPreview && !displayOnly)
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
    public float TurnInput()
    {
        float degrees = 0f;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current != null && !PixelInput.ControlHeld()) degrees += Mathf.Sign(Mouse.current.scroll.ReadValue().y) * (Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f ? 1f : 0f) * rotateStepDegrees;
#else
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && !PixelInput.ControlHeld()) degrees += Mathf.Sign(wheel) * rotateStepDegrees;   // (Ctrl + wheel zooms the camera)
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
