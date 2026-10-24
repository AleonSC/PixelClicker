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
///
/// Part files (partial class): PixelConsumablesTypes.cs (Potion / DeviceKind / Device), PixelConsumablesDefaults.cs (built-in table),
/// PixelConsumablesItems.cs (item list, prices, Operation), PixelConsumablesModels.cs (3D models), PixelConsumablesPlacing.cs (placing), PixelConsumablesSpawn.cs (SpawnDevice), PixelConsumablesSaving.cs (saving placed devices).
/// </summary>
public partial class PixelConsumables : MonoBehaviour
{
    // ---- progress: the inventory lives in the GameState (PixelGameState.Current.inventory) ----
    // What you HOLD: Potion.owned / bonusCap and Device.owned read and write the state by key (no stored copy here). What is out IN THE
    // WORLD (placed devices, the running potion) copies itself into the state on PixelGameState.Saving and is rebuilt from it on Loaded
    // (once the husks are back, before the tester machines: a Solar Panel links to a placed device).
    private static InventoryState Inv => PixelGameState.Current.inventory;

    private static int SecondOf(Potion p) => p.craftOnly ? (int)p.secondType + 1 : 0;
    private static int ThirdOf(Potion p) => p.craftOnly && p.hasThird ? (int)p.thirdType + 1 : 0;

    // A save is about to be written: the world part goes into the state.
    private void OnSaving()
    {
        InventoryState inv = Inv;
        inv.placed = GetPlacedDevices();
        inv.hasActive = IsActive;
        if (IsActive)
        {
            Potion running = Get(ActiveIndex);
            inv.activeType = (int)running.type;
            inv.activeSecond = SecondOf(running);
            inv.activeThird = ThirdOf(running);
            inv.activeRemaining = Remaining;
        }
    }

    // A save was loaded: what you hold is in the state already; the old world part goes and the saved one is rebuilt.
    private void OnLoaded()
    {
        StopActive();
        CancelPlacement();
        ClampAllHeld();   // (the Operation Storage levels are in place too)
        ClearPlacedDevices();
        InventoryState inv = Inv;
        foreach (PlacedState placed in inv.placed) RestorePlacedDevice(placed);
        if (!inv.hasActive) return;
        for (int i = 0; i < Count; i++)
        {
            Potion p = Get(i);
            if ((int)p.type != inv.activeType || SecondOf(p) != inv.activeSecond || ThirdOf(p) != inv.activeThird) continue;
            RestoreActive(i, inv.activeRemaining);
            break;
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        PotionDrunk = null;
        DevicePlaced = null;
        HoveringDisarmed = false;
    }

    /// <summary>True while the mouse is over a placed device that is still waiting for its first click (PixelClicker then ignores clicks, so the click switches the device on instead).</summary>
    public static bool HoveringDisarmed { get; private set; }


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

    [SerializeField, HideInInspector] private int consumablePriceVersion;   // 1 = the 2026-10-17 repricing (2-3 pure colours per item; see ResetDefaultDevicePrices), 2 = the 2026-10-18 dev-only items, 3 = Conveyor Belt Kit for everyone, 4 = Junction Box on sale, 5 = Quantum Organizer reach 24, 6 = kinds 29-34 renumbered (merge), 7 = Coupling dev-only

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
    [Tooltip("Placing snaps the spot to a grid of this many husk widths (also used by the Dev Hub tester); hold Shift for free placement. 0 = the coded default (1).")]
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
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        PixelGameState.Saving += OnSaving;
        PixelGameState.Loaded += OnLoaded;
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
        UseDescriptionPlaceholders();
        ApplyShopText();   // names, descriptions and prices from Resources/ShopText.txt (before the attuned copies are made from them)
        NamePotionsByTier();
        AppendComboPotions();
        AppendAttunedDevices();
    }

