// ---- System notes (moved here from CLAUDE.md) ----
// PixelDashMinigame.cs
//   Cube Dash minigame (id dash, pack unlocksMinigame = "dash", Minigames tab, needs the RGB pack, 50K each; NOT a TakeoverActive
//   minigame - the cube must stay clickable - but Takeover => true so the limiter keeps every other minigame from starting while it
//   runs): every minInterval..maxInterval s the cube dashes (PixelClicker.SetCubeOffset(world offset) / CubeBasePosition, applied in
//   AnimatePixel) between random spots inside the area* screen fractions: dashSeconds dash, dwellMin..Max s hanging at the spot
//   drifting in a Perlin wiggle (wiggleRadius), then the next spot (at least minHop away); after roundSeconds it zips home and pays
//   rewardMultiplier (10) x the pixel's worth per click (amountPerClick x PayoutMultiplier x ClickMultiplier / clicksToCollect) via
//   AddCurrency on top of the clicks' normal pay. Only manual clicks count (PixelCollected / PixelHit with automatic false); the auto
//   clicker is off meanwhile (PixelMinigame.AutoClickerPaused, read by PixelAutoClicker). The cube stays the pixel it was
//   (SetForcedSpawnTier, skipped when a potion already forces one; a Dragon Cube / meteor falls back to White). Stats
//   dash.rounds/clicks/best, tip minigame_dash, sounds dash_start/_win/_lose (silent until clips are assigned).
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// Cube Dash minigame (id "dash", bought in the shop's Minigames tab). Every so often the clickable cube starts dashing around the screen: it zips
/// to a spot, hangs there for a moment drifting around in a small area, then zips to another. The auto clicker is switched off meanwhile; only
/// your own clicks count. The cube stays the pixel it was when the round started. When time is up you are paid a bonus of
/// <see cref="rewardMultiplier"/> x that pixel's worth for every click you landed. (The clicks themselves still pay as usual.)
/// </summary>
public class PixelDashMinigame : PixelScheduledMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Rules")]
    [Min(5f)]
    [Tooltip("How long a round lasts (seconds).")]
    [SerializeField] private float roundSeconds = 20f;

    [Min(1f)]
    [Tooltip("Bonus paid at the end: this x the pixel's worth x the clicks you landed.")]
    [SerializeField] private float rewardMultiplier = 10f;

    [Header("Movement")]
    [Min(0.05f)]
    [Tooltip("Seconds one dash takes (the cube is fast, so keep it short).")]
    [SerializeField] private float dashSeconds = 0.3f;

    [Min(0.2f)]
    [Tooltip("Shortest time the cube stays at a spot (seconds).")]
    [SerializeField] private float dwellMin = 1.4f;

    [Min(0.2f)]
    [Tooltip("Longest time the cube stays at a spot (seconds).")]
    [SerializeField] private float dwellMax = 2.6f;

    [Range(0f, 0.15f)]
    [Tooltip("How far the cube drifts around its spot while it stays, as a fraction of the screen height.")]
    [SerializeField] private float wiggleRadius = 0.045f;

    [Min(0.1f)]
    [Tooltip("How fast the cube drifts around its spot.")]
    [SerializeField] private float wiggleSpeed = 1.6f;

    [Range(0.05f, 0.45f)]
    [Tooltip("A new spot is at least this far from the last one (as a fraction of the screen), so every dash is a real dash.")]
    [SerializeField] private float minHop = 0.3f;

    [Tooltip("Left edge of the area the cube dashes in (0 = left of the screen, 1 = right).")]
    [Range(0f, 0.5f)] [SerializeField] private float areaLeft = 0.14f;

    [Tooltip("Right edge of the dashing area.")]
    [Range(0.5f, 1f)] [SerializeField] private float areaRight = 0.86f;

    [Tooltip("Bottom edge of the dashing area (the black bars are kept clear).")]
    [Range(0f, 0.5f)] [SerializeField] private float areaBottom = 0.26f;

    [Tooltip("Top edge of the dashing area.")]
    [Range(0.5f, 1f)] [SerializeField] private float areaTop = 0.74f;

    [Header("Look")]
    [Min(10)]
    [Tooltip("Size of the status text at the top of the screen.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Status line while playing. {0} = clicks, {1} = seconds left.")]
    [SerializeField] private string statusFormat = "CUBE DASH   clicks {0}   {1}s";

    [Tooltip("Result line. {0} = clicks, {1} = the bonus (a number), {2} = the pixel's name.")]
    [SerializeField] private string resultFormat = "Dash over!  {0} clicks  +{1} {2}";

    [Min(0.5f)]
    [Tooltip("How long the result stays on screen (seconds).")]
    [SerializeField] private float resultSeconds = 3f;

    private enum Phase { Dash, Dwell, Return }

    private float timeLeft, phaseT, dwellLength, noiseSeed, resultTimer;
    private bool activeRound, forcedByMe;
    private int clicks, tierIndex;
    private Phase phase;
    private Vector2 from, to, anchor, current;
    private Camera cam;
    private GameObject uiRoot;
    private TMP_Text statusLabel;

    // This minigame's own defaults for the shared round schedule (PixelScheduledMinigame; a scene's values override them).
    public PixelDashMinigame()
    {
        firstRoundDelay = 70f;
        minInterval = 150f;
        maxInterval = 300f;
    }

    public override string Id => "dash";
    public override string DisplayName => "Cube Dash";
    public override bool Busy => activeRound;
    public override bool Takeover => true;   // no other minigame starts while it runs (the cube itself stays clickable: TakeoverActive is not set)

    public override void Deactivate()
    {
        base.Deactivate();
        if (activeRound) EndRound(false);
    }

    protected override void Awake()
    {
        base.Awake();
        if (!HookCubeClicks(ref clicker, OnCollected, OnHit)) return;
        StartSchedule();
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
        ScheduleNext();
    }

    [ContextMenu("Start A Dash Now")]
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
        noiseSeed = Random.value * 100f;
        activeRound = true;
        PixelMinigame.AutoClickerPaused = true;

        current = cam.WorldToViewportPoint(clicker.CubeBasePosition);
        BeginDash(PickSpot(current));
        BuildUi();
        Report(MinigameEvent.Spawned);
        PixelStats.Count("dash.rounds");
        PixelHints.Trigger("minigame_dash");
        PixelAudio.Play("dash_start");
        return true;
    }

    private Vector2 PickSpot(Vector2 away)
    {
        Vector2 spot = away;
        for (int i = 0; i < 12; i++)
        {
            spot = new Vector2(Random.Range(areaLeft, areaRight), Random.Range(areaBottom, areaTop));
            if ((spot - away).magnitude >= minHop) break;
        }
        return spot;
    }

    private void BeginDash(Vector2 target)
    {
        phase = Phase.Dash;
        phaseT = 0f;
        from = current;
        to = target;
    }

    private void Tick(float dt)
    {
        if (clicker == null || cam == null) { EndRound(false); return; }
        if (Time.timeScale <= 0f) return;

        if (phase != Phase.Return)
        {
            timeLeft -= dt;
            if (timeLeft <= 0f)
            {
                // Time is up: the cube zips home.
                phase = Phase.Return;
                phaseT = 0f;
                from = current;
                to = cam.WorldToViewportPoint(clicker.CubeBasePosition);
            }
        }

        switch (phase)
        {
            case Phase.Dash:
            case Phase.Return:
            {
                phaseT += dt / Mathf.Max(0.05f, dashSeconds);
                float k = Mathf.Clamp01(phaseT);
                k = k * k * (3f - 2f * k);                 // quick start and stop
                current = Vector2.Lerp(from, to, k);
                if (phaseT >= 1f)
                {
                    if (phase == Phase.Return) { EndRound(true); return; }
                    phase = Phase.Dwell;
                    phaseT = 0f;
                    anchor = to;
                    dwellLength = Random.Range(Mathf.Min(dwellMin, dwellMax), Mathf.Max(dwellMin, dwellMax));
                }
                break;
            }
            case Phase.Dwell:
            {
                phaseT += dt;
                float t = Time.time * wiggleSpeed;
                Vector2 wiggle = new Vector2((Mathf.PerlinNoise(noiseSeed + t, 0.37f) - 0.5f) * 2f, (Mathf.PerlinNoise(0.91f, noiseSeed + t) - 0.5f) * 2f) * wiggleRadius;
                wiggle.x /= Mathf.Max(0.5f, cam.aspect);   // the same distance on screen sideways and up
                current = anchor + wiggle;
                if (phaseT >= dwellLength) BeginDash(PickSpot(current));
                break;
            }
        }

        // Put the cube at that spot on the screen (same distance from the camera, so it keeps its size).
        PixelCubeScreenSpot.Place(clicker, cam, current);

        UpdateStatus();
    }

    private void EndRound(bool paid)
    {
        if (!activeRound) return;
        activeRound = false;
        ReleaseCubeRound(clicker, ref forcedByMe);
        clicker.SetCubeOffset(null);
        ScheduleNext();

        if (!paid) { DropRoundUi(ref uiRoot, ref statusLabel); return; }

        double bonus = PayCubeRoundBonus(clicker, tierIndex, clicks, rewardMultiplier);
        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];

        PixelStats.Count("dash.clicks", clicks);
        PixelStats.Best("dash.best", clicks);
        Report(MinigameEvent.Clicked);
        PixelAudio.Play(clicks > 0 ? "dash_win" : "dash_lose");
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
        uiRoot = PixelUIKit.CreateCanvas("Dash UI", 125, new Vector2(1920f, 1080f), true);
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
