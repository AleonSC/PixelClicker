// ---- System notes ----
// GameState.cs
//   The player's PROGRESS as plain data (no MonoBehaviour, no UnityEngine.Object references): one [Serializable] sub-state per system that has
//   moved here (BankState, StatsState...). The one current copy is owned by PixelGameState (PixelGameState.Current): saving writes it into the
//   save file whole (SaveData.state), loading replaces it, a new game / Restart starts a fresh 'new GameState()'.
//   Systems read and write their sub-state THROUGH PixelGameState.Current every time (never keep a reference: a load swaps the whole object);
//   their components keep only display, input and Inspector tuning. Husks and placed devices stay physics objects, saved the old way.
//   ADDING A SYSTEM'S PROGRESS: docs/systems/PixelGameState.md (steps + the compile-check rule). (The old per-system save parts - IPixelSaved /
//   PixelSaveRegistry - were retired 2026-10-24: everything is here.)
// ---------------------------------------------------
using System;
using System.Collections.Generic;

[Serializable]
public class GameState
{
    /// <summary>The systems whose progress lives here (they must not register restart resets for it: tools/compile-check/rules.sh checks it). Add a system's class name when it moves.</summary>
    public static readonly string[] MovedSystems = { "PixelBank", "PixelStats", "PixelAutoClicker", "PixelFocus", "PixelTimeStop", "PixelGrab", "PixelBaitBucket", "PixelCubieBill", "PixelCombo", "PixelHints", "PixelRobotWorker", "PixelPets", "PixelAchievements", "PixelConsumables", "PixelSkills", "PixelMinigame", "PixelShop", "PixelClicker" };

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
    public UfoState ufo = new UfoState();
    public PetsState pets = new PetsState();
    public AchievementsState achievements = new AchievementsState();
    public InventoryState inventory = new InventoryState();
    public SkillsState skills = new SkillsState();
    public MinigamesState minigames = new MinigamesState();
    public ShopState shop = new ShopState();
    public ClickerState clicker = new ClickerState();

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
        if (ufo == null) ufo = new UfoState();
        if (pets == null) pets = new PetsState();
        pets.Repair();
        if (achievements == null) achievements = new AchievementsState();
        achievements.Repair();
        if (inventory == null) inventory = new InventoryState();
        inventory.Repair();
        if (skills == null) skills = new SkillsState();
        skills.Repair();
        if (minigames == null) minigames = new MinigamesState();
        minigames.Repair();
        if (shop == null) shop = new ShopState();
        shop.Repair();
        if (clicker == null) clicker = new ClickerState();
        clicker.Repair();
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

/// <summary>The UFO Helper (PixelRobotWorker): switched off in Toggles, and its job - keep one device placed at a spot (device by its
/// display name, as the save always matched it) or bank husks of one pixel type. The ghost of the job is a world object rebuilt from this on load.</summary>
[Serializable]
public class UfoState
{
    public bool userDisabled;
    public string device = "";   // the device kept placed ("" = no device job)
    public UnityEngine.Vector3 position;
    public float yaw;
    public bool bankJob;         // the job is banking husks instead
    public int bankType;         // PixelClicker.PixelType it banks
}

/// <summary>The pets the player owns (PixelPets), in the order found: type, switched off, outfit, the player's name. The pet bodies are world objects
/// rebuilt from this on load; each live pet keeps a reference to its entry here, so changing it changes the state.</summary>
[Serializable]
public class PetsState
{
    [Serializable]
    public class Entry
    {
        public int type;              // PixelClicker.PixelType
        public bool off;              // switched off in the Toggles window
        public int outfit;            // 0 = none
        public string customName = "";   // "" = the default name
    }

    public List<Entry> owned = new List<Entry>();

    public void Repair()
    {
        if (owned == null) owned = new List<Entry>();
        owned.RemoveAll(e => e == null);
        foreach (Entry e in owned) { if (e.customName == null) e.customName = ""; if (e.outfit < 0) e.outfit = 0; }
    }
}

/// <summary>Earned achievements (PixelAchievements), by achievement id: how many tiers and when each was earned (UTC ticks, 0 = unknown).
/// The achievements themselves (titles, tiers, targets) are Inspector config on the component.</summary>
[Serializable]
public class AchievementsState
{
    [Serializable]
    public class Entry
    {
        public string id;
        public int tiers;
        public List<long> earnedAt = new List<long>();
    }

