// ---- System notes ----
// PixelGoldenSnitch.cs
//   The GOLDEN SNITCH (PixelType.GoldenSnitch 101): a very rare drop (a rareDrop tier like the Dragon Cubes, PixelClicker.EnsureGoldenSnitch, spawnWeight
//   goldenSnitchSpawnWeight 0.00003) that is never clicked off the cube (CollectInternal ignores it, Block Hold can't grab it). Component added by PixelClicker.Awake.
//   0. ON THE CUBE: it lands (PixelClicker.PixelMaterialized) and already LOOKS like the snitch: the detailed golden cube model - engraved dark edges, a middle
//      band, bright corner studs and face gems, gold wing knobs, a soft cube glow shell, a gold point light, sparkles - sits over the clicker cube (onCubeCover
//      1.03 x its size) and follows its turn / pops every frame (LateUpdate -> FollowCube), wings folded away. It waits for a MANUAL click
//      (PixelClicker.CollectInternal -> CubeClicked; the auto clicker leaves it alone). Nobody clicks it for onCubeSeconds (120) and it slips away (escaped).
//   1. SPROUT: the click makes that same cube sprout two silver feathered wings right where it sits (sproutSeconds 1.1, fluttering faster and faster)
//      while it eases off the cube towards the camera and turns to face it. The cube rolls a fresh block behind it (PixelClicker.ReplaceCurrentPixel).
//   2. CHASE: it darts round the screen fast (viewport steering inside the black bars: quick darts to far points, little hops, feints, a flutter), leaving a gold
//      trail, its wings buzzing (snitch_buzz). Keep the mouse ON it (hoverScale x its size on screen) and a bar above it charges (chargeSeconds 3.2 of hovering);
//      off it the bar does NOT charge (drainPerSecond 0 = it just waits). It flies faster and more wildly the fuller the bar. A click while it is still charging
//      costs earlyPenalty (25%) of the bar ('Too early!').
//   3. CAPTURE: with the bar full a white ring closes in on it towards a gold target ring (ringSeconds 1.1); CLICK while the mouse is on it AND the rings meet
//      (captureTolerance 0.2) to catch it. Too early / too late = a miss: the bar drops to missKeep (35%), it bolts away; maxMisses (3) misses or its lifetime
//      (lifetimeSeconds 30) and it escapes off the screen.
//   CAUGHT: it freezes, folds its wings and bursts into gold sparkles: it counts as collected (the Golden Snitch tier +1) and pays rewardClicks (400) x what a
//   click on your best unlocked pixel pays now, plus an ultraChance (50%) Ultra pixel; minigame-style skill XP (skillXp 40 to the skill in training).
//   Status lines (key 'snitch'), event log lines, code-made sounds (PixelSnitchSynth via PixelAudio: snitch_appear / buzz / ready / catch / miss), Stats
//   (snitch.seen / caught / escaped, Best snitch.fastest). In first person the crosshair is the pointer. Pauses with the pause menu; not saved (one in flight is
//   simply lost on quit).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelGoldenSnitch : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("Seconds the wings take to unfold on the cube before it flies.")]
    [SerializeField] private float sproutSeconds = 1.1f;
    [Tooltip("Its size on screen while flying, in pixels at 1080p.")]
    [SerializeField] private float screenSizePx = 110f;
    [Tooltip("Top flying speed in screen widths per second (at an empty bar).")]
    [SerializeField] private float speed = 0.6f;
    [Tooltip("Speed multiplier with a full bar (it gets wilder the closer you are).")]
    [SerializeField] private float speedAtFull = 1.35f;
    [Tooltip("Seconds between darts to a new point (min, max), shorter as the bar fills.")]
    [SerializeField] private Vector2 dartSeconds = new Vector2(0.6f, 1.4f);
    [Tooltip("Speed multiplier while the capture ring closes (a little slower, so the timing is fair).")]
    [SerializeField] private float captureSlow = 0.6f;
    [Tooltip("How much bigger than the clicker cube the snitch model is while it sits on the cube (a touch over 1 hides the plain gold block inside it).")]
    [SerializeField] private float onCubeCover = 1.03f;
    [Tooltip("Seconds it waits on the cube for your click before it slips away unseen (0 = it waits forever).")]
    [SerializeField] private float onCubeSeconds = 120f;
    [Tooltip("Seconds before it escapes off the screen if it is not caught.")]
    [SerializeField] private float lifetimeSeconds = 30f;

    [Header("Catching")]
    [Tooltip("How close to it the mouse counts as ON it, as a multiple of its size on screen.")]
    [SerializeField] private float hoverScale = 0.95f;
    [Tooltip("Seconds of keeping the mouse on it to fill the bar.")]
    [SerializeField] private float chargeSeconds = 3.2f;
    [Tooltip("How much of the bar drains per second while the mouse is off it (0 = it just stops charging).")]
    [SerializeField] private float drainPerSecond = 0f;
    [Tooltip("Share of the bar lost when you click while it is still charging.")]
    [SerializeField] private float earlyPenalty = 0.25f;
    [Tooltip("Seconds the capture ring takes to close.")]
    [SerializeField] private float ringSeconds = 1.1f;
    [Tooltip("How big the closing ring starts, as a multiple of the gold target ring.")]
    [SerializeField] private float ringStartScale = 3.2f;
    [Tooltip("How close (share of the target ring's size) the closing ring must be to the target for a click to catch it.")]
    [SerializeField] private float captureTolerance = 0.2f;
    [Tooltip("Share of the bar kept after a miss.")]
    [SerializeField] private float missKeep = 0.35f;
    [Tooltip("Misses before it escapes.")]
    [SerializeField] private int maxMisses = 3;

    [Header("Reward")]
    [Tooltip("The reward = this many clicks' worth of your best unlocked pixel (with its Value / Ultra multipliers).")]
    [SerializeField] private double rewardClicks = 400;
    [Range(0f, 1f)]
    [Tooltip("Chance of an Ultra pixel on top.")]
    [SerializeField] private float ultraChance = 0.5f;
    [Tooltip("Bonus XP for the skill in training when it is caught.")]
    [SerializeField] private float skillXp = 40f;

    [Header("Look")]
    [Tooltip("The snitch's gold.")]
    [SerializeField] private Color gold = new Color(1f, 0.78f, 0.22f, 1f);
    [Tooltip("The wings' colour (see-through silver).")]
    [SerializeField] private Color wingColor = new Color(0.96f, 0.95f, 0.88f, 0.72f);

    [Header("Texts")]
    [Tooltip("Event log line when a Golden Snitch lands on the cube.")] [SerializeField] private string landedText = "A Golden Snitch landed on the cube!";
    [Tooltip("Status line and event log line when you catch it (the payout follows).")] [SerializeField] private string caughtText = "You caught the Golden Snitch!";

    private enum State { Idle, OnCube, Sprouting, Chase, Capture, Escaping, Caught }
    private State state = State.Idle;

    /// <summary>A Golden Snitch is on the cube or flying.</summary>
    public static bool Active { get; private set; }

    private static PixelGoldenSnitch instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = false; instance = null; }

    /// <summary>
    /// Called by PixelClicker.CollectInternal when the Golden Snitch block on the cube is clicked: a manual click makes it sprout
    /// wings and fly (the auto clicker leaves it alone).
    /// </summary>
    public static void CubeClicked(bool automatic)
    {
        if (automatic || instance == null || instance.state != State.OnCube) return;
        instance.Sprout();
    }

    private PixelClicker clicker;
    private int snitchTier = -1;
    private bool replacePending;

    // model
    private GameObject root;
    private Transform body, wingL, wingR, shell;
    private readonly List<Transform> feathers = new List<Transform>();
    private Light glow;
    private TrailRenderer trail;
    private ParticleSystem sparkles;
    private Material lineMat;

    // flight
    private Vector2 vp, vel, target;
    private float depth, cubeDepth, stateT, life, charge, dartTimer, burstT, ringT, buzzTimer, flapPhase, spin;
    private Vector3 sproutPos, sproutFrom;
    private Quaternion sproutFromRot;
    private float sproutFromScale;
    private int misses;
    private bool hovering;

    // UI
    private GameObject canvasObject;
    private RectTransform canvasRect, barRoot, barFill, ringTargetRect, ringShrinkRect;
    private Image barFillImage;
    private PixelArcGraphic ringTarget, ringShrink;
    private TMP_Text hintLabel;

    private void Awake() { clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); instance = this; }
    private void OnEnable() { if (clicker != null) clicker.PixelMaterialized += OnMaterialized; }
    private void OnDisable() { if (clicker != null) clicker.PixelMaterialized -= OnMaterialized; Cleanup(); }

    private Camera Cam => PixelClicker.ViewCameraOf(clicker);

    private void OnMaterialized(int tierIndex)
    {
        if (clicker == null || !clicker.IsValidTierIndex(tierIndex) || clicker.Tiers[tierIndex].type != PixelClicker.PixelType.GoldenSnitch) return;
        if (state != State.Idle) { replacePending = true; return; }   // (one is already out: a second one on the cube would be stuck there - it is rolled away)
        Begin(tierIndex);
    }

    /// <summary>Dev / testing: puts a Golden Snitch on the cube right now (it sprouts wings at once).</summary>
    [ContextMenu("Spawn Golden Snitch Now")]
    public void SpawnNow()
    {
        if (clicker != null && state == State.Idle) clicker.DevSwapCurrentPixel(PixelClicker.PixelType.GoldenSnitch);
    }

    // ------------------------------------------------------------------
    // 1. Sprout
    // ------------------------------------------------------------------

    /// <summary>It lands on the cube as a gold block and waits for a click.</summary>
    private void Begin(int tierIndex)
    {
        snitchTier = tierIndex;
        Active = true;
        state = State.OnCube;
        stateT = 0f;
        PixelStats.Count("snitch.seen");
        PixelAudio.Play("snitch_appear");
        PixelStatusLines.Set("snitch", "A GOLDEN SNITCH is on the cube! Click it to make it fly.", gold, 6f);
        PixelHints.Announce(landedText);
        // It already LOOKS like the snitch on the cube (wings folded away): the model sits over the clicker cube and follows it (LateUpdate -> FollowCube).
        BuildModel();
        spin = 0f;
        FollowCube();
    }

    /// <summary>On the cube: the snitch model covers the clicker cube exactly (its position, turn and size, pops and squashes included).</summary>
    private void FollowCube()
    {
        Renderer r = clicker != null ? clicker.MainRenderer : null;
        if (root == null || r == null) return;
        Transform t = r.transform;
        Vector3 ls = t.lossyScale;
        root.transform.SetPositionAndRotation(r.bounds.center, t.rotation);
        root.transform.localScale = Vector3.one * (Mathf.Max(ls.x, ls.y, ls.z) / 0.8f * onCubeCover);   // (the model's cube is 0.8 across: a touch bigger hides the plain block inside it)
        if (body != null) body.localRotation = Quaternion.identity;
        if (shell != null) shell.localRotation = Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (state == State.OnCube) FollowCube();
    }

    /// <summary>The click: wings unfold out of the gold cube and it takes off.</summary>
    private void Sprout()
    {
        Camera cam = Cam;
        if (cam == null) return;
        state = State.Sprouting;
        stateT = 0f; life = 0f; charge = 0f; misses = 0; burstT = 0f; ringT = 0f;
        sproutPos = clicker.LiveWorldPosition - cam.transform.forward * clicker.PixelBaseSize * 0.9f;   // (it rises to just in front of the cube: the fresh block grows in behind it)
        cubeDepth = Vector3.Dot(sproutPos - cam.transform.position, cam.transform.forward);
        depth = cubeDepth;
        if (root == null) { BuildModel(); FollowCube(); }   // (normally it is already there, sitting on the cube)
        BuildUI();
        sproutFrom = root.transform.position;
        sproutFromRot = root.transform.rotation;
        sproutFromScale = root.transform.localScale.x;
        replacePending = true;   // (next frame: the cube rolls a fresh block behind it - not inside the click that set it off)
        PixelAudio.Play("snitch_ready");
        PixelStatusLines.Set("snitch", "It flies! Keep your mouse on it to charge, then click when the rings meet.", gold, 6f);
    }

    // ------------------------------------------------------------------
    // Every frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (state == State.Idle) return;
        if (state == State.OnCube) { UpdateOnCube(); return; }
        if (replacePending) { replacePending = false; clicker.ReplaceCurrentPixel(); }
        Camera cam = Cam;
        if (cam == null || root == null) { Cleanup(); return; }
        bool paused = PixelCalm.InMenu;
        if (canvasObject != null) canvasObject.SetActive(!paused);
        if (paused) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        stateT += dt;

        switch (state)
        {
            case State.Sprouting: UpdateSprout(cam, dt); break;
            case State.Chase:
            case State.Capture: UpdateChase(cam, dt); break;
            case State.Escaping: UpdateEscape(cam, dt); break;
            case State.Caught: UpdateCaught(dt); break;
        }
        AnimateModel(dt);
        UpdateUI(cam);
    }

    /// <summary>Waiting on the cube: if nobody clicks it for onCubeSeconds it slips away and the cube rolls a new block.</summary>
    private void UpdateOnCube()
    {
        if (clicker == null) { Cleanup(); return; }
        if (!clicker.IsValidTierIndex(clicker.CurrentClickTierIndex) || clicker.Tiers[clicker.CurrentClickTierIndex].type != PixelClicker.PixelType.GoldenSnitch) { Cleanup(); return; }   // (something else swapped the block)
        if (PixelCalm.InMenu) return;
        stateT += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (onCubeSeconds <= 0f || stateT < onCubeSeconds) return;
        PixelStats.Count("snitch.escaped");
        PixelStatusLines.Set("snitch", "The Golden Snitch slipped away before you clicked it.", new Color(1f, 0.55f, 0.35f), 3f);
        Cleanup();
        clicker.ReplaceCurrentPixel();
    }

    private void UpdateSprout(Camera cam, float dt)
    {
        float k = Mathf.Clamp01(stateT / Mathf.Max(0.2f, sproutSeconds));
        // The cube itself sprouts: the wings unfold where it sat while it eases off the cube towards the camera and turns to face it.
        float m = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.6f));
        root.transform.position = Vector3.Lerp(sproutFrom, sproutPos, m) + cam.transform.up * (0.15f * clicker.PixelBaseSize * Mathf.Sin(k * Mathf.PI));
        root.transform.localScale = Vector3.one * Mathf.Lerp(sproutFromScale, clicker.PixelBaseSize * 0.75f, m);
        FaceCamera(cam, 0f);
        root.transform.rotation = Quaternion.Slerp(sproutFromRot, root.transform.rotation, m);
        // The wings grow out of the ball with a little overshoot.
        float g = k < 0.75f ? EaseBack(k / 0.75f) : 1f;
        foreach (Transform f in feathers) if (f != null) f.localScale = new Vector3(Mathf.Max(0.001f, g), 1f, 1f);
        if (k < 1f) return;
        // Lift off: from the cube's spot on screen, darting away.
        Vector3 v = cam.WorldToViewportPoint(sproutPos);
        vp = new Vector2(v.x, v.y);
        vel = Vector2.zero;
        PickTarget(true);
        state = State.Chase;
        stateT = 0f;
        PixelAudio.Play("snitch_buzz", 1.2f);
    }

    private void UpdateChase(Camera cam, float dt)
    {
        life += dt;
        burstT -= dt;
        depth = Mathf.Lerp(depth, cubeDepth * 0.7f, 1f - Mathf.Exp(-4f * dt));   // (it rises towards the camera after lift-off)
        float spd = speed * Mathf.Lerp(1f, speedAtFull, charge) * (state == State.Capture ? captureSlow : 1f) * (burstT > 0f ? 1.8f : 1f);
        Steer(dt, spd);
        PlaceInWorld(cam);

        // Is the mouse on it?
        Vector3 sp = cam.WorldToScreenPoint(root.transform.position);
        float px = ScreenSizePx * hoverScale;
        hovering = sp.z > 0f && Vector2.Distance(PixelInput.PointerPosition(), new Vector2(sp.x, sp.y)) <= px;
        if (hovering || Vector2.Distance(PixelInput.PointerPosition(), new Vector2(sp.x, sp.y)) <= px * 1.6f) PixelPointerClaim.Claim();   // (clicks near it never hit the cube)
        bool click = PixelInput.LeftPressed();

        buzzTimer -= dt;
        if (buzzTimer <= 0f) { buzzTimer = 0.17f; PixelAudio.Play("snitch_buzz", 1f + 0.35f * charge, 0.35f); }

        if (state == State.Chase)
        {
            if (hovering) charge = Mathf.Min(1f, charge + dt / Mathf.Max(0.3f, chargeSeconds));
            else charge = Mathf.Max(0f, charge - drainPerSecond * dt);
            if (click && hovering && charge < 1f)
            {
                charge = Mathf.Max(0f, charge - earlyPenalty);
                PixelStatusLines.Set("snitch", "Too early! Charge the bar first.", new Color(1f, 0.55f, 0.35f), 1.6f);
                PixelAudio.Play("snitch_miss", 1.3f, 0.6f);
                burstT = 0.35f;
            }
            if (charge >= 1f)
            {
                state = State.Capture;
                ringT = 0f;
                PixelAudio.Play("snitch_ready");
                PixelStatusLines.Set("snitch", "NOW! Click when the white ring meets the gold one!", gold, 2f);
            }
        }
        else   // the capture ring closes
        {
            ringT += dt;
            float scale = RingScale();
            bool inWindow = Mathf.Abs(scale - 1f) <= captureTolerance;
            if (click && hovering && inWindow) { Catch(); return; }
            if (click) { Miss(scale > 1f ? "Too early!" : "Too late!"); return; }
            if (ringT >= ringSeconds) { Miss("Too late!"); return; }
        }
        if (life >= lifetimeSeconds) Escape("The Golden Snitch got bored and flew away.");
    }

    private float RingScale() => Mathf.Lerp(Mathf.Max(1.5f, ringStartScale), 0.45f, Mathf.Clamp01(ringT / Mathf.Max(0.3f, ringSeconds)));

    private void Miss(string why)
    {
        misses++;
        charge = Mathf.Clamp01(missKeep);
        state = State.Chase;
        burstT = 0.8f;
        PickTarget(true);
        PixelAudio.Play("snitch_miss");
        if (misses >= Mathf.Max(1, maxMisses)) { Escape("Missed! The Golden Snitch escaped."); return; }
        PixelStatusLines.Set("snitch", why + " It got away from you - " + (maxMisses - misses) + " more tr" + (maxMisses - misses == 1 ? "y" : "ies") + ".", new Color(1f, 0.55f, 0.35f), 2.2f);
    }

    private void Escape(string message)
    {
        state = State.Escaping;
        stateT = 0f;
        // Off the nearest side, fast.
        Vector2 dir = vel.sqrMagnitude > 0.0001f ? vel.normalized : (vp - new Vector2(0.5f, 0.5f)).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
        target = vp + dir * 2f;
        PixelStatusLines.Set("snitch", message, new Color(1f, 0.6f, 0.4f), 3f);
        PixelHints.Announce(message);
        PixelStats.Count("snitch.escaped");
    }

    private void UpdateEscape(Camera cam, float dt)
    {
        Vector2 to = target - vp;
        vel = Vector2.MoveTowards(vel, to.normalized * speed * 2.2f, speed * 12f * dt);
        vp += vel * dt;
        PlaceInWorld(cam);
        hovering = false;
        if (vp.x < -0.2f || vp.x > 1.2f || vp.y < -0.2f || vp.y > 1.2f || stateT > 3f) Cleanup();
    }

    private void Catch()
    {
        state = State.Caught;
        stateT = 0f;
        hovering = false;
        // The reward: rewardClicks clicks' worth of your best unlocked pixel, plus the snitch itself (collected) and maybe an Ultra pixel.
        int best = -1; double bestPay = 0d;
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = clicker.Tiers[i];
            if (!t.unlocked || t.rareDrop || PixelClicker.IsHazard(t.type)) continue;
            double pay = System.Math.Max(0d, t.amountPerClick) * clicker.PayoutMultiplier(i);
            if (pay > bestPay) { bestPay = pay; best = i; }
        }
        string paid = "";
        if (best >= 0 && bestPay > 0d)
        {
            double amount = bestPay * System.Math.Max(1d, rewardClicks);
            clicker.AddCurrency(best, amount);
            paid = " +" + PixelClicker.FormatNumberShort(amount) + " " + clicker.Tiers[best].displayName;
        }
        if (snitchTier >= 0) clicker.AddCurrency(snitchTier, 1d);   // (the snitch counts as collected: Inventory, Collection / Stats)
        if (clicker.RollUltra(ultraChance)) { PixelUltraFx.Play(clicker, root.transform.position); paid += " + an Ultra pixel"; }
        PixelSkills s = PixelSkills.Instance;
        PixelSkills.SkillPack training = s != null ? s.Find(s.TrainingId) : null;
        if (training != null && skillXp > 0f) s.AddXp(training, skillXp);
        PixelStats.Count("snitch.caught");
        float took = life;
        double fastest = PixelStats.Total("snitch.fastest");
        if (fastest <= 0d || took < fastest) { PixelStats.Clear("snitch.fastest"); PixelStats.Count("snitch.fastest", took); }
        PixelAudio.Play("snitch_catch");
        PixelStatusLines.Set("snitch", caughtText + paid, gold, 5f);
        PixelHints.Announce(caughtText + paid);
        if (sparkles != null) sparkles.Emit(70);
        if (trail != null) trail.emitting = false;
    }

    private void UpdateCaught(float dt)
    {
        // It freezes, swells, folds its wings and shrinks into the sparkles.
        float k = Mathf.Clamp01(stateT / 0.9f);
        float s = k < 0.25f ? 1f + k * 1.2f : Mathf.Lerp(1.3f, 0f, (k - 0.25f) / 0.75f);
        root.transform.localScale = Vector3.one * WorldSize() * s;
        foreach (Transform f in feathers) if (f != null) f.localScale = new Vector3(Mathf.Max(0.001f, 1f - k), 1f, 1f);
        if (k >= 1f) Cleanup();
    }

    // ------------------------------------------------------------------
    // Steering in viewport space (inside the black bars)
    // ------------------------------------------------------------------

    private const float MinX = 0.07f, MaxX = 0.93f;
    private float MinY => 0.12f + BarShare;
    private float MaxY => 0.88f - BarShare;
    private float BarShare => PixelHud.Instance != null ? PixelHud.Instance.BarHeight / 1080f : 0f;

    private void PickTarget(bool far)
    {
        Vector2 best = vp;
        float bestD = -1f;
        int tries = far || Random.value < 0.65f ? 4 : 1;
        for (int i = 0; i < tries; i++)
        {
            Vector2 c = Random.value < 0.22f && !far
                ? vp + new Vector2(Random.Range(-0.09f, 0.09f), Random.Range(-0.09f, 0.09f))   // a little hop
                : new Vector2(Random.Range(MinX, MaxX), Random.Range(MinY, MaxY));
            float d = (c - vp).sqrMagnitude;
            if (d > bestD) { bestD = d; best = c; }
        }
        target = new Vector2(Mathf.Clamp(best.x, MinX, MaxX), Mathf.Clamp(best.y, MinY, MaxY));
        dartTimer = Random.Range(Mathf.Min(dartSeconds.x, dartSeconds.y), Mathf.Max(dartSeconds.x, dartSeconds.y)) * (1f - 0.45f * charge);
    }

    private void Steer(float dt, float spd)
    {
        dartTimer -= dt;
        if (dartTimer <= 0f || (target - vp).sqrMagnitude < 0.0004f)
        {
            if (Random.value < 0.12f) { vel *= 0.15f; dartTimer = 0.18f; }   // a feint: it stops dead for a moment
            else PickTarget(false);
        }
        Vector2 to = target - vp;
        Vector2 want = to.normalized * spd * Mathf.Clamp01(to.magnitude / 0.07f);
        vel = Vector2.MoveTowards(vel, want, spd * 8f * dt);
        Vector2 perp = new Vector2(-vel.y, vel.x).normalized;
        vp += vel * dt + perp * Mathf.Sin(Time.unscaledTime * 13f) * 0.0035f;   // a flutter across its path
        if (vp.x < MinX || vp.x > MaxX) { vel.x = -vel.x * 0.6f; vp.x = Mathf.Clamp(vp.x, MinX, MaxX); }
        if (vp.y < MinY || vp.y > MaxY) { vel.y = -vel.y * 0.6f; vp.y = Mathf.Clamp(vp.y, MinY, MaxY); }
    }

    private float ScreenSizePx => Mathf.Max(8f, screenSizePx) * Screen.height / 1080f;

    private float WorldSize()
    {
        Camera cam = Cam;
        if (cam == null) return 0.5f;
        return PixelUIKit.ViewHeightAt(cam, Mathf.Max(0.1f, depth)) * (ScreenSizePx / Mathf.Max(1f, Screen.height));
    }

    private void PlaceInWorld(Camera cam)
    {
        root.transform.position = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, Mathf.Max(0.1f, depth)));
        if (state != State.Caught) root.transform.localScale = Vector3.Lerp(root.transform.localScale, Vector3.one * WorldSize(), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));   // (eases from its size on the cube to its flying size)
        FaceCamera(cam, Mathf.Clamp(-vel.x * 28f, -35f, 35f));
    }

    private void FaceCamera(Camera cam, float roll)
    {
        root.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, -roll);
    }

    // ------------------------------------------------------------------
    // The model
    // ------------------------------------------------------------------

    private void BuildModel()
    {
        root = new GameObject("Golden Snitch");
        PixelFactoryKit k = new PixelFactoryKit(clicker, false);
        Color deep = new Color(gold.r * 0.62f, gold.g * 0.5f, gold.b * 0.25f, 1f), bright = Color.Lerp(gold, Color.white, 0.35f);
        body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        // A polished gold cube: darker engraved edges, a band round its middle, bright studs on the corners and a gem on each face.
        const float h = 0.4f;   // (half its size: the cube is 0.8 across, the wings hinge on its sides)
        k.Prim(PrimitiveType.Cube, "Cube", body, Vector3.zero, Vector3.one * (h * 2f), gold, default, 0.92f, 0.94f, false, gold * 0.35f);
        for (int a = 0; a < 3; a++)
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -h : h, sy = (i & 2) == 0 ? -h : h;
                Vector3 pos = a == 0 ? new Vector3(0f, sx, sy) : a == 1 ? new Vector3(sx, 0f, sy) : new Vector3(sx, sy, 0f);
                Vector3 size = a == 0 ? new Vector3(h * 2.04f, 0.05f, 0.05f) : a == 1 ? new Vector3(0.05f, h * 2.04f, 0.05f) : new Vector3(0.05f, 0.05f, h * 2.04f);
                k.Box("Edge", body, pos, size, deep, default, false, 0.9f, 0.85f);
            }
        k.Box("Band", body, Vector3.zero, new Vector3(h * 2.03f, 0.06f, h * 2.03f), deep, default, false, 0.9f, 0.85f);
        for (int i = 0; i < 8; i++)
            k.Ball("Stud", body, new Vector3((i & 1) == 0 ? -h : h, (i & 2) == 0 ? -h : h, (i & 4) == 0 ? -h : h), 0.06f, bright, 0.95f, 0.95f);
        Vector3[] faces = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 f in faces) k.Ball("Gem", body, f * h, 0.07f, bright, 0.95f, 0.95f);

        // The glow shell (see-through, pulsing) and the light.
        GameObject sh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(sh.GetComponent<Collider>());
        sh.name = "Glow";
        sh.transform.SetParent(root.transform, false);
        sh.transform.localScale = Vector3.one * 1.15f;
        Renderer shr = sh.GetComponent<Renderer>();
        shr.sharedMaterial = clicker.CreateVisualMaterial(new Color(gold.r, gold.g, gold.b, 0.16f), true);
        shr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shell = sh.transform;
        glow = new GameObject("Light").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.type = LightType.Point;
        glow.color = gold;
        glow.intensity = 2.2f;

        // The wings: a gold knob on each side and six fanned see-through feathers with a gold spine.
        Material feather = clicker.CreateVisualMaterial(wingColor, true);
        feathers.Clear();
        wingR = BuildWing(k, 1f, feather, deep);
        wingL = BuildWing(k, -1f, feather, deep);

        // Trail and sparkles.
        trail = root.AddComponent<TrailRenderer>();
        lineMat = new Material(PixelShaders.SpriteDefault());
        trail.sharedMaterial = lineMat;
        trail.time = 0.28f;
        trail.minVertexDistance = 0.01f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
        Gradient tg = new Gradient();
        tg.SetKeys(new[] { new GradientColorKey(bright, 0f), new GradientColorKey(gold, 1f) }, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = tg;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.emitting = false;

        sparkles = root.AddComponent<ParticleSystem>();
        sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = sparkles.main;
        main.loop = true;
        main.startLifetime = 0.55f;
        main.startSpeed = 0.35f;
        main.startSize = 0.08f;
        main.startColor = new ParticleSystem.MinMaxGradient(gold, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 120;
        ParticleSystem.EmissionModule em = sparkles.emission;
        em.rateOverTime = 26f;
        ParticleSystem.ShapeModule shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f;
        ParticleSystemRenderer pr = root.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = lineMat;
        sparkles.Play();
    }

    private Transform BuildWing(PixelFactoryKit k, float side, Material feather, Color spine)
    {
        Transform hinge = new GameObject(side > 0f ? "Wing R" : "Wing L").transform;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = new Vector3(side * 0.44f, 0.08f, 0f);
        k.Ball("Knob", hinge, Vector3.zero, 0.085f, gold, 0.92f, 0.94f);
        float[] lengths = { 1.3f, 1.18f, 1.04f, 0.9f, 0.74f, 0.58f };
        for (int i = 0; i < lengths.Length; i++)
        {
            float a = Mathf.Lerp(6f, 68f, i / (float)(lengths.Length - 1));   // fanned from flat to steep
            float len = lengths[i];
            // Each feather is a pivot at the hinge (scaled along its length to unfold) holding a thin blade.
            Transform pivot = new GameObject("Feather Pivot").transform;
            pivot.SetParent(hinge, false);
            pivot.localRotation = Quaternion.Euler(0f, 0f, side > 0f ? a : 180f - a);
            GameObject blade = k.Box("Feather", pivot, new Vector3(len * 0.5f, 0f, 0f), new Vector3(len, 0.13f - i * 0.008f, 0.012f), wingColor);
            Renderer br = blade.GetComponent<Renderer>();
            if (br != null) { br.sharedMaterial = feather; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            if (i == 0) k.Box("Spine", pivot, new Vector3(len * 0.45f, 0.06f, 0f), new Vector3(len * 0.9f, 0.025f, 0.02f), spine, default, false, 0.9f, 0.85f);
            pivot.localScale = new Vector3(0.001f, 1f, 1f);
            feathers.Add(pivot);
        }
        return hinge;
    }

    private void AnimateModel(float dt)
    {
        if (root == null) return;
        bool flying = state == State.Chase || state == State.Capture || state == State.Escaping;
        float flapHz = state == State.Sprouting ? Mathf.Lerp(4f, 16f, Mathf.Clamp01(stateT / Mathf.Max(0.2f, sproutSeconds))) : state == State.Caught ? 0f : 18f + 6f * charge;
        flapPhase += dt * flapHz * Mathf.PI * 2f;
        float flap = Mathf.Sin(flapPhase);
        if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, -flap * 40f, flap * 22f);
        if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, flap * 40f, -flap * 22f);
        spin += dt * (flying ? 360f : 120f);
        if (body != null) body.localRotation = Quaternion.Euler(0f, spin, 12f * Mathf.Sin(Time.unscaledTime * 3f));
        float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 7f) + (hovering ? 0.12f : 0f);
        if (shell != null) { shell.localScale = Vector3.one * 1.15f * pulse; if (body != null) shell.localRotation = body.localRotation; }
        if (glow != null) { glow.range = root.transform.lossyScale.x * 7f; glow.intensity = (hovering ? 3.4f : 2.2f) * (state == State.Caught ? 1.6f : 1f); }
        if (trail != null)
        {
            trail.emitting = flying;
            trail.widthMultiplier = root.transform.lossyScale.x * 0.9f;
        }
    }

    // ------------------------------------------------------------------
    // The charge bar and the capture rings (screen-space UI following it)
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        if (canvasObject != null) return;
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        canvasObject = PixelUIKit.CreateCanvas("Golden Snitch UI", 610, new Vector2(1920f, 1080f), false);
        canvasObject.transform.SetParent(transform, false);
        canvasRect = canvasObject.GetComponent<RectTransform>();

        GameObject bar = new GameObject("Charge Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(canvasObject.transform, false);
        Image back = bar.GetComponent<Image>();
        back.color = new Color(0.06f, 0.05f, 0.03f, 0.85f);
        back.raycastTarget = false;
        PixelUIKit.StyleBox(back, true);
        barRoot = bar.GetComponent<RectTransform>();
        barRoot.anchorMin = barRoot.anchorMax = Vector2.zero;
        barRoot.pivot = new Vector2(0.5f, 0f);
        barRoot.sizeDelta = new Vector2(150f, 18f);
        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        barFillImage = fill.GetComponent<Image>();
        barFillImage.color = gold;
        barFillImage.raycastTarget = false;
        barFill = fill.GetComponent<RectTransform>();
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.offsetMin = new Vector2(3f, 3f);
        barFill.offsetMax = new Vector2(3f, -3f);

        ringTarget = MakeRing("Target Ring", new Color(1f, 0.82f, 0.25f, 0.95f), 0.07f, out ringTargetRect);
        ringShrink = MakeRing("Closing Ring", new Color(1f, 1f, 1f, 0.95f), 0.05f, out ringShrinkRect);

        hintLabel = PixelUIKit.CreateText(font, canvasObject.transform, "Hint", "", 26f, TextAlignmentOptions.Center, FontStyles.Bold, gold);
        hintLabel.raycastTarget = false;
        RectTransform hr = hintLabel.rectTransform;
        hr.anchorMin = hr.anchorMax = Vector2.zero;
        hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(420f, 40f);
    }

    private PixelArcGraphic MakeRing(string name, Color c, float halfWidth, out RectTransform rect) =>
        PixelArcRing.Create(canvasObject.transform, name, c, halfWidth, Vector2.zero, out rect);

    private void UpdateUI(Camera cam)
    {
        if (canvasObject == null || root == null) return;
        bool show = state == State.Chase || state == State.Capture;
        barRoot.gameObject.SetActive(show);
        hintLabel.gameObject.SetActive(show);
        bool capture = state == State.Capture;
        ringTarget.gameObject.SetActive(capture);
        ringShrink.gameObject.SetActive(capture);
        if (!show) return;
        Vector3 sp = cam.WorldToScreenPoint(root.transform.position);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector2 at = new Vector2(sp.x, sp.y) / scale;
        float radius = ScreenSizePx * 0.5f / scale;
        // The bar above it (kept on screen), filling with the charge.
        Rect r = canvasRect.rect;
        Vector2 barAt = at + new Vector2(0f, radius + 22f);
        barAt.x = Mathf.Clamp(barAt.x, 80f, r.width - 80f);
        barAt.y = Mathf.Min(barAt.y, r.height - 40f);
        barRoot.anchoredPosition = barAt;
        barFill.sizeDelta = new Vector2((barRoot.sizeDelta.x - 6f) * charge, barFill.sizeDelta.y);
        barFillImage.color = hovering ? Color.Lerp(gold, Color.white, 0.25f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f)) : new Color(gold.r * 0.65f, gold.g * 0.65f, gold.b * 0.65f, 1f);
        // The hint under it.
        hintLabel.rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(at.x, 210f, r.width - 210f), at.y - radius - 14f);
        if (capture)
        {
            float target = radius * 1.35f;
            float ringScale = RingScale();
            bool inWindow = Mathf.Abs(ringScale - 1f) <= captureTolerance;
            ringTargetRect.anchoredPosition = at;
            ringTargetRect.sizeDelta = Vector2.one * target * 2f / 0.9f;
            ringShrinkRect.anchoredPosition = at;
            ringShrinkRect.sizeDelta = Vector2.one * target * ringScale * 2f / 0.9f;
            ringShrink.color = inWindow ? new Color(0.55f, 1f, 0.55f, 1f) : new Color(1f, 1f, 1f, 0.95f);
            hintLabel.text = inWindow ? "CLICK!" : "Wait for the rings...";
            hintLabel.color = inWindow ? new Color(0.6f, 1f, 0.6f, 1f) : gold;
        }
        else
        {
            hintLabel.text = hovering ? "Charging..." : "Keep your mouse on it!";
            hintLabel.color = hovering ? gold : new Color(1f, 0.85f, 0.6f, 0.9f);
        }
    }

    // ------------------------------------------------------------------

    private void Cleanup()
    {
        if (root != null) Destroy(root);
        root = null;
        feathers.Clear();
        if (lineMat != null) Destroy(lineMat);
        lineMat = null;
        if (canvasObject != null) Destroy(canvasObject);
        canvasObject = null;
        state = State.Idle;
        Active = false;
        hovering = false;
        replacePending = false;
    }

    private static float EaseBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}

