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

    [Tooltip("Font size of the overlay line.")]
    [SerializeField] private float logFontSize = 20f;

    [Tooltip("Text colour of the overlay line.")]
    [SerializeField] private Color overlayTextColor = new Color(1f, 0.95f, 0.7f, 1f);

    [Tooltip("Draw the dark box behind the log and the coloured block behind each line. Off = just the text.")]
    [SerializeField] private bool showLogBackground = false;

    [Tooltip("Background colour behind the overlay line (used when Show Log Background is on).")]
    [SerializeField] private Color overlayBackColor = new Color(0f, 0f, 0f, 0.3f);

    [Tooltip("Distance from the left edge and from the black bar (canvas units).")]
    [SerializeField] private Vector2 overlayMargin = new Vector2(24f, 16f);

    [Min(100f)]
    [Tooltip("Width of the event log box (canvas units). Its height runs from above the bottom black bar up to the top black bar.")]
    [SerializeField] private float logWidth = 420f;

    [Tooltip("Sorting order of the event log's canvas. Kept below every other window (Inventory, Log, Shop, Crafting, Bank...). Renamed so scenes saved with an older, higher value pick up the new default.")]
    [SerializeField] private int eventLogSortingOrder = 5;

    [Tooltip("Background colour of each line (used when Show Log Background is on).")]
    [SerializeField] private Color overlayRowColor = new Color(0.1f, 0.18f, 0.28f, 0.8f);

    [Tooltip("Pixels the box scrolls per mouse-wheel notch.")]
    [SerializeField] private float overlayScrollSpeed = 70f;

    [Tooltip("Lines in the upper part of the event log slowly fade out towards the top of the box.")]
    [SerializeField] private bool fadeUpperLines = true;

    [Range(0f, 1f)]
    [Tooltip("Where the fade begins, as a fraction of the box's height from the bottom (0.5 = halfway up).")]
    [SerializeField] private float fadeStartFraction = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("How visible a line is at the very top of the box (0 = gone).")]
    [SerializeField] private float fadeTopAlpha = 0f;

    [Min(1)]
    [Tooltip("How many events the log keeps (saved with the save file).")]
    [SerializeField] private int historyMax = 100;


    [Header("Intro (first start of a new game)")]
    [Tooltip("On a brand-new game, open the Log and then the Inventory with a short explanation of each.")]
    [SerializeField] private bool showIntro = true;

    [Min(0f)]
    [Tooltip("Seconds to wait after the game starts (after Play) before the intro tips begin.")]
    [SerializeField] private float introDelay = 0.8f;

    [TextArea(2, 5)]
    [Tooltip("Tip shown with the Log open.")]
    [SerializeField] private string introLogText = "This is your Log. It lists every pixel type you have unlocked with its lifetime total, and its second tab tracks your achievements. Open it any time with the Log button at the bottom left.";

    [TextArea(2, 5)]
    [Tooltip("Third new-game intro tip (after the Log and Inventory ones): how you earn currency. Empty = the built-in text. The tier guide (\"Click X more...\") appears once this box is closed.")]
    [SerializeField] private string introEarnText = "Clicking the pixel is the main way to earn currency. As you play, upgrades, minigames and managing the old pixels that fall to the floor will open up other ways to earn.";

    [TextArea(2, 5)]
    [Tooltip("Tip shown with the Inventory open.")]
    [SerializeField] private string introInventoryText = "This is your Inventory. Currency shows how many of each pixel you hold - you spend them in the shop. Consumables holds your potions and devices. Open it any time with the Inventory button at the top left.";

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

    /// <summary>Distance from the left of the screen to the right edge of the event log (canvas units), for boxes placed beside it.</summary>
    public static float LogRightEdge => instance != null ? instance.overlayMargin.x + instance.logWidth + 20f : 440f;

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
            case "minigame_stardust": return "A star fell from the sky!";
            case "minigame_snake": return "Snake time!";
            case "minigame_sort": return "Sorting race!";
            case "minigame_breakout": return "Breakout!";
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
            case "upgrade_cap": return "Old Pixel Capacity upgraded!";
            case "upgrade_ultra": return "Ultra boost bought!";
            case "pixel_Glass": return "You unlocked Glass Pixels!";
            case "pixel_Vacuum": return "You unlocked Vacuum Pixels!";
            case "pixel_Obsidian": return "You unlocked Obsidian Pixels!";
            case "pixel_Luminescent": return "You unlocked Luminescent Pixels!";
            case "pixel_Singularity": return "You unlocked Singularity Pixels!";
            case "pixel_Ghost": return "You unlocked Ghost Pixels!";
            case "pixel_Meteor": return "You unlocked Meteor Pixels!";
            case "pixel_Electric": return "You unlocked Electric Pixels!";
            case "pixel_Mirror": return "You unlocked Mirror Pixels!";
            case "pixel_Seed": return "You unlocked Seed Pixels!";
            case "pixel_Water": return "You unlocked Water Pixels!";
            case "dragon_first": return "You found a Dragon Cube!";
            case "pixel_Red": return "You unlocked Red, Green and Blue Pixels!";
            case "bank_hose": return "Hose equipped!";
            case "bank_suck": return "Pixel stored in the Bank!";
            case "bank_spit": return "Spat out a stored pixel!";
            case "bank_full": return "The Bank is full!";
            case "timeslow": return "You unlocked Time Slow! (press S)";
            case "timestop_slow": return "Time slowed!";
            case "timestop_first": return "Time stopped!";
            case "timestop_empty": return "Time Stop ran out of energy!";
            default: return "";
        }
    }

    private static List<Hint> DefaultHints() => new List<Hint>
    {
        H("minigame_breakout", "Breakout! The old pixels on screen turned into a wall of bricks (the rest shrank away) and the cube stepped aside. Move the paddle along the bottom with the mouse or the Left / Right arrows to bounce the ball into them. Every brick you break pays 10x that pixel's worth; run out of balls and the rest simply drop."),
        H("minigame_sort", "Sorting race! Some old pixels froze in a jumbled grid. Click one pixel, then another, to swap them. Get every colour together in one unbroken group before the timer runs out - finishing early pays extra."),
        H("minigame_snake", "Snake! Every old pixel froze and stopped despawning. Steer the snake with the arrow keys (or the on-screen arrows) and eat every pixel without biting yourself - you are paid 10x their worth. Run out of time or bite yourself and they just drop."),
        H("minigame_stardust", "A meteor pixel knocked a star out of the sky! Click it to collect stardust before it fades. Enough stardust unlocks something new."),
        H("event_log", "This is the Event Log. It records what happens in the game, like unlocks. Scroll it with the mouse wheel, click a line to read its full tip, and press {key:EventLog} to show or hide it. You can switch it off in Settings > Interface > Event log."),
        H("crafting", "Crafting unlocked! Open the Crafting button at the bottom right, drag two items into the boxes and press Craft. A pixel + Glass makes that pixel's potion."),
        H("grab", "Pixel Grabbing unlocked! Hold the left mouse button on an old pixel to pick it up, then let go to throw it."),
        H("timestop", "Time Stop unlocked! Press T to freeze time and T again to resume. It drains an energy meter, but you can still click the cube - the old pixels pile up and burst out when time resumes."),
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
        H("upgrade_combo", "Combo Meter upgraded! Quick manual clicks build a combo; reaching a combo milestone boosts your payouts, and each level unlocks a higher milestone with a bigger bonus and a flashier meter."),
        H("upgrade_cap", "Old Pixel Capacity upgraded! More old pixels can lie around at once before the oldest ones are cleared away."),
        H("upgrade_bank", "Bank Storage upgraded! The Pixel Bank can now hold more stored pixels."),
        H("upgrade_ultra", "Ultra boost bought! That pixel type now pays more for every click. You can keep boosting it with more Ultra pixels."),
        H("pixel_Glass", "Glass Pixels unlocked! They are see-through, and you can combine one with another pixel in Crafting to make a potion."),
        H("pixel_Vacuum", "Vacuum Pixels unlocked! Clicking one sucks nearby old pixels in and collects them."),
        H("pixel_Obsidian", "Obsidian Pixels unlocked! A tougher pixel that takes more than one click to collect."),
        H("pixel_Luminescent", "Luminescent Pixels unlocked! They glow, and they are worth a lot more than the plain ones."),
        H("pixel_Singularity", "Singularity Pixels unlocked! A very rare pixel born from the black holes you fed."),
        H("pixel_Ghost", "Ghost Pixels unlocked! A rare pixel from all the ghosts you caught."),
        H("pixel_Electric", "Electric Pixels unlocked! A cube held together by crackling lightning."),
        H("pixel_Water", "Water Pixels unlocked! When an old Water pixel splashes on the ground near Seed sprouts or the Seed pet, they grow and sow much faster for a while. The water is used up either way. Stream them over your sprouts with a Sorter!"),
        H("pixel_Seed", "Seed Pixels unlocked! Old Seed pixels slowly grow bigger and worth more. Vacuum them, hose them up or click a ripe one to harvest."),
        H("pixel_Mirror", "Mirror Pixels unlocked! Clicking one also counts as a click on the last other pixel you collected, so it pays that pixel's amount again."),
        H("dragon_first", "A Dragon Cube! There are seven of them, each with a different number of small cubes inside, and they turn up extremely rarely among the pixels. Gather all seven and a button will appear to summon the cube dragon and make a wish."),
        H("pixel_Meteor", "Meteor Pixels unlocked! They streak away across the screen instead of falling."),
        H("pixel_Red", "The RGB Pack unlocked Red, Green and Blue pixels! They pay the same as the black, gray and white ones, but you need them to unlock Glass pixels and more."),
        H("bank_hose", "The hose is out! Scroll the mouse wheel to pick a stored pixel type. Left-click spits one out, right-click sucks up the nearest old pixel, and holding right-click sucks up everything of that type around the nozzle."),
        H("bank_suck", "Stored in the Bank! Open the Bank tab on the left edge to see what you have. Stored pixels keep their value when you spit them back out."),
        H("bank_spit", "Spat a stored pixel back out! It is worth what it was worth when you stored it."),
        H("bank_full", "The Bank is full! Spit some pixels out, or buy Bank Storage upgrades in the shop to hold more."),
        H("timeslow", "Time Slow unlocked! Press {key:TimeSlow} to slow time and again to return to normal. It uses the same energy meter as Time Stop, but drains it more slowly. Meteor pixels crawl while time is slow - grab one with Pixel Grabbing and put it where you like."),
        H("timestop_slow", "Time is slowed! Everything crawls - even meteor pixels, which you can now grab. The Time Stop meter drains slowly while it lasts; press {key:TimeSlow} to go back to normal speed."),
        H("timestop_first", "Time is stopped! Everything freezes, but you can keep clicking the cube. The meter at the top drains while time is stopped and refills once it runs again; press T to resume."),
        H("timestop_empty", "The Time Stop meter ran out, so time started again. It refills quickly while time runs - wait until it is a little full before stopping time again."),
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
        if (what == PixelMinigame.MinigameEvent.Spawned && game != null)
        {
            // Some minigames (the black hole) show their own first-time tip instead of one from the list: no warning for those.
            string id = "minigame_" + game.Id;
            if (hints != null && hints.Exists(h => h != null && h.id == id)) Trigger(id);
        }
    }

    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Player settings (Settings screen)
    // ------------------------------------------------------------------

    private const string PrefTips = "PixelClicker.Setting.Tips";
    private const string PrefEventLog = "PixelClicker.Setting.EventLog";
    private const string PrefLogSize = "PixelClicker.Setting.EventLogSize";

    /// <summary>Show the first-time tip boxes? (The event log still records them either way.)</summary>
    public static bool TipsEnabled
    {
        get => PlayerPrefs.GetInt(PrefTips, 1) != 0;
        set { PlayerPrefs.SetInt(PrefTips, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>Show the event log overlay at the bottom left? (Events are still recorded and saved.)</summary>
    public static bool EventLogEnabled
    {
        get => PlayerPrefs.GetInt(PrefEventLog, 1) != 0;
        set { PlayerPrefs.SetInt(PrefEventLog, value ? 1 : 0); PlayerPrefs.Save(); RefreshLog(); }
    }

    public static readonly string[] LogSizeNames = { "Small", "Medium", "Large" };
    private static readonly float[] LogSizeScale = { 0.8f, 1f, 1.3f };

    /// <summary>Event log text size: 0 = small, 1 = medium (the Inspector size), 2 = large.</summary>
    public static int LogSizeChoice
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefLogSize, 1), 0, LogSizeNames.Length - 1);
        set { PlayerPrefs.SetInt(PrefLogSize, Mathf.Clamp(value, 0, LogSizeNames.Length - 1)); PlayerPrefs.Save(); RefreshLog(); }
    }

    /// <summary>Rebuilds the event log lines (new text size) and hides it if it was switched off.</summary>
    public static void RefreshLog()
    {
        if (instance == null) return;
        instance.rowsDirty = true;
        if (!EventLogEnabled && instance.overlayRoot != null) instance.overlayRoot.SetActive(false);
    }

    private bool forceIntro; // "Replay tutorial": run the intro even though this is not a brand-new game

    /// <summary>Settings > Replay tutorial: forgets the shown tips and plays the intro tips (Log, Inventory) again.</summary>
    public static void ReplayTutorial()
    {
        ResetSeen();
        PixelGuideVendor.Replay(); // Cubie's visits are part of the tutorial
        if (instance == null) return;
        instance.forceIntro = true;
        instance.introChecked = false;
        instance.introWait = instance.introDelay; // start right away
        instance.queue.Clear();
    }

    /// <summary>Forgets which tips were shown (a brand-new game shows them all again).</summary>
    public static void ResetSeen()
    {
        try
        {
            if (instance != null && instance.hints != null)
                foreach (Hint h in instance.hints)
                    if (h != null && !string.IsNullOrEmpty(h.id)) PlayerPrefs.DeleteKey(PrefPrefix + h.id);
            PlayerPrefs.DeleteKey(PrefPrefix + "intro_log");
            PlayerPrefs.DeleteKey(PrefPrefix + "shop_tutorial");
            PlayerPrefs.DeleteKey(PrefPrefix + "BlackHole");
            PlayerPrefs.Save();
        }
        catch (Exception) { /* no PlayerPrefs: nothing to forget */ }
    }

    /// <summary>Shows the tip with this id the first time it is triggered (ever). Unknown / disabled / already-seen ids do nothing.</summary>
    public static void Trigger(string id)
    {
        if (instance != null) instance.Enqueue(id);
    }

    /// <summary>Has the tip / tutorial with this id been shown already? (Other tutorials use this.)</summary>
    public static bool IsTipSeen(string id) => Seen(id);

    /// <summary>Remembers that the tip / tutorial with this id was shown.</summary>
    public static void SetTipSeen(string id) => MarkSeen(id);

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

