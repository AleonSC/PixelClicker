// ---- System notes ----
// PixelShopTilePage.cs
//   Part of partial class PixelShop: the shared "tile page" layout used by the Pixels tab (PixelShopPixels.cs) and the Consumables
//   tab (PixelShopConsumables.cs). Top to bottom:
//   - category chips (the open one lit like a tab; each chip remembers its selected tile),
//   - a scrolling grid of tiles (still 3D picture, one-line name, short price line, a small corner badge, a 'Locked' mark over
//     the picture). Click a tile = select it, Shift-click = the page's quick buy, hover = its description as the shop tooltip.
//   - one buy bar for the selected tile: spinning picture, name, the full description, the cost, - count + (pages with hasCount)
//     and the Buy button (Shift-click Buy = the page's 'as many as you can').
//   A page only says WHAT to show through its delegates (collect, tileName, pictureKey, description, fillTile, fillBar, purchase,
//   quickBuy); building, layout, selection and the shared tooltip live here. Tiles are built once per id and reused.
//   Settings: PixelShop.cs "Consumables Tab" (chipHeight, buyBarHeight, tileWidth / Height / Gap, tileFontSize, buyMaxTip).
//   While a page is open the main scrolling list is hidden (RefreshTilePages, called from RefreshRows).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Part of <see cref="PixelShop"/>: a shop tab laid out as category chips, a grid of tiles and one buy bar (the Pixels and
/// Consumables tabs). The tab's own file fills in what each tile and the bar show.
/// </summary>
public partial class PixelShop
{
    /// <summary>One tile in a page's grid.</summary>
    private class ItemTile
    {
        public RectTransform rect;
        public Image background;
        public PictureSlot picture;
        public Func<string> key;   // the tile's picture (also shown, spinning, in the buy bar while selected)
        public TMP_Text name, price, badge, lockMark;
    }

    /// <summary>A tab laid out as chips + tile grid + buy bar. The delegates say what it shows; everything else is shared.</summary>
    private class TilePage
    {
        public GameObject area;
        public ShopTab tab;
        public string[] chipTitles;
        public Image[] chipImages;
        public int chip;                 // the open chip
        public int[] selected;           // the selected id on each chip (-1 = none)
        public bool hasCount;            // the bar has - count + (consumables); packs are bought one at a time
        public ScrollRect scroll;
        public RectTransform content;
        public GameObject scrollBar;
        public TMP_Text empty;
        public readonly Dictionary<int, ItemTile> tiles = new Dictionary<int, ItemTile>();
        public readonly List<int> ids = new List<int>();

        // The buy bar.
        public TMP_Text barName, barDescription, barCost, barBuyLabel;
        public TMP_InputField countField;
        public Button minus, plus, buy;
        public Image buyImage;
        public int count = 1;

        // What the page shows (set by the tab's own file).
        public Action<int, List<int>> collect;       // chip -> the ids listed on it, in order
        public Func<int, string> tileName;
        public Func<int, Func<string>> pictureKey;   // id -> its picture key (null = no picture)
        public Func<int, string> description;
        public Action<int, ItemTile, bool> fillTile; // id, tile, selected: price line, badge, lock
        public Action<int> fillBar;                  // id (or -1): cost line and Buy (SetPageBuy), count clamp
        public Action<int, bool> purchase;           // Buy pressed: id, Shift held ('as many as you can')
        public Action<int> quickBuy;                 // Shift-click on a tile

        public int Selected => selected[chip];
    }

