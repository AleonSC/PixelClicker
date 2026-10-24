using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared helpers for the small code-built particle bursts (Fire pixel, ore chips). Everything is made of little squares (pixel art).
/// One-shot bursts on a hit / break (farm, wood, fish, electric, ore, Obsidian, Fire, chest, meteor) come from a POOL: Rent(centre, seconds) gives a
/// reused root, burst.Add(name, spec) a reused particle system set up afresh by Configure (callers Play it), and the burst goes back to the pool
/// by itself after 'seconds' (one runner, no per-burst objects or coroutines). Building a GameObject + 1-4 ParticleSystems on every hit and
/// destroying them seconds later was the main per-click cost of the gathering skills. FadeGradient results and size-over-life curves are cached.
/// The plain click pop (PixelBasicFx) uses the same pool.
/// </summary>
public static class PixelFx
{
    private static Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        material = null;
        OnRestart();
        PixelStatics.ResetOnRestart(OnRestart);   // restart-reset-not-progress: the fire-burst effect pool
    }

    // (Restart reloads the scene: the pooled bursts and the runner go with it)
    private static void OnRestart() { pool.Clear(); active.Clear(); runner = null; }

    // ---- the burst pool ----

    /// <summary>A pooled one-shot effect: a root at the effect's centre and its particle systems (reused from earlier bursts).</summary>
    public sealed class Burst
    {
        internal GameObject root;
        internal readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        internal int used;
        internal float returnAt;

        /// <summary>The burst's root (for things that need a parent).</summary>
        public Transform Root => root.transform;

        /// <summary>The burst's next particle system, set up from 'spec' (a reused one is fully reset by Configure). Not playing yet: call Play on it.</summary>
        public ParticleSystem Add(string name, Spec spec)
        {
            ParticleSystem ps = used < systems.Count ? systems[used] : null;
            if (ps != null)
            {
                Configure(ps, spec);
                if (ps.name != name) ps.name = name;
            }
            else
            {
                ps = Make(root.transform, name, spec);
                if (used < systems.Count) systems[used] = ps; else systems.Add(ps);
            }
            used++;
            return ps;
        }
    }

    private const int MaxPooled = 32;   // spare bursts kept for reuse (more are destroyed when they finish)
    private static readonly Stack<Burst> pool = new Stack<Burst>();
    private static readonly List<Burst> active = new List<Burst>();
    private static PixelFxPoolRunner runner;

    /// <summary>A one-shot effect at 'centre' that goes back to the pool by itself after 'seconds' (make 'seconds' cover its longest particle life).</summary>
    public static Burst Rent(Vector3 centre, float seconds)
    {
        if (runner == null) runner = new GameObject("Fx Burst Pool").AddComponent<PixelFxPoolRunner>();
        Burst b = null;
        while (pool.Count > 0)
        {
            b = pool.Pop();
            if (b.root != null) break;   // (a scene reload destroyed the old ones)
            b = null;
        }
        if (b == null) b = new Burst { root = new GameObject("Fx Burst") };
        b.root.transform.position = centre;
        b.root.SetActive(true);
        b.used = 0;
        b.returnAt = Time.time + seconds;
        active.Add(b);
        return b;
    }

    /// <summary>Called every frame by the pool's runner: bursts that have played out go back to the pool.</summary>
    internal static void Tick()
    {
        float now = Time.time;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Burst b = active[i];
            if (b.root == null) { active.RemoveAt(i); continue; }
            if (now < b.returnAt) continue;
            active.RemoveAt(i);
            if (pool.Count >= MaxPooled) { Object.Destroy(b.root); continue; }
            foreach (ParticleSystem ps in b.systems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            b.root.SetActive(false);
            pool.Push(b);
        }
    }

    /// <summary>The unlit, alpha-blended sprite material the bursts use (null if the shader can't be found).</summary>
    public static Material Material
    {
        get
        {
            if (material == null)
            {
                Shader shader = PixelShaders.SpriteDefault();
                if (shader == null) return null;
                material = new Material(shader) { name = "PixelFx" };
            }
            return material;
        }
    }

    public struct Spec
    {
        public Vector2 lifetime, speed, size;
        public float gravity;
        public Color colorA, colorB;
        public int burst;
        public float rate;
        public bool loop;
        public bool localSpace;
        public Gradient overLife;        // multiplies the start colour over the particle's life
        public Vector2 sizeOverLife;     // start and end multipliers (0 = no change)
        public ParticleSystemShapeType shape;
        public float shapeRadius;
        public float shapeAngle;
        public Vector3 shapeRotation;
        public Vector3 shapePosition;
        public float randomDirection;
        public int maxParticles;
    }

    /// <summary>A white-in-the-middle, fading gradient with the given colours: (c0 at 0, c1 at 'mid', c2 at 1) and an alpha that fades out.</summary>
    /// The result is cached and shared: assign it, never change its keys.
    public static Gradient FadeGradient(Color c0, Color c1, Color c2, float peakAlpha = 1f)
    {
        var key = (c0, c1, c2, peakAlpha);
        if (gradients.TryGetValue(key, out Gradient cached)) return cached;
        if (gradients.Count > 512) gradients.Clear();   // (tier colours are a fixed set; this only guards against something odd)
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 0.45f), new GradientColorKey(c2, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.08f), new GradientAlphaKey(peakAlpha * 0.7f, 0.55f), new GradientAlphaKey(0f, 1f) });
        gradients[key] = g;
        return g;
    }

    private static readonly Dictionary<(Color, Color, Color, float), Gradient> gradients = new Dictionary<(Color, Color, Color, float), Gradient>();
    private static readonly Dictionary<Vector2, AnimationCurve> sizeCurves = new Dictionary<Vector2, AnimationCurve>();

    public static ParticleSystem Make(Transform parent, string name, Spec s)
    {
        GameObject go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        Configure(ps, s);
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    /// <summary>Sets a (new or reused) particle system up from a spec. Reusing one instead of building a new system is much cheaper in the Editor (click bursts do that).</summary>
    public static void Configure(ParticleSystem ps, Spec s)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = s.loop;
        main.playOnAwake = false;
        main.simulationSpace = s.localSpace ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(s.lifetime.x, s.lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(s.speed.x, s.speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(s.size.x, s.size.y);
        main.gravityModifier = s.gravity;
        main.startColor = new ParticleSystem.MinMaxGradient(s.colorA, s.colorB);
        main.maxParticles = s.maxParticles > 0 ? s.maxParticles : 120;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = s.rate;
        if (s.burst > 0) { emission.burstCount = 1; emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)s.burst)); }
        else emission.burstCount = 0;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = s.shape;
        shape.radius = s.shapeRadius;
        shape.angle = s.shapeAngle;
        shape.radiusThickness = 0f;
        shape.rotation = s.shapeRotation;
        shape.position = s.shapePosition;
        shape.randomDirectionAmount = s.randomDirection;
        shape.scale = Vector3.one;   // (a reused system may have had its box stretched)

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = s.overLife != null;
        if (s.overLife != null) col.color = s.overLife;
        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = s.sizeOverLife != Vector2.zero;
        if (s.sizeOverLife != Vector2.zero)
        {
            if (!sizeCurves.TryGetValue(s.sizeOverLife, out AnimationCurve curve))
            {
                if (sizeCurves.Count > 256) sizeCurves.Clear();
                curve = new AnimationCurve(new Keyframe(0f, s.sizeOverLife.x), new Keyframe(1f, s.sizeOverLife.y));
                sizeCurves[s.sizeOverLife] = curve;
            }
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);   // (the system keeps its own copy of the curve)
        }
    }
}

