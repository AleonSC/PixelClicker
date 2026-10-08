using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for minigames (ghost hunt, black hole...). It is what lets the shop, the trackers and the save system
/// treat every minigame the same way, so a new minigame needs NO changes to those scripts.
///
/// TO ADD A MINIGAME:
///  1. Make a script that extends PixelMinigame and implement Running / Activate / Deactivate (and DefaultId).
///  2. Optional goal counter: override HasTracker, TrackerTitle, TrackerDescription, TrackerCount, TrackerGoal and
///     RequirementFormat. It then gets a tracker row in the shop's Minigames tab, is saved automatically, and any
///     shop pack can require its goal.
///  3. In the shop's Packs list: put the minigame's id in a pack's 'Unlocks Minigame' (buying it switches the minigame on)
///     and/or another pack's 'Requires Minigame Goal'.
/// </summary>
public abstract class PixelMinigame : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Happened = null;
        registry.Clear();
    }

    private static readonly List<PixelMinigame> registry = new List<PixelMinigame>();

    /// <summary>Every minigame currently in the scene.</summary>
    public static IReadOnlyList<PixelMinigame> All => registry;

    /// <summary>The minigame with this id, or null.</summary>
    public static PixelMinigame Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (PixelMinigame m in registry)
            if (m != null && m.Id == id) return m;
        return null;
    }

    // ------------------------------------------------------------------
    // Identity and switching on/off
    // ------------------------------------------------------------------

    /// <summary>The id shop packs use to refer to this minigame (e.g. "ghost").</summary>
    public abstract string Id { get; }

    /// <summary>Is the minigame running?</summary>
    public abstract bool Running { get; }

    /// <summary>Things a minigame reports for the stats.</summary>
    public enum MinigameEvent { Spawned, Clicked }

    /// <summary>Raised whenever a minigame spawns its thing (ghost, meteor, hole) or the player clicks it.</summary>
    public static event System.Action<PixelMinigame, MinigameEvent> Happened;

    /// <summary>Subclasses call this when something happens (for the stats).</summary>
    protected void Report(MinigameEvent what) => Happened?.Invoke(this, what);

    /// <summary>Name shown in menus (e.g. the dev tools). Defaults to the id.</summary>
    public virtual string DisplayName => Id;

    /// <summary>Triggers one round of the minigame right now (dev tools / testing). Default: nothing.</summary>
    public virtual void SpawnNow() { }

    /// <summary>The player switched this (bought) minigame off in the shop. Saved. The shop won't start it while this is true.</summary>
    public bool UserDisabled { get; set; }

    /// <summary>Starts the minigame (a shop purchase). Safe to call more than once.</summary>
    public abstract void Activate();

    /// <summary>Stops the minigame (its tracker keeps its count).</summary>
    public abstract void Deactivate();

    // ------------------------------------------------------------------
    // Optional goal counter ("tracker")
    // ------------------------------------------------------------------

    /// <summary>True if this minigame has a goal counter (shown in the shop, saved, usable as a pack requirement).</summary>
    public virtual bool HasTracker => false;

    public virtual string TrackerTitle => "";

    public virtual string TrackerDescription => "";

    /// <summary>Progress so far.</summary>
    public virtual double TrackerCount => 0d;

    /// <summary>Progress needed to reach the goal.</summary>
    public virtual double TrackerGoal => 1d;

    /// <summary>Text for a locked pack that needs the goal. {0} = progress, {1} = goal.</summary>
    public virtual string RequirementFormat => "Requires: {0} / {1}";

    /// <summary>True once the goal has been reached.</summary>
    public bool GoalReached => HasTracker && TrackerCount >= TrackerGoal;

    /// <summary>Sets the progress (used when loading a save).</summary>
    public virtual void SetTrackerCount(double value) { }

    /// <summary>How much of this minigame's currency (e.g. bomb parts) can be spent right now. By default the goal counter itself.</summary>
    public virtual double SpendableCount => TrackerCount;

    /// <summary>Spends some of the minigame's currency (e.g. bomb parts) as a shop price. Returns false, and spends nothing, if there isn't enough.</summary>
    public virtual bool TrySpendTracker(double amount)
    {
        if (!HasTracker || amount < 0d || TrackerCount < amount) return false;
        SetTrackerCount(TrackerCount - amount);
        return true;
    }

    /// <summary>A second saved number, for minigames whose goal counter and spendable amount differ (the bomb's parts held). Default none.</summary>
    public virtual double ExtraValue => 0d;

    /// <summary>Restores <see cref="ExtraValue"/> when loading a save.</summary>
    public virtual void SetExtraValue(double value) { }

    // ------------------------------------------------------------------
    // Registration
    // ------------------------------------------------------------------

    protected virtual void Awake()
    {
        if (!registry.Contains(this)) registry.Add(this);
    }

    protected virtual void OnDestroy()
    {
        registry.Remove(this);
    }
}
