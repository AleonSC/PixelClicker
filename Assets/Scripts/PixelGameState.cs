// ---- System notes ----
// PixelGameState.cs
//   The OWNER of the player's progress (GameState.cs; docs/systems/PixelGameState.md).
//   - PixelGameState.Current is the one GameState. It is never null: it is made on first use, so a system that starts early never waits for
//     a save or for this component.
//   - SAVE: PixelSaveGame calls RaiseSaving() first (the Saving event: WORLD objects - placed devices, the running potion - copy themselves into
//     their sub-state there, instead of every frame), then writes Current into the save file whole (SaveData.state).
//   - LOAD: PixelSaveGame calls Replace(the file's copy) at the start of its load and RaiseLoaded() once the tiers, shop, husks and the IPixelSaved
//     parts are back but BEFORE the Dev Hub tester machines (2026-10-24: a Solar Panel links to a placed device and belts dock on devices' couplings,
//     so world objects rebuilt in Loaded must exist before the machines come back). Views refresh / world objects rebuild there.
//   - NEW GAME / RESTART: both reload the scene and call NewGame() right before it (PixelPauseMenu Restart, PixelTitleScreen New Game - the
//     only two scene loads), so the new scene starts with a fresh 'new GameState()'. Moved systems need no reset of their own. (Not done with
//     PixelStatics.ResetOnRestart: sceneUnloaded can come after the new scene has started, which would wipe the new game's first moments.)
//   - INSPECTOR: this component (in PixelSystems.All, Core) shows Current under 'State' while playing, so values can be read and typed to test.
//     It adopts an edited copy if the Inspector swaps the object.
// ---------------------------------------------------
using System;
using UnityEngine;

public class PixelGameState : MonoBehaviour
{
    [Tooltip("The player's progress while playing (the same object the game uses: type values here to test). Moved systems: GameState.MovedSystems. Edit-mode values are not used - a game always starts from a fresh state or a save.")]
    [SerializeField] private GameState state = new GameState();

    private static GameState current;
    private static GameState shown;   // the object this component last showed (to tell an Inspector swap from a load)

    /// <summary>Fired after a save's state was put in place (end of the load). Views refresh here (the bank's selection...).</summary>
    public static event Action Loaded;

    /// <summary>Fired right before a save is written: world objects (placed devices, the running potion) copy themselves into their sub-state here.</summary>
    public static event Action Saving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        shown = null;
        Loaded = null;
        Saving = null;
    }

    /// <summary>A fresh game: drops the current progress. Call it right before reloading the scene for Restart / New Game.</summary>
    public static void NewGame()
    {
        current = new GameState();
        shown = null;
    }

    /// <summary>The player's progress. Read sub-states through this every time (a load swaps the whole object).</summary>
    public static GameState Current
    {
        get
        {
            if (current == null) current = new GameState();
            return current;
        }
    }

    /// <summary>Puts a loaded state in place (null = a save without one: a fresh state). PixelSaveGame calls this at the start of a load.</summary>
    public static void Replace(GameState loaded)
    {
        current = loaded ?? new GameState();
        current.Repair();
    }

    /// <summary>Tells world objects to copy themselves into the state (PixelSaveGame calls it right before writing a save).</summary>
    public static void RaiseSaving() => Raise(Saving);

    /// <summary>Tells the views a load finished (PixelSaveGame calls it once everything but the tester machines is back).</summary>
    public static void RaiseLoaded() => Raise(Loaded);

    private static void Raise(Action handlers)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action)d)(); }
            catch (Exception e) { Debug.LogException(e); }   // (one broken view must not stop the others)
        }
    }

    private void Awake() { state = Current; shown = current; }   // (the scene's edit-mode copy is never used)

    private void LateUpdate() => Show();

    /// <summary>Keeps the Inspector field and Current the same object: a load replaced Current -> show it; the Inspector replaced the field -> use it.</summary>
    private void Show()
    {
        if (current != shown) { state = Current; shown = current; return; }
        if (state != null && state != current) { current = state; current.Repair(); shown = current; }
    }
}
