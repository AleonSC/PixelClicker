using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Black hole minigame for Pixel Clicker.
///
/// Once bought in the shop's Minigames tab, a dark swirling black hole opens on the floor every so often, at a
/// random spot and a random size, and stays for a while. Any old pixel that comes within its radius is caught:
/// it spirals inward, stretching out like spaghetti, and vanishes into the singularity.
///
/// Every pixel swallowed counts toward the SINGULARITY TRACKER (shown in the shop). Once the tracker reaches
/// the threshold, the Singularity Pixel can be bought. The tracker keeps counting after that.
///
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelBlackholeMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, old pixels). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when the Black Hole is bought.)")]
    [SerializeField] private bool running = false;

    [Header("Singularity Tracker")]
    [Min(1)]
    [Tooltip("Pixels that must be swallowed in total before the Singularity Pixel can be bought. The tracker keeps counting afterwards.")]
    [SerializeField] private double singularityThreshold = 250;

    [Tooltip("Runtime: pixels swallowed so far (saved with the game). You can type a number to test the unlock.")]
    [SerializeField] private double singularityCount = 0;

    [Tooltip("Title of this minigame's tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Singularity Tracker";

    [TextArea(1, 4)]
    [Tooltip("Description of the tracker row in the shop's Minigames tab.")]
    [SerializeField] private string trackerHelp = "Black holes swallow old pixels that land near them. Push pixels toward an open hole with the fan, Pixel Grabbing or the hose. Feed it enough to unlock the Singularity Pixel.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = pixels swallowed, {1} = goal.")]
    [SerializeField] private string requirementLine = "Feed {1} pixels to black holes: {0} / {1}";

    [Header("Hints")]
    [Tooltip("The first time a black hole opens, show a tip box explaining what to do (once; remembered between sessions).")]
    [SerializeField] private bool showFirstHoleHint = true;

    [TextArea(2, 5)]
    [Tooltip("The tip. {0} = the goal.")]
    [SerializeField] private string firstHoleHint = "A black hole has opened!\nIt swallows old pixels that land near it. Push them in with the fan, Pixel Grabbing or the hose. Feed it {0} pixels to unlock the Singularity Pixel.";

    [Tooltip("Show a live counter above each black hole (pixels swallowed / goal).")]
    [SerializeField] private bool showHoleCounter = true;

    [Min(1f)]
    [Tooltip("Size of the counter text above a black hole.")]
    [SerializeField] private float counterTextSize = 4f;

    [Tooltip("Counter text. {0} = pixels swallowed, {1} = goal.")]
    [SerializeField] private string counterFormat = "{0} / {1}";

    [Tooltip("Counter text once the goal has been reached. {0} = pixels swallowed.")]
    [SerializeField] private string counterReachedFormat = "{0} swallowed";

    [Tooltip("Also pay out the pixels' original reward when they are swallowed (like the Vacuum). Off = they are simply lost to the singularity.")]
    [SerializeField] private bool creditSwallowedPixels = false;

    [Header("When it opens")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before the first black hole can open.")]
    [SerializeField] private float firstHoleDelay = 15f;

    [Min(1f)]
    [Tooltip("Shortest wait between black holes (seconds).")]
    [SerializeField] private float minInterval = 60f;

    [Min(1f)]
    [Tooltip("Longest wait between black holes (seconds). Each wait is random between shortest and longest.")]
    [SerializeField] private float maxInterval = 120f;

    [Min(1f)]
    [Tooltip("How long a black hole stays open (seconds).")]
    [SerializeField] private float durationSeconds = 20f;

    [Min(0.1f)]
    [Tooltip("How long it takes to open (seconds).")]
    [SerializeField] private float openSeconds = 1f;

    [Min(0.1f)]
    [Tooltip("How long it takes to close (seconds).")]
    [SerializeField] private float closeSeconds = 1.2f;

    [Header("Size and Place")]
    [Min(0.2f)]
    [Tooltip("Smallest black hole radius (world units). Each hole gets a random size between smallest and largest.")]
    [SerializeField] private float minRadius = 1.5f;

    [Min(0.2f)]
    [Tooltip("Largest black hole radius (world units).")]
    [SerializeField] private float maxRadius = 3.5f;

    [Tooltip("The hole opens under a random spot inside this part of the screen (viewport: 0,0 = bottom-left, 1,1 = top-right). " +
             "This is the lower-left corner of that area.")]
    [SerializeField] private Vector2 screenAreaMin = new Vector2(0.15f, 0.1f);

    [Tooltip("Upper-right corner of that area.")]
    [SerializeField] private Vector2 screenAreaMax = new Vector2(0.85f, 0.55f);

    [Tooltip("Layers that count as floor when picking a spot.")]
    [SerializeField] private LayerMask floorLayers = ~0;

    [Tooltip("If the picked spot is not over any floor collider, the hole goes on a flat plane at this height.")]
    [SerializeField] private float fallbackFloorY = 0f;

    [Header("Swallowing and Spaghettification")]
    [Min(0.1f)]
    [Tooltip("Old pixels below this height above the hole are caught (world units).")]
    [SerializeField] private float captureHeight = 3f;

    [Min(0.1f)]
    [Tooltip("How fast a caught pixel moves toward the centre (world units per second). It speeds up as it gets closer.")]
    [SerializeField] private float pullSpeed = 2.5f;

    [Tooltip("How fast a caught pixel circles the centre (degrees per second). It speeds up as it gets closer.")]
    [SerializeField] private float swirlDegreesPerSecond = 220f;

    [Min(0f)]
    [Tooltip("How much longer a pixel gets right before it is swallowed (1 = double length, 8 = nine times as long).")]
    [SerializeField] private float maxStretch = 7f;

    [Range(0.02f, 1f)]
    [Tooltip("How thin a pixel gets at the end (1 = doesn't thin, 0.1 = a thread).")]
    [SerializeField] private float minThickness = 0.15f;

    [Range(0.02f, 0.6f)]
    [Tooltip("The event horizon: a pixel is swallowed once it reaches this fraction of the hole's radius from the centre.")]
    [SerializeField] private float horizonFraction = 0.12f;

    [Tooltip("Time dilation: while a black hole is open, old pixels stop despawning (their lifetime stops counting down). They age normally again when it closes.")]
    [SerializeField] private bool freezeOldPixels = true;

    [Header("Look")]
    [Tooltip("Colour of the swirl arms.")]
    [SerializeField] private Color armColor = new Color(0.55f, 0.2f, 0.95f, 1f);

    [Tooltip("Colour of the dark centre.")]
    [SerializeField] private Color coreColor = new Color(0.01f, 0f, 0.03f, 1f);

    [Range(0.5f, 3f)]
    [Tooltip("Brightens the swirl arms (1 = the arm colour as it is, 2 = twice as bright).")]
    [SerializeField] private float armBrightness = 1.7f;

    [Range(0f, 0.8f)]
    [Tooltip("How visible the dark gaps between the arms are (0 = clear gaps, higher = the whole hole is a stronger disc).")]
    [SerializeField] private float armMinAlpha = 0.22f;

    [Tooltip("Colour of the bright ring around the dark centre (the accretion ring).")]
    [SerializeField] private Color rimColor = new Color(0.9f, 0.7f, 1f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Brightness of the ring around the centre (0 = no ring).")]
    [SerializeField] private float rimStrength = 0.9f;

    [Range(0.02f, 0.4f)]
    [Tooltip("Thickness of the ring around the centre (fraction of the hole's radius).")]
    [SerializeField] private float rimWidth = 0.08f;

    [Range(0f, 4f)]
    [Tooltip("Makes the swirl glow by itself so it stays visible on a dark background (0 = only lit by the scene's lights).")]
    [SerializeField] private float glowIntensity = 1.5f;

    [Range(0.1f, 1f)]
    [Tooltip("Overall opacity of the hole.")]
    [SerializeField] private float opacity = 0.95f;

    [Min(1)]
    [Tooltip("Number of spiral arms in the outer swirl.")]
    [SerializeField] private int outerArms = 3;

    [Tooltip("How tightly the outer arms wind (negative = the other way).")]
    [SerializeField] private float outerTwist = 6f;

    [Tooltip("Spin speed of the outer swirl (degrees per second, negative = the other way).")]
    [SerializeField] private float outerSpinDegrees = 70f;

    [Min(1)]
    [Tooltip("Number of spiral arms in the inner swirl.")]
    [SerializeField] private int innerArms = 5;

    [Tooltip("How tightly the inner arms wind.")]
    [SerializeField] private float innerTwist = -8f;

    [Tooltip("Spin speed of the inner swirl (degrees per second). Different from the outer one for a swirling look.")]
    [SerializeField] private float innerSpinDegrees = -180f;

    [Tooltip("The hole floats this far above the floor so it doesn't flicker against it.")]
    [SerializeField] private float heightAboveFloor = 0.03f;

    [Header("Events")]
    [Tooltip("Fired when a black hole opens.")]
    public UnityEvent onHoleOpened;

    [Tooltip("Fired when a pixel is swallowed.")]
    public UnityEvent onPixelSwallowed;

    [Tooltip("Fired once, when the singularity tracker first reaches the threshold.")]
    public UnityEvent onThresholdReached;

    private float spawnTimer;
    private bool holeActive;

    public override bool Busy => holeActive;
    private Texture2D outerTexture, innerTexture;
    private Material outerMaterial, innerMaterial;
    private bool thresholdAnnounced;

    public override string DisplayName => "Black Hole";
    public override string Id => "blackhole";

    public override bool Running => running;

    // --- Tracker: the singularity (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerHelp;
    public override double TrackerCount => singularityCount;
    public override double TrackerGoal => singularityThreshold;
    public override string RequirementFormat => requirementLine;
    public override void SetTrackerCount(double value) => SetSingularityCount(value);

    /// <summary>Pixels swallowed so far.</summary>
    public double SingularityCount => singularityCount;

    /// <summary>The amount the tracker needs to reach.</summary>
    public double SingularityThreshold => singularityThreshold;

    /// <summary>True once enough pixels have been swallowed to unlock the Singularity Pixel.</summary>
    public bool ThresholdReached => singularityCount >= singularityThreshold;

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null)
        {
            clicker = PixelFind.First<PixelClicker>();
        }
        if (clicker == null)
        {
            Debug.LogError("PixelBlackholeMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (startRunning) running = true;
        spawnTimer = firstHoleDelay;
        thresholdAnnounced = ThresholdReached; // don't fire the event for a count that was already past it
    }

    private void Update()
    {
        if (!running || holeActive) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(HoleRoutine());
            else spawnTimer = PixelMinigameLimits.RetrySeconds; // too many minigames running right now
        }
    }

    /// <summary>Starts the minigame (the shop calls this when it is bought). Safe to call more than once.</summary>
    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstHoleDelay;
    }

    /// <summary>Stops the minigame (the tracker keeps its count).</summary>
    public override void Deactivate() => running = false;

    /// <summary>Sets the tracker (used when loading a save).</summary>
    public void SetSingularityCount(double value)
    {
        singularityCount = System.Math.Max(0d, value);
        thresholdAnnounced = ThresholdReached;
    }

    /// <summary>Opens a black hole right now (right-click the component &gt; Open Black Hole Now).</summary>
    [ContextMenu("Open Black Hole Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !holeActive) StartCoroutine(HoleRoutine());
    }

    // ------------------------------------------------------------------
    // The hole
    // ------------------------------------------------------------------

    private IEnumerator HoleRoutine()
    {
        holeActive = true;
        Report(MinigameEvent.Spawned);

        if (!TryPickSpot(out Vector3 center))
        {
            // Nowhere to put it right now: try again soon.
            spawnTimer = 5f;
            holeActive = false;
            yield break;
        }

        EnsureAssets();
        float radius = Random.Range(Mathf.Min(minRadius, maxRadius), Mathf.Max(minRadius, maxRadius));
        center.y += heightAboveFloor;

        // Two counter-rotating swirl discs.
        GameObject root = new GameObject("Black Hole");
        root.transform.position = center;
        Transform outer = BuildSwirl(root.transform, "Outer Swirl", outerMaterial, 0f);
        Transform inner = BuildSwirl(root.transform, "Inner Swirl", innerMaterial, 0.01f);

        // A live counter above the hole.
        TextMeshPro counter = null;
        if (showHoleCounter)
        {
            GameObject counterObject = new GameObject("Hole Counter");
            counterObject.transform.SetParent(root.transform, false);
            counter = counterObject.AddComponent<TextMeshPro>();
            counter.fontSize = counterTextSize;
            counter.fontStyle = FontStyles.Bold;
            counter.alignment = TextAlignmentOptions.Center;
            counter.color = Color.white;
            counter.outlineWidth = 0.2f;
            counter.outlineColor = new Color32(0, 0, 0, 255);
            counter.rectTransform.sizeDelta = new Vector2(8f, 2f);
            if (clicker != null && clicker.UIFont != null) counter.font = clicker.UIFont;
        }
        double shownCount = -1d;
        float punch = 0f;

        onHoleOpened?.Invoke();
        ShowFirstHoleHint();
        if (freezeOldPixels) OldPixelDespawn.Frozen = true; // time dilation: old pixels stop ageing while the hole is open

        float t = 0f;
        float total = openSeconds + durationSeconds + closeSeconds;
        while (t < total)
        {
            t += Time.deltaTime;

            // Grow, hold, shrink.
            float size = 1f;
            if (t < openSeconds) size = Mathf.SmoothStep(0f, 1f, t / openSeconds);
            else if (t > openSeconds + durationSeconds) size = Mathf.SmoothStep(1f, 0f, (t - openSeconds - durationSeconds) / closeSeconds);
            float currentRadius = radius * size;

            outer.localScale = new Vector3(currentRadius * 2f, currentRadius * 2f, 1f);
            inner.localScale = new Vector3(currentRadius * 1.1f, currentRadius * 1.1f, 1f);
            SpinAround(outer, t * outerSpinDegrees);
            SpinAround(inner, t * innerSpinDegrees);

            // Catch old pixels that come close (only while it is open enough to matter).
            if (size > 0.3f) CatchPixels(center, currentRadius, radius);

            if (counter != null)
            {
                if (singularityCount != shownCount)
                {
                    if (shownCount >= 0d) punch = 1f; // a pixel went in: the counter jumps
                    shownCount = singularityCount;
                    counter.text = ThresholdReached
                        ? string.Format(counterReachedFormat, PixelClicker.FormatNumber(singularityCount))
                        : string.Format(counterFormat, PixelClicker.FormatNumber(singularityCount), PixelClicker.FormatNumber(singularityThreshold));
                }
                punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 4f);

                Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
                counter.transform.position = center + Vector3.up * (0.9f + radius * 0.15f);
                if (cam != null) counter.transform.rotation = cam.transform.rotation;
                counter.transform.localScale = Vector3.one * (size * (1f + punch * 0.35f));
            }

            yield return null;
        }

        Destroy(root);
        OldPixelDespawn.Frozen = false;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        holeActive = false;
    }

    /// <summary>Spins a flat disc around the world up axis (the disc itself is rotated 90 degrees to lie flat).</summary>
    private static void SpinAround(Transform disc, float degrees)
    {
        disc.localRotation = Quaternion.Euler(0f, degrees, 0f) * Quaternion.Euler(90f, 0f, 0f);
    }

    private Transform BuildSwirl(Transform parent, string objectName, Material material, float lift)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objectName;
        Destroy(quad.GetComponent<Collider>()); // never blocks clicks or old pixels
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, lift, 0f);

        Renderer r = quad.GetComponent<Renderer>();
        if (material != null) r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return quad.transform;
    }

    private void CatchPixels(Vector3 center, float currentRadius, float fullRadius)
    {
        var pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody body = pixels[i];
            if (body == null) continue;

            Vector3 p = body.position;
            float dx = p.x - center.x, dz = p.z - center.z;
            if (dx * dx + dz * dz > currentRadius * currentRadius) continue;
            if (p.y < center.y - 0.5f || p.y > center.y + captureHeight) continue;

            if (clicker.ReleaseOldPixel(body, creditSwallowedPixels))
                StartCoroutine(SpiralIn(body, center, currentRadius, fullRadius));
        }
    }

    /// <summary>
    /// A caught pixel circles the centre, drawn inward faster and faster, and gets stretched along its direction
    /// of travel and thinned out - spaghettification - until it crosses the event horizon.
    /// </summary>
    private IEnumerator SpiralIn(Rigidbody body, Vector3 center, float captureRadius, float fullRadius)
    {
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;

        float uniform = t.localScale.x;
        Vector3 start = t.position;
        float dist = new Vector2(start.x - center.x, start.z - center.z).magnitude;
        float angle = Mathf.Atan2(start.z - center.z, start.x - center.x);
        float startHeight = start.y;
        float horizon = Mathf.Max(0.05f, fullRadius * horizonFraction);
        float span = Mathf.Max(0.01f, dist - horizon);
        float sign = outerSpinDegrees < 0f ? -1f : 1f; // circle the same way the swirl turns

        while (t != null && dist > horizon)
        {
            float k = Mathf.Clamp01(1f - (dist - horizon) / span); // 0 when caught -> 1 at the horizon

            dist -= pullSpeed * (1f + 3f * k) * Time.deltaTime;
            angle += sign * swirlDegreesPerSecond * Mathf.Deg2Rad * (1f + 2f * k) * Time.deltaTime;

            Vector3 pos = new Vector3(center.x + Mathf.Cos(angle) * dist,
                                      Mathf.Lerp(startHeight, center.y, k),
                                      center.z + Mathf.Sin(angle) * dist);
            Vector3 toCenter = center - pos;
            t.position = pos;
            if (toCenter.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);

            // Long and thin toward the centre (local Z points at it), and gone at the very end.
            float stretch = 1f + k * maxStretch;
            float thin = Mathf.Lerp(1f, minThickness, k);
            float vanish = k > 0.85f ? 1f - (k - 0.85f) / 0.15f : 1f;
            t.localScale = new Vector3(uniform * thin, uniform * thin, uniform * stretch) * vanish;

            yield return null;
        }

        if (t != null) Destroy(t.gameObject);
        RegisterContribution();
    }

    private const string HintPref = "PixelClicker.Hint.BlackHole";

    /// <summary>The very first black hole explains what to do (once, remembered between sessions).</summary>
    private void ShowFirstHoleHint()
    {
        if (!showFirstHoleHint || singularityCount > 0d) return;
        try
        {
            if (PlayerPrefs.GetInt(HintPref, 0) != 0) return;
            PlayerPrefs.SetInt(HintPref, 1);
        }
        catch (System.Exception) { /* no PlayerPrefs: just show it */ }

        string hintMessage = string.Format(firstHoleHint, PixelClicker.FormatNumber(singularityThreshold));
        PixelNotice.Show(hintMessage);
        PixelHints.Announce("A black hole opened!", hintMessage);
    }

    private void RegisterContribution()
    {
        singularityCount += 1d;
        onPixelSwallowed?.Invoke();

        if (!thresholdAnnounced && ThresholdReached)
        {
            thresholdAnnounced = true;
            onThresholdReached?.Invoke();
        }
    }

    // ------------------------------------------------------------------
    // Picking a spot on the floor
    // ------------------------------------------------------------------

    private bool TryPickSpot(out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector3 viewport = new Vector3(
                Random.Range(Mathf.Min(screenAreaMin.x, screenAreaMax.x), Mathf.Max(screenAreaMin.x, screenAreaMax.x)),
                Random.Range(Mathf.Min(screenAreaMin.y, screenAreaMax.y), Mathf.Max(screenAreaMin.y, screenAreaMax.y)), 0f);
            Ray ray = cam.ViewportPointToRay(viewport);

            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, floorLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
                if (clicker.PixelTransform != null && hit.transform.IsChildOf(clicker.PixelTransform)) continue;
                if (hit.normal.y < 0.5f) continue;
                point = hit.point;
                return true;
            }

            Plane floor = new Plane(Vector3.up, new Vector3(0f, fallbackFloorY, 0f));
            if (floor.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Swirl textures (drawn once in code, so no art assets are needed)
    // ------------------------------------------------------------------

    private void EnsureAssets()
    {
        if (outerMaterial != null && innerMaterial != null) return;

        outerTexture = BuildSwirlTexture(outerArms, outerTwist, 0.55f);
        innerTexture = BuildSwirlTexture(innerArms, innerTwist, 0.7f);
        outerMaterial = BuildMaterial(outerTexture);
        innerMaterial = BuildMaterial(innerTexture);
    }

    private Material BuildMaterial(Texture2D texture)
    {
        Color white = new Color(1f, 1f, 1f, opacity);
        Material m = clicker.CreateVisualMaterial(white, true);
        if (m == null) return null;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);

        // Self-lit: the swirl's own colours glow, so it doesn't vanish into a dark background.
        if (glowIntensity > 0f && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * glowIntensity);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", texture);
        }
        return m;
    }

    /// <summary>A round spiral: bright arms fading out toward the rim and a dark core in the middle.</summary>
    private Texture2D BuildSwirlTexture(int arms, float twist, float coreSize)
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);

                Color c = Color.clear;
                if (r <= 1f)
                {
                    float angle = Mathf.Atan2(v, u);
                    float arm = Mathf.Sin(arms * (angle + twist * r)) * 0.5f + 0.5f;     // 0..1 spiral pattern
                    float edge = 1f - Mathf.SmoothStep(0.7f, 1f, r);                     // fades out at the rim
                    float core = 1f - Mathf.SmoothStep(0f, coreSize, r);                 // dark in the middle

                    Color body = Color.Lerp(armColor * 0.35f, armColor, arm);
                    body = new Color(Mathf.Clamp01(body.r * armBrightness), Mathf.Clamp01(body.g * armBrightness),
                                     Mathf.Clamp01(body.b * armBrightness), 1f);
                    Color rgb = Color.Lerp(body, coreColor, core);
                    float alpha = Mathf.Max(Mathf.Lerp(armMinAlpha, 0.95f, arm) * edge, core * edge);

                    // A bright ring just outside the dark centre.
                    float ring = rimStrength * Mathf.Exp(-Mathf.Pow((r - coreSize) / rimWidth, 2f));
                    rgb = Color.Lerp(rgb, rimColor, ring);
                    alpha = Mathf.Max(alpha, ring * edge);

                    c = new Color(rgb.r, rgb.g, rgb.b, alpha);
                }
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        OldPixelDespawn.Frozen = false;
        if (outerTexture != null) Destroy(outerTexture);
        if (innerTexture != null) Destroy(innerTexture);
        if (outerMaterial != null) Destroy(outerMaterial);
        if (innerMaterial != null) Destroy(innerMaterial);
    }
}
