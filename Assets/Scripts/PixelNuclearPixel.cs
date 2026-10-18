// ---- System notes (moved here from CLAUDE.md) ----
// PixelNuclearPixel.cs
//   Nuclear pixel (PixelType.Nuclear 33, IsStandalone (no seeds, no 3-type combo potions), spawn
//   switch in Toggles; testing pack 'Nuclear Pixel': 10 White, no requirements, spawn weight 0.3, pays 8; looksVersion 27): glowing
//   toxic-green look with PixelLook.radiation (PixelRadiationFx.Attach: drifting green motes). Its OLD pixel gets OldPixelRadiation
//   (added in SpawnFallingCopy; nuclearRadiusPixels 0 = 4 main-pixel widths, nuclearAgeRate 0 = 40 age-seconds per real second,
//   disableNuclearFx): every 0.15 s it calls OldPixelDespawn.Hasten(seconds) on every other old pixel in range (not other Nuclear
//   pixels; held pixels, black holes, takeover minigames and the dev no-despawn switch still protect; gravity-well keep-alive does
//   NOT), so they vanish (no payout) within a second or two; a pulsing green ring (PixelFxRing) and light show the reach.
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// The Nuclear pixel (a testing pixel): a glowing toxic-green cube with radioactive motes drifting off it. Its OLD pixel is radioactive:
/// every old pixel lying inside its radius ages many times faster, so they vanish within a second or two (no payout) - it clears the
/// floor round itself. Other Nuclear pixels are immune. A pulsing green ring and light show how far it reaches.
/// </summary>
public class OldPixelRadiation : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<PixelHuskQuery.Husk> nearby = new System.Collections.Generic.List<PixelHuskQuery.Husk>();
    private PixelClicker clicker;
    private float radius, hastenPerSecond;
    private float tick, ringTimer;
    private Light glow;

    // The bodies of the Nuclear husks (they are immune to each other): a set lookup instead of a GetComponent per nearby husk per tick.
    private static readonly System.Collections.Generic.HashSet<GameObject> radiating = new System.Collections.Generic.HashSet<GameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => radiating.Clear();

    private void Awake() => radiating.Add(gameObject);
    private void OnDestroy() => radiating.Remove(gameObject);   // (also when PixelHuskPool strips a reused husk)

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
        Vector3 here = transform.position;
        PixelHuskQuery.Near(clicker, here, radius, nearby, false, true);
        foreach (PixelHuskQuery.Husk h in nearby)
        {
            if (h.body.gameObject == gameObject) continue;
            if (radiating.Contains(h.body.gameObject)) continue;   // Nuclear pixels are immune to each other
            if (h.despawn != null) h.despawn.Hasten(hastenPerSecond * step);
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
