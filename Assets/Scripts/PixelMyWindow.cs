// ---- System notes (moved here from CLAUDE.md) ----
// PixelMyWindow.cs
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
//   geometry). It can only be dragged INSIDE the playfield between the black bars and the screen sides (ClampToScreen). MINIMIZE button in the header: only the
//   header remains (still draggable); the full height is remembered. DOCKING: let go of a drag with the window against the left or right screen edge (edgeSnap px) and it docks
//   there at that height (dockSide -1 / +1, dockY): only a sliver (dockSliver) stays on screen and it slides out while the mouse is near / over it or while editing
//   (UpdateDock); an UNDOCK arrow button appears in the header while docked (it pulls the window back into the screen); a pale strip at the edge shows while a drag is close
//   enough to dock. Saved globally: PixelClicker.MyWindow.Min / Dock / DockY next to X / Y / W / H.
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
public class PixelMyWindow : MonoBehaviour
{
    [Header("My Window")]
    [Tooltip("Show the docked 'My Window' button from the start. Off = it appears once two pixel types are unlocked (like the Pixel Guide).")]
    [SerializeField] private bool showFromStart = false;

    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "My Window";

    [Tooltip("Starting size of the window (canvas units). 0 = the coded default (450 x 660, tall).")]
    [SerializeField] private Vector2 defaultSize = Vector2.zero;

    [Tooltip("Canvas sorting order (below the Shop / Skills windows, above the game). 0 = the coded default (107).")]
    [SerializeField] private int sortingOrder = 0;

    [Min(0f)]
    [Tooltip("Seconds between value refreshes. 0 = the coded default (0.4).")]
    [SerializeField] private float refreshSeconds = 0f;

    [Tooltip("Colours.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.12f, 0.2f, 0.94f);
    [SerializeField] private Color rowColor = new Color(0.16f, 0.2f, 0.3f, 0.9f);
    [SerializeField] private Color textColor = new Color(0.93f, 0.95f, 1f, 1f);
    [SerializeField] private Color dimTextColor = new Color(0.7f, 0.76f, 0.88f, 1f);
    [SerializeField] private Color barColor = new Color(0.3f, 0.8f, 0.45f, 1f);
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.45f, 0.8f, 1f);

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
    private GameObject canvasRoot, windowObject, openButtonObject;
    private Canvas canvas;
    private RectTransform canvasRect, windowRect, listContent, addBar;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_Text editLabel;
    private TMP_Dropdown typeDropdown, targetDropdown;
    private readonly List<string> targetKeys = new List<string>();
    private bool open, editing;
    private bool minimized;
    private float restoreHeight = 660f;
    private int dockSide;          // 0 = free, -1 = docked on the left edge, 1 = docked on the right edge
    private float dockY, dockSlide;
    private Button minimizeButton, undockButton;
    private GameObject editButtonObject, resizeHandleObject;
    private PixelPopoutDrag headerDrag;
    private RectTransform dockHint;
    private const float HeaderHeight = 60f, EdgeSnap = 70f, DockSliver = 36f, EdgeMargin = 14f;
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
        open = PlayerPrefs.GetInt(PrefPrefix + "Open", 0) != 0;
        if (minimized) windowRect.sizeDelta = new Vector2(windowRect.sizeDelta.x, HeaderHeight);
        ApplyDockState();
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
        string json = PlayerPrefs.GetString(PrefEntries, "");
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
        PlayerPrefs.SetString(PrefEntries, JsonUtility.ToJson(blob));
    }

