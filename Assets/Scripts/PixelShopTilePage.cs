// ---- System notes ----
// PixelShopTilePage.cs
//   Part of partial class PixelShop: the shop's tile page (one, the CATALOG: PixelShopCatalog.cs fills it). Since 2026-10-24 (the 'A' redesign) the
//   whole shop is this one page: the category rail picks what it lists (page.chip = the rail entry; no chip buttons), the search box lists matches.
//   - a scrolling grid of tiles (tileWidth x tileHeight, 4 to a row at the defaults): still 3D picture, the NAME, the price as colour swatches +
//     numbers (CostStrip: SetCosts / SetCostMessage; red number = can't afford), a small corner badge, a 'Locked' mark over the picture and a green
//     bar along the bottom when you can buy it now. Click a tile = select it, Shift-click = quick buy, hover = its description as the shop tooltip.
//   - the DETAIL PANEL on the right for the selected tile (BuildDetailPanel, replaced the bottom buy bar): spinning picture, name, where it is listed,
//     the full description, the cost with what you hold ('75 White / 12.4K'), an amount row (- count +) and quick amounts (x1 x5 x10 MAX) for
//     things bought in numbers (consumables, Value levels), 'Save up for this' (the Saving up for card, PixelShopCatalog.cs) and Buy.
//   A page only says WHAT to show through its delegates (collect, tileName, pictureKey, description, fillTile, fillBar, purchase, quickBuy, hasAmount,
//   maxAmount); building, layout, selection and the shared tooltip live here. Tiles are built once per id and reused.
//   Settings: PixelShop.cs "Consumables Tab" (tileWidth / Height / Gap, tileFontSize, buyMaxTip) and "Shop layout" (rail, detail panel, amounts).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Part of <see cref="PixelShop"/>: the shop's catalog page - a grid of tiles and a detail panel for the selected one.
/// What each tile and the panel show comes from PixelShopCatalog.cs.
/// </summary>
public partial class PixelShop
{
    /// <summary>One tile in the grid.</summary>
    private class ItemTile
    {
        public RectTransform rect;
        public Image background;
        public PictureSlot picture;
        public Func<string> key;   // the tile's picture (also shown, spinning, in the detail panel while selected)
        public TMP_Text name, badge, lockMark;
        public CostStrip costs;    // the price: a colour swatch + number per currency
        public GameObject canBuyBar;   // the green bar along the bottom: you can buy it right now
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
        public bool withNames;    // "75 White" instead of "75" (the detail panel has room)
        public bool withHeld;     // "... / 12.4K": what you hold of it (the detail panel)
        public float fontSize;
    }

    /// <summary>The catalog: a grid + detail panel. The delegates say what it shows; everything else is shared.</summary>
    private class TilePage
    {
        public GameObject area;
        public int chip;                 // the rail entry that is open (or the search entry)
        public int[] selected;           // the selected id on each entry (-1 = none)
        public ScrollRect scroll;
        public RectTransform content, gridRect;
        public GameObject scrollBar;
        public TMP_Text empty;
        public readonly Dictionary<int, ItemTile> tiles = new Dictionary<int, ItemTile>();
        public readonly List<int> ids = new List<int>();

        // The detail panel.
        public TMP_Text barName, barKind, barDescription, barBuyLabel, pinLabel;
        public CostStrip barCosts;
        public TMP_InputField countField;
        public Button minus, plus, buy, pin;
        public Button[] amountButtons;
        public GameObject amountRow, quickRow;
        public Image buyImage, pinImage;
        public int count = 1;

