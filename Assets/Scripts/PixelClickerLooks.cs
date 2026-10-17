// ---- System notes ----
// PixelClickerLooks.cs
//   Part of partial class PixelClicker: how the block on the cube LOOKS - its mesh per Pixel Look (BaseMesh, MeshFor), colour / texture /
//   surface (RenderColor, ApplyLookTexture, ApplyLookSurface, ApplyPixelColor, ApplyTierLook), damage cracks (SetLiveDamage), the glow
//   shell / halo / point light for glowing tiers (UpdateGlowShell, AnimateGlowShell, UpdateGlowLight), materials (MaterialForTier,
//   GetGlowMaterial, BuildTransparentMaterial, CreateVisualMaterial). Which look a type has lives in PixelLooks.cs; the saved list's
//   fix-ups in PixelClickerLookFixups.cs. Split out of PixelClicker.cs (no behaviour change).
// ---------------------------------------------------
using Unity.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

public partial class PixelClicker
{
    // ------------------------------------------------------------------
    // Visuals
    // ------------------------------------------------------------------

    private MeshRenderer liveShellRenderer, liveHaloRenderer;
    private Color liveShellColor;
    private float liveShellAlpha, liveShellHdr;
    private MaterialPropertyBlock shellBlock; // created on first use (native objects can't be made in a field initializer)

    /// <summary>Makes the glow shell and halo breathe like the real emission does (same speed and amount, a little stronger so it reads).</summary>
    private void AnimateGlowShell(float time)
    {
        if (liveShellRenderer == null && liveHaloRenderer == null) return;
        if (shellBlock == null) shellBlock = new MaterialPropertyBlock();
        float breath = Mathf.Max(0.3f, GlowBreath(activeGlowType, time, 1.5f));

        if (liveShellRenderer != null)
        {
            liveShellRenderer.GetPropertyBlock(shellBlock); // keeps what is already on it (textures)
            shellBlock.SetColor("_Color", new Color(liveShellColor.r * liveShellHdr * breath, liveShellColor.g * liveShellHdr * breath,
                                                    liveShellColor.b * liveShellHdr * breath, Mathf.Clamp01(liveShellAlpha * Mathf.Lerp(1f, breath, 0.5f))));
            liveShellRenderer.SetPropertyBlock(shellBlock);
        }

        if (liveHaloRenderer != null)
        {
            liveHaloRenderer.GetPropertyBlock(shellBlock);
            shellBlock.SetColor("_Color", new Color(liveShellColor.r * liveShellHdr * 0.7f * breath, liveShellColor.g * liveShellHdr * 0.7f * breath,
                                                    liveShellColor.b * liveShellHdr * 0.7f * breath, Mathf.Clamp01(liveShellAlpha * 0.3f * breath)));
            liveHaloRenderer.SetPropertyBlock(shellBlock);
        }
    }

    private PixelLook activeLook;          // the look of the tier the cube currently shows
    private GameObject liveExtras;         // outline / dark-matter core on the live cube
    private PixelType liveExtrasType;
    private bool liveExtrasBuilt;

    private Mesh baseCubeMesh;

    /// <summary>The plain cube mesh of the pixel (captured before any look swaps it for a jagged one).</summary>
    private Mesh BaseMesh()
    {
        if (baseCubeMesh == null && pixelRenderer != null)
        {
            MeshFilter mf = pixelRenderer.GetComponent<MeshFilter>();
            if (mf != null) baseCubeMesh = mf.sharedMesh;
        }
        return baseCubeMesh;
    }

    /// <summary>The mesh a look draws: the chipped rock cube for jagged looks, else the plain cube.</summary>
    private Mesh MeshFor(PixelLook look)
    {
        Mesh basic = BaseMesh();
        if (look != null && basic != null)
        {
            if (look.vacuumContainer) return PixelVacuumMesh.Get(basic);
            if (look.blob) return PixelGelFx.BlobMesh(basic);
            if (look.jagged) return PixelRockMesh.Get(basic, (int)look.type);
        }
        return basic;
    }

