// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopConsumables.cs
//   Third-and-a-half part of partial class PixelShop: the Consumables tab. Layout, top to bottom:
//   - category chips (Potions / Utilities / Seeds / Supplies = PixelConsumables.CategoryOf 0-3; titles potionsCardTitle...),
//   - a scrolling grid of item tiles for the open category (3D picture drawn once as a still, short name, price in green / red,
//     owned count badge; locked items stay listed, greyed, with a lock line). Click a tile = select it; Shift-click = buy one.
//     Hovering a tile shows its description as the shop tooltip. The Recycler stands first among Utilities.
//   - one buy bar for the selected tile: spinning picture, name, the full description, cost, - count +, Buy (Shift-click Buy =
//     as many as you can afford, MaxAffordable).
//   Tiles are built once per item and reused (consumableTiles); the selection is remembered per category. Craft-only, not-sold,
//   vendor-only items and Dragon seeds are never listed. The search box still lists matching items as rows in the main list
//   (RefreshSearchItems in PixelShopUI.cs). Buying goes through TryBuyItems(item, count). Settings are in PixelShop.cs
//   ("Consumables Tab").
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop's Consumables tab: category chips over a grid of item tiles, and one buy bar for the selected item. Items whose
/// pixel type is not unlocked yet are shown locked (with what they need) instead of being hidden. Part of <see cref="PixelShop"/>;
/// the settings live in PixelShop.cs.
/// </summary>
public partial class PixelShop
{
    /// <summary>One item's tile in the grid.</summary>
    private class ItemTile
    {
        public RectTransform rect;
        public Image background;
        public PictureSlot picture;
        public TMP_Text name, price, owned, lockMark;
    }

    private GameObject consumablesArea;
    private int consumablesCategory;                                   // 0 Potions, 1 Utilities, 2 Seeds, 3 Supplies
    private readonly int[] selectedItemIn = { -1, -1, -1, -1 };        // the selected item of each category
    private Image[] chipImages;
    private ScrollRect tileScroll;
    private RectTransform tileContent;
    private GameObject tileScrollBar;
    private TMP_Text tileEmptyLabel;
    private readonly Dictionary<int, ItemTile> consumableTiles = new Dictionary<int, ItemTile>();
    private readonly List<int> categoryItems = new List<int>();

    // The buy bar.
    private TMP_Text barName, barDescription, barCost, barBuyLabel;
    private TMP_InputField barCountField;
    private Button barMinus, barPlus, barBuy;
    private Image barBuyImage;
    private int barCount = 1;

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    private void BuildConsumablesArea(Transform panel, float top)
    {
        consumablesArea = new GameObject("Consumables Area", typeof(RectTransform));
        consumablesArea.transform.SetParent(panel, false);
        RectTransform ar = consumablesArea.GetComponent<RectTransform>();
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(panelPadding, panelPadding);
        ar.offsetMax = new Vector2(-panelPadding, -top);

        float width = panelWidth - panelPadding * 2f;
        BuildChips(consumablesArea.transform, width);

        // The tile grid fills the space between the chips and the buy bar.
        tileScroll = PixelUIKit.CreateScrollView(consumablesArea.transform, "Item Grid", tabActiveColor, PixelUIKit.ScrollBarWidth,
                                                 scrollSpeed, out tileContent, out tileScrollBar);
        RectTransform gr = tileScroll.GetComponent<RectTransform>();
        gr.anchorMin = Vector2.zero;
        gr.anchorMax = Vector2.one;
        gr.offsetMin = new Vector2(0f, buyBarHeight + cardGap);
        gr.offsetMax = new Vector2(0f, -(chipHeight + cardGap));

        tileEmptyLabel = CreateText(tileScroll.transform, "Empty", noItemsText, costFontSize, TextAlignmentOptions.Center, FontStyles.Italic);
        PixelUIKit.Stretch(tileEmptyLabel.rectTransform);
        tileEmptyLabel.gameObject.SetActive(false);

        BuildBuyBar(consumablesArea.transform, width);
        consumablesArea.SetActive(false);
    }

    private string CategoryTitle(int category) =>
        category == 0 ? potionsCardTitle : category == 1 ? utilitiesCardTitle : category == 2 ? seedsCardTitle : suppliesCardTitle;

