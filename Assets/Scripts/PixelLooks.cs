using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extra visual style for one pixel type, applied on top of the tier's own settings (colour, glow, translucent...).
/// The tier's colour is still what the UI shows; this only changes how the 3D pixel looks. Edit the list on PixelClicker.
/// </summary>
[Serializable]
public class PixelLook
{
    [Tooltip("The pixel type this style belongs to.")]
    public PixelClicker.PixelType type = PixelClicker.PixelType.White;

    [Header("Colour and surface")]
    [Tooltip("Draw the pixel in 'Color' instead of the tier's colour (the UI keeps using the tier colour).")]
    public bool useColor = false;

    [Tooltip("Colour of the 3D pixel (its alpha matters for see-through pixels).")]
    public Color color = Color.white;

    [Range(0f, 1f)]
    [Tooltip("Multiplies the colour's alpha. Lower = more see-through (only for translucent pixels).")]
    public float alpha = 1f;

    [Tooltip("Draw this pixel see-through even if its tier isn't marked Translucent.")]
    public bool forceTranslucent = false;

    [Range(-1f, 1f)]
    [Tooltip("Metallic surface (0-1). -1 = leave the material's own value.")]
    public float metallic = -1f;

    [Range(-1f, 1f)]
    [Tooltip("Glossiness (0-1). -1 = leave the material's own value.")]
    public float smoothness = -1f;

    [Min(0f)]
    [Tooltip("Plain self-lighting (emission) in the pixel's colour, even if the tier doesn't glow. 0 = none.")]
    public float emission = 0f;

    [Min(0f)]
    [Tooltip("Multiplies the glow of a glowing tier (1 = unchanged, 0.3 = much dimmer).")]
    public float glowScale = 1f;

    [Header("Outline")]
    [Tooltip("Draw glowing neon lines along the 12 edges of the cube.")]
    public bool outline = false;

    [Tooltip("Use the pixel's tier colour for the outline (otherwise 'Outline Color').")]
    public bool outlineUsesTierColor = true;

    [Tooltip("Outline colour when 'Outline Uses Tier Colour' is off.")]
    public Color outlineColor = Color.white;

    [Range(0.005f, 0.3f)]
    [Tooltip("Line thickness as a fraction of the cube's width.")]
    public float outlineThickness = 0.045f;

    [Range(0f, 1f)]
    [Tooltip("Overall strength of the outline (0 = invisible, 1 = full).")]
    public float outlineStrength = 1f;

    [Header("Special")]
    [Tooltip("A swirling dark-matter core inside the cube (best with 'Force Translucent' and a dark, see-through colour).")]
    public bool darkMatter = false;

    [Tooltip("Old pixels of this type shatter into shards when they hit the ground (they are gone afterwards).")]
    public bool shatter = false;

    /// <summary>Does this look add objects to the cube (outline / core)?</summary>
    public bool HasExtras => outline || darkMatter;
}

