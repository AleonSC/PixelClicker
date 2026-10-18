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
        }
        return ShadeWorlds(s, u, v, x, y, n, cells);   // the fourth batch (PixelFloorWorlds.cs)
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
        yield return Neo("Retro Sunset Grid", FloorPattern.SynthSun, new Color(0.1f, 0.02f, 0.22f), new Color(1f, 0.2f, 0.6f), new Color(0.1f, 0.9f, 1f), 256, 8f, 1, 24f, true, 0.8f);
        yield return Neo("Neon Highway", FloorPattern.NeonHighway, new Color(0.03f, 0.01f, 0.12f), new Color(1f, 0.15f, 0.55f), new Color(0.1f, 0.9f, 1f), 256, 8f, 1, 24f, true, 0.8f);
        yield return Neo("Grid Tunnel", FloorPattern.GridTunnel, new Color(0.02f, 0f, 0.06f), new Color(1f, 0.2f, 0.7f), new Color(0.1f, 0.8f, 1f), 192, 8f, 1, 24f, true, 1f);
        yield return Neo("Rain Alley", FloorPattern.RainAlley, new Color(0.03f, 0.03f, 0.07f), new Color(1f, 0.15f, 0.6f), new Color(0.1f, 0.85f, 1f), 192, 6f, 5, 15f, false, 0.5f);
        yield return Neo("Vapor Checker", FloorPattern.VaporChecker, new Color(0.35f, 0.9f, 0.95f), new Color(1f, 0.55f, 0.8f), new Color(0.75f, 0.6f, 1f), 256, 8f, 1, 20f, true, 0.2f);
        yield return Neo("Neon City Map", FloorPattern.CityMap, new Color(0.02f, 0.02f, 0.05f), new Color(1f, 0.2f, 0.65f), new Color(0.1f, 0.9f, 1f), 256, 8f, 6, 15f, false, 0.6f);
        yield return Neo("Light Cycles", FloorPattern.LightCycles, new Color(0f, 0.01f, 0.03f), new Color(1f, 0.55f, 0.1f), new Color(0.1f, 0.85f, 1f), 192, 6f, 10, 20f, false, 1f);
        yield return Neo("Arcade Carpet", FloorPattern.ArcadeCarpet, new Color(0.05f, 0.03f, 0.17f), new Color(1f, 0.2f, 0.65f), new Color(0.1f, 0.9f, 1f), 192, 6f, 8, 8f, false, 0.2f);
        // ---- 8-bit games ----
    }

    private static int WrapI(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

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
}
