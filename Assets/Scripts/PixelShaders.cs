using UnityEngine;

/// <summary>
/// Finds the shaders the game builds its runtime materials from. In the Editor <c>Shader.Find</c> finds any shader, but in a
/// built game it only finds shaders that were included in the build - and Sprites/Default is often not included in a URP
/// project, so trails, glow shells, face marks, arrows and stars all silently lost their material. The Editor script
/// (PixelShaderKeepAlive) puts a material that uses Sprites/Default into Assets/Resources/PixelShaderKeep, which does get included;
/// this class falls back to that material's shader when <c>Shader.Find</c> comes back empty.
/// </summary>
public static class PixelShaders
{
    private const string KeepMaterialPath = "PixelShaderKeep/Sprites_Default";
    private static Shader sprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { sprite = null; }

    /// <summary>The unlit, alpha-blended, vertex-colour shader (Sprites/Default), or null if it cannot be found at all.</summary>
    public static Shader SpriteDefault()
    {
        if (sprite != null) return sprite;
        sprite = Shader.Find("Sprites/Default");
        if (sprite == null)
        {
            Material keep = Resources.Load<Material>(KeepMaterialPath);
            if (keep != null) sprite = keep.shader;
        }
        return sprite;
    }
}
