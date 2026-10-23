// ---- System notes ----
// PixelCalm.cs
//   The shared "what is going on right now" checks, so each script stops building its own long condition (a new blocker - a new
//   full-screen window, a new intro - is added HERE once):
//   - InMenu: the game is not in front of the player (pause menu, title screen or its reverse swing). Autosave screenshots, saving,
//     the View window, and every place that used to check "pause menu OR title screen" by hand (Block Hold, tooltips, keys, minigame starts, vendors, the snitch...).
//     Code that cares about only ONE of them (the pause menu itself, the title screen itself) still checks it directly.
//   - CameraBusy: something else owns the camera (title screen / its swing, the camera intro, a takeover minigame). Orbital / free cam / zoom.
//   - ForTip: a calm moment for a tip box or tutorial (in the game, no other tip or tutorial dim, Cubie not visiting or about to (a visit whose trigger is met: AboutToVisit, not the whole time before his second visit), the
//     new-game intro over, no takeover minigame, time running). Perk popups, the shop / skills / toggles tutorials.
//   The title / intro / pause part comes from PixelGameMode (the one place for the top-level mode; 2026-10-24).
//   Scripts with their own extra conditions keep them next to the shared check. WHO shows next when several are ready is PixelTipQueue's job
//   (tutorials > perk popups > first-time tips > Did you know): a new tip source registers there instead of showing a box by itself.
// ---------------------------------------------------
using UnityEngine;

/// <summary>Shared checks for "is the player in the game", "is the camera free" and "is this a calm moment for a tip".</summary>
public static class PixelCalm
{
    /// <summary>The pause menu, the title screen or its swing back is up: the game isn't in front of the player.</summary>
    public static bool InMenu => PixelGameMode.InMenu;

    /// <summary>Something else owns the camera: the title screen or its swing back, the camera intro, or a takeover minigame.</summary>
    public static bool CameraBusy => PixelGameMode.TitleOrIntro || PixelMinigame.TakeoverActive;

    /// <summary>A calm moment for a tip box or a tutorial: in the game, nothing else talking, Cubie not visiting, no takeover, time running.</summary>
    public static bool ForTip =>
        PixelGameMode.Playing && !PixelHints.IntroHoldsGuide && !PixelNotice.IsShowing && !PixelTutorialDim.Active &&
        !PixelGuideVendor.AboutToVisit && !PixelGuideVendor.Visiting && !PixelMinigame.TakeoverActive && Time.timeScale > 0f;
}
