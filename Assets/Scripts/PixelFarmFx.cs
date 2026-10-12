using UnityEngine;

/// <summary>
/// Particles for the Farming pixels (Dirt, Seed, Water, Fertilizer, Pollen, Wheat, Weevil, Golden Fruit): each block has its own ambient effect
/// while it sits on the cube (<see cref="Attach"/>, driven by <see cref="PixelLook.farmFx"/>), a small effect on every non-final hit (<see cref="Hit"/>)
/// and a bigger burst when it breaks (<see cref="Break"/>). Built from the shared particle helper <see cref="PixelFx"/>; all code-made, no assets.
/// </summary>
public static class PixelFarmFx
{
    /// <summary>The ambient effect of a farm look: a loop of little particles around the cube.</summary>
    public static void Attach(Transform parent, Vector3 centre, Vector3 size, PixelLook.FarmAmbient kind)
    {
        if (PixelFx.Material == null || kind == PixelLook.FarmAmbient.None) return;
        float unit = Mathf.Max(0.05f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        PixelFx.Spec s = new PixelFx.Spec { loop = true, shape = ParticleSystemShapeType.Box, shapePosition = centre, randomDirection = 0.6f, maxParticles = 60 };
        Vector3 boxScale = size * 0.9f;
        string name = "Farm " + kind;
        switch (kind)
        {
            case PixelLook.FarmAmbient.Pollen:
                s.lifetime = new Vector2(1.4f, 2.6f); s.speed = new Vector2(0.05f, 0.25f) * unit; s.size = new Vector2(0.07f, 0.14f) * unit;
                s.gravity = -0.05f; s.colorA = new Color(1f, 0.95f, 0.45f, 1f); s.colorB = new Color(1f, 0.8f, 0.2f, 1f); s.rate = 16f;
                s.sizeOverLife = new Vector2(1f, 0.3f);
                s.overLife = PixelFx.FadeGradient(new Color(1f, 1f, 0.7f), new Color(1f, 0.9f, 0.35f), new Color(1f, 0.7f, 0.15f), 0.95f);
                break;
            case PixelLook.FarmAmbient.Dew:
                s.lifetime = new Vector2(0.7f, 1.2f); s.speed = new Vector2(0.02f, 0.12f) * unit; s.size = new Vector2(0.05f, 0.1f) * unit;
                s.gravity = 0.9f; s.colorA = new Color(0.7f, 0.9f, 1f, 0.9f); s.colorB = new Color(0.35f, 0.7f, 1f, 0.9f); s.rate = 7f;
                s.sizeOverLife = new Vector2(1f, 0.6f);
                break;
            case PixelLook.FarmAmbient.Crumbs:
                s.lifetime = new Vector2(0.8f, 1.4f); s.speed = new Vector2(0.02f, 0.15f) * unit; s.size = new Vector2(0.04f, 0.09f) * unit;
                s.gravity = 1.3f; s.colorA = new Color(0.45f, 0.3f, 0.16f, 1f); s.colorB = new Color(0.3f, 0.2f, 0.1f, 1f); s.rate = 5f;
                break;
            case PixelLook.FarmAmbient.Spores:
                s.lifetime = new Vector2(1.2f, 2.2f); s.speed = new Vector2(0.05f, 0.22f) * unit; s.size = new Vector2(0.05f, 0.11f) * unit;
                s.gravity = -0.1f; s.colorA = new Color(0.6f, 0.95f, 0.3f, 1f); s.colorB = new Color(0.35f, 0.8f, 0.2f, 1f); s.rate = 12f;
                s.sizeOverLife = new Vector2(1f, 0.2f);
                s.overLife = PixelFx.FadeGradient(new Color(0.8f, 1f, 0.5f), new Color(0.5f, 0.9f, 0.25f), new Color(0.2f, 0.5f, 0.1f), 0.9f);
                break;
            case PixelLook.FarmAmbient.Chaff:
                s.lifetime = new Vector2(1.2f, 2f); s.speed = new Vector2(0.05f, 0.3f) * unit; s.size = new Vector2(0.04f, 0.1f) * unit;
                s.gravity = 0.15f; s.colorA = new Color(0.97f, 0.85f, 0.45f, 1f); s.colorB = new Color(0.85f, 0.65f, 0.25f, 1f); s.rate = 8f;
                break;
            case PixelLook.FarmAmbient.Sparkle:
                s.lifetime = new Vector2(0.7f, 1.4f); s.speed = new Vector2(0.1f, 0.5f) * unit; s.size = new Vector2(0.05f, 0.13f) * unit;
                s.gravity = -0.1f; s.colorA = new Color(1f, 0.98f, 0.7f, 1f); s.colorB = new Color(1f, 0.8f, 0.25f, 1f); s.rate = 24f;
                s.sizeOverLife = new Vector2(1f, 0f); s.maxParticles = 80;
                s.overLife = PixelFx.FadeGradient(Color.white, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.65f, 0.1f), 1f);
                break;
            case PixelLook.FarmAmbient.Leaves:
                s.lifetime = new Vector2(1.2f, 2f); s.speed = new Vector2(0.03f, 0.14f) * unit; s.size = new Vector2(0.07f, 0.13f) * unit;
                s.gravity = 0.25f; s.colorA = new Color(0.5f, 0.88f, 0.35f, 1f); s.colorB = new Color(0.25f, 0.62f, 0.2f, 1f); s.rate = 6f;
                break;
            case PixelLook.FarmAmbient.Sap:
                s.lifetime = new Vector2(0.7f, 1.1f); s.speed = new Vector2(0.01f, 0.08f) * unit; s.size = new Vector2(0.05f, 0.1f) * unit;
                s.gravity = 0.9f; s.colorA = new Color(1f, 0.78f, 0.25f, 0.95f); s.colorB = new Color(0.9f, 0.55f, 0.1f, 0.95f); s.rate = 4f;
                break;
            case PixelLook.FarmAmbient.Hornets:
                // Yellow and dark specks buzzing round the nest.
                s.lifetime = new Vector2(0.5f, 1f); s.speed = new Vector2(0.6f, 1.5f) * unit; s.size = new Vector2(0.05f, 0.09f) * unit;
                s.gravity = 0f; s.colorA = new Color(1f, 0.85f, 0.15f, 1f); s.colorB = new Color(0.12f, 0.1f, 0.05f, 1f); s.rate = 12f;
                boxScale = size * 1.4f;
                break;
            case PixelLook.FarmAmbient.Bugs:
                // Tiny dark specks scurrying about along the bottom of the cube.
                s.lifetime = new Vector2(0.5f, 1f); s.speed = new Vector2(0.3f, 0.8f) * unit; s.size = new Vector2(0.05f, 0.09f) * unit;
                s.gravity = 0f; s.colorA = new Color(0.12f, 0.08f, 0.06f, 1f); s.colorB = new Color(0.3f, 0.2f, 0.1f, 1f); s.rate = 8f;
                s.shapePosition = centre + Vector3.down * size.y * 0.48f; boxScale = new Vector3(size.x, 0.05f * unit, size.z);
                break;
        }
        ParticleSystem ps = PixelFx.Make(parent, name, s);
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.scale = boxScale;
        ps.Play();
    }

