using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First-time tips. The first time something happens (a feature is bought, a minigame appears, a potion is drunk...)
/// a tip box (<see cref="PixelNotice"/>) explains it. Each tip shows once and is remembered between sessions
/// (PlayerPrefs "PixelClicker.Hint.&lt;id&gt;"). If several fire together they queue up and show one after another.
///
/// The texts are in the Inspector list (add or edit tips there). Other code calls <c>PixelHints.Trigger("id")</c>.
/// Added automatically by PixelClicker if the scene has none.
/// </summary>
public class PixelHints : MonoBehaviour
{
    [Serializable]
    public class Hint
    {
        [Tooltip("Unique id (also the PlayerPrefs key). Built-in ids are triggered by the game; don't rename them.")]
        public string id = "hint";

        [Tooltip("Show this tip?")]
        public bool enabled = true;

        [TextArea(2, 5)]
        [Tooltip("The message in the tip box.")]
        public string text = "";

        [Tooltip("The short line shown in the bottom-left overlay when this happens (click it, or press Enter, to reopen the full tip). Empty = no overlay line.")]
        public string shortText = "";
    }

    [Header("Settings")]
    [Tooltip("Master switch for all first-time tips.")]
    [SerializeField] private bool hintsEnabled = true;

    [Min(0f)]
    [Tooltip("Seconds to wait before a tip appears after its event (so it doesn't cover what just happened).")]
    [SerializeField] private float showDelay = 0.8f;

    [Min(0f)]
    [Tooltip("Close a tip by itself after this many seconds (0 = it stays until the player clicks Got it).")]
    [SerializeField] private float autoCloseSeconds = 0f;

    [Tooltip("Open the full tip box by itself when something happens (the overlay line always shows). Untick to only show the short line and let the player open the tip.")]
    [SerializeField] private bool autoOpenTips = true;

    [Tooltip("Ignore events during the first few seconds after the game starts (loading a save re-triggers unlocks).")]
    [SerializeField] private float startupQuietSeconds = 3f;

    [Header("Overlay (bottom-left line)")]
    [Tooltip("Show the short event line at the bottom left, above the black bar.")]
    [SerializeField] private bool showOverlay = true;

    [Min(0f)]
    [Tooltip("Seconds the line stays fully visible before fading.")]
    [SerializeField] private float overlayVisibleSeconds = 5f;

    [Min(0f)]
    [Tooltip("Seconds the line takes to fade away.")]
    [SerializeField] private float overlayFadeSeconds = 1.2f;

    [Tooltip("Key that reopens the latest event (shows the line; pressing it again while the line is visible opens its tip).")]
    [SerializeField] private KeyCode reopenKey = KeyCode.Return;

    [Tooltip("Font size of the overlay line.")]
    [SerializeField] private float overlayFontSize = 28f;

    [Tooltip("Text colour of the overlay line.")]
    [SerializeField] private Color overlayTextColor = new Color(1f, 0.95f, 0.7f, 1f);

