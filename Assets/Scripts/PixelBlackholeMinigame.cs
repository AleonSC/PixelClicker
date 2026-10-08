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
    [SerializeField] private string firstHoleMessage = "A black hole has opened!\nIt is hungry for old pixels. Find a way to feed it - {0} pixels will unlock something new.";

    [Tooltip("Show a live counter above each black hole (pixels swallowed / goal).")]
    [SerializeField] private bool showHoleCounter = true;

    [Min(0.2f)]
    [Tooltip("Seconds the counter stays visible after a pixel is consumed or the mouse leaves the hole, before it fades away.")]
    [SerializeField] private float counterVisibleSeconds = 3f;

    [Min(0.05f)]
    [Tooltip("Seconds the counter takes to fade in / out.")]
    [SerializeField] private float counterFadeSeconds = 0.6f;

    [Tooltip("Show the counter while the mouse is over the hole.")]
    [SerializeField] private bool counterShowOnHover = true;

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

    protected override void OnDespawned()
    {
        holeActive = false;
        OldPixelDespawn.Frozen = false; // time dilation ends with the hole
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }
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
        Track(root);
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
        float counterAlpha = 0f;           // starts hidden: it shows when a pixel goes in or the mouse is over the hole
        float lastActivity = -999f;

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
                    if (shownCount >= 0d) { punch = 1f; lastActivity = t; } // a pixel went in: the counter jumps and shows
                    shownCount = singularityCount;
                    counter.text = ThresholdReached
                        ? string.Format(counterReachedFormat, PixelClicker.FormatNumber(singularityCount))
                        : string.Format(counterFormat, PixelClicker.FormatNumber(singularityCount), PixelClicker.FormatNumber(singularityThreshold));
                }
                punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 4f);

                Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

                // Visible for a few seconds after activity (a pixel consumed, or the mouse over the hole), then it fades away.
                if (counterShowOnHover && cam != null && size > 0.3f && MouseOverHole(cam, center, currentRadius)) lastActivity = t;
                float wanted = t - lastActivity < counterVisibleSeconds ? 1f : 0f;
                counterAlpha = Mathf.MoveTowards(counterAlpha, wanted, Time.deltaTime / counterFadeSeconds);
                // Only the vertex alpha changes (it fades the outline with the face); the outline colour / material stay untouched,
                // because changing them every frame made the text glitch.
                counter.alpha = counterAlpha;
                counter.enabled = counterAlpha > 0.001f;
                counter.transform.localScale = Vector3.one * (size * (1f + punch * 0.35f));
                counter.transform.position = center + Vector3.up * (0.9f + radius * 0.15f);
                if (cam != null)
                {
                    counter.transform.rotation = cam.transform.rotation;
                    counter.transform.position = PixelUIKit.KeepOnScreen(cam, counter, counter.transform.position); // never off screen
                }
            }

            yield return null;
        }

        Destroy(root);
        OldPixelDespawn.Frozen = false;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        holeActive = false;
    }

    /// <summary>Is the mouse pointer over the hole (its on-screen size, with a little slack)?</summary>
    private static bool MouseOverHole(Camera cam, Vector3 center, float worldRadius)
    {
        Vector3 c = cam.WorldToScreenPoint(center);
        if (c.z <= 0f) return false;
        Vector3 edge = cam.WorldToScreenPoint(center + cam.transform.right * worldRadius);
        float screenRadius = Mathf.Max(40f, Vector2.Distance(c, edge) * 1.15f);
        return Vector2.Distance(PixelInput.PointerPosition(), c) <= screenRadius;
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

    private static Material spiralTrailMaterial;
    private static Sprite softSprite;

    /// <summary>
    /// A caught pixel is drawn into the hole: it eases into a spiral from wherever it was (no sudden jump), circles faster and
    /// faster as it falls in, turns to point along its path and is stretched into a curved streak, darkens (red-shift), leaves a
    /// fading purple trail, and vanishes across the event horizon with a small flash.
    /// </summary>
    private IEnumerator SpiralIn(Rigidbody body, Vector3 center, float captureRadius, float fullRadius)
    {
        Transform t = body.transform;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;

        // A trail that stays behind when the pixel is gone.
        if (spiralTrailMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) spiralTrailMaterial = new Material(shader) { name = "HoleTrail" };
        }
        GameObject trailGo = new GameObject("Spiral Trail");
        Track(trailGo);
        Track(body.gameObject);
        trailGo.transform.SetParent(t, false);
        TrailRenderer trail = trailGo.AddComponent<TrailRenderer>();
        trail.time = 0.45f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = Mathf.Max(0.05f, t.localScale.x * 0.55f);
        trail.endWidth = 0f;
        trail.alignment = LineAlignment.View;
        if (spiralTrailMaterial != null) trail.sharedMaterial = spiralTrailMaterial;
        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(armColor, Color.white, 0.35f), 0f), new GradientColorKey(armColor, 0.5f), new GradientColorKey(coreColor, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = trailGradient;

        // Colour handling: the pixel darkens as it falls in.
        Renderer[] renderers = t.GetComponentsInChildren<Renderer>();
        Color[] originals = new Color[renderers.Length];
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int r = 0; r < renderers.Length; r++)
        {
            renderers[r].GetPropertyBlock(block);
            Material m = renderers[r].sharedMaterial;
            originals[r] = !block.isEmpty ? block.GetColor("_BaseColor") : (m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
            if (originals[r] == Color.clear && m != null && m.HasProperty("_BaseColor")) originals[r] = m.GetColor("_BaseColor");
        }

        float uniform = t.localScale.x;
        Vector3 start = t.position;
        float dist = new Vector2(start.x - center.x, start.z - center.z).magnitude;
        float startDist = Mathf.Max(0.01f, dist);
        float angle = Mathf.Atan2(start.z - center.z, start.x - center.x);
        float startHeight = start.y;
        float horizon = Mathf.Max(0.05f, fullRadius * horizonFraction);
        float span = Mathf.Max(0.01f, dist - horizon);
        float sign = outerSpinDegrees < 0f ? -1f : 1f; // circle the same way the swirl turns
        float baseOmega = swirlDegreesPerSecond * Mathf.Deg2Rad;
        float age = 0f;
        Vector3 previous = start;

        while (t != null && dist > horizon)
        {
            float dt = Time.deltaTime;
            age += dt;
            float k = Mathf.Clamp01(1f - (dist - horizon) / span); // 0 when caught -> 1 at the horizon
            float ease = Mathf.SmoothStep(0f, 1f, age / 0.3f);     // the first moments ease in, so nothing jumps

            // Falls in faster and faster; spins faster the closer it gets (like conserved angular momentum).
            dist -= pullSpeed * (1f + 3f * k * k) * ease * dt;
            float omega = baseOmega * Mathf.Pow(startDist / Mathf.Max(dist, horizon * 0.8f), 1.3f);
            angle += sign * omega * ease * dt;

            float drop = Mathf.SmoothStep(0f, 1f, k);
            Vector3 pos = new Vector3(center.x + Mathf.Cos(angle) * dist,
                                      Mathf.Lerp(startHeight, center.y, drop),
                                      center.z + Mathf.Sin(angle) * dist);
            t.position = pos;

            // Turn to point along the path (smoothly, from whatever way it was tumbling).
            Vector3 travel = pos - previous;
            if (travel.sqrMagnitude > 1e-8f)
            {
                Quaternion target = Quaternion.LookRotation(travel.normalized, Vector3.up);
                t.rotation = Quaternion.Slerp(t.rotation, target, 1f - Mathf.Exp(-10f * dt));
            }
            previous = pos;

            // Stretched along its path into a streak that thins as it lengthens, then fades to nothing at the very end.
            float stretch = 1f + maxStretch * k * k;
            float thin = Mathf.Max(minThickness, 1f / Mathf.Sqrt(stretch));
            float vanish = Mathf.SmoothStep(1f, 0f, (k - 0.8f) / 0.2f);
            t.localScale = new Vector3(uniform * thin, uniform * thin, uniform * stretch) * vanish;

            // Darkens as it falls in.
            float fade = k * k;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null) continue;
                Color c = Color.Lerp(originals[r], coreColor, fade);
                c.a = originals[r].a;
                renderers[r].GetPropertyBlock(block);
                block.SetColor("_BaseColor", c);
                block.SetColor("_Color", c);
                renderers[r].SetPropertyBlock(block);
            }

            yield return null;
        }

        // Leave the trail to fade out by itself, then flash where the pixel went in.
        if (trailGo != null)
        {
            trailGo.transform.SetParent(null, true);
            Destroy(trailGo, trail.time + 0.1f);
        }
        if (t != null) Destroy(t.gameObject);
        StartCoroutine(AbsorbFlash(center, fullRadius));
        RegisterContribution();
    }

    /// <summary>A small soft flash at the centre each time the hole swallows a pixel.</summary>
    private IEnumerator AbsorbFlash(Vector3 center, float fullRadius)
    {
        if (softSprite == null) softSprite = BuildSoftSprite();
        GameObject go = new GameObject("Hole Flash");
        Track(go);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = softSprite;
        sr.sortingOrder = 5;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

        float size = Mathf.Max(0.4f, fullRadius * 0.45f);
        const float duration = 0.3f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            go.transform.position = center + Vector3.up * 0.15f;
            if (cam != null) go.transform.rotation = cam.transform.rotation;
            go.transform.localScale = Vector3.one * size * Mathf.Lerp(0.5f, 1.3f, k);
            Color c = Color.Lerp(armColor, Color.white, 0.5f * (1f - k));
            c.a = 0.85f * (1f - k) * (1f - k);
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    /// <summary>A white soft round blob (tinted by the sprite colour).</summary>
    private static Sprite BuildSoftSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HoleFlash" };
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = ((x + 0.5f) / size - 0.5f) * 2f, v = ((y + 0.5f) / size - 0.5f) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                a *= a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
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

        string hintMessage = string.Format(firstHoleMessage, PixelClicker.FormatNumber(singularityThreshold));
        PixelNotice.Show(hintMessage, small: true);
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
