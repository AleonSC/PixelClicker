// ---- System notes ----
// PixelDevToolsDocs.cs
//   Part of PixelDevTools (partial class): the To-Do and Changelog views of the Dev Hub Docs tab. The system notes are in PixelDevTools.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelDevTools
{
    private GameObject todoView, changelogView;
    private TMP_Text changelogLabel;
    private ScrollRect changelogScroll;
    private GameObject changelogBar;
    private RectTransform changelogViewRect;
    private RectTransform todoContent;
    private ScrollRect todoScroll;
    private GameObject todoBar;
    private RectTransform todoViewRect;


    /// <summary>The Changelog tab: the lines of Resources/Changelog.txt, newest first, in one scrolling text.</summary>
    private void BuildChangelogView(Transform box, float top)
    {
        changelogView = new GameObject("Changelog View", typeof(RectTransform));
        changelogView.transform.SetParent(box, false);
        PixelUIKit.Stretch(changelogView.GetComponent<RectTransform>());

        changelogScroll = PixelUIKit.CreateScrollView(changelogView.transform, "Changelog Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 16f, 60f,
                                                      out RectTransform content, out changelogBar);
        changelogViewRect = changelogScroll.GetComponent<RectTransform>();
        changelogViewRect.anchorMin = Vector2.zero;
        changelogViewRect.anchorMax = Vector2.one;
        changelogViewRect.offsetMin = new Vector2(40f, 30f);
        changelogViewRect.offsetMax = new Vector2(-40f, -top);

        changelogLabel = PixelUIKit.CreateText(font, content, "Changelog Text", "", fontSize * 0.8f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        changelogLabel.raycastTarget = false;
        RectTransform lr = changelogLabel.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(0f, 0f);
        lr.offsetMax = new Vector2(-30f, 0f);
    }

    private void RebuildChangelog()
    {
        if (changelogLabel == null) return;
        TextAsset asset = Resources.Load<TextAsset>(changelogResource);
        List<string> entries = new List<string>();
        if (asset != null)
            foreach (string raw in asset.text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0) entries.Add(line);
            }
        entries.Reverse();   // newest first
        string text = entries.Count > 0 ? string.Join("\n\n", entries) : "Nothing in the changelog yet.";
        changelogLabel.text = text;
        float width = Mathf.Max(100f, changelogViewRect.rect.width - 30f);
        float height = Mathf.Ceil(changelogLabel.GetPreferredValues(text, width, 0f).y) + 10f;
        changelogLabel.rectTransform.sizeDelta = new Vector2(-30f, height);
        PixelUIKit.UpdateScrollView(changelogScroll, changelogBar, height, changelogViewRect.rect.height);
    }

    /// <summary>The To-Do tab: a scrolling list of the lines in Resources/Todo.txt, plus a Close button.</summary>
    private void BuildTodoView(Transform box, float top, float inner)
    {
        todoView = new GameObject("Todo View", typeof(RectTransform));
        todoView.transform.SetParent(box, false);
        PixelUIKit.Stretch(todoView.GetComponent<RectTransform>());

        todoScroll = PixelUIKit.CreateScrollView(todoView.transform, "Todo Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 16f, 60f,
                                                 out todoContent, out todoBar);
        todoViewRect = todoScroll.GetComponent<RectTransform>();
        todoViewRect.anchorMin = Vector2.zero;
        todoViewRect.anchorMax = Vector2.one;
        todoViewRect.offsetMin = new Vector2(40f, 30f);
        todoViewRect.offsetMax = new Vector2(-40f, -top);

    }

    private readonly List<GameObject> todoLines = new List<GameObject>();

    private void RebuildTodo()
    {
        if (todoContent == null) return;
        foreach (GameObject g in todoLines) if (g != null) Destroy(g);
        todoLines.Clear();

        TextAsset asset = Resources.Load<TextAsset>(todoResource);
        string[] lines = asset != null ? asset.text.Split('\n') : new string[0];
        float width = Mathf.Max(100f, todoViewRect.rect.width - 40f);
        float y = 0f;
        float size = fontSize * 0.85f;
        int count = 0;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            bool heading = line.StartsWith("#");
            bool done = line.StartsWith("[x]") || line.StartsWith("[X]");
            string text = heading ? line.TrimStart('#', ' ') : done ? "[x] " + line.Substring(3).Trim()
                        : line.StartsWith("[ ]") ? "[ ] " + line.Substring(3).Trim() : line;

            TMP_Text label = PixelUIKit.CreateText(font, todoContent, "Todo Line", text, heading ? size * 1.15f : size,
                                                   TextAlignmentOptions.TopLeft, heading ? FontStyles.Bold : FontStyles.Normal,
                                                   done ? todoDoneColor : textColor);
            label.raycastTarget = false;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            float height = label.GetPreferredValues(text, width, 0f).y;
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, -y);
            y += height + 14f;
            todoLines.Add(label.gameObject);
            count++;
        }

        if (count == 0)
        {
            TMP_Text empty = PixelUIKit.CreateText(font, todoContent, "Todo Empty", "Nothing on the to-do list yet.", size,
                                                   TextAlignmentOptions.TopLeft, FontStyles.Italic, textColor);
            RectTransform er = empty.rectTransform;
            er.anchorMin = er.anchorMax = er.pivot = new Vector2(0f, 1f);
            er.sizeDelta = new Vector2(width, size * 1.5f);
            er.anchoredPosition = Vector2.zero;
            todoLines.Add(empty.gameObject);
            y = size * 1.5f;
        }
        PixelUIKit.UpdateScrollView(todoScroll, todoBar, y, todoViewRect.rect.height);
    }
}