    /// <summary>
    /// Device texts saved in the scene still carry the numbers they were written with ("Hold up to 5", "Adds 3 Pesticide charges", "husks"):
    /// swap those for the placeholders ItemDescription fills in from the real settings, and 'old pixel' for 'husk'. A text without them is left alone.
    /// </summary>
    private void UseDescriptionPlaceholders()
    {
        foreach (Device d in devices)
        {
            if (d == null) continue;
            if (!string.IsNullOrEmpty(d.description))
            {
                string t = d.description;
                if (d.maxHeld > 0) t = System.Text.RegularExpressions.Regex.Replace(t, @"Hold up to \d+", "Hold up to {held}");
                if (d.kind == DeviceKind.PesticideSpray) t = System.Text.RegularExpressions.Regex.Replace(t, @"Adds \d+ Pesticide charges", "Adds {charges} Pesticide charges");
                if (d.kind == DeviceKind.LightningRod) t = System.Text.RegularExpressions.Regex.Replace(t, @"after \d+ strikes", "after {uses} strikes");
                d.description = PixelClicker.HuskWording(t);
            }
            if (!string.IsNullOrEmpty(d.placingMessage)) d.placingMessage = PixelClicker.HuskWording(d.placingMessage);
            // (belts join machines through couplings now, not by latching onto an opening)
            if (d.description != null) d.description = d.description.Replace("(a conveyor belt opening in front of the chute latches on)", "(fit a coupling on the chute's socket to send them down a belt)");
            if (d.placingMessage != null) d.placingMessage = d.placingMessage
                .Replace("(a conveyor belt opening latches on)", "(a coupling on the chute's socket sends them down a belt)")
                .Replace("a conveyor belt opening in front of it latches on", "fit a coupling on its output socket to send the husks down a belt");
        }
    }

    /// <summary>The name players see for a pixel type: its tier's display name ("Termite Hive"), not the code name ("HornetNest").</summary>
    private string TierName(PixelClicker.PixelType type) => clicker != null ? clicker.DisplayNameOf(type) : PixelClicker.TypeName(type);   // (PixelClicker.DisplayNameOf)

