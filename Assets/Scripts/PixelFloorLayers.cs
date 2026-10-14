using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static PixelNoise;

/// <summary>
/// Big, non-repeating floors (part of <see cref="PixelFloor"/>).
/// <list type="bullet">
/// <item>Large textures (up to 1024 px, one copy over ~36-40 world units) are drawn a few milliseconds per frame (<see cref="FillSliced"/>) so picking a style never freezes the game.</item>
/// <item>Animated floors that used to redraw the whole texture many times a second (Radar, Circuit Flow, Watching Eyes) now draw their still picture once and only repaint the
///   small parts that move (<see cref="OverlayFrame"/>: radar blips, circuit sparks, blinking eyes).</item>
/// <item>FLOOR CACHE: a finished picture is saved (gzip) to <persistentDataPath>/FloorCache and loaded at once the next time that style is picked (key = style settings + build stamp; <c>cacheFloors</c> turns it off). The Aura floors also draw on all CPU cores (<c>ShadeKiAura</c> is pure).</item>
/// <item>The crater floors (Battle Crater, Moon Surface, Red Planet): a few big craters plus small ones and pits, lit from the upper left.</item>
/// </list>
/// </summary>
public partial class PixelFloor
{
    // ------------------------------------------------------------------
    // State read by the shaders
    // ------------------------------------------------------------------

    /// <summary>Multiplier for the noise lattices of a style that covers a big area with one copy (so the features keep their world size).</summary>
    private static int shadeDetail = 1;

    /// <summary>True while the still picture of an overlay style is drawn (the moving parts are painted later by <see cref="OverlayFrame"/>).</summary>
    private static bool shadeBase;

    /// <summary>Where the craters' big crater goes (texture coordinates): the part of the floor the camera looks at.</summary>
    private static Vector2 craterFocus = new Vector2(0.5f, 0.56f);

    private static void BeginShade(FloorStyle s, float time)
    {
        shadeTime = time;
        shadeDetail = Mathf.Max(1, s.detail);
        if (s.pattern == FloorPattern.BattleCrater || s.pattern == FloorPattern.Craters) PrepareCraters(s);
        if (s.pattern == FloorPattern.DragonRadar) PrepareRadar(s);
    }

    private static bool IsOverlayPattern(FloorPattern p)
    {
        return p == FloorPattern.DragonRadar || p == FloorPattern.CircuitFlow || p == FloorPattern.WatchingEyes;
    }

    /// <summary>Big still textures (and the still part of overlay styles) are drawn over several frames.</summary>
    private static bool UsesSlicedFill(FloorStyle s)
    {
        if (s.pattern == FloorPattern.Classic || s.pattern == FloorPattern.Disco) return false;
        if (Mathf.Clamp(s.resolution, 8, 1024) < 384) return false;
        return s.animateFps <= 0f || IsOverlayPattern(s.pattern);
    }

    // ------------------------------------------------------------------
    // Time-sliced drawing of the still picture
    // ------------------------------------------------------------------

    private Coroutine fillRoutine;

    /// <summary>True while the active style's picture is still being drawn.</summary>
    public bool Drawing => fillRoutine != null;

    private void StartSlicedFill(Texture2D tex, FloorStyle s)
    {
        StopSlicedFill();
        if (TryLoadCache(tex, s)) return;   // drawn before (this build, this style): the finished picture is there at once, no blur, no wait
        FillPreview(tex, s);   // a soft version of the finished picture shows at once; the sharp one replaces it when it is done
        fillRoutine = StartCoroutine(FillSliced(tex, s));
        PixelDebug.Info("PixelFloor: drawing '" + s.name + "' (" + tex.width + " px) over several frames.", this);
    }

    // ------------------------------------------------------------------
    // Disk cache of the finished pictures (<persistentDataPath>/FloorCache, gzip, one file per style): a style drawn once loads at once the next time.
    // The key is the style's settings + the view aspect (fitted pictures) + a stamp of the build (in the Editor the compiled script assembly's time, so a code change redraws).
    // ------------------------------------------------------------------

    [Header("Floor cache")]
    [Tooltip("Keep the finished big floor pictures on disk so a style that was drawn before shows up at once (no blurry preview, no wait). Turn off to always draw them again.")]
    [SerializeField] private bool cacheFloors = true;