    /// <summary>True if old pixels of this tier pass through the screen edges (ghosts), see PixelLook.ignoreViewBounds.</summary>
    public bool IgnoresViewBounds(int tierIndex)
    {
        PixelLook look = IsValidTier(tierIndex) ? LookOf(tiers[tierIndex]) : null;
        return look != null && look.ignoreViewBounds;
    }

    /// <summary>The look of a tier, or null (looks off / none defined).</summary>
    /// <summary>
    /// The look of this pixel type to EDIT at run time (the Look Studio): the one in the Pixel Looks list, or a new plain one added to the list when the
    /// type has none yet. Changes show after RefreshLooks (the cube) / PixelTierPicture.Rebuild (pictures); husks already on the floor keep their old look.
    /// </summary>
    public PixelLook EditableLook(PixelType type)
    {
        PixelLook look = PixelLooks.Find(looks, type);
        if (look != null) return look;
        look = new PixelLook { type = type };
        List<PixelLook> list = new List<PixelLook>(looks ?? new PixelLook[0]) { look };
        looks = list.ToArray();
        return look;
    }

    /// <summary>The look this pixel type has now, or null (Pixel Looks switched off, or the type has none).</summary>
    public PixelLook CurrentLook(PixelType type) => useLooks ? PixelLooks.Find(looks, type) : null;

    /// <summary>False when 'Use Looks' is switched off on PixelClicker (every pixel is then a plain coloured cube).</summary>
    public bool UseLooks => useLooks;

    private PixelLook LookOf(PixelTier tier) => useLooks && tier != null ? PixelLooks.Find(looks, tier.type) : null;

    /// <summary>The colour the 3D pixel is drawn in: the look's colour (alpha scaled) or the tier's own.</summary>
    private static Color RenderColor(PixelTier tier, PixelLook look)
    {
        Color c = look != null && look.useColor ? look.color : tier.color;
        if (look != null) c.a *= look.alpha;
        return c;
    }

    /// <summary>Puts a look's streak / crack texture on a property block (level = how damaged, 0..max).</summary>
    private static void ApplyLookTexture(MaterialPropertyBlock block, PixelLook look, int level, int max)
    {
        if (look == null || !look.HasSurfaceTexture) return;
        bool onlyBasic = look.blockSurface == PixelLook.BlockSurface.None && !look.streakTexture && !look.damageCracks && !look.chromeTexture && !look.oreTexture && !look.chestTexture && !look.meteorTexture && !look.sunTexture;
        Texture2D tex = look.blockSurface == PixelLook.BlockSurface.Seed ? PixelBlockTextures.SeedShell(look.damageCracks ? level : 0, max)
                      : look.blockSurface == PixelLook.BlockSurface.Pest ? PixelBlockTextures.PestHide(look.damageCracks ? level : 0, max)
                      : look.blockSurface == PixelLook.BlockSurface.Bottle ? PixelBlockTextures.BottleGlass(look.damageCracks ? level : 0, max)
                      : look.blockSurface != PixelLook.BlockSurface.None ? PixelBlockTextures.Get(look.blockSurface)
                      : onlyBasic ? PixelLooks.BasicTexture(look.basicSurface)
                      : look.meteorTexture ? PixelStarTextures.Meteor(look.damageCracks ? level : 0, max)
                      : look.sunTexture ? PixelStarTextures.Sun()
                      : look.chestTexture ? PixelLooks.ChestTexture(max > 0 ? Mathf.RoundToInt(Mathf.Clamp01(level / (float)max) * 8f) : 0)
                      : look.oreTexture ? PixelLooks.OreTexture(look.oreColor, look.damageCracks ? level : 0, max)
                      : look.chromeTexture ? PixelLooks.ChromeTexture()
                      : PixelLooks.SurfaceTexture(look.streakTexture, look.damageCracks ? level : 0, max, look.shellTexture);
        block.SetTexture("_BaseMap", tex);
        block.SetTexture("_MainTex", tex);
    }

