using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The shop's user interface (button, panel, tabs, scrolling list, upgrades window, trackers, refreshing).
/// Part of <see cref="PixelShop"/> - the buying logic and all Inspector settings are in PixelShop.cs.
/// The shop and its Upgrades window are standard Fixed windows (PixelUIKit.CreateWindow): ? in the right header slot, Back in the left one.
/// </summary>
public partial class PixelShop
{
    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    /// <summary>Drops a "(you have {n})" part from a format string (scenes saved earlier still carry it).</summary>
    private static string NoHave(string format)
    {
        return System.Text.RegularExpressions.Regex.Replace(format ?? "", @"\s*\((you have|owned)[^)]*\)", "");
    }

    private class PackRow
    {
        public RectTransform rect;
        public TMP_Text nameLabel;
        public RectTransform costBox;                                 // the boxed layout's cost box (set by StyleBoxedRow)
        public bool boxed;
        public TMP_Text descLabel;
        public Button buyButton;
        public Image buyImage;
        public TMP_Text buyLabel;
        public TMP_Text costLabel;
        public Button arrowButton;
        public bool isPotion;
        public TMP_Text levelLabel;                                   // "Level 4/5" on its own single line (upgrade rows)
        public RectTransform markerRoot;                              // the row of level boxes next to it
        public readonly System.Collections.Generic.List<Image> markers = new System.Collections.Generic.List<Image>();
    }

    private RectTransform panelRect;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private TMP_Text emptyLabel;
    private ShopTab currentTab = ShopTab.Pixels;
    private GameObject subPanelObject;
    private RectTransform subContentRect;
    private ScrollRect subScrollRect;
    private TMP_Text subEmptyLabel;
    private TMP_Text subTitle;
    private int openParent = -1;
    private PackRow[] valueRows;           // one Value upgrade row per pixel type (Upgrades > Value sub-tab)
    private RectTransform subTabRow;       // the "Upgrades | Pixel" buttons at the top of the Upgrades tab
    private Image[] subTabImages;
    private int upgradesSubTab;            // 0 = Features (the packs), 1 = Value upgrades
    private GameObject canvasRoot;
    private GameObject shopButtonObject;
    private GameObject panelObject;
    private PackRow[] rows;
    private System.Func<string> hoverTip; // set while the mouse is over an orange '!' box: returns the description to show as a tooltip
    private bool builtOk;
    private bool wasButtonVisible;
    private AudioSource audioSource;

    // ------------------------------------------------------------------
    // UI building
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelShop Canvas", sortingOrder, referenceResolution, true);

