// ---- System notes (moved here from CLAUDE.md) ----
// PixelAudio.cs (UFO beam)
//   The main-menu UFO's abduction plays ufo_beam (code-made PixelSynth.UfoBeam: a rising wobbling hum with a shimmer and a pop at the
//   end, ~2.6 s; stopped if the abduction is aborted).
//   Part files: PixelAudioCues.cs, PixelAudioSounds.cs (the built-in sound table + which synth clips fill it); the code-made synth PixelSynth
//   (static partial class) is in PixelAudioSynth.cs and PixelAudioSynthShows.cs.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's sound system. It owns every sound effect (a named list: drop clips into the Inspector), the music, the
/// volume settings (master / effects / music / mute, saved in PlayerPrefs and changed in the pause menu's Settings) and
/// the playback itself (a small pool of voices, a minimum gap between repeats of one sound, a random pitch and clip
/// variation so repeated sounds don't tire).
///
/// Most sounds play by themselves: this component listens to the game's existing events (clicks, purchases, potions,
/// devices, minigames, combo, crafting, achievements, buttons). Other scripts can also call
/// <c>PixelAudio.Play("id")</c>. A sound with no clips (or an unknown id) is simply silent, so you can fill the list in
/// one sound at a time.
///
/// Add this to any GameObject. The pause menu adds one automatically if the scene has none.
/// </summary>
public partial class PixelAudio : MonoBehaviour
{
    /// <summary>One named sound effect.</summary>
    [Serializable]
    public class Sound
    {
        [Tooltip("The sound's id. The game plays sounds by id (see the tooltip of 'Sounds' for the list). Don't rename the built-in ones.")]
        public string id = "sound";

