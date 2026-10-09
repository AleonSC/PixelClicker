using System.Collections;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using static PixelInput;

/// <summary>
/// Pong minigame for Pixel Clicker.
///
/// Every so often (once bought in the shop's Minigames tab) a Pong court appears in front of the screen and the cube steps out of
/// the way. Move your paddle (left) with the mouse or the Up / Down arrows; the ball is a spinning pixel taken from the ones lying
/// around. First to the winning score takes it: you are paid a multiple of that pixel's worth. Lose, or run out of time, and
/// nothing is lost. The court is drawn in a plane facing the camera, so it works at any camera angle. Add this to any GameObject
/// (PixelShop adds it automatically if it is missing).
/// </summary>
public class PixelPongMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, old pixels, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Pong is bought.)")]
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
    [Tooltip("First to this many points wins.")]
    [SerializeField] private int pointsToWin = 5;

    [Min(10f)]
    [Tooltip("Seconds before the game is called off.")]
    [SerializeField] private float timeLimit = 150f;

    [Min(1f)]
    [Tooltip("A win pays the ball pixel's worth times this.")]
    [SerializeField] private float rewardMultiplier = 20f;

    [Tooltip("Hide the clickable cube while playing (it grows back afterwards).")]
    [SerializeField] private bool hideCube = true;

    [Header("Court")]
    [Range(0.3f, 0.9f)]
    [Tooltip("Court width as a fraction of the screen width.")]
    [SerializeField] private float courtWidth = 0.6f;

    [Range(0.2f, 0.7f)]
    [Tooltip("Court height as a fraction of the screen height.")]
    [SerializeField] private float courtHeight = 0.48f;

    [Range(0.4f, 0.9f)]
    [Tooltip("Where the court's centre sits vertically on the screen (0 = bottom, 1 = top).")]
    [SerializeField] private float courtCentreY = 0.52f;

    [Range(0.2f, 0.9f)]
    [Tooltip("How far in front of the camera the court floats, as a fraction of the distance to the cube (it must be in front of the old pixels).")]
    [SerializeField] private float depthFraction = 0.6f;

    [Header("Play")]
    [Range(0.05f, 0.5f)]
    [Tooltip("Paddle height as a fraction of the court height.")]
    [SerializeField] private float paddleHeight = 0.24f;

    [Range(0.2f, 3f)]
    [Tooltip("Starting ball speed (court heights per second).")]
    [SerializeField] private float ballSpeed = 0.9f;

    [Min(0f)]
    [Tooltip("The ball gets this much faster (court heights per second) on every paddle hit.")]
    [SerializeField] private float speedUpPerHit = 0.06f;

    [Range(0.2f, 3f)]
    [Tooltip("Top ball speed (court heights per second).")]
    [SerializeField] private float maxBallSpeed = 2f;

    [Range(0.1f, 3f)]
    [Tooltip("How fast the computer's paddle can move (court heights per second). Lower is easier.")]
    [SerializeField] private float aiSpeed = 0.7f;

    [Range(0f, 1f)]
    [Tooltip("How sloppy the computer is: it aims off by up to this fraction of half a paddle.")]
    [SerializeField] private float aiError = 0.6f;

    [Range(0.02f, 0.2f)]
    [Tooltip("Ball size as a fraction of the court height.")]
    [SerializeField] private float ballSize = 0.07f;

    [Range(20f, 80f)]
    [Tooltip("Steepest angle (degrees) a paddle can send the ball off at.")]
    [SerializeField] private float maxBounceAngle = 55f;

    [Min(0f)]
    [Tooltip("Pause before each serve (seconds).")]
    [SerializeField] private float serveDelay = 1f;

    [Header("Look")]
    [SerializeField] private Color playerColor = new Color(0.4f, 0.9f, 1f, 1f);
    [SerializeField] private Color aiColor = new Color(1f, 0.55f, 0.4f, 1f);
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.55f);

    [Min(10)]
    [Tooltip("Size of the score text.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Score line. {0} = your points, {1} = the computer's, {2} = points to win, {3} = seconds left.")]
    [SerializeField] private string statusFormat = "PONG   {0} - {1}   first to {2}   {3}s";

    [SerializeField] private string winFormat = "You win!";
    [SerializeField] private string loseText = "The computer wins";
    [SerializeField] private string timeoutText = "Time's up";

    [Min(10f)]
    [SerializeField] private float messageTextSize = 5f;

    [Min(0.5f)]
    [SerializeField] private float messageSeconds = 2.2f;

    private float spawnTimer, timeLeft, serveTimer;
    private bool activeRound, setBlock;
    private Camera cam;
    private GameObject root, uiRoot;
    private Transform playerPad, aiPad, ball, ballModel;
    private TMP_Text statusLabel;
    private float halfW, halfH, padHalf, padW, ballR, speed;
    private float playerY, aiY, aiAim;
    private Vector2 ballPos, ballVel;
    private int playerScore, aiScore, ballTier;
    private double ballAmount;
    private Material flat;

    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPongStatics() { Active = false; }

    public override string Id => "pong";
    public override string DisplayName => "Pong";
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
            Debug.LogError("PixelPongMinigame: no PixelClicker found in the scene.", this);
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

    [ContextMenu("Start A Pong Game Now")]
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

        root = new GameObject("Pong Court");
        Track(root);
        root.transform.SetPositionAndRotation(centre, cam.transform.rotation);
        BuildCourt();

        playerScore = aiScore = 0;
        playerY = aiY = 0f;
        timeLeft = timeLimit;
        Serve(Random.value < 0.5f ? 1 : -1);
        BuildUi();

        if (hideCube) clicker.SetCubeShrink(0.02f, 0.5f);
        OldPixelDespawn.HoldAll = true;
        activeRound = true;
        Active = true;
        TakeoverActive = true;
        if (!PixelClicker.ExternalClickBlock) { PixelClicker.ExternalClickBlock = true; setBlock = true; }
        Report(MinigameEvent.Spawned);
        PixelAudio.Play("pong_start");
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

    private void BuildCourt()
    {
        float line = halfH * 0.03f;
        padHalf = halfH * paddleHeight;
        padW = halfH * 0.06f;
        ballR = halfH * ballSize * 0.5f;

        Block("Back", Vector2.zero, new Vector2(halfW * 2f, halfH * 2f), backColor, 0.05f);
        Block("Top", new Vector2(0f, halfH), new Vector2(halfW * 2f, line), lineColor, 0f);
        Block("Bottom", new Vector2(0f, -halfH), new Vector2(halfW * 2f, line), lineColor, 0f);
        int dashes = 9;
        for (int i = 0; i < dashes; i++)
        {
            float y = Mathf.Lerp(-halfH * 0.9f, halfH * 0.9f, i / (float)(dashes - 1));
            Block("Dash", new Vector2(0f, y), new Vector2(line, halfH * 0.1f), lineColor, 0f);
        }
        playerPad = Block("Player", new Vector2(-halfW + padW * 2f, 0f), new Vector2(padW, padHalf * 2f), playerColor, -0.02f);
        aiPad = Block("Computer", new Vector2(halfW - padW * 2f, 0f), new Vector2(padW, padHalf * 2f), aiColor, -0.02f);

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

    private void Serve(int direction)
    {
        speed = ballSpeed;
        ballPos = Vector2.zero;
        float angle = Random.Range(-25f, 25f) * Mathf.Deg2Rad;
        ballVel = new Vector2(Mathf.Cos(angle) * direction, Mathf.Sin(angle)) * (speed * halfH * 2f);
        serveTimer = serveDelay;
        aiAim = Random.Range(-1f, 1f) * aiError * padHalf;
    }

    // ------------------------------------------------------------------
    // The game
    // ------------------------------------------------------------------

    private void Tick(float dt)
    {
        timeLeft -= dt;
        if (timeLeft <= 0f) { EndRound(false, timeoutText); return; }

        // Player: mouse position projected onto the court plane, or the arrow keys.
        float key = ReadKeys();
        if (Mathf.Abs(key) > 0.01f) playerY += key * halfH * 2f * 0.9f * dt;
        else if (cam != null && !PointerOverUI())
        {
            Ray ray = cam.ScreenPointToRay(PointerPosition());
            Plane plane = new Plane(-cam.transform.forward, root.transform.position);
            if (plane.Raycast(ray, out float enter))
                playerY = root.transform.InverseTransformPoint(ray.GetPoint(enter)).y;
        }
        playerY = Mathf.Clamp(playerY, -halfH + padHalf, halfH - padHalf);

        // Computer: chases the ball (only when it comes its way), limited speed.
        float aiTarget = ballVel.x > 0f ? ballPos.y + aiAim : 0f;
        aiY = Mathf.MoveTowards(aiY, aiTarget, aiSpeed * halfH * 2f * dt);
        aiY = Mathf.Clamp(aiY, -halfH + padHalf, halfH - padHalf);

        if (serveTimer > 0f) serveTimer -= dt;
        else MoveBall(dt);

        Place();
        UpdateStatus();
    }

    private void MoveBall(float dt)
    {
        ballPos += ballVel * dt;

        // Walls.
        if (ballPos.y > halfH - ballR) { ballPos.y = halfH - ballR; ballVel.y = -Mathf.Abs(ballVel.y); }
        else if (ballPos.y < -halfH + ballR) { ballPos.y = -halfH + ballR; ballVel.y = Mathf.Abs(ballVel.y); }

        // Paddles.
        float playerX = -halfW + padW * 2f, aiX = halfW - padW * 2f;
        if (ballVel.x < 0f && ballPos.x - ballR <= playerX + padW * 0.5f && ballPos.x >= playerX - padW)
        {
            if (Mathf.Abs(ballPos.y - playerY) <= padHalf + ballR) Bounce(playerX + padW * 0.5f + ballR, playerY, 1);
        }
        else if (ballVel.x > 0f && ballPos.x + ballR >= aiX - padW * 0.5f && ballPos.x <= aiX + padW)
        {
            if (Mathf.Abs(ballPos.y - aiY) <= padHalf + ballR) Bounce(aiX - padW * 0.5f - ballR, aiY, -1);
        }

        // Scoring.
        if (ballPos.x < -halfW - ballR) Score(false);
        else if (ballPos.x > halfW + ballR) Score(true);
    }

    private void Bounce(float x, float padY, int direction)
    {
        float k = Mathf.Clamp((ballPos.y - padY) / padHalf, -1f, 1f);
        float angle = k * maxBounceAngle * Mathf.Deg2Rad;
        speed = Mathf.Min(maxBallSpeed, speed + speedUpPerHit);
        float v = speed * halfH * 2f;
        ballVel = new Vector2(Mathf.Cos(angle) * direction, Mathf.Sin(angle)) * v;
        ballPos.x = x;
        aiAim = Random.Range(-1f, 1f) * aiError * padHalf;
        PixelAudio.Play("pong_hit");
    }

    private void Score(bool player)
    {
        if (player) playerScore++; else aiScore++;
        PixelAudio.Play("pong_score");
        if (playerScore >= pointsToWin) { EndRound(true, null); return; }
        if (aiScore >= pointsToWin) { EndRound(false, loseText); return; }
        Serve(player ? -1 : 1); // the one who lost the point... gets it served at them
    }

    private void Place()
    {
        if (playerPad != null) playerPad.localPosition = new Vector3(playerPad.localPosition.x, playerY, playerPad.localPosition.z);
        if (aiPad != null) aiPad.localPosition = new Vector3(aiPad.localPosition.x, aiY, aiPad.localPosition.z);
        if (ball != null) ball.localPosition = new Vector3(ballPos.x, ballPos.y, -0.03f);
        if (ballModel != null) ballModel.localRotation = Quaternion.Euler(Time.unscaledTime * 40f, Time.unscaledTime * 70f, 0f);
    }

    private void UpdateStatus()
    {
        if (statusLabel == null) return;
        PixelUIKit.SetText(statusLabel, string.Format(statusFormat, playerScore, aiScore, pointsToWin, Mathf.CeilToInt(Mathf.Max(0f, timeLeft))));
    }

    private static float ReadKeys()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return 0f;
        return (kb.upArrow.isPressed ? 1f : 0f) - (kb.downArrow.isPressed ? 1f : 0f);
#else
        return (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
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
        playerPad = aiPad = ball = ballModel = null;
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = null;
        statusLabel = null;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));

        if (win)
        {
            clicker.AddCurrency(ballTier, payout);
            Report(MinigameEvent.Clicked);
            PixelAudio.Play("pong_win");
            ShowMessage(at, rot, winFormat + "\n+" + PixelClicker.FormatNumber(payout));
        }
        else if (!string.IsNullOrEmpty(message))
        {
            PixelAudio.Play("pong_lose");
            ShowMessage(at, rot, message);
        }
    }

    private void ShowMessage(Vector3 at, Quaternion rot, string text)
    {
        StartCoroutine(MessageRoutine(at, rot, text));
    }

    private IEnumerator MessageRoutine(Vector3 start, Quaternion rot, string message)
    {
        GameObject go = new GameObject("Pong Message");
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
        uiRoot = PixelUIKit.CreateCanvas("Pong UI", 125, new Vector2(1920f, 1080f), true);
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
