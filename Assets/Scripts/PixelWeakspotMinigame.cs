// ---- System notes (moved here from CLAUDE.md) ----
// PixelWeakspotMinigame.cs
//   Weak Spots (id weakspot, the skills' minigame; no shop pack, Running = any skill pack other than Standard is the pixel pack in
//   play (startRunning for testing), Toggles > Minigames switch like the others; Takeover => true so nothing else starts meanwhile):
//   every minInterval..maxInterval (120-240 s, first after firstRoundDelay 60 s) a round zooms a SECOND camera (CopyFrom of the main
//   one, which draws nothing meanwhile, like first person mode) in on the cube (zoomDistance 2.6 cube widths, zoomSeconds), drops
//   Time.timeScale to slowScale (0.1, fixedDeltaTime scaled) and 3-5 glowing weak spots (code-textured camera-facing quads with a
//   shrinking timer ring, found by raycasting the zoom camera onto the cube's hit box, spaced spotGap apart) appear; the cube ignores
//   ordinary clicks (ExternalClickBlock, PixelClicker.HoldSpin stops its spin, auto clicker paused). Variations per spot (RollKind,
//   disableVariants): still (red/orange), drifting (blue, bounces round the cube face in viewport space, re-raycast each frame,
//   driftChance 0 = 0.3, driftSpeed), pulsing (green, shrinks to pulseMin and swells every pulseSeconds, its click radius shrinks with
//   it, pulseChance 0 = 0.3) and flickering (yellow, shows flickerOn s then vanishes flickerOff s, can't be hit while gone,
//   flickerChance 0 = 0.2). Click all of them within huntSeconds (4 real seconds) for baseXp + speedXp x time left of XP for the skill
//   in training via GrantSkillXp; running out of time pays nothing. Time Stop / Time Slow / the title screen end the round at once.
//   The zoom camera starts / ends at the REAL camera's live pose every frame (PlaceCamera) and NeedsNormalCamera is false, so the player's orbit / zoom view is left alone and nothing pops when
//   the round ends. Each round logs one line about the cube, its mesh and the spot collider (DescribeCube; the F10 'mark this moment' report carries it).
//   Stats weakspot.rounds/wins, tip minigame_weakspot, sounds weakspot_start/_hit/_win/_lose (silent until clips are assigned).
// ---------------------------------------------------
using TMPro;
using UnityEngine;

