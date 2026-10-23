// ---- System notes ----
// PixelFloorModes.cs
//   What the floor does while 'Display floor' is OFF (Settings > Display, 'When floor is off': PixelFloor.OffMode, PlayerPrefs
//   PixelClicker.Setting.FloorOffMode, global): 0 Invisible (only the sky, the default), 1 Flat plane (PixelFloor.UpdatePlane: a flat
//   opaque lit sheet in the floor style's average colour just under the floor top, receives shadows; the edge fade melts its rim into
//   the sky), 2 Landing pulse (PixelFloorPulse.Landed / LandedAt, called from OldPixelImpact - OnCollisionEnter plus a fall check in its FixedUpdate for pixels that stop after a fast fall without a collision event: a soft rounded-square ripple, tinted by the pixel's
//   colour, lies on the surface the pixel landed on - turned to the contact normal and the pixel's heading, sized by the pixel and the
//   impact - grows and fades in about 0.7 s; at most 24 at once, so it also works on slopes / shapes / any other solid surface).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public partial class PixelFloor
{
    public static readonly string[] OffModeNames = { "Invisible", "Flat plane", "Landing pulse" };
    private const string OffModePref = "PixelClicker.Setting.FloorOffMode";
    private static int offModeCache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOffMode() { offModeCache = -1; }

    /// <summary>What the floor shows while 'Display floor' is off: 0 invisible, 1 flat plane, 2 landing pulse.</summary>
    public static int OffMode
    {
        get
        {
            if (offModeCache < 0) offModeCache = Mathf.Clamp(PixelPrefs.GetInt(OffModePref, 0), 0, OffModeNames.Length - 1);
            return offModeCache;
        }
        set
        {
            offModeCache = Mathf.Clamp(value, 0, OffModeNames.Length - 1);
            PixelPrefs.SetInt(OffModePref, offModeCache);
        }
    }

    /// <summary>The floor is off and its landing pulses are switched on.</summary>
    public static bool PulseActive => !PixelTitleWanderers.DisplayFloor && OffMode == 2;

    private bool PlaneMode => !PixelTitleWanderers.DisplayFloor && OffMode == 1;

    // ------------------------------------------------------------------
    // The world-space ground shader (styles with shaderGround > 0)
    // ------------------------------------------------------------------

    private static Shader groundShaderCache;
    private static bool groundShaderMissing;
    private bool groundShaderOn;
    private bool fadedTextureOn;   // a painted picture drawn with the Faded Floor shader (it melts into the sky itself)
    private static Shader fadedShaderCache;
    private static bool fadedShaderMissing;

    /// <summary>The material fades itself out (a ground shader, or a painted picture on the Faded Floor shader): no solid sky-coloured ring and no see-through proxy copies.</summary>
    private bool SelfFading => groundShaderOn || fadedTextureOn;

    private static Shader FadedShader()
    {
        if (fadedShaderCache == null && !fadedShaderMissing)
        {
            fadedShaderCache = Resources.Load<Shader>("PixelFadedFloor");
            if (fadedShaderCache == null) { fadedShaderMissing = true; Debug.LogWarning("PixelFloor: Resources/PixelFadedFloor.shader was not found; painted floors end in the solid sky-coloured ring instead."); }
        }
        return fadedShaderCache != null && fadedShaderCache.isSupported ? fadedShaderCache : null;
    }

    private static Shader GroundShader()
    {
        if (groundShaderCache == null && !groundShaderMissing)
        {
            groundShaderCache = Resources.Load<Shader>("PixelWorldGround");
            if (groundShaderCache == null)
            {
                groundShaderMissing = true;
                Debug.LogWarning("PixelFloor: Resources/PixelWorldGround.shader was not found; ground-shader styles use their painted texture instead.");
            }
        }
        return groundShaderCache;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGroundShader() { groundShaderCache = null; groundShaderMissing = false; fadedShaderCache = null; fadedShaderMissing = false; }

    /// <summary>Shows a style with the world-space ground shader. False (nothing changed) when the shader is missing or unsupported: the painted texture is used.</summary>
    private bool ApplyShaderGround(FloorStyle style)
    {
        Shader shader = GroundShader();
        if (shader == null || !shader.isSupported) return false;
        activeMaterial = new Material(shader) { name = "Floor (ground shader " + style.name + ")" };
        Material m = activeMaterial;
        m.SetColor("_ColorA", style.colorA);
        m.SetColor("_ColorB", style.colorB);
        m.SetColor("_ColorC", style.colorC);
        m.SetFloat("_Pattern", style.shaderGround);
        m.SetFloat("_Scale", style.shaderScale > 0f ? style.shaderScale : 6f);
        m.SetFloat("_Speed", 0.15f);
        m.SetFloat("_Glow", style.glow > 0f ? Mathf.Clamp(style.glow, 0.5f, 2f) : 1f);
        m.SetFloat("_Seed", style.seed % 97);
        m.SetFloat("_Fade", visibility);
        m.SetVector(ScrollId, style.scrollSpeed * Mathf.Max(0.1f, style.tileWorldSize));   // (the painted scroll is in pictures per second)
        m.SetFloat(ClockId, 0f);
        m.SetFloat(PulseId, 1f);
        Material[] mats = new Material[Mathf.Max(1, originalMaterials != null ? originalMaterials.Length : 1)];
        for (int i = 0; i < mats.Length; i++) mats[i] = m;
        floorRenderer.sharedMaterials = mats;
        animTimer = animClock = 0f;
        groundShaderOn = true;
        return true;
    }

    private static readonly int ClockId = Shader.PropertyToID("_Clock");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int ScrollId = Shader.PropertyToID("_Scroll");

    /// <summary>Every frame for a ground-shader style: its clock (unscaled time that freezes in Time Stop, like the painted floors' animations) and its glow pulse.</summary>
    private void TickGroundShader(FloorStyle style, float dt)
    {
        animClock += dt * style.animSpeed;
        float pulse = 1f;
        if (style.pulseSpeed > 0f)
        {
            pulseClock += dt;
            pulse = 1f - style.pulseAmount * (0.5f + 0.5f * Mathf.Sin(pulseClock * style.pulseSpeed * Mathf.PI * 2f));
        }
        activeMaterial.SetFloat(ClockId, animClock);
        activeMaterial.SetFloat(PulseId, pulse);
    }

    /// <summary>The shader fades the ground out in a circle round the cube (or the first person player), like the edge fade does for painted floors.</summary>
    private void UpdateGroundShader()
    {
        if (!SelfFading || activeMaterial == null || floorRenderer == null) return;
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null) return;
        FadeRadii(clicker, out float r0, out float r1);   // (the same rim as the wireframe)
        Vector3 centre = clicker.CubeBasePosition;
        if (PixelFirstPerson.TryGetPlayerPosition(out Vector3 playerPos)) centre = playerPos;
        activeMaterial.SetVector("_Center", new Vector4(centre.x, centre.z, 0f, 0f));
        activeMaterial.SetFloat("_FadeStart", r0);
        activeMaterial.SetFloat("_FadeEnd", r1);
        if (fadedTextureOn) activeMaterial.SetFloat("_Light", PixelDayNight.Enabled ? Mathf.Lerp(0.4f, 1f, PixelDayNight.Level) : 1f);   // (an unlit picture follows the day / night cycle by hand)
    }

    // ------------------------------------------------------------------
    // The game-made ground never lies below the bottom edge of the view
    // ------------------------------------------------------------------

    [Tooltip("Let the game-made ground lie below the bottom edge of the view even when that is further than the fall distance allows. Normally it is lifted until it is on screen: below the edge, falling pixels would stop on the invisible screen-edge limit instead of landing, so nothing would ever touch the ground.")]
    [SerializeField] private bool allowGroundOffScreen = false;

    private float groundTimer;

    private void UpdateGroundHeight()
    {
        if (ownFloor == null || allowGroundOffScreen) return;
        groundTimer -= Time.unscaledDeltaTime;
        if (groundTimer > 0f) return;
        groundTimer = 0.5f;
        if (PixelGameMode.TitleOrIntro) return;
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null) return;
        Camera cam = PixelOrbitCamera.HomeCamera != null ? PixelOrbitCamera.HomeCamera : clicker.ViewCamera;   // the NORMAL view, like the view bounds
        if (cam == null) return;

        Vector3 b = clicker.CubeBasePosition;
        float fall = groundFallDistance > 0f ? groundFallDistance : 10f;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.RawBarHeight / 1080f : 0f;
        float limit = bar + 0.035f;   // the lowest screen height a pixel may reach
        bool Visible(float drop)
        {
            Vector3 vp = cam.WorldToViewportPoint(new Vector3(b.x, b.y - drop, b.z));
            return vp.z > 0f && vp.y >= limit;
        }
        float lo = 0f, hi = fall;
        if (Visible(hi)) lo = hi;
        else for (int i = 0; i < 14; i++) { float mid = (lo + hi) * 0.5f; if (Visible(mid)) lo = mid; else hi = mid; }

        Vector3 pos = ownFloor.transform.position;
        float y = b.y - lo;
        if (Mathf.Abs(pos.y - y) > 0.05f) ownFloor.transform.position = new Vector3(pos.x, y, pos.z);
    }

    private GameObject plane;
    private Material planeMat;
    private int planeStyle = -2;

    /// <summary>True while the flat plane is showing (the edge fade then rims it).</summary>
    private bool PlaneVisible => plane != null && plane.activeSelf;

    private void UpdatePlane()
    {
        bool hideForTitle = HidesOnTitle && PixelTitleWanderers.LiveBackground && (PixelGameMode.TitleOrIntro || exitHold);
        bool want = PlaneMode && floorRenderer != null && !hideForTitle;
        if (!want)
        {
            if (plane != null && plane.activeSelf) plane.SetActive(false);
            return;
        }
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null) return;
        Bounds fb = floorRenderer.bounds;
        if (plane == null)
        {
            float half = Mathf.Max(fb.extents.x, fb.extents.z);
            plane = new GameObject("Floor Plane");
            Mesh mesh = new Mesh { name = "Floor Plane" };
            mesh.vertices = new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = plane.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            planeMat = clicker.CreateVisualMaterial(Color.gray, false);
            if (planeMat != null) mr.sharedMaterial = planeMat;
            planeStyle = -2;
        }
        plane.transform.position = new Vector3(fb.center.x, fb.max.y - 0.004f, fb.center.z);
        if (planeMat != null && planeStyle != current)
        {
            planeStyle = current;
            Color c = AverageColor();
            c.a = 1f;
            SetColor(planeMat, "_BaseColor", c);
            SetColor(planeMat, "_Color", c);
        }
        if (!plane.activeSelf) plane.SetActive(true);
    }
}

