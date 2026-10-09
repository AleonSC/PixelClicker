using System.Collections;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using static PixelInput;

/// <summary>
/// Breakout minigame for Pixel Clicker.
///
/// Every so often (once bought in the shop's Minigames tab) a wall of coloured bricks appears in front of the screen and the cube
/// steps out of the way. Move the paddle along the bottom with the mouse (or Left / Right arrows) to bounce a spinning pixel into
/// the bricks. Break every brick before you run out of balls and you are paid a multiple of that pixel's worth. Lose all your
/// balls, or run out of time, and nothing is lost. The court is drawn in a plane facing the camera. Add this to any GameObject
/// (PixelShop adds it automatically if it is missing).
/// </summary>
public class PixelBreakoutMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, old pixels, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Breakout is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame is switched on before the first game can start.")]
    [SerializeField] private float firstRoundDelay = 75f;

    [Min(1f)]
    [Tooltip("Shortest wait between games (seconds).")]
    [SerializeField] private float minInterval = 150f;

    [Min(1f)]
    [Tooltip("Longest wait between games (seconds).")]
    [SerializeField] private float maxInterval = 300f;

    [Min(1)]
    [Tooltip("A game only starts when at least this many old pixels are lying around (one of them lends its type to the ball).")]
    [SerializeField] private int minPixels = 3;

    [Header("Rules")]
    [Min(1)]
    [Tooltip("Balls you get. Missing the ball costs one.")]
    [SerializeField] private int lives = 3;

    [Min(10f)]
    [Tooltip("Seconds before the game is called off.")]
    [SerializeField] private float timeLimit = 150f;

    [Min(1f)]
    [Tooltip("Clearing every brick pays the ball pixel's worth times this.")]
    [SerializeField] private float rewardMultiplier = 20f;

    [Tooltip("Hide the clickable cube while playing (it grows back afterwards).")]
    [SerializeField] private bool hideCube = true;

    [Header("Court")]
    [Range(0.3f, 0.9f)]
    [Tooltip("Court width as a fraction of the screen width.")]
    [SerializeField] private float courtWidth = 0.5f;

    [Range(0.2f, 0.8f)]
    [Tooltip("Court height as a fraction of the screen height.")]
    [SerializeField] private float courtHeight = 0.6f;

    [Range(0.3f, 0.9f)]
    [Tooltip("Where the court's centre sits vertically on the screen (0 = bottom, 1 = top).")]
    [SerializeField] private float courtCentreY = 0.5f;

    [Range(0.2f, 0.9f)]
    [Tooltip("How far in front of the camera the court floats, as a fraction of the distance to the cube (it must be in front of the old pixels).")]
    [SerializeField] private float depthFraction = 0.6f;

    [Min(3)]
    [SerializeField] private int brickColumns = 9;

    [Min(1)]
    [SerializeField] private int brickRows = 5;

    [Header("Play")]
    [Range(0.05f, 0.6f)]
    [Tooltip("Paddle width as a fraction of the court width.")]
    [SerializeField] private float paddleWidth = 0.2f;

    [Range(0.2f, 3f)]
    [Tooltip("Starting ball speed (court heights per second).")]
    [SerializeField] private float ballSpeed = 0.8f;

    [Min(0f)]
    [Tooltip("The ball gets this much faster (court heights per second) on every brick it breaks.")]
    [SerializeField] private float speedUpPerBrick = 0.012f;

    [Range(0.2f, 3f)]
    [Tooltip("Top ball speed (court heights per second).")]
    [SerializeField] private float maxBallSpeed = 1.6f;

    [Range(0.02f, 0.2f)]
    [Tooltip("Ball size as a fraction of the court height.")]
    [SerializeField] private float ballSize = 0.06f;

    [Range(20f, 75f)]
    [Tooltip("Steepest angle (degrees) off straight up the paddle can send the ball.")]
    [SerializeField] private float maxBounceAngle = 60f;

    [Min(0f)]
    [Tooltip("Pause before each serve (seconds).")]
    [SerializeField] private float serveDelay = 1f;

    [Header("Look")]
    [SerializeField] private Color paddleColor = new Color(0.4f, 0.9f, 1f, 1f);
    [Tooltip("Brick colours, one per row (repeats if there are more rows). Empty = the colours of your unlocked pixel types.")]
    [SerializeField] private Color[] brickColors = new Color[0];
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.55f);

    [Min(10)]
    [Tooltip("Size of the score text.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Status line. {0} = bricks left, {1} = balls left, {2} = seconds left.")]
    [SerializeField] private string statusFormat = "BREAKOUT   bricks {0}   balls {1}   {2}s";

    [SerializeField] private string winFormat = "Cleared!";
    [SerializeField] private string loseText = "Out of balls";
    [SerializeField] private string timeoutText = "Time's up";

    [Min(10f)]
    [SerializeField] private float messageTextSize = 5f;

    [Min(0.5f)]
    [SerializeField] private float messageSeconds = 2.2f;

    private class Brick { public Transform tf; public int row; public bool alive; public Vector2 pos; }

    private float spawnTimer, timeLeft, serveTimer;
    private bool activeRound, setBlock;
    private Camera cam;
    private GameObject root, uiRoot;
    private Transform paddle, ball, ballModel;
    private TMP_Text statusLabel;
    private float halfW, halfH, padHalfW, padH, ballR, speed, padY;
    private float padX;
    private Vector2 ballPos, ballVel;
    private int ballsLeft, bricksLeft, ballTier;
    private double ballAmount;
    private Material flat;
    private readonly System.Collections.Generic.List<Brick> bricks = new System.Collections.Generic.List<Brick>();
    private Vector2 brickSize;

    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBreakoutStatics() { Active = false; }

    public override string Id => "breakout";
    public override string DisplayName => "Breakout";
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
            Debug.LogError("PixelBreakoutMinigame: no PixelClicker found in the scene.", this);
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
        if (activeRound) EndRound(false, null);
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    [ContextMenu("Start A Breakout Game Now")]
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
        spawnTimer = TryStart(false) ? 0f : 12f;
    }

    // ------------------------------------------------------------------
    // Starting
    // ------------------------------------------------------------------

    private bool TryStart(bool dev)
    {
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;

        // One of the old pixels lends the ball its type and worth.
        var all = clicker.OldPixels;
        int count = 0;
        OldPixelInfo pick = null;
        for (int i = 0; i < all.Count; i++)
        {
            Rigidbody rb = all[i];
            if (rb == null || clicker.IsFlyingPixel(rb)) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            count++;
            if (pick == null || Random.value < 1f / count) pick = info;
        }
        if (pick == null || count < (dev ? 1 : minPixels)) return false;
        ballTier = pick.tierIndex;
        ballAmount = pick.amount;

        // The court floats in a plane facing the camera, in front of the old pixels.
        Transform target = clicker.PixelTransform;
        float dist = target != null ? Vector3.Distance(cam.transform.position, target.position) * depthFraction : 6f;
        dist = Mathf.Max(cam.nearClipPlane + 0.5f, dist);
        Vector3 centre = cam.ViewportToWorldPoint(new Vector3(0.5f, courtCentreY, dist));
        Vector3 left = cam.ViewportToWorldPoint(new Vector3(0.5f - courtWidth * 0.5f, courtCentreY, dist));
        Vector3 top = cam.ViewportToWorldPoint(new Vector3(0.5f, courtCentreY + courtHeight * 0.5f, dist));
        halfW = Vector3.Dot(centre - left, cam.transform.right);
        halfH = Vector3.Dot(top - centre, cam.transform.up);
        if (halfW <= 0.01f || halfH <= 0.01f) return false;

        root = new GameObject("Breakout Court");
        Track(root);
        root.transform.SetPositionAndRotation(centre, cam.transform.rotation);
        BuildCourt();

        ballsLeft = lives;
        padX = 0f;
        timeLeft = timeLimit;
        Serve();
        BuildUi();

        if (hideCube) clicker.SetCubeShrink(0.02f, 0.5f);
        OldPixelDespawn.HoldAll = true;
        activeRound = true;
        Active = true;
        TakeoverActive = true;
        if (!PixelClicker.ExternalClickBlock) { PixelClicker.ExternalClickBlock = true; setBlock = true; }
        Report(MinigameEvent.Spawned);
        PixelAudio.Play("breakout_start");
        return true;
    }

    private Material Flat(Color c)
    {
        if (flat == null)
        {
            Shader s = PixelShaders.SpriteDefault();
            if (s == null) return null;
            flat = new Material(s);
        }
        Material m = new Material(flat);
        if (m.HasProperty("_Color")) m.color = c;
        return m;
    }

    private Transform Block(string name, Vector2 pos, Vector2 size, Color c, float z)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(root.transform, false);
        g.transform.localPosition = new Vector3(pos.x, pos.y, z);
        g.transform.localScale = new Vector3(size.x, size.y, 0.01f);
        Renderer r = g.GetComponent<Renderer>();
        Material m = Flat(c);
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }

    private Color RowColour(int row)
    {
        if (brickColors != null && brickColors.Length > 0) return brickColors[row % brickColors.Length];
        var tiers = clicker.Tiers;
        int n = 0;
        for (int i = 0; i < tiers.Length; i++) if (tiers[i].unlocked) n++;
        if (n == 0) return Color.white;
        int want = row % n;
        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i].unlocked && want-- == 0) return tiers[i].color;
        return Color.white;
    }

    private void BuildCourt()
    {
        float line = halfH * 0.03f;
        padHalfW = halfW * paddleWidth;
        padH = halfH * 0.05f;
        padY = -halfH + padH * 3f;
        ballR = halfH * ballSize * 0.5f;

        Block("Back", Vector2.zero, new Vector2(halfW * 2f, halfH * 2f), backColor, 0.05f);
        Block("Top", new Vector2(0f, halfH), new Vector2(halfW * 2f, line), lineColor, 0f);
        Block("Left", new Vector2(-halfW, 0f), new Vector2(line, halfH * 2f), lineColor, 0f);
        Block("Right", new Vector2(halfW, 0f), new Vector2(line, halfH * 2f), lineColor, 0f);
        paddle = Block("Paddle", new Vector2(0f, padY), new Vector2(padHalfW * 2f, padH), paddleColor, -0.02f);

        // The wall of bricks across the top.
        bricks.Clear();
        float gap = halfW * 0.02f;
        float areaW = halfW * 2f * 0.94f;
        brickSize = new Vector2(areaW / brickColumns - gap, halfH * 0.5f / brickRows - gap);
        for (int r = 0; r < brickRows; r++)
        {
            Color c = RowColour(r);
            for (int col = 0; col < brickColumns; col++)
            {
                Vector2 p = new Vector2(-areaW * 0.5f + (col + 0.5f) * (areaW / brickColumns), halfH * 0.88f - (r + 0.5f) * (halfH * 0.5f / brickRows));
                Transform t = Block("Brick", p, brickSize, c, 0f);
                bricks.Add(new Brick { tf = t, row = r, alive = true, pos = p });
            }
        }
        bricksLeft = bricks.Count;

        GameObject b = new GameObject("Ball");
        b.transform.SetParent(root.transform, false);
        ball = b.transform;
        GameObject model = clicker.CreateDisplayPixel(ballTier, ball, ballR * 2f);
        if (model != null) ballModel = model.transform;
        else
        {
            Transform fallback = Block("Ball Block", Vector2.zero, Vector2.one * ballR * 2f, Color.white, -0.03f);
            fallback.SetParent(ball, false);
        }
    }

    private void Serve()
    {
        speed = Mathf.Max(speed, ballSpeed);
        ballPos = new Vector2(padX, padY + padH * 0.5f + ballR);
        float angle = Random.Range(-25f, 25f) * Mathf.Deg2Rad;
        ballVel = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (speed * halfH * 2f);
        serveTimer = serveDelay;
    }

    // ------------------------------------------------------------------
    // The game
    // ------------------------------------------------------------------

    private void Tick(float dt)
    {
        timeLeft -= dt;
        if (timeLeft <= 0f) { EndRound(false, timeoutText); return; }

        // Paddle: the mouse position projected onto the court plane, or the arrow keys.
        float key = ReadKeys();
        if (Mathf.Abs(key) > 0.01f) padX += key * halfW * 2f * 0.8f * dt;
        else if (cam != null && !PointerOverUI())
        {
            Ray ray = cam.ScreenPointToRay(PointerPosition());
            Plane plane = new Plane(-cam.transform.forward, root.transform.position);
            if (plane.Raycast(ray, out float enter))
                padX = root.transform.InverseTransformPoint(ray.GetPoint(enter)).x;
        }
        padX = Mathf.Clamp(padX, -halfW + padHalfW, halfW - padHalfW);

        if (serveTimer > 0f)
        {
            serveTimer -= dt;
            ballPos = new Vector2(padX, padY + padH * 0.5f + ballR); // rides on the paddle until the serve
        }
        else MoveBall(dt);

        Place();
        UpdateStatus();
    }

    private void MoveBall(float dt)
    {
        ballPos += ballVel * dt;

        // Walls and ceiling.
        if (ballPos.x > halfW - ballR) { ballPos.x = halfW - ballR; ballVel.x = -Mathf.Abs(ballVel.x); }
        else if (ballPos.x < -halfW + ballR) { ballPos.x = -halfW + ballR; ballVel.x = Mathf.Abs(ballVel.x); }
        if (ballPos.y > halfH - ballR) { ballPos.y = halfH - ballR; ballVel.y = -Mathf.Abs(ballVel.y); }

        // Paddle.
        if (ballVel.y < 0f && ballPos.y - ballR <= padY + padH * 0.5f && ballPos.y > padY - padH)
        {
            if (Mathf.Abs(ballPos.x - padX) <= padHalfW + ballR)
            {
                float k = Mathf.Clamp((ballPos.x - padX) / padHalfW, -1f, 1f);
                float angle = k * maxBounceAngle * Mathf.Deg2Rad;
                float v = speed * halfH * 2f;
                ballVel = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * v;
                ballPos.y = padY + padH * 0.5f + ballR;
                PixelAudio.Play("breakout_hit");
            }
        }

        // Bricks: the first one the ball overlaps breaks and reflects it.
        for (int i = 0; i < bricks.Count; i++)
        {
            Brick b = bricks[i];
            if (!b.alive) continue;
            float dx = ballPos.x - b.pos.x, dy = ballPos.y - b.pos.y;
            float ox = brickSize.x * 0.5f + ballR - Mathf.Abs(dx);
            float oy = brickSize.y * 0.5f + ballR - Mathf.Abs(dy);
            if (ox <= 0f || oy <= 0f) continue;
            if (ox < oy) ballVel.x = Mathf.Abs(ballVel.x) * Mathf.Sign(dx);
            else ballVel.y = Mathf.Abs(ballVel.y) * Mathf.Sign(dy);
            b.alive = false;
            if (b.tf != null) b.tf.gameObject.SetActive(false);
            bricksLeft--;
            speed = Mathf.Min(maxBallSpeed, speed + speedUpPerBrick);
            ballVel = ballVel.normalized * (speed * halfH * 2f);
            PixelAudio.Play("breakout_brick");
            if (bricksLeft <= 0) { EndRound(true, null); return; }
            break;
        }

        // Missed.
        if (ballPos.y < -halfH - ballR)
        {
            ballsLeft--;
            PixelAudio.Play("breakout_miss");
            if (ballsLeft <= 0) { EndRound(false, loseText); return; }
            Serve();
        }
    }

    private void Place()
    {
        if (paddle != null) paddle.localPosition = new Vector3(padX, padY, paddle.localPosition.z);
        if (ball != null) ball.localPosition = new Vector3(ballPos.x, ballPos.y, -0.03f);
        if (ballModel != null) ballModel.localRotation = Quaternion.Euler(Time.unscaledTime * 40f, Time.unscaledTime * 70f, 0f);
    }

    private void UpdateStatus()
    {
        if (statusLabel == null) return;
        PixelUIKit.SetText(statusLabel, string.Format(statusFormat, bricksLeft, ballsLeft, Mathf.CeilToInt(Mathf.Max(0f, timeLeft))));
    }

    private static float ReadKeys()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return 0f;
        return (kb.rightArrow.isPressed ? 1f : 0f) - (kb.leftArrow.isPressed ? 1f : 0f);
