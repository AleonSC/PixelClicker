using Unity.Profiling;
// ---- System notes (moved here from CLAUDE.md) ----
// PixelClicker.cs
//   Core. Click-me hint: while the Inventory UI's guide text ("Click N more...") shows and nothing has been collected yet
//   (PixelClicker.ClickHint, set by PixelUI.UpdateGuide; ClickHintPulse), the cube pulses bigger / brighter
//   (clickHintAmount/Brightness/Speed, disableClickHint). Manual clicks on the cube are rate-limited by minClickInterval (0.07 s;
//   ignores clicks that come sooner; auto clicker, hose and minigames are exempt). Owns the PixelTier[] list, currency counts/totals,
//   click handling (raycast), spawn/materialize/pulse/hover/spin animation, old-pixel "falling copy" physics (half-size copies, pop in
//   tiny + no collision, swell-and-shrink despawn), vacuum suction (sucked-up pixels swirl round the Vacuum pixel while shrinking into
//   it: SuckRoutine turns each pixel's offset round the vertical axis, vacuumSwirlTurns 0 = 0.6 turns, scaled down for pixels that
//   start close to the target, lazy tumble, disableVacuumSwirl; also used by Vacuum devices) (the old Vacuum pixel that drops after a
//   vacuum click shows the pixels it sucked up as very small, drifting, spinning versions inside its see-through body: Vacuum()
//   collects their tiers in vacuumContents, SpawnFallingCopy adds PixelVacuumContents (PixelVacuumContents.cs, CreateDisplayPixel
//   children) - hideVacuumContents (off), vacuumContentsMax (0 = 16), vacuumContentSize (0 = 0.26 of the cube's edge): new fields use
//   'off' / 0 = default so a scene saved before they existed still gets the feature), bright falling pixels (brightOldPixelTypes =
//   Luminescent: extra emission oldPixelGlowBoost + a fading light trail, lightTrail* settings), shared UI font (UIFont), FormatNumber
//   (public static). The husk helper components (ScaledGravity, OldPixelInfo, OldPixelDespawn...) live in PixelHusks.cs.
// ---------------------------------------------------
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
public partial class PixelClicker : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        ExternalClickBlock = false;
        HoldSpin = false;
        DevClickBlock = false;
        ClickHint = false;
        GodMode = false;
        InfiniteResources = false;
        capacitorCharge = 0;
        transistorBoost = 1d;
        DevNoOldPixelLimit = false;
        DevClickCount = 1;
        OldPixelLanded = null;
        UltraGained = null;
        FlyAwayLaunched = null;
        abbreviateCache = -1;
        WishMultiplier = 1f;
        PixelStatics.ResetOnRestart(OnSceneUnloaded);
    }

    // Settings > Restart reloads the scene but statics survive it: game state from the old game (a Capacitor charge, a Transistor boost,
    // the Golden Hour multiplier, a click block / spin hold of a round cut short) must not carry into the new one. Dev switches stay.
    private static void OnSceneUnloaded()
    {
        ExternalClickBlock = false;
        HoldSpin = false;
        ClickHint = false;
        capacitorCharge = 0;
        transistorBoost = 1d;
        WishMultiplier = 1f;
    }


    // ------------------------------------------------------------------
    // Inspector fields
    // ------------------------------------------------------------------

    [Header("Dragon Cubes")]
    [Tooltip("Turn the seven Dragon Cubes off entirely (they are extremely rare random pixels; gathering all seven lets you summon the cube dragon for a wish).")]
    [SerializeField] private bool disableDragonCubes = false;

    [Min(0f)]
    [Tooltip("Spawn weight of EACH Dragon Cube in the random pixel spawns (a normal pixel is 1; 0 = the default 0.0001, i.e. about 1 in 70,000 pixels each).")]
    [SerializeField] private float dragonCubeSpawnWeight = 0f;

    [Header("God Cubes (Egyptian gods)")]
    [Tooltip("Turn the three Egyptian God Cubes (Slifer, Ra, Obelisk) off entirely. They are the rarest random pixels in the game and only one of each can be held.")]
    [SerializeField] private bool disableGodCubes = false;

    [Min(0f)]
    [Tooltip("Spawn weight of EACH God Cube in the random pixel spawns (a normal pixel is 1; 0 = the default 0.000001, about 1 in 7 million pixels each - 100 times rarer than a Dragon Cube).")]
    [SerializeField] private float godCubeSpawnWeight = 0f;

    [Header("Glow (tiers with Glow ticked)")]
    [Range(0f, 1f)]
    [Tooltip("How much the glow breathes in and out. 0 = steady glow.")]
    [SerializeField] private float glowBreathAmount = 0.3f;

    [Tooltip("Tick to let the Luminescent pixel's glow breathe like the others. Off (default) = it stays at its maximum glow.")]
    [SerializeField] private bool luminescentPulses = false;

    private PixelType activeGlowType;

    /// <summary>The glow breathing factor (around 1): a sine wave, except the Luminescent pixel which holds its maximum.</summary>
    private float GlowBreath(PixelType type, float time, float amountScale = 1f)
    {
        float amount = glowBreathAmount * amountScale;
        if (type == PixelType.Luminescent && !luminescentPulses) return 1f + amount; // steady at the peak
        return 1f + amount * Mathf.Sin(time * glowBreathSpeed * Mathf.PI * 2f);
    }

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
    [SerializeField, HideInInspector] private int looksVersion; // 1 = White/Gray/Black got a custom look; 2 = removed again; 3 = RGB outlines removed too; 4 = Vacuum look added; 5 = Obsidian look added; 6 = Ghost look added; 7 = RGB colour-blind marks; 8 = Singularity gravity well; 9 = Electric look added; 10 = Dragon Cube looks added; 17 = basic pixels got a subtle surface + rim; 18 = their rims became shades of their own colour; 19 = flat solid bevel instead of neon; 20 = Obsidian / Seed got the bevel too; 21 = basic pixels got metallic / smoothness / emission; 22 = Vacuum vortex marks + violet rim; 23 = RGB lost their rim, Gray rim darker; 24 = Black / Gray lost their rim, Obsidian rim darker; 25 = Fire + ore looks added; 26 = Treasure chest look added; 27 = Nuclear look added; 35 = Meteor / Solar looks; 36 = rocky pixels jagged, no outlines; 38 = Vacuum glass container; 39 = Ghost gel blob; 49 = RGB lost their faint emission, Mirror no longer leans on reflections / emission

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

    [Header("Glow Shell (works in builds)")]
    [Tooltip("Tick to turn the glow shell off. Glowing pixels normally get a thin unlit coloured shell on top of the normal glow: emission turned on from code can be stripped from built games, and the shell keeps glowing pixels luminous there.")]
    [SerializeField] private bool disableGlowShell = false;

    [Range(-0.4f, 0.4f)]
    [Tooltip("Added to the shell's strength (about 0.75 for a Glow Intensity of 2.5: the pixel's faces turn almost flat and bright, like emission). 0 = the default look; negative = more shaded, positive = brighter.")]
    [SerializeField] private float glowShellExtra = 0f;

    [Range(-0.3f, 1f)]
    [Tooltip("Added to how far above 1.0 (HDR) the shell's colour goes, per point of the pixel's Glow Intensity (base 0.35). This is what makes Bloom light the pixel up: higher = brighter / whiter, lower = a plain tint. Tick 'Glow Shell In Editor' to tune it in Play mode.")]
    [SerializeField] private float glowShellBrightnessExtra = 0f;

    [Tooltip("Also draw the shell in the Editor. Off by default: in the Editor the real emission already works, and the shell is meant for built games where it can be missing.")]
    [SerializeField] private bool glowShellInEditor = false;

    [Tooltip("Tick to hide the soft round halo around glowing pixels (the shell and the real emission are not affected).")]
    [SerializeField] private bool hideGlowHalo = false;

    private bool glowShell => !disableGlowShell && (!Application.isEditor || glowShellInEditor);
    private bool glowHalo => !hideGlowHalo;
    private float glowShellAlpha => 0.5f + glowShellExtra;

    [Header("Click Pulse")]
    [Tooltip("Turn off the very slight swell of the cube every time it is clicked.")]
    [SerializeField] private bool disableClickPulse = false;

    [Min(0f)]
    [Tooltip("How much the cube swells when clicked (0.03 = 3% bigger at the peak). 0 = the default (0.03).")]
    [SerializeField] private float clickPulseAmount = 0f;

    [Min(0f)]
    [Tooltip("How long the swell lasts (seconds). 0 = the default (0.12).")]
    [SerializeField] private float clickPulseSeconds = 0f;

    private float clickPulseTimer;

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
    /// <summary>The clicker cube's own renderer (the aura cuts the cube out of its glow with a copy of this mesh).</summary>
    public Renderer MainRenderer => pixelRenderer;

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
                        amountPerClick = 1, unlockThreshold = 100 },
        new PixelTier { type = PixelType.Black, displayName = "Black Pixels", color = Color.black,
                        amountPerClick = 1, unlockThreshold = 100 },
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

    [Header("Pixel rules (docs/systems/PixelRules.md)")]
    [Tooltip("OFF (the pixel rules) = old pixels are husks: material that never pays currency again. The Vacuum pixel and Vacuum Device give XP for what they suck up, the first-person axe gives only XP, a hovering Solar pixel keeps daylight up, and the black hole / Ultra Holes no longer pay a husk's value. ON = the old behaviour (husks pay their value again).")]
    [SerializeField] private bool husksPayCurrency = false;
    [Min(0f)]
    [Tooltip("XP one consumed husk gives (Vacuum pixel, Vacuum Device, Ultra Holes), as a share of the XP a manual click gives. Pure husks train Standard, a skill's husks train that skill. 0 = the coded default (0.5).")]
    [SerializeField] private float huskXpShare = 0f;

    [Header("Vacuum Pixel")]
    [Tooltip("Print a console line listing what each Vacuum click re-collected, per pixel type (for checking the numbers).")]
    [SerializeField] private bool logVacuum = true;

    [Tooltip("Seconds old pixels take to fly into the cube when a Vacuum pixel is clicked.")]
    [SerializeField] private float vacuumSuckDuration = 0.4f;

    [Tooltip("Speed curve of the suck-in (time 0..1, progress 0..1). Rising curves pull faster toward the end.")]
    [SerializeField] private AnimationCurve vacuumSuckCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Nuclear Pixel")]
    [Tooltip("Tick to turn off the Nuclear pixel's radiation (its old pixels no longer make their neighbours despawn; the pixel itself stays).")]
    [SerializeField] private bool disableNuclearFx = false;

    [Tooltip("How far an old Nuclear pixel's radiation reaches, in main-pixel widths. 0 = 4.")]
    [SerializeField] private float nuclearRadiusPixels = 0f;

    [Tooltip("How fast other old pixels inside the radiation age, in seconds of age per real second. 0 = 40 (a pixel with a minute left is gone in about a second and a half).")]
    [SerializeField] private float nuclearAgeRate = 0f;

    [Header("Early Pixel Click Effects")]
    [Tooltip("Tick to turn off the small particle pops on the early pixels (White, Gray, Black, Red, Green, Blue, Glass, Luminescent). Players can also change them in Settings > Interface > Click effects.")]
    [SerializeField] private bool disableClickPops = false;

    [Tooltip("How strong the click pops are (0 = 1). 0.5 = half as many particles, 2 = twice as many.")]
    [SerializeField] private float clickPopStrength = 0f;

    [Header("Fire Pixel / Prospector Ores")]
    [Tooltip("Tick to turn off the Fire pixel's burst of flames when it is clicked and its burning old pixels (the pixel itself stays).")]
    [SerializeField] private bool disableFireFx = false;

    [Min(0f)]
    [Tooltip("Seconds an old Fire pixel takes to burn away. 0 = the coded default (6).")]
    [SerializeField] private float fireBurnSeconds = 0f;

    [Tooltip("Tick to turn off the ore pixels' chips, glitter and rich veins.")]
    [SerializeField] private bool disableProspectorFx = false;

    [Header("Meteor and Solar")]
    [Tooltip("Tick to turn off the Meteor's hit particles and the fiery wake of its old pixel (the look's embers come from the look).")]
    [SerializeField] private bool disableMeteorFx = false;

    [Range(0f, 1f)]
    [Tooltip("Chance that a fresh ore pixel is a RICH VEIN (it glitters and pays much more). 0 = the coded default (8%).")]
    [SerializeField] private float richVeinChance = 0f;

    [Min(0f)]
    [Tooltip("How many times its normal payout a rich vein pays. 0 = the coded default (5).")]
    [SerializeField] private float richVeinMultiplier = 0f;

    [Tooltip("Old pixels sucked up swirl round the Vacuum pixel instead of flying straight in. Tick to turn the swirl off.")]
    [SerializeField] private bool disableVacuumSwirl = false;

    [Min(0f)]
    [Tooltip("How many times a sucked-up pixel circles the Vacuum pixel on its way in. 0 = the coded default (0.6). Pixels far from the Vacuum pixel get the full amount, nearby ones less.")]
    [SerializeField] private float vacuumSwirlTurns = 0f;

    [Tooltip("The old Vacuum pixel that drops after a vacuum click shows the pixels it sucked up as very small versions floating inside it.")]
    [SerializeField] private bool hideVacuumContents = false;

    [Min(0)]
    [Tooltip("Most tiny pixels shown inside the Vacuum pixel (a random selection of what it sucked up when there were more). 0 = the coded default (16).")]
    [SerializeField] private int vacuumContentsMax = 0;

    [Range(0f, 0.4f)]
    [Tooltip("Size of each tiny pixel inside the Vacuum pixel, as a fraction of its edge. 0 = the coded default (0.26).")]
    [SerializeField] private float vacuumContentSize = 0f;

    [Header("Old Pixel Physics")]
    [Tooltip("Seconds before an old pixel is destroyed. 0 = never. (Renamed so the old saved 6 s is dropped: the default is 15 s now.)")]
    [SerializeField] private float oldPixelLifetime = 15f;

    [Header("Click Limit")]
    [Min(0f)]
    [Tooltip("Shortest time (seconds) allowed between two manual clicks on the cube. A click that comes sooner is ignored. 0.07 = at most about 14 clicks per second, which stops macros that click faster than a person can. 0 = no limit. The auto clicker, the hose and minigame clicks are not affected.")]
    [SerializeField] private float minClickInterval = 0.07f;

    [Header("Ultra Boosts")]
    [Min(0f)]
    [Tooltip("Each Ultra boost level adds this much to a pixel type's payout multiplier (0.25 = +25% of its normal payout per level).")]
    [SerializeField] private float ultraBonusPerLevel = 0.25f;

    [Header("Potions")]
    [Min(1)]
    [Tooltip("While a potion forces Vacuum pixels to spawn, only every this-many-th Vacuum click actually vacuums the old pixels (the others just pay out). Stops a chained Vacuum potion from re-collecting the whole floor on every click. 1 = every click vacuums.")]
    [SerializeField] private int potionVacuumEvery = 10;

    private int potionVacuumCounter;

    /// <summary>See <see cref="potionVacuumEvery"/>.</summary>
    public int PotionVacuumEvery => Mathf.Max(1, potionVacuumEvery);

    /// <summary>Seconds an old pixel lies around before it despawns.</summary>
    public float FallingCopyLifetime => oldPixelLifetime > 0f ? oldPixelLifetime + PixelSkills.StandardLifetimeBonus : 0f;   // + the Standard skill's 'Lingering pixels' perk

    [Header("Value Upgrades")]
    [Min(0f)]
    [Tooltip("Each Value level adds this much to a pixel type's payout multiplier (1 = +100% of its base payout per level, so level 3 pays x4).")]
    [SerializeField] private float valueBonusPerLevel = 1f;

    [Min(0)]
    [Tooltip("Every this many Value levels the payout is also multiplied by 'Value Milestone Multiplier' (25 = at levels 25, 50, 75...). 0 = no milestones.")]
    [SerializeField] private int valueMilestoneEvery = 25;

    [Min(1f)]
    [Tooltip("Extra payout multiplier for each Value milestone reached (2 = doubles at every milestone).")]
    [SerializeField] private float valueMilestoneMultiplier = 2f;

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

    [Tooltip("Max old pixels kept in the scene (before any Old Pixel Capacity upgrade). The oldest is destroyed first. 0 = no cap.")]
    [SerializeField] private int baseOldPixelCap = 50;

    private int oldPixelCapOverride; // set by the shop's Old Pixel Capacity upgrade (0 = none bought)

    /// <summary>The cap in use: the shop upgrade's value when one is bought, otherwise the Inspector value.</summary>
    /// <summary>Gravity multiplier of old pixels (the Pixel Bank's aim line uses it to predict where a spat pixel lands).</summary>
    public float OldPixelGravityScale => gravityScale;

    /// <summary>Linear drag of old pixels (see <see cref="OldPixelGravityScale"/>).</summary>
    public float OldPixelDrag => fallingCopyDrag;

    public int OldPixelCap => (oldPixelCapOverride > 0 ? Mathf.Max(oldPixelCapOverride, baseOldPixelCap) : baseOldPixelCap) + PixelSkills.StandardCapacityBonus;   // + the Standard skill's 'Roomy floor' perk

    /// <summary>Called by the shop's Old Pixel Capacity upgrade (0 = back to the Inspector value).</summary>
    public void SetOldPixelCap(int value) => oldPixelCapOverride = Mathf.Max(0, value);

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

    [Header("Seed Pixel")]
    [Min(0.5f)]
    [Tooltip("Seconds a planted Seed sprout takes to grow its pixel from nothing to full size.")]
    [SerializeField] private float seedGrowSeconds = 6f;

    [Min(0f)]
    [Tooltip("Seconds a seedling from a cracked Seed shell takes to grow its plant pixel (bought seeds and the Seed pet use 'Seed Grow Seconds'). 0 = the coded default (40).")]
    [SerializeField] private float seedlingGrowSeconds = 0f;

    [Min(0f)]
    [Tooltip("Seconds the grown pixel waits (wobbling) on its sprout before it pops off.")]
    [SerializeField] private float seedHoldSeconds = 1f;

    [Min(1)]
    [Tooltip("The most Seed sprouts that can exist at once (a cracked shell drops several; extra ones are just not planted).")]
    [SerializeField] private int seedSproutCap = 36;

    [Min(0)]
    [Tooltip("The fewest seedlings a cracked Seed shell plants. 0 = the coded default (1).")]
    [SerializeField] private int seedlingsMin = 0;

    [Min(0)]
    [Tooltip("The most seedlings a cracked Seed shell plants (a random number between the two). 0 = the coded default (5).")]
    [SerializeField] private int seedlingsMax = 0;

    [Min(0f)]
    [Tooltip("Each seed has its own despawn timer: seconds from the moment it drops until it withers away if it hasn't popped its pixel yet. 0 = the coded default (45). The pixel it grows is an ordinary old pixel with its own normal despawn timer.")]
    [SerializeField] private float seedSproutLifetime = 0f;

    [Min(0f)]
    [Tooltip("Seeds can't be planted closer together than this (world units between sprout centres). 0 = the coded default (0.7 x a sprout's width).")]
    [SerializeField] private float seedSpacing = 0f;

    [Min(1f)]
    [Tooltip("How big the sprout is drawn compared to an old pixel (bigger = easier to spot).")]
    [SerializeField] private float seedSproutScale = 2.2f;

    [Min(0f)]
    [Tooltip("How strongly a sprout favours rare pixel types: a type's chance is 1 / spawnWeight to this power. 0 = every unlocked type equally likely, 1 = rarer types are proportionally more likely.")]
    [SerializeField] private float seedRarityBias = 1f;

    [Min(2)]
    [Tooltip("This many old Solar pixels touching each other (a connected group) never run out: they stay on the floor indefinitely. 0 = switched off.")]
    [SerializeField] private int solarClusterSize = 5;

    [Min(0.5f)]
    [Tooltip("Two old Solar pixels count as touching when their centres are within this many old-pixel widths of each other.")]
    [SerializeField] private float solarClusterDistance = 1.5f;

    [Min(0f)]
    [Tooltip("How close (in old-pixel widths) an old Solar pixel must lie to a growing sprout to speed it up.")]
    [SerializeField] private float seedSolarReach = 6f;

    [Min(0f)]
    [Tooltip("Growth speed added to a sprout by each old Solar pixel in reach (0.75 = +75% each).")]
    [SerializeField] private float seedSolarBoostEach = 0.75f;

    [Min(0f)]
    [Tooltip("Most growth speed Solar pixels can add to one sprout (3 = up to 4x as fast).")]
    [SerializeField] private float seedSolarBoostMax = 3f;

    [Min(0f)]
    [Tooltip("How close (in old-pixel widths) a Water pixel must splash to a growing sprout or the Seed pet to water it.")]
    [SerializeField] private float seedWaterReach = 5f;

    [Min(0f)]
    [Tooltip("Seconds of growth (sowing time for the Seed pet) a Water pixel's splash adds at once to every growing sprout / planted Seed pet within reach.")]
    [SerializeField] private float seedWaterGrowSeconds = 1.5f;

    [Min(0f)]
    [Tooltip("Each sprout / Seed pet accepts water at most this often (seconds), so a Sorter or auto clicker streaming Water pixels can't make growth instant. 1.5 s of growth per 0.75 s = about double speed at most.")]
    [SerializeField] private float seedWaterCooldown = 0.75f;

    [Min(0f)]
    [Tooltip("How long (seconds) a sprout / Seed pet stays 'sun soaked' (Solar speed-up) after an old Solar pixel was last close to it.")]
    [SerializeField] private float seedSunSoakSeconds = 8f;

    [Header("Click Hint")]
    [Tooltip("Turn off the 'click me' pulse that the cube plays after the first guide text until the first pixel is collected.")]
    [SerializeField] private bool disableClickHint = false;

    [Min(0f)]
    [Tooltip("How much bigger the cube gets at the peak of the click-me pulse (0.12 = +12%).")]
    [SerializeField] private float clickHintAmount = 0.12f;

    [Min(0f)]
    [Tooltip("How much brighter the cube gets at the peak of the click-me pulse (0.35 = +35%).")]
    [SerializeField] private float clickHintBrightness = 0.35f;

    [Min(0.1f)]
    [Tooltip("Click-me pulses per second.")]
    [SerializeField] private float clickHintSpeed = 1.6f;

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

    /// <summary>Set by the Inventory UI while the "Click N more..." guide text shows. With nothing collected yet the cube then pulses to say "click me".</summary>
    public static bool ClickHint;

    private bool clickHintDone;
    private int lastReflectTier = -1;      // the last non-Mirror pixel type collected (what a Mirror pixel reflects)
    private double lastReflectAmount;      // and what that click paid

    /// <summary>While true, clicks on the cube are ignored (set by the pixel bank's hose, which uses the mouse buttons itself).</summary>
    private static bool externalClickBlock;

    /// <summary>Something else (a minigame, the hose, a hovered pet, first person mode) has the mouse: the cube ignores ordinary clicks.</summary>
    public static bool ExternalClickBlock
    {
        get => externalClickBlock || DevClickBlock || PixelFirstPerson.Active || PixelConsumables.HoveringDisarmed || PixelPointerClaim.Claimed || PixelRobotWorker.Hovering || PixelRobotWorker.Piloting;
        set => externalClickBlock = value;
    }

    /// <summary>A minigame (Weak Spots) holds the cube still: no idle spin.</summary>
    public static bool HoldSpin;

    /// <summary>The cube's hit box (a full-size trigger).</summary>
    public Transform HitboxTransform => hitbox;

    /// <summary>Dev tools: the shape placer is open and uses the mouse (the cube ignores clicks).</summary>
    public static bool DevClickBlock;

    /// <summary>Dev tools "god pixel" mode: the mouse spawns / destroys pixels instead of clicking the cube.</summary>
    public static bool GodMode;

    private const string PrefRotation = "PixelClicker.Setting.Rotation";
    private const string PrefPulsing = "PixelClicker.Setting.Pulsing";
    private const string PrefBackground = "PixelClicker.Setting.RunInBackground";
    private bool allowRotation = true;
    private Quaternion startRotation = Quaternion.identity;   // the cube's pose in the scene, restored when rotation is switched off
    private bool allowPulsing = true;

    /// <summary>Player setting (accessibility): turns the cube's idle rotation on or off. Remembered between sessions.</summary>
    public bool AllowRotation
    {
        get => allowRotation;
        set
        {
            allowRotation = value;
            PixelPrefs.SetInt(PrefRotation, value ? 1 : 0);
            if (!value && pixelTransform != null) pixelTransform.rotation = startRotation;   // back to the default pose
        }
    }

    /// <summary>Reads the rotation / pulsing / background / abbreviate settings from PlayerPrefs again (a save file with its own settings was loaded).</summary>
    public void ReloadSettingsFromPrefs()
    {
        allowRotation = PixelPrefs.GetInt(PrefRotation, 1) != 0;
        allowPulsing = PixelPrefs.GetInt(PrefPulsing, 1) != 0;
        if (PixelPrefs.HasKey(PrefBackground)) Application.runInBackground = PixelPrefs.GetInt(PrefBackground) != 0;
        abbreviateCache = -1;
    }

    /// <summary>Player setting: keep the game running while its window is not in focus (alt-tabbed). Remembered between sessions.</summary>
    public bool RunInBackground
    {
        get => Application.runInBackground;
        set { Application.runInBackground = value; PixelPrefs.SetInt(PrefBackground, value ? 1 : 0); }
    }

    /// <summary>Player setting (accessibility): turns the cube's pulsing (size and brightness) on or off. Remembered between sessions.</summary>
    public bool AllowPulsing
    {
        get => allowPulsing;
        set { allowPulsing = value; PixelPrefs.SetInt(PrefPulsing, value ? 1 : 0); }
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
    private static readonly ProfilerMarker solarMarker = new ProfilerMarker("Pixel.SolarClusters");

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

    /// <summary>Raised with the world position when a fly-away (meteor) old pixel shoots off. The stardust minigame listens.</summary>
    public static event System.Action<Vector3> FlyAwayLaunched;

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
        if (PixelFind.First<PixelPets>() == null) gameObject.AddComponent<PixelPets>(); // rare pet versions of the pixels you click
        if (PixelFind.First<PixelSkills>() == null) gameObject.AddComponent<PixelSkills>();   // skill packs: Standard / Mining, the Skills window
        if (PixelFind.First<PixelBlockTooltip>() == null) gameObject.AddComponent<PixelBlockTooltip>();   // hover tooltip for blocks
        if (PixelFind.First<PixelShopNudge>() == null) gameObject.AddComponent<PixelShopNudge>();   // points the player at the Shop button after Cubie's second visit
        if (PixelFind.First<PixelTreasureMap>() == null) gameObject.AddComponent<PixelTreasureMap>();   // Fishing: the treasure map from a message in a bottle
        if (PixelFind.First<PixelWhaleFight>() == null) gameObject.AddComponent<PixelWhaleFight>();   // Fishing: the whale that fights the line
        if (PixelFind.First<PixelGodFind>() == null) gameObject.AddComponent<PixelGodFind>();   // the God Cubes' shows
        if (PixelFind.First<PixelInfinity>() == null) gameObject.AddComponent<PixelInfinity>();
        if (PixelFind.First<PixelCustomKeys>() == null) gameObject.AddComponent<PixelCustomKeys>();   // custom keybinds (My Window > Keybinds)
        if (PixelFind.First<PixelCollectionLog>() == null) gameObject.AddComponent<PixelCollectionLog>();   // the Collection Log book (opened from the Log window)   // the Prime Cubes' Gauntlet window
        if (PixelFind.First<PixelDragonFind>() == null) gameObject.AddComponent<PixelDragonFind>();   // the show when a Dragon Cube turns up
        if (PixelFind.First<PixelPixelGuide>() == null) gameObject.AddComponent<PixelPixelGuide>();   // the Pixel Guide window
        if (PixelFind.First<PixelShuffleBonus>() == null) gameObject.AddComponent<PixelShuffleBonus>();   // the shuffle bonus test (tap right click on filler blocks)
        if (PixelFind.First<PixelFocus>() == null) gameObject.AddComponent<PixelFocus>();
        if (PixelFind.First<PixelTreeMode>() == null) gameObject.AddComponent<PixelTreeMode>();   // tree mode (Woodcutting trees, test pack)   // Focus potions (Lumberjack's Focus...: hold off a skill's hazard for a while)
        if (PixelFind.First<PixelBaitBucket>() == null) gameObject.AddComponent<PixelBaitBucket>();   // Fishing's bait bucket consumable
        if (PixelFind.First<PixelViewWindow>() == null) gameObject.AddComponent<PixelViewWindow>();   // the View window (key V): views, flat view, zoom, orbit controls
        if (PixelFind.First<PixelZoom>() == null) gameObject.AddComponent<PixelZoom>();   // zoom key (X): three levels, hold + wheel for any
        if (PixelFind.First<PixelOrbitCamera>() == null) gameObject.AddComponent<PixelOrbitCamera>();   // orbital camera (third step of the view key)
        if (PixelFind.First<PixelClickerAura>() == null) gameObject.AddComponent<PixelClickerAura>();   // the soft glow behind the clicker cube (Settings)
        if (PixelFind.First<PixelDayNight>() == null) gameObject.AddComponent<PixelDayNight>();   // day / night cycle (sun, ambient, sky tint)
        if (PixelFind.First<PixelMyWindow>() == null) gameObject.AddComponent<PixelMyWindow>();   // the player's own window
        if (PixelFind.First<PixelDaylight>() == null) gameObject.AddComponent<PixelDaylight>();   // Solar's daylight bar
        if (PixelFind.First<PixelNextUp>() == null) gameObject.AddComponent<PixelNextUp>();   // the 'Next up' window (Block Hold)
        if (PixelFind.First<PixelDidYouKnow>() == null) gameObject.AddComponent<PixelDidYouKnow>();   // random 'Did you know?' tips
        if (PixelFind.First<PixelFilters>() == null) gameObject.AddComponent<PixelFilters>();   // the screen filters (CRT, VHS...)
        if (PixelFind.First<PixelBlockHold>() == null) gameObject.AddComponent<PixelBlockHold>();   // the Block Hold upgrade
        if (PixelFind.First<PixelHazardTell>() == null) gameObject.AddComponent<PixelHazardTell>();   // hazard blocks get warning tells (glow, sign, sound)
        if (PixelFind.First<PixelBlockRules>() == null) gameObject.AddComponent<PixelBlockRules>();   // special blocks + hold blocks (data rules)
        if (PixelFind.First<PixelGreatOak>() == null) gameObject.AddComponent<PixelGreatOak>();   // Woodcutting's Great Oak bird nests
        if (PixelFind.First<PixelMold>() == null) gameObject.AddComponent<PixelMold>();
        if (PixelFind.First<PixelGoldenSnitch>() == null) gameObject.AddComponent<PixelGoldenSnitch>();   // the very rare Golden Snitch that flies off the cube   // Foraging's mold zone and glowing finds
        if (PixelFind.First<PixelEggHatch>() == null) gameObject.AddComponent<PixelEggHatch>();   // the egg's hatching show
        if (PixelFind.First<PixelFirstPerson>() == null) gameObject.AddComponent<PixelFirstPerson>(); // run about on the floor at half the size of an old pixel
        if (PixelFind.First<PixelGuideVendor>() == null) gameObject.AddComponent<PixelGuideVendor>(); // Cubie, the shopkeeper who introduces the shop and the vendors
        if (PixelFind.First<PixelDragonWish>() == null) gameObject.AddComponent<PixelDragonWish>(); // the Dragon Cube wish (all seven cubes)
        if (PixelFind.First<PixelOvercharge>() == null) gameObject.AddComponent<PixelOvercharge>(); // clicking an Electric pixel overcharges the auto clicker
        if (PixelFind.First<PixelElectricLinks>() == null) gameObject.AddComponent<PixelElectricLinks>(); // Electric old pixels arc to nearby devices
        if (PixelFind.First<PixelCameraIntro>() == null) gameObject.AddComponent<PixelCameraIntro>();       // start close up, then zoom out
        if (PixelFind.First<PixelFloor>() == null) gameObject.AddComponent<PixelFloor>(); // floor styles (Settings)
        if (PixelFind.First<PixelSkybox>() == null) gameObject.AddComponent<PixelSkybox>(); // skybox styles (Settings, title screen)
        if (PixelFind.First<PixelOrthoView>() == null) gameObject.AddComponent<PixelOrthoView>();   // optional flat (orthographic) view
        if (pixelTransform == null) pixelTransform = transform;
        if (pixelRenderer == null) pixelRenderer = pixelTransform.GetComponentInChildren<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (audioSource == null && (defaultClickSound != null || unlockSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        RunLookFixups();   // brings the saved Pixel Looks list up to date (PixelClickerLookFixups.cs)

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
        startRotation = pixelTransform.rotation;

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
        allowRotation = PixelPrefs.GetInt(PrefRotation, 1) != 0;
        allowPulsing = PixelPrefs.GetInt(PrefPulsing, 1) != 0;
        if (PixelPrefs.HasKey(PrefBackground)) Application.runInBackground = PixelPrefs.GetInt(PrefBackground) != 0;
        colorPropertyId = Shader.PropertyToID(colorPropertyName);

        // Apply start-unlocked flags.
        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i].unlockedAtStart) tiers[i].unlocked = true;
            // Keep any Count typed into the Inspector; Starting Amount raises it if it's higher.
            if (tiers[i].count < tiers[i].startingAmount) tiers[i].count = tiers[i].startingAmount;
        }

        EnsureDragonCubes();
        EnsureGodCubes();
        EnsureGoldenSnitch();
        foreach (PixelTier legacy in tiers) if (legacy != null && legacy.type == PixelType.HornetNest && legacy.displayName == "Hornet Nest") legacy.displayName = "Termite Hive";   // renamed
    }


    private void Start()
    {
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        ApplyUIFont();
        ApplyTierLook(GetClickTier());
        RefreshUI();
    }

    private void Update()
    {
        AnimatePixel();


        CleanOldPixels();
        using (solarMarker.Auto()) UpdateSolarClusters();
        if (Time.timeScale > 0f && !PixelPauseMenu.IsPaused) { UpdateWeevil(); UpdateHazardCharges(); UpdateHazardExpiry(); }

        if ((Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PixelPauseMenu.IsPaused) return; // paused (see PixelPauseMenu); Time Stop still lets the cube be clicked
        if (clicksBlocked || ExternalClickBlock || GodMode || PixelCameraIntro.ClicksLocked) return; // e.g. placing a device (PixelConsumables) or holding the hose (PixelBank)
        bool leftClick = WasClickedThisFrame();
        // A RIGHT click on a hazard block (Pest, Gas Pocket) works it away safely (see DefuseHazard).
        bool rightClick = !leftClick && pixelTransform != null && !isSpawning && PixelInput.RightPressed() && (IsHazard(GetClickTier().type) || GetClickTier().type == PixelType.Bottle);
        if ((!leftClick && !rightClick) || targetCamera == null) return;
        if (ignoreClicksOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Ray ray = targetCamera.ScreenPointToRay(PointerPosition());
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Old pixels are skipped unless they're allowed to block clicks.
            if ((rightClick || (!oldPixelsBlockClicks && !GrabEnabled)) && hit.rigidbody != null && oldPixels.Contains(hit.rigidbody)) continue;   // (a right click on a hazard goes through old pixels in front of the cube)
            if (PixelRobotWorker.BelongsToUfo(hit.transform)) continue;   // the UFO is see-through: clicks go straight through it to the cube

            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform))
            {
                // Too soon after the last click: ignore it (stops macros clicking faster than a person can).
                if (Time.unscaledTime - lastManualClickTime >= minClickInterval)
                {
                    lastManualClickTime = Time.unscaledTime;
                    if (rightClick) { if (GetClickTier().type == PixelType.Bottle) DismissBottle(); else DefuseHazard(); }
                    else Collect();
                }
            }
            return; // first non-ignored hit decides
        }
    }

    /// <summary>
    /// What block is under the pointer? The nearest of the clickable cube and the old pixels lying around (used by the hover tooltip).
    /// isLive = the clickable cube; otherwise 'body' is the old pixel.
    /// </summary>
    public bool TryGetBlockAt(Vector2 screen, out int tierIndex, out bool isLive, out Rigidbody body)
    {
        tierIndex = -1; isLive = false; body = null;
        if (targetCamera == null || pixelTransform == null || tiers == null || tiers.Length == 0) return false;
        Ray ray = targetCamera.ScreenPointToRay(screen);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (PixelRobotWorker.BelongsToUfo(hit.transform)) continue;
            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform))
            {
                if (isSpawning || CubeHidden) return false;
                tierIndex = GetClickTierIndex();
                isLive = true;
                return true;
            }
            Rigidbody rb = hit.rigidbody;
            if (rb != null && (oldPixels.Contains(rb) || PixelRecycler.Holds(rb)))   // (husks in a Recycler left the old-pixel list but still show their tooltip)
            {
                OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
                if (info == null || info.tierIndex < 0 || info.tierIndex >= tiers.Length) return false;
                tierIndex = info.tierIndex;
                body = rb;
                return true;
            }
        }
        return false;
    }

    /// <summary>Is the clickable cube under this screen point, even with old pixels lying / falling in front of it (Block Hold: a pixel dropped by clearing a hazard must not stop you grabbing the next block)?</summary>
    public bool IsLiveCubeAt(Vector2 screen)
    {
        if (targetCamera == null || pixelTransform == null || isSpawning || CubeHidden) return false;
        Ray ray = targetCamera.ScreenPointToRay(screen);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, clickableLayers, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (PixelRobotWorker.BelongsToUfo(hit.transform)) continue;
            if (hit.transform == hitbox || hit.transform == pixelTransform || hit.transform.IsChildOf(pixelTransform)) return true;
        }
        return false;
    }

    /// <summary>Hits already put into the pixel on the cube, and how many it needs (for the hover tooltip).</summary>
    public void GetLiveProgress(out int hits, out int needed)
    {
        hits = hitsOnCurrentPixel;
        needed = tiers != null && tiers.Length > 0 ? NeededClicks(GetClickTier()) : 1;
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

    /// <summary>
    /// A manual click from first person mode: the player aims at the cube from any distance. Follows the same rules as a mouse click
    /// (paused, blocked, intro lock, the minimum time between clicks). Returns true if it clicked.
    /// </summary>
    public bool ManualClickFromAfar()
    {
        if ((Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PixelPauseMenu.IsPaused) return false;
        if (clicksBlocked || GodMode || PixelCameraIntro.ClicksLocked) return false;
        if (Time.unscaledTime - lastManualClickTime < minClickInterval) return false;
        lastManualClickTime = Time.unscaledTime;
        Collect();
        return true;
    }

    /// <summary>A click made by automation (the auto clicker). Same as <see cref="Collect"/>, but flagged as automatic.</summary>
    public void AutoCollect() => CollectInternal(true);

    /// <summary>Dev tools: how many clicks one click counts as (1 = normal). Multiplies payouts, tough-pixel hits and the combo.</summary>
    public static int DevClickCount = 1;

    /// <summary>The Weak Spots round for a fish ended: caught = pay it like a normal click, otherwise it got away (no payout, the next pixel comes).</summary>
    private void FishingDone(bool caught)
    {
        fishingBusy = false;
        if (caught)
        {
            fishingPass = true;
            try { CollectInternal(false); }
            finally { fishingPass = false; }
            return;
        }
        PixelHints.Announce("The fish got away!");
        hitsOnCurrentPixel = 0;
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    private void CollectInternal(bool automatic)
    {
        if (!disableClickPulse) clickPulseTimer = clickPulseSeconds > 0f ? clickPulseSeconds : 0.12f;
        if (blockClicksWhileSpawning && isSpawning) return;
        int clickCount = Mathf.Max(1, DevClickCount);
        if (!automatic) PixelBlockRules.OnManualClick(this);   // a held Whetstone follows every click with a rapid burst of extra clicks

        int tierIndex = GetClickTierIndex();
        PixelTier tier = tiers[tierIndex];
        if (tier.type == PixelType.GoldenSnitch) { PixelGoldenSnitch.CubeClicked(automatic); return; }   // the Golden Snitch is never clicked off the cube: your click makes it fly (PixelGoldenSnitch)
        PixelBlockHold.OnClick(automatic);   // a stake being held grows +0.1 per click; every 5 clicks give the hold meter 1%
        if (tier.stallsHold) PixelBlockHold.Stall(tier.stallSeconds > 0f ? tier.stallSeconds : 1.5f, tier.stallRefill > 0f ? tier.stallRefill : 0.04f);   // every click on a stalling block freezes the hold meter and refills it a little
        if (!automatic && IsHazard(tier.type) && PixelBlockRules.HeldClearsHazard(tier.type)) { ClearHazardNow(tier); return; }   // a held Pesticide: one left click clears it
        if (automatic && IsHazard(tier.type) && !HazardAutoClear) return;   // the auto clicker never touches a hazard: it waits until you deal with it (or it goes away)

        // A Fish is caught in a Weak Spots round: your click starts it, and only a clean run pays (the auto clicker waits for you).
        if (tier.type == PixelType.Fish && !fishingPass)
        {
            if (fishingBusy) return;
            if (PixelWeakspotMinigame.FishingAvailable)
            {
                if (automatic) return;   // reeling a fish in is up to you
                if (PixelWeakspotMinigame.TryStartFishing(FishingDone)) { fishingBusy = true; PlayClickEffects(tier); }
                return;
            }
            // The Weak Spots minigame is switched off: a fish takes a random 7-25 clicks to reel in (the last one pays).
            if (fishNeed == 0) fishNeed = UnityEngine.Random.Range(7, 26);
            fishReel += clickCount;
            if (fishReel < fishNeed)
            {
                hitPunchTimer = hitPunchDuration;
                PlayClickEffects(tier);
                if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, automatic, 0.35f, clickPopStrength);
                if (!disableFarmFx) PixelFarmFx.Hit(tier.type, pixelTransform.position, PixelBaseSize, tier.color, fishReel / (float)fishNeed);
                PixelHit?.Invoke(tierIndex, fishReel, fishNeed, automatic);
                onPixelClicked?.Invoke();
                return;
            }
        }

        // Tough pixels (Clicks To Collect > 1) take several clicks; only the last one pays out.
        int breaks = 1; // pixels broken by this click (more than 1 only with the dev click count)
        int need = NeededClicks(tier);                 // a Treasure chest needs a random number of clicks
        int shownNeed = tier.randomClicks && tier.type == PixelType.Treasure ? -1 : need;  // (the chest doesn't tell you how many it needs)
        if (need > 1)
        {
            hitsOnCurrentPixel += clickCount;
            if (hitsOnCurrentPixel < need)
            {
                hitPunchTimer = hitPunchDuration;
                SetLiveDamage(tier, hitsOnCurrentPixel);
                PlayClickEffects(tier);
                if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, automatic, 0.35f, clickPopStrength);   // a small chip of a tough pixel
                if (tier.type == PixelType.Seed || tier.type == PixelType.Dirt) SeedDigEffect(Mathf.Clamp01(hitsOnCurrentPixel / (float)need));   // digging noises + falling dirt
                if (tier.type != PixelType.Seed && (IsFarm(tier.type) || IsWoodBlock(tier.type) || IsForageBlock(tier.type) || IsFishBlock(tier.type) || tier.type == PixelType.HornetNest || tier.type == PixelType.Toadstool || tier.type == PixelType.Mold || tier.type == PixelType.Jellyfish) && !disableFarmFx) PixelFarmFx.Hit(tier.type, pixelTransform.position, PixelBaseSize, tier.color, hitsOnCurrentPixel / (float)need);
                if (tier.type == PixelType.Dirt) digShrinkTarget = Mathf.Lerp(1f, oldPixelScale, Mathf.Clamp01(hitsOnCurrentPixel / (float)need));   // each dig shrinks it towards old-pixel size
                if (tier.type == PixelType.Pest && !disablePestSwarm) PixelPestSwarm.Scatter(this, pixelTransform.position, 2 + Mathf.RoundToInt(3f * hitsOnCurrentPixel / (float)need));   // bugs spill out of it
                if (tier.type == PixelType.HornetNest && !disableTermites) PixelTermiteSwarm.Scatter(this, pixelTransform.position, 2 + Mathf.RoundToInt(3f * hitsOnCurrentPixel / (float)need));   // termites spill out of the hive
                if ((IsElectricalBlock(tier.type) || tier.type == PixelType.Jellyfish) && !disableFarmFx) PixelElectricFx.Hit(tier.type, pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
                if (tier.type == PixelType.Obsidian && !disableFarmFx) PixelObsidianFx.Hit(pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
                if (IsOre(tier.type) && !disableProspectorFx) OreFx.Hit(pixelTransform.position, PixelBaseSize, tier.color, hitsOnCurrentPixel / (float)need);
                if (tier.type == PixelType.Meteor && !disableMeteorFx) PixelCometFx.Hit(pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
                if (tier.type == PixelType.Treasure && !disableTreasureFx) TreasureFx.Hit(pixelTransform.position, PixelBaseSize, hitsOnCurrentPixel / (float)need);
                if (tier.type == PixelType.Mold) PixelMold.Spread(pixelTransform.position);   // every click spreads the mold further over the fallen pixels
                if (tier.type == PixelType.Whale) PixelWhaleFight.OnHit(this, automatic, hitsOnCurrentPixel, need);   // the first manual hit hooks it: it starts to fight the line
                PixelHit?.Invoke(tierIndex, hitsOnCurrentPixel, shownNeed, automatic);
                onPixelClicked?.Invoke();
                return;
            }
        }
        if (IsHazard(tier.type)) { TriggerHazard(tierIndex, tier); return; }   // hit one too often: it goes off (no payout)
        if (need > 1)
        {
            SetLiveDamage(tier, need); // the pieces that fall away are fully cracked
            PixelFinalHit?.Invoke(tierIndex, need, automatic);   // the last click shows the final count
        }
        if (need > 1)
        {
            breaks = hitsOnCurrentPixel / need;
            hitsOnCurrentPixel %= need;
        }
        else
        {
            breaks = clickCount;
            hitsOnCurrentPixel = 0;
        }

        double basePayout = tier.amountPerClick * breaks;
        if (transistorBoost > 1d) { basePayout *= transistorBoost; transistorBoost = 1d; }   // a Transistor amplified this click
        if (tier.type == PixelType.Transistor) transistorBoost = transistorMultiplier > 0f ? transistorMultiplier : 2f;
        if (tier.type == PixelType.Fuse)
            basePayout *= Time.time - liveSince <= (fuseSeconds > 0f ? fuseSeconds : 3f) ? (fuseBonus > 0f ? fuseBonus : 3f) : 0.3f;   // collected before it burns out: big, after: almost nothing
        if (tier.type == PixelType.Bulb)
            basePayout *= 1d + CountConductors() * (bulbPerConductor > 0f ? bulbPerConductor : 0.1f);   // it shines brighter the more wired-up pixels lie around
        if (tier.type == PixelType.Capacitor)
        {
            basePayout *= 1d + capacitorCharge * (capacitorPayPerCharge > 0f ? capacitorPayPerCharge : 0.15f);   // it pays out everything the other clicks stored in it
            capacitorCharge = 0;
        }
        else capacitorCharge = Mathf.Min(capacitorChargeCap > 0 ? capacitorChargeCap : 40, capacitorCharge + breaks);
        if (tier.randomClicks && tier.type == PixelType.Treasure) basePayout *= TreasureMultiplier(need);   // the more clicks the chest took, the bigger the reward
        if (tier.randomClicks && tier.type == PixelType.Whale) basePayout *= PixelWhaleFight.RewardMultiplier(need);   // so does a whale: the longer the fight, the bigger the catch
        if (tier.type == PixelType.Wheat) basePayout *= 1d + Mathf.Min(wheatMaxSprouts > 0 ? wheatMaxSprouts : 8, SeedSprout.Count) * (wheatPerSprout > 0f ? wheatPerSprout : 0.25f);   // a busy field feeds the wheat
        bool richBreak = liveRich && IsOre(tier.type);
        if (richBreak) basePayout *= richVeinMultiplier > 0f ? richVeinMultiplier : 5f;   // a rich vein pays big
        if (tier.randomPayout)
        {
            // A fresh random amount for every pixel harvested (a big dev click count uses the average instead of rolling thousands of times).
            int lo = Mathf.Min(tier.payoutMin, tier.payoutMax), hi = Mathf.Max(tier.payoutMin, tier.payoutMax);
            if (breaks > 50) basePayout = (lo + hi) * 0.5d * breaks;
            else
            {
                basePayout = 0d;
                for (int roll = 0; roll < breaks; roll++) basePayout += UnityEngine.Random.Range(lo, hi + 1);
            }
        }
        double amount = basePayout * PayoutMultiplier(tierIndex) * clickMultiplier * (automatic ? 1d : ManualClickBonus) * PixelBlockRules.HeldPayoutFactor * liveStake;
        tier.timesCollected += breaks;
        AddCurrency(tierIndex, amount);
        if (IsGodCube(tier.type) && amount > 0d) PixelGodFind.Play(tierIndex, pixelTransform.position);   // each God has its own show
        else if (IsDragonCube(tier.type) && amount > 0d) PixelDragonFind.Play(tierIndex, pixelTransform.position);   // the "you found one" show
        PixelCollected?.Invoke(tierIndex, amount, automatic);

        // A Mirror pixel also counts as a click on the last other pixel type collected: it pays that click's amount again.
        if (tier.type == PixelType.Mirror)
        {
            if (IsValidTier(lastReflectTier) && lastReflectAmount > 0d)
            {
                tiers[lastReflectTier].timesCollected += 1;
                AddCurrency(lastReflectTier, lastReflectAmount);
                PixelCollected?.Invoke(lastReflectTier, lastReflectAmount, automatic);
            }
        }
        else
        {
            lastReflectTier = tierIndex;
            lastReflectAmount = amount;
        }

        PlayClickEffects(tier);
        if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, automatic, 1f, clickPopStrength);
        if (tier.type == PixelType.Fire && !disableFireFx) FireBurstFx.Play(pixelTransform.position, PixelBaseSize);
        if (tier.type == PixelType.Solar) PixelDaylight.OnSolarClick();
        if (tier.type == PixelType.Treasure) OpenTreasure(tierIndex, need);
        if (tier.type == PixelType.Fertilizer) Fertilize();
        if (tier.type == PixelType.Bait) PixelBait.Drop(this, pixelTransform.position);
        QueueBaitAfter(tier.type);   // (Fishing: a Water pixel may make the next one Bait)
        if (tier.type == PixelType.Bottle) PixelTreasureMap.Begin(this, pixelTransform.position);   // the glass breaks and a treasure map unrolls
        if (tier.type == PixelType.Whale) PixelWhaleFight.OnCaught(this, need, amount);
        if ((IsFarm(tier.type) || IsWoodBlock(tier.type) || IsForageBlock(tier.type) || IsFishBlock(tier.type)) && tier.type != PixelType.Seed && !disableFarmFx) PixelFarmFx.Break(tier.type, pixelTransform.position, PixelBaseSize, tier.color);
        if (tier.type == PixelType.Dirt) PixelAudio.Play("seed_dig");   // the last dig
        if ((IsElectricalBlock(tier.type) || tier.type == PixelType.Jellyfish) && !disableFarmFx) PixelElectricFx.Break(tier.type, pixelTransform.position, PixelBaseSize);
        if (tier.type == PixelType.Obsidian && !disableFarmFx) PixelObsidianFx.Break(pixelTransform.position, PixelBaseSize);
        if (IsOre(tier.type) && !disableProspectorFx)
        {
            OreFx.Break(pixelTransform.position, PixelBaseSize, tier.color, richBreak);
            if (richBreak) { liveRich = false; if (richGlitter != null) { Destroy(richGlitter); richGlitter = null; } }
        }
        if (tier.vacuum && VacuumThisClick(tierIndex)) Vacuum(tierIndex); // before this pixel's own old copy spawns, so it isn't sucked up too
        if (tier.type == PixelType.Seed) { SeedDigEffect(1f); for (int s = 0, n = UnityEngine.Random.Range(Mathf.Max(1, seedlingsMin), Mathf.Max(Mathf.Max(1, seedlingsMin), seedlingsMax > 0 ? seedlingsMax : 5) + 1); s < n; s++) SpawnSeedSprout(); } // the shell cracked: 1-5 seedlings (they grow plant pixels) come out instead of an old pixel
        else if (spawnFallingCopy) SpawnFallingCopy(tierIndex, amount);

        // Roll the next pixel AFTER the click so a freshly unlocked tier can appear immediately.
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());

        onPixelClicked?.Invoke();
    }

    /// <summary>Adds to a tier's amount (use negative via <see cref="TrySpend"/> instead) and checks unlocks.</summary>
    public void AddCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0) return;
        if (IsGodCube(tiers[tierIndex].type))
        {
            if (tiers[tierIndex].count >= 1d) return;   // one of each God Cube, ever: a second one is never added
            amount = 1d;
        }

        bool firstOfRareDrop = tiers[tierIndex].rareDrop && tiers[tierIndex].totalCollected <= 0d;
        tiers[tierIndex].count += amount;
        tiers[tierIndex].totalCollected += amount;
        CurrencyGained?.Invoke(tierIndex, amount);
        if (firstOfRareDrop) AnnounceDragonCube(tierIndex);

        CheckUnlocks();
        NotifyChanged();
    }

    /// <summary>The first time each Dragon Cube turns up: a log line (and a tip the very first time), plus a note when all seven are held.</summary>
    private void AnnounceDragonCube(int tierIndex)
    {
        if (IsInfinityStone(tiers[tierIndex].type))
        {
            PixelHints.Announce("You found the " + tiers[tierIndex].displayName + "! Only one of each Prime Cube can exist.");
            PixelHints.Trigger("stone_first");
            return;
        }
        if (IsGodCube(tiers[tierIndex].type))
        {
            PixelHints.Announce("You found " + tiers[tierIndex].displayName + "! Only one of each God Cube can exist.");
            PixelHints.Trigger("god_first");
            return;
        }
        if (tiers[tierIndex].type == PixelType.GoldenSnitch)
        {
            PixelHints.Announce("Your first Golden Snitch! It is in your Inventory now.");
            return;
        }
        if (tiers[tierIndex].type == PixelType.InfinityCubelette)
        {
            PixelHints.Announce("You forged the Prime Cubelette!");
            return;
        }
        PixelHints.Announce("You found a " + tiers[tierIndex].displayName + "!");
        PixelHints.Trigger("dragon_first");
        if (HasAllDragonCubes) PixelHints.Announce("You hold all seven Dragon Cubes! Summon the dragon.");
    }

    /// <summary>A random Dragon Cube tier the player does not hold right now (so a Dragon Seed grows one they lack). -1 when they hold all seven.</summary>
    public int PickMissingDragonCube()
    {
        System.Collections.Generic.List<int> missing = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].rareDrop && IsDragonCube(tiers[i].type) && tiers[i].count < 1d) missing.Add(i);
        return missing.Count == 0 ? -1 : missing[UnityEngine.Random.Range(0, missing.Count)];
    }

    /// <summary>Any one of the seven Dragon Cube tiers at random (-1 if they don't exist). Used by the Dragon Seed under Infinite resources.</summary>
    public int PickAnyDragonCube()
    {
        System.Collections.Generic.List<int> all = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tiers.Length; i++) if (tiers[i].rareDrop && IsDragonCube(tiers[i].type)) all.Add(i);
        return all.Count == 0 ? -1 : all[UnityEngine.Random.Range(0, all.Count)];
    }

    /// <summary>Does the player hold at least one of each of the seven Dragon Cubes?</summary>
    public bool HasAllDragonCubes
    {
        get
        {
            for (int s = 0; s < 7; s++)
            {
                int index = IndexOf((PixelType)((int)PixelType.DragonCube1 + s));
                if (index < 0 || tiers[index].count < 1d) return false;
            }
            return true;
        }
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

    // Ultra pixels are ONE general currency now (not per pixel type): minigames reward them, upgrades will spend them.
    private long ultraPixels, pendingLegacyUltra;

    /// <summary>The shared Ultra roll used by the minigames: a 'chance' (0..1) of giving one Ultra pixel. Returns true if it did.</summary>
    public bool RollUltra(float chance)
    {
        if (chance <= 0f || UnityEngine.Random.value >= chance) return false;
        AddUltra(1);
        return true;
    }

    /// <summary>Gives Ultra pixels.</summary>
    public void AddUltra(long amount)
    {
        if (amount <= 0) return;
        ultraEarned += amount;
        ultraPixels += amount;
        NotifyChanged();
        UltraGained?.Invoke();
    }

    /// <summary>Restores the Ultra pixels held when loading a save (a negative value = an older save: the old per-pixel counts are added up instead).</summary>
    public void RestoreUltra(long held)
    {
        ultraPixels = held >= 0 ? held : pendingLegacyUltra;
        pendingLegacyUltra = 0;
        ultraEarned = System.Math.Max(ultraEarned, ultraPixels);
        NotifyChanged();
    }

    /// <summary>How much each Ultra boost level adds to a pixel type's payout multiplier.</summary>
    public float UltraBonusPerLevel => ultraBonusPerLevel;

    /// <summary>Is this a valid index into the tier list?</summary>
    public bool IsValidTierIndex(int index) => IsValidTier(index);

    /// <summary>A pixel type's payout multiplier from its Ultra boost level (1 = no boost).</summary>
    public double UltraMultiplier(int tierIndex) =>
        IsValidTier(tierIndex) ? 1d + tiers[tierIndex].ultraLevel * (double)ultraBonusPerLevel : 1d;

    /// <summary>Payout multiplier of a pixel type at a given Value level (1 at level 0).</summary>
    public double ValueMultiplierAt(int level)
    {
        level = System.Math.Max(0, level);
        double m = 1d + level * (double)valueBonusPerLevel;
        if (valueMilestoneEvery > 0) m *= System.Math.Pow(valueMilestoneMultiplier, level / valueMilestoneEvery);
        return m;
    }

    /// <summary>A pixel type's payout multiplier from its Value upgrade level (1 = no upgrade).</summary>
    public double ValueMultiplier(int tierIndex) => IsValidTier(tierIndex) ? ValueMultiplierAt(tiers[tierIndex].valueLevel) : 1d;

    /// <summary>Everything that scales a pixel type's payout per collect: Ultra boosts x Value upgrades.</summary>
    public double PayoutMultiplier(int tierIndex) => UltraMultiplier(tierIndex) * ValueMultiplier(tierIndex) * WishMultiplier;

    /// <summary>Spends a pixel type's own currency to raise its Value level by one. Returns false if you can't afford it.</summary>
    public bool TryBuyValueLevel(int tierIndex, double cost)
    {
        if (!IsValidTier(tierIndex) || !TrySpend(tierIndex, cost)) return false;
        tiers[tierIndex].valueLevel++;
        NotifyChanged();
        return true;
    }

    /// <summary>Spends Ultra pixels to raise a pixel type's boost level by one. Returns false if you don't have enough.</summary>
    public bool TryBuyUltraBoost(int tierIndex, long cost)
    {
        if (!IsValidTier(tierIndex) || cost < 0 || (!InfiniteResources && ultraPixels < cost)) return false;
        if (!InfiniteResources) ultraPixels -= cost;
        tiers[tierIndex].ultraLevel++;
        NotifyChanged();
        return true;
    }

    /// <summary>Ultra pixels held.</summary>
    public long TotalUltra => ultraPixels;

    /// <summary>Dev cheat (set by Dev Tools): everything costs nothing - spending always works and takes nothing away.</summary>
    public static bool InfiniteResources;

    /// <summary>Dev tools: the limit on how many old pixels can lie around is switched off while true.</summary>
    public static bool DevNoOldPixelLimit;

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
    public void LoadTierState(PixelType type, double count, double totalCollected, bool unlocked, bool spawnDisabled = false, long timesCollected = 0, long ultraCount = 0, int ultraLevel = 0, int valueLevel = 0)
    {
        int index = IndexOf(type);
        if (index < 0) return;

        PixelTier tier = tiers[index];
        tier.count = System.Math.Max(0d, count);
        tier.totalCollected = System.Math.Max(0d, totalCollected);
        tier.unlocked = unlocked || tier.unlockedAtStart;
        tier.spawnDisabled = spawnDisabled;
        tier.timesCollected = System.Math.Max(0L, timesCollected);
        pendingLegacyUltra += System.Math.Max(0L, ultraCount);   // older saves counted Ultra per pixel type: RestoreUltra adds them up
        tier.ultraLevel = System.Math.Max(0, ultraLevel);
        tier.valueLevel = System.Math.Max(0, valueLevel);
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
            currentTierIndex = RollTier();
            Materialize(GetClickTier());
        }
    }

    /// <summary>Call after <see cref="LoadTierState"/>: re-rolls the pixel on screen and refreshes every display.</summary>
    public void FinishLoad()
    {
        ClearForcedSpawnTier();
        if (randomizeSpawnTier) currentTierIndex = RollTier();
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
    private int CopyCap => DevNoOldPixelLimit ? 0 : PixelTimeStop.IsStopped ? (timeStopStockpileMax <= 0 ? 0 : Mathf.Max(timeStopStockpileMax, OldPixelCap)) : OldPixelCap;

    private static float AnimDelta => (PixelTimeStop.IsStopped || PixelTimeStop.IsSlowed || PixelTitleScreen.Showing) ? Time.unscaledDeltaTime : Time.deltaTime;

    /// <summary>0..1 pulse that tells a brand-new player to click the cube: only while the guide text shows and nothing has been collected yet.</summary>
    private float ClickHintPulse(float time)
    {
        if (disableClickHint || clickHintDone || !ClickHint) return 0f;
        foreach (PixelTier t in tiers)
            if (t.totalCollected > 0d) { clickHintDone = true; return 0f; }
        return 0.5f + 0.5f * Mathf.Sin(time * clickHintSpeed * Mathf.PI * 2f);
    }

    private float solarClusterTimer;
    private readonly System.Collections.Generic.List<Rigidbody> solarBodies = new System.Collections.Generic.List<Rigidbody>();
    private readonly System.Collections.Generic.List<OldPixelDespawn> solarDespawns = new System.Collections.Generic.List<OldPixelDespawn>();
    private readonly System.Collections.Generic.List<int> solarGroup = new System.Collections.Generic.List<int>();
    private bool[] solarSeen = new bool[0];

    /// <summary>
    /// Old Solar pixels that touch each other in a group of <see cref="solarClusterSize"/> or more are kept alive (they never age
    /// or despawn while the group is complete). Checked a few times a second; the keep-alive lasts a little longer than that.
    /// </summary>
    private void UpdateSolarClusters()
    {
        if (solarClusterSize < 2 || Time.timeScale <= 0f) return;
        solarClusterTimer -= Time.unscaledDeltaTime;
        if (solarClusterTimer > 0f) return;
        solarClusterTimer = 0.15f;

        int solar = IndexOf(PixelType.Solar);
        if (solar < 0) return;
        solarBodies.Clear();
        solarDespawns.Clear();
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null || info.tierIndex != solar) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d == null || d.IsDespawning) continue;
            solarBodies.Add(rb);
            solarDespawns.Add(d);
        }
        int n = solarBodies.Count;
        if (n < solarClusterSize) return;

        float reach = OldPixelWorldSize * solarClusterDistance, reachSqr = reach * reach;
        if (solarSeen.Length < n) solarSeen = new bool[n * 2];
        for (int i = 0; i < n; i++) solarSeen[i] = false;
        for (int start = 0; start < n; start++)
        {
            if (solarSeen[start]) continue;
            solarGroup.Clear();
            solarGroup.Add(start);
            solarSeen[start] = true;
            for (int g = 0; g < solarGroup.Count; g++)
            {
                Vector3 from = solarBodies[solarGroup[g]].position;
                for (int j = 0; j < n; j++)
                {
                    if (solarSeen[j] || (solarBodies[j].position - from).sqrMagnitude > reachSqr) continue;
                    solarSeen[j] = true;
                    solarGroup.Add(j);
                }
            }
            if (solarGroup.Count < solarClusterSize) continue;
            foreach (int member in solarGroup)
            {
                solarDespawns[member].KeepAlive();
                OldPixelSolarAura aura = solarBodies[member].GetComponent<OldPixelSolarAura>();
                if (aura != null) aura.Keep();   // a complete group glows
            }
        }
    }

    /// <summary>How big a Seed sprout is drawn, in world units (an old pixel's width x the sprout scale).</summary>
    public float SeedSproutWorldSize => OldPixelWorldSize * seedSproutScale;

    [Tooltip("Old Solar pixels last this many times as long as other old pixels, so it is easier to gather a group of them. 0 = 1.6.")]
    [SerializeField] private float solarLifetimeFactor = 0f;

    private float LifetimeFactor(int tierIndex)
    {
        if (!IsValidTier(tierIndex)) return 1f;
        float factor = tiers[tierIndex].type == PixelType.Solar ? (solarLifetimeFactor > 0f ? solarLifetimeFactor : 1.6f) : 1f;
        return factor * PixelSkills.SkillLifetimeFactor(tiers[tierIndex].type);   // + a skill perk (Woodcutting level 3: wood lasts longer)
    }

    /// <summary>The edge length of an old pixel in world units.</summary>
    public float OldPixelWorldSize => PixelBaseSize * oldPixelScale;

    /// <summary>The 'plant' pixels: what the seedlings of a cracked Seed shell grow (Wheat, Pollen, Golden Fruit and Wood).</summary>
    public static bool IsPlant(PixelType type) => PixelBlockTags.Has(type, PixelTag.Plant);   // (the tag table in PixelBlockTags.cs)

    /// <summary>A random unlocked plant pixel (by spawn weight, so Golden Fruit stays rare); falls back to any growable type when no plant is unlocked yet. -1 if none.</summary>
    public int RandomPlantPixelTier()
    {
        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].unlocked && IsPlant(tiers[i].type)) total += Mathf.Max(0.01f, tiers[i].spawnWeight);
        if (total <= 0f) return RandomSeedPixelTier();
        float pick = UnityEngine.Random.value * total;
        int last = -1;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!tiers[i].unlocked || !IsPlant(tiers[i].type)) continue;
            last = i;
            pick -= Mathf.Max(0.01f, tiers[i].spawnWeight);
            if (pick <= 0f) return i;
        }
        return last;
    }

    /// <summary>A random pixel type a Seed sprout can grow: unlocked, not a rare drop, not a fly-away type, not a Seed itself and never a Vacuum or Singularity pixel (those can still be planted from bought seeds). -1 if none.</summary>
    public int RandomSeedPixelTier()
    {
        // Golden Fruit: a rare treat from a fully grown sprout (once that type is unlocked).
        int fruit = IndexOf(PixelType.GoldenFruit);
        if (fruit >= 0 && tiers[fruit].unlocked && UnityEngine.Random.value < (fruitSproutChance > 0f ? fruitSproutChance : 0.03f)) return fruit;

        // Rarer types (lower spawn weight) are likelier: chance ~ 1 / weight^bias.
        System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();
        float total = 0f;
        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            if (!t.unlocked || t.rareDrop || t.flyAway || t.type == PixelType.Seed || t.type == PixelType.Singularity || t.type == PixelType.Vacuum || IsStandalone(t.type)) continue;
            float w = 1f / Mathf.Pow(Mathf.Max(0.02f, t.spawnWeight), seedRarityBias);
            candidates.Add(i);
            weights.Add(w);
            total += w;
        }
        if (candidates.Count == 0) return -1;
        float pick = UnityEngine.Random.value * total;
        for (int n = 0; n < candidates.Count; n++)
        {
            pick -= weights[n];
            if (pick <= 0f) return candidates[n];
        }
        return candidates[candidates.Count - 1];
    }

    /// <summary>Extra growth speed (0 = none) a sprout or Seed pet at 'position' gets from old Solar pixels lying close to it.</summary>
    public float SolarSeedBoostAt(Vector3 position)
    {
        int solar = IndexOf(PixelType.Solar), dirt = IndexOf(PixelType.Dirt);
        float solarEach = solar >= 0 ? seedSolarBoostEach : 0f;
        float dirtEach = dirt >= 0 ? (dirtSeedBoost > 0f ? dirtSeedBoost : 0.2f) : 0f;   // old Dirt pixels help sprouts a little too
        if (solarEach <= 0f && dirtEach <= 0f) return 0f;
        float reach = OldPixelWorldSize * seedSolarReach, reachSqr = reach * reach;
        float boost = 0f;
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null || (rb.position - position).sqrMagnitude > reachSqr) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            if (info.tierIndex == solar) boost += solarEach;
            else if (info.tierIndex == dirt) boost += dirtEach;
        }
        return Mathf.Min(seedSolarBoostMax, boost) + PixelDaylight.GrowthBonus;   // daylight (raised by clicking Solar) speeds growth everywhere
    }

    // ---- Farming pixels ----

    [Header("Farming Pixels")]
    [Tooltip("Growth speed an old Dirt pixel near a sprout adds (counts towards the same cap as Solar pixels). 0 = 0.2.")]
    [SerializeField] private float dirtSeedBoost = 0f;

    [Tooltip("Tick to turn off the farming blocks' hit and break particles (the ambient particles come from their looks).")]
    [SerializeField] private bool disableFarmFx = false;

    [Tooltip("Wheat: extra payout per growing sprout when you click it (0.25 = +25% each). 0 = 0.25.")]
    [SerializeField] private float wheatPerSprout = 0f;

    [Tooltip("Wheat: at most this many sprouts count. 0 = 8.")]
    [SerializeField] private int wheatMaxSprouts = 0;

    [Tooltip("Pollen: an old Pollen pixel within this many main-pixel widths of a sprout makes its grown pixel pay double. 0 = 3.")]
    [SerializeField] private float pollenReachPixels = 0f;

    [Tooltip("Golden Fruit: chance that a sprout grows a Golden Fruit instead of a random pixel (once Golden Fruit is unlocked). 0 = 3%.")]
    [SerializeField] private float fruitSproutChance = 0f;

    [Tooltip("Weevil: seconds between bites while a Weevil is on the cube (each bite eats one growing sprout until you click it). 0 = 2.5.")]
    [SerializeField] private float weevilBiteSeconds = 0f;

    private float weevilTimer;
    private bool weevilTold;

    /// <summary>Is an old Pollen pixel lying close to this spot (a sprout popping off here pays double)?</summary>
    public bool PollenNear(Vector3 position)
    {
        int pollen = IndexOf(PixelType.Pollen);
        if (pollen < 0) return false;
        float reach = PixelBaseSize * (pollenReachPixels > 0f ? pollenReachPixels : 3f), reachSqr = reach * reach;
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null || (rb.position - position).sqrMagnitude > reachSqr) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info != null && info.tierIndex == pollen) return true;
        }
        return false;
    }

    /// <summary>While a Weevil is the pixel on the cube it bites a growing sprout every few seconds until you click it.</summary>
    private void UpdateWeevil()
    {
        if (!IsValidTier(currentTierIndex) || tiers[currentTierIndex].type != PixelType.Weevil || isSpawning || pixelTransform == null) { weevilTimer = 0f; return; }
        weevilTimer += Time.deltaTime;
        if (weevilTimer < (weevilBiteSeconds > 0f ? weevilBiteSeconds : 2.5f)) return;
        weevilTimer = 0f;
        if (SeedSprout.EatOne(out Vector3 at))
        {
            SeedDigFx.Play(at, PixelBaseSize * 0.6f, 0.5f);
            if (!weevilTold) { weevilTold = true; PixelHints.Announce("A weevil is eating your sprouts! Click it!"); }
        }
    }

    /// <summary>Fertilizer: every growing sprout and Seed pet on the field jumps ahead at once.</summary>
    private void Fertilize()
    {
        WaterSplashAt(pixelTransform.position, 1000f);
        SeedDigFx.Play(pixelTransform.position, PixelBaseSize, 0.8f);
    }

    /// <summary>How long a sprout / Seed pet stays sun soaked after old Solar pixels were last near it.</summary>
    public float SeedSunSoakSeconds => seedSunSoakSeconds;

    [Header("Water splash pieces")]
    [Tooltip("Turn the splash pieces off: an old Water pixel then just puffs into droplets where it lands, as before.")]
    [SerializeField] private bool disableWaterPieces = false;

    [Min(0)]
    [Tooltip("How many smaller Water pixels an old Water pixel breaks into when it splashes. 0 = 4.")]
    [SerializeField] private int waterPieceCount = 0;

    [Min(0f)]
    [Tooltip("Size of each piece as a fraction of the old Water pixel. 0 = 0.4.")]
    [SerializeField] private float waterPieceSize = 0f;

    [Min(0f)]
    [Tooltip("How fast the pieces are flung out (sideways; they go up about as fast). 0 = 2.2.")]
    [SerializeField] private float waterPieceSpeed = 0f;

    [Min(0f)]
    [Tooltip("Longest a piece flies before it breaks into droplets anyway (seconds); it also breaks the moment it lands. 0 = 1.2.")]
    [SerializeField] private float waterPieceSeconds = 0f;

    public int WaterPieces => disableWaterPieces ? 0 : (waterPieceCount > 0 ? waterPieceCount : 4);
    public float WaterPieceSize => waterPieceSize > 0f ? waterPieceSize : 0.4f;
    public float WaterPieceSpeed => waterPieceSpeed > 0f ? waterPieceSpeed : 2.2f;
    public float WaterPieceSeconds => waterPieceSeconds > 0f ? waterPieceSeconds : 1.2f;

    /// <summary>Minimum seconds between two waterings of the same sprout / Seed pet.</summary>
    public float SeedWaterCooldown => seedWaterCooldown;

    /// <summary>A Water pixel splashed at 'point': every growing sprout and the Seed pet within reach gets an instant bit of growth (rate-limited per target).</summary>
    public void WaterSplashAt(Vector3 point, float reachOverride = 0f)
    {
        float reach = reachOverride > 0f ? reachOverride : OldPixelWorldSize * seedWaterReach;
        SeedSprout.WaterAll(point, reach, seedWaterGrowSeconds, seedWaterCooldown);
        PixelPets.WaterPets(point, reach, seedWaterGrowSeconds, seedWaterCooldown);
    }

    /// <summary>What one harvest of this pixel type would pay now (a random payout rolled for random-payout types, with the Value / Ultra multipliers).</summary>
    public double RollOldPixelAmount(int tierIndex)
    {
        if (!IsValidTier(tierIndex)) return 0d;
        PixelTier t = tiers[tierIndex];
        double basePayout = t.amountPerClick;
        if (t.randomPayout) basePayout = UnityEngine.Random.Range(Mathf.Min(t.payoutMin, t.payoutMax), Mathf.Max(t.payoutMin, t.payoutMax) + 1);
        return basePayout * PayoutMultiplier(tierIndex) * clickMultiplier;
    }

    /// <summary>The digging sound and a burst of dirt and dust falling from the shell; 'progress' 0-1 = how close it is to cracking.</summary>
    private void SeedDigEffect(float progress)
    {
        if (pixelTransform == null) return;
        PixelAudio.Play("seed_dig");
        SeedDigFx.Play(pixelTransform.position, PixelBaseSize, Mathf.Clamp01(0.25f + progress * 0.75f));
    }

    /// <summary>The least distance between two planted seeds (world units).</summary>
    public float SeedSpacing => seedSpacing > 0f ? seedSpacing : SeedSproutWorldSize * 0.7f;

    /// <summary>Is there room to plant a seed here? False when another sprout (or a planted Seed pet) is too close - seeds can't sit on top of each other.</summary>
    public bool SeedSpotFree(Vector3 position)
    {
        float r = SeedSpacing;
        return !SeedSprout.AnyNear(position, r, null) && !PixelPets.PlanterNear(position, r);
    }

    /// <summary>Plants a Seed sprout straight into the floor at 'position' (it grows a random pixel like any other). False when too many sprouts exist. Used by the Seed pet.</summary>
    public bool PlantSeedSproutAt(Vector3 position, int growTier = -1)
    {
        if (SeedSprout.Count >= seedSproutCap || !SeedSpotFree(position)) return false;
        GameObject go = new GameObject("Seed Sprout");
        go.transform.position = position;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) go.layer = fallingCopyLayer;
        SeedSprout sprout = go.AddComponent<SeedSprout>();
        sprout.Setup(this, OldPixelWorldSize, seedGrowSeconds, seedHoldSeconds, seedSproutScale);
        sprout.ForceTier(growTier);
        sprout.SetLifetime(seedSproutLifetime > 0f ? seedSproutLifetime : 45f);
        sprout.PlantNow();
        return true;
    }

    /// <summary>A cracked Seed shell: a small sprout pops out, falls, plants itself and grows a random old pixel (see <see cref="SeedSprout"/>).</summary>
    private void SpawnSeedSprout()
    {
        if (pixelTransform == null || SeedSprout.Count >= seedSproutCap) return;
        float unit = OldPixelWorldSize;

        GameObject go = new GameObject("Seed Sprout");
        go.transform.SetPositionAndRotation(pixelTransform.position, UnityEngine.Random.rotation);
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) go.layer = fallingCopyLayer;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = Vector3.one * unit * 0.5f;
        box.center = new Vector3(0f, unit * 0.3f, 0f);
        box.sharedMaterial = SharedOldPixelPhysicsMaterial();
        // A sprout never collides with the live cube (it starts inside it), and, like a fresh old pixel, it is a ghost for a moment
        // (a trigger) so many sprouts born at the same spot don't jam against each other.
        foreach (Collider live in pixelTransform.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(box, live);
        if (popNoCollisionSeconds > 0f) box.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.0001f, fallingCopyMass);
        rb.useGravity = false;
        rb.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = fallingCopyDrag;
        rb.angularDamping = fallingCopyAngularDrag;
