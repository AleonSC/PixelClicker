// ---- System notes ----
// PixelLooks.cs
//   PixelLook (one pixel type's extra 3D style) and PixelLooks (the helpers that build a look's runtime-drawn parts: neon edges, the dark-matter core,
//   glow shells / halos, shatter shards), plus the small husk / look behaviours (gravity well, float, wobble, shine, spin, shatter, lightning).
//   Part files: PixelLooksLook.cs (the PixelLook data class), PixelLooksDefaults.cs (the built-in looks table), PixelLooksTextures.cs (drawn textures),
//   PixelLooksMeshes.cs (the code-built meshes). The shared static caches stay in this file.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Helpers that build the runtime-drawn parts of a look: neon edges, the dark-matter core, shatter shards.</summary>
public static partial class PixelLooks
{
    private static readonly Dictionary<int, Texture2D> oreTextures = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, Texture2D> chestTextures = new Dictionary<int, Texture2D>();

    private static readonly Dictionary<int, Texture2D> surfaceTextures = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, Texture2D> basicTextures = new Dictionary<int, Texture2D>();

    // ------------------------------------------------------------------
    // Extras: neon edges and the dark-matter core
    // ------------------------------------------------------------------

    private static Material neonMaterial, coreMaterial;
    private static Texture2D coreTexture;
    private static readonly Dictionary<int, Mesh> edgeMeshes = new Dictionary<int, Mesh>();

