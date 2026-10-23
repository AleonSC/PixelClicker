// ---- System notes (moved here from CLAUDE.md) ----
// PixelSprinklerDevice.cs
//   (extends PixelPlacedDevice) Sprinkler (DeviceKind.Sprinkler 9, auto-added like the fan by EnsureDefaultPotions, Utilities card;
//   needs Water unlocked, 40 Water pixels, durationSeconds 45, radius 4.5, Device.sprayInterval 1): BuildModel in this file (base, post,
//   spinning 'Spinner' head with 'Nozzle 0/1'), droplet jets (PixelFx.Make per nozzle) and every sprayInterval s
//   PixelClicker.WaterSplashAt(point, reachOverride) - the same instant growth jump a splashing Water pixel gives every sprout /
//   planted Seed pet in reach (each target still accepts water only every seedWaterCooldown); placed like the vacuum (starts disarmed,
//   click to switch on), robot-placeable (IsPlaceableKind), tip device_Sprinkler, not part of the Operation tab yet.
// ---------------------------------------------------
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
        if (sprayTimer <= 0f && !IsEnding && Armed && clicker != null)
        {
            sprayTimer = interval;
            clicker.WaterSplashAt(transform.position + Vector3.up * 0.2f, radius);
            PixelAudio.PlayScaled("water_splash", 0.18f);
        }
    }

    // ---- the device card (PixelDeviceCard) ----
    protected override void AddInfo(PixelDeviceInfo info)
    {
        info.Add("Waters", "within " + radius.ToString("0.#") + " units, every " + interval.ToString("0.#") + " s");
    }

    // ---- The model (moved here from PixelConsumables 2026-10-24: each device builds its own look; PixelConsumables.BuildDeviceObject calls it) ----

    /// <summary>A flat base, a short post and a spinning head with two nozzles ("Spinner" with "Nozzle 0/1" children, used by <see cref="PixelSprinklerDevice"/>).</summary>
    public static void BuildModel(PixelClicker clicker, GameObject root, PixelConsumables.Device d, bool isPreview, float previewOpacity)
    {
        float opacity = isPreview ? previewOpacity : 1f;
        Color main = d.color; main.a = opacity;
        Color metal = new Color(0.62f, 0.66f, 0.72f, opacity);
        float H = d.bodyHeight;

        GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            Material m = clicker.CreateVisualMaterial(color, isPreview);
            if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(d.bodyDiameter * 1.4f, 0.04f, d.bodyDiameter * 1.4f), metal);
        Part(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, H * 0.5f, 0f), new Vector3(d.bodyDiameter * 0.28f, H * 0.5f, d.bodyDiameter * 0.28f), main);
        Transform spinner = new GameObject("Spinner").transform;
        spinner.SetParent(root.transform, false);
        spinner.localPosition = new Vector3(0f, H, 0f);
        Part(PrimitiveType.Sphere, "Hub", spinner, Vector3.zero, Vector3.one * d.bodyDiameter * 0.55f, main);
        Part(PrimitiveType.Cube, "Arm", spinner, Vector3.zero, new Vector3(d.bodyDiameter * 1.5f, d.bodyDiameter * 0.14f, d.bodyDiameter * 0.14f), metal);
        for (int i = 0; i < 2; i++)
        {
            GameObject nozzle = Part(PrimitiveType.Sphere, "Nozzle " + i, spinner, new Vector3((i == 0 ? 1f : -1f) * d.bodyDiameter * 0.75f, 0f, 0f), Vector3.one * d.bodyDiameter * 0.2f, metal);
        }
    }
}
