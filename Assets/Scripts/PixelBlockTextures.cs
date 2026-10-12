// ---- System notes (moved here from CLAUDE.md) ----
// Filler blocks / base block textures (PixelSkills.TryGetFiller, PixelClicker.FillerInfo, PixelBlockTextures.cs)
//   Each skill pack can have a 'useless' FILLER block (SkillPack.fillerType = a pixel type name; Farming = Dirt, Mining = Stone, the
//   old Rock block (enum PixelType.Rock kept for saves, now called Stone everywhere in the UI), which is now part of Mining's pixel
//   list, unlocked with the first ore, IsOptIn is false for everything now (Wood is part of Woodcutting), and comes with the
//   Prospector's Pack instead of its own pack (packDataVersion 13 migrates saved packs: drops the single-reward Rock pack, adds Stone
//   to Prospector's, sets it to 1 click / pay 1)). While the pack is in play the filler makes up fillerStartShare (0 = 60%) of all
//   spawns at level 1, fading evenly to fillerEndShare (0 = 10%) at fillerFadeLevel (0 = level 20): PickSpawnTier and SpawnShare (the
//   Pixel Guide's percentages) give it the weight others x share / (1 - share) instead of its own spawn weight. Farming / Mining skill
//   by level, so unlocked pixels come up more as you level. Wood, Dirt and Stone have crisp 64 px pixel-art surfaces
//   (PixelLook.blockSurface Wood / Dirt / Stone, PixelBlockTextures.Get, look colour white; looksVersion 40).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Crisp 64 px pixel-art surfaces for the base blocks of the skill packs: Wood (tree bark: tall ridges split by dark furrows), Dirt (clumpy earth with
/// pebbles) and Stone (a wall of chiselled blocks). Drawn once in code; they are white-balanced so the look's colour (white) shows them as drawn.
/// </summary>
public static class PixelBlockTextures
{
    private static Texture2D wood, dirt, stone, whale;

