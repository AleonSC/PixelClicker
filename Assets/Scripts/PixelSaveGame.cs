using System;
using System.Collections;
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
    private class TierSave
    {
        public int type;
        public double count;
        public double total;
        public bool unlocked;
        public bool spawnDisabled;
        public long timesCollected;
        public long ultraCount;
        public int ultraLevel;
        public int valueLevel;
    }

    [Serializable]
    private class PackSave
    {
        public string name;
        public bool purchased;
        public int level;
    }

    [Serializable]
    private class PotionSave
    {
        public int type;
        public int owned;
        public int second; // 0 = a normal potion; otherwise (second pixel type + 1) of a combo potion
        public int bonus;  // extra storage from potions kept in the ghost backpack
    }

    [Serializable]
    private class DeviceSave
    {
        public string name;
        public int owned;
    }

    [Serializable]
    private class MinigameSave
    {
        public string id;
        public double value;
        public bool disabled; // switched off by the player in the shop
        public double extra;  // a second number some minigames keep (the bomb's parts held)
        public bool hasExtra; // false in older saves, which only stored the one number
    }

    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public string savedAt;
        public long savedAtTicks;   // UTC, used for offline progress
        public TierSave[] tiers;
        public PackSave[] packs;
        public PotionSave[] potions;
        public DeviceSave[] devices;
        public MinigameSave[] minigames;

        // Older saves (before minigames were saved by id).
        public double singularityCount;
        public double ghostsCaught;
        public string[] achievements;
        public long statManualClicks;
        public long statAutoClicks;
        public double statPlaySeconds;
        public double statPixelsSpent;
        public PixelStats.ExtraData statExtra;
        public bool hasActivePotion;
        public int activePotionType;
        public int activePotionSecond;   // 0 = a normal potion; otherwise (second pixel type + 1) of a combo potion
        public float activePotionRemaining;
        public PixelConsumables.PlacedState[] placedDevices;
        public PixelBank.Entry[] bank;
        public bool autoClickerRunning;
        public bool autoClickerDisabled; // switched off by the player (Toggles window)
        public bool grabDisabled;
        public bool timeStopDisabled;
        public bool comboDisabled;
        public string[] pets; // "type:off" per pet (PixelPets)
        public string[] eventTexts;
        public string[] eventFulls;
        public PixelClicker.OldPixelState[] oldPixels;   // where the old pixels lie
        public PixelSettingsSync.Entry[] settings;       // the settings / dev tool choices of this save file
        public float autoClickerInterval;
        public int autoClickerClicks;
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    [Header("References (found automatically if empty)")]
    [Tooltip("The PixelClicker whose pixels are saved.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The shop whose purchases and upgrade levels are saved.")]
    [SerializeField] private PixelShop shop;

    [Tooltip("The potions you own are saved.")]
    [SerializeField] private PixelConsumables consumables;

    [Tooltip("The play statistics (clicks, time played, pixels spent) that are saved.")]
    [SerializeField] private PixelStats stats;

    [Tooltip("The achievements whose earned list is saved.")]
    [SerializeField] private PixelAchievements achievements;

    [Tooltip("The auto clicker's running state, interval and clicks per tick are saved.")]
    [SerializeField] private PixelAutoClicker autoClicker;

    [Tooltip("Pixel Grabbing (for its on/off switch). Found automatically if left empty.")]
    [SerializeField] private PixelGrab grab;

    [Tooltip("Time Stop (for its on/off switch). Found automatically if left empty.")]
    [SerializeField] private PixelTimeStop timeStop;

    [Tooltip("The Pixel Bank whose stored pixels are saved. Found automatically if left empty.")]
    [SerializeField] private PixelBank bank;

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
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefSlot, 1), 1, SlotCount);
        private set { PlayerPrefs.SetInt(PrefSlot, Mathf.Clamp(value, 1, SlotCount)); PlayerPrefs.Save(); }
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
    public bool TryGetSlotInfo(int slot, out string savedAt, out double totalPixels)
    {
        savedAt = "";
        totalPixels = 0d;
        try
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return false;
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null) return false;
            savedAt = data.savedAt;
            if (data.tiers != null) foreach (TierSave t in data.tiers) totalPixels += t.total;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Saves into this slot (it becomes the current slot). The Save / Load window asks before overwriting.</summary>
    public bool SaveToSlot(int slot)
    {
        CurrentSlot = slot;
        return Save(true);
    }

    /// <summary>Loads this slot (it becomes the current slot).</summary>
    public bool LoadSlot(int slot)
    {
        if (!SlotHasSave(slot))
        {
            ShowMessage("That slot is empty");
            return false;
        }
        CurrentSlot = slot;
        return Load(true);
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
        if (logToConsole) Debug.Log("PixelSaveGame: slot " + CurrentSlot + " is " + FilePath, this);

        // One frame later, so every other script has finished its own startup first.
        yield return null;
        if (loadOnStart && HasSave) Load(false, true);
    }

    private const string PrefAutoSave = "PixelClicker.Setting.AutoSave";
    private const string PrefAutoSaveMessage = "PixelClicker.Setting.AutoSaveMessage";

    /// <summary>The autosave intervals the player can pick in Settings (seconds; 0 = off).</summary>
    public static readonly float[] AutoSaveChoices = { 0f, 30f, 60f, 300f };
    public static readonly string[] AutoSaveNames = { "Off", "30 seconds", "1 minute", "5 minutes" };

    /// <summary>Index into <see cref="AutoSaveChoices"/>; -1 (never chosen) = use the Inspector's interval.</summary>
    public static int AutoSaveChoice
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefAutoSave, -1), -1, AutoSaveChoices.Length - 1);
        set { PlayerPrefs.SetInt(PrefAutoSave, Mathf.Clamp(value, 0, AutoSaveChoices.Length - 1)); PlayerPrefs.Save(); }
    }

    /// <summary>Show the "saved" message after every autosave? (Settings; before it is chosen, the Inspector's choice applies.)</summary>
    public static int AutoSaveMessageChoice // -1 = not chosen yet, 0 = no, 1 = yes
    {
        get => PlayerPrefs.GetInt(PrefAutoSaveMessage, -1);
        set { PlayerPrefs.SetInt(PrefAutoSaveMessage, value != 0 ? 1 : 0); PlayerPrefs.Save(); }
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
        if (autoClicker == null) autoClicker = PixelFind.First<PixelAutoClicker>();
        if (grab == null) grab = PixelFind.First<PixelGrab>();
        if (timeStop == null) timeStop = PixelFind.First<PixelTimeStop>();
        if (bank == null) bank = PixelFind.First<PixelBank>();
        if (achievements == null) achievements = PixelFind.First<PixelAchievements>();
        if (stats == null) stats = PixelFind.First<PixelStats>();
        // Use the potions the shop sells into, so both always agree.
        if (shop != null && shop.Consumables != null) consumables = shop.Consumables;
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont; // one shared font
    }

    // ------------------------------------------------------------------
    // Save
    // ------------------------------------------------------------------

    /// <summary>Writes the save file. Returns true on success.</summary>
    [ContextMenu("Save Now")]
    public bool Save() => Save(true);

    public bool Save(bool showMessage)
    {
        if (suppressSaving || clicker == null) return false;

        try
        {
            SaveData data = new SaveData { savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), savedAtTicks = DateTime.UtcNow.Ticks };

            PixelClicker.PixelTier[] tiers = clicker.Tiers;
            data.tiers = new TierSave[tiers.Length];
            for (int i = 0; i < tiers.Length; i++)
                data.tiers[i] = new TierSave
                {
                    type = (int)tiers[i].type,
                    count = tiers[i].count,
                    total = tiers[i].totalCollected,
                    unlocked = tiers[i].unlocked,
                    spawnDisabled = tiers[i].spawnDisabled,
                    timesCollected = tiers[i].timesCollected,
                    ultraCount = tiers[i].ultraCount,
                    ultraLevel = tiers[i].ultraLevel,
                    valueLevel = tiers[i].valueLevel,
                };

            if (shop != null)
            {
                data.packs = new PackSave[shop.PackCount];
                for (int i = 0; i < data.packs.Length; i++)
                    data.packs[i] = new PackSave
                    {
                        name = shop.GetPackName(i),
                        purchased = shop.GetPackPurchased(i),
                        level = shop.GetPackLevel(i),
                    };
            }

            if (consumables != null)
            {
                data.potions = new PotionSave[consumables.Count];
                for (int i = 0; i < data.potions.Length; i++)
                    data.potions[i] = new PotionSave
                    {
                        type = (int)consumables.Get(i).type,
                        owned = consumables.Get(i).owned,
                        bonus = consumables.Get(i).bonusCap,
                        second = consumables.Get(i).craftOnly ? (int)consumables.Get(i).secondType + 1 : 0,
                    };
            }

            if (consumables != null)
            {
                data.devices = new DeviceSave[consumables.DeviceCount];
                for (int i = 0; i < data.devices.Length; i++)
                    data.devices[i] = new DeviceSave
                    {
                        name = consumables.GetDevice(i).displayName,
                        owned = consumables.GetDevice(i).owned,
                    };
            }

            if (consumables != null && consumables.IsActive)
            {
                PixelConsumables.Potion running = consumables.Get(consumables.ActiveIndex);
                data.hasActivePotion = true;
                data.activePotionType = (int)running.type;
                data.activePotionSecond = running.craftOnly ? (int)running.secondType + 1 : 0;
                data.activePotionRemaining = consumables.Remaining;
            }
            if (consumables != null) data.placedDevices = consumables.GetPlacedDevices().ToArray();
            if (bank != null) data.bank = bank.GetState().ToArray();

            // Every minigame with a goal counter saves its count under its id.
            System.Collections.Generic.List<MinigameSave> minigameSaves = new System.Collections.Generic.List<MinigameSave>();
            foreach (PixelMinigame m in PixelMinigame.All)
                if (m != null) minigameSaves.Add(new MinigameSave { id = m.Id, value = m.HasTracker ? m.TrackerCount : 0d, disabled = m.UserDisabled, extra = m.ExtraValue, hasExtra = true });
            data.minigames = minigameSaves.ToArray();
            if (achievements != null) data.achievements = achievements.GetSaveState();
            if (stats != null)
            {
                data.statManualClicks = stats.ManualClicks;
                data.statAutoClicks = stats.AutoClicks;
                data.statPlaySeconds = stats.PlaySeconds;
                data.statPixelsSpent = stats.PixelsSpent;
                data.statExtra = stats.GetExtra();
            }

            if (autoClicker != null)
            {
                data.autoClickerRunning = autoClicker.Running;
                data.autoClickerDisabled = autoClicker.UserDisabled;
                data.autoClickerInterval = autoClicker.Interval;
                data.autoClickerClicks = autoClicker.ClicksPerTick;
            }

            if (grab != null) data.grabDisabled = grab.UserDisabled;
            if (timeStop != null) data.timeStopDisabled = timeStop.UserDisabled;
            PixelCombo comboMeter = PixelFind.First<PixelCombo>();
            if (comboMeter != null) data.comboDisabled = comboMeter.UserDisabled;
            PixelPets petSystem = PixelFind.First<PixelPets>();
            if (petSystem != null) data.pets = petSystem.Export();
            PixelHints.ExportHistory(out data.eventTexts, out data.eventFulls);
            // A minigame that moved the old pixels around (Breakout, Sorting Race...) is running: their positions aren't real now.
            if (!PixelMinigame.TakeoverActive) data.oldPixels = clicker.GetOldPixelStates().ToArray();
            data.settings = PixelSettingsSync.Collect();

            // Write to a temp file first so a crash mid-write can't destroy the old save.
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);

            if (logToConsole) Debug.Log("PixelSaveGame: saved (slot " + CurrentSlot + ").", this);
            if (showMessage) { if (autoSaveCall) ShowAutoNote(); else ShowMessage("Saved to slot " + CurrentSlot); }
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

    /// <summary>Reads the save file and applies it. Returns false if there is none or it can't be read.</summary>
    [ContextMenu("Load Now")]
    public bool Load() => Load(true);

    /// <summary>Loads the save. 'grantOffline' also pays out what the auto clicker earned while the game was closed (startup only).</summary>
    public bool Load(bool showMessage, bool grantOffline = false)
    {
        if (clicker == null) return false;

        if (!HasSave)
        {
            if (showMessage) ShowMessage("No save found");
            return false;
        }

        PixelHints.SuppressFor(2f); // loading re-fires unlocks; they are not new events

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (data == null || data.tiers == null) throw new Exception("the file is empty or damaged");

            // Pixels. Anything the save doesn't mention goes back to nothing / locked.
            foreach (PixelClicker.PixelTier tier in clicker.Tiers)
                clicker.LoadTierState(tier.type, 0d, 0d, false);
            foreach (TierSave t in data.tiers)
                clicker.LoadTierState((PixelClicker.PixelType)t.type, t.count, t.total, t.unlocked, t.spawnDisabled, t.timesCollected, t.ultraCount, t.ultraLevel, t.valueLevel);

            // Shop: match packs by name.
            if (shop != null)
            {
                for (int i = 0; i < shop.PackCount; i++)
                {
                    PackSave saved = data.packs != null
                        ? Array.Find(data.packs, p => p.name == shop.GetPackName(i)) : null;
                    shop.SetPackState(i, saved != null && saved.purchased, saved != null ? saved.level : 0);
                }
            }

            // Auto clicker.
            if (autoClicker != null)
            {
                if (data.autoClickerInterval > 0f) autoClicker.Interval = data.autoClickerInterval;
                if (data.autoClickerClicks > 0) autoClicker.ClicksPerTick = data.autoClickerClicks;
                autoClicker.UserDisabled = data.autoClickerDisabled;
                if (data.autoClickerRunning) autoClicker.Activate();
                else autoClicker.Deactivate();
            }

            if (grab != null) grab.UserDisabled = data.grabDisabled;
            if (timeStop != null) timeStop.UserDisabled = data.timeStopDisabled;
            PixelCombo comboMeterLoad = PixelFind.First<PixelCombo>();
            if (comboMeterLoad != null) comboMeterLoad.UserDisabled = data.comboDisabled;
            PixelPets petsLoad = PixelFind.First<PixelPets>();
            if (petsLoad != null) petsLoad.Import(data.pets); // older saves have no pets
            PixelHints.ImportHistory(data.eventTexts, data.eventFulls);
            PixelSettingsSync.Apply(data.settings);

            // Potions.
            if (consumables != null)
            {
                consumables.StopActive();
                for (int i = 0; i < consumables.Count; i++)
                {
                    PixelConsumables.Potion potion = consumables.Get(i);
                    int second = potion.craftOnly ? (int)potion.secondType + 1 : 0;
                    PotionSave saved = data.potions != null
                        ? Array.Find(data.potions, p => p.type == (int)potion.type && p.second == second) : null;
                    consumables.Get(i).bonusCap = saved != null ? Mathf.Max(0, saved.bonus) : 0;
                    consumables.Get(i).owned = saved != null ? consumables.ClampHeld(i, saved.owned) : 0;
                }
            }

            if (consumables != null)
            {
                consumables.CancelPlacement();
                for (int i = 0; i < consumables.DeviceCount; i++)
                {
                    DeviceSave saved = data.devices != null
                        ? Array.Find(data.devices, d => d.name == consumables.GetDevice(i).displayName) : null;
                    consumables.GetDevice(i).owned = saved != null ? Mathf.Max(0, saved.owned) : 0;
                }
            }

            foreach (PixelMinigame m in PixelMinigame.All)
            {
                if (m == null) continue;

                MinigameSave saved = data.minigames != null ? Array.Find(data.minigames, x => x.id == m.Id) : null;
                m.UserDisabled = saved != null && saved.disabled;
                if (m.UserDisabled) m.Deactivate();
                if (!m.HasTracker) continue;
                double value = saved != null ? saved.value
                             : m.Id == "ghost" ? data.ghostsCaught            // save from before minigames were saved by id
                             : m.Id == "blackhole" ? data.singularityCount : 0d;
                m.SetTrackerCount(value);

                // Older saves stored only one number: for the bomb that was the parts held, so it becomes both.
                if (saved != null) m.SetExtraValue(saved.hasExtra ? saved.extra : saved.value);
            }
            if (achievements != null) achievements.SetSaveState(data.achievements);
            if (stats != null)
            {
                stats.SetState(data.statManualClicks, data.statAutoClicks, data.statPlaySeconds, data.statPixelsSpent);
                stats.SetExtra(data.statExtra);
            }

            // Time away: only when the game has just started (loading by hand mid-game must not pay it again).
            if (grantOffline && data.savedAtTicks > 0)
            {
                PixelOfflineProgress offline = PixelFind.First<PixelOfflineProgress>();
                if (offline == null) offline = gameObject.AddComponent<PixelOfflineProgress>();
                offline.Grant(new DateTime(data.savedAtTicks, DateTimeKind.Utc));
            }

            clicker.FinishLoad();
            if (data.oldPixels != null) clicker.RestoreOldPixels(data.oldPixels); // older saves leave the pixels alone

            if (bank != null) bank.SetState(data.bank);

            // The running potion and the devices in the world (after FinishLoad, which resets the spawn choice).
            if (consumables != null)
            {
                consumables.ClearPlacedDevices();
                if (data.placedDevices != null)
                    foreach (PixelConsumables.PlacedState placed in data.placedDevices) consumables.RestorePlacedDevice(placed);

                if (data.hasActivePotion)
                {
                    for (int i = 0; i < consumables.Count; i++)
                    {
                        PixelConsumables.Potion potion = consumables.Get(i);
                        int second = potion.craftOnly ? (int)potion.secondType + 1 : 0;
                        if ((int)potion.type != data.activePotionType || second != data.activePotionSecond) continue;
                        consumables.RestoreActive(i, data.activePotionRemaining);
                        break;
                    }
                }
            }

            if (logToConsole) Debug.Log("PixelSaveGame: loaded the save from " + data.savedAt + ".", this);
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

    /// <summary>Deletes the save file.</summary>
    [ContextMenu("Delete Save")]
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (logToConsole) Debug.Log("PixelSaveGame: save deleted.", this);
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
