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
public class PixelAudio : MonoBehaviour
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
    [Tooltip("Every sound effect, by id. Built-in ids: click, auto_click, pixel_land (an old pixel hits the floor), hit, vacuum, combo, purchase, craft, drink, " +
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

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private const string PrefMaster = "PixelAudio.Master";
    private const string PrefEffects = "PixelAudio.Effects";
    private const string PrefMusic = "PixelAudio.Music";
    private const string PrefMuted = "PixelAudio.Muted";

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
        set { masterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefMaster, masterVolume); ApplyMusicVolume(); }
    }

    /// <summary>Sound effect volume 0..1 (saved).</summary>
    public float EffectsVolume
    {
        get => effectsVolume;
        set { effectsVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefEffects, effectsVolume); }
    }

    /// <summary>Music volume 0..1 (saved).</summary>
    public float MusicVolume
    {
        get => musicVolume;
        set { musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefMusic, musicVolume); ApplyMusicVolume(); }
    }

    /// <summary>All sound off (saved).</summary>
    public bool Muted
    {
        get => muted;
        set { muted = value; PlayerPrefs.SetInt(PrefMuted, muted ? 1 : 0); ApplyMusicVolume(); }
    }

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    private static Sound Make(string id, float cooldown = 0.03f, float pitchMin = 0.97f, float pitchMax = 1.03f)
    {
        return new Sound { id = id, cooldown = cooldown, pitchMin = pitchMin, pitchMax = pitchMax };
    }

    private static List<Sound> CreateDefaultSounds()
    {
        List<Sound> list = new List<Sound>
        {
            Make("click", 0.02f, 0.92f, 1.08f),
            Make("pixel_land", 0.02f, 0.88f, 1.12f),
            Make("auto_click", 0.06f, 0.92f, 1.08f),
            Make("hit", 0.03f, 0.92f, 1.08f),
            Make("vacuum", 0.2f),
            Make("combo", 0.02f, 1f, 1f),
            Make("purchase", 0.08f),
            Make("craft", 0.1f),
            Make("drink", 0.1f),
            Make("device_place", 0.1f),
            Make("achievement", 0.2f),
            Make("ui_click", 0.05f),
            Make("grab", 0.05f),
            Make("drop", 0.05f),
            Make("ui_scroll", 0.03f, 0.95f, 1.05f),
            Make("ghost_spawn", 0.2f),
            Make("ghost_click", 0.1f),
            Make("meteor_spawn", 0.2f),
            Make("meteor_click", 0.03f, 0.9f, 1.1f),
            Make("blackhole_spawn", 0.2f),
            Make("pad_spawn", 0.2f),
            Make("pad_click", 0.05f, 0.92f, 1.08f),
            Make("pad_wrong", 0.05f),
            Make("pad_ultra", 0.2f, 1f, 1f),
            Make("time_stop", 0.3f, 1f, 1f),
            Make("device_remove", 0.2f, 1f, 1f),
            Make("sorter_button", 0.2f, 1f, 1f),
            Make("hose_toggle", 0.2f, 1f, 1f),
            Make("bank_suck", 0.1f, 1f, 1f),
            Make("bank_spit", 0.05f, 1f, 1f),
            Make("bank_select", 0.05f, 1f, 1f),
            Make("bank_full", 0.3f, 1f, 1f),
            Make("bank_empty", 0.3f, 1f, 1f),
            Make("time_resume", 0.3f, 1f, 1f),
            Make("time_stop_loop", 0f, 1f, 1f),
            Make("bomb_spawn", 0.2f),
            Make("bomb_click", 0.1f),
            Make("bomb_tick", 0.1f, 1f, 1f),
            Make("bomb_explode", 0.2f),
        };
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            list.Add(Make("pixel_" + type.ToString().ToLowerInvariant(), 0.03f, 0.92f, 1.08f));
        return list;
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

        // The player's saved choices win over the Inspector's first-run values.
        if (PlayerPrefs.HasKey(PrefMaster)) masterVolume = PlayerPrefs.GetFloat(PrefMaster);
        if (PlayerPrefs.HasKey(PrefEffects)) effectsVolume = PlayerPrefs.GetFloat(PrefEffects);
        if (PlayerPrefs.HasKey(PrefMusic)) musicVolume = PlayerPrefs.GetFloat(PrefMusic);
        if (PlayerPrefs.HasKey(PrefMuted)) muted = PlayerPrefs.GetInt(PrefMuted) != 0;

        EnsureDefaultSounds();
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

    private void PlaySound(string id, float pitchMultiplier, float volumeMultiplier = 1f)
    {
        if (muted || voices == null || !byId.TryGetValue(id, out Sound s)) return;
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

        AudioSource voice = voices[nextVoice];
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
        musicSource.volume = muted ? 0f : musicVolume * masterVolume * musicTrackVolume;
    }

    private void UpdateMusic()
    {
        if (musicSource == null) return;
        ApplyMusicVolume();

        if (!playMusic || musicTracks == null || musicTracks.Length == 0)
        {
            if (musicSource.isPlaying) musicSource.Stop();
            return;
        }
        if (musicSource.isPlaying || AudioListener.pause) return; // paused audio also stops isPlaying; don't restart then

        int next;
        if (shuffleMusic && musicTracks.Length > 1)
        {
            next = UnityEngine.Random.Range(0, musicTracks.Length);
            if (next == lastTrack) next = (next + 1) % musicTracks.Length;
        }
        else next = (lastTrack + 1) % musicTracks.Length;

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
            PlaySound("pixel_" + clicker.Tiers[tierIndex].type.ToString().ToLowerInvariant(), 1f);
    }

    private void OnHit(int tierIndex, int hits, int needed, bool automatic) => PlaySound("hit", 1f);

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
