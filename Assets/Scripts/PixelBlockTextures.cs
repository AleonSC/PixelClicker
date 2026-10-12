using UnityEngine;

/// <summary>
/// Crisp 64 px pixel-art surfaces for the base blocks of the skill packs: Wood (planks with grain, a knot and nails), Dirt (clumpy earth with
/// pebbles) and Stone (a wall of chiselled blocks). Drawn once in code; they are white-balanced so the look's colour (white) shows them as drawn.
/// </summary>
public static class PixelBlockTextures
{
    private static Texture2D wood, dirt, stone;

    public static Texture2D Get(PixelLook.BlockSurface surface)
    {
        switch (surface)
        {
            case PixelLook.BlockSurface.Wood: return wood != null ? wood : (wood = Make("Wood Surface", WoodPixel));
            case PixelLook.BlockSurface.Dirt: return dirt != null ? dirt : (dirt = Make("Dirt Surface", DirtPixel));
            case PixelLook.BlockSurface.Stone: return stone != null ? stone : (stone = Make("Stone Surface", StonePixel));
        }
        return null;
    }

    private const int N = 64;

    private static Texture2D Make(string name, System.Func<int, int, Color> pixel)
    {
        Color32[] px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color c = pixel(x, y);
                px[y * N + x] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), 255);
            }
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = name };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }

    private static float H(int x, int y, int seed) => PixelNoise.Hash(x, y, seed);

    // ---- Wood: rough bark with vertical ridges, deep splits, pale flecks and patches of moss ----
    private static Color WoodPixel(int x, int y)
    {
        // Ridges wander sideways as they go down; each has its own tone.
        float wob = Mathf.Sin(y * 0.09f + x * 0.03f) * 2.2f + (H(x / 12, y / 16, 71) - 0.5f) * 3f;
        float fx = (x + wob) / 5.5f;
        int ridge = Mathf.FloorToInt(fx);
        float across = fx - ridge;                          // 0..1 across the ridge
        float tone = 0.75f + H(ridge, 2, 73) * 0.4f;
        Color bark = new Color(0.4f, 0.27f, 0.16f);
        Color c = bark * tone;
        // Relief: lit on the left of each ridge, dark in the groove between two.
        c *= across < 0.2f ? 0.55f : across < 0.45f ? 1.18f : across > 0.85f ? 0.7f : 1f;
        // Splits across the ridges and darker patches along them.
        int seg = y / 11;
        if (H(ridge, seg, 75) > 0.82f && y % 11 < 2) c *= 0.5f;
        c *= 0.88f + H(x, y / 2, 77) * 0.2f;
        // Pale flecks where the bark has flaked, and moss on the shadier side.
        if (H(x, y, 79) > 0.972f) c = Color.Lerp(c, new Color(0.72f, 0.58f, 0.4f), 0.8f);
        float moss = H(x / 6, y / 6, 81) * 0.55f + H(x / 3, y / 3, 83) * 0.45f;
        if (moss > 0.66f && H(x, y, 85) > 0.35f) c = Color.Lerp(c, new Color(0.34f, 0.46f, 0.2f) * (0.8f + H(x, y, 87) * 0.4f), 0.75f);
        return c;
    }

    // ---- Dirt: earthy clumps, dark specks and a few pebbles ----
    private static Color DirtPixel(int x, int y)
    {
        Color dark = new Color(0.3f, 0.19f, 0.1f), mid = new Color(0.46f, 0.3f, 0.17f), light = new Color(0.58f, 0.4f, 0.24f);
        float clump = H(x / 8, y / 8, 3) * 0.5f + H(x / 4, y / 4, 8) * 0.35f + H(x / 2, y / 2, 13) * 0.25f;   // blocky, layered noise
        Color c = clump < 0.42f ? Color.Lerp(dark, mid, clump / 0.42f) : Color.Lerp(mid, light, (clump - 0.42f) / 0.58f);
        // Dark flecks and lighter grit.
        float r = H(x, y, 21);
        if (r > 0.965f) c = dark * 0.7f;
        else if (r < 0.03f) c = light * 1.1f;
        // Pebbles: a lighter 3x3 lump with a shadow on its lower right.
        int px = x / 8, py = y / 8;
        if (H(px, py, 31) > 0.62f)
        {
            int ox = px * 8 + 1 + Mathf.FloorToInt(H(px, py, 32) * 3f), oy = py * 8 + 1 + Mathf.FloorToInt(H(px, py, 33) * 3f);
            if (x >= ox && x < ox + 3 && y >= oy && y < oy + 3) c = Color.Lerp(new Color(0.62f, 0.58f, 0.5f), new Color(0.78f, 0.74f, 0.66f), H(x, y, 34));
            else if (x == ox + 3 && y >= oy - 1 && y < oy + 3 || y == oy - 1 && x >= ox + 1 && x <= ox + 3) c *= 0.62f;
        }
        return c;
    }

    // ---- Stone: natural rock - irregular fracture planes (a jittered Voronoi), mineral flecks and lichen ----
    private static Color StonePixel(int x, int y)
    {
        const float cell = 21f;
        float gx = x / cell, gy = y / cell;
        int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
        float d1 = 9f, d2 = 9f;
        int id = 0;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = cx + ox, iy = cy + oy;
                float px = ix + 0.15f + H(ix, iy, 91) * 0.7f, py = iy + 0.15f + H(ix, iy, 93) * 0.7f;
                float d = Mathf.Sqrt((gx - px) * (gx - px) + (gy - py) * (gy - py));
                if (d < d1) { d2 = d1; d1 = d; id = ix * 31 + iy * 17; }
                else if (d < d2) d2 = d;
            }
        float tone = 0.68f + H(id, 5, 95) * 0.4f;                                    // each fracture plane its own grey
        Color baseGrey = Color.Lerp(new Color(0.5f, 0.5f, 0.52f), new Color(0.58f, 0.55f, 0.5f), H(id, 9, 97));
        Color c = baseGrey * tone;
        c *= 0.9f + H(x / 2, y / 2, 99) * 0.2f + (H(x / 8, y / 8, 101) - 0.5f) * 0.18f;   // weathered mottling
        float edge = (d2 - d1) * cell;                                                    // distance to the nearest fracture, in px
        if (edge < 1.2f) c *= 0.38f;                                                      // the crack itself
        else if (edge < 2.6f) c *= 0.82f;                                                 // darker where the planes meet
        // Mineral flecks and pits.
        float r = H(x, y, 103);
        if (r > 0.975f) c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.86f), 0.8f);
        else if (r < 0.025f) c *= 0.55f;
        // Lichen: yellow-green speckle in patches.
        float patch = H(x / 7, y / 7, 105) * 0.6f + H(x / 3, y / 3, 107) * 0.4f;
        if (patch > 0.7f && H(x, y, 109) > 0.45f) c = Color.Lerp(c, new Color(0.62f, 0.66f, 0.3f), 0.6f);
        return c;
    }

    // ---- Seed pod: a glossy, ridged husk with dimples; cracks spread as it is dug open and a green light leaks out of them ----
    private static readonly System.Collections.Generic.Dictionary<int, Texture2D> seeds = new System.Collections.Generic.Dictionary<int, Texture2D>();

    public static Texture2D SeedShell(int level, int maxLevel)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int key = level * 100 + maxLevel;
        if (seeds.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        float k = level / (float)maxLevel;                       // 0 whole .. 1 about to crack open
        // Crack branches: wandering polylines out of the middle, built once.
        const int branches = 6, steps = 12;
        Vector2[][] pts = new Vector2[branches][];
        for (int b = 0; b < branches; b++)
        {
            pts[b] = new Vector2[steps + 1];
            Vector2 p = new Vector2(0.5f, 0.5f);
            float angle = b * 6.2832f / branches + (H(b, 1, 111) - 0.5f) * 0.8f;
            pts[b][0] = p;
            for (int s = 1; s <= steps; s++)
            {
                angle += (H(b, s, 113) - 0.5f) * 0.9f;
                p += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.055f;
                pts[b][s] = p;
            }
        }
        int reach = Mathf.RoundToInt(k * steps);                  // how many segments of each branch exist so far

        Texture2D tex = Make("Seed Shell " + level, (x, y) =>
        {
            // The husk: long vertical ridges with a lit edge and a dark groove, a pale seam round the middle and little dimples.
            float u = x / (float)N, v = y / (float)N;
            float ridge = Mathf.Abs(Mathf.Sin(u * 6.2832f * 6f + Mathf.Sin(v * 6.2832f) * 0.8f));
            Color c = Color.Lerp(new Color(0.26f, 0.15f, 0.07f), new Color(0.58f, 0.36f, 0.16f), Mathf.Pow(ridge, 0.6f));
            c *= 0.9f + H(x / 2, y / 2, 121) * 0.2f;
            if (ridge > 0.9f) c = Color.Lerp(c, new Color(0.85f, 0.62f, 0.34f), 0.45f);         // a glossy highlight along each ridge
            if (Mathf.Abs(v - 0.5f) < 0.018f) c = new Color(0.78f, 0.66f, 0.46f);                // the seam
            if (H(x / 4, y / 4, 123) > 0.93f) c *= 0.7f;                                         // dimples
            // Cracks with a green glow that grows as they open.
            if (level > 0)
            {
                float best = 9f;
                for (int b = 0; b < branches; b++)
                    for (int s = 0; s < reach && s < steps; s++)
                    {
                        Vector2 a = pts[b][s], d = pts[b][s + 1] - a, q = new Vector2(u, v) - a;
                        float t = Mathf.Clamp01(Vector2.Dot(q, d) / d.sqrMagnitude);
                        best = Mathf.Min(best, (q - d * t).magnitude);
                    }
                float w = 0.011f + 0.007f * k;
                if (best < w) c = new Color(0.05f, 0.07f, 0.02f) + new Color(0.4f, 0.85f, 0.2f) * (0.25f + k * 0.55f);
                else if (best < w + 0.035f) c = Color.Lerp(c, new Color(0.45f, 0.95f, 0.3f), (1f - (best - w) / 0.035f) * (0.25f + k * 0.45f));
            }
            return c;
        });
        seeds[key] = tex;
        return tex;
    }

    // ---- Pest: a black carapace in plates, hazard stripes round every face, and red eyes that multiply (and cracks that glow) as it is hit ----
    private static readonly System.Collections.Generic.Dictionary<int, Texture2D> pests = new System.Collections.Generic.Dictionary<int, Texture2D>();

    public static Texture2D PestHide(int level, int maxLevel)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int key = level * 100 + maxLevel;
        if (pests.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        float k = level / (float)maxLevel;
        // Eye positions on a face (the same on all six): 3 to start, up to 7 as it gets angrier.
        Vector2[] eyes = { new Vector2(0.3f, 0.58f), new Vector2(0.7f, 0.58f), new Vector2(0.5f, 0.38f), new Vector2(0.25f, 0.32f), new Vector2(0.75f, 0.32f), new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.2f) };
        int visible = 3 + Mathf.RoundToInt(k * 4f);

        Texture2D tex = Make("Pest Hide " + level, (x, y) =>
        {
            float u = x / (float)N, v = y / (float)N;
            // Carapace plates: dark cells with lighter rims.
            const float cell = 11f;
            float gx = x / cell, gy = y / cell;
            int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
            float d1 = 9f, d2 = 9f;
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int ix = cx + ox, iy = cy + oy;
                    float px = ix + 0.2f + H(ix, iy, 141) * 0.6f, py = iy + 0.2f + H(ix, iy, 143) * 0.6f;
                    float d = Mathf.Sqrt((gx - px) * (gx - px) + (gy - py) * (gy - py));
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
            float rim = (d2 - d1) * cell;
            Color c = Color.Lerp(new Color(0.05f, 0.06f, 0.04f), new Color(0.16f, 0.18f, 0.1f), Mathf.Clamp01(d1 * 0.9f));
            if (rim < 1.2f) c = new Color(0.3f, 0.34f, 0.12f);                                   // a lit seam between the plates
            c *= 0.9f + H(x, y, 145) * 0.2f;

            // Warning stripes along all four borders.
            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            if (edge < 0.15f)
            {
                bool yellow = Mathf.FloorToInt((x + y) / 6f) % 2 == 0;
                c = yellow ? new Color(0.95f, 0.78f, 0.08f) : new Color(0.05f, 0.04f, 0.03f);
                if (edge < 0.025f) c = new Color(0.04f, 0.03f, 0.02f);                         // a dark outer line
            }

            // Red eyes: a glowing disc with a dark slit; more of them (and a hotter glow) the angrier it is.
            for (int e = 0; e < visible; e++)
            {
                float dx = (u - eyes[e].x) * 1.0f, dy = (v - eyes[e].y) * 1.0f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float radius = 0.07f + 0.012f * k;
                if (r < radius)
                {
                    c = Color.Lerp(new Color(1f, 0.15f, 0.05f), new Color(1f, 0.7f, 0.2f), (1f - r / radius) * (0.4f + k * 0.5f));
                    if (Mathf.Abs(dx) < 0.012f && Mathf.Abs(dy) < radius * 0.8f) c = new Color(0.05f, 0f, 0f);   // slit pupil
                }
                else if (r < radius + 0.03f) c = Color.Lerp(c, new Color(1f, 0.2f, 0.05f), (1f - (r - radius) / 0.03f) * (0.35f + k * 0.4f));
            }
            return c;
        });
        pests[key] = tex;
        return tex;
    }
}
