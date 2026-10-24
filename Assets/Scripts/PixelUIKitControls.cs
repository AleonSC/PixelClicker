// ---- System notes ----
// PixelUIKitControls.cs
//   Part of PixelUIKit (static partial class): the input controls - CreateDropdown / CreateLookDropdown (+ their icons and preview sprites),
//   CreateInputField + AddSearchIcon, CreateScrollView / CreateScrollContent / CreateScrollBar / SetupVerticalScroll / StyleScrollBar / UpdateScrollView, CreateTickBox, CreateSlider. The notes are in PixelUIKit.cs.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static partial class PixelUIKit
{
    /// <summary>
    /// A TMP dropdown built in code (closed box + scrolling list). Fill it with <c>options</c>. EVERY drop-down in the game is made here
    /// (a look picker through <see cref="CreateLookDropdown"/>), so a fix or style change reaches them all.
    /// </summary>
    public static TMP_Dropdown CreateDropdown(TMP_FontAsset font, Transform parent, string objectName, Vector2 size,
                                              Color boxColor, Color listColor, Color textColor, float fontSize, bool icons = false, bool escapeMasks = false)
    {
        // icons = a small picture left of each entry and of the shown name (fill it with TMP_Dropdown.OptionData(text, sprite)).
        // escapeMasks = the opened list moves to the root canvas so a scroll view around the drop-down can't clip it (see LetListEscapeMasks).
        // The list's entries are as tall as the closed box (size.y): a taller box gives bigger rows.
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
        if (escapeMasks) LetListEscapeMasks(dropdown);
        template.SetActive(false);
        return dropdown;
    }

    /// <summary>
    /// A drop-down listing every look of an <see cref="IPixelLookSource"/> (floor styles, skies) with a preview picture beside each name.
    /// Picking one applies it; the shown look follows changes made anywhere else (see <see cref="PixelLookDropdown"/>). Used by the
    /// Settings floor / sky rows and the title screen's pickers. 'source' is asked again each time, so it works even if the look wakes up later.
    /// </summary>
    public static PixelLookDropdown CreateLookDropdown(TMP_FontAsset font, Transform parent, string objectName, Vector2 size,
                                                       Color boxColor, Color listColor, Color textColor, float fontSize,
                                                       System.Func<IPixelLookSource> source, bool escapeMasks = false)
    {
        TMP_Dropdown dd = CreateDropdown(font, parent, objectName, size, boxColor, listColor, textColor, fontSize, true, escapeMasks);
        PixelLookDropdown look = dd.gameObject.AddComponent<PixelLookDropdown>();
        look.Setup(dd, source);
        return look;
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

        content = CreateScrollContent(view.transform);

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

    /// <summary>A scroll view's CONTENT rect: anchored to the top, full width, zero height (the owner sets the height as it fills it).</summary>
    public static RectTransform CreateScrollContent(Transform parent)
    {
        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(parent, false);
        RectTransform content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;
        return content;
    }

    /// <summary>
    /// A vertical SCROLL BAR named "Scrollbar" under 'parent' (for windows that build their own ScrollRect, e.g. the Log's Goals tab and the Shop):
    /// the track and a handle in the StyleScrollBar look, scrolling bottom to top. The caller positions its RectTransform and hands it to
    /// <see cref="SetupVerticalScroll"/>.
    /// </summary>
    public static Scrollbar CreateScrollBar(Transform parent, Color trackColor)
    {
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(parent, false);
        barGo.GetComponent<Image>().color = trackColor;

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(barGo.transform, false);
        Image handleImage = handleGo.GetComponent<Image>();
        StyleScrollBar(barGo.GetComponent<Image>(), handleImage);
        RectTransform hr = handleGo.GetComponent<RectTransform>();
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = hr;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    /// <summary>Sets a ScrollRect up to scroll its content up and down only (clamped, with the scroll sound), its bar fading in only when needed.</summary>
    public static void SetupVerticalScroll(ScrollRect scroll, RectTransform content, Scrollbar scrollbar, float wheelSpeed)
    {
        scroll.gameObject.AddComponent<PixelScrollSound>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = wheelSpeed;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
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
