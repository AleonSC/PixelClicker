// ---- System notes ----
// PixelPauseMenuRows.cs
//   Part of PixelPauseMenu (partial class): the shared building blocks of the menu screens - ShowView, the section panels with their title plate and close X, and the row builders (labels, hover tips, stat rows, tick boxes, eyedropper / cycle buttons, sliders). The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    // ------------------------------------------------------------------
    // Stats and Settings sections
    // ------------------------------------------------------------------

    /// <summary>Shows one of the menu's views (main, stats or settings) and hides the others.</summary>
    private void ShowView(GameObject view)
    {
        if (view == null) return;
        if (openedFromTitle && view == mainPanel) { SetPaused(false); return; } // 'back' returns to the title screen
        if (mainPanel != null) SetView(mainPanel, view == mainPanel);
        if (statsPanel != null) SetView(statsPanel, view == statsPanel);
        if (settingsPanel != null) SetView(settingsPanel, view == settingsPanel);
        if (howToPlayScreen != null) SetView(howToPlayScreen.panel, view == howToPlayScreen.panel);
        if (controlsScreen != null) SetView(controlsScreen.panel, view == controlsScreen.panel);
        if (howToPlayScreen != null && view == howToPlayScreen.panel) RefreshGuideScreen(howToPlayScreen);
        if (controlsScreen != null && view == controlsScreen.panel) RefreshGuideScreen(controlsScreen);
        if (restartPanel != null) SetView(restartPanel, view == restartPanel);
        if (quitPanel != null) SetView(quitPanel, view == quitPanel);
        if (saveLoadPanel != null) SetView(saveLoadPanel, view == saveLoadPanel);
        if (keysPanel != null) SetView(keysPanel, view == keysPanel);
        if (playlistPanel != null) SetView(playlistPanel, view == playlistPanel);
        if (view == playlistPanel) RefreshPlaylistToggles();
        if (view == keysPanel) { capturingAction = -1; RefreshKeyLabels(); }
        if (view == saveLoadPanel) OpenSaveLoad();
        if (view == restartPanel && restartHold != null) restartHold.ResetProgress();

        if (view == statsPanel) RefreshStats();
        if (view == settingsPanel && clicker != null)
        {
            rotationToggle.SetIsOnWithoutNotify(clicker.AllowRotation);
            pulsingToggle.SetIsOnWithoutNotify(clicker.AllowPulsing);
            abbreviateToggle.SetIsOnWithoutNotify(PixelClicker.AbbreviateNumbers);
            hidePurchasedToggle.SetIsOnWithoutNotify(PixelShop.HidePurchased);
            backgroundToggle.SetIsOnWithoutNotify(clicker.RunInBackground);
            pauseStopsToggle.SetIsOnWithoutNotify(pauseStopsGame);
        }
        if (view == settingsPanel) foreach (System.Action refresh in settingsRefreshers) refresh();
        if (view == settingsPanel && PixelAudio.Instance != null && masterSlider != null)
        {
            masterSlider.SetValueWithoutNotify(PixelAudio.Instance.MasterVolume);
            effectsSlider.SetValueWithoutNotify(PixelAudio.Instance.EffectsVolume);
            musicSlider.SetValueWithoutNotify(PixelAudio.Instance.MusicVolume);
            muteToggle.SetIsOnWithoutNotify(PixelAudio.Instance.Muted);
            if (muteMusicToggle != null) muteMusicToggle.SetIsOnWithoutNotify(PixelAudio.Instance.MusicMuted);
        }
    }

    /// <summary>A centred panel with a title; rows are added below it, then a Back button.</summary>
    private GameObject BuildSectionPanel(string objectName, string title, out float y)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(menuRoot.transform, false);
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = Vector2.zero;

        RectTransform plate = BuildTitlePlate(panel.transform);
        float plateH = PlateHeight, side = plateH - 2f * PlatePad;
        TMP_Text t = MakeText(plate, "Title", title, titleFontSize, FontStyles.Bold);
        PixelUIKit.Caps(t);
        t.raycastTarget = false;
        t.enableAutoSizing = true;   // (a long title shrinks instead of running under the X)
        t.fontSizeMax = titleFontSize;
        t.fontSizeMin = 20f;
        RectTransform tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        float room = PlatePad + side + 12f;   // the same room on both sides: truly centred
        tr.offsetMin = new Vector2(room, 2f);
        tr.offsetMax = new Vector2(-room, 0f);

        y = PlateMargin + plateH + 16f;
        return panel;
    }

    /// <summary>
    /// The title plate across the top of a menu window: a sunken rounded band with a
    /// three-pixel ornament at its left end (the X or the search box sits in its right end). Small cubes fall from behind it (PixelMenuCubeFall).
    /// </summary>
    private RectTransform BuildTitlePlate(Transform panel)
    {
        float plateH = PlateHeight;
        GameObject plateGo = new GameObject("Title Plate", typeof(RectTransform), typeof(Image));
        plateGo.transform.SetParent(panel, false);
        Image plateImage = plateGo.GetComponent<Image>();
        plateImage.color = titlePlateColor;
        plateImage.raycastTarget = false;
        PixelUIKit.StyleBox(plateImage, true);
        RectTransform plate = plateGo.GetComponent<RectTransform>();
        plate.anchorMin = new Vector2(0f, 1f);
        plate.anchorMax = new Vector2(1f, 1f);
        plate.pivot = new Vector2(0.5f, 1f);
        plate.offsetMin = new Vector2(PlateMargin, -PlateMargin - plateH);
        plate.offsetMax = new Vector2(-PlateMargin, -PlateMargin);

        // The left ornament: three stepped pixels in the accent colour, the same size box as the X so the two ends match.
        float side = plateH - 2f * PlatePad;
        GameObject orn = new GameObject("Ornament", typeof(RectTransform));
        orn.transform.SetParent(plate, false);
        RectTransform ornRect = orn.GetComponent<RectTransform>();
        ornRect.anchorMin = ornRect.anchorMax = ornRect.pivot = new Vector2(0f, 0.5f);
        ornRect.sizeDelta = new Vector2(side, side);
        ornRect.anchoredPosition = new Vector2(PlatePad, 0f);
        float px = side * 0.22f;
        for (int i = 0; i < 3; i++)
        {
            GameObject dot = new GameObject("Pixel", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(orn.transform, false);
            Image di = dot.GetComponent<Image>();
            Color c = PixelUITheme.ColorOf(ButtonRole.Selected);
            di.color = new Color(c.r, c.g, c.b, 1f - i * 0.28f);
            di.raycastTarget = false;
            RectTransform dr = di.rectTransform;
            dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(px, px);
            dr.anchoredPosition = new Vector2((i - 1) * px * 1.25f, (1 - i) * px * 1.25f);
        }

        PixelMenuFx.AddCubeFall(panel as RectTransform, plate);   // (fancy menu: little cubes with light trails drop from behind the plate)
        return plate;
    }

    // The title plate's geometry (BuildSectionPanel / AddCloseX share it).
    private const float PlateMargin = 16f, PlatePad = 10f;
    private float PlateHeight => titleFontSize * 1.45f;

    /// <summary>An X at the top right of a section window that goes back (to the main menu, or to 'backTo').</summary>
    private void AddCloseX(GameObject panel, GameObject backTo = null)
    {
        // Inside the right end of the title plate: the same gap above, below and to its right (mirrors the ornament on the left).
        float side = PlateHeight - 2f * PlatePad;
        Button close = MakeButton(panel.transform, "Close Button", "X", new Vector2(side, side), ButtonRole.Neutral, menuButtonFontSize * 0.8f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.sizeDelta = new Vector2(side, side);
        cr.anchoredPosition = new Vector2(-(PlateMargin + PlatePad), -(PlateMargin + PlatePad));
        close.onClick.AddListener(() => ShowView(backTo != null ? backTo : mainPanel));
    }

    /// <summary>Adds the X (top right), grows the panel to fit and hides it until it is opened.</summary>
    private void FinishSectionPanel(GameObject panel, float y, GameObject backTo = null)
    {
        AddCloseX(panel, backTo);

        RectTransform rect = panel.GetComponent<RectTransform>();
        float needed = y + 30f;
        if (needed > rect.sizeDelta.y) rect.sizeDelta = new Vector2(rect.sizeDelta.x, needed);
        panel.SetActive(false);
    }

    /// <summary>How much narrower than the list every row under a section header is (the Replay tutorial button's width): toggles, choices, sliders and buttons all line up.</summary>
    private const float SettingsRowInset = 112f;

    private TMP_Text AddRowLabel(Transform parent, string text, float y, out RectTransform row)
    {
        // "(after restart)" / "(next start)" / "(requires restart)" is taken off the name and shown as a hover tip instead.
        bool needsRestart = false;
        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text, @"\s*\((after restart|next start|requires restart)\)\s*$",
                                                                                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) { text = text.Substring(0, m.Index); needsRestart = true; }
        GameObject go = new GameObject(text + " Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        row = go.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-SettingsRowInset, rowHeight);   // every row under a header is as wide as the Replay tutorial button
        row.anchoredPosition = new Vector2(0f, -y);

        TMP_Text label = MakeText(go.transform, "Label", text, rowFontSize * 0.55f, FontStyles.Normal);   // one size for every row so they line up
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = false;
        label.fontSize = rowFontSize * 0.55f;
        label.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;   // ONE line: a long name shrinks to fit instead of wrapping
#else
        label.enableWordWrapping = false;
#endif
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = new Vector2(0.62f, 1f);
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        if (needsRestart)
        {
            Image hit = go.AddComponent<Image>();   // invisible, only so the whole row reacts to the mouse
            hit.color = new Color(0f, 0f, 0f, 0f);
            AddSettingTip(go, restartTipText);
        }
        return label;
    }

    private RectTransform settingTipRect;
    private TMP_Text settingTipLabel;

    /// <summary>A small box next to the mouse while it is over 'target' (the Settings screen's own hover tip).</summary>
    private void AddSettingTip(GameObject target, string text)
    {
        if (settingTipRect == null)
        {
            GameObject box = new GameObject("Setting Tip", typeof(RectTransform), typeof(Image), typeof(PixelDevTip));
            box.transform.SetParent(canvasRoot.transform, false);
            Image bg = box.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            settingTipRect = box.GetComponent<RectTransform>();
            settingTipRect.anchorMin = settingTipRect.anchorMax = new Vector2(0.5f, 0.5f);
            settingTipLabel = MakeText(box.transform, "Text", "", rowFontSize * 0.6f, FontStyles.Normal);
            settingTipLabel.raycastTarget = false;
            PixelUIKit.Stretch(settingTipLabel.rectTransform);
            settingTipLabel.rectTransform.offsetMin = new Vector2(12f, 8f);
            settingTipLabel.rectTransform.offsetMax = new Vector2(-12f, -8f);
            PixelDevTip follow = box.GetComponent<PixelDevTip>();
            follow.canvasRect = canvasRoot.GetComponent<RectTransform>();
            follow.rect = settingTipRect;
            box.SetActive(false);
        }
        PixelHoverTip hover = target.AddComponent<PixelHoverTip>();
        hover.onEnter = () =>
        {
            settingTipLabel.text = text;
            Vector2 pref = settingTipLabel.GetPreferredValues(text, 520f, 0f);
            settingTipRect.sizeDelta = new Vector2(Mathf.Min(520f, pref.x) + 24f, pref.y + 16f);
            settingTipRect.gameObject.SetActive(true);
            settingTipRect.SetAsLastSibling();
        };
        hover.onExit = () => { if (settingTipRect != null) settingTipRect.gameObject.SetActive(false); };
    }

    private TMP_Text AddStatRow(Transform parent, string label, ref float y)
    {
        AddRowLabel(parent, label, y, out RectTransform row);

        TMP_Text value = MakeText(row, "Value", "", rowFontSize, FontStyles.Bold);
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.color = statValueColor;
        value.enableAutoSizing = true;
        value.fontSizeMax = rowFontSize;
        value.fontSizeMin = 14f;
        RectTransform vr = value.rectTransform;
        vr.anchorMin = new Vector2(0.4f, 0f);
        vr.anchorMax = Vector2.one;
        vr.offsetMin = vr.offsetMax = Vector2.zero;

        y += rowHeight + 6f;
        return value;
    }

    private Toggle AddToggleRow(Transform parent, string label, bool isOn, UnityEngine.Events.UnityAction<bool> onChanged, ref float y)
    {
        TMP_Text toggleLabel = AddRowLabel(parent, label, y, out RectTransform row);
        toggleLabel.rectTransform.anchorMax = Vector2.one;                                   // the name may run right up to the tick box
        toggleLabel.rectTransform.offsetMax = new Vector2(-(tickBoxSize + 14f), 0f);

        Toggle toggle = MakeTickBox(row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));   // at the row's right end
        toggle.isOn = isOn;
        toggle.onValueChanged.AddListener(onChanged);
        toggle.onValueChanged.AddListener(_ => PixelAudio.Play("ui_click"));

        y += rowHeight + 6f;
        return toggle;
    }

    /// <summary>
    /// A tick box (box + tick picture + Toggle) inside a row, anchored at 'anchor' with 'pivot'. The caller sets its value and listeners.
    /// Shared by the tick box rows (AddToggleRow) and the look rows' tick box (AddLookRow).
    /// </summary>
    private Toggle MakeTickBox(Transform row, Vector2 anchor, Vector2 pivot)
    {
        GameObject box = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row, false);
        Image bg = box.GetComponent<Image>();
        bg.color = tickBoxColor;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = anchor;
        br.pivot = pivot;
        br.sizeDelta = new Vector2(tickBoxSize, tickBoxSize);
        br.anchoredPosition = Vector2.zero;

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(box.transform, false);
        tick.GetComponent<Image>().sprite = PixelUIKit.TickSprite();
        tick.GetComponent<Image>().color = tickColor;
        tick.GetComponent<Image>().raycastTarget = false;
        RectTransform tkr = tick.GetComponent<RectTransform>();
        tkr.anchorMin = Vector2.zero;
        tkr.anchorMax = Vector2.one;
        tkr.offsetMin = new Vector2(tickBoxSize * 0.1f, tickBoxSize * 0.1f);
        tkr.offsetMax = new Vector2(-tickBoxSize * 0.1f, -tickBoxSize * 0.1f);

        Toggle toggle = box.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = tick.GetComponent<Image>();
        return toggle;
    }

    /// <summary>A small eyedropper button just left of a tick box (picks a colour from the screen).</summary>
    private void AddEyedropperButton(Toggle toggle, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform row = toggle.transform.parent as RectTransform;
        Button b = MakeButton(row, "Eyedropper Button", "", new Vector2(tickBoxSize, tickBoxSize), ButtonRole.Neutral, rowFontSize * 0.7f);
        RectTransform br = b.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(tickBoxSize, tickBoxSize);
        br.anchoredPosition = new Vector2(-(tickBoxSize + 10f), 0f);
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(b.transform, false);
        Image im = icon.GetComponent<Image>();
        im.sprite = PixelUIKit.EyedropperSprite();
        im.color = Color.white;
        im.raycastTarget = false;
        im.preserveAspect = true;
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(tickBoxSize * 0.15f, tickBoxSize * 0.15f);
        ir.offsetMax = new Vector2(-tickBoxSize * 0.15f, -tickBoxSize * 0.15f);
        b.onClick.AddListener(onClick);
        TMP_Text label = row.GetComponentInChildren<TMP_Text>();
        if (label != null) label.rectTransform.offsetMax = new Vector2(-(tickBoxSize * 2f + 24f), 0f);
    }

    private static Sprite cycleSprite;

    /// <summary>A small rainbow button just left of the eyedropper: shows (and switches) that the colour cycles by itself. Bright = cycling, dim = off.</summary>
    private void AddCycleButton(Toggle toggle, System.Func<bool> get, System.Action<bool> set)
    {
        RectTransform row = toggle.transform.parent as RectTransform;
        Button b = MakeButton(row, "Cycle Button", "", new Vector2(tickBoxSize, tickBoxSize), ButtonRole.Neutral, rowFontSize * 0.7f);
        RectTransform br = b.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(tickBoxSize, tickBoxSize);
        br.anchoredPosition = new Vector2(-(tickBoxSize * 2f + 20f), 0f);
        if (cycleSprite == null)
        {
            const int n = 24;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int y2 = 0; y2 < n; y2++)
                for (int x2 = 0; x2 < n; x2++)
                {
                    float dx = x2 - (n - 1) * 0.5f, dy = y2 - (n - 1) * 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a2 = Mathf.Clamp01((n * 0.5f - 1f - r) + 0.5f) * Mathf.Clamp01((r - n * 0.2f) + 0.5f);   // a ring
                    Color c = Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI), 1f), 0.8f, 1f);
                    c.a = a2;
                    tex.SetPixel(x2, y2, c);
                }
            tex.Apply();
            cycleSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(b.transform, false);
        Image im = icon.GetComponent<Image>();
        im.sprite = cycleSprite;
        im.raycastTarget = false;
        im.preserveAspect = true;
        RectTransform ir = icon.GetComponent<RectTransform>();
        ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(tickBoxSize * 0.12f, tickBoxSize * 0.12f);
        ir.offsetMax = new Vector2(-tickBoxSize * 0.12f, -tickBoxSize * 0.12f);
        System.Action refresh = () => { im.color = get() ? Color.white : new Color(1f, 1f, 1f, 0.3f); };
        b.onClick.AddListener(() => { set(!get()); refresh(); });
        refresh();
        settingsRefreshers.Add(refresh);
        TMP_Text label = row.GetComponentInChildren<TMP_Text>();
        if (label != null) label.rectTransform.offsetMax = new Vector2(-(tickBoxSize * 3f + 38f), 0f);
    }

    /// <summary>A label with a 0..1 slider on its right.</summary>
    /// <summary>
    /// A setting slider row (0..1 unless min / max are given; whole = whole numbers only). valueText = a small read-out of the value
    /// just left of the slider (e.g. "75°"), kept up to date as it moves. reread = the setting's value, read again (with the read-out)
    /// whenever the Settings screen opens or a save's settings load.
    /// </summary>
    private Slider AddSliderRow(Transform parent, string label, float value, UnityEngine.Events.UnityAction<float> onChanged, ref float y,
                                float min = 0f, float max = 1f, bool whole = false, System.Func<float, string> valueText = null, System.Func<float> reread = null)
    {
        AddRowLabel(parent, label, y, out RectTransform row);

        Slider slider = PixelUIKit.CreateSlider(row, "Slider", sliderTrackColor, sliderFillColor, sliderHandleColor);
        RectTransform sr = slider.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.62f, 0f);
        sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChanged);
        if (valueText != null)
        {
            TMP_Text read = MakeText(row, "Value", valueText(value), rowFontSize * 0.5f, FontStyles.Bold);
            read.alignment = TextAlignmentOptions.MidlineRight;
            read.color = statValueColor;
            read.raycastTarget = false;
            RectTransform vr = read.rectTransform;
            vr.anchorMin = new Vector2(0.45f, 0f);
            vr.anchorMax = new Vector2(0.6f, 1f);
            vr.offsetMin = vr.offsetMax = Vector2.zero;
            slider.onValueChanged.AddListener(v => read.text = valueText(v));
        }
        if (reread != null)
            settingsRefreshers.Add(() =>
            {
                slider.SetValueWithoutNotify(reread());
                TMP_Text shown = valueText != null ? row.Find("Value")?.GetComponent<TMP_Text>() : null;
                if (shown != null) shown.text = valueText(slider.value);
            });

        y += rowHeight + 6f;
        return slider;
    }
}