        [Tooltip("The clips. If you add several, one is picked at random each time (never the same one twice in a row).")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        [Tooltip("Volume of this sound.")]
        public float volume = 1f;

        [Range(0.5f, 2f)]
        [Tooltip("Lowest random pitch (1 = normal).")]
        public float pitchMin = 0.97f;

        [Range(0.5f, 2f)]
        [Tooltip("Highest random pitch (1 = normal).")]
        public float pitchMax = 1.03f;

        [Min(0f)]
        [Tooltip("Shortest time (seconds) between two plays of this sound. Stops fast clicking or the auto clicker from machine-gunning it.")]
        public float cooldown = 0.03f;

        [NonSerialized] public float lastPlayed = -999f;
        [NonSerialized] public int lastClip = -1;
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker whose events trigger sounds. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Volumes (the player's choices are remembered; these are the first-run values)")]
    [Range(0f, 1f)]
    [Tooltip("Overall volume.")]
    [SerializeField] private float masterVolume = 1f;

    [Range(0f, 1f)]
    [Tooltip("Volume of sound effects.")]
    [SerializeField] private float effectsVolume = 1f;

    [Range(0f, 1f)]
    [Tooltip("Volume of the music.")]
    [SerializeField] private float musicVolume = 0.6f;

    [Tooltip("Start muted.")]
    [SerializeField] private bool muted = false;

    [Header("Sound Effects")]
    [Tooltip("Every sound effect, by id. Built-in ids: click, auto_click, pixel_land (a husk hits the floor), hit, vacuum, combo, purchase, craft, drink, " +
             "device_place, achievement, ui_click, ui_scroll, grab, drop, ghost_spawn, ghost_click, meteor_spawn, meteor_click, blackhole_spawn, pad_spawn, pad_click, pad_wrong, pad_ultra, bomb_spawn, bomb_click, bomb_tick, bomb_explode, " +
             "and pixel_<type> (e.g. pixel_vacuum, pixel_meteor) which plays when that pixel type is collected. " +
             "Leave 'Clips' empty for silence.")]
    [SerializeField] private List<Sound> sounds = CreateDefaultSounds();

    [Tooltip("Add any built-in sound that is missing from the list (so new ones show up in the Inspector).")]
    [SerializeField] private bool addDefaultSounds = true;

    [Min(1)]
    [Tooltip("How many sound effects can play at the same time. When all are busy the oldest is cut off.")]
    [SerializeField] private int maxVoices = 12;

    [Header("Combo Sound")]
    [Min(0f)]
    [Tooltip("The 'combo' sound goes up in pitch with the combo: this much (as a pitch multiplier) per combo step.")]
    [SerializeField] private float comboPitchStep = 0.04f;

    [Range(1f, 3f)]
    [Tooltip("The highest pitch multiplier the combo sound can reach.")]
    [SerializeField] private float comboPitchMax = 2f;

    [Header("Music")]
    [Tooltip("Music tracks. They play one after another (in random order if 'Shuffle Music' is on). Empty = no music.")]
    [SerializeField] private AudioClip[] musicTracks;

    [Tooltip("Play music.")]
    [SerializeField] private bool playMusic = true;

    [Tooltip("Pick the next track at random.")]
    [SerializeField] private bool shuffleMusic = true;

    [Range(0f, 1f)]
    [Tooltip("Volume of the music tracks (multiplied by the Music volume setting).")]
    [SerializeField] private float musicTrackVolume = 1f;

    [Serializable]
    public class TrackTempo
    {
        [Tooltip("The music clip (a Music Track or a music cue's clip).")]
        public AudioClip clip;
        [Min(1f)]
        [Tooltip("The song's tempo in beats per minute.")]
        public float bpm = 90f;
        [Min(0f)]
        [Tooltip("Seconds into the song where the first beat lands (any silence or pickup before it).")]
        public float firstBeatSeconds = 0f;
    }

    [Tooltip("Tempo of songs that are not in 'Music Tempos' (beats per minute). Things that move to the music (the main menu cube) use it.")]
    [SerializeField] private float defaultMusicBpm = 90f;

    [Tooltip("Each song's tempo and where its first beat lands, so things can move in time with it (the main menu cube changes on the beat). Songs not listed use 'Default Music Bpm' with the first beat at 0 s.")]
    [SerializeField] private List<TrackTempo> musicTempos = new List<TrackTempo>();

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private const string PrefMaster = "PixelAudio.Master";
    private const string PrefEffects = "PixelAudio.Effects";
    private const string PrefMusic = "PixelAudio.Music";
    private const string PrefMuted = "PixelAudio.Muted";
    // Per save (PixelSettingsSync): music mute and the playlist. (Named PixelClicker.Setting.* so a new game resets them; PixelAudio.* keys are the player's own and stay.)
    private const string PrefMusicMuted = "PixelClicker.Setting.MusicMuted";
    private const string PrefMusicShuffle = "PixelClicker.Setting.MusicShuffle";   // 1 = shuffle, 0 = in order; missing = the Inspector's 'Shuffle Music'
    private bool musicMuted;
    private bool shuffleOn = true;

    private float defaultMaster, defaultEffects, defaultMusic;
    private bool defaultMuted;

    /// <summary>A save file with its own settings was loaded: read the volumes again (missing ones go back to the Inspector values).</summary>
    private const string PrefUiClicks = "PixelAudio.UiClicks";
    private static int uiClicksCache = -1;

    /// <summary>Settings > Audio > 'UI click sounds': the click sound of every UI button / tick box (sound ids ui_click, ui_scroll and menu_open: the click, the scroll tick and the menu bloop). On by default.</summary>
    public static bool UiClicks
    {
        get { if (uiClicksCache < 0) uiClicksCache = PixelPrefs.GetInt(PrefUiClicks, 1); return uiClicksCache != 0; }
        set { uiClicksCache = value ? 1 : 0; PixelPrefs.SetInt(PrefUiClicks, uiClicksCache); PixelPrefs.Save(); }
    }

    public static void ReloadFromPrefs()
    {
        uiClicksCache = -1;
        if (instance == null) return;
        instance.masterVolume = PixelPrefs.HasKey(PrefMaster) ? PixelPrefs.GetFloat(PrefMaster) : instance.defaultMaster;
        instance.effectsVolume = PixelPrefs.HasKey(PrefEffects) ? PixelPrefs.GetFloat(PrefEffects) : instance.defaultEffects;
        instance.musicVolume = PixelPrefs.HasKey(PrefMusic) ? PixelPrefs.GetFloat(PrefMusic) : instance.defaultMusic;
        instance.muted = PixelPrefs.HasKey(PrefMuted) ? PixelPrefs.GetInt(PrefMuted) != 0 : instance.defaultMuted;
        instance.musicMuted = PixelPrefs.GetInt(PrefMusicMuted, 0) != 0;
        instance.shuffleOn = PixelPrefs.GetInt(PrefMusicShuffle, instance.shuffleMusic ? 1 : 0) != 0;
        instance.playlistOff = null;   // read again (per save)
        instance.ApplyMusicVolume();
    }

    private static PixelAudio instance;
    private readonly Dictionary<string, Sound> byId = new Dictionary<string, Sound>();
    private AudioSource[] voices;
    private int nextVoice;
    private AudioSource musicSource;
    private int lastTrack = -1;

    private PixelShop shop;
    private PixelAchievements achievements;

    /// <summary>The sound system (null if the scene has none).</summary>
    public static PixelAudio Instance => instance;

    /// <summary>Overall volume 0..1 (saved).</summary>
    public float MasterVolume
    {
        get => masterVolume;
        set { masterVolume = Mathf.Clamp01(value); PixelPrefs.SetFloat(PrefMaster, masterVolume); ApplyMusicVolume(); }
    }

    /// <summary>Sound effect volume 0..1 (saved).</summary>
    public float EffectsVolume
    {
        get => effectsVolume;
        set { effectsVolume = Mathf.Clamp01(value); PixelPrefs.SetFloat(PrefEffects, effectsVolume); }
    }

    /// <summary>Music volume 0..1 (saved).</summary>
    public float MusicVolume
    {
        get => musicVolume;
        set { musicVolume = Mathf.Clamp01(value); PixelPrefs.SetFloat(PrefMusic, musicVolume); ApplyMusicVolume(); }
    }

    /// <summary>All sound off (saved).</summary>
    public bool Muted
    {
        get => muted;
        set { muted = value; PixelPrefs.SetInt(PrefMuted, muted ? 1 : 0); ApplyMusicVolume(); }
    }

    /// <summary>The music alone off (saved per save file; the speaker button on the main menu and Settings > Audio).</summary>
    /// <summary>Is the next track picked at random (the music players' shuffle button)? Saved per save file; the default is the Inspector's 'Shuffle Music'.</summary>
    public bool MusicShuffle
    {
        get => shuffleOn;
        set { shuffleOn = value; PixelPrefs.SetInt(PrefMusicShuffle, value ? 1 : 0); }
    }

    public bool MusicMuted
    {
        get => musicMuted;
        set { musicMuted = value; PixelPrefs.SetInt(PrefMusicMuted, musicMuted ? 1 : 0); ApplyMusicVolume(); }
    }

    /// <summary>Every sound effect (the Inspector list): the dev Sound board lists them and changes their volume live.</summary>
    public static IReadOnlyList<Sound> AllSounds => instance != null && instance.sounds != null ? instance.sounds : (IReadOnlyList<Sound>)Array.Empty<Sound>();

    /// <summary>True if a sound with this id exists and has at least one clip.</summary>
    public static bool Has(string id)
    {
        return instance != null && !string.IsNullOrEmpty(id) && instance.byId.TryGetValue(id, out Sound s)
               && s.clips != null && s.clips.Length > 0 && s.clips[0] != null;
    }

    /// <summary>Plays a sound by id with its volume scaled (e.g. by how hard something hit).</summary>
    public static void PlayScaled(string id, float volumeMultiplier)
    {
        if (instance != null) instance.PlaySound(id, 1f, volumeMultiplier);
    }

    private bool EnsureDefaultSounds()
    {
        if (!addDefaultSounds) return false;
        if (sounds == null) sounds = new List<Sound>();
        bool changed = false;
        foreach (Sound d in CreateDefaultSounds())
        {
            if (sounds.Exists(s => s != null && s.id == d.id)) continue;
            sounds.Add(d);
            changed = true;
        }
        return changed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxVoices = Mathf.Max(1, maxVoices);
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultSounds()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("PixelAudio: more than one in the scene; remove the extra one.", this);
            return;
        }
        instance = this;

        defaultMaster = masterVolume; defaultEffects = effectsVolume; defaultMusic = musicVolume; defaultMuted = muted;

        // The player's saved choices win over the Inspector's first-run values.
        if (PixelPrefs.HasKey(PrefMaster)) masterVolume = PixelPrefs.GetFloat(PrefMaster);
        if (PixelPrefs.HasKey(PrefEffects)) effectsVolume = PixelPrefs.GetFloat(PrefEffects);
        if (PixelPrefs.HasKey(PrefMusic)) musicVolume = PixelPrefs.GetFloat(PrefMusic);
        if (PixelPrefs.HasKey(PrefMuted)) muted = PixelPrefs.GetInt(PrefMuted) != 0;
        shuffleOn = PixelPrefs.GetInt(PrefMusicShuffle, shuffleMusic ? 1 : 0) != 0;

        EnsureDefaultSounds();
        FillSynthesizedClips();
        EnsureDefaultCues();
        RebuildLookup();

        voices = new AudioSource[Mathf.Max(1, maxVoices)];
        for (int i = 0; i < voices.Length; i++)
        {
            GameObject go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);
            voices[i] = go.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }

        GameObject musicGo = new GameObject("Music");
        musicGo.transform.SetParent(transform, false);
        musicSource = musicGo.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = false;
        ApplyMusicVolume();
    }

