using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The shop's user interface (button, panel, tabs, scrolling list, upgrades window, trackers, refreshing).
/// Part of <see cref="PixelShop"/> - the buying logic and all Inspector settings are in PixelShop.cs.
/// </summary>
public partial class PixelShop
{
    // ------------------------------------------------------------------
    // Runtime
    // ------------------------------------------------------------------

    private class PackRow
    {
        public RectTransform rect;
        public TMP_Text nameLabel;
        public TMP_Text descLabel;
        public Button buyButton;
        public Image buyImage;
        public TMP_Text buyLabel;
        public TMP_Text costLabel;
        public Button arrowButton;
        public bool isPotion;
    }

    private RectTransform panelRect;
    private RectTransform contentRect;
    private ScrollRect scrollRect;
    private TMP_Text emptyLabel;
    private Image[] tabImages;
    private ShopTab currentTab = ShopTab.Pixels;
    private GameObject subPanelObject;
    private RectTransform subContentRect;
    private ScrollRect subScrollRect;
    private TMP_Text subEmptyLabel;
    private TMP_Text subTitle;
    private int openParent = -1;
    private PackRow[] ultraRows;           // one boost row per pixel type (Upgrades > Pixel sub-tab)
    private RectTransform subTabRow;       // the "Upgrades | Pixel" buttons at the top of the Upgrades tab
    private Image[] subTabImages;
    private int upgradesSubTab;            // 0 = normal upgrades, 1 = Pixel boosts
    private GameObject canvasRoot;
    private GameObject shopButtonObject;
    private GameObject panelObject;
    private PackRow[] rows;
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

        Button button = CreateButton(parent, "Shop Button", buttonText, buttonSize, buttonColor,
                                     buttonTextColor, buttonFontSize, out _, out _);
        shopButtonObject = button.gameObject;

        RectTransform rt = shopButtonObject.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = new Vector2(anchor.x > 0.5f ? -buttonMargin.x : buttonMargin.x,
                                          anchor.y > 0.5f ? -buttonMargin.y : buttonMargin.y);
        PixelHud.Ensure(gameObject).Dock(rt, anchor, () => panelObject != null && panelObject.activeSelf);

