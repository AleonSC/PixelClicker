using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dev tool: a UI MOCKUP BUILDER. Drag out windows, boxes, buttons, text and temporary images (PNG / JPG files from the Mockups folder), move
/// them, resize them from the corner handle, layer them (front / back / up / down), change their transparency and colour, type their text and
/// set exact numbers. Every piece has a number badge and the panel lists each piece's position and size in the 1920 x 1080 canvas space (origin
/// top-left) so a screenshot plus "Copy layout" tells exactly where everything goes. Pieces are saved (PlayerPrefs, not part of the game save).
/// Opened from the Dev Tools tick box; <b>Reset all</b> deletes every piece.
/// </summary>
public class PixelDevMockup : MonoBehaviour
{
    public enum PieceKind { Window, Box, Button, Text, Image }

    private const string PrefData = "PixelClicker.Dev.MockupData", PrefKeep = "PixelClicker.Dev.MockupKeep";

    private static PixelDevMockup instance;

    /// <summary>The builder panel is open (pieces can be edited).</summary>
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Active = false; }

    public static void SetActive(bool on)
    {
        Boot();
        instance.Apply(on);
    }

    /// <summary>Mock up mode's window is open: the pieces stay visible even while the other tab is showing.</summary>
    public static void SetWindowOpen(bool open)
    {
        Boot();
        instance.windowOpen = open;
        instance.ApplyVisibility();
    }

    public static void Boot()
    {
        if (instance != null) return;
        GameObject go = new GameObject("Dev Mockup");
        instance = go.AddComponent<PixelDevMockup>();
    }

    [Serializable]
    private class PieceData
    {
        public int id;
        public int kind;
        public float x = 200f, y = 200f, w = 400f, h = 300f;
        public float alpha = 1f;
        public string colour = "";
        public string text = "";
        public int fontSize = 28;
        public string image = "";
    }

    [Serializable]
    private class SaveBlob
    {
        public int nextId = 1;
        public List<PieceData> pieces = new List<PieceData>();   // back to front
    }

    private class Piece
    {
        public PieceData data;
        public RectTransform rt;
        public Image bg;
        public RawImage raw;
        public TMP_Text label;
        public CanvasGroup group;
        public Outline outline;
        public GameObject handle;
        public GameObject badge;
        public TMP_Text badgeText;
    }

    private SaveBlob blob = new SaveBlob();
    private readonly List<Piece> pieces = new List<Piece>();
    private Piece selected;
    private bool keepWhenClosed;
    private bool hideAll;

    private TMP_FontAsset font;
    private GameObject piecesCanvas, panelCanvas;
    private bool windowOpen;
    private RectTransform piecesRoot;
    private Canvas piecesCanvasComponent;
    private TMP_InputField textField, xField, yField, wField, hField, colourField;
    private Slider alphaSlider;
    private TMP_Dropdown imageDropdown;
    private TMP_Text readout, selectedLabel, keepLabel, hideLabel;
    private bool updatingFields;
    private bool dirty;
    private float saveTimer;
    private List<string> imageFiles = new List<string>();
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    private static string Folder => Path.Combine(Application.persistentDataPath, "Mockups");

    private void Awake()
    {
        instance = this;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        keepWhenClosed = PixelPrefs.GetInt(PrefKeep, 0) != 0;
        PixelUIKit.EnsureEventSystem();
        PixelDevMockWindow.Boot();

        piecesCanvas = PixelUIKit.CreateCanvas("Dev Mockup Pieces", 540, new Vector2(1920f, 1080f), true, false);
        piecesCanvas.transform.SetParent(transform, false);
        piecesCanvasComponent = piecesCanvas.GetComponent<Canvas>();
        GameObject rootGo = new GameObject("Pieces", typeof(RectTransform));
        rootGo.transform.SetParent(piecesCanvas.transform, false);
        piecesRoot = rootGo.GetComponent<RectTransform>();
        PixelUIKit.Stretch(piecesRoot);

        BuildPanel();
        Load();
        ApplyVisibility();
        RefreshImageList();
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; Active = false; }
    }

    private void Apply(bool on)
    {
        Active = on;
        if (!on) Select(null);
        ApplyVisibility();
        RefreshImageList();
        RefreshAll();
    }

    private void ApplyVisibility()
    {
        piecesCanvas.SetActive(!hideAll && (Active || windowOpen || keepWhenClosed));
        foreach (Piece p in pieces)
        {
            p.group.blocksRaycasts = Active;
            p.badge.SetActive(Active);
        }
    }

    // ------------------------------------------------------------------
    // Pieces
    // ------------------------------------------------------------------

    private static readonly Color DefaultWindow = new Color(0.1f, 0.12f, 0.2f, 0.95f), DefaultBox = new Color(0.16f, 0.2f, 0.3f, 1f),
                                  DefaultButton = new Color(0.25f, 0.5f, 0.8f, 1f), DefaultText = Color.white;

    private static Color ParseColour(string hex, Color fallback)
    {
        if (!string.IsNullOrEmpty(hex))
        {
            string h = hex.StartsWith("#") ? hex : "#" + hex;
            if (ColorUtility.TryParseHtmlString(h, out Color c)) return c;
        }
        return fallback;
    }

    private Color ColourOf(PieceData d)
    {
        switch ((PieceKind)d.kind)
        {
            case PieceKind.Window: return ParseColour(d.colour, DefaultWindow);
            case PieceKind.Box: return ParseColour(d.colour, DefaultBox);
            case PieceKind.Button: return ParseColour(d.colour, DefaultButton);
            case PieceKind.Text: return ParseColour(d.colour, DefaultText);
            default: return ParseColour(d.colour, Color.white);
        }
    }

    private Piece CreatePiece(PieceData d)
    {
        PieceKind kind = (PieceKind)Mathf.Clamp(d.kind, 0, 4);
        GameObject go = new GameObject("Piece " + d.id, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(piecesRoot, false);
        Piece p = new Piece { data = d, rt = go.GetComponent<RectTransform>(), group = go.GetComponent<CanvasGroup>() };

        if (kind == PieceKind.Image)
        {
            p.raw = go.AddComponent<RawImage>();
        }
        else
        {
            p.bg = go.AddComponent<Image>();
        }
        TextAlignmentOptions align = kind == PieceKind.Window ? TextAlignmentOptions.Top : kind == PieceKind.Text ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
        if (kind != PieceKind.Image)
        {
            p.label = PixelUIKit.CreateText(font, go.transform, "Label", d.text, d.fontSize, align, kind == PieceKind.Text ? FontStyles.Normal : FontStyles.Bold, Color.white);
            PixelUIKit.Stretch(p.label.rectTransform);
            p.label.rectTransform.offsetMin = new Vector2(10f, 10f);
            p.label.rectTransform.offsetMax = new Vector2(-10f, -10f);
        }

        PixelMockupPiece handler = go.AddComponent<PixelMockupPiece>();
        handler.owner = this;
        handler.id = d.id;

        // Resize handle (bottom-right), only visible on the selected piece.
        GameObject h = new GameObject("Resize", typeof(RectTransform), typeof(Image));
        h.transform.SetParent(go.transform, false);
        Image hi = h.GetComponent<Image>();
        hi.sprite = PixelUIKit.GripSprite();
        hi.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        RectTransform hr = h.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 0f);
        hr.sizeDelta = new Vector2(34f, 34f);
        hr.anchoredPosition = Vector2.zero;
        PixelMockupResizer resizer = h.AddComponent<PixelMockupResizer>();
        resizer.owner = this;
        resizer.id = d.id;
        p.handle = h;
        h.SetActive(false);

        // Number badge (top-left), so screenshots can refer to "#3".
        GameObject b = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        b.transform.SetParent(go.transform, false);
        b.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        b.GetComponent<Image>().raycastTarget = false;
        RectTransform br = b.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
        br.sizeDelta = new Vector2(48f, 30f);
        br.anchoredPosition = Vector2.zero;
        p.badgeText = PixelUIKit.CreateText(font, b.transform, "Num", "#" + d.id, 20f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.9f, 0.2f, 1f));
        PixelUIKit.Stretch(p.badgeText.rectTransform);
        p.badge = b;

        pieces.Add(p);
        ApplyLook(p);
        return p;
    }

    /// <summary>Applies colour, text, transparency, position and size to the object.</summary>
    private void ApplyLook(Piece p)
    {
        PieceData d = p.data;
        PieceKind kind = (PieceKind)Mathf.Clamp(d.kind, 0, 4);
        p.rt.anchorMin = p.rt.anchorMax = p.rt.pivot = new Vector2(0f, 1f);
        p.rt.anchoredPosition = new Vector2(d.x, -d.y);
        p.rt.sizeDelta = new Vector2(Mathf.Max(8f, d.w), Mathf.Max(8f, d.h));
        p.group.alpha = Mathf.Clamp01(d.alpha);
        Color colour = ColourOf(d);

        switch (kind)
        {
            case PieceKind.Window:
                p.bg.sprite = null;
                PixelUIKit.StyleWindow(p.bg, colour);
                break;
            case PieceKind.Box:
                p.bg.sprite = null;
                p.bg.color = colour;
                PixelUIKit.StyleBox(p.bg);
                break;
            case PieceKind.Button:
                p.bg.sprite = null;
                p.bg.color = colour;
                PixelUIKit.StyleButtonLook(p.bg);   // (a mock-up of a button, not a real one: its own colour)
                break;
            case PieceKind.Text:
                p.bg.color = new Color(1f, 1f, 1f, 0f);   // invisible, but still catches the mouse so it can be dragged
                p.label.color = colour;
                break;
            case PieceKind.Image:
                p.raw.texture = TextureFor(d.image);
                p.raw.color = Color.white;
                break;
        }
        if (p.label != null)
        {
            PixelUIKit.SetText(p.label, d.text);
            p.label.fontSize = Mathf.Max(8, d.fontSize);
        }
        PixelUIKit.SetText(p.badgeText, "#" + d.id);
        if (p.outline == null) { p.outline = p.rt.gameObject.AddComponent<Outline>(); p.outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f); p.outline.effectDistance = new Vector2(3f, -3f); }
        p.outline.enabled = p == selected;
    }

    private Texture2D TextureFor(string file)
    {
        if (!string.IsNullOrEmpty(file))
        {
            if (textureCache.TryGetValue(file, out Texture2D cached) && cached != null) return cached;
            string path = Path.Combine(Folder, file);
            try
            {
                if (File.Exists(path))
                {
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(File.ReadAllBytes(path)))
                    {
                        tex.name = file;
                        textureCache[file] = tex;
                        return tex;
                    }
                }
            }
            catch (Exception) { /* fall through to the placeholder */ }
        }
        return Placeholder();
    }

    private static Texture2D placeholder;

    private static Texture2D Placeholder()
    {
        if (placeholder != null) return placeholder;
        const int n = 64;
        placeholder = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                placeholder.SetPixel(x, y, ((x / 8 + y / 8) % 2 == 0) ? new Color(0.55f, 0.58f, 0.65f, 1f) : new Color(0.35f, 0.38f, 0.45f, 1f));
        placeholder.Apply();
        return placeholder;
    }

    private Piece Find(int id)
    {
        foreach (Piece p in pieces) if (p.data.id == id) return p;
        return null;
    }

    private void AddPiece(PieceKind kind)
    {
        PieceData d = new PieceData { id = blob.nextId++, kind = (int)kind };
        switch (kind)
        {
            case PieceKind.Window: d.w = 600f; d.h = 420f; d.text = "Window"; d.fontSize = 34; break;
            case PieceKind.Box: d.w = 360f; d.h = 120f; d.text = ""; break;
            case PieceKind.Button: d.w = 220f; d.h = 70f; d.text = "Button"; d.fontSize = 30; break;
            case PieceKind.Text: d.w = 360f; d.h = 60f; d.text = "Text"; d.fontSize = 32; break;
            case PieceKind.Image: d.w = 300f; d.h = 200f; d.image = imageDropdown != null && imageDropdown.value > 0 && imageDropdown.value <= imageFiles.Count ? imageFiles[imageDropdown.value - 1] : ""; break;
        }
        d.x = 300f + (pieces.Count % 6) * 40f;
        d.y = 200f + (pieces.Count % 6) * 40f;
        blob.pieces.Add(d);
        Piece p = CreatePiece(d);
        Select(p);
        dirty = true;
        ApplyVisibility();
        RefreshAll();
    }

    private void Select(Piece p)
    {
        selected = p;
        foreach (Piece q in pieces)
        {
            bool on = q == p && Active;
            q.handle.SetActive(on);
            if (q.outline != null) q.outline.enabled = on;
        }
        RefreshFields();
        RefreshReadout();
    }

    private void DeleteSelected()
    {
        if (selected == null) return;
        blob.pieces.Remove(selected.data);
        pieces.Remove(selected);
        Destroy(selected.rt.gameObject);
        selected = null;
        dirty = true;
        Select(null);
    }

    private void Duplicate()
    {
        if (selected == null) return;
        PieceData d = JsonUtility.FromJson<PieceData>(JsonUtility.ToJson(selected.data));
        d.id = blob.nextId++;
        d.x += 30f;
        d.y += 30f;
        blob.pieces.Add(d);
        Select(CreatePiece(d));
        ReorderToList();
        dirty = true;
        ApplyVisibility();
    }

    private void ResetAll()
    {
        foreach (Piece p in pieces) if (p.rt != null) Destroy(p.rt.gameObject);
        pieces.Clear();
        blob = new SaveBlob();
        selected = null;
        dirty = true;
        SaveNow();
        Select(null);
    }

    /// <summary>Moves the selected piece in the stack: toFront, toBack, or one step.</summary>
    private void Layer(int mode)   // 0 front, 1 back, 2 up, 3 down
    {
        if (selected == null) return;
        int i = blob.pieces.IndexOf(selected.data);
        if (i < 0) return;
        blob.pieces.RemoveAt(i);
        int target = mode == 0 ? blob.pieces.Count : mode == 1 ? 0 : mode == 2 ? Mathf.Min(blob.pieces.Count, i + 1) : Mathf.Max(0, i - 1);
        blob.pieces.Insert(target, selected.data);
        ReorderToList();
        dirty = true;
        RefreshReadout();
    }

    private void ReorderToList()
    {
        pieces.Sort((a, b) => blob.pieces.IndexOf(a.data).CompareTo(blob.pieces.IndexOf(b.data)));
        for (int i = 0; i < pieces.Count; i++) pieces[i].rt.SetSiblingIndex(i);
    }

    // ------------------------------------------------------------------
    // Called by the piece handlers
    // ------------------------------------------------------------------

    internal void PieceClicked(int id) { if (Active) Select(Find(id)); }

    internal void PieceDeleted(int id)
    {
        if (!Active) return;
        Piece p = Find(id);
        if (p == null) return;
        Select(p);
        DeleteSelected();
    }

    internal void PieceMoved(int id, Vector2 canvasDelta)
    {
        Piece p = Find(id);
        if (p == null || !Active) return;
        p.data.x += canvasDelta.x;
        p.data.y -= canvasDelta.y;
        ApplyLook(p);
        dirty = true;
        if (p == selected) RefreshFields();
    }

    internal void PieceResized(int id, Vector2 canvasDelta)
    {
        Piece p = Find(id);
        if (p == null || !Active) return;
        p.data.w = Mathf.Max(16f, p.data.w + canvasDelta.x);
        p.data.h = Mathf.Max(16f, p.data.h - canvasDelta.y);
        ApplyLook(p);
        dirty = true;
        if (p == selected) RefreshFields();
    }

    internal float CanvasScale => piecesCanvasComponent != null ? Mathf.Max(0.01f, piecesCanvasComponent.scaleFactor) : 1f;

    // ------------------------------------------------------------------
    // Save / load
    // ------------------------------------------------------------------

    private void Load()
    {
        string json = PixelPrefs.GetString(PrefData, "");
        if (string.IsNullOrEmpty(json)) return;
        try { blob = JsonUtility.FromJson<SaveBlob>(json) ?? new SaveBlob(); }
        catch (Exception) { blob = new SaveBlob(); }
        if (blob.pieces == null) blob.pieces = new List<PieceData>();
        foreach (PieceData d in blob.pieces) { blob.nextId = Mathf.Max(blob.nextId, d.id + 1); CreatePiece(d); }
        ReorderToList();
    }

    private void SaveNow()
    {
        dirty = false;
        PixelPrefs.SetString(PrefData, JsonUtility.ToJson(blob));
        PixelPrefs.Save();
    }

    private void Update()
    {
        saveTimer -= Time.unscaledDeltaTime;
        if (dirty && saveTimer <= 0f && !PixelInput.LeftHeld()) { SaveNow(); saveTimer = 0.5f; RefreshReadout(); }
    }

    // ------------------------------------------------------------------
    // Panel
    // ------------------------------------------------------------------

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private Button MakeButton(Transform parent, string label, float x, float y, float w, Color colour, System.Action onClick, float size = 22f)
    {
        Button b = PixelUIKit.CreateButton(font, parent, label, label, new Vector2(w, 42f), colour, Color.white, size);
        PlaceTL(b.GetComponent<RectTransform>(), x, y, w, 42f);
        b.onClick.AddListener(() => onClick());
        return b;
    }

    private TMP_InputField MakeField(Transform parent, string name, float x, float y, float w, TMP_InputField.ContentType type, System.Action<string> onEnd, string placeholder = "")
    {
        TMP_InputField f = PixelUIKit.CreateInputField(font, parent, name, new Vector2(w, 40f), new Color(0.16f, 0.2f, 0.3f, 1f), Color.white, 22f, placeholder);
        PlaceTL(f.GetComponent<RectTransform>(), x, y, w, 40f);
        f.contentType = type;
        f.onEndEdit.AddListener(s => { if (!updatingFields) onEnd(s); });
        return f;
    }

    private void BuildPanel()
    {
        // The controls live in the Mock up mode window's second tab (PixelDevMockWindow), 500 wide like the old panel.
        RectTransform tabRoot = PixelDevMockWindow.TabRoot(1);
        panelCanvas = tabRoot.gameObject;
        Transform t = tabRoot;

        Color text = new Color(0.92f, 0.94f, 1f, 1f), grey = new Color(0.35f, 0.4f, 0.5f, 1f), blue = new Color(0.25f, 0.45f, 0.8f, 1f);
        float y = 8f;

        // Add pieces
        MakeButton(t, "Window", 16f, y, 92f, blue, () => AddPiece(PieceKind.Window), 18f);
        MakeButton(t, "Box", 112f, y, 80f, blue, () => AddPiece(PieceKind.Box), 18f);
        MakeButton(t, "Button", 196f, y, 92f, blue, () => AddPiece(PieceKind.Button), 18f);
        MakeButton(t, "Text", 292f, y, 80f, blue, () => AddPiece(PieceKind.Text), 18f);
        MakeButton(t, "Image", 376f, y, 108f, blue, () => AddPiece(PieceKind.Image), 18f);
        y += 52f;

        // Image picker (files in the Mockups folder)
        imageDropdown = PixelUIKit.CreateDropdown(font, t, "Images", new Vector2(300f, 42f), new Color(0.16f, 0.2f, 0.3f, 1f), new Color(0.16f, 0.2f, 0.3f, 1f), text, 20f);
        PlaceTL(imageDropdown.GetComponent<RectTransform>(), 16f, y, 300f, 42f);
        imageDropdown.onValueChanged.AddListener(v =>
        {
            if (updatingFields || selected == null || selected.data.kind != (int)PieceKind.Image) return;
            selected.data.image = v > 0 && v <= imageFiles.Count ? imageFiles[v - 1] : "";
            ApplyLook(selected);
            dirty = true;
        });
        MakeButton(t, "Refresh", 322f, y, 80f, grey, RefreshImageList, 16f);
        MakeButton(t, "Folder", 406f, y, 78f, grey, () => { Directory.CreateDirectory(Folder); Application.OpenURL("file://" + Folder); }, 16f);
        y += 52f;

        selectedLabel = PixelUIKit.CreateText(font, t, "Selected", "Nothing selected", 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(1f, 0.9f, 0.3f, 1f));
        PlaceTL(selectedLabel.rectTransform, 20f, y, 460f, 32f);
        y += 36f;

        // Text
        textField = MakeField(t, "Text", 16f, y, 468f, TMP_InputField.ContentType.Standard, s => { if (selected != null) { selected.data.text = s; ApplyLook(selected); dirty = true; } }, "Text");
        y += 48f;

        // Position / size
        TMP_Text xl = PixelUIKit.CreateText(font, t, "XL", "X", 18f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, text); PlaceTL(xl.rectTransform, 8f, y, 24f, 40f);
        xField = MakeField(t, "X", 34f, y, 100f, TMP_InputField.ContentType.DecimalNumber, s => SetNumber(0, s));
        TMP_Text yl = PixelUIKit.CreateText(font, t, "YL", "Y", 18f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, text); PlaceTL(yl.rectTransform, 134f, y, 24f, 40f);
        yField = MakeField(t, "Y", 160f, y, 100f, TMP_InputField.ContentType.DecimalNumber, s => SetNumber(1, s));
        TMP_Text wl = PixelUIKit.CreateText(font, t, "WL", "W", 18f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, text); PlaceTL(wl.rectTransform, 260f, y, 28f, 40f);
        wField = MakeField(t, "W", 290f, y, 90f, TMP_InputField.ContentType.DecimalNumber, s => SetNumber(2, s));
        TMP_Text hl = PixelUIKit.CreateText(font, t, "HL", "H", 18f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, text); PlaceTL(hl.rectTransform, 380f, y, 24f, 40f);
        hField = MakeField(t, "H", 406f, y, 78f, TMP_InputField.ContentType.DecimalNumber, s => SetNumber(3, s));
        y += 48f;

        // Transparency, colour, font size
        TMP_Text al = PixelUIKit.CreateText(font, t, "AL", "Opacity", 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, text); PlaceTL(al.rectTransform, 20f, y, 90f, 40f);
        alphaSlider = PixelUIKit.CreateSlider(t, "Opacity", new Color(0.05f, 0.06f, 0.1f, 1f), blue, Color.white);
        PlaceTL(alphaSlider.GetComponent<RectTransform>(), 112f, y + 4f, 372f, 32f);
        alphaSlider.minValue = 0.05f; alphaSlider.maxValue = 1f; alphaSlider.value = 1f;
        alphaSlider.onValueChanged.AddListener(v => { if (!updatingFields && selected != null) { selected.data.alpha = v; ApplyLook(selected); dirty = true; } });
        y += 46f;

        TMP_Text cl = PixelUIKit.CreateText(font, t, "CL", "Colour", 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, text); PlaceTL(cl.rectTransform, 20f, y, 90f, 40f);
        colourField = MakeField(t, "Colour", 112f, y, 140f, TMP_InputField.ContentType.Standard, s => { if (selected != null) { selected.data.colour = s.Trim(); ApplyLook(selected); dirty = true; } }, "#RRGGBB");
        MakeButton(t, "Font -", 262f, y, 108f, grey, () => ChangeFont(-2), 18f);
        MakeButton(t, "Font +", 376f, y, 108f, grey, () => ChangeFont(2), 18f);
        y += 50f;

        // Layers + edit
        MakeButton(t, "Front", 16f, y, 88f, grey, () => Layer(0), 18f);
        MakeButton(t, "Up", 108f, y, 66f, grey, () => Layer(2), 18f);
        MakeButton(t, "Down", 178f, y, 78f, grey, () => Layer(3), 18f);
        MakeButton(t, "Back", 260f, y, 80f, grey, () => Layer(1), 18f);
        MakeButton(t, "Copy", 344f, y, 66f, new Color(0.25f, 0.6f, 0.4f, 1f), Duplicate, 18f);
        MakeButton(t, "Delete", 414f, y, 70f, new Color(0.65f, 0.25f, 0.25f, 1f), DeleteSelected, 16f);
        y += 52f;

        // Global
        Button keep = MakeButton(t, "Keep", 16f, y, 230f, grey, () =>
        {
            keepWhenClosed = !keepWhenClosed;
            PixelPrefs.SetInt(PrefKeep, keepWhenClosed ? 1 : 0);
            ApplyVisibility();
            RefreshAll();
        }, 17f);
        keepLabel = keep.GetComponentInChildren<TMP_Text>();
        Button hide = MakeButton(t, "Hide", 252f, y, 232f, grey, () => { hideAll = !hideAll; ApplyVisibility(); RefreshAll(); }, 17f);
        hideLabel = hide.GetComponentInChildren<TMP_Text>();
        y += 50f;
        MakeButton(t, "Copy layout", 16f, y, 230f, new Color(0.25f, 0.6f, 0.4f, 1f), CopyLayout, 20f);
        MakeButton(t, "RESET ALL", 252f, y, 232f, new Color(0.75f, 0.2f, 0.2f, 1f), ResetAll, 20f);
        y += 52f;

        readout = PixelUIKit.CreateText(font, t, "Readout", "", 17f, TextAlignmentOptions.TopLeft, FontStyles.Normal, text);
        PlaceTL(readout.rectTransform, 20f, y, 462f, PixelDevMockWindow.ContentHeight - y - 10f);
        readout.overflowMode = TextOverflowModes.Truncate;

        RefreshAll();
    }

    private void SetNumber(int which, string s)
    {
        if (selected == null) return;
        if (!float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) { RefreshFields(); return; }
        switch (which)
        {
            case 0: selected.data.x = v; break;
            case 1: selected.data.y = v; break;
            case 2: selected.data.w = Mathf.Max(8f, v); break;
            default: selected.data.h = Mathf.Max(8f, v); break;
        }
        ApplyLook(selected);
        dirty = true;
        RefreshFields();
    }

    private void ChangeFont(int delta)
    {
        if (selected == null) return;
        selected.data.fontSize = Mathf.Clamp(selected.data.fontSize + delta, 8, 200);
        ApplyLook(selected);
        dirty = true;
    }

    private void RefreshImageList()
    {
        imageFiles.Clear();
        try
        {
            Directory.CreateDirectory(Folder);
            foreach (string path in Directory.GetFiles(Folder))
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg") imageFiles.Add(Path.GetFileName(path));
            }
        }
        catch (Exception) { /* no folder access: only the placeholder is offered */ }
        imageFiles.Sort(StringComparer.OrdinalIgnoreCase);
        if (imageDropdown == null) return;
        updatingFields = true;
        imageDropdown.ClearOptions();
        List<string> options = new List<string> { imageFiles.Count == 0 ? "(no images: use Folder)" : "(placeholder)" };
        options.AddRange(imageFiles);
        imageDropdown.AddOptions(options);
        updatingFields = false;
        RefreshFields();
    }

    private void RefreshFields()
    {
        if (xField == null) return;
        updatingFields = true;
        bool has = selected != null;
        PixelUIKit.SetText(selectedLabel, has ? "#" + selected.data.id + "  " + (PieceKind)selected.data.kind : "Nothing selected (click a piece)");
        if (has)
        {
            PieceData d = selected.data;
            if (!textField.isFocused) textField.SetTextWithoutNotify(d.text);
            if (!xField.isFocused) xField.SetTextWithoutNotify(Mathf.Round(d.x).ToString());
            if (!yField.isFocused) yField.SetTextWithoutNotify(Mathf.Round(d.y).ToString());
            if (!wField.isFocused) wField.SetTextWithoutNotify(Mathf.Round(d.w).ToString());
            if (!hField.isFocused) hField.SetTextWithoutNotify(Mathf.Round(d.h).ToString());
            if (!colourField.isFocused) colourField.SetTextWithoutNotify(d.colour);
            alphaSlider.SetValueWithoutNotify(Mathf.Clamp(d.alpha, 0.05f, 1f));
            int index = string.IsNullOrEmpty(d.image) ? 0 : imageFiles.IndexOf(d.image) + 1;
            imageDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            imageDropdown.RefreshShownValue();
        }
        updatingFields = false;
    }

    private void RefreshAll()
    {
        if (keepLabel != null) PixelUIKit.SetText(keepLabel, keepWhenClosed ? "Show when closed: ON" : "Show when closed: OFF");
        if (hideLabel != null) PixelUIKit.SetText(hideLabel, hideAll ? "Pieces: HIDDEN" : "Pieces: shown");
        RefreshFields();
        RefreshReadout();
    }

    private void RefreshReadout()
    {
        if (readout == null) return;
        StringBuilder sb = new StringBuilder();
        sb.Append("Canvas 1920 x 1080, origin top-left. Pieces back to front:\n");
        int max = 14;
        for (int i = 0; i < blob.pieces.Count && i < max; i++) sb.Append(Line(blob.pieces[i], selected != null && selected.data == blob.pieces[i])).Append('\n');
        if (blob.pieces.Count > max) sb.Append("... and ").Append(blob.pieces.Count - max).Append(" more (Copy layout lists them all)");
        PixelUIKit.SetText(readout, sb.ToString());
    }

    private static string Line(PieceData d, bool isSelected)
    {
        string text = string.IsNullOrEmpty(d.text) ? "" : " \"" + (d.text.Length > 18 ? d.text.Substring(0, 18) + "..." : d.text) + "\"";
        return (isSelected ? "> " : "  ") + "#" + d.id + " " + (PieceKind)d.kind + text + "  x " + Mathf.Round(d.x) + " y " + Mathf.Round(d.y) + "  " + Mathf.Round(d.w) + "x" + Mathf.Round(d.h)
             + (d.alpha < 0.995f ? "  op " + d.alpha.ToString("0.00") : "") + (string.IsNullOrEmpty(d.colour) ? "" : "  " + d.colour) + (string.IsNullOrEmpty(d.image) ? "" : "  img " + d.image);
    }

    private void CopyLayout()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("UI mockup (canvas 1920 x 1080, origin top-left, units = canvas units; listed back to front, so later = on top)\n");
        foreach (PieceData d in blob.pieces) sb.Append(Line(d, false).Substring(2)).Append("  font ").Append(d.fontSize).Append('\n');
        GUIUtility.systemCopyBuffer = sb.ToString();
        PixelHints.Announce("Mockup layout copied", "The UI mockup layout (" + blob.pieces.Count + " pieces) is on your clipboard.");
    }
}

/// <summary>Click-to-select and drag-to-move for one mockup piece.</summary>
public class PixelMockupPiece : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    internal PixelDevMockup owner;
    internal int id;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner == null) return;
        if (eventData.button == PointerEventData.InputButton.Right) owner.PieceDeleted(id);   // right-click removes the piece
        else owner.PieceClicked(id);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.PieceMoved(id, eventData.delta / owner.CanvasScale);
    }
}

/// <summary>Drag the corner handle to resize a mockup piece.</summary>
public class PixelMockupResizer : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    internal PixelDevMockup owner;
    internal int id;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner != null) owner.PieceResized(id, eventData.delta / owner.CanvasScale);
    }
}