    private void Start()
    {
        if (instance != this) return;

        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker != null)
        {
            clicker.PixelCollected += OnCollected;
            clicker.PixelHit += OnHit;
            clicker.PixelsVacuumed += OnVacuumed;
        }
        PixelClicker.OldPixelLanded += OnLanded;

        achievements = PixelFind.First<PixelAchievements>();
        if (achievements != null) achievements.onAchievementUnlocked.AddListener(OnAchievement);

        shop = PixelFind.First<PixelShop>();
        if (shop != null) shop.onPackPurchased.AddListener(OnPurchase);

        PixelConsumables.PotionDrunk += OnPotionDrunk;
        PixelConsumables.DevicePlaced += OnDevicePlaced;
        PixelMinigame.Happened += OnMinigame;
        PixelCombo.ComboReached += OnCombo;
        PixelCrafting.Crafted += OnCrafted;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;

        if (clicker != null)
        {
            clicker.PixelCollected -= OnCollected;
            clicker.PixelHit -= OnHit;
            clicker.PixelsVacuumed -= OnVacuumed;
        }
        if (achievements != null) achievements.onAchievementUnlocked.RemoveListener(OnAchievement);
        if (shop != null) shop.onPackPurchased.RemoveListener(OnPurchase);

        PixelClicker.OldPixelLanded -= OnLanded;
        PixelConsumables.PotionDrunk -= OnPotionDrunk;
        PixelConsumables.DevicePlaced -= OnDevicePlaced;
        PixelMinigame.Happened -= OnMinigame;
        PixelCombo.ComboReached -= OnCombo;
        PixelCrafting.Crafted -= OnCrafted;
    }

