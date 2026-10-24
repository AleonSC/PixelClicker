// ---- System notes ----
// PixelShopPixels.cs
//   Part of partial class PixelShop: what the pack tiles show (the catalog's Toggleables / Skill packs entries, PixelShopCatalog.cs; Features and
//   Minigames packs use the same tiles). The two Pixels entries: Toggleables (pixelsToggleablesTitle: the RGB pack first until bought, then single pixels - Glass, Luminescent...;
//   SectionOf 0 / 1) and Skill packs (pixelsSkillPacksTitle: SectionOf 2, the Lumberjack's Pack first). A tile shows the pack's
//   pixels one after another (PackPictureKey), its first price (green / red; grey + 'Locked' while its requirement is missing),
//   'Owned' once bought and a level badge on leveled packs. The buy bar shows the full description, the cost (or what it still
//   needs) and Buy; a bought pack with upgrades turns Buy into a button that opens its upgrades window. Shift-click a tile = buy.
//   Same listing rules as the old rows: upgrade children are left out, locked packs only with showLockedPacks, hidden when bought
//   if 'hide purchased' is on. The search box still lists matching packs as rows in the main list.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>The shop's Pixels tab: pixel packs as tiles on two chips (Toggleables, Skill packs). Part of <see cref="PixelShop"/>.</summary>
public partial class PixelShop
{
    /// <summary>A bought pack that still has upgrades: its Buy button opens the upgrades window.</summary>
    private bool OpensUpgrades(int i) => IsPurchased(i) && HasPack(i) && HasChildren(i);

    /// <summary>The Pixels-tab packs on a chip (0 = Toggleables incl. the RGB pack, 1 = Skill packs), in shop order.</summary>
    private void CollectPixelPacks(int chip, List<int> list)
    {
        list.Clear();
        for (int i = 0; i < packs.Length; i++)
        {
            ShopPack pack = packs[i];
            if (pack == null || IsChild(i) || TabOf(pack) != ShopTab.Pixels) continue;
            if (!(IsPackRequirementMet(i) || showLockedPacks || HasPack(i)) || HiddenAsPurchased(i)) continue;
            int section = SectionOf(pack);
            if ((section == 2) != (chip == 1)) continue;
            if (section == 0 && IsPurchased(i)) continue;   // the RGB pack disappears once bought, whatever the 'hide purchased' setting says
            list.Add(i);
        }
        list.Sort((a, b) =>
        {
            int d = SectionOf(packs[a]).CompareTo(SectionOf(packs[b]));
            if (d != 0) return d;
            if (IsWoodcutterPack(packs[a]) != IsWoodcutterPack(packs[b])) return IsWoodcutterPack(packs[a]) ? -1 : 1;   // the Lumberjack's Pack stands first
            return a.CompareTo(b);
        });
    }

    private void FillPackTile(int i, ItemTile tile, bool selected)
    {
        ShopPack pack = packs[i];
        bool owned = IsPurchased(i);
        bool requirementMet = IsRequirementMet(i);
        bool locked = !owned && !requirementMet;
        if (owned) SetCostMessage(tile.costs, IsLeveled(pack) ? maxedText : ownedText, affordableColor);
        else SetCosts(tile.costs, CurrentCosts(pack), locked);
        string badge = IsLeveled(pack) ? pack.level + "/" + pack.levels.Length : "";
        SetTile(tile, badge, locked);
    }

    private void FillPackBar(TilePage page, int i)
    {
        ShopPack pack = packs[i];
        bool leveled = IsLeveled(pack);
        bool owned = IsPurchased(i);
        bool requirementMet = IsRequirementMet(i);

        if (owned) SetCostMessage(page.barCosts, "", textColor);
        else if (!requirementMet) SetCostMessage(page.barCosts, RequirementText(i), unaffordableColor);
        else SetCosts(page.barCosts, CurrentCosts(pack), false);

        if (OpensUpgrades(i)) SetPageBuy(page, true, upgradeText);
        else if (owned) SetPageBuy(page, false, leveled ? maxedText : ownedText);
        else if (!requirementMet) SetPageBuy(page, false, lockedText);
        else if (CanAfford(i)) SetPageBuy(page, true, leveled ? upgradeText : buyText);
        else SetPageBuy(page, false, NeedMoreLabel(CurrentCosts(pack), leveled ? upgradeText : buyText));
    }
}
