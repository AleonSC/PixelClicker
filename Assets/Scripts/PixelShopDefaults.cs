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

    /// <summary>Glass pays for Crafting too (the old Crafting pack cost 10K of each of the six basic pixels; Glass used to cost 2.5K Red / Green / Blue).</summary>
    private static PackCost[] GlassCosts() => new[]
    {
        new PackCost { type = PixelClicker.PixelType.White, amount = 10000 },
        new PackCost { type = PixelClicker.PixelType.Gray,  amount = 10000 },
        new PackCost { type = PixelClicker.PixelType.Black, amount = 10000 },
        new PackCost { type = PixelClicker.PixelType.Red,   amount = 12500 },
        new PackCost { type = PixelClicker.PixelType.Green, amount = 12500 },
        new PackCost { type = PixelClicker.PixelType.Blue,  amount = 12500 },
    };

    /// <summary>Default Glass pack: unlocks see-through Glass pixels for 100 Red + 100 Green + 100 Blue. Needs the RGB pack first.</summary>
    private static ShopPack CreateGlassPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Glass Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the see-through Glass pixel to the random spawn pool (switched on by default; you can switch it off in the Toggles window) and unlocks Crafting.",
            requirements = Needs(requiresRgbIndex),
            unlocksCrafting = true,
            costs = GlassCosts(),
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
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 17500 },
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 35000 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 35000 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 35000 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Vacuum, displayName = "Vacuum Pixels",
                    color = new Color(0.65f, 0.3f, 0.95f, 1f), vacuum = true,
                    amountPerClick = 1, spawnWeight = 0.28f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
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
                new PackCost { type = PixelClicker.PixelType.Black, amount = 100000 },
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 50000 },
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
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 175000 },
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 700000 },
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
                new PackCost { type = PixelClicker.PixelType.Black, amount = 35000 },
                new PackCost { type = PixelClicker.PixelType.Glass, amount = 17500 },
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
                new PackCost { type = PixelClicker.PixelType.Glass,        amount = 1400000 },
                new PackCost { type = PixelClicker.PixelType.Luminescent, amount = 4000000 },
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
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 35000 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 35000 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 35000 },
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
            description = "Four round holes open in the floor, one near each corner of the screen. The marked hole (a spinning pixel floats in its centre) wants that pixel: drop it in for its value and a chance at an Ultra pixel. Anything else that falls into a hole is lost. The marker jumps to another hole after every correct drop.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(175000),
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
                new PackCost { type = PixelClicker.PixelType.Black, amount = 1750000 },
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 3500000 },
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
                new PackCost { type = PixelClicker.PixelType.Luminescent, amount = 4000000 },
                new PackCost { type = PixelClicker.PixelType.Vacuum, amount = 550000 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Meteor, displayName = "Meteor Pixels",
                    color = new Color(0.9f, 0.4f, 0.1f, 1f), glow = false, glowIntensity = 2f,   // a dark rock whose cracks glow (the look draws them)
                    amountPerClick = 45, clicksToCollect = 3, spawnWeight = 0.25f,
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
            costs = AllSix(70000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "sort",
        };
    }

    /// <summary>Default Cube Dash pack (Minigames tab): switches on the dash minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateDashPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Cube Dash",
            tab = ShopTab.Minigames,
            description = "Now and then the cube dashes around the screen, hanging in one spot before zipping to the next. The auto clicker switches off: click it as often as you can. At the end you get a bonus of 10x the pixel's worth for every click you landed.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(50000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "dash",
        };
    }

    /// <summary>Default Red Light Green Light pack (Minigames tab): switches on the traffic minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateTrafficPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Red Light Green Light",
            tab = ShopTab.Minigames,
            description = "A traffic light appears beside the cube. Click on green to score, never on red (it costs you), and on yellow click slowly, in time with the circle that closes around the cube. At the end you get a bonus of 10x the pixel's worth for every point.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(80000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "traffic",
        };
    }

    /// <summary>Default Cube Evasion pack (Minigames tab): switches on the evade minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateEvadePack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Cube Evasion",
            tab = ShopTab.Minigames,
            description = "Now and then the cube runs away from your mouse, sliding along the edges and slipping out of corners. The auto clicker switches off: corner it and click. At the end you get a bonus of 10x the pixel's worth for every click you landed.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(70000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "evade",
        };
    }

    /// <summary>Default Cube Blink pack (Minigames tab): switches on the blink minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateBlinkPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Cube Blink",
            tab = ShopTab.Minigames,
            description = "Now and then the cube shrinks to nothing and teleports to a random spot, hides for a second or two, then pops back and stays a few seconds before teleporting again. The auto clicker switches off: click it whenever it shows. At the end you get a bonus of 10x the pixel's worth for every click you landed.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(60000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "blink",
        };
    }

    /// <summary>Default Breakout pack (Minigames tab): switches on the breakout minigame. Needs the RGB pack first.</summary>
    private static ShopPack CreateBreakoutPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Breakout",
            tab = ShopTab.Minigames,
            description = "Now and then (with 30+ old pixels on screen) the old pixels become a wall of bricks. Bounce the ball with your paddle and break them: each one pays 10x its worth.",
            requirements = Needs(requiresRgbIndex),
            costs = AllSix(175000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksMinigame = "breakout",
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
            costs = AllSix(35000),
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
                new PackCost { type = PixelClicker.PixelType.Meteor, amount = 700000 },
                new PackCost { type = PixelClicker.PixelType.Luminescent, amount = 7000000 },
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

    /// <summary>
    /// Default Electric Pixel pack (Pixels tab): an early unlock (before Vacuum) that needs the Auto Clicker pack, since it boosts
    /// the auto clicker. Pays a random 1-15 per harvest (8 on average) but is a fairly rare spawn, so it is a slight income boost.
    /// </summary>
    private static ShopPack CreateElectricPixelPack(int requiresAutoClickerIndex)
    {
        return new ShopPack
        {
            displayName = "Electric Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the Electric pixel: a cube held together by crackling lightning. Pays a random amount every harvest, overcharges the auto clicker when clicked and arcs to nearby devices to keep them running.",
            requirements = Needs(requiresAutoClickerIndex),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 8000 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 8000 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 8000 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Electric, displayName = "Electric Pixels",
                    color = new Color(0.4f, 0.85f, 1f, 1f), glow = true, glowIntensity = 3.5f,
                    amountPerClick = 8, randomPayout = true, payoutMin = 1, payoutMax = 15, // 1-15 every harvest (8 = the average)
                    spawnWeight = 0.2f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Electrician's Pack (Pixels tab, Skill packs): needs the Electric Pixel pack. Buying it makes the Electrician skill available: Electric
    /// pixels replace the basic cubes while the skill is in play.
    /// </summary>
    /// <summary>The three pixels the Electrician's Pack adds (Electric itself comes from the Electric Pixel pack).</summary>
    private static PixelClicker.PixelTier[] ElectricianTiers()
    {
        PixelClicker.PixelTier Block(PixelClicker.PixelType type, string name, Color color, int hits, double pay, float weight) => new PixelClicker.PixelTier
        {
            type = type, displayName = name, color = color, clicksToCollect = hits, amountPerClick = pay, spawnWeight = weight,
            unlockMode = PixelClicker.TierUnlockMode.ShopOnly
        };
        return new[]
        {
            new PixelClicker.PixelTier
            {
                type = PixelClicker.PixelType.Electric, displayName = "Electric Pixels",
                color = new Color(0.4f, 0.85f, 1f, 1f), glow = true, glowIntensity = 3.5f,
                amountPerClick = 8, randomPayout = true, payoutMin = 1, payoutMax = 15,   // 1-15 every harvest (8 = the average)
                spawnWeight = 0.2f,
                unlockMode = PixelClicker.TierUnlockMode.ShopOnly
            },
            Block(PixelClicker.PixelType.Wire, "Wire Pixels", new Color(0.86f, 0.5f, 0.26f, 1f), 1, 2, 1.0f),
            Block(PixelClicker.PixelType.Battery, "Battery Pixels", new Color(0.5f, 0.9f, 0.3f, 1f), 3, 12, 0.4f),
            Block(PixelClicker.PixelType.Capacitor, "Capacitor Pixels", new Color(0.3f, 0.55f, 0.95f, 1f), 1, 6, 0.35f),
            Block(PixelClicker.PixelType.Resistor, "Resistor Pixels", new Color(0.82f, 0.68f, 0.45f, 1f), 2, 5, 0.6f),
            Block(PixelClicker.PixelType.Transistor, "Transistor Pixels", new Color(0.3f, 0.34f, 0.45f, 1f), 1, 5, 0.4f),
            Block(PixelClicker.PixelType.Fuse, "Fuse Pixels", new Color(0.95f, 0.45f, 0.2f, 1f), 1, 5, 0.4f),
            Block(PixelClicker.PixelType.Bulb, "Bulb Pixels", new Color(1f, 0.95f, 0.6f, 1f), 1, 6, 0.3f),
        };
    }

    private static PackCost[] ElectricianCosts() => new[]
    {
        new PackCost { type = PixelClicker.PixelType.Red,   amount = 8000 },
        new PackCost { type = PixelClicker.PixelType.Green, amount = 8000 },
        new PackCost { type = PixelClicker.PixelType.Blue,  amount = 8000 },
    };

    private static ShopPack CreateElectricianPack(int requiresAutoClickerIndex)
    {
        return new ShopPack
        {
            displayName = "Electrician's Pack",
            tab = ShopTab.Pixels,
            skillId = PixelSkills.ElectricianId,
            description = "Unlocks the Electrician skill and its eight pixels: Electric, Wire, Battery, Capacitor, Resistor, Transistor, Fuse and Bulb. While the skill is in play (Toggles or Skills window) they replace the basic cubes. Wire, Battery and Resistor carry your device links, a Battery also sets off an Overcharge, a Capacitor stores charge from every other click, a Transistor doubles your next click, a Fuse pays big if you click it fast and a Bulb shines brighter the more wired pixels lie around.",
            requirements = Needs(requiresAutoClickerIndex),
            costs = ElectricianCosts(),
            rewardTiers = ElectricianTiers(),
        };
    }

    /// <summary>Default Seed Pixel pack (Pixels tab): an early unlock listed before Electric, needs the RGB pack. A brown shell that cracks; a sprout grows a random old pixel.</summary>
    private static ShopPack CreateSeedPixelPack(int requiresRgbIndex)
    {
        return new ShopPack
        {
            displayName = "Seed Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the Seed pixel: a brown shell you dig at with many clicks to crack. A sprout comes out, plants itself and grows a random old pixel that pops off.",
            requirements = Needs(requiresRgbIndex),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 4000 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 4000 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 4000 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Seed, displayName = "Seed Pixels",
                    color = new Color(0.55f, 0.38f, 0.18f, 1f),
                    amountPerClick = 15, clicksToCollect = 8, // 8 clicks crack the shell; only the last pays
                    spawnWeight = 0.3f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Water Pixel pack (Pixels tab): TESTING price (10 White, no requirements) so it is available right away. Old Water pixels splash
    /// on the ground and are used up; Seed sprouts and the Seed pet near a splash are watered (grow / sow faster): stream them over a field of
    /// sprouts with a Sorter to make a farm.
    /// </summary>
    private static ShopPack CreateWaterPixelPack()
    {
        return new ShopPack
        {
            displayName = "Water Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the Water pixel: a wobbling blue jelly cube. Old Water pixels splash when they hit the ground (and are used up); sprouts and the Seed pet near the splash grow and sow much faster for a while.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 10 } },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Water, displayName = "Water Pixels",
                    color = new Color(0.3f, 0.62f, 1f, 1f),
                    amountPerClick = 3,
                    spawnWeight = 0.4f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Fire Pixel pack (Pixels tab): a TESTING pixel (5 White, no requirements). Clicking it bursts a ring of flames round the cube; the
    /// old pixel falls while it burns away. No potions, pets, crafting or other integration.
    /// </summary>
    private static ShopPack CreateFirePixelPack()
    {
        return new ShopPack
        {
            displayName = "Fire Pixel",
            tab = ShopTab.Pixels,
            description = "TEST: Adds the Fire pixel, a glowing orange cube with flames rising off it. Clicking it bursts a ring of fire round the cube, and the old pixel burns away as it falls.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 5 } },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Fire, displayName = "Fire Pixels",
                    color = new Color(1f, 0.45f, 0.1f, 1f), glow = true, glowIntensity = 1.5f,
                    amountPerClick = 6,
                    spawnWeight = 0.5f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Nuclear Pixel pack (Pixels tab): a TESTING pixel (10 White, no requirements). A glowing radioactive cube; its old pixel makes every
    /// other old pixel near it despawn almost at once. No potions, pets, crafting or other integration.
    /// </summary>
    private static ShopPack CreateNuclearPixelPack()
    {
        return new ShopPack
        {
            displayName = "Nuclear Pixel",
            tab = ShopTab.Pixels,
            description = "TEST: Adds the Nuclear pixel, a radioactive cube. Its old pixel is so radioactive that every old pixel lying near it despawns almost at once.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 10 } },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Nuclear, displayName = "Nuclear Pixels",
                    color = new Color(0.5f, 1f, 0.2f, 1f), glow = true, glowIntensity = 1.5f,
                    amountPerClick = 8,
                    spawnWeight = 0.3f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Treasure Pixel pack (Pixels tab): a treasure chest that takes a RANDOM 1-20 clicks to open; the more clicks it took, the bigger
    /// the reward, and when it opens a pile of other pixel types pours out. No potions, pets, crafting or other integration.
    /// </summary>
    private static ShopPack CreateTreasurePixelPack()
    {
        return new ShopPack
        {
            displayName = "Treasure Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the Treasure pixel: a chest that takes a random 1 to 20 clicks to open. The more clicks it took, the bigger the reward, and when it bursts open a pile of other pixels pours out.",
            requirements = Needs(-1),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.White, amount = 50000 },
                new PackCost { type = PixelClicker.PixelType.Black, amount = 25000 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Treasure, displayName = "Treasure Pixels",
                    color = new Color(1f, 0.78f, 0.22f, 1f),
                    clicksToCollect = 10, randomClicks = true, randomClicksMin = 1, randomClicksMax = 20,
                    amountPerClick = 4,
                    spawnWeight = 0.1f,
                    unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Prospector's Pack (Pixels tab): very cheap (10 White, no requirements). Six base-metal ore pixels - tough rocks that crack and shed
    /// chips as you hit them, break in a shower of nuggets and are sometimes a glittering RICH VEIN that pays 5x. No other integration; a switch
    /// in the Toggles window (Prospector mode, off by default) brings them into what spawns in place of the monochrome, RGB, Glass and Luminescent pixels.
    /// </summary>
    /// <summary>Bedrock: needs a random 20-30 clicks (rolled per pixel) and pays a big chunk.</summary>
    private static PixelClicker.PixelTier BedrockTier() => new PixelClicker.PixelTier
    {
        type = PixelClicker.PixelType.Bedrock, displayName = "Bedrock", color = new Color(0.32f, 0.33f, 0.4f, 1f), clicksToCollect = 25, amountPerClick = 150, spawnWeight = 0.08f,
        randomClicks = true, randomClicksMin = 20, randomClicksMax = 30, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
    };

    private static ShopPack CreateProspectorPack()
    {
        PixelClicker.PixelTier Ore(PixelClicker.PixelType type, string name, Color color, int hits, int pay, float weight) => new PixelClicker.PixelTier
        {
            type = type, displayName = name, color = color, clicksToCollect = hits, amountPerClick = pay, spawnWeight = weight,
            unlockMode = PixelClicker.TierUnlockMode.ShopOnly
        };
        return new ShopPack
        {
            displayName = "Prospector's Pack",
            tab = ShopTab.Pixels,
            description = "Adds the Mining skill and seven ore pixels (Copper, Tin, Iron, Silver, Gold, Obsidian and Bedrock): rocks that crack and shed chips as you hit them, break in a shower of nuggets and are sometimes a glittering rich vein that pays big. Obsidian and Bedrock take 15-20 clicks but pay a lot. The ores only spawn while Mining is the skill in play (Skills window), plus plain Stone as filler and a Gas Pocket to watch out for.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 10 } },
            rewardTiers = new[]
            {
                // Stone: Mining's filler block (one click, pays almost nothing; most of what spawns at low mining level).
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Rock, displayName = "Stone Pixels", color = new Color(0.55f, 0.55f, 0.58f, 1f),
                    clicksToCollect = 1, amountPerClick = 1, spawnWeight = 1f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
                Ore(PixelClicker.PixelType.Copper, "Copper Ore", new Color(0.86f, 0.46f, 0.26f, 1f), 2, 3, 1.0f),
                Ore(PixelClicker.PixelType.Tin, "Tin Ore", new Color(0.75f, 0.8f, 0.84f, 1f), 2, 3, 0.9f),
                Ore(PixelClicker.PixelType.Iron, "Iron Ore", new Color(0.62f, 0.36f, 0.3f, 1f), 3, 5, 0.8f),
                Ore(PixelClicker.PixelType.Silver, "Silver Ore", new Color(0.88f, 0.9f, 0.95f, 1f), 4, 9, 0.5f),
                Ore(PixelClicker.PixelType.Gold, "Gold Ore", new Color(1f, 0.82f, 0.25f, 1f), 5, 14, 0.35f),
                // The two big rocks: 15-20 clicks each, but a good chunk of currency (and skill XP) when they finally break.
                Ore(PixelClicker.PixelType.Obsidian, "Obsidian Pixels", new Color(0.22f, 0.1f, 0.35f, 1f), 15, 80, 0.15f),
                BedrockTier(),
                // The Gas Pocket: a hazard (hit it too often and it explodes; it pays nothing).
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.GasPocket, displayName = "Gas Pocket", color = new Color(0.78f, 0.85f, 0.25f, 1f),
                    clicksToCollect = 5, amountPerClick = 0, spawnWeight = 0.05f, unlockMode = PixelClicker.TierUnlockMode.ShopOnly
                },
            }
        };
    }

    /// <summary>
    /// Default Woodcutter's Pack (Pixels tab): cheap, the intro skill. Six wood blocks for the Woodcutting skill: Wood, Bark, Sap, Leaf, Heartwood and
    /// the filler Twig, plus the Hornet Nest hazard. They only spawn while Woodcutting is the pack in play (Skills window).
    /// </summary>
    private static ShopPack CreateWoodPixelPack()
    {
        PixelClicker.PixelTier Block(PixelClicker.PixelType type, string name, Color color, int hits, double pay, float weight) => new PixelClicker.PixelTier
        {
            type = type, displayName = name, color = color, clicksToCollect = hits, amountPerClick = pay, spawnWeight = weight,
            unlockMode = PixelClicker.TierUnlockMode.ShopOnly
        };
        return new ShopPack
        {
            displayName = "Woodcutter's Pack",
            tab = ShopTab.Pixels,
            description = "Adds the Woodcutting skill and six wood blocks: Wood, Bark, Sap, Leaf, Heartwood (rare) and Twig, plus a Hornet Nest to watch out for and a Whetstone (hold it with Block Hold and your clicks count for more). They only spawn while Woodcutting is the pack in play (Skills window). Twig is a useless filler that is most of what you see at first; as your woodcutting level rises it gets rarer and the other blocks show up more.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 150 } },
            rewardTiers = new[]
            {
                Block(PixelClicker.PixelType.Wood, "Wood Pixels", new Color(0.6f, 0.4f, 0.22f, 1f), 2, 3, 1.0f),
                Block(PixelClicker.PixelType.Bark, "Bark Pixels", new Color(0.42f, 0.32f, 0.24f, 1f), 1, 2, 0.8f),
                Block(PixelClicker.PixelType.Sap, "Sap Pixels", new Color(1f, 0.72f, 0.2f, 1f), 1, 4, 0.5f),
                Block(PixelClicker.PixelType.Leaf, "Leaf Pixels", new Color(0.35f, 0.78f, 0.3f, 1f), 1, 1, 0.9f),
                Block(PixelClicker.PixelType.Heartwood, "Heartwood Pixels", new Color(0.78f, 0.35f, 0.28f, 1f), 4, 25, 0.1f),
                Block(PixelClicker.PixelType.Twig, "Twig Pixels", new Color(0.82f, 0.72f, 0.52f, 1f), 1, 1, 1.0f),
                // The Acorn: a base block, one click. Acorn old pixels lying on the field raise the Great Oak's spawn chance (PixelBlockRules).
                Block(PixelClicker.PixelType.Acorn, "Acorn", new Color(0.68f, 0.46f, 0.22f, 1f), 1, 1, 0.5f),
                // The Whetstone: a base block that takes 3 clicks. Hold it with Block Hold (right button) and every click counts for more while you hold it.
                Block(PixelClicker.PixelType.Whetstone, "Whetstone", new Color(0.62f, 0.68f, 0.76f, 1f), 3, 2, 0.35f),
                // The Hornet Nest: a hazard (hit it too often and the hornets chase off the old pixels around the cube; it pays nothing).
                Block(PixelClicker.PixelType.HornetNest, "Hornet Nest", new Color(0.85f, 0.78f, 0.5f, 1f), 5, 0, 0.05f),
            }
        };
    }

    /// <summary>
    /// Default Forager's Pack (Pixels tab): cheap, a base skill. Five foraged blocks for the Foraging skill: Berry, Mushroom, Herb, Truffle (rare) and the
    /// filler Weeds, plus the Mold hazard (the Toadstool is switched off for now). They only spawn while Foraging is the pack in play (Skills window).
    /// </summary>
    private static ShopPack CreateForagerPack()
    {
        PixelClicker.PixelTier Block(PixelClicker.PixelType type, string name, Color color, int hits, double pay, float weight) => new PixelClicker.PixelTier
        {
            type = type, displayName = name, color = color, clicksToCollect = hits, amountPerClick = pay, spawnWeight = weight,
            unlockMode = PixelClicker.TierUnlockMode.ShopOnly
        };
        return new ShopPack
        {
            displayName = "Forager's Pack",
            tab = ShopTab.Pixels,
            description = "Adds the Foraging skill and five foraged blocks: Berry, Mushroom, Herb, Truffle (rare) and Weeds, plus a spreading Mold to watch out for. They only spawn while Foraging is the pack in play (Skills window). Weeds are a useless filler that is most of what you see at first; as your foraging level rises they get rarer and the good finds show up more.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 150 } },
            rewardTiers = new[]
            {
                Block(PixelClicker.PixelType.Berry, "Berry Pixels", new Color(0.75f, 0.15f, 0.35f, 1f), 1, 3, 1.0f),
                Block(PixelClicker.PixelType.Mushroom, "Mushroom Pixels", new Color(0.82f, 0.4f, 0.3f, 1f), 2, 4, 0.6f),
                Block(PixelClicker.PixelType.Herb, "Herb Pixels", new Color(0.3f, 0.7f, 0.35f, 1f), 1, 2, 0.9f),
                Block(PixelClicker.PixelType.Truffle, "Truffle Pixels", new Color(0.34f, 0.23f, 0.17f, 1f), 3, 30, 0.1f),
                Block(PixelClicker.PixelType.Weeds, "Weeds", new Color(0.55f, 0.62f, 0.35f, 1f), 1, 1, 1.0f),
                // The Mold: a hazard (every click spreads mold over the fallen pixels, see PixelMold.cs; it pays nothing). The Toadstool (type kept) is switched off for now.
                Block(PixelClicker.PixelType.Mold, "Mold", new Color(0.4f, 0.55f, 0.25f, 1f), 8, 0, 0.05f),
            }
        };
    }

    /// <summary>
    /// Default Angler's Pack (Pixels tab): cheap, a base skill. Five fished blocks for the Fishing skill: Fish, Seaweed, Shell, Pearl (rare) and the
    /// filler Driftwood, plus the Jellyfish hazard. They only spawn while Fishing is the pack in play (Skills window).
    /// </summary>
    /// <summary>The Whale: a rare block that takes a random 15-50 clicks (it fights the line) and pays more the longer the fight.</summary>
    private static PixelClicker.PixelTier AnglerWhale() => new PixelClicker.PixelTier
    {
        type = PixelClicker.PixelType.Whale, displayName = "Whale", color = new Color(0.3f, 0.5f, 0.75f, 1f), clicksToCollect = 33, amountPerClick = 20, spawnWeight = 0.015f,
        randomClicks = true, randomClicksMin = 15, randomClicksMax = 50,
        unlockMode = PixelClicker.TierUnlockMode.ShopOnly
    };

    private static ShopPack CreateAnglerPack()
    {
        PixelClicker.PixelTier Block(PixelClicker.PixelType type, string name, Color color, int hits, double pay, float weight) => new PixelClicker.PixelTier
        {
            type = type, displayName = name, color = color, clicksToCollect = hits, amountPerClick = pay, spawnWeight = weight,
            unlockMode = PixelClicker.TierUnlockMode.ShopOnly
        };
        return new ShopPack
        {
            displayName = "Angler's Pack",
            tab = ShopTab.Pixels,
            description = "Adds the Fishing skill: Water (very common), Seaweed, Shell, rare Pearl and rare Fish, plus a stinging Jellyfish to watch out for, and two special finds: a message in a bottle (a treasure map!) and a mighty whale. They only spawn while Fishing is the pack in play (Skills window). Clicking a Fish starts a Weak Spots round: hit every spot to catch it, miss and it gets away. Water is a plain filler that gets rarer as your fishing level rises.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 150 } },
            rewardTiers = new[]
            {
                Block(PixelClicker.PixelType.Fish, "Fish Pixels", new Color(0.45f, 0.65f, 0.85f, 1f), 1, 12, 0.15f),   // rare; clicking one starts a Weak Spots round (pass = caught)
                Block(PixelClicker.PixelType.Seaweed, "Seaweed Pixels", new Color(0.2f, 0.6f, 0.4f, 1f), 1, 2, 0.9f),
                Block(PixelClicker.PixelType.Shell, "Shell Pixels", new Color(0.95f, 0.7f, 0.75f, 1f), 1, 4, 0.6f),
                Block(PixelClicker.PixelType.Pearl, "Pearl Pixels", new Color(0.97f, 0.97f, 1f, 1f), 4, 35, 0.08f),
                Block(PixelClicker.PixelType.Water, "Water Pixels", new Color(0.3f, 0.62f, 1f, 1f), 1, 3, 0.4f),   // the filler: very common (the same Water pixel as Farming)
                Block(PixelClicker.PixelType.Driftwood, "Driftwood Pixels", new Color(0.62f, 0.58f, 0.5f, 1f), 1, 1, 0.05f),   // a second, rarer filler (5% of Fishing's pixels; weights are in PixelFishingSpawns)
                // The Jellyfish: a hazard (hit it too often and it stings everything around the cube; it pays nothing).
                Block(PixelClicker.PixelType.Jellyfish, "Jellyfish", new Color(0.9f, 0.45f, 0.8f, 1f), 9, 0, 0.05f),
                // Bait: drops bait pellets round the cube; fish show up more while they lie there (a Jellyfish going off zaps them all).
                Block(PixelClicker.PixelType.Bait, "Bait Pixels", new Color(0.8f, 0.55f, 0.28f, 1f), 1, 1, 0.5f),
                // The SPECIAL blocks (rarer than the base ones): a message in a bottle (4 clicks; breaking it unrolls a treasure map) and the whale.
                Block(PixelClicker.PixelType.Bottle, "Message in a Bottle", new Color(0.4f, 0.75f, 0.65f, 1f), 4, 8, 0.05f),
                AnglerWhale(),
            }
        };
    }

    /// <summary>
    /// Default Farmer's Pack (Pixels tab): a TESTING price (10 White, no requirements). Eight farm blocks for the Farming skill: Dirt, Seed, Water,
    /// Fertilizer, Pollen, Wheat, Weevil and Golden Fruit. They only spawn while Farming is the pack in play (Toggles / Skills window), unless a
    /// Farming skill tree node turned one on as a guest for other rotations.
    /// </summary>
    private static ShopPack CreateFarmerPack()
    {
        PixelClicker.PixelTier Block(PixelClicker.PixelType type, string name, Color color, int hits, double pay, float weight) => new PixelClicker.PixelTier
        {
            type = type, displayName = name, color = color, clicksToCollect = hits, amountPerClick = pay, spawnWeight = weight,
            unlockMode = PixelClicker.TierUnlockMode.ShopOnly
        };
        return new ShopPack
        {
            displayName = "Farmer's Pack",
            tab = ShopTab.Pixels,
            description = "TEST: Adds eight farm blocks: Dirt, Seed, Water, Fertilizer, Pollen, Wheat, Weevil and Golden Fruit. They only spawn while Farming is the pack in play (Toggles or Skills window) and are a farm in themselves: seeds grow sprouts, water, dirt and fertilizer speed them up, pollen doubles a sprout's pixel, wheat pays more the busier your field is, and weevils eat sprouts until you click them.",
            requirements = Needs(-1),
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 10 } },
            rewardTiers = new[]
            {
                Block(PixelClicker.PixelType.Dirt, "Dirt Pixels", new Color(0.42f, 0.28f, 0.16f, 1f), 3, 1, 1.0f),
                Block(PixelClicker.PixelType.Seed, "Seed Pixels", new Color(0.55f, 0.38f, 0.18f, 1f), 8, 15, 0.3f),
                Block(PixelClicker.PixelType.Water, "Water Pixels", new Color(0.3f, 0.62f, 1f, 1f), 1, 3, 0.4f),
                Block(PixelClicker.PixelType.Fertilizer, "Fertilizer Pixels", new Color(0.5f, 0.66f, 0.25f, 1f), 1, 2, 0.5f),
                Block(PixelClicker.PixelType.Pollen, "Pollen Pixels", new Color(1f, 0.9f, 0.3f, 1f), 1, 2, 0.4f),
                Block(PixelClicker.PixelType.Wheat, "Wheat Pixels", new Color(0.93f, 0.76f, 0.3f, 1f), 2, 4, 0.5f),
                Block(PixelClicker.PixelType.Weevil, "Weevil Pixels", new Color(0.22f, 0.15f, 0.11f, 1f), 2, 3, 0.4f),
                Block(PixelClicker.PixelType.GoldenFruit, "Golden Fruit Pixels", new Color(1f, 0.82f, 0.22f, 1f), 1, 40, 0.03f),
                // The Pest: a hazard (hit it too often and it eats every sprout; it pays nothing).
                Block(PixelClicker.PixelType.Pest, "Pest", new Color(0.4f, 0.45f, 0.14f, 1f), 6, 0, 0.05f),
            }
        };
    }

    /// <summary>Default Mirror Pixel pack (Pixels tab): listed after Electric and before Vacuum, needs the Electric pack. Clicking a Mirror also counts as a click on the last other pixel type.</summary>
    private static ShopPack CreateMirrorPixelPack(int requiresElectricIndex)
    {
        return new ShopPack
        {
            displayName = "Mirror Pixel",
            tab = ShopTab.Pixels,
            description = "Adds the Mirror pixel: polished chrome that reflects the last other pixel you collected, so clicking it pays that pixel's amount again.",
            requirements = Needs(requiresElectricIndex),
            costs = new[]
            {
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 15000 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 15000 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 15000 },
            },
            rewardTiers = new[]
            {
                new PixelClicker.PixelTier
                {
                    type = PixelClicker.PixelType.Mirror, displayName = "Mirror Pixels",
                    color = new Color(0.88f, 0.92f, 1f, 1f),
                    amountPerClick = 4,
                    spawnWeight = 0.2f,
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
                new PackCost { type = PixelClicker.PixelType.Black,  amount = 350000 },
                new PackCost { type = PixelClicker.PixelType.Vacuum, amount = 70000 },
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
                new PackCost { type = PixelClicker.PixelType.Obsidian, amount = 2800000 },
                new PackCost { type = PixelClicker.PixelType.Vacuum,   amount = 280000 },
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
                new PackLevel { value = 0.8f,  costs = AllSix(1000) },
                new PackLevel { value = 0.6f,  costs = AllSix(5000) },
                new PackLevel { value = 0.45f, costs = AllSix(25000) },
                new PackLevel { value = 0.3f,  costs = AllSix(125000) },
                new PackLevel { value = 0.2f,  costs = AllSix(625000) },
            }
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
            costs = AllSix(25000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksBank = true,
        };
    }

    /// <summary>Default Block Hold upgrade (Upgrades tab): hold the right mouse button on the cube's block to pull it out of the rotation. Cheap while testing.</summary>
    private static ShopPack CreateBlockHoldPack()
    {
        return new ShopPack
        {
            displayName = "Block Hold",
            tab = ShopTab.Upgrades,
            description = "Hold the right mouse button on the cube's block to pull it out of the rotation: the cube gives you a new one while it stays held. Let go and it goes back. Holding drains a meter; if it runs dry the block is put back and the meter takes longer to recharge.",
            costs = new[] { new PackCost { type = PixelClicker.PixelType.White, amount = 5 } },
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksBlockHold = true,
        };
    }

    /// <summary>Default Robot Worker upgrade (Upgrades tab): a robot that keeps a chosen consumable running.</summary>
    private static ShopPack CreateRobotPack()
    {
        return new ShopPack
        {
            displayName = "UFO Helper",
            tab = ShopTab.Upgrades,
            description = "A little UFO hovers about the map. Click it, choose a consumable and place its ghost: every time that device runs out it beams a new one down there, as long as you have one to spare. It can also abduct loose pixels into your Pixel Bank.",
            costs = AllSix(1000000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksRobot = true,
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
                new PackLevel { value = 100f, costs = BankStorageCost(2500, 1000, 4) },
                new PackLevel { value = 160f, costs = BankStorageCost(25000, 10000, 8) },
                new PackLevel { value = 250f, costs = BankStorageCost(250000, 100000, 14) },
                new PackLevel { value = 400f, costs = BankStorageCost(1000000, 400000, 22) },
                new PackLevel { value = 600f, costs = BankStorageCost(5000000, 2000000, 32) },
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
                new PackLevel { value = 75f,  costs = AllSix(2500) },
                new PackLevel { value = 100f, costs = AllSix(25000) },
                new PackLevel { value = 150f, costs = AllSix(250000) },
                new PackLevel { value = 200f, costs = AllSix(1000000) },
                new PackLevel { value = 300f, costs = AllSix(5000000) },
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
            costs = AllSix(150000),
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
            costs = AllSix(500000),
            rewardTiers = new PixelClicker.PixelTier[0],
            unlocksTimeSlow = true,
        };
    }

    /// <summary>Default Combo Meter upgrade: quick clicks build a combo whose milestones (10 / 50 / 200 / 500 / 1000) give a multiplier; each level unlocks the next tier.</summary>
    private static ShopPack CreateComboPack()
    {
        return new ShopPack
        {
            displayName = "Combo Meter",
            tab = ShopTab.Upgrades,
            description = "Click fast to build a combo. Reaching each combo milestone boosts your clicks; every level unlocks a higher tier. Top multiplier: x{current}  →  x{next}",
            upgradeEffect = UpgradeEffect.ComboMeter,
            levels = new[]
            {
                new PackLevel { value = 1.5f, costs = AllSix(500) },
                new PackLevel { value = 2f,   costs = AllSix(5000) },
                new PackLevel { value = 3f,   costs = AllSix(50000) },
                new PackLevel { value = 4f,   costs = AllSix(500000) },
                new PackLevel { value = 5f,   costs = AllSix(5000000) },
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
                new PackLevel { value = 2f, costs = AllSix(2500) },
                new PackLevel { value = 3f, costs = AllSix(25000) },
                new PackLevel { value = 4f, costs = AllSix(250000) },
                new PackLevel { value = 5f, costs = AllSix(2500000) },
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
                new PackCost { type = PixelClicker.PixelType.White, amount = 1000 },
                new PackCost { type = PixelClicker.PixelType.Gray,  amount = 1000 },
                new PackCost { type = PixelClicker.PixelType.Black, amount = 1000 },
                new PackCost { type = PixelClicker.PixelType.Red,   amount = 1000 },
                new PackCost { type = PixelClicker.PixelType.Green, amount = 1000 },
                new PackCost { type = PixelClicker.PixelType.Blue,  amount = 1000 },
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
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.ComboMeter,
                          requires = null, create = i => CreateComboPack() },
        new DefaultPack { isThis = p => p.unlocksAutoClicker,
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateAutoClickerPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Glass),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateGlassPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Wood),
                          requires = null, create = i => CreateWoodPixelPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Berry),
                          requires = null, create = i => CreateForagerPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Fish),
                          requires = null, create = i => CreateAnglerPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Dirt),
                          requires = null, create = i => CreateFarmerPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Fire),
                          requires = null, create = i => CreateFirePixelPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Nuclear),
                          requires = null, create = i => CreateNuclearPixelPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Treasure),
                          requires = null, create = i => CreateTreasurePixelPack() },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Copper),
                          requires = null, create = i => CreateProspectorPack() },
        new DefaultPack { isThis = p => p.skillId == PixelSkills.ElectricianId,
                          requires = p => p.unlocksAutoClicker, create = CreateElectricianPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Mirror),
                          requires = p => Rewards(p, PixelClicker.PixelType.Electric), create = CreateMirrorPixelPack },
        new DefaultPack { isThis = p => Rewards(p, PixelClicker.PixelType.Vacuum),
                          requires = p => Rewards(p, PixelClicker.PixelType.Glass), create = CreateVacuumPack },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.AutoClickerInterval,
                          requires = p => p.unlocksAutoClicker, create = CreateIntervalUpgradePack },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.AutoClickerClicks,
                          requires = p => p.unlocksAutoClicker, create = CreateClicksUpgradePack },
        // (The Obsidian pixel is part of the Prospector's Pack now.)
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
        new DefaultPack { isThis = p => Unlocks(p, "pad"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreatePadPack },
        new DefaultPack { isThis = p => Unlocks(p, "dash"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateDashPack },
        new DefaultPack { isThis = p => Unlocks(p, "traffic"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateTrafficPack },
        new DefaultPack { isThis = p => Unlocks(p, "evade"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateEvadePack },
        new DefaultPack { isThis = p => Unlocks(p, "blink"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateBlinkPack },
        new DefaultPack { isThis = p => Unlocks(p, "breakout"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateBreakoutPack },
        new DefaultPack { isThis = p => Unlocks(p, "sort"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateSortPack },
        new DefaultPack { isThis = p => Unlocks(p, "snake"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateSnakePack },
        new DefaultPack { isThis = p => Unlocks(p, "bomb"),
                          requires = p => Rewards(p, PixelClicker.PixelType.Red), create = CreateBombPack },
        new DefaultPack { isThis = p => p.unlocksTimeStop,
                          requires = null, create = i => CreateTimeStopPack() },
        new DefaultPack { isThis = p => p.unlocksTimeSlow,
                          requires = p => p.unlocksTimeStop, create = CreateTimeSlowPack },
        new DefaultPack { isThis = p => p.unlocksBank,
                          requires = null, create = i => CreateBankPack() },
        new DefaultPack { isThis = p => p.unlocksBlockHold,
                          requires = null, create = i => CreateBlockHoldPack() },
        new DefaultPack { isThis = p => p.unlocksRobot,
                          requires = null, create = i => CreateRobotPack() },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.OldPixelCap,
                          requires = null, create = i => CreateOldPixelCapPack() },
        new DefaultPack { isThis = p => p.upgradeEffect == UpgradeEffect.BankCapacity,
                          requires = p => p.unlocksBank, create = CreateBankStoragePack },
    };

    /// <summary>
    /// Puts every built-in pack's prices back to the defaults above: one-time costs, and for upgrade packs the whole level list
    /// (costs and values). Names, descriptions, rewards and requirements are left alone. Returns how many packs were reset.
    /// </summary>
    private int ResetBuiltInPackPrices()
    {
        if (packs == null) return 0;
        int count = 0;
        foreach (DefaultPack builtIn in BuiltInPacks)
        {
            int index = Array.FindIndex(packs, p => p != null && builtIn.isThis(p));
            if (index < 0) continue;

            int requiredIndex = builtIn.requires != null ? Array.FindIndex(packs, p => p != null && builtIn.requires(p)) : -1;
            ShopPack defaults = builtIn.create(requiredIndex);
            ShopPack pack = packs[index];
            pack.costs = defaults.costs;
            if (defaults.levels != null && defaults.levels.Length > 0)
            {
                pack.levels = defaults.levels;
                pack.level = Mathf.Clamp(pack.level, 0, defaults.levels.Length);
            }
            count++;
        }
        return count;
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Built-in Pack Prices")]
    private void ResetBuiltInPackPricesFromMenu()
    {
        UnityEditor.Undo.RecordObject(this, "Reset Built-in Pack Prices");
        int count = ResetBuiltInPackPrices();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("PixelShop: reset the prices of " + count + " built-in packs to their defaults.", this);
    }

    /// <summary>Right-click the component header &gt; Add Missing Default Packs, to fill in any built-in pack that is not in the list.</summary>
    [ContextMenu("Add Missing Default Packs")]
    private void AddMissingDefaultPacksFromMenu()
    {
        UnityEditor.Undo.RecordObject(this, "Add Default Packs");
        if (EnsureDefaultPacks()) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
