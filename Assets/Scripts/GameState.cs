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
    public static readonly string[] MovedSystems = { "PixelBank", "PixelStats", "PixelAutoClicker", "PixelFocus", "PixelTimeStop", "PixelGrab", "PixelBaitBucket", "PixelCubieBill", "PixelCombo", "PixelHints" };

    public BankState bank = new BankState();
    public StatsState stats = new StatsState();
    public AutoClickerState autoClicker = new AutoClickerState();
    public FocusState focus = new FocusState();
    public SwitchState timeStop = new SwitchState();   // Time Stop's Toggles switch (PixelTimeStop)
    public SwitchState grab = new SwitchState();       // Pixel Grabbing's Toggles switch (PixelGrab)
    public BaitBucketState baitBucket = new BaitBucketState();
    public CubieBillState cubieBill = new CubieBillState();
    public SwitchState combo = new SwitchState();      // the Combo Meter's Toggles switch (PixelCombo)
    public EventLogState eventLog = new EventLogState();

    /// <summary>Fills in sub-states missing from an older save file (JsonUtility leaves a part the file lacks null in some cases).</summary>
    public void Repair()
    {
        if (bank == null) bank = new BankState();
        if (stats == null) stats = new StatsState();
        if (autoClicker == null) autoClicker = new AutoClickerState();
        if (focus == null) focus = new FocusState();
        if (timeStop == null) timeStop = new SwitchState();
        if (grab == null) grab = new SwitchState();
        if (baitBucket == null) baitBucket = new BaitBucketState();
        if (cubieBill == null) cubieBill = new CubieBillState();
        if (combo == null) combo = new SwitchState();
        if (eventLog == null) eventLog = new EventLogState();
        eventLog.Repair();
        baitBucket.Repair();
        cubieBill.Repair();
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

/// <summary>A bought feature's on / off switch in the Toggles window (Time Stop, Pixel Grabbing; reuse it for the others with only a switch).</summary>
[Serializable]
public class SwitchState
{
    public bool userDisabled;   // switched OFF by the player
}

/// <summary>The Bait Bucket (PixelBaitBucket): whether one stands on the floor, where, and the Fish / Water husks stored in it (by pixel type, with their payout).
/// The bucket itself is a world object: it writes its spot here while it stands and is rebuilt from it on load.</summary>
[Serializable]
public class BaitBucketState
{
    [Serializable]
    public class Entry
    {
        public int type;       // PixelClicker.PixelType
        public double amount;  // the husk's payout, kept for when it comes out again
        public bool fish;      // fish (live bait) or water
    }

    public bool placed;
    public UnityEngine.Vector3 position;
    public float yaw;
    public List<Entry> stored = new List<Entry>();

    public void Repair()
    {
        if (stored == null) stored = new List<Entry>();
        stored.RemoveAll(e => e == null);
    }

    public int FishCount { get { int n = 0; foreach (Entry e in stored) if (e.fish) n++; return n; } }
    public int WaterCount => stored.Count - FishCount;
}

/// <summary>Cubie's bill (PixelCubieBill): what was paid of each pure colour (by pixel type), the trust tier reached and whether the bill was revealed.</summary>
[Serializable]
public class CubieBillState
{
    [Serializable]
    public class Paid
    {
        public int type;       // PixelClicker.PixelType of the colour
        public double amount;
    }

    public int tier;
    public bool revealed;
    public List<Paid> paid = new List<Paid>();

    public void Repair()
    {
        if (paid == null) paid = new List<Paid>();
        paid.RemoveAll(p => p == null);
        if (tier < 0) tier = 0;
    }

    public double PaidOf(int type)
    {
        foreach (Paid p in paid) if (p.type == type) return p.amount;
        return 0d;
    }

    public void SetPaid(int type, double amount)
    {
        foreach (Paid p in paid) if (p.type == type) { p.amount = amount; return; }
        paid.Add(new Paid { type = type, amount = amount });
    }
}

/// <summary>The event log's lines of this save (PixelHints), oldest first: the short line and the tip shown when it is clicked.</summary>
[Serializable]
public class EventLogState
{
    [Serializable]
    public class Entry
    {
        public string text;
        public string full;   // the tip shown when the line is clicked (may be empty)
    }

    public List<Entry> lines = new List<Entry>();

    public void Repair()
    {
        if (lines == null) lines = new List<Entry>();
        lines.RemoveAll(e => e == null || string.IsNullOrEmpty(e.text));
        foreach (Entry e in lines) if (e.full == null) e.full = "";
    }
}
