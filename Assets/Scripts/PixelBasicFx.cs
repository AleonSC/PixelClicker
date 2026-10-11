using UnityEngine;

/// <summary>
/// Small, quiet click feedback for the early pixels (White, Gray, Black, Red, Green, Blue, Glass, Luminescent): a handful of tiny squares pop off the
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
    private static void ResetStatics() { levelCache = -1; nextAutoTime = 0f; live = 0; }

    /// <summary>0 = off, 1 = light, 2 = normal (default).</summary>
    public static int Level
    {
        get
        {
            if (levelCache < 0) levelCache = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 2), 0, LevelNames.Length - 1);
            return levelCache;
        }
        set
        {
            levelCache = Mathf.Clamp(value, 0, LevelNames.Length - 1);
            PlayerPrefs.SetInt(PrefKey, levelCache);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Re-reads the setting after a save with different settings was loaded.</summary>
    public static void ReloadFromPrefs() { levelCache = -1; }

    /// <summary>True for the pixel types this system draws.</summary>
    public static bool Handles(PixelClicker.PixelType type)
    {
        switch (type)
        {
            case PixelClicker.PixelType.White: case PixelClicker.PixelType.Gray: case PixelClicker.PixelType.Black:
            case PixelClicker.PixelType.Red: case PixelClicker.PixelType.Green: case PixelClicker.PixelType.Blue:
            case PixelClicker.PixelType.Glass: case PixelClicker.PixelType.Luminescent:
                return true;
        }
        return false;
    }

    /// <summary>
    /// One pop. 'amountScale' = 1 for the full effect (a break), smaller for a chip of a tough pixel. 'strength' is the Inspector multiplier (0 = 1).
    /// </summary>
    public static void Play(PixelClicker.PixelType type, Color tierColor, Vector3 centre, float size, bool automatic, float amountScale, float strength)
    {
        int level = Level;
        if (level == 0 || !Handles(type) || PixelFx.Material == null) return;

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

        GameObject root = new GameObject("Click Fx");
        root.transform.position = centre;
        live++;

        bool lumi = type == PixelClicker.PixelType.Luminescent;
        bool glass = type == PixelClicker.PixelType.Glass;

        // The pop: tiny squares thrown out and a little up, falling back, fading fast.
        int count = Mathf.Max(1, Mathf.RoundToInt((lumi ? 3f : glass ? 4f : 6f) * k));
        PixelFx.Make(root.transform, "Pop", new PixelFx.Spec
        {
            lifetime = new Vector2(0.22f, 0.4f), speed = new Vector2(0.8f, 1.9f) * size,
            size = (glass ? new Vector2(0.035f, 0.07f) : lumi ? new Vector2(0.03f, 0.06f) : new Vector2(0.04f, 0.09f)) * size,
            gravity = lumi ? -0.1f : 0.7f, colorA = baseColor, colorB = light,
            burst = count, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.2f,
            sizeOverLife = new Vector2(1f, 0.25f), overLife = PixelFx.FadeGradient(light, baseColor, baseColor * 0.6f, 1f),
            maxParticles = 12,
        }).Play();

        // Gray: one quick metallic glint. Glass: a pair of larger sparkles. Luminescent: a soft glow mote drifting up (its whole effect, besides the pop).
        if (type == PixelClicker.PixelType.Gray && !automatic)
        {
            PixelFx.Make(root.transform, "Glint", new PixelFx.Spec
            {
                lifetime = new Vector2(0.16f, 0.22f), speed = new Vector2(0f, 0.05f), size = new Vector2(0.13f, 0.16f) * size,
                colorA = Color.white, colorB = light, burst = 1, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f,
                sizeOverLife = new Vector2(1f, 0.1f), overLife = PixelFx.FadeGradient(Color.white, Color.white, light, 1f), maxParticles = 2,
            }).Play();
        }
        else if (glass)
        {
            PixelFx.Make(root.transform, "Sparkle", new PixelFx.Spec
            {
                lifetime = new Vector2(0.25f, 0.4f), speed = new Vector2(0.1f, 0.4f) * size, size = new Vector2(0.09f, 0.14f) * size,
                colorA = Color.white, colorB = baseColor, burst = Mathf.Max(1, Mathf.RoundToInt(2f * k)), shape = ParticleSystemShapeType.Sphere,
                shapeRadius = size * 0.55f, sizeOverLife = new Vector2(1f, 0f), overLife = PixelFx.FadeGradient(Color.white, light, baseColor, 0.9f), maxParticles = 4,
            }).Play();
        }
        else if (lumi)
        {
            PixelFx.Make(root.transform, "Mote", new PixelFx.Spec
            {
                lifetime = new Vector2(0.5f, 0.8f), speed = new Vector2(0.2f, 0.5f) * size, size = new Vector2(0.07f, 0.11f) * size,
                gravity = -0.15f, colorA = light, colorB = baseColor, burst = 1, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.45f,
                sizeOverLife = new Vector2(1f, 0.2f), overLife = PixelFx.FadeGradient(light, baseColor, baseColor * 0.7f, 0.8f), maxParticles = 2,
            }).Play();
        }

        PixelFxCleanup.Run(root, 1.2f, () => live = Mathf.Max(0, live - 1));
    }
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
