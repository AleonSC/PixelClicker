using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps shader variants alive in built games.
///
/// The game switches emission (glowing pixels, the meteor's lava cracks, bomb / pad lights...) and transparency on from code at
/// run time (Material.EnableKeyword("_EMISSION") and friends). In the Editor that always works, but a BUILD only contains the
/// shader variants that some saved material uses - the others are stripped, so glow and lava streaks silently disappear.
///
/// This script creates a few tiny materials in Assets/Resources/PixelShaderKeep that have those keywords switched on. Anything in
/// a Resources folder goes into the build with its variants, so the shaders then keep them. It runs by itself when the Editor
/// loads (only creating what is missing); Pixel Clicker > Rebuild Shader Keep-Alive Materials recreates them.
/// </summary>
[InitializeOnLoad]
public static class PixelShaderKeepAlive
{
    private const string Folder = "Assets/Resources/PixelShaderKeep";

    private static readonly string[] ShaderNames =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Standard",
    };

    static PixelShaderKeepAlive()
    {
        EditorApplication.delayCall += () => Ensure(false);
    }

    [MenuItem("Pixel Clicker/Rebuild Shader Keep-Alive Materials")]
    private static void Rebuild() => Ensure(true);

    private static void Ensure(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

        bool any = false;
        foreach (string shaderName in ShaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) continue;

            string tag = shaderName.Replace("Universal Render Pipeline/", "URP ").Replace(" ", "");
            any |= Make(tag + "_Emission", shader, true, false, force);
            any |= Make(tag + "_Transparent", shader, false, true, force);
            any |= Make(tag + "_TransparentEmission", shader, true, true, force);
        }
        if (any) AssetDatabase.SaveAssets();
    }

    private static bool Make(string name, Shader shader, bool emission, bool transparent, bool force)
    {
        string path = Folder + "/" + name + ".mat";
        bool exists = AssetDatabase.LoadAssetAtPath<Material>(path) != null;
        if (exists && !force) return false;

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "PixelShaderKeep");
        if (exists) AssetDatabase.DeleteAsset(path);

        Material m = new Material(shader) { name = name };
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (transparent)
        {
            if (m.HasProperty("_Surface")) // URP
            {
                m.SetFloat("_Surface", 1f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else if (m.HasProperty("_Mode")) // Built-in Standard
            {
                m.SetFloat("_Mode", 3f);
                m.EnableKeyword("_ALPHABLEND_ON");
            }
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(m, path);
        return true;
    }
}