    public static Texture2D Get(PixelLook.BlockSurface surface)
    {
        switch (surface)
        {
            case PixelLook.BlockSurface.Wood: return wood != null ? wood : (wood = Make("Wood Surface", WoodPixel));
            case PixelLook.BlockSurface.Dirt: return dirt != null ? dirt : (dirt = Make("Dirt Surface", DirtPixel));
            case PixelLook.BlockSurface.Stone: return stone != null ? stone : (stone = Make("Stone Surface", StonePixel));
            case PixelLook.BlockSurface.Whale: return whale != null ? whale : (whale = Make("Whale Surface", WhalePixel));
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

    // ---- Wood: tree bark - tall, uneven ridges split by deep dark furrows, vertical fibres, flaking and a little moss ----
    private static Color WoodPixel(int x, int y)
    {
        // Ridge cells are long and thin (an anisotropic Voronoi) and the whole pattern sways sideways as it goes down, like real bark.
        float sway = Mathf.Sin(y * 0.11f + H(x / 20, 0, 71) * 6.28f) * 1.9f + (H(x / 9, y / 22, 72) - 0.5f) * 2f;
        float gx = (x + sway) / 10f, gy = y / 34f;
        int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
        float d1 = 99f, d2 = 99f;
        int id = 0;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = cx + ox, iy = cy + oy;
                float px = ix + 0.15f + H(ix, iy, 73) * 0.7f, py = iy + 0.1f + H(ix, iy, 74) * 0.8f;
                float dx = (gx - px) * 10f, dy = (gy - py) * 34f * 0.28f;   // a step along the ridge counts for little: the ridges run up and down
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < d1) { d2 = d1; d1 = d; id = ix * 29 + iy * 13; }
                else if (d < d2) d2 = d;
            }
        float edge = d2 - d1;                                   // ~0 in the furrow between two ridges, bigger on a ridge's top

        // Each ridge has its own grey-brown tone.
        float tone = H(id, 5, 75);
        Color c = Color.Lerp(new Color(0.24f, 0.17f, 0.11f), new Color(0.46f, 0.37f, 0.28f), tone);
        c = Color.Lerp(c, new Color(0.4f, 0.37f, 0.34f), 0.25f * H(id, 7, 76));   // a hint of grey, like old bark
        // Relief: dark in the furrows, brightest along the ridge, shaded on the right side.
        c *= Mathf.Lerp(0.5f, 1.18f, Mathf.Clamp01(edge / 5.5f));
        if (edge < 1.1f) c = new Color(0.06f, 0.04f, 0.03f);   // the deep furrow itself
        else if (edge < 2.2f) c *= 0.7f;
        // Long vertical fibres and flaking (short pale / dark dashes along the ridges).
        c *= 0.82f + H(x, y / 11, 77) * 0.3f;
        if (H(x / 2, y / 5, 79) > 0.9f && edge > 2f) c *= 0.62f;                       // a small crack across a ridge
        if (H(x, y / 3, 81) > 0.965f && edge > 2.5f) c = Color.Lerp(c, new Color(0.62f, 0.5f, 0.38f), 0.7f);   // a fleck of flaked, pale bark
        // Moss in the furrows on the shadier patches.
        float moss = H(x / 7, y / 9, 83) * 0.6f + H(x / 3, y / 4, 85) * 0.4f;
        if (moss > 0.68f && edge < 3f && H(x, y, 87) > 0.3f) c = Color.Lerp(c, new Color(0.3f, 0.42f, 0.18f) * (0.8f + H(x, y, 89) * 0.4f), 0.7f);
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

    // ---- Whale: a blue whale swimming left on every face - deep sea behind it, a pale ribbed belly, a fin, an eye, a smile, a tail and a spout ----
    private static float Seg(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax, aby = by - ay;
        float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / Mathf.Max(1e-6f, abx * abx + aby * aby));
        float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private static Color WhalePixel(int x, int y)
    {
        float u = x / (float)(N - 1), v = y / (float)(N - 1);
        // The sea: dark at the bottom, bright at the top, with soft bands of light.
        Color c = Color.Lerp(new Color(0.04f, 0.17f, 0.36f), new Color(0.2f, 0.52f, 0.8f), v);
        c *= 0.92f + 0.08f * Mathf.Sin(u * 11f + v * 7f) + (H(x, y, 211) - 0.5f) * 0.05f;

        // The body: a long ellipse, fatter at the head (left), tapering to the tail stalk (right).
        float bx = (u - 0.44f) / 0.37f, by = (v - 0.42f) / (0.2f * (1.15f - 0.35f * Mathf.Clamp01((u - 0.2f) / 0.6f)));
        float body = bx * bx + by * by;
        // The tail stalk rises to the right and ends in two flukes.
        float stalk = Seg(u, v, 0.74f, 0.45f, 0.88f, 0.62f);
        float fluke = Mathf.Min(Seg(u, v, 0.88f, 0.62f, 0.97f, 0.76f), Seg(u, v, 0.88f, 0.62f, 0.78f, 0.78f));
        bool inBody = body < 1f || stalk < 0.065f - 0.035f * Mathf.Clamp01((u - 0.74f) / 0.14f) || fluke < 0.045f;
        if (inBody)
        {
            Color skin = Color.Lerp(new Color(0.18f, 0.32f, 0.5f), new Color(0.35f, 0.52f, 0.72f), v);
            // The belly: a pale underside with ribs.
            float bellyLine = 0.38f - 0.03f * Mathf.Sin(u * 9f);
            if (v < bellyLine && body < 1f)
            {
                skin = Color.Lerp(new Color(0.78f, 0.84f, 0.9f), new Color(0.9f, 0.93f, 0.95f), v * 2f);
                if (Mathf.Repeat(u * 22f, 1f) < 0.18f && v > 0.22f) skin *= 0.82f;   // the throat pleats
            }
            skin *= 0.94f + (H(x, y, 213) - 0.5f) * 0.1f;
            if (body > 0.82f && body < 1f) skin *= 0.7f;                               // a dark outline
            c = skin;
        }
        // Pectoral fin.
        if (Seg(u, v, 0.4f, 0.34f, 0.5f, 0.2f) < 0.04f && v < 0.38f) c = new Color(0.14f, 0.26f, 0.44f);
        // The eye, with a bright glint, and a smiling mouth line.
        float ex = u - 0.2f, ey = v - 0.47f;
        float er = Mathf.Sqrt(ex * ex + ey * ey);
        if (er < 0.032f) c = new Color(0.04f, 0.05f, 0.08f);
        if (Mathf.Sqrt((ex + 0.008f) * (ex + 0.008f) + (ey - 0.01f) * (ey - 0.01f)) < 0.01f) c = Color.white;
        float mouth = Mathf.Abs(v - (0.37f + 0.05f * Mathf.Pow(Mathf.Clamp01((u - 0.1f) / 0.22f), 2f)));
        if (u > 0.1f && u < 0.34f && mouth < 0.012f && body < 1f) c = new Color(0.08f, 0.14f, 0.24f);
        // The spout: a fountain of white drops from the blowhole.
        float sx = u - 0.3f;
        if (v > 0.6f && v < 0.95f && Mathf.Abs(sx) < 0.012f + (v - 0.6f) * 0.12f && H(x, y, 215) > 0.42f) c = Color.Lerp(c, Color.white, 0.9f);
        if (v > 0.62f && v < 0.9f && H(x, y, 216) > 0.965f) c = Color.white;
        return c;
    }

    // ---- Message in a bottle: a green glass bottle with a cork and a rolled-up message inside, floating in pale water; cracks spread as it is hit ----
    private static readonly System.Collections.Generic.Dictionary<int, Texture2D> bottles = new System.Collections.Generic.Dictionary<int, Texture2D>();

    public static Texture2D BottleGlass(int level, int maxLevel)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int key = level * 100 + maxLevel;
        if (bottles.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        float k = level / (float)maxLevel;
        int cracks = Mathf.RoundToInt(k * 8f);
        Texture2D tex = Make("Bottle " + level, (x, y) =>
        {
            float u = x / (float)(N - 1), v = y / (float)(N - 1);
            Color c = Color.Lerp(new Color(0.34f, 0.66f, 0.7f), new Color(0.62f, 0.88f, 0.86f), v);   // pale water behind
            c *= 0.95f + (H(x, y, 221) - 0.5f) * 0.06f;
            if (H(x / 3, y / 3, 222) > 0.96f) c = Color.Lerp(c, Color.white, 0.6f);                    // a few bubbles

            // The bottle's outline: a rounded body, a neck, a lip and a cork.
            float cx = Mathf.Abs(u - 0.5f);
            bool body = cx < 0.22f && v > 0.07f && v < 0.56f && !(cx > 0.1f && (v < 0.17f || v > 0.46f) && (cx - 0.1f) * (cx - 0.1f) + Mathf.Pow(Mathf.Max(0f, (v < 0.17f ? 0.17f - v : v - 0.46f)), 2f) > 0.012f);
            float neckHalf = 0.075f + 0.14f * Mathf.Clamp01((0.62f - v) / 0.1f);
            bool neck = v >= 0.54f && v < 0.8f && cx < neckHalf;
            bool lip = v >= 0.78f && v < 0.85f && cx < 0.11f;
            bool cork = v >= 0.85f && v < 0.95f && cx < 0.085f;
            if (cork)
            {
                c = Color.Lerp(new Color(0.62f, 0.42f, 0.22f), new Color(0.46f, 0.3f, 0.14f), H(x, y, 223));
                if (v > 0.93f) c *= 0.8f;
            }
            else if (body || neck || lip)
            {
                Color glass = Color.Lerp(new Color(0.12f, 0.42f, 0.3f), new Color(0.3f, 0.7f, 0.52f), 0.5f + 0.5f * Mathf.Sin(u * 9f));
                c = Color.Lerp(c, glass, 0.78f);
                if (u > 0.3f && u < 0.35f && v > 0.14f && v < 0.5f) c = Color.Lerp(c, Color.white, 0.55f);   // a glossy streak
                if (cx > 0.2f || (v > 0.052f && v < 0.085f)) c *= 0.7f;                                      // glass edge
                // The rolled-up message with a red ribbon.
                if (cx < 0.135f && v > 0.17f && v < 0.44f)
                {
                    float shade = 0.82f + 0.18f * Mathf.Cos((u - 0.5f) * 14f);
                    c = new Color(0.95f, 0.86f, 0.62f) * shade;
                    if (Mathf.Abs(v - 0.305f) < 0.022f) c = new Color(0.78f, 0.12f, 0.1f);
                    if (H(x, y, 224) > 0.9f) c *= 0.85f;
                }
            }

            // Cracks: dark lines with a pale edge, more of them the more it is hit.
            for (int i = 0; i < cracks; i++)
            {
                float a = H(i, 1, 225) * 6.28f;
                float len = 0.22f + H(i, 2, 226) * 0.3f;
                float ox = 0.5f + (H(i, 3, 227) - 0.5f) * 0.3f, oy = 0.34f + (H(i, 4, 228) - 0.5f) * 0.25f;
                float dd = Seg(u, v, ox, oy, ox + Mathf.Cos(a) * len, oy + Mathf.Sin(a) * len);
                if (dd < 0.011f) c = new Color(0.05f, 0.1f, 0.1f);
                else if (dd < 0.02f) c = Color.Lerp(c, Color.white, 0.4f);
            }
            return c;
        });
        bottles[key] = tex;
        return tex;
    }
}
