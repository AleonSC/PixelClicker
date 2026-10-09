using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static PixelInput;

/// <summary>
/// Ultra Pad minigame for Pixel Clicker.
///
/// Every so often a coloured square pad appears on the ground at a random spot. It wants old pixels of ONE pixel
/// type - the one whose colour it shows. Get old pixels onto it (they fall, you can fan or drag them there):
///   - a pixel of the right type is taken in, and has a chance to give you an "Ultra" version of that pixel;
///   - a pixel of any other type is taken too, but its value is subtracted from that pixel type's currency.
/// Ultra pixels are counted per pixel type (hover a pixel's entry in the inventory to see them) and are saved; they will
/// be used for upgrades later.
///
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelPadMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (for the camera, the old pixels and the pixel counts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when the Ultra Pad is bought.)")]
    [SerializeField] private bool running = false;

    [Header("Ultra Pixels")]
    [Range(0f, 1f)]
    [Tooltip("Chance that each CORRECT pixel gives you an Ultra version of it (0.1 = 10%).")]
    [SerializeField] private float ultraChance = 0.1f;

    [Min(1)]
    [Tooltip("The goal shown on the tracker's progress bar (total Ultra pixels of every type; the count keeps going).")]
    [SerializeField] private double ultraGoal = 10;

    [Tooltip("Title of the tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Ultra Pixels";

    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Feed the pad the pixels it wants for a chance at Ultra pixels. Hover a pixel in the inventory to see its Ultras.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = Ultra pixels, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} Ultra pixels";

    [Header("When it appears")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before the first pad.")]
    [SerializeField] private float firstPadDelay = 15f;

    [Min(1f)]
    [Tooltip("Shortest wait between pads (seconds), counted from when the last one went away.")]
    [SerializeField] private float minInterval = 45f;

    [Min(1f)]
    [Tooltip("Longest wait between pads (seconds).")]
    [SerializeField] private float maxInterval = 90f;

    [Min(1f)]
    [Tooltip("How long a pad stays before it disappears (seconds).")]
    [SerializeField] private float padSeconds = 30f;

    [Header("The Pad")]
    [Min(0.5f)]
    [Tooltip("Width of the square pad (world units).")]
    [SerializeField] private float padSize = 2.6f;

    [Min(0.2f)]
    [Tooltip("How far above the pad an old pixel still counts as 'on' it (world units).")]
    [SerializeField] private float captureHeight = 1.2f;

    [Tooltip("The pad appears to the LEFT or RIGHT of the clicker cube, between the cube and the screen edge. " +
             "This is how far from the cube's centre (as a fraction of the screen's width) the pad's area starts.")]
    [Range(0.05f, 0.45f)]
    [SerializeField] private float cubeClearance = 0.14f;

    [Tooltip("How far in from the screen edge the pad's area ends (fraction of the screen's width).")]
    [Range(0f, 0.3f)]
    [SerializeField] private float screenEdgeMargin = 0.07f;

    [Tooltip("How high up the screen the pad can appear (viewport fractions): lowest and highest.")]
    [SerializeField] private Vector2 heightRange = new Vector2(0.15f, 0.5f);

    [Tooltip("Layers that count as ground when picking a spot.")]
    [SerializeField] private LayerMask floorLayers = ~0;

    [Tooltip("If no ground is under the chosen spot, the pad goes on a flat plane at this height instead.")]
    [SerializeField] private float fallbackFloorY = 0f;

    [Tooltip("Colour of the pad's frame.")]
    [SerializeField] private Color frameColor = new Color(0.12f, 0.12f, 0.14f, 1f);

    [Min(0f)]
    [Tooltip("How much the pad glows (so it is easy to spot).")]
    [SerializeField] private float padGlow = 0.6f;

    [Tooltip("Text at the top of the pad. {0} = the pixel type in capitals.")]
    [SerializeField] private string nameText = "{0}";

    [Min(0.3f)]
    [Tooltip("Size of that text (3D text: about 10 per world unit). It is written flat on the pad.")]
    [SerializeField] private float labelTextSize = 2f;

    [Range(0f, 0.95f)]
    [Tooltip("Where the text sits on the pad: 0 = the middle, 1 = the pad's far edge (the top of it on screen).")]
    [SerializeField] private float labelInset = 0.58f;

    [Tooltip("Timer text at the bottom of the pad. {0} = seconds left.")]
    [SerializeField] private string timerText = "{0}s";

    [Min(0.3f)]
    [Tooltip("Size of the timer text (3D text: about 10 per world unit). Written flat on the pad.")]
    [SerializeField] private float timerTextSize = 3.4f;

    [Range(0f, 0.95f)]
    [Tooltip("Where the timer sits on the pad: 0 = the middle, 1 = the pad's near edge (the bottom of it on screen).")]
    [SerializeField] private float timerInset = 0.55f;

    [Header("Feedback")]
    [Tooltip("Text when a correct pixel is taken.")]
    [SerializeField] private string correctText = "+";

    [Tooltip("Text when you get an Ultra. {0} = pixel name.")]
    [SerializeField] private string ultraFormat = "ULTRA {0}!";

    [Tooltip("Text when a wrong pixel is taken. {0} = amount lost, {1} = pixel name.")]
    [SerializeField] private string wrongFormat = "-{0} {1}";

    [Tooltip("Colour of the correct-pixel text.")]
    [SerializeField] private Color correctColor = new Color(0.6f, 1f, 0.65f, 1f);

    [Tooltip("Colour of the Ultra text.")]
    [SerializeField] private Color ultraColor = new Color(1f, 0.85f, 0.25f, 1f);

    [Tooltip("Colour of the wrong-pixel text.")]
    [SerializeField] private Color wrongColor = new Color(1f, 0.4f, 0.35f, 1f);

    [Min(0.5f)]
    [Tooltip("Size of the rising text (3D text: about 10 per world unit).")]
    [SerializeField] private float messageFontSize = 4.5f;

    [Min(0.1f)]
    [Tooltip("How long the rising text lasts (seconds).")]
    [SerializeField] private float messageSeconds = 1.1f;

    [Header("Events")]
    [Tooltip("Fired when a pad appears.")]
    public UnityEvent onPadAppeared;

    [Tooltip("Fired when you receive an Ultra pixel.")]
    public UnityEvent onUltraGained;

    private float spawnTimer;
    private bool padActive;

    public override bool Busy => padActive;

    protected override void OnDespawned()
    {
        padActive = false;
        padPulling = false;
        padRoot = null;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    public override string Id => "pad";
    public override string DisplayName => "Ultra Pad";
    public override bool Running => running;

    // --- Tracker: Ultra pixels ever earned (spending them on boosts does not lower it; what you currently hold lives on the pixel types) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => clicker != null ? clicker.UltraEarned : 0d;
    public override void SetTrackerCount(double value) { if (clicker != null) clicker.SetUltraEarned((long)System.Math.Floor(value)); }
    public override double TrackerGoal => ultraGoal;
    public override string RequirementFormat => requirementFormat;

    /// <summary>True when the inventory should show Ultra counts in its tooltips. Ultra pixels now come from every minigame and
    /// the Inventory always lists them, so this is always on (even on a new game).</summary>
    public static bool UltraVisible => true;

    protected override void Awake()
    {
        base.Awake();
        clicker = clicker != null ? clicker : PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelPadMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (startRunning) running = true;
        spawnTimer = firstPadDelay;
    }

    private void Update()
    {
        if (!running || padActive) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(PadRoutine());
            else spawnTimer = PixelMinigameLimits.RetrySeconds; // too many minigames running right now
        }
    }

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstPadDelay;
    }

    public override void Deactivate() => running = false;

    /// <summary>Puts a pad down right now (right-click the component &gt; Spawn Pad Now).</summary>
    [ContextMenu("Spawn Pad Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !padActive) StartCoroutine(PadRoutine());
    }

    // ------------------------------------------------------------------
    // Picking what and where
    // ------------------------------------------------------------------

    /// <summary>A random unlocked pixel type, or -1.</summary>
    private int PickTargetTier()
    {
        System.Collections.Generic.List<int> candidates = new System.Collections.Generic.List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
            if (clicker.Tiers[i].unlocked) candidates.Add(i);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    private bool TryPickSpot(Camera cam, out Vector3 point)
    {
        point = Vector3.zero;
        // Beside the cube: pick the left or right side, then a spot between the cube and that screen edge.
        float cubeX = 0.5f;
        if (clicker.PixelTransform != null) cubeX = cam.WorldToViewportPoint(clicker.PixelTransform.position).x;
        bool left = Random.value < 0.5f;
        float xNear = left ? cubeX - cubeClearance : cubeX + cubeClearance;
        float xFar = left ? screenEdgeMargin : 1f - screenEdgeMargin;
        float xMin = Mathf.Clamp01(Mathf.Min(xNear, xFar)), xMax = Mathf.Clamp01(Mathf.Max(xNear, xFar));

        // Try several spots; take the first where the whole pad (arrows, model, label) is inside the view, else the least cut off.
        bool found = false;
        float bestCut = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector3 viewport = new Vector3(
                Random.Range(xMin, xMax),
                Random.Range(Mathf.Min(heightRange.x, heightRange.y), Mathf.Max(heightRange.x, heightRange.y)), 0f);
            Ray ray = cam.ViewportPointToRay(viewport);

            Vector3 candidate = Vector3.zero;
            bool ok = false;
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, floorLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a2, b2) => a2.distance.CompareTo(b2.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
                if (clicker.PixelTransform != null && hit.transform.IsChildOf(clicker.PixelTransform)) continue;
                if (hit.normal.y < 0.5f) continue;
                candidate = hit.point;
                ok = true;
                break;
            }
            if (!ok)
            {
                Plane floor = new Plane(Vector3.up, new Vector3(0f, fallbackFloorY, 0f));
                if (floor.Raycast(ray, out float enter)) { candidate = ray.GetPoint(enter); ok = true; }
            }
            if (!ok) continue;

            float cut = CutOff(cam, candidate);
            if (cut < bestCut) { bestCut = cut; bestPoint = candidate; found = true; }
            if (cut <= 0f) break;
        }
        point = bestPoint;
        return found;
    }

    /// <summary>How far (viewport fractions, summed) the pad's footprint, arrows, model and label would stick out of the view. 0 = fully visible.</summary>
    private float CutOff(Camera cam, Vector3 point)
    {
        float bars = PixelHud.Instance != null
            ? PixelHud.Instance.RawBarHeight * Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f) / Mathf.Max(1, Screen.height) : 0f;
        const float margin = 0.02f;
        float reach = padSize * 0.5f + (showArrows ? arrowTravel + arrowLength * 0.6f : 0.1f);


        Vector3[] checks =
        {
            point + new Vector3(reach, 0f, reach), point + new Vector3(-reach, 0f, reach),
            point + new Vector3(reach, 0f, -reach), point + new Vector3(-reach, 0f, -reach),
            point + Vector3.up * (modelLift + modelEdge),
        };
        float cut = 0f;
        foreach (Vector3 w in checks)
        {
            Vector3 v = cam.WorldToViewportPoint(w);
            if (v.z <= 0f) { cut += 1f; continue; }
            cut += Mathf.Max(0f, margin - v.x) + Mathf.Max(0f, v.x - (1f - margin));
            cut += Mathf.Max(0f, bars + margin - v.y) + Mathf.Max(0f, v.y - (1f - bars - margin));
        }
        return cut;
    }

    // ------------------------------------------------------------------
    // The pad
    [Header("Pad Look")]
    [Tooltip("Arrows around the OUTSIDE of the pad that drift inwards toward its border, showing it pulls pixels in.")]
    [SerializeField] private bool showArrows = true;

    [Range(1, 6)]
    [Tooltip("Arrows on each side of the pad.")]
    [SerializeField] private int arrowsEachSide = 4;

    [Min(0.05f)]
    [Tooltip("Size of an arrow (world units).")]
    [SerializeField] private float arrowLength = 0.4f;

    [Min(0.1f)]
    [Tooltip("How far outside the pad's edge the arrows start before drifting in (world units).")]
    [SerializeField] private float arrowTravel = 1.0f;

    [Min(0.05f)]
    [Tooltip("How fast the arrows drift inwards (cycles per second).")]
    [SerializeField] private float arrowSpeed = 0.45f;

    [Range(0f, 1f)]
    [Tooltip("How see-through the arrows are at their brightest.")]
    [SerializeField] private float arrowOpacity = 0.85f;

    [Tooltip("Show a spinning model of the wanted pixel (drawn with that pixel's current look) floating above the pad.")]
    [SerializeField] private bool showModel = true;

    [Min(0.1f)]
    [Tooltip("Edge length of the model (world units).")]
    [SerializeField] private float modelEdge = 0.55f;

    [Min(0f)]
    [Tooltip("How high above the pad the model sits (world units). Low = it rests on the pad.")]
    [SerializeField] private float modelLift = 0.42f;

    [Tooltip("Spin speed of the model (degrees per second, around the up axis).")]
    [SerializeField] private float modelSpin = 45f;

    [Header("Pull")]
    [Min(0f)]
    [Tooltip("How strongly the pad pulls old pixels toward its middle (acceleration). Gentle: a black hole is far stronger. 0 = no pull.")]
    [SerializeField] private float pullAcceleration = 8f;

    [Min(0f)]
    [Tooltip("How far beyond the pad's edge the pull reaches (world units). It fades with distance.")]
    [SerializeField] private float pullRange = 1.4f;

    [Min(0.2f)]
    [Tooltip("Old pixels higher than this above the pad are not pulled (world units).")]
    [SerializeField] private float pullHeight = 2f;

    // ------------------------------------------------------------------

    private Transform padRoot;
    private bool padPulling;
    private static Mesh arrowMesh;
    private static Material arrowMaterial;

    /// <summary>A small flat dart pointing along +Z (lying on the pad).</summary>
    private static Mesh ArrowMesh()
    {
        if (arrowMesh != null) return arrowMesh;
        arrowMesh = new Mesh { name = "Pad Arrow" };
        // A chevron (two thick arms meeting at a tip), flat, pointing +Z.
        arrowMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.3f),  // 0 left arm, outer
            new Vector3(0f, 0f, 0.2f),      // 1 tip, outer
            new Vector3(0.5f, 0f, -0.3f),   // 2 right arm, outer
            new Vector3(-0.5f, 0f, -0.58f), // 3 left arm, inner
            new Vector3(0f, 0f, -0.08f),    // 4 tip, inner
            new Vector3(0.5f, 0f, -0.58f),  // 5 right arm, inner
        };
        arrowMesh.triangles = new[] { 0, 1, 4, 0, 4, 3, 1, 2, 5, 1, 5, 4 };
        Color[] colours = { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
        arrowMesh.colors = colours;
        arrowMesh.RecalculateNormals();
        arrowMesh.RecalculateBounds();
        return arrowMesh;
    }

    private Material ArrowMaterial(Color colour)
    {
        if (arrowMaterial != null) return arrowMaterial;
        Shader shader = PixelShaders.SpriteDefault(); // unlit, alpha blended, vertex colour * _Color in every pipeline
        arrowMaterial = shader != null ? new Material(shader) : clicker.CreateVisualMaterial(colour, true);
        return arrowMaterial;
    }

    /// <summary>The animated extras of one pad: drifting arrows and the floating model.</summary>
    private class PadVisual
    {
        public struct Arrow { public Transform tf; public Renderer renderer; public int side; public float lateral, phase; }
        public System.Collections.Generic.List<Arrow> arrows = new System.Collections.Generic.List<Arrow>();
        public Transform model;
        public Color arrowColour;
        public float half, size, speed, opacity, modelLift, modelSpin, travel;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public void Animate(float time)
        {
            foreach (Arrow a in arrows)
            {
                if (a.tf == null) continue;
                float t = Mathf.Repeat(time * speed + a.phase, 1f);
                float angle = a.side * 90f * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 lateral = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
                float from = half + travel, to = half + size * 0.55f; // outside the pad, drifting in toward its border
                a.tf.localPosition = outward * Mathf.Lerp(from, to, t) + lateral * a.lateral + Vector3.up * 0.085f;
                a.tf.localRotation = Quaternion.LookRotation(-outward, Vector3.up); // points inwards

                Color c = arrowColour;
                c.a = Mathf.Sin(t * Mathf.PI) * opacity;
                block.SetColor("_Color", c);
                a.renderer.SetPropertyBlock(block);
            }

            if (model != null)
            {
                model.localPosition = new Vector3(0f, modelLift + Mathf.Sin(time * 1.6f) * 0.03f, 0f);
                model.localRotation = Quaternion.Euler(0f, time * modelSpin, 0f);
            }
        }
    }

    private PadVisual BuildPadVisual(Transform root, int target, Color colour)
    {
        PadVisual v = new PadVisual
        {
            half = padSize * 0.5f, travel = arrowTravel, size = arrowLength, speed = arrowSpeed, opacity = arrowOpacity,
            modelLift = modelLift, modelSpin = modelSpin,
            arrowColour = Color.Lerp(colour, Color.white, 0.55f),
        };

        if (showArrows)
        {
            Material mat = ArrowMaterial(colour);
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < arrowsEachSide; i++)
                {
                    GameObject go = new GameObject("Arrow");
                    go.transform.SetParent(root, false);
                    go.transform.localScale = Vector3.one * arrowLength;
                    go.AddComponent<MeshFilter>().sharedMesh = ArrowMesh();
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    float spread = padSize * 0.95f;
                    float lateral = arrowsEachSide == 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(arrowsEachSide - 1));
                    v.arrows.Add(new PadVisual.Arrow
                    {
                        tf = go.transform, renderer = mr, side = side, lateral = lateral,
                        phase = (i * 0.37f + side * 0.21f) % 1f, // staggered so they don't all pulse together
                    });
                }
        }

        if (showModel)
        {
            GameObject model = clicker.CreateDisplayPixel(target, root, modelEdge);
            if (model != null) v.model = model.transform;
        }
        return v;
    }

    private void FixedUpdate()
    {
        if (!padPulling || padRoot == null || pullAcceleration <= 0f) return;

        float half = padSize * 0.5f;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody body = list[i];
            if (body == null || body.isKinematic) continue;

            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || clicker.IsFlyingPixel(body)) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && despawn.Held) continue; // the player is carrying it

            Vector3 local = padRoot.InverseTransformPoint(body.position);
            float edge = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.z)); // square distance from the middle
            if (edge > half + pullRange || local.y < -0.3f || local.y > pullHeight) continue;

            Vector3 toCentre = padRoot.TransformDirection(new Vector3(-local.x, 0f, -local.z));
            if (toCentre.sqrMagnitude < 0.0001f) continue;
            float fade = 1f - Mathf.Clamp01((edge - half) / Mathf.Max(0.01f, pullRange)); // strongest on the pad, fading out
            body.AddForce(toCentre.normalized * pullAcceleration * fade, ForceMode.Acceleration);
        }
    }

    private IEnumerator PadRoutine()
    {
        padActive = true;

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        int target = PickTargetTier();
        if (cam == null || target < 0 || !TryPickSpot(cam, out Vector3 point))
        {
            spawnTimer = 5f; // try again shortly
            padActive = false;
            yield break;
        }

        onPadAppeared?.Invoke();
        Report(MinigameEvent.Spawned);

        PixelClicker.PixelTier tier = clicker.Tiers[target];
        Color colour = tier.UIColor;

        GameObject root = new GameObject("Ultra Pad");
        Track(root);
        root.transform.position = point;

        GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
        frame.name = "Frame";
        Destroy(frame.GetComponent<Collider>());
        frame.transform.SetParent(root.transform, false);
        frame.transform.localScale = new Vector3(padSize * 1.12f, 0.05f, padSize * 1.12f);
        frame.transform.localPosition = new Vector3(0f, 0.025f, 0f);
        Material frameMat = clicker.CreateVisualMaterial(frameColor, false);
        if (frameMat != null) frame.GetComponent<Renderer>().sharedMaterial = frameMat;

        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
        face.name = "Face";
        Destroy(face.GetComponent<Collider>());
        face.transform.SetParent(root.transform, false);
        face.transform.localScale = new Vector3(padSize, 0.06f, padSize);
        face.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        Color faceColour = Color.Lerp(colour, Color.black, 0.55f); // darker, so the arrows and the model stand out
        Material faceMat = clicker.CreateVisualMaterial(faceColour, false);
        if (faceMat != null)
        {
            faceMat.EnableKeyword("_EMISSION");
            faceMat.SetColor("_EmissionColor", colour * padGlow * 0.5f);
            face.GetComponent<Renderer>().sharedMaterial = faceMat;
        }

        // A bright glowing border strip around the face.
        Material borderMat = clicker.CreateVisualMaterial(colour, false);
        if (borderMat != null)
        {
            borderMat.EnableKeyword("_EMISSION");
            borderMat.SetColor("_EmissionColor", colour * padGlow * 1.6f);
        }
        float strip = padSize * 0.035f;
        for (int side = 0; side < 4; side++)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Border";
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(root.transform, false);
            bool alongX = side % 2 == 0;
            float offset = (padSize * 0.5f - strip * 0.5f) * (side < 2 ? 1f : -1f);
            bar.transform.localScale = alongX ? new Vector3(padSize, 0.065f, strip) : new Vector3(strip, 0.065f, padSize);
            bar.transform.localPosition = alongX ? new Vector3(0f, 0.045f, offset) : new Vector3(offset, 0.045f, 0f);
            if (borderMat != null) bar.GetComponent<Renderer>().sharedMaterial = borderMat;
        }

        PadVisual visual = BuildPadVisual(root.transform, target, colour);
        float animTime = 0f;

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
        label.fontSize = labelTextSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.Lerp(colour, Color.white, 0.4f);
        label.overflowMode = TextOverflowModes.Overflow;
        if (clicker.UIFont != null) label.font = clicker.UIFont;
        label.rectTransform.sizeDelta = new Vector2(padSize * 4f, 0.8f);

        // The text floats above the pixel preview, facing the camera.
        // The text is written flat on the pad, near its far (top-on-screen) edge, upright for the camera, the timer centred underneath.
        Vector3 far = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (far.sqrMagnitude < 0.0001f) far = Vector3.forward;
        far = Mathf.Abs(far.x) > Mathf.Abs(far.z) ? new Vector3(Mathf.Sign(far.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(far.z)); // parallel to the pad's edges
        labelGo.transform.position = point + far * (padSize * 0.5f * labelInset) + Vector3.up * 0.09f;
        labelGo.transform.rotation = Quaternion.LookRotation(Vector3.down, far);

        // The timer: bigger, flat on the pad near its near (bottom-on-screen) edge.
        GameObject timerGo = new GameObject("Timer");
        timerGo.transform.SetParent(root.transform, false);
        TextMeshPro timerLabel = timerGo.AddComponent<TextMeshPro>();
        timerLabel.fontSize = timerTextSize;
        timerLabel.fontStyle = FontStyles.Bold;
        timerLabel.alignment = TextAlignmentOptions.Center;
        timerLabel.color = Color.white;
        timerLabel.overflowMode = TextOverflowModes.Overflow;
        if (clicker.UIFont != null) timerLabel.font = clicker.UIFont;
        timerLabel.rectTransform.sizeDelta = new Vector2(padSize, 1.2f);
        timerGo.transform.position = point - far * (padSize * 0.5f * timerInset) + Vector3.up * 0.09f;
        timerGo.transform.rotation = Quaternion.LookRotation(Vector3.down, far);

        // Grow in.
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            animTime += Time.deltaTime;
            visual.Animate(animTime);
            root.transform.localScale = new Vector3(1f, 1f, 1f) * Mathf.SmoothStep(0f, 1f, t / 0.35f);
            yield return null;
        }
        root.transform.localScale = Vector3.one;
        padRoot = root.transform;
        padPulling = true;

        // Wait for pixels.
        float left = padSeconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;
            animTime += Time.deltaTime;
            visual.Animate(animTime);
            label.text = string.Format(nameText, tier.displayName.ToUpperInvariant());
            timerLabel.text = string.Format(timerText, Mathf.CeilToInt(Mathf.Max(0f, left)));
            TakePixels(root.transform, target, cam);
            yield return null;
        }

        // Shrink away.
        padPulling = false;
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            animTime += Time.deltaTime;
            visual.Animate(animTime);
            root.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t / 0.4f);
            yield return null;
        }
        padRoot = null;
        Destroy(root);

        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        padActive = false;
    }

    /// <summary>Takes in every old pixel that is on the pad: right ones may give an Ultra, wrong ones cost currency.</summary>
    private void TakePixels(Transform pad, int target, Camera cam)
    {
        var list = clicker.OldPixels;
        float half = padSize * 0.5f;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            Rigidbody body = list[i];
            if (body == null || clicker.IsFlyingPixel(body)) continue; // a meteor must be caught (grabbed while time is slowed) first

            Vector3 local = pad.InverseTransformPoint(body.position);
            if (Mathf.Abs(local.x) > half || Mathf.Abs(local.z) > half || local.y < -0.3f || local.y > captureHeight) continue;

            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.ReleaseOldPixel(body, false)) continue;

            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.Begin(); // it shrinks away into the pad

            Vector3 at = body.position;
            if (info.tierIndex == target)
            {
                Report(MinigameEvent.Clicked);
                if (Random.value < ultraChance)
                {
                    clicker.AddUltra(target, 1);
                    onUltraGained?.Invoke();
                    PixelAudio.Play("pad_ultra");
                    PixelUltraFx.Play(clicker, target, at, text: false, sound: false); // its own rising text and sound already play
                    StartCoroutine(RisingMessage(at, cam, string.Format(ultraFormat, clicker.Tiers[target].displayName), ultraColor, 1.4f));
                }
                else
                {
                    StartCoroutine(RisingMessage(at, cam, correctText, correctColor, 1f));
                }
            }
            else
            {
                double lost = clicker.RemoveCurrency(info.tierIndex, info.amount);
                PixelAudio.Play("pad_wrong");
                string name = info.tierIndex >= 0 && info.tierIndex < clicker.Tiers.Length ? clicker.Tiers[info.tierIndex].displayName : "";
                StartCoroutine(RisingMessage(at, cam, string.Format(wrongFormat, PixelClicker.FormatNumber(lost), name), wrongColor, 1f));
            }
        }
    }

    private IEnumerator RisingMessage(Vector3 start, Camera cam, string message, Color color, float sizeScale)
    {
        GameObject go = new GameObject("Pad Message");
        Track(go);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = messageFontSize * sizeScale;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Overflow;
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(20f, 3f);

        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / messageSeconds);
            go.transform.position = start + cam.transform.up * (0.5f + k * 1.2f) - cam.transform.forward * 0.4f;
            go.transform.rotation = cam.transform.rotation;
            Color c = color;
            c.a *= 1f - k * k;
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
