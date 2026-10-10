using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// Robot Worker (shop upgrade <c>unlocksRobot</c>, Upgrades tab): a little block-built robot lives on the left of the screen, next to
/// his charging station, with a speech bubble above him (a robot face) so you can always find him.
/// Click him and he asks which consumable to use; pick one (Vacuum, Fan, Sorter, Charge Booster, Lightning Rod) and place its
/// "ghost" where you want it (the same placing as by hand - nothing is used up yet). From then on he WALKS to the ghost and, every time
/// that device runs out, puts a fresh one on it as long as you have one to spare. When you have none left he walks back to the
/// charging station. Click him (or his bubble) again to change the job or clear it.
/// </summary>
public class PixelRobotWorker : MonoBehaviour
{
    [Header("Robot")]
    [Tooltip("Turn the Robot Worker off completely.")]
    [SerializeField] private bool disableRobot = false;

    [Tooltip("Where his charging station is: a point on the screen as a fraction of its width (0 = left, 1 = right). It sits on the floor under it.")]
    [SerializeField] private float screenX = 0.1f;

    [Tooltip("Where his charging station is: a point on the screen as a fraction of its height (0 = bottom, 1 = top).")]
    [SerializeField] private float screenY = 0.3f;

    [Tooltip("How fast he walks, in main-pixel widths per second.")]
    [SerializeField] private float walkSpeed = 3.5f;

    [Tooltip("How far from the ghost he stands while working, in main-pixel widths (on the side facing the station).")]
    [SerializeField] private float standDistance = 1.6f;

    [Tooltip("Size of the speech bubble with his face (canvas units).")]
    [SerializeField] private float bubbleSize = 96f;

    [Tooltip("Turn off the bubble over his head.")]
    [SerializeField] private bool hideBubble = false;

    [Tooltip("His height as a multiple of the main pixel's edge.")]
    [SerializeField] private float robotScale = 0.6f;

    [Tooltip("Seconds he spends 'working' before a new device appears on the ghost.")]
    [SerializeField] private float placeDelay = 1.2f;

    [Tooltip("Seconds between looks at whether the device is still there.")]
    [SerializeField] private float checkInterval = 0.3f;

    [Tooltip("Size of his label above him (3D text).")]
    [SerializeField] private float labelSize = 3.5f;

