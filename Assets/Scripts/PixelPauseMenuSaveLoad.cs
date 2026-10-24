// ---- System notes ----
// PixelPauseMenuSaveLoad.cs
//   Part of PixelPauseMenu (partial class): the Save / Load window (save slots, overwrite / result / rename overlays, hold-to-delete, slot tips, saving and loading a slot) and the Quit and Restart screens. The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    // ------------------------------------------------------------------
    // Save / Load window (save slots)
    // ------------------------------------------------------------------

    private void BuildSaveLoadPanel()
    {
        int count = saveGame.SlotCount;
        saveLoadPanel = BuildSectionPanel("Save Load Panel", saveLoadMenuText, out float y);

        // The slot list (scrolls when there are more slots than rows).
        float rowH = Mathf.Clamp(slotHeight * 0.65f, 56f, 76f);   // compact: just the name (the details show on hover)
        float rowStep = rowH + 8f;
        float viewHeight = FitViewHeight(Mathf.Min(count, visibleSlotRows) * rowStep, y + menuButtonSize.y + 14f);   // leaves room for the Save / Load buttons
        slotScroll = PixelUIKit.CreateScrollView(saveLoadPanel.transform, "Slot List", scrollbarColor, 12f, rowStep,
                                                 out RectTransform content, out slotBar);
        RectTransform vr = slotScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-40f, viewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        slotButtons = new Button[count];
        slotLabels = new TMP_Text[count];
        slotDetails = new string[count];
        for (int i = 0; i < count; i++)
        {
            int slot = i + 1;
            Button b = MakeButton(content, "Slot " + slot, "", new Vector2(panelSize.x - 80f, rowH), ButtonRole.Neutral, rowFontSize);   // exactly as wide as the Save / Load buttons below (the scroll bar sits in the margin)
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(i * rowStep));
            b.onClick.AddListener(() => SelectSlot(slot));
            PixelHoverTip hover = b.gameObject.AddComponent<PixelHoverTip>();
            hover.onEnter = () => hoverSlot = slot;
            hover.onExit = () => { if (hoverSlot == slot) hoverSlot = -1; };

            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = true;
            label.enableAutoSizing = true; // two lines always fit the row: the text shrinks instead of overflowing
            label.fontSizeMax = rowFontSize * 0.85f;
            label.fontSizeMin = 10f;
            label.overflowMode = TextOverflowModes.Truncate;
            float renameWidth = Mathf.Max(150f, rowH * 2f);
            label.rectTransform.offsetMin = new Vector2(20f, 6f);
            label.rectTransform.offsetMax = new Vector2(-(renameWidth + 24f), -6f);

            // A Rename button at the right end of the row.
            Button rename = MakeButton(b.transform, "Rename", renameButtonText, new Vector2(renameWidth, rowH - 20f), ButtonRole.Neutral, rowFontSize * 0.75f);
            RectTransform rr = rename.GetComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = rr.pivot = new Vector2(1f, 0.5f);
            rr.anchoredPosition = new Vector2(-10f, 0f);
            rename.onClick.AddListener(() => OpenRename(slot));
            slotButtons[i] = b;
            slotLabels[i] = label;
        }
        PixelUIKit.UpdateScrollView(slotScroll, slotBar, count * rowStep, viewHeight);
        y += viewHeight + 14f;

        // Save | Load for the selected slot.
        float gap = 12f;
        Vector2 half = new Vector2((panelSize.x - 80f - gap) * 0.5f, menuButtonSize.y);
        slotSaveButton = MakeButton(saveLoadPanel.transform, "Slot Save", saveText, half, ButtonRole.Primary, menuButtonFontSize);
        slotLoadButton = MakeButton(saveLoadPanel.transform, "Slot Load", loadText, half, ButtonRole.Primary, menuButtonFontSize);
        RectTransform sr = slotSaveButton.GetComponent<RectTransform>(), lr = slotLoadButton.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 1f);
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
        sr.anchoredPosition = new Vector2(-(half.x + gap) * 0.5f, -y);
        lr.anchoredPosition = new Vector2((half.x + gap) * 0.5f, -y);
        slotSaveButton.onClick.AddListener(OnSlotSave);
        slotLoadButton.onClick.AddListener(OnSlotLoad);
        y += menuButtonSize.y + 14f;

        // A small button under Save | Load that opens the list of autosaves (a separate window).
        Button autosavesButton = MakeButton(saveLoadPanel.transform, "Autosaves Button", autosavesText, new Vector2(Mathf.Min(260f, panelSize.x - 80f), menuButtonSize.y * 0.62f), ButtonRole.Neutral, menuButtonFontSize * 0.6f);
        RectTransform ar = autosavesButton.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 1f);
        ar.anchoredPosition = new Vector2(0f, -y);
        autosavesButton.onClick.AddListener(() => { if (saveGame != null) PixelAutosaveWindow.Open(saveGame, () => SetPaused(false)); });
        y += menuButtonSize.y * 0.62f + 14f;

        BuildOverwriteOverlay(saveLoadPanel.transform);
        BuildResultOverlay(saveLoadPanel.transform);
        BuildRenameOverlay(saveLoadPanel.transform);
        FinishSectionPanel(saveLoadPanel, y);
        PixelCubiePeek peekCubie = gameObject.AddComponent<PixelCubiePeek>();
        peekCubie.Setup(saveLoadPanel.GetComponent<RectTransform>(), () => IsPaused && saveLoadPanel != null && saveLoadPanel.activeSelf
            && !(renameOverlay != null && renameOverlay.activeSelf) && !(overwriteOverlay != null && overwriteOverlay.activeSelf) && !(resultOverlay != null && resultOverlay.activeSelf));
        overwriteOverlay.transform.SetAsLastSibling(); // above the Back button too
        renameOverlay.transform.SetAsLastSibling();
        resultOverlay.transform.SetAsLastSibling();
    }

    /// <summary>The "this slot already has a save" warning: a dimmed box over the window with Overwrite / Cancel.</summary>
    private void BuildOverwriteOverlay(Transform parent)
    {
        overwriteOverlay = new GameObject("Overwrite Warning", typeof(RectTransform), typeof(Image));
        overwriteOverlay.transform.SetParent(parent, false);
        overwriteOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // also blocks the clicks behind it
        PixelUIKit.Stretch(overwriteOverlay.GetComponent<RectTransform>());

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        box.transform.SetParent(overwriteOverlay.transform, false);
        PixelUIKit.StyleWindow(box.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(panelSize.x - 60f, 340f);

        TMP_Text title = MakeText(box.transform, "Title", "", titleFontSize * 0.8f, FontStyles.Bold);
        title.color = overwriteWarningColor;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.3f);
        tr.anchoredPosition = new Vector2(0f, -20f);
        title.enableAutoSizing = true; // long titles shrink instead of overflowing
        title.fontSizeMax = titleFontSize * 0.8f;
        title.fontSizeMin = 16f;
        overwriteTitleLabel = title;

        overwriteText = MakeText(box.transform, "Message", "", rowFontSize * 0.9f, FontStyles.Normal);
        overwriteText.alignment = TextAlignmentOptions.Top;
        RectTransform mr = overwriteText.rectTransform;
        mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.pivot = new Vector2(0.5f, 0.5f);
        mr.offsetMin = new Vector2(30f, 24f + menuButtonSize.y + 20f);               // above the buttons
        mr.offsetMax = new Vector2(-30f, -(20f + titleFontSize * 1.3f + 10f));        // below the title
        overwriteText.enableAutoSizing = true; // the message shrinks to fit its area
        overwriteText.fontSizeMax = rowFontSize * 0.9f;
        overwriteText.fontSizeMin = 14f;
        overwriteText.overflowMode = TextOverflowModes.Truncate;

        float gap = 12f;
        Vector2 half = new Vector2((br.sizeDelta.x - 60f - gap) * 0.5f, menuButtonSize.y);
        Button confirm = MakeButton(box.transform, "Overwrite", overwriteConfirmText, half, ButtonRole.Danger, menuButtonFontSize);
        Button cancel = MakeButton(box.transform, "Cancel", overwriteCancelText, half, ButtonRole.Neutral, menuButtonFontSize);
        RectTransform cr = confirm.GetComponent<RectTransform>(), xr = cancel.GetComponent<RectTransform>();
        foreach (Button b in new[] { confirm, cancel })
        {
            TMP_Text bl = b.GetComponentInChildren<TMP_Text>();
            bl.enableAutoSizing = true; // "Overwrite" never wraps
            bl.fontSizeMax = menuButtonFontSize * 0.8f;
            bl.fontSizeMin = 12f;
            bl.overflowMode = TextOverflowModes.Truncate;
        }
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0f);
        xr.anchorMin = xr.anchorMax = xr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(-(half.x + gap) * 0.5f, 24f);
        xr.anchoredPosition = new Vector2((half.x + gap) * 0.5f, 24f);
        // Overwriting needs a short HOLD on the red button (a fill grows across it), so it can't happen by an accidental click.
        TMP_Text confirmLabel = confirm.GetComponentInChildren<TMP_Text>();
        confirmLabel.text = !string.IsNullOrEmpty(overwriteHoldText) ? overwriteHoldText : "Hold to overwrite";
        confirmLabel.transform.SetAsLastSibling();
        GameObject holdFill = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        holdFill.transform.SetParent(confirm.transform, false);
        holdFill.transform.SetSiblingIndex(1);   // above the button's face, under its label
        Image holdImage = holdFill.GetComponent<Image>();
        holdImage.color = new Color(1f, 1f, 1f, 0.4f);
        holdImage.raycastTarget = false;
        RectTransform holdRect = holdFill.GetComponent<RectTransform>();
        holdRect.anchorMin = Vector2.zero; holdRect.anchorMax = new Vector2(0f, 1f); holdRect.offsetMin = holdRect.offsetMax = Vector2.zero;
        overwriteHold = confirm.gameObject.AddComponent<PixelHoldButton>();
        overwriteHold.Setup(holdRect, overwriteHoldSeconds > 0.01f ? overwriteHoldSeconds : 0.6f, () =>
        {
            overwriteHold.ResetProgress();
            overwriteOverlay.SetActive(false);
            bool saved = saveGame.SaveToSlot(selectedSlot, false);
            RefreshSlots();
            ShowResult(saved ? string.Format(savedResultFormat, SlotRef(selectedSlot)) : saveFailedResultText);
        });
        cancel.onClick.AddListener(() => overwriteOverlay.SetActive(false));

        overwriteOverlay.SetActive(false);
    }

    private TMP_Text overwriteTitleLabel;
    private PixelHoldButton overwriteHold;
    private GameObject resultOverlay;
    private TMP_Text resultLabel;

    /// <summary>A compact dialog card over a dimmed Save / Load menu that says what happened ("Saved to slot 2") with a Got it button.</summary>
    private void BuildResultOverlay(Transform parent)
    {
        resultOverlay = new GameObject("Result Window", typeof(RectTransform), typeof(Image));
        resultOverlay.transform.SetParent(parent, false);
        resultOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f); // dims the menu and blocks the clicks behind it
        PixelUIKit.Stretch(resultOverlay.GetComponent<RectTransform>());

        GameObject card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        card.transform.SetParent(resultOverlay.transform, false);
        PixelUIKit.StyleWindow(card.GetComponent<Image>(), resultColor);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(panelSize.x - 60f, 120f + menuButtonSize.y + 60f);

        resultLabel = MakeText(card.transform, "Message", "", titleFontSize * 0.8f, FontStyles.Bold);
        resultLabel.alignment = TextAlignmentOptions.Center;
        resultLabel.enableAutoSizing = true;
        resultLabel.fontSizeMax = titleFontSize * 0.8f;
        resultLabel.fontSizeMin = 16f;
        RectTransform lr = resultLabel.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.pivot = new Vector2(0.5f, 0.5f);
        lr.offsetMin = new Vector2(30f, 24f + menuButtonSize.y + 20f);
        lr.offsetMax = new Vector2(-30f, -30f);

        Button ok = MakeButton(card.transform, "Got It", resultButtonText, new Vector2(cr.sizeDelta.x - 80f, menuButtonSize.y), ButtonRole.Success, menuButtonFontSize);
        RectTransform or = ok.GetComponent<RectTransform>();
        or.anchorMin = or.anchorMax = or.pivot = new Vector2(0.5f, 0f);
        or.anchoredPosition = new Vector2(0f, 24f);
        ok.onClick.AddListener(() => resultOverlay.SetActive(false));
        resultOverlay.SetActive(false);
    }

    private void ShowResult(string text)
    {
        if (resultOverlay == null) return;
        resultLabel.text = text;
        resultOverlay.SetActive(true);
        resultOverlay.transform.SetAsLastSibling();
    }

    /// <summary>The rename box: a dimmed Save / Load menu with a text field, OK and Cancel.</summary>
    private void BuildRenameOverlay(Transform parent)
    {
        renameOverlay = new GameObject("Rename Slot", typeof(RectTransform), typeof(Image));
        renameOverlay.transform.SetParent(parent, false);
        renameOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        PixelUIKit.Stretch(renameOverlay.GetComponent<RectTransform>());

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        box.transform.SetParent(renameOverlay.transform, false);
        PixelUIKit.StyleWindow(box.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(panelSize.x - 60f, 120f + menuButtonSize.y * 2f + 80f);

        renameTitleLabel = MakeText(box.transform, "Title", "", titleFontSize * 0.75f, FontStyles.Bold);
        RectTransform tr = renameTitleLabel.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, titleFontSize * 1.2f);
        tr.anchoredPosition = new Vector2(0f, -20f);

        renameInput = PixelUIKit.CreateInputField(font, box.transform, "Name Field", new Vector2(br.sizeDelta.x - 60f, menuButtonSize.y),
                                                  tickBoxColor, textColor, menuButtonFontSize * 0.9f, renamePlaceholder);
        renameInput.characterLimit = Mathf.Max(4, slotNameMaxLength);
        RectTransform ir = renameInput.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 1f);
        ir.anchoredPosition = new Vector2(0f, -(30f + titleFontSize * 1.2f));

        float gap = 12f;
        Vector2 half = new Vector2((br.sizeDelta.x - 60f - gap) * 0.5f, menuButtonSize.y);
        Button ok = MakeButton(box.transform, "Rename OK", "OK", half, ButtonRole.Success, menuButtonFontSize);
        Button cancel = MakeButton(box.transform, "Rename Cancel", overwriteCancelText, half, ButtonRole.Neutral, menuButtonFontSize);
        RectTransform okr = ok.GetComponent<RectTransform>(), cr = cancel.GetComponent<RectTransform>();
        okr.anchorMin = okr.anchorMax = okr.pivot = new Vector2(0.5f, 0f);
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0.5f, 0f);
        okr.anchoredPosition = new Vector2(-(half.x + gap) * 0.5f, 24f);
        cr.anchoredPosition = new Vector2((half.x + gap) * 0.5f, 24f);
        ok.onClick.AddListener(ConfirmRename);
        cancel.onClick.AddListener(() => renameOverlay.SetActive(false));
        renameInput.onSubmit.AddListener(_ => ConfirmRename());
        renameOverlay.SetActive(false);
    }

    private void OpenRename(int slot)
    {
        if (renameOverlay == null) return;
        renameSlot = slot;
        renameTitleLabel.text = string.Format(renameTitle, slot);
        renameInput.text = saveGame != null ? saveGame.GetSlotName(slot) : "";
        renameOverlay.SetActive(true);
        renameOverlay.transform.SetAsLastSibling();
        renameInput.Select();
        renameInput.ActivateInputField();
    }

    private void ConfirmRename()
    {
        string name = (renameInput.text ?? "").Trim();
        if (saveGame != null) saveGame.SetSlotName(renameSlot, name);   // empty = back to 'Slot N'; the name is stored in the save file
        renameOverlay.SetActive(false);
        RefreshSlots();
    }

    private int deleteHoldSlot = -1;
    private float deleteHeld;
    private PixelHoldMeter deleteMeter;

    /// <summary>Hold the RIGHT mouse button on a slot that has a save: a ring fills, and when it is full the save is deleted.</summary>
    private void UpdateSlotDeleteHold()
    {
        bool can = IsPaused && hoverSlot > 0 && saveGame != null && saveLoadPanel != null && saveLoadPanel.activeInHierarchy && saveGame.SlotHasSave(hoverSlot)
                   && !(renameOverlay != null && renameOverlay.activeSelf) && !(overwriteOverlay != null && overwriteOverlay.activeSelf) && !(resultOverlay != null && resultOverlay.activeSelf);
        if (deleteHoldSlot < 0)
        {
            if (can && PixelInput.RightPressed()) { deleteHoldSlot = hoverSlot; deleteHeld = 0f; }
            else return;
        }
        if (!can || hoverSlot != deleteHoldSlot || !PixelInput.RightHeld())
        {
            deleteHoldSlot = -1; deleteHeld = 0f;
            if (deleteMeter != null) deleteMeter.Hide();
            return;
        }
        float need = deleteSlotHoldSeconds > 0f ? deleteSlotHoldSeconds : 1.4f;
        deleteHeld += Time.unscaledDeltaTime;
        if (deleteHeld >= need)
        {
            int slot = deleteHoldSlot;
            deleteHoldSlot = -1; deleteHeld = 0f;
            if (deleteMeter != null) deleteMeter.Hide();
            bool ok = saveGame.DeleteSlot(slot);
            RefreshSlots();
            PixelAudio.Play("device_remove");
            ShowResult(ok ? SlotRef(slot) + " deleted." : "Could not delete " + SlotRef(slot) + ".");
            return;
        }
        if (deleteMeter == null) deleteMeter = new GameObject("Delete Hold Meter").AddComponent<PixelHoldMeter>();
        deleteMeter.Show(PixelInput.PointerPosition(), deleteHeld / need, "", new Color(1f, 0.35f, 0.3f), true);
    }

    /// <summary>The hover box over a save slot: when it was saved, time played and pixels. Follows the mouse.</summary>
    private void UpdateSlotTip()
    {
        bool show = IsPaused && hoverSlot > 0 && saveLoadPanel != null && saveLoadPanel.activeInHierarchy && slotDetails != null && hoverSlot <= slotDetails.Length
                    && !(renameOverlay != null && renameOverlay.activeSelf) && !(overwriteOverlay != null && overwriteOverlay.activeSelf) && !(resultOverlay != null && resultOverlay.activeSelf);
        if (!show) { if (slotTip != null && slotTip.activeSelf) slotTip.SetActive(false); return; }
        if (slotTip == null)
        {
            slotTip = new GameObject("Slot Tip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            slotTip.transform.SetParent(canvasRoot.transform, false);
            slotTip.GetComponent<CanvasGroup>().blocksRaycasts = false;
            Image bg = slotTip.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.09f, 0.13f, 0.97f);
            bg.raycastTarget = false;
            PixelUIKit.StyleBox(bg);
            GameObject shotGo = new GameObject("Screenshot", typeof(RectTransform), typeof(RawImage));
            shotGo.transform.SetParent(slotTip.transform, false);
            slotTipImage = shotGo.GetComponent<RawImage>();
            slotTipImage.raycastTarget = false;
            slotTipImageRect = shotGo.GetComponent<RectTransform>();
            slotTipImageRect.anchorMin = slotTipImageRect.anchorMax = slotTipImageRect.pivot = new Vector2(0.5f, 1f);
            slotTipLabel = MakeText(slotTip.transform, "Text", "", rowFontSize * 0.8f, FontStyles.Normal);
            slotTipLabel.raycastTarget = false;
            slotTipLabel.alignment = TextAlignmentOptions.Left;
            RectTransform lr = slotTipLabel.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(16f, 8f); lr.offsetMax = new Vector2(-16f, -8f);
        }
        if (!slotTip.activeSelf) slotTip.SetActive(true);
        slotTip.transform.SetAsLastSibling();
        string text = slotDetails[hoverSlot - 1];
        if (slotTipLabel.text != text) slotTipLabel.text = text;
        RectTransform rt = slotTip.GetComponent<RectTransform>();
        // The screenshot taken when that slot was saved sits above the details.
        Texture2D shot = saveGame != null ? PixelSaveShots.Load(saveGame.SlotThumbPath(hoverSlot)) : null;
        float imgH = 0f;
        slotTipImage.gameObject.SetActive(shot != null);
        if (shot != null)
        {
            slotTipImage.texture = shot;
            imgH = 488f * shot.height / Mathf.Max(1, shot.width);
            slotTipImageRect.sizeDelta = new Vector2(488f, imgH);
            slotTipImageRect.anchoredPosition = new Vector2(0f, -10f);
            slotTipLabel.rectTransform.offsetMax = new Vector2(-16f, -(imgH + 18f));
        }
        else slotTipLabel.rectTransform.offsetMax = new Vector2(-16f, -8f);
        Vector2 size = new Vector2(520f, Mathf.Ceil(slotTipLabel.GetPreferredValues(text, 488f, 0f).y) + 20f + (shot != null ? imgH + 10f : 0f));
        rt.sizeDelta = size;
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local)) return;
        bool rightHalf = local.x > 0f;
        rt.pivot = new Vector2(rightHalf ? 1f : 0f, local.y < -canvasRect.rect.height * 0.25f ? 0f : 1f);
        rt.anchoredPosition = local + new Vector2(rightHalf ? -18f : 18f, rt.pivot.y > 0.5f ? -22f : 22f);
    }

    private void OpenSaveLoad()
    {
        selectedSlot = saveGame != null ? saveGame.CurrentSlot : 1;
        if (overwriteOverlay != null) overwriteOverlay.SetActive(false);
        if (resultOverlay != null) resultOverlay.SetActive(false);
        if (renameOverlay != null) renameOverlay.SetActive(false);
        RefreshSlots();
    }

    private void SelectSlot(int slot)
    {
        selectedSlot = slot;
        RefreshSlots();
    }

    /// <summary>Fills every slot row (name, date, pixels) and marks the selected one; Load only works on a slot with a save.</summary>
    private void RefreshSlots()
    {
        if (saveGame == null || slotButtons == null) return;
        int current = saveGame.CurrentSlot;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slot = i + 1;
            string name = SlotNameOf(slot) + (slot == current ? currentSlotTag : "");
            slotDetails[i] = saveGame.TryGetSlotInfo(slot, out string savedAt, out double pixels, out double played)
                ? savedAt + "\n" + string.Format(slotTimeFormat, PixelStats.FormatTime(played)) + "\n" + string.Format(slotPixelsFormat, FormatCount(pixels)) + "\n<color=#ff9a90>Hold right click to delete</color>"
                : emptySlotText;
            slotLabels[i].text = "<b>" + name + "</b>";
            // The selected row is a Selected button, the others Neutral (PixelUITheme; SetRole keeps the colour managed, so it follows the selection).
            PixelUIKit.SetRole(slotButtons[i], slot == selectedSlot ? ButtonRole.Selected : ButtonRole.Neutral);
        }
        bool hasSave = saveGame.SlotHasSave(selectedSlot);
        bool newGame = openedFromTitle && !hasSave;   // from the main menu an empty slot starts a new game there
        slotLoadButton.interactable = hasSave || newGame;
        TMP_Text loadLabel = slotLoadButton.GetComponentInChildren<TMP_Text>();
        if (loadLabel != null) loadLabel.text = newGame ? newGameButtonText : loadText;
    }

    private void OnSlotSave()
    {
        if (saveGame == null) return;
        if (!saveGame.SlotHasSave(selectedSlot))
        {
            bool saved = saveGame.SaveToSlot(selectedSlot, false);
            RefreshSlots();
            ShowResult(saved ? string.Format(savedResultFormat, SlotRef(selectedSlot)) : saveFailedResultText);
            return;
        }

        // Saving over an existing save: ask first.
        saveGame.TryGetSlotInfo(selectedSlot, out string savedAt, out double _);
        overwriteTitleLabel.text = string.Format(overwriteTitle, SlotRef(selectedSlot));
        overwriteText.text = string.Format(overwriteMessage, SlotRef(selectedSlot), savedAt);
        if (overwriteHold != null) overwriteHold.ResetProgress();
        overwriteOverlay.SetActive(true);
    }

    private void OnSlotLoad()
    {
        if (saveGame == null) return;
        if (openedFromTitle && !saveGame.SlotHasSave(selectedSlot))
        {
            int slot = selectedSlot;
            ShowView(mainPanel);   // closes the window (back to the title screen)
            PixelTitleScreen.StartNewGame(slot);
            return;
        }
        bool loaded = saveGame.LoadSlot(selectedSlot, false);
        RefreshSlots();
        if (loaded) { if (openedFromTitle) { ShowView(mainPanel); PixelTitleScreen.PlayFromMenu(); } else SetPaused(false); return; }   // from the main menu a load goes straight into the game, like New Game   // from the title: back to the title screen; in a game: resume straight into the loaded save   // a successful load closes the Save / Load window (back to the menu, or the title screen when opened from there)
        ShowResult(loadFailedResultText);
    }

    /// <summary>"This deletes everything" screen: the player has to HOLD the red button to confirm.</summary>
    private void BuildQuitPanel()
    {
        quitPanel = BuildSectionPanel("Quit Panel", quitChoiceTitle, out float y);
        AddMenuButton(quitPanel.transform, quitDesktopText, ButtonRole.Danger, ref y, Quit);
        AddMenuButton(quitPanel.transform, quitMainMenuText, ButtonRole.Neutral, ref y, ToMainMenu);
        FinishSectionPanel(quitPanel, y);
        PixelQuitFx.Build(quitPanel);   // the screen goes dark and red neon light pours out round the box
    }

    /// <summary>Quit > Main menu: saves, then the camera zooms back into the cube and the title screen returns.</summary>
    private void ToMainMenu()
    {
        if (saveGame != null) saveGame.Save(false);
        SetPaused(false);
        PixelTitleScreen.ReturnToMenu();
    }

    private void BuildRestartPanel()
    {
        restartPanel = BuildSectionPanel("Restart Panel", restartConfirmTitle, out float y);

        TMP_Text warning = MakeText(restartPanel.transform, "Warning", restartConfirmMessage, rowFontSize * 0.9f, FontStyles.Normal);
        warning.color = restartWarningColor;
        warning.alignment = TextAlignmentOptions.Top;
        float width = panelSize.x - 80f;
        float height = Mathf.Ceil(warning.GetPreferredValues(restartConfirmMessage, width, 0f).y) + 10f;
        RectTransform wr = warning.rectTransform;
        wr.anchorMin = new Vector2(0.5f, 1f);
        wr.anchorMax = new Vector2(0.5f, 1f);
        wr.pivot = new Vector2(0.5f, 1f);
        wr.sizeDelta = new Vector2(width, height);
        wr.anchoredPosition = new Vector2(0f, -y);
        y += height + 20f;

        // The hold-to-confirm button: a fill grows across it while the mouse button stays down.
        Button hold = MakeButton(restartPanel.transform, "Hold To Confirm", "", menuButtonSize, ButtonRole.Danger, menuButtonFontSize);
        RectTransform hr = hold.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 1f);
        hr.anchoredPosition = new Vector2(0f, -y);
        TMP_Text holdLabel = hold.GetComponentInChildren<TMP_Text>();
        holdLabel.text = restartHoldText;
        holdLabel.enableAutoSizing = true;
        holdLabel.fontSizeMax = menuButtonFontSize * 0.8f;
        holdLabel.fontSizeMin = 14f;
        holdLabel.transform.SetAsLastSibling();

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(hold.transform, false);
        fill.transform.SetSiblingIndex(0); // behind the label
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = restartFillColor;
        fillImage.raycastTarget = false;
        RectTransform fr = fill.GetComponent<RectTransform>();
        PixelUIKit.Stretch(fr);

        restartHold = hold.gameObject.AddComponent<PixelHoldButton>();
        restartHold.Setup(fr, restartHoldSeconds, Restart);
        y += menuButtonSize.y + menuButtonSpacing;

        FinishSectionPanel(restartPanel, y, statsPanel);
    }
}
