using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Save / load for Pixel Clicker.
///
/// Saves to a JSON file in Application.persistentDataPath: every pixel amount, lifetime total and
/// unlock, the shop purchases and upgrade levels, the auto clicker state, and the potions you own.
/// (A potion that is currently active is not saved - it simply ends.)
///
/// - Loads the save automatically when the game starts (optional), and pays offline progress (PixelOfflineProgress).
/// - Saves automatically every N seconds, when the game closes and when the app loses focus (optional).
/// - The pause menu gets Save / Load buttons when this component exists.
/// - Other scripts can call Save(), Load() or DeleteSave().
///
/// Add it to any GameObject (e.g. the cube). The pause menu adds one for you if it is missing.
/// </summary>
public class PixelSaveGame : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Save data (what ends up in the JSON file)
    // ------------------------------------------------------------------


    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public string savedAt;
        public string slotName = "";   // the save's own name (Save / Load window > Rename): lives in the file, so it travels with it
        public long savedAtTicks;   // UTC, used for offline progress
        public double playSeconds;  // time played, for the slot list's summary (the stats part keeps the real one)
        public double totalPixels;  // everything ever collected, for the slot list's summary (the GameState keeps the real numbers)
        public GameState state;   // the player's progress for the systems moved to GameState (PixelGameState; GameState.MovedSystems lists them)
        public PixelFeatureTester.TesterSave[] testerObjects;   // the Dev Hub tester machines (Wood Chipper, belts + their riders, Kiln, Solar Panels)
        public PixelClicker.OldPixelState[] oldPixels;   // where the old pixels lie
        public PixelSettingsSync.Entry[] settings;       // the settings / dev tool choices of this save file
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    [Header("References (found automatically if empty)")]
    [Tooltip("The PixelClicker whose pixels are saved.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The shop whose purchases and upgrade levels are saved.")]
    [SerializeField] private PixelShop shop;








    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("File")]
    [Tooltip("Name of the save file of slot 1 (so an old single save keeps working). Other slots add _slot2, _slot3... before the extension. They live in Application.persistentDataPath (see the Console when the game starts).")]
    [SerializeField] private string saveFileName = "pixelclicker_save.json";

    [Range(1, 12)]
    [Tooltip("How many save slots the Save / Load window offers.")]
    [SerializeField] private int slotCount = 5;

    [Header("When to save / load")]
    [Tooltip("Load the save when the game starts.")]
    [SerializeField] private bool loadOnStart = true;

    [Tooltip("Save every this many seconds while playing. 0 = no autosave.")]
    [Min(0f)]
    [SerializeField] private float autoSaveSeconds = 30f;

    [Tooltip("Keep a list of the autosaves (the pause menu's Save / Load window has an 'Autosaves' button that opens it, for testing). Every timer autosave is also copied into the Autosaves folder next to the save files.")]
    [SerializeField] private bool keepAutosaveHistory = true;

    [Min(1)]
    [Tooltip("How many autosaves the list keeps. When it is full, the OLDEST are deleted to make room for newer ones.")]
    [SerializeField] private int autosaveHistorySize = 100;

    [Tooltip("Save when the game closes (and when you stop Play mode in the Editor).")]
    [SerializeField] private bool saveOnQuit = true;

    [Tooltip("Save when the app loses focus or is minimised (mobile / some builds).")]
    [SerializeField] private bool saveWhenAppPauses = true;

    [Header("Messages")]
    [Tooltip("Show a short message on screen after saving or loading.")]
    [SerializeField] private bool showMessages = true;

    [Tooltip("Show a message after every autosave too (off = only manual saves).")]
    [SerializeField] private bool messageOnAutoSave = false;

    [Tooltip("How long a message stays on screen (seconds).")]
    [SerializeField] private float messageSeconds = 1.8f;

    [Tooltip("Message text size.")]
    [SerializeField] private float messageFontSize = 40f;

    [Tooltip("Message text colour.")]
    [SerializeField] private Color messageColor = Color.white;

    [Tooltip("Autosave note: text size (bottom-right corner, just above the black bar).")]
    [SerializeField] private float autoSaveNoteFontSize = 22f;

    [Tooltip("Autosave note: colour (its alpha is the brightest the pulse gets).")]
    [SerializeField] private Color autoSaveNoteColor = new Color(1f, 1f, 1f, 0.55f);

    [Tooltip("Autosave note: pulses per second.")]
    [SerializeField] private float autoSaveNotePulseSpeed = 1.6f;

    [Tooltip("Autosave note: the faintest the pulse gets, as a fraction of its brightest.")]
    [SerializeField] private float autoSaveNoteMinAlpha = 0.25f;

    [Tooltip("Autosave note: distance from the right edge and from the black bar (canvas units).")]
    [SerializeField] private Vector2 autoSaveNoteMargin = new Vector2(24f, 10f);

    [Tooltip("Distance of the message from the bottom of the screen (canvas units).")]
    [SerializeField] private float messageBottomMargin = 140f;

    [Tooltip("Sorting order of the message canvas (above the pause menu).")]
    [SerializeField] private int sortingOrder = 600;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Tooltip("Write a line to the Console for every save / load.")]
    [SerializeField] private bool logToConsole = true;

    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private GameObject canvasRoot;
    private TMP_Text messageLabel;
    private TMP_Text autoNoteLabel;
    private Coroutine autoNoteRoutine;
    private bool autoSaveCall;
    private Coroutine messageRoutine;
    private float autoSaveTimer;
    private bool suppressSaving;

    private const string PrefSlot = "PixelClicker.Save.Slot";

    /// <summary>How many save slots there are.</summary>
    public int SlotCount => Mathf.Max(1, slotCount);

    /// <summary>
    /// The slot autosave, saving on quit and the startup load use: the one you last saved to or loaded from.
    /// Remembered between sessions.
    /// </summary>
    public int CurrentSlot
    {
        get => Mathf.Clamp(PixelPrefs.GetInt(PrefSlot, 1), 1, SlotCount);
        private set { PixelPrefs.SetInt(PrefSlot, Mathf.Clamp(value, 1, SlotCount)); PixelPrefs.Save(); }
    }

    private string PathFor(int slot)
    {
        string name = slot <= 1 ? saveFileName
            : Path.GetFileNameWithoutExtension(saveFileName) + "_slot" + slot + Path.GetExtension(saveFileName);
        return Path.Combine(Application.persistentDataPath, name);
    }

    private string FilePath => PathFor(CurrentSlot);

    /// <summary>True if the current slot has a save file.</summary>
    public bool HasSave => File.Exists(FilePath);

    /// <summary>True if this slot has a save file.</summary>
    public bool SlotHasSave(int slot) => File.Exists(PathFor(slot));

    /// <summary>What a slot holds, for the slot list: when it was saved and how many pixels were collected in total.</summary>
    public bool TryGetSlotInfo(int slot, out string savedAt, out double totalPixels) => TryGetSlotInfo(slot, out savedAt, out totalPixels, out double _);

    /// <summary>Same, plus the total play time (seconds) stored in that slot.</summary>
    public bool TryGetSlotInfo(int slot, out string savedAt, out double totalPixels, out double playSeconds)
    {
        savedAt = "";
        totalPixels = 0d;
        playSeconds = 0d;
        try
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return false;
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null) return false;
            savedAt = data.savedAt;
            playSeconds = data.playSeconds;
            totalPixels = data.totalPixels;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The newest save of all: a manual slot (1..SlotCount) or the newest autosave (slot 0, 'path' = its file in the autosave list). Manual saves only ever
    /// go to the numbered slots and autosaves only to the autosave list; 'Continue' picks whichever is newest. False when there are no saves at all.
    /// </summary>
    public bool TryGetLatest(out int slot, out string path)
    {
        slot = 0;
        path = null;
        DateTime best = DateTime.MinValue;
        for (int i = 1; i <= SlotCount; i++)
        {
            if (!SlotHasSave(i)) continue;
            DateTime t = File.GetLastWriteTimeUtc(PathFor(i));
            if (path == null || t > best) { best = t; slot = i; path = PathFor(i); }
        }
        string newestAuto = PixelAutosaves.NewestPath();
        if (newestAuto != null)
        {
            DateTime t = File.GetLastWriteTimeUtc(newestAuto);
            if (path == null || t > best) { slot = 0; path = newestAuto; }
        }
        return path != null;
    }

    /// <summary>Same, without the file: true when there is any save; 'slot' is 0 when the newest one is an autosave.</summary>
    public bool TryGetMostRecentSlot(out int slot) => TryGetLatest(out slot, out _);

    /// <summary>Main menu 'Continue': loads the newest save, manual or autosave (and pays the time away). False if there is none or it can't be read.</summary>
    public bool ContinueMostRecent()
    {
        if (!TryGetLatest(out int slot, out string path)) return false;
        if (slot > 0) CurrentSlot = slot;
        skipSettingsOnce = appliedProfilePath == path || PixelSettingsSync.ChangedSinceBaseline();   // the main menu already shows this profile's settings, or the player changed them there (after a New Game, say): keep them
        bool ok = Load(false, true, path);
        skipSettingsOnce = false;
        return ok;
    }

    private string appliedProfilePath;
    private bool skipSettingsOnce;

    /// <summary>The main menu shows the settings of the profile that Continue would load (the newest save): put them in place.</summary>
    private void ApplyProfileSettings(string path)
    {
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null) return;
            appliedProfilePath = path;
            PixelSettingsSync.Apply(data.settings);   // (an older save without settings changes nothing)
        }
        catch (Exception) { }
    }

    // ---- The save's own name (stored in the file) ----

    private const string PendingNamePref = "PixelClicker.SlotName.";   // a name given to a slot that has no save yet (and the old, pre-file names): written into the file by the next save

    /// <summary>The name of a slot's save ('' = none): from the file, else a name waiting for the first save.</summary>
    public string GetSlotName(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (File.Exists(path))
            {
                SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (d != null && !string.IsNullOrWhiteSpace(d.slotName)) return d.slotName;
            }
        }
        catch (Exception) { }
        return PixelPrefs.GetString(PendingNamePref + slot, "");
    }

    /// <summary>Deletes a slot's save (file, screenshot and the pending name). The slot stays, empty. Returns false if the file could not be removed.</summary>
    public bool DeleteSlot(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
            string meta = Path.ChangeExtension(path, ".meta"), thumb = PixelSaveShots.ThumbPath(path);
            if (File.Exists(meta)) File.Delete(meta);
            if (File.Exists(thumb)) File.Delete(thumb);
            PixelSaveShots.Forget(thumb);
            PixelPrefs.DeleteKey(PendingNamePref + slot);
            PixelPrefs.Save();
            if (logToConsole) PixelDebug.Info("PixelSaveGame: deleted the save in slot " + slot + ".", this);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("PixelSaveGame: could not delete slot " + slot + " - " + e.Message, this);
            return false;
        }
    }

    /// <summary>Renames a slot: a save's name is written into its file at once; a slot with no save keeps the name until it is saved.</summary>
    public void SetSlotName(int slot, string name)
    {
        name = (name ?? "").Trim();
        string path = PathFor(slot);
        if (File.Exists(path))
        {
            try
            {
                SaveData d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (d != null)
                {
                    d.slotName = name;
                    string temp = path + ".tmp";
                    File.WriteAllText(temp, JsonUtility.ToJson(d, true));
                    File.Delete(path);
                    File.Move(temp, path);
                    PixelPrefs.DeleteKey(PendingNamePref + slot);
                    return;
                }
            }
            catch (Exception e) { Debug.LogWarning("PixelSaveGame: could not rename the save - " + e.Message, this); }
        }
        if (name.Length == 0) PixelPrefs.DeleteKey(PendingNamePref + slot);
        else PixelPrefs.SetString(PendingNamePref + slot, name);
        PixelPrefs.Save();
    }

    /// <summary>Main menu 'New Game': from now on autosave and quit-save write into this slot.</summary>
    public void UseSlotForNewGame(int slot) { CurrentSlot = slot; }

    /// <summary>Set when a New Game is started after the game has already been played this run: the scene reloads fresh and the title screen presses Play by itself.</summary>
    public static bool AutoStartNewGame;

    /// <summary>Set with AutoStartNewGame (Restart / New Game). The title's Play clears AutoStartNewGame in its first update, which can be BEFORE this component's startup check runs one frame later: that check then took the newest save's settings (tips off...) into the new game. This flag outlives it and is used up by the check.</summary>
    public static bool NewGameKeepsDefaults;

    /// <summary>Saves into this slot (it becomes the current slot). The Save / Load window asks before overwriting.</summary>
    public bool SaveToSlot(int slot, bool message = true)
    {
        CurrentSlot = slot;
        return WriteSave(slot, message);
    }

    /// <summary>Loads this slot (it becomes the current slot).</summary>
    public bool LoadSlot(int slot, bool message = true)
    {
        if (!SlotHasSave(slot))
        {
            if (message) ShowMessage("That slot is empty");
            return false;
        }
        CurrentSlot = slot;
        return Load(message);
    }

    // ------------------------------------------------------------------
    // Unity lifecycle
    // ------------------------------------------------------------------

    private IEnumerator Start()
    {
        FindReferences();
        if (clicker == null)
        {
            Debug.LogError("PixelSaveGame: no PixelClicker found in the scene.", this);
            enabled = false;
            yield break;
        }

        BuildMessageUI();
        if (logToConsole) PixelDebug.Info("PixelSaveGame: slot " + CurrentSlot + " is " + FilePath, this);

        // One frame later, so every other script has finished its own startup first.
        yield return null;
        bool keepDefaults = AutoStartNewGame || NewGameKeepsDefaults;
        NewGameKeepsDefaults = false;
        // With the main menu showing, nothing loads by itself: its Continue button (or the Load window) does.
        if (PixelTitleScreen.Showing)
        {
            if (keepDefaults) { }   // a fresh start (Restart / New Game): the defaults stay
            else
            // The main menu shows the settings of the profile Continue would load (a brand-new game, with no save, keeps the defaults).
            if (TryGetLatest(out int _, out string latestPath)) ApplyProfileSettings(latestPath);
        }
        else if (!keepDefaults && loadOnStart && TryGetLatest(out int latestSlot, out string startPath))
        {
            if (latestSlot > 0) CurrentSlot = latestSlot;
            Load(false, true, startPath);
        }
    }

    private const string PrefAutoSave = "PixelClicker.Setting.AutoSave";
    private const string PrefAutoSaveMessage = "PixelClicker.Setting.AutoSaveMessage";

    /// <summary>The autosave intervals the player can pick in Settings (seconds; 0 = off).</summary>
    public static readonly float[] AutoSaveChoices = { 0f, 30f, 60f, 300f };
    private static int autoSaveChoiceCache = -1;
    private static float autoSaveChoiceReadAt;
    public static readonly string[] AutoSaveNames = { "Off", "30 seconds", "1 minute", "5 minutes" };

    /// <summary>Index into <see cref="AutoSaveChoices"/>; -1 (never chosen) = use the Inspector's interval.</summary>
    public static int AutoSaveChoice
    {
        get
        {
            // (read every frame by the autosave timer: the setting is read again once a second, so a change - or a save's settings being put back - shows within a second)
            if (Time.unscaledTime >= autoSaveChoiceReadAt)
            {
                autoSaveChoiceReadAt = Time.unscaledTime + 1f;
                autoSaveChoiceCache = Mathf.Clamp(PixelPrefs.GetInt(PrefAutoSave, -1), -1, AutoSaveChoices.Length - 1);
            }
            return autoSaveChoiceCache;
        }
        set { PixelPrefs.SetInt(PrefAutoSave, Mathf.Clamp(value, 0, AutoSaveChoices.Length - 1)); PixelPrefs.Save(); autoSaveChoiceReadAt = 0f; }
    }

    /// <summary>Show the "saved" message after every autosave? (Settings; before it is chosen, the Inspector's choice applies.)</summary>
    public static int AutoSaveMessageChoice // -1 = not chosen yet, 0 = no, 1 = yes
    {
        get => PixelPrefs.GetInt(PrefAutoSaveMessage, -1);
        set { PixelPrefs.SetInt(PrefAutoSaveMessage, value != 0 ? 1 : 0); PixelPrefs.Save(); }
    }

    /// <summary>The autosave interval in use (the player's choice, else the Inspector value). 0 = off.</summary>
    public float AutoSaveInterval => AutoSaveChoice >= 0 ? AutoSaveChoices[AutoSaveChoice] : autoSaveSeconds;

    /// <summary>The inspector's default for the autosave message, as a Settings value.</summary>
    public bool AutoSaveMessageNow => AutoSaveMessageChoice >= 0 ? AutoSaveMessageChoice != 0 : messageOnAutoSave;

    /// <summary>The Inspector's autosave interval (used until the player picks one).</summary>
    public float InspectorAutoSaveSeconds => autoSaveSeconds;

    private void Update()
    {
        float interval = AutoSaveInterval;
        if (interval <= 0f) return;
        // The timer only runs while the game is being played: not on the main menu, not while the pause menu (or the Save / Load window) is open.
        if (PixelCalm.InMenu) return;

        autoSaveTimer += Time.unscaledDeltaTime;
        if (autoSaveTimer >= interval)
        {
            autoSaveTimer = 0f;
            autoSaveCall = true;
            Save(AutoSaveMessageNow);
            autoSaveCall = false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: components must not be added from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (PixelFind.First<PixelOfflineProgress>() != null) return;

            UnityEditor.Undo.AddComponent<PixelOfflineProgress>(gameObject);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        };
    }
