using System.Diagnostics;

/// <summary>
/// Diagnostic messages that only exist in the Editor and development builds (the calls are removed from release
/// builds, arguments included). Errors and warnings still use Debug.LogError / LogWarning directly.
/// </summary>
public static class PixelDebug
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Info(string message, UnityEngine.Object context = null)
    {
        UnityEngine.Debug.Log(message, context);
    }
}