    public List<Entry> earned = new List<Entry>();

    [NonSerialized] private Dictionary<string, Entry> index;   // (a lookup made on first use; never saved)

    public void Repair()
    {
        if (earned == null) earned = new List<Entry>();
        earned.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));
        foreach (Entry e in earned) { if (e.earnedAt == null) e.earnedAt = new List<long>(); if (e.tiers < 0) e.tiers = 0; }
        index = null;
    }

    /// <summary>The entry of an achievement (null when nothing is earned and 'create' is false).</summary>
    public Entry Find(string id, bool create)
    {
        if (id == null) return null;
        if (index == null || index.Count != earned.Count)
        {
            index = new Dictionary<string, Entry>();
            foreach (Entry e in earned) if (e != null && e.id != null) index[e.id] = e;
        }
        if (index.TryGetValue(id, out Entry found) || !create) return found;
        found = new Entry { id = id };
        earned.Add(found);
        index[id] = found;
        return found;
    }

    public void Clear() { earned.Clear(); index = null; }
}

/// <summary>The potion / device inventory (PixelConsumables): how many of each item you hold (potions by type + combo types, devices by
/// display name, as the save always matched them), the ghost backpack's extra storage per potion, and the WORLD part - the placed devices
/// and the running potion - which those copy in on PixelGameState.Saving and are rebuilt from on load.</summary>
[Serializable]
public class InventoryState
{
    [Serializable]
    public class Held
    {
        public string key;     // "p:type:second:third" for a potion (second / third = pixel type + 1 of a combo, 0 = none), "d:<display name>" for a device
        public int owned;
        public int bonus;      // a potion's extra storage from the ghost backpack
    }

    public List<Held> held = new List<Held>();

    // The world part (written on Saving, read on Loaded: between saves it is NOT kept up to date).
    public List<PixelConsumables.PlacedState> placed = new List<PixelConsumables.PlacedState>();
    public bool hasActive;
    public int activeType, activeSecond, activeThird;
    public float activeRemaining;

    [NonSerialized] private Dictionary<string, Held> index;   // (a lookup made on first use; never saved)

    public void Repair()
    {
        if (held == null) held = new List<Held>();
        held.RemoveAll(h => h == null || string.IsNullOrEmpty(h.key));
        if (placed == null) placed = new List<PixelConsumables.PlacedState>();
        placed.RemoveAll(p => p == null);
        index = null;
    }

    /// <summary>The entry of an item (null when there is none and 'create' is false).</summary>
    public Held Find(string key, bool create)
    {
        if (key == null) return null;
        if (index == null || index.Count != held.Count)
        {
            index = new Dictionary<string, Held>();
            foreach (Held h in held) if (h != null && h.key != null) index[h.key] = h;
        }
        if (index.TryGetValue(key, out Held found) || !create) return found;
        found = new Held { key = key };
        held.Add(found);
        index[key] = found;
        return found;
    }

    public int OwnedOf(string key) { Held h = Find(key, false); return h != null ? h.owned : 0; }
    public void SetOwned(string key, int value) { Held h = Find(key, value != 0); if (h != null) h.owned = value; }
    public int BonusOf(string key) { Held h = Find(key, false); return h != null ? h.bonus : 0; }
    public void SetBonus(string key, int value) { Held h = Find(key, value != 0); if (h != null) h.bonus = value; }
}

/// <summary>The skill choices of this save (PixelSkills): the pack in play, the guest blocks switched on, the offline task. Skill XP, bought
/// tree nodes, the bonus XP bank and Mastery are StatsState counters (PixelStats.Count / Total with the skill's keys), so they are in the GameState too.</summary>
[Serializable]
public class SkillsState
{
    public string activePack = "";        // the pack the player chose to train ('' / 'standard' = the usual rotation)
    public List<string> guests = new List<string>();   // guest blocks switched on (pixel type names) - a tree node / perk unlocks the switch
    public string offlineTask = "";       // what your character does while the game is closed ('' = Standard)

    public void Repair()
    {
        if (activePack == null) activePack = "";
        if (offlineTask == null) offlineTask = "";
        if (guests == null) guests = new List<string>();
        guests.RemoveAll(string.IsNullOrEmpty);
    }
}