    [Tooltip("Background colour behind the overlay line.")]
    [SerializeField] private Color overlayBackColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("Distance from the left edge and from the black bar (canvas units).")]
    [SerializeField] private Vector2 overlayMargin = new Vector2(24f, 16f);

    [Tooltip("Small hint appended to the line, telling the player how to open it.")]
    [SerializeField] private string overlayHintSuffix = "  <size=70%><color=#9fb7c9>(click or press Enter)</color></size>";

    [Tooltip("Add the built-in tips that are missing from the list below.")]
    [SerializeField] private bool addDefaultHints = true;

    [Header("Tips")]
    [Tooltip("One entry per tip.")]
    [SerializeField] private List<Hint> hints = new List<Hint>();

    private static PixelHints instance;
    private readonly Queue<Hint> queue = new Queue<Hint>();
    private float waitTimer;
    private bool wasShowing;

    private const string PrefPrefix = "PixelClicker.Hint.";

    private static Hint H(string id, string text) => new Hint { id = id, text = text, shortText = ShortFor(id) };

    private static string ShortFor(string id)
    {
        switch (id)
        {
            case "crafting": return "You unlocked Crafting!";
            case "grab": return "You unlocked Pixel Grabbing!";
            case "timestop": return "You unlocked Time Stop! (press T)";
            case "bank": return "You unlocked the Pixel Bank!";
            case "minigame_ghost": return "A ghost appeared!";
            case "minigame_meteor": return "A meteor appeared!";
            case "minigame_bomb": return "A bomb appeared!";
            case "minigame_pad": return "An Ultra Pad appeared!";
            case "potion": return "You drank a potion!";
            case "device_Vacuum": return "You placed a Vacuum Device!";
            case "device_Fan": return "You placed a Fan!";
            case "device_Sorter": return "You placed a Sorter!";
            case "craft": return "You crafted something!";
            case "combo": return "Combo started!";
            case "ultra": return "You earned an Ultra pixel!";
            case "upgrade_interval": return "Auto Clicker upgraded: faster clicks!";
            case "upgrade_clicks": return "Auto Clicker upgraded: more clicks per tick!";
            case "upgrade_combo": return "Combo Meter upgraded!";
            case "upgrade_bank": return "Bank Storage upgraded!";
            case "upgrade_ultra": return "Ultra boost bought!";
            case "pixel_Glass": return "You unlocked Glass Pixels!";
            case "pixel_Vacuum": return "You unlocked Vacuum Pixels!";
            case "pixel_Obsidian": return "You unlocked Obsidian Pixels!";
            case "pixel_Luminescent": return "You unlocked Luminescent Pixels!";
            case "pixel_Singularity": return "You unlocked Singularity Pixels!";
            case "pixel_Ghost": return "You unlocked Ghost Pixels!";
            case "pixel_Meteor": return "You unlocked Meteor Pixels!";
            case "pixel_Red": return "You unlocked Red, Green and Blue Pixels!";
            default: return "";
        }
    }

    private static List<Hint> DefaultHints() => new List<Hint>
    {
        H("crafting", "Crafting unlocked! Open the Crafting button at the bottom right, drag two items into the boxes and press Craft. A pixel + Glass makes that pixel's potion."),
        H("grab", "Pixel Grabbing unlocked! Hold the left mouse button on an old pixel to pick it up, then let go to throw it."),
        H("timestop", "Time Stop unlocked! Press T to freeze time. It drains an energy meter, but you can still click the cube - the old pixels pile up and burst out when time resumes."),
        H("bank", "Pixel Bank unlocked! Right-click an old pixel to store it. Open the Bank tab on the left edge, or press B for the hose: left-click spits a pixel out, hold right-click sucks up an area."),
        H("minigame_ghost", "A ghost! Click it before it floats away to catch it and get a free potion."),
        H("minigame_meteor", "A meteor! Click it as often as you like to chip off meteor chunks."),
        H("minigame_bomb", "A bomb! Cut the wire that matches the colour you have the MOST of before time runs out. A wrong wire (or timeout) clears your old pixels."),
        H("minigame_pad", "An Ultra Pad! Drop old pixels of the colour it asks for onto it to earn Ultra pixels. Other colours cost you pixels."),
        H("potion", "Potion drunk! For a while only that pixel type spawns. Right-click the active potion line at the top to cancel it."),
        H("device_Vacuum", "Vacuum Device placed! It pulls in old pixels and collects them again until its timer runs out. Hold right-click on a placed device to remove it."),
        H("device_Fan", "Fan placed! It blows old pixels along the floor in front of it until its timer runs out."),
        H("device_Sorter", "Sorter placed! Click its buttons to change how hard old pixels are pushed out of the pipe."),
        H("craft", "Crafted! The new item is in your Inventory under Consumables."),
        H("combo", "Combo! Keep clicking quickly to raise the combo bonus. It breaks if you stop for a moment."),
        H("upgrade_interval", "The auto clicker now clicks faster. Each level shortens the time between its clicks."),
        H("upgrade_clicks", "Multi-Click upgraded! The auto clicker now makes more clicks every time it ticks."),
        H("upgrade_combo", "Combo Meter upgraded! Quick manual clicks build a bonus on your payouts; each level raises the highest bonus you can reach."),
        H("upgrade_bank", "Bank Storage upgraded! The Pixel Bank can now hold more stored pixels."),
        H("upgrade_ultra", "Ultra boost bought! That pixel type now pays more for every click. You can keep boosting it with more Ultra pixels."),
        H("pixel_Glass", "Glass Pixels unlocked! They are see-through, and you can combine one with another pixel in Crafting to make a potion."),
        H("pixel_Vacuum", "Vacuum Pixels unlocked! Clicking one sucks nearby old pixels in and collects them."),
        H("pixel_Obsidian", "Obsidian Pixels unlocked! A tougher pixel that takes more than one click to collect."),
        H("pixel_Luminescent", "Luminescent Pixels unlocked! They glow, and they are worth a lot more than the plain ones."),
        H("pixel_Singularity", "Singularity Pixels unlocked! A very rare pixel born from the black holes you fed."),
        H("pixel_Ghost", "Ghost Pixels unlocked! A rare pixel from all the ghosts you caught."),
        H("pixel_Meteor", "Meteor Pixels unlocked! They streak away across the screen instead of falling."),
        H("pixel_Red", "The RGB Pack unlocked Red, Green and Blue pixels! They pay the same as the black, gray and white ones, but you need them to unlock Glass pixels and more."),
        H("ultra", "Ultra pixel earned! Spend Ultra pixels in the shop's Upgrades > Pixel sub-tab to boost a pixel type's payout."),
    };

    private bool EnsureDefaultHints()
    {
        if (!addDefaultHints) return false;
        if (hints == null) hints = new List<Hint>();
        bool changed = false;
        foreach (Hint d in DefaultHints())
        {
            Hint existing = hints.Find(h => h != null && h.id == d.id);
            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.shortText) && !string.IsNullOrEmpty(d.shortText)) { existing.shortText = d.shortText; changed = true; }
                continue;
            }
            hints.Add(d);
            changed = true;
        }
        return changed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultHints()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        EnsureDefaultHints();
    }

    private void OnEnable()
    {
        PixelConsumables.PotionDrunk += OnPotion;
        PixelConsumables.DevicePlaced += OnDevice;
        PixelCrafting.Crafted += OnCrafted;
        PixelCombo.ComboReached += OnCombo;
        PixelMinigame.Happened += OnMinigame;
        PixelClicker.UltraGained += OnUltra;
    }

    private void OnDisable()
    {
        PixelConsumables.PotionDrunk -= OnPotion;
        PixelConsumables.DevicePlaced -= OnDevice;
        PixelCrafting.Crafted -= OnCrafted;
        PixelCombo.ComboReached -= OnCombo;
        PixelMinigame.Happened -= OnMinigame;
        PixelClicker.UltraGained -= OnUltra;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnPotion(string name) => Trigger("potion");
    private void OnDevice(PixelConsumables.DeviceKind kind) => Trigger("device_" + kind);
    private void OnCrafted() => Trigger("craft");
    private void OnCombo(int combo) => Trigger("combo");
    private void OnUltra() => Trigger("ultra");

    private void OnMinigame(PixelMinigame game, PixelMinigame.MinigameEvent what)
    {
        if (what == PixelMinigame.MinigameEvent.Spawned && game != null) Trigger("minigame_" + game.Id);
    }

    // ------------------------------------------------------------------

    /// <summary>Shows the tip with this id the first time it is triggered (ever). Unknown / disabled / already-seen ids do nothing.</summary>
    public static void Trigger(string id)
    {
        if (instance != null) instance.Enqueue(id);
    }

    private static bool Seen(string id)
    {
        try { return PlayerPrefs.GetInt(PrefPrefix + id, 0) != 0; }
        catch (Exception) { return false; }
    }

    private static void MarkSeen(string id)
    {
        try { PlayerPrefs.SetInt(PrefPrefix + id, 1); }
        catch (Exception) { /* no PlayerPrefs: it may show again next session */ }
    }

    private void Enqueue(string id)
    {
        if (!hintsEnabled || string.IsNullOrEmpty(id) || Seen(id)) return;
        Hint hint = hints != null ? hints.Find(h => h != null && h.id == id) : null;
        if (hint == null || !hint.enabled || string.IsNullOrEmpty(hint.text)) return;
        MarkSeen(id);
        string full = hint.text;
        Announce(hint.shortText, () => PixelNotice.Show(full, autoCloseSeconds));
        if (!autoOpenTips) return;
        queue.Enqueue(hint);
        if (waitTimer <= 0f && !PixelNotice.IsShowing) waitTimer = showDelay;
    }

    // ------------------------------------------------------------------
    // The overlay line
    // ------------------------------------------------------------------

    private string lastShort;
    private Action lastOpen;
    private float overlayTimer;
    private GameObject overlayRoot;
    private CanvasGroup overlayGroup;
    private TMP_Text overlayLabel;

    /// <summary>
    /// Shows a short line at the bottom left ("You unlocked the Auto Clicker!"). Clicking it or pressing Enter runs 'open'
    /// (usually reopening the full tip). Ignored right after the game starts.
    /// </summary>
    public static void Announce(string shortText, Action open = null)
    {
        if (instance != null) instance.ShowOverlay(shortText, open);
    }

    private void ShowOverlay(string shortText, Action open)
    {
        if (!hintsEnabled || !showOverlay || string.IsNullOrEmpty(shortText)) return;
        if (Time.realtimeSinceStartup < startupQuietSeconds) return;
        lastShort = shortText;
        lastOpen = open;
        DisplayOverlay();
    }

    private void DisplayOverlay()
    {
        if (string.IsNullOrEmpty(lastShort)) return;
        if (overlayRoot == null) BuildOverlay();
        overlayLabel.text = lastShort + (lastOpen != null ? overlayHintSuffix : "");
        Vector2 size = overlayLabel.GetPreferredValues(overlayLabel.text, 1200f, 0f);
        RectTransform back = (RectTransform)overlayLabel.transform.parent;
        back.sizeDelta = new Vector2(Mathf.Ceil(size.x) + 36f, Mathf.Ceil(size.y) + 16f);
        overlayTimer = overlayVisibleSeconds + overlayFadeSeconds;
        overlayGroup.alpha = 1f;
        overlayRoot.SetActive(true);
    }

    private void BuildOverlay()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        overlayRoot = PixelUIKit.CreateCanvas("Pixel Event Line", 90, new Vector2(1920f, 1080f), true);
        overlayRoot.transform.SetParent(transform, false);
        overlayGroup = overlayRoot.AddComponent<CanvasGroup>();

        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        GameObject back = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
        back.transform.SetParent(overlayRoot.transform, false);
        Image img = back.GetComponent<Image>();
        img.color = overlayBackColor;
        RectTransform br = (RectTransform)back.transform;
        br.anchorMin = br.anchorMax = br.pivot = Vector2.zero;
        br.anchoredPosition = new Vector2(overlayMargin.x, bar + overlayMargin.y);
        back.GetComponent<Button>().onClick.AddListener(OpenLatest);

        overlayLabel = PixelUIKit.CreateText(font, back.transform, "Text", "", overlayFontSize, TextAlignmentOptions.Left, FontStyles.Bold, overlayTextColor);
        overlayLabel.raycastTarget = false;
        overlayLabel.overflowMode = TextOverflowModes.Overflow;
        RectTransform lr = overlayLabel.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(18f, 4f);
        lr.offsetMax = new Vector2(-18f, -4f);
    }

    private void OpenLatest()
    {
        if (lastOpen != null) lastOpen();
        overlayTimer = 0f;
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private bool ReopenKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(reopenKey) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private void UpdateOverlay()
    {
        if (PixelPauseMenu.IsPaused || string.IsNullOrEmpty(lastShort)) return;

        // Don't steal Enter from a text box.
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        bool typing = es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;

        bool visible = overlayRoot != null && overlayRoot.activeSelf;
        if (!typing && ReopenKeyPressed())
        {
            if (visible && lastOpen != null) OpenLatest();
            else DisplayOverlay();
            return;
        }

        if (!visible) return;
        overlayTimer -= Time.unscaledDeltaTime;
        overlayGroup.alpha = overlayFadeSeconds > 0f ? Mathf.Clamp01(overlayTimer / overlayFadeSeconds) : (overlayTimer > 0f ? 1f : 0f);
        overlayGroup.blocksRaycasts = overlayGroup.alpha > 0.3f;
        if (overlayTimer <= 0f) overlayRoot.SetActive(false);
    }

    private void Start()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker != null && clicker.onTierUnlocked != null) clicker.onTierUnlocked.AddListener(OnTierUnlocked);
    }

    private void OnTierUnlocked(int index)
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null || !clicker.IsValidTierIndex(index)) return;
        ShowOverlay("New pixel unlocked: " + clicker.Tiers[index].displayName + "!", null);
    }

    private void Update()
    {
        UpdateOverlay();
        bool showing = PixelNotice.IsShowing;
        if (wasShowing && !showing) waitTimer = Mathf.Max(waitTimer, 0.3f); // short gap between queued tips
        wasShowing = showing;

        if (queue.Count == 0 || showing) return;
        if (waitTimer > 0f) { waitTimer -= Time.unscaledDeltaTime; return; }

        PixelNotice.Show(queue.Dequeue().text, autoCloseSeconds);
        wasShowing = true;
    }
}
