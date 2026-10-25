// ---- System notes ----
// PixelGameMode.cs
//   ONE place that says what the game is doing at the top level, so scripts stop combining PixelTitleScreen.Showing / Reversing,
//   PixelPauseMenu.IsPaused and PixelCameraIntro.Moving by hand (2026-10-24, to-do 'Central game mode'):
//     Title   - the title screen is up, or the camera swings back to it (Quit > Main menu: PixelTitleScreen.Reversing)
//     Intro   - the opening camera move into the game runs (PixelCameraIntro.Moving; the cube may not be clickable yet)
//     Paused  - the pause menu is open (PixelPauseMenu.IsPaused; whether it also froze time is PixelPauseMenu.GameStopped)
//     Playing - none of these: the game is in front of the player
//   (Title wins over the others - the camera intro's swing back to the title counts as Title - and Paused wins over Intro.)
//   The flags stay where they are (their owners set them); their setters call Refresh(), which works the mode out again and fires Changed
//   (old mode, new mode) when it moved - subscribe instead of checking every frame. Takeover minigames (PixelMinigame.TakeoverActive) are
//   NOT a mode: they run inside Playing. PixelCalm.InMenu / CameraBusy are built on this.
//   Use: PixelGameMode.Current, OnTitle, TitleOrIntro (the game hasn't started for the player yet), InMenu (Title or Paused), Playing.
//   Code that cares about exactly one screen's own flag (the pause menu itself, the title screen itself) can still read it directly.
// ---------------------------------------------------
using System;
using UnityEngine;

public static class PixelGameMode
{
    public enum Mode { Title, Intro, Paused, Playing }

    private static Mode current = Mode.Playing;
    private static bool known;

    /// <summary>Fired when the mode changes (old mode, new mode).</summary>
    public static event Action<Mode, Mode> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = Mode.Playing;
        known = false;
        Changed = null;
    }

    /// <summary>What the game is doing right now.</summary>
    public static Mode Current
    {
        get
        {
            if (!known) { current = Work(); known = true; }
            return current;
        }
    }

    /// <summary>The title screen is up or the camera swings back to it.</summary>
    public static bool OnTitle => Current == Mode.Title;

    /// <summary>The game hasn't started for the player yet: the title screen (or the swing back to it) or the opening camera move.</summary>
    public static bool TitleOrIntro => Current == Mode.Title || PixelCameraIntro.Moving;   // (the move counts even while the pause menu is over it)

    /// <summary>The game isn't in front of the player: the title screen (or the swing back to it) or the pause menu.</summary>
    public static bool InMenu => Current == Mode.Title || Current == Mode.Paused;

    /// <summary>Nothing in the way: no title, no opening camera move, no pause menu.</summary>
    public static bool Playing => Current == Mode.Playing;

    // The owners' raw flags, read through here when code needs exactly one of them (not the combined mode above), so every read of
    // them sits behind this one class (the Code Health 'one-place' check). Prefer InMenu / OnTitle / TitleOrIntro where they mean the same.
    /// <summary>Exactly PixelPauseMenu.IsPaused: the pause menu is open (whatever else is going on; Current == Paused is false while Title wins).</summary>
    public static bool PauseMenuOpen => PixelPauseMenu.IsPaused;
    /// <summary>Exactly PixelTitleScreen.Showing: the title screen itself is up (not the swing back to it).</summary>
    public static bool TitleShowing => PixelTitleScreen.Showing;
    /// <summary>Exactly PixelCameraIntro.Moving: the opening camera move runs.</summary>
    public static bool IntroMoving => PixelCameraIntro.Moving;

    /// <summary>Works the mode out again from the owners' flags; fires Changed if it moved. The flags' setters call this.</summary>
    public static void Refresh()
    {
        Mode before = known ? current : Work();
        current = Work();
        known = true;
        if (current == before) return;
        Action<Mode, Mode> handlers = Changed;
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action<Mode, Mode>)d)(before, current); }
            catch (Exception e) { Debug.LogException(e); }   // (one broken listener must not stop the others)
        }
    }

    private static Mode Work()
    {
        if (PixelTitleScreen.Showing || PixelTitleScreen.Reversing) return Mode.Title;
        if (PixelPauseMenu.IsPaused) return Mode.Paused;
        if (PixelCameraIntro.Moving) return Mode.Intro;
        return Mode.Playing;
    }
}
