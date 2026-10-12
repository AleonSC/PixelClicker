using UnityEngine;

/// <summary>
/// Falling rain for the rainy skies (partial class PixelSkybox): while a style with the Rain pattern (Rainy Day, Thunderstorm) is the sky, a particle
/// system of long thin streaks falls through the scene in front of the camera. The sky picture itself is still; this is what moves.
/// Sized from the camera's distance to the cube, so it looks the same however the scene is scaled.
/// </summary>
public partial class PixelSkybox
{
    [Header("Rain (the rainy skies)")]
    [Tooltip("Turns the falling rain off (the rainy skies are then just pictures).")]
    [SerializeField] private bool disableRain = false;

    [Min(0f)]
    [Tooltip("Rain drops per second in a light rain. 0 = the default (650); a style with lightning gets twice as many.")]
    [SerializeField] private float rainPerSecond = 0f;

    private GameObject rainRoot;
    private ParticleSystem rainSystem;
    private Material rainMaterial;

    private void UpdateRain(Camera cam)
    {
        bool want = !disableRain && cam != null && current >= 0 && current < styles.Count && styles[current].pattern == SkyPattern.Rain;
        if (!want)
        {
            if (rainRoot != null && rainRoot.activeSelf) rainRoot.SetActive(false);
            return;
        }
        if (rainRoot == null) BuildRain();
        if (rainRoot == null) return;
        if (!rainRoot.activeSelf) rainRoot.SetActive(true);

        PixelClicker clicker = PixelFind.First<PixelClicker>();
        Vector3 target = clicker != null && clicker.PixelTransform != null ? clicker.CubeBasePosition : cam.transform.position + cam.transform.forward * 10f;
        // While orbiting, size the rain from the normal view so it doesn't swell and shrink.
        Vector3 eye = PixelOrbitCamera.HomeCamera != null ? PixelOrbitCamera.HomeCamera.transform.position : cam.transform.position;
        float reach = Mathf.Max(3f, Vector3.Distance(eye, target));

        rainRoot.transform.position = target + Vector3.up * reach * 0.9f;
        rainRoot.transform.rotation = Quaternion.identity;

        bool storm = styles[current].flashesPerMinute > 0f;
        float speed = reach * 2.6f;
        ParticleSystem.MainModule main = rainSystem.main;
        main.startSpeed = speed;
        main.startLifetime = Mathf.Max(0.2f, (reach * 1.0f) / speed);
        main.startSize = reach * 0.008f;
        ParticleSystem.EmissionModule emission = rainSystem.emission;
        emission.rateOverTime = (rainPerSecond > 0f ? rainPerSecond : 650f) * (storm ? 2f : 1f);
        ParticleSystem.ShapeModule shape = rainSystem.shape;
        shape.scale = new Vector3(reach * 2.6f, reach * 2.6f, 0.1f);   // (the box is turned flat: local X / Y spread the drops sideways, local Z is the thin fall direction)
        ParticleSystem.VelocityOverLifetimeModule vel = rainSystem.velocityOverLifetime;
        vel.x = new ParticleSystem.MinMaxCurve(reach * (storm ? 0.45f : 0.15f));
        ParticleSystemRenderer pr = rainSystem.GetComponent<ParticleSystemRenderer>();
        pr.lengthScale = 5f + reach * 0.15f;
    }

    private void BuildRain()
    {
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return;
        rainRoot = new GameObject("Sky Rain");
        rainRoot.hideFlags = HideFlags.DontSave;
        rainSystem = rainRoot.AddComponent<ParticleSystem>();
        rainSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = rainSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = 4000;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.85f, 1f, 0.3f), new Color(0.9f, 0.95f, 1f, 0.55f));
        main.gravityModifier = 0f;

        ParticleSystem.ShapeModule shape = rainSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.rotation = new Vector3(90f, 0f, 0f);   // (the box is flat: drops start in a sheet and fall straight down)

        ParticleSystem.VelocityOverLifetimeModule vel = rainSystem.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystemRenderer pr = rainSystem.GetComponent<ParticleSystemRenderer>();
        rainMaterial = new Material(shader) { name = "Sky Rain" };
        pr.sharedMaterial = rainMaterial;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.02f;
        pr.lengthScale = 6f;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        rainSystem.Play();
    }

    private void DestroyRain()
    {
        if (rainRoot != null) Destroy(rainRoot);
        if (rainMaterial != null) Destroy(rainMaterial);
    }
}
