// ---- System notes ----
// PixelLooksTextures.cs
//   Part of PixelLooks (partial class): the drawn textures (chest, ore, basic surfaces, streak / crack surface, chrome, dark matter). Their caches are in PixelLooks.cs. The system notes are in PixelLooks.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static partial class PixelLooks
{
    /// <summary>
    /// The Treasure chest's surface (the same on every face): chunky 64 px wooden planks with dark outlines, two gold straps, a gold lock plate
    /// with a keyhole and rivets at the corners. 'step' 0-8 = how far it has been hit: a yellow-white light leaks out along the lid seam, wider
    /// and brighter with every step. Drawn once per step.
    /// </summary>
    public static Texture2D ChestTexture(int step)
    {
        step = Mathf.Clamp(step, 0, 8);
        if (chestTextures.TryGetValue(step, out Texture2D cached) && cached != null) return cached;

        const int n = 64;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color c = ChestTexel(x, y, step);
                px[y * n + x] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), 255);
            }

        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = "ChestSurface" + step };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        chestTextures[step] = tex;
        return tex;
    }

    // The chest's colours (ChestTexture).
    private static readonly Color ChestWood = new Color(0.5f, 0.3f, 0.14f, 1f), ChestWoodDark = new Color(0.3f, 0.17f, 0.07f, 1f),
                                  ChestGold = new Color(1f, 0.78f, 0.22f, 1f), ChestGoldDark = new Color(0.62f, 0.44f, 0.08f, 1f),
                                  ChestDark = new Color(0.1f, 0.06f, 0.03f, 1f);

    /// <summary>ChestTexture step: the colour of one texel (x, y) of the 64 px chest face at hit 'step'.</summary>
    private static Color ChestTexel(int x, int y, int step)
    {
        Color c;
        // Planks run across; a seam every 8 px, each plank its own tone and grain.
        int plank = y / 8;
        float tone = 0.85f + PixelNoise.Hash(plank, 7, 3) * 0.3f;
        float grain = PixelNoise.Hash(x / 2, y, 11) * 0.12f;
        c = Color.Lerp(ChestWoodDark, ChestWood, 0.5f + grain) * tone;
        if (y % 8 == 0) c = ChestWoodDark * 0.8f;
        c.a = 1f;

        bool strap = (x >= 9 && x <= 14) || (x >= 49 && x <= 54);
        bool frame = x <= 2 || x >= 61 || y <= 2 || y >= 61;
        if (strap || frame) { c = Color.Lerp(ChestGoldDark, ChestGold, 0.55f + PixelNoise.Hash(x, y, 5) * 0.35f); if (x == 9 || x == 49 || (frame && (x == 2 || x == 61 || y == 2 || y == 61))) c = ChestGoldDark * 0.8f; c.a = 1f; }

        // The lid: a dark seam a bit above the middle, with a gold lock plate on it.
        int seam = 27;
        if (Mathf.Abs(y - seam) <= 1 && !strap) c = ChestDark;
        bool lockPlate = x >= 27 && x <= 36 && y >= 22 && y <= 36;
        if (lockPlate) { c = (x == 27 || x == 36 || y == 22 || y == 36) ? ChestGoldDark : Color.Lerp(ChestGoldDark, ChestGold, 0.75f); }
        bool keyhole = (x >= 31 && x <= 32 && y >= 27 && y <= 32) || (x >= 30 && x <= 33 && y >= 31 && y <= 32);
        if (keyhole) c = ChestDark;
        // Rivets.
        if ((x == 5 || x == 58) && (y == 5 || y == 58 || y == 32)) c = ChestGold;

        // Light leaking out of the seam as it is hit.
        if (step > 0) c = ChestSeamLight(c, y, seam, step, lockPlate);
        return c;
    }

    /// <summary>ChestTexel step: the yellow-white light leaking out along the lid seam (wider and brighter with every hit 'step').</summary>
    private static Color ChestSeamLight(Color c, int y, int seam, int step, bool lockPlate)
    {
        float spread = 1f + step * 0.9f;                                   // half-width in px
        float d = Mathf.Abs(y - seam) / spread;
        if (d < 1f && !lockPlate)
        {
            float glow = (1f - d) * (0.35f + step * 0.08f);
            c = Color.Lerp(c, new Color(1f, 0.93f, 0.55f, 1f), Mathf.Clamp01(glow));
        }
        return c;
    }

    /// <summary>
    /// An ore's surface: chunky 64 px pixel-art rock (grey, speckled) with veins and flecks of 'metal' running through it. 'level' 0 = whole,
    /// up to 'maxLevel' = about to break (dark cracks spread a little further with each level). Drawn once per colour and level.
    /// </summary>
    public static Texture2D OreTexture(Color metal, int level, int maxLevel)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int rgb = (Mathf.RoundToInt(metal.r * 255f) << 16) | (Mathf.RoundToInt(metal.g * 255f) << 8) | Mathf.RoundToInt(metal.b * 255f);
        int key = unchecked(rgb * 31 + level * 7919 + maxLevel * 104729);
        if (oreTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        const int size = 64;
        Color32[] px = new Color32[size * size];
        int seed = rgb % 997;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float rockNoise = PixelNoise.Fbm(u, v, 4, 3, 11 + seed);
                float grit = PixelNoise.Hash(x, y, 5 + seed);
                float grey = Mathf.Lerp(0.22f, 0.5f, rockNoise) + (grit - 0.5f) * 0.1f;
                Color c = new Color(grey * 1.04f, grey, grey * 0.96f, 1f);

                // Veins: thin winding bands where a second noise crosses its middle value.
                float vein = Mathf.Abs(PixelNoise.Fbm(u + 0.17f, v, 3, 3, 23 + seed) - 0.5f);
                // Flecks: blobs where a finer noise is high.
                float fleck = PixelNoise.Fbm(u, v, 7, 2, 31 + seed);
                bool isMetal = vein < 0.03f || fleck > 0.7f;
                if (isMetal)
                {
                    float shine = 0.78f + grit * 0.5f;
                    c = new Color(metal.r * shine, metal.g * shine, metal.b * shine, 1f);
                    if (grit > 0.92f) c = Color.Lerp(c, Color.white, 0.55f);   // a glint
                }
                px[y * size + x] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), 255);
            }

        if (level > 0)
        {
            // Dark cracks: the same jagged paths every time, longer and in more places with each level.
            int cracks = 2 + level * 2;
            int segments = 3 + level * 2;
            for (int c = 0; c < cracks; c++)
            {
                System.Random rnd = new System.Random(2000 + c * 37);
                float x = size * (0.25f + (float)rnd.NextDouble() * 0.5f), y = size * (0.25f + (float)rnd.NextDouble() * 0.5f);
                float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
                for (int sIdx = 0; sIdx < 12; sIdx++)
                {
                    angle += ((float)rnd.NextDouble() - 0.5f) * 1.2f;
                    float step = 3f + (float)rnd.NextDouble() * 5f;
                    float nx = x + Mathf.Cos(angle) * step, ny = y + Mathf.Sin(angle) * step;
                    if (sIdx < segments) DrawLine(px, size, x, y, nx, ny, level >= maxLevel ? 2 : 1, 1f, 0.04f);
                    x = nx; y = ny;
                }
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "OreSurface" };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        oreTextures[key] = tex;
        return tex;
    }


    // ------------------------------------------------------------------
    // Surface texture: black with white streaks, plus cracks that grow with damage
    // ------------------------------------------------------------------

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
}
