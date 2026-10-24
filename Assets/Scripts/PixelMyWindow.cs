// ---- System notes (moved here from CLAUDE.md) ----
// PixelMyWindow.cs   (the window is called POCKET now; the class and the saved keys keep the old name)
//   POCKET'S PINNED TAB IS A BOARD OF WIDGETS (the shared PixelCardBoard, like the Dev Hub's): each pinned thing is a card (BuildCard) with a header you DRAG by (the cards slide round each other, the order is saved in the
//   entries), a '-' that folds it up to its header (Entry.min, saved), and a small x you HOLD with the left button (0.6 s, a ring round the cursor, the card turns red: UpdateRemoveHold) to remove it. Under the last card a BLANK
//   card with a + opens a search (OpenPicker / FillPicker: every currency, skill, goal, tool, item, achievement and a note, filtered as you type); picking one pins it and a new blank card follows. With no card left only the
//   blank one shows. There is no Edit mode any more. (Old text below that talks about Edit mode / rows describes the earlier list.)
//   My Window (the player's own window; component added by PixelClicker.Awake): a docked 'My Window' button on the right edge under
//   the Pixel Guide (shows once two pixel types are unlocked, showFromStart) opens ONE free window (canvas 107, not registered with
//   PixelWindows, not closed by other windows) that is dragged by its header, resized by the corner handle (position / size / open
//   state are global PlayerPrefs PixelClicker.MyWindow.*) and holds a vertical LIST of rows chosen in Edit mode (an add bar: type +
//   target dropdowns + Add; each row gets ^ v x buttons, currencies a '/min' toggle). Row types (Entry.type): currency (one pixel
//   type, optional per-minute rate sampled over 60 s), skill (a chosen skill, 'training' = follows the skill in training, 'all' = one
//   row per available skill), goal ('next', 'all' = every row of the Log's Goals tab via PixelLog.CurrentGoals(), or mg:<id> a
//   minigame tracker), achievement (PixelAchievements id), note (an input field) and fav (a button: item:<name> uses a consumable
//   through PixelConsumables.TryUseItem, tool:<id> = the Pixel hose (PixelBank.ToggleHose) or opens a window via PixelMenuKeys.Click).
//   The rows are saved per save file (JSON in PlayerPrefs PixelClicker.Setting.MyWindow, listed in PixelSettingsSync.Known_, reloaded
//   by PixelMyWindow.ReloadFromPrefs). Values refresh every refreshSeconds (0 = 0.4); rows are rebuilt when what an 'all' row expands
//   to changes.
//   Layout (current): opens as a tall window (defaultSize 0 = 450 x 660) at the right side just above the bottom black bar (layout version 2 ignores older saved
//   geometry). WINDOW: a standard Tool window (PixelUIKit.CreateWindow, prefKey PixelClicker.MyWindow): centred title, grey X, its PixelWindowChrome drags it by the header
//   (kept between the black bars), minimizes it ('-') and docks it to a screen edge, saving X / Y / Min / Dock / DockY; this script only saves W / H / Open / Layout. MINIMIZED: only the
//   header remains (still draggable); the full height is remembered. EVENT LOG TAB (always there, Pinned | Event log | Keybinds; the old header LOG dock button is gone):
//   the log lines of PixelHints.TryGetHistory, newest at the bottom, a click shows the full tip; a switch at its top turns the IN-GAME (bottom-left) event log on / off (= PixelHints.EventLogEnabled,
//   Settings > Event log). While the tab is showing the overlay stays away (LogTabShowing); with the in-game log off, the event log key opens the tab.
//   INFO button in the header (PixelMyWindow.HoverDocked, kept on this machine): pins the block HOVER TOOLTIP into the window (MINIMIZED window: a small card with
//   the block and its name hangs just under the header instead - TryGetHoverSlot's 'compact'): while it is on and the window is open (Pinned tab) the tooltip card
//   no longer follows the mouse - PixelBlockTooltip moves its card into the window's hover slot (TryGetHoverSlot / SetHoverHeight), scaled to the window's width, and it shows whatever the mouse hovers (and stays on the last
//   one while the mouse is over the window); a hint shows until something was hovered.
//   TABS (always shown under the header): Pinned | Event log (only while the log is docked) | Keybinds (PixelCustomKeys.BuildView: bind items / tools to your own keys).
//   Header buttons (LayoutHeaderButtons): Info sits in the first right slot after the X and the chrome's '-' (one slot further in while the chrome's undock arrow shows), Edit
//   (hidden, no Edit mode) after it; the title stays centred clear of them. Layout reruns when the chrome minimizes / docks (its StateChanged event). The corner grip still
//   changes the window's WIDTH / HEIGHT (ResizeBy -> PixelWindowParts.SetHeight), not its scale.
//   Part files (partial class): PixelMyWindowCards.cs (the Pinned board of cards + the picker), PixelMyWindowRows.cs (the row builders per type).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// <b>My Window</b>: the player's own window, one list they fill with what they care about. It can be dragged anywhere and resized from the
/// corner, and sits in the screen like the pinned Inventory (it is not closed by other windows). Press <b>Edit</b> to add, reorder
/// and remove rows. A row can be: a single currency (optionally with a per-minute rate), a skill bar (a chosen skill, the skill in training, or all skills),
/// a goal (the next goal, all current goals, or one minigame's goal), a pinned achievement, a note, or a favourite - a consumable you can use
/// with one click, or a tool (the Pixel hose, or a button that opens the Shop, Inventory, Log and so on). The rows are saved per save file
/// (PlayerPrefs <c>PixelClicker.Setting.MyWindow</c>, synced by <see cref="PixelSettingsSync"/>); the window's place, size and open state are
/// global.
/// </summary>
public partial class PixelMyWindow : MonoBehaviour
{
    [Header("Pocket")]
    [Tooltip("Show the docked 'Pocket' button from the start. Off = it appears once two pixel types are unlocked (like the Pixel Guide).")]
    [SerializeField] private bool showFromStart = false;

    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Pocket";