    /// <summary>
    /// Builds the extra objects of a look under 'parent' (the pixel's mesh object, so they share its size and rotation).
    /// Returns the container (destroy it to remove them), or null if the look adds nothing.
    /// </summary>
    public static GameObject AddExtras(Transform parent, Mesh cube, PixelLook look, Color tierColor, Material baseMaterial)
    {
        if (parent == null || cube == null || look == null || !look.HasExtras) return null;

        GameObject root = new GameObject("Look Extras");
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        Vector3 size = cube.bounds.size;
        Vector3 centre = cube.bounds.center;

        if (look.flames) PixelFlames.Attach(root.transform, centre, size);
        if (look.radiation) PixelRadiationFx.Attach(root.transform, centre, size);
        if (look.cometTail) PixelCometFx.Attach(root.transform, centre, size);
        if (look.windFx) PixelVacuumFx.Attach(root.transform, centre, size);
        if (look.bottleMap) PixelBottleMap.Attach(root.transform, centre, size, baseMaterial);
        if (look.gelFx) PixelGelFx.Attach(root.transform, centre, size);
        if (look.sunFx) PixelSunFx.Attach(root.transform, centre, size);
        if (look.farmFx != PixelLook.FarmAmbient.None) PixelFarmFx.Attach(root.transform, centre, size, look.farmFx);
        if (look.obsidianFx) PixelObsidianFx.Attach(root.transform, centre, size);
        if (look.electricFx != PixelLook.ElectricAmbient.None) PixelElectricFx.Attach(root.transform, centre, size, look.electricFx);

        if (look.outline)
        {
            Color c = look.outlineUsesTierColor ? new Color(tierColor.r, tierColor.g, tierColor.b, 1f) : look.outlineColor;
            if (Mathf.Abs(look.outlineShade) > 0.001f)
            {
                // A lighter or darker shade of the pixel's own colour (its 3D colour if the look overrides it).
                Color own = look.useColor && !look.outlineShadeOfTier ? look.color : tierColor;
                c = look.outlineShade > 0f ? Color.Lerp(own, Color.white, look.outlineShade) : Color.Lerp(own, Color.black, -look.outlineShade);
                c.a = 1f;
            }
            GameObject edges = new GameObject("Neon Edges", typeof(MeshFilter), typeof(MeshRenderer));
            edges.transform.SetParent(root.transform, false);
            edges.transform.localPosition = centre;
            edges.layer = root.layer;
            edges.GetComponent<MeshFilter>().sharedMesh = EdgeMesh(size, look.outlineThickness, c, look.outlineStrength, look.outlineBevel);
            MeshRenderer mr = edges.GetComponent<MeshRenderer>();
            mr.sharedMaterial = NeonMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        if (look.faceCircles)
        {
            GameObject circles = new GameObject("Face Circles", typeof(MeshFilter), typeof(MeshRenderer));
            circles.transform.SetParent(root.transform, false);
            circles.transform.localPosition = centre;
            circles.layer = root.layer;
            circles.GetComponent<MeshFilter>().sharedMesh = look.faceRing ? VortexMesh(size, look.faceCircleRadius, look.faceCircleColor, look.faceRingColor) : CircleMesh(size, look.faceCircleRadius, look.faceCircleColor);
            MeshRenderer cr = circles.GetComponent<MeshRenderer>();
            cr.sharedMaterial = OverlayMaterial(); // drawn after the (translucent) pixel, so the circles stay solid black
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;
        }

        if (look.colorBlindSides >= 3 && PixelDisplaySettings.ColorBlind)
        {
            GameObject marks = new GameObject("Color Blind Marks", typeof(MeshFilter), typeof(MeshRenderer));
            marks.transform.SetParent(root.transform, false);
            marks.transform.localPosition = centre;
            marks.layer = root.layer;
            marks.GetComponent<MeshFilter>().sharedMesh = CircleMesh(size, look.colorBlindRadius, look.colorBlindColor, look.colorBlindSides, look.colorBlindRotation);
            MeshRenderer kr = marks.GetComponent<MeshRenderer>();
            kr.sharedMaterial = OverlayMaterial();
            kr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            kr.receiveShadows = false;
        }

        if (look.starCubes > 0)
        {
            GameObject stars = new GameObject("Dragon Stars", typeof(MeshFilter), typeof(MeshRenderer));
            stars.transform.SetParent(root.transform, false);
            stars.transform.localPosition = centre;
            stars.layer = root.layer;
            stars.GetComponent<MeshFilter>().sharedMesh = StarCubesMesh(size, look.starCubes, look.starColor);
            MeshRenderer sr = stars.GetComponent<MeshRenderer>();
            sr.sharedMaterial = OverlayMaterial(); // unlit vertex colours, drawn after the see-through body so they show through it
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }

        if (look.shine)
        {
            GameObject shine = new GameObject("Shine", typeof(MeshFilter), typeof(MeshRenderer));
            shine.transform.SetParent(root.transform, false);
            shine.transform.localPosition = centre;
            shine.layer = root.layer;
            MeshFilter sf = shine.GetComponent<MeshFilter>();
            sf.sharedMesh = ShineMesh(size, look.jagged, (int)look.type);
            MeshRenderer shr = shine.GetComponent<MeshRenderer>();
            shr.sharedMaterial = OverlayMaterial(); // unlit, alpha-blended vertex colours
            shr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shr.receiveShadows = false;
            shine.AddComponent<PixelLookShine>();
        }

        if (look.sprout)
        {
            GameObject sprout = new GameObject("Sprout", typeof(MeshFilter), typeof(MeshRenderer));
            sprout.transform.SetParent(root.transform, false);
            sprout.transform.localPosition = centre;
            sprout.layer = root.layer;
            sprout.GetComponent<MeshFilter>().sharedMesh = SproutMesh(size);
            MeshRenderer spr = sprout.GetComponent<MeshRenderer>();
            spr.sharedMaterial = OverlayMaterial(); // unlit vertex colours
            spr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spr.receiveShadows = false;
        }

        if (look.lightning)
        {
            GameObject bolts = new GameObject("Lightning", typeof(MeshFilter), typeof(MeshRenderer));
            bolts.transform.SetParent(root.transform, false);
            bolts.transform.localPosition = centre;
            bolts.layer = root.layer;
            MeshRenderer lr = bolts.GetComponent<MeshRenderer>();
            lr.sharedMaterial = OverlayMaterial(); // unlit vertex colours, drawn after the (see-through) body
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            bolts.AddComponent<PixelLookLightning>().Setup(size, look.lightningCoreColor, look.lightningGlowColor, look.lightningJitter);
        }

        if (look.darkMatter && baseMaterial != null)
        {
            GameObject core = new GameObject("Dark Matter Core", typeof(MeshFilter), typeof(MeshRenderer));
            core.transform.SetParent(root.transform, false);
            core.transform.localPosition = centre;
            core.transform.localScale = Vector3.one * 0.6f;
            core.layer = root.layer;
            core.GetComponent<MeshFilter>().sharedMesh = cube;
            MeshRenderer mr = core.GetComponent<MeshRenderer>();
            mr.sharedMaterial = CoreMaterial(baseMaterial);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.AddComponent<PixelLookSpin>().Setup(new Vector3(35f, 55f, 20f), 0.1f, 1.6f);
        }
        return root;
    }

    private static readonly Dictionary<int, Mesh> starMeshes = new Dictionary<int, Mesh>();

    private static Texture2D chromeTex;

    /// <summary>Vertices along one side of a Shine face grid.</summary>
    public const int ShineGrid = 8;

    /// <summary>
    /// A finely divided sheet just above each cube face. <see cref="PixelLookShine"/> animates the vertex alpha so a soft diagonal band of light
    /// sweeps across (a plain four-corner quad could only blink as a white rectangle).
    /// </summary>
    // (cached like the edge meshes: PixelLookShine copies the mesh for each cube, so the original must be shared, not made again per cube and orphaned)
    private static readonly Dictionary<int, Mesh> shineMeshes = new Dictionary<int, Mesh>();

    private static readonly Dictionary<int, Mesh> sproutMeshes = new Dictionary<int, Mesh>();

    /// <summary>Height of a free-standing sprout's stem top as a fraction of its size (where a growing pixel sits).</summary>
    public const float SproutStemTop = 0.26f;

    private static Material NeonMaterial()
    {
        if (neonMaterial != null) return neonMaterial;
        // Sprites/Default is unlit and uses vertex colours in every render pipeline.
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        neonMaterial = new Material(shader) { name = "NeonEdges" };
        return neonMaterial;
    }

    private static Material overlayMaterial;

    /// <summary>
    /// The unlit vertex-colour material for flat marks lying on a face (Vacuum's black circles, colour-blind shapes). It is
    /// drawn after the pixel itself (a higher render queue), so a translucent pixel can't be blended over the marks and tint them.
    /// </summary>
    internal static Material OverlayMaterial()
    {
        if (overlayMaterial != null) return overlayMaterial;
        Material source = NeonMaterial();
        if (source == null) return null;
        overlayMaterial = new Material(source) { name = "FaceMarks", renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50 };
        return overlayMaterial;
    }

    /// <summary>
    /// A glow that does not depend on shader keywords: a slightly larger copy of the mesh drawn on top with the unlit
    /// Sprites/Default shader (alpha-blended colour). Emission switched on from code is stripped from builds when no saved
    /// material uses it, so this keeps glowing pixels visibly luminous in a built game. Returns the new object (a child of 'parent').
    /// 'texture' (optional) is drawn instead of a flat colour (alpha of the texture = how much shows), e.g. lava cracks.
    /// </summary>
    public static GameObject AddGlowShell(Transform parent, Mesh mesh, Color colour, float alpha, float scale = 1.012f, Texture texture = null, float hdr = 1f)
    {
        Material material = OverlayMaterial();
        if (material == null || mesh == null || parent == null) return null;

        GameObject go = new GameObject("Glow Shell", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        // 'hdr' > 1 pushes the colour above 1.0 (HDR): that is what makes the post-processing Bloom light the pixel up, exactly as
        // the emission does in the Editor (the colour is unlit, so it goes straight to the screen buffer).
        block.SetColor("_Color", new Color(colour.r * hdr, colour.g * hdr, colour.b * hdr, Mathf.Clamp01(alpha)));
        if (texture != null) block.SetTexture("_MainTex", texture);
        mr.SetPropertyBlock(block);
        return go;
    }

    private static Mesh haloMesh;
    private static Texture2D haloTexture;

    /// <summary>
    /// A soft round camera-facing glow behind the pixel (like bloom, but drawn with the unlit Sprites/Default shader, so it
    /// needs no post-processing and no shader keywords). The pixel's own faces hide the middle of it, so only the halo around
    /// the silhouette shows.
    /// </summary>
    public static GameObject AddGlowHalo(Transform parent, Color colour, float alpha, float hdr, float boost = 1f)
    {
        Material material = OverlayMaterial();
        if (material == null || parent == null) return null;

        if (haloMesh == null)
        {
            haloMesh = new Mesh { name = "Glow Halo" };
            haloMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            haloMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            haloMesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
            haloMesh.RecalculateBounds();
        }
        if (haloTexture == null)
        {
            const int n = 128;
            haloTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GlowHalo" };
            Color32[] px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - r, 2.8f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            haloTexture.SetPixels32(px);
            haloTexture.Apply(false, true);
        }

        GameObject go = new GameObject("Glow Halo", typeof(MeshFilter), typeof(MeshRenderer), typeof(PixelFaceCamera));
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 1.9f;
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = haloMesh;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_Color", new Color(colour.r * hdr * 0.7f, colour.g * hdr * 0.7f, colour.b * hdr * 0.7f, Mathf.Clamp01(alpha * 0.3f * boost)));
        block.SetTexture("_MainTex", haloTexture);
        mr.SetPropertyBlock(block);
        return go;
    }

    private static Material CoreMaterial(Material baseMaterial)
    {
        if (coreMaterial != null) return coreMaterial;
        if (coreTexture == null) coreTexture = BuildDarkMatterTexture(128);

        coreMaterial = new Material(baseMaterial) { name = "DarkMatterCore" };
        coreMaterial.SetTexture("_BaseMap", coreTexture);
        coreMaterial.SetTexture("_MainTex", coreTexture);
        coreMaterial.SetTexture("_EmissionMap", coreTexture);
        if (coreMaterial.HasProperty("_BaseColor")) coreMaterial.SetColor("_BaseColor", Color.white);
        if (coreMaterial.HasProperty("_Color")) coreMaterial.SetColor("_Color", Color.white);
        if (coreMaterial.HasProperty("_EmissionColor")) coreMaterial.SetColor("_EmissionColor", new Color(1.3f, 1.3f, 1.3f, 1f));
        if (coreMaterial.HasProperty("_Metallic")) coreMaterial.SetFloat("_Metallic", 0f);
        coreMaterial.EnableKeyword("_EMISSION");
        coreMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return coreMaterial;
    }

    private static readonly Dictionary<int, Mesh> circleMeshes = new Dictionary<int, Mesh>();

    private static readonly Dictionary<int, Mesh> vortexMeshes = new Dictionary<int, Mesh>();

    private static readonly int[][] BoxQuads =
    {
        new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 4, 6, 2 },
        new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 }, new[] { 2, 6, 7, 3 },
    };

}

