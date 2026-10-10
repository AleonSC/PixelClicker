using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play statistics for Pixel Clicker: total clicks (manual and automatic), time played and pixels spent.
///
/// It just listens to PixelClicker, so nothing needs wiring. The numbers are saved by PixelSaveGame and shown
/// in the pause menu's Stats section. Add this to any GameObject (e.g. the cube); the pause menu adds one for you
/// if it is missing.
/// </summary>
public class PixelStats : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker to watch. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Numbers (saved with the game - type values to test)")]
    [Tooltip("Clicks you made yourself. A click that only hits a tough pixel (Obsidian...) counts too.")]
    [SerializeField] private long manualClicks;

    [Tooltip("Clicks made by the auto clicker.")]
    [SerializeField] private long autoClicks;

    [Tooltip("Seconds spent playing (time while the game is paused doesn't count).")]
    [SerializeField] private double playSeconds;

    [Tooltip("Total pixels spent in the shop, of every type added together.")]
    [SerializeField] private double pixelsSpent;

    [Header("Minigames, devices and combo (saved with the game)")]
    [Tooltip("Ghosts you clicked.")]
    [SerializeField] private long ghostsClicked;

    [Tooltip("Meteor clicks (every click on a meteor counts).")]
    [SerializeField] private long meteorsClicked;

    [Tooltip("Meteors that appeared.")]
    [SerializeField] private long meteorsSpawned;

    [Tooltip("Black holes that opened.")]
    [SerializeField] private long blackHolesSpawned;

    [Tooltip("Fans you placed.")]
    [SerializeField] private long fansUsed;

    [Tooltip("Vacuum devices you placed.")]
    [SerializeField] private long vacuumDevicesUsed;

    [Tooltip("Sorters you placed.")]
    [SerializeField] private long sortersUsed;

    [Tooltip("Singularity pixels you fed to a black hole.")]
    [SerializeField] private long singularitiesFed;

    [Tooltip("Highest combo you reached.")]
    [SerializeField] private int highestCombo;

    [Header("Behaviour")]
    [Tooltip("Keep counting play time while the pause menu is open.")]
    [SerializeField] private bool countTimeWhilePaused = false;

    public long ManualClicks => manualClicks;
    public long AutoClicks => autoClicks;
    public long TotalClicks => manualClicks + autoClicks;
    public double PlaySeconds => playSeconds;
    public double PixelsSpent => pixelsSpent;
    public long GhostsClicked => ghostsClicked;
    public long MeteorsClicked => meteorsClicked;
    public long MeteorsSpawned => meteorsSpawned;
    public long BlackHolesSpawned => blackHolesSpawned;
    public long FansUsed => fansUsed;
    public long VacuumDevicesUsed => vacuumDevicesUsed;
    public long SortersUsed => sortersUsed;
    public long SingularitiesFed => singularitiesFed;
    public int HighestCombo => highestCombo;

    /// <summary>How many of each potion you drank (name, count), in the order first used.</summary>
    public IReadOnlyList<KeyValuePair<string, long>> PotionsUsed => potionsUsed;

    /// <summary>Total potions drunk.</summary>
    public long TotalPotionsUsed
    {
        get
        {
            long total = 0;
            foreach (KeyValuePair<string, long> p in potionsUsed) total += p.Value;
            return total;
        }
    }

    private readonly List<KeyValuePair<string, long>> potionsUsed = new List<KeyValuePair<string, long>>();

    // ------------------------------------------------------------------
    // Generic counters: any script records a stat with PixelStats.Count("key") / Best / Fastest, no wiring needed.
    // They are saved with the game (ExtraData.counterKeys / counterValues) and listed by GetLines().
    // ------------------------------------------------------------------

    private static PixelStats instance;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private readonly Dictionary<string, double> counters = new Dictionary<string, double>();

    /// <summary>Adds to a counter (does nothing when there is no PixelStats).</summary>
    public static void Count(string key, double amount = 1d)
    {
        if (instance == null || amount <= 0d) return;
        instance.counters.TryGetValue(key, out double now);
        instance.counters[key] = now + amount;
    }

    /// <summary>Keeps the highest value ever given for a counter.</summary>
    public static void Best(string key, double value)
    {
        if (instance == null) return;
        instance.counters.TryGetValue(key, out double now);
        if (value > now) instance.counters[key] = value;
    }

    /// <summary>Keeps the lowest value ever given for a counter (0 = never set).</summary>
    public static void Fastest(string key, double value)
    {
        if (instance == null || value <= 0d) return;
        instance.counters.TryGetValue(key, out double now);
        if (now <= 0d || value < now) instance.counters[key] = value;
    }

    /// <summary>Removes a counter (back to 0).</summary>
    public static void Clear(string key)
    {
        if (instance != null) instance.counters.Remove(key);
    }

    /// <summary>A counter's value by key, from anywhere (0 when there is no PixelStats or nothing recorded).</summary>
    public static double Total(string key) => instance != null ? instance.Counter(key) : 0d;

    /// <summary>A counter's value (0 when never recorded).</summary>
    public double Counter(string key) => counters.TryGetValue(key, out double v) ? v : 0d;

    private double gainedTotal, sessionSeconds, sampleTimer;
    private readonly Queue<double> gainedSamples = new Queue<double>();
    private bool sessionCounted, wasStopped, wasSlowed;
    private float sessionCountTimer;

    /// <summary>The extra stats in a form the save file can store.</summary>
    [Serializable]
    public class ExtraData
    {
        public long ghostsClicked, meteorsClicked, meteorsSpawned, blackHolesSpawned, fansUsed, vacuumDevicesUsed, sortersUsed, singularitiesFed;
        public int highestCombo;
        public string[] potionNames;
        public long[] potionCounts;
        public string[] counterKeys;
        public double[] counterValues;
    }

    private void Start()
    {
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }
        if (clicker == null)
        {
            Debug.LogError("PixelStats: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        clicker.PixelCollected += OnPixelCollected;
        clicker.PixelHit += OnPixelHit;
        clicker.CurrencySpent += OnCurrencySpent;
        clicker.CurrencyGained += OnCurrencyGained;
        clicker.PixelsVacuumed += OnPixelsVacuumed;
        instance = this;
        PixelCrafting.Crafted += OnCrafted;

        PixelMinigame.Happened += OnMinigame;
        PixelBlackholeMinigame.PixelSwallowed += OnSwallowed;
        PixelConsumables.PotionDrunk += OnPotionDrunk;
        PixelConsumables.DevicePlaced += OnDevicePlaced;
        PixelCombo.ComboReached += OnCombo;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelCrafting.Crafted -= OnCrafted;
        PixelMinigame.Happened -= OnMinigame;
        PixelBlackholeMinigame.PixelSwallowed -= OnSwallowed;
        PixelConsumables.PotionDrunk -= OnPotionDrunk;
        PixelConsumables.DevicePlaced -= OnDevicePlaced;
        PixelCombo.ComboReached -= OnCombo;
        if (clicker == null) return;
        clicker.PixelCollected -= OnPixelCollected;
        clicker.PixelHit -= OnPixelHit;
        clicker.CurrencySpent -= OnCurrencySpent;
        clicker.CurrencyGained -= OnCurrencyGained;
        clicker.PixelsVacuumed -= OnPixelsVacuumed;
    }

    private void Update()
    {
        if (!countTimeWhilePaused && PixelPauseMenu.GameStopped) return;
        float dt = Time.unscaledDeltaTime;
        playSeconds += dt;
        sessionSeconds += dt;
        Best("session.longest", sessionSeconds);

        // A session counts once it has run a few seconds (a loaded save restores the count first and then adds this one).
        if (!sessionCounted)
        {
            sessionCountTimer += dt;
            if (sessionCountTimer >= 3f) { Count("sessions"); sessionCounted = true; }
        }

        // Earn rate: pixels gained over the last minute, sampled once a second; the best one is kept.
        if (!PixelTitleScreen.Showing)
        {
            sampleTimer += dt;
            while (sampleTimer >= 1d)
            {
                sampleTimer -= 1d;
                gainedSamples.Enqueue(gainedTotal);
                if (gainedSamples.Count > 61) gainedSamples.Dequeue();
                if (gainedSamples.Count > 60) Best("rate.best", gainedTotal - gainedSamples.Peek());
            }
        }

        // Time Stop / Time Slow: how long and how often.
        if (PixelTimeStop.IsStopped) { Count("timestop.seconds", dt); if (!wasStopped) Count("timestop.uses"); }
        if (PixelTimeStop.IsSlowed) { Count("timeslow.seconds", dt); if (!wasSlowed) Count("timeslow.uses"); }
        wasStopped = PixelTimeStop.IsStopped;
        wasSlowed = PixelTimeStop.IsSlowed;
    }

    private void OnCurrencyGained(int tier, double amount) => gainedTotal += amount;

    private void OnPixelsVacuumed(int tier, double total, int count) => Count("old.vacuumed", count);

    private void OnCrafted() => Count("crafts");

    private void OnPixelCollected(int tier, double amount, bool automatic) => CountClick(automatic);

    private void OnPixelHit(int tier, int hits, int needed, bool automatic) => CountClick(automatic);

    private void CountClick(bool automatic)
    {
        if (automatic) autoClicks++;
        else manualClicks++;
    }

    private void OnCurrencySpent(int tier, double amount) => pixelsSpent += amount;

    private void OnMinigame(PixelMinigame game, PixelMinigame.MinigameEvent what)
    {
        bool clicked = what == PixelMinigame.MinigameEvent.Clicked;
        switch (game.Id)
        {
            case "ghost": if (clicked) ghostsClicked++; break;
            case "meteor": if (clicked) meteorsClicked++; else meteorsSpawned++; break;
            case "blackhole": if (!clicked) blackHolesSpawned++; break;
        }
    }

    private void OnSwallowed(int tierIndex)
    {
        Count("holes.fed");
        if (clicker != null && clicker.IsValidTierIndex(tierIndex) && clicker.Tiers[tierIndex].type == PixelClicker.PixelType.Singularity) singularitiesFed++;
    }

    private void OnPotionDrunk(string potionName)
    {
        int i = potionsUsed.FindIndex(p => p.Key == potionName);
        if (i >= 0) potionsUsed[i] = new KeyValuePair<string, long>(potionName, potionsUsed[i].Value + 1);
        else potionsUsed.Add(new KeyValuePair<string, long>(potionName, 1));
    }

    private void OnDevicePlaced(PixelConsumables.DeviceKind kind)
    {
        if (kind == PixelConsumables.DeviceKind.Fan) fansUsed++;
        else if (kind == PixelConsumables.DeviceKind.Vacuum) vacuumDevicesUsed++;
        else if (kind == PixelConsumables.DeviceKind.Sorter) sortersUsed++;
    }

    private void OnCombo(int count)
    {
        if (count > highestCombo) highestCombo = count;
    }

    /// <summary>The extra stats for saving.</summary>
    public ExtraData GetExtra()
    {
        ExtraData d = new ExtraData
        {
            ghostsClicked = ghostsClicked, meteorsClicked = meteorsClicked, meteorsSpawned = meteorsSpawned,
            blackHolesSpawned = blackHolesSpawned, fansUsed = fansUsed, vacuumDevicesUsed = vacuumDevicesUsed, sortersUsed = sortersUsed, singularitiesFed = singularitiesFed,
            highestCombo = highestCombo,
            potionNames = new string[potionsUsed.Count],
            potionCounts = new long[potionsUsed.Count],
            counterKeys = new string[counters.Count],
            counterValues = new double[counters.Count],
        };
        int ci = 0;
        foreach (KeyValuePair<string, double> c in counters) { d.counterKeys[ci] = c.Key; d.counterValues[ci] = c.Value; ci++; }
        for (int i = 0; i < potionsUsed.Count; i++)
        {
            d.potionNames[i] = potionsUsed[i].Key;
            d.potionCounts[i] = potionsUsed[i].Value;
        }
        return d;
    }

    /// <summary>Restores the extra stats from a save (null = an older save without them).</summary>
    public void SetExtra(ExtraData d)
    {
        potionsUsed.Clear();
        counters.Clear();
        if (d != null && d.counterKeys != null && d.counterValues != null)
            for (int i = 0; i < d.counterKeys.Length && i < d.counterValues.Length; i++)
                counters[d.counterKeys[i]] = Math.Max(0d, d.counterValues[i]);
        if (!sessionCounted) { Count("sessions"); sessionCounted = true; } // this launch is one more session
        if (d == null)
        {
            ghostsClicked = meteorsClicked = meteorsSpawned = blackHolesSpawned = fansUsed = vacuumDevicesUsed = sortersUsed = singularitiesFed = 0;
            highestCombo = 0;
            return;
        }
        ghostsClicked = Math.Max(0L, d.ghostsClicked);
        meteorsClicked = Math.Max(0L, d.meteorsClicked);
        meteorsSpawned = Math.Max(0L, d.meteorsSpawned);
        blackHolesSpawned = Math.Max(0L, d.blackHolesSpawned);
        fansUsed = Math.Max(0L, d.fansUsed);
        vacuumDevicesUsed = Math.Max(0L, d.vacuumDevicesUsed);
        sortersUsed = Math.Max(0L, d.sortersUsed);
        singularitiesFed = Math.Max(0L, d.singularitiesFed);
        highestCombo = Math.Max(0, d.highestCombo);
        if (d.potionNames == null || d.potionCounts == null) return;
        for (int i = 0; i < d.potionNames.Length && i < d.potionCounts.Length; i++)
            potionsUsed.Add(new KeyValuePair<string, long>(d.potionNames[i], Math.Max(0L, d.potionCounts[i])));
    }

    /// <summary>Adds automatic clicks that happened while the game was closed (offline progress).</summary>
    public void AddAutoClicks(long count)
    {
        if (count > 0) autoClicks += count;
    }

    /// <summary>Restores the numbers from a save.</summary>
    public void SetState(long manual, long auto, double seconds, double spent)
    {
        manualClicks = System.Math.Max(0L, manual);
        autoClicks = System.Math.Max(0L, auto);
        playSeconds = System.Math.Max(0d, seconds);
        pixelsSpent = System.Math.Max(0d, spent);
    }

    /// <summary>Seconds as "1h 23m 45s" (hours and minutes are left out while they are zero).</summary>
    public static string FormatTime(double seconds)
    {
        long total = (long)System.Math.Max(0d, seconds);
        long h = total / 3600, m = total % 3600 / 60, s = total % 60;
        if (h > 0) return h + "h " + m + "m " + s + "s";
        if (m > 0) return m + "m " + s + "s";
        return s + "s";
    }

    // ------------------------------------------------------------------
    // The list shown on the Stats screen
    // ------------------------------------------------------------------

    /// <summary>One row of the Stats screen: a heading (no value) or a label with its value.</summary>
    public struct Line
    {
        public string label, value;
        public bool header;
    }

    /// <summary>Every stat row in order. 'number' formats counts the way the player chose (abbreviated or not).</summary>
    public List<Line> GetLines(Func<double, string> number)
    {
        List<Line> lines = new List<Line>();
        void Head(string text) => lines.Add(new Line { label = text, header = true });
        void Row(string label, string value) => lines.Add(new Line { label = label, value = value });
        void Num(string label, double value) => Row(label, number(value));
        void Cnt(string label, string key) => Num(label, Counter(key));
        // A minigame's rows appear once it is running or has something recorded.
        bool Seen(string id, params string[] keys)
        {
            PixelMinigame g = PixelMinigame.Find(id);
            if (g != null && g.Running) return true;
            foreach (string k in keys) if (Counter(k) > 0d) return true;
            return false;
        }

        Head("General");
        Num("Total clicks", TotalClicks);
        Num("   Your clicks", manualClicks);
        Num("   Auto clicker", autoClicks);
        Row("Time played", FormatTime(playSeconds));
        Row("Longest session", FormatTime(Counter("session.longest")));
        Num("Sessions played", Counter("sessions"));
        PixelAchievements ach = PixelFind.First<PixelAchievements>();
        if (ach != null && ach.TierTotal > 0) Row("Achievements", ach.EarnedTierTotal + " / " + ach.TierTotal);

        Head("Pixels");
        double earned = 0d, rarestWeight = double.MaxValue;
        string rarest = null;
        int highestValue = 0, valueLevels = 0;
        if (clicker != null)
        {
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
            {
                earned += t.totalCollected;
                highestValue = Math.Max(highestValue, t.valueLevel);
                valueLevels += t.valueLevel;
                if (t.totalCollected > 0d && t.spawnWeight > 0f && t.spawnWeight < rarestWeight) { rarestWeight = t.spawnWeight; rarest = t.displayName; }
            }
        }
        Num("Pixels earned", earned);
        if (clicker != null)
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
                if (t.totalCollected > 0d) Num("   " + t.displayName, t.totalCollected);
        Num("Pixels spent", pixelsSpent);
        double best = Counter("rate.best");
        Row("Best earn rate", best > 0d ? number(best) + " / min" : "-");
        Row("Rarest pixel collected", rarest ?? "-");
        Num("Value upgrade levels bought", valueLevels);
        Num("Highest Value level", highestValue);
        Cnt("Shop purchases", "shop.purchases");
        Cnt("Consumables bought", "shop.items");


        Head("Old pixels");
        Cnt("Vacuumed up", "old.vacuumed");
        Cnt("Stored in the bank", "old.banked");
        Cnt("Grabbed", "old.grabbed");
        Cnt("Shattered (glass)", "old.shattered");
        Cnt("Items crafted", "crafts");
        Num("Potions used", TotalPotionsUsed);
        Num("Fans placed", fansUsed);
        Num("Vacuum devices placed", vacuumDevicesUsed);
        Num("Sorters placed", sortersUsed);

        Head("Combo and time");
        Num("Highest combo", highestCombo);
        Num("Highest combo tier", Counter("combo.tier"));
        if (Counter("timestop.uses") > 0d || Counter("timeslow.uses") > 0d)
        {
            Row("Time stopped", FormatTime(Counter("timestop.seconds")));
            Cnt("   Times used", "timestop.uses");
            Row("Time slowed", FormatTime(Counter("timeslow.seconds")));
            Cnt("   Times used", "timeslow.uses");
        }

        PixelPets petSystem = PixelPets.Instance;
        if (petSystem != null && clicker != null)
        {
            Head("Pets");
            int petTypes = 0;
            foreach (PixelClicker.PixelTier t in clicker.Tiers) if (!t.rareDrop) petTypes++;
            Row("Pets found", petSystem.OwnedCount + " / " + petTypes);
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
                if (!t.rareDrop) Row("   " + (petSystem.IsOwned(t.type) ? petSystem.NameOf(t.type) : "???"), petSystem.IsOwned(t.type) ? (petSystem.IsOn(t.type) ? "Roaming" : "Switched off") : "-");
            if (petSystem.IsOwned(PixelClicker.PixelType.Vacuum) || Counter("pet.vacuum") > 0d)
                Cnt("Old pixels sucked by the Vacuum Pet", "pet.vacuum");
        }

        Head("Minigames");
        double ultra = clicker != null ? clicker.UltraEarned : 0d;
        Num("Ultra pixels earned", ultra);
        string[] ultraIds = { "pad", "snake", "sort", "breakout", "blackhole" };
        string[] ultraNames = { "Ultra Pad", "Snake", "Sorting Race", "Breakout", "Black Hole" };
        for (int i = 0; i < ultraIds.Length; i++)
            if (Counter("ultra." + ultraIds[i]) > 0d) Num("   from " + ultraNames[i], Counter("ultra." + ultraIds[i]));
        if (Seen("ghost", "ghost.kept") || ghostsClicked > 0)
        {
            Num("Ghosts clicked", ghostsClicked);
            Cnt("   Potions kept in the backpack", "ghost.kept");
        }
        if (Seen("meteor") || meteorsSpawned > 0)
        {
            Num("Meteors spawned", meteorsSpawned);
            Num("Meteors clicked", meteorsClicked);
        }
        PixelMinigame stardust = PixelMinigame.Find("stardust");
        if (stardust != null && stardust.TrackerCount > 0d) Num("Stardust collected", stardust.TrackerCount);
        if (Seen("blackhole", "holes.fed") || blackHolesSpawned > 0)
        {
            Num("Black holes opened", blackHolesSpawned);
            Cnt("   Pixels fed to them", "holes.fed");
            Num("   Singularities fed", singularitiesFed);
        }
        if (Seen("snake", "snake.win", "snake.loss"))
        {
            Cnt("Snake rounds won", "snake.win");
            Cnt("Snake rounds lost", "snake.loss");
            Num("Longest snake", Counter("snake.length"));
        }
        if (Seen("sort", "sort.win", "sort.loss"))
        {
            Cnt("Sorting races won", "sort.win");
            Cnt("Sorting races lost", "sort.loss");
            double fastest = Counter("sort.fastest");
            Row("Fastest sorting race", fastest > 0d ? FormatTime(fastest) : "-");
        }
        if (Seen("breakout", "breakout.win", "breakout.loss"))
        {
            Cnt("Breakout games won", "breakout.win");
            Cnt("Breakout games lost", "breakout.loss");
        }
        PixelMinigame bomb = PixelMinigame.Find("bomb");
        if (Seen("bomb", "bomb.defused", "bomb.exploded"))
        {
            Cnt("Bombs defused", "bomb.defused");
            Cnt("Bombs exploded", "bomb.exploded");
            if (bomb != null) Num("Bomb parts earned", bomb.TrackerCount);
        }
        return lines;
    }
}
