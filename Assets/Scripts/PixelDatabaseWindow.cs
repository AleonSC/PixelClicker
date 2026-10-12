using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Game Database window (a temporary 'Database' button in the Skills window opens it). A search box lists every pixel, shop pack, skill, consumable,
/// recipe, minigame, achievement and tip that matches; click one to see what it is and every REFERENCE to it in the game (click a reference to jump to
/// that entry). The entries and references come from <see cref="PixelDatabase"/>, rebuilt each time the window opens so they reflect the live game.
/// </summary>
public class PixelDatabaseWindow : MonoBehaviour
{
    private static PixelDatabaseWindow instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [Header("Window")]
    [Tooltip("Size of the window (canvas units).")]
    [SerializeField] private Vector2 windowSize = new Vector2(1560f, 900f);

    [Min(5)]
    [Tooltip("Most results listed at once.")]
    [SerializeField] private int maxResults = 250;

    [Min(5)]
    [Tooltip("Most references listed for one entry.")]
    [SerializeField] private int maxReferences = 400;

    [Tooltip("Window colour.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.14f, 0.98f);

    private PixelClicker clicker;
    private GameObject canvasRoot, windowObject;
    private TMP_InputField search;
    private RowList results, refList;
    private TMP_Text titleLabel, tagLabel, detailLabel, refHeader, emptyLabel;
    private PixelDatabase.Entry selected;
    private readonly List<string> history = new List<string>();

    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    /// <summary>Opens the window if it is closed, closes it otherwise.</summary>
    public static void Toggle()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("Game Database Window");
            instance = go.AddComponent<PixelDatabaseWindow>();
        }
        instance.ToggleInternal();
    }

    /// <summary>Closes the window if it is open (true when it was). The pause menu calls it first on Escape.</summary>
    public static bool CloseIfOpen()
    {
        if (!IsOpen) return false;
        PixelPop.Hide(instance.windowObject);
        return true;
    }

    public static bool IsOpen => instance != null && instance.windowObject != null && PixelPop.IsOpen(instance.windowObject);

