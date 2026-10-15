using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small, quiet click feedback for EVERY pixel type (the early pixels have their own colours and extras; the rest use their tier colour): a handful of tiny squares pop off the
/// cube in the pixel's own colour. Manual clicks get the full little burst, auto-clicker clicks a much lighter one (fewer, shorter, rate-limited), and
/// tough pixels only pop on the final break. Luminescent gets just a light touch. Player setting: Settings > Interface > Click effects (Off / Light / Normal).
/// Uses the shared <see cref="PixelFx"/> particle helper (unlit little squares), so nothing needs a texture or a prefab.
/// </summary>
public static class PixelBasicFx
{
    public static readonly string[] LevelNames = { "Off", "Light", "Normal" };
    private const string PrefKey = "PixelClicker.Setting.ClickFx";
    private static int levelCache = -1;
    private static float nextAutoTime;
    private static int live;   // bursts that are still alive (a cap keeps a fast auto clicker from filling the screen)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { levelCache = -1; nextAutoTime = 0f; live = 0; pool.Clear(); active.Clear(); runner = null; }

    /// <summary>0 = off, 1 = light, 2 = normal (default).</summary>
    public static int Level
    {
        get
        {
            if (levelCache < 0) levelCache = Mathf.Clamp(PixelPrefs.GetInt(PrefKey, 2), 0, LevelNames.Length - 1);
            return levelCache;
        }
        set
        {
            levelCache = Mathf.Clamp(value, 0, LevelNames.Length - 1);
            PixelPrefs.SetInt(PrefKey, levelCache);
            PixelPrefs.Save();
        }
    }

    /// <summary>Re-reads the setting after a save with different settings was loaded.</summary>
    public static void ReloadFromPrefs() { levelCache = -1; }

    /// <summary>True for the pixel types this system draws: every one of them now (the early pixels have their own colours and extras, the rest use their tier colour).</summary>
    public static bool Handles(PixelClicker.PixelType type) => true;

    /// <summary>
    /// One pop. 'amountScale' = 1 for the full effect (a break), smaller for a chip of a tough pixel. 'strength' is the Inspector multiplier (0 = 1).
    /// </summary>
    public static void Play(PixelClicker.PixelType type, Color tierColor, Vector3 centre, float size, bool automatic, float amountScale, float strength)
    {
        int level = Level;
        if (level == 0 || !Handles(type) || PixelFx.Material == null) return;
        using (playMarker.Auto()) PlayInner(type, tierColor, centre, size, automatic, amountScale, strength, level);
    }

