// ---- System notes ----
// PixelPauseMenuKeysPlaylist.cs
//   Part of PixelPauseMenu (partial class): the Key bindings screen (BuildKeysPanel, key capture) and the Custom playlist screen (Settings > Audio: the song checklist with preview buttons). The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    // ------------------------------------------------------------------
    // Custom playlist (Settings > Audio): a checklist of the songs
    // ------------------------------------------------------------------

    private void BuildPlaylistPanel()
    {
        PixelAudio audio = PixelFind.First<PixelAudio>();
        if (audio == null || audio.MusicTrackCount < 2) return;
        playlistPanel = BuildSectionPanel("Playlist Panel", playlistPanelTitle, out float top);
        playlistToggles.Clear();

        TMP_Text hint = MakeText(playlistPanel.transform, "Hint", playlistHintText, rowFontSize * 0.5f, FontStyles.Normal);
        hint.color = new Color(1f, 1f, 1f, 0.7f);
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(-80f, rowHeight * 0.8f);
        hr.anchoredPosition = new Vector2(0f, -top);
        top += rowHeight * 0.8f + 8f;

        float footerH = rowHeight * 0.8f, footer = 12f + footerH;
        float viewHeight = FitViewHeight(settingsViewHeight, top + footer);
        ScrollRect scroll = PixelUIKit.CreateScrollView(playlistPanel.transform, "Playlist List", scrollbarColor, 12f, rowHeight, out RectTransform content, out GameObject bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f); vr.anchorMax = new Vector2(1f, 1f); vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(0f, viewHeight);
        vr.anchoredPosition = new Vector2(0f, -top);

        float y = 0f;
        for (int i = 0; i < audio.MusicTrackCount; i++)
        {
            int index = i;
            Toggle t = null;
            t = AddToggleRow(content, audio.MusicTrackName(i), audio.IsTrackEnabled(i), on =>
            {
                PixelAudio a = PixelAudio.Instance;
                if (a != null && !a.SetTrackEnabled(index, on)) t.SetIsOnWithoutNotify(true);   // the last ticked song stays ticked
            }, ref y);
            playlistToggles.Add(t);
            AddPreviewButton(t, index);
        }
        PixelUIKit.UpdateScrollView(scroll, bar, y, viewHeight);

        Button all = MakeButton(playlistPanel.transform, "Select All Button", playlistAllText, new Vector2(10f, footerH), ButtonRole.Neutral, rowFontSize * 0.7f);
        RectTransform ar = all.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(0f, 1f); ar.anchorMax = new Vector2(1f, 1f); ar.pivot = new Vector2(0.5f, 1f);
        ar.sizeDelta = new Vector2(-96f, footerH);
        ar.anchoredPosition = new Vector2(0f, -(top + viewHeight + 12f));
        all.onClick.AddListener(() =>
        {
            if (PixelAudio.Instance != null) PixelAudio.Instance.EnableAllTracks();
            RefreshPlaylistToggles();
        });
        FinishSectionPanel(playlistPanel, top + viewHeight + footer, settingsPanel);
    }

    private readonly List<Image> previewButtons = new List<Image>();

    /// <summary>A small play button just left of a song's tick box: plays a short snippet of it (again = stops).</summary>
    private void AddPreviewButton(Toggle toggle, int track)
    {
        RectTransform row = toggle.transform.parent as RectTransform;
        Button b = MakeButton(row, "Preview Button", "", new Vector2(tickBoxSize, tickBoxSize), ButtonRole.Neutral, rowFontSize * 0.7f);
        RectTransform br = b.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(tickBoxSize, tickBoxSize);
        br.anchoredPosition = new Vector2(-(tickBoxSize + 10f), 0f);
        PixelUIKit.UseArrowGlyph(b, false);   // a right-pointing triangle = play
        b.onClick.AddListener(() => { if (PixelAudio.Instance != null) PixelAudio.Instance.PreviewTrack(track); RefreshPreviewButtons(); });
        TMP_Text label = row.GetComponentInChildren<TMP_Text>();
        if (label != null) label.rectTransform.offsetMax = new Vector2(-(tickBoxSize * 2f + 24f), 0f);
        while (previewButtons.Count <= track) previewButtons.Add(null);
        previewButtons[track] = b.GetComponent<Image>();
    }

    private void RefreshPreviewButtons()
    {
        int playing = PixelAudio.Instance != null ? PixelAudio.Instance.PreviewingTrack : -1;
        for (int i = 0; i < previewButtons.Count; i++)
            if (previewButtons[i] != null) PixelUIKit.SetRole(previewButtons[i], i == playing ? ButtonRole.Selected : ButtonRole.Neutral);
    }

    private void Update_PlaylistPreview()
    {
        if (playlistPanel == null) return;
        if (!playlistPanel.activeInHierarchy)
        {
            if (PixelAudio.Instance != null && PixelAudio.Instance.PreviewingTrack >= 0) PixelAudio.Instance.StopPreview();   // leaving the checklist ends a snippet
            return;
        }
        int now = PixelAudio.Instance != null ? PixelAudio.Instance.PreviewingTrack : -1;
        if (now != shownPreview) { shownPreview = now; RefreshPreviewButtons(); }
    }

    private int shownPreview = -1;

    private void RefreshPlaylistToggles()
    {
        PixelAudio a = PixelAudio.Instance;
        if (a == null) return;
        for (int i = 0; i < playlistToggles.Count; i++)
            if (playlistToggles[i] != null) playlistToggles[i].SetIsOnWithoutNotify(a.IsTrackEnabled(i));
    }

    // ------------------------------------------------------------------
    // Key bindings
    // ------------------------------------------------------------------

    private void BuildKeysPanel()
    {
        keysPanel = BuildSectionPanel("Key Bindings Panel", keyBindingsPanelTitle, out float y);
        RectTransform keysRect = keysPanel.GetComponent<RectTransform>();
        keysRect.sizeDelta = new Vector2(Mathf.Max(keysRect.sizeDelta.x, keyBindingsPanelWidth > 0f ? keyBindingsPanelWidth : 1150f), keysRect.sizeDelta.y);
        int count = PixelKeys.Count;
        keyButtonLabels = new TMP_Text[count];

        for (int i = 0; i < count; i++)
        {
            if ((PixelAction)i == PixelAction.FirstPerson || (PixelAction)i == PixelAction.Zoom) continue;   // retired keys: the View window does their job
            if (i >= (int)PixelAction.DevTools && !Application.isEditor && !Debug.isDebugBuild) continue;   // dev keys only exist in the Editor / development builds
            int index = i;
            string label = keyActionLabels != null && i < keyActionLabels.Length ? keyActionLabels[i] : (PixelAction)i == PixelAction.TimeSlow ? "Time slow" : (PixelAction)i == PixelAction.FirstPerson ? "Switch view (first person / orbit)" : MenuKeyLabel((PixelAction)i);
            TMP_Text keyLabel = AddRowLabel(keysPanel.transform, label, y, out RectTransform row);
            keyLabel.enableAutoSizing = true;           // long names ("Pixel Bank hose") shrink to fit, they are not cut off
            keyLabel.fontSizeMax = rowFontSize * 0.85f;
            keyLabel.fontSizeMin = 9f;
            keyLabel.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            keyLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            Button b = MakeButton(row, "Key", "", new Vector2(10f, 10f), ButtonRole.Neutral, rowFontSize * 0.85f);
            RectTransform br = b.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.64f, 0.06f);
            br.anchorMax = new Vector2(1f, 0.94f);
            br.offsetMin = br.offsetMax = Vector2.zero;
            TMP_Text text = b.GetComponentInChildren<TMP_Text>();
            text.enableAutoSizing = true;
            text.fontSizeMax = rowFontSize * 0.95f;
            text.fontSizeMin = 12f;
            keyButtonLabels[i] = text;
            b.onClick.AddListener(() => { capturingAction = index; RefreshKeyLabels(); });
            y += rowHeight + 6f;
        }

        y += 18f;
        AddMenuButton(keysPanel.transform, resetKeysText, ButtonRole.Danger, ref y, () =>
        {
            PixelKeys.ResetAll();
            capturingAction = -1;
            RefreshKeyLabels();
        });
        Transform resetButton = keysPanel.transform.GetChild(keysPanel.transform.childCount - 1);
        TMP_Text resetText = resetButton.GetComponentInChildren<TMP_Text>();
        if (resetText != null)
        {
            resetText.enableAutoSizing = true;          // one line
            resetText.fontSizeMax = menuButtonFontSize;
            resetText.fontSizeMin = 12f;
#if UNITY_2023_1_OR_NEWER
            resetText.textWrappingMode = TextWrappingModes.NoWrap;
#else
            resetText.enableWordWrapping = false;
#endif
        }
        RefreshKeyLabels();
        FinishSectionPanel(keysPanel, y, settingsPanel);
    }

    private static string MenuKeyLabel(PixelAction action)
    {
        switch (action)
        {
            case PixelAction.Inventory: return "Open Inventory";
            case PixelAction.Shop: return "Open Shop";
            case PixelAction.LogWindow: return "Open Log";
            case PixelAction.Crafting: return "Open Crafting";
            case PixelAction.Toggles: return "Open Toggles";
            case PixelAction.Bank: return "Open Pixel Bank";
            case PixelAction.Skills: return "Open Skills";
            case PixelAction.Feedback: return "Give feedback";
            case PixelAction.ViewWindow: return "Open View window (+ Shift: next view)";
            case PixelAction.DevTools: return "Open Dev Tools";
            case PixelAction.MarkMoment: return "Dev: mark this moment";
            case PixelAction.InspectHover: return "Dev: inspect under mouse";
        }
        return action.ToString();
    }

    private void RefreshKeyLabels()
    {
        if (keyButtonLabels == null) return;
        for (int i = 0; i < keyButtonLabels.Length; i++)
            if (keyButtonLabels[i] != null) keyButtonLabels[i].text = capturingAction == i ? pressAKeyText : PixelKeys.Name((PixelAction)i);
    }

    /// <summary>While a key button waits for a key: the next key press becomes the binding (Escape cancels).</summary>
    private bool UpdateKeyCapture()
    {
        if (capturingAction < 0) return false;
        if (EscapePressed())
        {
            capturingAction = -1;
            RefreshKeyLabels();
            return true;
        }
        if (PixelKeys.TryCapture(out KeyCode key))
        {
            if (key == KeyCode.Backspace || key == KeyCode.Delete) key = KeyCode.None;   // Backspace / Delete leaves the action blank
            PixelKeys.Set((PixelAction)capturingAction, key);
            capturingAction = -1;
            RefreshKeyLabels();
        }
        return true; // nothing else (like Escape closing the menu) reacts while waiting
    }
}
