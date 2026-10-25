using UnityEngine;
using static PixelInput;

/// <summary>
/// The Whale (Fishing's rarest special block). Its first manual click hooks it: it then stays almost still (drifting slowly) for 2-3 clicks, then dashes to a
/// new spot on the screen, rests again, and so on while you click it down - it needs a random 15-50 clicks and pays more the longer the fight. A thin fishing line is drawn from the cursor to the whale; it is
/// invisible within about a pixel of the cursor and fades in beyond that (the way the bank hose fades out near the camera). If you can't land it in time it
/// breaks the line and swims off. The auto clicker waits while a fight is on.
/// </summary>
public class PixelWhaleFight : MonoBehaviour, IPixelDatabaseSource
{
    [Header("Whale fight")]
    [SerializeField, Tooltip("Turns the whale's running and the fishing line off: a whale is then just a block that takes many clicks.")]
    private bool disableFight;
    [SerializeField, Tooltip("How many clicks the whale stays almost still before it runs to a new spot: a random number between this and 'Rest Clicks Max' (the hooking click counts as the first). 0 = 2.")]
    private int restClicksMin;
    [SerializeField, Tooltip("See 'Rest Clicks Min'. 0 = 3.")]
    private int restClicksMax;
    [SerializeField, Tooltip("How slowly the whale drifts while it rests, in screen heights per second. 0 = 0.03.")]
    private float driftSpeed;
    [SerializeField, Tooltip("How fast the whale dashes to its new spot, in screen heights per second. 0 = 1.3.")]
    private float runSpeed;
    [SerializeField, Tooltip("The longest a dash lasts, in seconds (it ends sooner when the whale arrives). 0 = 0.9.")]
    private float runSeconds;
    [SerializeField, Tooltip("The new spot is at least this far from the old one, in screen heights. 0 = 0.3.")]
    private float minRunDistance;
    [SerializeField, Tooltip("Seconds you have to land the whale before it breaks the line. 0 = 80.")]
    private float fightSeconds;
    [SerializeField, Tooltip("The reward grows with the clicks: pay x clicks x (1 + clicks x this). 0 = 0.05.")]
    private float rewardGrowth;
    [SerializeField, Tooltip("Thickness of the fishing line, in pixel widths. 0 = 0.03.")]
    private float lineThickness;
    [SerializeField, Tooltip("The line is invisible this close to the cursor, in pixel widths. 0 = 1.")]
    private float lineHiddenWithin;
    [SerializeField, Tooltip("Then it fades in over this distance, in pixel widths. 0 = 1.8.")]
    private float lineFadeLength;
    [SerializeField, Tooltip("Colour of the fishing line (its alpha is the line's top opacity).")]
    private Color lineColor = new Color(0.96f, 0.98f, 1f, 0.9f);

    [Header("Texts")]
    [Tooltip("Event log line when a whale bites.")] [SerializeField] private string hookedText = "A whale is on the line!";

    private static PixelWhaleFight instance;

    /// <summary>True while a whale is fighting the line (the auto clicker waits).</summary>
    public static bool Active => instance != null && instance.fighting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Pay x clicks x (1 + clicks x growth).</summary>
    public static float RewardMultiplier(int need)
    {
        float g = instance != null && instance.rewardGrowth > 0f ? instance.rewardGrowth : 0.05f;
        return need * (1f + need * g);
    }

    private PixelClicker clicker;
    private Camera cam;
    private bool fighting;
    private float fightLeft;
    private Vector2 pos, vel;              // viewport position and velocity (screen heights per second)
    private int restLeft;                  // clicks until the next run
    private bool running;
    private float runLeft;
    private Vector2 runTarget;             // viewport spot the whale dashes to
    private Vector2 driftDir;
    private float driftChange;
    private GameObject lineObject;
    private MeshFilter lineFilter;
    private Mesh lineMesh;
    private Material lineMaterial;
    private Vector3[] points;
    private float tension;                 // 1 = the whale just yanked (the line is pulled straight)
    private float areaLeft = 0.1f, areaRight = 0.9f, areaBottom = 0.2f, areaTop = 0.78f;