/// <summary>Returns finished PixelFx bursts to their pool (one per scene, made on first use).</summary>
public class PixelFxPoolRunner : MonoBehaviour
{
    private void LateUpdate() { PixelFx.Tick(); }
}

/// <summary>A flat ring that swells and fades (the shock ring of a fire burst) with an optional light flash. Destroys itself.</summary>
public class PixelFxRing : MonoBehaviour
{
    private float duration, startScale, endScale, age;
    private Color color;
    private Renderer ringRenderer;
    private Light flash;
    private float flashIntensity;

    // One ring mesh for every ring (the shape never changes) and the colour set through a property block on the shared overlay
    // material: each ring used to build its own mesh and material copy, and Destroy(gameObject) freed neither.
    private static Mesh ringMesh;
    private static MaterialPropertyBlock block;

    public static void Spawn(Vector3 centre, Color color, float startScale, float endScale, float seconds, float lightIntensity)
    {
        GameObject go = new GameObject("Fx Ring", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.position = centre;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lies flat
        if (ringMesh == null) ringMesh = PixelRingMesh.Build(0.86f, 1f, 0.02f, 40);
        go.GetComponent<MeshFilter>().sharedMesh = ringMesh;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = PixelLooks.OverlayMaterial();
        SetColor(mr, color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        PixelFxRing ring = go.AddComponent<PixelFxRing>();
        ring.duration = Mathf.Max(0.05f, seconds);
        ring.startScale = startScale;
        ring.endScale = endScale;
        ring.color = color;
        ring.ringRenderer = mr;
        if (lightIntensity > 0f)
        {
            GameObject lg = new GameObject("Flash");
            lg.transform.SetParent(go.transform, false);
            lg.transform.position = centre + Vector3.up * 0.4f;
            ring.flash = lg.AddComponent<Light>();
            ring.flash.type = LightType.Point;
            ring.flash.color = new Color(1f, 0.55f, 0.15f, 1f);
            ring.flash.range = endScale * 3f;
            ring.flash.intensity = lightIntensity;
            ring.flash.shadows = LightShadows.None;
            ring.flashIntensity = lightIntensity;
        }
        go.transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / duration);
        float eased = 1f - (1f - k) * (1f - k);
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
        Color c = color; c.a = color.a * (1f - k);
        SetColor(ringRenderer, c);
        if (flash != null) flash.intensity = flashIntensity * (1f - k) * (1f - k);
        if (k >= 1f) Destroy(gameObject);
    }

    private static void SetColor(Renderer r, Color c)
    {
        if (block == null) block = new MaterialPropertyBlock();
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }
}

/// <summary>The Fire pixel's click: a ring of flames bursts out of the cube, with sparks, a shock ring and a flash of light.</summary>
public static class FireBurstFx
{
    public static void Play(Vector3 centre, float size)
    {
        if (PixelFx.Material == null) return;
        PixelFx.Burst burst = PixelFx.Rent(centre, 2.5f);

        // A ring of flames flying out sideways from round the cube, curling upwards.
        ParticleSystem flames = burst.Add("Flames", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 0.95f), speed = new Vector2(2.2f, 4.2f) * size, size = new Vector2(0.2f, 0.42f) * size,
            gravity = -0.25f, colorA = new Color(1f, 0.9f, 0.3f, 1f), colorB = new Color(1f, 0.45f, 0.08f, 1f),
            burst = 90, shape = ParticleSystemShapeType.Circle, shapeRadius = size * 0.62f, shapeRotation = new Vector3(90f, 0f, 0f),
            randomDirection = 0.12f, sizeOverLife = new Vector2(1f, 0.15f),
            overLife = PixelFx.FadeGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.45f, 0.08f), new Color(0.35f, 0.06f, 0.02f), 1f),
        });

        // Sparks thrown up and out, falling back down.
        ParticleSystem sparks = burst.Add("Sparks", new PixelFx.Spec
        {
            lifetime = new Vector2(0.6f, 1.2f), speed = new Vector2(1.4f, 4.4f) * size, size = new Vector2(0.04f, 0.09f) * size,
            gravity = 1.1f, colorA = new Color(1f, 0.95f, 0.5f, 1f), colorB = new Color(1f, 0.55f, 0.1f, 1f),
            burst = 36, shape = ParticleSystemShapeType.Cone, shapeRadius = size * 0.5f, shapeAngle = 55f, shapeRotation = new Vector3(-90f, 0f, 0f),
            randomDirection = 0.3f, sizeOverLife = new Vector2(1f, 0.4f),
            overLife = PixelFx.FadeGradient(Color.white, new Color(1f, 0.7f, 0.2f), new Color(0.5f, 0.1f, 0.02f), 1f),
        });
        flames.Play();
        sparks.Play();
        PixelFxRing.Spawn(centre + Vector3.down * size * 0.35f, new Color(1f, 0.5f, 0.1f, 0.85f), size * 0.5f, size * 2.6f, 0.45f, 7f);
        PixelAudio.Play("fire_burst");
    }
}

