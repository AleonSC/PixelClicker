using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SHELL of the skill system: skill packs replace the standard base cubes (monochrome and RGB) with a different set, and you train
/// the skill that belongs to the pack you play with. This file holds the data and the rules; the window is in PixelSkillsWindow.cs.
///
/// What works now: the pack list (Standard + Mining, built on the Prospector pack), the active pack (Mining = Prospector mode, one source of truth),
/// XP and levels per pack (saved through the generic stat counters, nothing extra in the save file), level-up announcements and the Skills window.
/// What does not exist yet: perks that change the game, other packs, the XP bar on the main screen and a cycle-skill key (all on the to-do list).
/// </summary>
public partial class PixelSkills : MonoBehaviour
{
    [Serializable]
    public class Perk
    {
        [Tooltip("Level at which the perk is unlocked.")]
        public int level = 2;
        [Tooltip("What the perk does.")]
        public string text = "";
        [Tooltip("Off = shown as 'coming soon' (the effect is not built yet).")]
        public bool implemented = false;
        [TextArea(2, 5)]
        [Tooltip("A tutorial popup shown once when this level is reached (empty = none). Only with first-time tips on.")]
        public string noticeText = "";
        [Tooltip("Show the popup beside the Skills window: the window opens on this skill and its card gets the pulsing gold frame (PixelHighlight) until Got it is pressed. Off = the usual box on its own.")]
        public bool noticeInWindow = false;
        [Tooltip("A pixel type (name) this perk unlocks as a toggle: from this level the skill-only block can be switched on in the Toggles window so it also spawns in other rotations (like a tree node's Guest Type).")]
        public string guestType = "";
    }

    [Serializable]
    public class TreeNode
    {
        [Tooltip("Unique id inside the pack (the unlock is saved as the counter 'skill.<pack>.node.<id>').")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Id of the node that must be unlocked first (empty = a root node). Several ids separated by commas = ALL of them are needed (e.g. 'hold,power').")]
        public string requires = "";
        [Tooltip("Row in the tree (0 = top, then 1, 2, 3, 4 = the lowest; Standard's husk branch uses rows 3 and 4 - with more than 3 rows the nodes get shorter to fit). The column follows from how many nodes lie above it.")]
        public int row = 1;
        [Min(0)]
        [Tooltip("The skill must have reached this level before the node can be unlocked (0 = no level needed). Holds a root node back until later, e.g. Standard's Chain Crush at level 4.")]
        public int minLevel = 0;
        [Min(0)]
        [Tooltip("The column it is drawn in at the least (0 = from the chain of nodes it needs). Lets a root node sit beside nodes further along, e.g. Chain Crush next to the first twists; nodes after it move along with it.")]
        public int column = 0;
        [Tooltip("Skill points it costs. A skill earns one point per level above 1.")]
        public int cost = 1;
        [Tooltip("Off = shown as 'coming soon' (the effect is not built yet).")]
        public bool implemented = false;
        [Tooltip("A pixel type (name) this node unlocks as a toggle: once bought, that skill-only block can be switched on in the Toggles window so it also spawns in other rotations.")]
        public string guestType = "";

        // The unlock counter's name 'skill.<pack>.node.<id>', made once (it is read every frame by the tree and the Standard skills); rebuilt if an id changes.
        [NonSerialized] private string key, keyPack, keyId;
        public string KeyIn(string packId)
        {
            if (key == null || keyPack != packId || keyId != id) { keyPack = packId; keyId = id; key = "skill." + packId + ".node." + id; }
            return key;
        }
    }

    [Serializable]
    public class SkillPack
    {
        [Tooltip("Unique id (used for the XP counter 'skill.<id>.xp'; don't change it once players have XP).")]
        public string id = "";
        public string displayName = "";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Colour of the pack's card and progress bar.")]
        public Color accent = Color.white;
        [Tooltip("The pixels this pack takes out of the rotation (shown in the window).")]
        public string replaces = "";
        [Tooltip("The pixels this pack puts in.")]
        public string spawns = "";
        [Tooltip("What earns XP.")]
        public string xpFrom = "";
        [Tooltip("Shown on the locked card.")]
        public string unlockHint = "";
        [Tooltip("XP needed for level 2.")]
        public float baseXp = 100f;
        [Tooltip("Each following level needs this many times more XP than the one before.")]
        public float xpGrowth = 1.6f;
        [Min(0)]
        [Tooltip("The level this skill counts as maxed at for Mastery. 0 = the default cap on the Pixel Skills component.")]
        public int levelCap = 0;
        [Tooltip("The pixel types this pack puts in. The FIRST one must be unlocked (bought in the Shop) for the pack to be available. They only spawn while the pack is in play, unless turned on as guests.")]
        public PixelClicker.PixelType[] types = new PixelClicker.PixelType[0];
        [Tooltip("Name of this pack's FILLER block (a pixel type, e.g. Dirt for Farming, Rock = Stone for Mining): a 'useless' base block that makes up a large share of the spawns at low skill level and fades as the skill levels up. Empty = none.")]
        public string fillerType = "";
        [Tooltip("More FILLER blocks of this pack, as a comma separated list of pixel type names (Woodcutting: Pinecone,Moss). They share the filler's share of the spawns evenly with the main filler above (when the roll lands on the main filler, one of the family is picked). They must also be in the pack's Types list.")]
        public string extraFillerTypes = "";
        [Range(0f, 0.95f)]
        [Tooltip("The filler's share of all spawns at level 1. 0 = the default (0.6).")]
        public float fillerStartShare = 0f;
        [Range(0f, 0.95f)]
        [Tooltip("The filler's share of all spawns at the fade level and above. 0 = the default (0.1).")]
        public float fillerEndShare = 0f;
        [Min(0)]
        [Tooltip("The skill level where the filler's share reaches its end value. 0 = the default (20).")]
        public int fillerFadeLevel = 0;
        [Tooltip("Name of this pack's HAZARD block (Pest for Farming, GasPocket for Mining): hit it too many times and it goes off (kills every sprout / explodes) and the skill earns less XP for a minute. Empty = none.")]
        public string hazardType = "";
        [Range(0f, 0.5f)]
        [Tooltip("The hazard's share of all spawns at level 1. 0 = the default (0.03).")]
        public float hazardBaseShare = 0f;
        [Range(0f, 0.05f)]
        [Tooltip("How much its share grows with every skill level. 0 = the default (0.002).")]
        public float hazardPerLevelShare = 0f;
        [Range(0f, 0.5f)]
        [Tooltip("The most its share can reach. 0 = the default (0.08).")]
        public float hazardMaxShare = 0f;
        [Tooltip("A truly random rotation: every block of this pack (filler and hazards included) is equally likely, with no filler / hazard shares and no spawn weights - a pure reaction check (Foraging).")]
        public bool uniformRotation = false;
        public Perk[] perks = new Perk[0];
        [Tooltip("The skill tree: nodes you buy with skill points (one point per level above 1).")]
        public TreeNode[] nodes = new TreeNode[0];

        // The XP counter's name 'skill.<id>.xp', made once (read several times per click); rebuilt if the id changes.
        [NonSerialized] private string xpKey, xpKeyId;
        public string XpKey
        {
            get
            {
                if (xpKey == null || xpKeyId != id) { xpKeyId = id; xpKey = "skill." + id + ".xp"; }
                return xpKey;
            }
        }

        // The last level worked out (LevelOf): kept while the XP and the curve are the same (it is asked for several times per click and every frame).
        [NonSerialized] public double levelXp = -1d, levelBase, levelInto, levelNeed;
        [NonSerialized] public float levelGrowth;
        [NonSerialized] public int level;
    }

    public const string StandardId = "standard";
    private const string WoodChipperPerkText = "Wood Chipper: the Lumberjack's wood chipper goes on sale in the Shop";
    private const string WoodChipperNotice = "Woodcutting level 2! The <b>Wood Chipper</b> is now for sale in the Shop. Open the Shop and look under Consumables, or just type <b>chipper</b> into the search bar at the top of the Shop: searching finds every pack, potion and device in one go.";
    private const string WoodGuestPerkText = "Lumber stock: Wood can be switched on in the Toggles window to spawn in every rotation";
    private const string WoodGuestNotice = "Woodcutting level 5! <b>Wood</b> can now be switched on in the <b>Toggles</b> window (listed as Wood (Woodcutting)), so wood pixels also spawn when you play other rotations.";
    private const string WoodLifespanPerkText = "Seasoned wood: old wood pixels last longer before they vanish";
    private const string WoodLifespanNotice = "Woodcutting level 3! Old wood pixels now <b>last longer</b> before they vanish (the Seasoned wood perk), so more of them are still lying there when you collect or chip them.";

    private const string ConveyorPerkText = "Conveyor belts: the Conveyor Belt Kit goes on sale in the Shop";
    private const string ConveyorNotice = "Electrician level 5! The Conveyor Belt Kit is on sale in the Shop (Utilities).";
    private const string ConveyorWidePerkText = "Wide belts: conveyor belts are two cubes wide";
    private const string ConveyorWideNotice = "Electrician level 10! Every conveyor belt is now two cubes wide.";
    private const string ConveyorWidestPerkText = "Wider belts: conveyor belts are three cubes wide";
    private const string ConveyorWidestNotice = "Electrician level 20! Every conveyor belt is now three cubes wide.";
    private const string StandardDescription = "The usual rotation: the six monochrome and RGB pixels (Glass and Luminescent join in once bought, and have their own switches in Toggles). Standard is a skill too: it levels as you click and its perks - like longer-lasting husks - help in every rotation. Every block you collect gives it 1 XP, whatever skill you train.";
    private const string ForagingDescription = "Swap the basic cubes for foraged goods: berries, mushrooms, herbs, rare truffles and useless weeds - in a TRULY RANDOM rotation (every block equally likely). Two hazards keep you sharp: the Mold (one left click and it covers every husk on the field) and the Toadstool (one left click and its spores make the cube dodge your cursor). One right click clears either - a pure reaction check. Pick blocks for foraging XP.";
    private const string ForagingSpawns = "Weeds, Berry, Mushroom, Herb, Truffle, Mold, Toadstool (all equally likely)";
    private const string MiningDescription = "Swap the basic cubes for tough ores and plain Rock. Rock is a useless filler block that is most of what you see at first; as your mining level rises it gets rarer and the ores show up more. Copper, tin, iron, lead, zinc, nickel, silver and gold break in a few clicks; obsidian and bedrock take 15-20 clicks but pay a lot. Break ores for mining XP; some are rich veins that pay big.";
    private const string MiningSpawns = "Rock, Copper, Tin, Iron, Lead, Zinc, Nickel, Silver, Gold, Obsidian, Bedrock";

    private static PixelClicker.PixelType[] MiningTypes() => new[]
    {
        PixelClicker.PixelType.Copper, PixelClicker.PixelType.Tin, PixelClicker.PixelType.Iron, PixelClicker.PixelType.Lead, PixelClicker.PixelType.Zinc, PixelClicker.PixelType.Nickel,
        PixelClicker.PixelType.Silver, PixelClicker.PixelType.Gold, PixelClicker.PixelType.Obsidian, PixelClicker.PixelType.Bedrock, PixelClicker.PixelType.Rock, PixelClicker.PixelType.GasPocket
    };

    private static PixelClicker.PixelType[] FiremakingTypes() => new[]
    {
        PixelClicker.PixelType.Tinder, PixelClicker.PixelType.Kindling, PixelClicker.PixelType.Firewood, PixelClicker.PixelType.Flint, PixelClicker.PixelType.Ember,
        PixelClicker.PixelType.Fire, PixelClicker.PixelType.Ash, PixelClicker.PixelType.Wildfire   // (Charcoal left 2026-10-24: it is its own shop pixel, bought with Kiln charcoal)
    };

    public const string MiningId = "mining";
    public const string WoodcuttingId = "woodcutting";
    public const string ForagingId = "foraging";
    public const string FishingId = "fishing";
    public const string OfflineId = "offline";
    public const string FarmingId = "farming";
    public const string ElectricianId = "electrician";
    public const string FiremakingId = "firemaking";

