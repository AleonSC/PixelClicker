// ---- System notes ----
// PixelPauseMenuStats.cs
//   Part of PixelPauseMenu (partial class): the Stats screen - building it (BuildStatsPanel, LayoutStats), refreshing the numbers, the foldable stat sections and the stats search filter (StatRowsShown). The system notes are in PixelPauseMenu.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPauseMenu
{
    private void BuildStatsPanel()
    {
        statsPanel = BuildSectionPanel("Stats Panel", statsTitle, out float y);
        statsPanelRect = statsPanel.GetComponent<RectTransform>();

        // Search box over the list: typing shows only the rows (and sections) whose name or value matches.
        float searchH = rowFontSize * 1.5f;
        statsSearchField = PixelUIKit.CreateInputField(font, statsPanel.transform, "Stats Search", new Vector2(0f, searchH),
                                                       tickBoxColor, textColor, rowFontSize * 0.85f, statsSearchPlaceholder);
        RectTransform ssr = statsSearchField.GetComponent<RectTransform>();
        ssr.anchorMin = new Vector2(0f, 1f);
        ssr.anchorMax = new Vector2(1f, 1f);
        ssr.pivot = new Vector2(0.5f, 1f);
        ssr.sizeDelta = new Vector2(-60f, searchH);
        ssr.anchoredPosition = new Vector2(0f, -y);
        statsSearchField.onValueChanged.AddListener(_ =>
        {
            RebuildStatRows();
            PixelUIKit.UpdateScrollView(statsScroll, statsBar, statsContentHeight, statsViewHeight);
        });
        StyleSearchBox(statsSearchField);
        y += searchH + 10f;
        statsListTop = y;

        // The stats scroll when there are more than fit.
        statsScroll = PixelUIKit.CreateScrollView(statsPanel.transform, "Stats List", scrollbarColor, 12f, rowHeight,
                                                  out RectTransform content, out statsBar);
        RectTransform vr = statsScroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        vr.sizeDelta = new Vector2(0f, statsViewHeight);
        vr.anchoredPosition = new Vector2(0f, -y);

        statsContent = content;
        RebuildStatRows(); // the rows come from PixelStats.GetLines (they change as pixels unlock and minigames start)

        // Restart lives here now (it opens the hold-to-confirm screen).
        if (showRestart)
        {
            Vector2 restartSize = new Vector2(menuButtonSize.x * 0.5f, menuButtonSize.y * 0.7f);   // a small button at the bottom
            Button restart = MakeButton(statsPanel.transform, "Restart Button", restartText, restartSize, ButtonRole.Danger, menuButtonFontSize * 0.7f);
            statsRestartRect = restart.GetComponent<RectTransform>();
            statsRestartRect.anchorMin = statsRestartRect.anchorMax = statsRestartRect.pivot = new Vector2(0.5f, 1f);
            restart.onClick.AddListener(() => ShowView(restartPanel));
        }

        AddCloseX(statsPanel);   // the X at the top right replaces the Back button

        LayoutStats();
        statsPanel.SetActive(false);
    }

    /// <summary>Places the Restart and Back buttons under the stats list and sizes the Stats panel.</summary>
    private void LayoutStats()
    {
        float y = statsListTop + statsViewHeight + 12f;
        if (statsRestartRect != null)
        {
            statsRestartRect.anchoredPosition = new Vector2(0f, -y);
            y += statsRestartRect.sizeDelta.y;
        }
        y += 30f;
        statsPanelRect.sizeDelta = new Vector2(panelSize.x, Mathf.Max(panelSize.y, y));
    }

    private string FormatCount(double value)
    {
        if (!PixelClicker.AbbreviateNumbers || value < 1000d) return System.Math.Floor(value).ToString("N0");
        return PixelClicker.FormatNumber(value);
    }

    private void RefreshStats()
    {
        if (stats == null || statsContent == null) return;
        List<PixelStats.Line> lines = stats.GetLines(FormatCount);

        // Rows are rebuilt only when the list of rows itself changed (a new pixel type, a minigame that started...).
        string signature = lines.Count.ToString();
        foreach (PixelStats.Line l in lines) signature += "|" + (l.header ? "#" : "") + l.label;
        if (signature != statsSignature)
        {
            statsSignature = signature;
            RebuildStatRows(lines);
        }
        else
        {
            int v = 0;
            bool[] shown = StatRowsShown(lines);
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].header || !shown[i]) continue;
                if (v < statValueTexts.Count) statValueTexts[v++].text = lines[i].value;
            }
        }
        PixelUIKit.UpdateScrollView(statsScroll, statsBar, statsContentHeight, statsViewHeight);
    }

    private readonly HashSet<string> expandedStatSections = new HashSet<string>();   // empty = every section starts folded

    /// <summary>A Stats section heading you can click to fold / unfold the rows under it ("-" open, "+" folded).</summary>
    private void AddStatSectionHeader(Transform parent, string text, bool collapsed, ref float y)
    {
        y += 8f;
        float h = rowHeight * 0.8f;
        Button b = MakeButton(parent, text + " Header", (collapsed ? "+  " : "-  ") + text, new Vector2(10f, h), ButtonRole.Neutral, rowFontSize * 0.7f);
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-60f, h);   // 10 wider than the rows on each side, so the heading's text starts exactly where the row names do
        rt.anchoredPosition = new Vector2(0f, -y);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        t.color = statValueColor;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.rectTransform.offsetMin = new Vector2(10f, 0f);
        t.rectTransform.offsetMax = new Vector2(-10f, 0f);
        b.onClick.AddListener(() =>
        {
            if (!expandedStatSections.Remove(text)) expandedStatSections.Add(text);
            RebuildStatRows();
            PixelUIKit.UpdateScrollView(statsScroll, statsBar, statsContentHeight, statsViewHeight);
        });
        y += h + 4f;
    }

    private void RebuildStatRows(List<PixelStats.Line> lines = null)
    {
        if (statsContent == null) return;
        for (int i = statsContent.childCount - 1; i >= 0; i--) Destroy(statsContent.GetChild(i).gameObject);
        statValueTexts.Clear();
        if (lines == null && stats != null) lines = stats.GetLines(FormatCount);

        float cy = 0f;
        if (lines != null)
        {
            bool[] shown = StatRowsShown(lines);
            bool searching = StatsQuery.Length > 0;
            bool anyShown = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!shown[i]) continue;
                anyShown = true;
                PixelStats.Line line = lines[i];
                if (line.header)
                {
                    // While searching, matching sections show open (the "-" sign); clicking still folds them for later.
                    bool folded = !searching && !expandedStatSections.Contains(line.label);
                    AddStatSectionHeader(statsContent, line.label, folded, ref cy);
                }
                else
                {
                    TMP_Text value = AddStatRow(statsContent, line.label, ref cy);
                    value.text = line.value;
                    statValueTexts.Add(value);
                }
            }
            if (searching && !anyShown)
            {
                TMP_Text none = AddStatRow(statsContent, statsNoMatchText, ref cy);
                none.text = "";
            }
        }
        statsContentHeight = cy;
    }

    /// <summary>The Stats search text, trimmed and lower case ("" = no search).</summary>
    private string StatsQuery => statsSearchField != null && statsSearchField.text != null
        ? statsSearchField.text.Trim().ToLowerInvariant() : "";

    /// <summary>
    /// Which of the Stats lines are on screen. No search: every heading, plus the rows of unfolded sections. Searching: a row
    /// shows when its name or value matches (or its section heading matches), and a heading shows when it or any of its rows match.
    /// </summary>
    private bool[] StatRowsShown(List<PixelStats.Line> lines)
    {
        bool[] shown = new bool[lines.Count];
        string q = StatsQuery;
        int headerAt = -1;
        bool headerMatch = false, open = true;
        for (int i = 0; i < lines.Count; i++)
        {
            PixelStats.Line l = lines[i];
            if (l.header)
            {
                headerAt = i;
                open = expandedStatSections.Contains(l.label);
                headerMatch = q.Length > 0 && l.label != null && l.label.ToLowerInvariant().Contains(q);
                shown[i] = q.Length == 0 || headerMatch;
                continue;
            }
            if (q.Length == 0) { shown[i] = open; continue; }
            bool match = headerMatch
                         || (l.label != null && l.label.ToLowerInvariant().Contains(q))
                         || (l.value != null && l.value.ToLowerInvariant().Contains(q));
            shown[i] = match;
            if (match && headerAt >= 0) shown[headerAt] = true;
        }
        return shown;
    }
}
