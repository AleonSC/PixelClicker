using UnityEngine;

/// <summary>
/// The Nuclear pixel (a testing pixel): a glowing toxic-green cube with radioactive motes drifting off it. Its OLD pixel is radioactive:
/// every old pixel lying inside its radius ages many times faster, so they vanish within a second or two (no payout) - it clears the
/// floor round itself. Other Nuclear pixels are immune. A pulsing green ring and light show how far it reaches.
/// </summary>
public class OldPixelRadiation : MonoBehaviour
{
    private PixelClicker clicker;
    private float radius, hastenPerSecond;
    private float tick, ringTimer;
    private Light glow;

    public void Setup(PixelClicker owner, float radiusPixels, float ageSecondsPerSecond)
    {
        clicker = owner;
        radius = Mathf.Max(0.5f, radiusPixels) * owner.PixelBaseSize;
        hastenPerSecond = Mathf.Max(1f, ageSecondsPerSecond);
    }

    private void Start()
    {
        GameObject lg = new GameObject("Radiation Glow");
        lg.transform.SetParent(transform, false);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.45f, 1f, 0.2f, 1f);
        glow.range = Mathf.Max(2f, radius * 0.9f);
        glow.intensity = 1.4f;
        glow.shadows = LightShadows.None;
        ringTimer = Random.Range(0f, 0.6f);
    }

    private void Update()
    {
        if (clicker == null) { Destroy(this); return; }
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 7f + GetInstanceID());
        if (glow != null) glow.intensity = 1.4f * flicker;

        ringTimer -= Time.deltaTime;
        if (ringTimer <= 0f)
        {
            ringTimer = 1.4f;
            PixelFxRing.Spawn(transform.position, new Color(0.45f, 1f, 0.2f, 0.7f), radius * 0.15f, radius, 1.1f, 0f);
        }

        tick -= Time.deltaTime;
        if (tick > 0f) return;
        const float step = 0.15f;
        tick = step;
        float sqr = radius * radius;
        Vector3 here = transform.position;
        foreach (Rigidbody body in clicker.OldPixels)
        {
            if (body == null || body.gameObject == gameObject) continue;
            if ((body.position - here).sqrMagnitude > sqr) continue;
            OldPixelRadiation other = body.GetComponent<OldPixelRadiation>();
            if (other != null) continue;   // Nuclear pixels are immune to each other
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.Hasten(hastenPerSecond * step);
        }
    }
}

/// <summary>The Nuclear look's radioactive motes: little glowing green squares drifting up off the cube.</summary>
public static class PixelRadiationFx
{
    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        if (PixelFx.Material == null) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        ParticleSystem motes = PixelFx.Make(parent, "Radiation", new PixelFx.Spec
        {
            lifetime = new Vector2(0.9f, 1.8f), speed = new Vector2(0.08f, 0.35f) * unit, size = new Vector2(0.1f, 0.2f) * unit,
            gravity = -0.12f, colorA = new Color(0.7f, 1f, 0.3f, 1f), colorB = new Color(0.3f, 0.95f, 0.15f, 1f),
            rate = 14f, loop = true, shape = ParticleSystemShapeType.Box, shapeRadius = 0f,
            shapePosition = centre, randomDirection = 0.6f,
            sizeOverLife = new Vector2(1f, 0.2f), maxParticles = 60,
            overLife = PixelFx.FadeGradient(new Color(0.85f, 1f, 0.5f), new Color(0.4f, 1f, 0.15f), new Color(0.1f, 0.4f, 0.05f), 0.9f),
        });
        ParticleSystem.ShapeModule shape = motes.shape;
        shape.scale = size * 0.9f;
        motes.Play();
    }
}
