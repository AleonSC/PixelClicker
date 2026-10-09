using UnityEngine;

/// <summary>
/// Horizon fog: a soft, see-through sheet lying just above the floor that thickens with distance from the camera, so the
/// floor's far edge melts into the sky instead of ending in a hard line. It only covers the floor (and a wide skirt past
/// its edge, plus a short curtain at the very end), never the cube, the pixels or the UI. Its colour follows the sky's
/// horizon (Pixel Skybox), else the camera's background, else <see cref="classicSkyColor"/>.
/// Drawn with the unlit Sprites/Default shader and vertex colours (no Unity fog keywords, which builds tend to strip).
/// Player switch: Settings > Display > Horizon fog. Added by PixelClicker.Awake if missing.
/// </summary>
public class PixelHorizonFog : MonoBehaviour
{
    public enum FogColorMode { MatchSky, Custom }

    [Header("Fog")]
    [Tooltip("Where the fog begins, as a fraction of the distance at which it is full (0.5 = it starts halfway to the far edge).")]
    [Range(0f, 0.95f)]
    [SerializeField] private float startFraction = 0.55f;

    [Tooltip("Where the fog is full, as a fraction of the distance from the camera to the floor's far edge (1 = exactly at the edge).")]
    [Range(0.3f, 1.5f)]
    [SerializeField] private float endFraction = 1f;

    [Tooltip("How thick the fog gets at its fullest (1 = the floor is completely hidden there).")]
    [Range(0f, 1f)]
    [SerializeField] private float maxOpacity = 1f;

    [Tooltip("Shape of the fade: 1 = even, higher = stays thin longer then thickens quickly near the edge.")]
    [Range(0.3f, 4f)]
    [SerializeField] private float fadePower = 1.4f;

    [Header("Colour")]
    [Tooltip("MatchSky = the current sky's horizon colour; Custom = Custom Color.")]
    [SerializeField] private FogColorMode colorMode = FogColorMode.MatchSky;

    [Tooltip("Fog colour in Custom mode.")]
    [SerializeField] private Color customColor = new Color(0.72f, 0.8f, 0.9f, 1f);

    [Tooltip("Fog colour for the scene's own (Classic) skybox, when its horizon colour can't be worked out.")]
    [SerializeField] private Color classicSkyColor = new Color(0.72f, 0.8f, 0.9f, 1f);

    [Header("Shape")]
    [Tooltip("How far the fog sheet reaches past the floor, in floor sizes (it covers the empty ground between the floor and the horizon).")]
    [SerializeField] private float skirtSize = 6f;

    [Tooltip("Height of the fading curtain at the far end of the sheet, as a fraction of its distance (blends it into the sky).")]
    [SerializeField] private float curtainFraction = 0.08f;

    [Tooltip("How far above the floor the sheet lies (world units); it rises slightly with distance so it never flickers.")]
    [SerializeField] private float heightOffset = 0.01f;

    [Tooltip("Grid points per side of the fog sheet (more = smoother fade).")]
    [SerializeField] private int gridSize = 64;

    [Tooltip("Draw order of the fog sheet (Transparent = 3000). Keep it below other see-through effects you want on top.")]
    [SerializeField] private int renderQueue = 2990;

    /// <summary>The fog component in the scene (null until it wakes).</summary>
    public static PixelHorizonFog Instance { get; private set; }

    private const string PrefEnabled = "PixelClicker.Setting.HorizonFog";
    private static int enabledCache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { enabledCache = -1; Instance = null; }

    /// <summary>Player setting (Settings > Display > Horizon fog), on by default.</summary>
    public static bool Enabled
    {
        get
        {
            if (enabledCache < 0) enabledCache = PlayerPrefs.GetInt(PrefEnabled, 1);
            return enabledCache != 0;
        }
        set
        {
            enabledCache = value ? 1 : 0;
            PlayerPrefs.SetInt(PrefEnabled, enabledCache);
        }
    }

    /// <summary>Re-reads the setting (a save file's settings were just applied).</summary>
    public static void ReloadFromPrefs() { enabledCache = -1; }

    private GameObject root;
    private Mesh mesh;
    private Material material;
    private Vector3[] vertices;
    private Color[] colors;
    private int sheetCount;               // vertices in the flat sheet (the rest are the curtain)
    private Bounds builtFor;
    private float builtFarClip;
    private Vector3 lastCamPos;
    private Quaternion lastCamRot;
    private Color lastColor;
    private float lastEnd = -1f;

