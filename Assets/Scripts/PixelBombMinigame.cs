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
    [Tooltip("Runtime: bomb parts collected (saved with the game). Other systems will spend these later; you can type a number to test.")]
    [SerializeField] private double bombParts = 0;

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
    [SerializeField] private Color bodyColor = new Color(0.14f, 0.14f, 0.17f, 1f);

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
    [Tooltip("How far apart the wires hang, as a fraction of the bomb's width.")]
    [SerializeField] private float wireSpacing = 0.3f;

    [Tooltip("Small hint shown above the bomb. Leave empty to hide it.")]
    [SerializeField] private string hintText = "Cut the colour you have the most of";

    [Tooltip("Timer text. {0} = seconds left.")]
    [SerializeField] private string timerFormat = "{0:0.0}";

    [Min(0.5f)]
    [Tooltip("Size of the timer text (3D text: about 10 per world unit).")]
    [SerializeField] private float timerFontSize = 6f;

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

    public override string Id => "bomb";
    public override string DisplayName => "Bomb Defusal";
    public override bool Running => running;

    // --- Tracker: bomb parts (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => bombParts;
    public override double TrackerGoal => partsGoal;
    public override string RequirementFormat => requirementFormat;
    public override void SetTrackerCount(double value) => bombParts = System.Math.Max(0d, value);

    /// <summary>Bomb parts you have. Other systems spend them with <see cref="TrySpendParts"/>.</summary>
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
        if (spawnTimer <= 0f) StartCoroutine(BombRoutine());
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

    private TextMeshPro MakeText(Transform parent, string name, string text, float size, Color color, Vector3 localPos)
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
        t.rectTransform.sizeDelta = new Vector2(bombSize * 3f, bombSize * 0.6f);
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
        Vector3 position = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));

        GameObject root = new GameObject("Bomb");
        root.transform.position = position;
        root.transform.rotation = cam.transform.rotation; // faces the camera

        // Casing.
        float bw = bombSize * 1.5f, bh = bombSize * 0.9f, bd = bombSize * 0.35f;
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Casing";
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(bw, bh, bd);
        Material bodyMat = MakeMaterial(bodyColor, 0f);
        if (bodyMat != null) body.GetComponent<Renderer>().sharedMaterial = bodyMat;

        TextMeshPro timer = MakeText(root.transform, "Timer", string.Format(timerFormat, timeLimit), timerFontSize, timerColor,
                                     new Vector3(0f, 0f, -(bd * 0.5f + 0.02f)));
        if (!string.IsNullOrEmpty(hintText))
            MakeText(root.transform, "Hint", hintText, timerFontSize * 0.33f, new Color(1f, 1f, 1f, 0.8f),
                     new Vector3(0f, bh * 0.5f + bombSize * 0.18f, 0f));

        // Three wires hanging from the bottom: red, green, blue.
        Color[] colors = { redWireColor, greenWireColor, blueWireColor };
        float wireLength = bombSize * 1.1f;
        float spacing = bw * wireSpacing;
        GameObject[] wires = new GameObject[3];
        BoxCollider[] hitboxes = new BoxCollider[3];
        bool[] cut = new bool[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wire.name = "Wire " + i;
            Destroy(wire.GetComponent<Collider>());
            wire.transform.SetParent(root.transform, false);
            wire.transform.localScale = new Vector3(wireThickness, wireLength, wireThickness);
            wire.transform.localPosition = new Vector3((i - 1) * spacing, -(bh * 0.5f + wireLength * 0.5f), 0f);
            Material wm = MakeMaterial(colors[i], 0.35f);
            if (wm != null) wire.GetComponent<Renderer>().sharedMaterial = wm;

            // A separate, fatter trigger so thin wires are easy to click.
            GameObject hit = new GameObject("Hitbox " + i);
            hit.transform.SetParent(root.transform, false);
            hit.transform.localPosition = wire.transform.localPosition;
            BoxCollider box = hit.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(wireThickness * wireClickMultiplier, wireLength, wireThickness * wireClickMultiplier);

            wires[i] = wire;
            hitboxes[i] = box;
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
                    if (!cut[i] && hitboxes[i].Raycast(ray, out RaycastHit info, 1000f) && info.distance < best)
                    {
                        best = info.distance;
                        chosen = i;
                    }
            }
            yield return null;
        }

        // Result.
        bool defused = chosen >= 0 && chosen == CorrectWire();
        if (chosen >= 0) StartCoroutine(SeverWire(wires[chosen], root.transform));

        if (defused)
        {
            int parts = Random.Range(Mathf.Min(partsMin, partsMax), Mathf.Max(partsMin, partsMax) + 1);
            bombParts += parts;
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

    /// <summary>The cut wire falls away from the bomb.</summary>
    private IEnumerator SeverWire(GameObject wire, Transform bomb)
    {
        if (wire == null) yield break;
        // Detach the wire so it can fall; the bomb keeps a short stub.
        Vector3 pos = wire.transform.position;
        Quaternion rot = wire.transform.rotation;
        Vector3 scale = wire.transform.lossyScale;
        GameObject stub = Instantiate(wire, bomb);
        stub.transform.localScale = new Vector3(scale.x / bomb.lossyScale.x, scale.y * 0.2f / bomb.lossyScale.y, scale.z / bomb.lossyScale.z);
        stub.transform.position = pos + wire.transform.up * (scale.y * 0.4f);

        wire.transform.SetParent(null, true);
        float t = 0f;
        Vector3 spin = Random.onUnitSphere * 180f;
        while (t < 0.9f && wire != null)
        {
            t += Time.deltaTime;
            wire.transform.position += Vector3.down * (t * 6f * Time.deltaTime);
            wire.transform.Rotate(spin * Time.deltaTime, Space.Self);
            yield return null;
        }
        if (wire != null) Destroy(wire);
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
            go.transform.position = start + cam.transform.up * (bombSize * 0.8f + k * 1.5f) - cam.transform.forward * 0.5f;
            go.transform.rotation = cam.transform.rotation;
            Color c = color;
            c.a *= 1f - k * k;
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