#else
        rb.drag = fallingCopyDrag;
        rb.angularDrag = fallingCopyAngularDrag;
#endif
        if (gravityScale > 0f) go.AddComponent<ScaledGravity>().scale = gravityScale;

        Vector2 flat = UnityEngine.Random.insideUnitCircle;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector2.right;
        flat.Normalize();
        Vector3 direction = new Vector3(flat.x, UnityEngine.Random.Range(popVerticalRange.x, popVerticalRange.y), flat.y).normalized;
        rb.AddForce(direction * UnityEngine.Random.Range(popSpeedRange.x, popSpeedRange.y), ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y), ForceMode.VelocityChange);

        SeedSprout sprout = go.AddComponent<SeedSprout>();
        float grow = seedlingGrowSeconds > 0f ? seedlingGrowSeconds : 40f;   // seedlings from a shell grow slowly
        sprout.Setup(this, unit, grow, seedHoldSeconds, seedSproutScale);
        sprout.SetGhost(popNoCollisionSeconds + 0.15f);
        sprout.SetLifetime((seedSproutLifetime > 0f ? seedSproutLifetime : 45f) + Mathf.Max(0f, grow - seedGrowSeconds));   // the despawn timer waits for the longer growth
        sprout.PlantsOnly();   // a cracked shell's seedlings grow plant pixels only
    }

    private void AnimatePixel()
    {
        float time = Time.time;

        Vector3 dashOffset = cubeOffsetActive ? cubeOffset : Vector3.zero;
        if (suspendInMidair && hoverBob)
        {
            pixelTransform.position = basePosition + dashOffset +
                Vector3.up * (Mathf.Sin(time * hoverSpeed * Mathf.PI * 2f) * hoverAmplitude);
        }
        else if (cubeOffsetActive || cubeOffsetWasActive)
        {
            pixelTransform.position = basePosition + dashOffset;
        }
        cubeOffsetWasActive = cubeOffsetActive;

        if (idleSpin != Vector3.zero && allowRotation && !HoldSpin)
            pixelTransform.Rotate(idleSpin * AnimDelta, Space.Self);

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

        float clickSwell = 1f;
        if (clickPulseTimer > 0f)
        {
            float length = clickPulseSeconds > 0f ? clickPulseSeconds : 0.12f;
            clickPulseTimer -= AnimDelta;
            clickSwell = 1f + (clickPulseAmount > 0f ? clickPulseAmount : 0.03f) * Mathf.Sin(Mathf.Clamp01(1f - clickPulseTimer / length) * Mathf.PI);
        }

        Vector3 liveBase = baseScale;
        if (activeLook != null && activeLook.wobble && allowPulsing)
            liveBase = Vector3.Scale(baseScale, PixelWobble.Scale(time, activeLook.wobbleAmount, activeLook.wobbleSpeed, 0f));
        cubeShrinkCurrent = Mathf.MoveTowards(cubeShrinkCurrent, cubeShrinkTarget, Time.unscaledDeltaTime / Mathf.Max(0.05f, cubeShrinkSeconds));
        float hintPulse = ClickHintPulse(time);
        digShrinkCurrent = Mathf.MoveTowards(digShrinkCurrent, digShrinkTarget, Time.unscaledDeltaTime * 3f);
        pixelTransform.localScale = liveBase * (materializeFactor * punch * clickSwell * (1f + pulse * pulseAmount) * (1f + hintPulse * clickHintAmount) * cubeShrinkCurrent * digShrinkCurrent);

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

        if (hintPulse > 0f && clickHintBrightness > 0f)
        {
            Color bright = currentColor * (1f + hintPulse * clickHintBrightness);
            bright.a = currentColor.a;
            ApplyPixelColor(bright, false);
        }

        if (glowLight != null && glowLight.enabled)
        {
            float breath = GlowBreath(activeGlowType, time);
            glowLight.intensity = glowLightIntensity * activeGlow * breath;
        }

        AnimateGlowShell(time);
    }

    private MeshRenderer liveShellRenderer, liveHaloRenderer;
    private Color liveShellColor;
    private float liveShellAlpha, liveShellHdr;
    private MaterialPropertyBlock shellBlock; // created on first use (native objects can't be made in a field initializer)

    /// <summary>Makes the glow shell and halo breathe like the real emission does (same speed and amount, a little stronger so it reads).</summary>
    private void AnimateGlowShell(float time)
    {
        if (liveShellRenderer == null && liveHaloRenderer == null) return;
        if (shellBlock == null) shellBlock = new MaterialPropertyBlock();
        float breath = Mathf.Max(0.3f, GlowBreath(activeGlowType, time, 1.5f));

        if (liveShellRenderer != null)
        {
            liveShellRenderer.GetPropertyBlock(shellBlock); // keeps what is already on it (textures)
            shellBlock.SetColor("_Color", new Color(liveShellColor.r * liveShellHdr * breath, liveShellColor.g * liveShellHdr * breath,
                                                    liveShellColor.b * liveShellHdr * breath, Mathf.Clamp01(liveShellAlpha * Mathf.Lerp(1f, breath, 0.5f))));
            liveShellRenderer.SetPropertyBlock(shellBlock);
        }

        if (liveHaloRenderer != null)
        {
            liveHaloRenderer.GetPropertyBlock(shellBlock);
            shellBlock.SetColor("_Color", new Color(liveShellColor.r * liveShellHdr * 0.7f * breath, liveShellColor.g * liveShellHdr * 0.7f * breath,
                                                    liveShellColor.b * liveShellHdr * 0.7f * breath, Mathf.Clamp01(liveShellAlpha * 0.3f * breath)));
            liveHaloRenderer.SetPropertyBlock(shellBlock);
        }
    }


    private Vector3 cubeOffset;
    private bool cubeOffsetActive, cubeOffsetWasActive;

    /// <summary>Moves the clickable cube by a world offset from its home spot (Cube Dash); null puts it back.</summary>
    public void SetCubeOffset(Vector3? offset)
    {
        cubeOffsetActive = offset.HasValue;
        cubeOffset = offset ?? Vector3.zero;
    }

    /// <summary>The cube's home position (without any minigame offset or bobbing).</summary>
    public Vector3 CubeBasePosition => basePosition;

    /// <summary>The tier index of the pixel on the cube right now.</summary>
    public int CurrentClickTierIndex => GetClickTierIndex();

    /// <summary>The pixel type on the cube right now.</summary>
    public PixelType LiveType => tiers != null && tiers.Length > 0 ? tiers[GetClickTierIndex()].type : PixelType.White;

    /// <summary>Hits already put into the pixel on the cube.</summary>
    public int LiveHits => hitsOnCurrentPixel;

    /// <summary>A right click on a message in a bottle: you let it drift away (no payout, no map) and the next pixel comes.</summary>
    private void DismissBottle()
    {
        PixelAudio.Play("fish_splash");
        PixelHints.Announce("You let the bottle drift away");
        hitsOnCurrentPixel = 0;
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>The whale tore itself off the hook (the fight ran out of time): no payout, the next pixel comes.</summary>
    public void WhaleGotAway()
    {
        PixelHints.Announce("The whale broke the line and swam away!");
        hitsOnCurrentPixel = 0;
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>
    /// Buried treasure: 'count' good fishing pixels (bait most often, then seaweed, shells, fish and pearls - whichever are unlocked) are paid at once
    /// and the same number of old pixels fountain out of the hole for show. Returns how many pixels it was.
    /// </summary>
    public int GiveFishingHaul(Vector3 origin, int count)
    {
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
        System.Collections.Generic.List<float> weights = new System.Collections.Generic.List<float>();
        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            if (!t.unlocked || !IsFishBlock(t.type) || t.type == PixelType.Driftwood || t.type == PixelType.Bottle || t.type == PixelType.Whale) continue;
            pool.Add(i);
            weights.Add(t.type == PixelType.Bait ? 3f : t.type == PixelType.Pearl ? 0.8f : t.type == PixelType.Fish ? 1.2f : 2f);
        }
        if (pool.Count == 0 || count <= 0) return 0;
        float total = 0f;
        foreach (float w in weights) total += w;
        int[] picks = new int[count];
        double[] paid = new double[tiers.Length];
        for (int n = 0; n < count; n++)
        {
            float r = UnityEngine.Random.value * total;
            int k = 0;
            while (k < pool.Count - 1 && r > weights[k]) { r -= weights[k]; k++; }
            picks[n] = pool[k];
            paid[pool[k]] += RollOldPixelAmount(pool[k]) * 1.5d;
        }
        for (int t = 0; t < paid.Length; t++) if (paid[t] > 0d) AddCurrency(t, paid[t]);
        StartCoroutine(TreasureFountain(picks, origin));
        return count;
    }

    private float cubeShrinkCurrent = 1f, cubeShrinkTarget = 1f, cubeShrinkSeconds = 0.5f;
    private float digShrinkCurrent = 1f, digShrinkTarget = 1f;   // a Dirt block gets smaller with every dig, down to old-pixel size

    /// <summary>
    /// Smoothly shrinks the clickable cube to 'scale' times its size (1 = normal) over 'seconds'. Minigames that need the screen
    /// (the Sorting Race) use it so the cube doesn't cover their pieces, then call it again with 1 to bring it back.
    /// </summary>
    /// <summary>True while an event has shrunk the cube away (a takeover minigame): the auto clicker pauses.</summary>
    public bool CubeHidden => cubeShrinkTarget < 0.5f;

    public void SetCubeShrink(float scale, float seconds = 0.5f)
    {
        cubeShrinkTarget = Mathf.Clamp(scale, 0.02f, 1f);
        cubeShrinkSeconds = seconds;
    }

    private PixelLook activeLook;          // the look of the tier the cube currently shows
    private GameObject liveExtras;         // outline / dark-matter core on the live cube
    private PixelType liveExtrasType;
    private bool liveExtrasBuilt;

    private Mesh baseCubeMesh;

    /// <summary>The plain cube mesh of the pixel (captured before any look swaps it for a jagged one).</summary>
    private Mesh BaseMesh()
    {
        if (baseCubeMesh == null && pixelRenderer != null)
        {
            MeshFilter mf = pixelRenderer.GetComponent<MeshFilter>();
            if (mf != null) baseCubeMesh = mf.sharedMesh;
        }
        return baseCubeMesh;
    }

    /// <summary>The mesh a look draws: the chipped rock cube for jagged looks, else the plain cube.</summary>
    private Mesh MeshFor(PixelLook look)
    {
        Mesh basic = BaseMesh();
        if (look != null && basic != null)
        {
            if (look.vacuumContainer) return PixelVacuumMesh.Get(basic);
            if (look.blob) return PixelGelFx.BlobMesh(basic);
            if (look.jagged) return PixelRockMesh.Get(basic, (int)look.type);
        }
        return basic;
    }

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
        bool onlyBasic = look.blockSurface == PixelLook.BlockSurface.None && !look.streakTexture && !look.damageCracks && !look.chromeTexture && !look.oreTexture && !look.chestTexture && !look.meteorTexture && !look.sunTexture;
        Texture2D tex = look.blockSurface == PixelLook.BlockSurface.Seed ? PixelBlockTextures.SeedShell(look.damageCracks ? level : 0, max)
                      : look.blockSurface == PixelLook.BlockSurface.Pest ? PixelBlockTextures.PestHide(look.damageCracks ? level : 0, max)
                      : look.blockSurface == PixelLook.BlockSurface.Bottle ? PixelBlockTextures.BottleGlass(look.damageCracks ? level : 0, max)
                      : look.blockSurface != PixelLook.BlockSurface.None ? PixelBlockTextures.Get(look.blockSurface)
                      : onlyBasic ? PixelLooks.BasicTexture(look.basicSurface)
                      : look.meteorTexture ? PixelStarTextures.Meteor(look.damageCracks ? level : 0, max)
                      : look.sunTexture ? PixelStarTextures.Sun()
                      : look.chestTexture ? PixelLooks.ChestTexture(max > 0 ? Mathf.RoundToInt(Mathf.Clamp01(level / (float)max) * 8f) : 0)
                      : look.oreTexture ? PixelLooks.OreTexture(look.oreColor, look.damageCracks ? level : 0, max)
                      : look.chromeTexture ? PixelLooks.ChromeTexture()
                      : PixelLooks.SurfaceTexture(look.streakTexture, look.damageCracks ? level : 0, max, look.shellTexture);
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
    }

    /// <summary>A hit on a tough pixel: cracks spread over the live cube (level 0 = undamaged, max = about to break).</summary>
    private void SetLiveDamage(PixelTier tier, int level)
    {
        if (pixelRenderer == null || activeLook == null || !activeLook.damageCracks) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        ApplyLookTexture(propertyBlock, activeLook, level, NeededClicks(tier));
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
    /// <summary>A text report about how glow is set up (materials, shaders, keywords, shell) for the rendering report file.</summary>
    public string DebugGlowReport()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("glowShell=" + glowShell + " (inEditor=" + glowShellInEditor + ")  glowShellAlpha=" + glowShellAlpha + "  useLooks=" + useLooks + "  looks=" + (looks != null ? looks.Length : 0));
        Material dm = defaultMaterial;
        sb.AppendLine("defaultMaterial=" + (dm != null ? dm.name + "  shader=" + (dm.shader != null ? dm.shader.name : "null") +
                      "  emissionKeyword=" + dm.IsKeywordEnabled("_EMISSION") + "  hasEmissionColor=" + dm.HasProperty("_EmissionColor") : "null"));
        if (pixelRenderer != null)
        {
            Material cm = pixelRenderer.sharedMaterial;
            sb.AppendLine("live cube material=" + (cm != null ? cm.name + "  shader=" + (cm.shader != null ? cm.shader.name : "null") +
                          "  emissionKeyword=" + cm.IsKeywordEnabled("_EMISSION") + "  supported=" + (cm.shader != null && cm.shader.isSupported) : "null"));
            sb.AppendLine("live glow shell object=" + (liveGlowShell != null ? "yes (active=" + liveGlowShell.activeInHierarchy + ")" : "no") +
                          "  clicking type=" + (tiers != null && currentTierIndex >= 0 && currentTierIndex < tiers.Length ? tiers[currentTierIndex].type.ToString() : "?"));
        }
        if (tiers != null)
            foreach (PixelTier t in tiers)
            {
                PixelLook l = LookOf(t);
                sb.AppendLine("tier " + t.type + ": unlocked=" + t.unlocked + " glow=" + t.glow + " glowIntensity=" + t.glowIntensity +
                              " translucent=" + t.translucent + " lookEmission=" + (l != null ? l.emission.ToString() : "-") +
                              " wantsShell=" + WantsGlowShell(t, l));
            }
        return sb.ToString();
    }

    private GameObject liveGlowShell, liveGlowHalo;
    private PixelType liveGlowShellType = (PixelType)(-1);

    /// <summary>Does this tier get the build-proof glow shell (glowing or emissive, and not see-through)?</summary>
    private bool WantsGlowShell(PixelTier tier, PixelLook look) => glowShell && glowShellAlpha > 0f && IsGlowingSolid(tier, look);

    /// <summary>Does this tier get the soft glow halo (glowing or emissive, not see-through, and the halo is not hidden)?</summary>
    private bool WantsGlowHalo(PixelTier tier, PixelLook look) => glowHalo && IsGlowingSolid(tier, look);

    private static bool IsGlowingSolid(PixelTier tier, PixelLook look)
    {
        if (tier == null) return false;
        bool glowing = tier.glow || (look != null && look.emission > 0f);
        bool seeThrough = tier.translucent || (look != null && look.forceTranslucent);
        return glowing && !seeThrough;
    }

    private float GlowShellAlpha(PixelTier tier, float extra = 1f) =>
        Mathf.Clamp((glowShellAlpha + 0.1f * tier.glowIntensity) * extra, 0.25f, 0.92f);

    /// <summary>How far above 1.0 the shell's colour goes (so Bloom lights it up like emission): grows with the pixel's glow intensity.</summary>
    private float GlowShellHdr(PixelTier tier) => Mathf.Max(1f, 1f + tier.glowIntensity * Mathf.Max(0f, 0.35f + glowShellBrightnessExtra));

    private Color GlowShellColor(PixelTier tier) => Color.Lerp(tier.color, Color.white, 0.35f);

    private void UpdateGlowShell(PixelTier tier, PixelLook look)
    {
        MeshFilter mf = pixelRenderer != null ? pixelRenderer.GetComponent<MeshFilter>() : null;
        bool shell = mf != null && WantsGlowShell(tier, look);
        bool halo = pixelRenderer != null && mf != null && WantsGlowHalo(tier, look);
        if (!shell && !halo)
        {
            if (liveGlowShell != null) Destroy(liveGlowShell);
            if (liveGlowHalo != null) Destroy(liveGlowHalo);
            liveGlowShell = liveGlowHalo = null;
            liveShellRenderer = liveHaloRenderer = null;
            return;
        }
        if (liveGlowShellType == tier.type && (liveGlowShell != null) == shell && (liveGlowHalo != null) == halo) return;

        if (liveGlowShell != null) Destroy(liveGlowShell);
        if (liveGlowHalo != null) Destroy(liveGlowHalo);
        liveShellColor = GlowShellColor(tier);
        liveShellAlpha = GlowShellAlpha(tier);
        liveShellHdr = GlowShellHdr(tier);
        liveGlowShell = shell ? PixelLooks.AddGlowShell(pixelRenderer.transform, mf.sharedMesh, liveShellColor, liveShellAlpha, hdr: liveShellHdr) : null;
        liveGlowHalo = halo ? PixelLooks.AddGlowHalo(pixelRenderer.transform, liveShellColor, liveShellAlpha, liveShellHdr) : null;
        liveGlowShellType = tier.type;
        liveShellRenderer = liveGlowShell != null ? liveGlowShell.GetComponent<MeshRenderer>() : null;
        liveHaloRenderer = liveGlowHalo != null ? liveGlowHalo.GetComponent<MeshRenderer>() : null;
    }

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
            float breath = GlowBreath(activeGlowType, Time.time);
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
        activeGlowType = tier.type;
        activeLook = LookOf(tier);
        if (pixelRenderer != null)
        {
            MeshFilter liveMf = pixelRenderer.GetComponent<MeshFilter>();
            Mesh wantedMesh = MeshFor(activeLook);
            if (liveMf != null && wantedMesh != null && liveMf.sharedMesh != wantedMesh) liveMf.sharedMesh = wantedMesh;   // rocky pixels are drawn jagged
        }
        UpdateGlowLight(tier);
        UpdateLiveExtras(tier, activeLook);
        UpdateGlowShell(tier, activeLook);
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
            ApplyLookTexture(propertyBlock, activeLook, 0, NeededClicks(tier));
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

    public static Material BuildTransparentMaterial(Material source)
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

    [Header("Treasure pixel")]
    [Tooltip("Turn off the Treasure chest's sparkles and sounds (it still works).")]
    [SerializeField] private bool disableTreasureFx = false;

    [Min(0f)]
    [Tooltip("How much the reward grows per click the chest took: payout = clicks x (1 + clicks x this). 0 = the coded default (0.15), so a 20-click chest pays about 80x a 1-click one.")]
    [SerializeField] private float treasureRewardGrowth = 0f;

    [Min(0)]
    [Tooltip("Pixels that pour out of an opened chest = this + the clicks it took (at most 'Treasure Max Pixels'). 0 = the coded default (2).")]
    [SerializeField] private int treasureBasePixels = 0;

    [Min(0)]
    [Tooltip("Most pixels one chest can pour out. 0 = the coded default (24).")]
    [SerializeField] private int treasureMaxPixels = 0;

    private int liveNeed;           // clicks the pixel on the cube needs (only for random-click pixels, i.e. the Treasure chest)

    /// <summary>Reward multiplier of a chest that took 'need' clicks (grows faster than the clicks do).</summary>
    [Header("Electrician")]
    [Tooltip("The Capacitor pixel stores charge from every other click, up to this many (0 = 40) and pays 1 + charge x 'Capacitor Pay Per Charge' when collected.")]
    [SerializeField] private int capacitorChargeCap = 0;
    [Tooltip("Extra payout per stored charge for the Capacitor pixel (0 = 0.15).")]
    [SerializeField] private float capacitorPayPerCharge = 0f;
    private static int capacitorCharge;
    [Tooltip("The Transistor pixel makes the NEXT pixel you collect pay this many times as much (0 = 2).")]
    [SerializeField] private float transistorMultiplier = 0f;
    [Tooltip("Seconds a Fuse pixel can be collected for its bonus before it burns out (0 = 3).")]
    [SerializeField] private float fuseSeconds = 0f;
    [Tooltip("Payout multiplier of a Fuse collected in time (0 = 3). Too late it pays 0.3.")]
    [SerializeField] private float fuseBonus = 0f;
    [Tooltip("Extra payout of a Bulb per old conductor pixel (Electric, Wire, Battery, Resistor) lying around, up to 30 of them (0 = 0.1).")]
    [SerializeField] private float bulbPerConductor = 0f;
    private static double transistorBoost = 1d;
    private float liveSince;

    /// <summary>The old pixels on the floor that carry current (Electric, Wire, Battery, Resistor), at most 30.</summary>
    public int CountConductors()
    {
        int n = 0;
        foreach (Rigidbody body in oldPixels)
        {
            if (body == null) continue;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex) && IsConductorType(Tiers[info.tierIndex].type) && ++n >= 30) break;
        }
        return n;
    }

    /// <summary>Electric, Wire, Battery and Resistor pixels carry the Electrician's current.</summary>
    /// <summary>The Electrician pack's own pixels (Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb).</summary>
    public static bool IsElectricalBlock(PixelType t) => PixelBlockTags.Has(t, PixelTag.Electrical);   // (the tag table in PixelBlockTags.cs)

    public static bool IsConductorType(PixelType t) =>
        t == PixelType.Electric || t == PixelType.Wire || t == PixelType.Battery || t == PixelType.Resistor;

    /// <summary>How much charge the Capacitor pixel holds right now (every non-Capacitor click adds one).</summary>
    public static int CapacitorCharge => capacitorCharge;

    private float TreasureMultiplier(int need) => need * (1f + need * (treasureRewardGrowth > 0f ? treasureRewardGrowth : 0.15f));

    /// <summary>The chest cracked open: gold bursts out and a pile of other pixel types pours out (paid at once, the pixels that fly out are for show).</summary>
    private void OpenTreasure(int chestTier, int need)
    {
        Vector3 origin = pixelTransform.position;
        if (!disableTreasureFx) TreasureFx.Open(origin, PixelBaseSize, need);

        // Which pixel types can come out: any unlocked one except the chest itself, rare drops, fly-aways and wells.
        System.Collections.Generic.List<int> pool = new System.Collections.Generic.List<int>();
        for (int i = 0; i < tiers.Length; i++)
        {
            PixelTier t = tiers[i];
            if (i == chestTier || !t.unlocked || t.rareDrop || t.flyAway || t.type == PixelType.Treasure || t.type == PixelType.Singularity) continue;
            pool.Add(i);
        }
        int count = Mathf.Clamp((treasureBasePixels > 0 ? treasureBasePixels : 2) + need, 3, treasureMaxPixels > 0 ? treasureMaxPixels : 24);
        PixelStats.Count("treasure.opened");
        PixelStats.Best("treasure.most_clicks", need);
        if (pool.Count == 0) return;

        // Pay everything now; the old pixels that pour out carry nothing (so collecting them later doesn't pay twice).
        float bonus = 1f + need / 10f;
        int[] picks = new int[count];
        double[] paid = new double[tiers.Length];
        for (int i = 0; i < count; i++)
        {
            int t = pool[UnityEngine.Random.Range(0, pool.Count)];
            picks[i] = t;
            paid[t] += RollOldPixelAmount(t) * bonus;
        }
        for (int t = 0; t < paid.Length; t++) if (paid[t] > 0d) AddCurrency(t, paid[t]);
        PixelHints.Announce(need >= 15 ? "JACKPOT! The chest held " + count + " pixels" : "The treasure chest held " + count + " pixels");
        StartCoroutine(TreasureFountain(picks, origin));
    }

    private IEnumerator TreasureFountain(int[] picks, Vector3 origin)
    {
        float size = PixelBaseSize;
        for (int i = 0; i < picks.Length; i++)
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f;
            float speed = UnityEngine.Random.Range(1.2f, 3.2f) * size;
            Vector3 vel = new Vector3(Mathf.Cos(a) * speed, UnityEngine.Random.Range(3.5f, 6.5f) * size, Mathf.Sin(a) * speed);
            SpawnStoredPixel(picks[i], 0d, origin + Vector3.up * size * 0.3f, vel);
            yield return new WaitForSeconds(0.035f);
        }
    }

    private static int RollNeededClicks(PixelTier tier)
    {
        int lo = Mathf.Max(1, Mathf.Min(tier.randomClicksMin, tier.randomClicksMax));
        int hi = Mathf.Max(lo, Mathf.Max(tier.randomClicksMin, tier.randomClicksMax));
        return UnityEngine.Random.Range(lo, hi + 1);
    }

    /// <summary>How many clicks the pixel on the cube needs: the tier's own number, or this chest's random one.</summary>
    private int NeededClicks(PixelTier tier)
    {
        if (!tier.randomClicks) return tier.clicksToCollect;
        if (liveNeed < 1) liveNeed = RollNeededClicks(tier);
        return liveNeed;
    }

    private bool liveRich;          // the ore on the cube is a rich vein
    private GameObject richGlitter;

    // ------------------------------------------------------------------
    // Hazard blocks (Pest, Gas Pocket)
    // ------------------------------------------------------------------

    /// <summary>When true the auto clicker clicks hazard blocks too and clears them safely (a future upgrade). Off: it leaves them alone.</summary>
    public static bool HazardAutoClear = false;

    [Min(0f)]
    [Tooltip("How long a hazard block (Pest, Gas Pocket) stays on the cube before it goes away by itself, in seconds. 0 = the coded default (7).")]
    [SerializeField] private float hazardSeconds = 0f;

    [Tooltip("Turns off the Pest's bug swarm (a Pest that goes off just kills every sprout at once, as before).")]
    [SerializeField] private bool disablePestSwarm = false;

    [Min(0)]
    [Tooltip("Bugs the Pest sends at every sprout in your field. 0 = the coded default (6).")]
    [SerializeField] private int pestBugsPerSprout = 0;

    [Min(0)]
    [Tooltip("Bugs a Pest releases even when there is nothing to eat. 0 = the coded default (8).")]
    [SerializeField] private int pestBugsMin = 0;

    [Min(0f)]
    [Tooltip("How fast the pest bugs run, in old-pixel widths per second. 0 = the coded default (3.5).")]
    [SerializeField] private float pestBugSpeed = 0f;

    [Min(0f)]
    [Tooltip("Seconds one bug needs to gnaw through a sprout (more bugs on it are faster). 0 = the coded default (4).")]
    [SerializeField] private float pestChewSeconds = 0f;

    public int PestBugsPerSprout => pestBugsPerSprout > 0 ? pestBugsPerSprout : 6;
    public int PestBugsMin => pestBugsMin > 0 ? pestBugsMin : 8;
    public float PestBugSpeed => pestBugSpeed > 0f ? pestBugSpeed : 3.5f;
    public float PestChewSeconds => pestChewSeconds > 0f ? pestChewSeconds : 4f;

    [Header("Termite Hive (Woodcutting's hazard)")]
    [Tooltip("Turns off the termites: a Termite Hive that goes off then only costs the usual hazard XP penalty.")]
    [SerializeField] private bool disableTermites = false;
    [Min(0f)]
    [Tooltip("How fast a termite runs, in cube widths per second. 0 = the coded default (3.8).")]
    [SerializeField] private float termiteSpeed = 0f;
    [Min(0f)]
    [Tooltip("Seconds a termite needs to eat an old pixel; the bar over the pixel counts this down. Click the pixel before it ends to save it. 0 = the coded default (5).")]
    [SerializeField] private float termiteEatSeconds = 0f;
    [Min(0)]
    [Tooltip("The most termites (= old pixels attacked) one hive releases. 0 = the coded default (60).")]
    [SerializeField] private int termiteMax = 0;

    [Min(0)]
    [Tooltip("Right clicks that clear a Termite Hive (left clicks needed to break it are the pack's Clicks To Collect, 4). 0 = the coded default (10).")]
    [SerializeField] private int termiteRightClicks = 0;

    [Min(0)]
    [Tooltip("What clearing a hazard with RIGHT clicks pays, in that hazard's own currency (it also drops as an old pixel). Breaking one with left clicks pays nothing. 0 = the coded default (1).")]
    [SerializeField] private int hazardClearReward = 0;
    public float TermiteSpeed => termiteSpeed > 0f ? termiteSpeed : 3.8f;
    public float TermiteEatSeconds => termiteEatSeconds > 0f ? termiteEatSeconds : 5f;
    public int TermiteMax => termiteMax > 0 ? termiteMax : 60;

    [Min(0f)]
    [Tooltip("Gas Pocket blast: old pixels within this many old-pixel widths of the cube are blown away (no payout). 0 = the coded default (4).")]
    [SerializeField] private float gasBlastWidths = 0f;

    [Header("Fishing")]
    [Tooltip("Turn off the bait pellets a Bait block drops (and their effect on fish).")]
    [SerializeField] private bool disableBait = false;

    [Min(0)]
    [Tooltip("Pellets one Bait block drops. 0 = the default (6).")]
    [SerializeField] private int baitPellets = 0;

    [Min(0f)]
    [Tooltip("How much each pellet lying around adds to a Fish block's spawn weight (0.35 = +35% each). 0 = the default (0.35).")]
    [SerializeField] private float baitPerPellet = 0f;

    [Min(0f)]
    [Tooltip("Seconds a pellet lies there before it rots away. 0 = the default (60).")]
    [SerializeField] private float baitLifetime = 0f;

    [Min(0)]
    [Tooltip("Most pellets that can lie around at once (the oldest go first). 0 = the default (24).")]
    [SerializeField] private int baitMaxPellets = 0;

    [Min(0f)]
    [Tooltip("How many times wider than a normal hazard blast a going-off Jellyfish stings (and it also zaps every bait pellet). 0 = the default (1.5).")]
    [SerializeField] private float jellyfishBlastFactor = 0f;

    public bool DisableBait => disableBait;
    public int BaitPellets => baitPellets > 0 ? baitPellets : 6;
    public float BaitPerPellet => baitPerPellet > 0f ? baitPerPellet : 0.35f;
    public float BaitLifetime => baitLifetime > 0f ? baitLifetime : 60f;
    public int BaitMaxPellets => baitMaxPellets > 0 ? baitMaxPellets : 24;

    private bool fishingBusy, fishingPass;
    private int fishNeed, fishReel;   // reeling a fish in the long way (the Weak Spots minigame is switched off)

    /// <summary>A hazard block that sat on the cube too long leaves by itself (the cube rolls a new pixel).</summary>
    [Tooltip("Seconds a hazard is on the cube before a charge against it (e.g. a Pesticide charge against the Termite Hive) clears it automatically, so you see it happen.")]
    [SerializeField] private float chargeClearDelay = 0.35f;

    /// <summary>A charge against the hazard on the cube (PixelBlockRules: Pesticide charges vs the Termite Hive) clears it automatically, using one charge.</summary>
    private void UpdateHazardCharges()
    {
        if (pixelTransform == null || isSpawning || tiers == null || tiers.Length == 0 || CubeHidden) return;
        PixelTier t = GetClickTier();
        if (!IsHazard(t.type) || Time.time - liveSince < Mathf.Max(0f, chargeClearDelay)) return;
        if (!PixelBlockRules.UseCharge(t.type)) return;
        PixelStats.Count("charges.used." + t.type);
        ClearHazardNow(t);
    }

    private void UpdateHazardExpiry()
    {
        if (pixelTransform == null || isSpawning || tiers == null || tiers.Length == 0) return;
        PixelTier t = GetClickTier();
        if (!IsHazard(t.type) || Time.time - liveSince < (hazardSeconds > 0f ? hazardSeconds : 7f)) return;
        hitsOnCurrentPixel = 0;
        if (!disableClickPops) PixelBasicFx.Play(t.type, t.color, pixelTransform.position, PixelBaseSize, true, 1f, clickPopStrength);
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>Is the block on the cube a hazard right now? 'remaining' = 1 just appeared .. 0 about to leave by itself; 'since' = when it appeared (PixelHazardTell).</summary>
    public bool TryGetLiveHazard(out PixelType type, out float remaining, out float since)
    {
        type = PixelType.White; remaining = 0f; since = liveSince;
        if (pixelTransform == null || tiers == null || tiers.Length == 0 || CubeHidden) return false;
        PixelTier t = GetClickTier();
        if (!IsHazard(t.type)) return false;
        type = t.type;
        remaining = Mathf.Clamp01(1f - (Time.time - liveSince) / (hazardSeconds > 0f ? hazardSeconds : 7f));
        return true;
    }

    private int defuseClicks;

    /// <summary>When the last right click on a hazard / bottle happened (Block Hold ignores a right press that follows it closely: it is part of the same spam).</summary>
    public float LastDefuseTime { get; private set; } = -99f;

    /// <summary>
    /// A right click on a hazard block: it takes as many right clicks as the block takes to break PLUS the hits you had already put into it (5 to break
    /// and 2 hits in = 7 right clicks). Reaching that clears the block safely - no explosion / swarm, no XP penalty, no payout.
    /// </summary>
    private void DefuseHazard()
    {
        PixelTier tier = GetClickTier();
        if (!IsHazard(tier.type) || pixelTransform == null) return;
        int required = tier.type == PixelType.HornetNest ? (termiteRightClicks > 0 ? termiteRightClicks : 10) : NeededClicks(tier) + hitsOnCurrentPixel;   // (the Termite Hive: a flat 10 right clicks)
        LastDefuseTime = Time.unscaledTime;
        defuseClicks++;
        hitPunchTimer = hitPunchDuration;
        PixelAudio.Play("click");
        StartCoroutine(DefusePopup(Mathf.Min(defuseClicks, required) + "/" + required, pixelTransform.position + Vector3.up * PixelBaseSize * 0.7f));
        if (defuseClicks < required) return;
        FinishDefuse(tier, true);
    }

    /// <summary>A held Pesticide (PixelBlockRules): the hazard on the cube is cleared by this one click, safely (no explosion / swarm, no XP penalty, no payout).</summary>
    private void ClearHazardNow(PixelTier tier)
    {
        if (pixelTransform == null) return;
        LastDefuseTime = Time.unscaledTime;
        hitPunchTimer = hitPunchDuration;
        PixelAudio.Play("click");
        FinishDefuse(tier);
    }

    /// <summary>Per-skill hazard counters for the Stats screen ('hazards.triggered.woodcutting'...): the skill whose pack owns the hazard block.</summary>
    private static void CountHazardForSkill(string prefix, PixelTier tier)
    {
        PixelSkills s = PixelSkills.Instance;
        PixelSkills.SkillPack pack = s != null && tier != null ? s.PackOfType(tier.type) : null;
        if (pack != null) PixelStats.Count(prefix + pack.id, 1);
    }

    private void FinishDefuse(PixelTier tier, bool byRightClick = false)
    {
        if (byRightClick)
        {
            // Worked away with right clicks: it is collected as currency and drops as an old pixel (left clicking a hazard to pieces pays nothing).
            int idx = CurrentClickTierIndex;
            double reward = hazardClearReward > 0 ? hazardClearReward : 1;
            if (IsValidTier(idx)) { AddCurrency(idx, reward); if (spawnFallingCopy) SpawnFallingCopy(idx, reward); }
        }
        defuseClicks = 0;
        hitsOnCurrentPixel = 0;
        PixelStats.Count("hazards.defused", 1);
        CountHazardForSkill("hazards.defused.", tier);
        PixelHints.Announce(tier.type == PixelType.Mold ? "You scraped the mold off the cube. What it already spread stays - shake infected pixels clean." : "You cleared the " + tier.displayName.ToLowerInvariant() + " safely.");
        if (!disableClickPops) PixelBasicFx.Play(tier.type, tier.color, pixelTransform.position, PixelBaseSize, false, 1f, clickPopStrength);
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
    }

    /// <summary>A small number that rises off the cube and fades ('3/7').</summary>
    private IEnumerator DefusePopup(string text, Vector3 start)
    {
        GameObject go = new GameObject("Defuse Text");
        go.transform.position = start;
        TextMeshPro tm = go.AddComponent<TextMeshPro>();
        if (uiFont != null) tm.font = uiFont;
        tm.text = text;
        tm.fontSize = 5f * Mathf.Max(0.2f, PixelBaseSize);
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.color = new Color(1f, 0.75f, 0.3f, 1f);
#if UNITY_2023_1_OR_NEWER
        tm.textWrappingMode = TextWrappingModes.NoWrap;
#else
        tm.enableWordWrapping = false;
#endif
        for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
        {
            if (go == null) yield break;
            if (targetCamera != null) go.transform.rotation = targetCamera.transform.rotation;
            go.transform.position = start + Vector3.up * (PixelBaseSize * 0.8f * (t / 0.7f));
            Color c = tm.color; c.a = 1f - t / 0.7f; tm.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>The hazard block was hit too many times: the Pest kills every sprout, the Gas Pocket explodes. Either way the skill earns less XP for a while.</summary>
    private void TriggerHazard(int tierIndex, PixelTier tier)
    {
        hitsOnCurrentPixel = 0;
        PixelBlockRules.OnHazardWentOff(tier.type);   // some hazards (the Hornet Nest) cost the Block Hold meter part of its charge
        Vector3 pos = pixelTransform.position;
        string what;
        if (tier.type == PixelType.Pest)
        {
            int killed = disablePestSwarm ? SeedSprout.KillAll() : PixelPestSwarm.Release(this, pos);
            what = killed > 0 ? (disablePestSwarm ? "The pests ate " : "A swarm of pests is going for ") + killed + " sprout" + (killed == 1 ? "" : "s") + "!" : "The pests swarm out!";
            if (!disableFarmFx) PixelFarmFx.Break(PixelType.Weevil, pos, PixelBaseSize, tier.color);
        }
        else if (tier.type == PixelType.Toadstool)
        {
            what = "The toadstool burst in a cloud of dizzy spores - the cube keeps jerking away from your cursor!";
            if (!disableFarmFx) PixelFarmFx.Break(tier.type, pos, PixelBaseSize, tier.color);
            PixelToadstoolDaze.Begin(this);
        }
        else if (tier.type == PixelType.Wildfire)
        {
            what = "The wildfire spread and burned up the husks around the cube!";
            if (!disableFireFx) FireBurstFx.Play(pos, PixelBaseSize * 1.6f);
            float burnReach = OldPixelWorldSize * (gasBlastWidths > 0f ? gasBlastWidths : 4f) * 1.5f;
            for (int i = oldPixels.Count - 1; i >= 0; i--)
            {
                Rigidbody b = oldPixels[i];
                if (b != null && (b.position - pos).sqrMagnitude <= burnReach * burnReach) ReleaseOldPixel(b, false);
            }
        }
        else if (tier.type == PixelType.Jellyfish)
        {
            what = "The jellyfish stung everything around it!";
            if (!disableFarmFx) PixelFarmFx.Break(tier.type, pos, PixelBaseSize, tier.color);
            float sporeReach = OldPixelWorldSize * (gasBlastWidths > 0f ? gasBlastWidths : 4f);
            if (tier.type == PixelType.Jellyfish)
            {
                sporeReach *= jellyfishBlastFactor > 0f ? jellyfishBlastFactor : 1.5f;
                int pellets = PixelBait.Count;
                PixelBait.DestroyAll(true);   // the shock fries every bait pellet
                if (pellets > 0) what += " The bait pellets were fried!";
                if (!disableFarmFx) FractureFx.Play(pos, PixelBaseSize, 16, 3.4f, 0.55f, new Color(1f, 0.9f, 1f, 1f), new Color(0.95f, 0.4f, 1f, 1f));
                PixelAudio.Play("jelly_sting");
            }
            for (int i = oldPixels.Count - 1; i >= 0; i--)
            {
                Rigidbody b = oldPixels[i];
                if (b != null && (b.position - pos).sqrMagnitude <= sporeReach * sporeReach) ReleaseOldPixel(b, false);
            }
        }
        else if (tier.type == PixelType.Mold)
        {
            what = "The mold burst and spread far over the fallen pixels - shake them clean!";
            PixelMold.Spread(pos, 2f);
            if (!disableFarmFx) PixelFarmFx.Break(PixelType.Mold, pos, PixelBaseSize, tier.color);
        }
        else if (tier.type == PixelType.HornetNest)   // (the Termite Hive: the enum value kept its old name so saves stay valid)
        {
            int targeted = disableTermites ? 0 : PixelTermiteSwarm.Release(this, pos);
            what = targeted > 0 ? "Termites poured out of the hive and are going for " + targeted + " old pixel" + (targeted == 1 ? "" : "s") + " - click them to save them!" : "The termites poured out but found nothing to eat.";
            if (!disableFarmFx) PixelFarmFx.Break(PixelType.HornetNest, pos, PixelBaseSize, tier.color);
        }
        else
        {
            what = "The gas pocket exploded!";
            if (!disableFireFx) FireBurstFx.Play(pos, PixelBaseSize);
            float reach = OldPixelWorldSize * (gasBlastWidths > 0f ? gasBlastWidths : 4f);
            for (int i = oldPixels.Count - 1; i >= 0; i--)
            {
                Rigidbody b = oldPixels[i];
                if (b != null && (b.position - pos).sqrMagnitude <= reach * reach) ReleaseOldPixel(b, false);
            }
        }
        PixelStats.Count("hazards.triggered", 1);
        CountHazardForSkill("hazards.triggered.", tier);
        string penalty = PixelSkills.ApplyHazardPenalty();
        PixelHints.Announce(what, what + " " + penalty);
        PlayClickEffects(tier);
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());
        onPixelClicked?.Invoke();
    }

    private void Materialize(PixelTier tier)
    {
        using (materializeMarker.Auto()) MaterializeInner(tier);
        PixelMaterialized?.Invoke(System.Array.IndexOf(tiers, tier));
    }

    private void MaterializeInner(PixelTier tier)
    {
        if (IsHazard(tier.type)) PixelHints.Trigger("hazard");
        hitsOnCurrentPixel = 0; // a fresh pixel has taken no hits yet
        fishNeed = fishReel = 0;
        digShrinkTarget = digShrinkCurrent = 1f;
        defuseClicks = 0;
        liveSince = Time.time;
        liveNeed = tier.randomClicks ? RollNeededClicks(tier) : 0;   // a treasure chest needs a random number of clicks

        // A fresh ore pixel is sometimes a rich vein: it glitters and pays much more when it breaks.
        if (richGlitter != null) { Destroy(richGlitter); richGlitter = null; }
        liveRich = IsOre(tier.type) && !disableProspectorFx && UnityEngine.Random.value < (richVeinChance > 0f ? richVeinChance : 0.08f);
        if (liveRich && pixelTransform != null) richGlitter = OreFx.Glitter(pixelTransform, PixelBaseSize);
        if (materializeRoutine != null) StopCoroutine(materializeRoutine);
        materializeRoutine = StartCoroutine(MaterializeRoutine(tier));
        if (pendingHits > 0)
        {
            // A block that comes back from the hold keeps its damage (and its cracks).
            hitsOnCurrentPixel = Mathf.Clamp(pendingHits, 0, Mathf.Max(0, NeededClicks(tier) - 1));
            if (hitsOnCurrentPixel > 0) SetLiveDamage(tier, hitsOnCurrentPixel);
        }
        pendingHits = 0;
        liveStake = pendingStake;   // a staked block (Block Hold) comes back worth more... or nothing
        pendingStake = 1f;
        if (!Mathf.Approximately(liveStake, 1f)) StakeAppeared?.Invoke(liveStake);
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
    /// Vacuum pixel effect: every old pixel flies into the cube (pixel rules: husk XP; with husksPayCurrency its original reward is added again).
    /// </summary>
    // The tier of each pixel the last vacuum click sucked up, waiting for the old Vacuum pixel that drops in the same click.
    private readonly System.Collections.Generic.List<int> vacuumContents = new System.Collections.Generic.List<int>();

    /// <summary>
    /// Vacuum effect. By default every old pixel swirls into the cube; a 'target' (e.g. a Vacuum pixel spat out of the Pixel Bank hose)
    /// makes them swirl into that instead, and 'exclude' keeps that pixel itself out of the suction.
    /// </summary>
    public void Vacuum(int vacuumTierIndex, Transform target = null, Rigidbody exclude = null)
    {
        double total = 0d;
        int count = 0;
        double[] perTier = new double[tiers.Length];
        int[] huskCounts = new int[tiers.Length];   // pixel rules: what was sucked up, per type, for the XP it gives
        vacuumContents.Clear();

        for (int i = 0; i < oldPixels.Count; i++)
        {
            Rigidbody body = oldPixels[i];
            if (body == null || body == exclude) continue;

            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex))
            {
                if (husksPayCurrency)
                {
                    AddCurrency(info.tierIndex, info.amount);
                    perTier[info.tierIndex] += info.amount;
                    total += info.amount;
                }
                else huskCounts[info.tierIndex]++;
                PixelSkills.OldPixelConsumed(info.tierIndex);
                count++;
                if (!hideVacuumContents) vacuumContents.Add(info.tierIndex);
            }

            StartCoroutine(SuckRoutine(body, target));
        }

        oldPixels.Clear();
        if (exclude != null) oldPixels.Add(exclude); // the pixel that is doing the sucking stays an old pixel

        if (logVacuum && count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("Vacuum: re-collected " + count + " old pixels worth " + total + " (");
            for (int i = 0; i < tiers.Length; i++)
                if (perTier[i] > 0d) sb.Append(tiers[i].displayName).Append(" +").Append(perTier[i]).Append("  ");
            Debug.Log(sb.Append(")").ToString(), this);
        }

        for (int i = 0; i < huskCounts.Length; i++)
            if (huskCounts[i] > 0) GiveHuskXp(i, huskCounts[i]);

        if (count > 0)
        {
            if (total > 0d) VacuumBreakdown?.Invoke(perTier);
            PixelsVacuumed?.Invoke(vacuumTierIndex, total, count);   // total is 0 under the pixel rules (the sound and stats still use the count)
        }
    }

    /// <summary>Pixel rules: true = old pixels pay their value again when consumed (the old behaviour); false = they are husks (material, XP).</summary>
    public bool HusksPayCurrency => husksPayCurrency;

    /// <summary>Pixel rules: the XP 'count' consumed husks of one type give (a share of a manual click's XP; pure husks train Standard, a skill's husks that skill).</summary>
    public void GiveHuskXp(int tierIndex, int count)
    {
        if (husksPayCurrency || count <= 0 || !IsValidTier(tierIndex)) return;
        PixelSkills skills = PixelSkills.Instance;
        if (skills != null) skills.AddHuskXp(tierIndex, count * (huskXpShare > 0f ? huskXpShare : 0.5f));
    }

    /// <summary>Removes every old pixel lying around (no payout). Used by the dev tools.</summary>
    /// <summary>
    /// Dev tools: sets every pixel count to 0 - the inventory amount, the lifetime total shown in the Log and the click count -
    /// but keeps what is unlocked (pixel types, shop packs, Ultra pixels and boosts, earned achievements).
    /// </summary>
    public void ResetAllCounts()
    {
        foreach (PixelTier t in tiers)
        {
            t.count = 0d;
            t.totalCollected = 0d;
            t.timesCollected = 0L;
        }
        NotifyChanged();
    }

    public void ClearOldPixels()
    {
        for (int i = 0; i < oldPixels.Count; i++)
            if (oldPixels[i] != null) DespawnOldPixel(oldPixels[i].gameObject);
        oldPixels.Clear();
    }

    /// <summary>One old pixel as stored in a save file.</summary>
    [System.Serializable]
    public class OldPixelState
    {
        public int tier;
        public double amount;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float remaining = -1f;
    }

    /// <summary>The old pixels lying around, for the save file (not the ones streaking away or already vanishing).</summary>
    public System.Collections.Generic.List<OldPixelState> GetOldPixelStates()
    {
        var list = new System.Collections.Generic.List<OldPixelState>();
        for (int i = 0; i < oldPixels.Count; i++)
        {
            Rigidbody rb = oldPixels[i];
            if (rb == null || IsFlyingPixel(rb) || PixelConveyorSystem.IsRiding(rb)) continue;   // (pixels riding a belt are saved with the belt)
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && d.IsDespawning) continue;
            list.Add(new OldPixelState
            {
                tier = info.tierIndex,
                amount = info.amount,
                position = rb.position,
                rotation = rb.rotation,
#if UNITY_6000_0_OR_NEWER
                velocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity,
#else
                velocity = rb.isKinematic ? Vector3.zero : rb.velocity,
#endif
                angularVelocity = rb.isKinematic ? Vector3.zero : rb.angularVelocity,
                remaining = d != null ? d.Remaining : -1f,
            });
        }
        return list;
    }

    /// <summary>Loading a save: replaces the old pixels lying around with the saved ones, where they were and how fast they moved.</summary>
    public void RestoreOldPixels(System.Collections.Generic.IEnumerable<OldPixelState> states)
    {
        ClearOldPixels();
        if (states == null) return;
        foreach (OldPixelState s in states)
        {
            if (s == null || !IsValidTier(s.tier) || tiers[s.tier].flyAway) continue;
            int before = oldPixels.Count;
            SpawnStoredPixel(s.tier, s.amount, s.position, s.velocity);
            if (oldPixels.Count <= before) continue;
            Rigidbody rb = oldPixels[oldPixels.Count - 1];
            if (rb == null) continue;
            rb.position = s.position;
            rb.rotation = s.rotation;
            rb.transform.SetPositionAndRotation(s.position, s.rotation);
            // No pop-in for loaded pixels: full size and solid straight away (the tiny / ghost phase made them fall through each other).
            OldPixelPopIn pop = rb.GetComponent<OldPixelPopIn>();
            if (pop != null) pop.Finish();
            // Pixels that were lying still stay frozen where they were; ones that were moving carry on.
            bool resting = s.velocity.sqrMagnitude < 0.25f * 0.25f;
            Vector3 vel = resting ? Vector3.zero : s.velocity;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = vel;
#else
            rb.velocity = vel;
#endif
            rb.angularVelocity = resting ? Vector3.zero : s.angularVelocity;
            if (resting) rb.Sleep();
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null) d.SetRemaining(s.remaining);
        }
    }

    /// <summary>The old pixels currently lying around (read-only). Used by the vacuum device.</summary>
    public System.Collections.Generic.IReadOnlyList<Rigidbody> OldPixels => oldPixels;

    /// <summary>Takes an old pixel out of the old-pixel list WITHOUT paying anything: the caller (the Wood Chipper) now owns the object and must destroy it.</summary>
    public bool ClaimOldPixel(Rigidbody body)
    {
        if (body == null || !oldPixels.Remove(body)) return false;
        PixelStats.Count("old.shredded");
        return true;
    }

    /// <summary>
    /// Consumes one old pixel (pixel rules: it gives husk XP; with husksPayCurrency its reward is re-added) and pulls it into 'target' while it shrinks.
    /// Returns false if it isn't an old pixel any more.
    /// </summary>
    public bool AbsorbOldPixel(Rigidbody body, Transform target)
    {
        if (body == null || !oldPixels.Remove(body)) return false;

        PixelStats.Count("old.vacuumed");
        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info != null && IsValidTier(info.tierIndex))
        {
            PixelSkills.OldPixelConsumed(info.tierIndex);
            if (husksPayCurrency)
            {
                AddCurrency(info.tierIndex, info.amount);
                double[] perTier = new double[tiers.Length];
                perTier[info.tierIndex] = info.amount;
                VacuumBreakdown?.Invoke(perTier);
            }
            else GiveHuskXp(info.tierIndex, 1);
        }

        StartCoroutine(SuckRoutine(body, target));
        return true;
    }

    /// <summary>Hides / shows every old pixel on the floor (renderers only; they keep existing). Used while the floor is faded out on the main menu.</summary>
    public void CollectOldPixelTransforms(List<Transform> into)
    {
        foreach (Rigidbody rb in oldPixels) if (rb != null) into.Add(rb.transform);
    }

    public void SetOldPixelsHidden(bool hidden)
    {
        foreach (Rigidbody rb in oldPixels)
        {
            if (rb == null) continue;
            foreach (Renderer r in rb.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = hidden;
        }
    }

    /// <summary>Stops and clears every particle system on an old pixel (its look's ambient effects) - used when something takes the pixel away.</summary>
    private static void QuietParticles(GameObject go)
    {
        if (go == null) return;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// <summary>
    /// Takes an old pixel out of the game's old-pixel list so something else (the black hole) can handle it.
    /// 'credit' re-adds its original reward, but only with husksPayCurrency on (pixel rules). The caller is responsible for removing the object. Returns false if it wasn't listed.
    /// </summary>
    public bool ReleaseOldPixel(Rigidbody body, bool credit)
    {
        if (body == null || !oldPixels.Remove(body)) return false;
        QuietParticles(body.gameObject);   // a pixel being taken away stops emitting (the Vacuum's suction wind, embers, motes...)

        if (credit && husksPayCurrency)   // pixel rules: a husk never pays its value again
        {
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && IsValidTier(info.tierIndex)) AddCurrency(info.tierIndex, info.amount);
        }
        return true;
    }

    /// <summary>Puts a husk that was taken off the list (ReleaseOldPixel) back on it as a normal old pixel (a husk picked back out of a Recycler).</summary>
    public void AdoptOldPixel(Rigidbody body)
    {
        if (body != null && !oldPixels.Contains(body)) oldPixels.Add(body);
    }

    /// <summary>The sound id of a pixel type's own bounce ("pixel_bounce_electric"), or null for an invalid tier. PixelAudio falls back to the plain landing sound if no such sound has clips.</summary>
    private string BounceSoundId(int tierIndex) =>
        IsValidTier(tierIndex) ? "pixel_bounce_" + tiers[tierIndex].type.ToString().ToLowerInvariant() : null;

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
            Shader shader = PixelShaders.SpriteDefault();
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
        QuietParticles(body.gameObject);

        Vector3 startPos = t.position;
        Vector3 endPos = dest != null ? dest.position : startPos;
        Vector3 startScale = t.localScale;
        float time = 0f;

        // The swirl: the pixel's offset from the target turns round the vertical axis while it shrinks towards the centre.
        Vector3 startOffset = startPos - endPos;
        Vector2 startFlat = new Vector2(startOffset.x, startOffset.z);
        float startRadius = startFlat.magnitude;
        float startAngle = Mathf.Atan2(startOffset.z, startOffset.x);
        // Far pixels curve the most, nearby ones barely curve, so a big cleanup is a gentle sweep rather than a washing machine.
        float turns = vacuumSwirlTurns > 0f ? vacuumSwirlTurns : 0.6f;
        turns *= Mathf.Clamp01(startRadius / (PixelBaseSize * 8f) + 0.2f);
        float spinDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f; // some go round the other way for a busier look
        bool swirl = !disableVacuumSwirl && startRadius > 0.05f;

        while (time < vacuumSuckDuration)
        {
            if (t == null) yield break; // destroyed (lifetime ran out) while flying
            time += Time.deltaTime;
            float k = vacuumSuckDuration > 0f ? Mathf.Clamp01(time / vacuumSuckDuration) : 1f;
            float eased = vacuumSuckCurve.Evaluate(k);

            if (dest != null) endPos = dest.position;
            if (swirl)
            {
                float radius = startRadius * (1f - eased);
                float angle = startAngle + spinDirection * turns * Mathf.PI * 2f * eased;
                t.position = new Vector3(endPos.x + Mathf.Cos(angle) * radius,
                                         Mathf.Lerp(startPos.y, endPos.y, eased),
                                         endPos.z + Mathf.Sin(angle) * radius);
                t.Rotate(0f, spinDirection * 240f * Time.deltaTime, 0f, Space.World); // a lazy tumble as it goes round
            }
            else t.position = Vector3.Lerp(startPos, endPos, eased);
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

    /// <summary>Like <see cref="SpawnStoredPixel"/> but the pixel is 'magnetized' (a Sorter's stream: no collisions while it flies, solid once it lands). Returns its body, or null.</summary>
    public Rigidbody SpawnStreamedPixel(int tierIndex, double amount, Vector3 position, Vector3 velocity)
    {
        if (!IsValidTier(tierIndex) || tiers[tierIndex].flyAway || pixelTransform == null) return null;
        SpawnFallingCopy(tierIndex, amount, true, position, velocity, true);
        return oldPixels.Count > 0 ? oldPixels[oldPixels.Count - 1] : null;
    }

    private static readonly ProfilerMarker spawnCopyMarker = new ProfilerMarker("Pixel.SpawnFallingCopy");
    private static readonly ProfilerMarker materializeMarker = new ProfilerMarker("Pixel.Materialize");
    private static MaterialPropertyBlock spawnBlock;
    private Collider[] liveColliders;

    private void SpawnFallingCopy(int tierIndex, double amount, bool stored = false, Vector3 storedPosition = default,
                                  Vector3 storedVelocity = default, bool streamed = false)
    {
        using (spawnCopyMarker.Auto()) SpawnFallingCopyInner(tierIndex, amount, stored, storedPosition, storedVelocity, streamed);
    }

    private void SpawnFallingCopyInner(int tierIndex, double amount, bool stored, Vector3 storedPosition, Vector3 storedVelocity, bool streamed)
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
        Mesh copyMesh = MeshFor(IsValidTier(tierIndex) ? LookOf(tiers[tierIndex]) : null);
        if (srcFilter != null && pixelRenderer != null)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = copyMesh;
            MeshRenderer mr = copy.AddComponent<MeshRenderer>();
            if (spawnBlock == null) spawnBlock = new MaterialPropertyBlock();
            MaterialPropertyBlock block = spawnBlock;   // (SetPropertyBlock copies it, so one block is reused instead of allocating one per click)
            block.Clear();
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
            if (liveColliders == null) liveColliders = pixelTransform.GetComponentsInChildren<Collider>();
            foreach (Collider live in liveColliders)
                if (live != null) Physics.IgnoreCollision(box, live);
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
            // The collider stays as a trigger so a slowed-down meteor can still be picked up (see LandFlyingPixel).
            box.isTrigger = true;
            copy.AddComponent<OldPixelFlight>();
            Camera fc = targetCamera != null ? targetCamera : Camera.main;
            Vector2 d = tiers[tierIndex].flyDirection;
            if (tiers[tierIndex].flyRandomDirection)
            {
                Vector2 range = tiers[tierIndex].flyAngleRange;
                float angle = UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y)) * Mathf.Deg2Rad;
                d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); // always on the upper half of the screen
            }
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
            FlyAwayLaunched?.Invoke(copy.transform.position);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                         ForceMode.VelocityChange);

            PixelLook flyLook = LookOf(tiers[tierIndex]);
            if (flyLook != null && flyLook.cometTail && !disableMeteorFx) PixelCometFx.AttachWake(copy.transform, copy.transform.lossyScale.x);

            if (flyTrailSeconds > 0f)
            {
                TrailRenderer trail = copy.AddComponent<TrailRenderer>();
                trail.time = flyTrailSeconds;
                trail.startWidth = copy.transform.lossyScale.x;
                trail.endWidth = 0f;
                Color tc = tiers[tierIndex].color;
                trail.sharedMaterial = TrailMaterialFor(tierIndex, tc);
                // Smooth: a gently curved taper, an eased fade, rounded ends, and many short segments.
                trail.alignment = LineAlignment.View;
                trail.minVertexDistance = 0.02f;
                trail.numCapVertices = 6;
                trail.numCornerVertices = 4;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -0.6f), new Keyframe(0.35f, 0.62f), new Keyframe(1f, 0f, -1.2f, 0f));
                Gradient fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(Color.Lerp(tc, Color.white, 0.25f), 0f), new GradientColorKey(tc, 0.4f), new GradientColorKey(tc, 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.5f, 0.35f), new GradientAlphaKey(0.18f, 0.7f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = fade;
            }
        }
        else if (gravityScale > 0f)
            copy.AddComponent<ScaledGravity>().scale = gravityScale * (custom ? oldLook.gravityMultiplier : 1f);

        if (!fly) copy.AddComponent<OldPixelImpact>().Setup(landMinSpeed, landFullVolumeSpeed, landCooldown, BounceSoundId(tierIndex));
        if (!fly && IsValidTier(tierIndex) && srcFilter != null)
        {
            // Look extras: neon outline / dark-matter core, and shattering for glass.
            PixelLook styled = LookOf(tiers[tierIndex]);
            if (styled != null)
            {
                PixelLooks.AddExtras(copy.transform, copyMesh, styled, tiers[tierIndex].color, defaultMaterial);
                if (styled.wobble) MakeWobbleVisual(copy, styled);
                if (tiers[tierIndex].vacuum && !stored && vacuumContents.Count > 0)
                {
                    // The old Vacuum pixel carries what it just sucked up, as tiny pixels inside it.
                    int maxShown = vacuumContentsMax > 0 ? vacuumContentsMax : 16;
                    while (vacuumContents.Count > maxShown) vacuumContents.RemoveAt(UnityEngine.Random.Range(0, vacuumContents.Count));
                    copy.AddComponent<PixelVacuumContents>().Fill(this, vacuumContents, vacuumContentSize > 0f ? vacuumContentSize : 0.26f);
                    vacuumContents.Clear();
                }
                if (styled.gravityWell)
                    copy.AddComponent<OldPixelGravityWell>().Setup(this, styled);
                if (styled.floatAway)
                    copy.AddComponent<OldPixelFloat>().Setup(this, styled.floatAfterBounces, styled.floatLift, styled.floatDriftSpeed);
                if (styled.splash)
                    copy.AddComponent<OldPixelSplash>().Setup(this);
                if (styled.sunFx && !IsGodCube(styled.type))   // (Ra shows the sun's corona but does not hover or pay a trickle)
                {
                    copy.AddComponent<OldPixelSolarHover>().Setup(this);
                    copy.AddComponent<OldPixelSolarAura>();
                }
                if (styled.radiation && !disableNuclearFx)
                    copy.AddComponent<OldPixelRadiation>().Setup(this, nuclearRadiusPixels > 0f ? nuclearRadiusPixels : 4f, nuclearAgeRate > 0f ? nuclearAgeRate : 40f);
                if (styled.burn && !disableFireFx)
                    copy.AddComponent<OldPixelBurn>().Setup(this, fireBurnSeconds > 0f ? fireBurnSeconds : 6f);
                if (styled.shatter)
                    copy.AddComponent<OldPixelShatter>().Setup(this, shatterMinSpeed, shardCount, shardSpeed, shardLifeSeconds, shardSize, shatterSoundId);
            }
        }
        if (!fly && lightTrail && IsBrightOldPixel(tierIndex)) AddLightTrail(copy, tiers[tierIndex].color);
        if (srcFilter != null && IsValidTier(tierIndex))
        {
            PixelTier glowTier = tiers[tierIndex];
            PixelLook glowLook = LookOf(glowTier);
            bool bright = IsBrightOldPixel(tierIndex); // bright old pixels (Luminescent) glow much harder, like their boosted emission
            float hdr = GlowShellHdr(glowTier) * (bright ? Mathf.Max(1f, oldPixelGlowBoost * 0.7f) : 1f);
            if (WantsGlowShell(glowTier, glowLook))
                PixelLooks.AddGlowShell(copy.transform, copyMesh, GlowShellColor(glowTier), GlowShellAlpha(glowTier, bright ? 1.3f : 1f), hdr: hdr);
            if (WantsGlowHalo(glowTier, glowLook))
                PixelLooks.AddGlowHalo(copy.transform, GlowShellColor(glowTier), GlowShellAlpha(glowTier, bright ? 1.3f : 1f), hdr, bright ? 2.4f : 1f);
        }

        if (fly)
        {
            AddDespawn(copy, flyLifetime);
            OldPixelInfo flyInfo = copy.AddComponent<OldPixelInfo>();
        flyInfo.bornTime = Time.unscaledTime;
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
            AddDespawn(copy, FallingCopyLifetime * LifetimeFactor(tierIndex));
            OldPixelInfo routedInfo = copy.AddComponent<OldPixelInfo>();
        routedInfo.bornTime = Time.unscaledTime;
            routedInfo.tierIndex = tierIndex;
            routedInfo.amount = amount;
            if (!stored || streamed)
            {
                // A Sorter's stream: no collisions while the pixel is in the air, solid again once it has landed (see OldPixelStream).
                OldPixelStream stream = copy.AddComponent<OldPixelStream>();
                stream.Setup(this, box);
            }
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
        float vertical = Mathf.Min(0f, UnityEngine.Random.Range(popVerticalRange.x, popVerticalRange.y));   // never upward: the copy leaves through a side of the cube, not over its top
        // It appears at the side face it is thrown from (not at the centre, where it could show over the top face).
        Vector3 sidePosition = copy.transform.position + new Vector3(flat.x, 0f, flat.y) * ((popSideOffset > 0f ? popSideOffset : 0.45f) * PixelBaseSize);
        copy.transform.position = sidePosition;
        rb.position = sidePosition;
        Vector3 direction = new Vector3(flat.x, vertical, flat.y).normalized;
        float speed = UnityEngine.Random.Range(popSpeedRange.x, popSpeedRange.y);

        Vector3 launch = direction * speed;
        if (TryBurstVelocity(copy.transform.position, flat, out Vector3 burstVelocity)) launch = burstVelocity;   // a long fall: thrown out in a random direction so it lands round the cube
        rb.AddForce(launch, ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(popSpinRange.x, popSpinRange.y),
                     ForceMode.VelocityChange);

        if (popGrowSeconds > 0f || popNoCollisionSeconds > 0f)
        {
            // Spawns tiny and collision-free so fast clicking doesn't make a pile-up; grows and becomes solid while falling.
            OldPixelPopIn pop = copy.AddComponent<OldPixelPopIn>();
            pop.Setup(box, copy.transform.localScale, popStartScale, popGrowSeconds, popNoCollisionSeconds);
        }

        AddDespawn(copy, FallingCopyLifetime * LifetimeFactor(tierIndex));

        OldPixelInfo info = copy.AddComponent<OldPixelInfo>();
        info.bornTime = Time.unscaledTime;
        info.tierIndex = tierIndex;
        info.amount = amount;
        PixelMold.TryAttachGlow(copy, tiers[tierIndex].type);   // a Berry / Mushroom / Herb / Truffle is sometimes a glowing variant

        oldPixels.Add(rb);
        while (CopyCap > 0 && oldPixels.Count > CopyCap)
        {
            Rigidbody oldest = oldPixels[0];
            oldPixels.RemoveAt(0);
            if (oldest != null) DespawnOldPixel(oldest.gameObject);
        }
    }

    /// <summary>
    /// A display-only model of a pixel type (no physics) drawn with its current look: material, colour, surface texture, glow
    /// and extras (dark-matter core, face circles, wobble...). 'size' = edge length in world units. Used by the Ultra Pad.
    /// </summary>
    public GameObject CreateDisplayPixel(int tierIndex, Transform parent, float size)
    {
        if (!IsValidTier(tierIndex) || pixelRenderer == null) return null;
        MeshFilter srcFilter = pixelRenderer.GetComponent<MeshFilter>();
        if (srcFilter == null) return null;

        PixelTier tier = tiers[tierIndex];
        PixelLook styled = LookOf(tier);
        GameObject go = new GameObject("Pixel Model");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        if (fallingCopyLayer >= 0 && fallingCopyLayer < 32) go.layer = fallingCopyLayer;

        Mesh displayMesh = MeshFor(styled);
        go.AddComponent<MeshFilter>().sharedMesh = displayMesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        Material m = MaterialForTier(tier);
        mr.sharedMaterial = m != null ? m : pixelRenderer.sharedMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Color drawColor = RenderColor(tier, styled);
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor(colorPropertyId, drawColor);
        block.SetColor("_Color", drawColor);
        ApplyLookSurface(block, styled);
        ApplyLookTexture(block, styled, 0, tier.clicksToCollect);
        float glowMul = styled != null ? styled.glowScale : 1f;
        block.SetColor("_EmissionColor", tier.glow
            ? new Color(tier.color.r, tier.color.g, tier.color.b, 1f) * (tier.glowIntensity * glowMul)
            : styled != null && styled.emission > 0f
                ? new Color(drawColor.r, drawColor.g, drawColor.b, 1f) * styled.emission : Color.black);
        mr.SetPropertyBlock(block);

        if (styled != null)
        {
            PixelLooks.AddExtras(go.transform, displayMesh, styled, tier.color, defaultMaterial);
            if (styled.wobble) MakeWobbleVisual(go, styled);
        }
        if (WantsGlowShell(tier, styled)) PixelLooks.AddGlowShell(go.transform, displayMesh, GlowShellColor(tier), GlowShellAlpha(tier), hdr: GlowShellHdr(tier));
        if (WantsGlowHalo(tier, styled)) PixelLooks.AddGlowHalo(go.transform, GlowShellColor(tier), GlowShellAlpha(tier), GlowShellHdr(tier));
        return go;
    }

    /// <summary>True while an old pixel is still streaking away in a straight line (a meteor pixel nobody has caught).</summary>
    public bool IsFlyingPixel(Rigidbody body)
    {
        if (body == null) return false;
        OldPixelFlight flight = body.GetComponent<OldPixelFlight>();
        return flight != null && flight.Flying;
    }

    /// <summary>
    /// A flying meteor pixel that was grabbed (Pixel Grabbing + Time Slow) becomes an ordinary old pixel: solid, affected by
    /// gravity, kept inside the view, and given a normal lifetime.
    /// </summary>
    public void LandFlyingPixel(Rigidbody body)
    {
        if (!IsFlyingPixel(body)) return;
        body.GetComponent<OldPixelFlight>().Flying = false;

        BoxCollider box = body.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = false;
            box.sharedMaterial = SharedOldPixelPhysicsMaterial();
            if (!collideWithLivePixel && pixelTransform != null)
                foreach (Collider live in pixelTransform.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(box, live);
        }

        body.collisionDetectionMode = collisionDetection;
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = fallingCopyDrag;
        body.angularDamping = fallingCopyAngularDrag;
#else
        body.drag = fallingCopyDrag;
        body.angularDrag = fallingCopyAngularDrag;
#endif
        if (gravityScale > 0f && body.GetComponent<ScaledGravity>() == null)
            body.gameObject.AddComponent<ScaledGravity>().scale = gravityScale;
        if (body.GetComponent<OldPixelImpact>() == null)
            body.gameObject.AddComponent<OldPixelImpact>().Setup(landMinSpeed, landFullVolumeSpeed, landCooldown,
                BounceSoundId(body.GetComponent<OldPixelInfo>() != null ? body.GetComponent<OldPixelInfo>().tierIndex : -1));

        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.AddLifetime(FallingCopyLifetime);
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
            if (abbreviateCache < 0) abbreviateCache = PixelPrefs.GetInt(PrefAbbreviate, 1) != 0 ? 1 : 0;
            return abbreviateCache != 0;
        }
        set { abbreviateCache = value ? 1 : 0; PixelPrefs.SetInt(PrefAbbreviate, abbreviateCache); }
    }

    // Short-scale suffixes, one per factor of 1000: K, M, B, T, Qa, Qi, Sx, Sp, Oc, No, then Dc (1e33), UDc, DDc ...
    // up to UCe (1e306), which covers the whole range of a double.
    private static readonly string[] NumberSuffixes = BuildNumberSuffixes();

    private static string[] BuildNumberSuffixes()
    {
        var list = new List<string> { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No" };
        string[] units = { "", "U", "D", "T", "Qa", "Qi", "Sx", "Sp", "O", "N" };
        string[] tens = { "Dc", "Vg", "Tg", "Qag", "Qig", "Sxg", "Spg", "Ocg", "Nog", "Ce" };
        // 10^33 (Dc) .. 10^303 (Ce): unit prefix + tens name, e.g. UDc = 10^36, DVg = 10^69.
        for (int t = 0; t < tens.Length && list.Count < 103; t++)
            for (int u = 0; u < units.Length && list.Count < 103; u++)
                list.Add(units[u] + tens[t]);
        return list.ToArray();
    }

    /// <summary>Always-abbreviated number formatting (1.2K, 3.4M ... from 1K up), whatever the "Abbreviate numbers" setting says. Used by the shop.</summary>
    public static string FormatNumberShort(double value)
    {
        if (value < 999.995) return value.ToString("0.##");
        int s = 0;
        while (value >= 999.995 && s < NumberSuffixes.Length - 1) { value /= 1000; s++; }
        return value.ToString("0.##") + NumberSuffixes[s];
    }

    /// <summary>Number formatting: compact (1.2K, 3.4M ...) or full with separators (1,200), depending on <see cref="AbbreviateNumbers"/>.</summary>
    public static string FormatNumber(double value)
    {
        if (!AbbreviateNumbers) return value.ToString("#,0.##");
        if (value < 999.995) return value.ToString("0.##"); // small amounts keep their decimals (e.g. 6.25)
        int s = 0;
        while (value >= 999.995 && s < NumberSuffixes.Length - 1) { value /= 1000; s++; }
        return value.ToString("0.##") + NumberSuffixes[s];
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
        oldPixelLifetime = Mathf.Max(0f, oldPixelLifetime);
        baseOldPixelCap = Mathf.Max(0, baseOldPixelCap);
    }
#endif
}

