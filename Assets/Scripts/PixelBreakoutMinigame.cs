// ---- System notes (moved here from CLAUDE.md) ----
// PixelBreakoutMinigame.cs
//   Breakout minigame (id breakout, pack unlocksMinigame = "breakout", Minigames tab, needs the RGB pack; takeover minigame): every
//   minInterval..maxInterval s with at least minPixels (30) old pixels around, up to maxBricks of them (random) become the bricks:
//   frozen (kinematic, colliders off, OldPixelDespawn.HoldAll), scaled to fit and glided (slideSeconds) into a wall in a court plane
//   facing the camera (flat Sprites/Default blocks under a root, depthFraction of the way to the cube so it is in front of the old
//   pixels; court = the whole screen between the black bars (manualCourtSize ticked: courtWidth/Height/CentreY), wallHeightFraction, brickColumns); all other old pixels shrink away and come back after the
//   game (bystanderSeconds, same as Sorting Race) and the cube shrinks away (hideCube); the court's background is transparent by
//   default (backOpacity 0..1, colour backColor). Mouse (projected onto the court plane) or Left/Right arrows move the paddle; the
//   ball is a spinning CreateDisplayPixel of a random brick's type, speeds up per brick (speedUpPerBrick, maxBallSpeed); lives balls;
//   every broken brick pays amount * brickMultiplier (10) immediately via AddCurrency and shrinks away (OldPixelDespawn.Begin); bricks
//   left when the game ends (out of balls / timeLimit) go back to where they were and drop. Sounds
//   breakout_start/_hit/_brick/_miss/_win/_lose, tip minigame_breakout.
// ---------------------------------------------------
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
public class PixelBreakoutMinigame : PixelScheduledMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, husks, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("When it starts")]
    [Min(1)]
    [Tooltip("A game only starts when at least this many husks are lying around: they become the bricks.")]
    [SerializeField] private int minPixels = 30;

    [Min(5)]
    [Tooltip("At most this many husks become bricks. The rest are removed until the game is over.")]
    [SerializeField] private int maxBricks = 45;

    [Header("Rules")]
    [Min(1)]
    [Tooltip("Balls you get. Missing the ball costs one.")]
    [SerializeField] private int lives = 3;

    [Min(10f)]
    [Tooltip("Seconds before the game is called off.")]
    [SerializeField] private float timeLimit = 150f;

    [Min(1f)]
    [Tooltip("Every brick you break pays that pixel's worth times this.")]
    [SerializeField] private float brickMultiplier = 10f;

    [Tooltip("Hide the clickable cube while playing (it grows back afterwards).")]
    [SerializeField] private bool hideCube = true;

    [Header("Court")]
    [Tooltip("Tick to use the three numbers below by hand. Off (default): the court fills the screen between the black bars.")]
    [SerializeField] private bool manualCourtSize = false;

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
    [Tooltip("How far in front of the camera the court floats, as a fraction of the distance to the cube (it must be in front of the husks).")]
    [SerializeField] private float depthFraction = 0.6f;

    [Range(0.4f, 1f)]
    [Tooltip("When the court is sized automatically, how much of the available screen it fills (1 = all of it; lower = a smaller, less zoomed-in court). 0 = coded default 0.75.")]
    [SerializeField] private float autoFill = 0f;

    [Min(3)]
    [Tooltip("Bricks per row (more columns are used if the wall would get too tall).")]
    [SerializeField] private int brickColumns = 9;

    [Range(0.3f, 0.8f)]
    [Tooltip("The wall never takes more than this fraction of the court height.")]
    [SerializeField] private float wallHeightFraction = 0.55f;

    [Min(0.1f)]
    [Tooltip("Seconds the pixels take to glide into the wall.")]
    [SerializeField] private float slideSeconds = 0.8f;

    [Min(0.05f)]
    [Tooltip("Seconds the pixels that are not bricks take to shrink away / grow back.")]
    [SerializeField] private float bystanderSeconds = 0.6f;

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

    [Header("Ultra Pixels")]
    [Tooltip("Chance that each brick you break gives one Ultra pixel of its type. 0 = 0.03.")]
    [SerializeField] private float ultraChance = 0.03f;

    [Tooltip("Most Ultra pixels one round can give. 0 = 3.")]
    [SerializeField] private int ultraMaxPerRound = 3;

    [Tooltip("Turn Ultra pixel rewards from this minigame off.")]
    [SerializeField] private bool disableUltra = false;

    [Header("Look")]
    [Tooltip("Colour of the paddle.")]
    [SerializeField] private Color paddleColor = new Color(0.4f, 0.9f, 1f, 1f);
    [Tooltip("Colour of the court's top and side walls (alpha = how see-through).")]
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("Colour of the court's background. It is always drawn fully solid (its alpha is ignored), so nothing shows through it.")]
    [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 1f);
    [Tooltip("How solid the court's background is (0 = fully transparent, the default; 1 = solid backdrop in the colour above).")]
    [SerializeField, Range(0f, 1f)] private float backOpacity = 0f;

    [Min(10)]
    [Tooltip("Size of the score text.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Status line. {0} = bricks left, {1} = balls left, {2} = seconds left.")]
    [SerializeField] private string statusFormat = "BREAKOUT   bricks {0}   balls {1}   {2}s";

    [Tooltip("Floating message when every brick is broken.")]
    [SerializeField] private string winFormat = "Cleared!";
    [Tooltip("Floating message when the last ball is lost.")]
    [SerializeField] private string loseText = "Out of balls";
    [Tooltip("Floating message when the round's time runs out.")]
    [SerializeField] private string timeoutText = "Time's up";

    [Min(10f)]
    [Tooltip("Size of the floating win / lose message (world text size).")]
    [SerializeField] private float messageTextSize = 5f;

    [Min(0.5f)]
    [Tooltip("How long the floating win / lose message rises and fades (seconds).")]
    [SerializeField] private float messageSeconds = 2.2f;

    private class Brick
    {
        public Rigidbody body; public Transform tf; public int tier; public double amount; public OldPixelDespawn despawn;
        public Collider[] colliders; public Behaviour[] paused;
        public Vector3 startPos, endPos, startScale, endScale; public Quaternion startRot, endRot;
        public Vector2 pos; public bool alive;
    }

    private readonly PixelBystanders bystanders = new PixelBystanders();   // the husks not in the round shrink away while it runs
    private float slideT;
    private double earned;
    private float serveDelayExtra;

    private float timeLeft, serveTimer;
    private bool activeRound, setBlock;
    private Camera cam;
    private GameObject root, uiRoot;
    private Transform paddle, ball, ballModel;
    private TMP_Text statusLabel;
    private float halfW, halfH, padHalfW, padH, ballR, speed, padY;
    private float padX;
    private Vector2 ballPos, ballVel;
    private int ballsLeft, bricksLeft, ballTier;
    private Material flat;
    private readonly System.Collections.Generic.List<Brick> bricks = new System.Collections.Generic.List<Brick>();
    private Vector2 brickSize;

    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBreakoutStatics() { Active = false; }

    // This minigame's own defaults for the shared round schedule (PixelScheduledMinigame; a scene's values override them).
    public PixelBreakoutMinigame()
    {
        firstRoundDelay = 75f;
        minInterval = 150f;
        maxInterval = 300f;
    }

    public override string Id => "breakout";
    public override string DisplayName => "Breakout";
    public override bool Busy => activeRound;
    public override bool Takeover => true;

    public override void Deactivate()
    {
        base.Deactivate();
        if (activeRound) EndRound(false, null);
    }

    protected override void Awake()
    {
        base.Awake();
        PixelHuskPool.HuskReleasing += OnHuskReleasing;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelBreakoutMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        StartSchedule();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        PixelHuskPool.HuskReleasing -= OnHuskReleasing;
        if (activeRound) EndRound(false, null);
        bystanders.Restore();   // put the other husks back at once
    }

    // (PixelHuskPool: the husk is going away and may be reused - let go of it, as if it had been destroyed; the round-end restore skips it)
    private void OnHuskReleasing(Rigidbody body)
    {
        foreach (Brick br in bricks)
        {
            if (br.body != body) continue;
            if (br.alive) { br.alive = false; bricksLeft = Mathf.Max(0, bricksLeft - 1); }
            br.body = null; br.tf = null; br.despawn = null;
        }
        bystanders.Forget(body);
    }

    protected override void OnDespawned()
    {
        if (activeRound) EndRound(false, null);
        ScheduleNext();
    }

    [ContextMenu("Start A Breakout Game Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !activeRound) TryStart(true);
    }

    private void Update()
    {
        bystanders.Tick(activeRound, bystanderSeconds);
        if (activeRound) { Tick(Time.deltaTime); return; }
        if (!running) return;

        if (SpawnDue(ref spawnTimer)) spawnTimer = TryStart(false) ? 0f : 12f;
    }

    // ------------------------------------------------------------------
    // Starting
    // ------------------------------------------------------------------

    private bool TryStart(bool dev)
    {
        cam = clicker.ViewCamera;
        if (cam == null) return false;

        // The old pixels lying around become the bricks.
        var all = clicker.OldPixels;
        var candidates = new System.Collections.Generic.List<Brick>();
        for (int i = 0; i < all.Count; i++)
        {
            Rigidbody rb = all[i];
            if (rb == null || rb.isKinematic || clicker.IsFlyingPixel(rb)) continue;
            OldPixelInfo info = rb.GetComponent<OldPixelInfo>();
            if (info == null) continue;
            OldPixelDespawn d = rb.GetComponent<OldPixelDespawn>();
            if (d != null && (d.IsDespawning || d.Held)) continue;
            candidates.Add(new Brick
            {
                body = rb, tf = rb.transform, tier = info.tierIndex, amount = info.amount, despawn = d,
                colliders = rb.GetComponents<Collider>(),
                paused = new Behaviour[] { rb.GetComponent<OldPixelGravityWell>(), rb.GetComponent<OldPixelFloat>() },
            });
        }
        if (candidates.Count < (dev ? 6 : minPixels)) return false;
        for (int i = candidates.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t; }
        int use = Mathf.Min(maxBricks, candidates.Count);
        bricks.Clear();
        for (int i = 0; i < use; i++) bricks.Add(candidates[i]);
        var inWall = new System.Collections.Generic.HashSet<Rigidbody>();
        foreach (Brick br in bricks) inWall.Add(br.body);
        ballTier = bricks[Random.Range(0, bricks.Count)].tier;

        // The court floats in a plane facing the camera, in front of the old pixels.
        Transform target = clicker.PixelTransform;
        float dist = target != null ? Vector3.Distance(cam.transform.position, target.position) * depthFraction : 6f;
        dist = Mathf.Max(cam.nearClipPlane + 0.5f, dist);
        // The court is just big enough to match the screen: the full width and the height between the black bars (unless sized by hand).
        float cw = courtWidth, ch = courtHeight, cy = courtCentreY;
        if (!manualCourtSize)
        {
            float barFrac = PixelHud.Instance != null ? Mathf.Clamp(PixelHud.Instance.BarHeight / Mathf.Max(1f, Screen.height), 0f, 0.3f) : 0f;
            float fill = autoFill > 0.01f ? autoFill : 0.75f;
            cw = 0.98f * fill;
            ch = Mathf.Max(0.3f, (1f - 2f * barFrac - 0.02f) * fill);
            cy = 0.5f;
        }
        Vector3 centre = cam.ViewportToWorldPoint(new Vector3(0.5f, cy, dist));
        Vector3 left = cam.ViewportToWorldPoint(new Vector3(0.5f - cw * 0.5f, cy, dist));
        Vector3 top = cam.ViewportToWorldPoint(new Vector3(0.5f, cy + ch * 0.5f, dist));
        halfW = Vector3.Dot(centre - left, cam.transform.right);
        halfH = Vector3.Dot(top - centre, cam.transform.up);
        if (halfW <= 0.01f || halfH <= 0.01f) return false;

        root = new GameObject("Breakout Court");
        Track(root);
        root.transform.SetPositionAndRotation(centre, cam.transform.rotation);
        BuildCourt();

        OldPixelDespawn.HoldAll = true;
        foreach (Brick br in bricks)
        {
            PixelBody.Steer(br.body, Vector3.zero, true);
            br.body.isKinematic = true;
            foreach (Collider c in br.colliders) if (c != null) c.enabled = false;
            foreach (Behaviour x in br.paused) if (x != null) x.enabled = false;
        }
        bystanders.Shrink(clicker, inWall);
        slideT = 0f;
        earned = 0d;

        ballsLeft = lives;
        speed = 0f;
        padX = 0f;
        timeLeft = timeLimit;
        Serve();
        BuildUi();

        if (hideCube) clicker.SetCubeShrink(0.02f, 0.5f);
        activeRound = true;
        Active = true;
        TakeoverActive = true;
        if (!PixelClicker.ExternalClickBlock) { PixelClicker.ExternalClickBlock = true; setBlock = true; }
        ultraRoundCount = 0;
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
        courtMaterials.Add(m);
        return m;
    }

    private readonly System.Collections.Generic.List<Material> courtMaterials = new System.Collections.Generic.List<Material>();   // (the court's copies of 'flat', freed with the court)

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
        padHalfW = halfW * paddleWidth;
        padH = halfH * 0.05f;
        padY = -halfH + padH * 3f;
        ballR = halfH * ballSize * 0.5f;

        Block("Back", Vector2.zero, new Vector2(halfW * 2f, halfH * 2f), new Color(backColor.r, backColor.g, backColor.b, backOpacity), halfH * 0.2f);   // well behind the bricks: see-through pixels are drawn back to front, so a nearer backdrop would cover them
        Block("Top", new Vector2(0f, halfH), new Vector2(halfW * 2f, line), lineColor, 0f);
        Block("Left", new Vector2(-halfW, 0f), new Vector2(line, halfH * 2f), lineColor, 0f);
        Block("Right", new Vector2(halfW, 0f), new Vector2(line, halfH * 2f), lineColor, 0f);
        paddle = Block("Paddle", new Vector2(0f, padY), new Vector2(padHalfW * 2f, padH), paddleColor, -0.02f);

        // The wall: the old pixels themselves, glided into rows across the top and scaled to fit.
        int n = bricks.Count;
        int cols = Mathf.Max(3, brickColumns);
        float areaW = halfW * 2f * 0.94f;
        float maxWallH = halfH * 2f * wallHeightFraction;
        int rows = Mathf.CeilToInt(n / (float)cols);
        while (rows * (areaW / cols) > maxWallH && cols < n) { cols++; rows = Mathf.CeilToInt(n / (float)cols); }
        float pitch = Mathf.Min(areaW / cols, maxWallH / rows);
        float edge = pitch * 0.88f;
        brickSize = new Vector2(edge, edge);
        float top0 = halfH * 0.92f;
        for (int i = 0; i < n; i++)
        {
            int r = i / cols, c = i % cols;
            int inRow = Mathf.Min(cols, n - r * cols);
            Vector2 p = new Vector2((c - (inRow - 1) * 0.5f) * pitch, top0 - (r + 0.5f) * pitch);
            Brick br = bricks[i];
            br.pos = p;
            br.alive = true;
            br.startPos = br.tf.position; br.startRot = br.tf.rotation; br.startScale = br.tf.localScale;
            br.endPos = root.transform.TransformPoint(new Vector3(p.x, p.y, edge * 0.5f));
            br.endRot = root.transform.rotation * Quaternion.Euler(0f, 0f, 0f);
            br.endScale = br.startScale;
            MeshFilter mf = br.tf.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                float current = mf.sharedMesh.bounds.size.x * mf.transform.lossyScale.x;
                if (current > 0.0001f) br.endScale = br.startScale * (edge / current);
            }
        }
        bricksLeft = n;

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
        if (speed <= 0f) serveDelayExtra = slideSeconds;
        speed = Mathf.Max(speed, ballSpeed);
        ballPos = new Vector2(padX, padY + padH * 0.5f + ballR);
        float angle = Random.Range(-25f, 25f) * Mathf.Deg2Rad;
        ballVel = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (speed * halfH * 2f);
        serveTimer = serveDelay + serveDelayExtra;
        serveDelayExtra = 0f;
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

        if (slideT < 1f)
        {
            slideT = Mathf.Min(1f, slideT + dt / Mathf.Max(0.05f, slideSeconds));
            float e = Mathf.SmoothStep(0f, 1f, slideT);
            foreach (Brick br in bricks)
            {
                if (!br.alive || br.tf == null) continue;
                br.tf.position = Vector3.Lerp(br.startPos, br.endPos, e);
                br.tf.rotation = Quaternion.Slerp(br.startRot, br.endRot, e);
                br.tf.localScale = Vector3.Lerp(br.startScale, br.endScale, e);
            }
        }

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
            if (slideT < 1f) break;
            if (ox <= 0f || oy <= 0f) continue;
            if (ox < oy) ballVel.x = Mathf.Abs(ballVel.x) * Mathf.Sign(dx);
            else ballVel.y = Mathf.Abs(ballVel.y) * Mathf.Sign(dy);
            b.alive = false;
            double pay = b.amount * brickMultiplier;
            earned += pay;
            clicker.AddCurrency(b.tier, pay);
            UltraRoll(clicker, ultraChance, 0.03f, ultraMaxPerRound, 3, disableUltra, 1f, b.tf.position);
            if (b.despawn != null) b.despawn.Begin(); // the broken pixel shrinks away
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
        PixelStats.Count(win ? "breakout.win" : "breakout.loss");
        activeRound = false;
        Active = false;
        TakeoverActive = false;
        OldPixelDespawn.HoldAll = false;
        if (hideCube) clicker.SetCubeShrink(1f, 0.6f);
        if (setBlock) { PixelClicker.ExternalClickBlock = PixelBank.HoseOn; setBlock = false; }

        Vector3 at = root != null ? root.transform.position : Vector3.zero;
        Quaternion rot = root != null ? root.transform.rotation : Quaternion.identity;
        double payout = earned;
        // Bricks that survived go back where they were and drop; broken ones are already shrinking away.
        foreach (Brick br in bricks)
        {
            if (br.body == null || !br.alive) continue;
            br.tf.position = br.startPos; br.tf.rotation = br.startRot; br.tf.localScale = br.startScale;
            foreach (Collider c in br.colliders) if (c != null) c.enabled = true;
            foreach (Behaviour x in br.paused) if (x != null) x.enabled = true;
            br.body.isKinematic = false;
            PixelBody.Steer(br.body, Vector3.zero, true);
            if (br.despawn != null) br.despawn.AddLifetime(3f);
        }
        // Broken bricks: let go of physics only once they are gone (their despawn handles it).
        if (root != null) Destroy(root);
        root = null;
        foreach (Material m in courtMaterials) if (m != null) Destroy(m);
        courtMaterials.Clear();
        paddle = ball = ballModel = null;
        bricks.Clear();
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = null;
        statusLabel = null;
        ScheduleNext();

        if (win)
        {
            Report(MinigameEvent.Clicked);
            PixelAudio.Play("breakout_win");
            ShowMessage(at, rot, winFormat + "\n+" + PixelClicker.FormatNumber(payout) + " earned");
        }
        else if (!string.IsNullOrEmpty(message))
        {
            PixelAudio.Play("breakout_lose");
            ShowMessage(at, rot, message + (payout > 0d ? "\n+" + PixelClicker.FormatNumber(payout) + " earned" : ""));
        }
    }

    private void ShowMessage(Vector3 at, Quaternion rot, string text)
    {
        StartCoroutine(MessageRoutine(at, rot, text));
    }

    private IEnumerator MessageRoutine(Vector3 start, Quaternion rot, string message)
    {
        TextMeshPro text = PixelRisingText.Create("Breakout Message", message, messageTextSize, clicker.UIFont, new Vector2(14f, 4f));
        Track(text.gameObject);
        return PixelRisingText.Fade(text, messageSeconds, (tx, k) =>
        {
            tx.transform.position = PixelUIKit.KeepOnScreen(cam != null ? cam : PixelClicker.ViewCameraOf(clicker), tx, start + rot * Vector3.up * (k * 0.8f));
            tx.transform.rotation = rot;
        });
    }

    private void BuildUi()
    {
        uiRoot = PixelUIKit.CreateCanvas("Breakout UI", 125, new Vector2(1920f, 1080f), true);
        uiRoot.transform.SetParent(transform, false);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        statusLabel = PixelStatusBox.Create(clicker.UIFont, uiRoot.transform, statusFontSize);   // slides out from under the top bar
        UpdateStatus();
    }
}
