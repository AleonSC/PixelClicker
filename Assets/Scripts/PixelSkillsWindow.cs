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

    [Tooltip("Gap between the Bank / Hose buttons and the Skills tab under them.")]
    [SerializeField] private float tabGap = 10f;

    [SerializeField] private string windowTitle = "Skills";
    [SerializeField] private string tabText = "Skills";
    [SerializeField] private string trainText = "Train";
    [SerializeField] private string trainingText = "Training";
    [SerializeField] private string useStandardText = "Use standard pixels";
    [SerializeField] private string activeBadgeText = "ACTIVE";
    [SerializeField] private string lockedName = "???";
    [SerializeField] private string treeButtonText = "Skill tree";
    [SerializeField] private string detailsButtonText = "Details";
    [SerializeField] private string pixelsBadgeText = "PIXELS";

    [SerializeField] private string treeTitleText = "Skill tree";

    [Tooltip("Size of one skill tree node (canvas units).")]
    [SerializeField] private Vector2 nodeSize = new Vector2(190f, 90f);

    [Tooltip("Height of the area the skill tree nodes are laid out in (canvas units).")]
    [SerializeField] private float treeAreaHeight = 400f;

    [TextArea(2, 6)]
    [Tooltip("The first-time explanation shown beside the window.")]
    [SerializeField] private string introText = "Skill packs swap the basic cubes in your rotation for a new set. Pick a pack on the left and press Train: its pixels start to spawn and you earn XP for that skill. XP is kept per pack, so you can switch back and forth whenever you like. Offline Progression lets you play with any pack while only that skill earns XP, and every skill has a skill tree: spend the point you earn each level on its nodes.";

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
        public Button button;
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
    private TMP_Text headerLabel, detailName, detailDesc, detailInfo, trainLabel;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<TMP_Text> perkRows = new List<TMP_Text>();
    private int selected, selectedNode;
    private Button treeToggle, unlockButton;
    private TMP_Text treeToggleLabel, pointsLabel, nodeInfoLabel, unlockLabel, treeTitle, tipText;
    private RectTransform nodeArea, treeRect, tipRect;
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
        PixelHud hud = PixelHud.Ensure(gameObject);
        canvasRoot = PixelUIKit.CreateCanvas("PixelSkills Canvas", sortingOrder, new Vector2(1920f, 1080f), true);

        // ---- The tab: left edge, directly under the Bank + Hose buttons (same width as the pair). ----
        float groupWidth = PixelBank.DockGroupWidth(hud);
        tabButton = PixelUIKit.CreateButton(Font, canvasRoot.transform, "Skills Button", tabText, hud.ButtonSize, tabColor, textColor, 30f);
        tabObject = tabButton.gameObject;
        RectTransform tabRect = tabButton.GetComponent<RectTransform>();
        hud.DockInGroup(tabRect, new Vector2(0f, 0.5f), () => WindowOpen || (tabDock != null && tabDock.PointerNear()),
                        -(hud.ButtonSize.y + tabGap), groupWidth, 0f, groupWidth);
        tabDock = tabButton.GetComponent<PixelDockedButton>();
        tabGroup = tabObject.AddComponent<CanvasGroup>();
        tabButton.onClick.AddListener(Toggle);
        tabObject.SetActive(false);

        // ---- The window. ----
        windowObject = new GameObject("Skills Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(windowObject.GetComponent<Image>(), panelColor);
        windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0f, 0.5f);
        windowRect.sizeDelta = windowSize;
        windowRect.anchoredPosition = new Vector2(gapToButton + hud.SideMargin * 0.35f, 0f);   // takes the place of the tabs while it is open

        TMP_Text title = PixelUIKit.CreateText(Font, windowObject.transform, "Title", windowTitle, 56f, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        PixelUIKit.Caps(title);
        Place(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(100f, -84f), new Vector2(-100f, -14f));

        Button close = PixelUIKit.CreateButton(Font, windowObject.transform, "Close", "X", new Vector2(70f, 70f), new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 36f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -12f);
        close.onClick.AddListener(() => PixelPop.Hide(windowObject));

        // Header: "Training: Mining  Lv 3" + an XP bar.
        headerLabel = PixelUIKit.CreateText(Font, windowObject.transform, "Header", "", 34f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        Place(headerLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(30f, -158f), new Vector2(-30f, -100f));
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

        // Left: the pack cards.
        listScroll = PixelUIKit.CreateScrollView(windowObject.transform, "Pack List", new Color(1f, 1f, 1f, 0.35f), 10f, 60f, out listContent, out listBar);
        RectTransform lr = listScroll.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f);
        lr.anchorMax = new Vector2(0f, 1f);
        lr.pivot = new Vector2(0f, 0.5f);
        lr.offsetMin = new Vector2(26f, 24f);
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
        dr.offsetMin = new Vector2(380f, 24f);
        dr.offsetMax = new Vector2(-26f, -204f);

        detailName = PixelUIKit.CreateText(Font, detail.transform, "Name", "", 44f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, textColor);
        PixelUIKit.Caps(detailName);
        Place(detailName.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -62f), new Vector2(-200f, -10f));
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
        for (int i = 0; i < 8; i++)
        {
            TMP_Text row = PixelUIKit.CreateText(Font, detail.transform, "Perk " + i, "", 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, dimTextColor);
            row.enableAutoSizing = true;
            row.fontSizeMax = 22f;
            row.fontSizeMin = 12f;
            row.richText = true;
            RectTransform pr = row.rectTransform;
            pr.anchorMin = new Vector2(0f, 1f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(0.5f, 1f);
            pr.sizeDelta = new Vector2(-44f, 32f);
            pr.anchoredPosition = new Vector2(0f, -(84f + i * 38f));
            row.gameObject.SetActive(false);
            perkRows.Add(row);
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
        float right = gapToButton + hud.SideMargin * 0.35f;
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

        pointsLabel = PixelUIKit.CreateText(Font, treeWindow.transform, "Points", "", 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, perkUnlockedColor);
        Place(pointsLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(34f, -150f), new Vector2(-34f, -100f));

        GameObject area = new GameObject("Nodes", typeof(RectTransform));
        area.transform.SetParent(treeWindow.transform, false);
        nodeArea = area.GetComponent<RectTransform>();
        nodeArea.anchorMin = new Vector2(0f, 1f);
        nodeArea.anchorMax = new Vector2(1f, 1f);
        nodeArea.pivot = new Vector2(0f, 1f);
        nodeArea.offsetMin = new Vector2(34f, -150f - treeAreaHeight);
        nodeArea.offsetMax = new Vector2(-34f, -150f);

        for (int i = 0; i < 30; i++)   // connector lines first so the nodes draw over them
        {
            GameObject g = new GameObject("Line " + i, typeof(RectTransform), typeof(Image));
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

    private void ToggleTreeWindow()
    {
        if (TreeOpen) { PixelPop.Hide(treeWindow); return; }
        selectedNode = 0;
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
        float areaW = nodeArea.rect.width, areaH = nodeArea.rect.height;
        float colStep = maxDepth > 0 ? Mathf.Clamp((areaW - nodeSize.x) / maxDepth, nodeSize.x + 20f, nodeSize.x + 220f) : 0f;
        float rowStep = Mathf.Max(nodeSize.y + 14f, (areaH - nodeSize.y) / 2f);
        float usedW = maxDepth * colStep + nodeSize.x;
        float offX = Mathf.Max(0f, (areaW - usedW) * 0.5f);

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
        PixelUIKit.SetText(nodeInfoLabel, "<b>" + sel.displayName + "</b>" + soon + "\n" + sel.description);
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
        string text = WindowOpen && hoverTip != null ? hoverTip() : null;
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
        Vector2 half = canvasRect.rect.size * 0.5f;
        Vector2 pos = local + new Vector2(18f, -18f);
        if (pos.x + size.x > half.x) pos.x = local.x - size.x - 18f;
        if (pos.y - size.y < -half.y) pos.y = local.y + size.y + 18f;
        tipRect.anchoredPosition = pos;
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
        Place(c.nameLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(56f, -48f), new Vector2(-110f, -8f));
        RectTransform infoBox = CreateInfoBox(c.button.transform, 34f, () => PackTip(c.pack));   // the description is a tooltip on this orange '!' box
        infoBox.anchorMin = infoBox.anchorMax = infoBox.pivot = new Vector2(0f, 1f);
        infoBox.anchoredPosition = new Vector2(10f, -10f);
        c.levelLabel = PixelUIKit.CreateText(Font, c.button.transform, "Level", "", 26f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, dimTextColor);
        c.levelLabel.raycastTarget = false;
        Place(c.levelLabel.rectTransform, 1f, 1f, 1f, 1f, new Vector2(-110f, -48f), new Vector2(-14f, -8f));
        c.badge = PixelUIKit.CreateText(Font, c.button.transform, "Badge", activeBadgeText, 20f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, perkUnlockedColor);
        c.badge.raycastTarget = false;
        Place(c.badge.rectTransform, 0f, 0f, 0f, 0f, new Vector2(18f, 12f), new Vector2(120f, 40f));

        GameObject track = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(c.button.transform, false);
        track.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
        track.GetComponent<Image>().raycastTarget = false;
        Place(track.GetComponent<RectTransform>(), 0f, 0f, 1f, 0f, new Vector2(124f, 18f), new Vector2(-16f, 32f));
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
        c.button.onClick.AddListener(() => { selected = captured; selectedNode = 0; RefreshWindow(); });
        return c;
    }

    private void Toggle()
    {
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
        if (!PixelHints.IsTipSeen("skills_intro"))
        {
            PixelHints.SetTipSeen("skills_intro");
            PixelNotice.Show(introText, 0f, compact: true, beside: windowRect);
        }
    }

    private void Update()
    {
        if (!built) return;

        bool available = AnyTrainable;
        if (tabObject.activeSelf != available) tabObject.SetActive(available);

        // The tab tucks away while the window is up and fades back in afterwards, like the Bank tabs.
        bool open = WindowOpen;
        if (tabDock != null) tabDock.ForceHidden = open || tabGroup.alpha < 0.05f;
        if (open) { tabGroup.alpha = 0f; tabGroup.blocksRaycasts = false; }
        else
        {
            tabGroup.blocksRaycasts = true;
            tabGroup.alpha = Mathf.MoveTowards(tabGroup.alpha, 1f, Time.unscaledDeltaTime / 1.2f);
        }

        // The first time a skill pack becomes available the window opens by itself and explains the idea (once the screen is calm).
        if (available && !open && !PixelHints.IsTipSeen("skills_intro") && Time.timeScale > 0f && !PixelTitleScreen.Showing
            && !PixelPauseMenu.IsPaused && !PixelNotice.IsShowing)
            OpenWindow();

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
        string trainingFormat = active.id == StandardId ? "Playing: {0}" : "Training: {0}   Lv {1}";
        string header = string.Format(trainingFormat, active.displayName, level);
        if (active.id == OfflineId) header += "   (pixels: " + Find(ActiveId).displayName + ")";
        PixelUIKit.SetText(headerLabel, header);
        headerFill.anchorMax = new Vector2(active.id == StandardId ? 0f : Mathf.Clamp01((float)(into / need)), 1f);
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
            PixelUIKit.SetText(c.levelLabel, open && c.pack.id != StandardId ? "Lv " + cl : "");
            c.badge.gameObject.SetActive(isActive || pixelsOnly);
            c.barFill.color = open ? c.pack.accent : new Color(0.3f, 0.3f, 0.35f, 1f);
            RectTransform fr = c.barFill.rectTransform;
            fr.anchorMax = new Vector2(open && c.pack.id != StandardId ? Mathf.Clamp01((float)(ci / cn)) : 0f, 1f);
            fr.offsetMax = Vector2.zero;
        }
        PixelUIKit.UpdateScrollView(listScroll, listBar, cards.Count * 108f, listScroll.GetComponent<RectTransform>().rect.height);

        // Detail of the selected card.
        selected = Mathf.Clamp(selected, 0, packs.Count - 1);
        SkillPack p = packs[selected];
        bool available = IsAvailable(p);
        PixelUIKit.SetText(detailName, available ? p.displayName : lockedName);
        PixelUIKit.SetText(detailInfo, available && p.id == OfflineId ? "<b>Pixels to play with:</b>" : "");

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
        for (int i = 0; i < perkRows.Count; i++)
        {
            bool used = available && !showTree && p.id != OfflineId && p.perks != null && i < p.perks.Length;
            perkRows[i].gameObject.SetActive(used);
            if (!used) continue;
            Perk perk = p.perks[i];
            bool got = pl >= perk.level && perk.implemented;
            string tag = perk.implemented ? "" : "  <color=#ffffff80>(coming soon)</color>";
            PixelUIKit.SetText(perkRows[i], "Lv " + perk.level + "   " + perk.text + tag);
            perkRows[i].color = got ? perkUnlockedColor : dimTextColor;
        }

        bool isActiveSel = p.id == TrainingId;
        trainButton.interactable = available && !isActiveSel;
        PixelUIKit.SetText(trainLabel, isActiveSel ? trainingText : (p.id == StandardId ? useStandardText : trainText));
        trainButton.gameObject.SetActive(available);
    }
}
