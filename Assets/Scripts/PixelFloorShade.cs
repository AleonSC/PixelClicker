// ---- System notes ----
// PixelFloorShade.cs
//   Part of PixelFloor (partial class): drawing the floor textures (BuildTexture, Fill), Shade with its pattern groups (ShadeGrids / Stone / Tiles / Shiny / Arenas / Animated, then ShadeExtra in PixelFloorScenes.cs), the Pac-Man floor and the pattern helpers. The system notes are in PixelFloor.cs.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelNoise;

public partial class PixelFloor
{
    // ------------------------------------------------------------------
    // Drawing the textures
    // ------------------------------------------------------------------

    private static Texture2D BuildTexture(FloorStyle s, int step, bool deferFill = false)
    {
        int n = Mathf.Clamp(s.resolution, 8, 1024);
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, deferFill && !IsOverlayPattern(s.pattern) && !s.pixelated)   // (big still pictures get mipmaps so far-away ground doesn't shimmer)
        {
            name = "Floor " + s.name,
            wrapMode = s.stretchToFloor || s.fitToView ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,   // a fitted picture is ONE copy: never repeated over the floor
            filterMode = s.pixelated ? FilterMode.Point : FilterMode.Bilinear,
            anisoLevel = 4
        };
        if (s.pattern == FloorPattern.Disco) { DrawDisco(tex, s, step); return tex; }
        if (deferFill) return tex;   // the caller draws it over several frames (UsesSlicedFill)
        Fill(tex, s, 0f, false);
        return tex;
    }

    // Animation time read by the animated patterns while a texture is being drawn.
    private static float shadeTime;
    // Width / height of the area one copy of the texture covers (set from the fitted view) so pixel-art patterns can keep square pixels.
    private static float shadeAspect = 1f;
    private float viewAspect = 16f / 9f;
    private PixelCameraIntro intro;
    private Camera fitCam;
    private static Color[] fillBuffer; // reused by animated floors so redrawing doesn't allocate every frame

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { fillBuffer = null; pacPath = null; pacPathIndex = null; shadeTime = 0f; shadeAspect = 1f; }   // (the shaders' state in PixelFloorLayers.cs and the circuit caches in PixelFloorScenes.cs reset themselves)

    /// <summary>Draws the pattern into 'tex' at animation time 'time' (0 for still patterns). 'reuse' = keep the pixel buffer for the next frame.</summary>
    private static void Fill(Texture2D tex, FloorStyle s, float time, bool reuse)
    {
        int n = tex.width;
        if (IsOverlayPattern(s.pattern) && layerCur32 != null && layerN == n) { OverlayFrame(tex, s, time); return; }   // only the moving parts are repainted
        shadeBase = false;
        Color[] px;
        if (reuse)
        {
            if (fillBuffer == null || fillBuffer.Length != n * n) fillBuffer = new Color[n * n];
            px = fillBuffer;
        }
        else px = new Color[n * n];
        int cells = Mathf.Max(1, s.cells);
        BeginShade(s, time);
        if (s.pattern == FloorPattern.CircuitFlow) PrepareSparks(s, time);   // and the wandering sparks

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                Color c = Shade(s, u, v, x, y, n, cells);
                c.a = 1f;
                px[y * n + x] = c;
            }
        }

        // Scattered extras that are easier to place than to shade per texel.
        System.Random rng = new System.Random(s.seed);
        if (s.pattern == FloorPattern.Space) AddStars(px, n, s, rng);
        if (s.pattern == FloorPattern.PixelGrass) AddFlowers(px, n, s, rng);

        tex.SetPixels(px);
        tex.Apply(false);
    }

    /// <summary>
    /// A security-laser corridor: pale teal metal panels with glowing light strips along the seams, and thin red beams that sway across the floor in both directions,
    /// each blinking on and off, with white-hot spots where two cross. 'cells' = beams per direction (the texture tiles).
    /// </summary>
    private static Color LaserHall(FloorStyle s, float u, float v, int n, int cells)
    {
        float t = shadeTime;
        // Panels: brushed metal with dark seams and a lit strip of lights beside each seam.
        int panels = Mathf.Max(2, cells / 2);
        float pu = Frac(u * panels), pv = Frac(v * panels);
        float seam = Mathf.Min(Mathf.Min(pu, 1f - pu), Mathf.Min(pv, 1f - pv)) * n / panels;     // texels from the nearest seam
        Color c = s.colorA * (0.82f + 0.25f * Fbm(u, v, 6, 3, s.seed) + 0.05f * Mathf.Sin(v * n * 0.8f));
        if (seam < 1.2f) c = s.colorA * 0.3f;                                                    // the seam itself
        else if (seam < 3.2f && pu > 0.5f) c = Color.Lerp(c, s.colorB, 0.35f);                    // a glass edge catching the light
        // Wall-style light dashes along the vertical seams.
        float dash = Mathf.Repeat(v * panels * 6f, 1f);
        if (pu > 0.93f && dash < 0.45f) c = Color.Lerp(c, s.colorB, 0.9f);

        // Lasers.
        float[] acc = new float[2];
        for (int axis = 0; axis < 2; axis++)
        {
            float coord = axis == 0 ? v : u;
            for (int i = 0; i < cells; i++)
            {
                float centre = (i + 0.5f + 0.32f * Mathf.Sin(t * (0.8f + 0.11f * i) + i * 1.9f + axis * 2.4f)) / cells;
                int slot = Mathf.FloorToInt(t * 1.4f + i * 0.37f + axis * 3f);
                if (Hash(i * 7 + axis, slot, s.seed) <= 0.28f) continue;                         // this beam is blinking off
                float d = Mathf.Abs(Mathf.Repeat(coord - centre + 0.5f, 1f) - 0.5f) * n;           // texels from the beam
                float beam = Mathf.Clamp01(Mathf.Clamp01(1.3f - d) + Mathf.Exp(-d * 0.55f) * 0.55f);
                if (beam > acc[axis]) acc[axis] = beam;
            }
        }
        float best = Mathf.Max(acc[0], acc[1]);
        float hot = Mathf.Min(acc[0], acc[1]);                                                    // white-hot where two beams cross
        Color laser = s.colorC;
        c = Color.Lerp(c, laser, Mathf.Clamp01(best));
        c = Color.Lerp(c, Color.white, Mathf.Clamp01(hot) * 0.85f);
        c.a = 1f;
        return c;
    }

    private static Color HueShift(Color c, float shift)
    {
        Color.RGBToHSV(c, out float h, out float sat, out float val);
        Color r = Color.HSVToRGB(Mathf.Repeat(h + shift, 1f), sat, val);
        r.a = 1f;
        return r;
    }

    /// <summary>Strength (0..1) of one grid line in the 'merge' variant: every second line fades out and back, then every fourth, so the squares grow and shrink.</summary>
    private static float MergeStrength(int k, float phase)
    {
        float a = Sstep(0.12f, 0.45f, phase), b = Sstep(0.55f, 0.88f, phase);
        int m = ((k % 4) + 4) % 4;
        if (m == 0) return 1f;
        if (m == 2) return 1f - b;
        return 1f - a;
    }

    private static float NeonLine(float texels) { return Mathf.Clamp01(Mathf.Clamp01(1.5f - texels) + Mathf.Exp(-texels * 0.45f) * 0.55f); }

    /// <summary>The four animated neon grids (they move and change colour over time; 'cells' should be divisible by 4 so the texture still tiles).</summary>
    private static Color NeonAnimated(FloorStyle s, float u, float v, int n, int cells)
    {
        float t = shadeTime;
        float hue = 0f;
        float phase = 0.5f + 0.5f * Mathf.Sin(t * 0.45f);
        float gu = u, gv = v;
        switch (s.pattern)
        {
            case FloorPattern.NeonDrift:
                gu = u + t * 0.045f; gv = v + t * 0.028f;                       // the whole grid slides
                hue = t * 0.05f;
                break;
            case FloorPattern.NeonMerge:
                gu = u + t * 0.01f; gv = v - t * 0.008f;
                hue = t * 0.035f;
                break;
            case FloorPattern.NeonWave:
                gu = u + 0.022f * Mathf.Sin((v * 2f + t * 0.35f) * Mathf.PI * 2f);   // lines ripple like cloth
                gv = v + 0.022f * Mathf.Sin((u * 2f - t * 0.3f) * Mathf.PI * 2f);
                hue = t * 0.045f;
                break;
            default: // NeonPrism
                gu = u + t * 0.02f; gv = v;
                hue = Mathf.Repeat((u + v) * 1f - t * 0.12f, 1f);                    // a rainbow sweeping diagonally
                break;
        }
        Color lineColor = HueShift(s.colorB, hue), crossColor = HueShift(s.colorC, hue + 0.12f);
        Color ground = s.colorA * (0.85f + 0.15f * Fbm(u, v, 4, 3, s.seed));

        float fu = Frac(gu * cells), fv = Frac(gv * cells);
        int ku = Mathf.FloorToInt(gu * cells), kv = Mathf.FloorToInt(gv * cells);
        float bestU = 0f, bestV = 0f, nearU = 99f, nearV = 99f, strU = 1f, strV = 1f;
        for (int side = 0; side < 2; side++)
        {
            float du = side == 0 ? fu : 1f - fu, dv = side == 0 ? fv : 1f - fv;
            float stU = s.pattern == FloorPattern.NeonMerge ? MergeStrength(ku + side, phase) : 1f;
            float stV = s.pattern == FloorPattern.NeonMerge ? MergeStrength(kv + side, phase) : 1f;
            float iu = NeonLine(du * n / cells) * stU, iv = NeonLine(dv * n / cells) * stV;
            if (iu > bestU) { bestU = iu; nearU = du * n / cells; strU = stU; }
            if (iv > bestV) { bestV = iv; nearV = dv * n / cells; strV = stV; }
        }
        float line = Mathf.Max(bestU, bestV);
        Color c = Color.Lerp(ground, lineColor, line);
        float cross = Mathf.Clamp01(2.5f - Mathf.Max(nearU, nearV)) * Mathf.Min(strU, strV);
        return Color.Lerp(c, crossColor, cross * 0.8f);
    }

    /// <summary>The Aura floors' pixel. PURE (reads only its arguments and the style, no static state), so the rows can be drawn on several threads at once (PixelFloorLayers.FillSliced).</summary>
    private static Color ShadeKiAura(FloorStyle s, float u, float v, int x, int y, int D)
    {
        // Tall streaks (stretched noise) that scroll upward like flames; the lattices are scaled up so the streaks keep their width on a big texture.
        float warp = Fbm(u, v, 3 * D, 3, s.seed) * 0.15f;
        float flame = Fbm(u + warp, v, 10 * D, 2 * D, 3, s.seed + 1);
        float flicker = Fbm(u, v, 4 * D, 4 * D, 3, s.seed + 2);
        float k = Mathf.Clamp01((flame * 0.75f + flicker * 0.35f - 0.35f) * 2f);
        k = k * k;
        Color c = Color.Lerp(s.colorA, s.colorB, Mathf.Clamp01(k * 1.6f));
        c = Color.Lerp(c, s.colorC, Mathf.Clamp01(k * 2.2f - 1.1f));
        if (Hash(x, y, s.seed + 9) > 0.998f) c = s.colorC;            // sparks
        return c;
    }

    /// <summary>
    /// One texel of a floor pattern. Runs per texel, so it only hands each pattern to its group's method (one switch, no allocations);
    /// patterns not listed here go on to ShadeExtra (PixelFloorScenes.cs) and the later batches.
    /// </summary>
    private static Color Shade(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.NeonGrid:
            case FloorPattern.LaserHall:
            case FloorPattern.NeonDrift:
            case FloorPattern.NeonMerge:
            case FloorPattern.NeonWave:
            case FloorPattern.NeonPrism:
            case FloorPattern.Checker:
            case FloorPattern.PixelGrass:
            case FloorPattern.WoodPlanks:
                return ShadeGrids(s, u, v, x, y, n, cells);

            case FloorPattern.Cobblestone:
            case FloorPattern.Marble:
            case FloorPattern.Lava:
            case FloorPattern.Space:
            case FloorPattern.Ice:
                return ShadeStone(s, u, v, x, y, n, cells);

            case FloorPattern.Honeycomb:
            case FloorPattern.Bricks:
            case FloorPattern.CircuitBoard:
            case FloorPattern.SandDunes:
            case FloorPattern.Ocean:
            case FloorPattern.Snow:
            case FloorPattern.Tartan:
            case FloorPattern.TreadPlate:
                return ShadeTiles(s, u, v, x, y, n, cells);

            case FloorPattern.Hologram:
            case FloorPattern.Crystal:
            case FloorPattern.MatrixRain:
            case FloorPattern.Hazard:
            case FloorPattern.TournamentTiles:
            case FloorPattern.DragonRadar:
            case FloorPattern.DragonBalls:
                return ShadeShiny(s, u, v, x, y, n, cells);

            case FloorPattern.KiAura:
            case FloorPattern.Craters:
            case FloorPattern.MoonSurface:
            case FloorPattern.MarsSurface:
            case FloorPattern.GravityRoom:
            case FloorPattern.PowerArena:
            case FloorPattern.CardBack:
                return ShadeArenas(s, u, v, x, y, n, cells);

            case FloorPattern.Liquid:
            case FloorPattern.CloudSea:
            case FloorPattern.Kaleidoscope:
            case FloorPattern.PacMan:
            case FloorPattern.EnergyWisps:
                return ShadeAnimated(s, u, v, x, y, n, cells);
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    /// <summary>Part of Shade (one group of floor patterns, same pixel maths): NeonGrid to WoodPlanks (9 patterns).</summary>
    private static Color ShadeGrids(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.NeonGrid:
            {
                float cu = Frac(u * cells), cv = Frac(v * cells);
                float du = Mathf.Min(cu, 1f - cu), dv = Mathf.Min(cv, 1f - cv);
                float line = Mathf.Min(du, dv) * n / cells;               // texels from the nearest line
                float core = Mathf.Clamp01(1.5f - line);                  // sharp line
                float halo = Mathf.Exp(-line * 0.45f) * 0.55f;            // soft glow around it
                Color c = s.colorA * (0.8f + 0.2f * Fbm(u, v, 4, 3, s.seed));
                c = Color.Lerp(c, s.colorB, Mathf.Clamp01(core + halo));
                // Brighter dots where lines cross.
                float cross = Mathf.Clamp01(2.5f - Mathf.Max(du, dv) * n / cells);
                return Color.Lerp(c, s.colorC, cross * 0.8f);
            }

            case FloorPattern.LaserHall:
                return LaserHall(s, u, v, n, cells);

            case FloorPattern.NeonDrift:
            case FloorPattern.NeonMerge:
            case FloorPattern.NeonWave:
            case FloorPattern.NeonPrism:
                return NeonAnimated(s, u, v, n, cells);

            case FloorPattern.Checker:
            {
                int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
                Color c = ((cx + cy) & 1) == 0 ? s.colorA : s.colorB;
                // A light bevel on the top / left edge of each square for a tiled look.
                float cu = Frac(u * cells), cv = Frac(v * cells);
                if (cu < 0.08f || cv > 0.92f) c = Color.Lerp(c, Color.white, 0.12f);
                else if (cu > 0.92f || cv < 0.08f) c = Color.Lerp(c, Color.black, 0.15f);
                return c;
            }

            case FloorPattern.PixelGrass:
            {
                float h = Hash(x, y, s.seed);
                float patch = Fbm(u, v, 4, 3, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, patch);
                if (h > 0.86f) c = Color.Lerp(c, Color.white, 0.18f); // light blades
                else if (h < 0.12f) c = Color.Lerp(c, Color.black, 0.2f); // shadows
                return c;
            }

            case FloorPattern.WoodPlanks:
            {
                int rows = cells;
                int row = Mathf.FloorToInt(v * rows);
                float rv = Frac(v * rows);
                // Two planks per row; each row is shifted so the seams don't line up.
                float shift = Hash(row, 7, s.seed);
                float pu = Frac(u * 2f + shift);
                int plankInRow = ((Mathf.FloorToInt(u * 2f + shift) % 2) + 2) % 2;   // 2 planks per row: the plank at the right edge continues at the left, so the texture tiles
                int plank = plankInRow + row * 2;
                float tone = 0.85f + 0.3f * Hash(plank, 3, s.seed);
                float grain = Mathf.Sin((rv * 6f + Fbm(u, v, 2, 4, s.seed + plank) * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(s.colorA, s.colorB, grain * 0.6f) * tone;
                float texel = 1f / n * rows;
                if (rv < texel || pu < 1f / n * 2f) c = s.colorC;     // gaps between planks
                else if (rv > 1f - texel) c = Color.Lerp(c, Color.white, 0.15f);
                // A knot now and then.
                float knot = Hash(plank, 11, s.seed);
                if (knot > 0.7f)
                {
                    float kd = Mathf.Abs(pu - knot) * 3f + Mathf.Abs(rv - 0.5f);
                    if (kd < 0.18f) c = Color.Lerp(c, s.colorC, 0.6f);
                }
                c.a = 1f;
                return c;
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    /// <summary>Part of Shade (one group of floor patterns, same pixel maths): Cobblestone to Ice (5 patterns).</summary>
    private static Color ShadeStone(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.Cobblestone:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = f2 - f1;
                float tone = 0.75f + 0.4f * HashId(id, s.seed);
                Color stone = Color.Lerp(s.colorA, s.colorB, HashId(id, s.seed + 5)) * tone;
                stone *= 0.85f + 0.3f * Fbm(u, v, 8, 3, s.seed);
                float gap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge * cells - 0.06f) / 0.12f));
                // Rounded: darker towards the stone's edge.
                float round = Mathf.Clamp01(edge * cells * 2.2f);
                Color c = Color.Lerp(s.colorC, stone * (0.7f + 0.3f * round), gap);
                c.a = 1f;
                return c;
            }

            case FloorPattern.Marble:
            {
                int D = shadeDetail;
                float turb = Fbm(u, v, 3 * D, 5, s.seed);                                   // slow swirls over the whole floor
                float veins = Mathf.Abs(Mathf.Sin((u * cells + v * cells + turb * 5f) * Mathf.PI));
                float fine = Mathf.Abs(Mathf.Sin((v * cells * 2f - u * cells + Fbm(u, v, 6 * D, 4, s.seed + 3) * 6f) * Mathf.PI));
                Color c = s.colorA;
                c = Color.Lerp(s.colorB, c, Mathf.Pow(veins, 0.35f));
                c = Color.Lerp(s.colorC, c, Mathf.Clamp01(Mathf.Pow(fine, 0.18f) + 0.15f));
                c.a = 1f;
                return c;
            }
            case FloorPattern.Lava:
            {
                int D = shadeDetail;
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float heat = Mathf.Clamp01(1f - edge / 0.35f);
                heat = heat * heat;
                float n2 = Fbm(u, v, 4 * D, 4, s.seed);
                Color crust = s.colorA * (0.6f + 0.6f * n2);
                Color glow = Color.Lerp(s.colorB, s.colorC, Mathf.Clamp01(heat * 1.4f - 0.3f));
                Color c = Color.Lerp(crust, glow, Mathf.Clamp01(heat + (n2 - 0.6f) * 0.5f));
                c.a = 1f;
                return c;
            }
            case FloorPattern.Space:
            {
                int D = shadeDetail;
                float neb = Fbm(u, v, 3 * D, 5, s.seed);
                float neb2 = Fbm(u + 0.37f, v + 0.11f, 4 * D, 5, s.seed + 9);
                Color c = s.colorA;
                c = Color.Lerp(c, s.colorB, Mathf.Clamp01((neb - 0.45f) * 1.6f) * 0.7f);
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01((neb2 - 0.55f) * 1.8f) * 0.55f);
                c.a = 1f;
                return c;
            }
            case FloorPattern.Ice:
            {
                int D = shadeDetail;
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float crack = Mathf.Clamp01(1f - edge / 0.06f);
                Voronoi(u, v, cells * 3, s.seed + 1, out float g1, out float g2, out _);
                float small = Mathf.Clamp01(1f - (g2 - g1) * cells * 3 / 0.05f) * 0.45f;
                float frost = Fbm(u, v, 4 * D, 4, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, frost * 0.6f + HashId(id, s.seed) * 0.25f);
                c = Color.Lerp(c, s.colorC, Mathf.Max(crack, small));
                c.a = 1f;
                return c;
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    /// <summary>Part of Shade (one group of floor patterns, same pixel maths): Honeycomb to TreadPlate (8 patterns).</summary>
    private static Color ShadeTiles(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.Honeycomb:
            {
                HexCells(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;                         // 0 at the cell walls
                float wall = Mathf.Clamp01(1f - edge / 0.12f);
                float depth = Mathf.Clamp01(f1 * cells * 1.6f);          // darker towards each cell's middle (deep honey)
                Color honey = Color.Lerp(s.colorA, s.colorB, depth * 0.7f + HashId(id, s.seed) * 0.2f);
                honey = Color.Lerp(honey, Color.white, Mathf.Clamp01(0.35f - f1 * cells) * 0.5f); // glossy highlight
                return Color.Lerp(honey, s.colorC, wall);
            }

            case FloorPattern.Bricks:
            {
                int rows = cells;
                float gy = v * rows;
                int row = Mathf.FloorToInt(gy);
                float gx = u * rows * 0.5f + ((row & 1) == 0 ? 0f : 0.5f);  // bricks are twice as long as tall
                int col = Mathf.FloorToInt(gx);
                float bu = Frac(gx), bv = Frac(gy);
                float mortarU = 1.5f / n * rows * 0.5f, mortarV = 1.5f / n * rows;
                if (bu < mortarU || bv < mortarV) return s.colorC * (0.9f + 0.2f * Hash(x, y, s.seed));
                float tone = 0.8f + 0.35f * Hash(col, row, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, Hash(col * 3, row * 5, s.seed + 1)) * tone;
                c *= 0.88f + 0.24f * Hash(x, y, s.seed + 2);             // speckle
                if (bv > 1f - mortarV * 1.5f) c = Color.Lerp(c, Color.white, 0.12f); // lit top edge
                return c;
            }

            case FloorPattern.CircuitBoard:
            {
                float gx = u * cells - 0.5f, gy = v * cells - 0.5f;      // pads sit on whole numbers
                float w = 0.09f;
                Color c = s.colorA * (0.85f + 0.25f * Fbm(u, v, 8, 3, s.seed));
                int r = Mathf.RoundToInt(gy), cx = Mathf.FloorToInt(gx);
                int q = Mathf.RoundToInt(gx), cy = Mathf.FloorToInt(gy);
                bool trace = (Mathf.Abs(gy - r) < w && Seg(cx, r, cells, s.seed)) ||
                             (Mathf.Abs(gx - q) < w && Seg(q, cy, cells, s.seed + 50));
                // Pads on joints that have a trace.
                float pd = Vector2.Distance(new Vector2(gx, gy), new Vector2(q, r));
                bool joint = Seg(q, r, cells, s.seed) || Seg(q - 1, r, cells, s.seed) || Seg(q, r, cells, s.seed + 50) || Seg(q, r - 1, cells, s.seed + 50);
                if (trace) c = s.colorB * (0.85f + 0.15f * Hash(x, y, s.seed));
                if (joint && pd < 0.22f) c = pd < 0.1f ? s.colorA * 0.4f : s.colorB * 1.1f; // ring pad with a hole
                // A chip now and then, on top of everything.
                int chipX = Mathf.FloorToInt(gx + 0.5f), chipY = Mathf.FloorToInt(gy + 0.5f);
                if (Hash(Wrap(chipX, cells), Wrap(chipY, cells), s.seed + 9) > 0.86f)
                {
                    float lu = gx + 0.5f - chipX, lv = gy + 0.5f - chipY;
                    if (lu > 0.18f && lu < 0.82f && lv > 0.25f && lv < 0.75f) c = s.colorC * (0.9f + 0.2f * Hash(x, y, s.seed + 3));
                    else if (lu > 0.22f && lu < 0.78f && (lv > 0.18f && lv < 0.25f || lv > 0.75f && lv < 0.82f) && Frac(lu * 10f) < 0.5f)
                        c = new Color(0.8f, 0.8f, 0.82f); // pins
                }
                return c;
            }

            case FloorPattern.SandDunes:
            {
                float warp = Fbm(u, v, 3, 3, s.seed) * 2.5f;
                float t = Frac(v * cells + warp + Mathf.Sin(u * Mathf.PI * 2f * 2f) * 0.3f);
                float ripple = t < 0.7f ? t / 0.7f : (1f - t) / 0.3f;    // gentle slope up, steep slope down
                Color c = Color.Lerp(s.colorB, s.colorA, ripple);
                c *= 0.92f + 0.16f * Hash(x, y, s.seed);                 // grains
                if (Hash(x, y, s.seed + 7) > 0.995f) c = Color.Lerp(c, s.colorC, 0.7f);
                return c;
            }

            case FloorPattern.Ocean:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out _);
                Voronoi(u + 0.31f, v + 0.17f, cells * 2, s.seed + 4, out float g1, out float g2, out _);
                float caustic = Mathf.Pow(Mathf.Clamp01(1f - (f2 - f1) * cells / 0.25f), 2f)
                              + Mathf.Pow(Mathf.Clamp01(1f - (g2 - g1) * cells * 2 / 0.25f), 2f) * 0.6f;
                float deep = Fbm(u, v, 3, 4, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, deep * 0.7f);
                return Color.Lerp(c, s.colorC, Mathf.Clamp01(caustic) * 0.75f);
            }

            case FloorPattern.Snow:
            {
                float drift = Fbm(u, v, 3, 5, s.seed);
                Color c = Color.Lerp(s.colorB, s.colorA, Mathf.Clamp01(drift * 1.3f - 0.05f));
                float h = Hash(x, y, s.seed);
                if (h > 0.992f) c = s.colorC;                             // sparkles
                else if (h > 0.97f) c = Color.Lerp(c, s.colorC, 0.4f);
                return c;
            }

            case FloorPattern.Tartan:
            {
                // Thread colour along each axis, then a twill weave picks warp or weft per texel.
                Color warp = TartanThread(s, Frac(u * cells));
                Color weft = TartanThread(s, Frac(v * cells));
                bool showWarp = ((x + y) / 2 & 1) == 0;
                Color c = showWarp ? warp : weft;
                return c * (0.9f + 0.1f * Hash(x, y, s.seed));
            }

            case FloorPattern.TreadPlate:
            {
                float brushed = Fbm(u, v, 2, 64, 3, s.seed);             // fine streaks along one direction
                Color c = s.colorA * (0.85f + 0.25f * brushed);
                float cu = Frac(u * cells) - 0.5f, cv = Frac(v * cells) - 0.5f;
                int parity = (Mathf.FloorToInt(u * cells) + Mathf.FloorToInt(v * cells)) & 1;
                float k = 0.7071f;
                float a = parity == 0 ? (cu + cv) * k : (cu - cv) * k;   // along the diamond
                float b = parity == 0 ? (cu - cv) * k : (cu + cv) * k;   // across it
                float d = (a / 0.33f) * (a / 0.33f) + (b / 0.09f) * (b / 0.09f);
                if (d < 1f)
                {
                    float shade = Mathf.Clamp01(0.5f - b / 0.09f * 0.5f);  // lit on one side, shadowed on the other
                    c = Color.Lerp(s.colorB, s.colorC, shade);
                }
                else if (d < 1.4f) c *= 0.75f;                            // small shadow ring
                return c;
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    /// <summary>Part of Shade (one group of floor patterns, same pixel maths): Hologram to DragonBalls (7 patterns).</summary>
    private static Color ShadeShiny(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.Hologram:
            {
                float hue = Frac(u + v + Fbm(u, v, 2, 3, s.seed) * 0.25f);
                Color c = Color.HSVToRGB(hue, 0.55f, 1f);
                c = Color.Lerp(s.colorA, c, 0.8f);
                if ((y % 4) == 0) c *= 0.55f;                               // scan lines
                float grid = Mathf.Min(Frac(u * cells), Frac(v * cells));
                if (grid < 1.2f / n * cells) c = Color.Lerp(c, s.colorC, 0.6f);
                return c;
            }

            case FloorPattern.Crystal:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float facet = Mathf.Clamp01(1f - f1 * cells * 1.2f);      // brighter towards each gem's point
                Color gem = Color.Lerp(s.colorA, s.colorB, HashId(id, s.seed));
                gem *= 0.45f + 0.8f * facet * (0.7f + 0.3f * HashId(id, s.seed + 3));
                float rim = Mathf.Clamp01(1f - edge / 0.05f);
                return Color.Lerp(gem, s.colorC, rim * 0.8f);
            }

            case FloorPattern.MatrixRain:
            {
                int cols = cells;
                int glyph = Mathf.Max(4, n / cols);
                int col = x / glyph, row = y / glyph;
                int gx = x % glyph, gy = y % glyph;
                // Each column has its own trail: brightest at the head, fading upward (a few trails per texture height, so the columns never line up).
                float head = Hash(col, 0, s.seed);
                float trail = Frac(head - v * shadeDetail + Hash(col, 1, s.seed) * 0.3f);
                trail = Mathf.Pow(1f - trail, 2.5f);
                bool lit = gx >= 1 && gx < glyph - 2 && gy >= 1 && gy < glyph - 1 &&
                           Hash(col * 97 + gx, row * 53 + gy, s.seed + 5) > 0.45f;
                Color c = s.colorA;
                if (lit)
                {
                    float b = trail * (0.6f + 0.4f * Hash(col, row, s.seed + 6));
                    c = Color.Lerp(s.colorA, s.colorB, b);
                    if (trail > 0.9f) c = Color.Lerp(c, s.colorC, 0.7f);  // white-hot head
                }
                return c;
            }
            case FloorPattern.Hazard:
            {
                bool stripe = Frac((u + v) * cells) < 0.5f;
                Color c = stripe ? s.colorA : s.colorB;
                float wear = Fbm(u, v, 6, 4, s.seed);
                if (wear > 0.68f) c = Color.Lerp(c, s.colorC, (wear - 0.68f) * 3f);   // scuffed paint
                c *= 0.9f + 0.15f * Hash(x, y, s.seed);
                return c;
            }
            case FloorPattern.TournamentTiles:
            {
                float gx = u * cells, gy = v * cells;
                int tx = Mathf.FloorToInt(gx), ty = Mathf.FloorToInt(gy);
                float lu = Frac(gx), lv = Frac(gy);
                float grout = 1.5f / n * cells;
                if (lu < grout || lv < grout) return s.colorC * (0.85f + 0.3f * Hash(x, y, s.seed));
                float tone = 0.88f + 0.2f * Hash(tx, ty, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 6, 4, s.seed) * 0.6f) * tone;
                c *= 0.93f + 0.12f * Hash(x, y, s.seed + 1);            // grain
                if (lu > 1f - grout * 1.5f || lv > 1f - grout * 1.5f) c *= 0.85f;  // shaded edge
                else if (lu < grout * 2.5f || lv < grout * 2.5f) c = Color.Lerp(c, Color.white, 0.1f); // lit edge
                // Cracks in some tiles.
                if (Hash(Wrap(tx, cells), Wrap(ty, cells), s.seed + 3) > 0.62f)
                {
                    Voronoi(u, v, cells * 3, s.seed + 5, out float f1, out float f2, out _);
                    if ((f2 - f1) * cells * 3 < 0.04f) c = Color.Lerp(c, s.colorC, 0.75f);
                }
                return c;
            }

            case FloorPattern.DragonRadar:
                return ShadeRadar(s, u, v, x, y, n, cells);
            case FloorPattern.DragonBalls:
            {
                // Staggered rows of balls; each ball has 1-7 stars.
                float gy = v * cells;
                int row = Mathf.FloorToInt(gy);
                float gx = u * cells + ((row & 1) == 0 ? 0f : 0.5f);
                int col = Mathf.FloorToInt(gx);
                float lx = Frac(gx) - 0.5f, ly = Frac(gy) - 0.5f;
                float r = Mathf.Sqrt(lx * lx + ly * ly);
                const float R = 0.42f;
                Color bg = s.colorC * (0.85f + 0.2f * Fbm(u, v, 4, 3, s.seed));
                if (r > R) return r < R + 0.05f ? bg * 0.6f : bg;          // soft shadow ring
                float nx = lx / R, ny = ly / R, nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float light = Mathf.Clamp01(nx * -0.45f + ny * 0.55f + nz * 0.7f);
                Color c = Color.Lerp(s.colorB, s.colorA, light);
                // Stars.
                int count = 1 + (int)(Hash(Wrap(col, cells), Wrap(row, cells), s.seed) * 6.999f);
                float sr = count == 1 ? 0.13f : 0.065f;
                for (int i = 0; i < count; i++)
                {
                    float ax = 0f, ay = 0f;
                    if (count > 1 && !(count == 7 && i == 6))
                    {
                        int ring = count == 7 ? 6 : count;
                        float ang = i * Mathf.PI * 2f / ring + 0.3f;
                        ax = Mathf.Cos(ang) * 0.19f; ay = Mathf.Sin(ang) * 0.19f;
                    }
                    float sx = lx - ax, sy = ly - ay;
                    float sd = Mathf.Sqrt(sx * sx + sy * sy);
                    float th = Mathf.Atan2(sy, sx);
                    float edge = sr * (0.5f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * (th - Mathf.PI * 0.5f))), 3f));
                    if (sd < edge) c = new Color(0.85f, 0.08f, 0.05f);
                }
                // Glossy highlight.
                float hx = lx + 0.14f, hy = ly - 0.16f;
                float h = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.1f);
                return Color.Lerp(c, Color.white, h * 0.85f);
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    /// <summary>Part of Shade (one group of floor patterns, same pixel maths): KiAura to CardBack (7 patterns).</summary>
    private static Color ShadeArenas(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.KiAura:
                return ShadeKiAura(s, u, v, x, y, shadeDetail);
            case FloorPattern.Craters:
                return ShadeCraterField(s, u, v, x, y, n);
            case FloorPattern.MoonSurface:
                return ShadeMoon(s, u, v, x, y, n);
            case FloorPattern.MarsSurface:
                return ShadeMars(s, u, v, x, y, n);
            case FloorPattern.GravityRoom:
            {
                float pu = Frac(u * cells), pv = Frac(v * cells);
                float seam = 1.2f / n * cells;
                Color c = s.colorA * (0.88f + 0.15f * Fbm(u, v, 2, 48, 2, s.seed));   // brushed metal
                if (pu < seam || pv < seam) c = s.colorB * 0.6f;
                else if (pu < seam * 2f || pv < seam * 2f) c *= 1.12f;
                // Rivets in the panel corners.
                float rx = Mathf.Min(pu, 1f - pu), ry = Mathf.Min(pv, 1f - pv);
                if (Mathf.Abs(rx - 0.08f) < 0.025f && Mathf.Abs(ry - 0.08f) < 0.025f) c = s.colorB;
                // Glowing ring and markings around the middle of the texture.
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Abs(d - 0.36f);
                if (ring < 0.012f) c = s.colorC;
                else if (ring < 0.03f) c = Color.Lerp(c, s.colorC, (1f - (ring - 0.012f) / 0.018f) * 0.5f);
                if (Mathf.Abs(d - 0.3f) < 0.004f) c = s.colorB * 0.5f;
                float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * 16f;
                if (d > 0.4f && d < 0.44f && Frac(ang) < 0.25f) c = Color.Lerp(c, s.colorC, 0.7f);
                return c;
            }

            case FloorPattern.PowerArena:
            {
                HexCells(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                Color stone = Color.Lerp(s.colorA, s.colorB, HashId(id, s.seed) * 0.6f + Fbm(u, v, 6, 3, s.seed) * 0.4f);
                stone *= 0.9f + 0.15f * Hash(x, y, s.seed);
                stone *= 0.85f + 0.25f * Mathf.Clamp01(edge * 3f);         // plates slope down to the seams
                float glow = Mathf.Clamp01(1f - edge / 0.07f);
                return Color.Lerp(stone, s.colorC, glow);
            }

            case FloorPattern.CardBack:
            {
                // One card per tile, standing upright in the middle with a dark gap around it.
                float px = u - 0.5f, py = v - 0.5f;
                float hw = s.stretchToFloor ? 0.49f : 0.32f, hh = s.stretchToFloor ? 0.49f : 0.46f;
                const float corner = 0.025f;
                float qx = Mathf.Abs(px) - (hw - corner), qy = Mathf.Abs(py) - (hh - corner);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                              + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;               // < 0 inside the rounded card
                if (outside > 0f) return s.colorC * (0.9f + 0.15f * Fbm(u, v, 8, 3, s.seed));
                float inset = -outside;                                                // distance in from the card's edge

                // Frame: dark brown border with a thin gold line inside it.
                float border = s.stretchToFloor ? 0.03f : 0.04f;
                if (inset < border)
                {
                    Color frame = s.colorB * (0.75f + 0.35f * (inset / border));
                    return frame * (0.92f + 0.12f * Hash(x, y, s.seed));
                }
                if (inset < border + (s.stretchToFloor ? 0.004f : 0.006f)) return Color.Lerp(s.colorA, Color.white, 0.25f);

                // The vortex: spiral arms around the centre (squashed to the card's shape).
                float ex = px / hw, ey = py / hh;
                float r = Mathf.Sqrt(ex * ex + ey * ey);
                float ang = Mathf.Atan2(ey, ex);
                float spiral = Mathf.Sin(ang * 3f + Mathf.Log(Mathf.Max(r, 0.02f)) * 7f + Fbm(u, v, 6, 3, s.seed) * 2.5f);
                float arms = Mathf.SmoothStep(0f, 1f, spiral * 0.5f + 0.5f);
                Color dark = s.colorB * 0.55f;
                Color c = Color.Lerp(dark, s.colorA, arms * (0.55f + 0.45f * Mathf.Clamp01(1.2f - r)));
                c = Color.Lerp(c, Color.Lerp(s.colorA, new Color(1f, 0.92f, 0.6f), 0.4f), Mathf.Clamp01(0.45f - r) * 1.6f); // bright core
                c *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((r - 0.6f) * 1.4f));            // darker towards the frame

                // Dark oval in the middle with a glowing rim.
                float ox = px / (hw * 0.34f), oy = py / (hh * 0.16f);
                float o = Mathf.Sqrt(ox * ox + oy * oy);
                if (o < 1f) return Color.Lerp(new Color(0.03f, 0.02f, 0.02f), new Color(0.12f, 0.06f, 0.03f), o * o);
                if (o < 1.25f) c = Color.Lerp(c, Color.Lerp(s.colorA, Color.white, 0.35f), (1.25f - o) / 0.25f * 0.8f);
                return c;
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    /// <summary>Part of Shade (one group of floor patterns, same pixel maths): Liquid to EnergyWisps (5 patterns).</summary>
    private static Color ShadeAnimated(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.Liquid:
            {
                // Warped waves with bright ridges where two wave sets meet (drawn once over a big area; the whole picture drifts).
                int D = shadeDetail;
                float w1 = Fbm(u, v, 3 * D, 3, s.seed);
                float w2 = Fbm(u + w1 * 0.4f, v + w1 * 0.4f, 3 * D, 3, s.seed + 1);
                float a = Mathf.Sin((u * cells + w2 * 3f) * Mathf.PI * 2f);
                float b = Mathf.Sin((v * cells - w1 * 3f) * Mathf.PI * 2f);
                float k = Mathf.Clamp01(0.5f + 0.25f * (a + b));
                Color c = Color.Lerp(s.colorA, s.colorB, k);
                float ridge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(a - b) * 1.6f), 6f);  // thin shiny streaks
                return Color.Lerp(c, s.colorC, ridge * 0.8f);
            }
            case FloorPattern.CloudSea:
            {
                float d = Fbm(u, v, 3, 5, s.seed);
                float d2 = Fbm(u + 0.02f, v - 0.025f, 3, 5, s.seed);                    // a step towards the light
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.42f) * 4f));
                float lit = Mathf.Clamp01(0.6f + (d - d2) * 6f);
                Color puff = Color.Lerp(s.colorB, s.colorC, lit);
                Color c = s.colorA * (0.9f + 0.15f * Fbm(u, v, 6, 3, s.seed + 3));      // sky / sea far below
                c = Color.Lerp(c, s.colorA * 0.75f, Mathf.Clamp01((d2 - 0.42f) * 4f) * (1f - cloud) * 0.6f); // cloud shadows
                return Color.Lerp(c, puff, cloud);
            }

            case FloorPattern.Kaleidoscope:
            {
                float t = shadeTime;
                float px = u - 0.5f, py = v - 0.5f;
                float r = Mathf.Sqrt(px * px + py * py);
                float seg = Mathf.PI * 2f / Mathf.Max(3, cells);
                float ang = Mathf.Atan2(py, px) + t * 0.15f;
                float a = ang - Mathf.Floor(ang / seg) * seg;
                if (a > seg * 0.5f) a = seg - a;                                        // mirror each slice
                float fx = r * Mathf.Cos(a), fy = r * Mathf.Sin(a);
                float val = Mathf.Sin(fx * 18f + t) + Mathf.Sin(fy * 26f - t * 1.3f)
                          + Mathf.Sin((fx + fy) * 14f + t * 0.7f) + Mathf.Sin(r * 30f - t * 2f);
                float hue = Frac(val * 0.12f + t * 0.05f + r * 0.6f);
                float bright = 0.55f + 0.45f * Mathf.Sin(val * 2.2f);
                Color c = Color.HSVToRGB(hue, 0.8f, Mathf.Clamp01(bright));
                float lines = Mathf.Abs(Mathf.Sin(val * 3f));
                if (lines < 0.08f) c = Color.Lerp(c, s.colorC, 0.7f);                  // thin outlines between shapes
                return Color.Lerp(s.colorA, c, Mathf.Clamp01(1.1f - r * 0.4f));
            }

            case FloorPattern.PacMan:
                return ShadePacMan(s, u, v);

            case FloorPattern.EnergyWisps:
            {
                // Domain-warped noise: swirling wisps (lattices scaled up for the big texture).
                int D = shadeDetail;
                float q = Fbm(u, v, 3 * D, 4, s.seed);
                float r = Fbm(u + q * 0.6f, v + q * 0.6f, 3 * D, 4, s.seed + 1);
                float w = Fbm(u + r * 0.8f, v - r * 0.8f, 4 * D, 4, s.seed + 2);
                float k = Mathf.Clamp01((w - 0.45f) * 2.5f);
                k = k * k * (3f - 2f * k);
                Color c = Color.Lerp(s.colorA, s.colorB, k);
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01(k * k * 1.6f - 0.5f));
                if (Hash(x, y, s.seed + 4) > 0.9985f) c = s.colorC;           // sparks
                return c;
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    // ------------------------------------------------------------------
    // Pac-Man floor: a maze, pellets that get eaten as Pac-Man passes, and four ghosts chasing him round a loop
    // ------------------------------------------------------------------

    private static readonly string[] PacMaze =
    {
        "######### #########",
        "#o...............o#",
        "#.##.###.#.###.##.#",
        "#.................#",
        "#.##.#.#####.#.##.#",
        "#....#...#...#....#",
        "####.###.#.###.####",
        "####.#.......#.####",
        "####.#.##-##.#.####",
        "......  #   #......",
        "####.#.#####.#.####",
        "####.#.......#.####",
        "####.#.#####.#.####",
        "#........#........#",
        "#.##.###.#.###.##.#",
        "#o.#...........#.o#",
        "##.#.#.#####.#.#.##",
        "#....#...#...#....#",
        "#.######.#.######.#",
        "#.................#",
        "######### #########",
    };

    // Corners of the loop Pac-Man runs (column, row; rows counted from the top). Every step between them is open.
    private static readonly Vector2Int[] PacWaypoints =
    {
        new Vector2Int(1, 3), new Vector2Int(4, 3), new Vector2Int(4, 13), new Vector2Int(1, 13), new Vector2Int(1, 15),
        new Vector2Int(2, 15), new Vector2Int(2, 17), new Vector2Int(1, 17), new Vector2Int(1, 19), new Vector2Int(17, 19),
        new Vector2Int(17, 17), new Vector2Int(16, 17), new Vector2Int(16, 15), new Vector2Int(17, 15), new Vector2Int(17, 13),
        new Vector2Int(14, 13), new Vector2Int(14, 3), new Vector2Int(17, 3), new Vector2Int(17, 1), new Vector2Int(10, 1),
        new Vector2Int(10, 3), new Vector2Int(8, 3), new Vector2Int(8, 1), new Vector2Int(1, 1),
    };

    private static List<Vector2Int> pacPath;
    private static int[] pacPathIndex; // per maze cell: where it is on the loop (-1 = not on it)

    private static readonly Color[] GhostColors =
    {
        new Color(1f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.85f), new Color(0.2f, 1f, 1f), new Color(1f, 0.65f, 0.2f),
    };

    private static void BuildPacPath()
    {
        if (pacPath != null) return;
        int w = PacMaze[0].Length, h = PacMaze.Length;
        pacPath = new List<Vector2Int>();
        pacPathIndex = new int[w * h];
        for (int i = 0; i < pacPathIndex.Length; i++) pacPathIndex[i] = -1;
        for (int i = 0; i < PacWaypoints.Length; i++)
        {
            Vector2Int a = PacWaypoints[i], b = PacWaypoints[(i + 1) % PacWaypoints.Length];
            Vector2Int step = new Vector2Int(Math.Sign(b.x - a.x), Math.Sign(b.y - a.y));
            for (Vector2Int c = a; c != b; c += step)
            {
                if (pacPathIndex[c.y * w + c.x] < 0) pacPathIndex[c.y * w + c.x] = pacPath.Count;
                pacPath.Add(c);
            }
        }
    }

    /// <summary>Position (cell units, centre of the cell) and heading at 'progress' cells along the loop.</summary>
    private static Vector2 PacAt(float progress, out Vector2 dir)
    {
        int count = pacPath.Count;
        progress = Mathf.Repeat(progress, count);
        int i = Mathf.FloorToInt(progress);
        Vector2Int a = pacPath[i], b = pacPath[(i + 1) % count];
        dir = new Vector2(b.x - a.x, b.y - a.y);
        float f = progress - i;
        return new Vector2(a.x + 0.5f + dir.x * f, a.y + 0.5f + dir.y * f);
    }

    private static bool PacWall(int c, int r)
    {
        int w = PacMaze[0].Length, h = PacMaze.Length;
        return PacMaze[Wrap(r, h)][Wrap(c, w)] == '#'; // wraps: the maze repeats across the floor
    }

    private static Color ShadePacMan(FloorStyle s, float u, float v)
    {
        BuildPacPath();
        int w = PacMaze[0].Length, h = PacMaze.Length;
        float t = shadeTime;
        // The maze fills the texture (the style's tileAspect makes its cells square on the floor); rows run from the top.
        // Its edges line up with the next copy, so the tunnels on all four sides lead into the neighbouring mazes.
        float gx = u * w, gy = (1f - v) * h;
        int ci = Mathf.Clamp(Mathf.FloorToInt(gx), 0, w - 1), ri = Mathf.Clamp(Mathf.FloorToInt(gy), 0, h - 1);
        float lx = gx - ci, ly = gy - ri;
        char cell = PacMaze[ri][ci];

        // Characters first (they are drawn over everything).
        const float speed = 5f;                         // cells per second
        float progress = t * speed;
        Vector2 here = new Vector2(gx, gy);

        for (int g = 0; g < GhostColors.Length; g++)
        {
            Vector2 gp = PacAt(progress - 5f - g * 4f, out Vector2 gd);
            Vector2 d = here - gp;
            if (Mathf.Abs(d.x) > 0.5f || Mathf.Abs(d.y) > 0.5f) continue;
            const float R = 0.42f;
            bool body = d.y < 0f ? d.magnitude < R : (Mathf.Abs(d.x) < R && d.y < R);
            // Wavy skirt along the bottom.
            if (body && d.y > R - 0.12f && Mathf.Sin((d.x + t * 0.8f) * 30f) > 0f) body = false;
            if (!body) continue;
            for (int e = -1; e <= 1; e += 2)
            {
                Vector2 eye = d - new Vector2(e * 0.15f, -0.08f);
                if ((eye - gd * 0.05f).magnitude < 0.05f) return new Color(0.1f, 0.2f, 0.9f);  // pupil looks where it goes
                if (eye.magnitude < 0.11f) return Color.white;
            }
            return GhostColors[g];
        }

        Vector2 pp = PacAt(progress, out Vector2 pd);
        Vector2 pdl = here - pp;
        if (pdl.magnitude < 0.45f)
        {
            float mouth = 0.75f * Mathf.Abs(Mathf.Sin(t * 12f));          // half-angle of the open mouth (radians)
            float facing = Vector2.Angle(pdl, pd) * Mathf.Deg2Rad;
            if (pdl.magnitude < 0.06f || facing > mouth) return new Color(1f, 0.93f, 0.1f);
        }

        // Maze.
        if (cell == '#')
        {
            // A blue line along each side that faces a corridor.
            const float lo = 0.12f, hi = 0.24f;
            bool line =
                (!PacWall(ci - 1, ri) && lx >= lo && lx <= hi) ||
                (!PacWall(ci + 1, ri) && lx <= 1f - lo && lx >= 1f - hi) ||
                (!PacWall(ci, ri - 1) && ly >= lo && ly <= hi) ||
                (!PacWall(ci, ri + 1) && ly <= 1f - lo && ly >= 1f - hi);
            return line ? s.colorA : s.colorC;
        }
        if (cell == '-') return Mathf.Abs(ly - 0.5f) < 0.08f ? new Color(1f, 0.7f, 0.85f) : s.colorC;

        // Pellets: hidden once Pac-Man has passed them this lap.
        if (cell == '.' || cell == 'o')
        {
            int at = pacPathIndex[ri * w + ci];
            float lap = Mathf.Repeat(progress, pacPath.Count);
            bool eaten = at >= 0 && at <= lap;
            if (!eaten)
            {
                float dd = new Vector2(lx - 0.5f, ly - 0.5f).magnitude;
                if (cell == '.' && dd < 0.11f) return s.colorB;
                if (cell == 'o' && dd < 0.3f && Frac(t * 2f) < 0.6f) return s.colorB;  // blinking power pellet
            }
        }
        return s.colorC;
    }

    private static int Wrap(int i, int m) => ((i % m) + m) % m;

    /// <summary>Circuit board: is there a trace from joint (i, j) to the next joint along? (wraps, so it tiles)</summary>
    private static bool Seg(int i, int j, int cells, int seed) => Hash(Wrap(i, cells), Wrap(j, cells), seed) > 0.55f;

    /// <summary>Tartan: the thread colour at position t (0..1) across one repeat.</summary>
    private static Color TartanThread(FloorStyle s, float t)
    {
        if (t < 0.38f) return s.colorA;
        if (t < 0.46f) return s.colorB;
        if (t < 0.5f) return s.colorC;
        if (t < 0.54f) return s.colorB;
        if (t < 0.7f) return s.colorA * 0.75f;
        if (t < 0.74f) return Color.Lerp(s.colorC, Color.white, 0.5f);
        return s.colorB * 0.8f;
    }

    /// <summary>Hexagon-like cells: nearest point on a staggered grid (odd rows shifted half a cell). Tiles for even 'cells'.</summary>
    private static void HexCells(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
    {
        cells = Mathf.Max(2, cells + (cells & 1));
        float x = Frac(u) * cells, y = Frac(v) * cells * 0.866f;
        int rows = cells;
        int cy = Mathf.FloorToInt(y / 0.866f);
        f1 = f2 = 99f;
        id = 0;
        for (int oy = -1; oy <= 1; oy++)
        {
            int gy = cy + oy;
            float shift = (Wrap(gy, rows) & 1) == 0 ? 0f : 0.5f;
            int cx = Mathf.FloorToInt(x - shift);
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox;
                float px = gx + 0.5f + shift, py = (gy + 0.5f) * 0.866f;
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = Wrap(gy, rows) * cells + Wrap(gx, cells); }
                else if (d < f2) f2 = d;
            }
        }
        f1 /= cells;
        f2 /= cells;
    }

    private static void AddStars(Color[] px, int n, FloorStyle s, System.Random rng)
    {
        int count = n * n / 90;
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(n), y = rng.Next(n);
            float b = (float)rng.NextDouble();
            Color star = Color.Lerp(Color.white, s.colorC, (float)rng.NextDouble() * 0.4f);
            Blend(px, n, x, y, star, 0.4f + 0.6f * b);
            if (b > 0.93f) // a few bright ones with a small cross
            {
                Blend(px, n, x + 1, y, star, 0.5f); Blend(px, n, x - 1, y, star, 0.5f);
                Blend(px, n, x, y + 1, star, 0.5f); Blend(px, n, x, y - 1, star, 0.5f);
            }
        }
    }

    private static void AddFlowers(Color[] px, int n, FloorStyle s, System.Random rng)
    {
        int count = Mathf.Max(1, n * n / 300);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(n), y = rng.Next(n);
            Color petal = rng.NextDouble() < 0.5 ? s.colorC : new Color(1f, 1f, 1f, 1f);
            Blend(px, n, x + 1, y, petal, 1f); Blend(px, n, x - 1, y, petal, 1f);
            Blend(px, n, x, y + 1, petal, 1f); Blend(px, n, x, y - 1, petal, 1f);
            Blend(px, n, x, y, new Color(1f, 0.85f, 0.2f, 1f), 1f);
        }
    }

    /// <summary>Disco: a grid of tiles, each lit in a random bright colour that changes every beat.</summary>
    private static void DrawDisco(Texture2D tex, FloorStyle s, int step)
    {
        if (tex == null) return;
        int n = tex.width;
        int cells = Mathf.Max(1, s.cells);
        Color32[] px = new Color32[n * n];
        Color32 gap = s.colorA;
        for (int y = 0; y < n; y++)
        {
            int cy = y * cells / n;
            int ly = y - cy * n / cells;
            for (int x = 0; x < n; x++)
            {
                int cx = x * cells / n;
                int lx = x - cx * n / cells;
                int size = n / cells;
                bool border = lx == 0 || ly == 0 || lx == size - 1 || ly == size - 1;
                if (border) { px[y * n + x] = gap; continue; }
                float h = Hash(cx + step * 31, cy + step * 17, s.seed);
                bool lit = Hash(cx * 3 + step, cy * 5 - step, s.seed + 2) > 0.35f;
                Color c = Color.HSVToRGB(h, 0.85f, lit ? 1f : 0.18f);
                // A lighter centre on each tile so it reads as a lamp.
                float dx = (lx + 0.5f) / size - 0.5f, dy = (ly + 0.5f) / size - 0.5f;
                float centre = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                c = Color.Lerp(c, Color.white, lit ? centre * 0.35f : 0f);
                c.a = 1f;
                px[y * n + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
    }

    private static void Blend(Color[] px, int n, int x, int y, Color c, float k)
    {
        x = ((x % n) + n) % n; y = ((y % n) + n) % n;
        int i = y * n + x;
        px[i] = Color.Lerp(px[i], c, k);
    }
}
