using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using static PixelInput;

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

    [Header("Potion (carried, then dropped)")]
    [Range(0.1f, 1.5f)]
    [Tooltip("Size of the glass potion the ghost carries, relative to the ghost's size.")]
    [SerializeField] private float potionSize = 0.55f;

    [Range(0.1f, 1f)]
    [Tooltip("Size of the pixel cube inside the glass, relative to the glass.")]
    [SerializeField] private float potionInnerScale = 0.55f;

    [Tooltip("Colour of the potion's glass.")]
    [SerializeField] private Color potionGlassColor = new Color(0.8f, 0.92f, 1f, 0.4f);

    [Min(0.2f)]
    [Tooltip("The dropped potion shatters when it lands, or after this many seconds if it never lands.")]
    [SerializeField] private float dropMaxSeconds = 3f;

    [Header("Backpack (needs Pixel Grabbing)")]
    [Tooltip("With Pixel Grabbing bought, the falling potion can be grabbed and dropped on a backpack icon to keep it in your inventory.")]
    [SerializeField] private bool allowBackpack = true;

    [Min(0f)]
    [Tooltip("Air drag on the falling potion while it can be grabbed (higher = falls slower, easier to catch).")]
    [SerializeField] private float grabFallDrag = 2.5f;

    [Min(0.05f)]
    [Tooltip("How close the mouse must be to the potion (world units) to grab it.")]
    [SerializeField] private float potionGrabRadius = 0.45f;

    [Min(1f)]
    [Tooltip("How tightly the grabbed potion follows the mouse.")]
    [SerializeField] private float potionFollowSharpness = 20f;

    [Min(10f)]
    [Tooltip("Size of the backpack icon (canvas units).")]
    [SerializeField] private float backpackSize = 130f;

    [Tooltip("Distance of the backpack icon from the right edge and from the bottom black bar (canvas units).")]
    [SerializeField] private Vector2 backpackMargin = new Vector2(40f, 40f);

    [Range(0f, 1f)]
    [Tooltip("How see-through the backpack icon is while the potion falls / is held.")]
    [SerializeField] private float backpackAlpha = 0.5f;

    [Tooltip("Text announced in the event log when a potion is kept. {0} = potion name.")]
    [SerializeField] private string keptFormat = "Kept a {0} from the ghost";

    [Range(4, 40)]
    [Tooltip("Glass shards when the potion shatters.")]
    [SerializeField] private int shardCount = 16;

    [Min(0.5f)]
    [Tooltip("How fast the shards fly out.")]
    [SerializeField] private float shardSpeed = 4f;

    [Min(0.1f)]
    [Tooltip("Seconds the shards stay before shrinking away.")]
    [SerializeField] private float shardLife = 1.2f;

    [Range(0.05f, 0.6f)]
    [Tooltip("Size of a shard relative to the potion.")]
    [SerializeField] private float shardSize = 0.3f;

    [Tooltip("Sound played when the potion shatters (an id from PixelAudio).")]
    [SerializeField] private string shatterSound = "glass_shatter";

    [Header("Reward")]
    [Min(0.1f)]
    [Tooltip("The buff lasts the potion's normal duration times this.")]
    [SerializeField] private float buffDurationMultiplier = 1f;

    [Tooltip("Text that rises from the ghost when it is caught. {0} = the potion that was granted.")]
    [SerializeField] private string caughtFormat = "Ghost buff: {0}!";

    [Tooltip("Text size of that message (3D text: about 10 per world unit).")]
    [SerializeField] private float caughtTextSize = 2f;

    [Range(0f, 0.3f)]
    [Tooltip("How far from the edge of the screen the message stays (fraction of the screen), so it is never cut off.")]
    [SerializeField] private float caughtScreenMargin = 0.04f;

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
    private int baitGhostsLeft; // ghosts still to come from Ghost Bait

    [Header("Ghost Bait")]
    [Range(1, 10)]
    [Tooltip("How many ghosts one Ghost Bait calls.")]
    [SerializeField] private int baitGhosts = 3;

    [Min(0f)]
    [Tooltip("Seconds until the first bait ghost appears at the latest.")]
    [SerializeField] private float baitFirstDelay = 4f;

    [Min(0f)]
    [Tooltip("Seconds between the bait ghosts.")]
    [SerializeField] private float baitGap = 3f;

    [Min(1f)]
    [Tooltip("Bait ghosts cross the screen this many times slower than usual.")]
    [SerializeField] private float baitSlowFactor = 1.8f;

    /// <summary>
    /// Ghost Bait: the next ghost appears within a few seconds and a short wave of slower ghosts follows.
    /// Returns false if the minigame isn't running or a bait wave is already under way.
    /// </summary>
    public bool UseBait()
    {
        if (!running || baitGhostsLeft > 0) return false;
        baitGhostsLeft = Mathf.Max(1, baitGhosts);
        if (!ghostActive) spawnTimer = Mathf.Min(spawnTimer, baitFirstDelay);
        return true;
    }

    public override bool Busy => ghostActive;

    protected override void OnDespawned()
    {
        ghostActive = false;
        baitGhostsLeft = 0;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }
    private Material ghostMaterial;
    private AudioSource audioSource;

    public override string DisplayName => "Ghost Hunt";
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

    // --- Ectoplasm: a crafting material every caught ghost leaves behind (no use yet; saved through ExtraValue) ---
    [Header("Ectoplasm")]
    [Tooltip("Fewest ectoplasm a caught ghost leaves.")]
    [SerializeField] private int ectoplasmMin = 1;

    [Tooltip("Most ectoplasm a caught ghost leaves.")]
    [SerializeField] private int ectoplasmMax = 4;

    [Tooltip("Ectoplasm held (a minigame material, shown in the Inventory's Materials tab).")]
    [SerializeField] private double ectoplasm = 0;

    public override string MaterialName => "Ectoplasm";
    public override double MaterialAmount => ectoplasm;
    public override Color MaterialColor => new Color(0.55f, 1f, 0.8f, 1f);
    public override double ExtraValue => ectoplasm;
    public override void SetExtraValue(double value) => ectoplasm = System.Math.Max(0d, value);

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
            clicker = PixelFind.First<PixelClicker>();
        }
        if (clicker == null)
        {
            Debug.LogError("PixelGhostMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        if (consumables == null)
        {
            consumables = PixelFind.First<PixelConsumables>();
        }

        if (startRunning) running = true;
        spawnTimer = firstGhostDelay;
    }

    private void Update()
    {
        if (!running || ghostActive) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(GhostRoutine());
            else spawnTimer = PixelMinigameLimits.RetrySeconds; // too many minigames running right now
        }
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
    public override void SpawnNow()
    {
        if (Application.isPlaying && !ghostActive) StartCoroutine(GhostRoutine());
    }

    // ------------------------------------------------------------------
    // The ghost
    // ------------------------------------------------------------------

    private Transform ghostTransform;

    /// <summary>Where the ghost is on screen (pixels), or null when none is out. The first-time tip box follows it.</summary>
    public Vector2? GhostScreenPosition()
    {
        if (!ghostActive || ghostTransform == null) return null;
        Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return null;
        Vector3 sp = cam.WorldToScreenPoint(ghostTransform.position);
        return sp.z > 0f ? new Vector2(sp.x, sp.y) : (Vector2?)null;
    }

    private IEnumerator GhostRoutine()
    {
        ghostActive = true;
        bool bait = baitGhostsLeft > 0; // a bait ghost drifts slower
        if (bait) baitGhostsLeft--;
        float cross = crossSeconds * (bait ? baitSlowFactor : 1f);
        Report(MinigameEvent.Spawned);
        onGhostAppeared?.Invoke();

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) { ghostActive = false; yield break; }

        // Cube with a see-through material and a (trigger) collider for clicking.
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "Ghost";
        BoxCollider box = ghost.GetComponent<BoxCollider>();
        box.isTrigger = true; // never pushes old pixels around
        box.size = Vector3.one * clickSizeMultiplier;

        Track(ghost);
        ghostTransform = ghost.transform;
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

        // The potion the ghost carries (the buff you get when it shatters): a glass cube with the pixel cube inside.
        int potion = consumables != null ? consumables.PickRandomBuff() : -1;
        GameObject potionVisual = potion >= 0 ? BuildPotion(ghost.transform, potion) : null;
        Track(potionVisual);

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
        while (t < cross)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / cross);
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
        if (caught && potionVisual != null)
        {
            // He drops the potion: it falls and shatters, and the buff starts then.
            potionVisual.transform.SetParent(null, true);
            StartCoroutine(DropPotion(potionVisual, potion, cam));
        }
        else if (potionVisual != null) Destroy(potionVisual);
        Destroy(ghost);

        if (caught) Catch(lastPos, cam, potionVisual != null ? potion : -2);
        else onGhostMissed?.Invoke();

        spawnTimer = baitGhostsLeft > 0 ? baitGap
                   : Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
        ghostActive = false;
    }

    private bool ClickedGhost(Camera cam, Collider ghostCollider)
    {
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        return ghostCollider.Raycast(ray, out _, 1000f);
    }

    private void Catch(Vector3 position, Camera cam, int carried)
    {
        bool reachedBefore = ThresholdReached;
        ghostsCaught += 1d;
        int lo = Mathf.Max(0, ectoplasmMin), hi = Mathf.Max(lo, ectoplasmMax);
        int dropped = UnityEngine.Random.Range(lo, hi + 1);
        if (dropped > 0)
        {
            ectoplasm += dropped;
            PixelStats.Count("ghost.ectoplasm", dropped);
            PixelHints.Announce("+" + dropped + " Ectoplasm");
        }
        Report(MinigameEvent.Clicked);
        if (!reachedBefore && ThresholdReached) onThresholdReached?.Invoke();

        // carried >= 0: the dropped potion applies itself when it shatters. -2: no carried potion, grant one right away.
        string buffName = "";
        int potion = carried;
        if (carried >= 0 && consumables != null) buffName = consumables.PotionName(carried);
        else if (consumables != null) potion = consumables.GrantRandomBuff(buffDurationMultiplier, out buffName);

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

    /// <summary>Moves a 3D text so all of it stays inside the camera's view (the whole text width, not just its centre).</summary>
    private static Vector3 KeepOnScreen(Camera cam, TextMeshPro text, Vector3 position, float margin)
    {
        Vector3 v = cam.WorldToViewportPoint(position);
        if (v.z <= 0.01f) return position;

        // How big the view is at that distance, to turn the text's size into screen fractions.
        float viewHeight = 2f * v.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewWidth = viewHeight * cam.aspect;
        text.ForceMeshUpdate();
        float halfW = Mathf.Min(0.5f, text.preferredWidth * 0.5f / Mathf.Max(0.01f, viewWidth));
        float halfH = Mathf.Min(0.5f, text.preferredHeight * 0.5f / Mathf.Max(0.01f, viewHeight));

        // Stay clear of the black bars too.
        float bars = PixelHud.Instance != null ? PixelHud.Instance.RawBarHeight * Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f) / Mathf.Max(1, Screen.height) : 0f;
        float x = Mathf.Clamp(v.x, margin + halfW, 1f - margin - halfW);
        float y = Mathf.Clamp(v.y, bars + margin + halfH, 1f - bars - margin - halfH);
        return cam.ViewportToWorldPoint(new Vector3(x, y, v.z));
    }

    /// <summary>Text that rises from where the ghost was and fades away.</summary>
    private IEnumerator CaughtMessage(Vector3 start, Camera cam, string message)
    {
        GameObject go = new GameObject("Ghost Message");
        Track(go);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = caughtTextSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(20f, 3f);

        float t = 0f;
        while (t < caughtSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / caughtSeconds);
            go.transform.position = KeepOnScreen(cam, text, start + cam.transform.up * (k * 1.2f), caughtScreenMargin);
            go.transform.rotation = cam.transform.rotation;
            Color col = caughtColor;
            col.a *= 1f - k * k;
            text.color = col;
            yield return null;
        }
        Destroy(go);
    }

    // ------------------------------------------------------------------
    // The carried / dropped potion
    // ------------------------------------------------------------------

    private Color PotionPixelColor(int potion)
    {
        PixelClicker.PixelType type = consumables.PotionType(potion);
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (t.type == type) return new Color(t.color.r, t.color.g, t.color.b, 1f);
        return Color.white;
    }

    /// <summary>A small glass cube with the potion's pixel cube inside, parented to the ghost (no colliders).</summary>
    private GameObject BuildPotion(Transform parent, int potion)
    {
        GameObject root = new GameObject("Ghost Potion");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * potionSize;

        GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        inner.name = "Pixel";
        Destroy(inner.GetComponent<Collider>());
        inner.transform.SetParent(root.transform, false);
        inner.transform.localScale = Vector3.one * potionInnerScale;
        Material innerMat = clicker.CreateVisualMaterial(PotionPixelColor(potion), false);
        if (innerMat != null) inner.GetComponent<Renderer>().sharedMaterial = innerMat;

        GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glass.name = "Glass";
        Destroy(glass.GetComponent<Collider>());
        glass.transform.SetParent(root.transform, false);
        Material glassMat = clicker.CreateVisualMaterial(potionGlassColor, true);
        if (glassMat != null) glass.GetComponent<Renderer>().sharedMaterial = glassMat;

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return root;
    }

    /// <summary>
    /// The potion falls from where the ghost was and shatters on landing (or after dropMaxSeconds), then the buff starts.
    /// With Pixel Grabbing you can catch it in mid-air and drop it on the backpack icon to keep it instead.
    /// </summary>
    private IEnumerator DropPotion(GameObject potionObject, int potion, Camera cam)
    {
        Track(potionObject);
        Vector3 size = potionObject.transform.lossyScale;
        potionObject.transform.localScale = size; // now free of the ghost's squash
        BoxCollider box = potionObject.AddComponent<BoxCollider>();
        box.size = Vector3.one;
        Rigidbody rb = potionObject.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 0.3f;
        rb.AddTorque(Random.onUnitSphere * 2f, ForceMode.VelocityChange);
        GhostPotionImpact impact = potionObject.AddComponent<GhostPotionImpact>();

        PixelGrab grab = allowBackpack ? PixelFind.First<PixelGrab>() : null;
        bool canGrab = grab != null && grab.CanGrab && consumables != null;
        if (canGrab)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = grabFallDrag;
#else
            rb.drag = grabFallDrag;
#endif
            SetBackpackShown(true);
        }

        bool held = false, kept = false;
        Plane plane = default;
        Vector3 offset = Vector3.zero, velocity = Vector3.zero, lastPos = rb.position;
        float t = 0f;
        while (!impact.Hit && (held || t < dropMaxSeconds))
        {
            if (Time.timeScale > 0f && !PixelPauseMenu.IsPaused)
            {
                if (!held)
                {
                    t += Time.deltaTime;
                    if (canGrab && LeftPressed() && !PointerOverUI() && PointerNear(cam, rb.position, potionGrabRadius))
                    {
                        held = true;
                        plane = new Plane(-cam.transform.forward, rb.position);
                        Ray r = cam.ScreenPointToRay(PointerPosition());
                        offset = plane.Raycast(r, out float e) ? rb.position - r.GetPoint(e) : Vector3.zero;
                        rb.isKinematic = true;
                        lastPos = rb.position;
                        PixelAudio.Play("grab");
                    }
                }
                if (held)
                {
                    bool over = PointerOverBackpack();
                    SetBackpackHighlight(over);
                    if (!LeftHeld())
                    {
                        held = false;
                        SetBackpackHighlight(false);
                        if (over) { kept = true; break; }
                        rb.isKinematic = false; // let go: it carries on falling with the throw
#if UNITY_6000_0_OR_NEWER
                        rb.linearVelocity = velocity;
#else
                        rb.velocity = velocity;
#endif
                        PixelAudio.Play("drop");
                    }
                    else
                    {
                        Ray ray = cam.ScreenPointToRay(PointerPosition());
                        if (plane.Raycast(ray, out float enter))
                        {
                            Vector3 target = ray.GetPoint(enter) + offset;
                            Vector3 next = Vector3.Lerp(rb.position, target, 1f - Mathf.Exp(-potionFollowSharpness * Time.deltaTime));
                            potionObject.transform.position = next;
                            velocity = Vector3.Lerp(velocity, (next - lastPos) / Mathf.Max(0.0001f, Time.deltaTime), 0.5f);
                            lastPos = next;
                        }
                    }
                }
            }
            yield return null;
        }

        SetBackpackShown(false);

        if (kept)
        {
            yield return KeepPotion(potionObject, potion);
            yield break;
        }

        Vector3 point = potionObject.transform.position;
        Vector3 normal = impact.Hit ? impact.Normal : Vector3.up;
        ShatterPotion(potionObject, potion, point, normal);

        if (consumables != null) consumables.ApplyBuff(potion, buffDurationMultiplier);
    }

    private static bool PointerNear(Camera cam, Vector3 worldPoint, float radius)
    {
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        Vector3 to = worldPoint - ray.origin;
        if (Vector3.Dot(to, ray.direction) <= 0f) return false;
        return Vector3.Cross(ray.direction, to).magnitude <= radius;
    }

    /// <summary>The potion shrinks into the backpack and is added to the inventory (past the normal limit if needed).</summary>
    private IEnumerator KeepPotion(GameObject potionObject, int potion)
    {
        consumables.AddKept(potion);
        PixelStats.Count("ghost.kept");
        PixelAudio.Play("purchase");
        PixelHints.Announce(string.Format(keptFormat, consumables.PotionName(potion)));

        Rigidbody rb = potionObject.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        Collider col = potionObject.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Vector3 from = potionObject.transform.localScale;
        float t = 0f;
        const float seconds = 0.25f;
        while (t < seconds && potionObject != null)
        {
            t += Time.unscaledDeltaTime;
            potionObject.transform.localScale = from * Mathf.Clamp01(1f - t / seconds);
            yield return null;
        }
        if (potionObject != null) Destroy(potionObject);
    }

    // ------------------------------------------------------------------
    // Backpack icon (a semi-transparent pixel-art backpack at the bottom right)
    // ------------------------------------------------------------------

    private GameObject backpackCanvas;
    private RectTransform backpackRect;
    private Image backpackImage;
    private CanvasGroup backpackGroup;
    private Sprite backpackSprite;
    private bool backpackOver;

    private static readonly string[] BackpackRows =
    {
        ".....OOOOOOOO.....",
        "....O........O....",
        "....O........O....",
        "...OOOOOOOOOOOO...",
        "..OBBBBBBBBBBBBO..",
        "..OBBBBBBBBBBBBO..",
        "..OFFFFFFFFFFFFO..",
        "..OFFFFFFFFFFFFO..",
        "..OOOOOOOOOOOOOO..",
        "..OBBBBBBBBBBBBO..",
        "..OBBOOOOOOOOBBO..",
        "..OBBOPPPPPPOBBO..",
        "..OBBOPPPPPPOBBO..",
        "..OBBOPPPPPPOBBO..",
        "..OBBOOOOOOOOBBO..",
        "..OBBBBBBBBBBBBO..",
        "..OBBBBBBBBBBBBO..",
        "..OOOOOOOOOOOOOO..",
    };

    private Sprite BuildBackpackSprite()
    {
        int h = BackpackRows.Length, w = BackpackRows[0].Length;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 outline = new Color32(30, 24, 20, 255), body = new Color32(200, 140, 60, 255),
                flap = new Color32(165, 105, 45, 255), pocket = new Color32(230, 175, 95, 255);
        for (int y = 0; y < h; y++)
        {
            string row = BackpackRows[h - 1 - y]; // texture rows run bottom to top
            for (int x = 0; x < w; x++)
            {
                char c = row[x];
                tex.SetPixel(x, y, c == 'O' ? outline : c == 'B' ? body : c == 'F' ? flap : c == 'P' ? pocket : clear);
            }
        }
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void EnsureBackpack()
    {
        if (backpackCanvas != null) return;
        backpackCanvas = PixelUIKit.CreateCanvas("Ghost Backpack Canvas", 95, new Vector2(1920f, 1080f), false);
        Track(backpackCanvas);
        backpackGroup = backpackCanvas.AddComponent<CanvasGroup>();
        backpackGroup.blocksRaycasts = false;
        backpackGroup.interactable = false;

        if (backpackSprite == null) backpackSprite = BuildBackpackSprite();
        GameObject go = new GameObject("Backpack", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(backpackCanvas.transform, false);
        backpackImage = go.GetComponent<Image>();
        backpackImage.sprite = backpackSprite;
        backpackImage.preserveAspect = true;
        backpackImage.raycastTarget = false;
        backpackRect = go.GetComponent<RectTransform>();
        backpackRect.anchorMin = backpackRect.anchorMax = backpackRect.pivot = new Vector2(1f, 0f);
        backpackRect.sizeDelta = new Vector2(backpackSize, backpackSize);
        backpackCanvas.SetActive(false);
    }

    private void SetBackpackShown(bool shown)
    {
        if (shown)
        {
            EnsureBackpack();
            float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
            backpackRect.anchoredPosition = new Vector2(-backpackMargin.x, bar + backpackMargin.y);
            backpackOver = false;
            backpackCanvas.SetActive(true);
            StartCoroutine(PulseBackpack());
        }
        else if (backpackCanvas != null) backpackCanvas.SetActive(false);
    }

    private void SetBackpackHighlight(bool over) => backpackOver = over;

    private bool PointerOverBackpack()
    {
        if (backpackRect == null || backpackCanvas == null || !backpackCanvas.activeSelf) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(backpackRect, PointerPosition(), null);
    }

    private IEnumerator PulseBackpack()
    {
        while (backpackCanvas != null && backpackCanvas.activeSelf)
        {
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f);
            Color c = Color.white;
            c.a = backpackOver ? 1f : backpackAlpha * pulse;
            backpackImage.color = c;
            backpackRect.localScale = Vector3.one * (backpackOver ? 1.2f : 1f);
            yield return null;
        }
    }

    private void ShatterPotion(GameObject potionObject, int potion, Vector3 point, Vector3 normal)
    {
        if (!string.IsNullOrEmpty(shatterSound)) PixelAudio.Play(shatterSound);

        Renderer glass = potionObject.transform.Find("Glass").GetComponent<Renderer>();
        Renderer pixel = potionObject.transform.Find("Pixel").GetComponent<Renderer>();
        MeshFilter mesh = glass.GetComponent<MeshFilter>();
        float baseSize = potionObject.transform.lossyScale.x;

        for (int i = 0; i < shardCount; i++)
        {
            bool isPixel = i % 3 == 0; // a few shards are the pixel inside
            Renderer source = isPixel ? pixel : glass;
            GameObject shard = new GameObject("PotionShard");
            Track(shard);
            shard.transform.position = point + Random.insideUnitSphere * baseSize * 0.3f;
            shard.transform.rotation = Random.rotation;
            float s = baseSize * shardSize * (isPixel ? 0.5f : 1f);
            shard.transform.localScale = new Vector3(s * Random.Range(0.6f, 1.2f), s * Random.Range(0.12f, 0.3f) * (isPixel ? 3f : 1f), s * Random.Range(0.5f, 1f));
            shard.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            MeshRenderer mr = shard.AddComponent<MeshRenderer>();
            mr.sharedMaterial = source.sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shard.AddComponent<BoxCollider>();
            Rigidbody rb = shard.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            Vector3 push = normal * shardSpeed * Random.Range(0.4f, 1f) + Random.onUnitSphere * shardSpeed * 0.7f;
            push.y = Mathf.Abs(push.y);
            rb.AddForce(push, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * Random.Range(5f, 15f), ForceMode.VelocityChange);
            shard.AddComponent<OldPixelShard>().Setup(shardLife * Random.Range(0.7f, 1.2f));
        }
        Destroy(potionObject);
    }
}

/// <summary>Remembers that the dropped ghost potion hit something (the floor, the cube, an old pixel).</summary>
public class GhostPotionImpact : MonoBehaviour
{
    public bool Hit { get; private set; }
    public Vector3 Normal { get; private set; } = Vector3.up;

    private void OnCollisionEnter(Collision collision)
    {
        if (Hit || collision.collider.GetComponent<OldPixelShard>() != null) return;
        Hit = true;
        Normal = collision.GetContact(0).normal;
    }
}
