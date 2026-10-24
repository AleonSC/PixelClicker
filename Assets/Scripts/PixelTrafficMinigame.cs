// ---- System notes (moved here from CLAUDE.md) ----
// PixelTrafficMinigame.cs / PixelTrafficSynth.cs
//   Red Light Green Light minigame (id traffic, pack unlocksMinigame = "traffic", Minigames tab, needs the RGB pack, 80K each; like
//   Cube Dash: Takeover => true, cube stays clickable, AutoClickerPaused meanwhile): every minInterval..maxInterval s a round of
//   roundSeconds (30) starts. A code-built traffic light (dark housing, red / yellow / green lamps with halos, unlit Sprites/Default
//   materials, billboarded to the camera each frame, lightOffsetCubes 1.9 cube sizes to the right of the cube) cycles Green
//   (greenMin..Max 2-5 s) -> Yellow (3-5 s) -> Red (1.5-3 s) -> Green...; Judge() scores every manual click (PixelCollected / PixelHit
//   with automatic false): green +1, red -1; yellow: a click within hitWindow (0.22 s) of the moment the closing ring meets the cube
//   is +1 (once per beat, lastGoodBeat), any other click -1. During yellow a LineRenderer circle closes round the cube once every
//   beatSeconds (0.9, starts ringStartScale 3.2 cube sizes out, brightens / turns white as it nears; a tick sounds on the beat). The
//   status line is coloured like the light and flashes white on a mistake. At the end it pays rewardMultiplier (10) x the pixel's
//   worth x max(0, points) via AddCurrency (the clicks still pay normally); stats traffic.rounds / traffic.best, tip minigame_traffic;
//   a round with points > 0 reports Clicked (bonus skill XP 20). Sounds are code-made (PixelTrafficSynth, filled in by
//   PixelAudio.SynthFor while the clip lists are empty): traffic_start/_green/_yellow/_red/_tick/_good/_bad/_win/_lose.
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// Red Light Green Light minigame (id "traffic", bought in the shop's Minigames tab). A traffic light stands to the right of the cube for a round
/// and cycles green, yellow, red. On GREEN every click adds a point; on RED every click takes one away; on YELLOW you must click slowly, in time
/// with a circle that closes around the cube - a click as the circle meets the cube adds a point, any other click (or a second click in the same
/// beat) takes one away. The auto clicker is off meanwhile: only your own clicks count. When the round ends you are paid a bonus of
/// <see cref="rewardMultiplier"/> x the pixel's worth for every point you ended with (nothing if you ended below zero). The clicks still pay as usual.
/// Sounds are code-made (PixelTrafficSynth) until you put clips on the traffic_* sounds.
/// </summary>
public class PixelTrafficMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run from the start without buying it in the shop (for testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when Red Light Green Light is bought.)")]
    [SerializeField] private bool running = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the minigame is switched on before the first round can start.")]
    [SerializeField] private float firstRoundDelay = 100f;

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
    [Tooltip("Bonus paid at the end: this x the pixel's worth x your points.")]
    [SerializeField] private float rewardMultiplier = 10f;

    [Header("The light")]
    [Min(0.5f)]
    [Tooltip("Shortest green (seconds).")]
    [SerializeField] private float greenMin = 2f;

    [Min(0.5f)]
    [Tooltip("Longest green (seconds).")]
    [SerializeField] private float greenMax = 5f;

    [Min(1f)]
    [Tooltip("Shortest yellow (seconds). The circle needs a few beats, so keep it a few seconds.")]
    [SerializeField] private float yellowMin = 3f;

    [Min(1f)]
    [Tooltip("Longest yellow (seconds).")]
    [SerializeField] private float yellowMax = 5f;

    [Min(0.5f)]
    [Tooltip("Shortest red (seconds).")]
    [SerializeField] private float redMin = 1.5f;

    [Min(0.5f)]
    [Tooltip("Longest red (seconds).")]
    [SerializeField] private float redMax = 3f;

    [Header("Yellow beat")]
    [Min(0.3f)]
    [Tooltip("Seconds between the circle closing on the cube (the slow pace you must click at during yellow).")]
    [SerializeField] private float beatSeconds = 0.9f;

    [Min(0.05f)]
    [Tooltip("A click this close (seconds) to the circle meeting the cube counts as on time.")]
    [SerializeField] private float hitWindow = 0.22f;

    [Min(1.2f)]
    [Tooltip("How big the circle starts, as a multiple of the cube's size.")]
    [SerializeField] private float ringStartScale = 3.2f;

    [Header("Look")]
    [Min(0.5f)]
    [Tooltip("How far right of the cube the traffic light stands, in cube sizes.")]
    [SerializeField] private float lightOffsetCubes = 1.9f;

    [Min(10)]
    [Tooltip("Size of the status text at the top of the screen.")]
    [SerializeField] private int statusFontSize = 34;

    [Tooltip("Status line while playing. {0} = points, {1} = seconds left.")]
    [SerializeField] private string statusFormat = "RED LIGHT GREEN LIGHT   points {0}   {1}s";

    [Tooltip("Result line. {0} = points, {1} = the bonus (a number), {2} = the pixel's name.")]
    [SerializeField] private string resultFormat = "Light's out!  {0} points  +{1} {2}";

    [Min(0.5f)]
    [Tooltip("How long the result stays on screen (seconds).")]
    [SerializeField] private float resultSeconds = 3f;

    private enum Light { Green, Yellow, Red }

    private static readonly Color RedColor = new Color(1f, 0.2f, 0.2f, 1f), YellowColor = new Color(1f, 0.85f, 0.2f, 1f), GreenColor = new Color(0.25f, 1f, 0.4f, 1f);

    private float spawnTimer, timeLeft, lightTimer, yellowStart, resultTimer, flash;
    private bool activeRound, forcedByMe;
    private int points, tierIndex, lastGoodBeat, lastTickBeat;
    private Light lightState;
    private Camera cam;
    private GameObject uiRoot, lightRoot, ringObject;
    private TMP_Text statusLabel;
    private Renderer[] lamps = new Renderer[3];
    private Renderer[] halos = new Renderer[3];
    private Material[] lampMaterials = new Material[3];
    private Material[] haloMaterials = new Material[3];
    private Material ringMaterial, housingMaterial;
    private LineRenderer ring;

    public override string Id => "traffic";
    public override string DisplayName => "Red Light Green Light";
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
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelTrafficMinigame: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        clicker.PixelCollected += OnCollected;
        clicker.PixelHit += OnHit;
        if (startRunning) running = true;
        spawnTimer = firstRoundDelay;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (clicker != null)
        {
            clicker.PixelCollected -= OnCollected;
            clicker.PixelHit -= OnHit;
        }
        if (activeRound) EndRound(false);
        DestroyLight();
    }

    protected override void OnDespawned()
    {
        if (activeRound) EndRound(false);
        DestroyLight();
        if (uiRoot != null) { Destroy(uiRoot); uiRoot = null; statusLabel = null; }
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    [ContextMenu("Start A Traffic Round Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !activeRound) TryStart();
    }

    private void OnCollected(int tier, double amount, bool automatic)
    {
        if (activeRound && !automatic) Judge();
    }

    private void OnHit(int tier, int hits, int needed, bool automatic)
    {
        if (activeRound && !automatic) Judge();
    }

    private void Update()
    {
        if (ResultShownLong(ref resultTimer, activeRound, uiRoot)) { Destroy(uiRoot); uiRoot = null; statusLabel = null; }
        if (activeRound) { Tick(Time.deltaTime); return; }
        if (!running || UserDisabled) return;

        if (SpawnDue(ref spawnTimer, PixelPauseMenu.IsPaused || clicker.CubeHidden || TakeoverActive) && !TryStart()) spawnTimer = 12f;
    }

    // ------------------------------------------------------------------
    // Scoring a click
    // ------------------------------------------------------------------

    private void Judge()
    {
        switch (lightState)
        {
            case Light.Green:
                points++;
                PixelAudio.Play("traffic_good");
                break;
            case Light.Red:
                points--;
                flash = 1f;
                PixelAudio.Play("traffic_bad");
                break;
            default:
            {
                // Yellow: only a click close to the moment the circle meets the cube counts, once per beat.
                float t = Time.time - yellowStart;
                int beat = Mathf.Max(1, Mathf.RoundToInt(t / beatSeconds));
                float error = Mathf.Abs(t - beat * beatSeconds);
                if (error <= hitWindow && beat != lastGoodBeat)
                {
                    lastGoodBeat = beat;
                    points++;
                    PixelAudio.Play("traffic_good");
                }
                else
                {
                    points--;
                    flash = 1f;
                    PixelAudio.Play("traffic_bad");
                }
                break;
            }
        }
        UpdateStatus();
    }

    // ------------------------------------------------------------------
    // A round
    // ------------------------------------------------------------------

    private bool TryStart()
    {
        cam = clicker.ViewCamera;
        if (cam == null || clicker.PixelTransform == null) return false;

        tierIndex = clicker.CurrentClickTierIndex;
        if (tierIndex < 0 || tierIndex >= clicker.Tiers.Length) return false;
        if (clicker.Tiers[tierIndex].rareDrop || clicker.Tiers[tierIndex].flyAway)
        {
            tierIndex = clicker.IndexOf(PixelClicker.PixelType.White);   // never a Dragon Cube / meteor
            if (tierIndex < 0) return false;
        }

        // The cube stays this pixel for the whole round (a running potion already does the same by itself).
        PixelConsumables potions = PixelFind.First<PixelConsumables>();
        forcedByMe = potions == null || !potions.IsActive;
        if (forcedByMe) clicker.SetForcedSpawnTier(clicker.Tiers[tierIndex].type);

        points = 0;
        timeLeft = roundSeconds;
        flash = 0f;
        activeRound = true;
        PixelMinigame.AutoClickerPaused = true;
        BuildLight();
        BuildUi();
        SetLight(Light.Green);
        Report(MinigameEvent.Spawned);
        PixelStats.Count("traffic.rounds");
        PixelHints.Trigger("minigame_traffic");
        PixelAudio.Play("traffic_start");
        return true;
    }

    private void SetLight(Light next)
    {
        lightState = next;
        switch (next)
        {
            case Light.Green:
                lightTimer = Random.Range(Mathf.Min(greenMin, greenMax), Mathf.Max(greenMin, greenMax));
                PixelAudio.Play("traffic_green");
                break;
            case Light.Yellow:
                lightTimer = Random.Range(Mathf.Min(yellowMin, yellowMax), Mathf.Max(yellowMin, yellowMax));
                yellowStart = Time.time;
                lastGoodBeat = 0;
                lastTickBeat = 0;
                PixelAudio.Play("traffic_yellow");
                break;
            default:
                lightTimer = Random.Range(Mathf.Min(redMin, redMax), Mathf.Max(redMin, redMax));
                PixelAudio.Play("traffic_red");
                break;
        }
        ApplyLamps();
        UpdateStatus();
    }

    private void Tick(float dt)
    {
        if (clicker == null || cam == null) { EndRound(false); return; }
        if (Time.timeScale <= 0f) return;

        timeLeft -= dt;
        if (timeLeft <= 0f) { EndRound(true); return; }

        lightTimer -= dt;
        if (lightTimer <= 0f) SetLight(lightState == Light.Green ? Light.Yellow : lightState == Light.Yellow ? Light.Red : Light.Green);

        flash = Mathf.MoveTowards(flash, 0f, dt * 4f);
        UpdateRing();
        FaceCamera();
        UpdateStatus();
    }

    private void EndRound(bool paid)
    {
        if (!activeRound) return;
        activeRound = false;
        PixelMinigame.AutoClickerPaused = false;
        if (forcedByMe) clicker.ClearForcedSpawnTier();
        forcedByMe = false;
        DestroyLight();
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));

        if (!paid) { if (uiRoot != null) { Destroy(uiRoot); uiRoot = null; statusLabel = null; } return; }

        PixelClicker.PixelTier tier = clicker.Tiers[tierIndex];
        double perClick = tier.amountPerClick * clicker.PayoutMultiplier(tierIndex) * clicker.ClickMultiplier / Mathf.Max(1, tier.clicksToCollect);
        double bonus = Mathf.Max(0, points) * rewardMultiplier * perClick;
        if (bonus > 0d) clicker.AddCurrency(tierIndex, bonus);

        PixelStats.Best("traffic.best", Mathf.Max(0, points));
        if (points > 0) Report(MinigameEvent.Clicked);   // a success (bonus skill XP)
        PixelAudio.Play(points > 0 ? "traffic_win" : "traffic_lose");
        string line = string.Format(resultFormat, points, PixelClicker.FormatNumber(bonus), tier.displayName);
        if (statusLabel != null) { statusLabel.text = line; statusLabel.color = Color.white; }
        PixelHints.Announce(line);
        resultTimer = resultSeconds;
    }

    // ------------------------------------------------------------------
    // The traffic light and the closing circle
    // ------------------------------------------------------------------

    private Material FlatMaterial(Color color, int queue)
    {
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return null;
        Material m = new Material(shader) { color = color, renderQueue = queue };
        return m;
    }

    private GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        if (material != null) r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return g;
    }

    private void BuildLight()
    {
        DestroyLight();
        float size = Mathf.Max(0.1f, clicker.PixelBaseSize);
        lightRoot = new GameObject("Traffic Light");
        Track(lightRoot);
        Vector3 right = cam.transform.right;
        lightRoot.transform.position = clicker.CubeBasePosition + right * (size * lightOffsetCubes);
        lightRoot.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);   // faces the camera (kept up to date in FaceCamera)

        float w = size * 0.55f, h = size * 1.7f;
        housingMaterial = FlatMaterial(new Color(0.08f, 0.08f, 0.1f, 1f), 3000);
        Prim(PrimitiveType.Cube, lightRoot.transform, "Housing", Vector3.zero, new Vector3(w, h, size * 0.12f), housingMaterial);
        Prim(PrimitiveType.Cube, lightRoot.transform, "Pole", new Vector3(0f, -h * 0.5f - size * 0.35f, 0.01f), new Vector3(size * 0.1f, size * 0.7f, size * 0.08f), housingMaterial);
        Color[] colors = { RedColor, YellowColor, GreenColor };
        for (int i = 0; i < 3; i++)
        {
            float y = (1 - i) * h * 0.3f;   // red on top, green at the bottom
            lampMaterials[i] = FlatMaterial(colors[i], 3001);
            haloMaterials[i] = FlatMaterial(new Color(colors[i].r, colors[i].g, colors[i].b, 0f), 3002);
            lamps[i] = Prim(PrimitiveType.Sphere, lightRoot.transform, "Lamp " + i, new Vector3(0f, y, -size * 0.08f), Vector3.one * (w * 0.7f), lampMaterials[i]).GetComponent<Renderer>();
            halos[i] = Prim(PrimitiveType.Sphere, lightRoot.transform, "Halo " + i, new Vector3(0f, y, -size * 0.09f), Vector3.one * (w * 1.5f), haloMaterials[i]).GetComponent<Renderer>();
        }

        // The circle that closes on the cube during yellow.
        ringObject = new GameObject("Beat Ring");
        Track(ringObject);
        ring = ringObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = 64;
        ring.widthMultiplier = size * 0.06f;
        ring.alignment = LineAlignment.View;
        ringMaterial = FlatMaterial(YellowColor, 3003);
        if (ringMaterial != null) ring.sharedMaterial = ringMaterial;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringObject.SetActive(false);
    }

    private void DestroyLight()
    {
        if (lightRoot != null) Destroy(lightRoot);
        if (ringObject != null) Destroy(ringObject);
        lightRoot = null;
        ringObject = null;
        ring = null;
        foreach (Material m in lampMaterials) if (m != null) Destroy(m);
        foreach (Material m in haloMaterials) if (m != null) Destroy(m);
        if (housingMaterial != null) Destroy(housingMaterial);
        if (ringMaterial != null) Destroy(ringMaterial);
        lampMaterials = new Material[3];
        haloMaterials = new Material[3];
        housingMaterial = ringMaterial = null;
    }

    private void FaceCamera()
    {
        if (lightRoot == null || cam == null) return;
        float size = Mathf.Max(0.1f, clicker.PixelBaseSize);
        lightRoot.transform.position = clicker.CubeBasePosition + cam.transform.right * (size * lightOffsetCubes);
        lightRoot.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
    }

    /// <summary>The lit lamp glows (with a halo); the other two are dim.</summary>
    private void ApplyLamps()
    {
        for (int i = 0; i < 3; i++)
        {
            bool on = (i == 0 && lightState == Light.Red) || (i == 1 && lightState == Light.Yellow) || (i == 2 && lightState == Light.Green);
            Color baseColor = i == 0 ? RedColor : i == 1 ? YellowColor : GreenColor;
            if (lampMaterials[i] != null) lampMaterials[i].color = on ? baseColor : baseColor * 0.22f + new Color(0f, 0f, 0f, 0.78f);
            if (haloMaterials[i] != null) haloMaterials[i].color = new Color(baseColor.r, baseColor.g, baseColor.b, on ? 0.35f : 0f);
        }
    }

    /// <summary>During yellow a circle closes around the cube once per beat and meets it on the beat (a tick sounds then).</summary>
    private void UpdateRing()
    {
        if (ring == null) return;
        bool show = lightState == Light.Yellow;
        if (ringObject.activeSelf != show) ringObject.SetActive(show);
        if (!show) return;

        float t = Time.time - yellowStart;
        float phase = Mathf.Repeat(t, beatSeconds) / beatSeconds;   // 0 = just opened, 1 = closing
        int beat = Mathf.FloorToInt(t / beatSeconds) + 1;           // the beat it is closing towards
        if (beat != lastTickBeat && phase > 0.97f) { lastTickBeat = beat; PixelAudio.Play("traffic_tick"); }

        float size = Mathf.Max(0.1f, clicker.PixelBaseSize);
        float endR = size * 0.75f, startR = size * 0.5f * ringStartScale;
        float radius = Mathf.Lerp(startR, endR, phase);
        Vector3 centre = clicker.CubeBasePosition;
        Vector3 right = cam.transform.right, up = cam.transform.up;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, centre + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
        }
        // Brighter as it nears the cube; white right on the beat.
        float near = Mathf.Clamp01((phase - 0.7f) / 0.3f);
        Color c = Color.Lerp(new Color(YellowColor.r, YellowColor.g, YellowColor.b, 0.55f), Color.white, near);
        if (ringMaterial != null) ringMaterial.color = c;
    }

    // ------------------------------------------------------------------
    // The status line
    // ------------------------------------------------------------------

    private void BuildUi()
    {
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = PixelUIKit.CreateCanvas("Traffic UI", 125, new Vector2(1920f, 1080f), true);
        uiRoot.transform.SetParent(transform, false);
        Track(uiRoot);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        statusLabel = PixelStatusBox.Create(clicker.UIFont, uiRoot.transform, statusFontSize);   // slides out from under the top bar
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (statusLabel == null || !activeRound) return;
        PixelUIKit.SetText(statusLabel, string.Format(statusFormat, points, Mathf.CeilToInt(Mathf.Max(0f, timeLeft))));
        Color target = lightState == Light.Green ? GreenColor : lightState == Light.Yellow ? YellowColor : RedColor;
        statusLabel.color = Color.Lerp(target, Color.white, flash);   // flashes white on a mistake
    }
}
