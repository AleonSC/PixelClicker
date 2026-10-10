using UnityEngine;

/// <summary>
/// The Prospector's Pack: six base-metal ore pixels (Copper, Tin, Iron, Lead, Zinc, Nickel). They are tough little rocks that crack as you hit them,
/// shower chips and, now and then, turn out to be a RICH VEIN that pays big. They have no other integration (no potions, pets, crafting, Value
/// upgrades or achievements). Ores are NOT in the random spawns by default: a switch in the Toggles window ("Prospector mode", off by default) brings them in and
/// takes the monochrome pack, RGB, Glass and Luminescent out of what spawns; with it off the ores never appear at all.
/// </summary>
public static class PixelProspector
{
    private const string Pref = "PixelClicker.Setting.Prospector";
    private static int cache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache = -1; richAnnounced = false; }

    /// <summary>The player's switch: the ores replace the basic pixels while it is on (and at least one ore is unlocked).</summary>
    public static bool Setting
    {
        get
        {
            if (cache < 0) cache = PlayerPrefs.GetInt(Pref, 0) != 0 ? 1 : 0;
            return cache != 0;
        }
        set { cache = value ? 1 : 0; PlayerPrefs.SetInt(Pref, cache); }
    }

    /// <summary>A save file with its own settings was loaded: read the switch again.</summary>
    public static void ReloadSettingsFromPrefs() { cache = -1; }

    /// <summary>The pixels the ores replace while Prospector mode is on: the monochrome pack (White, Gray, Black), RGB, Glass and Luminescent.</summary>
    public static bool IsBase(PixelClicker.PixelType type) =>
        (type >= PixelClicker.PixelType.White && type <= PixelClicker.PixelType.Glass) || type == PixelClicker.PixelType.Luminescent;

    /// <summary>Is this type kept out of the random spawns right now? Ores only spawn in Prospector mode; in that mode the basic pixels don't.</summary>
    public static bool LeftOut(PixelClicker.PixelType type, bool prospecting) =>
        prospecting ? IsBase(type) : PixelClicker.IsOre(type);

    /// <summary>Has the player unlocked at least one ore?</summary>
    public static bool AnyOreUnlocked(PixelClicker clicker)
    {
        if (clicker == null) return false;
        for (int i = 0; i < clicker.Tiers.Length; i++)
            if (PixelClicker.IsOre(clicker.Tiers[i].type) && clicker.Tiers[i].unlocked) return true;
        return false;
    }

    private static bool richAnnounced;

    /// <summary>The first rich vein of a session gets a line in the event log.</summary>
    public static void AnnounceRich()
    {
        if (richAnnounced) return;
        richAnnounced = true;
        PixelHints.Announce("A rich vein! It glittered - and paid out big.");
    }
}

/// <summary>The ore pixels' hit effects: chips flying off every hit, a shower of nuggets and dust when the ore breaks, and the glitter of a rich vein.</summary>
public static class OreFx
{
    /// <summary>A hit that doesn't break the ore: rock chips and flecks of the metal fly off (more as it nears breaking).</summary>
    public static void Hit(Vector3 centre, float size, Color metal, float progress)
    {
        if (PixelFx.Material == null) return;
        GameObject root = new GameObject("Ore Chips");
        root.transform.position = centre;
        Color rock = new Color(0.5f, 0.48f, 0.46f, 1f);
        ParticleSystem chips = PixelFx.Make(root.transform, "Chips", new PixelFx.Spec
        {
            lifetime = new Vector2(0.55f, 1f), speed = new Vector2(1.2f, 3.2f) * size, size = new Vector2(0.05f, 0.12f) * size,
            gravity = 2.6f, colorA = rock, colorB = Color.Lerp(metal, Color.white, 0.2f),
            burst = Mathf.RoundToInt(Mathf.Lerp(6f, 14f, Mathf.Clamp01(progress))), shape = ParticleSystemShapeType.Sphere,
            shapeRadius = size * 0.5f, randomDirection = 0.15f, sizeOverLife = new Vector2(1f, 0.6f), maxParticles = 40,
        });
        chips.Play();
        Object.Destroy(root, 2f);
        PixelAudio.PlayScaled("ore_chip", 0.8f);
    }

