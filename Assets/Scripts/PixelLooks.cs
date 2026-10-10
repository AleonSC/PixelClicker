using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extra visual style for one pixel type, applied on top of the tier's own settings (colour, glow, translucent...).
/// The tier's colour is still what the UI shows; this only changes how the 3D pixel looks. Edit the list on PixelClicker.
/// </summary>
[Serializable]
public class PixelLook
{
    [Tooltip("The pixel type this style belongs to.")]
    public PixelClicker.PixelType type = PixelClicker.PixelType.White;

    [Header("Colour and surface")]
    [Tooltip("Draw the pixel in 'Color' instead of the tier's colour (the UI keeps using the tier colour).")]
    public bool useColor = false;

    [Tooltip("Colour of the 3D pixel (its alpha matters for see-through pixels).")]
    public Color color = Color.white;

    [Range(0f, 1f)]
    [Tooltip("Multiplies the colour's alpha. Lower = more see-through (only for translucent pixels).")]
    public float alpha = 1f;

    [Tooltip("Draw this pixel see-through even if its tier isn't marked Translucent.")]
    public bool forceTranslucent = false;

    [Range(-1f, 1f)]
    [Tooltip("Metallic surface (0-1). -1 = leave the material's own value.")]
    public float metallic = -1f;

    [Range(-1f, 1f)]
    [Tooltip("Glossiness (0-1). -1 = leave the material's own value.")]
    public float smoothness = -1f;

    [Min(0f)]
    [Tooltip("Plain self-lighting (emission) in the pixel's colour, even if the tier doesn't glow. 0 = none.")]
    public float emission = 0f;

    [Min(0f)]
    [Tooltip("Multiplies the glow of a glowing tier (1 = unchanged, 0.3 = much dimmer).")]
    public float glowScale = 1f;

    /// <summary>A gentle built-in surface for the basic pixels (the texture is near-white, so the colour shows through it).</summary>
    public enum BasicSurface { None = 0, Grain = 1, Brushed = 2, Sheen = 3, Mosaic = 4 }

    [Header("Surface texture")]
    [Tooltip("A subtle drawn surface: Grain (fine chalky speckle), Brushed (horizontal brushed-metal streaks), Sheen (soft diagonal highlights on dark pixels), Mosaic (a faint pixel-art mosaic with a lighter top edge on every face). Used by White, Gray, Black, Red, Green and Blue.")]
    public BasicSurface basicSurface = BasicSurface.None;

    [Tooltip("Draw thin white streaks on a dark base (use a white Color above so the texture shows true). Used by Obsidian.")]
    public bool streakTexture = false;

    [Tooltip("Tough pixels (several clicks to collect): white cracks spread over the pixel with every hit, more with each click.")]
    public bool damageCracks = false;

    [Header("Outline")]
    [Tooltip("Draw glowing neon lines along the 12 edges of the cube.")]
    public bool outline = false;

    [Tooltip("Use the pixel's tier colour for the outline (otherwise 'Outline Color').")]
    public bool outlineUsesTierColor = true;

    [Tooltip("Outline colour when 'Outline Uses Tier Colour' is off.")]
    public Color outlineColor = Color.white;

    [Tooltip("A bevel instead of neon: one thin SOLID line of exactly the outline colour lying flat along each edge (no glow, no bright core, no halo), so the cube looks like it has bevelled edges. Off = the glowing neon lines.")]
    public bool outlineBevel = false;

    [Tooltip("With 'Outline Shade': take the shade from the pixel's TIER colour even when the look draws the pixel in another colour (Obsidian and Seed are drawn white and get their colour from a texture).")]
    public bool outlineShadeOfTier = false;

    [Range(-1f, 1f)]
    [Tooltip("Draw the outline as a different SHADE of the pixel's own colour instead of 'Outline Color': above 0 lightens it towards white (0.4 = a lighter tint), below 0 darkens it towards black (-0.4 = a darker shade). 0 = off (use the colours above).")]
    public float outlineShade = 0f;

    [Range(0.005f, 0.3f)]
    [Tooltip("Line thickness as a fraction of the cube's width.")]
    public float outlineThickness = 0.045f;

    [Range(0f, 1f)]
    [Tooltip("Overall strength of the outline (0 = invisible, 1 = full).")]
    public float outlineStrength = 1f;

    [Header("Face circles")]
    [Tooltip("Draw a flat circle on each of the 6 faces of the cube.")]
    public bool faceCircles = false;

    [Tooltip("Colour of the face circles.")]
    public Color faceCircleColor = Color.black;

    [Range(0.05f, 0.5f)]
    [Tooltip("Circle radius as a fraction of the cube's width (0.5 = touches the edges).")]
    public float faceCircleRadius = 0.3f;

    [Header("Wobble (jelly)")]
    [Tooltip("The cube squashes and stretches like jelly (the live pixel and its falling copies).")]
    public bool wobble = false;

    [Range(0f, 0.5f)]
    [Tooltip("How much it squashes and stretches (0.15 = 15%).")]
    public float wobbleAmount = 0.14f;

    [Min(0f)]
    [Tooltip("How fast it wobbles.")]
    public float wobbleSpeed = 3.2f;

    [Header("Falling (old pixels of this type)")]
    [Tooltip("Use the bounce / friction / gravity below for this type's old pixels instead of the global ones.")]
    public bool customPhysics = false;

    [Range(0f, 1.5f)]
    [Tooltip("Bounciness of the old pixels (1 = keeps all its speed).")]
    public float bounce = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Friction of the old pixels (low = slides and keeps travelling).")]
    public float friction = 0.05f;

    [Min(0f)]
    [Tooltip("Multiplies the normal gravity on these old pixels (0.25 = falls four times more slowly).")]
    public float gravityMultiplier = 0.25f;

    [Tooltip("Old pixels of this type pass through the invisible screen edges instead of bouncing off them.")]
    public bool ignoreViewBounds = false;

    [Tooltip("After a few bounces the old pixel starts to float away and leaves the screen.")]
    public bool floatAway = false;

    [Min(1)]
    [Tooltip("How many ground bounces before it floats away.")]
    public int floatAfterBounces = 2;

    [Min(0f)]
    [Tooltip("How strongly it drifts upwards once it floats away (fraction of gravity).")]
    public float floatLift = 0.15f;

    [Min(0f)]
    [Tooltip("Sideways speed it picks up when it starts to float away.")]
    public float floatDriftSpeed = 2.5f;

    [Header("Colour-blind mark")]
    [Range(0, 32)]
    [Tooltip("Colour-blind support (Settings): draws a flat shape on every face - 3 = triangle, 4 = square, 32 = circle. 0 = none. Only shown while the colour-blind setting is on.")]
    public int colorBlindSides = 0;

    [Tooltip("Turns the colour-blind shape (degrees).")]
    public float colorBlindRotation = 0f;

    [Tooltip("Colour of the colour-blind shape.")]
    public Color colorBlindColor = new Color(1f, 1f, 1f, 0.95f);

    [Range(0.05f, 0.5f)]
    [Tooltip("Size of the colour-blind shape as a fraction of the cube's width.")]
    public float colorBlindRadius = 0.28f;

    [Header("Gravity well (old pixels of this type pull others in)")]
    [Tooltip("An old pixel of this type slowly pulls other old pixels towards it, inside a radius shown by a very faint ring.")]
    public bool gravityWell = false;

    [Min(0.5f)]
    [Tooltip("How far the pull reaches (world units).")]
    public float wellRadius = 3.5f;

    [Min(0f)]
    [Tooltip("How far (in pull radii) a streaking meteor pixel is bent towards the well. Inside the ring it is captured. 0 = 4.")]
    public float wellFlyReach = 4f;

    [Min(0f)]
    [Tooltip("Seconds the gravity-well pixel itself lasts before it vanishes (0 = 300, i.e. 5 minutes). Other old pixels inside its pull radius don't age while it is working.")]
    public float wellLifetime = 300f;

    [Min(0f)]
    [Tooltip("How fast nearby old pixels are dragged towards it (world units per second). The drag is constant inside the ring and overcomes the floor's friction.")]
    public float wellPullSpeed = 1.6f;

    [Min(0f)]
    [Tooltip("How quickly a pixel settles into that drag speed (per second). Higher = it grabs hold harder.")]
    public float wellGrip = 8f;

    [Min(0)]
    [Tooltip("The most old pixels one well pulls at a time (0 = 20). Other gravity-well pixels inside its radius count towards this, so wells placed close together use up each other's slots and have to be spaced out. A pixel is only ever held by one well.")]
    public int wellMaxPixels = 20;

    [Range(0f, 1f)]
    [Tooltip("How visible the ring is (0 = invisible). Keep it very faint.")]
    public float wellRingOpacity = 0.16f;

    [Tooltip("Colour of the ring.")]
    public Color wellRingColor = Color.black;

    [Header("Special")]
    [Tooltip("A swirling dark-matter core inside the cube (best with 'Force Translucent' and a dark, see-through colour).")]
    public bool darkMatter = false;

    [Tooltip("A cube made of lightning: jagged bolts crackle along all 12 edges and arc through the inside, re-rolled many times a second (best with a dark, very see-through colour and glow).")]
    public bool lightning = false;

    [Tooltip("Colour of the bright centre of each bolt.")]
    public Color lightningCoreColor = new Color(0.92f, 1f, 1f, 1f);

    [Tooltip("Colour of the softer glow around each bolt.")]
    public Color lightningGlowColor = new Color(0.25f, 0.7f, 1f, 1f);

