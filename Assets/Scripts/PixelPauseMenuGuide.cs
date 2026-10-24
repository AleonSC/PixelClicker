// ---- System notes ----
// PixelPauseMenuGuide.cs
//   Part of PixelPauseMenu (partial class): the Game Guide screens (Controls, How To Play, Database text tabs: BuildGuideScreen, RefreshGuideScreen, GuideCell) and Cubie peeking out from behind a panel (AddCubiePeek). The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    /// <summary>Cubie may peek out from behind this panel while it is open and idle (see PixelCubiePeek).</summary>
    private void AddCubiePeek(GameObject panelObject)
    {
        if (panelObject == null) return;
        PixelCubiePeek p = gameObject.AddComponent<PixelCubiePeek>();
        p.Setup(panelObject.GetComponent<RectTransform>(), () => IsPaused && panelObject != null && panelObject.activeSelf);
    }

    private GuideScreen BuildGuideScreen(string objectName, string title, string resource, string emptyText, bool isControlsTab)
    {
        GuideScreen s = new GuideScreen { resource = resource, empty = emptyText };
        s.panel = BuildSectionPanel(objectName, title, out float y);
        RectTransform pr = s.panel.GetComponent<RectTransform>();

        // Three tabs under the title: Controls | How To Play | Database (the first two are panels the tabs switch between; Database opens the game database window).
        float tabWidth = (guideWidth - 80f - 20f) / 3f;
        for (int tab = 0; tab < 3; tab++)
        {
            bool controls = tab == 0, database = tab == 2;
            bool active = !database && controls == isControlsTab;
            string label = controls ? controlsText : database ? databaseTabText : howToPlayTabText;
            Button tabButton = MakeButton(s.panel.transform, label + " Tab", label, new Vector2(tabWidth, rowHeight), active ? ButtonRole.Selected : ButtonRole.Neutral, menuButtonFontSize * 0.8f);
            RectTransform tabRect = tabButton.GetComponent<RectTransform>();
            tabRect.anchorMin = tabRect.anchorMax = tabRect.pivot = new Vector2(0.5f, 1f);
            tabRect.anchoredPosition = new Vector2((tab - 1) * (tabWidth + 10f), -y);
            tabButton.onClick.AddListener(() =>
            {
                if (database) { PixelDatabaseWindow.Toggle(); return; }
                GuideScreen target = controls ? controlsScreen : howToPlayScreen;
                if (target != null) ShowView(target.panel);
            });
        }
        y += rowHeight + 14f;

        s.viewHeight = FitViewHeight(guideViewHeight, y);
        pr.sizeDelta = new Vector2(guideWidth, pr.sizeDelta.y);

        s.scroll = PixelUIKit.CreateScrollView(s.panel.transform, title + " List", scrollbarColor, 12f, guideFontSize * 1.5f,
                                               out RectTransform content, out s.bar);
        RectTransform vr = s.scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(-60f, s.viewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        FinishSectionPanel(s.panel, y + s.viewHeight);
        return s;
    }

    /// <summary>
    /// Reloads the screen's text file and shows it as rows: lines starting with // are skipped, # is a coloured heading,
    /// "Key | Action" is two columns (the action wraps inside its own column, so rows never overlap), anything else is plain text.
    /// </summary>
    private void RefreshGuideScreen(GuideScreen s)
    {
        TextAsset asset = Resources.Load<TextAsset>(s.resource);
        string raw = PixelKeys.Replace(asset != null ? asset.text : "");
        RectTransform content = s.scroll.content;
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        float viewWidth = s.scroll.GetComponent<RectTransform>().rect.width;
        if (viewWidth < 100f) viewWidth = guideWidth - 60f;
        float width = viewWidth - 36f;
        float keyWidth = width * Mathf.Clamp(guideColumnPercent, 15f, 60f) / 100f;
        float gap = 14f;
        float actionWidth = width - keyWidth - gap;

        float y = 0f;
        bool any = false;
        foreach (string rawLine in raw.Replace("\r", "").Split('\n'))
        {
            string line = rawLine.TrimEnd();
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("//")) continue;

            if (trimmed.StartsWith("#"))
            {
                if (any) y += 18f;   // air above each heading
                float size = guideFontSize * guideHeadingPercent / 100f;
                TMP_Text h = GuideCell(content, trimmed.TrimStart('#').Trim(), size, FontStyles.Bold, guideHeadingColor, 10f, width, y);
                y += Mathf.Ceil(h.GetPreferredValues(h.text, width, 0f).y) + 8f;
            }
            else if (line.Contains(" | "))
            {
                int bar = line.IndexOf(" | ", System.StringComparison.Ordinal);
                TMP_Text k = GuideCell(content, line.Substring(0, bar).Trim(), guideFontSize, FontStyles.Bold, textColor, 10f, keyWidth, y);
                TMP_Text a = GuideCell(content, line.Substring(bar + 3).Trim(), guideFontSize, FontStyles.Normal, textColor, 10f + keyWidth + gap, actionWidth, y);
                float hk = Mathf.Ceil(k.GetPreferredValues(k.text, keyWidth, 0f).y);
                float ha = Mathf.Ceil(a.GetPreferredValues(a.text, actionWidth, 0f).y);
                y += Mathf.Max(hk, ha) + 10f;
            }
            else if (trimmed.Length == 0)
            {
                if (any) y += guideFontSize * 0.6f;
                continue;
            }
            else
            {
                TMP_Text p = GuideCell(content, line, guideFontSize, FontStyles.Normal, textColor, 10f, width, y);
                y += Mathf.Ceil(p.GetPreferredValues(p.text, width, 0f).y) + 8f;
            }
            any = true;
        }

        if (!any)
        {
            TMP_Text e = GuideCell(content, s.empty, guideFontSize, FontStyles.Normal, textColor, 10f, width, 0f);
            y = Mathf.Ceil(e.GetPreferredValues(e.text, width, 0f).y);
        }
        s.scroll.content.anchoredPosition = Vector2.zero;
        PixelUIKit.UpdateScrollView(s.scroll, s.bar, y + 10f, s.viewHeight);
    }

    /// <summary>One wrapping text cell of the Controls / How to Play screens, top-left anchored at (x, -y).</summary>
    private TMP_Text GuideCell(RectTransform parent, string text, float size, FontStyles style, Color color, float x, float width, float y)
    {
        TMP_Text t = MakeText(parent, "Guide Text", text, size, style);
        t.alignment = TextAlignmentOptions.TopLeft;
        t.richText = true;
        t.color = color;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(width, 10f);
        r.anchoredPosition = new Vector2(x, -y);
        return t;
    }
}