    private void Update()
    {
        UpdateMusic();
        UpdateLoops();
    }

    // ------------------------------------------------------------------
    // Looping sounds (a hum that runs while something is active)
    // ------------------------------------------------------------------

    private class LoopVoice
    {
        public AudioSource source;
        public float level;      // 0..1 fade
        public bool wanted;
    }

    private readonly Dictionary<string, LoopVoice> loops = new Dictionary<string, LoopVoice>();

    [Min(0f)]
    [Tooltip("Seconds a looping sound (e.g. the Time Stop hum) takes to fade in and out.")]
    [SerializeField] private float loopFadeSeconds = 0.25f;

    /// <summary>Starts a looping sound by id (uses the first clip of that sound, loops until StopLoop). Silent if it has no clip.</summary>
    public static void StartLoop(string id)
    {
        if (instance != null) instance.SetLoop(id, true);
    }

    /// <summary>Fades out and stops a sound started with StartLoop.</summary>
    public static void StopLoop(string id)
    {
        if (instance != null) instance.SetLoop(id, false);
    }

    private void SetLoop(string id, bool on)
    {
        if (!loops.TryGetValue(id, out LoopVoice v))
        {
            if (!on) return;
            if (!byId.TryGetValue(id, out Sound s) || s.clips == null || s.clips.Length == 0 || s.clips[0] == null) return;
            GameObject go = new GameObject("Loop " + id);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.clip = s.clips[0];
            src.volume = 0f;
            v = new LoopVoice { source = src };
            loops[id] = v;
        }
        v.wanted = on;
        if (on && !v.source.isPlaying) v.source.Play();
    }

    private void UpdateLoops()
    {
        foreach (KeyValuePair<string, LoopVoice> kv in loops)
        {
            LoopVoice v = kv.Value;
            float step = loopFadeSeconds > 0f ? Time.unscaledDeltaTime / loopFadeSeconds : 1f;
            v.level = Mathf.MoveTowards(v.level, v.wanted ? 1f : 0f, step);
            float vol = byId.TryGetValue(kv.Key, out Sound s) ? s.volume : 1f;
            v.source.volume = muted ? 0f : vol * effectsVolume * masterVolume * v.level;
            if (!v.wanted && v.level <= 0f && v.source.isPlaying) v.source.Stop();
        }
    }

    private void RebuildLookup()
    {
        byId.Clear();
        foreach (Sound s in sounds)
            if (s != null && !string.IsNullOrEmpty(s.id)) byId[s.id] = s;
    }

    // ------------------------------------------------------------------
    // Playing
    // ------------------------------------------------------------------

    /// <summary>Plays a sound by id (silent if it doesn't exist or has no clips).</summary>
    public static void Play(string id)
    {
        if (instance != null) instance.PlaySound(id, 1f);
    }

