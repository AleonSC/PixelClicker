using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floor styles: swaps the look of the scene's floor for one of several textures drawn in code at runtime (no art
/// assets needed). Picked in the pause menu's Settings ("Floor style"), remembered in PlayerPrefs. "Classic" puts
/// the floor's own material back. The floor is the renderer set in the Inspector, else the collider straight below
/// the cube, else an object called Floor / Ground / Plane. Some styles move (scroll, pulse, disco tiles).
/// Added by PixelClicker.Awake if missing.
/// </summary>
public class PixelFloor : MonoBehaviour
{
    public enum FloorPattern
    {
        Classic,      // the floor's own material, untouched
        NeonGrid,     // glowing grid lines on a dark ground
        Checker,      // two-colour checkerboard
        PixelGrass,   // pixel-art grass with little flowers
        WoodPlanks,   // pixel-art planks with grain and seams
        Cobblestone,  // rounded stones with dark gaps
        Marble,       // smooth, glossy stone with veins
        Lava,         // dark crust with glowing cracks
        Space,        // stars and faint nebula clouds
        Ice,          // pale glossy ice with white cracks
        Disco         // light-up tiles that change colour to a beat
    }

    [Serializable]
    public class FloorStyle
    {
        [Tooltip("Name shown in the Settings menu (also what is saved).")]
        public string name = "Style";

        [Tooltip("Which pattern is drawn.")]
        public FloorPattern pattern = FloorPattern.Classic;

        [Tooltip("Main colour (background / base).")]
        public Color colorA = Color.white;

        [Tooltip("Second colour (lines, cracks, veins, second checker square...).")]
        public Color colorB = Color.gray;

        [Tooltip("Accent colour (flowers, highlights, nebula...).")]
        public Color colorC = Color.black;

        [Tooltip("Texture size in texels. Small sizes + Pixelated look like pixel art.")]
        public int resolution = 64;

        [Tooltip("Pixel-art look (no smoothing between texels).")]
        public bool pixelated = true;

        [Tooltip("How many world units one copy of the texture covers on the floor.")]
        public float tileWorldSize = 4f;

        [Range(0f, 1f)]
        [Tooltip("How shiny the floor is.")]
        public float smoothness = 0.2f;

        [Tooltip("Glow strength (the texture lights itself). 0 = no glow.")]
        public float glow = 0f;

        [Tooltip("Pulse speed of the glow (cycles per second). 0 = steady.")]
        public float pulseSpeed = 0f;

        [Range(0f, 1f)]
        [Tooltip("How much the glow pulses (0 = not at all, 1 = fades to nothing).")]
        public float pulseAmount = 0f;

        [Tooltip("Texture scroll speed (tiles per second). (0, 0) = still.")]
        public Vector2 scrollSpeed = Vector2.zero;

        [Tooltip("Pattern size: grid cells, stones, planks or tiles per texture (depends on the pattern).")]
        public int cells = 8;

        [Tooltip("Random seed of the pattern. Change it for a different variation.")]
        public int seed = 1;

        [Tooltip("Disco only: seconds between colour changes.")]
        public float beatSeconds = 0.5f;
    }

    [Header("Floor")]
    [Tooltip("The floor's renderer. Empty = found automatically (collider below the cube, else an object named like one in Floor Names).")]
    [SerializeField] private Renderer floorRenderer;

    [Tooltip("Object names tried when no floor renderer is set and nothing is found below the cube.")]
    [SerializeField] private string[] floorNames = { "Floor", "Ground", "Plane" };

    [Header("Styles")]
    [Tooltip("Add the built-in styles that are missing from the list (by name).")]
    [SerializeField] private bool addDefaultStyles = true;

    [Tooltip("The styles you can pick in Settings, in order. The first Classic entry restores the floor's own look.")]
    [SerializeField] private List<FloorStyle> styles = new List<FloorStyle>();

    [Tooltip("Style used on a first launch (before the player picks one).")]
    [SerializeField] private string defaultStyle = "Classic";

