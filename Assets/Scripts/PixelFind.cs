using UnityEngine;

/// <summary>
/// One place for "find the one X in the scene". Unity renamed these calls in 2023 (FindObjectOfType became
/// FindFirstObjectByType), and every script used to carry its own version check - they now call this instead.
/// </summary>
public static class PixelFind
{
    /// <summary>The first object of type T in the scene (or null).</summary>
    public static T First<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    /// <summary>How many objects of type T are in the scene.</summary>
    public static int Count<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;
#else
        return Object.FindObjectsOfType<T>().Length;
#endif
    }
}
