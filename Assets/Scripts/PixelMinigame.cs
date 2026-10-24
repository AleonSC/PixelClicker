// ---- System notes (moved here from CLAUDE.md) ----
// PixelMinigame.cs
//   NeedsNormalCamera (default = Takeover): true while the minigame needs the camera at its normal pose (the orbit camera glides home, the zoom snaps back); Weak Spots overrides it to false.
//   Abstract base for minigames (registry PixelMinigame.All/Find(id), Id, Running, Activate/Deactivate, optional goal tracker:
//   HasTracker, TrackerTitle/Description/Count/Goal, RequirementFormat, GoalReached). Skill XP bonus: every
//   Report(MinigameEvent.Clicked) (each success: ghost caught, meteor click, star collected, bomb defused, Ultra Hole drop, Snake /
//   Sorting / Breakout win, Dash round) calls GrantSkillXp(), which gives bonus XP to the skill in training (none while Standard is
//   trained) via PixelSkills.AddXp (so the XP popup shows too); defaults in DefaultSkillXp by id (meteor 1, black hole 1 per swallowed
//   pixel, pad 6, stardust 8, ghost 12, dash 15, bomb 20, snake / sort / breakout 25, others 5); per minigame skillXpReward (0 =
//   default) and disableSkillXp in the Inspector. A minigame can name its own skill with SkillXpTarget (meteor = Mining, evil tree = Woodcutting): its XP then always goes there, whatever is in training (nothing if that skill is not bought). Bought minigames get an on/off row in the Toggles window
//   (PixelMinigame.UserDisabled, progress per minigame in the GameState - MinigamesState, with the goal counters: SavedCount / SavedExtra, 2026-10-24; the shop never auto-starts a disabled one). Shop packs refer to minigames by id
//   (unlocksMinigame; goals via a PackRequirement of kind MinigameGoal); the shop shows tracker rows and the save system stores counts
//   generically - a new minigame needs no changes to PixelShop or PixelSaveGame. For the dev tools' Despawn button a minigame must
//   call Track(go) on everything it puts in the scene (roots, messages, effects) and override OnDespawned() to reset its busy flag /
//   spawn timer (DespawnNow() stops its coroutines and destroys the tracked objects).
//   TakeoverActive, AutoClickerPaused and the floor area claims are cleared on every scene unload (Settings > Restart).
// ---------------------------------------------------
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
        AutoClickerPaused = false;
        claims.Clear();
        registry.Clear();
        PixelStatics.ResetOnRestart(OnSceneUnloaded);   // restart-reset-not-progress: a running round's takeover / auto-clicker pause / floor claims
    }

    // Settings > Restart reloads the scene but statics survive it: a round cut short left TakeoverActive / AutoClickerPaused on, and a
    // black hole's floor claim (its 'active' check reads the destroyed hole) kept blocking that spot in the new game.
    private static void OnSceneUnloaded() { TakeoverActive = false; AutoClickerPaused = false; claims.Clear(); }

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
    protected void Report(MinigameEvent what)
    {
        Happened?.Invoke(this, what);
        if (what == MinigameEvent.Spawned) ShowStartNotice();
        if (what == MinigameEvent.Clicked) GrantSkillXp();   // every 'Clicked' report is a success (caught, defused, won...)
    }

    [Header("Start notice (top status box)")]
    [Tooltip("Turn off the 'it has started' message that slides out under the top bar when this minigame spawns its thing.")]
    [SerializeField] private bool disableStartNotice = false;

    [Tooltip("The message. Empty = this minigame's own default (Ghost, Meteor, Black Hole, Bomb and Evil Tree have one; the others show nothing).")]
    [SerializeField] private string startNotice = "";

    [Tooltip("Colour of that message. Fully see-through (alpha 0) = the default colour.")]
    [SerializeField] private Color startNoticeColor = new Color(1f, 1f, 1f, 0f);

    private void ShowStartNotice()
    {
        if (disableStartNotice) return;
        string text = startNotice;
        Color color = startNoticeColor.a > 0f ? startNoticeColor : new Color(1f, 0.88f, 0.45f, 1f);
        if (string.IsNullOrEmpty(text))
            switch (Id)
            {
                case "ghost": text = "A ghost appeared!"; break;
                case "meteor": text = "Meteor incoming!"; break;
                case "blackhole": text = "A black hole opened!"; break;
                case "bomb": text = "BOMB!"; if (startNoticeColor.a <= 0f) color = new Color(1f, 0.4f, 0.3f, 1f); break;
                case "eviltree": text = "An evil tree is growing!"; break;
            }
        if (!string.IsNullOrEmpty(text)) PixelStatusLines.Set("start." + Id, text, color, 2.5f);
    }

    [Header("Skill XP Bonus")]
    [Tooltip("Turn off the bonus skill XP this minigame gives to the skill in training.")]
    [SerializeField] private bool disableSkillXp = false;

    [Min(0f)]
    [Tooltip("Bonus XP for the skill in training each time you succeed at this minigame (catch, defuse, win...). 0 = this minigame's own default.")]
    [SerializeField] private float skillXpReward = 0f;

    /// <summary>The coded default bonus XP per success, by minigame (frequent small events give little, big rounds give more).</summary>
    private float DefaultSkillXp()
    {
        switch (Id)
        {
            case "meteor": return 1f;
            case "eviltree": return 30f;
            case "blackhole": return 1f;
            case "pad": return 6f;
            case "stardust": return 8f;
            case "ghost": return 12f;
            case "dash":
            case "blink":
            case "evade": return 15f;
            case "traffic": return 20f;
            case "bomb": return 20f;
            case "snake":
            case "sort":
            case "breakout": return 25f;
            default: return 5f;
        }
    }

    /// <summary>The skill this minigame's XP always goes to (a skill id), or null = whichever skill is in training. The meteor trains Mining and the evil tree Woodcutting, whatever you are training.</summary>
    protected virtual string SkillXpTarget => null;

    /// <summary>Gives bonus XP to the skill in training (nothing while no skill is in training), or to the minigame's own skill (<see cref="SkillXpTarget"/>) when it has one. Turning minigames on is part of the reward.</summary>
    protected void GrantSkillXp(float amount = 0f)
    {
        if (disableSkillXp || PixelSkills.Instance == null) return;
        PixelSkills skills = PixelSkills.Instance;
        PixelSkills.SkillPack pack;
        if (!string.IsNullOrEmpty(SkillXpTarget))
        {
            pack = skills.Find(SkillXpTarget);
            if (pack == null || !skills.IsAvailable(pack)) return;   // its skill has not been bought yet: no XP
        }
        else
        {
            pack = skills.Find(skills.TrainingId);
            if (pack == null || pack.id == PixelSkills.StandardId) return;
        }
        float xp = amount > 0f ? amount : (skillXpReward > 0f ? skillXpReward : DefaultSkillXp());
        skills.AddXp(pack, xp);
    }

    /// <summary>Name shown in menus (e.g. the dev tools). Defaults to the id.</summary>
    public virtual string DisplayName => Id;

    /// <summary>True while this minigame has something on screen (a ghost, a meteor, a hole...). Used to limit how many run at once.</summary>
    public virtual bool Busy => false;

    /// <summary>
    /// True for minigames that take over every old pixel on the floor (Snake, Sorting Race). Only one such round runs at a time,
    /// and while it does cube clicks and Pixel Grabbing are off.
    /// </summary>
    public virtual bool Takeover => false;

    /// <summary>
    /// True while this minigame needs the camera back at its normal pose (Dash / Blink / Evade move the cube about on screen): the orbit camera glides home
    /// and the zoom snaps back. Defaults to <see cref="Takeover"/>; Weak Spots is a takeover only to stop other minigames, and uses its own camera, so it says false.
    /// </summary>
    public virtual bool NeedsNormalCamera => Takeover;

    /// <summary>True while a minigame wants the auto clicker switched off (Cube Dash: only your own clicks count). The cube stays clickable.</summary>
    public static bool AutoClickerPaused { get; protected set; }

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

    // Floor areas that a minigame's thing (a black hole, an Ultra Pad) occupies, so two of them never spawn on top of each other.
    private class AreaClaim { public object owner; public Vector3 centre; public float radius; public System.Func<bool> active; }
    private static readonly System.Collections.Generic.List<AreaClaim> claims = new System.Collections.Generic.List<AreaClaim>();

    /// <summary>Reserves a round floor area for 'owner' for as long as 'active' returns true (replaces the owner's earlier claim).</summary>
    protected static void ClaimArea(object owner, Vector3 centre, float radius, System.Func<bool> active)
    {
        claims.RemoveAll(c => c.owner == owner);
        claims.Add(new AreaClaim { owner = owner, centre = centre, radius = radius, active = active });
    }

    /// <summary>Drops this owner's area claim (a round ended or a new one is starting).</summary>
    protected static void ReleaseArea(object owner)
    {
        claims.RemoveAll(c => c.owner == owner);
    }

    /// <summary>Is this round floor area clear of every other minigame's claimed area? (Measured on the floor, ignoring height.)</summary>
    protected static bool AreaFree(object self, Vector3 centre, float radius)
    {
        foreach (AreaClaim c in claims)
        {
            if (c.owner == self || c.active == null || !c.active()) continue;
            float dx = c.centre.x - centre.x, dz = c.centre.z - centre.z;
            float sum = c.radius + radius;
            if (dx * dx + dz * dz < sum * sum) return false;
        }
        return true;
    }

    /// <summary>Ultra pixels this minigame has given in the current round (reset by the minigame when a round starts).</summary>
    protected int ultraRoundCount;

    /// <summary>
    /// The shared Ultra roll: 'chance' (or 'defaultChance' when it is 0, e.g. an older saved scene) that a pixel of this type gives one
    /// Ultra pixel, up to 'cap' per round ('defaultCap' when 0). Announces the gain in the event log. Returns true if one was given.
    /// </summary>
    protected bool UltraRoll(PixelClicker clicker, float chance, float defaultChance, int cap, int defaultCap,
                             bool disabled, float chanceMultiplier = 1f, Vector3? at = null)
    {
        if (disabled || clicker == null) return false;
        if (ultraRoundCount >= (cap > 0 ? cap : defaultCap)) return false;
        if (!clicker.RollUltra((chance > 0f ? chance : defaultChance) * chanceMultiplier)) return false;
        ultraRoundCount++;
        PixelStats.Count("ultra." + Id);
        PixelHints.Announce("Ultra pixel gained!");
        if (at.HasValue) PixelUltraFx.Play(clicker, at.Value);
        return true;
    }

    /// <summary>Reset the 'something is on screen' state (busy flag, next-spawn timer, effects) after <see cref="DespawnNow"/>.</summary>
    protected virtual void OnDespawned() { }

    // ---- progress: each minigame's switch, goal counter and second number live in the GameState (PixelGameState.Current.minigames, by Id) ----
    private MinigamesState.Entry Saved(bool create) => PixelGameState.Current.minigames.Find(Id, create);

    /// <summary>The player switched this (bought) minigame off in the shop. Saved (GameState). The shop won't start it while this is true.</summary>
    public bool UserDisabled
    {
        get { MinigamesState.Entry e = Saved(false); return e != null && e.disabled; }
        set { MinigamesState.Entry e = Saved(value); if (e != null) e.disabled = value; }
    }

    /// <summary>A subclass's goal counter, kept in the GameState (a subclass keeps its counter in a property over this, not a field).</summary>
    protected double SavedCount
    {
        get { MinigamesState.Entry e = Saved(false); return e != null ? e.count : 0d; }
        set { MinigamesState.Entry e = Saved(value != 0d); if (e != null) e.count = value; }
    }

    /// <summary>A subclass's second saved number (bomb parts held, ectoplasm), kept in the GameState.</summary>
    protected double SavedExtra
    {
        get { MinigamesState.Entry e = Saved(false); return e != null ? e.extra : 0d; }
        set { MinigamesState.Entry e = Saved(value != 0d); if (e != null) e.extra = value; }
    }

    /// <summary>A save was loaded (its counters are in the GameState now): a switched-off minigame stops; a subclass fixes up what follows from its counter (OnProgressLoaded).</summary>
    private void OnGameLoaded()
    {
        if (UserDisabled) Deactivate();
        OnProgressLoaded();
    }

    /// <summary>After a load: e.g. a goal already reached is not announced again.</summary>
    protected virtual void OnProgressLoaded() { }

    /// <summary>False = the player can't switch this one off (no switch in the Toggles window; e.g. the very rare cat visitors).</summary>
    public virtual bool CanSwitchOff => true;

    /// <summary>A short note shown as an orange '!' tooltip on this one's row in the Toggles window (null = none), e.g. when a vendor visits.</summary>
    public virtual string ToggleNote => null;

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

    /// <summary>Name of the crafting material this minigame hands out (shown in the Inventory's Materials tab), or null for none.</summary>
    public virtual string MaterialName => null;

    /// <summary>How much of that material the player holds.</summary>
    public virtual double MaterialAmount => 0d;

    /// <summary>Colour of the material's line in the Materials tab.</summary>
    public virtual Color MaterialColor => Color.white;

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
        PixelGameState.Loaded += OnGameLoaded;
    }

    protected virtual void OnDestroy()
    {
        registry.Remove(this);
        PixelGameState.Loaded -= OnGameLoaded;
    }

    /// <summary>
    /// Its Game Database entry ('Minigame': the goal counter's text and title, the id), called for every minigame in turn by PixelDatabase's minigame step.
    /// Returns the entry; a subclass adds more to it (a vendor lists its wares: PixelVisitorMinigame).
    /// </summary>
    /// <summary>The Game Database category of this minigame's entry ('Minigame'; the visitors are 'Character').</summary>
    protected virtual string DatabaseCategory => "Minigame";
    /// <summary>The name of this minigame's Game Database entry (a visitor: the person's name).</summary>
    protected virtual string DatabaseName => DisplayName;

    public virtual PixelDatabase.Entry AddToDatabase(PixelDatabase.Builder b)
    {
        string desc = HasTracker ? PixelDatabase.Plain(TrackerDescription) : "";
        PixelDatabase.Entry e = b.Add(DatabaseCategory, DatabaseName, (string.IsNullOrEmpty(desc) ? "" : desc + "\n") + "Id: " + Id + (HasTracker ? "\nHas a goal counter: " + PixelDatabase.Plain(TrackerTitle) : ""));
        b.Minigames[Id] = e;
        return e;
    }
}
