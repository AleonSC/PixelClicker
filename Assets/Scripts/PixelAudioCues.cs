// ---- System notes ----
// PixelAudioCues.cs
//   Music CUES (part of partial class PixelAudio): the gameplay music changes at triggers and fades in / out. An Inspector list (Music Cues: name, trigger, param, priority, clip or playlist track number, volume, loop,
//   fade in / out seconds). Triggers: Minigame (param = the minigame id: ghost, bomb, blackhole, eviltree, ...; empty = any takeover minigame; true while it has something on screen), Skill (param = the skill id
//   in play), Night (the day / night cycle is in deep night), FirstPerson, UfoRide (riding the UFO), TitleScreen, Manual (code calls PixelAudio.SetMusicCue(name, on): the God Cube shows and the Dragon wish do).
//   Every 0.25 s the highest-priority cue whose condition is true wins (the later one on a tie); a cue with neither a clip nor a track number is ignored. While a cue plays the normal playlist fades to silence and is PAUSED
//   where it was (cueHoldsPlaylist) and fades back in, resuming, when no cue is wanted; a change from one cue to another fades the first out (its fade out), then the second in (its fade in). A cue that does not loop and
//   ends by itself hands back to the playlist and is not restarted until its condition has gone false once. GameStart = the song you choose to play right after the clicker loads in (give the built-in "After load" cue a clip; Loop off = it plays once, then the playlist carries on; the param may be a number of seconds to cap it). Built-in cues (addDefaultCues: After load, Night, Bomb, Black hole, Evil tree, Ghost hunt, UFO ride, God find, Dragon wish)
//   are added to the list by name when missing, WITHOUT clips: drop a clip (or a playlist track number) into the ones you want. disableCues turns the whole thing off. Mute / music volume apply to cues too.
// ----------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PixelAudio
{
    public enum CueTrigger { Minigame, Skill, Night, FirstPerson, UfoRide, TitleScreen, Manual, GameStart }

    [Serializable]
    public class MusicCue
    {
        [Tooltip("A name for this cue (what code uses for a Manual trigger).")]
        public string name = "Cue";
        [Tooltip("What makes the cue play.")]
        public CueTrigger trigger = CueTrigger.Manual;
        [Tooltip("GameStart: how many seconds the cue may last after the clicker loads in (0 = until its music ends; turn Loop off then). Minigame: the minigame id (ghost, bomb, blackhole, eviltree, meteor...; empty = any takeover minigame). Skill: the skill id in play (woodcutting, mining...). Not used by the others.")]
        public string param = "";
        [Tooltip("When several cues are wanted at once the highest priority plays.")]
        public int priority = 1;
        [Tooltip("The music to play. Empty + 'Track Number' -1 = this cue is switched off.")]
        public AudioClip clip;
        [Tooltip("Use a track of the Music Tracks list instead (0 = the first). -1 = use the clip above.")]
        public int trackIndex = -1;
        [Range(0f, 1f)]
        [Tooltip("Volume of this cue (multiplied by the music volume settings). 0 = the default (1): a cue added with the + button starts at 0, which would be silent.")]
        public float volume = 1f;
        [Tooltip("Repeat while the trigger lasts. Off = it plays once, then the normal playlist comes back.")]
        public bool loop = true;
        [Min(0f)]
        [Tooltip("Seconds the cue takes to fade in (the playlist fades out over the same time). 0 = the default (2).")]
        public float fadeInSeconds = 2f;
        [Min(0f)]
        [Tooltip("Seconds the cue takes to fade out when its trigger ends (the playlist fades back in over the same time). 0 = the default (2.5).")]
        public float fadeOutSeconds = 2.5f;
    }

    [Header("Music cues (the music changes at triggers)")]
    [Tooltip("Turn the music cues off entirely (the playlist just plays).")]
    [SerializeField] private bool disableCues = false;

    [Tooltip("The cues. Give a cue a clip (or a Music Tracks number) to make it work.")]
    [SerializeField] private List<MusicCue> musicCues = new List<MusicCue>();

    [Tooltip("Add the built-in cues that are missing from the list (they come without music: fill in the ones you want).")]
    [SerializeField] private bool addDefaultCues = true;

    private AudioSource cueSource;
    private int cueActive = -1, cueTarget = -1, cueSpent = -1;
    private float cueGain, playlistGain = 1f, cueEvalTimer, cueReturnFade = 2f;
    private bool cueHoldsPlaylist;
    private readonly HashSet<string> manualCues = new HashSet<string>();

    /// <summary>Switches a Manual music cue on or off by name (the God Cube shows, the Dragon wish...).</summary>
    public static void SetMusicCue(string name, bool on)
    {
        if (instance == null || string.IsNullOrEmpty(name)) return;
        if (on) instance.manualCues.Add(name); else instance.manualCues.Remove(name);
    }

    private static MusicCue Cue(string name, CueTrigger trigger, string param, int priority, float fadeIn = 2f, float fadeOut = 2.5f, bool loop = true)
    {
        return new MusicCue { name = name, trigger = trigger, param = param, priority = priority, fadeInSeconds = fadeIn, fadeOutSeconds = fadeOut, loop = loop };
    }

    private void EnsureDefaultCues()
    {
        if (!addDefaultCues) return;
        if (musicCues == null) musicCues = new List<MusicCue>();
        MusicCue[] defaults =
        {
            Cue("Night", CueTrigger.Night, "", 2, 4f, 4f),
            Cue("Ghost hunt", CueTrigger.Minigame, "ghost", 5),
            Cue("Bomb", CueTrigger.Minigame, "bomb", 6, 0.8f, 1.5f),
            Cue("Black hole", CueTrigger.Minigame, "blackhole", 5),
            Cue("Evil tree", CueTrigger.Minigame, "eviltree", 5),
            Cue("UFO ride", CueTrigger.UfoRide, "", 3),
            Cue("God find", CueTrigger.Manual, "", 10, 0.6f, 2f),
            Cue("Dragon wish", CueTrigger.Manual, "", 10, 0.6f, 2f),
            Cue("After load", CueTrigger.GameStart, "", 1, 3f, 3f, false),
        };
        foreach (MusicCue d in defaults)
            if (!musicCues.Exists(c => c != null && c.name == d.name)) musicCues.Add(d);
    }

    private static float CueVolume(MusicCue c) => c.volume > 0f ? c.volume : 1f;   // (an element added in the Inspector starts with every number at 0)
    private static float CueFadeIn(MusicCue c) => c.fadeInSeconds > 0f ? c.fadeInSeconds : 2f;
    private static float CueFadeOut(MusicCue c) => c.fadeOutSeconds > 0f ? c.fadeOutSeconds : 2.5f;

    private AudioClip CueClip(MusicCue c)
    {
        if (c == null) return null;
        if (c.clip != null) return c.clip;
        return musicTracks != null && c.trackIndex >= 0 && c.trackIndex < musicTracks.Length ? musicTracks[c.trackIndex] : null;
    }

    private bool CueCondition(MusicCue c)
    {
        switch (c.trigger)
        {
            case CueTrigger.Minigame:
                if (string.IsNullOrEmpty(c.param)) return PixelMinigame.TakeoverActive;
                PixelMinigame m = PixelMinigame.Find(c.param);
                return m != null && m.Running && m.Busy;
            case CueTrigger.Skill: return PixelSkills.Instance != null && PixelSkills.Instance.ActiveId == c.param;
            case CueTrigger.Night: return PixelDayNight.Enabled && PixelDayNight.Level < 0.25f;
            case CueTrigger.FirstPerson: return PixelFirstPerson.Active;
            case CueTrigger.UfoRide: return PixelRobotWorker.Piloting;
            case CueTrigger.TitleScreen: return PixelTitleScreen.Showing;
            case CueTrigger.Manual: return manualCues.Contains(c.name);
            case CueTrigger.GameStart:
            {
                // True once the title screen has closed (the clicker loaded in); for 'param' seconds when that is a number, else until its music ends.
                if (PixelTitleScreen.Showing) return false;
                float span = float.TryParse(c.param, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 0f;
                return span <= 0f || Time.unscaledTime - gameLoadedAt < span;
            }
        }
        return false;
    }

    /// <summary>The cue that should play right now (-1 = none).</summary>
    private int EvaluateCues()
    {
        if (disableCues || !playMusic || musicCues == null) return -1;
        int best = -1;
        for (int i = 0; i < musicCues.Count; i++)
        {
            MusicCue c = musicCues[i];
            if (c == null || CueClip(c) == null) continue;
            bool on = CueCondition(c);
            if (i == cueSpent) { if (!on) cueSpent = -1; continue; }   // a one-shot cue that already played waits for its trigger to end before it can play again
            if (!on) continue;
            if (best < 0 || c.priority >= musicCues[best].priority) best = i;
        }
        return best;
    }

    private bool titleWasShowing = true;
    private float gameLoadedAt;

    private void UpdateCues()
    {
        float dt = Time.unscaledDeltaTime;
        bool titleNow = PixelTitleScreen.Showing;
        if (titleWasShowing && !titleNow) gameLoadedAt = Time.unscaledTime;   // the clicker just loaded in (the GameStart cue's clock)
        titleWasShowing = titleNow;
        cueEvalTimer -= dt;
        if (cueEvalTimer <= 0f) { cueEvalTimer = 0.25f; cueTarget = EvaluateCues(); }
        if (cueActive < 0 && cueTarget < 0 && playlistGain >= 1f && !cueHoldsPlaylist) return;   // nothing going on

        if (cueSource == null)
        {
            GameObject go = new GameObject("Music Cue");
            go.transform.SetParent(transform, false);
            cueSource = go.AddComponent<AudioSource>();
            cueSource.playOnAwake = false;
        }

        if (cueActive >= 0 && (cueActive >= musicCues.Count || cueTarget != cueActive))
        {
            // The cue's trigger ended (or another cue is wanted): fade this one out.
            float f = Mathf.Max(0.05f, cueActive < musicCues.Count ? CueFadeOut(musicCues[cueActive]) : 0.5f);
            cueGain = Mathf.MoveTowards(cueGain, 0f, dt / f);
            if (cueGain <= 0f) { cueSource.Stop(); cueReturnFade = f; cueActive = -1; }
        }
        else if (cueActive < 0 && cueTarget >= 0)
        {
            MusicCue c = musicCues[cueTarget];
            AudioClip clip = CueClip(c);
            if (clip != null)
            {
                cueSource.clip = clip; cueSource.loop = c.loop; cueSource.time = 0f; cueSource.Play();
                cueActive = cueTarget; cueGain = 0f;
            }
        }
        if (cueActive >= 0 && cueTarget == cueActive && cueActive < musicCues.Count)
        {
            cueGain = Mathf.MoveTowards(cueGain, 1f, dt / Mathf.Max(0.05f, CueFadeIn(musicCues[cueActive])));
            // A one-shot cue that has played to its end hands back to the playlist.
            if (!cueSource.loop && !cueSource.isPlaying && Application.isFocused && !AudioListener.pause && cueGain > 0.5f) { cueSpent = cueActive; cueTarget = -1; }
        }

        // The playlist: quiet (and paused where it was) while a cue is wanted or playing, back in when none is.
        bool wantPlaylist = cueActive < 0 && cueTarget < 0;
        float step = dt / Mathf.Max(0.05f, wantPlaylist ? cueReturnFade : (cueActive >= 0 && cueActive < musicCues.Count ? CueFadeIn(musicCues[cueActive]) : 1f));
        playlistGain = Mathf.MoveTowards(playlistGain, wantPlaylist ? 1f : 0f, step);
        if (!wantPlaylist && playlistGain <= 0f && !cueHoldsPlaylist && musicSource != null && musicSource.isPlaying) { musicSource.Pause(); cueHoldsPlaylist = true; }
        if (wantPlaylist && cueHoldsPlaylist && musicSource != null) { musicSource.UnPause(); cueHoldsPlaylist = false; }

        float baseVolume = muted || musicMuted || previewTrack >= 0 ? 0f : musicVolume * masterVolume * musicTrackVolume;
        cueSource.volume = baseVolume * (cueActive >= 0 && cueActive < musicCues.Count ? CueVolume(musicCues[cueActive]) : 0f) * cueGain;
    }
}
