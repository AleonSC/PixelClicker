// ---- System notes ----
// PixelShopSearch.cs
//   Part of partial class PixelShop: the search results, shown as a tile page (layout in PixelShopTilePage.cs, no chip row) while
//   the search box has text (shopSearch; RefreshTilePages opens it in place of every tab). One page lists four kinds of results,
//   told apart by id range:
//     packs (id = pack index, any tab; MatchesSearch: name, description, reward pixel names) - same tiles / bar as the Pixels tab,
//     consumables (ItemBase + item: potions, devices, seeds, supplies; name, description, category word) - as on the Consumables tab,
//     Value upgrades (ValueBase + tier: '<pixel> Value' + 'value upgrade') - Buy = one level, Shift = as many as you can,
//     Operation rows (OperationBase + kind index: '<device> upgrades operation') - Buy opens that device's upgrade window.
//   Shift-click a tile = buy one (Operation: opens the window). The emptied search shows the open tab again.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>The shop's search results as tiles. Part of <see cref="PixelShop"/>.</summary>
public partial class PixelShop
{
    private TilePage searchPage;

    private const int ItemBase = 100000, ValueBase = 200000, OperationBase = 300000;
    private static readonly string[] CategoryWords = { "potion", "device utility", "seed", "supply instant" };

    private enum SearchKind { Pack, Item, Value, Operation }

    private static SearchKind KindOf(int id) =>
        id >= OperationBase ? SearchKind.Operation : id >= ValueBase ? SearchKind.Value : id >= ItemBase ? SearchKind.Item : SearchKind.Pack;