        BuildShopButton(canvasRoot.transform);
        BuildPanel(canvasRoot.transform);
    }

    private void BuildShopButton(Transform parent)
    {
        Vector2 anchor = new Vector2(
            buttonCorner == ButtonCorner.TopRight || buttonCorner == ButtonCorner.BottomRight ? 1f : 0f,
            buttonCorner == ButtonCorner.TopLeft || buttonCorner == ButtonCorner.TopRight ? 1f : 0f);

        Button button = CreateButton(parent, "Shop Button", buttonText, buttonSize, ButtonRole.Primary,
                                     buttonTextColor, buttonFontSize, out _, out _);
        shopButtonObject = button.gameObject;

        RectTransform rt = shopButtonObject.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = new Vector2(anchor.x > 0.5f ? -buttonMargin.x : buttonMargin.x,
                                          anchor.y > 0.5f ? -buttonMargin.y : buttonMargin.y);
        PixelHud.Ensure(gameObject).Dock(rt, anchor, () => (panelObject != null && panelObject.activeSelf) || PixelGuideVendor.ShopHold);   // (stays slid out while Cubie's hold waits for the first click)

        button.onClick.AddListener(() =>
        {
            if (PixelTutorialDim.Active) return; // the shop buttons are locked until the tutorial is over
            if (PixelPop.IsOpen(panelObject)) PixelPop.Hide(panelObject); else PixelPop.Show(panelObject);
            if (PixelPop.IsOpen(panelObject)) RefreshRows();
        });
        PixelMenuKeys.Register(PixelAction.Shop, button);
        shopButtonObject.SetActive(false); // Update() reveals it once the required tier is unlocked.
    }

    private void BuildPanel(Transform parent)
    {
        currentTab = startTab == ShopTab.Automatic ? ShopTab.Pixels : startTab;

        // The standard Fixed window (PixelUIKit.CreateWindow): title, grey X, open / close animation (PixelPop). Centred, like before.
        // (The search box fits in the standard 90 header, so no headerHeight override.)
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, parent, "Shop Panel", panelTitle, new Vector2(panelWidth, panelHeight),
                                                         PixelUIKit.WindowKind.Fixed, () => PixelPop.Hide(panelObject));
        panelObject = parts.Root;
        panelRect = parts.Rect;
        ShopButtonExtras(parts.Close);
        PixelNotice.FixedSize = tipBoxMatchesShop ? new Vector2(panelWidth, panelHeight) : Vector2.zero;
        PixelNotice.SideAnchor = tipBoxMatchesShop ? panelRect : null; // tip boxes sit right of the shop
        PixelNotice.SideWidth = 0f;   // (no currency panel on the left any more: the amounts are a strip inside the window)
        PixelNotice.SideGap = currencyPanelGap;

        // The fancy menu look (Settings > Interface > Fancy pause menu): a colour-cycling glow behind the panel and a light segment sliding under the title.
        GameObject glow = new GameObject("Fancy Panel Glow", typeof(RectTransform), typeof(Image), typeof(PixelMenuPanelGlow));
        glow.transform.SetParent(parent, false);
        glow.transform.SetSiblingIndex(panelObject.transform.GetSiblingIndex());   // just behind the panel
        glow.GetComponent<Image>().raycastTarget = false;
        glow.GetComponent<PixelMenuPanelGlow>().target = panelRect;
        PixelUIKit.StyleBox(glow.GetComponent<Image>());
        PixelMenuFx.BuildTitleLine(panelRect, panelPadding, -(headerHeight + tabsDrop * 0.3f), panelWidth - panelPadding * 2f);   // the sliding white segment under the tabs

        // Help button (blue square with a question mark, in the standard right header slot just left of the X): hold it to replay the shop tutorial.
        Button help = parts.AddHeaderButton("Help", "?", false, ButtonRole.Primary);
        helpButtonRect = help.GetComponent<RectTransform>();
        ShopButtonExtras(help);

        // Cubie's bill (PixelCubieBill): an 'IOU' button in the right header slots, just left of the ?, shown once he has handed the bill over.
        // (Not the left slot: the search box sits there and covered it.)
        Button iou = parts.AddHeaderButton("Bill", billButtonText, false, ButtonRole.Primary);
        ShopButtonExtras(iou);
        iou.onClick.AddListener(PixelCubieBillWindow.Toggle);
        billButton = iou.gameObject;
        billButton.SetActive(PixelCubieBill.IsRevealed);
        PixelHoverTip iouTip = iou.gameObject.AddComponent<PixelHoverTip>();
        System.Func<string> iouText = () => "Cubie's bill: pay it off to earn his trust (and his better stock)";
        iouTip.onEnter = () => hoverTip = iouText;
        iouTip.onExit = () => { if (hoverTip == iouText) hoverTip = null; };
        PixelUIKit.AddHoldAction(help, helpHoldSeconds, () =>
        {
            PixelShopTutorial tut = PixelFind.First<PixelShopTutorial>();
            if (tut != null) tut.Replay();
        });
        PixelHoverTip helpTip = help.gameObject.AddComponent<PixelHoverTip>();
        System.Func<string> helpText = () => "Hold to replay the shop tutorial";
        helpTip.onEnter = () => hoverTip = helpText;
        helpTip.onExit = () => { if (hoverTip == helpText) hoverTip = null; };

        // The title sits at the top LEFT (the catalog layout), the search box right after it.
        const float titleWidth = 190f;
        parts.Title.alignment = TextAlignmentOptions.MidlineLeft;
        parts.Title.rectTransform.offsetMin = new Vector2(panelPadding + 4f, parts.Title.rectTransform.offsetMin.y);
        parts.Title.rectTransform.offsetMax = new Vector2(-(panelWidth - panelPadding - 4f - titleWidth), 0f);

        // Search box (after the title): lists every match from every part of the shop while it has text (the catalog's search entry).
        searchField = PixelUIKit.CreateInputField(font, panelObject.transform, "Search", new Vector2(Mathf.Max(120f, searchWidth), 64f),
                                                  disabledColor, textColor, tabFontSize * 0.85f, searchPlaceholder);
        PixelUIKit.AddSearchIcon(searchField);
        RectTransform sr = searchField.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
        sr.anchoredPosition = new Vector2(panelPadding + titleWidth + 16f, -(headerHeight - 64f) * 0.5f);
        searchField.onValueChanged.AddListener(text =>
        {
            shopSearch = (text ?? "").Trim().ToLowerInvariant();
            if (catalog != null) { catalog.content.anchoredPosition = Vector2.zero; catalog.selected[SearchEntry] = -1; }
            if (rows != null) RefreshRows();
        });

        // (the old tab bar is not built; the scrolling list stays hidden behind the catalog - its rows serve the pack Upgrades window)
        BuildScrollArea(panelObject.transform, CatalogTop, out contentRect, out scrollRect, out emptyLabel);
        BuildUpgradesWindow(parent);
        BuildShopTooltip();

        // Pack rows live inside the scrolling content.
        rows = new PackRow[packs.Length];
        for (int i = 0; i < packs.Length; i++)
            rows[i] = BuildRow(IsChild(i) ? subContentRect : contentRect, i);

        // One tracker row for every minigame that has a goal counter.
        trackers.Clear();
        foreach (PixelMinigame minigame in PixelMinigame.All)
            if (minigame != null && minigame.HasTracker)
                trackers[minigame] = BuildTracker(contentRect, minigame.Id + " Tracker", minigame.TrackerTitle, minigame.TrackerDescription);

        // The two sub-tab buttons at the top of the Upgrades tab (normal upgrades | Pixel boosts).
        BuildSubTabs(contentRect);

        // The catalog: category rail, Saving up for card, tile grid and detail panel (PixelShopCatalog.cs).
        railEntry = Mathf.Max(0, EntryOf(currentTab, 0));
        BuildCatalog(panelObject.transform);

        panelObject.SetActive(false);
        subPanelObject.SetActive(false);
    }

    private void BuildSubTabs(Transform parent)
    {
        GameObject go = new GameObject("Upgrade Sub Tabs", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        subTabRow = go.GetComponent<RectTransform>();
        subTabRow.anchorMin = new Vector2(0f, 1f);
        subTabRow.anchorMax = new Vector2(1f, 1f);
        subTabRow.pivot = new Vector2(0.5f, 1f);
        subTabRow.sizeDelta = new Vector2(0f, subTabHeight);

        string[] names = { featuresSubTabText, valueSubTabText, operationSubTabText };
        int n = names.Length;
        subTabImages = new Image[n];
        for (int i = 0; i < n; i++)
        {
            Button b = CreateButton(go.transform, "Sub Tab " + names[i], names[i], Vector2.zero, ButtonRole.Neutral, textColor,
                                    tabFontSize, out TMP_Text subLabel, out subTabImages[i]);
            PixelUIKit.Caps(b);
            subLabel.enableAutoSizing = true;
            subLabel.fontSizeMax = tabFontSize;
            subLabel.fontSizeMin = Mathf.Min(14f, tabFontSize);
            subLabel.rectTransform.offsetMin = new Vector2(tabTextPadding, 4f);
            subLabel.rectTransform.offsetMax = new Vector2(-tabTextPadding, -4f);
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2((float)i / n, 0f);
            rt.anchorMax = new Vector2((float)(i + 1) / n, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(i == 0 ? 0f : tabSpacing * 0.5f, 0f);
            rt.offsetMax = new Vector2(i == n - 1 ? 0f : -tabSpacing * 0.5f, 0f);

            int captured = i;
            b.onClick.AddListener(() =>
            {
                upgradesSubTab = captured;
                if (contentRect != null) contentRect.anchoredPosition = Vector2.zero;
                RefreshRows();
            });
        }
        go.SetActive(false);
    }

    /// <summary>A plain row (name, description, cost, button) not tied to a pack or potion; the caller wires the button.</summary>
    private PackRow BuildPlainRow(Transform parent, string objectName)
    {
        PackRow row = new PackRow();
        RectTransform rr = CreateRowBox(parent, objectName, rowHeight);
        row.rect = rr;

        float inset = buyButtonSize.x + 40f;

        AddRowLabels(row, rr, "", "", inset);
        AddRowBuyButton(row, rr, ultraButtonText);
        LayoutRowHeader(row, inset);
        return row;
    }

    /// <summary>A row's box: a styled panel stretched across the list, anchored at the top, 'height' tall (pack rows, plain rows, trackers).</summary>
    private RectTransform CreateRowBox(Transform parent, string objectName, float height)
    {
        GameObject rowGo = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        rowGo.transform.SetParent(parent, false);
        rowGo.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(rowGo.GetComponent<Image>());
        RectTransform rr = rowGo.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, height);
        return rr;
    }

    /// <summary>A row's name, description and cost lines: each in its band, kept 'inset' clear of the Buy button, and long texts
    /// (names, descriptions, six-currency cost lists) shrink to fit their band instead of spilling over the lines below.</summary>
    private void AddRowLabels(PackRow row, Transform rowParent, string rowName, string rowDescription, float inset)
    {
        row.nameLabel = CreateText(rowParent, "Name", rowName, nameFontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        row.nameLabel.richText = true;
        row.nameLabel.enableAutoSizing = true;
        row.nameLabel.fontSizeMax = nameFontSize;
        row.nameLabel.fontSizeMin = Mathf.Min(16f, nameFontSize);
        SetBand(row.nameLabel.rectTransform, 0.74f, 1f, inset);

        row.descLabel = CreateText(rowParent, "Description", rowDescription, descriptionFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        row.descLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        row.descLabel.enableAutoSizing = true;
        row.descLabel.fontSizeMax = descriptionFontSize;
        row.descLabel.fontSizeMin = Mathf.Min(12f, descriptionFontSize);
        SetBand(row.descLabel.rectTransform, 0.38f, 0.74f, inset);

        row.costLabel = CreateText(rowParent, "Cost", "", costFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        row.costLabel.richText = true;
        row.costLabel.enableAutoSizing = true;
        row.costLabel.fontSizeMax = costFontSize;
        row.costLabel.fontSizeMin = Mathf.Min(14f, costFontSize);
        SetBand(row.costLabel.rectTransform, 0.04f, 0.36f, inset);
    }

    /// <summary>A row's green Buy button at the right edge, centred on the row; returns its rect (a pack with upgrades moves it up).</summary>
    private RectTransform AddRowBuyButton(PackRow row, Transform rowParent, string label)
    {
        row.buyButton = CreateButton(rowParent, "Buy", label, buyButtonSize, ButtonRole.Success, textColor, buyFontSize,
                                     out row.buyLabel, out row.buyImage);
        RectTransform br = row.buyButton.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.anchoredPosition = new Vector2(-20f, 0f);
        return br;
    }

    /// <summary>Makes sure there is a Value row for every pixel type (the shop can add pixel types after the UI was built).</summary>
    private void EnsureValueRows()
    {
        int count = clicker.Tiers.Length;
        if (valueRows != null && valueRows.Length >= count) return;

        PackRow[] bigger = new PackRow[count];
        if (valueRows != null) System.Array.Copy(valueRows, bigger, valueRows.Length);
        for (int i = valueRows != null ? valueRows.Length : 0; i < count; i++)
        {
            PackRow row = BuildPlainRow(contentRect, "Value " + i);
            int captured = i;
            AddRowPicture(row, () => captured < clicker.Tiers.Length ? TierKey(clicker.Tiers[captured].type) : null);
            StyleValueRow(row);
            row.buyButton.onClick.AddListener(() => { TryBuyValueUpgrade(captured, PixelInput.ShiftHeld()); RefreshRows(); });
            // Hovering the Upgrade button explains clicking and Shift-clicking (the orange '!' box explains what a level does).
            System.Func<string> upgradeTip = () => valueUpgradeTip;
            PixelHoverTip buyTip = row.buyButton.gameObject.AddComponent<PixelHoverTip>();
            buyTip.onEnter = () => hoverTip = upgradeTip;
            buyTip.onExit = () => { if (hoverTip == upgradeTip) hoverTip = null; };
            bigger[i] = row;
        }
        valueRows = bigger;
    }

    /// <summary>
    /// The Value rows are boxed: next to the orange '!' button a box holds the pixel's name and a second box of the same height holds its level;
    /// a larger box underneath holds the cost lines, and the Upgrade button stays on the right.
    /// </summary>
    private void StyleValueRow(PackRow row) => StyleBoxedRow(row, buyButtonSize.x + 40f, true);

    /// <summary>
    /// The boxed row layout every shop row uses: the orange '!' button, a name box beside it (and a level box of the same height when 'hasLevel'),
    /// a larger cost box underneath, and the button(s) on the right ('rightInset' keeps the boxes clear of them). Call it after the picture was added.
    /// </summary>
    private void StyleBoxedRow(PackRow row, float rightInset, bool hasLevel)
    {
        if (row == null || row.boxed || row.nameLabel == null || row.costLabel == null) return;
        row.boxed = true;
        float s = Mathf.Min(52f, rowHeight * 0.22f);                       // the orange button's size
        Transform info = row.rect.Find("Info");
        float x0 = info != null ? info.GetComponent<RectTransform>().anchoredPosition.x : 20f;
        float topY = rowHeight * 0.4f * 0.5f - s * 0.5f;                   // the orange button's distance from the top
        float levelW = Mathf.Max(150f, s * 4f);
        float gap = 8f;

        RectTransform Box(string name, float left, float right, float top, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row.rect, false);
            go.transform.SetSiblingIndex(0);   // behind the labels
            Image im = go.GetComponent<Image>();
            im.color = new Color(0.07f, 0.07f, 0.1f, 1f);
            im.raycastTarget = false;
            PixelUIKit.StyleBox(im, true);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(left, -(top + height));
            r.offsetMax = new Vector2(-right, -top);
            return r;
        }

        float nameLeft = x0 + s + gap;
        RectTransform nameBox = Box("Name Box", nameLeft, rightInset + (hasLevel ? levelW + gap : 0f), topY, s);
        RectTransform levelBox = null;
        if (hasLevel)
        {
            levelBox = Box("Level Box", 0f, rightInset, topY, s);
            levelBox.anchorMin = new Vector2(1f, 1f);
            levelBox.anchorMax = new Vector2(1f, 1f);
            levelBox.pivot = new Vector2(1f, 1f);
            levelBox.sizeDelta = new Vector2(levelW, s);
            levelBox.anchoredPosition = new Vector2(-rightInset, -topY);
        }
        float costTop = topY + s + gap;
        RectTransform costBox = Box("Cost Box", x0, rightInset, costTop, Mathf.Max(30f, rowHeight - costTop - 14f));
        row.costBox = costBox;

        // Name inside its box.
        row.nameLabel.transform.SetParent(nameBox, false);
        RectTransform nr = row.nameLabel.rectTransform;
        nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
        nr.offsetMin = new Vector2(10f, 0f); nr.offsetMax = new Vector2(-10f, 0f);
        row.nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        row.nameLabel.enableAutoSizing = true;
        row.nameLabel.fontSizeMax = Mathf.Max(18f, s * 0.7f);
        row.nameLabel.fontSizeMin = 12f;
#if UNITY_2023_1_OR_NEWER
        row.nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
#else
        row.nameLabel.enableWordWrapping = false;
#endif

        // Level inside its own box.
        if (hasLevel) row.levelLabel = CreateText(levelBox, "Level", "", Mathf.Max(18f, s * 0.6f), TextAlignmentOptions.Center, FontStyles.Bold);
        if (hasLevel)
        {
            row.levelLabel.color = levelColor;
            row.levelLabel.enableAutoSizing = true;
            row.levelLabel.fontSizeMax = Mathf.Max(18f, s * 0.6f);
            row.levelLabel.fontSizeMin = 12f;
            RectTransform lr = row.levelLabel.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(8f, 0f); lr.offsetMax = new Vector2(-8f, 0f);
        }

        // Cost lines inside the big box.
        row.costLabel.transform.SetParent(costBox, false);
        RectTransform cr = row.costLabel.rectTransform;
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(12f, 6f); cr.offsetMax = new Vector2(-12f, -6f);
        row.costLabel.alignment = TextAlignmentOptions.MidlineLeft;
#if UNITY_2023_1_OR_NEWER
        row.costLabel.textWrappingMode = TextWrappingModes.NoWrap;   // lines are broken by the text itself; the size shrinks to fit instead
#else
        row.costLabel.enableWordWrapping = false;
#endif
    }

    /// <summary>Builds a scrolling list (view, content, scrollbar, empty message) below 'top' units from the panel's top edge.</summary>
    private void BuildScrollArea(Transform parent, float top, out RectTransform content, out ScrollRect scroll, out TMP_Text empty)
    {
        // Scroll view (also the viewport; the transparent image lets empty space take wheel/drag input).
        GameObject scrollGo = new GameObject("Scroll View", typeof(RectTransform), typeof(Image),
                                             typeof(RectMask2D), typeof(ScrollRect));
        scrollGo.transform.SetParent(parent, false);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        RectTransform sr = scrollGo.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.pivot = new Vector2(0.5f, 0.5f);
        sr.offsetMin = new Vector2(panelPadding, panelPadding);
        sr.offsetMax = new Vector2(-panelPadding, -top);   // full width, like the tabs and the Consumables cards: the scroll bar fades in over the right edge instead of reserving room there

        // Content
        content = PixelUIKit.CreateScrollContent(scrollGo.transform);

        // Scrollbar
        Scrollbar scrollbar = PixelUIKit.CreateScrollBar(parent, scrollbarTrackColor);
        GameObject barGo = scrollbar.gameObject;
        RectTransform br = barGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.offsetMin = new Vector2(-(panelPadding + PixelUIKit.ScrollBarWidth), panelPadding);
        br.offsetMax = new Vector2(-panelPadding, -top);

        // Scroll behaviour
        scroll = scrollGo.GetComponent<ScrollRect>();
        PixelUIKit.SetupVerticalScroll(scroll, content, scrollbar, scrollSpeed);
        HideBarGraphics(barGo);

        // "Nothing here yet" message for empty tabs.
        empty = CreateText(scrollGo.transform, "Empty", emptyTabText, descriptionFontSize + 8f,
                                TextAlignmentOptions.Center, FontStyles.Bold);
        empty.color = emptyTabColor;
        RectTransform er = empty.rectTransform;
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = er.offsetMax = Vector2.zero;
        empty.gameObject.SetActive(false);
    }

    /// <summary>Makes a scroll bar invisible (and unclickable) unless the shop is set to show them; the wheel still scrolls.</summary>
    private void HideBarGraphics(GameObject bar)
    {
        if (showScrollBars || bar == null) return;
        foreach (Image image in bar.GetComponentsInChildren<Image>(true)) image.enabled = false;
    }

    /// <summary>The second window: lists the upgrade packs that belong to the pack whose arrow was pressed.</summary>
    private void BuildUpgradesWindow(Transform parent)
    {
        // The standard Fixed window, the shop's size and centred like it; opaque: the shop behind must not show through.
        // It is still opened / closed with SetActive (PixelShopOperation opens it too): the builder's PixelPop grows it in when it opens
        // and fits it on screen like the shop; closing stays instant.
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, parent, "Upgrades Window", "", new Vector2(ClassicWidth, panelHeight), PixelUIKit.WindowKind.Fixed, CloseUpgradesWindow);
        subPanelObject = parts.Root;
        ShopButtonExtras(parts.Close);

        subTitle = parts.Title;   // (auto-sized: a long title like "Auto Clicker Upgrades" shrinks to ONE line between the two buttons)
        subTitle.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
        subTitle.textWrappingMode = TextWrappingModes.NoWrap;
#else
        subTitle.enableWordWrapping = false;
#endif

        // Back arrow in the standard left header slot (the X on the right also returns to the shop).
        Button back = parts.AddHeaderButton("Back", backText, true);
        ShopButtonExtras(back);
        PixelUIKit.UseArrowGlyph(back, true);   // a drawn backwards triangle instead of the "<" letter
        back.onClick.AddListener(CloseUpgradesWindow);

        BuildScrollArea(subPanelObject.transform, headerHeight + panelPadding * 0.5f,
                        out subContentRect, out subScrollRect, out subEmptyLabel);
        subEmptyLabel.text = emptyUpgradesText;
    }

    /// <summary>Opens the upgrades window for the pack at the given index.</summary>
    public void OpenUpgradesWindow(int parentIndex)
    {
        openParent = parentIndex;
        subContentRect.anchoredPosition = Vector2.zero;
        subScrollRect.StopMovement();
        subPanelObject.SetActive(true);
        RefreshRows();
    }

    public void CloseUpgradesWindow()
    {
        openParent = -1;
        openOperation = -1;
        subPanelObject.SetActive(false);
    }

    /// <summary>Upgrade packs (leveled packs that require another pack) are listed in that pack's upgrades window, not in a tab.</summary>
    private bool IsChild(int index)
    {
        ShopPack pack = packs[index];
        int req = pack.ParentIndex;
        return IsLeveled(pack) && req >= 0 && req < packs.Length && req != index;
    }

    /// <summary>
    /// Should this pack be left out of the list because it is bought and the player chose to hide bought items?
    /// A bought pack stays listed while it still has upgrades left to buy (its arrow opens them).
    /// </summary>
    private bool HiddenAsPurchased(int index)
    {
        if (packs[index].hideInShop) return true;   // testing packs: Dev Tools 'Shop unlocks' only
        if (!HidePurchased || !IsPurchased(index)) return false;
        for (int i = 0; i < packs.Length; i++)
            if (IsChild(i) && packs[i].ParentIndex == index && !IsPurchased(i)) return false;
        return true;
    }

    private bool HasChildren(int index)
    {
        for (int i = 0; i < packs.Length; i++)
            if (IsChild(i) && packs[i].ParentIndex == index) return true;
        return false;
    }

    /// <summary>Switches to a tab and scrolls back to the top.</summary>
    /// <summary>The shop window's rectangle (used to place tutorial boxes next to it).</summary>
    public RectTransform PanelRect => panelRect;
    private RectTransform helpButtonRect;
    /// <summary>The blue '?' hold button in the shop's header (plays the tour; framed gold when the player turns the first-time tour down).</summary>
    public RectTransform HelpButtonRect => helpButtonRect;

    /// <summary>The first-time question box over the shop window (PixelUIKit.AskBox in the shop's font and colours); the shop tour offer uses it.</summary>
    public GameObject AskBox(string title, string question, string yesText, string noText, System.Action onYes, System.Action onNo)
    {
        if (panelRect == null) return null;
        return PixelUIKit.AskBox(font, panelRect, title, new Color(1f, 0.82f, 0.25f, 1f), question, textColor, yesText, noText, onYes, onNo);
    }

    /// <summary>The Shop button (for the nudge that points the player at it).</summary>
    public RectTransform ShopButtonRect => shopButtonObject != null ? shopButtonObject.GetComponent<RectTransform>() : null;

    /// <summary>The shop window is open (and not closing).</summary>
    public bool WindowOpen => panelObject != null && PixelPop.IsOpen(panelObject);

    /// <summary>The shop window's size (width, height) from its settings, available before the window is built (the Crafting window copies it).</summary>
    public Vector2 PanelSize => new Vector2(ClassicWidth, panelHeight);

    /// <summary>The older, narrower shop width (classicWindowWidth) the Crafting window and the pack Upgrades window keep.</summary>
    private float ClassicWidth => Mathf.Clamp(classicWindowWidth, 600f, panelWidth);

    /// <summary>Shop tutorial: opens the shop on a tab (and, for Upgrades, a sub-tab: 0 = Features, 1 = Value).</summary>
    public void TutorialOpen(ShopTab tab, int subTab)
    {
        if (panelObject == null || !builtOk) return;
        PixelPop.Show(panelObject);
        upgradesSubTab = Mathf.Clamp(subTab, 0, 2);
        SelectRailEntry(Mathf.Max(0, EntryOf(tab == ShopTab.Automatic ? ShopTab.Pixels : tab, subTab)));
    }

    private TMP_InputField searchField;
    private string shopSearch = "";

    /// <summary>Does this pack match the search text (its name, description or reward pixel names)?</summary>
    private bool MatchesSearch(ShopPack pack)
    {
        if (shopSearch.Length == 0) return true;
        if ((pack.displayName ?? "").ToLowerInvariant().Contains(shopSearch)) return true;
        if ((ResolveDescription(pack) ?? "").ToLowerInvariant().Contains(shopSearch)) return true;
        if (pack.rewardTiers != null)
            foreach (PixelClicker.PixelTier t in pack.rewardTiers)
                if (t != null && (t.displayName ?? "").ToLowerInvariant().Contains(shopSearch)) return true;
        return false;
    }

    /// <summary>Does this text match the search (case-insensitive substring)?</summary>
    private bool TextMatches(string text) => shopSearch.Length == 0 || (text ?? "").ToLowerInvariant().Contains(shopSearch);

    /// <summary>The tab a pack is listed on (resolves 'Automatic').</summary>
    private ShopTab TabOf(ShopPack pack)
    {
        if (pack.tab != ShopTab.Automatic) return pack.tab;
        bool upgrade = IsLeveled(pack) || pack.unlocksAutoClicker || pack.upgradeEffect != UpgradeEffect.None;
        return upgrade ? ShopTab.Upgrades : ShopTab.Pixels;
    }

    private PackRow BuildRow(Transform parent, int index, bool potion = false)
    {
        string rowName = potion ? consumables.ItemName(index) : packs[index].displayName;
        string rowDescription = potion ? consumables.ItemDescription(index) : packs[index].description;
        PackRow row = new PackRow { isPotion = potion };

        RectTransform rr = CreateRowBox(parent, (potion ? "Potion " : "Pack ") + index, rowHeight);
        GameObject rowGo = rr.gameObject;
        rr.anchoredPosition = new Vector2(0f, -(index * (rowHeight + rowSpacing))); // re-laid out in RefreshRows
        row.rect = rr;

        bool hasChildren = !potion && HasChildren(index);
        float textRightInset = buyButtonSize.x + 40f; // keep text clear of the Buy button
        // (a pack's upgrade arrow now sits under the Buy button, so every row's boxes end at the same place)

        AddRowLabels(row, rowGo.transform, rowName, rowDescription, textRightInset);
        RectTransform br = AddRowBuyButton(row, rowGo.transform, buyText);

        LayoutRowHeader(row, textRightInset);
        if (!potion) AddPackPicture(row, index);
        StyleBoxedRow(row, textRightInset, !potion && IsLeveled(packs[index]));

        int captured = index;
        if (potion) row.buyButton.onClick.AddListener(() => TryBuyPotion(captured));
        else row.buyButton.onClick.AddListener(() => TryBuy(captured));

        if (hasChildren)
        {
            // Buy on top, the orange upgrades arrow right under it, both in the same column.
            Vector2 arrowSize = new Vector2(buyButtonSize.x, buyButtonSize.y * 0.8f);
            float dy = Mathf.Max(buyButtonSize.y, arrowSize.y) * 0.55f;
            row.arrowButton = CreateButton(rowGo.transform, "Upgrades Arrow", upgradesArrowText, arrowSize,
                                           upgradesArrowColor, textColor, buyFontSize, out _, out _);   // (role Custom: the orange arrow is its own colour, not a role)
            PixelUIKit.UseRightTriangle(row.arrowButton.gameObject, row.arrowButton.GetComponentInChildren<TMP_Text>(true), textColor, arrowSize);
            RectTransform ar = row.arrowButton.GetComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1f, 0.5f);
            ar.anchoredPosition = new Vector2(-20f, -dy);
            br.anchoredPosition = new Vector2(-20f, dy);
            row.arrowButton.onClick.AddListener(() => OpenUpgradesWindow(captured));
        }

        // (The old card-style layout - a wide Buy bar along the bottom - is not used any more: every row is boxed, see StyleBoxedRow.)
        return row;
    }

    /// <summary>A stat row with a title, a count and a progress bar (the Singularity and Ghost trackers).</summary>
    private class TrackerUI
    {
        public GameObject go;
        public RectTransform rect;
        public TMP_Text value;
        public RectTransform fill;
    }

    private readonly System.Collections.Generic.Dictionary<PixelMinigame, TrackerUI> trackers =
        new System.Collections.Generic.Dictionary<PixelMinigame, TrackerUI>();

    private TrackerUI BuildTracker(Transform parent, string objectName, string title, string description)
    {
        TrackerUI t = new TrackerUI();
        t.rect = CreateRowBox(parent, objectName, trackerRowHeight);
        t.go = t.rect.gameObject;

        TMP_Text titleLabel = CreateText(t.go.transform, "Title", title, nameFontSize,
                                         TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        SetBand(titleLabel.rectTransform, 0.62f, 1f, 340f);

        t.value = CreateText(t.go.transform, "Count", "", nameFontSize, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
        t.value.color = levelColor;
        t.value.enableAutoSizing = true;
        t.value.fontSizeMax = nameFontSize;
        t.value.fontSizeMin = 18f;
        RectTransform vr = t.value.rectTransform;
        vr.anchorMin = new Vector2(0.4f, 0.62f);
        vr.anchorMax = new Vector2(1f, 1f);
        vr.offsetMin = new Vector2(0f, 0f);
        vr.offsetMax = new Vector2(-20f, 0f);

        TMP_Text desc = CreateText(t.go.transform, "Description", description, descriptionFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        desc.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        desc.enableAutoSizing = true;
        desc.fontSizeMax = descriptionFontSize;
        desc.fontSizeMin = Mathf.Min(12f, descriptionFontSize);
        SetBand(desc.rectTransform, 0.34f, 0.62f, 20f);

        // Progress bar.
        GameObject back = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(t.go.transform, false);
        back.GetComponent<Image>().color = trackerBarBackColor;
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0.08f);
        br.anchorMax = new Vector2(1f, 0.28f);
        br.offsetMin = new Vector2(20f, 0f);
        br.offsetMax = new Vector2(-20f, 0f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(back.transform, false);
        fill.GetComponent<Image>().color = trackerBarFillColor;
        t.fill = fill.GetComponent<RectTransform>();
        t.fill.anchorMin = Vector2.zero;
        t.fill.anchorMax = new Vector2(0f, 1f);
        t.fill.offsetMin = t.fill.offsetMax = Vector2.zero;

        t.go.SetActive(false);
        return t;
    }

    /// <summary>Shows or hides a tracker row; when shown, places it at y and fills in its numbers.</summary>
    private void PlaceTracker(TrackerUI t, bool show, double count, double goal, ref float y, ref int visibleCount)
    {
        if (t == null) return;
        if (t.go.activeSelf != show) t.go.SetActive(show);
        if (!show) return;

        t.rect.anchoredPosition = new Vector2(0f, -y);
        y += trackerRowHeight + rowSpacing;
        visibleCount++;

        t.value.text = count >= goal
            ? string.Format(trackerReachedFormat, PixelClicker.FormatNumberShort(count))
            : string.Format(trackerFormat, PixelClicker.FormatNumberShort(count), PixelClicker.FormatNumberShort(goal));
        t.fill.anchorMax = new Vector2(goal > 0d ? Mathf.Clamp01((float)(count / goal)) : 1f, 1f);
    }

    /// <summary>Stretches a text across a horizontal band of its parent (anchors are 0..1 vertically).</summary>
    /// <summary>An orange box with an exclamation mark: hovering it shows 'source()' (an item's description) as a tooltip. The caller places it.</summary>
    private RectTransform CreateInfoBox(Transform parent, float size, System.Func<string> source) =>
        PixelUIKit.CreateInfoBadge(font, parent, size, () => hoverTip = source, () => { if (hoverTip == source) hoverTip = null; });   // (the shared orange '!')

    /// <summary>
    /// A row's name line with the info box in front of it (the description is a tooltip now): the name band is taller and the
    /// description label stays as hidden storage for the tooltip text.
    /// </summary>
    private void LayoutRowHeader(PackRow row, float rightInset)
    {
        float s = Mathf.Min(52f, rowHeight * 0.22f);
        SetBand(row.nameLabel.rectTransform, 0.6f, 1f, rightInset);
        row.nameLabel.rectTransform.offsetMin = new Vector2(20f + s + 12f, 0f);
        SetBand(row.costLabel.rectTransform, 0.05f, 0.56f, rightInset);
        row.descLabel.gameObject.SetActive(false);

        RectTransform box = CreateInfoBox(row.rect, s, () => row.descLabel != null ? row.descLabel.text : "");
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0f, 1f);
        box.anchoredPosition = new Vector2(20f, -(rowHeight * 0.4f * 0.5f - s * 0.5f));
    }

    private void SetBand(RectTransform rt, float yMin, float yMax, float rightInset)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(20f, 0f);
        rt.offsetMax = new Vector2(-rightInset, 0f);
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float size,
                                TextAlignmentOptions alignment, FontStyles style)
        => PixelUIKit.CreateText(font, parent, objectName, text, size, alignment, style, textColor);

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, Color color,
                                Color labelColor, float labelSize, out TMP_Text labelText, out Image image)
    {
        // The shared button (look, click sound, custom button colour, close cross) plus the shop's extras.
        Button button = PixelUIKit.CreateButton(font, parent, objectName, label, size, color, labelColor, labelSize);
        image = button.GetComponent<Image>();
        labelText = button.GetComponentInChildren<TMP_Text>(true);
        ShopButtonExtras(button);
        return button;
    }

    /// <summary>The same shop button in a ROLE (PixelUITheme): Neutral, Primary, Selected, Success, Danger.</summary>
    private Button CreateButton(Transform parent, string objectName, string label, Vector2 size, ButtonRole role,
                                Color labelColor, float labelSize, out TMP_Text labelText, out Image image)
    {
        Button button = CreateButton(parent, objectName, label, size, PixelUITheme.ColorOf(role), labelColor, labelSize, out labelText, out image);
        PixelUIKit.SetRole(image, role);
        shopRoles[image] = role;
        return button;
    }

    // The role each shop button was last given, so the row refresh only calls SetRole (a search of every button) when a role changes.
    private readonly System.Collections.Generic.Dictionary<Image, ButtonRole> shopRoles = new System.Collections.Generic.Dictionary<Image, ButtonRole>();

    /// <summary>Switches a shop button to a role (tab Selected / Neutral, Buy Success / Neutral), only when it changed.</summary>
    private void SetShopRole(Image image, ButtonRole role)
    {
        if (image == null || (shopRoles.TryGetValue(image, out ButtonRole now) && now == role)) return;
        shopRoles[image] = role;
        PixelUIKit.SetRole(image, role);
    }

    /// <summary>The shop's extras on a button made elsewhere (the window builder's X and header buttons): the press pulse and the fancy menu shine.</summary>
    private static void ShopButtonExtras(Button button)
    {
        if (button == null) return;
        button.gameObject.AddComponent<PixelButtonPulse>();   // a neon rim in the button's colour comes out from under it on every press
        PixelMenuFx.DecorateAll(button.transform);   // the fancy menu's brighten / shine / press (this button only)
    }

    // ------------------------------------------------------------------
    // Refreshing
    // ------------------------------------------------------------------

    private GameObject billButton;

    private void RefreshRows()
    {
        if (billButton != null && billButton.activeSelf != PixelCubieBill.IsRevealed) billButton.SetActive(PixelCubieBill.IsRevealed);
        float y = 0f;      // main list
        float subY = 0f;   // upgrades window list
        int visibleCount = 0, subVisibleCount = 0;

        bool searching = shopSearch.Length > 0;

        // The upgrades window only makes sense while its pack is owned.
        if (openParent >= 0 && !HasPack(openParent)) CloseUpgradesWindow();
        if (openParent >= 0) PixelUIKit.SetText(subTitle, string.Format(upgradesWindowTitle, packs[openParent].displayName));

        // Trackers: first things on the Minigames tab, once their minigame is in play.
        foreach (var pair in trackers)
        {
            PixelMinigame m = pair.Key;
            bool show = !searching && showMinigameTrackersInShop && currentTab == ShopTab.Minigames && m != null &&
                        (m.Running || m.TrackerCount > 0d);
            PlaceTracker(pair.Value, show, m != null ? m.TrackerCount : 0d, m != null ? m.TrackerGoal : 1d,
                         ref y, ref visibleCount);
        }

        // Upgrades tab: the "Upgrades | Pixel" sub-tab buttons come first.
        bool onUpgrades = !searching && currentTab == ShopTab.Upgrades;
        if (subTabRow != null)
        {
            if (subTabRow.gameObject.activeSelf != onUpgrades) subTabRow.gameObject.SetActive(onUpgrades);
            if (onUpgrades)
            {
                subTabRow.anchoredPosition = new Vector2(0f, -y);
                y += subTabHeight + rowSpacing;
                visibleCount++;
                for (int s = 0; s < subTabImages.Length; s++)
                    SetShopRole(subTabImages[s], s == upgradesSubTab ? ButtonRole.Selected : ButtonRole.Neutral);
            }
        }
        bool showGeneral = !onUpgrades || upgradesSubTab == 0;

        RefreshPackRows(searching, showGeneral, ref y, ref visibleCount, ref subY, ref subVisibleCount);

        // Pixels and Consumables tabs: their tile pages replace the scrolling list.
        RefreshTilePages(searching);

        // Upgrades > Value: one row per unlocked pixel type, paid for with that pixel's own currency.
        RefreshValueRows(onUpgrades, ref y, ref visibleCount);

        // Upgrades > Operation: the consumables and their upgrade window.
        RefreshOperation(onUpgrades, ref y, ref visibleCount, ref subY, ref subVisibleCount);


        // The scroll content is as tall as the visible rows; the panel itself stays a fixed size.
        ApplyContentHeight(contentRect, emptyLabel, y, visibleCount);
        ApplyContentHeight(subContentRect, subEmptyLabel, subY, subVisibleCount);
    }

    /// <summary>RefreshRows step: the pack rows of the open tab (and of the upgrades window), stacked from the top of their list.</summary>
    private void RefreshPackRows(bool searching, bool showGeneral, ref float y, ref int visibleCount, ref float subY, ref int subVisibleCount)
    {
        // (the Pixels tab is a tile page, PixelShopPixels.cs: its packs' rows only show while searching)
        int[] packOrder = new int[Mathf.Min(packs.Length, rows.Length)];
        for (int o = 0; o < packOrder.Length; o++) packOrder[o] = o;
        if (!searching && currentTab == ShopTab.Upgrades)
        {
            // Upgrades tab: the Pixel Bank is listed right above Time Stop (only the shown order; the pack list itself keeps its indexes, which requirements point at).
            int timeStop = System.Array.FindIndex(packs, p => p != null && p.unlocksTimeStop);
            float Key(int i) => timeStop >= 0 && packs[i] != null && packs[i].unlocksBank ? timeStop - 0.5f : i;
            System.Array.Sort(packOrder, (a, b) => Key(a) != Key(b) ? Key(a).CompareTo(Key(b)) : a.CompareTo(b));
        }

        for (int oi = 0; oi < packOrder.Length; oi++)
        {
            int i = packOrder[oi];
            PackRow row = rows[i];
            ShopPack pack = packs[i];
            bool leveled = IsLeveled(pack);
            bool child = IsChild(i);

            bool owned = IsPurchased(i); // one-time: bought. upgrade: max level.
            bool requirementMet = IsRequirementMet(i);
            bool listed = IsPackRequirementMet(i) || showLockedPacks || HasPack(i);
            bool visible = child
                ? openParent >= 0 && pack.ParentIndex == openParent && listed && !HiddenAsPurchased(i)
                : !searching && TabOf(pack) == currentTab && showGeneral && listed && !HiddenAsPurchased(i);   // (search results are tiles: PixelShopSearch.cs)

            if (visible && !searching && !child && currentTab == ShopTab.Pixels) visible = false;   // shown as tiles instead

            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            // Stack visible rows from the top of their list.
            if (child)
            {
                row.rect.anchoredPosition = new Vector2(0f, -subY);
                subY += rowHeight + rowSpacing;
                subVisibleCount++;
            }
            else
            {
                row.rect.anchoredPosition = new Vector2(0f, -y);
                y += rowHeight + rowSpacing;
                visibleCount++;
            }

            PixelUIKit.SetText(row.nameLabel, BuildNameText(pack));
            if (IsLeveled(pack)) ApplyLevelWidgets(row, pack.level, pack.levels.Length);
            PixelUIKit.SetText(row.descLabel, ResolveDescription(pack));

            bool canBuy = !owned && requirementMet && CanAfford(i);

            if (owned) PixelUIKit.SetText(row.costLabel, "");
            else if (!requirementMet) PixelUIKit.SetText(row.costLabel, RequirementText(i));
            else PixelUIKit.SetText(row.costLabel, BuildCostText(CurrentCosts(pack)));

            row.buyButton.interactable = canBuy;
            if (owned) PixelUIKit.SetText(row.buyLabel, leveled ? maxedText : ownedText);
            else if (!requirementMet) PixelUIKit.SetText(row.buyLabel, lockedText);
            else PixelUIKit.SetText(row.buyLabel, leveled ? upgradeText : buyText);
            SetShopRole(row.buyImage, canBuy ? ButtonRole.Success : ButtonRole.Neutral);

            // Arrow to the upgrades window: only once the pack is owned.
            if (row.arrowButton != null)
            {
                bool showArrow = HasPack(i);
                if (row.arrowButton.gameObject.activeSelf != showArrow) row.arrowButton.gameObject.SetActive(showArrow);
            }
        }
    }

    /// <summary>RefreshRows step: the Upgrades > Value rows (one per unlocked pixel type, paid for with that pixel's own currency).</summary>
    private void RefreshValueRows(bool onUpgrades, ref float y, ref int visibleCount)
    {
        EnsureValueRows();
        for (int t = 0; t < valueRows.Length && t < clicker.Tiers.Length; t++)
        {
            PackRow row = valueRows[t];
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            bool visible = onUpgrades && upgradesSubTab == 1 && tier.unlocked && !PixelClicker.IsHazard(tier.type) && !(HidePurchased && ValueUpgradeMaxed(t));
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight + rowSpacing;
            visibleCount++;

            bool maxed = ValueUpgradeMaxed(t);
            string capSkill = ""; int capLevel = 0;
            bool capped = !maxed && ValueSkillCapped(t, out capSkill, out capLevel);   // the owning skill's level holds the next one back
            double cost = ValueUpgradeCost(t);
            double now = clicker.ValueMultiplierAt(tier.valueLevel);
            double next = clicker.ValueMultiplierAt(tier.valueLevel + 1);

            string shortName = tier.displayName ?? "";
            if (shortName.EndsWith(" Pixels")) shortName = shortName.Substring(0, shortName.Length - 7);
            else if (shortName.EndsWith(" Pixel")) shortName = shortName.Substring(0, shortName.Length - 6);
            row.nameLabel.text = shortName;   // just the pixel's name ("White"), the row is on the Value tab already
            if (row.levelLabel != null) row.levelLabel.text = string.Format(ultraLevelFormat, tier.valueLevel);
            row.descLabel.text = valueInfoText;

            bool enough = PixelClicker.InfiniteResources || tier.count >= cost;
            row.costLabel.text = maxed ? "" : capped ? "<color=#" + ColorUtility.ToHtmlStringRGB(unaffordableColor) + ">" + string.Format(valueCappedFormat, capSkill, capLevel) + "</color>" : "<color=#" + ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor) + ">" +
                                 string.Format(NoHave(valueCostFormat), PixelClicker.FormatNumberShort(cost), tier.displayName, PixelClicker.FormatNumberShort(tier.count)) + "</color>";

            bool canBuy = !maxed && !capped && enough;
            row.buyButton.interactable = canBuy;
            row.buyLabel.text = maxed ? maxedText : capped ? valueCappedButtonText : valueButtonText;
            SetShopRole(row.buyImage, canBuy ? ButtonRole.Success : ButtonRole.Neutral);
        }
    }

    /// <summary>Which Pixels-tab section a pack belongs to: 0 = the RGB pack (listed above the sections, hidden once bought), 1 = single toggleable pixels (Glass, Luminescent, Obsidian...), 2 = skill packs (many pixels at once).</summary>
    /// <summary>The Lumberjack's Pack (the first skill pack): listed in the Skill packs section, first among them.</summary>
    private static bool IsWoodcutterPack(ShopPack pack) => pack != null && Rewards(pack, PixelClicker.PixelType.Wood);

    private static int SectionOf(ShopPack pack)
    {
        if (IsWoodcutterPack(pack)) return 2;   // (2026-10-16: with the other skill packs - sorted first among them)
        if (!string.IsNullOrEmpty(pack.skillId)) return 2;   // a skill pack, even without reward pixels of its own
        if (pack.rewardTiers == null || pack.rewardTiers.Length == 0) return 1;
        if (pack.rewardTiers.Length >= 4) return 2;
        foreach (PixelClicker.PixelTier t in pack.rewardTiers)
            if (t.type != PixelClicker.PixelType.Red && t.type != PixelClicker.PixelType.Green && t.type != PixelClicker.PixelType.Blue) return 1;
        return 0;
    }

    private void ApplyContentHeight(RectTransform content, TMP_Text empty, float y, int count)
    {
        float contentHeight = count > 0 ? y - rowSpacing : 0f;
        if (!Mathf.Approximately(content.sizeDelta.y, contentHeight))
            content.sizeDelta = new Vector2(0f, contentHeight);

        if (empty != null && empty.gameObject.activeSelf != (count == 0))
            empty.gameObject.SetActive(count == 0);
    }

    /// <summary>Pack name, plus "Level 2/5" for upgrade packs.</summary>
    private string BuildNameText(ShopPack pack)
    {
        return pack.displayName;   // the level shows on its own line + as level boxes (ApplyLevelWidgets)
    }

    /// <summary>
    /// An upgrade row's level: "Level 2/5" on its own single line under the name, and one small box per level beside it
    /// (dark = not bought yet, green = bought), so the progress reads at a glance.
    /// </summary>
    private void ApplyLevelWidgets(PackRow row, int level, int max)
    {
        float w = max > 10 ? 10f : 18f, h = 34f, gap = 6f;
        if (row.boxed && row.levelLabel != null && row.markerRoot == null && row.costBox != null)
        {
            // Boxed rows: "LEVEL 2/5" is in the level box; the little level boxes sit at the right end of the cost box.
            float markersWidth = max * (w + gap);
            GameObject rootGo = new GameObject("Level Markers", typeof(RectTransform));
            rootGo.transform.SetParent(row.costBox, false);
            row.markerRoot = rootGo.GetComponent<RectTransform>();
            row.markerRoot.anchorMin = row.markerRoot.anchorMax = new Vector2(1f, 0.5f);
            row.markerRoot.pivot = new Vector2(1f, 0.5f);
            row.markerRoot.anchoredPosition = new Vector2(-12f, 0f);
            row.markerRoot.sizeDelta = new Vector2(markersWidth, h);
            row.costLabel.rectTransform.offsetMax = new Vector2(-(markersWidth + 24f), -6f);
        }
        if (row.levelLabel == null)
        {
            float markersWidth = max * (w + gap);
            const float labelWidth = 210f, side = 14f;
            row.levelLabel = CreateText(row.rect, "Level", "", costFontSize, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            row.levelLabel.color = levelColor;
            row.levelLabel.raycastTarget = false;
            row.levelLabel.enableAutoSizing = true;
            row.levelLabel.fontSizeMax = costFontSize;
            row.levelLabel.fontSizeMin = 10f;
            row.levelLabel.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
            row.levelLabel.textWrappingMode = TextWrappingModes.NoWrap;
#else
            row.levelLabel.enableWordWrapping = false;
#endif
            // On the name's line, right-aligned: "LEVEL 2/5" and the little level boxes at the right edge.
            RectTransform lr = row.levelLabel.rectTransform;
            lr.anchorMin = new Vector2(1f, 0.62f); lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(1f, 0.5f);
            lr.sizeDelta = new Vector2(labelWidth, 0f);
            lr.anchoredPosition = new Vector2(-(side + markersWidth + 10f), 0f);

            GameObject rootGo = new GameObject("Level Markers", typeof(RectTransform));
            rootGo.transform.SetParent(row.rect, false);
            row.markerRoot = rootGo.GetComponent<RectTransform>();
            row.markerRoot.anchorMin = new Vector2(1f, 0.62f); row.markerRoot.anchorMax = new Vector2(1f, 1f);
            row.markerRoot.pivot = new Vector2(1f, 0.5f);
            row.markerRoot.anchoredPosition = new Vector2(-side, 0f);
            row.markerRoot.sizeDelta = new Vector2(markersWidth, 0f);

            // The name keeps clear of them.
            row.nameLabel.rectTransform.offsetMax = new Vector2(-(side + markersWidth + labelWidth + 20f), 0f);
        }

        bool maxed = level >= max;
        PixelUIKit.SetText(row.levelLabel, string.Format(maxed ? maxedLevelFormat : levelFormat, level, max));

        while (row.markers.Count < max)
        {
            GameObject box = new GameObject("Level " + row.markers.Count, typeof(RectTransform), typeof(Image));
            box.transform.SetParent(row.markerRoot, false);
            Image img = box.GetComponent<Image>();
            img.raycastTarget = false;
            PixelUIKit.StyleBox(img);
            RectTransform br = box.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(0f, 0.5f);
            br.pivot = new Vector2(0f, 0.5f);
            br.sizeDelta = new Vector2(w, h);
            br.anchoredPosition = new Vector2(row.markers.Count * (w + gap), 0f);
            row.markers.Add(img);
        }
        for (int i = 0; i < row.markers.Count; i++)
        {
            bool shown = i < max;
            if (row.markers[i].gameObject.activeSelf != shown) row.markers[i].gameObject.SetActive(shown);
            row.markers[i].color = i < level ? new Color(0.28f, 0.82f, 0.4f, 1f) : new Color(0.1f, 0.1f, 0.14f, 1f);
        }
    }

    private string ResolveDescription(ShopPack pack)
    {
        string text = pack.description ?? "";
        if (pack.rewardTiers != null && pack.rewardTiers.Length > 0)
            text = text.Replace("{clicks}", pack.rewardTiers[0].clicksToCollect.ToString());
        if (autoClicker != null) text = text.Replace("{interval}", autoClicker.Interval.ToString("0.##"));

        if (IsLeveled(pack))
        {
            float current = 0f;
            if (pack.upgradeEffect == UpgradeEffect.ComboMeter)
                current = pack.level > 0 ? pack.levels[pack.level - 1].value : 1f;
            else if (pack.upgradeEffect == UpgradeEffect.BankCapacity)
                current = bank != null ? bank.Capacity : 0f;
            else if (autoClicker != null)
                current = pack.upgradeEffect == UpgradeEffect.AutoClickerClicks ? autoClicker.ClicksPerTick : autoClicker.Interval;

            float next = pack.level < pack.levels.Length ? pack.levels[Mathf.Max(0, pack.level)].value : current;
            text = text.Replace("{level}", pack.level.ToString())
                       .Replace("{max}", pack.levels.Length.ToString())
                       .Replace("{current}", current.ToString("0.##"))
                       .Replace("{next}", next.ToString("0.##"));
        }
        return text;
    }

    private string RequirementName(ShopPack pack)
    {
        int req = pack.ParentIndex;
        return req >= 0 && req < packs.Length ? packs[req].displayName : "?";
    }

    /// <summary>"Cost: 100 White Pixels  100 Gray Pixels ..." with each part green/red by affordability.</summary>
    private string BuildCostText(PackCost[] costs, bool capsPrefix = false)
    {
        string prefix = capsPrefix ? costPrefix.ToUpperInvariant() : costPrefix;
        if (costs == null || costs.Length == 0) return prefix + "Free";

        // Pack rows: a tidy grid, three prices to a line in fixed columns ("1K White"), instead of one long ragged sentence.
        // Two or three prices go on one plain line (autosize shrinks it to fit): fixed columns overlapped as soon as a name was long ("150K Obsidian").
        bool grid = !capsPrefix && costs.Length > 3;
        StringBuilder sb = new StringBuilder(prefix);
        for (int i = 0; i < costs.Length; i++)
        {
            PackCost cost = costs[i];
            string name = CostName(cost);
            bool enough = CanAffordCost(cost);
            if (grid)
            {
                if (name.EndsWith(" Pixels")) name = name.Substring(0, name.Length - 7);
                else if (name.EndsWith(" Pixel")) name = name.Substring(0, name.Length - 6);
                int column = i % 3;
                if (i > 0 && column == 0) sb.Append('\n');
                sb.Append("<pos=").Append(column == 0 ? 17 : column == 1 ? 45 : 73).Append("%><color=#")
                  .Append(ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor))
                  .Append('>').Append(PixelClicker.FormatNumberShort(cost.amount)).Append(' ').Append(name).Append("</color>");
                continue;
            }

            if (!capsPrefix) { if (name.EndsWith(" Pixels")) name = name.Substring(0, name.Length - 7); else if (name.EndsWith(" Pixel")) name = name.Substring(0, name.Length - 6); }
            string part = string.Format(costEntryFormat, PixelClicker.FormatNumberShort(cost.amount), name);
            sb.Append("<color=#")
              .Append(ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor))
              .Append('>').Append(part).Append("</color>");

            if (i < costs.Length - 1) sb.Append("   ");
        }
        return sb.ToString();
    }
}