#endif

    private void OnApplicationPause(bool paused)
    {
        if (paused && saveWhenAppPauses) Save(false);
    }

    private void OnApplicationQuit()
    {
        if (saveOnQuit) Save(false);
    }

    private void OnDestroy()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void FindReferences()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont; // one shared font
    }

    // ------------------------------------------------------------------
    // Save
    // ------------------------------------------------------------------

    /// <summary>Writes the current slot's save (a manual save).</summary>
    [ContextMenu("Save Now")]
    public bool Save() => SaveToSlot(CurrentSlot, true);

    private float lastAutosaveTime = -999f;

    /// <summary>An AUTOSAVE (the timer, the save on quit / losing focus / Quit > Main menu): goes into the autosave list only - the numbered slots are only ever written by a manual save.</summary>
    public bool Save(bool showMessage)
    {
        if (PixelTitleScreen.Showing) return false;   // no autosaving from the main menu
        if (!autoSaveCall && Time.realtimeSinceStartup - lastAutosaveTime < 5f) return true;   // (quit + focus loss + main menu in quick succession: one is enough)
        return WriteSave(0, showMessage);
    }

    /// <summary>The slot's save: slot 0 = the autosave list, 1.. = a numbered slot (manual saves).</summary>
    /// <summary>Writes the game as a save file at this path (dev Test setups): the numbered slots, the current slot and the autosave list are not touched.</summary>
    public bool SaveToFile(string path, bool showMessage = false) => WriteSave(-1, showMessage, path);

    private bool WriteSave(int slot, bool showMessage, string fileOverride = null)
    {
        if (suppressSaving || clicker == null) return false;
        // On the main menu before anything was loaded or started, the game in memory is a blank one: an autosave / quit save / focus-loss save would write it over the real save in the current slot
        // (and fill the autosave list with blank games). Once a save is loaded or a new game started, or after Quit > Main menu (HasLoadedSave stays true), saving is fine.
        if (PixelTitleScreen.Showing && !HasLoadedSave) return false;

        try
        {
            SaveData data = new SaveData { savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), savedAtTicks = DateTime.UtcNow.Ticks };
            if (slot > 0) data.slotName = GetSlotName(slot);   // the save keeps its own name (a name waiting from a rename of an empty slot, or the one already in the file)



            data.testerObjects = PixelFeatureTester.ExportAll();

            PixelGameState.RaiseSaving();   // (world objects - placed devices, the running potion - copy themselves into the state first)
            data.state = PixelGameState.Current;   // (the moved systems' progress, whole)
            data.playSeconds = data.state.stats.playSeconds;
            data.totalPixels = data.state.clicker.TotalCollected;
            // A minigame that moved the old pixels around (Breakout, Sorting Race...) is running: their positions aren't real now.
            if (!PixelMinigame.TakeoverActive) data.oldPixels = clicker.GetOldPixelStates().ToArray();
            data.settings = PixelSettingsSync.Collect();

            string json = JsonUtility.ToJson(data, false);   // (no pretty print: smaller and quicker to write, every autosave)
            var shots = new System.Collections.Generic.List<string>();
            if (fileOverride != null)
            {
                string temp = fileOverride + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(fileOverride)) File.Delete(fileOverride);
                File.Move(temp, fileOverride);
                shots.Add(PixelSaveShots.ThumbPath(fileOverride));
            }
            else if (slot > 0)
            {
                // Write to a temp file first so a crash mid-write can't destroy the old save.
                string path = PathFor(slot);
                string temp = path + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                PixelPrefs.DeleteKey(PendingNamePref + slot);   // (the name is in the file now)
                shots.Add(PixelSaveShots.ThumbPath(path));   // a screenshot of the game goes next to every save, shown in the hover tooltips
            }
            else
            {
                double totalPixels = data.totalPixels;
                string autoShot = PixelAutosaves.Add(json, new PixelAutosaves.Meta { savedAt = data.savedAt, totalPixels = totalPixels, playSeconds = data.playSeconds },
                                                     keepAutosaveHistory ? autosaveHistorySize : 1);
                if (autoShot != null) shots.Add(autoShot);
                lastAutosaveTime = Time.realtimeSinceStartup;
            }
            HasLoadedSave = true;
            PixelSaveShots.Request(shots);

            if (logToConsole) PixelDebug.Info("PixelSaveGame: saved (" + (fileOverride != null ? fileOverride : slot > 0 ? "slot " + slot : "autosave") + ").", this);
            if (showMessage) { if (fileOverride != null) ShowMessage("Saved"); else if (slot == 0) ShowAutoNote(); else ShowMessage("Saved to slot " + slot); }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("PixelSaveGame: could not save - " + e.Message, this);
            if (showMessage) ShowMessage("Save failed");
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Load
    // ------------------------------------------------------------------

    /// <summary>True once a save was loaded or written this run (the main menu's button says 'Play' then, 'New Game' before).</summary>
    public static bool HasLoadedSave { get; internal set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLoadedFlag() { HasLoadedSave = false; autoSaveChoiceReadAt = 0f; }

    /// <summary>Reads the save file and applies it. Returns false if there is none or it can't be read.</summary>
    [ContextMenu("Load Now")]
    public bool Load() => Load(true);

    /// <summary>Loads one save file (an entry of the autosave list). The current slot does not change.</summary>
    public bool LoadFile(string path) => Load(false, false, path);

    /// <summary>The screenshot file of a slot's save.</summary>
    public string SlotThumbPath(int slot) => PixelSaveShots.ThumbPath(PathFor(slot));

    /// <summary>Loads the save. 'grantOffline' also pays out what the auto clicker earned while the game was closed (startup only). 'pathOverride' = load this file instead of the current slot's.</summary>
    public bool Load(bool showMessage, bool grantOffline = false, string pathOverride = null)
    {
        if (clicker == null) return false;
        if (!grantOffline)
        {
            // A later load (another slot) makes the "Welcome back" summary of the startup load meaningless.
            PixelOfflineProgress old = PixelFind.First<PixelOfflineProgress>();
            if (old != null) old.Dismiss();
        }

        if (pathOverride != null ? !File.Exists(pathOverride) : !HasSave)
        {
            if (showMessage) ShowMessage("No save found");
            return false;
        }

        PixelHints.SuppressFor(2f); // loading re-fires unlocks; they are not new events

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(pathOverride ?? FilePath));
            if (data == null || data.state == null) throw new Exception("the file is empty or damaged");

            // The moved systems' progress goes in first, whole (GameState); their views refresh at the end (PixelGameState.Loaded).
            PixelGameState.Replace(data.state);

            // Pixels: the currencies are in the GameState now (a type the save never had: nothing held, locked); the clicker settles them FIRST.
            clicker.ApplyLoadedTiers();

            // Shop: the packs bought are in the GameState; their effects are switched on / off HERE, before the rest of the load (a fixed core step).
            if (shop != null) shop.ApplyLoadedPackStates();

            // (Every system's progress is in the GameState - Replace above; the old per-system save parts were retired 2026-10-24.)
            if (!skipSettingsOnce) PixelSettingsSync.Apply(data.settings);   // (the profile's settings; Continue from the main menu keeps the ones it already showed)


            // Time away: only when the game has just started (loading by hand mid-game must not pay it again).
            if (grantOffline && data.savedAtTicks > 0)
            {
                PixelOfflineProgress offline = PixelFind.First<PixelOfflineProgress>();
                if (offline == null) offline = gameObject.AddComponent<PixelOfflineProgress>();
                offline.Grant(new DateTime(data.savedAtTicks, DateTimeKind.Utc));
            }

            clicker.FinishLoad();
            HasLoadedSave = true;
            if (PixelFloor.Instance != null && !PixelTitleScreen.Showing) PixelFloor.Instance.PropsFadeIn();   // pixels and devices fade in after a load
            if (data.oldPixels != null) clicker.RestoreOldPixels(data.oldPixels); // older saves leave the pixels alone

            PixelGameState.RaiseLoaded();   // (the moved systems' views refresh and their world objects - placed devices... - are rebuilt, BEFORE the tester machines)
            PixelFeatureTester.ImportAll(data.testerObjects);   // after the devices (a solar panel links to one) and the old pixels (belt riders are made again)

            if (logToConsole) PixelDebug.Info("PixelSaveGame: loaded the save from " + data.savedAt + ".", this);
            if (showMessage) ShowMessage("Loaded slot " + CurrentSlot);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("PixelSaveGame: could not load - " + e.Message, this);
            if (showMessage) ShowMessage("Load failed");
            return false;
        }
    }

    /// <summary>Deletes the autosaves (the manual slots are not touched).</summary>
    [ContextMenu("Delete Save")]
    public void DeleteSave()
    {
        try
        {
            PixelAutosaves.DeleteAll();   // (Restart: the autosaves of the old game go; manual saves in the slots stay)
            HasLoadedSave = false;
            if (logToConsole) PixelDebug.Info("PixelSaveGame: save deleted.", this);
        }
        catch (Exception e)
        {
            Debug.LogError("PixelSaveGame: could not delete the save - " + e.Message, this);
        }
    }

    /// <summary>Stops all saving (autosave, on quit...) - used right before starting a new game.</summary>
    public void SuppressSaving() => suppressSaving = true;

    // ------------------------------------------------------------------
    // On-screen message
    // ------------------------------------------------------------------

    private void BuildMessageUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelSaveGame Canvas", sortingOrder, referenceResolution, false);

        GameObject go = new GameObject("Message", typeof(RectTransform));
        go.transform.SetParent(canvasRoot.transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = messageFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = messageColor;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        messageLabel = tmp;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(900f, messageFontSize * 1.5f);
        rt.anchoredPosition = new Vector2(0f, messageBottomMargin);
        go.SetActive(false);

        GameObject noteGo = new GameObject("Autosave Note", typeof(RectTransform));
        noteGo.transform.SetParent(canvasRoot.transform, false);
        TextMeshProUGUI note = noteGo.AddComponent<TextMeshProUGUI>();
        note.fontSize = autoSaveNoteFontSize;
        note.alignment = TextAlignmentOptions.BottomRight;
        note.color = autoSaveNoteColor;
        note.raycastTarget = false;
        note.text = "Autosaving...";
        if (font != null) note.font = font;
        autoNoteLabel = note;
        RectTransform nrt = note.rectTransform;
        nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(1f, 0f);
        nrt.sizeDelta = new Vector2(400f, autoSaveNoteFontSize * 1.5f);
        noteGo.SetActive(false);
    }

    private void ShowAutoNote()
    {
        if (!showMessages || autoNoteLabel == null) return;
        if (autoNoteRoutine != null) StopCoroutine(autoNoteRoutine);
        autoNoteRoutine = StartCoroutine(AutoNoteRoutine());
    }

    private IEnumerator AutoNoteRoutine()
    {
        PixelHud hud = PixelHud.Instance;
        float bar = hud != null ? hud.BarHeight : 0f;
        autoNoteLabel.rectTransform.anchoredPosition = new Vector2(-autoSaveNoteMargin.x, bar + autoSaveNoteMargin.y);
        autoNoteLabel.gameObject.SetActive(true);

        // Unscaled time: it also pulses while the game is paused or time is stopped.
        float t = 0f;
        float total = Mathf.Max(0.5f, messageSeconds);
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            float wave = 0.5f + 0.5f * Mathf.Sin(t * autoSaveNotePulseSpeed * Mathf.PI * 2f);
            float fade = Mathf.Clamp01((total - t) / Mathf.Max(0.01f, total * 0.3f));
            Color c = autoSaveNoteColor;
            c.a = autoSaveNoteColor.a * Mathf.Lerp(Mathf.Clamp01(autoSaveNoteMinAlpha), 1f, wave) * fade;
            autoNoteLabel.color = c;
            yield return null;
        }
        autoNoteLabel.gameObject.SetActive(false);
        autoNoteRoutine = null;
    }

    private void ShowMessage(string text)
    {
        if (!showMessages || messageLabel == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text));
    }

    private IEnumerator MessageRoutine(string text)
    {
        messageLabel.text = text;
        messageLabel.gameObject.SetActive(true);

        // Unscaled time, so the message also fades while the game is paused.
        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            Color c = messageColor;
            c.a = messageColor.a * Mathf.Clamp01((messageSeconds - t) / Mathf.Max(0.01f, messageSeconds * 0.4f));
            messageLabel.color = c;
            yield return null;
        }

        messageLabel.gameObject.SetActive(false);
    }
}
