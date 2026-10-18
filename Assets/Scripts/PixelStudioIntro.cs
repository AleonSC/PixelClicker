// ---- System notes ----
// PixelStudioIntro.cs
//   The GALACTIC ARCADE studio intro (design agreed in the Design Discussion chat). Plays ONCE per launch, before the title screen's launch intro
//   (PixelTitleIntro.IntroRoutine starts with `yield return PixelStudioIntro.PlayAtLaunch()`), ~7-8 s; any key / click / Esc skips to the fade.
//   Settings > Display > 'Studio intro' turns it off (PixelPrefs PixelClicker.Setting.StudioIntro, on this machine). Dev Hub > Events > 'Studio intro'
//   replays it (PixelStudioIntro.Replay) over whatever is on screen.
//   Everything is built in code when it starts and DESTROYED when it ends (objects, meshes, textures, materials, render textures):
//   * Its own little world far below the game (worldOffset), filmed by its own two cameras into RenderTextures, shown on its own top canvas, so it never
//     touches the game scene. The SPACE camera films Earth + the galaxy into a
//     16:9 texture that is the ARCADE CABINET's screen; the CABINET camera starts framed exactly on that screen (so it looks like plain space) and
//     pulls back for the reveal. The canvas shows the cabinet camera's picture.
//   STORYBOARD (all lengths Inspector values): 1 Earth close-up (a turning sphere with a code-drawn equirect texture: oceans, continents, ice caps,
//   clouds; a soft blue atmosphere glow behind it); 2 a fast exponential pull-back (studio_whoosh) - Earth shrinks to a dot and a spiral galaxy
//   (galaxyStars glowing points in one mesh, slowly turning) fills the view; 3 'GAME OVER' blinks over it with scanlines (studio_gameover jingle);
//   4 the cabinet camera pulls back: the galaxy was on the screen of an arcade cabinet (boxes / cylinders via PixelFactoryKit, retro colours) with a
//   lit marquee reading studioName (studio_hum loop); a little green cube ALIEN (antennae, big eyes) stands at it slumped; 5 he perks up and taps
//   the coin slot and the buttons; 6 fade to black, then the normal title intro.
//   'GAME OVER' + scanlines are a world-space canvas just in front of the cabinet's screen (not in the space picture). The cabinet and the alien
//   float in the dark (no floor).
//   All music waits while it plays (PixelAudio checks HoldsMusic: playing, or still due this launch with an 8 s time-out; the title screen calls
//   SkipThisLaunch when its launch intro won't run).
//   At launch it first waits on black until frames come smoothly (maxSettleTime), and the timeline moves at most maxFrameStep per frame, so loading
//   hitches slow it down instead of skipping scenes. The space set is built small and scaled up (SpaceScale) to keep float precision far from the origin.
//   No per-frame allocations (everything is cached when it is built; the timeline runs in one coroutine on unscaled time).
// ---------------------------------------------------
using System.Collections;
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

    [Header("Timing (seconds)")]
    [SerializeField, Min(0.1f), Tooltip("Earth close-up.")] private float earthSeconds = 1.5f;
    [SerializeField, Min(0.1f), Tooltip("The fast pull-back from Earth to the whole galaxy.")] private float zoomSeconds = 2f;
    [SerializeField, Min(0.1f), Tooltip("'GAME OVER' blinking over the galaxy.")] private float gameOverSeconds = 1.1f;
    [SerializeField, Min(0.1f), Tooltip("The pull-back that reveals the arcade cabinet.")] private float revealSeconds = 2f;
    [SerializeField, Min(0.1f), Tooltip("The alien perks up and taps the coin slot and the buttons.")] private float alienSeconds = 1.3f;
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
    [SerializeField] private Color galaxyCoreColor = new Color(1f, 0.88f, 0.65f, 1f);
    [SerializeField] private Color galaxyArmColor = new Color(0.55f, 0.65f, 1f, 1f);
    [SerializeField] private Color gameOverColor = new Color(1f, 0.25f, 0.3f, 1f);
    [SerializeField, Range(500, 6000), Tooltip("Glowing points in the galaxy.")] private int galaxyStars = 3000;

    [Header("Arcade colours")]
    [SerializeField] private Color cabinetColor = new Color(0.16f, 0.1f, 0.35f, 1f);
    [SerializeField] private Color cabinetTrimColor = new Color(1f, 0.3f, 0.75f, 1f);
    [SerializeField] private Color marqueeColor = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color marqueeTextColor = new Color(0.12f, 0.05f, 0.25f, 1f);
    [SerializeField] private Color bezelColor = new Color(0.05f, 0.05f, 0.07f, 1f);
    [SerializeField, Range(0.5f, 3f), Tooltip("Size of the alien (1 = a small cube a third of the cabinet's height).")] private float alienSize = 1.75f;
    [SerializeField, Tooltip("Where the alien stands, relative to the cabinet's foot (x = left / right, z = in front; the control panel ends at z -0.47).")] private Vector3 alienPosition = new Vector3(-0.3f, 0f, -0.75f);
    [SerializeField] private Color alienColor = new Color(0.35f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color buttonColorA = new Color(1f, 0.2f, 0.25f, 1f);
    [SerializeField] private Color buttonColorB = new Color(0.2f, 0.6f, 1f, 1f);

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
    public static bool HoldsMusic => Playing || (instance != null && !playedThisLaunch && Enabled && Time.realtimeSinceStartup < 8f);

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
    private Transform earth, clouds, galaxy, alien, alienBody, antennaL, antennaR, armR;
    private Transform screenTransform;
    private Vector3 earthPos, galaxyCentre, screenCentre, alienArmRest, coinSlotPos, buttonPos;
    private Texture2D earthTex, cloudTex, glowTex, dotTex, scanTex;
    private Mesh galaxyMesh;
    private Material earthMat, cloudMat, glowMat, galaxyMat, screenMat;
    private TMP_Text gameOverLabel;
    private RawImage scanlines;
    private Image fade;
    private CanvasGroup group;
    private Vector3 eyeScale;
    private Transform[] eyes = new Transform[2];

    private static Shader UnlitShader()
    {
        Shader s = Shader.Find("Sprites/Default");   // (always in a build; unlit, alpha, vertex colour)
        return s != null ? s : Shader.Find("Unlit/Transparent");
    }

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

        float t = 0f;
        float zoomStart = earthSeconds, overStart = zoomStart + zoomSeconds, revealStart = overStart + gameOverSeconds;
        float alienStart = revealStart + revealSeconds, end = alienStart + alienSeconds;
        bool whoosh = false, jingle = false, hum = false, skipped = false;

        while (t < end)
        {
            if (PixelInput.AnyInputPressed()) { skipped = true; break; }
            if (!whoosh && t >= zoomStart) { whoosh = true; PixelAudio.Play("studio_whoosh"); }
            if (!jingle && t >= overStart) { jingle = true; PixelAudio.Play("studio_gameover"); }
            if (!hum && t >= revealStart) { hum = true; PixelAudio.StartLoop("studio_hum"); }
            Pose(t, zoomStart, overStart, revealStart, alienStart);
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
            if (!skipped) Pose(Mathf.Min(t + f, end), zoomStart, overStart, revealStart, alienStart);
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

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    private void Pose(float t, float zoomStart, float overStart, float revealStart, float alienStart)
    {
        // Space: Earth turns, the galaxy turns slowly, the space camera pulls back exponentially from Earth to the whole galaxy.
        earth.localRotation = Quaternion.Euler(-23f, -t * 30f, 0f);
        clouds.localRotation = Quaternion.Euler(-23f, -t * 38f, 0f);
        galaxy.localRotation = Quaternion.Euler(0f, 0f, t * 4f);
        float z = Smooth((t - zoomStart) / zoomSeconds);
        float ez = z * z;   // (slow to start, then fast: a pull-back)
        float dist = 0.075f * Mathf.Pow(150f / 0.075f, ez);
        Vector3 look = Vector3.Lerp(earthPos, galaxyCentre, Smooth((t - zoomStart) / zoomSeconds * 1.2f - 0.2f));
        spaceCam.transform.localPosition = look + new Vector3(0f, 0f, -dist);
        spaceCam.nearClipPlane = Mathf.Max(0.004f, dist * 0.05f) * SpaceScale;   // (clip planes are in world units: the space set is scaled up)
        spaceCam.farClipPlane = (dist + 200f) * SpaceScale;

        // GAME OVER: blinks while it is new, then stays (dimmer) through the reveal; scanlines scroll.
        bool over = t >= overStart;
        float blink = t < revealStart ? (Mathf.Repeat((t - overStart) * blinkRate, 1f) < 0.6f ? 1f : 0.15f) : 0.85f;
        gameOverLabel.color = new Color(gameOverColor.r, gameOverColor.g, gameOverColor.b, over ? blink : 0f);
        scanlines.color = new Color(1f, 1f, 1f, over ? 0.35f : 0.12f);
        scanlines.uvRect = new Rect(0f, t * 0.6f, 1f, 90f);

        // The cabinet camera: framed exactly on the screen until the reveal, then pulls back to show the cabinet and the alien.
        float r = Smooth((t - revealStart) / revealSeconds);
        Vector3 startPos = screenCentre + screenTransform.forward * -ScreenFillDistance;
        Vector3 endPos = screenCentre + new Vector3(-1.15f, 0.05f, -2.8f);
        cabinetCam.transform.localPosition = Vector3.Lerp(startPos, endPos, r);
        Vector3 aim = Vector3.Lerp(screenCentre, screenCentre + new Vector3(-0.35f, -0.35f, 0f), r);
        cabinetCam.transform.localRotation = Quaternion.LookRotation(aim - cabinetCam.transform.localPosition, Vector3.up);

        // The alien: slumped from the game over (head down, antennae drooping, eyes half shut), then perks up and taps the coin slot, then the buttons.
        float perk = Smooth((t - alienStart) / (alienSeconds * 0.3f));
        float slump = over ? 1f - perk : 0f;
        alienBody.localRotation = Quaternion.Euler(22f * slump, 0f, 0f);
        alienBody.localPosition = new Vector3(0f, 0.22f - 0.04f * slump + 0.02f * Mathf.Sin(t * 6f) * perk, 0f);
        antennaL.localRotation = Quaternion.Euler(0f, 0f, 18f + 50f * slump);
        antennaR.localRotation = Quaternion.Euler(0f, 0f, -18f - 50f * slump);
        for (int i = 0; i < 2; i++) eyes[i].localScale = new Vector3(eyeScale.x, eyeScale.y * Mathf.Lerp(1f, 0.4f, slump), eyeScale.z);
        float a = (t - alienStart) / alienSeconds;
        Vector3 hand = alienArmRest;
        if (a > 0.3f && a < 0.62f) hand = Vector3.Lerp(alienArmRest, coinSlotPos, Mathf.Sin(Mathf.Clamp01((a - 0.3f) / 0.32f) * Mathf.PI));   // tap the coin slot
        else if (a >= 0.62f && a < 1f) hand = Vector3.Lerp(alienArmRest, buttonPos, Mathf.Abs(Mathf.Sin((a - 0.62f) / 0.38f * Mathf.PI * 2f)));   // tap the buttons twice
        armR.localPosition = alien.InverseTransformPoint(world.transform.TransformPoint(hand));
    }

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

        int w = Mathf.Max(640, Screen.width), h = Mathf.Max(360, Screen.height);
        spaceTexture = new RenderTexture(1280, 720, 24) { name = "Studio Intro Space" };
        outTexture = new RenderTexture(w, h, 24) { name = "Studio Intro Out" };

        BuildSpace(clicker);
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

    private void BuildSpace(PixelClicker clicker)
    {
        Transform space = new GameObject("Space").transform;
        space.SetParent(world.transform, false);
        space.localScale = Vector3.one * SpaceScale;   // (the numbers below are small; scaled up so a tiny Earth this far from the origin keeps its float precision)
        galaxyCentre = Vector3.zero;
        earthPos = new Vector3(22f, -9f, 0f);   // on an outer arm

        // Earth: a turning sphere with a code-drawn texture, a cloud shell and a soft atmosphere glow behind it.
        float er = 0.02f;
        earthTex = MakeEarthTexture();
        cloudTex = MakeCloudTexture();
        earthMat = new Material(UnlitShader()) { mainTexture = earthTex };
        cloudMat = new Material(UnlitShader()) { mainTexture = cloudTex };
        earth = MakeSphere("Earth", space, earthPos, er, earthMat);
        clouds = MakeSphere("Clouds", space, earthPos, er * 1.025f, cloudMat);
        glowTex = MakeRadialTexture(64, 0.62f);
        glowMat = new Material(UnlitShader()) { mainTexture = glowTex, color = atmosphereColor };
        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(glow.GetComponent<Collider>());
        glow.name = "Atmosphere";
        glow.transform.SetParent(space, false);
        glow.transform.localPosition = earthPos + new Vector3(0f, 0f, er * 1.5f);   // (just behind the Earth)
        glow.transform.localScale = Vector3.one * er * 2.7f;
        glow.GetComponent<Renderer>().sharedMaterial = glowMat;

        // The galaxy: thousands of glowing points on two spiral arms + a bright core, one mesh of little quads.
        galaxy = new GameObject("Galaxy", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        galaxy.SetParent(space, false);
        galaxy.localPosition = galaxyCentre;
        galaxyMesh = MakeGalaxyMesh();
        galaxy.GetComponent<MeshFilter>().sharedMesh = galaxyMesh;
        dotTex = MakeRadialTexture(32, 0.9f);
        galaxyMat = new Material(UnlitShader()) { mainTexture = dotTex };
        galaxy.GetComponent<MeshRenderer>().sharedMaterial = galaxyMat;

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

    /// <summary>An equirect Earth: fbm noise decides land / sea, latitude adds ice caps and dry bands.</summary>
    private Texture2D MakeEarthTexture()
    {
        const int w = 256, h = 128;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float lat = Mathf.Abs(y / (h - 1f) - 0.5f) * 2f;   // 0 at the equator, 1 at the poles
            for (int x = 0; x < w; x++)
            {
                float n = PixelNoise.Fbm(x / (float)w, y / (float)h, 4, 5, 911);
                Color c;
                if (lat > 0.86f) c = iceColor;
                else if (n > 0.53f) c = Color.Lerp(landColor, desertColor, Mathf.Clamp01((0.35f - Mathf.Abs(lat - 0.35f)) * 2.5f) * PixelNoise.Fbm(x / (float)w, y / (float)h, 8, 3, 77));
                else c = Color.Lerp(oceanColor * 0.75f, oceanColor, n / 0.53f);
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
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
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

    /// <summary>A soft round glow (white, alpha falling off from the middle; 'core' = how much of the radius is the bright part).</summary>
    private static Texture2D MakeRadialTexture(int n, float core)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
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

    private Mesh MakeGalaxyMesh()
    {
        int n = galaxyStars;
        Vector3[] v = new Vector3[n * 4];
        Vector2[] uv = new Vector2[n * 4];
        Color[] col = new Color[n * 4];
        int[] tri = new int[n * 6];
        System.Random rng = new System.Random(2024);
        float R = 60f;
        for (int i = 0; i < n; i++)
        {
            float rnd() => (float)rng.NextDouble();
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
                // Two logarithmic arms with scatter.
                float k = rnd(), arm = (i & 1) * Mathf.PI;
                float rr = R * (0.12f + 0.88f * k);
                float ang = arm + k * 3.4f * Mathf.PI + (rnd() - 0.5f) * 0.55f;
                p = new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr, 0f) + new Vector3(rnd() - 0.5f, rnd() - 0.5f, 0f) * (2f + 6f * k);
                p.z = (rnd() - 0.5f) * 1.2f;
                size = 0.25f + rnd() * 0.6f;
                c = Color.Lerp(galaxyArmColor, galaxyCoreColor, (1f - k) * 0.6f);
                c = Color.Lerp(c, Color.white, rnd() * 0.3f);
            }
            if ((p - earthPos).sqrMagnitude < 4f) p += (p - earthPos).normalized * 2.5f + Vector3.right * 2.5f;   // (no star right on top of Earth)
            c.a = 0.55f + rnd() * 0.45f;
            float s = size * 0.5f;
            int b = i * 4;
            v[b] = p + new Vector3(-s, -s, 0f); v[b + 1] = p + new Vector3(-s, s, 0f); v[b + 2] = p + new Vector3(s, s, 0f); v[b + 3] = p + new Vector3(s, -s, 0f);
            uv[b] = new Vector2(0f, 0f); uv[b + 1] = new Vector2(0f, 1f); uv[b + 2] = new Vector2(1f, 1f); uv[b + 3] = new Vector2(1f, 0f);
            col[b] = col[b + 1] = col[b + 2] = col[b + 3] = c;
            int ti = i * 6;
            tri[ti] = b; tri[ti + 1] = b + 1; tri[ti + 2] = b + 2; tri[ti + 3] = b; tri[ti + 4] = b + 2; tri[ti + 5] = b + 3;
        }
        Mesh m = new Mesh { name = "Studio Intro Galaxy" };
        if (n * 4 > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.vertices = v; m.uv = uv; m.colors = col; m.triangles = tri;
        m.RecalculateBounds();
        return m;
    }

    private void BuildArcade(TMP_FontAsset font)
    {
        Transform arcade = new GameObject("Arcade").transform;
        arcade.SetParent(world.transform, false);
        arcade.localPosition = new Vector3(2000f, 0f, 0f);   // (far from the space set: neither camera sees the other set)
        Vector3 o = arcade.localPosition;

        // The cabinet: a tall box, pink side trims, a dark bezel round a 16:9 screen, an angled control panel, a coin door and a lit marquee.
        kit.Box("Cabinet", arcade, new Vector3(0f, 0.95f, 0.25f), new Vector3(1.0f, 1.9f, 0.8f), cabinetColor);
        kit.Box("Trim L", arcade, new Vector3(-0.505f, 0.95f, 0.25f), new Vector3(0.03f, 1.9f, 0.82f), cabinetTrimColor, default, false, 0.2f, 0.5f, cabinetTrimColor * 0.6f);
        kit.Box("Trim R", arcade, new Vector3(0.505f, 0.95f, 0.25f), new Vector3(0.03f, 1.9f, 0.82f), cabinetTrimColor, default, false, 0.2f, 0.5f, cabinetTrimColor * 0.6f);
        kit.Box("Bezel", arcade, new Vector3(0f, 1.3f, -0.16f), new Vector3(0.9f, 0.56f, 0.02f), bezelColor);
        Vector3 screenLocal = new Vector3(0f, 1.3f, -0.172f);
        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(screen.GetComponent<Collider>());
        screen.name = "Screen";
        screen.transform.SetParent(arcade, false);
        screen.transform.localPosition = screenLocal;
        screen.transform.localScale = new Vector3(ScreenW, ScreenH, 1f);
        screenMat = new Material(UnlitShader()) { mainTexture = spaceTexture };
        screen.GetComponent<Renderer>().sharedMaterial = screenMat;
        screenTransform = screen.transform;
        screenCentre = o + screenLocal;

        kit.Box("Marquee", arcade, new Vector3(0f, 1.78f, -0.12f), new Vector3(0.96f, 0.22f, 0.1f), marqueeColor, default, false, 0f, 0.6f, marqueeColor * 1.4f);
        kit.Box("Panel", arcade, new Vector3(0f, 0.95f, -0.28f), new Vector3(0.96f, 0.08f, 0.38f), cabinetColor * 1.25f, new Vector3(-12f, 0f, 0f));
        kit.Cyl("Stick", arcade, new Vector3(-0.22f, 1.03f, -0.3f), 0.012f, 0.1f, Color.black);
        kit.Ball("Stick Ball", arcade, new Vector3(-0.22f, 1.09f, -0.3f), 0.035f, buttonColorA);
        kit.Cyl("Button A", arcade, new Vector3(0.1f, 1.0f, -0.32f), 0.03f, 0.03f, buttonColorA);
        kit.Cyl("Button B", arcade, new Vector3(0.22f, 1.0f, -0.3f), 0.03f, 0.03f, buttonColorB);
        kit.Box("Coin Door", arcade, new Vector3(0f, 0.55f, -0.16f), new Vector3(0.32f, 0.3f, 0.02f), new Color(0.25f, 0.25f, 0.3f, 1f), default, false, 0.7f, 0.6f);
        kit.Box("Coin Slot", arcade, new Vector3(0f, 0.6f, -0.172f), new Vector3(0.06f, 0.012f, 0.01f), new Color(1f, 0.3f, 0.2f, 1f), default, false, 0f, 0.5f, new Color(1f, 0.3f, 0.2f, 1f) * 1.5f);
        coinSlotPos = o + new Vector3(0f, 0.6f, -0.2f);
        buttonPos = o + new Vector3(0.1f, 1.02f, -0.34f);

        // The marquee's text: a small world-space canvas in front of the lit box.
        GameObject mc = new GameObject("Marquee Text", typeof(RectTransform), typeof(Canvas));
        mc.transform.SetParent(arcade, false);
        Canvas marqueeCanvas = mc.GetComponent<Canvas>();
        marqueeCanvas.renderMode = RenderMode.WorldSpace;
        RectTransform mr = mc.GetComponent<RectTransform>();
        mr.sizeDelta = new Vector2(920f, 200f);
        mr.localScale = Vector3.one * 0.001f;
        mr.localPosition = new Vector3(0f, 1.78f, -0.172f);
        TMP_Text marquee = PixelUIKit.CreateText(font, mc.transform, "Name", studioName, 120f,
                                                 TextAlignmentOptions.Center, FontStyles.Bold, marqueeTextColor);
        marquee.enableAutoSizing = true; marquee.fontSizeMax = 120f; marquee.fontSizeMin = 30f;
        marquee.raycastTarget = false;
        PixelUIKit.Stretch(marquee.rectTransform);

        // Lights: the marquee's warm glow, the screen's cool glow and a soft front light.
        Light(arcade, "Marquee Light", new Vector3(0f, 1.95f, -0.6f), marqueeColor, 1.6f, 3f);
        Light(arcade, "Screen Light", new Vector3(0f, 1.3f, -0.7f), new Color(0.45f, 0.55f, 1f), 1.2f, 3f);
        Light(arcade, "Front Light", new Vector3(-1.5f, 1.6f, -2.5f), Color.white, 0.8f, 8f);

        BuildAlien(arcade);

        cabinetCam = MakeCamera("Cabinet Camera", world.transform, outTexture, CabinetFov, new Color(0.02f, 0.015f, 0.04f, 1f));
        cabinetCam.nearClipPlane = 0.01f;
        cabinetCam.farClipPlane = 40f;
    }

    private static void Light(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        GameObject go = new GameObject(name, typeof(Light));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Light l = go.GetComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
    }

    /// <summary>The little cube alien: a green cube body with big eyes, two antennae with ball tips, stubby legs and an arm that reaches for the cabinet.</summary>
    private void BuildAlien(Transform arcade)
    {
        alien = new GameObject("Alien").transform;
        alien.SetParent(arcade, false);
        alien.localPosition = alienPosition;
        alien.localRotation = Quaternion.Euler(0f, 25f, 0f);   // (turned a little towards the cabinet)
        alien.localScale = Vector3.one * alienSize;
        kit.Box("Leg L", alien, new Vector3(-0.08f, 0.06f, 0f), new Vector3(0.07f, 0.12f, 0.07f), alienColor * 0.8f);
        kit.Box("Leg R", alien, new Vector3(0.08f, 0.06f, 0f), new Vector3(0.07f, 0.12f, 0.07f), alienColor * 0.8f);
        alienBody = new GameObject("Body Pivot").transform;
        alienBody.SetParent(alien, false);
        alienBody.localPosition = new Vector3(0f, 0.22f, 0f);
        kit.Box("Body", alienBody, new Vector3(0f, 0.15f, 0f), new Vector3(0.32f, 0.32f, 0.32f), alienColor);
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -0.075f : 0.075f;
            Transform eye = kit.Ball("Eye", alienBody, new Vector3(x, 0.2f, 0.17f), 0.06f, Color.white).transform;
            kit.Ball("Pupil", eye, new Vector3(0f, 0f, 0.42f), 0.25f, Color.black);   // (inside the eye's own scale)
            eyes[i] = eye;
        }
        eyeScale = eyes[0].localScale;
        antennaL = Antenna("Antenna L", -0.09f);
        antennaR = Antenna("Antenna R", 0.09f);
        armR = kit.Box("Arm", alien, new Vector3(0.2f, 0.4f, 0.12f), new Vector3(0.06f, 0.06f, 0.06f), alienColor * 0.9f).transform;
        // The arm is a little hand that slides to where it taps (positions kept relative to the intro's world); the body stays put.
        alienArmRest = world.transform.InverseTransformPoint(armR.position);
    }

    private Transform Antenna(string name, float x)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(alienBody, false);
        pivot.localPosition = new Vector3(x, 0.31f, 0f);
        kit.Cyl("Stalk", pivot, new Vector3(0f, 0.07f, 0f), 0.008f, 0.14f, alienColor * 0.7f);
        kit.Ball("Tip", pivot, new Vector3(0f, 0.15f, 0f), 0.025f, new Color(0.8f, 1f, 0.4f, 1f));
        return pivot;
    }

    private void BuildCanvas(PixelClicker clicker)
    {
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        canvasGo = PixelUIKit.CreateCanvas("Studio Intro", sortingOrder, new Vector2(1920f, 1080f), true);
        group = canvasGo.AddComponent<CanvasGroup>();

        // The cabinet camera's picture over the whole screen (it blocks clicks to whatever is behind).
        GameObject view = new GameObject("View", typeof(RectTransform), typeof(RawImage));
        view.transform.SetParent(canvasGo.transform, false);
        RawImage ri = view.GetComponent<RawImage>();
        ri.texture = outTexture;
        ri.color = Color.white;
        PixelUIKit.Stretch(ri.rectTransform);

        GameObject fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeGo.transform.SetParent(canvasGo.transform, false);
        fade = fadeGo.GetComponent<Image>();
        fade.color = new Color(0f, 0f, 0f, 0f);
        fade.raycastTarget = false;
        PixelUIKit.Stretch(fade.rectTransform);

        // GAME OVER and the scanlines: a world-space canvas laid just in front of the arcade screen (the cabinet camera starts framed on the
        // screen, so it reads as plain space with text over it, then pulls back with it).
        GameObject sc = new GameObject("Screen Overlay", typeof(RectTransform), typeof(Canvas));
        sc.transform.SetParent(screenTransform.parent, false);
        sc.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform scr = sc.GetComponent<RectTransform>();
        scr.sizeDelta = new Vector2(1280f, 720f);
        scr.localScale = Vector3.one * (ScreenW / 1280f);
        scr.localPosition = screenTransform.localPosition + new Vector3(0f, 0f, -0.003f);
        scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        scanTex.SetPixels(new[] { new Color(0f, 0f, 0f, 0.9f), new Color(0f, 0f, 0f, 0.9f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f) });
        scanTex.Apply();
        GameObject sl = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
        sl.transform.SetParent(sc.transform, false);
        scanlines = sl.GetComponent<RawImage>();
        scanlines.texture = scanTex;
        scanlines.raycastTarget = false;
        PixelUIKit.Stretch(scanlines.rectTransform);
        gameOverLabel = PixelUIKit.CreateText(font, sc.transform, "Game Over", gameOverText, 150f, TextAlignmentOptions.Center, FontStyles.Bold, gameOverColor);
        gameOverLabel.characterSpacing = 8f;
        gameOverLabel.raycastTarget = false;
        PixelUIKit.Stretch(gameOverLabel.rectTransform);
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
        Object[] owned = { earthTex, cloudTex, glowTex, dotTex, scanTex, galaxyMesh, earthMat, cloudMat, glowMat, galaxyMat, screenMat };
        foreach (Object o in owned) if (o != null) Destroy(o);
        earthTex = cloudTex = glowTex = dotTex = scanTex = null;
        galaxyMesh = null;
        earthMat = cloudMat = glowMat = galaxyMat = screenMat = null;
        if (kit != null) { kit.DestroyMaterials(); kit = null; }
    }
}
