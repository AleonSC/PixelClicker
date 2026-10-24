// ---- System notes ----
// PixelConsumablesDefaults.cs
//   Part of PixelConsumables (partial class): the built-in potions and devices table (default makers, prices, shop text, EnsureDefaultPotions, OnValidate). The system notes are in PixelConsumables.cs.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;

public partial class PixelConsumables
{
    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    private static Potion CreateDefaultPotion(PixelClicker.PixelType type)
    {
        double price;
        switch (type)
        {
            case PixelClicker.PixelType.Glass: price = 50; break;
            case PixelClicker.PixelType.Vacuum: price = 10; break;
            default: price = 100; break;
        }

        return new Potion
        {
            displayName = type + " Potion",
            type = type,
            costs = new[] { new PixelShop.PackCost { type = type, amount = price } },
        };
    }

    private static Device CreateDefaultDevice()
    {
        return new Device
        {
            devOnly = true,   // (2026-10-18: out of the shop for now, Dev Hub > Main > Dev items gives it)
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass,  amount = 100 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Vacuum, amount = 10 },
            },
        };
    }

    private static Device CreateDefaultFan()
    {
        return new Device
        {
            kind = DeviceKind.Fan,
            displayName = "Fan",
            description = "Place it and turn it. It gently blows husks along a cone up to {radius} units long for {duration} seconds.",
            requiredType = PixelClicker.PixelType.Glass,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 150 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 150 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 80 },
            },
            radius = 6f,
            color = new Color(0.4f, 0.85f, 1f, 1f),
            bodyDiameter = 0.9f,
            bodyHeight = 0.9f,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultMagnet()
    {
        return new Device
        {
            kind = DeviceKind.Magnet,
            displayName = "Magnet",
            description = "Place it near the cube. For {duration} seconds it pulls every loose husk within {radius} units into a heap around it, without collecting them.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 20 },
            },
            durationSeconds = 30f,
            radius = 5f,
            pullAcceleration = 24f,
            absorbDistance = 0.9f,   // inside this distance a pixel just settles (nothing is collected)
            color = new Color(0.85f, 0.2f, 0.2f, 1f),
            bodyDiameter = 0.8f,
            bodyHeight = 0.9f,
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultStorageCrate()
    {
        return new Device
        {
            kind = DeviceKind.StorageCrate,
            displayName = "Storage Crate",
            description = "A sectioned crate with room for 10 pixels (2 rows of 5). It needs no switching on and stays until you remove it. Drag pixels onto it (Pixel Grabbing) and click the crate to store them in the next free sections; click a stored pixel to pick it up again.",
            requiredType = PixelClicker.PixelType.Gray,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 200 },
            },
            maxHeld = 3,
            durationSeconds = 1e9f,
            radius = 1f,
            boxCapacity = 10,
            color = new Color(0.62f, 0.45f, 0.28f, 1f),
            bodyDiameter = 2.5f,   // the crate's width (five compartments)
            bodyHeight = 0.3f,     // wall height
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultRecycler()
    {
        return new Device
        {
            kind = DeviceKind.Recycler,
            displayName = "Recycler",
            description = "A bin that recycles every husk that lands in it - or that its suction ring draws in from close by - for half its value as currency (same colour in a row = a streak that raises the pay, never above a click). Works for {duration} s; Speed / Size upgrades in the shop's Operation tab. Several can stand at once; each extra one costs more.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 75 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 50 },
            },
            maxHeld = 3,
            priceGrowthPerOwned = 2f,   // each Recycler you hold or have standing doubles the next one's price (Cubie's free one makes the 2nd 150 White + 100 Green)
            durationSeconds = 300f,   // 5 minutes (Operation: Run Time)
            radius = PixelRecycler.DefaultSize,   // the bin's size factor (1.8 = nearly twice as wide and deep as the first bin; Operation: Size adds to it)
            color = new Color(0.25f, 0.55f, 0.32f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 2f,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultSortingHopper()
    {
        return new Device
        {
            kind = DeviceKind.SortingHopper,
            displayName = "Sorting Hopper",
            description = "A big sorting funnel that holds up to 20 husks dropped into it. Right-click it and pick a filter: that type drops out of its green pipe, one after another - put a Recycler or a belt under it; every other husk drops out of its bottom spout. Drag it by its handles to move it. The Outputs upgrade adds side pipes (left, right, back), each with its own filter. It stays until you remove it.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 200 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 150 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 6f,   // how far from a Recycler's back it latches on (world units)
            color = new Color(0.3f, 0.48f, 0.6f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 1.5f,
            vendorOnly = true,          // not in the shop until...
            shopSkillId = "standard",   // ...Standard level 3 puts it on sale (GameDesign section 6: the first general husk tool); no vendor sells it
            shopSkillLevel = 3,
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it - near a Recycler it latches onto its back  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultFunnel()
    {
        return new Device
        {
            devOnly = true,   // (2026-10-18: out of the shop for now, Dev Hub > Main > Dev items gives it)
            kind = DeviceKind.Funnel,
            displayName = "Funnel",
            description = "A wide glass funnel that hangs under the clicker cube and catches the pixels that fall off it. They are carried down a see-through tube and come out at an output on the floor - click where you want it. Fit a coupling on its output and start a conveyor belt there to feed it. Only one stands at a time: placing it again moves it.",
            requiredType = PixelClicker.PixelType.Gray,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 350 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 200 },
            },
            maxHeld = 1,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.55f, 0.8f, 1f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 3f,
            placingMessage = "Click the floor where the {0}'s output should be. Scroll or Q / E turns the spout a little; fit a coupling on its output socket to send the husks down a belt  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultWoodChipper()
    {
        return new Device
        {
            kind = DeviceKind.WoodChipper,
            vendorOnly = true,
            shopSkillId = "woodcutting",   // Woodcutting level 2 puts it on sale in the shop as well
            shopSkillLevel = 2,
            displayName = "Wood Chipper",
            description = "A trailer-mounted wood chipper, bought from the Lumberjack. Wood pixels dropped into its hopper are shredded for Woodcutting XP and every few of them make a Wood Chips block that shoots out of its chute (fit a coupling on the chute's socket to send them down a belt). Anything that is not wood damages it, and enough of that breaks it for good.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Wood, amount = 400 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Bark, amount = 150 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.9f, 0.45f, 0.1f, 1f),
            bodyDiameter = 3f,
            bodyHeight = 3f,
            placingMessage = "Scroll or Q / E to turn the {0}. The arrow shows where its chips go (a coupling on the chute's socket sends them down a belt). Click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultQuantumOrganizer()
    {
        return new Device
        {
            devOnly = true,   // (2026-10-18: out of the shop for now, Dev Hub > Main > Dev items gives it)
            kind = DeviceKind.QuantumOrganizer,
            displayName = "Quantum Organizer",
            description = "A pad with walls. Switch it on, then grab a husk and drop it onto the pad to bind that type to it (up to three types; husks that just fall on it don't count). Every loose husk of a bound type within a wide reach is dematerialized with a glitch and rematerializes inside the box as storage (up to 60 husks). Click the box to let some out again; everything comes out when its {duration} seconds run out.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 60 },
            },
            maxHeld = 3,
            durationSeconds = 600f,
            radius = 24f,         // how far from the pad it reaches for pixels of its bound types (2026-10-19: 9 -> 24, about the whole play area)
            boxCapacity = 60,
            color = new Color(0.45f, 0.3f, 0.9f, 1f),
            bodyDiameter = 2.4f,  // the pad's inner side
            bodyHeight = 0.5f,    // wall height
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultMagicBeans()
    {
        return new Device
        {
            kind = DeviceKind.Seed,
            displayName = "Magic Beans",
            description = "Plant them (right-click in the Inventory, then click the floor). A tall beanstalk grows with a handful of random cubes along it, and one of them is a Dragon Cube you don't hold yet.",
            requiredType = PixelClicker.PixelType.White,
            seedType = PixelClicker.PixelType.White,
            beans = true,
            craftOnly = true,
            maxHeld = 1,
            costs = new PixelShop.PackCost[0],
            color = new Color(0.45f, 0.8f, 0.3f, 1f),
            placingMessage = "Click the floor to plant the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultSprinkler()
    {
        return new Device
        {
            devOnly = true,   // (2026-10-18: out of the shop for now, Dev Hub > Main > Dev items gives it)
            kind = DeviceKind.Sprinkler,
            displayName = "Sprinkler",
            description = "Place it near your seeds. For {duration} seconds it waters every growing sprout and Seed pet within {radius} units once a second, so they grow far faster.",
            requiredType = PixelClicker.PixelType.Water,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Water, amount = 40 },
            },
            durationSeconds = 45f,
            radius = 4.5f,
            sprayInterval = 1f,
            color = new Color(0.3f, 0.7f, 1f, 1f),
            bodyDiameter = 0.5f,
            bodyHeight = 0.7f,
            placingMessage = "Click the floor near your seeds to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultComboFuel()
    {
        return new Device
        {
            kind = DeviceKind.ComboFuel,
            displayName = "Combo Fuel",
            description = "Hold up to {held}. When your combo bar would run out, one is used automatically: the combo is kept and the bar slowly refills instead. Click before it is full again to carry on.",
            requiredType = PixelClicker.PixelType.Red,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red,   amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Green, amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue,  amount = 250 },
            },
            maxHeld = 3,
            color = new Color(0.35f, 0.8f, 1f, 1f),
        };
    }

    private static Device CreateDefaultGhostBait()
    {
        return new Device
        {
            kind = DeviceKind.GhostBait,
            displayName = "Ghost Bait",
            description = "Right-click to use (needs Ghost Hunt). The next ghost appears sooner, and three ghosts float by, slower than usual. You can only hold one.",
            requiredType = PixelClicker.PixelType.Glass,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 500 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 300 },
            },
            maxHeld = 1,
            color = new Color(0.8f, 0.85f, 1f, 1f),
        };
    }

    private static Device CreateDefaultBaitBucket()
    {
        return new Device
        {
            devOnly = true,   // (2026-10-18: out of the shop for now, Dev Hub > Main > Dev items gives it)
            kind = DeviceKind.BaitBucket,
            displayName = "Bait Bucket",
            description = "Right-click to set it out (needs Fishing as the skill in play). A bucket stands on the floor until you stop Fishing. Drag Fish and Water pixels into it (Pixel Grabbing): they are stored with their value instead of splashing or fading, and every stored fish makes more fish show up. Click the bucket to take a pixel out again. You can only hold one.",
            requiredType = PixelClicker.PixelType.Fish,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Water, amount = 150 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Seaweed, amount = 25 },
            },
            maxHeld = 1,
            color = new Color(0.8f, 0.22f, 0.22f, 1f),
        };
    }

    private static Device CreateDefaultGauntlet()
    {
        return new Device
        {
            kind = DeviceKind.Gauntlet,
            displayName = "Prime Gauntlet",
            description = "Right-click to use. A golden cube with a socket on every face: set the six Prime Cubes in it and forge the Prime Cubelette. The Gauntlet and the cubes are used up. You can only hold one.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 2000000 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 1000000 },
            },
            maxHeld = 1,
            vendorOnly = true,
            color = new Color(1f, 0.82f, 0.22f, 1f),
        };
    }

    private static Device CreateDefaultSweeper()
    {
        return new Device
        {
            kind = DeviceKind.Sweeper,
            displayName = "Sweeper",
            description = "A little wind-up clockwork cube with a broom. It rolls out from its pad and rescues husks that are about to fade, carrying each one to the NEAREST Storage Crate, Sorting Hopper or Recycler with room - so where you put it decides where strays go. It runs about a minute, then slows to a stop: click it to wind it again. It stays until you remove it.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 400 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 200 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.86f, 0.66f, 0.3f, 1f),
            bodyDiameter = 1.5f,
            bodyHeight = 1f,
            vendorOnly = true,          // not in the shop until...
            shopSkillId = "standard",   // ...Standard level 5 puts it on sale (GameDesign section 6 'The Sweeper'); no vendor sells it
            shopSkillLevel = 5,
            placingMessage = "Click the floor to put the {0}'s pad down - it sweeps strays to the nearest crate, hopper or Recycler  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultJunctionBox()
    {
        return new Device
        {
            kind = DeviceKind.JunctionBox,
            displayName = "Junction Box",
            description = "A box with eight coupling sockets, two on each side, and an opening on top that gently draws in husks just above it. Fit couplings and connect belts or hoses: a belt or hose bringing husks in makes that coupling an IN, one taking husks away makes it an OUT. Husks that go in are taken apart and new ones come out: they are shared out in turn between the outputs - or sent to the output whose FILTER is their type (drop a husk on a coupling, or click / scroll it, to set its filter). When no output can take a husk it waits in a small buffer. It stays until you remove it.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]   // (a machine, so monochrome: ~3.5 min of Gray, 2.5 of Black at early income)
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 200 },
            },
            maxHeld = 3,
            durationSeconds = 1e9f,
            radius = 1f,
            color = new Color(0.22f, 0.36f, 0.42f, 1f),
            bodyDiameter = 2f,
            bodyHeight = 1.3f,
            // (2026-10-19: on sale in the shop's Utilities for everyone; Dev Hub > Tester > Junction Box still places one too)
            placingMessage = "Scroll or Q / E to turn the {0}, click the floor to place it  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultPocketBiomassGenerator()
    {
        return new Device
        {
            kind = DeviceKind.PocketBiomassGenerator,
            displayName = "Pocket Biomass Generator",
            description = "A little generator that burns Woodcutting husks (Charcoal and Wood Chips burn longer). Every device inside its blue ring runs on its power, so their timers stop - they all show its blue energy bar. It comes fully charged and slowly runs down: drop wood husks into the firebox on top to top it up. When it runs dry it flashes red for a few seconds and then shuts down for good - unless you feed it in time.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Wood, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 150 },
            },
            maxHeld = 3,
            durationSeconds = 1e9f,
            radius = 4f,                   // how far its power reaches (world units)
            generatorMaxCharge = 60f,
            generatorChargePerHusk = 10f,
            color = new Color(0.33f, 0.45f, 0.24f, 1f),
            bodyDiameter = 0.7f,           // the model's size (the big one is 1.2)
            bodyHeight = 1f,
            placingMessage = "Click the floor to put the {0} down - every device inside its blue ring runs on its power  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultBiomassGenerator()
    {
        return new Device
        {
            kind = DeviceKind.BiomassGenerator,
            displayName = "Biomass Generator",
            description = "A big generator that burns Woodcutting husks (Charcoal and Wood Chips burn longer). Every device inside its wide blue ring runs on its power, so their timers stop - they all show its blue energy bar. It comes fully charged and holds much more than the pocket one: drop wood husks into the firebox on top (or feed them in through its intake coupling). When it runs dry it flashes red for a few seconds and then shuts down for good - unless you feed it in time.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Wood, amount = 2500 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 800 },
            },
            maxHeld = 2,
            durationSeconds = 1e9f,
            radius = 8f,
            generatorMaxCharge = 240f,
            generatorChargePerHusk = 15f,
            color = new Color(0.33f, 0.45f, 0.24f, 1f),
            bodyDiameter = 1.2f,
            bodyHeight = 1f,
            vendorOnly = true,               // not in the shop until...
            shopSkillId = "woodcutting",     // ...Woodcutting level 10 puts it on sale
            shopSkillLevel = 10,
            placingMessage = "Click the floor to put the {0} down - every device inside its blue ring runs on its power  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultPowerBank()
    {
        return new Device
        {
            kind = DeviceKind.PowerBank,
            displayName = "Power Bank",
            description = "A big battery. Biomass Generators standing inside its wide ring charge it with their spare power (they always keep some for themselves), and while it holds charge every device in the ring runs on it - their timers stop and show its energy bar. It comes empty and stays until you remove it; an idle bank keeps its charge.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]   // (2026-10-20: a first price - the Shops session owns it)
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Wood, amount = 4000 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 1500 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 1500 },
            },
            maxHeld = 1,
            durationSeconds = 1e9f,
            radius = 16f,                    // how far its power reaches, and how far it draws from generators
            generatorMaxCharge = 1800f,      // how much it holds (seconds of power for one device)
            color = new Color(0.45f, 0.85f, 1f, 1f),
            bodyDiameter = 2.4f,
            bodyHeight = 2.5f,
            vendorOnly = true,               // not in the shop until...
            shopSkillId = "woodcutting",     // ...Woodcutting level 10 puts it on sale, with the big generator
            shopSkillLevel = 10,
            placingMessage = "Click the floor to put the {0} down - generators inside its ring charge it, devices inside run on it  (right-click to cancel)",
        };
    }

    /// <summary>
    /// The conveyor-line pieces sold in the shop's Utilities (2026-10-20, moved out of the Dev Hub Tester): Belt Sorter, Conveyor Elevator, Belt Jump,
    /// Conveyor Combiner and Solar Panel. Each is placed by its tester feature (PixelInstantItems.PlacePieces): one is used up per piece placed.
    /// </summary>
    private static Device CreateDefaultBeltPiece(DeviceKind kind)
    {
        Device d = new Device { kind = kind, requiredType = PixelClicker.PixelType.White, maxHeld = 5 };
        PixelShop.PackCost Cost(PixelClicker.PixelType t, double a) => new PixelShop.PackCost { type = t, amount = a };
        switch (kind)
        {
            case DeviceKind.BeltSorter:
                d.displayName = "Belt Sorter";
                d.description = "A low gantry that sits on a conveyor belt and kicks every husk of ONE type off the side - onto the floor, or onto a belt that starts at its chute. Drop a husk on its tray to choose the type. Right-click to place it: bring it over a belt and it snaps on. One is used up per sorter.";
                d.costs = new[] { Cost(PixelClicker.PixelType.Gray, 200), Cost(PixelClicker.PixelType.Black, 150) };
                d.color = new Color(0.85f, 0.55f, 0.25f, 1f);
                break;
            case DeviceKind.ConveyorElevator:
                d.displayName = "Conveyor Elevator";
                d.description = "A tower that lifts husks from the end of a belt up to a higher belt. Right-click to place it: bring it to a belt's end (it snaps on), click, move the mouse up or down to set the height and click again. Start a new belt at its top. One is used up per tower.";
                d.costs = new[] { Cost(PixelClicker.PixelType.Gray, 250), Cost(PixelClicker.PixelType.Black, 200) };
                d.color = new Color(0.5f, 0.6f, 0.75f, 1f);
                break;
            case DeviceKind.BeltJump:
                d.displayName = "Belt Jump";
                d.description = "A pair of ramps for a conveyor line: husks reaching a belt's end are launched over a gap and land on a belt that starts at the landing ramp - so a line can cross over other things. Right-click to place it on a belt's end, then scroll to set the gap. One is used up per jump.";
                d.costs = new[] { Cost(PixelClicker.PixelType.Gray, 150), Cost(PixelClicker.PixelType.Black, 100) };
                d.color = new Color(0.9f, 0.75f, 0.3f, 1f);
                break;
            case DeviceKind.ConveyorCombiner:
                d.displayName = "Conveyor Combiner";
                d.description = "A box that sits on a conveyor belt and merges the belts that end at its two side inlets onto it whenever there is a gap - two feeds become one. Right-click to place it: bring it over a belt and it snaps on. One is used up per box.";
                d.costs = new[] { Cost(PixelClicker.PixelType.Gray, 200), Cost(PixelClicker.PixelType.Black, 150) };
                d.color = new Color(0.45f, 0.7f, 0.55f, 1f);
                break;
            case DeviceKind.SolarPanel:
                d.displayName = "Solar Panel";
                d.requiredType = PixelClicker.PixelType.Solar;
                d.description = "A flat solar panel: while Solar husks lie on it, it stops the countdown of the ONE device it is linked to. Solar husks on it are slowly used up, unless they sit in a complete group of 5. Right-click to place it, then click a device inside its blue ring to link it. One is used up per panel.";
                d.costs = new[] { Cost(PixelClicker.PixelType.Solar, 150), Cost(PixelClicker.PixelType.Black, 100) };
                d.color = new Color(0.3f, 0.45f, 0.85f, 1f);
                break;
        }
        return d;
    }

    private static Device CreateDefaultCoupling()
    {
        return new Device
        {
            kind = DeviceKind.Coupling,
            displayName = "Coupling",
            description = "Dev item: fits a coupling on a device's free socket by hand. In the game couplings fit themselves when a belt or hose attaches to a socket (the port plates). Its arrow shows the way husks go (green in, orange out; click an organizer's coupling dot to switch).",
            devOnly = true,   // (2026-10-20: no longer sold - couplings fit themselves; Dev Hub > Main > Dev items gives it)
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 40 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 40 },
            },
            maxHeld = 10,
            color = new Color(0.62f, 0.66f, 0.7f, 1f),
        };
    }

    private static Device CreateDefaultContainerHose()
    {
        return new Device
        {
            kind = DeviceKind.ContainerHose,
            displayName = "Container Hose",
            description = "A see-through hose that pumps stored husks from a Storage Crate or Quantum Organizer (an Out coupling) into any In coupling - the Recycler, another crate, a Trash Can... Right-click to lay one: click the start coupling, click bend points to lead it round things, then click the end coupling. A quick right click on a laid hose turns its flow round. One is used up per hose.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 80 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 80 },
            },
            maxHeld = 3,
            color = new Color(0.6f, 0.9f, 1f, 1f),
        };
    }

    private static Device CreateDefaultConveyorBelt()
    {
        return new Device
        {
            kind = DeviceKind.ConveyorBelt,   // (2026-10-19: back in the shop, Utilities, for everyone - a machine, so monochrome)
            displayName = "Conveyor Belt Kit",
            description = "Right-click to lay conveyor belts: click a point, then another, and a belt is built between them; keep clicking to chain pieces - the turns between them are rounded (scroll raises or lowers the end). Husks that land on a belt are carried along it, and belts snap to couplings on machines. Right click ends a chain; hold the right button on a belt to remove it. The kit is not used up. You can only hold one.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Gray, amount = 250 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 150 },
            },
            maxHeld = 1,
            color = new Color(0.95f, 0.8f, 0.2f, 1f),
        };
    }

    private static Device CreateDefaultPetTreat()
    {
        return new Device
        {
            kind = DeviceKind.PetTreat,
            displayName = "Pet Treat",
            description = "Right-click to use. Your pets go hyper and glow for a minute, and the extra spawn chance they give is doubled. Hold up to {held}.",
            requiredType = PixelClicker.PixelType.Black,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Red, amount = 1500 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Blue, amount = 1800 },
            },
            maxHeld = 5,
            color = new Color(1f, 0.85f, 0.4f, 1f),
        };
    }

    private static Device CreateDefaultFocusPotion()
    {
        return new Device
        {
            kind = DeviceKind.FocusPotion,
            displayName = "Lumberjack's Focus",
            description = "Right-click to drink. For a minute no Termite Hive turns up while you train Woodcutting. Sold now and then by the Lumberjack. Hold up to {held}.",
            requiredType = PixelClicker.PixelType.Wood,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.HornetNest, amount = 3 },   // (the Lumberjack adds two random pure pixels each visit)
            },
            maxHeld = 5,
            vendorOnly = true,
            color = new Color(0.45f, 0.85f, 0.35f, 1f),
        };
    }

    private static Device CreateDefaultPesticideSpray()
    {
        return new Device
        {
            kind = DeviceKind.PesticideSpray,
            displayName = "Pesticide Spray",
            description = "Right-click to use. Adds {charges} Pesticide charges: each one clears a Termite Hive by itself. Sold by the Lumberjack. Hold up to {held}.",
            requiredType = PixelClicker.PixelType.Pesticide,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Pesticide, amount = 5 },   // (the Lumberjack adds a random pure pixel each visit)
            },
            maxHeld = 10,
            vendorOnly = true,
            color = new Color(0.75f, 0.85f, 0.25f, 1f),
        };
    }

    private static Device CreateDefaultChargeBooster()
    {
        return new Device
        {
            kind = DeviceKind.ChargeBooster,
            displayName = "Charge Booster",
            description = "Place it. Electric pixels that link to it fill its meter and are used up. When the meter is full, click it for a few seconds of super-charged auto clicking. Dissolves after 100 auto clicks.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 200 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Glass, amount = 100 },
            },
            uses = 100, chargeCapacity = 10f, superChargeSeconds = 6f, superClicksPerSecond = 12f,
            radius = 3f,
            color = new Color(1f, 0.9f, 0.3f, 1f),
            bodyDiameter = 0.8f,
            bodyHeight = 0.8f,
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    private static Device CreateDefaultLightningRod()
    {
        return new Device
        {
            kind = DeviceKind.LightningRod,
            displayName = "Lightning Rod",
            description = "Place it. Every few seconds lightning strikes it and turns every husk around it into an Electric pixel. Dissolves after {uses} strikes.",
            requiredType = PixelClicker.PixelType.Electric,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.Electric, amount = 300 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Obsidian, amount = 50 },
            },
            uses = 8, strikeSeconds = 3f,
            radius = 5f,
            color = new Color(0.6f, 0.75f, 1f, 1f),
            bodyDiameter = 0.12f,
            bodyHeight = 2.5f,
            placingMessage = "Click the floor to place the {0}  (right-click to cancel)",
        };
    }

    /// <summary>True for the pixel types that have a seed: not rare drops, not fly-away types (they never land) and not the Seed pixel itself.</summary>
    private static bool HasSeed(PixelClicker.PixelType type) =>
        !PixelClicker.IsRareCube(type) && !PixelClicker.IsStandalone(type) && type != PixelClicker.PixelType.Seed && type != PixelClicker.PixelType.Meteor;

    /// <summary>The Dragon Seed: a rare seed only the Farmer offers; it grows a Dragon Cube the player doesn't hold (marked by seedType = DragonCube1).</summary>
    private Device CreateDefaultDragonSeed()
    {
        return new Device
        {
            kind = DeviceKind.Seed,
            seedType = PixelClicker.PixelType.DragonCube1,
            displayName = "Dragon Seed",
            description = "A glowing seed. Plant it: it sprouts into a Dragon Cube you don't have yet.",
            requiredType = PixelClicker.PixelType.White,
            costs = new[]
            {
                new PixelShop.PackCost { type = PixelClicker.PixelType.White, amount = 500000 },
                new PixelShop.PackCost { type = PixelClicker.PixelType.Black, amount = 250000 },
            },
            maxHeld = 3,
            color = new Color(1f, 0.85f, 0.2f, 1f),
            placingMessage = "Click the floor to plant the {0}  (right-click to stop)",
        };
    }

    private Device CreateDefaultSeed(PixelClicker.PixelType type)
    {
        return new Device
        {
            kind = DeviceKind.Seed,
            seedType = type,
            displayName = type + " Seed",
            description = "Right-click it in the inventory, then click the floor to plant it. It sprouts and grows one " + type + " pixel.",
            requiredType = type,
            costs = new PixelShop.PackCost[0],
            maxHeld = seedMaxHeld,
            color = new Color(0.45f, 0.85f, 0.35f, 1f),
            placingMessage = "Click the floor to plant a {0}  (right-click to stop)",
        };
    }

    private static Device[] CreateDefaultDevices() => new[]
    {
        CreateDefaultDevice(), CreateDefaultFan(),
        CreateDefaultComboFuel(), CreateDefaultGhostBait(), CreateDefaultPetTreat(), CreateDefaultBaitBucket(),
        CreateDefaultChargeBooster(), CreateDefaultLightningRod(),
    };

    private static Potion[] CreateDefaultPotions()
    {
        System.Collections.Generic.List<Potion> list = new System.Collections.Generic.List<Potion>();
        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            if (!PixelClicker.IsRareCube(type)) list.Add(CreateDefaultPotion(type)); // a potion for every pixel (only the Dragon Cubes have none)
        return list.ToArray();
    }

    /// <summary>
    /// Every built-in device kind and the method that makes its default (one entry per kind; seeds are made per pixel type
    /// elsewhere). Used to add missing devices to a saved list and by the price reset (consumablePriceVersion).
    /// </summary>
    private static readonly System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>[] DefaultDeviceMakers =
    {
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Vacuum, CreateDefaultDevice),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Fan, CreateDefaultFan),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ComboFuel, CreateDefaultComboFuel),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.GhostBait, CreateDefaultGhostBait),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PetTreat, CreateDefaultPetTreat),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ChargeBooster, CreateDefaultChargeBooster),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.LightningRod, CreateDefaultLightningRod),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Sprinkler, CreateDefaultSprinkler),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Magnet, CreateDefaultMagnet),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.QuantumOrganizer, CreateDefaultQuantumOrganizer),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.StorageCrate, CreateDefaultStorageCrate),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.BaitBucket, CreateDefaultBaitBucket),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.WoodChipper, CreateDefaultWoodChipper),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Funnel, CreateDefaultFunnel),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Gauntlet, CreateDefaultGauntlet),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ConveyorBelt, CreateDefaultConveyorBelt),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Recycler, CreateDefaultRecycler),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.FocusPotion, CreateDefaultFocusPotion),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PesticideSpray, CreateDefaultPesticideSpray),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.SortingHopper, CreateDefaultSortingHopper),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Coupling, CreateDefaultCoupling),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ContainerHose, CreateDefaultContainerHose),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.Sweeper, CreateDefaultSweeper),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.JunctionBox, CreateDefaultJunctionBox),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PocketBiomassGenerator, CreateDefaultPocketBiomassGenerator),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.BiomassGenerator, CreateDefaultBiomassGenerator),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.BeltSorter, () => CreateDefaultBeltPiece(DeviceKind.BeltSorter)),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ConveyorElevator, () => CreateDefaultBeltPiece(DeviceKind.ConveyorElevator)),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.BeltJump, () => CreateDefaultBeltPiece(DeviceKind.BeltJump)),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.ConveyorCombiner, () => CreateDefaultBeltPiece(DeviceKind.ConveyorCombiner)),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.SolarPanel, () => CreateDefaultBeltPiece(DeviceKind.SolarPanel)),
        new System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>>(DeviceKind.PowerBank, CreateDefaultPowerBank),
    };

    /// <summary>
    /// One-time price reset: every built-in device in the saved list takes its coded default price (costs and the rising-price
    /// factor), since the Inspector list keeps the old typed prices otherwise. Bump consumablePriceVersion to run it again after a
    /// repricing. Seeds and potions are priced from their pixel at run time, so they are not touched.
    /// </summary>
    private bool ResetDefaultDevicePrices()
    {
        if (devices == null) return false;
        bool changed = false;
        foreach (Device d in devices)
        {
            if (d == null || d.attuned || d.kind == DeviceKind.Seed) continue;
            foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> maker in DefaultDeviceMakers)
            {
                if (maker.Key != d.kind) continue;
                Device fresh = maker.Value();
                d.costs = fresh.costs;
                d.priceGrowthPerOwned = fresh.priceGrowthPerOwned;
                changed = true;
                break;
            }
        }
        return changed;
    }

    /// <summary>The name a built-in device is known by in ShopText.txt: its kind's coded default name (null for seeds and attuned copies).</summary>
    private static string ShopTextKey(Device d)
    {
        if (d == null || d.attuned || d.kind == DeviceKind.Seed) return null;
        foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> maker in DefaultDeviceMakers)
            if (maker.Key == d.kind) return PixelShopText.DeviceKey(maker.Value().displayName);
        return null;
    }

    /// <summary>
    /// Resources/ShopText.txt (PixelShopText): in the Editor, adds the devices it doesn't list yet with their current text and prices;
    /// then copies every name, description and price the file gives over the device's own (the file wins over the Inspector at play).
    /// </summary>
    private void ApplyShopText()
    {
        if (devices == null) return;
        string[] keys = new string[devices.Length];
        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> blocks = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
        for (int i = 0; i < devices.Length; i++)
        {
            keys[i] = ShopTextKey(devices[i]);
            if (keys[i] != null && Application.isEditor)
                blocks.Add(new System.Collections.Generic.KeyValuePair<string, string>(keys[i], PixelShopText.Block(keys[i], devices[i].displayName, devices[i].description, devices[i].costs, null)));
        }
        PixelShopText.AppendMissing(blocks);

        for (int i = 0; i < devices.Length; i++)
        {
            PixelShopText.Entry e = keys[i] != null ? PixelShopText.Find(keys[i]) : null;
            if (e == null) continue;
            Device d = devices[i];
            if (e.name != null) d.displayName = e.name;
            if (e.description != null) d.description = e.description;
            PixelShop.PackCost[] price = PixelShopText.ParsePrice(e.price, clicker, keys[i]);
            if (price != null) d.costs = price;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Built-in Device Prices")]
    private void ResetDevicePricesMenu()
    {
        UnityEditor.Undo.RecordObject(this, "Reset Built-in Device Prices");
        if (ResetDefaultDevicePrices()) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    /// <summary>A built-in kind's coded default name ("" when it has no default maker).</summary>
    private static string DefaultNameOf(DeviceKind kind)
    {
        foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> maker in DefaultDeviceMakers) if (maker.Key == kind) return maker.Value().displayName;
        return "";
    }

    /// <summary>Device kinds taken out of the game (their numbers stay reserved in the enum): the ring Sorter and the Magnetized Sorter.</summary>
    public static bool IsRetiredKind(DeviceKind kind) => kind == DeviceKind.Sorter || kind == DeviceKind.MagnetSorter;

    /// <summary>Adds a potion for every pixel type that doesn't have one. Returns true if anything was added.</summary>
    private bool EnsureDefaultPotions()
    {
        bool added = false;
        // The steps in order ('|=' runs every step even when an earlier one already added something).
        added |= AddMissingDefaultDevices();
        FixSavedDeviceTexts();
        added |= MigrateDevicePriceVersions();
        FixWoodChipperSkillGate();
        added |= AddMissingSeeds();
        added |= AddMissingDefaultPotions();
        return added;
    }

    // EnsureDefaultPotions step 1: drop retired kinds, add every built-in device kind the list lacks.
    private bool AddMissingDefaultDevices()
    {
        bool added = false;

        if (addDefaultDevices)
        {
            // 2026-10-20: the ring Sorter and the Magnetized Sorter were removed from the game: drop their entries from a saved list.
            if (devices != null && Array.Exists(devices, d => d != null && IsRetiredKind(d.kind)))
            {
                devices = Array.FindAll(devices, d => d == null || !IsRetiredKind(d.kind));
                added = true;
            }

            if (devices == null || devices.Length == 0)
            {
                devices = CreateDefaultDevices();
                added = true;
            }
            else
            {
                if (!Array.Exists(devices, d => d != null && d.kind == DeviceKind.Fan))
                {
                    Array.Resize(ref devices, devices.Length + 1);
                    devices[devices.Length - 1] = CreateDefaultFan();
                    added = true;
                }
                foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> extra in DefaultDeviceMakers)
                {
                    DeviceKind wanted = extra.Key;
                    if (Array.Exists(devices, d => d != null && d.kind == wanted)) continue;
                    Array.Resize(ref devices, devices.Length + 1);
                    devices[devices.Length - 1] = extra.Value();
                    added = true;
                }
            }
        }
        return added;
    }

    // EnsureDefaultPotions step 2: saved Gauntlet / Recycler / Sorting Hopper entries take the newer coded names, texts and sizes.
    private void FixSavedDeviceTexts()
    {
        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.Gauntlet && d.displayName != null && d.displayName.StartsWith("Infinity")) { Device fresh = CreateDefaultGauntlet(); d.displayName = fresh.displayName; d.description = fresh.description; }   // renamed (Prime Gauntlet)

        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.Recycler && d.description != null && d.description.Contains("up to 12")) d.description = CreateDefaultRecycler().description;   // 10 husks / 50% now

        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.Recycler && d.durationSeconds >= 1e8f) { Device fresh = CreateDefaultRecycler(); d.durationSeconds = fresh.durationSeconds; d.description = fresh.description; }   // (2026-10-16: the Recycler lasts 5 minutes)

        if (devices != null)
            foreach (Device d in devices)
            {
                if (d != null && d.kind == DeviceKind.Recycler && (d.radius < 1.5f || Mathf.Approximately(d.radius, 2f))) d.radius = PixelRecycler.DefaultSize;   // (2026-10-18: 2 -> 1.8, 10% smaller)   // (2026-10-18: the Recycler is twice as wide and deep by default)
                if (d != null && d.kind == DeviceKind.Recycler && d.description != null && d.description.StartsWith("A big bin that catches")) d.description = CreateDefaultRecycler().description;   // (2026-10-18: auto recycling back)
                if (d != null && d.kind == DeviceKind.SortingHopper && d.description != null && (d.description.StartsWith("A wide tray") || d.description.Contains("side tube") || d.description.Contains("behind a Recycler"))) d.description = CreateDefaultSortingHopper().description;   // (2026-10-18: the funnel hopper)
                if (d != null && d.kind == DeviceKind.Recycler && d.priceGrowthPerOwned <= 0f) { Device fresh = CreateDefaultRecycler(); d.priceGrowthPerOwned = fresh.priceGrowthPerOwned; d.maxHeld = Mathf.Max(d.maxHeld, fresh.maxHeld); d.description = fresh.description; }   // (2026-10-17 rework: currency only, suction, a rising price for more Recyclers)
            }
    }

    // EnsureDefaultPotions step 3: the consumablePriceVersion upgrades of saved entries (repricing, dev-only flags...).
    private bool MigrateDevicePriceVersions()
    {
        bool added = false;

        if (consumablePriceVersion < 1 && devices != null)
        {
            // Shop consumable repricing (2026-10-17, GameDesign section 8): every pure-currency item costs 2-3 of the six pure colours.
            ResetDefaultDevicePrices();
            consumablePriceVersion = 1;
            added = true;
        }
        if (consumablePriceVersion < 2 && devices != null)
        {
            // 2026-10-18: the Quantum Organizer, Funnel, Sprinkler, Vacuum Device, Sorter, Bait Bucket and Conveyor Belt Kit leave the
            // shop for the Dev Tools: saved devices take the coded devOnly flag.
            foreach (Device d in devices)
            {
                if (d == null || d.attuned || d.kind == DeviceKind.Seed) continue;
                foreach (System.Collections.Generic.KeyValuePair<DeviceKind, Func<Device>> maker in DefaultDeviceMakers)
                    if (maker.Key == d.kind) { d.devOnly = maker.Value().devOnly; break; }
            }
            consumablePriceVersion = 2;
            added = true;
        }
        if (consumablePriceVersion < 3 && devices != null)
        {
            // 2026-10-19: the Conveyor Belt Kit is in the shop for everyone (monochrome price, no skill gate): saved entries take the coded ones.
            foreach (Device d in devices)
            {
                if (d == null || d.kind != DeviceKind.ConveyorBelt) continue;
                Device fresh = CreateDefaultConveyorBelt();
                d.devOnly = false; d.vendorOnly = false; d.shopSkillId = ""; d.shopSkillLevel = 0;
                d.requiredType = fresh.requiredType; d.costs = fresh.costs; d.description = fresh.description;
            }
            consumablePriceVersion = 3;
            added = true;
        }
        if (consumablePriceVersion < 4 && devices != null)
        {
            // 2026-10-19: the Junction Box goes on sale (Utilities): saved entries lose the 'vendor only' placeholder.
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.JunctionBox) { d.vendorOnly = false; d.devOnly = false; d.costs = CreateDefaultJunctionBox().costs; }
            consumablePriceVersion = 4;
            added = true;
        }
        if (consumablePriceVersion < 5 && devices != null)
        {
            // 2026-10-19: the Quantum Organizer reaches much further (9 -> 24): saved entries take the coded reach.
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.QuantumOrganizer && !d.attuned) { d.radius = Mathf.Max(d.radius, CreateDefaultQuantumOrganizer().radius); d.description = CreateDefaultQuantumOrganizer().description; }
            consumablePriceVersion = 5;
            added = true;
        }
        if (consumablePriceVersion < 6 && devices != null)
        {
            // 2026-10-20 (merge of two sessions): kinds 29-34 were numbered differently on two branches. A saved entry whose name is not its kind's
            // coded name is dropped; the right defaults are added again below.
            devices = Array.FindAll(devices, d => d == null || d.attuned || (int)d.kind < 29 || d.displayName == DefaultNameOf(d.kind));
            consumablePriceVersion = 6;
            added = true;
        }
        if (consumablePriceVersion < 7 && devices != null)
        {
            // 2026-10-20: the Coupling leaves the shop (couplings fit themselves when a belt / hose attaches): saved entries take the coded flag.
            foreach (Device d in devices) if (d != null && !d.attuned && d.kind == DeviceKind.Coupling) d.devOnly = true;
            consumablePriceVersion = 7;
            added = true;
        }
        return added;
    }

    // EnsureDefaultPotions step 4: the Wood Chipper's shop skill gate.
    private void FixWoodChipperSkillGate()
    {
        if (devices != null)
            foreach (Device d in devices)
                if (d != null && d.kind == DeviceKind.WoodChipper && string.IsNullOrEmpty(d.shopSkillId)) { d.shopSkillId = "woodcutting"; d.shopSkillLevel = 2; }   // saved scenes: the chipper opens at Woodcutting 2
    }

    // EnsureDefaultPotions step 5: Magic Beans, the Dragon seed and one seed per growable pixel type.
    private bool AddMissingSeeds()
    {
        bool added = false;

        if (addDefaultDevices && devices != null && !Array.Exists(devices, d => d != null && d.beans))
        {
            Array.Resize(ref devices, devices.Length + 1);
            devices[devices.Length - 1] = CreateDefaultMagicBeans();
            added = true;
        }

        if (addDefaultDevices && devices != null && !Array.Exists(devices, d => d != null && d.kind == DeviceKind.Seed && !d.beans && PixelClicker.IsDragonCube(d.seedType)))
        {
            Array.Resize(ref devices, devices.Length + 1);
            devices[devices.Length - 1] = CreateDefaultDragonSeed();
            added = true;
        }

        if (addDefaultDevices && devices != null)
        {
            // One seed per pixel type that can be grown (appended at the end so placed-device indexes stay valid).
            foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
            {
                if (!HasSeed(type)) continue;
                if (Array.Exists(devices, d => d != null && d.kind == DeviceKind.Seed && !d.beans && d.seedType == type)) continue;
                Array.Resize(ref devices, devices.Length + 1);
                devices[devices.Length - 1] = CreateDefaultSeed(type);
                added = true;
            }
        }
        return added;
    }

    // EnsureDefaultPotions step 6: one potion per pixel type (not the rare cubes).
    private bool AddMissingDefaultPotions()
    {
        bool added = false;
        if (!addDefaultPotions) return false;
        if (potions == null) potions = new Potion[0];

        foreach (PixelClicker.PixelType type in Enum.GetValues(typeof(PixelClicker.PixelType)))
        {
            if (PixelClicker.IsRareCube(type)) continue;
            if (Array.Exists(potions, p => p != null && p.type == type)) continue;
            Array.Resize(ref potions, potions.Length + 1);
            potions[potions.Length - 1] = CreateDefaultPotion(type);
            added = true;
        }
        return added;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        // Delayed: serialized data must not be changed from inside OnValidate itself.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (EnsureDefaultPotions()) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif
}