    /// <summary>Plays a sound by id with its pitch multiplied (e.g. 1.5 = a fifth higher).</summary>
    public static void Play(string id, float pitchMultiplier)
    {
        if (instance != null) instance.PlaySound(id, pitchMultiplier);
    }

    /// <summary>Plays a sound by id with its pitch and volume multiplied (a very quiet tick = Play("ui_click", 1.7f, 0.25f)).</summary>
    public static void Play(string id, float pitchMultiplier, float volumeMultiplier)
    {
        if (instance != null) instance.PlaySound(id, pitchMultiplier, volumeMultiplier);
    }

    /// <summary>Cuts off every voice that is currently playing this sound (e.g. the Time Stop sound when time resumes).</summary>
    public static void Stop(string id)
    {
        if (instance == null || instance.voices == null) return;
        for (int i = 0; i < instance.voices.Length; i++)
            if (instance.voiceIds[i] == id && instance.voices[i] != null && instance.voices[i].isPlaying) instance.voices[i].Stop();
    }

    private string[] voiceIds;

    private void PlaySound(string id, float pitchMultiplier, float volumeMultiplier = 1f)
    {
        if (muted || voices == null || !byId.TryGetValue(id, out Sound s)) return;
        if (!UiClicks && (id == "ui_click" || id == "menu_open" || id == "ui_scroll")) return;   // the UI click sounds can be switched off in Settings > Audio
        if (s.clips == null || s.clips.Length == 0) return;

        float now = Time.unscaledTime;
        if (now - s.lastPlayed < s.cooldown) return;
        s.lastPlayed = now;

        // A random clip, never the same one twice in a row.
        int pick = UnityEngine.Random.Range(0, s.clips.Length);
        if (s.clips.Length > 1 && pick == s.lastClip) pick = (pick + 1) % s.clips.Length;
        s.lastClip = pick;
        AudioClip clip = s.clips[pick];
        if (clip == null) return;

        if (voiceIds == null || voiceIds.Length != voices.Length) voiceIds = new string[voices.Length];
        AudioSource voice = voices[nextVoice];
        voiceIds[nextVoice] = id;
        nextVoice = (nextVoice + 1) % voices.Length;
        voice.clip = clip;
        voice.volume = s.volume * effectsVolume * masterVolume * volumeMultiplier;
        voice.pitch = Mathf.Clamp(UnityEngine.Random.Range(s.pitchMin, s.pitchMax) * pitchMultiplier, 0.1f, 4f);
        voice.Play();
    }

    // ------------------------------------------------------------------
    // Music
    // ------------------------------------------------------------------

    private void ApplyMusicVolume()
    {
        if (musicSource == null) return;
        musicSource.volume = muted || musicMuted || previewTrack >= 0 ? 0f : musicVolume * masterVolume * musicTrackVolume * playlistGain;   // (playlistGain: faded down while a music cue plays)
    }

    private bool musicHeldByFocus;
    private AudioClip musicLastClip;
    private float musicLastTime;
    private float musicHeldTime;

    /// <summary>Alt-tabbing stops the audio engine, which used to make the music pick a NEW track. Hold the current one and carry on where it was.</summary>
    private void OnApplicationFocus(bool focus)
    {
        if (musicSource == null) return;
        if (!focus)
        {
            if (musicSource.isPlaying && musicSource.clip != null)
            {
                musicHeldTime = musicSource.time;
                musicHeldByFocus = true;
                musicSource.Pause();
            }
        }
        else if (musicHeldByFocus)
        {
            musicHeldByFocus = false;
            if (musicSource.clip != null && playMusic)
            {
                musicSource.UnPause();
                if (!musicSource.isPlaying)
                {
                    musicSource.time = Mathf.Min(musicHeldTime, Mathf.Max(0f, musicSource.clip.length - 0.05f));
                    musicSource.Play();
                }
            }
        }
    }

    // ---- Custom playlist: songs the player ticked off in Settings > Audio are skipped (all on by default; at least one stays on) ----

    private const string PrefPlaylistOff = "PixelClicker.Setting.PlaylistOff";
    private HashSet<string> playlistOff;

    private HashSet<string> PlaylistOff
    {
        get
        {
            if (playlistOff == null)
            {
                playlistOff = new HashSet<string>();
                foreach (string part in PixelPrefs.GetString(PrefPlaylistOff, "").Split('|')) if (part.Length > 0) playlistOff.Add(part);
            }
            return playlistOff;
        }
    }