    /// <summary>The four category buttons along the top: the open one is lit like a tab.</summary>
    private void BuildChips(Transform parent, float width)
    {
        chipImages = new Image[4];
        float chipWidth = (width - cardGap * 3f) / 4f;
        for (int c = 0; c < 4; c++)
        {
            int captured = c;
            Button chip = CreateButton(parent, "Chip " + CategoryTitle(c), CategoryTitle(c), Vector2.zero, tabInactiveColor, textColor,
                                       buyFontSize * 0.75f, out TMP_Text label, out chipImages[c]);
            PixelUIKit.Caps(label);
            PlaceTopLeft(chip.GetComponent<RectTransform>(), c * (chipWidth + cardGap), 0f, chipWidth, chipHeight);
            chip.onClick.AddListener(() => SelectConsumablesCategory(captured));
        }
    }

    /// <summary>The bar along the bottom: picture, name and description of the selected item, its cost, - count +, and Buy.</summary>
    private void BuildBuyBar(Transform parent, float width)
    {
        GameObject bar = new GameObject("Buy Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        bar.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(bar.GetComponent<Image>());
        RectTransform br = bar.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = new Vector2(0f, buyBarHeight);
        br.anchoredPosition = Vector2.zero;
        // (the bar is anchored to the bottom: children are placed from its top left like the cards were)
        RectTransform top = new GameObject("Top", typeof(RectTransform)).GetComponent<RectTransform>();
        top.SetParent(bar.transform, false);
        top.anchorMin = top.anchorMax = top.pivot = new Vector2(0f, 1f);
        top.sizeDelta = new Vector2(width, buyBarHeight);
        top.anchoredPosition = Vector2.zero;

        float pad = 14f;
        float textX = pad;
        if (!hidePictures)
        {
            AddPictureSlot(top, cardPictureSize, new Vector2(pad, (buyBarHeight - cardPictureSize) * 0.5f), false, SelectedItemKey, false);
            textX += cardPictureSize + pad;
        }

        // Right column: cost, - count +, Buy.
        float rightW = 300f;
        float rightX = width - pad - rightW;
        float lineH = (buyBarHeight - pad * 2f - 16f) / 3f;
        float y = pad;
        barCost = CreateText(top, "Cost", "", costFontSize, TextAlignmentOptions.Center, FontStyles.Normal);
        barCost.richText = true;
        barCost.enableAutoSizing = true;
        barCost.fontSizeMax = costFontSize;
        barCost.fontSizeMin = Mathf.Min(12f, costFontSize);
        PlaceTopLeft(barCost.rectTransform, rightX, y, rightW, lineH);
        y += lineH + 8f;

        float stepW = 56f, countW = rightW - stepW * 2f - 16f;
        barMinus = CreateButton(top, "Minus", "-", Vector2.zero, tabInactiveColor, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(barMinus.GetComponent<RectTransform>(), rightX, y, stepW, lineH);
        barMinus.onClick.AddListener(() => ChangeBarCount(-1));

        barCountField = PixelUIKit.CreateInputField(font, top, "Count", new Vector2(countW, lineH), tabInactiveColor, textColor, buyFontSize, "1");
        barCountField.contentType = TMP_InputField.ContentType.IntegerNumber;
        barCountField.text = "1";
        barCountField.textComponent.alignment = TextAlignmentOptions.Center;
        PlaceTopLeft(barCountField.GetComponent<RectTransform>(), rightX + stepW + 8f, y, countW, lineH);
        barCountField.onValueChanged.AddListener(s =>
        {
            barCount = int.TryParse(s, out int n) ? Mathf.Clamp(n, 1, maxPerPurchase) : 1;   // live, so the cost updates while typing
            RefreshRows();
        });
        barCountField.onEndEdit.AddListener(_ => barCountField.text = barCount.ToString());

        barPlus = CreateButton(top, "Plus", "+", Vector2.zero, tabInactiveColor, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(barPlus.GetComponent<RectTransform>(), rightX + rightW - stepW, y, stepW, lineH);
        barPlus.onClick.AddListener(() => ChangeBarCount(+1));
        y += lineH + 8f;

        barBuy = CreateButton(top, "Buy", "", Vector2.zero, buyColor, textColor, buyFontSize, out barBuyLabel, out barBuyImage);
        PlaceTopLeft(barBuy.GetComponent<RectTransform>(), rightX, y, rightW, lineH);
        barBuy.onClick.AddListener(BuySelectedItem);
        System.Func<string> maxTip = () => buyMaxTip;   // hovering Buy explains Shift-click
        PixelHoverTip buyTip = barBuy.gameObject.AddComponent<PixelHoverTip>();
        buyTip.onEnter = () => hoverTip = maxTip;
        buyTip.onExit = () => { if (hoverTip == maxTip) hoverTip = null; };

        // Middle: name and the full description.
        float textW = rightX - pad - textX;
        barName = CreateText(top, "Name", "", nameFontSize * 0.9f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        barName.enableAutoSizing = true;
        barName.fontSizeMax = nameFontSize * 0.9f;
        barName.fontSizeMin = 14f;
        PixelUIKit.Caps(barName);
        PlaceTopLeft(barName.rectTransform, textX, pad, textW, 44f);

        barDescription = CreateText(top, "Description", "", tileFontSize * 1.1f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        barDescription.enableAutoSizing = true;
        barDescription.fontSizeMax = tileFontSize * 1.1f;
        barDescription.fontSizeMin = 12f;
        PlaceTopLeft(barDescription.rectTransform, textX, pad + 50f, textW, buyBarHeight - pad * 2f - 50f);
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private string SelectedItemKey()
    {
        int it = selectedItemIn[consumablesCategory];
        return it >= 0 && consumables != null && it < consumables.ItemCount ? "item:" + consumables.ItemName(it) : null;
    }

    /// <summary>A tile: still picture on top, name, price line, and the owned count in the top right corner.</summary>
    private ItemTile BuildTile(int item)
    {
        GameObject go = new GameObject("Tile " + consumables.ItemName(item), typeof(RectTransform), typeof(Image));
        go.transform.SetParent(tileContent, false);
        ItemTile tile = new ItemTile { rect = go.GetComponent<RectTransform>(), background = go.GetComponent<Image>() };
        tile.background.color = rowColor;
        PixelUIKit.StyleBox(tile.background);
        tile.rect.anchorMin = tile.rect.anchorMax = tile.rect.pivot = new Vector2(0f, 1f);
        tile.rect.sizeDelta = new Vector2(tileWidth, tileHeight);

        float pic = tileWidth - 32f;
        float y = 8f;
        if (!hidePictures)
        {
            string key = "item:" + consumables.ItemName(item);
            tile.picture = AddPictureSlot(tile.rect, pic, new Vector2(16f, y), false, () => key, true, tileScroll.viewport, true);
        }
        y += pic + 4f;

        float lineH = tileFontSize * 1.35f;
        tile.name = CreateText(go.transform, "Name", consumables.ItemName(item), tileFontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        PlaceTopLeft(tile.name.rectTransform, 6f, y, tileWidth - 12f, lineH);
        PixelUIKit.FitOneLine(tile.name);
        y += lineH;

        tile.price = CreateText(go.transform, "Price", "", tileFontSize * 0.9f, TextAlignmentOptions.Center, FontStyles.Normal);
        tile.price.richText = true;
        PlaceTopLeft(tile.price.rectTransform, 6f, y, tileWidth - 12f, lineH);

        tile.owned = CreateText(go.transform, "Owned", "", tileFontSize * 0.85f, TextAlignmentOptions.TopRight, FontStyles.Bold);
        PlaceTopLeft(tile.owned.rectTransform, 6f, 6f, tileWidth - 12f, lineH);

        tile.lockMark = CreateText(go.transform, "Locked", lockedText, tileFontSize * 0.85f, TextAlignmentOptions.Center, FontStyles.Bold);
        tile.lockMark.color = unaffordableColor;
        PlaceTopLeft(tile.lockMark.rectTransform, 6f, 8f + pic * 0.5f - lineH * 0.5f, tileWidth - 12f, lineH);

        int captured = item;
        PixelUIKit.MakeClickable(go, () => ClickTile(captured), true, false);
        System.Func<string> tip = () => consumables != null && captured < consumables.ItemCount ? consumables.ItemDescription(captured) : null;
        PixelHoverTip hover = go.AddComponent<PixelHoverTip>();
        hover.onEnter = () => hoverTip = tip;
        hover.onExit = () => { if (hoverTip == tip) hoverTip = null; };
        return tile;
    }

    // ------------------------------------------------------------------
    // Using the page
    // ------------------------------------------------------------------

    private void SelectConsumablesCategory(int category)
    {
        consumablesCategory = Mathf.Clamp(category, 0, 3);
        if (tileContent != null) tileContent.anchoredPosition = Vector2.zero;
        if (tileScroll != null) tileScroll.StopMovement();
        RefreshRows();
    }

    private void ClickTile(int item)
    {
        selectedItemIn[consumablesCategory] = item;
        if (PixelInput.ShiftHeld()) TryBuyItems(item, 1);   // (refreshes the rows itself when it buys)
        RefreshRows();
    }

    private void ChangeBarCount(int delta)
    {
        barCount = Mathf.Clamp(barCount + delta, 1, maxPerPurchase);
        barCountField.SetTextWithoutNotify(barCount.ToString());
        RefreshRows();
    }

    private void BuySelectedItem()
    {
        int item = selectedItemIn[consumablesCategory];
        if (item < 0) return;
        int n = PixelInput.ShiftHeld() ? MaxAffordable(item) : barCount;
        if (n > 0) TryBuyItems(item, n);
    }

    /// <summary>How many of an item you could buy right now (room and maxPerPurchase allowing).</summary>
    private int MaxAffordable(int item)
    {
        PackCost[] one = consumables.ItemCosts(item);
        int lo = 0, hi = Mathf.Min(maxPerPurchase, consumables.ItemRoom(item));
        while (lo < hi)   // (affordability only drops as the count grows, so halve the range each step)
        {
            int mid = (lo + hi + 1) / 2;
            if (CanAffordCosts(ScaleCosts(one, mid))) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>A pixel type's item is locked until that pixel type is unlocked (you may already own some).</summary>
    private bool ItemLocked(int item) =>
        potionsNeedUnlockedPixel && consumables.ItemOwned(item) <= 0 && !clicker.IsUnlocked(consumables.ItemRequiredType(item));

    private static PackCost[] ScaleCosts(PackCost[] costs, int count)
    {
        if (costs == null) return new PackCost[0];
        PackCost[] scaled = new PackCost[costs.Length];
        for (int i = 0; i < costs.Length; i++) scaled[i] = new PackCost { type = costs[i].type, amount = costs[i].amount * count, minigameCurrency = costs[i].minigameCurrency };
        return scaled;
    }

    /// <summary>Items of a category, in shop order (the Recycler first among Utilities).</summary>
    private void CollectCategoryItems(int category, List<int> list)
    {
        list.Clear();
        for (int i = 0; i < consumables.ItemCount; i++)
        {
            if (consumables.ItemCraftOnly(i) || consumables.ItemNotSold(i) || consumables.ItemVendorOnly(i) || consumables.IsDragonSeed(i)) continue;   // crafted / vendor items aren't sold here
            if (consumables.CategoryOf(i) == category) list.Add(i);
        }
        if (category == 1)
        {
            int r = list.FindIndex(i => consumables.IsDevice(i) && consumables.DeviceKindOf(i) == PixelConsumables.DeviceKind.Recycler);
            if (r > 0) { int recyclerItem = list[r]; list.RemoveAt(r); list.Insert(0, recyclerItem); }
        }
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

    /// <summary>Called from RefreshRows: shows the page on the Consumables tab (and hides the scrolling list there).</summary>
    private void RefreshConsumablesTab(bool onConsumables)
    {
        if (consumablesArea == null) return;

        if (consumablesArea.activeSelf != onConsumables) consumablesArea.SetActive(onConsumables);
        if (scrollRect != null)
        {
            if (scrollRect.gameObject.activeSelf == onConsumables) scrollRect.gameObject.SetActive(!onConsumables);
            if (scrollRect.verticalScrollbar != null && scrollRect.verticalScrollbar.gameObject.activeSelf == onConsumables)
                scrollRect.verticalScrollbar.gameObject.SetActive(!onConsumables);
        }
        if (!onConsumables || consumables == null) return;

        for (int c = 0; c < chipImages.Length; c++) chipImages[c].color = c == consumablesCategory ? tabActiveColor : tabInactiveColor;

        CollectCategoryItems(consumablesCategory, categoryItems);
        if (!categoryItems.Contains(selectedItemIn[consumablesCategory]))
            selectedItemIn[consumablesCategory] = categoryItems.Count > 0 ? categoryItems[0] : -1;
        int selected = selectedItemIn[consumablesCategory];

        // Tiles: only this category's are shown, laid out left to right, row by row, the grid centred.
        foreach (var pair in consumableTiles)
            if (pair.Value.rect.gameObject.activeSelf && !categoryItems.Contains(pair.Key)) pair.Value.rect.gameObject.SetActive(false);

        float viewWidth = tileScroll.GetComponent<RectTransform>().rect.width;
        if (viewWidth <= 1f) viewWidth = panelWidth - panelPadding * 2f;
        float usable = viewWidth - PixelUIKit.ScrollBarWidth - 8f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((usable + tileGap) / (tileWidth + tileGap)));
        float left = (usable - (columns * tileWidth + (columns - 1) * tileGap)) * 0.5f;

        for (int n = 0; n < categoryItems.Count; n++)
        {
            int item = categoryItems[n];
            if (!consumableTiles.TryGetValue(item, out ItemTile tile)) { tile = BuildTile(item); consumableTiles[item] = tile; }
            if (!tile.rect.gameObject.activeSelf) tile.rect.gameObject.SetActive(true);
            tile.rect.anchoredPosition = new Vector2(left + (n % columns) * (tileWidth + tileGap), -(4f + (n / columns) * (tileHeight + tileGap)));

            bool locked = ItemLocked(item);
            PackCost[] costs = consumables.ItemCosts(item);
            bool full = !locked && consumables.ItemRoom(item) <= 0;
            bool affordable = !locked && !full && CanAffordCosts(ScaleCosts(costs, 1));
            tile.background.color = item == selected ? tabActiveColor : rowColor;
            PixelUIKit.SetText(tile.price, full ? Coloured(unaffordableColor, potionFullText) : TilePrice(costs, locked ? lockedItemColor : affordable ? affordableColor : unaffordableColor));
            int owned = consumables.ItemOwned(item);
            PixelUIKit.SetText(tile.owned, owned > 0 ? "x" + PixelConsumables.OwnedText(owned) : "");
            tile.name.color = locked ? lockedItemColor : textColor;
            if (tile.lockMark.gameObject.activeSelf != locked) tile.lockMark.gameObject.SetActive(locked);
            if (tile.picture != null) tile.picture.image.color = locked ? new Color(0.35f, 0.35f, 0.4f, 1f) : Color.white;
        }
        int rowCount = (categoryItems.Count + columns - 1) / columns;
        float contentHeight = rowCount > 0 ? 8f + rowCount * tileHeight + (rowCount - 1) * tileGap : 0f;
        PixelUIKit.UpdateScrollView(tileScroll, tileScrollBar, contentHeight, tileScroll.GetComponent<RectTransform>().rect.height);
        if (tileEmptyLabel.gameObject.activeSelf != (categoryItems.Count == 0)) tileEmptyLabel.gameObject.SetActive(categoryItems.Count == 0);

        RefreshBuyBar(selected);
    }

    private void RefreshBuyBar(int item)
    {
        bool has = item >= 0;
        barMinus.interactable = barPlus.interactable = has;
        if (!barCountField.isFocused && barCountField.text != barCount.ToString())
            barCountField.SetTextWithoutNotify(barCount.ToString());

        if (!has)
        {
            PixelUIKit.SetText(barName, "");
            PixelUIKit.SetText(barDescription, noItemsText);
            PixelUIKit.SetText(barCost, "");
            SetBarBuy(false, lockedText);
            return;
        }

        PixelUIKit.SetText(barName, consumables.ItemName(item));
        PixelUIKit.SetText(barDescription, consumables.ItemDescription(item));

        // Potions are capped per type: never offer more than there is room for.
        bool locked = ItemLocked(item);
        int room = consumables.ItemRoom(item);
        int count = room > 0 ? Mathf.Min(barCount, room) : barCount;
        if (room <= 0 && !locked)
        {
            PixelUIKit.SetText(barCost, Coloured(unaffordableColor, string.Format(NoHave(potionFullFormat), consumables.ItemCapacity(item), PixelConsumables.OwnedText(consumables.ItemOwned(item)))));
            SetBarBuy(false, potionFullText);
            return;
        }
        if (count != barCount)
        {
            barCount = count;
            if (!barCountField.isFocused) barCountField.SetTextWithoutNotify(barCount.ToString());
        }

        if (locked)
        {
            int t = clicker.IndexOf(consumables.ItemRequiredType(item));
            string need = t >= 0 ? clicker.Tiers[t].displayName : consumables.ItemRequiredType(item).ToString();
            PixelUIKit.SetText(barCost, Coloured(unaffordableColor, string.Format(lockedRequirementFormat, need)));
            SetBarBuy(false, lockedText);
            return;
        }
        PackCost[] total = ScaleCosts(consumables.ItemCosts(item), barCount);
        PixelUIKit.SetText(barCost, BuildCostText(total, true));
        SetBarBuy(CanAffordCosts(total), string.Format(buyCountFormat, barCount));
    }

    private void SetBarBuy(bool canBuy, string label)
    {
        barBuy.interactable = canBuy;
        PixelUIKit.SetText(barBuyLabel, label);
        barBuyImage.color = canBuy ? buyColor : disabledColor;
    }
}
