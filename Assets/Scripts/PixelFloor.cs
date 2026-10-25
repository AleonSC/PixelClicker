using Unity.Profiling;
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
/// Part files: PixelFloorShade.cs (drawing the textures, Shade and its pattern groups, Pac-Man), PixelFloorDefaults.cs (the built-in styles and their
/// upgrades), plus the style batches and features in PixelFloorLayers / Scenes / Retro / Worlds / Live / Planets / Modes / Fade.
/// </summary>
public partial class PixelFloor : MonoBehaviour, IPixelLookSource
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
        PacMan,          // maze with a chomping Pac-Man, pellets and ghosts (animated)
        RoadRacer,       // 8-bit top-down road: a car weaving through traffic as the road scrolls by (animated)
        Fighter,         // 8-bit side-view fight: two fighters trade punches and kicks, health bars, K.O. (animated)
        NeonDrift,       // neon grid that slides along while its colours cycle (animated)
        NeonMerge,       // neon grid whose lines fade out and back so the squares grow into bigger squares (animated)
        NeonWave,        // neon grid that ripples like cloth while its colours cycle (animated)
        NeonPrism,       // neon grid with a rainbow that sweeps across it (animated)
        LaserHall,       // teal glass-and-metal corridor floor with red security laser beams sweeping and blinking across it (animated)
        BattleCrater,    // scorched, cracked ground with shaded craters, burnt halos and glowing embers
        Galaxy,          // a slowly turning spiral galaxy (animated)
        BlackHole,       // a black hole with a glowing accretion disk and lensed stars (animated)
        Hyperspace,      // stars streaking outwards at light speed (animated)
        DataStream,      // lanes of glowing data packets racing past (animated)
        CircuitFlow,     // circuit traces with light pulses running along them (animated)
        CyberHex,        // hex cells pulsing with a scanning band (animated)
        Glitch,          // colour bars torn by digital glitches (animated)
        Psychedelic,     // flowing rainbow rings (animated)
        HypnoSpiral,     // a hypnotic rotating spiral (animated)
        Plasma,          // classic sine plasma (animated)
        OpArt,           // a warping bulging checkerboard (animated)
        BloodBoards,     // dark wooden boards with blood pools
        HauntedTiles,    // filthy cracked tiles under a flickering light (animated)
        Graveyard,       // grave soil with dead grass, bones and drifting fog (animated)
        WatchingEyes,    // darkness full of glowing eyes that blink (animated)
        NoirSmoker,      // a noir profile silhouette smoking a cigarette (animated)
        Skyline,         // a night city skyline reflected in water (animated)
        GroundSky,       // ground under a sky: meadow, desert, arctic, alien (cells 1-4 picks the scene)
        MineTrack,       // gravel with rails and wooden sleepers
        OreVeins,        // dark rock with glittering metal veins and crystals
        Strata,          // layers of sediment
        CropRows,        // ploughed rows with seedlings
        WheatField,      // swaying golden wheat (animated)
        Patchwork,       // a patchwork of fields seen from the air
        SynthSun,        // retrowave: a striped sun over mountains and a neon grid rushing past (animated)
        NeonHighway,     // retrowave: a neon road into a night city with tail lights (animated)
        GridTunnel,      // retrowave: a neon tunnel rushing past (animated)
        RainAlley,       // cyberpunk: wet asphalt with neon puddles, ripples and rain (animated)
        VaporChecker,    // vaporwave: a pastel checkerboard sliding towards a big sun (animated)
        CityMap,         // cyberpunk: an aerial night city with glowing roofs and traffic (animated)
        LightCycles,     // a dark grid with light trails racing along its lines (animated)
        ArcadeCarpet,    // 80s arcade carpet: neon confetti on a dark ground (animated)
        Platformer,      // 8-bit side scroller: hills, pipes, blocks, a running hero (animated)
        Invaders,        // 8-bit shoot-em-up: marching alien rows and a cannon (animated)
        BrickBreaker,    // 8-bit brick breaker with a bouncing ball (animated)
        PongCourt,       // 8-bit pong court (animated)
        SnakeGrid,       // 8-bit green LCD snake (animated)
        BlockStack,      // 8-bit falling blocks in a well (animated)
        Dungeon,         // 8-bit top-down dungeon: tiles, carpet, braziers, a hero and a slime (animated)
        Crossing,        // 8-bit crossing: traffic, a river and a hopping frog (animated)
        LushGrass,       // big meadow: blades, flower patches and a dirt trail (still, one big picture)
        NebulaField,     // warped gas clouds, dust lanes, stars with spikes, a gas giant and a moon
        StillPond,       // pond water with caustics, ripples, lily pads, lotus flowers and koi
        GraffitiWall,    // a brick wall laid flat with a big bubble-letter tag, drips and paint-over patches
        LeafLitter,      // soil under two layers of autumn leaves and twigs
        ZenGarden,       // raked sand that rings round mossy rocks
        PersianRug,      // (removed: Persian Rug is gone; the value stays so the numbers after it keep their meaning)
        CoralReef,       // seabed with brain / polyp / fan corals, seagrass, starfish and caustics
        CasinoFelt,      // green baize with gold rings, chips, dice and cards
        StainedGlass,    // jewel panes in lead with a rose window under the cube
        MoonSurface,     // grey regolith, dark basalt seas, a terraced crater with a central peak, ray and secondary crater chains, rilles, boulders and rover tracks
        MarsSurface,     // rust dunes with lee slopes, dark basalt streaks, dust-filled craters, cracked lakebed, boulders and dust devil trails
        WildMeadow,      // big meadow drawn blade by blade: hue patches, wind-bent blades, wildflower clusters, seed heads (PixelFloorLive.cs)
        CloverLawn,      // mown lawn with curving mower stripes, clover patches, daisies, worn spots and a fairy ring of mushrooms
        MossyGlade,      // forest floor: speckled moss, ferns, mossy stones, twigs and little puddles
        PrairieGrass,    // tall golden wind-swept grass with green patches, purple wildflowers and rocks
        NeonLasers,      // see-through floor: neon laser beams drifting and pulsing over nothing (3D beams, PixelFloorOverlay)
        LaserTracker,    // see-through floor: laser beams from emitters round the cube that all converge on the mouse pointer
        PicnicBlanket,   // a gingham picnic blanket on grass, with a little 3D ant that follows the mouse
        CrumbleTiles     // see-through floor: real stone tiles that crack loose and fall away one by one, then rebuild (PixelFloorOverlay Crumble)
    }

    /// <summary>Live 3D extras drawn on top of (or instead of) the floor picture, updated every frame (PixelFloorOverlay).</summary>
    public enum FloorOverlay { None = 0, Lasers = 1, LaserTracker = 2, Ant = 3, Crumble = 4, Caustics = 5 }

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

        [Tooltip("Width / height of one copy of the pattern on the floor (1 = square). Tile World Size is its height.")]
        public float tileAspect = 1f;

        [Tooltip("Fit ONE copy of the pattern to the part of the floor the camera sees (between the black bars), following the camera as it moves. Tile World Size is ignored.")]
        public bool fitToView = false;

        [Tooltip("Big textures that cover a large area with one copy: multiplies the noise lattices so features keep their size (0 / 1 = off).")]
        public int detail = 0;

        [Tooltip("Painted pictures that are ONE stretched copy: make the picture a little narrower than tall by the camera's tilt (sin of the base view's pitch), so round things do not look squashed from above at an angle.")]
        public bool tiltCorrect = false;

        [Tooltip("Pattern variation (crater floors: 0 = battle, 1 = moon, 2 = red planet).")]
        public int variant = 0;

        [Tooltip("Draw the ground with the world-space ground shader instead of a painted texture (0 = painted texture, 1 lava, 2 marble, 3 ice, 4 deep space, 5 data streams, 6 aura flames, 7 death energy wisps, 8 matrix rain, 9 liquid flow, 10 laser corridor). The picture then never repeats or stretches and stays sharp at any zoom. The menus' preview pictures stay painted.")]
        public int shaderGround = 0;

        [Tooltip("World units one 'cell' of the shader pattern spans (0 = the default 6). Patterns 6-10 (ports of painted floors): the world units one copy of the painted picture spanned, i.e. its Tile World Size.")]
        public float shaderScale = 0f;

        [Tooltip("Hide the floor picture itself (a see-through floor: only the sky and the overlay show; the floor still catches pixels).")]
        public bool hideBase = false;

        [Tooltip("Live 3D extras drawn over the floor every frame: lasers, lasers that converge on the mouse, an ant that follows the mouse.")]
        public FloorOverlay overlay = FloorOverlay.None;

        public FloorStyle Clone() => (FloorStyle)MemberwiseClone();
    }

    [Header("Floor")]
    [Tooltip("The floor's renderer. Empty = found automatically (collider below the cube, else an object named like one in Floor Names).")]
    [SerializeField] private Renderer floorRenderer;

    [Tooltip("Object names tried when no floor renderer is set and nothing is found below the cube.")]
    [SerializeField] private string[] floorNames = { "Floor", "Ground", "Plane" };

    [Header("Game-made floor")]
    [Tooltip("Use the floor object that is in the scene instead of the floor this component builds itself (a flat sheet with a collider, ten units under the cube by default). Off = the scene floor is hidden and its collider switched off.")]
    [SerializeField] private bool useSceneFloor = false;

    [Min(0f)]
    [Tooltip("Side length (world units) of the floor the game builds itself. 0 = the default (80).")]
    [SerializeField] private float ownFloorSize = 0f;

    [Min(0f)]
    [Tooltip("How far (world units) the game-made floor lies under the cube's resting spot - the falling distance of the husks. 0 = the default (10).")]
    [SerializeField] private float groundFallDistance = 0f;

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

    // Fit-to-view: an affine map from the floor mesh's local points to its UVs (from one of its triangles).
    private bool uvMapReady;
    private Vector3 uvP0, uvE1, uvE2;
    private Vector2 uvT0, uvD1, uvD2;
    private Vector3 lastFitCamPos;
    private Quaternion lastFitCamRot;
    private float lastFitAspect, lastFitOrtho;

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
        if (fitCam != null) Destroy(fitCam.gameObject);
        if (ownFloor != null) Destroy(ownFloor);
        if (Instance == this) Instance = null;
        DropProxies(true);
        if (flatBase != null) Destroy(flatBase);
        if (flatMat != null) Destroy(flatMat);
        DestroyEdgeFade();
        if (plane != null) Destroy(plane);
        if (planeMat != null) Destroy(planeMat);
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

    private bool floorReady;
    private float floorRetry;
    private int floorTries;
    private GameObject ownFloor;

    private void Start() { InitFloor(); }

    /// <summary>Finds the floor and applies the saved style. Called at start and, while no floor was found, once a second (the scene may not have been ready).</summary>
    private void InitFloor()
    {
        if (floorReady) return;
        if (!useSceneFloor)
        {
            // The game's own floor: ten units under the cube. A scene floor, if there is one, is switched off.
            Renderer scene = floorRenderer != null && !IsOwnFloor(floorRenderer) ? floorRenderer : FindFloor();
            floorRenderer = BuildOwnFloor(scene);
            if (floorRenderer == null) return;   // (no PixelClicker yet: try again)
        }
        else
        {
            if (floorRenderer == null) floorRenderer = FindFloor();
            if (floorRenderer == null)
            {
                floorTries++;
                if (floorTries >= 3) floorRenderer = BuildOwnFloor(null);   // no floor in the scene: make one
                if (floorRenderer == null) return;
            }
        }
        floorReady = true;
        originalMaterials = floorRenderer.sharedMaterials;
        BuildUvMap();

        string saved = PixelPrefs.GetString(PrefStyle, defaultStyle);
        switch (saved)   // styles that were renamed
        {
            case "Dragon Radar": saved = "Radar"; break;
            case "Super Saiyan Aura": saved = "Super Aura"; break;
            case "Ultra Instinct Aura": saved = "Divine Aura"; break;
            case "Super Saiyan Blue Aura": saved = "Blue Aura"; break;
            case "Super Saiyan God Aura": saved = "God Aura"; break;
            case "Destruction Energy": saved = "Death Energy"; break;
            case "Planet Namek": saved = "Alien Planet"; break;
            case "Time Chamber": saved = "Chamber Tiles"; break;
            case "Tournament of Power": saved = "Power Tournament"; break;
        }
        int index = styles.FindIndex(s => s.name == saved);
        SetStyle(index >= 0 ? index : 0);
    }

    // ------------------------------------------------------------------
    // Finding the floor
    // ------------------------------------------------------------------

    private bool IsOwnFloor(Renderer r) => ownFloor != null && r != null && r.gameObject == ownFloor;

    /// <summary>
    /// Builds the game's own floor: a flat sheet (a code-made quad, UVs 0..1 like a Unity plane) with a thin box collider, so the game never depends on a floor
    /// object in the scene. It lies 'groundFallDistance' under the cube; with a scene floor ('scene') it takes that floor's collider material and switches the scene floor off.
    /// </summary>
    private Renderer BuildOwnFloor(Renderer scene)
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null) return null;
        float size = ownFloorSize > 1f ? ownFloorSize : 80f;
        float y = clicker.CubeBasePosition.y - (groundFallDistance > 0f ? groundFallDistance : 10f);
        Vector3 centre = clicker.CubeBasePosition;

        ownFloor = new GameObject("Pixel Ground");
        ownFloor.transform.position = new Vector3(centre.x, y, centre.z);
        float h = size * 0.5f;
        Mesh mesh = new Mesh { name = "Pixel Ground" };
        mesh.vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        ownFloor.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = ownFloor.AddComponent<MeshRenderer>();
        Material mat = clicker.CreateVisualMaterial(new Color(0.55f, 0.55f, 0.58f), false);
        if (mat != null) mr.sharedMaterial = mat;

        BoxCollider box = ownFloor.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, -0.5f, 0f);
        box.size = new Vector3(size, 1f, size);
        bool copied = false;
        if (scene != null)
        {
            Collider sc = scene.GetComponent<Collider>();
            copied = PixelPhysicsMaterials.CopyOf(sc, box);   // (the scene ground's own bounce / friction, if it has a material)
            if (sc != null) sc.enabled = false;
            scene.enabled = false;
        }
        if (!copied) PixelPhysicsMaterials.Apply(box, "Pixel Ground", 0.1f, 0.6f);
        return mr;
    }

    /// <summary>The dev shape placer's shapes are never the floor.</summary>
    private static bool IsDevShape(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == "Dev Shapes" || t.name.StartsWith("Dev Shape ")) return true;
        return false;
    }

    private Renderer FindFloor()
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker != null)
        {
            // The first solid, non-moving collider straight below the cube.
            RaycastHit[] hits = Physics.RaycastAll(clicker.transform.position + Vector3.up * 0.5f, Vector3.down, 500f,
                                                   ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(clicker.transform) || IsDevShape(hit.collider.transform) || hit.collider.gameObject == ownFloor) continue;
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

        // Last resort: the biggest wide, flat mesh in the scene that lies below the cube.
        if (clicker != null)
        {
            Renderer best = null;
            float bestArea = 0f;
            Renderer[] everything = PixelFind.All<Renderer>();
            foreach (Renderer r in everything)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || !(r is MeshRenderer) || IsDevShape(r.transform)) continue;
                if (r.transform.IsChildOf(clicker.transform)) continue;
                Bounds b = r.bounds;
                if (b.size.y > Mathf.Max(2f, Mathf.Min(b.size.x, b.size.z) * 0.15f) || b.max.y > clicker.transform.position.y + 0.5f) continue;
                float area = b.size.x * b.size.z;
                if (area > bestArea) { bestArea = area; best = r; }
            }
            if (best != null && bestArea > 16f) return best;
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
        PixelPrefs.SetString(PrefStyle, styles[index].name);
        Apply(styles[index]);
        StyleChanged?.Invoke(index);
    }

    /// <summary>Steps to the next (+1) or previous (-1) style.</summary>
    public void Step(int direction) => SetStyle((current < 0 ? 0 : current) + direction);

    private void Apply(FloorStyle style)
    {
        if (floorRenderer == null) return;
        DropProxies(false);   // the style sets the real materials itself; the fade rebuilds its copies if it is still going
        ReleaseActive();
        ApplyLive(style);   // the live overlay (lasers, ant) and the see-through switch (PixelFloorLive.cs)
        offset = Vector2.zero;
        beatTimer = 0f;

        if (style.pattern == FloorPattern.Classic)
        {
            floorRenderer.sharedMaterials = originalMaterials;
            return;
        }

        if (style.shaderGround > 0 && ApplyShaderGround(style)) return;

        Material source = originalMaterials != null && originalMaterials.Length > 0 ? originalMaterials[0] : null;
        if (source == null)
        {
            PixelClicker clicker = PixelFind.Cached<PixelClicker>();
            source = clicker != null ? clicker.CreateVisualMaterial(Color.white, false) : null;
        }
        if (source == null) return;

        shadeAspect = style.fitToView ? viewAspect : 1f;
        SetCraterFocus(style);
        bool sliced = UsesSlicedFill(style);
        activeTexture = BuildTexture(style, 0, sliced);
        if (sliced) StartSlicedFill(activeTexture, style);
        Shader faded = fadeIntoSky && !disableEdgeFade ? FadedShader() : null;
        activeMaterial = faded != null ? new Material(faded) { name = "Floor (faded " + style.name + ")" } : new Material(source) { name = "Floor (" + style.name + ")" };
        fadedTextureOn = faded != null;
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
        float tile = EffectiveTileSize(style);   // (a stretched picture is at least as big as the standard floor area)
        // Tile by world size so every pattern has the same scale on any floor, centred on the floor's middle.
        float aspect = StyleAspect(style);
        baseTiling = new Vector2(Mathf.Max(0.02f, size.x / (tile * aspect)), Mathf.Max(0.02f, size.z / tile));
        baseOffset = new Vector2(0.5f - Frac(0.5f * baseTiling.x), 0.5f - Frac(0.5f * baseTiling.y));
        // A single clamped copy must sit exactly on the floor's middle: the repeating formula above jumps a whole copy away once the floor is 2+ pictures wide, which showed only the picture's edge colour.
        if (style.stretchToFloor) baseOffset = new Vector2(0.5f - 0.5f * baseTiling.x, 0.5f - 0.5f * baseTiling.y);
        animTimer = animClock = 0f;
        if (style.fitToView) FitToView(true);
        ApplyTiling(m, style);

        Material[] mats = new Material[Mathf.Max(1, originalMaterials != null ? originalMaterials.Length : 1)];
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        floorRenderer.sharedMaterials = mats;
    }

    private void ReleaseActive()
    {
        StopSlicedFill();
        ClearLayers();
        if (activeMaterial != null) Destroy(activeMaterial);
        if (activeTexture != null) Destroy(activeTexture);
        activeMaterial = null;
        activeTexture = null;
        groundShaderOn = false;
        fadedTextureOn = false;
    }

    private void Update()
    {
        if (!floorReady)
        {
            floorRetry -= Time.unscaledDeltaTime;
            if (floorRetry <= 0f) { floorRetry = 1f; InitFloor(); }
            return;
        }
        UpdateGroundHeight();
        if (activeMaterial == null || current < 0 || current >= styles.Count) return;
        FloorStyle style = styles[current];
        float dt = PixelTimeStop.IsStopped ? 0f : Time.unscaledDeltaTime; // keeps moving on the title screen, freezes in Time Stop
        if (groundShaderOn) { TickGroundShader(style, dt); return; }   // (the shader draws, scrolls and pulses itself: no tiling or redraws)

        bool tilingDirty = false;
        if (style.fitToView && FitToView(false)) tilingDirty = true;
        if (style.scrollSpeed != Vector2.zero)
        {
            offset += style.scrollSpeed * dt;
            offset.x -= Mathf.Floor(offset.x);
            offset.y -= Mathf.Floor(offset.y);
            tilingDirty = true;
        }
        float blend = FitBlend(style);
        if (!Mathf.Approximately(blend, lastBlend)) { lastBlend = blend; tilingDirty = true; }
        if (tilingDirty) ApplyTiling(activeMaterial, style);

        if (style.glow > 0f && style.pulseSpeed > 0f && activeMaterial.HasProperty("_EmissionColor"))
        {
            pulseClock += dt;
            float wave = 0.5f + 0.5f * Mathf.Sin(pulseClock * style.pulseSpeed * Mathf.PI * 2f);
            activeMaterial.SetColor("_EmissionColor", Color.white * style.glow * (1f - style.pulseAmount * wave));
        }

        if (style.pattern == FloorPattern.HauntedTiles && dt > 0f) UpdateHauntedFlicker();

        // An animated floor nobody can see (faded out on the main menu, or switched off in Settings) is not redrawn at all.
        bool unseen = visibilityInit && visibility <= 0.001f && !(PixelGameMode.TitleShowing && !PixelTitleWanderers.LiveBackground);
        if (style.animateFps > 0f && dt > 0f && activeTexture != null && fillRoutine == null && !unseen)
        {
            animClock += dt * style.animSpeed;
            animTimer += dt;
            // While the camera swings the redraws slow down (a big texture re-filled every frame is what made the pan stutter); no floor is redrawn faster than the cap.
            float fps = PixelCameraIntro.Progress > 0.001f && PixelCameraIntro.Progress < 0.999f ? Mathf.Min(style.animateFps, 8f) : style.animateFps;
            fps = Mathf.Min(fps, animFpsCap > 0f ? animFpsCap : 10f);
            if (animTimer >= 1f / fps)
            {
                animTimer = 0f;
                shadeAspect = style.fitToView ? viewAspect : 1f;
                using (redrawMarker.Auto()) Fill(activeTexture, style, animClock, true);
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
    // Fit to view: one copy of the pattern exactly covers what the camera sees of the floor
    // ------------------------------------------------------------------

    private static readonly ProfilerMarker redrawMarker = new ProfilerMarker("Pixel.FloorRedraw");
    private float lastBlend = 1f;

    /// <summary>1 = the picture is fitted to the view (normal); less while the camera is away from its normal view: a fitted style then spreads over the WHOLE floor once instead of repeating.</summary>
    private float FitBlend(FloorStyle style) => style.fitToView ? Mathf.Clamp01(PixelCameraIntro.Progress) : 1f;

    /// <summary>Sets the floor's tiling: the normal one, or for a fitted style a blend from 'the picture over the whole floor' to the fitted view.</summary>
    private void ApplyTiling(Material m, FloorStyle style)
    {
        float e = FitBlend(style);
        Vector2 tiling = baseTiling, off = baseOffset + offset;
        if (e < 0.9999f && TryWholeFloorTiling(out Vector2 wt, out Vector2 wo))
        {
            tiling = Vector2.Lerp(wt, baseTiling, e);
            off = Vector2.Lerp(wo, baseOffset, e) + offset;
        }
        SetTiling(m, tiling, off);
        if (proxyMats != null) foreach (Material p in proxyMats) if (p != null) SetTiling(p, tiling, off);   // the fading copies follow
    }

    // ------------------------------------------------------------------
    // Fading the floor in and out (the main menu has no floor, only the sky)
    // ------------------------------------------------------------------

    [Header("Animation cost")]
    [Min(0f)]
    [Tooltip("No animated floor is redrawn more often than this many times a second (0 = 10). Lower = cheaper; the floors look fine at 8-10.")]
    [SerializeField] private float animFpsCap = 0f;

    [Header("Fade")]
    [Tooltip("Tick to keep the floor always visible. (Normally, with 'Live background' on, the main menu and the camera swing show only the sky: the floor fades in once the camera has settled and fades out before it swings back.)")]
    [SerializeField] private bool keepFloorOnTitle = false;

    [Min(0f)]
    [Tooltip("Seconds the floor takes to fade in or out. 0 = 1.")]
    [SerializeField] private float floorFadeSeconds = 0f;

    private bool HidesOnTitle => !keepFloorOnTitle;

    /// <summary>The 'Display floor' switch is off: the floor is never drawn (its collider stays so pixels still land).</summary>
    private static bool FloorOff => !PixelTitleWanderers.DisplayFloor || PixelFirstPerson.Active;   // (first person has its own neon grid, PixelFirstPersonGrid)
    private const float FadeOutFactor = 0.5f;   // leaving (quit to the main menu) is quicker than arriving
    private const float FadeInFactor = 2.5f;   // the floor texture fades IN this much slower than it fades out
    private float FadeTime => floorFadeSeconds > 0.05f ? floorFadeSeconds : 1f;

    private float visibility = 1f;
    private bool visibilityInit, exitHold;
    private Material[] realMats, proxyMats;
    private System.Action exitDone;

    /// <summary>0 = the floor is hidden, 1 = fully shown (the fog sheet follows it).</summary>
    public float FloorVisibility => visibility;

    /// <summary>True while the floor or the old pixels / devices are still fading (either way).</summary>
    public bool Settling => visibilityInit && ((visibility < 0.999f && !FloorOff) || propsK < 0.999f) && HidesOnTitle;

    /// <summary>Fades the floor out, then calls 'onDone' (the main menu return waits for it before the camera swings back). Returns false (without calling it) if there is nothing to fade.</summary>
    public bool FadeOutThen(System.Action onDone)
    {
        if (floorRenderer == null || !HidesOnTitle || visibility <= 0.001f) return false;
        exitHold = true;
        exitDone = onDone;
        return true;
    }

    /// <summary>Dev Hub tick box 'Double floor area': the floor circle (picture, wireframe, borders and the pixel / first person limits) is twice as wide.</summary>
    public static bool DoubleArea { get; set; }

    /// <summary>How many times as wide the floor area is: the Dev Hub's 'Double floor area' (x2) or the Standard skill's 'Double floor' perk (x2 from level 10, PixelSkills.StandardFloorScale), whichever is bigger.</summary>
    public static float AreaScale => Mathf.Max(DoubleArea ? 2f : 1f, PixelSkills.StandardFloorScale);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAreaScale() { DoubleArea = false; }

    private float appliedAreaScale = 1f;

    /// <summary>Scales the floor object itself (its sheet and collider) when the area setting changes, and makes the picture fit and the edge fade redo themselves.</summary>
    private void ApplyAreaScale()
    {
        float want = AreaScale;
        if (Mathf.Approximately(want, appliedAreaScale)) return;
        float k = want / appliedAreaScale;
        Transform t = floorRenderer.transform;
        t.localScale = new Vector3(t.localScale.x * k, t.localScale.y, t.localScale.z * k);
        appliedAreaScale = want;
        lastFitAspect = -1f;       // (the picture fit is worked out again)
        ringR0 = ringR1 = -1f;     // (so are the wireframe and the borders)
        if (floorReady && current >= 0 && current < styles.Count) Apply(styles[current]);   // (the picture is drawn again at the new size)
    }

    private void LateUpdate()
    {
        if (floorRenderer == null) return;
        ApplyAreaScale();
        if (exitHold && PixelGameMode.TitleShowing) exitHold = false;
        bool hide = HidesOnTitle && (exitHold || (PixelTitleWanderers.LiveBackground && PixelGameMode.TitleOrIntro));
        // Old pixels and devices go first when leaving and come last when arriving: they fade out, THEN the floor; the floor fades in, THEN they do.
        // (Props only wait for the floor after a real arrival - the title screen / leaving it. A floor fade-in for any other reason, e.g. coming back
        // from first person, where the floor is off, must not shrink every husk and device away and back.)
        if (hide) propsWaitForFloor = true;
        float propsTarget = !HidesOnTitle ? 1f : (hide ? 0f : ((!propsWaitForFloor || visibility >= 0.999f || FloorOff) ? 1f : 0f));
        float fadeDt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // (a long frame, e.g. while a report is built, must not make the fade jump)
        propsK = Mathf.MoveTowards(propsK, propsTarget, fadeDt / (propsTarget < propsK ? FadeTime * FadeOutFactor : FadeTime));
        if (propsWaitForFloor && !hide && propsK >= 0.999f) propsWaitForFloor = false;
        if (hide && exitHold && propsK > 0.001f) hide = false;   // (the floor stays until the props are gone)
        float target = (hide || FloorOff) ? 0f : 1f;
        if (!visibilityInit) { visibilityInit = true; visibility = target; }
        else visibility = Mathf.MoveTowards(visibility, target, fadeDt / (target > visibility ? FadeTime * FadeInFactor : FadeTime * FadeOutFactor));
        ApplyVisibility();
        UpdatePlane();
        UpdateGroundShader();
        UpdateEdgeFade();
        if (exitDone != null && visibility <= 0.001f)
        {
            System.Action done = exitDone;
            exitDone = null;
            done();
        }
    }

    private const float flatBaseSpread = 30f;
    private GameObject flatBase;
    private Material flatMat;
    private readonly Dictionary<int, Color> averageColors = new Dictionary<int, Color>();

    /// <summary>The floor style's overall colour (Classic: its own colour; else the average of the style's picture).</summary>
    private Color AverageColor()
    {
        if (current < 0 || current >= styles.Count) return new Color(0.4f, 0.4f, 0.4f, 1f);
        if (styles[current].pattern == FloorPattern.Classic) return PreviewColor(current);
        if (averageColors.TryGetValue(current, out Color cached)) return cached;
        Color result = new Color(0.4f, 0.4f, 0.4f, 1f);
        try
        {
            Texture2D tex = PreviewTexture(current) as Texture2D;
            if (tex != null)
            {
                int step = Mathf.Max(1, Mathf.Min(tex.width, tex.height) / 12);
                float r = 0, g = 0, b = 0; int n = 0;
                for (int y = 0; y < tex.height; y += step)
                    for (int x = 0; x < tex.width; x += step) { Color c = tex.GetPixel(x, y); r += c.r; g += c.g; b += c.b; n++; }
                if (n > 0) result = new Color(r / n, g / n, b / n, 1f);
            }
        }
        catch (System.Exception) { }   // (unreadable texture: keep the grey)
        averageColors[current] = result;
        return result;
    }

    /// <summary>While the textured floor is hidden / fading, a flat floor-coloured copy of the floor mesh sits just under it, so the menu background is the floor's colour instead of the sky's lower half.</summary>
    private void UpdateFlatBase()
    {
        bool want = HidesOnTitle && visibility < 0.999f && !FloorOff;
        if (!want)
        {
            if (flatBase != null) flatBase.SetActive(false);
            return;
        }
        MeshFilter mf = floorRenderer.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        if (flatBase == null)
        {
            flatBase = new GameObject("Floor Flat Colour");
            flatBase.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer mr = flatBase.AddComponent<MeshRenderer>();
            flatMat = new Material(PixelShaders.SpriteDefault()) { renderQueue = 1990 };
            mr.sharedMaterial = flatMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        Transform ft = floorRenderer.transform;
        flatBase.transform.SetPositionAndRotation(ft.position + Vector3.down * 0.004f, ft.rotation);
        Vector3 fs = ft.lossyScale;
        Vector3 ext = Vector3.Scale(mf.sharedMesh.bounds.size, fs);   // (only the two biggest directions are widened, the thickness stays)
        int thin = ext.x <= ext.y && ext.x <= ext.z ? 0 : (ext.y <= ext.z ? 1 : 2);
        Vector3 wide = fs * flatBaseSpread;
        flatBase.transform.localScale = new Vector3(thin == 0 ? fs.x : wide.x, thin == 1 ? fs.y : wide.y, thin == 2 ? fs.z : wide.z);
        // The sky's horizon colour, so the sky seems to fill the whole menu view (the floor picked in the menu only decides what loads in).
        Color flat;
        Camera view = ViewCam();
        if (PixelSkybox.Instance != null && PixelSkybox.Instance.TryGetHorizonColor(out Color sky)) flat = sky;
        else if (view != null && view.clearFlags == CameraClearFlags.SolidColor) flat = view.backgroundColor;
        else flat = AverageColor();
        flat.a = 1f;
        flatMat.color = flat;
        flatBase.SetActive(true);
    }

    private float propsK = 1f;
    private bool propsWaitForFloor = true;   // set while the floor is hidden for the title; cleared once the props are back
    private readonly Dictionary<Transform, Vector3> propScales = new Dictionary<Transform, Vector3>();
    private readonly List<Transform> propList = new List<Transform>();
    private readonly List<ParticleSystem> stoppedParticles = new List<ParticleSystem>();
    private bool particlesStopped;

    /// <summary>Old pixels and devices start hidden and fade in (a game was just loaded).</summary>
    public void PropsFadeIn() { if (HidesOnTitle) propsK = 0f; }

    /// <summary>Shrinks every old pixel and placed device to 'propsK' of its size (a quick fade away / back); restores them exactly at 1.</summary>
    private void ApplyProps()
    {
        if (propsK >= 0.999f && propScales.Count == 0) return;
        // Held at the same size (the title screen keeps them hidden for its whole time) with the same husks and devices out: nothing to redo this frame.
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        int propCount = (clicker != null ? clicker.OldPixels.Count : 0) + PixelPlacedDevice.All.Count;
        if (propsK < 0.999f && propsK == lastPropsK && propCount == lastPropCount && Time.unscaledTime < nextPropsCheck) return;   // (checked again twice a second: a husk that was still popping in gets its turn)
        nextPropsCheck = Time.unscaledTime + 0.5f;
        lastPropsK = propsK;
        lastPropCount = propCount;
        propList.Clear();
        if (clicker != null) clicker.CollectOldPixelTransforms(propList);
        IReadOnlyList<PixelPlacedDevice> devices = PixelPlacedDevice.All;
        for (int i = 0; i < devices.Count; i++) if (devices[i] != null && !(devices[i] is PixelFunnelDevice)) propList.Add(devices[i].transform);   // (the Funnel is left out: its cone and tube are pinned to the world, so scaling its root would pull them apart)

        bool done = propsK >= 0.999f;
        float k = Mathf.Max(0.001f, propsK);

        // Particles don't shrink with their parent (they live in world space): stop them emitting while the props are away, wipe what is left once
        // the props are nearly gone, and start the same systems again when everything is back.
        if (!done && !particlesStopped)
        {
            particlesStopped = true;
            stoppedParticles.Clear();
            foreach (Transform t in propList)
            {
                if (t == null) continue;
                foreach (ParticleSystem ps in t.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps != null && ps.isPlaying) { stoppedParticles.Add(ps); ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            }
        }
        if (!done && propsK < 0.15f) foreach (ParticleSystem ps in stoppedParticles) if (ps != null) ps.Clear(true);
        if (done && particlesStopped)
        {
            particlesStopped = false;
            foreach (ParticleSystem ps in stoppedParticles) if (ps != null) ps.Play(true);
            stoppedParticles.Clear();
        }
        foreach (Transform t in propList)
        {
            if (t == null) continue;
            // A husk clicked off the cube DURING the fade is still growing from its tiny pop-in size: its size then is not its real one (remembering it
            // left the husk tiny for good - an 'invisible' husk after loading). It grows normally and is left out until it is full size.
            if (!propScales.ContainsKey(t) && t.GetComponent<OldPixelPopIn>() != null) continue;
            if (!propScales.TryGetValue(t, out Vector3 baseScale)) { baseScale = t.localScale; propScales[t] = baseScale; }
            t.localScale = done ? baseScale : baseScale * k;
        }
        if (done) propScales.Clear();
        else
        {
            List<Transform> gone = null;
            foreach (Transform t in propScales.Keys) if (t == null) (gone ?? (gone = new List<Transform>())).Add(t);
            if (gone != null) foreach (Transform t in gone) propScales.Remove(t);
        }
    }

    private float lastPropsK = -1f, nextPropsCheck;
    private int lastPropCount = -1;

    // The floor object's other renderers (a second mesh, trim): looked up once per floor (and again now and then, in case parts were added), not every frame.
    private Renderer[] floorExtras;
    private Renderer floorExtrasOf;
    private float floorExtrasTime = -10f;
    private int floorExtrasHidden = -1;   // what was last written to them (-1 = nothing yet)

    private void ApplyVisibility()
    {
        ApplyProps();
        UpdateFlatBase();
        floorRenderer.forceRenderingOff = visibility <= 0.001f || HideBaseNow;   // (a see-through style hides the picture but keeps the collider)   // (the renderer stays enabled so bounds and the fog's checks keep working)
        // Any other renderers on the floor object (a second mesh, trim) go with it.
        if (floorExtras == null || floorExtrasOf != floorRenderer || Time.unscaledTime - floorExtrasTime > 2f)
        {
            floorExtras = floorRenderer.GetComponentsInChildren<Renderer>(true);
            floorExtrasOf = floorRenderer;
            floorExtrasTime = Time.unscaledTime;
            floorExtrasHidden = -1;
        }
        int hideExtras = visibility < 0.999f ? 1 : 0;
        if (hideExtras != floorExtrasHidden)
        {
            floorExtrasHidden = hideExtras;
            foreach (Renderer extra in floorExtras) if (extra != null && extra != floorRenderer) extra.forceRenderingOff = hideExtras == 1;
        }
        if (SelfFading)
        {
            // The ground shader fades itself: no see-through copies of the material are needed.
            DropProxies(true);
            SetFloat(activeMaterial, "_Fade", visibility);
            return;
        }
        if (visibility >= 0.999f) DropProxies(true);
        else if (visibility > 0.001f)
        {
            EnsureProxies();
            if (proxyMats != null)
                foreach (Material m in proxyMats)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_BaseColor")) { Color c = m.GetColor("_BaseColor"); c.a = visibility; m.SetColor("_BaseColor", c); }
                    if (m.HasProperty("_Color")) { Color c = m.GetColor("_Color"); c.a = visibility; m.SetColor("_Color", c); }
                }
        }
    }

    /// <summary>While fading the floor is drawn with see-through copies of its materials.</summary>
    private void EnsureProxies()
    {
        if (proxyMats != null) return;
        realMats = floorRenderer.sharedMaterials;
        proxyMats = new Material[realMats.Length];
        for (int i = 0; i < realMats.Length; i++)
            proxyMats[i] = realMats[i] != null ? PixelClicker.BuildTransparentMaterial(realMats[i]) : null;
        floorRenderer.sharedMaterials = proxyMats;
    }

    /// <summary>Removes the see-through copies; 'restore' puts the real materials back first.</summary>
    private void DropProxies(bool restore)
    {
        if (proxyMats == null) return;
        if (restore && realMats != null && floorRenderer != null) floorRenderer.sharedMaterials = realMats;
        foreach (Material m in proxyMats) if (m != null) Destroy(m);
        proxyMats = null;
        realMats = null;
    }

    /// <summary>The tiling / offset that maps one copy of the texture over the floor's whole UV range.</summary>
    private bool TryWholeFloorTiling(out Vector2 tiling, out Vector2 off)
    {
        tiling = Vector2.one; off = Vector2.zero;
        if (floorRenderer == null) return false;
        Bounds b = floorRenderer.bounds;
        Vector2 a = WorldToFloorUv(new Vector3(b.min.x, b.max.y, b.min.z)), c = WorldToFloorUv(new Vector3(b.max.x, b.max.y, b.max.z));
        Vector2 d = WorldToFloorUv(new Vector3(b.min.x, b.max.y, b.max.z)), f = WorldToFloorUv(new Vector3(b.max.x, b.max.y, b.min.z));
        Vector2 lo = Vector2.Min(Vector2.Min(a, c), Vector2.Min(d, f)), hi = Vector2.Max(Vector2.Max(a, c), Vector2.Max(d, f));
        Vector2 span = hi - lo;
        if (span.x < 1e-5f || span.y < 1e-5f) return false;
        tiling = new Vector2(1f / span.x, 1f / span.y);
        off = new Vector2(-lo.x * tiling.x, -lo.y * tiling.y);
        return true;
    }

    /// <summary>Reads one triangle of the floor mesh so any point on the floor can be turned into its UV.</summary>
    private void BuildUvMap()
    {
        uvMapReady = false;
        MeshFilter mf = floorRenderer != null ? floorRenderer.GetComponent<MeshFilter>() : null;
        Mesh mesh = mf != null ? mf.sharedMesh : null;
        if (mesh == null) return;
        try
        {
            Vector3[] verts = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.triangles;
            if (verts.Length == 0 || uvs.Length != verts.Length || tris.Length < 3) return;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-10f) continue; // skip degenerate triangles
                uvP0 = a; uvE1 = b - a; uvE2 = c - a;
                uvT0 = uvs[tris[t]]; uvD1 = uvs[tris[t + 1]] - uvT0; uvD2 = uvs[tris[t + 2]] - uvT0;
                uvMapReady = true;
                return;
            }
        }
        catch (Exception) { /* mesh not readable: fall back to the bounds below */ }
    }

    /// <summary>The floor mesh's UV at a world point on the floor.</summary>
    private Vector2 WorldToFloorUv(Vector3 world)
    {
        Vector3 p = floorRenderer.transform.InverseTransformPoint(world);
        if (uvMapReady)
        {
            Vector3 d = p - uvP0;
            float a11 = Vector3.Dot(uvE1, uvE1), a12 = Vector3.Dot(uvE1, uvE2), a22 = Vector3.Dot(uvE2, uvE2);
            float b1 = Vector3.Dot(d, uvE1), b2 = Vector3.Dot(d, uvE2);
            float det = a11 * a22 - a12 * a12;
            if (Mathf.Abs(det) > 1e-12f)
            {
                float x = (b1 * a22 - b2 * a12) / det, y = (a11 * b2 - a12 * b1) / det;
                return uvT0 + uvD1 * x + uvD2 * y;
            }
        }
        // Fallback: a flat floor mapped corner to corner along its local X and Z.
        Bounds lb = floorRenderer.GetComponent<MeshFilter>() != null && floorRenderer.GetComponent<MeshFilter>().sharedMesh != null
            ? floorRenderer.GetComponent<MeshFilter>().sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 10f);
        return new Vector2((p.x - lb.min.x) / Mathf.Max(1e-4f, lb.size.x), (p.z - lb.min.z) / Mathf.Max(1e-4f, lb.size.z));
    }

    /// <summary>The game's view camera (the clicker's target camera, else the main one).</summary>
    private static Camera ViewCam() => PixelClicker.ViewCameraOf(PixelFind.Cached<PixelClicker>());

    /// <summary>
    /// Works out the floor's UVs at the bottom-left, bottom-right and top-left of the view (inside the black bars) and
    /// sets the tiling so one copy of the texture spans them. Returns true when the tiling changed.
    /// </summary>
    private bool FitToView(bool force)
    {
        Camera cam = ViewCam();
        if (cam == null || floorRenderer == null) return false;
        if (PixelOrbitCamera.HomeCamera != null) cam = PixelOrbitCamera.HomeCamera;   // orbiting: keep the fit of the normal view
        // While the camera intro moves the camera, fit to the pose it settles into instead of the live (close-up) one.
        if (intro == null) intro = PixelFind.First<PixelCameraIntro>();
        if (intro != null && intro.TryGetFinalPose(out Vector3 fp, out Quaternion fr, out float ffov, out float fortho))
        {
            if (fitCam == null)
            {
                GameObject go = new GameObject("Floor Fit Camera");
                go.hideFlags = HideFlags.HideAndDontSave;
                fitCam = go.AddComponent<Camera>();
                fitCam.enabled = false;
            }
            fitCam.CopyFrom(cam);
            fitCam.enabled = false;
            fitCam.transform.SetPositionAndRotation(fp, fr);
            fitCam.fieldOfView = ffov;
            if (fitCam.orthographic) fitCam.orthographicSize = fortho;
            fitCam.aspect = cam.aspect;
            cam = fitCam;
        }
        Transform ct = cam.transform;
        if (!force && ct.position == lastFitCamPos && ct.rotation == lastFitCamRot && Mathf.Approximately(cam.aspect, lastFitAspect) && (!cam.orthographic || Mathf.Approximately(cam.orthographicSize, lastFitOrtho))) return false;

        float bar = PixelHud.Instance != null ? PixelHud.Instance.RawBarHeight / 1080f : 0f;
        Plane floor = new Plane(floorRenderer.transform.up.sqrMagnitude > 0f ? floorRenderer.transform.up : Vector3.up,
                                new Vector3(0f, floorRenderer.bounds.max.y, 0f));
        if (!TryFloorPoint(cam, floor, new Vector2(0f, bar), out Vector3 bl) ||
            !TryFloorPoint(cam, floor, new Vector2(1f, bar), out Vector3 br) ||
            !TryFloorPoint(cam, floor, new Vector2(0f, 1f - bar), out Vector3 tl))
            return false; // the view doesn't reach the floor everywhere yet (e.g. camera intro): keep the last fit

        lastFitCamPos = ct.position;
        lastFitCamRot = ct.rotation;
        lastFitAspect = cam.aspect;
        lastFitOrtho = cam.orthographicSize;

        Vector2 uvBL = WorldToFloorUv(bl), uvBR = WorldToFloorUv(br), uvTL = WorldToFloorUv(tl);
        // Texture coordinate = uv * tiling + offset: 0 at the bottom-left corner of the view, 1 at the right / top edge.
        Vector2 across = uvBR - uvBL, up = uvTL - uvBL;
        bool swapped = Mathf.Abs(across.y) > Mathf.Abs(across.x); // camera turned 90 degrees to the floor's UVs
        float du = swapped ? up.x : across.x, dv = swapped ? across.y : up.y;
        if (Mathf.Abs(du) < 1e-5f || Mathf.Abs(dv) < 1e-5f) return false;
        baseTiling = new Vector2(1f / du, 1f / dv);
        baseOffset = new Vector2(-uvBL.x * baseTiling.x, -uvBL.y * baseTiling.y);
        if (AreaScale > 1.001f)
        {
            // A bigger floor: the picture is stretched by the same factor round the middle of the view.
            Vector2 centreUv = uvBL + 0.5f * (uvBR - uvBL) + 0.5f * (uvTL - uvBL);
            baseTiling /= AreaScale;
            baseOffset = new Vector2(0.5f - centreUv.x * baseTiling.x, 0.5f - centreUv.y * baseTiling.y);
        }
        float viewHeight = (tl - bl).magnitude;
        if (viewHeight > 1e-4f) viewAspect = (br - bl).magnitude / viewHeight;
        return true;
    }

    private static bool TryFloorPoint(Camera cam, Plane floor, Vector2 viewport, out Vector3 point)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        if (floor.Raycast(ray, out float enter) && enter > 0f && enter < cam.farClipPlane * 4f)
        {
            point = ray.GetPoint(enter);
            return true;
        }
        point = Vector3.zero;
        return false;
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
            float keepAspect = shadeAspect;
            shadeAspect = 1f; // square menu icon
            FloorStyle small = styles[index].Clone();
            small.resolution = Mathf.Min(small.resolution, 160);   // a menu icon never needs the big picture
            small.detail = Mathf.Max(1, small.detail / 3);
            Vector2 keepFocus = craterFocus;
            craterFocus = new Vector2(0.5f, 0.56f);
            tex = BuildTexture(small, 0);
            craterFocus = keepFocus;
            shadeAspect = keepAspect;
            previews[index] = tex;
        }
        return tex;
    }

    /// <summary>
    /// The picture the title screen shows when its background is not the live scene: the current style's texture (animated ones keep moving), how many times
    /// to repeat it across the screen (1 = the whole picture, for fitted / stretched styles that must not repeat) and a tint for a plain Classic floor.
    /// False when there is no floor to show.
    /// </summary>
    /// <summary>The scroll position of a sliding style (Matrix Rain...), so a picture drawn from the texture alone keeps moving too.</summary>
    public Vector2 ScrollOffset => offset;

    public bool TryGetTitlePicture(out Texture texture, out Vector2 tiles, out Color tint)
    {
        texture = null; tiles = Vector2.one; tint = Color.white;
        if (!Usable || current < 0 || current >= styles.Count) return false;
        bool classic = styles[current].pattern == FloorPattern.Classic;
        texture = !classic && activeTexture != null ? activeTexture : PreviewTexture(current);
        if (classic) tint = PreviewColor(current);
        if (texture != null && styles[current].hideBase)
            tiles = new Vector2(1f, Screen.height / Mathf.Max(1f, Screen.width));   // a see-through style's still picture does not tile: one copy across, square cells
        else if (texture != null && texture.wrapMode == TextureWrapMode.Repeat)
        {
            float across = styles[current].detail > 1 ? 1.5f : 6f;   // a big non-repeating texture is shown (nearly) once
            tiles = new Vector2(across, across * Screen.height / Mathf.Max(1f, Screen.width));   // square cells
        }
        return true;
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
}
