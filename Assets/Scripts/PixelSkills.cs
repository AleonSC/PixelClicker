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
    }

    public const string StandardId = "standard";
    public const string MiningId = "mining";

    [Header("Skill Packs")]
    [Tooltip("The packs, in the order they are listed. Missing built-in ones (by id) are added automatically.")]
    [SerializeField] private List<SkillPack> packs = new List<SkillPack>();

    [Tooltip("Share of the XP an automatic (auto clicker) click gives compared with a manual click.")]
    [SerializeField] private float autoXpFactor = 0.25f;

    [Tooltip("Turn the whole skill system (and its Skills tab) off.")]
    [SerializeField] private bool disableSkills = false;

    private PixelClicker clicker;

    public static PixelSkills Instance { get; private set; }
    public IReadOnlyList<SkillPack> Packs => packs;

    /// <summary>Raised when the active pack changes or a level is gained.</summary>
    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

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
    }

    public SkillPack Find(string id) => packs.Find(p => p.id == id);

    /// <summary>Can the player train / use this pack yet?</summary>
    public bool IsAvailable(SkillPack pack)
    {
        if (pack == null) return false;
        if (pack.id == StandardId) return true;
        if (pack.id == MiningId) return PixelProspector.AnyOreUnlocked(clicker);
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

    /// <summary>Switches the pack in play (swapping is free; XP is kept per pack).</summary>
    public void SetActive(string id)
    {
        SkillPack pack = Find(id);
        if (pack == null || !IsAvailable(pack) || id == ActiveId) return;
        PixelProspector.Setting = id == MiningId;
        if (clicker != null) clicker.RefreshSpawnTier();
        Changed?.Invoke();
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
            PixelHints.Announce(pack.displayName + " level " + after, "Your " + pack.displayName + " skill reached level " + after + ".");
        Changed?.Invoke();
    }
}
