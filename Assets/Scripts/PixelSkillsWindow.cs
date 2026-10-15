// ---- System notes (moved here from CLAUDE.md) ----
// Skill tree window (PixelSkillsWindow.cs, PixelScrollFadeX.cs)
//   The tree's nodes sit in a SIDEWAYS scroll view (nodeView + nodeScroll, wheel / drag; RefreshTree widens nodeArea when the tree is
//   wider than the view) whose left / right edges fade out while there is more to see (PixelScrollFadeX: a CanvasGroup on every node
//   and connector line, alpha by distance to the edge). A button at the tree window's TOP LEFT (treeExpandButton, arrow glyph) makes
//   the tree cover the Skills window (treeExpanded: UpdateTreeLayout starts the tree at the window's left edge); pressing again,
//   closing the tree or the window gives the window back.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Skills window and its docked tab (part of <see cref="PixelSkills"/>). The tab sits on the left edge directly under the Bank + Hose buttons and
/// appears once there is a skill pack besides Standard. The window opens from the left edge like the Bank window: a header with the pack you are
/// training, a list of pack cards on the left (Standard is a card too, so swapping away is one click), and the selected pack's details on the right
/// (what it replaces and spawns, where its XP comes from, its perks) with a Train button.
/// </summary>
public partial class PixelSkills
{
    [Header("Window")]
    [Tooltip("Size of the Skills window (canvas units).")]
    [SerializeField] private Vector2 windowSize = new Vector2(980f, 820f);

    [Tooltip("Gap between the screen edge's docked tab and the window.")]
    [SerializeField] private float gapToButton = 14f;

    [SerializeField] private string windowTitle = "Skills";
    [SerializeField] private string tabText = "Skills";
    [SerializeField] private string trainText = "Train";
    [SerializeField] private string trainingText = "Training";
    [SerializeField] private string useStandardText = "Use standard pixels";
    [SerializeField] private string activeBadgeText = "ACTIVE";
    [SerializeField] private string lockedName = "???";
    [SerializeField] private string treeButtonText = "Skill tree";
    [SerializeField] private string pixelsBadgeText = "PIXELS";

    [SerializeField] private string treeTitleText = "Skill tree";

    [Tooltip("Size of one skill tree node (canvas units).")]
    [SerializeField] private Vector2 nodeSize = new Vector2(190f, 90f);

    [Tooltip("Height of the area the skill tree nodes are laid out in (canvas units).")]
    [SerializeField] private float treeAreaHeight = 400f;

