using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static PixelInput;

/// <summary>
/// Bomb Defusal minigame for Pixel Clicker.
///
/// Every so often a bomb appears on screen with three wires: red, green and blue. A timer counts down. Cut the wire
/// of the RGB pixel type you currently hold the MOST of (click it) and the bomb is defused: you collect bomb parts,
/// which add up as a tracker in the shop's Minigames tab (to be used elsewhere later). Cut the wrong wire, or run out
/// of time, and the bomb explodes and destroys every old pixel.
///
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelBombMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (for the camera, the cube's position and the pixel counts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Bomb Defusal is bought.)")]
    [SerializeField] private bool running = false;

    [Header("Bomb Parts (tracker)")]
    [Tooltip("Runtime: bomb parts you have right now (saved with the game). Spending them in the shop lowers this; you can type a number to test.")]
    [SerializeField] private double bombParts = 0;

    [Tooltip("Runtime: bomb parts ever earned (saved with the game). This is what the tracker bar in the shop shows, so spending parts doesn't lower it.")]
    [SerializeField] private double bombPartsEarned = 0;

    [Min(1)]
    [Tooltip("The goal shown on the tracker's progress bar (the count keeps going past it).")]
    [SerializeField] private double partsGoal = 50;

    [Min(1)]
    [Tooltip("Fewest bomb parts a defused bomb gives.")]
    [SerializeField] private int partsMin = 2;

    [Min(1)]
    [Tooltip("Most bomb parts a defused bomb gives.")]
    [SerializeField] private int partsMax = 4;

    [Tooltip("Title of the tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Bomb Parts";

    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Defuse bombs to collect parts. They will be used for something later.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = parts collected, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} bomb parts";

    [Header("When it appears")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before the first bomb.")]
    [SerializeField] private float firstBombDelay = 20f;

    [Min(1f)]
    [Tooltip("Shortest wait between bombs (seconds).")]
    [SerializeField] private float minInterval = 120f;

    [Min(1f)]
    [Tooltip("Longest wait between bombs (seconds). Each wait is random between shortest and longest.")]
    [SerializeField] private float maxInterval = 240f;

    [Header("The Bomb")]
    [Min(1f)]
    [Tooltip("Seconds you have to cut the right wire.")]
    [SerializeField] private float timeLimit = 12f;

    [Min(0.3f)]
    [Tooltip("Size of the bomb (world units).")]
    [SerializeField] private float bombSize = 1.8f;

    [Range(0.05f, 1.5f)]
    [Tooltip("How far in front of the cube the bomb is, as a fraction of the camera's distance to the cube.")]
    [SerializeField] private float depthFraction = 0.8f;

    [Tooltip("Where the bomb can appear (screen fractions): from-to horizontally on each side of the cube.")]
    [SerializeField] private Vector2 leftSideX = new Vector2(0.12f, 0.3f);

    [Tooltip("Same, for the right side.")]
    [SerializeField] private Vector2 rightSideX = new Vector2(0.7f, 0.88f);

    [Tooltip("Height range of the bomb (screen fraction).")]
    [SerializeField] private Vector2 heightRange = new Vector2(0.35f, 0.7f);

    [Min(0f)]
    [Tooltip("How much the bomb sways (degrees).")]
    [SerializeField] private float swayDegrees = 4f;

    [Tooltip("Colour of the bomb's casing.")]
    [SerializeField] private Color casingColor = new Color(0.32f, 0.33f, 0.4f, 1f);

    [Min(0f)]
    [Tooltip("How much the casing glows, so it stands out against a dark background.")]
    [SerializeField] private float casingGlow = 0.25f;

    [Tooltip("Colour of the red wire.")]
    [SerializeField] private Color redWireColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("Colour of the green wire.")]
    [SerializeField] private Color greenWireColor = new Color(0.2f, 0.9f, 0.3f, 1f);

    [Tooltip("Colour of the blue wire.")]
    [SerializeField] private Color blueWireColor = new Color(0.25f, 0.45f, 1f, 1f);

    [Min(0.02f)]
    [Tooltip("How thick the wires look (world units).")]
    [SerializeField] private float wireThickness = 0.12f;

    [Min(1f)]
    [Tooltip("The clickable area of a wire is its thickness times this (so thin wires are easy to hit).")]
    [SerializeField] private float wireClickMultiplier = 3.5f;

    [Min(0.1f)]
    [Tooltip("How far apart the wires lie on the bomb (fraction of the bomb's radius).")]
    [SerializeField] private float wireSpacing = 0.3f;

    [Tooltip("Small hint shown above the bomb. Leave empty to hide it.")]
    [SerializeField] private string hintText = "Cut the colour you have the most of";

    [Tooltip("Timer text. {0} = seconds left.")]
    [SerializeField] private string timerFormat = "{0:0.0}";

    [Min(0.5f)]
    [Tooltip("Size of the timer text (3D text: about 10 per world unit).")]
    [SerializeField] private float timerTextSize = 3.5f;

    [Min(0.3f)]
    [Tooltip("Size of the hint text above the bomb.")]
    [SerializeField] private float hintTextSize = 1.6f;

    [Min(0f)]
    [Tooltip("The timer turns red and flashes when this many seconds are left.")]
    [SerializeField] private float warningSeconds = 4f;

    [Tooltip("Normal timer colour.")]
    [SerializeField] private Color timerColor = new Color(0.4f, 1f, 0.5f, 1f);

    [Tooltip("Timer colour when time is nearly up.")]
    [SerializeField] private Color timerWarningColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Defused / Exploded")]
    [Tooltip("Text that rises when you defuse the bomb. {0} = bomb parts gained.")]
    [SerializeField] private string defusedFormat = "Defused!  +{0} bomb parts";

    [Tooltip("Colour of that text.")]
    [SerializeField] private Color defusedColor = new Color(0.5f, 1f, 0.6f, 1f);

    [Tooltip("Text shown when the bomb goes off.")]
    [SerializeField] private string explodedText = "BOOM!  Old pixels destroyed";

    [Tooltip("Colour of that text.")]
    [SerializeField] private Color explodedColor = new Color(1f, 0.45f, 0.25f, 1f);

    [Tooltip("Size of the rising text (3D text: about 10 per world unit).")]
    [SerializeField] private float messageFontSize = 6f;

    [Min(0.1f)]
    [Tooltip("How long the rising text lasts (seconds).")]
    [SerializeField] private float messageSeconds = 1.6f;

    [Min(0.1f)]
    [Tooltip("How long the bomb stays after being defused (seconds).")]
    [SerializeField] private float defusedLingerSeconds = 0.8f;

    [Min(0.5f)]
    [Tooltip("How big the explosion flash gets (world units across).")]
    [SerializeField] private float explosionSize = 9f;

    [Min(0.1f)]
    [Tooltip("How long the explosion flash lasts (seconds).")]
    [SerializeField] private float explosionSeconds = 0.5f;

    [Tooltip("Colour of the explosion flash.")]
    [SerializeField] private Color explosionColor = new Color(1f, 0.55f, 0.15f, 1f);

    [Header("Events")]
    [Tooltip("Fired when a bomb appears.")]
    public UnityEvent onBombAppeared;

    [Tooltip("Fired when a bomb is defused.")]
    public UnityEvent onDefused;

    [Tooltip("Fired when a bomb explodes (wrong wire or time ran out).")]
    public UnityEvent onExploded;

    private float spawnTimer;
    private bool bombActive;

    public override bool Busy => bombActive;

    public override string Id => "bomb";
    public override string DisplayName => "Bomb Defusal";
    public override bool Running => running;

    // --- Tracker: bomb parts (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => bombPartsEarned;
    public override double SpendableCount => bombParts;
    public override double ExtraValue => bombParts;
    public override void SetExtraValue(double value)
    {
        bombParts = System.Math.Max(0d, value);
        bombPartsEarned = System.Math.Max(bombPartsEarned, bombParts);
    }
    public override bool TrySpendTracker(double amount) => TrySpendParts(amount);
    public override double TrackerGoal => partsGoal;
    public override string RequirementFormat => requirementFormat;
    public override void SetTrackerCount(double value) => bombPartsEarned = System.Math.Max(System.Math.Max(0d, value), bombParts);

    /// <summary>Bomb parts you have right now. Other systems spend them with <see cref="TrySpendParts"/>.</summary>
    public double BombParts => bombParts;

    /// <summary>Spends bomb parts. Returns false (and spends nothing) if you don't have enough.</summary>
    public bool TrySpendParts(double amount)
    {
        if (amount < 0d || bombParts < amount) return false;
        bombParts -= amount;
        return true;
    }

    protected override void Awake()
    {
        base.Awake();
        clicker = clicker != null ? clicker : PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelBombMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (startRunning) running = true;
        spawnTimer = firstBombDelay;
    }

    private void Update()
    {
        if (!running || bombActive) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(BombRoutine());
            else spawnTimer = PixelMinigameLimits.RetrySeconds; // too many minigames running right now
        }
    }

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstBombDelay;
    }

    public override void Deactivate() => running = false;

    /// <summary>Sends a bomb right now (right-click the component &gt; Spawn Bomb Now).</summary>
    [ContextMenu("Spawn Bomb Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !bombActive) StartCoroutine(BombRoutine());
    }

    // ------------------------------------------------------------------
    // The rule: which wire is right?
    // ------------------------------------------------------------------

    /// <summary>0 = red, 1 = green, 2 = blue: whichever of the three you hold the most of right now (a tie goes to the first).</summary>
    private int CorrectWire()
    {
        double r = clicker.GetCount(PixelClicker.PixelType.Red);
        double g = clicker.GetCount(PixelClicker.PixelType.Green);
        double b = clicker.GetCount(PixelClicker.PixelType.Blue);
        if (g > r && g >= b) return 1;
        if (b > r && b > g) return 2;
        return 0;
    }

    // ------------------------------------------------------------------
    // The bomb
    // ------------------------------------------------------------------

    private Material MakeMaterial(Color color, float emission)
    {
        Material m = clicker.CreateVisualMaterial(color, false);
        if (m != null && emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * emission);
        }
        return m;
    }

    private TextMeshPro MakeText(Transform parent, string name, string text, float size, Color color, Vector3 localPos, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        if (clicker.UIFont != null) t.font = clicker.UIFont;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = new Vector2(width, bombSize * 0.6f); // wide, so the text never wraps onto a second line
        return t;
    }

    private IEnumerator BombRoutine()
    {
        bombActive = true;
        onBombAppeared?.Invoke();
        Report(MinigameEvent.Spawned);

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) { bombActive = false; yield break; }

        // Place the bomb beside the cube.
        float depth = 10f;
        if (clicker.PixelTransform != null)
            depth = Mathf.Max(cam.nearClipPlane + 0.5f,
                              Vector3.Dot(clicker.PixelTransform.position - cam.transform.position, cam.transform.forward) * depthFraction);
        Vector2 xr = Random.value < 0.5f ? leftSideX : rightSideX;
        float vx = Random.Range(Mathf.Min(xr.x, xr.y), Mathf.Max(xr.x, xr.y));
        float vy = Random.Range(Mathf.Min(heightRange.x, heightRange.y), Mathf.Max(heightRange.x, heightRange.y));

        // Keep the whole bomb (and the hint above it) inside the view.
        float R = bombSize * 0.6f; // radius of the round bomb
        float viewHeight = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cam.aspect;
        float halfW = (R * 1.25f) / viewWidth, halfDown = (R * 1.3f) / viewHeight, halfUp = (R * 1.3f + bombSize * 0.5f) / viewHeight;
        vx = Mathf.Clamp(vx, 0.04f + halfW, 0.96f - halfW);
        vy = Mathf.Clamp(vy, 0.12f + halfDown, 0.88f - halfUp);
        Vector3 position = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));

        GameObject root = new GameObject("Bomb");
        root.transform.position = position;
        root.transform.rotation = cam.transform.rotation; // faces the camera

        // The casing: two halves of a round metal bomb that meet at a vertical seam. The wires hold them together.
        Material bodyMat = MakeMaterial(casingColor, casingGlow);
        if (bodyMat != null)
        {
            if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 0.75f);
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.6f);
            if (bodyMat.HasProperty("_Glossiness")) bodyMat.SetFloat("_Glossiness", 0.6f);
        }
        GameObject leftHalf = MakeHalf("Left Half", root.transform, R, false, bodyMat);
        GameObject rightHalf = MakeHalf("Right Half", root.transform, R, true, bodyMat);

        // A metal cap and a fuse on top, with a glowing spark at its tip (they belong to the left half).
        Material capMat = MakeMaterial(new Color(casingColor.r * 0.55f, casingColor.g * 0.55f, casingColor.b * 0.55f, 1f), 0f);
        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Fuse Cap";
        Destroy(cap.GetComponent<Collider>());
        cap.transform.SetParent(leftHalf.transform, false);
        cap.transform.localPosition = new Vector3(-R * 0.18f, R * 0.98f, 0f);
        cap.transform.localScale = new Vector3(R * 0.34f, R * 0.12f, R * 0.34f);
        if (capMat != null) cap.GetComponent<Renderer>().sharedMaterial = capMat;
        GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuse.name = "Fuse";
        Destroy(fuse.GetComponent<Collider>());
        fuse.transform.SetParent(leftHalf.transform, false);
        fuse.transform.localPosition = new Vector3(-R * 0.3f, R * 1.22f, 0f);
        fuse.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        fuse.transform.localScale = new Vector3(R * 0.07f, R * 0.2f, R * 0.07f);
        Material fuseMat = MakeMaterial(new Color(0.25f, 0.2f, 0.15f, 1f), 0f);
        if (fuseMat != null) fuse.GetComponent<Renderer>().sharedMaterial = fuseMat;
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "Fuse Spark";
        Destroy(spark.GetComponent<Collider>());
        spark.transform.SetParent(leftHalf.transform, false);
        spark.transform.localPosition = new Vector3(-R * 0.4f, R * 1.4f, 0f);
        spark.transform.localScale = Vector3.one * R * 0.16f;
        Material sparkMat = MakeMaterial(new Color(1f, 0.7f, 0.2f, 1f), 2.5f);
        if (sparkMat != null) spark.GetComponent<Renderer>().sharedMaterial = sparkMat;

        // Timer on the front, near the top.
        float timerY = R * 0.66f;
        TextMeshPro timer = MakeText(root.transform, "Timer", string.Format(timerFormat, timeLimit), timerTextSize, timerColor,
                                     new Vector3(0f, timerY, -(Mathf.Sqrt(Mathf.Max(0.01f, R * R - timerY * timerY)) + 0.04f)), R * 1.8f);
        TextMeshPro hint = null;
        Vector3 hintLocal = new Vector3(0f, R * 1.55f + bombSize * 0.15f, -R * 0.3f);
        if (!string.IsNullOrEmpty(hintText))
            hint = MakeText(root.transform, "Hint", hintText, hintTextSize, new Color(1f, 1f, 1f, 0.9f), hintLocal, bombSize * 8f);

        // Three wires run across the front, over the seam, from one half to the other: red, green, blue.
        Color[] colors = { redWireColor, greenWireColor, blueWireColor };
        float spacing = R * wireSpacing;
        float centreY = -R * 0.05f;
        Collider[][] hitboxes = new Collider[3][];
        bool[] cut = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            float y = centreY + (1 - i) * spacing; // red on top, blue at the bottom
            Material wm = MakeMaterial(colors[i], 0.45f);
            BuildWire("Wire " + i, y, R, wm, leftHalf.transform, rightHalf.transform, root.transform, out hitboxes[i]);

            // Colour-blind support (Settings): a letter beside each wire.
            if (PixelDisplaySettings.ColorBlind)
            {
                string[] letters = { "R", "G", "B" };
                float ring = Mathf.Sqrt(Mathf.Max(0.01f, R * R - y * y));
                MakeText(root.transform, "Wire Letter " + letters[i], letters[i], hintTextSize * 1.4f, Color.white,
                         new Vector3(ring * 0.78f, y, -ring * 0.62f), bombSize);
            }
        }

        // Count down.
        float left = timeLimit;
        int lastWhole = Mathf.CeilToInt(left);
        int chosen = -1;
        Quaternion baseRot = root.transform.rotation;
        float swayPhase = Random.value * 10f;

        while (left > 0f && chosen < 0)
        {
            left -= Time.deltaTime;

            // Sway and the timer.
            float sway = Mathf.Sin(Time.time * 1.7f + swayPhase) * swayDegrees;
            root.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, sway);
            bool warning = left <= warningSeconds;
            timer.text = string.Format(timerFormat, Mathf.Max(0f, left));
            timer.color = warning && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f ? timerWarningColor
                        : warning ? Color.Lerp(timerColor, timerWarningColor, 0.5f) : timerColor;
            spark.transform.localScale = Vector3.one * R * (0.16f + 0.05f * Mathf.Sin(Time.time * 22f));
            if (hint != null) hint.transform.position = PixelUIKit.KeepOnScreen(cam, hint, root.transform.TransformPoint(hintLocal)); // never off screen
            int whole = Mathf.CeilToInt(Mathf.Max(0f, left));
            if (whole != lastWhole)
            {
                lastWhole = whole;
                PixelAudio.Play("bomb_tick", warning ? 1.25f : 1f);
            }

            // Click a wire.
            if (Time.timeScale > 0f && LeftPressed() && !PointerOverUI())
            {
                Ray ray = cam.ScreenPointToRay(PointerPosition());
                float best = float.MaxValue;
                for (int i = 0; i < 3; i++)
                    foreach (Collider hb in hitboxes[i])
                        if (!cut[i] && hb.Raycast(ray, out RaycastHit info, 1000f) && info.distance < best)
                        {
                            best = info.distance;
                            chosen = i;
                        }
            }
            yield return null;
        }

        // Result.
        bool defused = chosen >= 0 && chosen == CorrectWire();
        // Cutting a wire lets go of the two halves: they split apart (violently if it was the wrong one).
        if (chosen >= 0)
        {
            leftHalf.transform.SetParent(null, true);
            rightHalf.transform.SetParent(null, true);
            StartCoroutine(SplitApart(leftHalf.transform, rightHalf.transform, cam, !(chosen == CorrectWire())));
        }

        if (defused)
        {
            int parts = Random.Range(Mathf.Min(partsMin, partsMax), Mathf.Max(partsMin, partsMax) + 1);
            bombParts += parts;
            bombPartsEarned += parts;
            Report(MinigameEvent.Clicked);
            onDefused?.Invoke();
            timer.text = "OK";
            timer.color = defusedColor;
            StartCoroutine(RisingMessage(position, cam, string.Format(defusedFormat, parts), defusedColor));
            yield return new WaitForSeconds(defusedLingerSeconds);
            Destroy(root);
        }
        else
        {
            Destroy(root);
            Explode(position, cam);
        }

        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        bombActive = false;
    }

    /// <summary>The two halves of the bomb swing apart and fall away.</summary>
    private IEnumerator SplitApart(Transform left, Transform right, Camera cam, bool violent)
    {
        float t = 0f;
        float duration = violent ? 0.7f : 1.1f;
        float speed = violent ? 7f : 2.6f;
        Vector3 leftDir = (-cam.transform.right + cam.transform.up * (violent ? 0.7f : 0.25f)).normalized;
        Vector3 rightDir = (cam.transform.right + cam.transform.up * (violent ? 0.7f : 0.25f)).normalized;
        Vector3 spinAxis = cam.transform.forward;
        Vector3 leftScale = left != null ? left.localScale : Vector3.one, rightScale = right != null ? right.localScale : Vector3.one;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float fall = t * t * 5f;
            if (left != null)
            {
                left.position += (leftDir * speed - Vector3.up * fall) * Time.deltaTime;
                left.Rotate(spinAxis, (violent ? 260f : 70f) * Time.deltaTime, Space.World);
                if (k > 0.7f) left.localScale = leftScale * (1f - (k - 0.7f) / 0.3f);
            }
            if (right != null)
            {
                right.position += (rightDir * speed - Vector3.up * fall) * Time.deltaTime;
                right.Rotate(spinAxis, -(violent ? 260f : 70f) * Time.deltaTime, Space.World);
                if (k > 0.7f) right.localScale = rightScale * (1f - (k - 0.7f) / 0.3f);
            }
            yield return null;
        }
        if (left != null) Destroy(left.gameObject);
        if (right != null) Destroy(right.gameObject);
    }

    /// <summary>One half of the round casing, with a flat face at the seam. 'right' = the half on the +x side.</summary>
    private GameObject MakeHalf(string objectName, Transform parent, float radius, bool right, Material material)
    {
        GameObject go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = HalfSphereMesh(radius, right);
        if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    /// <summary>A half sphere (x >= 0 for the right half, x <= 0 for the left) closed with a flat disc at the seam.</summary>
    private static Mesh HalfSphereMesh(float radius, bool right)
    {
        const int lat = 16, lon = 16, ring = 32;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();

        // The curved surface.
        for (int i = 0; i <= lat; i++)
        {
            float theta = Mathf.PI * i / lat;           // 0 = top, PI = bottom
            for (int j = 0; j <= lon; j++)
            {
                float psi = Mathf.PI * j / lon;          // 0..PI around the vertical axis
                float side = right ? 1f : -1f;
                // x = side * r * sin(psi), z = -r * cos(psi): the front of the bomb faces -z (toward the camera).
                Vector3 p = new Vector3(side * radius * Mathf.Sin(theta) * Mathf.Sin(psi), radius * Mathf.Cos(theta),
                                        -radius * Mathf.Sin(theta) * Mathf.Cos(psi));
                vertices.Add(p);
                normals.Add(p.normalized);
            }
        }
        int curvedCount = vertices.Count;
        for (int i = 0; i < lat; i++)
        {
            for (int j = 0; j < lon; j++)
            {
                int a = i * (lon + 1) + j, b = a + 1, c = a + lon + 1, d = c + 1;
                AddOutward(vertices, normals, triangles, a, c, b);
                AddOutward(vertices, normals, triangles, b, c, d);
            }
        }

        // The flat face at the seam (faces the other half).
        Vector3 faceNormal = right ? Vector3.left : Vector3.right;
        int centre = vertices.Count;
        vertices.Add(Vector3.zero); normals.Add(faceNormal);
        for (int k = 0; k < ring; k++)
        {
            float a = Mathf.PI * 2f * k / ring;
            vertices.Add(new Vector3(0f, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            normals.Add(faceNormal);
        }
        for (int k = 0; k < ring; k++)
        {
            int p1 = centre + 1 + k, p2 = centre + 1 + (k + 1) % ring;
            // Wind it so it faces along faceNormal.
            Vector3 n = Vector3.Cross(vertices[p1] - vertices[centre], vertices[p2] - vertices[centre]);
            if (Vector3.Dot(n, faceNormal) >= 0f) { triangles.Add(centre); triangles.Add(p1); triangles.Add(p2); }
            else { triangles.Add(centre); triangles.Add(p2); triangles.Add(p1); }
        }

        Mesh mesh = new Mesh { name = right ? "BombRight" : "BombLeft" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Adds a triangle wound so that it faces the same way as its vertex normals (so no face is ever inside out).</summary>
    private static void AddOutward(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n,
                                   System.Collections.Generic.List<int> tris, int a, int b, int c)
    {
        Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (face.sqrMagnitude < 1e-10f) return; // a degenerate triangle at the poles
        Vector3 avg = n[a] + n[b] + n[c];
        if (Vector3.Dot(face, avg) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
        else { tris.Add(a); tris.Add(c); tris.Add(b); }
    }

    /// <summary>
    /// A wire lying on the front of the bomb at height 'y', crossing the seam: its left piece belongs to the left half and its
    /// right piece to the right half (so a cut shows both pieces going away with their halves). Also makes the click colliders.
    /// </summary>
    private void BuildWire(string objectName, float y, float radius, Material material, Transform leftParent, Transform rightParent,
                           Transform hitParent, out Collider[] colliders)
    {
        float ring = Mathf.Sqrt(Mathf.Max(0.01f, radius * radius - y * y)) + wireThickness * 0.35f; // just above the surface
        const int steps = 10;
        const float reach = 1.05f; // radians either side of the front

        Vector3[] leftPoints = new Vector3[steps + 1], rightPoints = new Vector3[steps + 1];
        for (int k = 0; k <= steps; k++)
        {
            float t = reach * k / steps;
            rightPoints[k] = new Vector3(ring * Mathf.Sin(t), y, -ring * Mathf.Cos(t));   // from the seam round to the right
            leftPoints[k] = new Vector3(-ring * Mathf.Sin(t), y, -ring * Mathf.Cos(t));   // from the seam round to the left
        }

        foreach (var (points, parent) in new[] { (leftPoints, leftParent), (rightPoints, rightParent) })
        {
            GameObject piece = new GameObject(objectName + " Piece", typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(parent, false);
            piece.GetComponent<MeshFilter>().sharedMesh = PixelTube.Build(points, wireThickness * 0.5f, 8, null);
            if (material != null) piece.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // Click colliders: a row of fat invisible spheres along the wire (they sway with the bomb, not with the halves).
        GameObject hit = new GameObject(objectName + " Hitbox");
        hit.transform.SetParent(hitParent, false);
        float hitRadius = wireThickness * wireClickMultiplier * 0.5f;
        var list = new System.Collections.Generic.List<Collider>();
        for (int k = -steps; k <= steps; k += 2)
        {
            Vector3 p = k >= 0 ? rightPoints[k] : leftPoints[-k];
            SphereCollider sc = hit.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.center = p;
            sc.radius = hitRadius;
            list.Add(sc);
        }
        colliders = list.ToArray();
    }

    private void Explode(Vector3 position, Camera cam)
    {
        PixelAudio.Play("bomb_explode");
        clicker.ClearOldPixels(); // every old pixel is destroyed
        onExploded?.Invoke();
        StartCoroutine(ExplosionRoutine(position));
        StartCoroutine(RisingMessage(position, cam, explodedText, explodedColor));
    }

    private IEnumerator ExplosionRoutine(Vector3 position)
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Explosion";
        Destroy(flash.GetComponent<Collider>());
        flash.transform.position = position;
        Material m = clicker.CreateVisualMaterial(explosionColor, true);
        Renderer r = flash.GetComponent<Renderer>();
        if (m != null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", explosionColor * 2f);
            r.sharedMaterial = m;
        }

        GameObject lightGo = new GameObject("Explosion Light");
        lightGo.transform.position = position;
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = explosionColor;
        light.range = explosionSize * 1.5f;
        light.shadows = LightShadows.None;

        float t = 0f;
        while (t < explosionSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / explosionSeconds);
            float eased = 1f - (1f - k) * (1f - k);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, explosionSize, eased);
            light.intensity = Mathf.Lerp(8f, 0f, k);
            if (m != null)
            {
                Color c = explosionColor;
                c.a = Mathf.Lerp(0.8f, 0f, k);
                m.color = c;
            }
            yield return null;
        }
        Destroy(flash);
        Destroy(lightGo);
    }

    /// <summary>Text that rises from the bomb and fades.</summary>
    private IEnumerator RisingMessage(Vector3 start, Camera cam, string message, Color color)
    {
        GameObject go = new GameObject("Bomb Message");
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = messageFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(24f, 3f);

        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / messageSeconds);
            go.transform.rotation = cam.transform.rotation;
            go.transform.position = PixelUIKit.KeepOnScreen(cam, text, start + cam.transform.up * (bombSize * 0.8f + k * 1.5f) - cam.transform.forward * 0.5f);
            Color c = color;
            c.a *= 1f - k * k;
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
