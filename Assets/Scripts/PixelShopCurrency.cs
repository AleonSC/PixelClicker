// ---- System notes ----
// PixelShopCurrency.cs
//   Part of partial class PixelShop: what the shop does with the Inventory and Log windows, and the shop's tooltip.
//   Since 2026-10-24 the shop has NO currency row of its own: opening the shop opens the PINNED Inventory (switching the Inventory to its
//   pinned view for as long as the shop is open) and closes the Log; closing the shop closes that pinned Inventory again and puts the
//   Inventory back the way it was (full view, or the Log, if one was open before; a pinned Inventory that was already open stays). The player can still put the pinned Inventory away with
//   its HUD button / key while the shop stays open, and bring it back (a pinned Inventory opening never closes the shop: PixelUI.SetInventoryOpen).
//   The tooltip box here shows the purchase cards' hover text (hoverTip). Also: CloseShopWindow.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop's companion windows (the pinned Inventory shows your currency while you shop) and its hover tooltip.
/// Part of <see cref="PixelShop"/>.
/// </summary>
public partial class PixelShop
{
    /// <summary>Closes the shop window (the Inventory and Log call this when they open: the windows never show together).</summary>
    public static void CloseShopWindow()
    {
        PixelShop shop = PixelFind.First<PixelShop>();
        if (shop != null && shop.panelObject != null && PixelPop.IsOpen(shop.panelObject)) PixelPop.Hide(shop.panelObject);
    }

    private RectTransform currencyTipRect;
    private string currencyTipShown;   // (the tip text last measured, so the box is only measured again when it changes)
    private Vector2 currencyTipSize;
    private TMP_Text currencyTipText;
    private RectTransform currencyCanvasRect;
    private bool shopWasOpen;
    private bool reopenFullInventory, reopenLog, pinnedWasOpen; // which windows the shop closed when it opened, to bring back when it closes
    private int compactModeBefore;               // the Inventory's pinned mode before the shop pinned it (0 = it was not pinned)

    /// <summary>The hover tooltip for the shop's purchase cards.</summary>
    private void BuildShopTooltip()
    {
        if (clicker == null) return;
        currencyCanvasRect = canvasRoot.GetComponent<RectTransform>();
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

        currencyTipText = CreateText(go.transform, "Text", "", 29f, TextAlignmentOptions.Left, FontStyles.Bold);
        currencyTipText.raycastTarget = false;
        RectTransform tr = currencyTipText.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(14f, -10f);
#if UNITY_2023_1_OR_NEWER
        currencyTipText.textWrappingMode = TextWrappingModes.Normal;
#else
        currencyTipText.enableWordWrapping = true;
#endif
        currencyTipText.overflowMode = TextOverflowModes.Overflow;
        go.SetActive(false);
    }

    /// <summary>Called every frame: tooltip position, and showing the pinned Inventory (closing the Log) while the shop is open.</summary>
    private void UpdateCurrencyAndWindows()
    {
        bool open = panelObject != null && PixelPop.IsOpen(panelObject);
        if (open && !shopWasOpen)
        {
            // The pinned Inventory shows the currency while you shop (the full Inventory and the Log close).
            reopenFullInventory = PixelUI.InventoryOpen && !PixelUI.Compact;
            pinnedWasOpen = PixelUI.InventoryOpen && PixelUI.Compact;   // (already pinned and open: it simply stays when the shop closes)
            reopenLog = PixelLog.LogOpen;
            compactModeBefore = PixelUI.CompactMode;
            PixelLog.SetLogOpen(false);
            PixelUI.ShowPinnedBesideShop();
        }
        if (!open && shopWasOpen)
        {
            // The shop closed: put the pinned Inventory away (unless it was already open before the shop) and the Inventory's view back
            // the way it was; bring back the window it replaced, unless the player opened something else meanwhile.
            if (!pinnedWasOpen)
            {
                if (PixelUI.InventoryOpen && PixelUI.Compact) PixelUI.SetInventoryOpen(false, true);
                PixelUI.CompactMode = compactModeBefore;
            }
            bool inventory = reopenFullInventory, log = reopenLog;
            reopenFullInventory = reopenLog = false;
            if (!PixelWindows.AnyOpenExcept(this) && !PixelUI.InventoryOpen && !PixelLog.LogOpen)
            {
                if (inventory) PixelUI.SetInventoryOpen(true);
                else if (log) PixelLog.SetLogOpen(true);
            }
        }
        shopWasOpen = open;

        if (currencyTipRect == null) return;
        string tipString = open && hoverTip != null ? hoverTip() : null;
        bool show = !string.IsNullOrEmpty(tipString);
        if (currencyTipRect.gameObject.activeSelf != show) currencyTipRect.gameObject.SetActive(show);
        if (!show) return;

        // A purchase card's '!' box shows the item's description, wrapped.
        Vector2 size;
        if (tipString == currencyTipShown) size = currencyTipSize;   // (same text as last frame: no need to measure it again)
        else
        {
            const float wrapWidth = 460f;
            PixelUIKit.SetText(currencyTipText, tipString);
            Vector2 pref = currencyTipText.GetPreferredValues(tipString, wrapWidth, 0f);
            currencyTipText.rectTransform.sizeDelta = new Vector2(wrapWidth, pref.y);
            size = new Vector2(wrapWidth, pref.y) + new Vector2(28f, 20f);
        }
        currencyTipShown = tipString; currencyTipSize = size;
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
