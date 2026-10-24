// ---- System notes ----
// PixelLooksDefaults.cs
//   Part of PixelLooks (partial class): the built-in looks table (CreateDefaults and its ore / Prime Cube / Dragon Cube helpers) and Find. The system notes are in PixelLooks.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static partial class PixelLooks
{
    private static PixelLook OreLook(PixelClicker.PixelType type, Color metal)
        => new PixelLook { type = type, useColor = true, color = Color.white, metallic = 0.2f, smoothness = 0.3f, damageCracks = true, oreTexture = true, oreColor = metal, jagged = true };

    /// <summary>The built-in styles (used when the list on PixelClicker is new).</summary>
    public static PixelLook[] CreateDefaults()
    {
        // (one table, built in four pieces in this order so each piece stays short)
        List<PixelLook> all = new List<PixelLook>();
        all.AddRange(DefaultsBasicAndSpecial());
        all.AddRange(DefaultsOresAndFishing());
        all.AddRange(DefaultsTreasureToFarming());
        all.AddRange(DefaultsElectricAndRares());
        return all.ToArray();
    }

    /// <summary>Built-in looks, part of CreateDefaults: the basic pixels and the special blocks (Vacuum ... Solar).</summary>
    private static PixelLook[] DefaultsBasicAndSpecial()
    {
        return new[]
        {
            // The basic pixels: a subtle drawn surface and a thin bevelled rim (a slightly lighter edge line) so they read as solid objects.
            new PixelLook { type = PixelClicker.PixelType.White, metallic = 0f, smoothness = 0.35f, basicSurface = PixelLook.BasicSurface.Grain,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.62f, 0.64f, 0.68f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = -0.3f, outlineStrength = 0.55f },
            new PixelLook { type = PixelClicker.PixelType.Gray, metallic = 0.65f, smoothness = 0.55f, basicSurface = PixelLook.BasicSurface.Brushed, },
            new PixelLook { type = PixelClicker.PixelType.Black, metallic = 0.15f, smoothness = 0.85f, useColor = true, color = new Color(0.1f, 0.1f, 0.12f, 1f), basicSurface = PixelLook.BasicSurface.Sheen, },

            // Red, green and blue: a faint pixel-art mosaic and a lighter rim in their own colour; with the colour-blind setting on they also get a shape on every face.
            new PixelLook { type = PixelClicker.PixelType.Red, metallic = 0.1f, smoothness = 0.6f, emission = 0f, colorBlindSides = 3, colorBlindRotation = 90f, basicSurface = PixelLook.BasicSurface.Mosaic, },
            new PixelLook { type = PixelClicker.PixelType.Green, metallic = 0.1f, smoothness = 0.6f, emission = 0f, colorBlindSides = 4, colorBlindRotation = 45f, basicSurface = PixelLook.BasicSurface.Mosaic, },
            new PixelLook { type = PixelClicker.PixelType.Blue, metallic = 0.1f, smoothness = 0.6f, emission = 0f, colorBlindSides = 32, basicSurface = PixelLook.BasicSurface.Mosaic, },

            // Vacuum: a clear lavender glass container with one round opening and a suction wind curling into it.
            new PixelLook { type = PixelClicker.PixelType.Vacuum, useColor = true, color = new Color(0.62f, 0.5f, 0.95f, 0.2f),
                            forceTranslucent = true, smoothness = 0.97f, metallic = 0f, emission = 0.12f, vacuumContainer = true, windFx = true,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.5f, 0.27f, 0.92f, 1f), outlineBevel = true, outlineThickness = 0.055f, outlineShade = 0f, outlineStrength = 1f },   // thick SOLID violet edges: never mistaken for the plain glass pixel

            // Glass: very see-through, a faint edge so it can still be seen, shatters on the ground.
            new PixelLook { type = PixelClicker.PixelType.Glass, alpha = 0.4f, smoothness = 1f, metallic = 0f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.85f, 0.97f, 1f, 1f),
                            outlineThickness = 0.02f, outlineStrength = 0.5f, shatter = true },

            // Ghost: a gray, translucent, gelatinous blob with a darker nucleus and tiny bubbles, wobbling like jelly. Bounces slowly and floats off the screen.
            new PixelLook { type = PixelClicker.PixelType.Ghost, useColor = true, color = new Color(0.62f, 0.66f, 0.71f, 0.4f),
                            forceTranslucent = true, smoothness = 0.92f, metallic = 0f, emission = 0.22f, blob = true, gelFx = true,
                            wobble = true, wobbleAmount = 0.2f, wobbleSpeed = 2.8f, customPhysics = true, ignoreViewBounds = true, floatAway = true },

            // Obsidian: sheer polished black metal with white streaks; cracks spread with every click.
            new PixelLook { type = PixelClicker.PixelType.Obsidian, useColor = true, color = Color.white,
                            metallic = 1f, smoothness = 0.97f, streakTexture = true, damageCracks = true, shine = true, obsidianFx = true, jagged = true },

            // Electric: a faint dark-blue glass box held together by crackling lightning.
            new PixelLook { type = PixelClicker.PixelType.Electric, useColor = true, color = new Color(0.04f, 0.12f, 0.28f, 0.22f),
                            forceTranslucent = true, smoothness = 0.9f, metallic = 0f, emission = 0.6f, lightning = true },

            // Seed: a brown shell that cracks as you click it; a sprout comes out and grows a pixel.
            // A glossy, ridged seed pod (a rounded block): cracks spread over it as you dig and a green light leaks out of them.
            new PixelLook { type = PixelClicker.PixelType.Seed, useColor = true, color = Color.white,
                            metallic = 0.05f, smoothness = 0.6f, damageCracks = true, blockSurface = PixelLook.BlockSurface.Seed, blob = true,
                            farmFx = PixelLook.FarmAmbient.Sparkle },

            // Fire (testing pixel): a glowing orange cube with flames rising off it; the old pixel burns away.
            new PixelLook { type = PixelClicker.PixelType.Fire, useColor = true, color = new Color(1f, 0.42f, 0.08f, 1f),
                            metallic = 0f, smoothness = 0.15f, emission = 1.2f, flames = true, burn = true },

            // Nuclear (testing pixel): a glowing toxic-green cube with radioactive motes; the old pixel makes the pixels round it despawn almost at once.
            new PixelLook { type = PixelClicker.PixelType.Nuclear, useColor = true, color = new Color(0.5f, 1f, 0.2f, 1f),
                            metallic = 0f, smoothness = 0.3f, emission = 1.5f, radiation = true },

            // Meteor: a dark cratered rock whose lava cracks glow brighter with every hit; embers rise off it and the old pixel streaks away trailing fire.
            new PixelLook { type = PixelClicker.PixelType.Meteor, useColor = true, color = Color.white,
                            metallic = 0f, smoothness = 0.2f, damageCracks = true, meteorTexture = true, cometTail = true, jagged = true },

            // Solar: a clean golden sun-cube with a turning corona; the old pixel hovers and pays a trickle.
            new PixelLook { type = PixelClicker.PixelType.Solar, useColor = true, color = Color.white,
                            metallic = 0f, smoothness = 0.3f, emission = 0.9f, sunTexture = true, sunFx = true,
                            customPhysics = true, bounce = 0.3f, friction = 0.6f, gravityMultiplier = 0f },
        };
    }

    /// <summary>Built-in looks, part of CreateDefaults: the Prospector ores, the Fishing blocks and Bait.</summary>
    private static PixelLook[] DefaultsOresAndFishing()
    {
        return new[]
        {
            // The Prospector ores: rock cubes veined and flecked with metal; they crack as you hit them.
            OreLook(PixelClicker.PixelType.Copper, new Color(0.86f, 0.46f, 0.26f, 1f)),
            OreLook(PixelClicker.PixelType.Tin, new Color(0.75f, 0.8f, 0.84f, 1f)),
            OreLook(PixelClicker.PixelType.Iron, new Color(0.62f, 0.36f, 0.3f, 1f)),
            OreLook(PixelClicker.PixelType.Lead, new Color(0.46f, 0.52f, 0.66f, 1f)),
            OreLook(PixelClicker.PixelType.Zinc, new Color(0.7f, 0.88f, 0.95f, 1f)),
            OreLook(PixelClicker.PixelType.Nickel, new Color(0.82f, 0.76f, 0.58f, 1f)),
            OreLook(PixelClicker.PixelType.Silver, new Color(0.88f, 0.9f, 0.95f, 1f)),
            OreLook(PixelClicker.PixelType.Gold, new Color(1f, 0.82f, 0.25f, 1f)),
            OreLook(PixelClicker.PixelType.Bedrock, new Color(0.32f, 0.33f, 0.4f, 1f)),

            // The Fishing blocks: Fish (a silvery wobbling blob), Seaweed (green see-through jelly), Shell (pink and glossy), Pearl (rare, white, shining),
            // Driftwood (the grey filler) and the Jellyfish (a glowing translucent hazard).
            new PixelLook { type = PixelClicker.PixelType.Fish, useColor = true, color = new Color(0.45f, 0.65f, 0.85f, 1f), metallic = 0.5f, smoothness = 0.85f, basicSurface = PixelLook.BasicSurface.Sheen,
                            blob = true, wobble = true, wobbleAmount = 0.09f, farmFx = PixelLook.FarmAmbient.Dew },
            new PixelLook { type = PixelClicker.PixelType.Seaweed, useColor = true, color = new Color(0.2f, 0.6f, 0.4f, 0.75f), metallic = 0f, smoothness = 0.7f, forceTranslucent = true,
                            wobble = true, wobbleAmount = 0.1f, customPhysics = true, bounce = 0.2f, friction = 0.8f, gravityMultiplier = 0.4f, farmFx = PixelLook.FarmAmbient.Dew },
            new PixelLook { type = PixelClicker.PixelType.Shell, useColor = true, color = new Color(0.95f, 0.7f, 0.75f, 1f), metallic = 0.1f, smoothness = 0.9f, basicSurface = PixelLook.BasicSurface.Sheen,
                            blob = true, shine = true, farmFx = PixelLook.FarmAmbient.Sparkle },
            new PixelLook { type = PixelClicker.PixelType.Pearl, useColor = true, color = new Color(0.97f, 0.97f, 1f, 1f), metallic = 0.2f, smoothness = 1f, emission = 0.2f,
                            blob = true, shine = true, farmFx = PixelLook.FarmAmbient.Sparkle },
            new PixelLook { type = PixelClicker.PixelType.Driftwood, useColor = true, color = new Color(0.62f, 0.58f, 0.5f, 1f), metallic = 0f, smoothness = 0.1f, basicSurface = PixelLook.BasicSurface.Grain,
                            jagged = true, farmFx = PixelLook.FarmAmbient.Crumbs },
            new PixelLook { type = PixelClicker.PixelType.Jellyfish, useColor = true, color = new Color(0.9f, 0.45f, 0.8f, 0.6f), metallic = 0f, smoothness = 0.9f, emission = 0.5f, forceTranslucent = true,
                            blob = true, wobble = true, wobbleAmount = 0.14f, wobbleSpeed = 6f, farmFx = PixelLook.FarmAmbient.Spores,
                            lightning = true, lightningCoreColor = new Color(1f, 0.92f, 1f, 1f), lightningGlowColor = new Color(0.95f, 0.4f, 1f, 1f), lightningJitter = 0.1f },
            // Fishing's special blocks: a message in a bottle (cracks as you hit it) and a whale (a cube with a whale drawn on every face).
            // (the Bottle is a clear glass cube with a little 3D treasure map floating inside, PixelBottleMap.cs)
            new PixelLook { type = PixelClicker.PixelType.Bottle, useColor = true, color = new Color(0.55f, 0.9f, 0.8f, 0.3f), forceTranslucent = true, metallic = 0f, smoothness = 0.97f, emission = 0.04f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.8f, 1f, 0.95f, 1f), outlineBevel = true, outlineThickness = 0.035f, outlineStrength = 0.85f,
                            bottleMap = true, wobble = true, wobbleAmount = 0.03f, wobbleSpeed = 2.2f, farmFx = PixelLook.FarmAmbient.Sparkle },
            new PixelLook { type = PixelClicker.PixelType.Whale, useColor = true, color = Color.white, metallic = 0.05f, smoothness = 0.6f,
                            blockSurface = PixelLook.BlockSurface.Whale, blob = true, wobble = true, wobbleAmount = 0.07f, wobbleSpeed = 3.2f, farmFx = PixelLook.FarmAmbient.Dew },
            // Bait: small brown-orange pellets pressed into a lump.
            new PixelLook { type = PixelClicker.PixelType.Bait, useColor = true, color = new Color(0.8f, 0.55f, 0.28f, 1f), metallic = 0f, smoothness = 0.3f, basicSurface = PixelLook.BasicSurface.Grain,
                            blob = true, farmFx = PixelLook.FarmAmbient.Crumbs },
        };
    }

    /// <summary>Built-in looks, part of CreateDefaults: Treasure, the hazards, Wood / Rock, Woodcutting, Foraging and Farming.</summary>
    private static PixelLook[] DefaultsTreasureToFarming()
    {
        return new[]
        {
            // Treasure: a wooden chest with gold bands and a lock; light leaks from the lid as you hit it.
            new PixelLook { type = PixelClicker.PixelType.Treasure, useColor = true, color = Color.white, metallic = 0.25f, smoothness = 0.4f,
                            damageCracks = true, chestTexture = true },

            // Hazard blocks: the Pest (a dark, buzzing lump that cracks as you hit it) and the Gas Pocket (a wobbling yellow-green bubble of gas).
            // The Pest: a spiky black carapace in hazard stripes with glowing red eyes that multiply as you hit it; it twitches and bugs crawl over it.
            new PixelLook { type = PixelClicker.PixelType.Pest, useColor = true, color = Color.white, metallic = 0.35f, smoothness = 0.75f, damageCracks = true,
                            blockSurface = PixelLook.BlockSurface.Pest, jagged = true, wobble = true, wobbleAmount = 0.07f, wobbleSpeed = 9f,
                            farmFx = PixelLook.FarmAmbient.Bugs },
            new PixelLook { type = PixelClicker.PixelType.GasPocket, useColor = true, color = new Color(0.78f, 0.85f, 0.25f, 0.55f), forceTranslucent = true, metallic = 0f, smoothness = 0.8f, emission = 0.7f,
                            wobble = true, wobbleAmount = 0.1f, farmFx = PixelLook.FarmAmbient.Spores },

            // Wood and Rock: plain basic blocks that fit any rotation.
            new PixelLook { type = PixelClicker.PixelType.Wood, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.1f, blockSurface = PixelLook.BlockSurface.Wood, blob = true },
            new PixelLook { type = PixelClicker.PixelType.Rock, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.1f, blockSurface = PixelLook.BlockSurface.Stone, jagged = true },

            // The Woodcutting blocks: Bark (rough dark wood), Sap (sticky amber blob), Leaf (floaty green flake), Heartwood (rare glowing red-brown core),
            // Twig (the pale filler) and the Hornet Nest (a papery, twitching hazard).
            new PixelLook { type = PixelClicker.PixelType.Bark, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(0.62f, 0.5f, 0.42f, 1f), metallic = 0f, smoothness = 0.05f,
                            blockSurface = PixelLook.BlockSurface.Wood, jagged = true, farmFx = PixelLook.FarmAmbient.Crumbs },
            new PixelLook { type = PixelClicker.PixelType.Sap, useColor = true, color = new Color(1f, 0.72f, 0.2f, 0.75f), forceTranslucent = true, metallic = 0f, smoothness = 0.95f, emission = 0.25f,
                            customPhysics = true, bounce = 0f, friction = 1.4f, gravityMultiplier = 1f, wobble = true, wobbleAmount = 0.08f, farmFx = PixelLook.FarmAmbient.Sap },
            new PixelLook { type = PixelClicker.PixelType.Leaf, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.05f, blockSurface = PixelLook.BlockSurface.LeafBlock,
                            customPhysics = true, bounce = 0.3f, friction = 0.6f, gravityMultiplier = 0.25f, farmFx = PixelLook.FarmAmbient.Leaves, flutter = true },
            new PixelLook { type = PixelClicker.PixelType.Heartwood, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(1f, 0.62f, 0.5f, 1f), metallic = 0.15f, smoothness = 0.55f, emission = 0.35f, shine = true,
                            blockSurface = PixelLook.BlockSurface.Wood, blob = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = -0.4f, outlineStrength = 0.8f,
                            farmFx = PixelLook.FarmAmbient.Sparkle },
            new PixelLook { type = PixelClicker.PixelType.Twig, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.05f, blockSurface = PixelLook.BlockSurface.Twigs, jagged = true },
            new PixelLook { type = PixelClicker.PixelType.Pinecone, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.08f, blockSurface = PixelLook.BlockSurface.Pinecone, jagged = true },
            new PixelLook { type = PixelClicker.PixelType.Moss, customPhysics = true, bounce = 0.02f, friction = 1.2f, gravityMultiplier = 1f, useColor = true, color = Color.white, metallic = 0f, smoothness = 0f, blockSurface = PixelLook.BlockSurface.Moss, blob = true, farmFx = PixelLook.FarmAmbient.Spores },
            // The Termite Hive (enum HornetNest): a mud mound with tunnel holes; skittering termites.
            new PixelLook { type = PixelClicker.PixelType.HornetNest, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.1f, blockSurface = PixelLook.BlockSurface.Hive,
                            jagged = true, wobble = true, wobbleAmount = 0.04f, wobbleSpeed = 8f, farmFx = PixelLook.FarmAmbient.Termites },

            // The Foraging blocks: Berry (a juicy wobbling blob), Mushroom (a red-brown cap), Herb (leafy and light), Truffle (a rare dark lump with a shine),
            // Weeds (the pale filler) and the Toadstool (a purple, spore-puffing hazard).
            new PixelLook { type = PixelClicker.PixelType.Berry, useColor = true, color = new Color(0.75f, 0.15f, 0.35f, 1f), metallic = 0f, smoothness = 0.9f, emission = 0.15f,
                            blob = true, wobble = true, wobbleAmount = 0.08f, farmFx = PixelLook.FarmAmbient.Dew },
            new PixelLook { type = PixelClicker.PixelType.Mushroom, useColor = true, color = new Color(0.82f, 0.4f, 0.3f, 1f), metallic = 0f, smoothness = 0.4f, basicSurface = PixelLook.BasicSurface.Mosaic,
                            blob = true, farmFx = PixelLook.FarmAmbient.Spores },
            new PixelLook { type = PixelClicker.PixelType.Herb, useColor = true, color = new Color(0.3f, 0.7f, 0.35f, 1f), metallic = 0f, smoothness = 0.25f, basicSurface = PixelLook.BasicSurface.Grain,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = -0.35f, outlineStrength = 0.7f,
                            customPhysics = true, bounce = 0.3f, friction = 0.6f, gravityMultiplier = 0.5f, wobble = true, wobbleAmount = 0.05f, farmFx = PixelLook.FarmAmbient.Leaves },
            // (Truffle = a near-black, glossy lump with a gold sparkle; Mold = a pale, fuzzy, faintly glowing blue-green: never confused - looksVersion 62.)
            new PixelLook { type = PixelClicker.PixelType.Truffle, useColor = true, color = new Color(0.16f, 0.1f, 0.07f, 1f), metallic = 0.35f, smoothness = 0.82f, emission = 0.04f, shine = true,
                            jagged = true, farmFx = PixelLook.FarmAmbient.Sparkle },
            new PixelLook { type = PixelClicker.PixelType.Weeds, useColor = true, color = new Color(0.55f, 0.62f, 0.35f, 1f), metallic = 0f, smoothness = 0.05f, basicSurface = PixelLook.BasicSurface.Grain },
            new PixelLook { type = PixelClicker.PixelType.Acorn, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(0.68f, 0.46f, 0.22f, 1f), metallic = 0f, smoothness = 0.5f, basicSurface = PixelLook.BasicSurface.Grain, blob = true },
            new PixelLook { type = PixelClicker.PixelType.Charcoal, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(0.16f, 0.16f, 0.18f, 1f), metallic = 0.05f, smoothness = 0.25f, basicSurface = PixelLook.BasicSurface.Grain, jagged = true },
            new PixelLook { type = PixelClicker.PixelType.Pesticide, useColor = true, color = new Color(0.72f, 0.88f, 0.22f, 1f), metallic = 0.1f, smoothness = 0.55f, emission = 0.12f, basicSurface = PixelLook.BasicSurface.Mosaic, shine = true },
            new PixelLook { type = PixelClicker.PixelType.Whetstone, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(0.62f, 0.68f, 0.76f, 1f), metallic = 0.25f, smoothness = 0.4f, blockSurface = PixelLook.BlockSurface.Stone, shine = true },
            new PixelLook { type = PixelClicker.PixelType.GreatOak, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(0.42f, 0.3f, 0.18f, 1f), metallic = 0f, smoothness = 0.08f, blockSurface = PixelLook.BlockSurface.Wood,
                            jagged = true, farmFx = PixelLook.FarmAmbient.Leaves },
            new PixelLook { type = PixelClicker.PixelType.WoodChips, customPhysics = true, bounce = 0.05f, friction = 0.9f, gravityMultiplier = 1f, useColor = true, color = new Color(0.82f, 0.62f, 0.36f, 1f), metallic = 0f, smoothness = 0.05f, basicSurface = PixelLook.BasicSurface.Grain, jagged = true },
            new PixelLook { type = PixelClicker.PixelType.Mold, useColor = true, color = new Color(0.62f, 0.92f, 0.78f, 1f), metallic = 0f, smoothness = 0.02f, emission = 0.28f, basicSurface = PixelLook.BasicSurface.Grain,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.05f, outlineShadeOfTier = true, outlineShade = 0.35f, outlineStrength = 0.6f,
                            blob = true, wobble = true, wobbleAmount = 0.06f, wobbleSpeed = 2.5f, farmFx = PixelLook.FarmAmbient.Spores },
            new PixelLook { type = PixelClicker.PixelType.Toadstool, useColor = true, color = new Color(0.62f, 0.25f, 0.65f, 1f), metallic = 0f, smoothness = 0.35f, emission = 0.3f, basicSurface = PixelLook.BasicSurface.Mosaic,
                            blob = true, wobble = true, wobbleAmount = 0.07f, wobbleSpeed = 7f, farmFx = PixelLook.FarmAmbient.Spores },

            // The farming blocks: each has a bevelled rim in a shade of its own colour and an ambient particle effect.
            new PixelLook { type = PixelClicker.PixelType.Dirt, useColor = true, color = Color.white, metallic = 0f, smoothness = 0.05f, blockSurface = PixelLook.BlockSurface.Dirt,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = -0.45f, outlineStrength = 0.8f,
                            farmFx = PixelLook.FarmAmbient.Crumbs },
            new PixelLook { type = PixelClicker.PixelType.Fertilizer, useColor = true, color = new Color(0.5f, 0.66f, 0.25f, 1f), metallic = 0f, smoothness = 0.2f, emission = 0.2f, basicSurface = PixelLook.BasicSurface.Grain,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = 0.35f, outlineStrength = 0.7f,
                            farmFx = PixelLook.FarmAmbient.Spores },
            new PixelLook { type = PixelClicker.PixelType.Pollen, useColor = true, color = new Color(1f, 0.9f, 0.3f, 0.8f), forceTranslucent = true, metallic = 0f, smoothness = 0.3f, emission = 0.6f,
                            customPhysics = true, bounce = 0.6f, friction = 0.4f, gravityMultiplier = 0.3f, wobble = true, farmFx = PixelLook.FarmAmbient.Pollen },
            new PixelLook { type = PixelClicker.PixelType.Wheat, useColor = true, color = new Color(0.93f, 0.76f, 0.3f, 1f), metallic = 0.1f, smoothness = 0.4f, basicSurface = PixelLook.BasicSurface.Brushed,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = -0.35f, outlineStrength = 0.75f,
                            farmFx = PixelLook.FarmAmbient.Chaff },
            new PixelLook { type = PixelClicker.PixelType.Weevil, useColor = true, color = new Color(0.22f, 0.15f, 0.11f, 1f), metallic = 0.1f, smoothness = 0.6f, basicSurface = PixelLook.BasicSurface.Mosaic,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = 0.5f, outlineStrength = 0.7f,
                            farmFx = PixelLook.FarmAmbient.Bugs },
            new PixelLook { type = PixelClicker.PixelType.GoldenFruit, useColor = true, color = new Color(1f, 0.82f, 0.22f, 1f), metallic = 0.9f, smoothness = 0.9f, emission = 0.9f, shine = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.045f, outlineShadeOfTier = true, outlineShade = 0.5f, outlineStrength = 0.9f,
                            farmFx = PixelLook.FarmAmbient.Sparkle },
        };
    }

    /// <summary>Built-in looks, part of CreateDefaults: Electrician, Water, Mirror and the rare cubes (Dragon, God, Prime, Singularity, Snitch).</summary>
    private static PixelLook[] DefaultsElectricAndRares()
    {
        return new[]
        {
            // Electrician pixels: copper Wire, a green Battery and a blue Capacitor.
            new PixelLook { type = PixelClicker.PixelType.Wire, electricFx = PixelLook.ElectricAmbient.Wire, useColor = true, color = new Color(0.86f, 0.5f, 0.26f, 1f), metallic = 1f, smoothness = 0.65f, basicSurface = PixelLook.BasicSurface.Brushed,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.035f, outlineShadeOfTier = true, outlineShade = 0.4f, outlineStrength = 0.8f },
            new PixelLook { type = PixelClicker.PixelType.Battery, electricFx = PixelLook.ElectricAmbient.Battery, useColor = true, color = new Color(0.5f, 0.9f, 0.3f, 1f), metallic = 0.3f, smoothness = 0.75f, emission = 0.35f, shine = true,
                            damageCracks = false,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = -0.5f, outlineStrength = 0.85f },
            new PixelLook { type = PixelClicker.PixelType.Capacitor, electricFx = PixelLook.ElectricAmbient.Capacitor, useColor = true, color = new Color(0.3f, 0.55f, 0.95f, 1f), metallic = 0.8f, smoothness = 0.85f, emission = 0.3f, shine = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = 0.5f, outlineStrength = 0.8f },

            new PixelLook { type = PixelClicker.PixelType.Resistor, electricFx = PixelLook.ElectricAmbient.Resistor, useColor = true, color = new Color(0.82f, 0.68f, 0.45f, 1f), metallic = 0.1f, smoothness = 0.4f, basicSurface = PixelLook.BasicSurface.Grain,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.035f, outlineShadeOfTier = true, outlineShade = -0.45f, outlineStrength = 0.8f },
            new PixelLook { type = PixelClicker.PixelType.Transistor, electricFx = PixelLook.ElectricAmbient.Transistor, useColor = true, color = new Color(0.18f, 0.2f, 0.26f, 1f), metallic = 0.85f, smoothness = 0.8f, emission = 0.15f, shine = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = 0.55f, outlineStrength = 0.85f },
            new PixelLook { type = PixelClicker.PixelType.Fuse, electricFx = PixelLook.ElectricAmbient.Fuse, useColor = true, color = new Color(0.95f, 0.45f, 0.2f, 1f), metallic = 0.2f, smoothness = 0.5f, emission = 0.5f,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = -0.4f, outlineStrength = 0.8f },
            new PixelLook { type = PixelClicker.PixelType.Bulb, electricFx = PixelLook.ElectricAmbient.Bulb, useColor = true, color = new Color(1f, 0.95f, 0.6f, 0.9f), forceTranslucent = true, metallic = 0f, smoothness = 0.95f, emission = 1.2f, shine = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.04f, outlineShadeOfTier = true, outlineShade = 0.4f, outlineStrength = 0.7f },

            // Water: a see-through blue jelly cube that wobbles.
            new PixelLook { type = PixelClicker.PixelType.Water, useColor = true, color = new Color(0.25f, 0.6f, 1f, 0.5f),
                            forceTranslucent = true, smoothness = 1f, metallic = 0f, emission = 0.25f, wobble = true, splash = true, farmFx = PixelLook.FarmAmbient.Dew },

            // Mirror: polished chrome. The reflection is PAINTED into the texture (chromeTexture) and the glint is the Shine overlay, so it does not lean on the scene's
            // reflection probe (a build has no auto-updated one: a high metallic value turned the cube dark there) or on emission (stripped from builds, and it only
            // made the build-proof glow shell wash over the chrome).
            new PixelLook { type = PixelClicker.PixelType.Mirror, useColor = true, color = Color.white,
                            metallic = 0.15f, smoothness = 0.8f, emission = 0f, chromeTexture = true, shine = true },

            // Dragon Cubes 1-7: glassy orange cubes with 1-7 dark orange small cubes inside, like dragon balls.
            DragonCubeLook(PixelClicker.PixelType.DragonCube1, 1),
            DragonCubeLook(PixelClicker.PixelType.DragonCube2, 2),
            DragonCubeLook(PixelClicker.PixelType.DragonCube3, 3),
            DragonCubeLook(PixelClicker.PixelType.DragonCube4, 4),
            DragonCubeLook(PixelClicker.PixelType.DragonCube5, 5),
            DragonCubeLook(PixelClicker.PixelType.DragonCube6, 6),
            DragonCubeLook(PixelClicker.PixelType.DragonCube7, 7),

            // The three Egyptian God Cubes. Slifer: a red glass cube held together by storm lightning. Ra: polished gold with a sun's corona. Obelisk: deep blue stone that sheds dust.
            new PixelLook { type = PixelClicker.PixelType.GodSlifer, useColor = true, color = new Color(0.55f, 0.03f, 0.03f, 0.4f),
                            forceTranslucent = true, smoothness = 0.95f, metallic = 0f, emission = 0.8f, lightning = true,
                            lightningCoreColor = new Color(1f, 0.92f, 0.55f, 1f), lightningGlowColor = new Color(1f, 0.15f, 0.08f, 1f), lightningJitter = 0.12f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(1f, 0.78f, 0.25f, 1f), outlineBevel = true, outlineThickness = 0.03f, outlineStrength = 0.9f },
            new PixelLook { type = PixelClicker.PixelType.GodRa, useColor = true, color = new Color(1f, 0.78f, 0.18f, 1f),
                            metallic = 0.85f, smoothness = 0.9f, emission = 1.1f, sunFx = true, shine = true,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(1f, 0.97f, 0.8f, 1f), outlineBevel = true, outlineThickness = 0.03f, outlineStrength = 0.9f },
            new PixelLook { type = PixelClicker.PixelType.GodObelisk, useColor = true, color = new Color(0.1f, 0.2f, 0.62f, 1f),
                            metallic = 0.25f, smoothness = 0.5f, emission = 0.4f, shine = true, farmFx = PixelLook.FarmAmbient.Crumbs,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.45f, 0.85f, 1f, 1f), outlineBevel = true, outlineThickness = 0.035f, outlineStrength = 0.85f },

            // The six Prime Cubes (glassy gems with a small cube inside) and the Prime Cubelette.
            StoneLook(PixelClicker.PixelType.StoneSpace, 0),
            StoneLook(PixelClicker.PixelType.StoneMind, 1),
            StoneLook(PixelClicker.PixelType.StoneReality, 2),
            StoneLook(PixelClicker.PixelType.StonePower, 3),
            StoneLook(PixelClicker.PixelType.StoneTime, 4),
            StoneLook(PixelClicker.PixelType.StoneSoul, 5),
            new PixelLook { type = PixelClicker.PixelType.InfinityCubelette, useColor = true, color = new Color(1f, 0.9f, 0.5f, 0.55f),
                            forceTranslucent = true, smoothness = 1f, metallic = 0f, emission = 1f, shine = true, starCubes = 6, starColor = new Color(1f, 0.75f, 0.15f, 1f),
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(1f, 0.95f, 0.75f, 1f), outlineBevel = true, outlineThickness = 0.035f, outlineStrength = 1f },

            // Singularity: a box of dark matter.
            new PixelLook { type = PixelClicker.PixelType.Singularity, useColor = true, color = new Color(0.07f, 0.02f, 0.14f, 0.5f),
                            forceTranslucent = true, smoothness = 0.95f, metallic = 0f, glowScale = 0.3f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.65f, 0.25f, 1f, 1f),
                            outlineThickness = 0.04f, darkMatter = true, gravityWell = true },

            // Golden Snitch: a polished gold cube on the clicker cube until you click it - then it sprouts wings and flies (PixelGoldenSnitch).
            new PixelLook { type = PixelClicker.PixelType.GoldenSnitch, useColor = true, color = new Color(1f, 0.78f, 0.22f, 1f),
                            metallic = 0.92f, smoothness = 0.94f, emission = 0.35f, shine = true },
        };
    }

    /// <summary>An Prime Cube: a glassy cube in the stone's colour with one small bright cube inside, plus a signature effect (Space lightning, Reality flames, Power lightning, Soul mist).</summary>
    private static PixelLook StoneLook(PixelClicker.PixelType type, int stone)
    {
        Color c = PixelInfinity.StoneColor(stone);
        PixelLook look = new PixelLook
        {
            type = type, useColor = true, color = new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, 0.55f),
            forceTranslucent = true, smoothness = 1f, metallic = 0f, emission = 0.9f, shine = true,
            starCubes = 1, starColor = Color.Lerp(c, Color.white, 0.55f),
            outline = true, outlineUsesTierColor = false, outlineColor = Color.Lerp(c, Color.white, 0.5f), outlineBevel = true, outlineThickness = 0.035f, outlineStrength = 0.9f,
        };
        if (stone == 0 || stone == 3)
        {
            look.lightning = true; look.lightningCoreColor = Color.Lerp(c, Color.white, 0.8f); look.lightningGlowColor = c; look.lightningJitter = 0.1f;
        }
        if (stone == 2) look.flames = true;
        if (stone == 5) look.gelFx = true;
        return look;
    }

    private static PixelLook DragonCubeLook(PixelClicker.PixelType type, int stars)
    {
        return new PixelLook
        {
            type = type, useColor = true, color = new Color(1f, 0.58f, 0.1f, 0.5f),
            forceTranslucent = true, smoothness = 1f, metallic = 0f, emission = 0.45f,
            outline = true, outlineUsesTierColor = false, outlineColor = new Color(1f, 0.82f, 0.45f, 1f),
            outlineThickness = 0.025f, outlineStrength = 0.7f,
            starCubes = stars, starColor = new Color(0.55f, 0.18f, 0.02f, 1f),
        };
    }

    public static PixelLook Find(PixelLook[] looks, PixelClicker.PixelType type)
    {
        if (looks == null) return null;
        for (int i = 0; i < looks.Length; i++)
            if (looks[i] != null && looks[i].type == type) return looks[i];
        return null;
    }
}