    /// <summary>The floor style component in the scene (null until it wakes).</summary>
    public static PixelFloor Instance { get; private set; }

    /// <summary>Raised when the floor style changes (passes the new index).</summary>
    public static event Action<int> StyleChanged;

    private const string PrefStyle = "PixelClicker.Setting.FloorStyle";

    private Material[] originalMaterials;
    private Material activeMaterial;
    private Texture2D activeTexture;
    private readonly Dictionary<int, Texture2D> previews = new Dictionary<int, Texture2D>();
    private int current = -1;
    private float beatTimer;
    private int beatStep;
    private Vector2 baseTiling = Vector2.one;
    private Vector2 offset;

    public int StyleCount => styles.Count;
    public int Current => current;
    public bool HasFloor => floorRenderer != null;
    public string StyleName(int i) => i >= 0 && i < styles.Count ? styles[i].name : "";

    private void Awake()
    {
        Instance = this;
        if (addDefaultStyles) EnsureDefaultStyles();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseActive();
        foreach (Texture2D t in previews.Values) if (t != null) Destroy(t);
        previews.Clear();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!addDefaultStyles) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            EnsureDefaultStyles();
        };
    }

    [ContextMenu("Add Missing Default Styles")]
    private void AddMissingDefaultsMenu() => EnsureDefaultStyles();