    private void ToggleInternal()
    {
        if (windowObject == null) Build();
        if (PixelPop.IsOpen(windowObject)) { PixelPop.Hide(windowObject); return; }
        PixelDatabase.Build();
        selected = null;
        history.Clear();
        search.SetTextWithoutNotify("");
        PixelPop.Show(windowObject);
        Refresh();
        search.ActivateInputField();
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    private static void Place(RectTransform r, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = new Vector2(minX, minY);
        r.anchorMax = new Vector2(maxX, maxY);
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    private void Build()
    {
        clicker = PixelFind.First<PixelClicker>();
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("Game Database Canvas", 520, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);

        windowObject = new GameObject("Database Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(windowObject.GetComponent<Image>(), panelColor);
        RectTransform wr = windowObject.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = windowSize;
        wr.anchoredPosition = Vector2.zero;

        TMP_Text title = PixelUIKit.CreateText(Font, windowObject.transform, "Title", "Game Database", 52f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        PixelUIKit.Caps(title);
        Place(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(100f, -84f), new Vector2(-100f, -14f));

        Button close = PixelUIKit.CreateButton(Font, windowObject.transform, "Close", "X", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), Color.white, 36f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -12f);
        close.onClick.AddListener(() => PixelPop.Hide(windowObject));

        // Search box (left, under the title).
        search = PixelUIKit.CreateInputField(Font, windowObject.transform, "Search", new Vector2(640f, 70f), new Color(0.16f, 0.16f, 0.2f, 1f), Color.white, 30f,
                                             "Search pixels, packs, skills, items, tips...");
        PixelUIKit.AddSearchIcon(search);
        RectTransform sr = search.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
        sr.anchoredPosition = new Vector2(30f, -104f);
        search.onValueChanged.AddListener(_ => { selected = null; Refresh(); });

        // Left: the results.
        results = new RowList(this, windowObject.transform, "Results", 62f, 24f);
        RectTransform lr = results.scroll.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0f, 1f); lr.pivot = new Vector2(0f, 0.5f);
        lr.offsetMin = new Vector2(30f, 30f);
        lr.offsetMax = new Vector2(30f + 640f, -190f);

        // Right: the selected entry.
        GameObject detail = new GameObject("Detail", typeof(RectTransform), typeof(Image));
        detail.transform.SetParent(windowObject.transform, false);
        Image di = detail.GetComponent<Image>();
        di.color = new Color(0.13f, 0.13f, 0.17f, 1f);
        PixelUIKit.StyleBox(di);
        RectTransform dr = detail.GetComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = new Vector2(700f, 30f);
        dr.offsetMax = new Vector2(-30f, -104f);

        titleLabel = PixelUIKit.CreateText(Font, detail.transform, "Name", "", 44f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        titleLabel.enableAutoSizing = true; titleLabel.fontSizeMax = 44f; titleLabel.fontSizeMin = 20f;
        Place(titleLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -66f), new Vector2(-150f, -12f));
        tagLabel = PixelUIKit.CreateText(Font, detail.transform, "Category", "", 24f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, Color.white);
        Place(tagLabel.rectTransform, 1f, 1f, 1f, 1f, new Vector2(-300f, -60f), new Vector2(-22f, -16f));
        detailLabel = PixelUIKit.CreateText(Font, detail.transform, "Info", "", 24f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.85f, 0.87f, 0.92f));
        detailLabel.enableAutoSizing = true; detailLabel.fontSizeMax = 24f; detailLabel.fontSizeMin = 14f;
        Place(detailLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -272f), new Vector2(-22f, -76f));
        refHeader = PixelUIKit.CreateText(Font, detail.transform, "References Header", "", 28f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));
        Place(refHeader.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -318f), new Vector2(-22f, -276f));

        refList = new RowList(this, detail.transform, "References", 52f, 22f);
        RectTransform rr = refList.scroll.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
        rr.offsetMin = new Vector2(16f, 16f);
        rr.offsetMax = new Vector2(-16f, -326f);

        emptyLabel = PixelUIKit.CreateText(Font, windowObject.transform, "Empty", "Nothing found.", 30f, TextAlignmentOptions.Center, FontStyles.Italic, new Color(1f, 1f, 1f, 0.5f));
        Place(emptyLabel.rectTransform, 0f, 0f, 0f, 1f, new Vector2(30f, 300f), new Vector2(670f, -300f));
        emptyLabel.gameObject.SetActive(false);

        PixelWindows.Register(this, 16, () => windowObject != null && PixelPop.IsOpen(windowObject), () => PixelPop.Hide(windowObject));
        windowObject.SetActive(false);
    }

    // ------------------------------------------------------------------
    // Content
    // ------------------------------------------------------------------

    private static string Tag(string category) => "<color=#" + ColorUtility.ToHtmlStringRGB(PixelDatabase.CategoryColor(category)) + ">";

    private void Refresh()
    {
        List<PixelDatabase.Entry> found = PixelDatabase.Search(search != null ? search.text : "", maxResults);
        List<RowData> rows = new List<RowData>();
        foreach (PixelDatabase.Entry e in found)
        {
            PixelDatabase.Entry entry = e;
            rows.Add(new RowData
            {
                text = Tag(e.category) + "[" + e.category + "]</color>  " + e.name + (e.refs.Count > 0 ? "   <size=70%><alpha=#99>" + e.refs.Count + " ref" + (e.refs.Count == 1 ? "" : "s") + "</size>" : ""),
                click = () => Select(entry, true),
                highlight = selected == e,
            });
        }
        results.Fill(rows);
        emptyLabel.gameObject.SetActive(found.Count == 0);
        if (selected == null && found.Count > 0 && !string.IsNullOrWhiteSpace(search.text)) ShowEntry(found[0]);
        else ShowEntry(selected);
    }

    private void Select(PixelDatabase.Entry e, bool remember)
    {
        if (e == null) return;
        if (remember && selected != null) history.Add(selected.Key);
        ShowEntry(e);
    }

    private void ShowEntry(PixelDatabase.Entry e)
    {
        selected = e;
        if (e == null)
        {
            titleLabel.text = "";
            tagLabel.text = "";
            detailLabel.text = "Search the whole game: a pixel, a pack, a skill, a potion, a tip... Then click a result to see what it is and every place in the game that refers to it.";
            refHeader.text = "";
            refList.Fill(new List<RowData>());
            return;
        }
        titleLabel.text = e.name;
        tagLabel.text = Tag(e.category) + e.category.ToUpperInvariant() + "</color>";
        detailLabel.text = string.IsNullOrEmpty(e.detail) ? "(no description)" : e.detail;
        refHeader.text = "References in the game (" + e.refs.Count + ")";

        List<RowData> rows = new List<RowData>();
        if (history.Count > 0)
        {
            rows.Add(new RowData
            {
                text = "<color=#9FB4FF>< Back</color>",
                click = () =>
                {
                    PixelDatabase.Entry back = PixelDatabase.Find(history[history.Count - 1]);
                    history.RemoveAt(history.Count - 1);
                    if (back != null) ShowEntry(back);
                },
            });
        }
        for (int i = 0; i < e.refs.Count && i < maxReferences; i++)
        {
            PixelDatabase.Ref r = e.refs[i];
            PixelDatabase.Entry target = PixelDatabase.Find(r.key);
            rows.Add(new RowData
            {
                text = target != null ? Tag(target.category) + r.text + "</color>" : r.text,
                click = target != null ? (System.Action)(() => Select(target, true)) : null,
            });
        }
        if (e.refs.Count == 0) rows.Add(new RowData { text = "<alpha=#80>Nothing else in the game refers to this.", click = null });
        refList.Fill(rows);
        results.Highlight(e);
    }

    private void Update()
    {
        results?.Layout();
        refList?.Layout();
    }

    // ------------------------------------------------------------------
    // A scrolling list of clickable text rows (a small pool).
    // ------------------------------------------------------------------

    private struct RowData
    {
        public string text;
        public System.Action click;
        public bool highlight;
    }

    private class RowList
    {
        public readonly ScrollRect scroll;
        private readonly RectTransform content;
        private readonly GameObject bar;
        private readonly PixelDatabaseWindow owner;
        private readonly float rowHeight, fontSize;
        private readonly List<Button> pool = new List<Button>();
        private int count;
        private bool dirty;
        private List<RowData> data = new List<RowData>();

        public RowList(PixelDatabaseWindow window, Transform parent, string name, float rowH, float font)
        {
            owner = window;
            rowHeight = rowH;
            fontSize = font;
            scroll = PixelUIKit.CreateScrollView(parent, name, new Color(1f, 1f, 1f, 0.35f), 10f, 60f, out content, out bar);
        }

        public void Fill(List<RowData> rows)
        {
            data = rows;
            count = rows.Count;
            content.anchoredPosition = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                if (i >= pool.Count) pool.Add(MakeRow());
                Button b = pool[i];
                RowData d = rows[i];
                b.gameObject.SetActive(true);
                RectTransform r = b.GetComponent<RectTransform>();
                r.anchoredPosition = new Vector2(0f, -i * rowHeight);
                b.GetComponentInChildren<TMP_Text>().text = d.text;
                b.interactable = d.click != null;
                Image img = b.GetComponent<Image>();
                img.color = d.highlight ? new Color(0.28f, 0.34f, 0.5f, 1f) : d.click != null ? new Color(0.17f, 0.18f, 0.24f, 1f) : new Color(0.13f, 0.13f, 0.17f, 1f);
                b.onClick.RemoveAllListeners();
                if (d.click != null) b.onClick.AddListener(() => d.click());
            }
            for (int i = count; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
            dirty = true;
        }

        public void Highlight(PixelDatabase.Entry e)
        {
            // (The results list marks the selected row the next time it is filled; nothing to do per frame.)
        }

        public void Layout()
        {
            if (!dirty || scroll == null || scroll.viewport == null) return;
            float view = scroll.viewport.rect.height;
            if (view <= 1f) return;
            PixelUIKit.UpdateScrollView(scroll, bar, count * rowHeight, view);
            dirty = false;
        }

        private Button MakeRow()
        {
            Button b = PixelUIKit.CreateButton(owner.Font, content, "Row", "", new Vector2(0f, rowHeight - 4f), new Color(0.17f, 0.18f, 0.24f, 1f), Color.white, fontSize);
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(-14f, rowHeight - 4f);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = true;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = 12f;
            label.fontStyle = FontStyles.Normal;
            label.margin = new Vector4(14f, 0f, 10f, 0f);
#if UNITY_2023_1_OR_NEWER
            label.textWrappingMode = TextWrappingModes.NoWrap;
#else
            label.enableWordWrapping = false;
#endif
            label.overflowMode = TextOverflowModes.Ellipsis;
            return b;
        }
    }
}