    private static string BuildStamp()
    {
#if UNITY_EDITOR
        try { return System.IO.File.GetLastWriteTimeUtc(typeof(PixelFloor).Assembly.Location).Ticks.ToString(); } catch (System.Exception) { return "editor"; }
#else
        return Application.version + "|" + Application.buildGUID;
#endif
    }

    private static string CacheFolder => System.IO.Path.Combine(Application.persistentDataPath, "FloorCache");

    private static string CacheFileStem(FloorStyle s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char ch in s.name) sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }

    private string CachePath(FloorStyle s, int n)
    {
        string key = JsonUtility.ToJson(s) + "|" + n + "|" + (s.fitToView ? viewAspect.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "") + "|" + BuildStamp();
        uint h = 2166136261u;   // FNV-1a
        foreach (char ch in key) { h ^= ch; h *= 16777619u; }
        return System.IO.Path.Combine(CacheFolder, CacheFileStem(s) + "_" + h.ToString("x8") + ".fcz");
    }

    private bool TryLoadCache(Texture2D tex, FloorStyle s)
    {
        if (!cacheFloors || tex == null) return false;
        try
        {
            int n = tex.width;
            string path = CachePath(s, n);
            if (!System.IO.File.Exists(path)) return false;
            byte[] raw = new byte[n * n * 4];
            using (System.IO.FileStream fs = System.IO.File.OpenRead(path))
            using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
            {
                int got = 0;
                while (got < raw.Length) { int r = gz.Read(raw, got, raw.Length - got); if (r <= 0) break; got += r; }
                if (got != raw.Length) return false;
            }
            Color32[] px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
            tex.SetPixels32(px);
            tex.Apply(tex.mipmapCount > 1);
            if (IsOverlayPattern(s.pattern)) BuildLayers(tex);
            PixelDebug.Info("PixelFloor: '" + s.name + "' loaded from the floor cache.", this);
            return true;
        }
        catch (System.Exception) { return false; }   // (a bad cache file just means the picture is drawn again)
    }

    private void SaveCache(Texture2D tex, FloorStyle s)
    {
        if (!cacheFloors || tex == null) return;
        try
        {
            int n = tex.width;
            Color32[] px = tex.GetPixels32();
            byte[] raw = new byte[px.Length * 4];
            for (int i = 0; i < px.Length; i++) { raw[i * 4] = px[i].r; raw[i * 4 + 1] = px[i].g; raw[i * 4 + 2] = px[i].b; raw[i * 4 + 3] = px[i].a; }
            string path = CachePath(s, n), folder = CacheFolder, stem = CacheFileStem(s);
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(folder);
                    foreach (string old in System.IO.Directory.GetFiles(folder, stem + "_*.fcz")) if (old != path) System.IO.File.Delete(old);   // (an older version of this style)
                    string tmp = path + ".tmp";
                    using (System.IO.FileStream fs = System.IO.File.Create(tmp))
                    using (System.IO.Compression.GZipStream gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Fastest)) gz.Write(raw, 0, raw.Length);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    System.IO.File.Move(tmp, path);
                }
                catch (System.Exception) { }
            });
        }
        catch (System.Exception) { }
    }

    private void StopSlicedFill()
    {
        if (fillRoutine != null) StopCoroutine(fillRoutine);
        fillRoutine = null;
    }

    private static void FillFlat(Texture2D tex, Color c)
    {
        int n = tex.width;
        Color32[] flat = new Color32[n * n];
        Color32 c32 = c;
        for (int i = 0; i < flat.Length; i++) flat[i] = c32;
        tex.SetPixels32(flat);
        tex.Apply(false);
    }

    /// <summary>Fills the texture at once with a soft, blurred version of the finished picture (the style sampled on a coarse grid and smoothly enlarged), so the floor never shows a flat colour while the sharp picture is drawn.</summary>
    private void FillPreview(Texture2D tex, FloorStyle s)
    {
        int n = tex.width, q = 96, cells = Mathf.Max(1, s.cells);
        ResumeShade(s, IsOverlayPattern(s.pattern));
        Color[] sm = new Color[q * q];
        for (int y = 0; y < q; y++)
        {
            for (int x = 0; x < q; x++)
            {
                float u = (x + 0.5f) / q, v = (y + 0.5f) / q;
                Color c = Shade(s, u, v, Mathf.Min(n - 1, (int)(u * n)), Mathf.Min(n - 1, (int)(v * n)), n, cells);
                c.a = 1f;
                sm[y * q + x] = c;
            }
        }
        int[] x0 = new int[n], x1 = new int[n];
        float[] tx = new float[n];
        for (int x = 0; x < n; x++)
        {
            float fx = (x + 0.5f) / n * q - 0.5f;
            x0[x] = Mathf.Clamp(Mathf.FloorToInt(fx), 0, q - 1);
            x1[x] = Mathf.Min(q - 1, x0[x] + 1);
            tx[x] = Mathf.Clamp01(fx - x0[x]);
        }
        Color32[] full = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            float fy = (y + 0.5f) / n * q - 0.5f;
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, q - 1), y1 = Mathf.Min(q - 1, y0 + 1);
            float ty = Mathf.Clamp01(fy - y0);
            int r0 = y0 * q, r1 = y1 * q;
            for (int x = 0; x < n; x++)
            {
                float a = tx[x];
                Color c00 = sm[r0 + x0[x]], c10 = sm[r0 + x1[x]], c01 = sm[r1 + x0[x]], c11 = sm[r1 + x1[x]];
                float r = Mathf.Lerp(Mathf.Lerp(c00.r, c10.r, a), Mathf.Lerp(c01.r, c11.r, a), ty);
                float g = Mathf.Lerp(Mathf.Lerp(c00.g, c10.g, a), Mathf.Lerp(c01.g, c11.g, a), ty);
                float b = Mathf.Lerp(Mathf.Lerp(c00.b, c10.b, a), Mathf.Lerp(c01.b, c11.b, a), ty);
                full[y * n + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
            }
        }
        tex.SetPixels32(full);
        tex.Apply(tex.mipmapCount > 1);   // (rebuild the mip levels too, else far-away ground reads blank until the sharp picture lands)
    }

    private IEnumerator FillSliced(Texture2D tex, FloorStyle s)
    {
        int n = tex.width;
#if !UNITY_WEBGL
        // The Aura floors draw their rows on every CPU core in the background (their pixel function is pure): a second or less instead of several seconds.
        if (s.pattern == FloorPattern.KiAura)
        {
            Color32[] fast = new Color32[n * n];
            int detail = Mathf.Max(1, s.detail);
            System.Threading.Tasks.Task job = System.Threading.Tasks.Task.Run(() =>
                System.Threading.Tasks.Parallel.For(0, n, y =>
                {
                    for (int x = 0; x < n; x++)
                    {
                        Color c = ShadeKiAura(s, (x + 0.5f) / n, (y + 0.5f) / n, x, y, detail);
                        c.a = 1f;
                        fast[y * n + x] = c;
                    }
                }));
            while (!job.IsCompleted) { yield return null; if (tex == null) yield break; }
            if (!job.IsFaulted)
            {
                tex.SetPixels32(fast);
                tex.Apply(tex.mipmapCount > 1);
                fillRoutine = null;
                SaveCache(tex, s);
                yield break;
            }
        }
#endif
        Color[] px = new Color[n * n];
        int cells = Mathf.Max(1, s.cells);
        bool overlay = IsOverlayPattern(s.pattern);
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        bool failed = false;
        ResumeShade(s, overlay);
        for (int y = 0; y < n; y++)
        {
            if (watch.ElapsedMilliseconds >= 7)
            {
                yield return null;
                if (tex == null) yield break;
                watch.Restart();
                ResumeShade(s, overlay);   // (something else may have drawn a texture meanwhile and changed the shared state)
            }
            if (!FillRow(px, s, y, n, cells)) { failed = true; break; }
        }
        ResumeShade(s, overlay);
        System.Random rng = new System.Random(s.seed);
        if (s.pattern == FloorPattern.Space) AddStars(px, n, s, rng);
        if (s.pattern == FloorPattern.PixelGrass) AddFlowers(px, n, s, rng);
        if (failed) { fillRoutine = null; yield break; }   // (the soft preview stays up)
        tex.SetPixels(px);
        tex.Apply(tex.mipmapCount > 1);
        fillRoutine = null;
        SaveCache(tex, s);
        PixelDebug.Info("PixelFloor: finished drawing '" + s.name + "' in " + (watch.ElapsedMilliseconds / 1000f).ToString("0.0") + " s of work.", this);
        if (overlay) BuildLayers(tex);
    }

    private bool FillRow(Color[] px, FloorStyle s, int y, int n, int cells)
    {
        try
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                Color c = Shade(s, u, v, x, y, n, cells);
                c.a = 1f;
                px[y * n + x] = c;
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Floor '" + s.name + "' failed to draw at row " + y + ": " + e);
            return false;
        }
    }

    private void ResumeShade(FloorStyle s, bool overlay)
    {
        shadeAspect = s.fitToView ? viewAspect : 1f;
        BeginShade(s, 0f);
        shadeBase = overlay;
        if (s.pattern == FloorPattern.CircuitFlow) { sparkCount = 0; sparkNear = null; }
    }

    // ------------------------------------------------------------------
    // Overlay frames: only the moving parts are repainted
    // ------------------------------------------------------------------

    private static Color32[] layerBase32, layerCur32;
    private static int layerN;
    private static readonly List<RectInt> layerDirty = new List<RectInt>();
    private static int[] eyeLevel;

    private static void BuildLayers(Texture2D tex)
    {
        layerBase32 = tex.GetPixels32();
        layerCur32 = (Color32[])layerBase32.Clone();
        layerN = tex.width;
        layerDirty.Clear();
        eyeLevel = null;
    }

    private static void ClearLayers()
    {
        layerBase32 = layerCur32 = null;
        layerN = 0;
        layerDirty.Clear();
        eyeLevel = null;
    }

    private static void RestoreDirty(int n)
    {
        for (int i = 0; i < layerDirty.Count; i++) RestoreRect(layerDirty[i], n);
        layerDirty.Clear();
    }

    private static void RestoreRect(RectInt r, int n)
    {
        for (int y = r.y; y < r.y + r.height; y++)
        {
            int row = y * n;
            for (int x = r.x; x < r.x + r.width; x++) layerCur32[row + x] = layerBase32[row + x];
        }
    }

    private static RectInt ClampRect(int x0, int y0, int x1, int y1, int n)
    {
        x0 = Mathf.Clamp(x0, 0, n); x1 = Mathf.Clamp(x1, 0, n);
        y0 = Mathf.Clamp(y0, 0, n); y1 = Mathf.Clamp(y1, 0, n);
        return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
    }

    private static void AddLight(int i, float r, float g, float b)
    {
        Color32 p = layerCur32[i];
        int rr = p.r + (int)(r * 255f), gg = p.g + (int)(g * 255f), bb = p.b + (int)(b * 255f);
        layerCur32[i] = new Color32((byte)Mathf.Clamp(rr, 0, 255), (byte)Mathf.Clamp(gg, 0, 255), (byte)Mathf.Clamp(bb, 0, 255), 255);
    }

    private static void OverlayFrame(Texture2D tex, FloorStyle s, float time)
    {
        int n = layerN;
        shadeTime = time;
        switch (s.pattern)
        {
            case FloorPattern.DragonRadar: OverlayRadar(s, n, time); break;
            case FloorPattern.CircuitFlow: OverlayCircuit(s, n, time); break;
            case FloorPattern.WatchingEyes: OverlayEyes(s, n, time); break;
        }
        tex.SetPixels32(layerCur32);
        tex.Apply(false);
    }

    // ------------------------------------------------------------------
    // Craters
    // ------------------------------------------------------------------

    private struct CraterInfo { public float x, y, r, depth; }

    private static CraterInfo[] craters;
    private static int craterKey;

    private static void PrepareCraters(FloorStyle s)
    {
        int key = s.seed * 977 + s.cells * 13 + s.variant * 7 + Mathf.RoundToInt(craterFocus.x * 100f) * 31 + Mathf.RoundToInt(craterFocus.y * 100f);
        if (craters != null && craterKey == key) return;
        craterKey = key;
        List<CraterInfo> list = new List<CraterInfo>();
        // One big crater where the camera looks, so it is the first thing you see.
        float bigR = 0.095f;
        list.Add(new CraterInfo { x = craterFocus.x, y = craterFocus.y, r = bigR, depth = bigR * 0.5f });
        // A few medium craters, never touching the others.
        int count = Mathf.Max(3, s.cells);
        System.Random rng = new System.Random(s.seed * 17 + 5);
        for (int i = 0, tries = 0; i < count && tries < 400; tries++)
        {
            float r = 0.014f + 0.036f * (float)rng.NextDouble();
            float x = 0.06f + 0.88f * (float)rng.NextDouble(), y = 0.06f + 0.88f * (float)rng.NextDouble();
            bool ok = true;
            foreach (CraterInfo o in list)
            {
                float dx = x - o.x, dy = y - o.y;
                if (Mathf.Sqrt(dx * dx + dy * dy) < (r + o.r) * 1.35f + 0.012f) { ok = false; break; }
            }
            if (!ok) continue;
            list.Add(new CraterInfo { x = x, y = y, r = r, depth = r * 0.5f });
            i++;
        }
        // Lots of small pits.
        for (int i = 0, tries = 0; i < 60 && tries < 300; tries++)
        {
            float r = 0.003f + 0.007f * (float)rng.NextDouble();
            float x = (float)rng.NextDouble(), y = (float)rng.NextDouble();
            bool ok = true;
            foreach (CraterInfo o in list)
            {
                float dx = x - o.x, dy = y - o.y;
                if (Mathf.Sqrt(dx * dx + dy * dy) < (r + o.r) * 1.2f) { ok = false; break; }
            }
            if (!ok) continue;
            list.Add(new CraterInfo { x = x, y = y, r = r, depth = r * 0.45f });
            i++;
        }
        craters = list.ToArray();
    }

    /// <summary>Ground height at a point (negative inside a crater bowl, a raised rim round it, a low apron of rubble outside).</summary>
    private static float CraterHeight(float u, float v, out float bowl, out float scorch)
    {
        float h = 0f;
        bowl = 0f;
        scorch = 0f;
        for (int i = 0; i < craters.Length; i++)
        {
            CraterInfo c = craters[i];
            float dx = u - c.x, dy = v - c.y;
            float far = c.r * 3f;
            if (dx > far || dx < -far || dy > far || dy < -far) continue;
            float t = Mathf.Sqrt(dx * dx + dy * dy) / c.r;
            float rd = t - 1f;
            if (t < 1f)
            {
                float q = 1f - t * t;
                h -= c.depth * q;
                if (q > bowl) bowl = q;
            }
            h += c.depth * 0.32f * Mathf.Exp(-rd * rd * 22f);
            if (t > 1f)
            {
                h += c.depth * 0.1f * Mathf.Exp(-rd * 1.8f);
                float sc = Mathf.Exp(-rd * 1.2f) * (i == 0 ? 1f : 0.7f);
                if (sc > scorch) scorch = sc;
            }
            else scorch = 1f;
        }
        return h;
    }

    private static float CraterHeightOnly(float u, float v)
    {
        return CraterHeight(u, v, out _, out _);
    }

    private static Color ShadeCraterField(FloorStyle s, float u, float v, int x, int y, int n)
    {
        int mode = s.pattern == FloorPattern.BattleCrater ? 0 : s.variant;   // 0 battle, 1 moon, 2 red planet
        int D = shadeDetail;
        Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 4 * D, 4, s.seed));
        c *= 0.9f + 0.2f * Hash(x, y, s.seed);

        float h = CraterHeight(u, v, out float bowl, out float scorch);
        // Light from the upper left: ground that faces away from it is darker.
        float e = 1.4f / n;
        const float lx = -0.6f, ly = 0.8f;
        float slope = (CraterHeightOnly(u + lx * e, v + ly * e) - CraterHeightOnly(u - lx * e, v - ly * e)) / (2f * e);
        float shade = 1f - Mathf.Clamp(slope * 0.45f, -0.7f, 0.7f);
        c *= shade;
        c = Color.Lerp(c, s.colorC * shade, Mathf.Clamp01(bowl * 1.1f) * 0.75f);   // the bowls are darker

        if (mode == 0)
        {
            c = Color.Lerp(c, s.colorC * 0.9f, Mathf.Clamp01(scorch) * 0.4f);      // burnt ground round the craters
            // Cracks in the scorched earth, a few of them glowing.
            Voronoi(u, v, 30, s.seed + 5, out float g1, out float g2, out int cid);
            float crack = (g2 - g1) * 30f;
            if (crack < 0.05f && bowl < 0.02f)
            {
                float k = 1f - crack / 0.05f;
                c = Color.Lerp(c, new Color(0.04f, 0.03f, 0.03f), k * 0.8f);
                if (HashId(cid, s.seed + 9) > 0.9f) c = Color.Lerp(c, new Color(1f, 0.45f, 0.1f), k * 0.9f);
            }
        }
        else if (mode == 1 && craters != null && craters.Length > 0)
        {
            // Bright rays thrown out of the big crater.
            CraterInfo big = craters[0];
            float dx = u - big.x, dy = v - big.y;
            float t = Mathf.Sqrt(dx * dx + dy * dy) / big.r;
            if (t > 1.15f)
            {
                float ang = Mathf.Atan2(dy, dx);
                float ray = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ang * 11f + Hash(Mathf.FloorToInt(ang * 3f), 3, s.seed) * 6f), 7f);
                c += Color.white * (ray * 0.16f / (1f + (t - 1f) * 0.8f));
            }
        }

        // Scattered small rocks.
        if (Hash(x / 3, y / 3, s.seed + 11) > 0.992f) c = Color.Lerp(c, s.colorB * 1.25f * shade, 0.8f);
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Radar: a still green radar screen with glowing orange blips
    // ------------------------------------------------------------------

    private struct RadarDot { public float x, y, phase, rate; }

    private static RadarDot[] radarDots;
    private static int radarKey;

    private static void PrepareRadar(FloorStyle s)
    {
        int key = s.seed * 31 + 7;
        if (radarDots != null && radarKey == key) return;
        radarKey = key;
        radarDots = new RadarDot[30];
        for (int i = 0; i < radarDots.Length; i++)
        {
            radarDots[i] = new RadarDot
            {
                x = 0.05f + 0.9f * Hash(i, 1, s.seed), y = 0.05f + 0.9f * Hash(i, 2, s.seed),
                phase = Hash(i, 3, s.seed) * Mathf.PI * 2f, rate = 0.6f + Hash(i, 4, s.seed) * 1.3f,
            };
        }
    }

    private static Color ShadeRadar(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        Color c = s.colorA * (0.9f + 0.15f * Fbm(u, v, 4 * shadeDetail, 2, s.seed));
        float cu = Frac(u * cells), cv = Frac(v * cells);
        float line = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * n / cells;
        c = Color.Lerp(c, s.colorB * 0.7f, Mathf.Clamp01(1.2f - line) * 0.45f);
        float ax = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * n;                // the cross-hair through the middle
        c = Color.Lerp(c, s.colorB, Mathf.Clamp01(1.5f - ax) * 0.85f);
        if ((y % 3) == 0) c *= 0.88f;                                               // screen lines
        c.a = 1f;
        if (!shadeBase && radarDots != null)
        {
            for (int i = 0; i < radarDots.Length; i++)
            {
                float ddx = (u - radarDots[i].x) * n, ddy = (v - radarDots[i].y) * n;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d < 3f) c = Color.Lerp(s.colorC, new Color(1f, 0.95f, 0.6f), 0.5f);
                else if (d < 14f) c = Color.Lerp(c, s.colorC, (1f - (d - 3f) / 11f) * 0.45f);
            }
        }
        return c;
    }

    private static void OverlayRadar(FloorStyle s, int n, float t)
    {
        if (radarDots == null) PrepareRadar(s);
        RestoreDirty(n);
        const int R = 24;
        for (int i = 0; i < radarDots.Length; i++)
        {
            RadarDot d = radarDots[i];
            float cx = d.x * n, cy = d.y * n;
            RectInt rect = ClampRect(Mathf.FloorToInt(cx) - R, Mathf.FloorToInt(cy) - R, Mathf.FloorToInt(cx) + R + 1, Mathf.FloorToInt(cy) + R + 1, n);
            layerDirty.Add(rect);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * d.rate + d.phase);
            pulse *= pulse;
            float sigma = 5f + 5f * pulse;
            for (int y = rect.y; y < rect.y + rect.height; y++)
            {
                for (int x = rect.x; x < rect.x + rect.width; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float dist2 = dx * dx + dy * dy;
                    float dist = Mathf.Sqrt(dist2);
                    float glow = Mathf.Exp(-dist2 / (2f * sigma * sigma)) * (0.3f + 0.7f * pulse);
                            float core = dist < 3f ? 1f : (dist < 4.5f ? 1f - (dist - 3f) / 1.5f : 0f);
                    float k = glow;
                    int idx = y * n + x;
                    AddLight(idx, s.colorC.r * k, s.colorC.g * k, s.colorC.b * k);
                    if (core > 0f)
                    {
                        float w = core * (0.55f + 0.45f * pulse);
                        AddLight(idx, 1f * w, 0.92f * w, 0.55f * w);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Circuit Flow: the sparks are painted over the still circuit board
    // ------------------------------------------------------------------

    private static void OverlayCircuit(FloorStyle s, int n, float t)
    {
        RestoreDirty(n);
        int cells = Mathf.Max(2, s.cells);
        PrepareSparks(s, t);
        float cell = n / (float)cells;
        float R = cell * 0.75f;
        for (int k = 0; k < sparkCount; k++)
        {
            float w = k % SparkTrail == 0 ? 1f : Mathf.Pow(0.72f, k % SparkTrail);
            float cx = sparkX[k] * cell, cy = sparkY[k] * cell;
            RectInt rect = ClampRect(Mathf.FloorToInt(cx - R), Mathf.FloorToInt(cy - R), Mathf.CeilToInt(cx + R) + 1, Mathf.CeilToInt(cy + R) + 1, n);
            if (rect.width <= 0 || rect.height <= 0) continue;
            layerDirty.Add(rect);
            for (int y = rect.y; y < rect.y + rect.height; y++)
            {
                for (int x = rect.x; x < rect.x + rect.width; x++)
                {
                    float dx = (x + 0.5f - cx) / cell, dy = (y + 0.5f - cy) / cell;
                    float d2 = dx * dx + dy * dy;
                    if (d2 > 0.56f) continue;
                    float spark = w * Mathf.Exp(-d2 / 0.012f), glow = w * Mathf.Exp(-d2 / 0.18f);
                    float hot = spark * 1.2f;
                    AddLight(y * n + x, hot + s.colorC.r * glow * 0.55f, hot + s.colorC.g * glow * 0.55f, hot + s.colorC.b * glow * 0.55f);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Watching Eyes: dark ground, pairs of eyes that open and close
    // ------------------------------------------------------------------

    private static bool EyeCell(int wi, int wj, int seed, out float cx, out float cy, out float h)
    {
        h = Hash(wi, wj, seed);
        cx = 0.5f + (Hash(wi, wj, seed + 1) - 0.5f) * 0.3f;
        cy = 0.5f + (Hash(wi, wj, seed + 2) - 0.5f) * 0.3f;
        return h > 0.5f;
    }

    private static float EyeOpen(float h, float t)
    {
        float ph = Frac(t * 0.07f + h * 3f);
        return Mathf.Clamp01(Mathf.Min(ph / 0.04f, (0.86f - ph) / 0.04f));
    }

    private static void EyeLight(ref Color c, float fu, float fv, float cx, float cy, float open, Color glow)
    {
        if (open <= 0f) return;
        float best = 99f;
        for (int side = -1; side <= 1; side += 2)
        {
            float ex = cx + side * 0.15f;
            float dx = (fu - ex) / 0.1f, dy = (fv - cy) / (0.06f * open + 0.001f);
            float d = Mathf.Sqrt(dx * dx + dy * dy * (1f + Mathf.Abs(dx) * 0.8f));   // almond shape: the vertical radius shrinks towards the corners
            if (d < best) best = d;
            if (d < 1f && Mathf.Abs(fu - ex) < 0.012f) best = 0.99f;                // the slit pupil
        }
        float core = 1f - Sstep(0.65f, 1f, best);
        float halo = Mathf.Exp(-best * best * 1.6f) * 0.4f;
        c += glow * (core + halo);
    }

    private static Color ShadeEyes(FloorStyle s, float u, float v, int cells)
    {
        Color c = s.colorA * (0.7f + 0.6f * Fbm(u, v, 4 * shadeDetail, 3, s.seed));
        if (!shadeBase)
        {
            float gu = u * cells, gv = v * cells;
            int ci = Mathf.FloorToInt(gu), cj = Mathf.FloorToInt(gv);
            if (EyeCell(Wrap(ci, cells), Wrap(cj, cells), s.seed, out float cx, out float cy, out float h))
                EyeLight(ref c, gu - ci, gv - cj, cx, cy, EyeOpen(h, shadeTime), s.colorC);
        }
        c.a = 1f;
        return c;
    }

    private static void OverlayEyes(FloorStyle s, int n, float t)
    {
        int cells = Mathf.Max(2, s.cells);
        if (eyeLevel == null || eyeLevel.Length != cells * cells)
        {
            eyeLevel = new int[cells * cells];
            for (int i = 0; i < eyeLevel.Length; i++) eyeLevel[i] = -1;
        }
        float cs = n / (float)cells;
        for (int j = 0; j < cells; j++)
        {
            for (int i = 0; i < cells; i++)
            {
                if (!EyeCell(i, j, s.seed, out float cx, out float cy, out float h)) continue;
                float open = EyeOpen(h, t);
                int level = Mathf.RoundToInt(open * 5f);
                int idx = j * cells + i;
                if (eyeLevel[idx] == level) continue;
                eyeLevel[idx] = level;
                int x0 = Mathf.FloorToInt(i * cs), x1 = Mathf.Min(n, Mathf.FloorToInt((i + 1) * cs));
                int y0 = Mathf.FloorToInt(j * cs), y1 = Mathf.Min(n, Mathf.FloorToInt((j + 1) * cs));
                float lvl = level / 5f;
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        int p = y * n + x;
                        Color c = layerBase32[p];
                        EyeLight(ref c, (x + 0.5f) / cs - i, (y + 0.5f) / cs - j, cx, cy, lvl, s.colorC);
                        layerCur32[p] = c;
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // ------------------------------------------------------------------
}

public partial class PixelFloor
{
    /// <summary>The big crater goes where the camera looks: find that spot on the floor and turn it into texture coordinates.</summary>
    private void SetCraterFocus(FloorStyle style)
    {
        craterFocus = new Vector2(0.5f, 0.56f);
        if (style.pattern != FloorPattern.Craters && style.pattern != FloorPattern.BattleCrater) return;
        if (floorRenderer == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Bounds b = floorRenderer.bounds;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, b.max.y, 0f));
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.64f, 0f));
        if (!plane.Raycast(ray, out float dist)) return;
        Vector3 hit = ray.GetPoint(dist);
        float tile = EffectiveTileSize(style);
        float u = 0.5f + (hit.x - b.center.x) / tile, v = 0.5f + (hit.z - b.center.z) / tile;
        craterFocus = new Vector2(Mathf.Clamp(u, 0.3f, 0.7f), Mathf.Clamp(v, 0.3f, 0.7f));
    }

    private float hauntedClock;

    /// <summary>Haunted Tiles: the lights flicker now and then. The picture is drawn once; the flicker dims the whole material.</summary>
    private void UpdateHauntedFlicker()
    {
        if (activeMaterial == null) return;
        hauntedClock += Time.unscaledDeltaTime;
        float f = Hash(Mathf.FloorToInt(hauntedClock * 9f), 1, 77) > 0.93f ? 0.45f : 1f;
        f *= 0.9f + 0.1f * Mathf.Sin(hauntedClock * 2f);
        Color c = new Color(f, f, f, 1f);
        SetColor(activeMaterial, "_BaseColor", c);
        SetColor(activeMaterial, "_Color", c);
    }
}
