// ---- System notes ----
// PixelSessionRecorder.cs
//   Session recorder (dev / playtest tool, never shown to players): records how a real play session goes, so balance numbers can be set from
//   the player's actual pace instead of guesses. Component in PixelSystems (group Core).
//   - Dev Hub widget 'Session recorder' (PixelDevTools): Start, then Stop & save -> a text report in <persistentDataPath>/Snapshots +
//     clipboard (PixelDevProbe.Save), meant to be pasted to Claude.
//   - Tester builds (no Dev Tools): tick recordEveryPlay on the component. Recording starts by itself when the title screen goes away and the
//     report is written into <persistentDataPath>/<testerFolder> (default 'Feedback', the same folder as the Give feedback notes, so a tester
//     sends one folder) every autosaveMinutes and when the game quits.
//   What it records: only time in the game counts (not the pause menu / title screen). Clicks per second (manual and auto, per minute and the
//   best 10 s), blocks broken, income per pixel type per minute, Standard and training-skill XP per minute, a TIMELINE of milestones found by
//   polling once a second (pixel types unlocked, shop packs bought / levelled, skill level-ups, Cubie's visits, the shop window first opened),
//   Block Hold and stakes (hold.count, stake.placed, stake.won / lost), husk counts (blocks broken = husks dropped; despawned unused 'old.despawned', removed by the floor cap 'old.capped', grabbed, vacuumed, recycled, shredded, banked - PixelStats
//   counters), and the state at the start and end.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelSessionRecorder : MonoBehaviour
{
    [Header("Tester builds")]
    [Tooltip("Record every play session automatically (for a tester's build without Dev Tools). The report is saved into the tester folder every few minutes and on quit.")]
    [SerializeField] private bool recordEveryPlay = false;
    [Tooltip("Folder inside the save folder (persistentDataPath) for the automatic reports. 'Feedback' = next to the Give feedback notes.")]
    [SerializeField] private string testerFolder = "Feedback";
    [Min(1f)]
    [Tooltip("While recording automatically, the report is rewritten this often (minutes), so a crash loses little.")]
    [SerializeField] private float autosaveMinutes = 5f;

    [Header("Recording")]
    [Min(5)]
    [Tooltip("Window (seconds) for the 'best clicking speed' figure.")]
    [SerializeField] private int bestWindowSeconds = 10;
    [Tooltip("While a session is being recorded (Dev Hub / main menu button), a small 'REC m:ss' button shows in the game; click it to stop and save the report.")]
    [SerializeField] private bool showRecButton = true;
    [Tooltip("Where the in-game REC button sits: anchor corner of the screen (0,1 = top left, 1,1 = top right).")]
    [SerializeField] private Vector2 recButtonAnchor = new Vector2(1f, 1f);
    [Tooltip("Offset of the REC button from its anchor corner, in pixels at 1080p.")]
    [SerializeField] private Vector2 recButtonOffset = new Vector2(-24f, -96f);

    public static PixelSessionRecorder Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; GiftFrom = null; carried = null; }

    // A recording SURVIVES a scene reload (New game / Restart / back from the main menu reload the scene and with it this component): the old
    // component's recording state is kept here in OnDestroy and copied into the new one in Awake (CarryOver).
    private static PixelSessionRecorder carried;
    private readonly Dictionary<string, double> totalsAtReload = new Dictionary<string, double>();   // stat counters as the old scene ended (a new game may restart them)

    /// <summary>A session is being recorded right now.</summary>
    public static bool Recording => Instance != null && Instance.recording;

    /// <summary>Seconds of game time recorded so far (0 when not recording).</summary>
    public static float ElapsedSeconds => Instance != null && Instance.recording ? Instance.elapsed : 0f;

    private class Minute
    {
        public long manual, auto, blocks;
        public double standardXp, skillXp;
        public readonly Dictionary<int, double> income = new Dictionary<int, double>();
    }

    private static readonly string[] HuskKeys =
        { "old.despawned", "old.capped", "old.grabbed", "old.vacuumed", "recycler.husks", "recycler.paid", "recycler.xp", "old.shredded", "old.shattered", "old.banked", "old.attached" };

    private static readonly string[] HoldKeys = { "hold.count", "stake.placed", "stake.won", "stake.lost", "power.clicks" };

    private static string HoldLabel(string key)
    {
        switch (key)
        {
            case "hold.count": return "Blocks held (right mouse)";
            case "stake.placed": return "Stakes placed";
            case "stake.won": return "Stakes won (revealed)";
            case "stake.lost": return "Stakes lost (revealed)";
            case "power.clicks": return "Power clicks (left hold, Standard tree)";
            default: return key;
        }
    }

    private PixelClicker clicker;
    private bool recording, autoMode, autoStarted;
    private float elapsed, secondTimer, autosaveTimer;
    private float cubieSeconds, tipSeconds, windowSeconds;   // reading time inside the game time: Cubie's visits, tip / tutorial boxes, other open windows
    private DateTime startedAt;
    private string autoPath;
    private long lastManual, lastAuto;
    private bool clicksSeeded;   // (the stats component may not exist yet when recording starts at load: the first sample only sets the baseline)
    private double lastStandardXp, lastSkillXp;
    private int blocksThisSecond;
    private readonly List<Minute> minutes = new List<Minute>();
    private readonly List<string> timeline = new List<string>();
    private readonly Queue<long> lastSeconds = new Queue<long>();
    private long bestWindowClicks, totalBlocks;
    private readonly Dictionary<int, double> totalIncome = new Dictionary<int, double>();
    private readonly List<string> gifts = new List<string>();   // "mm:ss  Cubie: +80 Gray Pixels" - gifts are kept OUT of the income numbers

    /// <summary>
    /// Set by anything that hands the player currency as a GIFT (Cubie's gift pack) around its AddCurrency calls, then back to null: that currency is
    /// listed under Gifts in the report and left out of the income (which should only show what the player earned).
    /// </summary>
    public static string GiftFrom;
    private readonly Dictionary<string, double> statsAtStart = new Dictionary<string, double>();
    private string startState;

    // Polled state (what changed since the last second = a milestone)
    private bool[] tierUnlocked;
    private bool[] packOwned;
    private int[] packLevel;
    private readonly Dictionary<string, int> skillLevel = new Dictionary<string, int>();
    private bool cubieWasVisiting, shopOpenedOnce;
    private int cubieVisits;

    private void Awake()
    {
        Instance = this;
        clicker = PixelFind.First<PixelClicker>();
        if (carried != null && carried != this) CarryOver(carried);
        carried = null;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (recording)
        {
            carried = this;   // (a scene reload: the next scene's recorder picks the recording up)
            totalsAtReload.Clear();
            foreach (string k in statsAtStart.Keys) totalsAtReload[k] = PixelStats.Total(k);
        }
        if (recButton != null) Destroy(recButton.transform.root.gameObject);
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Takes over a recording from the recorder of the previous scene: every RUN-TIME field (not the Inspector settings, not the scene references) is
    /// copied across, then it listens to the new scene's clicker. Done field by field with reflection so a new field can never be forgotten here.
    /// </summary>
    private void CarryOver(PixelSessionRecorder old)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        foreach (System.Reflection.FieldInfo f in typeof(PixelSessionRecorder).GetFields(flags))
        {
            if (f.IsDefined(typeof(SerializeField), false) || typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;
            f.SetValue(this, f.GetValue(old));
        }
        if (!recording || clicker == null) { recording = false; return; }
        clicker.CurrencyGained += OnGained;
        clicker.PixelCollected += OnCollected;
        SnapshotPolledState();   // (the reloaded game must not look like a burst of new unlocks)
        // Counters may have started again (a new game): shift each start value so the report keeps counting from where it was.
        foreach (KeyValuePair<string, double> kv in totalsAtReload)
            if (statsAtStart.ContainsKey(kv.Key)) statsAtStart[kv.Key] += PixelStats.Total(kv.Key) - kv.Value;
        clicksSeeded = false;   // (the click counters are taken as a fresh baseline on the next sample)
        lastStandardXp = SkillXp(PixelSkills.StandardId);
        lastSkillXp = TrainingXp();
        Mark("Scene reloaded (main menu / new game) - recording carried on");
    }

    // ---- the in-game REC button (stop & save without opening the Dev Hub) ----
    private Button recButton;
    private TMP_Text recLabel;

    private void UpdateRecButton()
    {
        bool show = showRecButton && recording && !autoMode && !PixelCalm.InMenu;
        if (!show) { if (recButton != null && recButton.gameObject.activeSelf) recButton.gameObject.SetActive(false); return; }
        if (recButton == null)
        {
            GameObject canvas = PixelUIKit.CreateCanvas("Session REC Button", 950, new Vector2(1920f, 1080f), true);
            PixelUIKit.EnsureEventSystem();
            recButton = PixelUIKit.CreateButton(clicker != null ? clicker.UIFont : null, canvas.transform, "REC", "", new Vector2(200f, 46f), new Color(0.55f, 0.08f, 0.1f, 0.92f), Color.white, 20f);
            RectTransform r = recButton.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = recButtonAnchor;
            r.anchoredPosition = recButtonOffset;
            recLabel = recButton.GetComponentInChildren<TMP_Text>();
            recButton.onClick.AddListener(() => StopAndSave());
        }
        if (!recButton.gameObject.activeSelf) recButton.gameObject.SetActive(true);
        int sec = Mathf.FloorToInt(elapsed);
        bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
        string text = (blink ? "<color=#FF6060>REC</color> " : "<color=#802020>REC</color> ") + (sec / 60) + ":" + (sec % 60).ToString("00") + "  <size=70%>STOP</size>";
        if (recLabel != null && recLabel.text != text) recLabel.text = text;
    }

    private void OnApplicationQuit()
    {
        if (recording && autoMode) WriteAutoReport();
    }

    // ------------------------------------------------------------------
    // Start / stop
    // ------------------------------------------------------------------

    /// <summary>Starts a new recording (Dev Hub button). Does nothing if one is running.</summary>
    public static void StartRecording() { if (Instance != null) Instance.Begin(false); }

    /// <summary>Stops the recording and saves the report to Snapshots + clipboard. Returns the file path (null if nothing was recorded).</summary>
    public static string StopAndSave()
    {
        if (Instance == null || !Instance.recording) return null;
        string text = Instance.BuildReport();
        Instance.End();
        string path = PixelDevProbe.Save("session", text, false);
        if (path != null) PixelNotice.Show("Session report saved and copied to the clipboard.", 3f, null, true, true);
        return path;
    }

    private void Begin(bool auto)
    {
        if (recording) return;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        recording = true;
        autoMode = auto;
        elapsed = 0f; secondTimer = 0f; autosaveTimer = 0f;
        cubieSeconds = 0f; tipSeconds = 0f; windowSeconds = 0f;
        startedAt = DateTime.Now;
        minutes.Clear(); timeline.Clear(); gifts.Clear(); lastSeconds.Clear(); totalIncome.Clear(); statsAtStart.Clear(); skillLevel.Clear();
        bestWindowClicks = 0; totalBlocks = 0; blocksThisSecond = 0; cubieVisits = 0;
        shopOpenedOnce = false;
        cubieWasVisiting = PixelGuideVendor.Visiting;

        PixelStats stats = PixelFind.Cached<PixelStats>();
        clicksSeeded = stats != null;
        lastManual = stats != null ? stats.ManualClicks : 0;
        lastAuto = stats != null ? stats.AutoClicks : 0;
        foreach (string k in HuskKeys) statsAtStart[k] = PixelStats.Total(k);
        foreach (string k in HoldKeys) statsAtStart[k] = PixelStats.Total(k);
        lastStandardXp = SkillXp(PixelSkills.StandardId);
        lastSkillXp = TrainingXp();
        SnapshotPolledState();
        startState = StateText();
        autoPath = auto ? Path.Combine(Application.persistentDataPath, string.IsNullOrEmpty(testerFolder) ? "Feedback" : testerFolder,
                                       "session_" + startedAt.ToString("yyyyMMdd_HHmmss") + ".txt") : null;

        clicker.CurrencyGained += OnGained;
        clicker.PixelCollected += OnCollected;
        minutes.Add(new Minute());
        Mark("Recording started");
    }

    private void End()
    {
        Unsubscribe();
        recording = false;
    }

    private void Unsubscribe()
    {
        if (clicker == null) return;
        clicker.CurrencyGained -= OnGained;
        clicker.PixelCollected -= OnCollected;
    }

    // ------------------------------------------------------------------
    // Every frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (recordEveryPlay && !autoStarted && !PixelCalm.InMenu)
        {
            autoStarted = true;
            Begin(true);
        }
        if (carried != null && carried != this && !recording) { CarryOver(carried); carried = null; }   // (in case this scene's Awake ran before the old recorder was destroyed)
        UpdateRecButton();
        if (!recording || PixelCalm.InMenu) return;   // only time in the game counts

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);
        elapsed += dt;
        // Reading time (each second counts once, Cubie first): the player is reading rather than clicking.
        if (PixelGuideVendor.Visiting) cubieSeconds += dt;
        else if (PixelNotice.IsShowing) tipSeconds += dt;
        else if (PixelWindows.AnyOpen()) windowSeconds += dt;
        secondTimer += dt;
        if (secondTimer >= 1f)
        {
            secondTimer -= 1f;
            Sample();
        }
        if (autoMode)
        {
            autosaveTimer += dt;
            if (autosaveTimer >= autosaveMinutes * 60f) { autosaveTimer = 0f; WriteAutoReport(); }
        }
    }

    private Minute Current
    {
        get
        {
            int index = Mathf.FloorToInt(elapsed / 60f);
            while (minutes.Count <= index) minutes.Add(new Minute());
            return minutes[index];
        }
    }

    private void OnGained(int tierIndex, double amount)
    {
        if (!recording || amount <= 0d) return;
        if (!string.IsNullOrEmpty(GiftFrom)) { gifts.Add(Clock(elapsed) + "  " + GiftFrom + ": +" + PixelClicker.FormatNumber(amount) + " " + TierName(tierIndex)); return; }
        Minute m = Current;
        m.income.TryGetValue(tierIndex, out double a); m.income[tierIndex] = a + amount;
        totalIncome.TryGetValue(tierIndex, out double t); totalIncome[tierIndex] = t + amount;
    }

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        if (!recording) return;
        Current.blocks++;
        totalBlocks++;
        blocksThisSecond++;
    }

    /// <summary>Once a second: clicks, XP and the milestone checks.</summary>
    private void Sample()
    {
        Minute m = Current;
        PixelStats stats = PixelFind.Cached<PixelStats>();
        if (stats != null)
        {
            long manual = stats.ManualClicks, auto = stats.AutoClicks;
            if (!clicksSeeded) { clicksSeeded = true; lastManual = manual; lastAuto = auto; }
            long dm = Math.Max(0, manual - lastManual), da = Math.Max(0, auto - lastAuto);
            lastManual = manual; lastAuto = auto;
            m.manual += dm; m.auto += da;
            lastSeconds.Enqueue(dm);
            while (lastSeconds.Count > bestWindowSeconds) lastSeconds.Dequeue();
            long sum = 0; foreach (long c in lastSeconds) sum += c;
            if (lastSeconds.Count == bestWindowSeconds && sum > bestWindowClicks) bestWindowClicks = sum;
        }
        double sx = SkillXp(PixelSkills.StandardId), tx = TrainingXp();
        m.standardXp += Math.Max(0d, sx - lastStandardXp); lastStandardXp = sx;
        m.skillXp += Math.Max(0d, tx - lastSkillXp); lastSkillXp = tx;
        blocksThisSecond = 0;
        PollMilestones();
    }

    // ------------------------------------------------------------------
    // Milestones (polled)
    // ------------------------------------------------------------------

    private void SnapshotPolledState()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        tierUnlocked = new bool[tiers.Length];
        for (int i = 0; i < tiers.Length; i++) tierUnlocked[i] = tiers[i] != null && tiers[i].unlocked;
        PixelShop shop = PixelFind.Cached<PixelShop>();
        int n = shop != null ? shop.PackCount : 0;
        packOwned = new bool[n]; packLevel = new int[n];
        for (int i = 0; i < n; i++) { packOwned[i] = shop.GetPackPurchased(i); packLevel[i] = shop.GetPackLevel(i); }
        PixelSkills skills = PixelSkills.Instance;
        if (skills != null) foreach (PixelSkills.SkillPack p in skills.Packs) if (p != null) skillLevel[p.id] = skills.LevelOf(p);
    }

    private void PollMilestones()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        if (tierUnlocked == null || tierUnlocked.Length != tiers.Length) SnapshotPolledState();
        for (int i = 0; i < tiers.Length; i++)
        {
            bool now = tiers[i] != null && tiers[i].unlocked;
            if (now && !tierUnlocked[i]) Mark("Unlocked " + tiers[i].displayName);
            tierUnlocked[i] = now;
        }

        PixelShop shop = PixelFind.Cached<PixelShop>();
        if (shop != null && packOwned != null && packOwned.Length == shop.PackCount)
        {
            for (int i = 0; i < shop.PackCount; i++)
            {
                bool owned = shop.GetPackPurchased(i);
                int level = shop.GetPackLevel(i);
                if (owned && !packOwned[i]) Mark("Bought " + shop.GetPackName(i));
                else if (level > packLevel[i]) Mark(shop.GetPackName(i) + " to level " + level);
                packOwned[i] = owned; packLevel[i] = level;
            }
            if (!shopOpenedOnce && shop.WindowOpen) { shopOpenedOnce = true; Mark("Opened the shop (first time this session)"); }
        }

        PixelSkills skills = PixelSkills.Instance;
        if (skills != null)
            foreach (PixelSkills.SkillPack p in skills.Packs)
            {
                if (p == null) continue;
                int level = skills.LevelOf(p);
                skillLevel.TryGetValue(p.id, out int was);
                if (was > 0 && level > was) Mark(p.displayName + " level " + level);
                skillLevel[p.id] = level;
            }

        bool visiting = PixelGuideVendor.Visiting;
        if (visiting && !cubieWasVisiting) { cubieVisits++; Mark("Cubie arrived (visit " + cubieVisits + " this session)"); }
        cubieWasVisiting = visiting;
    }

    private void Mark(string what) => timeline.Add(Clock(elapsed) + "  " + what);

    // ------------------------------------------------------------------
    // The report
    // ------------------------------------------------------------------

    private void WriteAutoReport()
    {
        if (string.IsNullOrEmpty(autoPath)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(autoPath));
            File.WriteAllText(autoPath, BuildReport());
        }
        catch (Exception e) { Debug.LogWarning("PixelSessionRecorder: could not write the report: " + e.Message); }
    }

    private string BuildReport()
    {
        var sb = new StringBuilder(8192);
        float minutesPlayed = Mathf.Max(elapsed / 60f, 1f / 60f);
        long manual = 0, auto = 0;
        foreach (Minute m in minutes) { manual += m.manual; auto += m.auto; }

        sb.AppendLine("PIXEL CLICKER - SESSION REPORT");
        sb.AppendLine("Started " + startedAt.ToString("yyyy-MM-dd HH:mm") + " (local), " + Clock(elapsed) + " of game time recorded (pause menu / title screen not counted). Build " + Application.version + ".");
        sb.AppendLine();
        sb.AppendLine("READ ME (for whoever reads this): a recording of one real play session, used to set balance numbers (prices, XP, timings) from the");
        sb.AppendLine("player's actual pace. 'Manual' = the player's own clicks, 'auto' = the auto clicker. Husks = the small cubes each broken block drops.");
        sb.AppendLine();

        sb.AppendLine("== SUMMARY ==");
        sb.AppendLine("Manual clicks: " + manual + "  (" + (manual / (elapsed > 0f ? elapsed : 1f)).ToString("F2") + " per second on average, best " +
                      (bestWindowClicks / (float)bestWindowSeconds).ToString("F2") + " per second over " + bestWindowSeconds + " s)");
        sb.AppendLine("Auto clicks: " + auto + "  (" + (auto / (elapsed > 0f ? elapsed : 1f)).ToString("F2") + " per second on average)");
        float reading = cubieSeconds + tipSeconds;
        float active = Mathf.Max(1f, elapsed - reading);
        sb.AppendLine("Reading time inside the game time: " + Clock(reading) + "  (Cubie's visits " + Clock(cubieSeconds) + ", tip / tutorial boxes " + Clock(tipSeconds) +
                      "); a window (Inventory, Shop, Skills...) was open for another " + Clock(windowSeconds) + " (you can still click with most of those)");
        sb.AppendLine("Time not reading: " + Clock(elapsed - reading) + "  (manual clicks per second in that time: " + (manual / active).ToString("F2") + ")");
        sb.AppendLine("Blocks broken (= husks dropped): " + totalBlocks + "  (" + (totalBlocks / minutesPlayed).ToString("F1") + " per minute)");
        double stdXp = 0d, skXp = 0d;
        foreach (Minute m in minutes) { stdXp += m.standardXp; skXp += m.skillXp; }
        sb.AppendLine("Standard XP: " + stdXp.ToString("F0") + "  (" + (stdXp / minutesPlayed).ToString("F1") + " per minute)");
        sb.AppendLine("XP of the skill being trained: " + skXp.ToString("F0") + "  (" + (skXp / minutesPlayed).ToString("F1") + " per minute)");
        sb.AppendLine("Income by pixel type (total, per minute; gifts not included):");
        foreach (KeyValuePair<int, double> kv in Sorted(totalIncome))
            sb.AppendLine("  " + TierName(kv.Key) + ": " + PixelClicker.FormatNumber(kv.Value) + "  (" + PixelClicker.FormatNumber(kv.Value / minutesPlayed) + " / min)");
        sb.AppendLine("Gifts (game time, not counted as income):" + (gifts.Count == 0 ? " none" : ""));
        foreach (string g in gifts) sb.AppendLine("  " + g);
        sb.AppendLine();

        sb.AppendLine("== TIMELINE (game time mm:ss) ==");
        foreach (string line in timeline) sb.AppendLine(line);
        sb.AppendLine();

        sb.AppendLine("== HUSKS ==");
        sb.AppendLine("Dropped (blocks broken): " + totalBlocks);
        foreach (string k in HuskKeys)
        {
            statsAtStart.TryGetValue(k, out double was);
            double now = PixelStats.Total(k) - was;
            sb.AppendLine("  " + HuskLabel(k) + ": " + (k == "recycler.paid" || k == "recycler.xp" ? PixelClicker.FormatNumber(now) : now.ToString("F0")));
        }
        sb.AppendLine("  Longest Recycler streak (this save): " + PixelStats.Total("recycler.streak").ToString("F0"));
        sb.AppendLine();

        sb.AppendLine("== BLOCK HOLD, STAKES AND POWER CLICKS ==");
        foreach (string k in HoldKeys)
        {
            statsAtStart.TryGetValue(k, out double was);
            sb.AppendLine("  " + HoldLabel(k) + ": " + (PixelStats.Total(k) - was).ToString("F0"));
        }
        sb.AppendLine("  (A stake is revealed when its block comes round again, so won + lost can be lower than placed.)");
        sb.AppendLine();

        sb.AppendLine("== PER MINUTE (CSV) ==");
        sb.AppendLine("minute,manual_clicks,auto_clicks,blocks,standard_xp,skill_xp,income (type=amount; ...)");
        for (int i = 0; i < minutes.Count; i++)
        {
            Minute m = minutes[i];
            var inc = new StringBuilder();
            foreach (KeyValuePair<int, double> kv in Sorted(m.income)) { if (inc.Length > 0) inc.Append("; "); inc.Append(TierName(kv.Key) + "=" + kv.Value.ToString("0.##")); }
            sb.AppendLine((i + 1) + "," + m.manual + "," + m.auto + "," + m.blocks + "," + m.standardXp.ToString("0.#") + "," + m.skillXp.ToString("0.#") + ",\"" + inc + "\"");
        }
        sb.AppendLine();

        sb.AppendLine("== STATE AT THE START ==");
        sb.AppendLine(startState);
        sb.AppendLine("== STATE NOW ==");
        sb.AppendLine(StateText());
        return sb.ToString();
    }

    private string StateText()
    {
        var sb = new StringBuilder();
        var unlocked = new List<string>();
        foreach (PixelClicker.PixelTier t in clicker.Tiers) if (t != null && t.unlocked) unlocked.Add(t.displayName);
        sb.AppendLine("Pixel types unlocked: " + (unlocked.Count > 0 ? string.Join(", ", unlocked) : "none"));
        PixelShop shop = PixelFind.Cached<PixelShop>();
        if (shop != null)
        {
            var owned = new List<string>();
            for (int i = 0; i < shop.PackCount; i++)
                if (shop.GetPackPurchased(i)) owned.Add(shop.GetPackName(i) + (shop.GetPackLevel(i) > 1 ? " (level " + shop.GetPackLevel(i) + ")" : ""));
            sb.AppendLine("Shop packs owned: " + (owned.Count > 0 ? string.Join(", ", owned) : "none"));
        }
        PixelSkills skills = PixelSkills.Instance;
        if (skills != null)
        {
            var levels = new List<string>();
            foreach (PixelSkills.SkillPack p in skills.Packs) if (p != null) levels.Add(p.displayName + " " + skills.LevelOf(p));
            sb.AppendLine("Skill levels: " + string.Join(", ", levels) + "  (training: " + skills.TrainingId + ")");
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static double SkillXp(string id)
    {
        PixelSkills skills = PixelSkills.Instance;
        return skills != null ? skills.Xp(skills.Find(id)) : 0d;
    }

    private static double TrainingXp()
    {
        PixelSkills skills = PixelSkills.Instance;
        if (skills == null || skills.TrainingId == PixelSkills.StandardId) return 0d;
        return skills.Xp(skills.Find(skills.TrainingId));
    }

    private string TierName(int index) => clicker != null && clicker.IsValidTierIndex(index) ? clicker.Tiers[index].displayName : "#" + index;

    private static List<KeyValuePair<int, double>> Sorted(Dictionary<int, double> d)
    {
        var list = new List<KeyValuePair<int, double>>(d);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    private static string HuskLabel(string key)
    {
        switch (key)
        {
            case "old.despawned": return "Despawned unused (timer ran out)";
            case "old.capped": return "Removed by the floor cap (oldest first, floor full)";
            case "old.grabbed": return "Grabbed by hand";
            case "old.vacuumed": return "Vacuumed";
            case "recycler.husks": return "Recycled";
            case "recycler.paid": return "Recycler paid (currency)";
            case "recycler.xp": return "Recycler XP (with streaks)";
            case "old.shredded": return "Shredded";
            case "old.shattered": return "Shattered";
            case "old.banked": return "Stored in the Pixel Bank";
            case "old.attached": return "Stuck together (first person building)";
            default: return key;
        }
    }

    private static string Clock(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
