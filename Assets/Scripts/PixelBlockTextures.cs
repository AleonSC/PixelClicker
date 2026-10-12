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

    // ---- Wood: four planks across, each with its own tone, long grain streaks, a knot and a nail at each end ----
    private static Color WoodPixel(int x, int y)
    {
        const int plankH = 16;
        int plank = y / plankH, py = y % plankH;
        float tone = 0.82f + H(plank, 3, 41) * 0.3f;
        Color baseA = new Color(0.62f, 0.42f, 0.22f), baseB = new Color(0.46f, 0.29f, 0.14f);
        // Grain: streaks that run along the plank and wander a little.
        float wander = Mathf.Sin(x * 0.11f + plank * 2.3f) * 1.2f;
        float streak = H(Mathf.FloorToInt(x / 10f), Mathf.FloorToInt(py + wander), 17 + plank);
        Color c = Color.Lerp(baseB, baseA, 0.35f + streak * 0.5f) * tone;
        if (H(x / 3, py, 5 + plank) > 0.86f) c *= 0.82f;                       // a few dark fibres
        // A knot with growth rings.
        int kx = 14 + plank * 13 % 36, ky = plankH / 2;
        float kd = Mathf.Sqrt((x - kx - plank * 9) * (x - kx - plank * 9) * 0.35f + (py - ky) * (py - ky));
        if (plank % 2 == 0 && kd < 4.6f) c = Color.Lerp(baseB * 0.55f, baseA * 0.8f, Mathf.Abs(Mathf.Sin(kd * 1.7f)));
        // Plank edges: a dark seam above, a light lip below it.
        if (py == 0) c = baseB * 0.38f;
        else if (py == 1) c *= 1.18f;
        else if (py == plankH - 1) c *= 0.8f;
        // Nails near both ends.
        if ((x == 4 || x == 59) && py == 7) c = new Color(0.16f, 0.14f, 0.13f);
        if ((x == 3 || x == 58) && py == 6) c = new Color(0.78f, 0.7f, 0.58f);
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

    // ---- Stone: rows of chiselled blocks in three greys, mortar lines, lit top-left edges and chips ----
    private static Color StonePixel(int x, int y)
    {
        const int rowH = 16;
        int row = y / rowH, py = y % rowH;
        int shifted = x + (row % 2) * 16;
        int col = shifted / 32, px = shifted % 32;
        float tone = 0.72f + H(col, row, 51) * 0.3f;
        Color baseGrey = new Color(0.56f, 0.57f, 0.6f);
        Color c = baseGrey * tone;
        // Speckle and darker veins.
        float r = H(x, y, 7);
        if (r > 0.93f) c *= 1.15f;
        else if (r < 0.07f) c *= 0.8f;
        c *= 0.94f + H(x / 3, y / 3, 9) * 0.12f;
        // A chip knocked out of some blocks.
        if (H(col, row, 61) > 0.7f && px > 18 && px < 27 && py > 3 && py < 8 && (px + py) % 5 != 0) c *= 0.72f;
        // Mortar around every block, a bright bevel on the top / left edge and a dark one on the bottom / right.
        if (py == 0 || px == 0) c = new Color(0.2f, 0.2f, 0.22f);
        else if (py == 1 || px == 1) c *= 1.28f;
        else if (py == rowH - 1 || px == 31) c *= 0.7f;
        return c;
    }
}