/// <summary>
/// An old pixel that slowly pulls the other old pixels within a radius towards itself (singularity). A very faint ring
/// (a camera-facing sprite) shows how far the pull reaches. The ring follows the pixel and shrinks with it when it despawns.
/// </summary>
public class OldPixelGravityWell : MonoBehaviour
{
    private PixelClicker clicker;
    private float radius, pullSpeed, grip;
    private GameObject ring;
    private SpriteRenderer ringRenderer;
    private float startScale;
    private static Sprite ringSprite;

    private PixelLook look; // read every frame, so changing the values in the Inspector while playing takes effect at once

    // The pixels this well is holding right now (at most wellMaxPixels). A pixel is claimed by one well only.
    private readonly HashSet<Rigidbody> members = new HashSet<Rigidbody>();
    private readonly HashSet<Rigidbody> inReach = new HashSet<Rigidbody>();
    private readonly List<Rigidbody> candidates = new List<Rigidbody>();
    private readonly List<Rigidbody> leaving = new List<Rigidbody>();   // (reused every step: members that are no longer in reach)
    private Vector3 sortCentre;
    private Comparison<Rigidbody> byDistance;   // (made once: a lambda capturing the centre made a new one every physics step)
    private static readonly Dictionary<Rigidbody, OldPixelGravityWell> claims = new Dictionary<Rigidbody, OldPixelGravityWell>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { claims.Clear(); }

