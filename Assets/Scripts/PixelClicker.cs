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
//   FILES OF THIS CLASS (one partial class PixelClicker): PixelClicker.cs = the core (Inspector fields, Awake / Update, input, the click
//   and CollectInternal, Materialize); PixelClickerTypes.cs = PixelType / PixelTier and the type checks; PixelClickerTiers.cs = tier helpers
//   and the rare-tier setup; PixelClickerLooks.cs = the cube's look, glow and materials; PixelClickerLookFixups.cs = the looksVersion steps;
//   PixelClickerHusks.cs = husks (falling copies, vacuum, saving, device hand-offs); PixelClickerSkillBlocks.cs = farming / solar / seeds /
//   electric / treasure / fishing hooks; PixelClickerHazards.cs = hazard blocks; PixelFishingSpawns.cs and PixelBurstPop.cs = older pieces.
//   The components that ride on husks are separate classes in PixelHusks.cs.
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
    [SerializeField, HideInInspector] private int tierDataVersion; // 1 = Gray / Black unlock at 250 (was 100); 2 = Gray at 300 White, Black at 200 Gray
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
                        amountPerClick = 1, unlockThreshold = 300 },
        new PixelTier { type = PixelType.Black, displayName = "Black Pixels", color = Color.black,
                        amountPerClick = 1, unlockThreshold = 200 },
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
        get => externalClickBlock || DevClickBlock || PixelHints.IntroHoldsGuide || PixelFirstPerson.Active || PixelConsumables.HoveringDisarmed || PixelPointerClaim.Claimed || PixelRobotWorker.Hovering || PixelRobotWorker.Piloting;
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

    /// <summary>The camera the game is seen through: the clicker's target camera, else Camera.main. (One helper for the 'TargetCamera ?? Camera.main' that was written out in 60 scripts.)</summary>
    public Camera ViewCamera => targetCamera != null ? targetCamera : Camera.main;

    /// <summary>ViewCamera of 'c', or Camera.main when there is no clicker.</summary>
    public static Camera ViewCameraOf(PixelClicker c) => c != null ? c.ViewCamera : Camera.main;
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
        PixelSystems.AddMissing(gameObject);   // every system component not already in the scene (the list and their groups: PixelSystems.cs)
        if (pixelTransform == null) pixelTransform = transform;
        if (pixelRenderer == null) pixelRenderer = pixelTransform.GetComponentInChildren<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (audioSource == null && (defaultClickSound != null || unlockSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        RunLookFixups();   // brings the saved Pixel Looks list up to date (PixelClickerLookFixups.cs)
        if (tierDataVersion < 1)
        {
            // Monochrome takes longer (2026-10-16): Gray needs 250 White, Black 250 Gray (was 100 each). Only an untouched 100 is changed.
            foreach (PixelTier t in tiers)
                if (t != null && (t.type == PixelType.Gray || t.type == PixelType.Black) && t.unlockMode == TierUnlockMode.PreviousTierThreshold && t.unlockThreshold == 100d)
                    t.unlockThreshold = 250d;
            tierDataVersion = 1;
        }
        if (tierDataVersion < 2)
        {
            // Fewer of each later tier (2026-10-16): the spawn pool gets more mixed with every tier, so Gray needs 300 White, Black 200 Gray
            // (and the RGB step asks 100 Black). Only an untouched 100 / 250 is changed.
            foreach (PixelTier t in tiers)
            {
                if (t == null || t.unlockMode != TierUnlockMode.PreviousTierThreshold || (t.unlockThreshold != 100d && t.unlockThreshold != 250d)) continue;
                if (t.type == PixelType.Gray) t.unlockThreshold = 300d;
                else if (t.type == PixelType.Black) t.unlockThreshold = 200d;
            }
            tierDataVersion = 2;
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
    /// Power click (PixelPowerClick, TEST): ONE manual click that counts as 'clicks' clicks - payout x clicks, tough blocks take that many hits,
    /// the combo grows by that many (the same path the dev 'clicks per click' setting uses, DevClickCount).
    /// </summary>
    public void PowerClick(int clicks)
    {
        int was = DevClickCount;
        DevClickCount = Mathf.Max(1, was) * Mathf.Max(1, clicks);
        try { CollectInternal(false); }
        finally { DevClickCount = was; }
    }

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
        else if (spawnFallingCopy)
        {
            aimThisCopy = !automatic;   // (edge-click aiming: a manual click's husk flies towards the side of the cube that was clicked - PixelBurstPop.AimedDirection)
            SpawnFallingCopy(tierIndex, amount);
            aimThisCopy = false;
        }

        // Roll the next pixel AFTER the click so a freshly unlocked tier can appear immediately.
        if (randomizeSpawnTier) currentTierIndex = RollTier();
        Materialize(GetClickTier());

        onPixelClicked?.Invoke();
    }

    /// <summary>Adds to a tier's amount (use negative via <see cref="TrySpend"/> instead) and checks unlocks.</summary>
    /// <summary>
    /// Currency that does NOT count as earned: it is added to what you hold, but not to the lifetime total that unlocks the next tier (and shows
    /// in the Log). Used by the Recycler, so tier unlocks come from clicking, with recycling as a bonus on top.
    /// </summary>
    public void AddBonusCurrency(int tierIndex, double amount)
    {
        if (!IsValidTier(tierIndex) || amount <= 0 || IsGodCube(tiers[tierIndex].type)) return;
        tiers[tierIndex].count += amount;
        CurrencyGained?.Invoke(tierIndex, amount);
        NotifyChanged();
    }

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
        if (firstOfRareDrop) StartCoroutine(AnnounceAfterShow(tierIndex));   // the find show plays first, then the text box

        CheckUnlocks();
        NotifyChanged();
    }

    /// <summary>Waits for the find show (Dragon Cube / God / Prime Cube) to finish, so its text box doesn't pop up on top of the animation.</summary>
    private IEnumerator AnnounceAfterShow(int tierIndex)
    {
        yield return null;   // (the show starts in the same click)
        while (PixelDragonFind.Playing || PixelGodFind.Playing) yield return null;
        AnnounceDragonCube(tierIndex);
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

    private static float AnimDelta => (PixelTimeStop.IsStopped || PixelTimeStop.IsSlowed || PixelTitleScreen.Showing) ? Time.unscaledDeltaTime : Time.deltaTime;

    /// <summary>0..1 pulse that tells a brand-new player to click the cube: only while the guide text shows and nothing has been collected yet.</summary>
    private float ClickHintPulse(float time)
    {
        if (disableClickHint || clickHintDone || !ClickHint) return 0f;
        foreach (PixelTier t in tiers)
            if (t.totalCollected > 0d) { clickHintDone = true; return 0f; }
        return 0.5f + 0.5f * Mathf.Sin(time * clickHintSpeed * Mathf.PI * 2f);
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

    private int liveNeed;           // clicks the pixel on the cube needs (only for random-click pixels, i.e. the Treasure chest)

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
