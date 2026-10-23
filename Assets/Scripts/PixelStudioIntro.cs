// ---- System notes ----
// PixelStudioIntro.cs
//   The GALACTIC ARCADE studio intro (design agreed in the Design Discussion chat). Plays ONCE per launch, before the title screen's launch intro
//   (PixelTitleIntro.IntroRoutine starts with `yield return PixelStudioIntro.PlayAtLaunch()`), ~11 s; any key / click / Esc skips to the fade.
//   Settings > Display > 'Studio intro' turns it off (PixelPrefs PixelClicker.Setting.StudioIntro, on this machine). Dev Hub > Events > 'Studio intro'
//   replays it (PixelStudioIntro.Replay) over whatever is on screen.
//   Everything is built in code when it starts and DESTROYED when it ends (objects, meshes, textures, materials, render textures: the 'owned' list):
//   * Its own little world far below the game (worldOffset), filmed by its own two cameras into RenderTextures, shown on its own top canvas, so it never
//     touches the game scene. The SPACE camera films Earth + the galaxy into a
//     16:9 texture that is the ARCADE CABINET's screen; the CABINET camera starts framed exactly on that screen (so it looks like plain space) and
//     pulls back for the reveal. The canvas shows the cabinet camera's picture.
//   STORYBOARD (all lengths Inspector values):
//   1 Earth close-up: a turning sphere with a code-drawn texture (oceans, continents, ice caps), a cloud shell, a night side (a soft shadow disc
//     in front), a blue atmosphere glow, a cratered Moon passing in front, a sun glare and a shooting star; a starfield and coloured nebulae behind.
//   2 a fast exponential pull-back (studio_whoosh): Earth shrinks to a dot and a spiral galaxy fills the view (galaxyStars points in one mesh, pink
//     star-forming clumps on the arms, a glowing core), slowly turning, over the nebulae and the far starfield.
//   3 'GAME OVER' blinks over it with an RGB split, a score line, a blinking 'INSERT COIN', scanlines, a CRT vignette and glass glare; the screen's
//     light flickers with it (studio_gameover jingle).
//   4 the cabinet camera pulls back: the galaxy was on the screen of an arcade cabinet floating in space (its own starfield, nebulae and tumbling
//     pixel cubes; studio_hum loop): side art, neon bezel frame, speaker grilles, a marquee reading studioName ringed by chasing bulbs, a two-player
//     control panel, a coin door with two lit slots, a kick plate. A little green cube ALIEN stands at it, slumped (head down, antennae drooping).
//   5 he perks up, turns round to the camera, winks and smiles, and turns back (alienSeconds);
//   6 he drops a spinning coin in the slot (studio_coin, the slot flashes, 'CREDIT 01' + 'PRESS START'), presses 1P START (studio_start, the screen
//     flashes white, 'GET READY') and hops for joy (coinSeconds);
//   7 the camera rises to the marquee while the bulbs chase fast and 'presents' fades in (presentsSeconds); then fade to black and the title intro.
//   All music waits while it plays (PixelAudio checks HoldsMusic: playing, or still due this launch with a 12 s time-out; the title screen calls
//   SkipThisLaunch when its launch intro won't run).
//   At launch it first waits on black until frames come smoothly (maxSettleTime), and the timeline moves at most maxFrameStep per frame, so loading
//   hitches slow it down instead of skipping scenes. The space set is built small and scaled up (SpaceScale) to keep float precision far from the origin.
//   No per-frame allocations (everything is cached when it is built; texts only change at fixed moments; the timeline runs in one coroutine on unscaled time).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelStudioIntro : MonoBehaviour
{
    [Header("Studio intro")]
    [Tooltip("The studio name on the cabinet's marquee.")]
    [SerializeField] private string studioName = "GALACTIC ARCADE";
    [Tooltip("The text that blinks over the galaxy.")]
    [SerializeField] private string gameOverText = "GAME OVER";
    [Tooltip("The score line at the top of the arcade screen.")]
    [SerializeField] private string scoreText = "1UP  004730          HI  999990";
    [Tooltip("Blinks at the bottom of the arcade screen until the coin goes in.")]
    [SerializeField] private string insertCoinText = "INSERT COIN";
    [Tooltip("Shown at the bottom of the arcade screen once the coin is in.")]
    [SerializeField] private string creditText = "CREDIT 01";
    [Tooltip("Blinks in the middle of the screen once the coin is in.")]
    [SerializeField] private string pressStartText = "PRESS START";
    [Tooltip("Shown on the screen after the alien presses start.")]
    [SerializeField] private string getReadyText = "GET READY";
    [Tooltip("Fades in under the picture at the end (empty = none).")]
    [SerializeField] private string presentsText = "presents";

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.1f), Tooltip("Earth close-up.")] private float earthSeconds = 1.5f;
    [SerializeField, Min(0.1f), Tooltip("The fast pull-back from Earth to the whole galaxy.")] private float zoomSeconds = 2f;
    [SerializeField, Min(0.1f), Tooltip("'GAME OVER' blinking over the galaxy.")] private float gameOverSeconds = 1.1f;
    [SerializeField, Min(0.1f), Tooltip("The pull-back that reveals the arcade cabinet.")] private float revealSeconds = 2f;
    [SerializeField, Min(0.1f), Tooltip("The alien perks up, turns round to wink at the camera and turns back.")] private float alienSeconds = 1.3f;
    [SerializeField, Min(0.1f), Tooltip("The alien drops a coin in, presses START and hops.")] private float coinSeconds = 1.6f;
    [SerializeField, Min(0.1f), Tooltip("The camera rises to the marquee and 'presents' fades in.")] private float presentsSeconds = 1.4f;
    [SerializeField, Min(0.05f), Tooltip("The fade to black at the end.")] private float fadeSeconds = 0.6f;
    [SerializeField, Min(0.05f), Tooltip("The quicker fade to black after a skip.")] private float skipFadeSeconds = 0.25f;
    [SerializeField, Min(1f), Tooltip("Blinks per second of 'GAME OVER'.")] private float blinkRate = 5f;
    [SerializeField, Min(0f), Tooltip("The fade in from black at the start (after the set has rendered its first frames).")] private float fadeInTime = 1.2f;
    [SerializeField, Range(0.02f, 0.2f), Tooltip("The most the timeline moves on in one frame: a slow frame (the game is still loading at launch) slows it down instead of skipping scenes.")] private float maxFrameStep = 0.05f;
    [SerializeField, Min(0f), Tooltip("Most seconds it waits (on black) at the start for the game's loading hitches to settle before it begins.")] private float maxSettleTime = 0.6f;

    [Header("Space colours")]
    [SerializeField] private Color oceanColor = new Color(0.07f, 0.22f, 0.55f, 1f);
    [SerializeField] private Color landColor = new Color(0.2f, 0.55f, 0.22f, 1f);
    [SerializeField] private Color desertColor = new Color(0.72f, 0.62f, 0.38f, 1f);
    [SerializeField] private Color iceColor = new Color(0.92f, 0.95f, 1f, 1f);
    [SerializeField] private Color cloudColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color atmosphereColor = new Color(0.35f, 0.65f, 1f, 0.8f);
    [SerializeField, Tooltip("Colour of Earth's night side (alpha = how dark it gets).")] private Color nightColor = new Color(0.01f, 0.02f, 0.08f, 0.85f);
    [SerializeField] private Color moonColor = new Color(0.72f, 0.72f, 0.75f, 1f);
    [SerializeField] private Color sunColor = new Color(1f, 0.93f, 0.75f, 1f);
    [SerializeField] private Color galaxyCoreColor = new Color(1f, 0.88f, 0.65f, 1f);
    [SerializeField] private Color galaxyArmColor = new Color(0.55f, 0.65f, 1f, 1f);
    [SerializeField, Tooltip("The bright pink star-forming clumps along the galaxy's arms.")] private Color galaxyClumpColor = new Color(1f, 0.45f, 0.75f, 1f);
    [SerializeField, Tooltip("Nebula clouds behind the galaxy and the cabinet (alpha = how strong).")] private Color nebulaColorA = new Color(0.55f, 0.2f, 0.85f, 0.28f);
    [SerializeField] private Color nebulaColorB = new Color(0.1f, 0.6f, 0.75f, 0.22f);
    [SerializeField] private Color nebulaColorC = new Color(0.95f, 0.3f, 0.5f, 0.18f);
    [SerializeField] private Color gameOverColor = new Color(1f, 0.25f, 0.3f, 1f);
    [SerializeField, Tooltip("The other half of GAME OVER's RGB split.")] private Color gameOverSplitColor = new Color(0.2f, 0.9f, 1f, 0.55f);
    [SerializeField, Tooltip("Colour of the score line, INSERT COIN and CREDIT.")] private Color screenTextColor = new Color(1f, 0.85f, 0.3f, 1f);
    [SerializeField, Range(500, 6000), Tooltip("Glowing points in the galaxy.")] private int galaxyStars = 3000;
    [SerializeField, Range(0, 4000), Tooltip("Far background stars behind Earth and the galaxy.")] private int backgroundStars = 1600;

    [Header("Arcade colours")]
    [SerializeField] private Color cabinetColor = new Color(0.16f, 0.1f, 0.35f, 1f);
    [SerializeField] private Color cabinetTrimColor = new Color(1f, 0.3f, 0.75f, 1f);
    [SerializeField] private Color marqueeColor = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color marqueeTextColor = new Color(0.12f, 0.05f, 0.25f, 1f);
    [SerializeField] private Color bezelColor = new Color(0.05f, 0.05f, 0.07f, 1f);
    [SerializeField, Tooltip("The neon frame round the screen and the side art.")] private Color neonColor = new Color(0.2f, 0.95f, 1f, 1f);
    [SerializeField, Tooltip("The chasing bulbs round the marquee.")] private Color bulbColor = new Color(1f, 0.95f, 0.7f, 1f);
    [SerializeField, Range(0.5f, 3f), Tooltip("Size of the alien (1 = a small cube a third of the cabinet's height).")] private float alienSize = 1.75f;
    [SerializeField, Tooltip("Where the alien stands, relative to the cabinet's foot (x = left / right, z = in front; the control panel ends at z -0.47).")] private Vector3 alienPosition = new Vector3(-0.3f, 0f, -0.75f);
    [SerializeField] private Color alienColor = new Color(0.35f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color buttonColorA = new Color(1f, 0.2f, 0.25f, 1f);
    [SerializeField] private Color buttonColorB = new Color(0.2f, 0.6f, 1f, 1f);
    [SerializeField] private Color buttonColorC = new Color(1f, 0.85f, 0.15f, 1f);
    [SerializeField] private Color coinColor = new Color(1f, 0.8f, 0.25f, 1f);
    [SerializeField, Range(0, 40), Tooltip("Little pixel cubes tumbling in space round the cabinet.")] private int floatingCubes = 18;
    [SerializeField, Range(0, 2000), Tooltip("Stars behind the floating cabinet.")] private int arcadeStars = 700;
    [SerializeField, Min(0.5f), Tooltip("Bulb steps per second of the marquee chase (doubled at the end).")] private float chaseRate = 7f;

    [Header("Set-up")]
    [SerializeField, Tooltip("Where its little world is built: far from the game so no game camera ever sees it.")] private Vector3 worldOffset = new Vector3(0f, -20000f, 0f);
    [SerializeField, Tooltip("Sorting order of its canvas (above everything, the title screen included).")] private int sortingOrder = 5000;

    private const string Pref = "PixelClicker.Setting.StudioIntro";

    /// <summary>Play the studio intro when the game launches (Settings > Display > Studio intro).</summary>
    public static bool Enabled
    {
        get => PixelPrefs.GetInt(Pref, 1) != 0;
        set { PixelPrefs.SetInt(Pref, value ? 1 : 0); PixelPrefs.Save(); }
    }

    /// <summary>True while it plays.</summary>
    public static bool Playing { get; private set; }

    /// <summary>True while the music should wait: it is playing, or it is still due to play at this launch (with a time-out, so the music can
    /// never be held for long if the title intro does not run). PixelAudio checks this so the menu music starts after it.</summary>
    public static bool HoldsMusic => Playing || (instance != null && !playedThisLaunch && Enabled && Time.realtimeSinceStartup < 12f);

    /// <summary>The title screen calls this when its launch intro will not run (so nothing waits for the studio intro).</summary>
    public static void SkipThisLaunch() { playedThisLaunch = true; }

    private static PixelStudioIntro instance;
    private static bool playedThisLaunch;   // (not reset on Restart on purpose: once per launch)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; playedThisLaunch = false; Playing = false; }

    private void Awake() { instance = this; }
    private void OnDestroy() { if (instance == this) instance = null; TearDown(); }

    /// <summary>The title screen's launch intro waits on this first (yields nothing when it is off or already played this launch).</summary>
    public static IEnumerator PlayAtLaunch()
    {
        if (instance == null || playedThisLaunch || !Enabled || Playing) yield break;
        playedThisLaunch = true;
        yield return instance.Run(true);
    }

    /// <summary>Dev Hub > Events: plays it again now, over whatever is on screen.</summary>
    public static bool Replay()
    {
        if (instance == null || Playing) return false;
        instance.StartCoroutine(instance.Run(false));
        return true;
    }

    // ------------------------------------------------------------------
    // What it builds (all kept so the timeline needs no allocations and TearDown can destroy everything)
    // ------------------------------------------------------------------

    private GameObject world, canvasGo;
    private PixelFactoryKit kit;
    private RenderTexture spaceTexture, outTexture;
    private Camera spaceCam, cabinetCam;
    private readonly List<Object> owned = new List<Object>();   // textures, meshes and materials made here (destroyed by TearDown)

    // Space
    private Transform earth, clouds, moon, galaxy, backdrop, streak;
    private Vector3 earthPos, galaxyCentre;
    private Material glowMat, streakMat;

    // Arcade
    private Transform screenTransform, alien, alienBody, antennaL, antennaR, armR, coin, mouthL, mouthR;
    private Transform[] eyes = new Transform[2], pupils = new Transform[2], tips = new Transform[2];
    private Vector3 eyeScale, pupilRest, tipScale;
    private Vector3 screenCentre, marqueeCentre, alienArmRest, coinSlotPos, startButtonPos;
    private Light screenLight, slotLight;
    private float screenLightBase;
    private Renderer[] bulbs;
    private Renderer startButton;
    private Material bulbOn, bulbOff, startOn, startOff;
    private Transform[] cubes;
    private Vector3[] cubeSpin, cubeHome;

    // Screen overlay
    private TMP_Text gameOverLabel, splitLabel, scoreLabel, bottomLabel, presentsLabel;
    private RawImage scanlines;
    private Image fade, flash;
    private CanvasGroup group;
    private int screenStage = -1;   // which texts the screen shows (they change only at fixed moments: no per-frame strings)

    private static Shader UnlitShader()
    {
        Shader s = Shader.Find("Sprites/Default");   // (always in a build; unlit, alpha, vertex colour)
        return s != null ? s : Shader.Find("Unlit/Transparent");
    }

    private T Own<T>(T o) where T : Object { owned.Add(o); return o; }

    private IEnumerator Run(bool atLaunch)
    {
        Playing = true;
        Build();
        fade.color = Color.black;

        // Launch is the busiest moment (every system starting, shaders compiling): wait on black until frames come smoothly again,
        // so the first seconds of the story aren't lost in one long frame.
        float waited = 0f;
        int calm = 0;
        while (calm < 3 && waited < maxSettleTime)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
            calm = Time.unscaledDeltaTime < 0.1f ? calm + 1 : 0;
        }

        Timeline tl = MakeTimeline();
        float t = 0f;
        bool whoosh = false, jingle = false, hum = false, coinSound = false, startSound = false, skipped = false;

        while (t < tl.end)
        {
            if (PixelInput.AnyInputPressed()) { skipped = true; break; }
            if (!whoosh && t >= tl.zoom) { whoosh = true; PixelAudio.Play("studio_whoosh"); }
            if (!jingle && t >= tl.over) { jingle = true; PixelAudio.Play("studio_gameover"); }
            if (!hum && t >= tl.reveal) { hum = true; PixelAudio.StartLoop("studio_hum"); }
            if (!coinSound && t >= tl.coin + coinSeconds * CoinIn) { coinSound = true; PixelAudio.Play("studio_coin"); }
            if (!startSound && t >= tl.coin + coinSeconds * StartPress) { startSound = true; PixelAudio.Play("studio_start"); }
            Pose(t, tl);
            fade.color = new Color(0f, 0f, 0f, fadeInTime > 0f ? 1f - Smooth(t / fadeInTime) : 0f);
            yield return null;
            t += Mathf.Min(Time.unscaledDeltaTime, maxFrameStep);
        }

        // Fade to black (quicker after a skip); at launch the title's own black screen takes over from there.
        float f = 0f, fadeDur = skipped ? skipFadeSeconds : fadeSeconds;
        while (f < fadeDur)
        {
            f += Mathf.Min(Time.unscaledDeltaTime, maxFrameStep);
            fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(f / fadeDur));
            if (!skipped) Pose(t + f, tl);
            yield return null;
        }
        PixelAudio.StopLoop("studio_hum");
        if (!atLaunch)
        {
            // A replay over the game: drop the black away again instead of cutting to the game.
            DestroyWorld();
            float g = 0f;
            while (g < 0.35f) { g += Time.unscaledDeltaTime; group.alpha = 1f - g / 0.35f; yield return null; }
        }
        TearDown();
        Playing = false;
    }

    // ------------------------------------------------------------------
    // The timeline: where everything is at time t
    // ------------------------------------------------------------------

    /// <summary>When each scene starts (seconds from the start).</summary>
    private struct Timeline { public float zoom, over, reveal, alien, coin, presents, end; }

    private Timeline MakeTimeline()
    {
        Timeline tl;
        tl.zoom = earthSeconds;
        tl.over = tl.zoom + zoomSeconds;
        tl.reveal = tl.over + gameOverSeconds;
        tl.alien = tl.reveal + revealSeconds;
        tl.coin = tl.alien + alienSeconds;
        tl.presents = tl.coin + coinSeconds;
        tl.end = tl.presents + presentsSeconds;
        return tl;
    }

    // Moments inside the coin scene (fractions of coinSeconds).
    private const float CoinIn = 0.42f, StartPress = 0.74f;

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    private static float Bump(float x) => x <= 0f || x >= 1f ? 0f : Mathf.Sin(x * Mathf.PI);
    private static float Phase(float t, float start, float length) => (t - start) / Mathf.Max(0.001f, length);

    private void Pose(float t, Timeline tl)
    {
        PoseSpace(t, tl);
        PoseScreen(t, tl);
        PoseCamera(t, tl);
        PoseCabinet(t, tl);
        PoseAlien(t, tl);
    }

    private void PoseSpace(float t, Timeline tl)
    {
        // Earth turns, the galaxy turns slowly, the Moon slides across, the space camera pulls back exponentially from Earth to the whole galaxy.
        earth.localRotation = Quaternion.Euler(-23f, -t * 30f, 0f);
        clouds.localRotation = Quaternion.Euler(-23f, -t * 38f, 0f);
        moon.localRotation = Quaternion.Euler(0f, -t * 10f, 0f);
        float ma = -0.75f + t * 0.3f;
        moon.localPosition = earthPos + new Vector3(Mathf.Cos(ma) * 0.042f, 0.009f, Mathf.Sin(ma) * 0.042f);
        galaxy.localRotation = Quaternion.Euler(0f, 0f, t * 4f);
        backdrop.localRotation = Quaternion.Euler(0f, 0f, t * 0.6f);
        float z = Smooth(Phase(t, tl.zoom, zoomSeconds));
        float ez = z * z;   // (slow to start, then fast: a pull-back)
        float dist = 0.075f * Mathf.Pow(150f / 0.075f, ez);
        Vector3 look = Vector3.Lerp(earthPos, galaxyCentre, Smooth(Phase(t, tl.zoom, zoomSeconds) * 1.2f - 0.2f));
        // A small shake while the pull-back is at its fastest.
        float shake = Bump(Phase(t, tl.zoom + zoomSeconds * 0.25f, zoomSeconds * 0.5f)) * dist * 0.012f;
        Vector3 jitter = new Vector3(Mathf.Sin(t * 53f), Mathf.Sin(t * 47f + 1.3f), 0f) * shake;
        spaceCam.transform.localPosition = look + new Vector3(0f, 0f, -dist) + jitter;
        spaceCam.nearClipPlane = Mathf.Max(0.004f, dist * 0.05f) * SpaceScale;   // (clip planes are in world units: the space set is scaled up)
        spaceCam.farClipPlane = (dist + 650f) * SpaceScale;

        // A shooting star crosses behind Earth during the close-up.
        float s = Phase(t, earthSeconds * 0.25f, 0.55f);
        bool showStreak = s > 0f && s < 1f;
        streak.gameObject.SetActive(showStreak);
        if (showStreak)
        {
            streak.localPosition = earthPos + new Vector3(Mathf.Lerp(-0.09f, 0.07f, s), Mathf.Lerp(0.045f, 0.005f, s), 0.25f);
            streakMat.color = new Color(1f, 1f, 1f, Bump(s) * 0.9f);
        }
    }

    private void PoseScreen(float t, Timeline tl)
    {
        // Which texts the arcade screen shows: nothing, GAME OVER + INSERT COIN, the credit + PRESS START, GET READY.
        float coinA = Phase(t, tl.coin, coinSeconds);
        int stage = t < tl.over ? 0 : coinA < CoinIn ? 1 : coinA < StartPress ? 2 : 3;
        if (stage != screenStage)
        {
            screenStage = stage;
            string mid = stage == 2 ? pressStartText : stage == 3 ? getReadyText : gameOverText;
            gameOverLabel.text = mid;
            splitLabel.text = mid;
            bottomLabel.text = stage >= 2 ? creditText : insertCoinText;
            gameOverLabel.color = stage >= 2 ? screenTextColor : gameOverColor;
        }

        bool over = stage > 0;
        float blink;
        if (stage == 1) blink = t < tl.reveal ? (Mathf.Repeat((t - tl.over) * blinkRate, 1f) < 0.6f ? 1f : 0.15f) : 0.85f;
        else if (stage == 2) blink = Mathf.Repeat(t * 4f, 1f) < 0.55f ? 1f : 0.1f;
        else blink = 1f;
        Color c = gameOverLabel.color;
        gameOverLabel.color = new Color(c.r, c.g, c.b, over ? blink : 0f);
        splitLabel.color = new Color(gameOverSplitColor.r, gameOverSplitColor.g, gameOverSplitColor.b, over ? gameOverSplitColor.a * blink : 0f);
        float jitter = over && stage == 1 && t < tl.reveal ? Mathf.Sin(t * 37f) * 3f : 0f;   // (the split shivers while GAME OVER is new)
        splitLabel.rectTransform.anchoredPosition = new Vector2(7f + jitter, -2f);
        gameOverLabel.rectTransform.anchoredPosition = new Vector2(-3f, 0f);

        scoreLabel.color = new Color(screenTextColor.r, screenTextColor.g, screenTextColor.b, over ? 0.9f : 0f);
        float bottomBlink = stage == 1 ? (Mathf.Repeat(t * 1.6f, 1f) < 0.6f ? 1f : 0f) : 1f;
        bottomLabel.color = new Color(screenTextColor.r, screenTextColor.g, screenTextColor.b, over ? bottomBlink : 0f);
        scanlines.color = new Color(1f, 1f, 1f, over ? 0.35f : 0.12f);
        scanlines.uvRect = new Rect(0f, t * 0.6f, 1f, 90f);

        // The white flash when START is pressed.
        float fl = Phase(t, tl.coin + coinSeconds * StartPress, 0.35f);
        flash.color = new Color(1f, 1f, 1f, fl > 0f && fl < 1f ? (1f - fl) * 0.9f : 0f);

        // The screen's light on the alien follows what the screen shows.
        float lit = over ? 0.7f + 0.3f * blink : 0.8f;
        screenLight.intensity = screenLightBase * lit + (fl > 0f && fl < 1f ? (1f - fl) * 3f : 0f);
    }

    private void PoseCamera(float t, Timeline tl)
    {
        // The cabinet camera: framed exactly on the screen until the reveal, then pulls back to show the cabinet and the alien,
        // and at the end rises to the marquee.
        float r = Smooth(Phase(t, tl.reveal, revealSeconds));
        float p = Smooth(Phase(t, tl.presents, presentsSeconds));
        Vector3 startPos = screenCentre + screenTransform.forward * -ScreenFillDistance;
        Vector3 endPos = screenCentre + new Vector3(-1.15f, 0.05f, -2.8f);
        Vector3 risePos = screenCentre + new Vector3(-0.55f, 0.55f, -2.25f);
        Vector3 pos = Vector3.Lerp(Vector3.Lerp(startPos, endPos, r), risePos, p);
        pos += new Vector3(Mathf.Sin(t * 0.7f), Mathf.Sin(t * 0.9f + 1f), 0f) * 0.02f * r;   // (a gentle hand-held drift once it has pulled back)
        cabinetCam.transform.localPosition = pos;
        Vector3 aim = Vector3.Lerp(screenCentre, screenCentre + new Vector3(-0.35f, -0.35f, 0f), r);
        aim = Vector3.Lerp(aim, (marqueeCentre + screenCentre) * 0.5f + new Vector3(0f, 0.1f, 0f), p);
        cabinetCam.transform.localRotation = Quaternion.LookRotation(aim - pos, Vector3.up);

        if (!string.IsNullOrEmpty(presentsText))
        {
            float pa = Smooth(Phase(t, tl.presents + presentsSeconds * 0.25f, presentsSeconds * 0.5f));
            presentsLabel.color = new Color(1f, 1f, 1f, pa * 0.9f);
            presentsLabel.characterSpacing = Mathf.Lerp(40f, 18f, pa);
        }
    }

    private void PoseCabinet(float t, Timeline tl)
    {
        // Marquee bulbs chase (twice as fast at the end); the 1P START button blinks while the screen says PRESS START.
        float rate = t >= tl.presents ? chaseRate * 2f : chaseRate;
        int step = Mathf.FloorToInt(t * rate);
        for (int i = 0; i < bulbs.Length; i++) bulbs[i].sharedMaterial = (i + step) % 3 == 0 ? bulbOn : bulbOff;
        startButton.sharedMaterial = screenStage == 2 && Mathf.Repeat(t * 4f, 1f) < 0.55f || screenStage == 3 ? startOn : startOff;

        // The coin slot flashes as the coin goes in.
        float coinA = Phase(t, tl.coin, coinSeconds);
        slotLight.intensity = Bump((coinA - CoinIn + 0.05f) / 0.3f) * 2.5f;

        // Pixel cubes tumble and bob round the cabinet.
        for (int i = 0; i < cubes.Length; i++)
        {
            cubes[i].localRotation = Quaternion.Euler(cubeSpin[i] * t);
            cubes[i].localPosition = cubeHome[i] + new Vector3(0f, Mathf.Sin(t * 0.8f + i) * 0.06f, 0f);
        }
    }

    private void PoseAlien(float t, Timeline tl)
    {
        bool over = t >= tl.over;
        float a = Phase(t, tl.alien, alienSeconds);
        float c = Phase(t, tl.coin, coinSeconds);

        // Slumped from the game over (head down, antennae drooping, eyes half shut), then perks up.
        float perk = Smooth(a / 0.25f);
        float slump = over ? 1f - perk : 0f;
        // Turns round to the camera (wink + smile) and back to the cabinet.
        float turn = Smooth((a - 0.22f) / 0.22f) * (1f - Smooth((a - 0.78f) / 0.2f));
        float happy = Mathf.Clamp01(Smooth((a - 0.3f) / 0.2f) + (c > 0f ? 1f : 0f));
        // The hop of joy after START.
        float hop = Bump(Phase(c, StartPress + 0.02f, 0.2f)) + 0.6f * Bump(Phase(c, StartPress + 0.24f, 0.14f));
        float bounce = t >= tl.presents ? Mathf.Abs(Mathf.Sin((t - tl.presents) * 7f)) * 0.04f : 0f;

        alien.localPosition = alienPosition + new Vector3(0f, (hop * 0.14f + bounce) * alienSize, 0f);
        alienBody.localRotation = Quaternion.Euler(0f, TurnYaw * turn, 0f) * Quaternion.Euler(22f * slump - 6f * hop, 0f, 0f);
        alienBody.localPosition = new Vector3(0f, 0.22f - 0.04f * slump + 0.02f * Mathf.Sin(t * 6f) * perk, 0f);
        float wiggle = Mathf.Sin(t * 9f) * 8f * perk;
        antennaL.localRotation = Quaternion.Euler(0f, 0f, 18f + 50f * slump + wiggle);
        antennaR.localRotation = Quaternion.Euler(0f, 0f, -18f - 50f * slump - wiggle);
        float glow = 1f + 0.25f * Mathf.Sin(t * 8f) * perk + 0.5f * hop;
        tips[0].localScale = tips[1].localScale = tipScale * glow;

        // Eyes: half shut while slumped, a blink as he perks up, a wink (right eye) while facing the camera.
        float blinkShut = Bump(Phase(a, 0.12f, 0.08f));
        float wink = Bump(Phase(a, 0.48f, 0.2f));
        for (int i = 0; i < 2; i++)
        {
            float open = Mathf.Lerp(1f, 0.4f, slump) * (1f - 0.9f * blinkShut) * (i == 1 ? 1f - 0.9f * wink : 1f);
            eyes[i].localScale = new Vector3(eyeScale.x, eyeScale.y * open, eyeScale.z);
            pupils[i].localPosition = pupilRest + new Vector3(0f, -0.25f * slump + 0.12f * hop, 0f);
        }
        // Mouth corners: down while sad, up while happy.
        float mood = Mathf.Lerp(over ? -1f : 0f, 1f, happy);
        mouthL.localPosition = new Vector3(-0.045f, 0.07f + 0.014f * mood, 0.162f);
        mouthR.localPosition = new Vector3(0.045f, 0.07f + 0.014f * mood, 0.162f);
        mouthL.localRotation = Quaternion.Euler(0f, 0f, -25f * mood);
        mouthR.localRotation = Quaternion.Euler(0f, 0f, 25f * mood);

        // The hand: rests, then carries a coin to the slot, then presses 1P START twice.
        Vector3 hand = alienArmRest;
        if (c > 0f && c < CoinIn) hand = Vector3.Lerp(alienArmRest, coinSlotPos, Smooth(c / CoinIn)) + new Vector3(0f, Bump(c / CoinIn) * 0.12f, 0f);
        else if (c >= CoinIn && c < StartPress) hand = Vector3.Lerp(coinSlotPos, startButtonPos, Smooth((c - CoinIn) / (StartPress - CoinIn) * 1.3f)) + new Vector3(0f, Bump((c - CoinIn) / (StartPress - CoinIn)) * 0.1f, 0f);
        else if (c >= StartPress && c < 1f) hand = Vector3.Lerp(startButtonPos + new Vector3(0f, 0.05f, 0f), startButtonPos, Mathf.Abs(Mathf.Cos((c - StartPress) / (1f - StartPress) * Mathf.PI * 2f)) * (1f - Smooth((c - 0.9f) / 0.1f)));
        armR.localPosition = alien.InverseTransformPoint(world.transform.TransformPoint(hand));

        // The coin: in the hand on the way to the slot, spinning, gone once it is in.
        bool showCoin = c > 0.02f && c < CoinIn;
        coin.gameObject.SetActive(showCoin);
        if (showCoin)
        {
            coin.localPosition = hand + new Vector3(0f, 0.05f, -0.02f) * Mathf.Clamp01(1f - c / CoinIn * 1.1f);
            coin.localRotation = Quaternion.Euler(0f, t * 540f, 90f);
        }
    }

    private static readonly Color StartWhite = new Color(0.85f, 0.85f, 0.85f, 1f);   // (the START buttons when not lit)

    /// <summary>How far the alien's body turns to face the camera (degrees, from facing the cabinet).</summary>
    private const float TurnYaw = 172f;

    /// <summary>The space set is built in small numbers and scaled up by this (a 0.02-wide Earth 20000 units from the origin would lose float precision).</summary>
    private const float SpaceScale = 100f;
    private const float ScreenW = 0.8f, ScreenH = 0.45f, CabinetFov = 50f;
    /// <summary>How far in front of the screen the cabinet camera must be for the screen to fill its frame (16:9 screen, a hair closer so no edge shows).</summary>
    private static float ScreenFillDistance => ScreenH * 0.5f / Mathf.Tan(CabinetFov * 0.5f * Mathf.Deg2Rad) * 0.97f;

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    private void Build()
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        kit = new PixelFactoryKit(clicker, false);
        world = new GameObject("Studio Intro World");
        world.transform.position = worldOffset;
        screenStage = -1;

        int w = Mathf.Max(640, Screen.width), h = Mathf.Max(360, Screen.height);
        spaceTexture = new RenderTexture(1280, 720, 24) { name = "Studio Intro Space" };
        outTexture = new RenderTexture(w, h, 24) { name = "Studio Intro Out" };

        BuildSpace();
        BuildArcade(clicker != null ? clicker.UIFont : null);
        BuildCanvas(clicker);
    }

    private Camera MakeCamera(string name, Transform parent, RenderTexture target, float fov, Color clear)
    {
        GameObject go = new GameObject(name, typeof(Camera));
        go.transform.SetParent(parent, false);
        Camera c = go.GetComponent<Camera>();
        c.targetTexture = target;
        c.fieldOfView = fov;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = clear;
        c.depth = -50f;
        return c;
    }

    private Material UnlitMat(Texture tex, Color color) => Own(new Material(UnlitShader()) { mainTexture = tex, color = color });

    /// <summary>A flat quad facing the cameras (they look along +Z), unlit.</summary>
    private Transform Quad(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;
        q.GetComponent<Renderer>().sharedMaterial = mat;
        return q.transform;
    }

    private void BuildSpace()
    {
        Transform space = new GameObject("Space").transform;
        space.SetParent(world.transform, false);
        space.localScale = Vector3.one * SpaceScale;   // (the numbers below are small; scaled up so a tiny Earth this far from the origin keeps its float precision)
        galaxyCentre = Vector3.zero;
        earthPos = new Vector3(22f, -9f, 0f);   // on an outer arm

        Texture2D glowTex = Own(MakeRadialTexture(64, 0.62f));
        Texture2D dotTex = Own(MakeRadialTexture(32, 0.9f));
        Texture2D nebulaTex = Own(MakeNebulaTexture());

        // The far starfield and the nebulae: behind everything, so they show behind Earth at the start and behind the galaxy at the end.
        System.Random rng = new System.Random(5150);
        float R() => (float)rng.NextDouble();
        backdrop = new GameObject("Backdrop Stars", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        backdrop.SetParent(space, false);
        backdrop.localPosition = new Vector3(0f, 0f, 380f);
        Vector3[] bp = new Vector3[backgroundStars];
        float[] bs = new float[backgroundStars];
        Color[] bc = new Color[backgroundStars];
        for (int i = 0; i < backgroundStars; i++)
        {
            bp[i] = new Vector3((R() - 0.5f) * 1400f, (R() - 0.5f) * 900f, (R() - 0.5f) * 120f);
            bs[i] = 0.5f + Mathf.Pow(R(), 4f) * 2.6f;
            float hue = R();
            bc[i] = Color.Lerp(Color.white, hue < 0.33f ? new Color(0.7f, 0.8f, 1f) : hue < 0.66f ? new Color(1f, 0.85f, 0.7f) : Color.white, R() * 0.6f);
            bc[i].a = 0.35f + R() * 0.65f;
        }
        backdrop.GetComponent<MeshFilter>().sharedMesh = Own(MakeDotMesh("Studio Intro Backdrop", bp, bs, bc));
        backdrop.GetComponent<MeshRenderer>().sharedMaterial = UnlitMat(dotTex, Color.white);
        Material[] nebulae = { UnlitMat(nebulaTex, nebulaColorA), UnlitMat(nebulaTex, nebulaColorB), UnlitMat(nebulaTex, nebulaColorC) };
        Vector4[] clouds4 =
        {
            new Vector4(40f, -20f, 260f, 300f), new Vector4(-90f, 50f, 230f, 260f), new Vector4(110f, 60f, 200f, 220f),
            new Vector4(-30f, -80f, 180f, 200f), new Vector4(25f, -5f, 140f, 120f), new Vector4(-60f, -10f, 120f, 150f),
        };
        for (int i = 0; i < clouds4.Length; i++)
        {
            Transform n = Quad("Nebula", space, new Vector3(clouds4[i].x, clouds4[i].y, clouds4[i].z), Vector3.one * clouds4[i].w, nebulae[i % 3]);
            n.localRotation = Quaternion.Euler(0f, 0f, i * 67f);
        }

        // Earth: a turning sphere with a code-drawn texture, a cloud shell, a night side, a soft atmosphere glow behind it, the Moon and the sun's glare.
        float er = 0.02f;
        Material earthMat = UnlitMat(Own(MakeEarthTexture()), Color.white);
        Material cloudMat = UnlitMat(Own(MakeCloudTexture()), Color.white);
        earth = MakeSphere("Earth", space, earthPos, er, earthMat);
        clouds = MakeSphere("Clouds", space, earthPos, er * 1.025f, cloudMat);
        // The night side: a soft shadow disc a hair in front of Earth's centre, sized to its outline (the cameras look along +Z).
        Quad("Night Side", space, earthPos + new Vector3(0f, 0f, -er * 0.02f), Vector3.one * er * 2.12f, UnlitMat(Own(MakeNightTexture()), nightColor));
        Quad("Atmosphere", space, earthPos + new Vector3(0f, 0f, er * 1.5f), Vector3.one * er * 2.7f, UnlitMat(glowTex, atmosphereColor));
        moon = MakeSphere("Moon", space, earthPos, er * 0.27f, UnlitMat(Own(MakeMoonTexture()), moonColor));
        Vector3 sunPos = earthPos + new Vector3(-0.1f, 0.055f, 0.6f);
        Quad("Sun", space, sunPos, Vector3.one * 0.05f, UnlitMat(glowTex, sunColor));
        Quad("Sun Halo", space, sunPos + new Vector3(0f, 0f, 0.01f), Vector3.one * 0.3f, UnlitMat(glowTex, new Color(sunColor.r, sunColor.g, sunColor.b, 0.22f)));
        streakMat = UnlitMat(glowTex, Color.white);
        streak = Quad("Shooting Star", space, earthPos, new Vector3(0.035f, 0.0012f, 1f), streakMat);
        streak.localRotation = Quaternion.Euler(0f, 0f, -14f);
        streak.gameObject.SetActive(false);

        // The galaxy: thousands of glowing points on two spiral arms + a bright core, one mesh of little quads, over a soft core glow.
        galaxy = new GameObject("Galaxy", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        galaxy.SetParent(space, false);
        galaxy.localPosition = galaxyCentre;
        galaxy.GetComponent<MeshFilter>().sharedMesh = Own(MakeGalaxyMesh());
        galaxy.GetComponent<MeshRenderer>().sharedMaterial = UnlitMat(dotTex, Color.white);
        Quad("Galaxy Core Glow", space, galaxyCentre + new Vector3(0f, 0f, 1.5f), Vector3.one * 48f, UnlitMat(glowTex, new Color(galaxyCoreColor.r, galaxyCoreColor.g, galaxyCoreColor.b, 0.5f)));
        glowMat = UnlitMat(glowTex, Color.white);   // (kept for the arcade's nebulae / glows)

        spaceCam = MakeCamera("Space Camera", space, spaceTexture, 60f, Color.black);
        spaceCam.transform.localRotation = Quaternion.identity;   // (looks along +Z at the galaxy's plane)
    }

    private Transform MakeSphere(string name, Transform parent, Vector3 pos, float radius, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * radius * 2f;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    private static Texture2D NewTexture(int w, int h, TextureWrapMode wrap) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear };

    /// <summary>An equirect Earth: fbm noise decides land / sea, latitude adds ice caps and dry bands, coasts get a lighter shelf.</summary>
    private Texture2D MakeEarthTexture()
    {
        const int w = 256, h = 128;
        Texture2D tex = NewTexture(w, h, TextureWrapMode.Repeat);
        Color[] px = new Color[w * h];
        Color shelf = Color.Lerp(oceanColor, new Color(0.2f, 0.6f, 0.75f), 0.6f);
        for (int y = 0; y < h; y++)
        {
            float lat = Mathf.Abs(y / (h - 1f) - 0.5f) * 2f;   // 0 at the equator, 1 at the poles
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float n = PixelNoise.Fbm(u, v, 4, 5, 911);
                Color c;
                if (lat > 0.86f - (n - 0.5f) * 0.2f) c = iceColor;
                else if (n > 0.53f)
                {
                    float dry = Mathf.Clamp01((0.35f - Mathf.Abs(lat - 0.35f)) * 2.5f) * PixelNoise.Fbm(u, v, 8, 3, 77);
                    c = Color.Lerp(landColor, desertColor, dry);
                    c = Color.Lerp(c, c * 0.7f, Mathf.Clamp01((n - 0.62f) * 6f));   // (darker highlands inland)
                    if (n > 0.66f && lat > 0.55f) c = Color.Lerp(c, iceColor, 0.6f);   // (snowy peaks towards the poles)
                }
                else c = Color.Lerp(Color.Lerp(oceanColor * 0.7f, oceanColor, n / 0.53f), shelf, Mathf.Clamp01((n - 0.49f) * 25f));
                c.a = 1f;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private Texture2D MakeCloudTexture()
    {
        const int w = 256, h = 128;
        Texture2D tex = NewTexture(w, h, TextureWrapMode.Repeat);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = PixelNoise.Fbm(x / (float)w, y / (float)h, 6, 4, 4242);
                float a = Mathf.Clamp01((n - 0.55f) * 4f) * cloudColor.a;
                px[y * w + x] = new Color(cloudColor.r, cloudColor.g, cloudColor.b, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>Earth's night side: a disc, clear on the left (the sun's side), shading softly to dark on the right, with a darker rim all round.</summary>
    private static Texture2D MakeNightTexture()
    {
        const int n = 128;
        Texture2D tex = NewTexture(n, n, TextureWrapMode.Clamp);
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - (n - 1) * 0.5f) / (n * 0.5f), dy = (y - (n - 1) * 0.5f) / (n * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.94f;   // (the disc fills 94% of the quad: the quad is a touch bigger than Earth)
                float night = Mathf.Clamp01((dx + 0.15f) / 0.55f);
                float rim = Mathf.Clamp01((d - 0.75f) / 0.25f) * 0.45f;
                float edge = Mathf.Clamp01((1f - d) / 0.04f);   // (a soft outline)
                float a = Mathf.Clamp01(Mathf.Max(night * night * (3f - 2f * night), rim)) * edge;
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>The Moon: grey noise with crater rings.</summary>
    private static Texture2D MakeMoonTexture()
    {
        const int w = 128, h = 64;
        Texture2D tex = NewTexture(w, h, TextureWrapMode.Repeat);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                float n = PixelNoise.Fbm(u, v, 6, 4, 3131);
                PixelNoise.Voronoi(u, v, 9, 77, out float f1, out _, out _);
                float crater = Mathf.Clamp01(1f - Mathf.Abs(f1 * 9f - 0.35f) * 8f) * 0.25f - Mathf.Clamp01(0.3f - f1 * 9f) * 0.35f;
                float g = Mathf.Clamp01(0.55f + (n - 0.5f) * 0.8f + crater);
                px[y * w + x] = new Color(g, g, g, 1f);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>A soft cloudy blob for nebulae: noise, faded out towards the edges.</summary>
    private static Texture2D MakeNebulaTexture()
    {
        const int n = 128;
        Texture2D tex = NewTexture(n, n, TextureWrapMode.Clamp);
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                float f = PixelNoise.Fbm(u, v, 3, 5, 6060);
                float a = Mathf.Clamp01((f - 0.35f) * 2.2f) * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>A soft round glow (white, alpha falling off from the middle; 'core' = how much of the radius is the bright part).</summary>
    private static Texture2D MakeRadialTexture(int n, float core)
    {
        Texture2D tex = NewTexture(n, n, TextureWrapMode.Clamp);
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - (n - 1) * 0.5f, y - (n - 1) * 0.5f).magnitude / (n * 0.5f);
                float a = d < core ? 1f - d / core * 0.35f : Mathf.Clamp01(1f - (d - core) / (1f - core)) * 0.65f;
                a = Mathf.Clamp01(a * (1f - d * d * 0.3f));
                px[y * n + x] = new Color(1f, 1f, 1f, d >= 1f ? 0f : a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>One mesh of little glowing quads facing -Z (stars), one per point.</summary>
    private static Mesh MakeDotMesh(string name, Vector3[] pos, float[] size, Color[] col)
    {
        int n = pos.Length;
        Vector3[] v = new Vector3[n * 4];
        Vector2[] uv = new Vector2[n * 4];
        Color[] c = new Color[n * 4];
        int[] tri = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            float s = size[i] * 0.5f;
            Vector3 p = pos[i];
            int b = i * 4;
            v[b] = p + new Vector3(-s, -s, 0f); v[b + 1] = p + new Vector3(-s, s, 0f); v[b + 2] = p + new Vector3(s, s, 0f); v[b + 3] = p + new Vector3(s, -s, 0f);
            uv[b] = new Vector2(0f, 0f); uv[b + 1] = new Vector2(0f, 1f); uv[b + 2] = new Vector2(1f, 1f); uv[b + 3] = new Vector2(1f, 0f);
            c[b] = c[b + 1] = c[b + 2] = c[b + 3] = col[i];
            int ti = i * 6;
            tri[ti] = b; tri[ti + 1] = b + 1; tri[ti + 2] = b + 2; tri[ti + 3] = b; tri[ti + 4] = b + 2; tri[ti + 5] = b + 3;
        }
        Mesh m = new Mesh { name = name };
        if (n * 4 > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = v; m.uv = uv; m.colors = c; m.triangles = tri;
        m.RecalculateBounds();
        return m;
    }

    private Mesh MakeGalaxyMesh()
    {
        int n = galaxyStars;
        Vector3[] pos = new Vector3[n];
        float[] sizes = new float[n];
        Color[] cols = new Color[n];
        System.Random rng = new System.Random(2024);
        float rnd() => (float)rng.NextDouble();
        float R = 60f;
        for (int i = 0; i < n; i++)
        {
            Vector3 p;
            float size;
            Color c;
            if (i < n / 6)
            {
                // The core: a dense warm ball.
                float rr = Mathf.Pow(rnd(), 2f) * R * 0.18f, ang = rnd() * Mathf.PI * 2f;
                p = new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr, (rnd() - 0.5f) * 2f);
                size = 0.5f + rnd() * 0.9f;
                c = Color.Lerp(galaxyCoreColor, Color.white, rnd() * 0.5f);
            }
            else
            {
                // Two logarithmic arms with scatter; every 23rd star is a bright pink star-forming clump.
                float k = rnd(), arm = (i & 1) * Mathf.PI;
                float rr = R * (0.12f + 0.88f * k);
                float ang = arm + k * 3.4f * Mathf.PI + (rnd() - 0.5f) * 0.55f;
                p = new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr, 0f) + new Vector3(rnd() - 0.5f, rnd() - 0.5f, 0f) * (2f + 6f * k);
                p.z = (rnd() - 0.5f) * 1.2f;
                size = 0.25f + rnd() * 0.6f;
                c = Color.Lerp(galaxyArmColor, galaxyCoreColor, (1f - k) * 0.6f);
                c = Color.Lerp(c, Color.white, rnd() * 0.3f);
                if (i % 23 == 0) { c = Color.Lerp(galaxyClumpColor, Color.white, rnd() * 0.25f); size *= 2.2f; }
            }
            if ((p - earthPos).sqrMagnitude < 4f) p += (p - earthPos).normalized * 2.5f + Vector3.right * 2.5f;   // (no star right on top of Earth)
            c.a = 0.55f + rnd() * 0.45f;
            pos[i] = p; sizes[i] = size; cols[i] = c;
        }
        return MakeDotMesh("Studio Intro Galaxy", pos, sizes, cols);
    }

    private void BuildArcade(TMP_FontAsset font)
    {
        Transform arcade = new GameObject("Arcade").transform;
        arcade.SetParent(world.transform, false);
        // Well below the space set (space-local y -90: past the galaxy's edge and in front of its far nebulae / stars). It used to sit at x 2000 =
        // inside the scaled-up galaxy, whose huge glowing star sprites drifted behind the cabinet as a white sheen.
        arcade.localPosition = new Vector3(0f, -9000f, 0f);
        Vector3 o = arcade.localPosition;
        Color dark = new Color(0.04f, 0.04f, 0.06f, 1f);

        // The cabinet: a tall box, pink side trims, a dark bezel round a 16:9 screen, an angled control panel, a coin door and a lit marquee.
        kit.Box("Cabinet", arcade, new Vector3(0f, 0.95f, 0.25f), new Vector3(1.0f, 1.9f, 0.8f), cabinetColor);
        kit.Box("Trim L", arcade, new Vector3(-0.505f, 0.95f, 0.25f), new Vector3(0.03f, 1.9f, 0.82f), cabinetTrimColor, default, false, 0.2f, 0.5f, cabinetTrimColor * 0.6f);
        kit.Box("Trim R", arcade, new Vector3(0.505f, 0.95f, 0.25f), new Vector3(0.03f, 1.9f, 0.82f), cabinetTrimColor, default, false, 0.2f, 0.5f, cabinetTrimColor * 0.6f);
        kit.Box("Top Cap", arcade, new Vector3(0f, 1.91f, 0.25f), new Vector3(1.04f, 0.03f, 0.84f), cabinetTrimColor * 0.8f, default, false, 0.3f, 0.6f);
        kit.Box("Bezel", arcade, new Vector3(0f, 1.3f, -0.16f), new Vector3(0.9f, 0.56f, 0.02f), bezelColor);
        Vector3 screenLocal = new Vector3(0f, 1.3f, -0.172f);
        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(screen.GetComponent<Collider>());
        screen.name = "Screen";
        screen.transform.SetParent(arcade, false);
        screen.transform.localPosition = screenLocal;
        screen.transform.localScale = new Vector3(ScreenW, ScreenH, 1f);
        screen.GetComponent<Renderer>().sharedMaterial = UnlitMat(spaceTexture, Color.white);
        screenTransform = screen.transform;
        screenCentre = o + screenLocal;

        // A neon frame round the bezel.
        Color neonGlow = neonColor * 1.6f;
        kit.Box("Neon Top", arcade, new Vector3(0f, 1.588f, -0.176f), new Vector3(0.93f, 0.012f, 0.012f), neonColor, default, false, 0f, 0.5f, neonGlow);
        kit.Box("Neon Bottom", arcade, new Vector3(0f, 1.012f, -0.176f), new Vector3(0.93f, 0.012f, 0.012f), neonColor, default, false, 0f, 0.5f, neonGlow);
        kit.Box("Neon L", arcade, new Vector3(-0.459f, 1.3f, -0.176f), new Vector3(0.012f, 0.588f, 0.012f), neonColor, default, false, 0f, 0.5f, neonGlow);
        kit.Box("Neon R", arcade, new Vector3(0.459f, 1.3f, -0.176f), new Vector3(0.012f, 0.588f, 0.012f), neonColor, default, false, 0f, 0.5f, neonGlow);

        // Speaker grilles between the bezel and the marquee.
        for (int s = -1; s <= 1; s += 2)
        {
            kit.Box("Grille", arcade, new Vector3(s * 0.25f, 1.628f, -0.156f), new Vector3(0.3f, 0.06f, 0.012f), dark);
            for (int k = 0; k < 3; k++)
                kit.Box("Slat", arcade, new Vector3(s * 0.25f, 1.61f + k * 0.018f, -0.163f), new Vector3(0.27f, 0.006f, 0.004f), new Color(0.3f, 0.3f, 0.36f, 1f), default, false, 0.8f, 0.6f);
        }

        // The marquee and its ring of chasing bulbs.
        marqueeCentre = o + new Vector3(0f, 1.78f, -0.17f);
        kit.Box("Marquee", arcade, new Vector3(0f, 1.78f, -0.12f), new Vector3(0.96f, 0.22f, 0.1f), marqueeColor, default, false, 0f, 0.6f, marqueeColor * 1.4f);
        List<Renderer> bulbList = new List<Renderer>();
        for (int i = 0; i < 9; i++)
        {
            float x = -0.44f + i * 0.11f;
            bulbList.Add(kit.Box("Bulb", arcade, new Vector3(x, 1.896f, -0.176f), Vector3.one * 0.022f, bulbColor, default, false, 0f, 0.6f, bulbColor * 0.12f).GetComponent<Renderer>());
        }
        for (int i = 0; i < 2; i++)
            bulbList.Add(kit.Box("Bulb", arcade, new Vector3(0.475f, 1.78f + (i == 0 ? 0.06f : -0.06f), -0.176f), Vector3.one * 0.022f, bulbColor, default, false, 0f, 0.6f, bulbColor * 0.12f).GetComponent<Renderer>());
        for (int i = 8; i >= 0; i--)
        {
            float x = -0.44f + i * 0.11f;
            bulbList.Add(kit.Box("Bulb", arcade, new Vector3(x, 1.664f, -0.176f), Vector3.one * 0.022f, bulbColor, default, false, 0f, 0.6f, bulbColor * 0.12f).GetComponent<Renderer>());
        }
        for (int i = 0; i < 2; i++)
            bulbList.Add(kit.Box("Bulb", arcade, new Vector3(-0.475f, 1.78f + (i == 0 ? -0.06f : 0.06f), -0.176f), Vector3.one * 0.022f, bulbColor, default, false, 0f, 0.6f, bulbColor * 0.12f).GetComponent<Renderer>());
        bulbs = bulbList.ToArray();
        bulbOff = bulbs[0].sharedMaterial;
        bulbOn = kit.Mat(bulbColor, 0f, 0.6f, bulbColor * 2.2f);

        // The control panel: two players, a stick and three buttons each, 1P / 2P START in the middle.
        kit.Box("Panel", arcade, new Vector3(0f, 0.95f, -0.28f), new Vector3(0.96f, 0.08f, 0.38f), cabinetColor * 1.25f, new Vector3(-12f, 0f, 0f));
        kit.Box("Panel Edge", arcade, new Vector3(0f, 0.93f, -0.468f), new Vector3(0.98f, 0.05f, 0.02f), cabinetTrimColor, new Vector3(-12f, 0f, 0f), false, 0.2f, 0.5f, cabinetTrimColor * 0.5f);
        Color[] buttons = { buttonColorA, buttonColorB, buttonColorC };
        for (int p = 0; p < 2; p++)
        {
            float side = p == 0 ? -1f : 1f;
            kit.Cyl("Stick Base", arcade, new Vector3(side * 0.38f, 1.0f, -0.31f), 0.035f, 0.01f, dark);
            kit.Cyl("Stick", arcade, new Vector3(side * 0.38f, 1.04f, -0.31f), 0.01f, 0.09f, Color.black);
            kit.Ball("Stick Ball", arcade, new Vector3(side * 0.38f, 1.09f, -0.31f), 0.032f, p == 0 ? buttonColorA : buttonColorB);
            for (int b = 0; b < 3; b++)
                kit.Cyl("Button", arcade, new Vector3(side * (0.26f - b * 0.075f), 1.0f + (b == 1 ? 0.004f : 0f), -0.32f + (b == 1 ? 0.03f : 0f)), 0.026f, 0.03f, buttons[b]);
        }
        startOff = kit.Mat(StartWhite, 0.2f, 0.5f);
        startOn = kit.Mat(Color.white, 0.2f, 0.5f, new Color(1f, 1f, 0.8f) * 1.8f);
        startButton = kit.Cyl("1P Start", arcade, new Vector3(-0.035f, 1.012f, -0.22f), 0.018f, 0.02f, StartWhite).GetComponent<Renderer>();
        kit.Cyl("2P Start", arcade, new Vector3(0.035f, 1.012f, -0.22f), 0.018f, 0.02f, StartWhite);
        startButtonPos = o + new Vector3(-0.035f, 1.03f, -0.24f);

        // The coin door: two lit slots and coin returns.
        kit.Box("Coin Door", arcade, new Vector3(0f, 0.55f, -0.16f), new Vector3(0.32f, 0.3f, 0.02f), new Color(0.25f, 0.25f, 0.3f, 1f), default, false, 0.7f, 0.6f);
        Color slotRed = new Color(1f, 0.3f, 0.2f, 1f);
        for (int s = -1; s <= 1; s += 2)
        {
            kit.Box("Coin Slot Plate", arcade, new Vector3(s * 0.07f, 0.62f, -0.171f), new Vector3(0.08f, 0.07f, 0.004f), new Color(0.12f, 0.12f, 0.14f, 1f), default, false, 0.8f, 0.7f);
            kit.Box("Coin Slot", arcade, new Vector3(s * 0.07f, 0.62f, -0.174f), new Vector3(0.012f, 0.045f, 0.004f), slotRed, default, false, 0f, 0.5f, slotRed * 1.5f);
            kit.Box("Coin Return", arcade, new Vector3(s * 0.07f, 0.48f, -0.172f), new Vector3(0.05f, 0.035f, 0.01f), dark);
        }
        coinSlotPos = o + new Vector3(-0.07f, 0.62f, -0.2f);
        kit.Box("Kick Plate", arcade, new Vector3(0f, 0.06f, -0.156f), new Vector3(1.0f, 0.12f, 0.012f), new Color(0.2f, 0.2f, 0.24f, 1f), default, false, 0.85f, 0.7f);
        kit.Box("Kick Stripe", arcade, new Vector3(0f, 0.13f, -0.158f), new Vector3(1.0f, 0.012f, 0.012f), cabinetTrimColor, default, false, 0f, 0.5f, cabinetTrimColor);

        // Side art on the left side (the side the camera sees): neon stripes and a 3 x 3 pixel-cube emblem.
        Color[] stripe = { neonColor, cabinetTrimColor, marqueeColor };
        for (int i = 0; i < 3; i++)
            kit.Box("Side Stripe", arcade, new Vector3(-0.523f, 0.55f + i * 0.13f, 0.25f), new Vector3(0.006f, 0.035f, 0.95f), stripe[i], new Vector3(32f, 0f, 0f), false, 0f, 0.5f, stripe[i] * 0.9f);
        Color[] pixels = { buttonColorA, buttonColorC, buttonColorB, alienColor, Color.white, cabinetTrimColor, buttonColorB, neonColor, buttonColorA };
        for (int i = 0; i < 9; i++)
            kit.Box("Side Pixel", arcade, new Vector3(-0.523f, 1.42f + (i / 3 - 1) * 0.085f, 0.25f + (i % 3 - 1) * 0.085f), new Vector3(0.006f, 0.075f, 0.075f), pixels[i], default, false, 0f, 0.5f, pixels[i] * 0.6f);

        // The marquee's text: a small world-space canvas in front of the lit box.
        GameObject mc = new GameObject("Marquee Text", typeof(RectTransform), typeof(Canvas));
        mc.transform.SetParent(arcade, false);
        mc.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform mr = mc.GetComponent<RectTransform>();
        mr.sizeDelta = new Vector2(860f, 190f);
        mr.localScale = Vector3.one * 0.001f;
        mr.localPosition = new Vector3(0f, 1.78f, -0.172f);
        TMP_Text marquee = PixelUIKit.CreateText(font, mc.transform, "Name", studioName, 120f, TextAlignmentOptions.Center, FontStyles.Bold, marqueeTextColor);
        marquee.enableAutoSizing = true; marquee.fontSizeMax = 120f; marquee.fontSizeMin = 30f;
        marquee.raycastTarget = false;
        PixelUIKit.Stretch(marquee.rectTransform);

        // Lights: the marquee's warm glow, the screen's cool glow, a soft front light, coloured rim lights behind and the coin slot's flash.
        MakeLight(arcade, "Marquee Light", new Vector3(0f, 1.95f, -0.6f), marqueeColor, 1.6f, 3f);
        screenLight = MakeLight(arcade, "Screen Light", new Vector3(0f, 1.3f, -0.7f), new Color(0.45f, 0.55f, 1f), 1.2f, 3f);
        screenLightBase = screenLight.intensity;
        MakeLight(arcade, "Front Light", new Vector3(-1.5f, 1.6f, -2.5f), Color.white, 0.8f, 8f);
        MakeLight(arcade, "Rim Light Pink", new Vector3(0.9f, 1.9f, 1.3f), cabinetTrimColor, 2.2f, 4f);
        MakeLight(arcade, "Rim Light Cyan", new Vector3(-1.1f, 0.4f, 1.1f), neonColor, 1.6f, 4f);
        slotLight = MakeLight(arcade, "Slot Light", new Vector3(-0.07f, 0.62f, -0.35f), slotRed, 0f, 0.8f);

        BuildArcadeSky(arcade);
        BuildAlien(arcade);

        cabinetCam = MakeCamera("Cabinet Camera", world.transform, outTexture, CabinetFov, new Color(0.01f, 0.008f, 0.03f, 1f));
        cabinetCam.nearClipPlane = 0.01f;
        cabinetCam.farClipPlane = 40f;
    }

    /// <summary>Space round the floating cabinet: stars and nebulae behind it, and little pixel cubes tumbling round it.</summary>
    private void BuildArcadeSky(Transform arcade)
    {
        System.Random rng = new System.Random(8080);
        float R() => (float)rng.NextDouble();
        Vector3[] p = new Vector3[arcadeStars];
        float[] s = new float[arcadeStars];
        Color[] c = new Color[arcadeStars];
        for (int i = 0; i < arcadeStars; i++)
        {
            p[i] = new Vector3((R() - 0.5f) * 36f, (R() - 0.35f) * 22f, 7f + R() * 12f);
            s[i] = 0.02f + Mathf.Pow(R(), 4f) * 0.07f;
            c[i] = Color.Lerp(Color.white, R() < 0.5f ? new Color(0.7f, 0.8f, 1f) : new Color(1f, 0.8f, 0.9f), R() * 0.7f);
            c[i].a = 0.3f + R() * 0.7f;
        }
        Transform stars = new GameObject("Arcade Stars", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        stars.SetParent(arcade, false);
        stars.GetComponent<MeshFilter>().sharedMesh = Own(MakeDotMesh("Studio Intro Arcade Stars", p, s, c));
        stars.GetComponent<MeshRenderer>().sharedMaterial = UnlitMat(glowMat.mainTexture, Color.white);
        Texture nebulaTex = Own(MakeNebulaTexture());
        Quad("Nebula", arcade, new Vector3(3f, 2.5f, 12f), Vector3.one * 16f, UnlitMat(nebulaTex, nebulaColorA));
        Quad("Nebula", arcade, new Vector3(-5f, -1f, 10f), Vector3.one * 12f, UnlitMat(nebulaTex, nebulaColorB)).localRotation = Quaternion.Euler(0f, 0f, 120f);
        Quad("Nebula", arcade, new Vector3(-1f, 4f, 14f), Vector3.one * 14f, UnlitMat(nebulaTex, nebulaColorC)).localRotation = Quaternion.Euler(0f, 0f, 220f);

        // Tumbling pixel cubes: kept out of the camera's path (behind the cabinet or well off to the sides).
        Color[] palette = { buttonColorA, buttonColorB, buttonColorC, alienColor, Color.white, cabinetTrimColor, neonColor };
        cubes = new Transform[floatingCubes];
        cubeSpin = new Vector3[floatingCubes];
        cubeHome = new Vector3[floatingCubes];
        for (int i = 0; i < floatingCubes; i++)
        {
            bool behind = i % 3 != 0;
            Vector3 home = behind
                ? new Vector3((R() - 0.5f) * 5f, -0.3f + R() * 3.2f, 1.1f + R() * 4f)
                : new Vector3((R() < 0.5f ? -1f : 1f) * (1.8f + R() * 1.5f), -0.2f + R() * 2.8f, -1.5f + R() * 2f);
            if (!behind && home.x < 0f) home.x -= 1.2f;   // (the camera stands off to the left: keep cubes on that side further out)
            Color col = palette[i % palette.Length];
            cubes[i] = kit.Box("Pixel Cube", arcade, home, Vector3.one * (0.05f + R() * 0.1f), col, default, false, 0.1f, 0.5f, col * 0.35f).transform;
            cubeHome[i] = home;
            cubeSpin[i] = new Vector3((R() - 0.5f) * 90f, (R() - 0.5f) * 120f, (R() - 0.5f) * 90f);
        }
    }

    private static Light MakeLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        GameObject go = new GameObject(name, typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.GetComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        return l;
    }

    /// <summary>The little cube alien: a green cube body with big eyes, a mouth, two antennae with glowing tips, stubby legs and a hand that reaches for the cabinet.</summary>
    private void BuildAlien(Transform arcade)
    {
        alien = new GameObject("Alien").transform;
        alien.SetParent(arcade, false);
        alien.localPosition = alienPosition;
        alien.localRotation = Quaternion.Euler(0f, 25f, 0f);   // (turned a little towards the cabinet)
        alien.localScale = Vector3.one * alienSize;
        kit.Box("Leg L", alien, new Vector3(-0.08f, 0.06f, 0f), new Vector3(0.07f, 0.12f, 0.07f), alienColor * 0.8f);
        kit.Box("Leg R", alien, new Vector3(0.08f, 0.06f, 0f), new Vector3(0.07f, 0.12f, 0.07f), alienColor * 0.8f);
        kit.Box("Foot L", alien, new Vector3(-0.08f, 0.01f, 0.02f), new Vector3(0.09f, 0.025f, 0.11f), alienColor * 0.6f);
        kit.Box("Foot R", alien, new Vector3(0.08f, 0.01f, 0.02f), new Vector3(0.09f, 0.025f, 0.11f), alienColor * 0.6f);
        alienBody = new GameObject("Body Pivot").transform;
        alienBody.SetParent(alien, false);
        alienBody.localPosition = new Vector3(0f, 0.22f, 0f);
        kit.Box("Body", alienBody, new Vector3(0f, 0.15f, 0f), new Vector3(0.32f, 0.32f, 0.32f), alienColor);
        kit.Box("Belly Patch", alienBody, new Vector3(0f, 0.02f, 0.161f), new Vector3(0.16f, 0.04f, 0.004f), Color.Lerp(alienColor, Color.white, 0.4f));
        kit.Box("Back Spot", alienBody, new Vector3(0.06f, 0.2f, -0.161f), new Vector3(0.07f, 0.07f, 0.004f), alienColor * 0.75f);
        kit.Box("Back Spot", alienBody, new Vector3(-0.07f, 0.08f, -0.161f), new Vector3(0.05f, 0.05f, 0.004f), alienColor * 0.75f);
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -0.075f : 0.075f;
            Transform eye = kit.Ball("Eye", alienBody, new Vector3(x, 0.2f, 0.17f), 0.06f, Color.white).transform;
            pupils[i] = kit.Ball("Pupil", eye, new Vector3(0f, 0f, 0.42f), 0.25f, Color.black).transform;   // (inside the eye's own scale)
            kit.Ball("Shine", pupils[i], new Vector3(0.35f, 0.35f, 0.6f), 0.3f, Color.white);
            eyes[i] = eye;
        }
        eyeScale = eyes[0].localScale;
        pupilRest = pupils[0].localPosition;
        Color mouth = new Color(0.08f, 0.2f, 0.08f, 1f);
        kit.Box("Mouth", alienBody, new Vector3(0f, 0.07f, 0.162f), new Vector3(0.05f, 0.014f, 0.006f), mouth);
        mouthL = kit.Box("Mouth L", alienBody, new Vector3(-0.045f, 0.07f, 0.162f), new Vector3(0.045f, 0.014f, 0.006f), mouth).transform;
        mouthR = kit.Box("Mouth R", alienBody, new Vector3(0.045f, 0.07f, 0.162f), new Vector3(0.045f, 0.014f, 0.006f), mouth).transform;
        antennaL = Antenna("Antenna L", -0.09f, 0);
        antennaR = Antenna("Antenna R", 0.09f, 1);
        tipScale = tips[0].localScale;
        armR = kit.Box("Arm", alien, new Vector3(0.2f, 0.4f, 0.12f), new Vector3(0.06f, 0.06f, 0.06f), alienColor * 0.9f).transform;
        // The arm is a little hand that slides to where it taps (positions kept relative to the intro's world); the body stays put.
        alienArmRest = world.transform.InverseTransformPoint(armR.position);

        coin = kit.Cyl("Coin", world.transform, alienArmRest, 0.022f, 0.006f, coinColor, default, 0.9f, 0.8f).transform;
        coin.gameObject.SetActive(false);
    }

    private Transform Antenna(string name, float x, int index)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(alienBody, false);
        pivot.localPosition = new Vector3(x, 0.31f, 0f);
        kit.Cyl("Stalk", pivot, new Vector3(0f, 0.07f, 0f), 0.008f, 0.14f, alienColor * 0.7f);
        Color tip = new Color(0.8f, 1f, 0.4f, 1f);
        tips[index] = kit.Box("Tip", pivot, new Vector3(0f, 0.15f, 0f), Vector3.one * 0.045f, tip, new Vector3(45f, 45f, 0f), false, 0f, 0.6f, tip * 1.3f).transform;
        return pivot;
    }

    private TMP_Text ScreenText(TMP_FontAsset font, Transform parent, string name, string text, float size, Vector2 anchor, float y, Color color)
    {
        TMP_Text label = PixelUIKit.CreateText(font, parent, name, text, size, TextAlignmentOptions.Center, FontStyles.Bold, color);
        label.raycastTarget = false;
        RectTransform r = label.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = new Vector2(1200f, size * 1.4f);
        r.anchoredPosition = new Vector2(0f, y);
        return label;
    }

    private RawImage Overlay(Transform parent, string name, Texture tex, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        RawImage img = go.GetComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        PixelUIKit.Stretch(img.rectTransform);
        return img;
    }

    private void BuildCanvas(PixelClicker clicker)
    {
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        canvasGo = PixelUIKit.CreateCanvas("Studio Intro", sortingOrder, new Vector2(1920f, 1080f), true);
        group = canvasGo.AddComponent<CanvasGroup>();

        // The cabinet camera's picture over the whole screen (it blocks clicks to whatever is behind).
        RawImage view = Overlay(canvasGo.transform, "View", outTexture, Color.white);
        view.raycastTarget = true;

        presentsLabel = ScreenText(font, canvasGo.transform, "Presents", presentsText, 46f, new Vector2(0.5f, 0f), 120f, new Color(1f, 1f, 1f, 0f));
        presentsLabel.fontStyle = FontStyles.Italic;

        GameObject fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeGo.transform.SetParent(canvasGo.transform, false);
        fade = fadeGo.GetComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
        PixelUIKit.Stretch(fade.rectTransform);

        // GAME OVER, the score line, INSERT COIN, scanlines, a CRT vignette, glass glare and the START flash: a world-space canvas laid just in
        // front of the arcade screen (the cabinet camera starts framed on the screen, so it reads as plain space with text over it, then pulls back with it).
        GameObject sc = new GameObject("Screen Overlay", typeof(RectTransform), typeof(Canvas));
        sc.transform.SetParent(screenTransform.parent, false);
        sc.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform scr = sc.GetComponent<RectTransform>();
        scr.sizeDelta = new Vector2(1280f, 720f);
        scr.localScale = Vector3.one * (ScreenW / 1280f);
        scr.localPosition = screenTransform.localPosition + new Vector3(0f, 0f, -0.003f);

        splitLabel = ScreenText(font, sc.transform, "Game Over Split", gameOverText, 150f, new Vector2(0.5f, 0.5f), 0f, gameOverSplitColor);
        gameOverLabel = ScreenText(font, sc.transform, "Game Over", gameOverText, 150f, new Vector2(0.5f, 0.5f), 0f, gameOverColor);
        splitLabel.characterSpacing = gameOverLabel.characterSpacing = 8f;
        scoreLabel = ScreenText(font, sc.transform, "Score", scoreText, 42f, new Vector2(0.5f, 1f), -30f, screenTextColor);
        bottomLabel = ScreenText(font, sc.transform, "Insert Coin", insertCoinText, 52f, new Vector2(0.5f, 0f), 50f, screenTextColor);

        Texture2D scanTex = Own(new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point });
        scanTex.SetPixels(new[] { new Color(0f, 0f, 0f, 0.9f), new Color(0f, 0f, 0f, 0.9f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f) });
        scanTex.Apply();
        scanlines = Overlay(sc.transform, "Scanlines", scanTex, Color.white);
        Overlay(sc.transform, "Vignette", Own(MakeVignetteTexture()), Color.white);
        Overlay(sc.transform, "Glare", Own(MakeGlareTexture()), Color.white);
        GameObject flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flashGo.transform.SetParent(sc.transform, false);
        flash = flashGo.GetComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;
        PixelUIKit.Stretch(flash.rectTransform);
    }

    /// <summary>A CRT tube's dark rounded corners.</summary>
    private static Texture2D MakeVignetteTexture()
    {
        const int w = 128, h = 72;
        Texture2D tex = NewTexture(w, h, TextureWrapMode.Clamp);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x / (w - 1f) - 0.5f) * 2f, dy = Mathf.Abs(y / (h - 1f) - 0.5f) * 2f;
                float d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f);   // (a rounded rectangle)
                px[y * w + x] = new Color(0f, 0f, 0f, Mathf.Clamp01((d - 0.7f) / 0.3f) * 0.85f);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>A faint diagonal reflection on the screen's glass.</summary>
    private static Texture2D MakeGlareTexture()
    {
        const int w = 128, h = 72;
        Texture2D tex = NewTexture(w, h, TextureWrapMode.Clamp);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = y / (h - 1f);
                float band = Mathf.Clamp01(1f - Mathf.Abs(u + v * 0.6f - 0.55f) / 0.12f);
                float top = Mathf.Clamp01((v - 0.55f) / 0.45f) * 0.06f;
                px[y * w + x] = new Color(1f, 1f, 1f, band * 0.07f + top);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------
    // Tear-down: everything it built goes
    // ------------------------------------------------------------------

    private void DestroyWorld()
    {
        if (spaceCam != null) spaceCam.targetTexture = null;
        if (cabinetCam != null) cabinetCam.targetTexture = null;
        if (world != null) Destroy(world);
        world = null;
    }

    private void TearDown()
    {
        PixelAudio.StopLoop("studio_hum");
        DestroyWorld();
        if (canvasGo != null) Destroy(canvasGo);
        canvasGo = null;
        if (spaceTexture != null) { spaceTexture.Release(); Destroy(spaceTexture); spaceTexture = null; }
        if (outTexture != null) { outTexture.Release(); Destroy(outTexture); outTexture = null; }
        foreach (Object o in owned) if (o != null) Destroy(o);
        owned.Clear();
        glowMat = streakMat = null;
        if (kit != null) { kit.DestroyMaterials(); kit = null; }
    }
}