    public void Setup(PixelClicker owner, PixelLook source)
    {
        clicker = owner;
        look = source;
        ReadSettings();
        startScale = Mathf.Max(0.0001f, transform.lossyScale.x);

        if (ringSprite == null) ringSprite = BuildRingSprite();
        ring = new GameObject("Gravity Ring");
        ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = ringSprite;
        ringRenderer.sortingOrder = -2;
        ApplyRingColour();
    }

    private void Start()
    {
        // The well pixel lives much longer than a normal old pixel.
        OldPixelDespawn own = GetComponent<OldPixelDespawn>();
        if (own != null) own.SetLifetime(look != null && look.wellLifetime > 0f ? look.wellLifetime : 300f);
    }

    private void ReadSettings()
    {
        if (look == null) return;
        radius = look.wellRadius;
        pullSpeed = look.wellPullSpeed;
        grip = look.wellGrip;
    }

    private void ApplyRingColour()
    {
        if (ringRenderer == null || look == null) return;
        Color c = look.wellRingColor;
        c.a = look.wellRingOpacity;
        ringRenderer.color = c;
    }



    private void FixedUpdate()
    {
        ReadSettings();
        if (clicker == null || pullSpeed <= 0f) return;
        // The well keeps working while the player carries it (Pixel Grabbing); it only stops once it is shrinking away.
        if (transform.lossyScale.x < startScale * 0.9f) return;

        var list = clicker.OldPixels;
        Vector3 centre = transform.position;
        float blend = Mathf.Clamp01(grip * Time.fixedDeltaTime);
        candidates.Clear();
        inReach.Clear();
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody other = list[i];
            if (other == null || other.gameObject == gameObject) continue;
            if (clicker.IsFlyingPixel(other))
            {
                // A streaking meteor pixel is bent towards the well from far beyond the ring (it flies up and away from the floor,
                // so it would never come near otherwise) and slowed; once inside the ring it is captured and dragged in.
                float reach = radius * (look != null && look.wellFlyReach > 0f ? look.wellFlyReach : 4f);
                float sqr = (centre - other.position).sqrMagnitude;
                if (sqr > reach * reach) continue;
                if (sqr > radius * radius)
                {
                    // Never dips below the floor: a trigger-collider pixel has nothing to stop it, so keep it at or above the well's height.
                    if (other.position.y < centre.y)
                    {
                        Vector3 p = other.position;
                        p.y = centre.y;
                        other.position = p;
                        Vector3 low = PixelBody.Velocity(other);
                        if (low.y < 0f) { low.y = 0f; PixelBody.Steer(other, low); }
                    }
                    Vector3 fv = PixelBody.Velocity(other);
                    float fs = fv.magnitude;
                    if (fs > 0.01f)
                    {
                        Vector3 steered = Vector3.RotateTowards(fv.normalized, (centre - other.position).normalized, 5f * Time.fixedDeltaTime, 0f);
                        if (steered.y < 0f && other.position.y <= centre.y + 0.3f) steered.y = 0f; // skim along the floor, not into it
                        PixelBody.Steer(other, steered * (fs * Mathf.Clamp01(1f - 1.2f * Time.fixedDeltaTime)));
                    }
                    OldPixelDespawn fd = PixelHuskQuery.Get(other).despawn;
                    if (fd != null) fd.KeepAlive();
                    continue;
                }
                clicker.LandFlyingPixel(other);
                PixelBody.Steer(other, PixelBody.Velocity(other) * 0.2f);
            }
            if (other.isKinematic) continue;
            if ((centre - other.position).sqrMagnitude > radius * radius) continue;   // (the distance first: most husks are out of reach)
            OldPixelDespawn despawn = PixelHuskQuery.Get(other).despawn;
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue; // carried by the player / vanishing
            candidates.Add(other);
            inReach.Add(other);
        }

        // Keep the pixels already held that are still in reach; free the others.
        leaving.Clear();
        foreach (Rigidbody m in members) if (m == null || !inReach.Contains(m)) leaving.Add(m);
        foreach (Rigidbody m in leaving)
        {
            members.Remove(m);
            if (m != null && claims.TryGetValue(m, out OldPixelGravityWell owner) && owner == this) claims.Remove(m);
        }

