using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Snake minigame for Pixel Clicker.
///
/// Every so often (once bought in the shop's Minigames tab) the old pixels on the floor stop despawning and freeze in place, and a
/// snake head appears. Steer it with the arrow keys (or the on-screen arrows): every pixel it touches joins the end of the snake.
/// Eat ALL the pixels without biting your own body and you are paid 10x their worth. Bite yourself, or run out of time, and the
/// pixels simply drop and go back to ageing normally.
///
/// The snake glides at a constant speed; it can't reverse straight back on itself, and the edges of the playing area just stop it
/// until you turn. Everything is sized relative to the old pixels, so it works at any cube size. Add this to any GameObject
/// (PixelShop adds it automatically if it is missing).
/// </summary>
public class PixelSnakeMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, old pixels, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Snake is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame is switched on before the first snake round can start.")]
    [SerializeField] private float firstRoundDelay = 60f;

    [Min(1f)]
    [Tooltip("Shortest wait between rounds (seconds).")]
    [SerializeField] private float minInterval = 120f;

    [Min(1f)]
    [Tooltip("Longest wait between rounds (seconds).")]
    [SerializeField] private float maxInterval = 240f;

    [Min(2)]
    [Tooltip("A round only starts when at least this many old pixels are lying around.")]
    [SerializeField] private int minPixels = 8;

    [Min(3)]
    [Tooltip("A round never starts with more pixels than this (the rest keep ageing normally).")]
    [SerializeField] private int maxPixels = 40;

    [Header("Rules")]
    [Min(5f)]
    [Tooltip("Seconds you have to eat every pixel.")]
    [SerializeField] private float timeLimit = 90f;

    [Min(1)]
    [Tooltip("Payout when every pixel is eaten: this many times the worth of the pixels you ate.")]
    [SerializeField] private float rewardMultiplier = 10f;

    [Header("Movement (sizes are in old-pixel widths)")]
    [Min(0.5f)]
    [Tooltip("Starting speed of the snake, in pixel widths per second.")]
    [SerializeField] private float speed = 3f;

    [Min(0f)]
    [Tooltip("The snake speeds up by this much (pixel widths per second) for every pixel eaten.")]
    [SerializeField] private float speedGain = 0.06f;

    [Min(0.5f)]
    [Tooltip("Size of the head, relative to an old pixel.")]
    [SerializeField] private float headScale = 1.25f;

    [Min(0.3f)]
    [Tooltip("The head eats a pixel when their centres are closer than this (pixel widths).")]
    [SerializeField] private float eatDistance = 0.9f;

    [Min(0.3f)]
    [Tooltip("Gap between two segments of the body (pixel widths).")]
    [SerializeField] private float segmentSpacing = 1f;

    [Min(0.2f)]
    [Tooltip("Biting yourself: the head is this close (pixel widths) to a body segment.")]
    [SerializeField] private float biteDistance = 0.55f;

    [Min(2)]
    [Tooltip("The first few body segments right behind the head can never be bitten (it would be unfair on tight turns).")]
    [SerializeField] private int safeSegments = 4;

    [Min(1f)]
    [Tooltip("The playing area is the area the pixels lie on plus this margin on every side (pixel widths).")]
    [SerializeField] private float arenaMargin = 4f;

    [Header("Look")]
    [Tooltip("Colour of the snake's head.")]
    [SerializeField] private Color headColor = new Color(0.3f, 0.95f, 0.4f, 1f);

    [Tooltip("Text size of the status line at the top (canvas units).")]
    [SerializeField] private float statusFontSize = 40f;

    [Tooltip("Size of an on-screen arrow button (canvas units).")]
    [SerializeField] private float arrowButtonSize = 110f;

    [Tooltip("Show the on-screen arrow buttons (the arrow keys always work too).")]
    [SerializeField] private bool showArrowButtons = true;

    [Header("Text")]
    [Tooltip("Status line while playing. {0} = pixels eaten, {1} = pixels in the round, {2} = seconds left.")]
    [SerializeField] private string statusFormat = "SNAKE   {0} / {1}   {2}s";

    [Tooltip("Message when every pixel was eaten. {0} = the payout (a number).")]
    [SerializeField] private string winFormat = "ALL EATEN!  Worth x{0}!";

    [Tooltip("Message when you bite yourself.")]
    [SerializeField] private string biteText = "You bit yourself!";

    [Tooltip("Message when time runs out.")]
    [SerializeField] private string timeoutText = "Out of time!";

    [Tooltip("Size of those messages (3D text: about 10 per world unit).")]
    [SerializeField] private float messageTextSize = 3f;

    [Min(0.2f)]
    [Tooltip("How long the message stays (seconds).")]
    [SerializeField] private float messageSeconds = 2.2f;

    // ------------------------------------------------------------------

    /// <summary>True while a round is being played (pixels frozen, cube clicks and grabbing switched off).</summary>
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSnakeStatics() { Active = false; }

    private class Piece
    {
        public Rigidbody body;
        public Transform tf;
        public int tier;
        public double amount;
        public OldPixelDespawn despawn;
        public Collider[] colliders;
        public Behaviour[] paused; // gravity well / float behaviours switched off for the round
        public float height;
    }

    private readonly List<Piece> targets = new List<Piece>();
    private readonly List<Piece> bodyPieces = new List<Piece>();
    private readonly List<Vector3> trail = new List<Vector3>();

    private float spawnTimer;
    private bool activeRound;
    private Camera cam;
    private GameObject head;
    private Vector3 headPos, headDir;
    private Vector3[] dirs = new Vector3[4]; // 0 up, 1 down, 2 left, 3 right (on the floor, as the camera sees it)
    private int wantedDir = -1;
    private float groundY, unit, timeLeft, currentSpeed;
    private Vector2 arenaMin, arenaMax;
    private int totalPixels;
    private GameObject uiRoot;
    private TMP_Text statusLabel;
    private bool setBlock;

    public override string Id => "snake";
    public override string DisplayName => "Snake";
    public override bool Running => running;
    public override bool Busy => activeRound;
    public override bool Takeover => true;

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstRoundDelay;
    }

    public override void Deactivate()
    {
        running = false;
        if (activeRound) EndRound(false, null);
    }

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelSnakeMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (startRunning) running = true;
        spawnTimer = firstRoundDelay;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (activeRound) EndRound(false, null);
    }

    protected override void OnDespawned()
    {
        // The dev tools' Despawn: end the round quietly (everything goes back to normal).
        if (activeRound) EndRound(false, null);
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    [ContextMenu("Start A Snake Round Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !activeRound) TryStart(true);
    }

    private void Update()
    {
        if (activeRound) { Tick(Time.deltaTime); return; }
        if (!running) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        if (!PixelMinigameLimits.AllowAnother(this)) { spawnTimer = PixelMinigameLimits.RetrySeconds; return; }
        if (!TryStart(false)) spawnTimer = 12f; // not enough pixels lying around yet
        else spawnTimer = 0f;
    }

    // ------------------------------------------------------------------
    // Starting
    // ------------------------------------------------------------------

    private bool TryStart(bool dev)
    {
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;

        // The old pixels lying around (not flying meteors, not ones already vanishing or being held).
        List<Piece> found = new List<Piece>();
        var all = clicker.OldPixels;
        for (int i = 0; i < all.Count && found.Count < maxPixels; i++)
        {
            Rigidbody rb = all[i];
            if (rb == null || rb.isKinematic || clicker.IsFlyingPixel(rb)) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && (d.IsDespawning || d.Held)) continue;

            Renderer r = rb.GetComponentInChildren<Renderer>();
            found.Add(new Piece
            {
                body = rb, tf = rb.transform, tier = info.tierIndex, amount = info.amount, despawn = d,
                colliders = rb.GetComponents<Collider>(),
                paused = new Behaviour[] { rb.GetComponent<OldPixelGravityWell>(), rb.GetComponent<OldPixelFloat>() },
                height = r != null ? r.bounds.extents.y : 0.2f,
            });
        }
        if (found.Count < (dev ? 1 : minPixels)) return false;

        targets.Clear();
        bodyPieces.Clear();
        trail.Clear();
        targets.AddRange(found);
        totalPixels = targets.Count;

        // Sizes follow the old pixels' own size.
        float sum = 0f;
        groundY = float.MaxValue;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (Piece p in targets)
        {
            Renderer r = p.body.GetComponentInChildren<Renderer>();
            sum += r != null ? Mathf.Max(r.bounds.size.x, r.bounds.size.z) : 0.5f;
            Vector3 pos = p.body.position;
            groundY = Mathf.Min(groundY, pos.y);
            min = Vector2.Min(min, new Vector2(pos.x, pos.z));
            max = Vector2.Max(max, new Vector2(pos.x, pos.z));
        }
        unit = Mathf.Max(0.1f, sum / targets.Count);
        float margin = arenaMargin * unit;
        arenaMin = min - new Vector2(margin, margin);
        arenaMax = max + new Vector2(margin, margin);

        // Safeguard: the snake never leaves the screen. Cut the area down to the part of the floor that is in view.
        Plane floor = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        Vector2 viewMin = new Vector2(float.MaxValue, float.MaxValue), viewMax = new Vector2(float.MinValue, float.MinValue);
        bool viewOk = true;
        foreach (Vector2 corner in new[] { new Vector2(0.07f, 0.2f), new Vector2(0.93f, 0.2f), new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.78f) })
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(corner.x, corner.y, 0f));
            if (!floor.Raycast(ray, out float enter)) { viewOk = false; break; }
            Vector3 hit = ray.GetPoint(enter);
            viewMin = Vector2.Min(viewMin, new Vector2(hit.x, hit.z));
            viewMax = Vector2.Max(viewMax, new Vector2(hit.x, hit.z));
        }
        if (viewOk)
        {
            Vector2 cutMin = Vector2.Max(arenaMin, viewMin), cutMax = Vector2.Min(arenaMax, viewMax);
            if (cutMax.x - cutMin.x > unit * 3f && cutMax.y - cutMin.y > unit * 3f) { arenaMin = cutMin; arenaMax = cutMax; }
        }

        // Floor directions as the camera sees them.
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        dirs[0] = forward; dirs[1] = -forward; dirs[2] = -right; dirs[3] = right;

        // Freeze the pixels: nothing despawns, nothing moves.
        OldPixelDespawn.HoldAll = true;
        foreach (Piece p in targets)
        {
            SetVelocities(p.body, Vector3.zero);
            p.body.isKinematic = true;
            foreach (Behaviour b in p.paused) if (b != null) b.enabled = false;
        }

        // The head starts in the middle of the area, heading toward the nearest pixel.
        Vector2 centre = (arenaMin + arenaMax) * 0.5f;
        headPos = new Vector3(centre.x, groundY, centre.y);
        Piece nearest = null;
        float best = float.MaxValue;
        foreach (Piece p in targets)
        {
            float d = FlatDistance(headPos, p.body.position);
            if (d < best) { best = d; nearest = p; }
        }
        int startDir = 3;
        if (nearest != null)
        {
            Vector3 to = nearest.body.position - headPos;
            float bestDot = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float dot = Vector3.Dot(dirs[i], to);
                if (dot > bestDot) { bestDot = dot; startDir = i; }
            }
        }
        headDir = dirs[startDir];
        wantedDir = -1;
        currentSpeed = speed;
        timeLeft = timeLimit;
        trail.Add(headPos);

        BuildHead();
        BuildUi();
        activeRound = true;
        Active = true;
        TakeoverActive = true;
        if (!PixelClicker.ExternalClickBlock) { PixelClicker.ExternalClickBlock = true; setBlock = true; } // no cube clicks during the round
        Report(MinigameEvent.Spawned);
        PixelAudio.Play("snake_start");
        return true;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private void BuildHead()
    {
        head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Snake Head";
        Track(head);
        Destroy(head.GetComponent<Collider>());
        head.transform.localScale = Vector3.one * (unit * headScale);
        head.transform.position = headPos + Vector3.up * (unit * (headScale - 1f) * 0.5f);
        Material m = clicker.CreateVisualMaterial(headColor, false);
        if (m != null)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", headColor * 0.8f);
            head.GetComponent<Renderer>().sharedMaterial = m;
        }

        // Two little eyes on the side it is facing.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(head.transform, false);
            eye.transform.localScale = Vector3.one * 0.22f;
            eye.transform.localPosition = new Vector3(0.22f * side, 0.18f, 0.5f);
            Material eyeMat = clicker.CreateVisualMaterial(Color.black, false);
            if (eyeMat != null) eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
        }
    }

    // ------------------------------------------------------------------
    // The round
    // ------------------------------------------------------------------

    private void Tick(float dt)
    {
        timeLeft -= dt;
        if (timeLeft <= 0f) { EndRound(false, timeoutText); return; }

        ReadInput();
        if (wantedDir >= 0)
        {
            // No turning straight back on yourself.
            if (Vector3.Dot(dirs[wantedDir], headDir) > -0.5f) headDir = dirs[wantedDir];
            wantedDir = -1;
        }

        // Move, stopped by the edges of the area.
        Vector3 next = headPos + headDir * (currentSpeed * unit * dt);
        next.x = Mathf.Clamp(next.x, arenaMin.x, arenaMax.x);
        next.z = Mathf.Clamp(next.z, arenaMin.y, arenaMax.y);
        next.y = groundY;
        headPos = next;
        if (FlatDistance(headPos, trail[trail.Count - 1]) >= unit * segmentSpacing * 0.12f) trail.Add(headPos);

        if (head != null)
        {
            head.transform.position = headPos + Vector3.up * (unit * (headScale - 1f) * 0.5f);
            if (headDir.sqrMagnitude > 0.01f) head.transform.rotation = Quaternion.LookRotation(headDir, Vector3.up);
        }

        // Eat what it touches.
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            Piece p = targets[i];
            if (p.body == null) { targets.RemoveAt(i); totalPixels--; continue; }
            if (FlatDistance(headPos, p.body.position) <= eatDistance * unit) Eat(i);
        }

        PlaceBody();

        // Biting yourself.
        for (int i = safeSegments; i < bodyPieces.Count; i++)
            if (bodyPieces[i].tf != null && FlatDistance(headPos, bodyPieces[i].tf.position) < biteDistance * unit)
            {
                EndRound(false, biteText);
                return;
            }

        if (targets.Count == 0) { EndRound(true, null); return; }

        if (statusLabel != null)
            PixelUIKit.SetText(statusLabel, string.Format(statusFormat, bodyPieces.Count, totalPixels, Mathf.CeilToInt(Mathf.Max(0f, timeLeft))));
    }

    private void Eat(int targetIndex)
    {
        Piece p = targets[targetIndex];
        targets.RemoveAt(targetIndex);
        bodyPieces.Add(p);
        foreach (Collider c in p.colliders) if (c != null) c.enabled = false;
        p.tf.rotation = Quaternion.identity;
        currentSpeed += speedGain;
        PixelAudio.Play("snake_eat");
    }

    /// <summary>Lays the eaten pixels along the path the head took, one segment apart.</summary>
    private void PlaceBody()
    {
        if (bodyPieces.Count == 0) return;
        float spacing = unit * segmentSpacing;
        int placed = 0;
        float accumulated = 0f;
        Vector3 previous = headPos;
        for (int j = trail.Count - 1; j >= 0 && placed < bodyPieces.Count; j--)
        {
            Vector3 current = trail[j];
            float length = FlatDistance(previous, current);
            while (placed < bodyPieces.Count && length > 0.0001f && accumulated + length >= (placed + 1) * spacing)
            {
                float t = ((placed + 1) * spacing - accumulated) / length;
                SetPiece(bodyPieces[placed], Vector3.Lerp(previous, current, t));
                placed++;
            }
            accumulated += length;
            previous = current;
        }
        for (; placed < bodyPieces.Count; placed++) SetPiece(bodyPieces[placed], previous); // the tail is longer than the trail so far

        // Forget the part of the trail the tail has already passed.
        float needed = (bodyPieces.Count + 2) * spacing;
        if (accumulated > needed * 1.5f && trail.Count > 8) trail.RemoveRange(0, Mathf.Min(trail.Count / 4, trail.Count - 4));
    }

    private void SetPiece(Piece p, Vector3 position)
    {
        if (p.tf == null) return;
        p.tf.position = new Vector3(position.x, groundY, position.z);
    }

    // ------------------------------------------------------------------
    // Ending
    // ------------------------------------------------------------------

    /// <summary>Ends the round. 'win' pays out; otherwise the pixels drop and everything goes back to normal.</summary>
    private void EndRound(bool win, string message)
    {
        activeRound = false;
        Active = false;
        TakeoverActive = false;
        OldPixelDespawn.HoldAll = false;
        if (setBlock) { PixelClicker.ExternalClickBlock = PixelBank.HoseOn; setBlock = false; }

        Vector3 messageAt = headPos;
        double payout = 0d;
        List<Piece> all = new List<Piece>(bodyPieces);
        all.AddRange(targets);

        foreach (Piece p in all)
        {
            if (p.body == null) continue;
            foreach (Collider c in p.colliders) if (c != null) c.enabled = true;
            foreach (Behaviour b in p.paused) if (b != null) b.enabled = true;
            p.body.isKinematic = false;
            if (win && bodyPieces.Contains(p))
            {
                payout += p.amount * rewardMultiplier;
                clicker.AddCurrency(p.tier, p.amount * rewardMultiplier);
                if (p.despawn != null) p.despawn.Begin(); // the eaten pixels shrink away
            }
            else
            {
                // They simply drop and age normally again.
                SetVelocities(p.body, Vector3.zero);
                if (p.despawn != null) p.despawn.AddLifetime(3f);
            }
        }

        targets.Clear();
        bodyPieces.Clear();
        trail.Clear();
        if (head != null) Destroy(head);
        head = null;
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = null;
        statusLabel = null;

        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));

        if (win)
        {
            Report(MinigameEvent.Clicked);
            PixelAudio.Play("snake_win");
            ShowMessage(messageAt, string.Format(winFormat, PixelClicker.FormatNumber(rewardMultiplier)) + "\n+" + PixelClicker.FormatNumber(payout));
        }
        else if (!string.IsNullOrEmpty(message))
        {
            PixelAudio.Play("snake_lose");
            ShowMessage(messageAt, message);
        }
    }

    private void ShowMessage(Vector3 at, string text)
    {
        Camera c = cam != null ? cam : Camera.main;
        if (c != null) StartCoroutine(MessageRoutine(at, c, text));
    }

    private IEnumerator MessageRoutine(Vector3 start, Camera c, string message)
    {
        GameObject go = new GameObject("Snake Message");
        Track(go);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = messageTextSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        if (clicker.UIFont != null) text.font = clicker.UIFont;
        text.rectTransform.sizeDelta = new Vector2(14f, 4f);

        float t = 0f;
        while (t < messageSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / messageSeconds);
            go.transform.position = PixelUIKit.KeepOnScreen(c, text, start + Vector3.up * (1.2f + k * 1.2f));
            go.transform.rotation = c.transform.rotation;
            text.alpha = 1f - k * k;
            yield return null;
        }
        Destroy(go);
    }

    // ------------------------------------------------------------------
    // Input and the on-screen arrows
    // ------------------------------------------------------------------

    private void ReadInput()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.upArrow.wasPressedThisFrame) wantedDir = 0;
        else if (kb.downArrow.wasPressedThisFrame) wantedDir = 1;
        else if (kb.leftArrow.wasPressedThisFrame) wantedDir = 2;
        else if (kb.rightArrow.wasPressedThisFrame) wantedDir = 3;
