// ---- System notes (moved here from CLAUDE.md) ----
// PixelBlinkMinigame.cs
//   Cube Blink minigame (id blink, pack unlocksMinigame = "blink", Minigames tab, needs the RGB pack, 60K each; like Cube Dash:
//   Takeover => true but the cube stays clickable, AutoClickerPaused meanwhile): every minInterval..maxInterval s a round of
//   roundSeconds (30) starts: the cube SHRINKS to nothing (SetCubeShrink(0.02)), TELEPORTS while hidden (PickSpot inside the area*
//   fractions, at least minHop away, SetCubeOffset), stays hidden hiddenMin..Max (1-2 s), grows back and stays visible visibleMin..Max
//   (3-8 s), then vanishes and teleports again (Phase Vanish / Hidden / Appear / Visible / Return). Only manual clicks made while the
//   cube shows count; at the end it pays rewardMultiplier (10) x the pixel's worth per click via AddCurrency (the clicks still pay
//   normally); stats blink.rounds/clicks/best, tip minigame_blink, sounds blink_start/_vanish/_appear/_win/_lose (silent until clips
//   are assigned), picture case in BuildMinigameModel. A success reports Clicked (bonus skill XP 15).
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// Cube Blink minigame (id "blink", bought in the shop's Minigames tab). For a round the clickable cube shrinks to nothing and teleports to a random
/// spot in the game window; it stays hidden there for a second or two, grows back and stays for a few seconds, then shrinks away and teleports
/// again. The auto clicker is off meanwhile: only your own clicks count. When the round ends you are paid a bonus of <see cref="rewardMultiplier"/> x
/// the pixel's worth for every click you landed (the clicks still pay as usual). Works like Cube Dash.
/// </summary>
public class PixelBlinkMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Cube Blink is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame is switched on before the first round can start.")]
    [SerializeField] private float firstRoundDelay = 80f;

    [Min(1f)]
    [Tooltip("Shortest wait between rounds (seconds).")]
    [SerializeField] private float minInterval = 150f;

    [Min(1f)]
    [Tooltip("Longest wait between rounds (seconds).")]
    [SerializeField] private float maxInterval = 300f;

    [Header("Rules")]
    [Min(5f)]
    [Tooltip("How long a round lasts (seconds).")]
    [SerializeField] private float roundSeconds = 30f;

    [Min(1f)]
    [Tooltip("Bonus paid at the end: this x the pixel's worth x the clicks you landed.")]
    [SerializeField] private float rewardMultiplier = 10f;

    [Header("Teleporting")]
    [Min(0.1f)]
    [Tooltip("Shortest time the cube stays hidden at its new spot (seconds).")]
    [SerializeField] private float hiddenMin = 1f;

    [Min(0.1f)]
    [Tooltip("Longest time the cube stays hidden at its new spot (seconds).")]
    [SerializeField] private float hiddenMax = 2f;

    [Min(0.5f)]
    [Tooltip("Shortest time the cube stays visible before it teleports again (seconds).")]
    [SerializeField] private float visibleMin = 3f;

    [Min(0.5f)]
    [Tooltip("Longest time the cube stays visible before it teleports again (seconds).")]
    [SerializeField] private float visibleMax = 8f;

    [Min(0.05f)]
    [Tooltip("Seconds the cube takes to shrink away or grow back.")]
    [SerializeField] private float shrinkSeconds = 0.3f;

    [Range(0.05f, 0.45f)]
    [Tooltip("A new spot is at least this far from the last one (as a fraction of the screen).")]
    [SerializeField] private float minHop = 0.25f;

    [Tooltip("Left edge of the area the cube teleports in (0 = left of the screen, 1 = right).")]
    [Range(0f, 0.5f)] [SerializeField] private float areaLeft = 0.12f;

    [Tooltip("Right edge of the area.")]
    [Range(0.5f, 1f)] [SerializeField] private float areaRight = 0.88f;

    [Tooltip("Bottom edge of the area (the black bars are kept clear).")]
    [Range(0f, 0.5f)] [SerializeField] private float areaBottom = 0.26f;

    [Tooltip("Top edge of the area.")]
    [Range(0.5f, 1f)] [SerializeField] private float areaTop = 0.74f;

    [Header("Look")]
    [Min(10)]
    [Tooltip("Size of the status text at the top of the screen.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Status line while playing. {0} = clicks, {1} = seconds left.")]
    [SerializeField] private string statusFormat = "CUBE BLINK   clicks {0}   {1}s";

    [Tooltip("Result line. {0} = clicks, {1} = the bonus (a number), {2} = the pixel's name.")]
    [SerializeField] private string resultFormat = "Blink over!  {0} clicks  +{1} {2}";

    [Min(0.5f)]
    [Tooltip("How long the result stays on screen (seconds).")]
    [SerializeField] private float resultSeconds = 3f;

    private enum Phase { Vanish, Hidden, Appear, Visible, Return }

    private float spawnTimer, timeLeft, phaseT, phaseLength, resultTimer;
    private bool activeRound, forcedByMe;
    private int clicks, tierIndex;
    private Phase phase;
    private Vector2 spot;
    private Camera cam;
    private GameObject uiRoot;
    private TMP_Text statusLabel;

    public override string Id => "blink";
    public override string DisplayName => "Cube Blink";
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

    [ContextMenu("Start A Blink Round Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !activeRound) TryStart();
    }

    private void OnCollected(int tier, double amount, bool automatic)
    {
        if (activeRound && !automatic && phase != Phase.Hidden && phase != Phase.Vanish) clicks++;
    }

    private void OnHit(int tier, int hits, int needed, bool automatic)
    {
        if (activeRound && !automatic && phase != Phase.Hidden && phase != Phase.Vanish) clicks++;
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
        PixelMinigame.AutoClickerPaused = true;
        spot = cam.WorldToViewportPoint(clicker.CubeBasePosition);
        BeginVanish();
        BuildUi();
        Report(MinigameEvent.Spawned);
        PixelStats.Count("blink.rounds");
        PixelHints.Trigger("minigame_blink");
        PixelAudio.Play("blink_start");
        return true;
    }

    private Vector2 PickSpot(Vector2 away)
    {
        Vector2 pick = away;
        for (int i = 0; i < 12; i++)
        {
            pick = new Vector2(Random.Range(areaLeft, areaRight), Random.Range(areaBottom, areaTop));
            if ((pick - away).magnitude >= minHop) break;
        }
        return pick;
    }

    private void BeginVanish()
    {
        phase = Phase.Vanish;
        phaseT = 0f;
        phaseLength = shrinkSeconds;
        clicker.SetCubeShrink(0.02f, shrinkSeconds);
        PixelAudio.Play("blink_vanish");
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
                // Time is up: shrink away (if it is showing) and come back to the normal spot.
                phase = Phase.Return;
                phaseT = 0f;
                phaseLength = shrinkSeconds;
                clicker.SetCubeShrink(0.02f, shrinkSeconds);
            }
        }

        phaseT += dt;
        switch (phase)
        {
            case Phase.Vanish:
                if (phaseT >= phaseLength)
                {
                    spot = PickSpot(spot);   // the teleport: while it is too small to see
                    PixelCubeScreenSpot.Place(clicker, cam, spot);   // (the same distance from the camera, so it keeps its size)
                    phase = Phase.Hidden;
                    phaseT = 0f;
                    phaseLength = Random.Range(Mathf.Min(hiddenMin, hiddenMax), Mathf.Max(hiddenMin, hiddenMax));
                }
                break;
            case Phase.Hidden:
                if (phaseT >= phaseLength)
                {
                    phase = Phase.Appear;
                    phaseT = 0f;
                    phaseLength = shrinkSeconds;
                    clicker.SetCubeShrink(1f, shrinkSeconds);
                    PixelAudio.Play("blink_appear");
                }
                break;
            case Phase.Appear:
                if (phaseT >= phaseLength)
                {
                    phase = Phase.Visible;
                    phaseT = 0f;
                    phaseLength = Random.Range(Mathf.Min(visibleMin, visibleMax), Mathf.Max(visibleMin, visibleMax));
                }
                break;
            case Phase.Visible:
                if (phaseT >= phaseLength) BeginVanish();
                break;
            case Phase.Return:
                if (phaseT >= phaseLength)
                {
                    EndRound(true);
                    return;
                }
                break;
        }
        UpdateStatus();
    }

    private void EndRound(bool paid)
    {
        if (!activeRound) return;
        activeRound = false;
        ReleaseCubeRound(clicker, ref forcedByMe);
        clicker.SetCubeOffset(null);
        clicker.SetCubeShrink(1f, shrinkSeconds);
        spawnTimer = RandomBetween(minInterval, maxInterval);

        if (!paid) { DropRoundUi(ref uiRoot, ref statusLabel); return; }

        double bonus = PayCubeRoundBonus(clicker, tierIndex, clicks, rewardMultiplier);
        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];

        PixelStats.Count("blink.clicks", clicks);
        PixelStats.Best("blink.best", clicks);
        if (clicks > 0) Report(MinigameEvent.Clicked);   // a success (bonus skill XP)
        PixelAudio.Play(clicks > 0 ? "blink_win" : "blink_lose");
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
        uiRoot = PixelUIKit.CreateCanvas("Blink UI", 125, new Vector2(1920f, 1080f), true);
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
