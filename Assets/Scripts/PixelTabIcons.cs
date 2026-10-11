using UnityEngine;

/// <summary>
/// Small pixel-art tab icons drawn in code (32 x 32, point filtered): <see cref="Pixels"/> (three cubes in a triangle), <see cref="Upgrades"/> (a machine with a green up
/// arrow), <see cref="Minigames"/> (a joystick and a Pac-Man), <see cref="Vendors"/> (a market stall with a person in it) and <see cref="Pets"/> (a cat and a dog).
/// Each is built once and cached.
/// </summary>
public static class PixelTabIcons
{
    private const int N = 32;
    private static Texture2D pixels, upgrades, minigames, vendors, pets;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { pixels = upgrades = minigames = vendors = pets = null; }

    public static Texture2D Pixels => pixels != null ? pixels : pixels = Make(DrawPixels);
    public static Texture2D Upgrades => upgrades != null ? upgrades : upgrades = Make(DrawUpgrades);
    public static Texture2D Minigames => minigames != null ? minigames : minigames = Make(DrawMinigames);
    public static Texture2D Vendors => vendors != null ? vendors : vendors = Make(DrawVendors);
    public static Texture2D Pets => pets != null ? pets : pets = Make(DrawPets);

    /// <summary>The icon for tab number 0..4 (Pixels, Upgrades, Minigames, Vendors, Pets).</summary>
    public static Texture2D Get(int index)
    {
        switch (index) { case 0: return Pixels; case 1: return Upgrades; case 2: return Minigames; case 3: return Vendors; default: return Pets; }
    }

