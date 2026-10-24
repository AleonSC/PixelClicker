// ---- System notes ----
// PixelMyWindowRows.cs
//   Part of PixelMyWindow (partial class): the row builders of the pinned cards (currency with its per-minute rate, skill, goal, achievement, note, favorite) and their small UI helpers. The system notes are in PixelMyWindow.cs.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelMyWindow
{
    private RectTransform NewRow(float height)
    {
        GameObject go = new GameObject("Row", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(listContent, false);
        Image bg = go.GetComponent<Image>();
        bg.color = rowColor;
        PixelUIKit.StyleBox(bg);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-6f, height);
        return rt;
    }

    private TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal)
    {
        TMP_Text t = PixelUIKit.CreateText(Font, parent, name, text, size, align, style, color);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private static void Stretch(RectTransform rt, float x, float y)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(x, y); rt.offsetMax = new Vector2(-x, -y);
    }

    private Row BuildRow(Entry e, string sub, bool first, int entryIndex)
    {
        Row row = null;
        switch (e.type)
        {
            case "currency": row = BuildCurrency(e); break;
            case "skill": row = BuildSkill(e, sub); break;
            case "goal": row = BuildGoal(e, sub); break;
            case "achievement": row = BuildAchievement(e); break;
            case "note": row = BuildNote(e); break;
            case "fav": row = BuildFavorite(e); break;
        }
        if (row == null) return null;
        row.entry = e;
        row.sub = sub;
        row.first = first;
        if (editing && first) AddEditButtons(row, entryIndex);
        return row;
    }

    private void AddEditButtons(Row row, int index)
    {
        // Right end of the row: up, down, remove (and the rate toggle for a currency).
        float x = -6f;
        Button remove = MiniButton(row.rt, "x", ref x, ButtonRole.Danger);
        remove.onClick.AddListener(() => { blob.entries.Remove(row.entry); SaveEntries(); RebuildRows(); });
        Button down = MiniButton(row.rt, "v", ref x, ButtonRole.Neutral);
        down.onClick.AddListener(() => MoveEntry(row.entry, 1));
        Button up = MiniButton(row.rt, "^", ref x, ButtonRole.Neutral);
        up.onClick.AddListener(() => MoveEntry(row.entry, -1));
        if (row.entry.type == "currency")
        {
            Button rate = MiniButton(row.rt, "/min", ref x, ButtonRole.Neutral, 62f);
            rate.onClick.AddListener(() => { row.entry.flag = !row.entry.flag; SaveEntries(); RefreshValues(); });
        }
        // Make room: the row's own texts stop before the buttons.
        foreach (TMP_Text t in row.rt.GetComponentsInChildren<TMP_Text>())
            if (t.name == "Amount" || t.name == "Detail") t.rectTransform.offsetMax = new Vector2(x - 4f, t.rectTransform.offsetMax.y);
    }

    private Button MiniButton(RectTransform parent, string label, ref float x, ButtonRole role, float width = 34f)
    {
        Button b = PixelUIKit.CreateButton(Font, parent, "Edit " + label, label, new Vector2(width, 34f), role, 18f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0.5f);
        r.anchoredPosition = new Vector2(x, 0f);
        x -= width + 4f;
        return b;
    }

    private void MoveEntry(Entry e, int delta)
    {
        int i = blob.entries.IndexOf(e);
        int j = Mathf.Clamp(i + delta, 0, blob.entries.Count - 1);
        if (i < 0 || i == j) return;
        blob.entries.RemoveAt(i);
        blob.entries.Insert(j, e);
        SaveEntries();
        RebuildRows();
    }

    private (Image fill, RectTransform bar) MakeBar(RectTransform parent, float bottom, float height)
    {
        GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(parent, false);
        track.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 1f);
        PixelUIKit.StyleBox(track.GetComponent<Image>(), true);
        RectTransform tr = track.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 0f); tr.pivot = new Vector2(0.5f, 0f);
        tr.offsetMin = new Vector2(10f, bottom); tr.offsetMax = new Vector2(-10f, bottom + height);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        Image fi = fill.GetComponent<Image>();
        fi.color = barColor;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f);
        fr.offsetMin = new Vector2(2f, 2f); fr.offsetMax = new Vector2(-2f, -2f);
        return (fi, fr);
    }

    private static void SetFill(RectTransform fill, float fraction) => fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);

    // ---- Currency ----
    private Row BuildCurrency(Entry e)
    {
        int tierIndex = -1;
        if (Enum.TryParse(e.key, out PixelClicker.PixelType type)) tierIndex = clicker.IndexOf(type);
        if (tierIndex < 0) return null;
        Row row = new Row { height = 46f, rt = NewRow(46f) };
        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];

        GameObject swatch = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        swatch.transform.SetParent(row.rt, false);
        swatch.GetComponent<Image>().color = tier.UIColor;
        RectTransform sr = swatch.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(26f, 26f);
        sr.anchoredPosition = new Vector2(12f, 0f);

        string name = tier.displayName.EndsWith(" Pixels") ? tier.displayName.Substring(0, tier.displayName.Length - 7) : tier.displayName;
        TMP_Text label = Text(row.rt, "Name", name, 22f, TextAlignmentOptions.MidlineLeft, textColor);
        Stretch(label.rectTransform, 48f, 4f);
        label.rectTransform.offsetMax = new Vector2(-150f, -4f);
        TMP_Text amount = Text(row.rt, "Amount", "", 24f, TextAlignmentOptions.MidlineRight, textColor, FontStyles.Bold);
        Stretch(amount.rectTransform, 12f, 2f);
        amount.rectTransform.offsetMin = new Vector2(150f, 2f);
        row.refresh = () =>
        {
            string text = PixelClicker.FormatNumber(Math.Floor(tier.count));
            if (e.flag) text += "  <size=70%><color=#9fd8ff>" + RateText(tierIndex) + "</color></size>";
            PixelUIKit.SetText(amount, text);
        };
        return row;
    }

    private readonly Dictionary<int, List<(float t, double c)>> rateSamples = new Dictionary<int, List<(float, double)>>();
    private float sampleTimer;

    private void SampleRates()
    {
        foreach (Entry e in blob.entries)
        {
            if (e.type != "currency" || !e.flag || !Enum.TryParse(e.key, out PixelClicker.PixelType type)) continue;
            int i = clicker.IndexOf(type);
            if (i < 0) continue;
            if (!rateSamples.TryGetValue(i, out var list)) rateSamples[i] = list = new List<(float, double)>();
            list.Add((Time.unscaledTime, clicker.Tiers[i].count));
            while (list.Count > 0 && Time.unscaledTime - list[0].t > 60f) list.RemoveAt(0);
        }
    }

    private string RateText(int tierIndex)
    {
        if (!rateSamples.TryGetValue(tierIndex, out var list) || list.Count < 3) return "...";
        float dt = list[list.Count - 1].t - list[0].t;
        if (dt < 5f) return "...";
        double perMinute = (list[list.Count - 1].c - list[0].c) / dt * 60d;
        return (perMinute >= 0 ? "+" : "-") + PixelClicker.FormatNumber(Math.Abs(perMinute)) + "/min";
    }

    // ---- Skill ----
    private Row BuildSkill(Entry e, string sub)
    {
        PixelSkills skills = PixelSkills.Instance;
        if (skills == null) return null;
        Row row = new Row { height = 66f, rt = NewRow(66f) };
        TMP_Text name = Text(row.rt, "Name", "", 22f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f); name.rectTransform.pivot = new Vector2(0.5f, 1f);
        name.rectTransform.offsetMin = new Vector2(12f, -34f); name.rectTransform.offsetMax = new Vector2(-150f, -5f);
        TMP_Text detail = Text(row.rt, "Detail", "", 20f, TextAlignmentOptions.TopRight, dimTextColor);
        detail.rectTransform.anchorMin = new Vector2(0f, 1f); detail.rectTransform.anchorMax = new Vector2(1f, 1f); detail.rectTransform.pivot = new Vector2(0.5f, 1f);
        detail.rectTransform.offsetMin = new Vector2(150f, -34f); detail.rectTransform.offsetMax = new Vector2(-12f, -5f);
        var bar = MakeBar(row.rt, 8f, 16f);
        string id = !string.IsNullOrEmpty(sub) ? sub : e.key;
        row.refresh = () =>
        {
            PixelSkills.SkillPack pack = id == "training" ? skills.Find(skills.TrainingId) : skills.Find(id);
            if (pack == null) { PixelUIKit.SetText(name, "?"); return; }
            int level = skills.LevelOf(pack, out double into, out double need);
            bool training = pack.id == skills.TrainingId;
            PixelUIKit.SetText(name, pack.displayName + (training && id != "training" ? "  (training)" : id == "training" ? "  (training)" : ""));
            PixelUIKit.SetText(detail, "Lv " + level + "   " + PixelClicker.FormatNumber(Math.Floor(into)) + " / " + PixelClicker.FormatNumber(Math.Ceiling(need)));
            bar.fill.color = pack.accent;
            SetFill(bar.bar, (float)(into / Math.Max(1d, need)));
        };
        return row;
    }

    // ---- Goals ----
    private Row BuildGoal(Entry e, string sub)
    {
        Row row = new Row { height = 66f, rt = NewRow(66f) };
        TMP_Text name = Text(row.rt, "Name", "", 21f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f); name.rectTransform.pivot = new Vector2(0.5f, 1f);
        name.rectTransform.offsetMin = new Vector2(12f, -34f); name.rectTransform.offsetMax = new Vector2(-150f, -5f);
        TMP_Text detail = Text(row.rt, "Detail", "", 20f, TextAlignmentOptions.TopRight, dimTextColor);
        detail.rectTransform.anchorMin = new Vector2(0f, 1f); detail.rectTransform.anchorMax = new Vector2(1f, 1f); detail.rectTransform.pivot = new Vector2(0.5f, 1f);
        detail.rectTransform.offsetMin = new Vector2(150f, -34f); detail.rectTransform.offsetMax = new Vector2(-12f, -5f);
        var bar = MakeBar(row.rt, 8f, 16f);
        string key = !string.IsNullOrEmpty(sub) ? sub : e.key;
        row.refresh = () =>
        {
            string title = "?";
            double count = 0d, goal = 1d;
            if (key.StartsWith("mg:"))
            {
                PixelMinigame m = PixelMinigame.Find(key.Substring(3));
                if (m != null && m.HasTracker) { title = m.TrackerTitle; count = m.TrackerCount; goal = Math.Max(1d, m.TrackerGoal); }
            }
            else
            {
                PixelLog log = PixelFind.First<PixelLog>();
                List<(string title, double count, double goal)> goals = log != null ? log.CurrentGoals() : null;
                int index = key == "next" ? 0 : key.StartsWith("g") && int.TryParse(key.Substring(1), out int gi) ? gi : -1;
                if (goals != null && index >= 0 && index < goals.Count) { title = goals[index].title; count = goals[index].count; goal = Math.Max(1d, goals[index].goal); }
                else if (goals != null && key == "next") title = "No goal right now";
            }
            bool done = count >= goal;
            PixelUIKit.SetText(name, title);
            PixelUIKit.SetText(detail, done ? "Done" : PixelClicker.FormatNumber(Math.Floor(count)) + " / " + PixelClicker.FormatNumber(goal));
            bar.fill.color = done ? new Color(1f, 0.85f, 0.3f, 1f) : barColor;
            SetFill(bar.bar, (float)(count / goal));
        };
        return row;
    }

    // ---- Achievement ----
    private Row BuildAchievement(Entry e)
    {
        PixelAchievements ach = PixelFind.First<PixelAchievements>();
        if (ach == null) return null;
        int index = -1;
        for (int i = 0; i < ach.Count; i++) if (ach.GetId(i) == e.key) { index = i; break; }
        if (index < 0) return null;
        Row row = new Row { height = 66f, rt = NewRow(66f) };
        TMP_Text name = Text(row.rt, "Name", ach.GetTitle(index), 21f, TextAlignmentOptions.TopLeft, textColor, FontStyles.Bold);
        name.rectTransform.anchorMin = new Vector2(0f, 1f); name.rectTransform.anchorMax = new Vector2(1f, 1f); name.rectTransform.pivot = new Vector2(0.5f, 1f);
        name.rectTransform.offsetMin = new Vector2(12f, -34f); name.rectTransform.offsetMax = new Vector2(-150f, -5f);
        TMP_Text detail = Text(row.rt, "Detail", "", 20f, TextAlignmentOptions.TopRight, dimTextColor);
        detail.rectTransform.anchorMin = new Vector2(0f, 1f); detail.rectTransform.anchorMax = new Vector2(1f, 1f); detail.rectTransform.pivot = new Vector2(0.5f, 1f);
        detail.rectTransform.offsetMin = new Vector2(150f, -34f); detail.rectTransform.offsetMax = new Vector2(-12f, -5f);
        var bar = MakeBar(row.rt, 8f, 16f);
        row.refresh = () =>
        {
            bool complete = ach.IsComplete(index);
            double target = ach.GetCurrentTarget(index);
            PixelUIKit.SetText(detail, complete ? "Complete" : PixelClicker.FormatNumber(Math.Floor(ach.GetProgress(index))) + " / " + PixelClicker.FormatNumber(target));
            bar.fill.color = complete ? new Color(1f, 0.85f, 0.3f, 1f) : barColor;
            SetFill(bar.bar, complete ? 1f : ach.GetFraction(index));
        };
        return row;
    }

    // ---- Note ----
    private Row BuildNote(Entry e)
    {
        Row row = new Row { height = 60f, rt = NewRow(60f) };
        TMP_InputField field = PixelUIKit.CreateInputField(Font, row.rt, "Note", new Vector2(200f, 44f), new Color(0.08f, 0.1f, 0.16f, 1f), textColor, 21f, "Write a note...");
        RectTransform fr = field.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(8f, 8f);
        fr.offsetMax = new Vector2(editing ? -128f : -8f, -8f);
        field.SetTextWithoutNotify(e.text);
        field.onEndEdit.AddListener(s => { e.text = s; SaveEntries(); });
        return row;
    }

    // ---- Favorites ----
    private Row BuildFavorite(Entry e)
    {
        bool tool = e.key.StartsWith("tool:");
        Row row = new Row { height = 54f, rt = NewRow(54f) };
        Button button = PixelUIKit.CreateButton(Font, row.rt, "Use", "", new Vector2(200f, 40f), ButtonRole.Primary, 22f);
        RectTransform br = button.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = new Vector2(8f, 7f);
        br.offsetMax = new Vector2(editing ? -128f : -8f, -7f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMax = 22f;
        label.fontSizeMin = 11f;
        Image image = button.GetComponent<Image>();
        ButtonRole shownRole = ButtonRole.Primary;   // (refresh runs every few tenths of a second: only re-role on a change)
        void ShowRole(ButtonRole role) { if (role != shownRole) { shownRole = role; PixelUIKit.SetRole(image, role); } }

        if (tool)
        {
            string id = e.key.Substring(5);
            var def = Array.Find(Tools, t => t.id == id);
            if (def.id == null) return null;
            button.onClick.AddListener(() =>
            {
                if (id == "hose") { PixelBank bank = PixelFind.First<PixelBank>(); if (bank != null) bank.ToggleHose(); }
                else PixelMenuKeys.Click(def.action);
            });
            row.refresh = () =>
            {
                bool hose = id == "hose";
                bool available = hose ? PixelFind.First<PixelBank>() != null : PixelMenuKeys.CanClick(def.action);
                bool on = hose && PixelBank.HoseOn;
                PixelUIKit.SetText(label, def.name + (on ? "  (on)" : ""));
                ShowRole(on ? ButtonRole.Success : available ? ButtonRole.Primary : ButtonRole.Neutral);
            };
            return row;
        }

        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables == null) return null;
        string itemName = e.key.StartsWith("item:") ? e.key.Substring(5) : e.key;
        int item = -1;
        for (int i = 0; i < consumables.ItemCount; i++) if (consumables.ItemName(i) == itemName) { item = i; break; }
        if (item < 0) return null;
        button.onClick.AddListener(() => { consumables.TryUseItem(item); RefreshValues(); });
        row.refresh = () =>
        {
            int owned = consumables.ItemOwned(item);
            PixelUIKit.SetText(label, itemName + "   x" + PixelConsumables.OwnedText(owned));
            ShowRole(owned > 0 ? ButtonRole.Primary : ButtonRole.Neutral);
        };
        return row;
    }
}