        // What the page shows (set by PixelShopCatalog.cs).
        public Action<int, List<int>> collect;       // entry -> the ids listed on it, in order
        public Func<int, string> tileName;
        public Func<int, Func<string>> pictureKey;   // id -> its picture key (null = no picture)
        public Func<int, string> description;
        public Func<int, string> kindLine;           // id -> where it is listed ('Consumables > Potions')
        public Action<int, ItemTile, bool> fillTile; // id, tile, selected: price line, badge, lock
        public Func<int, bool> canBuyNow;            // id -> can it be bought right now (the green bar, 'Only what I can buy')
        public Action<int> fillBar;                  // id: cost line and Buy (SetPageBuy), count clamp
        public Action<int, bool> purchase;           // Buy pressed: id, Shift held ('as many as you can')
        public Action<int> quickBuy;                 // Shift-click on a tile
        public Func<int, bool> hasAmount;            // id -> bought in numbers (the amount rows show)
        public Func<int, int> maxAmount;             // id -> the MAX button's amount
        public Func<int, bool> canPin;               // id -> 'Save up for this' makes sense (not owned)

        public int Selected => selected[chip];
    }

    private TilePage catalog;

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds the (hidden) catalog page in the window area x..x+gridW (the grid) and the detail panel right of it, from 'top' down to the
    /// window's bottom padding. 'entries' = how many rail entries remember a selection. The caller (PixelShopCatalog.cs) sets the delegates.
    /// </summary>
    private TilePage BuildCatalogPage(Transform panel, float x, float top, float gridTop, float gridW, int entries)
    {
        TilePage page = new TilePage { chip = 0, selected = new int[Mathf.Max(1, entries)] };
        for (int i = 0; i < page.selected.Length; i++) page.selected[i] = -1;

        page.area = new GameObject("Catalog", typeof(RectTransform));
        page.area.transform.SetParent(panel, false);
        RectTransform ar = page.area.GetComponent<RectTransform>();
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(x, panelPadding);
        ar.offsetMax = new Vector2(-panelPadding, -top);

        // The tile grid: the left part, below the Saving up card and the heading row.
        page.scroll = PixelUIKit.CreateScrollView(page.area.transform, "Tile Grid", tabActiveColor, PixelUIKit.ScrollBarWidth,
                                                  scrollSpeed, out page.content, out page.scrollBar);
        page.gridRect = page.scroll.GetComponent<RectTransform>();
        page.gridRect.anchorMin = new Vector2(0f, 0f);
        page.gridRect.anchorMax = new Vector2(0f, 1f);
        page.gridRect.pivot = new Vector2(0f, 1f);
        page.gridRect.sizeDelta = new Vector2(gridW, -(gridTop - top));
        page.gridRect.anchoredPosition = new Vector2(0f, -(gridTop - top));

        page.empty = CreateText(page.scroll.transform, "Empty", noItemsText, costFontSize, TextAlignmentOptions.Center, FontStyles.Italic);
        PixelUIKit.Stretch(page.empty.rectTransform);
        page.empty.gameObject.SetActive(false);

        BuildDetailPanel(page, gridW + DetailGap);
        page.area.SetActive(false);
        return page;
    }

    private float DetailGap => 16f;

