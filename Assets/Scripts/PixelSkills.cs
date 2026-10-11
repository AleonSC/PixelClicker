using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SHELL of the skill system: skill packs replace the standard base cubes (monochrome, RGB, Glass, Luminescent) with a different set, and you train
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
        [Tooltip("The pixel types this pack puts in. The FIRST one must be unlocked (bought in the Shop) for the pack to be available. They only spawn while the pack is in play, unless turned on as guests.")]
        public PixelClicker.PixelType[] types = new PixelClicker.PixelType[0];
        public Perk[] perks = new Perk[0];
        [Tooltip("The skill tree: nodes you buy with skill points (one point per level above 1).")]
        public TreeNode[] nodes = new TreeNode[0];
    }

    public const string StandardId = "standard";
    public const string MiningId = "mining";
    public const string OfflineId = "offline";
    public const string FarmingId = "farming";
    public const string ElectricianId = "electrician";

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
    private static void ResetStatics() { Instance = null; offlineCache = -1; packCache = null; guestCache = null; }

    private float bundleCheckTimer;

    /// <summary>Safety net: the Farmer's Pack gives every farm block at once, so if the first one is unlocked (an old save, a half-applied purchase) the rest are unlocked too.</summary>
    private void CheckFarmBundle()
    {
        bundleCheckTimer -= Time.unscaledDeltaTime;
        if (bundleCheckTimer > 0f || clicker == null) return;
        bundleCheckTimer = 3f;
        foreach (SkillPack p in packs)
        {
            if (p.id != FarmingId || p.types == null || p.types.Length == 0 || !clicker.IsUnlocked(p.types[0])) continue;
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
                description = "The usual rotation: monochrome, RGB, Glass and Luminescent pixels. No skill is trained.",
                replaces = "-", spawns = "White, Gray, Black, Red, Green, Blue, Glass, Luminescent", xpFrom = "Nothing",
            });
        if (!packs.Exists(p => p.id == MiningId))
            packs.Add(new SkillPack
            {
                id = MiningId, displayName = "Mining", accent = new Color(0.86f, 0.46f, 0.26f),
                description = "Swap the basic cubes for six tough ores. Break them for mining XP; some are rich veins that pay big.",
                replaces = "Monochrome, RGB, Glass, Luminescent", spawns = "Copper, Tin, Iron, Lead, Zinc, Nickel", xpFrom = "Breaking ore pixels",
                unlockHint = "Buy Prospector's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = new[] { PixelClicker.PixelType.Copper, PixelClicker.PixelType.Tin, PixelClicker.PixelType.Iron, PixelClicker.PixelType.Lead, PixelClicker.PixelType.Zinc, PixelClicker.PixelType.Nickel },
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
                description = "Swap the basic cubes for eight farm blocks: dirt, seeds, water, fertilizer, pollen, wheat, weevils and golden fruit. Grow sprouts for farming XP.",
                replaces = "Monochrome, RGB, Glass, Luminescent", spawns = "Dirt, Seed, Water, Fertilizer, Pollen, Wheat, Weevil, Golden Fruit",
                xpFrom = "Collecting farm blocks and growing sprouts",
                unlockHint = "Buy Farmer's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = new[] { PixelClicker.PixelType.Dirt, PixelClicker.PixelType.Seed, PixelClicker.PixelType.Water, PixelClicker.PixelType.Fertilizer,
                                PixelClicker.PixelType.Pollen, PixelClicker.PixelType.Wheat, PixelClicker.PixelType.Weevil, PixelClicker.PixelType.GoldenFruit },
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
                description = "Swap the basic cubes for Electric pixels only. Every Electric click trains the skill and can set off an Overcharge; link the pixels you drop to power your devices.",
                replaces = "Monochrome, RGB, Glass, Luminescent", spawns = "Electric",
                xpFrom = "Collecting Electric pixels",
                unlockHint = "Buy the Electrician's Pack in the Shop",
                baseXp = 100f, xpGrowth = 1.6f,
                types = new[] { PixelClicker.PixelType.Electric },
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
                description = "Pick this skill, then pick which pack's pixels you play with. You earn no XP for that pack, only for Offline Progression, which will improve what you earn while you are away.",
                replaces = "Nothing: you choose the pixel pack", spawns = "Whatever the chosen pack spawns", xpFrom = "Every click, whichever pack is in play",
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
            if (pack.id == MiningId && (pack.types == null || pack.types.Length == 0))
                pack.types = new[] { PixelClicker.PixelType.Copper, PixelClicker.PixelType.Tin, PixelClicker.PixelType.Iron, PixelClicker.PixelType.Lead, PixelClicker.PixelType.Zinc, PixelClicker.PixelType.Nickel };
            // Built-in trees: add any default node the saved pack is missing (ones you edited are kept).
            if (pack.id != MiningId && pack.id != OfflineId && pack.id != FarmingId && pack.id != ElectricianId) continue;
            System.Collections.Generic.List<TreeNode> merged = new System.Collections.Generic.List<TreeNode>(pack.nodes ?? new TreeNode[0]);
            foreach (TreeNode def in DefaultNodes(pack.id))
                if (!merged.Exists(n => n.id == def.id)) merged.Add(def);
            pack.nodes = merged.ToArray();
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
    public static bool LeftOutOfSpawns(PixelClicker.PixelType type)
    {
        PixelSkills s = Instance;
        if (IsOptIn(type)) return s == null || !GuestSet.Contains(type.ToString());   // Wood and Rock: only when switched on in the Toggles window
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
    public static bool IsOptIn(PixelClicker.PixelType type) => type == PixelClicker.PixelType.Wood || type == PixelClicker.PixelType.Rock;

    /// <summary>Is the opt-in block (Wood, Rock) switched on?</summary>
    public static bool OptInOn(PixelClicker.PixelType type) => GuestSet.Contains(type.ToString());

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
    public static void ReloadSettingsFromPrefs() { offlineCache = -1; packCache = null; guestCache = null; }

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
        // Offline Progression: every click earns XP for that skill ONLY, whichever pixel pack is in play.
        SkillPack offline = Find(OfflineId);
        if (OfflineMode && IsAvailable(offline)) { AddXp(offline, automatic ? autoXpFactor : 1d); return; }
        SkillPack pack = PackForTier(tierIndex);
        if (pack == null || pack.id != ActiveId) return;
        AddXp(pack, automatic ? autoXpFactor : 1d);
    }

    public void AddXp(SkillPack pack, double xp)
    {
        if (pack == null || xp <= 0d) return;
        int before = LevelOf(pack);
        PixelStats.Count("skill." + pack.id + ".xp", xp);
        int after = LevelOf(pack);
        if (after > before)
            PixelHints.Announce(pack.displayName + " level " + after, "Your " + pack.displayName + " skill reached level " + after + " (+" + (after - before) + " skill point" + (after - before == 1 ? "" : "s") + ").");
        Changed?.Invoke();
    }
}
