// ---- System notes ----
// PixelBlockRules.cs
//   The shared foundation for SPECIAL blocks and HOLD blocks (component added by PixelClicker.Awake; Instance; the two lists are [Serializable] data, defaults are added in Awake by name
//   if missing, so a copy on a scene object keeps its edits). Docs: docs/systems/PixelBlockRules.md.
//   * SPECIAL BLOCK = a rare block that REPLACES a rolled block. SpecialRule: the block's tier definition (PixelTier: name, colour, clicks, pay...), the skill it belongs to (skillId; empty =
//     any), the block types it can replace and the chance. PixelClicker.PickSpawnTier calls PixelBlockRules.TryReplace(clicker, pick) after every normal roll; the tier is created on first
//     use (PixelClicker.EnsureSpecialTier, no spawn weight of its own). Defaults: Great Oak (Woodcutting, replaces any wood block, 1% rising to 25% with 12 Acorn old pixels lying on the field - SpecialRule.hasBoost). Fishing's Whale / Bottle / Pearl still use their own
//     rolls (PixelFishingSpawns) - move them into this table when they are next touched (their chances feed the Pixel Guide's SpawnShare).
//   * HOLD BLOCK = a base block of a skill that gives a bonus while it is held (Block Hold, the right button: PixelBlockHold). HoldRule: the block type, the skill, and what the bonus does:
//     extraClicks (RAPID CLICKS like Overcharge: each manual click is followed by n extra full clicks, burstGapSeconds apart, via AutoCollect; PixelClicker.CollectInternal -> PixelBlockRules.OnManualClick), payoutMultiplier (every
//     payout, CollectInternal) and xpMultiplier (skill XP of that skill, PixelSkills.AddXp) plus a label shown beside the hold meter. Defaults: Whetstone (Woodcutting, 2 extra rapid clicks = x3), Pesticide (Woodcutting, clearsHazard: a left click clears a Hornet Nest at once, PixelClicker.CollectInternal -> ClearHazardNow).
//   HAZARD PENALTY (HazardHoldPenalty list): a hazard that GOES OFF (destroyed by clicks, PixelClicker.TriggerHazard) takes a fraction of the hold meter's current charge; default Hornet Nest 50%.
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
        [Tooltip("The chance rises while husks of the 'Boost Block' lie on the field (they just have to exist; no gathering).")]
        public bool hasBoost = false;
        [Tooltip("The block whose husks lying on the field raise the chance (e.g. Acorn).")]
        public PixelClicker.PixelType boostBlock = PixelClicker.PixelType.White;
        [Min(1)]
        [Tooltip("How many of those husks on the field give the full 'Max Chance'. Fewer give a proportional part.")]
        public int boostFullCount = 12;
        [Range(0f, 1f)]
        [Tooltip("The chance with the full number of boost husks on the field.")]
        public float maxChance = 0.25f;
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
        [Min(0)]
        [Tooltip("RAPID CLICKS (like Overcharge): while held, every manual click is followed straight away by this many extra clicks (2 = three clicks in all), each one a full click with its own payout and effects.")]
        public int extraClicks = 0;
        [Min(0.01f)]
        [Tooltip("Seconds between the clicks of the rapid burst.")]
        public float burstGapSeconds = 0.06f;
        [Min(0f)]
        [Tooltip("Every payout is multiplied by this while held (1 = no change).")]
        public float payoutMultiplier = 1f;
        [Min(0f)]
        [Tooltip("The skill's XP is multiplied by this while held (1 = no change).")]
        public float xpMultiplier = 1f;
        [Tooltip("While held, a manual left click on the hazard block below clears it at once, safely (no explosion, no XP penalty).")]
        public bool clearsHazard = false;
        [Tooltip("The hazard this block clears instantly when held (e.g. HornetNest).")]
        public PixelClicker.PixelType hazardType = PixelClicker.PixelType.HornetNest;
        [Min(0)]
        [Tooltip("CHARGES: while held, the block is used up over 'Charge Seconds' (it shrinks to nothing and is gone, it does not go back in line) and gives this many charges, one at a time. Each charge clears the 'Charge Hazard' automatically when it comes up on the cube. 0 = no charges. Letting go early keeps the charges already given and still uses the block up.")]
        public int chargesWhenHeld = 0;
        [Min(0.1f)]
        [Tooltip("Seconds of holding it takes to use the block up and give all its charges.")]
        public float chargeSeconds = 1f;
        [Tooltip("The hazard one charge clears automatically (e.g. HornetNest = the Termite Hive).")]
        public PixelClicker.PixelType chargeHazard = PixelClicker.PixelType.HornetNest;

        [Tooltip("The text shown beside the hold meter while this block is held (e.g. 'Clicks x3').")]
        public string label = "";
    }

    [Header("Special blocks (rare replacements)")]
    [SerializeField] private List<SpecialRule> specialRules = new List<SpecialRule>();

    [Header("Hold blocks (bonus while held)")]
    [SerializeField] private List<HoldRule> holdRules = new List<HoldRule>();

    [Serializable]
    public class HazardHoldPenalty
    {
        [Tooltip("The hazard block that hurts the Block Hold meter when it goes off (is destroyed by clicking it; clearing it safely with right clicks or a Pesticide does not count).")]
        public PixelClicker.PixelType hazard = PixelClicker.PixelType.HornetNest;
        [Range(0f, 1f)]
        [Tooltip("The fraction of the meter's CURRENT charge that is lost (0.5 = half of what is left).")]
        public float loseFraction = 0.5f;
    }

    [Header("Hazards that drain the hold meter")]
    [SerializeField] private List<HazardHoldPenalty> hazardPenalties = new List<HazardHoldPenalty>();

    [Tooltip("Add the built-in rules that are missing from the lists above (at start).")]
    [SerializeField] private bool addDefaultRules = true;

    /// <summary>For the Game Database: the special-block and hold-block rules.</summary>
    public IReadOnlyList<SpecialRule> SpecialRules => specialRules;
    public IReadOnlyList<HoldRule> HoldRules => holdRules;

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
                chance = 0.01f,
                hasBoost = true, boostBlock = PixelClicker.PixelType.Acorn, boostFullCount = 12, maxChance = 0.25f,   // 1%, up to 25% with 12 acorns lying on the field
                tier = new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.GreatOak, displayName = "Great Oak", color = new Color(0.36f, 0.26f, 0.15f, 1f),
                    amountPerClick = 150, clicksToCollect = 100, randomClicks = true, randomClicksMin = 50, randomClicksMax = 100, stallsHold = true, stallSeconds = 1.5f,
                    spawnWeight = 0f, spawnSwitch = PixelClicker.SpawnSwitch.AlwaysOn, unlockMode = PixelClicker.TierUnlockMode.ShopOnly,
                },
            });
        if (!holdRules.Exists(r => r != null && r.type == PixelClicker.PixelType.Whetstone))
            holdRules.Add(new HoldRule { name = "Whetstone", type = PixelClicker.PixelType.Whetstone, skillId = PixelSkills.WoodcuttingId, extraClicks = 2, label = "Rapid clicks x3" });
        if (hazardPenalties == null) hazardPenalties = new List<HazardHoldPenalty>();
        if (!hazardPenalties.Exists(h => h != null && h.hazard == PixelClicker.PixelType.HornetNest))
            hazardPenalties.Add(new HazardHoldPenalty { hazard = PixelClicker.PixelType.HornetNest, loseFraction = 0.5f });
        if (!holdRules.Exists(r => r != null && r.type == PixelClicker.PixelType.Pesticide))
            holdRules.Add(new HoldRule { name = "Pesticide", type = PixelClicker.PixelType.Pesticide, skillId = PixelSkills.WoodcuttingId, chargesWhenHeld = 3, chargeSeconds = 1f, chargeHazard = PixelClicker.PixelType.HornetNest, label = "Pesticide: 3 charges" });
        // Pesticide used to clear a hive with one click while held; now holding uses it up for 3 charges that clear hives automatically.
        foreach (HoldRule r in holdRules)
            if (r != null && r.type == PixelClicker.PixelType.Pesticide && r.chargesWhenHeld == 0)
            {
                r.clearsHazard = false; r.chargesWhenHeld = 3; r.chargeSeconds = 1f; r.chargeHazard = PixelClicker.PixelType.HornetNest; r.label = "Pesticide: 3 charges";
            }
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
            if (UnityEngine.Random.value >= EffectiveChance(clicker, r)) continue;
            int index = clicker.EnsureSpecialTier(r.tier);
            if (clicker.IsValidTierIndex(index)) return index;
        }
        return pick;
    }

    /// <summary>The special rule whose block is this type (null if none).</summary>
    public static SpecialRule RuleFor(PixelClicker.PixelType type)
    {
        PixelBlockRules rules = Instance;
        if (rules == null || rules.specialRules == null) return null;
        foreach (SpecialRule r in rules.specialRules) if (r != null && r.tier != null && r.tier.type == type) return r;
        return null;
    }

    /// <summary>Makes sure a special block's tier exists (first use) and returns its index, or -1 if the block has no rule. Tree mode uses it for the Great Oak species.</summary>
    public static int EnsureSpecialBlock(PixelClicker clicker, PixelClicker.PixelType type)
    {
        SpecialRule r = RuleFor(type);
        return r != null && clicker != null ? clicker.EnsureSpecialTier(r.tier) : -1;
    }

    /// <summary>The rule's chance right now: its base chance, raised towards maxChance by the old pixels of the boost block lying on the field.</summary>
    public static float EffectiveChance(PixelClicker clicker, SpecialRule r)
    {
        if (r == null) return 0f;
        if (!r.hasBoost || clicker == null) return r.chance;
        int count = PixelHuskQuery.CountOfType(clicker, r.boostBlock);
        float t = Mathf.Clamp01(count / (float)Mathf.Max(1, r.boostFullCount));
        return Mathf.Lerp(r.chance, Mathf.Max(r.chance, r.maxChance), t);
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

    // ---- Charges (held blocks that are used up for charges: Pesticide) - kept as stat counters 'charges.<Hazard>', so they are saved ----

    /// <summary>How many charges against this hazard the player has.</summary>
    public static int Charges(PixelClicker.PixelType hazard) => Mathf.Max(0, (int)PixelStats.Total("charges." + hazard));

    /// <summary>Adds charges against a hazard (holding a charge block).</summary>
    public static void AddCharges(PixelClicker.PixelType hazard, int count) { if (count > 0) PixelStats.Count("charges." + hazard, count); }

    /// <summary>Uses one charge against this hazard; false when there is none.</summary>
    public static bool UseCharge(PixelClicker.PixelType hazard)
    {
        int now = Charges(hazard);
        if (now <= 0) return false;
        PixelStats.Clear("charges." + hazard);
        if (now > 1) PixelStats.Count("charges." + hazard, now - 1);
        return true;
    }

    /// <summary>True while a held hold block clears this hazard type with one left click (HoldRule.clearsHazard).</summary>
    public static bool HeldClearsHazard(PixelClicker.PixelType hazard)
    {
        HoldRule r = ActiveHold;
        return r != null && r.clearsHazard && r.hazardType == hazard;
    }

    private int pendingExtra;

    /// <summary>Called by PixelClicker for every manual click: while a hold block with rapid clicks is held, the click is followed by a quick burst of extra clicks (the burst's own clicks are automatic and never start another).</summary>
    public static void OnManualClick(PixelClicker clicker)
    {
        PixelBlockRules rules = Instance;
        if (rules == null || clicker == null) return;
        HoldRule r = ActiveHold;
        if (r == null || r.extraClicks <= 0 || rules.pendingExtra > 24) return;   // (a cap, so mashing the button cannot pile up a huge queue)
        rules.StartCoroutine(rules.RapidBurst(clicker, r.extraClicks, Mathf.Max(0.01f, r.burstGapSeconds)));
    }

    private System.Collections.IEnumerator RapidBurst(PixelClicker clicker, int count, float gap)
    {
        pendingExtra += count;
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(gap);
            pendingExtra--;
            if (clicker == null) continue;
            if (clicker.CubeHidden || PixelMinigame.TakeoverActive) continue;
            clicker.AutoCollect();
        }
    }

    /// <summary>Multiplier on every payout while a hold block is held.</summary>
    public static double HeldPayoutFactor { get { HoldRule r = ActiveHold; return r != null && r.payoutMultiplier > 0f ? r.payoutMultiplier : 1d; } }

    /// <summary>Multiplier on the XP of this skill while a hold block is held.</summary>
    public static double HeldXpFactor(string skillId)
    {
        HoldRule r = ActiveHold;
        if (r == null || r.xpMultiplier <= 0f) return 1d;
        return string.IsNullOrEmpty(r.skillId) || r.skillId == skillId ? r.xpMultiplier : 1d;
    }

    /// <summary>A hazard went off (it was destroyed by clicking): the Block Hold meter may lose part of its charge (HazardHoldPenalty).</summary>
    public static void OnHazardWentOff(PixelClicker.PixelType hazard)
    {
        PixelBlockRules rules = Instance;
        if (rules == null || PixelBlockHold.Instance == null) return;
        foreach (HazardHoldPenalty h in rules.hazardPenalties)
            if (h != null && h.hazard == hazard && h.loseFraction > 0f) PixelBlockHold.Instance.LoseFraction(h.loseFraction);
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
            if (s.hasBoost)
                sb.Append(" Every ").Append(s.boostBlock.ToString().ToLowerInvariant()).Append(" husk lying on the field raises it (they only have to be there): with ").Append(s.boostFullCount).Append(" of them out it is ").Append(Pct(s.maxChance)).Append(".");
        }
        if (TryGetHold(type, out HoldRule h))
        {
            sb.Append("\n\n<b>Hold block:</b> hold it with Block Hold (the right button on the cube) to pull it out of the rotation. While you hold it: ");
            var parts = new System.Collections.Generic.List<string>();
            if (h.extraClicks > 0) parts.Add("every click is followed by " + h.extraClicks + " extra rapid click" + (h.extraClicks > 1 ? "s" : "") + " (" + (h.extraClicks + 1) + " clicks at once, like an overcharge)");
            if (h.payoutMultiplier > 0f && !Mathf.Approximately(h.payoutMultiplier, 1f)) parts.Add("every payout is x" + h.payoutMultiplier.ToString("0.##"));
            if (h.xpMultiplier > 0f && !Mathf.Approximately(h.xpMultiplier, 1f)) parts.Add((string.IsNullOrEmpty(h.skillId) ? "skill" : h.skillId) + " XP is x" + h.xpMultiplier.ToString("0.##"));
            if (h.clearsHazard) parts.Add("a left click clears a " + (h.hazardType == PixelClicker.PixelType.HornetNest ? "termite hive" : h.hazardType.ToString().ToLowerInvariant()) + " at once, safely");
            sb.Append(parts.Count > 0 ? string.Join(", ", parts) : "it gives a bonus").Append(". The bonus lasts exactly as long as you hold it.");
        }
        if (PixelBlockHold.IsStakeBlock(type)) sb.Append(PixelBlockHold.StakeNote());   // the basic pixels can carry a stake (Block Hold)
        return sb.ToString();
    }
}