        // Fill the free slots with the nearest unclaimed pixels in reach (other gravity wells count as pixels too, which forces spacing).
        int cap = look != null && look.wellMaxPixels > 0 ? look.wellMaxPixels : 20;
        if (members.Count < cap)
        {
            sortCentre = centre;
            if (byDistance == null) byDistance = (a, b) => (sortCentre - a.position).sqrMagnitude.CompareTo((sortCentre - b.position).sqrMagnitude);
            candidates.Sort(byDistance);
            foreach (Rigidbody c in candidates)
            {
                if (members.Count >= cap) break;
                if (members.Contains(c)) continue;
                if (claims.TryGetValue(c, out OldPixelGravityWell owner) && owner != null && owner != this) continue; // another well has it
                claims[c] = this;
                members.Add(c);
            }
        }

        foreach (Rigidbody other in members)
        {
            Vector3 to = centre - other.position;
            float distance = to.magnitude;
            OldPixelDespawn despawn = PixelHuskQuery.Get(other).despawn;
            if (despawn != null) despawn.KeepAlive(); // held by the well: it doesn't age while the well works
            if (distance < 0.08f) continue;

            // A constant drag: whatever the pixel is doing (even sitting on the floor against its friction), its speed towards
            // the well is brought up to the drag speed. Speed it already has towards the well is kept.
            Vector3 dir = to / distance;
            Vector3 v = PixelBody.Velocity(other);
            float towards = Vector3.Dot(v, dir);
            if (towards < pullSpeed) PixelBody.Steer(other, v + dir * ((pullSpeed - towards) * blend));
        }
    }

    private void LateUpdate()
    {
        if (ring == null) return;
        ApplyRingColour();
        Camera cam = PixelClicker.ViewCameraOf(clicker);
        float shrink = Mathf.Clamp01(transform.lossyScale.x / startScale);
        ring.transform.position = transform.position;
        if (cam != null) ring.transform.rotation = cam.transform.rotation;
        ring.transform.localScale = Vector3.one * (radius * 2f * shrink);
    }

    private void OnDestroy()
    {
        foreach (Rigidbody m in members) claims.Remove(m); // let go of everything it held
        members.Clear();
        if (ring != null) Destroy(ring);
    }

    /// <summary>A thin round ring with a soft edge (white; tinted by the sprite colour).</summary>
    private static Sprite BuildRingSprite()
    {
        const int s = 128;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "GravityRing" };
        Color32[] px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float u = ((x + 0.5f) / s - 0.5f) * 2f, v = ((y + 0.5f) / s - 0.5f) * 2f;
                float d = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.94f);          // distance from the ring line
                float a = Mathf.Clamp01(1f - d / 0.035f);                          // the line
                a = Mathf.Max(a, Mathf.Clamp01(1f - d / 0.12f) * 0.25f);           // a soft glow beside it
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 128f);
    }
}

/// <summary>Squash-and-stretch scale for the jelly wobble (keeps the volume about the same).</summary>
public static class PixelWobble
{
    public static Vector3 Scale(float time, float amount, float speed, float phase)
    {
        float t = time * speed + phase;
        float x = 1f + amount * Mathf.Sin(t);
        float y = 1f + amount * Mathf.Sin(t * 1.31f + 2.1f);
        float z = 1f + amount * Mathf.Sin(t * 0.77f + 4.2f);
        float k = 1f / Mathf.Pow(Mathf.Max(0.01f, x * y * z), 1f / 3f);
        return new Vector3(x * k, y * k, z * k);
    }
}

/// <summary>Wobbles a falling copy's visual (a child of the old pixel, so the pixel's own scale animations still work).</summary>
public class PixelLookWobble : MonoBehaviour
{
    private float amount, speed, phase;

    public void Setup(float wobbleAmount, float wobbleSpeed)
    {
        amount = wobbleAmount;
        speed = wobbleSpeed;
        phase = UnityEngine.Random.value * 6.28f;
    }

    private void Update()
    {
        transform.localScale = PixelWobble.Scale(Time.time, amount, speed, phase);
    }
}

/// <summary>
/// An old pixel that bounces a few times and then floats away off the screen (ghosts). It stops colliding once it floats,
/// drifts sideways and upwards, and is removed when it is well outside the camera's view.
/// </summary>
public class OldPixelFloat : MonoBehaviour
{
    private PixelClicker clicker;
    private int bouncesLeft;
    private float lift, drift;
    private bool floating;
    private Rigidbody body;

    public void Setup(PixelClicker owner, int bounces, float liftFraction, float driftSpeed)
    {
        clicker = owner;
        bouncesLeft = Mathf.Max(1, bounces);
        lift = liftFraction;
        drift = driftSpeed;
        body = GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (floating) return;
        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (Vector3.Dot(collision.GetContact(0).normal, Vector3.up) < 0.4f) return;  // only the ground
        if (--bouncesLeft > 0) return;
        StartFloating();
    }

