// ---- System notes (moved here from CLAUDE.md) ----
// PixelFind.cs
//   PixelFind.First<T>() / All<T>() / Count<T>(): the only place with the Find version checks (FindObjectOfType before 2023.1, then
//   FindAnyObjectByType; FindObjectsByType with a sort mode until Unity 6.6, which deprecated the sort mode). Use it instead of calling
//   Unity's Find methods directly. 'First' is any one of them: every caller looks for a single component, so the order never mattered.
//   PixelFind.Cached<T>(): the same as First, but it remembers the answer (searches again only when the remembered object was destroyed, e.g.
//   after Settings > Restart reloads the scene, or when none was found yet). Use it for the game's single components (PixelShop, PixelStats,
//   PixelSkills...) anywhere that runs often; First does a full scene search on every call.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One place for "find the one X in the scene". Unity renamed these calls in 2023 (FindObjectOfType became
/// FindFirstObjectByType), and every script used to carry its own version check - they now call this instead.
/// </summary>
public static class PixelFind
{
    /// <summary>The first object of type T in the scene (or null).</summary>
    public static T First<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindAnyObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }

    private static readonly Dictionary<Type, UnityEngine.Object> cache = new Dictionary<Type, UnityEngine.Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache.Clear(); }

    /// <summary>
    /// The first object of type T in the scene, remembered: later calls cost nothing until that object is destroyed (then it searches again).
    /// For single components looked up often (in Update, per click). A null answer is not remembered, so it keeps looking until one exists.
    /// </summary>
    public static T Cached<T>() where T : UnityEngine.Object
    {
        if (cache.TryGetValue(typeof(T), out UnityEngine.Object o) && o != null) return (T)o;   // (Unity's != null is false for a destroyed object)
        T found = First<T>();
        if (found != null) cache[typeof(T)] = found; else cache.Remove(typeof(T));
        return found;
    }

    /// <summary>The first object of the given component type in the scene (or null) - for code that only has a Type, like PixelSystems.</summary>
    public static UnityEngine.Object First(Type type)
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindAnyObjectByType(type);
#else
        return UnityEngine.Object.FindObjectOfType(type);
#endif
    }

    /// <summary>Every object of type T in the scene (a new array each call: for dev tools / one-off dumps, not for Update).</summary>
    public static T[] All<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_6_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>();
#elif UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>();
#endif
    }

    /// <summary>How many objects of type T are in the scene.</summary>
    public static int Count<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_6_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>().Length;
#elif UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
#else
        return UnityEngine.Object.FindObjectsOfType<T>().Length;
#endif
    }
}
