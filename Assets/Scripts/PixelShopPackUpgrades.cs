// ---- System notes ----
// PixelShopPackUpgrades.cs
//   Part of PixelShop (partial class): EnsureDefaultPacks (adds the missing built-in packs, then brings saved packs up to date step by step). The system notes are in PixelShop.cs.
// ---------------------------------------------------
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelShop
{
    /// <summary>
    /// Adds any default pack (Auto Clicker, upgrades, Glass, Vacuum) missing from the Packs list.
    /// Runs at startup and, in the Editor, when the component is loaded - so older components get the
    /// new packs as real, editable entries in the Inspector. Returns true if anything was added.
    /// </summary>
    private bool EnsureDefaultPacks()
    {
        bool changed = MigrateOldPackData();
        if (!addDefaultPacks) return changed;
        if (packs == null) packs = new ShopPack[0];

        // Built-ins come in dependency order, so each one can find the pack it requires.
        foreach (DefaultPack builtIn in BuiltInPacks)
        {
            if (Array.Exists(packs, p => builtIn.isThis(p))) continue;

            int requiredIndex = builtIn.requires != null ? Array.FindIndex(packs, p => builtIn.requires(p)) : -1;
            Array.Resize(ref packs, packs.Length + 1);
            packs[packs.Length - 1] = builtIn.create(requiredIndex);
            changed = true;
        }

        changed |= UpgradeShopLayout();
        changed |= UpgradeSeedAndPackOrder();
        changed |= UpgradeBlockStats();
        changed |= UpgradeSkillPacks();
        changed |= UpgradeMiningAndFishingBlocks();
        changed |= UpgradeGatheringBlocks();
        changed |= UpgradePackPricesAndNames();

        return changed;
    }

    /// <summary>Step 1 of EnsureDefaultPacks: the 2026-10-16 shop rebalance and the tile size upgrades. Returns true if anything changed.</summary>
    private bool UpgradeShopLayout()
    {
        bool changed = false;
        if (shopBalanceVersion < 1)
        {
            // Shop rebalance (2026-10-16): every built-in pack takes its new default prices (one-off packs and every level of an upgrade), and
            // minigame packs need nothing but their price (no 'needs pack X' any more).
            ResetBuiltInPackPrices();   // (the same as the 'Reset Built-in Pack Prices' context menu)
            foreach (ShopPack p in packs) if (p != null && p.tab == ShopTab.Minigames) p.requirements = new PackRequirement[0];
            shopBalanceVersion = 1;
            changed = true;
        }
        if (shopBalanceVersion < 2)
        {
            // 2026-10-24: the gathering skill packs go on the price ladder (Tier 0): saved packs take the coded skillLadder flag.
            foreach (DefaultPack builtIn in BuiltInPacks)
            {
                int index = Array.FindIndex(packs, p => p != null && builtIn.isThis(p));
                if (index >= 0) packs[index].skillLadder = builtIn.create(-1).skillLadder;
            }
            shopBalanceVersion = 2;
            changed = true;
        }

        if (tileLayoutVersion < 1)
        {
            // 2026-10-18: bigger tiles (5 to a row, not 6) so the swatch prices read well.
            tileWidth = 160f;
            tileHeight = 200f;
            tileLayoutVersion = 1;
            changed = true;
        }
        if (tileLayoutVersion < 2)
        {
            // 2026-10-18: no name under the tile's picture (the hover tooltip and the buy bar show it), so the tiles are shorter.
            tileHeight = 176f;
            tileLayoutVersion = 2;
            changed = true;
        }
        return changed;
    }

    /// <summary>Step 2 of EnsureDefaultPacks (pack data 6-10): the Mirror / Seed / Water list order and the Seed shell's clicks and pay. Returns true if anything changed.</summary>
    private bool UpgradeSeedAndPackOrder()
    {
        bool changed = false;
        changed |= UpgradePackDataTo8();
        changed |= UpgradePackDataTo10();
        return changed;
    }

    /// <summary>Part of UpgradeSeedAndPackOrder: packDataVersion steps 6, 7, 8 (in this order): the Mirror / Seed list order and the first Seed shell. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo8()
    {
        bool changed = false;
        // The Mirror pixel is listed after Electric and before Vacuum: move it there once in lists that got it appended at the end.
        if (packDataVersion < 6)
        {
            int mirrorIndex = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Mirror));
            int vacuumIndex = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Vacuum));
            if (mirrorIndex >= 0 && vacuumIndex >= 0 && mirrorIndex > vacuumIndex) MovePack(mirrorIndex, vacuumIndex);
            if (mirrorIndex >= 0) { packDataVersion = 6; changed = true; }
        }

        // The Seed pixel is listed before Electric: move it there once in lists that got it appended at the end.
        if (packDataVersion < 7)
        {
            int seedIndex = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Seed));
            int electricIdx = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Electric));
            if (seedIndex >= 0 && electricIdx >= 0 && seedIndex > electricIdx) MovePack(seedIndex, electricIdx);
            if (seedIndex >= 0) { packDataVersion = 7; changed = true; }
        }

        // The Seed is a cracking shell now (3 clicks, pays 6 on the last, a sprout grows a random pixel): update packs saved with the first Seed version.
        if (packDataVersion < 8 && packs != null)
        {
            foreach (ShopPack pack in packs)
            {
                if (pack == null || pack.rewardTiers == null || !Rewards(pack, PixelClicker.PixelType.Seed)) continue;
                ShopPack fresh = CreateSeedPixelPack(-1);
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                {
                    if (reward.type != PixelClicker.PixelType.Seed) continue;
                    if (reward.clicksToCollect <= 1) reward.clicksToCollect = 3;
                    if (Mathf.Approximately((float)reward.amountPerClick, 2f)) reward.amountPerClick = 6;
                }
                if (pack.description != null && pack.description.StartsWith("Adds the Seed pixel: a brown cube with a sprout")) pack.description = fresh.description;
                packDataVersion = 8;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Part of UpgradeSeedAndPackOrder: packDataVersion steps 9, 10 (in this order): the 8-click Seed shell and the Water list order. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo10()
    {
        bool changed = false;
        // The Seed shell now takes 8 clicks and pays 15 (it was 3 and 6): only the previous defaults are changed.
        if (packDataVersion < 9 && packs != null)
        {
            foreach (ShopPack pack in packs)
            {
                if (pack == null || pack.rewardTiers == null || !Rewards(pack, PixelClicker.PixelType.Seed)) continue;
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                {
                    if (reward.type != PixelClicker.PixelType.Seed) continue;
                    if (reward.clicksToCollect == 3) reward.clicksToCollect = 8;
                    if (Mathf.Approximately((float)reward.amountPerClick, 6f)) reward.amountPerClick = 15;
                }
                if (pack.description != null && pack.description.StartsWith("Adds the Seed pixel: a brown shell you click a few times to crack."))
                    pack.description = CreateSeedPixelPack(-1).description;
                packDataVersion = 9;
                changed = true;
            }
        }
        // The Water pixel is listed right after Seed (before Electric): move it there once in lists that got it appended at the end.
        if (packDataVersion < 10)
        {
            int waterIndex = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Water));
            int electricAt = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Electric));
            if (waterIndex >= 0 && electricAt >= 0 && waterIndex > electricAt) MovePack(waterIndex, electricAt);
            if (waterIndex >= 0) { packDataVersion = Mathf.Max(packDataVersion, 10); changed = true; }
        }
        return changed;
    }

    /// <summary>Step 3 of EnsureDefaultPacks (pack data 11-15): spawn weights, the Meteor rock, Rock / hazard blocks and the Farmer's Pack blocks. Returns true if anything changed.</summary>
    private bool UpgradeBlockStats()
    {
        bool changed = false;
        changed |= UpgradePackDataTo12();
        changed |= UpgradePackDataTo15();
        return changed;
    }

    /// <summary>Part of UpgradeBlockStats: packDataVersion steps 11, 12 (in this order): Vacuum / Electric spawn weights and the Meteor rock. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo12()
    {
        bool changed = false;
        // Vacuum (0.2 -> 0.28) and Electric (0.15 -> 0.2) pixels are slightly more common; only the old defaults are changed, not a value you typed.
        if (packDataVersion < 11 && packs != null)
        {
            foreach (ShopPack pack in packs)
            {
                if (pack == null || pack.rewardTiers == null) continue;
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                {
                    if (reward.type == PixelClicker.PixelType.Vacuum && Mathf.Approximately(reward.spawnWeight, 0.2f)) reward.spawnWeight = 0.28f;
                    if (reward.type == PixelClicker.PixelType.Electric && Mathf.Approximately(reward.spawnWeight, 0.15f)) reward.spawnWeight = 0.2f;
                }
            }
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                {
                    if (t.type == PixelClicker.PixelType.Vacuum && Mathf.Approximately(t.spawnWeight, 0.2f)) t.spawnWeight = 0.28f;
                    if (t.type == PixelClicker.PixelType.Electric && Mathf.Approximately(t.spawnWeight, 0.15f)) t.spawnWeight = 0.2f;
                }
            packDataVersion = Mathf.Max(packDataVersion, 11);
            changed = true;
        }
        // The Meteor became a 3-click rock with glowing cracks (it paid 15 for one click, now 45 for three) and no longer glows as a flat colour.
        if (packDataVersion < 12)
        {
            if (packs != null)
                foreach (ShopPack pack in packs)
                {
                    if (pack == null || pack.rewardTiers == null) continue;
                    foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                    {
                        if (reward.type != PixelClicker.PixelType.Meteor) continue;
                        if (reward.clicksToCollect <= 1) reward.clicksToCollect = 3;
                        if (Mathf.Approximately((float)reward.amountPerClick, 15f)) reward.amountPerClick = 45;
                        reward.glow = false;
                    }
                }
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                {
                    if (t.type != PixelClicker.PixelType.Meteor) continue;
                    if (t.clicksToCollect <= 1) t.clicksToCollect = 3;
                    if (Mathf.Approximately((float)t.amountPerClick, 15f)) t.amountPerClick = 45;
                    t.glow = false;
                }
            packDataVersion = Mathf.Max(packDataVersion, 12);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradeBlockStats: packDataVersion steps 13, 14, 15 (in this order): Rock, the hazard blocks and the Farmer's Pack blocks. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo15()
    {
        bool changed = false;
        // Stone (the old Rock block) is Mining's filler: one click, tiny pay, and part of the Prospector's Pack.
        if (packDataVersion < 13)
        {
            if (packs != null)
                foreach (ShopPack pack in packs)
                {
                    if (pack == null || pack.rewardTiers == null || !Rewards(pack, PixelClicker.PixelType.Copper) || Rewards(pack, PixelClicker.PixelType.Rock)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> withStone = new System.Collections.Generic.List<PixelClicker.PixelTier>(pack.rewardTiers);
                    withStone.Insert(0, new PixelClicker.PixelTier
                    {
                        type = PixelClicker.PixelType.Rock, displayName = "Rock Pixels", color = new Color(0.55f, 0.55f, 0.58f, 1f),
                        clicksToCollect = 1, amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                    });
                    pack.rewardTiers = withStone.ToArray();
                }
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                {
                    if (t.type != PixelClicker.PixelType.Rock) continue;
                    t.displayName = "Rock Pixels";
                    t.clicksToCollect = 1;
                    t.amountPerClick = 1;
                    t.spawnWeight = 1f;
                }
            packDataVersion = Mathf.Max(packDataVersion, 13);
            changed = true;
        }
        // The hazard blocks (Gas Pocket for Mining, Pest for Farming) join the Prospector's and Farmer's Packs.
        if (packDataVersion < 14)
        {
            if (packs != null)
                foreach (ShopPack pack in packs)
                {
                    if (pack == null || pack.rewardTiers == null) continue;
                    PixelClicker.PixelTier add = null;
                    if (Rewards(pack, PixelClicker.PixelType.Copper) && !Rewards(pack, PixelClicker.PixelType.GasPocket))
                        add = new PixelClicker.PixelTier { type = PixelClicker.PixelType.GasPocket, displayName = "Gas Pocket", color = new Color(0.78f, 0.85f, 0.25f, 1f), clicksToCollect = 5, amountPerClick = 0, spawnWeight = 0.05f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly };
                    else if (Rewards(pack, PixelClicker.PixelType.Dirt) && !Rewards(pack, PixelClicker.PixelType.Pest))
                        add = new PixelClicker.PixelTier { type = PixelClicker.PixelType.Pest, displayName = "Pest", color = new Color(0.4f, 0.45f, 0.14f, 1f), clicksToCollect = 6, amountPerClick = 0, spawnWeight = 0.05f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly };
                    if (add == null) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(pack.rewardTiers) { add };
                    pack.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 14);
            changed = true;
        }

        // The Farmer's Pack keeps every farm block (a saved list could lack Seed or Water, which used to be sold alone) and Dirt takes a few digs.
        if (packDataVersion < 15)
        {
            if (packs != null)
                foreach (ShopPack pack in packs)
                {
                    if (pack == null || pack.rewardTiers == null || !Rewards(pack, PixelClicker.PixelType.Dirt)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(pack.rewardTiers);
                    foreach (PixelClicker.PixelTier def in CreateFarmerPack().rewardTiers)
                        if (!list.Exists(t => t.type == def.type)) list.Add(def);
                    foreach (PixelClicker.PixelTier t in list)
                        if (t.type == PixelClicker.PixelType.Dirt && t.clicksToCollect <= 1) t.clicksToCollect = 3;
                    pack.rewardTiers = list.ToArray();
                }
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                    if (t.type == PixelClicker.PixelType.Dirt && t.clicksToCollect <= 1) t.clicksToCollect = 3;
            packDataVersion = Mathf.Max(packDataVersion, 15);
            changed = true;
        }
        return changed;
    }

    /// <summary>Step 4 of EnsureDefaultPacks (pack data 16-19): Combo Meter order, the Lumberjack's, Forager's and Angler's Packs and the Prospector's ores. Returns true if anything changed.</summary>
    private bool UpgradeSkillPacks()
    {
        bool changed = false;
        changed |= UpgradePackDataTo18();
        changed |= UpgradePackDataTo19();
        return changed;
    }

    /// <summary>Part of UpgradeSkillPacks: packDataVersion steps 16, 17, 18 (in this order): Combo Meter order, the Lumberjack's and Forager's Packs. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo18()
    {
        bool changed = false;
        if (packDataVersion < 16)
        {
            // The Combo Meter is listed right above the Auto Clicker (the intro goals lead from one to the other).
            if (packs != null)
            {
                int comboAt = Array.FindIndex(packs, p => p != null && p.upgradeEffect == UpgradeEffect.ComboMeter);
                int autoAt = Array.FindIndex(packs, p => p != null && p.unlocksAutoClicker);
                if (comboAt >= 0 && autoAt >= 0 && comboAt > autoAt) MovePack(comboAt, autoAt);
            }
            packDataVersion = Mathf.Max(packDataVersion, 16);
            changed = true;
        }

        // The Wood Pixel pack becomes the Lumberjack's Pack (Woodcutting skill): six wood blocks and a hazard.
        if (packDataVersion < 17)
        {
            if (packs != null)
                foreach (ShopPack pack in packs)
                {
                    if (pack == null || pack.rewardTiers == null || !Rewards(pack, PixelClicker.PixelType.Wood) || Rewards(pack, PixelClicker.PixelType.Dirt)) continue;
                    ShopPack def = CreateWoodPixelPack();
                    pack.displayName = def.displayName;
                    pack.description = def.description;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(pack.rewardTiers);
                    foreach (PixelClicker.PixelTier t in def.rewardTiers)
                        if (!list.Exists(x => x.type == t.type)) list.Add(t);
                    foreach (PixelClicker.PixelTier t in list)
                        if (t.type == PixelClicker.PixelType.Wood && t.clicksToCollect <= 1) { t.clicksToCollect = 2; t.amountPerClick = 3; t.spawnWeight = 1f; }
                    pack.rewardTiers = list.ToArray();
                }
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                    if (t.type == PixelClicker.PixelType.Wood && t.clicksToCollect <= 1) { t.clicksToCollect = 2; t.amountPerClick = 3; t.spawnWeight = 1f; }
            packDataVersion = Mathf.Max(packDataVersion, 17);
            changed = true;
        }

        if (packDataVersion < 18)
        {
            // The Forager's Pack (Foraging skill) is new: add it to saved lists that lack it.
            if (packs != null && !System.Array.Exists(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Berry)))
            {
                System.Collections.Generic.List<ShopPack> withForager = new System.Collections.Generic.List<ShopPack>(packs) { CreateForagerPack() };
                packs = withForager.ToArray();
            }
            packDataVersion = Mathf.Max(packDataVersion, 18);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradeSkillPacks: packDataVersion step 19: the Angler's Pack and the Prospector's ores. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo19()
    {
        bool changed = false;
        if (packDataVersion < 19)
        {
            // New Angler's Pack (Fishing skill), and the Prospector's Pack changes its ores: Lead / Zinc / Nickel are out, Silver / Gold / Obsidian / Bedrock are in.
            if (packs != null && !System.Array.Exists(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Fish)))
            {
                System.Collections.Generic.List<ShopPack> withAngler = new System.Collections.Generic.List<ShopPack>(packs) { CreateAnglerPack() };
                packs = withAngler.ToArray();
            }
            int prospectorAt = packs == null ? -1 : System.Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Copper) && !Rewards(p, PixelClicker.PixelType.Fish));
            if (prospectorAt >= 0)
            {
                ShopPack prospector = packs[prospectorAt];
                ShopPack fresh = CreateProspectorPack();
                prospector.description = fresh.description;
                System.Collections.Generic.List<PixelClicker.PixelTier> ores = new System.Collections.Generic.List<PixelClicker.PixelTier>();
                foreach (PixelClicker.PixelTier t in prospector.rewardTiers)
                    if (!PixelClicker.IsRetiredOre(t.type)) ores.Add(t);
                foreach (PixelClicker.PixelTier t in fresh.rewardTiers)
                    if (!ores.Exists(x => x.type == t.type)) ores.Add(t);
                prospector.rewardTiers = ores.ToArray();
                // The old Obsidian Pixel pack goes: anything that needed it now needs the Prospector's Pack.
                int obsidianAt = System.Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Obsidian) && !Rewards(p, PixelClicker.PixelType.Copper));
                if (obsidianAt >= 0)
                {
                    System.Collections.Generic.List<ShopPack> kept = new System.Collections.Generic.List<ShopPack>(packs);
                    kept.RemoveAt(obsidianAt);
                    packs = kept.ToArray();
                    int newProspector = prospectorAt > obsidianAt ? prospectorAt - 1 : prospectorAt;
                    foreach (ShopPack other in packs)
                    {
                        if (other == null || other.requirements == null) continue;
                        foreach (PackRequirement r in other.requirements)
                        {
                            if (r.kind != RequirementKind.Pack) continue;
                            if (r.packIndex == obsidianAt) r.packIndex = newProspector;
                            else if (r.packIndex > obsidianAt) r.packIndex--;
                        }
                    }
                }
                // Obsidian is a 15-click rock now (and the ores' payouts follow the new pack).
                ShopPack source = CreateProspectorPack();
                foreach (PixelClicker.PixelTier def in source.rewardTiers)
                {
                    if (def.type != PixelClicker.PixelType.Obsidian) continue;
                    foreach (PixelClicker.PixelTier t in packs[System.Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Copper))].rewardTiers)
                        if (t.type == def.type) { t.clicksToCollect = def.clicksToCollect; t.amountPerClick = def.amountPerClick; t.spawnWeight = def.spawnWeight; }
                    if (clicker != null)
                        foreach (PixelClicker.PixelTier t in clicker.Tiers)
                            if (t.type == def.type) { t.clicksToCollect = def.clicksToCollect; t.amountPerClick = def.amountPerClick; t.spawnWeight = def.spawnWeight; }
                }
            }
            packDataVersion = Mathf.Max(packDataVersion, 19);
            changed = true;
        }
        return changed;
    }

    /// <summary>Step 5 of EnsureDefaultPacks (pack data 20-25): Bedrock and the Fishing blocks (Fish, Water, Jellyfish, Bait, Bottle, Whale, Driftwood). Returns true if anything changed.</summary>
    private bool UpgradeMiningAndFishingBlocks()
    {
        bool changed = false;
        changed |= UpgradePackDataTo21();
        changed |= UpgradePackDataTo23();
        changed |= UpgradePackDataTo25();
        return changed;
    }

    /// <summary>Part of UpgradeMiningAndFishingBlocks: packDataVersion steps 20, 21 (in this order): Bedrock's clicks, Water / Fish in the Angler's Pack. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo21()
    {
        bool changed = false;
        if (packDataVersion < 20)
        {
            // Bedrock needs a random 20-30 clicks now.
            PixelClicker.PixelTier def = null;
            foreach (PixelClicker.PixelTier t in CreateProspectorPack().rewardTiers) if (t.type == PixelClicker.PixelType.Bedrock) def = t;
            if (def != null)
            {
                void Apply(PixelClicker.PixelTier t)
                {
                    if (t == null || t.type != PixelClicker.PixelType.Bedrock) return;
                    t.clicksToCollect = def.clicksToCollect; t.randomClicks = true; t.randomClicksMin = def.randomClicksMin; t.randomClicksMax = def.randomClicksMax;
                }
                if (packs != null)
                    foreach (ShopPack p in packs)
                        if (p != null && p.rewardTiers != null) foreach (PixelClicker.PixelTier t in p.rewardTiers) Apply(t);
                if (clicker != null) foreach (PixelClicker.PixelTier t in clicker.Tiers) Apply(t);
            }
            packDataVersion = Mathf.Max(packDataVersion, 20);
            changed = true;
        }

        if (packDataVersion < 21)
        {
            // Fishing: Water replaces Driftwood as the filler, and Fish is a rare one-click catch (a Weak Spots round decides it).
            ShopPack def = CreateAnglerPack();
            PixelClicker.PixelTier fishDef = null, waterDef = null;
            foreach (PixelClicker.PixelTier t in def.rewardTiers)
            {
                if (t.type == PixelClicker.PixelType.Fish) fishDef = t;
                if (t.type == PixelClicker.PixelType.Water) waterDef = t;
            }
            void ApplyFish(PixelClicker.PixelTier t)
            {
                if (t == null || t.type != PixelClicker.PixelType.Fish || fishDef == null) return;
                t.clicksToCollect = fishDef.clicksToCollect; t.amountPerClick = fishDef.amountPerClick; t.spawnWeight = fishDef.spawnWeight;
            }
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Fish)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers);
                    list.RemoveAll(t => t != null && t.type == PixelClicker.PixelType.Driftwood);
                    if (waterDef != null && !list.Exists(t => t != null && t.type == PixelClicker.PixelType.Water)) list.Add(waterDef.Clone());
                    p.rewardTiers = list.ToArray();
                    foreach (PixelClicker.PixelTier t in p.rewardTiers) ApplyFish(t);
                }
            if (clicker != null) foreach (PixelClicker.PixelTier t in clicker.Tiers) ApplyFish(t);
            packDataVersion = Mathf.Max(packDataVersion, 21);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradeMiningAndFishingBlocks: packDataVersion steps 22, 23 (in this order): Jellyfish, Bait, Bottle and Whale. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo23()
    {
        bool changed = false;
        if (packDataVersion < 22)
        {
            // Fishing: the Jellyfish takes 9 clicks and the Bait block joins the pack.
            ShopPack def = CreateAnglerPack();
            PixelClicker.PixelTier jellyDef = null, baitDef = null;
            foreach (PixelClicker.PixelTier t in def.rewardTiers)
            {
                if (t.type == PixelClicker.PixelType.Jellyfish) jellyDef = t;
                if (t.type == PixelClicker.PixelType.Bait) baitDef = t;
            }
            void ApplyJelly(PixelClicker.PixelTier t)
            {
                if (t != null && t.type == PixelClicker.PixelType.Jellyfish && jellyDef != null) t.clicksToCollect = jellyDef.clicksToCollect;
            }
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Fish)) continue;
                    if (baitDef != null && !Rewards(p, PixelClicker.PixelType.Bait))
                    {
                        System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers) { baitDef.Clone() };
                        p.rewardTiers = list.ToArray();
                    }
                    foreach (PixelClicker.PixelTier t in p.rewardTiers) ApplyJelly(t);
                }
            if (clicker != null) foreach (PixelClicker.PixelTier t in clicker.Tiers) ApplyJelly(t);
            packDataVersion = Mathf.Max(packDataVersion, 22);
            changed = true;
        }

        if (packDataVersion < 23)
        {
            // Fishing's special blocks: the message in a bottle and the whale join the Angler's Pack.
            ShopPack def = CreateAnglerPack();
            PixelClicker.PixelTier bottleDef = null, whaleDef = null;
            foreach (PixelClicker.PixelTier t in def.rewardTiers)
            {
                if (t.type == PixelClicker.PixelType.Bottle) bottleDef = t;
                if (t.type == PixelClicker.PixelType.Whale) whaleDef = t;
            }
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Fish)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers);
                    if (bottleDef != null && !Rewards(p, PixelClicker.PixelType.Bottle)) list.Add(bottleDef.Clone());
                    if (whaleDef != null && !Rewards(p, PixelClicker.PixelType.Whale)) list.Add(whaleDef.Clone());
                    p.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 23);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradeMiningAndFishingBlocks: packDataVersion steps 24, 25 (in this order): Driftwood back and the Whale's clicks. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo25()
    {
        bool changed = false;
        if (packDataVersion < 24)
        {
            // Fishing's fixed spawn table brings Driftwood back (5% of the pixels) as a second filler.
            ShopPack def = CreateAnglerPack();
            PixelClicker.PixelTier driftDef = null;
            foreach (PixelClicker.PixelTier t in def.rewardTiers) if (t.type == PixelClicker.PixelType.Driftwood) driftDef = t;
            if (packs != null && driftDef != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Fish) || Rewards(p, PixelClicker.PixelType.Driftwood)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers) { driftDef.Clone() };
                    p.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 24);
            changed = true;
        }

        if (packDataVersion < 25)
        {
            // The Whale takes a random 15-50 clicks (was 15-100).
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null) continue;
                    foreach (PixelClicker.PixelTier t in p.rewardTiers) ApplyWhaleClicks(t);
                }
            if (clicker != null) foreach (PixelClicker.PixelTier t in clicker.Tiers) ApplyWhaleClicks(t);
            packDataVersion = Mathf.Max(packDataVersion, 25);
            changed = true;
        }
        return changed;
    }

    /// <summary>Step 6 of EnsureDefaultPacks (pack data 26-33): Foraging / Woodcutting blocks, the Multi-Click cap and the testing packs leaving the shop. Returns true if anything changed.</summary>
    private bool UpgradeGatheringBlocks()
    {
        bool changed = false;
        changed |= UpgradePackDataTo28();
        changed |= UpgradePackDataTo30();
        changed |= UpgradePackDataTo33();
        return changed;
    }

    /// <summary>Part of UpgradeGatheringBlocks: packDataVersion steps 26, 27, 28 (in this order): Mold, Whetstone and Acorn join their packs. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo28()
    {
        bool changed = false;
        if (packDataVersion < 26)
        {
            // Foraging: the Mold block joins the Forager's Pack (the Toadstool is switched off in the Skills pack, its tier stays).
            PixelClicker.PixelTier moldDef = null;
            foreach (PixelClicker.PixelTier t in CreateForagerPack().rewardTiers) if (t.type == PixelClicker.PixelType.Mold) moldDef = t;
            if (packs != null && moldDef != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Berry) || Rewards(p, PixelClicker.PixelType.Mold)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers) { moldDef.Clone() };
                    p.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 26);
            changed = true;
        }

        if (packDataVersion < 27)
        {
            // Woodcutting: the Whetstone joins the Lumberjack's Pack as a base block.
            PixelClicker.PixelTier stoneDef = null;
            foreach (PixelClicker.PixelTier t in CreateWoodPixelPack().rewardTiers) if (t.type == PixelClicker.PixelType.Whetstone) stoneDef = t;
            if (packs != null && stoneDef != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Wood) || Rewards(p, PixelClicker.PixelType.Whetstone)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers) { stoneDef.Clone() };
                    p.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 27);
            changed = true;
        }

        if (packDataVersion < 28)
        {
            // Woodcutting: the Acorn joins the Lumberjack's Pack as a base block.
            PixelClicker.PixelTier acornDef = null;
            foreach (PixelClicker.PixelTier t in CreateWoodPixelPack().rewardTiers) if (t.type == PixelClicker.PixelType.Acorn) acornDef = t;
            if (packs != null && acornDef != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Wood) || Rewards(p, PixelClicker.PixelType.Acorn)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers) { acornDef.Clone() };
                    p.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 28);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradeGatheringBlocks: packDataVersion steps 29, 30 (in this order): the Multi-Click cap and the Pesticide. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo30()
    {
        bool changed = false;
        if (packDataVersion < 29)
        {
            // The auto clicker's Multi-Click tops out at 3x for now: cut the levels above it (a saved level is clamped when the save loads).
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.upgradeEffect != UpgradeEffect.AutoClickerClicks || p.levels == null) continue;
                    System.Collections.Generic.List<PackLevel> keep = new System.Collections.Generic.List<PackLevel>();
                    foreach (PackLevel l in p.levels) if (l != null && l.value <= 3.001f) keep.Add(l);
                    if (keep.Count > 0 && keep.Count < p.levels.Length) { p.levels = keep.ToArray(); p.level = Mathf.Clamp(p.level, 0, p.levels.Length); }
                }
            packDataVersion = Mathf.Max(packDataVersion, 29);
            changed = true;
        }

        if (packDataVersion < 30)
        {
            // Woodcutting: the Pesticide joins the Lumberjack's Pack as a base block.
            PixelClicker.PixelTier pestDef = null;
            foreach (PixelClicker.PixelTier t in CreateWoodPixelPack().rewardTiers) if (t.type == PixelClicker.PixelType.Pesticide) pestDef = t;
            if (packs != null && pestDef != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Wood) || Rewards(p, PixelClicker.PixelType.Pesticide)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers) { pestDef.Clone() };
                    p.rewardTiers = list.ToArray();
                }
            packDataVersion = Mathf.Max(packDataVersion, 30);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradeGatheringBlocks: packDataVersion steps 31, 32, 33 (in this order): Pinecone / Moss, the Termite Hive and the testing packs leaving the shop. Returns true if anything changed.</summary>
    private bool UpgradePackDataTo33()
    {
        bool changed = false;
        if (packDataVersion < 31)
        {
            // Woodcutting: Pinecone and Moss (the other two fillers) join the Lumberjack's Pack, and the Hornet Nest became the Termite Hive.
            System.Collections.Generic.List<PixelClicker.PixelTier> fillerDefs = new System.Collections.Generic.List<PixelClicker.PixelTier>();
            foreach (PixelClicker.PixelTier t in CreateWoodPixelPack().rewardTiers) if (t.type == PixelClicker.PixelType.Pinecone || t.type == PixelClicker.PixelType.Moss) fillerDefs.Add(t);
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Wood)) continue;
                    System.Collections.Generic.List<PixelClicker.PixelTier> list = new System.Collections.Generic.List<PixelClicker.PixelTier>(p.rewardTiers);
                    foreach (PixelClicker.PixelTier d in fillerDefs) if (!Rewards(p, d.type)) list.Add(d.Clone());
                    foreach (PixelClicker.PixelTier t in list) if (t != null && t.type == PixelClicker.PixelType.HornetNest && t.displayName == "Hornet Nest") t.displayName = "Termite Hive";
                    p.rewardTiers = list.ToArray();
                    if (p.description != null) p.description = p.description.Replace("Hornet Nest", "Termite Hive");
                }
            packDataVersion = Mathf.Max(packDataVersion, 31);
            changed = true;
        }

        if (packDataVersion < 32)
        {
            // The Termite Hive takes 4 hits to break now (was 5).
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && p.rewardTiers != null)
                        foreach (PixelClicker.PixelTier t in p.rewardTiers)
                            if (t != null && t.type == PixelClicker.PixelType.HornetNest && t.clicksToCollect == 5) t.clicksToCollect = 4;
            packDataVersion = Mathf.Max(packDataVersion, 32);
            changed = true;
        }

        if (packDataVersion < 33)
        {
            // The testing packs (Fire, Nuclear, Farmer's) and Block Hold (free from the start) leave the shop: unlock them with the Dev Tools 'Shop unlocks' widget.
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && IsDevOnlyPack(p)) p.hideInShop = true;
            packDataVersion = Mathf.Max(packDataVersion, 33);
            changed = true;
        }
        return changed;
    }

    /// <summary>Step 7 of EnsureDefaultPacks (pack data 34-40, applied in this order: 35, 40, 39, 38, 37, 36, 34): prices, names, descriptions and colours. Returns true if anything changed.</summary>
    private bool UpgradePackPricesAndNames()
    {
        bool changed = false;
        changed |= UpgradeSoldSkillPacksAndCharcoal();
        changed |= UpgradeRgbAndLumberjackTexts();
        changed |= UpgradeColoursAndDevPacks();
        return changed;
    }

    /// <summary>Part of UpgradePackPricesAndNames: packDataVersion steps 35, 42, 41 (in this order): skill packs sold for real, the Firemaker's Pack and Charcoal. Returns true if anything changed.</summary>
    private bool UpgradeSoldSkillPacksAndCharcoal()
    {
        bool changed = false;
        if (packDataVersion < 35)
        {
            // Foraging's hazards take ONE click each (and the Toadstool is back), the Prospector's / Farmer's Packs are sold for real, the Fire Pixel test pack
            // became the Firemaker's Pack, Lead / Zinc / Nickel are back in Mining and Stone is called Rock again.
            if (packs != null)
                for (int i = 0; i < packs.Length; i++)
                {
                    ShopPack p = packs[i];
                    if (p == null) continue;
                    if (Rewards(p, PixelClicker.PixelType.Fire))
                    {
                        ShopPack fresh = CreateFirePixelPack();
                        p.displayName = fresh.displayName; p.description = fresh.description; p.rewardTiers = fresh.rewardTiers; p.hideInShop = true;
                    }
                    if (Rewards(p, PixelClicker.PixelType.Copper))
                    {
                        ShopPack fresh = CreateProspectorPack();
                        p.hideInShop = false; p.costs = fresh.costs; p.description = fresh.description; p.rewardTiers = fresh.rewardTiers;
                    }
                    if (Rewards(p, PixelClicker.PixelType.Dirt))
                    {
                        ShopPack fresh = CreateFarmerPack();
                        p.hideInShop = false; p.costs = fresh.costs; p.description = fresh.description;
                    }
                    if (Rewards(p, PixelClicker.PixelType.Berry))
                    {
                        ShopPack fresh = CreateForagerPack();
                        p.rewardTiers = fresh.rewardTiers; p.description = fresh.description;
                    }
                }
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                {
                    if (t.type == PixelClicker.PixelType.Mold || t.type == PixelClicker.PixelType.Toadstool) t.clicksToCollect = 1;
                    if (t.type == PixelClicker.PixelType.Rock) t.displayName = "Rock Pixels";
                }
            packDataVersion = Mathf.Max(packDataVersion, 35);
            changed = true;
        }

        if (packDataVersion < 42)
        {
            // The Charcoal Pixel's husks are the most efficient fuel (8 wood husks in a Biomass Generator): its saved description says so.
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && p.displayName == "Charcoal Pixel") p.description = CreateCharcoalPixelPack(-1).description;
            packDataVersion = Mathf.Max(packDataVersion, 42);
            changed = true;
        }

        if (packDataVersion < 41)
        {
            // Charcoal is its own shop pixel now (2026-10-24, bought with the Kiln's charcoal material): the Firemaker's Pack no longer rewards it.
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || p.rewardTiers == null || !Rewards(p, PixelClicker.PixelType.Fire)) continue;
                    p.rewardTiers = Array.FindAll(p.rewardTiers, t => t == null || t.type != PixelClicker.PixelType.Charcoal);
                    if (p.description != null) p.description = p.description.Replace("Ember, Fire and Charcoal", "Ember and Fire");
                }
            packDataVersion = Mathf.Max(packDataVersion, 41);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradePackPricesAndNames: packDataVersion steps 40, 39, 38 (in this order): the RGB unlock and Auto Clicker price, Skills-window wording, the Lumberjack's Pack name and price. Returns true if anything changed.</summary>
    private bool UpgradeRgbAndLumberjackTexts()
    {
        bool changed = false;
        if (packDataVersion < 40)
        {
            // RGB is an unlock now (2026-10-16): Cubie's second visit gives the RGB Pack (hidden from the shop), the shop opens with it, and the Auto
            // Clicker - the shop's first buy now - costs 150 of each of the six pure pixels (was 1,000).
            if (requiredTier == PixelClicker.PixelType.Black) requiredTier = PixelClicker.PixelType.Red;
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null) continue;
                    if (!IsLeveled(p) && p.rewardTiers != null && Array.FindIndex(p.rewardTiers, t => t != null && t.type == PixelClicker.PixelType.Red) >= 0)
                    { p.hideInShop = true; p.description = CreateRgbPack().description; }
                    if (p.unlocksAutoClicker) p.costs = CreateAutoClickerPack(-1).costs;
                }
            packDataVersion = Mathf.Max(packDataVersion, 40);
            changed = true;
        }

        if (packDataVersion < 39)
        {
            // Skill packs are chosen in the Skills window only: saved descriptions still said '(Toggles or Skills window)'.
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && p.description != null) p.description = p.description.Replace("(Toggles or Skills window)", "(Skills window)");
            packDataVersion = Mathf.Max(packDataVersion, 39);
            changed = true;
        }

        if (packDataVersion < 38)
        {
            // The Woodcutter's Pack is the Lumberjack's Pack now and costs 250 of each pure pixel.
            if (packs != null)
                foreach (ShopPack p in packs)
                {
                    if (p == null || (p.displayName != "Woodcutter's Pack" && p.displayName != "Lumberjack's Pack")) continue;
                    ShopPack fresh = CreateWoodPixelPack();
                    p.displayName = fresh.displayName;
                    p.costs = fresh.costs;
                    if (p.description != null) p.description = p.description.Replace("Woodcutter's Pack", "Lumberjack's Pack");
                }
            packDataVersion = Mathf.Max(packDataVersion, 38);
            changed = true;
        }
        return changed;
    }

    /// <summary>Part of UpgradePackPricesAndNames: packDataVersion steps 37, 36, 34 (in this order): Mold / Truffle colours, the Lumberjack's Pack price and the Prospector's Pack out of the shop. Returns true if anything changed.</summary>
    private bool UpgradeColoursAndDevPacks()
    {
        bool changed = false;
        if (packDataVersion < 37)
        {
            // Mold and Truffle got clearly different colours (their Inventory swatches too): Mold pale blue-green, Truffle near-black.
            if (clicker != null)
                foreach (PixelClicker.PixelTier t in clicker.Tiers)
                {
                    if (t.type == PixelClicker.PixelType.Mold) t.color = new Color(0.62f, 0.92f, 0.78f, 1f);
                    if (t.type == PixelClicker.PixelType.Truffle) t.color = new Color(0.16f, 0.1f, 0.07f, 1f);
                }
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && p.rewardTiers != null)
                        foreach (PixelClicker.PixelTier t in p.rewardTiers)
                        {
                            if (t == null) continue;
                            if (t.type == PixelClicker.PixelType.Mold) t.color = new Color(0.62f, 0.92f, 0.78f, 1f);
                            if (t.type == PixelClicker.PixelType.Truffle) t.color = new Color(0.16f, 0.1f, 0.07f, 1f);
                        }
            packDataVersion = Mathf.Max(packDataVersion, 37);
            changed = true;
        }

        if (packDataVersion < 36)
        {
            // The Lumberjack's Pack (the first skill pack, Cubie's 'starter set') costs a little more: 250 White (was 150).
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && (p.displayName == "Woodcutter's Pack" || p.displayName == "Lumberjack's Pack")) p.costs = CreateWoodPixelPack().costs;
            packDataVersion = Mathf.Max(packDataVersion, 36);
            changed = true;
        }

        if (packDataVersion < 34)
        {
            // The Prospector's Pack (testing price) leaves the shop too.
            if (packs != null)
                foreach (ShopPack p in packs)
                    if (p != null && IsDevOnlyPack(p)) p.hideInShop = true;
            packDataVersion = Mathf.Max(packDataVersion, 34);
            changed = true;
        }
        return changed;
    }

    /// <summary>Packs kept out of the shop (testing packs: Fire, Nuclear, Farmer's, Prospector's; and Block Hold); the Dev Tools 'Shop unlocks' widget handles them.</summary>
    private static bool IsDevOnlyPack(ShopPack p) =>
        p.unlocksBlockHold || Rewards(p, PixelClicker.PixelType.Fire) || Rewards(p, PixelClicker.PixelType.Nuclear);   // (the Farmer's and Prospector's Packs are in the shop now, with real prices)

    private static void ApplyWhaleClicks(PixelClicker.PixelTier t)
    {
        if (t == null || t.type != PixelClicker.PixelType.Whale) return;
        t.randomClicks = true;
        t.randomClicksMin = Mathf.Min(t.randomClicksMin, 50);
        t.randomClicksMax = 50;
        t.clicksToCollect = 33;   // (the average of 15-50, used for estimates)
    }
}
