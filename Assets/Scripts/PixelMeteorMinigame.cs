using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static PixelInput;

/// <summary>
/// Meteor minigame for Pixel Clicker.
///
/// Once bought in the shop's Minigames tab, a large glowing meteor flies slowly across the screen every so often.
/// Every click on it gives you meteor chunks (+1 by default) - the meteor is NOT destroyed, so you can keep clicking
/// until it has gone by. Collecting enough chunks unlocks the Meteor Pixel in the shop.
///
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelMeteorMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (for the camera and the cube's position). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Meteor Strike is bought.)")]
    [SerializeField] private bool running = false;

    [Header("Meteor Chunks (goal)")]
    [Min(1)]
    [Tooltip("Chunks you need in total before the Meteor Pixel can be bought. The count keeps going afterwards.")]
    [SerializeField] private double chunkThreshold = 100;

    [Tooltip("Runtime: chunks collected so far (saved with the game). You can type a number to test the unlock.")]
    [SerializeField] private double chunks = 0;

    [Min(1)]
    [Tooltip("Chunks gained by each click on the meteor.")]
    [SerializeField] private double chunksPerClick = 1;

    [Tooltip("Title of this minigame's tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Meteor Chunks";

    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Click the meteor as it passes. Collect enough chunks to unlock the Meteor Pixel.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = chunks collected, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} meteor chunks";

    [Header("When it appears")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before the first meteor.")]
    [SerializeField] private float firstMeteorDelay = 10f;

    [Min(1f)]
    [Tooltip("Shortest wait between meteors (seconds).")]
    [SerializeField] private float minInterval = 60f;

    [Min(1f)]
    [Tooltip("Longest wait between meteors (seconds). Each wait is random between shortest and longest.")]
    [SerializeField] private float maxInterval = 120f;

    [Header("Movement")]
    [Min(2f)]
    [Tooltip("Seconds the meteor takes to cross the screen. Higher = slower = more clicks.")]
    [SerializeField] private float crossSeconds = 14f;

    [Range(0f, 1f)]
    [Tooltip("Lowest height of the meteor's path (fraction of screen height).")]
    [SerializeField] private float minHeight = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Highest height of the meteor's path (fraction of screen height).")]
    [SerializeField] private float maxHeight = 0.85f;

    [Range(0.05f, 1.5f)]
    [Tooltip("How far in front of the cube the meteor flies, as a fraction of the camera's distance to the cube.")]
    [SerializeField] private float depthFraction = 0.8f;

    [Min(0f)]
    [Tooltip("How fast the rock tumbles (degrees per second).")]
    [SerializeField] private float tumbleDegrees = 25f;

    [Header("Look")]
    [Min(0.2f)]
    [Tooltip("Size of the meteor (world units).")]
    [SerializeField] private float meteorSize = 2.8f;

    [Tooltip("Colour of the rock.")]
    [SerializeField] private Color rockColor = new Color(0.32f, 0.2f, 0.14f, 1f);

    [Tooltip("Colour of the glowing heat on the rock and of the tail.")]
    [SerializeField] private Color flameColor = new Color(1f, 0.45f, 0.1f, 1f);

    [Min(0f)]
    [Tooltip("How strongly the rock glows.")]
    [SerializeField] private float glowIntensity = 1.2f;

    [Min(0.1f)]
    [Tooltip("How long the fiery tail lasts behind the meteor (seconds). Longer = longer tail.")]
    [SerializeField] private float tailSeconds = 2.5f;

    [Range(0.1f, 1.5f)]
    [Tooltip("Width of the tail where it leaves the meteor, as a fraction of the meteor's size. It tapers to a point.")]
    [SerializeField] private float tailWidth = 0.8f;

    [Tooltip("Add a real light to the meteor so it lights up the scene as it passes.")]
    [SerializeField] private bool castLight = true;

    [Min(1f)]
    [Tooltip("The clickable area is the meteor's size times this.")]
    [SerializeField] private float clickSizeMultiplier = 1.1f;

    [Header("Click Feedback")]
    [Range(0f, 0.4f)]
    [Tooltip("How much the meteor squashes when clicked. 0 = no reaction.")]
    [SerializeField] private float punchAmount = 0.1f;

    [Tooltip("Text that rises from where you clicked. {0} = chunks gained.")]
    [SerializeField] private string popupFormat = "+{0} chunk";

    [Tooltip("Size of that text (3D text: about 10 per world unit).")]
    [SerializeField] private float popupFontSize = 5f;

    [Tooltip("Colour of that text.")]
    [SerializeField] private Color popupColor = new Color(1f, 0.8f, 0.4f, 1f);

    [Min(0.1f)]
    [Tooltip("How long that text rises and fades (seconds).")]
    [SerializeField] private float popupSeconds = 0.9f;

    [Tooltip("Sound played on each click.")]
    [SerializeField] private AudioClip hitSound;

    [Range(0f, 1f)]
    [Tooltip("Click sound volume.")]
    [SerializeField] private float soundVolume = 1f;

    [Header("Events")]
    [Tooltip("Fired when a meteor appears.")]
    public UnityEvent onMeteorAppeared;

    [Tooltip("Fired for every chunk click. Passes nothing; read the total from the tracker.")]
    public UnityEvent onChunkCollected;

    [Tooltip("Fired once, when the chunk count first reaches the goal.")]
    public UnityEvent onThresholdReached;

    private float spawnTimer;
    private bool meteorActive;
    private bool thresholdAnnounced;
    private AudioSource audioSource;

    public override string DisplayName => "Meteor Strike";
    public override string Id => "meteor";
    public override bool Running => running;

    // --- Tracker: meteor chunks (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => chunks;
    public override double TrackerGoal => chunkThreshold;
    public override string RequirementFormat => requirementFormat;

    public override void SetTrackerCount(double value)
    {
        chunks = System.Math.Max(0d, value);
        thresholdAnnounced = chunks >= chunkThreshold;
    }

    protected override void Awake()
    {
        base.Awake();
        clicker = clicker != null ? clicker : PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelMeteorMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (startRunning) running = true;
        spawnTimer = firstMeteorDelay;
        thresholdAnnounced = chunks >= chunkThreshold;
    }

    private void Update()
    {
        if (!running || meteorActive) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f) StartCoroutine(MeteorRoutine());
    }

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstMeteorDelay;
    }

    public override void Deactivate() => running = false;

    /// <summary>Sends a meteor right now (right-click the component &gt; Send Meteor Now).</summary>
    [ContextMenu("Send Meteor Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !meteorActive) StartCoroutine(MeteorRoutine());
    }

    // ------------------------------------------------------------------
    // The meteor
    // ------------------------------------------------------------------

    private IEnumerator MeteorRoutine()
    {
        meteorActive = true;
        onMeteorAppeared?.Invoke();

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) { meteorActive = false; yield break; }

        float depth = 10f;
        if (clicker.PixelTransform != null)
            depth = Mathf.Max(cam.nearClipPlane + 0.5f,
                              Vector3.Dot(clicker.PixelTransform.position - cam.transform.position, cam.transform.forward)
                              * depthFraction);

        // A straight slow diagonal from one side of the screen to the other.
        bool leftToRight = Random.value < 0.5f;
        float fromX = leftToRight ? -0.15f : 1.15f, toX = leftToRight ? 1.15f : -0.15f;
        float lo = Mathf.Min(minHeight, maxHeight), hi = Mathf.Max(minHeight, maxHeight);
        float fromY = Random.Range(lo, hi), toY = Random.Range(lo, hi);
        Vector3 Path(float k) => cam.ViewportToWorldPoint(new Vector3(Mathf.Lerp(fromX, toX, k), Mathf.Lerp(fromY, toY, k), depth));
        
        // ---- Build it: lumpy rock, glowing heat, fiery tail, light.
        GameObject root = new GameObject("Meteor");
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        rock.transform.SetParent(root.transform, false);
        SphereCollider hit = rock.GetComponent<SphereCollider>();
        hit.isTrigger = true; // never pushes old pixels around
        hit.radius = 0.5f * clickSizeMultiplier;

        Material rockMat = clicker.CreateVisualMaterial(rockColor, false);
        if (rockMat != null)
        {
            rockMat.EnableKeyword("_EMISSION");
            rockMat.SetColor("_EmissionColor", flameColor * glowIntensity * 0.35f);
            rock.GetComponent<Renderer>().sharedMaterial = rockMat;
        }
        else
        {
            rock.GetComponent<Renderer>().material.color = rockColor;
        }

        // Smooth tapering tail: one trail renderer that fades from flame colour to transparent.
        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = tailSeconds;
        trail.startWidth = meteorSize * tailWidth;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.alignment = LineAlignment.View;
        Shader trailShader = Shader.Find("Sprites/Default");
        trail.sharedMaterial = trailShader != null ? new Material(trailShader) : clicker.CreateVisualMaterial(flameColor, true);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(flameColor, Color.white, 0.4f), 0f), new GradientColorKey(flameColor, 0.35f), new GradientColorKey(new Color(flameColor.r * 0.6f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        Light glow = null;
        if (castLight)
        {
            GameObject lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = flameColor;
            glow.range = meteorSize * 5f;
            glow.intensity = glowIntensity * 2f;
            glow.shadows = LightShadows.None;
        }

        // ---- Fly.
        float t = 0f;
        float punch = 0f;
        Vector3 baseScale = new Vector3(1f, 0.85f, 1.1f) * meteorSize;
        Quaternion spin = Random.rotation;
        Vector3 spinAxis = Random.onUnitSphere;

        while (t < crossSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / crossSeconds);

            root.transform.position = Path(k);
            rock.transform.rotation = spin * Quaternion.AngleAxis(t * tumbleDegrees, spinAxis);

            punch = Mathf.Max(0f, punch - Time.deltaTime / 0.15f);
            rock.transform.localScale = baseScale * (1f - punchAmount * punch);

            if (Time.timeScale > 0f && LeftPressed() && !PointerOverUI())
            {
                Ray ray = cam.ScreenPointToRay(PointerPosition());
                if (hit.Raycast(ray, out RaycastHit info, 1000f))
                {
                    punch = 1f;
                    CollectChunk(info.point, cam);
                }
            }

            yield return null;
        }

        Destroy(root);
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        meteorActive = false;
    }

    private void CollectChunk(Vector3 point, Camera cam)
    {
        bool reachedBefore = chunks >= chunkThreshold;
        chunks += chunksPerClick;
        onChunkCollected?.Invoke();
        if (!reachedBefore && chunks >= chunkThreshold && !thresholdAnnounced)
        {
            thresholdAnnounced = true;
            onThresholdReached?.Invoke();
        }

        if (hitSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(hitSound, soundVolume);
        }

        StartCoroutine(PopupRoutine(point, cam, string.Format(popupFormat, PixelClicker.FormatNumber(chunksPerClick))));
    }

    /// <summary>Text that rises from the click point and fades away.</summary>
    private IEnumerator PopupRoutine(Vector3 start, Camera cam, string message)
    {
        GameObject go = new GameObject("Chunk Popup");
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = popupFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(20f, 3f);

        float t = 0f;
        while (t < popupSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / popupSeconds);
            go.transform.position = start + cam.transform.up * (k * 1.2f) - cam.transform.forward * 0.5f; // a little toward the camera
            go.transform.rotation = cam.transform.rotation;
            Color c = popupColor;
            c.a *= 1f - k * k;
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
