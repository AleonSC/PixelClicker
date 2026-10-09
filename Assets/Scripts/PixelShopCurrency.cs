using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop's currency panel: a scrolling list docked to the left side of the shop window (the shop itself stays where it is).
/// Each unlocked pixel type shows a small spinning cube in its colour and the amount you hold, always abbreviated from 1K up.
/// Hovering the number shows a tooltip with the pixel's name and the full amount. Part of <see cref="PixelShop"/>; its
/// Inspector settings are in PixelShop.cs ("Currency Panel").
/// </summary>
public partial class PixelShop
{
    private class CurrencyRow
    {
        public RectTransform rect;
        public PixelCubeIcon icon;
        public TMP_Text amount;
        public Image background;
    }

    private GameObject currencyPanel;
    private RectTransform currencyContent;
    private ScrollRect currencyScroll;
    private GameObject currencyScrollBar;
    private CurrencyRow[] currencyRows;
    private RectTransform currencyViewRect;
    private RectTransform currencyTipRect;
    private TMP_Text currencyTipText;
    private RectTransform currencyCanvasRect;
    private int currencyHover = -1;
    private bool shopWasOpen;

    private float CurrencyWidth => currencyPanelWidth > 40f ? currencyPanelWidth : 380f;
    private float CurrencyRowHeight => currencyRowHeight > 20f ? currencyRowHeight : 84f;
    private float CurrencyIconSize => currencyIconSize > 8f ? currencyIconSize : 60f;
    private float CurrencyFontSize => currencyFontSize > 8f ? currencyFontSize : 34f;

    private void BuildCurrencyPanel(Transform shopPanel)
    {
        if (hideCurrencyPanel || clicker == null) return;

        currencyCanvasRect = canvasRoot.GetComponent<RectTransform>();

        currencyPanel = new GameObject("Currency Panel", typeof(RectTransform), typeof(Image));
        currencyPanel.transform.SetParent(shopPanel, false);
        currencyPanel.GetComponent<Image>().color = panelColor;
        RectTransform pr = currencyPanel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
        pr.pivot = new Vector2(1f, 0.5f);
        pr.sizeDelta = new Vector2(CurrencyWidth, panelHeight);
        pr.anchoredPosition = new Vector2(-Mathf.Max(0f, currencyPanelGap), 0f);

        TMP_Text title = CreateText(currencyPanel.transform, "Title", string.IsNullOrEmpty(currencyTitle) ? "Currency" : currencyTitle,
                                    titleFontSize * 0.8f, TextAlignmentOptions.Center, FontStyles.Bold);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-panelPadding * 2f, headerHeight);
        tr.anchoredPosition = Vector2.zero;

        currencyScroll = PixelUIKit.CreateScrollView(currencyPanel.transform, "Currency Scroll", scrollbarHandleColor, Mathf.Min(scrollbarWidth, 16f),
                                                     scrollSpeed, out currencyContent, out currencyScrollBar);
        currencyViewRect = currencyScroll.GetComponent<RectTransform>();
        currencyViewRect.anchorMin = Vector2.zero;
        currencyViewRect.anchorMax = Vector2.one;
        currencyViewRect.offsetMin = new Vector2(panelPadding, panelPadding);
        currencyViewRect.offsetMax = new Vector2(-panelPadding, -headerHeight);

