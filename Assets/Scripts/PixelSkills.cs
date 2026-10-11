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
        public Perk[] perks = new Perk[0];
        [Tooltip("The skill tree: nodes you buy with skill points (one point per level above 1).")]
        public TreeNode[] nodes = new TreeNode[0];
    }

    public const string StandardId = "standard";
    public const string MiningId = "mining";
    public const string OfflineId = "offline";

    [Header("Skill Packs")]
    [Tooltip("The packs, in the order they are listed. Missing built-in ones (by id) are added automatically.")]
    [SerializeField] private List<SkillPack> packs = new List<SkillPack>();

    [Tooltip("Share of the XP an automatic (auto clicker) click gives compared with a manual click.")]
    [SerializeField] private float autoXpFactor = 0.25f;

    [Tooltip("Turn the whole skill system (and its Skills tab) off.")]
    [SerializeField] private bool disableSkills = false;

    private PixelClicker clicker;
    private PixelAutoClicker autoClicker;

    public static PixelSkills Instance { get; private set; }
    public IReadOnlyList<SkillPack> Packs => packs;

    /// <summary>Raised when the active pack changes or a level is gained.</summary>
    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; offlineCache = -1; }

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
                perks = new[]
                {
                    new Perk { level = 2, text = "Richer veins: a higher chance of a rich vein" },
                    new Perk { level = 4, text = "Steady hands: ores take one click less" },
                    new Perk { level = 6, text = "Prospector's eye: rich veins glitter from further away" },
                    new Perk { level = 8, text = "Motherlode: rich veins pay more" },
                    new Perk { level = 10, text = "Mastery: a small permanent bonus to every pixel" },
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
            if ((pack.nodes == null || pack.nodes.Length == 0) && (pack.id == MiningId || pack.id == OfflineId)) pack.nodes = DefaultNodes(pack.id);
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
        if (pack.id == MiningId) return PixelProspector.AnyOreUnlocked(clicker);
        if (pack.id == OfflineId)
        {
            if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
            return autoClicker != null && autoClicker.Running;   // needs the Auto Clicker, like offline progress itself
        }
        return false;
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

    /// <summary>The pack in use. Mining IS Prospector mode, so the Toggles switch and the Skills window never disagree.</summary>
    public string ActiveId => PixelProspector.Setting && IsAvailable(Find(MiningId)) ? MiningId : StandardId;

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
    public static void ReloadSettingsFromPrefs() { offlineCache = -1; }

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
            if (id != ActiveId) { PixelProspector.Setting = id == MiningId; changed = true; }   // takes effect from the NEXT spawn: the cube you are clicking stays until it is collected
        }
        if (changed) Changed?.Invoke();
    }

    /// <summary>Chooses which pack's pixels spawn WITHOUT changing the skill being trained (used while Offline Progression is chosen).</summary>
    public void SetPixelPack(string id)
    {
        SkillPack pack = Find(id);
        if (pack == null || pack.id == OfflineId || !IsAvailable(pack) || id == ActiveId) return;
        PixelProspector.Setting = id == MiningId;
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
        return PixelClicker.IsOre(clicker.Tiers[tierIndex].type) ? Find(MiningId) : null;
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
