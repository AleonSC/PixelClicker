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

    [Range(0f, 0.5f)]
    [Tooltip("How lumpy and craggy the rock is (0 = a smooth ball).")]
    [SerializeField] private float lumpiness = 0.28f;

    [Tooltip("Draw glowing lava cracks across the rock.")]
    [SerializeField] private bool lavaCracks = true;

    [Range(0f, 200f)]
    [Tooltip("Glowing embers shed behind the meteor, per second (0 = none).")]
    [SerializeField] private float embersPerSecond = 45f;

    [Range(0f, 1f)]
    [Tooltip("Strength of the soft fiery glow around the meteor (0 = none).")]
    [SerializeField] private float haloOpacity = 0.4f;

    [Min(1f)]
    [Tooltip("Size of that glow as a multiple of the meteor's size.")]
    [SerializeField] private float haloSize = 2.6f;

    [Tooltip("A thin white-hot core inside the fiery tail.")]
    [SerializeField] private bool hotCoreTail = true;

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
    [SerializeField] private float popupTextSize = 1.6f;

    [Range(0f, 1f)]
    [Tooltip("How see-through the message is (1 = solid, 0.7 = slightly transparent).")]
    [SerializeField] private float popupOpacity = 0.7f;

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

    public override bool Busy => meteorActive;
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
        if (spawnTimer <= 0f)
        {
            if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(MeteorRoutine());
            else spawnTimer = PixelMinigameLimits.RetrySeconds; // too many minigames running right now
        }
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
        Report(MinigameEvent.Spawned);

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
        Mesh lumpy = BuildLumpyRock(rock.GetComponent<MeshFilter>().sharedMesh);
        if (lumpy != null) rock.GetComponent<MeshFilter>().sharedMesh = lumpy;

        // Dark rock with glowing lava cracks (the cracks are the emission).
        Material rockMat = clicker.CreateVisualMaterial(lavaCracks ? Color.white : rockColor, false);
        if (rockMat != null)
        {
            if (lavaCracks)
            {
                EnsureRockTextures();
                rockMat.SetTexture("_BaseMap", rockAlbedo);
                rockMat.SetTexture("_MainTex", rockAlbedo);
                rockMat.SetTexture("_EmissionMap", rockEmission);
                rockMat.SetColor("_EmissionColor", flameColor * glowIntensity * 1.6f);
            }
            else rockMat.SetColor("_EmissionColor", flameColor * glowIntensity * 0.35f);
            rockMat.EnableKeyword("_EMISSION");
            if (rockMat.HasProperty("_Smoothness")) rockMat.SetFloat("_Smoothness", 0.15f);
            if (rockMat.HasProperty("_Glossiness")) rockMat.SetFloat("_Glossiness", 0.15f);
            if (rockMat.HasProperty("_Metallic")) rockMat.SetFloat("_Metallic", 0f);
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

        // A thin white-hot core inside the tail.
        if (hotCoreTail)
        {
            GameObject coreGo = new GameObject("Core Trail");
            coreGo.transform.SetParent(root.transform, false);
            TrailRenderer core = coreGo.AddComponent<TrailRenderer>();
            core.time = tailSeconds * 0.55f;
            core.startWidth = meteorSize * tailWidth * 0.32f;
            core.endWidth = 0f;
            core.minVertexDistance = 0.05f;
            core.alignment = LineAlignment.View;
            core.sharedMaterial = trail.sharedMaterial;
            Gradient coreGradient = new Gradient();
            coreGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.Lerp(flameColor, Color.white, 0.6f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
            core.colorGradient = coreGradient;
        }

        // Soft fiery glow around the rock (a camera-facing sprite).
        SpriteRenderer halo = null;
        if (haloOpacity > 0f)
        {
            if (haloSprite == null) haloSprite = BuildSoftSprite();
            GameObject haloGo = new GameObject("Halo");
            haloGo.transform.SetParent(root.transform, false);
            haloGo.transform.localScale = Vector3.one * meteorSize * haloSize;
            halo = haloGo.AddComponent<SpriteRenderer>();
            halo.sprite = haloSprite;
            halo.sortingOrder = -1;
        }

        // Embers drifting off the meteor (world space, so they stay behind as it moves on).
        if (embersPerSecond > 0f) BuildEmbers(root.transform);

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

            // Heat flicker: the halo faces the camera and breathes, the light flickers.
            float flicker = 0.85f + 0.15f * Mathf.Sin(t * 9f) * Mathf.Sin(t * 5.3f + 1f);
            if (halo != null)
            {
                halo.transform.rotation = cam.transform.rotation;
                halo.transform.position = root.transform.position + cam.transform.forward * (meteorSize * 0.6f); // just behind the rock
                Color hc = Color.Lerp(flameColor, Color.white, 0.15f);
                hc.a = haloOpacity * flicker;
                halo.color = hc;
            }
            if (glow != null) glow.intensity = glowIntensity * 2f * flicker;

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
        if (rockMat != null) Destroy(rockMat);
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        meteorActive = false;
    }

    // ------------------------------------------------------------------
    // Runtime-built visuals: lumpy rock mesh, lava texture, soft glow sprite, embers
    // ------------------------------------------------------------------

    private static Texture2D rockAlbedo, rockEmission;
    private static Sprite haloSprite;

    /// <summary>A copy of a sphere mesh with its surface pushed in and out by noise (craggy lumps).</summary>
    private Mesh BuildLumpyRock(Mesh sphere)
    {
        if (sphere == null || lumpiness <= 0f) return null;
        Vector3[] v = sphere.vertices;
        Vector3[] n = sphere.normals;
        float seed = Random.value * 100f;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = n[i] * 2.2f + new Vector3(seed, seed * 0.7f, seed * 1.3f);
            // Three noise planes make a cheap 3D noise; a finer layer adds small chips.
            float big = (Mathf.PerlinNoise(p.x, p.y) + Mathf.PerlinNoise(p.y, p.z) + Mathf.PerlinNoise(p.z, p.x)) / 3f - 0.5f;
            float small = (Mathf.PerlinNoise(p.x * 3.1f, p.z * 3.1f) + Mathf.PerlinNoise(p.y * 3.1f, p.x * 3.1f)) * 0.5f - 0.5f;
            v[i] = n[i] * (0.5f * (1f + (big * 1.6f + small * 0.5f) * lumpiness * 2f));
        }
        Mesh mesh = new Mesh { name = "LumpyRock" };
        mesh.vertices = v;
        mesh.uv = sphere.uv;
        mesh.triangles = sphere.triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Dark cracked rock (albedo) and the glowing cracks alone (emission), drawn once and shared.</summary>
    private static void EnsureRockTextures()
    {
        if (rockAlbedo != null && rockEmission != null) return;

        const int size = 192, cells = 34;
        System.Random rnd = new System.Random(4242);
        Vector2[] sites = new Vector2[cells];
        for (int i = 0; i < cells; i++) sites[i] = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble());

        Color32[] albedo = new Color32[size * size], emission = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                // Distance to the nearest and second nearest site (wrapping around the edges): the gap between them is a crack.
                float d1 = 9f, d2 = 9f;
                for (int s = 0; s < cells; s++)
                {
                    float dx = Mathf.Abs(uv.x - sites[s].x), dy = Mathf.Abs(uv.y - sites[s].y);
                    if (dx > 0.5f) dx = 1f - dx;
                    if (dy > 0.5f) dy = 1f - dy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
                float edge = d2 - d1;
                float crack = Mathf.Clamp01(1f - edge / 0.022f);
                crack *= 0.55f + 0.45f * Mathf.PerlinNoise(uv.x * 9f, uv.y * 9f); // uneven brightness along the crack

                float grain = Mathf.PerlinNoise(uv.x * 14f, uv.y * 14f) * 0.6f + Mathf.PerlinNoise(uv.x * 40f, uv.y * 40f) * 0.4f;
                float rock = 0.10f + grain * 0.16f;
                albedo[y * size + x] = new Color(rock * 1.15f, rock * 0.85f, rock * 0.7f, 1f);
                emission[y * size + x] = new Color(crack, crack * 0.55f, crack * 0.15f, 1f);
            }
        }

        rockAlbedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "MeteorRock" };
        rockAlbedo.SetPixels32(albedo);
        rockAlbedo.Apply(true, false);
        rockEmission = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "MeteorLava" };
        rockEmission.SetPixels32(emission);
        rockEmission.Apply(true, false);
    }

    /// <summary>A white soft round blob (tinted by the sprite colour).</summary>
    private static Sprite BuildSoftSprite()
    {
        const int s = 64;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MeteorSoft" };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = ((x + 0.5f) / s - 0.5f) * 2f, v = ((y + 0.5f) / s - 0.5f) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                a *= a;
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 64f);
    }

    /// <summary>Glowing sparks that stream off the rock and fade from yellow to red.</summary>
    private void BuildEmbers(Transform parent)
    {
        GameObject go = new GameObject("Embers");
        go.transform.SetParent(parent, false);
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop();

        var main = system.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(meteorSize * 0.03f, meteorSize * 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(flameColor, Color.white, 0.5f), flameColor);
        main.maxParticles = 300;

        var emission = system.emission;
        emission.rateOverTime = embersPerSecond;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = meteorSize * 0.42f;

        var color = system.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(flameColor, 0.4f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(fade);

        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            if (haloSprite == null) haloSprite = BuildSoftSprite();
            Material m = new Material(shader) { mainTexture = haloSprite.texture };
            renderer.sharedMaterial = m;
        }
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        system.Play();
    }

    private void CollectChunk(Vector3 point, Camera cam)
    {
        bool reachedBefore = chunks >= chunkThreshold;
        chunks += chunksPerClick;
        Report(MinigameEvent.Clicked);
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
        text.fontSize = popupTextSize;
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
            c.a *= popupOpacity * (1f - k * k);
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
