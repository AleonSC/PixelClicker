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
        PersianRug,      // a woven rug with a medallion and borders on a wooden floor
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
    public enum FloorOverlay { None = 0, Lasers = 1, LaserTracker = 2, Ant = 3, Crumble = 4 }

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
    [Tooltip("How far (world units) the game-made floor lies under the cube's resting spot - the falling distance of the old pixels. 0 = the default (10).")]
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

    /// <summary>
    /// The floor cell (one square of the pattern's grid: the style's tile size divided by its 'cells') that contains a world point:
    /// its centre on the floor and its side length. False when the floor has no steady grid (Classic, fitted or scrolling styles).
    /// Used to line the Robot Worker up with the tiles.
    /// </summary>
    public bool TryGetCell(Vector3 world, out Vector3 centre, out float size)
    {
        centre = world; size = 0f;
        if (floorRenderer == null || current < 0 || current >= styles.Count) return false;
        FloorStyle st = styles[current];
        if (st.pattern == FloorPattern.Classic || st.fitToView || st.stretchToFloor || st.scrollSpeed != Vector2.zero || st.cells < 1) return false;
        float aspect = StyleAspect(st);
        float cellX = st.tileWorldSize * aspect / st.cells, cellZ = st.tileWorldSize / st.cells;
        if (cellX < 0.05f || cellZ < 0.05f) return false;
        Vector3 c = floorRenderer.bounds.center;
        float sh = (st.cells % 2 == 0) ? 0f : 0.5f;   // an even grid has a line through the floor's middle, an odd one a cell centre
        centre = new Vector3(c.x + (Mathf.Floor((world.x - c.x) / cellX - sh) + 0.5f + sh) * cellX, world.y,
                             c.z + (Mathf.Floor((world.z - c.z) / cellZ - sh) + 0.5f + sh) * cellZ);
        size = Mathf.Min(cellX, cellZ);
        return true;
    }
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
        PixelClicker clicker = PixelFind.First<PixelClicker>();
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
#if UNITY_6000_0_OR_NEWER
        PhysicsMaterial surface = null;
#else
        PhysicMaterial surface = null;
