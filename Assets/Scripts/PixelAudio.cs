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

    private float defaultMaster, defaultEffects, defaultMusic;
    private bool defaultMuted;

    /// <summary>A save file with its own settings was loaded: read the volumes again (missing ones go back to the Inspector values).</summary>
    public static void ReloadFromPrefs()
    {
        if (instance == null) return;
        instance.masterVolume = PlayerPrefs.HasKey(PrefMaster) ? PlayerPrefs.GetFloat(PrefMaster) : instance.defaultMaster;
        instance.effectsVolume = PlayerPrefs.HasKey(PrefEffects) ? PlayerPrefs.GetFloat(PrefEffects) : instance.defaultEffects;
        instance.musicVolume = PlayerPrefs.HasKey(PrefMusic) ? PlayerPrefs.GetFloat(PrefMusic) : instance.defaultMusic;
        instance.muted = PlayerPrefs.HasKey(PrefMuted) ? PlayerPrefs.GetInt(PrefMuted) != 0 : instance.defaultMuted;
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
            Make("pixel_bounce_electric", 0.05f, 0.9f, 1.15f),
            Make("overcharge", 0.3f, 1f, 1f),
            Make("dragon_summon", 1f, 1f, 1f),
            Make("glass_shatter", 0.04f, 0.9f, 1.1f),
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
            Make("ultra_gain", 0.2f, 1f, 1f),
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
            Make("game_start", 0.2f, 1f, 1f),
            Make("bomb_spawn", 0.2f),
            Make("bomb_click", 0.1f),
            Make("bomb_tick", 0.1f, 1f, 1f),
            Make("bomb_explode", 0.2f),
            Make("seed_dig", 0.04f, 0.9f, 1.1f),
            Make("seed_plant", 0.05f, 0.9f, 1.1f),
            Make("water_splash", 0.05f, 0.9f, 1.1f),
            Make("fire_burst", 0.1f, 0.92f, 1.08f),
            Make("dragon_find", 0.1f, 1f, 1f),
            Make("dragon_stash", 0.1f, 1f, 1f),
            Make("fire_crackle", 0.15f, 0.85f, 1.15f),
            Make("ore_chip", 0.04f, 0.8f, 1.3f),
            Make("ore_gleam", 0.3f, 0.95f, 1.05f),
        };
        foreach (PixelClicker.PixelType ore in Enum.GetValues(typeof(PixelClicker.PixelType)))
            if (PixelClicker.IsOre(ore)) list.Add(Make("pixel_bounce_" + ore.ToString().ToLowerInvariant(), 0.04f, 0.9f, 1.15f)); // ore clinks when an old ore lands
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            list.Add(Make("pixel_" + type.ToString().ToLowerInvariant(), 0.03f, 0.92f, 1.08f));
        return list;
    }

    /// <summary>
    /// Sounds that ship without audio files get a clip synthesized in code (only while their clip list is empty, so putting your own
    /// clips in the Inspector replaces them): the Electric pixel's bounce is a short crackling zap.
    /// </summary>
    private void FillSynthesizedClips()
    {
        if (sounds == null) return;
        foreach (Sound s in sounds)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            AudioClip[] made = SynthFor(s.id);
            if (made == null) continue;
            if (s.clips != null && s.clips.Length > 0 && s.clips[0] != null) continue;
            s.clips = made;
        }
    }

    /// <summary>The code-made clips of a sound id (null = this sound has none).</summary>
    private static AudioClip[] SynthFor(string id)
    {
        switch (id)
        {
            case "pixel_bounce_electric": return new[] { PixelSynth.Zap(1), PixelSynth.Zap(2), PixelSynth.Zap(3) };
            case "overcharge": return new[] { PixelSynth.Charge() };
            case "dragon_summon": return new[] { PixelSynth.Summon() };
            case "seed_dig": return new[] { PixelSynth.Dig(1), PixelSynth.Dig(2), PixelSynth.Dig(3) };
            case "seed_plant": return new[] { PixelSynth.Crunch(1), PixelSynth.Crunch(2), PixelSynth.Crunch(3) };
            case "water_splash": return new[] { PixelSynth.Splash(1), PixelSynth.Splash(2), PixelSynth.Splash(3) };
            case "dragon_find": return new[] { PixelSynth.DragonFind() };
            case "dragon_stash": return new[] { PixelSynth.DragonStash() };
            case "fire_burst": return new[] { PixelSynth.FireBurst(1), PixelSynth.FireBurst(2), PixelSynth.FireBurst(3) };
            case "fire_crackle": return new[] { PixelSynth.FireCrackle(1), PixelSynth.FireCrackle(2), PixelSynth.FireCrackle(3) };
            case "ore_chip": return new[] { PixelSynth.OreChip(1), PixelSynth.OreChip(2), PixelSynth.OreChip(3), PixelSynth.OreChip(4) };
            case "ore_gleam": return new[] { PixelSynth.OreGleam() };
        }
        // Each ore's own click is the sound of it breaking; its landing is a metallic clink.
        foreach (PixelClicker.PixelType ore in System.Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (!PixelClicker.IsOre(ore)) continue;
            string name = ore.ToString().ToLowerInvariant();
            int variant = (int)ore - (int)PixelClicker.PixelType.Copper;
            if (id == "pixel_" + name) return new[] { PixelSynth.OreBreak(1 + variant), PixelSynth.OreBreak(7 + variant) };
            if (id == "pixel_bounce_" + name) return new[] { PixelSynth.OreClink(1 + variant), PixelSynth.OreClink(11 + variant), PixelSynth.OreClink(21 + variant) };
        }
        return null;
    }

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
        if (PlayerPrefs.HasKey(PrefMaster)) masterVolume = PlayerPrefs.GetFloat(PrefMaster);
        if (PlayerPrefs.HasKey(PrefEffects)) effectsVolume = PlayerPrefs.GetFloat(PrefEffects);
        if (PlayerPrefs.HasKey(PrefMusic)) musicVolume = PlayerPrefs.GetFloat(PrefMusic);
        if (PlayerPrefs.HasKey(PrefMuted)) muted = PlayerPrefs.GetInt(PrefMuted) != 0;

        EnsureDefaultSounds();
        FillSynthesizedClips();
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

    private void OnHit(int tierIndex, int hits, int needed, bool automatic)
    {
        PlaySound("hit", 1f);
        // Tough pixels (Obsidian) also play their own pixel sound on every hit, not only on the breaking click. It ramps up: each
        // hit is louder than the one before, and the breaking click (OnCollected) plays at the full volume set in the Sounds list.
        if (clicker != null && tierIndex >= 0 && tierIndex < clicker.Tiers.Length)
        {
            float ramp = Mathf.Clamp01(hits / (float)Mathf.Max(1, needed));
            PlaySound("pixel_" + clicker.Tiers[tierIndex].type.ToString().ToLowerInvariant(), 1f, Mathf.Max(0.1f, ramp * ramp));
        }
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


/// <summary>Tiny sound synthesizer: makes AudioClips in code for sounds that have no audio file yet.</summary>
public static class PixelSynth
{
    /// <summary>A short electric zap: a buzzing chirp falling in pitch under a burst of crackle, with a fast decay.</summary>
    public static AudioClip Zap(int seed)
    {
        const int rate = 44100;
        float length = 0.3f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(seed * 7919);

        float phase = 0f, crackle = 0f;
        float startHz = 1100f + seed * 140f, endHz = 160f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float sec = i / (float)rate;
            float env = Mathf.Exp(-sec * 13f);

            // Buzz: a sawtooth chirp, a little ragged.
            float hz = Mathf.Lerp(startHz, endHz, Mathf.Pow(t, 0.45f)) * (1f + ((float)rng.NextDouble() - 0.5f) * 0.04f);
            phase += hz / rate;
            phase -= Mathf.Floor(phase);
            float saw = phase * 2f - 1f;

            // Crackle: random sparks that are held for a few samples.
            if (rng.NextDouble() < 0.035 * (1f - t)) crackle = ((float)rng.NextDouble() * 2f - 1f);
            crackle *= 0.9f;

            float noise = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 40f); // the initial snap
            data[i] = (saw * 0.35f + crackle * 0.9f + noise * 0.8f) * env;
        }

        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;

        AudioClip clip = AudioClip.Create("Zap " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A charge-up: a rising buzz with growing crackle that ends in a big snap.</summary>
    /// <summary>A watery splash: a hiss of filtered noise plus a few quick rising bubble "bloops". 'seed' picks a variant.</summary>
    public static AudioClip Splash(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.32f);
        float[] data = new float[n];
        System.Random rng = new System.Random(500 + seed * 31);
        float lp = 0f;
        float[] bloopAt = { 0.015f, 0.06f + seed * 0.01f, 0.115f };
        float[] bloopHz = { 520f + seed * 60f, 700f + seed * 50f, 900f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.18f;
            float hiss = (noise - lp) * Mathf.Exp(-sec * 14f);        // bright splash hiss
            float bloops = 0f;
            for (int k = 0; k < bloopAt.Length; k++)
            {
                float t = sec - bloopAt[k];
                if (t < 0f) continue;
                float hz = bloopHz[k] * (1f + t * 6f);               // each bubble rises in pitch as it pops
                bloops += Mathf.Sin(t * hz * Mathf.PI * 2f) * Mathf.Exp(-t * 38f) * 0.45f;
            }
            data[i] = (hiss * 0.8f + bloops) * Mathf.Clamp01(sec / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("Splash " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A small crunch, like a seed being pressed into gravelly soil: a few quick crackles over a soft low squash. 'seed' picks a variant.</summary>
    public static AudioClip Crunch(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.2f);
        float[] data = new float[n];
        System.Random rng = new System.Random(300 + seed * 23);
        // A handful of crackle bursts at slightly different times: the "crunch".
        float[] at = { 0.004f, 0.03f + seed * 0.004f, 0.065f, 0.1f + seed * 0.003f };
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.55f;
            float env = 0f;
            for (int k = 0; k < at.Length; k++)
                if (sec >= at[k]) env += Mathf.Exp(-(sec - at[k]) * 70f) * (0.9f - k * 0.15f);
            float squash = Mathf.Sin(sec * (95f - seed * 8f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 30f) * 0.5f;
            data[i] = (lp * env + squash) * Mathf.Clamp01(sec / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("Crunch " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short digging sound: a gritty scrape of dirt over a low thud. 'seed' picks one of a few variants.</summary>
    public static AudioClip Dig(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.26f);
        float[] data = new float[n];
        System.Random rng = new System.Random(900 + seed * 17);
        float lp = 0f, lp2 = 0f;
        float thumpHz = 60f + seed * 9f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.32f;            // rough, mid-low scrape
            lp2 += (lp - lp2) * 0.5f;
            float scrape = lp2 * (0.55f + 0.45f * Mathf.Sin(sec * (70f + seed * 12f))) * Mathf.Exp(-sec * 11f);
            float grit = rng.NextDouble() < 0.035 ? ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 18f) : 0f;
            float thump = Mathf.Sin(sec * thumpHz * Mathf.PI * 2f) * Mathf.Exp(-sec * 24f) * 0.9f;
            data[i] = (scrape * 1.4f + grit * 0.6f + thump) * Mathf.Clamp01(sec / 0.004f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Dig " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip Charge()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.7f);
        float[] data = new float[n];
        System.Random rng = new System.Random(4242);
        float phase = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float sec = i / (float)rate;
            float hz = Mathf.Lerp(180f, 1500f, t * t);
            phase += hz / rate;
            phase -= Mathf.Floor(phase);
            float buzz = (phase * 2f - 1f) * Mathf.Lerp(0.15f, 0.45f, t);

            if (rng.NextDouble() < 0.01 + 0.06 * t) crackle = (float)rng.NextDouble() * 2f - 1f;
            crackle *= 0.88f;

            float body = (buzz + crackle * Mathf.Lerp(0.2f, 0.9f, t)) * Mathf.Clamp01(sec / 0.05f);
            float snap = t > 0.78f ? ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-(t - 0.78f) * 45f) : 0f; // the final crack
            data[i] = body * (t > 0.78f ? Mathf.Exp(-(t - 0.78f) * 18f) : 1f) + snap * 0.9f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;
        AudioClip clip = AudioClip.Create("Overcharge", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The dragon summoning: a deep drone that rises with shimmering overtones and crackle, ending in a boom (about 5 seconds).</summary>
    public static AudioClip Summon()
    {
        const int rate = 22050;
        float length = 5.2f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(777);
        float p1 = 0f, p2 = 0f, p3 = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float t = sec / length;
            float rise = t * t;
            float f = Mathf.Lerp(55f, 420f, rise);
            p1 += f / rate; p2 += f * 1.5f / rate; p3 += f * 2.01f / rate;
            float drone = Mathf.Sin(p1 * 6.2832f) * 0.5f + Mathf.Sin(p2 * 6.2832f) * 0.25f * rise + Mathf.Sin(p3 * 6.2832f) * 0.18f * rise;
            drone *= 1f + 0.25f * Mathf.Sin(sec * Mathf.Lerp(4f, 22f, rise) * 6.2832f); // tremolo that speeds up

            if (rng.NextDouble() < 0.002 + 0.03 * rise) crackle = (float)rng.NextDouble() * 2f - 1f;
            crackle *= 0.9f;

            float env = Mathf.Clamp01(sec / 0.4f) * Mathf.Lerp(0.3f, 1f, rise);
            float value = drone * env + crackle * 0.5f * rise;

            // The boom when the cubes burst together.
            float boomT = sec - 3.9f;
            if (boomT > 0f)
            {
                float boom = Mathf.Sin(boomT * 6.2832f * Mathf.Lerp(90f, 30f, Mathf.Clamp01(boomT * 2f))) * Mathf.Exp(-boomT * 3.5f);
                float hiss = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-boomT * 7f);
                value = value * Mathf.Exp(-boomT * 4f) + boom * 1.1f + hiss * 0.6f;
            }
            data[i] = value;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;
        AudioClip clip = AudioClip.Create("Dragon Summon", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A Dragon Cube turns up: a swelling whoosh, then a sparkling rising arpeggio of bell tones (about 3 seconds).</summary>
    public static AudioClip DragonFind()
    {
        const int rate = 22050;
        float length = 3.2f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rng = new System.Random(4242);
        float lp = 0f;
        float[] notes = { 392f, 493.9f, 587.3f, 784f, 987.8f, 1174.7f, 1568f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.08f;
            float whoosh = lp * 2.2f * Mathf.Sin(Mathf.Clamp01(sec / 0.7f) * Mathf.PI) * (sec < 0.7f ? 1f : 0f);
            float v = whoosh;
            for (int k = 0; k < notes.Length; k++)
            {
                float start = 0.55f + k * 0.22f;
                float t = sec - start;
                if (t < 0f) continue;
                float env = Mathf.Exp(-t * 2.6f) * Mathf.Clamp01(t / 0.01f);
                v += (Mathf.Sin(t * notes[k] * 6.2832f) + 0.35f * Mathf.Sin(t * notes[k] * 2.01f * 6.2832f)) * env * 0.28f;
            }
            data[i] = v;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("DragonFind", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The Dragon Cube drops into the backpack: a soft thump with a quick glittering chime.</summary>
    public static AudioClip DragonStash()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.9f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float thump = Mathf.Sin(sec * 90f * 6.2832f) * Mathf.Exp(-sec * 12f);
            float chime = (Mathf.Sin(sec * 1568f * 6.2832f) + Mathf.Sin(sec * 2093f * 6.2832f) * 0.6f) * Mathf.Exp(-sec * 5f) * 0.4f * Mathf.Clamp01((sec - 0.03f) / 0.01f);
            data[i] = thump + chime;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("DragonStash", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A whoosh of fire: a rising roar of band-limited noise with a deep thump at the start and a sprinkle of crackles. 'seed' picks a variant.</summary>
    public static AudioClip FireBurst(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.7f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1100 + seed * 41);
        float lp = 0f, lp2 = 0f, crackle = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.12f;           // low-passed: a rumbling roar
            lp2 += (lp - lp2) * 0.35f;
            float env = Mathf.Clamp01(sec / 0.05f) * Mathf.Exp(-sec * 4.2f);
            float thump = Mathf.Sin(sec * (70f - seed * 6f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 14f) * 0.9f;
            if (rng.NextDouble() < 0.012 * (1f - sec)) crackle = ((float)rng.NextDouble() * 2f - 1f) * 0.9f;
            crackle *= 0.86f;
            data[i] = (lp2 * 3.2f * env + thump + crackle * 0.5f * Mathf.Exp(-sec * 3f));
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("FireBurst " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fire crackling: a few sharp pops and snaps over a soft hiss.</summary>
    public static AudioClip FireCrackle(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.45f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1300 + seed * 53);
        float lp = 0f, pop = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.3f;
            if (rng.NextDouble() < 0.006) pop = ((float)rng.NextDouble() * 2f - 1f);
            pop *= 0.78f;
            data[i] = (pop * 1.2f + (noise - lp) * 0.1f) * Mathf.Exp(-sec * 5f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("FireCrackle " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A pickaxe hit on rock: a tick of grit and a short metallic ping. 'seed' picks the pitch.</summary>
    public static AudioClip OreChip(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.22f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1500 + seed * 17);
        float hz = 1500f + seed * 260f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float tick = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 160f);
            float ping = (Mathf.Sin(sec * hz * Mathf.PI * 2f) + 0.5f * Mathf.Sin(sec * hz * 2.76f * Mathf.PI * 2f)) * Mathf.Exp(-sec * 28f);
            data[i] = tick * 0.9f + ping * 0.45f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("OreChip " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The ore breaking: a crack, a low rumble and a cascade of falling nuggets (little pings). 'seed' picks the variant.</summary>
    public static AudioClip OreBreak(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.5f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1700 + seed * 29);
        float lp = 0f;
        float[] at = { 0.05f, 0.09f, 0.14f, 0.2f, 0.27f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.4f;
            float crack = lp * Mathf.Exp(-sec * 38f) * 1.4f;
            float rumble = Mathf.Sin(sec * (55f + (seed % 5) * 6f) * Mathf.PI * 2f) * Mathf.Exp(-sec * 9f) * 0.8f;
            float nuggets = 0f;
            for (int k = 0; k < at.Length; k++)
            {
                float t = sec - at[k] - (seed % 3) * 0.004f;
                if (t < 0f) continue;
                float hz = 1600f + k * 330f + (seed % 7) * 70f;
                nuggets += Mathf.Sin(t * hz * Mathf.PI * 2f) * Mathf.Exp(-t * 55f) * (0.5f - k * 0.07f);
            }
            data[i] = (crack + rumble + nuggets) * Mathf.Clamp01(sec / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("OreBreak " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A bright three-note bell sparkle: the sound of a rich vein.</summary>
    public static AudioClip OreGleam()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.8f);
        float[] data = new float[n];
        float[] notes = { 1318.5f, 1760f, 2637f };   // E6, A6, E7
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float t = sec - k * 0.09f;
                if (t < 0f) continue;
                v += (Mathf.Sin(t * notes[k] * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * notes[k] * 3f * Mathf.PI * 2f)) * Mathf.Exp(-t * 5.5f) * 0.5f;
            }
            data[i] = v * Mathf.Clamp01(sec / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("OreGleam", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short metallic clink of a chunk of ore landing. 'seed' picks the pitch.</summary>
    public static AudioClip OreClink(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.18f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1900 + seed * 13);
        float hz = 900f + (seed % 9) * 130f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float tick = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-sec * 220f);
            float ring = (Mathf.Sin(sec * hz * Mathf.PI * 2f) + 0.6f * Mathf.Sin(sec * hz * 1.59f * Mathf.PI * 2f)) * Mathf.Exp(-sec * 32f);
            data[i] = tick * 0.7f + ring * 0.5f;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("OreClink " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
