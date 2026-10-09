using System;
using System.Collections.Generic;
using UnityEngine;
using static PixelNoise;

/// <summary>
/// Floor styles: swaps the look of the scene's floor for one of several textures drawn in code at runtime (no art
/// assets needed). Picked in the pause menu's Settings ("Floor style"), remembered in PlayerPrefs. "Classic" puts
/// the floor's own material back. The floor is the renderer set in the Inspector, else the collider straight below
/// the cube, else an object called Floor / Ground / Plane. Some styles move (scroll, pulse, disco tiles).
/// Added by PixelClicker.Awake if missing.
/// </summary>
public class PixelFloor : MonoBehaviour, IPixelLookSource
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
        Disco,        // light-up tiles that change colour to a beat
        Honeycomb,    // golden hexagon cells
        Bricks,       // pixel-art brick wall laid flat
        CircuitBoard, // green board with copper traces, pads and chips
        SandDunes,    // wind ripples in sand
        Ocean,        // deep water with moving light patterns (caustics)
        Snow,         // soft drifts with sparkles
        Tartan,       // woven plaid cloth
        TreadPlate,   // shiny metal floor with raised diamonds
        Hologram,     // rainbow shimmer with scan lines
        Crystal,      // glowing faceted gems
        MatrixRain,   // falling green code
        Hazard        // yellow / black warning stripes
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

        [Range(0f, 1f)]
        [Tooltip("How metallic the floor looks (0 = not at all).")]
        public float metallic = 0f;

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
    private float pulseClock;

    public int StyleCount => styles.Count;
    public int Current => current;
    public bool HasFloor => floorRenderer != null;
    /// <summary>The floor being restyled (null if none was found). Used by the horizon fog.</summary>
    public Renderer FloorRenderer => floorRenderer;
    public string StyleName(int i) => i >= 0 && i < styles.Count ? styles[i].name : "";
    public bool Usable => HasFloor && styles.Count > 0;
    public Rect PreviewRect => new Rect(0f, 0f, 1f, 1f);

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
        SetFloat(m, "_Metallic", style.metallic);
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
        float dt = PixelTimeStop.IsStopped ? 0f : Time.unscaledDeltaTime; // keeps moving on the title screen, freezes in Time Stop

        if (style.scrollSpeed != Vector2.zero)
        {
            offset += style.scrollSpeed * dt;
            offset.x -= Mathf.Floor(offset.x);
            offset.y -= Mathf.Floor(offset.y);
            SetTiling(activeMaterial, baseTiling, offset);
        }

        if (style.glow > 0f && style.pulseSpeed > 0f && activeMaterial.HasProperty("_EmissionColor"))
        {
            pulseClock += dt;
            float wave = 0.5f + 0.5f * Mathf.Sin(pulseClock * style.pulseSpeed * Mathf.PI * 2f);
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

            case FloorPattern.Honeycomb:
            {
                HexCells(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;                         // 0 at the cell walls
                float wall = Mathf.Clamp01(1f - edge / 0.12f);
                float depth = Mathf.Clamp01(f1 * cells * 1.6f);          // darker towards each cell's middle (deep honey)
                Color honey = Color.Lerp(s.colorA, s.colorB, depth * 0.7f + HashId(id, s.seed) * 0.2f);
                honey = Color.Lerp(honey, Color.white, Mathf.Clamp01(0.35f - f1 * cells) * 0.5f); // glossy highlight
                return Color.Lerp(honey, s.colorC, wall);
            }

            case FloorPattern.Bricks:
            {
                int rows = cells;
                float gy = v * rows;
                int row = Mathf.FloorToInt(gy);
                float gx = u * rows * 0.5f + ((row & 1) == 0 ? 0f : 0.5f);  // bricks are twice as long as tall
                int col = Mathf.FloorToInt(gx);
                float bu = Frac(gx), bv = Frac(gy);
                float mortarU = 1.5f / n * rows * 0.5f, mortarV = 1.5f / n * rows;
                if (bu < mortarU || bv < mortarV) return s.colorC * (0.9f + 0.2f * Hash(x, y, s.seed));
                float tone = 0.8f + 0.35f * Hash(col, row, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, Hash(col * 3, row * 5, s.seed + 1)) * tone;
                c *= 0.88f + 0.24f * Hash(x, y, s.seed + 2);             // speckle
                if (bv > 1f - mortarV * 1.5f) c = Color.Lerp(c, Color.white, 0.12f); // lit top edge
                return c;
            }

            case FloorPattern.CircuitBoard:
            {
                float gx = u * cells - 0.5f, gy = v * cells - 0.5f;      // pads sit on whole numbers
                float w = 0.09f;
                Color c = s.colorA * (0.85f + 0.25f * Fbm(u, v, 8, 3, s.seed));
                int r = Mathf.RoundToInt(gy), cx = Mathf.FloorToInt(gx);
                int q = Mathf.RoundToInt(gx), cy = Mathf.FloorToInt(gy);
                bool trace = (Mathf.Abs(gy - r) < w && Seg(cx, r, cells, s.seed)) ||
                             (Mathf.Abs(gx - q) < w && Seg(q, cy, cells, s.seed + 50));
                // Pads on joints that have a trace.
                float pd = Vector2.Distance(new Vector2(gx, gy), new Vector2(q, r));
                bool joint = Seg(q, r, cells, s.seed) || Seg(q - 1, r, cells, s.seed) || Seg(q, r, cells, s.seed + 50) || Seg(q, r - 1, cells, s.seed + 50);
                if (trace) c = s.colorB * (0.85f + 0.15f * Hash(x, y, s.seed));
                if (joint && pd < 0.22f) c = pd < 0.1f ? s.colorA * 0.4f : s.colorB * 1.1f; // ring pad with a hole
                // A chip now and then, on top of everything.
                int chipX = Mathf.FloorToInt(gx + 0.5f), chipY = Mathf.FloorToInt(gy + 0.5f);
                if (Hash(Wrap(chipX, cells), Wrap(chipY, cells), s.seed + 9) > 0.86f)
                {
                    float lu = gx + 0.5f - chipX, lv = gy + 0.5f - chipY;
                    if (lu > 0.18f && lu < 0.82f && lv > 0.25f && lv < 0.75f) c = s.colorC * (0.9f + 0.2f * Hash(x, y, s.seed + 3));
                    else if (lu > 0.22f && lu < 0.78f && (lv > 0.18f && lv < 0.25f || lv > 0.75f && lv < 0.82f) && Frac(lu * 10f) < 0.5f)
                        c = new Color(0.8f, 0.8f, 0.82f); // pins
                }
                return c;
            }

            case FloorPattern.SandDunes:
            {
                float warp = Fbm(u, v, 3, 3, s.seed) * 2.5f;
                float t = Frac(v * cells + warp + Mathf.Sin(u * Mathf.PI * 2f * 2f) * 0.3f);
                float ripple = t < 0.7f ? t / 0.7f : (1f - t) / 0.3f;    // gentle slope up, steep slope down
                Color c = Color.Lerp(s.colorB, s.colorA, ripple);
                c *= 0.92f + 0.16f * Hash(x, y, s.seed);                 // grains
                if (Hash(x, y, s.seed + 7) > 0.995f) c = Color.Lerp(c, s.colorC, 0.7f);
                return c;
            }

            case FloorPattern.Ocean:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out _);
                Voronoi(u + 0.31f, v + 0.17f, cells * 2, s.seed + 4, out float g1, out float g2, out _);
                float caustic = Mathf.Pow(Mathf.Clamp01(1f - (f2 - f1) * cells / 0.25f), 2f)
                              + Mathf.Pow(Mathf.Clamp01(1f - (g2 - g1) * cells * 2 / 0.25f), 2f) * 0.6f;
                float deep = Fbm(u, v, 3, 4, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, deep * 0.7f);
                return Color.Lerp(c, s.colorC, Mathf.Clamp01(caustic) * 0.75f);
            }

            case FloorPattern.Snow:
            {
                float drift = Fbm(u, v, 3, 5, s.seed);
                Color c = Color.Lerp(s.colorB, s.colorA, Mathf.Clamp01(drift * 1.3f - 0.05f));
                float h = Hash(x, y, s.seed);
                if (h > 0.992f) c = s.colorC;                             // sparkles
                else if (h > 0.97f) c = Color.Lerp(c, s.colorC, 0.4f);
                return c;
            }

            case FloorPattern.Tartan:
            {
                // Thread colour along each axis, then a twill weave picks warp or weft per texel.
                Color warp = TartanThread(s, Frac(u * cells));
                Color weft = TartanThread(s, Frac(v * cells));
                bool showWarp = ((x + y) / 2 & 1) == 0;
                Color c = showWarp ? warp : weft;
                return c * (0.9f + 0.1f * Hash(x, y, s.seed));
            }

            case FloorPattern.TreadPlate:
            {
                float brushed = Fbm(u, v, 2, 64, 3, s.seed);             // fine streaks along one direction
                Color c = s.colorA * (0.85f + 0.25f * brushed);
                float cu = Frac(u * cells) - 0.5f, cv = Frac(v * cells) - 0.5f;
                int parity = (Mathf.FloorToInt(u * cells) + Mathf.FloorToInt(v * cells)) & 1;
                float k = 0.7071f;
                float a = parity == 0 ? (cu + cv) * k : (cu - cv) * k;   // along the diamond
                float b = parity == 0 ? (cu - cv) * k : (cu + cv) * k;   // across it
                float d = (a / 0.33f) * (a / 0.33f) + (b / 0.09f) * (b / 0.09f);
                if (d < 1f)
                {
                    float shade = Mathf.Clamp01(0.5f - b / 0.09f * 0.5f);  // lit on one side, shadowed on the other
                    c = Color.Lerp(s.colorB, s.colorC, shade);
                }
                else if (d < 1.4f) c *= 0.75f;                            // small shadow ring
                return c;
            }

            case FloorPattern.Hologram:
            {
                float hue = Frac(u + v + Fbm(u, v, 2, 3, s.seed) * 0.25f);
                Color c = Color.HSVToRGB(hue, 0.55f, 1f);
                c = Color.Lerp(s.colorA, c, 0.8f);
                if ((y % 4) == 0) c *= 0.55f;                               // scan lines
                float grid = Mathf.Min(Frac(u * cells), Frac(v * cells));
                if (grid < 1.2f / n * cells) c = Color.Lerp(c, s.colorC, 0.6f);
                return c;
            }

            case FloorPattern.Crystal:
            {
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float facet = Mathf.Clamp01(1f - f1 * cells * 1.2f);      // brighter towards each gem's point
                Color gem = Color.Lerp(s.colorA, s.colorB, HashId(id, s.seed));
                gem *= 0.45f + 0.8f * facet * (0.7f + 0.3f * HashId(id, s.seed + 3));
                float rim = Mathf.Clamp01(1f - edge / 0.05f);
                return Color.Lerp(gem, s.colorC, rim * 0.8f);
            }

            case FloorPattern.MatrixRain:
            {
                int cols = cells;
                int glyph = Mathf.Max(4, n / cols);
                int col = x / glyph, row = y / glyph;
                int gx = x % glyph, gy = y % glyph;
                // Each column has its own trail: brightest at the head, fading upward.
                float head = Hash(col, 0, s.seed);
                float trail = Frac(head - v * 1.0f + Hash(col, 1, s.seed) * 0.3f);
                trail = Mathf.Pow(1f - trail, 2.5f);
                bool lit = gx >= 1 && gx < glyph - 2 && gy >= 1 && gy < glyph - 1 &&
                           Hash(col * 97 + gx, row * 53 + gy, s.seed + 5) > 0.45f;
                Color c = s.colorA;
                if (lit)
                {
                    float b = trail * (0.6f + 0.4f * Hash(col, row, s.seed + 6));
                    c = Color.Lerp(s.colorA, s.colorB, b);
                    if (trail > 0.9f) c = Color.Lerp(c, s.colorC, 0.7f);  // white-hot head
                }
                return c;
            }

            case FloorPattern.Hazard:
            {
                bool stripe = Frac((u + v) * cells) < 0.5f;
                Color c = stripe ? s.colorA : s.colorB;
                float wear = Fbm(u, v, 6, 4, s.seed);
                if (wear > 0.68f) c = Color.Lerp(c, s.colorC, (wear - 0.68f) * 3f);   // scuffed paint
                c *= 0.9f + 0.15f * Hash(x, y, s.seed);
                return c;
            }
        }
        return s.colorA;
    }

    private static int Wrap(int i, int m) => ((i % m) + m) % m;

    /// <summary>Circuit board: is there a trace from joint (i, j) to the next joint along? (wraps, so it tiles)</summary>
    private static bool Seg(int i, int j, int cells, int seed) => Hash(Wrap(i, cells), Wrap(j, cells), seed) > 0.55f;

    /// <summary>Tartan: the thread colour at position t (0..1) across one repeat.</summary>
    private static Color TartanThread(FloorStyle s, float t)
    {
        if (t < 0.38f) return s.colorA;
        if (t < 0.46f) return s.colorB;
        if (t < 0.5f) return s.colorC;
        if (t < 0.54f) return s.colorB;
        if (t < 0.7f) return s.colorA * 0.75f;
        if (t < 0.74f) return Color.Lerp(s.colorC, Color.white, 0.5f);
        return s.colorB * 0.8f;
    }

    /// <summary>Hexagon-like cells: nearest point on a staggered grid (odd rows shifted half a cell). Tiles for even 'cells'.</summary>
    private static void HexCells(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
    {
        cells = Mathf.Max(2, cells + (cells & 1));
        float x = Frac(u) * cells, y = Frac(v) * cells * 0.866f;
        int rows = cells;
        int cy = Mathf.FloorToInt(y / 0.866f);
        f1 = f2 = 99f;
        id = 0;
        for (int oy = -1; oy <= 1; oy++)
        {
            int gy = cy + oy;
            float shift = (Wrap(gy, rows) & 1) == 0 ? 0f : 0.5f;
            int cx = Mathf.FloorToInt(x - shift);
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox;
                float px = gx + 0.5f + shift, py = (gy + 0.5f) * 0.866f;
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; id = Wrap(gy, rows) * cells + Wrap(gx, cells); }
                else if (d < f2) f2 = d;
            }
        }
        f1 /= cells;
        f2 /= cells;
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

        FloorStyle honey = S("Honeycomb", FloorPattern.Honeycomb, new Color(1f, 0.72f, 0.15f), new Color(0.72f, 0.35f, 0.02f),
                             new Color(0.35f, 0.2f, 0.05f), 256, false, 5f, 6, 0.85f, 0.25f);
        yield return honey;

        yield return S("Bricks", FloorPattern.Bricks, new Color(0.62f, 0.24f, 0.17f), new Color(0.5f, 0.2f, 0.15f),
                       new Color(0.62f, 0.6f, 0.55f), 64, true, 4f, 8, 0.1f);

        FloorStyle circuit = S("Circuit Board", FloorPattern.CircuitBoard, new Color(0.04f, 0.3f, 0.14f), new Color(0.95f, 0.7f, 0.25f),
                               new Color(0.06f, 0.06f, 0.07f), 256, false, 6f, 8, 0.55f, 0.35f);
        circuit.pulseSpeed = 0.25f; circuit.pulseAmount = 0.5f;
        yield return circuit;

        yield return S("Sand Dunes", FloorPattern.SandDunes, new Color(0.93f, 0.8f, 0.55f), new Color(0.75f, 0.58f, 0.35f),
                       Color.white, 128, false, 6f, 7, 0.1f);

        FloorStyle ocean = S("Ocean", FloorPattern.Ocean, new Color(0.02f, 0.18f, 0.35f), new Color(0.05f, 0.4f, 0.55f),
                             new Color(0.6f, 0.95f, 1f), 256, false, 7f, 5, 0.95f, 0.35f);
        ocean.scrollSpeed = new Vector2(0.03f, 0.018f);
        yield return ocean;

        yield return S("Snow", FloorPattern.Snow, new Color(0.97f, 0.98f, 1f), new Color(0.74f, 0.8f, 0.9f),
                       Color.white, 128, false, 6f, 1, 0.75f);

        yield return S("Tartan", FloorPattern.Tartan, new Color(0.55f, 0.06f, 0.08f), new Color(0.05f, 0.12f, 0.3f),
                       new Color(0.95f, 0.8f, 0.2f), 128, true, 4f, 2, 0.05f);

        FloorStyle tread = S("Tread Plate", FloorPattern.TreadPlate, new Color(0.55f, 0.57f, 0.6f), new Color(0.35f, 0.36f, 0.4f),
                             new Color(0.92f, 0.93f, 0.95f), 256, false, 4f, 6, 0.7f);
        tread.metallic = 0.85f;
        yield return tread;

        FloorStyle holo = S("Hologram", FloorPattern.Hologram, new Color(0.6f, 0.7f, 0.9f), Color.white,
                            Color.white, 128, false, 6f, 4, 0.9f, 0.9f);
        holo.scrollSpeed = new Vector2(0.06f, 0.03f); holo.pulseSpeed = 0.6f; holo.pulseAmount = 0.3f;
        yield return holo;

        FloorStyle crystal = S("Crystal", FloorPattern.Crystal, new Color(0.55f, 0.2f, 0.85f), new Color(0.2f, 0.55f, 0.95f),
                               new Color(0.95f, 0.85f, 1f), 256, false, 5f, 6, 0.95f, 0.7f);
        crystal.pulseSpeed = 0.3f; crystal.pulseAmount = 0.4f;
        yield return crystal;

        FloorStyle matrix = S("Matrix Rain", FloorPattern.MatrixRain, new Color(0.0f, 0.03f, 0.0f), new Color(0.15f, 1f, 0.35f),
                              new Color(0.85f, 1f, 0.9f), 128, true, 6f, 16, 0.5f, 1.3f);
        matrix.scrollSpeed = new Vector2(0f, 0.25f);
        yield return matrix;

        yield return S("Hazard Stripes", FloorPattern.Hazard, new Color(0.98f, 0.8f, 0.05f), new Color(0.08f, 0.08f, 0.08f),
                       new Color(0.45f, 0.42f, 0.38f), 128, false, 4f, 4, 0.3f);
    }
}
