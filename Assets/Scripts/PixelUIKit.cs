using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small shared helpers for the code-built UI (canvas, event system, text, buttons), so every UI script
/// builds things the same way instead of carrying its own copy.
/// </summary>
public static class PixelUIKit
{
    /// <summary>Makes sure the scene has an EventSystem (with the right input module for the active input system).</summary>
    public static void EnsureEventSystem()
    {
        if (PixelFind.First<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    /// <summary>A screen-space overlay canvas that scales with the screen. 'clickable' adds a GraphicRaycaster.</summary>
    public static GameObject CreateCanvas(string objectName, int sortingOrder, Vector2 referenceResolution, bool clickable, bool scalable = true)
    {
        GameObject root = new GameObject(objectName);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // The player's UI scale (Settings): a smaller reference resolution makes everything bigger. The black bars opt out.
        scaler.referenceResolution = scalable ? referenceResolution / PixelDisplaySettings.UIScale : referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        if (clickable) root.AddComponent<GraphicRaycaster>();
        return root;
    }

    /// <summary>
    /// Places a HOVER TOOLTIP: at the BOTTOM LEFT of the pointer (the lower right when there is no room on the left, above the pointer when there is no room below). 'tip' must be centre-anchored with its
    /// sizeDelta set; 'pointerLocal' = the pointer in the canvas rect's local space. Every hover tooltip uses this, so they all sit in the same place.
    /// </summary>
    public static void PlaceTooltip(RectTransform tip, RectTransform canvasRect, Vector2 pointerLocal, float gap = 18f)
    {
        if (tip == null || canvasRect == null) return;
        Vector2 size = tip.sizeDelta;
        Rect r = canvasRect.rect;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        const float edge = 8f;
        bool left = pointerLocal.x - gap - size.x >= r.xMin + edge;
        bool down = pointerLocal.y - gap - size.y >= r.yMin + bar + edge;
        tip.pivot = new Vector2(left ? 1f : 0f, down ? 1f : 0f);
        Vector2 pos = pointerLocal + new Vector2(left ? -gap : gap, down ? -gap : gap);
        pos.x = Mathf.Clamp(pos.x, r.xMin + edge + (left ? size.x : 0f), Mathf.Max(r.xMin + edge + (left ? size.x : 0f), r.xMax - edge - (left ? 0f : size.x)));
        float lo = r.yMin + bar + edge + (down ? size.y : 0f), hi = r.yMax - bar - edge - (down ? 0f : size.y);
        pos.y = Mathf.Clamp(pos.y, lo, Mathf.Max(lo, hi));
        tip.anchoredPosition = pos;
    }

    /// <summary>The height of the camera's view (world units) at 'depth' in front of it - works for the normal and the flat (orthographic) view.</summary>
    public static float ViewHeightAt(Camera cam, float depth)
    {
        return cam.orthographic ? 2f * cam.orthographicSize : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    /// <summary>
    /// Moves a 3D text so all of it stays inside the camera's view (its whole width and height, with a margin as a fraction of
    /// the screen), clear of the black bars. Returns the adjusted world position.
    /// </summary>
    public static Vector3 KeepOnScreen(Camera cam, TextMeshPro text, Vector3 position, float margin = 0.03f)
    {
        if (cam == null || text == null) return position;
        Vector3 v = cam.WorldToViewportPoint(position);
        if (v.z <= 0.01f) return position;

        float viewHeight = ViewHeightAt(cam, v.z);
        float viewWidth = viewHeight * cam.aspect;
        text.ForceMeshUpdate();
        float halfW = Mathf.Min(0.45f, text.preferredWidth * 0.5f * text.transform.lossyScale.x / Mathf.Max(0.01f, viewWidth));
        float halfH = Mathf.Min(0.45f, text.preferredHeight * 0.5f * text.transform.lossyScale.y / Mathf.Max(0.01f, viewHeight));

        float bars = PixelHud.Instance != null
            ? PixelHud.Instance.RawBarHeight * Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f) / Mathf.Max(1, Screen.height) : 0f;
        float x = Mathf.Clamp(v.x, margin + halfW, 1f - margin - halfW);
        float y = Mathf.Clamp(v.y, bars + margin + halfH, 1f - bars - margin - halfH);
        return cam.ViewportToWorldPoint(new Vector3(x, y, v.z));
    }

    /// <summary>Sets a text only when it changed (skips the TMP rebuild for identical strings).</summary>
    public static void SetText(TMP_Text label, string text)
    {
        if (label != null && label.text != text) label.text = text;
    }

    /// <summary>A TextMeshPro UI text that doesn't catch clicks. A null font = the TMP default.</summary>
    public static TMP_Text CreateText(TMP_FontAsset font, Transform parent, string objectName, string text, float size,
                                      TextAlignmentOptions alignment, FontStyles style, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    /// <summary>A coloured button with a centred bold label that stretches over it.</summary>
    public static Button CreateButton(TMP_FontAsset font, Transform parent, string objectName, string label, Vector2 size,
                                      Color color, Color labelColor, float labelSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        StyleButton(image);
        go.GetComponent<Button>().targetGraphic = image;
        go.GetComponent<Button>().onClick.AddListener(() => PixelAudio.Play("ui_click"));

        TMP_Text text = CreateText(font, go.transform, "Label", label, labelSize, TextAlignmentOptions.Center,
                                   FontStyles.Bold, labelColor);
        Stretch(text.rectTransform);
        if (label == "X") UseCloseGlyph(go, text, labelColor, size); // close buttons draw a cross instead of the letter
        return go.GetComponent<Button>();
    }

    private static Sprite buttonSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetButtonStatics() { buttonSprite = null; boxSprite = null; insetSprite = null; }

    /// <summary>
    /// A grey-scale 9-sliced button sprite (drawn once in code; it takes the button's colour as a tint): rounded corners, a dark outline,
    /// a vertical light-to-dark gradient and a bright strip just inside the top edge, so a button reads as a raised key.
    /// </summary>
    private const string PrefUiColor = "PixelClicker.Setting.UiColor", PrefUiHue = "PixelClicker.Setting.UiHue", PrefUiSat = "PixelClicker.Setting.UiSat";
    private static int uiColorCache = -1;
    private static float uiHueCache = -1f, uiSatCache = -1f;
    private static readonly System.Collections.Generic.Dictionary<Color32, Sprite> windowSprites = new System.Collections.Generic.Dictionary<Color32, Sprite>();
    private struct WindowEntry { public Image image; public Color color; }
    private static readonly System.Collections.Generic.List<WindowEntry> windowImages = new System.Collections.Generic.List<WindowEntry>();

    /// <summary>Use the player's own colour for the windows (and their glowing rim) instead of the neutral dark grey. Off by default. Applies to open windows at once.</summary>
    public static bool UiColorOn
    {
        get
        {
            if (uiColorCache < 0) uiColorCache = PixelPrefs.GetInt(PrefUiColor, 0);
            return uiColorCache != 0;
        }
        set
        {
            uiColorCache = value ? 1 : 0;
            PixelPrefs.SetInt(PrefUiColor, uiColorCache);
            RefreshWindows();
        }
    }

    /// <summary>The hue (0 - 1) of the window colour while UI colour is on.</summary>
    public static float UiHue
    {
        get
        {
            if (uiHueCache < 0f) uiHueCache = Mathf.Clamp01(PixelPrefs.GetFloat(PrefUiHue, 0.6f));
            return uiHueCache;
        }
        set
        {
            uiHueCache = Mathf.Round(Mathf.Clamp01(value) * 48f) / 48f;   // in steps, so dragging the slider doesn't build a new frame picture for every pixel of movement
            PixelPrefs.SetFloat(PrefUiHue, uiHueCache);
            if (UiColorOn) RefreshWindows();
        }
    }

    /// <summary>How colourful the windows are while UI colour is on (0.2 - 1; the eyedropper sets it from the picked colour).</summary>
    public static float UiSat
    {
        get
        {
            if (uiSatCache < 0f) uiSatCache = Mathf.Clamp(PixelPrefs.GetFloat(PrefUiSat, 0.55f), 0.2f, 1f);
            return uiSatCache;
        }
    }

    /// <summary>Makes the windows take on the colour of 'c' (its hue and how colourful it is) and switches the custom UI colour on. Used by the eyedropper in Settings.</summary>
    public static void SetUiColorFromColor(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out _);
        uiHueCache = Mathf.Round(Mathf.Clamp01(h) * 96f) / 96f;
        uiSatCache = Mathf.Clamp(Mathf.Round(s * 24f) / 24f, 0.2f, 1f);
        uiColorCache = 1;
        PixelPrefs.SetFloat(PrefUiHue, uiHueCache);
        PixelPrefs.SetFloat(PrefUiSat, uiSatCache);
        PixelPrefs.SetInt(PrefUiColor, 1);
        PixelPrefs.Save();
        RefreshWindows();
    }

    public static void ReloadColorFromPrefs()
    {
        uiColorCache = -1; uiHueCache = -1f; uiSatCache = -1f;
        RefreshWindows();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWindowStatics() { buttonImages.Clear(); buttonColorCache = -1; buttonHueCache = -1f; buttonSatCache = -1f; uiColorCache = -1; uiHueCache = -1f; uiSatCache = -1f; windowSprites.Clear(); windowImages.Clear(); }

    /// <summary>A window colour with the player's UI hue applied (dark colours stay dark but pick up a clear tint).</summary>
    public static Color Tinted(Color c)
    {
        if (!UiColorOn) return c;
        Color.RGBToHSV(c, out _, out float s, out float v);
        Color o = Color.HSVToRGB(UiHue, Mathf.Max(s, UiSat), Mathf.Min(1f, v * 1.5f + 0.05f));
        o.a = c.a;
        return o;
    }

    /// <summary>
    /// Gives a window's background image its colour: a rounded 9-slice frame (soft vertical gradient, a bright
    /// rim and a little glow bleeding inward, the colour baked into the sprite) - otherwise a flat colour as before. Remembered, so changing
    /// the UI colour in Settings recolours every open window at once.
    /// </summary>
    public static void StyleWindow(Image image, Color color)
    {
        if (image == null) return;
        windowImages.Add(new WindowEntry { image = image, color = color });
        ApplyWindow(image, color);
    }

    /// <summary>The body colour of the nearest window above 'from' (the one built with <see cref="StyleWindow"/>), or a dark navy if there is none. Used by the scroll fade.</summary>
    public static Color WindowBodyColorAbove(Transform from)
    {
        for (Transform t = from; t != null; t = t.parent)
        {
            Image img = t.GetComponent<Image>();
            if (img == null) continue;
            for (int i = 0; i < windowImages.Count; i++)
                if (windowImages[i].image == img) { Color c = Tinted(windowImages[i].color); c.a = 1f; return c; }
        }
        return new Color(0.08f, 0.1f, 0.16f, 1f);
    }

    private static void ApplyWindow(Image image, Color color)
    {
        Color body = Tinted(color);
        Color32 key = body;
        if (!windowSprites.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = BuildWindowSprite(body);
            windowSprites[key] = sprite;
        }
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(1f, 1f, 1f, body.a);
    }

    /// <summary>Re-applies the window colour to every window that has been built (after the UI colour changed).</summary>
    public static void RefreshWindows()
    {
        System.Collections.Generic.List<Sprite> old = new System.Collections.Generic.List<Sprite>(windowSprites.Values);
        windowSprites.Clear();
        for (int i = windowImages.Count - 1; i >= 0; i--)
        {
            if (windowImages[i].image == null) { windowImages.RemoveAt(i); continue; }
            ApplyWindow(windowImages[i].image, windowImages[i].color);
        }
        foreach (Sprite sp in old) if (sp != null) { if (sp.texture != null) Object.Destroy(sp.texture); Object.Destroy(sp); }
    }

    private static Sprite BuildWindowSprite(Color body)
    {
        const int n = 64;
        const float r = 18f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Window Frame", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        Color rim = UiColorOn ? Color.HSVToRGB(UiHue, 0.55f, 1f) : new Color(0.45f, 0.85f, 1f, 1f);
        Color edge = Color.Lerp(body, rim, 0.8f);   // the glowing rim
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x - c) - (c - r), 0f), qy = Mathf.Max(Mathf.Abs(y - c) - (c - r), 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) - r;
                float depth = -d;
                float t = y / (float)(n - 1);
                Color col = Color.Lerp(body * 0.8f, body * 1.25f, t);                    // lighter towards the top
                col.a = 1f;
                if (depth < 2f) col = Color.Lerp(edge, col, Mathf.Clamp01((depth - 1.2f) / 0.8f));                  // the bright rim
                else if (depth < 4.5f) col = Color.Lerp(col, edge, 0.35f * (1f - (depth - 2f) / 2.5f));            // its glow bleeding inward
                col.a = Mathf.Clamp01(0.5f - d);
                px[y * n + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(26, 26, 26, 26));
    }

    private static Sprite boxSprite, insetSprite;

    /// <summary>
    /// Gives a list row / card background a softly rounded, gently shaded look with a thin lighter rim (a raised box). With 'inset' the
    /// shading is reversed (darker at the top, lighter rim at the bottom): a sunken frame, used round the 3D pictures. 
    /// </summary>
    public static void StyleBox(Image image, bool inset = false)
    {
        if (image == null) return;
        image.sprite = inset ? InsetSprite() : BoxSprite();
        image.type = Image.Type.Sliced;
    }

    private static Sprite BoxSprite() { if (boxSprite == null) boxSprite = BuildBoxSprite(false); return boxSprite; }
    private static Sprite InsetSprite() { if (insetSprite == null) insetSprite = BuildBoxSprite(true); return insetSprite; }

    private static Sprite BuildBoxSprite(bool inset)
    {
        const int n = 32;
        const float r = 8f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = inset ? "Inset Box" : "Box", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x - c) - (c - r), 0f), qy = Mathf.Max(Mathf.Abs(y - c) - (c - r), 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) - r;
                float depth = -d;
                float t = y / (float)(n - 1);                       // 0 bottom, 1 top
                float shade = inset ? Mathf.Lerp(1.1f, 0.78f, t) : Mathf.Lerp(0.8f, 1.08f, t);
                if (depth < 1.6f) shade = inset ? Mathf.Lerp(1.5f, 0.5f, t) : Mathf.Lerp(0.55f, 1.55f, t);   // a thin rim: light on the lit side
                else if (depth < 3.2f) shade *= inset ? 0.9f : 1f;
                float a = Mathf.Clamp01(0.5f - d);
                px[y * n + x] = new Color(shade, shade, shade, a);
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
    }

    /// <summary>Gives a button's background image the bevelled look (also used by the shop's own button builder).</summary>
    public static void StyleButton(Image image)
    {
        if (image == null) return;
        image.sprite = ButtonSprite();
        image.type = Image.Type.Sliced;
        Color baseColor = image.color;
        Color applied = ButtonTinted(baseColor);
        if (ButtonColorOn) image.color = applied;
        buttonImages.Add(new ButtonEntry { image = image, baseColor = baseColor, applied = applied });
        if (buttonImages.Count > 600) buttonImages.RemoveAll(e => e.image == null);
    }

    // ---- the player's own button colour (Settings > 'Custom UI button colour') ----

    private const string PrefButtonColor = "PixelClicker.Setting.UiButtonColor", PrefButtonHue = "PixelClicker.Setting.UiButtonHue", PrefButtonSat = "PixelClicker.Setting.UiButtonSat";
    private static int buttonColorCache = -1;
    private static float buttonHueCache = -1f, buttonSatCache = -1f;
    private struct ButtonEntry { public Image image; public Color baseColor, applied; }
    private static readonly System.Collections.Generic.List<ButtonEntry> buttonImages = new System.Collections.Generic.List<ButtonEntry>();

    /// <summary>Use the player's own colour for the (neutral) buttons. Off by default. Coloured buttons (red Quit, green confirm...) keep their meaning colour. Applies to every button at once.</summary>
    public static bool ButtonColorOn
    {
        get { if (buttonColorCache < 0) buttonColorCache = PixelPrefs.GetInt(PrefButtonColor, 0); return buttonColorCache != 0; }
        set { buttonColorCache = value ? 1 : 0; PixelPrefs.SetInt(PrefButtonColor, buttonColorCache); RefreshButtons(); }
    }

    private static float ButtonHue { get { if (buttonHueCache < 0f) buttonHueCache = Mathf.Clamp01(PixelPrefs.GetFloat(PrefButtonHue, 0.6f)); return buttonHueCache; } }
    private static float ButtonSat { get { if (buttonSatCache < 0f) buttonSatCache = Mathf.Clamp(PixelPrefs.GetFloat(PrefButtonSat, 0.55f), 0.2f, 1f); return buttonSatCache; } }

    /// <summary>Makes the buttons take on the colour of 'c' (hue and how colourful it is) and switches the custom button colour on. Used by the eyedropper in Settings.</summary>
    public static void SetButtonColorFromColor(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out _);
        buttonHueCache = Mathf.Clamp01(h);
        buttonSatCache = Mathf.Clamp(s, 0.2f, 1f);
        buttonColorCache = 1;
        PixelPrefs.SetFloat(PrefButtonHue, buttonHueCache);
        PixelPrefs.SetFloat(PrefButtonSat, buttonSatCache);
        PixelPrefs.SetInt(PrefButtonColor, 1);
        PixelPrefs.Save();
        RefreshButtons();
    }

    public static void ReloadButtonColorFromPrefs() { buttonColorCache = -1; buttonHueCache = -1f; buttonSatCache = -1f; RefreshButtons(); }

    /// <summary>A button colour with the player's hue applied; strongly coloured buttons are left alone.</summary>
    private static Color ButtonTinted(Color c)
    {
        if (!ButtonColorOn) return c;
        Color.RGBToHSV(c, out _, out float s, out float v);
        if (s > 0.45f) return c;
        Color o = Color.HSVToRGB(ButtonHue, Mathf.Max(s, ButtonSat), Mathf.Min(1f, v * 1.5f + 0.05f));
        o.a = c.a;
        return o;
    }

    private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

    /// <summary>Recolours every button that has been built (after the setting changed). A button whose colour something else changed meanwhile (a selected row) is left alone.</summary>
    public static void RefreshButtons()
    {
        for (int i = buttonImages.Count - 1; i >= 0; i--)
        {
            ButtonEntry e = buttonImages[i];
            if (e.image == null) { buttonImages.RemoveAt(i); continue; }
            Color target = ButtonTinted(e.baseColor);
            if (Near(e.image.color, e.applied)) { Color c = target; c.a = e.image.color.a; e.image.color = c; }
            e.applied = target;
            buttonImages[i] = e;
        }
    }

    private static Sprite ButtonSprite()
    {
        if (buttonSprite != null) return buttonSprite;
        const int n = 48;
        const float r = 10f;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Button", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x - c) - (c - r), 0f), qy = Mathf.Max(Mathf.Abs(y - c) - (c - r), 0f);
                float d = Mathf.Sqrt(qx * qx + qy * qy) - r;                   // < 0 inside the rounded rectangle
                float alpha = Mathf.Clamp01(0.5f - d);
                float depth = -d;                                              // distance in from the edge
                float t = y / (float)(n - 1);                                  // 0 bottom, 1 top
                float shade = Mathf.Lerp(0.7f, 1f, t);
                if (depth < 2.2f) shade = Mathf.Lerp(0.42f, shade, Mathf.Clamp01(depth - 1f));          // the dark outline
                else if (depth < 5f && t > 0.5f) shade = Mathf.Min(1.25f, shade + 0.22f * (1f - (depth - 2.2f) / 2.8f));   // the light strip under the top edge
                else if (depth < 4f && t < 0.35f) shade *= 0.88f;                                       // a slightly darker lip at the bottom
                px[y * n + x] = new Color(Mathf.Clamp01(shade), Mathf.Clamp01(shade), Mathf.Clamp01(shade), alpha);
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        buttonSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        return buttonSprite;
    }

    /// <summary>Shows a label in capitals (a style, so the text itself and later changes stay as written).</summary>
    public static void Caps(TMP_Text label)
    {
        if (label != null) label.fontStyle |= FontStyles.UpperCase;
    }

    /// <summary>Shows a button's label in capitals.</summary>
    public static void Caps(Button button)
    {
        if (button != null) Caps(button.GetComponentInChildren<TMP_Text>(true));
    }

    private static Sprite closeCross;

    /// <summary>A white, smooth-edged cross (drawn once in code) for close buttons.</summary>
    private static Sprite CloseCrossSprite()
    {
        if (closeCross != null) return closeCross;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Close Cross", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        const float half = 0.075f; // half thickness of each bar (in 0..1 units, measured across the bar)
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cover = 0f;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 4f) / n, v = (y + (sy + 0.5f) / 4f) / n;
                        bool inside = u >= 0.08f && u <= 0.92f && v >= 0.08f && v <= 0.92f;
                        float d1 = Mathf.Abs(u - v) * 0.70711f, d2 = Mathf.Abs(u + v - 1f) * 0.70711f; // distance to the two diagonals
                        if (inside && (d1 <= half || d2 <= half)) cover += 1f / 16f;
                    }
                px[y * n + x] = new Color(1f, 1f, 1f, cover);
            }
        tex.SetPixels(px);
        tex.Apply();
        closeCross = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        closeCross.name = "Close Cross";
        return closeCross;
    }

    private static Sprite eyedropper;

    /// <summary>
    /// A white eyedropper (pipette) drawn once in code: a thin glass tip at the bottom left, a see-through tube with a little liquid in it, a collar and a
    /// rubber squeeze bulb at the top right. Edges are smooth; the glass is semi-transparent so it reads as glass.
    /// </summary>
    public static Sprite EyedropperSprite()
    {
        if (eyedropper != null) return eyedropper;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Eyedropper", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        Vector2 a = new Vector2(0.1f, 0.1f), b = new Vector2(0.9f, 0.9f);
        Vector2 axis = (b - a).normalized, perp = new Vector2(-axis.y, axis.x);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float alpha = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        Vector2 q = new Vector2((x + (sx + 0.5f) / 3f) / n, (y + (sy + 0.5f) / 3f) / n) - a;
                        float along = Vector2.Dot(q, axis), across = Mathf.Abs(Vector2.Dot(q, perp));
                        float v = 0f;
                        if (along >= 0f && along < 0.22f) v = across <= Mathf.Lerp(0.012f, 0.05f, along / 0.22f) ? 1f : 0f;              // the thin tip
                        else if (along >= 0.22f && along < 0.66f)                                                                      // the glass tube
                        {
                            if (across <= 0.058f) v = across > 0.042f ? 1f : (along < 0.4f ? 0.95f : 0.28f);                           // wall, liquid, empty glass
                        }
                        else if (along >= 0.66f && along < 0.73f) v = across <= 0.09f ? 1f : 0f;                                      // the collar
                        else if (along >= 0.73f)                                                                                      // the squeeze bulb
                        {
                            float ex = (along - 0.93f) / 0.19f, ey = across / 0.125f;
                            if (ex * ex + ey * ey <= 1f) v = 1f;
                            float hx = (along - 0.9f) / 0.05f, hy = (Vector2.Dot(q, perp) + 0.05f) / 0.035f;
                            if (v > 0f && hx * hx + hy * hy <= 1f) v = 0.62f;                                                          // a highlight on the bulb
                        }
                        alpha += v / 9f;
                    }
                px[y * n + x] = new Color(1f, 1f, 1f, alpha);
            }
        tex.SetPixels(px);
        tex.Apply();
        eyedropper = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        eyedropper.name = "Eyedropper";
        return eyedropper;
    }

    private static Sprite magnifier;

    /// <summary>A white magnifying glass (a ring with a handle), drawn once in code, for search boxes.</summary>
    public static Sprite MagnifierSprite()
    {
        if (magnifier != null) return magnifier;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Magnifier", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        Vector2 centre = new Vector2(0.4f, 0.6f);
        const float ringRadius = 0.27f, ringHalf = 0.065f;
        Vector2 handleA = centre + new Vector2(0.19f, -0.19f), handleB = new Vector2(0.88f, 0.12f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cover = 0f;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / 3f) / n, (y + (sy + 0.5f) / 3f) / n);
                        bool ring = Mathf.Abs(Vector2.Distance(p, centre) - ringRadius) <= ringHalf;
                        bool handle = SegDist(p, handleA, handleB) <= 0.075f;
                        if (ring || handle) cover += 1f / 9f;
                    }
                px[y * n + x] = new Color(1f, 1f, 1f, cover);
            }
        tex.SetPixels(px);
        tex.Apply();
        magnifier = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        magnifier.name = "Magnifier";
        return magnifier;
    }

    private static Sprite tickMark;

    /// <summary>A white, smooth-edged check mark with rounded ends (drawn once in code) for the fill of tick boxes.</summary>
    public static Sprite TickSprite()
    {
        if (tickMark != null) return tickMark;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Tick Mark", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        Vector2 a = new Vector2(0.14f, 0.52f), b = new Vector2(0.40f, 0.24f), c = new Vector2(0.86f, 0.78f);
        const float half = 0.085f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cover = 0f;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / 4f) / n, (y + (sy + 0.5f) / 4f) / n);
                        if (Mathf.Min(SegDist(p, a, b), SegDist(p, b, c)) <= half) cover += 1f / 16f;
                    }
                px[y * n + x] = new Color(1f, 1f, 1f, cover);
            }
        tex.SetPixels(px);
        tex.Apply();
        tickMark = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        tickMark.name = "Tick Mark";
        return tickMark;
    }

    private static Sprite gripSprite;

    /// <summary>
    /// Fills a drop-down with 'labels' in ALPHABETICAL order (case-insensitive) and returns the map option index -> index in 'labels'. The first
    /// 'fixedCount' labels stay at the top in their own order (an 'All' / 'off' entry). 'keys' (same length, optional) are what is sorted by, so a
    /// changing suffix like ' (locked)' never reorders the list between refills. Read a pick back as labels[map[dropdown.value]].
    /// </summary>
    public static int[] AddSortedOptions(TMP_Dropdown dd, System.Collections.Generic.IList<string> labels, int fixedCount = 0, System.Collections.Generic.IList<string> keys = null)
    {
        int n = labels != null ? labels.Count : 0;
        int[] map = new int[n];
        for (int i = 0; i < n; i++) map[i] = i;
        int start = Mathf.Clamp(fixedCount, 0, n);
        System.Array.Sort(map, start, n - start, System.Collections.Generic.Comparer<int>.Create((a, b) =>
        {
            string ka = keys != null && a < keys.Count ? keys[a] : labels[a], kb = keys != null && b < keys.Count ? keys[b] : labels[b];
            int c = string.Compare(ka, kb, System.StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : a.CompareTo(b);   // (stable for equal names)
        }));
        if (dd == null) return map;
        System.Collections.Generic.List<string> sorted = new System.Collections.Generic.List<string>(n);
        for (int i = 0; i < n; i++) sorted.Add(labels[map[i]]);
        dd.ClearOptions();
        dd.AddOptions(sorted);
        return map;
    }

    /// <summary>The option index that shows original entry 'original' of a map from <see cref="AddSortedOptions"/> (0 when it is not there; 'original' itself without a map).</summary>
    public static int OptionOf(int[] map, int original)
    {
        if (map == null) return original;
        for (int i = 0; i < map.Length; i++) if (map[i] == original) return i;
        return 0;
    }

    /// <summary>The original entry behind option 'option' of a map from <see cref="AddSortedOptions"/> ('option' itself without a map or out of range).</summary>
    public static int Original(int[] map, int option) => map != null && option >= 0 && option < map.Length ? map[option] : option;

    /// <summary>
    /// A drop-down inside a scroll view has its opened list clipped by the view's mask. This moves the list (which TMP_Dropdown makes as a child called 'Dropdown List', with its own
    /// override-sorting canvas) up to the root canvas while it is open, at the same place and size on screen, so it can be seen and used in full.
    /// </summary>
    public static void LetListEscapeMasks(TMP_Dropdown dd)
    {
        if (dd != null && dd.GetComponent<PixelDropdownListEscape>() == null) dd.gameObject.AddComponent<PixelDropdownListEscape>();
    }

    /// <summary>Scrolls a vertical scroll view so a top-left anchored item in its content is at the top of the view (nothing happens if everything fits).</summary>
    public static void ScrollTo(ScrollRect scroll, RectTransform item)
    {
        if (scroll == null || scroll.content == null || item == null) return;
        RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
        float total = scroll.content.rect.height, view = viewport.rect.height;
        if (total <= view + 1f) return;
        float top = Mathf.Clamp(-item.anchoredPosition.y - 8f, 0f, total - view);
        scroll.verticalNormalizedPosition = 1f - top / (total - view);
    }

    /// <summary>Keeps a label on one line and shrinks its text (down to minSize) when it is too wide for its box, instead of spilling out of a button.</summary>
    public static void FitOneLine(TMP_Text t, float minSize = 10f)
    {
        if (t == null) return;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = minSize;
        t.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        t.textWrappingMode = TextWrappingModes.NoWrap;
#else
        t.enableWordWrapping = false;
#endif
    }

    /// <summary>
    /// The orange '!' INFO BADGE (Skills cards, Toggles rows...): a small rounded orange square with a white '!' and a drop shadow. 'onEnter' / 'onExit'
    /// run while the pointer is over it (the caller shows its own tooltip). Returns the badge's rect (position it yourself).
    /// </summary>
    public static RectTransform CreateInfoBadge(TMP_FontAsset font, Transform parent, float size, System.Action onEnter, System.Action onExit)
    {
        GameObject info = new GameObject("Info", typeof(RectTransform), typeof(Image));
        info.transform.SetParent(parent, false);
        Image img = info.GetComponent<Image>();
        img.color = new Color(0.95f, 0.55f, 0.12f, 1f);
        StyleButton(img);
        Shadow dropShadow = info.AddComponent<Shadow>();
        dropShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        dropShadow.effectDistance = new Vector2(0f, -2f);
        RectTransform rect = info.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        TMP_Text mark = CreateText(font, info.transform, "Mark", "!", size * 0.75f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        mark.raycastTarget = false;
        Stretch(mark.rectTransform);
        PixelHoverTip tip = info.AddComponent<PixelHoverTip>();
        tip.onEnter = onEnter;
        tip.onExit = onExit;
        return rect;
    }

    /// <summary>A resize grip for a window's bottom-right corner: three short diagonal lines, transparent elsewhere (white; tint it with the Image colour).</summary>
    public static Sprite GripSprite()
    {
        if (gripSprite != null) return gripSprite;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Resize Grip", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        // lines x + (1 - y) = c, i.e. running from bottom-left up to the top-right of the lower-right triangle
        float[] offsets = { 0.30f, 0.58f, 0.86f };
        const float half = 0.045f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cover = 0f;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 4f) / n, v = (y + (sy + 0.5f) / 4f) / n;
                        // distance along the diagonal (u + (1 - v)) measured from the corner (1, 0)
                        float d = ((1f - u) + v) * 0.7071f;
                        foreach (float o in offsets) if (Mathf.Abs(d - o * 0.7071f) <= half && u + (1f - v) > 1f) { cover += 1f / 16f; break; }
                    }
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(cover));
            }
        tex.SetPixels(px);
        tex.Apply();
        gripSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        gripSprite.name = "Resize Grip";
        return gripSprite;
    }

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    /// <summary>Swaps a button's text for a drawn triangle pointing right (the down triangle turned a quarter), tinted with 'color'.</summary>
    public static void UseRightTriangle(GameObject button, TMP_Text label, Color color, Vector2 buttonSize)
    {
        if (label != null) label.gameObject.SetActive(false);
        GameObject g = new GameObject("Triangle", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(button.transform, false);
        Image im = g.GetComponent<Image>();
        im.sprite = DownTriangleSprite();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        float s = Mathf.Min(buttonSize.x, buttonSize.y) * 0.42f;
        r.sizeDelta = new Vector2(s, s * 0.7f);
        r.localRotation = Quaternion.Euler(0f, 0f, 90f); // down -> right
        r.anchoredPosition = Vector2.zero;
    }

    /// <summary>Swaps a "&lt;" / "&gt;" arrow button's letter for a drawn triangle pointing left or right, tinted with the label's colour.</summary>
    /// <summary>Draws the little tilted pushpin (the Skills window's pin button) on a button; 'size' is the icon's size in canvas units (the Skills button uses 40). One icon for every pin button.</summary>
    public static void AddPinIcon(Button button, float size = 40f)
    {
        GameObject icon = new GameObject("Pin Icon", typeof(RectTransform));
        icon.transform.SetParent(button.transform, false);
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(size, size);
        ir.localRotation = Quaternion.Euler(0f, 0f, 35f);
        float k = size / 40f;
        void Part(Vector2 partSize, Vector2 at, Color c)
        {
            GameObject g = new GameObject("Part", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(icon.transform, false);
            g.GetComponent<Image>().color = c;
            g.GetComponent<Image>().raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = partSize * k;
            r.anchoredPosition = at * k;
        }
        Part(new Vector2(26f, 8f), new Vector2(0f, 14f), new Color(0.95f, 0.35f, 0.3f));      // the head
        Part(new Vector2(14f, 16f), new Vector2(0f, 4f), new Color(0.85f, 0.25f, 0.22f));    // the body
        Part(new Vector2(20f, 5f), new Vector2(0f, -4f), new Color(0.95f, 0.95f, 1f));       // the collar
        Part(new Vector2(4f, 16f), new Vector2(0f, -14f), new Color(0.85f, 0.88f, 0.95f));   // the needle
    }

    public static void UseArrowGlyph(Button button, bool pointLeft)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        Color color = label != null ? label.color : Color.white;
        Vector2 size = button.GetComponent<RectTransform>().sizeDelta;
        if (size.x <= 1f || size.y <= 1f) size = new Vector2(80f, 80f);
        UseRightTriangle(button.gameObject, label, color, size);
        Transform glyph = button.transform.Find("Triangle");
        if (glyph != null && pointLeft) glyph.localRotation = Quaternion.Euler(0f, 0f, -90f); // down -> left
    }

    /// <summary>Swaps a close button's "X" letter for the drawn cross, tinted with the label colour.</summary>
    public static void UseCloseGlyph(GameObject button, TMP_Text label, Color color, Vector2 buttonSize)
    {
        if (label != null) label.gameObject.SetActive(false);
        GameObject g = new GameObject("Cross", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(button.transform, false);
        Image im = g.GetComponent<Image>();
        im.sprite = CloseCrossSprite();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        float s = Mathf.Min(buttonSize.x, buttonSize.y) * 0.42f;
        r.sizeDelta = new Vector2(s, s);
        r.anchoredPosition = Vector2.zero;
    }

    /// <summary>Makes a rect fill its parent.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Sprite downTriangle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { downTriangle = null; closeCross = null; eyedropper = null; magnifier = null; }

    /// <summary>A white, smooth-edged downward triangle (drawn once in code) for drop-down arrows.</summary>
    private static Sprite DownTriangleSprite()
    {
        if (downTriangle != null) return downTriangle;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Down Triangle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Apex at the bottom centre, top edge across the full width; 4x4 supersampling for the edges.
                float cover = 0f;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        float u = (x + (sx + 0.5f) / 4f) / n, v = (y + (sy + 0.5f) / 4f) / n; // v = 0 at the bottom
                        if (Mathf.Abs(u - 0.5f) <= 0.5f * v) cover += 1f / 16f;
                    }
                px[y * n + x] = new Color(1f, 1f, 1f, cover);
            }
        tex.SetPixels(px);
        tex.Apply();
        downTriangle = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        downTriangle.name = "Down Triangle";
        return downTriangle;
    }

    /// <summary>A TMP dropdown built in code (closed box + scrolling list). Fill it with <c>options</c>.</summary>
    public static TMP_Dropdown CreateDropdown(TMP_FontAsset font, Transform parent, string objectName, Vector2 size,
                                              Color boxColor, Color listColor, Color textColor, float fontSize, bool icons = false)
    {
        // icons = a small picture left of each entry and of the shown name (fill it with TMP_Dropdown.OptionData(text, sprite)).
        float iconPad = icons ? size.y - 4f : 0f;
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
        root.transform.SetParent(parent, false);
        root.GetComponent<RectTransform>().sizeDelta = size;
        Image rootImage = root.GetComponent<Image>();
        rootImage.color = boxColor;
        StyleButton(rootImage);   // the closed box looks like a button (bevelled, rounded)

        TMP_Text label = CreateText(font, root.transform, "Label", "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        label.richText = true;
        label.enableAutoSizing = true; // a long name shrinks to fit instead of wrapping over the arrow
        label.fontSizeMax = fontSize;
        label.fontSizeMin = 10f;
        label.overflowMode = TextOverflowModes.Truncate;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(14f + iconPad, 2f); lr.offsetMax = new Vector2(-size.y, -2f);
        Image captionIcon = null;
        if (icons)
        {
            captionIcon = MakeDropdownIcon(root.transform, "Caption Icon", size.y - 12f, 8f);
        }

        // Down arrow: a drawn triangle (not a letter), tinted with the text colour.
        GameObject arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowGo.transform.SetParent(root.transform, false);
        Image arrow = arrowGo.GetComponent<Image>();
        arrow.sprite = DownTriangleSprite();
        arrow.color = textColor;
        arrow.raycastTarget = false;
        RectTransform ar = arrow.rectTransform;
        float arrowSize = Mathf.Max(8f, size.y * 0.32f);
        ar.anchorMin = ar.anchorMax = new Vector2(1f, 0.5f);
        ar.pivot = new Vector2(0.5f, 0.5f);
        ar.sizeDelta = new Vector2(arrowSize, arrowSize * 0.7f);
        ar.anchoredPosition = new Vector2(-size.y * 0.5f, 0f);

        // Template (the popup list), hidden until opened.
        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(root.transform, false);
        template.GetComponent<Image>().color = listColor;
        StyleBox(template.GetComponent<Image>());   // the opened list: a rounded, softly shaded panel
        RectTransform tr = template.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = Vector2.zero;
        tr.sizeDelta = new Vector2(0f, size.y * 6f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(template.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<RectTransform>().offsetMin = new Vector2(5f, 5f);    // room for the rounded corners
        viewport.GetComponent<RectTransform>().offsetMax = new Vector2(-5f, -5f);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform cr = content.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = Vector2.one;
        cr.pivot = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(0f, size.y);

        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        RectTransform ir = item.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 0.5f); ir.anchorMax = new Vector2(1f, 0.5f);
        ir.sizeDelta = new Vector2(0f, size.y);

        GameObject itemBg = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
        itemBg.transform.SetParent(item.transform, false);
        Stretch(itemBg.GetComponent<RectTransform>());
        itemBg.GetComponent<RectTransform>().offsetMin = new Vector2(2f, 2f);
        itemBg.GetComponent<RectTransform>().offsetMax = new Vector2(-2f, -2f);
        Image itemBgImage = itemBg.GetComponent<Image>();
        itemBgImage.color = new Color(listColor.r + 0.08f, listColor.g + 0.08f, listColor.b + 0.08f, 1f);
        StyleBox(itemBgImage);   // each entry is its own rounded chip

        GameObject check = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(item.transform, false);
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = new Color(1f, 1f, 1f, 0.18f);
        Stretch(check.GetComponent<RectTransform>());
        check.GetComponent<RectTransform>().offsetMin = new Vector2(2f, 2f);
        check.GetComponent<RectTransform>().offsetMax = new Vector2(-2f, -2f);
        StyleBox(checkImage);

        TMP_Text itemLabel = CreateText(font, item.transform, "Item Label", "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        itemLabel.richText = true;
        itemLabel.enableAutoSizing = true;
        itemLabel.fontSizeMax = fontSize;
        itemLabel.fontSizeMin = 10f;
        itemLabel.overflowMode = TextOverflowModes.Truncate;
        RectTransform ilr = itemLabel.rectTransform;
        ilr.anchorMin = Vector2.zero; ilr.anchorMax = Vector2.one;
        ilr.offsetMin = new Vector2(14f + iconPad, 2f); ilr.offsetMax = new Vector2(-8f, -2f);
        Image itemIcon = icons ? MakeDropdownIcon(item.transform, "Item Icon", size.y - 12f, 10f) : null;

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBgImage;
        toggle.graphic = checkImage;
        toggle.isOn = false;

        // A scroll bar for long lists, in the same slim rounded style as every other one (hidden when the list fits).
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(template.transform, false);
        RectTransform barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(1f, 0f); barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(ScrollBarWidth, -12f);
        barRt.anchoredPosition = new Vector2(-4f, 0f);
        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(barGo.transform, false);
        Image handleImg = handleGo.GetComponent<Image>();
        StyleScrollBar(barGo.GetComponent<Image>(), handleImg);
        Stretch(handleGo.GetComponent<RectTransform>());
        Scrollbar dropBar = barGo.GetComponent<Scrollbar>();
        dropBar.direction = Scrollbar.Direction.BottomToTop;
        dropBar.handleRect = handleGo.GetComponent<RectTransform>();
        dropBar.targetGraphic = handleImg;

        ScrollRect scroll = template.GetComponent<ScrollRect>();
        scroll.gameObject.AddComponent<PixelScrollSound>();
        scroll.content = cr;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.verticalScrollbar = dropBar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = rootImage;
        dropdown.template = tr;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        root.AddComponent<PixelDropdownScroll>();   // an opened list starts at the chosen entry instead of the top
        if (icons) { dropdown.captionImage = captionIcon; dropdown.itemImage = itemIcon; }
        template.SetActive(false);
        return dropdown;
    }

    private static Image MakeDropdownIcon(Transform parent, string objectName, float side, float x)
    {
        GameObject g = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.raycastTarget = false;
        im.preserveAspect = false;
        RectTransform r = im.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(side, side);
        r.anchoredPosition = new Vector2(x, 0f);
        return im;
    }

    /// <summary>A tiny picture for a dropdown entry: the part 'uv' of 'tex' (or a flat 'tint' square when tex is null).</summary>
    public static Sprite PreviewSprite(Texture tex, Rect uv, Color tint)
    {
        Texture2D t2 = tex as Texture2D;
        if (t2 == null)
        {
            Texture2D flat = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            flat.SetPixels(new[] { tint, tint, tint, tint });
            flat.Apply();
            return Sprite.Create(flat, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
        }
        Rect px = new Rect(uv.x * t2.width, uv.y * t2.height, Mathf.Max(1f, uv.width * t2.width), Mathf.Max(1f, uv.height * t2.height));
        return Sprite.Create(t2, px, new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>A TMP input field built in code. Content type is free text; set it afterwards if needed.</summary>
    public static TMP_InputField CreateInputField(TMP_FontAsset font, Transform parent, string objectName, Vector2 size,
                                                  Color boxColor, Color textColor, float fontSize, string placeholder)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        root.transform.SetParent(parent, false);
        root.GetComponent<RectTransform>().sizeDelta = size;
        Image image = root.GetComponent<Image>();
        image.color = boxColor;
        StyleBox(image, true);   // a sunken, rounded field

        GameObject area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(root.transform, false);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        Stretch(areaRect);
        areaRect.offsetMin = new Vector2(12f, 4f);
        areaRect.offsetMax = new Vector2(-12f, -4f);

        TMP_Text hint = CreateText(font, area.transform, "Placeholder", placeholder, fontSize, TextAlignmentOptions.MidlineLeft,
                                   FontStyles.Italic, new Color(textColor.r, textColor.g, textColor.b, 0.45f));
        Stretch(hint.rectTransform);
        TMP_Text text = CreateText(font, area.transform, "Text", "", fontSize, TextAlignmentOptions.MidlineLeft,
                                   FontStyles.Normal, textColor);
        Stretch(text.rectTransform);

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = areaRect;
        input.textComponent = text;
        input.placeholder = hint;

        // Clicking the box puts a flashing cursor in it and the example text goes away (the default caret is dark grey, invisible on a dark box).
        input.customCaretColor = true;
        input.caretColor = Color.white;
        input.caretWidth = 3;
        input.caretBlinkRate = 0.85f;
        input.selectionColor = new Color(0.3f, 0.55f, 1f, 0.45f);
        input.onSelect.AddListener(_ => { if (input.placeholder != null) input.placeholder.gameObject.SetActive(false); });
        input.onDeselect.AddListener(_ => { if (input.placeholder != null) input.placeholder.gameObject.SetActive(string.IsNullOrEmpty(input.text)); });
        return input;
    }

    /// <summary>A magnifying glass inside the left end of a search box (the text starts to the right of it).</summary>
    public static void AddSearchIcon(TMP_InputField field)
    {
        if (field == null) return;
        const float iconWidth = 34f;
        GameObject icon = new GameObject("Search Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(field.transform, false);
        Image im = icon.GetComponent<Image>();
        im.sprite = MagnifierSprite();
        im.color = new Color(1f, 1f, 1f, 0.7f);
        im.raycastTarget = false;
        im.preserveAspect = true;
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(0f, 0.15f);
        ir.anchorMax = new Vector2(0f, 0.85f);
        ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(iconWidth, 0f);
        ir.anchoredPosition = new Vector2(8f, 0f);
        if (field.textViewport != null) field.textViewport.offsetMin = new Vector2(8f + iconWidth + 6f, field.textViewport.offsetMin.y);
    }

    /// <summary>
    /// A vertical scroll view: a masked viewport (mouse wheel / drag) with a content rect on top and a thin scroll bar
    /// that is only shown while the content is taller than the viewport. The caller positions the returned viewport,
    /// fills <paramref name="content"/> (anchored top, full width) and calls <see cref="UpdateScrollView"/> after
    /// changing the content height.
    /// </summary>
    public static ScrollRect CreateScrollView(Transform parent, string objectName, Color handleColor, float barWidth,
                                              float wheelSpeed, out RectTransform content, out GameObject barObject)
    {
        GameObject view = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(parent, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but takes wheel / drag input
        RectTransform viewport = view.GetComponent<RectTransform>();

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(view.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        barObject = new GameObject("Scroll Bar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barObject.transform.SetParent(view.transform, false);
        barObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        RectTransform br = barObject.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(Mathf.Min(barWidth, ScrollBarWidth), 0f);
        br.anchoredPosition = new Vector2(-2f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(barObject.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        StyleScrollBar(barObject.GetComponent<Image>(), handleImage);
        Stretch(handle.GetComponent<RectTransform>());

        Scrollbar bar = barObject.GetComponent<Scrollbar>();
        bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.GetComponent<RectTransform>();
        bar.targetGraphic = handleImage;

        ScrollRect scroll = view.GetComponent<ScrollRect>();
        scroll.gameObject.AddComponent<PixelScrollSound>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = wheelSpeed;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        barObject.SetActive(false);
        PixelScrollBarAutoHide hide = scroll.gameObject.AddComponent<PixelScrollBarAutoHide>();
        hide.scroll = scroll;
        hide.bar = barObject;
        return scroll;
    }

    /// <summary>The one look for every scroll bar: a slim rounded dark track with a lighter rounded handle.</summary>
    public static void StyleScrollBar(Image track, Image handle)
    {
        if (track != null) { track.color = new Color(0.16f, 0.16f, 0.2f, 1f); StyleButton(track); }
        if (track != null && track.GetComponent<Scrollbar>() != null && track.GetComponent<PixelScrollBarFade>() == null)
            track.gameObject.AddComponent<PixelScrollBarFade>();   // every scroll bar fades away when not in use
        if (handle != null) { handle.color = new Color(0.5f, 0.5f, 0.6f, 1f); StyleButton(handle); }
    }

    /// <summary>Width of every scroll bar (canvas units).</summary>
    public const float ScrollBarWidth = 10f;

    /// <summary>Sets a scroll view's content height and shows the scroll bar only when the content doesn't fit.</summary>
    public static void UpdateScrollView(ScrollRect scroll, GameObject barObject, float contentHeight, float viewHeight)
    {
        scroll.content.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, 1f));
        bool scrolls = contentHeight > viewHeight + 0.5f;
        if (barObject.activeSelf != scrolls) barObject.SetActive(scrolls);
        if (!scrolls) scroll.content.anchoredPosition = Vector2.zero;
    }

    /// <summary>A square tick box (a Toggle: box image + a drawn tick that shows while it is on), 'size' wide. Place it yourself.</summary>
    public static Toggle CreateTickBox(Transform parent, float size, Color boxColor, Color tickColor)
    {
        GameObject boxGo = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        boxGo.transform.SetParent(parent, false);
        Image bg = boxGo.GetComponent<Image>();
        bg.color = boxColor;
        boxGo.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(boxGo.transform, false);
        Image tickImage = tick.GetComponent<Image>();
        tickImage.sprite = TickSprite();
        tickImage.color = tickColor;
        tickImage.raycastTarget = false;
        RectTransform kr = tick.GetComponent<RectTransform>();
        Stretch(kr);
        kr.offsetMin = new Vector2(size * 0.1f, size * 0.1f);
        kr.offsetMax = new Vector2(-size * 0.1f, -size * 0.1f);

        Toggle toggle = boxGo.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = tickImage;
        return toggle;
    }

    /// <summary>A horizontal 0..1 slider built in code (track, fill and a draggable handle).</summary>
    public static Slider CreateSlider(Transform parent, string objectName, Color trackColor, Color fillColor, Color handleColor)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        bg.GetComponent<Image>().color = trackColor;
        RectTransform bgr = bg.GetComponent<RectTransform>();
        bgr.anchorMin = new Vector2(0f, 0.35f);
        bgr.anchorMax = new Vector2(1f, 0.65f);
        bgr.offsetMin = bgr.offsetMax = Vector2.zero;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        RectTransform far = fillArea.GetComponent<RectTransform>();
        far.anchorMin = new Vector2(0f, 0.35f);
        far.anchorMax = new Vector2(1f, 0.65f);
        far.offsetMin = new Vector2(8f, 0f);
        far.offsetMax = new Vector2(-8f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = fillColor;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(0f, 1f);
        fr.sizeDelta = new Vector2(8f, 0f);

        GameObject slideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        slideArea.transform.SetParent(root.transform, false);
        RectTransform sar = slideArea.GetComponent<RectTransform>();
        sar.anchorMin = Vector2.zero;
        sar.anchorMax = Vector2.one;
        sar.offsetMin = new Vector2(10f, 0f);
        sar.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(slideArea.transform, false);
        Image hi = handle.GetComponent<Image>();
        hi.color = handleColor;
        RectTransform hr = handle.GetComponent<RectTransform>();
        hr.anchorMin = Vector2.zero;
        hr.anchorMax = new Vector2(0f, 1f);
        hr.sizeDelta = new Vector2(22f, 0f);

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fr;
        slider.handleRect = hr;
        slider.targetGraphic = hi;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        return slider;
    }
}


/// <summary>Shows a code-built scroll view's bar only while its content is taller than the view (checked every frame, so a resized window can't leave a useless bar behind).</summary>
public class PixelScrollBarAutoHide : MonoBehaviour
{
    public ScrollRect scroll;
    public GameObject bar;

    private void LateUpdate()
    {
        if (scroll == null || bar == null || scroll.content == null) return;
        RectTransform view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        bool needed = scroll.content.rect.height > view.rect.height + 0.5f;
        if (bar.activeSelf != needed) bar.SetActive(needed);
        if (!needed && scroll.content.anchoredPosition.y != 0f) scroll.content.anchoredPosition = Vector2.zero;
    }
}

/// <summary>
/// Keeps a scroll list centred on one entry for its first few frames (the Settings look list, and every drop-down made by PixelUIKit.CreateDropdown via PixelDropdownScroll):
/// the list is built with its layout still settling, so the position is set again for three frames instead of once.
/// </summary>
public class PixelScrollKeep : MonoBehaviour
{
    public RectTransform content, viewport;
    public int index, count;
    public float itemHeight;   // 0 = content height / count
    private int frames = 3;

    private void LateUpdate()
    {
        if (frames-- <= 0 || content == null || viewport == null) { Destroy(this); return; }
        float contentH = content.rect.height, viewH = viewport.rect.height;
        float item = itemHeight > 0f ? itemHeight : (count > 0 ? contentH / count : 0f);
        if (item <= 0f || contentH <= viewH) return;
        float target = Mathf.Clamp((index + 0.5f) * item - viewH * 0.5f, 0f, contentH - viewH);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, target);
    }
}

/// <summary>On every code-built drop-down: when its list opens, scroll it to the currently chosen entry.</summary>
public class PixelDropdownScroll : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    private bool wasOpen;

    private void Update()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
        if (dropdown == null) return;
        bool open = dropdown.IsExpanded;
        if (open && !wasOpen)
        {
            Transform list = transform.Find("Dropdown List");
            ScrollRect sr = list != null ? list.GetComponent<ScrollRect>() : null;
            if (sr != null && sr.content != null && sr.viewport != null)
            {
                PixelScrollKeep keep = sr.gameObject.AddComponent<PixelScrollKeep>();
                keep.content = sr.content; keep.viewport = sr.viewport;
                keep.index = dropdown.value; keep.count = dropdown.options.Count;
            }
        }
        wasOpen = open;
    }
}

/// <summary>Moves a drop-down's opened list to the root canvas (see PixelUIKit.LetListEscapeMasks).</summary>
public class PixelDropdownListEscape : MonoBehaviour
{
    private Transform moved;

    private void LateUpdate()
    {
        if (moved != null) return;   // (it is destroyed when the list closes, then this looks for the next one)
        Transform list = transform.Find("Dropdown List");
        if (list == null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        list.SetParent(canvas.rootCanvas.transform, true);
        list.SetAsLastSibling();
        moved = list;
    }
}

/// <summary>Hands drags on a UI element to code (onDrag gets the pointer data each frame of a drag, onBegin / onEnd when it starts / stops). Add it to any raycast-target graphic.</summary>
public class PixelDragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public System.Action<PointerEventData> onBegin, onDrag, onEnd;
    public void OnBeginDrag(PointerEventData e) { onBegin?.Invoke(e); }
    public void OnDrag(PointerEventData e) { onDrag?.Invoke(e); }
    public void OnEndDrag(PointerEventData e) { onEnd?.Invoke(e); }
}

/// <summary>Makes &lt;link="id"&gt; parts of a TextMeshPro text clickable: onLink gets the link id. Add it to the text's object (its raycastTarget must be on).</summary>
public class PixelLinkClick : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text text;
    public System.Action<string> onLink;

    public void OnPointerClick(PointerEventData e)
    {
        if (text == null || onLink == null) return;
        int i = TMP_TextUtilities.FindIntersectingLink(text, e.position, e.pressEventCamera);
        if (i >= 0 && i < text.textInfo.linkCount) onLink(text.textInfo.linkInfo[i].GetLinkID());
    }
}