    [Header("Skill switch block (test)")]
    [Tooltip("Tick to turn the test off: choosing a skill normally swaps the block on the cube for a Glass pixel at once (a 'skill switch' block).")]
    [SerializeField] private bool disableSkillSwitchBlock;

    [Header("Skill Packs")]
    [Tooltip("The packs, in the order they are listed. Missing built-in ones (by id) are added automatically.")]
    [SerializeField] private List<SkillPack> packs = new List<SkillPack>();

    [Tooltip("Share of the XP an automatic (auto clicker) click gives compared with a manual click.")]
    [SerializeField] private float autoXpFactor = 0.25f;

    [Tooltip("Turn the whole skill system (and its Skills tab) off.")]
    [SerializeField] private bool disableSkills = false;

    private PixelClicker clicker;
    private PixelAutoClicker autoClicker;
    private PixelShop shop;

    public static PixelSkills Instance { get; private set; }
    public IReadOnlyList<SkillPack> Packs => packs;

    /// <summary>Raised when the active pack changes or a level is gained.</summary>
    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        penaltyLevels.Clear(); penaltyEnds.Clear(); Instance = null;
        PixelStatics.ResetOnRestart(OnSceneUnloaded);   // restart-reset-not-progress: hazard penalties of the old game (never saved)
    }

    // (Restart reloads the scene but statics survive it: a hazard penalty of the old game must not run on in the new one)
    private static void OnSceneUnloaded() { penaltyLevels.Clear(); penaltyEnds.Clear(); }

    private float bundleCheckTimer;

    /// <summary>Safety net: the Farmer's Pack gives every farm block at once, so if the first one is unlocked (an old save, a half-applied purchase) the rest are unlocked too.</summary>
    private void CheckFarmBundle()
    {
        bundleCheckTimer -= Time.unscaledDeltaTime;
        if (bundleCheckTimer > 0f || clicker == null) return;
        bundleCheckTimer = 3f;
        foreach (SkillPack p in packs)
        {
            if (p.types == null || p.types.Length == 0) continue;
            if (p.id == ElectricianId)
            {
                if (shop == null) shop = PixelFind.First<PixelShop>();
                if (shop == null || !shop.IsSkillPackBought(ElectricianId)) continue;   // bought: every one of its pixels is unlocked
            }
            else if ((p.id != FarmingId && p.id != MiningId && p.id != WoodcuttingId && p.id != ForagingId && p.id != FishingId && p.id != FiremakingId) || !clicker.IsUnlocked(p.types[0])) continue;   // (Mining: the first ore unlocked brings Stone, its filler, too)
            foreach (PixelClicker.PixelType t in p.types)
            {
                int i = clicker.IndexOf(t);
                if (i >= 0 && !clicker.Tiers[i].unlocked) clicker.UnlockTier(i);
            }
        }
    }

    private void Awake()
    {
        Instance = this;
        PixelGameState.Loaded += OnLoaded;
        EnsureDefaults();
        ApplyDialogueFile();
        // Perk popups and the skills tutorial take their turn in the one tip queue (PixelTipQueue).
        PixelTipQueue.Register(this, PixelTipQueue.Priority.PerkPopup, "Perk popups", () => perkNotices.Count > 0 && PixelHints.TipsEnabled && PixelCalm.ForTip, ShowNextPerkNotice);
        PixelTipQueue.Register(this, PixelTipQueue.Priority.Tutorial, "Skills tutorial", () => tutorialWanted && !tutorialRunning, () => { tutorialWanted = false; StartTutorial(); });
    }

    /// <summary>The user's own texts from Assets/Resources/Dialogue.txt (PixelDialogue): perk popups, the skills tutorial and the Woodcutting guide.</summary>
    private void ApplyDialogueFile()
    {
        PixelTextFile f = PixelDialogue.File;
        foreach (SkillPack p in packs)
        {
            if (p == null || p.perks == null) continue;
            foreach (Perk perk in p.perks)
                if (perk != null && !string.IsNullOrEmpty(perk.noticeText)) f.Apply("Perk popup: " + p.id + " level " + perk.level, "text", ref perk.noticeText);
        }
        if (tutorialSteps == null || tutorialSteps.Count == 0) tutorialSteps = DefaultTutorialSteps();
        PixelDialogue.Steps("Tutorial: Skills", tutorialSteps, st => st.text, (st, t) => st.text = t);
        if (woodGuideSteps == null || woodGuideSteps.Count == 0) woodGuideSteps = DefaultWoodGuide();
        f.Apply("Guide: Woodcutting", "question", ref woodGuideQuestion);
        f.Apply("Guide: Woodcutting", "later", ref woodGuideLaterText);
        f.ApplyList("Guide: Woodcutting", "step", woodGuideSteps);
        f.Flush();
    }

    private void OnDestroy()
    {
        PixelTipQueue.Unregister(this);
        PixelGameState.Loaded -= OnLoaded;
        if (Instance == this) Instance = null;
        if (clicker != null) clicker.PixelCollected -= OnCollected;
    }

