// ---- System notes (moved here from CLAUDE.md) ----
// PixelAudio.cs (UFO beam)
//   The main-menu UFO's abduction plays ufo_beam (code-made PixelSynth.UfoBeam: a rising wobbling hum with a shimmer and a pop at the
//   end, ~2.6 s; stopped if the abduction is aborted).
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
    // Per save (PixelSettingsSync): music mute, the playlist and the chosen song. (Named PixelClicker.Setting.* so a new game resets them; PixelAudio.* keys are the player's own and stay.)
    private const string PrefMusicMuted = "PixelClicker.Setting.MusicMuted";
    private const string PrefSong = "PixelClicker.Setting.Song";
    private bool musicMuted;

    private float defaultMaster, defaultEffects, defaultMusic;
    private bool defaultMuted;

    /// <summary>A save file with its own settings was loaded: read the volumes again (missing ones go back to the Inspector values).</summary>
    private const string PrefUiClicks = "PixelAudio.UiClicks";
    private static int uiClicksCache = -1;

    /// <summary>Settings > Audio > 'UI click sounds': the click sound of every UI button / tick box (sound id ui_click). On by default.</summary>
    public static bool UiClicks
    {
        get { if (uiClicksCache < 0) uiClicksCache = PlayerPrefs.GetInt(PrefUiClicks, 1); return uiClicksCache != 0; }
        set { uiClicksCache = value ? 1 : 0; PlayerPrefs.SetInt(PrefUiClicks, uiClicksCache); PlayerPrefs.Save(); }
    }

    public static void ReloadFromPrefs()
    {
        uiClicksCache = -1;
        if (instance == null) return;
        instance.masterVolume = PlayerPrefs.HasKey(PrefMaster) ? PlayerPrefs.GetFloat(PrefMaster) : instance.defaultMaster;
        instance.effectsVolume = PlayerPrefs.HasKey(PrefEffects) ? PlayerPrefs.GetFloat(PrefEffects) : instance.defaultEffects;
        instance.musicVolume = PlayerPrefs.HasKey(PrefMusic) ? PlayerPrefs.GetFloat(PrefMusic) : instance.defaultMusic;
        instance.muted = PlayerPrefs.HasKey(PrefMuted) ? PlayerPrefs.GetInt(PrefMuted) != 0 : instance.defaultMuted;
        instance.musicMuted = PlayerPrefs.GetInt(PrefMusicMuted, 0) != 0;
        instance.playlistOff = null;   // read again (per save)
        instance.ApplyMusicVolume();
        instance.ApplySavedSong();
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

    /// <summary>The music alone off (saved per save file; the speaker button on the main menu and Settings > Audio).</summary>
    public bool MusicMuted
    {
        get => musicMuted;
        set { musicMuted = value; PlayerPrefs.SetInt(PrefMusicMuted, musicMuted ? 1 : 0); ApplyMusicVolume(); }
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
            Make("meteor_hit", 0.05f, 0.9f, 1.12f),
            new Sound { id = "pixel_vanish", cooldown = 0.04f, pitchMin = 0.88f, pitchMax = 1.3f, volume = 0.4f },
            new Sound { id = "meteor_rumble", cooldown = 0f, volume = 0.45f },
            Make("blackhole_spawn", 0.2f),
            Make("pad_spawn", 0.2f),
            Make("pad_click", 0.05f, 0.92f, 1.08f),
            Make("pad_wrong", 0.05f),
            Make("pad_ultra", 0.2f, 1f, 1f),
            Make("ultra_gain", 0.2f, 1f, 1f),
            Make("time_stop", 0.3f, 1f, 1f),
            Make("device_remove", 0.2f, 1f, 1f),
            Make("hazard_warn", 0.4f, 1f, 1f),
            Make("hornet_buzz", 0.5f, 0.95f, 1.05f),
            Make("axe_swing", 0.1f, 0.95f, 1.08f),
            Make("axe_chop", 0.05f, 0.92f, 1.08f),
            Make("nest_fall", 0.1f, 0.95f, 1.1f),
            Make("nest_catch", 0.1f, 0.97f, 1.05f),
            Make("remove_buildup", 0.05f, 1f, 1f),   // the rising whine while the right mouse button is held on a placed device (PixelHold)
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
            Make("jelly_sting", 0.1f, 0.95f, 1.05f),
            Make("fire_burst", 0.1f, 0.92f, 1.08f),
            Make("dragon_find", 0.1f, 1f, 1f),
            Make("chest_hit", 0.1f, 0.92f, 1.08f),
            Make("chest_open", 0.1f, 1f, 1f),
            Make("dragon_stash", 0.1f, 1f, 1f),
            Make("fire_crackle", 0.15f, 0.85f, 1.15f),
            Make("ore_chip", 0.04f, 0.8f, 1.3f),
            Make("ore_gleam", 0.3f, 0.95f, 1.05f),
            Make("stardust_fall", 0.3f, 0.97f, 1.03f),
            Make("pixel_bounce_ghost", 0.05f, 0.9f, 1.15f),
            Make("ghost_float", 0.3f, 0.95f, 1.05f),
            Make("ufo_beam", 0.3f, 1f, 1f),
            Make("talk_blip", 0.035f, 0.92f, 1.08f),
            Make("stardust_collect", 0.1f, 0.97f, 1.04f),
            Make("farm_hit", 0.04f, 0.85f, 1.2f),
            Make("wood_chop", 0.04f, 0.9f, 1.15f),
            Make("forage_pick", 0.04f, 0.9f, 1.15f),
            Make("fish_splash", 0.04f, 0.9f, 1.15f),
            Make("chipper_feed", 0.08f, 0.95f, 1.05f),
            Make("rack_store", 0.05f, 0.9f, 1.1f),
            Make("farm_gleam", 0.3f, 0.95f, 1.05f),
            Make("traffic_start", 0.2f, 1f, 1f),
            Make("traffic_green", 0.2f, 1f, 1f),
            Make("traffic_yellow", 0.2f, 1f, 1f),
            Make("traffic_red", 0.2f, 1f, 1f),
            Make("traffic_tick", 0.08f, 0.97f, 1.03f),
            Make("traffic_good", 0.04f, 0.97f, 1.05f),
            Make("traffic_bad", 0.06f, 0.95f, 1.02f),
            Make("traffic_win", 0.3f, 1f, 1f),
            Make("traffic_lose", 0.3f, 1f, 1f),
        };
        foreach (string oceanId in PixelOceanSynth.Ids) list.Add(Make(oceanId, 0.05f, 0.95f, 1.05f));   // the bottle / treasure map / whale sounds
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
        AudioClip[] ocean = PixelOceanSynth.For(id);
        if (ocean != null) return ocean;
        AudioClip[] meteorSounds = PixelMeteorSynth.For(id);
        if (meteorSounds != null) return meteorSounds;
        AudioClip[] axe = PixelAxeSynth.For(id);
        if (axe != null) return axe;
        AudioClip[] hazard = PixelHazardSynth.For(id);
        if (hazard != null) return hazard;
        switch (id)
        {
            case "pixel_bounce_electric": return new[] { PixelSynth.Zap(1), PixelSynth.Zap(2), PixelSynth.Zap(3) };
            case "overcharge": return new[] { PixelSynth.Charge() };
            case "remove_buildup": return new[] { PixelSynth.Buildup() };
            case "dragon_summon": return new[] { PixelSynth.Summon() };
            case "seed_dig": return new[] { PixelSynth.Dig(1), PixelSynth.Dig(2), PixelSynth.Dig(3) };
            case "seed_plant": return new[] { PixelSynth.Crunch(1), PixelSynth.Crunch(2), PixelSynth.Crunch(3) };
            case "pixel_water": return new[] { PixelSynth.WaterClick(1), PixelSynth.WaterClick(2), PixelSynth.WaterClick(3), PixelSynth.WaterClick(4), PixelSynth.WaterClick(5), PixelSynth.WaterClick(6), PixelSynth.WaterClick(7), PixelSynth.WaterClick(8), PixelSynth.WaterClick(9), PixelSynth.WaterClick(10), PixelSynth.WaterClick(11), PixelSynth.WaterClick(12) };   // a pool: every click sounds different
            case "water_splash": return new[] { PixelSynth.Splash(1), PixelSynth.Splash(2), PixelSynth.Splash(3) };
            case "chest_hit": return new[] { PixelSynth.ChestHit(1), PixelSynth.ChestHit(2), PixelSynth.ChestHit(3) };
            case "chest_open": return new[] { PixelSynth.ChestOpen() };
            case "dragon_find": return new[] { PixelSynth.DragonFind() };
            case "dragon_stash": return new[] { PixelSynth.DragonStash() };
            case "fire_burst": return new[] { PixelSynth.FireBurst(1), PixelSynth.FireBurst(2), PixelSynth.FireBurst(3) };
            case "fire_crackle": return new[] { PixelSynth.FireCrackle(1), PixelSynth.FireCrackle(2), PixelSynth.FireCrackle(3) };
            case "ore_chip": return new[] { PixelSynth.OreChip(1), PixelSynth.OreChip(2), PixelSynth.OreChip(3), PixelSynth.OreChip(4) };
            case "ore_gleam": return new[] { PixelSynth.OreGleam() };
            case "pixel_ghost": return new[] { PixelSynth.GhostClick(1), PixelSynth.GhostClick(2), PixelSynth.GhostClick(3) };
            case "pixel_bounce_ghost": return new[] { PixelSynth.GhostBounce(1), PixelSynth.GhostBounce(2), PixelSynth.GhostBounce(3) };
            case "ghost_float": return new[] { PixelSynth.GhostFloat() };
            case "ufo_beam": return new[] { PixelSynth.UfoBeam() };
            case "pixel_vanish": return new[] { PixelSynth.Bloop(1), PixelSynth.Bloop(2), PixelSynth.Bloop(3) };
            case "talk_blip": return new[] { PixelSynth.TalkBlip(1), PixelSynth.TalkBlip(2), PixelSynth.TalkBlip(3), PixelSynth.TalkBlip(4), PixelSynth.TalkBlip(5), PixelSynth.TalkBlip(6) };
            case "stardust_fall": return new[] { PixelSynth.StarFall(1), PixelSynth.StarFall(2) };
            case "stardust_collect": return new[] { PixelSynth.StarChime() };
            case "pixel_wire": return new[] { PixelSynth.Elec(0, 1), PixelSynth.Elec(0, 2), PixelSynth.Elec(0, 3) };
            case "pixel_battery": return new[] { PixelSynth.Elec(1, 1), PixelSynth.Elec(1, 2), PixelSynth.Elec(1, 3) };
            case "pixel_resistor": return new[] { PixelSynth.Elec(3, 1), PixelSynth.Elec(3, 2), PixelSynth.Elec(3, 3) };
            case "pixel_transistor": return new[] { PixelSynth.Elec(4, 1), PixelSynth.Elec(4, 2), PixelSynth.Elec(4, 3) };
            case "pixel_fuse": return new[] { PixelSynth.Elec(5, 1), PixelSynth.Elec(5, 2) };
            case "pixel_bulb": return new[] { PixelSynth.Elec(6, 1), PixelSynth.Elec(6, 2), PixelSynth.Elec(6, 3) };
            case "pixel_capacitor": return new[] { PixelSynth.Elec(2, 1), PixelSynth.Elec(2, 2) };
            case "pixel_obsidian": return new[] { PixelSynth.ObsidianPing(1), PixelSynth.ObsidianPing(2) };
            case "chipper_feed": return new[] { PixelSynth.Chipper(1), PixelSynth.Chipper(2), PixelSynth.Chipper(3) };
            case "rack_store": return new[] { PixelSynth.Wood(0, 1), PixelSynth.Wood(4, 2) };
            case "fish_splash": return new[] { PixelSynth.Fishing(0, 1), PixelSynth.Fishing(0, 2), PixelSynth.Fishing(0, 3) };
            case "pixel_fish": return new[] { PixelSynth.Fishing(0, 4), PixelSynth.Fishing(0, 5), PixelSynth.Fishing(0, 6) };
            case "pixel_seaweed": return new[] { PixelSynth.Fishing(1, 1), PixelSynth.Fishing(1, 2), PixelSynth.Fishing(1, 3) };
            case "pixel_shell": return new[] { PixelSynth.Fishing(2, 1), PixelSynth.Fishing(2, 2), PixelSynth.Fishing(2, 3) };
            case "pixel_pearl": return new[] { PixelSynth.Fishing(3, 1), PixelSynth.Fishing(3, 2) };
            case "pixel_driftwood": return new[] { PixelSynth.Fishing(4, 1), PixelSynth.Fishing(4, 2), PixelSynth.Fishing(4, 3) };
            case "pixel_jellyfish": return new[] { PixelSynth.Zap(4), PixelSynth.Zap(5), PixelSynth.Zap(6) };
            case "jelly_sting": return new[] { PixelSynth.Zap(7), PixelSynth.Charge() };
            case "pixel_bait": return new[] { PixelSynth.Farm(4, 1), PixelSynth.Farm(4, 2), PixelSynth.Farm(4, 3) };
            case "forage_pick": return new[] { PixelSynth.Forage(2, 1), PixelSynth.Forage(2, 2), PixelSynth.Forage(2, 3) };
            case "pixel_berry": return new[] { PixelSynth.Forage(0, 1), PixelSynth.Forage(0, 2), PixelSynth.Forage(0, 3) };
            case "pixel_mushroom": return new[] { PixelSynth.Forage(1, 1), PixelSynth.Forage(1, 2), PixelSynth.Forage(1, 3) };
            case "pixel_herb": return new[] { PixelSynth.Forage(2, 1), PixelSynth.Forage(2, 2), PixelSynth.Forage(2, 3) };
            case "pixel_truffle": return new[] { PixelSynth.Forage(3, 1), PixelSynth.Forage(3, 2) };
            case "pixel_weeds": return new[] { PixelSynth.Forage(4, 1), PixelSynth.Forage(4, 2), PixelSynth.Forage(4, 3) };
            case "pixel_mold":
            case "pixel_toadstool": return new[] { PixelSynth.Forage(5, 1), PixelSynth.Forage(5, 2) };
            case "wood_chop": return new[] { PixelSynth.Wood(0, 1), PixelSynth.Wood(0, 2), PixelSynth.Wood(0, 3) };
            case "pixel_wood": return new[] { PixelSynth.Wood(0, 1), PixelSynth.Wood(0, 2), PixelSynth.Wood(0, 3) };
            case "pixel_bark": return new[] { PixelSynth.Wood(1, 1), PixelSynth.Wood(1, 2), PixelSynth.Wood(1, 3) };
            case "pixel_sap": return new[] { PixelSynth.Wood(2, 1), PixelSynth.Wood(2, 2), PixelSynth.Wood(2, 3) };
            case "pixel_leaf": return new[] { PixelSynth.Wood(3, 1), PixelSynth.Wood(3, 2), PixelSynth.Wood(3, 3) };
            case "pixel_heartwood": return new[] { PixelSynth.Wood(4, 1), PixelSynth.Wood(4, 2) };
            case "pixel_twig": return new[] { PixelSynth.Wood(5, 1), PixelSynth.Wood(5, 2), PixelSynth.Wood(5, 3) };
            case "pixel_hornetnest": return new[] { PixelSynth.Wood(6, 1), PixelSynth.Wood(6, 2) };
            case "farm_hit": return new[] { PixelSynth.Farm(0, 1), PixelSynth.Farm(0, 2), PixelSynth.Farm(0, 3) };
            case "farm_gleam": return new[] { PixelSynth.Farm(9, 1) };
            case "traffic_start": return new[] { PixelTrafficSynth.Start() };
            case "traffic_green": return new[] { PixelTrafficSynth.Green() };
            case "traffic_yellow": return new[] { PixelTrafficSynth.Yellow() };
            case "traffic_red": return new[] { PixelTrafficSynth.Red() };
            case "traffic_tick": return new[] { PixelTrafficSynth.Tick() };
            case "traffic_good": return new[] { PixelTrafficSynth.Good(1), PixelTrafficSynth.Good(2) };
            case "traffic_bad": return new[] { PixelTrafficSynth.Bad() };
            case "traffic_win": return new[] { PixelTrafficSynth.Win() };
            case "traffic_lose": return new[] { PixelTrafficSynth.Lose() };
            case "pixel_dirt": return new[] { PixelSynth.Farm(1, 1), PixelSynth.Farm(1, 2), PixelSynth.Farm(1, 3) };
            case "pixel_fertilizer": return new[] { PixelSynth.Farm(2, 1), PixelSynth.Farm(2, 2), PixelSynth.Farm(2, 3) };
            case "pixel_pollen": return new[] { PixelSynth.Farm(3, 1), PixelSynth.Farm(3, 2), PixelSynth.Farm(3, 3) };
            case "pixel_wheat": return new[] { PixelSynth.Farm(4, 1), PixelSynth.Farm(4, 2), PixelSynth.Farm(4, 3) };
            case "pixel_weevil": return new[] { PixelSynth.Farm(5, 1), PixelSynth.Farm(5, 2), PixelSynth.Farm(5, 3) };
            case "pixel_goldenfruit": return new[] { PixelSynth.Farm(6, 1), PixelSynth.Farm(6, 2) };
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
        if (!UiClicks && id == "ui_click") return;   // the UI click sounds can be switched off in Settings > Audio
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
        musicSource.volume = muted || musicMuted || previewTrack >= 0 ? 0f : musicVolume * masterVolume * musicTrackVolume;
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
                foreach (string part in PlayerPrefs.GetString(PrefPlaylistOff, "").Split('|')) if (part.Length > 0) playlistOff.Add(part);
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
        PlayerPrefs.SetString(PrefPlaylistOff, string.Join("|", PlaylistOff));
        return true;
    }

    /// <summary>Puts every song back in the playlist.</summary>
    public void EnableAllTracks()
    {
        PlaylistOff.Clear();
        PlayerPrefs.SetString(PrefPlaylistOff, "");
    }

    // ---- Track choice (the main menu's music selector) ----

    /// <summary>How many music tracks there are.</summary>
    public int MusicTrackCount => musicTracks != null ? musicTracks.Length : 0;

    /// <summary>A track's name for lists (the clip's name, underscores / dashes turned into spaces).</summary>
    public string MusicTrackName(int i)
    {
        if (musicTracks == null || i < 0 || i >= musicTracks.Length || musicTracks[i] == null) return "Track " + (i + 1);
        string n = musicTracks[i].name.Replace('_', ' ').Replace('-', ' ').Trim();
        return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : "Track " + (i + 1);
    }

    /// <summary>The index of the track playing now (-1 = none yet).</summary>
    public int CurrentMusicTrack => musicSource != null && musicSource.clip != null && musicTracks != null ? System.Array.IndexOf(musicTracks, musicSource.clip) : -1;

    /// <summary>Starts this track now from the beginning and remembers it as the chosen song of this save file; the normal shuffle / order carries on after it.</summary>
    public void SelectMusicTrack(int i)
    {
        if (i < 0 || musicTracks == null || i >= musicTracks.Length || musicTracks[i] == null) return;
        PlayerPrefs.SetString(PrefSong, TrackKey(i));
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

    /// <summary>The index of this save file's chosen song (-1 = none chosen, or it is not in the list any more).</summary>
    private int SavedSongIndex()
    {
        string name = PlayerPrefs.GetString(PrefSong, "");
        if (name.Length == 0 || musicTracks == null) return -1;
        for (int i = 0; i < musicTracks.Length; i++) if (musicTracks[i] != null && musicTracks[i].name == name) return i;
        return -1;
    }

    /// <summary>A save file was loaded: its chosen song starts playing (nothing changes when it has none or it is already playing).</summary>
    private void ApplySavedSong()
    {
        int i = SavedSongIndex();
        if (i >= 0 && i != CurrentMusicTrack) PlayTrack(i);
    }

    private void UpdateMusic()
    {
        if (musicSource == null) return;
        ApplyMusicVolume();
        UpdatePreview();

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
        int saved = lastTrack < 0 ? SavedSongIndex() : -1;
        if (saved >= 0) next = saved;   // the song this save file was left on
        else if ((shuffleMusic || lastTrack < 0) && pool.Count > 1)
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
            // Obsidian: the clicks climb in pitch, the breaking one is the highest.
            PlaySound("pixel_" + type.ToString().ToLowerInvariant(), type == PixelClicker.PixelType.Obsidian ? 1.6f : 1f);
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
            float pitch = type == PixelClicker.PixelType.Obsidian ? Mathf.Lerp(0.6f, 1.45f, ramp) : 1f;   // starts low, rises each click
            PlaySound("pixel_" + type.ToString().ToLowerInvariant(), pitch, Mathf.Max(0.1f, ramp * ramp));
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
    /// <summary>A small high bloop (a quick upward chirp with a soft overtone) for an old pixel vanishing. 'seed' picks the pitch.</summary>
    public static AudioClip Bloop(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.12f);
        float[] data = new float[n];
        float start = 1100f + seed * 140f, end = 2300f + seed * 220f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n, sec = i / (float)rate;
            float hz = Mathf.Lerp(start, end, 1f - Mathf.Pow(1f - t, 2.2f));
            phase += hz / rate;
            float env = Mathf.Clamp01(sec / 0.004f) * Mathf.Exp(-sec * 30f);
            data[i] = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.25f * Mathf.Sin(phase * 2f * Mathf.PI * 2f)) * env * 0.6f;
        }
        AudioClip clip = AudioClip.Create("Bloop " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

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

    /// <summary>One speech blip for dialogue boxes: a short voiced squeak (a few harmonics of a falling pitch under a soft envelope), about 70 ms. 'variant' picks the base pitch.</summary>
    public static AudioClip TalkBlip(int variant)
    {
        const int rate = 22050;
        const float length = 0.075f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        float baseHz = 190f + variant * 26f, phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate, t = i / (float)n;
            float hz = baseHz * (1.12f - 0.16f * t) * (1f + 0.018f * Mathf.Sin(sec * 70f));   // a little downward glide and wobble
            phase += hz / rate;
            phase -= Mathf.Floor(phase);
            float w = Mathf.Sin(phase * 6.2832f) + 0.5f * Mathf.Sin(phase * 12.566f) + 0.28f * Mathf.Sin(phase * 18.85f) + 0.12f * Mathf.Sin(phase * 25.13f);
            lp += (w - lp) * 0.35f;   // soften the buzz
            float env = Mathf.Clamp01(sec / 0.006f) * Mathf.Exp(-sec * 26f) * (1f - t * t);
            data[i] = lp * env;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.5f;
        AudioClip clip = AudioClip.Create("TalkBlip " + variant, n, 1, rate, false);
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
        float[] bloopHz = { 230f + seed * 30f, 310f + seed * 25f, 400f };
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.07f;
            float hiss = lp * 4f * Mathf.Exp(-sec * 11f);              // a soft, low wash instead of a bright hiss
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

    /// <summary>A short splashy click for a Water pixel: a quick droplet "plip" with a little splash hiss around it. 'seed' picks a variant.</summary>
    /// <summary>Farming sounds. kind: 0 soft hit, 1 dirt thud, 2 wet squelch, 3 airy puff, 4 dry rustle, 5 squeaky crunch, 6 golden chime, 9 gleam.</summary>
    public static AudioClip Farm(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 6 ? 0.55f : kind == 9 ? 0.9f : 0.22f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(1300 + kind * 101 + seed * 17);
        float lp = 0f;
        float baseHz = 180f + seed * 25f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: lp += (noise - lp) * 0.3f; v = lp * Mathf.Exp(-t * 40f) + Mathf.Sin(t * 260f * Mathf.PI * 2f) * Mathf.Exp(-t * 35f) * 0.5f; break;
                case 1: lp += (noise - lp) * 0.12f; v = lp * Mathf.Exp(-t * 22f) * 1.4f + Mathf.Sin(t * baseHz * 0.5f * Mathf.PI * 2f) * Mathf.Exp(-t * 28f); break;
                case 2: lp += (noise - lp) * 0.18f; v = lp * Mathf.Exp(-t * 14f) + Mathf.Sin(t * (140f + 90f * Mathf.Sin(t * 30f)) * Mathf.PI * 2f) * Mathf.Exp(-t * 16f) * 0.7f; break;
                case 3: lp += (noise - lp) * 0.5f; v = (noise - lp) * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * 0.6f; break;
                case 4: v = (rng.NextDouble() < 0.12 ? noise : 0f) * Mathf.Exp(-t * 12f) + (noise - lp) * 0.2f * Mathf.Exp(-t * 18f); lp += (noise - lp) * 0.4f; break;
                case 5: v = Mathf.Sin(t * (1400f + seed * 200f) * (1f + t * 6f) * Mathf.PI * 2f) * Mathf.Exp(-t * 30f) * 0.5f + noise * Mathf.Exp(-t * 45f) * 0.5f; break;
                case 6:
                    {
                        float f = 880f + seed * 220f;
                        v = (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * f * 2.01f * Mathf.PI * 2f) + 0.25f * Mathf.Sin(t * f * 3.02f * Mathf.PI * 2f)) * Mathf.Exp(-t * 7f) * 0.5f;
                        break;
                    }
                default:
                    {
                        float f1 = 1046f, f2 = 1568f, f3 = 2093f;
                        v = Mathf.Sin(t * f1 * Mathf.PI * 2f) * Mathf.Exp(-t * 4f) + (t > 0.12f ? Mathf.Sin((t - 0.12f) * f2 * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.12f) * 4f) : 0f)
                            + (t > 0.24f ? Mathf.Sin((t - 0.24f) * f3 * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.24f) * 4f) : 0f);
                        v *= 0.4f;
                        break;
                    }
            }
            data[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Farm " + kind + "-" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A glassy, ringing tap of black volcanic glass; the game pitches it up with every click.</summary>
    public static AudioClip ObsidianPing(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.45f);
        float[] data = new float[n];
        System.Random rng = new System.Random(2100 + seed * 31);
        float f = 380f + seed * 40f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float ring = Mathf.Sin(t * f * Mathf.PI * 2f) * Mathf.Exp(-t * 7f)
                       + 0.55f * Mathf.Sin(t * f * 2.76f * Mathf.PI * 2f) * Mathf.Exp(-t * 11f)
                       + 0.3f * Mathf.Sin(t * f * 5.4f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f);
            data[i] = (ring + noise * Mathf.Exp(-t * 90f) * 0.8f) * Mathf.Clamp01(t / 0.001f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Obsidian " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// The Electrician pixels' own click sounds: 0 Wire (a metallic twang), 1 Battery (a low thunk with a hum), 2 Capacitor (a rising charge whine ending in a snap),
    /// 3 Resistor (a dull buzz), 4 Transistor (a two-tone digital blip), 5 Fuse (a hiss that ends in a pop), 6 Bulb (a glassy ting).
    /// </summary>
    public static AudioClip Elec(int kind, int seed)
    {
        const int rate = 22050;
        float[] lengths = { 0.28f, 0.38f, 0.5f, 0.3f, 0.2f, 0.5f, 0.55f };
        int n = (int)(rate * lengths[Mathf.Clamp(kind, 0, 6)]);
        float[] data = new float[n];
        System.Random rng = new System.Random(3300 + kind * 131 + seed * 19);
        float detune = 1f + 0.06f * (seed - 2);
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0:   // Wire: frequency-modulated twang that dips
                {
                    float f = 880f * detune;
                    v = Mathf.Sin(t * f * Mathf.PI * 2f + 4f * Mathf.Sin(t * f * 1.5f * Mathf.PI * 2f) * Mathf.Exp(-t * 18f)) * Mathf.Exp(-t * 15f);
                    v += noise * Mathf.Exp(-t * 90f) * 0.3f;
                    break;
                }
                case 1:   // Battery: a deep thunk and a settling hum
                {
                    float f = (120f - 50f * Mathf.Min(1f, t / 0.12f)) * detune;
                    phase += f / rate;
                    v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 9f) + 0.35f * Mathf.Sin(t * 55f * Mathf.PI * 2f) * Mathf.Exp(-t * 5f);
                    v += noise * Mathf.Exp(-t * 110f) * 0.4f;
                    break;
                }
                case 2:   // Capacitor: charge sweep up, then a snap
                {
                    float charge = 0.32f;
                    if (t < charge)
                    {
                        float k = t / charge;
                        phase += (350f + 2300f * k * k) * detune / rate;
                        v = Mathf.Sin(phase * Mathf.PI * 2f) * (0.2f + 0.6f * k);
                    }
                    else
                    {
                        float d = t - charge;
                        lp += (noise - lp) * 0.5f;
                        v = lp * Mathf.Exp(-d * 35f) * 1.3f + Mathf.Sin(d * 170f * Mathf.PI * 2f) * Mathf.Exp(-d * 30f);
                    }
                    break;
                }
                case 3:   // Resistor: a rough 100 Hz buzz
                {
                    float saw = 2f * Mathf.Repeat(t * 105f * detune, 1f) - 1f;
                    lp += (noise - lp) * 0.25f;
                    v = saw * Mathf.Exp(-t * 11f) * 0.55f + lp * Mathf.Exp(-t * 16f) * 0.6f;
                    break;
                }
                case 4:   // Transistor: two quick square blips
                {
                    float f = (t < 0.085f ? 1250f : 1870f) * detune;
                    float local = t < 0.085f ? t : t - 0.085f;
                    float sq = Mathf.Sin(t * f * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                    v = sq * 0.45f * Mathf.Exp(-local * 22f);
                    break;
                }
                case 5:   // Fuse: a hiss that thins out, then a pop
                {
                    float pop = 0.36f;
                    if (t < pop)
                    {
                        lp += (noise - lp) * 0.8f;
                        v = (noise - lp) * (0.25f + 0.2f * Mathf.Sin(t * 60f)) * (1f - t / pop * 0.5f);   // high-passed noise
                    }
                    else
                    {
                        float d = t - pop;
                        v = Mathf.Sin(d * 190f * detune * Mathf.PI * 2f) * Mathf.Exp(-d * 45f) + noise * Mathf.Exp(-d * 70f) * 0.7f;
                    }
                    break;
                }
                default:  // Bulb: a glassy ting
                {
                    float f = 2200f * detune;
                    v = (Mathf.Sin(t * f * Mathf.PI * 2f) + 0.5f * Mathf.Sin(t * f * 1.5f * Mathf.PI * 2f) + 0.3f * Mathf.Sin(t * f * 2.3f * Mathf.PI * 2f)) * Mathf.Exp(-t * 8f);
                    v += noise * Mathf.Exp(-t * 120f) * 0.35f;
                    break;
                }
            }
            data[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Elec " + kind + "-" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// A deep, smooth water sound for a Water pixel: one to three low bubbles that climb a little in pitch, over a soft, heavily filtered wash.
    /// Every 'seed' rolls its own pitch, bubble count, timing and length, so a pool of them never sounds the same twice.
    /// </summary>
    public static AudioClip WaterClick(int seed)
    {
        const int rate = 22050;
        System.Random rng = new System.Random(900 + seed * 37);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float length = R(0.28f, 0.42f);
        int n = (int)(rate * length);
        float[] data = new float[n];

        int bubbles = 1 + rng.Next(0, 3);
        float[] at = new float[bubbles], hz = new float[bubbles], amp = new float[bubbles], rise = new float[bubbles], decay = new float[bubbles];
        float t0 = 0f;
        for (int k = 0; k < bubbles; k++)
        {
            at[k] = t0;
            hz[k] = R(130f, 330f) * (k == 0 ? 1f : R(0.9f, 1.5f));
            amp[k] = k == 0 ? 1f : R(0.35f, 0.7f);
            rise[k] = R(2f, 6f);
            decay[k] = R(8f, 15f);
            t0 += R(0.05f, 0.11f);
        }
        float washDecay = R(6f, 11f), washAmount = R(0.2f, 0.45f);
        float lp1 = 0f, lp2 = 0f, lp3 = 0f, smooth = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp1 += (noise - lp1) * 0.05f;
            lp2 += (lp1 - lp2) * 0.1f;
            lp3 += (lp2 - lp3) * 0.2f;
            float wash = lp3 * 7f * Mathf.Exp(-sec * washDecay) * Mathf.Clamp01(sec / 0.02f) * washAmount;   // a soft, low rumble of moving water
            float body = 0f;
            for (int k = 0; k < bubbles; k++)
            {
                float t = sec - at[k];
                if (t < 0f) continue;
                float phase = hz[k] * (t + rise[k] * t * t * 0.5f) * Mathf.PI * 2f;                 // pitch climbs as the bubble rises
                float env = Mathf.Exp(-t * decay[k]) * Mathf.Clamp01(t / 0.008f);
                body += (Mathf.Sin(phase) + 0.18f * Mathf.Sin(phase * 2f)) * env * amp[k];
            }
            float raw = wash + body * 0.7f;
            smooth += (raw - smooth) * 0.55f;   // one more gentle low-pass: no sharp edges
            data[i] = smooth;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.6f;
        AudioClip clip = AudioClip.Create("Water Click " + seed, n, 1, rate, false);
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

    /// <summary>The woodchipper taking a block in: a motor whine that revs up, a grinding crunch of chips, then a rattle as they spit out.</summary>
    public static AudioClip Chipper(int seed)
    {
        const int rate = 22050;
        float len = 0.7f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(3300 + seed * 31);
        float phase = 0f, lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 3.2f);
            phase += (150f + 160f * Mathf.Min(1f, t * 3f) + 12f * seed) / rate;
            float motor = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.5f * Mathf.Sin(phase * 3f * Mathf.PI * 2f)) * 0.3f;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.22f;
            lp2 += (lp - lp2) * 0.5f;
            float crunch = (rng.NextDouble() < 0.28 ? noise : lp2) * (0.6f + 0.4f * Mathf.Sin(t * 70f)) * Mathf.Exp(-t * 5f);
            data[i] = (motor * 0.8f + crunch * 0.9f) * env;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.75f;
        AudioClip clip = AudioClip.Create("Chipper " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Fishing sounds. kind: 0 fish splash + flop, 1 seaweed slap, 2 shell clack, 3 pearl chime, 4 driftwood knock, 5 jellyfish squelch + zap.</summary>
    public static AudioClip Fishing(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 3 ? 0.55f : kind == 5 ? 0.4f : 0.25f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(3400 + kind * 83 + seed * 17);
        float lp = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: // a splash of water then a wet flop
                    lp += (noise - lp) * 0.3f;
                    v = (noise - lp) * Mathf.Exp(-t * 14f) * 0.8f + Mathf.Sin(t * (140f + seed * 15f) * Mathf.PI * 2f) * Mathf.Exp(-t * 18f) * 0.6f * (t > 0.05f ? 1f : 0f);
                    break;
                case 1: // a wet slap
                    lp += (noise - lp) * 0.2f;
                    v = lp * Mathf.Exp(-t * 20f) * 1.4f + Mathf.Sin(t * (110f + seed * 10f) * Mathf.PI * 2f) * Mathf.Exp(-t * 26f) * 0.5f;
                    break;
                case 2: // a hard clack
                    v = Mathf.Sin(t * (1400f + seed * 160f) * Mathf.PI * 2f) * Mathf.Exp(-t * 60f) + noise * Mathf.Exp(-t * 90f) * 0.5f;
                    break;
                case 3: // a soft pearly chime
                    phase += (1040f + seed * 130f) / rate;
                    v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.7f + Mathf.Sin(phase * Mathf.PI * 3f) * Mathf.Exp(-t * 9f) * 0.25f + noise * Mathf.Exp(-t * 80f) * 0.2f;
                    break;
                case 4: // a dry wooden knock
                    v = Mathf.Sin(t * (190f + seed * 25f) * Mathf.PI * 2f) * Mathf.Exp(-t * 35f) + noise * Mathf.Exp(-t * 70f) * 0.4f;
                    break;
                default: // a squelch with an electric crackle
                    lp += (noise - lp) * 0.15f;
                    v = lp * Mathf.Exp(-t * 9f) + (rng.NextDouble() < 0.1 ? noise : 0f) * Mathf.Exp(-t * 7f) * 0.8f;
                    break;
            }
            data[i] = v * Mathf.Clamp01(t / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Fishing " + kind + "_" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Foraging sounds. kind: 0 berry squish, 1 mushroom pop, 2 herb rustle, 3 truffle dig + chime, 4 weed rip, 5 toadstool hiss.</summary>
    public static AudioClip Forage(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 3 ? 0.45f : kind == 5 ? 0.5f : 0.2f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(2900 + kind * 71 + seed * 13);
        float lp = 0f, lp2 = 0f, phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: // a wet squish
                    lp += (noise - lp) * 0.14f;
                    v = lp * Mathf.Exp(-t * 16f) * 1.2f + Mathf.Sin(t * (200f - 80f * t * 6f + seed * 20f) * Mathf.PI * 2f) * Mathf.Exp(-t * 22f) * 0.7f;
                    break;
                case 1: // a soft pop with a puff of air
                    lp += (noise - lp) * 0.35f;
                    v = Mathf.Sin(t * (320f + seed * 30f) * Mathf.PI * 2f) * Mathf.Exp(-t * 45f) + lp * Mathf.Exp(-t * 14f) * 0.5f;
                    break;
                case 2: // a leafy rustle
                    lp += (noise - lp) * 0.5f;
                    v = (noise - lp) * Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI) * 0.6f;
                    break;
                case 3: // digging up something precious: a scrape then a small bright chime
                    lp += (noise - lp) * 0.12f;
                    phase += (880f + seed * 110f) / rate;
                    v = lp * Mathf.Exp(-t * 18f) * 1.2f + (t > 0.06f ? Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-(t - 0.06f) * 9f) * 0.45f : 0f);
                    break;
                case 4: // tearing out a weed: a short ripping crackle
                    v = (rng.NextDouble() < 0.3 ? noise : 0f) * Mathf.Exp(-t * 20f) + (noise - lp) * 0.2f * Mathf.Exp(-t * 28f);
                    lp += (noise - lp) * 0.4f;
                    break;
                default: // a poisonous hiss
                    lp += (noise - lp) * 0.6f;
                    lp2 += (lp - lp2) * 0.6f;
                    v = (noise - lp2) * Mathf.Exp(-t * 6f) * 0.7f;
                    break;
            }
            data[i] = v * Mathf.Clamp01(t / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Forage " + kind + "_" + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Woodcutting sounds. kind: 0 chop, 1 bark crack, 2 sap squelch, 3 leaf rustle, 4 heartwood knock + ring, 5 twig snap, 6 hornet buzz.</summary>
    public static AudioClip Wood(int kind, int seed)
    {
        const int rate = 22050;
        float len = kind == 4 ? 0.5f : kind == 6 ? 0.45f : 0.22f;
        int n = (int)(rate * len);
        float[] data = new float[n];
        System.Random rng = new System.Random(2100 + kind * 97 + seed * 19);
        float lp = 0f, lp2 = 0f, phase = 0f;
        float knockHz = 150f + seed * 22f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float v = 0f;
            switch (kind)
            {
                case 0: // a woody chop: a low thock with a short crack on top
                    lp += (noise - lp) * 0.25f;
                    v = Mathf.Sin(t * knockHz * Mathf.PI * 2f) * Mathf.Exp(-t * 28f) + lp * Mathf.Exp(-t * 55f) * 0.9f;
                    break;
                case 1: // bark: a dry crackle
                    v = (rng.NextDouble() < 0.18 ? noise : 0f) * Mathf.Exp(-t * 16f) + Mathf.Sin(t * 110f * Mathf.PI * 2f) * Mathf.Exp(-t * 30f) * 0.6f;
                    break;
                case 2: // sap: a wet squelch
                    lp += (noise - lp) * 0.15f;
                    v = lp * Mathf.Exp(-t * 14f) + Mathf.Sin(t * (170f + 120f * Mathf.Sin(t * 26f)) * Mathf.PI * 2f) * Mathf.Exp(-t * 15f) * 0.7f;
                    break;
                case 3: // leaf: a soft rustle
                    lp += (noise - lp) * 0.5f;
                    v = (noise - lp) * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * 0.55f;
                    break;
                case 4: // heartwood: a deep knock with a ringing tail
                    phase += (210f + seed * 18f) / rate;
                    v = Mathf.Sin(t * 95f * Mathf.PI * 2f) * Mathf.Exp(-t * 14f) + Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.5f;
                    break;
                case 5: // twig: a sharp snap
                    lp += (noise - lp) * 0.6f;
                    v = (noise - lp) * Mathf.Exp(-t * 70f) + Mathf.Sin(t * (1800f + seed * 220f) * Mathf.PI * 2f) * Mathf.Exp(-t * 90f) * 0.4f;
                    break;
                default: // hornets: an angry buzz
                    phase += (230f + 40f * Mathf.Sin(t * 40f)) / rate;
                    lp += (noise - lp) * 0.3f;
                    v = (Mathf.Sin(phase * Mathf.PI * 2f) * 0.6f + Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2f)) * 0.2f + lp * 0.25f) * Mathf.Exp(-t * 5f) * (0.7f + 0.3f * Mathf.Sin(t * 90f));
                    break;
            }
            lp2 += (v - lp2) * 0.7f;
            data[i] = lp2 * Mathf.Clamp01(t / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("Wood " + kind + "_" + seed, n, 1, rate, false);
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

    /// <summary>The removal buildup: a smooth tone that climbs from low to high with a growing shimmer, 0.8 s long (PixelHold plays it with its pitch scaled so it lasts as long as the hold).</summary>
    public const float BuildupSeconds = 0.8f;

    public static AudioClip Buildup()
    {
        const int rate = 44100;
        int n = (int)(rate * BuildupSeconds);
        float[] data = new float[n];
        System.Random rng = new System.Random(77);
        float phase = 0f, phase2 = 0f, noise = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float sec = i / (float)rate;
            float hz = Mathf.Lerp(150f, 1250f, t * t);
            phase += hz / rate;
            phase2 += hz * 1.5f / rate;
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) * 0.6f + Mathf.Sin(phase2 * Mathf.PI * 2f) * 0.22f * t;
            float tremolo = 1f - 0.25f * Mathf.Sin(sec * Mathf.Lerp(10f, 34f, t) * Mathf.PI * 2f) * t;
            noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, 0.35f);
            data[i] = (tone * tremolo + noise * 0.12f * t) * Mathf.Lerp(0.2f, 1f, t) * Mathf.Clamp01(sec / 0.03f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create("RemoveBuildup", n, 1, rate, false);
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

    /// <summary>A knock on a wooden chest: a short dull thump with a woody click and a tiny coin chime. 'seed' picks a variant.</summary>
    public static AudioClip ChestHit(int seed)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.35f);
        float[] data = new float[n];
        System.Random rng = new System.Random(900 + seed * 17);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.3f;
            float thump = Mathf.Sin(sec * (120f + seed * 12f) * 6.2832f) * Mathf.Exp(-sec * 22f);
            float knock = lp * Mathf.Exp(-sec * 60f) * 0.8f;
            float chime = (Mathf.Sin(sec * (2200f + seed * 180f) * 6.2832f) + 0.5f * Mathf.Sin(sec * 3300f * 6.2832f)) * Mathf.Exp(-sec * 18f) * 0.25f * Mathf.Clamp01((sec - 0.01f) / 0.004f);
            data[i] = thump + knock + chime;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("ChestHit " + seed, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A chest bursts open: a creaking lid, then a shower of coins jingling (about 1.6 seconds).</summary>
    public static AudioClip ChestOpen()
    {
        const int rate = 22050;
        int n = (int)(rate * 1.6f);
        float[] data = new float[n];
        System.Random rng = new System.Random(321);
        float phase = 0f;
        float[] pingAt = new float[30], pingHz = new float[30];
        for (int k = 0; k < pingAt.Length; k++) { pingAt[k] = 0.3f + (float)rng.NextDouble() * 1.0f; pingHz[k] = 1500f + (float)rng.NextDouble() * 2600f; }
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            // The creak: a rough saw that glides up while it wobbles, over the first 0.35 s.
            float v = 0f;
            if (sec < 0.4f)
            {
                float f = Mathf.Lerp(80f, 210f, sec / 0.4f) * (1f + 0.15f * Mathf.Sin(sec * 60f));
                phase += f / rate;
                float saw = (phase % 1f) * 2f - 1f;
                v += saw * 0.35f * Mathf.Sin(Mathf.Clamp01(sec / 0.4f) * Mathf.PI);
            }
            // A thump as it opens, then coins.
            float boomT = sec - 0.3f;
            if (boomT > 0f) v += Mathf.Sin(boomT * 70f * 6.2832f) * Mathf.Exp(-boomT * 9f) * 0.8f;
            for (int k = 0; k < pingAt.Length; k++)
            {
                float t = sec - pingAt[k];
                if (t < 0f) continue;
                v += Mathf.Sin(t * pingHz[k] * 6.2832f) * Mathf.Exp(-t * 14f) * 0.16f * Mathf.Clamp01(t / 0.003f);
            }
            data[i] = v;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
        AudioClip clip = AudioClip.Create("ChestOpen", n, 1, rate, false);
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

    private static void Normalize(float[] data, float level)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * level;
    }

    /// <summary>The Ghost pixel's click: a wet gloopy "bloop" (a pitch that drops then wobbles up, a second bubble pop) with a soft airy breath after it.</summary>
    public static AudioClip GhostClick(int variant)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.5f);
        float[] data = new float[n];
        System.Random rnd = new System.Random(300 + variant * 17);
        float baseFreq = 170f + variant * 38f;
        float phase = 0f, phase2 = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            // Bloop: the pitch starts high, drops fast and then rises a touch, like a bubble closing.
            float f = baseFreq * (1f + 1.1f * Mathf.Exp(-t * 18f) + 0.18f * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI));
            phase += f / rate;
            float v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 9f);
            // A second smaller bubble.
            float t2 = t - 0.085f;
            if (t2 > 0f)
            {
                phase2 += baseFreq * 2.3f * (1f + 0.5f * Mathf.Exp(-t2 * 22f)) / rate;
                v += Mathf.Sin(phase2 * Mathf.PI * 2f) * Mathf.Exp(-t2 * 20f) * 0.45f;
            }
            // Wet squelch: low-passed noise right at the start.
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.22f;
            v += lp * Mathf.Exp(-t * 34f) * 0.5f;
            // The ghost's breath: two soft detuned tones with a slow wobble.
            float breath = (Mathf.Sin(t * 520f * Mathf.PI * 2f + 2f * Mathf.Sin(t * 6f)) + 0.6f * Mathf.Sin(t * 781f * Mathf.PI * 2f)) * Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.05f) * 0.13f;
            data[i] = (v + breath) * Mathf.Clamp01(t / 0.002f);
        }
        Normalize(data, 0.55f);
        AudioClip clip = AudioClip.Create("GhostClick" + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A soft squishy landing: a low "splut" dropping in pitch with a puff of wet noise.</summary>
    public static AudioClip GhostBounce(int variant)
    {
        const int rate = 22050;
        int n = (int)(rate * 0.26f);
        float[] data = new float[n];
        System.Random rnd = new System.Random(500 + variant * 13);
        float phase = 0f, lp = 0f;
        float baseFreq = 70f + variant * 14f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            phase += baseFreq * (1f + 1.6f * Mathf.Exp(-t * 26f)) / rate;
            float v = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 15f);
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * 0.16f;
            v += lp * Mathf.Exp(-t * 40f) * 0.55f;
            float t2 = t - 0.045f;
            if (t2 > 0f) v += Mathf.Sin(t2 * (360f + variant * 60f) * Mathf.PI * 2f) * Mathf.Exp(-t2 * 38f) * 0.22f;   // a tiny bubble
            data[i] = v * Mathf.Clamp01(t / 0.002f);
        }
        Normalize(data, 0.5f);
        AudioClip clip = AudioClip.Create("GhostBounce" + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The ghost starts to float away: a hollow, breathy "whoo" that rises and fades.</summary>
    public static AudioClip GhostFloat()
    {
        const int rate = 22050;
        float length = 1.1f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rnd = new System.Random(777);
        float phase = 0f, lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float k = t / length;
            float env = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            env *= env;
            float f = Mathf.Lerp(300f, 520f, k) * (1f + 0.025f * Mathf.Sin(t * 9f));
            phase += f / rate;
            float tone = Mathf.Sin(phase * Mathf.PI * 2f) + 0.35f * Mathf.Sin(phase * 2f * Mathf.PI * 2f);
            // Breath: noise run through two one-pole low-passes whose cutoff rises with the sound.
            float coef = Mathf.Lerp(0.05f, 0.18f, k);
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * coef;
            lp2 += (lp - lp2) * coef;
            data[i] = (tone * 0.35f + lp2 * 2.2f) * env;
        }
        Normalize(data, 0.45f);
        AudioClip clip = AudioClip.Create("GhostFloat", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>The UFO's abduction beam: a rising, wobbling hum with a shimmer on top that sweeps up, then a soft pop as the victim arrives (about 2.6 s).</summary>
    public static AudioClip UfoBeam()
    {
        const int rate = 22050;
        float length = 2.7f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rnd = new System.Random(4242);
        float phase = 0f, phase2 = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float k = Mathf.Clamp01(t / 2.4f);
            float env = Mathf.Clamp01(t / 0.12f) * (t < 2.4f ? 1f : 0f);
            // The hum climbs while a vibrato widens; a second, detuned voice beats against it.
            float f = Mathf.Lerp(140f, 520f, k * k) * (1f + 0.06f * Mathf.Sin(t * (7f + 22f * k)));
            phase += f / rate;
            phase2 += f * 1.012f / rate;
            float v = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.8f * Mathf.Sin(phase2 * Mathf.PI * 2f)) * 0.3f;
            v += Mathf.Sin(phase * 3f * Mathf.PI * 2f) * 0.07f;
            // A glassy shimmer: filtered noise whose level pulses faster and louder towards the top.
            lp += (((float)rnd.NextDouble() * 2f - 1f) - lp) * Mathf.Lerp(0.1f, 0.4f, k);
            v += lp * (0.18f + 0.3f * k) * (0.6f + 0.4f * Mathf.Sin(t * (18f + 30f * k)));
            data[i] = v * env;
            // The pop when it arrives.
            float tp = t - 2.42f;
            if (tp > 0f) data[i] += Mathf.Sin(tp * 640f * Mathf.PI * 2f * (1f - tp * 2.2f)) * Mathf.Exp(-tp * 26f) * 0.9f;
        }
        Normalize(data, 0.5f);
        AudioClip clip = AudioClip.Create("UfoBeam", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A star falling: a glassy whistle that slides down while tiny sparkles tinkle behind it. 'variant' changes the pitch and the sparkles.</summary>
    public static AudioClip StarFall(int variant)
    {
        const int rate = 22050;
        float length = 1.25f;
        int n = (int)(rate * length);
        float[] data = new float[n];
        System.Random rnd = new System.Random(900 + variant * 31);
        float top = variant == 1 ? 3300f : 3700f, bottom = variant == 1 ? 760f : 840f;
        float phase = 0f;
        // Sparkle ticks: short high pings scattered through the fall, getting sparser.
        int pings = 16;
        float[] pingAt = new float[pings], pingFreq = new float[pings], pingAmp = new float[pings];
        for (int k = 0; k < pings; k++)
        {
            pingAt[k] = Mathf.Pow((float)rnd.NextDouble(), 0.8f) * length * 0.95f;
            pingFreq[k] = 2800f + (float)rnd.NextDouble() * 4200f;
            pingAmp[k] = 0.1f + (float)rnd.NextDouble() * 0.16f;
        }
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float k01 = sec / length;
            float freq = bottom + (top - bottom) * Mathf.Exp(-sec * 3.2f);
            freq *= 1f + 0.012f * Mathf.Sin(sec * 38f);
            phase += freq / rate;
            float env = Mathf.Clamp01(sec / 0.015f) * Mathf.Exp(-sec * 1.9f) * Mathf.Clamp01((length - sec) / 0.25f);
            float v = (Mathf.Sin(phase * Mathf.PI * 2f) + 0.25f * Mathf.Sin(phase * 2f * Mathf.PI * 2f)) * env * 0.5f;
            for (int p = 0; p < pings; p++)
            {
                float t = sec - pingAt[p];
                if (t < 0f || t > 0.25f) continue;
                v += Mathf.Sin(t * pingFreq[p] * Mathf.PI * 2f) * Mathf.Exp(-t * 28f) * pingAmp[p] * (1f - k01 * 0.6f);
            }
            data[i] = v;
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.55f;
        AudioClip clip = AudioClip.Create("StarFall" + variant, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A short rising three-note chime for collecting a fallen star.</summary>
    public static AudioClip StarChime()
    {
        const int rate = 22050;
        int n = (int)(rate * 0.7f);
        float[] data = new float[n];
        float[] notes = { 1046.5f, 1568f, 2093f };   // C6, G6, C7
        for (int i = 0; i < n; i++)
        {
            float sec = i / (float)rate;
            float v = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float t = sec - k * 0.07f;
                if (t < 0f) continue;
                v += (Mathf.Sin(t * notes[k] * Mathf.PI * 2f) + 0.2f * Mathf.Sin(t * notes[k] * 2.01f * Mathf.PI * 2f)) * Mathf.Exp(-t * 6f) * 0.5f;
            }
            data[i] = v * Mathf.Clamp01(sec / 0.003f);
        }
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.55f;
        AudioClip clip = AudioClip.Create("StarChime", n, 1, rate, false);
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
