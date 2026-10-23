// ---- System notes (moved here from CLAUDE.md) ----
// PixelStats.cs
//   Play stats. Typed fields (manual/auto clicks, play time, pixels spent, ghosts/meteors clicked, meteors/black holes spawned,
//   fans/vacuum devices placed, singularities fed, potions used per name, highest combo) plus generic counters: any script
//   calls static PixelStats.Count("key", n) / Best("key", v) (keeps the max) / Fastest("key", v) (keeps the min); they are saved in
//   the GameState (StatsState.counters) and need no other wiring. Counters in use: shop.purchases/items, snake/sort/breakout.win/loss,
//   snake.length, sort.fastest, bomb.defused/exploded, ultra.<minigame id> (PixelMinigame.UltraRoll + the Pad), ghost.kept,
//   combo.tier, old.vacuumed/banked/grabbed/shattered, crafts, holes.fed, sessions, session.longest, rate.best (best pixels per
//   minute, sampled each second), timestop/timeslow.seconds/uses. GetLines(number) builds the Stats screen's rows (headings General /
//   Pixels / Clicks per pixel / Husks (+ Recycler cycles and payout) / Block Hold (holds, stakes won / lost - counted when shown - best stake) / Skills (level + hazards set off / cleared per skill: hazards.triggered.<id>, hazards.defused.<id>) / Combo and time / Crafting and items / Devices placed (every DeviceKind) / Rare finds (Dragon x/7, God x/3, Prime x/6) / Pets / Minigames (Ultra sources from PixelMinigame.All); per-pixel-type earned rows; a minigame's rows only once it runs or has data;
//   Ultra earned, stardust, bomb parts and achievements are read from their owners); the pause menu's Stats screen just renders that
//   list (RebuildStatRows, rebuilt only when the row list changes). To add a stat: record it with PixelStats.Count/Best/Fastest and
//   add a row in GetLines. PROGRESS lives in PixelGameState.Current.stats (GameState.cs, moved 2026-10-23): saved / loaded / reset with it;
//   this component keeps only listeners, the per-session numbers (rate samples, session timer) and the Stats screen.
// ---------------------------------------------------
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
    // ---- progress: lives in the GameState (PixelGameState.Current.stats); this component only listens and builds the Stats screen ----
    private static StatsState S => PixelGameState.Current.stats;


    [Header("References")]
    [Tooltip("The PixelClicker to watch. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Behaviour")]
    [Tooltip("Keep counting play time while the pause menu is open.")]
    [SerializeField] private bool countTimeWhilePaused = false;

    // (the numbers themselves are in StatsState; type values to test in the Pixel Game State component's Inspector)
    public long ManualClicks => S.manualClicks;
    public long AutoClicks => S.autoClicks;
    public long TotalClicks => S.manualClicks + S.autoClicks;
    public double PlaySeconds => S.playSeconds;
    public double PixelsSpent => S.pixelsSpent;
    public long GhostsClicked => S.ghostsClicked;
    public long MeteorsClicked => S.meteorsClicked;
    public long MeteorsSpawned => S.meteorsSpawned;
    public long BlackHolesSpawned => S.blackHolesSpawned;
    public long FansUsed => S.fansUsed;
    public long VacuumDevicesUsed => S.vacuumDevicesUsed;
    public long SingularitiesFed => S.singularitiesFed;
    public int HighestCombo => S.highestCombo;

    /// <summary>How many of each potion you drank (name, count), in the order first used.</summary>
    public IEnumerable<KeyValuePair<string, long>> PotionsUsed
    {
        get { foreach (StatsState.PotionCount p in S.potionsUsed) yield return new KeyValuePair<string, long>(p.name, p.count); }
    }

    /// <summary>Total potions drunk.</summary>
    public long TotalPotionsUsed
    {
        get
        {
            long total = 0;
            foreach (StatsState.PotionCount p in S.potionsUsed) total += p.count;
            return total;
        }
    }

    // ------------------------------------------------------------------
    // Generic counters: any script records a stat with PixelStats.Count("key") / Best / Fastest, no wiring needed.
    // They live in StatsState.counters (saved with the GameState) and are listed by GetLines().
    // ------------------------------------------------------------------

    /// <summary>Adds to a counter.</summary>
    public static void Count(string key, double amount = 1d)
    {
        if (key == null || amount <= 0d) return;
        S.Set(key, S.Get(key) + amount);
    }

    /// <summary>Keeps the highest value ever given for a counter.</summary>
    public static void Best(string key, double value)
    {
        if (key != null && value > S.Get(key)) S.Set(key, value);
    }

    /// <summary>Keeps the lowest value ever given for a counter (0 = never set).</summary>
    public static void Fastest(string key, double value)
    {
        if (key == null || value <= 0d) return;
        double now = S.Get(key);
        if (now <= 0d || value < now) S.Set(key, value);
    }

    /// <summary>Removes a counter (back to 0).</summary>
    public static void Clear(string key) => S.Remove(key);

    /// <summary>A counter's value by key, from anywhere (0 when nothing recorded).</summary>
    public static double Total(string key) => S.Get(key);

    /// <summary>A counter's value (0 when never recorded).</summary>
    public double Counter(string key) => S.Get(key);

    private double gainedTotal, sessionSeconds, sampleTimer;
    private readonly Queue<double> gainedSamples = new Queue<double>();
    private bool sessionCounted, wasStopped, wasSlowed;
    private float sessionCountTimer;


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
        PixelGameState.Loaded += OnLoaded;
        PixelCrafting.Crafted += OnCrafted;

        PixelMinigame.Happened += OnMinigame;
        PixelBlackholeMinigame.PixelSwallowed += OnSwallowed;
        PixelConsumables.PotionDrunk += OnPotionDrunk;
        PixelConsumables.DevicePlaced += OnDevicePlaced;
        PixelCombo.ComboReached += OnCombo;
    }

    private void OnDestroy()
    {
        PixelGameState.Loaded -= OnLoaded;
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
        S.playSeconds += dt;
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

    // A loaded save: this launch is one more session in it (if the 3-second count has not happened yet).
    private void OnLoaded()
    {
        if (!sessionCounted) { Count("sessions"); sessionCounted = true; }
    }

    private void OnCurrencyGained(int tier, double amount) => gainedTotal += amount;

    private void OnPixelsVacuumed(int tier, double total, int count) => Count("old.vacuumed", count);

    private void OnCrafted() => Count("crafts");

    private void OnPixelCollected(int tier, double amount, bool automatic)
    {
        CountClick(automatic);
        float now = Time.unscaledTime;
        if (clickTime.TryGetValue(tier, out float last) && now - last < ClickIndicatorSeconds) clickSum[tier] = clickSum[tier] + 1;
        else clickSum[tier] = 1;
        clickTime[tier] = now;
    }

    // The "+N" next to a pixel's click count on the Stats screen: clicks in the last moment (auto clicker ticks included).
    private const float ClickIndicatorSeconds = 1.5f;
    private readonly Dictionary<int, float> clickTime = new Dictionary<int, float>();
    private readonly Dictionary<int, int> clickSum = new Dictionary<int, int>();

    private int RecentClicks(int tier)
    {
        return clickTime.TryGetValue(tier, out float t) && Time.unscaledTime - t < ClickIndicatorSeconds ? clickSum[tier] : 0;
    }

    private void OnPixelHit(int tier, int hits, int needed, bool automatic) => CountClick(automatic);

    private void CountClick(bool automatic)
    {
        if (automatic) S.autoClicks++;
        else S.manualClicks++;
    }

    private void OnCurrencySpent(int tier, double amount) => S.pixelsSpent += amount;

    private void OnMinigame(PixelMinigame game, PixelMinigame.MinigameEvent what)
    {
        bool clicked = what == PixelMinigame.MinigameEvent.Clicked;
        switch (game.Id)
        {
            case "ghost": if (clicked) S.ghostsClicked++; break;
            case "meteor": if (clicked) S.meteorsClicked++; else S.meteorsSpawned++; break;
            case "blackhole": if (!clicked) S.blackHolesSpawned++; break;
        }
    }

    private void OnSwallowed(int tierIndex)
    {
        Count("holes.fed");
        if (clicker != null && clicker.IsValidTierIndex(tierIndex) && clicker.Tiers[tierIndex].type == PixelClicker.PixelType.Singularity) S.singularitiesFed++;
    }

    private void OnPotionDrunk(string potionName)
    {
        StatsState.PotionCount p = S.potionsUsed.Find(x => x.name == potionName);
        if (p != null) p.count++;
        else S.potionsUsed.Add(new StatsState.PotionCount { name = potionName, count = 1 });
    }

    private void OnDevicePlaced(PixelConsumables.DeviceKind kind)
    {
        if (kind == PixelConsumables.DeviceKind.Fan) S.fansUsed++;
        else if (kind == PixelConsumables.DeviceKind.Vacuum) S.vacuumDevicesUsed++;
        else Count("device.placed." + kind);   // every other kind (Charge Booster, Lightning Rod, Sprinkler) is counted generically
    }

    private void OnCombo(int count)
    {
        if (count > S.highestCombo) S.highestCombo = count;
    }

    /// <summary>Adds automatic clicks that happened while the game was closed (offline progress).</summary>
    public void AddAutoClicks(long count)
    {
        if (count > 0) S.autoClicks += count;
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

    /// <summary>'QuantumOrganizer' -> 'Quantum Organizer' (device kinds on the Stats screen).</summary>
    private static string SpacedName(string name)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }

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
        Num("   Your clicks", S.manualClicks);
        Num("   Auto clicker", S.autoClicks);
        Row("Time played", FormatTime(S.playSeconds));
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
        Num("Pixels spent", S.pixelsSpent);
        double best = Counter("rate.best");
        Row("Best earn rate", best > 0d ? number(best) + " / min" : "-");
        Row("Rarest pixel collected", rarest ?? "-");
        Num("Value upgrade levels bought", valueLevels);
        Num("Highest Value level", highestValue);
        Cnt("Shop purchases", "shop.purchases");
        Cnt("Consumables bought", "shop.items");


        // How many times each pixel type was CLICKED (the Log's old Clicks tab). A gold "+N" shows beside a count while clicks are coming in.
        Head("Clicks per pixel");
        if (clicker != null)
        {
            double clickTotal = 0d;
            for (int i = 0; i < clicker.Tiers.Length; i++)
            {
                PixelClicker.PixelTier t = clicker.Tiers[i];
                if (!t.unlocked) continue;
                clickTotal += t.timesCollected;
                int recent = RecentClicks(i);
                Row("   " + t.displayName, number(t.timesCollected) + (recent > 0 ? "  <color=#FFD24D>+" + recent + "</color>" : ""));
            }
            Row("All pixels", number(clickTotal));
        }

        Head("Husks");
        Cnt("Vacuumed up", "old.vacuumed");
        Cnt("Stored in the bank", "old.banked");
        Cnt("Grabbed", "old.grabbed");
        Cnt("Shattered (glass)", "old.shattered");
        if (Counter("recycler.cycles") > 0d)
        {
            Cnt("Recycled", "recycler.husks");
            Cnt("   Recycler cycles", "recycler.cycles");
            Cnt("   Pixels paid back", "recycler.paid");
        }
        if (Counter("old.shredded") > 0d) Cnt("Shredded (Wood Chipper)", "old.shredded");

        Head("Block Hold");
        Cnt("Blocks held", "hold.count");
        Cnt("Stakes won", "stake.won");
        Cnt("Stakes lost", "stake.lost");
        double bestStake = Counter("stake.best");
        Row("Best stake", bestStake > 0d ? "x" + bestStake.ToString("0.0") : "-");

        PixelSkills skills = PixelSkills.Instance;
        if (skills != null && skills.Packs != null)
        {
            // Every skill you can train (Standard always) with its level, and the hazards of its pack set off / cleared.
            Head("Skills");
            foreach (PixelSkills.SkillPack sp in skills.Packs)
            {
                if (sp == null || (sp.id != PixelSkills.StandardId && !skills.IsAvailable(sp))) continue;
                Row(sp.displayName, "Level " + skills.LevelOf(sp));
                if (Counter("hazards.triggered." + sp.id) > 0d) Cnt("   Hazards set off", "hazards.triggered." + sp.id);
                if (Counter("hazards.defused." + sp.id) > 0d) Cnt("   Hazards cleared", "hazards.defused." + sp.id);
            }
        }

        Head("Combo and time");
        Num("Highest combo", S.highestCombo);
        Num("Highest combo tier", Counter("combo.tier"));
        if (Counter("timestop.uses") > 0d || Counter("timeslow.uses") > 0d)
        {
            Row("Time stopped", FormatTime(Counter("timestop.seconds")));
            Cnt("   Times used", "timestop.uses");
            Row("Time slowed", FormatTime(Counter("timeslow.seconds")));
            Cnt("   Times used", "timeslow.uses");
        }

        Head("Crafting and items");
        Cnt("Items crafted", "crafts");
        Num("Potions used", TotalPotionsUsed);
        if (Counter("fuel.used") > 0d) Cnt("Combo Fuel used", "fuel.used");
        if (Counter("bait.used") > 0d) Cnt("Ghost Bait used", "bait.used");
        if (Counter("treat.used") > 0d) Cnt("Pet Treats used", "treat.used");

        // Every device kind that has been placed at least once (the fan / vacuum device keep their old typed counters; retired kinds are skipped).
        Head("Devices placed");
        bool anyDevice = false;
        foreach (PixelConsumables.DeviceKind kind in (PixelConsumables.DeviceKind[])Enum.GetValues(typeof(PixelConsumables.DeviceKind)))
        {
            if (PixelConsumables.IsRetiredKind(kind)) continue;
            double placed = kind == PixelConsumables.DeviceKind.Fan ? S.fansUsed
                          : kind == PixelConsumables.DeviceKind.Vacuum ? S.vacuumDevicesUsed
                          : Counter("device.placed." + kind);
            if (placed <= 0d) continue;
            anyDevice = true;
            Num(kind == PixelConsumables.DeviceKind.Vacuum ? "Vacuum Device" : SpacedName(kind.ToString()), placed);
        }
        if (!anyDevice) Row("None yet", "-");

        // The rare finds: how many of each set you have ever found (the Collection Log shows which).
        if (clicker != null)
        {
            int dragons = 0, gods = 0, primes = 0;
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
            {
                if (t.totalCollected <= 0d && t.count <= 0d) continue;
                if (PixelClicker.IsDragonCube(t.type)) dragons++;
                else if (t.type == PixelClicker.PixelType.GodSlifer || t.type == PixelClicker.PixelType.GodRa || t.type == PixelClicker.PixelType.GodObelisk) gods++;
                else if ((int)t.type >= (int)PixelClicker.PixelType.StoneSpace && (int)t.type < (int)PixelClicker.PixelType.InfinityCubelette) primes++;
            }
            Head("Rare finds");
            Row("Dragon Cubes found", dragons + " / 7");
            if (Counter("dragon.summons") > 0d) Cnt("   Dragons summoned", "dragon.summons");
            if (Counter("dragon.wishes") > 0d) Cnt("   Wishes made", "dragon.wishes");
            Row("God Cubes found", gods + " / 3");
            Row("Prime Cubes found", primes + " / 6");
            if (Counter("infinity.forged") > 0d) Cnt("   Prime Cubelettes forged", "infinity.forged");
            if (Counter("snitch.seen") > 0d)
            {
                Cnt("Golden Snitches seen", "snitch.seen");
                Cnt("   Caught", "snitch.caught");
                Cnt("   Escaped", "snitch.escaped");
                double fastest = Counter("snitch.fastest");
                if (fastest > 0d) Row("   Fastest catch", fastest.ToString("0.0") + "s");
            }
        }

        PixelPets petSystem = PixelPets.Instance;
        if (petSystem != null && clicker != null)
        {
            Head("Pets");
            int petTypes = 0;
            foreach (PixelClicker.PixelTier t in clicker.Tiers) if (PixelPets.CanHavePet(t.type)) petTypes++;
            Row("Pets found", petSystem.OwnedCount + " / " + petTypes);
            foreach (PixelClicker.PixelTier t in clicker.Tiers)
                if (PixelPets.CanHavePet(t.type)) Row("   " + (petSystem.IsOwned(t.type) ? petSystem.NameOf(t.type) : "???"), petSystem.IsOwned(t.type) ? (petSystem.IsOn(t.type) ? "Roaming" : "Switched off") : "-");
            if (petSystem.IsOwned(PixelClicker.PixelType.Vacuum) || Counter("pet.vacuum") > 0d)
                Cnt("Husks sucked by the Vacuum Pet", "pet.vacuum");
        }

        Head("Minigames");
        double ultra = clicker != null ? clicker.UltraEarned : 0d;
        Num("Ultra pixels earned", ultra);
        foreach (PixelMinigame g in PixelMinigame.All)   // (every minigame that has given any: a new Ultra source is listed by itself)
            if (g != null && Counter("ultra." + g.Id) > 0d) Num("   from " + g.DisplayName, Counter("ultra." + g.Id));
        if (Seen("weakspot", "weakspot.rounds"))
        {
            Cnt("Weak Spots rounds", "weakspot.rounds");
            Cnt("Weak Spots won", "weakspot.wins");
        }
        if (Seen("ghost", "ghost.kept") || S.ghostsClicked > 0)
        {
            Num("Ghosts clicked", S.ghostsClicked);
            Cnt("   Potions kept in the backpack", "ghost.kept");
        }
        if (Seen("meteor") || S.meteorsSpawned > 0)
        {
            Num("Meteors spawned", S.meteorsSpawned);
            Num("Meteors clicked", S.meteorsClicked);
        }
        PixelMinigame stardust = PixelMinigame.Find("stardust");
        if (stardust != null && stardust.TrackerCount > 0d) Num("Stardust collected", stardust.TrackerCount);
        if (Seen("blackhole", "holes.fed") || S.blackHolesSpawned > 0)
        {
            Num("Black holes opened", S.blackHolesSpawned);
            Cnt("   Pixels fed to them", "holes.fed");
            Num("   Singularities fed", S.singularitiesFed);
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
        if (Seen("dash", "dash.rounds", "dash.clicks"))
        {
            Cnt("Cube Dash rounds", "dash.rounds");
            Cnt("Cube Dash clicks", "dash.clicks");
            Row("Best Cube Dash", PixelClicker.FormatNumber(Counter("dash.best")) + " clicks");
        }
        if (Seen("traffic", "traffic.rounds", "traffic.best"))
        {
            Cnt("Red Light Green Light rounds", "traffic.rounds");
            Row("Best Red Light Green Light", PixelClicker.FormatNumber(Counter("traffic.best")) + " points");
        }
        if (Seen("evade", "evade.rounds", "evade.clicks"))
        {
            Cnt("Cube Evasion rounds", "evade.rounds");
            Cnt("Cube Evasion clicks", "evade.clicks");
            Row("Best Cube Evasion", PixelClicker.FormatNumber(Counter("evade.best")) + " clicks");
        }
        if (Seen("blink", "blink.rounds", "blink.clicks"))
        {
            Cnt("Cube Blink rounds", "blink.rounds");
            Cnt("Cube Blink clicks", "blink.clicks");
            Row("Best Cube Blink", PixelClicker.FormatNumber(Counter("blink.best")) + " clicks");
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