    [Min(0f)]
    [Tooltip("How far the bolts wander from a straight line, as a fraction of the cube's size.")]
    public float lightningJitter = 0.09f;

    [Range(0, 7)]
    [Tooltip("Dragon Cubes: how many small cubes (1-7) float inside the cube, laid out like the stars on a dragon ball. 0 = none.")]
    public int starCubes = 0;

    [Tooltip("Colour of the small cubes inside a Dragon Cube.")]
    public Color starColor = new Color(0.55f, 0.18f, 0.02f, 1f);

    [Tooltip("Seed: a small green sprout (stem and two leaves) grows out of the top of the cube.")]
    public bool sprout = false;

    [Tooltip("Mirror: a chrome texture (sky, bright horizon band and dark ground, with soft streaks) so the cube looks reflective in any render pipeline.")]
    public bool chromeTexture = false;

    [Tooltip("Mirror: a bright diagonal glint sweeps across the faces in turn.")]
    public bool shine = false;

    [Tooltip("Seed: draw a brown shell texture (tan with dark cracks that spread as the shell is hit) instead of the default dark surface texture. Needs Damage Cracks.")]
    public bool shellTexture = false;

    [Tooltip("Water: old pixels of this type splash on the ground and are gone (whether or not anything needed watering); nearby Seed sprouts and the Seed pet are watered.")]
    public bool splash = false;

    [Tooltip("Old pixels of this type shatter into shards when they hit the ground (they are gone afterwards).")]
    public bool shatter = false;

    /// <summary>Does this look add objects to the cube (outline / core)?</summary>
    public bool HasExtras => outline || darkMatter || lightning || sprout || shine || starCubes > 0 || faceCircles || (colorBlindSides >= 3 && PixelDisplaySettings.ColorBlind);

    /// <summary>Does this look put its own texture on the pixel (streaks and/or damage)?</summary>
    public bool HasSurfaceTexture => streakTexture || damageCracks || chromeTexture || basicSurface != BasicSurface.None;
}

