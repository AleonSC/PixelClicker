// ---- System notes ----
// PixelGoals.cs
//   DATA-DRIVEN GOALS for the Log's Goals tab (2026-10-18, design docs/GameDesign.md section 6 'Early game feel: click, pile, crush' point 2:
//   goals ALTERNATE clicking and husks, so crushing feels on-task). ONE table (goals, editable in the Inspector; the built-in rows are added by
//   id when missing - DefaultGoals) instead of one hand-written goal each. A row = id, title, description, what it counts, target, when it
//   APPEARS, and a small REWARD paid by the goal (skill XP and / or a little currency - never by a husk).
//   - Counter: StatSinceShown (a PixelStats counter, counted from the moment the goal appeared), StatValue (a counter as it is, e.g. a best
//     value like husk.crush.streak) or ManualClicksSinceShown.
//   - Appears: Always, HusksOnFloor (at least appearValue husks lying out), StatAtLeast (appearKey >= appearValue, e.g. guide.stage1 = Cubie's
//     visit 1 handed over the Recycler) or MinutesPlayed. Never during the new-game intro or on the title screen. Built-in rows: Click 50 times
//     (start), Crush 10 husks (20 husks lying out), Recycle 20 husks (after Cubie's Recycler), Get a crush streak of 5 (minute 3).
//   - State is saved per save in PixelStats counters: goal.<id>.shown, goal.<id>.start (the counter when it appeared), goal.<id>.done.
//     Checked every checkSeconds; on completion the reward is paid (PixelSkills.AddRewardXp / PixelClicker.AddBonusCurrency), the event log says
//     so with a sound, and the session recorder notes it on its timeline. A finished goal stays in the tab as complete for showDoneSeconds.
//   - PixelLog.CollectGoals shows the active ones right after the next-pixel-unlock goal (the hand-built click / unlock goals stay as they are);
//     the Game Database lists every row (category 'Goal', discovered once it has appeared).
//   THE EARLY GOALS ARE THE UNLOCKS (2026-10-18, design 'Early game feel' item 2): rewards can also UNLOCK A TIER (rewardUnlocksTier + rewardTier;
//   the tier's TierUnlockMode is ByGoal so its threshold never unlocks it while a goal does - PixelClicker.CheckUnlocks) and START A CUBIE VISIT
//   (rewardCubieVisit 1 / 2 -> PixelGuideVendor.RequestVisit; Cubie's White / Black counts are only the fallback without such a row). afterGoal chains
//   rows; TierTotal counts a pixel type's lifetime total. Built-in chain (goalDataVersion 1 replaced the earlier rows once):
//     Click 50 blocks (start)                        -> Cubie's visit 1 (the Recycler)
//     Crush 10 husks (20 husks on the floor)         -> Gray unlocks
//     Recycle 20 husks (after visit 1)               -> Black unlocks
//     Get a crush streak of 5 (minute 3)             -> 60 Standard XP
//     Earn 120 Black (after Recycle 20)              -> Cubie's visit 2: RGB + the shop
//     Earn 2,000 Blue (minute 40, after Earn 120 Black) -> Cubie's visit 3: his BILL (PixelCubieBill; ~1 h, the end of the early game - tune it)
//   PACE (sized for RGB + the shop at about 6 min, ~3.5 blocks a second, about 60% of the time clicking): click 50 ~0:20 + Cubie ~0:30; Gray ~1:00;
//   Black ~2:30-3:00 (the Recycler is placed and 20 husks land in it); 120 Black at 1/3 of blocks ~ 2.5-3 min of mixed play -> visit 2 at ~5:30,
//   the shop at ~6:00. Re-check against the next session report (the recorder's timeline lists 'Goal done: ...').
//   The on-screen guide line follows the current goal (PixelUI.BuildGuideText, CurrentGuideGoal).
//   A NEW goal = a new row (or a DefaultGoals line), no code.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelGoals : MonoBehaviour, IPixelDatabaseSource
{
    public enum CounterKind { StatSinceShown, StatValue, ManualClicksSinceShown, TierTotal }
    public enum AppearKind { Always, HusksOnFloor, StatAtLeast, MinutesPlayed }

    [Serializable]
    public class GoalDef
    {
        [Tooltip("Unique id (its saved state is the counters goal.<id>.shown / .start / .done; don't change it once players have it).")]
        public string id = "";
        [Tooltip("Title in the Goals tab.")]
        public string title = "";
        [TextArea(1, 3)]
        [Tooltip("Text in the goal's hover box.")]
        public string description = "";
        [Tooltip("What it counts: a PixelStats counter counted from when the goal appeared, a counter as it is (best values), or manual clicks since it appeared.")]
        public CounterKind counter = CounterKind.StatSinceShown;
        [Tooltip("The PixelStats counter it watches (husk.crushed, recycler.husks, husk.crush.streak...). Not used by ManualClicksSinceShown / TierTotal.")]
        public string counterKey = "";
        [Tooltip("TierTotal: the pixel type whose lifetime total (earned by clicks) it counts, e.g. 'Earn 120 Black'.")]
        public PixelClicker.PixelType counterTier = PixelClicker.PixelType.Black;
        [Min(1f)]
        [Tooltip("How much of it completes the goal.")]
        public double target = 10d;
        [Tooltip("When the goal appears.")]
        public AppearKind appear = AppearKind.Always;
        [Tooltip("StatAtLeast: the PixelStats counter that must reach appearValue.")]
        public string appearKey = "";
        [Tooltip("HusksOnFloor: husks lying out; StatAtLeast: the counter's value; MinutesPlayed: minutes of play.")]
        public double appearValue = 0d;
        [Tooltip("It also waits until the goal with this id is done (empty = no goal needed). Chains the early goals.")]
        public string afterGoal = "";
        [Tooltip("Reward: XP for this skill id (empty = Standard). 0 XP = no XP reward.")]
        public string rewardSkillId = "";
        [Min(0f)]
        [Tooltip("Reward: skill XP paid when the goal is done (flat, no multipliers).")]
        public double rewardXp = 0d;
        [Tooltip("Reward: the pixel type of the currency paid when the goal is done.")]
        public PixelClicker.PixelType rewardCurrencyType = PixelClicker.PixelType.White;
        [Min(0f)]
        [Tooltip("Reward: how much of that currency (bonus currency: it does not count towards the next unlock). 0 = none.")]
        public double rewardCurrency = 0d;
        [Tooltip("Reward: unlock this pixel type (its TierUnlockMode should be ByGoal, so it only unlocks here). Gray / Black are early goals' rewards.")]
        public bool rewardUnlocksTier = false;
        [Tooltip("Reward: the pixel type unlocked (with rewardUnlocksTier).")]
        public PixelClicker.PixelType rewardTier = PixelClicker.PixelType.Gray;
        [Range(0, 2)]
        [Tooltip("Reward: start Cubie's visit 1 (the Recycler), 2 (RGB + the shop) or 3 (his bill, PixelCubieBill). 0 = none. PixelGuideVendor.RequestVisit; with Cubie off the visit's gifts still arrive.")]
        public int rewardCubieVisit = 0;
        [Tooltip("Off = this goal never appears.")]
        public bool enabled = true;
    }

    [Header("Goals (the Log's Goals tab)")]
    [Tooltip("The data-driven goals, in the order they are listed. Built-in rows are added by id when missing (editing one keeps your values).")]
    [SerializeField] private List<GoalDef> goals = new List<GoalDef>();

    [Tooltip("Add the built-in goals that are missing from the list.")]
    [SerializeField] private bool addDefaultGoals = true;

    [Tooltip("Data version of the built-in rows (1 = 2026-10-18 'goals ARE the early unlocks': the built-in rows are replaced by the new ones once). Raised by code.")]
    [SerializeField] private int goalDataVersion = 0;

    [Min(0.05f)]
    [Tooltip("Seconds between two checks of every goal (appear / progress / done).")]
    [SerializeField] private float checkSeconds = 0.25f;

    [Min(0f)]
    [Tooltip("Seconds a finished goal stays in the Goals tab as complete before it goes.")]
    [SerializeField] private float showDoneSeconds = 8f;

    [Tooltip("Sound when a goal is completed (a PixelAudio id).")]
    [SerializeField] private string doneSoundId = "achievement";

    public static PixelGoals Instance { get; private set; }

    private PixelClicker clicker;
    private float checkTimer;
    private readonly Dictionary<string, float> doneAt = new Dictionary<string, float>();   // this run: when a goal was finished (it lingers as complete)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    /// <summary>The built-in goal rows (added by id when missing).</summary>
    /// <summary>
    /// The built-in rows: the early game's progression (2026-10-18, design 'Early game feel' item 2 - the GOALS ARE THE UNLOCKS, each new colour
    /// rewards learning one action). Sized for RGB + the shop at about 6 minutes (see the header): Click 50 -> Cubie's visit 1 (the Recycler),
    /// Crush 10 -> Gray, Recycle 20 -> Black, crush streak of 5 -> Standard XP, Earn 120 Black -> Cubie's visit 2 (RGB + the shop).
    /// </summary>
    private static GoalDef[] DefaultGoals() => new[]
    {
        new GoalDef { id = "click50", title = "Click 50 blocks", description = "Click the cube 50 times. Every click pays its pixel and drops a husk. Someone is watching...", counter = CounterKind.ManualClicksSinceShown, target = 50d, appear = AppearKind.Always, rewardCubieVisit = 1 },
        new GoalDef { id = "crush10", title = "Crush 10 husks", description = "Husks are piling up! Right-click husks on the floor to crush them.", counter = CounterKind.StatSinceShown, counterKey = "husk.crushed", target = 10d, appear = AppearKind.HusksOnFloor, appearValue = 20d, rewardUnlocksTier = true, rewardTier = PixelClicker.PixelType.Gray },
        new GoalDef { id = "recycle20", title = "Recycle 20 husks", description = "Place Cubie's Recycler (in your Consumables) where husks land: every husk that falls in turns back into a little currency.", counter = CounterKind.StatSinceShown, counterKey = "recycler.husks", target = 20d, appear = AppearKind.StatAtLeast, appearKey = "guide.stage1", appearValue = 1d, rewardUnlocksTier = true, rewardTier = PixelClicker.PixelType.Black },
        new GoalDef { id = "crushstreak5", title = "Get a crush streak of 5", description = "Crush 5 husks in a row, each within a second of the last - every crush in a streak gives more XP.", counter = CounterKind.StatValue, counterKey = "husk.crush.streak", target = 5d, appear = AppearKind.MinutesPlayed, appearValue = 3d, rewardXp = 60d },
        new GoalDef { id = "earnblack", title = "Earn 120 Black", description = "Click Black blocks until you have earned 120 Black pixels. Cubie wants to see a real pile.", counter = CounterKind.TierTotal, counterTier = PixelClicker.PixelType.Black, target = 120d, appear = AppearKind.Always, afterGoal = "recycle20", rewardCubieVisit = 2 },
        new GoalDef { id = "billreveal", title = "Earn 2,000 Blue", description = "Keep clicking and build up the pure colours. Cubie keeps glancing at that tag on your Recycler...", counter = CounterKind.TierTotal, counterTier = PixelClicker.PixelType.Blue, target = 2000d, appear = AppearKind.MinutesPlayed, appearValue = 40d, afterGoal = "earnblack", rewardCubieVisit = 3 },
    };

    private void Awake()
    {
        Instance = this;
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        EnsureDefaults();
    }

    private void OnValidate() { if (addDefaultGoals) EnsureDefaults(); }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void EnsureDefaults()
    {
        if (!addDefaultGoals) return;
        if (goals == null) goals = new List<GoalDef>();
        if (goalDataVersion < 1)
        {
            // The early goals became the unlocks (2026-10-18): the older built-in rows (XP / currency rewards) are replaced by the new ones once.
            foreach (GoalDef def in DefaultGoals()) goals.RemoveAll(g => g != null && g.id == def.id);
            goalDataVersion = 1;
        }
        foreach (GoalDef def in DefaultGoals())
            if (!goals.Exists(g => g != null && g.id == def.id)) goals.Add(def);
    }

    /// <summary>An enabled goal row's reward unlocks this pixel type (then a ByGoal tier waits for it instead of its threshold).</summary>
    public static bool UnlocksTier(PixelClicker.PixelType type) => GoalUnlocking(type) != null;

    /// <summary>The enabled goal row whose reward unlocks this pixel type (null = none).</summary>
    public static GoalDef GoalUnlocking(PixelClicker.PixelType type)
    {
        PixelGoals g = Instance;
        if (g == null || g.goals == null) return null;
        for (int i = 0; i < g.goals.Count; i++)   // (a plain loop: asked on every unlock check, no allocations)
        {
            GoalDef d = g.goals[i];
            if (d != null && d.enabled && d.rewardUnlocksTier && d.rewardTier == type) return d;
        }
        return null;
    }

    /// <summary>An enabled goal row's reward starts this Cubie visit (then PixelGuideVendor waits for it instead of his White / Black counts).</summary>
    public static bool GrantsVisit(int visit)
    {
        PixelGoals g = Instance;
        if (g == null || g.goals == null) return false;
        for (int i = 0; i < g.goals.Count; i++)   // (a plain loop: Cubie asks often, no allocations)
        {
            GoalDef d = g.goals[i];
            if (d != null && d.enabled && d.rewardCubieVisit == visit) return true;
        }
        return false;
    }

    /// <summary>
    /// The goal the on-screen guide line follows: the first one showing (not done) whose reward moves the game on (unlocks a tier or starts a Cubie visit),
    /// else the first one showing. Null when none is showing.
    /// </summary>
    public GoalDef CurrentGuideGoal()
    {
        if (goals == null) return null;
        GoalDef any = null;
        foreach (GoalDef g in goals)
        {
            if (g == null || !g.enabled || string.IsNullOrEmpty(g.id) || !Shown(g) || Done(g)) continue;
            if (g.rewardUnlocksTier || g.rewardCubieVisit > 0) return g;
            if (any == null) any = g;
        }
        return any;
    }

    /// <summary>Every goal row (for the Game Database).</summary>
    public IReadOnlyList<GoalDef> Defs => goals;

    private static string Key(GoalDef g, string part) => "goal." + g.id + "." + part;

    public static bool Shown(GoalDef g) => g != null && PixelStats.Total(Key(g, "shown")) >= 1d;
    public static bool Done(GoalDef g) => g != null && PixelStats.Total(Key(g, "done")) >= 1d;

    /// <summary>The goal's progress now (capped at its target).</summary>
    public double Progress(GoalDef g)
    {
        double raw = RawCounter(g);
        if (g.counter != CounterKind.StatValue) raw -= PixelStats.Total(Key(g, "start"));
        return Math.Min(Math.Max(0d, raw), g.target);
    }

    private double RawCounter(GoalDef g)
    {
        if (g.counter == CounterKind.ManualClicksSinceShown)
        {
            PixelStats stats = PixelFind.Cached<PixelStats>();
            return stats != null ? stats.ManualClicks : 0d;
        }
        if (g.counter == CounterKind.TierTotal)
        {
            int tier = clicker != null ? clicker.IndexOf(g.counterTier) : -1;
            return tier >= 0 ? clicker.Tiers[tier].totalCollected : 0d;
        }
        return string.IsNullOrEmpty(g.counterKey) ? 0d : PixelStats.Total(g.counterKey);
    }

    /// <summary>The reward as a short text ('+40 Standard XP', '+30 White').</summary>
    public string RewardText(GoalDef g)
    {
        List<string> parts = new List<string>();
        if (g.rewardUnlocksTier && clicker != null)
        {
            int tier = clicker.IndexOf(g.rewardTier);
            parts.Add((tier >= 0 ? clicker.Tiers[tier].displayName : g.rewardTier.ToString()) + " unlock");
        }
        if (g.rewardCubieVisit == 1) parts.Add("a visitor drops by");
        else if (g.rewardCubieVisit == 2) parts.Add("Red, Green, Blue + the shop");
        else if (g.rewardCubieVisit == 3) parts.Add("Cubie has something for you");
        if (g.rewardXp > 0d)
        {
            PixelSkills skills = PixelSkills.Instance;
            PixelSkills.SkillPack pack = skills != null ? skills.Find(string.IsNullOrEmpty(g.rewardSkillId) ? PixelSkills.StandardId : g.rewardSkillId) : null;
            parts.Add("+" + g.rewardXp.ToString("0.#") + " " + (pack != null ? PixelSkills.ShownName(pack) : "skill") + " XP");
        }
        if (g.rewardCurrency > 0d && clicker != null)
        {
            int tier = clicker.IndexOf(g.rewardCurrencyType);
            parts.Add("+" + PixelClicker.FormatNumberShort(g.rewardCurrency) + " " + (tier >= 0 ? clicker.Tiers[tier].displayName : g.rewardCurrencyType.ToString()));
        }
        return string.Join(", ", parts);
    }

    /// <summary>The goals to list now: appeared and not done, plus ones finished within showDoneSeconds (shown as complete).</summary>
    public void ActiveGoals(List<GoalDef> into)
    {
        into.Clear();
        if (goals == null) return;
        foreach (GoalDef g in goals)
        {
            if (g == null || !g.enabled || string.IsNullOrEmpty(g.id) || !Shown(g)) continue;
            if (Done(g) && !(doneAt.TryGetValue(g.id, out float at) && Time.unscaledTime - at < showDoneSeconds)) continue;
            into.Add(g);
        }
    }

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); if (clicker == null) return; }
        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = Mathf.Max(0.05f, checkSeconds);
        if (PixelCalm.InMenu || PixelHints.IntroHoldsGuide || goals == null) return;
        foreach (GoalDef g in goals)
        {
            if (g == null || !g.enabled || string.IsNullOrEmpty(g.id) || Done(g)) continue;
            if (!Shown(g))
            {
                if (!AppearMet(g) || !AfterMet(g)) continue;
                PixelStats.Best(Key(g, "start"), RawCounter(g));   // (counted from here: set once, when it appears)
                PixelStats.Best(Key(g, "shown"), 1d);
            }
            if (Progress(g) >= g.target) Complete(g);
        }
    }

    private bool AfterMet(GoalDef g)
    {
        if (string.IsNullOrEmpty(g.afterGoal)) return true;
        GoalDef before = goals.Find(x => x != null && x.id == g.afterGoal);
        return before == null || !before.enabled || Done(before);   // (a missing / switched-off goal never blocks the chain)
    }

    private bool AppearMet(GoalDef g)
    {
        switch (g.appear)
        {
            case AppearKind.HusksOnFloor: return clicker.OldPixels.Count >= g.appearValue;
            case AppearKind.StatAtLeast: return !string.IsNullOrEmpty(g.appearKey) && PixelStats.Total(g.appearKey) >= g.appearValue;
            case AppearKind.MinutesPlayed:
                PixelStats stats = PixelFind.Cached<PixelStats>();
                return stats != null && stats.PlaySeconds >= g.appearValue * 60d;
            default: return true;
        }
    }

    /// <summary>A goal is done: it pays its own reward (never a husk's), says so in the event log and on the session recorder's timeline.</summary>
    private void Complete(GoalDef g)
    {
        PixelStats.Best(Key(g, "done"), 1d);
        doneAt[g.id] = Time.unscaledTime;
        string reward = RewardText(g);
        if (g.rewardXp > 0d && PixelSkills.Instance != null) PixelSkills.Instance.AddRewardXp(string.IsNullOrEmpty(g.rewardSkillId) ? PixelSkills.StandardId : g.rewardSkillId, g.rewardXp);
        if (g.rewardCurrency > 0d)
        {
            int tier = clicker.IndexOf(g.rewardCurrencyType);
            if (tier >= 0) clicker.AddBonusCurrency(tier, g.rewardCurrency);
        }
        if (g.rewardUnlocksTier)
        {
            int tier = clicker.IndexOf(g.rewardTier);
            if (tier >= 0) clicker.UnlockTier(tier);   // (the tier's unlock sound, onTierUnlocked: the event log, the unlock banner)
        }
        if (g.rewardCubieVisit > 0) PixelGuideVendor.RequestVisit(g.rewardCubieVisit);
        if (!string.IsNullOrEmpty(doneSoundId)) PixelAudio.Play(doneSoundId);
        PixelHints.Announce("Goal complete: " + g.title + (reward.Length > 0 ? " (" + reward + ")" : ""), "You completed the goal '" + g.title + "'." + (reward.Length > 0 ? " Reward: " + reward + "." : ""));
        PixelSessionRecorder.Note("Goal done: " + g.title);
        PixelStats.Count("goals.done");
    }

    // ---- Game Database (IPixelDatabaseSource): one 'Goal' entry per row of the table, discovered once it has appeared in the Log's Goals tab ----

    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        steps.Add(PixelDatabase.GoalsOrder, b =>
        {
            if (Defs == null) return;
            foreach (GoalDef g in Defs)
            {
                if (g == null || !g.enabled || string.IsNullOrEmpty(g.title)) continue;
                string reward = RewardText(g);
                PixelDatabase.Entry ge = b.Add("Goal", g.title, g.description + "\nTarget: " + g.target.ToString("0.#") + (reward.Length > 0 ? "\nReward: " + reward : "") + (Done(g) ? "\nDone." : ""));
                ge.discovered = Shown(g);
            }
        });
    }
}
