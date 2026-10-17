using System.Collections.Generic;
using UnityEngine;
using static PixelNoise;

/// <summary>
/// The fourth batch of floor patterns (part of <see cref="PixelFloor"/>): ten big, still, non-repeating floors drawn once at 1024 px over ~40 world units (like Moon Surface / Graveyard:
/// <c>stretchToFloor</c>, time-sliced fill). Everything is drawn in WORLD units (<c>tileWorldSize</c> = how far the picture spans) so a feature keeps its size at any resolution:
/// Lush Grass, Nebula Field, Still Pond, Graffiti Wall, Leaf Litter, Zen Garden, Coral Reef (+ the Caustics overlay), Casino Felt, Stained Glass (Persian Rug was removed).
/// Dispatched from <see cref="ShadeRetro"/>'s fall-through (<see cref="ShadeWorlds"/>); styles come from <see cref="WorldStyles"/>.
/// </summary>
public partial class PixelFloor
{
    private static Color ShadeWorlds(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.LushGrass: return ShadeLushGrass(s, u, v, x, y, n);
            case FloorPattern.NebulaField: return ShadeNebula(s, u, v, x, y, n);
            case FloorPattern.StillPond: return ShadePond(s, u, v, x, y, n);
            case FloorPattern.GraffitiWall: return ShadeGraffiti(s, u, v, x, y, n);
            case FloorPattern.LeafLitter: return ShadeLeaves(s, u, v, x, y, n);
            case FloorPattern.ZenGarden: return ShadeZen(s, u, v, x, y, n);
            case FloorPattern.CoralReef: return ShadeReef(s, u, v, x, y, n);
            case FloorPattern.CasinoFelt: return ShadeCasino(s, u, v, x, y, n);
            case FloorPattern.StainedGlass: return ShadeGlass(s, u, v, x, y, n);
        }
        return ShadeLive(s, u, v, x, y, n);   // the fifth batch (PixelFloorLive.cs)
    }

    private static IEnumerable<FloorStyle> WorldStyles()
    {
        FloorStyle W(string name, FloorPattern p, Color a, Color b, Color c, int tile, float smooth, float glow = 0f, int seed = 1)
        {
            FloorStyle f = S(name, p, a, b, c, 1024, false, tile, 1, smooth, glow);
            f.stretchToFloor = true; f.detail = 6; f.seed = seed;
            f.tiltCorrect = p == FloorPattern.StainedGlass || p == FloorPattern.CasinoFelt || p == FloorPattern.NebulaField;   // (round things in these look squashed from the tilted base view)
            return f;
        }
        yield return W("Lush Grass", FloorPattern.LushGrass, new Color(0.12f, 0.36f, 0.1f), new Color(0.38f, 0.66f, 0.2f), new Color(1f, 0.95f, 0.6f), 40, 0.1f, 0f, 3);
        yield return W("Nebula Field", FloorPattern.NebulaField, new Color(0.01f, 0.01f, 0.05f), new Color(0.6f, 0.15f, 0.7f), new Color(0.1f, 0.5f, 0.95f), 40, 0.1f, 0.35f, 5);
        yield return W("Still Pond", FloorPattern.StillPond, new Color(0.02f, 0.2f, 0.28f), new Color(0.25f, 0.7f, 0.65f), new Color(0.25f, 0.6f, 0.2f), 40, 0.75f, 0f, 7);
        yield return W("Graffiti Wall", FloorPattern.GraffitiWall, new Color(0.55f, 0.22f, 0.15f), new Color(0.72f, 0.38f, 0.25f), new Color(0.55f, 0.52f, 0.48f), 40, 0.15f, 0f, 9);
        yield return W("Leaf Litter", FloorPattern.LeafLitter, new Color(0.14f, 0.09f, 0.05f), new Color(0.85f, 0.35f, 0.08f), new Color(0.95f, 0.75f, 0.15f), 40, 0.1f, 0f, 11);
        yield return W("Zen Garden", FloorPattern.ZenGarden, new Color(0.86f, 0.8f, 0.66f), new Color(0.42f, 0.42f, 0.44f), new Color(0.28f, 0.45f, 0.18f), 40, 0.1f, 0f, 13);
        FloorStyle reef = W("Coral Reef", FloorPattern.CoralReef, new Color(0.85f, 0.8f, 0.62f), new Color(0.95f, 0.4f, 0.5f), new Color(0.5f, 0.25f, 0.75f), 40, 0.7f, 0f, 17);
        reef.overlay = FloorOverlay.Caustics;   // (the moving caustic light is drawn live over the still picture)
        yield return reef;
        yield return W("Casino Felt", FloorPattern.CasinoFelt, new Color(0.04f, 0.32f, 0.17f), new Color(0.9f, 0.75f, 0.3f), new Color(0.8f, 0.1f, 0.12f), 40, 0.1f, 0f, 19);
        yield return W("Stained Glass", FloorPattern.StainedGlass, new Color(0.15f, 0.3f, 0.8f), new Color(0.85f, 0.2f, 0.25f), new Color(0.95f, 0.75f, 0.2f), 40, 0.8f, 0.25f, 21);
    }

    // ---- helpers ----

    private static float Wu(FloorStyle s) { return Mathf.Max(1f, s.tileWorldSize); }

    /// <summary>Noise lattice period that gives features about 'feature' world units across.</summary>
    private static int Per(FloorStyle s, float feature) { return Mathf.Max(1, Mathf.RoundToInt(Wu(s) / Mathf.Max(0.05f, feature))); }

    private static void Rot2(float x, float y, float deg, out float rx, out float ry)
    {
        float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
        rx = x * c - y * sn;
        ry = x * sn + y * c;
    }

    private static Color Fin(Color c) { c.a = 1f; return c; }

    /// <summary>Inside a heart of radius 1 centred on the origin (y up).</summary>
    private static bool InHeart(float px, float py)
    {
        float a = px * px + py * py - 1f;
        return a * a * a - px * px * py * py * py < 0f;
    }

    /// <summary>Inside a five-point star of outer radius r.</summary>
    private static bool InStar(float dx, float dy, float r)
    {
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        float ang = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
        return d < r * (0.42f + 0.58f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * ang)), 1.4f));
    }

    // ------------------------------------------------------------------
    // 1. Lush Grass: blades, flower patches, a dirt trail winding through the middle
    // ------------------------------------------------------------------

    private static Color ShadeLushGrass(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, Per(s, 9f), 4, s.seed));
        c *= 0.8f + 0.4f * Fbm(u, v, Per(s, 4.5f), 3, s.seed + 4);

        // Blades: short strokes, every column with its own phase, light tips and dark roots.
        int bx = Mathf.FloorToInt(wx / 0.07f);
        float yy = wy / 0.34f + Hash(bx, 3, s.seed) * 4f;
        int by = Mathf.FloorToInt(yy);
        float fy = yy - by, bl = Hash(bx, by, s.seed + 9);
        c *= 0.76f + 0.42f * bl;
        c = ScMix(c, s.colorB * 1.35f, Sstep(0.55f, 1f, fy) * 0.35f * bl);
        c *= 0.8f + 0.2f * Sstep(0f, 0.3f, fy);
        if (Hash(x, y, s.seed + 2) > 0.985f) c = ScMix(c, s.colorB * 1.5f, 0.6f);   // sparkly dew

        // A dirt trail.
        float yc = W * 0.5f + 4f * Mathf.Sin(wx * 0.11f + s.seed) + 1.6f * Mathf.Sin(wx * 0.29f);
        float dpath = Mathf.Abs(wy - yc) - (0.9f + 0.55f * Fbm(u, v, Per(s, 2f), 3, s.seed + 6));
        float dirtMix = 1f - Sstep(-0.15f, 0.35f, dpath);
        if (dirtMix > 0f)
        {
            Color dirt = Color.Lerp(new Color(0.42f, 0.3f, 0.18f), new Color(0.6f, 0.47f, 0.3f), Fbm(u, v, Per(s, 1.2f), 3, s.seed + 7));
            dirt *= 0.85f + 0.3f * Hash(x, y, s.seed + 8);
            if (Hash(x / 2, y / 2, s.seed + 10) > 0.975f) dirt = ScMix(dirt, new Color(0.7f, 0.66f, 0.6f), 0.7f);   // pebbles
            c = ScMix(c, dirt, dirtMix);
        }

        // Flowers: one chance per 1.15 x 1.15 cell, never on the trail.
        float cell = 1.15f;
        int ix = Mathf.FloorToInt(wx / cell), iy = Mathf.FloorToInt(wy / cell);
        float fh = Hash(ix, iy, s.seed + 20);
        if (fh > 0.84f && dirtMix < 0.3f)
        {
            float fcx = (ix + 0.25f + 0.5f * Hash(ix, iy, s.seed + 21)) * cell, fcy = (iy + 0.25f + 0.5f * Hash(ix, iy, s.seed + 22)) * cell;
            float dx = wx - fcx, dy = wy - fcy, d = Mathf.Sqrt(dx * dx + dy * dy);
            float petal = 0.1f + 0.045f * Mathf.Cos(5f * Mathf.Atan2(dy, dx));
            if (d < petal)
            {
                float pick = Hash(ix, iy, s.seed + 23);
                Color pc = pick < 0.4f ? s.colorC : pick < 0.7f ? Color.white : pick < 0.9f ? new Color(1f, 0.85f, 0.3f) : new Color(0.92f, 0.45f, 0.72f);
                c = d < 0.04f ? new Color(1f, 0.8f, 0.15f) : pc * (0.9f + 0.15f * Mathf.Cos(d * 40f));
            }
        }
        return Fin(c);
    }

    // ------------------------------------------------------------------
    // 2. Nebula Field: warped gas clouds, dust lanes, three sizes of stars, a ringed-free gas giant and a moon
    // ------------------------------------------------------------------

    private static Color ShadeNebula(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        float wu = u + 0.07f * (Fbm(u, v, Per(s, 14f), 3, s.seed) - 0.5f), wv = v + 0.07f * (Fbm(u, v, Per(s, 14f), 3, s.seed + 9) - 0.5f);
        float n1 = Fbm(wu, wv, Per(s, 13f), 5, s.seed + 1), n2 = Fbm(wu + 0.3f, wv - 0.2f, Per(s, 9f), 5, s.seed + 2);
        float cloud1 = Sstep(0.36f, 0.78f, n1), cloud2 = Sstep(0.42f, 0.8f, n2);
        Color c = s.colorA;
        c += s.colorB * cloud1 * 0.85f + s.colorC * cloud2 * 0.7f;
        c += (s.colorB + s.colorC) * 0.5f * Sstep(0.62f, 0.9f, n1 * n2 * 2.2f) * 0.8f;
        float lane = Sstep(0.55f, 0.72f, Fbm(u, v, Per(s, 7f), 4, s.seed + 3));
        c *= 1f - 0.65f * lane * Mathf.Max(cloud1, cloud2);

        // Stars: single-texel dust, mid stars with a soft glow, big ones with spikes.
        if (Hash(x, y, s.seed + 40) > 0.9972f) c += Color.white * (0.4f + 0.6f * Hash(x, y, s.seed + 41));
        float cell = 1.1f;
        int cx = Mathf.FloorToInt(wx / cell), cy = Mathf.FloorToInt(wy / cell);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = cx + ox, iy = cy + oy;
                float h = Hash(ix, iy, s.seed + 30);
                if (h < 0.24f) continue;
                float sx = (ix + Hash(ix, iy, s.seed + 31)) * cell, sy = (iy + Hash(ix, iy, s.seed + 32)) * cell;
                float r = 0.05f + 0.07f * Hash(ix, iy, s.seed + 33);
                float d2 = (wx - sx) * (wx - sx) + (wy - sy) * (wy - sy);
                float glow = Mathf.Exp(-d2 / (r * r));
                if (glow < 0.01f) continue;
                float t = Hash(ix, iy, s.seed + 34);
                Color sc = t < 0.5f ? Color.white : t < 0.75f ? new Color(0.6f, 0.75f, 1f) : new Color(1f, 0.8f, 0.55f);
                c += sc * glow * (0.5f + 0.9f * h);
            }
        float bigCell = 5.5f;
        int bx = Mathf.FloorToInt(wx / bigCell), by = Mathf.FloorToInt(wy / bigCell);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = bx + ox, iy = by + oy;
                if (Hash(ix, iy, s.seed + 50) < 0.45f) continue;
                float sx = (ix + 0.2f + 0.6f * Hash(ix, iy, s.seed + 51)) * bigCell, sy = (iy + 0.2f + 0.6f * Hash(ix, iy, s.seed + 52)) * bigCell;
                float dx = wx - sx, dy = wy - sy;
                float core = Mathf.Exp(-(dx * dx + dy * dy) / 0.05f);
                float spike = Mathf.Exp(-Mathf.Abs(dy) / 0.025f) * Mathf.Exp(-Mathf.Abs(dx) / 0.9f) + Mathf.Exp(-Mathf.Abs(dx) / 0.025f) * Mathf.Exp(-Mathf.Abs(dy) / 0.9f);
                c += new Color(1f, 0.95f, 0.85f) * (core * 1.2f + spike * 0.55f);
            }

        // A banded gas giant and a small moon.
        c = NebulaBody(c, s, wx, wy, W * 0.62f, W * 0.6f, 3.1f, true);
        c = NebulaBody(c, s, wx, wy, W * 0.44f, W * 0.56f, 0.85f, false);
        return Fin(c);
    }

    private static Color NebulaBody(Color under, FloorStyle s, float wx, float wy, float cx, float cy, float R, bool banded)
    {
        float dx = (wx - cx) / R, dy = (wy - cy) / R, r2 = dx * dx + dy * dy;
        if (r2 > 1.6f) return under;
        float r = Mathf.Sqrt(r2);
        Color c = under;
        c += s.colorC * Mathf.Exp(-Mathf.Max(0f, r - 1f) * 9f) * 0.5f * (r > 1f ? 1f : 0f);   // atmosphere haze outside
        if (r >= 1f) return c;
        float nz = Mathf.Sqrt(1f - r2);
        float light = Mathf.Clamp01(-dx * 0.55f + dy * 0.45f + nz * 0.6f);
        Color body;
        if (banded)
        {
            float band = Mathf.Sin(dy * 9f + 2.2f * Fbm(wx / 40f, wy / 40f, 6, 3, s.seed + 70)) * 0.5f + 0.5f;
            body = Color.Lerp(Color.Lerp(s.colorB, new Color(1f, 0.75f, 0.45f), 0.45f), Color.Lerp(s.colorC, Color.white, 0.4f), band);
        }
        else body = Color.Lerp(new Color(0.55f, 0.55f, 0.58f), new Color(0.8f, 0.8f, 0.82f), Fbm(wx / 40f, wy / 40f, 20, 3, s.seed + 71));
        body *= 0.12f + 1.1f * light;
        float edge = Sstep(0.9f, 1f, r);
        return ScMix(body, c, edge * 0.5f);
    }

    // ------------------------------------------------------------------
    // 3. Still Pond: depth, caustics, ripples, lily pads with lotus flowers, koi
    // ------------------------------------------------------------------

    private static Color ShadePond(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        float depth = Fbm(u, v, Per(s, 12f), 4, s.seed);
        Color c = Color.Lerp(s.colorA, s.colorB, Mathf.Pow(depth, 1.3f));
        float shallow = Mathf.Clamp01(depth * 1.4f);

        // Caustic light: two crossing nets of bright lines.
        int cc = Per(s, 1.6f);
        Voronoi(u, v, cc, s.seed + 1, out float f1, out float f2, out int id);
        float net = 1f - Sstep(0f, 0.2f, (f2 - f1) * cc);
        Voronoi(u + 0.37f, v + 0.11f, Per(s, 1.0f), s.seed + 2, out float g1, out float g2, out int id2);
        float net2 = 1f - Sstep(0f, 0.2f, (g2 - g1) * Per(s, 1.0f));
        c += new Color(0.9f, 1f, 0.95f) * (net * 0.14f + net2 * 0.08f) * (0.35f + shallow);

        // Murk: greener water where the algae bloom, and soft reflections of the sky on the open water.
        c = ScMix(c, new Color(0.08f, 0.3f, 0.12f), Sstep(0.55f, 0.8f, Fbm(u, v, Per(s, 6f), 3, s.seed + 90)) * 0.28f);
        c += new Color(0.5f, 0.65f, 0.8f) * Sstep(0.55f, 0.85f, Fbm(u, v, Per(s, 22f), 2, s.seed + 91)) * 0.07f * (1f - shallow * 0.5f);

        // Pebbles on the bottom, seen through the shallows.
        if (shallow > 0.7f)
        {
            int pc = Per(s, 0.5f);
            Voronoi(u, v, pc, s.seed + 92, out float p1, out float p2, out int pid);
            float pr = (0.3f + 0.14f * HashId(pid, s.seed + 93)) / pc;
            if (p1 < pr)
            {
                Color pebble = Color.Lerp(new Color(0.45f, 0.42f, 0.36f), new Color(0.62f, 0.6f, 0.55f), HashId(pid, s.seed + 94));
                c = ScMix(c, pebble * (0.7f + 0.5f * (1f - p1 / pr)) * new Color(0.8f, 1f, 1f, 1f), 0.5f * Sstep(0.7f, 0.9f, shallow));
            }
        }

        // Ripples from a few drops.
        for (int i = 0; i < 7; i++)
        {
            float rx = (0.1f + 0.8f * Hash(i, 1, s.seed + 60)) * W, ry = (0.1f + 0.8f * Hash(i, 2, s.seed + 60)) * W;
            float d = Mathf.Sqrt((wx - rx) * (wx - rx) + (wy - ry) * (wy - ry));
            if (d > 3.2f) continue;
            float ring = Mathf.Sin(d * 11f + i) * Mathf.Exp(-d * 0.95f);
            c += new Color(1f, 1f, 1f) * ring * 0.06f;
        }

        // Koi (dark long shapes with orange backs) swim in the deeper water.
        for (int i = 0; i < 4; i++)
        {
            float fx = (0.2f + 0.6f * Hash(i, 5, s.seed + 80)) * W, fy = (0.2f + 0.6f * Hash(i, 6, s.seed + 80)) * W;
            float ang = Hash(i, 7, s.seed + 80) * 360f;
            Rot2(wx - fx, wy - fy, -ang, out float lx, out float ly);
            float body = ScEll(lx, ly, 0f, 0f, 0.95f, 0.3f, 0.04f);
            float tail = ScEll(lx, ly, -1.15f, 0f, 0.35f, 0.18f * (1f + 0.5f * Mathf.Sin(lx * 8f)), 0.04f);
            float fish = Mathf.Max(body, tail * 0.8f);
            // The koi's shadow on the bottom, its side fins and the ring of ripples at its nose.
            Rot2(-0.3f, 0.35f, ang, out float shx, out float shy);
            float shadowKoi = Mathf.Max(ScEll(lx, ly, shx, shy, 0.95f, 0.3f, 0.04f), ScEll(lx, ly, shx - 1.15f, shy, 0.35f, 0.16f, 0.04f) * 0.7f);
            c *= 1f - 0.25f * shadowKoi * (1f - fish);
            float fins = Mathf.Max(ScEll(lx, ly, 0.3f, 0.34f, 0.22f, 0.07f, 0.04f), ScEll(lx, ly, 0.3f, -0.34f, 0.22f, 0.07f, 0.04f));
            fish = Mathf.Max(fish, fins * 0.75f);
            float wake = Mathf.Exp(-Mathf.Abs(Mathf.Sqrt((lx - 1.1f) * (lx - 1.1f) + ly * ly) - 0.5f) * 9f) * 0.07f;
            c += Color.white * wake;
            if (fish > 0f)
            {
                Color koi = i % 2 == 0 ? new Color(1f, 0.5f, 0.15f) : new Color(0.95f, 0.9f, 0.85f);
                if (Fbm(lx * 0.3f + 0.5f, ly * 0.3f + 0.5f, 3, 2, s.seed + i) > 0.55f) koi = new Color(0.9f, 0.12f, 0.1f);
                c = ScMix(c, c * 0.45f + koi * 0.35f, fish * 0.8f);
            }
        }

        // Reeds: clumps of blades with a brown cattail in the middle, at the edge of the shallows.
        float rcell = 7f;
        int rx0 = Mathf.FloorToInt(wx / rcell), ry0 = Mathf.FloorToInt(wy / rcell);
        if (Hash(rx0, ry0, s.seed + 100) > 0.55f)
        {
            float rcx = (rx0 + 0.25f + 0.5f * Hash(rx0, ry0, s.seed + 101)) * rcell, rcy = (ry0 + 0.25f + 0.5f * Hash(rx0, ry0, s.seed + 102)) * rcell;
            float rdx = wx - rcx, rdy = wy - rcy, rd = Mathf.Sqrt(rdx * rdx + rdy * rdy);
            if (rd < 1.1f)
            {
                c *= 1f - 0.3f * Sstep(1.1f, 0.5f, rd);                                   // a soft shadow on the water
                float ra = Mathf.Atan2(rdy, rdx);
                float blade = Mathf.Abs(Mathf.Sin(ra * 7f + Hash(rx0, ry0, s.seed + 103) * 6f + rd * 3f));
                if (blade > 0.82f && rd > 0.12f && rd < 0.95f) c = Color.Lerp(new Color(0.12f, 0.38f, 0.14f), new Color(0.5f, 0.8f, 0.3f), rd);
                if (rd < 0.16f) c = rd < 0.07f ? new Color(0.35f, 0.2f, 0.1f) : new Color(0.45f, 0.3f, 0.14f);
            }
        }

        // Petals floating on the water.
        float pcell = 1.3f;
        int ppx = Mathf.FloorToInt(wx / pcell), ppy = Mathf.FloorToInt(wy / pcell);
        if (Hash(ppx, ppy, s.seed + 110) > 0.94f)
        {
            float qx = (ppx + 0.5f + 0.3f * (Hash(ppx, ppy, s.seed + 111) - 0.5f)) * pcell, qy = (ppy + 0.5f + 0.3f * (Hash(ppx, ppy, s.seed + 112) - 0.5f)) * pcell;
            Rot2(wx - qx, wy - qy, Hash(ppx, ppy, s.seed + 113) * 180f, out float ex, out float ey);
            float petal = ScEll(ex, ey, 0f, 0f, 0.13f, 0.06f, 0.04f);
            c = ScMix(c, Color.Lerp(new Color(1f, 0.7f, 0.8f), Color.white, Hash(ppx, ppy, s.seed + 114)), petal * 0.9f);
        }

        // Lily pads (one chance per 3.2 unit cell), some with a pink lotus flower.
        float cell = 3.2f;
        int ix = Mathf.FloorToInt(wx / cell), iy = Mathf.FloorToInt(wy / cell);
        if (Hash(ix, iy, s.seed + 20) > 0.45f)
        {
            float px = (ix + 0.3f + 0.4f * Hash(ix, iy, s.seed + 21)) * cell, py = (iy + 0.3f + 0.4f * Hash(ix, iy, s.seed + 22)) * cell;
            float rad = 0.6f + 0.3f * Hash(ix, iy, s.seed + 23);
            float dx = wx - px, dy = wy - py, d = Mathf.Sqrt(dx * dx + dy * dy);
            float shadow = Mathf.Exp(-Mathf.Max(0f, Mathf.Sqrt((dx - 0.1f) * (dx - 0.1f) + (dy + 0.1f) * (dy + 0.1f)) - rad) * 6f);
            c *= 1f - 0.4f * shadow * (d > rad ? 1f : 0f);
            float notch = Hash(ix, iy, s.seed + 24) * 6.283f;
            float da = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, notch * Mathf.Rad2Deg));
            if (d < rad && da > 12f)
            {
                Color pad = Color.Lerp(s.colorC * 0.7f, s.colorC * 1.25f, 0.5f + 0.5f * Mathf.Cos(d * 5f));
                float vein = Mathf.Abs(Frac((Mathf.Atan2(dy, dx) + notch) / 6.283f * 14f) - 0.5f);
                if (vein < 0.06f && d > 0.08f) pad *= 0.78f;
                if (d > rad - 0.05f) pad *= 0.75f;
                c = pad;
            }
            if (Hash(ix, iy, s.seed + 25) > 0.7f && d < 0.34f)
            {
                float lotus = 0.34f * (0.55f + 0.45f * Mathf.Abs(Mathf.Cos(4f * Mathf.Atan2(dy, dx))));
                if (d < lotus) c = d < 0.07f ? new Color(1f, 0.85f, 0.2f) : Color.Lerp(new Color(1f, 0.6f, 0.78f), Color.white, d / 0.34f);
            }
        }
        if (Hash(x, y, s.seed + 95) > 0.9985f) c += Color.white * 0.45f;   // a glint of sun on the water
        return Fin(c);
    }

    // ------------------------------------------------------------------
    // 4. Graffiti Wall: a brick wall laid flat with a big bubble-letter tag, a star, a heart, a swoosh, drips, overspray and paint-over patches
    // ------------------------------------------------------------------

    private static readonly float[][] TagLetters =
    {
        new[] { 0f, 0f, 0f, 1f,  0f, 1f, 0.62f, 1f,  0.62f, 1f, 0.9f, 0.86f,  0.9f, 0.86f, 0.9f, 0.64f,  0.9f, 0.64f, 0.62f, 0.5f,  0.62f, 0.5f, 0f, 0.5f },   // P
        new[] { 0.5f, 0f, 0.5f, 1f,  0.15f, 1f, 0.85f, 1f,  0.15f, 0f, 0.85f, 0f },                                                                          // I
        new[] { 0f, 0f, 1f, 1f,  0f, 1f, 1f, 0f },                                                                                                            // X
        new[] { 0f, 0f, 0f, 1f,  0f, 1f, 0.95f, 1f,  0f, 0.5f, 0.8f, 0.5f,  0f, 0f, 0.95f, 0f },                                                             // E
        new[] { 0f, 0f, 0f, 1f,  0f, 0f, 0.95f, 0f },                                                                                                         // L
    };

    private static readonly float[][] TagDrips = { new[] { 0f }, new[] { 0.15f, 0.5f, 0.85f }, new[] { 0f, 1f }, new[] { 0f, 0.5f, 0.95f }, new[] { 0f, 0.95f } };

    private static readonly Color[] TagColors =
    {
        new Color(1f, 0.2f, 0.55f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.15f), new Color(0.15f, 0.9f, 0.95f), new Color(0.6f, 0.35f, 1f),
    };

    private const float TagH = 3.6f, TagW = 2.3f, TagGap = 0.5f, TagRot = -5f, TagR = 0.3f;

    /// <summary>Distance from wall point (px, py) to the tag's strokes (and which letter is nearest); 99 far away from the lettering.</summary>
    private static float TagDistance(float px, float py, float cx, float cy, out int letter)
    {
        letter = 0;
        float total = 5f * TagW + 4f * TagGap;
        Rot2(px - cx, py - cy, -TagRot, out float tx, out float ty);
        if (Mathf.Abs(tx) > total * 0.5f + 1.2f || Mathf.Abs(ty) > TagH * 0.5f + 1.6f) return 99f;
        float best = 99f;
        for (int i = 0; i < 5; i++)
        {
            float ox = -total * 0.5f + i * (TagW + TagGap), oy = -TagH * 0.5f + 0.14f * Mathf.Sin(i * 1.7f);
            float[] seg = TagLetters[i];
            for (int k = 0; k + 3 < seg.Length; k += 4)
            {
                float d = ScSeg(tx, ty, ox + seg[k] * TagW, oy + seg[k + 1] * TagH, ox + seg[k + 2] * TagW, oy + seg[k + 3] * TagH, out float along);
                if (d < best) { best = d; letter = i; }
            }
        }
        return best;
    }

    private static Color ShadeGraffiti(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W, aa = W / n * 0.8f;
        float cx = W * 0.5f, cy = W * 0.5f;

        // ---- the brick wall ----
        const float bw = 1.6f, bh = 0.7f, mw = 0.07f;
        int row = Mathf.FloorToInt(wy / bh);
        float off = (row & 1) * 0.5f * bw;
        float bxu = (wx + off) / bw;
        int col = Mathf.FloorToInt(bxu);
        float fx = (bxu - col) * bw, fyb = (wy / bh - row) * bh;
        float hb = HashId(col * 31 + row * 7, s.seed);
        Color c;
        float edgeD = Mathf.Min(Mathf.Min(fx, bw - fx), Mathf.Min(fyb, bh - fyb));
        if (edgeD < mw)
        {
            c = s.colorC * (0.7f + 0.3f * Hash(x, y, s.seed + 1)) * (0.8f + 0.3f * Fbm(u, v, Per(s, 0.6f), 3, s.seed + 2));   // mortar
        }
        else
        {
            c = Color.Lerp(s.colorA, s.colorB, hb) * (0.85f + 0.25f * Fbm(u, v, Per(s, 0.5f), 3, s.seed + 3));
            c *= 0.88f + 0.2f * Hash(x, y, s.seed + 4);
            if (fyb > bh - mw * 2.2f || fx < mw * 2.2f) c *= 1.12f;     // lit top / left edge
            else if (fyb < mw * 2.2f || fx > bw - mw * 2.2f) c *= 0.82f; // shaded bottom / right edge
            if (Hash(x / 2, y / 2, s.seed + 5) > 0.992f) c *= 0.6f;      // chips
        }
        c *= 0.85f + 0.25f * Fbm(u, v, Per(s, 6f), 4, s.seed + 6);        // grime
        float rain = Sstep(0.5f, 0.8f, Fbm(u * 6f, v * 0.4f, 24, 2, 5, s.seed + 7));
        c *= 1f - 0.25f * rain;                                           // vertical rain streaks

        // ---- older paint-over patches (buffed grey rectangles with roller streaks) ----
        for (int i = 0; i < 3; i++)
        {
            float px = cx + (i - 1) * 13f + (Hash(i, 1, s.seed + 90) - 0.5f) * 3f, py = cy + (Hash(i, 2, s.seed + 90) - 0.5f) * 7f;
            float hw = 3.4f + 1.2f * Hash(i, 3, s.seed + 90), hh = 2.4f + 1.4f * Hash(i, 4, s.seed + 90);
            float a = ScBox(wx, wy, px - hw, py - hh, px + hw, py + hh, 0.06f);
            if (a > 0f)
            {
                Color buff = new Color(0.52f, 0.46f, 0.42f) * (0.88f + 0.18f * Mathf.Sin(wx * 9f + Fbm(u, v, Per(s, 1f), 2, s.seed) * 4f));
                if (i == 1) buff = new Color(0.36f, 0.4f, 0.46f) * (0.9f + 0.15f * Mathf.Sin(wx * 7f));
                c = ScMix(c, ScMix(c, buff, 0.82f), a);
            }
        }

        // ---- the tag ----
        float d = TagDistance(wx, wy, cx, cy, out int li);
        Color paint = TagColors[li % TagColors.Length];
        if (d < 3f)
        {
            c = ScMix(c, paint, 0.2f * Mathf.Exp(-Mathf.Max(0f, d - TagR) / 0.45f));        // overspray haze
            float ds = TagDistance(wx - 0.17f, wy + 0.17f, cx, cy, out int dummyLetter);
            if (ds < TagR + 0.12f) c *= 0.62f;                                               // drop shadow on the bricks
            float outline = 1f - Sstep(TagR + 0.12f - aa, TagR + 0.12f + aa, d);
            c = ScMix(c, new Color(0.05f, 0.04f, 0.07f), outline);
            float fill = 1f - Sstep(TagR - aa, TagR + aa, d);
            if (fill > 0f)
            {
                Color fc = paint * (1.05f - 0.25f * Sstep(-1.8f, 1.8f, -(wy - cy)));
                float hl = TagDistance(wx + 0.1f, wy - 0.1f, cx, cy, out int dummy2);
                if (hl < TagR * 0.32f) fc = ScMix(fc, Color.white, 0.55f);
                c = ScMix(c, fc, fill);
            }
        }
        // drips: thin runs under the strokes that touch the baseline
        {
            float total = 5f * TagW + 4f * TagGap;
            Rot2(wx - cx, wy - cy, -TagRot, out float tx, out float ty);
            for (int i = 0; i < 5; i++)
            {
                float ox = -total * 0.5f + i * (TagW + TagGap), oy = -TagH * 0.5f + 0.14f * Mathf.Sin(i * 1.7f);
                float[] dr = TagDrips[i];
                for (int k = 0; k < dr.Length; k++)
                {
                    float dx = ox + dr[k] * TagW;
                    float len = 0.5f + 1.5f * Hash(i * 7 + k, 11, s.seed + 95);
                    if (Mathf.Abs(tx - dx) < 0.07f && ty < oy + 0.05f && ty > oy - len)
                    {
                        float tip = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Clamp01((oy - ty - (len - 0.1f)) / 0.1f), 2f)));
                        if (Mathf.Abs(tx - dx) < 0.07f * tip) c = ScMix(c, TagColors[i], 0.95f);
                    }
                }
            }
        }

        // ---- a star, a heart and a swoosh ----
        {
            float sx = cx + 10.5f, sy = cy + 3f;
            float sdx = wx - sx, sdy = wy - sy;
            if (InStar(sdx, sdy, 1.45f)) c = Mathf.Abs(sdx) < 0.04f ? c : new Color(1f, 0.9f, 0.2f) * (0.9f + 0.15f * Sstep(-1f, 1f, sdy));
            else if (InStar(sdx, sdy, 1.62f)) c = new Color(0.05f, 0.04f, 0.07f);
            float hx = (wx - (cx - 10.5f)) / 1.25f, hy = (wy - (cy - 1.5f)) / 1.25f;
            if (InHeart(hx * 0.9f, hy * 0.9f + 0.05f)) c = new Color(0.9f, 0.1f, 0.2f) * (0.85f + 0.2f * Sstep(-1f, 1f, hy));
            else if (InHeart(hx * 0.8f, hy * 0.8f + 0.05f)) c = new Color(0.05f, 0.04f, 0.07f);
            if (wx > cx - 8f && wx < cx + 8.4f)
            {
                float swy = cy - 3.6f + 0.35f * Mathf.Sin((wx - cx) * 0.9f);
                float sd = Mathf.Abs(wy - swy);
                float thick = 0.12f + 0.1f * Sstep(cx - 8f, cx + 8f, wx);
                if (sd < thick + 0.07f) c = sd < thick ? new Color(0.2f, 0.9f, 1f) : new Color(0.05f, 0.04f, 0.07f);
            }
        }
        // spray splatter
        if (Hash(Mathf.FloorToInt(wx / 0.6f), Mathf.FloorToInt(wy / 0.6f), s.seed + 98) > 0.992f)
        {
            float sd = Mathf.Sqrt(Mathf.Pow(Frac(wx / 0.6f) - 0.5f, 2f) + Mathf.Pow(Frac(wy / 0.6f) - 0.5f, 2f)) * 0.6f;
            if (sd < 0.1f) c = ScMix(c, TagColors[Mathf.FloorToInt(wx) % 5 < 0 ? 0 : Mathf.FloorToInt(Mathf.Abs(wx)) % 5], 0.9f);
        }
        return Fin(c);
    }

    // ------------------------------------------------------------------
    // 5. Leaf Litter: soil under two layers of fallen autumn leaves, twigs
    // ------------------------------------------------------------------

    private static Color ShadeLeaves(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        Color c = s.colorA * (0.75f + 0.5f * Fbm(u, v, Per(s, 5f), 4, s.seed)) * (0.85f + 0.3f * Hash(x, y, s.seed + 1));
        if (Hash(x, y, s.seed + 2) > 0.995f) c = ScMix(c, new Color(0.3f, 0.45f, 0.15f), 0.6f);   // a green shoot

        // Twigs.
        float tcell = 5.5f;
        int tx = Mathf.FloorToInt(wx / tcell), ty = Mathf.FloorToInt(wy / tcell);
        if (Hash(tx, ty, s.seed + 5) > 0.5f)
        {
            float ang = Hash(tx, ty, s.seed + 6) * 3.14f;
            float cxx = (tx + 0.3f + 0.4f * Hash(tx, ty, s.seed + 7)) * tcell, cyy = (ty + 0.3f + 0.4f * Hash(tx, ty, s.seed + 8)) * tcell;
            float ex = Mathf.Cos(ang) * 1.3f, ey = Mathf.Sin(ang) * 1.3f;
            float d = ScSeg(wx, wy, cxx - ex, cyy - ey, cxx + ex, cyy + ey, out float along);
            if (d < 0.05f) c = new Color(0.3f, 0.2f, 0.12f) * (0.8f + 0.4f * Hash(x, y, s.seed + 9));
        }

        // Two layers of leaves: the older, browner ones below.
        for (int layer = 0; layer < 2; layer++)
        {
            float cell = layer == 0 ? 1.3f : 1.05f;
            float shift = layer == 0 ? 0f : 0.47f;
            int ix = Mathf.FloorToInt((wx + shift) / cell), iy = Mathf.FloorToInt((wy + shift * 1.7f) / cell);
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int jx = ix + ox, jy = iy + oy;
                    int sd = s.seed + 100 * layer;
                    if (Hash(jx, jy, sd + 20) < 0.12f) continue;
                    float lcx = (jx + 0.25f + 0.5f * Hash(jx, jy, sd + 21)) * cell - shift, lcy = (jy + 0.25f + 0.5f * Hash(jx, jy, sd + 22)) * cell - shift * 1.7f;
                    float ang = Hash(jx, jy, sd + 23) * 360f;
                    float len = (0.45f + 0.3f * Hash(jx, jy, sd + 24)) * (layer == 0 ? 1.1f : 0.95f), wid = len * 0.42f;
                    Rot2(wx - lcx, wy - lcy, -ang, out float lx, out float ly);
                    float tt = lx / len;
                    if (Mathf.Abs(tt) > 1f) continue;
                    float hw = wid * Mathf.Pow(Mathf.Clamp01(1f - tt * tt), 0.75f) * (1f - 0.2f * tt);
                    if (Mathf.Abs(ly) >= hw) continue;
                    float pick = Hash(jx, jy, sd + 25);
                    Color lc = pick < 0.38f ? s.colorB : pick < 0.68f ? Color.Lerp(s.colorB, s.colorC, 0.5f) : pick < 0.9f ? s.colorC : new Color(0.55f, 0.12f, 0.08f);
                    lc *= 0.8f + 0.35f * Hash(jx, jy, sd + 26);
                    if (layer == 0) lc = Color.Lerp(lc, new Color(0.35f, 0.2f, 0.1f), 0.45f);
                    float vein = Mathf.Abs(Frac((lx - Mathf.Abs(ly) * 1.1f) * 6f / len * 0.5f) - 0.5f);
                    if (Mathf.Abs(ly) < 0.012f) lc *= 0.55f;
                    else if (vein < 0.07f && Mathf.Abs(ly) < hw * 0.9f) lc *= 0.82f;
                    lc *= 0.8f + 0.25f * Sstep(0f, hw, hw - Mathf.Abs(ly));           // darker near the rim
                    lc *= 0.9f + 0.18f * Hash(x, y, s.seed + 27);
                    c = lc;
                }
        }
        return Fin(c);
    }

    // ------------------------------------------------------------------
    // 6. Zen Garden: raked sand that rings round the rocks, mossy boulders, pebbles
    // ------------------------------------------------------------------

    private const int ZenRocks = 9;

    private static float ZenNearest(FloorStyle s, float wx, float wy, out int rock, out float radius, out float cx, out float cy)
    {
        float W = Wu(s), best = 99f;
        rock = 0; radius = 1f; cx = 0f; cy = 0f;
        for (int i = 0; i < ZenRocks; i++)
        {
            float rx = W * (0.18f + 0.64f * Hash(i, 3, s.seed)), ry = W * (0.18f + 0.64f * Hash(i, 4, s.seed));
            float r = 0.7f + 1.1f * Hash(i, 5, s.seed);
            float d = Mathf.Sqrt((wx - rx) * (wx - rx) + (wy - ry) * (wy - ry)) - r;
            if (d < best) { best = d; rock = i; radius = r; cx = rx; cy = ry; }
        }
        return best;
    }

    private static Color ShadeZen(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        float dR = ZenNearest(s, wx, wy, out int rk, out float rr, out float rcx, out float rcy);
        dR += 0.18f * (Fbm(u, v, Per(s, 1.4f), 3, s.seed + 1) - 0.5f);   // the rings wobble a little

        // Rake lines: rings round the rocks near them, straight lines far away.
        float blend = Sstep(2.4f, 3.6f, dR);
        float phase = Mathf.Lerp(dR * 3.4f, wy * 3.4f + 0.3f * Mathf.Sin(wx * 0.4f), blend);
        float groove = 0.5f + 0.5f * Mathf.Sin(phase * 6.283f);
        Color sand = Color.Lerp(s.colorA * 0.93f, s.colorA * 1.04f, Fbm(u, v, Per(s, 7f), 3, s.seed + 2));
        Color c = sand * (0.86f + 0.2f * groove) * (0.94f + 0.1f * Hash(x, y, s.seed + 3));
        if (groove > 0.92f) c = ScMix(c, Color.white * 0.97f, 0.1f);

        // Pebbles.
        float pc = 2.2f;
        int ix = Mathf.FloorToInt(wx / pc), iy = Mathf.FloorToInt(wy / pc);
        if (Hash(ix, iy, s.seed + 20) > 0.82f)
        {
            float px = (ix + 0.2f + 0.6f * Hash(ix, iy, s.seed + 21)) * pc, py = (iy + 0.2f + 0.6f * Hash(ix, iy, s.seed + 22)) * pc;
            float r = 0.08f + 0.08f * Hash(ix, iy, s.seed + 23);
            float d = Mathf.Sqrt((wx - px) * (wx - px) + (wy - py) * (wy - py));
            float sh = Mathf.Sqrt((wx - px - 0.05f) * (wx - px - 0.05f) + (wy - py + 0.05f) * (wy - py + 0.05f));
            if (sh < r * 1.2f && d > r) c *= 0.75f;
            if (d < r) c = s.colorB * (0.8f + 0.5f * Sstep(-r, r, -(wx - px) * 0.6f + (wy - py) * 0.8f));
        }

        // The rocks: lit from the upper left, moss on top, a dark contact shadow.
        if (dR < 0.5f)
        {
            // (dR was wobbled above; use the true edge for the rock itself)
            float edge = dR;
            float cs = 1f - Sstep(0f, 0.5f, edge);
            c *= 1f - 0.4f * cs * (wx - rcx + wy - rcy < 0f ? 0.6f : 1f);
        }
        if (dR < 0.04f)
        {
            float nx = (wx - rcx) / rr, ny = (wy - rcy) / rr;
            Color rock = Color.Lerp(s.colorB * 0.8f, s.colorB * 1.3f, Fbm(u, v, Per(s, 0.9f), 4, s.seed + 30 + rk));
            float lit = Mathf.Clamp01(0.55f - nx * 0.4f + ny * 0.5f);
            rock *= 0.55f + 0.75f * lit;
            rock *= 0.85f + 0.3f * Hash(x, y, s.seed + 31);
            float moss = Sstep(0.5f, 0.62f, Fbm(u, v, Per(s, 0.7f), 4, s.seed + 40 + rk) + 0.25f * ny);
            rock = ScMix(rock, s.colorC * (0.7f + 0.6f * Hash(x / 2, y / 2, s.seed + 41)), moss * 0.85f);
            c = rock;
        }
        return Fin(c);
    }

    // ------------------------------------------------------------------
    // 8. Coral Reef: a shallow sandy seabed seen from above - reef rock patches where the corals cluster (brain, branching, table,
    //    soft coral and lace sea fans, each an irregular domed colony with its shadow on the sand), seagrass meadows, starfish,
    //    urchins and shells. The moving caustic light is NOT in the picture: the Caustics overlay draws it live (PixelFloorOverlay).
    // ------------------------------------------------------------------

    private const float ReefShadowX = 0.28f, ReefShadowY = -0.28f;   // light from the top left: shadows fall down-right (world units)

    private static Color ShadeReef(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;

        // Sand: pale, with gentle ripple bands and a fine grain.
        float drift = Fbm(u, v, Per(s, 1.5f), 3, s.seed + 2);
        Color sand = Color.Lerp(s.colorA, new Color(0.97f, 0.95f, 0.86f), 0.45f) * (0.88f + 0.2f * Fbm(u, v, Per(s, 5f), 4, s.seed)) * (0.93f + 0.14f * Hash(x, y, s.seed + 1));
        sand *= 0.94f + 0.08f * Mathf.Sin(wy * 4.2f + wx * 1.3f + drift * 7f);
        Color c = sand;

        // Reef rock: grey-brown patches of old coral rock with pits and a film of algae.
        float rock = ReefRock(s, u, v);
        if (rock > 0f)
        {
            float pits = Fbm(u, v, Per(s, 0.35f), 2, s.seed + 5);
            Color stone = Color.Lerp(new Color(0.66f, 0.6f, 0.5f), new Color(0.58f, 0.6f, 0.46f), Fbm(u, v, Per(s, 2f), 2, s.seed + 6));
            stone *= 0.82f + 0.3f * pits;
            stone *= 1f - 0.25f * (1f - Sstep(0.2f, 0.3f, pits));   // pits
            c = Color.Lerp(c, stone, rock);
        }

        // Seagrass meadows in the open sand: blades lying in the current.
        float meadow = Sstep(0.6f, 0.72f, Fbm(u + 0.37f, v + 0.11f, Per(s, 7f), 3, s.seed + 4)) * (1f - rock);
        if (meadow > 0.01f)
        {
            float wave = 0.6f + 0.5f * (Fbm(u, v, Per(s, 4f), 2, s.seed + 7) - 0.5f);
            float blade = BladeLayer(wx, wy, 0.6f, 3, 0.12f, 0.35f, 0.85f, 0.045f, 0.5f, wave - 0.6f, s.seed + 8, out float bt, out float btone);
            c *= 1f - 0.25f * meadow;   // the meadow's shade
            Color green = Color.Lerp(new Color(0.12f, 0.36f, 0.2f), new Color(0.42f, 0.66f, 0.3f), bt) * (0.85f + 0.3f * btone);
            c = Color.Lerp(c, green, blade * meadow);
        }

        // Shadows of the corals on the sand / rock.
        float shade = ReefCoral(s, wx - ReefShadowX, wy - ReefShadowY, u, v, out _, out _, out _, out _, out _);
        c *= 1f - 0.4f * shade;

        // Starfish: five tapering arms, a raised spine line, little white knobs.
        float scell = 6f;
        int sx0 = Mathf.FloorToInt(wx / scell), sy0 = Mathf.FloorToInt(wy / scell);
        if (Hash(sx0, sy0, s.seed + 30) > 0.55f)
        {
            float px = (sx0 + 0.25f + 0.5f * Hash(sx0, sy0, s.seed + 31)) * scell, py = (sy0 + 0.25f + 0.5f * Hash(sx0, sy0, s.seed + 32)) * scell;
            float dx = wx - px, dy = wy - py, d = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) + Hash(sx0, sy0, s.seed + 33) * 6f;
            float arm = Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * ang)), 1.6f);
            float r = 0.45f * (0.34f + 0.66f * arm);
            c *= 1f - 0.3f * (1f - Sstep(r, r + 0.12f, Mathf.Sqrt((dx - ReefShadowX * 0.4f) * (dx - ReefShadowX * 0.4f) + (dy - ReefShadowY * 0.4f) * (dy - ReefShadowY * 0.4f))));
            if (d < r)
            {
                Color star = Color.Lerp(new Color(1f, 0.5f, 0.15f), new Color(0.92f, 0.25f, 0.2f), Hash(sx0, sy0, s.seed + 34));
                star *= 0.7f + 0.45f * Sstep(0.55f, 1f, arm) * (1f - d / r) + 0.15f * (1f - d / r);
                if (Hash(Mathf.FloorToInt(wx / 0.06f), Mathf.FloorToInt(wy / 0.06f), s.seed + 35) > 0.88f) star = ScMix(star, new Color(1f, 0.95f, 0.85f), 0.6f);
                c = star;
            }
        }

        // Shells and urchins in the sand.
        float shcell = 2.6f;
        int shx = Mathf.FloorToInt(wx / shcell), shy = Mathf.FloorToInt(wy / shcell);
        if (Hash(shx, shy, s.seed + 70) > 0.84f)
        {
            float px = (shx + 0.2f + 0.6f * Hash(shx, shy, s.seed + 71)) * shcell, py = (shy + 0.2f + 0.6f * Hash(shx, shy, s.seed + 72)) * shcell;
            float dx = wx - px, dy = wy - py, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 0.12f) c = Color.Lerp(new Color(0.95f, 0.88f, 0.78f), new Color(0.85f, 0.55f, 0.45f), 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(dy, dx) * 2f + d * 70f)) * (0.8f + 0.3f * Mathf.Clamp01(0.5f - dy * 4f));
        }
        float ucell = 5.3f;
        int ux0 = Mathf.FloorToInt(wx / ucell), uy0 = Mathf.FloorToInt(wy / ucell);
        if (Hash(ux0, uy0, s.seed + 50) > 0.7f)
        {
            float px = (ux0 + 0.2f + 0.6f * Hash(ux0, uy0, s.seed + 51)) * ucell, py = (uy0 + 0.2f + 0.6f * Hash(ux0, uy0, s.seed + 52)) * ucell;
            float dx = wx - px, dy = wy - py, d = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx);
            float spike = 0.13f + 0.11f * Mathf.Pow(Mathf.Abs(Mathf.Cos(9f * ang)), 9f);
            c *= 1f - 0.3f * (1f - Sstep(0.15f, 0.35f, d));
            if (d < spike) c = Color.Lerp(new Color(0.08f, 0.03f, 0.14f), new Color(0.4f, 0.2f, 0.55f), Sstep(0.08f, spike, d));
        }

        // The corals.
        float coral = ReefCoral(s, wx, wy, u, v, out int type, out float k, out float nx, out float ny, out float pick);
        if (coral > 0f)
        {
            Color col;
            switch (type)
            {
                case 0: col = Color.Lerp(new Color(0.78f, 0.64f, 0.38f), new Color(0.62f, 0.7f, 0.36f), pick); break;   // brain coral: tan / olive
                case 1: col = pick < 0.4f ? s.colorC : pick < 0.75f ? new Color(0.35f, 0.6f, 0.95f) : s.colorB; break;      // branching: purple / blue / pink
                case 2: col = Color.Lerp(new Color(0.55f, 0.66f, 0.5f), new Color(0.75f, 0.68f, 0.5f), pick); break;     // table coral: sage / sand
                case 3: col = pick < 0.5f ? new Color(1f, 0.55f, 0.3f) : s.colorB; break;                                // soft coral: orange / pink
                default: col = pick < 0.6f ? s.colorC * 1.1f : new Color(0.85f, 0.25f, 0.3f); break;                      // sea fan: violet / red
            }
            float lit = Mathf.Clamp01(0.55f + (-nx + ny) * 0.45f);
            float dome = Mathf.Sqrt(Mathf.Max(0f, 1f - k * k));
            if (type == 0)
            {
                // Brain coral: meandering grooves.
                float groove = Mathf.Abs(Mathf.Sin(Fbm(u, v, Per(s, 0.7f), 3, s.seed + 10 + Mathf.FloorToInt(pick * 7f)) * 24f));
                col *= 0.62f + 0.48f * Sstep(0.15f, 0.6f, groove);
            }
            else if (type == 1)
            {
                // Branching coral from above: crowded branch tips, bright at the tip, dark down between them.
                float tip = ReefTips(wx, wy, 0.13f, s.seed + 11);
                col = Color.Lerp(col * 0.35f, Color.Lerp(col * 1.15f, Color.white, 0.25f * tip), Sstep(0.15f, 0.7f, tip));
            }
            else if (type == 2)
            {
                // Table coral: a flat plate with fine radial lines, growth rings and a pale growing rim.
                float ang = Mathf.Atan2(ny, nx);
                col *= 0.85f + 0.15f * Mathf.Abs(Mathf.Sin(ang * 70f)) - 0.08f * Sstep(0.4f, 0.5f, Frac(k * 4f));
                col = Color.Lerp(col, Color.Lerp(col, Color.white, 0.4f), Sstep(0.82f, 0.95f, k));
                dome = 0.8f + 0.2f * dome;
            }
            else if (type == 3)
            {
                // Soft coral: a ring of wavy tentacles, their tips a brighter colour, a dark mouth in the middle.
                float ang = Mathf.Atan2(ny, nx);
                float tent = Mathf.Abs(Mathf.Sin(ang * 26f + Mathf.Sin(k * 14f + pick * 6f) * 1.2f + Fbm(u, v, Per(s, 0.4f), 2, s.seed + 12) * 4f));
                col = Color.Lerp(col * 0.7f, col * 1.15f, Sstep(0.2f, 0.8f, tent));
                col = Color.Lerp(col, new Color(0.9f, 0.85f, 1f), Sstep(0.75f, 1f, k) * 0.45f);
                if (k < 0.16f) col = Color.Lerp(new Color(0.25f, 0.08f, 0.12f), col, Sstep(0.08f, 0.16f, k));
            }
            else
            {
                dome = 0.9f + 0.1f * dome;   // a sea fan lies flat; its lace already lets the sand show through (ReefCoral)
                col *= 0.85f + 0.3f * (1f - k);
            }
            col *= (0.6f + 0.5f * lit) * (0.7f + 0.35f * dome);
            if (k > 0.85f && type != 4) col *= 0.82f;   // the colony's edge curving down
            c = Color.Lerp(c, col, coral);
        }

        // Water over everything: a turquoise tint and deeper blue hollows.
        c *= new Color(0.84f, 1f, 1.02f, 1f);
        float deep = Sstep(0.62f, 0.8f, Fbm(u, v, Per(s, 16f), 3, s.seed + 80));
        c = ScMix(c, c * new Color(0.3f, 0.55f, 0.85f, 1f), deep * 0.55f);
        return Fin(c);
    }

    /// <summary>Coral Reef: how much reef rock covers this spot (0..1).</summary>
    private static float ReefRock(FloorStyle s, float u, float v) { return Sstep(0.5f, 0.6f, Fbm(u, v, Per(s, 9f), 4, s.seed + 3)); }

    /// <summary>Coral Reef: crowded branch tips (1 at a tip's centre) on a jittered grid 'cell' world units across.</summary>
    private static float ReefTips(float wx, float wy, float cell, int seed)
    {
        int gx = Mathf.FloorToInt(wx / cell), gy = Mathf.FloorToInt(wy / cell);
        float best = 9f;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = gx + ox, iy = gy + oy;
                float dx = wx / cell - (ix + 0.2f + 0.6f * Hash(ix, iy, seed)), dy = wy / cell - (iy + 0.2f + 0.6f * Hash(ix, iy, seed + 1));
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
        return Mathf.Clamp01(1f - Mathf.Sqrt(best) / 0.45f);
    }

    /// <summary>
    /// Coral Reef colonies: coverage (0..1) at a world point, the colony's type (0 brain, 1 branching, 2 table, 3 soft, 4 sea fan),
    /// how far out from its middle (k, 0..1), the outward direction (nx, ny) and a per-colony random value. Colonies sit on a jittered grid,
    /// mostly on the reef rock, with lobed irregular outlines; the highest one wins where two overlap.
    /// </summary>
    private static float ReefCoral(FloorStyle s, float wx, float wy, float u, float v, out int type, out float k, out float nx, out float ny, out float pick)
    {
        type = 0; k = 1f; nx = ny = 0f; pick = 0f;
        const float cell = 2.2f;
        float W = Wu(s);
        int gx = Mathf.FloorToInt(wx / cell), gy = Mathf.FloorToInt(wy / cell);
        float best = 0f, bestOrder = -1f;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = gx + ox, iy = gy + oy;
                float cx = (ix + 0.2f + 0.6f * Hash(ix, iy, s.seed + 20)) * cell, cy = (iy + 0.2f + 0.6f * Hash(ix, iy, s.seed + 21)) * cell;
                float onRock = ReefRock(s, cx / W, cy / W);
                if (Hash(ix, iy, s.seed + 22) > 0.1f + 0.85f * onRock) continue;   // mostly on the rock, a few alone in the sand
                int t = Mathf.FloorToInt(Hash(ix, iy, s.seed + 23) * 5f);
                float r = 0.65f + 0.8f * Hash(ix, iy, s.seed + 24);
                float dx = wx - cx, dy = wy - cy;
                float lx = dx, ly = dy;
                if (t == 4)
                {
                    // A sea fan: a wide, thin fan shape standing across the current (stretched one way).
                    float a = Hash(ix, iy, s.seed + 25) * Mathf.PI;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    lx = (dx * ca + dy * sa) / 1.25f; ly = (-dx * sa + dy * ca) / 0.45f;
                }
                float ang = Mathf.Atan2(ly, lx);
                float p1 = Hash(ix, iy, s.seed + 26) * 6.28f, p2 = Hash(ix, iy, s.seed + 27) * 6.28f;
                float lobes = t == 1 || t == 3 ? 0.16f : 0.09f;
                float wob = 1f + lobes * Mathf.Sin(ang * 3f + p1) + lobes * 0.6f * Mathf.Sin(ang * 5f + p2) + 0.04f * Mathf.Sin(ang * 11f + p1 + p2);
                float d = Mathf.Sqrt(lx * lx + ly * ly) / (r * wob);
                if (d >= 1f) continue;
                float cover = 1f - Sstep(0.94f, 1f, d);
                if (t == 4)
                {
                    // The fan's lace: a net of fine branches with sand showing through the holes.
                    float a2 = Mathf.Atan2(ly, lx + 1.1f), rr = Mathf.Sqrt((lx + 1.1f) * (lx + 1.1f) + ly * ly);
                    float ribs = Mathf.Abs(Mathf.Sin(a2 * 38f)), cross = Mathf.Abs(Mathf.Sin(rr * 26f + Mathf.Sin(a2 * 9f)));
                    cover *= Mathf.Max(1f - Sstep(0.2f, 0.4f, ribs), 1f - Sstep(0.18f, 0.35f, cross));
                }
                float order = Hash(ix, iy, s.seed + 28);
                if (cover <= 0.02f || order <= bestOrder) continue;
                bestOrder = order;
                best = cover; type = t; k = d; pick = Hash(ix, iy, s.seed + 29);
                float len = Mathf.Max(1e-4f, Mathf.Sqrt(dx * dx + dy * dy));
                nx = dx / len * d; ny = dy / len * d;
            }
        return best;
    }

    // ------------------------------------------------------------------
    // 9. Casino Felt: green baize with gold betting rings, embossed suits, chips, dice and a fan of cards
    // ------------------------------------------------------------------

    private static bool InSuit(int suit, float px, float py)
    {
        switch (suit)
        {
            case 0: return InHeart(px, py);
            case 1: return Mathf.Abs(px) * 1.15f + Mathf.Abs(py) < 1f;
            case 2:   // spade: a flipped heart with a stem
                return InHeart(px, -py + 0.2f) && py > -0.85f || (Mathf.Abs(px) < 0.14f * (1f + (-py - 0.4f)) && py < -0.35f && py > -1f);
            default:  // club: three circles and a stem
            {
                float a = Mathf.Sqrt(px * px + (py - 0.42f) * (py - 0.42f)), b = Mathf.Sqrt((px + 0.44f) * (px + 0.44f) + (py + 0.08f) * (py + 0.08f)), c2 = Mathf.Sqrt((px - 0.44f) * (px - 0.44f) + (py + 0.08f) * (py + 0.08f));
                return a < 0.4f || b < 0.4f || c2 < 0.4f || (Mathf.Abs(px) < 0.12f * (1f + (-py - 0.3f)) && py < -0.2f && py > -1f);
            }
        }
    }

    private static readonly float[] CasinoRings = { 4.2f, 8.4f, 12.2f };

    // pip positions: a diagonal 3 first, then the other two corners (five = 3 + 2)
    private static readonly float[] DieSpots = { -0.28f, 0.28f, 0f, 0f, 0.28f, -0.28f, 0.28f, 0.28f, -0.28f, -0.28f };

    private static Color ChipColor(int i, Color red)
    {
        switch (i % 5)
        {
            case 0: return red;
            case 1: return new Color(0.1f, 0.2f, 0.7f);
            case 2: return new Color(0.06f, 0.06f, 0.07f);
            case 3: return new Color(0.1f, 0.55f, 0.25f);
            default: return new Color(0.92f, 0.92f, 0.9f);
        }
    }

    private static Color ShadeCasino(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W, cx = W * 0.5f, cy = W * 0.5f, aa = W / n;
        // felt
        Color c = s.colorA * (0.82f + 0.3f * Fbm(u, v, Per(s, 6f), 3, s.seed)) * (0.9f + 0.18f * Hash(x, y, s.seed + 1));
        if (Hash(x / 2, y / 2, s.seed + 2) > 0.9f) c *= 1.1f;   // fibres

        // embossed suits in a loose grid (darker felt)
        float sc = 3.2f;
        int sx = Mathf.FloorToInt(wx / sc), sy = Mathf.FloorToInt(wy / sc);
        float fx = (wx / sc - sx - 0.5f) * 2f, fy = (wy / sc - sy - 0.5f) * 2f;
        if (InSuit((sx + sy * 2) & 3, fx * 1.5f, fy * 1.5f)) c *= 0.88f;

        Color gold = s.colorB;
        float dc = Mathf.Sqrt((wx - cx) * (wx - cx) + (wy - cy) * (wy - cy));
        // gold rings and the outer line
        for (int ri = 0; ri < CasinoRings.Length; ri++)
        {
            float rd = Mathf.Abs(dc - CasinoRings[ri]);
            if (rd < 0.1f) c = gold * (0.85f + 0.2f * Hash(x, y, s.seed + 3));
        }
        float spoke = Mathf.Abs(Frac(Mathf.Atan2(wy - cy, wx - cx) / 6.283f * 12f) - 0.5f);
        if (dc > 4.2f && dc < 8.4f && spoke < 0.012f * (8f / Mathf.Max(dc, 1f))) c = gold * 0.9f;

        // betting circles on the outer ring
        for (int i = 0; i < 7; i++)
        {
            float a = Mathf.PI * (0.15f + 0.7f * i / 6f) + Mathf.PI;
            float bx = cx + Mathf.Cos(a) * 10.3f, by = cy - Mathf.Sin(a) * 10.3f * -1f;
            float bd = Mathf.Sqrt((wx - bx) * (wx - bx) + (wy - by) * (wy - by));
            if (Mathf.Abs(bd - 0.9f) < 0.08f) c = gold;
        }

        // chips
        for (int i = 0; i < 9; i++)
        {
            float px = cx + (Hash(i, 1, s.seed + 50) - 0.5f) * 20f, py = cy + (Hash(i, 2, s.seed + 50) - 0.5f) * 15f;
            float d = Mathf.Sqrt((wx - px) * (wx - px) + (wy - py) * (wy - py));
            float sh = Mathf.Sqrt((wx - px - 0.1f) * (wx - px - 0.1f) + (wy - py + 0.1f) * (wy - py + 0.1f));
            if (sh < 0.58f && d > 0.55f) c *= 0.7f;
            if (d < 0.55f)
            {
                Color cc = ChipColor(i, s.colorC);
                float ang = Mathf.Atan2(wy - py, wx - px);
                if (d > 0.4f && Frac(ang / 6.283f * 8f) < 0.5f) cc = Color.white * 0.95f;
                else if (Mathf.Abs(d - 0.3f) < 0.025f) cc = Color.white * 0.9f;
                else if (d < 0.18f) cc *= 0.8f;
                c = cc * (0.9f + 0.2f * Sstep(-0.5f, 0.5f, -(wx - px) * 0.7f + (wy - py) * 0.7f));
            }
        }

        // two dice
        for (int i = 0; i < 2; i++)
        {
            float px = cx + (i == 0 ? -2.2f : 2.9f), py = cy + (i == 0 ? -1.6f : 1.1f);
            Rot2(wx - px, wy - py, i == 0 ? -18f : 27f, out float dx, out float dy);
            float box = ScBox(dx, dy, -0.5f, -0.5f, 0.5f, 0.5f, aa);
            float shd = ScBox(dx - 0.1f, dy + 0.1f, -0.5f, -0.5f, 0.5f, 0.5f, aa);
            if (shd > 0f && box < 1f) c *= 1f - 0.4f * shd;
            if (box > 0f)
            {
                Color die = Color.Lerp(new Color(0.95f, 0.94f, 0.9f), new Color(0.8f, 0.8f, 0.78f), Sstep(-0.5f, 0.5f, dx * 0.5f - dy * 0.5f));
                int pips = i == 0 ? 5 : 3;
                for (int p = 0; p < pips; p++)
                {
                    float spx = DieSpots[p * 2], spy = DieSpots[p * 2 + 1];   // (the first three spots are the 3 face; five uses them all)
                    float dd = Mathf.Sqrt((dx - spx) * (dx - spx) + (dy - spy) * (dy - spy));
                    if (dd < 0.07f) die = i == 1 ? s.colorC : new Color(0.08f, 0.08f, 0.08f);
                }
                c = ScMix(c, die, box);
            }
        }

        // a fan of three cards
        for (int i = 0; i < 3; i++)
        {
            float px = cx + 5.4f + i * 0.5f, py = cy - 3.2f;
            Rot2(wx - px, wy - py, -22f + i * 22f, out float dx, out float dy);
            dy -= 1.1f;
            float box = ScBox(dx, dy, -0.8f, -1.15f, 0.8f, 1.15f, aa);
            float shd = ScBox(dx - 0.1f, dy + 0.1f, -0.8f, -1.15f, 0.8f, 1.15f, aa);
            if (shd > 0f && box < 1f) c *= 1f - 0.4f * shd;
            if (box > 0f)
            {
                Color card = new Color(0.97f, 0.96f, 0.93f);
                bool red = i != 1;
                int suit = i == 0 ? 0 : i == 1 ? 2 : 1;
                Color ink = red ? s.colorC : new Color(0.07f, 0.07f, 0.09f);
                if (InSuit(suit, dx / 0.4f, (dy - 0.05f) / 0.4f)) card = ink;
                if (InSuit(suit, (dx + 0.55f) / 0.13f, (dy - 0.85f) / 0.13f)) card = ink;
                if (InSuit(suit, (dx - 0.55f) / 0.13f, (dy + 0.85f) / 0.13f)) card = ink;
                if (ScBox(dx, dy, -0.7f, -1.05f, 0.7f, 1.05f, aa) < 0.5f) card = ScMix(card, new Color(0.7f, 0.68f, 0.64f), 0.5f);
                c = ScMix(c, card, box);
            }
        }
        return Fin(c);
    }

    // ------------------------------------------------------------------
    // 10. Stained Glass: jewel-coloured Voronoi panes in lead, a rose window under the cube, light shafts
    // ------------------------------------------------------------------

    private static Color ShadeGlass(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W, cx = W * 0.5f, cy = W * 0.5f;
        float dx = wx - cx, dy = wy - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
        Color glass;
        float leadEdge;          // 0 = in the lead, grows into the pane
        float shade;             // brightness inside a pane (brighter in the middle)

        if (r < 9.2f)
        {
            // rose window: sectors and rings
            int sectors = 12;
            float ang = Mathf.Atan2(dy, dx) / 6.283f + 0.5f;
            int sec = Mathf.FloorToInt(ang * sectors);
            float sf = Frac(ang * sectors);
            int ring = r < 1.6f ? 0 : r < 3.4f ? 1 : r < 6f ? 2 : 3;
            float ringIn = ring == 0 ? 0f : ring == 1 ? 1.6f : ring == 2 ? 3.4f : 6f, ringOut = ring == 0 ? 1.6f : ring == 1 ? 3.4f : ring == 2 ? 6f : 9.2f;
            float angDist = Mathf.Min(sf, 1f - sf) * 6.283f * r / sectors;
            float radDist = Mathf.Min(r - ringIn, ringOut - r);
            leadEdge = ring == 0 ? radDist : Mathf.Min(angDist, radDist);
            int pane = ring * 17 + (ring == 0 ? 0 : sec);
            float pick = HashId(pane, s.seed);
            glass = pick < 0.34f ? s.colorA : pick < 0.67f ? s.colorB : s.colorC;
            glass = Color.Lerp(glass, Hue(HashId(pane, s.seed + 5), 0.8f, 1f), 0.25f);
            if (ring == 0) glass = s.colorC;
            shade = Mathf.Clamp01(leadEdge / 0.9f);
        }
        else
        {
            int cc = Per(s, 2.3f);
            Voronoi(u, v, cc, s.seed + 1, out float f1, out float f2, out int id);
            leadEdge = (f2 - f1) * cc * (W / cc);
            float pick = HashId(id, s.seed);
            glass = pick < 0.34f ? s.colorA : pick < 0.67f ? s.colorB : s.colorC;
            glass = Color.Lerp(glass, Hue(HashId(id, s.seed + 5), 0.8f, 1f), 0.3f);
            shade = Mathf.Clamp01(1f - f1 * cc * 1.3f);
        }

        Color c;
        const float lead = 0.12f;
        if (leadEdge < lead)
        {
            float k = leadEdge / lead;
            c = new Color(0.1f, 0.1f, 0.12f) * (0.8f + 0.5f * Mathf.Sin(k * 3.14f)) + new Color(0.18f, 0.18f, 0.2f) * Sstep(0.35f, 0.5f, k) * (1f - Sstep(0.5f, 0.65f, k));
        }
        else
        {
            float swirl = Fbm(u, v, Per(s, 0.9f), 4, s.seed + 9);
            c = glass * (0.55f + 0.5f * shade + 0.25f * swirl);
            c += glass * 0.35f * Sstep(0.6f, 0.9f, swirl);                                   // streaky bright glass
            if (Hash(x / 2, y / 2, s.seed + 10) > 0.997f) c = ScMix(c, Color.white, 0.8f);   // bubbles
            if (leadEdge < lead * 2f) c *= 0.75f + 0.25f * (leadEdge - lead) / lead;
        }

        // light shafts falling across the window
        float beam = Mathf.Sin((wx * 0.55f + wy * 0.35f) * 0.8f + 1f);
        c += new Color(1f, 0.95f, 0.8f) * Sstep(0.72f, 1f, beam) * 0.18f;
        return Fin(c);
    }

    private static Color Hue(float h, float sat, float val) { return Color.HSVToRGB(Frac(h), sat, val); }
}
