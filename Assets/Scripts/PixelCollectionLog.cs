// ---- System notes ----
// PixelCollectionLog.cs
//   The Collection Log: a book of the rare collectibles (the seven Dragon Cubes, the three God Cubes, the six Prime Cubes and the Prime Cubelette). Component added by PixelClicker.Awake;
//   opened by the small BOOK button at the top-left of the Log window (PixelLog.BuildUI -> PixelCollectionLog.Toggle()); Escape / the X close it (PixelWindows priority 70).
//   A spread of two pages: a title page (with how many are found), then one entry per page. Click the right page to turn forward, the left page to turn back (the page squashes like a turning leaf).
//   An entry is '???' (name, text and a black silhouette of its picture) until the cube is KNOWN = its tier's totalCollected > 0 (lifetime total, so spending it - the wish, the Gauntlet - never hides it).
//   GALLERY: every entry page has a small CAMERA button (top right of the page) that walks you into PixelCollectionGallery (a pitch black first person room with all the cubes floating and lit, built on entering and
//   destroyed on leaving); the settings are under the Gallery header here, and leaving brings the book back on the same page.
//   Pictures are PixelTierPicture (a real model, turning). To add an entry: add a line to Entries (type + flavour text).
//   The book is a standard Fixed window (PixelUIKit.CreateWindow, styled: false so it keeps its plain leather cover; no window title, the pages have
//   their own): the standard grey X sits in the leather band at the top, and the pages start below the standard header height.
// ----------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelCollectionLog : MonoBehaviour
{
    [Header("Book")]
    [Tooltip("Size of the open book (canvas units). 0 = 1240 x 812.")]
    [SerializeField] private Vector2 bookSize = Vector2.zero;
    [Tooltip("Seconds a page takes to turn. 0 = 0.28.")]
    [SerializeField] private float turnSeconds = 0f;
    [Tooltip("Text shown for anything not found yet.")]
    [SerializeField] private string unknownText = "???";
    [Tooltip("Hint at the bottom of the book.")]
    [SerializeField] private string hintText = "Click the right page to turn forward, the left page to turn back";

    [Header("Gallery (the camera button on an entry)")]
    [Tooltip("Size of the gallery room's floor (canvas = world units). 0 = 20.")]
    [SerializeField] private float galleryRoomSize = 0f;
    [Tooltip("Size of the square you can walk in (the room is bigger). 0 = 10.")]
    [SerializeField] private float galleryBoundsSize = 0f;
    [Tooltip("Walking speed in the gallery (units per second). 0 = 4.")]
    [SerializeField] private float galleryWalkSpeed = 0f;
    [Tooltip("How much faster Shift makes you. 0 = 1.8.")]
    [SerializeField] private float galleryRunFactor = 0f;
    [Tooltip("Mouse look speed in the gallery. 0 = 0.12.")]
    [SerializeField] private float galleryLookSensitivity = 0f;
    [Tooltip("Size of a floating cube. 0 = 0.9.")]
    [SerializeField] private float galleryItemSize = 0f;
    [Tooltip("Height of the floating cubes above the floor. 0 = 1.5.")]
    [SerializeField] private float galleryItemHeight = 0f;
    [Tooltip("How far each cube's light reaches. 0 = 5.")]
    [SerializeField] private float galleryLightRange = 0f;
    [Tooltip("Brightness of each cube's light. 0 = 3.")]
    [SerializeField] private float galleryLightIntensity = 0f;

    private struct Entry { public PixelClicker.PixelType type; public string section, flavour; }

    private static readonly Entry[] Entries = BuildEntries();

    /// <summary>One entry per member of every super-rare set, in the table's order (PixelRareSets: section, flavour).</summary>
    private static Entry[] BuildEntries()
    {
        System.Collections.Generic.List<Entry> list = new System.Collections.Generic.List<Entry>();
        foreach (PixelRareSets.Set set in PixelRareSets.All)
            foreach (PixelRareSets.Member m in set.members)
                list.Add(new Entry { type = m.type, section = set.logSection, flavour = m.flavour });
        return list.ToArray();
    }

    // The Gallery reads the entries through these.
    public static int EntryCount => Entries.Length;
    public static PixelClicker.PixelType EntryType(int i) => Entries[i].type;
    public static string EntryFlavour(int i) => Entries[i].flavour;

    /// <summary>Has this entry's cube been found (its tier's lifetime total is above 0)? 'tier' = the tier's index (-1 when there is none).</summary>
    public static bool EntryKnown(PixelClicker clicker, int i, out int tier)
    {
        tier = clicker != null ? clicker.IndexOf(Entries[i].type) : -1;
        return tier >= 0 && (clicker.Tiers[tier].totalCollected > 0d || clicker.Tiers[tier].count > 0d);
    }

    public static string EntryName(PixelClicker clicker, int i) => EntryKnown(clicker, i, out int tier) ? clicker.Tiers[tier].displayName : "???";

    /// <summary>Opens the book on this entry's page (the gallery returns here).</summary>
    public static void ShowEntry(int entry)
    {
        if (instance == null) return;
        instance.Open();
        instance.ShowSpread((Mathf.Clamp(entry, 0, Entries.Length - 1) + 2) / 2);
    }

    private struct Page
    {
        public Button cameraButton;
        public int entry;
        public RectTransform root;
        public TMP_Text section, title, body, foot, number;
        public RawImage picture;
    }

    private static PixelCollectionLog instance;
    private PixelClicker clicker;
    private GameObject canvasRoot, bookObject;
    private Page left, right;
    private int spread;                  // 0 = title page + entry 1
    private float turnT = -1f;           // -1 = not turning
    private bool turnForward, swapped;
    private static Sprite bookSprite;
    private float refreshTimer;

    private int PageCount => Entries.Length + 1;
    private int SpreadCount => PageCount / 2 + 1;   // spread s shows pages 2s - 1 and 2s

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; bookSprite = null; }

    private void Awake()
    {
        instance = this;
        PixelWindows.Register(this, 70, () => IsOpen, Close);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    private bool IsOpen => bookObject != null && PixelPop.IsOpen(bookObject);

    /// <summary>Opens the book (or closes it when it is open): the Log window's book button.</summary>
    public static void Toggle()
    {
        if (instance == null) return;
        if (instance.IsOpen) instance.Close(); else instance.Open();
    }

    /// <summary>A little open-book picture for buttons (drawn once in code).</summary>
    public static Sprite BookSprite()
    {
        if (bookSprite != null) return bookSprite;
        const int w = 32, h = 24;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color clear = new Color(0f, 0f, 0f, 0f), cover = new Color(0.45f, 0.26f, 0.12f, 1f), page = new Color(0.96f, 0.91f, 0.78f, 1f), line = new Color(0.55f, 0.45f, 0.3f, 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = clear;
                if (y >= 2 && y < h - 1 && x >= 1 && x < w - 1) c = cover;                                  // the cover
                if (y >= 4 && y < h - 3 && x >= 3 && x < w - 3) c = x == w / 2 || x == w / 2 - 1 ? line : page;   // two pages, the spine between
                if (c == page && y % 4 == 0 && x > 5 && x < w - 5 && x != w / 2 && x != w / 2 - 1) c = line;      // text lines
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        bookSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        return bookSprite;
    }

    private void EnterGallery(int entry)
    {
        if (entry < 0 || entry >= Entries.Length || clicker == null) return;
        Close();   // the book goes away while you are in the room; the gallery brings it back
        PixelCollectionGallery.Enter(clicker, entry, new PixelCollectionGallery.Settings
        {
            roomSize = galleryRoomSize, boundsSize = galleryBoundsSize, walkSpeed = galleryWalkSpeed, runFactor = galleryRunFactor, lookSensitivity = galleryLookSensitivity,
            itemSize = galleryItemSize, itemHeight = galleryItemHeight, lightRange = galleryLightRange, lightIntensity = galleryLightIntensity,
        });
    }

    private void Open()
    {
        clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        PixelWindows.CloseAllExcept(this);
        if (bookObject == null) Build();
        spread = 0; turnT = -1f;
        ShowSpread(spread);
        PixelPop.Show(bookObject);
        PixelAudio.Play("ui_click");
    }

    private void Close()
    {
        if (bookObject != null && PixelPop.IsOpen(bookObject)) PixelPop.Hide(bookObject);
    }

    // ------------------------------------------------------------------

    private void Build()
    {
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("Collection Log", 600, new Vector2(1920f, 1080f), true);
        Vector2 size = bookSize.x > 100f && bookSize.y > 100f ? bookSize : new Vector2(1240f, 812f);   // (812: the pages keep their old 668 height under the 90 header strip)

        // The standard Fixed window with a plain leather cover instead of the usual style (the pages sit on it). No window title: each page has its own.
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, canvasRoot.transform, "Book", "", size, new Color(0.34f, 0.19f, 0.09f, 1f),
                                                         PixelUIKit.WindowKind.Fixed, Close, styled: false);
        bookObject = parts.Root;
        RectTransform br = parts.Rect;

        // The pages fill the book below the header strip (where the X sits) and above the hint line at the bottom.
        float pad = 26f, bottom = 54f, pageW = (size.x - pad * 2f - 8f) * 0.5f, pageH = size.y - parts.HeaderHeight - bottom;
        float pageY = (bottom - parts.HeaderHeight) * 0.5f;   // centre of the pages (the band above is wider than the one below)
        left = BuildPage(font, br, "Left Page", new Vector2(1f, 0.5f), new Vector2(-4f, pageY), new Vector2(pageW, pageH));
        right = BuildPage(font, br, "Right Page", new Vector2(0f, 0.5f), new Vector2(4f, pageY), new Vector2(pageW, pageH));
        // Pages: click right = forward, click left = back.
        left.cameraButton.onClick.AddListener(() => EnterGallery(left.entry));
        right.cameraButton.onClick.AddListener(() => EnterGallery(right.entry));
        PixelUIKit.MakeClickable(left.root.gameObject, () => Turn(false), sound: false, pressTint: false);   // (Turn plays the page sound)
        PixelUIKit.MakeClickable(right.root.gameObject, () => Turn(true), sound: false, pressTint: false);

        GameObject spine = new GameObject("Spine", typeof(RectTransform), typeof(Image));
        spine.transform.SetParent(br, false);
        spine.GetComponent<Image>().color = new Color(0.2f, 0.1f, 0.04f, 0.9f);
        spine.GetComponent<Image>().raycastTarget = false;
        RectTransform sr = spine.GetComponent<RectTransform>();
        sr.sizeDelta = new Vector2(8f, pageH + 6f); sr.anchoredPosition = new Vector2(0f, pageY);

        TMP_Text hint = PixelUIKit.CreateText(font, br, "Hint", hintText, 22f, TextAlignmentOptions.Center, FontStyles.Italic, new Color(1f, 0.9f, 0.7f, 0.8f));
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 34f); hr.anchoredPosition = new Vector2(0f, 4f);
    }

    private Page BuildPage(TMP_FontAsset font, RectTransform parent, string name, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        Page p = new Page();
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        p.root = go.GetComponent<RectTransform>();
        p.root.anchorMin = p.root.anchorMax = new Vector2(0.5f, 0.5f);
        p.root.pivot = pivot;
        p.root.sizeDelta = size;
        p.root.anchoredPosition = pos;
        go.GetComponent<Image>().color = new Color(0.94f, 0.89f, 0.76f, 1f);

        Color ink = new Color(0.22f, 0.14f, 0.07f, 1f);
        // The camera: walk round all the collection in the gallery, starting beside this entry.
        GameObject camGo = new GameObject("Gallery Button", typeof(RectTransform), typeof(Image));
        camGo.transform.SetParent(p.root, false);
        Image camImg = camGo.GetComponent<Image>();
        camImg.sprite = PixelCollectionGallery.CameraSprite(); camImg.preserveAspect = true;
        RectTransform camR = camGo.GetComponent<RectTransform>();
        camR.anchorMin = camR.anchorMax = camR.pivot = new Vector2(1f, 1f);
        camR.sizeDelta = new Vector2(64f, 48f); camR.anchoredPosition = new Vector2(-14f, -12f);
        p.cameraButton = PixelUIKit.MakeClickable(camGo, null);   // (what it opens is added by the caller)
        p.section = PixelUIKit.CreateText(font, p.root, "Section", "", 24f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.5f, 0.35f, 0.15f, 1f));
        Anchor(p.section.rectTransform, 1f, -16f, 36f);
        p.title = PixelUIKit.CreateText(font, p.root, "Title", "", 44f, TextAlignmentOptions.Center, FontStyles.Bold, ink);
        Anchor(p.title.rectTransform, 1f, -54f, 64f);
        p.title.enableAutoSizing = true; p.title.fontSizeMax = 44f; p.title.fontSizeMin = 20f;

        GameObject pic = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
        pic.transform.SetParent(p.root, false);
        p.picture = pic.GetComponent<RawImage>();
        p.picture.raycastTarget = false;
        RectTransform pr = pic.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(280f, 280f); pr.anchoredPosition = new Vector2(0f, -124f);

        p.body = PixelUIKit.CreateText(font, p.root, "Body", "", 28f, TextAlignmentOptions.Top, FontStyles.Normal, ink);
        RectTransform bodyR = p.body.rectTransform;
        bodyR.anchorMin = new Vector2(0f, 0f); bodyR.anchorMax = new Vector2(1f, 1f); bodyR.pivot = new Vector2(0.5f, 1f);
        bodyR.offsetMin = new Vector2(40f, 90f); bodyR.offsetMax = new Vector2(-40f, -420f);
        p.foot = PixelUIKit.CreateText(font, p.root, "Foot", "", 24f, TextAlignmentOptions.Center, FontStyles.Italic, new Color(0.4f, 0.28f, 0.12f, 1f));
        p.foot.rectTransform.anchorMin = new Vector2(0f, 0f); p.foot.rectTransform.anchorMax = new Vector2(1f, 0f); p.foot.rectTransform.pivot = new Vector2(0.5f, 0f);
        p.foot.rectTransform.sizeDelta = new Vector2(0f, 36f); p.foot.rectTransform.anchoredPosition = new Vector2(0f, 46f);
        p.number = PixelUIKit.CreateText(font, p.root, "Number", "", 22f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(0.4f, 0.28f, 0.12f, 0.8f));
        p.number.rectTransform.anchorMin = new Vector2(0f, 0f); p.number.rectTransform.anchorMax = new Vector2(1f, 0f); p.number.rectTransform.pivot = new Vector2(0.5f, 0f);
        p.number.rectTransform.sizeDelta = new Vector2(0f, 30f); p.number.rectTransform.anchoredPosition = new Vector2(0f, 10f);
        return p;
    }

    private static void Anchor(RectTransform r, float top, float y, float height)
    {
        r.anchorMin = new Vector2(0f, top); r.anchorMax = new Vector2(1f, top); r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-40f, height); r.anchoredPosition = new Vector2(0f, y);
    }

    // ------------------------------------------------------------------

    private bool Known(Entry e, out int tier, out double total)
    {
        tier = clicker.IndexOf(e.type); total = 0d;
        if (tier < 0) return false;
        total = clicker.Tiers[tier].totalCollected;
        return total > 0d || clicker.Tiers[tier].count > 0d;
    }

    private int FoundCount()
    {
        int n = 0;
        foreach (Entry e in Entries) if (Known(e, out _, out _)) n++;
        return n;
    }

    private void Fill(ref Page p, int pageIndex)
    {
        p.root.gameObject.SetActive(pageIndex >= 0 && pageIndex < PageCount);
        if (!p.root.gameObject.activeSelf) return;
        p.number.text = (pageIndex + 1).ToString();
        p.entry = pageIndex - 1;
        if (p.cameraButton != null) p.cameraButton.gameObject.SetActive(pageIndex >= 1);
        p.picture.texture = null; p.picture.enabled = false;
        if (pageIndex == 0)
        {
            p.section.text = "";
            p.title.text = "Collection Log";
            p.body.text = "Everything rare you have found, or have yet to find.\n\n<b>" + FoundCount() + " / " + Entries.Length + "</b> found\n\nTurn the page.";
            p.foot.text = "";
            return;
        }
        Entry e = Entries[pageIndex - 1];
        bool known = Known(e, out int tier, out double total);
        p.section.text = e.section;
        p.title.text = known ? clicker.Tiers[tier].displayName : unknownText;
        p.body.text = known ? e.flavour : unknownText;
        p.foot.text = known ? (total > 1d ? "Found " + total.ToString("0") + " times" : "Found") + (clicker.Tiers[tier].count >= 1d ? "  -  in your backpack" : "") : "Not found yet";
        if (tier >= 0)
        {
            Texture tex = PixelTierPicture.Get(clicker, tier, 256);
            if (tex != null) { p.picture.texture = tex; p.picture.enabled = true; p.picture.color = known ? Color.white : Color.black; }   // (an unknown one is a black silhouette)
        }
    }

    private void ShowSpread(int s)
    {
        spread = Mathf.Clamp(s, 0, SpreadCount - 1);
        Fill(ref left, spread * 2 - 1);   // the left page of spread 0 is empty (the book's inside cover)
        Fill(ref right, spread * 2);
        if (spread == 0) { left.root.gameObject.SetActive(true); left.section.text = ""; left.title.text = ""; left.body.text = ""; left.foot.text = ""; left.number.text = ""; left.picture.enabled = false; }
    }

    private void Turn(bool forward)
    {
        if (turnT >= 0f) return;
        int target = spread + (forward ? 1 : -1);
        if (target < 0 || target >= SpreadCount) return;
        turnT = 0f; turnForward = forward; swapped = false;
        PixelAudio.Play("ui_scroll");
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (turnT >= 0f)
        {
            float dur = turnSeconds > 0f ? turnSeconds : 0.28f;
            turnT += Time.unscaledDeltaTime / dur;
            Page turning = turnForward ? right : left;
            Page other = turnForward ? left : right;
            // The page being turned squashes to nothing at the spine, the content swaps, then it opens out again.
            float k = turnT < 0.5f ? 1f - turnT * 2f : (turnT - 0.5f) * 2f;
            if (turnT >= 0.5f && !swapped) { swapped = true; ShowSpread(spread + (turnForward ? 1 : -1)); }
            turning.root.localScale = new Vector3(Mathf.Clamp01(k), 1f, 1f);
            other.root.localScale = Vector3.one;
            if (turnT >= 1f) { turnT = -1f; left.root.localScale = right.root.localScale = Vector3.one; }
        }

        // The pictures turn slowly; the texts refresh now and then (a cube may have been found while the book is open).
        float yaw = Time.unscaledTime * 50f;
        for (int i = 0; i < Entries.Length; i++)
        {
            int tier = clicker.IndexOf(Entries[i].type);
            bool shown = (left.root.gameObject.activeSelf && spread * 2 - 1 == i + 1) || (right.root.gameObject.activeSelf && spread * 2 == i + 1);
            if (shown && tier >= 0) PixelTierPicture.Render(tier, yaw);
        }
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f && turnT < 0f) { refreshTimer = 1f; ShowSpread(spread); }
    }
}