    private void SaveGeometry()
    {
        geometryDirty = false;
        PlayerPrefs.SetFloat(PrefPrefix + "X", windowRect.anchoredPosition.x);
        PlayerPrefs.SetFloat(PrefPrefix + "Y", windowRect.anchoredPosition.y);
        PlayerPrefs.SetFloat(PrefPrefix + "W", windowRect.sizeDelta.x);
        PlayerPrefs.SetFloat(PrefPrefix + "H", minimized ? restoreHeight : windowRect.sizeDelta.y);
        PlayerPrefs.SetInt(PrefPrefix + "Open", open ? 1 : 0);
        PlayerPrefs.SetInt(PrefPrefix + "Layout", 2);
        PlayerPrefs.SetInt(PrefPrefix + "Min", minimized ? 1 : 0);
        PlayerPrefs.SetInt(PrefPrefix + "Dock", dockSide);
        PlayerPrefs.SetFloat(PrefPrefix + "DockY", dockY);
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

        windowObject = new GameObject("My Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(windowObject.GetComponent<Image>(), panelColor);
        windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0f, 1f);
        Vector2 size = defaultSize.x > 100f && defaultSize.y > 100f ? defaultSize : new Vector2(450f, 660f);
        bool saved = PlayerPrefs.GetInt(PrefPrefix + "Layout", 0) == 2;   // (older saved places / sizes are ignored once: the window now opens tall at the right)
        windowRect.sizeDelta = saved ? new Vector2(Mathf.Max(MinSize.x, PlayerPrefs.GetFloat(PrefPrefix + "W", size.x)), Mathf.Max(MinSize.y, PlayerPrefs.GetFloat(PrefPrefix + "H", size.y))) : size;
        restoreHeight = windowRect.sizeDelta.y;
        Vector2 cs = canvasRect.rect.size;
        if (cs.x < 100f || cs.y < 100f) cs = new Vector2(1920f, 1080f);
        float inset = BarInset();
        windowRect.anchoredPosition = saved
            ? new Vector2(PlayerPrefs.GetFloat(PrefPrefix + "X", 60f), PlayerPrefs.GetFloat(PrefPrefix + "Y", -170f))
            : new Vector2(cs.x - windowRect.sizeDelta.x - 22f, -(cs.y - inset - windowRect.sizeDelta.y));   // right side, just above the bottom bar
        if (saved)
        {
            minimized = PlayerPrefs.GetInt(PrefPrefix + "Min", 0) != 0;
            dockSide = PlayerPrefs.GetInt(PrefPrefix + "Dock", 0);
            dockY = PlayerPrefs.GetFloat(PrefPrefix + "DockY", windowRect.anchoredPosition.y);
        }

        // Header: title (drag handle), Edit, close.
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(windowObject.transform, false);
        header.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        RectTransform hr = header.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(0f, 60f); hr.anchoredPosition = Vector2.zero;
        PixelPopoutDrag drag = header.AddComponent<PixelPopoutDrag>();
        drag.target = windowRect;
        drag.canvas = canvas;
        headerDrag = drag;
        drag.onMoved = () => { ClampToScreen(); UpdateDockHint(); geometryDirty = true; };
        drag.onEnd = () => { TryDock(); UpdateDockHint(); geometryDirty = true; };

        TMP_Text title = PixelUIKit.CreateText(Font, header.transform, "Title", windowTitle, 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        PixelUIKit.Caps(title);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMax = 30f;
        title.fontSizeMin = 14f;
        title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.offsetMin = new Vector2(18f, 0f); title.rectTransform.offsetMax = new Vector2(-270f, 0f);

        Button close = PixelUIKit.CreateButton(Font, header.transform, "Close", "X", new Vector2(44f, 44f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 26f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 0.5f);
        cr.anchoredPosition = new Vector2(-10f, 0f);
        close.onClick.AddListener(() => { open = false; ApplyOpen(); });

        Button edit = PixelUIKit.CreateButton(Font, header.transform, "Edit", "Edit", new Vector2(96f, 44f), buttonColor, Color.white, 22f);
        RectTransform er = edit.GetComponent<RectTransform>();
        er.anchorMin = er.anchorMax = er.pivot = new Vector2(1f, 0.5f);
        er.anchoredPosition = new Vector2(-62f, 0f);
        editLabel = edit.GetComponentInChildren<TMP_Text>();
        edit.onClick.AddListener(() => { editing = !editing; RebuildRows(); });
        editButtonObject = edit.gameObject;

        // Minimize: a small bar icon. Undock: an arrow (shown only while docked) that pulls the window back into the screen.
        minimizeButton = PixelUIKit.CreateButton(Font, header.transform, "Minimize", "", new Vector2(44f, 44f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 22f);
        RectTransform mr = minimizeButton.GetComponent<RectTransform>();
        mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(1f, 0.5f);
        mr.anchoredPosition = new Vector2(-166f, 0f);
        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(minimizeButton.transform, false);
        bar.GetComponent<Image>().color = Color.white;
        bar.GetComponent<Image>().raycastTarget = false;
        RectTransform br2 = bar.GetComponent<RectTransform>();
        br2.anchorMin = br2.anchorMax = br2.pivot = new Vector2(0.5f, 0.5f);
        br2.sizeDelta = new Vector2(20f, 4f);
        br2.anchoredPosition = new Vector2(0f, -6f);
        minimizeButton.onClick.AddListener(() => SetMinimized(!minimized));

        undockButton = PixelUIKit.CreateButton(Font, header.transform, "Undock", ">", new Vector2(44f, 44f), buttonColor, Color.white, 22f);
        RectTransform ur = undockButton.GetComponent<RectTransform>();
        ur.anchorMin = ur.anchorMax = ur.pivot = new Vector2(1f, 0.5f);
        ur.anchoredPosition = new Vector2(-218f, 0f);
        PixelUIKit.UseArrowGlyph(undockButton, false);
        undockButton.onClick.AddListener(Undock);
        undockButton.gameObject.SetActive(false);

        // Add bar (edit mode): what to add, which one, the Add button.
        GameObject bar = new GameObject("Add Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(windowObject.transform, false);
        bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        PixelUIKit.StyleBox(bar.GetComponent<Image>(), true);
        addBar = bar.GetComponent<RectTransform>();
        addBar.anchorMin = new Vector2(0f, 1f); addBar.anchorMax = new Vector2(1f, 1f); addBar.pivot = new Vector2(0.5f, 1f);
        addBar.sizeDelta = new Vector2(-24f, 164f);
        addBar.anchoredPosition = new Vector2(0f, -64f);

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
        Button add = PixelUIKit.CreateButton(Font, bar.transform, "Add", "+ Add to my window", new Vector2(300f, 44f), new Color(0.25f, 0.6f, 0.4f, 1f), Color.white, 22f);
        SetRect(add.GetComponent<RectTransform>(), 8f, 110f, 0f, 44f);
        add.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        add.GetComponent<RectTransform>().offsetMax = new Vector2(-8f, -110f);
        add.onClick.AddListener(AddSelected);

        // The list.
        scroll = PixelUIKit.CreateScrollView(windowObject.transform, "List", new Color(1f, 1f, 1f, 0.35f), 12f, 60f, out listContent, out scrollBar);
        PixelScrollBarFade fade = scrollBar != null ? scrollBar.GetComponent<PixelScrollBarFade>() : null;
        if (fade != null) fade.hideBar = true;

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

        // A pale strip at a screen edge while a drag is close enough to dock there.
        GameObject hint = new GameObject("Dock Hint", typeof(RectTransform), typeof(Image));
        hint.transform.SetParent(canvasRoot.transform, false);
        hint.GetComponent<Image>().color = new Color(0.45f, 0.85f, 1f, 0.35f);
        hint.GetComponent<Image>().raycastTarget = false;
        dockHint = hint.GetComponent<RectTransform>();
        hint.SetActive(false);

        FillTargets();
        ClampToScreen();
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
        Vector2 size = windowRect.sizeDelta + new Vector2(delta.x, -delta.y);
        size.x = Mathf.Clamp(size.x, MinSize.x, 1200f);
        float availableHeight = Mathf.Max(MinSize.y, canvasRect.rect.height - 2f * BarInset());
        size.y = Mathf.Clamp(size.y, MinSize.y, Mathf.Min(1000f, availableHeight));
        windowRect.sizeDelta = size;
        restoreHeight = size.y;
        ClampToScreen();
        Layout();
        geometryDirty = true;
    }

    internal float CanvasScale => canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

    /// <summary>How far the black bars (plus a little air) reach into the screen from the top / bottom.</summary>
    private float BarInset() => (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 84f) + EdgeMargin;

    /// <summary>Keeps the window INSIDE the playfield: between the black bars and the screen sides (a docked window only slides sideways, so only its height is kept in).</summary>
    private void ClampToScreen()
    {
        Vector2 cs = canvasRect.rect.size;
        if (cs.x < 100f || cs.y < 100f) return;   // (the canvas has no size yet)
        Vector2 p = windowRect.anchoredPosition, sz = windowRect.sizeDelta;
        float inset = BarInset();
        if (dockSide == 0) p.x = Mathf.Clamp(p.x, EdgeMargin, Mathf.Max(EdgeMargin, cs.x - sz.x - EdgeMargin));
        float maxY = -inset, minY = -(cs.y - inset - sz.y);
        p.y = Mathf.Clamp(p.y, Mathf.Min(minY, maxY), maxY);
        windowRect.anchoredPosition = p;
        if (dockSide != 0) dockY = p.y;
    }

    // ---- Minimize ----

    private void SetMinimized(bool on)
    {
        if (on == minimized) return;
        if (on) restoreHeight = windowRect.sizeDelta.y;
        minimized = on;
        windowRect.sizeDelta = new Vector2(windowRect.sizeDelta.x, minimized ? HeaderHeight : restoreHeight);
        ClampToScreen();
        Layout();
        geometryDirty = true;
    }

    // ---- Docking to a screen edge ----

    /// <summary>After a drag: against the left or right screen edge = dock there at this height.</summary>
    private void TryDock()
    {
        if (dockSide != 0 || canvasRect == null) return;
        Vector2 cs = canvasRect.rect.size;
        float left = windowRect.anchoredPosition.x, right = left + windowRect.sizeDelta.x;
        int side = left < EdgeSnap ? -1 : cs.x - right < EdgeSnap ? 1 : 0;
        if (side == 0) return;
        dockSide = side;
        dockY = windowRect.anchoredPosition.y;
        dockSlide = 1f;   // (it is out right now; it tucks away when the mouse leaves)
        ApplyDockState();
        PixelAudio.Play("ui_click");
    }

    private void Undock()
    {
        if (dockSide == 0) return;
        Vector2 cs = canvasRect.rect.size;
        int side = dockSide;
        dockSide = 0;
        windowRect.anchoredPosition = new Vector2(side < 0 ? EdgeMargin : cs.x - windowRect.sizeDelta.x - EdgeMargin, dockY);
        ClampToScreen();
        ApplyDockState();
        geometryDirty = true;
        PixelAudio.Play("ui_click");
    }

    /// <summary>Shows the undock button and locks / unlocks dragging.</summary>
    private void ApplyDockState()
    {
        if (undockButton != null)
        {
            undockButton.gameObject.SetActive(dockSide != 0);
            undockButton.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, dockSide > 0 ? 180f : 0f);   // points into the screen
        }
        if (headerDrag != null) headerDrag.locked = dockSide != 0;   // a docked window is moved by undocking it first
        geometryDirty = true;
    }

    /// <summary>The pale strip at the edge a drag is about to dock to.</summary>
    private void UpdateDockHint()
    {
        if (dockHint == null || canvasRect == null) return;
        bool dragging = PixelInput.LeftHeld() && dockSide == 0;
        Vector2 cs = canvasRect.rect.size;
        float left = windowRect.anchoredPosition.x, right = left + windowRect.sizeDelta.x;
        int side = !dragging ? 0 : left < EdgeSnap ? -1 : cs.x - right < EdgeSnap ? 1 : 0;
        dockHint.gameObject.SetActive(side != 0);
        if (side == 0) return;
        float inset = BarInset() - EdgeMargin;
        dockHint.anchorMin = new Vector2(side < 0 ? 0f : 1f, 0f);
        dockHint.anchorMax = new Vector2(side < 0 ? 0f : 1f, 1f);
        dockHint.pivot = new Vector2(side < 0 ? 0f : 1f, 0.5f);
        dockHint.sizeDelta = new Vector2(16f, -2f * inset);
        dockHint.anchoredPosition = Vector2.zero;
    }

    /// <summary>A docked window sits at its edge with only a sliver showing and slides out while the mouse is near it (or while it is being edited).</summary>
    private void UpdateDock()
    {
        Vector2 cs = canvasRect.rect.size;
        float w = windowRect.sizeDelta.x, h = windowRect.sizeDelta.y;
        bool near = editing;
        if (!near && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
        {
            float px = local.x + cs.x * 0.5f, py = cs.y * 0.5f - local.y;   // from the top left, y down
            float edge = dockSide < 0 ? px : cs.x - px;
            float top = -dockY;
            near = edge <= DockSliver + dockSlide * w && py >= top - 20f && py <= top + h + 20f;
        }
        dockSlide = Mathf.MoveTowards(dockSlide, near ? 1f : 0f, Time.unscaledDeltaTime / 0.22f);
        float e = Mathf.SmoothStep(0f, 1f, dockSlide);
        float shownX = dockSide < 0 ? EdgeMargin * 0.5f : cs.x - w - EdgeMargin * 0.5f;
        float hiddenX = dockSide < 0 ? -(w - DockSliver) : cs.x - DockSliver;
        windowRect.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, shownX, e), dockY);
    }

    private void ApplyOpen()
    {
        if (windowObject == null) return;
        if (open && dockSide != 0) dockSlide = 1f;
        if (open) PixelPop.Show(windowObject); else PixelPop.Hide(windowObject);
        if (open) RebuildRows();
        geometryDirty = true;
    }

    /// <summary>Places the list under the header (and the add bar while editing).</summary>
    private void Layout()
    {
        if (scroll == null) return;
        float top = 64f + (editing ? 170f : 0f);
        scroll.gameObject.SetActive(!minimized);
        if (resizeHandleObject != null) resizeHandleObject.SetActive(!minimized);
        if (editButtonObject != null) editButtonObject.SetActive(!minimized);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(12f, 34f);
        vr.offsetMax = new Vector2(-12f, -top);
        addBar.gameObject.SetActive(editing && !minimized);
        if (editLabel != null) PixelUIKit.SetText(editLabel, editing ? "Done" : "Edit");
        UpdateContentHeight();
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

    private void RebuildRows()
    {
        if (listContent == null) return;
        foreach (Row r in rows) if (r.rt != null) Destroy(r.rt.gameObject);
        rows.Clear();
        float y = 4f;
        for (int i = 0; i < blob.entries.Count; i++)
        {
            Entry e = blob.entries[i];
            List<string> subs = Expand(e);
            for (int s = 0; s < subs.Count; s++)
            {
                Row row = BuildRow(e, subs[s], s == 0, i);
                if (row == null) continue;
                row.rt.anchoredPosition = new Vector2(0f, -y);
                y += row.height + 6f;
                rows.Add(row);
            }
        }
        if (rows.Count == 0)
        {
            Row hint = new Row { entry = null, height = 90f };
            hint.rt = NewRow(90f);
            TMP_Text t = Text(hint.rt, "Hint", "Nothing here yet.\nPress Edit to pin currencies, skills, goals, achievements, notes and favorites.", 20f, TextAlignmentOptions.Center, dimTextColor);
            Stretch(t.rectTransform, 12f, 6f);
            hint.rt.anchoredPosition = new Vector2(0f, -4f);
            rows.Add(hint);
            y = 100f;
        }
        contentHeight = y + 4f;
        rowSignature = Signature();
        Layout();
        RefreshValues();
    }

    private RectTransform NewRow(float height)
    {
        GameObject go = new GameObject("Row", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(listContent, false);
        Image bg = go.GetComponent<Image>();
        bg.color = rowColor;
        PixelUIKit.StyleBox(bg);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-6f, height);
        return rt;
    }

    private TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
    {
        TMP_Text t = PixelUIKit.CreateText(Font, parent, name, text, size, align, style, color);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private static void Stretch(RectTransform rt, float x, float y)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(x, y); rt.offsetMax = new Vector2(-x, -y);
    }

    private Row BuildRow(Entry e, string sub, bool first, int entryIndex)
    {
        Row row = null;
        switch (e.type)
        {
            case "currency": row = BuildCurrency(e); break;
            case "skill": row = BuildSkill(e, sub); break;
            case "goal": row = BuildGoal(e, sub); break;
            case "achievement": row = BuildAchievement(e); break;
            case "note": row = BuildNote(e); break;
            case "fav": row = BuildFavorite(e); break;
        }
        if (row == null) return null;
        row.entry = e;
        row.sub = sub;
        row.first = first;
        if (editing && first) AddEditButtons(row, entryIndex);
        return row;
    }

    private void AddEditButtons(Row row, int index)
    {
        // Right end of the row: up, down, remove (and the rate toggle for a currency).
        float x = -6f;
        Button remove = MiniButton(row.rt, "x", ref x, new Color(0.65f, 0.25f, 0.25f, 1f));
        remove.onClick.AddListener(() => { blob.entries.Remove(row.entry); SaveEntries(); RebuildRows(); });
        Button down = MiniButton(row.rt, "v", ref x, new Color(0.3f, 0.35f, 0.5f, 1f));
        down.onClick.AddListener(() => MoveEntry(row.entry, 1));
        Button up = MiniButton(row.rt, "^", ref x, new Color(0.3f, 0.35f, 0.5f, 1f));
        up.onClick.AddListener(() => MoveEntry(row.entry, -1));
        if (row.entry.type == "currency")
        {
            Button rate = MiniButton(row.rt, "/min", ref x, new Color(0.25f, 0.5f, 0.45f, 1f), 62f);
            rate.onClick.AddListener(() => { row.entry.flag = !row.entry.flag; SaveEntries(); RefreshValues(); });
        }
        // Make room: the row's own texts stop before the buttons.
        foreach (TMP_Text t in row.rt.GetComponentsInChildren<TMP_Text>())
            if (t.name == "Amount" || t.name == "Detail") t.rectTransform.offsetMax = new Vector2(x - 4f, t.rectTransform.offsetMax.y);
    }

    private Button MiniButton(RectTransform parent, string label, ref float x, Color colour, float width = 34f)
    {
        Button b = PixelUIKit.CreateButton(Font, parent, "Edit " + label, label, new Vector2(width, 34f), colour, Color.white, 18f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0.5f);
        r.anchoredPosition = new Vector2(x, 0f);
        x -= width + 4f;
        return b;
    }

    private void MoveEntry(Entry e, int delta)
    {
        int i = blob.entries.IndexOf(e);
        int j = Mathf.Clamp(i + delta, 0, blob.entries.Count - 1);
        if (i < 0 || i == j) return;
        blob.entries.RemoveAt(i);
        blob.entries.Insert(j, e);
        SaveEntries();
        RebuildRows();
    }

    private (Image fill, RectTransform bar) MakeBar(RectTransform parent, float bottom, float height)
    {
        GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(parent, false);
        track.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 1f);
        PixelUIKit.StyleBox(track.GetComponent<Image>(), true);
        RectTransform tr = track.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f);
        tr.offsetMin = new Vector2(10f, bottom); tr.offsetMax = new Vector2(-10f, bottom + height);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        Image fi = fill.GetComponent<Image>();
        fi.color = barColor;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f);
        fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
        return (fi, fr);
    }