    private static GameObject Root(string name, Vector3 centre)
    {
        GameObject root = new GameObject(name);
        root.transform.position = centre;
        Object.Destroy(root, 3.5f);
        return root;
    }

    /// <summary>A small effect on a non-final hit of a tough farm block (progress 0-1).</summary>
    public static void Hit(PixelClicker.PixelType type, Vector3 centre, float size, Color color, float progress)
    {
        if (PixelFx.Material == null) return;
        GameObject root = Root("Farm Hit", centre);
        PixelFx.Make(root.transform, "Bits", new PixelFx.Spec
        {
            lifetime = new Vector2(0.5f, 0.9f), speed = new Vector2(0.8f, 2.4f) * size, size = new Vector2(0.05f, 0.11f) * size,
            gravity = 2.2f, colorA = color, colorB = Color.Lerp(color, Color.black, 0.4f), burst = Mathf.RoundToInt(Mathf.Lerp(4f, 9f, progress)),
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.1f, maxParticles = 20,
        }).Play();
        PixelAudio.Play(PixelClicker.IsWoodBlock(type) ? "wood_chop" : PixelClicker.IsForageBlock(type) || type == PixelClicker.PixelType.Toadstool ? "forage_pick" : PixelClicker.IsFishBlock(type) || type == PixelClicker.PixelType.Jellyfish ? "fish_splash" : "farm_hit");
    }

