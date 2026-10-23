// ---- System notes ----
// PixelDevHub.cs
//   The DEV HUB: ONE window for every dev tool (opens with PixelAction.DevTools, F9, the pause menu's Dev Tools button; PixelDevTools.OpenPanel / Close
//   delegate here). It replaces the old Dev Tools panel, the Inspector window and the Mock up window.
//   * The window itself is a standard Tool window (PixelUIKit.CreateWindow + PixelWindowChrome): centred title, grey X, drag by the header (kept between the black bars), minimize,
//     dock to the left / right screen edge, the corner grip scales it; the search box sits at the left of the header (no PixelPop: the hub opens with its canvas).
//   * FILING-CABINET TABS along the top: Main (PixelDevTools' widgets), To-Do, Changelog, Inspector (PixelDevInspector), Shapes (PixelDevShapes), UI Mock (PixelDevMockup), Looks (PixelLookStudio),
//     Setups (PixelDevSetups), Events (PixelDevEvents), Sounds (PixelDevSounds), Console (PixelDevConsole), Models (PixelModelEditor); new tabs build with PixelDevUI. Drag a tab
//     sideways to REARRANGE the tabs; drag it down out of the strip to POP IT OUT into its own floating window (drag that window by its header, '-' minimizes it); the 'Dock' button or
//     dropping the floating window on the hub puts it back as a tab. Order, active tab, floating tabs + their places and the hub's open state are saved (PlayerPrefs PixelClicker.Dev.Hub.*).
//   * Tools get their content root with PixelDevHub.TabRoot(id) (a top-left anchored RectTransform of the tab's design size, 700 x 780 or 500 x 780 for the mockups) and learn when they
//     are on screen through PixelDevHub.SetTabCallback(id, visible => ...): visible = the hub is open and the tab is the active docked tab (or a floating, un-minimized window).
//   * the Main tab's draggable WIDGETS are a PixelCardBoard (PixelCardBoard.cs, shared with Pocket): each row sits on a card with a grip on its left; drag it to move the row, the others make room; the order is saved.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PixelDevHub : MonoBehaviour
{
    public const float ContentW = 700f, ContentH = 780f, HeaderH = PixelUIKit.ToolHeaderHeight, TabStripH = 36f, Pad = 8f, FloatHeaderH = 44f;
    private const string P = "PixelClicker.Dev.Hub.";

    public sealed class Tab
    {
        public string id, title;
        public Vector2 size;
        public RectTransform root, buttonRect, window, floatBody;
        public Image image, icon;
        public TMP_Text label;
        public float labelWidth;
        public Action<bool> onVisible;
        public bool floating, minimized, visible;
        public float width;
    }

    // ---- search bar (header): finds tabs, widgets and tester features; picking a result opens its tab and reveals the item ----

    private sealed class SearchEntry { public string label, keywords, tabId; public Action reveal; }
    private readonly List<SearchEntry> searchEntries = new List<SearchEntry>();
    private TMP_InputField searchField;
    private RectTransform searchPanel;
    private readonly List<Button> searchRows = new List<Button>();
    private readonly List<SearchEntry> shownResults = new List<SearchEntry>();
    private const int MaxResults = 8;
    private const float SearchX = 12f, SearchW = 250f, ResultsW = 356f;   // the box sits left of the centred title; the results drop down wider

    /// <summary>Adds something to the hub's search: its label (shown in the results), the tab it lives in, what to do once its tab is open (scroll to it...), and extra words that also match.</summary>
    public static void RegisterSearch(string label, string tabId, Action reveal, string keywords = "")
    {
        Boot();
        instance.searchEntries.Add(new SearchEntry { label = label, tabId = tabId, reveal = reveal, keywords = keywords ?? "" });
    }

    private void BuildSearch()
    {
        searchField = PixelUIKit.CreateInputField(font, windowRect, "Search Field", new Vector2(SearchW, 34f), new Color(0.06f, 0.07f, 0.1f, 1f), TextColor, 20f, "Search the hub...");
        PlaceTL(searchField.GetComponent<RectTransform>(), SearchX, (HeaderH - 34f) * 0.5f, SearchW, 34f);   // (the left of the header, clear of the centred title)
        searchField.onValueChanged.AddListener(RefreshSearch);
        searchField.onSubmit.AddListener(_ => { if (shownResults.Count > 0) ChooseResult(shownResults[0]); });
        GameObject panelGo = new GameObject("Search Results", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(windowRect, false);
        panelGo.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.18f, 0.98f);
        PixelUIKit.StyleBox(panelGo.GetComponent<Image>());
        searchPanel = panelGo.GetComponent<RectTransform>();
        PlaceTL(searchPanel, SearchX, HeaderH - 6f, ResultsW, 40f);
        panelGo.SetActive(false);
        foreach (Tab t in tabs) searchEntries.Add(new SearchEntry { label = "Tab: " + t.title, tabId = t.id, keywords = t.id });
    }

    private void RefreshSearch(string text)
    {
        string q = (text ?? "").Trim().ToLowerInvariant();
        shownResults.Clear();
        if (q.Length > 0)
        {
            List<SearchEntry> starts = new List<SearchEntry>(), contains = new List<SearchEntry>();
            foreach (SearchEntry e in searchEntries)
            {
                string label = e.label.ToLowerInvariant();
                int at = label.IndexOf(q, StringComparison.Ordinal);
                bool afterColon = at >= 0 && label.IndexOf(": ", StringComparison.Ordinal) is int c && c >= 0 && at >= c + 2 && at == c + 2;
                if (afterColon) starts.Add(e);
                else if (at >= 0 || e.keywords.ToLowerInvariant().Contains(q)) contains.Add(e);
            }
            shownResults.AddRange(starts);
            shownResults.AddRange(contains);
            if (shownResults.Count > MaxResults) shownResults.RemoveRange(MaxResults, shownResults.Count - MaxResults);
        }
        searchPanel.gameObject.SetActive(shownResults.Count > 0 || q.Length > 0);
        while (searchRows.Count < MaxResults)
        {
            int index = searchRows.Count;
            Button b = PixelUIKit.CreateButton(font, searchPanel, "Result " + index, "", new Vector2(340f, 32f), new Color(0.2f, 0.26f, 0.38f, 1f), TextColor, 18f);
            TMP_Text l = b.GetComponentInChildren<TMP_Text>();
            l.alignment = TextAlignmentOptions.MidlineLeft;
            l.rectTransform.offsetMin = new Vector2(10f, 0f);
            PixelUIKit.FitOneLine(l, 11f);
            b.onClick.AddListener(() => { if (index < shownResults.Count) ChooseResult(shownResults[index]); });
            searchRows.Add(b);
        }
        for (int i = 0; i < searchRows.Count; i++)
        {
            bool on = i < shownResults.Count;
            searchRows[i].gameObject.SetActive(on);
            if (!on) continue;
            searchRows[i].GetComponentInChildren<TMP_Text>().text = shownResults[i].label;
            PlaceTL((RectTransform)searchRows[i].transform, 8f, 4f + i * 34f, 340f, 32f);
        }
        if (q.Length > 0 && shownResults.Count == 0 && searchRows.Count > 0)
        {
            searchRows[0].gameObject.SetActive(true);
            searchRows[0].GetComponentInChildren<TMP_Text>().text = "Nothing found";
            PlaceTL((RectTransform)searchRows[0].transform, 8f, 4f, 340f, 32f);
            searchPanel.sizeDelta = new Vector2(ResultsW, 40f);
        }
        else searchPanel.sizeDelta = new Vector2(ResultsW, 8f + shownResults.Count * 34f);
        searchPanel.SetAsLastSibling();
    }

    private void ChooseResult(SearchEntry e)
    {
        searchField.SetTextWithoutNotify("");
        searchPanel.gameObject.SetActive(false);
        shownResults.Clear();
        if (!open) SetOpen(true);
        if (chrome != null) chrome.Wake();   // a minimized (or tucked away docked) hub opens up to show the result
        RevealTab(e.tabId);
        StartCoroutine(RevealNextFrame(e));   // (the window has its full size again by then, so scrolling to the item lands right)
    }

    private System.Collections.IEnumerator RevealNextFrame(SearchEntry e)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        e.reveal?.Invoke();
    }

    private void RevealTab(string id)
    {
        Tab t = Find(id);
        if (t == null) return;
        if (t.floating) { if (t.window != null) t.window.SetAsLastSibling(); }
        else { active = t; LayoutTabs(null); SaveState(); }
    }

    private static PixelDevHub instance;
    public static bool IsOpen => instance != null && instance.open;
    public static event Action<bool> OpenChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; OpenChanged = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Hub").AddComponent<PixelDevHub>();
    }

    public static void Open() { Boot(); instance.SetOpen(true); }

    /// <summary>Opens the hub on this tab (a floating tab comes to the front).</summary>
    public static void OpenTab(string id) { Open(); instance.RevealTab(id); }
    public static void Close() { if (instance != null) instance.SetOpen(false); }
    public static void Toggle() { Boot(); instance.SetOpen(!instance.open); }

    /// <summary>The canvas everything of the hub lives on (the Dev Tools tooltip lives there too).</summary>
    public static GameObject CanvasObject { get { Boot(); return instance.canvasRoot; } }

    /// <summary>The content root of a tab ('main', 'todo', 'changelog', 'inspector', 'shapes', 'uimock').</summary>
    public static RectTransform TabRoot(string id)
    {
        Boot();
        Tab t = instance.Find(id);
        return t != null ? t.root : null;
    }

    /// <summary>Called with true / false when the tab becomes / stops being visible.</summary>
    public static void SetTabCallback(string id, Action<bool> onVisible)
    {
        Boot();
        Tab t = instance.Find(id);
        if (t != null) t.onVisible = onVisible;
    }

    // ------------------------------------------------------------------

    private static readonly Color PanelColor = new Color(0.08f, 0.1f, 0.16f, 0.97f), BodyColor = new Color(0.12f, 0.15f, 0.23f, 1f),
                                  TabOff = new Color(0.2f, 0.24f, 0.34f, 1f), TextColor = new Color(0.92f, 0.94f, 1f, 1f);

    private TMP_FontAsset font;
    private GameObject canvasRoot;
    private RectTransform canvasRect, windowRect, strip, content;
    private PixelWindowChrome chrome;
    private readonly List<Tab> tabs = new List<Tab>();
    private Tab active;
    private bool open;

    private Tab Find(string id) { foreach (Tab t in tabs) if (t.id == id) return t; return null; }

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private float Scale => canvasRoot != null && canvasRoot.GetComponent<Canvas>().scaleFactor > 0f ? canvasRoot.GetComponent<Canvas>().scaleFactor : 1f;

    private void Awake()
    {
        instance = this;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("Dev Hub UI", 700, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        // ---- the main window ----
        // A standard Tool window (PixelUIKit.CreateWindow): centred title, grey X, drag by the header, minimize, dock to a screen edge, place saved under its old key.
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, canvasRoot.transform, "Hub Window", "Dev Hub", new Vector2(ContentW + Pad * 2f, HeaderH + TabStripH + ContentH + Pad),
                                                         PanelColor, PixelUIKit.WindowKind.Tool, () => SetOpen(false), P + "Window",
                                                         pop: false);   // (it opens with its canvas, and its resize grip SCALES it: PixelPop would keep undoing that)
        parts.Root.AddComponent<PixelBringToFront>();
        windowRect = parts.Rect;
        chrome = parts.Chrome;

        GameObject stripGo = new GameObject("Tab Strip", typeof(RectTransform));
        stripGo.transform.SetParent(windowRect, false);
        strip = stripGo.GetComponent<RectTransform>();
        PlaceTL(strip, Pad, HeaderH, ContentW, TabStripH);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(Image));
        contentGo.transform.SetParent(windowRect, false);
        content = contentGo.GetComponent<RectTransform>();
        PlaceTL(content, Pad, HeaderH + TabStripH, ContentW, ContentH);
        contentGo.GetComponent<Image>().color = BodyColor;
        contentGo.GetComponent<Image>().raycastTarget = false;
        PixelUIKit.StyleBox(contentGo.GetComponent<Image>(), true);

        // ---- the tabs ----
        string[,] defs =
        {
            { "main", "Main", "700" }, { "docs", "Docs", "700" }, { "tester", "Tester", "700" },
            { "inspector", "Inspector", "700" }, { "shapes", "Shapes", "500" }, { "uimock", "UI Mock", "500" },
            { "looks", "Looks", "700" }, { "setups", "Setups", "700" }, { "events", "Events", "700" },
            { "sounds", "Sounds", "700" }, { "console", "Console", "700" }, { "models", "Models", "700" },
        };
        string saved = PixelPrefs.GetString(P + "Order", "");
        List<string> order = new List<string>(saved.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        for (int i = 0; i < defs.GetLength(0); i++) if (!order.Contains(defs[i, 0])) order.Add(defs[i, 0]);
        foreach (string id in order)
        {
            for (int i = 0; i < defs.GetLength(0); i++)
            {
                if (defs[i, 0] != id) continue;
                Tab t = new Tab { id = id, title = defs[i, 1], size = new Vector2(float.Parse(defs[i, 2]), ContentH) };
                BuildTab(t);
                tabs.Add(t);
            }
        }
        active = Find(PixelPrefs.GetString(P + "Active", "main")) ?? tabs[0];

        chrome.EnableResize();   // the grip in the bottom-right corner scales the whole hub
        BuildSearch();
        chrome.Keep(searchField.gameObject);
        gameObject.AddComponent<PixelDevTextEdit>().Setup(canvasRoot.transform, font);   // right click any label to set your own text

        // Tabs that were floating when the game last closed come back where they were.
        foreach (string id in PixelPrefs.GetString(P + "Floating", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            Tab t = Find(id);
            if (t == null || DockedCount() <= 1) continue;
            PopOut(t, new Vector2(PixelPrefs.GetFloat(P + "Float." + id + ".X", 200f), PixelPrefs.GetFloat(P + "Float." + id + ".Y", -200f)), true);
        }
        if (active.floating) active = FirstDocked();
        LayoutTabs(null);

        // The tools that live in tabs (they ask for their roots with TabRoot).
        Find("shapes").onVisible = PixelDevShapes.SetActive;
        Find("uimock").onVisible = PixelDevMockup.SetActive;
        PixelDevInspector.Boot();
        PixelDevDocs.Boot();
        PixelFeatureTester.Boot();
        PixelLookStudio.Boot();
        PixelDevSetups.Boot();
        PixelDevEvents.Boot();
        PixelDevSounds.Boot();
        PixelDevConsole.Boot();
        PixelModelEditor.Boot();
        windowRect.gameObject.SetActive(true);
        canvasRoot.SetActive(false);
    }

    // ---- tab pictures: tiny pixel-art icons drawn from text ----

    private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

    private static Sprite TabIcon(string id)
    {
        if (iconCache.TryGetValue(id, out Sprite cached) && cached != null) return cached;
        string[] rows;
        switch (id)
        {
            case "main":       // sliders
                rows = new[] { "............", ".##########.", "....##......", "............", ".##########.", "........##..", "............", ".##########.", "..##........", "............" }; break;
            case "tester":     // a flask
                rows = new[] { "...######...", "...#....#...", "...#....#...", "...#....#...", "..#......#..", ".#...##...#.", ".#..####..#.", ".#........#.", "..########..", "............" }; break;
            case "docs":       // a book
                rows = new[] { "............", ".#########..", ".#.......##.", ".#.#####.#..", ".#.......#..", ".#.#####.#..", ".#.......#..", ".#.#####.#..", ".#########..", "............" }; break;
            case "inspector":  // a magnifying glass
                rows = new[] { "............", "...#####....", "..#.....#...", ".#.......#..", ".#.......#..", ".#.......#..", "..#.....#...", "...#####.#..", ".........##.", "..........##" }; break;
            case "looks":      // a paint brush
                rows = new[] { "..........##", ".........###", "........###.", ".......###..", "......###...", "....####....", "...####.....", "..####......", ".###........", "##.........." }; break;
            case "setups":     // a floppy disk
                rows = new[] { "##########..", "#.######.##.", "#.######.###", "#.######...#", "#..........#", "#.########.#", "#.#......#.#", "#.#......#.#", "############", "............" }; break;
            case "events":     // a lightning bolt
                rows = new[] { "......###...", ".....###....", "....###.....", "...#######..", ".....####...", "....###.....", "...###......", "..##........", ".#..........", "............" }; break;
            case "sounds":     // a speaker with waves
                rows = new[] { "....#....#..", "...##..#..#.", "#####...#.#.", "#####.#.#.#.", "#####.#.#.#.", "#####...#.#.", "...##..#..#.", "....#....#..", "............", "............" }; break;
            case "console":    // a terminal prompt
                rows = new[] { "############", "#..........#", "#.#........#", "#..#.......#", "#...#......#", "#..#.......#", "#.#..####..#", "#..........#", "############", "............" }; break;
            case "models":     // a cube in perspective
                rows = new[] { "...######...", "..#.....##..", ".#.....#.#..", "#######..#..", "#.....#..#..", "#.....#..#..", "#.....#.#...", "#.....##....", "#######.....", "............" }; break;
            case "shapes":     // a square and a circle
                rows = new[] { "#######.....", "#.....#.....", "#.....#.##..", "#.....####..", "#######.###.", "......#####.", "......#####.", ".......###..", "............", "............" }; break;
            default:           // a window with a title bar (UI mock)
                rows = new[] { "############", "#..........#", "############", "#..........#", "#..###.....#", "#..........#", "#..######..#", "#..........#", "############", "............" }; break;
        }
        int w = 12, h = rows.Length;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, x < rows[y].Length && rows[y][x] == '#' ? Color.white : new Color(1f, 1f, 1f, 0f));
        tex.Apply(false, true);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        iconCache[id] = sp;
        return sp;
    }

    private int DockedCount() { int n = 0; foreach (Tab t in tabs) if (!t.floating) n++; return n; }
    private Tab FirstDocked() { foreach (Tab t in tabs) if (!t.floating) return t; return null; }

    private void BuildTab(Tab t)
    {
        GameObject r = new GameObject("Tab " + t.id, typeof(RectTransform));
        r.transform.SetParent(content, false);
        t.root = r.GetComponent<RectTransform>();
        PlaceTL(t.root, (ContentW - t.size.x) * 0.5f, 0f, t.size.x, t.size.y);
        r.SetActive(false);

        Button b = PixelUIKit.CreateButton(font, strip, "Tab Button " + t.id, t.title, new Vector2(100f, TabStripH), TabOff, TextColor, 18f);
        PixelUIKit.Caps(b);
        TMP_Text label = b.GetComponentInChildren<TMP_Text>();
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.rectTransform.offsetMin = new Vector2(36f, 0f);
        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        t.label = label;
        t.labelWidth = Mathf.Ceil(label.GetPreferredValues(t.title.ToUpperInvariant()).x);
        t.width = 44f;
        // The small picture (the only thing an inactive tab shows).
        GameObject ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        ig.transform.SetParent(b.transform, false);
        t.icon = ig.GetComponent<Image>();
        t.icon.sprite = TabIcon(t.id);
        t.icon.raycastTarget = false;
        t.icon.color = TextColor;
        RectTransform ir = ig.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(22f, 22f);
        ir.anchoredPosition = new Vector2(10f, 0f);
        t.buttonRect = b.GetComponent<RectTransform>();
        t.buttonRect.anchorMin = t.buttonRect.anchorMax = t.buttonRect.pivot = new Vector2(0f, 1f);
        t.image = b.GetComponent<Image>();
        b.onClick.AddListener(() => { active = t; LayoutTabs(null); SaveState(); });
        PixelHubTabHandle h = b.gameObject.AddComponent<PixelHubTabHandle>();
        h.hub = this; h.tab = t;
    }

    /// <summary>Puts the docked tabs in a row (the active one raised and joined to the body); 'skip' (a tab being dragged) keeps its own place.</summary>
    private void LayoutTabs(Tab skip)
    {
        float x = 4f;
        foreach (Tab t in tabs)
        {
            if (t.floating) { t.buttonRect.gameObject.SetActive(false); continue; }
            t.buttonRect.gameObject.SetActive(true);
            bool on = t == active;
            t.width = on ? 36f + t.labelWidth + 14f : 44f;   // the active tab shows its name, the others only their picture
            t.label.text = on ? t.title.ToUpperInvariant() : "";
            float h = on ? TabStripH : TabStripH - 6f;
            t.buttonRect.sizeDelta = new Vector2(t.width, h);
            if (t != skip) t.buttonRect.anchoredPosition = new Vector2(x, on ? 0f : -6f);
            t.image.color = on ? BodyColor : TabOff;
            x += t.width - 6f;
        }
        if (active != null && !active.floating && active != skip) active.buttonRect.SetAsLastSibling();   // the active tab overlaps its neighbours like a folder tab
    }

    // ---- tab dragging (reorder, pop out) ----

    internal void TabBeginDrag(Tab t) { t.buttonRect.SetAsLastSibling(); }

    internal void TabDrag(Tab t, Vector2 delta)
    {
        Vector2 p = t.buttonRect.anchoredPosition + delta / Scale;
        p.x = Mathf.Clamp(p.x, -30f, ContentW - 40f);
        p.y = Mathf.Min(p.y, 6f);
        t.buttonRect.anchoredPosition = p;
        // Reorder while it slides past its neighbours.
        for (int guard = 0; guard < 6; guard++)
        {
            List<Tab> docked = tabs.FindAll(x => !x.floating);
            int i = docked.IndexOf(t);
            float centre = p.x + t.width * 0.5f;
            if (i > 0 && centre < docked[i - 1].buttonRect.anchoredPosition.x + docked[i - 1].width * 0.5f) Swap(t, docked[i - 1]);
            else if (i < docked.Count - 1 && centre > docked[i + 1].buttonRect.anchoredPosition.x + docked[i + 1].width * 0.5f) Swap(t, docked[i + 1]);
            else break;
            LayoutTabs(t);
        }
    }

    private void Swap(Tab a, Tab b)
    {
        int ia = tabs.IndexOf(a), ib = tabs.IndexOf(b);
        tabs[ia] = b; tabs[ib] = a;
    }

    internal void TabEndDrag(Tab t, Vector2 screenPos)
    {
        bool pulled = t.buttonRect.anchoredPosition.y < -(TabStripH + 30f) && DockedCount() > 1;
        if (pulled)
        {
            if (active == t) active = FirstDockedOtherThan(t);
            PopOut(t, screenPos, false);
        }
        LayoutTabs(null);
        SaveState();
    }

    private Tab FirstDockedOtherThan(Tab t) { foreach (Tab x in tabs) if (!x.floating && x != t) return x; return t; }

    // ---- floating windows ----

    private Vector2 CanvasSize => canvasRect.rect.size;

    private void PopOut(Tab t, Vector2 where, bool whereIsCanvasPos)
    {
        t.floating = true;
        t.minimized = false;
        float w = t.size.x + Pad * 2f, h = FloatHeaderH + t.size.y + Pad;
        GameObject g = new GameObject("Float " + t.id, typeof(RectTransform), typeof(Image), typeof(PixelBringToFront));
        g.transform.SetParent(canvasRoot.transform, false);
        t.window = g.GetComponent<RectTransform>();
        PixelUIKit.StyleWindow(g.GetComponent<Image>(), PanelColor);
        Vector2 cs = CanvasSize;
        Vector2 pos;
        if (whereIsCanvasPos) pos = where;
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, where, null, out Vector2 local);
            pos = new Vector2(local.x + cs.x * 0.5f - w * 0.5f, local.y - cs.y * 0.5f + 24f);   // canvas coordinates: x right, y down is negative
        }
        PlaceTL(t.window, pos.x, -pos.y, w, h);
        ClampFloat(t);

        GameObject head = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(PixelPopoutDrag));
        head.transform.SetParent(t.window, false);
        PlaceTL(head.GetComponent<RectTransform>(), 0f, 0f, w, FloatHeaderH);
        head.GetComponent<Image>().color = new Color(0.14f, 0.18f, 0.28f, 1f);
        PixelPopoutDrag drag = head.GetComponent<PixelPopoutDrag>();
        drag.target = t.window;
        drag.canvas = canvasRoot.GetComponent<Canvas>();
        drag.onMoved = () => ClampFloat(t);
        drag.onEnd = () => FloatDropped(t);
        TMP_Text title = PixelUIKit.CreateText(font, head.transform, "Title", t.title.ToUpperInvariant(), 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, TextColor);
        PlaceTL(title.rectTransform, 14f, 0f, w - 190f, FloatHeaderH);
        title.raycastTarget = false;
        Button dock = PixelUIKit.CreateButton(font, head.transform, "Dock", "Dock", new Vector2(80f, 34f), new Color(0.25f, 0.45f, 0.8f, 1f), TextColor, 20f);
        PlaceTL(dock.GetComponent<RectTransform>(), w - 140f, 5f, 80f, 34f);
        dock.onClick.AddListener(() => Dock(t));
        Button min = PixelUIKit.CreateButton(font, head.transform, "Minimize", "-", new Vector2(40f, 34f), new Color(0.3f, 0.36f, 0.5f, 1f), TextColor, 26f);
        PlaceTL(min.GetComponent<RectTransform>(), w - 52f, 5f, 40f, 34f);
        min.onClick.AddListener(() => ToggleFloatMinimized(t));

        GameObject body = new GameObject("Body", typeof(RectTransform), typeof(Image));
        body.transform.SetParent(t.window, false);
        t.floatBody = body.GetComponent<RectTransform>();
        PlaceTL(t.floatBody, Pad, FloatHeaderH, t.size.x, t.size.y);
        body.GetComponent<Image>().color = BodyColor;
        body.GetComponent<Image>().raycastTarget = false;
        PixelUIKit.StyleBox(body.GetComponent<Image>(), true);

        t.root.SetParent(t.floatBody, false);
        PlaceTL(t.root, 0f, 0f, t.size.x, t.size.y);
        if (!whereIsCanvasPos) SaveState();
    }

    private void ToggleFloatMinimized(Tab t)
    {
        t.minimized = !t.minimized;
        t.floatBody.gameObject.SetActive(!t.minimized);
        t.window.sizeDelta = new Vector2(t.window.sizeDelta.x, t.minimized ? FloatHeaderH : FloatHeaderH + t.size.y + Pad);
    }

    private void ClampFloat(Tab t)
    {
        Vector2 cs = CanvasSize;
        if (cs.x < 100f) return;
        Vector2 p = t.window.anchoredPosition;
        p.x = Mathf.Clamp(p.x, -t.window.sizeDelta.x + 120f, cs.x - 120f);
        p.y = Mathf.Clamp(p.y, -(cs.y - FloatHeaderH), 0f);
        t.window.anchoredPosition = p;
    }

    private void FloatDropped(Tab t)
    {
        // Let go over the hub window: it goes back in as a tab.
        Vector3[] corners = new Vector3[4];
        windowRect.GetWorldCorners(corners);
        Vector2 pointer = PixelInput.PointerPosition();
        bool overHub = windowRect.gameObject.activeSelf && !chrome.Minimized && pointer.x >= corners[0].x && pointer.x <= corners[2].x && pointer.y >= corners[0].y && pointer.y <= corners[2].y;
        if (overHub) { Dock(t); return; }
        PixelPrefs.SetFloat(P + "Float." + t.id + ".X", t.window.anchoredPosition.x);
        PixelPrefs.SetFloat(P + "Float." + t.id + ".Y", t.window.anchoredPosition.y);
    }

    private void Dock(Tab t)
    {
        if (!t.floating) return;
        t.floating = false;
        t.root.SetParent(content, false);
        PlaceTL(t.root, (ContentW - t.size.x) * 0.5f, 0f, t.size.x, t.size.y);
        if (t.window != null) Destroy(t.window.gameObject);
        t.window = null; t.floatBody = null;
        active = t;
        LayoutTabs(null);
        SaveState();
        PixelAudio.Play("ui_click");
    }

    private void SaveState()
    {
        List<string> order = new List<string>(), floating = new List<string>();
        foreach (Tab t in tabs) { order.Add(t.id); if (t.floating) floating.Add(t.id); }
        PixelPrefs.SetString(P + "Order", string.Join(",", order));
        PixelPrefs.SetString(P + "Floating", string.Join(",", floating));
        PixelPrefs.SetString(P + "Active", active != null ? active.id : "main");
    }

    // ------------------------------------------------------------------

    private void SetOpen(bool on)
    {
        if (open == on) return;
        open = on;
        canvasRoot.SetActive(on);
        PixelPrefs.SetInt(P + "Open", on ? 1 : 0);
        PixelDevMockup.SetWindowOpen(on);
        OpenChanged?.Invoke(on);
        if (!on) UpdateVisibility();
    }

    private void Start()
    {
        if (PixelPrefs.GetInt(P + "Open", 0) != 0) SetOpen(true);   // (here, not in Awake: the tools that boot the hub from their own Awake must be finished first)
        PixelWindows.Register(this, 200, () => open, () => SetOpen(false));   // Escape closes it
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
    }

    private void Update()
    {
        UpdateVisibility();
    }

    /// <summary>Docked tabs show only while active; floating ones while not minimized; callbacks fire when a tab appears / disappears (the hub being closed or minimized hides all).</summary>
    private void UpdateVisibility()
    {
        bool hubMin = chrome != null && chrome.Minimized;
        foreach (Tab t in tabs)
        {
            bool shown = t.floating ? !t.minimized : t == active;
            if (t.root.gameObject.activeSelf != shown) t.root.gameObject.SetActive(shown);
            bool vis = open && shown && (t.floating || !hubMin);
            if (vis == t.visible) continue;
            t.visible = vis;
            t.onVisible?.Invoke(vis);
        }
    }
}

/// <summary>Raises a window above the others when it is clicked.</summary>
public class PixelBringToFront : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData e) { transform.SetAsLastSibling(); }
}

/// <summary>Drag handler of a hub tab button (the click is the button's own).</summary>
public class PixelHubTabHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PixelDevHub hub;
    public PixelDevHub.Tab tab;
    public void OnBeginDrag(PointerEventData e) { hub.TabBeginDrag(tab); }
    public void OnDrag(PointerEventData e) { hub.TabDrag(tab, e.delta); }
    public void OnEndDrag(PointerEventData e) { hub.TabEndDrag(tab, e.position); }
}
