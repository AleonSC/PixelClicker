// ---- System notes ----
// PixelShopCurrency.cs
//   Part of partial class PixelShop: the CURRENCY STRIP under the shop's header (since 2026-10-24; it replaced the slide-out Currency panel on the
//   shop's left). One chip per pixel type you have unlocked or hold: a small spinning cube in its colour + the amount (abbreviated), left to right;
//   when there are more than fit, the mouse wheel over the strip scrolls it sideways. Hover a chip: the pixel's name and the full amount.
//   Also: CloseShopWindow, and the shop closing the Inventory / Log while it is open (and bringing them back after). Settings: PixelShop.cs
//   "Currency strip" (hideCurrencyPanel, currencyStripHeight).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop's currency strip: every pixel type you hold, as a small spinning cube and the amount, always visible under the header.
/// Part of <see cref="PixelShop"/>; its Inspector settings are in PixelShop.cs ("Currency strip").
/// </summary>
public partial class PixelShop
{
    private class CurrencyChip
    {
        public RectTransform rect;
        public PixelCubeIcon icon;
        public TMP_Text amount;
    }

    /// <summary>Closes the shop window (the Inventory and Log call this when they open: the windows never show together).</summary>
    public static void CloseShopWindow()
    {
        PixelShop shop = PixelFind.First<PixelShop>();
        if (shop != null && shop.panelObject != null && PixelPop.IsOpen(shop.panelObject)) PixelPop.Hide(shop.panelObject);
    }

    private GameObject currencyStrip;
    private RectTransform currencyContent;
    private ScrollRect currencyScroll;
    private CurrencyChip[] currencyChips;
    private RectTransform currencyTipRect;
    private string currencyTipShown;   // (the tip text last measured, so the box is only measured again when it changes)
    private bool currencyTipWasCard;
    private Vector2 currencyTipSize;
    private TMP_Text currencyTipText;
    private RectTransform currencyCanvasRect;
    private int currencyHover = -1;
    private bool shopWasOpen;
    private bool reopenInventory, reopenLog; // which windows the shop closed when it opened, to bring back when it closes

    private float CurrencyStripHeight => Mathf.Max(30f, currencyStripHeight);
    private float CurrencyIconSize => CurrencyStripHeight * 0.55f;
    private float CurrencyFontSize => CurrencyStripHeight * 0.46f;

