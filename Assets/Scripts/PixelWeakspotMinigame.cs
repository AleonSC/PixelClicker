using TMPro;
using UnityEngine;

/// <summary>
/// Weak Spots (id "weakspot"), the Mining skill's own minigame. It needs no shop pack: it runs by itself while Mining is the pixel pack in play.
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
    [Tooltip("Run even when Mining is not the pixel pack in play (testing).")]
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
    [Tooltip("Mining XP for clearing every weak spot.")]
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

    [Header("Look")]
    [Min(10)]
    [Tooltip("Size of the status text at the top of the screen.")]
    [SerializeField] private int statusFontSize = 36;

    [Tooltip("Status line while hunting. {0} = spots hit, {1} = spots in total, {2} = seconds left.")]
    [SerializeField] private string statusFormat = "WEAK SPOTS   {0} / {1}   {2}s";

    [Tooltip("Shown when every spot was hit. {0} = the bonus XP.")]
    [SerializeField] private string winFormat = "Perfect!  +{0} Mining XP";

    [Tooltip("Shown when time ran out.")]
    [SerializeField] private string loseText = "Too slow...";

    [Min(0.2f)]
    [Tooltip("How long the result stays before the camera zooms back (real seconds).")]
    [SerializeField] private float resultSeconds = 1f;

    private class Spot
    {
        public Transform root, ring;
        public Material mat, ringMat;
        public Vector3 pos;
        public float born;
        public bool hit;
        public float hitT = -1f;
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
    public override bool Running => startRunning || (PixelSkills.Instance != null && PixelSkills.Instance.ActiveId == PixelSkills.MiningId);
    public override bool Busy => phase != Phase.Idle;
    public override bool Takeover => true;   // nothing else starts meanwhile (the cube is not 'taken over': TakeoverActive stays off)

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
        if (PixelPauseMenu.IsPaused) return;   // the pause menu owns timeScale; our clocks wait
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

    private bool CanStartNow()
    {
        return PixelMinigameLimits.AllowAnother(this) && !PixelPauseMenu.IsPaused && !TakeoverActive && !clicker.CubeHidden
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
    }

    /// <summary>Finds a spot on the face of the cube that the camera sees, away from the others.</summary>
    private Spot MakeSpot()
    {
        Transform cubeT = clicker.PixelTransform;
        float size = clicker.PixelBaseSize;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector3 vp = new Vector3(Random.Range(0.3f, 0.7f), Random.Range(0.3f, 0.7f), 0f);
            Ray ray = zoomCam.ViewportPointToRay(vp);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            RaycastHit pick = default;
            foreach (RaycastHit h in hits)
            {
                if (h.transform != cubeT && !h.transform.IsChildOf(cubeT) && (clicker.HitboxTransform == null || h.transform != clicker.HitboxTransform)) continue;
                if (h.distance < best) { best = h.distance; pick = h; }
            }
            if (best == float.MaxValue) continue;
            Vector3 pos = pick.point + pick.normal * (size * 0.04f);
            bool apart = true;
            foreach (Spot o in spots)
                if (Vector2.Distance(zoomCam.WorldToViewportPoint(pos), zoomCam.WorldToViewportPoint(o.pos)) < spotGap) { apart = false; break; }
            if (!apart) continue;
            return BuildSpot(pos, size * spotSize);
        }
        return null;
    }

    private Spot BuildSpot(Vector3 pos, float size)
    {
        EnsureTextures();
        Spot s = new Spot { pos = pos, born = Time.unscaledTime };
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
            float pop = Mathf.Clamp01((t - s.born) / 0.18f);
            float pulse = 1f + Mathf.Sin(t * 9f) * 0.08f;
            s.root.localScale = Vector3.one * (Mathf.Sin(pop * Mathf.PI * 0.5f) * pulse);
            if (s.ring != null)
            {
                float left = Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds));
                s.ring.localScale = Vector3.one * Mathf.Lerp(1f, 1.9f, left) * (s.root.childCount > 0 ? s.root.GetChild(0).localScale.x : 1f);
            }
            Color c = Color.Lerp(Color.red, spotColor, Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds) + 0.2f));
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
            if (s.hit) continue;
            Vector3 sp = zoomCam.WorldToScreenPoint(s.pos);
            if (sp.z <= 0f) continue;
            float d = Vector2.Distance(p, new Vector2(sp.x, sp.y));
            if (d < bestD) { bestD = d; nearest = s; }
        }
        if (nearest == null) return;
        // projected radius of the spot (world size -> pixels), never smaller than the forgiveness
        Vector3 a = zoomCam.WorldToScreenPoint(nearest.pos);
        Vector3 b = zoomCam.WorldToScreenPoint(nearest.pos + zoomCam.transform.right * (clicker.PixelBaseSize * spotSize * 0.5f));
        float radius = Mathf.Max(clickPixels, Vector2.Distance(a, b) * 1.25f);
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
        if (won)
        {
            float frac = Mathf.Clamp01(huntLeft / Mathf.Max(0.1f, huntSeconds));
            float xp = Mathf.Round(baseXp + speedXp * frac);
            GrantSkillXp(Mathf.Max(0.01f, xp));
            PixelStats.Count("weakspot.wins");
            PixelAudio.Play("weakspot_win");
            line = string.Format(winFormat, xp);
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
