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
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        ExternalClickBlock = false;
        GodMode = false;
        InfiniteResources = false;
        OldPixelLanded = null;
        UltraGained = null;
        abbreviateCache = -1;
    }

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
        Singularity = 10,
        Ghost = 11,
        Meteor = 12,
    }

    /// <summary>Can the player switch this pixel's spawning off (tick box in the inventory)?</summary>
    public enum SpawnSwitch
    {
        /// <summary>The special pixels (Vacuum, Obsidian, Singularity, Ghost, Meteor) can be switched off; the others can't.</summary>
        Auto = 0,
        /// <summary>The player can always switch this pixel off.</summary>
        Switchable = 1,
        /// <summary>This pixel always spawns normally.</summary>
        AlwaysOn = 2,
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

        [Tooltip("Instead of dropping and bouncing, the old pixel of this tier flies away in a straight line like a meteor.")]
        public bool flyAway = false;

        [Tooltip("Screen direction a fly-away pixel leaves in (x = right, y = up).")]
        public Vector2 flyDirection = new Vector2(1f, 0.6f);

        [Min(0f)]
        [Tooltip("Speed of a fly-away pixel (world units per second).")]
        public float flySpeed = 30f;

        [Tooltip("Optional: one TMP label showing this tier's current count. Leave empty to skip.")]
        public TMP_Text countLabel;

        [Tooltip("Optional: sound played when clicking while this tier is active. Falls back to the default click sound.")]
        public AudioClip clickSound;

        // Runtime state (visible in the Inspector for debugging, editable at runtime).
        [Tooltip("Can the player switch this pixel's spawning off with a tick box in the inventory? Auto = only the special pixels (Vacuum, Obsidian, Singularity, Ghost, Meteor).")]
        public SpawnSwitch spawnSwitch = SpawnSwitch.Auto;

        [Header("Runtime State")]
        [Tooltip("Current spendable amount.")]
        public double count;

        [Tooltip("Total ever collected (never decreases when spending). Used for unlock thresholds.")]
        public double totalCollected;

        [Tooltip("Runtime: how many times the player has spent Ultra pixels to boost this pixel type's payout (saved). See the shop's Upgrades > Pixel tab.")]
        public int ultraLevel;

        [Tooltip("How many Ultra versions of this pixel type you own (from the Ultra Pad minigame; saved). They will be used for upgrades later.")]
        public long ultraCount;

        [Tooltip("How many pixels of this type have been collected (one per payout, whatever it paid). Used by the 'times clicked' achievements.")]
        public long timesCollected;

        [Tooltip("Has this tier been unlocked?")]
        public bool unlocked;

        [Tooltip("Runtime: the player switched this pixel's spawning off (saved).")]
        public bool spawnDisabled;

        /// <summary>True for the special pixels (Vacuum, Obsidian, Singularity, Ghost, Meteor) that can have their spawning switched off.</summary>
        public static bool IsSpecialType(PixelType t) =>
            t == PixelType.Vacuum || t == PixelType.Obsidian || t == PixelType.Singularity ||
            t == PixelType.Ghost || t == PixelType.Meteor;

        /// <summary>True if the player may switch this pixel's spawning off.</summary>
        public bool CanSwitchOff
        {
            get
            {
                if (spawnSwitch == SpawnSwitch.Switchable) return true;
                if (spawnSwitch == SpawnSwitch.AlwaysOn) return false;
                return IsSpecialType(type);
            }
        }

        /// <summary>Can this pixel spawn right now (unlocked and not switched off)?</summary>
        public bool CanSpawn => unlocked && !(spawnDisabled && CanSwitchOff);

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

    [Header("Pixel Looks (per pixel type)")]
    [Tooltip("Turn the per-type looks below on or off (colour/finish, neon outline, dark matter, glass shatter).")]
    [SerializeField] private bool useLooks = true;

    [Tooltip("Extra look of each pixel type: finish, outline, dark-matter core, shatter. A type not listed here looks as its tier says.")]
    [SerializeField] private PixelLook[] looks = PixelLooks.CreateDefaults();

    [Min(0f)]
    [SerializeField, HideInInspector] private int looksVersion; // 1 = White/Gray/Black got a custom look; 2 = removed again; 3 = RGB outlines removed too; 4 = Vacuum look added; 5 = Obsidian look added; 6 = Ghost look added; 7 = RGB colour-blind marks

    [Tooltip("Shattering pixels (see Looks): how hard they must hit the ground to break.")]
    [SerializeField] private float shatterMinSpeed = 2f;

    [Range(3, 40)]
    [Tooltip("How many shards a shattering pixel breaks into.")]
    [SerializeField] private int shardCount = 12;

    [Min(0f)]
    [Tooltip("How fast the shards fly apart.")]
    [SerializeField] private float shardSpeed = 3.5f;

    [Min(0.1f)]
    [Tooltip("Seconds a shard stays before it shrinks away.")]
    [SerializeField] private float shardLifeSeconds = 1.4f;

    [Range(0.05f, 1f)]
    [Tooltip("Size of a shard compared to the pixel.")]
    [SerializeField] private float shardSize = 0.4f;

    [Tooltip("Sound id played when a pixel shatters (give it clips in PixelAudio).")]
    [SerializeField] private string shatterSoundId = "glass_shatter";

    [Header("Bright Falling Pixels (light trail)")]
    [Tooltip("Pixel types whose old (falling) pixels get a light trail and extra glow. They should also have Glow ticked on their tier.")]
    [SerializeField] private PixelType[] brightOldPixelTypes = { PixelType.Luminescent };

    [Tooltip("Leave a fading light trail behind these pixels as they fall.")]
    [SerializeField] private bool lightTrail = true;

    [Min(0.05f)]
    [Tooltip("How long the light trail lasts (seconds).")]
    [SerializeField] private float lightTrailSeconds = 0.5f;

    [Min(0.05f)]
    [Tooltip("Width of the trail, as a fraction of the old pixel's size.")]
    [SerializeField] private float lightTrailWidth = 0.7f;

    [Min(0.1f)]
    [Tooltip("Brightness of the trail colour (1 = the tier colour, higher = lighter, towards white).")]
    [SerializeField] private float lightTrailBrightness = 1.6f;

    [Min(1f)]
    [Tooltip("Extra glow on these old pixels: multiplies the tier's Glow Intensity (1 = no extra).")]
    [SerializeField] private float oldPixelGlowBoost = 2.5f;

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

    [Header("Click Limit")]
    [Min(0f)]
    [Tooltip("Shortest time (seconds) allowed between two manual clicks on the cube. A click that comes sooner is ignored. 0.07 = at most about 14 clicks per second, which stops macros that click faster than a person can. 0 = no limit. The auto clicker, the hose and minigame clicks are not affected.")]
    [SerializeField] private float minClickInterval = 0.07f;

    [Header("Ultra Boosts")]
    [Min(0f)]
    [Tooltip("Each Ultra boost level adds this much to a pixel type's payout multiplier (0.25 = +25% of its normal payout per level).")]
    [SerializeField] private float ultraBonusPerLevel = 0.25f;

    [Header("Old Pixel Landing Sound")]
    [Min(0f)]
    [Tooltip("An old pixel only makes a landing sound when it hits a surface at least this fast (world units per second), so resting or rolling pixels stay quiet.")]
    [SerializeField] private float landMinSpeed = 1.5f;

    [Min(0.1f)]
    [Tooltip("The impact speed at which the landing sound is at full volume. Slower hits are quieter.")]
    [SerializeField] private float landFullVolumeSpeed = 8f;

    [Min(0f)]
    [Tooltip("Shortest time (seconds) between landing sounds from the same old pixel (it bounces a few times).")]
    [SerializeField] private float landCooldown = 0.12f;

    [Header("Old Pixel Size")]
    [Range(0.05f, 1f)]
    [Tooltip("Full size of an old pixel as a fraction of the clickable pixel (0.5 = half size). Smaller = less visual clutter.")]
    [SerializeField] private float oldPixelScale = 0.5f;

    [Header("Old Pixel Pop-In")]
    [Range(0.01f, 1f)]
    [Tooltip("Size an old pixel starts at, as a fraction of its normal size (0.05 = tiny).")]
    [SerializeField] private float popStartScale = 0.05f;

    [Min(0f)]
    [Tooltip("Seconds an old pixel takes to grow back to full size while falling. 0 = full size at once.")]
    [SerializeField] private float popGrowSeconds = 0.35f;

    [Min(0f)]
    [Tooltip("Seconds right after spawning that an old pixel has NO collision (so quick clicks don't make them pile up). 0 = solid at once.")]
    [SerializeField] private float popNoCollisionSeconds = 0.2f;

    [Header("Old Pixel Despawn")]
    [Range(1f, 2f)]
    [Tooltip("How big an old pixel swells (times its size) just before it vanishes.")]
    [SerializeField] private float despawnSwellScale = 1.25f;

    [Min(0.01f)]
    [Tooltip("Seconds the swell takes.")]
    [SerializeField] private float despawnSwellSeconds = 0.08f;

    [Min(0.01f)]
    [Tooltip("Seconds it then takes to shrink to nothing and disappear.")]
    [SerializeField] private float despawnShrinkSeconds = 0.15f;

    [Header("Fly-Away Pixels (tiers with Fly Away on)")]
    [Min(0.1f)]
    [Tooltip("Seconds before a fly-away old pixel is removed.")]
    [SerializeField] private float flyLifetime = 3f;

    [Min(0f)]
    [Tooltip("How long the fiery trail behind a fly-away pixel lasts (seconds). 0 = no trail.")]
    [SerializeField] private float flyTrailSeconds = 0.6f;

    [Range(0f, 45f)]
    [Tooltip("Random angle (degrees) a fly-away pixel's direction may deviate.")]
    [SerializeField] private float flySpreadDegrees = 6f;

    [Tooltip("Max old pixels kept in the scene. The oldest is destroyed first. 0 = no cap.")]
    [SerializeField] private int maxFallingCopies = 30;

    [Tooltip("Max old pixels kept while Time Stop is on (the stockpile that bursts out when time resumes). 0 = no cap.")]
    [SerializeField] private int timeStopStockpileMax = 150;

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
    private bool clicksBlocked;
    private float lastManualClickTime = -99f;

    /// <summary>While true, clicks on the cube are ignored (set by the pixel bank's hose, which uses the mouse buttons itself).</summary>
    public static bool ExternalClickBlock;

    /// <summary>Dev tools "god pixel" mode: the mouse spawns / destroys pixels instead of clicking the cube.</summary>
    public static bool GodMode;

    private const string PrefRotation = "PixelClicker.Setting.Rotation";
    private const string PrefPulsing = "PixelClicker.Setting.Pulsing";
    private const string PrefBackground = "PixelClicker.Setting.RunInBackground";
    private bool allowRotation = true;
    private bool allowPulsing = true;

    /// <summary>Player setting (accessibility): turns the cube's idle rotation on or off. Remembered between sessions.</summary>
    public bool AllowRotation
    {
        get => allowRotation;
        set { allowRotation = value; PlayerPrefs.SetInt(PrefRotation, value ? 1 : 0); }
    }

    /// <summary>Player setting: keep the game running while its window is not in focus (alt-tabbed). Remembered between sessions.</summary>
    public bool RunInBackground
    {
        get => Application.runInBackground;
        set { Application.runInBackground = value; PlayerPrefs.SetInt(PrefBackground, value ? 1 : 0); }
    }

    /// <summary>Player setting (accessibility): turns the cube's pulsing (size and brightness) on or off. Remembered between sessions.</summary>
    public bool AllowPulsing
    {
        get => allowPulsing;
        set { allowPulsing = value; PlayerPrefs.SetInt(PrefPulsing, value ? 1 : 0); }
    }
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

    /// <summary>Raised on the click that finally breaks a tough pixel (just before the payout): tier, hits needed, automatic. Used to show "5/5".</summary>
    public event Action<int, int, bool> PixelFinalHit;

    /// <summary>Fired when currency is spent (tier index, amount).</summary>
    public event Action<int, double> CurrencySpent;

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

    /// <summary>Raised when an old pixel hits the floor (or any surface). Passes how hard, 0..1. Used by the sound system.</summary>
    public static event System.Action<float> OldPixelLanded;

    /// <summary>Raised whenever Ultra pixels are given (first-time tips).</summary>
    public static event System.Action UltraGained;

    internal static void RaiseOldPixelLanded(float intensity) => OldPixelLanded?.Invoke(intensity);

    /// <summary>While true (the Pixel Grabbing upgrade), a click on an old pixel is caught by it instead of passing through to the cube.</summary>
    public bool GrabEnabled { get; set; }

    /// <summary>Extra multiplier on YOUR clicks (not the auto clicker's). Set every frame by the combo meter.</summary>
    public double ManualClickBonus { get; set; } = 1d;

    /// <summary>True if every new pixel is a random unlocked tier (the normal mode).</summary>
    public bool RandomizesSpawnTier => randomizeSpawnTier;

    /// <summary>The tier clicks produce when spawning is NOT random (the highest unlocked tier, or the chosen one).</summary>
    public int FixedTierIndex
    {
        get
        {
            if (autoUseHighestTier) return GetHighestUnlockedIndex();
            return IsValidTier(activeTierIndex) && tiers[activeTierIndex].unlocked ? activeTierIndex : GetHighestUnlockedIndex();
        }
    }

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (PixelFind.First<PixelTitleScreen>() == null) gameObject.AddComponent<PixelTitleScreen>(); // start screen (Play button)
        if (PixelFind.First<PixelHints>() == null) gameObject.AddComponent<PixelHints>(); // first-time tips
        if (PixelFind.First<PixelCrashLog>() == null) gameObject.AddComponent<PixelCrashLog>(); // writes error / crash reports
        if (PixelFind.First<PixelViewBounds>() == null) gameObject.AddComponent<PixelViewBounds>(); // keeps old pixels on screen
        if (PixelFind.First<PixelMinigameLimits>() == null) gameObject.AddComponent<PixelMinigameLimits>(); // how many minigames run at once
        if (pixelTransform == null) pixelTransform = transform;
        if (pixelRenderer == null) pixelRenderer = pixelTransform.GetComponentInChildren<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (audioSource == null && (defaultClickSound != null || unlockSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (looksVersion < 3)
        {
            // White, gray, black and RGB are back to their plain flat look: drop the entries earlier versions added.
            if (looks != null)
            {
                System.Collections.Generic.List<PixelLook> kept = new System.Collections.Generic.List<PixelLook>();
                foreach (PixelLook l in looks)
                    if (l != null && l.type != PixelType.White && l.type != PixelType.Gray && l.type != PixelType.Black &&
                        l.type != PixelType.Red && l.type != PixelType.Green && l.type != PixelType.Blue) kept.Add(l);
                looks = kept.ToArray();
            }
            looksVersion = 3;
        }
        if (looksVersion < 4)
        {
            // The Vacuum look (dark purple glass block with black circles) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Vacuum) == null)
            {
                PixelLook vacuumLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Vacuum);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (vacuumLook != null) extended.Add(vacuumLook);
                looks = extended.ToArray();
            }
            looksVersion = 4;
        }
        if (looksVersion < 5)
        {
            // The Obsidian look (polished black metal, white streaks, cracks) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Obsidian) == null)
            {
                PixelLook obsidianLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Obsidian);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (obsidianLook != null) extended.Add(obsidianLook);
                looks = extended.ToArray();
            }
            looksVersion = 5;
        }
        if (looksVersion < 6)
        {
            // The Ghost look (wobbly, matte, floats away) is new: add it to lists saved before it existed.
            if (PixelLooks.Find(looks, PixelType.Ghost) == null)
            {
                PixelLook ghostLook = PixelLooks.Find(PixelLooks.CreateDefaults(), PixelType.Ghost);
                System.Collections.Generic.List<PixelLook> extended = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
                if (ghostLook != null) extended.Add(ghostLook);
                looks = extended.ToArray();
            }
            looksVersion = 6;
        }
        if (looksVersion < 7)
        {
            // Red, green and blue need an entry again (colour-blind marks); add any that are missing.
            System.Collections.Generic.List<PixelLook> list7 = new System.Collections.Generic.List<PixelLook>(looks ?? new PixelLook[0]);
            foreach (PixelType t in new[] { PixelType.Red, PixelType.Green, PixelType.Blue })
            {
                if (PixelLooks.Find(list7.ToArray(), t) != null) continue;
                PixelLook def = PixelLooks.Find(PixelLooks.CreateDefaults(), t);
                if (def != null) list7.Add(def);
            }
            looks = list7.ToArray();
            looksVersion = 7;
        }

        if (pixelRenderer != null)
        {
            // A deleted / missing material shows as pink: build a plain one so the game still works.
            Material current = pixelRenderer.sharedMaterial;
            if (current == null || current.shader == null || !current.shader.isSupported || current.shader.name == "Hidden/InternalErrorShader")
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Debug.LogWarning("PixelClicker: the pixel had no working material - created a temporary one. " +
                                     "Assign a real Lit material to the Pixel (and its prefab) to fix this properly.", this);
                    pixelRenderer.sharedMaterial = new Material(shader) { name = "Pixel (generated)" };
                }
            }
            defaultMaterial = pixelRenderer.sharedMaterial;
        }

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
        allowRotation = PlayerPrefs.GetInt(PrefRotation, 1) != 0;
        allowPulsing = PlayerPrefs.GetInt(PrefPulsing, 1) != 0;
        if (PlayerPrefs.HasKey(PrefBackground)) Application.runInBackground = PlayerPrefs.GetInt(PrefBackground) != 0;
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

        if ((Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PixelPauseMenu.IsPaused) return; // paused (see PixelPauseMenu); Time Stop still lets the cube be clicked
        if (clicksBlocked || ExternalClickBlock || GodMode) return; // e.g. placing a device (PixelConsumables) or holding the hose (PixelBank)
        if (!WasClickedThisFrame() || targetCamera == null) return;
        if (ignoreClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = targetCamera.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Old pixels are skipped unless they're allowed to block clicks.
            if (!oldPixelsBlockClicks && !GrabEnabled && hit.rigidbody != null && oldPixels.Contains(hit.rigidbody)) continue;

            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform))
            {
                // Too soon after the last click: ignore it (stops macros clicking faster than a person can).
                if (Time.unscaledTime - lastManualClickTime >= minClickInterval)
                {
                    lastManualClickTime = Time.unscaledTime;
                    Collect();
                }
            }
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
                SetLiveDamage(tier, hitsOnCurrentPixel);
                PlayClickEffects(tier);
                PixelHit?.Invoke(tierIndex, hitsOnCurrentPixel, tier.clicksToCollect, automatic);
                onPixelClicked?.Invoke();
                return;
            }
        }
        if (tier.clicksToCollect > 1)
        {
            SetLiveDamage(tier, tier.clicksToCollect); // the pieces that fall away are fully cracked
            PixelFinalHit?.Invoke(tierIndex, tier.clicksToCollect, automatic);
        }
        hitsOnCurrentPixel = 0;

        double amount = tier.amountPerClick * UltraMultiplier(tierIndex) * clickMultiplier * (automatic ? 1d : ManualClickBonus);
        tier.timesCollected++;
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

    /// <summary>Takes up to 'amount' off a tier's currency (never below zero) without counting it as spending. Returns how much was taken.</summary>
    public double RemoveCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return 0d;
        double taken = System.Math.Min(tiers[tierIndex].count, amount);
        if (taken <= 0d) return 0d;
        tiers[tierIndex].count -= taken;
        NotifyChanged();
        return taken;
    }

    private long ultraEarned;

    /// <summary>Ultra pixels ever earned (spending them on boosts does not lower this). The Ultra Pad's goal counter uses it.</summary>
    public long UltraEarned => ultraEarned;

    /// <summary>Restores the lifetime Ultra count when loading a save (never below what you currently hold).</summary>
    public void SetUltraEarned(long value) => ultraEarned = System.Math.Max(System.Math.Max(0L, value), TotalUltra);

    /// <summary>Gives Ultra versions of a pixel type.</summary>
    public void AddUltra(int tierIndex, long amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return;
        ultraEarned += amount;
        tiers[tierIndex].ultraCount += amount;
        NotifyChanged();
        UltraGained?.Invoke();
    }

    /// <summary>How much each Ultra boost level adds to a pixel type's payout multiplier.</summary>
    public float UltraBonusPerLevel => ultraBonusPerLevel;

    /// <summary>Is this a valid index into the tier list?</summary>
    public bool IsValidTierIndex(int index) => IsValidTier(index);

    /// <summary>A pixel type's payout multiplier from its Ultra boost level (1 = no boost).</summary>
    public double UltraMultiplier(int tierIndex) =>
        IsValidTier(tierIndex) ? 1d + tiers[tierIndex].ultraLevel * (double)ultraBonusPerLevel : 1d;

    /// <summary>Spends Ultra pixels to raise a pixel type's boost level by one. Returns false if you don't have enough.</summary>
    public bool TryBuyUltraBoost(int tierIndex, long cost)
    {
        if (!IsValidTier(tierIndex) || cost < 0 || (!InfiniteResources && tiers[tierIndex].ultraCount < cost)) return false;
        if (!InfiniteResources) tiers[tierIndex].ultraCount -= cost;
        tiers[tierIndex].ultraLevel++;
        NotifyChanged();
        return true;
    }

    /// <summary>Ultra versions owned of a pixel type.</summary>
    public long GetUltra(PixelType type)
    {
        int i = IndexOf(type);
        return i >= 0 ? tiers[i].ultraCount : 0L;
    }

    /// <summary>Ultra versions owned, of every pixel type together.</summary>
    public long TotalUltra
    {
        get
        {
            long total = 0;
            foreach (PixelTier t in tiers) total += t.ultraCount;
            return total;
        }
    }

    /// <summary>Dev cheat (set by Dev Tools): everything costs nothing - spending always works and takes nothing away.</summary>
    public static bool InfiniteResources;

    /// <summary>Can the player pay this much of a pixel type? (Always, with the infinite resources cheat.)</summary>
    public bool CanAfford(PixelType type, double amount) => InfiniteResources || GetCount(type) >= amount;

    /// <summary>Spends currency if the player can afford it. Use this from shops later.</summary>
    public bool TrySpend(int tierIndex, double amount)
    {
        if (InfiniteResources) return IsValidTier(tierIndex) && amount >= 0;
        if (!IsValidTier(tierIndex) || amount < 0 || tiers[tierIndex].count < amount) return false;
        tiers[tierIndex].count -= amount;
        if (amount > 0d) CurrencySpent?.Invoke(tierIndex, amount);
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
    public void LoadTierState(PixelType type, double count, double totalCollected, bool unlocked, bool spawnDisabled = false, long timesCollected = 0, long ultraCount = 0, int ultraLevel = 0)
    {
        int index = IndexOf(type);
        if (index < 0) return;

        PixelTier tier = tiers[index];
        tier.count = System.Math.Max(0d, count);
        tier.totalCollected = System.Math.Max(0d, totalCollected);
        tier.unlocked = unlocked || tier.unlockedAtStart;
        tier.spawnDisabled = spawnDisabled;
        tier.timesCollected = System.Math.Max(0L, timesCollected);
        tier.ultraCount = System.Math.Max(0L, ultraCount);
        tier.ultraLevel = System.Math.Max(0, ultraLevel);
    }

    /// <summary>Adds to a tier's "times collected" (used for offline progress, which pays without clicking).</summary>
    public void AddTimesCollected(int tierIndex, long count)
    {
        if (IsValidTier(tierIndex) && count > 0) tiers[tierIndex].timesCollected += count;
    }

    /// <summary>Switches a pixel type's spawning on or off (only pixels that can be switched off). Re-rolls the next pixel if needed.</summary>
    public void SetSpawnEnabled(int tierIndex, bool enabled)
    {
        if (!IsValidTier(tierIndex) || !tiers[tierIndex].CanSwitchOff) return;
        tiers[tierIndex].spawnDisabled = !enabled;
        if (randomizeSpawnTier && !enabled && currentTierIndex == tierIndex && !isSpawning)
        {
            currentTierIndex = PickSpawnTier();
            Materialize(GetClickTier());
        }
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

    private int[] devSpawnTiers; // dev tools: only these tiers spawn (locked ones too); null = normal spawning

    /// <summary>True while the dev tools restrict spawning to chosen pixel types.</summary>
    public bool HasDevSpawn => devSpawnTiers != null;

    /// <summary>
    /// Dev tools: only the given pixel types spawn (even locked ones; several = chosen at random). Null or empty = back to
    /// normal spawning. The pixel on screen is swapped straight away.
    /// </summary>
    public void SetDevSpawnTiers(PixelType[] types)
    {
        System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
        if (types != null)
            foreach (PixelType type in types)
            {
                int index = IndexOf(type);
                if (index >= 0 && !list.Contains(index)) list.Add(index);
            }

        devSpawnTiers = list.Count > 0 ? list.ToArray() : null;
        if (!randomizeSpawnTier) return;

        bool fine = devSpawnTiers != null ? System.Array.IndexOf(devSpawnTiers, currentTierIndex) >= 0
                                          : IsValidTier(currentTierIndex) && tiers[currentTierIndex].unlocked;
        if (fine) return;
        currentTierIndex = PickSpawnTier();
        Materialize(GetClickTier());
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

    private int forcedTierIndex2 = -1;

    /// <summary>
    /// Like <see cref="SetForcedSpawnTier"/> but for two pixel types (a combo potion): each new pixel is one of the two,
    /// 50/50. If only one of them is unlocked, only that one spawns.
    /// </summary>
    public void SetForcedSpawnTiers(PixelType a, PixelType b)
    {
        int ia = IndexOf(a), ib = IndexOf(b);
        bool okA = ia >= 0 && tiers[ia].unlocked, okB = ib >= 0 && tiers[ib].unlocked;
        if (!okA && !okB) return;

        forcedTierIndex = okA ? ia : ib;
        forcedTierIndex2 = okA && okB ? ib : -1;

        if (randomizeSpawnTier && currentTierIndex != forcedTierIndex && currentTierIndex != forcedTierIndex2)
        {
            currentTierIndex = PickSpawnTier();
            Materialize(GetClickTier());
        }
    }

    /// <summary>Back to the normal weighted random spawning.</summary>
    public void ClearForcedSpawnTier()
    {
        forcedTierIndex = -1;
        forcedTierIndex2 = -1;
    }

    /// <summary>Weighted random pick among unlocked tiers (or the forced tier while a potion is active).</summary>
    private int PickSpawnTier()
    {
        if (devSpawnTiers != null) return devSpawnTiers[UnityEngine.Random.Range(0, devSpawnTiers.Length)];

        if (IsValidTier(forcedTierIndex) && tiers[forcedTierIndex].unlocked)
        {
            if (IsValidTier(forcedTierIndex2) && tiers[forcedTierIndex2].unlocked && UnityEngine.Random.value < 0.5f)
                return forcedTierIndex2;
            return forcedTierIndex;
        }

        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].CanSpawn) total += Mathf.Max(0f, tiers[i].spawnWeight);

        if (total <= 0f) return GetHighestUnlockedIndex();

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!tiers[i].CanSpawn) continue;
            roll -= Mathf.Max(0f, tiers[i].spawnWeight);
            if (roll <= 0f) return i;
        }
        return GetHighestUnlockedIndex();
    }

    private int GetClickTierIndex()
    {
        if (randomizeSpawnTier && devSpawnTiers != null && IsValidTier(currentTierIndex)) return currentTierIndex; // dev tools: even locked tiers
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

    private void OnEnable() => PixelDisplaySettings.ColorBlindChanged += RefreshLooks;

    private void OnDisable() => PixelDisplaySettings.ColorBlindChanged -= RefreshLooks;

    /// <summary>Re-draws the pixel on the cube with its current look (e.g. after the colour-blind setting changed).</summary>
    private void RefreshLooks()
    {
        if (pixelRenderer == null || tiers == null || tiers.Length == 0) return;
        if (liveExtras != null) Destroy(liveExtras);
        liveExtras = null;
        liveExtrasBuilt = false;
        ApplyTierLook(GetClickTier());
    }

    private void OnDestroy()
    {
        if (hitbox != null) Destroy(hitbox.gameObject);
        ExternalClickBlock = false;
    }

    /// <summary>Frame time for the cube's click animations: they keep running while Time Stop has frozen the game clock.</summary>
    private int CopyCap => PixelTimeStop.IsStopped ? timeStopStockpileMax : maxFallingCopies;

    private static float AnimDelta => PixelTimeStop.IsStopped ? Time.unscaledDeltaTime : Time.deltaTime;

    private void AnimatePixel()
    {
        float time = Time.time;

        if (suspendInMidair && hoverBob)
        {
            pixelTransform.position = basePosition +
                Vector3.up * (Mathf.Sin(time * hoverSpeed * Mathf.PI * 2f) * hoverAmplitude);
        }

        if (idleSpin != Vector3.zero && allowRotation)
            pixelTransform.Rotate(idleSpin * Time.deltaTime, Space.Self);

        if (hitbox != null) hitbox.SetPositionAndRotation(pixelTransform.position, pixelTransform.rotation);

        float pulse = 0f;
        if (pulseEnabled && allowPulsing)
        {
            float cycle = Mathf.Repeat(time * pulseSpeed, 1f);
            pulse = pulseCurve.Evaluate(cycle); // -1..1
        }

        // Squash briefly when a tough pixel is hit but not yet collected.
        float punch = 1f;
        if (hitPunchTimer > 0f)
        {
            hitPunchTimer -= AnimDelta;
            punch = 1f - hitPunchAmount * Mathf.Clamp01(hitPunchTimer / Mathf.Max(0.01f, hitPunchDuration));
        }

        Vector3 liveBase = baseScale;
        if (activeLook != null && activeLook.wobble && allowPulsing)
            liveBase = Vector3.Scale(baseScale, PixelWobble.Scale(time, activeLook.wobbleAmount, activeLook.wobbleSpeed, 0f));
        pixelTransform.localScale = liveBase * (materializeFactor * punch * (1f + pulse * pulseAmount));

        if (pulseEnabled && allowPulsing && pulseBrightness)
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

    private PixelLook activeLook;          // the look of the tier the cube currently shows
    private GameObject liveExtras;         // outline / dark-matter core on the live cube
    private PixelType liveExtrasType;
    private bool liveExtrasBuilt;

    /// <summary>True if old pixels of this tier pass through the screen edges (ghosts), see PixelLook.ignoreViewBounds.</summary>
    public bool IgnoresViewBounds(int tierIndex)
    {
        PixelLook look = IsValidTier(tierIndex) ? LookOf(tiers[tierIndex]) : null;
        return look != null && look.ignoreViewBounds;
    }

    /// <summary>The look of a tier, or null (looks off / none defined).</summary>
    private PixelLook LookOf(PixelTier tier) => useLooks && tier != null ? PixelLooks.Find(looks, tier.type) : null;

    /// <summary>The colour the 3D pixel is drawn in: the look's colour (alpha scaled) or the tier's own.</summary>
    private static Color RenderColor(PixelTier tier, PixelLook look)
    {
        Color c = look != null && look.useColor ? look.color : tier.color;
        if (look != null) c.a *= look.alpha;
        return c;
    }

    /// <summary>Puts a look's streak / crack texture on a property block (level = how damaged, 0..max).</summary>
    private static void ApplyLookTexture(MaterialPropertyBlock block, PixelLook look, int level, int max)
    {
        if (look == null || !look.HasSurfaceTexture) return;
        Texture2D tex = PixelLooks.SurfaceTexture(look.streakTexture, look.damageCracks ? level : 0, max);
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
    }

    /// <summary>A hit on a tough pixel: cracks spread over the live cube (level 0 = undamaged, max = about to break).</summary>
    private void SetLiveDamage(PixelTier tier, int level)
    {
        if (pixelRenderer == null || activeLook == null || !activeLook.damageCracks) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        ApplyLookTexture(propertyBlock, activeLook, level, tier.clicksToCollect);
        pixelRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>Metallic / glossiness from a look onto a property block.</summary>
    private static void ApplyLookSurface(MaterialPropertyBlock block, PixelLook look)
    {
        if (look == null) return;
        if (look.metallic >= 0f) block.SetFloat("_Metallic", look.metallic);
        if (look.smoothness >= 0f)
        {
            block.SetFloat("_Smoothness", look.smoothness);
            block.SetFloat("_Glossiness", look.smoothness);
        }
    }

    /// <summary>Adds / removes the outline and core on the live cube to match the tier's look.</summary>
    private void UpdateLiveExtras(PixelTier tier, PixelLook look)
    {
        bool wanted = look != null && look.HasExtras && pixelRenderer != null;
        if (!wanted)
        {
            if (liveExtras != null) Destroy(liveExtras);
            liveExtras = null;
            liveExtrasBuilt = false;
            return;
        }
        if (liveExtrasBuilt && liveExtras != null && liveExtrasType == tier.type) return;

        if (liveExtras != null) Destroy(liveExtras);
        MeshFilter mf = pixelRenderer.GetComponent<MeshFilter>();
        liveExtras = mf != null ? PixelLooks.AddExtras(pixelRenderer.transform, mf.sharedMesh, look, tier.color, defaultMaterial) : null;
        liveExtrasType = tier.type;
        liveExtrasBuilt = true;
    }

    private void ApplyPixelColor(Color color, bool remember = true)
    {
        if (remember) currentColor = color;
        if (pixelRenderer == null) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyId, color);
        // Also set the built-in name so either pipeline works if the property name is left default.
        propertyBlock.SetColor("_Color", color);

        ApplyLookSurface(propertyBlock, activeLook);

        // Glowing tiers: emission follows the colour (and breathes). Black emission = off for everything else.
        if (activeGlow > 0f)
        {
            float breath = 1f + glowBreathAmount * Mathf.Sin(Time.time * glowBreathSpeed * Mathf.PI * 2f);
            float scale = activeLook != null ? activeLook.glowScale : 1f;
            Color emission = new Color(color.r, color.g, color.b, 1f) * (activeGlow * breath * scale);
            propertyBlock.SetColor("_EmissionColor", emission);
        }
        else if (activeLook != null && activeLook.emission > 0f)
        {
            // Plain self-lighting from the look (so e.g. white pixels stand out).
            propertyBlock.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * activeLook.emission);
        }
        else
        {
            propertyBlock.SetColor("_EmissionColor", Color.black);
        }
        pixelRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>The material a tier's pixel is drawn with (normal, translucent, override, or glowing).</summary>
    private Material MaterialForTier(PixelTier tier)
    {
        Material wanted = defaultMaterial;
        if (tier.materialOverride != null) wanted = tier.materialOverride;
        else if ((tier.translucent || (LookOf(tier) != null && LookOf(tier).forceTranslucent)) && defaultMaterial != null)
        {
            if (transparentMaterial == null) transparentMaterial = BuildTransparentMaterial(defaultMaterial);
            wanted = transparentMaterial;
        }

        // Glowing tiers need a copy of the material with emission switched on.
        PixelLook styled = LookOf(tier);
        bool selfLit = tier.glow || (styled != null && styled.emission > 0f);
        if (selfLit && tier.materialOverride == null && wanted != null) wanted = GetGlowMaterial(wanted);
        return wanted;
    }

    /// <summary>Applies a tier's material (normal, translucent or override) and colour to the pixel.</summary>
    private void ApplyTierLook(PixelTier tier)
    {
        if (pixelRenderer != null)
        {
            Material wanted = MaterialForTier(tier);
            if (wanted != null && pixelRenderer.sharedMaterial != wanted) pixelRenderer.sharedMaterial = wanted;
        }

        activeGlow = tier.glow ? tier.glowIntensity : 0f;
        activeLook = LookOf(tier);
        UpdateGlowLight(tier);
        UpdateLiveExtras(tier, activeLook);
        if (pixelRenderer != null)
        {
            // Start from a clean block so nothing (texture, glossiness) is left over from the previous pixel type.
            propertyBlock.Clear();
            pixelRenderer.SetPropertyBlock(propertyBlock);
        }
        ApplyPixelColor(RenderColor(tier, activeLook));
        if (pixelRenderer != null && activeLook != null && activeLook.HasSurfaceTexture)
        {
            pixelRenderer.GetPropertyBlock(propertyBlock);
            ApplyLookTexture(propertyBlock, activeLook, 0, tier.clicksToCollect);
            pixelRenderer.SetPropertyBlock(propertyBlock);
        }
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
            t += AnimDelta;
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
    public void Vacuum(int vacuumTierIndex)
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

    /// <summary>Removes every old pixel lying around (no payout). Used by the dev tools.</summary>
    public void ClearOldPixels()
    {
        for (int i = 0; i < oldPixels.Count; i++)
            if (oldPixels[i] != null) DespawnOldPixel(oldPixels[i].gameObject);
        oldPixels.Clear();
    }

    /// <summary>The old pixels currently lying around (read-only). Used by the vacuum device.</summary>
    public System.Collections.Generic.IReadOnlyList<Rigidbody> OldPixels => oldPixels;

    /// <summary>
    /// Collects one old pixel again (its reward is re-added) and pulls it into 'target' while it shrinks.
    /// Returns false if it isn't an old pixel any more.
    /// </summary>
    public bool AbsorbOldPixel(Rigidbody body, Transform target)
    {
        if (body == null || !oldPixels.Remove(body)) return false;

        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info != null && IsValidTier(info.tierIndex))
        {
            AddCurrency(info.tierIndex, info.amount);
            double[] perTier = new double[tiers.Length];
            perTier[info.tierIndex] = info.amount;
            VacuumBreakdown?.Invoke(perTier);
        }

        StartCoroutine(SuckRoutine(body, target));
        return true;
    }

    /// <summary>
    /// Takes an old pixel out of the game's old-pixel list so something else (the black hole) can handle it.
    /// 'credit' re-adds its original reward. The caller is responsible for removing the object. Returns false if it wasn't listed.
    /// </summary>
    public bool ReleaseOldPixel(Rigidbody body, bool credit)
    {
        if (body == null || !oldPixels.Remove(body)) return false;

        if (credit)
        {
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex)) AddCurrency(info.tierIndex, info.amount);
        }
        return true;
    }

    /// <summary>The cube's normal size in world units (not counting pulsing / materializing). Used to size things around it.</summary>
    public float PixelBaseSize
    {
        get
        {
            if (pixelTransform == null) return 1f;
            Vector3 s = baseScale;
            if (pixelTransform.parent != null) s = Vector3.Scale(s, pixelTransform.parent.lossyScale);
            return Mathf.Max(s.x, s.y, s.z);
        }
    }

    /// <summary>While true, clicks on the cube are ignored (used while a device is being placed).</summary>
    public void SetClicksBlocked(bool blocked) => clicksBlocked = blocked;

    /// <summary>
    /// A copy of the pixel's material tinted with 'color' - for simple scene objects that should match the game's look.
    /// 'transparent' makes it see-through (the colour's alpha sets how much).
    /// </summary>
    public Material CreateVisualMaterial(Color color, bool transparent)
    {
        Material source = defaultMaterial != null ? defaultMaterial
                        : pixelRenderer != null ? pixelRenderer.sharedMaterial : null;
        if (source == null) return null;

        Material mat = transparent ? BuildTransparentMaterial(source) : new Material(source);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

#if UNITY_6000_0_OR_NEWER
    private PhysicsMaterial oldPixelPhysics;
#else
    private PhysicMaterial oldPixelPhysics;
#endif
    private float oldPixelPhysicsBounce = -1f, oldPixelPhysicsFriction = -1f;
    private readonly System.Collections.Generic.Dictionary<PixelType, UnityEngine.Object> lookPhysics =
        new System.Collections.Generic.Dictionary<PixelType, UnityEngine.Object>();
    private readonly System.Collections.Generic.Dictionary<int, Material> trailMaterials = new System.Collections.Generic.Dictionary<int, Material>();
    private readonly System.Collections.Generic.Dictionary<int, Color> trailMaterialColors = new System.Collections.Generic.Dictionary<int, Color>();

    /// <summary>One physics material shared by every old pixel (rebuilt when bounciness / friction change).</summary>
    /// <summary>Bounce / friction material of one pixel type's look (cached; rebuilt if the values change).</summary>
#if UNITY_6000_0_OR_NEWER
    private PhysicsMaterial LookPhysicsMaterial(PixelLook look)
    {
        if (lookPhysics.TryGetValue(look.type, out UnityEngine.Object cached) && cached != null)
        {
            PhysicsMaterial existing = (PhysicsMaterial)cached;
            if (Mathf.Approximately(existing.bounciness, look.bounce) && Mathf.Approximately(existing.dynamicFriction, look.friction)) return existing;
        }
        PhysicsMaterial m = new PhysicsMaterial("OldPixel_" + look.type)
        {
            bounciness = look.bounce, dynamicFriction = look.friction, staticFriction = look.friction,
            bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum
        };
        lookPhysics[look.type] = m;
        return m;
    }
#else
    private PhysicMaterial LookPhysicsMaterial(PixelLook look)
    {
        if (lookPhysics.TryGetValue(look.type, out UnityEngine.Object cached) && cached != null)
        {
            PhysicMaterial existing = (PhysicMaterial)cached;
            if (Mathf.Approximately(existing.bounciness, look.bounce) && Mathf.Approximately(existing.dynamicFriction, look.friction)) return existing;
        }
        PhysicMaterial m = new PhysicMaterial("OldPixel_" + look.type)
        {
            bounciness = look.bounce, dynamicFriction = look.friction, staticFriction = look.friction,
            bounceCombine = PhysicMaterialCombine.Maximum, frictionCombine = PhysicMaterialCombine.Minimum
        };
        lookPhysics[look.type] = m;
        return m;
    }
#endif

#if UNITY_6000_0_OR_NEWER
    private PhysicsMaterial SharedOldPixelPhysicsMaterial()
    {
        if (oldPixelPhysics == null || oldPixelPhysicsBounce != bounciness || oldPixelPhysicsFriction != friction)
        {
            oldPixelPhysics = new PhysicsMaterial("OldPixel")
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average
            };
            oldPixelPhysicsBounce = bounciness;
            oldPixelPhysicsFriction = friction;
        }
        return oldPixelPhysics;
    }