/// <summary>Helpers that build the runtime-drawn parts of a look: neon edges, the dark-matter core, shatter shards.</summary>
public static class PixelLooks
{
    /// <summary>The built-in styles (used when the list on PixelClicker is new).</summary>
    public static PixelLook[] CreateDefaults()
    {
        return new[]
        {
            // The basic pixels: a subtle drawn surface and a thin bevelled rim (a slightly lighter edge line) so they read as solid objects.
            new PixelLook { type = PixelClicker.PixelType.White, basicSurface = PixelLook.BasicSurface.Grain,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.62f, 0.64f, 0.68f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = -0.3f, outlineStrength = 0.55f },
            new PixelLook { type = PixelClicker.PixelType.Gray, basicSurface = PixelLook.BasicSurface.Brushed,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.86f, 0.87f, 0.9f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = 0.4f, outlineStrength = 0.5f },
            new PixelLook { type = PixelClicker.PixelType.Black, useColor = true, color = new Color(0.1f, 0.1f, 0.12f, 1f), basicSurface = PixelLook.BasicSurface.Sheen,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.42f, 0.42f, 0.48f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = 0.3f, outlineStrength = 0.6f },

            // Red, green and blue: a faint pixel-art mosaic and a lighter rim in their own colour; with the colour-blind setting on they also get a shape on every face.
            new PixelLook { type = PixelClicker.PixelType.Red, colorBlindSides = 3, colorBlindRotation = 90f, basicSurface = PixelLook.BasicSurface.Mosaic,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(1f, 0.6f, 0.55f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = 0.4f, outlineStrength = 0.5f },
            new PixelLook { type = PixelClicker.PixelType.Green, colorBlindSides = 4, colorBlindRotation = 45f, basicSurface = PixelLook.BasicSurface.Mosaic,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.65f, 1f, 0.65f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = 0.4f, outlineStrength = 0.5f },
            new PixelLook { type = PixelClicker.PixelType.Blue, colorBlindSides = 32, basicSurface = PixelLook.BasicSurface.Mosaic,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.6f, 0.75f, 1f, 1f), outlineBevel = true, outlineThickness = 0.045f, outlineShade = 0.4f, outlineStrength = 0.5f },

            // Vacuum: a dark purple see-through block with a black circle on every face.
            new PixelLook { type = PixelClicker.PixelType.Vacuum, useColor = true, color = new Color(0.2f, 0.05f, 0.35f, 0.55f),
                            forceTranslucent = true, smoothness = 0.95f, metallic = 0f, faceCircles = true },

            // Glass: very see-through, a faint edge so it can still be seen, shatters on the ground.
            new PixelLook { type = PixelClicker.PixelType.Glass, alpha = 0.4f, smoothness = 1f, metallic = 0f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.85f, 0.97f, 1f, 1f),
                            outlineThickness = 0.02f, outlineStrength = 0.5f, shatter = true },

            // Ghost: soft, matte, see-through (not shiny like glass), wobbling like jelly. Bounces slowly and floats off the screen.
            new PixelLook { type = PixelClicker.PixelType.Ghost, useColor = true, color = new Color(0.88f, 0.94f, 1f, 0.28f),
                            smoothness = 0f, metallic = 0f, emission = 0.7f,
                            wobble = true, customPhysics = true, ignoreViewBounds = true, floatAway = true },

            // Obsidian: sheer polished black metal with white streaks; cracks spread with every click.
            new PixelLook { type = PixelClicker.PixelType.Obsidian, useColor = true, color = Color.white,
                            metallic = 1f, smoothness = 0.95f, streakTexture = true, damageCracks = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.045f, outlineShadeOfTier = true, outlineShade = 0.45f, outlineStrength = 0.8f },

            // Electric: a faint dark-blue glass box held together by crackling lightning.
            new PixelLook { type = PixelClicker.PixelType.Electric, useColor = true, color = new Color(0.04f, 0.12f, 0.28f, 0.22f),
                            forceTranslucent = true, smoothness = 0.9f, metallic = 0f, emission = 0.6f, lightning = true },

            // Seed: a brown shell that cracks as you click it; a sprout comes out and grows a pixel.
            new PixelLook { type = PixelClicker.PixelType.Seed, useColor = true, color = Color.white,
                            metallic = 0f, smoothness = 0.1f, damageCracks = true, shellTexture = true,
                            outline = true, outlineUsesTierColor = false, outlineBevel = true, outlineThickness = 0.045f, outlineShadeOfTier = true, outlineShade = -0.4f, outlineStrength = 0.85f },

            // Water: a see-through blue jelly cube that wobbles.
            new PixelLook { type = PixelClicker.PixelType.Water, useColor = true, color = new Color(0.25f, 0.6f, 1f, 0.5f),
                            forceTranslucent = true, smoothness = 1f, metallic = 0f, emission = 0.25f, wobble = true, splash = true },

            // Mirror: polished chrome that reflects the sky.
            new PixelLook { type = PixelClicker.PixelType.Mirror, useColor = true, color = Color.white,
                            metallic = 0.7f, smoothness = 0.95f, emission = 0.3f, chromeTexture = true, shine = true },

            // Dragon Cubes 1-7: glassy orange cubes with 1-7 dark orange small cubes inside, like dragon balls.
            DragonCubeLook(PixelClicker.PixelType.DragonCube1, 1),
            DragonCubeLook(PixelClicker.PixelType.DragonCube2, 2),
            DragonCubeLook(PixelClicker.PixelType.DragonCube3, 3),
            DragonCubeLook(PixelClicker.PixelType.DragonCube4, 4),
            DragonCubeLook(PixelClicker.PixelType.DragonCube5, 5),
            DragonCubeLook(PixelClicker.PixelType.DragonCube6, 6),
            DragonCubeLook(PixelClicker.PixelType.DragonCube7, 7),

            // Singularity: a box of dark matter.
            new PixelLook { type = PixelClicker.PixelType.Singularity, useColor = true, color = new Color(0.07f, 0.02f, 0.14f, 0.5f),
                            forceTranslucent = true, smoothness = 0.95f, metallic = 0f, glowScale = 0.3f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.65f, 0.25f, 1f, 1f),
                            outlineThickness = 0.04f, darkMatter = true, gravityWell = true },
        };
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

    // ------------------------------------------------------------------
    // Surface texture: black with white streaks, plus cracks that grow with damage
    // ------------------------------------------------------------------

    private static readonly Dictionary<int, Texture2D> surfaceTextures = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, Texture2D> basicTextures = new Dictionary<int, Texture2D>();

    /// <summary>
    /// The subtle surfaces of the basic pixels (see <see cref="PixelLook.BasicSurface"/>): 128 px, mostly near-white so the pixel's colour shows
    /// through (the texture multiplies the colour). Drawn once per style and shared. Tiles seamlessly.
    /// </summary>
    public static Texture2D BasicTexture(PixelLook.BasicSurface style)
    {
        int key = (int)style;
        if (basicTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        const int size = 128;
        Color32[] px = new Color32[size * size];
        System.Random rnd = new System.Random(300 + key);
        float[] rowTone = new float[size];
        for (int y = 0; y < size; y++) rowTone[y] = (float)rnd.NextDouble();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = 0.93f;
                switch (style)
                {
                    case PixelLook.BasicSurface.Grain:
                    {
                        // Fine chalky speckle with a few soft larger blotches.
                        float blotch = PixelNoise.TileNoise(x / (float)size, y / (float)size, 6, 6, 11) - 0.5f;
                        v = 0.94f + ((float)rnd.NextDouble() - 0.5f) * 0.07f + blotch * 0.05f;
                        break;
                    }
                    case PixelLook.BasicSurface.Brushed:
                    {
                        // Horizontal brushed-metal streaks: every row has its own tone, stretched along x with a little slow variation.
                        float along = PixelNoise.TileNoise(x / (float)size, y / (float)size, 3, 128, 7) - 0.5f;
                        v = 0.88f + (rowTone[y] - 0.5f) * 0.12f + along * 0.06f;
                        break;
                    }
                    case PixelLook.BasicSurface.Sheen:
                    {
                        // Soft diagonal highlight bands (the colour is dark, so this reads as a glossy sheen) plus very fine grain.
                        float band = Mathf.Sin((x + y) / (float)size * Mathf.PI * 4f) * 0.5f + 0.5f;
                        v = 0.8f + band * band * 0.2f + ((float)rnd.NextDouble() - 0.5f) * 0.03f;
                        break;
                    }
                    case PixelLook.BasicSurface.Mosaic:
                    {
                        // An 8x8 pixel-art mosaic: neighbouring cells differ a little; each face is lighter at the top and darker at the bottom.
                        int cx = x / 16, cy = y / 16;
                        float cell = (((cx + cy) & 1) == 0 ? 0.0f : -0.05f) + (Hash01(cx, cy) - 0.5f) * 0.05f;
                        float gradient = Mathf.Lerp(0.86f, 1.0f, y / (float)(size - 1));
                        v = (0.97f + cell) * gradient;
                        break;
                    }
                }
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                px[y * size + x] = new Color32(b, b, b, 255);
            }
        }
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = style == PixelLook.BasicSurface.Mosaic ? FilterMode.Point : FilterMode.Bilinear, name = "PixelBasic" + style };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        basicTextures[key] = tex;
        return tex;
    }

    private static float Hash01(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    /// <summary>
    /// The pixel's surface texture. 'level' 0 = undamaged; up to 'maxLevel' = about to break (more cracks each level).
    /// Drawn once per level and shared.
    /// </summary>
    public static Texture2D SurfaceTexture(bool streaks, int level, int maxLevel, bool shell = false)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int key = (streaks ? 1 : 0) + level * 2 + maxLevel * 1000 + (shell ? 1 << 22 : 0);
        if (surfaceTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        const int size = 128;
        Color32[] px = new Color32[size * size];
        Color32 baseColour = shell ? new Color32(140, 92, 46, 255) : new Color32(6, 6, 9, 255);
        for (int i = 0; i < px.Length; i++) px[i] = baseColour;
        if (shell)
        {
            // Speckled wood-like grain so the shell isn't flat.
            System.Random grain = new System.Random(5);
            for (int i = 0; i < px.Length; i++)
            {
                int d = grain.Next(-14, 12);
                px[i] = new Color32((byte)Mathf.Clamp(140 + d, 0, 255), (byte)Mathf.Clamp(92 + d, 0, 255), (byte)Mathf.Clamp(46 + d / 2, 0, 255), 255);
            }
        }

        if (streaks)
        {
            // Long, thin, slightly slanted white streaks of different strength (always the same ones).
            System.Random rnd = new System.Random(77);
            for (int i = 0; i < 9; i++)
            {
                float x0 = (float)rnd.NextDouble() * size, y0 = (float)rnd.NextDouble() * size;
                float angle = 0.45f + ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                float length = size * (0.35f + (float)rnd.NextDouble() * 0.5f);
                float strength = 0.35f + (float)rnd.NextDouble() * 0.65f;
                DrawLine(px, size, x0, y0, x0 + Mathf.Cos(angle) * length, y0 + Mathf.Sin(angle) * length, 1, strength, 0.35f);
            }
        }

        if (level > 0)
        {
            // Cracks: the same jagged paths every time, drawn a little further and in more places with each level.
            int cracks = 2 + level * 2;
            int segments = 3 + level * 2;
            for (int c = 0; c < cracks; c++)
            {
                System.Random rnd = new System.Random(1000 + c * 31);
                float x = size * (0.25f + (float)rnd.NextDouble() * 0.5f), y = size * (0.25f + (float)rnd.NextDouble() * 0.5f);
                float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
                for (int s = 0; s < 14; s++)
                {
                    angle += ((float)rnd.NextDouble() - 0.5f) * 1.1f;
                    float step = 6f + (float)rnd.NextDouble() * 9f;
                    float nx = x + Mathf.Cos(angle) * step, ny = y + Mathf.Sin(angle) * step;
                    if (s < segments) DrawLine(px, size, x, y, nx, ny, level >= maxLevel ? 2 : 1, 1f, shell ? 0.06f : 0.97f);
                    x = nx; y = ny;
                }
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "PixelSurface" };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        surfaceTextures[key] = tex;
        return tex;
    }

    /// <summary>Draws a line into the texture, lightening what is underneath towards 'tone' (wraps around the edges).</summary>
    private static void DrawLine(Color32[] px, int size, float x0, float y0, float x1, float y1, int thickness, float strength, float tone)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
        byte target = (byte)Mathf.Clamp(Mathf.RoundToInt(tone * 255f), 0, 255);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            for (int dy = 0; dy < thickness; dy++)
            {
                for (int dx = 0; dx < thickness; dx++)
                {
                    int px_ = ((cx + dx) % size + size) % size, py_ = ((cy + dy) % size + size) % size;
                    Color32 old = px[py_ * size + px_];
                    byte r = (byte)Mathf.Lerp(old.r, target, strength);
                    byte g = (byte)Mathf.Lerp(old.g, target, strength);
                    byte b = (byte)Mathf.Lerp(old.b, Mathf.Min(255, target + 6), strength);
                    px[py_ * size + px_] = new Color32(r, g, b, 255);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Extras: neon edges and the dark-matter core
    // ------------------------------------------------------------------

    private static Material neonMaterial, coreMaterial;
    private static Texture2D coreTexture;
    private static readonly Dictionary<int, Mesh> edgeMeshes = new Dictionary<int, Mesh>();

    /// <summary>
    /// Builds the extra objects of a look under 'parent' (the pixel's mesh object, so they share its size and rotation).
    /// Returns the container (destroy it to remove them), or null if the look adds nothing.
    /// </summary>
    public static GameObject AddExtras(Transform parent, Mesh cube, PixelLook look, Color tierColor, Material baseMaterial)
    {
        if (parent == null || cube == null || look == null || !look.HasExtras) return null;

        GameObject root = new GameObject("Look Extras");
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        Vector3 size = cube.bounds.size;
        Vector3 centre = cube.bounds.center;

        if (look.outline)
        {
            Color c = look.outlineUsesTierColor ? new Color(tierColor.r, tierColor.g, tierColor.b, 1f) : look.outlineColor;
            if (Mathf.Abs(look.outlineShade) > 0.001f)
            {
                // A lighter or darker shade of the pixel's own colour (its 3D colour if the look overrides it).
                Color own = look.useColor && !look.outlineShadeOfTier ? look.color : tierColor;
                c = look.outlineShade > 0f ? Color.Lerp(own, Color.white, look.outlineShade) : Color.Lerp(own, Color.black, -look.outlineShade);
                c.a = 1f;
            }
            GameObject edges = new GameObject("Neon Edges", typeof(MeshFilter), typeof(MeshRenderer));
            edges.transform.SetParent(root.transform, false);
            edges.transform.localPosition = centre;
            edges.layer = root.layer;
            edges.GetComponent<MeshFilter>().sharedMesh = EdgeMesh(size, look.outlineThickness, c, look.outlineStrength, look.outlineBevel);
            MeshRenderer mr = edges.GetComponent<MeshRenderer>();
            mr.sharedMaterial = NeonMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        if (look.faceCircles)
        {
            GameObject circles = new GameObject("Face Circles", typeof(MeshFilter), typeof(MeshRenderer));
            circles.transform.SetParent(root.transform, false);
            circles.transform.localPosition = centre;
            circles.layer = root.layer;
            circles.GetComponent<MeshFilter>().sharedMesh = CircleMesh(size, look.faceCircleRadius, look.faceCircleColor);
            MeshRenderer cr = circles.GetComponent<MeshRenderer>();
            cr.sharedMaterial = OverlayMaterial(); // drawn after the (translucent) pixel, so the circles stay solid black
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;
        }

        if (look.colorBlindSides >= 3 && PixelDisplaySettings.ColorBlind)
        {
            GameObject marks = new GameObject("Color Blind Marks", typeof(MeshFilter), typeof(MeshRenderer));
            marks.transform.SetParent(root.transform, false);
            marks.transform.localPosition = centre;
            marks.layer = root.layer;
            marks.GetComponent<MeshFilter>().sharedMesh = CircleMesh(size, look.colorBlindRadius, look.colorBlindColor, look.colorBlindSides, look.colorBlindRotation);
            MeshRenderer kr = marks.GetComponent<MeshRenderer>();
            kr.sharedMaterial = OverlayMaterial();
            kr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            kr.receiveShadows = false;
        }

        if (look.starCubes > 0)
        {
            GameObject stars = new GameObject("Dragon Stars", typeof(MeshFilter), typeof(MeshRenderer));
            stars.transform.SetParent(root.transform, false);
            stars.transform.localPosition = centre;
            stars.layer = root.layer;
            stars.GetComponent<MeshFilter>().sharedMesh = StarCubesMesh(size, look.starCubes, look.starColor);
            MeshRenderer sr = stars.GetComponent<MeshRenderer>();
            sr.sharedMaterial = OverlayMaterial(); // unlit vertex colours, drawn after the see-through body so they show through it
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }

        if (look.shine)
        {
            GameObject shine = new GameObject("Shine", typeof(MeshFilter), typeof(MeshRenderer));
            shine.transform.SetParent(root.transform, false);
            shine.transform.localPosition = centre;
            shine.layer = root.layer;
            MeshFilter sf = shine.GetComponent<MeshFilter>();
            sf.sharedMesh = ShineMesh(size);
            MeshRenderer shr = shine.GetComponent<MeshRenderer>();
            shr.sharedMaterial = OverlayMaterial(); // unlit, alpha-blended vertex colours
            shr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shr.receiveShadows = false;
            shine.AddComponent<PixelLookShine>();
        }

        if (look.sprout)
        {
            GameObject sprout = new GameObject("Sprout", typeof(MeshFilter), typeof(MeshRenderer));
            sprout.transform.SetParent(root.transform, false);
            sprout.transform.localPosition = centre;
            sprout.layer = root.layer;
            sprout.GetComponent<MeshFilter>().sharedMesh = SproutMesh(size);
            MeshRenderer spr = sprout.GetComponent<MeshRenderer>();
            spr.sharedMaterial = OverlayMaterial(); // unlit vertex colours
            spr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spr.receiveShadows = false;
        }

        if (look.lightning)
        {
            GameObject bolts = new GameObject("Lightning", typeof(MeshFilter), typeof(MeshRenderer));
            bolts.transform.SetParent(root.transform, false);
            bolts.transform.localPosition = centre;
            bolts.layer = root.layer;
            MeshRenderer lr = bolts.GetComponent<MeshRenderer>();
            lr.sharedMaterial = OverlayMaterial(); // unlit vertex colours, drawn after the (see-through) body
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            bolts.AddComponent<PixelLookLightning>().Setup(size, look.lightningCoreColor, look.lightningGlowColor, look.lightningJitter);
        }

        if (look.darkMatter && baseMaterial != null)
        {
            GameObject core = new GameObject("Dark Matter Core", typeof(MeshFilter), typeof(MeshRenderer));
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = centre;
            core.transform.localScale = Vector3.one * 0.6f;
            core.layer = root.layer;
            core.GetComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer mr = core.GetComponent<MeshRenderer>();
            mr.sharedMaterial = CoreMaterial(baseMaterial);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.AddComponent<PixelLookSpin>().Setup(new Vector3(35f, 55f, 20f), 0.1f, 1.6f);
        }
        return root;
    }

    private static readonly Dictionary<int, Mesh> starMeshes = new Dictionary<int, Mesh>();

    /// <summary>
    /// 1-7 small cubes inside a cube, laid out like the stars of a dragon ball (1 centre; 2 pair; 3 triangle; 4 square; 5 square +
    /// centre; 6 two columns; 7 hexagon + centre), flat-shaded with vertex colours. Cached per size / count / colour.
    /// </summary>
    private static Mesh StarCubesMesh(Vector3 size, int count, Color colour)
    {
        count = Mathf.Clamp(count, 1, 7);
        int key = count * 100003 + Mathf.RoundToInt(size.x * 1000f) + Mathf.RoundToInt(colour.r * 255f) * 7 + Mathf.RoundToInt(colour.g * 255f) * 13;
        if (starMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        Vector2[] spots;
        switch (count)
        {
            case 1: spots = new[] { Vector2.zero }; break;
            case 2: spots = new[] { new Vector2(-0.17f, 0f), new Vector2(0.17f, 0f) }; break;
            case 3: spots = new[] { new Vector2(0f, 0.17f), new Vector2(-0.18f, -0.12f), new Vector2(0.18f, -0.12f) }; break;
            case 4: spots = new[] { new Vector2(-0.16f, 0.16f), new Vector2(0.16f, 0.16f), new Vector2(-0.16f, -0.16f), new Vector2(0.16f, -0.16f) }; break;
            case 5: spots = new[] { new Vector2(-0.2f, 0.2f), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f) }; break;
            case 6: spots = new[] { new Vector2(-0.16f, 0.22f), new Vector2(0.16f, 0.22f), new Vector2(-0.16f, 0f), new Vector2(0.16f, 0f), new Vector2(-0.16f, -0.22f), new Vector2(0.16f, -0.22f) }; break;
            default:
                spots = new Vector2[7];
                spots[0] = Vector2.zero;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    spots[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.22f;
                }
                break;
        }

        float unit = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float half = unit * (count >= 5 ? 0.075f : 0.09f); // half the edge of a small cube
        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        float[] shade = { 1f, 0.5f, 0.72f, 0.78f, 0.9f, 0.62f };

        foreach (Vector2 s in spots)
        {
            Vector3 centre = new Vector3(s.x * size.x, s.y * size.y, 0f);
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = normals[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(n, u);
                int b = v.Count;
                v.Add(centre + (n + u + w) * half); v.Add(centre + (n + u - w) * half);
                v.Add(centre + (n - u - w) * half); v.Add(centre + (n - u + w) * half);
                Color shaded = new Color(colour.r * shade[f], colour.g * shade[f], colour.b * shade[f], colour.a);
                for (int k = 0; k < 4; k++) c.Add(shaded);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
        }
        Mesh mesh = new Mesh { name = "Dragon Stars " + count };
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        starMeshes[key] = mesh;
        return mesh;
    }

    private static Texture2D chromeTex;

    /// <summary>A chrome-looking texture: pale sky on top, a bright horizon band, dark ground below, with soft diagonal streaks. Shared.</summary>
    public static Texture2D ChromeTexture()
    {
        if (chromeTex != null) return chromeTex;
        const int size = 128;
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)(size - 1); // 0 = bottom
            Color c;
            if (v > 0.52f) c = Color.Lerp(new Color(0.92f, 0.97f, 1f), new Color(0.45f, 0.65f, 0.9f), (v - 0.52f) / 0.48f);          // sky: pale near the horizon, bluer above
            else c = Color.Lerp(new Color(0.9f, 0.92f, 0.95f), new Color(0.16f, 0.17f, 0.2f), (0.52f - v) / 0.52f);                   // ground: bright at the horizon, dark below
            float band = Mathf.Exp(-Mathf.Pow((v - 0.52f) * 14f, 2f)); // the bright horizon line
            c = Color.Lerp(c, Color.white, band * 0.8f);
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float streak = Mathf.Exp(-Mathf.Pow(Mathf.Repeat(u + v * 0.6f, 0.5f) - 0.12f, 2f) * 700f) * 0.28f; // soft diagonal streaks
                Color f = Color.Lerp(c, Color.white, streak);
                px[y * size + x] = new Color32((byte)(f.r * 255f), (byte)(f.g * 255f), (byte)(f.b * 255f), 255);
            }
        }
        chromeTex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "PixelChrome" };
        chromeTex.SetPixels32(px);
        chromeTex.Apply(true, false);
        return chromeTex;
    }

    /// <summary>Vertices along one side of a Shine face grid.</summary>
    public const int ShineGrid = 8;

    /// <summary>
    /// A finely divided sheet just above each cube face. <see cref="PixelLookShine"/> animates the vertex alpha so a soft diagonal band of light
    /// sweeps across (a plain four-corner quad could only blink as a white rectangle).
    /// </summary>
    private static Mesh ShineMesh(Vector3 size)
    {
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        int n = ShineGrid;
        foreach (Vector3 normal in normals)
        {
            Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(normal, u);
            int b = v.Count;
            for (int iy = 0; iy <= n; iy++)
            {
                for (int ix = 0; ix <= n; ix++)
                {
                    float fx = ix / (float)n - 0.5f, fy = iy / (float)n - 0.5f;
                    v.Add(Vector3.Scale(normal * 0.508f + u * fx + w * fy, size));
                    c.Add(new Color(1f, 1f, 1f, 0f));
                }
            }
            for (int iy = 0; iy < n; iy++)
            {
                for (int ix = 0; ix < n; ix++)
                {
                    int i0 = b + iy * (n + 1) + ix, i1 = i0 + 1, i2 = i0 + (n + 1), i3 = i2 + 1;
                    t.Add(i0); t.Add(i2); t.Add(i1); t.Add(i1); t.Add(i2); t.Add(i3);
                    t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i1); t.Add(i3); t.Add(i2); // double-sided
                }
            }
        }
        Mesh mesh = new Mesh { name = "Shine" };
        mesh.MarkDynamic();
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static readonly Dictionary<int, Mesh> sproutMeshes = new Dictionary<int, Mesh>();

    /// <summary>A little sprout on top of a cube: a green stem and two angled leaves, flat-shaded with vertex colours.</summary>
    private static Mesh SproutMesh(Vector3 size, bool baseAtOrigin = false)
    {
        int key = Mathf.RoundToInt(size.x * 1000f) * 31 + Mathf.RoundToInt(size.y * 1000f) + (baseAtOrigin ? 1 << 24 : 0);
        if (sproutMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        List<Vector3> v = new List<Vector3>();
        List<Color> c = new List<Color>();
        List<int> t = new List<int>();
        float top = baseAtOrigin ? 0f : size.y * 0.5f; // on top of a cube, or standing on its own origin
        AddShadedBox(v, c, t, new Vector3(0f, top + size.y * 0.13f, 0f), new Vector3(size.x * 0.035f, size.y * 0.13f, size.x * 0.035f), Quaternion.identity, new Color(0.3f, 0.7f, 0.22f, 1f));
        Vector3 leafHalf = new Vector3(size.x * 0.15f, size.y * 0.022f, size.x * 0.075f);
        AddShadedBox(v, c, t, new Vector3(-size.x * 0.13f, top + size.y * 0.3f, 0f), leafHalf, Quaternion.Euler(0f, 0f, 24f), new Color(0.42f, 0.85f, 0.3f, 1f));
        AddShadedBox(v, c, t, new Vector3(size.x * 0.13f, top + size.y * 0.3f, 0f), leafHalf, Quaternion.Euler(0f, 0f, -24f), new Color(0.35f, 0.78f, 0.26f, 1f));

        Mesh mesh = new Mesh { name = "Sprout" };
        mesh.SetVertices(v);
        mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        sproutMeshes[key] = mesh;
        return mesh;
    }

    /// <summary>Height of a free-standing sprout's stem top as a fraction of its size (where a growing pixel sits).</summary>
    public const float SproutStemTop = 0.26f;

    /// <summary>A free-standing sprout (stem + two leaves, base at the origin) as a child of 'parent'; 'unit' = the size it is built for (a pixel's edge).</summary>
    public static GameObject CreateSproutObject(Transform parent, float unit)
    {
        GameObject go = new GameObject("Sprout", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = SproutMesh(Vector3.one * Mathf.Max(0.01f, unit), true);
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = OverlayMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    /// <summary>Adds a flat-shaded box (centre, half extents, rotation) to a vertex-colour mesh under construction.</summary>
    private static void AddShadedBox(List<Vector3> v, List<Color> c, List<int> t, Vector3 centre, Vector3 half, Quaternion rot, Color colour)
    {
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        float[] shade = { 1f, 0.5f, 0.72f, 0.78f, 0.9f, 0.62f };
        for (int f = 0; f < 6; f++)
        {
            Vector3 n = normals[f];
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(n, u);
            int b = v.Count;
            Vector3 hn = Vector3.Scale(n, half), hu = Vector3.Scale(u, half), hw = Vector3.Scale(w, half);
            v.Add(centre + rot * (hn + hu + hw)); v.Add(centre + rot * (hn + hu - hw));
            v.Add(centre + rot * (hn - hu - hw)); v.Add(centre + rot * (hn - hu + hw));
            Color shaded = new Color(colour.r * shade[f], colour.g * shade[f], colour.b * shade[f], colour.a);
            for (int k = 0; k < 4; k++) c.Add(shaded);
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
    }

    private static Material NeonMaterial()
    {
        if (neonMaterial != null) return neonMaterial;
        // Sprites/Default is unlit and uses vertex colours in every render pipeline.
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        neonMaterial = new Material(shader) { name = "NeonEdges" };
        return neonMaterial;
    }

    private static Material overlayMaterial;

    /// <summary>
    /// The unlit vertex-colour material for flat marks lying on a face (Vacuum's black circles, colour-blind shapes). It is
    /// drawn after the pixel itself (a higher render queue), so a translucent pixel can't be blended over the marks and tint them.
    /// </summary>
    internal static Material OverlayMaterial()
    {
        if (overlayMaterial != null) return overlayMaterial;
        Material source = NeonMaterial();
        if (source == null) return null;
        overlayMaterial = new Material(source) { name = "FaceMarks", renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50 };
        return overlayMaterial;
    }

    /// <summary>
    /// A glow that does not depend on shader keywords: a slightly larger copy of the mesh drawn on top with the unlit
    /// Sprites/Default shader (alpha-blended colour). Emission switched on from code is stripped from builds when no saved
    /// material uses it, so this keeps glowing pixels visibly luminous in a built game. Returns the new object (a child of 'parent').
    /// 'texture' (optional) is drawn instead of a flat colour (alpha of the texture = how much shows), e.g. lava cracks.
    /// </summary>
    public static GameObject AddGlowShell(Transform parent, Mesh mesh, Color colour, float alpha, float scale = 1.012f, Texture texture = null, float hdr = 1f)
    {
        Material material = OverlayMaterial();
        if (material == null || mesh == null || parent == null) return null;

        GameObject go = new GameObject("Glow Shell", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        // 'hdr' > 1 pushes the colour above 1.0 (HDR): that is what makes the post-processing Bloom light the pixel up, exactly as
        // the emission does in the Editor (the colour is unlit, so it goes straight to the screen buffer).
        block.SetColor("_Color", new Color(colour.r * hdr, colour.g * hdr, colour.b * hdr, Mathf.Clamp01(alpha)));
        if (texture != null) block.SetTexture("_MainTex", texture);
        mr.SetPropertyBlock(block);
        return go;
    }

    private static Mesh haloMesh;
    private static Texture2D haloTexture;

    /// <summary>
    /// A soft round camera-facing glow behind the pixel (like bloom, but drawn with the unlit Sprites/Default shader, so it
    /// needs no post-processing and no shader keywords). The pixel's own faces hide the middle of it, so only the halo around
    /// the silhouette shows.
    /// </summary>
    public static GameObject AddGlowHalo(Transform parent, Color colour, float alpha, float hdr, float boost = 1f)
    {
        Material material = OverlayMaterial();
        if (material == null || parent == null) return null;

        if (haloMesh == null)
        {
            haloMesh = new Mesh { name = "Glow Halo" };
            haloMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            haloMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            haloMesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
            haloMesh.RecalculateBounds();
        }
        if (haloTexture == null)
        {
            const int n = 128;
            haloTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GlowHalo" };
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - r, 2.8f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            haloTexture.SetPixels32(px);
            haloTexture.Apply(false, true);
        }

        GameObject go = new GameObject("Glow Halo", typeof(MeshFilter), typeof(MeshRenderer), typeof(PixelFaceCamera));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 1.9f;
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = haloMesh;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_Color", new Color(colour.r * hdr * 0.7f, colour.g * hdr * 0.7f, colour.b * hdr * 0.7f, Mathf.Clamp01(alpha * 0.3f * boost)));
        block.SetTexture("_MainTex", haloTexture);
        mr.SetPropertyBlock(block);
        return go;
    }

    private static Material CoreMaterial(Material baseMaterial)
    {
        if (coreMaterial != null) return coreMaterial;
        if (coreTexture == null) coreTexture = BuildDarkMatterTexture(128);

        coreMaterial = new Material(baseMaterial) { name = "DarkMatterCore" };
        coreMaterial.SetTexture("_BaseMap", coreTexture);
        coreMaterial.SetTexture("_MainTex", coreTexture);
        coreMaterial.SetTexture("_EmissionMap", coreTexture);
        if (coreMaterial.HasProperty("_BaseColor")) coreMaterial.SetColor("_BaseColor", Color.white);
        if (coreMaterial.HasProperty("_Color")) coreMaterial.SetColor("_Color", Color.white);
        if (coreMaterial.HasProperty("_EmissionColor")) coreMaterial.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.3f, 1f));
        if (coreMaterial.HasProperty("_Metallic")) coreMaterial.SetFloat("_Metallic", 0f);
        coreMaterial.EnableKeyword("_EMISSION");
        coreMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return coreMaterial;
    }

    /// <summary>A swirling purple nebula with a few stars (drawn once, shared by every dark-matter core).</summary>
    private static Texture2D BuildDarkMatterTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "DarkMatter" };
        Color32[] px = new Color32[size * size];
        Color deep = new Color(0.01f, 0f, 0.03f, 1f), violet = new Color(0.42f, 0.06f, 0.85f, 1f), glow = new Color(0.85f, 0.45f, 1f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u) + r * 9f; // twist: the further out, the more it swirls
                float sx = 0.5f + Mathf.Cos(a) * r, sy = 0.5f + Mathf.Sin(a) * r;
                float n = Mathf.PerlinNoise(sx * 4f + 20f, sy * 4f + 20f) * 0.65f + Mathf.PerlinNoise(sx * 10f + 5f, sy * 10f + 5f) * 0.35f;
                n = Mathf.SmoothStep(0.3f, 0.85f, n);
                Color c = Color.Lerp(deep, violet, n);
                c = Color.Lerp(c, glow, Mathf.Clamp01((n - 0.8f) * 4f) * 0.5f);

                // The odd bright speck, like a star.
                float h = Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
                if (h > 0.994f) c = Color.Lerp(c, Color.white, 0.9f);

                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    // ------------------------------------------------------------------
    // Neon edge mesh: 12 thin boxes for a bright core plus 12 wider, fainter ones for the halo
    // ------------------------------------------------------------------

    private static Mesh EdgeMesh(Vector3 size, float thicknessFraction, Color colour, float strength, bool bevel = false)
    {
        int key = unchecked(size.GetHashCode() * 31 + thicknessFraction.GetHashCode() * 17 + colour.GetHashCode() * 13 + strength.GetHashCode() + (bevel ? 977 : 0));
        if (edgeMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        float t = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * thicknessFraction;
        Color halo = new Color(colour.r, colour.g, colour.b, 0.3f * strength);
        Color core = Color.Lerp(colour, Color.white, 0.5f);
        core.a = strength;

        List<Vector3> v = new List<Vector3>(400);
        List<Color> col = new List<Color>(400);
        List<int> tri = new List<int>(600);
        if (bevel)
        {
            // One thin solid strip along each edge, lying just inside the surface (its outer faces a hair outside the cube's faces).
            Color solid = colour;
            solid.a = Mathf.Clamp01(strength);
            AddEdgeBoxes(v, col, tri, size, t, solid, true);
        }
        else
        {
            AddEdgeBoxes(v, col, tri, size, t * 2.6f, halo);
            AddEdgeBoxes(v, col, tri, size, t, core);
        }

        Mesh mesh = new Mesh { name = "NeonEdges" };
        mesh.SetVertices(v);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        edgeMeshes[key] = mesh;
        return mesh;
    }

    private static void AddEdgeBoxes(List<Vector3> v, List<Color> col, List<int> tri, Vector3 size, float t, Color colour, bool flush = false)
    {
        Vector3 h = size * 0.5f;
        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    Vector3 centre = Vector3.zero, ext = Vector3.zero;
                    float inset = flush ? t * 0.5f - Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.004f : 0f; // flush: pulled in so the strip lies on the faces
                    centre[a] = i * (h[a] - inset);
                    centre[b] = j * (h[b] - inset);
                    ext[axis] = h[axis] + t * 0.5f; // a little longer so the corners close up
                    ext[a] = t * 0.5f;
                    ext[b] = t * 0.5f;
                    AddBox(v, col, tri, centre, ext, colour);
                }
            }
        }
    }

    private static readonly Dictionary<int, Mesh> circleMeshes = new Dictionary<int, Mesh>();

    /// <summary>Six flat discs, one just outside each face of a box of this size.</summary>
    private static Mesh CircleMesh(Vector3 size, float radiusFraction, Color colour, int segments = 32, float rotationDegrees = 0f)
    {
        int key = unchecked(size.GetHashCode() * 31 + radiusFraction.GetHashCode() * 17 + colour.GetHashCode() * 13 + segments * 7 + rotationDegrees.GetHashCode() * 3);
        if (circleMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        int Segments = Mathf.Clamp(segments, 3, 64);
        float rot = rotationDegrees * Mathf.Deg2Rad;
        float gap = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.004f; // just off the face so it never z-fights
        List<Vector3> v = new List<Vector3>();
        List<Color> col = new List<Color>();
        List<int> tri = new List<int>();

        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            float radius = Mathf.Min(size[a], size[b]) * radiusFraction;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 centre = Vector3.zero;
                centre[axis] = sign * (size[axis] * 0.5f + gap);
                int start = v.Count;
                v.Add(centre); col.Add(colour);
                for (int i = 0; i < Segments; i++)
                {
                    float ang = rot + i * Mathf.PI * 2f / Segments;
                    Vector3 p = centre;
                    p[a] += Mathf.Cos(ang) * radius;
                    p[b] += Mathf.Sin(ang) * radius;
                    v.Add(p); col.Add(colour);
                }
                for (int i = 0; i < Segments; i++)
                {
                    int p1 = start + 1 + i, p2 = start + 1 + (i + 1) % Segments;
                    tri.Add(start); tri.Add(p1); tri.Add(p2);   // both windings, so it shows from either side
                    tri.Add(start); tri.Add(p2); tri.Add(p1);
                }
            }
        }

        Mesh mesh = new Mesh { name = "FaceMarks" };
        mesh.SetVertices(v);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        circleMeshes[key] = mesh;
        return mesh;
    }

    private static readonly int[][] BoxQuads =
    {
        new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 4, 6, 2 },
        new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 }, new[] { 2, 6, 7, 3 },
    };

    private static void AddBox(List<Vector3> v, List<Color> col, List<int> tri, Vector3 centre, Vector3 ext, Color colour)
    {
        int start = v.Count;
        for (int i = 0; i < 8; i++)
        {
            v.Add(centre + new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z));
            col.Add(colour);
        }
        foreach (int[] q in BoxQuads)
        {
            tri.Add(start + q[0]); tri.Add(start + q[1]); tri.Add(start + q[2]);
            tri.Add(start + q[0]); tri.Add(start + q[2]); tri.Add(start + q[3]);
        }
    }
}

