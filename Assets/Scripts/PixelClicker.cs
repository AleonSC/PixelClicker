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
//   FILES OF THIS CLASS (one partial class PixelClicker): PixelClicker.cs = the core (Inspector fields, statics, Awake / Update, the
//   hitbox); PixelClickerClick.cs = the click (CollectInternal = the click flow, and its steps); PixelClickerPointer.cs = what is under the
//   pointer + the mouse reads; PixelClickerCurrency.cs = currency, Ultra, multipliers, unlocks, loading the tiers; PixelClickerAnimation.cs =
//   the cube's animation, Materialize, click effects; PixelClickerNumbers.cs = the Inspector labels and FormatNumber; PixelClickerTypes.cs = PixelType / PixelTier and the type checks; PixelClickerTiers.cs = tier helpers
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
        PixelStatics.ResetOnRestart(OnSceneUnloaded);   // restart-reset-not-progress: unsaved run effects (charges, boosts, the wish multiplier, click blocks)
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

    // Run effects reset above (declared here, next to their reset): the Capacitor's stored charge (PixelClickerSkillBlocks), the Transistor's
    // boost for the next click, and the Golden Hour wish multiplier (PixelDragonWish sets it).
    private static int capacitorCharge;
    private static double transistorBoost = 1d;

    /// <summary>Extra payout multiplier from outside effects (a dragon wish's Golden Hour); 1 normally.</summary>
    public static float WishMultiplier = 1f;

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

    [Tooltip("Also add a real light at the pixel so a glowing tier lights up nearby husks and the scene.")]
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
    [SerializeField, HideInInspector] private int huskSettleVersion; // 1 = husks settle into heaps: bounce 0.3 -> 0.08, friction 0.5 -> 0.65, sleep threshold 0.03; 2 = husks wait: lifetime 15 -> 35 s, floor limit 50 -> 120
    [SerializeField, HideInInspector] private int tierDataVersion; // 1 = Gray / Black unlock at 250 (was 100); 2 = Gray at 300 White, Black at 200 Gray; 3 = Gray / Black unlock by goal (ByGoal)
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
    [Tooltip("Width of the trail, as a fraction of the husk's size.")]
    [SerializeField] private float lightTrailWidth = 0.7f;

    [Min(0.1f)]
    [Tooltip("Brightness of the trail colour (1 = the tier colour, higher = lighter, towards white).")]
    [SerializeField] private float lightTrailBrightness = 1.6f;

    [Min(1f)]
    [Tooltip("Extra glow on these husks: multiplies the tier's Glow Intensity (1 = no extra).")]
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
                        amountPerClick = 1, unlockThreshold = 300, unlockMode = TierUnlockMode.ByGoal },   // (unlocked by the goal 'Crush 10 husks', PixelGoals; the threshold is only the fallback)
        new PixelTier { type = PixelType.Black, displayName = "Black Pixels", color = Color.black,
                        amountPerClick = 1, unlockThreshold = 200, unlockMode = TierUnlockMode.ByGoal },   // (unlocked by 'Recycle 20 husks')
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
    [Tooltip("OFF (the pixel rules) = husks are material that never pays currency again. The Vacuum pixel and Vacuum Device give XP for what they suck up, the first-person axe gives only XP, a hovering Solar pixel keeps daylight up, and the black hole / Ultra Holes no longer pay a husk's value. ON = the old behaviour (husks pay their value again).")]
    [SerializeField] private bool husksPayCurrency = false;
    [Min(0f)]
    [Tooltip("XP one consumed husk gives (Vacuum pixel, Vacuum Device, Ultra Holes), as a share of the XP a manual click gives. Pure husks train Standard, a skill's husks train that skill. 0 = the coded default (0.5).")]
    [SerializeField] private float huskXpShare = 0f;

    [Header("Vacuum Pixel")]
    [Tooltip("Print a console line listing what each Vacuum click re-collected, per pixel type (for checking the numbers).")]
    [SerializeField] private bool logVacuum = true;

    [Tooltip("Seconds husks take to fly into the cube when a Vacuum pixel is clicked.")]
    [SerializeField] private float vacuumSuckDuration = 0.4f;

    [Tooltip("Speed curve of the suck-in (time 0..1, progress 0..1). Rising curves pull faster toward the end.")]
    [SerializeField] private AnimationCurve vacuumSuckCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Nuclear Pixel")]
    [Tooltip("Tick to turn off the Nuclear pixel's radiation (its husks no longer make their neighbours despawn; the pixel itself stays).")]
    [SerializeField] private bool disableNuclearFx = false;

    [Tooltip("How far an old Nuclear pixel's radiation reaches, in main-pixel widths. 0 = 4.")]
    [SerializeField] private float nuclearRadiusPixels = 0f;

    [Tooltip("How fast other husks inside the radiation age, in seconds of age per real second. 0 = 40 (a pixel with a minute left is gone in about a second and a half).")]
    [SerializeField] private float nuclearAgeRate = 0f;

    [Header("Early Pixel Click Effects")]
    [Tooltip("Tick to turn off the small particle pops on the early pixels (White, Gray, Black, Red, Green, Blue, Glass, Luminescent). Players can also change them in Settings > Interface > Click effects.")]
    [SerializeField] private bool disableClickPops = false;

    [Tooltip("How strong the click pops are (0 = 1). 0.5 = half as many particles, 2 = twice as many.")]
    [SerializeField] private float clickPopStrength = 0f;

    [Header("Fire Pixel / Prospector Ores")]
    [Tooltip("Tick to turn off the Fire pixel's burst of flames when it is clicked and its burning husks (the pixel itself stays).")]
    [SerializeField] private bool disableFireFx = false;

    [Min(0f)]
    [Tooltip("Seconds an old Fire pixel takes to burn away. 0 = the coded default (6).")]
    [SerializeField] private float fireBurnSeconds = 0f;

    [Tooltip("Tick to turn off the ore pixels' chips, glitter and rich veins.")]
    [SerializeField] private bool disableProspectorFx = false;

    [Header("Meteor and Solar")]
    [Tooltip("Tick to turn off the Meteor's hit particles and the fiery wake of its husk (the look's embers come from the look).")]
    [SerializeField] private bool disableMeteorFx = false;

    [Range(0f, 1f)]
    [Tooltip("Chance that a fresh ore pixel is a RICH VEIN (it glitters and pays much more). 0 = the coded default (8%).")]
    [SerializeField] private float richVeinChance = 0f;

    [Min(0f)]
    [Tooltip("How many times its normal payout a rich vein pays. 0 = the coded default (5).")]
    [SerializeField] private float richVeinMultiplier = 0f;

    [Tooltip("Husks sucked up swirl round the Vacuum pixel instead of flying straight in. Tick to turn the swirl off.")]
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

    [Header("Husk Physics")]
    [Tooltip("Seconds a husk lies on the floor before it fades away (Standard's Lingering perk adds to it). 0 = never. 35 s (was 15) so husks WAIT: the player clicks a while, builds a pile, then clears it, instead of husks and clicks competing (docs/GameDesign.md 'Early game feel').")]
    [SerializeField] private float oldPixelLifetime = 35f;

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
    [Tooltip("While a potion forces Vacuum pixels to spawn, only every this-many-th Vacuum click actually vacuums the husks (the others just pay out). Stops a chained Vacuum potion from re-collecting the whole floor on every click. 1 = every click vacuums.")]
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

    [Header("Husk Landing Sound")]
    [Min(0f)]
    [Tooltip("A husk only makes a landing sound when it hits a surface at least this fast (world units per second), so resting or rolling pixels stay quiet.")]
    [SerializeField] private float landMinSpeed = 1.5f;

    [Min(0.1f)]
    [Tooltip("The impact speed at which the landing sound is at full volume. Slower hits are quieter.")]
    [SerializeField] private float landFullVolumeSpeed = 8f;

    [Min(0f)]
    [Tooltip("Shortest time (seconds) between landing sounds from the same husk (it bounces a few times).")]
    [SerializeField] private float landCooldown = 0.12f;

    [Header("Husk Size")]
    [Range(0.05f, 1f)]
    [Tooltip("Full size of a husk as a fraction of the clickable pixel (0.5 = half size). Smaller = less visual clutter.")]
    [SerializeField] private float oldPixelScale = 0.5f;

    [Header("Husk Pop-In")]
    [Range(0.01f, 1f)]
    [Tooltip("Size a husk starts at, as a fraction of its normal size (0.05 = tiny).")]
    [SerializeField] private float popStartScale = 0.05f;

    [Min(0f)]
    [Tooltip("Seconds a husk takes to grow back to full size while falling. 0 = full size at once.")]
    [SerializeField] private float popGrowSeconds = 0.35f;

    [Min(0f)]
    [Tooltip("Seconds right after spawning that a husk has NO collision (so quick clicks don't make them pile up). 0 = solid at once.")]
    [SerializeField] private float popNoCollisionSeconds = 0.2f;

    [Header("Husk Despawn")]
    [Range(1f, 2f)]
    [Tooltip("How big a husk swells (times its size) just before it vanishes.")]
    [SerializeField] private float despawnSwellScale = 1.25f;

    [Min(0.01f)]
    [Tooltip("Seconds the swell takes.")]
    [SerializeField] private float despawnSwellSeconds = 0.08f;

    [Min(0.01f)]
    [Tooltip("Seconds it then takes to shrink to nothing and disappear.")]
    [SerializeField] private float despawnShrinkSeconds = 0.15f;

    [Header("Fly-Away Pixels (tiers with Fly Away on)")]
    [Min(0.1f)]
    [Tooltip("Seconds before a fly-away husk is removed.")]
    [SerializeField] private float flyLifetime = 3f;

    [Min(0f)]
    [Tooltip("How long the fiery trail behind a fly-away pixel lasts (seconds). 0 = no trail.")]
    [SerializeField] private float flyTrailSeconds = 0.6f;

    [Range(0f, 45f)]
    [Tooltip("Random angle (degrees) a fly-away pixel's direction may deviate.")]
    [SerializeField] private float flySpreadDegrees = 6f;

    [Tooltip("Floor limit: most husks lying about at once (Standard's Roomy floor perk adds to it). When it is full the OLDEST husk goes first. 0 = no cap. 120 (was 50): at ~3.5 blocks a second 50 husks filled the floor in ~14 s, so the longer lifetime was culled away.")]
    [SerializeField] private int baseOldPixelCap = 120;

    /// <summary>Gravity multiplier of old pixels (the Pixel Bank's aim line uses it to predict where a spat pixel lands).</summary>
    public float OldPixelGravityScale => gravityScale;

    /// <summary>Linear drag of old pixels (see <see cref="OldPixelGravityScale"/>).</summary>
    public float OldPixelDrag => fallingCopyDrag;

    /// <summary>The floor cap in use: the Inspector value + the Standard skill's 'Roomy floor' perk.</summary>
    public int OldPixelCap => baseOldPixelCap + PixelSkills.StandardCapacityBonus;

    [Tooltip("Max husks kept while Time Stop is on (the stockpile that bursts out when time resumes). 0 = no cap.")]
    [SerializeField] private int timeStopStockpileMax = 150;

    [Tooltip("Husks below this world Y are destroyed (catches pixels that fall off the world).")]
    [SerializeField] private float fallingCopyKillHeight = -50f;

    [Tooltip("Random launch speed range (min, max) in units/second.")]
    [SerializeField] private Vector2 popSpeedRange = new Vector2(1.5f, 4f);

    [Tooltip("Vertical part of the launch direction (min, max). -1 = straight down, 0 = sideways, 1 = straight up. Negative values pop it toward the ground.")]
    [SerializeField] private Vector2 popVerticalRange = new Vector2(-0.6f, 0.1f);

    [Tooltip("Random spin range (min, max) in radians/second, applied around a random axis.")]
    [SerializeField] private Vector2 popSpinRange = new Vector2(1f, 6f);

    [Tooltip("Mass of a husk. Affects how it pushes other rigidbodies.")]
    [SerializeField] private float fallingCopyMass = 1f;

    [Tooltip("Air resistance (linear). 0 = none.")]
    [SerializeField] private float fallingCopyDrag = 0f;

    [Tooltip("Air resistance on spin.")]
    [SerializeField] private float fallingCopyAngularDrag = 0.05f;

    [Tooltip("Gravity multiplier. 0 = floats, 1 = normal gravity (uses the project's gravity setting).")]
    [SerializeField] private float gravityScale = 1f;

    [Range(0f, 1f)]
    [Tooltip("Bounciness of husks (0 = no bounce, 1 = perfect bounce). Low on purpose: a husk lands, maybe rolls or tips once, and stays put, so the floor fills with small heaps. Types whose look sets its own physics (Water, Sap, Leaf...) keep their own value.")]
    [SerializeField] private float bounciness = 0.08f;

    [Range(0f, 1f)]
    [Tooltip("Friction of husks (higher = they stop sooner and heaps hold their shape). Types whose look sets its own physics keep their own value.")]
    [SerializeField] private float friction = 0.65f;

    [Min(0f)]
    [Tooltip("How still a husk must be before the physics lets it fall asleep (Rigidbody.sleepThreshold; Unity's default is 0.005). Higher = heaps go still sooner and cost nothing; too high and a slow-rolling husk freezes mid-roll. Anything that touches a sleeping husk wakes it.")]
    [SerializeField] private float huskSleepThreshold = 0.03f;

    [Tooltip("Reuse the objects of husks that have vanished for new husks instead of making new ones each click (PixelHuskPool). Turn off if a husk ever behaves oddly after a lot of clicking: everything works the same without it.")]
    [SerializeField] private bool huskPooling = true;

    [Min(0)]
    [Tooltip("The most vanished husks kept switched off for reuse.")]
    [SerializeField] private int huskPoolSize = 64;

    [Tooltip("Continuous collision stops fast pixels tunneling through thin floors. Costs slightly more.")]
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

    [Tooltip("Layer husks are placed on (controls what they collide with in Project Settings > Physics). -1 = leave on Default.")]
    [SerializeField] private int fallingCopyLayer = -1;

    [Tooltip("Can husks block the click raycast? Off = clicks pass through them to the live pixel.")]
    [SerializeField] private bool oldPixelsBlockClicks = false;

    [Tooltip("Should husks collide with the live floating pixel?")]
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
    [Tooltip("Each seed has its own despawn timer: seconds from the moment it drops until it withers away if it hasn't popped its pixel yet. 0 = the coded default (45). The pixel it grows is an ordinary husk with its own normal despawn timer.")]
    [SerializeField] private float seedSproutLifetime = 0f;

    [Min(0f)]
    [Tooltip("Seeds can't be planted closer together than this (world units between sprout centres). 0 = the coded default (0.7 x a sprout's width).")]
    [SerializeField] private float seedSpacing = 0f;

    [Min(1f)]
    [Tooltip("How big the sprout is drawn compared to a husk (bigger = easier to spot).")]
    [SerializeField] private float seedSproutScale = 2.2f;

    [Min(0f)]
    [Tooltip("How strongly a sprout favours rare pixel types: a type's chance is 1 / spawnWeight to this power. 0 = every unlocked type equally likely, 1 = rarer types are proportionally more likely.")]
    [SerializeField] private float seedRarityBias = 1f;

    [Min(2)]
    [Tooltip("This many old Solar pixels touching each other (a connected group) never run out: they stay on the floor indefinitely. 0 = switched off.")]
    [SerializeField] private int solarClusterSize = 5;

    [Min(0.5f)]
    [Tooltip("Two old Solar pixels count as touching when their centres are within this many husk widths of each other.")]
    [SerializeField] private float solarClusterDistance = 1.5f;

    [Min(0f)]
    [Tooltip("How close (in husk widths) an old Solar pixel must lie to a growing sprout to speed it up.")]
    [SerializeField] private float seedSolarReach = 6f;

    [Min(0f)]
    [Tooltip("Growth speed added to a sprout by each old Solar pixel in reach (0.75 = +75% each).")]
    [SerializeField] private float seedSolarBoostEach = 0.75f;

    [Min(0f)]
    [Tooltip("Most growth speed Solar pixels can add to one sprout (3 = up to 4x as fast).")]
    [SerializeField] private float seedSolarBoostMax = 3f;

    [Min(0f)]
    [Tooltip("How close (in husk widths) a Water pixel must splash to a growing sprout or the Seed pet to water it.")]
    [SerializeField] private float seedWaterReach = 5f;

    [Min(0f)]
    [Tooltip("Seconds of growth (sowing time for the Seed pet) a Water pixel's splash adds at once to every growing sprout / planted Seed pet within reach.")]
    [SerializeField] private float seedWaterGrowSeconds = 1.5f;

    [Min(0f)]
    [Tooltip("Each sprout / Seed pet accepts water at most this often (seconds), so an auto clicker streaming Water pixels can't make growth instant. 1.5 s of growth per 0.75 s = about double speed at most.")]
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

    /// <summary>Dev tools: how many clicks one click counts as (1 = normal). Multiplies payouts, tough-pixel hits and the combo.</summary>
    public static int DevClickCount = 1;

    /// <summary>Dev cheat (set by Dev Tools): everything costs nothing - spending always works and takes nothing away.</summary>
    public static bool InfiniteResources;

    /// <summary>Dev tools: the limit on how many old pixels can lie around is switched off while true.</summary>
    public static bool DevNoOldPixelLimit;

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
        PixelHuskPool.Enabled = huskPooling;
        PixelHuskPool.MaxPooled = huskPoolSize;
        if (pixelTransform == null) pixelTransform = transform;
        if (pixelRenderer == null) pixelRenderer = pixelTransform.GetComponentInChildren<Renderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        if (audioSource == null && (defaultClickSound != null || unlockSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        RunLookFixups();   // brings the saved Pixel Looks list up to date (PixelClickerLookFixups.cs)
        RunTierDataFixups();
        RunHuskSettleFixups();
        FixMissingPixelMaterial();

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

        // The clicker's tiers keep their progress in the GameState from here on (PixelTier.MakeLive), then the start-unlocked flags.
        MakeTiersLive();
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
        ReapplyTextOverrides(false);
#if UNITY_EDITOR
        PixelTextOverrides.AppendMissing(this);   // (the file lists every pixel type, new ones too)
#endif
    }

    // Awake step: upgrades the saved tier list's unlock rules (tierDataVersion; only untouched old defaults are changed).
    private void RunTierDataFixups()
    {
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
        if (tierDataVersion < 3)
        {
            // The early goals ARE the unlocks (2026-10-18): Gray and Black are unlocked by goal rewards (PixelGoals: Crush 10 husks, Recycle 20 husks),
            // not by earning 300 White / 200 Gray. Their thresholds stay as the fallback when no goal unlocks them.
            foreach (PixelTier t in tiers)
                if (t != null && (t.type == PixelType.Gray || t.type == PixelType.Black) && t.unlockMode == TierUnlockMode.PreviousTierThreshold)
                    t.unlockMode = TierUnlockMode.ByGoal;
            tierDataVersion = 3;
        }
    }

    // Awake step: upgrades the saved husk physics defaults (huskSettleVersion; only untouched old defaults are changed).
    private void RunHuskSettleFixups()
    {
        if (huskSettleVersion < 1)
        {
            // Husks settle into small heaps (2026-10-17): less bounce, a little more grip, quick sleep. Only untouched old defaults are changed.
            if (Mathf.Approximately(bounciness, 0.3f)) bounciness = 0.08f;
            if (Mathf.Approximately(friction, 0.5f)) friction = 0.65f;
            if (huskSleepThreshold <= 0f) huskSleepThreshold = 0.03f;
            huskSettleVersion = 1;
        }
        if (huskSettleVersion < 2)
        {
            // Husks wait (2026-10-18, 'Early game feel 1'): they last 35 s and the floor holds 120. Only untouched old defaults are changed.
            if (Mathf.Approximately(oldPixelLifetime, 15f)) oldPixelLifetime = 35f;
            if (baseOldPixelCap == 50) baseOldPixelCap = 120;
            huskSettleVersion = 2;
        }
    }

    // Awake step: a deleted / missing pixel material is replaced by a plain generated one; remembers the default material.
    private void FixMissingPixelMaterial()
    {
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
    }

    /// <summary>Gives every tier its name from Assets/Resources/PixelTexts.txt (PixelTextOverrides); Dev Hub's 'Reload pixel texts' calls it after a Reload.</summary>
    public void ReapplyTextOverrides(bool refreshUI = true)
    {
        foreach (PixelTier t in tiers) PixelTextOverrides.ApplyName(t);
        if (refreshUI) RefreshUI();
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
        MakeTiersLive();   // (a tier object the Inspector re-created while playing joins the GameState again; a plain flag check per tier)
        AnimatePixel();

        CleanOldPixels();
        using (solarMarker.Auto()) UpdateSolarClusters();
        if (Time.timeScale > 0f && !PixelPauseMenu.IsPaused) { UpdateWeevil(); UpdateHazardCharges(); UpdateHazardExpiry(); }

        if ((Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PixelPauseMenu.IsPaused) return; // paused (see PixelPauseMenu); Time Stop still lets the cube be clicked
        if (clicksBlocked || ExternalClickBlock || GodMode || PixelCameraIntro.ClicksLocked) return; // e.g. placing a device (PixelConsumables) or holding the hose (PixelBank)
        bool leftClick = WasClickedThisFrame();
        // A RIGHT click on a hazard block (Pest, Gas Pocket) works it away safely (see DefuseHazard).
        bool rightClick = !leftClick && pixelTransform != null && !isSpawning && PixelInput.RightPressed() && (IsHazard(GetClickTier().type) || GetClickTier().type == PixelType.Bottle)
                          && PixelRightClick.Current == PixelRightClick.Owner.Cube;   // (the shared right-click order, PixelRightClick)
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

    /// <summary>Re-draws the pixel on the cube with its current look (e.g. after the colour-blind setting changed, or an edit in the Look Studio).</summary>
    public void RefreshLooks()
    {
        if (pixelRenderer == null || tiers == null || tiers.Length == 0) return;
        if (liveExtras != null) Destroy(liveExtras);
        liveExtras = null;
        liveExtrasBuilt = false;
        liveGlowShellType = (PixelType)(-1);   // the glow shell is built again too (its colour / strength may have changed)
        ApplyTierLook(GetClickTier());
    }

    private void OnDestroy()
    {
        if (hitbox != null) Destroy(hitbox.gameObject);
        ExternalClickBlock = false;
    }

    // ------------------------------------------------------------------
    // Editor helpers
    // ------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnValidate()
    {
        PixelHuskPool.Enabled = huskPooling;
        PixelHuskPool.MaxPooled = huskPoolSize;
        if (tiers != null && tiers.Length > 0)
            activeTierIndex = Mathf.Clamp(activeTierIndex, 0, tiers.Length - 1);
        materializeDuration = Mathf.Max(0f, materializeDuration);
        oldPixelLifetime = Mathf.Max(0f, oldPixelLifetime);
        baseOldPixelCap = Mathf.Max(0, baseOldPixelCap);
    }
#endif
}
