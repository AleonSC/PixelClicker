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

    [TextArea(2, 6)]
    [Tooltip("The first-time explanation shown beside the window.")]
    [SerializeField] private string introText = "Skill packs swap the basic cubes in your rotation for a new set. Pick a pack on the left and press Train: its pixels start to spawn and you earn XP for that skill. XP is kept per pack, so you can switch back and forth whenever you like.";

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
    private int selected;
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
        Place(detailName.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -62f), new Vector2(-22f, -10f));
        detailDesc = PixelUIKit.CreateText(Font, detail.transform, "Description", "", 24f, TextAlignmentOptions.TopLeft, FontStyles.Normal, dimTextColor);
        detailDesc.enableAutoSizing = true;   // always fits its band
        detailDesc.fontSizeMax = 24f;
        detailDesc.fontSizeMin = 13f;
        Place(detailDesc.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -158f), new Vector2(-22f, -66f));
        detailInfo = PixelUIKit.CreateText(Font, detail.transform, "Info", "", 22f, TextAlignmentOptions.TopLeft, FontStyles.Normal, textColor);
        detailInfo.richText = true;
        detailInfo.enableAutoSizing = true;
        detailInfo.fontSizeMax = 22f;
        detailInfo.fontSizeMin = 13f;
        Place(detailInfo.rectTransform, 0f, 1f, 1f, 1f, new Vector2(22f, -312f), new Vector2(-22f, -164f));
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
            pr.anchoredPosition = new Vector2(0f, -(320f + i * 36f));
            row.gameObject.SetActive(false);
            perkRows.Add(row);
        }

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

        windowObject.SetActive(false);
        PixelWindows.Register(this, 14, () => WindowOpen, () => PixelPop.Hide(windowObject));
        Changed += () => { if (WindowOpen) RefreshWindow(); };
        built = true;
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
        Place(c.nameLabel.rectTransform, 0f, 1f, 1f, 1f, new Vector2(18f, -48f), new Vector2(-110f, -8f));
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
        c.button.onClick.AddListener(() => { selected = captured; RefreshWindow(); });
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
        selected = Mathf.Max(0, packs.FindIndex(p => p.id == ActiveId));
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

        if (!open) return;
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f) { refreshTimer = 0.4f; RefreshWindow(); }
    }

    private void RefreshWindow()
    {
        if (!built) return;
        refreshTimer = 0.4f;

        SkillPack active = Find(ActiveId);
        double into, need;
        int level = LevelOf(active, out into, out need);
        string trainingFormat = active.id == StandardId ? "Playing: {0}" : "Training: {0}   Lv {1}";
        PixelUIKit.SetText(headerLabel, string.Format(trainingFormat, active.displayName, level));
        headerFill.anchorMax = new Vector2(active.id == StandardId ? 0f : Mathf.Clamp01((float)(into / need)), 1f);
        headerFill.offsetMax = new Vector2(-3f, -3f);

        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            bool open = IsAvailable(c.pack);
            bool isActive = c.pack.id == ActiveId;
            double ci, cn;
            int cl = LevelOf(c.pack, out ci, out cn);
            c.background.color = i == selected ? cardSelectedColor : cardColor;
            PixelUIKit.SetText(c.nameLabel, open ? c.pack.displayName : lockedName);
            PixelUIKit.SetText(c.levelLabel, open && c.pack.id != StandardId ? "Lv " + cl : "");
            c.badge.gameObject.SetActive(isActive);
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
        PixelUIKit.SetText(detailDesc, available ? p.description : p.unlockHint);
        if (available)
        {
            string info = "<b>Replaces:</b> " + p.replaces + "\n<b>Spawns:</b> " + p.spawns + "\n<b>XP from:</b> " + p.xpFrom;
            PixelUIKit.SetText(detailInfo, info);
        }
        else PixelUIKit.SetText(detailInfo, "");

        int pl = LevelOf(p);
        for (int i = 0; i < perkRows.Count; i++)
        {
            bool used = available && p.perks != null && i < p.perks.Length;
            perkRows[i].gameObject.SetActive(used);
            if (!used) continue;
            Perk perk = p.perks[i];
            bool got = pl >= perk.level && perk.implemented;
            string tag = perk.implemented ? "" : "  <color=#ffffff80>(coming soon)</color>";
            PixelUIKit.SetText(perkRows[i], "Lv " + perk.level + "   " + perk.text + tag);
            perkRows[i].color = got ? perkUnlockedColor : dimTextColor;
        }

        bool isActiveSel = p.id == ActiveId;
        trainButton.interactable = available && !isActiveSel;
        PixelUIKit.SetText(trainLabel, isActiveSel ? trainingText : (p.id == StandardId ? useStandardText : trainText));
        trainButton.gameObject.SetActive(available);
    }
}
