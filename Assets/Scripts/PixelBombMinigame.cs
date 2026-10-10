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
    [Tooltip("How far apart the wires are (fraction of the bomb's height). Click areas never grow bigger than this, so wires can't overlap.")]
    [SerializeField] private float wireGap = 0.22f;

    [Tooltip("Timer text. {0} = seconds left.")]
    [SerializeField] private string timerFormat = "{0:0.0}";

    [Min(0.5f)]
    [Tooltip("Size of the timer text (3D text: about 10 per world unit).")]
    [SerializeField] private float timerTextSize = 3.5f;

    [Min(0.3f)]
    [Tooltip("Size of the colour-blind letters beside the wires.")]
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

    protected override void OnDespawned()
    {
        bombActive = false;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    public override string Id => "bomb";
    public override string DisplayName => "Bomb Defusal";
    public override bool Running => running;

    // --- Tracker: bomb parts (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => bombPartsEarned;
    public override double SpendableCount => bombParts;
    public override string MaterialName => "Bomb Parts";
    public override double MaterialAmount => bombParts;
    public override Color MaterialColor => new Color(0.75f, 0.78f, 0.85f, 1f);
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

        // The bomb is a metal rectangle. Keep all of it inside the view.
        float bw = bombSize * 1.6f, bh = bombSize * 1.25f, bd = bombSize * 0.35f;
        float viewHeight = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cam.aspect;
        float halfW = (bw * 0.6f) / viewWidth, halfDown = (bh * 0.65f) / viewHeight, halfUp = (bh * 0.65f + bombSize * 0.5f) / viewHeight;
        vx = Mathf.Clamp(vx, 0.04f + halfW, 0.96f - halfW);
        vy = Mathf.Clamp(vy, 0.12f + halfDown, 0.88f - halfUp);
        Vector3 position = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));

        GameObject root = new GameObject("Bomb");
        Track(root);
        root.transform.position = position;
        root.transform.rotation = cam.transform.rotation; // faces the camera

        // Casing, a darker panel on the front, metal end blocks and an indicator light.
        Material bodyMat = MakeMaterial(casingColor, casingGlow);
        Material panelMat = MakeMaterial(new Color(casingColor.r * 0.45f, casingColor.g * 0.45f, casingColor.b * 0.5f, 1f), 0f);
        Material blockMat = MakeMaterial(new Color(casingColor.r * 0.7f, casingColor.g * 0.7f, casingColor.b * 0.75f, 1f), casingGlow * 0.5f);
        if (bodyMat != null)
        {
            if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 0.6f);
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.5f);
            if (bodyMat.HasProperty("_Glossiness")) bodyMat.SetFloat("_Glossiness", 0.5f);
        }
        AddBox("Casing", root.transform, Vector3.zero, new Vector3(bw, bh, bd), bodyMat);
        AddBox("Front Panel", root.transform, new Vector3(0f, 0f, -(bd * 0.5f + 0.01f)), new Vector3(bw * 0.9f, bh * 0.82f, 0.04f), panelMat);
        AddBox("Left Block", root.transform, new Vector3(-(bw * 0.5f + bw * 0.03f), 0f, 0f), new Vector3(bw * 0.07f, bh * 0.7f, bd * 1.15f), blockMat);
        AddBox("Right Block", root.transform, new Vector3(bw * 0.5f + bw * 0.03f, 0f, 0f), new Vector3(bw * 0.07f, bh * 0.7f, bd * 1.15f), blockMat);
        GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "Indicator Light";
        Destroy(spark.GetComponent<Collider>());
        spark.transform.SetParent(root.transform, false);
        spark.transform.localPosition = new Vector3(bw * 0.4f, bh * 0.5f + bombSize * 0.06f, 0f);
        float sparkBase = bombSize * 0.09f;
        spark.transform.localScale = Vector3.one * sparkBase;
        Material sparkMat = MakeMaterial(new Color(1f, 0.35f, 0.2f, 1f), 2.5f);
        if (sparkMat != null) spark.GetComponent<Renderer>().sharedMaterial = sparkMat;

        // The timer sits low on the front, under the wires.
        float frontZ = -(bd * 0.5f + 0.06f);
        TextMeshPro timer = MakeText(root.transform, "Timer", string.Format(timerFormat, timeLimit), timerTextSize, timerColor,
                                     new Vector3(0f, -bh * 0.37f, frontZ), bw * 0.9f);

        // Three wires run straight across the top of the front panel: red, green, blue. Each is two pieces that meet in the middle.
        Color[] colors = { redWireColor, greenWireColor, blueWireColor };
        float wireSpan = bw * 0.84f;
        Collider[][] hitboxes = new Collider[3][];
        Transform[] leftPivots = new Transform[3], rightPivots = new Transform[3];
        bool[] cut = new bool[3];
        wireHitMax = bh * wireGap * 0.8f; // click boxes stay clearly apart
        for (int i = 0; i < 3; i++)
        {
            float y = bh * 0.30f - i * bh * wireGap; // red on top, blue lowest, all above the timer
            Material wm = MakeMaterial(colors[i], 0.45f);
            BuildWire("Wire " + i, root.transform, y, frontZ - wireThickness * 0.5f, wireSpan, wm, out leftPivots[i], out rightPivots[i], out hitboxes[i]);

            // Colour-blind support (Settings): a letter at the end of each wire.
            if (PixelDisplaySettings.ColorBlind)
            {
                string[] letters = { "R", "G", "B" };
                MakeText(root.transform, "Wire Letter " + letters[i], letters[i], hintTextSize * 1.3f, Color.white,
                         new Vector3(-(wireSpan * 0.5f + bw * 0.045f), y, frontZ), bombSize);
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
            spark.transform.localScale = Vector3.one * sparkBase * (1f + 0.35f * Mathf.Sin(Time.time * (left <= warningSeconds ? 26f : 10f)));
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
        // Cutting a wire snaps just that wire in two: the pieces swing down and hang from the ends.
        if (chosen >= 0) StartCoroutine(CutWire(leftPivots[chosen], rightPivots[chosen], hitboxes[chosen]));

        if (defused)
        {
            int parts = Random.Range(Mathf.Min(partsMin, partsMax), Mathf.Max(partsMin, partsMax) + 1);
            bombParts += parts;
            bombPartsEarned += parts;
            PixelStats.Count("bomb.defused");
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
            PixelStats.Count("bomb.exploded");
            Explode(position, cam);
        }

        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        bombActive = false;
    }

    private static GameObject AddBox(string objectName, Transform parent, Vector3 localPosition, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;
        if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    /// <summary>
    /// A wire straight across the front at height 'y', as two pieces that meet in the middle. Each piece hangs from a pivot at
    /// its far end, so when it is cut the pieces swing down. One wide invisible box along the wire makes it easy to click.
    /// </summary>
    private float wireHitMax = 1f;

    private void BuildWire(string objectName, Transform parent, float y, float z, float span, Material material,
                           out Transform leftPivot, out Transform rightPivot, out Collider[] colliders)
    {
        float half = span * 0.5f;

        leftPivot = new GameObject(objectName + " Left Pivot").transform;
        leftPivot.SetParent(parent, false);
        leftPivot.localPosition = new Vector3(-half, y, z);
        AddBox("Piece", leftPivot, new Vector3(half * 0.5f, 0f, 0f), new Vector3(half, wireThickness, wireThickness), material); // runs from the pivot to the middle

        rightPivot = new GameObject(objectName + " Right Pivot").transform;
        rightPivot.SetParent(parent, false);
        rightPivot.localPosition = new Vector3(half, y, z);
        AddBox("Piece", rightPivot, new Vector3(-half * 0.5f, 0f, 0f), new Vector3(half, wireThickness, wireThickness), material);

        // Small bolts where the wire is fixed at each end.
        AddBox("Bolt", parent, new Vector3(-half, y, z), new Vector3(wireThickness * 1.6f, wireThickness * 1.6f, wireThickness * 1.2f), material);
        AddBox("Bolt", parent, new Vector3(half, y, z), new Vector3(wireThickness * 1.6f, wireThickness * 1.6f, wireThickness * 1.2f), material);

        GameObject hit = new GameObject(objectName + " Hitbox");
        hit.transform.SetParent(parent, false);
        hit.transform.localPosition = new Vector3(0f, y, z);
        BoxCollider box = hit.AddComponent<BoxCollider>();
        box.isTrigger = true;
        float hitSize = Mathf.Min(wireThickness * wireClickMultiplier, wireHitMax);
        box.size = new Vector3(span, hitSize, wireThickness * wireClickMultiplier);
        colliders = new Collider[] { box };
    }

    /// <summary>The cut wire's two pieces swing down from their ends and hang there (a little spark at the cut).</summary>
    private IEnumerator CutWire(Transform leftPivot, Transform rightPivot, Collider[] hitbox)
    {
        if (leftPivot == null || rightPivot == null) yield break;
        foreach (Collider c in hitbox) if (c != null) c.enabled = false;

        const float swing = 0.35f;
        float t = 0f;
        while (t < swing && leftPivot != null && rightPivot != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / swing);
            float eased = 1f - (1f - k) * (1f - k);
            // Negative z turns a piece pointing to +x downwards; positive does the same for the piece pointing to -x.
            leftPivot.localRotation = Quaternion.Euler(0f, 0f, -80f * eased + Mathf.Sin(k * 18f) * 4f * (1f - k));
            rightPivot.localRotation = Quaternion.Euler(0f, 0f, 80f * eased - Mathf.Sin(k * 18f) * 4f * (1f - k));
            yield return null;
        }
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
        Track(lightGo);
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
        Track(go);
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