    /// <summary>A hit on a tough pixel: cracks spread over the live cube (level 0 = undamaged, max = about to break).</summary>
    private void SetLiveDamage(PixelTier tier, int level)
    {
        if (pixelRenderer == null || activeLook == null || !activeLook.damageCracks) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        ApplyLookTexture(propertyBlock, activeLook, level, NeededClicks(tier));
        pixelRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>Metallic / glossiness from a look onto a property block.</summary>
    private static void ApplyLookSurface(MaterialPropertyBlock block, PixelLook look)
    {
        if (look == null) return;
        if (look.metallic >= 0f) block.SetFloat("_Metallic", look.metallic);
        if (look.smoothness >= 0f)
        {
            block.SetFloat("_Smoothness", look.smoothness);
            block.SetFloat("_Glossiness", look.smoothness);
        }
    }

    /// <summary>Adds / removes the outline and core on the live cube to match the tier's look.</summary>
    /// <summary>A text report about how glow is set up (materials, shaders, keywords, shell) for the rendering report file.</summary>
    public string DebugGlowReport()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("glowShell=" + glowShell + " (inEditor=" + glowShellInEditor + ")  glowShellAlpha=" + glowShellAlpha + "  useLooks=" + useLooks + "  looks=" + (looks != null ? looks.Length : 0));
        Material dm = defaultMaterial;
        sb.AppendLine("defaultMaterial=" + (dm != null ? dm.name + "  shader=" + (dm.shader != null ? dm.shader.name : "null") +
                      "  emissionKeyword=" + dm.IsKeywordEnabled("_EMISSION") + "  hasEmissionColor=" + dm.HasProperty("_EmissionColor") : "null"));
        if (pixelRenderer != null)
        {
            Material cm = pixelRenderer.sharedMaterial;
            sb.AppendLine("live cube material=" + (cm != null ? cm.name + "  shader=" + (cm.shader != null ? cm.shader.name : "null") +
                          "  emissionKeyword=" + cm.IsKeywordEnabled("_EMISSION") + "  supported=" + (cm.shader != null && cm.shader.isSupported) : "null"));
            sb.AppendLine("live glow shell object=" + (liveGlowShell != null ? "yes (active=" + liveGlowShell.activeInHierarchy + ")" : "no") +
                          "  clicking type=" + (tiers != null && currentTierIndex >= 0 && currentTierIndex < tiers.Length ? tiers[currentTierIndex].type.ToString() : "?"));
        }
        if (tiers != null)
            foreach (PixelTier t in tiers)
            {
                PixelLook l = LookOf(t);
                sb.AppendLine("tier " + t.type + ": unlocked=" + t.unlocked + " glow=" + t.glow + " glowIntensity=" + t.glowIntensity +
                              " translucent=" + t.translucent + " lookEmission=" + (l != null ? l.emission.ToString() : "-") +
                              " wantsShell=" + WantsGlowShell(t, l));
            }
        return sb.ToString();
    }

    private GameObject liveGlowShell, liveGlowHalo;
    private PixelType liveGlowShellType = (PixelType)(-1);

    /// <summary>Does this tier get the build-proof glow shell (glowing or emissive, and not see-through)?</summary>
    private bool WantsGlowShell(PixelTier tier, PixelLook look) => glowShell && glowShellAlpha > 0f && IsGlowingSolid(tier, look);

    /// <summary>Does this tier get the soft glow halo (glowing or emissive, not see-through, and the halo is not hidden)?</summary>
    private bool WantsGlowHalo(PixelTier tier, PixelLook look) => glowHalo && IsGlowingSolid(tier, look);

    private static bool IsGlowingSolid(PixelTier tier, PixelLook look)
    {
        if (tier == null) return false;
        bool glowing = tier.glow || (look != null && look.emission > 0f);
        bool seeThrough = tier.translucent || (look != null && look.forceTranslucent);
        return glowing && !seeThrough;
    }

    private float GlowShellAlpha(PixelTier tier, float extra = 1f) =>
        Mathf.Clamp((glowShellAlpha + 0.1f * tier.glowIntensity) * extra, 0.25f, 0.92f);

    /// <summary>How far above 1.0 the shell's colour goes (so Bloom lights it up like emission): grows with the pixel's glow intensity.</summary>
    private float GlowShellHdr(PixelTier tier) => Mathf.Max(1f, 1f + tier.glowIntensity * Mathf.Max(0f, 0.35f + glowShellBrightnessExtra));

    private Color GlowShellColor(PixelTier tier) => Color.Lerp(tier.color, Color.white, 0.35f);