/// <summary>Code-made sounds of the Golden Snitch (registered in PixelAudio: snitch_appear, snitch_buzz, snitch_ready, snitch_catch, snitch_miss).</summary>
public static class PixelSnitchSynth
{
    private const int Rate = 22050;

    private static AudioClip Clip(string name, float[] data)
    {
        float peak = 0.0001f;
        for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.7f;
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>A bright rising shimmer with a high chime: the snitch appears.</summary>
    public static AudioClip Appear()
    {
        int n = (int)(Rate * 0.9f);
        float[] d = new float[n];
        System.Random rng = new System.Random(77);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float sweep = Mathf.Sin(2f * Mathf.PI * (900f * t + 900f * t * t)) * Mathf.Exp(-t * 3f) * 0.5f;
            float chime = (Mathf.Sin(2f * Mathf.PI * 1760f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 2640f * t)) * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 6f) * (t > 0.25f ? 1f : 0f);
            float sparkle = ((float)rng.NextDouble() * 2f - 1f) * 0.12f * Mathf.Exp(-t * 4f);
            d[i] = (sweep + chime * 0.6f + sparkle) * Mathf.Clamp01(t / 0.01f);
        }
        return Clip("Snitch Appear", d);
    }

    /// <summary>A short fluttering wing buzz (re-triggered quickly while it flies, so it sounds continuous).</summary>
    public static AudioClip Buzz()
    {
        int n = (int)(Rate * 0.2f);
        float[] d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * (210f + 30f * Mathf.Sin(t * 120f)) / Rate;
            float flutter = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 38f * t);
            float env = Mathf.Sin(Mathf.PI * t / 0.2f);
            d[i] = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.2f * Mathf.Sign(Mathf.Sin(phase * 3f))) * flutter * env;
        }
        return Clip("Snitch Buzz", d);
    }

    /// <summary>Two quick high dings: the bar is full.</summary>
    public static AudioClip Ready()
    {
        int n = (int)(Rate * 0.45f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float a = Mathf.Sin(2f * Mathf.PI * 1318f * t) * Mathf.Exp(-t * 14f);
            float t2 = t - 0.12f;
            float b = t2 > 0f ? Mathf.Sin(2f * Mathf.PI * 1976f * t2) * Mathf.Exp(-t2 * 10f) : 0f;
            d[i] = a + b;
        }
        return Clip("Snitch Ready", d);
    }

    /// <summary>A sparkling rising arpeggio: caught!</summary>
    public static AudioClip Catch()
    {
        float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f };
        int n = (int)(Rate * 1.3f);
        float[] d = new float[n];
        System.Random rng = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, s = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float tk = t - k * 0.07f;
                if (tk > 0f) s += Mathf.Sin(2f * Mathf.PI * notes[k] * tk) * Mathf.Exp(-tk * 3.5f);
            }
            s += ((float)rng.NextDouble() * 2f - 1f) * 0.08f * Mathf.Exp(-t * 2.5f) * (0.5f + 0.5f * Mathf.Sin(t * 90f));
            d[i] = s;
        }
        return Clip("Snitch Catch", d);
    }

    /// <summary>A falling whoosh: a miss.</summary>
    public static AudioClip Miss()
    {
        int n = (int)(Rate * 0.45f);
        float[] d = new float[n];
        System.Random rng = new System.Random(9);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * Mathf.Lerp(0.5f, 0.05f, t / 0.45f);
            float tone = Mathf.Sin(2f * Mathf.PI * (700f - 900f * t) * t) * 0.4f;
            d[i] = (lp + tone) * Mathf.Sin(Mathf.PI * t / 0.45f);
        }
        return Clip("Snitch Miss", d);
    }
}
