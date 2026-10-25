// ---- System notes ----
// PixelSkillsGuide.cs
//   Part of partial class PixelSkills: SKILL GUIDES (first one: Woodcutting). The first time the Woodcutting card shows in the Skills window (clicked, or the window opened on it)
//   (and no other tutorial runs), a small box over the window asks 'Want a quick tutorial?' (Yes / No; stat guide.woodcutting.asked, per save).
//   YES = the guide: small Got it boxes beside the Skills window (PixelNotice beside windowRect, back arrow / Skip), the screen dimmed and paused
//   (PixelTutorialDim) - the basics of Woodcutting and how its blocks spawn (filler rotation, trees and their order, Timber, infested trees, the Great Oak,
//   skill points / the Lumberjack). The texts are the Inspector list woodGuideSteps (empty = the built-ins).
//   NO = one box pointing at the GUIDE BOOK button that sits between the skill's name and the 'Skill tree' button in the detail panel (framed with the shared gold
//   PixelHighlight until Got it). The book (a code-drawn little book) shows for every skill that has a guide (HasGuide) and replays it any time.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelSkills
{
    [Header("Skill guides (Woodcutting)")]
    [Tooltip("Ask the first time the Woodcutting card is selected whether the player wants its tutorial.")]
    [SerializeField] private bool askWoodGuide = true;
    [Tooltip("The Woodcutting guide: one Got it box per entry, in order. Empty = the built-in texts.")]
    [SerializeField, TextArea(2, 5)] private List<string> woodGuideSteps = new List<string>();
    [Tooltip("The question in the first-time box.")]
    [SerializeField] private string woodGuideQuestion = "First time chopping? Want a quick tutorial on how Woodcutting works?";
    [Tooltip("Shown when the player says No: where to find the guide later.")]
    [SerializeField] private string woodGuideLaterText = "No problem! Whenever you want it, the <b>guide book</b> next to the skill's name opens the Woodcutting guide.";

    private const string WoodGuideAskedKey = "guide.woodcutting.asked";
    private Button guideBookButton;
    private GameObject guideAskPanel;
    private bool guideRunning;

    private static List<string> DefaultWoodGuide() => new List<string>
    {
        "<b>Woodcutting</b> is a skill. Press <b>Train</b> and the cube stops showing pure pixels and grows a forest instead. Every woodcutting block you break pays its own currency and gives Woodcutting XP.",
        "Between trees the cube shows <b>filler</b>: Twig, Leaf and Wood at random (Moss and Sap join at level 2). Filler is quick and cheap - and the higher your level, the shorter the wait for the next tree.",
        "Then a <b>tree</b> arrives and plays out <b>in order</b>: leaves first, then a run of Wood, and its special blocks near the end - Sap (level 2+), Acorn (level 3+) and Heartwood (level 6+). The <b>tree bar</b> at the top shows how far along you are.",
        "Break a tree's last block for <b>Timber!</b> - bonus XP for the whole tree. Once you know a tree's shape you know what's coming next.",
        "From level 2 some trees are <b>infested</b>: the tree bar warns you, and the tree ends in a <b>Termite Hive</b>, a tough hazard block. Pesticide (it shows up in a tree's trunk now and then) and the Lumberjack's Focus potion help.",
        "Rare: the <b>Great Oak</b>. From level 3 a tree can be a Great Oak - one giant block that shakes bird nests loose (click them for XP). <b>Acorn</b> husks left lying on the field make it more likely.",
        "Every level gives a <b>skill point</b> for the <b>Skill tree</b>, and Big Tim the <b>Lumberjack</b> visits while you chop - he buys your wood for pure pixels. Open this guide again any time with the <b>book</b> next to the skill's name.",
    };

    /// <summary>Does this skill have a guide (the book button shows for it)?</summary>
    private static bool HasGuide(SkillPack p) => p != null && p.id == WoodcuttingId;

    // ---- the book button ----

    /// <summary>A small code-drawn book after the skill's name in the detail panel (called once while the window is built).</summary>
    private void BuildGuideBook(Transform detail)
    {
        guideBookButton = PixelUIKit.CreateButton(Font, detail, "Guide Book", "", new Vector2(48f, 42f), ButtonRole.Neutral, 20f);
        GameObject go = guideBookButton.gameObject;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(48f, 42f);
        // the book: a cover, two pages and a spine
        Part(go.transform, new Vector2(34f, 26f), Vector2.zero, new Color(0.62f, 0.32f, 0.18f, 1f));
        Part(go.transform, new Vector2(14f, 20f), new Vector2(-7.5f, 1f), new Color(0.97f, 0.93f, 0.82f, 1f));
        Part(go.transform, new Vector2(14f, 20f), new Vector2(7.5f, 1f), new Color(0.97f, 0.93f, 0.82f, 1f));
        Part(go.transform, new Vector2(2f, 22f), new Vector2(0f, 0f), new Color(0.45f, 0.22f, 0.12f, 1f));
        for (int i = 0; i < 3; i++)
        {
            Part(go.transform, new Vector2(9f, 1.5f), new Vector2(-7.5f, 6f - i * 5f), new Color(0.55f, 0.5f, 0.45f, 1f));
            Part(go.transform, new Vector2(9f, 1.5f), new Vector2(7.5f, 6f - i * 5f), new Color(0.55f, 0.5f, 0.45f, 1f));
        }
        guideBookButton.onClick.AddListener(() => { CloseGuideAsk(); StartWoodGuide(); });
        go.SetActive(false);
    }

    private static void Part(Transform parent, Vector2 size, Vector2 pos, Color color)
    {
        GameObject g = new GameObject("Part", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.sizeDelta = size;
        r.anchoredPosition = pos;
    }

    /// <summary>Shows the book left of the 'Skill tree' button for a skill with a guide (called from RefreshWindow).</summary>
    private void RefreshGuideBook(SkillPack p, bool available)
    {
        if (guideBookButton == null || detailName == null) return;
        bool show = available && HasGuide(p);
        guideBookButton.gameObject.SetActive(show);
        // The book sits just LEFT of the 'Skill tree' button (measured, so it never clips into it) and the name ends before the book.
        RectTransform nr = detailName.rectTransform;
        float treeW = treeToggle != null && treeToggle.gameObject.activeSelf ? treeToggle.GetComponent<RectTransform>().rect.width : 0f;
        float bookRight = treeW > 0f ? 18f + treeW + 10f : 18f;
        nr.offsetMax = new Vector2(show ? -(bookRight + 48f + 10f) : (treeW > 0f ? -(18f + treeW + 10f) : -200f), nr.offsetMax.y);
        if (!show) return;
        guideBookButton.transform.SetAsLastSibling();
        RectTransform r = guideBookButton.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 0.5f);
        r.anchoredPosition = new Vector2(-bookRight, -38f);
        MaybeAskGuide(p);   // however Woodcutting came up in the window (a click, or opening the window on the skill in training), ask the first time
    }

    // ---- first-time question ----

    /// <summary>A card was selected: the first time it is Woodcutting, ask whether the player wants the guide.</summary>
    private void MaybeAskGuide(SkillPack p)
    {
        if (!askWoodGuide || !WindowOpen || !HasGuide(p) || !IsAvailable(p) || guideRunning || tutorialRunning || PixelTutorialDim.Active) return;
        if (PixelStats.Total(WoodGuideAskedKey) >= 1d || guideAskPanel != null) return;
        PixelStats.Best(WoodGuideAskedKey, 1d);
        ShowGuideAsk();
    }

    private void ShowGuideAsk()
    {
        guideAskPanel = PixelUIKit.AskBox(Font, windowObject.transform, "WOODCUTTING", new Color(0.9f, 0.65f, 0.35f, 1f), woodGuideQuestion, textColor,
                                          "Yes, show me", "No thanks", () => { guideAskPanel = null; StartWoodGuide(); }, () => { guideAskPanel = null; PointAtGuideBook(); });
    }

    private void CloseGuideAsk()
    {
        if (guideAskPanel != null) Destroy(guideAskPanel);
        guideAskPanel = null;
    }

    /// <summary>'No': one box pointing at the book, which glows gold until Got it.</summary>
    private void PointAtGuideBook()
    {
        if (guideBookButton != null && guideBookButton.gameObject.activeInHierarchy) PixelHighlight.Set(this, guideBookButton.GetComponent<RectTransform>());
        PixelNotice.Show(woodGuideLaterText, 0f, small: true, beside: windowRect, onClosed: () => PixelHighlight.Clear(this));
    }

    // ---- the guide ----

    /// <summary>Plays the Woodcutting guide (the first-time Yes, or the book button).</summary>
    public void StartWoodGuide()
    {
        if (guideRunning || tutorialRunning || !built) return;
        if (woodGuideSteps == null || woodGuideSteps.Count == 0) woodGuideSteps = DefaultWoodGuide();
        PixelNotice.Hide();
        guideRunning = true;
        PixelTutorialDim.Begin(() => GuideStep(0));
    }

    private void GuideStep(int index)
    {
        if (index >= woodGuideSteps.Count)
        {
            guideRunning = false;
            PixelTutorialDim.End();
            return;
        }
        if (!WindowOpen) OpenWindow();
        float longest = 0f;
        foreach (string s in woodGuideSteps) if (s != null) longest = Mathf.Max(longest, s.Length);
        float height = Mathf.Max(260f, 124f + (Mathf.CeilToInt(longest / 28f) + 1) * 30f);
        PixelNotice.Show(woodGuideSteps[index], 0f, small: true, beside: windowRect, onClosed: () => GuideStep(index + 1), minHeight: height,
                         onBack: index > 0 ? (System.Action)(() => GuideStep(index - 1)) : null, onSkip: () => GuideStep(woodGuideSteps.Count));
    }
}
