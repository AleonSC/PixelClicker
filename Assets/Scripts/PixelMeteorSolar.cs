using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Meteor and Solar pixels' own looks and behaviour (see CLAUDE.md): Meteor = a dark cratered rock whose glowing cracks spread with every
/// hit (3 hits), a few rising embers on the cube and a fiery wake behind the old pixel that streaks away; Solar = a clean golden sun-cube with a
/// pulsing corona and slowly turning rays, an old pixel that HOVERS and (pixel rules) keeps daylight up while it floats (old behaviour: a small currency trickle), and the daylight bar that every Solar click raises.
/// </summary>
public static class PixelStarTextures
{
    private static readonly Dictionary<int, Texture2D> meteor = new Dictionary<int, Texture2D>();
    private static Texture2D sun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { meteor.Clear(); sun = null; }

    private static Color32 To32(Color c) => new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), 255);

    /// <summary>The meteor's surface: dark charcoal rock with pitted craters; glowing lava cracks spread further with each level (0 = a couple of dull embers, max = about to break).</summary>
    public static Texture2D Meteor(int level, int maxLevel)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int step = Mathf.RoundToInt(level / (float)maxLevel * 8f);
        if (meteor.TryGetValue(step, out Texture2D cached) && cached != null) return cached;

        const int n = 64;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float rock = PixelNoise.Fbm(u, v, 4, 3, 41);
                float grit = PixelNoise.Hash(x, y, 9);
                float grey = Mathf.Lerp(0.07f, 0.2f, rock) + (grit - 0.5f) * 0.05f;
                float pit = PixelNoise.Fbm(u + 0.31f, v + 0.11f, 3, 2, 77);
                if (pit < 0.3f) grey *= 0.62f;                       // dark craters
                else if (pit > 0.72f) grey *= 1.25f;                 // lighter ridges
                px[y * n + x] = To32(new Color(grey * 1.1f, grey * 0.95f, grey * 0.88f, 1f));
            }

        bool dim = step == 0;
        int cracks = dim ? 2 : 3 + step;
        int segments = dim ? 5 : 3 + step * 2;
        Color core = dim ? new Color(0.55f, 0.16f, 0.05f) : step >= 4 ? new Color(1f, 0.86f, 0.4f) : new Color(1f, 0.55f, 0.12f);
        Color halo = dim ? new Color(0.3f, 0.09f, 0.04f) : new Color(0.95f, 0.3f, 0.06f);
        for (int c = 0; c < cracks; c++)
        {
            System.Random rnd = new System.Random(5000 + c * 53);
            float x = n * (0.2f + (float)rnd.NextDouble() * 0.6f), y = n * (0.2f + (float)rnd.NextDouble() * 0.6f);
            float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
            for (int s = 0; s < 12; s++)
            {
                angle += ((float)rnd.NextDouble() - 0.5f) * 1.3f;
                float len = 3f + (float)rnd.NextDouble() * 5f;
                float nx = x + Mathf.Cos(angle) * len, ny = y + Mathf.Sin(angle) * len;
                if (s < segments) GlowLine(px, n, x, y, nx, ny, core, halo, !dim);
                x = nx; y = ny;
            }
        }

        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "MeteorSurface" + step };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        meteor[step] = tex;
        return tex;
    }

    private static void GlowLine(Color32[] px, int n, float x0, float y0, float x1, float y1, Color core, Color halo, bool wide)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            if (wide)
            {
                Put(px, n, cx + 1, cy, halo); Put(px, n, cx - 1, cy, halo); Put(px, n, cx, cy + 1, halo); Put(px, n, cx, cy - 1, halo);
            }
            Put(px, n, cx, cy, core);
        }
    }

    private static void Put(Color32[] px, int n, int x, int y, Color c)
    {
        x = ((x % n) + n) % n; y = ((y % n) + n) % n;
        px[y * n + x] = To32(c);
    }

    /// <summary>The sun-cube's surface: churning gold with a white-hot middle on every face.</summary>
    public static Texture2D Sun()
    {
        if (sun != null) return sun;
        const int n = 64;
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float gran = PixelNoise.Fbm(u, v, 5, 3, 19);
                Color c = Color.Lerp(new Color(1f, 0.55f, 0.08f), new Color(1f, 0.9f, 0.4f), gran);
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                float hot = Mathf.Clamp01(1f - d / 0.75f);
                c = Color.Lerp(c, new Color(1f, 0.98f, 0.86f), hot * hot * 0.9f);
                if (x < 2 || y < 2 || x > n - 3 || y > n - 3) c *= 0.88f;
                px[y * n + x] = To32(c);
            }
        sun = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = "SunSurface" };
        sun.SetPixels32(px);
        sun.Apply(true, false);
        return sun;
    }
}

