// ---- System notes ----
// PixelShopMigrate.cs
//   Part of PixelShop (partial class): MigrateOldPackData (older pack fields, renames and retired packs) and MovePack. The system notes are in PixelShop.cs.
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
    /// <summary>Moves the pack at 'from' to position 'to' in the list and renumbers every pack requirement that refers to a pack index.</summary>
    private void MovePack(int from, int to)
    {
        if (packs == null || from == to || from < 0 || to < 0 || from >= packs.Length || to >= packs.Length) return;
        int[] map = new int[packs.Length]; // old index -> new index
        System.Collections.Generic.List<int> order = new System.Collections.Generic.List<int>();
        for (int i = 0; i < packs.Length; i++) order.Add(i);
        order.RemoveAt(from);
        order.Insert(to, from);
        for (int newIndex = 0; newIndex < order.Count; newIndex++) map[order[newIndex]] = newIndex;

        ShopPack[] moved = new ShopPack[packs.Length];
        for (int newIndex = 0; newIndex < order.Count; newIndex++) moved[newIndex] = packs[order[newIndex]];
        foreach (ShopPack pack in moved)
        {
            if (pack == null || pack.requirements == null) continue;
            foreach (PackRequirement r in pack.requirements)
                if (r.kind == RequirementKind.Pack && r.packIndex >= 0 && r.packIndex < map.Length) r.packIndex = map[r.packIndex];
        }
        packs = moved;
    }

    /// <summary>Brings packs saved by older versions up to date (renames, tab, minigame ids). Returns true if anything changed.</summary>
    private bool MigrateOldPackData()
    {
        bool renamed = false;

        renamed |= MigrateRenamedAndRetiredPacks();
        renamed |= MigrateElectricianAndGlassPacks();
        renamed |= MigrateOldPackNumbers();
        renamed |= MigrateOldPackFields();

        return renamed;
    }

    /// <summary>Step 1 of MigrateOldPackData: renamed packs (UFO Helper, husk wording) and retired packs taken out of the list (Pong, the visitors, single Seed / Water / Rock / Electric packs...). Returns true if anything changed.</summary>
    private bool MigrateRenamedAndRetiredPacks()
    {
        bool renamed = false;
        // The Robot Worker is the UFO Helper now (saves are matched by pack name, PixelSaveGame knows the old one too).
        if (packs != null)
            foreach (ShopPack rp in packs)
                if (rp != null && rp.unlocksRobot && rp.displayName == "Robot Worker") { rp.displayName = "UFO Helper"; renamed = true; }


        // 'Old pixel' is 'husk' in every player text now: packs saved in the scene keep their old texts otherwise.
        if (packs != null)
            foreach (ShopPack hp in packs)
            {
                if (hp == null) continue;
                string name = PixelClicker.HuskWording(hp.displayName), text = PixelClicker.HuskWording(hp.description);
                if (name != hp.displayName || text != hp.description) { hp.displayName = name; hp.description = text; renamed = true; }
            }

        // The short-lived Pong pack (replaced by Breakout) may still be saved in the scene's pack list: take it out, and fix the
        // pack numbers the other packs' requirements point at.
        if (packs != null)
        {
            for (int i = packs.Length - 1; i >= 0; i--)
            {
                ShopPack pack = packs[i];
                if (!IsRetiredPack(pack)) continue;
                System.Collections.Generic.List<ShopPack> kept = new System.Collections.Generic.List<ShopPack>(packs);
                kept.RemoveAt(i);
                packs = kept.ToArray();
                foreach (ShopPack other in packs)
                {
                    if (other == null || other.requirements == null) continue;
                    System.Collections.Generic.List<PackRequirement> reqs = new System.Collections.Generic.List<PackRequirement>();
                    foreach (PackRequirement r in other.requirements)
                    {
                        if (r.kind == RequirementKind.Pack)
                        {
                            if (r.packIndex == i) continue;          // it needed Pong: drop that requirement
                            if (r.packIndex > i) r.packIndex--;      // the packs after it moved up one
                        }
                        reqs.Add(r);
                    }
                    other.requirements = reqs.ToArray();
                }
                renamed = true;
            }
        }
        return renamed;
    }

    /// <summary>Part of MigrateRenamedAndRetiredPacks: true for a saved pack that is no longer sold and leaves the list (Pong, the visitors, single Seed / Water / Rock / Electric packs, Grabbing, Old Pixel Capacity, a standalone Crafting pack).</summary>
    private static bool IsRetiredPack(ShopPack pack)
    {
        return pack != null && (string.Equals(pack.displayName, "Pong", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(pack.unlocksMinigame, "pong", StringComparison.OrdinalIgnoreCase)
                                // The visitors (Wizard, Tinkerer, Pixel Entrepreneur) are no longer bought: Cubie's guide starts them.
                                || string.Equals(pack.unlocksMinigame, "wizard", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(pack.unlocksMinigame, "tinkerer", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(pack.unlocksMinigame, "entrepreneur", StringComparison.OrdinalIgnoreCase)
                                // Seed and Water are part of the Farmer's Pack (Farming skill) now, no longer sold on their own.
                                || (pack.rewardTiers != null && pack.rewardTiers.Length == 1
                                    && (pack.rewardTiers[0].type == PixelClicker.PixelType.Seed || pack.rewardTiers[0].type == PixelClicker.PixelType.Water))
                                // Pixel Grabbing is unlocked from the start now (it keeps its switch in the Toggles window).
                                || pack.unlocksGrabbing
                                // Old Pixel Capacity is no longer sold: it will come back as a Standard skill reward (see the to-do list).
                                || pack.upgradeEffect == UpgradeEffect.OldPixelCap
                                // Crafting comes with the Glass pixel now (a standalone Crafting pack has no reward pixels).
                                || (pack.unlocksCrafting && (pack.rewardTiers == null || pack.rewardTiers.Length == 0))
                                // Stone (the old Rock block) is Mining's filler now: it comes with the Prospector's Pack, no longer sold on its own.
                                || (pack.rewardTiers != null && pack.rewardTiers.Length == 1 && pack.rewardTiers[0].type == PixelClicker.PixelType.Rock)
                                // Electric is part of the Electrician's Pack now, no longer sold on its own.
                                || (pack.rewardTiers != null && pack.rewardTiers.Length == 1 && pack.rewardTiers[0].type == PixelClicker.PixelType.Electric
                                    && string.IsNullOrEmpty(pack.skillId)));
    }

    /// <summary>Step 2 of MigrateOldPackData: the Electrician's Pack's pixels / price / requirement (and the Mirror needing it) and the Glass pack unlocking Crafting. Returns true if anything changed.</summary>
    private bool MigrateElectricianAndGlassPacks()
    {
        bool renamed = false;
        // A saved Electrician's Pack from before it held every electrical pixel (and Electric itself) is brought up to date: its pixels, price and the Auto Clicker requirement;
        // the Mirror pixel then needs it (it used to need the Electric Pixel pack, which is gone).
        if (packs != null)
        {
            int electricianAt = Array.FindIndex(packs, p => p != null && p.skillId == PixelSkills.ElectricianId);
            int autoAt = Array.FindIndex(packs, p => p != null && p.unlocksAutoClicker);
            if (electricianAt >= 0)
            {
                ShopPack ep = packs[electricianAt];
                if (ep.rewardTiers == null || ep.rewardTiers.Length < 8)
                {
                    ep.rewardTiers = ElectricianTiers();
                    ep.costs = ElectricianCosts();
                    ep.requirements = Needs(autoAt);
                    ep.description = CreateElectricianPack(autoAt).description;
                    renamed = true;
                }
                int mirrorAt = Array.FindIndex(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Mirror));
                if (mirrorAt >= 0)
                {
                    ShopPack mp = packs[mirrorAt];
                    bool hasPackReq = false;
                    if (mp.requirements != null) foreach (PackRequirement r in mp.requirements) if (r.kind == RequirementKind.Pack && r.packIndex >= 0) hasPackReq = true;
                    if (!hasPackReq)
                    {
                        System.Collections.Generic.List<PackRequirement> reqs = new System.Collections.Generic.List<PackRequirement>(mp.requirements ?? new PackRequirement[0]);
                        reqs.Add(new PackRequirement { kind = RequirementKind.Pack, packIndex = electricianAt });
                        mp.requirements = reqs.ToArray();
                        renamed = true;
                    }
                }
            }
        }

        // The Glass pack unlocks Crafting (price raised to match); older saved Glass packs are brought up to date unless the price was edited.
        if (packs != null)
        {
            ShopPack glassPack = Array.Find(packs, p => p != null && Rewards(p, PixelClicker.PixelType.Glass));
            if (glassPack != null && !glassPack.unlocksCrafting)
            {
                glassPack.unlocksCrafting = true;
                if (glassPack.costs != null && glassPack.costs.Length == 3 && glassPack.costs[0].amount == 2500) glassPack.costs = GlassCosts();
                if (glassPack.description == "Adds the see-through Glass pixel to the random spawn pool.") glassPack.description = CreateGlassPack(-1).description;
                renamed = true;
            }
        }
        return renamed;
    }

    /// <summary>Step 3 of MigrateOldPackData (pack data 1-5): Obsidian's pay, Singularity's spawn weight and the Electric pixel's payout, price and place in the list. Returns true if anything changed.</summary>
    private bool MigrateOldPackNumbers()
    {
        bool renamed = false;
        renamed |= MigratePackDataTo3();
        renamed |= MigratePackDataTo5();
        return renamed;
    }

    /// <summary>Part of MigrateOldPackNumbers: packDataVersion steps 1, 2, 3 (in this order): Obsidian's pay, Singularity's spawn weight and the Electric payout. Returns true if anything changed.</summary>
    private bool MigratePackDataTo3()
    {
        bool renamed = false;
        // Obsidian now pays +7 per 5 clicks (it was +5); only changes the old default, not a value you typed.
        if (packDataVersion < 1)
        {
            foreach (ShopPack pack in packs)
            {
                if (pack.rewardTiers == null) continue;
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                    if (reward.type == PixelClicker.PixelType.Obsidian && reward.amountPerClick == 5d && reward.clicksToCollect == 5)
                        reward.amountPerClick = 7d;
            }
            packDataVersion = 1;
            renamed = true;
        }

        // Singularity pixels are rarer now (spawn weight 0.2 -> 0.08); only the old default is changed, not a value you typed.
        if (packDataVersion < 2)
        {
            foreach (ShopPack pack in packs)
            {
                if (pack.rewardTiers == null) continue;
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                    if (reward.type == PixelClicker.PixelType.Singularity && Mathf.Approximately(reward.spawnWeight, 0.2f))
                        reward.spawnWeight = 0.08f;
            }
            packDataVersion = 2;
            renamed = true;
        }

        // Electric pixels pay a random 1-15 per harvest (they paid a flat 10); only the old default is changed.
        if (packDataVersion < 3)
        {
            foreach (ShopPack pack in packs)
            {
                if (pack.rewardTiers == null) continue;
                foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                    if (reward.type == PixelClicker.PixelType.Electric)
                    {
                        reward.randomPayout = true;
                        reward.payoutMin = 1;
                        reward.payoutMax = 15;
                        if (reward.amountPerClick == 10d) reward.amountPerClick = 8d;
                    }
            }
            packDataVersion = 3;
            renamed = true;
        }
        return renamed;
    }

    /// <summary>Part of MigrateOldPackNumbers: packDataVersion steps 4, 5 (in this order): the Electric pixel's price, requirement and place in the list. Returns true if anything changed.</summary>
    private bool MigratePackDataTo5()
    {
        bool renamed = false;
        // The Electric pixel is a proper pack now (it was a 10 White test pack): real price, needs the Luminescent pack, rarer.
        // Only the old test values are replaced, not anything you typed.
        if (packDataVersion < 4)
        {
            int luminescentIndex = Array.FindIndex(packs, p => Rewards(p, PixelClicker.PixelType.Luminescent));
            foreach (ShopPack pack in packs)
            {
                if (!Rewards(pack, PixelClicker.PixelType.Electric)) continue;
                bool oldCost = pack.costs != null && pack.costs.Length == 1 && pack.costs[0].type == PixelClicker.PixelType.White && pack.costs[0].amount == 10d;
                if (oldCost)
                {
                    ShopPack fresh = CreateElectricPixelPack(luminescentIndex);
                    pack.costs = fresh.costs;
                    pack.description = fresh.description;
                    pack.requirements = fresh.requirements;
                    foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                        if (reward.type == PixelClicker.PixelType.Electric && Mathf.Approximately(reward.spawnWeight, 0.5f)) reward.spawnWeight = 0.35f;
                }
            }
            packDataVersion = 4;
            renamed = true;
        }

        // The Electric pixel is an EARLY unlock (a slight boost to the auto clicker): cheaper, needs the Auto Clicker pack, rarer,
        // and sits in the list just before the Vacuum pixel. Only the previous default values are replaced.
        if (packDataVersion < 5)
        {
            int autoIndex = Array.FindIndex(packs, p => p != null && p.unlocksAutoClicker);
            int electricIndex = Array.FindIndex(packs, p => Rewards(p, PixelClicker.PixelType.Electric));
            if (electricIndex >= 0)
            {
                ShopPack pack = packs[electricIndex];
                bool previousCost = pack.costs != null && pack.costs.Length == 2 && pack.costs[0].type == PixelClicker.PixelType.Obsidian && pack.costs[0].amount == 2000000d;
                if (previousCost)
                {
                    ShopPack fresh = CreateElectricPixelPack(autoIndex);
                    pack.costs = fresh.costs;
                    pack.requirements = fresh.requirements;
                    foreach (PixelClicker.PixelTier reward in pack.rewardTiers)
                        if (reward.type == PixelClicker.PixelType.Electric && Mathf.Approximately(reward.spawnWeight, 0.35f)) reward.spawnWeight = 0.15f;
                }

                // Move it to just before the Vacuum pack (requirements refer to packs by index, so they are renumbered).
                int vacuumIndex = Array.FindIndex(packs, p => Rewards(p, PixelClicker.PixelType.Vacuum));
                if (vacuumIndex >= 0 && electricIndex > vacuumIndex) MovePack(electricIndex, vacuumIndex);
            }
            packDataVersion = 5;
            renamed = true;
        }
        return renamed;
    }

    /// <summary>Step 4 of MigrateOldPackData: the old minigame tick boxes and single requirement fields become ids / the requirements list, and the Glass / Vacuum names and tab. Returns true if anything changed.</summary>
    private bool MigrateOldPackFields()
    {
        bool renamed = false;
        // Turn the old per-minigame tick boxes into minigame ids.
        foreach (ShopPack pack in packs)
        {
            if (pack.unlocksGhostMinigame) { pack.unlocksMinigame = "ghost"; pack.unlocksGhostMinigame = false; renamed = true; }
            if (pack.unlocksBlackholeMinigame) { pack.unlocksMinigame = "blackhole"; pack.unlocksBlackholeMinigame = false; renamed = true; }
            if (pack.requiresGhosts) { pack.requiresMinigameGoal = "ghost"; pack.requiresGhosts = false; renamed = true; }
            if (pack.requiresSingularity) { pack.requiresMinigameGoal = "blackhole"; pack.requiresSingularity = false; renamed = true; }
        }

        // Turn the old single requirement fields into the requirements list.
        foreach (ShopPack pack in packs)
        {
            bool hadPack = pack.requiresPackIndex >= 0;
            bool hadGoal = !string.IsNullOrEmpty(pack.requiresMinigameGoal);
            if (!hadPack && !hadGoal) continue;

            System.Collections.Generic.List<PackRequirement> list = new System.Collections.Generic.List<PackRequirement>();
            if (pack.requirements != null) list.AddRange(pack.requirements);
            if (hadPack) list.Add(new PackRequirement { kind = RequirementKind.Pack, packIndex = pack.requiresPackIndex });
            if (hadGoal) list.Add(new PackRequirement { kind = RequirementKind.MinigameGoal, minigameId = pack.requiresMinigameGoal });

            pack.requirements = list.ToArray();
            pack.requiresPackIndex = -1;
            pack.requiresMinigameGoal = "";
            renamed = true;
        }

        // Older components saved the previous names; Glass and Vacuum are single pixels listed on the Pixels tab.
        foreach (ShopPack pack in packs)
        {
            if (pack.rewardTiers == null) continue;
            bool isGlass = Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Glass);
            bool isVacuum = Array.Exists(pack.rewardTiers, r => r.type == PixelClicker.PixelType.Vacuum);
            if (!isGlass && !isVacuum) continue;

            if (pack.displayName == "Glass Pack") { pack.displayName = "Glass Pixel"; renamed = true; }
            if (pack.displayName == "Vacuum Pack") { pack.displayName = "Vacuum Pixel"; renamed = true; }
            if (pack.tab != ShopTab.Pixels) { pack.tab = ShopTab.Pixels; renamed = true; }
        }
        return renamed;
    }
}
