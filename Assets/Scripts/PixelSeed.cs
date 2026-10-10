using UnityEngine;

/// <summary>
/// A sprout that came out of a cracked Seed shell. It tumbles down, "plants" itself upright where it lands (a dirt mound pushes up and a
/// soft green light shows where), then grows a random old pixel from nothing on its tip over a few seconds (faster while Solar pixels lie
/// close by), holds it a moment (wobbling) and finally pops it off: the pixel becomes an ordinary old pixel of that random type (with its
/// normal payout) and the sprout shrinks away. Created by <see cref="PixelClicker"/>.
/// </summary>
public class SeedSprout : MonoBehaviour
{
    private enum Stage { Falling, Growing, Holding, Shrinking }

    /// <summary>How many sprouts exist right now (PixelClicker caps it).</summary>
    public static int Count { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Count = 0; }

    private PixelClicker clicker;
    private Rigidbody body;
    private Collider hit;
    private Transform visual;      // the sprout mesh
    private Transform mound;       // the dirt mound it grows out of
    private Light glow;            // the soft light that makes it easy to spot
    private GameObject pixel;      // the growing pixel (a display model)
    private Stage stage = Stage.Falling;
    private float unit, sproutScale, growSeconds, holdSeconds, age, stageTime, solarTimer, boost;
    private int tier = -1;
    private double amount;
    private float yaw;

    private static readonly Color GlowGreen = new Color(0.45f, 1f, 0.4f, 1f), GlowSun = new Color(1f, 0.85f, 0.3f, 1f);

    private float StemTop => unit * sproutScale * PixelLooks.SproutStemTop;

    private void OnEnable() => Count++;
    private void OnDisable() => Count = Mathf.Max(0, Count - 1);

    public void Setup(PixelClicker owner, float pixelSize, float grow, float hold, float scale)
    {
        clicker = owner;
        unit = Mathf.Max(0.01f, pixelSize);
        sproutScale = Mathf.Max(1f, scale);
        growSeconds = Mathf.Max(0.5f, grow);
        holdSeconds = Mathf.Max(0f, hold);
        body = GetComponent<Rigidbody>();
        hit = GetComponent<Collider>();
        visual = PixelLooks.CreateSproutObject(transform, unit * sproutScale).transform;

        GameObject lg = new GameObject("Sprout Glow");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, unit * sproutScale * 0.4f, 0f);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = GlowGreen;
        glow.range = unit * 6f;
        glow.intensity = 0.8f;
    }

    /// <summary>Plants the sprout right where it is (no falling): used by the Seed pet when it sows seeds around itself.</summary>
    public void PlantNow() => Plant();

    private void OnCollisionEnter(Collision collision)
    {
        if (stage != Stage.Falling || collision.contactCount == 0) return;
        if (collision.GetContact(0).normal.y > 0.5f) Plant(); // landed on something flat
    }

    private void Update()
    {
        if (clicker == null) { Destroy(gameObject); return; }
        age += Time.deltaTime;

        switch (stage)
        {
            case Stage.Falling:
                if (age > 6f || transform.position.y < -50f) Plant(); // never lands: plant it where it is
                break;

            case Stage.Growing:
            {
                solarTimer -= Time.deltaTime;
                if (solarTimer <= 0f) { solarTimer = 0.3f; boost = clicker.SolarSeedBoostAt(transform.position); }
                stageTime += Time.deltaTime * (1f + boost); // Solar pixels nearby speed it up
                float k = Mathf.Clamp01(stageTime / growSeconds);
                float ease = k * k * (3f - 2f * k);
                if (pixel != null)
                {
                    float size = unit * ease;
                    pixel.transform.localScale = Vector3.one * Mathf.Max(0.0001f, size);
                    pixel.transform.localPosition = new Vector3(0f, StemTop + size * 0.5f, 0f);
                    pixel.transform.localRotation = Quaternion.Euler(0f, stageTime * 50f, 0f);
                }
                AnimatePlant(stageTime, 1f + boost);
                if (k >= 1f) { stage = Stage.Holding; stageTime = 0f; }
                break;
            }

            case Stage.Holding:
            {
                stageTime += Time.deltaTime;
                float shake = Mathf.Clamp01(stageTime / Mathf.Max(0.01f, holdSeconds)); // wobbles harder as it is about to pop
                if (pixel != null)
                {
                    pixel.transform.localRotation = Quaternion.Euler(Mathf.Sin(stageTime * 32f) * 8f * shake, stageTime * 50f, Mathf.Cos(stageTime * 29f) * 8f * shake);
                    pixel.transform.localPosition = new Vector3(0f, StemTop + unit * 0.5f + shake * unit * 0.05f * Mathf.Abs(Mathf.Sin(stageTime * 14f)), 0f);
                }
                if (glow != null) glow.intensity = 0.8f + shake * 1.2f;
                if (stageTime >= holdSeconds) PopOff();
                break;
            }

            case Stage.Shrinking:
            {
                stageTime += Time.deltaTime;
                float k = Mathf.Clamp01(stageTime / 0.5f);
                transform.localScale = Vector3.one * (1f - k);
                if (k >= 1f) Destroy(gameObject);
                break;
            }
        }
    }

    /// <summary>Sway, the mound pushing up and the glow (warmer and brighter while Solar pixels speed it up).</summary>
    private void AnimatePlant(float t, float rate)
    {
        transform.rotation = Quaternion.Euler(Mathf.Sin(t * 3f) * 3f, yaw, Mathf.Cos(t * 2.3f) * 3f);
        if (mound != null)
        {
            float push = Mathf.Clamp01(t / 0.6f);
            mound.localScale = new Vector3(unit * 1.2f * push, unit * 0.24f * push, unit * 1.2f * push);
        }
        if (glow != null)
        {
            glow.color = Color.Lerp(GlowGreen, GlowSun, Mathf.Clamp01(boost / 1.5f));
            glow.intensity = (0.8f + 0.25f * Mathf.Sin(t * 5f)) * (1f + boost * 0.6f);
        }
    }

    /// <summary>Stands the sprout upright, switches off its physics, pushes up a dirt mound and starts growing the pixel.</summary>
    private void Plant()
    {
        if (stage != Stage.Falling) return;
        if (body != null)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = Vector3.zero;
#else
            body.velocity = Vector3.zero;
#endif
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        if (hit != null) hit.enabled = false;
        yaw = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.position += Vector3.down * (unit * 0.03f); // pressed slightly into the ground
        PixelAudio.PlayScaled("pixel_land", 0.6f);
        SeedDigFx.Play(transform.position + Vector3.up * (unit * 0.1f), unit, 0.5f); // a puff of dirt where it lands

        GameObject m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m.name = "Dirt Mound";
        Destroy(m.GetComponent<Collider>());
        m.transform.SetParent(transform, false);
        m.transform.localPosition = new Vector3(0f, unit * 0.02f, 0f);
        m.transform.localScale = Vector3.zero;
        Material dirt = clicker.CreateVisualMaterial(new Color(0.36f, 0.23f, 0.12f, 1f), false);
        Renderer mr = m.GetComponent<Renderer>();
        if (dirt != null) mr.sharedMaterial = dirt; else mr.material.color = new Color(0.36f, 0.23f, 0.12f, 1f);
        mound = m.transform;

        tier = clicker.RandomSeedPixelTier();
        if (tier < 0) { stage = Stage.Shrinking; stageTime = 0f; return; }
        amount = clicker.RollOldPixelAmount(tier);
        pixel = clicker.CreateDisplayPixel(tier, transform, unit);
        if (pixel != null) pixel.transform.localScale = Vector3.one * 0.0001f;
        stage = Stage.Growing;
        stageTime = 0f;
    }

    /// <summary>The grown pixel pops off the sprout as an ordinary old pixel of its type.</summary>
    private void PopOff()
    {
        Vector3 at = pixel != null ? pixel.transform.position : transform.position + Vector3.up * unit;
        if (pixel != null) Destroy(pixel);
        pixel = null;
        Vector2 side = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.2f);
        clicker.SpawnStoredPixel(tier, amount, at, new Vector3(side.x, Random.Range(2.5f, 4f), side.y));
        PixelAudio.Play("click");
        SeedDigFx.Play(at, unit, 0.4f);
        PixelStats.Count("seed.grown");
        stage = Stage.Shrinking;
        stageTime = 0f;
    }
}