    private string TrackKey(int i) => musicTracks != null && i >= 0 && i < musicTracks.Length && musicTracks[i] != null ? musicTracks[i].name : "#" + i;

    /// <summary>Is this song in the playlist (ticked)?</summary>
    public bool IsTrackEnabled(int i) => !PlaylistOff.Contains(TrackKey(i));

    /// <summary>How many songs are in the playlist.</summary>
    public int EnabledTrackCount
    {
        get { int n = 0; for (int i = 0; i < MusicTrackCount; i++) if (IsTrackEnabled(i)) n++; return n; }
    }

    /// <summary>Ticks / unticks a song (saved). The last ticked song cannot be unticked: returns false then.</summary>
    public bool SetTrackEnabled(int i, bool on)
    {
        if (i < 0 || i >= MusicTrackCount) return false;
        if (!on && IsTrackEnabled(i) && EnabledTrackCount <= 1) return false;
        if (on) PlaylistOff.Remove(TrackKey(i)); else PlaylistOff.Add(TrackKey(i));
        PixelPrefs.SetString(PrefPlaylistOff, string.Join("|", PlaylistOff));
        return true;
    }

    /// <summary>Puts every song back in the playlist.</summary>
    public void EnableAllTracks()
    {
        PlaylistOff.Clear();
        PixelPrefs.SetString(PrefPlaylistOff, "");
    }

    // ---- Track choice (the main menu's music selector) ----

    /// <summary>How many music tracks there are.</summary>
    public int MusicTrackCount => musicTracks != null ? musicTracks.Length : 0;

    /// <summary>A track's name for lists (the clip's name, underscores / dashes turned into spaces).</summary>
    public string MusicTrackName(int i)
    {
        if (musicTracks == null || i < 0 || i >= musicTracks.Length || musicTracks[i] == null) return "Track " + (i + 1);
        return PrettyClipName(musicTracks[i], "Track " + (i + 1));
    }

    /// <summary>A clip's name for lists and captions (underscores / dashes turned into spaces).</summary>
    public static string PrettyClipName(AudioClip clip, string fallback)
    {
        if (clip == null) return fallback;
        string n = clip.name.Replace('_', ' ').Replace('-', ' ').Trim();
        return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : fallback;
    }

    /// <summary>The clip that is audible now: the music cue's while one plays (it may not be one of the Music Tracks), otherwise the playlist's track.</summary>
    public AudioClip PlayingClip
    {
        get
        {
            if (cueActive >= 0 && cueSource != null && cueSource.clip != null && cueGain > 0.05f) return cueSource.clip;
            return musicSource != null ? musicSource.clip : null;
        }
    }

    /// <summary>
    /// Where the audible music is in beats (0 = its first beat, 1.5 = halfway between the 2nd and 3rd; negative = before the first beat),
    /// from the playing source's sample position and the song's tempo in musicTempos (defaultMusicBpm when not listed).
    /// False while no music plays (stopped, paused, between songs) - the caller then keeps its own clock.
    /// </summary>
    public bool TryGetMusicBeat(out double beat)
    {
        beat = 0d;
        AudioSource src = cueActive >= 0 && cueSource != null && cueSource.clip != null && cueGain > 0.05f ? cueSource : musicSource;
        if (src == null || src.clip == null || !src.isPlaying || src.clip.frequency <= 0) return false;
        float bpm = defaultMusicBpm, first = 0f;
        if (musicTempos != null)
            for (int i = 0; i < musicTempos.Count; i++)
                if (musicTempos[i] != null && musicTempos[i].clip == src.clip) { bpm = musicTempos[i].bpm; first = musicTempos[i].firstBeatSeconds; break; }
        if (bpm <= 0f) return false;
        double seconds = (double)src.timeSamples / src.clip.frequency;   // (sample position: exact, unlike AudioSource.time)
        beat = (seconds - first) * bpm / 60d;
        return true;
    }

    /// <summary>The Music Tracks index of a clip (-1 = not one of them).</summary>
    public int TrackIndexOf(AudioClip clip) => clip != null && musicTracks != null ? System.Array.IndexOf(musicTracks, clip) : -1;

    /// <summary>The index of the track playing now (-1 = none yet).</summary>
    public int CurrentMusicTrack => TrackIndexOf(PlayingClip);

    /// <summary>Starts this track now from the beginning; the normal shuffle / order carries on after it. (Not remembered: the next launch starts on a random song.)</summary>
    public void SelectMusicTrack(int i)
    {
        if (i < 0 || musicTracks == null || i >= musicTracks.Length || musicTracks[i] == null) return;
        if (cueActive >= 0) { cueSpent = cueActive; cueTarget = -1; }   // picking a song overrides the cue that is playing (until its trigger has ended once)
        PlayTrack(i);
    }