#if UNITY_EDITOR
    private readonly HashSet<string> warnedIds = new HashSet<string>();
#endif

    private void Enqueue(string id)
    {
        if (!hintsEnabled || string.IsNullOrEmpty(id) || Seen(id)) return; // (the tip boxes can also be switched off in Settings)
        Hint hint = hints != null ? hints.Find(h => h != null && h.id == id) : null;
#if UNITY_EDITOR
        if (hint == null && warnedIds.Add(id))
            Debug.LogWarning("PixelHints: Trigger(\"" + id + "\") has no matching hint in the list (typo, or add a Hint with this id).", this);
#endif
        if (hint == null || !hint.enabled || string.IsNullOrEmpty(hint.text)) return;
        MarkSeen(id);
        string full = hint.text;
        Announce(hint.shortText, full);
        if (!TipsEnabled) return; // the event log line is kept, the tip box is not shown
        if (!autoOpenTips) return;
        queue.Enqueue(hint);
        if (waitTimer <= 0f && !PixelNotice.IsShowing) waitTimer = showDelay;
    }

    // ------------------------------------------------------------------
    // The event log (bottom-left box)
    // ------------------------------------------------------------------

    /// <summary>One line of the event log.</summary>
    public class Entry
    {
        public string text;
        public string full; // the tip shown when the line is clicked (may be empty)
    }

    private readonly List<Entry> history = new List<Entry>();
    private float overlayTimer;
    private float scrollOffset;       // pixels scrolled up from the newest line
    private float suppressUntil;
    private bool rowsDirty;
    private GameObject overlayRoot;
    private CanvasGroup overlayGroup;
    private RectTransform boxRect, listRect, handleRect;
    private TMP_FontAsset overlayFont;
    private float contentHeight, viewHeight;
    private readonly List<RectTransform> rowRects = new List<RectTransform>();
    private readonly List<CanvasGroup> rowGroups = new List<CanvasGroup>();

    /// <summary>
    /// Adds a short line to the event log at the bottom left ("You unlocked the Auto Clicker!") and shows the box.
    /// Clicking the line shows 'fullText' in the tip box. Ignored right after the game starts or loads.
    /// </summary>
    public static void Announce(string shortText, string fullText = null)
    {
        if (instance != null) instance.ShowOverlay(shortText, fullText);
    }

    /// <summary>Ignores announcements for a moment (loading a save re-fires unlocks).</summary>
    public static void SuppressFor(float seconds)
    {
        if (instance != null) instance.suppressUntil = Time.realtimeSinceStartup + seconds;
    }

    /// <summary>The log of the current save, for the save file.</summary>
    public static void ExportHistory(out string[] texts, out string[] fulls)
    {
        List<Entry> list = instance != null ? instance.history : new List<Entry>();
        texts = new string[list.Count];
        fulls = new string[list.Count];
        for (int i = 0; i < list.Count; i++) { texts[i] = list[i].text; fulls[i] = list[i].full ?? ""; }
    }

    /// <summary>Replaces the log with the one from a save file (no box is shown).</summary>
    public static void ImportHistory(string[] texts, string[] fulls)
    {
        if (instance == null) return;
        instance.history.Clear();
        if (texts != null)
        {
            for (int i = 0; i < texts.Length; i++)
                instance.history.Add(new Entry { text = texts[i], full = fulls != null && i < fulls.Length ? fulls[i] : "" });
        }
        instance.TrimHistory();
        instance.rowsDirty = true;
        instance.scrollOffset = 0f;
        instance.overlayTimer = 0f;
        if (instance.overlayRoot != null) instance.overlayRoot.SetActive(false);
    }

    private void TrimHistory()
    {
        while (history.Count > historyMax) history.RemoveAt(0);
    }

    private void ShowOverlay(string shortText, string fullText)
    {
        if (!hintsEnabled || string.IsNullOrEmpty(shortText)) return;
        if (Time.timeSinceLevelLoad < startupQuietSeconds || Time.realtimeSinceStartup < suppressUntil) return;
        history.Add(new Entry { text = shortText, full = fullText ?? "" });
        TrimHistory();
        rowsDirty = true;
        scrollOffset = 0f;
        if (showOverlay) DisplayOverlay();
    }

    /// <summary>True while the whole log is showing (the player opened it); false = only the newest entry (a new event just happened).</summary>
    private bool fullView;

    private void DisplayOverlay(bool showAll = false)
    {
        if (history.Count == 0 || !EventLogEnabled) return;
        if (overlayRoot == null) BuildOverlay();
        if (fullView != showAll) { fullView = showAll; rowsDirty = true; }
        forceFade = false;
        overlayTimer = overlayVisibleSeconds + overlayFadeSeconds;
        overlayGroup.alpha = 1f;
        overlayRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RebuildRows();
    }

    private void BuildOverlay()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        overlayFont = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        overlayRoot = PixelUIKit.CreateCanvas("Pixel Event Log", eventLogSortingOrder, new Vector2(1920f, 1080f), true);
        overlayRoot.transform.SetParent(transform, false);
        overlayGroup = overlayRoot.AddComponent<CanvasGroup>();

        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        box.transform.SetParent(overlayRoot.transform, false);
        Image img = box.GetComponent<Image>();
        img.color = showLogBackground ? overlayBackColor : Color.clear;
        img.raycastTarget = false; // empty space in the box never blocks clicks
        boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 0f);
        boxRect.anchorMax = new Vector2(0f, 1f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.offsetMin = new Vector2(overlayMargin.x, bar + overlayMargin.y);
        boxRect.offsetMax = new Vector2(overlayMargin.x + logWidth, -(bar + overlayMargin.y));

        GameObject list = new GameObject("List", typeof(RectTransform));
        list.transform.SetParent(box.transform, false);
        listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 0f);
        listRect.pivot = new Vector2(0.5f, 0f);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = Vector2.zero;

        GameObject handle = new GameObject("Scroll Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(box.transform, false);
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.35f);
        handle.GetComponent<Image>().raycastTarget = false;
        handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(1f, 0f);
        handleRect.anchorMax = new Vector2(1f, 0f);
        handleRect.pivot = new Vector2(1f, 0f);
        handleRect.sizeDelta = new Vector2(6f, 40f);
        handleRect.anchoredPosition = new Vector2(-3f, 0f);
    }

    private void RebuildRows()
    {
        rowsDirty = false;
        if (listRect == null) return;
        for (int i = listRect.childCount - 1; i >= 0; i--) Destroy(listRect.GetChild(i).gameObject);
        rowRects.Clear();
        rowGroups.Clear();

        float width = logWidth - 40f;
        float y = 8f;
        int oldest = fullView ? 0 : history.Count - 1; // a new event shows only the newest line; the whole list is for when the player opens the log
        for (int i = history.Count - 1; i >= oldest; i--) // newest at the bottom
        {
            Entry entry = history[i];
                        string text = entry.text;

            GameObject row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            row.transform.SetParent(listRect, false);
            rowRects.Add(row.GetComponent<RectTransform>());
            rowGroups.Add(row.GetComponent<CanvasGroup>());
            row.GetComponent<Image>().color = showLogBackground ? overlayRowColor : Color.clear; // clear still takes clicks
            RectTransform rr = row.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(0f, 0f);

            TMP_Text label = PixelUIKit.CreateText(overlayFont, row.transform, "Text", text, logFontSize * LogSizeScale[LogSizeChoice],
                                                   TextAlignmentOptions.TopLeft, FontStyles.Bold, overlayTextColor);
            label.overflowMode = TextOverflowModes.Overflow;
            float h = Mathf.Ceil(label.GetPreferredValues(text, width, 0f).y) + 12f;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(10f, 6f); lr.offsetMax = new Vector2(-10f, -6f);

            rr.sizeDelta = new Vector2(width, h);
            rr.anchoredPosition = new Vector2(8f, y);
            y += h + 6f;

            string full = entry.full;
            Button b = row.GetComponent<Button>();
            if (!string.IsNullOrEmpty(full)) b.onClick.AddListener(() => PixelNotice.Show(full, autoCloseSeconds));
            else b.interactable = false;
        }

        contentHeight = y + 4f;
        listRect.sizeDelta = new Vector2(0f, contentHeight);
        ApplyScroll();
    }

    private void ApplyScroll()
    {
        if (boxRect == null) return;
        // The box's height computed from the screen (its own rect isn't valid until the canvas has updated once).
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        viewHeight = Mathf.Max(0f, Screen.height / scale - 2f * (bar + overlayMargin.y));
        float max = Mathf.Max(0f, contentHeight - viewHeight);
        scrollOffset = Mathf.Clamp(scrollOffset, 0f, max);
        // The list's bottom edge sits at the box's bottom (newest line); scrolling up (offset > 0) slides it DOWN to reveal older lines.
        listRect.anchoredPosition = new Vector2(0f, -scrollOffset);

        bool scrolls = max > 0.5f;
        if (handleRect.gameObject.activeSelf != scrolls) handleRect.gameObject.SetActive(scrolls);
        if (scrolls)
        {
            float handleH = Mathf.Max(30f, viewHeight * viewHeight / contentHeight);
            handleRect.sizeDelta = new Vector2(6f, handleH);
            handleRect.anchoredPosition = new Vector2(-3f, (viewHeight - handleH) * (scrollOffset / max));
        }
        UpdateRowFade();
    }

    /// <summary>Fades each line by how high it sits in the box: full below the start fraction, fading out towards the top.</summary>
    private void UpdateRowFade()
    {
        float start = viewHeight * fadeStartFraction;
        float span = Mathf.Max(1f, viewHeight - start);
        for (int i = 0; i < rowRects.Count; i++)
        {
            RectTransform r = rowRects[i];
            CanvasGroup g = rowGroups[i];
            if (r == null || g == null) continue;
            float alpha = 1f;
            if (fadeUpperLines && viewHeight > 1f)
            {
                float centre = r.anchoredPosition.y + r.sizeDelta.y * 0.5f - scrollOffset; // height above the box's bottom
                float t = Mathf.Clamp01((centre - start) / span);
                alpha = Mathf.Lerp(1f, fadeTopAlpha, Mathf.SmoothStep(0f, 1f, t));
            }
            g.alpha = alpha;
            g.blocksRaycasts = alpha > 0.15f; // nearly invisible lines don't catch clicks
        }
    }

    private bool forceFade;

    /// <summary>Makes the open log fade away now, even while the mouse is over it.</summary>
    private void FadeNow()
    {
        forceFade = true;
        overlayTimer = Mathf.Min(overlayTimer, overlayFadeSeconds);
    }

    private bool ReopenKeyPressed()
    {
        return PixelKeys.Pressed(PixelAction.EventLog); // rebindable in Settings
    }

    private static float ScrollWheel()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    private void UpdateOverlay()
    {
        if (PixelPauseMenu.IsPaused || history.Count == 0) return;

        // Don't steal Enter from a text box.
        UnityEngine.EventSystems.EventSystem es = UnityEngine.EventSystems.EventSystem.current;
        bool typing = es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;

        bool visible = overlayRoot != null && overlayRoot.activeSelf;
        if (!EventLogEnabled)
        {
            if (overlayRoot != null && overlayRoot.activeSelf) overlayRoot.SetActive(false);
            return;
        }
        if (!typing && ReopenKeyPressed())
        {
            if (visible && fullView) FadeNow();                   // the full log is open: close it
            else { scrollOffset = 0f; DisplayOverlay(true); }     // closed, or showing just the newest line: open the whole log
            return;
        }
        if (visible && PixelInput.RightPressed()) FadeNow(); // right-click anywhere closes the log

        if (!visible) return;
        if (rowsDirty) RebuildRows();

        // Hovering the box keeps it open and the mouse wheel scrolls it.
        bool hover = !forceFade && RectTransformUtility.RectangleContainsScreenPoint(boxRect, PixelInput.PointerPosition(), null);
        if (hover)
        {
            float wheel = ScrollWheel();
            if (Mathf.Abs(wheel) > 0.001f) { scrollOffset += wheel * overlayScrollSpeed; ApplyScroll(); }
            overlayTimer = overlayVisibleSeconds + overlayFadeSeconds;
        }

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
        ShowOverlay("New pixel unlocked: " + clicker.Tiers[index].displayName + "!", "");
        Trigger("event_log"); // the very first line of the log: explain what the log is
    }

    private bool introChecked, freshAtStart = true;
    private bool introRunning;
    private float introIdle;

    /// <summary>True from the start of a new game until the intro tips (Log, Inventory) have been closed: the "Click 100 more..." guide stays hidden meanwhile.</summary>
    public static bool IntroHoldsGuide => instance != null && instance.IntroHoldsGuideNow;

    private bool IntroHoldsGuideNow
    {
        get
        {
            if (introRunning) return true;
            if (introChecked) return false;
            if (!showIntro || !hintsEnabled || !TipsEnabled) return false;
            return forceIntro || (freshAtStart && !Seen("intro_log"));
        }
    }

    private static bool AnyCollected()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return false;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (t.totalCollected > 0d) return true;
        return false;
    }
    private float introWait;

    private void TryStartIntro()
    {
        if (introChecked) return;
        if (PixelTitleScreen.Showing)
        {
            freshAtStart = !AnyCollected(); // judged on the title screen, before the player can click anything
            return;
        }
        introWait += Time.unscaledDeltaTime; // counts from the moment the game starts (after Play)
        if (introWait < introDelay) return;
        introChecked = true;
        if (!showIntro || !hintsEnabled || !TipsEnabled || Seen("intro_log")) return;

        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        if (!forceIntro && !freshAtStart) return; // not a new game
        bool replay = forceIntro;
        forceIntro = false;

        introRunning = true;
        introIdle = 0f;
        PixelLog.SetLogOpen(true);
        PixelNotice.Show(introLogText, 0f, compact: true, beside: PixelLog.WindowRect, besideBottom: true, onClosed: () =>
        {
            PixelLog.SetLogOpen(false);
            PixelUI.SetInventoryOpen(true);
            PixelNotice.Show(introInventoryText, 0f, compact: true, beside: PixelUI.WindowRect, onClosed: () =>
            {
                PixelUI.SetInventoryOpen(false);
                // Third tip: how currency is earned. The tier guide ("Click X more...") shows once it is closed.
                string earn = string.IsNullOrEmpty(introEarnText)
                    ? "Clicking the pixel is the main way to earn currency. As you play, upgrades, minigames and managing the old pixels that fall to the floor will open up other ways to earn."
                    : introEarnText;
                PixelNotice.Show(earn, 0f, compact: true, onClosed: () =>
                {
                    introRunning = false; // the tier guide may show now
                    MarkSeen("intro_log"); // only counts once the player has been through all the tips
                    if (replay) ShowEventLogTip(); // the tutorial's last step: what the event log is
                });
            });
        });
    }

    /// <summary>Shows the event log (bottom left) and the small tip beside it explaining it.</summary>
    private void ShowEventLogTip()
    {
        Hint hint = hints != null ? hints.Find(h => h != null && h.id == "event_log") : null;
        if (hint == null || !hint.enabled || string.IsNullOrEmpty(hint.text)) return;
        MarkSeen("event_log"); // so the first pixel unlock doesn't repeat it
        DisplayOverlay(true);
        overlayTimer = Mathf.Max(overlayTimer, 20f); // keep the log up while the tip is read
        PixelNotice.Show(hint.text, autoCloseSeconds, nearLog: true);
        wasShowing = true;
    }

    private bool windowWasOpen;

    private void Update()
    {
        TryStartIntro();
        // The event log steps aside the moment any window opens.
        bool windowOpen = PixelWindows.AnyOpen();
        if (windowOpen && !windowWasOpen && overlayRoot != null && overlayRoot.activeSelf) FadeNow();
        windowWasOpen = windowOpen;
        UpdateOverlay();
        bool showing = PixelNotice.IsShowing;
        if (introRunning)
        {
            // Safety: if the intro tips were replaced by another tip, don't hide the guide forever.
            introIdle = showing ? 0f : introIdle + Time.unscaledDeltaTime;
            if (introIdle > 2f) introRunning = false;
        }
        if (wasShowing && !showing) waitTimer = Mathf.Max(waitTimer, 0.3f); // short gap between queued tips
        wasShowing = showing;

        if (queue.Count == 0 || showing) return;
        if (waitTimer > 0f) { waitTimer -= Time.unscaledDeltaTime; return; }

        Hint next = queue.Dequeue();
        // The tips for something that just happened in a minigame are small; the rest are shop-sized.
        // The first ghost tip appears beside the ghost and stops time until it is closed.
        PixelGhostMinigame ghostGame = next.id == "minigame_ghost" ? PixelMinigame.Find("ghost") as PixelGhostMinigame : null;
        if (ghostGame != null && ghostGame.GhostScreenPosition().HasValue)
        {
            PixelNotice.Show(next.text, 0f, small: true, nearScreen: ghostGame.GhostScreenPosition, freezeTime: true);
            wasShowing = true;
            return;
        }
        PixelNotice.Show(next.text, autoCloseSeconds, small: next.id != null && (next.id.StartsWith("minigame_") || next.id.StartsWith("timestop_") || next.id.StartsWith("bank_")),
                         aboveCombo: next.id == "combo", nearLog: next.id == "event_log"); // the first combo tip sits just above the combo meter
        wasShowing = true;
    }
}