    private void UpdateGlowShell(PixelTier tier, PixelLook look)
    {
        MeshFilter mf = pixelRenderer != null ? pixelRenderer.GetComponent<MeshFilter>() : null;
        bool shell = mf != null && WantsGlowShell(tier, look);
        bool halo = pixelRenderer != null && mf != null && WantsGlowHalo(tier, look);
        if (!shell && !halo)
        {
            if (liveGlowShell != null) Destroy(liveGlowShell);
            if (liveGlowHalo != null) Destroy(liveGlowHalo);
            liveGlowShell = liveGlowHalo = null;
            liveShellRenderer = liveHaloRenderer = null;
            return;
        }
        if (liveGlowShellType == tier.type && (liveGlowShell != null) == shell && (liveGlowHalo != null) == halo) return;

        if (liveGlowShell != null) Destroy(liveGlowShell);
        if (liveGlowHalo != null) Destroy(liveGlowHalo);
        liveShellColor = GlowShellColor(tier);
        liveShellAlpha = GlowShellAlpha(tier);
        liveShellHdr = GlowShellHdr(tier);
        liveGlowShell = shell ? PixelLooks.AddGlowShell(pixelRenderer.transform, mf.sharedMesh, liveShellColor, liveShellAlpha, hdr: liveShellHdr) : null;
        liveGlowHalo = halo ? PixelLooks.AddGlowHalo(pixelRenderer.transform, liveShellColor, liveShellAlpha, liveShellHdr) : null;
        liveGlowShellType = tier.type;
        liveShellRenderer = liveGlowShell != null ? liveGlowShell.GetComponent<MeshRenderer>() : null;
        liveHaloRenderer = liveGlowHalo != null ? liveGlowHalo.GetComponent<MeshRenderer>() : null;
    }

    private void UpdateLiveExtras(PixelTier tier, PixelLook look)
    {
        bool wanted = look != null && look.HasExtras && pixelRenderer != null;
        if (!wanted)
        {
            if (liveExtras != null) Destroy(liveExtras);
            liveExtras = null;
            liveExtrasBuilt = false;
            return;
        }
        if (liveExtrasBuilt && liveExtras != null && liveExtrasType == tier.type) return;

        if (liveExtras != null) Destroy(liveExtras);
        MeshFilter mf = pixelRenderer.GetComponent<MeshFilter>();
        liveExtras = mf != null ? PixelLooks.AddExtras(pixelRenderer.transform, mf.sharedMesh, look, tier.color, defaultMaterial) : null;
        liveExtrasType = tier.type;
        liveExtrasBuilt = true;
    }

    private void ApplyPixelColor(Color color, bool remember = true)
    {
        if (remember) currentColor = color;
        if (pixelRenderer == null) return;
        pixelRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(colorPropertyId, color);
        // Also set the built-in name so either pipeline works if the property name is left default.
        propertyBlock.SetColor("_Color", color);

        ApplyLookSurface(propertyBlock, activeLook);

        // Glowing tiers: emission follows the colour (and breathes). Black emission = off for everything else.
        if (activeGlow > 0f)
        {
            float breath = GlowBreath(activeGlowType, Time.time);
            float scale = activeLook != null ? activeLook.glowScale : 1f;
            Color emission = new Color(color.r, color.g, color.b, 1f) * (activeGlow * breath * scale);
            propertyBlock.SetColor("_EmissionColor", emission);
        }
        else if (activeLook != null && activeLook.emission > 0f)
        {
            // Plain self-lighting from the look (so e.g. white pixels stand out).
            propertyBlock.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * activeLook.emission);
        }
        else
        {
            propertyBlock.SetColor("_EmissionColor", Color.black);
        }
        pixelRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>The material a tier's pixel is drawn with (normal, translucent, override, or glowing).</summary>
    private Material MaterialForTier(PixelTier tier)
    {
        Material wanted = defaultMaterial;
        if (tier.materialOverride != null) wanted = tier.materialOverride;
        else if ((tier.translucent || (LookOf(tier) != null && LookOf(tier).forceTranslucent)) && defaultMaterial != null)
        {
            if (transparentMaterial == null) transparentMaterial = BuildTransparentMaterial(defaultMaterial);
            wanted = transparentMaterial;
        }

        // Glowing tiers need a copy of the material with emission switched on.
        PixelLook styled = LookOf(tier);
        bool selfLit = tier.glow || (styled != null && styled.emission > 0f);
        if (selfLit && tier.materialOverride == null && wanted != null) wanted = GetGlowMaterial(wanted);
        return wanted;
    }

