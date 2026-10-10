using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop's Consumables tab: two "purchase cards" side by side - Potions and Utilities (devices). On each you pick an
/// item from a drop-down, set how many you want (type it, or use - and +), and press Buy. Items whose pixel type is not
/// unlocked yet are listed as locked (with what they need) instead of being hidden. Part of <see cref="PixelShop"/>;
/// the settings live in PixelShop.cs.
/// </summary>
public partial class PixelShop
{
    private class PurchaseCard
    {
        public bool utilities;
        public bool seeds;     // the Seeds card: lists only seed items
        public bool blank;     // a spare card with nothing on it yet
        public RectTransform rect;
        public TMP_Dropdown dropdown;
        public TMP_Text cost, buyLabel;
        public string tipText = "";   // the item's description, shown as a tooltip over the orange '!' box
        public TMP_InputField countField;
        public Button minus, plus, buy;
        public Image buyImage;
        public readonly List<int> items = new List<int>(); // consumables item index for each drop-down entry
        public string signature = "";
        public int count = 1;
    }

    private PurchaseCard potionCard, utilityCard, seedCard, spareCard;
    private GameObject consumablesArea;

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

        float width = (panelWidth - panelPadding * 2f - cardGap) * 0.5f;
        potionCard = BuildCard(consumablesArea.transform, false, potionsCardTitle, 0f, width, 0f);
        utilityCard = BuildCard(consumablesArea.transform, true, utilitiesCardTitle, width + cardGap, width, 0f);
        float row2 = cardRowHeight + cardGap;
        seedCard = BuildCard(consumablesArea.transform, false, seedsCardTitle, 0f, width, row2, seeds: true);
        spareCard = BuildCard(consumablesArea.transform, false, "", width + cardGap, width, row2, blank: true);
        consumablesArea.SetActive(false);
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private PurchaseCard BuildCard(Transform parent, bool utilities, string title, float x, float width, float top, bool seeds = false, bool blank = false)
    {
        PurchaseCard card = new PurchaseCard { utilities = utilities, seeds = seeds, blank = blank };

        GameObject go = new GameObject(blank ? "Spare Card" : seeds ? "Seeds Card" : utilities ? "Utilities Card" : "Potions Card", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(go.GetComponent<Image>());
        card.rect = go.GetComponent<RectTransform>();
        PlaceTopLeft(card.rect, x, top, width, cardRowHeight);
        if (blank) return card; // nothing on it yet

        float inner = width - 28f;
        float y = 8f;

        TMP_Text heading = CreateText(go.transform, "Title", title, nameFontSize * 1.1f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        heading.enableAutoSizing = true;
        heading.fontSizeMax = nameFontSize * 1.1f;
        heading.fontSizeMin = 14f;
        PixelUIKit.Caps(heading); // every title is shown in capitals
        PlaceTopLeft(heading.rectTransform, 14f, y, inner, 44f);
        RectTransform headingRect = heading.rectTransform;
        y += 44f + 6f;

        card.dropdown = PixelUIKit.CreateDropdown(font, go.transform, "Item Dropdown", new Vector2(inner, 58f), tabInactiveColor,
                                                  new Color(0.12f, 0.12f, 0.16f, 1f), textColor, buyFontSize * 0.85f);
        PlaceTopLeft(card.dropdown.GetComponent<RectTransform>(), 14f, y, inner, 58f);
        card.dropdown.onValueChanged.AddListener(_ => RefreshRows());
        AddCardPicture(card, headingRect, () =>
        {
            int it = SelectedItem(card);
            return it >= 0 && consumables != null ? "item:" + consumables.ItemName(it) : null;
        });
        y += 58f + 8f;

        // An orange box with an exclamation mark on the top left corner of the item's picture: hover it to read the item's description.
        float infoSize = 34f;
        RectTransform infoRect = CreateInfoBox(go.transform, infoSize, () => card.tipText);
        float pictureX = width - 14f - cardPictureSize;
        if (hidePictures) PlaceTopLeft(infoRect, width - 14f - infoSize, 8f, infoSize, infoSize);
        else PlaceTopLeft(infoRect, pictureX, 8f, infoSize, infoSize);   // flush with the picture box's top left corner
        infoRect.SetAsLastSibling();   // above the picture

        card.cost = CreateText(go.transform, "Cost", "", costFontSize, TextAlignmentOptions.Center, FontStyles.Normal);   // centred in the space beside the '!' box
        card.cost.richText = true;
        card.cost.enableAutoSizing = true;
        card.cost.fontSizeMax = costFontSize;
        card.cost.fontSizeMin = Mathf.Min(12f, costFontSize);
        PlaceTopLeft(card.cost.rectTransform, 14f, y, inner, 58f);   // the whole card width: the cost reads on one or two clean centred lines
        y += 58f + 8f;

        // How many: [-] [ 1 ] [+] - the whole group centred on the card.
        float cx = 14f + (inner - (52f + 8f + 120f + 8f + 52f)) * 0.5f;
        card.minus = CreateButton(go.transform, "Minus", "-", Vector2.zero, tabInactiveColor, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(card.minus.GetComponent<RectTransform>(), cx, y, 52f, 56f);
        card.minus.onClick.AddListener(() => ChangeCount(card, -1));
        cx += 52f + 8f;

        card.countField = PixelUIKit.CreateInputField(font, go.transform, "Count", new Vector2(120f, 56f), tabInactiveColor,
                                                      textColor, buyFontSize, "1");
        card.countField.contentType = TMP_InputField.ContentType.IntegerNumber;
        card.countField.text = "1";
        card.countField.textComponent.alignment = TextAlignmentOptions.Center;
        PlaceTopLeft(card.countField.GetComponent<RectTransform>(), cx, y, 120f, 56f);
        card.countField.onValueChanged.AddListener(s =>
        {
            card.count = int.TryParse(s, out int n) ? Mathf.Clamp(n, 1, maxPerPurchase) : 1; // live, so the cost updates while typing
            RefreshRows();
        });
        card.countField.onEndEdit.AddListener(_ => card.countField.text = card.count.ToString());
        cx += 120f + 8f;

        card.plus = CreateButton(go.transform, "Plus", "+", Vector2.zero, tabInactiveColor, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(card.plus.GetComponent<RectTransform>(), cx, y, 52f, 56f);
        card.plus.onClick.AddListener(() => ChangeCount(card, +1));
        y += 56f + 8f;

        card.buy = CreateButton(go.transform, "Buy", "", Vector2.zero, buyColor, textColor, buyFontSize, out card.buyLabel, out card.buyImage);
        PlaceTopLeft(card.buy.GetComponent<RectTransform>(), 14f, y, inner, 60f);
        card.buy.onClick.AddListener(() => BuySelected(card));
        return card;
    }

    // ------------------------------------------------------------------
    // Using the cards
    // ------------------------------------------------------------------

    private void ChangeCount(PurchaseCard card, int delta)
    {
        card.count = Mathf.Clamp(card.count + delta, 1, maxPerPurchase);
        card.countField.SetTextWithoutNotify(card.count.ToString());
        RefreshRows();
    }

    private int SelectedItem(PurchaseCard card)
    {
        int v = card.dropdown.value;
        return v >= 0 && v < card.items.Count ? card.items[v] : -1;
    }

    private void BuySelected(PurchaseCard card)
    {
        int item = SelectedItem(card);
        if (item >= 0) TryBuyItems(item, card.count);
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

    private void RefreshCard(PurchaseCard card)
    {
        // Which items belong on this card.
        List<int> list = new List<int>();
        for (int i = 0; i < consumables.ItemCount; i++)
        {
            if (consumables.ItemCraftOnly(i)) continue;             // combo potions can only be crafted
            bool seedItem = consumables.IsSeedItem(i);
            if (card.seeds) { if (seedItem && !consumables.IsDragonSeed(i)) list.Add(i); }          // the Seeds card: seeds only
            else if (!seedItem && consumables.IsDevice(i) == card.utilities) list.Add(i);
        }

        // Rebuild the drop-down's entries only when something changed, keeping the selection.
        StringBuilder sb = new StringBuilder();
        List<string> options = new List<string>();
        foreach (int i in list)
        {
            // Just the name; a locked item is shown in grey.
            string text = ItemLocked(i)
                ? "<color=#" + ColorUtility.ToHtmlStringRGB(lockedItemColor) + ">" + consumables.ItemName(i) + "</color>"
                : consumables.ItemName(i);
            options.Add(text);
            sb.Append(text).Append('|');
        }
        if (sb.ToString() != card.signature)
        {
            int previous = SelectedItem(card);
            card.signature = sb.ToString();
            card.items.Clear();
            card.items.AddRange(list);
            card.dropdown.ClearOptions();
            card.dropdown.AddOptions(options);
            int keep = card.items.IndexOf(previous);
            card.dropdown.SetValueWithoutNotify(keep >= 0 ? keep : 0);
            card.dropdown.RefreshShownValue();
        }

        int item = SelectedItem(card);
        bool has = item >= 0;
        card.dropdown.interactable = has;
        card.minus.interactable = card.plus.interactable = has;

        if (!card.countField.isFocused && card.countField.text != card.count.ToString())
            card.countField.SetTextWithoutNotify(card.count.ToString());

        if (!has)
        {
            card.tipText = noItemsText;
            card.cost.text = "";
            card.buy.interactable = false;
            card.buyLabel.text = lockedText;
            card.buyImage.color = disabledColor;
            return;
        }

        card.tipText = consumables.ItemDescription(item);

        // Potions are capped per type: never offer more than there is room for.
        int room = consumables.ItemRoom(item);
        if (room > 0 && card.count > room)
        {
            card.count = room;
            if (!card.countField.isFocused) card.countField.SetTextWithoutNotify(card.count.ToString());
        }
        if (room <= 0 && !ItemLocked(item))
        {
            card.cost.text = "<color=#" + ColorUtility.ToHtmlStringRGB(unaffordableColor) + ">" +
                             string.Format(NoHave(potionFullFormat), consumables.ItemCapacity(item), PixelConsumables.OwnedText(consumables.ItemOwned(item))) + "</color>";
            card.buy.interactable = false;
            card.buyLabel.text = potionFullText;
            card.buyImage.color = disabledColor;
            return;
        }

        bool locked = ItemLocked(item);
        PackCost[] total = ScaleCosts(consumables.ItemCosts(item), card.count);
        bool canBuy = !locked && CanAffordCosts(total);

        if (locked)
        {
            int t = clicker.IndexOf(consumables.ItemRequiredType(item));
            string need = t >= 0 ? clicker.Tiers[t].displayName : consumables.ItemRequiredType(item).ToString();
            card.cost.text = "<color=#" + ColorUtility.ToHtmlStringRGB(unaffordableColor) + ">" + string.Format(lockedRequirementFormat, need) + "</color>";
        }
        else card.cost.text = BuildCostText(total, true);

        card.buy.interactable = canBuy;
        card.buyLabel.text = locked ? lockedText : string.Format(buyCountFormat, card.count);
        card.buyImage.color = canBuy ? buyColor : disabledColor;
    }

    /// <summary>Called from RefreshRows: shows the cards on the Consumables tab (and hides the scrolling list there).</summary>
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

        RefreshCard(potionCard);
        RefreshCard(utilityCard);
        RefreshCard(seedCard);
    }
}