    // ---- Playlist preview: a short snippet of a song (the custom playlist's play buttons); the music itself goes quiet meanwhile ----

    private AudioSource previewSource;
    private float previewEnds;
    private int previewTrack = -1;
    private const float PreviewSeconds = 8f;

    /// <summary>The song whose snippet is playing (-1 = none).</summary>
    public int PreviewingTrack => previewTrack;

    /// <summary>Plays a snippet (about 8 s from a bit into the song). Calling it again for the same song stops it.</summary>
    public void PreviewTrack(int i)
    {
        if (musicTracks == null || i < 0 || i >= musicTracks.Length || musicTracks[i] == null) return;
        if (previewTrack == i) { StopPreview(); return; }
        if (previewSource == null)
        {
            GameObject go = new GameObject("Music Preview");
            go.transform.SetParent(transform, false);
            previewSource = go.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.loop = false;
        }
        AudioClip clip = musicTracks[i];
        previewSource.clip = clip;
        previewSource.time = Mathf.Clamp(clip.length * 0.3f, 0f, Mathf.Max(0f, clip.length - PreviewSeconds - 0.5f));
        previewSource.Play();
        previewTrack = i;
        previewEnds = Time.unscaledTime + PreviewSeconds;
        ApplyMusicVolume();
    }

    /// <summary>Ends a snippet (the music comes back).</summary>
    public void StopPreview()
    {
        if (previewSource != null && previewSource.isPlaying) previewSource.Stop();
        previewTrack = -1;
        ApplyMusicVolume();
    }

    private void UpdatePreview()
    {
        if (previewTrack < 0) return;
        bool over = Time.unscaledTime >= previewEnds || previewSource == null || (!previewSource.isPlaying && Application.isFocused && !AudioListener.pause);
        if (over) { StopPreview(); return; }
        // Fade out over the last second.
        float left = previewEnds - Time.unscaledTime;
        previewSource.volume = (muted ? 0f : musicVolume * masterVolume * musicTrackVolume) * Mathf.Clamp01(left);
    }

    private void PlayTrack(int i)
    {
        if (musicSource == null || musicTracks == null || i < 0 || i >= musicTracks.Length || musicTracks[i] == null || !playMusic) return;
        lastTrack = i;
        musicLastClip = null; musicLastTime = 0f; musicHeldByFocus = false;
        musicSource.clip = musicTracks[i];
        musicSource.time = 0f;
        musicSource.Play();
    }

    private bool musicHeldByStudio;   // the music is paused for the studio intro (PixelStudioIntro.HoldsMusic)

    private void UpdateMusic()
    {
        if (musicSource == null) return;
        if (PixelStudioIntro.HoldsMusic)
        {
            // The studio intro has its own sounds: all music waits (paused, so it carries on where it was) until it ends.
            if (musicSource.isPlaying) musicSource.Pause();
            if (cueSource != null && cueSource.isPlaying) cueSource.Pause();
            musicHeldByStudio = true;
            return;
        }
        if (musicHeldByStudio)
        {
            musicHeldByStudio = false;
            musicSource.UnPause();
            if (cueSource != null) cueSource.UnPause();
        }
        UpdateCues();
        ApplyMusicVolume();
        UpdatePreview();
        if (cueHoldsPlaylist) return;   // the playlist is paused while a music cue plays (it resumes where it was)

        if (!playMusic || musicTracks == null || musicTracks.Length == 0)
        {
            if (musicSource.isPlaying) musicSource.Stop();
            return;
        }
        if (musicSource.isPlaying)
        {
            // Remember where the track is, so a stop that was not the track ending (alt-tab) can be resumed there.
            musicLastClip = musicSource.clip;
            musicLastTime = musicSource.time;
            return;
        }
        if (AudioListener.pause || musicHeldByFocus || !Application.isFocused) return; // paused audio also stops isPlaying; don't restart then

        // Stopped in the middle of a track (the audio engine stops when the window loses focus): carry on with it instead of picking a new one.
        if (musicLastClip != null && musicLastClip == musicSource.clip && musicLastTime > 0.05f && musicLastTime < musicLastClip.length - 0.75f)
        {
            musicSource.time = musicLastTime;
            musicSource.Play();
            return;
        }
        musicLastClip = null;
        musicLastTime = 0f;

        // Only the songs in the playlist (all of them unless the player made a custom one). The FIRST song after the game loads is always a random one.
        List<int> pool = new List<int>();
        for (int i = 0; i < musicTracks.Length; i++) if (musicTracks[i] != null && IsTrackEnabled(i)) pool.Add(i);
        if (pool.Count == 0) for (int i = 0; i < musicTracks.Length; i++) if (musicTracks[i] != null) pool.Add(i);
        if (pool.Count == 0) return;
        int next;
        if ((shuffleOn || lastTrack < 0) && pool.Count > 1)
        {
            do next = pool[UnityEngine.Random.Range(0, pool.Count)]; while (next == lastTrack);
        }
        else if (pool.Count == 1) next = pool[0];
        else
        {
            next = pool[0];
            foreach (int c in pool) if (c > lastTrack) { next = c; break; }   // the next song in the list that is in the playlist
        }

        lastTrack = next;
        if (musicTracks[next] == null) return;
        musicSource.clip = musicTracks[next];
        musicSource.Play();
    }