#else
        return (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
#endif
    }

    // ------------------------------------------------------------------
    // Ending
    // ------------------------------------------------------------------

    private void EndRound(bool win, string message)
    {
        activeRound = false;
        Active = false;
        TakeoverActive = false;
        OldPixelDespawn.HoldAll = false;
        if (hideCube) clicker.SetCubeShrink(1f, 0.6f);
        if (setBlock) { PixelClicker.ExternalClickBlock = PixelBank.HoseOn; setBlock = false; }

        Vector3 at = root != null ? root.transform.position : Vector3.zero;
        Quaternion rot = root != null ? root.transform.rotation : Quaternion.identity;
        double payout = ballAmount * rewardMultiplier;
        if (root != null) Destroy(root);
        root = null;
        paddle = ball = ballModel = null;
        bricks.Clear();
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = null;
        statusLabel = null;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));

        if (win)
        {
            clicker.AddCurrency(ballTier, payout);
            Report(MinigameEvent.Clicked);
            PixelAudio.Play("breakout_win");
            ShowMessage(at, rot, winFormat + "\n+" + PixelClicker.FormatNumber(payout));
        }
        else if (!string.IsNullOrEmpty(message))
        {
            PixelAudio.Play("breakout_lose");
            ShowMessage(at, rot, message);
        }
    }

    private void ShowMessage(Vector3 at, Quaternion rot, string text)
    {
        StartCoroutine(MessageRoutine(at, rot, text));
    }

    private IEnumerator MessageRoutine(Vector3 start, Quaternion rot, string message)
    {
        GameObject go = new GameObject("Breakout Message");
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
            go.transform.position = PixelUIKit.KeepOnScreen(cam != null ? cam : Camera.main, text, start + rot * Vector3.up * (k * 0.8f));
            go.transform.rotation = rot;
            text.alpha = 1f - k * k;
            yield return null;
        }
        Destroy(go);
    }

    private void BuildUi()
    {
        uiRoot = PixelUIKit.CreateCanvas("Breakout UI", 125, new Vector2(1920f, 1080f), true);
        uiRoot.transform.SetParent(transform, false);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        statusLabel = PixelUIKit.CreateText(clicker.UIFont, uiRoot.transform, "Status", "", statusFontSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(900f, statusFontSize * 1.5f);
        sr.anchoredPosition = new Vector2(0f, -(bar + 12f));
        UpdateStatus();
    }
}