    private void BuildSearchPage(Transform panel, float top)
    {
        TilePage page = BuildTilePage(panel, top, ShopTab.Automatic, new string[0], false, 0);
        page.empty.text = searchNoResultsText;
        page.collect = (chip, list) => CollectSearchResults(list);
        page.tileName = SearchTileName;
        page.pictureKey = SearchPictureKey;
        page.description = SearchDescription;
        page.fillTile = (id, tile, selected) =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: FillPackTile(id, tile, selected); break;
                case SearchKind.Item: FillItemTile(id - ItemBase, tile, selected); break;
                case SearchKind.Value: FillValueTile(id - ValueBase, tile); break;
                default: FillOperationTile(id - OperationBase, tile); break;
            }
        };
        page.fillBar = id =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: FillPackBar(page, id); break;
                case SearchKind.Item: FillItemBar(page, id - ItemBase); break;
                case SearchKind.Value: FillValueBar(page, id - ValueBase); break;
                default: SetCostMessage(page.barCosts, "", textColor); SetPageBuy(page, true, upgradeText); break;
            }
        };
        page.purchase = (id, max) =>
        {
            switch (KindOf(id))
            {
                case SearchKind.Pack: if (OpensUpgrades(id)) OpenUpgradesWindow(id); else TryBuy(id); break;
                case SearchKind.Item: { int item = id - ItemBase; int n = max ? MaxAffordable(item) : 1; if (n > 0) TryBuyItems(item, n); break; }
                case SearchKind.Value: TryBuyValueUpgrade(id - ValueBase, max); RefreshRows(); break;
                default: OpenOperationWindow(id - OperationBase); break;
            }
        };
        page.quickBuy = id => page.purchase(id, false);
        searchPage = page;
    }

    /// <summary>Every match, in order: packs, consumables, Value upgrades, Operation rows.</summary>
    private void CollectSearchResults(List<int> list)
    {
        list.Clear();
        if (shopSearch.Length == 0) return;
        for (int i = 0; i < packs.Length && i < ItemBase; i++)
        {
            ShopPack pack = packs[i];
            if (pack == null || IsChild(i) || HiddenAsPurchased(i)) continue;
            if (!(IsPackRequirementMet(i) || showLockedPacks || HasPack(i))) continue;
            if (MatchesSearch(pack)) list.Add(i);
        }
        if (consumables != null)
            for (int i = 0; i < consumables.ItemCount; i++)
            {
                if (consumables.ItemCraftOnly(i) || consumables.ItemNotSold(i) || consumables.ItemVendorOnly(i) || consumables.IsDragonSeed(i) || !ItemTrustListed(i)) continue;
                if (TextMatches(consumables.ItemName(i)) || TextMatches(consumables.ItemDescription(i)) || TextMatches(CategoryWords[Mathf.Clamp(consumables.CategoryOf(i), 0, 3)]))
                    list.Add(ItemBase + i);
            }
        for (int t = 0; t < clicker.Tiers.Length; t++)
        {
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            if (!tier.unlocked || PixelClicker.IsHazard(tier.type) || (HidePurchased && ValueUpgradeMaxed(t))) continue;
            if (TextMatches(string.Format(valueRowNameFormat, tier.displayName) + " value upgrade")) list.Add(ValueBase + t);
        }
        if (consumables != null)
            for (int k = 0; k < OperationKinds.Length; k++)
                if (consumables.OperationListed(OperationKinds[k]) && TextMatches(consumables.OperationName(OperationKinds[k]) + " upgrades operation"))
                    list.Add(OperationBase + k);
    }

    private string SearchTileName(int id)
    {
        switch (KindOf(id))
        {
            case SearchKind.Pack: return packs[id].displayName;
            case SearchKind.Item: return consumables.ItemName(id - ItemBase);
            case SearchKind.Value: return string.Format(valueRowNameFormat, clicker.Tiers[id - ValueBase].displayName);
            default: return consumables.OperationName(OperationKinds[id - OperationBase]) + " " + operationSubTabText;
        }
    }

    private System.Func<string> SearchPictureKey(int id)
    {
        switch (KindOf(id))
        {
            case SearchKind.Pack: return PackPictureKey(id);
            case SearchKind.Item: { string key = "item:" + consumables.ItemName(id - ItemBase); return () => key; }
            case SearchKind.Value: { int t = id - ValueBase; return () => t < clicker.Tiers.Length ? TierKey(clicker.Tiers[t].type) : null; }
            default:
            {
                PixelConsumables.Device d = consumables.FindDeviceOfKind(OperationKinds[id - OperationBase]);
                string key = d != null ? "item:" + d.displayName : null;
                return () => key;
            }
        }
    }

    private string SearchDescription(int id)
    {
        switch (KindOf(id))
        {
            case SearchKind.Pack: return id < packs.Length ? ResolveDescription(packs[id]) : null;
            case SearchKind.Item: return consumables != null && id - ItemBase < consumables.ItemCount ? consumables.ItemDescription(id - ItemBase) : null;
            case SearchKind.Value:
            {
                int t = id - ValueBase;
                return t < clicker.Tiers.Length ? string.Format(ultraLevelFormat, clicker.Tiers[t].valueLevel) + ". " + valueInfoText : null;
            }
            default: return consumables.OperationDescription(OperationKinds[id - OperationBase]);
        }
    }

    // ---- Value upgrades as tiles ----

    private void FillValueTile(int t, ItemTile tile)
    {
        PixelClicker.PixelTier tier = clicker.Tiers[t];
        if (ValueUpgradeMaxed(t)) SetCostMessage(tile.costs, maxedText, affordableColor);
        else if (ValueSkillCapped(t, out _, out _)) SetCostMessage(tile.costs, valueCappedButtonText, lockedItemColor);
        else SetCosts(tile.costs, new[] { new PackCost { type = tier.type, amount = ValueUpgradeCost(t) } }, false);
        SetTile(tile, "Lv " + tier.valueLevel, false);
    }

    private void FillValueBar(TilePage page, int t)
    {
        PixelClicker.PixelTier tier = clicker.Tiers[t];
        if (ValueUpgradeMaxed(t)) { SetCostMessage(page.barCosts, "", textColor); SetPageBuy(page, false, maxedText); return; }
        if (ValueSkillCapped(t, out string skill, out int level))
        {
            SetCostMessage(page.barCosts, string.Format(valueCappedFormat, skill, level), unaffordableColor);
            SetPageBuy(page, false, valueCappedButtonText);
            return;
        }
        PackCost[] cost = { new PackCost { type = tier.type, amount = ValueUpgradeCost(t) } };
        SetCosts(page.barCosts, cost, false);
        SetPageBuy(page, CanAffordCosts(cost), valueButtonText);
    }

    // ---- Operation rows as tiles ----

    private void FillOperationTile(int k, ItemTile tile)
    {
        int bought = 0, total = 0;
        foreach (PixelOperationUpgrade u in operationUpgrades)
            if (u != null && u.device == OperationKinds[k]) { bought += OperationLevel(u); total += u.maxLevel; }
        SetCostMessage(tile.costs, bought + " / " + total, levelColor);
        SetTile(tile, "", false);
    }
}