/// <summary>
/// An old Fire pixel: it falls like any other but burns the whole time - it slowly shrinks away into drifting embers and ash, crackling now and then,
/// and is gone (no payout) when nothing is left of it. It doesn't age while it burns.
/// </summary>
public class OldPixelBurn : MonoBehaviour
{
    private PixelClicker clicker;
    private Rigidbody body;
    private OldPixelDespawn despawn;
    private float burnSeconds, age, crackleTimer;
    private Vector3 startScale;
    private ParticleSystem embers;
    private float emberBase;

    public void Setup(PixelClicker owner, float seconds)
    {
        clicker = owner;
        burnSeconds = Mathf.Max(0.5f, seconds);
        body = GetComponent<Rigidbody>();
        despawn = GetComponent<OldPixelDespawn>();
    }

    private void Start()
    {
        startScale = transform.localScale;
        float unit = Mathf.Max(0.05f, transform.lossyScale.x);
        emberBase = 16f;
        if (PixelFx.Material != null)
        {
            embers = PixelFx.Make(transform, "Embers", new PixelFx.Spec
            {
                lifetime = new Vector2(0.8f, 1.6f), speed = new Vector2(0.2f, 0.9f), size = new Vector2(0.18f, 0.32f) * unit,
                gravity = -0.18f, colorA = new Color(1f, 0.7f, 0.2f, 1f), colorB = new Color(1f, 0.35f, 0.05f, 1f),
                rate = emberBase, loop = true, shape = ParticleSystemShapeType.Sphere, shapeRadius = unit * 0.5f, randomDirection = 0.4f,
                sizeOverLife = new Vector2(1f, 0.2f), maxParticles = 60,
                overLife = PixelFx.FadeGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.08f), new Color(0.12f, 0.1f, 0.1f), 1f),
            });
            embers.Play();
        }
        crackleTimer = Random.Range(0.1f, 0.5f);
        PixelAudio.PlayScaled("fire_crackle", 0.8f);
    }

    private void Update()
    {
        if (clicker == null) { Destroy(this); return; }
        if (despawn != null && (despawn.Held)) return;      // carried: it burns on the spot, not while held
        if (despawn != null) despawn.KeepAlive();           // it vanishes by burning, not by ageing

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / burnSeconds);
        float flicker = 1f + 0.05f * Mathf.Sin(age * 31f) + 0.03f * Mathf.Sin(age * 53f);
        transform.localScale = startScale * Mathf.Pow(1f - t, 0.85f) * flicker;

        if (embers != null)
        {
            ParticleSystem.EmissionModule em = embers.emission;
            em.rateOverTime = emberBase * (1.2f - 0.7f * t);
        }

        crackleTimer -= Time.deltaTime;
        if (crackleTimer <= 0f)
        {
            crackleTimer = Random.Range(0.8f, 1.8f);
            PixelAudio.PlayScaled("fire_crackle", 0.45f);
        }

        if (t >= 1f) Finish();
    }

    private void Finish()
    {
        if (body != null) clicker.ReleaseOldPixel(body, false);   // nothing left: no payout
        if (embers != null)
        {
            embers.transform.SetParent(null, true);               // let the last embers drift off
            ParticleSystem.EmissionModule em = embers.emission;
            em.rateOverTime = 0f;
            Destroy(embers.gameObject, 2f);
        }
        Destroy(gameObject);
    }
}

