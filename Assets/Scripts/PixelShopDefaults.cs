using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The shop's built-in packs (RGB, Auto Clicker, Glass, Vacuum, Obsidian, Luminescent, Ghost Hunt, Black Hole,
/// Singularity Pixel, Ghost Pixel, Meteor Strike, Meteor Pixel, the auto clicker upgrades). Part of <see cref="PixelShop"/>: edit a pack's numbers
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
            requirements = Needs(requiresRgbIndex),
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
            requirements = Needs(requiresGlassIndex),
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
            requirements = Needs(requiresGlassIndex),
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
                    amountPerClick = 7, clicksToCollect = 5, spawnWeight = 0.4f,
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
            requirements = Needs(requiresObsidianIndex),
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
            requirements = Needs(requiresGlassIndex),
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
            requirements = Needs(requiresGhostHuntIndex, "ghost"),
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

    /// <summary>Default Bomb Defusal pack (Minigames tab): switches on the bomb minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateBombPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Bomb Defusal",
            tab = ShopTab.Minigames,
            description = "A bomb with red, green and blue wires appears now and then. Cut the wire of the colour you hold the most of before the timer runs out, or it blows up your old pixels. Defusing earns bomb parts.",
            requirements = Needs(requiresRgbIndex),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 200 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 200 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 200 },
            },
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "bomb",
        };
    }

    /// <summary>Default Ultra Pad pack (Minigames tab): switches on the ultra pad minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreatePadPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Ultra Pad",
            tab = ShopTab.Minigames,
            description = "A coloured pad appears on the ground. Get old pixels of that colour onto it for a chance at Ultra pixels - but wrong pixels cost you currency.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(300),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "pad",
        };
    }

    /// <summary>Default Meteor Strike pack (Minigames tab): switches on the meteor minigame. Needs the Obsidian pack first.</summary>
    private static ShopPack CreateMeteorStrikePack(int requiresObsidianIndex)
    {
        return new ShopPack
        {
            displayName = "Meteor Strike",
            tab = ShopTab.Minigames,
            description = "A huge meteor drifts slowly across the screen now and then. Every click on it chips off a meteor chunk.",
            requirements = Needs(requiresObsidianIndex),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Black, amount = 500 },
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 20 },
            },
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "meteor",
        };
    }

    /// <summary>Default Meteor Pixel pack (Pixels tab): needs Meteor Strike AND enough meteor chunks.</summary>
    private static ShopPack CreateMeteorPixelPack(int requiresMeteorIndex)
    {
        return new ShopPack
        {
            displayName = "Meteor Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the fiery Meteor pixel. When clicked it doesn't fall - it streaks away like a meteor.",
            requirements = Needs(requiresMeteorIndex, "meteor"),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Luminescent, amount = 100 },
                new PackCost { type = PixelClicker.PixelType.Vacuum, amount = 50 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Meteor, displayName = "Meteor Pixels",
                    color = new Color(0.9f, 0.4f, 0.1f, 1f), glow = true, glowIntensity = 2f,
                    amountPerClick = 15, spawnWeight = 0.25f,
                    flyAway = true, flyDirection = new Vector2(1f, 0.6f), flySpeed = 30f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>Default Sorting Race pack (Minigames tab): switches on the sorting minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateSortPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Sorting Race",
            tab = ShopTab.Minigames,
            description = "Now and then some old pixels freeze in a jumbled grid. Click two pixels to swap them and group each type together before the timer runs out for a bonus payout.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(500),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "sort",
        };
    }

    /// <summary>Default Snake pack (Minigames tab): switches on the snake minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateSnakePack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Snake",
            tab = ShopTab.Minigames,
            description = "Now and then every old pixel stops despawning and freezes. Steer a snake with the arrow keys and eat them all without biting yourself for 10x their worth.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(500),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "snake",
        };
    }

    /// <summary>Default Solar Pixel pack (Pixels tab): needs the Meteor Pixel pack AND enough stardust.</summary>
    private static ShopPack CreateSolarPixelPack(int requiresMeteorPixelIndex)
    {
        return new ShopPack
        {
            displayName = "Solar Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the blazing Solar pixel, forged from stardust. Meteor pixels sometimes knock a star out of the sky - collect them.",
            requirements = Needs(requiresMeteorPixelIndex, "stardust"),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Meteor, amount = 50 },
                new PackCost { type = PixelClicker.PixelType.Luminescent, amount = 200 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Solar, displayName = "Solar Pixels",
                    color = new Color(1f, 0.82f, 0.2f, 1f), glow = true, glowIntensity = 3f,
                    amountPerClick = 25, spawnWeight = 0.2f,
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
            requirements = Needs(requiresVacuumIndex),
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
            requirements = Needs(requiresBlackholeIndex, "blackhole"),
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
                    amountPerClick = 25, clicksToCollect = 3, spawnWeight = 0.08f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>A requirements list: the pack at this index must be bought (-1 = none) and, optionally, a minigame's goal reached.</summary>
    private static PackRequirement[] Needs(int packIndex, string minigameGoal = null)
    {
        System.Collections.Generic.List<PackRequirement> list = new System.Collections.Generic.List<PackRequirement>();
        if (packIndex >= 0) list.Add(new PackRequirement { kind = RequirementKind.Pack, packIndex = packIndex });
        if (!string.IsNullOrEmpty(minigameGoal)) list.Add(new PackRequirement { kind = RequirementKind.MinigameGoal, minigameId = minigameGoal });
        return list.ToArray();
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
            requirements = Needs(requiresAutoClickerIndex),
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

    /// <summary>Default Crafting upgrade (Upgrades tab): puts a Crafting button on screen.</summary>
    private static ShopPack CreateCraftingPack()
    {
        return new ShopPack
        {
            displayName = "Crafting",
            tab = ShopTab.Upgrades,
            description = "Unlocks the Crafting button: combine two items to make potions.",
            costs = AllSix(300),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksCrafting = true,
        };
    }

    /// <summary>Default Pixel Grabbing upgrade (Upgrades tab): old pixels can be clicked and dragged.</summary>
    private static ShopPack CreateGrabbingPack()
    {
        return new ShopPack
        {
            displayName = "Pixel Grabbing",
            tab = ShopTab.Upgrades,
            description = "Click and drag old pixels around. Let go to drop or throw them.",
            costs = AllSix(150),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksGrabbing = true,
        };
    }

    /// <summary>Default Pixel Bank upgrade (Upgrades tab): a hose that stores old pixels and spits them back out.</summary>
    private static ShopPack CreateBankPack()
    {
        return new ShopPack
        {
            displayName = "Pixel Bank",
            tab = ShopTab.Upgrades,
            description = "Store old pixels in a bank. Press B for a hose: right-click sucks a pixel up, left-click spits the selected one out, scroll to choose.",
            costs = AllSix(250),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksBank = true,
        };
    }

    /// <summary>Default Bank Storage upgrade: 5 levels, each raising how much the Pixel Bank holds. Paid in glass, vacuum pixels and bomb parts.</summary>
    private static ShopPack CreateBankStoragePack(int requiresBankIndex)
    {
        return new ShopPack
        {
            displayName = "Bank Storage",
            tab = ShopTab.Upgrades,
            description = "Pixel Bank capacity: {current}  →  {next}",
            requirements = Needs(requiresBankIndex),
            upgradeEffect = UpgradeEffect.BankCapacity,
            levels = new[]
            {
                new PackLevel { value = 100f, costs = BankStorageCost(100, 20, 4) },
                new PackLevel { value = 160f, costs = BankStorageCost(200, 40, 8) },
                new PackLevel { value = 250f, costs = BankStorageCost(400, 80, 14) },
                new PackLevel { value = 400f, costs = BankStorageCost(800, 160, 22) },
                new PackLevel { value = 600f, costs = BankStorageCost(1600, 320, 32) },
            }
        };
    }

    /// <summary>Default Old Pixel Capacity upgrade (Upgrades tab, 5 levels): raises how many old pixels may lie around at once (base 50).</summary>
    private static ShopPack CreateOldPixelCapPack()
    {
        return new ShopPack
        {
            displayName = "Old Pixel Capacity",
            tab = ShopTab.Upgrades,
            description = "How many old pixels can lie around at once: {current}  →  {next}",
            upgradeEffect = UpgradeEffect.OldPixelCap,
            levels = new[]
            {
                new PackLevel { value = 75f,  costs = AllSix(250) },
                new PackLevel { value = 100f, costs = AllSix(700) },
                new PackLevel { value = 150f, costs = AllSix(2000) },
                new PackLevel { value = 200f, costs = AllSix(6000) },
                new PackLevel { value = 300f, costs = AllSix(20000) },
            }
        };
    }

    private static PackCost[] BankStorageCost(double glass, double vacuum, double bombParts)
    {
        return new[]
        {
            new PackCost { type = PixelClicker.PixelType.Glass, amount = glass },
            new PackCost { type = PixelClicker.PixelType.Vacuum, amount = vacuum },
            new PackCost { minigameCurrency = "bomb", amount = bombParts },
        };
    }

    /// <summary>Default Time Stop upgrade (Upgrades tab): the T key freezes the whole game.</summary>
    private static ShopPack CreateTimeStopPack()
    {
        return new ShopPack
        {
            displayName = "Time Stop",
            tab = ShopTab.Upgrades,
            description = "Press T to stop time - everything freezes, even countdowns. Press T again to resume.",
            costs = AllSix(400),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksTimeStop = true,
        };
    }

    /// <summary>Default Time Slow upgrade (Upgrades tab): the S key slows time; needs the Time Stop pack.</summary>
    private static ShopPack CreateTimeSlowPack(int requiresTimeStopIndex)
    {
        return new ShopPack
        {
            displayName = "Time Slow",
            tab = ShopTab.Upgrades,
            description = "Press S to slow time down (it shares the Time Stop energy meter). Fast things, like meteor pixels, slow to a crawl - slow enough to grab with Pixel Grabbing.",
            requirements = Needs(requiresTimeStopIndex),
            costs = AllSix(800),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksTimeSlow = true,
        };
    }

    /// <summary>Default Combo Meter upgrade: quick clicks build a multiplier; each level raises its maximum.</summary>
    private static ShopPack CreateComboPack()
    {
        return new ShopPack
        {
            displayName = "Combo Meter",
            tab = ShopTab.Upgrades,
            description = "Click fast to build a combo that boosts your clicks. Max multiplier: x{current}  →  x{next}",
            upgradeEffect = UpgradeEffect.ComboMeter,
            levels = new[]
            {
                new PackLevel { value = 1.5f, costs = AllSix(100) },
                new PackLevel { value = 2f,   costs = AllSix(300) },
                new PackLevel { value = 3f,   costs = AllSix(900) },
                new PackLevel { value = 4f,   costs = AllSix(2700) },
                new PackLevel { value = 5f,   costs = AllSix(8100) },
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
            requirements = Needs(requiresAutoClickerIndex),
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
    private static ShopPack CreateAutoClickerPack(int requiresRgbIndex)
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
            requirements = Needs(requiresRgbIndex),
            unlocksAutoClicker = true,
        };
    }

    // ------------------------------------------------------------------
    // The built-in pack table: how to recognise each one in the list, what it needs, how to build it.
    // To add a built-in pack: write its Create...Pack method above and add one line here (after the pack it requires).
    // ------------------------------------------------------------------

    private class DefaultPack
    {
        public Func<ShopPack, bool> isThis;      // recognises this pack in the Packs list
        public Func<ShopPack, bool> requires;    // recognises the pack it requires (null = none)
        public Func<int, ShopPack> create;       // builds it, given the index of the required pack (-1 if not found)
    }

    private static bool Rewards(ShopPack pack, PixelClicker.PixelType type)
    {
        return pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == type);
    }

    private static bool Unlocks(ShopPack pack, string minigameId) => pack.unlocksMinigame == minigameId;

    private static readonly DefaultPack[] BuiltInPacks =
    {
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Red),
                          requires = null, create = i => CreateRgbPack() },
        new DefaultPack { isThis = p => p.unlocksAutoClicker,
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateAutoClickerPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Glass),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateGlassPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Vacuum),
                          requires = p => Rewards(p, PixelClicker.PixelType.Glass), create = CreateVacuumPack },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.AutoClickerInterval,
                          requires = p => p.unlocksAutoClicker, create = CreateIntervalUpgradePack },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.AutoClickerClicks,
                          requires = p => p.unlocksAutoClicker, create = CreateClicksUpgradePack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Obsidian),
                          requires = p => Rewards(p, PixelClicker.PixelType.Glass), create = CreateObsidianPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Luminescent),
                          requires = p => Rewards(p, PixelClicker.PixelType.Obsidian), create = CreateLuminescentPack },
        new DefaultPack { isThis = p => Unlocks(p, "ghost"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Glass), create = CreateGhostPack },
        new DefaultPack { isThis = p => Unlocks(p, "blackhole"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Vacuum), create = CreateBlackholePack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Singularity),
                          requires = p => Unlocks(p, "blackhole"), create = CreateSingularityPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Ghost),
                          requires = p => Unlocks(p, "ghost"), create = CreateGhostPixelPack },
        new DefaultPack { isThis = p => Unlocks(p, "meteor"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Obsidian), create = CreateMeteorStrikePack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Meteor),
                          requires = p => Unlocks(p, "meteor"), create = CreateMeteorPixelPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Solar),
                          requires = p => Rewards(p, PixelClicker.PixelType.Meteor), create = CreateSolarPixelPack },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.ComboMeter,
                          requires = null, create = i => CreateComboPack() },
        new DefaultPack { isThis = p => Unlocks(p, "pad"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreatePadPack },
        new DefaultPack { isThis = p => Unlocks(p, "sort"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateSortPack },
        new DefaultPack { isThis = p => Unlocks(p, "snake"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateSnakePack },
        new DefaultPack { isThis = p => Unlocks(p, "bomb"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateBombPack },
        new DefaultPack { isThis = p => p.unlocksCrafting,
                          requires = null, create = i => CreateCraftingPack() },
        new DefaultPack { isThis = p => p.unlocksGrabbing,
                          requires = null, create = i => CreateGrabbingPack() },
        new DefaultPack { isThis = p => p.unlocksTimeStop,
                          requires = null, create = i => CreateTimeStopPack() },
        new DefaultPack { isThis = p => p.unlocksTimeSlow,
                          requires = p => p.unlocksTimeStop, create = CreateTimeSlowPack },
        new DefaultPack { isThis = p => p.unlocksBank,
                          requires = null, create = i => CreateBankPack() },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.OldPixelCap,
                          requires = null, create = i => CreateOldPixelCapPack() },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.BankCapacity,
                          requires = p => p.unlocksBank, create = CreateBankStoragePack },
    };

#if UNITY_EDITOR
    /// <summary>Right-click the component header &gt; Add Missing Default Packs, to fill in any built-in pack that is not in the list.</summary>
    [ContextMenu("Add Missing Default Packs")]
    private void AddMissingDefaultPacksFromMenu()
    {
        UnityEditor.Undo.RecordObject(this, "Add Default Packs");
        if (EnsureDefaultPacks()) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