#endif

    private void Start()
    {
        if (floorRenderer == null) floorRenderer = FindFloor();
        if (floorRenderer == null)
        {
            Debug.LogWarning("PixelFloor: no floor found. Assign the floor renderer on the Pixel Floor component to use floor styles.");
            return;
        }
        originalMaterials = floorRenderer.sharedMaterials;

        string saved = PlayerPrefs.GetString(PrefStyle, defaultStyle);
        int index = styles.FindIndex(s => s.name == saved);
        SetStyle(index >= 0 ? index : 0);
    }

    // ------------------------------------------------------------------
    // Finding the floor
    // ------------------------------------------------------------------

    private Renderer FindFloor()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker != null)
        {
            // The first solid, non-moving collider straight below the cube.
            RaycastHit[] hits = Physics.RaycastAll(clicker.transform.position + Vector3.up * 0.5f, Vector3.down, 500f,
                                                   ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(clicker.transform)) continue;
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;
                Renderer r = hit.collider.GetComponent<Renderer>();
                if (r != null) return r;
            }
        }

        foreach (string n in floorNames)
        {
            if (string.IsNullOrEmpty(n)) continue;
            GameObject go = GameObject.Find(n);
            Renderer r = go != null ? go.GetComponent<Renderer>() : null;
            if (r != null) return r;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // Picking a style
    // ------------------------------------------------------------------

    /// <summary>Applies style i to the floor and remembers it.</summary>
    public void SetStyle(int index)
    {
        if (styles.Count == 0) return;
        index = ((index % styles.Count) + styles.Count) % styles.Count;
        current = index;
        PlayerPrefs.SetString(PrefStyle, styles[index].name);
        Apply(styles[index]);
        StyleChanged?.Invoke(index);
    }

    /// <summary>Steps to the next (+1) or previous (-1) style.</summary>
    public void Step(int direction) => SetStyle((current < 0 ? 0 : current) + direction);

    private void Apply(FloorStyle style)
    {
        if (floorRenderer == null) return;
        ReleaseActive();
        offset = Vector2.zero;
        beatTimer = 0f;

        if (style.pattern == FloorPattern.Classic)
        {
            floorRenderer.sharedMaterials = originalMaterials;
            return;
        }

        Material source = originalMaterials != null && originalMaterials.Length > 0 ? originalMaterials[0] : null;
        if (source == null)
        {
            PixelClicker clicker = PixelFind.First<PixelClicker>();
            source = clicker != null ? clicker.CreateVisualMaterial(Color.white, false) : null;
        }
        if (source == null) return;

        activeTexture = BuildTexture(style, 0);
        activeMaterial = new Material(source) { name = "Floor (" + style.name + ")" };
        Material m = activeMaterial;
        SetColor(m, "_BaseColor", Color.white);
        SetColor(m, "_Color", Color.white);
        SetTexture(m, "_BaseMap", activeTexture);
        SetTexture(m, "_MainTex", activeTexture);
        SetTexture(m, "_BumpMap", null);
        SetTexture(m, "_MetallicGlossMap", null);
        SetTexture(m, "_OcclusionMap", null);
        SetFloat(m, "_Metallic", 0f);
        SetFloat(m, "_Smoothness", style.smoothness);
        SetFloat(m, "_Glossiness", style.smoothness);

        if (style.glow > 0f && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SetTexture(m, "_EmissionMap", activeTexture);
            m.SetColor("_EmissionColor", Color.white * style.glow);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
        }

        // Tile by world size so the pattern has the same scale on any floor.
        Vector3 size = floorRenderer.bounds.size;
        float tile = Mathf.Max(0.1f, style.tileWorldSize);
        baseTiling = new Vector2(Mathf.Max(1f, size.x / tile), Mathf.Max(1f, size.z / tile));
        SetTiling(m, baseTiling, offset);

        Material[] mats = new Material[Mathf.Max(1, originalMaterials != null ? originalMaterials.Length : 1)];
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        floorRenderer.sharedMaterials = mats;
    }

    private void ReleaseActive()
    {
        if (activeMaterial != null) Destroy(activeMaterial);
        if (activeTexture != null) Destroy(activeTexture);
        activeMaterial = null;
        activeTexture = null;
    }

    private void Update()
    {
        if (activeMaterial == null || current < 0 || current >= styles.Count) return;
        FloorStyle style = styles[current];
        float dt = Time.deltaTime; // stops with the game (pause, Time Stop)

        if (style.scrollSpeed != Vector2.zero)
        {
            offset += style.scrollSpeed * dt;
            offset.x -= Mathf.Floor(offset.x);
            offset.y -= Mathf.Floor(offset.y);
            SetTiling(activeMaterial, baseTiling, offset);
        }

        if (style.glow > 0f && style.pulseSpeed > 0f && activeMaterial.HasProperty("_EmissionColor"))
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * style.pulseSpeed * Mathf.PI * 2f);
            activeMaterial.SetColor("_EmissionColor", Color.white * style.glow * (1f - style.pulseAmount * wave));
        }

        if (style.pattern == FloorPattern.Disco && dt > 0f)
        {
            beatTimer += dt;
            if (beatTimer >= Mathf.Max(0.05f, style.beatSeconds))
            {
                beatTimer = 0f;
                beatStep++;
                DrawDisco(activeTexture, style, beatStep);
            }
        }
    }

    // ------------------------------------------------------------------
    // Previews (for the Settings menu)
    // ------------------------------------------------------------------

    /// <summary>A small picture of style i for the menu (null for Classic: use <see cref="ClassicColor"/>).</summary>
    public Texture PreviewTexture(int index)
    {
        if (index < 0 || index >= styles.Count) return null;
        if (styles[index].pattern == FloorPattern.Classic)
            return originalMaterials != null && originalMaterials.Length > 0 && originalMaterials[0] != null
                ? originalMaterials[0].mainTexture : null;
        if (!previews.TryGetValue(index, out Texture2D tex) || tex == null)
        {
            tex = BuildTexture(styles[index], 0);
            previews[index] = tex;
        }
        return tex;
    }

    /// <summary>Tint for the preview of style i (the floor's own colour for Classic, else white).</summary>
    public Color PreviewColor(int index)
    {
        if (index >= 0 && index < styles.Count && styles[index].pattern == FloorPattern.Classic)
        {
            Material m = originalMaterials != null && originalMaterials.Length > 0 ? originalMaterials[0] : null;
            if (m != null && m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m != null && m.HasProperty("_Color")) return m.GetColor("_Color");
            return new Color(0.6f, 0.6f, 0.6f, 1f);
        }
        return Color.white;
    }

    // ------------------------------------------------------------------
    // Drawing the textures
    // ------------------------------------------------------------------

    private static Texture2D BuildTexture(FloorStyle s, int step)
    {
        int n = Mathf.Clamp(s.resolution, 8, 1024);
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "Floor " + s.name,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = s.pixelated ? FilterMode.Point : FilterMode.Bilinear,
            anisoLevel = 4
        };
        if (s.pattern == FloorPattern.Disco) { DrawDisco(tex, s, step); return tex; }

        Color[] px = new Color[n * n];
        int cells = Mathf.Max(1, s.cells);
        System.Random rng = new System.Random(s.seed);

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                Color c = Shade(s, u, v, x, y, n, cells);
                c.a = 1f;
                px[y * n + x] = c;
            }
        }

        // Scattered extras that are easier to place than to shade per texel.
        if (s.pattern == FloorPattern.Space) AddStars(px, n, s, rng);
        if (s.pattern == FloorPattern.PixelGrass) AddFlowers(px, n, s, rng);

        tex.SetPixels(px);
        tex.Apply(false);
        return tex;
    }

    private static Color Shade(FloorStyle s, float u, float v, int x, int y, int n, int cells)
    {
        switch (s.pattern)
        {
            case FloorPattern.NeonGrid:
            {
                float cu = Frac(u * cells), cv = Frac(v * cells);
                float du = Mathf.Min(cu, 1f - cu), dv = Mathf.Min(cv, 1f - cv);
                float line = Mathf.Min(du, dv) * n / cells;               // texels from the nearest line
                float core = Mathf.Clamp01(1.5f - line);                  // sharp line
                float halo = Mathf.Exp(-line * 0.45f) * 0.55f;            // soft glow around it
                Color c = s.colorA * (0.8f + 0.2f * Fbm(u, v, 4, 3, s.seed));
                c = Color.Lerp(c, s.colorB, Mathf.Clamp01(core + halo));
                // Brighter dots where lines cross.
                float cross = Mathf.Clamp01(2.5f - Mathf.Max(du, dv) * n / cells);
                return Color.Lerp(c, s.colorC, cross * 0.8f);
            }

            case FloorPattern.Checker:
            {
                int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
                Color c = ((cx + cy) & 1) == 0 ? s.colorA : s.colorB;
                // A light bevel on the top / left edge of each square for a tiled look.
                float cu = Frac(u * cells), cv = Frac(v * cells);
                if (cu < 0.08f || cv > 0.92f) c = Color.Lerp(c, Color.white, 0.12f);
                else if (cu > 0.92f || cv < 0.08f) c = Color.Lerp(c, Color.black, 0.15f);
                return c;
            }

            case FloorPattern.PixelGrass:
            {
                float h = Hash(x, y, s.seed);
                float patch = Fbm(u, v, 4, 3, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, patch);
                if (h > 0.86f) c = Color.Lerp(c, Color.white, 0.18f); // light blades
                else if (h < 0.12f) c = Color.Lerp(c, Color.black, 0.2f); // shadows
                return c;
            }

            case FloorPattern.WoodPlanks:
            {
                int rows = cells;
                int row = Mathf.FloorToInt(v * rows);
                float rv = Frac(v * rows);
                // Two planks per row; each row is shifted so the seams don't line up.
                float shift = Hash(row, 7, s.seed);
                float pu = Frac(u * 2f + shift);
                int plank = Mathf.FloorToInt(u * 2f + shift) + row * 3;
                float tone = 0.85f + 0.3f * Hash(plank, 3, s.seed);
                float grain = Mathf.Sin((rv * 6f + Fbm(u, v, 2, 4, s.seed + plank) * 3f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(s.colorA, s.colorB, grain * 0.6f) * tone;
                float texel = 1f / n * rows;
                if (rv < texel || pu < 1f / n * 2f) c = s.colorC;     // gaps between planks
                else if (rv > 1f - texel) c = Color.Lerp(c, Color.white, 0.15f);
                // A knot now and then.
                float knot = Hash(plank, 11, s.seed);
                if (knot > 0.7f)
                {
                    float kd = Mathf.Abs(pu - knot) * 3f + Mathf.Abs(rv - 0.5f);
                    if (kd < 0.18f) c = Color.Lerp(c, s.colorC, 0.6f);
                }
                c.a = 1f;
                return c;
            }

            case FloorPattern.Cobblestone:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = f2 - f1;
                float tone = 0.75f + 0.4f * HashId(id, s.seed);
                Color stone = Color.Lerp(s.colorA, s.colorB, HashId(id, s.seed + 5)) * tone;
                stone *= 0.85f + 0.3f * Fbm(u, v, 8, 3, s.seed);
                float gap = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge * cells - 0.06f) / 0.12f));
                // Rounded: darker towards the stone's edge.
                float round = Mathf.Clamp01(edge * cells * 2.2f);
                Color c = Color.Lerp(s.colorC, stone * (0.7f + 0.3f * round), gap);
                c.a = 1f;
                return c;
            }

            case FloorPattern.Marble:
            {
                float turb = Fbm(u, v, 4, 5, s.seed);
                float veins = Mathf.Abs(Mathf.Sin((u * cells + v * cells * 0.5f + turb * 4f) * Mathf.PI));
                float fine = Mathf.Abs(Mathf.Sin((v * cells * 2f - u * cells + Fbm(u, v, 8, 4, s.seed + 3) * 5f) * Mathf.PI));
                Color c = s.colorA;
                c = Color.Lerp(s.colorB, c, Mathf.Pow(veins, 0.35f));
                c = Color.Lerp(s.colorC, c, Mathf.Clamp01(Mathf.Pow(fine, 0.18f) + 0.15f));
                c.a = 1f;
                return c;
            }

            case FloorPattern.Lava:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float heat = Mathf.Clamp01(1f - edge / 0.35f);
                heat = heat * heat;
                float n2 = Fbm(u, v, 4, 4, s.seed);
                Color crust = s.colorA * (0.6f + 0.6f * n2);
                Color glow = Color.Lerp(s.colorB, s.colorC, Mathf.Clamp01(heat * 1.4f - 0.3f));
                Color c = Color.Lerp(crust, glow, Mathf.Clamp01(heat + (n2 - 0.6f) * 0.5f));
                c.a = 1f;
                return c;
            }

            case FloorPattern.Space:
            {
                float neb = Fbm(u, v, 3, 5, s.seed);
                float neb2 = Fbm(u + 0.37f, v + 0.11f, 4, 5, s.seed + 9);
                Color c = s.colorA;
                c = Color.Lerp(c, s.colorB, Mathf.Clamp01((neb - 0.45f) * 1.6f) * 0.7f);
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01((neb2 - 0.55f) * 1.8f) * 0.55f);
                c.a = 1f;
                return c;
            }

            case FloorPattern.Ice:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float crack = Mathf.Clamp01(1f - edge / 0.06f);
                Voronoi(u, v, cells * 3, s.seed + 1, out float g1, out float g2, out _);
                float small = Mathf.Clamp01(1f - (g2 - g1) * cells * 3 / 0.05f) * 0.45f;
                float frost = Fbm(u, v, 4, 4, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, frost * 0.6f + HashId(id, s.seed) * 0.25f);
                c = Color.Lerp(c, s.colorC, Mathf.Max(crack, small));
                c.a = 1f;
                return c;
            }
        }
        return s.colorA;
    }

    private static void AddStars(Color[] px, int n, FloorStyle s, System.Random rng)
    {
        int count = n * n / 90;
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(n), y = rng.Next(n);
            float b = (float)rng.NextDouble();
            Color star = Color.Lerp(Color.white, s.colorC, (float)rng.NextDouble() * 0.4f);
            Blend(px, n, x, y, star, 0.4f + 0.6f * b);
            if (b > 0.93f) // a few bright ones with a small cross
            {
                Blend(px, n, x + 1, y, star, 0.5f); Blend(px, n, x - 1, y, star, 0.5f);
                Blend(px, n, x, y + 1, star, 0.5f); Blend(px, n, x, y - 1, star, 0.5f);
            }
        }
    }

    private static void AddFlowers(Color[] px, int n, FloorStyle s, System.Random rng)
    {
        int count = Mathf.Max(1, n * n / 300);
        for (int i = 0; i < count; i++)
        {
            int x = rng.Next(n), y = rng.Next(n);
            Color petal = rng.NextDouble() < 0.5 ? s.colorC : new Color(1f, 1f, 1f, 1f);
            Blend(px, n, x + 1, y, petal, 1f); Blend(px, n, x - 1, y, petal, 1f);
            Blend(px, n, x, y + 1, petal, 1f); Blend(px, n, x, y - 1, petal, 1f);
            Blend(px, n, x, y, new Color(1f, 0.85f, 0.2f, 1f), 1f);
        }
    }

    /// <summary>Disco: a grid of tiles, each lit in a random bright colour that changes every beat.</summary>
    private static void DrawDisco(Texture2D tex, FloorStyle s, int step)
    {
        if (tex == null) return;
        int n = tex.width;
        int cells = Mathf.Max(1, s.cells);
        Color32[] px = new Color32[n * n];
        Color32 gap = s.colorA;
        for (int y = 0; y < n; y++)
        {
            int cy = y * cells / n;
            int ly = y - cy * n / cells;
            for (int x = 0; x < n; x++)
            {
                int cx = x * cells / n;
                int lx = x - cx * n / cells;
                int size = n / cells;
                bool border = lx == 0 || ly == 0 || lx == size - 1 || ly == size - 1;
                if (border) { px[y * n + x] = gap; continue; }
                float h = Hash(cx + step * 31, cy + step * 17, s.seed);
                bool lit = Hash(cx * 3 + step, cy * 5 - step, s.seed + 2) > 0.35f;
                Color c = Color.HSVToRGB(h, 0.85f, lit ? 1f : 0.18f);
                // A lighter centre on each tile so it reads as a lamp.
                float dx = (lx + 0.5f) / size - 0.5f, dy = (ly + 0.5f) / size - 0.5f;
                float centre = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                c = Color.Lerp(c, Color.white, lit ? centre * 0.35f : 0f);
                c.a = 1f;
                px[y * n + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
    }

    // ------------------------------------------------------------------
    // Noise helpers (all tile seamlessly)
    // ------------------------------------------------------------------

    private static float Frac(float f) => f - Mathf.Floor(f);

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static float HashId(int id, int seed) => Hash(id, id * 7 + 3, seed);

    /// <summary>Value noise on a lattice of 'period' cells that wraps around (tileable).</summary>
    private static float TileNoise(float u, float v, int period, int seed)
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
    private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
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
    private static void Voronoi(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
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

    private static void Blend(Color[] px, int n, int x, int y, Color c, float k)
    {
        x = ((x % n) + n) % n; y = ((y % n) + n) % n;
        int i = y * n + x;
        px[i] = Color.Lerp(px[i], c, k);
    }

    // ------------------------------------------------------------------
    // Material helpers (built-in and URP property names)
    // ------------------------------------------------------------------

    private static void SetColor(Material m, string prop, Color c) { if (m.HasProperty(prop)) m.SetColor(prop, c); }
    private static void SetFloat(Material m, string prop, float f) { if (m.HasProperty(prop)) m.SetFloat(prop, f); }
    private static void SetTexture(Material m, string prop, Texture t) { if (m.HasProperty(prop)) m.SetTexture(prop, t); }

    private static void SetTiling(Material m, Vector2 scale, Vector2 off)
    {
        foreach (string prop in new[] { "_BaseMap", "_MainTex" })
        {
            if (!m.HasProperty(prop)) continue;
            m.SetTextureScale(prop, scale);
            m.SetTextureOffset(prop, off);
        }
    }

    // ------------------------------------------------------------------
    // Built-in styles
    // ------------------------------------------------------------------

    private void EnsureDefaultStyles()
    {
        foreach (FloorStyle def in DefaultStyles())
            if (!styles.Exists(s => s.name == def.name)) styles.Add(def);
    }

    private static FloorStyle S(string name, FloorPattern p, Color a, Color b, Color c, int res, bool pixelated,
                                float tile, int cells, float smooth = 0.2f, float glow = 0f)
        => new FloorStyle { name = name, pattern = p, colorA = a, colorB = b, colorC = c, resolution = res,
                            pixelated = pixelated, tileWorldSize = tile, cells = cells, smoothness = smooth, glow = glow,
                            seed = name.Length * 7 + 3 };

    private static IEnumerable<FloorStyle> DefaultStyles()
    {
        yield return S("Classic", FloorPattern.Classic, Color.white, Color.gray, Color.black, 8, false, 1f, 1);

        FloorStyle neon = S("Neon Grid", FloorPattern.NeonGrid, new Color(0.03f, 0.02f, 0.08f), new Color(0.1f, 0.85f, 1f),
                            new Color(1f, 0.3f, 0.9f), 256, false, 6f, 4, 0.7f, 1.6f);
        neon.pulseSpeed = 0.35f; neon.pulseAmount = 0.35f;
        yield return neon;

        yield return S("Retro Checker", FloorPattern.Checker, new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.12f, 0.14f),
                       Color.black, 64, true, 4f, 4, 0.45f);

        yield return S("Pixel Grass", FloorPattern.PixelGrass, new Color(0.22f, 0.55f, 0.18f), new Color(0.36f, 0.72f, 0.25f),
                       new Color(0.95f, 0.35f, 0.55f), 48, true, 4f, 1, 0.05f);

        yield return S("Wood Planks", FloorPattern.WoodPlanks, new Color(0.55f, 0.35f, 0.18f), new Color(0.42f, 0.25f, 0.12f),
                       new Color(0.16f, 0.09f, 0.05f), 64, true, 5f, 6, 0.25f);

        yield return S("Cobblestone", FloorPattern.Cobblestone, new Color(0.55f, 0.55f, 0.58f), new Color(0.42f, 0.4f, 0.38f),
                       new Color(0.12f, 0.12f, 0.13f), 128, true, 5f, 6, 0.15f);

        yield return S("Marble", FloorPattern.Marble, new Color(0.95f, 0.94f, 0.92f), new Color(0.55f, 0.55f, 0.6f),
                       new Color(0.78f, 0.66f, 0.42f), 256, false, 8f, 3, 0.9f);

        FloorStyle lava = S("Lava", FloorPattern.Lava, new Color(0.12f, 0.05f, 0.04f), new Color(1f, 0.25f, 0.02f),
                            new Color(1f, 0.85f, 0.3f), 128, true, 6f, 5, 0.35f, 1.4f);
        lava.pulseSpeed = 0.5f; lava.pulseAmount = 0.4f; lava.scrollSpeed = new Vector2(0.01f, 0.006f);
        yield return lava;

        FloorStyle space = S("Deep Space", FloorPattern.Space, new Color(0.01f, 0.01f, 0.04f), new Color(0.35f, 0.1f, 0.55f),
                             new Color(0.1f, 0.45f, 0.8f), 256, false, 10f, 1, 0.6f, 1.1f);
        space.scrollSpeed = new Vector2(0.004f, 0.012f);
        yield return space;

        yield return S("Ice", FloorPattern.Ice, new Color(0.7f, 0.85f, 0.95f), new Color(0.5f, 0.7f, 0.9f),
                       new Color(0.97f, 0.99f, 1f), 256, false, 7f, 4, 0.95f);

        FloorStyle disco = S("Disco", FloorPattern.Disco, new Color(0.05f, 0.05f, 0.07f), Color.white, Color.white,
                             64, true, 6f, 4, 0.8f, 1.3f);
        disco.beatSeconds = 0.5f;
        yield return disco;
    }
}
