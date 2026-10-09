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
        TakeoverActive = false;
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

    /// <summary>True while this minigame has something on screen (a ghost, a meteor, a hole...). Used to limit how many run at once.</summary>
    public virtual bool Busy => false;

    /// <summary>
    /// True for minigames that take over every old pixel on the floor (Snake, Sorting Race). Only one such round runs at a time,
    /// and while it does cube clicks and Pixel Grabbing are off.
    /// </summary>
    public virtual bool Takeover => false;

    /// <summary>True while any takeover minigame has a round going. Takeover minigames set it when their round starts / ends.</summary>
    public static bool TakeoverActive { get; protected set; }

    /// <summary>Triggers one round of the minigame right now (dev tools / testing). Default: nothing.</summary>
    public virtual void SpawnNow() { }

    private readonly System.Collections.Generic.List<GameObject> tracked = new System.Collections.Generic.List<GameObject>();

    /// <summary>Subclasses call this for everything they put in the scene, so <see cref="DespawnNow"/> can remove it.</summary>
    protected void Track(GameObject go)
    {
        if (go == null) return;
        tracked.RemoveAll(g => g == null);
        tracked.Add(go);
    }

    /// <summary>Removes whatever this minigame currently has on screen and stops its round (dev tools). The timer for the next one restarts.</summary>
    public void DespawnNow()
    {
        StopAllCoroutines();
        foreach (GameObject go in tracked) if (go != null) Destroy(go);
        tracked.Clear();
        OnDespawned();
    }

    /// <summary>Ultra pixels this minigame has given in the current round (reset by the minigame when a round starts).</summary>
    protected int ultraRoundCount;

    /// <summary>
    /// The shared Ultra roll: 'chance' (or 'defaultChance' when it is 0, e.g. an older saved scene) that a pixel of this type gives one
    /// Ultra pixel, up to 'cap' per round ('defaultCap' when 0). Announces the gain in the event log. Returns true if one was given.
    /// </summary>
    protected bool UltraRoll(PixelClicker clicker, int tier, float chance, float defaultChance, int cap, int defaultCap,
                             bool disabled, float chanceMultiplier = 1f, Vector3? at = null)
    {
        if (disabled || clicker == null || !clicker.IsValidTierIndex(tier)) return false;
        if (ultraRoundCount >= (cap > 0 ? cap : defaultCap)) return false;
        if (!clicker.RollUltra(tier, (chance > 0f ? chance : defaultChance) * chanceMultiplier)) return false;
        ultraRoundCount++;
        PixelHints.Announce("Ultra " + clicker.Tiers[tier].displayName + " pixel gained!");
        if (at.HasValue) PixelUltraFx.Play(clicker, tier, at.Value);
        return true;
    }

    /// <summary>Reset the 'something is on screen' state (busy flag, next-spawn timer, effects) after <see cref="DespawnNow"/>.</summary>
    protected virtual void OnDespawned() { }

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