/// <summary>
/// An old pixel that slowly pulls the other old pixels within a radius towards itself (singularity). A very faint ring
/// (a camera-facing sprite) shows how far the pull reaches. The ring follows the pixel and shrinks with it when it despawns.
/// </summary>
public class OldPixelGravityWell : MonoBehaviour
{
    private PixelClicker clicker;
    private float radius, pullSpeed, grip;
    private GameObject ring;
    private SpriteRenderer ringRenderer;
    private float startScale;
    private static Sprite ringSprite;

    private PixelLook look; // read every frame, so changing the values in the Inspector while playing takes effect at once

    // The pixels this well is holding right now (at most wellMaxPixels). A pixel is claimed by one well only.
    private readonly HashSet<Rigidbody> members = new HashSet<Rigidbody>();
    private readonly HashSet<Rigidbody> inReach = new HashSet<Rigidbody>();
    private readonly List<Rigidbody> candidates = new List<Rigidbody>();
    private static readonly Dictionary<Rigidbody, OldPixelGravityWell> claims = new Dictionary<Rigidbody, OldPixelGravityWell>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { claims.Clear(); }

    public void Setup(PixelClicker owner, PixelLook source)
    {
        clicker = owner;
        look = source;
        ReadSettings();
        startScale = Mathf.Max(0.0001f, transform.lossyScale.x);

        if (ringSprite == null) ringSprite = BuildRingSprite();
        ring = new GameObject("Gravity Ring");
        ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = ringSprite;
        ringRenderer.sortingOrder = -2;
        ApplyRingColour();
    }

