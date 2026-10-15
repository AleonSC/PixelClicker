// ---- System notes ----
// PixelBarEdgeFx.cs
//   BAR STYLES: what glows along the inner edge of the black bars (the HUD's top / bottom bars and the title screen's bar). PixelBarStyle.Current (Settings > Display 'Bar style', PlayerPrefs
//   PixelClicker.Setting.BarStyle, the player's own like the other look settings - not per save): 0 Neon glow (the original: soft outer + inner gradient and a bright line, PixelBarGlow),
//   1 Pixel equalizer (columns of pixel cells rising from the edge to the music / sounds: AudioListener.GetSpectrumData, a quiet ambient wobble when nothing plays, falling peak dots),
//   2 Pixel wave (rolling heights, no audio), 3 Marquee (one row of cells with a light chasing along), 4 Scanner (a comet sweeping to and fro), 5 Thin line (just the line).
//   PixelBarEdgeFx.Create(parent, edgeIsTop) builds one edge (all style parts, built lazily the first time a style needs them); the owner calls Tick(colour, strength) every frame
//   (PixelHud for the two bars, PixelTitleScreen for the title bar's two edges). Cells are a tiled 12 x 12 sprite, columns snap to whole cells; the colour follows the glow colour
//   (a slight hue shift along the row). To add a style: a name in PixelBarStyle.Names and a case in PixelBarEdgeFx.Tick.
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

/// <summary>The bar edge styles (the player's choice, kept on this machine).</summary>
public static class PixelBarStyle
{
    public static readonly string[] Names = { "Neon glow", "Pixel equalizer", "Pixel wave", "Marquee", "Scanner", "Thin line" };
    private const string Pref = "PixelClicker.Setting.BarStyle";
    private static int cache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache = -1; }

    public static int Current
    {
        get { if (cache < 0) cache = Mathf.Clamp(PixelPrefs.GetInt(Pref, 0), 0, Names.Length - 1); return cache; }
        set { cache = Mathf.Clamp(value, 0, Names.Length - 1); PixelPrefs.SetInt(Pref, cache); PixelPrefs.Save(); }
    }
}

public class PixelBarEdgeFx : MonoBehaviour
{
    private const float Cell = 12f, Pitch = 16f;   // a cell is 12 canvas units, one column every 16
    private const int MaxCells = 7;