/// <summary>The Fire look's flames: a little stream of flame squares rising off the top of the pixel (the particles live in world space, so they trail when it moves).</summary>
public static class PixelFlames
{
    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        if (PixelFx.Material == null) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        ParticleSystem flames = PixelFx.Make(parent, "Flames", new PixelFx.Spec
        {
            lifetime = new Vector2(0.45f, 0.85f), speed = new Vector2(0.15f, 0.55f) * unit, size = new Vector2(0.16f, 0.3f) * unit,
            gravity = -0.35f, colorA = new Color(1f, 0.85f, 0.25f, 1f), colorB = new Color(1f, 0.4f, 0.06f, 1f),
            rate = 26f, loop = true, shape = ParticleSystemShapeType.Box, shapeRadius = 0f,
            shapePosition = centre + new Vector3(0f, size.y * 0.42f, 0f), shapeRotation = new Vector3(-90f, 0f, 0f), randomDirection = 0.2f,
            sizeOverLife = new Vector2(1f, 0.1f), maxParticles = 80,
            overLife = PixelFx.FadeGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.45f, 0.08f), new Color(0.3f, 0.05f, 0.02f), 0.95f),
        });
        ParticleSystem.ShapeModule shape = flames.shape;
        shape.scale = new Vector3(size.x * 0.9f, size.z * 0.9f, 0.01f);   // a flat box across the top
        flames.Play();
    }
}