    /// <summary>The detail panel on the right: picture, name, where it is listed, description, cost (with what you hold), amounts, Save up and Buy.</summary>
    private void BuildDetailPanel(TilePage page, float x)
    {
        GameObject panel = new GameObject("Detail Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(page.area.transform, false);
        panel.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(panel.GetComponent<Image>());
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.offsetMin = new Vector2(x, 0f);
        pr.offsetMax = Vector2.zero;
        float w = DetailWidth;
        // (the picture, name and kind hang from the top edge; the cost, amounts, Save up and Buy stand on the bottom edge; the description
        //  stretches between them - so the panel works at any window height)
        RectTransform top = pr;

        const float pad = 16f;
        float inner = w - pad * 2f;
        float pic = Mathf.Min(150f, inner);
        if (!hidePictures) AddPictureSlot(top, pic, new Vector2((w - pic) * 0.5f, pad), false, () => SelectedTileKey(page), false);
        float y = pad + pic + 8f;

        page.barName = CreateText(top, "Name", "", nameFontSize * 0.75f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        page.barName.enableAutoSizing = true;
        page.barName.fontSizeMax = nameFontSize * 0.75f;
        page.barName.fontSizeMin = 14f;
        PixelUIKit.Caps(page.barName);
        PlaceTopLeft(page.barName.rectTransform, pad, y, inner, 40f);
        y += 40f;
        page.barKind = CreateText(top, "Kind", "", tileFontSize * 0.9f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        page.barKind.color = levelColor;
        PixelUIKit.FitOneLine(page.barKind, 11f);
        PlaceTopLeft(page.barKind.rectTransform, pad, y, inner, 24f);
        y += 30f;

        // Bottom up (distances from the bottom edge): Buy, Save up for this, quick amounts, - count +, the cost. The description takes what is left between.
        float buyH = 62f, pinH = 36f, quickH = 44f, stepH = 48f, costH = 92f, gap = 8f;
        float buyB = pad;
        float pinB = buyB + buyH + gap;
        float quickB = pinB + pinH + gap;
        float stepB = quickB + quickH + gap;
        float costB = stepB + stepH + gap;

        page.barDescription = CreateText(top, "Description", "", tileFontSize * 1.05f, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        page.barDescription.richText = true;
        page.barDescription.enableAutoSizing = true;
        page.barDescription.fontSizeMax = tileFontSize * 1.05f;
        page.barDescription.fontSizeMin = 12f;
        page.barDescription.color = new Color(0.78f, 0.82f, 0.9f, 1f);
        RectTransform dr = page.barDescription.rectTransform;
        dr.anchorMin = Vector2.zero;
        dr.anchorMax = Vector2.one;
        dr.pivot = new Vector2(0f, 1f);
        dr.offsetMin = new Vector2(pad, costB + costH + gap);
        dr.offsetMax = new Vector2(-pad, -y);

        page.barCosts = BuildCostStrip(top, pad, 0f, inner, costH, 6, 2, tileFontSize * 0.95f, true);
        PlaceBottomLeft(page.barCosts.root, pad, costB, inner, costH);
        page.barCosts.withHeld = true;

        // - count +
        page.amountRow = new GameObject("Amount", typeof(RectTransform)).gameObject;
        RectTransform amr = page.amountRow.GetComponent<RectTransform>();
        amr.SetParent(top, false);
        PlaceBottomLeft(amr, pad, stepB, inner, stepH);
        float stepW = 56f, countW = inner - stepW * 2f - 16f;
        page.minus = CreateButton(amr, "Minus", "-", Vector2.zero, ButtonRole.Neutral, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(page.minus.GetComponent<RectTransform>(), 0f, 0f, stepW, stepH);
        page.minus.onClick.AddListener(() => ChangePageCount(page, -1));
        page.countField = PixelUIKit.CreateInputField(font, amr, "Count", new Vector2(countW, stepH), tabInactiveColor, textColor, buyFontSize * 0.85f, "1");
        page.countField.contentType = TMP_InputField.ContentType.IntegerNumber;
        page.countField.text = "1";
        page.countField.textComponent.alignment = TextAlignmentOptions.Center;
        PlaceTopLeft(page.countField.GetComponent<RectTransform>(), stepW + 8f, 0f, countW, stepH);
        page.countField.onValueChanged.AddListener(s =>
        {
            page.count = int.TryParse(s, out int n) ? Mathf.Clamp(n, 1, maxPerPurchase) : 1;   // live, so the cost updates while typing
            RefreshRows();
        });
        page.countField.onEndEdit.AddListener(_ => page.countField.text = page.count.ToString());
        page.plus = CreateButton(amr, "Plus", "+", Vector2.zero, ButtonRole.Neutral, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(page.plus.GetComponent<RectTransform>(), inner - stepW, 0f, stepW, stepH);
        page.plus.onClick.AddListener(() => ChangePageCount(page, +1));

        // Quick amounts: x1 x5 x10 ... and MAX (as many as you can afford and hold).
        page.quickRow = new GameObject("Quick Amounts", typeof(RectTransform)).gameObject;
        RectTransform qr = page.quickRow.GetComponent<RectTransform>();
        qr.SetParent(top, false);
        PlaceBottomLeft(qr, pad, quickB, inner, quickH);
        int[] presets = AmountPresets;
        int buttons = presets.Length + 1;
        float qw = (inner - gap * (buttons - 1)) / buttons;
        page.amountButtons = new Button[buttons];
        for (int b = 0; b < buttons; b++)
        {
            bool max = b == presets.Length;
            int amount = max ? 0 : presets[b];
            Button q = CreateButton(qr, max ? "Max" : "x" + amount, max ? amountMaxText : "x" + amount, Vector2.zero, ButtonRole.Neutral, textColor, buyFontSize * 0.6f, out TMP_Text ql, out _);
            PixelUIKit.Caps(ql);
            PlaceTopLeft(q.GetComponent<RectTransform>(), b * (qw + gap), 0f, qw, quickH);
            q.onClick.AddListener(() =>
            {
                int id = page.Selected;
                if (id < 0) return;
                SetPageCount(page, max && page.maxAmount != null ? Mathf.Max(1, page.maxAmount(id)) : amount);
            });
            page.amountButtons[b] = q;
        }

        // Save up for this: pins the item to the Saving up for card.
        page.pin = CreateButton(top, "Save Up", "", Vector2.zero, ButtonRole.Neutral, textColor, buyFontSize * 0.55f, out page.pinLabel, out page.pinImage);
        PlaceBottomLeft(page.pin.GetComponent<RectTransform>(), pad, pinB, inner, pinH);
        page.pin.onClick.AddListener(() => { int id = page.Selected; if (id >= 0) ToggleSavingGoal(id); });

        page.buy = CreateButton(top, "Buy", "", Vector2.zero, ButtonRole.Success, textColor, buyFontSize * 0.85f, out page.barBuyLabel, out page.buyImage);
        PixelUIKit.FitOneLine(page.barBuyLabel, 14f);
        PlaceBottomLeft(page.buy.GetComponent<RectTransform>(), pad, buyB, inner, buyH);
        page.buy.onClick.AddListener(() => { int id = page.Selected; if (id >= 0) page.purchase(id, PixelInput.ShiftHeld()); });
        Func<string> maxTip = () => buyMaxTip;   // hovering Buy explains Shift-click
        PixelHoverTip buyTip = page.buy.gameObject.AddComponent<PixelHoverTip>();
        buyTip.onEnter = () => { int id = page.Selected; if (id >= 0 && page.hasAmount != null && page.hasAmount(id)) hoverTip = maxTip; };
        buyTip.onExit = () => { if (hoverTip == maxTip) hoverTip = null; };
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    /// <summary>Places a box 'y' units up from its parent's bottom left corner.</summary>
    private static void PlaceBottomLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static string SelectedTileKey(TilePage page)
    {
        int id = page.Selected;
        return id >= 0 && page.tiles.TryGetValue(id, out ItemTile tile) && tile.key != null ? tile.key() : null;
    }

    /// <summary>A tile: still picture on top, the name, the price line, a badge in the top right corner, a 'Locked' mark and the can-buy bar.</summary>
    private ItemTile BuildTile(TilePage page, int id)
    {
        GameObject go = new GameObject("Tile " + id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(page.content, false);
        ItemTile tile = new ItemTile { rect = go.GetComponent<RectTransform>(), background = go.GetComponent<Image>(), key = page.pictureKey(id) };
        tile.background.color = rowColor;
        PixelUIKit.StyleBox(tile.background);
        tile.rect.anchorMin = tile.rect.anchorMax = tile.rect.pivot = new Vector2(0f, 1f);
        tile.rect.sizeDelta = new Vector2(tileWidth, tileHeight);

        float lineH = tileFontSize * 1.3f;
        float pic = Mathf.Max(40f, Mathf.Min(tileWidth - 36f, tileHeight - lineH * 2f - 26f));
        float y = 8f;
        if (!hidePictures && tile.key != null)
            tile.picture = AddPictureSlot(tile.rect, pic, new Vector2((tileWidth - pic) * 0.5f, y), false, tile.key, true, page.scroll.viewport, true);
        y += pic + 4f;

        tile.name = CreateText(go.transform, "Name", page.tileName(id), tileFontSize * 0.85f, TextAlignmentOptions.Center, FontStyles.Bold);
        PixelUIKit.FitOneLine(tile.name, 11f);
        tile.name.raycastTarget = false;
        PlaceTopLeft(tile.name.rectTransform, 6f, y, tileWidth - 12f, lineH);
        y += lineH;

        tile.costs = BuildCostStrip(go.transform, 6f, y, tileWidth - 12f, Mathf.Max(lineH, tileHeight - y - 10f), 3, 3, tileFontSize * 0.9f, false);

        tile.badge = CreateText(go.transform, "Badge", "", tileFontSize * 0.8f, TextAlignmentOptions.TopRight, FontStyles.Bold);
        tile.badge.richText = true;
        tile.badge.raycastTarget = false;
        PlaceTopLeft(tile.badge.rectTransform, 6f, 6f, tileWidth - 12f, lineH);

        tile.lockMark = CreateText(go.transform, "Locked", lockedText, tileFontSize * 0.85f, TextAlignmentOptions.Center, FontStyles.Bold);
        tile.lockMark.color = unaffordableColor;
        tile.lockMark.raycastTarget = false;
        PlaceTopLeft(tile.lockMark.rectTransform, 6f, 8f + pic * 0.5f - lineH * 0.5f, tileWidth - 12f, lineH);
        tile.lockMark.gameObject.SetActive(false);

        // The can-buy bar: a thin green line along the bottom edge.
        GameObject bar = new GameObject("Can Buy", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(go.transform, false);
        Image bi = bar.GetComponent<Image>();
        bi.color = canBuyColor;
        bi.raycastTarget = false;
        RectTransform brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.offsetMin = new Vector2(10f, 3f);
        brt.offsetMax = new Vector2(-10f, 7f);
        tile.canBuyBar = bar;
        bar.SetActive(false);

        PixelUIKit.MakeClickable(go, () => ClickTile(page, id), true, false);
        Func<string> tip = () => "<b>" + page.tileName(id) + "</b>\n" + page.description(id);
        PixelHoverTip hover = go.AddComponent<PixelHoverTip>();
        hover.onEnter = () => hoverTip = tip;
        hover.onExit = () => { if (hoverTip == tip) hoverTip = null; };
        return tile;
    }

    // ------------------------------------------------------------------
    // Using the page
    // ------------------------------------------------------------------

    private void ClickTile(TilePage page, int id)
    {
        if (page.Selected != id) SetPageCount(page, 1, false);   // a new item starts at x1
        page.selected[page.chip] = id;
        if (PixelInput.ShiftHeld() && page.quickBuy != null) page.quickBuy(id);
        RefreshRows();
    }

    private void ChangePageCount(TilePage page, int delta) => SetPageCount(page, page.count + delta);

    private void SetPageCount(TilePage page, int count, bool refresh = true)
    {
        page.count = Mathf.Clamp(count, 1, maxPerPurchase);
        if (page.countField != null) page.countField.SetTextWithoutNotify(page.count.ToString());
        if (refresh) RefreshRows();
    }

    /// <summary>Sets the detail panel's Buy button (called from fillBar).</summary>
    private void SetPageBuy(TilePage page, bool canBuy, string label)
    {
        page.buy.interactable = canBuy;
        PixelUIKit.SetText(page.barBuyLabel, label);
        SetShopRole(page.buyImage, canBuy ? ButtonRole.Success : ButtonRole.Neutral);
    }

    /// <summary>Sets a tile's corner badge and lock mark (called from fillTile; the price is SetCosts / SetCostMessage on tile.costs).</summary>
    private static void SetTile(ItemTile tile, string badge, bool locked)
    {
        PixelUIKit.SetText(tile.badge, badge);
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
            strip.numbers[i].richText = true;
            PixelUIKit.FitOneLine(strip.numbers[i], 11f);
            strip.numbers[i].raycastTarget = false;
            sw.SetActive(false);
            strip.numbers[i].gameObject.SetActive(false);
        }
        strip.message = CreateText(strip.root, "Message", "", fontSize, TextAlignmentOptions.Center, FontStyles.Bold);
        strip.message.richText = true;
        strip.message.enableAutoSizing = true;
        strip.message.fontSizeMax = fontSize;
        strip.message.fontSizeMin = 12f;
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
        float cellW = size.x / columns, cellH = Mathf.Min(size.y / rows, strip.fontSize * 1.6f);
        float topY = (size.y - cellH * rows) * 0.5f;   // (fewer rows than room: centred in the strip)
        float swatch = Mathf.Min(cellH * 0.7f, strip.fontSize * 0.9f);
        for (int i = 0; i < slots; i++)
        {
            bool on = i < shown;
            if (strip.swatches[i].gameObject.activeSelf != on) strip.swatches[i].gameObject.SetActive(on);
            if (strip.numbers[i].gameObject.activeSelf != on) strip.numbers[i].gameObject.SetActive(on);
            if (!on) continue;

            PackCost cost = costs[i];
            int t = string.IsNullOrEmpty(cost.minigameCurrency) ? clicker.IndexOf(cost.type) : -1;
            strip.swatches[i].color = t >= 0 ? clicker.Tiers[t].UIColor : PixelMaterials.CostColor(cost.minigameCurrency);   // (materials: their colour; minigame counters: grey)
            string text = PixelClicker.FormatNumberShort(cost.amount);
            if (strip.withNames) text += " " + ShortCostName(cost);
            if (strip.withHeld) text += " <color=#9AA3BD>/ " + PixelClicker.FormatNumberShort(CostHeld(cost)) + "</color>";
            if (i == shown - 1 && costs.Length > shown) text += " +" + (costs.Length - shown);   // (more than fit: '+n')
            TMP_Text number = strip.numbers[i];
            PixelUIKit.SetText(number, text);
            number.color = locked ? lockedItemColor : CanAffordCost(cost) ? textColor : unaffordableColor;

            // Place swatch + number as a pair in its cell (the last row is centred on its own; the detail panel's list is left aligned).
            int row = i / columns, col = i % columns;
            int inRow = row == rows - 1 ? shown - row * columns : columns;
            float rowLeft = strip.withHeld ? 0f : (size.x - inRow * cellW) * 0.5f;
            float numberW = Mathf.Min(cellW - swatch - 6f, number.GetPreferredValues(text).x + 2f);
            float pairX = strip.withHeld ? rowLeft + col * cellW : rowLeft + col * cellW + (cellW - (swatch + 4f + numberW)) * 0.5f;
            float midY = topY + row * cellH + cellH * 0.5f;
            PlaceTopLeft((RectTransform)strip.swatches[i].transform, pairX, midY - swatch * 0.5f, swatch, swatch);
            PlaceTopLeft(number.rectTransform, pairX + swatch + 4f, midY - cellH * 0.5f, Mathf.Max(10f, numberW), cellH);
        }
    }

    /// <summary>How much the player holds of one line of a price (a pixel type, a material or a minigame counter).</summary>
    private double CostHeld(PackCost cost)
    {
        if (!string.IsNullOrEmpty(cost.minigameCurrency)) return PixelMaterials.CostHeld(cost.minigameCurrency);
        int t = clicker.IndexOf(cost.type);
        return t >= 0 ? clicker.Tiers[t].count : 0d;
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

    /// <summary>Called from RefreshRows: refreshes the catalog and hides the old scrolling list (the catalog always replaces it).</summary>
    private void RefreshTilePages(bool searching)
    {
        if (catalog == null) return;
        if (!catalog.area.activeSelf) catalog.area.SetActive(true);
        if (scrollRect != null)
        {
            if (scrollRect.gameObject.activeSelf) scrollRect.gameObject.SetActive(false);
            if (scrollRect.verticalScrollbar != null && scrollRect.verticalScrollbar.gameObject.activeSelf) scrollRect.verticalScrollbar.gameObject.SetActive(false);
        }
        if (panelObject != null && !panelObject.activeInHierarchy) return;   // (closed: nothing to draw)
        catalog.chip = searching ? SearchEntry : railEntry;
        RefreshCatalogChrome(searching);
        RefreshTilePage(catalog);
    }

    private void RefreshTilePage(TilePage page)
    {
        page.collect(page.chip, page.ids);
        if (onlyAffordable && page.canBuyNow != null) page.ids.RemoveAll(id => !page.canBuyNow(id));
        if (!page.ids.Contains(page.Selected)) page.selected[page.chip] = page.ids.Count > 0 ? page.ids[0] : -1;
        int selected = page.Selected;

        // Tiles: only this entry's are shown, laid out left to right, row by row, the grid centred.
        foreach (var pair in page.tiles)
            if (pair.Value.rect.gameObject.activeSelf && !page.ids.Contains(pair.Key)) pair.Value.rect.gameObject.SetActive(false);

        RectTransform view = page.gridRect;
        float viewWidth = view.rect.width > 1f ? view.rect.width : CatalogGridWidth;
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
            PixelUIKit.SetText(tile.name, page.tileName(id));
            page.fillTile(id, tile, id == selected);
            bool can = page.canBuyNow != null && page.canBuyNow(id);
            if (tile.canBuyBar.activeSelf != can) tile.canBuyBar.SetActive(can);
        }
        int rowCount = (page.ids.Count + columns - 1) / columns;
        float contentHeight = rowCount > 0 ? 8f + rowCount * tileHeight + (rowCount - 1) * tileGap : 0f;
        PixelUIKit.UpdateScrollView(page.scroll, page.scrollBar, contentHeight, view.rect.height);
        if (page.empty.gameObject.activeSelf != (page.ids.Count == 0)) page.empty.gameObject.SetActive(page.ids.Count == 0);

        RefreshDetailPanel(page, selected);
    }

    /// <summary>The detail panel for the selected id (or a blank one).</summary>
    private void RefreshDetailPanel(TilePage page, int selected)
    {
        bool has = selected >= 0;
        bool amounts = has && page.hasAmount != null && page.hasAmount(selected);
        if (page.amountRow.activeSelf != amounts) page.amountRow.SetActive(amounts);
        if (page.quickRow.activeSelf != amounts) page.quickRow.SetActive(amounts);
        if (!amounts && page.count != 1) SetPageCount(page, 1, false);

        bool pinnable = has && page.canPin != null && page.canPin(selected);
        if (page.pin.gameObject.activeSelf != pinnable) page.pin.gameObject.SetActive(pinnable);
        if (pinnable)
        {
            bool pinned = IsSavingGoal(selected);
            PixelUIKit.SetText(page.pinLabel, pinned ? savingThisText : saveForThisText);
            SetShopRole(page.pinImage, pinned ? ButtonRole.Selected : ButtonRole.Neutral);
        }

        PixelUIKit.SetText(page.barName, has ? page.tileName(selected) : "");
        PixelUIKit.SetText(page.barKind, has && page.kindLine != null ? page.kindLine(selected) : "");
        PixelUIKit.SetText(page.barDescription, has ? page.description(selected) : noItemsText);
        if (!has)
        {
            SetCostMessage(page.barCosts, "", textColor);
            SetPageBuy(page, false, lockedText);
            return;
        }
        page.fillBar(selected);
        if (amounts && !page.countField.isFocused && page.countField.text != page.count.ToString())
            page.countField.SetTextWithoutNotify(page.count.ToString());   // (fillBar may have clamped the count)
    }
}
