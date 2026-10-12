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
    }

    [Serializable]
    public class TreeNode
    {
        [Tooltip("Unique id inside the pack (the unlock is saved as the counter 'skill.<pack>.node.<id>').")]
        public string id = "";
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Id of the node that must be unlocked first (empty = a root node).")]
        public string requires = "";
        [Tooltip("Row in the tree (0 = top, 1 = middle, 2 = bottom). The column follows from how many nodes lie above it.")]
        public int row = 1;
        [Tooltip("Skill points it costs. A skill earns one point per level above 1.")]
        public int cost = 1;
        [Tooltip("Off = shown as 'coming soon' (the effect is not built yet).")]
        public bool implemented = false;
        [Tooltip("A pixel type (name) this node unlocks as a toggle: once bought, that skill-only block can be switched on in the Toggles window so it also spawns in other rotations.")]
        public string guestType = "";
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
        public Perk[] perks = new Perk[0];
        [Tooltip("The skill tree: nodes you buy with skill points (one point per level above 1).")]
        public TreeNode[] nodes = new TreeNode[0];
    }

    public const string StandardId = "standard";
    private const string StandardDescription = "The usual rotation: the six monochrome and RGB pixels (Glass and Luminescent join in once bought, and have their own switches in Toggles). Standard is a skill too: it levels as you click and its perks - like longer-lasting old pixels - help in every rotation. While you train another skill it still gains a tiny 5% of the XP.";
    public const string MiningId = "mining";
    public const string OfflineId = "offline";
    public const string FarmingId = "farming";
    public const string ElectricianId = "electrician";

    [Header("Skill Packs")]
    [Tooltip("The packs, in the order they are listed. Missing built-in ones (by id) are added automatically.")]
    [SerializeField] private List<SkillPack> packs = new List<SkillPack>();

    [Tooltip("Share of the XP an automatic (auto clicker) click gives compared with a manual click.")]
    [SerializeField] private float autoXpFactor = 0.25f;

    [Tooltip("Offline Progression: the skill pack you play with while Offline Progression is chosen earns this share of its normal XP (0.5 = 50%).")]
    [SerializeField] private float offlineAssignedShare = 0.5f;

    [Tooltip("Offline Progression: extra XP for Offline Progression itself when no skill pack is in play (0.5 = +50%).")]
    [SerializeField] private float offlineNoSkillBonus = 0.5f;

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
    private static void ResetStatics() { penaltyLevel = 0; penaltyUntil = 0f; Instance = null; offlineCache = -1; packCache = null; guestCache = null; offCache = null; }

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
            else if ((p.id != FarmingId && p.id != MiningId) || !clicker.IsUnlocked(p.types[0])) continue;   // (Mining: the first ore unlocked brings Stone, its filler, too)
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
        EnsureDefaults();
    }

    private void OnDestroy()
    {
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
        if (!packs.Exists(p => p.id == StandardId))
            packs.Insert(0, new SkillPack
            {
                id = StandardId, displayName = "Standard", accent = new Color(0.5f, 0.55f, 0.65f),
                baseXp = 80f, xpGrowth = 1.5f, perks = StandardPerks(),
                description = StandardDescription,
                replaces = "-", spawns = "White, Gray, Black, Red, Green, Blue (+ Glass, Luminescent once bought)", xpFrom = "Every pixel you collect (5% while another skill is trained)",
            });
        if (!packs.Exists(p => p.id == MiningId))
            packs.Add(new SkillPack
            {
                id = MiningId, displayName = "Mining", accent = new Color(0.86f, 0.46f, 0.26f),
                description = "Swap the basic cubes for six tough ores and plain Stone. Stone is a useless filler block that is most of what you see at first; as your mining level rises it gets rarer and the ores show up more. Break ores for mining XP; some are rich veins that pay big.",
                replaces = "Monochrome, RGB", spawns = "Stone, Copper, Tin, Iron, Lead, Zinc, Nickel", xpFrom = "Breaking ore pixels",
                unlockHint = "Buy Prospector's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = new[] { PixelClicker.PixelType.Copper, PixelClicker.PixelType.Tin, PixelClicker.PixelType.Iron, PixelClicker.PixelType.Lead, PixelClicker.PixelType.Zinc, PixelClicker.PixelType.Nickel, PixelClicker.PixelType.Rock, PixelClicker.PixelType.GasPocket },
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
                description = "Pick this skill, then pick which pack's pixels you play with. Every click trains Offline Progression, which will improve what you earn while you are away. The pack you assign still earns 50% of its normal XP. With no skill pack assigned (Standard), Offline Progression earns +50% XP.",
                replaces = "Nothing: you choose the pixel pack", spawns = "Whatever the chosen pack spawns", xpFrom = "Every click (the assigned pack earns 50%; +50% for itself with no pack assigned)",
                unlockHint = "Buy the Auto Clicker in the Shop",
                baseXp = 150f, xpGrowth = 1.6f,
                perks = new[]
                {
                    new Perk { level = 2, text = "Efficient rest: a higher share of earnings while away" },
                    new Perk { level = 5, text = "Long sleeper: a longer time away counts" },
                },
            });
        foreach (SkillPack pack in packs)
        {
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
                if (pack.spawns != null && !pack.spawns.Contains("Stone")) pack.spawns = "Stone, " + pack.spawns;
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
            // Hazard blocks: Farming's Pest and Mining's Gas Pocket (part of their packs' pixel lists).
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
            if (pack.id != MiningId && pack.id != OfflineId && pack.id != FarmingId && pack.id != ElectricianId) continue;
            System.Collections.Generic.List<TreeNode> merged = new System.Collections.Generic.List<TreeNode>(pack.nodes ?? new TreeNode[0]);
            foreach (TreeNode def in DefaultNodes(pack.id))
                if (!merged.Exists(n => n.id == def.id)) merged.Add(def);
            pack.nodes = merged.ToArray();
        }

        // Texts saved in older scenes still say the skill packs replace Glass and Luminescent too.
        foreach (SkillPack pack in packs)
        {
            if (pack.replaces != null) pack.replaces = pack.replaces.Replace("Monochrome, RGB, Glass, Luminescent", "Monochrome, RGB");
            if (pack.id == OfflineId)
            {
                if (pack.description != null && pack.description.Contains("You earn no XP for that pack"))
                    pack.description = "Pick this skill, then pick which pack's pixels you play with. Every click trains Offline Progression, which will improve what you earn while you are away. The pack you assign still earns 50% of its normal XP. With no skill pack assigned (Standard), Offline Progression earns +50% XP.";
                if (pack.xpFrom == "Every click, whichever pack is in play")
                    pack.xpFrom = "Every click (the assigned pack earns 50%; +50% for itself with no pack assigned)";
            }
            if (pack.id == StandardId)
            {
                if (pack.perks == null || pack.perks.Length == 0) pack.perks = StandardPerks();
                if (pack.description != null && pack.description.Contains("No skill is trained")) pack.description = StandardDescription;
                if (pack.xpFrom == "Nothing") pack.xpFrom = "Every pixel you collect (5% while another skill is trained)";
                if (pack.description != null && pack.description.StartsWith("The usual rotation: monochrome, RGB, Glass and Luminescent"))
                    pack.description = "The usual rotation: the six monochrome and RGB pixels (Glass and Luminescent join in once bought, and have their own switches in Toggles). No skill is trained.";
                if (pack.spawns == "White, Gray, Black, Red, Green, Blue, Glass, Luminescent")
                    pack.spawns = "White, Gray, Black, Red, Green, Blue (+ Glass, Luminescent once bought)";
            }
        }

        // Offline Progression is listed right under Standard.
        int offlineAt = packs.FindIndex(p => p.id == OfflineId);
        if (offlineAt > 1)
        {
            SkillPack offline = packs[offlineAt];
            packs.RemoveAt(offlineAt);
            packs.Insert(1, offline);
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

    public SkillPack Find(string id) => packs.Find(p => p.id == id);

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

    private const string PackPref = "PixelClicker.Setting.SkillPack";
    private static string packCache;

    /// <summary>The pixel pack the player chose (saved per save file; '' / 'standard' = the usual rotation). Older saves with Prospector mode on read as Mining.</summary>
    public static string PackSetting
    {
        get
        {
            if (packCache == null)
            {
                packCache = PlayerPrefs.GetString(PackPref, "");
                if (string.IsNullOrEmpty(packCache) && PlayerPrefs.GetInt("PixelClicker.Setting.Prospector", 0) != 0) packCache = MiningId;
            }
            return packCache;
        }
        set { packCache = value ?? ""; PlayerPrefs.SetString(PackPref, packCache); }
    }

    /// <summary>The pixel pack in play (a bought, chosen pack, else Standard). The Toggles switches and the Skills window share this one setting.</summary>
    public string ActiveId
    {
        get
        {
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
        if (pack == null || string.IsNullOrEmpty(pack.fillerType) || !Enum.TryParse(pack.fillerType, out type)) return false;
        float start = pack.fillerStartShare > 0f ? pack.fillerStartShare : 0.6f;
        float end = pack.fillerEndShare > 0f ? pack.fillerEndShare : 0.1f;
        int fade = pack.fillerFadeLevel > 1 ? pack.fillerFadeLevel : 20;
        float k = Mathf.Clamp01((s.LevelOf(pack) - 1) / (float)(fade - 1));
        share = Mathf.Clamp(Mathf.Lerp(start, end, k), 0f, 0.95f);
        return true;
    }

    /// <summary>The hazard block of the pack in play and its share of the spawns now (grows a little with level). False when there is none.</summary>
    public static bool TryGetHazard(out PixelClicker.PixelType type, out float share)
    {
        type = PixelClicker.PixelType.White;
        share = 0f;
        PixelSkills s = Instance;
        if (s == null) return false;
        SkillPack pack = s.Find(s.ActiveId);
        if (pack == null || string.IsNullOrEmpty(pack.hazardType) || !Enum.TryParse(pack.hazardType, out type)) return false;
        float start = pack.hazardBaseShare > 0f ? pack.hazardBaseShare : 0.03f;
        float per = pack.hazardPerLevelShare > 0f ? pack.hazardPerLevelShare : 0.002f;
        float max = pack.hazardMaxShare > 0f ? pack.hazardMaxShare : 0.08f;
        share = Mathf.Clamp(start + per * (s.LevelOf(pack) - 1), 0f, max);
        return true;
    }

    // ---- The hazard XP penalty: -10% per hazard set off inside the last minute (cumulative, the minute restarts each time) ----

    private static int penaltyLevel;
    private static float penaltyUntil;

    [Min(0f)]
    [Tooltip("How long the hazard XP penalty lasts, in seconds, restarting each time another hazard goes off. 0 = the coded default (60).")]
    [SerializeField] private float penaltySeconds = 0f;

    [Range(0f, 0.5f)]
    [Tooltip("How much XP each hazard takes off (cumulative while the penalty is running). 0 = the coded default (0.1 = 10%).")]
    [SerializeField] private float penaltyStep = 0f;

    /// <summary>The XP multiplier right now (1 = no penalty).</summary>
    public static float PenaltyFactor
    {
        get
        {
            if (penaltyLevel <= 0 || Time.time >= penaltyUntil) return 1f;
            float step = Instance != null && Instance.penaltyStep > 0f ? Instance.penaltyStep : 0.1f;
            return Mathf.Max(0.1f, 1f - penaltyLevel * step);
        }
    }

    /// <summary>The XP penalty running now, in percent (0 = none).</summary>
    public static int PenaltyPercent => PenaltyFactor < 1f ? Mathf.RoundToInt((1f - PenaltyFactor) * 100f) : 0;

    /// <summary>Seconds left on the XP penalty.</summary>
    public static float PenaltySecondsLeft => Mathf.Max(0f, penaltyUntil - Time.time);

    /// <summary>A hazard went off: one more step of penalty (a new one when none is running) and the timer starts over. Returns text describing it.</summary>
    public static string ApplyHazardPenalty()
    {
        float seconds = Instance != null && Instance.penaltySeconds > 0f ? Instance.penaltySeconds : 60f;
        float step = Instance != null && Instance.penaltyStep > 0f ? Instance.penaltyStep : 0.1f;
        penaltyLevel = Time.time < penaltyUntil ? Mathf.Min(penaltyLevel + 1, Mathf.RoundToInt(0.9f / step)) : 1;
        penaltyUntil = Time.time + seconds;
        return "Skill XP -" + Mathf.RoundToInt(penaltyLevel * step * 100f) + "% for " + Mathf.RoundToInt(seconds) + " seconds.";
    }

    public static bool LeftOutOfSpawns(PixelClicker.PixelType type)
    {
        PixelSkills s = Instance;
        if (IsOptIn(type)) return !OptInOn(type);   // Wood and Rock: on by default once unlocked, switchable in the Toggles window
        if (s == null) return PixelClicker.IsOre(type) || PixelClicker.IsFarm(type);
        SkillPack owner = s.PackOfType(type);
        string active = s.ActiveId;
        if (owner != null) return owner.id != active && !s.GuestOn(type);
        return active != StandardId && PixelProspector.IsBase(type);
    }

    // ---- Guests: skill-only blocks a skill tree node lets the player switch on for other rotations ----

    private const string GuestPref = "PixelClicker.Setting.SkillGuests";
    private static System.Collections.Generic.HashSet<string> guestCache;

    private static System.Collections.Generic.HashSet<string> GuestSet
    {
        get
        {
            if (guestCache == null)
            {
                guestCache = new System.Collections.Generic.HashSet<string>();
                foreach (string part in PlayerPrefs.GetString(GuestPref, "").Split(',')) if (part.Length > 0) guestCache.Add(part);
            }
            return guestCache;
        }
    }

    /// <summary>The basic shop blocks (Wood, Rock) are never part of a rotation by themselves: they spawn only while the player has switched them on (the same switch set as the guests).</summary>
    public static bool IsOptIn(PixelClicker.PixelType type) => type == PixelClicker.PixelType.Wood;

    /// <summary>Is the opt-in block (Wood, Rock) switched on?</summary>
    public static bool OptInOn(PixelClicker.PixelType type) => !OffSet.Contains(type.ToString());

    private const string OffPref = "PixelClicker.Setting.OptInOff";
    private static System.Collections.Generic.HashSet<string> offCache;

    private static System.Collections.Generic.HashSet<string> OffSet
    {
        get
        {
            if (offCache == null)
            {
                offCache = new System.Collections.Generic.HashSet<string>();
                foreach (string part in PlayerPrefs.GetString(OffPref, "").Split(',')) if (part.Length > 0) offCache.Add(part);
            }
            return offCache;
        }
    }

    /// <summary>Switches Wood / Rock on or off (they are on by default).</summary>
    public void SetOptIn(PixelClicker.PixelType type, bool on)
    {
        if (on) OffSet.Remove(type.ToString()); else OffSet.Add(type.ToString());
        PlayerPrefs.SetString(OffPref, string.Join(",", OffSet));
        Changed?.Invoke();
    }

    /// <summary>Has a skill tree node that unlocks this block as a toggle been bought?</summary>
    public bool GuestUnlocked(PixelClicker.PixelType type)
    {
        string name = type.ToString();
        foreach (SkillPack p in packs)
            if (p.nodes != null)
                foreach (TreeNode n in p.nodes)
                    if (n.guestType == name && NodeUnlocked(p, n)) return true;
        return false;
    }

    /// <summary>Is the guest block unlocked and switched on (it spawns in every rotation)?</summary>
    public bool GuestOn(PixelClicker.PixelType type) => GuestSet.Contains(type.ToString()) && GuestUnlocked(type);

    public void SetGuest(PixelClicker.PixelType type, bool on)
    {
        if (on) GuestSet.Add(type.ToString()); else GuestSet.Remove(type.ToString());
        PlayerPrefs.SetString(GuestPref, string.Join(",", GuestSet));
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

    private const string OfflinePref = "PixelClicker.Setting.SkillOffline";
    private static int offlineCache = -1;

    /// <summary>The player chose Offline Progression as the skill to train (saved per save file). The pixel pack in play is still <see cref="ActiveId"/>.</summary>
    public static bool OfflineMode
    {
        get
        {
            if (offlineCache < 0) offlineCache = PlayerPrefs.GetInt(OfflinePref, 0) != 0 ? 1 : 0;
            return offlineCache != 0;
        }
        private set { offlineCache = value ? 1 : 0; PlayerPrefs.SetInt(OfflinePref, offlineCache); }
    }

    /// <summary>A save file with its own settings was loaded: read the switch again.</summary>
    public static void ReloadSettingsFromPrefs() { offlineCache = -1; packCache = null; guestCache = null; offCache = null; }

    /// <summary>The skill that earns the XP right now: Offline Progression while it is chosen, else the pack in play.</summary>
    public string TrainingId => OfflineMode && IsAvailable(Find(OfflineId)) ? OfflineId : ActiveId;

    /// <summary>Chooses the skill to train. A normal pack also becomes the pixel pack in play (and ends Offline Progression); Offline Progression keeps the pixel pack you have.</summary>
    public void SetActive(string id)
    {
        SkillPack pack = Find(id);
        if (pack == null || !IsAvailable(pack)) return;
        bool changed = false;
        if (id == OfflineId) { if (!OfflineMode) { OfflineMode = true; changed = true; } }
        else
        {
            if (OfflineMode) { OfflineMode = false; changed = true; }
            if (id != ActiveId) { PackSetting = id; changed = true; }   // takes effect from the NEXT spawn: the cube you are clicking stays until it is collected
        }
        if (changed) Changed?.Invoke();
    }

    /// <summary>Chooses which pack's pixels spawn WITHOUT changing the skill being trained (used while Offline Progression is chosen).</summary>
    public void SetPixelPack(string id)
    {
        SkillPack pack = Find(id);
        if (pack == null || pack.id == OfflineId || !IsAvailable(pack) || id == ActiveId) return;
        PackSetting = id;
        Changed?.Invoke();
    }

    // ---- Skill tree ----

    public bool NodeUnlocked(SkillPack pack, TreeNode node) => pack != null && node != null && PixelStats.Total("skill." + pack.id + ".node." + node.id) >= 1d;

    public TreeNode ParentOf(SkillPack pack, TreeNode node)
    {
        if (pack == null || node == null || string.IsNullOrEmpty(node.requires) || pack.nodes == null) return null;
        foreach (TreeNode n in pack.nodes) if (n.id == node.requires) return n;
        return null;
    }

    /// <summary>Column of a node: how many parents lie above it.</summary>
    public int Depth(SkillPack pack, TreeNode node)
    {
        int d = 0;
        TreeNode cur = ParentOf(pack, node);
        while (cur != null && d < 12) { d++; cur = ParentOf(pack, cur); }
        return d;
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
        if (pack == null || node == null || NodeUnlocked(pack, node)) return false;
        TreeNode parent = ParentOf(pack, node);
        if (parent != null && !NodeUnlocked(pack, parent)) return false;
        return PointsAvailable(pack) >= Mathf.Max(0, node.cost);
    }

    public bool UnlockNode(SkillPack pack, TreeNode node)
    {
        if (!CanUnlock(pack, node)) return false;
        PixelStats.Best("skill." + pack.id + ".node." + node.id, 1d);
        PixelHints.Announce(pack.displayName + ": " + node.displayName, "You unlocked '" + node.displayName + "' in the " + pack.displayName + " skill tree.");
        Changed?.Invoke();
        return true;
    }

    // ---- XP and levels ----

    public double Xp(SkillPack pack) => pack == null ? 0d : PixelStats.Total("skill." + pack.id + ".xp");

    /// <summary>Level (1 = none yet) and the XP inside it: 'into' of 'need' to the next level.</summary>
    public int LevelOf(SkillPack pack, out double into, out double need)
    {
        double xp = Xp(pack);
        int level = 1;
        need = Math.Max(1d, pack.baseXp);
        while (xp >= need && level < 99)
        {
            xp -= need;
            need *= Math.Max(1.05f, pack.xpGrowth);
            level++;
        }
        into = xp;
        return level;
    }

    public int LevelOf(SkillPack pack) => LevelOf(pack, out _, out _);

    private SkillPack PackForTier(int tierIndex)
    {
        if (clicker == null || tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return null;
        SkillPack owner = PackOfType(clicker.Tiers[tierIndex].type);
        return owner != null && owner.id != StandardId ? owner : null;
    }

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        // Standard is a skill too: full XP while it is the one trained, a tiny share while another skill is.
        SkillPack standard = Find(StandardId);
        if (standard != null) AddXp(standard, (automatic ? autoXpFactor : 1d) * (TrainingId == StandardId ? 1d : StandardSideRate), TrainingId == StandardId && AnyTrainable);   // (no popups before the Skills tab exists)

        // Offline Progression: every click trains it; the pack assigned to it (the pixels in play) still earns half of its normal XP,
        // and with no skill pack in play Offline Progression itself earns 50% more.
        SkillPack offline = Find(OfflineId);
        if (OfflineMode && IsAvailable(offline))
        {
            double rate = automatic ? autoXpFactor : 1d;
            SkillPack assigned = PackForTier(tierIndex);
            if (assigned != null && assigned.id == ActiveId) AddXp(assigned, rate * offlineAssignedShare);
            AddXp(offline, rate * (ActiveId == StandardId ? 1d + offlineNoSkillBonus : 1d));
            return;
        }
        SkillPack pack = PackForTier(tierIndex);
        if (pack == null || pack.id != ActiveId) return;
        AddXp(pack, automatic ? autoXpFactor : 1d);
    }

    public void AddXp(SkillPack pack, double xp, bool popup = true)
    {
        if (pack == null || xp <= 0d) return;
        xp *= PenaltyFactor;   // a hazard went off recently
        xp += DrawBonus(pack, xp);   // bonus bank: doubles the gain, the extra comes out of the bank
        int before = LevelOf(pack);
        PixelStats.Count("skill." + pack.id + ".xp", xp);
        if (popup) QueueXpPopup(pack, xp);
        int after = LevelOf(pack);
        if (after > before && (pack.id != StandardId || AnyTrainable))
            PixelHints.Announce(pack.displayName + " level " + after, "Your " + pack.displayName + " skill reached level " + after + " (+" + (after - before) + " skill point" + (after - before == 1 ? "" : "s") + ").");
        Changed?.Invoke();
    }
}
