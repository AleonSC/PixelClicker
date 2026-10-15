// ---- System notes ----
// PixelStatics.cs
//   One registry for "clear this static game state when the scene is reloaded". Statics outlive Settings > Restart (it reloads the scene), so
//   game state kept in a static (a status line, a held penalty, a takeover flag...) carried into the new game - the old 'Oak 3 / 9' tree text
//   came back after a Restart that way. A class with static GAME state registers its reset once, from its [RuntimeInitializeOnLoadMethod]
//   ResetStatics:  PixelStatics.ResetOnRestart(OnRestart);  and puts the clearing in that method. Registering the same method twice is ignored.
//   Keep DEV switches (Dev Tools tick boxes) out of the restart reset; they are meant to survive it. References to the new scene's objects are
//   re-found by the new scene's Awake, so clear only state, not something the new scene may already have set.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public static class PixelStatics
{
    private static readonly List<Action> resets = new List<Action>();

    /// <summary>Runs 'reset' every time a scene is unloaded (Settings > Restart, returning to a reloaded scene). Safe to call more than once.</summary>
    public static void ResetOnRestart(Action reset)
    {
        if (reset == null) return;
        if (!resets.Contains(reset)) resets.Add(reset);   // (delegates to the same static method compare equal)
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        for (int i = 0; i < resets.Count; i++)
        {
            try { resets[i](); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }   // (one bad reset must not stop the others)
        }
    }
}