    private void StartFloating()
    {
        floating = true;
        PixelAudio.Play("ghost_float");
        ScaledGravity gravity = GetComponent<ScaledGravity>();
        if (gravity != null) gravity.scale = -lift; // gently upwards from now on
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false; // nothing stops it any more

        Camera cam = PixelClicker.ViewCameraOf(clicker);
        Vector3 side = cam != null ? cam.transform.right : Vector3.right;
        float sign = cam != null && cam.WorldToViewportPoint(transform.position).x < 0.5f ? -1f : 1f;
        if (UnityEngine.Random.value < 0.25f) sign = -sign;
        if (body != null) body.AddForce(side * (sign * drift) + Vector3.up * (drift * 0.4f), ForceMode.VelocityChange);
    }

    private static readonly RaycastHit[] groundHits = new RaycastHit[12];

    /// <summary>
    /// A floating ghost pixel has no colliders, so a gravity well dragging it around could pull it through the floor. While it
    /// floats it is kept above the ground below it instead (the ground = the nearest upward-facing surface that is not an old pixel or the cube).
    /// </summary>
    private void FixedUpdate()
    {
        if (!floating || body == null) return;
        Vector3 pos = body.position;
        float half = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)) * 0.5f;
        int n = Physics.RaycastNonAlloc(pos + Vector3.up * (half * 6f), Vector3.down, groundHits, half * 40f, ~0, QueryTriggerInteraction.Ignore);
        float groundY = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = groundHits[i];
            if (h.collider == null || h.normal.y < 0.5f) continue;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
            if (clicker != null && clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.point.y > groundY) groundY = h.point.y;
        }
        if (float.IsNegativeInfinity(groundY) || pos.y >= groundY + half) return;
        pos.y = groundY + half;
        body.position = pos;
#if UNITY_6000_0_OR_NEWER
        Vector3 vel = body.linearVelocity;
        if (vel.y < 0f) { vel.y = 0f; body.linearVelocity = vel; }
#else
        Vector3 vel = body.velocity;
        if (vel.y < 0f) { vel.y = 0f; body.velocity = vel; }
#endif
    }

    private void Update()
    {
        if (!floating) return;
        Camera cam = PixelClicker.ViewCameraOf(clicker);
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        if (v.z < 0f || v.x < -0.4f || v.x > 1.4f || v.y > 1.4f || v.y < -0.4f)
        {
            if (clicker != null && body != null) clicker.ReleaseOldPixel(body, false);
            Destroy(gameObject);
        }
    }
}

/// <summary>Sweeps a soft diagonal band of light across the Mirror pixel's faces in turn (animates the vertex alpha of the Shine mesh).</summary>
public class PixelLookShine : MonoBehaviour
{
    private Mesh mesh;
    private Color[] colors;
    private float[] diagonal;      // each vertex's position along the sweep direction, -1..1
    private float phase;
    private bool wasActive;

    private void Awake()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        mesh = Instantiate(mf.sharedMesh); // each cube gets its own copy so the glints are independent
        mf.sharedMesh = mesh;
        phase = UnityEngine.Random.value * 6.28f;

        int n = PixelLooks.ShineGrid, perFace = (n + 1) * (n + 1);
        colors = new Color[perFace * 6];
        diagonal = new float[perFace * 6];
        for (int face = 0; face < 6; face++)
            for (int iy = 0; iy <= n; iy++)
                for (int ix = 0; ix <= n; ix++)
                    diagonal[face * perFace + iy * (n + 1) + ix] = (ix + iy) / (float)n - 1f;
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }

    private void Update()
    {
        if (mesh == null) return;
        int n = PixelLooks.ShineGrid, perFace = (n + 1) * (n + 1);
        float time = Time.unscaledTime * 0.55f + phase;
        bool any = false;
        for (int face = 0; face < 6; face++)
        {
            // Each face gets a sweep once per cycle (a little after the previous face); the rest of the time it is clear.
            float s = Mathf.Repeat(time - face * 0.22f, 2.4f);
            bool active = s < 0.7f;
            float centre = Mathf.Lerp(-1.5f, 1.5f, s / 0.7f);
            for (int i = 0; i < perFace; i++)
            {
                float a = 0f;
                if (active)
                {
                    float d = Mathf.Abs(diagonal[face * perFace + i] - centre) / 0.45f;
                    float k = Mathf.Clamp01(1f - d);
                    a = k * k * (3f - 2f * k) * 0.8f;
                    any = true;
                }
                colors[face * perFace + i] = new Color(1f, 1f, 1f, a);
            }
        }
        if (any || wasActive) mesh.colors = colors; // nothing to redraw while every face is clear
        wasActive = any;
    }
}

/// <summary>Spins (and gently pulses) a pixel's dark-matter core.</summary>
public class PixelLookSpin : MonoBehaviour
{
    private Vector3 spin, baseScale;
    private float pulseAmount, pulseSpeed, phase;

    public void Setup(Vector3 degreesPerSecond, float pulse, float speed)
    {
        spin = degreesPerSecond;
        baseScale = transform.localScale;
        pulseAmount = pulse;
        pulseSpeed = speed;
        phase = UnityEngine.Random.value * 6.28f;
        transform.localRotation = UnityEngine.Random.rotation;
    }

    private void Update()
    {
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        transform.localScale = baseScale * (1f + pulseAmount * Mathf.Sin(Time.time * pulseSpeed + phase));
    }
}

