using UnityEngine;
using static PixelNoise;

/// <summary>
/// Moon Surface and Red Planet (part of <see cref="PixelFloor"/>): two big still pictures drawn once at 1024 px over ~40 world units, each its own place instead of one crater field in two colours.
/// They share the crater list (<see cref="PrepareCraters"/>: one big crater where the camera looks, medium ones, pits) but not the look:
/// MOON - highland regolith, smooth dark basalt seas, ejecta halos, a TERRACED big crater with a central peak, bright rays and secondary crater chains, a rille, boulders with long shadows and a rover's tracks.
/// MARS - wind-carved dunes with sharp lee slopes, dark basalt streaks, dust-filled craters with frost in the shade, a cracked dry lakebed, boulders and dust devil trails.
/// Both only READ shared state (the crater list), so they are drawn on worker threads.
/// </summary>
public partial class PixelFloor
{
    private static float LightShade(float slope) { return 1f - Mathf.Clamp(slope * 0.45f, -0.7f, 0.7f); }

    /// <summary>Slope of the crater height field towards the light (upper left).</summary>
    private static float CraterSlope(float u, float v, int n)
    {
        float e = 1.4f / n;
        const float lx = -0.6f, ly = 0.8f;
        return (CraterHeightOnly(u + lx * e, v + ly * e) - CraterHeightOnly(u - lx * e, v - ly * e)) / (2f * e);
    }

