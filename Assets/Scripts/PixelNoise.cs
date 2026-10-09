using UnityEngine;

/// <summary>
/// Small noise toolkit for textures drawn in code (floor styles, skyboxes). Everything tiles seamlessly: u / v are
/// 0..1 texture coordinates and wrap around.
/// </summary>
public static class PixelNoise
{
    public static float Frac(float f) => f - Mathf.Floor(f);

    public static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    public static float HashId(int id, int seed) => Hash(id, id * 7 + 3, seed);

    /// <summary>Value noise on a lattice of 'period' cells that wraps around (tileable).</summary>
    public static float TileNoise(float u, float v, int period, int seed)
    {
        float x = Frac(u) * period, y = Frac(v) * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
        x0 %= period; y0 %= period;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
        float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>Layered tileable noise, 0..1.</summary>
    public static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int period = Mathf.Max(1, basePeriod);
        for (int i = 0; i < octaves; i++)
        {
            sum += TileNoise(u, v, period, seed + i * 101) * amp;
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }
        return sum / norm;
    }

    /// <summary>Tileable cell noise: distance to the nearest (f1) and second-nearest (f2) cell point, in texture units.</summary>
    public static void Voronoi(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
    {
        float x = Frac(u) * cells, y = Frac(v) * cells;
        int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
        f1 = f2 = 99f;
        id = 0;
        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox, gy = cy + oy;
                int wx = ((gx % cells) + cells) % cells, wy = ((gy % cells) + cells) % cells;
                float px = gx + 0.15f + 0.7f * Hash(wx, wy, seed);
                float py = gy + 0.15f + 0.7f * Hash(wx, wy, seed + 77);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = wy * cells + wx; }
                else if (d < f2) f2 = d;
            }
        }
        f1 /= cells;
        f2 /= cells;
    }

    /// <summary>Value noise with different lattice periods across and down (for 2:1 panoramas). Wraps in both.</summary>
    public static float TileNoise(float u, float v, int periodU, int periodV, int seed)
    {
        float x = Frac(u) * periodU, y = Frac(v) * periodV;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        int x1 = (x0 + 1) % periodU, y1 = (y0 + 1) % periodV;
        x0 %= periodU; y0 %= periodV;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx);
        float b = Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>Layered noise with separate periods across and down, 0..1.</summary>
    public static float Fbm(float u, float v, int periodU, int periodV, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int pu = Mathf.Max(1, periodU), pv = Mathf.Max(1, periodV);
        for (int i = 0; i < octaves; i++)
        {
            sum += TileNoise(u, v, pu, pv, seed + i * 101) * amp;
            norm += amp;
            amp *= 0.5f;
            pu *= 2;
            pv *= 2;
        }
        return sum / norm;
    }
}
