// ---- System notes ----
// PixelLogGoals.cs
//   Part of PixelLog (partial class): the Goals tab: building its list, the goal rows, collecting the goals (next unlock, PixelGoals table, shop / skill goals, minigame trackers) and RefreshGoalsTab. The system notes are in PixelLog.cs.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelLog
{
    /// <summary>The scrolling list of the Goals tab (progress bars for trackers and the next pixel unlock).</summary>
    private void BuildGoalsGroup()
    {
        BuildScrollGroup("Goals", noGoalsText, false, out goalsGroup, out goalsContent, out TMP_Text unusedSummary, out noGoalsLabel);
    }

    /// <summary>A panel-sized group with an optional summary line and a scroll view + scroll bar (a tab's content).</summary>
    private void BuildScrollGroup(string groupName, string emptyMessage, bool withSummary, out GameObject group,
                                  out RectTransform contentRect, out TMP_Text summary, out TMP_Text empty)
    {
        group = new GameObject(groupName, typeof(RectTransform));
        group.transform.SetParent(panelObject.transform, false);
        RectTransform gr = group.GetComponent<RectTransform>();
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = gr.offsetMax = Vector2.zero;

        float summaryHeight = rowFontSize * 1.3f;
        summary = null;
        if (withSummary)
        {
            // A fancy progress bar: a rounded dark trough, a coloured fill with a lighter strip along its top, and the count centred over it.
            GameObject summaryBarGo = new GameObject("Summary Bar", typeof(RectTransform), typeof(Image));
            summaryBarGo.transform.SetParent(group.transform, false);
            Image trough = summaryBarGo.GetComponent<Image>();
            trough.color = new Color(0.07f, 0.07f, 0.1f, 1f);
            PixelUIKit.StyleButtonLook(trough);   // (the button shape only: a progress trough must not take the custom button colour)
            RectTransform sr = summaryBarGo.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.sizeDelta = new Vector2(-panelPadding * 2f, summaryHeight);
            sr.anchoredPosition = new Vector2(0f, -ContentTop);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(summaryBarGo.transform, false);
            achievementSummaryFill = fillGo.GetComponent<Image>();
            achievementSummaryFill.color = achievementUnlockedColor;
            achievementSummaryFill.raycastTarget = false;
            achievementSummaryFillRect = fillGo.GetComponent<RectTransform>();
            achievementSummaryFillRect.anchorMin = Vector2.zero;
            achievementSummaryFillRect.anchorMax = new Vector2(0f, 1f);
            achievementSummaryFillRect.offsetMin = new Vector2(4f, 4f);
            achievementSummaryFillRect.offsetMax = new Vector2(-4f, -4f);

            GameObject shineGo = new GameObject("Shine", typeof(RectTransform), typeof(Image));
            shineGo.transform.SetParent(fillGo.transform, false);
            Image shine = shineGo.GetComponent<Image>();
            shine.color = new Color(1f, 1f, 1f, 0.28f);
            shine.raycastTarget = false;
            RectTransform shr = shineGo.GetComponent<RectTransform>();
            shr.anchorMin = new Vector2(0f, 0.5f); shr.anchorMax = Vector2.one;
            shr.offsetMin = shr.offsetMax = Vector2.zero;

            summary = CreateText(summaryBarGo.transform, "Summary", "", rowFontSize * 0.8f,
                                 TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
            summary.enableAutoSizing = true;
            summary.fontSizeMax = rowFontSize * 0.8f;
            summary.fontSizeMin = 10f;
            summary.raycastTarget = false;
            PixelUIKit.Stretch(summary.rectTransform);
        }

        float viewTop = withSummary ? ContentTop + summaryHeight + 6f : ContentTop;
        if (withSummary)
        {
            // A search box between the total bar and the first heading, exactly as wide as the bar.
            float searchHeight = rowFontSize * 1.3f;
            achievementSearchField = PixelUIKit.CreateInputField(font, group.transform, "Search", new Vector2(0f, searchHeight),
                                                                 achievementRowColor, textColor, rowFontSize * 0.75f, searchPlaceholder);
            PixelUIKit.AddSearchIcon(achievementSearchField);
            RectTransform sfr = achievementSearchField.GetComponent<RectTransform>();
            sfr.anchorMin = new Vector2(0f, 1f);
            sfr.anchorMax = new Vector2(1f, 1f);
            sfr.pivot = new Vector2(0.5f, 1f);
            sfr.sizeDelta = new Vector2(-panelPadding * 2f, searchHeight);
            sfr.anchoredPosition = new Vector2(0f, -viewTop);
            achievementSearchField.onValueChanged.AddListener(text =>
            {
                achievementSearch = (text ?? "").Trim().ToLowerInvariant();
                if (achievementsContent != null) achievementsContent.anchoredPosition = Vector2.zero;
                if (currentTab == 1) RefreshAchievementsTab();
            });
            viewTop += searchHeight + 6f;
        }

        // Scroll view (also the viewport; the transparent image lets empty space take wheel/drag input).
        GameObject view = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(group.transform, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        RectTransform vr = view.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(0.5f, 1f);
        float viewHeight = FixedPanelHeight - viewTop - panelPadding;   // fills the fixed window under the tabs (and the summary line)
        vr.sizeDelta = new Vector2(-(panelPadding * 2f), viewHeight);   // exactly as wide as the tab row above: the rows line up with the tabs
        vr.anchoredPosition = new Vector2(0f, -viewTop);

        contentRect = PixelUIKit.CreateScrollContent(view.transform);

        // Scrollbar
        Scrollbar scrollbar = PixelUIKit.CreateScrollBar(group.transform, scrollbarTrackColor);
        RectTransform br = scrollbar.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 1f);
        float barWidth = PixelUIKit.ScrollBarWidth;                               // slim, rounded, and centred in the margin beside the rows
        br.sizeDelta = new Vector2(barWidth, viewHeight);
        br.anchoredPosition = new Vector2(-(panelPadding - barWidth) * 0.5f, -viewTop);

        ScrollRect scroll = view.GetComponent<ScrollRect>();
        PixelUIKit.SetupVerticalScroll(scroll, contentRect, scrollbar, achievementScrollSpeed);

        empty = CreateText(view.transform, "Empty", emptyMessage.ToUpperInvariant(), rowFontSize, // capitals, centred in the window
                                         TextAlignmentOptions.Center, FontStyles.Bold,
                                         new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform er = empty.rectTransform;
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = er.offsetMax = Vector2.zero;
        empty.gameObject.SetActive(false);

        group.SetActive(false);
    }


    private class GoalRow
    {
        public RectTransform rect;
        public TMP_Text title, progress;
        public Image barFill;
        public RectTransform barFillRect;
        public PixelCubeIcon icon;
        public RawImage skin;
        public int skinTier = -1;
        public int goalIndex;
    }

    private readonly List<GoalRow> goalRows = new List<GoalRow>();
    private readonly List<Goal> shownGoals = new List<Goal>();
    private int hoverGoal = -1;
    /// <summary>
    /// One goal per row, laid out like an achievement badge: a framed box with a live 3D picture of the goal's item on the left,
    /// the title big and bold, and a wide bar with the count in the middle. The explanation and exact numbers show on hover.
    /// </summary>
    private GoalRow BuildGoalRow(int index)
    {
        GoalRow row = new GoalRow { goalIndex = index };
        GameObject go = new GameObject("Goal " + index, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(goalsContent, false);
        go.GetComponent<Image>().color = achievementRowColor;
        row.rect = go.GetComponent<RectTransform>();
        row.rect.anchorMin = new Vector2(0f, 1f);
        row.rect.anchorMax = new Vector2(1f, 1f);
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.sizeDelta = new Vector2(0f, goalRowHeight);

        // The framed picture box: a live 3D picture of the item the goal is about (a spinning cube when there is none).
        float boxSize = goalRowHeight - 24f;
        GameObject frame = new GameObject("Icon Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(go.transform, false);
        frame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
        frame.GetComponent<Image>().raycastTarget = false;
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(0f, 0.5f);
        fr.sizeDelta = new Vector2(boxSize, boxSize);
        fr.anchoredPosition = new Vector2(10f, 0f);
        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(frame.transform, false);
        inner.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        inner.GetComponent<Image>().raycastTarget = false;
        RectTransform inr = inner.GetComponent<RectTransform>();
        inr.anchorMin = Vector2.zero; inr.anchorMax = Vector2.one;
        inr.offsetMin = new Vector2(3f, 3f); inr.offsetMax = new Vector2(-3f, -3f);

        float iconSize = boxSize - 12f;
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
        iconGo.transform.SetParent(inner.transform, false);
        row.icon = iconGo.AddComponent<PixelCubeIcon>();
        row.icon.spinDegreesPerSecond = achievementIconSpin;
        RectTransform icr = row.icon.rectTransform;
        icr.anchorMin = icr.anchorMax = icr.pivot = new Vector2(0.5f, 0.5f);
        icr.sizeDelta = new Vector2(iconSize, iconSize);
        icr.anchoredPosition = Vector2.zero;

        GameObject skinGo = new GameObject("Skin", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        skinGo.transform.SetParent(inner.transform, false);
        row.skin = skinGo.GetComponent<RawImage>();
        row.skin.raycastTarget = false;
        RectTransform skr = row.skin.rectTransform;
        skr.anchorMin = skr.anchorMax = skr.pivot = new Vector2(0.5f, 0.5f);
        skr.sizeDelta = new Vector2(iconSize, iconSize);
        skr.anchoredPosition = Vector2.zero;
        skinGo.SetActive(false);

        float left = 10f + boxSize + 16f;

        row.title = CreateText(go.transform, "Title", "", rowFontSize * 1.0f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        row.title.enableAutoSizing = true;
        row.title.fontSizeMax = rowFontSize * 1.0f;
        row.title.fontSizeMin = 14f;
        row.title.raycastTarget = false;
        PlaceBadgeTitle(row.title.rectTransform, left);

        BuildBadgeBar(go.transform, left, 0.12f, out row.barFill, out row.barFillRect, out row.progress);

        PixelHoverTip tip = go.AddComponent<PixelHoverTip>();
        tip.onEnter = () => hoverGoal = row.goalIndex;
        tip.onExit = () => { if (hoverGoal == row.goalIndex) hoverGoal = -1; };
        return row;
    }

    private PixelShop goalShop;

    private struct Goal
    {
        public string title, description;
        public double count, goal;
        public int skinTier;        // tier whose 3D look is the goal's picture (-1 = a spinning cube in 'iconColor')
        public Color iconColor;
    }

    /// <summary>The pixel type that stands for a minigame goal (its tracker's item).</summary>
    private int GoalSkinTier(string minigameId)
    {
        PixelClicker.PixelType type;
        switch (minigameId)
        {
            case "ghost": type = PixelClicker.PixelType.Ghost; break;
            case "blackhole": type = PixelClicker.PixelType.Singularity; break;
            case "meteor": type = PixelClicker.PixelType.Meteor; break;
            case "stardust": type = PixelClicker.PixelType.Solar; break;
            default: return -1;
        }
        return clicker.IndexOf(type);
    }

    /// <summary>The progress bars: the next pixel unlock, then every minigame tracker that is in play (ghosts caught, singularity, stardust...).</summary>
    /// <summary>The goals the Goals tab shows right now (the player's own window pins them): title, current count, goal.</summary>
    public List<(string title, double count, double goal)> CurrentGoals()
    {
        List<(string, double, double)> list = new List<(string, double, double)>();
        if (clicker == null) return list;
        foreach (Goal g in CollectGoals()) list.Add((g.title, g.count, g.goal));
        return list;
    }

    private List<Goal> CollectGoals()
    {
        List<Goal> goals = new List<Goal>();

        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        for (int i = 1; i < tiers.Length; i++)
        {
            PixelClicker.PixelTier t = tiers[i];
            bool byThreshold = t.unlockMode == PixelClicker.TierUnlockMode.PreviousTierThreshold || (t.unlockMode == PixelClicker.TierUnlockMode.ByGoal && !PixelGoals.UnlocksTier(t.type));   // (a ByGoal tier shows its goal row instead, PixelGoals)
            if (t.unlocked || !byThreshold || t.unlockThreshold <= 0d) continue;
            goals.Add(new Goal
            {
                title = string.Format(nextPixelGoalFormat, t.displayName),
                description = string.Format(nextPixelGoalDescription, tiers[i - 1].displayName),
                count = tiers[i - 1].totalCollected,
                goal = t.unlockThreshold,
                skinTier = i,
                iconColor = t.color,
            });
            break; // only the next one
        }

        // The data-driven goals (PixelGoals: crush / recycle / streak goals that alternate with the click and unlock goals), each with its own reward.
        AddTableGoals(goals);

        // Cubie's bill (PixelCubieBill): the next trust tier, pinned once the bill is revealed.
        if (PixelCubieBill.NextGoal(out string billTitle, out string billText, out double billHave, out double billNeed))
            goals.Add(new Goal { title = billTitle, description = billText, count = billHave, goal = billNeed, skinTier = -1, iconColor = new Color(0.3f, 0.85f, 0.75f, 1f) });

        // Before RGB: earn the Black that brings Cubie's second visit (he unlocks Red, Green and Blue and opens the shop).
        if (goalShop == null) goalShop = PixelFind.First<PixelShop>();
        PixelShop shop = goalShop;
        int blackLeft = PixelGuideVendor.BlackUntilVisitTwo;
        int blackIndex = clicker.IndexOf(PixelClicker.PixelType.Black);
        if (blackLeft > 0 && blackIndex >= 0 && tiers[blackIndex].unlocked)
        {
            double earned = tiers[blackIndex].totalCollected;
            goals.Add(new Goal
            {
                title = string.Format(nextPixelGoalFormat, "Red, Green and Blue"),
                description = string.Format(nextPixelGoalDescription, tiers[blackIndex].displayName) + " Cubie will drop by.",
                count = earned,
                goal = earned + blackLeft,
                skinTier = clicker.IndexOf(PixelClicker.PixelType.Red),
                iconColor = new Color(1f, 0.3f, 0.3f, 1f),
            });
        }

        // After RGB the shop's first buy is the Auto Clicker (the bar fills as its price is saved up).
        if (shop != null && shop.ShopAvailable && clicker.IsUnlocked(PixelClicker.PixelType.Red) && !shop.AutoClickerBought() &&
            shop.TryGetAutoClickerGoal(out string autoName, out string autoCost, out double autoHave, out double autoNeed))
        {
            goals.Add(new Goal
            {
                title = string.Format(buyRgbGoalFormat, autoName),
                description = string.Format(buyRgbGoalDescription, autoCost),
                count = autoHave,
                goal = autoNeed,
                skinTier = -1,
                iconColor = new Color(0.4f, 0.8f, 1f, 1f),
            });
        }

        // After the Auto Clicker (and Cubie's Toggles tutorial that hints at skills; with tips off straight away): buy the Lumberjack's Pack, the first skill.
        if (shop != null && shop.ShopAvailable && shop.AutoClickerBought() && (PixelStats.Total(PixelTogglesTutorial.DoneKey) >= 1d || !PixelHints.TipsEnabled) &&
            shop.TryGetWoodcutterGoal(out string woodName, out string woodCost, out double woodHave, out double woodNeed))
        {
            goals.Add(new Goal
            {
                title = string.Format(buyRgbGoalFormat, woodName),
                description = string.Format(buyRgbGoalDescription, woodCost),
                count = woodHave,
                goal = woodNeed,
                skinTier = clicker.IndexOf(PixelClicker.PixelType.Wood),
                iconColor = new Color(0.6f, 0.4f, 0.22f, 1f),
            });
        }

        foreach (PixelMinigame m in PixelMinigame.All)
        {
            if (m == null || !m.HasTracker || !(m.Running || m.TrackerCount > 0d)) continue;
            goals.Add(new Goal
            {
                title = m.TrackerTitle, description = m.TrackerDescription, count = m.TrackerCount, goal = Math.Max(1d, m.TrackerGoal),
                skinTier = GoalSkinTier(m.Id), iconColor = new Color(1f, 0.85f, 0.3f, 1f),
            });
        }
        return goals;
    }

    private readonly List<PixelGoals.GoalDef> tableGoals = new List<PixelGoals.GoalDef>();

    /// <summary>Adds the PixelGoals rows that are showing now (appeared, not done - or just done) to the Goals tab's list.</summary>
    private void AddTableGoals(List<Goal> goals)
    {
        PixelGoals table = PixelGoals.Instance;
        if (table == null) return;
        table.ActiveGoals(tableGoals);
        foreach (PixelGoals.GoalDef d in tableGoals)
        {
            string reward = table.RewardText(d);
            goals.Add(new Goal
            {
                title = d.title,
                description = d.description + (reward.Length > 0 ? "\n<color=#ffd54a>Reward: " + reward + "</color>" : ""),
                count = PixelGoals.Done(d) ? d.target : table.Progress(d),
                goal = d.target,
                skinTier = -1,
                iconColor = d.counter == PixelGoals.CounterKind.ManualClicksSinceShown ? new Color(0.9f, 0.9f, 0.9f, 1f) : new Color(1f, 0.6f, 0.25f, 1f),
            });
        }
    }

    private void RefreshGoalsTab()
    {
        HidePixelsTab();

        List<Goal> goals = CollectGoals();
        shownGoals.Clear();
        shownGoals.AddRange(goals);
        while (goalRows.Count < goals.Count) goalRows.Add(BuildGoalRow(goalRows.Count));

        float y = 0f;
        for (int i = 0; i < goalRows.Count; i++)
        {
            GoalRow row = goalRows[i];
            bool visible = i < goals.Count;
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            Goal g = goals[i];
            bool done = g.count >= g.goal;
            float fraction = Mathf.Clamp01((float)(g.count / g.goal));
            PixelUIKit.SetText(row.title, g.title);
            PixelUIKit.SetText(row.progress, done ? achievementUnlockedText : string.Format(achievementProgressFormat, FormatAmount(g.count), FormatAmount(g.goal)));
            Color accent = done ? achievementUnlockedColor : achievementBarColor;
            row.title.color = done ? achievementUnlockedColor : textColor;
            row.barFill.color = accent;
            row.skinTier = g.skinTier;
            Texture skinTex = g.skinTier >= 0 ? SkinIcon(g.skinTier) : null;
            row.skin.gameObject.SetActive(skinTex != null);
            row.icon.gameObject.SetActive(skinTex == null);
            if (skinTex != null) row.skin.texture = skinTex; else row.icon.color = g.iconColor;
            row.barFillRect.anchorMax = new Vector2(fraction, 1f);

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += goalRowHeight + achievementSpacing;
        }

        goalsContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, y - achievementSpacing));
        noGoalsLabel.gameObject.SetActive(goals.Count == 0);
        panelRect.sizeDelta = new Vector2(panelWidth, FixedPanelHeight);
    }
}