    private void Start()
    {
        // The well pixel lives much longer than a normal old pixel.
        OldPixelDespawn own = GetComponent<OldPixelDespawn>();
        if (own != null) own.SetLifetime(look != null && look.wellLifetime > 0f ? look.wellLifetime : 300f);
    }

    private void ReadSettings()
    {
        if (look == null) return;
        radius = look.wellRadius;
        pullSpeed = look.wellPullSpeed;
        grip = look.wellGrip;
    }

    private void ApplyRingColour()
    {
        if (ringRenderer == null || look == null) return;
        Color c = look.wellRingColor;
        c.a = look.wellRingOpacity;
        ringRenderer.color = c;
    }

    private static Vector3 VelocityOf(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocityOf(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    private void FixedUpdate()
    {
        ReadSettings();
        if (clicker == null || pullSpeed <= 0f) return;
        // The well keeps working while the player carries it (Pixel Grabbing); it only stops once it is shrinking away.
        if (transform.lossyScale.x < startScale * 0.9f) return;

        var list = clicker.OldPixels;
        Vector3 centre = transform.position;
        float blend = Mathf.Clamp01(grip * Time.fixedDeltaTime);
        candidates.Clear();
        inReach.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody other = list[i];
            if (other == null || other.gameObject == gameObject) continue;
            if (clicker.IsFlyingPixel(other))
            {
                // A streaking meteor pixel is bent towards the well from far beyond the ring (it flies up and away from the floor,
                // so it would never come near otherwise) and slowed; once inside the ring it is captured and dragged in.
                float reach = radius * (look != null && look.wellFlyReach > 0f ? look.wellFlyReach : 4f);
                float sqr = (centre - other.position).sqrMagnitude;
                if (sqr > reach * reach) continue;
                if (sqr > radius * radius)
                {
                    // Never dips below the floor: a trigger-collider pixel has nothing to stop it, so keep it at or above the well's height.
                    if (other.position.y < centre.y)
                    {
                        Vector3 p = other.position;
                        p.y = centre.y;
                        other.position = p;
                        Vector3 low = VelocityOf(other);
                        if (low.y < 0f) { low.y = 0f; SetVelocityOf(other, low); }
                    }
                    Vector3 fv = VelocityOf(other);
                    float fs = fv.magnitude;
                    if (fs > 0.01f)
                    {
                        Vector3 steered = Vector3.RotateTowards(fv.normalized, (centre - other.position).normalized, 5f * Time.fixedDeltaTime, 0f);
                        if (steered.y < 0f && other.position.y <= centre.y + 0.3f) steered.y = 0f; // skim along the floor, not into it
                        SetVelocityOf(other, steered * (fs * Mathf.Clamp01(1f - 1.2f * Time.fixedDeltaTime)));
                    }
                    OldPixelDespawn fd = other.GetComponent<OldPixelDespawn>();
                    if (fd != null) fd.KeepAlive();
                    continue;
                }
                clicker.LandFlyingPixel(other);
                SetVelocityOf(other, VelocityOf(other) * 0.2f);
            }
            if (other.isKinematic) continue;
            OldPixelDespawn despawn = other.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue; // carried by the player / vanishing

            if ((centre - other.position).magnitude > radius) continue;
            candidates.Add(other);
            inReach.Add(other);
        }

        // Keep the pixels already held that are still in reach; free the others.
        members.RemoveWhere(m =>
        {
            bool keep = m != null && inReach.Contains(m);
            if (!keep && claims.TryGetValue(m, out OldPixelGravityWell owner) && owner == this) claims.Remove(m);
            return !keep;
        });

        // Fill the free slots with the nearest unclaimed pixels in reach (other gravity wells count as pixels too, which forces spacing).
        int cap = look != null && look.wellMaxPixels > 0 ? look.wellMaxPixels : 20;
        if (members.Count < cap)
        {
            candidates.Sort((a, b) => (centre - a.position).sqrMagnitude.CompareTo((centre - b.position).sqrMagnitude));
            foreach (Rigidbody c in candidates)
            {
                if (members.Count >= cap) break;
                if (members.Contains(c)) continue;
                if (claims.TryGetValue(c, out OldPixelGravityWell owner) && owner != null && owner != this) continue; // another well has it
                claims[c] = this;
                members.Add(c);
            }
        }

        foreach (Rigidbody other in members)
        {
            Vector3 to = centre - other.position;
            float distance = to.magnitude;
            OldPixelDespawn despawn = other.GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.KeepAlive(); // held by the well: it doesn't age while the well works
            if (distance < 0.08f) continue;

            // A constant drag: whatever the pixel is doing (even sitting on the floor against its friction), its speed towards
            // the well is brought up to the drag speed. Speed it already has towards the well is kept.
            Vector3 dir = to / distance;
            Vector3 v = VelocityOf(other);
            float towards = Vector3.Dot(v, dir);
            if (towards < pullSpeed) SetVelocityOf(other, v + dir * ((pullSpeed - towards) * blend));
        }
    }

    private void LateUpdate()
    {
        if (ring == null) return;
        ApplyRingColour();
        Camera cam = Camera.main;
        float shrink = Mathf.Clamp01(transform.lossyScale.x / startScale);
        ring.transform.position = transform.position;
        if (cam != null) ring.transform.rotation = cam.transform.rotation;
        ring.transform.localScale = Vector3.one * (radius * 2f * shrink);
    }

    private void OnDestroy()
    {
        foreach (Rigidbody m in members) claims.Remove(m); // let go of everything it held
        members.Clear();
        if (ring != null) Destroy(ring);
    }

    /// <summary>A thin round ring with a soft edge (white; tinted by the sprite colour).</summary>
    private static Sprite BuildRingSprite()
    {
        const int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "GravityRing" };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = ((x + 0.5f) / s - 0.5f) * 2f, v = ((y + 0.5f) / s - 0.5f) * 2f;
                float d = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.94f);          // distance from the ring line
                float a = Mathf.Clamp01(1f - d / 0.035f);                          // the line
                a = Mathf.Max(a, Mathf.Clamp01(1f - d / 0.12f) * 0.25f);           // a soft glow beside it
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 128f);
    }
}

