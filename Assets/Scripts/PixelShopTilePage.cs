// ---- System notes ----
// PixelShopTilePage.cs
//   Part of partial class PixelShop: the shared "tile page" layout used by the Pixels tab (PixelShopPixels.cs) and the Consumables
//   tab (PixelShopConsumables.cs). Top to bottom:
//   - category chips (the open one lit like a tab; each chip remembers its selected tile),
//   - a scrolling grid of tiles, 160 x 200 = 5 to a row (still 3D picture, one-line name, the price as colour swatches + numbers
//     (CostStrip: SetCosts / SetCostMessage; red number = can't afford), a small corner badge, a 'Locked' mark over
//     the picture). Click a tile = select it, Shift-click = the page's quick buy, hover = its description as the shop tooltip.
//   - one buy bar for the selected tile: spinning picture, name, the full description, the cost (swatch + number + currency name,
//     two columns), - count + (pages with hasCount)
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
        public TMP_Text name, badge, lockMark;
        public CostStrip costs;   // the price: a colour swatch + number per currency
    }

    /// <summary>
    /// A price shown as colour swatches with numbers (one per currency: the swatch is the pixel's colour, the number is white, or red
    /// when you can't afford it, grey while locked), or one line of text instead ('Full', 'Owned', what it still needs...).
    /// </summary>
    private class CostStrip
    {
        public RectTransform root;
        public Image[] swatches;
        public TMP_Text[] numbers;
        public TMP_Text message;
        public int maxColumns;    // slots per row (the rest wrap onto more rows)
        public bool withNames;    // "75 White" instead of "75" (the buy bar has room)
        public float fontSize;
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
        public TMP_Text barName, barDescription, barBuyLabel;
        public CostStrip barCosts;
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
        float costH = page.hasCount ? lineH : lineH * 2f + 8f;   // without the count row the cost gets two lines
        page.barCosts = BuildCostStrip(top, rightX, y, rightW, costH, 6, 2, costFontSize * 0.8f, true);
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
        float lineH = tileFontSize * 1.35f;
        float y = 8f;
        if (!hidePictures && tile.key != null)
            tile.picture = AddPictureSlot(tile.rect, pic, new Vector2(16f, y), false, tile.key, true, page.scroll.viewport, true);
        y += pic + 4f;

        tile.name = CreateText(go.transform, "Name", page.tileName(id), tileFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        PlaceTopLeft(tile.name.rectTransform, 6f, y, tileWidth - 12f, lineH);
        PixelUIKit.FitOneLine(tile.name, 14f);
        y += lineH;

        tile.costs = BuildCostStrip(go.transform, 6f, y, tileWidth - 12f, Mathf.Max(lineH, tileHeight - y - 6f), 3, 3, tileFontSize, false);

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

    /// <summary>Sets a tile's corner badge, name colour and lock mark (called from a page's fillTile; the price is SetCosts / SetCostMessage on tile.costs).</summary>
    private static void SetTile(ItemTile tile, string badge, bool locked, Color nameColour)
    {
        PixelUIKit.SetText(tile.badge, badge);
        tile.name.color = nameColour;
        if (tile.lockMark.gameObject.activeSelf != locked) tile.lockMark.gameObject.SetActive(locked);
        if (tile.picture != null) tile.picture.image.color = locked ? new Color(0.35f, 0.35f, 0.4f, 1f) : Color.white;
    }

    // ------------------------------------------------------------------
    // Prices as colour swatches
    // ------------------------------------------------------------------

    /// <summary>Builds an empty cost strip in a w x h box at (x, y) from its parent's top left.</summary>
    private CostStrip BuildCostStrip(Transform parent, float x, float y, float w, float h, int slots, int maxColumns, float fontSize, bool withNames)
    {
        CostStrip strip = new CostStrip { maxColumns = Mathf.Max(1, maxColumns), withNames = withNames, fontSize = fontSize };
        strip.root = new GameObject("Costs", typeof(RectTransform)).GetComponent<RectTransform>();
        strip.root.SetParent(parent, false);
        PlaceTopLeft(strip.root, x, y, w, h);
        strip.swatches = new Image[slots];
        strip.numbers = new TMP_Text[slots];
        for (int i = 0; i < slots; i++)
        {
            GameObject sw = new GameObject("Swatch " + i, typeof(RectTransform), typeof(Image));
            sw.transform.SetParent(strip.root, false);
            strip.swatches[i] = sw.GetComponent<Image>();
            PixelUIKit.StyleBox(strip.swatches[i]);
            strip.swatches[i].raycastTarget = false;
            strip.numbers[i] = CreateText(strip.root, "Number " + i, "", fontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            PixelUIKit.FitOneLine(strip.numbers[i], 12f);
            strip.numbers[i].raycastTarget = false;
            sw.SetActive(false);
            strip.numbers[i].gameObject.SetActive(false);
        }
        strip.message = CreateText(strip.root, "Message", "", fontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        strip.message.richText = true;
        strip.message.enableAutoSizing = true;
        strip.message.fontSizeMax = fontSize;
        strip.message.fontSizeMin = 14f;
        strip.message.raycastTarget = false;
        PixelUIKit.Stretch(strip.message.rectTransform);
        return strip;
    }

    /// <summary>Shows a price: one swatch (the currency's colour) + number per cost, laid out in centred rows. 'locked' greys the numbers.</summary>
    private void SetCosts(CostStrip strip, PackCost[] costs, bool locked)
    {
        if (costs == null || costs.Length == 0) { SetCostMessage(strip, "Free", affordableColor); return; }
        if (strip.message.gameObject.activeSelf) strip.message.gameObject.SetActive(false);

        int slots = strip.swatches.Length;
        int shown = Mathf.Min(costs.Length, slots);
        int columns = Mathf.Min(shown, strip.maxColumns);
        int rows = (shown + columns - 1) / columns;
        Vector2 size = strip.root.sizeDelta;
        float cellW = size.x / columns, cellH = size.y / rows;
        float swatch = Mathf.Min(cellH * 0.7f, strip.fontSize * 0.9f);
        for (int i = 0; i < slots; i++)
        {
            bool on = i < shown;
            if (strip.swatches[i].gameObject.activeSelf != on) strip.swatches[i].gameObject.SetActive(on);
            if (strip.numbers[i].gameObject.activeSelf != on) strip.numbers[i].gameObject.SetActive(on);
            if (!on) continue;

            PackCost cost = costs[i];
            int t = string.IsNullOrEmpty(cost.minigameCurrency) ? clicker.IndexOf(cost.type) : -1;
            strip.swatches[i].color = t >= 0 ? clicker.Tiers[t].UIColor : new Color(0.6f, 0.6f, 0.65f, 1f);   // (minigame currencies: grey)
            string text = PixelClicker.FormatNumberShort(cost.amount);
            if (strip.withNames) text += " " + ShortCostName(cost);
            if (i == shown - 1 && costs.Length > shown) text += " +" + (costs.Length - shown);   // (more than fit: '+n')
            TMP_Text number = strip.numbers[i];
            PixelUIKit.SetText(number, text);
            number.color = locked ? lockedItemColor : CanAffordCost(cost) ? textColor : unaffordableColor;

            // Place swatch + number as a centred pair in its cell (the last row is centred on its own).
            int row = i / columns, col = i % columns;
            int inRow = row == rows - 1 ? shown - row * columns : columns;
            float rowLeft = (size.x - inRow * cellW) * 0.5f;
            float numberW = Mathf.Min(cellW - swatch - 6f, number.GetPreferredValues(text).x + 2f);
            float pairX = rowLeft + col * cellW + (cellW - (swatch + 4f + numberW)) * 0.5f;
            float midY = row * cellH + cellH * 0.5f;
            PlaceTopLeft((RectTransform)strip.swatches[i].transform, pairX, midY - swatch * 0.5f, swatch, swatch);
            PlaceTopLeft(number.rectTransform, pairX + swatch + 4f, midY - cellH * 0.5f, numberW, cellH);
        }
    }

    /// <summary>Shows one line of text instead of a price ('Full', 'Owned', what it still needs...).</summary>
    private static void SetCostMessage(CostStrip strip, string text, Color colour)
    {
        for (int i = 0; i < strip.swatches.Length; i++)
        {
            if (strip.swatches[i].gameObject.activeSelf) strip.swatches[i].gameObject.SetActive(false);
            if (strip.numbers[i].gameObject.activeSelf) strip.numbers[i].gameObject.SetActive(false);
        }
        bool on = !string.IsNullOrEmpty(text);
        if (strip.message.gameObject.activeSelf != on) strip.message.gameObject.SetActive(on);
        PixelUIKit.SetText(strip.message, text ?? "");
        strip.message.color = colour;
    }

    /// <summary>A currency's name without " Pixel(s)" ("White").</summary>
    private string ShortCostName(PackCost cost)
    {
        string name = CostName(cost);
        if (name.EndsWith(" Pixels")) return name.Substring(0, name.Length - 7);
        if (name.EndsWith(" Pixel")) return name.Substring(0, name.Length - 6);
        return name;
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
            SetCostMessage(page.barCosts, "", textColor);
            SetPageBuy(page, false, lockedText);
            return;
        }
        page.fillBar(selected);
        if (page.hasCount && !page.countField.isFocused && page.countField.text != page.count.ToString())
            page.countField.SetTextWithoutNotify(page.count.ToString());   // (fillBar may have clamped the count)
    }
}
