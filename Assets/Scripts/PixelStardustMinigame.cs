using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static PixelInput;

/// <summary>
/// Stardust minigame for Pixel Clicker.
///
/// Once the Meteor pixel is unlocked, every meteor pixel that shoots off has a small chance of knocking a star out of the
/// sky. The star falls into the old-pixel area, lands and waits for a while: click it to collect stardust. Enough stardust
/// (the goal below) lets the player buy the Solar Pixel in the shop (a pack with a stardust requirement).
///
/// Passive: it has no timer and no shop pack of its own (it runs as soon as the Meteor pixel is unlocked), but it has a
/// tracker row, a switch in the Toggles window and a Despawn / Spawn entry in the dev tools like every minigame.
/// Added automatically by PixelShop.
/// </summary>
public class PixelStardustMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube position, meteor pixel). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("When a star falls")]
    [Range(0f, 1f)]
    [Tooltip("Chance that a meteor pixel shooting off knocks a star down.")]
    [SerializeField] private float starChance = 0.12f;

    [Min(0f)]
    [Tooltip("Seconds after the meteor pixel leaves before the star appears at the top of the screen.")]
    [SerializeField] private float fallDelaySeconds = 0.6f;

    [Min(1)]
    [Tooltip("Most stars on screen at once; a new one is not knocked down while this many wait to be collected.")]
    [SerializeField] private int maxStars = 3;

    [Range(0.05f, 0.45f)]
    [Tooltip("The star falls this far to the left or right of the cube (fraction of the screen width), so it lands beside it. Range: closest and farthest.")]
    [SerializeField] private Vector2 sideDistance = new Vector2(0.12f, 0.3f);

    [Min(0f)]
    [Tooltip("Gravity of the star as a multiple of normal gravity (below 1 = it drifts down slowly).")]
    [SerializeField] private float gravityScale = 0.6f;

    [Min(2f)]
    [Tooltip("Seconds the star waits to be collected after landing before it fades away.")]
    [SerializeField] private float starLifetime = 25f;

    [Header("Look")]
    [Min(0.1f)]
    [Tooltip("Size of the star (world units).")]
    [SerializeField] private float starSize = 0.9f;

    [Tooltip("Colour of the star.")]
    [SerializeField] private Color starColor = new Color(1f, 0.86f, 0.3f, 1f);

    [Tooltip("Degrees per second the star spins on its face.")]
    [SerializeField] private float spinSpeed = 60f;

    [Min(0f)]
    [Tooltip("How long the glowing trail behind the falling star is (seconds). 0 = none.")]
    [SerializeField] private float trailSeconds = 0.7f;

    [Min(0f)]
    [Tooltip("Strength of the star's light (0 = no light).")]
    [SerializeField] private float lightIntensity = 1.4f;

    [Header("Collecting")]
    [Min(10f)]
    [Tooltip("The star can be clicked within this many pixels of its centre (at 1080p screen height).")]
    [SerializeField] private float clickRadiusPixels = 70f;

    [Min(1)]
    [Tooltip("Stardust gained per star.")]
    [SerializeField] private int stardustPerStar = 1;

    [Tooltip("Text that rises when a star is collected. {0} = amount.")]
    [SerializeField] private string collectedFormat = "+{0} Stardust";

    [Tooltip("Text size of that message (3D text: about 10 per world unit).")]
    [SerializeField] private float messageTextSize = 2.2f;

    [Tooltip("Colour of that message.")]
    [SerializeField] private Color messageColor = new Color(1f, 0.92f, 0.5f, 1f);

    [Min(0.1f)]
    [Tooltip("How long the message rises and fades (seconds).")]
    [SerializeField] private float messageSeconds = 1.2f;

    [Header("Solar Pixel Unlock")]
    [Min(1)]
    [Tooltip("Stardust needed in total before the Solar Pixel can be bought. The count keeps going afterwards.")]
    [SerializeField] private double stardustGoal = 10;

    [Tooltip("Runtime: stardust collected so far (saved with the game). You can type a number to test the unlock.")]
    [SerializeField] private double stardust = 0;

    [Tooltip("Title of this minigame's tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Stardust";

    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Meteor pixels sometimes knock a star out of the sky. Click the fallen star to collect stardust.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = stardust, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} stardust";

    [Header("Events")]
    [Tooltip("Fired when a star is knocked down.")]
    public UnityEvent onStarFell;

    [Tooltip("Fired when a star is collected.")]
    public UnityEvent onStarCollected;

    private class Star
    {
        public GameObject root;
        public Transform visual;
        public Rigidbody body;
        public Renderer[] renderers;
        public float age;
        public bool landed;
    }

    private readonly List<Star> stars = new List<Star>();
    private int pending; // stars announced (waiting for their delay) but not yet created
    private static Mesh starMesh;
    private static Material starMaterial;

    public override string Id => "stardust";
    public override string DisplayName => "Stardust";

    /// <summary>Runs as soon as the Meteor pixel has been unlocked (there is nothing to buy for it).</summary>
    public override bool Running => clicker != null && clicker.IsUnlocked(PixelClicker.PixelType.Meteor);

    public override void Activate() { }
    public override void Deactivate() { }

    // --- Tracker: stardust collected (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => stardust;
    public override double TrackerGoal => stardustGoal;
    public override string RequirementFormat => requirementFormat;
    public override void SetTrackerCount(double value) => stardust = System.Math.Max(0d, value);

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnEnable() => PixelClicker.FlyAwayLaunched += OnMeteorLaunched;

    private void OnDisable() => PixelClicker.FlyAwayLaunched -= OnMeteorLaunched;

    protected override void OnDespawned()
    {
        stars.Clear();
        pending = 0;
    }

    private void OnMeteorLaunched(Vector3 position)
    {
        if (clicker == null || !Running || UserDisabled) return;
        if (stars.Count + pending >= maxStars) return;
        if (Random.value >= starChance) return;
        pending++;
        StartCoroutine(StarAfterDelay(fallDelaySeconds));
    }

    [ContextMenu("Drop A Star Now")]
    public override void SpawnNow()
    {
        if (!Application.isPlaying || clicker == null) return;
        pending++;
        StartCoroutine(StarAfterDelay(0f));
    }

    private IEnumerator StarAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        pending = Mathf.Max(0, pending - 1);
        CreateStar();
    }

    // ------------------------------------------------------------------
    // The star
    // ------------------------------------------------------------------

    /// <summary>A five-pointed star lying in the XY plane (flat, drawn from both sides with vertex colours).</summary>
    private static Mesh StarMesh(Color colour)
    {
        if (starMesh != null) return starMesh;
        const int points = 5;
        Vector3[] vertices = new Vector3[points * 2 + 1];
        Color[] colours = new Color[vertices.Length];
        vertices[0] = Vector3.zero;
        colours[0] = Color.Lerp(colour, Color.white, 0.75f); // a bright core
        for (int i = 0; i < points * 2; i++)
        {
            float angle = (i / (float)(points * 2)) * Mathf.PI * 2f + Mathf.PI * 0.5f;
            float radius = i % 2 == 0 ? 0.5f : 0.21f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            colours[i + 1] = i % 2 == 0 ? colour : Color.Lerp(colour, new Color(1f, 0.55f, 0.1f, 1f), 0.4f);
        }
        List<int> triangles = new List<int>();
        for (int i = 0; i < points * 2; i++)
        {
            int a = i + 1, b = (i + 1) % (points * 2) + 1;
            triangles.Add(0); triangles.Add(a); triangles.Add(b);
        }
        starMesh = new Mesh { name = "Stardust Star" };
        starMesh.vertices = vertices;
        starMesh.colors = colours;
        starMesh.triangles = triangles.ToArray();
        starMesh.RecalculateNormals();
        starMesh.RecalculateBounds();
        return starMesh;
    }

    private Material StarMaterial()
    {
        if (starMaterial != null) return starMaterial;
        Shader shader = Shader.Find("Sprites/Default"); // unlit, double sided, vertex colours, in every pipeline
        starMaterial = shader != null ? new Material(shader) : clicker.CreateVisualMaterial(starColor, false);
        return starMaterial;
    }

    private void CreateStar()
    {
        Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null || clicker.PixelTransform == null) return;

        // It starts above the top of the screen, beside the cube (at the cube's distance), and falls onto the old pixels' floor.
        float depth = Mathf.Max(cam.nearClipPlane + 0.5f, Vector3.Dot(clicker.PixelTransform.position - cam.transform.position, cam.transform.forward));
        float cubeX = cam.WorldToViewportPoint(clicker.PixelTransform.position).x;
        float offset = Random.Range(Mathf.Min(sideDistance.x, sideDistance.y), Mathf.Max(sideDistance.x, sideDistance.y)) * (Random.value < 0.5f ? -1f : 1f);
        float vx = Mathf.Clamp(cubeX + offset, 0.08f, 0.92f);
        Vector3 start = cam.ViewportToWorldPoint(new Vector3(vx, 1.08f, depth));

        GameObject root = new GameObject("Fallen Star");
        Track(root);
        root.transform.position = start;
        root.transform.localScale = Vector3.one;

        GameObject visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * starSize;
        visual.GetComponent<MeshFilter>().sharedMesh = StarMesh(starColor);
        MeshRenderer mr = visual.GetComponent<MeshRenderer>();
        mr.sharedMaterial = StarMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.radius = starSize * 0.3f;
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.3f;
        rb.useGravity = false; // gravity is applied below so gravityScale works
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (trailSeconds > 0f)
        {
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = trailSeconds;
            trail.startWidth = starSize * 0.55f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.02f;
            trail.numCapVertices = 6;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.sharedMaterial = StarMaterial();
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(starColor, Color.white, 0.5f), 0f), new GradientColorKey(starColor, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }

        if (lightIntensity > 0f)
        {
            GameObject lightGo = new GameObject("Star Light");
            lightGo.transform.SetParent(root.transform, false);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = starColor;
            light.intensity = lightIntensity;
            light.range = starSize * 5f;
            light.shadows = LightShadows.None;
        }

        Star star = new Star { root = root, visual = visual.transform, body = rb, renderers = new Renderer[] { mr } };
        stars.Add(star);
        Report(MinigameEvent.Spawned);
        PixelAudio.Play("stardust_fall");
        onStarFell?.Invoke();
    }

    private static float Speed(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity.magnitude;
#else
        return body.velocity.magnitude;
#endif
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < stars.Count; i++)
        {
            Star s = stars[i];
            if (s.root != null && s.body != null && !s.body.isKinematic)
                s.body.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
        }
    }

    private void Update()
    {
        if (stars.Count == 0) return;
        Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;

        bool click = Time.timeScale > 0f && LeftPressed() && !PointerOverUI() && !PixelClicker.ExternalClickBlock && !PixelClicker.GodMode;
        Vector2 pointer = PointerPosition();
        float radius = clickRadiusPixels * Screen.height / 1080f;
        Star clicked = null;
        float best = float.MaxValue;

        for (int i = stars.Count - 1; i >= 0; i--)
        {
            Star s = stars[i];
            if (s.root == null) { stars.RemoveAt(i); continue; }

            s.age += Time.deltaTime;
            if (!s.landed && s.body != null && Speed(s.body) < 0.22f && s.age > 0.3f) s.landed = true;

            // Face the camera, spin on its face, pulse a little.
            float pulse = 1f + Mathf.Sin(Time.time * 4f + i) * 0.07f;
            s.visual.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * spinSpeed);
            float life = s.landed ? Mathf.Max(0f, starLifetime - (s.age - 0.3f)) : starLifetime;
            float fade = life > 1f ? 1f : Mathf.Clamp01(life); // shrinks away in the last second
            float blink = life < 4f && life > 1f ? (Mathf.Sin(Time.time * 14f) > 0f ? 1f : 0.4f) : 1f; // blinks when about to go
            s.visual.localScale = Vector3.one * (starSize * pulse * fade * blink);
            if (s.landed && life <= 0f) { Destroy(s.root); stars.RemoveAt(i); continue; }

            if (click)
            {
                Vector3 sp = cam.WorldToScreenPoint(s.root.transform.position);
                float d = Vector2.Distance(pointer, sp);
                if (sp.z > 0f && d <= radius && d < best) { best = d; clicked = s; }
            }
        }

        if (clicked != null) Collect(clicked, cam);
    }

    private void Collect(Star star, Camera cam)
    {
        Vector3 at = star.root.transform.position;
        stars.Remove(star);
        Destroy(star.root);

        stardust += stardustPerStar;
        Report(MinigameEvent.Clicked);
        PixelAudio.Play("stardust_collect");
        onStarCollected?.Invoke();
        StartCoroutine(RisingMessage(at, cam, string.Format(collectedFormat, stardustPerStar)));
    }

    /// <summary>Text that rises from where the star was and fades away, kept on screen.</summary>
    private IEnumerator RisingMessage(Vector3 start, Camera cam, string message)
    {
        GameObject go = new GameObject("Stardust Message");
        Track(go);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = messageTextSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(10f, 2f);

        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / messageSeconds);
            go.transform.position = PixelUIKit.KeepOnScreen(cam, text, start + cam.transform.up * (k * 1.2f));
            go.transform.rotation = cam.transform.rotation;
            Color c = messageColor;
            c.a *= 1f - k * k;
            text.alpha = c.a;
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