    private readonly List<TilePage> tilePages = new List<TilePage>();

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    /// <summary>Builds an (empty, hidden) page below 'top' units from the panel's top edge. The caller sets its delegates.</summary>
    private TilePage BuildTilePage(Transform panel, float top, ShopTab tab, string[] chipTitles, bool hasCount, int startChip)
    {
        TilePage page = new TilePage { tab = tab, chipTitles = chipTitles, hasCount = hasCount, chip = Mathf.Clamp(startChip, 0, chipTitles.Length - 1) };
        page.selected = new int[chipTitles.Length];
        for (int i = 0; i < page.selected.Length; i++) page.selected[i] = -1;

        page.area = new GameObject(tab + " Page", typeof(RectTransform));
        page.area.transform.SetParent(panel, false);
        RectTransform ar = page.area.GetComponent<RectTransform>();
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(panelPadding, panelPadding);
        ar.offsetMax = new Vector2(-panelPadding, -top);

        float width = panelWidth - panelPadding * 2f;
        BuildChips(page, width);

        // The tile grid fills the space between the chips and the buy bar.
        page.scroll = PixelUIKit.CreateScrollView(page.area.transform, "Tile Grid", tabActiveColor, PixelUIKit.ScrollBarWidth,
                                                  scrollSpeed, out page.content, out page.scrollBar);
        RectTransform gr = page.scroll.GetComponent<RectTransform>();
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = new Vector2(0f, buyBarHeight + cardGap);
        gr.offsetMax = new Vector2(0f, -(chipHeight + cardGap));

        page.empty = CreateText(page.scroll.transform, "Empty", noItemsText, costFontSize, TextAlignmentOptions.Center, FontStyles.Italic);
        PixelUIKit.Stretch(page.empty.rectTransform);
        page.empty.gameObject.SetActive(false);

        BuildBuyBar(page, width);
        page.area.SetActive(false);
        tilePages.Add(page);
        return page;
    }

    /// <summary>The category buttons along the top: the open one is lit like a tab.</summary>
    private void BuildChips(TilePage page, float width)
    {
        int n = page.chipTitles.Length;
        page.chipImages = new Image[n];
        float chipWidth = (width - cardGap * (n - 1)) / n;
        for (int c = 0; c < n; c++)
        {
            int captured = c;
            Button chip = CreateButton(page.area.transform, "Chip " + page.chipTitles[c], page.chipTitles[c], Vector2.zero, ButtonRole.Neutral,
                                       textColor, buyFontSize * 0.75f, out TMP_Text label, out page.chipImages[c]);
            PixelUIKit.Caps(label);
            PlaceTopLeft(chip.GetComponent<RectTransform>(), c * (chipWidth + cardGap), 0f, chipWidth, chipHeight);
            chip.onClick.AddListener(() => SelectPageChip(page, captured));
        }
    }