    [Tooltip("Starting size of the window (canvas units). 0 = the coded default (450 x 660, tall).")]
    [SerializeField] private Vector2 defaultSize = Vector2.zero;

    [Tooltip("Canvas sorting order (below the Shop / Skills windows, above the game). 0 = the coded default (107).")]
    [SerializeField] private int sortingOrder = 0;

    [Min(0f)]
    [Tooltip("Seconds between value refreshes. 0 = the coded default (0.4).")]
    [SerializeField] private float refreshSeconds = 0f;

    [Tooltip("Background colour of each row card in the window.")]
    [SerializeField] private Color rowColor = new Color(0.16f, 0.2f, 0.3f, 0.9f);
    private Color textColor => PixelUITheme.TextColor;   // (the theme: PixelUITheme)
    [Tooltip("Colour of the dim detail text (row details, empty-list notes).")]
    [SerializeField] private Color dimTextColor = new Color(0.7f, 0.76f, 0.88f, 1f);
    [Tooltip("Fill colour of the progress bars (unfinished ones; finished bars turn gold).")]
    [SerializeField] private Color barColor = new Color(0.3f, 0.8f, 0.45f, 1f);

    // ------------------------------------------------------------------
    // Data
    // ------------------------------------------------------------------

    [Serializable]
    private class Entry
    {
        public string type = "note";   // currency, skill, goal, achievement, note, fav
        public string key = "";
        public bool flag;              // currency: show the per-minute rate
        public string text = "";       // note text
        public bool min;               // the widget is folded up to its header
    }

    [Serializable]
    private class Blob { public List<Entry> entries = new List<Entry>(); }

    private class Row
    {
        public Entry entry;
        public string sub = "";
        public RectTransform rt;
        public float height;
        public Action refresh;
        public bool first;
    }

    private static readonly Vector2 MinSize = new Vector2(400f, 300f);
    private const string PrefEntries = "PixelClicker.Setting.MyWindow";
    private const string PrefPrefix = "PixelClicker.MyWindow.";
    private static PixelMyWindow instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Called after a save is loaded: the rows belong to the save file.</summary>
    /// <summary>Opens My Window, or closes it when it is open (the button in the Toggles window).</summary>
    public static void ToggleOpen()
    {
        if (instance == null || instance.canvasRoot == null) return;
        instance.open = !instance.open;
        instance.ApplyOpen();
    }

    /// <summary>Is the window available yet (it shows up once two pixel types are unlocked)?</summary>
    public static bool Available => instance != null && instance.buttonAvailable;

    private const string PrefHoverDocked = "PixelClicker.MyWindow.HoverDocked";

    /// <summary>Is the hover tooltip pinned into My Window (the player's own choice, kept on this machine)? While it is and My Window is open, the block tooltip no longer follows the mouse: whatever is hovered shows in the window.</summary>
    public static bool HoverDocked => PixelPrefs.GetInt(PrefHoverDocked, 0) != 0;

    private RectTransform hoverSlot;
    private TMP_Text hoverPlaceholder;
    private Button hoverDockButton;
    private Image hoverDockImage;
    private bool? shownHoverDocked;   // the Info button's last shown state, and the tab last shown as Selected (0 pinned, 1 log, 2 keys; -1 = not yet)
    private int shownTab = -1;
    private float hoverShownH = -1f, hoverLaidH = -1f;

    /// <summary>The place in the window where the pinned hover card goes (null unless the tooltip is pinned and the window is open on its Pinned tab).
    /// MINIMIZED: the slot hangs just under the header and 'compact' asks for the small card (the block and its name only).</summary>
    public static bool TryGetHoverSlot(out RectTransform slot, out float width, out bool compact)
    {
        slot = null; width = 0f; compact = false;
        PixelMyWindow w = instance;
        if (w == null || w.canvasRoot == null || w.hoverSlot == null || !w.open || !HoverDocked) return false;
        if (!w.minimized && (w.showLogTab || w.showKeysTab)) return false;
        slot = w.hoverSlot;
        compact = w.minimized;
        width = Mathf.Max(200f, w.windowRect.sizeDelta.x - 24f);
        return true;
    }

    /// <summary>The pinned hover card tells the window how tall it is (0 = nothing hovered yet: a hint shows instead).</summary>
    public static void SetHoverHeight(float height)
    {
        PixelMyWindow w = instance;
        if (w == null) return;
        w.hoverShownH = height;
        if (Mathf.Abs(height - w.hoverLaidH) > 1f) w.Layout();
    }

    /// <summary>Is the window open on its Event log tab right now? (The bottom-left log overlay stays away meanwhile, so the log doesn't show twice.)</summary>
    public static bool LogTabShowing => instance != null && instance.canvasRoot != null && instance.open && !instance.minimized && instance.showLogTab;

    /// <summary>Opens My Window on its Event log tab (the event log key does this while the on-screen log is switched off).</summary>
    public static void ShowLogTab()
    {
        if (instance == null || instance.canvasRoot == null) return;
        instance.showLogTab = true;
        if (instance.minimized) instance.chrome.SetMinimized(false);
        if (!instance.open) { instance.open = true; instance.ApplyOpen(); }
        instance.Layout();
        instance.RebuildLog();
    }

    /// <summary>Can things be pinned to My Window right now (the window exists)? The Game Database's Pin button uses this.</summary>
    public static bool CanPin => instance != null && instance.canvasRoot != null;

    /// <summary>Is a row of this type (currency, skill, achievement, fav...) and key already pinned?</summary>
    public static bool IsPinned(string type, string key) => instance != null && instance.blob.entries.Exists(e => e.type == type && e.key == key);