    private void Start()
    {
        clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) { enabled = false; return; }
        if (disableSkills) { enabled = false; return; }
        clicker.PixelCollected += OnCollected;
        BuildWindow();
    }

    /// <summary>Adds the built-in packs that are missing from the serialized list (by id).</summary>
    private void EnsureDefaults()
    {
        AddMissingGatheringPacks();
        AddMissingOtherPacks();
        foreach (SkillPack pack in packs) FixSavedPackContents(pack);

        // Texts saved in older scenes still say the skill packs replace Glass and Luminescent too.
        foreach (SkillPack pack in packs) FixSavedPackTexts(pack);

        perkWindowVersion = Mathf.Max(perkWindowVersion, 1);

        // Offline Progression is listed right under Standard.
        int offlineAt = packs.FindIndex(p => p.id == OfflineId);
        if (offlineAt > 1)
        {
            SkillPack offline = packs[offlineAt];
            packs.RemoveAt(offlineAt);
            packs.Insert(1, offline);
        }
    }

    // EnsureDefaults, step 1: the built-in packs up to Firemaking (Standard, Woodcutting, Foraging, Fishing, Firemaking), each added when missing.
    private void AddMissingGatheringPacks()
    {
        if (!packs.Exists(p => p.id == StandardId))
            packs.Insert(0, new SkillPack
            {
                id = StandardId, displayName = "Standard", accent = new Color(0.5f, 0.55f, 0.65f),
                baseXp = 600f, xpGrowth = 1.5f, perks = StandardPerks(),   // (level 2 at 600 x xpCurveScale: retuned 2026-10-17 for the Recycler + Husk Crush XP, see the Standard notes)
                description = StandardDescription,
                replaces = "-", spawns = "White, Gray, Black, Red, Green, Blue (+ Glass, Luminescent once bought)", xpFrom = "1 XP for every block you collect, whatever skill is trained",
            });
        if (!packs.Exists(p => p.id == WoodcuttingId))
            packs.Insert(Mathf.Min(packs.Count, 1), new SkillPack
            {
                id = WoodcuttingId, displayName = "Woodcutting", accent = new Color(0.62f, 0.42f, 0.24f),
                description = "Swap the basic cubes for wood blocks: wood, bark, sap, leaves and rare heartwood, plus three useless filler blocks (twigs, pinecones and moss) that are most of what you see at first; as your woodcutting level rises it gets rarer and the other blocks show up more. Watch out for the termite hive. Chop blocks for woodcutting XP: the simple first skill.",
                replaces = "Monochrome, RGB", spawns = "Twig, Wood, Bark, Sap, Leaf, Heartwood",
                xpFrom = "Collecting wood blocks",
                unlockHint = "Buy the Lumberjack's Pack in the Shop",
                baseXp = 60f, xpGrowth = 1.45f,
                types = new[] { PixelClicker.PixelType.Wood, PixelClicker.PixelType.Bark, PixelClicker.PixelType.Sap, PixelClicker.PixelType.Leaf,
                                PixelClicker.PixelType.Heartwood, PixelClicker.PixelType.Twig, PixelClicker.PixelType.Pinecone, PixelClicker.PixelType.Moss, PixelClicker.PixelType.HornetNest, PixelClicker.PixelType.GreatOak, PixelClicker.PixelType.Whetstone, PixelClicker.PixelType.Acorn, PixelClicker.PixelType.Pesticide },
                fillerType = "Twig", extraFillerTypes = "Pinecone,Moss", fillerStartShare = 0.75f, fillerEndShare = 0.25f, hazardType = "HornetNest", hazardBaseShare = 0.015f, hazardPerLevelShare = 0.001f,
                perks = new[]
                {
                    new Perk { level = 2, text = WoodChipperPerkText, implemented = true, noticeText = WoodChipperNotice },
                    new Perk { level = 3, text = WoodLifespanPerkText, implemented = true, noticeText = WoodLifespanNotice },
                    new Perk { level = 4, text = "Sap collector: sap shows up more often" },
                    new Perk { level = 5, text = WoodGuestPerkText, implemented = true, noticeText = WoodGuestNotice, guestType = "Wood" },
                    new Perk { level = 6, text = "Heart of the forest: heartwood shows up more often" },
                },
            });
        if (!packs.Exists(p => p.id == ForagingId))
            packs.Insert(Mathf.Min(packs.Count, 2), new SkillPack
            {
                id = ForagingId, displayName = "Foraging", accent = new Color(0.78f, 0.28f, 0.4f),
                description = ForagingDescription,
                replaces = "Monochrome, RGB", spawns = ForagingSpawns,
                xpFrom = "Collecting foraged blocks",
                unlockHint = "Buy the Forager's Pack in the Shop",
                baseXp = 60f, xpGrowth = 1.45f,
                types = new[] { PixelClicker.PixelType.Berry, PixelClicker.PixelType.Mushroom, PixelClicker.PixelType.Herb, PixelClicker.PixelType.Truffle,
                                PixelClicker.PixelType.Weeds, PixelClicker.PixelType.Mold, PixelClicker.PixelType.Toadstool },
                fillerType = "Weeds", hazardType = "Mold,Toadstool", uniformRotation = true,
                perks = new[]
                {
                    new Perk { level = 2, text = "Keen eyes: berries show up more often" },
                    new Perk { level = 4, text = "Mushroom lore: mushrooms show up more often" },
                    new Perk { level = 6, text = "Truffle nose: truffles show up more often" },
                },
            });
        if (!packs.Exists(p => p.id == FishingId))
            packs.Insert(Mathf.Min(packs.Count, 3), new SkillPack
            {
                id = FishingId, displayName = "Fishing", accent = new Color(0.3f, 0.6f, 0.9f),
                description = "Swap the basic cubes for fished goods: very common Water, seaweed, shells, rare pearls and rare fish. Click a fish and a Weak Spots round starts: hit every spot to catch it, miss and it gets away. Water is a plain filler that gets rarer as your fishing level rises. Watch out for the stinging jellyfish. A cheap early skill that leads on towards cooking.",
                replaces = "Monochrome, RGB", spawns = "Water, Fish, Seaweed, Shell, Pearl, Bait, Bottle, Whale",
                xpFrom = "Collecting fished blocks",
                unlockHint = "Buy the Angler's Pack in the Shop",
                baseXp = 60f, xpGrowth = 1.45f,
                types = new[] { PixelClicker.PixelType.Fish, PixelClicker.PixelType.Seaweed, PixelClicker.PixelType.Shell, PixelClicker.PixelType.Pearl,
                                PixelClicker.PixelType.Water, PixelClicker.PixelType.Driftwood, PixelClicker.PixelType.Bait, PixelClicker.PixelType.Jellyfish,
                                PixelClicker.PixelType.Bottle, PixelClicker.PixelType.Whale },
                fillerType = "Water", hazardType = "Jellyfish", fillerStartShare = 0.75f, fillerEndShare = 0.5f,
                perks = new[]
                {
                    new Perk { level = 2, text = "Good bait: fish show up more often" },
                    new Perk { level = 4, text = "Shell collector: shells show up more often" },
                    new Perk { level = 6, text = "Pearl diver: pearls show up more often" },
                },
            });
        if (!packs.Exists(p => p.id == FiremakingId))
            packs.Add(new SkillPack
            {
                id = FiremakingId, displayName = "Firemaking", accent = new Color(0.95f, 0.45f, 0.12f),
                description = "TEST SKILL (Dev Tools only for now; its mechanics are still to come): swap the basic cubes for fire goods - tinder, kindling, firewood, flint, embers and flames - with plain ash as the filler. Watch out for the wildfire hazard.",
                replaces = "Monochrome, RGB", spawns = "Ash, Tinder, Kindling, Firewood, Flint, Ember, Fire", xpFrom = "Collecting fire blocks",
                unlockHint = "Dev Tools only (Shop unlocks: Firemaker's Pack)",
                baseXp = 80f, xpGrowth = 1.5f,
                types = FiremakingTypes(),
                fillerType = "Ash", hazardType = "Wildfire",
                perks = new[] { new Perk { level = 2, text = "Coming soon" } },
            });
    }

    // EnsureDefaults, step 2: the rest of the built-in packs (Mining, Farming, Electrician, Offline Progression), each added when missing.
    private void AddMissingOtherPacks()
    {
        if (!packs.Exists(p => p.id == MiningId))
            packs.Add(new SkillPack
            {
                id = MiningId, displayName = "Mining", accent = new Color(0.86f, 0.46f, 0.26f),
                description = MiningDescription,
                replaces = "Monochrome, RGB", spawns = MiningSpawns, xpFrom = "Breaking ore pixels",
                unlockHint = "Buy Prospector's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = MiningTypes(),
                fillerType = "Rock", hazardType = "GasPocket",
                perks = new[]
                {
                    new Perk { level = 2, text = "Richer veins: a higher chance of a rich vein" },
                    new Perk { level = 4, text = "Steady hands: ores take one click less" },
                    new Perk { level = 6, text = "Prospector's eye: rich veins glitter from further away" },
                    new Perk { level = 8, text = "Motherlode: rich veins pay more" },
                    new Perk { level = 10, text = "Mastery: a small permanent bonus to every pixel" },
                },
            });
        if (!packs.Exists(p => p.id == FarmingId))
            packs.Insert(Mathf.Min(packs.Count, 2), new SkillPack
            {
                id = FarmingId, displayName = "Farming", accent = new Color(0.45f, 0.78f, 0.3f),
                description = "Swap the basic cubes for eight farm blocks: dirt, seeds, water, fertilizer, pollen, wheat, weevils and golden fruit. Dirt is a useless filler block that is most of what you see at first; as your farming level rises it gets rarer and the other blocks show up more. Grow sprouts for farming XP.",
                replaces = "Monochrome, RGB", spawns = "Dirt, Seed, Water, Fertilizer, Pollen, Wheat, Weevil, Golden Fruit",
                xpFrom = "Collecting farm blocks and growing sprouts",
                unlockHint = "Buy Farmer's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = new[] { PixelClicker.PixelType.Dirt, PixelClicker.PixelType.Seed, PixelClicker.PixelType.Water, PixelClicker.PixelType.Fertilizer,
                                PixelClicker.PixelType.Pollen, PixelClicker.PixelType.Wheat, PixelClicker.PixelType.Weevil, PixelClicker.PixelType.GoldenFruit, PixelClicker.PixelType.Pest },
                fillerType = "Dirt", hazardType = "Pest",
                perks = new[]
                {
                    new Perk { level = 2, text = "Green thumb: sprouts grow a little faster" },
                    new Perk { level = 5, text = "Bumper crop: sprouts pay a little more" },
                },
            });
        if (!packs.Exists(p => p.id == ElectricianId))
            packs.Insert(Mathf.Min(packs.Count, 3), new SkillPack
            {
                id = ElectricianId, displayName = "Electrician", accent = new Color(0.35f, 0.7f, 1f),
                description = "Swap the basic cubes for eight electrical blocks: Electric, Wire, Battery, Capacitor, Resistor, Transistor, Fuse and Bulb. Wire, Battery and Resistor link up to power your devices, a Battery sets off an Overcharge, a Capacitor stores charge from every other click, a Transistor doubles your next click, a Fuse pays big if you click it fast and a Bulb shines brighter the more wired pixels lie around.",
                replaces = "Monochrome, RGB", spawns = "Electric, Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb",
                xpFrom = "Collecting electrical pixels",
                unlockHint = "Buy the Electrician's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = new[] { PixelClicker.PixelType.Electric, PixelClicker.PixelType.Wire, PixelClicker.PixelType.Battery, PixelClicker.PixelType.Capacitor, PixelClicker.PixelType.Resistor, PixelClicker.PixelType.Transistor, PixelClicker.PixelType.Fuse, PixelClicker.PixelType.Bulb },
                perks = new[]
                {
                    new Perk { level = 2, text = "Overcharge+: Overcharge bursts add more clicks" },
                    new Perk { level = 4, text = "Wired up: device links reach further" },
                    new Perk { level = 6, text = "Power bank: the Electric pet holds more charge" },
                    new Perk { level = 8, text = "Surge: a rare click pays much more" },
                },
            });
        if (!packs.Exists(p => p.id == OfflineId))
            packs.Add(new SkillPack
            {
                id = OfflineId, displayName = "Offline Progression", accent = new Color(0.62f, 0.45f, 0.9f),
                description = "A PASSIVE skill: it never trains while you play. Pick what your character does while the game is closed: your Auto Clicker keeps working (offline progress), every offline click gives Offline Progression XP and a little XP to the skill you picked, shown in the Welcome back window. Its levels will make your time away pay more.",
                replaces = "Nothing (passive)", spawns = "Nothing (passive)", xpFrom = "Offline progress only: each offline Auto Clicker click while the game is closed",
                unlockHint = "Buy the Auto Clicker in the Shop",
                baseXp = 150f, xpGrowth = 1.6f,
                perks = new[]
                {
                    new Perk { level = 2, text = "Efficient rest: a higher share of earnings while away" },
                    new Perk { level = 5, text = "Long sleeper: a longer time away counts" },
                },
            });
    }

    // EnsureDefaults, step 3: a saved pack gets the blocks, fillers, hazards, perks and tree nodes added since it was saved.
    private void FixSavedPackContents(SkillPack pack)
    {
        if (pack.id == FiremakingId && pack.types != null && System.Array.IndexOf(pack.types, PixelClicker.PixelType.Charcoal) >= 0)   // Charcoal is its own shop pixel now (2026-10-24)
        {
            pack.types = System.Array.FindAll(pack.types, t => t != PixelClicker.PixelType.Charcoal);
            if (pack.spawns != null) pack.spawns = pack.spawns.Replace(", Charcoal", "");
            if (pack.description != null) pack.description = pack.description.Replace("flint, embers, flames and charcoal", "flint, embers and flames");
        }
        if (pack.id == ElectricianId && (pack.types == null || pack.types.Length < 8))   // a saved pack from when Electric was its only pixel
        {
            pack.types = new[] { PixelClicker.PixelType.Electric, PixelClicker.PixelType.Wire, PixelClicker.PixelType.Battery, PixelClicker.PixelType.Capacitor, PixelClicker.PixelType.Resistor, PixelClicker.PixelType.Transistor, PixelClicker.PixelType.Fuse, PixelClicker.PixelType.Bulb };
            pack.spawns = "Electric, Wire, Battery, Capacitor, Resistor, Transistor, Fuse, Bulb";
            pack.xpFrom = "Collecting electrical pixels";
        }
        if (pack.id == MiningId && (pack.types == null || pack.types.Length == 0))
            pack.types = new[] { PixelClicker.PixelType.Copper, PixelClicker.PixelType.Tin, PixelClicker.PixelType.Iron, PixelClicker.PixelType.Lead, PixelClicker.PixelType.Zinc, PixelClicker.PixelType.Nickel };
        // Stone (the old Rock block) is Mining's filler; Dirt is Farming's.
        if (pack.id == MiningId)
        {
            if (System.Array.IndexOf(pack.types, PixelClicker.PixelType.Rock) < 0)
            {
                System.Collections.Generic.List<PixelClicker.PixelType> withStone = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types) { PixelClicker.PixelType.Rock };
                pack.types = withStone.ToArray();
            }
            if (string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Rock.ToString();
            if (pack.spawns != null && !pack.spawns.Contains("Rock")) pack.spawns = "Rock, " + pack.spawns;
        }
        if (pack.id == FarmingId && string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Dirt.ToString();
        if (pack.id == FarmingId)
        {
            // A saved Farming pack could lack a farm block (Seed and Water used to be sold on their own): make sure every one is in the rotation.
            System.Collections.Generic.List<PixelClicker.PixelType> farm = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Dirt, PixelClicker.PixelType.Seed, PixelClicker.PixelType.Water, PixelClicker.PixelType.Fertilizer,
                                                         PixelClicker.PixelType.Pollen, PixelClicker.PixelType.Wheat, PixelClicker.PixelType.Weevil, PixelClicker.PixelType.GoldenFruit })
                if (!farm.Contains(t)) farm.Add(t);
            pack.types = farm.ToArray();
        }
        // Hazard blocks: Farming's Pest, Mining's Gas Pocket and Woodcutting's Hornet Nest (part of their packs' pixel lists).
        if (pack.id == WoodcuttingId)
        {
            // A saved Woodcutting pack could lack a block: make sure every one is in the rotation.
            System.Collections.Generic.List<PixelClicker.PixelType> wood = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Wood, PixelClicker.PixelType.Bark, PixelClicker.PixelType.Sap, PixelClicker.PixelType.Leaf,
                                                         PixelClicker.PixelType.Heartwood, PixelClicker.PixelType.Twig, PixelClicker.PixelType.Pinecone, PixelClicker.PixelType.Moss, PixelClicker.PixelType.HornetNest, PixelClicker.PixelType.GreatOak, PixelClicker.PixelType.Whetstone, PixelClicker.PixelType.Acorn, PixelClicker.PixelType.Pesticide })
                if (!wood.Contains(t)) wood.Add(t);
            pack.types = wood.ToArray();
            if (string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Twig.ToString();
            if (string.IsNullOrEmpty(pack.extraFillerTypes)) pack.extraFillerTypes = "Pinecone,Moss";   // the filler family
            if (pack.fillerStartShare <= 0f && pack.fillerEndShare <= 0f) { pack.fillerStartShare = 0.75f; pack.fillerEndShare = 0.25f; }   // the fillers show up a little more (was 60% -> 10%)
            if (string.IsNullOrEmpty(pack.hazardType)) pack.hazardType = PixelClicker.PixelType.HornetNest.ToString();
            if (pack.hazardBaseShare <= 0f && pack.hazardPerLevelShare <= 0f) { pack.hazardBaseShare = 0.015f; pack.hazardPerLevelShare = 0.001f; }   // the Termite Hive: half as common as before (3% + 0.2%/level)
            // Perks: the old 'Sharp axe' level 2 perk becomes the Wood Chipper unlock; level 3 is new (a saved pack keeps any text you edited).
            System.Collections.Generic.List<Perk> woodPerks = new System.Collections.Generic.List<Perk>(pack.perks ?? new Perk[0]);
            woodPerks.RemoveAll(pk => pk != null && pk.text != null && pk.text.StartsWith("Sharp axe"));
            if (!woodPerks.Exists(pk => pk != null && pk.level == 2)) woodPerks.Add(new Perk { level = 2, text = WoodChipperPerkText, implemented = true, noticeText = WoodChipperNotice });
            if (!woodPerks.Exists(pk => pk != null && pk.level == 3)) woodPerks.Add(new Perk { level = 3, text = WoodLifespanPerkText, implemented = true, noticeText = WoodLifespanNotice });
            foreach (Perk pk in woodPerks) if (pk != null && pk.level == 2 && pk.text == WoodChipperPerkText) { pk.implemented = true; if (string.IsNullOrEmpty(pk.noticeText)) pk.noticeText = WoodChipperNotice; }
            foreach (Perk pk in woodPerks) if (pk != null && pk.level == 3 && pk.text == WoodLifespanPerkText) { pk.implemented = true; if (string.IsNullOrEmpty(pk.noticeText)) pk.noticeText = WoodLifespanNotice; }
            if (!woodPerks.Exists(pk => pk != null && pk.guestType == "Wood")) woodPerks.Add(new Perk { level = 5, text = WoodGuestPerkText, implemented = true, noticeText = WoodGuestNotice, guestType = "Wood" });
            woodPerks.Sort((a, b) => a.level.CompareTo(b.level));
            pack.perks = woodPerks.ToArray();
        }
        if (pack.id == ElectricianId)
        {
            // Level 5: the Conveyor Belt Kit goes on sale in the shop (PixelConsumables DeviceKind.ConveyorBelt, shopSkillId electrician / level 5).
            System.Collections.Generic.List<Perk> elPerks = new System.Collections.Generic.List<Perk>(pack.perks ?? new Perk[0]);
            bool changedPerks = false;
            if (!elPerks.Exists(pk => pk != null && pk.level == 5)) { elPerks.Add(new Perk { level = 5, text = ConveyorPerkText, implemented = true, noticeText = ConveyorNotice }); changedPerks = true; }
            if (!elPerks.Exists(pk => pk != null && pk.level == 10)) { elPerks.Add(new Perk { level = 10, text = ConveyorWidePerkText, implemented = true, noticeText = ConveyorWideNotice }); changedPerks = true; }
            if (!elPerks.Exists(pk => pk != null && pk.level == 20)) { elPerks.Add(new Perk { level = 20, text = ConveyorWidestPerkText, implemented = true, noticeText = ConveyorWidestNotice }); changedPerks = true; }
            if (changedPerks)
            {
                elPerks.Sort((a, b) => a.level.CompareTo(b.level));
                pack.perks = elPerks.ToArray();
            }
        }
        if (pack.id == FishingId)
        {
            System.Collections.Generic.List<PixelClicker.PixelType> fish = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Fish, PixelClicker.PixelType.Seaweed, PixelClicker.PixelType.Shell,
                                                         PixelClicker.PixelType.Pearl, PixelClicker.PixelType.Water, PixelClicker.PixelType.Driftwood, PixelClicker.PixelType.Bait, PixelClicker.PixelType.Jellyfish,
                                                         PixelClicker.PixelType.Bottle, PixelClicker.PixelType.Whale })
                if (!fish.Contains(t)) fish.Add(t);
            pack.types = fish.ToArray();
            if (string.IsNullOrEmpty(pack.fillerType) || pack.fillerType == PixelClicker.PixelType.Driftwood.ToString())
            {
                pack.fillerType = PixelClicker.PixelType.Water.ToString();
                pack.fillerStartShare = 0.75f; pack.fillerEndShare = 0.5f;
                pack.spawns = "Water, Fish, Seaweed, Shell, Pearl";
                pack.description = "Swap the basic cubes for fished goods: very common Water, seaweed, shells, rare pearls and rare fish. Click a fish and a Weak Spots round starts: hit every spot to catch it, miss and it gets away. Water is a plain filler that gets rarer as your fishing level rises. Watch out for the stinging jellyfish. A cheap early skill that leads on towards cooking.";
            }
            if (string.IsNullOrEmpty(pack.hazardType)) pack.hazardType = PixelClicker.PixelType.Jellyfish.ToString();
        }
        if (pack.id == MiningId && pack.types != null)
        {
            // Every ore is in: Lead / Zinc / Nickel are back alongside Silver / Gold / Obsidian / Bedrock; the filler is called Rock.
            System.Collections.Generic.List<PixelClicker.PixelType> mine = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types);
            mine.RemoveAll(PixelClicker.IsRetiredOre);
            foreach (PixelClicker.PixelType t in MiningTypes())
                if (!mine.Contains(t)) mine.Add(t);
            pack.types = mine.ToArray();
            pack.spawns = MiningSpawns;
            pack.description = MiningDescription;
        }
        if (pack.id == ForagingId)
        {
            System.Collections.Generic.List<PixelClicker.PixelType> forage = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types ?? new PixelClicker.PixelType[0]);
            foreach (PixelClicker.PixelType t in new[] { PixelClicker.PixelType.Berry, PixelClicker.PixelType.Mushroom, PixelClicker.PixelType.Herb,
                                                         PixelClicker.PixelType.Truffle, PixelClicker.PixelType.Weeds, PixelClicker.PixelType.Mold, PixelClicker.PixelType.Toadstool })
                if (!forage.Contains(t)) forage.Add(t);   // (the Toadstool is back beside the Mold)
            pack.types = forage.ToArray();
            if (string.IsNullOrEmpty(pack.fillerType)) pack.fillerType = PixelClicker.PixelType.Weeds.ToString();
            pack.hazardType = "Mold,Toadstool";
            pack.uniformRotation = true;   // a truly random rotation: a pure reaction check
            pack.description = ForagingDescription;
            pack.spawns = ForagingSpawns;
        }
        if (pack.id == FiremakingId && (pack.types == null || pack.types.Length == 0)) pack.types = FiremakingTypes();
        if (pack.id == MiningId || pack.id == FarmingId)
        {
            PixelClicker.PixelType hazard = pack.id == MiningId ? PixelClicker.PixelType.GasPocket : PixelClicker.PixelType.Pest;
            if (pack.types != null && System.Array.IndexOf(pack.types, hazard) < 0)
            {
                System.Collections.Generic.List<PixelClicker.PixelType> withHazard = new System.Collections.Generic.List<PixelClicker.PixelType>(pack.types) { hazard };
                pack.types = withHazard.ToArray();
            }
            if (string.IsNullOrEmpty(pack.hazardType)) pack.hazardType = hazard.ToString();
        }
        // Built-in trees: add any default node the saved pack is missing (ones you edited are kept).
        if (pack.id != MiningId && pack.id != OfflineId && pack.id != FarmingId && pack.id != ElectricianId && pack.id != WoodcuttingId && pack.id != StandardId) return;
        System.Collections.Generic.List<TreeNode> merged = new System.Collections.Generic.List<TreeNode>(pack.nodes ?? new TreeNode[0]);
        foreach (TreeNode def in DefaultNodes(pack.id))
            if (!merged.Exists(n => n.id == def.id)) merged.Add(def);
            else if (def.id == "hold" && pack.id == StandardId) { TreeNode saved = merged.Find(n => n.id == def.id); if (saved.description != null && saved.description.Contains("Holding a basic block puts a stake")) saved.description = def.description; }   // (2026-10-16: right-hold is each block's second action)
        pack.nodes = merged.ToArray();
    }

    // EnsureDefaults, step 4: older wording in a saved pack (renamed packs, retuned XP, perk popups) is brought up to date.
    private void FixSavedPackTexts(SkillPack pack)
    {
        if (pack.unlockHint != null) pack.unlockHint = pack.unlockHint.Replace("Woodcutter's Pack", "Lumberjack's Pack");   // (renamed pack)
        if (pack.description != null) pack.description = pack.description.Replace("Woodcutter's Pack", "Lumberjack's Pack");
        if (pack.replaces != null) pack.replaces = pack.replaces.Replace("Monochrome, RGB, Glass, Luminescent", "Monochrome, RGB");
        if (pack.id == OfflineId)
        {
            if (pack.description != null && (pack.description.StartsWith("Pick this skill") || pack.description.StartsWith("A PASSIVE skill: it never trains while you play. While")))   // (2026-10-16: offline XP only comes from offline progress)
            { pack.description = "A PASSIVE skill: it never trains while you play. Pick what your character does while the game is closed: your Auto Clicker keeps working (offline progress), every offline click gives Offline Progression XP and a little XP to the skill you picked, shown in the Welcome back window. Its levels will make your time away pay more."; pack.replaces = "Nothing (passive)"; pack.spawns = "Nothing (passive)"; pack.xpFrom = "Offline progress only: each offline Auto Clicker click while the game is closed"; }
            if (pack.description != null && pack.description.Contains("You earn no XP for that pack"))
                pack.description = "Pick this skill, then pick which pack's pixels you play with. Every click trains Offline Progression, which will improve what you earn while you are away. The pack you assign still earns 50% of its normal XP. With no skill pack assigned (Standard), Offline Progression earns +50% XP.";
            if (pack.xpFrom == "Every click, whichever pack is in play")
                pack.xpFrom = "Every click (the assigned pack earns 50%; +50% for itself with no pack assigned)";
        }
        if (pack.id == StandardId)
        {
            if (pack.perks == null || pack.perks.Length == 0) pack.perks = StandardPerks();
            if (Mathf.Approximately(pack.baseXp, 80f)) pack.baseXp = 380f;
            if (Mathf.Approximately(pack.baseXp, 380f)) pack.baseXp = 600f;   // (2026-10-17: the Recycler's streak XP + Husk Crush levelled Standard ~2x too fast - level 4 at 8:21 in the 10:50 report)
            if (pack.perks != null && !System.Array.Exists(pack.perks, pk => pk != null && pk.text != null && pk.text.StartsWith("Steady grip")))
            {
                // The grip perks (Block Hold starts at half length; level 3 and level 5 each add 10% of it): added to a saved Standard pack, kept in level order.
                System.Collections.Generic.List<Perk> withGrip = new System.Collections.Generic.List<Perk>(pack.perks);
                foreach (Perk def in StandardPerks())
                    if (def.text.StartsWith("Steady grip") || def.text.StartsWith("Firm grip")) withGrip.Add(def);
                withGrip.Sort((x, y) => (x != null ? x.level : 0).CompareTo(y != null ? y.level : 0));
                pack.perks = withGrip.ToArray();
            }   // level 2 now takes the 380 XP level 4 used to (the whole curve x4.75, growth stays 1.5)
            if (pack.perks != null)
            {
                // Perks added later (2026-10-18: the crush refund perks, Double floor): any missing one is added to a saved Standard pack, kept in level order.
                foreach (Perk pk in pack.perks)   // (the first wording of Double floor, 2026-10-18, was about the husk limit)
                    if (pk != null && pk.text == "Double floor: the floor holds twice as many husks") pk.text = DoubleFloorPerk;
                System.Collections.Generic.List<Perk> withLater = new System.Collections.Generic.List<Perk>(pack.perks);
                bool added = false;
                foreach (Perk def in StandardPerks())
                    if ((def.text == CrushPerkPower || def.text == CrushPerkGrip || def.text == CrushPerkRhythm || def.text == DoubleFloorPerk)
                        && !withLater.Exists(pk => pk != null && pk.text == def.text)) { withLater.Add(def); added = true; }
                if (added)
                {
                    withLater.Sort((x, y) => (x != null ? x.level : 0).CompareTo(y != null ? y.level : 0));
                    pack.perks = withLater.ToArray();
                }
            }
            if (pack.perks != null)   // the level 2 popup that introduces the skill
                foreach (Perk perk in pack.perks)
                    if (perk != null && perk.level == 2 && (string.IsNullOrEmpty(perk.noticeText) || perk.noticeText.Contains("Every pixel you click gives Standard XP") || perk.noticeText.Contains("every level unlocks a perk.\n\nYour first perk") || perk.noticeText.Contains("Later points take the other one and branch further.") || perk.noticeText.StartsWith("Standard level 2! Surprise"))) perk.noticeText = StandardLevel2Notice;
            if (pack.perks != null)   // the level 3 popup: the second point is a choice - the skipped move or Aimed Clicks (2026-10-17)
                foreach (Perk perk in pack.perks)
                    if (perk != null && perk.level == 3 && string.IsNullOrEmpty(perk.noticeText) && perk.text != null && perk.text.StartsWith("Steady grip")) { perk.noticeText = StandardLevel3Notice; perk.noticeInWindow = true; }
            if (perkWindowVersion < 1 && pack.perks != null)   // the level 2 popup opens the Skills window and frames the Standard card
                foreach (Perk perk in pack.perks)
                    if (perk != null && perk.level == 2) perk.noticeInWindow = true;
            if (pack.perks != null)
                foreach (Perk perk in pack.perks)
                    if (perk != null && perk.text != null && perk.text.Contains("last 2 seconds longer")) perk.text = perk.text.Replace("last 2 seconds longer", "last 5 seconds longer");
            if (pack.description != null && pack.description.Contains("No skill is trained")) pack.description = StandardDescription;
            if (pack.xpFrom == "Nothing") pack.xpFrom = "1 XP for every block you collect, whatever skill is trained";
            if (pack.description != null && pack.description.StartsWith("The usual rotation: monochrome, RGB, Glass and Luminescent"))
                pack.description = "The usual rotation: the six monochrome and RGB pixels (Glass and Luminescent join in once bought, and have their own switches in Toggles). No skill is trained.";
            if (pack.spawns == "White, Gray, Black, Red, Green, Blue, Glass, Luminescent")
                pack.spawns = "White, Gray, Black, Red, Green, Blue (+ Glass, Luminescent once bought)";
        }
    }

    private static TreeNode[] DefaultNodes(string id)
    {
        if (id == MiningId)
            return new[]
            {
                new TreeNode { id = "veins", displayName = "Richer veins", description = "A higher chance of a rich vein.", row = 1 },
                new TreeNode { id = "hands", displayName = "Steady hands", description = "Ores take one click less.", requires = "veins", row = 0 },
                new TreeNode { id = "eye", displayName = "Prospector's eye", description = "Rich veins glitter from further away.", requires = "veins", row = 2 },
                new TreeNode { id = "lode", displayName = "Motherlode", description = "Rich veins pay more.", requires = "hands", row = 0, cost = 2 },
                new TreeNode { id = "mastery", displayName = "Mastery", description = "A small permanent bonus to every pixel.", requires = "eye", row = 2, cost = 2 },
                new TreeNode { id = "iron", displayName = "Iron samples", description = "Iron Ore can be switched on in the Toggles window to spawn in any rotation.", requires = "lode", row = 0, cost = 2, implemented = true, guestType = "Iron" },
                new TreeNode { id = "copper", displayName = "Copper samples", description = "Copper Ore can be switched on in the Toggles window to spawn in any rotation.", requires = "mastery", row = 2, cost = 2, implemented = true, guestType = "Copper" },
            };
        if (id == FarmingId)
            return new[]
            {
                new TreeNode { id = "seeds", displayName = "Heirloom seeds", description = "Seed blocks can be switched on in the Toggles window to spawn in any rotation.", row = 1, implemented = true, guestType = "Seed" },
                new TreeNode { id = "rain", displayName = "Rain barrel", description = "Water blocks can be switched on in the Toggles window to spawn in any rotation.", requires = "seeds", row = 0, implemented = true, guestType = "Water" },
                new TreeNode { id = "compost", displayName = "Compost heap", description = "Fertilizer blocks can be switched on in the Toggles window to spawn in any rotation.", requires = "seeds", row = 2, implemented = true, guestType = "Fertilizer" },
                new TreeNode { id = "hive", displayName = "Beehive", description = "Pollen blocks can be switched on in the Toggles window to spawn in any rotation.", requires = "rain", row = 0, cost = 2, implemented = true, guestType = "Pollen" },
                new TreeNode { id = "thumb", displayName = "Green thumb", description = "Sprouts grow a little faster.", requires = "compost", row = 2, cost = 2 },
            };
        if (id == StandardId)
            return new[]
            {
                // Two ways to vary plain clicking - the first skill point picks one, later points can take the other and branch on.
                new TreeNode { id = "hold", displayName = "Block Hold", description = "Unlocks the right mouse button's HOLD on the cube's block: a second action that depends on the block. On the pure pixels (white, gray, black, red, green, blue) holding puts a stake on it - it may pay much more when it comes round again, or nothing. Skill blocks do their own job instead (Pesticide gives charges, the Whetstone rapid clicks...). The held block steps out of the rotation while a meter drains.", row = 0, cost = 1, implemented = true },
                new TreeNode { id = "lucky", displayName = "Lucky stake", description = "Stakes pay off 10% more often.", requires = "hold", row = 0, cost = 1, implemented = true },
                new TreeNode { id = "highroller", displayName = "High roller", description = "Stakes grow 50% faster while you hold.", requires = "lucky", row = 0, cost = 2, implemented = true },
                new TreeNode { id = "shuffle", displayName = "Shuffle Combo", description = "TAP right click on a skill's filler block to shuffle it behind the next block and start a combo: break the block it hid behind, then shuffle the filler again before the bar runs out to grow it. Break the filler to cash the combo in for bonus XP.", requires = "highroller", row = 0, cost = 1, implemented = true },
                new TreeNode { id = "power", displayName = "Power Click", description = "Hold the LEFT mouse button on the cube to charge, let go at full charge: one click worth 25 clicks. Long cooldown.", row = 2, cost = 1, implemented = true },
                new TreeNode { id = "quick", displayName = "Quick charge", description = "Power clicks charge 40% faster.", requires = "power", row = 2, cost = 1, implemented = true },
                new TreeNode { id = "aftershock", displayName = "Aftershock", description = "The power click cooldown is a third shorter.", requires = "quick", row = 2, cost = 2, implemented = true },
                // The third new way to click, the alternative to the second move at Standard 3 (row 1, column 0: between the two first moves). PixelBurstPop.AimingOwned.
                new TreeNode { id = AimedClicksNode, displayName = "Aimed Clicks", description = "Click near the EDGE of the cube and its husk flies out of that side (an arrow cursor shows where); a click in the middle still scatters it. Without this, every husk scatters at random round the cube. Aim husks at a Recycler, a crate or a clear spot to crush them.", row = 1, cost = 1, minLevel = 3, implemented = true },
                // Needs BOTH first nodes: the block predictor (PixelNextUp.requireTreeNode).
                new TreeNode { id = "nextup", displayName = "What's Next", description = "Shows the Next up window: the block coming next, the one on the cube now and the one before - plan your holds and power clicks ahead.", requires = "hold,power", row = 1, cost = 1, implemented = true },
                new TreeNode { id = "nextup2", displayName = "Look Further", description = "The Next up window shows one more block ahead (the next two).", requires = "nextup", row = 1, cost = 2, implemented = true },
                new TreeNode { id = "nextup3", displayName = "Foresight", description = "The Next up window shows one more block ahead (the next three).", requires = "nextup2", row = 1, cost = 2, implemented = true },
                // The husk branch (bottom row): crushing a husk (right-click) is free from the start; these build on it, beside the twists so those levels are real picks (PixelHuskCrush).
                new TreeNode { id = "chaincrush", displayName = "Chain Crush", description = "Crushing a husk (right-click it) also crushes the husks of the SAME type touching it, and the ones touching those.", row = 3, cost = 1, minLevel = 4, column = 1, implemented = true },
                new TreeNode { id = "cleanbreak", displayName = "Clean Break", description = "Every husk you crush gives the husks lying around it a few seconds of their time back before they fade.", requires = "chaincrush", row = 3, cost = 2, minLevel = 6, implemented = true },
                new TreeNode { id = "hardhands", displayName = "Hard Hands", description = "Crushing a husk gives more XP (still well below a click).", requires = "cleanbreak", row = 3, cost = 2, minLevel = 9, implemented = true },
                new TreeNode { id = "chargecrush", displayName = "Charge Crush", description = "HOLD the right mouse button on a husk: a ring grows round it the longer you hold. Let go and an arc jumps to every husk of the SAME type inside the ring - touching or not - and crushes them all. A quick click is still Chain Crush. Short cooldown after a charged crush.", requires = "chaincrush", row = 4, cost = 2, minLevel = 7, implemented = true },
            };
        if (id == WoodcuttingId)
            return new[]
            {
                // The first node of the Woodcutting tree: the axe. For now the axe is simply always available; this node is what the first person axe will be tied to later.
                new TreeNode { id = "axe", displayName = "Axe", description = "Unlock the axe to get the job done yourself.", row = 1, cost = 1, implemented = false },
            };
        if (id == ElectricianId)
            return new[]
            {
                new TreeNode { id = "static", displayName = "Static charge", description = "Electric blocks can be switched on in the Toggles window to spawn in any rotation.", row = 1, implemented = true, guestType = "Electric" },
                new TreeNode { id = "overcharge", displayName = "Overcharge+", description = "Overcharge bursts add more clicks.", requires = "static", row = 0 },
                new TreeNode { id = "grid", displayName = "Wired up", description = "Device links reach further and carry more pixels.", requires = "static", row = 2 },
                new TreeNode { id = "bank", displayName = "Power bank", description = "The Electric pet holds more charge and recharges faster.", requires = "overcharge", row = 0, cost = 2 },
                new TreeNode { id = "surge", displayName = "Surge", description = "A rare click pays much more.", requires = "grid", row = 2, cost = 2 },
            };
        return new[]
        {
            new TreeNode { id = "rest", displayName = "Efficient rest", description = "A higher share of your earnings counts while you are away.", row = 1 },
            new TreeNode { id = "sleep", displayName = "Long sleeper", description = "A longer time away counts.", requires = "rest", row = 0 },
            new TreeNode { id = "dream", displayName = "Dream work", description = "The pack you play with also earns a little while you are away.", requires = "rest", row = 2 },
            new TreeNode { id = "deep", displayName = "Deep rest", description = "A big boost to everything above.", requires = "sleep", row = 0, cost = 2 },
        };
    }

    // (a plain loop: Find runs many times per spawn roll and per frame, and List.Find with a lambda makes a new closure on every call)
    public SkillPack Find(string id)
    {
        for (int i = 0; i < packs.Count; i++) if (packs[i].id == id) return packs[i];
        return null;
    }

    /// <summary>Can the player train / use this pack yet?</summary>
    public bool IsAvailable(SkillPack pack)
    {
        if (pack == null) return false;
        if (pack.id == StandardId) return true;
        if (pack.id == OfflineId)
        {
            if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
            return autoClicker != null && autoClicker.Running;   // needs the Auto Clicker, like offline progress itself
        }
        if (pack.id == ElectricianId)   // bought as the Electrician's Pack (and needs the Electric pixel itself)
        {
            if (shop == null) shop = PixelFind.First<PixelShop>();
            return shop != null && shop.IsSkillPackBought(ElectricianId) && clicker != null && clicker.IsUnlocked(PixelClicker.PixelType.Electric);
        }
        if (pack.types != null && pack.types.Length > 0 && clicker != null) return clicker.IsUnlocked(pack.types[0]);   // bought in the Shop
        return false;
    }

    /// <summary>Is at least one pixel pack (Mining, Farming...) bought? The Toggles window shows its pack switches then.</summary>
    public bool AnyPackAvailable
    {
        get
        {
            foreach (SkillPack p in packs) if (p.id != StandardId && p.id != OfflineId && IsAvailable(p)) return true;
            return false;
        }
    }

    /// <summary>True once there is something besides Standard to choose (the Skills tab appears then).</summary>
    public bool AnyTrainable
    {
        get
        {
            if (disableSkills || clicker == null) return false;
            foreach (SkillPack p in packs) if (p.id != StandardId && IsAvailable(p)) return true;
            return false;
        }
    }

    // ---- progress: the skill choices live in the GameState (PixelGameState.Current.skills); XP / nodes / Mastery are its StatsState counters ----
    private static SkillsState S => PixelGameState.Current.skills;

    /// <summary>The pixel pack the player chose (progress in the GameState; '' / 'standard' = the usual rotation).</summary>
    public static string PackSetting
    {
        get => S.activePack;
        set => S.activePack = value ?? "";
    }

    // ---- Preview (PixelBalanceReport only): pretend a pack is in play at a skill level, with all of its pixels unlocked. Always set / cleared in a try-finally. ----

    /// <summary>Pack id the balance report pretends is in play (null = off). Read by ActiveId and PreviewForces.</summary>
    public static string PreviewPackId;

    /// <summary>Skill level the balance report pretends the previewed pack has (0 = the real level; used by the filler / hazard share).</summary>
    public static int PreviewLevel;

    /// <summary>True while the balance report previews a pack that owns this pixel type (so PixelTier.CanSpawn counts it as unlocked).</summary>
    public static bool PreviewForces(PixelClicker.PixelType type)
    {
        if (PreviewPackId == null || PreviewPackId == StandardId || Instance == null) return false;
        SkillPack p = Instance.Find(PreviewPackId);
        return p != null && p.types != null && Array.IndexOf(p.types, type) >= 0;
    }

    /// <summary>Share of the XP an automatic click gives compared with a manual click (autoXpFactor).</summary>
    public float AutoXpFactor => autoXpFactor;

    /// <summary>The level a skill counts as maxed at for Mastery (its levelCap, else the component's default).</summary>
    public int LevelCapOf(SkillPack pack) => CapOf(pack);

    /// <summary>The pixel pack in play (a bought, chosen pack, else Standard). The Toggles switches and the Skills window share this one setting.</summary>
    public string ActiveId
    {
        get
        {
            if (PreviewPackId != null) return PreviewPackId;   // the balance report is pretending this pack is in play
            string id = PackSetting;
            SkillPack p = Find(id);
            return p != null && id != StandardId && id != OfflineId && IsAvailable(p) ? id : StandardId;
        }
    }

    /// <summary>The pack that owns a pixel type (null for the basic pixels and everything outside the skill packs).</summary>
    public SkillPack PackOfType(PixelClicker.PixelType type)
    {
        foreach (SkillPack p in packs)
            if (p.types != null && Array.IndexOf(p.types, type) >= 0) return p;
        return null;
    }

    /// <summary>Is this pixel type kept out of the random spawns right now? Pack pixels only spawn while their pack is in play (or as guests); the basic pixels step aside while a pack is in play.</summary>
    /// <summary>
    /// The filler block of the skill pack in play (Dirt for Farming, Stone for Mining) and the share (0..1) of all spawns it should make up now:
    /// 'fillerStartShare' at level 1, fading evenly to 'fillerEndShare' at 'fillerFadeLevel'. False when the pack in play has no filler.
    /// </summary>
    public static bool TryGetFiller(out PixelClicker.PixelType type, out float share)
    {
        type = PixelClicker.PixelType.White;
        share = 0f;
        PixelSkills s = Instance;
        if (s == null) return false;
        SkillPack pack = s.Find(s.ActiveId);
        PixelClicker.PixelType[] filler = ParseTypes(pack != null ? pack.fillerType : null);
        if (filler.Length == 0) return false;
        type = filler[0];
        float start = pack.fillerStartShare > 0f ? pack.fillerStartShare : 0.6f;
        float end = pack.fillerEndShare > 0f ? pack.fillerEndShare : 0.1f;
        int fade = pack.fillerFadeLevel > 1 ? pack.fillerFadeLevel : 20;
        float k = Mathf.Clamp01(((PreviewLevel > 0 ? PreviewLevel : s.LevelOf(pack)) - 1) / (float)(fade - 1));
        share = Mathf.Clamp(Mathf.Lerp(start, end, k), 0f, 0.95f);
        return true;
    }

    /// <summary>The filler FAMILY of the pack in play: the main filler first, then the pack's extra fillers. False when the pack has no filler.</summary>
    public static bool GetFillerFamily(System.Collections.Generic.List<PixelClicker.PixelType> into)
    {
        into.Clear();
        PixelSkills s = Instance;
        if (s == null) return false;
        SkillPack pack = s.Find(s.ActiveId);
        PixelClicker.PixelType[] filler = ParseTypes(pack != null ? pack.fillerType : null);
        if (filler.Length == 0) return false;
        into.Add(filler[0]);
        foreach (PixelClicker.PixelType extra in ParseTypes(pack.extraFillerTypes))
            if (!into.Contains(extra)) into.Add(extra);
        return true;
    }

    private static readonly System.Collections.Generic.List<PixelClicker.PixelType> familyScratch = new System.Collections.Generic.List<PixelClicker.PixelType>();

    /// <summary>Is this type one of the fillers (the main one or an extra) of the pack in play?</summary>
    public static bool IsFillerType(PixelClicker.PixelType type) => GetFillerFamily(familyScratch) && familyScratch.Contains(type);

    /// <summary>A pack's hazard setting may list several blocks ("Mold,Toadstool"): one of them, picked at random each time. False when none parses.</summary>
    private static bool TryParseHazard(string list, out PixelClicker.PixelType type)
    {
        type = PixelClicker.PixelType.White;
        PixelClicker.PixelType[] parsed = ParseTypes(list);
        if (parsed.Length == 0) return false;
        type = parsed[UnityEngine.Random.Range(0, parsed.Length)];
        return true;
    }

    // The packs' block settings are text ("Twig", "Pinecone,Moss", "Mold,Toadstool") and are read on every spawn roll: each distinct text is parsed once and remembered.
    private static readonly System.Collections.Generic.Dictionary<string, PixelClicker.PixelType[]> parsedTypeLists = new System.Collections.Generic.Dictionary<string, PixelClicker.PixelType[]>();
    private static readonly PixelClicker.PixelType[] noTypes = new PixelClicker.PixelType[0];

    /// <summary>The pixel types named in a comma-separated setting, in order, unknown names skipped (cached; do not change the array). Empty for null / "".</summary>
    public static PixelClicker.PixelType[] ParseTypes(string list)
    {
        if (string.IsNullOrEmpty(list)) return noTypes;
        if (parsedTypeLists.TryGetValue(list, out PixelClicker.PixelType[] cached)) return cached;
        System.Collections.Generic.List<PixelClicker.PixelType> found = new System.Collections.Generic.List<PixelClicker.PixelType>();
        foreach (string raw in list.Split(','))
            if (Enum.TryParse(raw.Trim(), out PixelClicker.PixelType t)) found.Add(t);
        PixelClicker.PixelType[] result = found.Count > 0 ? found.ToArray() : noTypes;
        parsedTypeLists[list] = result;
        return result;
    }

    /// <summary>
    /// A truly random rotation (SkillPack.uniformRotation, Foraging): fills 'pool' with the spawnable blocks of the pack in play, each equally likely.
    /// False when the pack in play doesn't use one.
    /// </summary>
    public static bool TryGetUniformPool(PixelClicker clicker, System.Collections.Generic.List<int> pool)
    {
        pool.Clear();
        PixelSkills s = Instance;
        if (s == null || s.disableSkills || clicker == null) return false;
        SkillPack pack = s.Find(s.ActiveId);
        if (pack == null || !pack.uniformRotation || pack.types == null) return false;
        foreach (PixelClicker.PixelType t in pack.types)
        {
            if (PreviewLevel <= 0 && PixelFocus.HoldsOffHazards(pack.id) && PixelClicker.IsHazard(t)) continue;
            int i = clicker.IndexOf(t);
            if (i < 0 || !clicker.Tiers[i].CanSpawn || LeftOutOfSpawns(t)) continue;
            pool.Add(i);
        }
        return pool.Count > 0;
    }

    /// <summary>The hazard block of the pack in play and its share of the spawns now (grows a little with level). False when there is none.</summary>
    public static bool TryGetHazard(out PixelClicker.PixelType type, out float share)
    {
        type = PixelClicker.PixelType.White;
        share = 0f;
        PixelSkills s = Instance;
        if (s == null) return false;
        SkillPack pack = s.Find(s.ActiveId);
        if (pack == null || string.IsNullOrEmpty(pack.hazardType) || !TryParseHazard(pack.hazardType, out type)) return false;
        if (PreviewLevel <= 0 && PixelFocus.HoldsOffHazards(pack.id)) return false;   // a focus potion of this skill is active (PixelFocus): no hazard spawns
        float start = pack.hazardBaseShare > 0f ? pack.hazardBaseShare : 0.03f;
        float per = pack.hazardPerLevelShare > 0f ? pack.hazardPerLevelShare : 0.002f;
        float max = pack.hazardMaxShare > 0f ? pack.hazardMaxShare : 0.08f;
        share = Mathf.Clamp(start + per * ((PreviewLevel > 0 ? PreviewLevel : s.LevelOf(pack)) - 1), 0f, max);
        return true;
    }

    // ---- The hazard XP penalty: -10% per hazard set off inside the last minute (cumulative, the minute restarts each time) ----

    // One penalty per SKILL (the pack in play when the hazard went off): -10% each, restarting that skill's minute. Other skills are not affected.
    private static readonly System.Collections.Generic.Dictionary<string, int> penaltyLevels = new System.Collections.Generic.Dictionary<string, int>();
    private static readonly System.Collections.Generic.Dictionary<string, float> penaltyEnds = new System.Collections.Generic.Dictionary<string, float>();

    [Min(0f)]
    [Tooltip("How long the hazard XP penalty lasts, in seconds, restarting each time another hazard goes off. 0 = the coded default (60).")]
    [SerializeField] private float penaltySeconds = 0f;

    [Range(0f, 0.5f)]
    [Tooltip("How much XP each hazard takes off (cumulative while the penalty is running). 0 = the coded default (0.1 = 10%).")]
    [SerializeField] private float penaltyStep = 0f;

    private static int PenaltyLevelOf(string id) =>
        id != null && penaltyLevels.TryGetValue(id, out int level) && penaltyEnds.TryGetValue(id, out float end) && Time.time < end ? level : 0;

    /// <summary>The XP multiplier of this skill right now (1 = no penalty).</summary>
    public static float PenaltyFactor(string id)
    {
        int level = PenaltyLevelOf(id);
        if (level <= 0) return 1f;
        float step = Instance != null && Instance.penaltyStep > 0f ? Instance.penaltyStep : 0.1f;
        return Mathf.Max(0.1f, 1f - level * step);
    }

    /// <summary>The XP penalty running on this skill, in percent (0 = none).</summary>
    public static int PenaltyPercent(string id) { float f = PenaltyFactor(id); return f < 1f ? Mathf.RoundToInt((1f - f) * 100f) : 0; }

    /// <summary>Seconds left on this skill's XP penalty.</summary>
    public static float PenaltySecondsLeft(string id) => id != null && penaltyEnds.TryGetValue(id, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;

    /// <summary>A hazard went off: one more step of penalty on the skill in play (a new one when none is running) and its timer starts over. Returns text describing it.</summary>
    public static string ApplyHazardPenalty()
    {
        string id = Instance != null ? Instance.ActiveId : StandardId;
        float seconds = Instance != null && Instance.penaltySeconds > 0f ? Instance.penaltySeconds : 60f;
        float step = Instance != null && Instance.penaltyStep > 0f ? Instance.penaltyStep : 0.1f;
        int level = PenaltyLevelOf(id);
        level = level > 0 ? Mathf.Min(level + 1, Mathf.RoundToInt(0.9f / step)) : 1;
        penaltyLevels[id] = level;
        penaltyEnds[id] = Time.time + seconds;
        string name = Instance != null && Instance.Find(id) != null ? ShownName(Instance.Find(id)) : "Skill";
        return name + " XP -" + Mathf.RoundToInt(level * step * 100f) + "% for " + Mathf.RoundToInt(seconds) + " seconds.";
    }

    public static bool LeftOutOfSpawns(PixelClicker.PixelType type)
    {
        PixelSkills s = Instance;
        if (PixelClicker.IsRetiredOre(type)) return true;   // ores tagged RetiredOre never spawn (none at the moment)
        if (s == null) return PixelClicker.IsOre(type) || PixelClicker.IsFarm(type) || PixelClicker.IsWoodBlock(type) || PixelClicker.IsForageBlock(type) || PixelClicker.IsFishBlock(type) || type == PixelClicker.PixelType.Obsidian || type == PixelClicker.PixelType.Jellyfish || type == PixelClicker.PixelType.HornetNest || type == PixelClicker.PixelType.Toadstool || type == PixelClicker.PixelType.Mold;
        SkillPack owner = s.PackOfType(type);
        string active = s.ActiveId;
        if (owner != null)
        {
            foreach (SkillPack p in s.packs)   // a block can belong to several packs (Water: Farming and Fishing)
                if (p.id == active && p.types != null && Array.IndexOf(p.types, type) >= 0) return false;
            return !s.GuestOn(type);
        }
        return active != StandardId && PixelProspector.IsBase(type);
    }

    /// <summary>
    /// Is this pixel part of what you are playing right now? With a skill pack in play (Woodcutting, Fishing...) ONLY that pack's own blocks count (and guests its tree switched on):
    /// the extra shop pixels (Meteor, Ghost, Glass...) are not in the pack even though they still spawn. On Standard it is the normal rotation. Used by the Ghost's potions and Ultra Holes' requests.
    /// </summary>
    public static bool InCurrentRotation(PixelClicker.PixelType type)
    {
        PixelSkills s = Instance;
        if (s == null || s.disableSkills) return !LeftOutOfSpawns(type);
        SkillPack active = s.Find(s.ActiveId);
        if (active == null || active.id == StandardId || active.id == OfflineId) return !LeftOutOfSpawns(type);
        if (PixelClicker.IsHazard(type)) return false;
        if (active.types != null && Array.IndexOf(active.types, type) >= 0) return true;
        return s.GuestOn(type);
    }

    // ---- Guests: skill-only blocks a skill tree node lets the player switch on for other rotations ----

    // (the switched-on guest blocks are the state's list: a handful of names, a plain Contains is fine)

    /// <summary>Has a skill tree node that unlocks this block as a toggle been bought?</summary>
    public bool GuestUnlocked(PixelClicker.PixelType type)
    {
        string name = PixelClicker.TypeName(type);
        foreach (SkillPack p in packs)
            if (p.nodes != null)
                foreach (TreeNode n in p.nodes)
                    if (n.guestType == name && NodeUnlocked(p, n)) return true;
        foreach (SkillPack p in packs)   // a perk can unlock one too (Woodcutting level 5: Wood)
            if (p.perks != null && IsAvailable(p))
                foreach (Perk pk in p.perks)
                    if (pk != null && pk.guestType == name && LevelOf(p) >= pk.level) return true;
        return false;
    }

    /// <summary>Is the guest block unlocked and switched on (it spawns in every rotation)?</summary>
    public bool GuestOn(PixelClicker.PixelType type) => S.guests.Contains(PixelClicker.TypeName(type)) && GuestUnlocked(type);

    public void SetGuest(PixelClicker.PixelType type, bool on)
    {
        string name = PixelClicker.TypeName(type);
        if (on) { if (!S.guests.Contains(name)) S.guests.Add(name); }
        else S.guests.Remove(name);
        Changed?.Invoke();
    }

    /// <summary>The guest blocks the player has unlocked (shown as switches in the Toggles window).</summary>
    public System.Collections.Generic.List<PixelClicker.PixelType> UnlockedGuests()
    {
        System.Collections.Generic.List<PixelClicker.PixelType> list = new System.Collections.Generic.List<PixelClicker.PixelType>();
        foreach (SkillPack p in packs)
            if (p.nodes != null)
                foreach (TreeNode n in p.nodes)
                    if (!string.IsNullOrEmpty(n.guestType) && NodeUnlocked(p, n) && Enum.TryParse(n.guestType, out PixelClicker.PixelType t) && !list.Contains(t)) list.Add(t);
        foreach (SkillPack p in packs)
            if (p.perks != null && IsAvailable(p))
                foreach (Perk pk in p.perks)
                    if (pk != null && !string.IsNullOrEmpty(pk.guestType) && LevelOf(p) >= pk.level && Enum.TryParse(pk.guestType, out PixelClicker.PixelType t2) && !list.Contains(t2)) list.Add(t2);
        return list;
    }

    /// <summary>Growing a sprout in the Farming pack earns farming XP (called when a sprout pops its pixel off).</summary>
    public static void GrewSprout()
    {
        PixelSkills s = Instance;
        if (s == null) return;
        SkillPack farming = s.Find(FarmingId);
        if (farming != null && s.TrainingId == FarmingId) s.AddXp(farming, 2d);
    }

    // ---- Offline Progression mode: you play with some pack's pixels but only the Offline Progression skill earns XP ----


    /// <summary>
    /// What your character does while the game is closed: the skill that gets a little XP from offline progress (picked on Offline Progression's card;
    /// saved per save file). '' = Standard.
    /// </summary>
    public static string OfflineTask
    {
        get => S.offlineTask;
        set => S.offlineTask = value ?? "";
    }

    /// <summary>The offline task's pack (Standard when none / not available).</summary>
    public SkillPack OfflineTaskPack
    {
        get
        {
            SkillPack p = Find(OfflineTask);
            return p != null && p.id != OfflineId && IsAvailable(p) ? p : Find(StandardId);
        }
    }

    /// <summary>Offline progress: a little XP for the offline task skill. Returns the XP given.</summary>
    public double AddOfflineTaskXp(double xp)
    {
        SkillPack task = OfflineTaskPack;
        if (disableSkills || xp <= 0d || task == null) return 0d;
        AddXpFlat(task, xp, false);
        return xp;
    }

    /// <summary>Offline progress paid out: 'xp' goes to Offline Progression (once it is available - the Auto Clicker runs). Returns the XP given.</summary>
    public double AddOfflineXp(double xp)
    {
        SkillPack offline = Find(OfflineId);
        if (disableSkills || xp <= 0d || offline == null || !IsAvailable(offline)) return 0d;
        AddXp(offline, xp, false);
        return xp;
    }

    // A save was loaded: its skill choices are in the state now - everything that shows the pack in play redraws.
    private void OnLoaded() => Changed?.Invoke();

    /// <summary>The skill that earns the XP right now: the pack in play (Offline Progression is passive since 2026-10-16: it only gets XP from offline progress, AddOfflineXp).</summary>
    public string TrainingId => ActiveId;

    /// <summary>Chooses the skill to train. A normal pack also becomes the pixel pack in play (and ends Offline Progression); Offline Progression keeps the pixel pack you have.</summary>
    public void SetActive(string id)
    {
        SkillPack pack = Find(id);
        if (pack == null || !IsAvailable(pack)) return;
        bool changed = false;
        if (id == OfflineId) return;   // (passive: it is never chosen to train)
        else
        {
            if (id != ActiveId)
            {
                PackSetting = id; changed = true;   // takes effect from the NEXT spawn: the cube you are clicking stays until it is collected
                if (!disableSkillSwitchBlock && !PixelTitleScreen.Showing)
                {
                    if (clicker == null) clicker = PixelFind.First<PixelClicker>();
                    if (clicker != null) clicker.ShowSkillSwitchBlock();   // ...except for this test: a Glass 'skill switch' block replaces it right away
                }
            }
        }
        if (changed) Changed?.Invoke();
    }

    // ---- Skill tree ----

    /// <summary>Is this node of the STANDARD skill tree unlocked? (Block Hold, Power Click and their branches read it.)</summary>
    /// <summary>Is this skill the one in training right now? (One check instead of comparing TrainingId by hand everywhere.)</summary>
    public static bool IsTraining(string skillId) => Instance != null && !string.IsNullOrEmpty(skillId) && Instance.TrainingId == skillId;

    /// <summary>Id of the Standard tree node 'Aimed Clicks' (edge-click aiming, PixelBurstPop.AimingOwned).</summary>
    public const string AimedClicksNode = "aimed";

    // (the Standard skills ask this every frame: the counter names are made once per node id)
    private static readonly System.Collections.Generic.Dictionary<string, string> standardNodeKeys = new System.Collections.Generic.Dictionary<string, string>();

    public static bool HasStandardNode(string nodeId)
    {
        if (nodeId == null) return false;
        if (!standardNodeKeys.TryGetValue(nodeId, out string key)) { key = "skill." + StandardId + ".node." + nodeId; standardNodeKeys[nodeId] = key; }
        return PixelStats.Total(key) >= 1d;
    }

    public bool NodeUnlocked(SkillPack pack, TreeNode node) => pack != null && node != null && PixelStats.Total(node.KeyIn(pack.id)) >= 1d;

    /// <summary>Every node this one needs ('requires' can list several ids, comma separated; all of them must be unlocked).</summary>
    public System.Collections.Generic.List<TreeNode> ParentsOf(SkillPack pack, TreeNode node)
    {
        var list = new System.Collections.Generic.List<TreeNode>();
        if (pack == null || node == null || string.IsNullOrEmpty(node.requires) || pack.nodes == null) return list;
        foreach (string raw in node.requires.Split(','))
        {
            string id = raw.Trim();
            if (id.Length == 0) continue;
            foreach (TreeNode n in pack.nodes) if (n != null && n.id == id) { list.Add(n); break; }
        }
        return list;
    }

    /// <summary>The first node this one needs (null for a root node).</summary>
    public TreeNode ParentOf(SkillPack pack, TreeNode node)
    {
        var parents = ParentsOf(pack, node);
        return parents.Count > 0 ? parents[0] : null;
    }

    /// <summary>Column of a node: the longest chain of parents above it, or its own 'column' if that is further along (a node's children follow it).</summary>
    public int Depth(SkillPack pack, TreeNode node) => DepthOf(pack, node, 0);

    private int DepthOf(SkillPack pack, TreeNode node, int guard)
    {
        if (guard > 12) return 0;   // (a loop in hand-edited data)
        int d = Mathf.Max(0, node.column);
        foreach (TreeNode p in ParentsOf(pack, node)) d = Mathf.Max(d, 1 + DepthOf(pack, p, guard + 1));
        return d;
    }

    /// <summary>The skill is below the node's minLevel (it can't be unlocked yet, whatever the points).</summary>
    public bool NeedsLevel(SkillPack pack, TreeNode node) => pack != null && node != null && node.minLevel > 1 && LevelOf(pack) < node.minLevel;

    /// <summary>The parents not unlocked yet, as 'A and B' (empty when nothing is missing).</summary>
    public string MissingParents(SkillPack pack, TreeNode node)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (TreeNode p in ParentsOf(pack, node)) if (!NodeUnlocked(pack, p)) names.Add(p.displayName);
        return string.Join(" and ", names);
    }

    public int PointsSpent(SkillPack pack)
    {
        int spent = 0;
        if (pack == null || pack.nodes == null) return 0;
        foreach (TreeNode n in pack.nodes) if (NodeUnlocked(pack, n)) spent += Mathf.Max(0, n.cost);
        return spent;
    }

    /// <summary>One point per level above 1, minus what the unlocked nodes cost.</summary>
    public int PointsAvailable(SkillPack pack) => pack == null ? 0 : Mathf.Max(0, LevelOf(pack) - 1 - PointsSpent(pack));

    public bool CanUnlock(SkillPack pack, TreeNode node)
    {
        if (pack == null || node == null || NodeUnlocked(pack, node) || NeedsLevel(pack, node)) return false;
        foreach (TreeNode parent in ParentsOf(pack, node)) if (!NodeUnlocked(pack, parent)) return false;
        return PointsAvailable(pack) >= Mathf.Max(0, node.cost);
    }

    public bool UnlockNode(SkillPack pack, TreeNode node)
    {
        if (!CanUnlock(pack, node)) return false;
        PixelStats.Best(node.KeyIn(pack.id), 1d);
        PixelHints.Announce(pack.displayName + ": " + node.displayName, "You unlocked '" + node.displayName + "' in the " + pack.displayName + " skill tree.");
        PixelHints.Trigger("node_" + pack.id + "_" + node.id);   // a node's first-time tip, if it has one (e.g. node_standard_aimed); unknown ids do nothing
        Changed?.Invoke();
        return true;
    }

    // ---- XP and levels ----

    public double Xp(SkillPack pack) => pack == null ? 0d : PixelStats.Total(pack.XpKey);

    /// <summary>Level (1 = none yet) and the XP inside it: 'into' of 'need' to the next level.</summary>
    [Header("XP curve")]
    [Min(0.1f)]
    [Tooltip("Multiplies EVERY skill's XP curve (each pack's baseXp, so every level takes this many times as long). 2 = twice as long as the packs' own numbers.")]
    [SerializeField] private float xpCurveScale = 2f;

    /// <summary>XP from level 1 to 2 for a pack, with the global curve scale (xpCurveScale) applied. Every level-up step grows from this by the pack's xpGrowth.</summary>
    public double BaseXpOf(SkillPack pack) => pack == null ? 1d : Math.Max(1d, pack.baseXp * Math.Max(0.1f, xpCurveScale));

    public int LevelOf(SkillPack pack, out double into, out double need)
    {
        double xp = Xp(pack);
        double baseXp = BaseXpOf(pack);
        if (pack != null && pack.levelXp == xp && pack.levelBase == baseXp && pack.levelGrowth == pack.xpGrowth)
        {
            into = pack.levelInto; need = pack.levelNeed;   // (same XP and curve as last time: no need to walk the levels again)
            return pack.level;
        }
        double total = xp;
        int level = 1;
        need = baseXp;
        while (xp >= need && level < 99)
        {
            xp -= need;
            need *= Math.Max(1.05f, pack != null ? pack.xpGrowth : 1.6f);
            level++;
        }
        into = xp;
        if (pack != null) { pack.levelXp = total; pack.levelBase = baseXp; pack.levelGrowth = pack.xpGrowth; pack.levelInto = into; pack.levelNeed = need; pack.level = level; }
        return level;
    }

    public int LevelOf(SkillPack pack) => LevelOf(pack, out _, out _);

    private SkillPack PackForTier(int tierIndex)
    {
        if (clicker == null || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return null;
        PixelClicker.PixelType type = clicker.Tiers[tierIndex].type;
        SkillPack owner = PackOfType(type);
        foreach (SkillPack p in packs)   // a block shared by two packs counts for the one in play
            if (p.id == ActiveId && p.types != null && Array.IndexOf(p.types, type) >= 0) { owner = p; break; }
        return owner != null && owner.id != StandardId ? owner : null;
    }

    private void OnCollected(int tierIndex, double amount, bool automatic) => OnCollectedInner(tierIndex, amount, automatic);   // (XP popups go to the XP dock either way: PixelSkillsPopups)

    private void OnCollectedInner(int tierIndex, double amount, bool automatic)
    {
        // Standard is the skill that levels no matter what: every collected block gives it a flat standardXpPerBlock (1), manual or auto, whatever
        // skill is trained, with no multipliers. Popups only while Standard is the one trained (from the very first click).
        SkillPack standard = Find(StandardId);
        if (standard != null) AddXpFlat(standard, StandardXpPerBlock, TrainingId == StandardId);

        // (Offline Progression earns nothing from clicks: its XP only comes from offline progress - AddOfflineXp, called by PixelOfflineProgress.Grant.)
        SkillPack pack = PackForTier(tierIndex);
        if (pack == null || pack.id != ActiveId) return;
        AddXp(pack, automatic ? autoXpFactor : 1d);
    }

    /// <summary>
    /// Pixel rules: XP from consumed husks (PixelClicker.GiveHuskXp: Vacuum pixel, Vacuum Device, Ultra Holes; the Recycler; Husk Crush). A husk trains the skill it belongs to
    /// (wood husks Woodcutting, ores Mining...), pure husks train Standard. Popups only once the Skills tab exists, like Standard's click XP - except a
    /// crush (crushed = true), which always shows its popup, small amounts with decimals: it is the crush's feedback.
    /// </summary>
    /// popupText (Husk Crush's bonuses): shown by the mouse ('Air crush bonus!'), the crush's own '+n XP' still shows where the husk broke ('at', world);
    /// noPopup = give the XP silently.
    public void AddHuskXp(int tierIndex, double xp, bool crushed = false, string popupText = null, bool noPopup = false, Vector3? at = null)
    {
        if (disableSkills || xp <= 0d) return;
        SkillPack pack = PackForTier(tierIndex) ?? Find(StandardId);
        bool popup = pack != null && !noPopup && (crushed || (popupText == null && (pack.id != StandardId || AnyTrainable)));
        xpPopupDecimals = crushed;   // (read by QueueXpPopup: the crush's popup shows decimals...)
        xpPopupAt = crushed ? at : null;   // (...and stays where the husk broke)
        AddXp(pack, xp, popup);
        xpPopupDecimals = false;
        xpPopupAt = null;
        if (pack != null && popupText != null && !hideXpPopups) ShowMouseText(pack, popupText);   // (a crush bonus: by the mouse, stacked so they never overlap)
    }

    /// <summary>A reward's XP (PixelGoals): exactly 'xp' to that skill (no multipliers), with its popup.</summary>
    public void AddRewardXp(string skillId, double xp)
    {
        if (disableSkills || xp <= 0d) return;
        AddXpFlat(Find(skillId) ?? Find(StandardId), xp, true);
    }

    /// <summary>The XP one manual click on this block gives its skill before multipliers: 1 for a skill's block, Standard's flat XP per block for a pure one (Husk Crush pays a share of it).</summary>
    public double ClickXpOf(int tierIndex) => PackForTier(tierIndex) != null ? 1d : StandardXpPerBlock;

    /// <summary>The id of the skill a husk of this tier trains (its pack's, or Standard for the pure pixels) - the skill AddHuskXp pays.</summary>
    public string HuskSkillId(int tierIndex)
    {
        SkillPack pack = PackForTier(tierIndex);
        return pack != null ? pack.id : StandardId;
    }

    /// <summary>The name of the skill a husk of this type trains (its pack's, or Standard for the pure pixels).</summary>
    public string HuskSkillName(PixelClicker.PixelType type)
    {
        SkillPack owner = PackOfType(type);
        if (owner == null || owner.id == StandardId) owner = Find(StandardId);
        return owner != null ? ShownName(owner) : "Standard";
    }

    /// <summary>Extra XP for the skill being trained (the shuffle bonus): goes through AddXp, so penalties, minigame multipliers and the bonus bank apply as usual.</summary>
    public void AddShuffleXp(double xp)
    {
        AddXp(Find(TrainingId), xp);
    }

    // ---- Perk popups and level-gated unlocks ----
    private struct PerkNotice { public string text; public string packId; public bool inWindow; }
    private readonly System.Collections.Generic.Queue<PerkNotice> perkNotices = new System.Collections.Generic.Queue<PerkNotice>();
    private readonly object perkHighlightKey = new object();

    /// <summary>A level was reached: every perk of that level with a popup text queues its tutorial box once (it waits for a calm moment, see UpdatePerkNotices).</summary>
    /// <summary>
    /// The Skills tab shows once a skill pack is owned, or once Standard has reached level 2 and its level-2 popup (which opens the Skills window beside it) is no longer waiting -
    /// skills are introduced by that popup, not by Cubie. Older saves past Standard level 2 simply have the tab.
    /// </summary>
    public bool SkillsTabUnlocked
    {
        get
        {
            if (disableSkills || clicker == null) return false;
            if (AnyTrainable) return true;   // a skill pack is owned (bought, or Dev Tools unlocked everything): the button is needed whatever Standard's level
            if (LevelOfId(StandardId) < 2) return false;
            foreach (PerkNotice n in perkNotices) if (n.packId == StandardId) return false;
            return true;
        }
    }

    private void QueuePerkNotices(SkillPack pack, int before, int after)
    {
        if (pack.perks == null) return;
        foreach (Perk perk in pack.perks)
        {
            if (perk == null || perk.level <= before || perk.level > after || string.IsNullOrEmpty(perk.noticeText)) continue;
            string key = "perknotice." + pack.id + "." + perk.level;
            if (PixelStats.Total(key) >= 1d) continue;
            PixelStats.Count(key);
            perkNotices.Enqueue(new PerkNotice { text = perk.noticeText, packId = pack.id, inWindow = perk.noticeInWindow });
        }
    }

    private void UpdatePerkNotices()
    {
        if (perkNotices.Count > 0 && !PixelHints.TipsEnabled) perkNotices.Clear();   // (PixelTipQueue shows them one by one: ShowNextPerkNotice)
    }

    /// <summary>Shows the next queued perk popup (called by PixelTipQueue when it is this source's turn).</summary>
    private void ShowNextPerkNotice()
    {
        if (perkNotices.Count == 0) return;
        PerkNotice notice = perkNotices.Dequeue();
        int index = packs.FindIndex(p => p.id == notice.packId);
        if (!notice.inWindow || !built || index < 0 || PixelTutorialDim.Active) { PixelNotice.Show(notice.text, freezeTime: notice.inWindow); return; }
        // Beside the Skills window: it opens on this skill, the small box sits to its right and the skill's card pulses gold until Got it.
        OpenWindow();
        selected = index;
        RefreshWindow();
        RectTransform card = index < cards.Count && cards[index].button != null ? cards[index].button.GetComponent<RectTransform>() : null;
        if (card != null) PixelHighlight.Set(perkHighlightKey, card);
        PixelNotice.Show(notice.text, 0f, () => PixelHighlight.Clear(perkHighlightKey), small: true, beside: windowRect, freezeTime: true);   // (time stands still while the player reads it - Standard level 2's skill intro)
    }

    /// <summary>
    /// The skill that owns a pixel type for the Value cap (a skill pack's blocks: that skill; every other type: Standard) and its level.
    /// False while skills are switched off.
    /// </summary>
    public static bool TryGetOwnerLevel(PixelClicker.PixelType type, out string skillName, out int level)
    {
        skillName = ""; level = 1;
        PixelSkills s = Instance;
        if (s == null || s.disableSkills) return false;
        SkillPack pack = s.PackOfType(type);
        if (pack == null || pack.id == OfflineId) pack = s.Find(StandardId);
        if (pack == null) return false;
        skillName = ShownName(pack);   // ('???' for Standard until level 2)
        level = s.LevelOf(pack);
        return true;
    }

    /// <summary>The level of a skill by id (1 when the skill or the system is missing).</summary>
    public static int LevelOfId(string id)
    {
        PixelSkills s = Instance;
        SkillPack pack = s != null ? s.Find(id) : null;
        return pack != null ? s.LevelOf(pack) : 1;
    }

    [Header("Skill perks")]
    [Min(0f)]
    [Tooltip("Woodcutting perk 'Seasoned wood' (level 3): old wood pixels last this much longer (0.5 = 50% longer). 0 = the coded default (0.5).")]
    [SerializeField] private float woodcuttingLifespanBonus = 0f;

    /// <summary>Multiplier on how long an old pixel of this type lasts, from skill perks (1 = none). Woodcutting level 3: wood blocks last longer.</summary>
    public static float SkillLifetimeFactor(PixelClicker.PixelType type)
    {
        PixelSkills s = Instance;
        if (s == null || s.disableSkills || !PixelClicker.IsWoodBlock(type) || LevelOfId(WoodcuttingId) < 3) return 1f;
        return 1f + (s.woodcuttingLifespanBonus > 0f ? s.woodcuttingLifespanBonus : 0.5f);
    }

    public void AddXp(SkillPack pack, double xp, bool popup = true)
    {
        if (pack == null || xp <= 0d) return;
        xp *= PenaltyFactor(pack.id);   // a hazard went off recently in this skill
        xp *= MinigameXpMultiplier;   // every enabled minigame adds to all XP
        xp *= PixelBlockRules.HeldXpFactor(pack.id);   // a held hold block (Block Hold) with an XP bonus
        xp += DrawBonus(pack, xp);   // bonus bank: doubles the gain, the extra comes out of the bank
        AddXpFlat(pack, xp, popup);
    }

    /// <summary>Adds exactly 'xp' (no penalty, minigame, hold or bonus bank multipliers): Standard's flat XP per block.</summary>
    private void AddXpFlat(SkillPack pack, double xp, bool popup)
    {
        if (pack == null || xp <= 0d) return;
        int before = LevelOf(pack);
        PixelStats.Count(pack.XpKey, xp);
        if (popup) QueueXpPopup(pack, xp);
        int after = LevelOf(pack);
        if (after > before) QueuePerkNotices(pack, before, after);
        if (after > before && (pack.id != StandardId || after > 2)) PixelSkillLevelPopup.Show(pack, after, pack.id == OfflineId ? 0 : after - before);   // the small badge popup (Standard's level 2 has its own big popup)
        bool revealed = pack.id == StandardId && before < 2 && after >= 2 && !showStandardNameEarly && !disableSkills;
        if (revealed) RevealStandard();   // the mystery bar flips to 'Standard' (its own event log line)
        else if (after > before)
            PixelHints.Announce(pack.displayName + " level " + after, "Your " + pack.displayName + " skill reached level " + after + " (+" + (after - before) + " skill point" + (after - before == 1 ? "" : "s") + ").");
        Changed?.Invoke();
    }
}
