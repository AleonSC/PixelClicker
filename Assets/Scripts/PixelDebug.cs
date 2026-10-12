// ---- System notes (moved here from CLAUDE.md) ----
// PixelDebug.cs
//   PixelDebug.Info(msg, ctx): diagnostic logging that is compiled out of release builds (Editor/dev builds only). Use it instead of
//   Debug.Log for non-error messages. Also: PixelUIKit.SetText(label, text) skips identical strings; open windows (shop, Inventory,
//   Log, stats) refresh on a timer (rowRefreshSeconds, refreshSeconds), not every frame; each class with static state has a
//   [RuntimeInitializeOnLoadMethod] ResetStatics() (Enter Play Mode without domain reload) - add one when adding static state.
//   PixelHints warns (editor only) once per unknown Trigger id.
// ---------------------------------------------------
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