    /// <summary>The bar along the bottom: picture, name and description of the selected tile, its cost, (- count +) and Buy.</summary>
    private void BuildBuyBar(TilePage page, float width)
    {
        GameObject bar = new GameObject("Buy Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(page.area.transform, false);
        bar.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(bar.GetComponent<Image>());
        RectTransform br = bar.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = new Vector2(0f, buyBarHeight);
        br.anchoredPosition = Vector2.zero;
        // (the bar is anchored to the bottom; its children are placed from this top-left holder)
        RectTransform top = new GameObject("Top", typeof(RectTransform)).GetComponent<RectTransform>();
        top.SetParent(bar.transform, false);
        top.anchorMin = top.anchorMax = top.pivot = new Vector2(0f, 1f);
        top.sizeDelta = new Vector2(width, buyBarHeight);
        top.anchoredPosition = Vector2.zero;

        float pad = 14f;
        float textX = pad;
        if (!hidePictures)
        {
            AddPictureSlot(top, cardPictureSize, new Vector2(pad, (buyBarHeight - cardPictureSize) * 0.5f), false, () => SelectedTileKey(page), false);
            textX += cardPictureSize + pad;
        }

        // Right column: cost, (- count +), Buy.
        float rightW = 300f;
        float rightX = width - pad - rightW;
        float lineH = (buyBarHeight - pad * 2f - 16f) / 3f;
        float y = pad;
        page.barCost = CreateText(top, "Cost", "", costFontSize, TextAlignmentOptions.Center, FontStyles.Normal);
        page.barCost.richText = true;
        page.barCost.enableAutoSizing = true;
        page.barCost.fontSizeMax = costFontSize;
        page.barCost.fontSizeMin = Mathf.Min(12f, costFontSize);
        float costH = page.hasCount ? lineH : lineH * 2f + 8f;   // without the count row the cost gets two lines
        PlaceTopLeft(page.barCost.rectTransform, rightX, y, rightW, costH);
        y += costH + 8f;

        if (page.hasCount)
        {
            float stepW = 56f, countW = rightW - stepW * 2f - 16f;
            page.minus = CreateButton(top, "Minus", "-", Vector2.zero, ButtonRole.Neutral, textColor, buyFontSize, out _, out _);
            PlaceTopLeft(page.minus.GetComponent<RectTransform>(), rightX, y, stepW, lineH);
            page.minus.onClick.AddListener(() => ChangePageCount(page, -1));

            page.countField = PixelUIKit.CreateInputField(font, top, "Count", new Vector2(countW, lineH), tabInactiveColor, textColor, buyFontSize, "1");
            page.countField.contentType = TMP_InputField.ContentType.IntegerNumber;
            page.countField.text = "1";
            page.countField.textComponent.alignment = TextAlignmentOptions.Center;
            PlaceTopLeft(page.countField.GetComponent<RectTransform>(), rightX + stepW + 8f, y, countW, lineH);
            page.countField.onValueChanged.AddListener(s =>
            {
                page.count = int.TryParse(s, out int n) ? Mathf.Clamp(n, 1, maxPerPurchase) : 1;   // live, so the cost updates while typing
                RefreshRows();
            });
            page.countField.onEndEdit.AddListener(_ => page.countField.text = page.count.ToString());

            page.plus = CreateButton(top, "Plus", "+", Vector2.zero, ButtonRole.Neutral, textColor, buyFontSize, out _, out _);
            PlaceTopLeft(page.plus.GetComponent<RectTransform>(), rightX + rightW - stepW, y, stepW, lineH);
            page.plus.onClick.AddListener(() => ChangePageCount(page, +1));
            y += lineH + 8f;
        }

        page.buy = CreateButton(top, "Buy", "", Vector2.zero, ButtonRole.Success, textColor, buyFontSize, out page.barBuyLabel, out page.buyImage);
        PlaceTopLeft(page.buy.GetComponent<RectTransform>(), rightX, y, rightW, lineH);
        page.buy.onClick.AddListener(() => { int id = page.Selected; if (id >= 0) page.purchase(id, PixelInput.ShiftHeld()); });
        if (page.hasCount)
        {
            Func<string> maxTip = () => buyMaxTip;   // hovering Buy explains Shift-click
            PixelHoverTip buyTip = page.buy.gameObject.AddComponent<PixelHoverTip>();
            buyTip.onEnter = () => hoverTip = maxTip;
            buyTip.onExit = () => { if (hoverTip == maxTip) hoverTip = null; };
        }

        // Middle: name and the full description.
        float textW = rightX - pad - textX;
        page.barName = CreateText(top, "Name", "", nameFontSize * 0.9f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        page.barName.enableAutoSizing = true;
        page.barName.fontSizeMax = nameFontSize * 0.9f;
        page.barName.fontSizeMin = 14f;
        PixelUIKit.Caps(page.barName);
        PlaceTopLeft(page.barName.rectTransform, textX, pad, textW, 44f);

        page.barDescription = CreateText(top, "Description", "", tileFontSize * 1.1f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        page.barDescription.richText = true;
        page.barDescription.enableAutoSizing = true;
        page.barDescription.fontSizeMax = tileFontSize * 1.1f;
        page.barDescription.fontSizeMin = 12f;
        PlaceTopLeft(page.barDescription.rectTransform, textX, pad + 50f, textW, buyBarHeight - pad * 2f - 50f);
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private static string SelectedTileKey(TilePage page)
    {
        int id = page.Selected;
        return id >= 0 && page.tiles.TryGetValue(id, out ItemTile tile) && tile.key != null ? tile.key() : null;
    }

    /// <summary>A tile: still picture on top, name, price line, a badge in the top right corner and a 'Locked' mark.</summary>
    private ItemTile BuildTile(TilePage page, int id)
    {
        GameObject go = new GameObject("Tile " + id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(page.content, false);
        ItemTile tile = new ItemTile { rect = go.GetComponent<RectTransform>(), background = go.GetComponent<Image>(), key = page.pictureKey(id) };
        tile.background.color = rowColor;
        PixelUIKit.StyleBox(tile.background);
        tile.rect.anchorMin = tile.rect.anchorMax = tile.rect.pivot = new Vector2(0f, 1f);
        tile.rect.sizeDelta = new Vector2(tileWidth, tileHeight);

        float pic = tileWidth - 32f;
        float y = 8f;
        if (!hidePictures && tile.key != null)
            tile.picture = AddPictureSlot(tile.rect, pic, new Vector2(16f, y), false, tile.key, true, page.scroll.viewport, true);
        y += pic + 4f;

        float lineH = tileFontSize * 1.35f;
        tile.name = CreateText(go.transform, "Name", page.tileName(id), tileFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        PlaceTopLeft(tile.name.rectTransform, 6f, y, tileWidth - 12f, lineH);
        PixelUIKit.FitOneLine(tile.name);
        y += lineH;

        tile.price = CreateText(go.transform, "Price", "", tileFontSize * 0.9f, TextAlignmentOptions.Center, FontStyles.Normal);
        tile.price.richText = true;
        tile.price.enableAutoSizing = true;
        tile.price.fontSizeMax = tileFontSize * 0.9f;
        tile.price.fontSizeMin = 10f;
        PlaceTopLeft(tile.price.rectTransform, 6f, y, tileWidth - 12f, lineH);

        tile.badge = CreateText(go.transform, "Badge", "", tileFontSize * 0.85f, TextAlignmentOptions.TopRight, FontStyles.Bold);
        tile.badge.richText = true;
        PlaceTopLeft(tile.badge.rectTransform, 6f, 6f, tileWidth - 12f, lineH);

        tile.lockMark = CreateText(go.transform, "Locked", lockedText, tileFontSize * 0.85f, TextAlignmentOptions.Center, FontStyles.Bold);
        tile.lockMark.color = unaffordableColor;
        PlaceTopLeft(tile.lockMark.rectTransform, 6f, 8f + pic * 0.5f - lineH * 0.5f, tileWidth - 12f, lineH);
        tile.lockMark.gameObject.SetActive(false);

        PixelUIKit.MakeClickable(go, () => ClickTile(page, id), true, false);
        Func<string> tip = () => page.description(id);
        PixelHoverTip hover = go.AddComponent<PixelHoverTip>();
        hover.onEnter = () => hoverTip = tip;
        hover.onExit = () => { if (hoverTip == tip) hoverTip = null; };
        return tile;
    }

    // ------------------------------------------------------------------
    // Using a page
    // ------------------------------------------------------------------

    private void SelectPageChip(TilePage page, int chip)
    {
        page.chip = Mathf.Clamp(chip, 0, page.chipTitles.Length - 1);
        if (page.content != null) page.content.anchoredPosition = Vector2.zero;
        if (page.scroll != null) page.scroll.StopMovement();
        RefreshRows();
    }

    private void ClickTile(TilePage page, int id)
    {
        page.selected[page.chip] = id;
        if (PixelInput.ShiftHeld() && page.quickBuy != null) page.quickBuy(id);
        RefreshRows();
    }

    private void ChangePageCount(TilePage page, int delta)
    {
        page.count = Mathf.Clamp(page.count + delta, 1, maxPerPurchase);
        page.countField.SetTextWithoutNotify(page.count.ToString());
        RefreshRows();
    }

    /// <summary>Sets the buy bar's Buy button (called from a page's fillBar).</summary>
    private void SetPageBuy(TilePage page, bool canBuy, string label)
    {
        page.buy.interactable = canBuy;
        PixelUIKit.SetText(page.barBuyLabel, label);
        SetShopRole(page.buyImage, canBuy ? ButtonRole.Success : ButtonRole.Neutral);
    }

    /// <summary>Sets a tile's price line, corner badge and lock mark (called from a page's fillTile).</summary>
    private static void SetTile(ItemTile tile, string price, string badge, bool locked, Color nameColour)
    {
        PixelUIKit.SetText(tile.price, price);
        PixelUIKit.SetText(tile.badge, badge);
        tile.name.color = nameColour;
        if (tile.lockMark.gameObject.activeSelf != locked) tile.lockMark.gameObject.SetActive(locked);
        if (tile.picture != null) tile.picture.image.color = locked ? new Color(0.35f, 0.35f, 0.4f, 1f) : Color.white;
    }

    private string Coloured(Color c, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";

    /// <summary>A tile's short price: the first cost ("50 White"), plus "+n" when there are more.</summary>
    private string TilePrice(PackCost[] costs, Color colour)
    {
        if (costs == null || costs.Length == 0) return Coloured(colour, "Free");
        string name = CostName(costs[0]);
        if (name.EndsWith(" Pixels")) name = name.Substring(0, name.Length - 7);
        else if (name.EndsWith(" Pixel")) name = name.Substring(0, name.Length - 6);
        string text = PixelClicker.FormatNumberShort(costs[0].amount) + " " + name + (costs.Length > 1 ? " +" + (costs.Length - 1) : "");
        return Coloured(colour, text);
    }

    // ------------------------------------------------------------------
    // Refreshing
    // ------------------------------------------------------------------

    /// <summary>Called from RefreshRows: shows the open tab's page (if it has one) and hides the scrolling list while it is open.</summary>
    private void RefreshTilePages(bool searching)
    {
        bool anyOpen = false;
        foreach (TilePage page in tilePages)
        {
            bool open = !searching && currentTab == page.tab;
            if (page.area.activeSelf != open) page.area.SetActive(open);
            if (!open) continue;
            anyOpen = true;
            RefreshTilePage(page);
        }
        if (scrollRect != null)
        {
            if (scrollRect.gameObject.activeSelf == anyOpen) scrollRect.gameObject.SetActive(!anyOpen);
            if (scrollRect.verticalScrollbar != null && scrollRect.verticalScrollbar.gameObject.activeSelf == anyOpen)
                scrollRect.verticalScrollbar.gameObject.SetActive(!anyOpen);
        }
    }

    private void RefreshTilePage(TilePage page)
    {
        for (int c = 0; c < page.chipImages.Length; c++) SetShopRole(page.chipImages[c], c == page.chip ? ButtonRole.Selected : ButtonRole.Neutral);

        page.collect(page.chip, page.ids);
        if (!page.ids.Contains(page.Selected)) page.selected[page.chip] = page.ids.Count > 0 ? page.ids[0] : -1;
        int selected = page.Selected;

        // Tiles: only this chip's are shown, laid out left to right, row by row, the grid centred.
        foreach (var pair in page.tiles)
            if (pair.Value.rect.gameObject.activeSelf && !page.ids.Contains(pair.Key)) pair.Value.rect.gameObject.SetActive(false);

        RectTransform view = page.scroll.GetComponent<RectTransform>();
        float viewWidth = view.rect.width > 1f ? view.rect.width : panelWidth - panelPadding * 2f;
        float usable = viewWidth - PixelUIKit.ScrollBarWidth - 8f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((usable + tileGap) / (tileWidth + tileGap)));
        float left = (usable - (columns * tileWidth + (columns - 1) * tileGap)) * 0.5f;

        for (int n = 0; n < page.ids.Count; n++)
        {
            int id = page.ids[n];
            if (!page.tiles.TryGetValue(id, out ItemTile tile)) { tile = BuildTile(page, id); page.tiles[id] = tile; }
            if (!tile.rect.gameObject.activeSelf) tile.rect.gameObject.SetActive(true);
            tile.rect.anchoredPosition = new Vector2(left + (n % columns) * (tileWidth + tileGap), -(4f + (n / columns) * (tileHeight + tileGap)));
            tile.background.color = id == selected ? tabActiveColor : rowColor;
            page.fillTile(id, tile, id == selected);
        }
        int rowCount = (page.ids.Count + columns - 1) / columns;
        float contentHeight = rowCount > 0 ? 8f + rowCount * tileHeight + (rowCount - 1) * tileGap : 0f;
        PixelUIKit.UpdateScrollView(page.scroll, page.scrollBar, contentHeight, view.rect.height);
        if (page.empty.gameObject.activeSelf != (page.ids.Count == 0)) page.empty.gameObject.SetActive(page.ids.Count == 0);

        // The buy bar.
        bool has = selected >= 0;
        if (page.hasCount)
        {
            page.minus.interactable = page.plus.interactable = has;
            if (!page.countField.isFocused && page.countField.text != page.count.ToString())
                page.countField.SetTextWithoutNotify(page.count.ToString());
        }
        PixelUIKit.SetText(page.barName, has ? page.tileName(selected) : "");
        PixelUIKit.SetText(page.barDescription, has ? page.description(selected) : noItemsText);
        if (!has)
        {
            PixelUIKit.SetText(page.barCost, "");
            SetPageBuy(page, false, lockedText);
            return;
        }
        page.fillBar(selected);
        if (page.hasCount && !page.countField.isFocused && page.countField.text != page.count.ToString())
            page.countField.SetTextWithoutNotify(page.count.ToString());   // (fillBar may have clamped the count)
    }
}