/// <summary>
/// Added to old pixels that shatter (glass): when one hits the ground fast enough it breaks into flying shards and is gone.
/// </summary>
public class OldPixelShatter : MonoBehaviour
{
    private PixelClicker clicker;
    private float minSpeed, shardSpeed, shardLife, shardSize;
    private int shardCount;
    private string soundId;
    private bool done;
    private bool held;          // the player is carrying it (Pixel Grabbing): it can't break
    private bool gentle;        // let go softly: its next landing is a soft set-down, not a break
    private float gentleBonus;  // seconds added to its despawn timer on that soft landing

    /// <summary>The player picked it up: it can't shatter while carried.</summary>
    public void Grabbed()
    {
        held = true;
        gentle = false;
    }

    /// <summary>
    /// The player let go. If that was gentle (slow), its next landing is a soft set-down: it doesn't break, loses most of its
    /// speed, and its despawn timer is extended by 'lifetimeBonus' seconds.
    /// </summary>
    public void Released(bool gentleRelease, float lifetimeBonus)
    {
        held = false;
        gentle = gentleRelease;
        gentleBonus = lifetimeBonus;
    }

    public void Setup(PixelClicker owner, float minImpactSpeed, int shards, float speed, float life, float size, string sound)
    {
        clicker = owner;
        minSpeed = minImpactSpeed;
        shardCount = shards;
        shardSpeed = speed;
        shardLife = life;
        shardSize = size;
        soundId = sound;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (done || clicker == null || held) return;
        if (collision.collider.GetComponent<OldPixelShard>() != null) return;

        if (gentle)
        {
            // Set down gently: it settles (any surface, including other pixels) and lives longer.
            gentle = false;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity *= 0.2f;
#else
                rb.velocity *= 0.2f;
#endif
                rb.angularVelocity *= 0.3f;
            }
            OldPixelDespawn despawn = GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.AddLifetime(gentleBonus);
            return;
        }

        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (collision.relativeVelocity.magnitude < minSpeed) return;

        ContactPoint contact = collision.GetContact(0);
        if (Vector3.Dot(contact.normal, Vector3.up) < 0.4f) return; // only the ground, not a wall or the cube

        Shatter(contact.point, contact.normal);
    }

    private void Shatter(Vector3 point, Vector3 normal)
    {
        PixelStats.Count("old.shattered");
        done = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) clicker.ReleaseOldPixel(body, false);

        if (!string.IsNullOrEmpty(soundId)) PixelAudio.Play(soundId);
        SpawnShards(gameObject, normal, shardCount, shardSpeed, shardLife, shardSize);
        Destroy(gameObject);
    }

    /// <summary>
    /// Bursts flying slivers of a husk's own mesh and look out of it (they shrink away after 'life' seconds). The husk itself is left alone - the caller
    /// destroys it. Used by the glass shatter and by Husk Crush (PixelHuskCrush).
    /// </summary>
    public static void SpawnShards(GameObject source, Vector3 normal, int shardCount, float shardSpeed, float shardLife, float shardSize)
    {
        if (source == null || shardCount <= 0) return;
        Transform transform = source.transform;
        MeshFilter mf = source.GetComponent<MeshFilter>();
        if (mf == null) mf = source.GetComponentInChildren<MeshFilter>();   // (a look built from child parts: its first part's mesh and colour)
        MeshRenderer mr = mf != null ? mf.GetComponent<MeshRenderer>() : null;
        if (mf != null && mr != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            Vector3 scale = transform.lossyScale;
            float baseSize = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = new GameObject("GlassShard");
                shard.layer = source.layer;
                shard.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * baseSize * 0.35f;
                shard.transform.rotation = UnityEngine.Random.rotation;
                // Flat slivers of different sizes.
                shard.transform.localScale = new Vector3(
                    baseSize * shardSize * UnityEngine.Random.Range(0.6f, 1.2f),
                    baseSize * shardSize * UnityEngine.Random.Range(0.12f, 0.3f),
                    baseSize * shardSize * UnityEngine.Random.Range(0.5f, 1f));

                shard.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                MeshRenderer smr = shard.AddComponent<MeshRenderer>();
                smr.sharedMaterial = mr.sharedMaterial;
                smr.SetPropertyBlock(block);
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                shard.AddComponent<BoxCollider>();
                Rigidbody rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.05f;
                Vector3 push = normal * shardSpeed * UnityEngine.Random.Range(0.4f, 1f)
                             + UnityEngine.Random.onUnitSphere * shardSpeed * 0.7f;
                push.y = Mathf.Abs(push.y);
                rb.AddForce(push, ForceMode.VelocityChange);
                rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(5f, 15f), ForceMode.VelocityChange);

                shard.AddComponent<OldPixelShard>().Setup(shardLife * UnityEngine.Random.Range(0.7f, 1.2f));
            }
        }
    }
}

/// <summary>A flying glass shard: lives for a moment, then shrinks away.</summary>
public class OldPixelShard : MonoBehaviour
{
    private float life, age;
    private Vector3 startScale;
    private const float ShrinkSeconds = 0.35f;

    public void Setup(float lifeSeconds)
    {
        life = lifeSeconds;
        startScale = transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age < life) return;

