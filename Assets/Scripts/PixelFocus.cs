// ---- System notes ----
// PixelFocus.cs
//   FOCUS POTIONS (component added by PixelClicker.Awake, PixelFocus.Instance; one potion per skill, the list is an Inspector list: displayName, skillId, durationSeconds). While one is
//   active, the HAZARD block of its skill stops spawning (PixelSkills.TryGetHazard returns false, so SpecialInfo gives the hazard no weight and the rest of the rotation renormalises;
//   the pre-rolled Next-up block is dropped too, PixelClicker.UpcomingValid). A hazard that is already on the cube is NOT removed. The filler is untouched (the shuffle bonus still works).
//   Only the skill named in skillId is affected, and only while that skill is in play; the timer keeps running if the player switches away.
//   First potion: Lumberjack's Focus (woodcutting, 60 s, holds off the Termite Hive, the Hornet Nest block's new name). Now an ITEM too (PixelConsumables DeviceKind.FocusPotion, Supplies, vendorOnly: the Lumberjack sells it now
//   and then, focusChance; right-click drinks it through DrinkForSkill). Also consumed from Dev Tools (the 'Focus potions' widget: drop-down +
//   Drink + Cancel) through Drink(index) / Cancel(). A shop / crafting recipe from the skill's own materials is the intended source later.
//   Runs on game time (pauses with the game), one potion at a time (drinking another replaces it). The countdown shows as a PixelStatusLines line (key 'focus').
//   PROGRESS (the running potion: skill, name, seconds left) lives in PixelGameState.Current.focus (GameState.cs, moved 2026-10-23): saved / loaded /
//   reset with it; the potion list stays on this component (Inspector). HoldsOffHazards reads the state, so it works without the component.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelFocus : MonoBehaviour
{
    [Serializable]
    public class FocusPotion
    {
        [Tooltip("Name shown in the Dev Tools drop-down and the status line.")]
        public string displayName = "Lumberjack's Focus";
        [Tooltip("The skill (SkillPack id, e.g. 'woodcutting') this potion works in. Its hazard block stops spawning while the potion lasts.")]
        public string skillId = PixelSkills.WoodcuttingId;
        [Min(1f)]
        [Tooltip("Seconds it lasts.")]
        public float durationSeconds = 60f;
    }

    [Header("Focus potions")]
    [SerializeField, Tooltip("Turns the focus potions off (Drink does nothing).")]
    private bool disableFocus;
    [SerializeField, Tooltip("One entry per skill. An empty list is filled with Lumberjack's Focus on start.")]
    private List<FocusPotion> potions = new List<FocusPotion>();
    [SerializeField, Tooltip("Colour of the status line while a potion is active.")]
    private Color statusColor = new Color(0.55f, 0.92f, 0.5f, 1f);

    private const string StatusKey = "focus";

    public static PixelFocus Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; PixelStatusLines.Clear(StatusKey); }

    // ---- progress: the running potion lives in the GameState (PixelGameState.Current.focus) ----
    private static FocusState S => PixelGameState.Current.focus;

    private int shownSecond = -1;
    private bool shownInPlay;

    public IReadOnlyList<FocusPotion> Potions => potions;
    public bool Active => S.Active;
    public FocusPotion ActivePotion => Active ? FindPotion(S.skillId, S.name) : null;
    public float Remaining => Active ? S.remaining : 0f;

    /// <summary>Is a focus potion for this skill active? (PixelSkills.TryGetHazard asks this for the skill in play.)</summary>
    public static bool HoldsOffHazards(string skillId) => S.Active && !string.IsNullOrEmpty(skillId) && S.skillId == skillId;

    /// <summary>Is a focus potion active for the skill in play right now? (a pre-rolled hazard is dropped while true)</summary>
    public static bool HoldsOffHazardsNow()
    {
        PixelSkills s = PixelSkills.Instance;
        return S.Active && s != null && S.skillId == s.ActiveId;
    }

    /// <summary>The potion of this skill with this name (or the skill's first one if no name matches), null if there is none.</summary>
    private FocusPotion FindPotion(string skillId, string name)
    {
        if (potions == null) return null;
        FocusPotion first = null;
        foreach (FocusPotion p in potions)
        {
            if (p == null || p.skillId != skillId) continue;
            if (p.displayName == name) return p;
            if (first == null) first = p;
        }
        return first;
    }

    private void Awake()
    {
        Instance = this;
        PixelGameState.Loaded += OnLoaded;
        if (potions == null) potions = new List<FocusPotion>();
        if (potions.Count == 0) potions.Add(new FocusPotion());   // Lumberjack's Focus
    }

    private void OnDestroy()
    {
        PixelGameState.Loaded -= OnLoaded;
        if (Instance == this) { Instance = null; PixelStatusLines.Clear(StatusKey); }
    }

    // A save was loaded: its running potion (if any) is in the state now - redraw the status line.
    private void OnLoaded()
    {
        shownSecond = -1;
        if (S.Active) RefreshStatus(); else PixelStatusLines.Clear(StatusKey);
    }

    /// <summary>Drinks the potion at this index of <see cref="Potions"/> (replaces the one that is active). False if there is no such potion or focus is switched off.</summary>
    /// <summary>Drinks the first focus potion of this skill (the item in the inventory does this). False if there is none or focus is switched off.</summary>
    public bool DrinkForSkill(string skillId)
    {
        if (potions == null) return false;
        for (int i = 0; i < potions.Count; i++) if (potions[i] != null && potions[i].skillId == skillId) return Drink(i);
        return false;
    }

    public bool Drink(int index)
    {
        if (disableFocus || potions == null || index < 0 || index >= potions.Count || potions[index] == null) return false;
        FocusPotion p = potions[index];
        S.skillId = p.skillId;
        S.name = p.displayName;
        S.remaining = Mathf.Max(1f, p.durationSeconds);
        shownSecond = -1;
        RefreshStatus();
        return true;
    }

    /// <summary>Stops the active potion early (no refund; the hazard can spawn again at once).</summary>
    public void Cancel()
    {
        S.Clear();   // (also when the timer just ran out: Active is already false then, the line still has to go)
        PixelStatusLines.Clear(StatusKey);
    }

    private void Update()
    {
        if (!S.Active) return;
        S.remaining -= Time.deltaTime;
        if (S.remaining <= 0f) { Cancel(); return; }
        RefreshStatus();
    }

    /// <summary>Redraws the status line when the shown second or the 'skill in play' state changes (no per-frame string work).</summary>
    private void RefreshStatus()
    {
        int sec = Mathf.CeilToInt(S.remaining);
        PixelSkills s = PixelSkills.Instance;
        bool inPlay = s != null && s.ActiveId == S.skillId;
        if (sec == shownSecond && inPlay == shownInPlay) return;
        shownSecond = sec;
        shownInPlay = inPlay;
        string text = S.name + " " + (sec / 60) + ":" + (sec % 60).ToString("00") + (inPlay ? " - hazards held off" : " - not in play");
        PixelStatusLines.Set(StatusKey, text, statusColor);
    }
}
