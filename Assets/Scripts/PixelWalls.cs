using UnityEngine;

/// <summary>Marks an invisible wall so click / placement raycasts look straight through it.</summary>
public class PixelWallMarker : MonoBehaviour { }

/// <summary>
/// Invisible physics walls along the top and bottom of the screen (optionally the sides) that box in the old pixels, so
/// they can't fall out of view or fly off the top. The walls sit where the screen edges are at the cube's depth, and
/// stay just inside the black bars. They never block clicks or device placement.
///
/// In dev mode (Editor / development builds, with the Dev Tools "Show walls" box ticked) the walls are drawn slightly
/// see-through so you can see where they are; otherwise they are invisible. Fly-away pixels (Meteor) have no collider,
/// so they still leave the screen.
///
/// Added automatically by PixelHud. Add it to any GameObject yourself to change its settings in the Inspector.
/// </summary>
public class PixelWalls : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker (for the camera and the cube's position). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Walls")]
    [Tooltip("Turn all the walls on or off.")]
    [SerializeField] private bool wallsActive = true;

    [Tooltip("A wall along the bottom of the screen (old pixels land on it).")]
    [SerializeField] private bool bottomWall = true;

    [Tooltip("A wall along the top of the screen (old pixels bounce off it).")]
    [SerializeField] private bool topWall = true;

    [Tooltip("Walls down the left and right edges too.")]
    [SerializeField] private bool sideWalls = false;

    [Tooltip("Put the top and bottom walls at the inner edge of the black bars instead of the very edge of the screen.")]
    [SerializeField] private bool insideBlackBars = true;

    [Range(-0.2f, 0.4f)]
    [Tooltip("Moves every wall further in (positive) or out (negative), as a fraction of the screen.")]
    [SerializeField] private float inset = 0f;

    [Min(0.5f)]
    [Tooltip("Thickness of a wall (world units). Thick walls stop fast pixels tunnelling through.")]
    [SerializeField] private float thickness = 4f;

    [Min(5f)]
    [Tooltip("How far a wall extends sideways and into the scene (world units), so pixels can't go round it.")]
    [SerializeField] private float length = 60f;

    [Header("Surface")]
    [Range(0f, 1f)]
    [Tooltip("How bouncy the walls are.")]
    [SerializeField] private float bounciness = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("How much old pixels grip the walls.")]
    [SerializeField] private float friction = 0.4f;

    [Header("Dev Mode Look")]
    [Tooltip("Colour of the walls while they are shown (keep the alpha low so they are slightly see-through).")]
    [SerializeField] private Color devColor = new Color(1f, 0.35f, 0.25f, 0.18f);

    private class Wall
    {
        public GameObject go;
        public Renderer renderer;
    }

    private Wall top, bottom, left, right;
    private Material devMaterial;
    private Camera cam;
    private float checkTimer;
    private string lastSignature = "";
    private bool built;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelWalls: no PixelClicker found in the scene.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        built = true;
        Rebuild();
    }

    private void OnDestroy()
    {
        foreach (Wall w in new[] { top, bottom, left, right })
            if (w != null && w.go != null) Destroy(w.go);
    }

    private void Update()
    {
        if (!built) return;

        // Re-place the walls when the screen, camera or cube moves (checked a few times a second).
        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.25f;
            string sig = Signature();
            if (sig != lastSignature) Rebuild();
        }

        // Dev mode: shown see-through; normal play: invisible.
        bool show = PixelDevTools.Available && PixelDevTools.WallsVisible;
        foreach (Wall w in new[] { top, bottom, left, right })
            if (w != null && w.renderer != null && w.renderer.enabled != show) w.renderer.enabled = show;
    }

    private string Signature()
    {
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null || clicker.PixelTransform == null) return "";
        float bars = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        return Screen.width + "x" + Screen.height + "|" + cam.fieldOfView.ToString("0.##") + "|" + cam.orthographic + "|" +
               cam.transform.position + cam.transform.rotation.eulerAngles + "|" + clicker.PixelTransform.position + "|" +
               bars + "|" + wallsActive + bottomWall + topWall + sideWalls + insideBlackBars + inset + thickness + length;
    }

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    private Wall MakeWall(string name)
    {
        Wall w = new Wall();
        w.go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.go.name = name;
        w.go.transform.SetParent(transform, false);
        w.go.AddComponent<PixelWallMarker>();

        BoxCollider box = w.go.GetComponent<BoxCollider>();
#if UNITY_6000_0_OR_NEWER
        box.sharedMaterial = new PhysicsMaterial("Wall") { bounciness = bounciness, dynamicFriction = friction, staticFriction = friction };
#else
        box.sharedMaterial = new PhysicMaterial("Wall") { bounciness = bounciness, dynamicFriction = friction, staticFriction = friction };
#endif

        w.renderer = w.go.GetComponent<Renderer>();
        if (devMaterial == null) devMaterial = clicker.CreateVisualMaterial(devColor, true);
        if (devMaterial != null) w.renderer.sharedMaterial = devMaterial;
        w.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        w.renderer.enabled = false;
        return w;
    }

    private void Rebuild()
    {
        lastSignature = Signature();
        if (lastSignature == "") return; // no camera or cube yet

        // A wall that isn't wanted is removed, one that is wanted is created once and then only moved.
        bool on = wallsActive;
        EnsureWall(ref bottom, "Bottom Wall", on && bottomWall);
        EnsureWall(ref top, "Top Wall", on && topWall);
        EnsureWall(ref left, "Left Wall", on && sideWalls);
        EnsureWall(ref right, "Right Wall", on && sideWalls);

        float bars = insideBlackBars && PixelHud.Instance != null && PixelHud.Instance.BarHeight > 0f ? BarFraction() : 0f;
        float vBottom = bars + inset, vTop = 1f - bars - inset;

        Vector3 centre = clicker.PixelTransform.position;
        float depth = Mathf.Max(cam.nearClipPlane + 0.1f, Vector3.Dot(centre - cam.transform.position, cam.transform.forward));
        Vector3 pBottom = cam.ViewportToWorldPoint(new Vector3(0.5f, vBottom, depth));
        Vector3 pTop = cam.ViewportToWorldPoint(new Vector3(0.5f, vTop, depth));
        Vector3 pLeft = cam.ViewportToWorldPoint(new Vector3(inset, 0.5f, depth));
        Vector3 pRight = cam.ViewportToWorldPoint(new Vector3(1f - inset, 0.5f, depth));

        // Walls are level with the world and turned to face the way the camera looks (yaw only).
        Quaternion yaw = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        Vector3 right3 = yaw * Vector3.right;

        // Bottom: its top surface is at the screen's bottom edge. Top: its underside is at the top edge.
        Place(bottom, new Vector3(centre.x, pBottom.y - thickness * 0.5f, centre.z), yaw, new Vector3(length, thickness, length));
        Place(top, new Vector3(centre.x, pTop.y + thickness * 0.5f, centre.z), yaw, new Vector3(length, thickness, length));
        // Sides: standing walls at the left / right screen edges.
        Place(left, new Vector3(pLeft.x, centre.y, pLeft.z) - right3 * (thickness * 0.5f), yaw, new Vector3(thickness, length, length));
        Place(right, new Vector3(pRight.x, centre.y, pRight.z) + right3 * (thickness * 0.5f), yaw, new Vector3(thickness, length, length));
    }

    private void EnsureWall(ref Wall wall, string name, bool wanted)
    {
        if (wanted && wall == null) wall = MakeWall(name);
        else if (!wanted && wall != null)
        {
            if (wall.go != null) Destroy(wall.go);
            wall = null;
        }
    }

    private static void Place(Wall w, Vector3 position, Quaternion rotation, Vector3 size)
    {
        if (w == null || w.go == null) return;
        w.go.transform.SetPositionAndRotation(position, rotation);
        w.go.transform.localScale = size;
    }

    /// <summary>The bars' thickness as a fraction of the screen's height.</summary>
    private float BarFraction()
    {
        Canvas canvas = null;
        if (PixelHud.Instance != null) canvas = PixelHud.Instance.GetComponentInChildren<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : Screen.height / 1080f;
        // The hud's canvas is a child object that was created at runtime, so fall back to the usual reference scaling.
        if (canvas == null) scale = Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f);
        return Mathf.Clamp01(PixelHud.Instance.BarHeight * scale / Mathf.Max(1, Screen.height));
    }
}