/// <summary>Squash-and-stretch scale for the jelly wobble (keeps the volume about the same).</summary>
public static class PixelWobble
{
    public static Vector3 Scale(float time, float amount, float speed, float phase)
    {
        float t = time * speed + phase;
        float x = 1f + amount * Mathf.Sin(t);
        float y = 1f + amount * Mathf.Sin(t * 1.31f + 2.1f);
        float z = 1f + amount * Mathf.Sin(t * 0.77f + 4.2f);
        float k = 1f / Mathf.Pow(Mathf.Max(0.01f, x * y * z), 1f / 3f);
        return new Vector3(x * k, y * k, z * k);
    }
}

/// <summary>Wobbles a falling copy's visual (a child of the old pixel, so the pixel's own scale animations still work).</summary>
public class PixelLookWobble : MonoBehaviour
{
    private float amount, speed, phase;

    public void Setup(float wobbleAmount, float wobbleSpeed)
    {
        amount = wobbleAmount;
        speed = wobbleSpeed;
        phase = UnityEngine.Random.value * 6.28f;
    }

    private void Update()
    {
        transform.localScale = PixelWobble.Scale(Time.time, amount, speed, phase);
    }
}

/// <summary>
/// An old pixel that bounces a few times and then floats away off the screen (ghosts). It stops colliding once it floats,
/// drifts sideways and upwards, and is removed when it is well outside the camera's view.
/// </summary>
public class OldPixelFloat : MonoBehaviour
{
    private PixelClicker clicker;
    private int bouncesLeft;
    private float lift, drift;
    private bool floating;
    private Rigidbody body;