/// <summary>Meteor particles: a few rising embers on the cube, sparks and chips on every hit, and a fiery wake behind the streaking old pixel.</summary>
public static class PixelCometFx
{
    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        if (PixelFx.Material == null) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        ParticleSystem embers = PixelFx.Make(parent, "Embers", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.4f), speed = new Vector2(0.1f, 0.45f) * unit, size = new Vector2(0.04f, 0.1f) * unit,
            gravity = -0.12f, colorA = new Color(1f, 0.55f, 0.12f, 1f), colorB = new Color(1f, 0.25f, 0.05f, 1f),
            rate = 18f, loop = true, shape = ParticleSystemShapeType.Sphere, shapeRadius = unit * 0.55f, shapePosition = centre,
            randomDirection = 0.3f, sizeOverLife = new Vector2(1f, 0.1f), maxParticles = 60,
            overLife = PixelFx.FadeGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.08f), new Color(0.25f, 0.05f, 0.02f), 0.9f),
        });
        embers.Play();
    }

    /// <summary>The wake of a meteor old pixel that is flying away (world-space particles, so they hang in the air behind it).</summary>
    public static void AttachWake(Transform parent, float size)
    {
        if (PixelFx.Material == null) return;
        ParticleSystem wake = PixelFx.Make(parent, "Fiery Wake", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 1.1f), speed = new Vector2(0f, 0.25f) * size, size = new Vector2(0.18f, 0.34f) * size,
            gravity = -0.1f, colorA = new Color(1f, 0.6f, 0.15f, 1f), colorB = new Color(1f, 0.3f, 0.06f, 1f),
            rate = 70f, loop = true, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.4f, randomDirection = 0.3f,
            sizeOverLife = new Vector2(1f, 0.05f), maxParticles = 160,
            overLife = PixelFx.FadeGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.42f, 0.08f), new Color(0.2f, 0.04f, 0.02f), 0.85f),
        });
        wake.Play();
    }

    /// <summary>A hit that doesn't launch it: sparks and rock chips (more as the cracks open).</summary>
    public static void Hit(Vector3 centre, float size, float progress)
    {
        if (PixelFx.Material == null) return;
        GameObject root = new GameObject("Meteor Hit");
        root.transform.position = centre;
        float k = Mathf.Clamp01(progress);
        ParticleSystem sparks = PixelFx.Make(root.transform, "Sparks", new PixelFx.Spec
        {
            lifetime = new Vector2(0.4f, 0.9f), speed = new Vector2(1.4f, 3.8f) * size, size = new Vector2(0.04f, 0.1f) * size,
            gravity = 1.4f, colorA = new Color(1f, 0.85f, 0.35f, 1f), colorB = new Color(1f, 0.4f, 0.08f, 1f),
            burst = Mathf.RoundToInt(Mathf.Lerp(8f, 22f, k)), shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f,
            randomDirection = 0.15f, sizeOverLife = new Vector2(1f, 0.2f), maxParticles = 50,
        });
        ParticleSystem chips = PixelFx.Make(root.transform, "Chips", new PixelFx.Spec
        {
            lifetime = new Vector2(0.6f, 1.1f), speed = new Vector2(1f, 2.6f) * size, size = new Vector2(0.05f, 0.11f) * size,
            gravity = 2.6f, colorA = new Color(0.16f, 0.14f, 0.13f, 1f), colorB = new Color(0.3f, 0.27f, 0.25f, 1f),
            burst = Mathf.RoundToInt(Mathf.Lerp(4f, 10f, k)), shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f,
            randomDirection = 0.15f, sizeOverLife = new Vector2(1f, 0.6f), maxParticles = 30,
        });
        sparks.Play();
        chips.Play();
        Object.Destroy(root, 2f);
        PixelAudio.PlayScaled("ore_chip", 0.8f);
    }
}

/// <summary>Solar look: a soft corona and rays drawn behind the cube, turning slowly and pulsing; they swell and brighten with daylight.</summary>
public static class PixelSunFx
{
    private static Sprite raySprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { raySprite = null; }

    private static Sprite RaySprite()
    {
        if (raySprite != null) return raySprite;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float falloff = Mathf.Clamp01(1f - r);
                float glow = Mathf.Pow(falloff, 2.2f);
                float angle = Mathf.Atan2(dy, dx);
                float rays = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * 12f)), 8f) * Mathf.Pow(falloff, 1.3f) * 0.9f;
                tex.SetPixel(x, y, new Color(1f, 0.88f, 0.4f, Mathf.Clamp01(glow * 0.7f + rays)));
            }
        tex.Apply(false, true);
        raySprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return raySprite;
    }

    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        if (PixelFx.Material == null) return;
        GameObject go = new GameObject("Sun Rays", typeof(SpriteRenderer), typeof(PixelSunRays));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = RaySprite();
        sr.sharedMaterial = PixelFx.Material;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        go.GetComponent<PixelSunRays>().Setup(sr, centre, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
    }
}

public class PixelSunRays : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector3 centre;
    private float unit, phase;

    public void Setup(SpriteRenderer renderer, Vector3 localCentre, float meshUnit)
    {
        sr = renderer; centre = localCentre; unit = meshUnit;
        phase = Random.Range(0f, 6.28f);
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (sr == null || cam == null || transform.parent == null) return;
        float sc = Mathf.Max(0.0001f, transform.parent.lossyScale.x);
        float worldUnit = unit * sc;
        float day = PixelDaylight.Level;
        float t = Time.time + phase;
        transform.position = transform.parent.TransformPoint(centre) + cam.transform.forward * worldUnit * 0.8f;   // behind the cube, so it frames it
        transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, t * 10f);
        float diameter = worldUnit * (2.9f + 0.12f * Mathf.Sin(t * 2.1f) + 0.6f * day);
        transform.localScale = Vector3.one * (diameter / 1.28f / sc);
        Color c = sr.color; c.a = Mathf.Clamp01(0.5f + 0.1f * Mathf.Sin(t * 2.1f) + 0.3f * day); sr.color = c;
    }
}