    private static void SetFill(RectTransform fill, float fraction) => fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

    // ---- Currency ----
    private Row BuildCurrency(Entry e)
    {
        int tierIndex = -1;
        if (Enum.TryParse(e.key, out PixelClicker.PixelType type)) tierIndex = clicker.IndexOf(type);
        if (tierIndex < 0) return null;
        Row row = new Row { height = 46f, rt = NewRow(46f) };
        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];

        GameObject swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        swatch.transform.SetParent(row.rt, false);
        swatch.GetComponent<Image>().color = tier.UIColor;
        RectTransform sr = swatch.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(26f, 26f);
        sr.anchoredPosition = new Vector2(12f, 0f);

        string name = tier.displayName.EndsWith(" Pixels") ? tier.displayName.Substring(0, tier.displayName.Length - 7) : tier.displayName;
        TMP_Text label = Text(row.rt, "Name", name, 22f, TextAlignmentOptions.MidlineLeft, textColor);
        Stretch(label.rectTransform, 48f, 4f);
        label.rectTransform.offsetMax = new Vector2(-150f, -4f);
        TMP_Text amount = Text(row.rt, "Amount", "", 24f, TextAlignmentOptions.MidlineRight, textColor, FontStyles.Bold);
        Stretch(amount.rectTransform, 12f, 2f);
        amount.rectTransform.offsetMin = new Vector2(150f, 2f);
        row.refresh = () =>
        {
            string text = PixelClicker.FormatNumber(Math.Floor(tier.count));
            if (e.flag) text += "  <size=70%><color=#9fd8ff>" + RateText(tierIndex) + "</color></size>";
            PixelUIKit.SetText(amount, text);
        };
        return row;
    }

    private readonly Dictionary<int, List<(float t, double c)>> rateSamples = new Dictionary<int, List<(float, double)>>();
    private float sampleTimer;

    private void SampleRates()
    {
        foreach (Entry e in blob.entries)
        {
            if (e.type != "currency" || !e.flag || !Enum.TryParse(e.key, out PixelClicker.PixelType type)) continue;
            int i = clicker.IndexOf(type);
            if (i < 0) continue;
            if (!rateSamples.TryGetValue(i, out var list)) rateSamples[i] = list = new List<(float, double)>();
            list.Add((Time.unscaledTime, clicker.Tiers[i].count));
            while (list.Count > 0 && Time.unscaledTime - list[0].t > 60f) list.RemoveAt(0);
        }
    }

    private string RateText(int tierIndex)
    {
        if (!rateSamples.TryGetValue(tierIndex, out var list) || list.Count < 3) return "...";
        float dt = list[list.Count - 1].t - list[0].t;
        if (dt < 5f) return "...";
        double perMinute = (list[list.Count - 1].c - list[0].c) / dt * 60d;
        return (perMinute >= 0 ? "+" : "-") + PixelClicker.FormatNumber(Math.Abs(perMinute)) + "/min";
    }

    // ---- Skill ----
    private Row BuildSkill(Entry e, string sub)
    {
        PixelSkills skills = PixelSkills.Instance;
        if (skills == null) return null;
        Row row = new Row { height = 66f, rt = NewRow(66f) };
        TMP_Text name = Text(row.rt, "Name", "", 22f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f); name.rectTransform.pivot = new Vector2(0.5f, 1f);
        name.rectTransform.offsetMin = new Vector2(12f, -34f); name.rectTransform.offsetMax = new Vector2(-150f, -5f);
        TMP_Text detail = Text(row.rt, "Detail", "", 20f, TextAlignmentOptions.TopRight, dimTextColor);
        detail.rectTransform.anchorMin = new Vector2(0f, 1f); detail.rectTransform.anchorMax = new Vector2(1f, 1f); detail.rectTransform.pivot = new Vector2(0.5f, 1f);
        detail.rectTransform.offsetMin = new Vector2(150f, -34f); detail.rectTransform.offsetMax = new Vector2(-12f, -5f);
        var bar = MakeBar(row.rt, 8f, 16f);
        string id = !string.IsNullOrEmpty(sub) ? sub : e.key;
        row.refresh = () =>
        {
            PixelSkills.SkillPack pack = id == "training" ? skills.Find(skills.TrainingId) : skills.Find(id);
            if (pack == null) { PixelUIKit.SetText(name, "?"); return; }
            int level = skills.LevelOf(pack, out double into, out double need);
            bool training = pack.id == skills.TrainingId;
            PixelUIKit.SetText(name, pack.displayName + (training && id != "training" ? "  (training)" : id == "training" ? "  (training)" : ""));
            PixelUIKit.SetText(detail, "Lv " + level + "   " + PixelClicker.FormatNumber(Math.Floor(into)) + " / " + PixelClicker.FormatNumber(Math.Ceiling(need)));
            bar.fill.color = pack.accent;
            SetFill(bar.bar, (float)(into / Math.Max(1d, need)));
        };
        return row;
    }

    // ---- Goals ----
    private Row BuildGoal(Entry e, string sub)
    {
        Row row = new Row { height = 66f, rt = NewRow(66f) };
        TMP_Text name = Text(row.rt, "Name", "", 21f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f); name.rectTransform.pivot = new Vector2(0.5f, 1f);
        name.rectTransform.offsetMin = new Vector2(12f, -34f); name.rectTransform.offsetMax = new Vector2(-150f, -5f);
        TMP_Text detail = Text(row.rt, "Detail", "", 20f, TextAlignmentOptions.TopRight, dimTextColor);
        detail.rectTransform.anchorMin = new Vector2(0f, 1f); detail.rectTransform.anchorMax = new Vector2(1f, 1f); detail.rectTransform.pivot = new Vector2(0.5f, 1f);
        detail.rectTransform.offsetMin = new Vector2(150f, -34f); detail.rectTransform.offsetMax = new Vector2(-12f, -5f);
        var bar = MakeBar(row.rt, 8f, 16f);
        string key = !string.IsNullOrEmpty(sub) ? sub : e.key;
        row.refresh = () =>
        {
            string title = "?";
            double count = 0d, goal = 1d;
            if (key.StartsWith("mg:"))
            {
                PixelMinigame m = PixelMinigame.Find(key.Substring(3));
                if (m != null && m.HasTracker) { title = m.TrackerTitle; count = m.TrackerCount; goal = Math.Max(1d, m.TrackerGoal); }
            }
            else
            {
                PixelLog log = PixelFind.First<PixelLog>();
                List<(string title, double count, double goal)> goals = log != null ? log.CurrentGoals() : null;
                int index = key == "next" ? 0 : key.StartsWith("g") && int.TryParse(key.Substring(1), out int gi) ? gi : -1;
                if (goals != null && index >= 0 && index < goals.Count) { title = goals[index].title; count = goals[index].count; goal = Math.Max(1d, goals[index].goal); }
                else if (goals != null && key == "next") title = "No goal right now";
            }
            bool done = count >= goal;
            PixelUIKit.SetText(name, title);
            PixelUIKit.SetText(detail, done ? "Done" : PixelClicker.FormatNumber(Math.Floor(count)) + " / " + PixelClicker.FormatNumber(goal));
            bar.fill.color = done ? new Color(1f, 0.85f, 0.3f, 1f) : barColor;
            SetFill(bar.bar, (float)(count / goal));
        };
        return row;
    }

    // ---- Achievement ----
    private Row BuildAchievement(Entry e)
    {
        PixelAchievements ach = PixelFind.First<PixelAchievements>();
        if (ach == null) return null;
        int index = -1;
        for (int i = 0; i < ach.Count; i++) if (ach.GetId(i) == e.key) { index = i; break; }
        if (index < 0) return null;
        Row row = new Row { height = 66f, rt = NewRow(66f) };
        TMP_Text name = Text(row.rt, "Name", ach.GetTitle(index), 21f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f); name.rectTransform.pivot = new Vector2(0.5f, 1f);
        name.rectTransform.offsetMin = new Vector2(12f, -34f); name.rectTransform.offsetMax = new Vector2(-150f, -5f);
        TMP_Text detail = Text(row.rt, "Detail", "", 20f, TextAlignmentOptions.TopRight, dimTextColor);
        detail.rectTransform.anchorMin = new Vector2(0f, 1f); detail.rectTransform.anchorMax = new Vector2(1f, 1f); detail.rectTransform.pivot = new Vector2(0.5f, 1f);
        detail.rectTransform.offsetMin = new Vector2(150f, -34f); detail.rectTransform.offsetMax = new Vector2(-12f, -5f);
        var bar = MakeBar(row.rt, 8f, 16f);
        row.refresh = () =>
        {
            bool complete = ach.IsComplete(index);
            double target = ach.GetCurrentTarget(index);
            PixelUIKit.SetText(detail, complete ? "Complete" : PixelClicker.FormatNumber(Math.Floor(ach.GetProgress(index))) + " / " + PixelClicker.FormatNumber(target));
            bar.fill.color = complete ? new Color(1f, 0.85f, 0.3f, 1f) : barColor;
            SetFill(bar.bar, complete ? 1f : ach.GetFraction(index));
        };
        return row;
    }

    // ---- Note ----
    private Row BuildNote(Entry e)
    {
        Row row = new Row { height = 60f, rt = NewRow(60f) };
        TMP_InputField field = PixelUIKit.CreateInputField(Font, row.rt, "Note", new Vector2(200f, 44f), new Color(0.08f, 0.1f, 0.16f, 1f), textColor, 21f, "Write a note...");
        RectTransform fr = field.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(8f, 8f);
        fr.offsetMax = new Vector2(editing ? -128f : -8f, -8f);
        field.SetTextWithoutNotify(e.text);
        field.onEndEdit.AddListener(s => { e.text = s; SaveEntries(); });
        return row;
    }

    // ---- Favorites ----
    private Row BuildFavorite(Entry e)
    {
        bool tool = e.key.StartsWith("tool:");
        Row row = new Row { height = 54f, rt = NewRow(54f) };
        Button button = PixelUIKit.CreateButton(Font, row.rt, "Use", "", new Vector2(200f, 40f), buttonColor, Color.white, 22f);
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(8f, 7f);
        br.offsetMax = new Vector2(editing ? -128f : -8f, -7f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMax = 22f;
        label.fontSizeMin = 11f;
        Image image = button.GetComponent<Image>();

        if (tool)
        {
            string id = e.key.Substring(5);
            var def = Array.Find(Tools, t => t.id == id);
            if (def.id == null) return null;
            button.onClick.AddListener(() =>
            {
                if (id == "hose") { PixelBank bank = PixelFind.First<PixelBank>(); if (bank != null) bank.ToggleHose(); }
                else PixelMenuKeys.Click(def.action);
            });
            row.refresh = () =>
            {
                bool hose = id == "hose";
                bool available = hose ? PixelFind.First<PixelBank>() != null : PixelMenuKeys.CanClick(def.action);
                bool on = hose && PixelBank.HoseOn;
                PixelUIKit.SetText(label, def.name + (on ? "  (on)" : ""));
                image.color = on ? new Color(0.25f, 0.65f, 0.4f, 1f) : available ? buttonColor : new Color(0.3f, 0.3f, 0.35f, 1f);
            };
            return row;
        }

        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null) return null;
        string itemName = e.key.StartsWith("item:") ? e.key.Substring(5) : e.key;
        int item = -1;
        for (int i = 0; i < consumables.ItemCount; i++) if (consumables.ItemName(i) == itemName) { item = i; break; }
        if (item < 0) return null;
        button.onClick.AddListener(() => { consumables.TryUseItem(item); RefreshValues(); });
        row.refresh = () =>
        {
            int owned = consumables.ItemOwned(item);
            PixelUIKit.SetText(label, itemName + "   x" + PixelConsumables.OwnedText(owned));
            image.color = owned > 0 ? buttonColor : new Color(0.3f, 0.3f, 0.35f, 1f);
        };
        return row;
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

        if (open && dockSide != 0) UpdateDock();
        if (open) UpdateDockHint();
        if (!open) return;
        sampleTimer -= Time.unscaledDeltaTime;
        if (sampleTimer <= 0f) { sampleTimer = 1f; SampleRates(); }
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = refreshSeconds > 0f ? refreshSeconds : 0.4f;
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