    [Tooltip("Colours.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);
    [SerializeField] private Color tabColor = new Color(0.22f, 0.3f, 0.45f, 1f);
    [SerializeField] private Color cardColor = new Color(0.17f, 0.17f, 0.21f, 1f);
    [SerializeField] private Color cardSelectedColor = new Color(0.26f, 0.32f, 0.5f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color dimTextColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color perkUnlockedColor = new Color(0.55f, 1f, 0.6f, 1f);
    [SerializeField] private int sortingOrder = 131;

    private class Card
    {
        public SkillPack pack;
        public Button button, treeButton;
        public Image background, barFill;
        public TMP_Text nameLabel, levelLabel, badge;
    }

    private GameObject canvasRoot, windowObject, tabObject;
    private Button tabButton, trainButton;
    private PixelDockedButton tabDock;
    private CanvasGroup tabGroup;
    private RectTransform windowRect, listContent, headerFill;
    private ScrollRect listScroll;
    private GameObject listBar;
    private TMP_Text minigameBonusLabel, headerLabel, detailName, detailDesc, detailInfo, trainLabel;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<Button> perkHeads = new List<Button>();
    private readonly List<TMP_Text> perkHeadLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> perkBodies = new List<TMP_Text>();
    private readonly HashSet<string> perkOpen = new HashSet<string>();   // "<pack id>:<level>" of the sections that are expanded
    private ScrollRect perkScroll;
    private RectTransform perkContent;
    private GameObject perkBar;
    private int selected, selectedNode;
    private Button treeToggle, unlockButton;
    private TMP_Text treeToggleLabel, pointsLabel, nodeInfoLabel, unlockLabel, treeTitle, tipText;
    private RectTransform nodeArea, nodeView, treeRect, tipRect;
    private ScrollRect nodeScroll;
    private Button treeExpandButton;
    private Transform treeExpandGlyph;
    private bool treeExpanded;   // the tree covers the Skills window for a bigger view
    private GameObject treeWindow;
    private System.Func<string> hoverTip;   // set while the mouse is over a card's orange '!' box
    private readonly object treeKey = new object();
    private readonly List<Button> nodeButtons = new List<Button>();
    private readonly List<Image> nodeImages = new List<Image>();
    private readonly List<TMP_Text> nodeLabels = new List<TMP_Text>();
    private readonly List<Image> treeLines = new List<Image>();
    private readonly List<Button> pixelPackButtons = new List<Button>();
    private readonly List<TMP_Text> pixelPackLabels = new List<TMP_Text>();
    private float refreshTimer;
    private bool built;

    public bool WindowOpen => windowObject != null && PixelPop.IsOpen(windowObject);

    /// <summary>Closes the window if it is open (the Bank, Inventory and Log call this when they open).</summary>
    public static void CloseWindowIfOpen()
    {
        PixelSkills s = Instance;
        if (s != null && s.WindowOpen) PixelPop.Hide(s.windowObject);
    }

    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    private void BuildWindow()
    {
        LoadTrackerPrefs();
        PixelHud hud = PixelHud.Ensure(gameObject);
        canvasRoot = PixelUIKit.CreateCanvas("PixelSkills Canvas", sortingOrder, new Vector2(1920f, 1080f), true);

        // ---- The tab: left edge, directly under the Bank + Hose buttons (same width as the pair). ----
        tabButton = PixelUIKit.CreateButton(Font, canvasRoot.transform, "Skills Button", tabText, hud.ButtonSize, tabColor, textColor, 30f);
        tabObject = tabButton.gameObject;
        RectTransform tabRect = tabButton.GetComponent<RectTransform>();
        // Bottom-left corner of the bars: Log (corner), then the Skills tab.
        hud.Dock(tabRect, new Vector2(0f, 0f), () => WindowOpen || (tabDock != null && tabDock.PointerNear()), 0f, 1f, hud.ButtonSize.x + hud.CornerGap);
        tabDock = tabButton.GetComponent<PixelDockedButton>();
        tabDock.blockPointer = () => { PixelBank b = PixelFind.First<PixelBank>(); return b != null && b.WindowIsOpen; };
        tabGroup = tabObject.AddComponent<CanvasGroup>();
        tabButton.onClick.AddListener(Toggle);
        PixelMenuKeys.Register(PixelAction.Skills, tabButton);
        tabObject.SetActive(false);

        // ---- The window. ----
        windowObject = new GameObject("Skills Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(windowObject.GetComponent<Image>(), panelColor);
        windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0f, 0.5f);
        windowRect.sizeDelta = windowSize;
        windowRect.anchoredPosition = new Vector2(gapToButton + hud.SideShownInset, 0f);   // takes the place of the tabs while it is open

        TMP_Text title = PixelUIKit.CreateText(Font, windowObject.transform, "Title", windowTitle, 56f, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        PixelUIKit.Caps(title);
        Place(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(180f, -84f), new Vector2(-180f, -14f));   // (room for the pin button on the left and the ? and X buttons on the right)

        Button close = PixelUIKit.CreateButton(Font, windowObject.transform, "Close", "X", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -12f);
        close.onClick.AddListener(() => PixelPop.Hide(windowObject));
        // '?' next to the X: plays Cubie's skills intro again, for anyone who wants to go into it more.
        Button help = PixelUIKit.CreateButton(Font, windowObject.transform, "Help", "?", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform hr = help.GetComponent<RectTransform>();
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(1f, 1f);
        hr.anchoredPosition = new Vector2(-14f - 70f - 10f, -12f);
        help.onClick.AddListener(StartTutorial);
        PixelMenuFx.AddWindowLine(windowObject.transform, 90f);
        BuildPinButton();   // top left: pins the training skill with an XP bar in a small window you can move
        // Header: "Training: Mining  Lv 3" + an XP bar.
        headerLabel = PixelUIKit.CreateText(Font, windowObject.transform, "Header", "", 34f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        Place(headerLabel.rectTransform, 0f, 1f, 0.6f, 1f, new Vector2(30f, -158f), new Vector2(-6f, -100f));   // the left part of the row (the minigame bonus has the right part, so they never overlap)
        headerLabel.enableAutoSizing = true; headerLabel.fontSizeMax = 34f; headerLabel.fontSizeMin = 16f;
        headerLabel.textWrappingMode = TextWrappingModes.NoWrap;
        // Right side of the header: the XP multiplier from the minigames that are switched on.
        minigameBonusLabel = PixelUIKit.CreateText(Font, windowObject.transform, "Minigame Bonus", "", 28f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, new Color(1f, 0.85f, 0.35f, 1f));
        Place(minigameBonusLabel.rectTransform, 0.6f, 1f, 1f, 1f, new Vector2(0f, -158f), new Vector2(-30f, -100f));
        minigameBonusLabel.raycastTarget = false;
        minigameBonusLabel.enableAutoSizing = true; minigameBonusLabel.fontSizeMax = 28f; minigameBonusLabel.fontSizeMin = 13f;
        minigameBonusLabel.textWrappingMode = TextWrappingModes.NoWrap;
        GameObject track = new GameObject("XP Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(windowObject.transform, false);
        Image trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);
        PixelUIKit.StyleBox(trackImage, true);
        Place(track.GetComponent<RectTransform>(), 0f, 1f, 1f, 1f, new Vector2(30f, -188f), new Vector2(-30f, -162f));
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        fill.GetComponent<Image>().color = new Color(0.3f, 0.8f, 0.45f, 1f);
        headerFill = fill.GetComponent<RectTransform>();
        headerFill.anchorMin = new Vector2(0f, 0f);
        headerFill.anchorMax = new Vector2(0f, 1f);
        headerFill.offsetMin = new Vector2(3f, 3f);
        headerFill.offsetMax = new Vector2(-3f, -3f);
        headerFill.pivot = new Vector2(0f, 0.5f);

        BuildMasteryBar();   // along the bottom: progress across all skills

        // Left: the pack cards.
        listScroll = PixelUIKit.CreateScrollView(windowObject.transform, "Pack List", new Color(1f, 1f, 1f, 0.35f), 10f, 60f, out listContent, out listBar);
        RectTransform lr = listScroll.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f);
        lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.offsetMin = new Vector2(26f, 84f);
        lr.offsetMax = new Vector2(26f + 330f, -204f);
        for (int i = 0; i < packs.Count; i++) cards.Add(BuildCard(i));

        // Right: the selected pack.
        GameObject detail = new GameObject("Detail", typeof(RectTransform), typeof(Image));
        detail.transform.SetParent(windowObject.transform, false);
        Image detailImage = detail.GetComponent<Image>();
        detailImage.color = new Color(0.13f, 0.13f, 0.17f, 1f);
        PixelUIKit.StyleBox(detailImage);
        RectTransform dr = detail.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0f, 0f);
        dr.anchorMax = new Vector2(1f, 1f);
        dr.offsetMin = new Vector2(380f, 84f);
        dr.offsetMax = new Vector2(-26f, -204f);

        detailName = PixelUIKit.CreateText(Font, detail.transform, "Name", "", 44f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        PixelUIKit.Caps(detailName);
        Place(detailName.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -62f), new Vector2(-200f, -10f));
        detailName.enableAutoSizing = true;   // one line: a long name ("Offline Progression") shrinks instead of wrapping over the text below
        detailName.fontSizeMax = 44f;
        detailName.fontSizeMin = 20f;
#if UNITY_2023_1_OR_NEWER
        detailName.textWrappingMode = TextWrappingModes.NoWrap;
#else
        detailName.enableWordWrapping = false;
#endif
        detailDesc = PixelUIKit.CreateText(Font, detail.transform, "Description", "", 24f, TextAlignmentOptions.TopLeft, FontStyles.Normal, dimTextColor);
        detailDesc.enableAutoSizing = true;   // always fits its band
        detailDesc.fontSizeMax = 24f;
        detailDesc.fontSizeMin = 13f;
        Place(detailDesc.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -158f), new Vector2(-22f, -66f));
        detailDesc.gameObject.SetActive(false);   // the description lives in the card's '!' tooltip now
        detailInfo = PixelUIKit.CreateText(Font, detail.transform, "Info", "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        detailInfo.richText = true;
        detailInfo.enableAutoSizing = true;
        detailInfo.fontSizeMax = 22f;
        detailInfo.fontSizeMin = 13f;
        Place(detailInfo.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -122f), new Vector2(-22f, -70f));
        // The perks: one collapsible section per level (a header button, the text underneath when open).
        perkScroll = PixelUIKit.CreateScrollView(detail.transform, "Perks", new Color(0f, 0f, 0f, 0f), 12f, 40f, out perkContent, out perkBar);
        RectTransform psr = perkScroll.GetComponent<RectTransform>();
        psr.anchorMin = Vector2.zero;
        psr.anchorMax = Vector2.one;
        psr.offsetMin = new Vector2(14f, 106f);
        psr.offsetMax = new Vector2(-14f, -84f);
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            Button head = PixelUIKit.CreateButton(Font, perkContent, "Perk Header " + i, "", new Vector2(10f, 44f), new Color(0.2f, 0.22f, 0.3f, 1f), textColor, 24f);
            TMP_Text hl = head.GetComponentInChildren<TMP_Text>();
            hl.alignment = TextAlignmentOptions.MidlineLeft;
            hl.richText = true;
            hl.margin = new Vector4(16f, 0f, 8f, 0f);
            head.onClick.AddListener(() =>
            {
                if (selected < 0 || selected >= packs.Count || packs[selected].perks == null || index >= packs[selected].perks.Length) return;
                string key = packs[selected].id + ":" + packs[selected].perks[index].level;
                if (!perkOpen.Add(key)) perkOpen.Remove(key);
                RefreshWindow();
            });
            head.gameObject.SetActive(false);
            perkHeads.Add(head);
            perkHeadLabels.Add(hl);

            TMP_Text body = PixelUIKit.CreateText(Font, perkContent, "Perk Body " + i, "", 24f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(1f, 1f, 1f, 0.92f));
            body.richText = true;
            body.gameObject.SetActive(false);
            perkBodies.Add(body);
        }

        BuildTreeUi(detail.transform);

        trainButton = PixelUIKit.CreateButton(Font, detail.transform, "Train", trainText, new Vector2(10f, 70f), new Color(0.2f, 0.55f, 0.3f, 1f), textColor, 34f);
        PixelUIKit.Caps(trainButton);
        trainLabel = trainButton.GetComponentInChildren<TMP_Text>();
        RectTransform tr = trainButton.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(0.5f, 0f);
        tr.offsetMin = new Vector2(22f, 18f);
        tr.offsetMax = new Vector2(-22f, 88f);
        trainButton.onClick.AddListener(() =>
        {
            if (selected >= 0 && selected < packs.Count) SetActive(packs[selected].id);
            RefreshWindow();
        });

        BuildTreeWindow();
        BuildTip();
        windowObject.SetActive(false);
        PixelWindows.Register(this, 14, () => WindowOpen, () => PixelPop.Hide(windowObject));
        Changed += () => { if (WindowOpen) RefreshWindow(); };
        built = true;
    }

    /// <summary>The 'Skill tree' button in the detail panel, the separate skill tree window and the pixel-pack buttons of Offline Progression.</summary>
    private void BuildTreeUi(Transform detail)
    {
        treeToggle = PixelUIKit.CreateButton(Font, detail, "Tree Toggle", treeButtonText, new Vector2(170f, 48f), new Color(0.25f, 0.3f, 0.45f, 1f), textColor, 24f);
        PixelUIKit.Caps(treeToggle);
        treeToggleLabel = treeToggle.GetComponentInChildren<TMP_Text>();
        RectTransform tt = treeToggle.GetComponent<RectTransform>();
        tt.anchorMin = tt.anchorMax = tt.pivot = new Vector2(1f, 1f);
        tt.anchoredPosition = new Vector2(-18f, -14f);
        treeToggle.onClick.AddListener(ToggleTreeWindow);
        treeToggle.gameObject.SetActive(false);

        // Offline Progression: which pack's pixels to play with.
        for (int i = 0; i < 4; i++)
        {
            Button b = PixelUIKit.CreateButton(Font, detail, "Pixel Pack " + i, "", new Vector2(10f, 50f), cardColor, textColor, 26f);
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(-44f, 50f);
            r.anchoredPosition = new Vector2(0f, -(128f + i * 58f));
            int slot = i;
            b.onClick.AddListener(() =>
            {
                int n = 0;
                foreach (SkillPack pack in packs)
                {
                    if (pack.id == OfflineId || !IsAvailable(pack)) continue;
                    if (n++ == slot) { SetPixelPack(pack.id); break; }
                }
                RefreshWindow();
            });
            b.gameObject.SetActive(false);
            pixelPackButtons.Add(b);
            pixelPackLabels.Add(b.GetComponentInChildren<TMP_Text>());
        }
    }

    /// <summary>The skill tree window: opens to the right of the Skills window, stretches to the right edge of the screen and is as tall as the Skills window.</summary>
    private void BuildTreeWindow()
    {
        PixelHud hud = PixelHud.Ensure(gameObject);
        treeWindow = new GameObject("Skill Tree Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        treeWindow.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(treeWindow.GetComponent<Image>(), panelColor);
        treeRect = treeWindow.GetComponent<RectTransform>();
        float left = windowRect.anchoredPosition.x + windowSize.x + 14f;
        float right = gapToButton + hud.SideShownInset;
        treeRect.anchorMin = new Vector2(0f, 0.5f);
        treeRect.anchorMax = new Vector2(1f, 0.5f);
        treeRect.pivot = new Vector2(0f, 0.5f);
        treeRect.offsetMin = new Vector2(left, -windowSize.y * 0.5f);
        treeRect.offsetMax = new Vector2(-right, windowSize.y * 0.5f);

        treeTitle = PixelUIKit.CreateText(Font, treeWindow.transform, "Title", "", 52f, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        PixelUIKit.Caps(treeTitle);
        treeTitle.enableAutoSizing = true;
        treeTitle.fontSizeMax = 52f;
        treeTitle.fontSizeMin = 24f;
        Place(treeTitle.rectTransform, 0f, 1f, 1f, 1f, new Vector2(100f, -84f), new Vector2(-100f, -14f));

        Button close = PixelUIKit.CreateButton(Font, treeWindow.transform, "Close", "X", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -12f);
        close.onClick.AddListener(() => PixelPop.Hide(treeWindow));

        // Top left: makes the tree cover the Skills window for a bigger view; pressing it again gives the window back.
        treeExpandButton = PixelUIKit.CreateButton(Font, treeWindow.transform, "Expand", "<", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform er = treeExpandButton.GetComponent<RectTransform>();
        er.anchorMin = er.anchorMax = er.pivot = new Vector2(0f, 1f);
        er.anchoredPosition = new Vector2(14f, -12f);
        PixelUIKit.UseArrowGlyph(treeExpandButton, true);
        treeExpandGlyph = treeExpandButton.transform.Find("Triangle");
        treeExpandButton.onClick.AddListener(() => { treeExpanded = !treeExpanded; ApplyTreeExpandGlyph(); UpdateTreeLayout(); if (nodeScroll != null) nodeScroll.horizontalNormalizedPosition = 0f; RefreshWindow(); });   // re-laid out in the same frame, so nothing jumps

        pointsLabel = PixelUIKit.CreateText(Font, treeWindow.transform, "Points", "", 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, perkUnlockedColor);
        Place(pointsLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(34f, -150f), new Vector2(-34f, -100f));

        // The nodes live in a sideways scroll view (wheel / drag) whose left and right edges fade out while there is more to see.
        GameObject view = new GameObject("Node View", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect), typeof(PixelScrollFadeX));
        view.transform.SetParent(treeWindow.transform, false);
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // invisible, but takes wheel / drag input
        nodeView = view.GetComponent<RectTransform>();
        nodeView.anchorMin = new Vector2(0f, 1f);
        nodeView.anchorMax = new Vector2(1f, 1f);
        nodeView.pivot = new Vector2(0f, 1f);
        nodeView.offsetMin = new Vector2(34f, -150f - treeAreaHeight);
        nodeView.offsetMax = new Vector2(-34f, -150f);

        GameObject area = new GameObject("Nodes", typeof(RectTransform));
        area.transform.SetParent(view.transform, false);
        nodeArea = area.GetComponent<RectTransform>();
        nodeArea.anchorMin = nodeArea.anchorMax = nodeArea.pivot = new Vector2(0f, 1f);
        nodeArea.anchoredPosition = Vector2.zero;
        nodeArea.sizeDelta = new Vector2(100f, treeAreaHeight);

        nodeScroll = view.GetComponent<ScrollRect>();
        nodeScroll.viewport = nodeView;
        nodeScroll.content = nodeArea;
        nodeScroll.horizontal = true;
        nodeScroll.vertical = false;
        nodeScroll.movementType = ScrollRect.MovementType.Clamped;
        nodeScroll.scrollSensitivity = 40f;
        PixelScrollFadeX fadeX = view.GetComponent<PixelScrollFadeX>();
        fadeX.view = nodeView;
        fadeX.content = nodeArea;

        for (int i = 0; i < 30; i++)   // connector lines first so the nodes draw over them
        {
            GameObject g = new GameObject("Line " + i, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            g.transform.SetParent(nodeArea, false);
            Image im = g.GetComponent<Image>();
            im.raycastTarget = false;
            RectTransform r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            g.SetActive(false);
            treeLines.Add(im);
        }
        for (int i = 0; i < 8; i++)
        {
            Button b = PixelUIKit.CreateButton(Font, nodeArea, "Node " + i, "", nodeSize, cardColor, textColor, 26f);
            b.gameObject.AddComponent<CanvasGroup>();
            RectTransform r = b.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMax = 28f;
            label.fontSizeMin = 12f;
            label.richText = true;
            label.rectTransform.offsetMin = new Vector2(6f, 4f);
            label.rectTransform.offsetMax = new Vector2(-6f, -4f);
            int captured = i;
            b.onClick.AddListener(() => { selectedNode = captured; RefreshWindow(); });
            b.gameObject.SetActive(false);
            nodeButtons.Add(b);
            nodeImages.Add(b.GetComponent<Image>());
            nodeLabels.Add(label);
        }

        nodeInfoLabel = PixelUIKit.CreateText(Font, treeWindow.transform, "Node Info", "", 28f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        nodeInfoLabel.richText = true;
        nodeInfoLabel.enableAutoSizing = true;
        nodeInfoLabel.fontSizeMax = 28f;
        nodeInfoLabel.fontSizeMin = 14f;
        Place(nodeInfoLabel.rectTransform, 0f, 0f, 1f, 0f, new Vector2(34f, 108f), new Vector2(-34f, 236f));

        unlockButton = PixelUIKit.CreateButton(Font, treeWindow.transform, "Unlock", "Unlock", new Vector2(10f, 70f), new Color(0.2f, 0.45f, 0.7f, 1f), textColor, 32f);
        PixelUIKit.Caps(unlockButton);
        unlockLabel = unlockButton.GetComponentInChildren<TMP_Text>();
        RectTransform ur = unlockButton.GetComponent<RectTransform>();
        ur.anchorMin = new Vector2(0f, 0f);
        ur.anchorMax = new Vector2(1f, 0f);
        ur.pivot = new Vector2(0.5f, 0f);
        ur.offsetMin = new Vector2(34f, 26f);
        ur.offsetMax = new Vector2(-34f, 96f);
        unlockButton.onClick.AddListener(() =>
        {
            SkillPack sp = packs[Mathf.Clamp(selected, 0, packs.Count - 1)];
            if (sp.nodes != null && selectedNode >= 0 && selectedNode < sp.nodes.Length) UnlockNode(sp, sp.nodes[selectedNode]);
        });

        treeWindow.SetActive(false);
        PixelWindows.Register(treeKey, 15, () => TreeOpen, () => PixelPop.Hide(treeWindow));
    }

    public bool TreeOpen => treeWindow != null && PixelPop.IsOpen(treeWindow);

    private void ApplyTreeExpandGlyph()
    {
        if (treeExpandGlyph != null) treeExpandGlyph.localRotation = Quaternion.Euler(0f, 0f, treeExpanded ? 90f : -90f);   // points left = grow over the window, right = give it back
    }

    private void ToggleTreeWindow()
    {
        if (TreeOpen) { PixelPop.Hide(treeWindow); return; }
        selectedNode = 0;
        treeExpanded = false;
        ApplyTreeExpandGlyph();
        PixelPop.Show(treeWindow);
        RefreshWindow();
    }

    /// <summary>Shows the selected pack's skill tree: nodes laid out by depth (column) and row over the whole width of the window, joined by lines.</summary>
    private void RefreshTree(SkillPack p)
    {
        int count = p.nodes != null ? Mathf.Min(p.nodes.Length, nodeButtons.Count) : 0;
        selectedNode = Mathf.Clamp(selectedNode, 0, Mathf.Max(0, count - 1));
        PixelUIKit.SetText(treeTitle, p.displayName + " - " + treeTitleText);
        PixelUIKit.SetText(pointsLabel, "Skill points: " + PointsAvailable(p) + "   (one per level)");

        int maxDepth = 0;
        for (int i = 0; i < count; i++) maxDepth = Mathf.Max(maxDepth, Depth(p, p.nodes[i]));
        float viewW = nodeView.rect.width, areaH = nodeView.rect.height;
        float colStep = maxDepth > 0 ? Mathf.Clamp((viewW - nodeSize.x) / maxDepth, nodeSize.x + 20f, nodeSize.x + 220f) : 0f;
        float rowStep = Mathf.Max(nodeSize.y + 14f, (areaH - nodeSize.y) / 2f);
        float usedW = maxDepth * colStep + nodeSize.x;
        const float pad = 20f;
        float areaW = Mathf.Max(viewW, usedW + pad * 2f);   // wider than the view = the tree scrolls sideways
        nodeArea.sizeDelta = new Vector2(areaW, areaH);
        float offX = usedW + pad * 2f > viewW ? pad : (viewW - usedW) * 0.5f;

        Vector2[] pos = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            TreeNode n = p.nodes[i];
            pos[i] = new Vector2(offX + Depth(p, n) * colStep, Mathf.Clamp(n.row, 0, 2) * rowStep);
        }
        int line = 0;
        for (int i = 0; i < nodeButtons.Count; i++)
        {
            bool used = i < count;
            nodeButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            TreeNode n = p.nodes[i];
            RectTransform r = nodeButtons[i].GetComponent<RectTransform>();
            r.sizeDelta = nodeSize;
            r.anchoredPosition = new Vector2(pos[i].x, -pos[i].y);
            bool owned = NodeUnlocked(p, n), can = CanUnlock(p, n);
            Color c = owned ? new Color(0.2f, 0.5f, 0.3f, 1f) : can ? new Color(0.25f, 0.38f, 0.62f, 1f) : cardColor;
            if (i == selectedNode) c = Color.Lerp(c, Color.white, 0.22f);
            nodeImages[i].color = c;
            PixelUIKit.SetText(nodeLabels[i], n.displayName + "\n<size=70%>" + (owned ? "OWNED" : n.cost + (n.cost == 1 ? " point" : " points")) + "</size>");

            // Connector from the parent: right edge -> a vertical step -> this node's left edge.
            TreeNode parent = ParentOf(p, n);
            int pi = parent != null ? System.Array.IndexOf(p.nodes, parent) : -1;
            if (pi < 0 || pi >= count) continue;
            Color lc = NodeUnlocked(p, parent) ? perkUnlockedColor : new Color(1f, 1f, 1f, 0.3f);
            float x0 = pos[pi].x + nodeSize.x, y0 = pos[pi].y + nodeSize.y * 0.5f;
            float x1 = pos[i].x, y1 = pos[i].y + nodeSize.y * 0.5f;
            float mid = (x0 + x1) * 0.5f;
            if (line + 3 <= treeLines.Count)
            {
                SetLine(treeLines[line++], x0, y0, mid, y0, lc);
                SetLine(treeLines[line++], mid, y0, mid, y1, lc);
                SetLine(treeLines[line++], mid, y1, x1, y1, lc);
            }
        }
        for (int i = line; i < treeLines.Count; i++) treeLines[i].gameObject.SetActive(false);

        if (count == 0) { PixelUIKit.SetText(nodeInfoLabel, ""); unlockButton.gameObject.SetActive(false); return; }
        TreeNode sel = p.nodes[selectedNode];
        bool own = NodeUnlocked(p, sel);
        TreeNode par = ParentOf(p, sel);
        string soon = sel.implemented ? "" : "  <color=#ffffff80>(coming soon)</color>";
        string guest = string.IsNullOrEmpty(sel.guestType) ? "" : "\n<color=#9be08c>Unlocks " + sel.guestType + " as a toggle for every rotation (Toggles window).</color>";
        PixelUIKit.SetText(nodeInfoLabel, "<b>" + sel.displayName + "</b>" + soon + "\n" + sel.description + guest);
        unlockButton.gameObject.SetActive(true);
        bool canUnlock = CanUnlock(p, sel);
        unlockButton.interactable = canUnlock;
        PixelUIKit.SetText(unlockLabel, own ? "Unlocked" : canUnlock ? "Unlock (" + sel.cost + " pt)"
            : (par != null && !NodeUnlocked(p, par) ? "Needs " + par.displayName : "Not enough points"));
    }

    private static void SetLine(Image img, float x0, float y0, float x1, float y1, Color color)
    {
        const float t = 5f;
        float minX = Mathf.Min(x0, x1), maxX = Mathf.Max(x0, x1), minY = Mathf.Min(y0, y1), maxY = Mathf.Max(y0, y1);
        img.gameObject.SetActive(true);
        img.color = color;
        RectTransform r = img.rectTransform;
        r.anchoredPosition = new Vector2(minX - (maxX - minX < t ? t * 0.5f : 0f), -(minY - (maxY - minY < t ? t * 0.5f : 0f)));
        r.sizeDelta = new Vector2(Mathf.Max(t, maxX - minX), Mathf.Max(t, maxY - minY));
    }

    // ---- The orange '!' tooltip on the skill cards ----

    private void BuildTip()
    {
        GameObject go = new GameObject("Skill Tip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRoot.transform, false);
        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.97f);
        bg.raycastTarget = false;
        PixelUIKit.StyleBox(bg);
        tipRect = go.GetComponent<RectTransform>();
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tipRect.pivot = new Vector2(0f, 1f);
        tipText = PixelUIKit.CreateText(Font, go.transform, "Text", "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        tipText.richText = true;
        tipText.raycastTarget = false;
        RectTransform tr = tipText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
        tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(14f, -10f);
        go.SetActive(false);
    }

    private void UpdateTip()
    {
        if (tipRect == null) return;
        string text = (WindowOpen || trackerOn) && hoverTip != null ? hoverTip() : null;
        bool show = !string.IsNullOrEmpty(text);
        if (tipRect.gameObject.activeSelf != show) tipRect.gameObject.SetActive(show);
        if (!show) return;
        const float wrap = 460f;
        PixelUIKit.SetText(tipText, text);
        Vector2 pref = tipText.GetPreferredValues(text, wrap, 0f);
        tipText.rectTransform.sizeDelta = new Vector2(wrap, pref.y);
        Vector2 size = new Vector2(wrap + 28f, pref.y + 20f);
        tipRect.sizeDelta = size;
        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
        PixelUIKit.PlaceTooltip(tipRect, canvasRect, local);
        tipRect.SetAsLastSibling();
    }

    /// <summary>The text of a card's '!' tooltip: what the skill is, what it replaces / spawns and where its XP comes from.</summary>
    private string PackTip(SkillPack p)
    {
        if (!IsAvailable(p)) return p.unlockHint;
        string tip = p.description;
        if (p.id != StandardId) tip += "\n\n<b>Replaces:</b> " + p.replaces + "\n<b>Spawns:</b> " + p.spawns + "\n<b>XP from:</b> " + p.xpFrom;
        return tip;
    }

    private RectTransform CreateInfoBox(Transform parent, float size, System.Func<string> source)
    {
        GameObject info = new GameObject("Info", typeof(RectTransform), typeof(Image));
        info.transform.SetParent(parent, false);
        info.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.12f, 1f);
        PixelUIKit.StyleButton(info.GetComponent<Image>());
        Shadow dropShadow = info.AddComponent<Shadow>();
        dropShadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        dropShadow.effectDistance = new Vector2(0f, -2f);
        RectTransform rect = info.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        TMP_Text mark = PixelUIKit.CreateText(Font, info.transform, "Mark", "!", size * 0.75f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        mark.raycastTarget = false;
        PixelUIKit.Stretch(mark.rectTransform);
        PixelHoverTip tip = info.AddComponent<PixelHoverTip>();
        tip.onEnter = () => hoverTip = source;
        tip.onExit = () => { if (hoverTip == source) hoverTip = null; };
        return rect;
    }

    private static void Place(RectTransform r, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = new Vector2(minX, minY);
        r.anchorMax = new Vector2(maxX, maxY);
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    private int lastCardClick = -1;
    private float lastCardClickTime;

    private Card BuildCard(int index)
    {
        Card c = new Card { pack = packs[index] };
        c.button = PixelUIKit.CreateButton(Font, listContent, "Card " + index, "", new Vector2(10f, 100f), cardColor, textColor, 30f);
        c.background = c.button.GetComponent<Image>();
        RectTransform rt = c.button.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(-14f, 100f);
        rt.anchoredPosition = new Vector2(0f, -index * 108f);

        TMP_Text built0 = c.button.GetComponentInChildren<TMP_Text>();
        if (built0 != null) built0.text = "";   // the card draws its own texts

        c.nameLabel = PixelUIKit.CreateText(Font, c.button.transform, "Name", "", 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        c.nameLabel.raycastTarget = false;
        PixelUIKit.Caps(c.nameLabel);
        c.nameLabel.enableAutoSizing = true;   // shrinks to fit (two lines at most) instead of breaking inside a word
        c.nameLabel.fontSizeMax = 30f;
        c.nameLabel.fontSizeMin = 12f;
        c.nameLabel.overflowMode = TextOverflowModes.Truncate;
        Place(c.nameLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(56f, -52f), new Vector2(-84f, -6f));
        RectTransform infoBox = CreateInfoBox(c.button.transform, 34f, () => PackTip(c.pack));   // the description is a tooltip on this orange '!' box
        infoBox.anchorMin = infoBox.anchorMax = infoBox.pivot = new Vector2(0f, 1f);
        infoBox.anchoredPosition = new Vector2(10f, -8f);
        infoBox.sizeDelta = new Vector2(30f, 30f);

        // A green 'skill tree' square under the '!': selects the skill and opens its tree.
        GameObject treeSquare = new GameObject("Tree Button", typeof(RectTransform), typeof(Image), typeof(Button));
        treeSquare.transform.SetParent(c.button.transform, false);
        Image treeImage = treeSquare.GetComponent<Image>();
        treeImage.color = new Color(0.2f, 0.62f, 0.3f, 1f);
        PixelUIKit.StyleButton(treeImage);
        treeSquare.GetComponent<Button>().targetGraphic = treeImage;
        RectTransform tsr = treeSquare.GetComponent<RectTransform>();
        tsr.anchorMin = tsr.anchorMax = tsr.pivot = new Vector2(0f, 1f);
        tsr.sizeDelta = new Vector2(30f, 30f);
        tsr.anchoredPosition = new Vector2(10f, -44f);
        // The glyph: a root node on top joined to two nodes below.
        void Dot(float x, float y, float w, float h)
        {
            GameObject d = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            d.transform.SetParent(treeSquare.transform, false);
            d.GetComponent<Image>().color = Color.white;
            d.GetComponent<Image>().raycastTarget = false;
            RectTransform r = d.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x, y);
        }
        Dot(0f, 8f, 8f, 7f); Dot(0f, 2f, 2f, 5f); Dot(0f, -1f, 14f, 2f); Dot(-7f, -6f, 7f, 7f); Dot(7f, -6f, 7f, 7f);
        PixelHoverTip treeTip = treeSquare.AddComponent<PixelHoverTip>();
        System.Func<string> treeSource = () => "Open the skill tree";
        treeTip.onEnter = () => hoverTip = treeSource;
        treeTip.onExit = () => { if (hoverTip == treeSource) hoverTip = null; };
        c.treeButton = treeSquare.GetComponent<Button>();
        int treeIndex = index;
        c.treeButton.onClick.AddListener(() =>
        {
            selected = treeIndex;
            selectedNode = 0;
            RefreshWindow();
            SkillPack sp = packs[Mathf.Clamp(selected, 0, packs.Count - 1)];
            if (IsAvailable(sp) && sp.nodes != null && sp.nodes.Length > 0)
            {
                PixelPop.Show(treeWindow);
                RefreshWindow();
            }
        });
        c.levelLabel = PixelUIKit.CreateText(Font, c.button.transform, "Level", "", 26f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, dimTextColor);
        c.levelLabel.raycastTarget = false;
        Place(c.levelLabel.rectTransform, 1f, 1f, 1f, 1f, new Vector2(-82f, -52f), new Vector2(-10f, -6f));
        c.levelLabel.enableAutoSizing = true;
        c.levelLabel.fontSizeMax = 24f;
        c.levelLabel.fontSizeMin = 12f;
        c.badge = PixelUIKit.CreateText(Font, c.button.transform, "Badge", activeBadgeText, 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, perkUnlockedColor);
        c.badge.raycastTarget = false;
        Place(c.badge.rectTransform, 0f, 0f, 0f, 0f, new Vector2(50f, 12f), new Vector2(130f, 40f));
        c.badge.enableAutoSizing = true;
        c.badge.fontSizeMax = 20f;
        c.badge.fontSizeMin = 10f;

        GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(c.button.transform, false);
        track.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        track.GetComponent<Image>().raycastTarget = false;
        Place(track.GetComponent<RectTransform>(), 0f, 0f, 1f, 0f, new Vector2(136f, 18f), new Vector2(-16f, 32f));
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(track.transform, false);
        c.barFill = fill.GetComponent<Image>();
        c.barFill.raycastTarget = false;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = new Vector2(0f, 1f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
        fr.pivot = new Vector2(0f, 0.5f);

        int captured = index;
        c.button.onClick.AddListener(() =>
        {
            // A second click on the same card within a moment chooses that skill (same as the Train button).
            bool doubleClick = lastCardClick == captured && Time.unscaledTime - lastCardClickTime < 0.4f;
            lastCardClick = doubleClick ? -1 : captured;
            lastCardClickTime = Time.unscaledTime;
            selected = captured;
            selectedNode = 0;
            if (doubleClick && captured >= 0 && captured < packs.Count) SetActive(packs[captured].id);
            RefreshWindow();
        });
        return c;
    }

    private void Toggle()
    {
        if (PixelTutorialDim.Active) return;   // the Skills tab is locked until the tutorial is over
        if (WindowOpen) PixelPop.Hide(windowObject);
        else OpenWindow();
    }

    private void OpenWindow()
    {
        PixelUI.SetInventoryOpen(false);   // the Skills window replaces the Inventory, the Log and the Bank window (and they replace it)
        PixelLog.SetLogOpen(false);
        PixelBank.CloseWindowIfOpen();
        selected = Mathf.Max(0, packs.FindIndex(p => p.id == TrainingId));
        PixelPop.Show(windowObject);
        RefreshWindow();
    }

    /// <summary>
    /// The tree window sits right of the Skills window and stretches to the right screen edge. When the Skills window had to shrink to fit
    /// the screen (high UI scale), both share that scale: the tree window keeps the same design height and is made wider by 1 / scale to still reach the edge.
    /// </summary>
    private void UpdateTreeLayout()
    {
        if (treeExpanded && (treeWindow == null || !TreeOpen)) { treeExpanded = false; ApplyTreeExpandGlyph(); }
        if (treeRect == null || windowObject == null || treeWindow == null || !treeWindow.activeSelf) return;
        PixelPop pop = windowObject.GetComponent<PixelPop>();
        float s = pop != null ? Mathf.Max(0.3f, pop.Fit) : 1f;
        RectTransform parent = treeRect.parent as RectTransform;
        if (parent == null) return;
        PixelHud hud = PixelHud.Ensure(gameObject);
        float parentW = parent.rect.width;
        float leftVisual = treeExpanded ? windowRect.anchoredPosition.x : windowRect.anchoredPosition.x + windowSize.x * s + 14f;   // expanded: it covers the Skills window
        float rightVisual = gapToButton + hud.SideShownInset;
        float designW = Mathf.Max(300f, (parentW - leftVisual - rightVisual) / s);
        treeRect.offsetMin = new Vector2(leftVisual, -windowSize.y * 0.5f);
        treeRect.offsetMax = new Vector2(leftVisual + designW - parentW, windowSize.y * 0.5f);
        // The window changed width (expanded, collapsed, screen resized): lay the nodes out for the new width at once instead of at the next timed refresh.
        float viewW = nodeView != null ? nodeView.rect.width : 0f;
        if (Mathf.Abs(viewW - lastTreeViewW) > 1f)
        {
            lastTreeViewW = viewW;
            if (built && WindowOpen) RefreshWindow();
        }
    }

    private float lastTreeViewW = -1f;

    private void Update()
    {
        UpdateTracker();
        UpdateXpPopups();
        CheckFarmBundle();
        UpdatePerkNotices();
        UpdateTreeLayout();
        if (!built) return;

        bool available = SkillsTabUnlocked;
        if (tabObject.activeSelf != available) tabObject.SetActive(available);

        // The tab tucks away while the window is up and fades back in afterwards, like the Bank tabs.
        bool open = WindowOpen;
        // (The tab now stays where it is while the window is up, so it can be clicked again to close it.)
        if (tabDock != null) tabDock.ForceHidden = false;
        tabGroup.alpha = 1f;
        tabGroup.blocksRaycasts = true;

        // The first time a skill pack becomes available Cubie walks through the window (the screen dims and the game pauses meanwhile).
        UpdateTutorialStart(available, open);

        UpdateTip();
        if (!open && TreeOpen) PixelPop.Hide(treeWindow);   // the tree belongs to the Skills window
        if (!open) return;
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) { refreshTimer = 0.4f; RefreshWindow(); }
    }

    private void RefreshWindow()
    {
        if (!built) return;
        refreshTimer = 0.4f;

        SkillPack active = Find(TrainingId);
        double into, need;
        int level = LevelOf(active, out into, out need);
        string trainingFormat = "Training: {0}   Lv {1}";
        string header = string.Format(trainingFormat, active.displayName, level);
        if (active.id == OfflineId) header += "   (pixels: " + Find(ActiveId).displayName + ")";
        PixelUIKit.SetText(headerLabel, header);
        int minigames = EnabledMinigames;
        float multiplier = MinigameXpMultiplier;
        PixelUIKit.SetText(minigameBonusLabel, multiplier > 1.0001f ? "Minigames: x" + multiplier.ToString("0.##") + " XP (" + minigames + " on)" : "");
        RefreshMastery();
        headerFill.anchorMax = new Vector2(Mathf.Clamp01((float)(into / need)), 1f);
        headerFill.offsetMax = new Vector2(-3f, -3f);

        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            bool open = IsAvailable(c.pack);
            bool isActive = c.pack.id == TrainingId;
            bool pixelsOnly = !isActive && OfflineMode && c.pack.id == ActiveId;   // Offline Progression is trained, this pack only supplies the pixels
            PixelUIKit.SetText(c.badge, pixelsOnly ? pixelsBadgeText : activeBadgeText);
            double ci, cn;
            int cl = LevelOf(c.pack, out ci, out cn);
            c.background.color = i == selected ? cardSelectedColor : cardColor;
            PixelUIKit.SetText(c.nameLabel, open ? c.pack.displayName : lockedName);
            PixelUIKit.SetText(c.levelLabel, open ? "Lv " + cl : "");
            c.badge.gameObject.SetActive(isActive || pixelsOnly);
            c.treeButton.gameObject.SetActive(open && c.pack.nodes != null && c.pack.nodes.Length > 0);
            c.barFill.color = open ? c.pack.accent : new Color(0.3f, 0.3f, 0.35f, 1f);
            RectTransform fr = c.barFill.rectTransform;
            fr.anchorMax = new Vector2(open ? Mathf.Clamp01((float)(ci / cn)) : 0f, 1f);
            fr.offsetMax = Vector2.zero;
        }
        PixelUIKit.UpdateScrollView(listScroll, listBar, cards.Count * 108f, listScroll.GetComponent<RectTransform>().rect.height);

        // Detail of the selected card.
        selected = Mathf.Clamp(selected, 0, packs.Count - 1);
        SkillPack p = packs[selected];
        bool available = IsAvailable(p);
        PixelUIKit.SetText(detailName, available ? p.displayName : lockedName);
        PixelUIKit.SetText(detailInfo, available && p.id == OfflineId ? "<b>Base Pixel Pack:</b>" : "");

        // The skill tree has its own window; the button only shows for a skill that has one.
        bool hasNodes = available && p.nodes != null && p.nodes.Length > 0;
        treeToggle.gameObject.SetActive(hasNodes);
        PixelUIKit.SetText(treeToggleLabel, treeButtonText);
        if (!hasNodes && TreeOpen) PixelPop.Hide(treeWindow);
        if (hasNodes && TreeOpen) RefreshTree(p);
        bool showTree = false;

        // Offline Progression: one button per pack whose pixels you can play with.
        int slot = 0;
        foreach (SkillPack other in packs)
        {
            if (other.id == OfflineId || !IsAvailable(other)) continue;
            if (slot >= pixelPackButtons.Count) break;
            bool show = available && p.id == OfflineId && !showTree;
            pixelPackButtons[slot].gameObject.SetActive(show);
            if (show)
            {
                bool current = other.id == ActiveId;
                PixelUIKit.SetText(pixelPackLabels[slot], other.displayName + (current ? "   (playing)" : ""));
                pixelPackButtons[slot].GetComponent<Image>().color = current ? new Color(0.2f, 0.5f, 0.3f, 1f) : cardColor;
            }
            slot++;
        }
        for (int i = slot; i < pixelPackButtons.Count; i++) pixelPackButtons[i].gameObject.SetActive(false);

        int pl = LevelOf(p);
        bool showPerks = available && !showTree && p.id != OfflineId && p.perks != null;
        perkScroll.gameObject.SetActive(showPerks);
        float py = 0f;
        float bodyWidth = Mathf.Max(200f, perkContent.rect.width > 1f ? perkContent.rect.width - 40f : 360f);
        for (int i = 0; i < perkHeads.Count; i++)
        {
            bool used = showPerks && i < p.perks.Length;
            perkHeads[i].gameObject.SetActive(used);
            perkBodies[i].gameObject.SetActive(false);
            if (!used) continue;
            Perk perk = p.perks[i];
            bool got = pl >= perk.level && perk.implemented;
            bool open = perkOpen.Contains(p.id + ":" + perk.level);
            string status = got ? "unlocked" : (perk.implemented ? "locked" : "coming soon");
            string color = got ? "#8CFF99" : "#C8CCD8";
            PixelUIKit.SetText(perkHeadLabels[i], (open ? "-  " : "+  ") + "Level " + perk.level + "   <size=70%><color=" + color + ">" + status.ToUpperInvariant() + "</color></size>");
            RectTransform hr = perkHeads[i].GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(0f, 44f);
            hr.anchoredPosition = new Vector2(0f, -py);
            py += 48f;
            if (open)
            {
                TMP_Text body = perkBodies[i];
                body.gameObject.SetActive(true);
                PixelUIKit.SetText(body, perk.text);
                float h = body.GetPreferredValues(perk.text, bodyWidth, 0f).y + 14f;
                RectTransform br = body.rectTransform;
                br.anchorMin = new Vector2(0f, 1f);
                br.anchorMax = new Vector2(1f, 1f);
                br.pivot = new Vector2(0.5f, 1f);
                br.offsetMin = new Vector2(20f, 0f);
                br.offsetMax = new Vector2(-20f, 0f);
                br.sizeDelta = new Vector2(-40f, h);
                br.anchoredPosition = new Vector2(0f, -py - 4f);
                py += h + 10f;
            }
        }
        if (showPerks)
        {
            RectTransform sr = perkScroll.GetComponent<RectTransform>();
            PixelUIKit.UpdateScrollView(perkScroll, perkBar, py, sr.rect.height > 1f ? sr.rect.height : 260f);
        }

        bool isActiveSel = p.id == TrainingId;
        trainButton.interactable = available && !isActiveSel;
        PixelUIKit.SetText(trainLabel, isActiveSel ? trainingText : (p.id == StandardId ? useStandardText : trainText));
        trainButton.gameObject.SetActive(available);
    }
}
