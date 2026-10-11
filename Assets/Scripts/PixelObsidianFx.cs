using UnityEngine;

/// <summary>
/// Extra flair for the Obsidian pixel: violet sparks drifting off the cube while it sits there (<see cref="Attach"/>, driven by
/// <see cref="PixelLook.obsidianFx"/>), glassy shards and a violet flash on every non-final hit (<see cref="Hit"/>, stronger as it nears
/// breaking) and a big shatter with a ring when it breaks (<see cref="Break"/>). Built from <see cref="PixelFx"/>; all code-made.
/// </summary>
public static class PixelObsidianFx
{
    private static readonly Color Violet = new Color(0.65f, 0.35f, 1f, 1f);
    private static readonly Color Pale = new Color(0.9f, 0.8f, 1f, 1f);

    public static void Attach(Transform parent, Vector3 centre, Vector3 size)
    {
        if (PixelFx.Material == null) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        PixelFx.Spec s = new PixelFx.Spec
        {
            loop = true, shape = ParticleSystemShapeType.Box, shapePosition = centre, randomDirection = 0.6f, maxParticles = 50,
            lifetime = new Vector2(0.8f, 1.6f), speed = new Vector2(0.05f, 0.3f) * unit, size = new Vector2(0.04f, 0.1f) * unit,
            gravity = -0.08f, colorA = Pale, colorB = Violet, rate = 10f, sizeOverLife = new Vector2(1f, 0f),
            overLife = PixelFx.FadeGradient(Color.white, new Color(0.8f, 0.6f, 1f), new Color(0.4f, 0.15f, 0.8f), 1f),
        };
        ParticleSystem ps = PixelFx.Make(parent, "Obsidian Sparks", s);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.scale = size * 1.05f;
        ps.Play();
    }

    private static GameObject Root(string name, Vector3 centre)
    {
        GameObject root = new GameObject(name);
        root.transform.position = centre;
        Object.Destroy(root, 3.5f);
        return root;
    }

    /// <summary>A non-final hit (progress 0-1): dark glassy shards and violet glints; more of them the closer it is to breaking.</summary>
    public static void Hit(Vector3 centre, float size, float progress)
    {
        if (PixelFx.Material == null) return;
        GameObject root = Root("Obsidian Hit", centre);
        PixelFx.Make(root.transform, "Shards", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 0.9f), speed = new Vector2(1f, 3f) * size, size = new Vector2(0.04f, 0.1f) * size, gravity = 2.4f,
            colorA = new Color(0.12f, 0.08f, 0.2f), colorB = new Color(0.3f, 0.2f, 0.45f), burst = Mathf.RoundToInt(Mathf.Lerp(5f, 12f, progress)),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.1f, maxParticles = 20,
        }).Play();
        PixelFx.Make(root.transform, "Glints", new PixelFx.Spec
        {
            lifetime = new Vector2(0.3f, 0.7f), speed = new Vector2(0.6f, 2.2f) * size, size = new Vector2(0.05f, 0.12f) * size, gravity = -0.1f,
            colorA = Pale, colorB = Violet, burst = Mathf.RoundToInt(Mathf.Lerp(3f, 10f, progress)), sizeOverLife = new Vector2(1f, 0f),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f, randomDirection = 0.1f, maxParticles = 20,
            overLife = PixelFx.FadeGradient(Color.white, Pale, Violet, 1f),
        }).Play();
        if (progress > 0.5f) PixelFxRing.Spawn(centre, new Color(0.6f, 0.35f, 1f, 0.5f * progress), size * 0.5f, size * 1.6f, 0.3f, 0f);
    }

    /// <summary>The final break: a spray of black glass, a violet burst and an expanding ring.</summary>
    public static void Break(Vector3 centre, float size)
    {
        if (PixelFx.Material == null) return;
        GameObject root = Root("Obsidian Break", centre);
        PixelFx.Make(root.transform, "Glass", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.3f), speed = new Vector2(1.5f, 4.5f) * size, size = new Vector2(0.05f, 0.15f) * size, gravity = 2.6f,
            colorA = new Color(0.1f, 0.06f, 0.18f), colorB = new Color(0.35f, 0.22f, 0.55f), burst = 34,
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.1f, maxParticles = 44,
        }).Play();
        PixelFx.Make(root.transform, "Violet", new PixelFx.Spec
        {
            lifetime = new Vector2(0.8f, 1.6f), speed = new Vector2(0.8f, 3.2f) * size, size = new Vector2(0.06f, 0.16f) * size, gravity = -0.1f,
            colorA = Pale, colorB = Violet, burst = 40, sizeOverLife = new Vector2(1f, 0f),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.6f, randomDirection = 0.1f, maxParticles = 50,
            overLife = PixelFx.FadeGradient(Color.white, new Color(0.8f, 0.6f, 1f), new Color(0.4f, 0.15f, 0.8f), 1f),
        }).Play();
        PixelFxRing.Spawn(centre, new Color(0.65f, 0.4f, 1f, 0.8f), size * 0.4f, size * 3.4f, 0.55f, 2.5f);
    }
}
