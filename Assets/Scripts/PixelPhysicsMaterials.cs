// ---- System notes ----
// PixelPhysicsMaterials.cs
//   ONE place that makes physics materials (bounce / friction of a collider). Unity 6 renamed the type (PhysicsMaterial / PhysicsMaterialCombine,
//   older: PhysicMaterial / PhysicMaterialCombine); the UNITY_6000_0_OR_NEWER split lives only in this file, so callers never write it twice.
//   - Apply(collider, name, bounce, friction, bounceCombine, frictionCombine): puts a material with exactly these values on the collider.
//     Materials are CACHED by name + values, so every husk / pet with the same settings shares one (nothing is made per husk, nothing leaks);
//     a changed Inspector value simply asks for (and caches) a new one. Never change a material you got from here: it is shared.
//   - ApplyGrippy(collider, grip): Pixel Grabbing's thrown husk: a copy of the collider's own material with at least 'grip' friction,
//     combined by Maximum (so its grip wins against slippery floors / husks). Restore(collider, original) puts the saved one back.
//   - CopyOf(from, to): copies another collider's material (the floor reuses the scene ground's).
//   Used by: husks (PixelClickerHusks: the shared husk material + per-look materials), Pixel Grabbing (thrown grip), pets (PixelPets), the floor (PixelFloor).
//   The cache lives for the whole run (a few dozen tiny assets at most); materials are never destroyed (colliders on pooled husks keep using them).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using PMat = UnityEngine.PhysicsMaterial;
using PCombine = UnityEngine.PhysicsMaterialCombine;
#else
using PMat = UnityEngine.PhysicMaterial;
using PCombine = UnityEngine.PhysicMaterialCombine;
#endif

public static class PixelPhysicsMaterials
{
    /// <summary>How two touching colliders' values are combined (the same four choices as Unity's, without the version split).</summary>
    public enum Combine { Average, Minimum, Multiply, Maximum }

    // (keyed by a value tuple, so asking for a cached one makes no garbage - husks ask on every drop)
    private static readonly Dictionary<(string, float, float, float, PCombine, PCombine), PMat> cache = new Dictionary<(string, float, float, float, PCombine, PCombine), PMat>();

    /// <summary>Puts a shared material with these values on the collider (cached by name + values).</summary>
    public static void Apply(Collider col, string name, float bounce, float dynamicFriction, float staticFriction,
                             Combine bounceCombine = Combine.Average, Combine frictionCombine = Combine.Average)
    {
        if (col != null) col.sharedMaterial = Get(name, bounce, dynamicFriction, staticFriction, ToUnity(bounceCombine), ToUnity(frictionCombine));
    }

    /// <summary>Same with one friction for both static and dynamic.</summary>
    public static void Apply(Collider col, string name, float bounce, float friction, Combine bounceCombine = Combine.Average, Combine frictionCombine = Combine.Average)
        => Apply(col, name, bounce, friction, friction, bounceCombine, frictionCombine);

    /// <summary>
    /// A thrown husk's grip: a copy of the collider's own material (name + "_Thrown", same bounce and bounce combine) with at least 'grip'
    /// friction, combined by Maximum. Returns the material it replaced, for Restore.
    /// </summary>
    public static Object ApplyGrippy(Collider col, float grip)
    {
        if (col == null) return null;
        PMat original = col.sharedMaterial;
        float g = Mathf.Max(grip, original != null ? original.dynamicFriction : 0f);
        col.sharedMaterial = Get((original != null ? original.name : "OldPixel") + "_Thrown", original != null ? original.bounciness : 0f, g, g,
                                 original != null ? original.bounceCombine : PCombine.Average, PCombine.Maximum);
        return original;
    }

    /// <summary>Puts back a material saved from ApplyGrippy (or any physics material, as an Object).</summary>
    public static void Restore(Collider col, Object original)
    {
        if (col != null) col.sharedMaterial = original as PMat;
    }

    /// <summary>Gives 'to' the material of 'from'. False if 'from' has none (the caller then applies its own).</summary>
    public static bool CopyOf(Collider from, Collider to)
    {
        if (from == null || to == null || from.sharedMaterial == null) return false;
        to.sharedMaterial = from.sharedMaterial;
        return true;
    }

    private static PMat Get(string name, float bounce, float dynamicFriction, float staticFriction, PCombine bounceCombine, PCombine frictionCombine)
    {
        var key = (name, bounce, dynamicFriction, staticFriction, bounceCombine, frictionCombine);
        if (cache.TryGetValue(key, out PMat m) && m != null) return m;
        m = new PMat(name)
        {
            bounciness = bounce, dynamicFriction = dynamicFriction, staticFriction = staticFriction,
            bounceCombine = bounceCombine, frictionCombine = frictionCombine
        };
        cache[key] = m;
        return m;
    }

    private static PCombine ToUnity(Combine c)
    {
        switch (c)
        {
            case Combine.Minimum: return PCombine.Minimum;
            case Combine.Multiply: return PCombine.Multiply;
            case Combine.Maximum: return PCombine.Maximum;
            default: return PCombine.Average;
        }
    }
}