    /// <summary>A boulder with a long shadow: 'cell' world units per chance, light from the upper left (the shadow falls to the lower right). Returns the new colour.</summary>
    private static Color Boulders(Color c, FloorStyle s, float wx, float wy, float cell, float chance, int seed, Color rock, float sizeMin, float sizeMax)
    {
        int ix = Mathf.FloorToInt(wx / cell), iy = Mathf.FloorToInt(wy / cell);
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = ix + ox, cy = iy + oy;
                if (Hash(cx, cy, seed) < chance) continue;
                float bx = (cx + 0.2f + 0.6f * Hash(cx, cy, seed + 1)) * cell, by = (cy + 0.2f + 0.6f * Hash(cx, cy, seed + 2)) * cell;
                float r = sizeMin + (sizeMax - sizeMin) * Hash(cx, cy, seed + 3);
                float dx = wx - bx, dy = wy - by;
                // The shadow: a stretched ellipse behind the rock.
                float sh = ScEll(dx, dy, 0.55f * r * 2.2f, -0.75f * r * 2.2f, r * 1.9f, r * 0.8f, 0.04f);
                c = ScMix(c, c * 0.5f, sh * 0.7f);
                float body = ScEll(dx, dy, 0f, 0f, r, r * 0.82f, 0.04f);
                if (body > 0f)
                {
                    float lit = 0.7f + 0.55f * Mathf.Clamp01(0.5f - (dx * 0.6f - dy * 0.8f) / (2f * r));
                    Color b = rock * lit * (0.85f + 0.3f * Hash(cx, cy, seed + 4));
                    c = ScMix(c, b, body);
                }
            }
        return c;
    }

    // ------------------------------------------------------------------
    // Moon
    // ------------------------------------------------------------------

    private static Color ShadeMoon(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        int D = shadeDetail;

        // Highlands and the dark, smooth basalt seas.
        float mare = Sstep(0.5f, 0.62f, Fbm(u, v, 3, 4, s.seed + 50));
        Color hi = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 4 * D, 4, s.seed));
        Color lo = Color.Lerp(s.colorC * 1.7f, s.colorB * 0.7f, Fbm(u, v, 6 * D, 3, s.seed + 3)) * new Color(0.92f, 0.96f, 1.06f, 1f);
        Color c = Color.Lerp(hi, lo, mare * 0.85f);
        c *= 0.9f + 0.2f * Hash(x, y, s.seed);                                   // grain
        c *= 0.92f + 0.16f * Fbm(u, v, 40, 2, s.seed + 8);                        // mottled regolith
        c *= 1f - 0.12f * mare * Fbm(u, v, 12, 3, s.seed + 9);

        // Craters: shaded bowls, bright ejecta blankets.
        float h = CraterHeight(u, v, out float bowl, out float ejecta);
        float shade = LightShade(CraterSlope(u, v, n));
        c *= shade;
        c = Color.Lerp(c, s.colorC * 1.2f * shade, Mathf.Clamp01(bowl * 1.1f) * 0.6f);
        c += Color.white * (Mathf.Clamp01(ejecta) * (1f - bowl) * 0.07f * (1f - mare * 0.6f));

        if (craters != null && craters.Length > 0)
        {
            CraterInfo big = craters[0];
            float dx = u - big.x, dy = v - big.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float t = dist / big.r;
            if (t < 1f)
            {
                // Terraces: stepped rings down the wall; a bright central peak.
                float ring = Frac(t * 3.2f);
                c *= 0.86f + 0.2f * Sstep(0f, 0.25f, ring) - 0.1f * Sstep(0.85f, 1f, ring);
                float peak = Sstep(0.2f, 0f, t);
                c = Color.Lerp(c, s.colorA * 1.5f * LightShade(-dx * 40f + dy * 55f), peak * 0.85f);
            }
            else
            {
                float ang = Mathf.Atan2(dy, dx);
                // Bright rays thrown out of the big crater.
                if (t > 1.15f)
                {
                    float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * 11f + Hash(Mathf.FloorToInt(ang * 3f), 3, s.seed) * 6f), 7f);
                    c += Color.white * (ray * 0.15f / (1f + (t - 1f) * 0.8f));
                }
                // Secondary craters: little pits strung along a few rays.
                if (t > 1.6f && t < 6f)
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Hash(i, 71, s.seed) * 6.2832f;
                        float lateral = dist * Mathf.Sin(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                        float along = dist * Mathf.Cos(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                        if (along < 0f || Mathf.Abs(lateral) > 0.03f) continue;
                        float gx = Frac(along / 0.045f) - 0.5f, gy = lateral / 0.045f;
                        float pd = Mathf.Sqrt(gx * gx + gy * gy);
                        float pr = 0.22f + 0.18f * Hash(Mathf.FloorToInt(along / 0.045f), i, s.seed + 72);
                        if (pd < pr) c *= 0.55f + 0.5f * (pd / pr);
                        else if (pd < pr * 1.25f) c += Color.white * 0.05f;
                    }
            }
        }

        // A rille: a thin winding valley through the dark seas.
        float rilleY = 0.3f + 0.06f * Mathf.Sin(u * 13f + s.seed) + 0.025f * Mathf.Sin(u * 31f);
        float rd = Mathf.Abs(v - rilleY) * W;
        if (mare > 0.4f && u > 0.12f && u < 0.9f) c *= 1f - 0.5f * (1f - Sstep(0.04f, 0.22f, rd)) * Sstep(0.4f, 0.6f, mare);

        // Boulders with long shadows.
        c = Boulders(c, s, wx, wy, 1.1f, 0.94f, s.seed + 100, s.colorB * 1.4f, 0.08f, 0.2f);

        // A rover's tracks: two dashed treads wandering across.
        float py = 0.66f + 0.09f * Mathf.Sin(u * 8f + 1f) + 0.035f * Mathf.Sin(u * 21f);
        float lat = (v - py) * W;
        if (u > 0.08f && u < 0.92f)
            for (int side = -1; side <= 1; side += 2)
            {
                float d = Mathf.Abs(lat - side * 0.45f);
                if (d < 0.07f)
                {
                    float tread = 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(wx * 38f));
                    c *= 0.74f + 0.14f * tread;
                }
            }
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Mars
    // ------------------------------------------------------------------

    private static Color ShadeMars(FloorStyle s, float u, float v, int x, int y, int n)
    {
        float W = Wu(s), wx = u * W, wy = v * W;
        int D = shadeDetail;

        // Dunes: ridges across the floor with a long gentle windward side and a short steep lee side, lit from the upper left.
        float phase = wy * 0.5f + Fbm(u, v, 5, 4, s.seed + 1) * 15f + Fbm(u, v, 11, 3, s.seed + 2) * 3.2f;
        float saw = Frac(phase);
        float slope = saw < 0.72f ? 0.35f : -1.1f;                                  // gentle up, sharp down
        float dune = 1f + 0.2f * slope;
        float ripple = 0.96f + 0.06f * Mathf.Sin(wx * 9f + Fbm(u, v, 20, 2, s.seed + 5) * 10f + phase * 3f);   // small wind ripples along the crest

        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 4 * D, 4, s.seed));
        float bright = Sstep(0.52f, 0.7f, Fbm(u, v, 3, 4, s.seed + 20));            // pale dust drifts
        c = Color.Lerp(c, s.colorA * 1.3f + new Color(0.12f, 0.08f, 0f, 0f), bright * 0.6f);
        float dark = Sstep(0.56f, 0.7f, Fbm(u, v, 3, 4, s.seed + 60));              // dark basalt sand streaks
        c = Color.Lerp(c, s.colorC * 2.4f + new Color(0.02f, 0.02f, 0.04f, 0f), dark * 0.75f);
        c *= dune * ripple;
        c *= 0.92f + 0.16f * Hash(x, y, s.seed);

        // Cracked dry lakebed in places.
        float bed = Sstep(0.58f, 0.7f, Fbm(u, v, 2, 3, s.seed + 70));
        if (bed > 0.01f)
        {
            int cc = Per(s, 0.8f);
            Voronoi(u, v, cc, s.seed + 71, out float f1, out float f2, out int id);
            float edge = (f2 - f1) * cc;
            Color plate = c * (0.9f + 0.18f * HashId(id, s.seed + 72)) * 1.05f;
            Color crack = c * 0.35f;
            Color bedCol = ScMix(plate, crack, 1f - Sstep(0.04f, 0.14f, edge));
            c = ScMix(c, bedCol, bed);
        }

        // Craters: eroded rims, floors filled with dust, a little frost in the shade.
        float h = CraterHeight(u, v, out float bowl, out float scorch);
        float shade = LightShade(CraterSlope(u, v, n) * 0.7f);
        c *= shade;
        c = Color.Lerp(c, s.colorA * 1.05f * shade * (0.9f + 0.1f * Mathf.Sin(wy * 6f)), Mathf.Clamp01(bowl * 1.2f) * 0.5f);
        c = Color.Lerp(c, s.colorC * 2f * shade, Mathf.Clamp01(scorch) * (1f - bowl) * 0.12f);
        if (bowl > 0.45f && shade < 0.97f && Hash(x, y, s.seed + 80) > 0.965f) c = ScMix(c, new Color(0.95f, 0.92f, 0.9f), 0.7f);

        // Boulders and dust devil trails.
        c = Boulders(c, s, wx, wy, 1.3f, 0.93f, s.seed + 100, s.colorB * 0.8f + new Color(0.05f, 0.03f, 0.02f, 0f), 0.08f, 0.24f);
        for (int i = 0; i < 3; i++)
        {
            float y0 = 0.2f + 0.3f * i + 0.1f * Hash(i, 91, s.seed);
            float trailY = y0 + 0.16f * Mathf.Sin(u * (4f + i * 1.7f) + i * 2f) + 0.02f * Mathf.Sin(u * 40f + i);
            float dd = Mathf.Abs(v - trailY) * W;
            float span = Sstep(0.05f + i * 0.1f, 0.2f + i * 0.1f, u) * Sstep(0.95f, 0.65f, u);
            c *= 1f - 0.28f * (1f - Sstep(0.05f, 0.2f, dd)) * span;
        }
        c *= new Color(1.04f, 0.97f, 0.93f, 1f);   // a rusty haze
        c.a = 1f;
        return c;
    }
}