    public void Setup(PixelClicker owner, int bounces, float liftFraction, float driftSpeed)
    {
        clicker = owner;
        bouncesLeft = Mathf.Max(1, bounces);
        lift = liftFraction;
        drift = driftSpeed;
        body = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (floating) return;
        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (Vector3.Dot(collision.GetContact(0).normal, Vector3.up) < 0.4f) return;  // only the ground
        if (--bouncesLeft > 0) return;
        StartFloating();
    }

    private void StartFloating()
    {
        floating = true;
        ScaledGravity gravity = GetComponent<ScaledGravity>();
        if (gravity != null) gravity.scale = -lift; // gently upwards from now on
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false; // nothing stops it any more

        Camera cam = Camera.main;
        Vector3 side = cam != null ? cam.transform.right : Vector3.right;
        float sign = cam != null && cam.WorldToViewportPoint(transform.position).x < 0.5f ? -1f : 1f;
        if (UnityEngine.Random.value < 0.25f) sign = -sign;
        if (body != null) body.AddForce(side * (sign * drift) + Vector3.up * (drift * 0.4f), ForceMode.VelocityChange);
    }

    private static readonly RaycastHit[] groundHits = new RaycastHit[12];

    /// <summary>
    /// A floating ghost pixel has no colliders, so a gravity well dragging it around could pull it through the floor. While it
    /// floats it is kept above the ground below it instead (the ground = the nearest upward-facing surface that is not an old pixel or the cube).
    /// </summary>
    private void FixedUpdate()
    {
        if (!floating || body == null) return;
        Vector3 pos = body.position;
        float half = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)) * 0.5f;
        int n = Physics.RaycastNonAlloc(pos + Vector3.up * (half * 6f), Vector3.down, groundHits, half * 40f, ~0, QueryTriggerInteraction.Ignore);
        float groundY = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = groundHits[i];
            if (h.collider == null || h.normal.y < 0.5f) continue;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
            if (clicker != null && clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.point.y > groundY) groundY = h.point.y;
        }
        if (float.IsNegativeInfinity(groundY) || pos.y >= groundY + half) return;
        pos.y = groundY + half;
        body.position = pos;
#if UNITY_6000_0_OR_NEWER
        Vector3 vel = body.linearVelocity;
        if (vel.y < 0f) { vel.y = 0f; body.linearVelocity = vel; }
#else
        Vector3 vel = body.velocity;
        if (vel.y < 0f) { vel.y = 0f; body.velocity = vel; }
#endif
    }

    private void Update()
    {
        if (!floating) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        if (v.z < 0f || v.x < -0.4f || v.x > 1.4f || v.y > 1.4f || v.y < -0.4f)
        {
            if (clicker != null && body != null) clicker.ReleaseOldPixel(body, false);
            Destroy(gameObject);
        }
    }
}

/// <summary>Sweeps a soft diagonal band of light across the Mirror pixel's faces in turn (animates the vertex alpha of the Shine mesh).</summary>
public class PixelLookShine : MonoBehaviour
{
    private Mesh mesh;
    private Color[] colors;
    private float[] diagonal;      // each vertex's position along the sweep direction, -1..1
    private float phase;
    private bool wasActive;

    private void Awake()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        mesh = Instantiate(mf.sharedMesh); // each cube gets its own copy so the glints are independent
        mf.sharedMesh = mesh;
        phase = UnityEngine.Random.value * 6.28f;

        int n = PixelLooks.ShineGrid, perFace = (n + 1) * (n + 1);
        colors = new Color[perFace * 6];
        diagonal = new float[perFace * 6];
        for (int face = 0; face < 6; face++)
            for (int iy = 0; iy <= n; iy++)
                for (int ix = 0; ix <= n; ix++)
                    diagonal[face * perFace + iy * (n + 1) + ix] = (ix + iy) / (float)n - 1f;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    private void Update()
    {
        if (mesh == null) return;
        int n = PixelLooks.ShineGrid, perFace = (n + 1) * (n + 1);
        float time = Time.unscaledTime * 0.55f + phase;
        bool any = false;
        for (int face = 0; face < 6; face++)
        {
            // Each face gets a sweep once per cycle (a little after the previous face); the rest of the time it is clear.
            float s = Mathf.Repeat(time - face * 0.22f, 2.4f);
            bool active = s < 0.7f;
            float centre = Mathf.Lerp(-1.5f, 1.5f, s / 0.7f);
            for (int i = 0; i < perFace; i++)
            {
                float a = 0f;
                if (active)
                {
                    float d = Mathf.Abs(diagonal[face * perFace + i] - centre) / 0.45f;
                    float k = Mathf.Clamp01(1f - d);
                    a = k * k * (3f - 2f * k) * 0.8f;
                    any = true;
                }
                colors[face * perFace + i] = new Color(1f, 1f, 1f, a);
            }
        }
        if (any || wasActive) mesh.colors = colors; // nothing to redraw while every face is clear
        wasActive = any;
    }
}

/// <summary>Spins (and gently pulses) a pixel's dark-matter core.</summary>
public class PixelLookSpin : MonoBehaviour
{
    private Vector3 spin, baseScale;
    private float pulseAmount, pulseSpeed, phase;

    public void Setup(Vector3 degreesPerSecond, float pulse, float speed)
    {
        spin = degreesPerSecond;
        baseScale = transform.localScale;
        pulseAmount = pulse;
        pulseSpeed = speed;
        phase = UnityEngine.Random.value * 6.28f;
        transform.localRotation = UnityEngine.Random.rotation;
    }

    private void Update()
    {
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        transform.localScale = baseScale * (1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed + phase));
    }
}