    /// <summary>Applies a tier's material (normal, translucent or override) and colour to the pixel.</summary>
    private void ApplyTierLook(PixelTier tier)
    {
        if (pixelRenderer != null)
        {
            Material wanted = MaterialForTier(tier);
            if (wanted != null && pixelRenderer.sharedMaterial != wanted) pixelRenderer.sharedMaterial = wanted;
        }

        activeGlow = tier.glow ? tier.glowIntensity : 0f;
        activeGlowType = tier.type;
        activeLook = LookOf(tier);
        if (pixelRenderer != null)
        {
            MeshFilter liveMf = pixelRenderer.GetComponent<MeshFilter>();
            Mesh wantedMesh = MeshFor(activeLook);
            if (liveMf != null && wantedMesh != null && liveMf.sharedMesh != wantedMesh) liveMf.sharedMesh = wantedMesh;   // rocky pixels are drawn jagged
        }
        UpdateGlowLight(tier);
        UpdateLiveExtras(tier, activeLook);
        UpdateGlowShell(tier, activeLook);
        if (pixelRenderer != null)
        {
            // Start from a clean block so nothing (texture, glossiness) is left over from the previous pixel type.
            propertyBlock.Clear();
            pixelRenderer.SetPropertyBlock(propertyBlock);
        }
        ApplyPixelColor(RenderColor(tier, activeLook));
        if (pixelRenderer != null && activeLook != null && activeLook.HasSurfaceTexture)
        {
            pixelRenderer.GetPropertyBlock(propertyBlock);
            ApplyLookTexture(propertyBlock, activeLook, 0, NeededClicks(tier));
            pixelRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>Copies a material and switches it to alpha blending (Standard or URP Lit/Unlit).</summary>
    /// <summary>A copy of the material with emission enabled (cached per source material).</summary>
    private Material GetGlowMaterial(Material source)
    {
        if (glowMaterials.TryGetValue(source, out Material cached) && cached != null) return cached;

        Material copy = new Material(source) { name = source.name + " (Glow)" };
        copy.EnableKeyword("_EMISSION");
        copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        glowMaterials[source] = copy;
        return copy;
    }

    /// <summary>Creates / updates / hides the point light that goes with a glowing tier.</summary>
    private void UpdateGlowLight(PixelTier tier)
    {
        bool wanted = tier.glow && glowCastsLight && pixelTransform != null;
        if (wanted && glowLight == null)
        {
            GameObject go = new GameObject("Glow Light");
            go.transform.SetParent(pixelTransform, false);
            glowLight = go.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.shadows = LightShadows.None;
        }

        if (glowLight == null) return;
        glowLight.enabled = wanted;
        if (!wanted) return;

        glowLight.color = new Color(tier.color.r, tier.color.g, tier.color.b, 1f);
        glowLight.range = glowLightRange;
        glowLight.intensity = glowLightIntensity * tier.glowIntensity;
    }

    public static Material BuildTransparentMaterial(Material source)
    {
        Material m = new Material(source) { name = source.name + " (Transparent)" };

        if (m.HasProperty("_Surface")) // URP
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
        }
        else if (m.HasProperty("_Mode")) // Built-in Standard
        {
            m.SetFloat("_Mode", 3f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    /// <summary>
    /// A copy of the pixel's material tinted with 'color' - for simple scene objects that should match the game's look.
    /// 'transparent' makes it see-through (the colour's alpha sets how much).
    /// </summary>
    public Material CreateVisualMaterial(Color color, bool transparent)
    {
        Material source = defaultMaterial != null ? defaultMaterial
                        : pixelRenderer != null ? pixelRenderer.sharedMaterial : null;
        if (source == null) return null;

        Material mat = transparent ? BuildTransparentMaterial(source) : new Material(source);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }
}
