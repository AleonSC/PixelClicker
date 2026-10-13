// ---- System notes ----
// Editor/PixelDefineSymbols.cs
//   Editor-only ([InitializeOnLoad]): adds the scripting define PIXEL_URP to the project when the Universal Render Pipeline package is installed (and removes it when it is not).
//   PixelFilters.cs uses #if PIXEL_URP for its post-processing volume (UNITY_PIPELINE_URP is only defined inside Unity's own packages, not in Assembly-CSharp). The first time it
//   runs it changes Project Settings > Player > Scripting Define Symbols, which makes Unity recompile once. Menu: Pixel Clicker > Refresh Define Symbols (runs it again).
// ---------------------------------------------------
using System;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

[InitializeOnLoad]
public static class PixelDefineSymbols
{
    private const string Symbol = "PIXEL_URP";

    static PixelDefineSymbols() { EditorApplication.delayCall += Apply; }

    [MenuItem("Pixel Clicker/Refresh Define Symbols")]
    private static void Apply()
    {
        bool urp = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime") != null;
        bool changed = false;
#if UNITY_2021_2_OR_NEWER
        foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup) })
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            bool has = Array.IndexOf(defines.Split(';'), Symbol) >= 0;
            if (urp == has) continue;
            defines = urp ? (defines.Length == 0 ? Symbol : defines + ";" + Symbol) : string.Join(";", Array.FindAll(defines.Split(';'), s => s != Symbol));
            PlayerSettings.SetScriptingDefineSymbols(target, defines);
            changed = true;
        }
#endif
        if (changed) UnityEngine.Debug.Log("PixelDefineSymbols: " + (urp ? "added" : "removed") + " the " + Symbol + " define (Universal RP package " + (urp ? "found" : "not found") + "). Unity will recompile once.");
    }
}