    /// <summary>The burst when a farm block breaks: a mix of bits, puffs and glints that suits the block.</summary>
    public static void Break(PixelClicker.PixelType type, Vector3 centre, float size, Color color)
    {
        if (PixelFx.Material == null) return;
        GameObject root = Root("Farm Break", centre);
        Transform t = root.transform;
        ParticleSystem Burst(string name, Vector2 life, Vector2 speed, Vector2 psize, float gravity, Color a, Color b, int count, float radius, bool fade, Vector2 sizeLife = default)
        {
            PixelFx.Spec s = new PixelFx.Spec
            {
                lifetime = life, speed = speed * size, size = psize * size, gravity = gravity, colorA = a, colorB = b, burst = count,
                shape = ParticleSystemShapeType.Sphere, shapeRadius = size * radius, randomDirection = 0.1f, maxParticles = count + 10, sizeOverLife = sizeLife,
            };
            if (fade) s.overLife = PixelFx.FadeGradient(Color.white, Color.white, Color.white, 0.85f);
            return PixelFx.Make(t, name, s);
        }

        switch (type)
        {
            case PixelClicker.PixelType.Dirt:
                Burst("Clods", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3.4f), new Vector2(0.07f, 0.16f), 2.6f, new Color(0.45f, 0.3f, 0.17f), new Color(0.28f, 0.18f, 0.1f), 22, 0.5f, false).Play();
                Burst("Dust", new Vector2(0.9f, 1.5f), new Vector2(0.2f, 1f), new Vector2(0.25f, 0.5f), -0.04f, new Color(0.62f, 0.5f, 0.38f, 0.5f), new Color(0.5f, 0.4f, 0.3f, 0.4f), 8, 0.5f, true, new Vector2(0.6f, 1.6f)).Play();
                break;
            case PixelClicker.PixelType.Water:
            {
                // A splash instead of a ring: a crown of drops flung up and out, a thin jet of bigger drops, a few bubbles and a little mist.
                Color deep = new Color(0.25f, 0.55f, 0.95f), lightWater = new Color(0.75f, 0.92f, 1f);
                ParticleSystem Splash(string name, Vector2 life, Vector2 speed, Vector2 psize, float gravity, Color ca, Color cb, int count, float radius, float angle, bool fade, Vector2 sizeLife = default)
                {
                    PixelFx.Spec sp = new PixelFx.Spec
                    {
                        lifetime = life, speed = speed * size, size = psize * size, gravity = gravity, colorA = ca, colorB = cb, burst = count,
                        shape = ParticleSystemShapeType.Cone, shapeRadius = size * radius, shapeAngle = angle, shapeRotation = new Vector3(-90f, 0f, 0f),
                        randomDirection = 0.05f, maxParticles = count + 6, sizeOverLife = sizeLife,
                    };
                    if (fade) sp.overLife = PixelFx.FadeGradient(Color.white, Color.white, Color.white, 0.8f);
                    return PixelFx.Make(t, name, sp);
                }
                Splash("Crown", new Vector2(0.55f, 0.95f), new Vector2(1.6f, 3.2f), new Vector2(0.06f, 0.13f), 3.2f, lightWater, deep, 22, 0.4f, 58f, false).Play();
                Splash("Jet", new Vector2(0.7f, 1.1f), new Vector2(3.2f, 4.6f), new Vector2(0.08f, 0.15f), 3.6f, lightWater, deep, 5, 0.1f, 6f, false).Play();
                Splash("Bubbles", new Vector2(0.6f, 1.1f), new Vector2(0.25f, 0.7f), new Vector2(0.05f, 0.1f), -0.35f, new Color(0.85f, 0.95f, 1f, 0.8f), new Color(0.6f, 0.85f, 1f, 0.6f), 7, 0.45f, 40f, true, new Vector2(1f, 0.3f)).Play();
                Splash("Mist", new Vector2(0.7f, 1.1f), new Vector2(0.4f, 1.1f), new Vector2(0.22f, 0.4f), -0.02f, new Color(0.75f, 0.9f, 1f, 0.35f), new Color(0.6f, 0.82f, 1f, 0.25f), 6, 0.45f, 70f, true, new Vector2(0.7f, 1.5f)).Play();
                break;
            }
            case PixelClicker.PixelType.Fertilizer:
                Burst("Spores", new Vector2(1f, 1.8f), new Vector2(0.6f, 2.2f), new Vector2(0.06f, 0.14f), -0.15f, new Color(0.7f, 1f, 0.4f), new Color(0.35f, 0.8f, 0.2f), 30, 0.55f, true, new Vector2(1f, 0.2f)).Play();
                Burst("Clumps", new Vector2(0.6f, 1f), new Vector2(1f, 3f), new Vector2(0.06f, 0.12f), 2.5f, new Color(0.45f, 0.35f, 0.15f), new Color(0.3f, 0.5f, 0.15f), 12, 0.5f, false).Play();
                PixelFxRing.Spawn(centre, new Color(0.6f, 1f, 0.35f, 0.6f), size * 0.5f, size * 3.5f, 0.6f, 0f);
                break;
            case PixelClicker.PixelType.Pollen:
                Burst("Cloud", new Vector2(1.2f, 2.2f), new Vector2(0.3f, 1.4f), new Vector2(0.15f, 0.35f), -0.05f, new Color(1f, 0.92f, 0.4f, 0.6f), new Color(1f, 0.8f, 0.25f, 0.5f), 14, 0.5f, true, new Vector2(0.6f, 1.8f)).Play();
                Burst("Motes", new Vector2(1.2f, 2.4f), new Vector2(0.5f, 1.8f), new Vector2(0.05f, 0.11f), -0.08f, new Color(1f, 1f, 0.7f), new Color(1f, 0.85f, 0.3f), 34, 0.5f, true, new Vector2(1f, 0.2f)).Play();
                break;
            case PixelClicker.PixelType.Wheat:
                Burst("Chaff", new Vector2(0.9f, 1.6f), new Vector2(1.2f, 3.2f), new Vector2(0.05f, 0.12f), 0.9f, new Color(0.98f, 0.86f, 0.45f), new Color(0.82f, 0.62f, 0.22f), 36, 0.5f, false).Play();
                Burst("Grains", new Vector2(0.7f, 1.2f), new Vector2(1.4f, 3.6f), new Vector2(0.05f, 0.09f), 2.4f, new Color(0.95f, 0.78f, 0.3f), new Color(0.75f, 0.55f, 0.18f), 14, 0.45f, false).Play();
                break;
            case PixelClicker.PixelType.Weevil:
                Burst("Legs", new Vector2(0.5f, 0.9f), new Vector2(1.6f, 4.2f), new Vector2(0.05f, 0.1f), 2.2f, new Color(0.15f, 0.1f, 0.07f), new Color(0.32f, 0.22f, 0.12f), 24, 0.5f, false).Play();
                Burst("Splat", new Vector2(0.6f, 1.1f), new Vector2(0.4f, 1.6f), new Vector2(0.12f, 0.26f), 0.4f, new Color(0.7f, 0.75f, 0.25f, 0.8f), new Color(0.5f, 0.55f, 0.15f, 0.7f), 8, 0.5f, true, new Vector2(1f, 1.4f)).Play();
                break;
            case PixelClicker.PixelType.Wood:
            case PixelClicker.PixelType.Twig:
                Burst("Chips", new Vector2(0.6f, 1.1f), new Vector2(1.4f, 3.6f), new Vector2(0.06f, 0.14f), 2.6f, new Color(0.7f, 0.5f, 0.3f), new Color(0.45f, 0.3f, 0.16f), 26, 0.5f, false).Play();
                Burst("Splinters", new Vector2(0.5f, 0.9f), new Vector2(1.8f, 4.4f), new Vector2(0.03f, 0.06f), 2.2f, new Color(0.9f, 0.75f, 0.5f), new Color(0.7f, 0.5f, 0.3f), 14, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Bark:
                Burst("Shards", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3.2f), new Vector2(0.07f, 0.16f), 2.8f, new Color(0.45f, 0.33f, 0.24f), new Color(0.3f, 0.2f, 0.14f), 24, 0.5f, false).Play();
                Burst("Dust", new Vector2(0.9f, 1.4f), new Vector2(0.2f, 0.9f), new Vector2(0.2f, 0.4f), -0.04f, new Color(0.55f, 0.45f, 0.35f, 0.5f), new Color(0.45f, 0.36f, 0.28f, 0.4f), 8, 0.5f, true, new Vector2(0.6f, 1.5f)).Play();
                break;
            case PixelClicker.PixelType.Sap:
                Burst("Drops", new Vector2(0.6f, 1.2f), new Vector2(1.2f, 3f), new Vector2(0.07f, 0.15f), 2f, new Color(1f, 0.8f, 0.3f), new Color(0.9f, 0.55f, 0.1f), 24, 0.45f, false, new Vector2(1f, 0.5f)).Play();
                PixelFxRing.Spawn(centre, new Color(1f, 0.75f, 0.25f, 0.7f), size * 0.4f, size * 2f, 0.5f, 0f);
                break;
            case PixelClicker.PixelType.Leaf:
                Burst("Leaves", new Vector2(1.2f, 2.2f), new Vector2(0.8f, 2.4f), new Vector2(0.08f, 0.16f), 0.3f, new Color(0.55f, 0.9f, 0.35f), new Color(0.25f, 0.62f, 0.2f), 22, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Heartwood:
                Burst("Glints", new Vector2(0.9f, 1.7f), new Vector2(1f, 3.8f), new Vector2(0.06f, 0.14f), -0.1f, new Color(1f, 0.85f, 0.6f), new Color(1f, 0.5f, 0.3f), 44, 0.55f, true, new Vector2(1f, 0f)).Play();
                Burst("Chips", new Vector2(0.6f, 1.1f), new Vector2(1.4f, 3.6f), new Vector2(0.07f, 0.15f), 2.4f, new Color(0.85f, 0.45f, 0.32f), new Color(0.55f, 0.25f, 0.18f), 22, 0.5f, false).Play();
                PixelFxRing.Spawn(centre, new Color(1f, 0.6f, 0.4f, 0.85f), size * 0.4f, size * 3f, 0.6f, 2f);
                break;
            case PixelClicker.PixelType.Berry:
                Burst("Juice", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3.2f), new Vector2(0.07f, 0.16f), 2.4f, new Color(0.85f, 0.2f, 0.4f), new Color(0.55f, 0.1f, 0.3f), 24, 0.45f, false, new Vector2(1f, 0.5f)).Play();
                Burst("Splat", new Vector2(0.6f, 1f), new Vector2(0.4f, 1.4f), new Vector2(0.12f, 0.26f), 0.4f, new Color(0.8f, 0.2f, 0.4f, 0.8f), new Color(0.6f, 0.12f, 0.3f, 0.7f), 8, 0.5f, true, new Vector2(1f, 1.4f)).Play();
                break;
            case PixelClicker.PixelType.Mushroom:
                Burst("Spores", new Vector2(1f, 1.8f), new Vector2(0.5f, 1.8f), new Vector2(0.05f, 0.12f), -0.1f, new Color(0.95f, 0.85f, 0.7f), new Color(0.8f, 0.55f, 0.4f), 34, 0.5f, true, new Vector2(1f, 0.2f)).Play();
                Burst("Cap Bits", new Vector2(0.6f, 1f), new Vector2(1.2f, 3f), new Vector2(0.07f, 0.14f), 2.4f, new Color(0.85f, 0.4f, 0.3f), new Color(0.95f, 0.9f, 0.8f), 14, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Herb:
                Burst("Flecks", new Vector2(0.9f, 1.6f), new Vector2(0.8f, 2.6f), new Vector2(0.05f, 0.11f), 0.5f, new Color(0.45f, 0.85f, 0.4f), new Color(0.2f, 0.55f, 0.25f), 26, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Truffle:
                Burst("Earth", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3.2f), new Vector2(0.07f, 0.15f), 2.6f, new Color(0.4f, 0.28f, 0.2f), new Color(0.25f, 0.17f, 0.12f), 20, 0.5f, false).Play();
                Burst("Glints", new Vector2(0.9f, 1.6f), new Vector2(1f, 3.4f), new Vector2(0.05f, 0.12f), -0.1f, new Color(1f, 0.95f, 0.7f), new Color(0.9f, 0.75f, 0.4f), 36, 0.5f, true, new Vector2(1f, 0f)).Play();
                PixelFxRing.Spawn(centre, new Color(0.9f, 0.75f, 0.4f, 0.8f), size * 0.4f, size * 2.6f, 0.5f, 2f);
                break;
            case PixelClicker.PixelType.Weeds:
                Burst("Bits", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3f), new Vector2(0.05f, 0.11f), 2.2f, new Color(0.6f, 0.7f, 0.35f), new Color(0.4f, 0.5f, 0.25f), 16, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Toadstool:
                Burst("Spores", new Vector2(1.2f, 2.2f), new Vector2(1f, 3.2f), new Vector2(0.1f, 0.22f), -0.05f, new Color(0.75f, 0.4f, 0.85f, 0.8f), new Color(0.45f, 0.9f, 0.4f, 0.7f), 50, 0.55f, true, new Vector2(1f, 0.3f)).Play();
                Burst("Cloud", new Vector2(1.2f, 2f), new Vector2(0.3f, 1.2f), new Vector2(0.3f, 0.6f), -0.03f, new Color(0.6f, 0.35f, 0.7f, 0.5f), new Color(0.4f, 0.7f, 0.35f, 0.4f), 12, 0.5f, true, new Vector2(0.6f, 1.8f)).Play();
                PixelFxRing.Spawn(centre, new Color(0.7f, 0.4f, 0.85f, 0.8f), size * 0.5f, size * 4f, 0.7f, 0f);
                break;
            case PixelClicker.PixelType.Fish:
                Burst("Drops", new Vector2(0.6f, 1.1f), new Vector2(1.4f, 3.4f), new Vector2(0.05f, 0.12f), 2.6f, new Color(0.6f, 0.8f, 1f), new Color(0.35f, 0.55f, 0.85f), 28, 0.5f, false).Play();
                Burst("Scales", new Vector2(0.7f, 1.2f), new Vector2(0.8f, 2.2f), new Vector2(0.06f, 0.12f), 1.4f, new Color(0.85f, 0.92f, 1f), new Color(0.6f, 0.7f, 0.85f), 12, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Seaweed:
                Burst("Strands", new Vector2(0.9f, 1.6f), new Vector2(0.6f, 2f), new Vector2(0.06f, 0.14f), 0.6f, new Color(0.25f, 0.7f, 0.4f), new Color(0.15f, 0.45f, 0.3f), 22, 0.5f, false).Play();
                Burst("Drops", new Vector2(0.6f, 1f), new Vector2(1f, 2.6f), new Vector2(0.04f, 0.09f), 2.4f, new Color(0.6f, 0.85f, 1f), new Color(0.4f, 0.65f, 0.9f), 12, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Shell:
                Burst("Shards", new Vector2(0.6f, 1.1f), new Vector2(1.4f, 3.2f), new Vector2(0.06f, 0.13f), 2.6f, new Color(1f, 0.8f, 0.82f), new Color(0.9f, 0.55f, 0.65f), 20, 0.5f, false).Play();
                Burst("Glints", new Vector2(0.8f, 1.4f), new Vector2(0.8f, 2.6f), new Vector2(0.04f, 0.1f), -0.05f, new Color(1f, 1f, 1f), new Color(1f, 0.85f, 0.9f), 16, 0.5f, true).Play();
                break;
            case PixelClicker.PixelType.Pearl:
                Burst("Glints", new Vector2(1f, 1.8f), new Vector2(1f, 3.6f), new Vector2(0.05f, 0.13f), -0.1f, new Color(1f, 1f, 1f), new Color(0.85f, 0.9f, 1f), 44, 0.5f, true).Play();
                Burst("Drops", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3f), new Vector2(0.05f, 0.1f), 2.4f, new Color(0.7f, 0.85f, 1f), new Color(0.5f, 0.7f, 0.95f), 14, 0.5f, false).Play();
                PixelFxRing.Spawn(centre, new Color(0.9f, 0.95f, 1f, 0.8f), size * 0.4f, size * 2.6f, 0.5f, 2f);
                break;
            case PixelClicker.PixelType.Driftwood:
                Burst("Splinters", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3f), new Vector2(0.05f, 0.11f), 2.2f, new Color(0.7f, 0.65f, 0.55f), new Color(0.5f, 0.45f, 0.38f), 14, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.Jellyfish:
                Burst("Sparks", new Vector2(0.5f, 1f), new Vector2(2f, 5f), new Vector2(0.05f, 0.12f), 0f, new Color(1f, 0.6f, 0.95f), new Color(0.5f, 0.8f, 1f), 40, 0.55f, true).Play();
                Burst("Goo", new Vector2(0.9f, 1.6f), new Vector2(0.4f, 1.6f), new Vector2(0.2f, 0.45f), 0.3f, new Color(0.9f, 0.5f, 0.85f, 0.5f), new Color(0.6f, 0.4f, 0.9f, 0.4f), 12, 0.5f, true, new Vector2(1f, 1.6f)).Play();
                PixelFxRing.Spawn(centre, new Color(1f, 0.6f, 0.95f, 0.8f), size * 0.5f, size * 4f, 0.6f, 0f);
                break;
            case PixelClicker.PixelType.HornetNest:
                Burst("Hornets", new Vector2(0.8f, 1.5f), new Vector2(1.5f, 4.2f), new Vector2(0.06f, 0.11f), 0f, new Color(1f, 0.85f, 0.15f), new Color(0.12f, 0.1f, 0.05f), 40, 0.5f, false).Play();
                Burst("Paper", new Vector2(0.6f, 1.1f), new Vector2(1.2f, 3f), new Vector2(0.07f, 0.15f), 1.6f, new Color(0.9f, 0.84f, 0.6f), new Color(0.7f, 0.62f, 0.4f), 18, 0.5f, false).Play();
                break;
            case PixelClicker.PixelType.GoldenFruit:
                Burst("Glints", new Vector2(0.9f, 1.8f), new Vector2(1f, 4.2f), new Vector2(0.06f, 0.15f), -0.1f, new Color(1f, 0.98f, 0.7f), new Color(1f, 0.78f, 0.2f), 60, 0.6f, true, new Vector2(1f, 0f)).Play();
                Burst("Juice", new Vector2(0.7f, 1.2f), new Vector2(1.4f, 3.8f), new Vector2(0.07f, 0.15f), 2.2f, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.65f, 0.1f), 24, 0.5f, false).Play();
                PixelFxRing.Spawn(centre, new Color(1f, 0.85f, 0.3f, 0.9f), size * 0.4f, size * 3.2f, 0.6f, 3f);
                PixelAudio.Play("farm_gleam");
                break;
        }
    }
}