    /// <summary>Pins the row, or takes it away again when it is already pinned. Returns true when it is pinned afterwards.</summary>
    public static bool TogglePin(string type, string key)
    {
        if (instance == null) return false;
        int at = instance.blob.entries.FindIndex(e => e.type == type && e.key == key);
        if (at >= 0) instance.blob.entries.RemoveAt(at);
        else instance.blob.entries.Add(new Entry { type = type, key = key });
        instance.SaveEntries();
        if (instance.open) instance.RebuildRows();
        return at < 0;
    }

    /// <summary>Is the window open right now?</summary>
    public static bool IsOpen => instance != null && instance.open;

    public static void ReloadFromPrefs()
    {
        if (instance != null) instance.LoadEntries();
    }

    private Blob blob = new Blob();
    private readonly List<Row> rows = new List<Row>();
    private string rowSignature = "";

    private PixelClicker clicker;
    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;
    private GameObject canvasRoot, windowObject;
    private Canvas canvas;
    private RectTransform canvasRect, windowRect, listContent, addBar;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_Text editLabel;
    private TMP_Dropdown typeDropdown, targetDropdown;
    private readonly List<string> targetKeys = new List<string>();
    private bool open, editing;
    // The window is a standard Tool window (PixelUIKit.CreateWindow): its PixelWindowChrome owns dragging, minimizing and docking (and saves X / Y / Min / Dock / DockY).
    private PixelWindowParts parts;
    private PixelWindowChrome chrome;
    private bool minimized => chrome != null && chrome.Minimized;
    private float fullHeight = 660f;          // the window's height when not minimized (saved as H; the resize grip changes it)
    private Button tabPinnedButton, tabLogButton, tabKeysButton;
    private Image tabKeysImage;
    private RectTransform keysRoot;
    private bool showKeysTab;         // the Keybinds tab is showing (PixelCustomKeys builds its contents)
    private Image tabPinnedImage, tabLogImage;
    private Button screenLogButton;   // top of the Event log tab: the in-game event log on / off
    private GameObject editButtonObject, resizeHandleObject, tabBar;
    private TMP_Text titleLabel;
    private ScrollRect logScroll;
    private RectTransform logContent;
    private GameObject logScrollBar;
    private bool showLogTab;          // the Event log tab is showing (only while the log is docked)
    private int logVersionShown = -1;
    private float logContentHeight;
    private const float TabBarHeight = 46f;
    private const float HeaderHeight = PixelUIKit.ToolHeaderHeight, EdgeMargin = 14f;
    private float refreshTimer, saveTimer, hudTimer;
    private bool geometryDirty;
    private bool buttonAvailable;

    private static readonly string[] TypeNames = { "Currency", "Skill", "Goal", "Achievement", "Favorite", "Note" };
    private static readonly string[] TypeKeys = { "currency", "skill", "goal", "achievement", "fav", "note" };

    private static readonly (string id, string name, PixelAction action)[] Tools =
    {
        ("hose", "Pixel hose", PixelAction.Hose),
        ("inventory", "Open Inventory", PixelAction.Inventory),
        ("shop", "Open Shop", PixelAction.Shop),
        ("log", "Open Log", PixelAction.LogWindow),
        ("crafting", "Open Crafting", PixelAction.Crafting),
        ("toggles", "Open Toggles", PixelAction.Toggles),
        ("bank", "Open Pixel Bank", PixelAction.Bank),
        ("skills", "Open Skills", PixelAction.Skills),
    };

    /// <summary>A tool a favourite / keybind can use: its display name and the menu action it clicks (the hose is handled by the caller).</summary>
    public static bool TryGetTool(string id, out string name, out PixelAction action)
    {
        foreach (var t in Tools) if (t.id == id) { name = t.name; action = t.action; return true; }
        name = null; action = PixelAction.Hose;
        return false;
    }

