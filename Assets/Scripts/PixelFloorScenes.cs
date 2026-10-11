using UnityEngine;
using static PixelNoise;

/// <summary>
/// The second batch of floor patterns (part of <see cref="PixelFloor"/>): space, digital / tech, trippy, horror, a noir smoker, a city skyline,
/// ground-and-sky scenes and the reworked battle crater. All drawn per texel in code like the others. 'Scene' patterns (galaxy, black hole, hyperspace,
/// glitch, hypno spiral, plasma, op art, noir smoker, skyline, ground + sky) are set to fit one copy to the view (<c>fitToView</c>) and use
/// <c>shadeAspect</c> so their shapes are not stretched; the rest tile.
/// </summary>
public partial class PixelFloor
{
    private static Color ShadeExtra(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.BattleCrater: return ShadeBattleCrater(s, u, v, x, y, n, cells);
            case FloorPattern.Galaxy: return ShadeGalaxy(s, u, v, x, y);
            case FloorPattern.BlackHole: return ShadeBlackHole(s, u, v);
            case FloorPattern.Hyperspace: return ShadeHyperspace(s, u, v, cells);
            case FloorPattern.DataStream: return ShadeDataStream(s, u, v, cells);
            case FloorPattern.CircuitFlow: return ShadeCircuitFlow(s, u, v, n, cells);
            case FloorPattern.CyberHex: return ShadeCyberHex(s, u, v, cells);
            case FloorPattern.Glitch: return ShadeGlitch(s, u, v, n);
            case FloorPattern.Psychedelic: return ShadePsychedelic(s, u, v);
            case FloorPattern.HypnoSpiral: return ShadeHypno(s, u, v, cells);
            case FloorPattern.Plasma: return ShadePlasma(s, u, v);
            case FloorPattern.OpArt: return ShadeOpArt(s, u, v, cells);
            case FloorPattern.BloodBoards: return ShadeBloodBoards(s, u, v, cells);
            case FloorPattern.HauntedTiles: return ShadeHauntedTiles(s, u, v, x, y, cells);
            case FloorPattern.Graveyard: return ShadeGraveyard(s, u, v, x, y);
            case FloorPattern.WatchingEyes: return ShadeEyes(s, u, v, cells);
            case FloorPattern.NoirSmoker: return ShadeSmoker(s, u, v, n);
            case FloorPattern.Skyline: return ShadeSkyline(s, u, v);
            case FloorPattern.GroundSky: return ShadeGroundSky(s, u, v, x, y, cells);
        }
        return s.colorA;
    }

    // ---- small helpers ----

    /// <summary>Smooth 0..1 step between two edges (the shader-style smoothstep; Mathf.SmoothStep is a different thing).</summary>
    private static float Sstep(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / Mathf.Max(1e-6f, e1 - e0));
        return t * t * (3f - 2f * t);
    }

    private static float ViewAspect() { return Mathf.Max(0.4f, shadeAspect); }

    /// <summary>Anti-aliased coverage (0..1) of an axis-aligned ellipse; 'texel' is the size of one texel in the same units.</summary>
    private static float ScEll(float px, float py, float cx, float cy, float rx, float ry, float texel)
    {
        float dx = (px - cx) / rx, dy = (py - cy) / ry;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) / texel + 0.5f);
    }

    /// <summary>Anti-aliased coverage of an axis-aligned box.</summary>
    private static float ScBox(float px, float py, float x0, float y0, float x1, float y1, float texel)
    {
        float dx = Mathf.Min(px - x0, x1 - px), dy = Mathf.Min(py - y0, y1 - py);
        return Mathf.Clamp01(Mathf.Min(dx, dy) / texel + 0.5f);
    }

    /// <summary>Distance from point p to the segment a-b.</summary>
    private static float ScSeg(float px, float py, float ax, float ay, float bx, float by, out float along)
    {
        float vx = bx - ax, vy = by - ay;
        float len2 = vx * vx + vy * vy;
        along = len2 > 1e-9f ? Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / len2) : 0f;
        float cx = ax + vx * along, cy = ay + vy * along;
        return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    private static Color ScMix(Color under, Color over, float a) { return Color.Lerp(under, over, Mathf.Clamp01(a)); }

    // ------------------------------------------------------------------
    // Battle crater (reworked): scorched, cracked ground with shaded bowls, burnt halos, rocks and glowing embers. Tiles.
    // ------------------------------------------------------------------

    private static Color ShadeBattleCrater(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 6, 4, s.seed));
        c *= 0.86f + 0.26f * Hash(x, y, s.seed);

        float e = 1.5f / n;
        Voronoi(u, v, cells, s.seed + 1, out float f1, out _, out int id);
        float d = f1 * cells;
        bool hasCrater = HashId(id, s.seed) > 0.22f;
        float radius = 0.2f + 0.2f * HashId(id, s.seed + 2);

        if (hasCrater)
        {
            // Burnt ground around the crater, fading outwards.
            float burn = Mathf.Clamp01(1f - (d - radius) / (radius * 1.7f));
            c = ScMix(c, s.colorC, burn * 0.5f);

            // Light from the upper left: the slope of the distance field tells which way the wall faces.
            Voronoi(u + e, v, cells, s.seed + 1, out float fx, out _, out _);
            Voronoi(u, v + e, cells, s.seed + 1, out float fy, out _, out _);
            float slopeX = (fx - f1) / e, slopeY = (fy - f1) / e;
            float lit = Mathf.Clamp(-slopeX * -0.6f + slopeY * -0.8f, -1f, 1f);

            if (d < radius)
            {
                float depth = d / radius;                                     // 0 at the middle, 1 at the rim
                Color bowl = Color.Lerp(s.colorC * 0.45f, s.colorB * 0.7f, depth);
                c = bowl * (1f + lit * 0.5f * Sstep(0.15f, 0.95f, depth));
            }
            else if (d < radius * 1.28f)
            {
                float rim = 1f - (d - radius) / (radius * 0.28f);              // raised lip
                c = ScMix(c, c * (1.25f - lit * 0.4f) + new Color(0.08f, 0.06f, 0.04f), rim);
            }
        }

        // Cracks in the scorched earth, a few of them glowing.
        Voronoi(u, v, cells * 2, s.seed + 5, out float g1, out float g2, out int cid);
        float crack = (g2 - g1) * cells * 2f;
        if (crack < 0.05f && d > radius * 1.1f)
        {
            float k = 1f - crack / 0.05f;
            c = ScMix(c, new Color(0.04f, 0.03f, 0.03f), k * 0.8f);
            if (HashId(cid, s.seed + 9) > 0.88f)
            {
                float flick = 0.7f + 0.3f * Mathf.Sin(shadeTime * 2f + cid);
                c = ScMix(c, new Color(1f, 0.45f, 0.1f), k * 0.9f * flick);
            }
        }

        // Scattered small rocks.
        if (Hash(x / 3, y / 3, s.seed + 11) > 0.965f) c = ScMix(c, s.colorB * 1.25f, 0.8f);
        return c;
    }

    // ------------------------------------------------------------------
    // Space
    // ------------------------------------------------------------------

    private static Color ShadeGalaxy(FloorStyle s, float u, float v, int x, int y)
    {
        float A = ViewAspect();
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float r = Mathf.Sqrt(px * px + py * py);
        float ang = Mathf.Atan2(py, px);
        float t = shadeTime;

        float phase = ang * 2f - Mathf.Log(r + 0.02f) * 5.5f + t * 0.06f;
        float arm = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase), 2.2f);
        float falloff = Mathf.Exp(-r * 3.4f);
        float dust = Fbm(u, v, 6, 4, s.seed);
        float body = arm * falloff * (0.55f + 0.9f * dust);

        Color c = s.colorA * (0.7f + 0.3f * Fbm(u, v, 3, 3, s.seed + 3));
        c += s.colorB * body * 1.6f;
        c += s.colorC * Mathf.Exp(-r * 11f) * 1.3f;
        if (Hash(x, y, s.seed + 5) > 0.992f) c += Color.white * (0.4f + 0.6f * Hash(y, x, s.seed));       // stars
        else if (Hash(x, y, s.seed + 6) > 0.9f && body > 0.12f) c += s.colorB * 0.35f;                     // bright knots in the arms
        c.a = 1f;
        return c;
    }

    private static Color ShadeBlackHole(FloorStyle s, float u, float v)
    {
        float A = ViewAspect();
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float r = Mathf.Sqrt(px * px + py * py);
        float t = shadeTime;

        // Background stars, bent outwards around the hole.
        float lens = 0.02f / (r * r + 0.01f);
        float lx = px + px / (r + 0.001f) * lens * 0.1f, ly = py + py / (r + 0.001f) * lens * 0.1f;
        int sx = Mathf.FloorToInt((lx + 1.5f) * 190f), sy = Mathf.FloorToInt((ly + 1.5f) * 190f);
        Color c = s.colorA;
        float star = Hash(sx, sy, s.seed);
        if (star > 0.985f) c += Color.white * (0.3f + 0.7f * Hash(sy, sx, s.seed + 1));

        // The tilted accretion disk (an ellipse) and the lensed copy of its far side arching over the hole.
        float er = Mathf.Sqrt(px * px + (py * 3.3f) * (py * 3.3f));
        float swirl = Frac(Mathf.Atan2(py * 3.3f, px) / (Mathf.PI * 2f) + t * 0.05f / (er + 0.15f));
        float tex = Fbm(swirl, er * 3f, 6, 3, s.seed + 2);
        Color diskColor = Color.Lerp(s.colorC, s.colorB, Sstep(0.18f, 0.5f, er)) * (0.5f + 0.9f * tex);
        diskColor *= 1f + 0.5f * Mathf.Clamp(-px / 0.4f, -1f, 1f);   // the side turning towards us is brighter
        float diskMask = Sstep(0.15f, 0.21f, er) * (1f - Sstep(0.42f, 0.52f, er));

        float arch = Mathf.Exp(-Mathf.Pow((r - 0.14f) / 0.035f, 2f)) * (py > 0f ? 1f : 0f) * (0.6f + 0.6f * tex);
        c += diskColor * arch * 1.4f;
        if (py < 0f || r > 0.11f) c = ScMix(c, diskColor, diskMask);

        float ring = Mathf.Exp(-Mathf.Pow((r - 0.098f) / 0.011f, 2f));
        c += s.colorC * ring * 1.2f;
        c = ScMix(c, Color.black, 1f - Sstep(0.082f, 0.092f, r));   // the shadow itself
        c.a = 1f;
        return c;
    }

    private static Color ShadeHyperspace(FloorStyle s, float u, float v, int cells)
    {
        float A = ViewAspect();
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float r = Mathf.Sqrt(px * px + py * py);
        float t = shadeTime;
        int streaks = Mathf.Max(40, cells * 12);
        float a01 = Mathf.Atan2(py, px) / (Mathf.PI * 2f) + 0.5f;
        float cell = a01 * streaks;
        int k = Mathf.FloorToInt(cell);
        float across = Mathf.Abs(Frac(cell) - 0.5f) * 2f;                      // 0 on the streak's centre line
        float h = Hash(k, 3, s.seed);
        float z = Frac(h * 7f + t * (0.25f + h * 0.5f));
        float head = z * z * 1.1f;                                              // streaks speed up as they near the edge
        float len = 0.03f + 0.35f * head;
        float along = (r - (head - len)) / Mathf.Max(0.001f, len);
        float width = 1f - Sstep(0.15f + 0.4f * head, 0.9f, across);
        float strength = (along > 0f && along < 1f) ? along * along * width : 0f;
        Color c = Color.Lerp(s.colorA, s.colorB * 0.4f, Mathf.Exp(-r * 6f));
        Color streak = Color.Lerp(s.colorB, s.colorC, h);
        c += streak * strength * 1.6f;
        c += Color.white * Mathf.Exp(-r * 22f) * 0.9f;
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Digital / tech
    // ------------------------------------------------------------------

    private static Color ShadeDataStream(FloorStyle s, float u, float v, int cells)
    {
        float A = ViewAspect();
        float t = shadeTime;
        int lanes = Mathf.Max(6, cells);
        float lv = v * lanes;
        int lane = Mathf.FloorToInt(lv);
        float lf = Frac(lv);
        float dirSign = Hash(lane, 2, s.seed) > 0.5f ? 1f : -1f;
        float speed = (0.15f + 0.5f * Hash(lane, 1, s.seed)) * dirSign;
        float xc = u * A * 18f + t * speed * 4f;
        int ci = Mathf.FloorToInt(xc);
        float cf = Frac(xc);

        Color c = s.colorA * (0.85f + 0.2f * Hash(lane, 8, s.seed));
        if (lf < 0.05f || lf > 0.95f) c = ScMix(c, s.colorB, 0.12f);                              // lane rails
        float on = Hash(ci, lane + 50, s.seed);
        if (on > 0.5f && lf > 0.2f && lf < 0.8f && cf > 0.08f && cf < 0.92f)
        {
            float pick = Hash(ci, lane + 9, s.seed);
            Color packet = pick < 0.6f ? s.colorB : pick < 0.88f ? s.colorC : Color.white;
            float shine = 0.55f + 0.45f * Hash(ci, lane + 33, s.seed);
            // The leading edge (in the direction of travel) is brighter.
            float lead = dirSign > 0f ? cf : 1f - cf;
            c = packet * (shine * (0.6f + 0.6f * lead));
        }
        else if (on > 0.5f && lf > 0.35f && lf < 0.65f) c = ScMix(c, s.colorB, 0.25f);              // glow smear between packets
        c.a = 1f;
        return c;
    }

    private static Color ShadeCircuitFlow(FloorStyle s, float u, float v, int n, int cells)
    {
        float t = shadeTime;
        float gu = u * cells, gv = v * cells;
        int ci = Mathf.FloorToInt(gu), cj = Mathf.FloorToInt(gv);
        float fu = gu - ci, fv = gv - cj;
        float texel = cells / (float)n;                                    // one texel in cell units
        int wi = Wrap(ci, cells), wj = Wrap(cj, cells);
        int wl = Wrap(ci - 1, cells), wd = Wrap(cj - 1, cells);

        Color c = s.colorA * (0.9f + 0.15f * Fbm(u, v, 5, 3, s.seed));
        float trace = 0f, pulse = 0f;
        float wTrace = 1.3f * texel;

        // Right edge of this node and the left half of it (the edge from the left neighbour).
        bool right = Hash(wi, wj, s.seed) > 0.42f, left = Hash(wl, wj, s.seed) > 0.42f;
        bool up = Hash(wi, wj, s.seed + 5) > 0.42f, down = Hash(wi, wd, s.seed + 5) > 0.42f;
        if (Mathf.Abs(fv - 0.5f) < wTrace)
        {
            if (right && fu >= 0.5f) { trace = 1f; pulse = Pulse(fu - 0.5f, 1f, Hash(wi, wj, s.seed + 20), t); }
            if (left && fu < 0.5f) { trace = 1f; pulse = Mathf.Max(pulse, Pulse(fu + 0.5f, 1f, Hash(wl, wj, s.seed + 20), t)); }
        }
        if (Mathf.Abs(fu - 0.5f) < wTrace)
        {
            if (up && fv >= 0.5f) { trace = 1f; pulse = Mathf.Max(pulse, Pulse(fv - 0.5f, 1f, Hash(wi, wj, s.seed + 21), t)); }
            if (down && fv < 0.5f) { trace = 1f; pulse = Mathf.Max(pulse, Pulse(fv + 0.5f, 1f, Hash(wi, wd, s.seed + 21), t)); }
        }
        float nd = Mathf.Sqrt((fu - 0.5f) * (fu - 0.5f) + (fv - 0.5f) * (fv - 0.5f));
        bool hasNode = right || left || up || down;
        if (hasNode && nd < 0.1f) { trace = 1f; if (nd < 0.05f) c = ScMix(c, s.colorB * 0.4f, 0.6f); }

        if (trace > 0f) c = ScMix(c, s.colorB * 0.7f, 0.85f);
        c += s.colorC * pulse * trace * 1.4f;
        c.a = 1f;
        return c;
    }

    /// <summary>A pulse running along an edge: 'pos' is how far along the edge (0..1 of 'length'), the head position cycles with time.</summary>
    private static float Pulse(float pos, float length, float phase, float t)
    {
        float p = pos / length;
        float head = Frac(t * 0.35f + phase);
        float d = Mathf.Abs(p - head);
        d = Mathf.Min(d, 1f - d);
        return Mathf.Exp(-d * d * 260f);
    }

    private static Color ShadeCyberHex(FloorStyle s, float u, float v, int cells)
    {
        float t = shadeTime;
        HexCells(u, v, cells, s.seed, out float f1, out float f2, out int id);
        float edge = (f2 - f1) * cells;
        float ph = HashId(id, s.seed) * 6.2832f;
        float glowCell = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 1.3f + ph), 8f);
        Color c = s.colorA * (0.8f + 0.2f * HashId(id, s.seed + 4));
        c += s.colorC * glowCell * 0.55f;
        float line = Mathf.Clamp01(1f - edge / 0.09f);
        c = ScMix(c, s.colorB, line * (0.55f + 0.45f * glowCell));
        float scan = Mathf.Clamp01(1f - Mathf.Abs(Frac(v - t * 0.07f) - 0.5f) / 0.03f);                 // a bright band sweeping across
        c += s.colorB * scan * 0.18f * (0.5f + line);
        c.a = 1f;
        return c;
    }

    private static Color GlitchBase(FloorStyle s, float u, float v, int n)
    {
        int bar = Mathf.FloorToInt(Frac(u) * 8f);
        Color c = Color.HSVToRGB(Frac(bar * 0.137f + 0.55f), 0.75f, 0.85f);
        c = ScMix(c, s.colorB, 0.3f);
        c = ScMix(s.colorA, c, 0.35f + 0.65f * Sstep(0.15f, 0.7f, Fbm(u, v, 4, 3, s.seed)));
        if ((Mathf.FloorToInt(v * n) & 1) == 0) c *= 0.82f;                  // scanlines
        return c;
    }

    private static Color ShadeGlitch(FloorStyle s, float u, float v, int n)
    {
        float t = shadeTime;
        int slot = Mathf.FloorToInt(t * 7f);
        int row = Mathf.FloorToInt(v * 26f);
        float g = Hash(row, slot, s.seed);
        float off = 0f, ca = 0.004f;
        if (g > 0.82f) { off = (Hash(row, slot + 99, s.seed) - 0.5f) * 0.28f; ca = 0.025f; }
        float uu = Frac(u + off);
        Color r = GlitchBase(s, uu + ca, v, n), gr = GlitchBase(s, uu, v, n), b = GlitchBase(s, uu - ca, v, n);
        Color c = new Color(r.r, gr.g, b.b, 1f);
        if (Hash(Mathf.FloorToInt(u * 40f), row + slot * 3, s.seed + 7) > 0.985f) c = Color.white;           // stray bright blocks
        return c;
    }

    // ------------------------------------------------------------------
    // Trippy
    // ------------------------------------------------------------------

    private static Color ShadePsychedelic(FloorStyle s, float u, float v)
    {
        float A = ViewAspect();
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float r = Mathf.Sqrt(px * px + py * py), ang = Mathf.Atan2(py, px);
        float t = shadeTime;
        float q = Fbm(u + t * 0.01f, v, 3, 3, s.seed);
        float hue = Frac(r * 2.4f - t * 0.12f + q * 1.6f + Mathf.Sin(ang * 3f + t * 0.4f) * 0.12f);
        float val = 0.72f + 0.28f * Mathf.Sin(r * 38f - t * 2f + q * 6f);
        Color c = Color.HSVToRGB(hue, 0.88f, val);
        c = ScMix(c, s.colorA, 0.12f);
        c.a = 1f;
        return c;
    }

    private static Color ShadeHypno(FloorStyle s, float u, float v, int cells)
    {
        float A = ViewAspect();
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float r = Mathf.Sqrt(px * px + py * py), ang = Mathf.Atan2(py, px);
        float t = shadeTime;
        float arms = Mathf.Max(2, cells);
        float band = Frac(ang / (Mathf.PI * 2f) * arms + Mathf.Log(r + 0.002f) * 1.6f - t * 0.25f);
        float m = Sstep(0.38f, 0.62f, Mathf.Abs(band * 2f - 1f));
        Color c = Color.Lerp(s.colorA, s.colorB, m);
        c = ScMix(c, s.colorC, Mathf.Exp(-r * 30f));
        c *= 0.55f + 0.45f * Mathf.Clamp01(1f - r * 0.9f);
        c.a = 1f;
        return c;
    }

    private static Color ShadePlasma(FloorStyle s, float u, float v)
    {
        float A = ViewAspect();
        float x = (u - 0.5f) * A * 6f, y = (v - 0.5f) * 6f;
        float t = shadeTime;
        float val = Mathf.Sin(x * 1.7f + t) + Mathf.Sin(y * 1.9f + t * 1.3f) + Mathf.Sin((x + y) * 1.2f + t * 0.7f)
                  + Mathf.Sin(Mathf.Sqrt(x * x + y * y) * 2.4f - t * 1.8f);
        float k = Mathf.Abs(2f * Frac(val * 0.125f + 0.5f) - 1f);
        Color c = k < 0.5f ? Color.Lerp(s.colorA, s.colorB, k * 2f) : Color.Lerp(s.colorB, s.colorC, (k - 0.5f) * 2f);
        c.a = 1f;
        return c;
    }

    private static Color ShadeOpArt(FloorStyle s, float u, float v, int cells)
    {
        float A = ViewAspect();
        float x = (u - 0.5f) * A, y = v - 0.5f;
        float t = shadeTime;
        float r = Mathf.Sqrt(x * x + y * y);
        float bulge = 1f + 1.6f * Mathf.Exp(-r * r * 9f);
        float sx = x * bulge + 0.035f * Mathf.Sin(y * 14f + t * 1.2f);
        float sy = y * bulge + 0.035f * Mathf.Sin(x * 14f - t * 0.9f);
        int ix = Mathf.FloorToInt(sx * cells * 2f), iy = Mathf.FloorToInt(sy * cells * 2f);
        Color c = ((ix + iy) & 1) == 0 ? s.colorA : s.colorB;
        c *= 0.55f + 0.45f * Mathf.Exp(-r * r * 4f) + 0.15f;
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Horror
    // ------------------------------------------------------------------

    private static Color ShadeBloodBoards(FloorStyle s, float u, float v, int cells)
    {
        int plank = Mathf.FloorToInt(v * cells);
        float pv = Frac(v * cells);
        float shift = Hash(plank, 1, s.seed);
        float pu = Frac(u * 3f + shift);
        float grain = Fbm(u, v, 4, cells * 2, 3, s.seed);
        Color wood = Color.Lerp(s.colorA, s.colorB, grain) * (0.85f + 0.2f * Hash(plank, 4, s.seed));
        if (pv < 0.06f || pv > 0.985f) wood = s.colorA * 0.25f;                 // seams
        else if (pu < 0.012f) wood = s.colorA * 0.3f;                           // end joints

        float m = Fbm(u, v, 3, 3, s.seed + 4);
        float drip = Fbm(u, v, 26, 3, 2, s.seed + 6);
        float pool = Sstep(0.54f, 0.6f, m + (drip - 0.5f) * 0.14f);
        Color blood = s.colorC * (0.55f + 0.35f * grain);
        Color c = ScMix(wood, blood, pool * 0.92f);
        float rim = pool * (1f - pool) * 4f;                                    // a wet shine on the pool's edge
        c += new Color(0.5f, 0.12f, 0.1f) * rim * 0.35f;
        c.a = 1f;
        return c;
    }

    private static Color ShadeHauntedTiles(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        float t = shadeTime;
        int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
        Color c = ((cx + cy) & 1) == 0 ? s.colorA : s.colorB;
        float grime = Fbm(u, v, 5, 4, s.seed);
        c *= 0.55f + 0.6f * grime;
        float fu = Frac(u * cells), fv = Frac(v * cells);
        if (Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)) < 0.02f) c *= 0.4f;     // grout
        Voronoi(u, v, cells, s.seed + 3, out float f1, out float f2, out int id);
        if ((f2 - f1) * cells < 0.03f && HashId(id, s.seed) > 0.55f) c *= 0.3f;                // cracks in some tiles
        // Dried stains.
        float stain = Sstep(0.62f, 0.7f, Fbm(u, v, 3, 3, s.seed + 8));
        c = ScMix(c, s.colorC * 0.6f, stain * 0.7f);
        // The light flickers now and then.
        float flicker = Hash(Mathf.FloorToInt(t * 9f), 1, s.seed) > 0.93f ? 0.45f : 1f;
        c *= flicker * (0.9f + 0.1f * Mathf.Sin(t * 2f));
        c.a = 1f;
        return c;
    }

    private static Color ShadeGraveyard(FloorStyle s, float u, float v, int x, int y)
    {
        float t = shadeTime;
        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 6, 4, s.seed));
        c *= 0.82f + 0.3f * Hash(x, y, s.seed);
        if (Hash(x, y, s.seed + 3) > 0.965f) c = ScMix(c, new Color(0.36f, 0.34f, 0.16f), 0.8f);          // dead grass
        if (Hash(x / 2, y / 2, s.seed + 5) > 0.992f) c = ScMix(c, new Color(0.8f, 0.78f, 0.7f), 0.8f);   // bits of bone
        float fog = Sstep(0.42f, 0.82f, Fbm(u + t * 0.012f, v - t * 0.008f, 3, 4, s.seed + 8));
        c = ScMix(c, s.colorC, fog * 0.6f);
        c.a = 1f;
        return c;
    }

    private static Color ShadeEyes(FloorStyle s, float u, float v, int cells)
    {
        float t = shadeTime;
        float gu = u * cells, gv = v * cells;
        int ci = Mathf.FloorToInt(gu), cj = Mathf.FloorToInt(gv);
        float fu = gu - ci, fv = gv - cj;
        int wi = Wrap(ci, cells), wj = Wrap(cj, cells);

        Color c = s.colorA * (0.7f + 0.6f * Fbm(u + t * 0.01f, v, 4, 3, s.seed));
        float h = Hash(wi, wj, s.seed);
        if (h > 0.4f)
        {
            float cx = 0.5f + (Hash(wi, wj, s.seed + 1) - 0.5f) * 0.3f;
            float cy = 0.5f + (Hash(wi, wj, s.seed + 2) - 0.5f) * 0.3f;
            float ph = Frac(t * 0.07f + h * 3f);
            float open = Mathf.Clamp01(Mathf.Min(ph / 0.04f, (0.86f - ph) / 0.04f));
            if (open > 0f)
            {
                float best = 99f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float dx = (fu - (cx + side * 0.13f)) / 0.075f, dy = (fv - cy) / (0.04f * open + 0.001f);
                    // Almond-ish eye: the vertical radius shrinks towards the corners.
                    float d = Mathf.Sqrt(dx * dx + dy * dy * (1f + Mathf.Abs(dx) * 0.8f));
                    if (d < best) best = d;
                    if (d < 1f && Mathf.Abs(fu - (cx + side * 0.13f)) < 0.01f) best = 0.99f + 0.0f;
                }
                float core = 1f - Sstep(0.65f, 1f, best);
                float halo = Mathf.Exp(-best * best * 1.6f) * 0.4f;
                c += s.colorC * (core + halo);
            }
        }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Noir smoker: a glamorous profile silhouette in a smoky room, a cigarette with a glowing ember and a curl of smoke rising.
    // ------------------------------------------------------------------

    private static Color ShadeSmoker(FloorStyle s, float u, float v, int n)
    {
        float A = ViewAspect();
        float px = (u - 0.5f) * A, py = v - 0.5f;
        float texel = 1f / n;
        float t = shadeTime;

        // Room: dark blue-green with slanting light from window blinds.
        float blinds = Sstep(0.3f, 0.5f, Mathf.Abs(Frac((py + px * 0.4f) * 8f) - 0.5f) * 2f);
        Color c = Color.Lerp(s.colorA * 0.45f, s.colorA * 1.35f, blinds * Mathf.Clamp01(0.6f + px * 0.8f));
        c *= 1f - 0.5f * Mathf.Clamp01((Mathf.Abs(px) / A * 2f) * 0.9f + Mathf.Abs(py) * 0.5f - 0.25f);     // vignette

        Color skin = s.colorB, lips = s.colorC;
        Color hair = new Color(0.04f, 0.035f, 0.05f);
        float light = Mathf.Clamp01((-px + 0.05f) * 4f);                                     // lit from the left

        // Back hair, falling behind the shoulders.
        float backHair = Mathf.Max(ScEll(px, py, 0.06f, 0.02f, 0.15f, 0.27f, texel), ScEll(px, py, 0.1f, -0.2f, 0.12f, 0.2f, texel));
        c = ScMix(c, hair * (0.8f + 0.5f * Mathf.Clamp01(Fbm(u, v, 6, 3, s.seed) - 0.3f)), backHair);

        // Dress / shoulders (dark with a red edge) and the neck.
        Color dress = Color.Lerp(new Color(0.07f, 0.04f, 0.06f), s.colorC * 0.5f, Mathf.Clamp01(light * 0.7f));
        c = ScMix(c, dress, ScEll(px, py, 0.03f, -0.4f, 0.34f, 0.26f, texel));
        c = ScMix(c, skin * (0.55f + 0.35f * light), ScBox(px, py, -0.045f, -0.17f, 0.055f, 0.03f, texel));
        c = ScMix(c, skin * (0.6f + 0.35f * light), ScEll(px, py, 0f, -0.2f, 0.085f, 0.07f, texel) * 0.9f);      // collarbone

        // Face in profile, looking left: head, nose, chin, lips.
        float face = Mathf.Max(ScEll(px, py, 0f, 0.12f, 0.13f, 0.17f, texel), ScEll(px, py, -0.128f, 0.113f, 0.028f, 0.032f, texel));
        face = Mathf.Max(face, ScEll(px, py, -0.083f, -0.012f, 0.058f, 0.05f, texel));
        face = Mathf.Max(face, ScEll(px, py, -0.1f, 0.02f, 0.045f, 0.05f, texel));
        c = ScMix(c, skin * (0.62f + 0.42f * light), face);
        c = ScMix(c, lips, ScEll(px, py, -0.122f, 0.047f, 0.026f, 0.012f, texel) * face);
        c = ScMix(c, new Color(0.03f, 0.02f, 0.03f), ScEll(px, py, -0.07f, 0.148f, 0.032f, 0.0065f, texel) * face);   // the eye
        c = ScMix(c, new Color(0.03f, 0.02f, 0.03f), ScEll(px, py, -0.078f, 0.172f, 0.04f, 0.005f, texel) * face);   // brow

        // Front hair: a swept fringe over the forehead and the crown.
        float front = ScEll(px, py, 0.04f, 0.2f, 0.15f, 0.17f, texel) * Sstep(-0.06f - (py - 0.2f) * 0.5f, -0.04f - (py - 0.2f) * 0.5f, px);
        c = ScMix(c, hair * (1f + 0.6f * Mathf.Exp(-Mathf.Pow((px - 0.0f) / 0.04f, 2f))), front);

        // Cigarette held in the fingers.
        float along;
        float dCig = ScSeg(px, py, -0.14f, 0.044f, -0.31f, 0.078f, out along);
        float cig = Mathf.Clamp01((0.0045f - dCig) / texel + 0.5f);
        Color paper = along > 0.86f ? new Color(1f, 0.5f, 0.12f) : new Color(0.95f, 0.94f, 0.9f);
        // Hand: two finger blobs around the cigarette.
        float hand = Mathf.Max(ScEll(px, py, -0.2f, 0.045f, 0.028f, 0.042f, texel), ScEll(px, py, -0.19f, 0.07f, 0.02f, 0.03f, texel));
        c = ScMix(c, paper, cig);
        c = ScMix(c, skin * (0.7f + 0.3f * light), hand * 0.97f);
        float ember = Mathf.Exp(-(Mathf.Pow(px + 0.31f, 2f) + Mathf.Pow(py - 0.078f, 2f)) / 0.0007f) * (0.75f + 0.25f * Mathf.Sin(t * 5f));
        c += new Color(1f, 0.5f, 0.15f) * ember * 1.1f;

        // Smoke: a curling plume from the tip that widens, drifts and fades as it rises.
        float sy = py - 0.078f;
        if (sy > 0f)
        {
            float cx = -0.31f + Mathf.Sin(sy * 15f - t * 1.4f) * 0.028f * Mathf.Min(1f, sy * 6f) + sy * 0.1f;
            float w = 0.008f + sy * 0.2f;
            float dens = Mathf.Exp(-((px - cx) * (px - cx)) / (w * w)) * Mathf.Exp(-sy * 1.7f);
            dens *= 0.35f + 0.9f * Fbm(u * 2f - t * 0.02f, v * 2f - t * 0.07f, 4, 3, s.seed + 7);
            c = ScMix(c, new Color(0.86f, 0.88f, 0.92f), Mathf.Clamp01(dens) * 0.8f);
        }
        // A thin exhale drifting away from the lips.
        float ex = px + 0.15f, ey = py - 0.05f;
        if (ex < 0f)
        {
            float spread = 0.01f + (-ex) * 0.22f;
            float wob = Mathf.Sin(-ex * 18f - t * 1.2f) * 0.012f;
            float dens2 = Mathf.Exp(-((ey - wob - (-ex) * 0.1f) * (ey - wob - (-ex) * 0.1f)) / (spread * spread)) * Mathf.Exp(ex * 2.6f);
            dens2 *= 0.25f + 0.8f * Fbm(u * 2f - t * 0.03f, v * 2f, 4, 3, s.seed + 9);
            c = ScMix(c, new Color(0.8f, 0.82f, 0.88f), Mathf.Clamp01(dens2) * 0.45f);
        }

        c += Color.white * ((Hash(Mathf.FloorToInt(u * n), Mathf.FloorToInt(v * n), s.seed + Mathf.FloorToInt(t * 12f)) - 0.5f) * 0.05f);   // film grain
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // City skyline at night, mirrored in water below.
    // ------------------------------------------------------------------

    private const float SkylineHorizon = 0.34f;

    /// <summary>The sky and the three layers of buildings (everything above the waterline) at height v.</summary>
    private static Color SkylineAbove(FloorStyle s, float u, float v, float A, float t)
    {
        float k = Mathf.Clamp01((v - SkylineHorizon) / (1f - SkylineHorizon));
        Color c = Color.Lerp(s.colorB, s.colorA, Mathf.Pow(k, 0.7f));
        float x = u * A;

        if (v > 0.5f)
        {
            int sx = Mathf.FloorToInt(u * 230f), sy = Mathf.FloorToInt(v * 230f);
            if (Hash(sx, sy, s.seed) > 0.992f) c = ScMix(c, Color.white, 0.8f * k);
        }
        float md = Mathf.Sqrt((x - A * 0.78f) * (x - A * 0.78f) + (v - 0.8f) * (v - 0.8f));                 // the moon
        c += new Color(0.9f, 0.85f, 0.7f) * Mathf.Exp(-md * 8f) * 0.22f;
        c = ScMix(c, new Color(1f, 0.97f, 0.86f), 1f - Sstep(0.05f, 0.056f, md));

        for (int layer = 0; layer < 3; layer++)
        {
            float w = layer == 0 ? 0.045f : layer == 1 ? 0.07f : 0.11f;
            float xs = x + layer * 3.7f;
            int col = Mathf.FloorToInt(xs / w);
            float fx = xs / w - col;
            float h = Hash(col, layer + 1, s.seed);
            float top = SkylineHorizon + (0.06f + 0.2f * h) * (0.8f + 0.25f * layer);
            if (h > 0.92f && fx > 0.47f && fx < 0.53f) top += 0.05f;                                         // an antenna
            if (v >= top || v < SkylineHorizon - 0.02f) continue;

            Color body = Color.Lerp(Color.Lerp(s.colorC, s.colorB, 0.55f), s.colorC * 0.5f, layer * 0.5f);
            int wc = Mathf.FloorToInt(fx * 4f);
            float wxf = Frac(fx * 4f);
            float row = (v - SkylineHorizon) / 0.022f;
            int wr = Mathf.FloorToInt(row);
            float wyf = Frac(row);
            bool windowShape = wxf > 0.25f && wxf < 0.75f && wyf > 0.3f && wyf < 0.75f && v < top - 0.012f && fx > 0.05f && fx < 0.95f;
            if (windowShape)
            {
                float hw = Hash(wc + col * 7, wr, s.seed + layer);
                bool lit = hw > 0.58f;
                if (Hash(wc + col * 7, wr + Mathf.FloorToInt(t * 0.2f) * 5, s.seed + 40) > 0.97f) lit = !lit;   // a few windows flip on and off
                c = lit ? new Color(1f, 0.86f, 0.5f) * (0.6f + 0.2f * layer + 0.3f * hw) : body * 0.8f;
            }
            else c = body * (fx < 0.06f ? 0.8f : 1f);
        }
        return c;
    }

    private static Color ShadeSkyline(FloorStyle s, float u, float v)
    {
        float A = ViewAspect();
        float t = shadeTime;
        Color c;
        if (v >= SkylineHorizon) c = SkylineAbove(s, u, v, A, t);
        else
        {
            // The water: the skyline upside down, rippled and darker, with a dark base.
            float depth = (SkylineHorizon - v) / SkylineHorizon;
            float ripple = Mathf.Sin(v * 110f + t * 1.6f) * 0.006f * (0.3f + depth) + (Fbm(u, v, 8, 3, s.seed) - 0.5f) * 0.01f;
            Color refl = SkylineAbove(s, u + ripple, 2f * SkylineHorizon - v + 0.015f, A, t);
            c = ScMix(refl * 0.55f, s.colorC * 0.3f, 0.35f + 0.4f * depth);
            c += Color.white * Sstep(0.985f, 1f, Mathf.Sin(v * 220f + u * 40f + t)) * 0.05f;
        }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Ground and sky scenes (variant = 'cells' 1..4: meadow, desert, arctic, alien twin moons)
    // ------------------------------------------------------------------

    private static Color ShadeGroundSky(FloorStyle s, float u, float v, int x, int y, int variant)
    {
        float A = ViewAspect();
        float t = shadeTime;
        float horizon = 0.46f;
        float px = (u - 0.5f) * A;
        variant = Mathf.Clamp(variant - 1, 0, 3);   // cells 1..4

        // Mountains / dunes / hills along the horizon.
        float ridgeNoise = Fbm(Frac(u * 1.0f), 0.37f, 5, 4, s.seed + 20);
        float ridge = horizon + 0.015f + (variant == 1 ? 0.045f * Fbm(u, 0.2f, 2, 2, s.seed + 21) : variant == 3 ? 0.14f * Mathf.Pow(ridgeNoise, 1.6f) : 0.1f * ridgeNoise);

        if (v >= horizon)
        {
            float k = Mathf.Clamp01((v - horizon) / (1f - horizon));
            Color c = Color.Lerp(s.colorB, s.colorA, Mathf.Pow(k, 0.65f));

            if (variant == 3 && v > 0.55f && Hash(x, y, s.seed) > 0.993f) c = ScMix(c, Color.white, 0.8f);   // stars in an alien sky
            if (variant == 2)
            {
                float aur = Mathf.Exp(-Mathf.Pow((v - 0.78f - 0.05f * Mathf.Sin(px * 5f + t * 0.2f)) / 0.06f, 2f)) * (0.4f + 0.8f * Fbm(u, v, 4, 3, s.seed + 30));
                c += Color.Lerp(new Color(0.2f, 1f, 0.6f), new Color(0.8f, 0.3f, 1f), Mathf.Clamp01((v - 0.7f) * 5f)) * aur * 0.7f;
            }
            if (variant != 1)
            {
                float cl = Sstep(0.52f, 0.78f, Fbm(u + t * 0.004f, v * 1.0f, 3, 5, 3, s.seed + 40)) * (1f - k * 0.4f);
                c = ScMix(c, variant == 3 ? new Color(0.65f, 0.45f, 0.8f) : Color.white, cl * (variant == 3 ? 0.35f : 0.8f));
            }

            // Sun / moons.
            if (variant == 0) c = Sun(c, px, v, 0.3f, 0.74f, 0.045f, new Color(1f, 0.96f, 0.75f));
            else if (variant == 1) c = Sun(c, px, v, -0.2f, horizon + 0.09f, 0.11f, new Color(1f, 0.55f, 0.15f));
            else if (variant == 2) c = Sun(c, px, v, 0.35f, horizon + 0.05f, 0.04f, new Color(1f, 0.9f, 0.8f));
            else
            {
                c = Sun(c, px, v, -0.3f, 0.78f, 0.1f, new Color(0.85f, 0.75f, 1f));
                c = Sun(c, px, v, 0.34f, 0.66f, 0.04f, new Color(1f, 0.7f, 0.6f));
            }

            if (v < ridge)
            {
                Color far = Color.Lerp(s.colorB, s.colorC, variant == 1 ? 0.45f : 0.3f);
                c = far * 0.9f;
                if (variant == 2 && v > ridge - 0.03f) c = ScMix(c, Color.white, 0.85f);                   // snow caps
            }
            c.a = 1f;
            return c;
        }

        // Ground in perspective: the further away, the more squashed and hazy.
        float z = 1f / (horizon - v + 0.03f);
        float gx = px * z * 0.5f, gy = z * 0.6f;
        float haze = Sstep(7f, 30f, z);
        Color g;
        switch (variant)
        {
            case 0:
            {
                g = Frac(gy * 0.5f) < 0.5f ? s.colorC : s.colorC * 0.86f;                                    // mown stripes
                g *= 0.92f + 0.14f * Hash(Mathf.FloorToInt(gx * 6f), Mathf.FloorToInt(gy * 6f), s.seed);
                break;
            }
            case 1:
            {
                float dune = Fbm(gx * 0.12f + 0.5f, gy * 0.12f, 6, 3, s.seed + 50);
                g = Color.Lerp(s.colorC, s.colorC * 0.7f, dune);
                g *= 0.92f + 0.12f * Mathf.Sin(gy * 5f + gx * 0.8f + dune * 6f);
                break;
            }
            case 2:
            {
                g = Color.Lerp(s.colorC, s.colorC * 0.82f, Fbm(gx * 0.1f + 0.5f, gy * 0.1f, 6, 3, s.seed + 50));
                if (Hash(x, y, s.seed + 60) > 0.994f) g = Color.white;                                       // sparkles
                break;
            }
            default:
            {
                float veins = Fbm(gx * 0.12f + 0.5f, gy * 0.12f, 6, 3, s.seed + 55);
                g = s.colorC * (0.8f + 0.3f * Fbm(gx * 0.2f + 0.5f, gy * 0.2f, 6, 3, s.seed + 56));
                float glow = Sstep(0.6f, 0.66f, veins) * (1f - haze);
                g += new Color(0.25f, 1f, 0.75f) * glow * (0.7f + 0.3f * Mathf.Sin(t * 1.5f + veins * 20f));  // glowing veins
                break;
            }
        }
        g = ScMix(g, s.colorB, haze * 0.8f);
        g.a = 1f;
        return g;
    }

    private static Color Sun(Color under, float px, float v, float cx, float cy, float radius, Color colour)
    {
        float d = Mathf.Sqrt((px - cx) * (px - cx) + (v - cy) * (v - cy));
        under += colour * Mathf.Exp(-d / (radius * 2.2f)) * 0.3f;
        return ScMix(under, colour, 1f - Sstep(radius * 0.93f, radius, d));
    }
}