#endif
        if (scene != null)
        {
            Collider sc = scene.GetComponent<Collider>();
            if (sc != null) surface = sc.sharedMaterial;
            if (sc != null) sc.enabled = false;
            scene.enabled = false;
        }
        if (surface == null)
        {
#if UNITY_6000_0_OR_NEWER
            surface = new PhysicsMaterial("Pixel Ground") { dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0.1f };
#else
            surface = new PhysicMaterial("Pixel Ground") { dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0.1f };
#endif
        }
        box.sharedMaterial = surface;
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
        PixelClicker clicker = PixelFind.First<PixelClicker>();
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
            PixelClicker clicker = PixelFind.First<PixelClicker>();
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

    private bool floorWarned;

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
        bool unseen = visibilityInit && visibility <= 0.001f && !(PixelTitleScreen.Showing && !PixelTitleWanderers.LiveBackground);
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
    public static float AreaScale => DoubleArea ? 2f : 1f;

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
        if (exitHold && PixelTitleScreen.Showing) exitHold = false;
        bool hide = HidesOnTitle && (exitHold || (PixelTitleWanderers.LiveBackground && (PixelTitleScreen.Showing || PixelCameraIntro.Moving)));
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
        if (PixelSkybox.Instance != null && PixelSkybox.Instance.TryGetHorizonColor(out Color sky)) flat = sky;
        else if (Camera.main != null && Camera.main.clearFlags == CameraClearFlags.SolidColor) flat = Camera.main.backgroundColor;
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
        propList.Clear();
        PixelClicker clicker = PixelFind.First<PixelClicker>();
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

    private void ApplyVisibility()
    {
        ApplyProps();
        UpdateFlatBase();
        floorRenderer.forceRenderingOff = visibility <= 0.001f || HideBaseNow;   // (a see-through style hides the picture but keeps the collider)   // (the renderer stays enabled so bounds and the fog's checks keep working)
        // Any other renderers on the floor object (a second mesh, trim) go with it.
        foreach (Renderer extra in floorRenderer.GetComponentsInChildren<Renderer>(true)) if (extra != floorRenderer) extra.forceRenderingOff = visibility < 0.999f;
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

    /// <summary>
    /// Works out the floor's UVs at the bottom-left, bottom-right and top-left of the view (inside the black bars) and
    /// sets the tiling so one copy of the texture spans them. Returns true when the tiling changed.
    /// </summary>
    private bool FitToView(bool force)
    {
        Camera cam = Camera.main;
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
        if (texture != null && texture.wrapMode == TextureWrapMode.Repeat)
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
    // Drawing the textures
    // ------------------------------------------------------------------

    private static Texture2D BuildTexture(FloorStyle s, int step, bool deferFill = false)
    {
        int n = Mathf.Clamp(s.resolution, 8, 1024);
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, deferFill && !IsOverlayPattern(s.pattern) && !s.pixelated)   // (big still pictures get mipmaps so far-away ground doesn't shimmer)
        {
            name = "Floor " + s.name,
            wrapMode = s.stretchToFloor || s.fitToView ? TextureWrapMode.Clamp : TextureWrapMode.Repeat,   // a fitted picture is ONE copy: never repeated over the floor
            filterMode = s.pixelated ? FilterMode.Point : FilterMode.Bilinear,
            anisoLevel = 4
        };
        if (s.pattern == FloorPattern.Disco) { DrawDisco(tex, s, step); return tex; }
        if (deferFill) return tex;   // the caller draws it over several frames (UsesSlicedFill)
        Fill(tex, s, 0f, false);
        return tex;
    }

    // Animation time read by the animated patterns while a texture is being drawn.
    private static float shadeTime;
    // Width / height of the area one copy of the texture covers (set from the fitted view) so pixel-art patterns can keep square pixels.
    private static float shadeAspect = 1f;
    private float viewAspect = 16f / 9f;
    private PixelCameraIntro intro;
    private Camera fitCam;
    private static Color[] fillBuffer; // reused by animated floors so redrawing doesn't allocate every frame

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { fillBuffer = null; pacPath = null; pacPathIndex = null; shadeTime = 0f; shadeAspect = 1f; shadeDetail = 1; shadeBase = false; craters = null; radarDots = null; ClearLayers(); }

    /// <summary>Draws the pattern into 'tex' at animation time 'time' (0 for still patterns). 'reuse' = keep the pixel buffer for the next frame.</summary>
    private static void Fill(Texture2D tex, FloorStyle s, float time, bool reuse)
    {
        int n = tex.width;
        if (IsOverlayPattern(s.pattern) && layerCur32 != null && layerN == n) { OverlayFrame(tex, s, time); return; }   // only the moving parts are repainted
        shadeBase = false;
        Color[] px;
        if (reuse)
        {
            if (fillBuffer == null || fillBuffer.Length != n * n) fillBuffer = new Color[n * n];
            px = fillBuffer;
        }
        else px = new Color[n * n];
        int cells = Mathf.Max(1, s.cells);
        BeginShade(s, time);
        if (s.pattern == FloorPattern.CircuitFlow) PrepareSparks(s, time);   // and the wandering sparks

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

    /// <summary>
    /// A security-laser corridor: pale teal metal panels with glowing light strips along the seams, and thin red beams that sway across the floor in both directions,
    /// each blinking on and off, with white-hot spots where two cross. 'cells' = beams per direction (the texture tiles).
    /// </summary>
    private static Color LaserHall(FloorStyle s, float u, float v, int n, int cells)
    {
        float t = shadeTime;
        // Panels: brushed metal with dark seams and a lit strip of lights beside each seam.
        int panels = Mathf.Max(2, cells / 2);
        float pu = Frac(u * panels), pv = Frac(v * panels);
        float seam = Mathf.Min(Mathf.Min(pu, 1f - pu), Mathf.Min(pv, 1f - pv)) * n / panels;     // texels from the nearest seam
        Color c = s.colorA * (0.82f + 0.25f * Fbm(u, v, 6, 3, s.seed) + 0.05f * Mathf.Sin(v * n * 0.8f));
        if (seam < 1.2f) c = s.colorA * 0.3f;                                                    // the seam itself
        else if (seam < 3.2f && pu > 0.5f) c = Color.Lerp(c, s.colorB, 0.35f);                    // a glass edge catching the light
        // Wall-style light dashes along the vertical seams.
        float dash = Mathf.Repeat(v * panels * 6f, 1f);
        if (pu > 0.93f && dash < 0.45f) c = Color.Lerp(c, s.colorB, 0.9f);

        // Lasers.
        float[] acc = new float[2];
        for (int axis = 0; axis < 2; axis++)
        {
            float coord = axis == 0 ? v : u;
            for (int i = 0; i < cells; i++)
            {
                float centre = (i + 0.5f + 0.32f * Mathf.Sin(t * (0.8f + 0.11f * i) + i * 1.9f + axis * 2.4f)) / cells;
                int slot = Mathf.FloorToInt(t * 1.4f + i * 0.37f + axis * 3f);
                if (Hash(i * 7 + axis, slot, s.seed) <= 0.28f) continue;                         // this beam is blinking off
                float d = Mathf.Abs(Mathf.Repeat(coord - centre + 0.5f, 1f) - 0.5f) * n;           // texels from the beam
                float beam = Mathf.Clamp01(Mathf.Clamp01(1.3f - d) + Mathf.Exp(-d * 0.55f) * 0.55f);
                if (beam > acc[axis]) acc[axis] = beam;
            }
        }
        float best = Mathf.Max(acc[0], acc[1]);
        float hot = Mathf.Min(acc[0], acc[1]);                                                    // white-hot where two beams cross
        Color laser = s.colorC;
        c = Color.Lerp(c, laser, Mathf.Clamp01(best));
        c = Color.Lerp(c, Color.white, Mathf.Clamp01(hot) * 0.85f);
        c.a = 1f;
        return c;
    }

    private static Color HueShift(Color c, float shift)
    {
        Color.RGBToHSV(c, out float h, out float sat, out float val);
        Color r = Color.HSVToRGB(Mathf.Repeat(h + shift, 1f), sat, val);
        r.a = 1f;
        return r;
    }

    /// <summary>Strength (0..1) of one grid line in the 'merge' variant: every second line fades out and back, then every fourth, so the squares grow and shrink.</summary>
    private static float MergeStrength(int k, float phase)
    {
        float a = Sstep(0.12f, 0.45f, phase), b = Sstep(0.55f, 0.88f, phase);
        int m = ((k % 4) + 4) % 4;
        if (m == 0) return 1f;
        if (m == 2) return 1f - b;
        return 1f - a;
    }

    private static float NeonLine(float texels) { return Mathf.Clamp01(Mathf.Clamp01(1.5f - texels) + Mathf.Exp(-texels * 0.45f) * 0.55f); }

    /// <summary>The four animated neon grids (they move and change colour over time; 'cells' should be divisible by 4 so the texture still tiles).</summary>
    private static Color NeonAnimated(FloorStyle s, float u, float v, int n, int cells)
    {
        float t = shadeTime;
        float hue = 0f;
        float phase = 0.5f + 0.5f * Mathf.Sin(t * 0.45f);
        float gu = u, gv = v;
        switch (s.pattern)
        {
            case FloorPattern.NeonDrift:
                gu = u + t * 0.045f; gv = v + t * 0.028f;                       // the whole grid slides
                hue = t * 0.05f;
                break;
            case FloorPattern.NeonMerge:
                gu = u + t * 0.01f; gv = v - t * 0.008f;
                hue = t * 0.035f;
                break;
            case FloorPattern.NeonWave:
                gu = u + 0.022f * Mathf.Sin((v * 2f + t * 0.35f) * Mathf.PI * 2f);   // lines ripple like cloth
                gv = v + 0.022f * Mathf.Sin((u * 2f - t * 0.3f) * Mathf.PI * 2f);
                hue = t * 0.045f;
                break;
            default: // NeonPrism
                gu = u + t * 0.02f; gv = v;
                hue = Mathf.Repeat((u + v) * 1f - t * 0.12f, 1f);                    // a rainbow sweeping diagonally
                break;
        }
        Color lineColor = HueShift(s.colorB, hue), crossColor = HueShift(s.colorC, hue + 0.12f);
        Color ground = s.colorA * (0.85f + 0.15f * Fbm(u, v, 4, 3, s.seed));

        float fu = Frac(gu * cells), fv = Frac(gv * cells);
        int ku = Mathf.FloorToInt(gu * cells), kv = Mathf.FloorToInt(gv * cells);
        float bestU = 0f, bestV = 0f, nearU = 99f, nearV = 99f, strU = 1f, strV = 1f;
        for (int side = 0; side < 2; side++)
        {
            float du = side == 0 ? fu : 1f - fu, dv = side == 0 ? fv : 1f - fv;
            float stU = s.pattern == FloorPattern.NeonMerge ? MergeStrength(ku + side, phase) : 1f;
            float stV = s.pattern == FloorPattern.NeonMerge ? MergeStrength(kv + side, phase) : 1f;
            float iu = NeonLine(du * n / cells) * stU, iv = NeonLine(dv * n / cells) * stV;
            if (iu > bestU) { bestU = iu; nearU = du * n / cells; strU = stU; }
            if (iv > bestV) { bestV = iv; nearV = dv * n / cells; strV = stV; }
        }
        float line = Mathf.Max(bestU, bestV);
        Color c = Color.Lerp(ground, lineColor, line);
        float cross = Mathf.Clamp01(2.5f - Mathf.Max(nearU, nearV)) * Mathf.Min(strU, strV);
        return Color.Lerp(c, crossColor, cross * 0.8f);
    }

    /// <summary>The Aura floors' pixel. PURE (reads only its arguments and the style, no static state), so the rows can be drawn on several threads at once (PixelFloorLayers.FillSliced).</summary>
    private static Color ShadeKiAura(FloorStyle s, float u, float v, int x, int y, int D)
    {
        // Tall streaks (stretched noise) that scroll upward like flames; the lattices are scaled up so the streaks keep their width on a big texture.
        float warp = Fbm(u, v, 3 * D, 3, s.seed) * 0.15f;
        float flame = Fbm(u + warp, v, 10 * D, 2 * D, 3, s.seed + 1);
        float flicker = Fbm(u, v, 4 * D, 4 * D, 3, s.seed + 2);
        float k = Mathf.Clamp01((flame * 0.75f + flicker * 0.35f - 0.35f) * 2f);
        k = k * k;
        Color c = Color.Lerp(s.colorA, s.colorB, Mathf.Clamp01(k * 1.6f));
        c = Color.Lerp(c, s.colorC, Mathf.Clamp01(k * 2.2f - 1.1f));
        if (Hash(x, y, s.seed + 9) > 0.998f) c = s.colorC;            // sparks
        return c;
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

            case FloorPattern.LaserHall:
                return LaserHall(s, u, v, n, cells);

            case FloorPattern.NeonDrift:
            case FloorPattern.NeonMerge:
            case FloorPattern.NeonWave:
            case FloorPattern.NeonPrism:
                return NeonAnimated(s, u, v, n, cells);

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
                int plankInRow = ((Mathf.FloorToInt(u * 2f + shift) % 2) + 2) % 2;   // 2 planks per row: the plank at the right edge continues at the left, so the texture tiles
                int plank = plankInRow + row * 2;
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
                int D = shadeDetail;
                float turb = Fbm(u, v, 3 * D, 5, s.seed);                                   // slow swirls over the whole floor
                float veins = Mathf.Abs(Mathf.Sin((u * cells + v * cells + turb * 5f) * Mathf.PI));
                float fine = Mathf.Abs(Mathf.Sin((v * cells * 2f - u * cells + Fbm(u, v, 6 * D, 4, s.seed + 3) * 6f) * Mathf.PI));
                Color c = s.colorA;
                c = Color.Lerp(s.colorB, c, Mathf.Pow(veins, 0.35f));
                c = Color.Lerp(s.colorC, c, Mathf.Clamp01(Mathf.Pow(fine, 0.18f) + 0.15f));
                c.a = 1f;
                return c;
            }
            case FloorPattern.Lava:
            {
                int D = shadeDetail;
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float heat = Mathf.Clamp01(1f - edge / 0.35f);
                heat = heat * heat;
                float n2 = Fbm(u, v, 4 * D, 4, s.seed);
                Color crust = s.colorA * (0.6f + 0.6f * n2);
                Color glow = Color.Lerp(s.colorB, s.colorC, Mathf.Clamp01(heat * 1.4f - 0.3f));
                Color c = Color.Lerp(crust, glow, Mathf.Clamp01(heat + (n2 - 0.6f) * 0.5f));
                c.a = 1f;
                return c;
            }
            case FloorPattern.Space:
            {
                int D = shadeDetail;
                float neb = Fbm(u, v, 3 * D, 5, s.seed);
                float neb2 = Fbm(u + 0.37f, v + 0.11f, 4 * D, 5, s.seed + 9);
                Color c = s.colorA;
                c = Color.Lerp(c, s.colorB, Mathf.Clamp01((neb - 0.45f) * 1.6f) * 0.7f);
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01((neb2 - 0.55f) * 1.8f) * 0.55f);
                c.a = 1f;
                return c;
            }
            case FloorPattern.Ice:
            {
                int D = shadeDetail;
                Voronoi(u, v, cells, s.seed, out float f1, out float f2, out int id);
                float edge = (f2 - f1) * cells;
                float crack = Mathf.Clamp01(1f - edge / 0.06f);
                Voronoi(u, v, cells * 3, s.seed + 1, out float g1, out float g2, out _);
                float small = Mathf.Clamp01(1f - (g2 - g1) * cells * 3 / 0.05f) * 0.45f;
                float frost = Fbm(u, v, 4 * D, 4, s.seed);
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
                // Each column has its own trail: brightest at the head, fading upward (a few trails per texture height, so the columns never line up).
                float head = Hash(col, 0, s.seed);
                float trail = Frac(head - v * shadeDetail + Hash(col, 1, s.seed) * 0.3f);
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
                return ShadeRadar(s, u, v, x, y, n, cells);
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
                return ShadeKiAura(s, u, v, x, y, shadeDetail);
            case FloorPattern.Craters:
                return ShadeCraterField(s, u, v, x, y, n);
            case FloorPattern.MoonSurface:
                return ShadeMoon(s, u, v, x, y, n);
            case FloorPattern.MarsSurface:
                return ShadeMars(s, u, v, x, y, n);
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
                // Warped waves with bright ridges where two wave sets meet (drawn once over a big area; the whole picture drifts).
                int D = shadeDetail;
                float w1 = Fbm(u, v, 3 * D, 3, s.seed);
                float w2 = Fbm(u + w1 * 0.4f, v + w1 * 0.4f, 3 * D, 3, s.seed + 1);
                float a = Mathf.Sin((u * cells + w2 * 3f) * Mathf.PI * 2f);
                float b = Mathf.Sin((v * cells - w1 * 3f) * Mathf.PI * 2f);
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
                // Domain-warped noise: swirling wisps (lattices scaled up for the big texture).
                int D = shadeDetail;
                float q = Fbm(u, v, 3 * D, 4, s.seed);
                float r = Fbm(u + q * 0.6f, v + q * 0.6f, 3 * D, 4, s.seed + 1);
                float w = Fbm(u + r * 0.8f, v - r * 0.8f, 4 * D, 4, s.seed + 2);
                float k = Mathf.Clamp01((w - 0.45f) * 2.5f);
                k = k * k * (3f - 2f * k);
                Color c = Color.Lerp(s.colorA, s.colorB, k);
                c = Color.Lerp(c, s.colorC, Mathf.Clamp01(k * k * 1.6f - 0.5f));
                if (Hash(x, y, s.seed + 4) > 0.9985f) c = s.colorC;           // sparks
                return c;
            }
        }
        return ShadeExtra(s, u, v, x, y, n, cells);
    }

    // ------------------------------------------------------------------
    // Pac-Man floor: a maze, pellets that get eaten as Pac-Man passes, and four ghosts chasing him round a loop
    // ------------------------------------------------------------------

    private static readonly string[] PacMaze =
    {
        "######### #########",
        "#o...............o#",
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
        "######### #########",
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
        int w = PacMaze[0].Length, h = PacMaze.Length;
        return PacMaze[Wrap(r, h)][Wrap(c, w)] == '#'; // wraps: the maze repeats across the floor
    }

    private static Color ShadePacMan(FloorStyle s, float u, float v)
    {
        BuildPacPath();
        int w = PacMaze[0].Length, h = PacMaze.Length;
        float t = shadeTime;
        // The maze fills the texture (the style's tileAspect makes its cells square on the floor); rows run from the top.
        // Its edges line up with the next copy, so the tunnels on all four sides lead into the neighbouring mazes.
        float gx = u * w, gy = (1f - v) * h;
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
                (!PacWall(ci - 1, ri) && lx >= lo && lx <= hi) ||
                (!PacWall(ci + 1, ri) && lx <= 1f - lo && lx >= 1f - hi) ||
                (!PacWall(ci, ri - 1) && ly >= lo && ly <= hi) ||
                (!PacWall(ci, ri + 1) && ly <= 1f - lo && ly >= 1f - hi);
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
        // Pac-Man mazes now fill their tile and join up with their neighbours.
        if (defaultsVersion < 2)
        {
            foreach (FloorStyle st in styles)
                if (st != null && st.name == "Pac-Man") st.tileAspect = 19f / 21f;
            defaultsVersion = 2;
        }
        // These four now fit one copy to the camera's view of the floor.
        if (defaultsVersion < 3)
        {
            foreach (FloorStyle st in styles)
                if (st != null && (st.name == "Yu-Gi-Oh Card Back" || st.name == "Moving Clouds" || st.name == "Kaleidoscope" || st.name == "Pac-Man"))
                    st.fitToView = true;
            defaultsVersion = 3;
        }

        // Renamed, removed and regrouped styles: bring saved lists up to date once (before the missing defaults are added, so nothing is duplicated).
        if (defaultsVersion < 4)
        {
            string[,] renames =
            {
                { "Dragon Radar", "Radar" }, { "Super Saiyan Aura", "Super Aura" }, { "Ultra Instinct Aura", "Divine Aura" },
                { "Super Saiyan Blue Aura", "Blue Aura" }, { "Super Saiyan God Aura", "God Aura" }, { "Destruction Energy", "Death Energy" },
            };
            for (int r = 0; r < renames.GetLength(0); r++)
            {
                FloorStyle st = styles.Find(x => x != null && x.name == renames[r, 0]);
                if (st != null && !styles.Exists(x => x != null && x.name == renames[r, 1])) st.name = renames[r, 1];
            }
            styles.RemoveAll(x => x != null && (x.name == "Dragon Balls" || x.name == "Yu-Gi-Oh Card Back" || x.pattern == FloorPattern.DragonBalls || x.pattern == FloorPattern.CardBack));
            FloorStyle superAura = styles.Find(x => x != null && x.name == "Super Aura"), divine = styles.Find(x => x != null && x.name == "Divine Aura");
            if (superAura != null && divine != null)
            {
                styles.Remove(superAura);
                styles.Insert(styles.IndexOf(divine), superAura);   // the auras sit together
            }
            // The new neon grid variants go right after Neon Grid.
            int neonAt = styles.FindIndex(x => x != null && x.name == "Neon Grid");
            if (neonAt >= 0)
                foreach (FloorStyle def in DefaultStyles())
                    if (def.name.StartsWith("Neon ") && !styles.Exists(x => x != null && x.name == def.name)) styles.Insert(++neonAt, def);
            defaultsVersion = 4;
        }

        // Renames, removals and the reworked battle crater.
        if (defaultsVersion < 5)
        {
            string[,] renames5 = { { "Planet Namek", "Alien Planet" }, { "Time Chamber", "Chamber Tiles" }, { "Tournament of Power", "Power Tournament" } };
            for (int r = 0; r < renames5.GetLength(0); r++)
            {
                FloorStyle st = styles.Find(x => x != null && x.name == renames5[r, 0]);
                if (st != null && !styles.Exists(x => x != null && x.name == renames5[r, 1])) st.name = renames5[r, 1];
            }
            styles.RemoveAll(x => x != null && (x.name == "Gravity Room" || x.pattern == FloorPattern.GravityRoom));
            FloorStyle crater = styles.Find(x => x != null && x.name == "Battle Crater");
            if (crater != null)
            {
                foreach (FloorStyle def in DefaultStyles())
                    if (def.name == "Battle Crater") { int at = styles.IndexOf(crater); styles[at] = def; break; }
            }
            defaultsVersion = 5;
        }

        if (defaultsVersion < 6)
        {
            styles.RemoveAll(x => x != null && (x.name == "Noir Smoke" || x.name == "Bloody Boards" || x.pattern == FloorPattern.NoirSmoker || x.pattern == FloorPattern.BloodBoards));
            foreach (FloorStyle st in styles)
                if (st != null && st.name == "Circuit Flow") { st.animateFps = 20f; st.cells = Mathf.Max(8, st.cells); }
            defaultsVersion = 6;
        }
        // Circuit Flow with its sparks was heavy: a smaller texture redrawn a little less often.
        if (defaultsVersion < 7)
        {
            foreach (FloorStyle st in styles)
                if (st != null && st.name == "Circuit Flow") { st.resolution = 192; st.animateFps = 15f; }
            defaultsVersion = 7;
        }

        // A scene saved with older tile sizes kept them forever: copy the layout numbers of every built-in style from the current defaults.
        if (defaultsVersion < 8)
        {
            foreach (FloorStyle def in DefaultStyles())
            {
                FloorStyle old = styles.Find(s => s != null && s.name == def.name);
                if (old == null) continue;
                old.tileWorldSize = def.tileWorldSize; old.tileAspect = def.tileAspect; old.cells = def.cells;
                old.fitToView = def.fitToView; old.stretchToFloor = def.stretchToFloor;
            }
            defaultsVersion = 8;
        }

        // The 8-bit floors are gone, and these styles were redrawn as big non-repeating pictures: take the new definitions over the saved ones.
        if (defaultsVersion < 9)
        {
            styles.RemoveAll(x => x != null && (x.name.StartsWith("8-Bit ") || x.pattern == FloorPattern.RoadRacer || x.pattern == FloorPattern.Fighter ||
                (x.pattern >= FloorPattern.Platformer && x.pattern <= FloorPattern.Crossing)));
            string[] reworked =
            {
                "Marble", "Lava", "Deep Space", "Ice", "Matrix Rain", "Radar", "Battle Crater", "Super Aura", "Blue Aura", "Divine Aura", "God Aura",
                "Death Energy", "Laser Corridor", "Liquid Flow", "Spiral Galaxy", "Black Hole", "Moon Surface", "Red Planet", "Circuit Flow", "Cyber Hex",
                "Haunted Tiles", "Graveyard", "Watching Eyes",
            };
            foreach (FloorStyle def in DefaultStyles())
            {
                if (System.Array.IndexOf(reworked, def.name) < 0) continue;
                int at = styles.FindIndex(x => x != null && x.name == def.name);
                if (at >= 0) styles[at] = def;
            }
            defaultsVersion = 9;
        }

        // Spiral Galaxy and Black Hole are gone; Radar lost its circles; Liquid Flow, Circuit Flow, Cyber Hex and Watching Eyes are bigger.
        if (defaultsVersion < 10)
        {
            styles.RemoveAll(x => x != null && (x.name == "Spiral Galaxy" || x.name == "Black Hole" || x.pattern == FloorPattern.Galaxy || x.pattern == FloorPattern.BlackHole));
            foreach (FloorStyle def in DefaultStyles())
            {
                if (def.name != "Radar" && def.name != "Liquid Flow" && def.name != "Circuit Flow" && def.name != "Cyber Hex" && def.name != "Watching Eyes") continue;
                int at = styles.FindIndex(x => x != null && x.name == def.name);
                if (at >= 0) styles[at] = def;
            }
            defaultsVersion = 10;
        }

        // Lava, Marble, Ice and Deep Space are drawn by the world-space ground shader in the game (their menu pictures stay painted).
        if (defaultsVersion < 11)
        {
            foreach (FloorStyle def in DefaultStyles())
            {
                if (def.shaderGround <= 0) continue;
                FloorStyle old = styles.Find(x => x != null && x.name == def.name);
                if (old != null) { old.shaderGround = def.shaderGround; old.shaderScale = def.shaderScale; }
            }
            defaultsVersion = 11;
        }

        // Data Streams: drawn by the ground shader now (pattern 5), so it fills the whole floor circle.
        if (defaultsVersion < 12)
        {
            FloorStyle streams = styles.Find(x => x != null && x.name == "Data Streams");
            if (streams != null) { streams.shaderGround = 5; streams.shaderScale = 1.1f; }
            defaultsVersion = 12;
        }

        // Moon Surface and Red Planet became their own patterns (not one crater field in two colours); Stained Glass, Casino Felt and Nebula Field correct the camera's tilt.
        if (defaultsVersion < 13)
        {
            foreach (FloorStyle st in styles)
            {
                if (st == null) continue;
                if (st.name == "Moon Surface") { st.pattern = FloorPattern.MoonSurface; }
                if (st.name == "Red Planet") { st.pattern = FloorPattern.MarsSurface; st.colorA = new Color(0.72f, 0.36f, 0.2f); st.colorB = new Color(0.52f, 0.25f, 0.14f); st.colorC = new Color(0.12f, 0.07f, 0.06f); }
                if (st.name == "Stained Glass" || st.name == "Casino Felt" || st.name == "Nebula Field") st.tiltCorrect = true;
            }
            defaultsVersion = 13;
        }

        // The see-through laser floors got a real still picture (their menu icon / title picture was one flat pink colour at 16 px).
        if (defaultsVersion < 14)
        {
            foreach (FloorStyle st in styles)
                if (st != null && (st.name == "Neon Lasers" || st.name == "Laser Tracker")) st.resolution = Mathf.Max(st.resolution, 160);
            defaultsVersion = 14;
        }

        // The auras, Death Energy, Matrix Rain, Liquid Flow and Laser Corridor are drawn by the world-space ground shader in the game (patterns 6-10).
        if (defaultsVersion < 15)
        {
            foreach (FloorStyle def in DefaultStyles())
            {
                if (def.shaderGround < 6) continue;
                FloorStyle old = styles.Find(x => x != null && x.name == def.name);
                if (old != null) { old.shaderGround = def.shaderGround; old.shaderScale = def.shaderScale; }
            }
            defaultsVersion = 15;
        }

        foreach (FloorStyle def in DefaultStyles())
            if (!styles.Exists(s => s.name == def.name)) styles.Add(def);
    }

    private static FloorStyle S(string name, FloorPattern p, Color a, Color b, Color c, int res, bool pixelated,
                                float tile, int cells, float smooth = 0.2f, float glow = 0f)
        => new FloorStyle { name = name, pattern = p, colorA = a, colorB = b, colorC = c, resolution = res,
                            pixelated = pixelated, tileWorldSize = tile, cells = cells, smoothness = smooth, glow = glow,
                            seed = name.Length * 7 + 3 };

    private static FloorStyle NeonVariant(string name, FloorPattern p, Color ground, Color line, Color cross)
    {
        FloorStyle st = S(name, p, ground, line, cross, 128, false, 6f, 8, 0.7f, 1.6f);
        st.animateFps = 20f; st.pulseSpeed = 0.35f; st.pulseAmount = 0.3f;
        return st;
    }

    private static IEnumerable<FloorStyle> DefaultStyles()
    {
        yield return S("Classic", FloorPattern.Classic, Color.white, Color.gray, Color.black, 8, false, 1f, 1);

        FloorStyle neon = S("Neon Grid", FloorPattern.NeonGrid, new Color(0.03f, 0.02f, 0.08f), new Color(0.1f, 0.85f, 1f),
                            new Color(1f, 0.3f, 0.9f), 256, false, 6f, 4, 0.7f, 1.6f);
        neon.pulseSpeed = 0.35f; neon.pulseAmount = 0.35f;
        yield return neon;

        yield return NeonVariant("Neon Drift", FloorPattern.NeonDrift, new Color(0.03f, 0.02f, 0.08f), new Color(0.1f, 0.85f, 1f), new Color(1f, 0.3f, 0.9f));
        yield return NeonVariant("Neon Merge", FloorPattern.NeonMerge, new Color(0.02f, 0.03f, 0.08f), new Color(0.2f, 1f, 0.6f), new Color(1f, 0.9f, 0.3f));
        yield return NeonVariant("Neon Wave", FloorPattern.NeonWave, new Color(0.04f, 0.01f, 0.08f), new Color(1f, 0.3f, 0.8f), new Color(0.3f, 0.9f, 1f));
        yield return NeonVariant("Neon Prism", FloorPattern.NeonPrism, new Color(0.02f, 0.02f, 0.05f), new Color(1f, 0.2f, 0.3f), new Color(1f, 1f, 1f));

        yield return S("Retro Checker", FloorPattern.Checker, new Color(0.92f, 0.92f, 0.9f), new Color(0.12f, 0.12f, 0.14f),
                       Color.black, 64, true, 4f, 4, 0.45f);

        yield return S("Pixel Grass", FloorPattern.PixelGrass, new Color(0.22f, 0.55f, 0.18f), new Color(0.36f, 0.72f, 0.25f),
                       new Color(0.95f, 0.35f, 0.55f), 48, true, 4f, 1, 0.05f);

        yield return S("Wood Planks", FloorPattern.WoodPlanks, new Color(0.55f, 0.35f, 0.18f), new Color(0.42f, 0.25f, 0.12f),
                       new Color(0.16f, 0.09f, 0.05f), 64, true, 5f, 6, 0.25f);

        yield return S("Cobblestone", FloorPattern.Cobblestone, new Color(0.55f, 0.55f, 0.58f), new Color(0.42f, 0.4f, 0.38f),
                       new Color(0.12f, 0.12f, 0.13f), 128, true, 5f, 6, 0.15f);

        // The big floors below are ONE huge picture (about 40 world units wide, drawn a few milliseconds per frame) instead of a small tile repeated many times.
        FloorStyle marble = S("Marble", FloorPattern.Marble, new Color(0.95f, 0.94f, 0.92f), new Color(0.55f, 0.55f, 0.6f),
                              new Color(0.78f, 0.66f, 0.42f), 1024, false, 40f, 14, 0.9f);
        marble.stretchToFloor = true; marble.detail = 5; marble.shaderGround = 2; marble.shaderScale = 7f;
        yield return marble;

        FloorStyle lava = S("Lava", FloorPattern.Lava, new Color(0.12f, 0.05f, 0.04f), new Color(1f, 0.25f, 0.02f),
                            new Color(1f, 0.85f, 0.3f), 768, true, 40f, 36, 0.35f, 1.4f);
        lava.pulseSpeed = 0.5f; lava.pulseAmount = 0.4f; lava.scrollSpeed = new Vector2(0.0015f, 0.0009f); lava.detail = 8; lava.shaderGround = 1; lava.shaderScale = 6f;
        yield return lava;

        FloorStyle space = S("Deep Space", FloorPattern.Space, new Color(0.01f, 0.01f, 0.04f), new Color(0.35f, 0.1f, 0.55f),
                             new Color(0.1f, 0.45f, 0.8f), 1024, false, 40f, 1, 0.6f, 1.1f);
        space.scrollSpeed = new Vector2(0.001f, 0.003f); space.detail = 2; space.shaderGround = 4; space.shaderScale = 9f;
        yield return space;

        FloorStyle ice = S("Ice", FloorPattern.Ice, new Color(0.7f, 0.85f, 0.95f), new Color(0.5f, 0.7f, 0.9f),
                           new Color(0.97f, 0.99f, 1f), 1024, false, 40f, 28, 0.95f);
        ice.stretchToFloor = true; ice.detail = 6; ice.shaderGround = 3; ice.shaderScale = 8f;
        yield return ice;

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
                              new Color(0.85f, 1f, 0.9f), 1024, true, 40f, 128, 0.5f, 1.3f);
        matrix.scrollSpeed = new Vector2(0f, 0.0375f); matrix.detail = 6; matrix.shaderGround = 8; matrix.shaderScale = 40f;
        yield return matrix;

        yield return S("Hazard Stripes", FloorPattern.Hazard, new Color(0.98f, 0.8f, 0.05f), new Color(0.08f, 0.08f, 0.08f),
                       new Color(0.45f, 0.42f, 0.38f), 128, false, 4f, 4, 0.3f);

        // ---- Dragon Ball themed ----
        yield return S("Tournament Stage", FloorPattern.TournamentTiles, new Color(0.86f, 0.84f, 0.78f), new Color(0.72f, 0.7f, 0.64f),
                       new Color(0.3f, 0.28f, 0.26f), 256, false, 6f, 4, 0.25f);

        FloorStyle radar = S("Radar", FloorPattern.DragonRadar, new Color(0.04f, 0.2f, 0.08f), new Color(0.3f, 0.8f, 0.35f),
                             new Color(1f, 0.55f, 0.05f), 512, false, 36f, 18, 0.7f, 0.9f);
        radar.stretchToFloor = true; radar.animateFps = 8f; radar.pulseSpeed = 0.5f; radar.pulseAmount = 0.2f;   // (one big radar screen; its blips glow and ping)
        yield return radar;


        // ---- Dragon Ball Z themed ----
        yield return S("Alien Planet", FloorPattern.PixelGrass, new Color(0.15f, 0.55f, 0.45f), new Color(0.3f, 0.72f, 0.55f),
                       new Color(0.95f, 0.85f, 0.3f), 48, true, 4f, 1, 0.05f);


        FloorStyle battle = S("Battle Crater", FloorPattern.BattleCrater, new Color(0.5f, 0.4f, 0.3f), new Color(0.36f, 0.29f, 0.22f),
                              new Color(0.12f, 0.09f, 0.07f), 1024, false, 40f, 7, 0.1f, 0.1f);
        battle.stretchToFloor = true; battle.detail = 6;   // (cells = how many smaller craters)
        yield return battle;

        yield return S("Chamber Tiles", FloorPattern.Checker, new Color(0.98f, 0.98f, 0.97f), new Color(0.93f, 0.93f, 0.92f),
                       Color.white, 64, false, 6f, 2, 0.9f);

        // ---- Dragon Ball Super themed ----
        FloorStyle top = S("Power Tournament", FloorPattern.PowerArena, new Color(0.62f, 0.66f, 0.74f), new Color(0.42f, 0.45f, 0.55f),
                           new Color(0.4f, 0.85f, 1f), 256, false, 6f, 6, 0.45f, 0.45f);
        top.pulseSpeed = 0.4f; top.pulseAmount = 0.4f;
        yield return top;

        FloorStyle ssj = S("Super Aura", FloorPattern.KiAura, new Color(0.35f, 0.18f, 0.0f), new Color(1f, 0.78f, 0.1f),
                           new Color(1f, 1f, 0.85f), 256, false, 6f, 1, 0.6f, 1.3f);
        ssj.scrollSpeed = new Vector2(0f, 0.0525f); ssj.pulseSpeed = 1.5f; ssj.pulseAmount = 0.3f; ssj.detail = 7; ssj.tileWorldSize = 40f; ssj.resolution = 1024; ssj.shaderGround = 6; ssj.shaderScale = 40f;
        yield return ssj;

        FloorStyle ui = S("Divine Aura", FloorPattern.KiAura, new Color(0.12f, 0.14f, 0.25f), new Color(0.65f, 0.75f, 1f),
                          new Color(1f, 1f, 1f), 256, false, 6f, 1, 0.8f, 1.2f);
        ui.scrollSpeed = new Vector2(0f, 0.0375f); ui.pulseSpeed = 0.7f; ui.pulseAmount = 0.35f; ui.seed = 91; ui.detail = 7; ui.tileWorldSize = 40f; ui.resolution = 1024; ui.shaderGround = 6; ui.shaderScale = 40f;
        yield return ui;

        FloorStyle blue = S("Blue Aura", FloorPattern.KiAura, new Color(0.0f, 0.12f, 0.3f), new Color(0.1f, 0.7f, 1f),
                            new Color(0.85f, 1f, 1f), 256, false, 6f, 1, 0.7f, 1.3f);
        blue.scrollSpeed = new Vector2(0f, 0.0525f); blue.pulseSpeed = 1.2f; blue.pulseAmount = 0.3f; blue.seed = 57; blue.detail = 7; blue.tileWorldSize = 40f; blue.resolution = 1024; blue.shaderGround = 6; blue.shaderScale = 40f;
        yield return blue;

        FloorStyle god = S("God Aura", FloorPattern.KiAura, new Color(0.3f, 0.02f, 0.05f), new Color(1f, 0.25f, 0.3f),
                           new Color(1f, 0.85f, 0.75f), 256, false, 6f, 1, 0.6f, 1.3f);
        god.scrollSpeed = new Vector2(0f, 0.045f); god.pulseSpeed = 1f; god.pulseAmount = 0.3f; god.seed = 73; god.detail = 7; god.tileWorldSize = 40f; god.resolution = 1024; god.shaderGround = 6; god.shaderScale = 40f;
        yield return god;

        FloorStyle hakai = S("Death Energy", FloorPattern.EnergyWisps, new Color(0.04f, 0.0f, 0.08f), new Color(0.55f, 0.1f, 0.85f),
                             new Color(1f, 0.7f, 1f), 256, false, 7f, 1, 0.6f, 1.2f);
        hakai.scrollSpeed = new Vector2(0.0035f, 0.006f); hakai.pulseSpeed = 0.5f; hakai.pulseAmount = 0.4f; hakai.detail = 6; hakai.tileWorldSize = 40f; hakai.resolution = 1024; hakai.shaderGround = 7; hakai.shaderScale = 40f;
        yield return hakai;

        // ---- Animated ----
        FloorStyle lasers = S("Laser Corridor", FloorPattern.LaserHall, new Color(0.2f, 0.34f, 0.36f), new Color(0.6f, 0.95f, 1f),
                              new Color(1f, 0.08f, 0.05f), 128, false, 18f, 8, 0.8f, 1.1f);   // (scaled up: bigger panels and beams)
        lasers.animateFps = 14f; lasers.metallic = 0.3f; lasers.shaderGround = 10; lasers.shaderScale = 18f;
        yield return lasers;

        FloorStyle liquid = S("Liquid Flow", FloorPattern.Liquid, new Color(0.02f, 0.15f, 0.35f), new Color(0.1f, 0.55f, 0.75f),
                              new Color(0.85f, 1f, 1f), 768, false, 40f, 12, 0.95f, 0.25f);
        liquid.detail = 3; liquid.scrollSpeed = new Vector2(0.004f, 0.006f); liquid.pulseSpeed = 0.3f; liquid.pulseAmount = 0.15f;   // (one big picture that drifts)
        liquid.shaderGround = 9; liquid.shaderScale = 40f;
        yield return liquid;

        FloorStyle clouds = S("Moving Clouds", FloorPattern.CloudSea, new Color(0.35f, 0.6f, 0.9f), new Color(0.72f, 0.78f, 0.88f),
                              Color.white, 256, false, 5f, 1, 0.2f);
        clouds.scrollSpeed = new Vector2(0.015f, 0.006f); clouds.fitToView = true;
        yield return clouds;

        FloorStyle kaleido = S("Kaleidoscope", FloorPattern.Kaleidoscope, new Color(0.02f, 0.02f, 0.05f), Color.white,
                               new Color(0.05f, 0.05f, 0.08f), 256, false, 8f, 8, 0.7f, 0.6f);
        kaleido.animateFps = 15f; kaleido.fitToView = true;
        yield return kaleido;

        FloorStyle pac = S("Pac-Man", FloorPattern.PacMan, new Color(0.15f, 0.25f, 1f), new Color(1f, 0.8f, 0.65f),
                           Color.black, 256, true, 8f, 1, 0.4f, 0.8f);
        pac.animateFps = 20f; pac.tileAspect = 19f / 21f; pac.fitToView = true;
        yield return pac;


        foreach (FloorStyle extra in ExtraStyles()) yield return extra;
        foreach (FloorStyle retro in RetroStyles()) yield return retro;
        foreach (FloorStyle world in WorldStyles()) yield return world;
        foreach (FloorStyle live in LiveStyles()) yield return live;
    }

    /// <summary>The second batch: space, tech, trippy, horror, a noir smoker, a skyline and ground-and-sky scenes (patterns in PixelFloorScenes.cs).</summary>
    private static IEnumerable<FloorStyle> ExtraStyles()
    {
        FloorStyle St(string name, FloorPattern p, Color a, Color b, Color c, int res, float tile, int cells, float fps, bool fit, float glow = 0f, float smooth = 0.4f)
        {
            FloorStyle f = S(name, p, a, b, c, res, false, tile, cells, smooth, glow);
            f.animateFps = fps; f.fitToView = fit;
            return f;
        }
        // ---- Space ----
        yield return St("Hyperspace", FloorPattern.Hyperspace, new Color(0f, 0f, 0.03f), new Color(0.5f, 0.7f, 1f), new Color(0.9f, 0.5f, 1f), 192, 8f, 6, 24f, true, 1f);
        // (Moon Surface and Red Planet: one big picture with a big crater, smaller ones and pits, like Battle Crater; cells = how many smaller craters.)
        FloorStyle moon = St("Moon Surface", FloorPattern.MoonSurface, new Color(0.55f, 0.55f, 0.57f), new Color(0.4f, 0.4f, 0.43f), new Color(0.15f, 0.15f, 0.17f), 1024, 40f, 10, 0f, false, 0f, 0.1f);
        moon.stretchToFloor = true; moon.detail = 6; moon.variant = 1;
        yield return moon;
        FloorStyle red = St("Red Planet", FloorPattern.MarsSurface, new Color(0.72f, 0.36f, 0.2f), new Color(0.52f, 0.25f, 0.14f), new Color(0.12f, 0.07f, 0.06f), 1024, 40f, 8, 0f, false, 0f, 0.1f);
        red.stretchToFloor = true; red.detail = 6; red.variant = 2;
        yield return red;
        // ---- Digital / tech ----
        // (Data Streams is drawn by the world-space ground shader in the game, pattern 5: lanes of packets over the whole floor circle, never stretched; the painted version is the menu picture / fallback.)
        FloorStyle streams = St("Data Streams", FloorPattern.DataStream, new Color(0.01f, 0.02f, 0.05f), new Color(0.1f, 0.8f, 1f), new Color(1f, 0.3f, 0.7f), 192, 8f, 16, 20f, true, 1f);
        streams.shaderGround = 5; streams.shaderScale = 1.1f;
        yield return streams;
        yield return St("Circuit Flow", FloorPattern.CircuitFlow, new Color(0.02f, 0.07f, 0.08f), new Color(0.1f, 0.7f, 0.8f), new Color(0.7f, 1f, 1f), 768, 36f, 12, 8f, false, 0.8f);   // (the circuit board is drawn once; only the sparks move)
        FloorStyle hex = St("Cyber Hex", FloorPattern.CyberHex, new Color(0.02f, 0.03f, 0.07f), new Color(0.2f, 0.55f, 1f), new Color(0.6f, 0.3f, 1f), 1024, 36f, 13, 0f, false, 0.9f);
        hex.pulseSpeed = 0.5f; hex.pulseAmount = 0.3f;
        yield return hex;
        yield return St("Glitch", FloorPattern.Glitch, new Color(0.03f, 0.03f, 0.05f), new Color(0.9f, 0.2f, 0.6f), new Color(0.2f, 1f, 0.9f), 192, 8f, 1, 14f, true, 0.4f);
        // ---- Trippy ----
        yield return St("Psychedelic", FloorPattern.Psychedelic, new Color(0.3f, 0.1f, 0.5f), Color.white, Color.white, 192, 8f, 1, 20f, true, 0.6f);
        yield return St("Hypno Spiral", FloorPattern.HypnoSpiral, new Color(0.05f, 0f, 0.15f), new Color(1f, 0.2f, 0.8f), Color.white, 192, 8f, 8, 20f, true, 0.4f);
        yield return St("Plasma", FloorPattern.Plasma, new Color(0.1f, 0f, 0.4f), new Color(1f, 0.2f, 0.5f), new Color(1f, 0.9f, 0.2f), 128, 8f, 1, 20f, true, 0.7f);
        yield return St("Op Art", FloorPattern.OpArt, Color.black, Color.white, Color.gray, 192, 8f, 6, 20f, true, 0f);
        // ---- Horror ----
        FloorStyle haunted = St("Haunted Tiles", FloorPattern.HauntedTiles, new Color(0.1f, 0.1f, 0.11f), new Color(0.55f, 0.55f, 0.52f), new Color(0.35f, 0.05f, 0.04f), 1024, 36f, 26, 0f, false, 0f, 0.25f);
        haunted.detail = 5;   // (drawn once; the lights flicker by dimming the material)
        yield return haunted;
        FloorStyle yard = St("Graveyard", FloorPattern.Graveyard, new Color(0.1f, 0.08f, 0.06f), new Color(0.2f, 0.17f, 0.12f), new Color(0.55f, 0.65f, 0.7f), 1024, 40f, 1, 0f, false, 0f, 0.1f);
        yard.detail = 6;
        yield return yard;
        yield return St("Watching Eyes", FloorPattern.WatchingEyes, new Color(0.02f, 0.02f, 0.03f), Color.black, new Color(1f, 0.1f, 0.05f), 1024, 36f, 11, 10f, false, 1.2f);   // (the ground is drawn once; only the eyes that blink are repainted)
        // ---- Scenes ----
        yield return St("City Skyline", FloorPattern.Skyline, new Color(0.02f, 0.03f, 0.12f), new Color(0.55f, 0.25f, 0.45f), new Color(0.06f, 0.05f, 0.12f), 256, 8f, 1, 8f, true, 0.3f);
        yield return St("Meadow Horizon", FloorPattern.GroundSky, new Color(0.25f, 0.5f, 0.9f), new Color(0.75f, 0.88f, 0.98f), new Color(0.3f, 0.62f, 0.2f), 256, 8f, 1, 0f, true, 0f, 0.15f);
        yield return St("Desert Dusk", FloorPattern.GroundSky, new Color(0.25f, 0.1f, 0.4f), new Color(1f, 0.6f, 0.3f), new Color(0.8f, 0.55f, 0.3f), 256, 8f, 2, 0f, true, 0f, 0.1f);
        yield return St("Arctic Dawn", FloorPattern.GroundSky, new Color(0.05f, 0.1f, 0.3f), new Color(0.7f, 0.6f, 0.7f), new Color(0.9f, 0.95f, 1f), 256, 8f, 3, 6f, true, 0.1f, 0.3f);
        yield return St("Twin Moons", FloorPattern.GroundSky, new Color(0.05f, 0.02f, 0.2f), new Color(0.5f, 0.2f, 0.45f), new Color(0.05f, 0.2f, 0.22f), 256, 8f, 4, 6f, true, 0.4f, 0.3f);
        // ---- Mining ----
        yield return St("Mine Track", FloorPattern.MineTrack, new Color(0.3f, 0.27f, 0.24f), new Color(0.5f, 0.46f, 0.42f), new Color(1f, 0.85f, 0.3f), 256, 6f, 4, 0f, false, 0f, 0.15f);
        yield return St("Ore Veins", FloorPattern.OreVeins, new Color(0.14f, 0.14f, 0.16f), new Color(1f, 0.75f, 0.25f), new Color(0.3f, 0.85f, 1f), 256, 6f, 5, 0f, false, 0.15f, 0.35f);
        yield return St("Rock Strata", FloorPattern.Strata, new Color(0.55f, 0.4f, 0.28f), new Color(0.72f, 0.6f, 0.45f), new Color(0.38f, 0.3f, 0.26f), 256, 6f, 8, 0f, false, 0f, 0.1f);
        // ---- Farming ----
        yield return St("Crop Rows", FloorPattern.CropRows, new Color(0.2f, 0.12f, 0.07f), new Color(0.4f, 0.26f, 0.14f), new Color(0.3f, 0.7f, 0.22f), 256, 6f, 8, 0f, false, 0f, 0.1f);
        yield return St("Wheat Field", FloorPattern.WheatField, new Color(0.62f, 0.45f, 0.14f), new Color(0.95f, 0.8f, 0.35f), new Color(1f, 0.92f, 0.55f), 128, 6f, 1, 15f, false, 0f, 0.1f);
        yield return St("Farm Patchwork", FloorPattern.Patchwork, new Color(0.12f, 0.34f, 0.12f), new Color(0.88f, 0.72f, 0.28f), new Color(0.38f, 0.24f, 0.14f), 256, 8f, 4, 0f, false, 0f, 0.1f);
    }
}