    // ------------------------------------------------------------------
    // Game events -> sounds
    // ------------------------------------------------------------------

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        PlaySound(automatic ? "auto_click" : "click", 1f);
        if (clicker != null && tierIndex >= 0 && tierIndex < clicker.Tiers.Length)
        {
            PixelClicker.PixelType type = clicker.Tiers[tierIndex].type;
            // Obsidian: the clicks climb in pitch, the breaking one is the highest. Inside a TREE every block sounds a little higher the further
            // along the tree you are, and the trunk creaks on the last two blocks - an audible 'nearly down' cue before Timber.
            float pitch = type == PixelClicker.PixelType.Obsidian ? 1.6f : TreePitch();
            PlaySound("pixel_" + PixelBlockCatalog.SoundType(type).ToString().ToLowerInvariant(), pitch);   // (a card can borrow another block's sounds: soundAs)
            int left = PixelTreeMode.LiveBlocksLeft;
            if (left == 1 || left == 2) PlaySound("tree_creak", left == 1 ? 1.08f : 0.95f, left == 1 ? 1f : 0.6f);
        }
    }

    private void OnHit(int tierIndex, int hits, int needed, bool automatic)
    {
        PlaySound("hit", 1f);
        // Tough pixels (Obsidian) also play their own pixel sound on every hit, not only on the breaking click. It ramps up: each
        // hit is louder than the one before, and the breaking click (OnCollected) plays at the full volume set in the Sounds list.
        if (clicker != null && tierIndex >= 0 && tierIndex < clicker.Tiers.Length)
        {
            float ramp = Mathf.Clamp01(hits / (float)Mathf.Max(1, needed));
            PixelClicker.PixelType type = clicker.Tiers[tierIndex].type;
            float pitch = type == PixelClicker.PixelType.Obsidian ? Mathf.Lerp(0.6f, 1.45f, ramp) : TreePitch();   // starts low, rises each click
            PlaySound("pixel_" + PixelBlockCatalog.SoundType(type).ToString().ToLowerInvariant(), pitch, Mathf.Max(0.1f, ramp * ramp));
        }
    }

    /// <summary>Inside a tree: the block sounds rise from 0.9x on its first block to 1.25x on its last (1 outside a tree).</summary>
    private static float TreePitch()
    {
        float p = PixelTreeMode.LiveProgress;
        return p < 0f ? 1f : Mathf.Lerp(0.9f, 1.25f, p);
    }

    private void OnVacuumed(int tierIndex, double total, int count) => PlaySound("vacuum", 1f);

    private void OnLanded(float intensity) => PlaySound("pixel_land", 1f, Mathf.Lerp(0.25f, 1f, intensity));

    private void OnPurchase(int packIndex) => PlaySound("purchase", 1f);

    private void OnAchievement(int index) => PlaySound("achievement", 1f);

    private void OnPotionDrunk(string potionName) => PlaySound("drink", 1f);

    private void OnDevicePlaced(PixelConsumables.DeviceKind kind) => PlaySound("device_place", 1f);

    private void OnCrafted() => PlaySound("craft", 1f);

    private void OnCombo(int count)
    {
        float pitch = Mathf.Min(comboPitchMax, 1f + Mathf.Max(0, count - 1) * comboPitchStep);
        PlaySound("combo", pitch);
    }

    private void OnMinigame(PixelMinigame game, PixelMinigame.MinigameEvent what)
    {
        PlaySound(game.Id + (what == PixelMinigame.MinigameEvent.Clicked ? "_click" : "_spawn"), 1f);
    }
}
