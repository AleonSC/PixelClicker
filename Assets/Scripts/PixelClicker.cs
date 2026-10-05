using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Pixel Clicker - core clicking loop.
///
/// Flow: player clicks the 3D pixel (cube) -> currency of the active tier is added ->
/// a copy of the pixel falls away -> the real pixel re-materializes (scale-in) with the
/// colour of the active tier.
///
/// Tiers (White -> Gray -> Black) live in a single list so adding new ones
/// (e.g. R, G, B) later is just a matter of adding an entry in the Inspector
/// and, optionally, a new value at the end of <see cref="PixelType"/>.
/// </summary>
public class PixelClicker : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Types
    // ------------------------------------------------------------------

    /// <summary>Currency identifiers. Append new values at the END to keep Inspector data valid.</summary>
    public enum PixelType
    {
        White = 0,
        Gray = 1,
        Black = 2,
        Red = 3,
        Green = 4,
        Blue = 5,
        Glass = 6,
        Vacuum = 7,
        Obsidian = 8,
        Luminescent = 9,
    }

    /// <summary>How a tier becomes available.</summary>
    public enum TierUnlockMode
    {
        /// <summary>Unlocks automatically once the previous tier's lifetime total reaches Unlock Threshold.</summary>
        PreviousTierThreshold = 0,
        /// <summary>Never unlocks on its own. Unlocked by a shop purchase (or code calling UnlockTier).</summary>
        ShopOnly = 1,
    }

    /// <summary>Everything that defines one currency/tier. Fully editable in the Inspector.</summary>
    [Serializable]
    public class PixelTier
    {
        [Tooltip("Which currency this tier represents.")]
        public PixelType type = PixelType.White;

        [Tooltip("Display name used by the UI.")]
        public string displayName = "White Pixels";

        [Tooltip("Colour applied to the pixel cube while this tier is active.")]
        public Color color = Color.white;

        [Tooltip("Currency gained per click on this tier.")]
        public double amountPerClick = 1;

        [Min(1)]
        [Tooltip("How many clicks it takes to collect one pixel of this tier. Only the last click pays out (the pixel is just hit before that).")]
        public int clicksToCollect = 1;

        [Tooltip("Lifetime amount of the PREVIOUS tier needed to unlock this tier. Ignored if 'Unlocked At Start' is on.")]
        public double unlockThreshold = 10;

        [Tooltip("Tier is available from the very beginning.")]
        public bool unlockedAtStart = false;

        [Tooltip("Clicking this tier sucks up every old pixel currently in the scene and collects them again (a '+X' popup shows the total).")]
        public bool vacuum = false;

        [Tooltip("Render the pixel as see-through while this tier is active. A transparent copy of the pixel's material is made automatically " +
                 "(works with the Standard and URP Lit shaders). The tier colour's alpha sets how see-through it is.")]
        public bool translucent = false;

        [Tooltip("Make the pixel glow (emission) and light up its surroundings while this tier is active. " +
                 "Works with the Standard and URP Lit shaders.")]
        public bool glow = false;

        [Min(0f)]
        [Tooltip("How bright the glow is (multiplies the tier colour). 1 = same as the colour, 3+ = strongly glowing.")]
        public float glowIntensity = 2f;

        [Tooltip("Optional: a material to use for this tier instead of the pixel's normal one (e.g. your own glass material). Overrides 'Translucent'.")]
        public Material materialOverride;

        [Tooltip("Amount of this currency you start the game with (handy for testing the shop).")]
        public double startingAmount = 0;

        [Tooltip("How this tier unlocks: by collecting the previous tier, or only through the shop.")]
        public TierUnlockMode unlockMode = TierUnlockMode.PreviousTierThreshold;

        [Tooltip("Relative chance this tier is picked when a new pixel spawns (only used while Randomize Spawn Tier is on). 0 = never spawns.")]
        [Min(0f)] public float spawnWeight = 1f;

        [Tooltip("Optional: one TMP label showing this tier's current count. Leave empty to skip.")]
        public TMP_Text countLabel;

        [Tooltip("Optional: sound played when clicking while this tier is active. Falls back to the default click sound.")]
        public AudioClip clickSound;

        // Runtime state (visible in the Inspector for debugging, editable at runtime).
        [Header("Runtime State")]
        [Tooltip("Current spendable amount.")]
        public double count;

        [Tooltip("Total ever collected (never decreases when spending). Used for unlock thresholds.")]
        public double totalCollected;

        [Tooltip("Has this tier been unlocked?")]
        public bool unlocked;

        /// <summary>The tier colour with full opacity, for text and icons (a glass tier's colour is see-through).</summary>
        public Color UIColor => new Color(color.r, color.g, color.b, 1f);

        /// <summary>Shallow copy (used by the shop to add its reward tiers).</summary>
        public PixelTier Clone() => (PixelTier)MemberwiseClone();
    }

    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Glow (tiers with Glow ticked)")]
    [Range(0f, 1f)]
    [Tooltip("How much the glow breathes in and out. 0 = steady glow.")]
    [SerializeField] private float glowBreathAmount = 0.3f;

    [Min(0f)]
    [Tooltip("Speed of the glow breathing (cycles per second).")]
    [SerializeField] private float glowBreathSpeed = 0.8f;

    [Tooltip("Also add a real light at the pixel so a glowing tier lights up nearby old pixels and the scene.")]
    [SerializeField] private bool glowCastsLight = true;

    [Min(0f)]
    [Tooltip("Brightness of that light (multiplied by the tier's Glow Intensity).")]
    [SerializeField] private float glowLightIntensity = 1.2f;

    [Min(0.1f)]
    [Tooltip("How far the light reaches.")]
    [SerializeField] private float glowLightRange = 6f;

    [Header("Tough Pixels (Clicks To Collect > 1)")]
    [Range(0f, 0.6f)]
    [Tooltip("How much the pixel squashes when it is hit but not yet collected. 0 = no reaction.")]
    [SerializeField] private float hitPunchAmount = 0.18f;

    [Min(0.01f)]
    [Tooltip("How long the squash lasts (seconds).")]
    [SerializeField] private float hitPunchDuration = 0.12f;

    [Header("Pixel Object")]
    [Tooltip("The cube the player clicks. Defaults to this GameObject if empty.")]
    [SerializeField] private Transform pixelTransform;

    [Tooltip("Renderer of the pixel. Auto-found on the pixel if empty.")]
    [SerializeField] private Renderer pixelRenderer;

    [Tooltip("Camera used for raycasting. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Layers the click raycast can hit.")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    [Tooltip("Use a separate, always-full-size click area. Without it, clicks miss while the pixel is still scaling up after a click, which makes fast clicking lose clicks.")]
    [SerializeField] private bool fullSizeHitbox = true;

    [Tooltip("Size of the click area relative to the pixel's full size (1 = exactly the pixel, 1.2 = a bit more forgiving).")]
    [SerializeField] private float hitboxScale = 1f;

    [Tooltip("Max raycast distance.")]
    [SerializeField] private float maxRayDistance = 100f;

    [Tooltip("Name of the colour property on the material (URP Lit = _BaseColor, Built-in = _Color).")]
    [SerializeField] private string colorPropertyName = "_BaseColor";

    [Header("Tiers / Currencies")]
    [Tooltip("Ordered list of tiers. Index 0 is the first currency. Each tier unlocks from the previous one.")]
    [SerializeField] private PixelTier[] tiers =
    {
        new PixelTier { type = PixelType.White, displayName = "White Pixels", color = Color.white,
                        amountPerClick = 1, unlockedAtStart = true, unlockThreshold = 0 },
        new PixelTier { type = PixelType.Gray,  displayName = "Gray Pixels",  color = new Color(0.5f, 0.5f, 0.5f),
                        amountPerClick = 1, unlockThreshold = 10 },
        new PixelTier { type = PixelType.Black, displayName = "Black Pixels", color = Color.black,
                        amountPerClick = 1, unlockThreshold = 10 },
    };

    [Tooltip("Each new pixel spawns as a random UNLOCKED tier (weighted by each tier's Spawn Weight). Clicking it gives that tier's currency. Overrides the two settings below.")]
    [SerializeField] private bool randomizeSpawnTier = true;

    [Tooltip("If on, clicks always produce the highest unlocked tier. If off, use SetActiveTier() (e.g. from a button).")]
    [SerializeField] private bool autoUseHighestTier = true;

    [Tooltip("Index of the tier clicks currently produce (used when Auto Use Highest Tier is off).")]
    [SerializeField] private int activeTierIndex = 0;

    [Tooltip("Global multiplier applied to every click. Hook shops/upgrades into this later.")]
    [SerializeField] private double clickMultiplier = 1;

    [Header("UI Font")]
    [Tooltip("ONE font for every piece of UI in the game (count box, shop, log, dev button, popups, and the labels assigned below). " +
             "Leave empty to use each script's own font / the TextMeshPro default.")]
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("UI (optional)")]
    [Tooltip("Label for a combined summary of all unlocked currencies.")]
    [SerializeField] private TMP_Text summaryLabel;

    [Tooltip("Text format for the summary line. {0} = name, {1} = amount.")]
    [SerializeField] private string summaryLineFormat = "{0}: {1}";

    [Tooltip("Label that shows hints like 'Collect 10 White Pixels to unlock Gray'.")]
    [SerializeField] private TMP_Text nextUnlockLabel;

    [Tooltip("Text for the next-unlock hint. {0} = required amount, {1} = current tier name, {2} = next tier name.")]
    [SerializeField] private string nextUnlockFormat = "Collect {0} {1} to unlock {2}";

    [Tooltip("Text shown when everything is unlocked.")]
    [SerializeField] private string allUnlockedText = "All tiers unlocked!";

    [Header("Respawn Animation")]
    [Tooltip("Spawn a falling copy of the pixel on every click.")]
    [SerializeField] private bool spawnFallingCopy = true;

    [Header("Vacuum Pixel")]
    [Tooltip("Print a console line listing what each Vacuum click re-collected, per pixel type (for checking the numbers).")]
    [SerializeField] private bool logVacuum = true;

    [Tooltip("Seconds old pixels take to fly into the cube when a Vacuum pixel is clicked.")]
    [SerializeField] private float vacuumSuckDuration = 0.4f;

    [Tooltip("Speed curve of the suck-in (time 0..1, progress 0..1). Rising curves pull faster toward the end.")]
    [SerializeField] private AnimationCurve vacuumSuckCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Old Pixel Physics")]
    [Tooltip("Seconds before an old pixel is destroyed. 0 = never.")]
    [SerializeField] private float fallingCopyLifetime = 6f;

    [Tooltip("Max old pixels kept in the scene. The oldest is destroyed first. 0 = no cap.")]
    [SerializeField] private int maxFallingCopies = 30;

    [Tooltip("Old pixels below this world Y are destroyed (catches pixels that fall off the world).")]
    [SerializeField] private float fallingCopyKillHeight = -50f;

    [Tooltip("Random launch speed range (min, max) in units/second.")]
    [SerializeField] private Vector2 popSpeedRange = new Vector2(1.5f, 4f);

    [Tooltip("Vertical part of the launch direction (min, max). -1 = straight down, 0 = sideways, 1 = straight up. Negative values pop it toward the ground.")]
    [SerializeField] private Vector2 popVerticalRange = new Vector2(-0.6f, 0.1f);

    [Tooltip("Random spin range (min, max) in radians/second, applied around a random axis.")]
    [SerializeField] private Vector2 popSpinRange = new Vector2(1f, 6f);

    [Tooltip("Mass of an old pixel. Affects how it pushes other rigidbodies.")]
    [SerializeField] private float fallingCopyMass = 1f;

    [Tooltip("Air resistance (linear). 0 = none.")]
    [SerializeField] private float fallingCopyDrag = 0f;

    [Tooltip("Air resistance on spin.")]
    [SerializeField] private float fallingCopyAngularDrag = 0.05f;

    [Tooltip("Gravity multiplier. 0 = floats, 1 = normal gravity (uses the project's gravity setting).")]
    [SerializeField] private float gravityScale = 1f;

    [Range(0f, 1f)]
    [Tooltip("Bounciness of old pixels (0 = no bounce, 1 = perfect bounce).")]
    [SerializeField] private float bounciness = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Friction of old pixels.")]
    [SerializeField] private float friction = 0.5f;

    [Tooltip("Continuous collision stops fast pixels tunneling through thin floors. Costs slightly more.")]
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

    [Tooltip("Layer old pixels are placed on (controls what they collide with in Project Settings > Physics). -1 = leave on Default.")]
    [SerializeField] private int fallingCopyLayer = -1;

    [Tooltip("Can old pixels block the click raycast? Off = clicks pass through them to the live pixel.")]
    [SerializeField] private bool oldPixelsBlockClicks = false;

    [Tooltip("Should old pixels collide with the live floating pixel?")]
    [SerializeField] private bool collideWithLivePixel = false;

    [Tooltip("Seconds the new pixel takes to materialize.")]
    [SerializeField] private float materializeDuration = 0.25f;

    [Tooltip("Scale curve over the materialize duration (0..1 time, 0..1 scale).")]
    [SerializeField] private AnimationCurve materializeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Ignore clicks while the pixel is still materializing.")]
    [SerializeField] private bool blockClicksWhileSpawning = false;

    [Tooltip("Ignore clicks that land on UI (buttons, panels), so pressing the Shop button doesn't also collect a pixel.")]
    [SerializeField] private bool ignoreClicksOverUI = true;

    [Header("Suspended In Midair")]
    [Tooltip("Keep the pixel floating: disables gravity on any Rigidbody on the pixel and makes it kinematic.")]
    [SerializeField] private bool suspendInMidair = true;

    [Tooltip("Gently bob up and down while floating.")]
    [SerializeField] private bool hoverBob = true;

    [Tooltip("How far (world units) the pixel bobs up and down.")]
    [SerializeField] private float hoverAmplitude = 0.1f;

    [Tooltip("Bob cycles per second.")]
    [SerializeField] private float hoverSpeed = 0.5f;

    [Tooltip("Slowly spin the pixel (degrees per second per axis). Set to 0 for no spin.")]
    [SerializeField] private Vector3 idleSpin = new Vector3(0f, 20f, 0f);

    [Header("Pulsing")]
    [Tooltip("Enable the pulsing scale effect.")]
    [SerializeField] private bool pulseEnabled = true;

    [Tooltip("How much the size changes. 0.05 = +/-5% of the base scale.")]
    [SerializeField] private float pulseAmount = 0.05f;

    [Tooltip("Pulses per second.")]
    [SerializeField] private float pulseSpeed = 1f;

    [Tooltip("Shape of one pulse (time 0..1 across a cycle, value -1..1). Default is a smooth sine-like wave.")]
    [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f * Mathf.PI * 0.5f),
        new Keyframe(0.25f, 1f, 0f, 0f),
        new Keyframe(0.75f, -1f, 0f, 0f),
        new Keyframe(1f, 0f, 2f * Mathf.PI * 0.5f, 0f));

    [Tooltip("Also pulse the pixel's brightness along with its size.")]
    [SerializeField] private bool pulseBrightness = false;

    [Tooltip("Brightness change at the pulse peak (0.1 = +/-10%). Only used if Pulse Brightness is on.")]
    [SerializeField] private float brightnessAmount = 0.1f;

    [Header("Effects")]
    [Tooltip("Particle system played at the pixel on click. Its start colour is set to the tier colour.")]
    [SerializeField] private ParticleSystem clickParticles;

    [Tooltip("Audio source used for sound effects. Added automatically if missing.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Default click sound.")]
    [SerializeField] private AudioClip defaultClickSound;

    [Tooltip("Sound played when a new tier unlocks.")]
    [SerializeField] private AudioClip unlockSound;

    [Range(0f, 1f)]
    [Tooltip("Volume of all sounds.")]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Random pitch variation (+/-) per click for variety.")]
    [SerializeField] private float pitchVariation = 0.05f;

    [Header("Events")]
    [Tooltip("Fired after every successful click.")]
    public UnityEvent onPixelClicked;

    [Tooltip("Fired when a tier unlocks. Passes the tier index.")]
    public UnityEvent<int> onTierUnlocked;

    [Tooltip("Fired whenever any currency amount changes (hook UI/save systems here).")]
    public UnityEvent onCurrencyChanged;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private Vector3 baseScale = Vector3.one;
    private Coroutine materializeRoutine;
    private bool isSpawning;
    private MaterialPropertyBlock propertyBlock;
    private int colorPropertyId;
    private Vector3 basePosition;
    private float materializeFactor = 1f;
    private int hitsOnCurrentPixel;
    private float hitPunchTimer;
    private Color currentColor = Color.white;
    private Material defaultMaterial;
    private Material transparentMaterial;
    private readonly System.Collections.Generic.Dictionary<Material, Material> glowMaterials =
        new System.Collections.Generic.Dictionary<Material, Material>();
    private float activeGlow;      // 0 = the current tier doesn't glow
    private Light glowLight;
    private Transform hitbox;
    private int currentTierIndex; // tier of the pixel currently on screen (random mode)

    // Old pixels currently in the scene (used for the cap and kill height).
    private readonly System.Collections.Generic.List<Rigidbody> oldPixels = new System.Collections.Generic.List<Rigidbody>();

    /// <summary>C# event version of onCurrencyChanged, handy for other scripts.</summary>
    public event Action CurrencyChanged;

    /// <summary>Fired whenever currency is gained: (tier index, amount). Used by PixelUI for "+1" popups.</summary>
    public event Action<int, double> CurrencyGained;

    /// <summary>
    /// Fired for every collected pixel: (tier index, amount, wasAutomatic).
    /// wasAutomatic is true for clicks made by the auto clicker (see <see cref="AutoCollect"/>).
    /// </summary>
    public event Action<int, double, bool> PixelCollected;

    /// <summary>Fired when a click only damages a multi-click pixel: (tier index, hits so far, hits needed, automatic).</summary>
    public event Action<int, int, int, bool> PixelHit;

    /// <summary>Fired when a Vacuum pixel is clicked: (vacuum tier index, total amount re-collected, number of pixels).</summary>
    public event Action<int, double, int> PixelsVacuumed;

    /// <summary>Fired with the amount re-collected per tier (indexed like <see cref="Tiers"/>) when a Vacuum pixel is clicked.</summary>
    public event Action<double[]> VacuumBreakdown;

    public PixelTier[] Tiers => tiers;

    /// <summary>The shared UI font (may be null). All UI scripts use this when it is set.</summary>
    public TMP_FontAsset UIFont => uiFont;
    public Transform PixelTransform => pixelTransform;
    public Camera TargetCamera => targetCamera;
    public double ClickMultiplier { get => clickMultiplier; set => clickMultiplier = value; }

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (pixelTransform == null) pixelTransform = transform;
        if (pixelRenderer == null) pixelRenderer = pixelTransform.GetComponentInChildren<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (audioSource == null && (defaultClickSound != null || unlockSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (pixelRenderer != null) defaultMaterial = pixelRenderer.sharedMaterial;

        baseScale = pixelTransform.localScale;
        basePosition = pixelTransform.position;

        if (suspendInMidair)
        {
            Rigidbody body = pixelTransform.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.useGravity = false;
                body.isKinematic = true;
            }
        }
        if (fullSizeHitbox) BuildHitbox();

        propertyBlock = new MaterialPropertyBlock();
        colorPropertyId = Shader.PropertyToID(colorPropertyName);

        // Apply start-unlocked flags.
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].unlockedAtStart) tiers[i].unlocked = true;
            // Keep any Count typed into the Inspector; Starting Amount raises it if it's higher.
            if (tiers[i].count < tiers[i].startingAmount) tiers[i].count = tiers[i].startingAmount;
        }
    }

    private void Start()
    {
        if (randomizeSpawnTier) currentTierIndex = PickSpawnTier();
        ApplyUIFont();
        ApplyTierLook(GetClickTier());
        RefreshUI();
    }

    private void Update()
    {
        AnimatePixel();

        CleanOldPixels();

        if (Time.timeScale <= 0f) return; // paused (see PixelPauseMenu)
        if (!WasClickedThisFrame() || targetCamera == null) return;
        if (ignoreClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = targetCamera.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Old pixels are skipped unless they're allowed to block clicks.
            if (!oldPixelsBlockClicks && hit.rigidbody != null && oldPixels.Contains(hit.rigidbody)) continue;

            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform))
                Collect();
            return; // first non-ignored hit decides
        }
    }

    // ------------------------------------------------------------------
    // Old pixel housekeeping
    // ------------------------------------------------------------------

    /// <summary>Drops destroyed/out-of-bounds pixels from the list and enforces the cap.</summary>
    private void CleanOldPixels()
    {
        for (int i = oldPixels.Count - 1; i >= 0; i--)
        {
            Rigidbody b = oldPixels[i];
            if (b == null) { oldPixels.RemoveAt(i); continue; }
            if (b.position.y < fallingCopyKillHeight)
            {
                Destroy(b.gameObject);
                oldPixels.RemoveAt(i);
            }
        }
    }

    // ------------------------------------------------------------------
    // Input
    // ------------------------------------------------------------------

    private bool WasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }

    // ------------------------------------------------------------------
    // Core loop
    // ------------------------------------------------------------------

    /// <summary>Performs one click. Public so buttons or automation can call it too.</summary>
    public void Collect() => CollectInternal(false);

    /// <summary>A click made by automation (the auto clicker). Same as <see cref="Collect"/>, but flagged as automatic.</summary>
    public void AutoCollect() => CollectInternal(true);

    private void CollectInternal(bool automatic)
    {
        if (blockClicksWhileSpawning && isSpawning) return;

        int tierIndex = GetClickTierIndex();
        PixelTier tier = tiers[tierIndex];

        // Tough pixels (Clicks To Collect > 1) take several clicks; only the last one pays out.
        if (tier.clicksToCollect > 1)
        {
            hitsOnCurrentPixel++;
            if (hitsOnCurrentPixel < tier.clicksToCollect)
            {
                hitPunchTimer = hitPunchDuration;
                PlayClickEffects(tier);
                PixelHit?.Invoke(tierIndex, hitsOnCurrentPixel, tier.clicksToCollect, automatic);
                onPixelClicked?.Invoke();
                return;
            }
        }
        hitsOnCurrentPixel = 0;

        double amount = tier.amountPerClick * clickMultiplier;
        AddCurrency(tierIndex, amount);
        PixelCollected?.Invoke(tierIndex, amount, automatic);

        PlayClickEffects(tier);
        if (tier.vacuum) Vacuum(tierIndex); // before this pixel's own old copy spawns, so it isn't sucked up too
        if (spawnFallingCopy) SpawnFallingCopy(tierIndex, amount);

        // Roll the next pixel AFTER the click so a freshly unlocked tier can appear immediately.
        if (randomizeSpawnTier) currentTierIndex = PickSpawnTier();
        Materialize(GetClickTier());

        onPixelClicked?.Invoke();
    }

    /// <summary>Adds to a tier's amount (use negative via <see cref="TrySpend"/> instead) and checks unlocks.</summary>
    public void AddCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return;

        tiers[tierIndex].count += amount;
        tiers[tierIndex].totalCollected += amount;
        CurrencyGained?.Invoke(tierIndex, amount);

        CheckUnlocks();
        NotifyChanged();
    }

    /// <summary>Spends currency if the player can afford it. Use this from shops later.</summary>
    public bool TrySpend(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount < 0 || tiers[tierIndex].count < amount) return false;
        tiers[tierIndex].count -= amount;
        NotifyChanged();
        return true;
    }

    /// <summary>Convenience overload using the enum.</summary>
    public bool TrySpend(PixelType type, double amount) => TrySpend(IndexOf(type), amount);

    /// <summary>Current spendable amount of a currency.</summary>
    public double GetCount(PixelType type)
    {
        int i = IndexOf(type);
        return i >= 0 ? tiers[i].count : 0;
    }

    /// <summary>Manually select which tier clicks produce (only used when Auto Use Highest Tier is off).</summary>
    public void SetActiveTier(int index)
    {
        if (!IsValidTier(index) || !tiers[index].unlocked) return;
        activeTierIndex = index;
        ApplyTierLook(tiers[index]);
        RefreshUI();
    }

    /// <summary>Replaces one tier's saved numbers (used by PixelSaveGame). Tiers unlocked at start stay unlocked.</summary>
    public void LoadTierState(PixelType type, double count, double totalCollected, bool unlocked)
    {
        int index = IndexOf(type);
        if (index < 0) return;

        PixelTier tier = tiers[index];
        tier.count = System.Math.Max(0d, count);
        tier.totalCollected = System.Math.Max(0d, totalCollected);
        tier.unlocked = unlocked || tier.unlockedAtStart;
    }

    /// <summary>Call after <see cref="LoadTierState"/>: re-rolls the pixel on screen and refreshes every display.</summary>
    public void FinishLoad()
    {
        ClearForcedSpawnTier();
        if (randomizeSpawnTier) currentTierIndex = PickSpawnTier();
        Materialize(GetClickTier());
        NotifyChanged();
    }

    /// <summary>Unlock tier N once tier N-1's lifetime total reaches N's threshold.</summary>
    private void CheckUnlocks()
    {
        for (int i = 1; i < tiers.Length; i++)
        {
            if (tiers[i].unlocked) continue;
            if (tiers[i].unlockMode == TierUnlockMode.ShopOnly) continue;
            if (tiers[i - 1].totalCollected >= tiers[i].unlockThreshold)
            {
                tiers[i].unlocked = true;
                PlaySound(unlockSound);
                onTierUnlocked?.Invoke(i);
            }
        }
    }

    // ------------------------------------------------------------------
    // Tier helpers
    // ------------------------------------------------------------------

    private bool IsValidTier(int i) => tiers != null && i >= 0 && i < tiers.Length;

    /// <summary>Index of the tier with this currency type, or -1 if there isn't one.</summary>
    public int IndexOf(PixelType type)
    {
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].type == type) return i;
        return -1;
    }

    private int GetHighestUnlockedIndex()
    {
        // Shop tiers are skipped so buying them doesn't change the "highest tier" click behaviour.
        for (int i = tiers.Length - 1; i >= 0; i--)
            if (tiers[i].unlocked && tiers[i].unlockMode != TierUnlockMode.ShopOnly) return i;
        return 0;
    }

    /// <summary>Is the tier with this currency type unlocked?</summary>
    public bool IsUnlocked(PixelType type)
    {
        int i = IndexOf(type);
        return i >= 0 && tiers[i].unlocked;
    }

    /// <summary>Unlocks a tier directly (shops, achievements...). Returns false if invalid or already unlocked.</summary>
    public bool UnlockTier(int index)
    {
        if (!IsValidTier(index) || tiers[index].unlocked) return false;
        tiers[index].unlocked = true;
        PlaySound(unlockSound);
        onTierUnlocked?.Invoke(index);
        NotifyChanged();
        return true;
    }

    /// <summary>
    /// Makes sure a tier of this type exists, appending a locked copy of <paramref name="definition"/> if not.
    /// Returns its index. Lets the shop add Red/Green/Blue without editing the Tiers list by hand.
    /// </summary>
    public int EnsureTier(PixelTier definition)
    {
        int existing = IndexOf(definition.type);
        if (existing >= 0)
        {
            // The Tiers list already has this type: still honour a Starting Amount set on the shop's definition.
            if (definition.startingAmount > 0 && tiers[existing].startingAmount <= 0)
            {
                tiers[existing].startingAmount = definition.startingAmount;
                tiers[existing].count = definition.startingAmount;
            }
            return existing;
        }

        PixelTier copy = definition.Clone();
        copy.unlocked = false;
        // Keep Count / Total Collected typed on the shop's definition (as for normal tiers);
        // Starting Amount raises Count if it's higher.
        if (copy.count < copy.startingAmount) copy.count = copy.startingAmount;

        List<PixelTier> list = new List<PixelTier>(tiers) { copy };
        tiers = list.ToArray();
        return tiers.Length - 1;
    }

    private int forcedTierIndex = -1;

    /// <summary>True while a potion (or other effect) restricts spawning to one pixel type.</summary>
    public bool HasForcedSpawnTier => forcedTierIndex >= 0;

    /// <summary>
    /// Makes only this pixel type spawn until <see cref="ClearForcedSpawnTier"/> is called. The pixel
    /// currently shown is swapped for it straight away. Ignored if the tier is missing or locked.
    /// </summary>
    public void SetForcedSpawnTier(PixelType type)
    {
        int index = IndexOf(type);
        if (index < 0 || !tiers[index].unlocked) return;

        forcedTierIndex = index;
        if (randomizeSpawnTier && currentTierIndex != index)
        {
            currentTierIndex = index;
            Materialize(GetClickTier());
        }
    }

    /// <summary>Back to the normal weighted random spawning.</summary>
    public void ClearForcedSpawnTier() => forcedTierIndex = -1;

    /// <summary>Weighted random pick among unlocked tiers (or the forced tier while a potion is active).</summary>
    private int PickSpawnTier()
    {
        if (IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked) return forcedTierIndex;

        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].unlocked) total += Mathf.Max(0f, tiers[i].spawnWeight);

        if (total <= 0f) return GetHighestUnlockedIndex();

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!tiers[i].unlocked) continue;
            roll -= Mathf.Max(0f, tiers[i].spawnWeight);
            if (roll <= 0f) return i;
        }
        return GetHighestUnlockedIndex();
    }

    private int GetClickTierIndex()
    {
        if (randomizeSpawnTier)
            return IsValidTier(currentTierIndex) && tiers[currentTierIndex].unlocked
                ? currentTierIndex
                : GetHighestUnlockedIndex();

        if (autoUseHighestTier) return GetHighestUnlockedIndex();
        return IsValidTier(activeTierIndex) && tiers[activeTierIndex].unlocked
            ? activeTierIndex
            : GetHighestUnlockedIndex();
    }

    private PixelTier GetClickTier() => tiers[GetClickTierIndex()];

    // ------------------------------------------------------------------
    // Visuals
    // ------------------------------------------------------------------

    /// <summary>Applies float, spin and pulse every frame. Scale = base * materialize * pulse.</summary>
    /// <summary>
    /// The pixel's own collider shrinks to nothing while it materializes (it scales with the cube),
    /// so clicks landing in that moment used to miss. This invisible trigger box stays full size
    /// and follows the pixel, so every click registers.
    /// </summary>
    private void BuildHitbox()
    {
        GameObject go = new GameObject("PixelHitbox");
        go.layer = pixelTransform.gameObject.layer;
        hitbox = go.transform;
        hitbox.SetPositionAndRotation(pixelTransform.position, pixelTransform.rotation);
        hitbox.localScale = pixelTransform.lossyScale * hitboxScale;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true; // never pushes physics objects around

        BoxCollider source = pixelTransform.GetComponent<BoxCollider>();
        if (source != null)
        {
            box.center = source.center;
            box.size = source.size;
        }
    }

    private void OnDestroy()
    {
        if (hitbox != null) Destroy(hitbox.gameObject);
    }

    private void AnimatePixel()
    {
        float time = Time.time;

        if (suspendInMidair && hoverBob)
        {
            pixelTransform.position = basePosition +
                Vector3.up * (Mathf.Sin(time * hoverSpeed * Mathf.PI * 2f) * hoverAmplitude);
        }

        if (idleSpin != Vector3.zero)
            pixelTransform.Rotate(idleSpin * Time.deltaTime, Space.Self);

        if (hitbox != null) hitbox.SetPositionAndRotation(pixelTransform.position, pixelTransform.rotation);

        float pulse = 0f;
        if (pulseEnabled)
        {
            float cycle = Mathf.Repeat(time * pulseSpeed, 1f);
            pulse = pulseCurve.Evaluate(cycle); // -1..1
        }

        // Squash briefly when a tough pixel is hit but not yet collected.
        float punch = 1f;
        if (hitPunchTimer > 0f)
        {
            hitPunchTimer -= Time.deltaTime;
            punch = 1f - hitPunchAmount * Mathf.Clamp01(hitPunchTimer / Mathf.Max(0.01f, hitPunchDuration));
        }

        pixelTransform.localScale = baseScale * (materializeFactor * punch * (1f + pulse * pulseAmount));

        if (pulseEnabled && pulseBrightness)
        {
            Color pulsed = currentColor * (1f + pulse * brightnessAmount);
            pulsed.a = currentColor.a; // brightness only, keep see-through tiers see-through
            ApplyPixelColor(pulsed, false);
        }
        else if (activeGlow > 0f)
        {
            ApplyPixelColor(currentColor, false); // keeps the glow breathing
        }

        if (glowLight != null && glowLight.enabled)
        {
            float breath = 1f + glowBreathAmount * Mathf.Sin(time * glowBreathSpeed * Mathf.PI * 2f);
            glowLight.intensity = glowLightIntensity * activeGlow * breath;
        }
    }

    private void ApplyPixelColor(Color color, bool remember = true)
    {
        if (remember) currentColor = color;
        if (pixelRenderer == null) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyId, color);
        // Also set the built-in name so either pipeline works if the property name is left default.
        propertyBlock.SetColor("_Color", color);

        // Glowing tiers: emission follows the colour (and breathes). Black emission = off for everything else.
        if (activeGlow > 0f)
        {
            float breath = 1f + glowBreathAmount * Mathf.Sin(Time.time * glowBreathSpeed * Mathf.PI * 2f);
            Color emission = new Color(color.r, color.g, color.b, 1f) * (activeGlow * breath);
            propertyBlock.SetColor("_EmissionColor", emission);
        }
        else
        {
            propertyBlock.SetColor("_EmissionColor", Color.black);
        }
        pixelRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>Applies a tier's material (normal, translucent or override) and colour to the pixel.</summary>
    private void ApplyTierLook(PixelTier tier)
    {
        if (pixelRenderer != null)
        {
            Material wanted = defaultMaterial;
            if (tier.materialOverride != null) wanted = tier.materialOverride;
            else if (tier.translucent && defaultMaterial != null)
            {
                if (transparentMaterial == null) transparentMaterial = BuildTransparentMaterial(defaultMaterial);
                wanted = transparentMaterial;
            }

            // Glowing tiers need a copy of the material with emission switched on.
            if (tier.glow && tier.materialOverride == null && wanted != null) wanted = GetGlowMaterial(wanted);

            if (wanted != null && pixelRenderer.sharedMaterial != wanted) pixelRenderer.sharedMaterial = wanted;
        }

        activeGlow = tier.glow ? tier.glowIntensity : 0f;
        UpdateGlowLight(tier);
        ApplyPixelColor(tier.color);
    }

    /// <summary>Copies a material and switches it to alpha blending (Standard or URP Lit/Unlit).</summary>
    /// <summary>A copy of the material with emission enabled (cached per source material).</summary>
    private Material GetGlowMaterial(Material source)
    {
        if (glowMaterials.TryGetValue(source, out Material cached) && cached != null) return cached;

        Material copy = new Material(source) { name = source.name + " (Glow)" };
        copy.EnableKeyword("_EMISSION");
        copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        glowMaterials[source] = copy;
        return copy;
    }

    /// <summary>Creates / updates / hides the point light that goes with a glowing tier.</summary>
    private void UpdateGlowLight(PixelTier tier)
    {
        bool wanted = tier.glow && glowCastsLight && pixelTransform != null;
        if (wanted && glowLight == null)
        {
            GameObject go = new GameObject("Glow Light");
            go.transform.SetParent(pixelTransform, false);
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.shadows = LightShadows.None;
        }

        if (glowLight == null) return;
        glowLight.enabled = wanted;
        if (!wanted) return;

        glowLight.color = new Color(tier.color.r, tier.color.g, tier.color.b, 1f);
        glowLight.range = glowLightRange;
        glowLight.intensity = glowLightIntensity * tier.glowIntensity;
    }

    private static Material BuildTransparentMaterial(Material source)
    {
        Material m = new Material(source) { name = source.name + " (Transparent)" };

        if (m.HasProperty("_Surface")) // URP
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
        }
        else if (m.HasProperty("_Mode")) // Built-in Standard
        {
            m.SetFloat("_Mode", 3f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    private void Materialize(PixelTier tier)
    {
        hitsOnCurrentPixel = 0; // a fresh pixel has taken no hits yet
        if (materializeRoutine != null) StopCoroutine(materializeRoutine);
        materializeRoutine = StartCoroutine(MaterializeRoutine(tier));
    }

    private IEnumerator MaterializeRoutine(PixelTier tier)
    {
        isSpawning = true;
        ApplyTierLook(tier);
        materializeFactor = 0f;

        float t = 0f;
        while (t < materializeDuration)
        {
            t += Time.deltaTime;
            float k = materializeDuration > 0f ? Mathf.Clamp01(t / materializeDuration) : 1f;
            materializeFactor = materializeCurve.Evaluate(k);
            yield return null;
        }

        materializeFactor = 1f;
        isSpawning = false;
        materializeRoutine = null;
    }

    /// <summary>
    /// Vacuum pixel effect: every old pixel flies into the cube and its original reward is added again.
    /// </summary>
    private void Vacuum(int vacuumTierIndex)
    {
        double total = 0d;
        int count = 0;
        double[] perTier = new double[tiers.Length];

        for (int i = 0; i < oldPixels.Count; i++)
        {
            Rigidbody body = oldPixels[i];
            if (body == null) continue;

            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex))
            {
                AddCurrency(info.tierIndex, info.amount);
                perTier[info.tierIndex] += info.amount;
                total += info.amount;
                count++;
            }

            StartCoroutine(SuckRoutine(body));
        }

        oldPixels.Clear();

        if (logVacuum && count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Vacuum: re-collected " + count + " old pixels worth " + total + " (");
            for (int i = 0; i < tiers.Length; i++)
                if (perTier[i] > 0d) sb.Append(tiers[i].displayName).Append(" +").Append(perTier[i]).Append("  ");
            Debug.Log(sb.Append(")").ToString(), this);
        }

        if (count > 0)
        {
            VacuumBreakdown?.Invoke(perTier);
            PixelsVacuumed?.Invoke(vacuumTierIndex, total, count);
        }
    }

    /// <summary>Pulls one old pixel into the cube while shrinking it, then removes it.</summary>
    private IEnumerator SuckRoutine(Rigidbody body)
    {
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;

        Vector3 startPos = t.position;
        Vector3 startScale = t.localScale;
        float time = 0f;

        while (time < vacuumSuckDuration)
        {
            if (t == null) yield break; // destroyed (lifetime ran out) while flying
            time += Time.deltaTime;
            float k = vacuumSuckDuration > 0f ? Mathf.Clamp01(time / vacuumSuckDuration) : 1f;
            float eased = vacuumSuckCurve.Evaluate(k);

            t.position = Vector3.Lerp(startPos, pixelTransform.position, eased);
            t.localScale = startScale * (1f - eased);
            yield return null;
        }

        if (t != null) Destroy(t.gameObject);
    }

    /// <summary>Clones the visible pixel, adds real physics, and pops it out in a random direction.</summary>
    private void SpawnFallingCopy(int tierIndex, double amount)
    {
        // Skip if the pixel is mid-materialize and basically invisible.
        if (pixelTransform.localScale.sqrMagnitude < 0.0001f) return;

        GameObject copy = new GameObject("OldPixel");
        copy.transform.SetPositionAndRotation(pixelTransform.position, pixelTransform.rotation);
        copy.transform.localScale = pixelTransform.lossyScale;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) copy.layer = fallingCopyLayer;

        // Copy only the visuals (mesh + material) so we don't duplicate this script.
        MeshFilter srcFilter = pixelRenderer != null ? pixelRenderer.GetComponent<MeshFilter>() : null;
        if (srcFilter != null && pixelRenderer != null)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;
            MeshRenderer mr = copy.AddComponent<MeshRenderer>();
            mr.sharedMaterial = pixelRenderer.sharedMaterial;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            pixelRenderer.GetPropertyBlock(block);
            mr.SetPropertyBlock(block);
        }

        // Collider with a physics material so bounce/friction are tweakable.
        BoxCollider box = copy.AddComponent<BoxCollider>();