    /// <summary>The ids of every tool.</summary>
    public static IEnumerable<string> ToolIds() { foreach (var t in Tools) yield return t.id; }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        instance = this;
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void Start()
    {
        BuildUI();
        LoadEntries();
        open = PixelPrefs.GetInt(PrefPrefix + "Open", 0) != 0;
        Layout();
        ApplyOpen();
        if (!open) windowObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private void LoadEntries()
    {
        string json = PixelPrefs.GetString(PrefEntries, "");
        blob = new Blob();
        if (!string.IsNullOrEmpty(json))
        {
            try { blob = JsonUtility.FromJson<Blob>(json) ?? new Blob(); } catch (Exception) { blob = new Blob(); }
        }
        if (blob.entries == null) blob.entries = new List<Entry>();
        rowSignature = "";   // force a rebuild
        if (canvasRoot != null) RebuildRows();
    }

    private void SaveEntries()
    {
        PixelPrefs.SetString(PrefEntries, JsonUtility.ToJson(blob));
    }

    private void SaveGeometry()
    {
        geometryDirty = false;
        // (X / Y / Min / Dock / DockY are saved by the window's PixelWindowChrome under the same keys.)
        PixelPrefs.SetFloat(PrefPrefix + "W", windowRect.sizeDelta.x);
        PixelPrefs.SetFloat(PrefPrefix + "H", fullHeight);
        PixelPrefs.SetInt(PrefPrefix + "Open", open ? 1 : 0);
        PixelPrefs.SetInt(PrefPrefix + "Layout", 2);
    }

    // ------------------------------------------------------------------
    // Window
    // ------------------------------------------------------------------

    private bool ShowButton()
    {
        if (showFromStart || clicker == null) return true;
        int unlocked = 0;
        foreach (PixelClicker.PixelTier t in clicker.Tiers) if (t.unlocked && ++unlocked >= 2) return true;
        return false;
    }

    private void BuildUI()
    {
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("My Window Canvas", sortingOrder > 0 ? sortingOrder : 107, new Vector2(1920f, 1080f), true);
        canvas = canvasRoot.GetComponent<Canvas>();
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        // (No docked button any more: it is opened from the small button at the top left of the Toggles window, see PixelMyWindow.ToggleOpen.)

        Vector2 size = defaultSize.x > 100f && defaultSize.y > 100f ? defaultSize : new Vector2(450f, 660f);
        bool saved = PixelPrefs.GetInt(PrefPrefix + "Layout", 0) == 2;   // (older saved sizes are ignored once: the window now opens tall at the right)
        if (saved) size = new Vector2(Mathf.Max(MinSize.x, PixelPrefs.GetFloat(PrefPrefix + "W", size.x)), Mathf.Max(MinSize.y, PixelPrefs.GetFloat(PrefPrefix + "H", size.y)));
        fullHeight = size.y;

        // A standard Tool window: centred title, grey X, drag by the header, minimize '-', dock to a screen edge (PixelWindowChrome; its place is saved
        // under PixelClicker.MyWindow.X / Y / Min / Dock / DockY, the keys this window always used).
        parts = PixelUIKit.CreateWindow(Font, canvasRoot.transform, "My Window", windowTitle == "My Window" ? "Pocket" : windowTitle, size,
                                        PixelUIKit.WindowKind.Tool, () => { open = false; ApplyOpen(); }, "PixelClicker.MyWindow");
        windowObject = parts.Root;
        windowRect = parts.Rect;
        chrome = parts.Chrome;
        titleLabel = parts.Title;
        chrome.defaultPlacer = cs => new Vector2(cs.x - windowRect.sizeDelta.x - 22f, -(cs.y - BarInset() - fullHeight));   // first time: right side, just above the bottom bar
        chrome.StateChanged += Layout;   // (minimized / restored, docked / undocked: lay the window out at once)

        // Info (a right header slot): pins the hover tooltip into this window (and unpins it).
        hoverDockButton = parts.AddHeaderButton("Dock Info", "Info", false);
        hoverDockImage = hoverDockButton.GetComponent<Image>();
        shownHoverDocked = null; shownTab = -1;   // (a fresh build: show the states again)
        FitOneLine(hoverDockButton, 18f);
        hoverDockButton.onClick.AddListener(ToggleHoverDock);

        // Edit (the next right slot): hidden since the Pinned tab became a board of widgets (no Edit mode), kept for the old list's add bar.
        Button edit = parts.AddHeaderButton("Edit", "Edit", false, ButtonRole.Primary);
        FitOneLine(edit, 18f);
        editLabel = edit.GetComponentInChildren<TMP_Text>();
        edit.onClick.AddListener(() => { editing = !editing; RebuildRows(); });
        editButtonObject = edit.gameObject;

        // Tabs under the header while the log is docked: Pinned | Event log.
        GameObject tabs = new GameObject("Tabs", typeof(RectTransform));
        tabs.transform.SetParent(windowObject.transform, false);
        tabBar = tabs;
        RectTransform tbr = tabs.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f); tbr.anchorMax = new Vector2(1f, 1f); tbr.pivot = new Vector2(0.5f, 1f);
        tbr.sizeDelta = new Vector2(-24f, TabBarHeight - 6f);
        tbr.anchoredPosition = new Vector2(0f, -(HeaderHeight + 4f));
        tabPinnedButton = MakeTab(tabs.transform, "Pinned", 0f, 0.5f, out tabPinnedImage);
        tabLogButton = MakeTab(tabs.transform, "Event log", 0.5f, 1f, out tabLogImage);
        tabKeysButton = MakeTab(tabs.transform, "Keybinds", 0.5f, 1f, out tabKeysImage);
        tabPinnedButton.onClick.AddListener(() => { showLogTab = false; showKeysTab = false; Layout(); });
        tabLogButton.onClick.AddListener(() => { showLogTab = true; showKeysTab = false; Layout(); RebuildLog(); });
        tabKeysButton.onClick.AddListener(() => { showLogTab = false; showKeysTab = true; Layout(); });
        tabs.SetActive(false);

        // The place for the pinned hover card (under the header / tabs); PixelBlockTooltip puts its card in here.
        GameObject slotGo = new GameObject("Hover Slot", typeof(RectTransform));
        slotGo.transform.SetParent(windowObject.transform, false);
        hoverSlot = slotGo.GetComponent<RectTransform>();
        hoverSlot.anchorMin = new Vector2(0f, 1f); hoverSlot.anchorMax = new Vector2(1f, 1f); hoverSlot.pivot = new Vector2(0.5f, 1f);
        chrome.Keep(slotGo);   // (Layout shows / hides it: minimized, the small hover card hangs just under the header)
        hoverPlaceholder = PixelUIKit.CreateText(Font, hoverSlot, "Hint", "Hover over a block to see its info here", 20f, TextAlignmentOptions.Center, FontStyles.Italic, new Color(1f, 1f, 1f, 0.55f));
        hoverPlaceholder.raycastTarget = false;
        PixelUIKit.Stretch(hoverPlaceholder.rectTransform);
        slotGo.SetActive(false);

        // Add bar (edit mode): what to add, which one, the Add button.
        GameObject bar = new GameObject("Add Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(windowObject.transform, false);
        bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        PixelUIKit.StyleBox(bar.GetComponent<Image>(), true);
        addBar = bar.GetComponent<RectTransform>();
        addBar.anchorMin = new Vector2(0f, 1f); addBar.anchorMax = new Vector2(1f, 1f); addBar.pivot = new Vector2(0.5f, 1f);
        addBar.sizeDelta = new Vector2(-24f, 164f);
        addBar.anchoredPosition = new Vector2(0f, -(HeaderHeight + 4f));

        Color box = new Color(0.16f, 0.2f, 0.3f, 1f);
        typeDropdown = PixelUIKit.CreateDropdown(Font, bar.transform, "Type", new Vector2(170f, 40f), box, box, textColor, 21f);
        typeDropdown.ClearOptions();
        typeDropdown.AddOptions(new List<string>(TypeNames));
        SetRect(typeDropdown.GetComponent<RectTransform>(), 8f, 8f, 0f, 42f);
        typeDropdown.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        typeDropdown.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -8f);
        targetDropdown = PixelUIKit.CreateDropdown(Font, bar.transform, "Target", new Vector2(250f, 40f), box, box, textColor, 21f);
        SetRect(targetDropdown.GetComponent<RectTransform>(), 8f, 58f, 0f, 42f);
        targetDropdown.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        targetDropdown.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -58f);
        typeDropdown.onValueChanged.AddListener(_ => FillTargets());
        Button add = PixelUIKit.CreateButton(Font, bar.transform, "Add", "+ Add to my window", new Vector2(300f, 44f), ButtonRole.Success, 22f);
        SetRect(add.GetComponent<RectTransform>(), 8f, 110f, 0f, 44f);
        add.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        add.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -110f);
        add.onClick.AddListener(AddSelected);

        // The list.
        scroll = PixelUIKit.CreateScrollView(windowObject.transform, "List", new Color(1f, 1f, 1f, 0.35f), 12f, 60f, out listContent, out scrollBar);
        PixelScrollBarFade fade = scrollBar != null ? scrollBar.GetComponent<PixelScrollBarFade>() : null;
        if (fade != null) fade.hideBar = true;

        // The docked event log's list (shown instead of the pinned list on the Event log tab).
        logScroll = PixelUIKit.CreateScrollView(windowObject.transform, "Event Log List", new Color(1f, 1f, 1f, 0.35f), 12f, 60f, out logContent, out logScrollBar);
        PixelScrollBarFade logFade = logScrollBar != null ? logScrollBar.GetComponent<PixelScrollBarFade>() : null;
        if (logFade != null) logFade.hideBar = true;
        logScroll.gameObject.SetActive(false);
        screenLogButton = PixelUIKit.CreateButton(Font, windowObject.transform, "In-game Log", "", new Vector2(200f, 44f), ButtonRole.Neutral, 22f);
        screenLogButton.onClick.AddListener(ToggleScreenLog);
        FitOneLine(screenLogButton, 22f);
        screenLogButton.gameObject.SetActive(false);

        // The Keybinds tab (PixelCustomKeys builds what is in it).
        GameObject keysGo = new GameObject("Keybinds", typeof(RectTransform));
        keysGo.transform.SetParent(windowObject.transform, false);
        keysRoot = keysGo.GetComponent<RectTransform>();
        keysRoot.anchorMin = Vector2.zero; keysRoot.anchorMax = Vector2.one;
        PixelCustomKeys keys = PixelCustomKeys.Instance != null ? PixelCustomKeys.Instance : PixelFind.First<PixelCustomKeys>();
        if (keys != null) keys.BuildView(keysRoot, Font);
        keysGo.SetActive(false);

        // Resize handle (bottom-right).
        GameObject handle = new GameObject("Resize", typeof(RectTransform), typeof(Image), typeof(PixelMyWindowResizer));
        handle.transform.SetParent(windowObject.transform, false);
        Image gi = handle.GetComponent<Image>();
        gi.sprite = PixelUIKit.GripSprite();
        gi.color = new Color(1f, 1f, 1f, 0.55f);
        RectTransform rr = handle.GetComponent<RectTransform>();
        rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(1f, 0f);
        rr.sizeDelta = new Vector2(30f, 30f);
        rr.anchoredPosition = new Vector2(-6f, 6f);
        PixelMyWindowResizer resizer = handle.GetComponent<PixelMyWindowResizer>();
        resizer.owner = this;
        resizeHandleObject = handle;

        FillTargets();
        Layout();
    }

    private static void SetRect(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    internal void ResizeBy(Vector2 delta)
    {
        Vector2 size = new Vector2(windowRect.sizeDelta.x, fullHeight) + new Vector2(delta.x, -delta.y);
        size.x = Mathf.Clamp(size.x, MinSize.x, 1200f);
        float availableHeight = Mathf.Max(MinSize.y, canvasRect.rect.height - 2f * BarInset());
        size.y = Mathf.Clamp(size.y, MinSize.y, Mathf.Min(1000f, availableHeight));
        windowRect.sizeDelta = new Vector2(size.x, windowRect.sizeDelta.y);
        fullHeight = size.y;
        parts.SetHeight(size.y);   // (the chrome keeps the window inside the playfield and remembers the height for a minimize)
        Layout();
        geometryDirty = true;
    }

    internal float CanvasScale => canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

    /// <summary>How far the black bars (plus a little air) reach into the screen from the top / bottom.</summary>
    private float BarInset() => (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 84f) + EdgeMargin;

    private void ApplyOpen()
    {
        if (windowObject == null) return;
        if (open && chrome != null) chrome.PlaceNow();   // (its saved spot, or the first-time one, before it shows)
        if (open) PixelPop.Show(windowObject); else PixelPop.Hide(windowObject);
        if (open) RebuildRows();
        geometryDirty = true;
    }

    private Button MakeTab(Transform parent, string label, float x0, float x1, out Image image)
    {
        Button b = PixelUIKit.CreateButton(Font, parent, "Tab " + label, label, new Vector2(100f, 40f), ButtonRole.Neutral, 22f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(x0, 0f); r.anchorMax = new Vector2(x1, 1f);
        r.offsetMin = new Vector2(x0 > 0f ? 3f : 0f, 0f); r.offsetMax = new Vector2(x1 < 1f ? -3f : 0f, 0f);
        image = b.GetComponent<Image>();
        FitOneLine(b, 22f);
        return b;
    }

    /// <summary>Keeps a button's label on one line, shrinking it to fit a narrow window ('Event log' used to wrap onto two lines) - the shared PixelUIKit.FitOneLine plus a little side margin.</summary>
    private static void FitOneLine(Button b, float maxSize)
    {
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t == null) return;
        t.fontSize = maxSize;
        PixelUIKit.FitOneLine(t, 10f);
        t.margin = new Vector4(4f, 0f, 4f, 0f);
    }

    private static void SetTabAnchors(Button b, float x0, float x1)
    {
        if (b == null) return;
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(x0, 0f); r.anchorMax = new Vector2(x1, 1f);
        r.offsetMin = new Vector2(x0 > 0f ? 3f : 0f, 0f); r.offsetMax = new Vector2(x1 < 1f ? -3f : 0f, 0f);
    }

    private void ToggleHoverDock()
    {
        PixelPrefs.SetInt(PrefHoverDocked, HoverDocked ? 0 : 1);
        hoverShownH = -1f;
        Layout();
    }

    /// <summary>The switch at the top of the Event log tab: the in-game (bottom-left) event log on / off - the same setting as Settings > 'Event log'.</summary>
    private void ToggleScreenLog()
    {
        PixelHints.EventLogEnabled = !PixelHints.EventLogEnabled;
        UpdateScreenLogLabel();
        PixelAudio.Play("ui_click");
    }

    private void UpdateScreenLogLabel()
    {
        if (screenLogButton == null) return;
        bool on = PixelHints.EventLogEnabled;
        PixelUIKit.SetText(screenLogButton.GetComponentInChildren<TMP_Text>(), "In-game event log: " + (on ? "ON" : "OFF"));
        PixelUIKit.SetRole(screenLogButton, on ? ButtonRole.Success : ButtonRole.Neutral);
    }

    /// <summary>
    /// The right header slots: the X and the chrome's '-' (and its undock arrow while docked) come first, then Info (hidden while minimized) and the
    /// (always hidden) Edit. The title stays centred, clear of the buttons on both sides.
    /// </summary>
    private void LayoutHeaderButtons()
    {
        if (parts == null) return;
        float size = PixelUIKit.ToolCloseSize, top = PixelUIKit.ToolCloseOffset.y;
        float right = PixelUIKit.ToolCloseOffset.x + size + 6f + size + 8f;   // the right edge of the first free slot: past the X and the '-'
        if (chrome != null && chrome.Docked) right += size + 8f;               // (the chrome's undock arrow sits in that slot while docked)
        float used = right - 8f;                                               // how far in from the right edge the buttons reach
        if (hoverDockButton != null)
        {
            bool showInfo = !minimized;
            hoverDockButton.gameObject.SetActive(showInfo);
            if (showInfo)
            {
                hoverDockButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-right, -top);
                right += size + 8f;
                used = right - 8f;
            }
        }
        if (editButtonObject != null) editButtonObject.SetActive(false);   // (the Pinned tab is a board of widgets now: no Edit mode)
        if (titleLabel != null)
        {
            RectTransform tr = titleLabel.rectTransform;
            tr.offsetMin = new Vector2(used + 6f, tr.offsetMin.y);   // the title never runs under a button
            tr.offsetMax = new Vector2(-(used + 6f), tr.offsetMax.y);
        }
    }

    /// <summary>Places the list under the header (and the add bar while editing), the tabs while the event log is docked, and the right list for the tab.</summary>
    private void Layout()
    {
        if (scroll == null) return;
        const bool docked = true;   // (the Event log tab is always there now)
        if (showLogTab) showKeysTab = false;
        bool logTab = docked && showLogTab;
        bool keysTab = showKeysTab && !logTab;
        float tabs = TabBarHeight;
        bool hoverOn = HoverDocked && !minimized && !logTab && !keysTab;
        bool hoverMini = HoverDocked && minimized;   // minimized: the small card (block + name) hangs just under the header
        float hoverH = hoverOn ? Mathf.Max(hoverShownH, 64f) + 8f : 0f;   // the pinned hover card (or its hint) sits under the header / tabs
        hoverLaidH = hoverShownH;
        LayoutHoverSlot(hoverOn, hoverMini, hoverH, tabs);
        float top = HeaderHeight + 4f + tabs + hoverH + (editing && !logTab && !keysTab ? 170f : 0f);
        scroll.gameObject.SetActive(!minimized && !logTab && !keysTab);
        if (pickerRoot != null && !scroll.gameObject.activeSelf) pickerRoot.SetActive(false);
        if (logScroll != null) logScroll.gameObject.SetActive(!minimized && logTab);
        LayoutTabs(docked, logTab, keysTab, tabs);
        if (resizeHandleObject != null) resizeHandleObject.SetActive(!minimized);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(12f, 34f);
        vr.offsetMax = new Vector2(-12f, -top);
        LayoutLogArea(top, logTab);
        addBar.anchoredPosition = new Vector2(0f, -(HeaderHeight + 4f + tabs + hoverH));
        addBar.gameObject.SetActive(editing && !minimized && !logTab && !keysTab);
        if (editLabel != null) PixelUIKit.SetText(editLabel, editing ? "Done" : "Edit");
        LayoutHeaderButtons();
        UpdateContentHeight();
    }

    // Layout step: the pinned hover card's slot under the header / tabs, and the pin button's colour.
    private void LayoutHoverSlot(bool hoverOn, bool hoverMini, float hoverH, float tabs)
    {
        if (hoverSlot != null)
        {
            hoverSlot.gameObject.SetActive(hoverOn || hoverMini);
            hoverSlot.sizeDelta = new Vector2(-24f, hoverMini ? Mathf.Max(0f, hoverShownH) : Mathf.Max(0f, hoverH - 8f));
            hoverSlot.anchoredPosition = new Vector2(0f, hoverMini ? -(HeaderHeight + 4f) : -(HeaderHeight + 4f + tabs));
            if (hoverPlaceholder != null) hoverPlaceholder.gameObject.SetActive(hoverShownH <= 0f && !hoverMini);
        }
        if (hoverDockImage != null && shownHoverDocked != HoverDocked)   // (only re-role on a change)
        {
            shownHoverDocked = HoverDocked;
            PixelUIKit.SetRole(hoverDockImage, HoverDocked ? ButtonRole.Success : ButtonRole.Neutral);
        }
    }

    // Layout step: the tab bar, its three tabs (Pinned / Event log / Keys) and the Keys tab's page.
    private void LayoutTabs(bool docked, bool logTab, bool keysTab, float tabs)
    {
        if (tabBar != null) tabBar.SetActive(!minimized);
        if (tabLogButton != null) tabLogButton.gameObject.SetActive(docked);
        SetTabAnchors(tabPinnedButton, 0f, docked ? 1f / 3f : 0.5f);
        SetTabAnchors(tabLogButton, 1f / 3f, 2f / 3f);
        SetTabAnchors(tabKeysButton, docked ? 2f / 3f : 0.5f, 1f);
        if (keysRoot != null)
        {
            keysRoot.gameObject.SetActive(!minimized && keysTab);
            keysRoot.offsetMin = new Vector2(12f, 34f); keysRoot.offsetMax = new Vector2(-12f, -(HeaderHeight + 4f + tabs));
            PixelCustomKeys keysView = PixelCustomKeys.Instance;
            if (keysView != null) keysView.SetVisible(!minimized && keysTab);
        }
        int tabNow = keysTab ? 2 : logTab ? 1 : 0;
        if (tabNow != shownTab)   // (only re-role on a change)
        {
            shownTab = tabNow;
            PixelUIKit.SetRole(tabPinnedImage, tabNow == 0 ? ButtonRole.Selected : ButtonRole.Neutral);
            PixelUIKit.SetRole(tabLogImage, tabNow == 1 ? ButtonRole.Selected : ButtonRole.Neutral);
            PixelUIKit.SetRole(tabKeysImage, tabNow == 2 ? ButtonRole.Selected : ButtonRole.Neutral);
        }
    }

    // Layout step: the docked event log's list and its 'show on screen' button ('top' = where the lists start under the header).
    private void LayoutLogArea(float top, bool logTab)
    {
        if (logScroll != null)
        {
            RectTransform lr = logScroll.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(12f, 34f); lr.offsetMax = new Vector2(-12f, -(top + 52f));
        }
        if (screenLogButton != null)
        {
            screenLogButton.gameObject.SetActive(!minimized && logTab);
            RectTransform br = screenLogButton.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(12f, -(top + 44f)); br.offsetMax = new Vector2(-12f, -top);
            if (logTab) UpdateScreenLogLabel();
        }
    }

    /// <summary>The docked event log: every line of PixelHints' history, oldest at the top and the newest at the bottom (the list opens scrolled to it); a click shows the line's full tip.</summary>
    private void RebuildLog()
    {
        if (logContent == null || logScroll == null) return;
        logVersionShown = PixelHints.HistoryVersion;
        for (int i = logContent.childCount - 1; i >= 0; i--) Destroy(logContent.GetChild(i).gameObject);
        float width = Mathf.Max(200f, windowRect.sizeDelta.x - 24f - 28f);
        float y = 4f;
        int count = PixelHints.HistoryCount;
        for (int i = 0; i < count; i++)
        {
            if (!PixelHints.TryGetHistory(i, out string text, out string full)) continue;
            GameObject go = new GameObject("Line " + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(logContent, false);
            go.GetComponent<Image>().color = rowColor;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            TMP_Text label = Text(rt, "Text", text, 20f, TextAlignmentOptions.TopLeft, textColor);
            label.textWrappingMode = TextWrappingModes.Normal;
            float h = Mathf.Ceil(label.GetPreferredValues(text, width - 20f, 0f).y) + 14f;
            Stretch(label.rectTransform, 10f, 6f);
            rt.sizeDelta = new Vector2(-8f, h);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += h + 5f;
            bool opens = !string.IsNullOrEmpty(full);
            string tip = full;
            Button b = PixelUIKit.MakeClickable(go, opens ? () => PixelNotice.Show(tip, 0f) : (UnityEngine.Events.UnityAction)null, sound: opens);
            b.interactable = opens;
        }
        if (count == 0)
        {
            TMP_Text empty = Text(logContent, "Empty", "Nothing has happened yet.", 20f, TextAlignmentOptions.Center, dimTextColor);
            RectTransform er0 = empty.rectTransform;
            er0.anchorMin = new Vector2(0f, 1f); er0.anchorMax = new Vector2(1f, 1f); er0.pivot = new Vector2(0.5f, 1f);
            er0.sizeDelta = new Vector2(0f, 60f); er0.anchoredPosition = new Vector2(0f, -10f);
            y = 70f;
        }
        logContentHeight = y + 4f;
        Canvas.ForceUpdateCanvases();
        PixelUIKit.UpdateScrollView(logScroll, logScrollBar, logContentHeight, logScroll.GetComponent<RectTransform>().rect.height);
        logScroll.verticalNormalizedPosition = 0f;   // the newest line is at the bottom
    }

    private float contentHeight;

    private void UpdateContentHeight()
    {
        float viewHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, scrollBar, contentHeight, viewHeight);
    }

    // ------------------------------------------------------------------
    // Rows
    // ------------------------------------------------------------------

    private string Signature()
    {
        // Rows that expand (all skills / all goals) change with the game: rebuild when what they expand to changes.
        StringBuilder sb = new StringBuilder();
        foreach (Entry e in blob.entries)
            foreach (string sub in Expand(e)) sb.Append(e.type).Append(':').Append(e.key).Append('/').Append(sub).Append(';');
        sb.Append(editing ? "E" : "V");
        return sb.ToString();
    }

    private List<string> Expand(Entry e)
    {
        List<string> subs = new List<string>();
        if (e.type == "skill" && e.key == "all")
        {
            PixelSkills skills = PixelSkills.Instance;
            if (skills != null) foreach (PixelSkills.SkillPack p in skills.Packs) if (skills.IsAvailable(p)) subs.Add(p.id);
        }
        else if (e.type == "goal" && e.key == "all")
        {
            PixelLog log = PixelFind.First<PixelLog>();
            int n = log != null ? log.CurrentGoals().Count : 0;
            for (int i = 0; i < n; i++) subs.Add("g" + i);
        }
        else subs.Add("");
        return subs;
    }

    // ------------------------------------------------------------------
    // Adding
    // ------------------------------------------------------------------

    private void FillTargets()
    {
        if (typeDropdown == null || targetDropdown == null || clicker == null) return;
        string type = TypeKeys[Mathf.Clamp(typeDropdown.value, 0, TypeKeys.Length - 1)];
        List<string> names = new List<string>();
        targetKeys.Clear();
        switch (type)
        {
            case "currency":
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                {
                    if (!t.unlocked && t.count <= 0d) continue;
                    names.Add(t.displayName.EndsWith(" Pixels") ? t.displayName.Substring(0, t.displayName.Length - 7) : t.displayName);
                    targetKeys.Add(t.type.ToString());
                }
                break;
            case "skill":
            {
                names.Add("Skill in training (follows)"); targetKeys.Add("training");
                names.Add("All skills"); targetKeys.Add("all");
                PixelSkills skills = PixelSkills.Instance;
                if (skills != null) foreach (PixelSkills.SkillPack p in skills.Packs) if (skills.IsAvailable(p)) { names.Add(p.displayName); targetKeys.Add(p.id); }
                break;
            }
            case "goal":
            {
                names.Add("Next goal"); targetKeys.Add("next");
                names.Add("All current goals"); targetKeys.Add("all");
                foreach (PixelMinigame m in PixelMinigame.All)
                    if (m != null && !(m is PixelVisitorMinigame) && m.HasTracker) { names.Add(m.TrackerTitle); targetKeys.Add("mg:" + m.Id); }
                break;
            }
            case "achievement":
            {
                PixelAchievements ach = PixelFind.First<PixelAchievements>();
                if (ach != null) for (int i = 0; i < ach.Count; i++) { names.Add(ach.GetTitle(i)); targetKeys.Add(ach.GetId(i)); }
                break;
            }
            case "fav":
            {
                foreach (var tool in Tools) { names.Add("Tool: " + tool.name); targetKeys.Add("tool:" + tool.id); }
                PixelConsumables consumables = PixelFind.First<PixelConsumables>();
                if (consumables != null)
                    for (int i = 0; i < consumables.ItemCount; i++)
                        if (consumables.ItemListable(i)) { names.Add(consumables.ItemName(i)); targetKeys.Add("item:" + consumables.ItemName(i)); }
                break;
            }
            default:
                names.Add("(a blank note)"); targetKeys.Add("");
                break;
        }
        targetDropdown.ClearOptions();
        targetDropdown.AddOptions(names);
        targetDropdown.SetValueWithoutNotify(0);
        targetDropdown.RefreshShownValue();
        targetDropdown.interactable = names.Count > 0;
    }

    private void AddSelected()
    {
        string type = TypeKeys[Mathf.Clamp(typeDropdown.value, 0, TypeKeys.Length - 1)];
        if (targetKeys.Count == 0) { FillTargets(); if (targetKeys.Count == 0) return; }
        string key = targetKeys[Mathf.Clamp(targetDropdown.value, 0, targetKeys.Count - 1)];
        blob.entries.Add(new Entry { type = type, key = key });
        SaveEntries();
        RebuildRows();
        scroll.verticalNormalizedPosition = 0f;
    }

    // ------------------------------------------------------------------
    // Update
    // ------------------------------------------------------------------

    private void RefreshValues()
    {
        foreach (Row r in rows) r.refresh?.Invoke();
    }

    private void Update()
    {
        if (canvasRoot == null) return;
        hudTimer -= Time.unscaledDeltaTime;
        if (hudTimer <= 0f)
        {
            hudTimer = 0.5f;
            bool show = ShowButton();
            buttonAvailable = show;
            if (PixelTitleScreen.Showing != !canvasRoot.activeSelf) canvasRoot.SetActive(!PixelTitleScreen.Showing);
        }
        saveTimer -= Time.unscaledDeltaTime;
        if (geometryDirty && saveTimer <= 0f && !PixelInput.LeftHeld()) { SaveGeometry(); saveTimer = 0.5f; }

        if (open) UpdateRemoveHold();
        if (!open) return;
        sampleTimer -= Time.unscaledDeltaTime;
        if (sampleTimer <= 0f) { sampleTimer = 1f; SampleRates(); }
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshSeconds > 0f ? refreshSeconds : 0.4f;
        if (showLogTab && PixelHints.HistoryVersion != logVersionShown) RebuildLog();   // new events show up in the docked log
        if (Signature() != rowSignature) RebuildRows();
        else RefreshValues();
    }
}

/// <summary>The corner handle that resizes My Window.</summary>
public class PixelMyWindowResizer : MonoBehaviour, IDragHandler
{
    internal PixelMyWindow owner;

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.ResizeBy(eventData.delta / owner.CanvasScale);
    }
}