/// <summary>Every minigame's progress (PixelMinigame and its subclasses), by minigame id: switched off by the player, its goal counter
/// (singularities, ghosts caught, meteor chunks, stardust, magic wood, bomb parts earned...) and a second number some keep (bomb parts held, ectoplasm).</summary>
[Serializable]
public class MinigamesState
{
    [Serializable]
    public class Entry
    {
        public string id;
        public bool disabled;
        public double count;
        public double extra;
    }

    public List<Entry> games = new List<Entry>();

    [NonSerialized] private Dictionary<string, Entry> index;   // (a lookup made on first use; never saved)

    public void Repair()
    {
        if (games == null) games = new List<Entry>();
        games.RemoveAll(e => e == null || string.IsNullOrEmpty(e.id));
        index = null;
    }

    public Entry Find(string id, bool create)
    {
        if (id == null) return null;
        if (index == null || index.Count != games.Count)
        {
            index = new Dictionary<string, Entry>();
            foreach (Entry e in games) if (e != null && e.id != null) index[e.id] = e;
        }
        if (index.TryGetValue(id, out Entry found) || !create) return found;
        found = new Entry { id = id };
        games.Add(found);
        index[id] = found;
        return found;
    }
}

/// <summary>The shop packs bought (PixelShop), by pack display name (as the save always matched them): bought, and the level of an upgrade.</summary>
[Serializable]
public class ShopState
{
    [Serializable]
    public class Entry
    {
        public string name;
        public bool purchased;
        public int level;
    }

    public List<Entry> packs = new List<Entry>();

    [NonSerialized] private Dictionary<string, Entry> index;   // (a lookup made on first use; never saved)

    public void Repair()
    {
        if (packs == null) packs = new List<Entry>();
        packs.RemoveAll(e => e == null || string.IsNullOrEmpty(e.name));
        index = null;
    }

    public Entry Find(string name, bool create)
    {
        if (name == null) return null;
        if (index == null || index.Count != packs.Count)
        {
            index = new Dictionary<string, Entry>();
            foreach (Entry e in packs) if (e != null && e.name != null && !index.ContainsKey(e.name)) index[e.name] = e;
        }
        if (index.TryGetValue(name, out Entry found) || !create) return found;
        found = new Entry { name = name };
        packs.Add(found);
        index[name] = found;
        return found;
    }
}

/// <summary>The currencies (PixelClicker): per pixel type how much you hold, the lifetime total, unlocked, spawning switched off, times collected and
/// the Ultra / Value upgrade levels; and the Ultra pixels held and ever earned. Every live PixelTier of the clicker binds to its entry here (by type).</summary>
[Serializable]
public class ClickerState
{
    [Serializable]
    public class Tier
    {
        public int type;              // PixelClicker.PixelType
        public double count;          // spendable amount
        public double total;          // ever collected
        public bool unlocked;
        public bool spawnDisabled;    // switched off by the player
        public long timesCollected;
        public int ultraLevel;
        public int valueLevel;
    }

    public List<Tier> tiers = new List<Tier>();
    public long ultraHeld;
    public long ultraEarned;

    [NonSerialized] private Dictionary<int, Tier> index;   // (a lookup made on first use; never saved)

    public void Repair()
    {
        if (tiers == null) tiers = new List<Tier>();
        tiers.RemoveAll(t => t == null);
        HashSet<int> seen = new HashSet<int>();
        tiers.RemoveAll(t => !seen.Add(t.type));   // (one entry per type)
        foreach (Tier t in tiers)
        {
            if (t.count < 0d) t.count = 0d;
            if (t.total < 0d) t.total = 0d;
            if (t.timesCollected < 0L) t.timesCollected = 0L;
            if (t.ultraLevel < 0) t.ultraLevel = 0;
            if (t.valueLevel < 0) t.valueLevel = 0;
        }
        if (ultraHeld < 0L) ultraHeld = 0L;
        if (ultraEarned < ultraHeld) ultraEarned = ultraHeld;
        index = null;
    }

    public Tier Find(int type, bool create)
    {
        if (index == null || index.Count != tiers.Count)
        {
            index = new Dictionary<int, Tier>();
            foreach (Tier t in tiers) if (t != null) index[t.type] = t;
        }
        if (index.TryGetValue(type, out Tier found) || !create) return found;
        found = new Tier { type = type };
        tiers.Add(found);
        index[type] = found;
        return found;
    }

    /// <summary>Everything ever collected, all types together (the save slots' 'pixels collected' summary).</summary>
    public double TotalCollected { get { double sum = 0d; foreach (Tier t in tiers) sum += t.total; return sum; } }
}
