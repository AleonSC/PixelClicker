using UnityEngine;
using TMPro;

/// <summary>
/// A placed Sprinkler (device <c>kind</c> = Sprinkler): a short post with a spinning head that sprays droplets. While it is switched on it
/// waters every growing Seed sprout and planted Seed pet within its radius once every <c>sprayInterval</c> seconds (the same instant growth jump a
/// splashing Water pixel gives, see <see cref="PixelClicker.WaterSplashAt"/>), so seeds grow far faster. The countdown, timer text and
/// shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelSprinklerDevice : PixelPlacedDevice
{
    private float radius;
    private float interval = 1f;
    private float sprayTimer;
    private float spinAngle;
    private Transform spinner;
    private ParticleSystem[] sprays;

    public void Init(PixelClicker owner, Camera camera, TextMeshPro timer, string format, float duration, float shrinkTime,
                     float reach, float sprayInterval)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        radius = reach;
        interval = sprayInterval > 0.1f ? sprayInterval : 1f;
        spinner = transform.Find("Spinner");
        if (PixelFx.Material != null && spinner != null)
        {
            // Two jets, one from each nozzle, thrown outwards and up; the droplets fall back on their own.
            sprays = new ParticleSystem[2];
            for (int i = 0; i < 2; i++)
            {
                Transform nozzle = spinner.Find("Nozzle " + i);
                if (nozzle == null) continue;
                sprays[i] = PixelFx.Make(nozzle, "Spray", new PixelFx.Spec
                {
                    lifetime = new Vector2(0.55f, 0.9f), speed = new Vector2(1.8f, 2.8f), size = new Vector2(0.05f, 0.1f),
                    gravity = 1.1f, colorA = new Color(0.75f, 0.93f, 1f, 0.9f), colorB = new Color(0.35f, 0.7f, 1f, 0.9f),
                    rate = 45f, loop = true, shape = ParticleSystemShapeType.Cone, shapeRadius = 0.02f, shapeAngle = 14f,
                    shapeRotation = new Vector3(-50f, i == 0 ? 90f : -90f, 0f), randomDirection = 0.1f, sizeOverLife = new Vector2(1f, 0.5f),
                    maxParticles = 80,
                });
            }
        }
    }

    protected override void OnTick()
    {
        // The head spins and the jets run while it works (OnTick only runs once it is switched on).
        if (spinner != null)
        {
            spinAngle += 200f * Time.deltaTime;
            spinner.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
        }
        if (sprays != null)
            foreach (ParticleSystem ps in sprays)
                if (ps != null && !ps.isPlaying) ps.Play();

        sprayTimer -= Time.deltaTime;
        if (sprayTimer <= 0f && !IsDying && Armed && clicker != null)
        {
            sprayTimer = interval;
            clicker.WaterSplashAt(transform.position + Vector3.up * 0.2f, radius);
            PixelAudio.PlayScaled("water_splash", 0.18f);
        }
    }
}
