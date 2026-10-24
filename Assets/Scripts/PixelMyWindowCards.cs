// ---- System notes ----
// PixelMyWindowCards.cs
//   Part of PixelMyWindow (partial class): the Pinned tab as a board of cards (RebuildRows, BuildCard, the blank + card, hold-to-remove) and the picker that searches for what to pin. The system notes are in PixelMyWindow.cs.
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
    // ------------------------------------------------------------------
    // The Pinned tab as a board of WIDGETS (cards): each pinned thing is a card with a header you drag by, a '-' to fold it up and a small x you HOLD to remove it; a blank card with a +
    // under the last one opens a search to pin something new. (Same look and moves as the Dev Hub's widgets.)
    // ------------------------------------------------------------------

    // The cards themselves (drag to reorder, fold, layout) are the shared PixelCardBoard (the Dev Hub's widgets use it too); a Pocket card adds
    // its entry, its rows and the hold-to-remove x. The order and the folds are saved in the entries (per save file), not by the board.
    private class Card
    {
        public Entry entry;
        public PixelCardBoard.Card card;
        public RectTransform rect, xRect;
        public Image bg;
        public readonly List<Row> rows = new List<Row>();
    }

    private readonly List<Card> cards = new List<Card>();
    private RectTransform plusCard;
    private PixelCardBoard board;
    private const float CardHeader = 34f, CardGap = 8f, PlusHeight = 78f, RemoveHoldSeconds = 0.6f;
    private static readonly Color CardColor = new Color(0.07f, 0.09f, 0.16f, 0.9f), CardRed = new Color(0.75f, 0.12f, 0.1f, 0.95f);

    private void RebuildRows()
    {
        if (listContent == null) return;
        foreach (Card c in cards) if (c.rect != null) Destroy(c.rect.gameObject);
        foreach (Row r in rows) if (r.rt != null) Destroy(r.rt.gameObject);   // (rows not yet inside a card)
        if (plusCard != null) Destroy(plusCard.gameObject);
        cards.Clear(); rows.Clear(); plusCard = null;
        if (board == null)
            board = new PixelCardBoard(listContent, new PixelCardBoard.Style
            {
                headerHeight = CardHeader, topPad = 4f, gap = CardGap, handleAlpha = 0.03f, titleLeft = 40f, titleRight = 78f,
                foldPos = new Vector2(-38f, -5f), titleColor = new Color(0.85f, 0.9f, 1f, 0.92f), cardColor = CardColor,
            }, OnBoardHeight)
            {
                OrderChanged = OnCardsReordered,
                FoldChanged = c => { if (c.tag is Card pc) { pc.entry.min = c.folded; SaveEntries(); } },
            };
        board.Clear();
        for (int i = 0; i < blob.entries.Count; i++)
        {
            Entry e = blob.entries[i];
            List<string> subs = Expand(e);
            List<Row> made = new List<Row>();
            for (int s = 0; s < subs.Count; s++)
            {
                Row row = BuildRow(e, subs[s], s == 0, i);
                if (row != null) made.Add(row);
            }
            if (made.Count == 0) continue;
            cards.Add(BuildCard(e, made));
        }
        BuildPlusCard();
        rowSignature = Signature();
        board.Finish();   // (lays the cards out; OnBoardHeight puts the blank card under the last one)
        RefreshValues();
    }

    private string TitleFor(Entry e)
    {
        switch (e.type)
        {
            case "currency":
            {
                if (System.Enum.TryParse(e.key, out PixelClicker.PixelType t) && clicker != null)
                {
                    int ti = clicker.IndexOf(t);
                    if (ti >= 0) { string n = clicker.Tiers[ti].displayName; return n.EndsWith(" Pixels") ? n.Substring(0, n.Length - 7) : n; }
                }
                return "Currency";
            }
            case "skill":
            {
                if (e.key == "training") return "Skill in training";
                if (e.key == "all") return "All skills";
                PixelSkills sk = PixelSkills.Instance;
                if (sk != null) foreach (PixelSkills.SkillPack p in sk.Packs) if (p.id == e.key) return p.displayName;
                return "Skill";
            }
            case "goal":
            {
                if (e.key == "next") return "Next goal";
                if (e.key == "all") return "Goals";
                if (e.key.StartsWith("mg:")) { PixelMinigame m = PixelMinigame.Find(e.key.Substring(3)); if (m != null) return m.TrackerTitle; }
                return "Goal";
            }
            case "achievement":
            {
                PixelAchievements ach = PixelFind.First<PixelAchievements>();
                if (ach != null) for (int i = 0; i < ach.Count; i++) if (ach.GetId(i) == e.key) return ach.GetTitle(i);
                return "Achievement";
            }
            case "fav":
            {
                if (e.key.StartsWith("tool:") && TryGetTool(e.key.Substring(5), out string tn, out _)) return tn;
                return e.key.StartsWith("item:") ? e.key.Substring(5) : e.key;
            }
        }
        return "Note";
    }

    private Card BuildCard(Entry e, List<Row> made)
    {
        Card c = new Card { entry = e };
        GameObject go = new GameObject("Widget", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(listContent, false);
        c.bg = go.GetComponent<Image>();
        c.bg.color = CardColor;
        PixelUIKit.StyleBox(c.bg);
        c.rect = go.GetComponent<RectTransform>();
        c.rect.anchorMin = new Vector2(0f, 1f); c.rect.anchorMax = new Vector2(1f, 1f); c.rect.pivot = new Vector2(0.5f, 1f);

        // The body: the entry's rows, one under the other.
        float y = CardHeader + 2f;
        foreach (Row r in made)
        {
            r.rt.SetParent(c.rect, false);
            r.rt.anchorMin = new Vector2(0f, 1f); r.rt.anchorMax = new Vector2(1f, 1f); r.rt.pivot = new Vector2(0.5f, 1f);
            r.rt.sizeDelta = new Vector2(-12f, r.height);
            r.rt.anchoredPosition = new Vector2(0f, -y);
            y += r.height + 4f;
            c.rows.Add(r);
            rows.Add(r);
        }
        c.rect.sizeDelta = new Vector2(-6f, y + 4f);

        // The header (drag strip, grip, title, '-' fold) comes from the board; Pocket adds the small x you hold to remove the card.
        c.card = board.Add(e.type + ":" + e.key, c.rect, y + 4f, TitleFor(e));
        c.card.tag = c;
        Button x = PixelUIKit.CreateButton(Font, c.rect, "Remove (hold)", "x", new Vector2(26f, 24f), ButtonRole.Danger, 18f);
        c.xRect = x.GetComponent<RectTransform>();
        c.xRect.anchorMin = c.xRect.anchorMax = c.xRect.pivot = new Vector2(1f, 1f);
        c.xRect.anchoredPosition = new Vector2(-6f, -5f);
        board.KeepInHeader(c.card, x.gameObject);
        board.SetFolded(c.card, e.min, false);
        return c;
    }

    private void BuildPlusCard()
    {
        GameObject go = new GameObject("Blank Widget", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(listContent, false);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.05f);
        PixelUIKit.StyleBox(bg, true);
        plusCard = go.GetComponent<RectTransform>();
        plusCard.anchorMin = new Vector2(0f, 1f); plusCard.anchorMax = new Vector2(1f, 1f); plusCard.pivot = new Vector2(0.5f, 1f);
        plusCard.sizeDelta = new Vector2(-6f, PlusHeight);
        TMP_Text plus = Text(plusCard, "Plus", "+", 56f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.55f), FontStyles.Bold);
        plus.raycastTarget = false;
        PixelUIKit.Stretch(plus.rectTransform);
        PixelUIKit.MakeClickable(go, OpenPicker);
    }

    // The board laid the cards out: the blank card goes under the last one and the scroll content follows.
    private void OnBoardHeight(float total)
    {
        if (plusCard != null) plusCard.anchoredPosition = new Vector2(0f, -total);
        contentHeight = total + PlusHeight + 8f;
        if (scroll != null) UpdateContentHeight();
    }

    // A card was dragged to a new place: the saved order (the entries) follows the cards.
    private void OnCardsReordered()
    {
        List<Entry> ordered = new List<Entry>();
        foreach (PixelCardBoard.Card k in board.Cards) if (k.tag is Card pc && !ordered.Contains(pc.entry)) ordered.Add(pc.entry);
        foreach (Entry e in blob.entries) if (!ordered.Contains(e)) ordered.Add(e);
        blob.entries = ordered;
        SaveEntries();
        rowSignature = Signature();
    }

    /// <summary>Hold the LEFT button on a card's small x to remove it: a ring fills round the cursor and the card turns red.</summary>
    private void UpdateRemoveHold()
    {
        Card hot = null;
        if (cards.Count > 0 && scroll != null && scroll.gameObject.activeInHierarchy && !minimized)
        {
            Vector2 p = PixelInput.PointerPosition();
            foreach (Card c in cards)
                if (c.xRect != null && c.xRect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(c.xRect, p, null)) { hot = c; break; }
        }
        bool done = PixelHold.Update(this, hot, RemoveHoldSeconds, "", new Color(1f, 0.3f, 0.25f, 1f), true, "remove_buildup", true);
        foreach (Card c in cards)
            if (c.bg != null) c.bg.color = c == hot ? Color.Lerp(CardColor, CardRed, PixelHold.ProgressOf(c)) : CardColor;
        if (done && hot != null)
        {
            blob.entries.Remove(hot.entry);
            SaveEntries();
            PixelAudio.Play("device_remove");
            RebuildRows();   // (with no widget left, the blank one with the + is all that shows)
        }
    }

    // ---- the picker: search for what to pin ----

    private struct Option { public string label, type, key; }
    private GameObject pickerRoot;
    private TMP_InputField pickerSearch;
    private RectTransform pickerContent;
    private ScrollRect pickerScroll;
    private GameObject pickerBar;
    private string pickerText = "";

    private List<Option> AllOptions()
    {
        List<Option> list = new List<Option>();
        if (clicker == null) return list;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
        {
            if (!t.unlocked && t.count <= 0d) continue;
            string n = t.displayName.EndsWith(" Pixels") ? t.displayName.Substring(0, t.displayName.Length - 7) : t.displayName;
            list.Add(new Option { label = n, type = "currency", key = t.type.ToString() });
        }
        list.Add(new Option { label = "Skill in training (follows)", type = "skill", key = "training" });
        list.Add(new Option { label = "All skills", type = "skill", key = "all" });
        PixelSkills skills = PixelSkills.Instance;
        if (skills != null) foreach (PixelSkills.SkillPack p in skills.Packs) if (skills.IsAvailable(p)) list.Add(new Option { label = "Skill: " + p.displayName, type = "skill", key = p.id });
        list.Add(new Option { label = "Next goal", type = "goal", key = "next" });
        list.Add(new Option { label = "All current goals", type = "goal", key = "all" });
        foreach (PixelMinigame m in PixelMinigame.All)
            if (m != null && !(m is PixelVisitorMinigame) && m.HasTracker) list.Add(new Option { label = "Goal: " + m.TrackerTitle, type = "goal", key = "mg:" + m.Id });
        foreach (var tool in Tools) list.Add(new Option { label = "Tool: " + tool.name, type = "fav", key = "tool:" + tool.id });
        PixelConsumables consumables = PixelFind.First<PixelConsumables>();
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount; i++)
                if (consumables.ItemListable(i)) list.Add(new Option { label = consumables.ItemName(i), type = "fav", key = "item:" + consumables.ItemName(i) });
        PixelAchievements ach = PixelFind.First<PixelAchievements>();
        if (ach != null) for (int i = 0; i < ach.Count; i++) list.Add(new Option { label = "Achievement: " + ach.GetTitle(i), type = "achievement", key = ach.GetId(i) });
        list.Add(new Option { label = "A note", type = "note", key = "" });
        return list;
    }

    private void OpenPicker()
    {
        if (pickerRoot == null) BuildPicker();
        RectTransform sr = scroll.GetComponent<RectTransform>();
        RectTransform pr = pickerRoot.GetComponent<RectTransform>();
        pr.anchorMin = sr.anchorMin; pr.anchorMax = sr.anchorMax; pr.offsetMin = sr.offsetMin; pr.offsetMax = sr.offsetMax;
        pickerRoot.SetActive(true);
        pickerRoot.transform.SetAsLastSibling();
        pickerText = "";
        pickerSearch.SetTextWithoutNotify("");
        FillPicker();
        pickerSearch.ActivateInputField();
    }

    private void ClosePicker() { if (pickerRoot != null) pickerRoot.SetActive(false); }

    private void BuildPicker()
    {
        pickerRoot = new GameObject("Pocket Picker", typeof(RectTransform), typeof(Image));
        pickerRoot.transform.SetParent(windowObject.transform, false);
        pickerRoot.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.17f, 0.98f);
        PixelUIKit.StyleBox(pickerRoot.GetComponent<Image>());
        RectTransform root = pickerRoot.GetComponent<RectTransform>();

        pickerSearch = PixelUIKit.CreateInputField(Font, root, "Search", new Vector2(100f, 44f), new Color(0.12f, 0.15f, 0.22f, 1f), Color.white, 22f, "Search for something to pin...");
        RectTransform sf = pickerSearch.GetComponent<RectTransform>();
        sf.anchorMin = new Vector2(0f, 1f); sf.anchorMax = new Vector2(1f, 1f); sf.pivot = new Vector2(0.5f, 1f);
        sf.offsetMin = new Vector2(8f, -56f); sf.offsetMax = new Vector2(-72f, -10f);
        PixelUIKit.AddSearchIcon(pickerSearch);
        pickerSearch.onValueChanged.AddListener(v => { pickerText = (v ?? "").Trim().ToLowerInvariant(); FillPicker(); });
        Button close = PixelUIKit.CreateButton(Font, root, "Cancel", "X", new Vector2(54f, 44f), ButtonRole.Danger, 24f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-8f, -10f);
        close.onClick.AddListener(ClosePicker);

        pickerScroll = PixelUIKit.CreateScrollView(root, "Options", new Color(1f, 1f, 1f, 0.35f), 12f, 60f, out pickerContent, out pickerBar);
        RectTransform ps = pickerScroll.GetComponent<RectTransform>();
        ps.anchorMin = Vector2.zero; ps.anchorMax = Vector2.one;
        ps.offsetMin = new Vector2(6f, 6f); ps.offsetMax = new Vector2(-6f, -66f);
    }

    private void FillPicker()
    {
        for (int i = pickerContent.childCount - 1; i >= 0; i--) Destroy(pickerContent.GetChild(i).gameObject);
        float y = 2f;
        int shown = 0;
        foreach (Option o in AllOptions())
        {
            if (pickerText.Length > 0 && !o.label.ToLowerInvariant().Contains(pickerText)) continue;
            if (shown >= 70) break;
            shown++;
            bool already = o.type != "note" && blob.entries.Exists(e => e.type == o.type && e.key == o.key);
            Option opt = o;
            Button b = PixelUIKit.CreateButton(Font, pickerContent, "Option", o.label + (already ? "   (pinned)" : ""), new Vector2(100f, 42f), already ? ButtonRole.Neutral : ButtonRole.Primary, 21f);
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(4f, -(y + 42f)); r.offsetMax = new Vector2(-4f, -y);
            TMP_Text lbl = b.GetComponentInChildren<TMP_Text>();
            lbl.alignment = TextAlignmentOptions.MidlineLeft; lbl.enableAutoSizing = true; lbl.fontSizeMax = 21f; lbl.fontSizeMin = 11f; lbl.textWrappingMode = TextWrappingModes.NoWrap;
            lbl.rectTransform.offsetMin = new Vector2(12f, 0f);
            b.interactable = !already;
            b.onClick.AddListener(() => { blob.entries.Add(new Entry { type = opt.type, key = opt.key }); SaveEntries(); ClosePicker(); RebuildRows(); scroll.verticalNormalizedPosition = 0f; });
            y += 46f;
        }
        if (shown == 0)
        {
            TMP_Text none = Text(pickerContent, "None", "Nothing matches.", 22f, TextAlignmentOptions.Center, dimTextColor, FontStyles.Italic);
            none.rectTransform.anchorMin = new Vector2(0f, 1f); none.rectTransform.anchorMax = new Vector2(1f, 1f); none.rectTransform.pivot = new Vector2(0.5f, 1f);
            none.rectTransform.sizeDelta = new Vector2(0f, 50f); none.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            y = 60f;
        }
        Canvas.ForceUpdateCanvases();
        PixelUIKit.UpdateScrollView(pickerScroll, pickerBar, y + 4f, pickerScroll.GetComponent<RectTransform>().rect.height);
    }
}
