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
/// - Loads the save automatically when the game starts (optional).
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
    }

    [Serializable]
    private class DeviceSave
    {
        public string name;
        public int owned;
    }

    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public string savedAt;
        public TierSave[] tiers;
        public PackSave[] packs;
        public PotionSave[] potions;
        public DeviceSave[] devices;
        public bool autoClickerRunning;
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

    [Tooltip("The auto clicker's running state, interval and clicks per tick are saved.")]
    [SerializeField] private PixelAutoClicker autoClicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("File")]
    [Tooltip("Name of the save file. It lives in Application.persistentDataPath (see the Console when the game starts).")]
    [SerializeField] private string saveFileName = "pixelclicker_save.json";

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
    private Coroutine messageRoutine;
    private float autoSaveTimer;
    private bool suppressSaving;

    private string FilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    /// <summary>True if a save file exists.</summary>
    public bool HasSave => File.Exists(FilePath);

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
        if (logToConsole) Debug.Log("PixelSaveGame: save file is " + FilePath, this);

        // One frame later, so every other script has finished its own startup first.
        yield return null;
        if (loadOnStart && HasSave) Load(false);
    }

    private void Update()
    {
        if (autoSaveSeconds <= 0f) return;

        autoSaveTimer += Time.unscaledDeltaTime;
        if (autoSaveTimer >= autoSaveSeconds)
        {
            autoSaveTimer = 0f;
            Save(messageOnAutoSave);
        }
    }

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
#if UNITY_2023_1_OR_NEWER
        if (clicker == null) clicker = FindFirstObjectByType<PixelClicker>();
        if (shop == null) shop = FindFirstObjectByType<PixelShop>();
        if (autoClicker == null) autoClicker = FindFirstObjectByType<PixelAutoClicker>();
#else
        if (clicker == null) clicker = FindObjectOfType<PixelClicker>();
        if (shop == null) shop = FindObjectOfType<PixelShop>();
        if (autoClicker == null) autoClicker = FindObjectOfType<PixelAutoClicker>();
#endif
        // Use the potions the shop sells into, so both always agree.
        if (shop != null && shop.Consumables != null) consumables = shop.Consumables;
#if UNITY_2023_1_OR_NEWER
        if (consumables == null) consumables = FindFirstObjectByType<PixelConsumables>();
#else
        if (consumables == null) consumables = FindObjectOfType<PixelConsumables>();
#endif
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
            SaveData data = new SaveData { savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };

            PixelClicker.PixelTier[] tiers = clicker.Tiers;
            data.tiers = new TierSave[tiers.Length];
            for (int i = 0; i < tiers.Length; i++)
                data.tiers[i] = new TierSave
                {
                    type = (int)tiers[i].type,
                    count = tiers[i].count,
                    total = tiers[i].totalCollected,
                    unlocked = tiers[i].unlocked,
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

            if (autoClicker != null)
            {
                data.autoClickerRunning = autoClicker.Running;
                data.autoClickerInterval = autoClicker.Interval;
                data.autoClickerClicks = autoClicker.ClicksPerTick;
            }

            // Write to a temp file first so a crash mid-write can't destroy the old save.
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);

            if (logToConsole) Debug.Log("PixelSaveGame: saved.", this);
            if (showMessage) ShowMessage("Game saved");
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

    public bool Load(bool showMessage)
    {
        if (clicker == null) return false;

        if (!HasSave)
        {
            if (showMessage) ShowMessage("No save found");
            return false;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (data == null || data.tiers == null) throw new Exception("the file is empty or damaged");

            // Pixels. Anything the save doesn't mention goes back to nothing / locked.
            foreach (PixelClicker.PixelTier tier in clicker.Tiers)
                clicker.LoadTierState(tier.type, 0d, 0d, false);
            foreach (TierSave t in data.tiers)
                clicker.LoadTierState((PixelClicker.PixelType)t.type, t.count, t.total, t.unlocked);

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
                if (data.autoClickerRunning) autoClicker.Activate();
                else autoClicker.Deactivate();
            }

            // Potions.
            if (consumables != null)
            {
                consumables.StopActive();
                for (int i = 0; i < consumables.Count; i++)
                {
                    PotionSave saved = data.potions != null
                        ? Array.Find(data.potions, p => p.type == (int)consumables.Get(i).type) : null;
                    consumables.Get(i).owned = saved != null ? Mathf.Max(0, saved.owned) : 0;
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

            clicker.FinishLoad();

            if (logToConsole) Debug.Log("PixelSaveGame: loaded the save from " + data.savedAt + ".", this);
            if (showMessage) ShowMessage("Game loaded");
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
        canvasRoot = new GameObject("PixelSaveGame Canvas");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

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
