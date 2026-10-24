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
            case FloorPattern.BattleCrater: return ShadeCraterField(s, u, v, x, y, n);
            case FloorPattern.Hyperspace: return ShadeHyperspace(s, u, v, cells);
            case FloorPattern.DataStream: return ShadeDataStream(s, u, v, cells);
            case FloorPattern.CircuitFlow: return ShadeCircuitFlow(s, u, v, n, cells);
            case FloorPattern.CyberHex: return ShadeCyberHex(s, u, v, cells);
            case FloorPattern.Glitch: return ShadeGlitch(s, u, v, n);
            case FloorPattern.Psychedelic: return ShadePsychedelic(s, u, v);
            case FloorPattern.HypnoSpiral: return ShadeHypno(s, u, v, cells);
            case FloorPattern.Plasma: return ShadePlasma(s, u, v);
            case FloorPattern.OpArt: return ShadeOpArt(s, u, v, cells);
            case FloorPattern.HauntedTiles: return ShadeHauntedTiles(s, u, v, x, y, cells);
            case FloorPattern.Graveyard: return ShadeGraveyard(s, u, v, x, y);
            case FloorPattern.WatchingEyes: return ShadeEyes(s, u, v, cells);
            case FloorPattern.Skyline: return ShadeSkyline(s, u, v);
            case FloorPattern.GroundSky: return ShadeGroundSky(s, u, v, x, y, cells);
            case FloorPattern.MineTrack: return ShadeMineTrack(s, u, v, x, y, cells);
            case FloorPattern.OreVeins: return ShadeOreVeins(s, u, v, x, y, cells);
            case FloorPattern.Strata: return ShadeStrata(s, u, v, x, y, cells);
            case FloorPattern.CropRows: return ShadeCropRows(s, u, v, x, y, cells);
            case FloorPattern.WheatField: return ShadeWheat(s, u, v, x, y);
            case FloorPattern.Patchwork: return ShadePatchwork(s, u, v, x, y, cells);
        }
        return ShadeRetro(s, u, v, x, y, n, cells);
    }

    // ---- small helpers ----

    /// <summary>Smooth 0..1 step between two edges (the shader-style smoothstep; Mathf.SmoothStep is a different thing).</summary>
    private static float Sstep(float e0, float e1, float x)
    {
        // Like the shader's smoothstep, the edges may come in either order (Sstep(1, 0.9, k) = 1 inside 0.9, fading to 0 at 1).
        // (It used to clamp a falling range to 1e-6, so every call with e0 > e1 was a hard 0 / 1 step and Coral Reef's corals never showed.)
        float range = e1 - e0;
        if (Mathf.Abs(range) < 1e-6f) return x < e0 ? 0f : 1f;
        float t = Mathf.Clamp01((x - e0) / range);
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

    // ------------------------------------------------------------------
    // Space
    // ------------------------------------------------------------------

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

        Color c = s.colorA * (0.9f + 0.15f * CircuitGround(u, v, n, s.seed));
        float trace = 0f, pulse = 0f;
        float wTrace = 1.3f * texel * Mathf.Max(1f, 24f / cells);   // (thicker traces when the board is zoomed in)

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

        // The bright sparks wandering across the circuits (positions worked out once per frame in PrepareSparks).
        float spark = 0f, sparkGlow = 0f;
        // Only the cells next to a spark need the (comparatively costly) glow maths; sparkNear is filled once per frame.
        bool near = sparkNear != null && sparkNear.Length == cells * cells && sparkNear[Wrap(cj, cells) * cells + Wrap(ci, cells)];
        for (int k = 0; near && k < sparkCount; k++)
        {
            float w = k % SparkTrail == 0 ? 1f : Mathf.Pow(0.72f, k % SparkTrail);
            float dx = Mathf.Abs(Mathf.Repeat(gu - sparkX[k] + cells * 0.5f, cells) - cells * 0.5f);
            if (dx > 1.1f) continue;
            float dy = Mathf.Abs(Mathf.Repeat(gv - sparkY[k] + cells * 0.5f, cells) - cells * 0.5f);
            if (dy > 1.1f) continue;
            float d2 = dx * dx + dy * dy;
            spark += w * Mathf.Exp(-d2 / 0.012f);
            sparkGlow += w * Mathf.Exp(-d2 / 0.18f);
        }
        c += Color.white * Mathf.Clamp01(spark) * 1.2f + s.colorC * sparkGlow * 0.55f;
        c.a = 1f;
        return c;
    }

    // ---- cached background noise (it never changes, so it is worked out once per size / seed) ----
    private static float[] circuitNoise;
    private static int circuitNoiseN, circuitNoiseSeed;

    /// <summary>A fresh play-mode start (domain reload off) drops the cached circuit noise and the sparks' state.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneStatics() { circuitNoise = null; circuitNoiseN = 0; circuitNoiseSeed = 0; sparkNear = null; sparkCount = 0; }

    private static float CircuitGround(float u, float v, int n, int seed)
    {
        if (shadeBase) return Fbm(u, v, 5, 3, seed);   // (the still picture is drawn once, a frame at a time: no big cache to build in one go)
        if (circuitNoise == null || circuitNoiseN != n || circuitNoiseSeed != seed)
        {
            circuitNoise = new float[n * n];
            for (int yy = 0; yy < n; yy++)
                for (int xx = 0; xx < n; xx++)
                    circuitNoise[yy * n + xx] = Fbm((xx + 0.5f) / n, (yy + 0.5f) / n, 5, 3, seed);
            circuitNoiseN = n; circuitNoiseSeed = seed;
        }
        int ix = Mathf.Clamp(Mathf.FloorToInt(u * n), 0, n - 1), iy = Mathf.Clamp(Mathf.FloorToInt(v * n), 0, n - 1);
        return circuitNoise[iy * n + ix];
    }

    private static bool[] sparkNear;

    // ---- the wandering sparks of Circuit Flow ----
    private const int SparkTrail = 7, SparkWalkers = 2, SparkSteps = 120;
    private static int sparkCount;
    private static readonly float[] sparkX = new float[SparkTrail * SparkWalkers], sparkY = new float[SparkTrail * SparkWalkers];

    private static readonly int[] sparkDirs = new int[4], sparkPick = new int[4];
    private static readonly int[] sparkNi = new int[SparkSteps + 3], sparkNj = new int[SparkSteps + 3];

    private static bool RightEdge(int i, int j, int cells, int seed) { return Hash(Wrap(i, cells), Wrap(j, cells), seed) > 0.42f; }
    private static bool UpEdge(int i, int j, int cells, int seed) { return Hash(Wrap(i, cells), Wrap(j, cells), seed + 5) > 0.42f; }

    /// <summary>Walks a spark along the traces: every few seconds it starts at a random node and follows the circuit, turning at random junctions.</summary>
    private static void PrepareSparks(FloorStyle s, float t)
    {
        int cells = Mathf.Max(2, s.cells);
        const float edgesPerSecond = 3.2f, epochSeconds = 22f;
        int[] ni = sparkNi, nj = sparkNj;
        sparkCount = 0;
        for (int w = 0; w < SparkWalkers; w++)
        {
            float local = t + w * epochSeconds * 0.5f;
            int epoch = Mathf.FloorToInt(local / epochSeconds);
            float p = (local - epoch * epochSeconds) * edgesPerSecond;
            int steps = Mathf.Min(SparkSteps, Mathf.FloorToInt(p) + 2);
            int seed = s.seed + w * 31;
            ni[0] = Mathf.FloorToInt(Hash(epoch, w, seed + 1) * cells);
            nj[0] = Mathf.FloorToInt(Hash(epoch, w + 7, seed + 2) * cells);
            int prevDir = -1;   // 0 right, 1 left, 2 up, 3 down
            for (int k = 1; k <= steps; k++)
            {
                int i = ni[k - 1], j = nj[k - 1];
                int[] dirs = sparkDirs; int count = 0;
                if (RightEdge(i, j, cells, s.seed)) dirs[count++] = 0;
                if (RightEdge(i - 1, j, cells, s.seed)) dirs[count++] = 1;
                if (UpEdge(i, j, cells, s.seed)) dirs[count++] = 2;
                if (UpEdge(i, j - 1, cells, s.seed)) dirs[count++] = 3;
                // Don't turn straight back unless it is a dead end.
                int back = prevDir == 0 ? 1 : prevDir == 1 ? 0 : prevDir == 2 ? 3 : prevDir == 3 ? 2 : -1;
                int usable = 0; int[] pick = sparkPick;
                for (int d = 0; d < count; d++) if (dirs[d] != back) pick[usable++] = dirs[d];
                int chosen;
                if (usable > 0) chosen = pick[Mathf.Min(usable - 1, Mathf.FloorToInt(Hash(i + epoch * 13, j + k, seed + 9) * usable))];
                else if (count > 0) chosen = dirs[0];
                else chosen = Hash(i, j + k, seed + 3) > 0.5f ? 0 : 2;       // an isolated node: jump on
                ni[k] = i + (chosen == 0 ? 1 : chosen == 1 ? -1 : 0);
                nj[k] = j + (chosen == 2 ? 1 : chosen == 3 ? -1 : 0);
                prevDir = chosen;
            }
            for (int tr = 0; tr < SparkTrail; tr++)
            {
                float q = p - tr * 0.2f;
                if (q < 0f) q = 0f;
                int idx = Mathf.Min(steps - 1, Mathf.FloorToInt(q));
                float f = Mathf.Min(1f, q - idx);
                int slot = w * SparkTrail + tr;
                sparkX[slot] = Mathf.Lerp(ni[idx], ni[idx + 1], f) + 0.5f;
                sparkY[slot] = Mathf.Lerp(nj[idx], nj[idx + 1], f) + 0.5f;
            }
            sparkCount += SparkTrail;
        }

        // Flag every cell within one cell of a spark point.
        if (sparkNear == null || sparkNear.Length != cells * cells) sparkNear = new bool[cells * cells];
        else System.Array.Clear(sparkNear, 0, sparkNear.Length);
        for (int k = 0; k < sparkCount; k++)
        {
            int ci = Mathf.FloorToInt(sparkX[k]), cj = Mathf.FloorToInt(sparkY[k]);
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    sparkNear[Wrap(cj + oy, cells) * cells + Wrap(ci + ox, cells)] = true;
        }
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
        if (s.animateFps > 0f)
        {
            float scan = Mathf.Clamp01(1f - Mathf.Abs(Frac(v - t * 0.07f) - 0.5f) / 0.03f);             // a bright band sweeping across
            c += s.colorB * scan * 0.18f * (0.5f + line);
        }
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

    private static Color ShadeHauntedTiles(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        float t = shadeTime;
        int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
        Color c = ((cx + cy) & 1) == 0 ? s.colorA : s.colorB;
        float grime = Fbm(u, v, 5 * shadeDetail, 4, s.seed);
        c *= 0.55f + 0.6f * grime;
        float fu = Frac(u * cells), fv = Frac(v * cells);
        if (Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)) < 0.02f) c *= 0.4f;     // grout
        Voronoi(u, v, cells, s.seed + 3, out float f1, out float f2, out int id);
        if ((f2 - f1) * cells < 0.03f && HashId(id, s.seed) > 0.55f) c *= 0.3f;                // cracks in some tiles
        // Dried stains.
        float stain = Sstep(0.62f, 0.7f, Fbm(u, v, 3 * shadeDetail, 3, s.seed + 8));
        c = ScMix(c, s.colorC * 0.6f, stain * 0.7f);
        // (the light flickers now and then: PixelFloor.UpdateHauntedFlicker dims the whole material)
        c.a = 1f;
        return c;
    }

    private static Color ShadeGraveyard(FloorStyle s, float u, float v, int x, int y)
    {
        float t = shadeTime;
        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 6 * shadeDetail, 4, s.seed));
        c *= 0.82f + 0.3f * Hash(x, y, s.seed);
        if (Hash(x, y, s.seed + 3) > 0.965f) c = ScMix(c, new Color(0.36f, 0.34f, 0.16f), 0.8f);          // dead grass
        if (Hash(x / 2, y / 2, s.seed + 5) > 0.992f) c = ScMix(c, new Color(0.8f, 0.78f, 0.7f), 0.8f);   // bits of bone
        float fog = Sstep(0.42f, 0.82f, Fbm(u, v, 3 * shadeDetail, 4, s.seed + 8));
        c = ScMix(c, s.colorC, fog * 0.6f);
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

    // ------------------------------------------------------------------
    // Mining
    // ------------------------------------------------------------------

    /// <summary>Gravel with a pair of rails and wooden sleepers running across it, a few metal flecks in the stone. Tiles.</summary>
    private static Color ShadeMineTrack(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 8, 3, s.seed));
        c *= 0.78f + 0.4f * Hash(x, y, s.seed);
        if (Hash(x / 2, y / 2, s.seed + 3) > 0.965f) c = ScMix(c, s.colorB * 1.5f, 0.7f);                  // pale pebbles
        if (Hash(x, y, s.seed + 7) > 0.996f) c = ScMix(c, s.colorC, 0.9f);                                    // glinting ore flecks

        // Sleepers: wooden planks across the track.
        float su = Frac(u * cells);
        if (v > 0.2f && v < 0.8f && su > 0.12f && su < 0.88f)
        {
            float grain = Fbm(u, v, cells * 2, 18, 2, s.seed + 4);
            Color wood = Color.Lerp(new Color(0.32f, 0.2f, 0.1f), new Color(0.5f, 0.33f, 0.18f), grain) * (0.85f + 0.2f * Hash(Mathf.FloorToInt(u * cells), 2, s.seed));
            if (su < 0.16f || su > 0.84f || v < 0.215f || v > 0.785f) wood *= 0.65f;                          // dark edges
            c = wood;
        }
        // Two steel rails with a bright top edge, nails where they cross a sleeper.
        for (int r = 0; r < 2; r++)
        {
            float rc = r == 0 ? 0.36f : 0.64f;
            float d = Mathf.Abs(v - rc);
            if (d < 0.022f) c = Color.Lerp(new Color(0.3f, 0.31f, 0.34f), new Color(0.72f, 0.74f, 0.78f), 1f - d / 0.022f);
            else if (d < 0.03f) c *= 0.55f;
        }
        return c;
    }

    /// <summary>Dark rock whose cracks are filled with glittering metal veins and a few embedded crystals. Tiles.</summary>
    private static Color ShadeOreVeins(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        Color c = Color.Lerp(s.colorA, s.colorA * 1.8f, Fbm(u, v, 6, 4, s.seed));
        c *= 0.8f + 0.3f * Hash(x, y, s.seed);
        Voronoi(u, v, cells, s.seed + 1, out float f1, out float f2, out int id);
        float edge = (f2 - f1) * cells;
        float width = 0.05f + 0.07f * Fbm(u, v, 10, 2, s.seed + 2);
        if (edge < width && HashId(id, s.seed) > 0.35f)
        {
            float k = 1f - edge / width;
            Color vein = Color.Lerp(s.colorB * 0.7f, s.colorB * 1.3f, Hash(x, y, s.seed + 5));
            if (Hash(x, y, s.seed + 6) > 0.93f) vein = Color.white;                                           // glitter
            c = ScMix(c, vein, Mathf.Clamp01(k * 2.2f));
        }
        // A crystal cluster in some cells: faceted, glowing from within.
        float d = f1 * cells;
        if (HashId(id, s.seed + 8) > 0.72f && d < 0.1f)
        {
            float facet = 0.55f + 0.5f * Mathf.Abs(Mathf.Sin(Mathf.Atan2(v * cells - Mathf.Floor(v * cells) - 0.5f, u * cells - Mathf.Floor(u * cells) - 0.5f) * 3f));
            c = s.colorC * facet * (1f - d * 4f) + c * 0.2f;
            c += s.colorC * 0.25f;
        }
        return c;
    }

    /// <summary>Layers of sediment in different rock colours with wavy boundaries, grit and pebbles. Tiles.</summary>
    private static Color ShadeStrata(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        float warp = (Fbm(u, v, 3, 3, s.seed) - 0.5f) * 1.8f;
        float band = v * cells + warp;
        int layer = Mathf.FloorToInt(band);
        float inside = band - layer;
        float h = HashId(Wrap(layer, cells), s.seed);
        Color c = h < 0.33f ? Color.Lerp(s.colorA, s.colorB, h * 3f) : h < 0.66f ? Color.Lerp(s.colorB, s.colorC, (h - 0.33f) * 3f) : Color.Lerp(s.colorC, s.colorA, (h - 0.66f) * 3f);
        c *= 0.85f + 0.25f * Fbm(u, v, 24, cells * 4, 2, s.seed + 3);                                         // grain along the layer
        c *= 0.9f + 0.2f * Hash(x, y, s.seed + 4);
        if (inside < 0.06f) c *= 0.6f + 0.4f * (inside / 0.06f);                                              // dark join between layers
        if (Hash(x / 2, y / 2, s.seed + 5) > 0.985f) c = ScMix(c, c * 1.5f, 0.8f);                           // pebbles
        return c;
    }

    // ------------------------------------------------------------------
    // Farming
    // ------------------------------------------------------------------

    /// <summary>Ploughed rows of earth with little green seedlings of different sizes growing along them. Tiles.</summary>
    private static Color ShadeCropRows(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        float rv = Frac(v * cells);
        float ridge = Mathf.Sin(rv * Mathf.PI);
        Color c = Color.Lerp(s.colorA, s.colorB, ridge) * (0.85f + 0.25f * Hash(x, y, s.seed));
        c *= 0.9f + 0.2f * Fbm(u, v, 8, 3, s.seed);

        // One seedling per cell along the ridge: a star of leaves around a stem.
        int ci = Mathf.FloorToInt(u * cells), cj = Mathf.FloorToInt(v * cells);
        float h = Hash(Wrap(ci, cells), Wrap(cj, cells), s.seed + 2);
        if (h > 0.12f)
        {
            float dx = Frac(u * cells) - 0.5f, dy = rv - 0.5f;
            float scale = 0.45f + 0.55f * Hash(Wrap(ci, cells), Wrap(cj, cells), s.seed + 3);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx);
            float leaf = (0.12f + 0.2f * Mathf.Abs(Mathf.Cos(ang * 2f + h * 6f))) * scale;
            if (r < leaf)
            {
                Color green = Color.Lerp(s.colorC * 0.7f, s.colorC * 1.25f, 1f - r / Mathf.Max(0.01f, leaf));
                c = green * (0.85f + 0.3f * Mathf.Cos(ang - 0.8f));
            }
            else if (r < leaf + 0.05f && dx > 0f) c *= 0.7f;                                                  // a soft shadow to the right
        }
        return c;
    }

    /// <summary>A ripe wheat field seen from above: golden streaks and seed heads with waves of wind rolling through. Animated, tiles.</summary>
    private static Color ShadeWheat(FloorStyle s, float u, float v, int x, int y)
    {
        float t = shadeTime;
        float wave = Mathf.Sin((u * 2f + v * 3f) * Mathf.PI * 2f - t * 1.1f) * 0.5f + Mathf.Sin((u * 5f - v * 2f) * Mathf.PI * 2f + t * 0.7f) * 0.25f;
        float streak = Fbm(u + wave * 0.01f, v, 48, 6, 3, s.seed);                                            // stalks run along the field
        Color c = Color.Lerp(s.colorA, s.colorB, streak);
        c *= 0.82f + 0.22f * wave + 0.1f;
        if (Hash(x, y, s.seed + 4) > 0.93f) c = ScMix(c, s.colorC, 0.7f);                                     // seed heads
        return c;
    }

    /// <summary>A patchwork of fields from the air: crops, wheat, ploughed earth, meadow and flowers, with hedges between. Tiles.</summary>
    private static Color ShadePatchwork(FloorStyle s, float u, float v, int x, int y, int cells)
    {
        float gu = u * cells, gv = v * cells;
        int ci = Mathf.FloorToInt(gu), cj = Mathf.FloorToInt(gv);
        float fu = gu - ci, fv = gv - cj;
        int wi = Wrap(ci, cells), wj = Wrap(cj, cells);
        float h = Hash(wi, wj, s.seed);
        bool alongX = Hash(wi, wj, s.seed + 1) > 0.5f;
        float line = Frac((alongX ? fv : fu) * 14f);
        float tone = 0.88f + 0.2f * Hash(x, y, s.seed + 2);

        Color c;
        if (h < 0.25f) c = (line < 0.5f ? new Color(0.28f, 0.62f, 0.22f) : new Color(0.2f, 0.5f, 0.16f)) * tone;          // crop rows
        else if (h < 0.5f) c = (line < 0.5f ? s.colorB : s.colorB * 0.88f) * tone;                                        // wheat
        else if (h < 0.7f) c = (line < 0.5f ? s.colorC : s.colorC * 0.75f) * tone;                                        // ploughed earth
        else if (h < 0.88f) c = new Color(0.45f, 0.72f, 0.3f) * (0.9f + 0.15f * Fbm(u, v, 12, 3, s.seed + 5));            // meadow
        else
        {
            c = new Color(0.4f, 0.68f, 0.28f) * tone;
            if (Hash(x / 2, y / 2, s.seed + 6) > 0.9f) c = Hash(x, y, s.seed + 7) > 0.5f ? new Color(0.95f, 0.85f, 0.2f) : new Color(0.85f, 0.4f, 0.7f);   // flowers
        }
        // Hedges along the field borders.
        float edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)) * cells;
        float hedge = 0.045f + 0.02f * Hash(x, y, s.seed + 8);
        if (edge * 1f / cells < hedge) c = ScMix(s.colorA * (0.8f + 0.4f * Hash(x, y, s.seed + 9)), c, 0f);
        return c;
    }

    private static Color Sun(Color under, float px, float v, float cx, float cy, float radius, Color colour)
    {
        float d = Mathf.Sqrt((px - cx) * (px - cx) + (v - cy) * (v - cy));
        under += colour * Mathf.Exp(-d / (radius * 2.2f)) * 0.3f;
        return ScMix(under, colour, 1f - Sstep(radius * 0.93f, radius, d));
    }
}
