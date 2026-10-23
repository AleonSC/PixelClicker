// ---- System notes ----
// PixelShopDatabase.cs
//   Part of PixelShop: its Game Database entries (IPixelDatabaseSource, see PixelDatabase.cs).
//   - 'Shop pack' entries (PacksOrder): description + tab; discovered = bought or a reward pixel unlocked; links to reward pixels, cost pixels /
//     minigame currencies, required packs / minigame goals and the minigame it unlocks.
//   - Pack -> skill links (PackSkillLinksOrder, after the skills exist).
//   - 'Upgrade' entries (ExtrasOrder, 2026-10-24): one per Operation upgrade (Upgrades > Operation; operationUpgrades): its description, what each
//     level adds, the level bought, linked to its consumable; discovered with that consumable (or once a level is bought).
// ---------------------------------------------------
using System.Collections.Generic;

public partial class PixelShop : IPixelDatabaseSource
{
    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        // ---- Shop packs ----
        steps.Add(PixelDatabase.PacksOrder, b =>
        {
            if (packs == null) return;
            for (int i = 0; i < packs.Length; i++)
            {
                ShopPack p = packs[i];
                if (p == null) { b.Packs.Add(null); continue; }
                PixelDatabase.Entry packEntry = b.Add("Shop pack", p.displayName, PixelDatabase.Plain(p.description) + "\nShop tab: " + p.tab + (p.purchased ? "\nBought." : ""));
                bool rewardKnown = false;
                if (p.rewardTiers != null && b.Clicker != null) foreach (PixelClicker.PixelTier rt in p.rewardTiers) if (rt != null && b.Clicker.IsUnlocked(rt.type)) rewardKnown = true;
                packEntry.discovered = p.purchased || rewardKnown;
                b.Packs.Add(packEntry);
            }
            for (int i = 0; i < packs.Length; i++)
            {
                ShopPack p = packs[i];
                PixelDatabase.Entry pe = b.Packs[i];
                if (p == null || pe == null) continue;
                if (p.rewardTiers != null)
                    foreach (PixelClicker.PixelTier t in p.rewardTiers) b.Link(pe, "Rewards pixel", b.Pixel(t.type), "Rewarded by shop pack");
                if (p.costs != null)
                    foreach (PackCost c in p.costs)
                    {
                        if (!string.IsNullOrEmpty(c.minigameCurrency)) { b.Minigames.TryGetValue(c.minigameCurrency, out PixelDatabase.Entry mg); b.Link(pe, "Costs " + c.amount + " of minigame currency", mg, "Spent on shop pack"); }
                        else b.Link(pe, "Costs " + c.amount, b.Pixel(c.type), "Pays for shop pack");
                    }
                if (p.requirements != null)
                    foreach (PackRequirement r in p.requirements)
                    {
                        if (r.kind == RequirementKind.Pack && r.packIndex >= 0 && r.packIndex < b.Packs.Count) b.Link(pe, "Requires pack", b.Packs[r.packIndex], "Required by pack");
                        else if (r.kind != RequirementKind.Pack && b.Minigames.TryGetValue(r.minigameId ?? "", out PixelDatabase.Entry mg)) b.Link(pe, "Requires goal of", mg, "Goal needed for shop pack");
                    }
                if (!string.IsNullOrEmpty(p.unlocksMinigame) && b.Minigames.TryGetValue(p.unlocksMinigame, out PixelDatabase.Entry unlocked)) b.Link(pe, "Unlocks minigame", unlocked, "Unlocked by shop pack");
            }
        });

        // ---- Packs that unlock a skill (the skill entries exist by now) ----
        steps.Add(PixelDatabase.PackSkillLinksOrder, b =>
        {
            if (packs == null) return;
            for (int i = 0; i < packs.Length && i < b.Packs.Count; i++)
                if (packs[i] != null && !string.IsNullOrEmpty(packs[i].skillId) && b.Skills.TryGetValue(packs[i].skillId, out PixelDatabase.Entry se))
                    b.Link(b.Packs[i], "Unlocks skill", se, "Unlocked by shop pack");
        });

        // ---- Operation upgrades (one entry per upgrade, linked to its consumable) ----
        steps.Add(PixelDatabase.ExtrasOrder, b =>
        {
            if (operationUpgrades == null) return;
            foreach (PixelOperationUpgrade u in operationUpgrades)
            {
                if (u == null || string.IsNullOrEmpty(u.displayName)) continue;
                PixelConsumables.Device d = consumables != null ? consumables.FindDeviceOfKind(u.device) : null;
                string itemName = d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : u.device.ToString();
                int level = OperationLevel(u);
                string per = (u.perLevel >= 0f ? "+" : "") + u.perLevel.ToString("0.###") + u.unit;
                PixelDatabase.Entry ue = b.Add("Upgrade", itemName + ": " + u.displayName,
                    (string.IsNullOrEmpty(u.description) ? "" : PixelDatabase.Plain(u.description) + "\n") +
                    "An Operation upgrade of the " + itemName + " (shop: Upgrades > Operation). Each level: " + per + ", up to level " + u.maxLevel + "." +
                    "\nLevel bought: " + level + " / " + u.maxLevel + ".");
                b.Items.TryGetValue(itemName, out PixelDatabase.Entry item);
                ue.discovered = level > 0 || (item != null && item.discovered);
                b.Link(ue, "Upgrades consumable", item, "Operation upgrade");
            }
        });
    }
}