/// <summary>An old Solar pixel: it hovers a little above the floor, bobbing, instead of falling, and pays a small trickle while it floats (more in daylight).</summary>
public class OldPixelSolarHover : MonoBehaviour
{
    private PixelClicker clicker;
    private Rigidbody body;
    private OldPixelInfo info;
    private OldPixelDespawn despawn;
    private float phase, payTimer, floorY;
    private bool haveFloor;
    private float floorRefresh;

    public void Setup(PixelClicker owner)
    {
        clicker = owner;
        body = GetComponent<Rigidbody>();
        info = GetComponent<OldPixelInfo>();
        despawn = GetComponent<OldPixelDespawn>();
        phase = Random.Range(0f, 6.28f);
    }

    private void FixedUpdate()
    {
        if (clicker == null || body == null || body.isKinematic) return;
        if (despawn != null && (despawn.Held || despawn.IsDespawning)) return;
        floorRefresh -= Time.fixedDeltaTime;
        if (floorRefresh <= 0f)
        {
            floorRefresh = 0.25f;
            if (Physics.Raycast(body.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<OldPixelInfo>() == null)
            {
                floorY = hit.point.y; haveFloor = true;
            }
        }
        if (!haveFloor) return;
        float size = clicker.OldPixelWorldSize;
        float target = floorY + size * (1.7f + 0.18f * Mathf.Sin(Time.time * 1.3f + phase));
#if UNITY_6000_0_OR_NEWER
        Vector3 v = body.linearVelocity;
#else
        Vector3 v = body.velocity;
#endif
        body.AddForce(Vector3.up * ((target - body.position.y) * 14f - v.y * 4f), ForceMode.Acceleration);
        body.AddForce(new Vector3(-v.x, 0f, -v.z) * 0.8f, ForceMode.Acceleration);   // drifts to a stop
    }

    private void Update()
    {
        if (clicker == null || info == null || Time.timeScale <= 0f) return;
        if (despawn != null && (despawn.Held || despawn.IsDespawning)) return;
        payTimer += Time.deltaTime;
        float every = PixelDaylight.TrickleSeconds;
        if (payTimer < every) return;
        payTimer = 0f;
        if (!clicker.HusksPayCurrency) { PixelDaylight.OnHoverTick(); return; }   // pixel rules: a hovering Solar husk keeps daylight up instead of paying
        double pay = info.amount * PixelDaylight.TrickleFraction * PixelDaylight.PayMultiplier;
        if (pay > 0d) clicker.AddCurrency(info.tierIndex, pay);
    }
}

/// <summary>
/// The faint yellow aura of a complete Solar group: every old Solar pixel that is part of a group of 5+ (see <see cref="PixelClicker"/> UpdateSolarClusters,
/// which calls <see cref="Keep"/>) draws a soft camera-facing glow; overlapping glows read as one aura round the group. Fades in and out.
/// </summary>
public class OldPixelSolarAura : MonoBehaviour
{
    private static Sprite glow;
    private SpriteRenderer sr;
    private float lastKeep = -99f, alpha;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { glow = null; }

    private static Sprite GlowSprite()
    {
        if (glow != null) return glow;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float f = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 0.86f, 0.35f, f * f * (3f - 2f * f)));
            }
        tex.Apply(false, true);
        glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return glow;
    }

    /// <summary>Called every few frames while this pixel is part of a complete group.</summary>
    public void Keep() { lastKeep = Time.time; }

    private void Awake()
    {
        if (PixelFx.Material == null) { enabled = false; return; }
        GameObject go = new GameObject("Solar Aura", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = GlowSprite();
        sr.sharedMaterial = PixelFx.Material;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        sr.color = new Color(1f, 0.86f, 0.35f, 0f);
        go.SetActive(false);
    }

    private void LateUpdate()
    {
        if (sr == null) return;
        bool on = Time.time - lastKeep < 0.5f;
        alpha = Mathf.MoveTowards(alpha, on ? 0.3f : 0f, Time.deltaTime * 0.6f);
        bool show = alpha > 0.005f;
        if (sr.gameObject.activeSelf != show) sr.gameObject.SetActive(show);
        if (!show) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        float size = Mathf.Max(0.05f, transform.lossyScale.x);
        Transform t = sr.transform;
        t.position = transform.position + cam.transform.forward * size * 0.9f;   // behind the pixel
        t.rotation = cam.transform.rotation;
        t.localScale = Vector3.one * (size * 4.2f / 0.64f / size);   // diameter ~4.2 pixel widths (sprite is 0.64 units across)
        Color c = sr.color; c.a = alpha * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f)); sr.color = c;
    }
}
