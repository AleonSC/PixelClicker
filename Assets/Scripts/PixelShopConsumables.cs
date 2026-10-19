// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopConsumables.cs
//   Third-and-a-half part of partial class PixelShop: the Consumables tab, a tile page (layout in PixelShopTilePage.cs).
//   Chips: Utilities (opens first), Potions, Seeds, Supplies (ConsumableChipCategory -> PixelConsumables.CategoryOf 1 / 0 / 2 / 3;
//   labels utilitiesCardTitle...). Tiles: price in green / red / grey when locked, owned count badge ("x3"), 'Full' when a potion
//   is at its cap. Shift-click a tile = buy one; the buy bar has - count + and Buy (Shift-click Buy = MaxAffordable). The
//   Recycler stands first among Utilities. Craft-only, not-sold, vendor-only items and Dragon seeds are never listed. The search
//   box still lists matching items as rows in the main list (RefreshSearchItems in PixelShopUI.cs). Buying goes through
//   TryBuyItems(item, count). Settings are in PixelShop.cs ("Consumables Tab").
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shop's Consumables tab: a tile page of potions, devices, seeds and supplies. Items whose pixel type is not unlocked yet
/// are shown locked (with what they need) instead of being hidden. Part of <see cref="PixelShop"/>; the settings live in PixelShop.cs.
/// </summary>
public partial class PixelShop
{
    /// <summary>The item category (PixelConsumables.CategoryOf) of each chip, in chip order: Utilities first.</summary>
    private static readonly int[] ConsumableChipCategory = { 1, 0, 2, 3 };

    private void BuildConsumablesArea(Transform panel, float top)
    {
        string[] titles = new string[ConsumableChipCategory.Length];
        for (int c = 0; c < titles.Length; c++)
        {
            int cat = ConsumableChipCategory[c];
            titles[c] = cat == 0 ? potionsCardTitle : cat == 1 ? utilitiesCardTitle : cat == 2 ? seedsCardTitle : suppliesCardTitle;
        }
        TilePage page = BuildTilePage(panel, top, ShopTab.Consumables, titles, true, 0);
        page.collect = (chip, list) => CollectCategoryItems(ConsumableChipCategory[chip], list);
        page.tileName = item => consumables.ItemName(item);
        page.pictureKey = item => { string key = "item:" + consumables.ItemName(item); return () => key; };
        page.description = item => consumables != null && item < consumables.ItemCount ? consumables.ItemDescription(item) : null;
        page.fillTile = FillItemTile;
        page.fillBar = item => FillItemBar(page, item);
        page.purchase = (item, max) => { int n = max ? MaxAffordable(item) : page.count; if (n > 0) TryBuyItems(item, n); };
        page.quickBuy = item => TryBuyItems(item, 1);
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

    private static readonly PixelConsumables.DeviceKind[] UtilitiesFirst =
    {
        PixelConsumables.DeviceKind.Recycler, PixelConsumables.DeviceKind.SortingHopper, PixelConsumables.DeviceKind.Coupling, PixelConsumables.DeviceKind.ContainerHose,
        PixelConsumables.DeviceKind.ConveyorBelt, PixelConsumables.DeviceKind.ConveyorCombiner, PixelConsumables.DeviceKind.BeltJump,
    };

    /// <summary>Items of a category, in shop order (the Recycler, Sorting Hopper, Coupling and Container Hose first among Utilities).</summary>
    private void CollectCategoryItems(int category, List<int> list)
    {
        list.Clear();
        if (consumables == null) return;
        for (int i = 0; i < consumables.ItemCount; i++)
        {
            if (consumables.ItemCraftOnly(i) || consumables.ItemNotSold(i) || consumables.ItemVendorOnly(i) || consumables.IsDragonSeed(i)) continue;   // crafted / vendor items aren't sold here
            if (consumables.CategoryOf(i) == category) list.Add(i);
        }
        if (category == 1)
        {
            // The early husk setup leads the Utilities, in this order: Recycler, Sorting Hopper, Coupling, Container Hose (those on sale).
            int at = 0;
            foreach (PixelConsumables.DeviceKind kind in UtilitiesFirst)
            {
                int r = list.FindIndex(i => consumables.IsDevice(i) && consumables.DeviceKindOf(i) == kind);
                if (r < 0) continue;
                int item = list[r];
                list.RemoveAt(r);
                list.Insert(Mathf.Min(at, list.Count), item);
                at++;
            }
        }
    }

    private void FillItemTile(int item, ItemTile tile, bool selected)
    {
        bool locked = ItemLocked(item);
        PackCost[] costs = consumables.ItemCosts(item);
        bool full = !locked && consumables.ItemRoom(item) <= 0;
        if (full) SetCostMessage(tile.costs, potionFullText, unaffordableColor);
        else SetCosts(tile.costs, costs, locked);
        int owned = consumables.ItemOwned(item);
        SetTile(tile, owned > 0 ? "x" + PixelConsumables.OwnedText(owned) : "", locked);
    }

    private void FillItemBar(TilePage page, int item)
    {
        // Potions are capped per type: never offer more than there is room for.
        bool locked = ItemLocked(item);
        int room = consumables.ItemRoom(item);
        if (room <= 0 && !locked)
        {
            SetCostMessage(page.barCosts, string.Format(NoHave(potionFullFormat), consumables.ItemCapacity(item), PixelConsumables.OwnedText(consumables.ItemOwned(item))), unaffordableColor);
            SetPageBuy(page, false, potionFullText);
            return;
        }
        if (room > 0 && page.count > room) page.count = room;

        if (locked)
        {
            int t = clicker.IndexOf(consumables.ItemRequiredType(item));
            string need = t >= 0 ? clicker.Tiers[t].displayName : consumables.ItemRequiredType(item).ToString();
            SetCostMessage(page.barCosts, string.Format(lockedRequirementFormat, need), unaffordableColor);
            SetPageBuy(page, false, lockedText);
            return;
        }
        PackCost[] total = ScaleCosts(consumables.ItemCosts(item), page.count);
        SetCosts(page.barCosts, total, false);
        SetPageBuy(page, CanAffordCosts(total), string.Format(buyCountFormat, page.count));
    }
}