/// <summary>Landing pulses: a small ripple on the surface an old pixel lands on (used while the floor is invisible and 'Landing pulse' is chosen).</summary>
public class PixelFloorPulse : MonoBehaviour
{
    private const int MaxRipples = 24;
    private const float Life = 0.7f;

    private class Ripple { public Transform t; public Material m; public float age, start, end; public Color color; }

    private static PixelFloorPulse instance;
    private static Mesh quad;
    private static Texture2D ringTexture;
    private static PixelClicker clicker;
    private readonly List<Ripple> ripples = new List<Ripple>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; quad = null; ringTexture = null; clicker = null; }

    /// <summary>An old pixel hit a surface: puts a ripple there (if the landing pulse is switched on).</summary>
    public static void Landed(Collision collision, Transform pixel, int tierIndex, float intensity)
    {
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        LandedAt(contact.point, contact.normal, pixel, tierIndex, intensity);
    }

    /// <summary>An old pixel came down on a surface at this point (found by a collision, or by OldPixelImpact's fall check when the collision event never came): puts a ripple there.</summary>
    public static void LandedAt(Vector3 point, Vector3 normal, Transform pixel, int tierIndex, float intensity)
    {
        if (!PixelFloor.PulseActive || pixel == null) return;
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        if (clicker == null) return;
        if (instance == null)
        {
            GameObject go = new GameObject("Floor Pulses");
            instance = go.AddComponent<PixelFloorPulse>();
        }
        Color color = clicker.IsValidTierIndex(tierIndex) ? clicker.Tiers[tierIndex].UIColor : Color.white;
        instance.Add(point, normal, pixel.eulerAngles.y, clicker.OldPixelWorldSize, intensity, color);
    }

    private void Add(Vector3 point, Vector3 normal, float yaw, float pixelSize, float intensity, Color color)
    {
        if (quad == null) BuildAssets();
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return;
        while (ripples.Count >= MaxRipples) Remove(0);
        GameObject go = new GameObject("Ripple");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Material m = new Material(shader) { mainTexture = ringTexture };
        mr.sharedMaterial = m;
        Quaternion onSurface = Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up);
        go.transform.rotation = onSurface * Quaternion.Euler(0f, yaw, 0f);
        go.transform.position = point + normal.normalized * 0.012f;
        float size = Mathf.Max(0.2f, pixelSize);
        ripples.Add(new Ripple { t = go.transform, m = m, start = size * (1.1f + 0.3f * intensity), end = size * (2.0f + 1.6f * intensity), color = color });
        go.transform.localScale = Vector3.one * ripples[ripples.Count - 1].start;
        m.color = new Color(color.r, color.g, color.b, 0f);
    }

    private void Remove(int i)
    {
        Ripple r = ripples[i];
        if (r.t != null) Destroy(r.t.gameObject);
        if (r.m != null) Destroy(r.m);
        ripples.RemoveAt(i);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Ripple r = ripples[i];
            r.age += dt;
            float k = r.age / Life;
            if (k >= 1f || r.t == null) { Remove(i); continue; }
            float grow = 1f - (1f - k) * (1f - k);
            r.t.localScale = Vector3.one * Mathf.Lerp(r.start, r.end, grow);
            float alpha = Mathf.Sin(Mathf.Min(1f, k * 6f) * Mathf.PI * 0.5f) * (1f - k) * 0.8f;
            r.m.color = new Color(r.color.r, r.color.g, r.color.b, alpha);
        }
    }

    private void OnDestroy()
    {
        foreach (Ripple r in ripples) if (r.m != null) Destroy(r.m);
        ripples.Clear();
        if (instance == this) instance = null;
    }

    private static void BuildAssets()
    {
        quad = new Mesh { name = "Ripple Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.RecalculateBounds();

        // A soft rounded-square outline (the pixel's own footprint) with a faint fill.
        const int n = 128;
        ringTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Ripple Ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);   // a rounded square
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.07f, 2f));
                float fill = d < 0.82f ? 0.12f * (1f - d / 0.82f) : 0f;
                float a = Mathf.Clamp01(ring + fill) * (d > 1f ? 0f : 1f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        ringTexture.SetPixels32(px);
        ringTexture.Apply(false);
    }
}