/// <summary>
/// Weak Spots (id "weakspot"), the skills' minigame. It needs no shop pack: it runs by itself while any skill pack (Mining, Woodcutting, Farming...) is the pixel pack in play.
/// Every few minutes the camera zooms in on the cube, time slows right down and a handful of glowing weak spots appear on the cube's face.
/// Click every one of them before the time runs out and the Mining skill gets bonus XP (more the faster you were).
/// The zoom uses a second camera (like first person mode), so the real camera and everything that reads it stay put.
/// </summary>
public class PixelWeakspotMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker (camera, cube, payouts). Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("State")]
    [Tooltip("Run even while no skill pack is in play (testing).")]
    [SerializeField] private bool startRunning = false;

    [Header("When it starts")]
    [Min(0f)]
    [Tooltip("Seconds after the game starts before the first round can happen.")]
    [SerializeField] private float firstRoundDelay = 60f;

    [Min(1f)]
    [Tooltip("Shortest wait between rounds (seconds).")]
    [SerializeField] private float minInterval = 120f;

    [Min(1f)]
    [Tooltip("Longest wait between rounds (seconds).")]
    [SerializeField] private float maxInterval = 240f;

    [Header("Rules")]
    [Min(1)]
    [Tooltip("Fewest weak spots in a round.")]
    [SerializeField] private int spotsMin = 3;

    [Min(1)]
    [Tooltip("Most weak spots in a round.")]
    [SerializeField] private int spotsMax = 5;

    [Min(0.5f)]
    [Tooltip("Real seconds you have to click all of them once they appear.")]
    [SerializeField] private float huntSeconds = 4f;

    [Range(0.02f, 1f)]
    [Tooltip("The speed of the game during the round (1 = normal; 0.1 = ten times slower).")]
    [SerializeField] private float slowScale = 0.1f;

    [Min(0f)]
    [Tooltip("XP for clearing every weak spot (goes to the skill in training).")]
    [SerializeField] private float baseXp = 30f;

    [Min(0f)]
    [Tooltip("Extra Mining XP for clearing them instantly (scaled down by the time you used).")]
    [SerializeField] private float speedXp = 30f;

    [Header("Camera")]
    [Min(1.2f)]
    [Tooltip("How far the zoomed camera stands from the cube, in cube widths.")]
    [SerializeField] private float zoomDistance = 2.6f;

    [Min(0.05f)]
    [Tooltip("Seconds the zoom in / out takes (real time).")]
    [SerializeField] private float zoomSeconds = 0.45f;

    [Header("Weak spots")]
    [Range(0.08f, 0.5f)]
    [Tooltip("Size of a weak spot as a fraction of the cube's width.")]
    [SerializeField] private float spotSize = 0.2f;

    [Range(0.05f, 0.4f)]
    [Tooltip("Smallest gap between two spots as a fraction of the screen height.")]
    [SerializeField] private float spotGap = 0.14f;

    [Min(10f)]
    [Tooltip("Click forgiveness: a click this many screen pixels from a spot's middle (at least) counts.")]
    [SerializeField] private float clickPixels = 44f;

    [Tooltip("Colour of the weak spots.")]
    [SerializeField] private Color spotColor = new Color(1f, 0.45f, 0.1f, 1f);

    [Header("Variations")]
    [Tooltip("Turn off the variations: every weak spot just sits still.")]
    [SerializeField] private bool disableVariants = false;

    [Range(0f, 1f)]
    [Tooltip("Chance a weak spot DRIFTS across the cube's face, bouncing off the edges of the play area. 0 = the default (0.3).")]
    [SerializeField] private float driftChance = 0f;

    [Range(0f, 1f)]
    [Tooltip("Chance a weak spot PULSES: it shrinks and swells, and is harder to hit while small. 0 = the default (0.3).")]
    [SerializeField] private float pulseChance = 0f;

    [Range(0f, 1f)]
    [Tooltip("Chance a weak spot FLICKERS: it keeps vanishing and reappearing, and can only be hit while it shows. 0 = the default (0.2).")]
    [SerializeField] private float flickerChance = 0f;

    [Min(0.05f)]
    [Tooltip("How fast a drifting spot moves (screen widths per real second). 0.05 minimum; the default ~0.12.")]
    [SerializeField] private float driftSpeed = 0.12f;

    [Min(0.2f)]
    [Tooltip("Seconds for one shrink-and-swell of a pulsing spot.")]
    [SerializeField] private float pulseSeconds = 1.3f;

    [Range(0.1f, 0.9f)]
    [Tooltip("How small a pulsing spot gets (fraction of its full size).")]
    [SerializeField] private float pulseMin = 0.35f;

    [Min(0.1f)]
    [Tooltip("How long a flickering spot shows each time (real seconds).")]
    [SerializeField] private float flickerOn = 0.9f;

    [Min(0.1f)]
    [Tooltip("How long a flickering spot is gone each time (real seconds).")]
    [SerializeField] private float flickerOff = 0.5f;

    [Header("Look")]
    [Min(10)]
    [Tooltip("Size of the status text at the top of the screen.")]
    [SerializeField] private int statusFontSize = 36;

    [Tooltip("Status line while hunting. {0} = spots hit, {1} = spots in total, {2} = seconds left.")]
    [SerializeField] private string statusFormat = "WEAK SPOTS   {0} / {1}   {2}s";

    [Tooltip("Shown when every spot was hit. {0} = the bonus XP, {1} = the skill name.")]
    [SerializeField] private string winFormat = "Perfect!  +{0} {1} XP";

    [Tooltip("Shown when time ran out.")]
    [SerializeField] private string loseText = "Too slow...";

    [Min(0.2f)]
    [Tooltip("How long the result stays before the camera zooms back (real seconds).")]
    [SerializeField] private float resultSeconds = 1f;

    private class Spot
    {
        public Transform root, ring;
        public Material mat, ringMat;
        public Vector3 pos, local;
        public float born;
        public int kind;                 // 0 still, 1 drifts, 2 pulses, 3 flickers
        public Vector2 vp, vel;          // viewport position / velocity of a drifting spot
        public float phase;              // random offset of the pulse / flicker
        public float sizeK = 1f;         // current size (pulsing)
        public bool shown = true;        // flickering: visible right now
        public bool hit;
        public float hitT = -1f;
    }

    private System.Action<bool> onFinished;   // set by a fishing round: told whether every spot was hit
    private bool fishing;

    /// <summary>Is the minigame there and switched on? (If not, fish are reeled in with 7-25 plain clicks.)</summary>
    public static bool FishingAvailable
    {
        get { PixelWeakspotMinigame m = PixelFind.First<PixelWeakspotMinigame>(); return m != null && !m.UserDisabled; }
    }

    /// <summary>Starts a round for a fish on the cube (Fishing). 'done' gets true when every spot was hit, false otherwise. False = could not start (the caller catches the fish the plain way).</summary>
    public static bool TryStartFishing(System.Action<bool> done)
    {
        PixelWeakspotMinigame m = PixelFind.First<PixelWeakspotMinigame>();
        return m != null && m.BeginFishing(done);
    }

    private bool BeginFishing(System.Action<bool> done)
    {
        if (phase != Phase.Idle || UserDisabled || !CanStartNow(true)) return false;
        onFinished = done;
        fishing = true;
        if (TryStart()) return true;
        onFinished = null;
        fishing = false;
        return false;
    }

    private enum Phase { Idle, ZoomIn, Hunt, Result, ZoomOut }

    private readonly System.Collections.Generic.List<Spot> spots = new System.Collections.Generic.List<Spot>();
    private Phase phase = Phase.Idle;
    private float phaseT, huntLeft, spawnTimer, savedFixed, savedScale = 1f;
    private bool won, slowed;
    private Camera mainCam, zoomCam;
    private int savedMask;
    private Vector3 fromPos, toPos;
    private Quaternion fromRot, toRot;
    private float fromOrtho, toOrtho;
    private GameObject uiRoot;
    private TMP_Text statusLabel;
    private Texture2D spotTex, ringTex;

    public override string Id => "weakspot";
    public override string DisplayName => "Weak Spots";
    public override bool Running => startRunning || (PixelSkills.Instance != null && PixelSkills.Instance.ActiveId != PixelSkills.StandardId);
    public override bool Busy => phase != Phase.Idle;
    public override bool Takeover => true;   // nothing else starts meanwhile (the cube is not 'taken over': TakeoverActive stays off)
    public override bool NeedsNormalCamera => false;   // it zooms its OWN camera: the player's view (orbit / zoom) is left alone, so nothing pops when the round ends

    public override void Activate() { startRunning = true; }
    public override void Deactivate() { startRunning = false; if (phase != Phase.Idle) Finish(); }

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) { enabled = false; return; }
        spawnTimer = firstRoundDelay;
    }

    protected override void OnDestroy()
    {
        onFinished = null;
        base.OnDestroy();
        if (phase != Phase.Idle) Finish();
    }

    protected override void OnDespawned()
    {
        if (phase != Phase.Idle) Finish();
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    [ContextMenu("Start A Weak Spot Round Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && phase == Phase.Idle) TryStart();
    }

    private void Update()
    {
        if (phase == Phase.Idle)
        {
            if (!Running || UserDisabled) return;
            spawnTimer -= Time.unscaledDeltaTime;
            if (spawnTimer > 0f) return;
            if (!CanStartNow()) { spawnTimer = PixelMinigameLimits.RetrySeconds; return; }
            if (!TryStart()) spawnTimer = 12f;
            return;
        }

        // Anything that takes over the clock or the screen ends the round at once.
        if (PixelTimeStop.IsStopped || PixelTimeStop.IsSlowed || PixelTitleScreen.Showing) { Finish(); return; }
        if (PixelPauseMenu.IsPaused || PixelTutorialDim.Active) return;   // the pause menu / a frozen tip box owns timeScale; our clocks wait
        if (phase != Phase.ZoomOut && slowed && !Mathf.Approximately(Time.timeScale, slowScale)) ApplySlow(true);

        float dt = Time.unscaledDeltaTime;
        phaseT += dt;
        switch (phase)
        {
            case Phase.ZoomIn:
                PlaceCamera(Mathf.Clamp01(phaseT / zoomSeconds));
                if (phaseT >= zoomSeconds) BeginHunt();
                break;
            case Phase.Hunt:
                huntLeft -= dt;
                UpdateSpots();
                HandleClick();
                if (AllHit()) { won = true; Resolve(); }
                else if (huntLeft <= 0f) { won = false; Resolve(); }
                UpdateStatus();
                break;
            case Phase.Result:
                UpdateSpots();
                if (phaseT >= resultSeconds) BeginZoomOut();
                break;
            case Phase.ZoomOut:
                PlaceCamera(1f - Mathf.Clamp01(phaseT / zoomSeconds));
                if (phaseT >= zoomSeconds) Finish();
                break;
        }
    }

    private bool CanStartNow(bool playerStarted = false)
    {
        return PixelMinigameLimits.AllowAnother(this, playerStarted) && !PixelPauseMenu.IsPaused && !TakeoverActive && !clicker.CubeHidden
            && !PixelTitleScreen.Showing && !PixelCameraIntro.Moving && !PixelCameraIntro.ClicksLocked
            && !PixelTimeStop.IsStopped && !PixelTimeStop.IsSlowed && !PixelFirstPerson.Active && !PixelRobotWorker.Piloting
            && Mathf.Approximately(Time.timeScale, 1f);
    }

    // ------------------------------------------------------------------
    // A round
    // ------------------------------------------------------------------

    private bool TryStart()
    {
        mainCam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (mainCam == null || clicker.PixelTransform == null) return false;
        int tier = clicker.CurrentClickTierIndex;
        if (tier < 0 || tier >= clicker.Tiers.Length) return false;
        if (clicker.Tiers[tier].flyAway) return false;

        // A second camera does the zooming; the real one draws nothing meanwhile.
        GameObject co = new GameObject("Weak Spot Camera");
        co.transform.SetParent(transform, false);
        Track(co);
        zoomCam = co.AddComponent<Camera>();
        zoomCam.CopyFrom(mainCam);
        zoomCam.depth = mainCam.depth + 1f;
        savedMask = mainCam.cullingMask;
        mainCam.cullingMask = 0;

        Vector3 cube = clicker.PixelTransform.position;
        float size = clicker.PixelBaseSize;
        fromPos = mainCam.transform.position;
        fromRot = mainCam.transform.rotation;
        Vector3 dir = (fromPos - cube).normalized;
        toPos = cube + dir * (size * zoomDistance);
        toRot = Quaternion.LookRotation(cube - toPos, Vector3.up);
        fromOrtho = mainCam.orthographicSize;
        toOrtho = Mathf.Max(0.1f, size * zoomDistance * 0.5f);
        PlaceCamera(0f);

        PixelClicker.ExternalClickBlock = true;   // the cube ignores ordinary clicks: the weak spots take them
        PixelClicker.HoldSpin = true;
        PixelMinigame.AutoClickerPaused = true;
        ApplySlow(true);
        slowed = true;

        won = false;
        phase = Phase.ZoomIn;
        phaseT = 0f;
        BuildUi();
        Report(MinigameEvent.Spawned);
        PixelStats.Count("weakspot.rounds");
        PixelHints.Trigger("minigame_weakspot");
        PixelAudio.Play("weakspot_start");
        return true;
    }

    private void ApplySlow(bool on)
    {
        if (on)
        {
            if (!slowed) { savedScale = Time.timeScale > 0f ? Time.timeScale : 1f; savedFixed = Time.fixedDeltaTime; }
            Time.timeScale = slowScale;
            Time.fixedDeltaTime = (savedFixed > 0f ? savedFixed : 0.02f) * slowScale;
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = savedFixed > 0f ? savedFixed : 0.02f;
        }
    }

    private void PlaceCamera(float k)
    {
        if (zoomCam == null || mainCam == null) return;
        // The real camera may still be moving (orbit, auto orbit): start and end the zoom where it IS, so the zoom out lands exactly on it - no pop.
        fromPos = mainCam.transform.position;
        fromRot = mainCam.transform.rotation;
        fromOrtho = mainCam.orthographicSize;
        k = k * k * (3f - 2f * k);
        zoomCam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
        if (zoomCam.orthographic) zoomCam.orthographicSize = Mathf.Lerp(fromOrtho, toOrtho, k);
    }

    private void BeginHunt()
    {
        PlaceCamera(1f);
        int count = Random.Range(Mathf.Min(spotsMin, spotsMax), Mathf.Max(spotsMin, spotsMax) + 1);
        for (int i = 0; i < count; i++)
        {
            Spot s = MakeSpot();
            if (s == null) break;
            spots.Add(s);
        }
        if (spots.Count == 0) { Finish(); return; }
        huntLeft = huntSeconds;
        phase = Phase.Hunt;
        phaseT = 0f;
        DescribeCube(count);
    }

    /// <summary>One log line about the cube and the collider the spots were placed on (for chasing glitches: it lands in the marked-moment report's recent log).</summary>
    private void DescribeCube(int wanted)
    {
        MeshFilter mf = clicker.PixelTransform != null ? clicker.PixelTransform.GetComponentInChildren<MeshFilter>() : null;
        int tier = clicker.CurrentClickTierIndex;
        string tierName = tier >= 0 && tier < clicker.Tiers.Length ? clicker.Tiers[tier].displayName : "?";
        PixelDebug.Info("Weak Spots: cube=" + tierName + " mesh=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.name + " tris=" + mf.sharedMesh.GetIndexCount(0) / 3 : "none") +
                        " scale=" + (clicker.PixelTransform != null ? clicker.PixelTransform.localScale.ToString("F2") : "?") + " surface=" + (surface != null ? surface.bounds.size.ToString("F2") : "none") +
                        " spots=" + spots.Count + "/" + wanted + (fishing ? " (fishing round)" : ""));
    }

    /// <summary>The point on the cube's face the zoom camera sees through a viewport position (false when it misses the cube).</summary>
    private bool Project(Vector2 vp, out Vector3 pos)
    {
        pos = Vector3.zero;
        Transform cubeT = clicker.PixelTransform;
        Ray ray = zoomCam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
        EnsureSurface();
        float best = float.MaxValue, bestSurface = float.MaxValue;
        RaycastHit pick = default, pickSurface = default;
        foreach (RaycastHit h in hits)
        {
            if (surface != null && h.collider == surface)
            {
                if (h.distance < bestSurface) { bestSurface = h.distance; pickSurface = h; }
                continue;
            }
            if (h.transform != cubeT && !h.transform.IsChildOf(cubeT) && (clicker.HitboxTransform == null || h.transform != clicker.HitboxTransform)) continue;
            if (h.distance < best) { best = h.distance; pick = h; }
        }
        // The cube's real shape wins over its box: a rounded block (shell, ghost) bulges past the box and would hide a spot placed on the box.
        if (bestSurface < float.MaxValue) { best = bestSurface; pick = pickSurface; }
        if (best == float.MaxValue) return false;
        pos = pick.point + pick.normal * (clicker.PixelBaseSize * 0.05f);
        return true;
    }

    private MeshCollider surface;

    /// <summary>A hidden convex collider with the cube's actual mesh (the hit box is always a plain cube), so weak spots sit on what the player sees.</summary>
    private void EnsureSurface()
    {
        if (surface != null || clicker == null || clicker.PixelTransform == null) return;
        MeshFilter mf = clicker.PixelTransform.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        GameObject go = new GameObject("Weak Spot Surface");
        go.transform.SetParent(mf.transform, false);
        surface = go.AddComponent<MeshCollider>();
        surface.sharedMesh = mf.sharedMesh;
        surface.convex = true;
        surface.isTrigger = true;
    }

    private int RollKind()
    {
        if (disableVariants) return 0;
        float d = driftChance > 0f ? driftChance : 0.3f, p = pulseChance > 0f ? pulseChance : 0.3f, f = flickerChance > 0f ? flickerChance : 0.2f;
        float r = Random.value;
        if (r < d) return 1;
        if (r < d + p) return 2;
        if (r < d + p + f) return 3;
        return 0;
    }

    /// <summary>Finds a spot on the face of the cube that the camera sees, away from the others.</summary>
    private Spot MakeSpot()
    {
        float size = clicker.PixelBaseSize;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 vp = new Vector2(Random.Range(0.3f, 0.7f), Random.Range(0.3f, 0.7f));
            Vector3 pos;
            if (!Project(vp, out pos)) continue;
            bool apart = true;
            foreach (Spot o in spots)
                if (Vector2.Distance(vp, o.vp) < spotGap) { apart = false; break; }
            if (!apart) continue;
            Spot s = BuildSpot(pos, size * spotSize);
            s.vp = vp;
            s.kind = RollKind();
            s.phase = Random.value * 6.28f;
            if (s.kind == 1)
            {
                float a = Random.value * 6.28f;
                s.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Max(0.05f, driftSpeed);
            }
            return s;
        }
        return null;
    }

    private Spot BuildSpot(Vector3 pos, float size)
    {
        EnsureTextures();
        Spot s = new Spot { pos = pos, born = Time.unscaledTime };
        if (clicker.PixelTransform != null) s.local = clicker.PixelTransform.InverseTransformPoint(pos);
        GameObject root = new GameObject("Weak Spot");
        root.transform.SetParent(transform, false);
        Track(root);
        root.transform.position = pos;
        s.root = root.transform;
        s.mat = MakeMat(spotTex);
        s.ringMat = MakeMat(ringTex);
        MakeQuad(root.transform, "Glow", s.mat, size, 0);
        s.ring = MakeQuad(root.transform, "Ring", s.ringMat, size, 1).transform;
        return s;
    }

    private Material MakeMat(Texture2D tex)
    {
        Material m = new Material(PixelShaders.SpriteDefault());
        m.mainTexture = tex;
        m.renderQueue = 3100;
        return m;
    }

    private GameObject MakeQuad(Transform parent, string name, Material mat, float size, int order)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localScale = Vector3.one * size;
        q.GetComponent<Renderer>().sharedMaterial = mat;
        return q;
    }

    private void EnsureTextures()
    {
        if (spotTex != null) return;
        const int n = 64;
        spotTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                float core = Mathf.Clamp01(1f - d / 0.55f);
                float glow = Mathf.Clamp01(1f - d) * 0.5f;
                float a = Mathf.Max(core, glow);
                float cross = (Mathf.Abs(x - n / 2) < 2 || Mathf.Abs(y - n / 2) < 2) && d < 0.9f ? 0.7f : 0f;
                spotTex.SetPixel(x, y, new Color(1f, Mathf.Lerp(0.85f, 1f, core), Mathf.Lerp(0.5f, 1f, core), Mathf.Clamp01(a + cross)));
                float r = Mathf.Abs(d - 0.92f);
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - r / 0.07f)));
            }
        spotTex.Apply();
        ringTex.Apply();
    }

    private void UpdateSpots()
    {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        foreach (Spot s in spots)
        {
            if (s.root == null) continue;
            s.root.rotation = zoomCam != null ? zoomCam.transform.rotation : s.root.rotation;   // faces the camera
            if (s.hit)
            {
                float k = Mathf.Clamp01((t - s.hitT) / 0.25f);
                s.root.localScale = Vector3.one * Mathf.Lerp(1.2f, 0f, k);
                continue;
            }

            if (clicker.PixelTransform != null)   // the cube turns / bobs: the spot stays stuck to its face
            {
                s.pos = clicker.PixelTransform.TransformPoint(s.local);
                s.root.position = s.pos;
            }
            if (s.kind == 1 && phase == Phase.Hunt && zoomCam != null)
            {
                Vector2 np = s.vp + s.vel * dt;
                if (np.x < 0.28f || np.x > 0.72f) { s.vel.x = -s.vel.x; np.x = Mathf.Clamp(np.x, 0.28f, 0.72f); }
                if (np.y < 0.28f || np.y > 0.72f) { s.vel.y = -s.vel.y; np.y = Mathf.Clamp(np.y, 0.28f, 0.72f); }
                Vector3 np3;
                if (Project(np, out np3)) { s.vp = np; s.pos = np3; s.root.position = np3; if (clicker.PixelTransform != null) s.local = clicker.PixelTransform.InverseTransformPoint(np3); }
                else s.vel = -s.vel;   // slid off the cube: turn round
            }
            s.sizeK = s.kind == 2 ? Mathf.Lerp(pulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(t * 6.283f / Mathf.Max(0.2f, pulseSeconds) + s.phase)) : 1f;
            if (s.kind == 3)
            {
                float cycle = flickerOn + flickerOff;
                s.shown = (t + s.phase) % cycle < flickerOn;
            }

            float pop = Mathf.Clamp01((t - s.born) / 0.18f);
            float pulse = 1f + Mathf.Sin(t * 9f) * 0.08f;
            float vis = s.shown ? 1f : 0.0f;
            s.root.localScale = Vector3.one * (Mathf.Sin(pop * Mathf.PI * 0.5f) * pulse * s.sizeK * vis);
            if (s.ring != null)
            {
                float left = Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds));
                s.ring.localScale = Vector3.one * Mathf.Lerp(1f, 1.9f, left) * (s.root.childCount > 0 ? s.root.GetChild(0).localScale.x : 1f);
            }
            Color c = Color.Lerp(Color.red, spotColor, Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds) + 0.2f));
            if (s.kind == 1) c = Color.Lerp(c, new Color(0.4f, 0.8f, 1f, 1f), 0.6f);       // drifting: blue-ish
            else if (s.kind == 2) c = Color.Lerp(c, new Color(0.7f, 1f, 0.4f, 1f), 0.6f);  // pulsing: green-ish
            else if (s.kind == 3) c = Color.Lerp(c, new Color(1f, 0.9f, 0.3f, 1f), 0.6f);  // flickering: yellow
            if (s.mat != null) s.mat.color = c;
            if (s.ringMat != null) s.ringMat.color = c;
        }
    }

    private void HandleClick()
    {
        if (!PixelInput.LeftPressed() || zoomCam == null) return;
        Vector2 p = PixelInput.PointerPosition();
        Spot nearest = null;
        float bestD = float.MaxValue;
        foreach (Spot s in spots)
        {
            if (s.hit || !s.shown) continue;
            Vector3 sp = zoomCam.WorldToScreenPoint(s.pos);
            if (sp.z <= 0f) continue;
            float d = Vector2.Distance(p, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; nearest = s; }
        }
        if (nearest == null) return;
        // projected radius of the spot (world size -> pixels), never smaller than the forgiveness
        Vector3 a = zoomCam.WorldToScreenPoint(nearest.pos);
        Vector3 b = zoomCam.WorldToScreenPoint(nearest.pos + zoomCam.transform.right * (clicker.PixelBaseSize * spotSize * 0.5f));
        float radius = Mathf.Max(clickPixels * Mathf.Lerp(0.5f, 1f, nearest.sizeK), Vector2.Distance(a, b) * 1.25f * nearest.sizeK);
        if (bestD > radius) return;
        nearest.hit = true;
        nearest.hitT = Time.unscaledTime;
        PixelAudio.Play("weakspot_hit");
        PixelBasicFx.Play(clicker.Tiers[Mathf.Max(0, clicker.CurrentClickTierIndex)].type, clicker.Tiers[Mathf.Max(0, clicker.CurrentClickTierIndex)].color, nearest.pos, clicker.PixelBaseSize, false, 0.5f, 1f);
    }

    private bool AllHit()
    {
        foreach (Spot s in spots) if (!s.hit) return false;
        return true;
    }

    private int HitCount()
    {
        int n = 0;
        foreach (Spot s in spots) if (s.hit) n++;
        return n;
    }

    private void Resolve()
    {
        phase = Phase.Result;
        phaseT = 0f;
        ApplySlow(false);   // normal speed again for the result and the zoom out
        slowed = false;
        string line;
        if (won && fishing)
        {
            float frac = Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds));
            GrantSkillXp(Mathf.Max(0.01f, Mathf.Round(baseXp + speedXp * frac)));
            PixelStats.Count("weakspot.wins");
            PixelAudio.Play("weakspot_win");
            line = "Caught it!";
        }
        else if (!won && fishing)
        {
            PixelAudio.Play("weakspot_lose");
            line = "It got away...";
        }
        else if (won)
        {
            float frac = Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds));
            float xp = Mathf.Round(baseXp + speedXp * frac);
            GrantSkillXp(Mathf.Max(0.01f, xp));
            PixelStats.Count("weakspot.wins");
            PixelAudio.Play("weakspot_win");
            PixelSkills sk = PixelSkills.Instance;
            PixelSkills.SkillPack pk = sk != null ? sk.Find(sk.TrainingId) : null;
            line = string.Format(winFormat, xp, pk != null ? pk.displayName : "skill");
            PixelHints.Announce(line);
        }
        else
        {
            PixelAudio.Play("weakspot_lose");
            line = loseText;
        }
        if (statusLabel != null) statusLabel.text = line;
    }

    private void BeginZoomOut()
    {
        foreach (Spot s in spots) if (s.root != null) Destroy(s.root.gameObject);
        spots.Clear();
        phase = Phase.ZoomOut;
        phaseT = 0f;
        if (statusLabel != null) statusLabel.text = "";
    }

    private void Finish()
    {
        bool caught = won && (phase == Phase.Result || phase == Phase.ZoomOut);
        System.Action<bool> callback = onFinished;
        onFinished = null;
        fishing = false;
        FinishRound();
        callback?.Invoke(caught);
    }

    private void FinishRound()
    {
        if (surface != null) { Destroy(surface.gameObject); surface = null; }
        foreach (Spot s in spots) if (s.root != null) Destroy(s.root.gameObject);
        spots.Clear();
        if (slowed || !Mathf.Approximately(Time.timeScale, 1f) && !PixelPauseMenu.IsPaused && !PixelTimeStop.IsStopped && !PixelTimeStop.IsSlowed)
            ApplySlow(false);
        slowed = false;
        if (mainCam != null && phase != Phase.Idle) mainCam.cullingMask = savedMask;
        if (zoomCam != null) Destroy(zoomCam.gameObject);
        zoomCam = null;
        if (uiRoot != null) { Destroy(uiRoot); uiRoot = null; statusLabel = null; }
        PixelClicker.ExternalClickBlock = false;
        PixelClicker.HoldSpin = false;
        PixelMinigame.AutoClickerPaused = false;
        phase = Phase.Idle;
        spawnTimer = Random.Range(Mathf.Min(minInterval, maxInterval), Mathf.Max(minInterval, maxInterval));
    }

    // ------------------------------------------------------------------
    // The status line
    // ------------------------------------------------------------------

    private void BuildUi()
    {
        if (uiRoot != null) Destroy(uiRoot);
        uiRoot = PixelUIKit.CreateCanvas("Weak Spot UI", 125, new Vector2(1920f, 1080f), false);
        uiRoot.transform.SetParent(transform, false);
        Track(uiRoot);
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        statusLabel = PixelUIKit.CreateText(clicker.UIFont, uiRoot.transform, "Status", "", statusFontSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        statusLabel.raycastTarget = false;
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(1100f, statusFontSize * 1.5f);
        sr.anchoredPosition = new Vector2(0f, -(bar + 12f));
    }

    private void UpdateStatus()
    {
        if (statusLabel == null || phase != Phase.Hunt) return;
        PixelUIKit.SetText(statusLabel, string.Format(statusFormat, HitCount(), spots.Count, Mathf.CeilToInt(Mathf.Max(0f, huntLeft))));
    }
}