    private static Sprite cellSprite, blobSprite;
    private static readonly float[] spectrum = new float[256];
    private static int spectrumFrame = -1;
    private static float spectrumEnergy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cellSprite = null; blobSprite = null; spectrumFrame = -1; }

    private static Sprite CellSprite()
    {
        if (cellSprite != null) return cellSprite;
        Texture2D t = new Texture2D(12, 12, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 12; y++) for (int x = 0; x < 12; x++) t.SetPixel(x, y, x >= 1 && x <= 10 && y >= 1 && y <= 10 ? Color.white : new Color(1f, 1f, 1f, 0f));
        t.Apply(false, true);
        cellSprite = Sprite.Create(t, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return cellSprite;
    }

    private static Sprite BlobSprite()
    {
        if (blobSprite != null) return blobSprite;
        const int w = 64;
        Texture2D t = new Texture2D(w, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < w; x++)
        {
            float d = Mathf.Abs(x / (w - 1f) - 0.5f) * 2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
            for (int y = 0; y < 2; y++) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(false, true);
        blobSprite = Sprite.Create(t, new Rect(0, 0, w, 2), new Vector2(0.5f, 0.5f), 100f);
        return blobSprite;
    }

    /// <summary>Builds one bar edge under 'parent' (stretched over it). 'edgeIsTop' = the parent's TOP edge is the one facing the play area (a bottom bar); false = its bottom edge (a top bar).</summary>
    public static PixelBarEdgeFx Create(Transform parent, bool edgeIsTop, float glowSize, float innerReach)
    {
        GameObject go = new GameObject("Bar Edge Fx", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        PixelBarEdgeFx fx = go.AddComponent<PixelBarEdgeFx>();
        fx.edgeIsTop = edgeIsTop;
        fx.holder = rt;
        fx.outer = PixelBarGlow.AddStrip(rt, "Glow", edgeIsTop, true, glowSize);
        fx.inner = PixelBarGlow.AddStrip(rt, "Glow Inner", edgeIsTop, false, innerReach);
        fx.line = PixelBarGlow.AddLine(rt, "Glow Line", edgeIsTop);
        return fx;
    }

    private bool edgeIsTop;
    private RectTransform holder;
    private Image outer, inner, line;

    // equalizer / wave / marquee columns
    private RectTransform columnRoot;
    private Image[] bodies, peaks;
    private float[] height, peak, peakHold;
    private int columns;
    // scanner
    private RectTransform scannerRoot;
    private Image blob, core;

    /// <summary>Draws the current style in colour 'c' at overall strength 'strength' (call every frame while the bar glow is on).</summary>
    public void Tick(Color c, float strength)
    {
        int style = PixelBarStyle.Current;
        bool neon = style == 0;
        outer.gameObject.SetActive(neon); inner.gameObject.SetActive(neon);
        bool columnStyle = style >= 1 && style <= 3;
        if (columnRoot != null && columnRoot.gameObject.activeSelf != columnStyle) columnRoot.gameObject.SetActive(columnStyle);
        if (scannerRoot != null && scannerRoot.gameObject.activeSelf != (style == 4)) scannerRoot.gameObject.SetActive(style == 4);

        if (neon) { PixelBarGlow.Colorize(outer, inner, line, c, strength); return; }
        // Every other style keeps the thin line (dimmer under the animated ones).
        Color lc = Color.Lerp(c, Color.white, 0.55f);
        lc.a = style == 5 ? 1f : 0.55f;
        line.color = lc;
        if (style == 5) return;
        if (columnStyle) { EnsureColumns(); TickColumns(style, c, strength); }
        else { EnsureScanner(); TickScanner(c, strength); }
    }

    // ---------------- columns ----------------

    private void EnsureColumns()
    {
        int wanted = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(holder.rect.width, 640f) / Pitch), 16, 150);
        if (columnRoot != null && columns == wanted) return;
        if (columnRoot != null) Destroy(columnRoot.gameObject);
        columns = wanted;
        GameObject go = new GameObject("Columns", typeof(RectTransform));
        go.transform.SetParent(holder, false);
        columnRoot = go.GetComponent<RectTransform>();
        float edgeY = edgeIsTop ? 1f : 0f;
        columnRoot.anchorMin = new Vector2(0f, edgeY); columnRoot.anchorMax = new Vector2(1f, edgeY);
        columnRoot.pivot = new Vector2(0.5f, edgeIsTop ? 0f : 1f);
        columnRoot.sizeDelta = Vector2.zero; columnRoot.anchoredPosition = Vector2.zero;
        bodies = new Image[columns]; peaks = new Image[columns];
        height = new float[columns]; peak = new float[columns]; peakHold = new float[columns];
        for (int i = 0; i < columns; i++)
        {
            bodies[i] = NewCell(columnRoot, "Column " + i, i, true);
            peaks[i] = NewCell(columnRoot, "Peak " + i, i, false);
        }
    }

    private Image NewCell(RectTransform parent, string name, int index, bool tiled)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = CellSprite();
        img.type = tiled ? Image.Type.Tiled : Image.Type.Simple;
        RectTransform r = go.GetComponent<RectTransform>();
        float x = (index + 0.5f) / columns;
        r.anchorMin = r.anchorMax = new Vector2(x, 0.5f);
        r.pivot = new Vector2(0.5f, edgeIsTop ? 0f : 1f);
        r.anchorMin = new Vector2(x, edgeIsTop ? 0f : 1f); r.anchorMax = r.anchorMin;
        r.sizeDelta = new Vector2(Cell, Cell);
        r.anchoredPosition = Vector2.zero;
        img.enabled = false;
        return img;
    }

    private static void SampleSpectrum()
    {
        if (spectrumFrame == Time.frameCount) return;
        spectrumFrame = Time.frameCount;
        AudioListener.GetSpectrumData(spectrum, 0, FFTWindow.Blackman);
        float e = 0f;
        for (int i = 0; i < 96; i++) e += spectrum[i];
        spectrumEnergy = e;
    }

    private void TickColumns(int style, Color c, float strength)
    {
        float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
        Color.RGBToHSV(c, out float hue, out float sat, out float val);
        bool audio = false;
        if (style == 1) { SampleSpectrum(); audio = spectrumEnergy > 0.0008f; }
        for (int i = 0; i < columns; i++)
        {
            float u = i / (float)(columns - 1);
            float target;
            if (style == 1)
            {
                if (audio)
                {
                    // Low sounds on the left, high on the right (a log-ish spread over the first ~110 bins), a gain that grows with frequency.
                    int bin = Mathf.Clamp(Mathf.FloorToInt(Mathf.Pow(u, 2.1f) * 110f), 0, 200);
                    float v = spectrum[bin] + (bin + 1 < spectrum.Length ? spectrum[bin + 1] : 0f);
                    target = Mathf.Clamp01(Mathf.Sqrt(v * (6f + bin * 0.35f)));
                }
                else target = 0.12f + 0.3f * Mathf.PerlinNoise(i * 0.23f, t * 1.4f);   // nothing playing: a quiet ambient wobble
            }
            else if (style == 2)
                target = Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(i * 0.2f - t * 2.4f)) * (0.55f + 0.45f * Mathf.Sin(t * 0.7f + i * 0.06f));
            else target = 1f / MaxCells;   // marquee: one row

            // Quick to rise, slow to fall.
            height[i] = target > height[i] ? Mathf.Lerp(height[i], target, 1f - Mathf.Exp(-22f * dt)) : Mathf.Max(target, height[i] - 1.6f * dt);
            int cells = style == 3 ? 1 : Mathf.Clamp(Mathf.RoundToInt(height[i] * MaxCells), 0, MaxCells);

            float h2 = Mathf.Repeat(hue + (u - 0.5f) * 0.22f, 1f);
            Color col = Color.HSVToRGB(h2, Mathf.Clamp01(sat), 1f);
            float a = strength;
            if (style == 3) a *= 0.25f + 0.75f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(i * 0.35f - t * 5f)), 2f);

            Image body = bodies[i];
            bool show = cells > 0;
            if (body.enabled != show) body.enabled = show;
            if (show)
            {
                body.rectTransform.sizeDelta = new Vector2(Cell, cells * Cell);
                col.a = Mathf.Clamp01(a * 1.4f);
                body.color = col;
            }

            Image pk = peaks[i];
            if (style == 1)
            {
                // The peak dot: holds a moment above the column, then falls a cell at a time.
                if (cells >= peak[i]) { peak[i] = cells; peakHold[i] = 0.35f; }
                else if (peakHold[i] > 0f) peakHold[i] -= dt;
                else peak[i] = Mathf.Max(cells, peak[i] - 9f * dt);
                int pc = Mathf.Clamp(Mathf.RoundToInt(peak[i]), 0, MaxCells + 1);
                bool showPeak = pc > cells && pc > 0;
                if (pk.enabled != showPeak) pk.enabled = showPeak;
                if (showPeak)
                {
                    pk.rectTransform.anchoredPosition = new Vector2(0f, (edgeIsTop ? 1f : -1f) * (pc - 1) * Cell);
                    Color pcol = Color.Lerp(col, Color.white, 0.6f); pcol.a = Mathf.Clamp01(a * 1.6f);
                    pk.color = pcol;
                }
            }
            else if (pk.enabled) pk.enabled = false;
        }
    }

    // ---------------- scanner ----------------

    private void EnsureScanner()
    {
        if (scannerRoot != null) return;
        GameObject go = new GameObject("Scanner", typeof(RectTransform));
        go.transform.SetParent(holder, false);
        scannerRoot = go.GetComponent<RectTransform>();
        float edgeY = edgeIsTop ? 1f : 0f;
        scannerRoot.anchorMin = new Vector2(0f, edgeY); scannerRoot.anchorMax = new Vector2(1f, edgeY);
        scannerRoot.pivot = new Vector2(0.5f, 0.5f);
        scannerRoot.sizeDelta = Vector2.zero; scannerRoot.anchoredPosition = Vector2.zero;
        blob = NewScanImage("Comet Glow", BlobSprite(), new Vector2(520f, 46f));
        core = NewScanImage("Comet Core", BlobSprite(), new Vector2(200f, 6f));
    }

    private Image NewScanImage(string name, Sprite sprite, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(scannerRoot, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = sprite;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0.5f, edgeIsTop ? 0f : 1f);
        r.sizeDelta = size;
        return img;
    }

    private void TickScanner(Color c, float strength)
    {
        float width = Mathf.Max(holder.rect.width, 640f);
        float t = Time.unscaledTime * 0.32f;
        float ping = Mathf.PingPong(t * 2f, 1f);                 // 0..1..0
        float x = Mathf.SmoothStep(0f, 1f, ping) * width;
        Color glow = c; glow.a = Mathf.Clamp01(strength * 1.1f);
        Color hot = Color.Lerp(c, Color.white, 0.7f); hot.a = 1f;
        blob.rectTransform.anchoredPosition = new Vector2(x, 0f); blob.color = glow;
        core.rectTransform.anchoredPosition = new Vector2(x, 0f); core.color = hot;
    }
}
