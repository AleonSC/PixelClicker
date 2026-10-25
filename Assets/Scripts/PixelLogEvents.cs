// ---- System notes ----
// PixelLogEvents.cs
//   Part of partial class PixelLog: the EVENT LOG tab (the Log window's third tab, since 2026-10-25). It used to be a box on the game screen
//   (bottom left, PixelHints' overlay) and a tab in the Pocket; both are gone. The lines are PixelHints' history (PixelGameState.Current.eventLog,
//   PixelHints.Announce adds them), oldest at the top and the newest at the bottom (the list opens scrolled down to it); a click on a line shows its
//   full tip (PixelNotice). The list is rebuilt only when the history changes (PixelHints.HistoryVersion) or the text size does
//   (Settings > Accessibility > Event log size -> PixelHints.RefreshLog -> RefreshEventLog). The event log key (Enter by default) opens the Log
//   on this tab (ShowEventLogTab) and closes it again (PixelHints.UpdateLogKey). Settings: PixelLog.cs "Event Log Tab".
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Part of <see cref="PixelLog"/>: the Event log tab (every event line, a click shows its full tip).</summary>
public partial class PixelLog
{
    private const int EventsTab = 3;

    private GameObject eventsGroup;
    private ScrollRect eventsScroll;
    private RectTransform eventsContent;
    private GameObject eventsBar;
    private int eventsVersionShown = -1;   // PixelHints.HistoryVersion the list was built for (-1 = rebuild)

    /// <summary>Is the Log showing its Event log tab (open or not)?</summary>
    public static bool EventLogTabShowing => logInstance != null && logInstance.currentTab == EventsTab;

    /// <summary>Opens the Log on its Event log tab (the event log key, the tutorial's event log step).</summary>
    public static void ShowEventLogTab()
    {
        PixelLog log = logInstance;
        if (log == null) return;
        log.currentTab = EventsTab;
        if (!LogOpen) SetLogOpen(true);
        if (log.built) log.Refresh();
    }

    /// <summary>Redraws the Event log tab's lines the next time it shows (a new text size).</summary>
    public static void RefreshEventLog()
    {
        if (logInstance != null) logInstance.eventsVersionShown = -1;
    }

    /// <summary>The tab's page: one scroll view under the tabs, filling the window.</summary>
    private void BuildEventsGroup()
    {
        eventsGroup = new GameObject("Event Log", typeof(RectTransform));
        eventsGroup.transform.SetParent(panelObject.transform, false);
        PixelUIKit.Stretch(eventsGroup.GetComponent<RectTransform>());
        eventsScroll = PixelUIKit.CreateScrollView(eventsGroup.transform, "Event Log List", scrollbarHandleColor, 12f, achievementScrollSpeed, out eventsContent, out eventsBar);
        RectTransform vr = eventsScroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(panelPadding, panelPadding);
        vr.offsetMax = new Vector2(-panelPadding, -ContentTop);
        eventsGroup.SetActive(false);
    }

    private void RefreshEventsTab()
    {
        if (eventsContent == null || eventsVersionShown == PixelHints.HistoryVersion) return;
        eventsVersionShown = PixelHints.HistoryVersion;
        for (int i = eventsContent.childCount - 1; i >= 0; i--) Destroy(eventsContent.GetChild(i).gameObject);

        float fontSize = Mathf.Max(12f, eventLogFontSize * PixelHints.LogSizeScaleNow);
        float width = Mathf.Max(200f, panelWidth - panelPadding * 2f - 24f);
        float y = 4f;
        int count = PixelHints.HistoryCount;
        for (int i = 0; i < count; i++)
        {
            if (!PixelHints.TryGetHistory(i, out string text, out string full)) continue;
            GameObject go = new GameObject("Line " + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(eventsContent, false);
            go.GetComponent<Image>().color = achievementRowColor;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            TMP_Text label = CreateText(rt, "Text", text, fontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            float h = Mathf.Ceil(label.GetPreferredValues(text, width - 24f, 0f).y) + 16f;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(12f, 8f); lr.offsetMax = new Vector2(-12f, -8f);
            rt.sizeDelta = new Vector2(-16f, h);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += h + 6f;
            bool opens = !string.IsNullOrEmpty(full);
            string tip = full;
            Button b = PixelUIKit.MakeClickable(go, opens ? () => PixelNotice.Show(tip, 0f) : (UnityEngine.Events.UnityAction)null, sound: opens);
            b.interactable = opens;
        }
        if (count == 0)
        {
            TMP_Text empty = CreateText(eventsContent, "Empty", noEventsText, fontSize, TextAlignmentOptions.Center, FontStyles.Italic,
                                        new Color(textColor.r, textColor.g, textColor.b, 0.6f));
            RectTransform er = empty.rectTransform;
            er.anchorMin = new Vector2(0f, 1f); er.anchorMax = new Vector2(1f, 1f); er.pivot = new Vector2(0.5f, 1f);
            er.sizeDelta = new Vector2(0f, 60f); er.anchoredPosition = new Vector2(0f, -10f);
            y = 70f;
        }
        Canvas.ForceUpdateCanvases();
        PixelUIKit.UpdateScrollView(eventsScroll, eventsBar, y + 4f, eventsScroll.GetComponent<RectTransform>().rect.height);
        eventsScroll.verticalNormalizedPosition = 0f;   // the newest line is at the bottom
    }
}
