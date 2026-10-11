using System.Collections.Generic;
using UnityEngine;
using static PixelNoise;

/// <summary>
/// The third batch of floor patterns (part of <see cref="PixelFloor"/>): retrowave / cyberpunk scenes and 8-bit game scenes. All drawn per texel in code
/// like the others; they are animated (unscaled time) and, like the Road Racer, fit one copy to the view and use square virtual pixels so the sprites
/// are not stretched. The 8-bit ones use a virtual picture <c>P</c> rows tall (x counted from the middle of the picture).
/// </summary>
public partial class PixelFloor
{
    private static Color ShadeRetro(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.SynthSun: return ShadeSynthSun(s, u, v);
            case FloorPattern.NeonHighway: return ShadeNeonHighway(s, u, v);
            case FloorPattern.GridTunnel: return ShadeGridTunnel(s, u, v);
            case FloorPattern.RainAlley: return ShadeRainAlley(s, u, v, x, y, cells);
            case FloorPattern.VaporChecker: return ShadeVaporChecker(s, u, v, x, y);
            case FloorPattern.CityMap: return ShadeCityMap(s, u, v, cells);
            case FloorPattern.LightCycles: return ShadeLightCycles(s, u, v, n, cells);
            case FloorPattern.ArcadeCarpet: return ShadeArcadeCarpet(s, u, v, cells);
            case FloorPattern.Platformer: return ShadePlatformer(s, u, v);
            case FloorPattern.Invaders: return ShadeInvaders(s, u, v);
            case FloorPattern.BrickBreaker: return ShadeBrickBreaker(s, u, v);
            case FloorPattern.PongCourt: return ShadePongCourt(s, u, v);
            case FloorPattern.SnakeGrid: return ShadeSnakeGrid(s, u, v);
            case FloorPattern.BlockStack: return ShadeBlockStack(s, u, v);
            case FloorPattern.Dungeon: return ShadeDungeon(s, u, v);
            case FloorPattern.Crossing: return ShadeCrossing(s, u, v);
        }
        return s.colorA;
    }

    // ---- helpers ----

    /// <summary>The third batch: retrowave / cyberpunk looks and 8-bit game scenes.</summary>
    private static IEnumerable<FloorStyle> RetroStyles()
    {
        FloorStyle Neo(string name, FloorPattern p, Color a, Color b, Color c, int res, float tile, int cells, float fps, bool fit, float glow)
        {
            FloorStyle f = S(name, p, a, b, c, res, false, tile, cells, 0.4f, glow);
            f.animateFps = fps; f.fitToView = fit;
            return f;
        }
        FloorStyle Bit(string name, FloorPattern p, Color a, Color b, Color c, float fps)
        {
            FloorStyle f = S(name, p, a, b, c, 256, true, 8f, 1, 0.3f, 0f);
            f.animateFps = fps; f.fitToView = true;
            return f;
        }
        // ---- Retrowave / cyberpunk ----
        yield return Neo("Retro Sunset Grid", FloorPattern.SynthSun, new Color(0.1f, 0.02f, 0.22f), new Color(1f, 0.2f, 0.6f), new Color(0.1f, 0.9f, 1f), 256, 8f, 1, 24f, true, 0.8f);
        yield return Neo("Neon Highway", FloorPattern.NeonHighway, new Color(0.03f, 0.01f, 0.12f), new Color(1f, 0.15f, 0.55f), new Color(0.1f, 0.9f, 1f), 256, 8f, 1, 24f, true, 0.8f);
        yield return Neo("Grid Tunnel", FloorPattern.GridTunnel, new Color(0.02f, 0f, 0.06f), new Color(1f, 0.2f, 0.7f), new Color(0.1f, 0.8f, 1f), 192, 8f, 1, 24f, true, 1f);
        yield return Neo("Rain Alley", FloorPattern.RainAlley, new Color(0.03f, 0.03f, 0.07f), new Color(1f, 0.15f, 0.6f), new Color(0.1f, 0.85f, 1f), 192, 6f, 5, 15f, false, 0.5f);
        yield return Neo("Vapor Checker", FloorPattern.VaporChecker, new Color(0.35f, 0.9f, 0.95f), new Color(1f, 0.55f, 0.8f), new Color(0.75f, 0.6f, 1f), 256, 8f, 1, 20f, true, 0.2f);
        yield return Neo("Neon City Map", FloorPattern.CityMap, new Color(0.02f, 0.02f, 0.05f), new Color(1f, 0.2f, 0.65f), new Color(0.1f, 0.9f, 1f), 256, 8f, 6, 15f, false, 0.6f);
        yield return Neo("Light Cycles", FloorPattern.LightCycles, new Color(0f, 0.01f, 0.03f), new Color(1f, 0.55f, 0.1f), new Color(0.1f, 0.85f, 1f), 192, 6f, 10, 20f, false, 1f);
        yield return Neo("Arcade Carpet", FloorPattern.ArcadeCarpet, new Color(0.05f, 0.03f, 0.17f), new Color(1f, 0.2f, 0.65f), new Color(0.1f, 0.9f, 1f), 192, 6f, 8, 8f, false, 0.2f);
        // ---- 8-bit games ----
        yield return Bit("8-Bit Platformer", FloorPattern.Platformer, new Color(0.36f, 0.58f, 0.99f), Color.gray, Color.gray, 20f);
        yield return Bit("8-Bit Invaders", FloorPattern.Invaders, Color.black, new Color(0.9f, 0.3f, 0.9f), new Color(0.3f, 0.9f, 1f), 20f);
        yield return Bit("8-Bit Brick Breaker", FloorPattern.BrickBreaker, Color.black, Color.gray, Color.gray, 20f);
        yield return Bit("8-Bit Pong", FloorPattern.PongCourt, Color.black, Color.gray, Color.gray, 20f);
        yield return Bit("8-Bit Snake", FloorPattern.SnakeGrid, new Color(0.6f, 0.73f, 0.06f), new Color(0.06f, 0.22f, 0.06f), new Color(0.19f, 0.38f, 0.19f), 20f);
        yield return Bit("8-Bit Block Stack", FloorPattern.BlockStack, new Color(0.05f, 0.05f, 0.12f), Color.gray, Color.gray, 20f);
        yield return Bit("8-Bit Dungeon", FloorPattern.Dungeon, new Color(0.35f, 0.33f, 0.3f), Color.gray, Color.gray, 20f);
        yield return Bit("8-Bit Crossing", FloorPattern.Crossing, new Color(0f, 0.6f, 0f), Color.gray, Color.gray, 20f);
    }

    private static Color K(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }

    private static int WrapI(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    /// <summary>0 -> 1 -> 0 triangle wave with period 1.</summary>
    private static float Tri(float x) { x = Frac(x); return x < 0.5f ? x * 2f : 2f - x * 2f; }

    /// <summary>The character of a sprite (rows listed top first) at column 'col' and row 'row' counted from the top; '.' outside it.</summary>
    private static char SpriteAt(string[] sprite, int col, int row)
    {
        if (row < 0 || row >= sprite.Length || col < 0 || col >= sprite[row].Length) return '.';
        return sprite[row][col];
    }

    private static float Glow(float distance, float size) { return Mathf.Exp(-Mathf.Max(0f, distance) / size); }

    // ------------------------------------------------------------------
    // Retro sunset grid: a striped sun over mountains, a neon grid rushing towards you
    // ------------------------------------------------------------------

    private static Color ShadeSynthSun(FloorStyle s, float u, float v)
    {
        float A = ViewAspect(), t = shadeTime, h = 0.5f;
        float px = (u - 0.5f) * A;
        Color c;
        if (v >= h)
        {
            float k = (v - h) / (1f - h);
            c = Color.Lerp(s.colorB, s.colorA, Mathf.Pow(k, 0.55f));
            if (k > 0.3f)
            {
                int sx = Mathf.FloorToInt(u * 150f), sy = Mathf.FloorToInt(v * 150f);
                float hs = Hash(sx, sy, s.seed);
                if (hs > 0.993f) c = ScMix(c, Color.white, 0.35f + 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 2f + hs * 90f)));
            }
            // The sun: yellow at the top fading to pink, cut by stripes that get fatter towards the bottom.
            float cy = h + 0.2f, R = 0.19f;
            float d = Mathf.Sqrt(px * px + (v - cy) * (v - cy));
            float ys = Mathf.Clamp01((cy - v) / R);
            float stripe = (v < cy && ys > 0.12 && Frac(v * 28f) < ys * 0.62f) ? 0f : 1f;
            Color sun = Color.Lerp(s.colorB, new Color(1f, 0.92f, 0.3f), Mathf.Clamp01((v - (cy - R)) / (2f * R)));
            c += new Color(1f, 0.6f, 0.4f) * Glow(d - R, 0.12f) * 0.25f;
            c = ScMix(c, sun, (1f - Sstep(R - 0.004f, R, d)) * stripe);
            // Mountains with a glowing rim (a valley in the middle so the sun stays in view).
            float mh = (0.05f + 0.12f * Mathf.Pow(Fbm(u + 0.1f, 0.4f, 6, 3, s.seed), 1.3f)) * Sstep(0.05f, 0.4f, Mathf.Abs(px));
            if (v < h + mh)
            {
                c = new Color(0.04f, 0.015f, 0.09f);
                if (v > h + mh - 0.006f) c = s.colorB * 1.1f;
            }
            c += s.colorB * Mathf.Exp(-(v - h) * 22f) * 0.45f;
        }
        else
        {
            float d = h - v;
            c = s.colorA * 0.3f;
            float w = 0.1f / (d + 0.001f) + t * 1.1f;
            float fz = Frac(w), fx = Frac(px / (d + 0.001f) * 0.9f);
            float lineZ = 1f - Sstep(0f, 0.09f, Mathf.Min(fz, 1f - fz));
            float lineX = 1f - Sstep(0f, 0.05f, Mathf.Min(fx, 1f - fx));
            float fade = Sstep(0.01f, 0.08f, d);
            float line = Mathf.Max(lineZ, lineX) * fade;
            c = ScMix(c, s.colorC, line);
            c += s.colorC * line * 0.2f;
            c += s.colorB * Mathf.Exp(-px * px * 6f) * Mathf.Exp(-d * 3f) * 0.25f;
            c += s.colorB * Mathf.Exp(-d * 30f) * 0.5f;
        }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Neon highway: a road into a night city, glowing edges, dashed lines, tail lights
    // ------------------------------------------------------------------

    private static Color ShadeNeonHighway(FloorStyle s, float u, float v)
    {
        float A = ViewAspect(), t = shadeTime, h = 0.6f;
        float px = (u - 0.5f) * A;
        Color c;
        if (v >= h)
        {
            float k = (v - h) / (1f - h);
            c = Color.Lerp(s.colorB * 0.8f, s.colorA, Mathf.Pow(k, 0.5f));
            int bi = Mathf.FloorToInt(px / 0.06f);
            float tall = 0.03f + 0.15f * Mathf.Pow(Hash(bi, 3, s.seed), 1.5f);
            tall *= 0.3f + 0.7f * Sstep(0.05f, 0.4f, Mathf.Abs(px));
            if (v < h + tall)
            {
                c = new Color(0.02f, 0.01f, 0.05f);
                float wx = Frac(px / 0.06f);
                int wxi = Mathf.FloorToInt(wx * 4f), wyi = Mathf.FloorToInt((v - h) / 0.013f);
                if (wx > 0.1f && wx < 0.9f && Frac(wx * 4f) > 0.2f && Hash(bi * 4 + wxi, wyi, s.seed + Mathf.FloorToInt(t * 0.4f)) > 0.72f)
                    c = Hash(bi, wyi, s.seed + 9) > 0.5f ? s.colorC * 0.85f : new Color(1f, 0.7f, 0.25f);
                if (v > h + tall - 0.004f) c = s.colorB;
            }
            c += s.colorB * Mathf.Exp(-(v - h) * 18f) * 0.35f;
        }
        else
        {
            float d = h - v;
            float xw = px / (d + 0.001f);
            float ax = Mathf.Abs(xw);
            float z = 0.1f / (d + 0.001f) + t * 1.6f;
            if (ax < 0.9f)
            {
                c = new Color(0.035f, 0.03f, 0.07f);
                if (ax < 0.03f && Frac(z) < 0.5f) c = new Color(1f, 0.9f, 0.3f);
                if (Mathf.Abs(ax - 0.45f) < 0.02f && Frac(z * 1.0f) < 0.5f) c = ScMix(c, s.colorC, 0.7f);
                float edge = 1f - Sstep(0.0f, 0.06f, Mathf.Abs(ax - 0.9f));
                c += (xw < 0f ? s.colorB : s.colorC) * edge * 0.8f;
            }
            else
            {
                c = s.colorA * 0.45f;
                float fz = Frac(z * 0.8f), fx = Frac((ax - 0.9f) * 0.8f);
                float line = Mathf.Max(1f - Sstep(0f, 0.07f, Mathf.Min(fz, 1f - fz)), 1f - Sstep(0f, 0.05f, Mathf.Min(fx, 1f - fx))) * Sstep(0.01f, 0.08f, d);
                c = ScMix(c, s.colorB * 0.7f, line * 0.7f);
                float edge = Mathf.Exp(-(ax - 0.9f) * 6f);
                c += (xw < 0f ? s.colorB : s.colorC) * edge * 0.25f;
            }
            // Tail lights of cars driving away.
            for (int i = 0; i < 4; i++)
            {
                float phase = Frac(t * 0.12f + i * 0.27f + Hash(i, 5, s.seed) * 0.2f);
                float dc = 0.015f + 0.58f * (1f - phase) * (1f - phase);
                float lane = (i % 2 == 0 ? -0.45f : 0.45f);
                float cxp = lane * dc, cvp = h - dc;
                for (int side = -1; side <= 1; side += 2)
                {
                    float lx = cxp + side * 0.14f * dc;
                    float r = 0.03f * dc + 0.002f;
                    float dd = Mathf.Sqrt((px - lx) * (px - lx) + (v - cvp) * (v - cvp));
                    c += new Color(1f, 0.1f, 0.1f) * (1f - Sstep(r * 0.6f, r, dd)) + new Color(1f, 0.15f, 0.15f) * Glow(dd - r, r * 3f) * 0.3f;
                }
            }
            c += s.colorB * Mathf.Exp(-d * 20f) * 0.3f;
        }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Grid tunnel: a neon tunnel rushing past
    // ------------------------------------------------------------------

    private static Color ShadeGridTunnel(FloorStyle s, float u, float v)
    {
        float A = ViewAspect(), t = shadeTime;
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float ax = Mathf.Abs(px), ay = Mathf.Abs(py);
        float r = Mathf.Max(ax, ay) + 0.0001f;
        bool sideWall = ax > ay;
        float z = 0.06f / r + t * 1.3f;
        float fz = Frac(z);
        float along = sideWall ? py / ax : px / ay;
        float fa = Frac(along * 3f + 0.5f);
        float ring = 1f - Sstep(0f, 0.08f, Mathf.Min(fz, 1f - fz));
        float rad = 1f - Sstep(0f, 0.045f, Mathf.Min(fa, 1f - fa));
        Color wall = sideWall ? s.colorC : (py < 0f ? s.colorB : s.colorC * 0.55f);
        float fade = Sstep(0.01f, 0.2f, r);
        Color c = s.colorA * (0.7f + 0.3f * Frac(z * 0.5f));
        c += wall * Mathf.Max(ring, rad) * fade;
        c += wall * 0.08f * fade;
        c += s.colorB * Mathf.Exp(-r * 28f) * 0.9f;
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Rain alley: wet asphalt, neon puddles with ripples, falling rain. Tiles.
    // ------------------------------------------------------------------

    private static Color ShadeRainAlley(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        float t = shadeTime;
        Color c = s.colorA * (0.8f + 0.4f * Fbm(u, v, 6, 3, s.seed));
        // Slab seams.
        float cu = Frac(u * cells), cv = Frac(v * cells);
        if (Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) < 0.025f) c *= 0.55f;

        Voronoi(u, v, cells + 2, s.seed + 5, out float f1, out _, out int id);
        float d = f1 * (cells + 2);
        bool puddle = HashId(id, s.seed) > 0.35f;
        float size = 0.28f + 0.2f * HashId(id, s.seed + 2);
        if (puddle && d < size)
        {
            float edge = Sstep(size, size - 0.06f, d);
            float streak = Sstep(0.45f, 0.8f, Fbm(u, v, 14, 2, 3, s.seed + 7));
            Color neon = HashId(id, s.seed + 4) > 0.5f ? s.colorB : s.colorC;
            Color water = s.colorA * 0.5f + neon * (0.12f + 0.55f * streak);
            float ring = Mathf.Abs(d - Frac(t * 0.35f + HashId(id, s.seed + 9)) * size);
            water += neon * (1f - Sstep(0f, 0.025f, ring)) * 0.5f * (1f - d / size);
            c = ScMix(c, water, edge);
        }
        // Raindrops hitting the ground.
        if (Hash(x, y, s.seed + Mathf.FloorToInt(t * 12f)) > 0.9985f) c = ScMix(c, Color.white, 0.6f);
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Vapor checker: a pastel checkerboard sliding towards a big sun
    // ------------------------------------------------------------------

    private static Color ShadeVaporChecker(FloorStyle s, float u, float v, int tx, int ty)
    {
        float A = ViewAspect(), t = shadeTime, h = 0.48f;
        float px = (u - 0.5f) * A;
        Color c;
        if (v >= h)
        {
            float k = (v - h) / (1f - h);
            c = Color.Lerp(s.colorB, s.colorA, Mathf.Pow(k, 0.7f));
            float cy = h + 0.16f, R = 0.15f;
            float d = Mathf.Sqrt(px * px + (v - cy) * (v - cy));
            Color sun = Color.Lerp(new Color(1f, 0.95f, 0.6f), s.colorB, Mathf.Clamp01((cy + R - v) / (2f * R)));
            c = ScMix(c, sun, 1f - Sstep(R - 0.004f, R, d));
            // Sparkles (plus signs).
            int gx = Mathf.FloorToInt(u * 22f), gy = Mathf.FloorToInt(v * 22f);
            if (v > h + 0.1f && Hash(gx, gy, s.seed) > 0.93f)
            {
                float lx = Frac(u * 22f) - 0.5f, ly = Frac(v * 22f) - 0.5f;
                float tw = 0.5f + 0.5f * Mathf.Sin(t * 3f + Hash(gx, gy, s.seed + 1) * 20f);
                if ((Mathf.Abs(lx) < 0.05f && Mathf.Abs(ly) < 0.28f) || (Mathf.Abs(ly) < 0.05f && Mathf.Abs(lx) < 0.28f)) c = ScMix(c, Color.white, 0.8f * tw);
            }
        }
        else
        {
            float d = h - v;
            float w = 0.12f / (d + 0.001f) + t * 0.8f;
            float xw = px / (d + 0.001f) * 1.2f;
            bool odd = ((Mathf.FloorToInt(w) + Mathf.FloorToInt(xw)) & 1) == 0;
            c = odd ? s.colorB : s.colorA;
            c = Color.Lerp(c, s.colorC, 0.15f);
            float haze = Mathf.Exp(-d * 9f);
            c = ScMix(c, Color.Lerp(s.colorB, Color.white, 0.5f), haze * 0.7f);
        }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Neon city map: an aerial view of a night city, glowing roofs and traffic. Tiles.
    // ------------------------------------------------------------------

    private static Color ShadeCityMap(FloorStyle s, float u, float v, int cells)
    {
        float t = shadeTime;
        float gu = u * cells, gv = v * cells;
        int ix = Mathf.FloorToInt(gu), iy = Mathf.FloorToInt(gv);
        float cu = gu - ix, cv = gv - iy;
        const float rw = 0.1f;
        bool roadX = cu < rw || cu > 1f - rw, roadY = cv < rw || cv > 1f - rw;
        if (roadX || roadY)
        {
            Color c = new Color(0.03f, 0.03f, 0.06f);
            // Which road is it and how far across / along is this spot? (vertical roads at cu ~ 0 / 1, horizontal at cv ~ 0 / 1)
            bool vertical = roadX && (!roadY || Mathf.Min(cu, 1f - cu) < Mathf.Min(cv, 1f - cv));
            float across = vertical ? (cu < 0.5f ? cu : cu - 1f) : (cv < 0.5f ? cv : cv - 1f);
            float along = vertical ? gv : gu;
            int roadId = vertical ? Mathf.RoundToInt(gu) : Mathf.RoundToInt(gv);
            if (Mathf.Abs(across) < 0.012f && Frac(along * 3f) < 0.5f) c = ScMix(c, new Color(0.9f, 0.8f, 0.3f), 0.6f);
            float edge = 1f - Sstep(0f, 0.02f, Mathf.Abs(Mathf.Abs(across) - 0.085f));
            c += s.colorB * 0.35f * edge;
            for (int lane = -1; lane <= 1; lane += 2)
            {
                float dirn = lane;
                float spot = Frac(along * 0.7f + dirn * t * 0.25f + Hash(roadId, lane + 5, s.seed));
                if (Mathf.Abs(across - lane * 0.045f) < 0.016f && spot < 0.045f)
                    c = dirn > 0f ? new Color(1f, 0.95f, 0.8f) : new Color(1f, 0.15f, 0.1f);
            }
            c.a = 1f;
            return c;
        }
        // A block: a roof with a neon rim and a few details.
        float bu = (cu - rw) / (1f - 2f * rw), bv = (cv - rw) / (1f - 2f * rw);
        float h1 = Hash(ix, iy, s.seed);
        Color neon = h1 < 0.33f ? s.colorB : (h1 < 0.66f ? s.colorC : new Color(1f, 0.6f, 0.15f));
        Color roof = Color.Lerp(new Color(0.05f, 0.05f, 0.1f), new Color(0.09f, 0.08f, 0.16f), Hash(ix, iy, s.seed + 3));
        float rim = Mathf.Min(Mathf.Min(bu, 1f - bu), Mathf.Min(bv, 1f - bv));
        roof += neon * (1f - Sstep(0f, 0.06f, rim)) * 0.7f;
        // Rooftop units.
        int ux = Mathf.FloorToInt(bu * 4f), uy = Mathf.FloorToInt(bv * 4f);
        if (Hash(ix * 7 + ux, iy * 7 + uy, s.seed + 11) > 0.65f && rim > 0.12f)
        {
            float lx = Frac(bu * 4f), ly = Frac(bv * 4f);
            if (lx > 0.2f && lx < 0.8f && ly > 0.2f && ly < 0.8f) roof = Color.Lerp(roof, neon, 0.25f) + neon * 0.1f * (0.5f + 0.5f * Mathf.Sin(t * 3f + h1 * 20f));
        }
        roof.a = 1f;
        return roof;
    }

    // ------------------------------------------------------------------
    // Light cycles: a dark grid with glowing light trails racing along its lines. Tiles.
    // ------------------------------------------------------------------

    private static Color ShadeLightCycles(FloorStyle s, float u, float v, int n, int cells)
    {
        float t = shadeTime;
        float cu = Frac(u * cells), cv = Frac(v * cells);
        float du = Mathf.Min(cu, 1f - cu), dv = Mathf.Min(cv, 1f - cv);
        float line = Mathf.Min(du, dv) * n / cells;
        Color c = s.colorA + s.colorC * 0.35f * Mathf.Clamp01(1.4f - line) + s.colorC * 0.08f * Mathf.Exp(-line * 0.5f);
        const int count = 8;
        for (int i = 0; i < count; i++)
        {
            bool horizontal = (i & 1) == 0;
            int lineIndex = Mathf.FloorToInt(Hash(i, 1, s.seed) * cells);
            float speed = (0.05f + 0.07f * Hash(i, 2, s.seed)) * (Hash(i, 3, s.seed) > 0.5f ? 1f : -1f);
            float head = Frac(Hash(i, 4, s.seed) + t * speed);
            float coord = horizontal ? u : v, across = horizontal ? v : u;
            float onLine = Mathf.Abs(across - lineIndex / (float)cells) * n;
            if (onLine > 3f) continue;
            float behind = speed > 0f ? Frac(head - coord) : Frac(coord - head);
            const float trail = 0.3f;
            if (behind < trail)
            {
                float k = 1f - behind / trail;
                Color cyc = (i % 3 == 0) ? s.colorB : s.colorC;
                float core = Mathf.Clamp01(1.8f - onLine);
                c += cyc * k * (core * 0.9f + Mathf.Exp(-onLine) * 0.3f);
            }
        }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Arcade carpet: neon confetti on a dark carpet. Tiles.
    // ------------------------------------------------------------------

    private static Color ShadeArcadeCarpet(FloorStyle s, float u, float v, int cells)
    {
        float t = shadeTime;
        Color c = s.colorA * (0.85f + 0.3f * Hash(Mathf.FloorToInt(u * 160f), Mathf.FloorToInt(v * 160f), s.seed));
        int gx0 = Mathf.FloorToInt(u * cells), gy0 = Mathf.FloorToInt(v * cells);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = gx0 + ox, gy = gy0 + oy;
                int hx = WrapI(gx, cells), hy = WrapI(gy, cells);
                float h0 = Hash(hx, hy, s.seed + 21);
                float cx = (gx + 0.2f + 0.6f * Hash(hx, hy, s.seed + 22)) / cells, cy = (gy + 0.2f + 0.6f * Hash(hx, hy, s.seed + 23)) / cells;
                float dx = (u - cx) * cells, dy = (v - cy) * cells;
                float ang = Hash(hx, hy, s.seed + 24) * 6.2832f, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                float rx = dx * ca - dy * sa, ry = dx * sa + dy * ca;
                int shape = Mathf.FloorToInt(h0 * 5f);
                bool hit = false;
                switch (shape)
                {
                    case 0: hit = rx * rx + ry * ry < 0.035f; break;                                  // dot
                    case 1: { float r2 = rx * rx + ry * ry; hit = r2 < 0.07f && r2 > 0.035f; break; } // ring
                    case 2: hit = ry > -0.18f && ry < 0.2f && Mathf.Abs(rx) < (0.22f - (ry + 0.18f) * 0.55f); break; // triangle
                    case 3: hit = (Mathf.Abs(rx) < 0.04f && Mathf.Abs(ry) < 0.2f) || (Mathf.Abs(ry) < 0.04f && Mathf.Abs(rx) < 0.2f); break; // plus
                    default: hit = Mathf.Abs(rx) < 0.28f && Mathf.Abs(ry - 0.07f * Mathf.Sin(rx * 22f)) < 0.035f; break; // squiggle
                }
                if (!hit) continue;
                float p = Hash(hx, hy, s.seed + 25);
                Color col = p < 0.25f ? s.colorB : p < 0.5f ? s.colorC : p < 0.7f ? new Color(1f, 0.9f, 0.2f) : p < 0.85f ? new Color(0.7f, 0.35f, 1f) : new Color(0.2f, 1f, 0.5f);
                c = col * (0.8f + 0.2f * Mathf.Sin(t * 2f + h0 * 40f));
            }
        c.a = 1f;
        return c;
    }

    // ==================================================================
    // 8-bit game scenes. Virtual picture P rows tall; px counted from the middle, py from the bottom.
    // ==================================================================

    private static readonly string[] HeroSprite =
    {
        "..RRRRR.", ".RRRRRRR", "..SSSKS.", ".SSSSSSS", "..SSSS..", ".RRBRRR.", "RRRBBRRR", "SSBBBBSS", ".BBBBBB.", ".BB..BB.",
    };
    private static readonly string[] HeroSprite2 =
    {
        "..RRRRR.", ".RRRRRRR", "..SSSKS.", ".SSSSSSS", "..SSSS..", ".RRBRRR.", "RRRBBRRR", "SSBBBBSS", ".BBBBBB.", "BB....BB",
    };

    private static bool HeroPixel(int col, int rowFromTop, bool frame2, Color shirt, Color pants, out Color c)
    {
        char ch = SpriteAt(frame2 ? HeroSprite2 : HeroSprite, col, rowFromTop);
        c = Color.clear;
        switch (ch)
        {
            case 'R': c = shirt; return true;
            case 'S': c = K(252, 188, 120); return true;
            case 'K': c = K(30, 20, 10); return true;
            case 'B': c = pants; return true;
        }
        return false;
    }

    // ---- Platformer: a side-scrolling level ----

    private static readonly string[] QuestionBlock =
    {
        "BBBBBBBB", "BYQQQQYB", "BQQYYQQB", "BYYYQQYB", "BYYQQYYB", "BYYYYYYB", "BYYQQYYB", "BBBBBBBB",
    };

    private static Color ShadePlatformer(FloorStyle s, float u, float v)
    {
        const int P = 64, ground = 10;
        float t = shadeTime;
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        int sc = Mathf.FloorToInt(t * 24f);
        int wx = px + sc;

        // The hero runs in place on the ground.
        int heroX = -22;
        bool frame2 = ((int)(t * 8f) & 1) == 1;
        if (HeroPixel(px - heroX, (ground + 10 - 1) - py, frame2, K(216, 40, 0), K(0, 80, 216), out Color hero)) return hero;

        // Question blocks with a coin floating above.
        int bw = WrapI(wx, 112), by = ground + 22;
        if (bw >= 48 && bw < 72 && py >= by && py < by + 8)
        {
            int k = (bw - 48) / 8, lx = (bw - 48) % 8, ly = by + 7 - py;
            if (k == 1)
            {
                char ch = SpriteAt(QuestionBlock, lx, ly);
                bool blink = Mathf.Repeat(t, 0.8f) < 0.4f;
                if (ch == 'B') return K(140, 60, 0);
                if (ch == 'Q') return K(120, 50, 0);
                return blink ? K(252, 188, 60) : K(255, 224, 130);
            }
            if (lx == 0 || ly == 0 || ly == 7 || ly == 3 || (ly < 3 ? (lx == 4) : (lx == 2 || lx == 6))) return K(80, 30, 0);
            return K(200, 76, 12);
        }
        int coinW = 1 + Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(t * 6f)) * 3f);
        if (py >= by + 12 && py < by + 20 && Mathf.Abs(bw - 60) <= coinW)
            return (Mathf.Abs(bw - 60) < coinW - 1 || coinW == 1) ? K(255, 224, 80) : K(214, 150, 0);

        // Pipe.
        int pw = WrapI(wx, 160), dy = py - ground;
        if (pw >= 100 && pw < 124 && dy >= 0 && dy < 18)
        {
            bool rim = dy >= 14;
            int lx = pw - 100;
            if (!rim && (lx < 2 || lx > 21)) { /* beside the narrower body: sky shows through */ }
            else
            {
                if (rim) { if (dy == 14 || dy == 17 || lx == 0 || lx == 23) return K(0, 60, 0); }
                else if (lx == 2 || lx == 21) return K(0, 60, 0);
                if (lx < 5) return K(150, 235, 80);
                if (lx > 17) return K(0, 110, 0);
                return K(0, 168, 0);
            }
        }

        // Ground bricks.
        if (py < ground)
        {
            int row = py / 5;
            int bx = WrapI(wx + (row & 1) * 8, 16);
            if (py == ground - 1) return K(240, 160, 70);
            if (py % 5 == 0 || bx == 0) return K(90, 30, 0);
            return K(200, 76, 12);
        }

        // Bush on the ground.
        int bpw = WrapI(wx, 90);
        float bdx = (bpw - 22f) / 14f, bdy = (py - ground) / 7f;
        if (py >= ground && bdx * bdx + bdy * bdy < 1f) return ((bpw + py) % 5 == 0) ? K(120, 230, 100) : K(0, 150, 0);

        // Hills (slower), clouds (slower still), then the sky.
        int hw = WrapI(px + sc / 2, 96);
        float hx = (hw - 48f) / 40f, hy = (py - ground) / 22f;
        float hd = hx * hx + hy * hy;
        if (py >= ground && hd < 1f) return hd > 0.82f ? K(0, 100, 0) : K(0, 168, 0);

        int cw = WrapI(px + sc / 4, 120);
        if (CloudHit(cw, py, 30, 52, 9, 4) || CloudHit(cw, py, 22, 50, 6, 3) || CloudHit(cw, py, 38, 50, 6, 3) || CloudHit(cw, py, 90, 44, 7, 3) || CloudHit(cw, py, 84, 42, 5, 2))
            return py < 49 ? K(172, 224, 252) : Color.white;
        return s.colorA;
    }

    private static bool CloudHit(int x, int y, int cx, int cy, int rx, int ry)
    {
        float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
        return dx * dx + dy * dy < 1f;
    }

    // ---- Invaders: rows of aliens marching, a cannon firing ----

    private static readonly string[] AlienBig =
    {
        "..#.....#..", "...#...#...", "..#######..", ".##.###.##.", "###########", "#.#######.#", "#.#.....#.#", "...##.##...",
    };
    private static readonly string[] AlienBig2 =
    {
        "..#.....#..", "#..#...#..#", "#.#######.#", "###.###.###", "###########", ".#########.", "..#.....#..", ".#.......#.",
    };
    private static readonly string[] AlienSmall =
    {
        "..#..#..", "...##...", "..####..", ".##..##.", "########", "#.####.#", "#.#..#.#", "...##...",
    };
    private static readonly string[] AlienSmall2 =
    {
        "..#..#..", "#..##..#", "#.####.#", "###..###", "########", ".######.", "..#..#..", ".#....#.",
    };
    private static readonly string[] Cannon =
    {
        "......#......", ".....###.....", ".....###.....", ".###########.", "#############", "#############", "#############", "#############",
    };

    private static Color ShadeInvaders(FloorStyle s, float u, float v)
    {
        const int P = 64;
        float t = shadeTime;
        int halfW = Mathf.FloorToInt(ViewAspect() * P * 0.5f);
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        bool frame2 = ((int)(t * 2f) & 1) == 1;

        // The formation shuffles sideways and creeps down, then starts again.
        int step = Mathf.FloorToInt(t * 3f);
        int range = 12;
        int cycle = step % (range * 2 * 4);
        int phase = cycle % (range * 2), lap = cycle / (range * 2);
        int dx = (phase < range ? phase : range * 2 - phase) * 2 - range;
        int dyDrop = lap * 4;

        // Player cannon follows the formation; its bullet flies up now and then.
        int cannonX = Mathf.RoundToInt(dx * 0.9f);
        if (SpriteAt(Cannon, px - (cannonX - 6), (9 + 7) - py) == '#') return K(60, 255, 100);
        float shotT = Mathf.Repeat(t, 1.3f);
        int bulletY = 17 + Mathf.FloorToInt(shotT * 42f);
        if (Mathf.Abs(px - cannonX) < 1 && py >= bulletY && py < bulletY + 4 && shotT < 1f) return Color.white;

        // Aliens: 5 rows of 8.
        for (int row = 0; row < 5; row++)
            for (int col = 0; col < 8; col++)
            {
                bool small = row == 0;
                string[] sprite = small ? (frame2 ? AlienSmall2 : AlienSmall) : (frame2 ? AlienBig2 : AlienBig);
                int w = small ? 8 : 11;
                int ox = (col - 4) * 13 + dx - (w >> 1) + 6;
                int oy = P - 8 - row * 10 - dyDrop;
                if (SpriteAt(sprite, px - ox, oy - py) == '#')
                    return row == 0 ? s.colorB : (row < 3 ? s.colorC : Color.white);
            }

        // Shields.
        for (int sh = 0; sh < 4; sh++)
        {
            int sx = (sh - 2) * 26 + 13, lx = px - sx + 6, ly = py - 8;
            if (lx >= 0 && lx < 12 && ly >= 0 && ly < 8)
            {
                bool shape = ly < 6 ? true : (lx < 3 || lx > 8);
                if (shape && !(ly > 5 && lx > 2 && lx < 9) && Hash(lx + sh * 13, ly, s.seed) > 0.12f && !(ly < 3 && (lx < 1 || lx > 10))) return K(60, 255, 100);
            }
        }
        if (py == 4 && Mathf.Abs(px) < halfW) return K(60, 255, 100);
        // Stars drifting down.
        int sy = py + Mathf.FloorToInt(t * 4f);
        if (Hash(px, sy, s.seed) > 0.992f) return new Color(0.7f, 0.7f, 0.8f);
        return s.colorA;
    }

    // ---- Brick breaker ----

    private static Color ShadeBrickBreaker(FloorStyle s, float u, float v)
    {
        const int P = 64;
        float t = shadeTime;
        int halfW = Mathf.FloorToInt(ViewAspect() * P * 0.5f);
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        int lx = px + halfW;
        Color grey = K(142, 142, 142);
        if (px < -halfW + 2 || px >= halfW - 2 || py >= P - 2) return grey;

        // Ball and paddle (the ball follows triangle waves, the paddle follows the ball).
        float ballX = -halfW + 6 + Tri(t * 0.11f) * (2 * halfW - 12);
        float ballY = 10f + Tri(t * 0.17f) * (P - 36);
        if (Mathf.Abs(px - ballX) <= 1.5f && Mathf.Abs(py - ballY) <= 1.5f) return Color.white;
        float padX = Mathf.Clamp(-halfW + 6 + Tri(t * 0.11f - 0.008f) * (2 * halfW - 12), -halfW + 9, halfW - 9);
        if (py >= 3 && py < 5 && Mathf.Abs(px - padX) < 8) return K(200, 72, 72);

        // Six rows of bricks that vanish gradually, then come back.
        int top = P - 8;
        int rowI = (top - py) / 4;
        if (py <= top && rowI >= 0 && rowI < 6)
        {
            int col = lx / 10, bx = lx % 10;
            bool body = bx < 9 && ((top - py) % 4) < 3;
            float cyc = Frac(t / 26f);
            bool gone = Hash(col, rowI, s.seed) < cyc * 1.05f - 0.03f;
            if (body && !gone)
            {
                Color[] palette = { K(200, 72, 72), K(198, 108, 58), K(180, 122, 48), K(162, 162, 42), K(72, 160, 72), K(66, 72, 200) };
                return palette[rowI];
            }
        }
        return s.colorA;
    }

    // ---- Pong ----

    private static readonly string[] DigitFont =
    {
        "###" + "#.#" + "#.#" + "#.#" + "###", ".#." + "##." + ".#." + ".#." + "###", "###" + "..#" + "###" + "#.." + "###",
        "###" + "..#" + "###" + "..#" + "###", "#.#" + "#.#" + "###" + "..#" + "..#", "###" + "#.." + "###" + "..#" + "###",
        "###" + "#.." + "###" + "#.#" + "###", "###" + "..#" + "..#" + "..#" + "..#", "###" + "#.#" + "###" + "#.#" + "###",
        "###" + "#.#" + "###" + "..#" + "###",
    };

    /// <summary>True if a 3x5 digit (drawn at 2x scale) has ink at (x, y) relative to its bottom-left corner.</summary>
    private static bool DigitInk(int digit, int x, int y)
    {
        if (x < 0 || y < 0 || x >= 6 || y >= 10) return false;
        int cx = x / 2, cy = 4 - y / 2;
        return DigitFont[Mathf.Clamp(digit, 0, 9)][cy * 3 + cx] == '#';
    }

    private static Color ShadePongCourt(FloorStyle s, float u, float v)
    {
        const int P = 64;
        float t = shadeTime;
        int halfW = Mathf.FloorToInt(ViewAspect() * P * 0.5f);
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        Color white = K(235, 235, 235);
        if (py >= P - 2 || py < 2) return white;
        float ballX = -halfW + 8 + Tri(t * 0.21f) * (2 * halfW - 16);
        float ballY = 6f + Tri(t * 0.33f) * (P - 14);
        if (Mathf.Abs(px - ballX) <= 1.5f && Mathf.Abs(py - ballY) <= 1.5f) return white;
        // Paddles chase the ball height a little late.
        float leftY = 6f + Tri(t * 0.33f - 0.05f * (1f - Tri(t * 0.21f))) * (P - 14);
        float rightY = 6f + Tri(t * 0.33f - 0.03f) * (P - 14);
        if (px >= -halfW + 3 && px < -halfW + 5 && Mathf.Abs(py - leftY) < 6.5f) return white;
        if (px >= halfW - 5 && px < halfW - 3 && Mathf.Abs(py - rightY) < 6.5f) return white;
        if (px == 0 && ((py / 4) & 1) == 0) return white;
        // Scores.
        int scoreL = Mathf.FloorToInt(t / 7f) % 10, scoreR = Mathf.FloorToInt(t / 11f) % 10;
        if (DigitInk(scoreL, px + 18, py - (P - 16)) || DigitInk(scoreR, px - 12, py - (P - 16))) return white;
        return s.colorA;
    }

    // ---- Snake: a green LCD with a snake running round the edge ----

    private static Color ShadeSnakeGrid(FloorStyle s, float u, float v)
    {
        const int P = 60, cell = 3;
        float t = shadeTime;
        int halfW = Mathf.FloorToInt(ViewAspect() * P * 0.5f);
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        int cx = Mathf.FloorToInt(px / (float)cell), cy = py / cell;
        int lx = WrapI(px, cell), ly = py % cell;
        int rows = P / cell;
        int xr = halfW / cell - 3, x0 = -xr, x1 = xr - 1, y0 = 3, y1 = rows - 4;
        int W = x1 - x0, H = y1 - y0, ring = 2 * (W + H);

        Color bg = s.colorA, ink = s.colorB;
        // Faint LCD cell texture.
        if (lx == 1 && ly == 1) bg = Color.Lerp(bg, s.colorC, 0.25f);

        // Where is this cell on the ring? (clockwise from the bottom left corner)
        int idx = -1;
        if (cy == y0 && cx >= x0 && cx <= x1) idx = cx - x0;
        else if (cx == x1 && cy > y0 && cy <= y1) idx = W + (cy - y0);
        else if (cy == y1 && cx < x1 && cx >= x0) idx = W + H + (x1 - cx);
        else if (cx == x0 && cy < y1 && cy > y0) idx = 2 * W + H + (y1 - cy);

        int head = Mathf.FloorToInt(t * 9f) % ring;
        const int length = 16;
        if (idx >= 0)
        {
            int behind = WrapI(head - idx, ring);
            if (behind < length && !(lx == 2 && ly == 2 && behind > 0)) return ink;
        }
        // Food in the middle, changing after every few laps of the head.
        int eaten = Mathf.FloorToInt(t * 9f / 40f);
        int fx = x0 + 3 + Mathf.FloorToInt(Hash(eaten, 1, s.seed) * (W - 5)), fy = y0 + 3 + Mathf.FloorToInt(Hash(eaten, 2, s.seed) * (H - 5));
        if (Mathf.Repeat(t, 0.5f) < 0.3f && ((cx == fx && cy == fy && !(lx == 0 && ly == 0)) || (Mathf.Abs(cx - fx) + Mathf.Abs(cy - fy) == 1 && lx == 1 && ly == 1))) return ink;
        // A border around the field.
        if ((cx == x0 - 1 || cx == x1 + 1) && cy >= y0 - 1 && cy <= y1 + 1) return lx == 1 ? ink : bg;
        if ((cy == y0 - 1 || cy == y1 + 1) && cx >= x0 - 1 && cx <= x1 + 1) return ly == 1 ? ink : bg;
        return bg;
    }

    // ---- Block stack: falling pieces in a well ----

    private static readonly int[][] PieceShapes =
    {
        new[] { 0, 0, 1, 0, 2, 0, 3, 0 }, new[] { 0, 0, 1, 0, 0, 1, 1, 1 }, new[] { 0, 0, 1, 0, 2, 0, 1, 1 }, new[] { 1, 0, 2, 0, 0, 1, 1, 1 },
        new[] { 0, 0, 1, 0, 1, 1, 2, 1 }, new[] { 0, 0, 1, 0, 2, 0, 0, 1 }, new[] { 0, 0, 1, 0, 2, 0, 2, 1 },
    };

    private static Color PieceColor(int type)
    {
        switch (type)
        {
            case 0: return K(0, 220, 230);
            case 1: return K(240, 220, 0);
            case 2: return K(170, 0, 220);
            case 3: return K(0, 220, 0);
            case 4: return K(230, 0, 0);
            case 5: return K(0, 70, 230);
            default: return K(240, 140, 0);
        }
    }

    private static Color ShadeBlockStack(FloorStyle s, float u, float v)
    {
        const int P = 80, cell = 4, cols = 10;
        float t = shadeTime;
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        int wellL = -cols * cell / 2;
        Color bg = (((px / cell) + (py / cell)) & 1) == 0 ? s.colorA : s.colorA * 1.25f;
        if (px < wellL - 3 || px >= wellL + cols * cell + 3) return bg * 0.8f;
        if (px < wellL || px >= wellL + cols * cell)
        {
            // The well walls (a lit left face, shaded right).
            bool left = px < wellL;
            return left ? K(160, 160, 170) : K(90, 90, 100);
        }
        int col = (px - wellL) / cell, row = py / cell;
        int lx = (px - wellL) % cell, ly = py % cell;
        Color baseColor = new Color(0.03f, 0.03f, 0.08f);
        int type = -1;

        // The heap at the bottom, with a few holes.
        if (row < 5 && Hash(col, row, s.seed) > 0.14f + row * 0.1f) type = Mathf.FloorToInt(Hash(col, row, s.seed + 5) * 7f);
        // Three pieces dropping at different times.
        for (int i = 0; i < 3; i++)
        {
            const float period = 6f;
            float tt = t / period + i / 3f;
            int epoch = Mathf.FloorToInt(tt);
            float ph = tt - epoch;
            int steps = Mathf.FloorToInt(ph * 14f);
            int cy = 19 - steps, ptype = Mathf.FloorToInt(Hash(epoch, i, s.seed + 31) * 7f);
            int c0 = Mathf.FloorToInt(Hash(epoch, i + 9, s.seed + 32) * 7f);
            int[] shape = PieceShapes[ptype];
            for (int k = 0; k < 4; k++)
                if (col == c0 + shape[k * 2] && row == cy + shape[k * 2 + 1]) type = ptype;
        }
        if (type < 0) return baseColor;
        Color c = PieceColor(type);
        if (lx == 0 || ly == cell - 1) return c * 1.35f;
        if (lx == cell - 1 || ly == 0) return c * 0.6f;
        return c;
    }

    // ---- Dungeon: stone tiles, a carpet, braziers, a hero and a slime ----

    private static readonly string[] SlimeSprite =
    {
        "........", "..GGGG..", ".GKGGKG.", "GGGGGGGG", "GGGGGGGG", ".GGGGGG.",
    };

    private static Color ShadeDungeon(FloorStyle s, float u, float v)
    {
        const int P = 64;
        float t = shadeTime;
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        int wy = py + Mathf.FloorToInt(t * 8f);

        // The hero walks up the carpet, a slime bobs along the side.
        bool frame2 = ((int)(t * 6f) & 1) == 1;
        if (HeroPixel(px + 4, 27 - py, frame2, K(30, 160, 60), K(120, 80, 40), out Color hero)) return hero;
        int slimeX = Mathf.RoundToInt(Mathf.Sin(t * 0.7f) * 18f) + 30;
        int bounce = Mathf.RoundToInt(Mathf.Abs(Mathf.Sin(t * 4f)) * 3f);
        char sl = SpriteAt(SlimeSprite, px - slimeX, (14 + 5 + bounce) - py);
        if (sl == 'G') return K(90, 210, 90);
        if (sl == 'K') return K(20, 30, 20);

        // Braziers every 48 rows on both sides, with a flickering flame and a warm glow.
        float glow = 0f;
        for (int side = -1; side <= 1; side += 2)
        {
            int bx = side * 28;
            int by = WrapI(wy, 48);
            float ddx = px - bx, ddy = by - 24;
            float dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);
            if (dist < 3.2f)
            {
                float fl = Hash(px, wy, s.seed + Mathf.FloorToInt(t * 12f));
                return dist < 1.6f ? (fl > 0.4f ? K(255, 240, 120) : K(255, 190, 40)) : (fl > 0.5f ? K(255, 130, 20) : K(200, 60, 10));
            }
            if (dist < 4.2f) return K(60, 50, 50);
            glow = Mathf.Max(glow, Mathf.Clamp01(1f - dist / 20f));
        }

        // Floor: stone tiles, with a carpet down the middle.
        int tx = Mathf.FloorToInt(px / 8f), ty = Mathf.FloorToInt(wy / 8f);
        int lx = WrapI(px, 8), ly = WrapI(wy, 8);
        Color c = Color.Lerp(s.colorA * 0.85f, s.colorA * 1.15f, Hash(tx, ty, s.seed));
        if (lx == 0 || ly == 0) c *= 0.6f;
        else if (lx == 1 || ly == 7) c *= 1.12f;
        if (Hash(px, wy, s.seed + 4) > 0.97f) c *= 0.8f;
        if (Mathf.Abs(px + 4) < 12)
        {
            int ax = Mathf.Abs(px + 4);
            c = K(165, 30, 40);
            if (ax >= 10) c = K(220, 170, 40);
            else if (ax >= 8) c = K(120, 20, 30);
            else if (((WrapI(wy, 8) - 4) + 0) * ((WrapI(wy, 8) - 4)) + ax * ax < 8) c = K(220, 170, 40);
        }
        // Dithered warm light round the braziers.
        if (glow > 0f && ((px + py) & 1) == 0) c = Color.Lerp(c, new Color(1f, 0.6f, 0.2f), glow * 0.5f);
        else c = Color.Lerp(c, new Color(1f, 0.7f, 0.3f), glow * 0.15f);
        c *= 1f - 0.25f * (1f - glow);
        c.a = 1f;
        return c;
    }

    // ---- Crossing: roads with traffic, a river with logs and turtles, a hopping frog ----

    private static readonly string[] FrogSprite =
    {
        "GG...GG", ".GGGGG.", ".GWGWG.", ".GGGGG.", "GG...GG",
    };

    private static Color ShadeCrossing(FloorStyle s, float u, float v)
    {
        const int P = 66;
        float t = shadeTime;
        int px = Mathf.FloorToInt((u - 0.5f) * ViewAspect() * P), py = Mathf.FloorToInt(v * P);
        int lane = Mathf.Clamp(py / 6, 0, 10), ly = py % 6;

        // The frog.
        int frogLane = Mathf.FloorToInt(t * 1.6f) % 11;
        char fr = SpriteAt(FrogSprite, px + 3, (frogLane * 6 + 5) - py);
        if (fr == 'G') return K(80, 255, 80);
        if (fr == 'W') return Color.white;

        float dir = (lane & 1) == 0 ? 1f : -1f;
        float speed = 7f + 7f * Hash(lane, 1, s.seed);
        int wx = px + Mathf.FloorToInt(t * speed * dir);
        Color grass = K(0, 150, 0);

        if (lane == 0 || lane == 5)
        {
            if ((WrapI(px, 8) == 3 && ly == 3) && Hash(px / 8, lane, s.seed) > 0.6f) return K(255, 120, 200);   // a flower
            return ly == 0 ? K(0, 110, 0) : grass;
        }
        if (lane >= 1 && lane <= 4)
        {
            Color road = K(55, 55, 65);
            if (ly == 0 && WrapI(px, 10) < 5) return K(220, 220, 220);
            int spacing = 34 + (lane * 7) % 11, lx = WrapI(wx, spacing);
            if (lx < 12 && ly >= 1 && ly <= 4)
            {
                if ((ly == 1 || ly == 4) && (lx < 3 || lx > 8)) return K(10, 10, 10);
                if ((ly == 2 || ly == 3) && lx >= 4 && lx <= 7) return K(150, 220, 255);
                Color[] body = { K(220, 40, 40), K(240, 220, 40), K(60, 200, 230), K(230, 90, 200), K(235, 235, 235) };
                return body[Mathf.FloorToInt(Hash(lane, Mathf.FloorToInt(wx / (float)spacing), s.seed) * 5f) % 5];
            }
            return road;
        }
        if (lane >= 6 && lane <= 9)
        {
            Color water = K(30, 60, 190);
            if (ly == 2 && WrapI(wx + Mathf.FloorToInt(t * 3f), 8) == 0) return K(90, 130, 240);
            if ((lane & 1) == 0)
            {
                int spacing = 46, lx = WrapI(wx, spacing);
                if (lx < 22 && ly >= 1 && ly <= 4) return (lx % 6 == 0 || lx == 21) ? K(80, 45, 10) : K(130, 78, 24);
            }
            else
            {
                int lx = WrapI(wx, 40);
                if (lx < 27 && (lx % 9) < 7 && ly >= 1 && ly <= 4)
                {
                    int tl = lx % 9;
                    return (tl >= 2 && tl <= 4 && ly >= 2 && ly <= 3) ? K(80, 190, 110) : K(0, 110, 60);
                }
            }
            return water;
        }
        // Goal row: hedge with water bays.
        if (WrapI(px + 6, 24) < 10 && ly >= 1) return ly == 5 ? K(30, 60, 190) : (Hash(px, py, s.seed) > 0.9f ? K(80, 190, 110) : K(30, 60, 190));
        return Hash(px, py, s.seed) > 0.85f ? K(0, 130, 0) : K(0, 90, 0);
    }
}
