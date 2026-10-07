using System;
using System.Collections.Generic;
using UnityEngine;

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

    private static Hint H(string id, string text) => new Hint { id = id, text = text };

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
        H("ultra", "Ultra pixel earned! Spend Ultra pixels in the shop's Upgrades > Pixel sub-tab to boost a pixel type's payout."),
    };

    private bool EnsureDefaultHints()
    {
        if (!addDefaultHints) return false;
        if (hints == null) hints = new List<Hint>();
        bool changed = false;
        foreach (Hint d in DefaultHints())
        {
            if (hints.Exists(h => h != null && h.id == d.id)) continue;
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
        queue.Enqueue(hint);
        if (waitTimer <= 0f && !PixelNotice.IsShowing) waitTimer = showDelay;
    }

    private void Update()
    {
        bool showing = PixelNotice.IsShowing;
        if (wasShowing && !showing) waitTimer = Mathf.Max(waitTimer, 0.3f); // short gap between queued tips
        wasShowing = showing;

        if (queue.Count == 0 || showing) return;
        if (waitTimer > 0f) { waitTimer -= Time.unscaledDeltaTime; return; }

        PixelNotice.Show(queue.Dequeue().text, autoCloseSeconds);
        wasShowing = true;
    }
}