    private void Awake() { Instance = this; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (root != null) Destroy(root);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }

    private void LateUpdate()
    {
        Renderer floor = PixelFloor.Instance != null ? PixelFloor.Instance.FloorRenderer : null;
        Camera cam = Camera.main;
        bool show = Enabled && floor != null && floor.enabled && floor.gameObject.activeInHierarchy && cam != null;
        if (!show)
        {
            if (root != null && root.activeSelf) root.SetActive(false);
            return;
        }

        Bounds b = floor.bounds;
        if (root == null || !SameBounds(b, builtFor) || !Mathf.Approximately(cam.farClipPlane, builtFarClip)) Build(b, cam);
        if (root == null) return;
        if (!root.activeSelf) root.SetActive(true);

        float end = FullFogDistance(b, cam);
        Color c = FogColor(cam);
        Transform ct = cam.transform;
        if (ct.position == lastCamPos && ct.rotation == lastCamRot && c == lastColor && Mathf.Approximately(end, lastEnd)) return;
        lastCamPos = ct.position;
        lastCamRot = ct.rotation;
        lastColor = c;
        lastEnd = end;
        Recolor(ct.position, end, c);
    }

    private static bool SameBounds(Bounds a, Bounds b) => (a.center - b.center).sqrMagnitude < 1e-6f && (a.size - b.size).sqrMagnitude < 1e-6f;

    // ------------------------------------------------------------------
    // Where the fog is full: the nearest far edge of the floor
    // ------------------------------------------------------------------

    private float FullFogDistance(Bounds b, Camera cam)
    {
        Vector3 p = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        Vector2 f = new Vector2(fwd.x, fwd.z);
        f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector2.up;

        // The four sides: outward direction and the camera's distance to that side line.
        float best = float.MaxValue;
        Consider(new Vector2(1f, 0f), b.max.x - p.x, f, ref best);
        Consider(new Vector2(-1f, 0f), p.x - b.min.x, f, ref best);
        Consider(new Vector2(0f, 1f), b.max.z - p.z, f, ref best);
        Consider(new Vector2(0f, -1f), p.z - b.min.z, f, ref best);
        if (best == float.MaxValue) best = Mathf.Max(b.extents.x, b.extents.z); // camera outside the floor or looking straight down
        return Mathf.Max(0.5f, best * endFraction);
    }

    private static void Consider(Vector2 outward, float distance, Vector2 forward, ref float best)
    {
        if (distance <= 0f) return;                       // the camera is past this side
        if (Vector2.Dot(outward, forward) < 0.25f) return; // a side or near edge, not a far one
        if (distance < best) best = distance;
    }

    // ------------------------------------------------------------------
    // Colour
    // ------------------------------------------------------------------

    private Color FogColor(Camera cam)
    {
        Color c;
        if (colorMode == FogColorMode.Custom) c = customColor;
        else if (PixelSkybox.Instance != null && PixelSkybox.Instance.TryGetHorizonColor(out Color sky)) c = sky;
        else if (cam.clearFlags == CameraClearFlags.SolidColor) c = cam.backgroundColor;
        else c = classicSkyColor;
        c.a = 1f;
        return c;
    }

    // ------------------------------------------------------------------
    // Building the sheet (once per floor size) and recolouring it (when the camera or sky changes)
    // ------------------------------------------------------------------

