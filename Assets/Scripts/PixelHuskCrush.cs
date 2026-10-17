// ---- System notes ----
// PixelHuskCrush.cs
//   HUSK CRUSH (design: docs/GameDesign.md section 6, 'Standard - the ability skill'; built 2026-10-17). Free from the first minute (not a tree
//   node): RIGHT-CLICK a husk lying on the floor and it breaks apart for a little skill XP - never currency (pixel rules: husks are never currency;
//   only the Recycler turns them into currency, at a loss). Who gets the right click is decided by PixelRightClick (the cube, the hose, the
//   conveyor kit, pets and device parts come first); this script only acts when PixelRightClick.Current is Husk.
//   - XP: xpShare (0.25) of the XP a manual click on that block gives (PixelSkills.ClickXpOf: 1 for a skill block, Standard's flat XP for a pure
//     one), to the husk's own skill via PixelSkills.AddHuskXp(crushed: true) - pure husks train Standard, wood husks Woodcutting... The crush's popup
//     always shows, small amounts with decimals (PixelSkillsPopups).
//   - CanCrush: a loose husk ON the floor's husk list that a container could take (PixelHuskHolder.Takeable: not vanishing, not held / reserved by a
//     machine - Recycler, Storage Crate, Sorting Hopper, Bait Bucket -, not riding a belt, not in the hand, not a fly-away), not a rare collectible
//     (Dragon / God / Prime Cubes, the Golden Snitch) and not in neverCrush.
//   - Feedback: shards of the husk itself (OldPixelShatter.SpawnShards, the glass shatter's burst), crushSoundId with a little pitch spread.
//   - STANDARD TREE (DefaultNodes(StandardId), bottom row, beside the twists): 'chaincrush' Chain Crush (level 4): the husks of the SAME type
//     touching the crushed one (centre within chainTouch husk widths, PixelHuskQuery.Near), and the ones touching those, crush too in quick waves
//     (chainStepSeconds), at most chainMax extra; 'cleanbreak' Clean Break (level 6): every crush gives the husks within cleanBreakRadius widths
//     cleanBreakSeconds of their lived time back (OldPixelDespawn.GiveBackTime); 'hardhands' Hard Hands (level 9): crush XP x hardHandsFactor (1.6 =
//     40% of a click).
//   - First-time tip 'husk_crush' (PixelHints) once tipAfterClicks manual clicks were made and a crushable husk lies about (not during the intro).
//   Stats: husk.crushed, husk.crushed.xp (the base XP handed to AddHuskXp, before XP multipliers), husk.crushed.chain (the extra ones Chain Crush
//   broke), husk.crushed.<skill id> / husk.crushed.xp.<skill id> (per skill); the session recorder reports them. Nothing saved.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelHuskCrush : MonoBehaviour
{
    [Header("Husk Crush")]
    [Tooltip("Off = right-clicking a husk does nothing.")]
    [SerializeField] private bool crushEnabled = true;

    [Min(0f)]
    [Tooltip("XP a crushed husk gives its skill, as a share of the XP a manual click on that block gives (0.25 = a quarter of a click). Pure husks train Standard, a skill's husks that skill. Never currency.")]
    [SerializeField] private float xpShare = 0.25f;

    [Tooltip("Pixel types whose husks can never be crushed (the rare collectibles - Dragon, God and Prime Cubes, the Golden Snitch - never can, whatever this list says).")]
    [SerializeField] private PixelClicker.PixelType[] neverCrush = new PixelClicker.PixelType[0];

    [Header("Feedback")]
    [Tooltip("Sound of a crush (a PixelAudio id).")]
    [SerializeField] private string crushSoundId = "hit";

    [Tooltip("Pitch of the crush sound (min, max: a random pitch in between each time).")]
    [SerializeField] private Vector2 crushPitch = new Vector2(1.1f, 1.35f);

    [Min(0)]
    [Tooltip("Chunks the husk breaks into.")]
    [SerializeField] private int shardCount = 6;

    [Min(0)]
    [Tooltip("Chunks each husk crushed by Chain Crush breaks into (fewer, so a big chain stays light).")]
    [SerializeField] private int chainShardCount = 3;

    [Min(0f)]
    [Tooltip("How hard the chunks fly out (world units per second).")]
    [SerializeField] private float shardSpeed = 2.2f;

    [Min(0f)]
    [Tooltip("Seconds the chunks last before they shrink away.")]
    [SerializeField] private float shardLife = 0.45f;

    [Min(0f)]
    [Tooltip("Size of a chunk, as a share of the husk's size.")]
    [SerializeField] private float shardSize = 0.4f;

    [Header("Chain Crush (Standard tree, level 4)")]
    [Min(0f)]
    [Tooltip("Husks of the same type whose centres are within this many husk widths of a crushed one count as touching it and crush too.")]
    [SerializeField] private float chainTouch = 1.25f;

    [Min(0)]
    [Tooltip("Most extra husks one crush can take with it.")]
    [SerializeField] private int chainMax = 8;

    [Min(0f)]
    [Tooltip("Seconds between the waves of a chain (each wave = the husks touching the last one).")]
    [SerializeField] private float chainStepSeconds = 0.06f;

    [Header("Clean Break (Standard tree, level 6)")]
    [Min(0f)]
    [Tooltip("Husks within this many husk widths of a crush get some of their time back.")]
    [SerializeField] private float cleanBreakRadius = 3f;

    [Min(0f)]
    [Tooltip("Seconds of lived time every crush gives back to each husk in range (never more than it has lived).")]
    [SerializeField] private float cleanBreakSeconds = 2f;

    [Header("Hard Hands (Standard tree, level 9)")]
    [Min(1f)]
    [Tooltip("Crush XP is multiplied by this (1.6 with the 0.25 share = 40% of a click: still well below a click).")]
    [SerializeField] private float hardHandsFactor = 1.6f;

    [Header("Tip")]
    [Min(0)]
    [Tooltip("The first-time tip 'Right-click a husk to crush it' shows after this many manual clicks, once a husk lies on the floor.")]
    [SerializeField] private int tipAfterClicks = 12;

    // (public: the session recorder marks when each one is bought)
    public const string ChainNode = "chaincrush", CleanNode = "cleanbreak", HardNode = "hardhands";

    private static PixelHuskCrush instance;
    private PixelClicker clicker;
    private bool tipDone;
    private float tipCheck;
    private readonly List<PixelHuskQuery.Husk> near = new List<PixelHuskQuery.Husk>();
    private readonly List<Rigidbody> chain = new List<Rigidbody>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private void Awake()
    {
        instance = this;
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Can this body be crushed by hand now? (A loose husk on the floor, not kept by a machine, not a rare collectible.)</summary>
    public static bool CanCrush(PixelClicker clicker, Rigidbody body)
    {
        PixelHuskCrush c = instance;
        if (c == null || !c.crushEnabled || clicker == null || body == null || !PixelHuskHolder.Takeable(clicker, body)) return false;
        PixelHuskQuery.Husk h = PixelHuskQuery.Get(body);
        if (!PixelHuskQuery.TypeOf(clicker, h, out PixelClicker.PixelType type) || !CrushableType(type)) return false;
        return IsOnFloor(clicker, body);
    }

    /// <summary>Husks of this type can be crushed (not a rare collectible, not in neverCrush).</summary>
    private static bool CrushableType(PixelClicker.PixelType type)
    {
        if (PixelClicker.IsRareCube(type) || PixelClicker.IsInfinityStone(type)) return false;
        return instance == null || instance.neverCrush == null || System.Array.IndexOf(instance.neverCrush, type) < 0;
    }

    private static bool IsOnFloor(PixelClicker clicker, Rigidbody body)
    {
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++) if (list[i] == body) return true;
        return false;
    }

    /// <summary>The sentence the Pixel Guide / husk tooltip adds about crushing this type's husk ("" when it can't be crushed).</summary>
    public static string GuideNote(PixelClicker.PixelType type)
    {
        if (instance != null && !instance.crushEnabled) return "";
        if (!CrushableType(type)) return "";
        PixelSkills skills = PixelSkills.Instance;
        string skill = skills != null ? skills.HuskSkillName(type) : "Standard";
        return " Right-click it to crush it for a little " + skill + " XP.";
    }

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); if (clicker == null) return; }
        UpdateTip();
        if (!crushEnabled || !PixelInput.RightPressed()) return;
        Rigidbody target = PixelRightClick.Husk;
        if (target != null) Crush(target);
    }

    /// <summary>Crushes one husk (and, with Chain Crush, the same-type husks touching it).</summary>
    public void Crush(Rigidbody body)
    {
        if (!CanCrush(clicker, body)) return;
        int tier = PixelHuskQuery.Get(body).Tier;
        if (PixelSkills.HasStandardNode(ChainNode)) CollectChain(body, tier);
        else chain.Clear();
        CrushOne(body, tier, shardCount, 1f, false);
        if (chain.Count > 0) StartCoroutine(CrushChain(tier, new List<Rigidbody>(chain)));
        chain.Clear();
    }

    /// <summary>Chain Crush: the same-type husks touching the first one, then the ones touching those (wave by wave, at most chainMax), into 'chain' in wave order.</summary>
    private void CollectChain(Rigidbody first, int tier)
    {
        chain.Clear();
        float touch = Mathf.Max(0.1f, chainTouch) * clicker.OldPixelWorldSize;
        int cap = Mathf.Max(0, chainMax);
        int from = -1;   // -1 = look round the first husk; then round each husk already in the chain
        while (chain.Count < cap && from < chain.Count)
        {
            Vector3 centre = from < 0 ? first.position : chain[from].position;
            from++;
            PixelHuskQuery.Near(clicker, centre, touch, near, false);
            for (int i = 0; i < near.Count && chain.Count < cap; i++)
            {
                Rigidbody b = near[i].body;
                if (b == first || near[i].Tier != tier || chain.Contains(b) || !CanCrush(clicker, b)) continue;
                chain.Add(b);
            }
        }
        near.Clear();
    }

    private IEnumerator CrushChain(int tier, List<Rigidbody> bodies)
    {
        float step = Mathf.Max(0f, chainStepSeconds);
        for (int i = 0; i < bodies.Count; i++)
        {
            if (step > 0f) yield return new WaitForSeconds(step);
            Rigidbody b = bodies[i];
            if (CanCrush(clicker, b)) CrushOne(b, tier, chainShardCount, 0.92f, true);   // (taken by a machine, picked up or faded meanwhile: skipped)
        }
    }

    /// <summary>One husk: XP to its skill, Clean Break round it, chunks and a crunch; the husk is gone.</summary>
    private void CrushOne(Rigidbody body, int tier, int shards, float pitchScale, bool chained)
    {
        Vector3 at = body.position;
        clicker.ReleaseOldPixel(body, false);   // (never pays: husks are not currency)
        OldPixelShatter.SpawnShards(body.gameObject, Vector3.up, shards, shardSpeed, shardLife, shardSize);
        Destroy(body.gameObject);
        if (!string.IsNullOrEmpty(crushSoundId)) PixelAudio.Play(crushSoundId, Random.Range(crushPitch.x, Mathf.Max(crushPitch.x, crushPitch.y)) * pitchScale);

        PixelSkills skills = PixelSkills.Instance;
        double xp = 0d;
        if (skills != null)
        {
            xp = skills.ClickXpOf(tier) * Mathf.Max(0f, xpShare) * (PixelSkills.HasStandardNode(HardNode) ? Mathf.Max(1f, hardHandsFactor) : 1f);
            skills.AddHuskXp(tier, xp, true);
            string skillId = skills.HuskSkillId(tier);
            PixelStats.Count("husk.crushed." + skillId);
            PixelStats.Count("husk.crushed.xp." + skillId, xp);
        }
        if (PixelSkills.HasStandardNode(CleanNode)) CleanBreak(at);
        PixelStats.Count("husk.crushed");
        PixelStats.Count("husk.crushed.xp", xp);
        if (chained) PixelStats.Count("husk.crushed.chain");
    }

    /// <summary>Clean Break: the husks lying round a crush get some of their lived time back.</summary>
    private void CleanBreak(Vector3 at)
    {
        PixelHuskQuery.Near(clicker, at, Mathf.Max(0f, cleanBreakRadius) * clicker.OldPixelWorldSize, near, false);
        for (int i = 0; i < near.Count; i++)
            if (near[i].despawn != null) near[i].despawn.GiveBackTime(cleanBreakSeconds);
        near.Clear();
    }

    /// <summary>The first-time tip, once the player has clicked a little and a crushable husk lies about (checked twice a second).</summary>
    private void UpdateTip()
    {
        if (tipDone || !crushEnabled) return;
        tipCheck -= Time.unscaledDeltaTime;
        if (tipCheck > 0f) return;
        tipCheck = 0.5f;
        if (PixelHints.IsTipSeen(TipId)) { tipDone = true; return; }
        if (PixelCalm.InMenu || PixelHints.IntroHoldsGuide) return;
        PixelStats stats = PixelFind.Cached<PixelStats>();
        if (stats == null || stats.ManualClicks < tipAfterClicks) return;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            if (!CanCrush(clicker, list[i])) continue;
            tipDone = true;
            PixelHints.Trigger(TipId);
            return;
        }
    }

    /// <summary>The id of the first-time tip (PixelHints).</summary>
    public const string TipId = "husk_crush";
}
