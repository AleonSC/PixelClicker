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

    [Tooltip("Where on the screen a pad can appear (viewport fractions): lowest corner.")]
    [SerializeField] private Vector2 screenAreaMin = new Vector2(0.15f, 0.12f);

    [Tooltip("Where on the screen a pad can appear (viewport fractions): highest corner.")]
    [SerializeField] private Vector2 screenAreaMax = new Vector2(0.85f, 0.5f);

    [Tooltip("Layers that count as ground when picking a spot.")]
    [SerializeField] private LayerMask floorLayers = ~0;

    [Tooltip("If no ground is under the chosen spot, the pad goes on a flat plane at this height instead.")]
    [SerializeField] private float fallbackFloorY = 0f;

    [Tooltip("Colour of the pad's frame.")]
    [SerializeField] private Color frameColor = new Color(0.12f, 0.12f, 0.14f, 1f);

    [Min(0f)]
    [Tooltip("How much the pad glows (so it is easy to spot).")]
    [SerializeField] private float padGlow = 0.6f;

    [Tooltip("Text above the pad. {0} = pixel name, {1} = seconds left.")]
    [SerializeField] private string labelFormat = "Feed me: {0}   {1}s";

    [Min(0.5f)]
    [Tooltip("Size of that text (3D text: about 10 per world unit).")]
    [SerializeField] private float labelFontSize = 4f;

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

    public override string Id => "pad";
    public override string DisplayName => "Ultra Pad";
    public override bool Running => running;

    // --- Tracker: the total of Ultra pixels (the real counts live on the pixel types and are saved with them) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => clicker != null ? clicker.TotalUltra : 0d;
    public override double TrackerGoal => ultraGoal;
    public override string RequirementFormat => requirementFormat;

    /// <summary>True when the inventory should show Ultra counts in its tooltips (the minigame is bought or you own some).</summary>
    public static bool UltraVisible
    {
        get
        {
            PixelPadMinigame pad = Find("pad") as PixelPadMinigame;
            return pad != null && (pad.running || (pad.clicker != null && pad.clicker.TotalUltra > 0));
        }
    }

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
        if (spawnTimer <= 0f) StartCoroutine(PadRoutine());
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
    // The pad
    // ------------------------------------------------------------------

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
        Material faceMat = clicker.CreateVisualMaterial(colour, false);
        if (faceMat != null)
        {
            faceMat.EnableKeyword("_EMISSION");
            faceMat.SetColor("_EmissionColor", colour * padGlow);
            face.GetComponent<Renderer>().sharedMaterial = faceMat;
        }

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, padSize * 0.55f, 0f);
        TextMeshPro label = labelGo.AddComponent<TextMeshPro>();
        label.fontSize = labelFontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.Lerp(colour, Color.white, 0.4f);
        label.overflowMode = TextOverflowModes.Overflow;
        if (clicker.UIFont != null) label.font = clicker.UIFont;
        label.rectTransform.sizeDelta = new Vector2(padSize * 4f, 1f);

        // Grow in.
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            root.transform.localScale = new Vector3(1f, 1f, 1f) * Mathf.SmoothStep(0f, 1f, t / 0.35f);
            labelGo.transform.rotation = cam.transform.rotation;
            yield return null;
        }
        root.transform.localScale = Vector3.one;

        // Wait for pixels.
        float left = padSeconds;
        while (left > 0f)
        {
            left -= Time.deltaTime;
            label.text = string.Format(labelFormat, tier.displayName, Mathf.CeilToInt(Mathf.Max(0f, left)));
            labelGo.transform.rotation = cam.transform.rotation;
            TakePixels(root.transform, target, cam);
            yield return null;
        }

        // Shrink away.
        t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            root.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t / 0.4f);
            yield return null;
        }
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
            if (body == null) continue;

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