#else
    private PhysicMaterial SharedOldPixelPhysicsMaterial()
    {
        if (oldPixelPhysics == null || oldPixelPhysicsBounce != bounciness || oldPixelPhysicsFriction != friction)
        {
            oldPixelPhysics = new PhysicMaterial("OldPixel")
            {
                bounciness = bounciness,
                dynamicFriction = friction,
                staticFriction = friction,
                bounceCombine = PhysicMaterialCombine.Maximum,
                frictionCombine = PhysicMaterialCombine.Average
            };
            oldPixelPhysicsBounce = bounciness;
            oldPixelPhysicsFriction = friction;
        }
        return oldPixelPhysics;
    }
#endif

    /// <summary>One trail material per pixel type, shared by all of its fly-away copies.</summary>
    private Material TrailMaterialFor(int tierIndex, Color tierColor)
    {
        if (trailMaterials.TryGetValue(tierIndex, out Material cached) && cached != null &&
            trailMaterialColors[tierIndex] == tierColor)
            return cached;

        Material mat = CreateVisualMaterial(new Color(tierColor.r, tierColor.g, tierColor.b, 0.6f), true);
        trailMaterials[tierIndex] = mat;
        trailMaterialColors[tierIndex] = tierColor;
        return mat;
    }

    /// <summary>
    /// Moves a falling copy's drawing onto a child object that wobbles (the pixel's own scale is used by pop-in / despawn,
    /// so the wobble can't share it).
    /// </summary>
    private static void MakeWobbleVisual(GameObject copy, PixelLook look)
    {
        MeshFilter mf = copy.GetComponent<MeshFilter>();
        MeshRenderer mr = copy.GetComponent<MeshRenderer>();
        if (mf == null || mr == null) return;

        GameObject visual = new GameObject("Wobble Visual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(copy.transform, false);
        visual.layer = copy.layer;
        visual.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        MeshRenderer vr = visual.GetComponent<MeshRenderer>();
        vr.sharedMaterial = mr.sharedMaterial;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        mr.GetPropertyBlock(block);
        vr.SetPropertyBlock(block);
        vr.shadowCastingMode = mr.shadowCastingMode;
        mr.enabled = false;
        visual.AddComponent<PixelLookWobble>().Setup(look.wobbleAmount, look.wobbleSpeed);
    }

    private bool IsBrightOldPixel(int tierIndex)
    {
        if (!IsValidTier(tierIndex) || brightOldPixelTypes == null) return false;
        PixelTier t = tiers[tierIndex];
        if (!t.glow) return false;
        for (int i = 0; i < brightOldPixelTypes.Length; i++)
            if (brightOldPixelTypes[i] == t.type) return true;
        return false;
    }

    private Material lightTrailMaterial;

    /// <summary>A glowing, fading trail behind a falling old pixel (colours come from the trail's gradient).</summary>
    private void AddLightTrail(GameObject copy, Color tierColor)
    {
        if (lightTrailMaterial == null)
        {
            // Sprites/Default uses the trail's vertex colours in every render pipeline; fall back to a tinted copy of the pixel material.
            Shader shader = Shader.Find("Sprites/Default");
            lightTrailMaterial = shader != null ? new Material(shader) { name = "LightTrail" }
                                                : CreateVisualMaterial(Color.white, true);
        }

        Color hot = Color.Lerp(tierColor, Color.white, Mathf.Clamp01((lightTrailBrightness - 1f) * 0.5f));
        hot = new Color(Mathf.Clamp01(hot.r * Mathf.Min(lightTrailBrightness, 1.5f)), Mathf.Clamp01(hot.g * Mathf.Min(lightTrailBrightness, 1.5f)),
                        Mathf.Clamp01(hot.b * Mathf.Min(lightTrailBrightness, 1.5f)), 1f);

        TrailRenderer trail = copy.AddComponent<TrailRenderer>();
        trail.sharedMaterial = lightTrailMaterial;
        trail.time = lightTrailSeconds;
        trail.minVertexDistance = 0.04f;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.widthMultiplier = copy.transform.lossyScale.x * lightTrailWidth;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));

        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(hot, 0f), new GradientColorKey(tierColor, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
    }

    /// <summary>Pulls one old pixel into the cube while shrinking it, then removes it.</summary>
    private IEnumerator SuckRoutine(Rigidbody body, Transform target = null)
    {
        Transform dest = target != null ? target : pixelTransform;
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;

        Vector3 startPos = t.position;
        Vector3 endPos = dest != null ? dest.position : startPos;
        Vector3 startScale = t.localScale;
        float time = 0f;

        while (time < vacuumSuckDuration)
        {
            if (t == null) yield break; // destroyed (lifetime ran out) while flying
            time += Time.deltaTime;
            float k = vacuumSuckDuration > 0f ? Mathf.Clamp01(time / vacuumSuckDuration) : 1f;
            float eased = vacuumSuckCurve.Evaluate(k);

            if (dest != null) endPos = dest.position;
            t.position = Vector3.Lerp(startPos, endPos, eased);
            t.localScale = startScale * (1f - eased);
            yield return null;
        }

        if (t != null) Destroy(t.gameObject);
    }

    /// <summary>Clones the visible pixel, adds real physics, and pops it out in a random direction.</summary>
    /// <summary>Gives an old pixel its lifetime: after 'lifetime' seconds (0 = never) it swells, shrinks away and is destroyed.</summary>
    private void AddDespawn(GameObject copy, float lifetime)
    {
        OldPixelDespawn d = copy.AddComponent<OldPixelDespawn>();
        d.Setup(lifetime, despawnSwellScale, despawnSwellSeconds, despawnShrinkSeconds);
    }

    /// <summary>Makes an old pixel swell and shrink away now (instead of just vanishing).</summary>
    private void DespawnOldPixel(GameObject copy)
    {
        OldPixelDespawn d = copy.GetComponent<OldPixelDespawn>();
        if (d == null)
        {
            AddDespawn(copy, 0f);
            d = copy.GetComponent<OldPixelDespawn>();
        }
        d.Begin();
    }

    /// <summary>
    /// Puts an old pixel of any type into the world at 'position' moving at 'velocity' (the pixel bank spitting one out).
    /// Fly-away types (meteor) can't be spawned this way.
    /// </summary>
    public bool SpawnStoredPixel(int tierIndex, double amount, Vector3 position, Vector3 velocity)
    {
        if (!IsValidTier(tierIndex) || tiers[tierIndex].flyAway || pixelTransform == null) return false;
        SpawnFallingCopy(tierIndex, amount, true, position, velocity);
        return true;
    }

    private void SpawnFallingCopy(int tierIndex, double amount, bool stored = false, Vector3 storedPosition = default,
                                  Vector3 storedVelocity = default)
    {
        // Skip if the pixel is mid-materialize and basically invisible.
        if (!stored && pixelTransform.localScale.sqrMagnitude < 0.0001f) return;

        // A working sorter spits the pixel out of its pipe instead of popping it out at random (not for fly-away pixels).
        bool routed = false;
        Vector3 routedPosition = pixelTransform.position, routedVelocity = Vector3.zero;
        bool flyType = tierIndex >= 0 && tierIndex < tiers.Length && tiers[tierIndex].flyAway;
        if (stored)
        {
            routed = true; // the pixel bank decides where it goes and how fast
            routedPosition = storedPosition;
            routedVelocity = storedVelocity;
        }
        else if (!flyType && PixelSorterDevice.Current != null)
            routed = PixelSorterDevice.Current.TryRoute(out routedPosition, out routedVelocity);

        GameObject copy = new GameObject("OldPixel");
        copy.transform.SetPositionAndRotation(routed ? routedPosition : pixelTransform.position,
                                              stored ? UnityEngine.Random.rotation : pixelTransform.rotation);
        Vector3 sourceScale = pixelTransform.lossyScale;
        if (stored)
        {
            sourceScale = baseScale; // the cube's normal size, whatever it is doing right now
            if (pixelTransform.parent != null) sourceScale = Vector3.Scale(sourceScale, pixelTransform.parent.lossyScale);
        }
        copy.transform.localScale = sourceScale * oldPixelScale;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) copy.layer = fallingCopyLayer;

        // Copy only the visuals (mesh + material) so we don't duplicate this script.
        MeshFilter srcFilter = pixelRenderer != null ? pixelRenderer.GetComponent<MeshFilter>() : null;
        if (srcFilter != null && pixelRenderer != null)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;
            MeshRenderer mr = copy.AddComponent<MeshRenderer>();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            if (stored && IsValidTier(tierIndex))
            {
                // A stored pixel looks like its own type, not like whatever the cube currently shows.
                PixelTier look = tiers[tierIndex];
                PixelLook styled = LookOf(look);
                Color drawColor = RenderColor(look, styled);
                Material m = MaterialForTier(look);
                mr.sharedMaterial = m != null ? m : pixelRenderer.sharedMaterial;
                block.SetColor(colorPropertyId, drawColor);
                block.SetColor("_Color", drawColor);
                ApplyLookSurface(block, styled);
                ApplyLookTexture(block, styled, 0, look.clicksToCollect);
                float glowMul = styled != null ? styled.glowScale : 1f;
                block.SetColor("_EmissionColor", look.glow
                    ? new Color(look.color.r, look.color.g, look.color.b, 1f) * (look.glowIntensity * glowMul)
                    : styled != null && styled.emission > 0f
                        ? new Color(drawColor.r, drawColor.g, drawColor.b, 1f) * styled.emission : Color.black);
            }
            else
            {
                mr.sharedMaterial = pixelRenderer.sharedMaterial;
                pixelRenderer.GetPropertyBlock(block);
            }
            if (!flyType && IsBrightOldPixel(tierIndex))
            {
                // Extra bright falling pixel: stronger emission than the cube had.
                PixelTier look = tiers[tierIndex];
                Color baseGlow = new Color(look.color.r, look.color.g, look.color.b, 1f);
                block.SetColor("_EmissionColor", baseGlow * (Mathf.Max(look.glowIntensity, 1f) * oldPixelGlowBoost));
            }
            mr.SetPropertyBlock(block);
        }

        // Collider with a physics material so bounce/friction are tweakable.
        BoxCollider box = copy.AddComponent<BoxCollider>();
        PixelLook oldLook = IsValidTier(tierIndex) ? LookOf(tiers[tierIndex]) : null;
        bool custom = oldLook != null && oldLook.customPhysics;
        box.sharedMaterial = custom ? LookPhysicsMaterial(oldLook) : SharedOldPixelPhysicsMaterial();

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
        bool fly = tierIndex >= 0 && tierIndex < tiers.Length && tiers[tierIndex].flyAway;
        if (fly)
        {
            // Meteor-style: no gravity, no collisions, one straight direction (camera-relative), with a trail.
            box.enabled = false;
            Camera fc = targetCamera != null ? targetCamera : Camera.main;
            Vector2 d = tiers[tierIndex].flyDirection;
            if (d.sqrMagnitude < 0.0001f) d = Vector2.right;
            d.Normalize();
            Vector3 dir = fc != null ? fc.transform.right * d.x + fc.transform.up * d.y : new Vector3(d.x, d.y, 0f);
            dir = Quaternion.AngleAxis(UnityEngine.Random.Range(-flySpreadDegrees, flySpreadDegrees),
                                       fc != null ? fc.transform.forward : Vector3.forward) * dir;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
#else
            rb.drag = 0f;
            rb.angularDrag = 0f;
#endif
            rb.AddForce(dir.normalized * tiers[tierIndex].flySpeed, ForceMode.VelocityChange);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                         ForceMode.VelocityChange);

            if (flyTrailSeconds > 0f)
            {
                TrailRenderer trail = copy.AddComponent<TrailRenderer>();
                trail.time = flyTrailSeconds;
                trail.startWidth = copy.transform.lossyScale.x;
                trail.endWidth = 0f;
                Color tc = tiers[tierIndex].color;
                trail.sharedMaterial = TrailMaterialFor(tierIndex, tc);
                trail.startColor = new Color(tc.r, tc.g, tc.b, 0.8f);
                trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);
            }
        }
        else if (gravityScale > 0f)
            copy.AddComponent<ScaledGravity>().scale = gravityScale * (custom ? oldLook.gravityMultiplier : 1f);

        if (!fly) copy.AddComponent<OldPixelImpact>().Setup(landMinSpeed, landFullVolumeSpeed, landCooldown);
        if (!fly && IsValidTier(tierIndex) && srcFilter != null)
        {
            // Look extras: neon outline / dark-matter core, and shattering for glass.
            PixelLook styled = LookOf(tiers[tierIndex]);
            if (styled != null)
            {
                PixelLooks.AddExtras(copy.transform, srcFilter.sharedMesh, styled, tiers[tierIndex].color, defaultMaterial);
                if (styled.wobble) MakeWobbleVisual(copy, styled);
                if (styled.floatAway)
                    copy.AddComponent<OldPixelFloat>().Setup(this, styled.floatAfterBounces, styled.floatLift, styled.floatDriftSpeed);
                if (styled.shatter)
                    copy.AddComponent<OldPixelShatter>().Setup(this, shatterMinSpeed, shardCount, shardSpeed, shardLifeSeconds, shardSize, shatterSoundId);
            }
        }
        if (!fly && lightTrail && IsBrightOldPixel(tierIndex)) AddLightTrail(copy, tiers[tierIndex].color);

        if (fly)
        {
            AddDespawn(copy, flyLifetime);
            OldPixelInfo flyInfo = copy.AddComponent<OldPixelInfo>();
            flyInfo.tierIndex = tierIndex;
            flyInfo.amount = amount;
            oldPixels.Add(rb);
            return;
        }

        if (routed)
        {
            rb.AddForce(routedVelocity, ForceMode.VelocityChange);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                         ForceMode.VelocityChange);
            AddDespawn(copy, fallingCopyLifetime);
            OldPixelInfo routedInfo = copy.AddComponent<OldPixelInfo>();
            routedInfo.tierIndex = tierIndex;
            routedInfo.amount = amount;
            oldPixels.Add(rb);
            while (CopyCap > 0 && oldPixels.Count > CopyCap)
            {
                Rigidbody oldest = oldPixels[0];
                oldPixels.RemoveAt(0);
                if (oldest != null) DespawnOldPixel(oldest.gameObject);
            }
            return;
        }

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

        if (popGrowSeconds > 0f || popNoCollisionSeconds > 0f)
        {
            // Spawns tiny and collision-free so fast clicking doesn't make a pile-up; grows and becomes solid while falling.
            OldPixelPopIn pop = copy.AddComponent<OldPixelPopIn>();
            pop.Setup(box, copy.transform.localScale, popStartScale, popGrowSeconds, popNoCollisionSeconds);
        }

        AddDespawn(copy, fallingCopyLifetime);

        OldPixelInfo info = copy.AddComponent<OldPixelInfo>();
        info.tierIndex = tierIndex;
        info.amount = amount;

        oldPixels.Add(rb);
        while (CopyCap > 0 && oldPixels.Count > CopyCap)
        {
            Rigidbody oldest = oldPixels[0];
            oldPixels.RemoveAt(0);
            if (oldest != null) DespawnOldPixel(oldest.gameObject);
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

    private const string PrefAbbreviate = "PixelClicker.Setting.AbbreviateNumbers";
    private static int abbreviateCache = -1; // -1 = not read yet

    /// <summary>Player setting: show big numbers as 1.2K / 3.4M (on) or in full as 1,200 (off). Remembered between sessions; used by every number display.</summary>
    public static bool AbbreviateNumbers
    {
        get
        {
            if (abbreviateCache < 0) abbreviateCache = PlayerPrefs.GetInt(PrefAbbreviate, 1) != 0 ? 1 : 0;
            return abbreviateCache != 0;
        }
        set { abbreviateCache = value ? 1 : 0; PlayerPrefs.SetInt(PrefAbbreviate, abbreviateCache); }
    }

    /// <summary>Number formatting: compact (1.2K, 3.4M ...) or full with separators (1,200), depending on <see cref="AbbreviateNumbers"/>.</summary>
    public static string FormatNumber(double value)
    {
        if (!AbbreviateNumbers) return value.ToString("#,0.##");
        if (value < 999.995) return value.ToString("0.##"); // small amounts keep their decimals (e.g. 6.25)
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
/// Reports when an old pixel hits a surface hard enough to be heard (PixelClicker.OldPixelLanded). Pixels bumping into
/// each other don't count - only the floor and other solid surfaces.
/// </summary>
public class OldPixelImpact : MonoBehaviour
{
    private float minSpeed = 1.5f, fullSpeed = 8f, cooldown = 0.12f, lastTime = -99f;

    public void Setup(float min, float full, float gap)
    {
        minSpeed = min;
        fullSpeed = Mathf.Max(0.1f, full);
        cooldown = gap;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (Time.time - lastTime < cooldown) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minSpeed) return;

        lastTime = Time.time;
        PixelClicker.RaiseOldPixelLanded(Mathf.Clamp01(speed / fullSpeed));
    }
}

/// <summary>
/// Makes a new old pixel start tiny and non-colliding, then grow to full size and become solid again.
/// </summary>
public class OldPixelPopIn : MonoBehaviour
{
    private Collider body;
    private Vector3 fullScale;
    private float startScale, growSeconds, ghostSeconds, age;

    public void Setup(Collider collider, Vector3 full, float start, float grow, float noCollision)
    {
        body = collider;
        fullScale = full;
        startScale = start;
        growSeconds = grow;
        ghostSeconds = noCollision;
        if (body != null && ghostSeconds > 0f) body.isTrigger = true; // a trigger doesn't collide (keeps ignore-collision settings)
        transform.localScale = fullScale * (growSeconds > 0f ? startScale : 1f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (growSeconds > 0f)
        {
            float k = Mathf.Clamp01(age / growSeconds);
            transform.localScale = fullScale * Mathf.Lerp(startScale, 1f, k * (2f - k)); // ease out
        }
        if (body != null && body.isTrigger && age >= ghostSeconds) body.isTrigger = false;
        if (age >= growSeconds && age >= ghostSeconds) Destroy(this);
    }
}

/// <summary>
/// Ends an old pixel's life: after its lifetime (or when Begin is called) it swells slightly for a split second,
/// then shrinks very quickly to nothing and is destroyed.
/// </summary>
public class OldPixelDespawn : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Frozen = false;
        DevNoDespawn = false;
    }

    private float lifetime, swellScale, swellSeconds, shrinkSeconds, age, phaseTime;
    private bool despawning;

    /// <summary>True while the player holds this pixel (its lifetime is frozen).</summary>
    public bool Held { get; set; }

    /// <summary>While true, no old pixel's lifetime counts down (a black hole is open: time dilation). Pixels already vanishing finish.</summary>
    public static bool Frozen { get; set; }

    /// <summary>Dev tools: old pixels never expire on their own while true.</summary>
    public static bool DevNoDespawn { get; set; }

    /// <summary>Gives the pixel more time before it starts to vanish (no effect once it is vanishing or if it never expires).</summary>
    public void AddLifetime(float seconds)
    {
        if (!despawning && lifetime > 0f) lifetime += seconds;
    }

    /// <summary>True once it has started swelling and shrinking away.</summary>
    public bool IsDespawning => despawning;
    private Vector3 baseScale;

    public void Setup(float life, float swell, float swellTime, float shrinkTime)
    {
        lifetime = life;
        swellScale = swell;
        swellSeconds = Mathf.Max(0.01f, swellTime);
        shrinkSeconds = Mathf.Max(0.01f, shrinkTime);
    }

    /// <summary>Starts the swell-and-shrink now. Safe to call more than once.</summary>
    public void Begin()
    {
        if (despawning) return;
        despawning = true;
        baseScale = transform.localScale;

        OldPixelPopIn pop = GetComponent<OldPixelPopIn>();
        if (pop != null) Destroy(pop); // stop growing; we take over the scale

        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true; // a vanishing pixel shouldn't shove others around
    }

    private void Update()
    {
        if (!despawning)
        {
            if (Held || Frozen || DevNoDespawn) return;
            age += Time.deltaTime;
            if (lifetime > 0f && age >= lifetime) Begin();
            return;
        }

        phaseTime += Time.deltaTime;
        if (phaseTime < swellSeconds)
        {
            float k = phaseTime / swellSeconds;
            transform.localScale = baseScale * Mathf.Lerp(1f, swellScale, k);
        }
        else
        {
            float k = (phaseTime - swellSeconds) / shrinkSeconds;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.localScale = baseScale * Mathf.Lerp(swellScale, 0f, k * k);
        }
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
