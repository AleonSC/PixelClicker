using UnityEngine;

/// <summary>
/// Small pixel-art icons for the title screen's buttons, drawn in code (32 x 32, point filtered, built once and cached): a floppy disk (Load), a cog (Settings)
/// and an open door (Quit).
/// </summary>
public static class PixelMenuIcons
{
    private const int N = 32;
    private static Texture2D floppy, cog, door;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { floppy = cog = door = null; }

    public static Texture2D Floppy => floppy != null ? floppy : floppy = Make(DrawFloppy);
    public static Texture2D Cog => cog != null ? cog : cog = Make(DrawCog);
    public static Texture2D Door => door != null ? door : door = Make(DrawDoor);

    private static Texture2D Make(System.Action<Color32[]> draw)
    {
        Color32[] px = new Color32[N * N];
        draw(px);
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Menu Icon" };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static void Set(Color32[] px, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < N && y < N) px[y * N + x] = c; }
    private static void Rect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(px, x, y, c); }

    private static void DrawFloppy(Color32[] px)
    {
        Color32 outline = new Color32(18, 36, 96, 255), body = new Color32(48, 100, 215, 255), light = new Color32(90, 145, 245, 255);
        Color32 metal = new Color32(205, 210, 222, 255), metalDark = new Color32(120, 126, 142, 255), slot = new Color32(50, 54, 68, 255);
        Color32 label = new Color32(243, 243, 236, 255), line = new Color32(150, 160, 195, 255);
        Rect(px, 3, 3, 28, 28, outline);
        Rect(px, 4, 4, 27, 27, body);
        Rect(px, 4, 27, 27, 27, light);
        Rect(px, 4, 4, 4, 27, light);
        // The notched top-right corner.
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int d = x + y;
                if (x >= 3 && y >= 3 && x <= 28 && y <= 28)
                {
                    if (d >= 53) Set(px, x, y, new Color32(0, 0, 0, 0));
                    else if (d >= 51) Set(px, x, y, outline);
                }
            }
        // The metal shutter with its slot.
        Rect(px, 9, 20, 22, 27, metal);
        Rect(px, 9, 20, 22, 20, metalDark);
        Rect(px, 17, 21, 20, 26, slot);
        // The label.
        Rect(px, 6, 4, 25, 15, label);
        Rect(px, 8, 12, 23, 12, line);
        Rect(px, 8, 9, 23, 9, line);
        Rect(px, 8, 6, 16, 6, line);
    }

    private static void DrawCog(Color32[] px)
    {
        Color32 outline = new Color32(70, 76, 94, 255), metal = new Color32(196, 202, 218, 255), light = new Color32(232, 236, 246, 255), shade = new Color32(140, 146, 166, 255);
        const float cx = 15.5f, cy = 15.5f;
        bool[] solid = new bool[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - cx, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                bool tooth = Mathf.Cos(a * 8f) > -0.1f;
                float edge = tooth ? 13f : 10.5f;
                bool hole = r <= 4.6f;
                solid[y * N + x] = r <= edge && !hole;
            }
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                if (!solid[y * N + x]) continue;
                bool border = false;
                for (int oy = -1; oy <= 1 && !border; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= N || ny >= N || !solid[ny * N + nx]) { border = true; break; }
                    }
                Color32 c = metal;
                if (border) c = outline;
                else if (x + y > 31) c = shade;          // shaded lower right
                else if (x + y < 24) c = light;          // lit upper left
                Set(px, x, y, c);
            }
        // The hub ring.
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = x - cx, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > 4.6f && r <= 5.8f) Set(px, x, y, outline);
            }
    }

    private static void DrawDoor(Color32[] px)
    {
        Color32 frame = new Color32(78, 48, 26, 255), frameLight = new Color32(120, 80, 44, 255);
        Color32 glowTop = new Color32(255, 232, 150, 255), glowBottom = new Color32(255, 190, 90, 255), spill = new Color32(255, 236, 170, 255);
        Color32 leaf = new Color32(155, 98, 52, 255), leafDark = new Color32(104, 64, 32, 255), knob = new Color32(255, 214, 80, 255);
        // The frame and the bright room beyond the doorway.
        Rect(px, 4, 2, 27, 30, frame);
        Rect(px, 4, 30, 27, 30, frameLight);
        for (int y = 3; y <= 28; y++) Rect(px, 6, y, 25, y, Color32.Lerp(glowBottom, glowTop, (y - 3) / 25f));
        // Light spilling onto the floor.
        Rect(px, 15, 3, 25, 4, spill);
        // The open door leaf, seen in perspective on the left (narrower towards the edge).
        for (int x = 6; x <= 16; x++)
        {
            float t = (x - 6) / 10f;
            int top = Mathf.RoundToInt(Mathf.Lerp(28f, 24f, t)), bottom = Mathf.RoundToInt(Mathf.Lerp(3f, 6f, t));
            Rect(px, x, bottom, x, top, x == 16 ? leafDark : leaf);
            Set(px, x, top, leafDark);
            Set(px, x, bottom, leafDark);
        }
        // Panels and the knob.
        for (int x = 8; x <= 14; x++)
        {
            float t = (x - 6) / 10f;
            int top = Mathf.RoundToInt(Mathf.Lerp(28f, 24f, t)) - 3, bottom = Mathf.RoundToInt(Mathf.Lerp(3f, 6f, t)) + 3;
            Set(px, x, top, leafDark);
            Set(px, x, bottom, leafDark);
            Set(px, x, (top + bottom) / 2, leafDark);
        }
        Rect(px, 14, 14, 15, 15, knob);
    }
}
