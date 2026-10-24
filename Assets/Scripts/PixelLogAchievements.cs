// ---- System notes ----
// PixelLogAchievements.cs
//   Part of PixelLog (partial class): the Achievements tab: its badge rows (and the badge bar / title pieces the goal rows share), the Collector / Clicker row groups, the section headers and RefreshAchievementsTab. The system notes are in PixelLog.cs.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelLog
{
    /// <summary>
    /// One achievement per row, laid out like a badge: the pixel's picture on the left, a thin vertical divider, then the title with a wide
    /// progress bar under it that carries its "60 / 100" count in the middle. The row itself has no background.
    /// </summary>
    private AchievementRow BuildAchievementRow(int index)
    {
        AchievementRow row = new AchievementRow();

        GameObject go = new GameObject("Achievement " + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(achievementsContent, false);
        row.background = go.GetComponent<Image>();
        row.background.color = achievementRowColor;   // a panel per achievement, with a gap between them
        row.rect = go.GetComponent<RectTransform>();
        row.rect.anchorMin = new Vector2(0f, 1f);
        row.rect.anchorMax = new Vector2(1f, 1f);
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.sizeDelta = new Vector2(0f, achievementRowHeight);

        float iconSize = Mathf.Min(achievementIconSize, achievementRowHeight - 16f);

        // The pixel's picture (a spinning cube icon until its real look is rendered).
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(go.transform, false);
        row.icon = iconGo.AddComponent<PixelCubeIcon>();
        row.icon.spinDegreesPerSecond = achievementIconSpin;
        RectTransform ir = row.icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
        ir.sizeDelta = new Vector2(iconSize, iconSize);
        ir.anchoredPosition = new Vector2(8f, 0f);

        GameObject skinGo = new GameObject("Skin", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        skinGo.transform.SetParent(go.transform, false);
        row.skin = skinGo.GetComponent<RawImage>();
        row.skin.raycastTarget = false;
        RectTransform sr = row.skin.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(iconSize, iconSize);
        sr.anchoredPosition = new Vector2(8f, 0f);
        skinGo.SetActive(false);

        // The picture sits in its own framed box.
        float boxSize = iconSize + 12f;
        GameObject frame = new GameObject("Icon Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(go.transform, false);
        frame.transform.SetSiblingIndex(0);
        frame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
        frame.GetComponent<Image>().raycastTarget = false;
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
        fr.sizeDelta = new Vector2(boxSize, boxSize);
        fr.anchoredPosition = new Vector2(8f, 0f);
        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(frame.transform, false);
        inner.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        inner.GetComponent<Image>().raycastTarget = false;
        RectTransform inr = inner.GetComponent<RectTransform>();
        inr.anchorMin = Vector2.zero; inr.anchorMax = Vector2.one;
        inr.offsetMin = new Vector2(3f, 3f); inr.offsetMax = new Vector2(-3f, -3f);
        ir.anchoredPosition = new Vector2(8f + 6f, 0f);
        sr.anchoredPosition = new Vector2(8f + 6f, 0f);
        float dividerX = 8f + boxSize - 3f;

        float left = dividerX + 3f + 16f;

        // The title, big and bold.
        row.title = CreateText(go.transform, "Title", "", rowFontSize * 1.05f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        row.title.enableAutoSizing = true;
        row.title.fontSizeMax = rowFontSize * 1.05f;
        row.title.fontSizeMin = 14f;
        PlaceBadgeTitle(row.title.rectTransform, left);

        // The progress bar with its count in the middle.
        BuildBadgeBar(go.transform, left, 0.1f, out row.barFill, out row.barFillRect, out row.progress);

        PixelHoverTip tip = go.AddComponent<PixelHoverTip>();
        tip.onEnter = () => hoverAchievement = row.achIndex;
        tip.onExit = () => { if (hoverAchievement == row.achIndex) hoverAchievement = -1; };

        // (The description is kept only as hidden storage; the tooltip says it all.)
        row.description = CreateText(go.transform, "Description", "", 12f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        row.description.gameObject.SetActive(false);

        return row;
    }

    // ---- The badge rows' shared pieces (achievement rows and goal rows look alike) ----

    /// <summary>A badge row's title: the top half of the row, right of the picture box ('left').</summary>
    private static void PlaceBadgeTitle(RectTransform tr, float left)
    {
        tr.anchorMin = new Vector2(0f, 0.5f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(left, 0f);
        tr.offsetMax = new Vector2(-12f, -6f);
    }

    /// <summary>
    /// A badge row's progress bar: a dark back bar from 'bottom' up to the middle of the row (right of 'left'), a fill anchored
    /// to its left edge (its width is set when the row refreshes) and the count text in the middle.
    /// </summary>
    private void BuildBadgeBar(Transform row, float left, float bottom, out Image barFill, out RectTransform barFillRect, out TMP_Text progress)
    {
        GameObject back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(row, false);
        back.GetComponent<Image>().color = achievementBarBackColor;
        back.GetComponent<Image>().raycastTarget = false;
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, bottom);
        br.anchorMax = new Vector2(1f, 0.46f);
        br.offsetMin = new Vector2(left, 0f);
        br.offsetMax = new Vector2(-12f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(back.transform, false);
        barFill = fill.GetComponent<Image>();
        barFill.raycastTarget = false;
        barFillRect = fill.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = new Vector2(0f, 1f);
        barFillRect.offsetMin = barFillRect.offsetMax = Vector2.zero;

        progress = CreateText(back.transform, "Progress", "", rowFontSize * 0.72f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        progress.enableAutoSizing = true;
        progress.fontSizeMax = rowFontSize * 0.72f;
        progress.fontSizeMin = 10f;
        progress.raycastTarget = false;
        PixelUIKit.Stretch(progress.rectTransform);
    }


    // A pixel's "Collector" and "Clicker" achievements share one row; clicking the row switches which one it shows.
    private readonly List<List<int>> achievementGroups = new List<List<int>>();
    private const string ModePrefKey = "PixelLog.AchievementMode.";

    private bool ShowsClicks(PixelClicker.PixelType type) => PixelPrefs.GetInt(ModePrefKey + (int)type, 0) == 1;

    /// <summary>Groups the achievements: one group per pixel type for the two per-pixel kinds, a group of one for the rest.</summary>
    private void BuildAchievementGroups()
    {
        achievementGroups.Clear();
        int total = achievements != null ? achievements.Count : 0;
        for (int i = 0; i < total; i++) achievementGroups.Add(new List<int> { i });   // every achievement has its own row
    }

    /// <summary>The achievement a group currently shows.</summary>
    private int ChosenAchievement(List<int> group)
    {
        if (group.Count == 1) return group[0];
        PixelClicker.PixelType type = achievements.GetPixelType(group[0]);
        PixelAchievements.Kind want = ShowsClicks(type) ? PixelAchievements.Kind.ClickPixelType : PixelAchievements.Kind.CollectPixelType;
        foreach (int i in group)
            if (achievements.GetKind(i) == want) return i;
        return group[0];
    }


    private const float sectionHeaderHeight = 56f;
    private readonly HashSet<string> collapsedAchievementSections = new HashSet<string>();

    private class SectionHeader
    {
        public Button button;
        public TMP_Text label;
        public string name;
    }
    private readonly List<SectionHeader> achievementSectionHeaders = new List<SectionHeader>();

    /// <summary>Which section an achievement belongs to: what it measures, or (for code-made ones) what it is about.</summary>
    private static string AchievementSection(PixelAchievements.Kind kind, string id)
    {
        if (kind == PixelAchievements.Kind.CollectPixelType || kind == PixelAchievements.Kind.CollectAnyPixel) return "Collected";
        if (kind == PixelAchievements.Kind.ClickPixelType) return "Clicks";
        if (id.StartsWith("pet_")) return "Pets";
        if (id.StartsWith("use_")) return "Items";
        return "Other";
    }

    private static int SectionRank(string name)
    {
        switch (name)
        {
            case "Collected": return 0;
            case "Clicks": return 1;
            case "Pets": return 2;
            case "Items": return 3;
            default: return 9;
        }
    }

    private SectionHeader BuildSectionHeader(int index)
    {
        SectionHeader h = new SectionHeader();
        h.button = CreateButton(achievementsContent, "Section " + index, "", new Vector2(10f, sectionHeaderHeight),
                                ButtonRole.Neutral, amountColor, rowFontSize * 0.9f);
        RectTransform rt = h.button.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, sectionHeaderHeight);
        h.label = h.button.GetComponentInChildren<TMP_Text>();
        h.label.alignment = TextAlignmentOptions.MidlineLeft;
        h.label.fontStyle = FontStyles.Bold;
        h.label.rectTransform.offsetMin = new Vector2(16f, 0f);
        h.button.onClick.AddListener(() =>
        {
            if (!collapsedAchievementSections.Remove(h.name)) collapsedAchievementSections.Add(h.name);
            RefreshAchievementsTab();
        });
        return h;
    }

    private void RefreshAchievementsTab()
    {
        HidePixelsTab();

        BuildAchievementGroups();
        int count = achievementGroups.Count;
        while (achievementRows.Count < count) achievementRows.Add(BuildAchievementRow(achievementRows.Count));

        // Sections (Collected, Clicks, Pets, ...): each one a fold-away heading with its achievements under it.
        List<string> sectionNames = new List<string>();
        List<List<int>> sectionRows = new List<List<int>>();
        for (int i = 0; i < count; i++)
        {
            string name = AchievementSection(achievements.GetKind(achievementGroups[i][0]), achievements.GetId(achievementGroups[i][0]));
            if (achievementSearch.Length > 0)
            {
                int ach = achievementGroups[i][0];
                if (!(achievements.GetTitle(ach) ?? "").ToLowerInvariant().Contains(achievementSearch) &&
                    !(achievements.GetDescription(ach) ?? "").ToLowerInvariant().Contains(achievementSearch) &&
                    !name.ToLowerInvariant().Contains(achievementSearch)) continue;
            }
            int s = sectionNames.IndexOf(name);
            if (s < 0) { sectionNames.Add(name); sectionRows.Add(new List<int>()); s = sectionNames.Count - 1; }
            sectionRows[s].Add(i);
        }
        int[] order = new int[sectionNames.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        System.Array.Sort(order, (p, q) => SectionRank(sectionNames[p]).CompareTo(SectionRank(sectionNames[q])));

        float y = 0f;
        float[] rowY = new float[achievementRows.Count];
        bool[] rowShown = new bool[achievementRows.Count];
        while (achievementSectionHeaders.Count < sectionNames.Count) achievementSectionHeaders.Add(BuildSectionHeader(achievementSectionHeaders.Count));
        for (int h = 0; h < achievementSectionHeaders.Count; h++) achievementSectionHeaders[h].button.gameObject.SetActive(false);
        for (int n = 0; n < order.Length; n++)
        {
            int sIndex = order[n];
            string name = sectionNames[sIndex];
            bool collapsed = achievementSearch.Length == 0 && collapsedAchievementSections.Contains(name);   // a search shows its matches expanded
            int earned = 0, total = sectionRows[sIndex].Count;
            foreach (int r in sectionRows[sIndex]) if (achievements.IsComplete(achievementGroups[r][0])) earned++;

            SectionHeader header = achievementSectionHeaders[n];
            header.name = name;
            header.button.gameObject.SetActive(true);
            PixelUIKit.SetText(header.label, (collapsed ? "+  " : "-  ") + name.ToUpperInvariant() + "  (" + earned + " / " + total + ")");
            header.button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -y);
            y += sectionHeaderHeight + achievementSpacing;

            if (collapsed) continue;
            foreach (int r in sectionRows[sIndex])
            {
                rowY[r] = y;
                rowShown[r] = true;
                y += achievementRowHeight + achievementSpacing;
            }
        }

        for (int i = 0; i < achievementRows.Count; i++)
        {
            AchievementRow row = achievementRows[i];
            bool visible = i < count && rowShown[i];
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            row.groupIndex = i;
            List<int> group = achievementGroups[i];
            int a = ChosenAchievement(group);
            row.achIndex = a;

            bool unlocked = achievements.IsComplete(a);           // every tier earned
            bool started = achievements.HasAnyTier(a);            // at least one tier earned
            float dim = started ? 1f : achievementLockedBrightness;

            int tierCount = achievements.GetTierCount(a);
            string tierText = tierCount > 1
                ? string.Format(achievementTierFormat, Math.Min(achievements.GetEarnedTiers(a) + 1, tierCount), tierCount) : "";
            PixelUIKit.SetText(row.title, achievements.GetTitle(a));
            PixelUIKit.SetText(row.description, achievements.GetDescription(a) + tierText);
            row.title.color = unlocked ? achievementUnlockedColor : new Color(textColor.r * dim, textColor.g * dim, textColor.b * dim, textColor.a);

            double currentTarget = achievements.GetCurrentTarget(a);
            PixelUIKit.SetText(row.progress, unlocked
                ? achievementUnlockedText
                : string.Format(achievementProgressFormat, FormatAmount(Math.Min(achievements.GetProgress(a), currentTarget)), FormatAmount(currentTarget)));
            row.progress.color = unlocked ? achievementUnlockedColor : amountColor;

            Color icon = achievements.GetIconColor(a);
            row.icon.color = new Color(icon.r * dim, icon.g * dim, icon.b * dim, icon.a);
            row.skinTier = achievements.GetSkinTier(a);
            Texture skinTex = SkinIcon(row.skinTier);
            row.skin.gameObject.SetActive(skinTex != null);
            row.icon.gameObject.SetActive(skinTex == null);
            if (skinTex != null) { row.skin.texture = skinTex; float skinDim = Mathf.Lerp(1f, dim, 0.35f); row.skin.color = new Color(skinDim, skinDim, skinDim, 1f); }

            row.barFill.color = unlocked ? achievementUnlockedColor : achievementBarColor;
            row.barFillRect.anchorMax = new Vector2(unlocked ? 1f : achievements.GetFraction(a), 1f);

            row.rect.anchoredPosition = new Vector2(0f, -rowY[i]);
        }

        achievementsContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, y - achievementSpacing));
        noAchievementsLabel.gameObject.SetActive(count == 0 || (achievementSearch.Length > 0 && order.Length == 0));
        int earnedTotal = achievements != null ? achievements.EarnedTierTotal : 0, tierTotal = achievements != null ? achievements.TierTotal : 0;
        PixelUIKit.SetText(achievementSummary, string.Format(achievementSummaryFormat, earnedTotal, tierTotal));
        if (achievementSummaryFillRect != null) achievementSummaryFillRect.anchorMax = new Vector2(tierTotal > 0 ? Mathf.Clamp01(earnedTotal / (float)tierTotal) : 0f, 1f);

        float summaryHeight = rowFontSize * 1.3f;
        panelRect.sizeDelta = new Vector2(panelWidth, FixedPanelHeight);
    }
}