/// <summary>Helpers that build the runtime-drawn parts of a look: neon edges, the dark-matter core, shatter shards.</summary>
public static class PixelLooks
{
    /// <summary>The built-in styles (used when the list on PixelClicker is new).</summary>
    public static PixelLook[] CreateDefaults()
    {
        return new[]
        {
            // White, gray and black keep their plain tier look (no entry).

            // RGB: neon outline in their own colour.
            new PixelLook { type = PixelClicker.PixelType.Red, outline = true },
            new PixelLook { type = PixelClicker.PixelType.Green, outline = true },
            new PixelLook { type = PixelClicker.PixelType.Blue, outline = true },

            // Glass: very see-through, a faint edge so it can still be seen, shatters on the ground.
            new PixelLook { type = PixelClicker.PixelType.Glass, alpha = 0.4f, smoothness = 1f, metallic = 0f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.85f, 0.97f, 1f, 1f),
                            outlineThickness = 0.02f, outlineStrength = 0.5f, shatter = true },

            // Singularity: a box of dark matter.
            new PixelLook { type = PixelClicker.PixelType.Singularity, useColor = true, color = new Color(0.07f, 0.02f, 0.14f, 0.5f),
                            forceTranslucent = true, smoothness = 0.95f, metallic = 0f, glowScale = 0.3f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.65f, 0.25f, 1f, 1f),
                            outlineThickness = 0.04f, darkMatter = true },
        };
    }

    public static PixelLook Find(PixelLook[] looks, PixelClicker.PixelType type)
    {
        if (looks == null) return null;
        for (int i = 0; i < looks.Length; i++)
            if (looks[i] != null && looks[i].type == type) return looks[i];
        return null;
    }

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

        if (look.outline)
        {
            Color c = look.outlineUsesTierColor ? new Color(tierColor.r, tierColor.g, tierColor.b, 1f) : look.outlineColor;
            GameObject edges = new GameObject("Neon Edges", typeof(MeshFilter), typeof(MeshRenderer));
            edges.transform.SetParent(root.transform, false);
            edges.transform.localPosition = centre;
            edges.layer = root.layer;
            edges.GetComponent<MeshFilter>().sharedMesh = EdgeMesh(size, look.outlineThickness, c, look.outlineStrength);
            MeshRenderer mr = edges.GetComponent<MeshRenderer>();
            mr.sharedMaterial = NeonMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
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

    private static Material NeonMaterial()
    {
        if (neonMaterial != null) return neonMaterial;
        // Sprites/Default is unlit and uses vertex colours in every render pipeline.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        neonMaterial = new Material(shader) { name = "NeonEdges" };
        return neonMaterial;
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

    /// <summary>A swirling purple nebula with a few stars (drawn once, shared by every dark-matter core).</summary>
    private static Texture2D BuildDarkMatterTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "DarkMatter" };
        Color32[] px = new Color32[size * size];
        Color deep = new Color(0.01f, 0f, 0.03f, 1f), violet = new Color(0.42f, 0.06f, 0.85f, 1f), glow = new Color(0.85f, 0.45f, 1f, 1f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Atan2(v, u) + r * 9f; // twist: the further out, the more it swirls
                float sx = 0.5f + Mathf.Cos(a) * r, sy = 0.5f + Mathf.Sin(a) * r;
                float n = Mathf.PerlinNoise(sx * 4f + 20f, sy * 4f + 20f) * 0.65f + Mathf.PerlinNoise(sx * 10f + 5f, sy * 10f + 5f) * 0.35f;
                n = Mathf.SmoothStep(0.3f, 0.85f, n);
                Color c = Color.Lerp(deep, violet, n);
                c = Color.Lerp(c, glow, Mathf.Clamp01((n - 0.8f) * 4f) * 0.5f);

                // The odd bright speck, like a star.
                float h = Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
                if (h > 0.994f) c = Color.Lerp(c, Color.white, 0.9f);

                px[y * size + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    // ------------------------------------------------------------------
    // Neon edge mesh: 12 thin boxes for a bright core plus 12 wider, fainter ones for the halo
    // ------------------------------------------------------------------

    private static Mesh EdgeMesh(Vector3 size, float thicknessFraction, Color colour, float strength)
    {
        int key = unchecked(size.GetHashCode() * 31 + thicknessFraction.GetHashCode() * 17 + colour.GetHashCode() * 13 + strength.GetHashCode());
        if (edgeMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        float t = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * thicknessFraction;
        Color halo = new Color(colour.r, colour.g, colour.b, 0.3f * strength);
        Color core = Color.Lerp(colour, Color.white, 0.5f);
        core.a = strength;

        List<Vector3> v = new List<Vector3>(400);
        List<Color> col = new List<Color>(400);
        List<int> tri = new List<int>(600);
        AddEdgeBoxes(v, col, tri, size, t * 2.6f, halo);
        AddEdgeBoxes(v, col, tri, size, t, core);

        Mesh mesh = new Mesh { name = "NeonEdges" };
        mesh.SetVertices(v);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        edgeMeshes[key] = mesh;
        return mesh;
    }

    private static void AddEdgeBoxes(List<Vector3> v, List<Color> col, List<int> tri, Vector3 size, float t, Color colour)
    {
        Vector3 h = size * 0.5f;
        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    Vector3 centre = Vector3.zero, ext = Vector3.zero;
                    centre[a] = i * h[a];
                    centre[b] = j * h[b];
                    ext[axis] = h[axis] + t * 0.5f; // a little longer so the corners close up
                    ext[a] = t * 0.5f;
                    ext[b] = t * 0.5f;
                    AddBox(v, col, tri, centre, ext, colour);
                }
            }
        }
    }

    private static readonly int[][] BoxQuads =
    {
        new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 4, 6, 2 },
        new[] { 1, 3, 7, 5 }, new[] { 0, 1, 5, 4 }, new[] { 2, 6, 7, 3 },
    };

    private static void AddBox(List<Vector3> v, List<Color> col, List<int> tri, Vector3 centre, Vector3 ext, Color colour)
    {
        int start = v.Count;
        for (int i = 0; i < 8; i++)
        {
            v.Add(centre + new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z));
            col.Add(colour);
        }
        foreach (int[] q in BoxQuads)
        {
            tri.Add(start + q[0]); tri.Add(start + q[1]); tri.Add(start + q[2]);
            tri.Add(start + q[0]); tri.Add(start + q[2]); tri.Add(start + q[3]);
        }
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
        if (done || clicker == null) return;
        if (collision.collider.GetComponentInParent<OldPixelInfo>() != null) return; // another old pixel
        if (collision.collider.GetComponent<OldPixelShard>() != null) return;
        if (collision.relativeVelocity.magnitude < minSpeed) return;

        ContactPoint contact = collision.GetContact(0);
        if (Vector3.Dot(contact.normal, Vector3.up) < 0.4f) return; // only the ground, not a wall or the cube

        Shatter(contact.point, contact.normal);
    }

    private void Shatter(Vector3 point, Vector3 normal)
    {
        done = true;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) clicker.ReleaseOldPixel(body, false);

        if (!string.IsNullOrEmpty(soundId)) PixelAudio.Play(soundId);

        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mf != null && mr != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            mr.GetPropertyBlock(block);
            Vector3 scale = transform.lossyScale;
            float baseSize = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

            for (int i = 0; i < shardCount; i++)
            {
                GameObject shard = new GameObject("GlassShard");
                shard.layer = gameObject.layer;
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
        Destroy(gameObject);
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