        float k = 1f - (age - life) / ShrinkSeconds;
        if (k <= 0f) { Destroy(gameObject); return; }
        transform.localScale = startScale * k;
    }
}

/// <summary>Turns its object to face the camera every frame (the glow halo).</summary>
public class PixelFaceCamera : MonoBehaviour
{
    private Camera cam;

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}


/// <summary>
/// The Electric pixel's lightning: a mesh of jagged bolts along the 12 edges plus a few arcs through the inside, rebuilt a
/// dozen times a second so it crackles. Every bolt is two crossed ribbons (so it reads from any angle) in two passes: a wide
/// soft glow and a thin bright core. With many electric pixels around, the bolts get simpler to keep things light.
/// </summary>
public class PixelLookLightning : MonoBehaviour
{
    private Vector3 size = Vector3.one;
    private Color core = Color.white, glow = Color.cyan;
    private float jitter = 0.09f;
    private Mesh mesh;
    private float timer;
    private static int active; // how many are alive: more of them = simpler bolts

    private readonly System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
    private readonly System.Collections.Generic.List<Color> colors = new System.Collections.Generic.List<Color>();
    private readonly System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { active = 0; }

    public void Setup(Vector3 cubeSize, Color coreColor, Color glowColor, float jitterFraction)
    {
        size = cubeSize;
        core = coreColor;
        glow = glowColor;
        jitter = jitterFraction;
        mesh = new Mesh { name = "Lightning" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        Rebuild();
        timer = UnityEngine.Random.Range(0.02f, 0.08f);
    }

    private void OnEnable() { active++; }
    private void OnDisable() { active = Mathf.Max(0, active - 1); }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }

    private void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = active <= 6 ? UnityEngine.Random.Range(0.04f, 0.09f) : UnityEngine.Random.Range(0.1f, 0.18f);
        Rebuild();
    }

    private Vector3 Corner(int i) => new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);

    private void Rebuild()
    {
        if (mesh == null) return;
        verts.Clear(); colors.Clear(); tris.Clear();
        bool rich = active <= 6;
        int segs = rich ? 6 : 3;
        float unit = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float coreW = unit * 0.035f, glowW = unit * 0.11f;

        // The 12 edges: corner pairs that differ in exactly one bit.
        for (int a = 0; a < 8; a++)
            for (int bit = 1; bit <= 4; bit <<= 1)
            {
                int b = a ^ bit;
                if (b < a) continue;
                Bolt(Corner(a), Corner(b), segs, jitter, coreW, glowW, rich);
            }

        // A few arcs through the inside, from one point on the surface to another.
        int arcs = rich ? UnityEngine.Random.Range(3, 6) : UnityEngine.Random.Range(1, 3);
        for (int i = 0; i < arcs; i++)
            Bolt(RandomSurfacePoint(), RandomSurfacePoint(), segs + 2, jitter * 1.6f, coreW * 0.8f, glowW * 0.8f, rich);

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.bounds = new Bounds(Vector3.zero, size * 1.3f);
    }

    private Vector3 RandomSurfacePoint()
    {
        Vector3 p = new Vector3(UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f, UnityEngine.Random.value - 0.5f);
        int axis = UnityEngine.Random.Range(0, 3);
        p[axis] = UnityEngine.Random.value < 0.5f ? -0.5f : 0.5f;
        return p;
    }

    private void Bolt(Vector3 a, Vector3 b, int segments, float wander, float coreWidth, float glowWidth, bool withGlow)
    {
        Vector3 prev = a;
        for (int s = 1; s <= segments; s++)
        {
            float t = s / (float)segments;
            Vector3 p = Vector3.Lerp(a, b, t);
            if (s < segments)
            {
                float fade = Mathf.Sin(t * Mathf.PI);
                p += new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)) * (wander * fade);
            }
            float flicker = UnityEngine.Random.Range(0.55f, 1f);
            if (withGlow) Ribbon(prev, p, glowWidth, new Color(glow.r, glow.g, glow.b, 0.22f * flicker));
            Ribbon(prev, p, coreWidth, new Color(core.r, core.g, core.b, flicker));
            prev = p;
        }
    }

    /// <summary>Two crossed flat strips from 'a' to 'b' (unit-cube space, scaled by the cube's size here).</summary>
    private void Ribbon(Vector3 a, Vector3 b, float width, Color colour)
    {
        Vector3 pa = Vector3.Scale(a, size), pb = Vector3.Scale(b, size);
        Vector3 dir = pb - pa;
        if (dir.sqrMagnitude < 1e-8f) return;
        dir.Normalize();
        Vector3 side1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized * (width * 0.5f);
        Vector3 side2 = Vector3.Cross(dir, side1).normalized * (width * 0.5f);
        AddQuad(pa, pb, side1, colour);
        AddQuad(pa, pb, side2, colour);
    }

    private void AddQuad(Vector3 pa, Vector3 pb, Vector3 side, Color colour)
    {
        int i = verts.Count;
        verts.Add(pa - side); verts.Add(pa + side); verts.Add(pb + side); verts.Add(pb - side);
        for (int k = 0; k < 4; k++) colors.Add(colour);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }
}
