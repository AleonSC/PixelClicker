// ---- System notes ----
// PixelPauseMenuSettings.cs
//   Part of PixelPauseMenu (partial class): the menu search (top right) and the Settings screen - building the list one section at a time, the foldable section headers, the layout / filter and the settings catalog, the toggle / drop-down / wide-button rows. The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    // ------------------------------------------------------------------
    // Menu search (top right of the pause menu): lists the menu's screens and the matching settings
    // ------------------------------------------------------------------

    /// <summary>A magnifying glass on the left of the search box, and smaller one-line text so the placeholder fits inside it.</summary>
    private static void StyleSearchBox(TMP_InputField field)
    {
        PixelUIKit.AddSearchIcon(field);
        foreach (TMP_Text t in new[] { field.textComponent, field.placeholder as TMP_Text })
        {
            if (t == null) continue;
            t.enableAutoSizing = true;
            t.fontSizeMax = Mathf.Max(12f, t.fontSize * 0.7f);
            t.fontSizeMin = 10f;
            t.overflowMode = TextOverflowModes.Truncate;
#if UNITY_2023_1_OR_NEWER
            t.textWrappingMode = TextWrappingModes.NoWrap;
#else
            t.enableWordWrapping = false;
#endif
        }
    }

    private TMP_InputField menuSearchField;
    private float menuSearchTop;
    private readonly List<GameObject> mainButtons = new List<GameObject>();
    private readonly List<GameObject> searchResults = new List<GameObject>();
    private List<MenuEntry> mainEntries;
    private string menuSearch = "";
    private string settingsFilter = "";
    private TMP_InputField settingsSearchField;

    private static string RowText(SettingsItem item)
    {
        TMP_Text label = item.rect != null ? item.rect.GetComponentInChildren<TMP_Text>(true) : null;
        return label != null ? label.text : "";
    }

    // The Floor and Sky rows (AddLookRow): searching for any floor / sky NAME finds them, and the menu search lists each matching look to pick at once.
    private readonly Dictionary<RectTransform, System.Func<IPixelLookSource>> lookRows = new Dictionary<RectTransform, System.Func<IPixelLookSource>>();

    /// <summary>Does this settings row match the search? Its label, or for the Floor / Sky rows the name of any of their looks.</summary>
    private bool RowMatches(SettingsItem item, string query)
    {
        if (RowText(item).ToLowerInvariant().Contains(query)) return true;
        if (item.rect == null || !lookRows.TryGetValue(item.rect, out System.Func<IPixelLookSource> source)) return false;
        IPixelLookSource look = source != null ? source() : null;
        if (look == null) return false;
        for (int i = 0; i < look.StyleCount; i++)
        {
            string n = look.StyleName(i);
            if (!string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains(query)) return true;
        }
        return false;
    }

    private void OnMenuSearchChanged(string text)
    {
        menuSearch = (text ?? "").Trim().ToLowerInvariant();
        bool searching = menuSearch.Length > 0;
        foreach (GameObject b in mainButtons) if (b != null) b.SetActive(!searching);
        foreach (GameObject r in searchResults) if (r != null) Destroy(r);
        searchResults.Clear();
        if (!searching || mainPanel == null) return;

        // Collect every result first, so the lines can be sized (and the settings list trimmed) to fit inside the panel.
        List<string> labels = new List<string>();
        List<Color> colors = new List<Color>();   // (used by role Custom results: the main menu's own colours)
        List<ButtonRole> roles = new List<ButtonRole>();
        List<UnityEngine.Events.UnityAction> actions = new List<UnityEngine.Events.UnityAction>();
        if (mainEntries != null)
            foreach (MenuEntry e in mainEntries)
                if (e.label.ToLowerInvariant().Contains(menuSearch)) { labels.Add(e.label); colors.Add(e.color); roles.Add(e.role); actions.Add(e.action); }
        int entryCount = labels.Count;

        // Every floor / sky whose NAME matches: a result that switches to it straight away (it stays highlighted as the one in use).
        int lookResults = 0;
        const int maxLookResults = 8;   // (a short search like 'a' matches dozens: the Settings search shows the rest)
        foreach (KeyValuePair<RectTransform, System.Func<IPixelLookSource>> lr in lookRows)
        {
            IPixelLookSource look = lr.Value != null ? lr.Value() : null;
            if (look == null || !look.Usable) continue;
            TMP_Text rowLabelText = lr.Key != null ? lr.Key.GetComponentInChildren<TMP_Text>(true) : null;
            string kind = rowLabelText != null ? rowLabelText.text : "Look";
            for (int i = 0; i < look.StyleCount; i++)
            {
                string n = look.StyleName(i);
                if (string.IsNullOrEmpty(n) || !n.ToLowerInvariant().Contains(menuSearch)) continue;
                if (lookResults++ >= maxLookResults) break;
                int index = i;
                IPixelLookSource target = look;
                labels.Add(kind + ": " + n + (look.Current == i ? "  (in use)" : ""));
                colors.Add(Color.clear); roles.Add(look.Current == i ? ButtonRole.Selected : ButtonRole.Neutral);
                actions.Add(() => { target.SetStyle(index); PixelAudio.Play("ui_click"); OnMenuSearchChanged(menuSearch); settingsRefreshersRun(); });
            }
        }
        entryCount = labels.Count;

        List<string> settingLabels = new List<string>();
        foreach (SettingsItem item in settingsItems)
        {
            if (item.isHeader || !item.wasActive) continue;
            string rowLabel = RowText(item);
            if (rowLabel.ToLowerInvariant().Contains(menuSearch)) settingLabels.Add(rowLabel);
        }

        float avail = (mainPanel.transform as RectTransform).rect.height - menuSearchTop - 24f;
        float minHeight = 38f;
        int maxLines = Mathf.Max(1, Mathf.FloorToInt((avail + menuButtonSpacing) / (minHeight + menuButtonSpacing)));
        int settingsRoom = Mathf.Max(0, maxLines - entryCount);
        int shownSettings = settingLabels.Count <= settingsRoom ? settingLabels.Count : Mathf.Max(0, settingsRoom - 1);   // one line is kept for '+ N more'
        for (int k = 0; k < shownSettings; k++)
        {
            labels.Add(settingsText + " > " + settingLabels[k]); colors.Add(Color.clear); roles.Add(ButtonRole.Neutral); actions.Add(OpenFilteredSettings);
        }
        if (settingLabels.Count > shownSettings)
        {
            labels.Add("+ " + (settingLabels.Count - shownSettings) + " more in " + settingsText); colors.Add(Color.clear); roles.Add(ButtonRole.Neutral); actions.Add(OpenFilteredSettings);
        }

        float height = Mathf.Clamp(avail / Mathf.Max(1, labels.Count) - menuButtonSpacing, minHeight, Mathf.Min(menuButtonSize.y, 64f));
        float y = menuSearchTop;
        for (int k = 0; k < labels.Count; k++)
        {
            Button b = MakeButton(mainPanel.transform, labels[k] + " Result", labels[k], new Vector2(mainPanelWidth - 80f, height), roles[k], colors[k], menuButtonFontSize * (height < 50f ? 0.7f : 0.85f));
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
            b.onClick.AddListener(actions[k]);
            searchResults.Add(b.gameObject);
            y += height + menuButtonSpacing;
        }

        if (labels.Count == 0)
        {
            TMP_Text none = MakeText(mainPanel.transform, "No Results", "NOTHING FOUND", rowFontSize, FontStyles.Normal);
            none.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
            RectTransform nr = none.rectTransform;
            nr.anchorMin = nr.anchorMax = nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(mainPanelWidth - 80f, rowHeight);
            nr.anchoredPosition = new Vector2(0f, -y);
            searchResults.Add(none.gameObject);
        }
    }

    private void OpenFilteredSettings()
    {
        settingsFilter = menuSearch;
        if (settingsSearchField != null) settingsSearchField.SetTextWithoutNotify(menuSearch);
        ShowView(settingsPanel);
        LayoutSettings();
        if (settingsScroll != null && settingsScroll.content != null) settingsScroll.content.anchoredPosition = Vector2.zero;
    }

    private void ClearMenuSearch()
    {
        settingsFilter = "";
        if (settingsSearchField != null) settingsSearchField.SetTextWithoutNotify("");
        if (menuSearchField != null && menuSearchField.text.Length > 0) menuSearchField.text = "";   // notifies: the main buttons come back
        else if (menuSearch.Length > 0) OnMenuSearchChanged("");
    }

    private void BuildSettingsPanel()
    {
        settingsPanel = BuildSectionPanel("Settings Panel", settingsTitle, out float top);
        settingsRefreshers.Clear();
        lookRows.Clear();

        // Search bar under the title: filters the rows in place (the same filter the main menu's search hands over).
        float searchH = rowHeight * 0.9f;
        settingsSearchField = PixelUIKit.CreateInputField(font, settingsPanel.transform, "Settings Search", new Vector2(100f, searchH),
                                                          tickBoxColor, textColor, rowFontSize * 0.85f, settingsSearchPlaceholder);
        RectTransform ssr = settingsSearchField.GetComponent<RectTransform>();
        ssr.anchorMin = new Vector2(0f, 1f); ssr.anchorMax = new Vector2(1f, 1f); ssr.pivot = new Vector2(0.5f, 1f);
        ssr.sizeDelta = new Vector2(-80f, searchH);
        ssr.anchoredPosition = new Vector2(0f, -top);
        StyleSearchBox(settingsSearchField);
        settingsSearchField.onValueChanged.AddListener(text =>
        {
            settingsFilter = (text ?? "").Trim().ToLowerInvariant();
            LayoutSettings();
            if (settingsScroll != null && settingsScroll.content != null) settingsScroll.content.anchoredPosition = Vector2.zero;
        });
        top += searchH + 12f;

        // Everything scrolls: there are more rows than fit between the black bars.
        float footerH = rowHeight * 0.8f, footerGap = 6f, footer = 12f + 2f * footerH + footerGap;   // two slim buttons under the list (Replay tutorial, Change keybinds)
        float viewHeight = FitViewHeight(settingsViewHeight, top + footer);
        ScrollRect scroll = PixelUIKit.CreateScrollView(settingsPanel.transform, "Settings List", scrollbarColor, 12f, rowHeight,
                                                        out RectTransform content, out GameObject bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(0f, viewHeight);
        vr.anchoredPosition = new Vector2(0f, -top);

        Transform list = content;
        float y = 0f;

        AddGameplaySettings(list, ref y);
        AddInterfaceSettings(list, ref y);
        AddAccessibilitySettings(list, ref y);
        AddDisplaySettings(list, ref y);
        AddAudioSettings(list, ref y);

        settingsScroll = scroll;
        settingsBar = bar;
        settingsListHeight = viewHeight;
        CaptureSettingsLayout(content, y);
        LayoutSettings();
        BuildKeysPanel();
        BuildPlaylistPanel();
        // The two fixed buttons at the very bottom of the window: as wide as the section headers, not tall.
        void FooterButton(string label, UnityEngine.Events.UnityAction onClick, float offset)
        {
            Button fb = MakeButton(settingsPanel.transform, label + " Button", label, new Vector2(10f, footerH), ButtonRole.Neutral, rowFontSize * 0.7f);
            RectTransform fr = fb.GetComponent<RectTransform>();
            fr.anchorMin = new Vector2(0f, 1f); fr.anchorMax = new Vector2(1f, 1f); fr.pivot = new Vector2(0.5f, 1f);
            fr.sizeDelta = new Vector2(-96f, footerH);
            fr.anchoredPosition = new Vector2(0f, -(top + viewHeight + 12f + offset));
            fb.onClick.AddListener(onClick);
        }
        FooterButton(replayTutorialText, () => { PixelHints.ReplayTutorial(); SetPaused(false); }, 0f);   // (so the tips show over the game)
        FooterButton(keybindsButtonText, () => ShowView(keysPanel), footerH + footerGap);
        FinishSectionPanel(settingsPanel, top + viewHeight + footer);
        AddCubiePeek(settingsPanel);
        AddCubiePeek(keysPanel);
    }

    /// <summary>The Gameplay section: numbers, shop, background / pause, tips, day / night, autosave, the floor and the sky.</summary>
    private void AddGameplaySettings(Transform list, ref float y)
    {
        // ---- Gameplay ----
        AddSettingsSectionHeader(list, headerGameplay, ref y);
        abbreviateToggle = AddToggleRow(list, abbreviateLabel.Replace(" (1.2K)", ""), PixelClicker.AbbreviateNumbers,
                                        on => PixelClicker.AbbreviateNumbers = on, ref y);
        hidePurchasedToggle = AddToggleRow(list, hidePurchasedLabel, PixelShop.HidePurchased,
                                           on => PixelShop.HidePurchased = on, ref y);
        backgroundToggle = AddToggleRow(list, runInBackgroundLabel, clicker != null && clicker.RunInBackground,
                                        on => { if (clicker != null) clicker.RunInBackground = on; }, ref y);
        pauseStopsToggle = AddToggleRow(list, pauseStopsLabel, pauseStopsGame, on =>
        {
            pauseStopsGame = on;
            PixelPrefs.SetInt(PrefPauseStops, on ? 1 : 0);
            ApplyFreeze(); // takes effect right away, even though the menu is open
        }, ref y);
        AddSettingToggle(list, didYouKnowLabel, () => PixelDidYouKnow.Enabled, on => PixelDidYouKnow.Enabled = on, ref y);
        AddSettingToggle(list, dayNightLabel, () => PixelDayNight.Enabled, on => PixelDayNight.Enabled = on, ref y);
        AddDropdownRow(list, dayNightLengthLabel, PixelDayNight.LengthNames, () => PixelDayNight.LengthChoice, v => PixelDayNight.LengthChoice = v, ref y);
        if (saveGame != null)
        {
            AddSettingToggle(list, autoSaveMessageLabel, () => saveGame.AutoSaveMessageNow,
                             on => PixelSaveGame.AutoSaveMessageChoice = on ? 1 : 0, ref y);
            AddDropdownRow(list, autoSaveLabel, PixelSaveGame.AutoSaveNames, CurrentAutoSaveIndex, v => PixelSaveGame.AutoSaveChoice = v, ref y);
        }
        // The floor: its style + the tick box that switches it on / off in one entry, then what shows when it is off; then the sky.
        AddLookRow(list, floorStyleLabel, () => PixelFloor.Instance, ref y, popout: true,
                   getToggle: () => PixelTitleWanderers.DisplayFloor, setToggle: on => PixelTitleWanderers.DisplayFloor = on);
        AddDropdownRow(list, floorOffModeLabel, PixelFloor.OffModeNames, () => PixelFloor.OffMode, v => PixelFloor.OffMode = v, ref y);
        AddLookRow(list, skyStyleLabel, () => PixelSkybox.Instance, ref y, popout: true);
    }

    /// <summary>The Interface section: tips, logs, popups, block tips, aura, Next up, menus, bars and the UI / button colours.</summary>
    private void AddInterfaceSettings(Transform list, ref float y)
    {
        // ---- Interface ----
        AddSettingsSectionHeader(list, headerInterface, ref y);
        AddSettingToggle(list, tipsLabel, () => PixelHints.TipsEnabled, on => PixelHints.TipsEnabled = on, ref y);
        AddSettingToggle(list, eventLogLabel, () => PixelHints.EventLogEnabled, on => PixelHints.EventLogEnabled = on, ref y);
        AddSettingToggle(list, popupsLabel, () => PixelUI.PopupsEnabled, on => PixelUI.PopupsEnabled = on, ref y);
        AddDropdownRow(list, blockTipsLabel, PixelBlockTooltip.ModeNames, () => PixelBlockTooltip.Mode, v => PixelBlockTooltip.Mode = v, ref y);   // Full / Minimal / Off (Alt cycles it in the game)
        Toggle auraToggle = AddToggleRow(list, clickerAuraLabel, PixelClickerAura.Enabled, on => PixelClickerAura.Enabled = on, ref y);
        settingsRefreshers.Add(() => auraToggle.SetIsOnWithoutNotify(PixelClickerAura.Enabled));
        AddEyedropperButton(auraToggle, () => PixelEyedropper.Begin(c => { PixelClickerAura.SetCustomColor(c); PixelClickerAura.Mode = 1; settingsRefreshersRun(); }));   // pick any colour on screen for the aura
        AddDropdownRow(list, clickerAuraColorLabel, new[] { "Match pixel", "Custom (eyedropper)", "Rainbow" }, () => PixelClickerAura.Mode, v => PixelClickerAura.Mode = v, ref y);
        AddSettingToggle(list, nextUpLabel, () => PixelNextUp.Enabled, on => PixelNextUp.Enabled = on, ref y);
        AddSettingToggle(list, dropGuideLabel, () => PixelDisplaySettings.DropGuide, on => PixelDisplaySettings.DropGuide = on, ref y);
        AddSettingToggle(list, fancyMenuLabel, () => PixelMenuFx.Enabled, on => PixelMenuFx.Enabled = on, ref y);
        Toggle buttonColorToggle = AddToggleRow(list, buttonColorLabel, PixelUIKit.ButtonColorOn, on => PixelUIKit.ButtonColorOn = on, ref y);
        settingsRefreshers.Add(() => buttonColorToggle.SetIsOnWithoutNotify(PixelUIKit.ButtonColorOn));
        AddEyedropperButton(buttonColorToggle, () => PixelEyedropper.Begin(c => { PixelUIKit.SetButtonColorFromColor(c); settingsRefreshersRun(); }));   // pick the buttons' colour from anywhere on screen
        AddSettingToggle(list, cameraIntroLabel, () => PixelCameraIntro.Enabled, on => PixelCameraIntro.Enabled = on, ref y);
        AddSettingToggle(list, studioIntroLabel, () => PixelStudioIntro.Enabled, on => PixelStudioIntro.Enabled = on, ref y);
        AddDropdownRow(list, buttonModeLabel, ButtonModeNames(), () => PixelHud.ButtonMode, v => PixelHud.ButtonMode = v, ref y);
        Toggle glowToggle = AddToggleRow(list, barGlowLabel, PixelHud.BarGlow, on => PixelHud.BarGlow = on, ref y);
        AddDropdownRow(list, barStyleLabel, PixelBarStyle.Names, () => PixelBarStyle.Current, v => PixelBarStyle.Current = v, ref y);
        settingsRefreshers.Add(() => glowToggle.SetIsOnWithoutNotify(PixelHud.BarGlow));
        AddEyedropperButton(glowToggle, () => PixelEyedropper.Begin(c => { PixelHud.SetGlowFromColor(c); settingsRefreshersRun(); }));
        AddCycleButton(glowToggle, () => PixelHud.GlowCycle, on => PixelHud.GlowCycle = on);   // a small rainbow button beside the eyedropper: the glow colour cycles
        Toggle uiColorToggle = AddToggleRow(list, uiColorLabel, PixelUIKit.UiColorOn, on => PixelUIKit.UiColorOn = on, ref y);
        settingsRefreshers.Add(() => uiColorToggle.SetIsOnWithoutNotify(PixelUIKit.UiColorOn));
        AddEyedropperButton(uiColorToggle, () => PixelEyedropper.Begin(c => { PixelUIKit.SetUiColorFromColor(c); settingsRefreshersRun(); }));   // pick the windows' colour from anywhere on screen
        Toggle barColorToggle = AddToggleRow(list, barColorLabel, PixelHud.BarColorOn, on => PixelHud.BarColorOn = on, ref y);
        settingsRefreshers.Add(() => barColorToggle.SetIsOnWithoutNotify(PixelHud.BarColorOn));
        AddEyedropperButton(barColorToggle, () => PixelEyedropper.Begin(c => { PixelHud.SetBarColorFromColor(c); settingsRefreshersRun(); }));   // pick the black bars' colour from anywhere on screen
    }

    /// <summary>The Accessibility section: cube rotation / pulsing, colour blind, popup / log / UI sizes, and the two reset buttons.</summary>
    private void AddAccessibilitySettings(Transform list, ref float y)
    {
        // ---- Accessibility ----
        AddSettingsSectionHeader(list, headerAccessibility, ref y);
        rotationToggle = AddToggleRow(list, rotationLabel, clicker == null || clicker.AllowRotation,
                                      on => { if (clicker != null) clicker.AllowRotation = on; }, ref y);
        pulsingToggle = AddToggleRow(list, pulsingLabel, clicker == null || clicker.AllowPulsing,
                                     on => { if (clicker != null) clicker.AllowPulsing = on; }, ref y);
        AddSettingToggle(list, colorBlindLabel, () => PixelDisplaySettings.ColorBlind, on => PixelDisplaySettings.ColorBlind = on, ref y);
        AddDropdownRow(list, popupSizeLabel, PixelUI.PopupSizeNames, () => PixelUI.PopupSizeChoice, v => PixelUI.PopupSizeChoice = v, ref y);
        AddDropdownRow(list, eventLogSizeLabel, PixelHints.LogSizeNames, () => PixelHints.LogSizeChoice, v => PixelHints.LogSizeChoice = v, ref y);
        string[] scaleNames = new string[PixelDisplaySettings.UIScaleChoices.Length];
        for (int i = 0; i < scaleNames.Length; i++) scaleNames[i] = Mathf.RoundToInt(PixelDisplaySettings.UIScaleChoices[i] * 100f) + "%";
        AddDropdownRow(list, uiScaleLabel, scaleNames, NearestUiScaleIndex, v => PixelDisplaySettings.SavedUIScale = PixelDisplaySettings.UIScaleChoices[v], ref y);
        AddWideButtonRow(list, resetPopupPinText, () => { PixelAchievements ach = PixelFind.First<PixelAchievements>(); if (ach != null) ach.ResetPopupPin(); }, ref y);
        AddWideButtonRow(list, resetTipsLocationText, () => { PixelDidYouKnow tips = PixelFind.First<PixelDidYouKnow>(); if (tips != null) tips.ResetLocation(); }, ref y);   // (last in this section)
    }

    /// <summary>The Display section: filter + strength, the orbit camera's field of view, VSync, fullscreen and resolution.</summary>
    private void AddDisplaySettings(Transform list, ref float y)
    {
        // ---- Display ----
        AddSettingsSectionHeader(list, headerDisplay, ref y);
        AddDropdownRow(list, filterLabel, PixelFilters.Names, () => PixelFilters.Choice, v => PixelFilters.Choice = v, ref y);
        Slider filterSlider = AddSliderRow(list, filterStrengthLabel, PixelFilters.Strength, v => PixelFilters.Strength = v, ref y);
        settingsRefreshers.Add(() => filterSlider.SetValueWithoutNotify(PixelFilters.Strength));
        // The orbit camera's field of view (the default camera; Top down too). 0 saved = the normal view's own, shown as that number.
        System.Func<float> fovNow = () => PixelOrbitCamera.FieldOfView > 0f ? PixelOrbitCamera.FieldOfView : Mathf.Round(PixelOrbitCamera.NormalFov);
        AddSliderRow(list, orbitFovLabel, fovNow(), v => PixelOrbitCamera.FieldOfView = v, ref y,
                     PixelOrbitCamera.MinFov, PixelOrbitCamera.MaxFov, true, v => Mathf.RoundToInt(v).ToString(), fovNow);
        AddSettingToggle(list, vsyncLabel, () => PixelDisplaySettings.VSync, on => PixelDisplaySettings.VSync = on, ref y);
        AddSettingToggle(list, fullscreenLabel, () => PixelDisplaySettings.Fullscreen, on => PixelDisplaySettings.Fullscreen = on, ref y);
        List<Vector2Int> resolutions = PixelDisplaySettings.Resolutions;
        string[] resolutionNames = new string[resolutions.Count];
        for (int i = 0; i < resolutionNames.Length; i++) resolutionNames[i] = resolutions[i].x + " x " + resolutions[i].y;
        AddDropdownRow(list, resolutionLabel, resolutionNames, () => Mathf.Max(0, resolutions.IndexOf(PixelDisplaySettings.Resolution)),
                       v => PixelDisplaySettings.Resolution = resolutions[v], ref y);
    }

    /// <summary>The Audio section (only when the sound system exists): the mute boxes, the three volume sliders and the custom playlist button.</summary>
    private void AddAudioSettings(Transform list, ref float y)
    {
        // ---- Audio: the mute box, then three volume sliders (the sound system is added by Start, which runs first) ----
        PixelAudio audio = PixelFind.First<PixelAudio>();
        if (audio != null)
        {
            AddSettingsSectionHeader(list, headerAudio, ref y);
            muteToggle = AddToggleRow(list, muteLabel, audio.Muted, on => audio.Muted = on, ref y);
            muteMusicToggle = AddToggleRow(list, muteMusicLabel, audio.MusicMuted, on => audio.MusicMuted = on, ref y);
            AddSettingToggle(list, uiClicksLabel, () => PixelAudio.UiClicks, on => PixelAudio.UiClicks = on, ref y);
            masterSlider = AddSliderRow(list, masterVolumeLabel, audio.MasterVolume, v => audio.MasterVolume = v, ref y);
            effectsSlider = AddSliderRow(list, effectsVolumeLabel, audio.EffectsVolume, v => audio.EffectsVolume = v, ref y);
            musicSlider = AddSliderRow(list, musicVolumeLabel, audio.MusicVolume, v => audio.MusicVolume = v, ref y);
            if (audio.MusicTrackCount > 1) AddWideButtonRow(list, customPlaylistText, () => ShowView(playlistPanel), ref y);
        }
    }

    // ---- Settings sections: a heading you click folds / unfolds the rows under it (all start folded) ----
    private ScrollRect settingsScroll;
    private GameObject settingsBar;
    private float settingsListHeight, settingsTotalHeight;
    private readonly HashSet<string> expandedSettingsSections = new HashSet<string>();
    private readonly Dictionary<RectTransform, string> settingsHeaders = new Dictionary<RectTransform, string>();
    private readonly List<SettingsItem> settingsItems = new List<SettingsItem>();

    private class SettingsItem
    {
        public RectTransform rect;
        public string section;          // the heading it sits under (null = above the first heading: always shown)
        public bool isHeader, wasActive;
        public float top, extent;       // where it was built, and how far down to the next item (its own height + the gap)
    }

    private void AddSettingsSectionHeader(Transform parent, string text, ref float y)
    {
        y += 8f;
        float h = rowHeight * 0.8f;
        Button b = MakeButton(parent, text + " Header", "", new Vector2(10f, h), ButtonRole.Neutral, rowFontSize * 0.9f);
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-96f, h);   // a little narrower than the buttons (-80)
        rt.anchoredPosition = new Vector2(0f, -y);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        t.color = statValueColor;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.rectTransform.offsetMin = new Vector2(18f, 0f);
        t.rectTransform.offsetMax = new Vector2(-10f, 0f);
        settingsHeaders[rt] = text;
        b.onClick.AddListener(() =>
        {
            if (!expandedSettingsSections.Remove(text)) expandedSettingsSections.Add(text);
            LayoutSettings();
        });
        y += h + 4f;
    }

    /// <summary>Remembers where every settings row was built (they are laid out top to bottom in creation order).</summary>
    private void CaptureSettingsLayout(RectTransform content, float totalHeight)
    {
        settingsItems.Clear();
        settingsTotalHeight = totalHeight;
        string section = null;
        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform rt = content.GetChild(i) as RectTransform;
            if (rt == null) continue;
            SettingsItem item = new SettingsItem { rect = rt, top = -rt.anchoredPosition.y, wasActive = rt.gameObject.activeSelf };
            if (settingsHeaders.TryGetValue(rt, out string name)) { item.isHeader = true; section = name; item.section = name; }
            else item.section = section;
            settingsItems.Add(item);
        }
        settingsItems.Sort((a, b) => a.top.CompareTo(b.top));
        for (int i = 0; i < settingsItems.Count; i++)
        {
            float next = i + 1 < settingsItems.Count ? settingsItems[i + 1].top : totalHeight;
            settingsItems[i].extent = Mathf.Max(0f, next - settingsItems[i].top);
        }
    }

    /// <summary>Hides the rows of folded sections and closes the gaps they leave.</summary>
    private void LayoutSettings()
    {
        float removed = 0f;
        bool filtering = settingsFilter.Length > 0;
        HashSet<string> sectionsWithMatch = new HashSet<string>();
        if (filtering)
            foreach (SettingsItem it in settingsItems)
                if (!it.isHeader && it.wasActive && it.section != null && RowMatches(it, settingsFilter)) sectionsWithMatch.Add(it.section);
        foreach (SettingsItem item in settingsItems)
        {
            bool shown = item.isHeader || item.section == null || expandedSettingsSections.Contains(item.section);
            if (filtering)   // a search shows just the matching rows (and their headings), whatever is folded
                shown = item.isHeader ? sectionsWithMatch.Contains(item.section) : RowMatches(item, settingsFilter);
            if (item.isHeader)
            {
                TMP_Text label = item.rect.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = (filtering || expandedSettingsSections.Contains(item.section) ? "-  " : "+  ") + item.section;
            }
            if (!shown) { item.rect.gameObject.SetActive(false); removed += item.extent; continue; }
            if (item.wasActive && !item.rect.gameObject.activeSelf) item.rect.gameObject.SetActive(true);
            Vector2 p = item.rect.anchoredPosition;
            item.rect.anchoredPosition = new Vector2(p.x, -(item.top - removed));
        }
        if (settingsScroll != null) PixelUIKit.UpdateScrollView(settingsScroll, settingsBar, settingsTotalHeight - removed, settingsListHeight);
    }

    /// <summary>One row of the Settings screen as the Game Database lists it (read from the built rows, so labels and choices are never typed twice).</summary>
    public struct SettingInfo
    {
        public string section, label, kind, current;
        public string[] choices;        // drop-downs: every choice (null for other rows)
        public IPixelLookSource look;   // the Floor / Sky rows: the looks they pick from (null for other rows)
    }

    /// <summary>Every row of the Settings screen (section, label, what kind of control, its choices and current value), in screen order.</summary>
    public List<SettingInfo> SettingsCatalog()
    {
        List<SettingInfo> list = new List<SettingInfo>();
        foreach (SettingsItem item in settingsItems)
        {
            if (item.isHeader || !item.wasActive || item.rect == null) continue;
            SettingInfo info = new SettingInfo { section = item.section, label = RowText(item) };
            if (string.IsNullOrEmpty(info.label)) continue;
            TMP_Dropdown dd = item.rect.GetComponentInChildren<TMP_Dropdown>(true);
            Slider slider = item.rect.GetComponentInChildren<Slider>(true);
            Toggle toggle = item.rect.GetComponentInChildren<Toggle>(true);
            if (lookRows.TryGetValue(item.rect, out System.Func<IPixelLookSource> source))
            {
                info.kind = "Look picker";
                info.look = source != null ? source() : null;
                if (info.look != null && info.look.Current >= 0 && info.look.Current < info.look.StyleCount) info.current = info.look.StyleName(info.look.Current);
            }
            else if (dd != null)
            {
                info.kind = "Drop-down";
                info.choices = new string[dd.options.Count];
                for (int i = 0; i < dd.options.Count; i++) info.choices[i] = dd.options[i].text;
                if (dd.value >= 0 && dd.value < dd.options.Count) info.current = dd.options[dd.value].text;
            }
            else if (slider != null)
            {
                info.kind = "Slider";
                info.current = slider.maxValue <= 1.0001f ? Mathf.RoundToInt(slider.value * 100f) + "%" : slider.value.ToString("0.##");
            }
            else if (toggle != null)
            {
                info.kind = "Tick box";
                info.current = toggle.isOn ? "On" : "Off";
            }
            else info.kind = "Button";
            list.Add(info);
        }
        return list;
    }

    private int NearestUiScaleIndex()
    {
        float saved = PixelDisplaySettings.SavedUIScale;
        int best = 0;
        for (int i = 1; i < PixelDisplaySettings.UIScaleChoices.Length; i++)
            if (Mathf.Abs(PixelDisplaySettings.UIScaleChoices[i] - saved) < Mathf.Abs(PixelDisplaySettings.UIScaleChoices[best] - saved)) best = i;
        return best;
    }

    private int CurrentAutoSaveIndex()
    {
        if (PixelSaveGame.AutoSaveChoice >= 0) return PixelSaveGame.AutoSaveChoice;
        // Not chosen yet: show the choice closest to the Inspector's interval.
        float inspector = saveGame != null ? saveGame.InspectorAutoSaveSeconds : 0f;
        int best = 0;
        for (int i = 1; i < PixelSaveGame.AutoSaveChoices.Length; i++)
            if (Mathf.Abs(PixelSaveGame.AutoSaveChoices[i] - inspector) < Mathf.Abs(PixelSaveGame.AutoSaveChoices[best] - inspector)) best = i;
        return best;
    }

    /// <summary>A tick box that reads / writes a setting; it re-reads the setting whenever the screen is shown.</summary>
    private static string[] ButtonModeNames() => new[] { "Peeking", "Always showing", "Hidden" };

    private void AddSettingToggle(Transform parent, string label, System.Func<bool> get, System.Action<bool> set, ref float y)
    {
        Toggle toggle = AddToggleRow(parent, label, get(), on => set(on), ref y);
        settingsRefreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
    }

    /// <summary>A label with a drop-down list on the right (for settings with many options).</summary>
    private void AddDropdownRow(Transform parent, string label, string[] options, System.Func<int> get, System.Action<int> set, ref float y)
    {
        TMP_Text rowLabel = AddRowLabel(parent, label, y, out RectTransform row);
        rowLabel.rectTransform.anchorMax = new Vector2(0.6f, 1f);
        rowLabel.rectTransform.offsetMax = new Vector2(-14f, 0f);
        TMP_Dropdown dd = PixelUIKit.CreateDropdown(font, row, label + " Dropdown", new Vector2(10f, rowHeight * 0.88f), tickBoxColor, tickBoxColor, Color.white, rowFontSize * 0.8f);
        RectTransform dr = dd.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.62f, 0.06f);
        dr.anchorMax = new Vector2(1f, 0.94f);
        dr.offsetMin = dr.offsetMax = Vector2.zero;
        dd.ClearOptions();
        dd.AddOptions(new System.Collections.Generic.List<string>(options));
        System.Action refresh = () => dd.SetValueWithoutNotify(options.Length == 0 ? 0 : Mathf.Clamp(get(), 0, options.Length - 1));
        dd.onValueChanged.AddListener(v => set(v));
        refresh();
        settingsRefreshers.Add(refresh);
        y += rowHeight + 6f;
    }

    /// <summary>A full-width button as a row of the settings list (opens a sub-screen).</summary>
    private void AddWideButtonRow(Transform parent, string label, UnityEngine.Events.UnityAction onClick, ref float y)
    {
        Button b = MakeButton(parent, label + " Button", label, new Vector2(10f, rowHeight), ButtonRole.Neutral, rowFontSize * 0.7f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-SettingsRowInset, rowHeight);
        r.anchoredPosition = new Vector2(0f, -y);
        b.onClick.AddListener(onClick);
        y += rowHeight + 6f;
    }
}
