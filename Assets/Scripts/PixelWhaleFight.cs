using UnityEngine;
using static PixelInput;

/// <summary>
/// The Whale (Fishing's rarest special block). Its first manual click hooks it: it yanks the line and runs from the mouse, dashing about the screen while
/// you click it down - it needs a random 15-100 clicks and pays more the longer the fight. A thin fishing line is drawn from the cursor to the whale; it is
/// invisible within about a pixel of the cursor and fades in beyond that (the way the bank hose fades out near the camera). If you can't land it in time it
/// breaks the line and swims off. The auto clicker waits while a fight is on.
/// </summary>
public class PixelWhaleFight : MonoBehaviour
{
    [Header("Whale fight")]
    [SerializeField, Tooltip("Turns the whale's running and the fishing line off: a whale is then just a block that takes many clicks.")]
    private bool disableFight;
    [SerializeField, Tooltip("How close the mouse can come before the whale runs, in screen heights. 0 = 0.2.")]
    private float fleeRadius;
    [SerializeField, Tooltip("How fast the whale swims away from the mouse, in screen heights per second. 0 = 0.55.")]
    private float fleeSpeed;
    [SerializeField, Tooltip("How much faster the whale dashes when it yanks the line (times the flee speed). 0 = 2.4.")]
    private float yankSpeedFactor;
    [SerializeField, Tooltip("Seconds between the whale's own yanks (random between half and one and a half times this). 0 = 2.6.")]
    private float yankEvery;
    [SerializeField, Tooltip("How long one yank lasts, in seconds. 0 = 0.35.")]
    private float yankSeconds;
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
    private float yankLeft, nextYank;
    private Vector2 yankDir;
    private GameObject lineObject;
    private MeshFilter lineFilter;
    private Mesh lineMesh;
    private Material lineMaterial;
    private Vector3[] points;
    private float tension;                 // 1 = the whale just yanked (the line is pulled straight)
    private float areaLeft = 0.1f, areaRight = 0.9f, areaBottom = 0.2f, areaTop = 0.78f;

    private float FleeRadius => fleeRadius > 0f ? fleeRadius : 0.2f;
    private float FleeSpeed => fleeSpeed > 0f ? fleeSpeed : 0.55f;

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
        Yank(true);
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
        yankLeft = 0f;
        ScheduleYank();
        PixelAudio.Play("whale_hook");
        PixelHints.Announce("A whale is on the line!");
        PixelStats.Count("whales.hooked");
    }

    private void ScheduleYank()
    {
        float every = yankEvery > 0f ? yankEvery : 2.6f;
        nextYank = Time.unscaledTime + every * Random.Range(0.5f, 1.5f);
    }

    /// <summary>The whale pulls: a burst of speed away from the mouse (or a random way).</summary>
    private void Yank(bool fromClick)
    {
        if (!fighting || cam == null) return;
        Vector2 mouse = cam.ScreenToViewportPoint(PointerPosition());
        float aspect = Mathf.Max(0.5f, cam.aspect);
        Vector2 away = new Vector2((pos.x - mouse.x) * aspect, pos.y - mouse.y);
        yankDir = away.sqrMagnitude > 1e-5f ? away.normalized : Random.insideUnitCircle.normalized;
        if (!fromClick) yankDir = (yankDir + Random.insideUnitCircle * 0.6f).normalized;
        yankLeft = (yankSeconds > 0f ? yankSeconds : 0.35f) * (fromClick ? 0.7f : 1f);
        tension = 1f;
        PixelAudio.Play("whale_yank");
        ScheduleYank();
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
        if (Time.unscaledTime >= nextYank) Yank(false);
        tension = Mathf.MoveTowards(tension, 0f, dt * 2.5f);
        Steer(dt);
        Place();
    }

    /// <summary>Swims away from the mouse (the same rules as Cube Evasion), with a burst of speed on every yank.</summary>
    private void Steer(float dt)
    {
        float aspect = Mathf.Max(0.5f, cam.aspect);
        Vector2 mouse = cam.ScreenToViewportPoint(PointerPosition());
        Vector2 away = new Vector2((pos.x - mouse.x) * aspect, pos.y - mouse.y);
        float dist = away.magnitude;
        Vector2 desired = Vector2.zero;
        float agility = 7f;
        float marginX = 0.04f / aspect, marginY = 0.04f;

        if (yankLeft > 0f)
        {
            yankLeft -= dt;
            Vector2 dir = yankDir;
            // A dash that meets a wall turns along it.
            if ((pos.x < areaLeft + marginX && dir.x < 0f) || (pos.x > areaRight - marginX && dir.x > 0f)) dir.x = -dir.x * 0.6f;
            if ((pos.y < areaBottom + marginY && dir.y < 0f) || (pos.y > areaTop - marginY && dir.y > 0f)) dir.y = -dir.y * 0.6f;
            yankDir = dir.normalized;
            desired = yankDir * FleeSpeed * (yankSpeedFactor > 0f ? yankSpeedFactor : 2.4f);
            agility = 14f;
        }
        else if (dist < FleeRadius)
        {
            Vector2 dir = dist > 0.0005f ? away / dist : Random.insideUnitCircle.normalized;
            float urgency = 1f - Mathf.Clamp01(dist / FleeRadius);
            float speed = FleeSpeed * Mathf.Lerp(0.35f, 1f, urgency);
            bool blockL = pos.x < areaLeft + marginX && dir.x < 0f, blockR = pos.x > areaRight - marginX && dir.x > 0f;
            bool blockB = pos.y < areaBottom + marginY && dir.y < 0f, blockT = pos.y > areaTop - marginY && dir.y > 0f;
            bool blockX = blockL || blockR, blockY = blockB || blockT;
            if (blockX && blockY)
            {
                Vector2 centre = new Vector2((areaLeft + areaRight) * 0.5f, (areaBottom + areaTop) * 0.5f);
                Vector2 toCentre = new Vector2((centre.x - pos.x) * aspect, centre.y - pos.y);
                dir = toCentre.sqrMagnitude > 1e-6f ? toCentre.normalized : Vector2.up;
            }
            else if (blockX)
            {
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

    private void Place()
    {
        Vector3 basePos = clicker.CubeBasePosition;
        float depth = Vector3.Dot(basePos - cam.transform.position, cam.transform.forward);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(pos.x, pos.y, depth));
        clicker.SetCubeOffset(world - basePos);
    }

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
}