    /// <summary>A plain potion still carrying the automatic code-name title ("HornetNest Potion") gets its tier's name instead (a name typed in the Inspector stays).</summary>
    private void NamePotionsByTier()
    {
        foreach (Potion p in potions)
            if (p != null && !p.craftOnly && p.displayName == PixelClicker.TypeName(p.type) + " Potion") p.displayName = TierName(p.type) + " Potion";
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
                displayName = TierName(a.type) + " + " + TierName(b.type) + " Potion",
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
                    displayName = TierName(a.type) + " + " + TierName(b.type) + " + " + TierName(c.type) + " Potion",
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
                string name = "Attuned " + src.displayName + " (" + TierName(p.type) + ")";
                if (all.Exists(x => x != null && x.displayName == name)) continue;
                Device d = src.Clone();
                d.attuned = true;
                d.attuneType = p.type;
                d.displayName = name;
                d.description = "Works like the " + src.displayName + ", but only affects " + TierName(p.type) + " husks. Made by crafting a " + src.displayName + " with a " + TierName(p.type) + " potion.";
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

    /// <summary>Does the Inventory list this item (held, and not a craft-only potion / attuned copy you have none of)? The Inventory and the 3D consumables window both ask this.</summary>
    public bool ShownInInventory(int item) => item >= 0 && item < ItemCount && ItemOwned(item) > 0 && (!ItemCraftOnly(item) || Get(item).owned > 0) && (!ItemAttuned(item) || GetDevice(item - Count).owned > 0);

    /// <summary>The kind of a device item (null for a potion).</summary>
    public DeviceKind? ItemKind(int item) => IsDevice(item) ? devices[item - potions.Length].kind : (DeviceKind?)null;

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

    /// <summary>Never sold, by the shop or a visitor: craft-only devices and Dev Tools-only ones (Device.devOnly).</summary>
    public bool ItemNotSold(int item) => IsDevice(item) && (devices[item - potions.Length].craftOnly || devices[item - potions.Length].devOnly);

    /// <summary>A Dev Tools-only device (Device.devOnly): not on sale anywhere, given by the Dev Hub's 'Dev items' widget.</summary>
    public bool ItemDevOnly(int item) => IsDevice(item) && !devices[item - potions.Length].attuned && devices[item - potions.Length].devOnly;

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
            target = PixelRightClick.PressOwner == PixelRightClick.Owner.Device ? DeviceUnderPointer() : null;   // (only after removeStartDelay: a quick tap is the device panel's, PixelDeviceCard; a press on a husk is Husk Crush's, never a removal)

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
        if (target == null || target.IsEnding) return;
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

    // (asked 2-3 times a frame - right click, removal, arming: worked out once per frame for each of the two questions)
    private int pointerDeviceFrame = -1, pointerDisarmedFrame = -1;
    private PixelPlacedDevice pointerDevice, pointerDisarmed;

    /// <summary>The placed device under the mouse (nearest first). Looks at the device's visible parts, not its range markings. 'onlyDisarmed' = only devices still waiting for their first click.</summary>
    private PixelPlacedDevice DeviceUnderPointer(bool onlyDisarmed = false)
    {
        int frame = Time.frameCount;
        if (onlyDisarmed)
        {
            if (pointerDisarmedFrame != frame) { pointerDisarmedFrame = frame; pointerDisarmed = FindDeviceUnderPointer(true); }
            return pointerDisarmed;
        }
        if (pointerDeviceFrame != frame) { pointerDeviceFrame = frame; pointerDevice = FindDeviceUnderPointer(false); }
        return pointerDevice;
    }

    private PixelPlacedDevice FindDeviceUnderPointer(bool onlyDisarmed)
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
            if (device == null || device.IsEnding || (onlyDisarmed && device.Armed)) continue;
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
        PixelGameState.Saving -= OnSaving;
        PixelGameState.Loaded -= OnLoaded;
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
    /// <summary>Puts every held count back inside its cap (after a load, once the Operation Storage levels are known).</summary>
    private void ClampAllHeld()
    {
        for (int i = 0; i < potions.Length; i++) if (potions[i] != null) potions[i].owned = ClampHeld(i, potions[i].owned);
        for (int i = 0; i < devices.Length; i++) if (devices[i] != null) devices[i].owned = ClampDeviceHeld(i, devices[i].owned);
    }

    public int ClampHeld(int index, int owned) => maxPotionsHeld > 0 ? Mathf.Clamp(owned, 0, ItemCapacity(index)) : Mathf.Max(0, owned);

    /// <summary>How many more of an item you can hold right now (no limit when its capacity is unlimited).</summary>
    public int ItemRoom(int item)
    {
        if (Inf) return int.MaxValue;
        int cap = ItemCapacity(item);
        if (cap == int.MaxValue) return int.MaxValue;
        return Mathf.Max(0, cap - ItemOwned(item));
    }

    /// <summary>Raised when the player drinks a potion (its name). Used by the stats.</summary>
    public static event Action<string> PotionDrunk;

    /// <summary>Raised when the player places a device. Used by the stats.</summary>
    public static event Action<DeviceKind> DevicePlaced;

    /// <summary>
    /// Drinks one potion: only its pixel type spawns for its duration. Drinking another potion replaces the
    /// running one (the event log says so). Returns false if you have none or its pixel type isn't unlocked.
    /// </summary>
    public bool TryConsume(int index)
    {
        if (index < 0 || index >= potions.Length) return false;

        Potion potion = potions[index];
        if ((!Inf && potion.owned <= 0) || !clicker.IsUnlocked(potion.type)) return false;
        if (potion.craftOnly && (!clicker.IsUnlocked(potion.secondType) || (potion.hasThird && !clicker.IsUnlocked(potion.thirdType)))) return false;

        if (!Inf) potion.owned--;
        if (IsActive && Remaining > 0.5f)   // (one potion at a time: tell the player the running one ended, with its time left)
            PixelHints.Announce(ItemName(index) + " replaced " + ItemName(ActiveIndex) + " (" + Mathf.CeilToInt(Remaining) + " s were left)");
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

}
