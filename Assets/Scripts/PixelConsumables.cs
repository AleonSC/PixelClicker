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
    }

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

        [Tooltip("Runtime: a combo potion (made by crafting). It makes its pixel type AND the second type appear, is never sold in the shop, and is created automatically.")]
        public bool craftOnly = false;

        [Tooltip("Runtime: the second pixel type of a combo potion.")]
        public PixelClicker.PixelType secondType = PixelClicker.PixelType.White;
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

    [Header("Devices")]
    [Tooltip("Placeable devices. A Vacuum Device is created for you; edit its price, radius and duration here.")]
    [SerializeField] private Device[] devices = CreateDefaultDevices();

    [Tooltip("Add the default Vacuum Device if the list has none (e.g. when this component was added before devices existed).")]
    [SerializeField] private bool addDefaultDevices = true;

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

    private static Device[] CreateDefaultDevices() => new[] { CreateDefaultDevice(), CreateDefaultFan(), CreateDefaultSorter() };

    private static Potion[] CreateDefaultPotions()
    {
        Array values = Enum.GetValues(typeof(PixelClicker.PixelType));
        Potion[] list = new Potion[values.Length];
        for (int i = 0; i < values.Length; i++)
            list[i] = CreateDefaultPotion((PixelClicker.PixelType)values.GetValue(i));
        return list;
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
            }
        }

        if (!addDefaultPotions) return added;
        if (potions == null) potions = new Potion[0];

        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
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
    /// Adds a craft-only combo potion for every pair of pixel types where at least one is a pixel that can't be toggled
    /// (White, Gray, Black, Red, Green, Blue, Glass, Luminescent) - the other can be anything, even Obsidian.
    /// They exist only at runtime (not in the Inspector list), are never sold, and are saved by their two types.
    /// </summary>
    private void AppendComboPotions()
    {
        System.Collections.Generic.List<Potion> bases = new System.Collections.Generic.List<Potion>();
        foreach (Potion p in potions) if (p != null && !p.craftOnly) bases.Add(p);

        System.Collections.Generic.List<Potion> all = new System.Collections.Generic.List<Potion>(potions);
        for (int i = 0; i < bases.Count; i++)
        for (int j = i + 1; j < bases.Count; j++)
        {
            Potion a = bases[i], b = bases[j];
            if (PixelClicker.PixelTier.IsSpecialType(a.type) && PixelClicker.PixelTier.IsSpecialType(b.type)) continue;
            if (FindComboPotion(a.type, b.type) >= 0) continue;

            bool aFirst = (int)a.type <= (int)b.type;
            Potion first = aFirst ? a : b, second = aFirst ? b : a;
            all.Add(new Potion
            {
                displayName = first.type + " + " + second.type + " Potion",
                type = first.type,
                secondType = second.type,
                craftOnly = true,
                description = "Only {pixel} and {pixel2} pixels appear for {duration} seconds. Made by crafting.",
                costs = new PixelShop.PackCost[0],
                durationSeconds = (a.durationSeconds + b.durationSeconds) * 0.5f,
            });
        }
        potions = all.ToArray();
    }

    /// <summary>Index of the combo potion for these two pixel types (either order), or -1.</summary>
    public int FindComboPotion(PixelClicker.PixelType a, PixelClicker.PixelType b)
    {
        for (int i = 0; i < potions.Length; i++)
        {
            Potion p = potions[i];
            if (p == null || !p.craftOnly) continue;
            if ((p.type == a && p.secondType == b) || (p.type == b && p.secondType == a)) return i;
        }
        return -1;
    }

    /// <summary>True for a combo potion (craft-only, never in the shop).</summary>
    public bool ItemCraftOnly(int item) => !IsDevice(item) && potions[item].craftOnly;

    /// <summary>The second pixel type of a combo potion.</summary>
    public PixelClicker.PixelType ItemSecondType(int item) => potions[item].secondType;

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

    /// <summary>The placed device under the mouse (nearest first). Looks at the device's visible parts, not its range markings.</summary>
    private PixelPlacedDevice DeviceUnderPointer()
    {
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(PointerPosition());

        PixelPlacedDevice best = null;
        float bestDistance = float.MaxValue;
        foreach (PixelPlacedDevice device in PixelPlacedDevice.All)
        {
            if (device == null || device.IsRemoving) continue;
            foreach (Renderer r in device.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.name == "Range" || r.name == "Blow Area") continue;
                if (r.bounds.IntersectRay(ray, out float distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = device;
                }
            }
        }
        return best;
    }

    private void Update()
    {
        if (IsPlacing) UpdatePlacement();
        UpdateRemoval();

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

    /// <summary>Gives the player potions (e.g. from a purchase).</summary>
    public void Add(int index, int amount = 1)
    {
        if (index < 0 || index >= potions.Length || amount <= 0) return;
        potions[index].owned += amount;
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
        if (potion.owned <= 0 || !clicker.IsUnlocked(potion.type)) return false;
        if (potion.craftOnly && !clicker.IsUnlocked(potion.secondType)) return false;

        potion.owned--;
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
        if (potion.craftOnly) clicker.SetForcedSpawnTiers(potion.type, potion.secondType);
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

    public int ItemOwned(int item) => IsDevice(item) ? devices[item - potions.Length].owned : potions[item].owned;

    public PixelShop.PackCost[] ItemCosts(int item) => IsDevice(item) ? devices[item - potions.Length].costs : potions[item].costs;

    /// <summary>The pixel type that must be unlocked before the item is listed in the shop.</summary>
    public PixelClicker.PixelType ItemRequiredType(int item) =>
        IsDevice(item) ? devices[item - potions.Length].requiredType : potions[item].type;

    public string ItemDescription(int item)
    {
        if (!IsDevice(item)) return Describe(item);

        Device d = devices[item - potions.Length];
        return (d.description ?? "")
            .Replace("{radius}", d.radius.ToString("0.##"))
            .Replace("{duration}", d.durationSeconds.ToString("0.##"));
    }

    /// <summary>Adds items to the inventory (a shop purchase).</summary>
    public void AddItem(int item, int amount = 1)
    {
        if (IsDevice(item))
        {
            if (amount > 0) devices[item - potions.Length].owned += amount;
        }
        else
        {
            Add(item, amount);
        }
    }

    /// <summary>The kind of device an item index refers to (items past the potions are devices).</summary>
    public DeviceKind DeviceKindOf(int item) => devices[item - potions.Length].kind;

    /// <summary>Takes items out of the inventory (crafting). Returns false if you don't have that many.</summary>
    public bool TryRemoveItem(int item, int amount)
    {
        if (item < 0 || item >= ItemCount || amount <= 0 || ItemOwned(item) < amount) return false;
        if (IsDevice(item)) devices[item - potions.Length].owned -= amount;
        else potions[item].owned -= amount;
        return true;
    }

    /// <summary>Right-click use: drinks a potion, or starts placing a device.</summary>
    public bool TryUseItem(int item) => IsDevice(item) ? BeginPlacement(item - potions.Length) : TryConsume(item);

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
        : devices[placingIndex].kind == DeviceKind.Sorter && sorterPhase == 1 ? devices[placingIndex].sorterBendMessage
        : devices[placingIndex].placingMessage;

    private float placingYaw;
    private PixelSorterDevice.Parts previewSorter;
    private int sorterPhase;      // 0 = turning the pipe, 1 = bending it
    private float sorterBend;

    /// <summary>Starts placing a device: a cylinder follows the mouse until you click the floor. Returns false if you own none.</summary>
    public bool BeginPlacement(int deviceIndex)
    {
        if (IsPlacing || deviceIndex < 0 || deviceIndex >= devices.Length) return false;
        if (devices[deviceIndex].owned <= 0) return false;

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

        if (LeftPressed() && hasPoint && !PointerOverUI()) PlaceDevice(point);
        else if (RightPressed()) CancelPlacement();
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
        d.owned = Mathf.Max(0, d.owned - 1);

        SpawnDevice(index, Vector3.zero, 0f, d.durationSeconds, placingYaw, sorterBend, -1);

        EndPlacement();
        DevicePlaced?.Invoke(d.kind);
        onDevicePlaced?.Invoke(index);
    }

    private void PlaceDevice(Vector3 point)
    {
        int index = placingIndex;
        Device d = devices[index];
        d.owned = Mathf.Max(0, d.owned - 1);

        SpawnDevice(index, point, placingYaw, d.durationSeconds, 0f, 0f, -1);

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
            };
            if (placed is PixelSorterDevice sorter)
            {
                state.aim = sorter.AimDegrees;
                state.bend = sorter.BendDegrees;
                state.force = sorter.Force;
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
        SpawnDevice(index, state.position, state.yaw, state.remaining, state.aim, state.bend, state.force);
    }

    /// <summary>Starts a potion again with the time it had left (no sound, nothing used up). Used when loading a save.</summary>
    public void RestoreActive(int index, float secondsLeft)
    {
        if (index < 0 || index >= potions.Length || secondsLeft <= 0f) return;
        Potion potion = potions[index];
        activeIndex = index;
        remaining = secondsLeft;
        if (potion.craftOnly) clicker.SetForcedSpawnTiers(potion.type, potion.secondType);
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
            timer.text = string.Format(timerFormat, Mathf.CeilToInt(d.durationSeconds));
            timer.fontSize = timerFontSize;
            timer.fontStyle = FontStyles.Bold;
            timer.alignment = TextAlignmentOptions.Center;
            timer.color = timerColor;
            if (clicker.UIFont != null) timer.font = clicker.UIFont;
        }

        return root;
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
            timer.text = string.Format(timerFormat, Mathf.CeilToInt(d.durationSeconds));
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
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.isPressed) degrees += rotateSpeedDegrees * Time.deltaTime;
            if (Keyboard.current.qKey.isPressed) degrees -= rotateSpeedDegrees * Time.deltaTime;
        }
#else
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) degrees += Mathf.Sign(wheel) * rotateStepDegrees;
        if (Input.GetKey(KeyCode.E)) degrees += rotateSpeedDegrees * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) degrees -= rotateSpeedDegrees * Time.deltaTime;
#endif
        return degrees;
    }

    /// <summary>Description with {pixel} and {duration} filled in.</summary>
    public string Describe(int index)
    {
        Potion potion = potions[index];
        return (potion.description ?? "")
            .Replace("{pixel}", potion.type.ToString())
            .Replace("{pixel2}", potion.secondType.ToString())
            .Replace("{duration}", potion.durationSeconds.ToString("0.##"));
    }
}