    [Header("Colours")]
    [SerializeField] private Color bodyColor = new Color(0.55f, 0.6f, 0.7f, 1f);
    [SerializeField] private Color accentColor = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.3f, 0.95f, 1f, 1f);
    [SerializeField] private Color darkColor = new Color(0.12f, 0.13f, 0.18f, 1f);

    [Header("Texts")]
    [SerializeField] private string clickMeText = "Click me!";
    [SerializeField] private string changeJobText = "Click to change job";
    [SerializeField] private string outOfFormat = "Out of {0}";
    [SerializeField] private string windowTitle = "Robot Worker";
    [SerializeField] private string askText = "Which device should I keep running? Pick one, then choose where it goes.";
    [SerializeField] private string noDevicesText = "You have no devices to work with yet.";
    [SerializeField] private string clearJobText = "Stop working";
    [SerializeField] private string jobSetFormat = "The Robot Worker will keep a {0} running there";

    private PixelClicker clicker;
    private PixelConsumables consumables;
    private bool active;

    // The job.
    private int jobDevice = -1;
    private Vector3 jobPos;
    private float jobYaw, jobBend;
    private GameObject ghost;
    private bool occupied;
    private float nextCheck, waitT, workT;

    // The robot.
    private GameObject root;
    private Transform head, leftArm, rightArm, bodyPivot;
    private Renderer antennaLight, chestLight;
    private BoxCollider box;
    private TextMeshPro label;
    private Transform legLeft, legRight, stationRoot;
    private Renderer stationLight;
    private Vector3 homePos;
    private bool hasHome;
    private float nextSpotTime;
    private bool hovering, walking;
    private float cellSize;   // side of the floor tile he stands on (0 = the floor has no grid: free standing)

    // The bubble with his face.
    private GameObject bubbleCanvas;
    private Canvas bubbleCanvasComponent;
    private RectTransform bubbleRect;
    private Sprite bubbleSprite;

    // The window.
    private GameObject windowRoot;
    private RectTransform windowPanel;
    private int ignoreClickFrame = -1;

    /// <summary>True while the mouse is over the robot (the cube ignores that click).</summary>
    public static bool Hovering;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Hovering = false; }

    public bool Active => active && !disableRobot;

    /// <summary>The shop upgrade was bought.</summary>
    public void Activate() { active = true; }

    /// <summary>The upgrade is not owned (the job, if any, is kept for when it is).</summary>
    public void Deactivate()
    {
        active = false;
        Hovering = false;
        if (root != null) root.SetActive(false);
        if (stationRoot != null) stationRoot.gameObject.SetActive(false);
        if (bubbleCanvas != null) bubbleCanvas.SetActive(false);
        if (ghost != null) ghost.SetActive(false);
        CloseWindow();
    }

    private void Start()
    {
        clicker = PixelFind.First<PixelClicker>();
        consumables = PixelFind.First<PixelConsumables>();
        PixelWindows.Register(this, 55, () => windowRoot != null && windowRoot.activeSelf, CloseWindow);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        Hovering = false;
        if (root != null) Destroy(root);
        if (stationRoot != null) Destroy(stationRoot.gameObject);
        if (bubbleCanvas != null) Destroy(bubbleCanvas);
        if (ghost != null) Destroy(ghost);
        if (windowRoot != null) Destroy(windowRoot);
    }

    // ------------------------------------------------------------------
    // Per frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (clicker == null || consumables == null) return;
        bool show = Active && !PixelTitleScreen.Showing && !PixelMinigame.TakeoverActive;
        if (!show)
        {
            Hovering = false;
            if (root != null && root.activeSelf) root.SetActive(false);
            if (stationRoot != null && stationRoot.gameObject.activeSelf) stationRoot.gameObject.SetActive(false);
            if (bubbleCanvas != null && bubbleCanvas.activeSelf) bubbleCanvas.SetActive(false);
            if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
            return;
        }

        if (root == null) BuildRobot();
        if (!root.activeSelf) root.SetActive(true);
        if (!stationRoot.gameObject.activeSelf) stationRoot.gameObject.SetActive(true);

        UpdateMovement();
        UpdateHoverAndClick();
        UpdateJob();
        Animate();
        UpdateBubble();
    }

    private Camera Cam => clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

    /// <summary>One model unit in world units: he is as wide as a floor tile when the floor has a grid, else a size relative to the main pixel.</summary>
    private float UnitScale => cellSize > 0f ? cellSize / 1.5f : clicker.PixelBaseSize * robotScale / 2.5f;

    /// <summary>The floor spot where he stands next to the ghost: on the side facing his station (a sorter's ring is round the cube, so further out).</summary>
    private Vector3 PostSpot()
    {
        PixelConsumables.Device d = consumables.GetDevice(jobDevice);
        bool sorter = d.kind == PixelConsumables.DeviceKind.Sorter;
        Vector3 anchor = jobPos;
        float extra = 0f;
        if (sorter && clicker.PixelTransform != null)
        {
            anchor = new Vector3(clicker.PixelTransform.position.x, homePos.y, clicker.PixelTransform.position.z);
            extra = 1.8f;
        }
        Vector3 away = homePos - anchor; away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.left;
        Vector3 spot = cellSize > 0f ? anchor + away * (cellSize + extra * clicker.PixelBaseSize)   // always the neighbouring tile
                                      : anchor + away * (standDistance + extra) * clicker.PixelBaseSize;
        spot.y = homePos.y;
        if (cellSize > 0f && PixelFloor.Instance != null && PixelFloor.Instance.TryGetCell(spot, out Vector3 snapped, out _))
        {
            spot = snapped;
            spot.y = homePos.y;
        }
        return spot;
    }

    private bool HasJob => jobDevice >= 0 && jobDevice < consumables.DeviceCount;

    private bool AtPost()
    {
        if (!HasJob || !hasHome) return false;
        Vector3 d = PostSpot() - root.transform.position; d.y = 0f;
        return d.magnitude < 0.15f * clicker.PixelBaseSize;
    }

    /// <summary>Where he wants to be: at the ghost while there is work (or a device running there and he is already standing by), else at the charging station.</summary>
    private Vector3 WantedSpot()
    {
        if (HasJob)
        {
            bool stock = consumables.DeviceOwned(jobDevice) > 0;
            if (stock || (occupied && AtPost())) return PostSpot();
        }
        return homePos;
    }

    private void UpdateMovement()
    {
        if (Time.unscaledTime >= nextSpotTime)
        {
            nextSpotTime = Time.unscaledTime + 0.5f;
            if (consumables.TryGetFloorPoint(new Vector2(Screen.width * screenX, Screen.height * screenY), out Vector3 p))
            {
                if (PixelFloor.Instance != null && PixelFloor.Instance.TryGetCell(p, out Vector3 cell, out float cs)) { p = cell; cellSize = cs; }   // lined up with a floor tile
                else cellSize = 0f;
                homePos = p;
                if (!hasHome) { hasHome = true; root.transform.position = p; }
            }
        }
        if (!hasHome) return;

        stationRoot.position = homePos;
        float unit = UnitScale;
        stationRoot.localScale = Vector3.one * (cellSize > 0f ? cellSize / 2.1f : unit);   // the pad is exactly one tile

        Camera cam = Cam;
        Vector3 toCam = cam != null ? cam.transform.position - root.transform.position : Vector3.back; toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) toCam = Vector3.back;
        stationRoot.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);

        // Walk.
        Vector3 target = WantedSpot();
        Vector3 flat = target - root.transform.position; flat.y = 0f;
        float dist = flat.magnitude;
        walking = dist > 0.04f * clicker.PixelBaseSize;
        Vector3 faceDir;
        if (walking)
        {
            float step = walkSpeed * clicker.PixelBaseSize * Time.deltaTime;
            Vector3 pos = root.transform.position + flat.normalized * Mathf.Min(step, dist);
            pos.y = Mathf.Lerp(pos.y, target.y, 0.2f);
            root.transform.position = pos;
            faceDir = flat.normalized;
        }
        else
        {
            // Standing: face the camera, turned a little towards the cube.
            Vector3 toCube = clicker.PixelTransform != null ? clicker.PixelTransform.position - root.transform.position : toCam; toCube.y = 0f;
            faceDir = toCube.sqrMagnitude > 0.0001f ? Vector3.Slerp(toCam.normalized, toCube.normalized, 0.3f) : toCam.normalized;
        }
        Quaternion want = Quaternion.LookRotation(-faceDir, Vector3.up);   // the model's front is -Z
        root.transform.rotation = Quaternion.Slerp(root.transform.rotation, want, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        root.transform.localScale = Vector3.one * unit * (hovering ? 1.06f : 1f);
    }

    private void UpdateHoverAndClick()
    {
        Camera cam = Cam;
        bool blocked = cam == null || Time.timeScale <= 0f || PixelPauseMenu.IsPaused || consumables.IsPlacing ||
                       (windowRoot != null && windowRoot.activeSelf) || PixelFirstPerson.Active || PixelClicker.GodMode;
        hovering = false;
        if (!blocked && !PointerOverUI())
        {
            Ray ray = cam.ScreenPointToRay(PointerPosition());
            hovering = box != null && box.Raycast(ray, out _, 1000f);
        }
        Hovering = hovering;
        if (hovering && LeftPressed() && Time.frameCount != ignoreClickFrame) OpenWindow();
    }

    private void UpdateJob()
    {
        if (!HasJob)
        {
            if (ghost != null) ghost.SetActive(false);
            waitT = 0f;
            SetLabel(hovering ? clickMeText : "", hovering);
            return;
        }

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            occupied = IsOccupied();
        }
        if (ghost != null && ghost.activeSelf == occupied) ghost.SetActive(!occupied);

        PixelConsumables.Device d = consumables.GetDevice(jobDevice);
        bool stock = consumables.DeviceOwned(jobDevice) > 0;
        if (occupied) { waitT = 0f; SetLabel(hovering ? changeJobText : "", hovering); return; }
        if (!stock)
        {
            waitT = 0f;
            SetLabel(string.Format(outOfFormat, d.displayName), !walking, new Color(1f, 0.4f, 0.35f, 1f));
            return;
        }

        SetLabel(hovering ? changeJobText : "", hovering);
        if (!AtPost()) { waitT = 0f; return; }   // still walking over

        workT = 0.25f;
        waitT += Time.deltaTime;
        if (waitT >= placeDelay)
        {
            waitT = 0f;
            PixelPlacedDevice placed = consumables.RobotPlace(jobDevice, jobPos, jobYaw, jobBend);
            if (placed != null)
            {
                occupied = true;
                nextCheck = Time.time + checkInterval;
                PixelStats.Count("robot.placed");
                if (ghost != null) ghost.SetActive(false);
            }
        }
    }

    /// <summary>Is a device of the job's kind already standing there (or, for the sorter, anywhere)?</summary>
    private bool IsOccupied()
    {
        bool sorter = consumables.GetDevice(jobDevice).kind == PixelConsumables.DeviceKind.Sorter;
        float reach = clicker.PixelBaseSize * 1.5f;
        foreach (PixelPlacedDevice p in PixelPlacedDevice.All)
        {
            if (p == null || p.DeviceIndex != jobDevice || p.IsEnding) continue;
            if (sorter) return true;
            Vector3 a = p.transform.position - jobPos; a.y = 0f;
            if (a.magnitude <= reach) return true;
        }
        return false;
    }

    private void SetLabel(string text, bool visible, Color? color = null)
    {
        if (label == null) return;
        bool on = visible && !string.IsNullOrEmpty(text);
        if (label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
        if (!on) return;
        if (label.text != text) label.text = text;
        label.color = color ?? Color.white;
        Camera cam = Cam;
        if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, Vector3.up);
    }

    // ------------------------------------------------------------------
    // The robot's body
    // ------------------------------------------------------------------

    private GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Material m = clicker.CreateVisualMaterial(color, false);
        Renderer r = g.GetComponent<Renderer>();
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    private Transform Pivot(Transform parent, string name, Vector3 pos)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        return g.transform;
    }

    /// <summary>A little blocky robot about 2.5 units tall, front towards -Z, feet on the origin.</summary>
    private void BuildRobot()
    {
        root = new GameObject("Robot Worker");
        bodyPivot = Pivot(root.transform, "Body", Vector3.zero);
        Transform b = bodyPivot;

        legLeft = Pivot(b, "Leg L", new Vector3(-0.22f, 0.68f, 0f));
        legRight = Pivot(b, "Leg R", new Vector3(0.22f, 0.68f, 0f));
        foreach (Transform leg in new[] { legLeft, legRight })
        {
            Prim(PrimitiveType.Cube, leg, "Leg", new Vector3(0f, -0.28f, 0f), new Vector3(0.26f, 0.55f, 0.26f), bodyColor * 0.8f);
            Prim(PrimitiveType.Cube, leg, "Foot", new Vector3(0f, -0.61f, -0.05f), new Vector3(0.34f, 0.14f, 0.5f), darkColor);
        }
        Prim(PrimitiveType.Cube, b, "Torso", new Vector3(0f, 1.05f, 0f), new Vector3(0.95f, 0.8f, 0.62f), bodyColor);
        Prim(PrimitiveType.Cube, b, "Belt", new Vector3(0f, 0.68f, 0f), new Vector3(1f, 0.12f, 0.66f), accentColor);
        Prim(PrimitiveType.Cube, b, "Panel", new Vector3(0f, 1.1f, -0.32f), new Vector3(0.55f, 0.4f, 0.04f), darkColor);
        chestLight = Prim(PrimitiveType.Sphere, b, "Chest Light", new Vector3(0f, 1.1f, -0.36f), Vector3.one * 0.16f, eyeColor).GetComponent<Renderer>();

        head = Pivot(b, "Head", new Vector3(0f, 1.7f, 0f));
        Prim(PrimitiveType.Cube, head, "Neck", new Vector3(0f, -0.2f, 0f), new Vector3(0.2f, 0.12f, 0.2f), darkColor);
        Prim(PrimitiveType.Cube, head, "Skull", new Vector3(0f, 0.12f, 0f), new Vector3(0.78f, 0.58f, 0.6f), bodyColor * 1.1f);
        Prim(PrimitiveType.Cube, head, "Visor", new Vector3(0f, 0.14f, -0.28f), new Vector3(0.62f, 0.26f, 0.06f), darkColor);
        Prim(PrimitiveType.Cube, head, "Eye L", new Vector3(-0.15f, 0.14f, -0.32f), new Vector3(0.14f, 0.14f, 0.04f), eyeColor);
        Prim(PrimitiveType.Cube, head, "Eye R", new Vector3(0.15f, 0.14f, -0.32f), new Vector3(0.14f, 0.14f, 0.04f), eyeColor);
        Prim(PrimitiveType.Cylinder, head, "Antenna", new Vector3(0f, 0.5f, 0f), new Vector3(0.05f, 0.14f, 0.05f), darkColor);
        antennaLight = Prim(PrimitiveType.Sphere, head, "Antenna Light", new Vector3(0f, 0.7f, 0f), Vector3.one * 0.15f, accentColor).GetComponent<Renderer>();

        leftArm = Pivot(b, "Arm L", new Vector3(-0.62f, 1.38f, 0f));
        rightArm = Pivot(b, "Arm R", new Vector3(0.62f, 1.38f, 0f));
        foreach (Transform arm in new[] { leftArm, rightArm })
        {
            Prim(PrimitiveType.Cube, arm, "Upper", new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 0.6f, 0.2f), bodyColor * 0.85f);
            Prim(PrimitiveType.Cube, arm, "Hand", new Vector3(0f, -0.66f, 0f), new Vector3(0.26f, 0.2f, 0.26f), accentColor);
        }

        box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 1.25f, 0f);
        box.size = new Vector3(1.5f, 2.6f, 1f);
        box.isTrigger = true;

        GameObject lg = new GameObject("Label");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = new Vector3(0f, 2.95f, 0f);
        label = lg.AddComponent<TextMeshPro>();
        label.fontSize = labelSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