    private static void PlayInner(PixelClicker.PixelType type, Color tierColor, Vector3 centre, float size, bool automatic, float amountScale, float strength, int level)
    {

        // Auto clicks: lighter, and never more often than every 0.18 s, so a fast auto clicker stays calm.
        if (automatic)
        {
            if (Time.unscaledTime < nextAutoTime) return;
            nextAutoTime = Time.unscaledTime + 0.18f;
        }
        if (live >= 6) return;

        float k = (level == 1 ? 0.55f : 1f) * (automatic ? 0.4f : 1f) * Mathf.Clamp01(amountScale) * (strength > 0f ? strength : 1f);
        if (k <= 0.02f) return;

        Color baseColor = new Color(tierColor.r, tierColor.g, tierColor.b, 1f);
        Color light = Color.Lerp(baseColor, Color.white, 0.45f);
        switch (type)
        {
            case PixelClicker.PixelType.White: baseColor = new Color(0.95f, 0.95f, 0.95f); light = Color.white; break;
            case PixelClicker.PixelType.Gray: baseColor = new Color(0.62f, 0.64f, 0.68f); light = new Color(0.85f, 0.87f, 0.92f); break;
            case PixelClicker.PixelType.Black: baseColor = new Color(0.34f, 0.35f, 0.4f); light = new Color(0.55f, 0.57f, 0.64f); break;   // lighter than the cube so the flecks show
            case PixelClicker.PixelType.Glass: baseColor = new Color(0.75f, 0.9f, 1f); light = Color.white; break;
            case PixelClicker.PixelType.Luminescent: baseColor = Color.Lerp(baseColor, new Color(1f, 1f, 0.7f), 0.5f); light = new Color(1f, 1f, 0.85f); break;
        }

        // A pixel with its own dark colour (Obsidian, Pest, some ores...) gets lighter flecks so the pop shows against the floor.
        if (Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b)) < 0.35f) baseColor = Color.Lerp(baseColor, new Color(0.6f, 0.62f, 0.68f), 0.6f);

        Burst burst = Acquire(centre);   // (a pooled burst: building a ParticleSystem from scratch on every click is what made fast clicking lag in the Editor)

        bool lumi = type == PixelClicker.PixelType.Luminescent;
        bool glass = type == PixelClicker.PixelType.Glass;

        // The pop: tiny squares thrown out and a little up, falling back, fading fast.
        int count = Mathf.Max(1, Mathf.RoundToInt((lumi ? 3f : glass ? 4f : 6f) * k));
        Fire(burst, "Pop", new PixelFx.Spec
        {
            lifetime = new Vector2(0.22f, 0.4f), speed = new Vector2(0.8f, 1.9f) * size,
            size = (glass ? new Vector2(0.035f, 0.07f) : lumi ? new Vector2(0.03f, 0.06f) : new Vector2(0.04f, 0.09f)) * size,
            gravity = lumi ? -0.1f : 0.7f, colorA = baseColor, colorB = light,
            burst = count, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.2f,
            sizeOverLife = new Vector2(1f, 0.25f), overLife = PixelFx.FadeGradient(light, baseColor, baseColor * 0.6f, 1f),
            maxParticles = 12,
        });

        // Gray: one quick metallic glint. Glass: a pair of larger sparkles. Luminescent: a soft glow mote drifting up (its whole effect, besides the pop).
        if (type == PixelClicker.PixelType.Gray && !automatic)
        {
            Fire(burst, "Glint", new PixelFx.Spec
            {
                lifetime = new Vector2(0.16f, 0.22f), speed = new Vector2(0f, 0.05f), size = new Vector2(0.13f, 0.16f) * size,
                colorA = Color.white, colorB = light, burst = 1, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f,
                sizeOverLife = new Vector2(1f, 0.1f), overLife = PixelFx.FadeGradient(Color.white, Color.white, light, 1f), maxParticles = 2,
            });
        }
        else if (glass)
        {
            Fire(burst, "Sparkle", new PixelFx.Spec
            {
                lifetime = new Vector2(0.25f, 0.4f), speed = new Vector2(0.1f, 0.4f) * size, size = new Vector2(0.09f, 0.14f) * size,
                colorA = Color.white, colorB = baseColor, burst = Mathf.Max(1, Mathf.RoundToInt(2f * k)), shape = ParticleSystemShapeType.Sphere,
                shapeRadius = size * 0.55f, sizeOverLife = new Vector2(1f, 0f), overLife = PixelFx.FadeGradient(Color.white, light, baseColor, 0.9f), maxParticles = 4,
            });
        }
        else if (lumi)
        {
            Fire(burst, "Mote", new PixelFx.Spec
            {
                lifetime = new Vector2(0.5f, 0.8f), speed = new Vector2(0.2f, 0.5f) * size, size = new Vector2(0.07f, 0.11f) * size,
                gravity = -0.15f, colorA = light, colorB = baseColor, burst = 1, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.45f,
                sizeOverLife = new Vector2(1f, 0.2f), overLife = PixelFx.FadeGradient(light, baseColor, baseColor * 0.7f, 0.8f), maxParticles = 2,
            });
        }

        Release(burst, 1.2f);
    }
    // ------------------------------------------------------------------
    // The pool: a burst's objects (root + its particle systems) are reused instead of being built and destroyed on every click
    // ------------------------------------------------------------------

    private class Burst
    {
        public GameObject root;
        public readonly Dictionary<string, ParticleSystem> systems = new Dictionary<string, ParticleSystem>();
        public float returnAt;
    }

    private static readonly Stack<Burst> pool = new Stack<Burst>();
    private static readonly List<Burst> active = new List<Burst>();
    private static PixelBasicFxPool runner;
    private static readonly Unity.Profiling.ProfilerMarker playMarker = new Unity.Profiling.ProfilerMarker("Pixel.ClickFx");

    private static Burst Acquire(Vector3 centre)
    {
        if (runner == null) runner = new GameObject("Click Fx Pool").AddComponent<PixelBasicFxPool>();
        Burst b = null;
        while (pool.Count > 0)
        {
            b = pool.Pop();
            if (b.root != null) break;   // (a scene reload destroyed the old ones)
            b = null;
        }
        if (b == null) b = new Burst { root = new GameObject("Click Fx") };
        b.root.transform.position = centre;
        b.root.SetActive(true);
        live++;
        return b;
    }

    private static void Fire(Burst b, string name, PixelFx.Spec spec)
    {
        if (!b.systems.TryGetValue(name, out ParticleSystem ps) || ps == null)
        {
            ps = PixelFx.Make(b.root.transform, name, spec);
            b.systems[name] = ps;
        }
        else PixelFx.Configure(ps, spec);
        ps.Play();
    }

    private static void Release(Burst b, float seconds)
    {
        b.returnAt = Time.time + seconds;
        active.Add(b);
    }

    /// <summary>Called every frame by the pool's runner: bursts that have played out go back to the pool.</summary>
    internal static void Tick()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Burst b = active[i];
            if (b.root == null) { active.RemoveAt(i); live = Mathf.Max(0, live - 1); continue; }
            if (Time.time < b.returnAt) continue;
            foreach (ParticleSystem ps in b.systems.Values) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            b.root.SetActive(false);
            active.RemoveAt(i);
            pool.Push(b);
            live = Mathf.Max(0, live - 1);
        }
    }
}

/// <summary>Returns finished click bursts to their pool (one per scene).</summary>
public class PixelBasicFxPool : MonoBehaviour
{
    private void Update() { PixelBasicFx.Tick(); }
}

/// <summary>Destroys a burst's root after a delay and tells the owner (so the live-burst count stays right).</summary>
public class PixelFxCleanup : MonoBehaviour
{
    private float life;
    private System.Action done;

    public static void Run(GameObject root, float seconds, System.Action onDone)
    {
        PixelFxCleanup c = root.AddComponent<PixelFxCleanup>();
        c.life = seconds;
        c.done = onDone;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }

    private void OnDestroy() { done?.Invoke(); }
}