#else
        if (Input.GetKeyDown(KeyCode.UpArrow)) wantedDir = 0;
        else if (Input.GetKeyDown(KeyCode.DownArrow)) wantedDir = 1;
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) wantedDir = 2;
        else if (Input.GetKeyDown(KeyCode.RightArrow)) wantedDir = 3;
#endif
    }

    private void BuildUi()
    {
        TMP_FontAsset font = clicker.UIFont;
        uiRoot = PixelUIKit.CreateCanvas("Snake UI", 125, new Vector2(1920f, 1080f), true);
        uiRoot.transform.SetParent(transform, false);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;

        statusLabel = PixelUIKit.CreateText(font, uiRoot.transform, "Status", "", statusFontSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(900f, statusFontSize * 1.5f);
        sr.anchoredPosition = new Vector2(0f, -(bar + 12f));

        if (!showArrowButtons) return;
        float s = arrowButtonSize, gap = 10f;
        float cx = -(s * 1.5f + gap * 2f + 60f); // centre column of the arrow pad (from the right edge)
        float baseY = bar + 40f;
        MakeArrow(font, "Up", "^", 0, new Vector2(cx, baseY + s + gap));
        MakeArrow(font, "Left", "<", 2, new Vector2(cx - s - gap, baseY));
        MakeArrow(font, "Down", "v", 1, new Vector2(cx, baseY));
        MakeArrow(font, "Right", ">", 3, new Vector2(cx + s + gap, baseY));
    }

    private void MakeArrow(TMP_FontAsset font, string name, string label, int dir, Vector2 position)
    {
        Button b = PixelUIKit.CreateButton(font, uiRoot.transform, name + " Arrow", label, new Vector2(arrowButtonSize, arrowButtonSize),
                                           new Color(0.18f, 0.2f, 0.26f, 0.9f), Color.white, arrowButtonSize * 0.55f);
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
        r.anchoredPosition = position;
        b.onClick.AddListener(() => wantedDir = dir);
    }

    // ------------------------------------------------------------------

    private static void SetVelocities(Rigidbody rb, Vector3 v)
    {
        if (rb == null || rb.isKinematic) return;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }
}