        button.onClick.AddListener(() =>
        {
            panelObject.SetActive(!panelObject.activeSelf);
            if (panelObject.activeSelf) RefreshRows();
        });
        shopButtonObject.SetActive(false); // Update() reveals it once the required tier is unlocked.
    }

    private void BuildPanel(Transform parent)
    {
        currentTab = startTab == ShopTab.Automatic ? ShopTab.Pixels : startTab;

        panelObject = new GameObject("Shop Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        panelRect.anchoredPosition = Vector2.zero;
        panelObject.GetComponent<Image>().color = panelColor;

        // Title
        TMP_Text title = CreateText(panelObject.transform, "Title", panelTitle, titleFontSize,
                                    TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-(panelPadding * 2f + 160f), headerHeight);
        tr.anchoredPosition = Vector2.zero;

        // Close button
        Button close = CreateButton(panelObject.transform, "Close", "X", new Vector2(80f, 80f),
                                    disabledColor, textColor, 40f, out _, out _);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
        close.onClick.AddListener(() => panelObject.SetActive(false));

        BuildTabs(panelObject.transform);
        BuildScrollArea(panelObject.transform, headerHeight + tabHeight + panelPadding * 0.5f,
                        out contentRect, out scrollRect, out emptyLabel);
        BuildUpgradesWindow(parent);

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

        // The Consumables tab is two purchase cards (potions / utilities), not part of the scrolling list.
        BuildConsumablesArea(panelObject.transform, headerHeight + tabHeight + panelPadding * 0.5f);

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

        string[] names = { upgradesSubTabText, pixelSubTabText };
        subTabImages = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            Button b = CreateButton(go.transform, "Sub Tab " + names[i], names[i], Vector2.zero, tabInactiveColor, textColor,
                                    tabFontSize, out TMP_Text subLabel, out subTabImages[i]);
            subLabel.enableAutoSizing = true;
            subLabel.fontSizeMax = tabFontSize;
            subLabel.fontSizeMin = Mathf.Min(14f, tabFontSize);
            subLabel.rectTransform.offsetMin = new Vector2(tabTextPadding, 4f);
            subLabel.rectTransform.offsetMax = new Vector2(-tabTextPadding, -4f);
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(i * 0.5f, 0f);
            rt.anchorMax = new Vector2((i + 1) * 0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(i == 0 ? 0f : tabSpacing * 0.5f, 0f);
            rt.offsetMax = new Vector2(i == 1 ? 0f : -tabSpacing * 0.5f, 0f);

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
        GameObject rowGo = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        rowGo.transform.SetParent(parent, false);
        rowGo.GetComponent<Image>().color = rowColor;
        RectTransform rr = rowGo.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, rowHeight);
        row.rect = rr;

        float inset = buyButtonSize.x + 40f;

        row.nameLabel = CreateText(rowGo.transform, "Name", "", nameFontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        row.nameLabel.richText = true;
        row.nameLabel.enableAutoSizing = true;
        row.nameLabel.fontSizeMax = nameFontSize;
        row.nameLabel.fontSizeMin = Mathf.Min(16f, nameFontSize);
        SetBand(row.nameLabel.rectTransform, 0.74f, 1f, inset);

        row.descLabel = CreateText(rowGo.transform, "Description", "", descriptionFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        row.descLabel.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        row.descLabel.enableAutoSizing = true;
        row.descLabel.fontSizeMax = descriptionFontSize;
        row.descLabel.fontSizeMin = Mathf.Min(12f, descriptionFontSize);
        SetBand(row.descLabel.rectTransform, 0.38f, 0.74f, inset);

        row.costLabel = CreateText(rowGo.transform, "Cost", "", costFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        row.costLabel.richText = true;
        row.costLabel.enableAutoSizing = true;
        row.costLabel.fontSizeMax = costFontSize;
        row.costLabel.fontSizeMin = Mathf.Min(14f, costFontSize);
        SetBand(row.costLabel.rectTransform, 0.04f, 0.36f, inset);

        row.buyButton = CreateButton(rowGo.transform, "Buy", ultraButtonText, buyButtonSize, buyColor, textColor, buyFontSize,
                                     out row.buyLabel, out row.buyImage);
        RectTransform br = row.buyButton.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.anchoredPosition = new Vector2(-20f, 0f);
        return row;
    }

    /// <summary>Makes sure there is a boost row for every pixel type (the shop can add pixel types after the UI was built).</summary>
    private void EnsureUltraRows()
    {
        int count = clicker.Tiers.Length;
        if (ultraRows != null && ultraRows.Length >= count) return;

        PackRow[] bigger = new PackRow[count];
        if (ultraRows != null) System.Array.Copy(ultraRows, bigger, ultraRows.Length);
        for (int i = ultraRows != null ? ultraRows.Length : 0; i < count; i++)
        {
            PackRow row = BuildPlainRow(contentRect, "Boost " + i);
            int captured = i;
            row.buyButton.onClick.AddListener(() => { TryBuyUltraBoost(captured); RefreshRows(); });
            bigger[i] = row;
        }
        ultraRows = bigger;
    }

    private void BuildTabs(Transform parent)
    {
        string[] names = { pixelsTabText, upgradesTabText, consumablesTabText, minigamesTabText };
        ShopTab[] tabs = { ShopTab.Pixels, ShopTab.Upgrades, ShopTab.Consumables, ShopTab.Minigames };
        tabImages = new Image[tabs.Length];

        GameObject bar = new GameObject("Tabs", typeof(RectTransform));
        bar.transform.SetParent(parent, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(-panelPadding * 2f, tabHeight);
        barRect.anchoredPosition = new Vector2(0f, -headerHeight);

        float slice = 1f / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            Button tabButton = CreateButton(bar.transform, "Tab " + names[i], names[i], Vector2.zero,
                                            tabInactiveColor, textColor, tabFontSize, out TMP_Text tabLabel, out tabImages[i]);
            // Four tabs share the width, so long names shrink to fit instead of overflowing.
            tabLabel.enableAutoSizing = true;
            tabLabel.fontSizeMax = tabFontSize;
            tabLabel.fontSizeMin = Mathf.Min(14f, tabFontSize);
            tabLabel.rectTransform.offsetMin = new Vector2(tabTextPadding, 4f);   // keep the text off the button's edges
            tabLabel.rectTransform.offsetMax = new Vector2(-tabTextPadding, -4f);
            RectTransform rt = tabButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(i * slice, 0f);
            rt.anchorMax = new Vector2((i + 1) * slice, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(i == 0 ? 0f : tabSpacing * 0.5f, 0f);
            rt.offsetMax = new Vector2(i == tabs.Length - 1 ? 0f : -tabSpacing * 0.5f, 0f);

            ShopTab captured = tabs[i];
            tabButton.onClick.AddListener(() => SelectTab(captured));
        }
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
        sr.offsetMax = new Vector2(-(panelPadding + scrollbarWidth + 8f), -top);

        // Content
        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(scrollGo.transform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        // Scrollbar
        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        barGo.transform.SetParent(parent, false);
        barGo.GetComponent<Image>().color = scrollbarTrackColor;
        RectTransform br = barGo.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(1f, 0f);
        br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(1f, 0.5f);
        br.offsetMin = new Vector2(-(panelPadding + scrollbarWidth), panelPadding);
        br.offsetMax = new Vector2(-panelPadding, -top);

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(barGo.transform, false);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = scrollbarHandleColor;
        RectTransform hr = handleGo.GetComponent<RectTransform>();
        hr.offsetMin = hr.offsetMax = Vector2.zero;

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = hr;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        // Scroll behaviour
        scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.gameObject.AddComponent<PixelScrollSound>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = scrollSpeed;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // "Nothing here yet" message for empty tabs.
        empty = CreateText(scrollGo.transform, "Empty", emptyTabText, descriptionFontSize + 6f,
                                TextAlignmentOptions.Center, FontStyles.Italic);
        empty.color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
        RectTransform er = empty.rectTransform;
        er.anchorMin = Vector2.zero;
        er.anchorMax = Vector2.one;
        er.offsetMin = er.offsetMax = Vector2.zero;
        empty.gameObject.SetActive(false);
    }

    /// <summary>The second window: lists the upgrade packs that belong to the pack whose arrow was pressed.</summary>
    private void BuildUpgradesWindow(Transform parent)
    {
        subPanelObject = new GameObject("Upgrades Window", typeof(RectTransform), typeof(Image));
        subPanelObject.transform.SetParent(parent, false);
        RectTransform pr = subPanelObject.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(panelWidth, panelHeight);
        pr.anchoredPosition = Vector2.zero;
        subPanelObject.GetComponent<Image>().color = panelColor;

        subTitle = CreateText(subPanelObject.transform, "Title", "", titleFontSize,
                              TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform tr = subTitle.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-(panelPadding * 2f + 200f), headerHeight);
        tr.anchoredPosition = Vector2.zero;

        // Back arrow (top-left) and close X (top-right) both return to the shop.
        Button back = CreateButton(subPanelObject.transform, "Back", backText, new Vector2(80f, 80f),
                                   disabledColor, textColor, 40f, out _, out _);
        RectTransform br = back.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
        br.anchoredPosition = new Vector2(panelPadding, -panelPadding * 0.5f);
        back.onClick.AddListener(CloseUpgradesWindow);

        Button close = CreateButton(subPanelObject.transform, "Close", "X", new Vector2(80f, 80f),
                                    disabledColor, textColor, 40f, out _, out _);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-panelPadding, -panelPadding * 0.5f);
        close.onClick.AddListener(CloseUpgradesWindow);

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
    public void SelectTab(ShopTab tab)
    {
        if (tab == ShopTab.Automatic) tab = ShopTab.Pixels;
        currentTab = tab;
        if (contentRect != null) contentRect.anchoredPosition = Vector2.zero;
        if (scrollRect != null) scrollRect.StopMovement();
        if (rows != null) RefreshRows();
    }

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

        GameObject rowGo = new GameObject((potion ? "Potion " : "Pack ") + index, typeof(RectTransform), typeof(Image));
        rowGo.transform.SetParent(parent, false);
        rowGo.GetComponent<Image>().color = rowColor;

        RectTransform rr = rowGo.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(0f, rowHeight);
        rr.anchoredPosition = new Vector2(0f, -(index * (rowHeight + rowSpacing))); // re-laid out in RefreshRows
        row.rect = rr;

        bool hasChildren = !potion && HasChildren(index);
        float textRightInset = buyButtonSize.x + 40f; // keep text clear of the Buy button
        if (hasChildren) textRightInset += upgradesArrowSize.x + 10f;

        TMP_Text name = CreateText(rowGo.transform, "Name", rowName, nameFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        name.richText = true;
        row.nameLabel = name;
        // Long names / descriptions shrink to fit their band instead of spilling over the lines below.
        name.enableAutoSizing = true;
        name.fontSizeMax = nameFontSize;
        name.fontSizeMin = Mathf.Min(16f, nameFontSize);
        SetBand(name.rectTransform, 0.74f, 1f, textRightInset);

        TMP_Text desc = CreateText(rowGo.transform, "Description", rowDescription, descriptionFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        desc.color = new Color(textColor.r, textColor.g, textColor.b, 0.75f);
        desc.enableAutoSizing = true;
        desc.fontSizeMax = descriptionFontSize;
        desc.fontSizeMin = Mathf.Min(12f, descriptionFontSize);
        desc.alignment = TextAlignmentOptions.TopLeft;
        SetBand(desc.rectTransform, 0.38f, 0.74f, textRightInset);
        row.descLabel = desc;

        row.costLabel = CreateText(rowGo.transform, "Cost", "", costFontSize,
                                   TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
        row.costLabel.richText = true;
        // Long cost lists (six currencies) shrink to fit instead of overflowing.
        row.costLabel.enableAutoSizing = true;
        row.costLabel.fontSizeMax = costFontSize;
        row.costLabel.fontSizeMin = Mathf.Min(14f, costFontSize);
        row.costLabel.alignment = TextAlignmentOptions.TopLeft;
        SetBand(row.costLabel.rectTransform, 0.04f, 0.36f, textRightInset);

        row.buyButton = CreateButton(rowGo.transform, "Buy", buyText, buyButtonSize, buyColor, textColor,
                                     buyFontSize, out row.buyLabel, out row.buyImage);
        RectTransform br = row.buyButton.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.anchoredPosition = new Vector2(-20f, 0f);

        int captured = index;
        if (potion) row.buyButton.onClick.AddListener(() => TryBuyPotion(captured));
        else row.buyButton.onClick.AddListener(() => TryBuy(captured));

        if (hasChildren)
        {
            row.arrowButton = CreateButton(rowGo.transform, "Upgrades Arrow", upgradesArrowText, upgradesArrowSize,
                                           upgradesArrowColor, textColor, buyFontSize, out _, out _);
            RectTransform ar = row.arrowButton.GetComponent<RectTransform>();
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(1f, 0.5f);
            ar.anchoredPosition = new Vector2(-(20f + buyButtonSize.x + 10f), 0f);
            row.arrowButton.onClick.AddListener(() => OpenUpgradesWindow(captured));
        }

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
        t.go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        t.go.transform.SetParent(parent, false);
        t.go.GetComponent<Image>().color = rowColor;

        t.rect = t.go.GetComponent<RectTransform>();
        t.rect.anchorMin = new Vector2(0f, 1f);
        t.rect.anchorMax = new Vector2(1f, 1f);
        t.rect.pivot = new Vector2(0.5f, 1f);
        t.rect.sizeDelta = new Vector2(0f, trackerRowHeight);

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
            ? string.Format(trackerReachedFormat, PixelClicker.FormatNumber(count))
            : string.Format(trackerFormat, PixelClicker.FormatNumber(count), PixelClicker.FormatNumber(goal));
        t.fill.anchorMax = new Vector2(goal > 0d ? Mathf.Clamp01((float)(count / goal)) : 1f, 1f);
    }

    /// <summary>Stretches a text across a horizontal band of its parent (anchors are 0..1 vertically).</summary>
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
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => PixelAudio.Play("ui_click")); // the shop builds its own buttons, so it adds the sound itself

        labelText = CreateText(go.transform, "Label", label, labelSize, TextAlignmentOptions.Center, FontStyles.Bold);
        labelText.color = labelColor;
        RectTransform lr = labelText.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;

        return button;
    }

    // ------------------------------------------------------------------
    // Refreshing
    // ------------------------------------------------------------------

    private void RefreshRows()
    {
        float y = 0f;      // main list
        float subY = 0f;   // upgrades window list
        int visibleCount = 0, subVisibleCount = 0;

        // Tab buttons: highlight the open one.
        if (tabImages != null)
        {
            ShopTab[] order = { ShopTab.Pixels, ShopTab.Upgrades, ShopTab.Consumables, ShopTab.Minigames };
            for (int t = 0; t < tabImages.Length; t++)
                tabImages[t].color = order[t] == currentTab ? tabActiveColor : tabInactiveColor;
        }

        // The upgrades window only makes sense while its pack is owned.
        if (openParent >= 0 && !HasPack(openParent)) CloseUpgradesWindow();
        if (openParent >= 0) PixelUIKit.SetText(subTitle, string.Format(upgradesWindowTitle, packs[openParent].displayName));

        // Trackers: first things on the Minigames tab, once their minigame is in play.
        foreach (var pair in trackers)
        {
            PixelMinigame m = pair.Key;
            bool show = showMinigameTrackers && currentTab == ShopTab.Minigames && m != null &&
                        (m.Running || m.TrackerCount > 0d);
            PlaceTracker(pair.Value, show, m != null ? m.TrackerCount : 0d, m != null ? m.TrackerGoal : 1d,
                         ref y, ref visibleCount);
        }

        // Upgrades tab: the "Upgrades | Pixel" sub-tab buttons come first.
        bool onUpgrades = currentTab == ShopTab.Upgrades;
        if (subTabRow != null)
        {
            if (subTabRow.gameObject.activeSelf != onUpgrades) subTabRow.gameObject.SetActive(onUpgrades);
            if (onUpgrades)
            {
                subTabRow.anchoredPosition = new Vector2(0f, -y);
                y += subTabHeight + rowSpacing;
                visibleCount++;
                for (int s = 0; s < subTabImages.Length; s++)
                    subTabImages[s].color = s == upgradesSubTab ? tabActiveColor : tabInactiveColor;
            }
        }
        bool showGeneral = !onUpgrades || upgradesSubTab == 0;

        for (int i = 0; i < packs.Length && i < rows.Length; i++)
        {
            PackRow row = rows[i];
            ShopPack pack = packs[i];
            bool leveled = IsLeveled(pack);
            bool child = IsChild(i);

            bool owned = IsPurchased(i); // one-time: bought. upgrade: max level.
            bool requirementMet = IsRequirementMet(i);
            bool listed = IsPackRequirementMet(i) || showLockedPacks || HasPack(i);
            bool visible = child
                ? openParent >= 0 && pack.ParentIndex == openParent && listed && !HiddenAsPurchased(i)
                : TabOf(pack) == currentTab && listed && showGeneral && !HiddenAsPurchased(i);

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
            PixelUIKit.SetText(row.descLabel, ResolveDescription(pack));

            bool canBuy = !owned && requirementMet && CanAfford(i);

            if (owned) PixelUIKit.SetText(row.costLabel, "");
            else if (!requirementMet) PixelUIKit.SetText(row.costLabel, RequirementText(i));
            else PixelUIKit.SetText(row.costLabel, BuildCostText(CurrentCosts(pack)));

            row.buyButton.interactable = canBuy;
            if (owned) PixelUIKit.SetText(row.buyLabel, leveled ? maxedText : ownedText);
            else if (!requirementMet) PixelUIKit.SetText(row.buyLabel, lockedText);
            else PixelUIKit.SetText(row.buyLabel, leveled ? upgradeText : buyText);
            row.buyImage.color = canBuy ? buyColor : disabledColor;

            // Arrow to the upgrades window: only once the pack is owned.
            if (row.arrowButton != null)
            {
                bool showArrow = HasPack(i);
                if (row.arrowButton.gameObject.activeSelf != showArrow) row.arrowButton.gameObject.SetActive(showArrow);
            }
        }

        // Consumables tab: the two purchase cards replace the scrolling list.
        if (consumables != null) RefreshConsumablesTab(currentTab == ShopTab.Consumables);

        // Upgrades > Pixel: one boost row per unlocked pixel type, paid for with Ultra pixels.
        EnsureUltraRows();
        for (int t = 0; t < ultraRows.Length && t < clicker.Tiers.Length; t++)
        {
            PackRow row = ultraRows[t];
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            bool visible = onUpgrades && upgradesSubTab == 1 && tier.unlocked && !(HidePurchased && UltraBoostMaxed(t));
            row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowHeight + rowSpacing;
            visibleCount++;

            bool maxed = UltraBoostMaxed(t);
            long cost = UltraBoostCost(t);
            double now = clicker.UltraMultiplier(t);
            double next = now + clicker.UltraBonusPerLevel;

            row.nameLabel.text = string.Format(ultraRowNameFormat, tier.displayName) + "   <size=65%><color=#" +
                                 ColorUtility.ToHtmlStringRGB(levelColor) + ">" + string.Format(ultraLevelFormat, tier.ultraLevel) + "</color></size>";
            row.descLabel.text = string.Format(ultraRowDescFormat, tier.displayName, now.ToString("0.##"), maxed ? now.ToString("0.##") : next.ToString("0.##"));

            bool enough = PixelClicker.InfiniteResources || tier.ultraCount >= cost;
            row.costLabel.text = maxed ? "" : "<color=#" + ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor) + ">" +
                                 string.Format(ultraCostFormat, PixelClicker.FormatNumber(cost), tier.displayName, PixelClicker.FormatNumber(tier.ultraCount)) + "</color>";

            bool canBuy = !maxed && enough;
            row.buyButton.interactable = canBuy;
            row.buyLabel.text = maxed ? maxedText : ultraButtonText;
            row.buyImage.color = canBuy ? buyColor : disabledColor;
        }

        // The scroll content is as tall as the visible rows; the panel itself stays a fixed size.
        ApplyContentHeight(contentRect, emptyLabel, y, visibleCount);
        ApplyContentHeight(subContentRect, subEmptyLabel, subY, subVisibleCount);
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
        if (!IsLeveled(pack)) return pack.displayName;

        bool maxed = pack.level >= pack.levels.Length;
        string levelText = string.Format(maxed ? maxedLevelFormat : levelFormat, pack.level, pack.levels.Length);
        return pack.displayName + "   <size=65%><color=#" + ColorUtility.ToHtmlStringRGB(levelColor) + ">" +
               levelText + "</color></size>";
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
    private string BuildCostText(PackCost[] costs)
    {
        if (costs == null || costs.Length == 0) return costPrefix + "Free";

        StringBuilder sb = new StringBuilder(costPrefix);
        for (int i = 0; i < costs.Length; i++)
        {
            PackCost cost = costs[i];
            string name = CostName(cost);
            bool enough = CanAffordCost(cost);

            string part = string.Format(costEntryFormat, PixelClicker.FormatNumber(cost.amount), name);
            sb.Append("<color=#")
              .Append(ColorUtility.ToHtmlStringRGB(enough ? affordableColor : unaffordableColor))
              .Append('>').Append(part).Append("</color>");

            if (i < costs.Length - 1) sb.Append("   ");
        }
        return sb.ToString();
    }
}