/// <summary>
/// The Seed shell's digging effect: a one-shot burst of dirt chunks and drifting dust that fall from the cube (and a puff where a sprout
/// lands). Built from two particle systems in code; the burst destroys itself.
/// </summary>
public static class SeedDigFx
{
    private static Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { material = null; }

    /// <summary>Plays a burst around 'centre' for a cube of edge 'size'; 'strength' 0-1 scales how much falls (the shell cracking open is 1).</summary>
    public static void Play(Vector3 centre, float size, float strength)
    {
        if (material == null)
        {
            Shader shader = PixelShaders.SpriteDefault();
            if (shader == null) return;
            material = new Material(shader) { name = "SeedDirt" };
        }
        strength = Mathf.Clamp01(strength);
        GameObject root = new GameObject("Seed Dig Fx");
        root.transform.position = centre;

        // Dirt: small dark-brown chunks that drop away from the underside.
        ParticleSystem dirt = Build(root.transform, "Dirt", size, material,
                                    lifetime: new Vector2(0.7f, 1.2f), speed: new Vector2(0.4f, 2.2f), startSize: new Vector2(0.05f, 0.11f),
                                    gravity: 2.2f, colorA: new Color(0.30f, 0.19f, 0.09f, 1f), colorB: new Color(0.52f, 0.34f, 0.17f, 1f),
                                    count: Mathf.RoundToInt(Mathf.Lerp(5f, 16f, strength)), fade: false);
        // Dust: larger pale puffs that hang in the air and fade.
        ParticleSystem dust = Build(root.transform, "Dust", size, material,
                                    lifetime: new Vector2(1.0f, 1.7f), speed: new Vector2(0.1f, 0.6f), startSize: new Vector2(0.22f, 0.45f),
                                    gravity: 0.04f, colorA: new Color(0.78f, 0.68f, 0.52f, 0.45f), colorB: new Color(0.65f, 0.55f, 0.42f, 0.35f),
                                    count: Mathf.RoundToInt(Mathf.Lerp(3f, 10f, strength)), fade: true);
        dirt.Play();
        dust.Play();
        Object.Destroy(root, 3.5f);
    }

    private static ParticleSystem Build(Transform parent, string name, float size, Material mat, Vector2 lifetime, Vector2 speed, Vector2 startSize,
                                        float gravity, Color colorA, Color colorB, int count, bool fade)
    {
        GameObject go = new GameObject(name, typeof(ParticleSystem));
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize.x * size, startSize.y * size);
        main.gravityModifier = gravity;
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        // Emitted from a flat box at the underside of the cube, heading down and a little outwards.
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size * 0.9f, 0.02f, size * 0.9f);
        shape.position = new Vector3(0f, -size * 0.35f, 0f);
        shape.rotation = new Vector3(90f, 0f, 0f); // +Z (the emit direction) points down
        shape.randomDirectionAmount = 0.55f;

        if (fade)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.5f)));
        }

        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }
}