    private int RollRest()
    {
        int lo = Mathf.Max(1, restClicksMin > 0 ? restClicksMin : 2);
        int hi = Mathf.Max(lo, restClicksMax > 0 ? restClicksMax : 3);
        return Random.Range(lo, hi + 1);
    }

    private void Awake() { instance = this; }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        EndFight(false);
        if (lineMesh != null) Destroy(lineMesh);
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    /// <summary>A click on the whale: a manual one hooks it, and every click (while hooked) makes it yank the line.</summary>
    public static void OnHit(PixelClicker c, bool automatic, int hits, int need)
    {
        if (instance == null) return;
        instance.Hit(c, automatic, hits, need);
    }

    private void Hit(PixelClicker c, bool automatic, int hits, int need)
    {
        if (disableFight) return;
        clicker = c;
        if (!fighting)
        {
            if (automatic || !CanFight()) return;
            BeginFight();
        }
        ClickedWhale();
    }

    /// <summary>Each click while hooked: the whale stays almost still for a few clicks, then dashes to a new spot.</summary>
    private void ClickedWhale()
    {
        restLeft--;
        if (restLeft <= 0) { StartRun(); restLeft = RollRest(); }
        else tension = Mathf.Max(tension, 0.35f);   // a small tug on the line, no movement
    }

    private bool CanFight()
    {
        if (clicker == null || clicker.PixelTransform == null || PixelMinigame.TakeoverActive) return false;
        foreach (PixelMinigame m in PixelMinigame.All)   // Dash / Blink / Evade also move the cube: one at a time
            if (m != null && m.Takeover && m.Busy) return false;
        return FindCamera() != null;
    }

    private Camera FindCamera() { cam = clicker.TargetCamera != null ? clicker.TargetCamera : UnityEngine.Camera.main; return cam; }

    private void BeginFight()
    {
        FindCamera();
        fighting = true;
        fightLeft = fightSeconds > 0f ? fightSeconds : 80f;
        Vector3 vp = cam.WorldToViewportPoint(clicker.PixelTransform.position);
        pos = new Vector2(Mathf.Clamp(vp.x, areaLeft, areaRight), Mathf.Clamp(vp.y, areaBottom, areaTop));
        vel = Vector2.zero;
        running = false;
        restLeft = RollRest();
        driftChange = 0f;
        PixelAudio.Play("whale_hook");
        PixelHints.Announce(hookedText);
        PixelStats.Count("whales.hooked");
    }

