// ---- System notes ----
// PixelClickerTypes.cs
//   Part of partial class PixelClicker: the data shapes - the PixelType enum (every pixel type; values are only ever APPENDED so Inspector
//   data stays valid), TierUnlockMode, PixelTier (one currency / block: colour, spawn weight, payout, toughness, glow, fly-away...) and the
//   SpawnSwitch, plus the type checks right under the enum (IsWoodBlock, IsHazard, IsDragonCube, IsRareCube... - all read the block tags,
//   PixelBlockTags). Split out of PixelClicker.cs.
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

public partial class PixelClicker
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
        Singularity = 10,
        Ghost = 11,
        Meteor = 12,
        Solar = 13,
        Electric = 14,
        DragonCube1 = 15,
        DragonCube2 = 16,
        DragonCube3 = 17,
        DragonCube4 = 18,
        DragonCube5 = 19,
        DragonCube6 = 20,
        DragonCube7 = 21,
        Mirror = 22,
        Seed = 23,
        Water = 24,
        Fire = 25,
        Copper = 26,
        Tin = 27,
        Iron = 28,
        Lead = 29,
        Zinc = 30,
        Nickel = 31,
        Treasure = 32,
        Nuclear = 33,
        Wood = 34,
        Rock = 35,
        Dirt = 36,
        Fertilizer = 37,
        Pollen = 38,
        Wheat = 39,
        Weevil = 40,
        GoldenFruit = 41,
        Wire = 42,
        Battery = 43,
        Capacitor = 44,
        Resistor = 45,
        Transistor = 46,
        Fuse = 47,
        Bulb = 48,
        Pest = 49,        // Farming's hazard block
        GasPocket = 50,   // Mining's hazard block
        Bark = 51,        // Woodcutting
        Sap = 52,
        Leaf = 53,
        Heartwood = 54,
        Twig = 55,        // Woodcutting's filler block
        HornetNest = 56,  // Woodcutting's hazard block: now the TERMITE HIVE (the enum value keeps its old name so saves stay valid)
        Berry = 57,       // Foraging
        Mushroom = 58,
        Herb = 59,
        Truffle = 60,
        Weeds = 61,       // Foraging's filler block
        Toadstool = 62,   // Foraging's hazard block
        Silver = 63,      // Mining
        Gold = 64,
        Bedrock = 65,
        Fish = 66,        // Fishing
        Seaweed = 67,
        Shell = 68,
        Pearl = 69,
        Driftwood = 70,   // Fishing's filler block
        Jellyfish = 71,   // Fishing's hazard block
        Bait = 72,        // Fishing: drops bait pellets that make fish appear more often
        Bottle = 73,      // Fishing's special block: a message in a bottle (breaking it gives a treasure map)
        Whale = 74,       // Fishing's special block: a whale that fights the line
        Mold = 75,        // one of Foraging's two hazard blocks (with the Toadstool): see PixelMold.cs
        Charcoal = 81,    // made by the Kiln (PixelKiln): a standalone block that only exists as an old pixel
        GodSlifer = 82,   // the three Egyptian God Cubes (PixelGodFind.cs): the rarest drops in the game, only one of each can be held
        GodRa = 83,
        GodObelisk = 84,
        StoneSpace = 85,  // the six Prime Cubes (PixelGodFind.cs, same one-of-each rules as the God Cubes) ...
        StoneMind = 86,
        StoneReality = 87,
        StonePower = 88,
        StoneTime = 89,
        StoneSoul = 90,
        Pinecone = 92,    // Woodcutting's second filler block (the filler family: Twig, Pinecone, Moss share the filler share)
        Moss = 93,        // Woodcutting's third filler block
        InfinityCubelette = 91,   // ... and what the Prime Gauntlet makes of all six (PixelInfinity.cs): never drops
        Pesticide = 80,   // Woodcutting's base block that can be held (Block Hold): while it is held a left click clears a Hornet Nest at once (PixelBlockRules HoldRule.clearsHazard)
        Acorn = 79,       // Woodcutting's base block: acorn old pixels lying on the field raise the Great Oak's spawn chance (PixelBlockRules SpecialRule boost)
        Whetstone = 78,   // Woodcutting's base block that can be held (Block Hold): while it is held every manual click is followed by a rapid burst of extra clicks (PixelBlockRules HoldRule.extraClicks)
        GreatOak = 77,    // Woodcutting's rare giant block (50-100 clicks, shakes bird nests loose): see PixelGreatOak.cs
        WoodChips = 76,   // spat out by the Wood Chipper (every 5 wood pixels shredded): never spawns on the cube, only exists as an old pixel (PixelWoodChipper)
        Tinder = 94,      // Firemaking (dev-only skill pack, mechanics to come): dry fluff and grass
        Kindling = 95,    // Firemaking: a bundle of thin sticks
        Firewood = 96,    // Firemaking: a split log
        Flint = 97,       // Firemaking: a striker stone that throws sparks
        Ember = 98,       // Firemaking: a glowing coal
        Ash = 99,         // Firemaking's filler block
        Wildfire = 100,   // Firemaking's hazard block: set it off and the flames spread over the husks around the cube
        GoldenSnitch = 101,   // a very rare drop that sprouts wings and flies off the cube: catch it (PixelGoldenSnitch.cs)
    }

    /// <summary>The Firemaking skill pack's blocks (Fire, Tinder, Kindling, Firewood, Flint, Ember, Charcoal, the filler Ash; Wildfire is its hazard).</summary>
    public static bool IsFireBlock(PixelType type) => PixelBlockTags.Has(type, PixelTag.Firemaking);   // (the tag table in PixelBlockTags.cs)

    /// <summary>The Fishing skill pack's blocks: Fish, Seaweed, Shell, Pearl and the filler Driftwood (the Jellyfish is its hazard).</summary>
    public static bool IsFishBlock(PixelType type) => PixelBlockTags.Has(type, PixelTag.Fish);   // (the tag table in PixelBlockTags.cs)

    /// <summary>Ores taken out of Mining (none at the moment: Lead, Zinc and Nickel are back). Tag a block RetiredOre to keep it out of the spawns.</summary>
    public static bool IsRetiredOre(PixelType type) => PixelBlockTags.Has(type, PixelTag.RetiredOre);   // (the tag table in PixelBlockTags.cs)

    /// <summary>The Foraging skill pack's blocks: Berry, Mushroom, Herb, Truffle and the filler Weeds (the Toadstool is its hazard).</summary>
    public static bool IsForageBlock(PixelType type) => PixelBlockTags.Has(type, PixelTag.Forage);   // (the tag table in PixelBlockTags.cs)

    /// <summary>The Woodcutting skill pack's blocks: Wood, Bark, Sap, Leaf, Heartwood and the filler Twig (the Hornet Nest is its hazard).</summary>
    public static bool IsWoodBlock(PixelType type) => PixelBlockTags.Has(type, PixelTag.Wood);   // (the tag table in PixelBlockTags.cs)

    /// <summary>The Farming skill pack's pixels: Seed, Water and the six new farm blocks (Dirt, Fertilizer, Pollen, Wheat, Weevil, Golden Fruit).</summary>
    public static bool IsFarm(PixelType type) => PixelBlockTags.Has(type, PixelTag.Farm);   // (the tag table in PixelBlockTags.cs)

    /// <summary>A HAZARD block (the Pest in Farming, the Gas Pocket in Mining): hit it too many times and it goes off.</summary>
    public static bool IsHazard(PixelType type) => PixelBlockTags.Has(type, PixelTag.Hazard);   // (the tag table in PixelBlockTags.cs)

    /// <summary>The six Prospector ores (Copper, Tin, Iron, Lead, Zinc, Nickel).</summary>
    public static bool IsOre(PixelType type) => PixelBlockTags.Has(type, PixelTag.Ore);   // (the tag table in PixelBlockTags.cs)

    /// <summary>Pixel types with no integration beyond the basics (looks, sounds, click data, breaking): the Fire pixel and the Prospector ores. They get no potions, seeds, crafting, pets, achievements or Value upgrades.</summary>
    public static bool IsStandalone(PixelType type) => PixelBlockTags.Has(type, PixelTag.Standalone);   // (the tag table in PixelBlockTags.cs)

    /// <summary>Is this one of the seven Dragon Cubes (extremely rare drops that summon the cube dragon when all are gathered)?</summary>
    public static bool IsDragonCube(PixelType type) => PixelBlockTags.Has(type, PixelTag.DragonCube);   // (the tag table in PixelBlockTags.cs)

    /// <summary>Is this one of the three Egyptian God Cubes (Slifer, Ra, Obelisk): the rarest drops of all, only one of each can ever be held?</summary>
    public static bool IsGodCube(PixelType type) => PixelBlockTags.Has(type, PixelTag.RareCollectible);   // (the tag table in PixelBlockTags.cs)

    /// <summary>One of the six Prime Cubes (Space, Mind, Reality, Power, Time, Soul).</summary>
    public static bool IsInfinityStone(PixelType type) => PixelBlockTags.Has(type, PixelTag.PrimeCube);   // (the tag table in PixelBlockTags.cs)

    /// <summary>A Dragon Cube or a God Cube: the rare collectible drops kept out of potions, crafting, pets, achievements and the Value upgrades.</summary>
    public static bool IsRareCube(PixelType type) => IsDragonCube(type) || IsGodCube(type) || type == PixelType.GoldenSnitch;   // (the rare collectibles: no pets, potions, crafting or pixel achievements; the Golden Snitch too)

    /// <summary>How many small cubes (1-7) a Dragon Cube carries; 0 for every other pixel type.</summary>
    public static int DragonCubeStars(PixelType type) => IsDragonCube(type) ? (int)type - (int)PixelType.DragonCube1 + 1 : 0;

    /// <summary>Extra payout multiplier from outside effects (a dragon wish's Golden Hour); 1 normally.</summary>
    public static float WishMultiplier = 1f;

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

        [Tooltip("Pay a random whole amount between 'Payout Min' and 'Payout Max' (before multipliers) every time a pixel of this tier is harvested, instead of a fixed amount. 'Amount Per Click' should then be the average (used for price and offline estimates).")]
        public bool randomPayout = false;

        [Min(1)]
        [Tooltip("Smallest random payout.")]
        public int payoutMin = 1;

        [Min(1)]
        [Tooltip("Largest random payout.")]
        public int payoutMax = 15;

        [Min(1)]
        [Tooltip("How many clicks it takes to collect one pixel of this tier. Only the last click pays out (the pixel is just hit before that).")]
        public int clicksToCollect = 1;

        [Tooltip("Treasure pixel: every fresh pixel needs a RANDOM number of clicks between 'Random Clicks Min' and 'Random Clicks Max' (Clicks To Collect is then just the average, for estimates). The more clicks it took, the bigger the reward.")]
        public bool randomClicks = false;

        [Min(1)]
        [Tooltip("Fewest clicks a random-click pixel can need.")]
        public int randomClicksMin = 1;

        [Min(1)]
        [Tooltip("Most clicks a random-click pixel can need.")]
        public int randomClicksMax = 20;

        [Tooltip("Lifetime amount of the PREVIOUS tier needed to unlock this tier (total collected, spending does not lower it). Ignored if 'Unlocked At Start' is on.")]
        public double unlockThreshold = 100;

        [Tooltip("Tier is available from the very beginning.")]
        public bool unlockedAtStart = false;

        [Tooltip("STALL: every click on this block freezes the Block Hold meter (it stops draining) for 'Stall Seconds'. Any block can have it, whatever its hit count.")]
        public bool stallsHold = false;

        [Min(0f)]
        [Tooltip("How long one click on a stalling block freezes the Block Hold meter (each click restarts it). 0 = the coded default (1.5).")]
        public float stallSeconds = 0f;

        [Range(0f, 1f)]
        [Tooltip("STALL blocks also REFILL the Block Hold meter: every click on this block adds this fraction of the whole meter (0.05 = 5%). 0 = the coded default (0.04).")]
        public float stallRefill = 0f;

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

        [Tooltip("Screen direction a fly-away pixel leaves in (x = right, y = up). Used when 'Fly Random Direction' is off.")]
        public Vector2 flyDirection = new Vector2(1f, 0.6f);

        [Tooltip("Each fly-away pixel shoots off in a random direction inside 'Fly Angle Range' instead of the fixed direction.")]
        public bool flyRandomDirection = true;

        [Tooltip("Random direction range in degrees on the screen: 0 = right, 90 = straight up, 180 = left. 10 to 170 = anywhere in the top half.")]
        public Vector2 flyAngleRange = new Vector2(10f, 170f);

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

        [Tooltip("Total ever collected (never decreases when spending). Used for unlock thresholds, achievements and stats.")]
        public double totalCollected;

        [Tooltip("Runtime: how many times the player has spent Ultra pixels to boost this pixel type's payout (saved). See the shop's Upgrades > Pixel tab.")]
        public int ultraLevel;

        [Tooltip("Runtime: Value upgrade level of this pixel type, bought with its own currency (saved). Raises its payout. See the shop's Upgrades > Value tab.")]
        public int valueLevel;


        [Tooltip("How many pixels of this type have been collected (one per payout, whatever it paid). Used by the 'times clicked' achievements.")]
        public long timesCollected;

        [Tooltip("Has this tier been unlocked?")]
        public bool unlocked;

        [Tooltip("A rare drop: spawns even though it was never 'unlocked', stays out of the unlock chain, the Value upgrades, potions, pets and achievements, and only shows in the inventory once you hold one (the seven Dragon Cubes).")]
        public bool rareDrop = false;

        [Tooltip("Runtime: the player switched this pixel's spawning off (saved).")]
        public bool spawnDisabled;

        /// <summary>True for the special pixels (Vacuum, Obsidian, Singularity, Ghost, Meteor, Electric, Mirror, Seed) that can have their spawning switched off.</summary>
        public static bool IsSpecialType(PixelType t) =>
            t == PixelType.Vacuum || t == PixelType.Singularity ||
            t == PixelType.Ghost || t == PixelType.Meteor || t == PixelType.Solar || t == PixelType.Mirror || t == PixelType.Treasure || t == PixelType.Nuclear;   // (Fire is a Firemaking block now: no spawn switch)

        /// <summary>True if the player may switch this pixel's spawning off.</summary>
        public bool CanSwitchOff
        {
            get
            {
                if (spawnSwitch == SpawnSwitch.Switchable) return true;
                if (spawnSwitch == SpawnSwitch.AlwaysOn) return false;
                if (type == PixelType.Glass || type == PixelType.Luminescent) return true;   // extra pixels: a spawn switch in Toggles, on by default
                return IsSpecialType(type);
            }
        }

        /// <summary>Can this pixel spawn right now (unlocked and not switched off)?</summary>
        public bool CanSpawn => (unlocked || rareDrop || PixelSkills.PreviewForces(type)) && !(spawnDisabled && CanSwitchOff) && !(IsGodCube(type) && count >= 1d);   // (a God Cube you hold never comes again: one of each)

        /// <summary>The tier colour with full opacity, for text and icons (a glass tier's colour is see-through).</summary>
        public Color UIColor => new Color(color.r, color.g, color.b, 1f);

        /// <summary>Shallow copy (used by the shop to add its reward tiers).</summary>
        public PixelTier Clone() => (PixelTier)MemberwiseClone();
    }
}