#if UNITY_6000_0_OR_NEWER
        box.sharedMaterial = new PhysicsMaterial("OldPixel")
        {
            bounciness = bounciness,
            dynamicFriction = friction,
            staticFriction = friction,
            bounceCombine = PhysicsMaterialCombine.Maximum,
            frictionCombine = PhysicsMaterialCombine.Average
        };
#else
        box.sharedMaterial = new PhysicMaterial("OldPixel")
        {
            bounciness = bounciness,
            dynamicFriction = friction,
            staticFriction = friction,
            bounceCombine = PhysicMaterialCombine.Maximum,
            frictionCombine = PhysicMaterialCombine.Average
        };
#endif

        // Keep the old pixel from colliding with / bumping the live one if requested.
        if (!collideWithLivePixel)
        {
            foreach (Collider live in pixelTransform.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(box, live);
        }

        Rigidbody rb = copy.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.0001f, fallingCopyMass);
        rb.useGravity = false; // gravity applied by ScaledGravity so gravityScale works
        rb.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = fallingCopyDrag;
        rb.angularDamping = fallingCopyAngularDrag;
#else
        rb.drag = fallingCopyDrag;
        rb.angularDrag = fallingCopyAngularDrag;
#endif
        if (gravityScale > 0f)
            copy.AddComponent<ScaledGravity>().scale = gravityScale;

        // Random direction: random heading on the ground plane, random vertical component.
        Vector2 flat = UnityEngine.Random.insideUnitCircle;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
        flat.Normalize();
        float vertical = UnityEngine.Random.Range(popVerticalRange.x, popVerticalRange.y);
        Vector3 direction = new Vector3(flat.x, vertical, flat.y).normalized;
        float speed = UnityEngine.Random.Range(popSpeedRange.x, popSpeedRange.y);

        rb.AddForce(direction * speed, ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                     ForceMode.VelocityChange);

        if (fallingCopyLifetime > 0f) Destroy(copy, fallingCopyLifetime);

        OldPixelInfo info = copy.AddComponent<OldPixelInfo>();
        info.tierIndex = tierIndex;
        info.amount = amount;

        oldPixels.Add(rb);
        while (maxFallingCopies > 0 && oldPixels.Count > maxFallingCopies)
        {
            Rigidbody oldest = oldPixels[0];
            oldPixels.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
    }

    private void PlayClickEffects(PixelTier tier)
    {
        if (clickParticles != null)
        {
            ParticleSystem.MainModule main = clickParticles.main;
            main.startColor = tier.color;
            clickParticles.transform.position = pixelTransform.position;
            clickParticles.Play();
        }

        PlaySound(tier.clickSound != null ? tier.clickSound : defaultClickSound);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
        audioSource.PlayOneShot(clip, soundVolume);
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private void NotifyChanged()
    {
        RefreshUI();
        onCurrencyChanged?.Invoke();
        CurrencyChanged?.Invoke();
    }

    /// <summary>Applies the shared UI font to the labels assigned to this script.</summary>
    private void ApplyUIFont()
    {
        if (uiFont == null) return;
        if (summaryLabel != null) summaryLabel.font = uiFont;
        if (nextUnlockLabel != null) nextUnlockLabel.font = uiFont;
        foreach (PixelTier t in tiers)
            if (t.countLabel != null) t.countLabel.font = uiFont;
    }

    /// <summary>Refreshes every assigned label. Safe to call any time.</summary>
    public void RefreshUI()
    {
        if (tiers == null) return;

        System.Text.StringBuilder sb = summaryLabel != null ? new System.Text.StringBuilder() : null;

        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            string amount = FormatNumber(t.count);

            if (t.countLabel != null)
            {
                t.countLabel.gameObject.SetActive(t.unlocked);
                t.countLabel.text = string.Format(summaryLineFormat, t.displayName, amount);
            }

            if (sb != null && t.unlocked)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.AppendFormat(summaryLineFormat, t.displayName, amount);
            }
        }

        if (summaryLabel != null) summaryLabel.text = sb.ToString();

        if (nextUnlockLabel != null)
        {
            int next = -1;
            for (int i = 1; i < tiers.Length; i++)
            {
                if (!tiers[i].unlocked && tiers[i].unlockMode != TierUnlockMode.ShopOnly) { next = i; break; }
            }

            nextUnlockLabel.text = next < 0
                ? allUnlockedText
                : string.Format(nextUnlockFormat, FormatNumber(tiers[next].unlockThreshold),
                                tiers[next - 1].displayName, tiers[next].displayName);
        }
    }

    /// <summary>Compact number formatting (1.2K, 3.4M ...). Replace with your own for big-number support.</summary>
    public static string FormatNumber(double value)
    {
        if (value < 1000) return Math.Floor(value).ToString("0");
        string[] suffix = { "", "K", "M", "B", "T" };
        int s = 0;
        while (value >= 1000 && s < suffix.Length - 1) { value /= 1000; s++; }
        return value.ToString("0.##") + suffix[s];
    }

    // ------------------------------------------------------------------
    // Editor helpers
    // ------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tiers != null && tiers.Length > 0)
            activeTierIndex = Mathf.Clamp(activeTierIndex, 0, tiers.Length - 1);
        materializeDuration = Mathf.Max(0f, materializeDuration);
        fallingCopyLifetime = Mathf.Max(0f, fallingCopyLifetime);
        maxFallingCopies = Mathf.Max(0, maxFallingCopies);
    }
#endif
}

/// <summary>
/// Tiny helper added at runtime to old pixels so their gravity can be scaled
/// (Unity's Rigidbody only has on/off gravity).
/// </summary>
public class ScaledGravity : MonoBehaviour
{
    [HideInInspector] public float scale = 1f;
    private Rigidbody body;

    private void Awake() => body = GetComponent<Rigidbody>();

    private void FixedUpdate()
    {
        if (body != null && !body.isKinematic)
            body.AddForce(Physics.gravity * scale, ForceMode.Acceleration);
    }
}

/// <summary>
/// Runtime tag on each old pixel: which tier it was and how much it paid out.
/// The Vacuum pixel uses it to pay that amount again.
/// </summary>
public class OldPixelInfo : MonoBehaviour
{
    [HideInInspector] public int tierIndex;
    [HideInInspector] public double amount;
}
