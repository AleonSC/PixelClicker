// ---- System notes ----
// PixelBlockRules.cs
//   The shared foundation for SPECIAL blocks and HOLD blocks (component added by PixelClicker.Awake; Instance; the two lists are [Serializable] data, defaults are added in Awake by name
//   if missing, so a copy on a scene object keeps its edits). Docs: docs/systems/PixelBlockRules.md.
//   * SPECIAL BLOCK = a rare block that REPLACES a rolled block. SpecialRule: the block's tier definition (PixelTier: name, colour, clicks, pay...), the skill it belongs to (skillId; empty =
//     any), the block types it can replace and the chance. PixelClicker.PickSpawnTier calls PixelBlockRules.TryReplace(clicker, pick) after every normal roll; the tier is created on first
//     use (PixelClicker.EnsureSpecialTier, no spawn weight of its own). Defaults: Great Oak (Woodcutting, replaces any wood block, 5%). Fishing's Whale / Bottle / Pearl still use their own
//     rolls (PixelFishingSpawns) - move them into this table when they are next touched (their chances feed the Pixel Guide's SpawnShare).
//   * HOLD BLOCK = a base block of a skill that gives a bonus while it is held (Block Hold, the right button: PixelBlockHold). HoldRule: the block type, the skill, and what the bonus does:
//     clickMultiplier (every manual click counts as n clicks: damage on tough blocks and payout; PixelBlockHold.ClickFactor -> PixelClicker.CollectInternal), payoutMultiplier (every
//     payout, CollectInternal) and xpMultiplier (skill XP of that skill, PixelSkills.AddXp) plus a label shown beside the hold meter. Defaults: Whetstone (Woodcutting, clicks x3).
//   Add a new special / hold block: give it a PixelType (append), a look and spawn data like any block (hold blocks are normal base blocks of their pack: shop pack reward tier + the skill's
//   types list), then add ONE rule here. Behaviour beyond the numbers (the Great Oak's nests) still lives in its own script and listens to PixelClicker.PixelHit.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelBlockRules : MonoBehaviour
{
    [Serializable]
    public class SpecialRule
    {
        [Tooltip("Name for the Inspector list only.")]
        public string name = "";
        [Tooltip("The special block's tier (name, colour, clicks to collect, pay...). It has no spawn weight: it only comes from replacing another block.")]
        public PixelClicker.PixelTier tier = new PixelClicker.PixelTier();
        [Tooltip("The skill whose blocks it replaces (a skill id like Woodcutting; empty = whatever skill is in play).")]
        public string skillId = "";
        [Tooltip("Rolled blocks of these types may be replaced.")]
        public PixelClicker.PixelType[] replaces = new PixelClicker.PixelType[0];
        [Range(0f, 1f)]
        [Tooltip("Chance a rolled block of those types becomes this special block.")]
        public float chance = 0.05f;
    }

    [Serializable]
    public class HoldRule
    {
        [Tooltip("Name for the Inspector list only.")]
        public string name = "";
        [Tooltip("The block that gives the bonus while held (Block Hold, right button).")]
        public PixelClicker.PixelType type = PixelClicker.PixelType.White;
        [Tooltip("The skill the bonus belongs to (a skill id; empty = any). The XP multiplier only affects this skill's XP.")]
        public string skillId = "";
        [Min(1)]
        [Tooltip("Every manual click counts as this many clicks while held (1 = no change).")]
        public int clickMultiplier = 1;
        [Min(0f)]
        [Tooltip("Every payout is multiplied by this while held (1 = no change).")]
        public float payoutMultiplier = 1f;
        [Min(0f)]
        [Tooltip("The skill's XP is multiplied by this while held (1 = no change).")]
        public float xpMultiplier = 1f;
        [Tooltip("The text shown beside the hold meter while this block is held (e.g. 'Clicks x3').")]
        public string label = "";
    }

    [Header("Special blocks (rare replacements)")]
    [SerializeField] private List<SpecialRule> specialRules = new List<SpecialRule>();

    [Header("Hold blocks (bonus while held)")]
    [SerializeField] private List<HoldRule> holdRules = new List<HoldRule>();

    [Tooltip("Add the built-in rules that are missing from the lists above (at start).")]
    [SerializeField] private bool addDefaultRules = true;

    public static PixelBlockRules Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    private void Awake()
    {
        Instance = this;
        if (addDefaultRules) AddMissingDefaults();
    }

    [ContextMenu("Add Missing Default Rules")]
    private void AddMissingDefaults()
    {
        if (specialRules == null) specialRules = new List<SpecialRule>();
        if (holdRules == null) holdRules = new List<HoldRule>();
        if (!specialRules.Exists(r => r != null && r.tier != null && r.tier.type == PixelClicker.PixelType.GreatOak))
            specialRules.Add(new SpecialRule
            {
                name = "Great Oak",
                skillId = PixelSkills.WoodcuttingId,
                replaces = new[] { PixelClicker.PixelType.Wood, PixelClicker.PixelType.Bark, PixelClicker.PixelType.Sap, PixelClicker.PixelType.Leaf, PixelClicker.PixelType.Heartwood, PixelClicker.PixelType.Twig },
                chance = 0.05f,
                tier = new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.GreatOak, displayName = "Great Oak", color = new Color(0.36f, 0.26f, 0.15f, 1f),
                    amountPerClick = 150, clicksToCollect = 100, randomClicks = true, randomClicksMin = 50, randomClicksMax = 100,
                    spawnWeight = 0f, spawnSwitch = PixelClicker.SpawnSwitch.AlwaysOn, unlockMode = PixelClicker.TierUnlockMode.ShopOnly,
                },
            });
        if (!holdRules.Exists(r => r != null && r.type == PixelClicker.PixelType.Whetstone))
            holdRules.Add(new HoldRule { name = "Whetstone", type = PixelClicker.PixelType.Whetstone, skillId = PixelSkills.WoodcuttingId, clickMultiplier = 3, label = "Clicks x3" });
    }

    // ---------------- special blocks ----------------

    /// <summary>Called by the spawn roll: returns 'pick' unchanged, or the index of a special block that replaces it.</summary>
    public static int TryReplace(PixelClicker clicker, int pick)
    {
        PixelBlockRules rules = Instance;
        if (rules == null || clicker == null || !clicker.IsValidTierIndex(pick)) return pick;
        PixelClicker.PixelType type = clicker.Tiers[pick].type;
        PixelSkills skills = PixelSkills.Instance;
        string active = skills != null ? skills.ActiveId : "";
        foreach (SpecialRule r in rules.specialRules)
        {
            if (r == null || r.tier == null || r.replaces == null || r.chance <= 0f || r.tier.type == type) continue;
            if (!string.IsNullOrEmpty(r.skillId) && r.skillId != active) continue;
            if (Array.IndexOf(r.replaces, type) < 0) continue;
            if (UnityEngine.Random.value >= r.chance) continue;
            int index = clicker.EnsureSpecialTier(r.tier);
            if (clicker.IsValidTierIndex(index)) return index;
        }
        return pick;
    }

    // ---------------- hold blocks ----------------

    /// <summary>The rule of the block being held right now (null when nothing is held, or the held block has no bonus, or its skill is not in play).</summary>
    public static HoldRule ActiveHold
    {
        get
        {
            PixelBlockRules rules = Instance;
            PixelBlockHold hold = PixelBlockHold.Instance;
            if (rules == null || hold == null) return null;
            if (!hold.TryGetHeldType(out PixelClicker.PixelType held)) return null;
            PixelSkills skills = PixelSkills.Instance;
            string active = skills != null ? skills.ActiveId : "";
            foreach (HoldRule r in rules.holdRules)
                if (r != null && r.type == held && (string.IsNullOrEmpty(r.skillId) || r.skillId == active)) return r;
            return null;
        }
    }

    /// <summary>How many clicks one manual click counts as (1 normally).</summary>
    public static int HeldClickFactor { get { HoldRule r = ActiveHold; return r != null ? Mathf.Max(1, r.clickMultiplier) : 1; } }

    /// <summary>Multiplier on every payout while a hold block is held.</summary>
    public static double HeldPayoutFactor { get { HoldRule r = ActiveHold; return r != null && r.payoutMultiplier > 0f ? r.payoutMultiplier : 1d; } }

    /// <summary>Multiplier on the XP of this skill while a hold block is held.</summary>
    public static double HeldXpFactor(string skillId)
    {
        HoldRule r = ActiveHold;
        if (r == null || r.xpMultiplier <= 0f) return 1d;
        return string.IsNullOrEmpty(r.skillId) || r.skillId == skillId ? r.xpMultiplier : 1d;
    }

    // ---------------- the Pixel Guide (and anything else that lists blocks) ----------------

    /// <summary>The special rule whose block this is (false if it is not a special block).</summary>
    public static bool TryGetSpecial(PixelClicker.PixelType type, out SpecialRule rule)
    {
        rule = null;
        PixelBlockRules rules = Instance;
        if (rules == null) return false;
        foreach (SpecialRule r in rules.specialRules) if (r != null && r.tier != null && r.tier.type == type) { rule = r; return true; }
        return false;
    }

    /// <summary>The hold rule of this block (false if it gives no bonus when held).</summary>
    public static bool TryGetHold(PixelClicker.PixelType type, out HoldRule rule)
    {
        rule = null;
        PixelBlockRules rules = Instance;
        if (rules == null) return false;
        foreach (HoldRule r in rules.holdRules) if (r != null && r.type == type) { rule = r; return true; }
        return false;
    }

    /// <summary>Makes sure the tiers of the special blocks of the skill in play exist, so the Pixel Guide can list them before the first one has come up.</summary>
    public static void EnsureActiveSpecialTiers(PixelClicker clicker)
    {
        PixelBlockRules rules = Instance;
        PixelSkills skills = PixelSkills.Instance;
        if (rules == null || clicker == null || skills == null) return;
        foreach (SpecialRule r in rules.specialRules)
        {
            if (r == null || r.tier == null || r.chance <= 0f) continue;
            if (!string.IsNullOrEmpty(r.skillId) && r.skillId != skills.ActiveId) continue;
            if (clicker.IndexOf(r.tier.type) < 0) clicker.EnsureSpecialTier(r.tier);
        }
    }

    /// <summary>The spawn share of block i with the special rules worked in: a special block gets chance x the share of the blocks it replaces, and those blocks give up that part.</summary>
    public static float AdjustShare(PixelClicker clicker, int i, float baseShare)
    {
        PixelBlockRules rules = Instance;
        PixelSkills skills = PixelSkills.Instance;
        if (rules == null || skills == null || !clicker.IsValidTierIndex(i)) return baseShare;
        PixelClicker.PixelType type = clicker.Tiers[i].type;
        foreach (SpecialRule r in rules.specialRules)
        {
            if (r == null || r.tier == null || r.replaces == null || r.chance <= 0f) continue;
            if (!string.IsNullOrEmpty(r.skillId) && r.skillId != skills.ActiveId) continue;
            if (r.tier.type == type)
            {
                float sum = 0f;
                foreach (PixelClicker.PixelType t in r.replaces) { int k = clicker.IndexOf(t); if (k >= 0) sum += clicker.SpawnShareBase(k); }
                return sum * r.chance;
            }
            if (Array.IndexOf(r.replaces, type) >= 0) baseShare *= 1f - r.chance;
        }
        return baseShare;
    }

    private static string Pct(float f) => (f * 100f >= 10f ? Mathf.RoundToInt(f * 100f).ToString() : (f * 100f).ToString("0.#")) + "%";

    /// <summary>The sentences the Pixel Guide adds to a block's description for its special / hold role ("" for ordinary blocks).</summary>
    public static string GuideNotes(PixelClicker.PixelType type)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (TryGetSpecial(type, out SpecialRule s))
        {
            sb.Append("\n\n<b>Special block:</b> every ");
            for (int i = 0; i < s.replaces.Length; i++) sb.Append(i > 0 ? (i == s.replaces.Length - 1 ? " or " : ", ") : "").Append(s.replaces[i].ToString().ToLowerInvariant());
            sb.Append(" block that comes up has a ").Append(Pct(s.chance)).Append(" chance of being this one instead.");
        }
        if (TryGetHold(type, out HoldRule h))
        {
            sb.Append("\n\n<b>Hold block:</b> hold it with Block Hold (the right button on the cube) to pull it out of the rotation. While you hold it: ");
            var parts = new System.Collections.Generic.List<string>();
            if (h.clickMultiplier > 1) parts.Add("every click counts as " + h.clickMultiplier + " clicks");
            if (h.payoutMultiplier > 0f && !Mathf.Approximately(h.payoutMultiplier, 1f)) parts.Add("every payout is x" + h.payoutMultiplier.ToString("0.##"));
            if (h.xpMultiplier > 0f && !Mathf.Approximately(h.xpMultiplier, 1f)) parts.Add((string.IsNullOrEmpty(h.skillId) ? "skill" : h.skillId) + " XP is x" + h.xpMultiplier.ToString("0.##"));
            sb.Append(parts.Count > 0 ? string.Join(", ", parts) : "it gives a bonus").Append(". The bonus lasts exactly as long as you hold it.");
        }
        return sb.ToString();
    }
}