#if UNITY_2023_1_OR_NEWER
        label.textWrappingMode = TextWrappingModes.NoWrap;
#else
        label.enableWordWrapping = false;
#endif
        if (clicker.UIFont != null) label.font = clicker.UIFont;
        lg.SetActive(false);

        BuildStation();
        BuildBubble();
    }

    /// <summary>His charging station: a dark pad with a glowing frame and a charger post with a cord (a separate object that stays put).</summary>
    private void BuildStation()
    {
        stationRoot = new GameObject("Robot Charging Station").transform;
        Transform t = stationRoot;
        Prim(PrimitiveType.Cube, t, "Pad", new Vector3(0f, 0.04f, 0f), new Vector3(2.1f, 0.08f, 2.1f), darkColor);
        Color glow = eyeColor * 0.9f;
        Prim(PrimitiveType.Cube, t, "Edge F", new Vector3(0f, 0.09f, -1.0f), new Vector3(2.0f, 0.05f, 0.08f), glow);
        Prim(PrimitiveType.Cube, t, "Edge B", new Vector3(0f, 0.09f, 1.0f), new Vector3(2.0f, 0.05f, 0.08f), glow);
        Prim(PrimitiveType.Cube, t, "Edge L", new Vector3(-1.0f, 0.09f, 0f), new Vector3(0.08f, 0.05f, 2.0f), glow);
        Prim(PrimitiveType.Cube, t, "Edge R", new Vector3(1.0f, 0.09f, 0f), new Vector3(0.08f, 0.05f, 2.0f), glow);
        Prim(PrimitiveType.Cube, t, "Post", new Vector3(0f, 0.8f, 0.85f), new Vector3(0.45f, 1.5f, 0.3f), bodyColor * 0.7f);
        Prim(PrimitiveType.Cube, t, "Cap", new Vector3(0f, 1.6f, 0.85f), new Vector3(0.55f, 0.14f, 0.4f), accentColor);
        stationLight = Prim(PrimitiveType.Sphere, t, "Lamp", new Vector3(0f, 1.25f, 0.68f), Vector3.one * 0.18f, eyeColor).GetComponent<Renderer>();
        Prim(PrimitiveType.Cube, t, "Cable", new Vector3(0f, 0.12f, 0.35f), new Vector3(0.08f, 0.05f, 0.9f), darkColor);
        Prim(PrimitiveType.Cube, t, "Plug", new Vector3(0f, 0.13f, -0.06f), new Vector3(0.22f, 0.1f, 0.2f), accentColor);
    }

    /// <summary>The speech bubble with a robot face that floats over him (a button: click it like him).</summary>
    private void BuildBubble()
    {
        if (hideBubble) return;
        bubbleCanvas = PixelUIKit.CreateCanvas("Robot Bubble", 280, new Vector2(1920f, 1080f), true);
        bubbleCanvasComponent = bubbleCanvas.GetComponent<Canvas>();
        if (bubbleSprite == null) bubbleSprite = BuildBubbleSprite();
        GameObject go = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(bubbleCanvas.transform, false);
        Image img = go.GetComponent<Image>();
        img.sprite = bubbleSprite;
        img.preserveAspect = true;
        go.GetComponent<Button>().targetGraphic = img;
        go.GetComponent<Button>().onClick.AddListener(() => { if (!consumables.IsPlacing && Time.timeScale > 0f && !PixelPauseMenu.IsPaused) OpenWindow(); });
        bubbleRect = go.GetComponent<RectTransform>();
        bubbleRect.anchorMin = bubbleRect.anchorMax = Vector2.zero;
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.sizeDelta = new Vector2(bubbleSize * 48f / 56f, bubbleSize);
    }

    /// <summary>A round speech bubble with a pointer at the bottom and a pixel-art robot head in it (drawn in code).</summary>
    private Sprite BuildBubbleSprite()
    {
        const int w = 48, h = 56;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32 clear = new Color32(0, 0, 0, 0), border = new Color32(25, 28, 40, 255), fill = new Color32(225, 238, 250, 255);
        Color32 head = bodyColor, dark = darkColor, eye = eyeColor, orange = accentColor;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, clear);
        float cx = 23.5f, cy = 33f, r = 22.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r) tex.SetPixel(x, y, d > r - 2.2f ? border : fill);
            }
        // The pointer: a small triangle under the circle, tipped at the bottom row.
        for (int row = 1; row <= 12; row++)
        {
            int half = Mathf.Clamp((row - 1) / 2, 0, 5);
            for (int x = 24 - half - 1; x <= 24 + half; x++)
            {
                bool edge = x == 24 - half - 1 || x == 24 + half || row == 1;
                tex.SetPixel(x, row, edge ? border : fill);
            }
        }
        void Fill(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) tex.SetPixel(x, y, c); }
        Fill(14, 25, 33, 43, head);                         // head
        Fill(14, 25, 33, 25, dark); Fill(14, 43, 33, 43, dark); Fill(14, 25, 14, 43, dark); Fill(33, 25, 33, 43, dark);
        Fill(17, 32, 30, 39, dark);                         // visor
        Fill(19, 34, 22, 37, eye); Fill(25, 34, 28, 37, eye);   // eyes
        Fill(20, 28, 27, 28, dark);                         // mouth
        Fill(23, 44, 24, 48, dark); Fill(22, 49, 25, 51, orange);   // antenna
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    private void UpdateBubble()
    {
        if (bubbleCanvas == null) return;
        Camera cam = Cam;
        bool windowOpen = windowRoot != null && windowRoot.activeSelf;
        if (cam == null || windowOpen) { if (bubbleCanvas.activeSelf) bubbleCanvas.SetActive(false); return; }
        if (!bubbleCanvas.activeSelf) bubbleCanvas.SetActive(true);

        Vector3 world = root.transform.position + Vector3.up * (3.0f * UnitScale * 1.0f);
        Vector3 sp = cam.WorldToScreenPoint(world);
        float scaleFactor = bubbleCanvasComponent.scaleFactor > 0f ? bubbleCanvasComponent.scaleFactor : 1f;
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height) / scaleFactor;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 pos = new Vector2(sp.x, sp.y) / scaleFactor;
        if (sp.z < 0f) pos = new Vector2(canvasSize.x * 0.5f, bar);   // behind the camera: park it
        pos.x = Mathf.Clamp(pos.x, bubbleRect.sizeDelta.x * 0.5f, canvasSize.x - bubbleRect.sizeDelta.x * 0.5f);
        pos.y = Mathf.Clamp(pos.y, bar, canvasSize.y - bar - bubbleRect.sizeDelta.y);
        pos.y += Mathf.Sin(Time.time * 3f) * 5f;
        bubbleRect.anchoredPosition = pos;
        bubbleRect.localScale = Vector3.one * (hovering ? 1.1f : 1f);
    }

    private void Animate()
    {
        if (root == null || bodyPivot == null) return;
        float t = Time.time;
        workT = Mathf.Max(0f, workT - Time.deltaTime);
        bool working = !walking && (workT > 0f || waitT > 0f);
        bool charging = !walking && hasHome && (root.transform.position - homePos).sqrMagnitude < 0.01f * clicker.PixelBaseSize * clicker.PixelBaseSize;

        bodyPivot.localPosition = new Vector3(0f, walking ? Mathf.Abs(Mathf.Sin(t * 10f)) * 0.06f : Mathf.Sin(t * 2f) * 0.03f, 0f);
        head.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 4f + (charging && !hovering ? 14f : 0f),
                                              hovering ? Mathf.Sin(t * 5f) * 25f : (walking || charging) ? 0f : Mathf.Sin(t * 0.7f) * 18f, 0f);

        float legSwing = walking ? Mathf.Sin(t * 10f) * 35f : 0f;
        legLeft.localRotation = Quaternion.Euler(legSwing, 0f, 0f);
        legRight.localRotation = Quaternion.Euler(-legSwing, 0f, 0f);

        float idle = Mathf.Sin(t * 1.6f) * 7f;
        float armSwing = walking ? Mathf.Sin(t * 10f) * 30f : idle;
        leftArm.localRotation = Quaternion.Euler(working ? 80f + Mathf.Sin(t * 12f) * 25f : -armSwing, 0f, 6f);
        rightArm.localRotation = hovering && !working && !walking
            ? Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(t * 8f) * 20f)       // waves hello
            : Quaternion.Euler(working ? 80f + Mathf.Sin(t * 12f + 2f) * 25f : armSwing, 0f, -6f);

        float pulse = 0.5f + 0.5f * Mathf.Sin(t * (working ? 12f : walking ? 6f : 3f));
        SetRendererColor(antennaLight, Color.Lerp(accentColor * 0.5f, Color.white, pulse));
        SetRendererColor(chestLight, Color.Lerp(eyeColor * 0.5f, eyeColor, (working || walking) ? pulse : 1f));
        // The station lamp glows steadily while he charges, and blinks idly when he is away.
        SetRendererColor(stationLight, charging ? Color.Lerp(eyeColor, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 2.5f)) : eyeColor * (0.35f + 0.25f * Mathf.Sin(t * 1.5f)));
    }

    private static void SetRendererColor(Renderer r, Color c)
    {
        if (r == null) return;
        c.a = 1f;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }

    // ------------------------------------------------------------------
    // The job window
    // ------------------------------------------------------------------

    private void OpenWindow()
    {
        PixelWindows.CloseAllExcept(this);
        BuildWindow();
        windowRoot.SetActive(true);
        PixelAudio.Play("ui_click");
    }

    private void CloseWindow()
    {
        if (windowRoot != null && windowRoot.activeSelf)
        {
            windowRoot.SetActive(false);
            ignoreClickFrame = Time.frameCount;
        }
    }

    private void BuildWindow()
    {
        if (windowRoot != null) Destroy(windowRoot);
        TMP_FontAsset font = clicker.UIFont;
        windowRoot = PixelUIKit.CreateCanvas("Robot Worker Window", 520, new Vector2(1920f, 1080f), true);
        PixelUIKit.EnsureEventSystem();

        // Rows: every placeable device the player can use.
        var rows = new System.Collections.Generic.List<int>();
        for (int i = 0; i < consumables.DeviceCount; i++)
        {
            PixelConsumables.Device d = consumables.GetDevice(i);
            if (PixelConsumables.IsPlaceableKind(d.kind) && consumables.OperationListed(d.kind)) rows.Add(i);
        }

        const float width = 760f, rowH = 76f, gap = 12f, pad = 24f;
        float height = pad + 70f + 90f + rows.Count * (rowH + gap) + (jobDevice >= 0 ? rowH + gap : 0f) + (rows.Count == 0 ? 60f : 0f) + pad;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(windowRoot.transform, false);
        panel.GetComponent<Image>().color = new Color(0.09f, 0.1f, 0.14f, 0.97f);
        windowPanel = panel.GetComponent<RectTransform>();
        windowPanel.anchorMin = windowPanel.anchorMax = windowPanel.pivot = new Vector2(0.5f, 0.5f);
        windowPanel.sizeDelta = new Vector2(width, height);

        TMP_Text title = PixelUIKit.CreateText(font, panel.transform, "Title", windowTitle, 44f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-150f, 70f); tr.anchoredPosition = new Vector2(0f, -pad * 0.5f);

        Button close = PixelUIKit.CreateButton(font, panel.transform, "Close", "X", new Vector2(56f, 56f), new Color(0.55f, 0.2f, 0.2f, 1f), Color.white, 30f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -14f);
        close.onClick.AddListener(CloseWindow);

        TMP_Text ask = PixelUIKit.CreateText(font, panel.transform, "Ask", rows.Count == 0 ? noDevicesText : askText, 28f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(1f, 1f, 1f, 0.85f));
        ask.enableAutoSizing = true; ask.fontSizeMax = 28f; ask.fontSizeMin = 18f;
        RectTransform ar = ask.rectTransform;
        ar.anchorMin = new Vector2(0f, 1f); ar.anchorMax = new Vector2(1f, 1f); ar.pivot = new Vector2(0.5f, 1f);
        ar.sizeDelta = new Vector2(-pad * 2f, 84f); ar.anchoredPosition = new Vector2(0f, -(pad * 0.5f + 70f));

        float y = pad + 70f + 90f;
        foreach (int index in rows)
        {
            int captured = index;
            PixelConsumables.Device d = consumables.GetDevice(index);
            bool current = index == jobDevice;
            int owned = consumables.DeviceOwned(index);
            Button b = PixelUIKit.CreateButton(font, panel.transform, "Device " + index,
                d.displayName + "      x" + (owned >= 99999 ? "inf" : owned.ToString()), new Vector2(width - pad * 2f, rowH),
                current ? new Color(0.75f, 0.42f, 0.1f, 1f) : new Color(0.2f, 0.22f, 0.3f, 1f), Color.white, 32f);
            PlaceTop(b.GetComponent<RectTransform>(), y);
            b.onClick.AddListener(() => ChooseDevice(captured));
            y += rowH + gap;
        }
        if (jobDevice >= 0)
        {
            Button clear = PixelUIKit.CreateButton(font, panel.transform, "Clear", clearJobText, new Vector2(width - pad * 2f, rowH),
                                                   new Color(0.45f, 0.18f, 0.18f, 1f), Color.white, 30f);
            PlaceTop(clear.GetComponent<RectTransform>(), y);
            clear.onClick.AddListener(() => { ClearJob(); CloseWindow(); });
        }
    }

    private static void PlaceTop(RectTransform rt, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y);
    }

    private void ChooseDevice(int index)
    {
        CloseWindow();
        if (consumables.BeginRobotPlacement(index, OnSpotChosen))
            PixelHints.Announce("Choose where the Robot Worker should put it: click the floor");
    }

    private void OnSpotChosen(int device, Vector3 point, float yaw, float bend)
    {
        ignoreClickFrame = Time.frameCount;
        SetJob(device, point, yaw, bend);
        PixelHints.Announce(string.Format(jobSetFormat, consumables.GetDevice(device).displayName));
    }

    // ------------------------------------------------------------------
    // Job + saving
    // ------------------------------------------------------------------

    private void SetJob(int device, Vector3 point, float yaw, float bend)
    {
        jobDevice = device; jobPos = point; jobYaw = yaw; jobBend = bend;
        waitT = 0f;
        occupied = false;
        nextCheck = 0f;
        if (ghost != null) Destroy(ghost);
        ghost = device >= 0 ? consumables.CreateGhost(device, point, yaw, bend) : null;
        if (ghost != null) ghost.SetActive(false);   // shown by UpdateJob while the spot is empty
    }

    private void ClearJob() { SetJob(-1, Vector3.zero, 0f, 0f); }

    /// <summary>The job as text for the save file ("" = none).</summary>
    public string Export()
    {
        if (jobDevice < 0 || consumables == null) return "";
        CultureInfo c = CultureInfo.InvariantCulture;
        return consumables.GetDevice(jobDevice).displayName + "|" + jobPos.x.ToString("R", c) + "|" + jobPos.y.ToString("R", c) + "|" +
               jobPos.z.ToString("R", c) + "|" + jobYaw.ToString("R", c) + "|" + jobBend.ToString("R", c);
    }

    /// <summary>Restores a saved job (an empty or unknown text clears it).</summary>
    public void Import(string text)
    {
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        ClearJobQuiet();
        if (string.IsNullOrEmpty(text) || consumables == null) return;
        string[] p = text.Split('|');
        if (p.Length < 6) return;
        CultureInfo c = CultureInfo.InvariantCulture;
        int index = -1;
        for (int i = 0; i < consumables.DeviceCount; i++) if (consumables.GetDevice(i).displayName == p[0]) { index = i; break; }
        if (index < 0 || !PixelConsumables.IsPlaceableKind(consumables.GetDevice(index).kind)) return;
        if (!float.TryParse(p[1], NumberStyles.Float, c, out float x) || !float.TryParse(p[2], NumberStyles.Float, c, out float y) ||
            !float.TryParse(p[3], NumberStyles.Float, c, out float z) || !float.TryParse(p[4], NumberStyles.Float, c, out float yaw) ||
            !float.TryParse(p[5], NumberStyles.Float, c, out float bend)) return;
        SetJob(index, new Vector3(x, y, z), yaw, bend);
    }

    private void ClearJobQuiet()
    {
        jobDevice = -1;
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }
}
