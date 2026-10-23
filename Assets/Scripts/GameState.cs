// ---- System notes ----
// GameState.cs
//   The player's PROGRESS as plain data (no MonoBehaviour, no UnityEngine.Object references): one [Serializable] sub-state per system that has
//   moved here (BankState, StatsState...). The one current copy is owned by PixelGameState (PixelGameState.Current): saving writes it into the
//   save file whole (SaveData.state), loading replaces it, a new game / Restart starts a fresh 'new GameState()'.
//   Systems read and write their sub-state THROUGH PixelGameState.Current every time (never keep a reference: a load swaps the whole object);
//   their components keep only display, input and Inspector tuning. Husks and placed devices stay physics objects, saved the old way.
//   MOVING A SYSTEM HERE: docs/systems/PixelGameState.md (steps + the compile-check rule). Systems still saving themselves use IPixelSaved.
// ---------------------------------------------------
using System;
using System.Collections.Generic;

[Serializable]
public class GameState
{
    /// <summary>The systems whose progress lives here (they must not implement IPixelSaved or register restart resets: tools/compile-check/rules.sh checks it). Add a system's class name when it moves.</summary>
    public static readonly string[] MovedSystems = { "PixelBank", "PixelStats", "PixelAutoClicker", "PixelFocus" };

    public BankState bank = new BankState();
    public StatsState stats = new StatsState();
    public AutoClickerState autoClicker = new AutoClickerState();
    public FocusState focus = new FocusState();

    /// <summary>Fills in sub-states missing from an older save file (JsonUtility leaves a part the file lacks null in some cases).</summary>
    public void Repair()
    {
        if (bank == null) bank = new BankState();
        if (stats == null) stats = new StatsState();
        if (autoClicker == null) autoClicker = new AutoClickerState();
        if (focus == null) focus = new FocusState();
        bank.Repair();
        stats.Repair();
    }
}

/// <summary>The Pixel Bank's stored husks, by pixel type (PixelBank).</summary>
[Serializable]
public class BankState
{
    [Serializable]
    public class Entry
    {
        public int type;      // PixelClicker.PixelType
        public long count;
        public double value;  // total payout of the stored husks (a spat-out husk keeps its worth)
    }

    public List<Entry> stored = new List<Entry>();

    public void Repair()
    {
        if (stored == null) stored = new List<Entry>();
        stored.RemoveAll(e => e == null || e.count <= 0);
    }

    /// <summary>The entry of a pixel type, or null when none is stored.</summary>
    public Entry Find(int type)
    {
        for (int i = 0; i < stored.Count; i++) if (stored[i].type == type) return stored[i];
        return null;
    }

    public long CountOf(int type) { Entry e = Find(type); return e != null ? e.count : 0L; }
    public double ValueOf(int type) { Entry e = Find(type); return e != null ? e.value : 0d; }

    /// <summary>Sets what is stored of a type (0 removes the entry).</summary>
    public void Set(int type, long count, double value)
    {
        Entry e = Find(type);
        if (count <= 0) { if (e != null) stored.Remove(e); return; }
        if (e == null) { e = new Entry { type = type }; stored.Add(e); }
        e.count = count;
        e.value = Math.Max(0d, value);
    }

    public long Total
    {
        get { long t = 0; for (int i = 0; i < stored.Count; i++) t += stored[i].count; return t; }
    }
}

/// <summary>Play statistics (PixelStats): the typed numbers plus the generic counters (PixelStats.Count / Best / Fastest).</summary>
[Serializable]
public class StatsState
{
    [Serializable]
    public class Counter
    {
        public string key;
        public double value;
    }

    [Serializable]
    public class PotionCount
    {
        public string name;
        public long count;
    }

    public long manualClicks, autoClicks;
    public double playSeconds, pixelsSpent;
    public long ghostsClicked, meteorsClicked, meteorsSpawned, blackHolesSpawned, fansUsed, vacuumDevicesUsed, singularitiesFed;
    public int highestCombo;
    public List<PotionCount> potionsUsed = new List<PotionCount>();   // in the order first drunk
    public List<Counter> counters = new List<Counter>();

    [NonSerialized] private Dictionary<string, Counter> index;   // (a lookup made on first use; never saved)

    public void Repair()
    {
        if (potionsUsed == null) potionsUsed = new List<PotionCount>();
        if (counters == null) counters = new List<Counter>();
        potionsUsed.RemoveAll(p => p == null || string.IsNullOrEmpty(p.name));
        counters.RemoveAll(c => c == null || string.IsNullOrEmpty(c.key));
        index = null;
    }

    private Counter Find(string key, bool create)
    {
        if (index == null || index.Count != counters.Count)
        {
            index = new Dictionary<string, Counter>();
            foreach (Counter c in counters) if (c != null && c.key != null) index[c.key] = c;
        }
        if (index.TryGetValue(key, out Counter found)) return found;
        if (!create) return null;
        found = new Counter { key = key };
        counters.Add(found);
        index[key] = found;
        return found;
    }

    public double Get(string key) { Counter c = key != null ? Find(key, false) : null; return c != null ? c.value : 0d; }
    public void Set(string key, double value) { if (key != null) Find(key, true).value = value; }

    public void Remove(string key)
    {
        Counter c = key != null ? Find(key, false) : null;
        if (c == null) return;
        counters.Remove(c);
        index.Remove(key);
    }
}

/// <summary>The Auto Clicker (PixelAutoClicker): bought, switched off in Toggles, and its upgraded speed (0 = the component's Inspector base).</summary>
[Serializable]
public class AutoClickerState
{
    public bool running;          // bought (the shop's Auto Clicker pack, or 'Start Running' for testing)
    public bool userDisabled;     // switched off in the Toggles window
    public float interval;        // seconds between clicks from the Interval upgrade (0 = the Inspector base)
    public int clicksPerTick;     // clicks per tick from the Clicks upgrade (0 = the Inspector base)
}

/// <summary>The running Focus potion (PixelFocus): which skill's potion and the seconds left (0 = none running).</summary>
[Serializable]
public class FocusState
{
    public string skillId;
    public string name;           // the potion's display name (picks the right one when a skill has several)
    public float remaining;

    public bool Active => remaining > 0f && !string.IsNullOrEmpty(skillId);
    public void Clear() { skillId = null; name = null; remaining = 0f; }
}
