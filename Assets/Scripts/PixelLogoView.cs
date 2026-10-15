// ---- System notes ----
// PixelLogoView.cs
//   The title screen's 'PIXEL CLICKER' logo as a grid of little bevelled cells (one UI image per lit pixel of the 5x7 letters, plus a shadow image and a glow image each), so every
//   cell can move, scale, spin and change colour every frame. PixelTitleScreen.BuildLogo adds this component to the 'Logo' object and calls Init.
//   * STYLES: a table (Styles) of 20 looks, each a small static function (Cell + time -> offset / scale / rotation / colour / glow, 'S') plus an INTRO kind that plays every time the style
//     is shown (Drop, Fly, Zoom, Wipe, Spiral, Stack, Flip): Classic, Rainbow Wave, Neon, Shatter, Matrix, Inferno, Frost, Glitch, Jelly, Flip, Blocks, Plasma, Synthwave, Gold, Ghost,
//     Storm, Shockwave, Disco, Ocean, Hologram. To add one: write a function, add a line to Styles. A style may also scale a whole LETTER (S.lsx / lsy, about its base / centre).
//   * FAVORITE: a small star under each arrow (fades in with it) makes the style showing now the favourite of the save (PlayerPrefs PixelClicker.Setting.LogoFavorite = index + 1, in PixelSettingsSync, so it is per save);
//     that style is shown instead of a random one, and ReloadFavorite switches to it when a save loads. Click the star again to clear it.
//   * ARROWS: two invisible buttons at the far left / right of the logo fade in while the mouse is near them and step to the previous / next style (wraps); the style's name shows
//     under the logo for a moment. A random style is picked at every launch (never the same as last time; PlayerPrefs PixelClicker.Setting.LogoStyle remembers the last one only for that).
//   * PixelTitleScreen only positions / bobs / fades (SetAlpha) the container. Runs on unscaled time (the game is frozen while the title shows).
//   Cost: ~200 cells x 3 images, updated each frame only while the title screen is up.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelLogoView : MonoBehaviour
{
    private const string Pref = "PixelClicker.Setting.LogoStyle";
    /// <summary>The favourite logo style + 1 (0 = none); saved per save file (PixelSettingsSync).</summary>
    public const string FavoritePref = "PixelClicker.Setting.LogoFavorite";
    private static PixelLogoView current;
    private static Sprite starFilled, starHollow;
    private Image[] starImages;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStarStatics() { current = null; starFilled = starHollow = null; }

    /// <summary>A save was loaded: show its favourite logo (if it has one).</summary>
    public static void ReloadFavorite()
    {
        if (current == null) return;
        int fav = PixelPrefs.GetInt(FavoritePref, 0) - 1;
        if (fav >= 0 && fav < Styles.Length && fav != current.style) current.SetStyle(fav);
        else current.RefreshStars();
    }

    private static Sprite StarSprite(bool filled)
    {
        if (filled ? starFilled != null : starHollow != null) return filled ? starFilled : starHollow;
        const int n = 64;
        Vector2[] poly = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 1f : 0.42f;
            poly[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f) * 1.05f;
                bool inside = InStar(poly, p), core = InStar(poly, p / 0.72f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside && (filled || !core) ? 1f : 0f));
            }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        if (filled) starFilled = s; else starHollow = s;
        return s;
    }

    private static bool InStar(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) inside = !inside;
        return inside;
    }

    private bool IsFavorite => PixelPrefs.GetInt(FavoritePref, 0) - 1 == style;

    private void RefreshStars()
    {
        if (starImages == null) return;
        bool fav = IsFavorite;
        foreach (Image im in starImages)
        {
            if (im == null) continue;
            im.sprite = StarSprite(fav);
            im.color = fav ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.8f);
        }
    }

    private void ToggleFavorite()
    {
        if (group != null && group.alpha < 0.9f) return;
        bool wasFav = IsFavorite;
        PixelPrefs.SetInt(FavoritePref, wasFav ? 0 : style + 1);
        RefreshStars();
        PixelAudio.Play("ui_click");
        nameT = 0f;
        if (nameLabel != null) nameLabel.text = wasFav ? "FAVORITE CLEARED" : "FAVORITE LOGO SAVED";
    }

    /// <summary>A small star under an arrow (it fades in and out with the arrow): click to make the logo showing now the favourite of this save.</summary>
    private Image MakeStar(RectTransform arrow)
    {
        GameObject go = new GameObject("Favorite Star", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(arrow, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(44f, 44f);
        r.anchoredPosition = new Vector2(0f, -8f);
        Image im = go.GetComponent<Image>();
        go.GetComponent<Button>().targetGraphic = im;
        go.GetComponent<Button>().onClick.AddListener(ToggleFavorite);
        return im;
    }
    private const int GridRows = 15, TotalCols = 41;
    private const float BodyBase = 0.88f;   // brightness of the bevel sprite's middle (tints are divided by it)

    private sealed class Cell
    {
        public float ux, uy, nx, ny, dist, angle, rnd, rnd2, lcx, wbY;   // position in cell units from the logo centre (y up), normalised -1..1, random seeds, letter centre x, word base y
        public int letter, word, rowG, colG, index;
        public Color baseColor;
        public RectTransform body, shadow, glow;
        public Image bodyImage, shadowImage, glowImage;
    }

    private struct S
    {
        public Vector2 off;          // offset in cell units
        public float sx, sy, rot;    // scale, rotation (degrees)
        public float lsx, lsy;       // scale of the whole letter (about its centre / base)
        public float alpha, glow, shadow;
        public Color col, glowCol;
    }

    private delegate void StyleFn(Cell c, float t, ref S s);
    private struct StyleDef { public string name; public StyleFn fn; public int intro; }

    private const int IntroNone = 0, IntroDrop = 1, IntroFly = 2, IntroZoom = 3, IntroWipe = 4, IntroSpiral = 5, IntroStack = 6, IntroFlip = 7;

    private static readonly StyleDef[] Styles =
    {
        new StyleDef { name = "Classic", fn = Classic, intro = IntroDrop },
        new StyleDef { name = "Rainbow Wave", fn = RainbowWave, intro = IntroZoom },
        new StyleDef { name = "Neon", fn = Neon, intro = IntroWipe },
        new StyleDef { name = "Shatter", fn = Shatter, intro = IntroFly },
        new StyleDef { name = "Matrix", fn = Matrix, intro = IntroDrop },
        new StyleDef { name = "Inferno", fn = Inferno, intro = IntroZoom },
        new StyleDef { name = "Frost", fn = Frost, intro = IntroWipe },
        new StyleDef { name = "Glitch", fn = Glitch, intro = IntroFlip },
        new StyleDef { name = "Jelly", fn = Jelly, intro = IntroStack },
        new StyleDef { name = "Flip", fn = FlipStyle, intro = IntroZoom },
        new StyleDef { name = "Blocks", fn = Blocks, intro = IntroStack },
        new StyleDef { name = "Plasma", fn = Plasma, intro = IntroSpiral },
        new StyleDef { name = "Synthwave", fn = Synthwave, intro = IntroWipe },
        new StyleDef { name = "Gold", fn = Gold, intro = IntroZoom },
        new StyleDef { name = "Ghost", fn = Ghost, intro = IntroZoom },
        new StyleDef { name = "Storm", fn = Storm, intro = IntroFly },
        new StyleDef { name = "Shockwave", fn = Shockwave, intro = IntroZoom },
        new StyleDef { name = "Disco", fn = Disco, intro = IntroDrop },
        new StyleDef { name = "Ocean", fn = Ocean, intro = IntroWipe },
        new StyleDef { name = "Hologram", fn = Hologram, intro = IntroWipe },
    };

    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
        { 'I', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" } },
        { 'X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" } },
        { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
        { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
        { 'C', new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" } },
        { 'K', new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" } },
        { 'R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" } },
    };

    private readonly List<Cell> cells = new List<Cell>();
    private CanvasGroup group;
    private RectTransform arrowLeft, arrowRight;
    private CanvasGroup arrowLeftGroup, arrowRightGroup;
    private float arrowLeftAlpha, arrowRightAlpha;
    private TMP_Text nameLabel;
    private float unit, introT, nameT = -1f;
    private int style;
    private static Sprite bevelSprite, glowSprite;
    private readonly Vector3[] corners = new Vector3[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { bevelSprite = null; glowSprite = null; }

    public int StyleCount => Styles.Length;

    /// <summary>Sets the whole logo's transparency (the Play transition fades it).</summary>
    public void SetAlpha(float a) { if (group != null) group.alpha = a; }

    // ------------------------------------------------------------------
    // Build
    // ------------------------------------------------------------------

    private static Sprite BevelSprite()
    {
        if (bevelSprite != null) return bevelSprite;
        const int n = 8;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = BodyBase;
                if (y < 2 || x >= n - 2) v = 0.4f;          // dark bottom / right
                else if (y >= n - 2 || x < 2) v = 1f;       // light top / left
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        tex.Apply(false, true);
        bevelSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return bevelSprite;
    }

    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 32;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - n * 0.5f) * (x + 0.5f - n * 0.5f) + (y + 0.5f - n * 0.5f) * (y + 0.5f - n * 0.5f)) / (n * 0.5f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(false, true);
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return glowSprite;
    }

    private RectTransform MakeLayer(string name)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(transform, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = Vector2.zero;
        return r;
    }

    private Image MakeCellImage(RectTransform layer, string name, Sprite sprite, float size, out RectTransform rt)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(layer, false);
        Image im = g.GetComponent<Image>();
        im.sprite = sprite;
        im.raycastTarget = false;
        rt = g.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        return im;
    }

    /// <summary>Builds the cells ('palette' colours the letters in turn) and the style arrows; 'width' = the logo's width in canvas units.</summary>
    public void Init(float width, Color[] palette, TMP_FontAsset font)
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (palette == null || palette.Length == 0) palette = new[] { Color.white };
        unit = width / (TotalCols + 3);
        // A different style every time the game starts (never the one used last time).
        int last = PixelPrefs.GetInt(Pref, -1);
        int fav = PixelPrefs.GetInt(FavoritePref, 0) - 1;   // the favourite logo of this save (0 = none)
        if (fav >= 0 && fav < Styles.Length) style = fav;
        else do style = Random.Range(0, Styles.Length); while (style == last && Styles.Length > 1);
        PixelPrefs.SetInt(Pref, style);
        current = this;

        RectTransform glowLayer = MakeLayer("Glows"), shadowLayer = MakeLayer("Shadows"), bodyLayer = MakeLayer("Bodies");
        string[] words = { "PIXEL", "CLICKER" };
        int rowOffset = 0, colorIndex = 0, letterIndex = 0, index = 0;
        foreach (string word in words)
        {
            int wordCols = word.Length * 5 + (word.Length - 1);
            int startCol = (TotalCols - wordCols) / 2;
            float wordBase = (GridRows - 1) * 0.5f - (rowOffset + 6) - 0.5f;
            for (int li = 0; li < word.Length; li++, letterIndex++)
            {
                if (!Glyphs.TryGetValue(word[li], out string[] glyph)) continue;
                Color baseColor = palette[colorIndex++ % palette.Length];
                int letterCol = startCol + li * 6;
                float lcx = letterCol + 2 - (TotalCols - 1) * 0.5f;
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                    {
                        if (glyph[r][c] != '#') continue;
                        Cell cell = new Cell();
                        cell.colG = letterCol + c;
                        cell.rowG = rowOffset + r;
                        cell.ux = cell.colG - (TotalCols - 1) * 0.5f;
                        cell.uy = (GridRows - 1) * 0.5f - cell.rowG;
                        cell.nx = cell.ux / (TotalCols * 0.5f);
                        cell.ny = cell.uy / (GridRows * 0.5f);
                        cell.dist = Mathf.Sqrt(cell.ux * cell.ux + cell.uy * cell.uy);
                        cell.angle = Mathf.Atan2(cell.uy, cell.ux);
                        cell.letter = letterIndex;
                        cell.word = word == "PIXEL" ? 0 : 1;
                        cell.lcx = lcx;
                        cell.wbY = wordBase;
                        cell.index = index++;
                        cell.rnd = Hash(cell.index, 17);
                        cell.rnd2 = Hash(cell.index, 91);
                        cell.baseColor = Color.Lerp(baseColor, Color.white, 0.25f * (1f - r / 6f));   // lighter toward the top of each letter
                        cell.glowImage = MakeCellImage(glowLayer, "Glow", GlowSprite(), unit * 4f, out cell.glow);
                        cell.shadowImage = MakeCellImage(shadowLayer, "Shadow", BevelSprite(), unit * 1.02f, out cell.shadow);
                        cell.bodyImage = MakeCellImage(bodyLayer, "Cell", BevelSprite(), unit * 1.02f, out cell.body);
                        cell.glowImage.enabled = false;
                        cells.Add(cell);
                    }
            }
            rowOffset += 8;
        }

        // The style arrows: invisible until the mouse is near, then fade in.
        float h = unit * 18f;
        arrowLeft = MakeArrow(font, "Logo Arrow Left", true, -(width * 0.5f + unit * 5f), -h * 0.5f, out arrowLeftGroup);
        arrowRight = MakeArrow(font, "Logo Arrow Right", false, width * 0.5f + unit * 5f, -h * 0.5f, out arrowRightGroup);
        arrowLeft.GetComponent<Button>().onClick.AddListener(() => Step(-1));
        arrowRight.GetComponent<Button>().onClick.AddListener(() => Step(1));
        starImages = new[] { MakeStar(arrowLeft), MakeStar(arrowRight) };
        RefreshStars();

        nameLabel = PixelUIKit.CreateText(font, transform, "Logo Style Name", "", 26f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.9f));
        nameLabel.raycastTarget = false;
        RectTransform nr = nameLabel.rectTransform;
        nr.anchorMin = new Vector2(0.5f, 0f); nr.anchorMax = new Vector2(0.5f, 0f); nr.pivot = new Vector2(0.5f, 1f);
        nr.sizeDelta = new Vector2(500f, 36f);
        nr.anchoredPosition = new Vector2(0f, -4f);

        introT = 0f;
    }

    private RectTransform MakeArrow(TMP_FontAsset font, string name, bool left, float x, float y, out CanvasGroup g)
    {
        Button b = PixelUIKit.CreateButton(font, transform, name, left ? "<" : ">", new Vector2(64f, 90f), new Color(0.25f, 0.3f, 0.4f, 0.95f), Color.white, 36f);
        PixelUIKit.UseArrowGlyph(b, left);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        g = b.gameObject.AddComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        return r;
    }

    private void Step(int direction)
    {
        if (group != null && group.alpha < 0.9f) return;   // (the logo is fading away for Play)
        SetStyle((style + direction + Styles.Length) % Styles.Length);
        PixelAudio.Play("ui_click");
    }

    private void SetStyle(int index)
    {
        style = index;
        introT = 0f;
        nameT = 0f;
        PixelPrefs.SetInt(Pref, style);
        RefreshStars();
        if (nameLabel != null) nameLabel.text = Styles[style].name.ToUpperInvariant() + "   " + (style + 1) + " / " + Styles.Length;
    }

    // ------------------------------------------------------------------
    // Frame
    // ------------------------------------------------------------------

    private void UpdateArrow(RectTransform r, CanvasGroup g, ref float alpha, float dt)
    {
        Vector2 pointer = PixelInput.PointerPosition();
        r.GetWorldCorners(corners);   // overlay canvas: world = screen pixels
        const float margin = 60f;
        bool over = group.alpha > 0.9f && pointer.x >= corners[0].x - margin && pointer.x <= corners[2].x + margin && pointer.y >= corners[0].y - margin && pointer.y <= corners[2].y + margin;
        alpha = Mathf.MoveTowards(alpha, over ? 1f : 0f, dt / (over ? 0.12f : 0.35f));
        g.alpha = alpha;
        g.blocksRaycasts = alpha > 0.05f;
    }

    private void OnEnable()
    {
        introT = 0f;   // the intro plays each time the title appears
        if (nameLabel != null) nameLabel.text = "";
    }

    private void Update()
    {
        if (cells.Count == 0) return;
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        introT += dt;
        UpdateArrow(arrowLeft, arrowLeftGroup, ref arrowLeftAlpha, dt);
        UpdateArrow(arrowRight, arrowRightGroup, ref arrowRightAlpha, dt);
        if (nameT >= 0f)
        {
            nameT += dt;
            nameLabel.color = new Color(1f, 1f, 1f, Mathf.Clamp01(2.4f - nameT) * 0.9f);
            if (nameT > 2.4f) { nameT = -1f; nameLabel.text = ""; }
        }

        StyleDef def = Styles[style];
        float shadowOff = unit * 0.375f;
        foreach (Cell c in cells)
        {
            S s = new S { sx = 1f, sy = 1f, lsx = 1f, lsy = 1f, alpha = 1f, shadow = 0.55f, col = c.baseColor, glowCol = Color.white };
            def.fn(c, t, ref s);
            Intro(def.intro, c, introT, ref s);

            Vector2 off = s.off;
            off.x += (c.ux - c.lcx) * (s.lsx - 1f);
            off.y += (c.uy - c.wbY) * (s.lsy - 1f);
            Vector2 p = (new Vector2(c.ux, c.uy) + off) * unit;
            Vector3 scale = new Vector3(s.sx * s.lsx, s.sy * s.lsy, 1f);
            Quaternion rot = Quaternion.Euler(0f, 0f, s.rot);
            float a = Mathf.Clamp01(s.alpha) * s.col.a;

            c.body.anchoredPosition = p; c.body.localScale = scale; c.body.localRotation = rot;
            c.bodyImage.color = new Color(Mathf.Clamp01(s.col.r / BodyBase), Mathf.Clamp01(s.col.g / BodyBase), Mathf.Clamp01(s.col.b / BodyBase), a);

            bool showShadow = s.shadow > 0.01f && a > 0.01f;
            if (c.shadowImage.enabled != showShadow) c.shadowImage.enabled = showShadow;
            if (showShadow)
            {
                c.shadow.anchoredPosition = p + new Vector2(shadowOff, -shadowOff); c.shadow.localScale = scale; c.shadow.localRotation = rot;
                c.shadowImage.color = new Color(0f, 0f, 0f, s.shadow * a);
            }

            bool showGlow = s.glow > 0.01f && a > 0.01f;
            if (c.glowImage.enabled != showGlow) c.glowImage.enabled = showGlow;
            if (showGlow)
            {
                c.glow.anchoredPosition = p;
                c.glow.localScale = Vector3.one * (0.8f + s.glow * 0.5f);
                c.glowImage.color = new Color(s.glowCol.r, s.glowCol.g, s.glowCol.b, Mathf.Clamp01(s.glow) * a * 0.55f);
            }
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static float Hash(int a, int b)
    {
        float v = Mathf.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private static Color Hue(float h, float sat = 1f, float val = 1f) => Color.HSVToRGB(Mathf.Repeat(h, 1f), sat, val);

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    private static float EaseOutBack(float x) { x = Mathf.Clamp01(x); const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }

    private static float EaseOutBounce(float x)
    {
        x = Mathf.Clamp01(x);
        const float n1 = 7.5625f, d1 = 2.75f;
        if (x < 1f / d1) return n1 * x * x;
        if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
        if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
        x -= 2.625f / d1; return n1 * x * x + 0.984375f;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float cs = Mathf.Cos(radians), sn = Mathf.Sin(radians);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    // ------------------------------------------------------------------
    // Intros (play each time a style is shown)
    // ------------------------------------------------------------------

    private static void Intro(int kind, Cell c, float it, ref S s)
    {
        if (kind == IntroNone || it > 3f) return;
        switch (kind)
        {
            case IntroDrop:
            {
                float delay = (c.nx * 0.5f + 0.5f) * 0.5f + c.rnd * 0.25f;
                float p = Mathf.Clamp01((it - delay) / 0.7f);
                if (p <= 0f) { s.alpha = 0f; return; }
                s.off.y += (1f - EaseOutBounce(p)) * 24f;
                break;
            }
            case IntroStack:
            {
                float delay = (GridRows - 1 - c.rowG) * 0.06f + c.rnd * 0.12f;
                float p = Mathf.Clamp01((it - delay) / 0.75f);
                if (p <= 0f) { s.alpha = 0f; return; }
                s.off.y += (1f - EaseOutBounce(p)) * 28f;
                break;
            }
            case IntroFly:
            {
                float delay = c.rnd * 0.4f;
                float p = Mathf.Clamp01((it - delay) / 0.9f);
                if (p <= 0f) { s.alpha = 0f; return; }
                float e = EaseOutBack(p);
                Vector2 start = new Vector2(Mathf.Cos(c.rnd2 * 6.283f), Mathf.Sin(c.rnd2 * 6.283f)) * (26f + c.rnd * 22f);
                s.off = Vector2.LerpUnclamped(start, s.off, e);
                s.rot += (1f - e) * 360f * (c.rnd - 0.5f);
                break;
            }
            case IntroZoom:
            {
                float delay = c.dist * 0.012f + c.rnd * 0.15f;
                float p = Mathf.Clamp01((it - delay) / 0.5f);
                if (p <= 0f) { s.alpha = 0f; return; }
                float e = EaseOutBack(p);
                s.sx *= e; s.sy *= e;
                break;
            }
            case IntroWipe:
            {
                float delay = (c.nx * 0.5f + 0.5f) * 0.7f;
                float p = Mathf.Clamp01((it - delay) / 0.35f);
                s.alpha *= p;
                float grow = 1f + (1f - p) * 0.8f;
                s.sx *= grow; s.sy *= grow;
                break;
            }
            case IntroSpiral:
            {
                float delay = c.rnd * 0.3f;
                float p = Mathf.Clamp01((it - delay) / 1f);
                float e = 1f - Mathf.Pow(1f - p, 3f);
                Vector2 pos = new Vector2(c.ux, c.uy);
                s.off += Rotate(pos, (1f - e) * 4f) * (1f + (1f - e) * 1.8f) - pos;
                s.alpha *= Mathf.Clamp01(p * 4f);
                break;
            }
            case IntroFlip:
            {
                float delay = c.letter * 0.08f;
                float p = Mathf.Clamp01((it - delay) / 0.5f);
                if (p <= 0f) { s.alpha = 0f; return; }
                s.lsx *= EaseOutBack(p);
                break;
            }
        }
    }

    // ------------------------------------------------------------------
    // The styles
    // ------------------------------------------------------------------

    private static void Classic(Cell c, float t, ref S s)
    {
        float pos = c.nx * 0.5f - c.ny * 0.2f;
        float sweep = Mathf.Repeat(t * 0.25f, 2.4f) - 0.7f;
        float k = Mathf.Clamp01(1f - Mathf.Abs(pos - (sweep - 0.5f)) * 6f);   // a light band that sweeps across now and then
        s.col = Color.Lerp(c.baseColor, Color.white, k * 0.7f);
    }

    private static void RainbowWave(Cell c, float t, ref S s)
    {
        s.col = Hue(c.nx * 0.25f + t * 0.25f - c.ny * 0.05f, 0.75f, 1f);
        s.off.y = Mathf.Sin(t * 2.4f + c.ux * 0.28f) * 0.9f;
    }

    private static void Neon(Cell c, float t, ref S s)
    {
        Color baseCol = c.word == 0 ? new Color(1f, 0.25f, 0.75f) : new Color(0.2f, 0.9f, 1f);
        float n = Mathf.PerlinNoise(c.letter * 3.1f, t * 2.5f);
        float k = n < 0.17f ? 0.22f : 1f;   // a letter now and then stutters like a failing tube
        float hum = 0.9f + 0.1f * Mathf.Sin(t * 9f + c.letter);
        s.col = Color.Lerp(baseCol, Color.white, 0.35f) * (hum * k);
        s.col.a = 1f;
        s.glow = 0.85f * k;
        s.glowCol = baseCol;
        s.shadow = 0f;
    }

    private static void Shatter(Cell c, float t, ref S s)
    {
        float ph = Mathf.Repeat(t, 7f), k;
        if (ph < 4.2f) k = 0f;
        else if (ph < 4.9f) k = Smooth((ph - 4.2f) / 0.7f);
        else if (ph < 5.5f) k = 1f;
        else if (ph < 6.8f) k = 1f - Smooth((ph - 5.5f) / 1.3f);
        else k = 0f;
        Vector2 dir = new Vector2(c.ux, c.uy).normalized + new Vector2(c.rnd - 0.5f, c.rnd2 - 0.5f) * 1.5f;
        s.off = dir * k * (10f + c.rnd * 18f);
        s.rot = k * (c.rnd2 - 0.5f) * 540f;
        s.col = Color.Lerp(c.baseColor, Hue(c.rnd), k * 0.6f);
        s.glow = k * 0.5f;
        s.glowCol = s.col;
    }

    private static void Matrix(Cell c, float t, ref S s)
    {
        float speed = 6f + Hash(c.colG, 3) * 5f, offset = Hash(c.colG, 8) * 40f;
        float head = Mathf.Repeat(t * speed + offset, GridRows + 12f) - 4f;
        float d = head - c.rowG;
        float bright = d >= 0f && d < 9f ? 1f - d / 9f : 0f;
        Color dark = new Color(0.02f, 0.28f, 0.08f), lit = new Color(0.45f, 1f, 0.55f);
        s.col = Color.Lerp(dark, lit, bright);
        if (d >= 0f && d < 1f) s.col = Color.Lerp(s.col, Color.white, 0.7f);
        float flick = Hash(c.index, Mathf.FloorToInt(t * 9f));
        s.alpha = 0.75f + 0.25f * flick;
        s.glow = bright * 0.6f;
        s.glowCol = new Color(0.2f, 1f, 0.4f);
        s.shadow = 0.25f;
    }

    private static void Inferno(Cell c, float t, ref S s)
    {
        float n = Mathf.PerlinNoise(c.ux * 0.25f, t * 2.2f - c.uy * 0.15f);
        float v = Mathf.Clamp01((0.5f - c.ny * 0.5f) * 0.8f + n * 0.5f);   // hotter at the bottom
        Color red = new Color(0.8f, 0.08f, 0.04f), orange = new Color(1f, 0.5f, 0.08f), yellow = new Color(1f, 0.95f, 0.4f);
        s.col = v < 0.5f ? Color.Lerp(red, orange, v * 2f) : Color.Lerp(orange, yellow, (v - 0.5f) * 2f);
        s.off.y = (n - 0.4f) * 1.1f;
        s.sy = 1f + n * 0.35f;
        s.glow = 0.5f;
        s.glowCol = new Color(1f, 0.4f, 0.05f);
    }

    private static void Frost(Cell c, float t, ref S s)
    {
        Color a = new Color(0.5f, 0.82f, 1f), b = new Color(0.9f, 0.97f, 1f);
        s.col = Color.Lerp(a, b, 0.5f + 0.5f * Mathf.Sin(c.ux * 0.2f + c.uy * 0.3f - t * 0.8f));
        s.off.y = Mathf.Sin(t * 1.2f + c.ux * 0.1f) * 0.35f;
        float ph = Mathf.Repeat(t * 0.35f + c.rnd * 5f, 3f);
        if (ph < 0.2f)
        {
            float tw = Mathf.Sin(ph / 0.2f * Mathf.PI);
            s.col = Color.Lerp(s.col, Color.white, tw);
            s.glow = tw;
            s.glowCol = new Color(0.7f, 0.9f, 1f);
            s.sx = s.sy = 1f + 0.25f * tw;
        }
    }

    private static void Glitch(Cell c, float t, ref S s)
    {
        int slice = Mathf.FloorToInt(t * 14f);
        bool burst = Mathf.PerlinNoise(Mathf.Floor(t * 7f) * 0.37f, 0.5f) > 0.74f;
        if (!burst) return;
        if (Hash(c.rowG, slice) > 0.55f)
        {
            s.off.x = (Hash(c.rowG, slice + 5) - 0.5f) * 9f;
            float tint = Hash(c.rowG, slice + 9);
            s.col = tint < 0.33f ? new Color(1f, 0.1f, 0.35f) : tint < 0.66f ? new Color(0.1f, 1f, 0.95f) : Color.white;
            s.glow = 0.4f;
            s.glowCol = s.col;
        }
        if (Hash(c.index, slice) > 0.93f) s.alpha = 0.1f;
    }

    private static void Jelly(Cell c, float t, ref S s)
    {
        float ph = Mathf.Repeat(t - c.letter * 0.13f, 2.6f);
        const float air = 0.9f;
        if (ph < air)
        {
            float u = ph / air;
            s.off.y = 4f * u * (1f - u) * 3.5f;
            s.lsy = 1f + 0.3f * Mathf.Sin(u * Mathf.PI);
            s.lsx = 1f - 0.12f * Mathf.Sin(u * Mathf.PI);
        }
        else
        {
            float w = Mathf.Exp(-(ph - air) * 5f) * Mathf.Sin((ph - air) * 22f) * 0.25f;   // squashes and wobbles after landing
            s.lsy = 1f - w;
            s.lsx = 1f + w;
        }
        s.col = Hue(c.letter * 0.085f, 0.8f, 1f);
    }

    private static void FlipStyle(Cell c, float t, ref S s)
    {
        float ph = Mathf.Repeat(t * 0.5f - c.letter * 0.08f, 3f);
        float shade = 1f;
        if (ph < 0.8f)
        {
            float cs = Mathf.Cos(ph / 0.8f * Mathf.PI * 2f);
            s.lsx = cs;
            shade = 0.55f + 0.45f * Mathf.Abs(cs);
        }
        s.col = c.baseColor * shade;
        s.col.a = 1f;
    }

    private static readonly Color[] Tetromino =
    {
        new Color(0.2f, 0.9f, 1f), new Color(1f, 0.9f, 0.15f), new Color(0.7f, 0.25f, 0.9f), new Color(0.25f, 0.85f, 0.35f),
        new Color(0.95f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 1f), new Color(1f, 0.55f, 0.1f),
    };

    private static void Blocks(Cell c, float t, ref S s)
    {
        s.col = Tetromino[c.letter % Tetromino.Length];
        float head = Mathf.Repeat(t * 9f, 34f);   // a cleared-line flash runs down the rows
        float k = Mathf.Clamp01(1f - Mathf.Abs(head - c.rowG) * 0.5f);
        s.col = Color.Lerp(s.col, Color.white, k);
        s.glow = k * 0.6f;
    }

    private static void Plasma(Cell c, float t, ref S s)
    {
        float v = Mathf.Sin(c.ux * 0.22f + t * 1.1f) + Mathf.Sin(c.uy * 0.45f - t * 1.3f) + Mathf.Sin((c.ux + c.uy) * 0.18f + t * 0.7f);
        s.col = Hue(v / 6f + 0.5f + t * 0.03f, 0.85f, 1f);
        s.sx = s.sy = 0.85f + 0.2f * Mathf.Sin(v * 2f);
    }

    private static void Synthwave(Cell c, float t, ref S s)
    {
        float t01 = Mathf.Clamp01(0.5f - c.ny * 0.5f);   // 0 at the top
        s.col = Color.Lerp(new Color(1f, 0.25f, 0.7f), new Color(1f, 0.85f, 0.2f), t01);
        if (c.rowG % 2 == 1) s.col *= 0.8f;
        float scan = Mathf.Repeat(t * 0.5f, 1.4f) - 0.2f;
        float k = Mathf.Exp(-Mathf.Pow((t01 - scan) * 8f, 2f));
        s.col = Color.Lerp(s.col, Color.white, k * 0.6f);
        s.col.a = 1f;
        s.glow = 0.45f;
        s.glowCol = new Color(1f, 0.2f, 0.8f);
        s.off.y = Mathf.Sin(t * 1.6f) * 0.25f;
    }

    private static void Gold(Cell c, float t, ref S s)
    {
        s.col = Color.Lerp(new Color(0.62f, 0.42f, 0.08f), new Color(1f, 0.83f, 0.28f), 0.5f + 0.5f * Mathf.Sin(c.rowG * 0.9f + c.ux * 0.05f));
        float pos = c.nx * 0.6f + c.ny * 0.4f;
        float band = Mathf.Repeat(t * 0.35f, 2.4f) - 0.7f;
        float k = Mathf.Exp(-Mathf.Pow((pos - (band - 0.5f)) * 4f, 2f));
        s.col = Color.Lerp(s.col, Color.white, k * 0.85f);
        float ph = Mathf.Repeat(t * 0.5f + c.rnd * 7f, 4f);
        if (ph < 0.25f)
        {
            float tw = Mathf.Sin(ph / 0.25f * Mathf.PI);
            s.glow = tw;
            s.glowCol = new Color(1f, 0.95f, 0.6f);
            s.sx = s.sy = 1f + 0.3f * tw;
        }
        s.off.y = Mathf.Sin(t * 1.1f) * 0.2f;
    }

    private static void Ghost(Cell c, float t, ref S s)
    {
        s.col = new Color(0.82f, 0.92f, 1f, 1f);
        s.alpha = 0.55f + 0.35f * Mathf.Sin(t * 1.7f + c.ux * 0.15f + c.letter);
        s.off.x = Mathf.Sin(t * 1.3f + c.uy * 0.3f) * 0.5f;
        s.off.y = Mathf.Sin(t * 0.9f + c.ux * 0.2f) * 0.6f;
        if (Mathf.PerlinNoise(c.letter * 5.3f, t * 0.4f) < 0.25f) s.alpha *= 0.15f;   // a letter fades out now and then
        s.glow = 0.4f;
        s.glowCol = new Color(0.6f, 0.8f, 1f);
        s.shadow = 0f;
    }

    private static void Storm(Cell c, float t, ref S s)
    {
        s.col = new Color(0.25f, 0.5f, 1f);
        float ph = Mathf.Repeat(t, 2.2f);
        if (Hash(c.index, Mathf.FloorToInt(t * 20f)) > 0.97f) s.col = Color.white;   // crackle
        if (ph < 0.35f)
        {
            float k = Mathf.Clamp01(1f - Mathf.Abs(c.nx - (ph / 0.35f * 2f - 1f)) * 5f);   // a bolt races across
            s.col = Color.Lerp(s.col, Color.white, k);
            s.glow = k;
            s.glowCol = new Color(0.5f, 0.8f, 1f);
            s.off += new Vector2(Hash(c.index, Mathf.FloorToInt(t * 40f)) - 0.5f, Hash(c.index, 7 + Mathf.FloorToInt(t * 40f)) - 0.5f) * 0.6f * k;
        }
    }

    private static void Shockwave(Cell c, float t, ref S s)
    {
        float r = Mathf.Repeat(t * 14f, 44f);
        float d = c.dist - r;
        float k = Mathf.Exp(-d * d * 0.08f);
        Vector2 dir = new Vector2(c.ux, c.uy).normalized;
        s.off = dir * k * 1.2f;
        s.col = Color.Lerp(c.baseColor, Color.white, k);
        s.sx = s.sy = 1f + 0.4f * k;
        s.glow = k * 0.7f;
    }

    private static void Disco(Cell c, float t, ref S s)
    {
        int beat = Mathf.FloorToInt(t * 3f);
        float lit = Hash(c.index, beat);
        if (lit > 0.6f)
        {
            s.col = Hue(Hash(c.index, beat + 40), 0.85f, 1f);
            s.glow = 0.7f;
            s.glowCol = s.col;
        }
        else s.col = c.baseColor * 0.55f;
        s.col.a = 1f;
        s.off.y = Mathf.Abs(Mathf.Sin(t * 4f + c.letter * 0.5f)) * 1f;
    }

    private static void Ocean(Cell c, float t, ref S s)
    {
        float w = Mathf.Sin(t * 2f + c.ux * 0.3f);
        s.off.y = w * 0.7f;
        s.off.x = Mathf.Cos(t * 1.5f + c.uy * 0.4f) * 0.3f;
        float depth = Mathf.Clamp01(0.5f - c.ny * 0.5f);
        s.col = Color.Lerp(new Color(0.4f, 0.85f, 1f), new Color(0.05f, 0.25f, 0.7f), depth);
        if (w > 0.85f && c.rowG % 7 < 2) s.col = Color.Lerp(s.col, Color.white, 0.8f);   // foam on the crests
    }

    private static void Hologram(Cell c, float t, ref S s)
    {
        s.col = new Color(0.3f, 1f, 0.95f);
        float scan = Mathf.Repeat(t * 0.6f, 1.4f) - 0.2f;
        float k = Mathf.Exp(-Mathf.Pow((0.5f - c.ny * 0.5f - scan) * 10f, 2f));
        s.alpha = 0.55f + 0.4f * k + 0.1f * Hash(c.index, Mathf.FloorToInt(t * 12f));
        s.col = Color.Lerp(s.col, Color.white, k * 0.6f);
        if (Hash(c.rowG, Mathf.FloorToInt(t * 12f)) > 0.93f) s.off.x = (Hash(c.rowG, 3) - 0.5f) * 2f;
        s.glow = 0.3f + 0.3f * k;
        s.glowCol = new Color(0.2f, 1f, 0.9f);
        s.shadow = 0f;
    }
}