/// <summary>
/// Added to old pixels that shatter (glass): when one hits the ground fast enough it breaks into flying shards and is gone.
/// </summary>
public class OldPixelShatter : MonoBehaviour
{
    private PixelClicker clicker;
    private float minSpeed, shardSpeed, shardLife, shardSize;
    private int shardCount;
    private string soundId;
    private bool done;
    private bool held;          // the player is carrying it (Pixel Grabbing): it can't break
    private bool gentle;        // let go softly: its next landing is a soft set-down, not a break
    private float gentleBonus;  // seconds added to its despawn timer on that soft landing

    /// <summary>The player picked it up: it can't shatter while carried.</summary>
    public void Grabbed()
    {
        held = true;
        gentle = false;
    }

    /// <summary>
    /// The player let go. If that was gentle (slow), its next landing is a soft set-down: it doesn't break, loses most of its
    /// speed, and its despawn timer is extended by 'lifetimeBonus' seconds.
    /// </summary>
    public void Released(bool gentleRelease, float lifetimeBonus)
    {
        held = false;
        gentle = gentleRelease;
        gentleBonus = lifetimeBonus;
    }

    public void Setup(PixelClicker owner, float minImpactSpeed, int shards, float speed, float life, float size, string sound)
    {
        clicker = owner;
        minSpeed = minImpactSpeed;
        shardCount = shards;
        shardSpeed = speed;
        shardLife = life;
        shardSize = size;
        soundId = sound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (done || clicker == null || held) return;
        if (collision.collider.GetComponent<OldPixelShard>() != null) return;

        if (gentle)
        {
            // Set down gently: it settles (any surface, including other pixels) and lives longer.
            gentle = false;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity *= 0.2f;
#else
                rb.velocity *= 0.2f;
#endif
                rb.angularVelocity *= 0.3f;
            }
            OldPixelDespawn despawn = GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.AddLifetime(gentleBonus);
            return;
        }

        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (collision.relativeVelocity.magnitude < minSpeed) return;

        ContactPoint contact = collision.GetContact(0);
        if (Vector3.Dot(contact.normal, Vector3.up) < 0.4f) return; // only the ground, not a wall or the cube

        Shatter(contact.point, contact.normal);
    }

    /// <summary>A Sorter's stream put the pixel on the ground (it had no collision on the way). Returns true if it shattered (then it is gone).</summary>
    public bool StreamLand(Vector3 point, Vector3 normal)
    {
        if (done || clicker == null || held) return false;
        Shatter(point, normal);
        return true;
    }

    private void Shatter(Vector3 point, Vector3 normal)
    {
        PixelStats.Count("old.shattered");
        done = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) clicker.ReleaseOldPixel(body, false);

        if (!string.IsNullOrEmpty(soundId)) PixelAudio.Play(soundId);

        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mf != null && mr != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            Vector3 scale = transform.lossyScale;
            float baseSize = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = new GameObject("GlassShard");
                shard.layer = gameObject.layer;
                shard.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * baseSize * 0.35f;
                shard.transform.rotation = UnityEngine.Random.rotation;
                // Flat slivers of different sizes.
                shard.transform.localScale = new Vector3(
                    baseSize * shardSize * UnityEngine.Random.Range(0.6f, 1.2f),
                    baseSize * shardSize * UnityEngine.Random.Range(0.12f, 0.3f),
                    baseSize * shardSize * UnityEngine.Random.Range(0.5f, 1f));

                shard.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer smr = shard.AddComponent<MeshRenderer>();
                smr.sharedMaterial = mr.sharedMaterial;
                smr.SetPropertyBlock(block);
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                shard.AddComponent<BoxCollider>();
                Rigidbody rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                Vector3 push = normal * shardSpeed * UnityEngine.Random.Range(0.4f, 1f)
                             + UnityEngine.Random.onUnitSphere * shardSpeed * 0.7f;
                push.y = Mathf.Abs(push.y);
                rb.AddForce(push, ForceMode.VelocityChange);
                rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(5f, 15f), ForceMode.VelocityChange);

                shard.AddComponent<OldPixelShard>().Setup(shardLife * UnityEngine.Random.Range(0.7f, 1.2f));
            }
        }
        Destroy(gameObject);
    }
}

/// <summary>A flying glass shard: lives for a moment, then shrinks away.</summary>
public class OldPixelShard : MonoBehaviour
{
    private float life, age;
    private Vector3 startScale;
    private const float ShrinkSeconds = 0.35f;

    public void Setup(float lifeSeconds)
    {
        life = lifeSeconds;
        startScale = transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age < life) return;

        float k = 1f - (age - life) / ShrinkSeconds;
        if (k <= 0f) { Destroy(gameObject); return; }
        transform.localScale = startScale * k;
    }
}

/// <summary>Turns its object to face the camera every frame (the glow halo).</summary>
public class PixelFaceCamera : MonoBehaviour
{
    private Camera cam;

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}


/// <summary>
/// The Electric pixel's lightning: a mesh of jagged bolts along the 12 edges plus a few arcs through the inside, rebuilt a
/// dozen times a second so it crackles. Every bolt is two crossed ribbons (so it reads from any angle) in two passes: a wide
/// soft glow and a thin bright core. With many electric pixels around, the bolts get simpler to keep things light.
/// </summary>
public class PixelLookLightning : MonoBehaviour
{
    private Vector3 size = Vector3.one;
    private Color core = Color.white, glow = Color.cyan;
    private float jitter = 0.09f;
    private Mesh mesh;
    private float timer;
    private static int active; // how many are alive: more of them = simpler bolts

    private readonly System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
    private readonly System.Collections.Generic.List<Color> colors = new System.Collections.Generic.List<Color>();
    private readonly System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = 0; }

    public void Setup(Vector3 cubeSize, Color coreColor, Color glowColor, float jitterFraction)
    {
        size = cubeSize;
        core = coreColor;
        glow = glowColor;
        jitter = jitterFraction;
        mesh = new Mesh { name = "Lightning" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        Rebuild();
        timer = UnityEngine.Random.Range(0.02f, 0.08f);
    }

    private void OnEnable() { active++; }
    private void OnDisable() { active = Mathf.Max(0, active - 1); }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = active <= 6 ? UnityEngine.Random.Range(0.04f, 0.09f) : UnityEngine.Random.Range(0.1f, 0.18f);
        Rebuild();
    }

    private Vector3 Corner(int i) => new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);

    private void Rebuild()
    {
        if (mesh == null) return;
        verts.Clear(); colors.Clear(); tris.Clear();
        bool rich = active <= 6;
        int segs = rich ? 6 : 3;
        float unit = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float coreW = unit * 0.035f, glowW = unit * 0.11f;

        // The 12 edges: corner pairs that differ in exactly one bit.
        for (int a = 0; a < 8; a++)
            for (int bit = 1; bit <= 4; bit <<= 1)
            {
                int b = a ^ bit;
                if (b < a) continue;
                Bolt(Corner(a), Corner(b), segs, jitter, coreW, glowW, rich);
            }

        // A few arcs through the inside, from one point on the surface to another.
        int arcs = rich ? UnityEngine.Random.Range(3, 6) : UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < arcs; i++)
            Bolt(RandomSurfacePoint(), RandomSurfacePoint(), segs + 2, jitter * 1.6f, coreW * 0.8f, glowW * 0.8f, rich);

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.bounds = new Bounds(Vector3.zero, size * 1.3f);
    }

    private Vector3 RandomSurfacePoint()
    {
        Vector3 p = new Vector3(UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f);
        int axis = UnityEngine.Random.Range(0, 3);
        p[axis] = UnityEngine.Random.value < 0.5f ? -0.5f : 0.5f;
        return p;
    }

    private void Bolt(Vector3 a, Vector3 b, int segments, float wander, float coreWidth, float glowWidth, bool withGlow)
    {
        Vector3 prev = a;
        for (int s = 1; s <= segments; s++)
        {
            float t = s / (float)segments;
            Vector3 p = Vector3.Lerp(a, b, t);
            if (s < segments)
            {
                float fade = Mathf.Sin(t * Mathf.PI);
                p += new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * (wander * fade);
            }
            float flicker = UnityEngine.Random.Range(0.55f, 1f);
            if (withGlow) Ribbon(prev, p, glowWidth, new Color(glow.r, glow.g, glow.b, 0.22f * flicker));
            Ribbon(prev, p, coreWidth, new Color(core.r, core.g, core.b, flicker));
            prev = p;
        }
    }

    /// <summary>Two crossed flat strips from 'a' to 'b' (unit-cube space, scaled by the cube's size here).</summary>
    private void Ribbon(Vector3 a, Vector3 b, float width, Color colour)
    {
        Vector3 pa = Vector3.Scale(a, size), pb = Vector3.Scale(b, size);
        Vector3 dir = pb - pa;
        if (dir.sqrMagnitude < 1e-8f) return;
        dir.Normalize();
        Vector3 side1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (width * 0.5f);
        Vector3 side2 = Vector3.Cross(dir, side1).normalized * (width * 0.5f);
        AddQuad(pa, pb, side1, colour);
        AddQuad(pa, pb, side2, colour);
    }

    private void AddQuad(Vector3 pa, Vector3 pb, Vector3 side, Color colour)
    {
        int i = verts.Count;
        verts.Add(pa - side); verts.Add(pa + side); verts.Add(pb + side); verts.Add(pb - side);
        for (int k = 0; k < 4; k++) colors.Add(colour);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }
}
