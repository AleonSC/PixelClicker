// ---- System notes ----
// PixelTreeMode.cs
//   TREE MODE (step 1 of docs/systems/WoodcuttingProgression.md; tested in Unity 2026-10-16 - it plays well, timings / numbers still being tuned; component added by PixelClicker.Awake, PixelTreeMode.Instance). While the skill
//   named in skillId (woodcutting) is in play AND a shop pack with unlocksTreeMode is owned (the hidden 'Oak Trees (test)' pack: unlock it with the
//   Dev Tools 'Shop unlocks' widget), the cube stops rolling the pack's blocks by weight and alternates between:
//     - the FILLER rotation: random picks from 'fillers' (Twig, Leaf, Wood; Moss and Sap from level 2). At least a minimum of them, then a
//       growing chance per block until a tree is certain at a maximum. The run SHRINKS with the level: 12-30 at level 1 (minFillerAtLevel1 /
//       maxFillerAtLevel1) down to 3-10 at fillerLevelSpan (20) and above (minFillerAtTop / maxFillerAtTop), so trees are a treat early on;
//     - a TREE: a species picked by weight among the unlocked ones (Oak only for now) plays out its slot list in order (Fixed / Run / Wildcard /
//       Optional / Hazard). An INFESTED tree fills its Hazard slot (the Termite Hive, always last): chance infestedChance at infestFromLevel rising to
//       infestedChanceAt20 at level 20, 0 while a Focus potion holds hazards off; it is announced on the tree bar from its first block.
//   PixelClicker.PickSpawnTier asks TryPick first (tree picks skip the filler family swap and the special-block replacement). Every pick leaves a TAG
//   (TakeTag: 1 = tree block, 2 = the tree's last block, 4 = the tree's first block) that PixelClicker keeps with the pre-rolled block and the live
//   block, and RollTier reports each change of the live block (OnLiveChanged): the tree bar counts the live blocks, and 'Timber!' fires when the last
//   block leaves the cube (bonus XP = timberXpShare of the tree's blocks, a status line, timberSound, stat woodcutting.trees_felled).
//   The tree bar is a PixelStatusLines line (key 'tree'): 'Oak  3 / 9', plus '- infested: Termite Hive at the end'.
//   Not saved: a reload starts in the filler rotation. Not built yet (see the design doc): other species, the Great Oak as a species, the infested
//   look and sound, lookahead past one block, Pesticide charges, Shuffle switched off inside a tree. A block pulled out with Block Hold loses its
//   tree tag (pulling the tree's last block fires Timber early).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelTreeMode : MonoBehaviour
{
    public enum SlotKind { Fixed, Run, Wildcard, Optional, Hazard }

    [Serializable]
    public class TreeSlot
    {
        [Tooltip("Fixed = always 'block'. Run = 'block' minCount..maxCount times. Wildcard = 'block' at 'chance', else 'altBlock'. Optional = 'block' at 'chance', else nothing. Hazard = 'block' only in an infested tree.")]
        public SlotKind kind = SlotKind.Fixed;
        [Tooltip("The block, as a PixelType name (Leaf, Wood, Sap, Acorn, Heartwood, HornetNest = the Termite Hive...).")]
        public string block = "Wood";
        [Tooltip("Wildcard: the block used when the roll misses. Every kind: the block used while the skill level is below fromLevel (empty = the slot is skipped).")]
        public string altBlock = "";
        [Range(0f, 1f)]
        [Tooltip("Wildcard / Optional: the chance of 'block'.")]
        public float chance = 1f;
        [Min(1)]
        [Tooltip("Run: fewest blocks in the run.")]
        public int minCount = 1;
        [Min(1)]
        [Tooltip("Run: most blocks in the run.")]
        public int maxCount = 1;
        [Tooltip("Run: a rare block that can stand in for any block of the run (e.g. Pesticide). Empty = none.")]
        public string rareBlock = "";
        [Range(0f, 1f)]
        [Tooltip("Run: the chance of the rare block, per block of the run.")]
        public float rareChance = 0f;
        [Min(1)]
        [Tooltip("Skill level from which 'block' can come (below it the slot uses altBlock, or is skipped).")]
        public int fromLevel = 1;
    }

    [Serializable]
    public class TreeSpecies
    {
        [Tooltip("Name shown on the tree bar.")]
        public string displayName = "Oak";
        [Min(1)]
        [Tooltip("Skill level from which this species can arrive.")]
        public int unlockLevel = 1;
        [Min(0f)]
        [Tooltip("Pick weight among the unlocked species (0 = never).")]
        public float weight = 1f;
        [Tooltip("Can this species be infested (its Hazard slot filled)?")]
        public bool canBeInfested = true;
        [Tooltip("The tree's blocks, in order.")]
        public List<TreeSlot> slots = new List<TreeSlot>();
    }

    [Serializable]
    public class FillerBlock
    {
        [Tooltip("The filler block, as a PixelType name.")]
        public string block = "Twig";
        [Min(1)]
        [Tooltip("Skill level from which it joins the filler rotation.")]
        public int fromLevel = 1;
    }

    [Header("Tree mode")]
    [Tooltip("Turns tree mode off completely (the skill rolls its blocks by weight as before, even with the test pack owned).")]
    [SerializeField] private bool disableTreeMode = false;
    [Tooltip("The skill that uses tree mode (its id).")]
    [SerializeField] private string skillId = PixelSkills.WoodcuttingId;

    [Header("Filler rotation")]
    [Tooltip("The filler blocks (random picks between trees). Empty = the built-in list (Twig, Leaf, Wood; Moss and Sap from level 2).")]
    [SerializeField] private List<FillerBlock> fillers = new List<FillerBlock>();
    // How long the filler run between trees is SHRINKS with the skill level: long at level 1 (trees are a treat), short by fillerLevelSpan.
    // (Renamed from minFiller / maxFiller so the old saved 3 / 10 don't carry over.)
    [Min(0)]
    [Tooltip("At level 1: filler blocks that always come before the next tree can arrive.")]
    [SerializeField] private int minFillerAtLevel1 = 12;
    [Min(1)]
    [Tooltip("At level 1: by this many filler blocks a tree is certain (the chance climbs evenly from the minimum to here).")]
    [SerializeField] private int maxFillerAtLevel1 = 30;
    [Min(0)]
    [Tooltip("At fillerLevelSpan and above: filler blocks that always come before the next tree can arrive.")]
    [SerializeField] private int minFillerAtTop = 3;
    [Min(1)]
    [Tooltip("At fillerLevelSpan and above: by this many filler blocks a tree is certain.")]
    [SerializeField] private int maxFillerAtTop = 10;
    [Min(2)]
    [Tooltip("The skill level where the filler run reaches its shortest (the 'At Top' values). Between level 1 and this it shrinks evenly.")]
    [SerializeField] private int fillerLevelSpan = 20;

    [Header("Species")]
    [Tooltip("The tree species. Empty = the built-in list (Oak).")]
    [SerializeField] private List<TreeSpecies> species = new List<TreeSpecies>();

    [Header("Infested trees")]
    [Min(1)]
    [Tooltip("Skill level from which trees can be infested.")]
    [SerializeField] private int infestFromLevel = 2;
    [Range(0f, 1f)]
    [Tooltip("Chance a tree is infested at infestFromLevel.")]
    [SerializeField] private float infestedChance = 0.25f;
    [Range(0f, 1f)]
    [Tooltip("Chance a tree is infested at level 20 (rises evenly to it; stays there above 20).")]
    [SerializeField] private float infestedChanceAt20 = 0.45f;

    [Header("Timber")]
    [Min(0f)]
    [Tooltip("Bonus XP when a tree falls, as a share of the number of blocks in the tree (0.2 = a 10-block tree gives 2 XP; at least 1).")]
    [SerializeField] private float timberXpShare = 0.2f;
    [Tooltip("Sound played when a tree falls (a sound id).")]
    [SerializeField] private string timberSound = "axe_chop";

    [Header("Tree bar")]
    [Tooltip("Show the tree bar (a line in the top status box) while a tree is up.")]
    [SerializeField] private bool showTreeBar = true;
    [Tooltip("Colour of the tree bar.")]
    [SerializeField] private Color barColor = new Color(0.55f, 0.85f, 0.4f, 1f);
    [Tooltip("Colour of the tree bar for an infested tree.")]
    [SerializeField] private Color infestedColor = new Color(0.95f, 0.55f, 0.25f, 1f);
    [Tooltip("Seconds the 'Timber!' line stays.")]
    [SerializeField] private float timberLineSeconds = 3f;

    public static PixelTreeMode Instance { get; private set; }

    private const string BarKey = "tree";
    private const int TagTree = 1, TagLast = 2, TagFirst = 4;

    private PixelShop shop;
    private readonly List<int> queue = new List<int>();   // the current tree's blocks still to hand out (tier indexes)
    private int fillerCount;
    private int lastTag;

    // The tree whose blocks are being handed out (set when it starts) and the tree whose blocks are live on the cube (set when its first block appears).
    private string pendingName; private int pendingTotal; private bool pendingInfested;
    private string liveName; private int liveTotal, liveIndex; private bool liveInfested, barShown;

    private void Awake()
    {
        Instance = this;
        if (species == null || species.Count == 0) species = DefaultSpecies();
        if (fillers == null || fillers.Count == 0) fillers = DefaultFillers();
        if (maxFillerAtLevel1 < minFillerAtLevel1 + 1) maxFillerAtLevel1 = minFillerAtLevel1 + 1;
        if (maxFillerAtTop < minFillerAtTop + 1) maxFillerAtTop = minFillerAtTop + 1;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private static List<FillerBlock> DefaultFillers() => new List<FillerBlock>
    {
        new FillerBlock { block = "Twig" }, new FillerBlock { block = "Leaf" }, new FillerBlock { block = "Wood" },
        new FillerBlock { block = "Moss", fromLevel = 2 }, new FillerBlock { block = "Sap", fromLevel = 2 },
    };

    private static List<TreeSpecies> DefaultSpecies() => new List<TreeSpecies>
    {
        // Oak: Leaf, Leaf, Wood x3-6, [Sap 30% / Wood], Acorn?, [Heartwood 20% / Wood], (Hive). Sap from level 2, Acorn from 3, Heartwood from 6.
        new TreeSpecies
        {
            displayName = "Oak", unlockLevel = 1, weight = 1f, canBeInfested = true,
            slots = new List<TreeSlot>
            {
                new TreeSlot { kind = SlotKind.Fixed, block = "Leaf" },
                new TreeSlot { kind = SlotKind.Fixed, block = "Leaf" },
                new TreeSlot { kind = SlotKind.Run, block = "Wood", minCount = 3, maxCount = 6, rareBlock = "Pesticide", rareChance = 0.06f },
                new TreeSlot { kind = SlotKind.Wildcard, block = "Sap", altBlock = "Wood", chance = 0.3f, fromLevel = 2 },
                new TreeSlot { kind = SlotKind.Optional, block = "Acorn", chance = 0.5f, fromLevel = 3 },
                new TreeSlot { kind = SlotKind.Wildcard, block = "Heartwood", altBlock = "Wood", chance = 0.2f, fromLevel = 6 },
                new TreeSlot { kind = SlotKind.Hazard, block = "HornetNest" },
            },
        },
    };

    // ---- Called by PixelClicker ----

    /// <summary>The tag of the last pick (1 tree block, 2 last block, 4 first block; 0 = filler or not tree mode), cleared as it is read.</summary>
    public static int TakeTag()
    {
        if (Instance == null) return 0;
        int t = Instance.lastTag;
        Instance.lastTag = 0;
        return t;
    }

    /// <summary>The next block while tree mode is on (a filler pick or the current tree's next slot). False = tree mode is off: roll as usual.</summary>
    public static bool TryPick(PixelClicker clicker, out int tierIndex)
    {
        tierIndex = -1;
        return Instance != null && Instance.Pick(clicker, out tierIndex);
    }

    /// <summary>The live block on the cube changed: counts tree blocks on the tree bar and fires Timber when the tree's last block left the cube.</summary>
    public static void OnLiveChanged(int outgoingTag, int incomingTag)
    {
        if (Instance != null) Instance.LiveChanged(outgoingTag, incomingTag);
    }

    // ---- Picking ----

    private bool Active(PixelClicker clicker)
    {
        if (disableTreeMode || clicker == null) return false;
        PixelSkills skills = PixelSkills.Instance;
        if (skills == null || skills.ActiveId != skillId) return false;
        if (shop == null) shop = PixelFind.First<PixelShop>();
        return shop != null && shop.TreeModeOwned();
    }

    private bool Pick(PixelClicker clicker, out int tierIndex)
    {
        tierIndex = -1;
        lastTag = 0;
        if (!Active(clicker))
        {
            if (queue.Count > 0 || pendingName != null) { queue.Clear(); pendingName = null; fillerCount = 0; }
            HideBar();
            return false;
        }

        int level = Mathf.Max(1, PixelSkills.LevelOfId(skillId));
        bool first = false;
        FillerRange(level, out int minFiller, out int maxFiller);
        if (queue.Count == 0 && fillerCount >= minFiller && UnityEngine.Random.value < StartChance(minFiller, maxFiller))
            first = StartTree(clicker, level);

        if (queue.Count > 0)
        {
            tierIndex = queue[0];
            queue.RemoveAt(0);
            lastTag = TagTree | (first ? TagFirst : 0) | (queue.Count == 0 ? TagLast : 0);
            if (queue.Count == 0) fillerCount = 0;   // the next pick is filler again
            return true;
        }

        tierIndex = PickFiller(clicker, level);
        if (tierIndex < 0) return false;   // no filler can spawn: let the normal roll handle it
        fillerCount++;
        return true;
    }

    /// <summary>The filler run for this skill level: the level-1 values shrinking evenly to the 'At Top' values by fillerLevelSpan.</summary>
    private void FillerRange(int level, out int minFiller, out int maxFiller)
    {
        float t = Mathf.Clamp01((level - 1) / (float)Mathf.Max(1, fillerLevelSpan - 1));
        minFiller = Mathf.RoundToInt(Mathf.Lerp(minFillerAtLevel1, minFillerAtTop, t));
        maxFiller = Mathf.Max(minFiller + 1, Mathf.RoundToInt(Mathf.Lerp(maxFillerAtLevel1, maxFillerAtTop, t)));
    }

    private float StartChance(int minFiller, int maxFiller)
    {
        int span = Mathf.Max(1, maxFiller - minFiller + 1);
        return Mathf.Clamp01((fillerCount - minFiller + 1) / (float)span);
    }

    private int PickFiller(PixelClicker clicker, int level)
    {
        int chosen = -1, count = 0;
        foreach (FillerBlock f in fillers)
        {
            if (f == null || f.fromLevel > level) continue;
            int i = SpawnableIndex(clicker, f.block);
            if (i < 0) continue;
            count++;
            if (UnityEngine.Random.value < 1f / count) chosen = i;
        }
        return chosen;
    }

    /// <summary>Starts a tree of a random unlocked species (fills the queue). False if no species can grow.</summary>
    private bool StartTree(PixelClicker clicker, int level)
    {
        TreeSpecies s = PickSpecies(level);
        if (s == null) return false;
        bool infested = s.canBeInfested && UnityEngine.Random.value < InfestedChance(level);
        queue.Clear();
        foreach (TreeSlot slot in s.slots)
            if (slot != null) AddSlot(clicker, slot, level, infested);
        if (queue.Count == 0) return false;
        pendingName = string.IsNullOrEmpty(s.displayName) ? "Tree" : s.displayName;
        pendingTotal = queue.Count;
        pendingInfested = infested && queue.Exists(i => PixelClicker.IsHazard(clicker.Tiers[i].type));
        return true;
    }

    private TreeSpecies PickSpecies(int level)
    {
        float total = 0f;
        foreach (TreeSpecies s in species) if (s != null && s.unlockLevel <= level) total += Mathf.Max(0f, s.weight);
        if (total <= 0f) return null;
        float roll = UnityEngine.Random.value * total;
        foreach (TreeSpecies s in species)
        {
            if (s == null || s.unlockLevel > level) continue;
            roll -= Mathf.Max(0f, s.weight);
            if (roll <= 0f) return s;
        }
        return null;
    }

    private float InfestedChance(int level)
    {
        if (level < infestFromLevel || PixelFocus.HoldsOffHazardsNow()) return 0f;
        float t = infestFromLevel >= 20 ? 1f : Mathf.Clamp01((level - infestFromLevel) / (float)(20 - infestFromLevel));
        return Mathf.Lerp(infestedChance, infestedChanceAt20, t);
    }

    private void AddSlot(PixelClicker clicker, TreeSlot slot, int level, bool infested)
    {
        bool open = level >= slot.fromLevel;
        switch (slot.kind)
        {
            case SlotKind.Fixed:
                Add(clicker, open ? slot.block : slot.altBlock);
                break;
            case SlotKind.Run:
            {
                int n = UnityEngine.Random.Range(Mathf.Max(1, slot.minCount), Mathf.Max(slot.minCount, slot.maxCount) + 1);
                for (int k = 0; k < n; k++)
                {
                    bool rare = open && !string.IsNullOrEmpty(slot.rareBlock) && UnityEngine.Random.value < slot.rareChance && SpawnableIndex(clicker, slot.rareBlock) >= 0;
                    Add(clicker, rare ? slot.rareBlock : open ? slot.block : slot.altBlock);
                }
                break;
            }
            case SlotKind.Wildcard:
                Add(clicker, open && UnityEngine.Random.value < slot.chance ? slot.block : slot.altBlock);
                break;
            case SlotKind.Optional:
                if (open && UnityEngine.Random.value < slot.chance) Add(clicker, slot.block);
                break;
            case SlotKind.Hazard:
                if (infested) Add(clicker, slot.block);
                break;
        }
    }

    private void Add(PixelClicker clicker, string block)
    {
        int i = SpawnableIndex(clicker, block);
        if (i >= 0) queue.Add(i);   // a block that cannot spawn (locked, switched off) is left out of the tree
    }

    /// <summary>The tier index of a block named by its PixelType, if it may spawn now (else -1).</summary>
    private static int SpawnableIndex(PixelClicker clicker, string block)
    {
        if (string.IsNullOrEmpty(block) || !Enum.TryParse(block, out PixelClicker.PixelType type)) return -1;
        int i = clicker.IndexOf(type);
        if (i < 0 || !clicker.Tiers[i].CanSpawn || PixelSkills.LeftOutOfSpawns(type)) return -1;
        if (PixelClicker.IsHazard(type) && PixelFocus.HoldsOffHazardsNow()) return -1;
        return i;
    }

    // ---- The live tree: tree bar and Timber ----

    private void LiveChanged(int outgoingTag, int incomingTag)
    {
        if ((outgoingTag & TagLast) != 0 && liveName != null) Timber();
        if ((incomingTag & TagTree) == 0) return;
        if ((incomingTag & TagFirst) != 0 || liveName == null)
        {
            liveName = pendingName ?? "Tree";
            liveTotal = Mathf.Max(1, pendingTotal);
            liveInfested = pendingInfested;
            liveIndex = 0;
        }
        liveIndex = Mathf.Min(liveIndex + 1, liveTotal);
        ShowBar();
    }

    private void Timber()
    {
        int xp = Mathf.Max(1, Mathf.RoundToInt(liveTotal * timberXpShare));
        PixelSkills skills = PixelSkills.Instance;
        PixelSkills.SkillPack pack = skills != null ? skills.Find(skillId) : null;
        if (pack != null) skills.AddXp(pack, xp);
        PixelStats.Count("woodcutting.trees_felled");
        PixelAudio.Play(timberSound);
        if (showTreeBar) PixelStatusLines.Set(BarKey, "Timber! " + liveName + " felled (+" + xp + " XP)", barColor, timberLineSeconds);
        barShown = false;
        liveName = null;
    }

    private void ShowBar()
    {
        if (!showTreeBar || liveName == null) return;
        string text = liveName + "  " + liveIndex + " / " + liveTotal
                      + (liveInfested ? "  -  infested: Termite Hive at the end (Pesticide charges: " + PixelBlockRules.Charges(PixelClicker.PixelType.HornetNest) + ")" : "");
        PixelStatusLines.Set(BarKey, text, liveInfested ? infestedColor : barColor);
        barShown = true;
    }

    private void HideBar()
    {
        liveName = null;
        if (!barShown) return;
        PixelStatusLines.Clear(BarKey);
        barShown = false;
    }
}