    /// <summary>The strip under the header: a sideways-scrolling row of chips (cube + amount), one per pixel type.</summary>
    private void BuildCurrencyStrip(Transform shopPanel)
    {
        if (clicker == null) return;
        currencyCanvasRect = canvasRoot.GetComponent<RectTransform>();
        BuildCurrencyTooltip();
        if (hideCurrencyPanel) return;

        currencyStrip = new GameObject("Currency Strip", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        currencyStrip.transform.SetParent(shopPanel, false);
        Image bg = currencyStrip.GetComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        PixelUIKit.StyleBox(bg, true);
        PlaceTopLeft(currencyStrip.GetComponent<RectTransform>(), panelPadding, headerHeight + 8f, panelWidth - panelPadding * 2f, CurrencyStripHeight);

        currencyContent = new GameObject("Chips", typeof(RectTransform)).GetComponent<RectTransform>();
        currencyContent.SetParent(currencyStrip.transform, false);
        currencyContent.anchorMin = currencyContent.anchorMax = currencyContent.pivot = new Vector2(0f, 0.5f);
        currencyContent.sizeDelta = new Vector2(0f, CurrencyStripHeight);
        currencyScroll = currencyStrip.GetComponent<ScrollRect>();
        currencyScroll.content = currencyContent;
        currencyScroll.viewport = currencyStrip.GetComponent<RectTransform>();
        currencyScroll.horizontal = true;
        currencyScroll.vertical = false;   // (the mouse wheel then scrolls it sideways)
        currencyScroll.movementType = ScrollRect.MovementType.Clamped;
        currencyScroll.scrollSensitivity = Mathf.Max(10f, scrollSpeed);

        var tiers = clicker.Tiers;
        currencyChips = new CurrencyChip[tiers.Length];
        float icon = CurrencyIconSize;
        for (int i = 0; i < tiers.Length; i++)
        {
            CurrencyChip chip = new CurrencyChip();
            GameObject go = new GameObject("Currency " + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(currencyContent, false);
            Image cb = go.GetComponent<Image>();
            cb.color = rowColor;
            PixelUIKit.StyleBox(cb);
            chip.rect = go.GetComponent<RectTransform>();
            chip.rect.anchorMin = chip.rect.anchorMax = chip.rect.pivot = new Vector2(0f, 0.5f);

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
            iconGo.transform.SetParent(go.transform, false);
            chip.icon = iconGo.AddComponent<PixelCubeIcon>();
            chip.icon.spinDegreesPerSecond = 60f + (i % 5) * 8f; // not all in step
            chip.icon.raycastTarget = false;
            RectTransform ir = chip.icon.rectTransform;
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(icon, icon);
            ir.anchoredPosition = new Vector2(8f, 0f);

            chip.amount = CreateText(go.transform, "Amount", "", CurrencyFontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            chip.amount.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
            chip.amount.textWrappingMode = TextWrappingModes.NoWrap;
#else
            chip.amount.enableWordWrapping = false;
#endif
            chip.amount.raycastTarget = false;
            RectTransform ar = chip.amount.rectTransform;
            ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0f, 0.5f);
            ar.anchoredPosition = new Vector2(icon + 14f, 0f);

            // Hover the chip: name + the full amount.
            PixelHoverTip tip = go.AddComponent<PixelHoverTip>();
            int tier = i;
            tip.onEnter = () => currencyHover = tier;
            tip.onExit = () => { if (currencyHover == tier) currencyHover = -1; };
            currencyChips[i] = chip;
        }
    }

    private void BuildCurrencyTooltip()
    {
        GameObject go = new GameObject("Currency Tooltip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasRoot.transform, false);
        go.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.96f);
        go.GetComponent<Image>().raycastTarget = false;
        currencyTipRect = go.GetComponent<RectTransform>();
        currencyTipRect.anchorMin = currencyTipRect.anchorMax = new Vector2(0.5f, 0.5f);
        currencyTipRect.pivot = new Vector2(0f, 1f);

        currencyTipText = CreateText(go.transform, "Text", "", 29f, TextAlignmentOptions.Left, FontStyles.Bold);
        currencyTipText.raycastTarget = false;
        RectTransform tr = currencyTipText.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(14f, -10f);
#if UNITY_2023_1_OR_NEWER
        currencyTipText.textWrappingMode = TextWrappingModes.NoWrap;
#else
        currencyTipText.enableWordWrapping = false;
#endif
        currencyTipText.overflowMode = TextOverflowModes.Overflow;
        go.SetActive(false);
    }

    /// <summary>Updates the amounts and which pixel types are listed (unlocked ones, or any you hold), laid out left to right.</summary>
    private void RefreshCurrency()
    {
        if (currencyStrip == null || currencyChips == null) return;
        var tiers = clicker.Tiers;
        float x = 6f, gap = 8f, chipH = CurrencyStripHeight - 10f;
        for (int i = 0; i < currencyChips.Length && i < tiers.Length; i++)
        {
            CurrencyChip chip = currencyChips[i];
            PixelClicker.PixelTier tier = tiers[i];
            bool visible = tier.unlocked || tier.count > 0d;
            if (chip.rect.gameObject.activeSelf != visible) chip.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            chip.icon.color = tier.UIColor;
            chip.amount.color = tier.UIColor;
            string text = PixelClicker.FormatNumberShort(tier.count);
            PixelUIKit.SetText(chip.amount, text);
            float textW = chip.amount.GetPreferredValues(text).x + 4f;
            chip.amount.rectTransform.sizeDelta = new Vector2(textW, chipH);
            float w = CurrencyIconSize + 14f + textW + 12f;
            chip.rect.sizeDelta = new Vector2(w, chipH);
            chip.rect.anchoredPosition = new Vector2(x, 0f);
            x += w + gap;
        }
        currencyContent.sizeDelta = new Vector2(x, CurrencyStripHeight);
    }

    /// <summary>Called every frame: tooltip position, and closing the Inventory / Log the moment the shop opens.</summary>
    private void UpdateCurrencyAndWindows()
    {
        bool open = panelObject != null && PixelPop.IsOpen(panelObject);
        if (open && !shopWasOpen)
        {
            // The shop replaces the Inventory and Log windows (its currency strip shows what they would).
            reopenInventory = PixelUI.InventoryOpen;
            reopenLog = PixelLog.LogOpen;
            PixelUI.SetInventoryOpen(false);
            PixelLog.SetLogOpen(false);
            RefreshCurrency();
        }
        if (!open && shopWasOpen)
        {
            // The shop closed: bring back the window it replaced, unless the player opened something else meanwhile
            // (opening the Inventory / Log / Crafting is what closed it).
            bool inventory = reopenInventory, log = reopenLog;
            reopenInventory = reopenLog = false;
            if (!PixelWindows.AnyOpenExcept(this) && !PixelUI.InventoryOpen && !PixelLog.LogOpen)
            {
                if (inventory) PixelUI.SetInventoryOpen(true);
                else if (log) PixelLog.SetLogOpen(true);
            }
        }
        shopWasOpen = open;

        if (currencyTipRect == null) return;
        string tipString = open && hoverTip != null ? hoverTip() : null;
        bool cardTip = !string.IsNullOrEmpty(tipString);
        bool show = cardTip || (open && currencyHover >= 0 && currencyHover < clicker.Tiers.Length && currencyStrip != null);
        if (currencyTipRect.gameObject.activeSelf != show) currencyTipRect.gameObject.SetActive(show);
        if (!show) return;

        // A purchase card's '!' box shows the item's description (wrapped); the currency list shows name + amount on one line each.
        Vector2 size;
        string wanted = cardTip ? tipString : null;
        if (!cardTip)
        {
            PixelClicker.PixelTier hovered = clicker.Tiers[currencyHover];
            wanted = hovered.displayName + "\n" + hovered.count.ToString("#,0.##");
        }
        if (wanted == currencyTipShown && cardTip == currencyTipWasCard) size = currencyTipSize;   // (same text as last frame: no need to measure it again)
        else if (cardTip)
        {
            const float wrapWidth = 460f;
#if UNITY_2023_1_OR_NEWER
            currencyTipText.textWrappingMode = TextWrappingModes.Normal;
#else
            currencyTipText.enableWordWrapping = true;
#endif
            PixelUIKit.SetText(currencyTipText, tipString);
            Vector2 pref = currencyTipText.GetPreferredValues(tipString, wrapWidth, 0f);
            currencyTipText.rectTransform.sizeDelta = new Vector2(wrapWidth, pref.y);
            size = new Vector2(wrapWidth, pref.y) + new Vector2(28f, 20f);
        }
        else
        {
#if UNITY_2023_1_OR_NEWER
            currencyTipText.textWrappingMode = TextWrappingModes.NoWrap;
#else
            currencyTipText.enableWordWrapping = false;
#endif
            PixelUIKit.SetText(currencyTipText, wanted);
            size = currencyTipText.GetPreferredValues() + new Vector2(28f, 20f);
            currencyTipText.rectTransform.sizeDelta = size - new Vector2(28f, 20f);
        }
        currencyTipShown = wanted; currencyTipWasCard = cardTip; currencyTipSize = size;
        currencyTipRect.sizeDelta = size;

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(currencyCanvasRect, PixelInput.PointerPosition(), null, out local);
        Vector2 half = currencyCanvasRect.rect.size * 0.5f;
        Vector2 pos = local + new Vector2(18f, -18f);
        if (pos.x + size.x > half.x) pos.x = local.x - size.x - 18f;
        if (pos.y - size.y < -half.y) pos.y = local.y + size.y + 18f;
        currencyTipRect.anchoredPosition = pos;
        currencyTipRect.SetAsLastSibling();
    }
}