    private static Texture2D Make(System.Action<Color32[]> draw)
    {
        Color32[] px = new Color32[N * N];
        draw(px);
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Tab Icon" };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // ---- drawing helpers (x to the right, y up) ----
    private static void Set(Color32[] px, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < N && y < N) px[y * N + x] = c; }
    private static void Rect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(px, x, y, c); }
    private static void Disc(Color32[] px, float cx, float cy, float r, Color32 c)
    {
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Set(px, x, y, c);
    }
    private static Color32 Shade(Color32 c, float k) { return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255); }

    private static void Cube(Color32[] px, int x, int y, int size, Color32 c)
    {
        Rect(px, x, y, x + size - 1, y + size - 1, c);
        Rect(px, x, y + size - 1, x + size - 1, y + size - 1, Shade(c, 1.3f));   // lit top
        Rect(px, x, y, x, y + size - 1, Shade(c, 1.2f));                          // lit left
        Rect(px, x, y, x + size - 1, y, Shade(c, 0.6f));                          // shaded bottom
        Rect(px, x + size - 1, y, x + size - 1, y + size - 2, Shade(c, 0.7f));    // shaded right
    }

    private static void DrawPixels(Color32[] px)
    {
        Cube(px, 11, 18, 10, new Color32(230, 80, 80, 255));
        Cube(px, 3, 4, 10, new Color32(80, 205, 100, 255));
        Cube(px, 19, 4, 10, new Color32(90, 140, 245, 255));
    }

    private static void DrawUpgrades(Color32[] px)
    {
        Color32 body = new Color32(135, 140, 158, 255), dark = new Color32(60, 64, 78, 255), light = new Color32(190, 196, 212, 255);
        Rect(px, 2, 5, 19, 17, dark);
        Rect(px, 3, 6, 18, 16, body);
        Rect(px, 3, 16, 18, 16, light);
        Rect(px, 5, 17, 8, 25, dark);                 // chimney
        Rect(px, 6, 17, 7, 25, body);
        Disc(px, 5.5f, 27.5f, 2.2f, new Color32(210, 214, 226, 255));   // smoke
        Disc(px, 9f, 11f, 3.4f, dark);                // dial
        Disc(px, 9f, 11f, 2.2f, light);
        Set(px, 9, 12, dark); Set(px, 10, 12, dark);
        Rect(px, 14, 8, 17, 9, new Color32(255, 190, 70, 255));         // a lamp / lever
        // The green up arrow.
        Color32 green = new Color32(70, 225, 100, 255), greenDark = new Color32(30, 140, 60, 255);
        for (int row = 0; row < 9; row++) Rect(px, 26 - (row < 8 ? 7 - row : 0) + (row == 8 ? 0 : 0), 20 + row, 26 + (row < 8 ? 7 - row : 0) - 1, 20 + row, green);
        Rect(px, 23, 6, 28, 20, green);
        Rect(px, 23, 6, 23, 20, greenDark);
        Rect(px, 28, 6, 28, 20, greenDark);
    }

    private static void DrawMinigames(Color32[] px)
    {
        // Joystick (left).
        Color32 dark = new Color32(55, 58, 70, 255), grey = new Color32(120, 125, 140, 255);
        Rect(px, 1, 3, 14, 8, dark);
        Rect(px, 2, 4, 13, 7, grey);
        Rect(px, 7, 9, 8, 19, new Color32(200, 204, 214, 255));
        Disc(px, 7.5f, 22f, 5f, new Color32(230, 70, 70, 255));
        Disc(px, 6f, 24f, 1.6f, new Color32(255, 170, 170, 255));
        // Pac-Man (right) with his mouth open to the right, and a pellet.
        float cx = 23f, cy = 13f, r = 8f;
        Color32 yellow = new Color32(255, 220, 40, 255);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > r * r) continue;
                if (dx > 0f && Mathf.Abs(dy) < dx * 0.75f) continue;     // the mouth
                Set(px, x, y, yellow);
            }
        Rect(px, 22, 18, 23, 19, new Color32(30, 30, 40, 255));            // eye
        Rect(px, 30, 12, 31, 13, new Color32(255, 255, 255, 255));         // pellet
    }

    private static void DrawVendors(Color32[] px)
    {
        Color32 red = new Color32(225, 70, 70, 255), white = new Color32(245, 245, 245, 255);
        for (int x = 1; x <= 30; x++) Rect(px, x, 23, x, 28, ((x - 1) / 4) % 2 == 0 ? red : white);   // striped awning
        Rect(px, 1, 22, 30, 22, new Color32(150, 40, 40, 255));
        Color32 wood = new Color32(140, 95, 55, 255), woodDark = new Color32(90, 60, 35, 255);
        Rect(px, 3, 9, 4, 22, woodDark);              // poles
        Rect(px, 27, 9, 28, 22, woodDark);
        // The person behind the counter.
        Rect(px, 11, 9, 20, 15, new Color32(70, 120, 220, 255));       // body
        Rect(px, 11, 9, 11, 15, new Color32(50, 90, 180, 255));
        Disc(px, 15.5f, 18.5f, 3.4f, new Color32(250, 205, 165, 255)); // head
        Rect(px, 12, 20, 19, 21, new Color32(60, 40, 30, 255));        // hair
        Set(px, 14, 18, new Color32(30, 30, 40, 255)); Set(px, 17, 18, new Color32(30, 30, 40, 255));
        // Counter.
        Rect(px, 2, 3, 29, 9, woodDark);
        Rect(px, 3, 4, 28, 8, wood);
        Rect(px, 2, 9, 29, 9, new Color32(190, 140, 85, 255));
    }

    private static void DrawPets(Color32[] px)
    {
        // Cat (left): orange head, pointed ears.
        Color32 orange = new Color32(240, 150, 60, 255), orangeDark = new Color32(190, 100, 30, 255);
        Disc(px, 8f, 12f, 6.5f, orange);
        for (int i = 0; i < 5; i++) { Rect(px, 2 + (i < 4 ? 0 : 1), 17 + i, 5 - i / 2 + 0, 17 + i, orange); Rect(px, 11 + i / 2, 17 + i, 14 - (i < 4 ? 0 : 1), 17 + i, orange); }
        Rect(px, 5, 15, 6, 15, orangeDark); Rect(px, 10, 15, 11, 15, orangeDark);
        Rect(px, 5, 13, 6, 14, new Color32(30, 30, 40, 255)); Rect(px, 10, 13, 11, 14, new Color32(30, 30, 40, 255));   // eyes
        Rect(px, 7, 10, 9, 10, new Color32(255, 130, 150, 255));                                                        // nose
        Rect(px, 0, 10, 3, 10, new Color32(240, 240, 240, 255)); Rect(px, 13, 10, 15, 10, new Color32(240, 240, 240, 255)); // whiskers
        // Dog (right): tan head, floppy brown ears, dark nose.
        Color32 tan = new Color32(225, 180, 120, 255), brown = new Color32(130, 80, 45, 255);
        Disc(px, 24f, 12f, 6.5f, tan);
        Rect(px, 16, 7, 18, 18, brown); Rect(px, 30, 7, 31, 18, brown);     // floppy ears
        Rect(px, 21, 13, 22, 14, new Color32(30, 30, 40, 255)); Rect(px, 26, 13, 27, 14, new Color32(30, 30, 40, 255));
        Rect(px, 22, 8, 25, 11, new Color32(245, 225, 190, 255));            // muzzle
        Rect(px, 23, 10, 24, 11, new Color32(40, 35, 40, 255));              // nose
        Rect(px, 23, 6, 24, 7, new Color32(255, 120, 140, 255));             // tongue
    }
}
