// ---- System notes ----
// PixelDevConsole.cs
//   CONSOLE (Dev Hub tab 'Console'): the game's log while it runs - also in a built game, where there is no Unity console. Reads PixelLogBuffer.
//   * Filter buttons All / Errors / Warnings / Logs (with counts) and a search box; newest line at the bottom (the list follows new lines while you are at the
//     bottom). Errors red, warnings yellow. Click a line to show / hide its stack trace. A repeated line shows 'x3'.
//   * 'Copy for Claude' copies the last lines shown (with stack traces for errors / warnings) plus the game-state summary of the crash reports
//     (PixelCrashLog.GameStateText): paste that to Claude. 'Clear' empties the list.
//   * Error badge: when an error happens and the Console isn't showing, a small red box '2 errors' appears at the top right of the screen (its own canvas); clicking it opens the Console on Errors. Switch it off with the tick box in the tab (PlayerPrefs PixelClicker.Dev.Console.Badge).
//   One big text with <link> tags instead of a row per line, so thousands of lines cost nothing; rebuilt at most 4 times a second, only while showing.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevConsole : MonoBehaviour
{
    private const float W = PixelDevHub.ContentW, H = PixelDevHub.ContentH, ListTop = 150f, TextW = 650f, RefreshSeconds = 0.25f;
    private const int MaxShown = 300, MaxChars = 60000, CopyLines = 50;
    private const string PrefBadge = "PixelClicker.Dev.Console.Badge";

    private enum Filter { All = 0, Errors = 1, Warnings = 2, Logs = 3 }

    private static PixelDevConsole instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    public static void Boot()
    {
        if (instance != null) return;
        new GameObject("Dev Console").AddComponent<PixelDevConsole>();
    }

    private RectTransform root, content;
    private ScrollRect scroll;
    private GameObject scrollBar;
    private TMP_Text list, status;
    private TMP_InputField search;
    private readonly Image[] filterImages = new Image[4];
    private readonly TMP_Text[] filterLabels = new TMP_Text[4];
    private Toggle badgeToggle;
    private Filter filter = Filter.All;
    private bool built, visible;
    private int shownVersion = -1;
    private float nextRefresh;
    private readonly List<PixelLogBuffer.Entry> snapshot = new List<PixelLogBuffer.Entry>();
    private readonly List<PixelLogBuffer.Entry> matching = new List<PixelLogBuffer.Entry>();
    private readonly HashSet<int> expanded = new HashSet<int>();

    // The on-screen error badge.
    private GameObject badgeCanvas;
    private TMP_Text badgeLabel;
    private bool badgeOn;
    private int badgeShown = -1;

    private void Awake()
    {
        instance = this;
        root = PixelDevHub.TabRoot("console");
        PixelDevHub.SetTabCallback("console", SetVisible);
        badgeOn = PixelPrefs.GetBool(PrefBadge, true);
        BuildBadge();
    }

    private void OnDestroy()
    {
        if (badgeCanvas != null) Destroy(badgeCanvas);
        if (instance == this) instance = null;
    }

    private void SetVisible(bool on)
    {
        visible = on;
        if (!on) return;
        if (!built) Build();
        shownVersion = -1;   // rebuild now
        PixelLogBuffer.MarkSeen();
    }

    private void Build()
    {
        built = true;
        string[] names = { "All", "Errors", "Warnings", "Logs" };
        float bw = (W - 20f - 18f) / 4f;
        for (int i = 0; i < 4; i++)
        {
            Filter f = (Filter)i;
            Button b = PixelDevUI.Button(root, names[i], 10f + i * (bw + 6f), 8f, bw, 38f, PixelDevUI.ButtonColor, () => { filter = f; shownVersion = -1; ShowFilter(); }, 19f);
            filterImages[i] = b.GetComponent<Image>();
            filterLabels[i] = b.GetComponentInChildren<TMP_Text>();
        }

        search = PixelDevUI.Input(root, "Search", 10f, 54f, 330f, 40f, "Search the log");
        PixelUIKit.AddSearchIcon(search);
        search.onValueChanged.AddListener(_ => shownVersion = -1);
        PixelDevUI.Button(root, "Clear", 348f, 54f, 110f, 40f, PixelDevUI.ButtonColor, () => { PixelLogBuffer.Clear(); expanded.Clear(); });
        PixelDevUI.Button(root, "Copy for Claude", 466f, 54f, 224f, 40f, PixelDevUI.Green, CopyForClaude);

        badgeToggle = PixelDevUI.Tick(root, 10f, 104f, 32f, badgeOn);
        badgeToggle.onValueChanged.AddListener(on => { badgeOn = on; PixelPrefs.SetBool(PrefBadge, on); });
        PixelDevUI.Label(root, "Red badge on screen when an error happens", 50f, 100f, 360f, 40f, 18f, PixelDevUI.TextColor);
        status = PixelDevUI.Label(root, "", 420f, 100f, 270f, 40f, 16f, PixelDevUI.DimText, TextAlignmentOptions.MidlineRight);

        scroll = PixelUIKit.CreateScrollView(root, "Log", PixelDevUI.Accent, 10f, 60f, out content, out scrollBar);
        PixelDevUI.PlaceTL(scroll.GetComponent<RectTransform>(), 10f, ListTop, 680f, H - ListTop - 8f);
        list = PixelDevUI.WrapText(content, "Lines", 6f, 0f, TextW, 10f, 16f, PixelDevUI.TextColor);
        list.richText = true;
        list.raycastTarget = true;   // (clicks on a line open its stack trace)
        PixelLinkClick click = list.gameObject.AddComponent<PixelLinkClick>();
        click.text = list;
        click.onLink = id =>
        {
            if (!int.TryParse(id, out int n)) return;
            if (!expanded.Remove(n)) expanded.Add(n);
            shownVersion = -1;
        };
        ShowFilter();
    }

    private void ShowFilter()
    {
        for (int i = 0; i < 4; i++) filterImages[i].color = i == (int)filter ? new Color(0.35f, 0.5f, 0.8f, 1f) : PixelDevUI.ButtonColor;
    }

    private bool Matches(PixelLogBuffer.Entry e, string q)
    {
        switch (filter)
        {
            case Filter.Errors: if (!e.IsError) return false; break;
            case Filter.Warnings: if (e.type != LogType.Warning) return false; break;
            case Filter.Logs: if (e.IsError || e.type == LogType.Warning) return false; break;
        }
        return q.Length == 0 || e.message.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TypeColor(PixelLogBuffer.Entry e) => e.IsError ? "#ff6b6b" : e.type == LogType.Warning ? "#ffd166" : "#9fb3d1";
    private static string TypeName(PixelLogBuffer.Entry e) => e.type == LogType.Exception ? "EXCEPTION" : e.IsError ? "ERROR" : e.type == LogType.Warning ? "WARNING" : "LOG";

    private void Rebuild()
    {
        shownVersion = PixelLogBuffer.Version;
        filterLabels[0].text = "All (" + (PixelLogBuffer.Errors + PixelLogBuffer.Warnings + PixelLogBuffer.Logs) + ")";
        filterLabels[1].text = "Errors (" + PixelLogBuffer.Errors + ")";
        filterLabels[2].text = "Warnings (" + PixelLogBuffer.Warnings + ")";
        filterLabels[3].text = "Logs (" + PixelLogBuffer.Logs + ")";

        PixelLogBuffer.Snapshot(snapshot);
        string q = search.text.Trim();
        matching.Clear();
        foreach (PixelLogBuffer.Entry e in snapshot) if (Matches(e, q)) matching.Add(e);
        int start = Mathf.Max(0, matching.Count - MaxShown);

        StringBuilder sb = new StringBuilder();
        for (int i = start; i < matching.Count; i++)
        {
            PixelLogBuffer.Entry e = matching[i];
            sb.Append("<link=\"").Append(e.id).Append("\"><color=").Append(TypeColor(e)).Append(">[").Append(e.time.ToString("HH:mm:ss")).Append("] ")
              .Append(TypeName(e)).Append("</color> <noparse>").Append(e.message).Append("</noparse>");
            if (e.count > 1) sb.Append(" <color=#8a93a8>x").Append(e.count).Append("</color>");
            sb.Append("</link>\n");
            if (expanded.Contains(e.id) && e.stack.Length > 0)
                sb.Append("<size=85%><color=#7f889c><noparse>").Append(e.stack.TrimEnd()).Append("</noparse></color></size>\n");
        }
        if (sb.Length > MaxChars) sb.Remove(0, sb.Length - MaxChars);   // (a huge burst: keep the newest part)
        if (matching.Count == 0) sb.Append("<color=#8a93a8>Nothing here yet.</color>");

        bool atBottom = scroll.verticalNormalizedPosition <= 0.02f || content.sizeDelta.y <= H - ListTop;
        list.text = sb.ToString();
        float height = list.GetPreferredValues(list.text, TextW, 0f).y + 12f;
        list.rectTransform.sizeDelta = new Vector2(TextW, height);
        PixelUIKit.UpdateScrollView(scroll, scrollBar, height, H - ListTop - 8f);
        if (atBottom) scroll.verticalNormalizedPosition = 0f;   // follow new lines while reading the newest
        status.text = matching.Count > MaxShown ? "showing the newest " + MaxShown + " of " + matching.Count : matching.Count + " line" + (matching.Count == 1 ? "" : "s");
    }

    private void CopyForClaude()
    {
        PixelLogBuffer.Snapshot(snapshot);
        string q = search != null ? search.text.Trim() : "";
        matching.Clear();
        foreach (PixelLogBuffer.Entry e in snapshot) if (Matches(e, q)) matching.Add(e);

        StringBuilder sb = new StringBuilder();
        sb.Append("// Pixel Clicker console - ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(", Unity ").Append(Application.unityVersion)
          .Append(", filter ").Append(filter).Append('\n');
        for (int i = Mathf.Max(0, matching.Count - CopyLines); i < matching.Count; i++)
        {
            PixelLogBuffer.Entry e = matching[i];
            sb.Append('[').Append(e.time.ToString("HH:mm:ss")).Append("] ").Append(TypeName(e)).Append(": ").Append(e.message);
            if (e.count > 1) sb.Append(" (x").Append(e.count).Append(')');
            sb.Append('\n');
            if ((e.IsError || e.type == LogType.Warning) && e.stack.Length > 0) sb.Append(e.stack.TrimEnd()).Append('\n');
        }
        string state = PixelCrashLog.GameStateText();
        if (!string.IsNullOrEmpty(state)) sb.Append("\n// Game state\n").Append(state).Append('\n');
        GUIUtility.systemCopyBuffer = sb.ToString();
        if (status != null) status.text = "copied - paste it to Claude";
    }

    // ------------------------------------------------------------------
    // The on-screen error badge
    // ------------------------------------------------------------------

    private void BuildBadge()
    {
        badgeCanvas = PixelUIKit.CreateCanvas("Dev Error Badge", 690, new Vector2(1920f, 1080f), true);
        Button b = PixelUIKit.CreateButton(PixelDevUI.Font, badgeCanvas.transform, "Error Badge", "", new Vector2(200f, 40f), PixelDevUI.Red, Color.white, 19f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-16f, -96f);
        badgeLabel = b.GetComponentInChildren<TMP_Text>();
        b.onClick.AddListener(() =>
        {
            filter = Filter.Errors;
            PixelDevHub.OpenTab("console");
            if (built) { ShowFilter(); shownVersion = -1; }
        });
        badgeCanvas.SetActive(false);
    }

    private void Update()
    {
        if (visible)
        {
            PixelLogBuffer.MarkSeen();
            if (built && shownVersion != PixelLogBuffer.Version && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                Rebuild();
            }
        }

        int unseen = PixelLogBuffer.UnseenErrors;
        bool showBadge = unseen > 0 && !visible && badgeOn;
        if (badgeCanvas != null && badgeCanvas.activeSelf != showBadge) badgeCanvas.SetActive(showBadge);
        if (showBadge && badgeLabel != null && badgeShown != unseen)
        {
            badgeShown = unseen;
            badgeLabel.text = unseen + (unseen == 1 ? " error - click" : " errors - click");
        }
    }
}
