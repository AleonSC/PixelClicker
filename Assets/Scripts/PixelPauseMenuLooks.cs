// ---- System notes ----
// PixelPauseMenuLooks.cs
//   Part of PixelPauseMenu (partial class): the floor / sky look rows of the Settings list (AddLookRow) and the draggable floor-style pop-out box they open (OpenLookPopout, UpdateFloorPopout, CloseFloorPopout). The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    // ------------------------------------------------------------------
    // The draggable floor-style box (opened from Settings > Floor style)
    // ------------------------------------------------------------------

    private GameObject floorPopout;
    private System.Func<IPixelLookSource> popSource;   // what the box restyles: the floor or the sky
    private string popTitle;
    private RawImage floorPopPreview;
    private TMP_Text floorPopName;

    /// <summary>Hides the menu and shows a small box you can drag around whose only option is the floor style. Closing it brings the menu back.</summary>
    private void OpenLookPopout(System.Func<IPixelLookSource> source, string popoutTitle)
    {
        IPixelLookSource first = source();
        if (floorPopout != null || first == null || !first.Usable) return;
        popSource = source;
        popTitle = popoutTitle;
        if (menuRoot != null) menuRoot.SetActive(false);

        floorPopout = new GameObject("Floor Popout", typeof(RectTransform), typeof(Image));
        floorPopout.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(floorPopout.GetComponent<Image>(), WindowShade.Window);   // (the theme window colour, live)
        RectTransform box = floorPopout.GetComponent<RectTransform>();
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(460f, 270f);
        box.anchoredPosition = floorPopPosition;

        // The whole top strip drags the box.
        GameObject handle = new GameObject("Drag Handle", typeof(RectTransform), typeof(Image), typeof(PixelPopoutDrag));
        handle.transform.SetParent(box, false);
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        RectTransform hr = handle.GetComponent<RectTransform>();
        hr.anchorMin = new Vector2(0f, 1f);
        hr.anchorMax = new Vector2(1f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(-16f, 64f);
        hr.anchoredPosition = new Vector2(0f, -8f);
        PixelPopoutDrag drag = handle.GetComponent<PixelPopoutDrag>();
        drag.target = box;
        drag.canvas = canvasRoot.GetComponent<Canvas>();
        drag.onMoved = () => floorPopPosition = box.anchoredPosition;

        TMP_Text title = MakeText(handle.transform, "Title", popTitle.ToUpperInvariant(), rowFontSize * 0.8f, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
        PixelUIKit.Stretch(title.rectTransform);
        title.rectTransform.offsetMax = new Vector2(-70f, 0f);
        title.rectTransform.offsetMin = new Vector2(16f, 0f);

        Button close = MakeButton(box, "Close", "X", new Vector2(52f, 52f), ButtonRole.Neutral, rowFontSize);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -14f);
        close.onClick.AddListener(CloseFloorPopout);

        // Body: [<] picture [>] and the style's name underneath.
        float pic = 130f;
        GameObject picGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
        picGo.transform.SetParent(box, false);
        floorPopPreview = picGo.GetComponent<RawImage>();
        floorPopPreview.raycastTarget = false;
        RectTransform pr = picGo.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(pic, pic);
        pr.anchoredPosition = new Vector2(0f, -18f);

        for (int side = -1; side <= 1; side += 2)
        {
            int dir = side;
            Button arrow = MakeButton(box, side < 0 ? "Previous" : "Next", side < 0 ? "<" : ">", new Vector2(64f, 64f), ButtonRole.Neutral, rowFontSize);
            RectTransform ar = arrow.GetComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 0.5f);
            ar.anchoredPosition = new Vector2(side * 140f, -18f);
            PixelUIKit.UseArrowGlyph(arrow, side < 0);
            arrow.onClick.AddListener(() => { IPixelLookSource src = popSource != null ? popSource() : null; if (src != null) src.Step(dir); });
        }

        floorPopName = MakeText(box, "Name", "", rowFontSize * 0.7f, FontStyles.Normal);
        floorPopName.alignment = TextAlignmentOptions.Center;
        floorPopName.raycastTarget = false;
        floorPopName.enableAutoSizing = true;
        floorPopName.fontSizeMax = rowFontSize * 0.7f;
        floorPopName.fontSizeMin = 12f;
        RectTransform nr = floorPopName.rectTransform;
        nr.anchorMin = new Vector2(0f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot = new Vector2(0.5f, 0f);
        nr.sizeDelta = new Vector2(-40f, 44f);
        nr.anchoredPosition = new Vector2(0f, 14f);

        UpdateFloorPopout();
    }

    private void UpdateFloorPopout()
    {
        IPixelLookSource look = popSource != null ? popSource() : null;
        if (look == null || floorPopName == null) return;
        PixelUIKit.SetText(floorPopName, look.StyleName(look.Current));
        floorPopPreview.texture = look.PreviewTexture(look.Current);
        floorPopPreview.color = look.PreviewColor(look.Current);
        floorPopPreview.uvRect = look.PreviewRect;
    }

    private void CloseFloorPopout()
    {
        if (floorPopout != null) Destroy(floorPopout);
        floorPopout = null;
        if (menuRoot != null) { menuRoot.SetActive(true); settingsRefreshersRun(); }
    }


    /// <summary>
    /// A "pick a look" row (floor style, sky): a picture of the current look, its name, and arrows to step through
    /// the looks. 'source' is asked again each time, so it works even if the look component wakes up later.
    /// </summary>
    private void AddLookRow(Transform parent, string labelText, System.Func<IPixelLookSource> source, ref float y, bool popout = false,
                            System.Func<bool> getToggle = null, System.Action<bool> setToggle = null)
    {
        TMP_Text label = AddRowLabel(parent, labelText, y, out RectTransform row);
        label.rectTransform.anchorMax = new Vector2(getToggle != null ? 0.3f : 0.4f, 1f);
        lookRows[row] = source;   // the search finds this row by any of its look names too

        if (getToggle != null)
        {
            // A tick box in the same entry, between the name and the picker (the floor: shown / hidden).
            Toggle toggle = MakeTickBox(row, new Vector2(0.355f, 0.5f), new Vector2(0.5f, 0.5f));
            toggle.SetIsOnWithoutNotify(getToggle());
            toggle.onValueChanged.AddListener(on => setToggle?.Invoke(on));
            toggle.onValueChanged.AddListener(_ => PixelAudio.Play("ui_click"));
            settingsRefreshers.Add(() => toggle.SetIsOnWithoutNotify(getToggle()));
        }

        // The shared look drop-down (PixelUIKit.CreateLookDropdown): picture + name, and a list of every look with its picture.
        // Its box is as tall as the row's other controls, and the list's entries are as tall as the box (the bigger look rows).
        PixelLookDropdown pick = PixelUIKit.CreateLookDropdown(font, row, labelText + " Picker", new Vector2(10f, rowHeight * 0.88f),
                                                               tickBoxColor, tickBoxColor, statValueColor, rowFontSize * 0.7f, source, escapeMasks: true);
        pick.unusableText = noFloorText;
        RectTransform pr = pick.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.42f, 0.06f);
        pr.anchorMax = new Vector2(1f, 0.94f);
        pr.offsetMin = pr.offsetMax = Vector2.zero;

        if (popout)
        {
            // A small 'pop-out' button right of the picker: closes the settings and opens a draggable box for this look (floor or sky).
            pr.offsetMax = new Vector2(-(rowHeight * 0.8f + 10f), 0f);   // (room for it)
            Button pop = MakeButton(row, "Look Popout Button", "", new Vector2(rowHeight * 0.8f, rowHeight * 0.88f), ButtonRole.Neutral, rowFontSize * 0.7f);
            RectTransform pbr = pop.GetComponent<RectTransform>();
            pbr.anchorMin = pbr.anchorMax = pbr.pivot = new Vector2(1f, 0.5f);
            pbr.anchoredPosition = Vector2.zero;
            // Icon: a big square with a smaller one standing out of its corner.
            void Square(float size, Vector2 at, Color c)
            {
                GameObject g = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(pop.transform, false);
                g.GetComponent<Image>().color = c;
                g.GetComponent<Image>().raycastTarget = false;
                RectTransform r = g.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(size, size);
                r.anchoredPosition = at;
            }
            float s = rowHeight * 0.3f;
            Square(s * 1.5f, new Vector2(-s * 0.25f, -s * 0.2f), new Color(1f, 1f, 1f, 0.55f));
            Square(s, new Vector2(s * 0.35f, s * 0.3f), Color.white);
            pop.onClick.AddListener(() => OpenLookPopout(source, labelText));
        }

        settingsRefreshers.Add(() => pick.Refresh(true));
        y += rowHeight + 6f;
    }
}
