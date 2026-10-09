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

    private static double readyAt;

    static PixelShaderKeepAlive()
    {
        // Don't touch the asset database while Unity is still compiling or importing (that gives the red
        // "TransientArtifactProvider ... transient artifacts are getting updated" error). Wait until the Editor is idle.
        readyAt = EditorApplication.timeSinceStartup + 3.0;
        EditorApplication.update += WaitUntilIdle;
    }

    private static void WaitUntilIdle()
    {
        if (EditorApplication.timeSinceStartup < readyAt) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            readyAt = EditorApplication.timeSinceStartup + 2.0;
            return;
        }

        EditorApplication.update -= WaitUntilIdle;
        if (SessionState.GetBool("PixelShaderKeepAlive.Done", false)) return; // once per Editor session
        SessionState.SetBool("PixelShaderKeepAlive.Done", true);
        try { Ensure(false); }
        catch (System.Exception e) { Debug.LogWarning("PixelShaderKeepAlive: could not create the materials (" + e.Message + "). Use Pixel Clicker > Rebuild Shader Keep-Alive Materials."); }
    }

    [MenuItem("Pixel Clicker/Rebuild Shader Keep-Alive Materials")]
    private static void Rebuild() => Ensure(true);

    private static void Ensure(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

        bool any = false;
        bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        AssetDatabase.StartAssetEditing(); // one batch instead of many separate imports
        try
        {
        foreach (string shaderName in ShaderNames)
        {
            // The built-in Standard shader is pink in a URP project and is never used there: leave it out (and remove old copies).
            bool standard = shaderName == "Standard";
            if (standard && urp)
            {
                foreach (string suffix in new[] { "_Emission", "_Transparent", "_TransparentEmission" })
                    if (AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Standard" + suffix + ".mat") != null)
                        { AssetDatabase.DeleteAsset(Folder + "/Standard" + suffix + ".mat"); any = true; }
                continue;
            }
            Shader shader = Shader.Find(shaderName);
            if (shader == null) continue;

            string tag = shaderName.Replace("Universal Render Pipeline/", "URP ").Replace(" ", "");
            any |= Make(tag + "_Emission", shader, true, false, force);
            any |= Make(tag + "_Transparent", shader, false, true, force);
            any |= Make(tag + "_TransparentEmission", shader, true, true, force);
        }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        any |= MakeSpriteMaterial(force);
        any |= MakeProjectCopies(force);
        if (any) AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Copies of the project's own URP / Standard materials (the pixel material, ...) with emission switched on, plus a transparent
    /// copy. A shader variant is only kept when some material uses exactly that combination of keywords, and the game's glow
    /// materials are copies of the pixel material, so the keep-alive copies must start from the real material too.
    /// </summary>
    private static bool MakeProjectCopies(bool force)
    {
        bool any = false;
        int made = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string source = AssetDatabase.GUIDToAssetPath(guid);
            if (source.StartsWith(Folder)) continue;
            Material original = AssetDatabase.LoadAssetAtPath<Material>(source);
            if (original == null || original.shader == null) continue;
            string shaderName = original.shader.name;
            if (!shaderName.StartsWith("Universal Render Pipeline/") || shaderName.Contains("Particles") || shaderName.Contains("Unlit")) continue;
            if (made++ >= 10) break; // a project with hundreds of materials doesn't need hundreds of copies

            string safe = original.name.Replace(" ", "").Replace("/", "_");
            any |= MakeCopy("Project_" + safe + "_Emission", original, true, false, force);
            any |= MakeCopy("Project_" + safe + "_TransparentEmission", original, true, true, force);
        }
        return any;
    }

    private static bool MakeCopy(string name, Material original, bool emission, bool transparent, bool force)
    {
        string path = Folder + "/" + name + ".mat";
        bool exists = AssetDatabase.LoadAssetAtPath<Material>(path) != null;
        if (exists && !force) return false;

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "PixelShaderKeep");
        if (exists) AssetDatabase.DeleteAsset(path);

        Material m = new Material(original) { name = name };
        if (emission)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (transparent && m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        AssetDatabase.CreateAsset(m, path);
        return true;
    }

    /// <summary>A material that uses Sprites/Default, so that shader is included in builds (PixelShaders.SpriteDefault falls back to it).</summary>
    private static bool MakeSpriteMaterial(bool force)
    {
        string path = Folder + "/Sprites_Default.mat";
        bool exists = AssetDatabase.LoadAssetAtPath<Material>(path) != null;
        if (exists && !force) return false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return false;

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "PixelShaderKeep");
        if (exists) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(new Material(shader) { name = "Sprites_Default" }, path);
        return true;
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