    /// <summary>The ore breaks: a bigger shower of rock and metal nuggets plus a puff of dust; a rich vein adds a burst of gold glitter.</summary>
    public static void Break(Vector3 centre, float size, Color metal, bool rich)
    {
        if (PixelFx.Material == null) return;
        GameObject root = new GameObject("Ore Break");
        root.transform.position = centre;
        Color rock = new Color(0.46f, 0.44f, 0.42f, 1f);
        Color nugget = Color.Lerp(metal, Color.white, 0.35f);

        ParticleSystem rocks = PixelFx.Make(root.transform, "Rocks", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.3f), speed = new Vector2(1.6f, 4.6f) * size, size = new Vector2(0.07f, 0.16f) * size,
            gravity = 2.8f, colorA = rock, colorB = new Color(0.3f, 0.29f, 0.28f, 1f), burst = 34,
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.55f, randomDirection = 0.1f, sizeOverLife = new Vector2(1f, 0.7f), maxParticles = 80,
        });
        ParticleSystem nuggets = PixelFx.Make(root.transform, "Nuggets", new PixelFx.Spec
        {
            lifetime = new Vector2(0.8f, 1.4f), speed = new Vector2(1.8f, 5f) * size, size = new Vector2(0.06f, 0.13f) * size,
            gravity = 2.4f, colorA = metal, colorB = nugget, burst = 16,
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, randomDirection = 0.1f, maxParticles = 50,
        });
        ParticleSystem dust = PixelFx.Make(root.transform, "Dust", new PixelFx.Spec
        {
            lifetime = new Vector2(0.9f, 1.5f), speed = new Vector2(0.2f, 1f) * size, size = new Vector2(0.25f, 0.5f) * size,
            gravity = -0.05f, colorA = new Color(0.7f, 0.68f, 0.64f, 0.5f), colorB = new Color(0.55f, 0.53f, 0.5f, 0.4f), burst = 8,
            shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.5f, sizeOverLife = new Vector2(0.6f, 1.6f), maxParticles = 20,
            overLife = PixelFx.FadeGradient(Color.white, Color.white, Color.white, 0.8f),
        });
        rocks.Play(); nuggets.Play(); dust.Play();

        if (rich)
        {
            ParticleSystem glitter = PixelFx.Make(root.transform, "Glitter", new PixelFx.Spec
            {
                lifetime = new Vector2(0.8f, 1.5f), speed = new Vector2(1f, 3.4f) * size, size = new Vector2(0.05f, 0.11f) * size,
                gravity = -0.1f, colorA = new Color(1f, 0.95f, 0.55f, 1f), colorB = new Color(1f, 0.75f, 0.15f, 1f), burst = 46,
                shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.6f, sizeOverLife = new Vector2(1f, 0.2f), maxParticles = 60,
                overLife = PixelFx.FadeGradient(Color.white, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.6f, 0.1f), 1f),
            });
            glitter.Play();
            PixelFxRing.Spawn(centre, new Color(1f, 0.85f, 0.3f, 0.8f), size * 0.4f, size * 2.2f, 0.5f, 3f);
            PixelAudio.Play("ore_gleam");
            PixelProspector.AnnounceRich();
        }
        Object.Destroy(root, 3f);
    }

    /// <summary>The slow glitter that hangs round a rich vein while it sits on the cube. Returns the object (destroy it when the ore is gone).</summary>
    public static GameObject Glitter(Transform parent, float size)
    {
        if (PixelFx.Material == null || parent == null) return null;
        GameObject holder = new GameObject("Rich Vein Glitter");
        holder.transform.SetParent(parent, false);
        ParticleSystem ps = PixelFx.Make(holder.transform, "Glitter", new PixelFx.Spec
        {
            lifetime = new Vector2(0.7f, 1.2f), speed = new Vector2(0.05f, 0.3f), size = new Vector2(0.05f, 0.1f) * size,
            gravity = -0.12f, colorA = new Color(1f, 0.95f, 0.55f, 1f), colorB = new Color(1f, 0.75f, 0.15f, 1f),
            rate = 16f, loop = true, localSpace = false, shape = ParticleSystemShapeType.Sphere, shapeRadius = size * 0.62f,
            sizeOverLife = new Vector2(1f, 0.1f), maxParticles = 40,
            overLife = PixelFx.FadeGradient(Color.white, new Color(1f, 0.85f, 0.3f), new Color(1f, 0.6f, 0.1f), 1f),
        });
        ps.Play();
        return holder;
    }
}
