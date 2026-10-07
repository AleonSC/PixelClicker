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
        public RectTransform rect;
        public TMP_Dropdown dropdown;
        public TMP_Text description, cost, buyLabel;
        public TMP_InputField countField;
        public Button minus, plus, buy;
        public Image buyImage;
        public readonly List<int> items = new List<int>(); // consumables item index for each drop-down entry
        public string signature = "";
        public int count = 1;
    }

    private PurchaseCard potionCard, utilityCard;
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
        potionCard = BuildCard(consumablesArea.transform, false, potionsCardTitle, 0f, width);
        utilityCard = BuildCard(consumablesArea.transform, true, utilitiesCardTitle, width + cardGap, width);
        consumablesArea.SetActive(false);
    }

    private static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    private PurchaseCard BuildCard(Transform parent, bool utilities, string title, float x, float width)
    {
        PurchaseCard card = new PurchaseCard { utilities = utilities };

        GameObject go = new GameObject(utilities ? "Utilities Card" : "Potions Card", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = rowColor;
        card.rect = go.GetComponent<RectTransform>();
        PlaceTopLeft(card.rect, x, 0f, width, cardHeight);

        float inner = width - 28f;
        float y = 12f;

        TMP_Text heading = CreateText(go.transform, "Title", title, nameFontSize * 1.1f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
        PlaceTopLeft(heading.rectTransform, 14f, y, inner, 46f);
        y += 46f + 8f;

        card.dropdown = PixelUIKit.CreateDropdown(font, go.transform, "Item Dropdown", new Vector2(inner, 60f), tabInactiveColor,
                                                  new Color(0.12f, 0.12f, 0.16f, 1f), textColor, buyFontSize * 0.9f);
        PlaceTopLeft(card.dropdown.GetComponent<RectTransform>(), 14f, y, inner, 60f);
        card.dropdown.onValueChanged.AddListener(_ => RefreshRows());
        y += 60f + 10f;

        card.description = CreateText(go.transform, "Description", "", descriptionFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        card.description.color = new Color(textColor.r, textColor.g, textColor.b, 0.8f);
        card.description.enableAutoSizing = true;
        card.description.fontSizeMax = descriptionFontSize;
        card.description.fontSizeMin = Mathf.Min(12f, descriptionFontSize);
        PlaceTopLeft(card.description.rectTransform, 14f, y, inner, 84f);
        y += 84f + 6f;

        card.cost = CreateText(go.transform, "Cost", "", costFontSize, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        card.cost.richText = true;
        card.cost.enableAutoSizing = true;
        card.cost.fontSizeMax = costFontSize;
        card.cost.fontSizeMin = Mathf.Min(14f, costFontSize);
        PlaceTopLeft(card.cost.rectTransform, 14f, y, inner, 62f);
        y += 62f + 8f;

        // How many: [-] [ 1 ] [+]
        float cx = 14f;
        card.minus = CreateButton(go.transform, "Minus", "-", Vector2.zero, tabInactiveColor, textColor, buyFontSize, out _, out _);
        PlaceTopLeft(card.minus.GetComponent<RectTransform>(), cx, y, 56f, 56f);
        card.minus.onClick.AddListener(() => ChangeCount(card, -1));
        cx += 56f + 8f;

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
        PlaceTopLeft(card.plus.GetComponent<RectTransform>(), cx, y, 56f, 56f);
        card.plus.onClick.AddListener(() => ChangeCount(card, +1));
        y += 56f + 10f;

        card.buy = CreateButton(go.transform, "Buy", "", Vector2.zero, buyColor, textColor, buyFontSize, out card.buyLabel, out card.buyImage);
        PlaceTopLeft(card.buy.GetComponent<RectTransform>(), 14f, y, inner, 64f);
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
            if (consumables.IsDevice(i) == card.utilities) list.Add(i);
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
            card.description.text = noItemsText;
            card.cost.text = "";
            card.buy.interactable = false;
            card.buyLabel.text = lockedText;
            card.buyImage.color = disabledColor;
            return;
        }

        card.description.text = consumables.ItemDescription(item);

        bool locked = ItemLocked(item);
        PackCost[] total = ScaleCosts(consumables.ItemCosts(item), card.count);
        bool canBuy = !locked && CanAffordCosts(total);

        if (locked)
        {
            int t = clicker.IndexOf(consumables.ItemRequiredType(item));
            string need = t >= 0 ? clicker.Tiers[t].displayName : consumables.ItemRequiredType(item).ToString();
            card.cost.text = "<color=#" + ColorUtility.ToHtmlStringRGB(unaffordableColor) + ">" + string.Format(lockedRequirementFormat, need) + "</color>";
        }
        else card.cost.text = BuildCostText(total);

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
    }
}