    /// <summary>The whale bolts to a new spot at least minRunDistance away.</summary>
    private void StartRun()
    {
        if (!fighting || cam == null) return;
        float aspect = Mathf.Max(0.5f, cam.aspect);
        float need = minRunDistance > 0f ? minRunDistance : 0.3f;
        Vector2 best = pos;
        float bestDist = -1f;
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = new Vector2(Random.Range(areaLeft, areaRight), Random.Range(areaBottom, areaTop));
            float d = new Vector2((c.x - pos.x) * aspect, c.y - pos.y).magnitude;
            if (d > bestDist) { best = c; bestDist = d; }
            if (d >= need) { best = c; bestDist = d; break; }
        }
        runTarget = best;
        running = true;
        runLeft = runSeconds > 0f ? runSeconds : 0.9f;
        tension = 1f;
        PixelAudio.Play("whale_yank");
    }

    private void EndFight(bool resetCube)
    {
        if (!fighting) return;
        fighting = false;
        if (clicker != null) clicker.SetCubeOffset(null);
        if (lineObject != null) lineObject.SetActive(false);
    }

    /// <summary>The whale broke the surface for the last time: caught.</summary>
    public static void OnCaught(PixelClicker c, int need, double amount)
    {
        if (instance != null) instance.Caught(c, need);
        PixelStats.Count("whales.caught");
        PixelStats.Best("whales.most_clicks", need);
        PixelAudio.Play("whale_caught");
        PixelHints.Announce(need >= 60 ? "You landed a GIANT whale! (" + need + " clicks)" : "You landed a whale! (" + need + " clicks)");
    }

    private void Caught(PixelClicker c, int need)
    {
        Vector3 at = c.PixelTransform.position;
        EndFight(false);
        PixelFxRing.Spawn(at, new Color(0.6f, 0.85f, 1f, 0.9f), c.PixelBaseSize * 0.6f, c.PixelBaseSize * 7f, 1.1f, 3f);
        PixelFxRing.Spawn(at, new Color(1f, 1f, 1f, 0.8f), c.PixelBaseSize * 0.3f, c.PixelBaseSize * 4.5f, 0.7f, 0f);
    }

    private void Update()
    {
        if (!fighting) return;
        if (clicker == null || clicker.PixelTransform == null || cam == null) { EndFight(false); return; }
        if (clicker.LiveType != PixelClicker.PixelType.Whale || clicker.LiveHits <= 0 || clicker.CubeHidden || PixelTitleScreen.Showing) { EndFight(false); return; }
        if ((Time.timeScale <= 0f && !PixelTimeStop.IsStopped) || PixelPauseMenu.IsPaused) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        fightLeft -= dt;
        if (fightLeft <= 0f)
        {
            PixelAudio.Play("whale_escape");
            EndFight(false);
            clicker.WhaleGotAway();
            return;
        }
        tension = Mathf.MoveTowards(tension, 0f, dt * 2.5f);
        Steer(dt);
        Place();
    }

    /// <summary>Drifts slowly while it rests; dashes to its new spot when a run starts.</summary>
    private void Steer(float dt)
    {
        float aspect = Mathf.Max(0.5f, cam.aspect);
        Vector2 desired = Vector2.zero;
        float agility = 2f;

        if (running)
        {
            runLeft -= dt;
            Vector2 toTarget = new Vector2((runTarget.x - pos.x) * aspect, runTarget.y - pos.y);
            if (toTarget.magnitude < 0.03f || runLeft <= 0f) running = false;
            else desired = toTarget.normalized * (runSpeed > 0f ? runSpeed : 1.3f);
            agility = 14f;
        }
        else
        {
            driftChange -= dt;
            if (driftChange <= 0f)
            {
                driftChange = Random.Range(1.2f, 2.6f);
                Vector2 centre = new Vector2((areaLeft + areaRight) * 0.5f, (areaBottom + areaTop) * 0.5f);
                Vector2 toCentre = new Vector2((centre.x - pos.x) * aspect, centre.y - pos.y);
                driftDir = (Random.insideUnitCircle * 0.8f + toCentre * 0.8f).normalized;   // a lazy wander that leans back towards the middle
            }
            desired = driftDir * (driftSpeed > 0f ? driftSpeed : 0.03f);
        }

        vel = Vector2.Lerp(vel, desired, 1f - Mathf.Exp(-agility * dt));
        pos += new Vector2(vel.x / aspect, vel.y) * dt;
        pos.x = Mathf.Clamp(pos.x, areaLeft, areaRight);
        pos.y = Mathf.Clamp(pos.y, areaBottom, areaTop);
    }

    private void Place() => PixelCubeScreenSpot.Place(clicker, cam, pos);

    // ------------------------------------------------------------------
    // The fishing line
    // ------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!fighting || clicker == null || clicker.PixelTransform == null || cam == null) { if (lineObject != null && lineObject.activeSelf) lineObject.SetActive(false); return; }
        BuildLine();
    }

    private void EnsureLine()
    {
        if (lineObject != null) return;
        lineObject = new GameObject("Fishing Line", typeof(MeshFilter), typeof(MeshRenderer));
        lineFilter = lineObject.GetComponent<MeshFilter>();
        lineMaterial = new Material(PixelShaders.SpriteDefault());
        MeshRenderer r = lineObject.GetComponent<MeshRenderer>();
        r.sharedMaterial = lineMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        points = new Vector3[28];
    }

    private void BuildLine()
    {
        EnsureLine();
        float size = clicker.PixelBaseSize;
        Vector3 whale = clicker.PixelTransform.position;
        // The hand: where the cursor's ray meets the plane the whale swims in.
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        float depth = Vector3.Dot(whale - cam.transform.position, cam.transform.forward);
        float along = Vector3.Dot(ray.direction, cam.transform.forward);
        Vector3 hand = ray.origin + ray.direction * (depth / Mathf.Max(0.01f, along));

        float length = Vector3.Distance(hand, whale);
        float hidden = (lineHiddenWithin > 0f ? lineHiddenWithin : 1f) * size;
        float fade = (lineFadeLength > 0f ? lineFadeLength : 1.8f) * size;
        if (length <= hidden)   // the cursor is on (or right next to) the whale: no line at all
        {
            if (lineObject.activeSelf) lineObject.SetActive(false);
            return;
        }
        if (!lineObject.activeSelf) lineObject.SetActive(true);

        // The line hangs in a gentle curve (less when the whale yanks it tight) and ripples a little.
        float sag = length * 0.12f * (1f - 0.85f * tension);
        float t = Time.unscaledTime;
        for (int i = 0; i < points.Length; i++)
        {
            float k = i / (float)(points.Length - 1);
            Vector3 p = Vector3.Lerp(hand, whale, k);
            p += Vector3.down * (Mathf.Sin(k * Mathf.PI) * sag);
            p += cam.transform.up * (Mathf.Sin(k * 18f + t * 9f) * 0.012f * length * Mathf.Sin(k * Mathf.PI) * (0.4f + tension));
            points[i] = p;
        }
        float radius = (lineThickness > 0f ? lineThickness : 0.03f) * size;
        Color baseColor = lineColor;
        lineMesh = PixelTube.Build(points, radius, 4, lineMesh, (ring, position, normal) =>
        {
            float d = Vector3.Distance(position, hand);
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - hidden) / fade));   // nothing within a pixel of the cursor, then fading in
            return new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * a);
        });
        lineMesh.RecalculateBounds();
        lineFilter.sharedMesh = lineMesh;
    }

    // ---- Game Database (IPixelDatabaseSource): the whale fight as a 'Special' entry (numbers read from the settings above), discovered with the Whale pixel ----

    void IPixelDatabaseSource.DatabaseSteps(PixelDatabase.Steps steps)
    {
        steps.Add(PixelDatabase.ExtrasOrder, b =>
        {
            PixelDatabase.Entry whale = b.Pixel(PixelClicker.PixelType.Whale);
            int restMin = restClicksMin > 0 ? restClicksMin : 2, restMax = restClicksMax > 0 ? restClicksMax : 3;
            string text = disableFight
                ? "Fishing's rarest special block. The fight is switched off: a whale is just a block that takes many clicks."
                : "Fishing's rarest special block. Your first manual click hooks it: it rests for " + restMin + "-" + restMax + " clicks, then dashes to a new spot on the screen, rests again, and so on " +
                  "while you click it down. A thin fishing line runs from the cursor to it. Land it within " + Mathf.RoundToInt(fightSeconds > 0f ? fightSeconds : 80f) + " seconds or it breaks the line and swims off; " +
                  "the longer the fight, the more it pays. The auto clicker waits while a fight is on.";
            PixelDatabase.Entry e = b.Add("Special", "Whale fight", text);
            e.discovered = whale != null && whale.discovered;
            b.Link(e, "Started by", whale, "Starts a special");
            if (b.Skills.TryGetValue(PixelSkills.FishingId, out PixelDatabase.Entry fishing)) b.Link(e, "Skill", fishing, "Special of skill");
        });
    }
}
