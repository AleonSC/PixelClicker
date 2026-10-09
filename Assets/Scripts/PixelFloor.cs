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
        Hazard,          // yellow / black warning stripes
        TournamentTiles, // big pale stone arena tiles with grout and the odd crack
        DragonRadar,     // green radar screen with glowing orange blips
        DragonBalls,     // rows of glossy orange balls with red stars (1-7)
        KiAura,          // rising flame-like energy (colour set per style)
        Craters,         // battle-scarred rocky ground full of craters
        GravityRoom,     // metal training-room panels with a glowing ring
        PowerArena,      // floating hex stone plates with glowing seams
        EnergyWisps,     // swirling wisps of destructive energy
        CardBack,        // trading-card back: brown frame, swirling vortex, dark oval in the middle
        Liquid,          // flowing liquid (animated)
        CloudSea,        // fluffy clouds seen from above (scrolls)
        Kaleidoscope,    // mirrored, colour-shifting kaleidoscope (animated)
        PacMan           // maze with a chomping Pac-Man, pellets and ghosts (animated)
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

        [Tooltip("Show ONE copy of the pattern (Tile World Size across, centred on the floor) instead of repeating it; the rest of the floor shows the pattern's edge colour.")]
        public bool stretchToFloor = false;

        [Tooltip("Animated patterns (Liquid, Kaleidoscope, Pac-Man): how many times per second the pattern is redrawn. 0 = still. Higher = smoother but costs more.")]
        public float animateFps = 0f;

        [Tooltip("Animated patterns: how fast the animation plays (1 = normal).")]
        public float animSpeed = 1f;
    }

    [Header("Floor")]
    [Tooltip("The floor's renderer. Empty = found automatically (collider below the cube, else an object named like one in Floor Names).")]
    [SerializeField] private Renderer floorRenderer;

    [Tooltip("Object names tried when no floor renderer is set and nothing is found below the cube.")]
    [SerializeField] private string[] floorNames = { "Floor", "Ground", "Plane" };

    [Header("Styles")]
    [Tooltip("Add the built-in styles that are missing from the list (by name).")]
    [SerializeField] private bool addDefaultStyles = true;

    [Tooltip("Which round of built-in style fixes this list has had (set automatically; lower it to apply them again).")]
    [SerializeField] private int defaultsVersion = 0;

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
    private Vector2 baseOffset;
    private Vector2 offset;
    private float pulseClock;
    private float animTimer, animClock;

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
        // Tile by world size so every pattern has the same scale on any floor, centred on the floor's middle.
        baseTiling = new Vector2(Mathf.Max(0.02f, size.x / tile), Mathf.Max(0.02f, size.z / tile));
        baseOffset = new Vector2(0.5f - Frac(0.5f * baseTiling.x), 0.5f - Frac(0.5f * baseTiling.y));
        animTimer = animClock = 0f;
        SetTiling(m, baseTiling, baseOffset + offset);

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
            SetTiling(activeMaterial, baseTiling, baseOffset + offset);
        }

        if (style.glow > 0f && style.pulseSpeed > 0f && activeMaterial.HasProperty("_EmissionColor"))
        {
            pulseClock += dt;
            float wave = 0.5f + 0.5f * Mathf.Sin(pulseClock * style.pulseSpeed * Mathf.PI * 2f);
            activeMaterial.SetColor("_EmissionColor", Color.white * style.glow * (1f - style.pulseAmount * wave));
        }

        if (style.animateFps > 0f && dt > 0f && activeTexture != null)
        {
            animClock += dt * style.animSpeed;
            animTimer += dt;
            if (animTimer >= 1f / style.animateFps)
            {
                animTimer = 0f;
                Fill(activeTexture, style, animClock, true);
            }
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
            wrapMode = s.stretchToFloor ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,
            filterMode = s.pixelated ? FilterMode.Point : FilterMode.Bilinear,
            anisoLevel = 4
        };
        if (s.pattern == FloorPattern.Disco) { DrawDisco(tex, s, step); return tex; }
        Fill(tex, s, 0f, false);
        return tex;
    }

    // Animation time read by the animated patterns while a texture is being drawn.
    private static float shadeTime;
    private static Color[] fillBuffer; // reused by animated floors so redrawing doesn't allocate every frame

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { fillBuffer = null; pacPath = null; pacPathIndex = null; shadeTime = 0f; }

    /// <summary>Draws the pattern into 'tex' at animation time 'time' (0 for still patterns). 'reuse' = keep the pixel buffer for the next frame.</summary>
    private static void Fill(Texture2D tex, FloorStyle s, float time, bool reuse)
    {
        int n = tex.width;
        Color[] px;
        if (reuse)
        {
            if (fillBuffer == null || fillBuffer.Length != n * n) fillBuffer = new Color[n * n];
            px = fillBuffer;
        }
        else px = new Color[n * n];
        int cells = Mathf.Max(1, s.cells);
        shadeTime = time;

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
        System.Random rng = new System.Random(s.seed);
        if (s.pattern == FloorPattern.Space) AddStars(px, n, s, rng);
        if (s.pattern == FloorPattern.PixelGrass) AddFlowers(px, n, s, rng);

        tex.SetPixels(px);
        tex.Apply(false);
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
            case FloorPattern.TournamentTiles:
            {
                float gx = u * cells, gy = v * cells;
                int tx = Mathf.FloorToInt(gx), ty = Mathf.FloorToInt(gy);
                float lu = Frac(gx), lv = Frac(gy);
                float grout = 1.5f / n * cells;
                if (lu < grout || lv < grout) return s.colorC * (0.85f + 0.3f * Hash(x, y, s.seed));
                float tone = 0.88f + 0.2f * Hash(tx, ty, s.seed);
                Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 6, 4, s.seed) * 0.6f) * tone;
                c *= 0.93f + 0.12f * Hash(x, y, s.seed + 1);            // grain
                if (lu > 1f - grout * 1.5f || lv > 1f - grout * 1.5f) c *= 0.85f;  // shaded edge
                else if (lu < grout * 2.5f || lv < grout * 2.5f) c = Color.Lerp(c, Color.white, 0.1f); // lit edge
                // Cracks in some tiles.
                if (Hash(Wrap(tx, cells), Wrap(ty, cells), s.seed + 3) > 0.62f)
                {
                    Voronoi(u, v, cells * 3, s.seed + 5, out float f1, out float f2, out _);
                    if ((f2 - f1) * cells * 3 < 0.04f) c = Color.Lerp(c, s.colorC, 0.75f);
                }
                return c;
            }

            case FloorPattern.DragonRadar:
            {
                Color c = s.colorA * (0.9f + 0.15f * Fbm(u, v, 4, 2, s.seed));
                float cu = Frac(u * cells), cv = Frac(v * cells);
                float line = Mathf.Min(Mathf.Min(cu, 1f - cu), Mathf.Min(cv, 1f - cv)) * n / cells;
                c = Color.Lerp(c, s.colorB, Mathf.Clamp01(1.3f - line) * 0.8f);
                if ((y % 3) == 0) c *= 0.85f;                              // screen lines
                // Seven blips at fixed random spots (wrapping, so the texture tiles).
                for (int i = 0; i < 7; i++)
                {
                    float px = Hash(i, 1, s.seed), py = Hash(i, 2, s.seed);
                    float dx = Mathf.Abs(u - px); dx = Mathf.Min(dx, 1f - dx);
                    float dy = Mathf.Abs(v - py); dy = Mathf.Min(dy, 1f - dy);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * n;           // in texels
                    if (d < 3.2f) c = Color.Lerp(s.colorC, new Color(1f, 0.95f, 0.6f), Mathf.Clamp01(1.5f - d));
                    else if (d < 9f) c = Color.Lerp(c, s.colorC, (1f - (d - 3.2f) / 5.8f) * 0.45f); // glow
                }
                return c;
            }

            case FloorPattern.DragonBalls:
            {
                // Staggered rows of balls; each ball has 1-7 stars.
                float gy = v * cells;
                int row = Mathf.FloorToInt(gy);
                float gx = u * cells + ((row & 1) == 0 ? 0f : 0.5f);
                int col = Mathf.FloorToInt(gx);
                float lx = Frac(gx) - 0.5f, ly = Frac(gy) - 0.5f;
                float r = Mathf.Sqrt(lx * lx + ly * ly);
                const float R = 0.42f;
                Color bg = s.colorC * (0.85f + 0.2f * Fbm(u, v, 4, 3, s.seed));
                if (r > R) return r < R + 0.05f ? bg * 0.6f : bg;          // soft shadow ring
                float nx = lx / R, ny = ly / R, nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                float light = Mathf.Clamp01(nx * -0.45f + ny * 0.55f + nz * 0.7f);
                Color c = Color.Lerp(s.colorB, s.colorA, light);
                // Stars.
                int count = 1 + (int)(Hash(Wrap(col, cells), Wrap(row, cells), s.seed) * 6.999f);
                float sr = count == 1 ? 0.13f : 0.065f;
                for (int i = 0; i < count; i++)
                {
                    float ax = 0f, ay = 0f;
                    if (count > 1 && !(count == 7 && i == 6))
                    {
                        int ring = count == 7 ? 6 : count;
                        float ang = i * Mathf.PI * 2f / ring + 0.3f;
                        ax = Mathf.Cos(ang) * 0.19f; ay = Mathf.Sin(ang) * 0.19f;
                    }
                    float sx = lx - ax, sy = ly - ay;
                    float sd = Mathf.Sqrt(sx * sx + sy * sy);
                    float th = Mathf.Atan2(sy, sx);
                    float edge = sr * (0.5f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * (th - Mathf.PI * 0.5f))), 3f));
                    if (sd < edge) c = new Color(0.85f, 0.08f, 0.05f);
                }
                // Glossy highlight.
                float hx = lx + 0.14f, hy = ly - 0.16f;
                float h = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.1f);
                return Color.Lerp(c, Color.white, h * 0.85f);
            }

            case FloorPattern.KiAura:
            {
                // Tall streaks (stretched noise) that scroll upward like flames.
                float warp = Fbm(u, v, 3, 3, s.seed) * 0.15f;
                float flame = Fbm(u + warp, v, 10, 2, 4, s.seed + 1);
                float flicker = Fbm(u, v, 4, 4, 3, s.seed + 2);
                float k = Mathf.Clamp01((flame * 0.75f + flicker * 0.35f - 0.35f) * 2f);
                k = k * k;
                Color c = Color.Lerp(s.colorA, s.colorB, Mathf.Clamp01(k * 1.6f));
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01(k * 2.2f - 1.1f));
                if (Hash(x, y, s.seed + 9) > 0.996f) c = s.colorC;            // sparks
                return c;
            }

            case FloorPattern.Craters:
            {
                Color c = Color.Lerp(s.colorA, s.colorB, Fbm(u, v, 5, 5, s.seed));
                c *= 0.9f + 0.2f * Hash(x, y, s.seed);
                Voronoi(u, v, cells, s.seed + 1, out float f1, out _, out int id);
                if (HashId(id, s.seed) > 0.3f)
                {
                    float d = f1 * cells;
                    float r = 0.18f + 0.22f * HashId(id, s.seed + 2);
                    if (d < r)
                    {
                        float depth = 1f - d / r;
                        c = Color.Lerp(c, s.colorC, 0.25f + depth * 0.55f);        // darker bowl
                    }
                    else if (d < r * 1.3f) c = Color.Lerp(c, Color.white, 0.18f * (1f - (d - r) / (r * 0.3f))); // raised rim
                }
                return c;
            }

            case FloorPattern.GravityRoom:
            {
                float pu = Frac(u * cells), pv = Frac(v * cells);
                float seam = 1.2f / n * cells;
                Color c = s.colorA * (0.88f + 0.15f * Fbm(u, v, 2, 48, 2, s.seed));   // brushed metal
                if (pu < seam || pv < seam) c = s.colorB * 0.6f;
                else if (pu < seam * 2f || pv < seam * 2f) c *= 1.12f;
                // Rivets in the panel corners.
                float rx = Mathf.Min(pu, 1f - pu), ry = Mathf.Min(pv, 1f - pv);
                if (Mathf.Abs(rx - 0.08f) < 0.025f && Mathf.Abs(ry - 0.08f) < 0.025f) c = s.colorB;
                // Glowing ring and markings around the middle of the texture.
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Abs(d - 0.36f);
                if (ring < 0.012f) c = s.colorC;
                else if (ring < 0.03f) c = Color.Lerp(c, s.colorC, (1f - (ring - 0.012f) / 0.018f) * 0.5f);
                if (Mathf.Abs(d - 0.3f) < 0.004f) c = s.colorB * 0.5f;
                float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * 16f;
                if (d > 0.4f && d < 0.44f && Frac(ang) < 0.25f) c = Color.Lerp(c, s.colorC, 0.7f);
                return c;
            }

            case FloorPattern.PowerArena:
            {
                HexCells(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                Color stone = Color.Lerp(s.colorA, s.colorB, HashId(id, s.seed) * 0.6f + Fbm(u, v, 6, 3, s.seed) * 0.4f);
                stone *= 0.9f + 0.15f * Hash(x, y, s.seed);
                stone *= 0.85f + 0.25f * Mathf.Clamp01(edge * 3f);         // plates slope down to the seams
                float glow = Mathf.Clamp01(1f - edge / 0.07f);
                return Color.Lerp(stone, s.colorC, glow);
            }

            case FloorPattern.CardBack:
            {
                // One card per tile, standing upright in the middle with a dark gap around it.
                float px = u - 0.5f, py = v - 0.5f;
                float hw = s.stretchToFloor ? 0.49f : 0.32f, hh = s.stretchToFloor ? 0.49f : 0.46f;
                const float corner = 0.025f;
                float qx = Mathf.Abs(px) - (hw - corner), qy = Mathf.Abs(py) - (hh - corner);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
                              + Mathf.Min(Mathf.Max(qx, qy), 0f) - corner;               // < 0 inside the rounded card
                if (outside > 0f) return s.colorC * (0.9f + 0.15f * Fbm(u, v, 8, 3, s.seed));
                float inset = -outside;                                                // distance in from the card's edge

                // Frame: dark brown border with a thin gold line inside it.
                float border = s.stretchToFloor ? 0.03f : 0.04f;
                if (inset < border)
                {
                    Color frame = s.colorB * (0.75f + 0.35f * (inset / border));
                    return frame * (0.92f + 0.12f * Hash(x, y, s.seed));
                }
                if (inset < border + (s.stretchToFloor ? 0.004f : 0.006f)) return Color.Lerp(s.colorA, Color.white, 0.25f);

                // The vortex: spiral arms around the centre (squashed to the card's shape).
                float ex = px / hw, ey = py / hh;
                float r = Mathf.Sqrt(ex * ex + ey * ey);
                float ang = Mathf.Atan2(ey, ex);
                float spiral = Mathf.Sin(ang * 3f + Mathf.Log(Mathf.Max(r, 0.02f)) * 7f + Fbm(u, v, 6, 3, s.seed) * 2.5f);
                float arms = Mathf.SmoothStep(0f, 1f, spiral * 0.5f + 0.5f);
                Color dark = s.colorB * 0.55f;
                Color c = Color.Lerp(dark, s.colorA, arms * (0.55f + 0.45f * Mathf.Clamp01(1.2f - r)));
                c = Color.Lerp(c, Color.Lerp(s.colorA, new Color(1f, 0.92f, 0.6f), 0.4f), Mathf.Clamp01(0.45f - r) * 1.6f); // bright core
                c *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((r - 0.6f) * 1.4f));            // darker towards the frame

                // Dark oval in the middle with a glowing rim.
                float ox = px / (hw * 0.34f), oy = py / (hh * 0.16f);
                float o = Mathf.Sqrt(ox * ox + oy * oy);
                if (o < 1f) return Color.Lerp(new Color(0.03f, 0.02f, 0.02f), new Color(0.12f, 0.06f, 0.03f), o * o);
                if (o < 1.25f) c = Color.Lerp(c, Color.Lerp(s.colorA, Color.white, 0.35f), (1.25f - o) / 0.25f * 0.8f);
                return c;
            }

            case FloorPattern.Liquid:
            {
                // Two layers of warped waves drifting against each other; bright ridges where they meet.
                float t = shadeTime;
                float w1 = Fbm(u + t * 0.03f, v - t * 0.02f, 3, 3, s.seed);
                float w2 = Fbm(u - t * 0.025f + w1 * 0.4f, v + t * 0.035f + w1 * 0.4f, 3, 3, s.seed + 1);
                float a = Mathf.Sin((u * cells + w2 * 3f) * Mathf.PI * 2f + t * 1.4f);
                float b = Mathf.Sin((v * cells - w1 * 3f) * Mathf.PI * 2f - t * 1.1f);
                float k = Mathf.Clamp01(0.5f + 0.25f * (a + b));
                Color c = Color.Lerp(s.colorA, s.colorB, k);
                float ridge = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(a - b) * 1.6f), 6f);  // thin shiny streaks
                return Color.Lerp(c, s.colorC, ridge * 0.8f);
            }

            case FloorPattern.CloudSea:
            {
                float d = Fbm(u, v, 3, 5, s.seed);
                float d2 = Fbm(u + 0.02f, v - 0.025f, 3, 5, s.seed);                    // a step towards the light
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.42f) * 4f));
                float lit = Mathf.Clamp01(0.6f + (d - d2) * 6f);
                Color puff = Color.Lerp(s.colorB, s.colorC, lit);
                Color c = s.colorA * (0.9f + 0.15f * Fbm(u, v, 6, 3, s.seed + 3));      // sky / sea far below
                c = Color.Lerp(c, s.colorA * 0.75f, Mathf.Clamp01((d2 - 0.42f) * 4f) * (1f - cloud) * 0.6f); // cloud shadows
                return Color.Lerp(c, puff, cloud);
            }

            case FloorPattern.Kaleidoscope:
            {
                float t = shadeTime;
                float px = u - 0.5f, py = v - 0.5f;
                float r = Mathf.Sqrt(px * px + py * py);
                float seg = Mathf.PI * 2f / Mathf.Max(3, cells);
                float ang = Mathf.Atan2(py, px) + t * 0.15f;
                float a = ang - Mathf.Floor(ang / seg) * seg;
                if (a > seg * 0.5f) a = seg - a;                                        // mirror each slice
                float fx = r * Mathf.Cos(a), fy = r * Mathf.Sin(a);
                float val = Mathf.Sin(fx * 18f + t) + Mathf.Sin(fy * 26f - t * 1.3f)
                          + Mathf.Sin((fx + fy) * 14f + t * 0.7f) + Mathf.Sin(r * 30f - t * 2f);
                float hue = Frac(val * 0.12f + t * 0.05f + r * 0.6f);
                float bright = 0.55f + 0.45f * Mathf.Sin(val * 2.2f);
                Color c = Color.HSVToRGB(hue, 0.8f, Mathf.Clamp01(bright));
                float lines = Mathf.Abs(Mathf.Sin(val * 3f));
                if (lines < 0.08f) c = Color.Lerp(c, s.colorC, 0.7f);                  // thin outlines between shapes
                return Color.Lerp(s.colorA, c, Mathf.Clamp01(1.1f - r * 0.4f));
            }

            case FloorPattern.PacMan:
                return ShadePacMan(s, u, v);

            case FloorPattern.EnergyWisps:
            {
                // Domain-warped noise: swirling wisps.
                float q = Fbm(u, v, 3, 4, s.seed);
                float r = Fbm(u + q * 0.6f, v + q * 0.6f, 3, 4, s.seed + 1);
                float w = Fbm(u + r * 0.8f, v - r * 0.8f, 4, 4, s.seed + 2);
                float k = Mathf.Clamp01((w - 0.45f) * 2.5f);
                k = k * k * (3f - 2f * k);
                Color c = Color.Lerp(s.colorA, s.colorB, k);
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01(k * k * 1.6f - 0.5f));
                if (Hash(x, y, s.seed + 4) > 0.997f) c = s.colorC;           // sparks
                return c;
            }
        }
        return s.colorA;
    }

    // ------------------------------------------------------------------
    // Pac-Man floor: a maze, pellets that get eaten as Pac-Man passes, and four ghosts chasing him round a loop
    // ------------------------------------------------------------------

    private static readonly string[] PacMaze =
    {
        "###################",
        "#o.......#.......o#",
        "#.##.###.#.###.##.#",
        "#.................#",
        "#.##.#.#####.#.##.#",
        "#....#...#...#....#",
        "####.###.#.###.####",
        "####.#.......#.####",
        "####.#.##-##.#.####",
        "......  #   #......",
        "####.#.#####.#.####",
        "####.#.......#.####",
        "####.#.#####.#.####",
        "#........#........#",
        "#.##.###.#.###.##.#",
        "#o.#...........#.o#",
        "##.#.#.#####.#.#.##",
        "#....#...#...#....#",
        "#.######.#.######.#",
        "#.................#",
        "###################",
    };

    // Corners of the loop Pac-Man runs (column, row; rows counted from the top). Every step between them is open.
    private static readonly Vector2Int[] PacWaypoints =
    {
        new Vector2Int(1, 3), new Vector2Int(4, 3), new Vector2Int(4, 13), new Vector2Int(1, 13), new Vector2Int(1, 15),
        new Vector2Int(2, 15), new Vector2Int(2, 17), new Vector2Int(1, 17), new Vector2Int(1, 19), new Vector2Int(17, 19),
        new Vector2Int(17, 17), new Vector2Int(16, 17), new Vector2Int(16, 15), new Vector2Int(17, 15), new Vector2Int(17, 13),
        new Vector2Int(14, 13), new Vector2Int(14, 3), new Vector2Int(17, 3), new Vector2Int(17, 1), new Vector2Int(10, 1),
        new Vector2Int(10, 3), new Vector2Int(8, 3), new Vector2Int(8, 1), new Vector2Int(1, 1),
    };

    private static List<Vector2Int> pacPath;
    private static int[] pacPathIndex; // per maze cell: where it is on the loop (-1 = not on it)

    private static readonly Color[] GhostColors =
    {
        new Color(1f, 0.1f, 0.1f), new Color(1f, 0.6f, 0.85f), new Color(0.2f, 1f, 1f), new Color(1f, 0.65f, 0.2f),
    };

    private static void BuildPacPath()
    {
        if (pacPath != null) return;
        int w = PacMaze[0].Length, h = PacMaze.Length;
        pacPath = new List<Vector2Int>();
        pacPathIndex = new int[w * h];
        for (int i = 0; i < pacPathIndex.Length; i++) pacPathIndex[i] = -1;
        for (int i = 0; i < PacWaypoints.Length; i++)
        {
            Vector2Int a = PacWaypoints[i], b = PacWaypoints[(i + 1) % PacWaypoints.Length];
            Vector2Int step = new Vector2Int(Math.Sign(b.x - a.x), Math.Sign(b.y - a.y));
            for (Vector2Int c = a; c != b; c += step)
            {
                if (pacPathIndex[c.y * w + c.x] < 0) pacPathIndex[c.y * w + c.x] = pacPath.Count;
                pacPath.Add(c);
            }
        }
    }

    /// <summary>Position (cell units, centre of the cell) and heading at 'progress' cells along the loop.</summary>
    private static Vector2 PacAt(float progress, out Vector2 dir)
    {
        int count = pacPath.Count;
        progress = Mathf.Repeat(progress, count);
        int i = Mathf.FloorToInt(progress);
        Vector2Int a = pacPath[i], b = pacPath[(i + 1) % count];
        dir = new Vector2(b.x - a.x, b.y - a.y);
        float f = progress - i;
        return new Vector2(a.x + 0.5f + dir.x * f, a.y + 0.5f + dir.y * f);
    }

    private static bool PacWall(int c, int r)
    {
        if (r < 0 || r >= PacMaze.Length || c < 0 || c >= PacMaze[0].Length) return false;
        return PacMaze[r][c] == '#';
    }

    private static Color ShadePacMan(FloorStyle s, float u, float v)
    {
        BuildPacPath();
        int w = PacMaze[0].Length, h = PacMaze.Length;
        float t = shadeTime;
        // The maze fills the height of the texture and is centred across it; rows run from the top.
        float gx = u * h - (h - w) * 0.5f, gy = (1f - v) * h;
        if (gx < 0f || gx >= w) return s.colorC;
        int ci = Mathf.Clamp(Mathf.FloorToInt(gx), 0, w - 1), ri = Mathf.Clamp(Mathf.FloorToInt(gy), 0, h - 1);
        float lx = gx - ci, ly = gy - ri;
        char cell = PacMaze[ri][ci];

        // Characters first (they are drawn over everything).
        const float speed = 5f;                         // cells per second
        float progress = t * speed;
        Vector2 here = new Vector2(gx, gy);

        for (int g = 0; g < GhostColors.Length; g++)
        {
            Vector2 gp = PacAt(progress - 5f - g * 4f, out Vector2 gd);
            Vector2 d = here - gp;
            if (Mathf.Abs(d.x) > 0.5f || Mathf.Abs(d.y) > 0.5f) continue;
            const float R = 0.42f;
            bool body = d.y < 0f ? d.magnitude < R : (Mathf.Abs(d.x) < R && d.y < R);
            // Wavy skirt along the bottom.
            if (body && d.y > R - 0.12f && Mathf.Sin((d.x + t * 0.8f) * 30f) > 0f) body = false;
            if (!body) continue;
            for (int e = -1; e <= 1; e += 2)
            {
                Vector2 eye = d - new Vector2(e * 0.15f, -0.08f);
                if ((eye - gd * 0.05f).magnitude < 0.05f) return new Color(0.1f, 0.2f, 0.9f);  // pupil looks where it goes
                if (eye.magnitude < 0.11f) return Color.white;
            }
            return GhostColors[g];
        }

        Vector2 pp = PacAt(progress, out Vector2 pd);
        Vector2 pdl = here - pp;
        if (pdl.magnitude < 0.45f)
        {
            float mouth = 0.75f * Mathf.Abs(Mathf.Sin(t * 12f));          // half-angle of the open mouth (radians)
            float facing = Vector2.Angle(pdl, pd) * Mathf.Deg2Rad;
            if (pdl.magnitude < 0.06f || facing > mouth) return new Color(1f, 0.93f, 0.1f);
        }

        // Maze.
        if (cell == '#')
        {
            // A blue line along each side that faces a corridor.
            const float lo = 0.12f, hi = 0.24f;
            bool line =
                (!PacWall(ci - 1, ri) && ci > 0 && lx >= lo && lx <= hi) ||
                (!PacWall(ci + 1, ri) && ci < w - 1 && lx <= 1f - lo && lx >= 1f - hi) ||
                (!PacWall(ci, ri - 1) && ri > 0 && ly >= lo && ly <= hi) ||
                (!PacWall(ci, ri + 1) && ri < h - 1 && ly <= 1f - lo && ly >= 1f - hi);
            return line ? s.colorA : s.colorC;
        }
        if (cell == '-') return Mathf.Abs(ly - 0.5f) < 0.08f ? new Color(1f, 0.7f, 0.85f) : s.colorC;

        // Pellets: hidden once Pac-Man has passed them this lap.
        if (cell == '.' || cell == 'o')
        {
            int at = pacPathIndex[ri * w + ci];
            float lap = Mathf.Repeat(progress, pacPath.Count);
            bool eaten = at >= 0 && at <= lap;
            if (!eaten)
            {
                float dd = new Vector2(lx - 0.5f, ly - 0.5f).magnitude;
                if (cell == '.' && dd < 0.11f) return s.colorB;
                if (cell == 'o' && dd < 0.3f && Frac(t * 2f) < 0.6f) return s.colorB;  // blinking power pellet
            }
        }
        return s.colorC;
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
        // Sizes changed after these styles first shipped (they looked too zoomed in): bring saved copies up to date once.
        if (defaultsVersion < 1)
        {
            foreach (FloorStyle st in styles)
            {
                if (st == null) continue;
                switch (st.name)
                {
                    case "Yu-Gi-Oh Card Back": st.stretchToFloor = true; st.tileWorldSize = 8f; st.resolution = Mathf.Max(st.resolution, 1024); break;
                    case "Liquid Flow": st.tileWorldSize = 3f; break;
                    case "Moving Clouds": st.tileWorldSize = 5f; break;
                    case "Kaleidoscope": st.stretchToFloor = false; st.tileWorldSize = 8f; break;
                    case "Pac-Man": st.stretchToFloor = false; st.tileWorldSize = 8f; break;
                }
            }
            defaultsVersion = 1;
        }

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

        // ---- Dragon Ball themed ----
        yield return S("Tournament Stage", FloorPattern.TournamentTiles, new Color(0.86f, 0.84f, 0.78f), new Color(0.72f, 0.7f, 0.64f),
                       new Color(0.3f, 0.28f, 0.26f), 256, false, 6f, 4, 0.25f);

        FloorStyle radar = S("Dragon Radar", FloorPattern.DragonRadar, new Color(0.04f, 0.2f, 0.08f), new Color(0.3f, 0.8f, 0.35f),
                             new Color(1f, 0.55f, 0.05f), 256, false, 8f, 8, 0.7f, 0.9f);
        radar.pulseSpeed = 0.8f; radar.pulseAmount = 0.45f;
        yield return radar;

        yield return S("Dragon Balls", FloorPattern.DragonBalls, new Color(1f, 0.72f, 0.2f), new Color(0.8f, 0.35f, 0.02f),
                       new Color(0.1f, 0.12f, 0.25f), 256, false, 5f, 4, 0.85f);

        // ---- Dragon Ball Z themed ----
        yield return S("Planet Namek", FloorPattern.PixelGrass, new Color(0.15f, 0.55f, 0.45f), new Color(0.3f, 0.72f, 0.55f),
                       new Color(0.95f, 0.85f, 0.3f), 48, true, 4f, 1, 0.05f);

        FloorStyle ssj = S("Super Saiyan Aura", FloorPattern.KiAura, new Color(0.35f, 0.18f, 0.0f), new Color(1f, 0.78f, 0.1f),
                           new Color(1f, 1f, 0.85f), 256, false, 6f, 1, 0.6f, 1.3f);
        ssj.scrollSpeed = new Vector2(0f, 0.35f); ssj.pulseSpeed = 1.5f; ssj.pulseAmount = 0.3f;
        yield return ssj;

        yield return S("Battle Crater", FloorPattern.Craters, new Color(0.62f, 0.5f, 0.36f), new Color(0.48f, 0.38f, 0.27f),
                       new Color(0.2f, 0.15f, 0.1f), 256, false, 8f, 5, 0.1f);

        FloorStyle gravity = S("Gravity Room", FloorPattern.GravityRoom, new Color(0.62f, 0.64f, 0.68f), new Color(0.25f, 0.26f, 0.3f),
                               new Color(1f, 0.15f, 0.1f), 256, false, 10f, 4, 0.75f, 0.35f);
        gravity.metallic = 0.7f; gravity.pulseSpeed = 0.6f; gravity.pulseAmount = 0.5f;
        yield return gravity;

        yield return S("Time Chamber", FloorPattern.Checker, new Color(0.98f, 0.98f, 0.97f), new Color(0.93f, 0.93f, 0.92f),
                       Color.white, 64, false, 6f, 2, 0.9f);

        // ---- Dragon Ball Super themed ----
        FloorStyle top = S("Tournament of Power", FloorPattern.PowerArena, new Color(0.62f, 0.66f, 0.74f), new Color(0.42f, 0.45f, 0.55f),
                           new Color(0.4f, 0.85f, 1f), 256, false, 6f, 6, 0.45f, 0.45f);
        top.pulseSpeed = 0.4f; top.pulseAmount = 0.4f;
        yield return top;

        FloorStyle ui = S("Ultra Instinct Aura", FloorPattern.KiAura, new Color(0.12f, 0.14f, 0.25f), new Color(0.65f, 0.75f, 1f),
                          new Color(1f, 1f, 1f), 256, false, 6f, 1, 0.8f, 1.2f);
        ui.scrollSpeed = new Vector2(0f, 0.25f); ui.pulseSpeed = 0.7f; ui.pulseAmount = 0.35f; ui.seed = 91;
        yield return ui;

        FloorStyle blue = S("Super Saiyan Blue Aura", FloorPattern.KiAura, new Color(0.0f, 0.12f, 0.3f), new Color(0.1f, 0.7f, 1f),
                            new Color(0.85f, 1f, 1f), 256, false, 6f, 1, 0.7f, 1.3f);
        blue.scrollSpeed = new Vector2(0f, 0.35f); blue.pulseSpeed = 1.2f; blue.pulseAmount = 0.3f; blue.seed = 57;
        yield return blue;

        FloorStyle god = S("Super Saiyan God Aura", FloorPattern.KiAura, new Color(0.3f, 0.02f, 0.05f), new Color(1f, 0.25f, 0.3f),
                           new Color(1f, 0.85f, 0.75f), 256, false, 6f, 1, 0.6f, 1.3f);
        god.scrollSpeed = new Vector2(0f, 0.3f); god.pulseSpeed = 1f; god.pulseAmount = 0.3f; god.seed = 73;
        yield return god;

        FloorStyle hakai = S("Destruction Energy", FloorPattern.EnergyWisps, new Color(0.04f, 0.0f, 0.08f), new Color(0.55f, 0.1f, 0.85f),
                             new Color(1f, 0.7f, 1f), 256, false, 7f, 1, 0.6f, 1.2f);
        hakai.scrollSpeed = new Vector2(0.02f, 0.035f); hakai.pulseSpeed = 0.5f; hakai.pulseAmount = 0.4f;
        yield return hakai;

        // ---- Yu-Gi-Oh themed ----
        FloorStyle card = S("Yu-Gi-Oh Card Back", FloorPattern.CardBack, new Color(0.95f, 0.55f, 0.15f), new Color(0.35f, 0.17f, 0.06f),
                            new Color(0.06f, 0.04f, 0.03f), 1024, false, 8f, 1, 0.55f);
        card.stretchToFloor = true;
        yield return card;

        // ---- Animated ----
        FloorStyle liquid = S("Liquid Flow", FloorPattern.Liquid, new Color(0.02f, 0.15f, 0.35f), new Color(0.1f, 0.55f, 0.75f),
                              new Color(0.85f, 1f, 1f), 128, false, 3f, 2, 0.95f, 0.25f);
        liquid.animateFps = 15f;
        yield return liquid;

        FloorStyle clouds = S("Moving Clouds", FloorPattern.CloudSea, new Color(0.35f, 0.6f, 0.9f), new Color(0.72f, 0.78f, 0.88f),
                              Color.white, 256, false, 5f, 1, 0.2f);
        clouds.scrollSpeed = new Vector2(0.015f, 0.006f);
        yield return clouds;

        FloorStyle kaleido = S("Kaleidoscope", FloorPattern.Kaleidoscope, new Color(0.02f, 0.02f, 0.05f), Color.white,
                               new Color(0.05f, 0.05f, 0.08f), 256, false, 8f, 8, 0.7f, 0.6f);
        kaleido.animateFps = 15f;
        yield return kaleido;

        FloorStyle pac = S("Pac-Man", FloorPattern.PacMan, new Color(0.15f, 0.25f, 1f), new Color(1f, 0.8f, 0.65f),
                           Color.black, 256, true, 8f, 1, 0.4f, 0.8f);
        pac.animateFps = 20f;
        yield return pac;
    }
}
