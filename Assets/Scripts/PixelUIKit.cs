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
    /// Moves a 3D text so all of it stays inside the camera's view (its whole width and height, with a margin as a fraction of
    /// the screen), clear of the black bars. Returns the adjusted world position.
    /// </summary>
    public static Vector3 KeepOnScreen(Camera cam, TextMeshPro text, Vector3 position, float margin = 0.03f)
    {
        if (cam == null || text == null) return position;
        Vector3 v = cam.WorldToViewportPoint(position);
        if (v.z <= 0.01f) return position;

        float viewHeight = 2f * v.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
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
        go.GetComponent<Button>().targetGraphic = image;
        go.GetComponent<Button>().onClick.AddListener(() => PixelAudio.Play("ui_click"));

        TMP_Text text = CreateText(font, go.transform, "Label", label, labelSize, TextAlignmentOptions.Center,
                                   FontStyles.Bold, labelColor);
        Stretch(text.rectTransform);
        return go.GetComponent<Button>();
    }

    /// <summary>Makes a rect fill its parent.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>A TMP dropdown built in code (closed box + scrolling list). Fill it with <c>options</c>.</summary>
    public static TMP_Dropdown CreateDropdown(TMP_FontAsset font, Transform parent, string objectName, Vector2 size,
                                              Color boxColor, Color listColor, Color textColor, float fontSize)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
        root.transform.SetParent(parent, false);
        root.GetComponent<RectTransform>().sizeDelta = size;
        Image rootImage = root.GetComponent<Image>();
        rootImage.color = boxColor;

        TMP_Text label = CreateText(font, root.transform, "Label", "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        label.richText = true;
        label.enableAutoSizing = true; // a long name shrinks to fit instead of wrapping over the arrow
        label.fontSizeMax = fontSize;
        label.fontSizeMin = 10f;
        label.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(14f, 2f); lr.offsetMax = new Vector2(-size.y, -2f);

        TMP_Text arrow = CreateText(font, root.transform, "Arrow", "v", fontSize, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        RectTransform ar = arrow.rectTransform;
        ar.anchorMin = new Vector2(1f, 0f); ar.anchorMax = Vector2.one;
        ar.pivot = new Vector2(1f, 0.5f);
        ar.sizeDelta = new Vector2(size.y, 0f); ar.anchoredPosition = Vector2.zero;

        // Template (the popup list), hidden until opened.
        GameObject template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        template.transform.SetParent(root.transform, false);
        template.GetComponent<Image>().color = listColor;
        RectTransform tr = template.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = Vector2.zero;
        tr.sizeDelta = new Vector2(0f, size.y * 6f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(template.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());

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
        Image itemBgImage = itemBg.GetComponent<Image>();
        itemBgImage.color = new Color(listColor.r + 0.08f, listColor.g + 0.08f, listColor.b + 0.08f, 1f);

        GameObject check = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(item.transform, false);
        Image checkImage = check.GetComponent<Image>();
        checkImage.color = new Color(1f, 1f, 1f, 0.18f);
        Stretch(check.GetComponent<RectTransform>());

        TMP_Text itemLabel = CreateText(font, item.transform, "Item Label", "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        itemLabel.richText = true;
        itemLabel.enableAutoSizing = true;
        itemLabel.fontSizeMax = fontSize;
        itemLabel.fontSizeMin = 10f;
        itemLabel.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform ilr = itemLabel.rectTransform;
        ilr.anchorMin = Vector2.zero; ilr.anchorMax = Vector2.one;
        ilr.offsetMin = new Vector2(14f, 2f); ilr.offsetMax = new Vector2(-8f, -2f);

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBgImage;
        toggle.graphic = checkImage;
        toggle.isOn = false;

        ScrollRect scroll = template.GetComponent<ScrollRect>();
        scroll.gameObject.AddComponent<PixelScrollSound>();
        scroll.content = cr;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = rootImage;
        dropdown.template = tr;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        template.SetActive(false);
        return dropdown;
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
        return input;
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
        br.sizeDelta = new Vector2(barWidth, 0f);
        br.anchoredPosition = new Vector2(-2f, 0f);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(barObject.transform, false);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = handleColor;
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
        return scroll;
    }

    /// <summary>Sets a scroll view's content height and shows the scroll bar only when the content doesn't fit.</summary>
    public static void UpdateScrollView(ScrollRect scroll, GameObject barObject, float contentHeight, float viewHeight)
    {
        scroll.content.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, 1f));
        bool scrolls = contentHeight > viewHeight + 0.5f;
        if (barObject.activeSelf != scrolls) barObject.SetActive(scrolls);
        if (!scrolls) scroll.content.anchoredPosition = Vector2.zero;
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