    private void Build(Bounds b, Camera cam)
    {
        builtFor = b;
        builtFarClip = cam.farClipPlane;
        lastEnd = -1f; // force a recolour

        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return;
        if (root == null)
        {
            root = new GameObject("Horizon Fog");
            mesh = new Mesh { name = "Horizon Fog" };
            mesh.MarkDynamic();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = root.AddComponent<MeshRenderer>();
            material = new Material(shader) { name = "Horizon Fog", renderQueue = renderQueue };
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        int n = Mathf.Clamp(gridSize, 8, 200);
        Vector3 centre = new Vector3(b.center.x, b.max.y, b.center.z);
        float halfX = b.extents.x, halfZ = b.extents.z;
        float floorSize = Mathf.Max(b.size.x, b.size.z);
        float outer = Mathf.Max(halfX, halfZ) + floorSize * Mathf.Max(0f, skirtSize);
        // Stay inside the camera's far clip so the sheet is never cut off.
        float camToCentre = Vector3.Distance(cam.transform.position, centre);
        float maxOuter = (cam.farClipPlane * 0.9f - camToCentre) / 1.42f;
        outer = Mathf.Max(Mathf.Max(halfX, halfZ) * 1.05f, Mathf.Min(outer, maxOuter));
        float outerX = Mathf.Max(outer, halfX * 1.05f), outerZ = Mathf.Max(outer, halfZ * 1.05f);

        sheetCount = n * n;
        int curtainCols = (n - 1) * 4;      // around the outer edge
        vertices = new Vector3[sheetCount + curtainCols * 2];
        colors = new Color[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Grid: the middle half of the points covers the floor, the outer quarters stretch out over the skirt.
        for (int j = 0; j < n; j++)
        {
            float zt = j / (float)(n - 1) * 2f - 1f;
            float z = Spread(zt, halfZ, outerZ);
            for (int i = 0; i < n; i++)
            {
                float xt = i / (float)(n - 1) * 2f - 1f;
                float x = Spread(xt, halfX, outerX);
                float lift = heightOffset + Mathf.Sqrt(x * x + z * z) * 0.002f;
                vertices[j * n + i] = centre + new Vector3(x, lift, z);
            }
        }

        // Curtain: a ring of quads standing on the sheet's outer edge.
        int[] edge = new int[curtainCols];
        int e = 0;
        for (int i = 0; i < n - 1; i++) edge[e++] = i;                          // bottom row, left to right
        for (int j = 0; j < n - 1; j++) edge[e++] = j * n + (n - 1);            // right column, up
        for (int i = n - 1; i > 0; i--) edge[e++] = (n - 1) * n + i;            // top row, right to left
        for (int j = n - 1; j > 0; j--) edge[e++] = j * n;                      // left column, down
        float curtainHeight = outer * Mathf.Max(0f, curtainFraction);
        for (int k = 0; k < curtainCols; k++)
        {
            Vector3 bottom = vertices[edge[k]];
            vertices[sheetCount + k * 2] = bottom;
            vertices[sheetCount + k * 2 + 1] = bottom + Vector3.up * curtainHeight;
        }

        int[] tris = new int[(n - 1) * (n - 1) * 6 + curtainCols * 6];
        int t = 0;
        for (int j = 0; j < n - 1; j++)
        {
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, bIdx = a + 1, c = a + n, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = bIdx;
                tris[t++] = bIdx; tris[t++] = c; tris[t++] = d;
            }
        }
        for (int k = 0; k < curtainCols; k++)
        {
            int a = sheetCount + k * 2, a2 = a + 1;
            int bIdx = sheetCount + ((k + 1) % curtainCols) * 2, b2 = bIdx + 1;
            tris[t++] = a; tris[t++] = a2; tris[t++] = bIdx;   // Sprites/Default draws both sides, so winding doesn't matter
            tris[t++] = bIdx; tris[t++] = a2; tris[t++] = b2;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }

    /// <summary>Maps -1..1 to a position: the middle half covers the floor (half-size 'inner'), the rest reaches 'outer'.</summary>
    private static float Spread(float t, float inner, float outer)
    {
        float a = Mathf.Abs(t);
        float v = a <= 0.5f ? a / 0.5f * inner : inner + (a - 0.5f) / 0.5f * (outer - inner);
        return Mathf.Sign(t) * v;
    }

    private void Recolor(Vector3 camPos, float end, Color fog)
    {
        if (vertices == null || mesh == null) return;
        float start = end * startFraction;
        Color baseColor = QualitySettings.activeColorSpace == ColorSpace.Linear ? fog.linear : fog;

        for (int i = 0; i < sheetCount; i++)
        {
            Vector3 v = vertices[i];
            float dx = v.x - camPos.x, dz = v.z - camPos.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float k = Mathf.Clamp01((d - start) / Mathf.Max(0.01f, end - start));
            k = Mathf.Pow(Mathf.SmoothStep(0f, 1f, k), fadePower);
            Color c = baseColor;
            c.a = k * maxOpacity;
            colors[i] = c;
        }
        for (int i = sheetCount; i < colors.Length; i += 2)
        {
            Color c = baseColor;
            c.a = maxOpacity;
            colors[i] = c;          // bottom of the curtain: full fog
            c.a = 0f;
            colors[i + 1] = c;      // top: fades into the sky
        }
        mesh.colors = colors;
    }
}
