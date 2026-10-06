using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The shop's built-in packs (RGB, Auto Clicker, Glass, Vacuum, Obsidian, Luminescent, Ghost Hunt, Black Hole,
/// Singularity Pixel, Ghost Pixel, the auto clicker upgrades). Part of <see cref="PixelShop"/>: edit a pack's numbers
/// here to change what NEW shops start with, or in the Inspector's Packs list for the one in your scene.
/// </summary>
public partial class PixelShop
{
    /// <summary>Default RGB pack: 100 White + 100 Gray + 100 Black for Red, Green and Blue.</summary>
    private static ShopPack CreateRgbPack()
    {
        return new ShopPack
        {
            displayName = "RGB Pack",
            tab = ShopTab.Pixels,
            description = "Adds Red, Green and Blue pixels to the random spawn pool.",
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.White, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Gray,  amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Black, amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Red, displayName = "Red Pixels", color = Color.red,
                    amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Green, displayName = "Green Pixels", color = Color.green,
                    amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Blue, displayName = "Blue Pixels", color = Color.blue,
                    amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Glass pack: unlocks see-through Glass pixels for 100 Red + 100 Green + 100 Blue. Needs the RGB pack first.</summary>
    private static ShopPack CreateGlassPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Glass Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the see-through Glass pixel to the random spawn pool.",
            requiresPackIndex = requiresRgbIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Glass, displayName = "Glass Pixels",
                    color = new Color(0.7f, 0.92f, 1f, 0.35f), translucent = true,
                    amountPerClick = 1, spawnWeight = 0.5f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Vacuum pack: a rare pixel that re-collects every old pixel. Needs the Glass pack first.</summary>
    private static ShopPack CreateVacuumPack(int requiresGlassIndex)
    {
        return new ShopPack
        {
            displayName = "Vacuum Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the rare Vacuum pixel. Clicking it sucks up every old pixel and collects them again.",
            requiresPackIndex = requiresGlassIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Vacuum, displayName = "Vacuum Pixels",
                    color = new Color(0.65f, 0.3f, 0.95f, 1f), vacuum = true,
                    amountPerClick = 1, spawnWeight = 0.2f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Obsidian pack: a tough pixel that takes several clicks to collect but pays more. Needs the Glass pack first.</summary>
    private static ShopPack CreateObsidianPack(int requiresGlassIndex)
    {
        return new ShopPack
        {
            displayName = "Obsidian Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the tough Obsidian pixel. It takes {clicks} clicks to collect, but pays more.",
            requiresPackIndex = requiresGlassIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Black, amount = 500 },
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 100 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Obsidian, displayName = "Obsidian Pixels",
                    color = new Color(0.22f, 0.1f, 0.35f, 1f),
                    amountPerClick = 5, clicksToCollect = 5, spawnWeight = 0.4f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Luminescent pack: a glowing pixel that pays more. Needs the Obsidian pack first.</summary>
    private static ShopPack CreateLuminescentPack(int requiresObsidianIndex)
    {
        return new ShopPack
        {
            displayName = "Luminescent Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the glowing Luminescent pixel, which pays more per click.",
            requiresPackIndex = requiresObsidianIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 200 },
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 25 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Luminescent, displayName = "Luminescent Pixels",
                    color = new Color(0.4f, 1f, 0.7f, 1f), glow = true, glowIntensity = 2.5f,
                    amountPerClick = 3, spawnWeight = 0.5f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Ghost Hunt pack (Minigames tab): switches on the ghost minigame. Needs the Glass pack first.</summary>
    private static ShopPack CreateGhostPack(int requiresGlassIndex)
    {
        return new ShopPack
        {
            displayName = "Ghost Hunt",
            tab = ShopTab.Minigames,
            description = "A faint ghost cube floats across the screen now and then. Click it for a random pixel buff.",
            requiresPackIndex = requiresGlassIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Black, amount = 200 },
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 50 },
            },
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "ghost",
        };
    }

    /// <summary>Default Ghost Pixel pack (Pixels tab): needs the Ghost Hunt pack AND enough ghosts caught.</summary>
    private static ShopPack CreateGhostPixelPack(int requiresGhostHuntIndex)
    {
        return new ShopPack
        {
            displayName = "Ghost Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the faint, see-through Ghost pixel to the spawn pool. Pays well.",
            requiresPackIndex = requiresGhostHuntIndex,
            requiresMinigameGoal = "ghost",
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Glass,        amount = 300 },
                new PackCost { type = PixelClicker.PixelType.Luminescent, amount = 50 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Ghost, displayName = "Ghost Pixels",
                    color = new Color(0.85f, 0.95f, 1f, 0.3f), translucent = true,
                    amountPerClick = 8, spawnWeight = 0.3f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Black Hole pack (Minigames tab). Needs the Vacuum pack first.</summary>
    private static ShopPack CreateBlackholePack(int requiresVacuumIndex)
    {
        return new ShopPack
        {
            displayName = "Black Hole",
            tab = ShopTab.Minigames,
            description = "A black hole sometimes opens on the floor and swallows old pixels, feeding the singularity.",
            requiresPackIndex = requiresVacuumIndex,
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Black,  amount = 500 },
                new PackCost { type = PixelClicker.PixelType.Vacuum, amount = 50 },
            },
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "blackhole",
        };
    }

    /// <summary>Default Singularity Pixel pack (Pixels tab): needs the Black Hole pack AND the tracker's goal.</summary>
    private static ShopPack CreateSingularityPack(int requiresBlackholeIndex)
    {
        return new ShopPack
        {
            displayName = "Singularity Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the tough Singularity pixel, forged from what the black hole swallowed. Pays hugely.",
            requiresPackIndex = requiresBlackholeIndex,
            requiresMinigameGoal = "blackhole",
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Vacuum,   amount = 50 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Singularity, displayName = "Singularity Pixels",
                    color = new Color(0.25f, 0.05f, 0.4f, 1f), glow = true, glowIntensity = 2f,
                    amountPerClick = 25, clicksToCollect = 3, spawnWeight = 0.2f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    private static PackCost[] AllSix(double amount)
    {
        return new[]
        {
            new PackCost { type = PixelClicker.PixelType.White, amount = amount },
            new PackCost { type = PixelClicker.PixelType.Gray,  amount = amount },
            new PackCost { type = PixelClicker.PixelType.Black, amount = amount },
            new PackCost { type = PixelClicker.PixelType.Red,   amount = amount },
            new PackCost { type = PixelClicker.PixelType.Green, amount = amount },
            new PackCost { type = PixelClicker.PixelType.Blue,  amount = amount },
        };
    }

    /// <summary>Default "quicker interval" upgrade: 5 levels, each costing more of all six pixel types.</summary>
    private static ShopPack CreateIntervalUpgradePack(int requiresAutoClickerIndex)
    {
        return new ShopPack
        {
            displayName = "Faster Clicking",
            tab = ShopTab.Upgrades,
            description = "Auto clicker interval: {current}s  →  {next}s",
            requiresPackIndex = requiresAutoClickerIndex,
            upgradeEffect = UpgradeEffect.AutoClickerInterval,
            levels = new[]
            {
                new PackLevel { value = 0.8f,  costs = AllSix(200) },
                new PackLevel { value = 0.6f,  costs = AllSix(400) },
                new PackLevel { value = 0.45f, costs = AllSix(800) },
                new PackLevel { value = 0.3f,  costs = AllSix(1600) },
                new PackLevel { value = 0.2f,  costs = AllSix(3200) },
            }
        };
    }

    /// <summary>Default "extra clicks" upgrade: 4 levels, +1 click per tick each.</summary>
    private static ShopPack CreateClicksUpgradePack(int requiresAutoClickerIndex)
    {
        return new ShopPack
        {
            displayName = "Multi-Click",
            tab = ShopTab.Upgrades,
            description = "Clicks per auto click: {current}  →  {next}",
            requiresPackIndex = requiresAutoClickerIndex,
            upgradeEffect = UpgradeEffect.AutoClickerClicks,
            levels = new[]
            {
                new PackLevel { value = 2f, costs = AllSix(300) },
                new PackLevel { value = 3f, costs = AllSix(600) },
                new PackLevel { value = 4f, costs = AllSix(1200) },
                new PackLevel { value = 5f, costs = AllSix(2400) },
            }
        };
    }

    /// <summary>Default Auto Clicker pack: all six pixel types, available after the RGB pack (index 0).</summary>
    private static ShopPack CreateAutoClickerPack()
    {
        return new ShopPack
        {
            displayName = "Auto Clicker",
            tab = ShopTab.Upgrades,
            description = "Clicks the cube for you every {interval} seconds.",
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.White, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Gray,  amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Black, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 100 },
            },
            rewardTiers = new PixelClicker.PixelTier[0],
            requiresPackIndex = 0,
            unlocksAutoClicker = true,
        };
    }

#if UNITY_EDITOR
    /// <summary>Right-click the component header > Add Default Auto Clicker Pack, to make it editable in the list.</summary>
    [ContextMenu("Add Default Vacuum Pack To List")]
    private void AddVacuumPackToList()
    {
        int glass = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                                Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Glass));
        UnityEditor.Undo.RecordObject(this, "Add Vacuum Pack");
        Array.Resize(ref packs, packs.Length + 1);
        packs[packs.Length - 1] = CreateVacuumPack(glass);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Default Glass Pack To List")]
    private void AddGlassPackToList()
    {
        int rgb = Array.FindIndex(packs, p => p.rewardTiers != null &&
                                              Array.Exists(p.rewardTiers, r => r.type == PixelClicker.PixelType.Red));
        UnityEditor.Undo.RecordObject(this, "Add Glass Pack");
        Array.Resize(ref packs, packs.Length + 1);
        packs[packs.Length - 1] = CreateGlassPack(rgb);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Default Auto Clicker Upgrade Packs To List")]
    private void AddUpgradePacksToList()
    {
        int auto = Array.FindIndex(packs, p => p.unlocksAutoClicker);
        UnityEditor.Undo.RecordObject(this, "Add Upgrade Packs");
        Array.Resize(ref packs, packs.Length + 2);
        packs[packs.Length - 2] = CreateIntervalUpgradePack(auto);
        packs[packs.Length - 1] = CreateClicksUpgradePack(auto);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Default Auto Clicker Pack To List")]
    private void AddAutoClickerPackToList()
    {
        UnityEditor.Undo.RecordObject(this, "Add Auto Clicker Pack");
        Array.Resize(ref packs, packs.Length + 1);
        packs[packs.Length - 1] = CreateAutoClickerPack();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
