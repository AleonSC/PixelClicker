using UnityEngine;

/// <summary>
/// The Treasure chest's effects: gold sparks and splinters with every hit (more as it nears opening), and a fountain of gold coins and
/// glitter, a gold shock ring and a flash when it bursts open. Sounds <c>chest_hit</c> / <c>chest_open</c> (code-made until you give them clips).
/// </summary>
public static class TreasureFx
{
    private static readonly Color Gold = new Color(1f, 0.8f, 0.25f, 1f);
    private static readonly Color PaleGold = new Color(1f, 0.95f, 0.65f, 1f);
    private static readonly Color Wood = new Color(0.5f, 0.3f, 0.14f, 1f);

    /// <summary>A hit that doesn't open the chest: splinters and a few gold sparks (progress 0-1 = how close it is to opening).</summary>
    public static void Hit(Vector3 centre, float size, float progress)
    {
        if (PixelFx.Material == null) return;
        GameObject root = new GameObject("Chest Hit");
        root.transform.position = centre;
        ParticleSystem splinters = PixelFx.Make(root.transform, "Splinters", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 0.9f), speed = new Vector2(1.2f, 3f) * size, size = new Vector2(0.04f, 0.1f) * size,
            gravity = 2.4f, colorA = Wood, colorB = new Color(0.32f, 0.18f, 0.07f, 1f),
            burst = 6, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.15f,
            sizeOverLife = new Vector2(1f, 0.6f), maxParticles = 30,
        });
        ParticleSystem sparks = PixelFx.Make(root.transform, "Sparks", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 1f), speed = new Vector2(0.8f, 2.6f) * size, size = new Vector2(0.03f, 0.08f) * size,
            gravity = -0.1f, colorA = PaleGold, colorB = Gold,
            burst = Mathf.RoundToInt(Mathf.Lerp(3f, 16f, Mathf.Clamp01(progress))), shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f,
            randomDirection = 0.2f, sizeOverLife = new Vector2(1f, 0.2f), maxParticles = 40,
            overLife = PixelFx.FadeGradient(Color.white, PaleGold, Gold, 1f),
        });
        splinters.Play();
        sparks.Play();
        Object.Destroy(root, 2f);
        PixelAudio.PlayScaled("chest_hit", Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(progress)));
    }

    /// <summary>The chest bursts open: a fountain of coins, glitter and a gold ring; a bigger chest (more clicks) makes a bigger show.</summary>
    public static void Open(Vector3 centre, float size, int clicks)
    {
        if (PixelFx.Material == null) return;
        float big = Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01(clicks / 20f));
        GameObject root = new GameObject("Chest Open");
        root.transform.position = centre;

        ParticleSystem coins = PixelFx.Make(root.transform, "Coins", new PixelFx.Spec
        {
            lifetime = new Vector2(0.9f, 1.6f), speed = new Vector2(2.5f, 6f) * size * big, size = new Vector2(0.08f, 0.17f) * size,
            gravity = 1.6f, colorA = Gold, colorB = new Color(1f, 0.6f, 0.1f, 1f),
            burst = Mathf.RoundToInt(30f * big), shape = ParticleSystemShapeType.Cone, shapeRadius = size * 0.4f, shapeAngle = 40f,
            shapeRotation = new Vector3(-90f, 0f, 0f), randomDirection = 0.2f, sizeOverLife = new Vector2(1f, 0.5f), maxParticles = 120,
        });
        ParticleSystem glitter = PixelFx.Make(root.transform, "Glitter", new PixelFx.Spec
        {
            lifetime = new Vector2(0.8f, 1.8f), speed = new Vector2(0.6f, 3.2f) * size * big, size = new Vector2(0.03f, 0.09f) * size,
            gravity = 0.2f, colorA = Color.white, colorB = PaleGold,
            burst = Mathf.RoundToInt(50f * big), shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.6f, randomDirection = 0.3f,
            sizeOverLife = new Vector2(1f, 0.1f), maxParticles = 160,
            overLife = PixelFx.FadeGradient(Color.white, PaleGold, Gold, 1f),
        });
        ParticleSystem splinters = PixelFx.Make(root.transform, "Splinters", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.2f), speed = new Vector2(1.5f, 4f) * size, size = new Vector2(0.06f, 0.14f) * size,
            gravity = 2.6f, colorA = Wood, colorB = new Color(0.3f, 0.17f, 0.07f, 1f),
            burst = 20, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f, randomDirection = 0.1f, sizeOverLife = new Vector2(1f, 0.6f), maxParticles = 50,
        });
        coins.Play(); glitter.Play(); splinters.Play();
        PixelFxRing.Spawn(centre + Vector3.down * size * 0.35f, new Color(1f, 0.85f, 0.3f, 0.9f), size * 0.5f, size * 3f * big, 0.5f, 8f);
        Object.Destroy(root, 3f);
        PixelAudio.Play("chest_open");
    }
}
