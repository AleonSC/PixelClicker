using UnityEngine;

// ---- System notes ----
// PixelObjectId.cs
//   One number per Unity object (a component, GameObject, material...), for dictionary keys, cache keys and random seeds.
//   Newer Unity (6.3+) made Object.GetInstanceID() obsolete (an error) in favour of GetEntityId(), which returns an EntityId, not an int.
//   Every script asks HERE instead of calling either one, so the version switch lives in one place.
//   The number is stable while the object exists. On new Unity it is the EntityId's hash: different objects get different numbers in practice,
//   but code that must be exact still compares the objects themselves (PixelHuskQuery's cache does).
// ---------------------------------------------------
public static class PixelObjectId
{
    /// <summary>This object's id number (0 for null).</summary>
    public static int Of(Object o)
    {
        if (ReferenceEquals(o, null)) return 0;
#if UNITY_6000_3_OR_NEWER
        return o.GetEntityId().GetHashCode();
#else
        return o.GetInstanceID();
#endif
    }
}