        var tiers = clicker.Tiers;
        currencyRows = new CurrencyRow[tiers.Length];
        for (int i = 0; i < tiers.Length; i++)
        {
            CurrencyRow row = new CurrencyRow();
            GameObject go = new GameObject("Currency " + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(currencyContent, false);
            row.background = go.GetComponent<Image>();
            row.background.color = rowColor;
            row.background.raycastTarget = false;
            row.rect = go.GetComponent<RectTransform>();
            row.rect.anchorMin = new Vector2(0f, 1f);
            row.rect.anchorMax = new Vector2(1f, 1f);
            row.rect.pivot = new Vector2(0.5f, 1f);
            row.rect.sizeDelta = new Vector2(0f, CurrencyRowHeight - 8f);

            // The pixel as a small spinning cube in its colour.
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
            iconGo.transform.SetParent(go.transform, false);
            row.icon = iconGo.AddComponent<PixelCubeIcon>();
            row.icon.spinDegreesPerSecond = 60f + (i % 5) * 8f; // not all in step
            RectTransform ir = row.icon.rectTransform;
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(CurrencyIconSize, CurrencyIconSize);
            ir.anchoredPosition = new Vector2(10f, 0f);

            row.amount = CreateText(go.transform, "Amount", "", CurrencyFontSize, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            row.amount.enableAutoSizing = true;
            row.amount.fontSizeMax = CurrencyFontSize;
            row.amount.fontSizeMin = 12f;
            row.amount.overflowMode = TextOverflowModes.Overflow;
#if UNITY_2023_1_OR_NEWER
            row.amount.textWrappingMode = TextWrappingModes.NoWrap;
#else
            row.amount.enableWordWrapping = false;
#endif
            RectTransform ar = row.amount.rectTransform;
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(CurrencyIconSize + 24f, 0f);
            ar.offsetMax = new Vector2(-8f, 0f);

            // Hover the number: name + the full amount.
            row.amount.raycastTarget = true;
            PixelHoverTip tip = row.amount.gameObject.AddComponent<PixelHoverTip>();
            int tier = i;
            tip.onEnter = () => currencyHover = tier;
            tip.onExit = () => { if (currencyHover == tier) currencyHover = -1; };

            currencyRows[i] = row;
        }

        BuildCurrencyTooltip();
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

        currencyTipText = CreateText(go.transform, "Text", "", CurrencyFontSize * 0.85f, TextAlignmentOptions.Left, FontStyles.Bold);
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

    /// <summary>Updates the amounts and which pixel types are listed (unlocked ones, or any you hold).</summary>
    private void RefreshCurrency()
    {
        if (currencyPanel == null || currencyRows == null) return;

        var tiers = clicker.Tiers;
        float y = 0f;
        for (int i = 0; i < currencyRows.Length && i < tiers.Length; i++)
        {
            CurrencyRow row = currencyRows[i];
            PixelClicker.PixelTier tier = tiers[i];
            bool visible = tier.unlocked || tier.count > 0d;
            if (row.rect.gameObject.activeSelf != visible) row.rect.gameObject.SetActive(visible);
            if (!visible) continue;

            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += CurrencyRowHeight;
            row.icon.color = tier.UIColor;
            row.amount.color = tier.UIColor;
            PixelUIKit.SetText(row.amount, PixelClicker.FormatNumberShort(tier.count));
        }
        PixelUIKit.UpdateScrollView(currencyScroll, currencyScrollBar, y, currencyViewRect.rect.height);
    }

    /// <summary>Called every frame: tooltip position, and closing the Inventory / Log the moment the shop opens.</summary>
    private void UpdateCurrencyAndWindows()
    {
        bool open = panelObject != null && panelObject.activeSelf;
        if (open && !shopWasOpen)
        {
            // The shop replaces the Inventory and Log windows (its currency panel shows what they would).
            PixelUI.SetInventoryOpen(false);
            PixelLog.SetLogOpen(false);
            RefreshCurrency();
        }
        shopWasOpen = open;

        if (currencyTipRect == null) return;
        bool show = open && currencyHover >= 0 && currencyHover < clicker.Tiers.Length && currencyPanel != null;
        if (currencyTipRect.gameObject.activeSelf != show) currencyTipRect.gameObject.SetActive(show);
        if (!show) return;

        PixelClicker.PixelTier tier = clicker.Tiers[currencyHover];
        PixelUIKit.SetText(currencyTipText, tier.displayName + "\n" + tier.count.ToString("#,0.##"));
        Vector2 size = currencyTipText.GetPreferredValues() + new Vector2(28f, 20f);
        currencyTipText.rectTransform.sizeDelta = size - new Vector2(28f, 20f);
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
