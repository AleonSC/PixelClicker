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
//   - CHARGE CRUSH ('chargecrush', level 7, needs Chain Crush; row 4 of the tree): a quick CLICK on a husk is still the crush above (Chain Crush
//     included) - with the node owned the crush happens when the button comes UP, so a press can still turn into a hold. HOLDING right on a husk
//     for holdDelay (0.25 s) starts a charge (mirrors Power Click): a ring on the floor round the husk grows from startReach to maxReach husk widths
//     over chargeSeconds, with the rising 'remove_buildup' whine (stretched to the charge time, like PixelHold) and Power Click's 'snitch_ready'
//     ding + a gold pulsing ring at full. Letting go (any time after the charge started) crushes the held husk and sends an arc (a short jagged
//     line) to every husk of the SAME type inside the ring, touching or not, nearest first (arcStepSeconds apart, at most maxTargets); every one is
//     a normal crush (same XP rules, Clean Break, never currency). Then a cooldown (cooldownSeconds 8): holding a husk meanwhile shows a grey
//     ring of the time left and letting go just crushes it like a click. Whose press it is comes from PixelRightClick.PressOwner (a press that
//     started on a husk stays the husk's while held, so Block Hold never starts from it). A charge is dropped (no cooldown) in a menu, a takeover
//     minigame, first person or frozen time. Stat husk.chargecrush.
//   - First-time tip 'husk_crush' (PixelHints) once tipAfterClicks manual clicks were made and a crushable husk lies about (not during the intro).
//   Stats: husk.crushed, husk.crushed.xp (the base XP handed to AddHuskXp, before XP multipliers), husk.crushed.chain (the extra ones Chain Crush
//   broke), husk.crushed.charge (the ones a Charge Crush release broke, the held one included), husk.crushed.<skill id> / husk.crushed.xp.<skill id> (per skill); the session recorder reports them. Nothing saved.
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

    [Header("Charge Crush (Standard tree, level 7)")]
    [Min(0.05f)]
    [Tooltip("Seconds you must keep holding right on a husk before the charge starts (a shorter press is a normal click crush).")]
    [SerializeField] private float holdDelay = 0.25f;

    [Min(0.1f)]
    [Tooltip("Seconds of holding for the ring to reach its full size.")]
    [SerializeField] private float chargeSeconds = 1.5f;

    [Min(0f)]
    [Tooltip("Radius of the ring when the charge starts, in husk widths.")]
    [SerializeField] private float startReach = 1.5f;

    [Min(0.5f)]
    [Tooltip("Radius of the ring at full charge (the most it can reach), in husk widths.")]
    [SerializeField] private float maxReach = 8f;

    [Min(0f)]
    [Tooltip("Seconds after a charged crush before the next charge can start (a click crush works meanwhile).")]
    [SerializeField] private float cooldownSeconds = 8f;

    [Min(1)]
    [Tooltip("Most husks one charged crush can take (the nearest ones first).")]
    [SerializeField] private int maxTargets = 40;

    [Min(0f)]
    [Tooltip("Seconds between two arcs of a charged crush (nearest husk first).")]
    [SerializeField] private float arcStepSeconds = 0.035f;

    [Min(0.05f)]
    [Tooltip("Seconds an arc stays visible while it fades.")]
    [SerializeField] private float arcSeconds = 0.25f;

    [Tooltip("Colour of the ring while it grows.")]
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.85f);

    [Tooltip("Colour of the ring at full charge, and of the arcs.")]
    [SerializeField] private Color readyColor = new Color(1f, 0.82f, 0.25f, 1f);

    [Tooltip("Colour of the ring that shows the cooldown left (shown while you hold a husk during the cooldown).")]
    [SerializeField] private Color cooldownColor = new Color(0.6f, 0.65f, 0.75f, 0.6f);

    [Header("Tip")]
    [Min(0)]
    [Tooltip("The first-time tip 'Right-click a husk to crush it' shows after this many manual clicks, once a husk lies on the floor.")]
    [SerializeField] private int tipAfterClicks = 12;

    // (public: the session recorder marks when each one is bought)
    public const string ChainNode = "chaincrush", CleanNode = "cleanbreak", HardNode = "hardhands", ChargeNode = "chargecrush";
    private const string BuildupSound = "remove_buildup";

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
        if (charging) PixelAudio.Stop(BuildupSound);
        if (lineMaterial != null) Destroy(lineMaterial);
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
        return " Right-click it to crush it for a little " + skill + " XP." + (PixelSkills.HasStandardNode(ChargeNode) ? " Hold right on it to charge a Charge Crush: every husk of its type inside the ring is crushed too." : "");
    }

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); if (clicker == null) return; }
        if (cooldownLeft > 0f && !PixelCalm.InMenu) cooldownLeft -= Time.unscaledDeltaTime;
        UpdateTip();
        UpdateArcs();
        if (!crushEnabled) { CancelCharge(); return; }
        if (state == ChargeState.Idle)
        {
            if (!PixelInput.RightPressed() || PixelRightClick.PressOwner != PixelRightClick.Owner.Husk) { ShowRing(false); return; }
            Rigidbody target = PixelRightClick.Husk;
            if (target == null) return;
            if (!PixelSkills.HasStandardNode(ChargeNode)) { Crush(target); return; }   // no Charge Crush: the crush happens on the press
            state = ChargeState.Pressed;   // with it, wait: a quick release is a click crush, a hold becomes a charge
            pressTarget = target;
            pressTime = Time.unscaledTime;
            chargeTier = PixelHuskQuery.Get(target).Tier;
            chargeCentre = target.position;
            return;
        }
        UpdateCharge();
    }

    // ------------------------------------------------------------------
    // Charge Crush: hold right on a husk, a ring grows, let go and arcs crush every same-type husk inside it
    // ------------------------------------------------------------------

    private enum ChargeState { Idle, Pressed, Charging }
    private ChargeState state = ChargeState.Idle;
    private Rigidbody pressTarget;
    private float pressTime, charge, cooldownLeft;
    private int chargeTier = -1;
    private Vector3 chargeCentre;
    private bool charging, fullDinged;

    private float ChargeTime => Mathf.Max(0.1f, chargeSeconds);
    private float ReachWorld => Mathf.Lerp(Mathf.Max(0f, startReach), Mathf.Max(startReach, maxReach), charge) * clicker.OldPixelWorldSize;

    /// <summary>Things that drop a charge: menus, takeover minigames, first person, time frozen by something other than Time Stop.</summary>
    private static bool ChargeBlocked() =>
        PixelCalm.InMenu || PixelMinigame.TakeoverActive || PixelFirstPerson.Active || (Time.timeScale <= 0f && !PixelTimeStop.IsStopped);

    private void UpdateCharge()
    {
        if (ChargeBlocked()) { CancelCharge(); return; }
        if (pressTarget != null) chargeCentre = pressTarget.position;   // (the ring follows the husk while it rolls; it stays put once the husk is gone)
        if (state == ChargeState.Pressed)
        {
            if (!PixelInput.RightHeld())   // a click: the normal crush (Chain Crush included)
            {
                Rigidbody target = pressTarget;
                CancelCharge();
                if (target != null) Crush(target);
                return;
            }
            if (Time.unscaledTime - pressTime < holdDelay) return;
            if (cooldownLeft > 0f) { ShowCooldownRing(); return; }   // still cooling down: letting go is a click crush
            state = ChargeState.Charging;
            charge = 0f;
            charging = true;
            fullDinged = false;
            PixelAudio.Play(BuildupSound, PixelSynth.BuildupSeconds / ChargeTime);   // the rising whine, stretched to last the whole charge
        }

        if (!PixelInput.RightHeld()) { FireCharge(); return; }
        charge = Mathf.Clamp01(charge + Time.unscaledDeltaTime / ChargeTime);
        if (charge >= 1f && !fullDinged) { fullDinged = true; PixelAudio.Play("snitch_ready", 1.3f); }   // full: Power Click's ready ding
        ShowChargeRing();
    }

    /// <summary>Drops the press / charge without doing anything (no cooldown).</summary>
    private void CancelCharge()
    {
        if (charging) PixelAudio.Stop(BuildupSound);
        charging = false;
        state = ChargeState.Idle;
        pressTarget = null;
        charge = 0f;
        ShowRing(false);
    }

    /// <summary>Let go after charging: the held husk and every husk of its type inside the ring are crushed, nearest first, each reached by an arc.</summary>
    private void FireCharge()
    {
        float reach = ReachWorld;
        Vector3 centre = chargeCentre;
        int tier = chargeTier;
        Rigidbody first = pressTarget;
        CancelCharge();
        cooldownLeft = Mathf.Max(0f, cooldownSeconds);

        List<Rigidbody> targets = new List<Rigidbody>();
        if (CanCrush(clicker, first)) targets.Add(first);
        PixelHuskQuery.Near(clicker, centre, reach, near, true);
        near.Sort((a, b) => (a.body.position - centre).sqrMagnitude.CompareTo((b.body.position - centre).sqrMagnitude));
        int cap = Mathf.Max(1, maxTargets);
        for (int i = 0; i < near.Count && targets.Count < cap; i++)
        {
            Rigidbody b = near[i].body;
            if (b != first && near[i].Tier == tier && CanCrush(clicker, b)) targets.Add(b);
        }
        near.Clear();
        PixelAudio.Play("overcharge", 1.4f);   // the release zap (Power Click's fire sound, a little higher)
        PixelStats.Count("husk.chargecrush");
        if (targets.Count > 0) StartCoroutine(ArcCrush(centre, tier, targets));
    }

    private IEnumerator ArcCrush(Vector3 centre, int tier, List<Rigidbody> targets)
    {
        float step = Mathf.Max(0f, arcStepSeconds);
        for (int i = 0; i < targets.Count; i++)
        {
            Rigidbody b = targets[i];
            if (!CanCrush(clicker, b)) continue;   // (taken by a machine, picked up or faded meanwhile)
            if (i > 0 && step > 0f) yield return new WaitForSeconds(step);
            if (!CanCrush(clicker, b)) continue;
            if ((b.position - centre).sqrMagnitude > 0.0001f) ShowArc(centre, b.position);
            CrushOne(b, tier, i == 0 ? shardCount : chainShardCount, 1f + 0.02f * Mathf.Min(i, 15), "husk.crushed.charge");   // (the crunches climb a little, like a streak)
        }
    }

    // ---- Look: the floor ring and the arcs (world-space lines, Sprites/Default) ----

    private const int RingPoints = 64;
    private Material lineMaterial;
    private LineRenderer ring;

    private struct Arc { public LineRenderer line; public float age; }
    private readonly List<Arc> arcs = new List<Arc>();
    private readonly List<LineRenderer> spareArcs = new List<LineRenderer>();

    private LineRenderer MakeLine(string name)
    {
        if (lineMaterial == null) lineMaterial = new Material(PixelShaders.SpriteDefault());
        GameObject go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.GetComponent<LineRenderer>();
        lr.sharedMaterial = lineMaterial;
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 3;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    private void ShowRing(bool show)
    {
        if (ring != null && ring.gameObject.activeSelf != show) ring.gameObject.SetActive(show);
    }

    /// <summary>The ring on the floor round the charging husk: its radius is the reach, it brightens to gold and pulses at full.</summary>
    private void ShowChargeRing()
    {
        float pulse = charge >= 1f ? 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 18f) : 1f;
        Color c = charge >= 1f ? readyColor : Color.Lerp(ringColor, readyColor, charge * 0.5f);
        DrawRing(ReachWorld * pulse, 1f, c, charge >= 1f ? 0.09f : 0.06f);
    }

    /// <summary>During the cooldown: a small grey ring round the held husk, as much of it drawn as there is cooldown left.</summary>
    private void ShowCooldownRing()
    {
        float left = cooldownSeconds > 0f ? Mathf.Clamp01(cooldownLeft / cooldownSeconds) : 0f;
        DrawRing(Mathf.Max(1f, startReach) * clicker.OldPixelWorldSize, left, cooldownColor, 0.04f);
    }

    private void DrawRing(float radius, float fill, Color color, float widthInHusks)
    {
        if (ring == null) ring = MakeLine("Charge Crush Ring");
        ShowRing(true);
        int points = Mathf.Max(2, Mathf.CeilToInt(RingPoints * Mathf.Clamp01(fill)) + 1);
        bool whole = fill >= 0.999f;
        ring.loop = whole;
        ring.positionCount = whole ? RingPoints : points;
        float y = chargeCentre.y - clicker.OldPixelWorldSize * 0.45f;   // about the bottom of the husk: on the floor it lies on
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = (whole ? i / (float)RingPoints : i / (float)(points - 1) * fill) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(chargeCentre.x + Mathf.Cos(a) * radius, y, chargeCentre.z + Mathf.Sin(a) * radius));
        }
        ring.startWidth = ring.endWidth = clicker.OldPixelWorldSize * widthInHusks;
        ring.startColor = ring.endColor = color;
    }

    /// <summary>A short jagged arc from the charge centre to a husk; it fades over arcSeconds (UpdateArcs).</summary>
    private void ShowArc(Vector3 from, Vector3 to)
    {
        LineRenderer lr;
        if (spareArcs.Count > 0) { lr = spareArcs[spareArcs.Count - 1]; spareArcs.RemoveAt(spareArcs.Count - 1); lr.gameObject.SetActive(true); }
        else lr = MakeLine("Charge Crush Arc");
        const int n = 8;
        lr.loop = false;
        lr.positionCount = n;
        float size = clicker.OldPixelWorldSize;
        float lift = Mathf.Min((to - from).magnitude * 0.25f, size * 2f);   // it arcs up a little on the way
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * lift;
            if (i > 0 && i < n - 1) p += Random.insideUnitSphere * size * 0.25f;   // jagged, like a spark
            lr.SetPosition(i, p);
        }
        lr.startWidth = size * 0.08f;
        lr.endWidth = size * 0.04f;
        lr.startColor = lr.endColor = readyColor;
        arcs.Add(new Arc { line = lr, age = 0f });
    }

    private void UpdateArcs()
    {
        float life = Mathf.Max(0.05f, arcSeconds);
        for (int i = arcs.Count - 1; i >= 0; i--)
        {
            Arc a = arcs[i];
            a.age += Time.unscaledDeltaTime;
            if (a.line == null) { arcs.RemoveAt(i); continue; }
            if (a.age >= life) { a.line.gameObject.SetActive(false); spareArcs.Add(a.line); arcs.RemoveAt(i); continue; }
            Color c = readyColor;
            c.a *= 1f - a.age / life;
            a.line.startColor = a.line.endColor = c;
            arcs[i] = a;
        }
    }

    /// <summary>Crushes one husk (and, with Chain Crush, the same-type husks touching it).</summary>
    public void Crush(Rigidbody body)
    {
        if (!CanCrush(clicker, body)) return;
        int tier = PixelHuskQuery.Get(body).Tier;
        if (PixelSkills.HasStandardNode(ChainNode)) CollectChain(body, tier);
        else chain.Clear();
        CrushOne(body, tier, shardCount, 1f, null);
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
            if (CanCrush(clicker, b)) CrushOne(b, tier, chainShardCount, 0.92f, "husk.crushed.chain");   // (taken by a machine, picked up or faded meanwhile: skipped)
        }
    }

    /// <summary>One husk: XP to its skill, Clean Break round it, chunks and a crunch; the husk is gone.</summary>
    private void CrushOne(Rigidbody body, int tier, int shards, float pitchScale, string howStat)   // howStat: null = a plain crush, else the stat counting how it was crushed
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
        if (howStat != null) PixelStats.Count(howStat);
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
