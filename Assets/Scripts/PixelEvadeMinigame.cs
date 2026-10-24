// ---- System notes (moved here from CLAUDE.md) ----
// PixelEvadeMinigame.cs
//   Cube Evasion minigame (id evade, pack unlocksMinigame = "evade", Minigames tab, needs the RGB pack, 70K each; like Cube Dash /
//   Blink: Takeover => true, cube stays clickable, AutoClickerPaused meanwhile): every minInterval..maxInterval s a round of
//   roundSeconds (25) starts and the cube RUNS FROM THE MOUSE (Steer, in viewport space measured in screen heights): inside fleeRadius
//   (0.22) it accelerates (agility) directly away from the pointer at up to fleeSpeed (0.5 screen heights per second, faster the
//   closer the mouse is), slides along the wall when it runs into the area's edge (edgeMargin, area* fractions) and, cornered on both
//   axes, breaks back across the middle; away from the mouse it just coasts to a stop. Clicks (manual only) count; at the end it
//   returns home and pays rewardMultiplier (10) x the pixel's worth per click via AddCurrency; stats evade.rounds/clicks/best, tip
//   minigame_evade, sounds evade_start/_win/_lose (silent until clips are assigned), picture case in BuildMinigameModel; a success
//   reports Clicked (bonus skill XP 15).
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// Cube Evasion minigame (id "evade", bought in the shop's Minigames tab). For a round the clickable cube runs away from your mouse: while the pointer
/// is close it always moves away from it, sliding along the edges of the play area and breaking back across the middle when it is cornered. It is
/// kept slow enough to follow, so you can corner it and click. The auto clicker is off meanwhile: only your own clicks count. When the round ends you
/// are paid a bonus of <see cref="rewardMultiplier"/> x the pixel's worth for every click you landed (the clicks still pay as usual).
/// </summary>
public class PixelEvadeMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Cube Evasion is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame is switched on before the first round can start.")]
    [SerializeField] private float firstRoundDelay = 90f;

    [Min(1f)]
    [Tooltip("Shortest wait between rounds (seconds).")]
    [SerializeField] private float minInterval = 150f;

    [Min(1f)]
    [Tooltip("Longest wait between rounds (seconds).")]
    [SerializeField] private float maxInterval = 300f;

    [Header("Rules")]
    [Min(5f)]
    [Tooltip("How long a round lasts (seconds).")]
    [SerializeField] private float roundSeconds = 25f;

    [Min(1f)]
    [Tooltip("Bonus paid at the end: this x the pixel's worth x the clicks you landed.")]
    [SerializeField] private float rewardMultiplier = 10f;

    [Header("Evading")]
    [Min(0.05f)]
    [Tooltip("Top speed of the cube, in screen heights per second. Keep it low enough that a player can follow and corner it.")]
    [SerializeField] private float fleeSpeed = 0.5f;

    [Range(0.05f, 0.6f)]
    [Tooltip("The cube starts running when the mouse is closer than this (as a fraction of the screen height). Closer = faster.")]
    [SerializeField] private float fleeRadius = 0.22f;

    [Min(0.5f)]
    [Tooltip("How quickly the cube speeds up and turns (higher = twitchier).")]
    [SerializeField] private float agility = 6f;

    [Range(0.02f, 0.3f)]
    [Tooltip("How close to the edge of the area the cube starts sliding along it instead of running into it (fraction of the screen height).")]
    [SerializeField] private float edgeMargin = 0.08f;

    [Tooltip("Left edge of the area the cube stays in (0 = left of the screen, 1 = right).")]
    [Range(0f, 0.5f)] [SerializeField] private float areaLeft = 0.1f;

    [Tooltip("Right edge of the area.")]
    [Range(0.5f, 1f)] [SerializeField] private float areaRight = 0.9f;

    [Tooltip("Bottom edge of the area (the black bars are kept clear).")]
    [Range(0f, 0.5f)] [SerializeField] private float areaBottom = 0.24f;

    [Tooltip("Top edge of the area.")]
    [Range(0.5f, 1f)] [SerializeField] private float areaTop = 0.76f;

    [Header("Look")]
    [Min(10)]
    [Tooltip("Size of the status text at the top of the screen.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Status line while playing. {0} = clicks, {1} = seconds left.")]
    [SerializeField] private string statusFormat = "CUBE EVASION   clicks {0}   {1}s";

    [Tooltip("Result line. {0} = clicks, {1} = the bonus (a number), {2} = the pixel's name.")]
    [SerializeField] private string resultFormat = "Evasion over!  {0} clicks  +{1} {2}";

    [Min(0.5f)]
    [Tooltip("How long the result stays on screen (seconds).")]
    [SerializeField] private float resultSeconds = 3f;

    private float spawnTimer, timeLeft, resultTimer, returnT;
    private bool activeRound, forcedByMe, returning;
    private int clicks, tierIndex;
    private Vector2 pos, vel, returnFrom, home;   // viewport coordinates
    private Camera cam;
    private GameObject uiRoot;
    private TMP_Text statusLabel;

    public override string Id => "evade";
    public override string DisplayName => "Cube Evasion";
    public override bool Running => running;
    public override bool Busy => activeRound;
    public override bool Takeover => true;   // no other minigame starts while it runs (the cube itself stays clickable: TakeoverActive is not set)

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstRoundDelay;
    }

    public override void Deactivate()
    {
        running = false;
        if (activeRound) EndRound(false);
    }

    protected override void Awake()
    {
        base.Awake();
        if (!HookCubeClicks(ref clicker, OnCollected, OnHit)) return;
        if (startRunning) running = true;
        spawnTimer = firstRoundDelay;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        UnhookCubeClicks(clicker, OnCollected, OnHit);
        if (activeRound) EndRound(false);
    }

    protected override void OnDespawned()
    {
        if (activeRound) EndRound(false);
        DropRoundUi(ref uiRoot, ref statusLabel);
        spawnTimer = RandomBetween(minInterval, maxInterval);
    }

    [ContextMenu("Start An Evasion Round Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !activeRound) TryStart();
    }

    private void OnCollected(int tier, double amount, bool automatic)
    {
        if (activeRound && !automatic) clicks++;
    }

    private void OnHit(int tier, int hits, int needed, bool automatic)
    {
        if (activeRound && !automatic) clicks++;
    }

    private void Update()
    {
        if (ResultShownLong(ref resultTimer, activeRound, uiRoot)) DropRoundUi(ref uiRoot, ref statusLabel);
        if (activeRound) { Tick(Time.deltaTime); return; }
        if (!running || UserDisabled) return;

        if (SpawnDue(ref spawnTimer, PixelPauseMenu.IsPaused || clicker.CubeHidden || TakeoverActive) && !TryStart()) spawnTimer = 12f;
    }

    // ------------------------------------------------------------------
    // A round
    // ------------------------------------------------------------------

    private bool TryStart()
    {
        if (!PrepareCubeRound(clicker, ref cam, ref tierIndex, ref forcedByMe)) return false;

        clicks = 0;
        timeLeft = roundSeconds;
        activeRound = true;
        returning = false;
        PixelMinigame.AutoClickerPaused = true;
        home = cam.WorldToViewportPoint(clicker.CubeBasePosition);
        pos = home;
        vel = Vector2.zero;
        BuildUi();
        Report(MinigameEvent.Spawned);
        PixelStats.Count("evade.rounds");
        PixelHints.Trigger("minigame_evade");
        PixelAudio.Play("evade_start");
        return true;
    }

    private void Tick(float dt)
    {
        if (clicker == null || cam == null) { EndRound(false); return; }
        if (Time.timeScale <= 0f) return;

        if (!returning)
        {
            timeLeft -= dt;
            if (timeLeft <= 0f)
            {
                returning = true;
                returnT = 0f;
                returnFrom = pos;
            }
        }

        if (returning)
        {
            returnT += dt / 0.5f;
            float k = Mathf.Clamp01(returnT);
            k = k * k * (3f - 2f * k);
            pos = Vector2.Lerp(returnFrom, home, k);
            Place();
            if (returnT >= 1f) { EndRound(true); return; }
            return;
        }

        Steer(dt);
        Place();
        UpdateStatus();
    }

    /// <summary>Runs from the mouse: away from it, sliding along the edges, breaking back across the middle when cornered.</summary>
    private void Steer(float dt)
    {
        float aspect = Mathf.Max(0.5f, cam.aspect);
        Vector2 mouse = cam.ScreenToViewportPoint(PixelInput.PointerPosition());
        // Work in 'screen height' units so a distance means the same sideways and up.
        Vector2 away = new Vector2((pos.x - mouse.x) * aspect, pos.y - mouse.y);
        float dist = away.magnitude;
        Vector2 desired = Vector2.zero;

        if (dist < fleeRadius)
        {
            Vector2 dir = dist > 0.0005f ? away / dist : Random.insideUnitCircle.normalized;
            float urgency = 1f - Mathf.Clamp01(dist / fleeRadius);   // closer = faster
            float speed = fleeSpeed * Mathf.Lerp(0.35f, 1f, urgency);

            float marginX = edgeMargin / aspect, marginY = edgeMargin;
            bool blockL = pos.x < areaLeft + marginX && dir.x < 0f, blockR = pos.x > areaRight - marginX && dir.x > 0f;
            bool blockB = pos.y < areaBottom + marginY && dir.y < 0f, blockT = pos.y > areaTop - marginY && dir.y > 0f;
            bool blockX = blockL || blockR, blockY = blockB || blockT;

            if (blockX && blockY)
            {
                // Cornered: break back across the middle (past the mouse), the only way out.
                Vector2 centre = new Vector2((areaLeft + areaRight) * 0.5f, (areaBottom + areaTop) * 0.5f);
                Vector2 toCentre = new Vector2((centre.x - pos.x) * aspect, centre.y - pos.y);
                dir = toCentre.sqrMagnitude > 1e-6f ? toCentre.normalized : Vector2.up;
            }
            else if (blockX)
            {
                // Against a side wall: slide along it, whichever way is away from the mouse (or towards the roomier end).
                float sign = Mathf.Abs(away.y) > 0.01f ? Mathf.Sign(away.y) : (pos.y < (areaBottom + areaTop) * 0.5f ? 1f : -1f);
                dir = new Vector2(0f, sign);
            }
            else if (blockY)
            {
                float sign = Mathf.Abs(away.x) > 0.01f ? Mathf.Sign(away.x) : (pos.x < (areaLeft + areaRight) * 0.5f ? 1f : -1f);
                dir = new Vector2(sign, 0f);
            }
            desired = dir * speed;
        }

        vel = Vector2.Lerp(vel, desired, 1f - Mathf.Exp(-agility * dt));
        pos += new Vector2(vel.x / aspect, vel.y) * dt;
        pos.x = Mathf.Clamp(pos.x, areaLeft, areaRight);
        pos.y = Mathf.Clamp(pos.y, areaBottom, areaTop);
    }

    /// <summary>Puts the cube at the viewport spot (the same distance from the camera, so it keeps its size).</summary>
    private void Place() => PixelCubeScreenSpot.Place(clicker, cam, pos);

    private void EndRound(bool paid)
    {
        if (!activeRound) return;
        activeRound = false;
        returning = false;
        ReleaseCubeRound(clicker, ref forcedByMe);
        clicker.SetCubeOffset(null);
        spawnTimer = RandomBetween(minInterval, maxInterval);

        if (!paid) { DropRoundUi(ref uiRoot, ref statusLabel); return; }

        double bonus = PayCubeRoundBonus(clicker, tierIndex, clicks, rewardMultiplier);
        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];

        PixelStats.Count("evade.clicks", clicks);
        PixelStats.Best("evade.best", clicks);
        if (clicks > 0) Report(MinigameEvent.Clicked);   // a success (bonus skill XP)
        PixelAudio.Play(clicks > 0 ? "evade_win" : "evade_lose");
        string line = string.Format(resultFormat, clicks, PixelClicker.FormatNumber(bonus), tier.displayName);
        if (statusLabel != null) statusLabel.text = line;
        PixelHints.Announce(line);
        resultTimer = resultSeconds;
    }

    // ------------------------------------------------------------------
    // The status line
    // ------------------------------------------------------------------

    private void BuildUi()
    {
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = PixelUIKit.CreateCanvas("Evade UI", 125, new Vector2(1920f, 1080f), true);
        uiRoot.transform.SetParent(transform, false);
        Track(uiRoot);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        statusLabel = PixelStatusBox.Create(clicker.UIFont, uiRoot.transform, statusFontSize);   // slides out from under the top bar
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (statusLabel == null || !activeRound) return;
        PixelUIKit.SetText(statusLabel, string.Format(statusFormat, clicks, Mathf.CeilToInt(Mathf.Max(0f, timeLeft))));
    }
}
