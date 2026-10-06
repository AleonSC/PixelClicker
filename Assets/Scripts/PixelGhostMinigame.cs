using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ghost minigame for Pixel Clicker.
///
/// Once bought in the shop's Minigames tab, a faint see-through ghost cube floats across the screen every
/// so often, wobbling and bouncing as it goes. Click it before it leaves to get a random pixel potion buff
/// (only that pixel type spawns for a while, like drinking a potion - but free).
///
/// Everything is adjustable: how often it appears, how fast it crosses, how see-through it is, how it wobbles.
/// Add this to any GameObject (e.g. the cube). PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelGhostMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (for the camera and the cube's position). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Gives the buff. Found automatically (or added) if left empty.")]
    [SerializeField] private PixelConsumables consumables;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when the Ghost Hunt is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it appears")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame starts before the first ghost can appear.")]
    [SerializeField] private float firstGhostDelay = 10f;

    [Min(1f)]
    [Tooltip("Shortest wait between ghosts (seconds).")]
    [SerializeField] private float minInterval = 45f;

    [Min(1f)]
    [Tooltip("Longest wait between ghosts (seconds). Each wait is random between the shortest and longest.")]
    [SerializeField] private float maxInterval = 90f;

    [Header("Movement")]
    [Min(0.5f)]
    [Tooltip("Seconds the ghost takes to cross the screen. Lower = faster = harder to click.")]
    [SerializeField] private float crossSeconds = 8f;

    [Range(0f, 1f)]
    [Tooltip("Lowest point of the ghost's path, as a fraction of screen height (0 = bottom).")]
    [SerializeField] private float minHeight = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Highest point of the ghost's path, as a fraction of screen height (1 = top).")]
    [SerializeField] private float maxHeight = 0.8f;

    [Range(0.05f, 1.5f)]
    [Tooltip("How far in front of the cube the ghost floats, as a fraction of the camera's distance to the cube. " +
             "Below 1 = in front of the cube (so clicking the ghost doesn't click the cube).")]
    [SerializeField] private float depthFraction = 0.8f;

    [Header("Wobble / Bounce")]
    [Range(0f, 0.3f)]
    [Tooltip("How far the ghost drifts up and down in a smooth wave (fraction of screen height).")]
    [SerializeField] private float wobbleAmplitude = 0.05f;

    [Min(0f)]
    [Tooltip("Speed of that wave (cycles per second).")]
    [SerializeField] private float wobbleFrequency = 0.8f;

    [Range(0f, 0.3f)]
    [Tooltip("Height of the little hops, like a ghost bouncing along (fraction of screen height).")]
    [SerializeField] private float bounceHeight = 0.04f;

    [Min(0f)]
    [Tooltip("Hops per second.")]
    [SerializeField] private float bounceFrequency = 1.2f;

    [Range(0f, 45f)]
    [Tooltip("How far the cube tilts side to side while floating (degrees).")]
    [SerializeField] private float swayDegrees = 18f;

    [Range(0f, 0.5f)]
    [Tooltip("How much the cube squashes and stretches with each hop.")]
    [SerializeField] private float squashAmount = 0.12f;

    [Header("Look")]
    [Min(0.05f)]
    [Tooltip("Size of the ghost cube (world units, at the cube's distance).")]
    [SerializeField] private float ghostSize = 1.1f;

    [Tooltip("Colour of the ghost.")]
    [SerializeField] private Color ghostColor = new Color(0.85f, 0.95f, 1f, 1f);

    [Range(0.02f, 1f)]
    [Tooltip("How visible the ghost is. Low = faint and hard to see.")]
    [SerializeField] private float ghostOpacity = 0.14f;

    [Range(0f, 0.5f)]
    [Tooltip("The ghost fades in and out over this fraction of its trip (at the screen edges).")]
    [SerializeField] private float edgeFade = 0.1f;

    [Min(1f)]
    [Tooltip("The clickable area is the ghost's size times this, so a faint ghost is still fair to click.")]
    [SerializeField] private float clickSizeMultiplier = 1.4f;

    [Header("Reward")]
    [Min(0.1f)]
    [Tooltip("The buff lasts the potion's normal duration times this.")]
    [SerializeField] private float buffDurationMultiplier = 1f;

    [Tooltip("Text that rises from the ghost when it is caught. {0} = the potion that was granted.")]
    [SerializeField] private string caughtFormat = "Ghost buff: {0}!";

    [Tooltip("Text size of that message (3D text: about 10 per world unit).")]
    [SerializeField] private float caughtFontSize = 5f;

    [Tooltip("Colour of that message.")]
    [SerializeField] private Color caughtColor = Color.white;

    [Min(0.1f)]
    [Tooltip("How long the message rises and fades (seconds).")]
    [SerializeField] private float caughtSeconds = 1.4f;

    [Header("Ghost Pixel Unlock")]
    [Min(1)]
    [Tooltip("Ghosts that must be caught in total before the Ghost Pixel can be bought. The count keeps going afterwards.")]
    [SerializeField] private double ghostThreshold = 10;

    [Tooltip("Runtime: ghosts caught so far (saved with the game). You can type a number to test the unlock.")]
    [SerializeField] private double ghostsCaught = 0;

    [Tooltip("Title of this minigame's tracker row in the shop.")]
    [SerializeField] private string trackerTitle = "Ghosts Caught";

    [TextArea(1, 3)]
    [Tooltip("Description of the tracker row.")]
    [SerializeField] private string trackerDescription = "Click ghosts as they float past. Catch enough to unlock the Ghost Pixel.";

    [Tooltip("Text on a locked shop pack that needs the goal. {0} = ghosts caught, {1} = goal.")]
    [SerializeField] private string requirementFormat = "Requires: {0} / {1} ghosts caught";

    [Header("Sound / Events")]
    [Tooltip("Sound played when a ghost is caught.")]
    [SerializeField] private AudioClip caughtSound;

    [Range(0f, 1f)]
    [Tooltip("Caught sound volume.")]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Fired when a ghost appears.")]
    public UnityEvent onGhostAppeared;

    [Tooltip("Fired when a ghost is clicked. Passes the index of the potion buff given.")]
    public UnityEvent<int> onGhostCaught;

    [Tooltip("Fired when a ghost floats away uncaught.")]
    public UnityEvent onGhostMissed;

    [Tooltip("Fired once, when the number of ghosts caught first reaches the threshold.")]
    public UnityEvent onThresholdReached;

    private float spawnTimer;
    private bool ghostActive;
    private Material ghostMaterial;
    private AudioSource audioSource;

    public override string Id => "ghost";

    public override bool Running => running;

    // --- Tracker: ghosts caught (see PixelMinigame) ---
    public override bool HasTracker => true;
    public override string TrackerTitle => trackerTitle;
    public override string TrackerDescription => trackerDescription;
    public override double TrackerCount => ghostsCaught;
    public override double TrackerGoal => ghostThreshold;
    public override string RequirementFormat => requirementFormat;
    public override void SetTrackerCount(double value) => ghostsCaught = System.Math.Max(0d, value);

    /// <summary>Ghosts caught so far.</summary>
    public double GhostsCaught => ghostsCaught;

    /// <summary>Ghosts needed to unlock the Ghost Pixel.</summary>
    public double GhostThreshold => ghostThreshold;

    /// <summary>True once enough ghosts have been caught.</summary>
    public bool ThresholdReached => ghostsCaught >= ghostThreshold;

    /// <summary>Sets the count (used when loading a save).</summary>
    public void SetGhostsCaught(double value) => ghostsCaught = System.Math.Max(0d, value);

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null)
        {
#if UNITY_2023_1_OR_NEWER
            clicker = FindFirstObjectByType<PixelClicker>();
#else
            clicker = FindObjectOfType<PixelClicker>();
#endif
        }
        if (clicker == null)
        {
            Debug.LogError("PixelGhostMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (consumables == null)
        {
#if UNITY_2023_1_OR_NEWER
            consumables = FindFirstObjectByType<PixelConsumables>();
#else
            consumables = FindObjectOfType<PixelConsumables>();
#endif
        }

        if (startRunning) running = true;
        spawnTimer = firstGhostDelay;
    }

    private void Update()
    {
        if (!running || ghostActive) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f) StartCoroutine(GhostRoutine());
    }

    /// <summary>Starts the minigame (the shop calls this when it is bought). Safe to call more than once.</summary>
    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstGhostDelay;
    }

    /// <summary>Stops the minigame.</summary>
    public override void Deactivate() => running = false;

    /// <summary>Makes a ghost appear right now (handy for testing: right-click the component &gt; Spawn Ghost Now).</summary>
    [ContextMenu("Spawn Ghost Now")]
    public void SpawnNow()
    {
        if (Application.isPlaying && !ghostActive) StartCoroutine(GhostRoutine());
    }

    // ------------------------------------------------------------------
    // The ghost
    // ------------------------------------------------------------------

    private IEnumerator GhostRoutine()
    {
        ghostActive = true;
        onGhostAppeared?.Invoke();

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) { ghostActive = false; yield break; }

        // Cube with a see-through material and a (trigger) collider for clicking.
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "Ghost";
        BoxCollider box = ghost.GetComponent<BoxCollider>();
        box.isTrigger = true; // never pushes old pixels around
        box.size = Vector3.one * clickSizeMultiplier;

        Renderer rend = ghost.GetComponent<Renderer>();
        Color c = ghostColor;
        c.a = ghostOpacity;
        if (ghostMaterial == null) ghostMaterial = clicker.CreateVisualMaterial(c, true);
        if (ghostMaterial != null)
        {
            rend.sharedMaterial = ghostMaterial;
        }
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        // Distance along the camera's forward axis: a bit in front of the cube.
        float depth = 10f;
        if (clicker.PixelTransform != null)
            depth = Mathf.Max(cam.nearClipPlane + 0.5f,
                              Vector3.Dot(clicker.PixelTransform.position - cam.transform.position, cam.transform.forward)
                              * depthFraction);

        bool leftToRight = Random.value < 0.5f;
        float fromX = leftToRight ? -0.1f : 1.1f;
        float toX = leftToRight ? 1.1f : -0.1f;
        float baseY = Random.Range(Mathf.Min(minHeight, maxHeight), Mathf.Max(minHeight, maxHeight));
        float phase = Random.value * 10f;

        float t = 0f;
        bool caught = false;
        while (t < crossSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / crossSeconds);
            float time = t + phase;

            // Smooth drift plus little hops.
            float hop = Mathf.Abs(Mathf.Sin(time * bounceFrequency * Mathf.PI));
            float y = baseY + Mathf.Sin(time * wobbleFrequency * Mathf.PI * 2f) * wobbleAmplitude + hop * bounceHeight;
            Vector3 pos = cam.ViewportToWorldPoint(new Vector3(Mathf.Lerp(fromX, toX, k), y, depth));

            ghost.transform.position = pos;
            ghost.transform.rotation = cam.transform.rotation * Quaternion.Euler(
                Mathf.Sin(time * 1.3f) * swayDegrees * 0.5f,
                Mathf.Sin(time * 0.9f) * swayDegrees,
                Mathf.Sin(time * 1.7f) * swayDegrees);
            float squash = 1f + (hop - 0.5f) * squashAmount * 2f;
            ghost.transform.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash)) * ghostSize;

            // Fade in/out at the edges.
            float fade = edgeFade > 0f ? Mathf.Clamp01(Mathf.Min(k, 1f - k) / edgeFade) : 1f;
            Color shown = c;
            shown.a = ghostOpacity * fade;
            block.SetColor("_BaseColor", shown);
            block.SetColor("_Color", shown);
            rend.SetPropertyBlock(block);

            if (Time.timeScale > 0f && LeftPressed() && !PointerOverUI() && ClickedGhost(cam, box))
            {
                caught = true;
                break;
            }
            yield return null;
        }

        Vector3 lastPos = ghost.transform.position;
        Destroy(ghost);

        if (caught) Catch(lastPos, cam);
        else onGhostMissed?.Invoke();

        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        ghostActive = false;
    }

    private bool ClickedGhost(Camera cam, Collider ghostCollider)
    {
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        return ghostCollider.Raycast(ray, out _, 1000f);
    }

    private void Catch(Vector3 position, Camera cam)
    {
        bool reachedBefore = ThresholdReached;
        ghostsCaught += 1d;
        if (!reachedBefore && ThresholdReached) onThresholdReached?.Invoke();

        string buffName = "";
        int potion = -1;
        if (consumables != null) potion = consumables.GrantRandomBuff(buffDurationMultiplier, out buffName);

        if (caughtSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
            audioSource.PlayOneShot(caughtSound, soundVolume);
        }

        StartCoroutine(CaughtMessage(position, cam, potion >= 0 ? string.Format(caughtFormat, buffName) : "Ghost caught!"));
        onGhostCaught?.Invoke(potion);
    }

    /// <summary>Text that rises from where the ghost was and fades away.</summary>
    private IEnumerator CaughtMessage(Vector3 start, Camera cam, string message)
    {
        GameObject go = new GameObject("Ghost Message");
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = caughtFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(20f, 3f);

        float t = 0f;
        while (t < caughtSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / caughtSeconds);
            go.transform.position = start + cam.transform.up * (k * 1.2f);
            go.transform.rotation = cam.transform.rotation;
            Color col = caughtColor;
            col.a *= 1f - k * k;
            text.color = col;
            yield return null;
        }
        Destroy(go);
    }

    // --- Mouse input (works with both input systems) ---

    private static Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool LeftPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static bool PointerOverUI() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
