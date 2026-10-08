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

    [Header("Surface texture")]
    [Tooltip("Draw thin white streaks on a dark base (use a white Color above so the texture shows true). Used by Obsidian.")]
    public bool streakTexture = false;

    [Tooltip("Tough pixels (several clicks to collect): white cracks spread over the pixel with every hit, more with each click.")]
    public bool damageCracks = false;

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

    [Header("Face circles")]
    [Tooltip("Draw a flat circle on each of the 6 faces of the cube.")]
    public bool faceCircles = false;

    [Tooltip("Colour of the face circles.")]
    public Color faceCircleColor = Color.black;

    [Range(0.05f, 0.5f)]
    [Tooltip("Circle radius as a fraction of the cube's width (0.5 = touches the edges).")]
    public float faceCircleRadius = 0.3f;

    [Header("Wobble (jelly)")]
    [Tooltip("The cube squashes and stretches like jelly (the live pixel and its falling copies).")]
    public bool wobble = false;

    [Range(0f, 0.5f)]
    [Tooltip("How much it squashes and stretches (0.15 = 15%).")]
    public float wobbleAmount = 0.14f;

    [Min(0f)]
    [Tooltip("How fast it wobbles.")]
    public float wobbleSpeed = 3.2f;

    [Header("Falling (old pixels of this type)")]
    [Tooltip("Use the bounce / friction / gravity below for this type's old pixels instead of the global ones.")]
    public bool customPhysics = false;

    [Range(0f, 1.5f)]
    [Tooltip("Bounciness of the old pixels (1 = keeps all its speed).")]
    public float bounce = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Friction of the old pixels (low = slides and keeps travelling).")]
    public float friction = 0.05f;

    [Min(0f)]
    [Tooltip("Multiplies the normal gravity on these old pixels (0.25 = falls four times more slowly).")]
    public float gravityMultiplier = 0.25f;

    [Tooltip("Old pixels of this type pass through the invisible screen edges instead of bouncing off them.")]
    public bool ignoreViewBounds = false;

    [Tooltip("After a few bounces the old pixel starts to float away and leaves the screen.")]
    public bool floatAway = false;

    [Min(1)]
    [Tooltip("How many ground bounces before it floats away.")]
    public int floatAfterBounces = 2;

    [Min(0f)]
    [Tooltip("How strongly it drifts upwards once it floats away (fraction of gravity).")]
    public float floatLift = 0.15f;

    [Min(0f)]
    [Tooltip("Sideways speed it picks up when it starts to float away.")]
    public float floatDriftSpeed = 2.5f;

    [Header("Special")]
    [Tooltip("A swirling dark-matter core inside the cube (best with 'Force Translucent' and a dark, see-through colour).")]
    public bool darkMatter = false;

    [Tooltip("Old pixels of this type shatter into shards when they hit the ground (they are gone afterwards).")]
    public bool shatter = false;

    /// <summary>Does this look add objects to the cube (outline / core)?</summary>
    public bool HasExtras => outline || darkMatter || faceCircles;

    /// <summary>Does this look put its own texture on the pixel (streaks and/or damage)?</summary>
    public bool HasSurfaceTexture => streakTexture || damageCracks;
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

            // Red, green and blue keep their plain flat colours (no entry).

            // Vacuum: a dark purple see-through block with a black circle on every face.
            new PixelLook { type = PixelClicker.PixelType.Vacuum, useColor = true, color = new Color(0.2f, 0.05f, 0.35f, 0.55f),
                            forceTranslucent = true, smoothness = 0.95f, metallic = 0f, faceCircles = true },

            // Glass: very see-through, a faint edge so it can still be seen, shatters on the ground.
            new PixelLook { type = PixelClicker.PixelType.Glass, alpha = 0.4f, smoothness = 1f, metallic = 0f,
                            outline = true, outlineUsesTierColor = false, outlineColor = new Color(0.85f, 0.97f, 1f, 1f),
                            outlineThickness = 0.02f, outlineStrength = 0.5f, shatter = true },

            // Ghost: soft, matte, see-through (not shiny like glass), wobbling like jelly. Bounces slowly and floats off the screen.
            new PixelLook { type = PixelClicker.PixelType.Ghost, useColor = true, color = new Color(0.88f, 0.94f, 1f, 0.28f),
                            smoothness = 0f, metallic = 0f, emission = 0.7f,
                            wobble = true, customPhysics = true, ignoreViewBounds = true, floatAway = true },

            // Obsidian: sheer polished black metal with white streaks; cracks spread with every click.
            new PixelLook { type = PixelClicker.PixelType.Obsidian, useColor = true, color = Color.white,
                            metallic = 1f, smoothness = 0.95f, streakTexture = true, damageCracks = true },

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
    // Surface texture: black with white streaks, plus cracks that grow with damage
    // ------------------------------------------------------------------

    private static readonly Dictionary<int, Texture2D> surfaceTextures = new Dictionary<int, Texture2D>();

    /// <summary>
    /// The pixel's surface texture. 'level' 0 = undamaged; up to 'maxLevel' = about to break (more cracks each level).
    /// Drawn once per level and shared.
    /// </summary>
    public static Texture2D SurfaceTexture(bool streaks, int level, int maxLevel)
    {
        maxLevel = Mathf.Max(1, maxLevel);
        level = Mathf.Clamp(level, 0, maxLevel);
        int key = (streaks ? 1 : 0) + level * 2 + maxLevel * 1000;
        if (surfaceTextures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        const int size = 128;
        Color32[] px = new Color32[size * size];
        Color32 baseColour = new Color32(6, 6, 9, 255);
        for (int i = 0; i < px.Length; i++) px[i] = baseColour;

        if (streaks)
        {
            // Long, thin, slightly slanted white streaks of different strength (always the same ones).
            System.Random rnd = new System.Random(77);
            for (int i = 0; i < 9; i++)
            {
                float x0 = (float)rnd.NextDouble() * size, y0 = (float)rnd.NextDouble() * size;
                float angle = 0.45f + ((float)rnd.NextDouble() - 0.5f) * 0.5f;
                float length = size * (0.35f + (float)rnd.NextDouble() * 0.5f);
                float strength = 0.35f + (float)rnd.NextDouble() * 0.65f;
                DrawLine(px, size, x0, y0, x0 + Mathf.Cos(angle) * length, y0 + Mathf.Sin(angle) * length, 1, strength, 0.35f);
            }
        }

        if (level > 0)
        {
            // Cracks: the same jagged paths every time, drawn a little further and in more places with each level.
            int cracks = 2 + level * 2;
            int segments = 3 + level * 2;
            for (int c = 0; c < cracks; c++)
            {
                System.Random rnd = new System.Random(1000 + c * 31);
                float x = size * (0.25f + (float)rnd.NextDouble() * 0.5f), y = size * (0.25f + (float)rnd.NextDouble() * 0.5f);
                float angle = (float)rnd.NextDouble() * Mathf.PI * 2f;
                for (int s = 0; s < 14; s++)
                {
                    angle += ((float)rnd.NextDouble() - 0.5f) * 1.1f;
                    float step = 6f + (float)rnd.NextDouble() * 9f;
                    float nx = x + Mathf.Cos(angle) * step, ny = y + Mathf.Sin(angle) * step;
                    if (s < segments) DrawLine(px, size, x, y, nx, ny, level >= maxLevel ? 2 : 1, 1f, 0.97f);
                    x = nx; y = ny;
                }
            }
        }

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "PixelSurface" };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        surfaceTextures[key] = tex;
        return tex;
    }

    /// <summary>Draws a line into the texture, lightening what is underneath towards 'tone' (wraps around the edges).</summary>
    private static void DrawLine(Color32[] px, int size, float x0, float y0, float x1, float y1, int thickness, float strength, float tone)
    {
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
        byte target = (byte)Mathf.Clamp(Mathf.RoundToInt(tone * 255f), 0, 255);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            for (int dy = 0; dy < thickness; dy++)
            {
                for (int dx = 0; dx < thickness; dx++)
                {
                    int px_ = ((cx + dx) % size + size) % size, py_ = ((cy + dy) % size + size) % size;
                    Color32 old = px[py_ * size + px_];
                    byte r = (byte)Mathf.Lerp(old.r, target, strength);
                    byte g = (byte)Mathf.Lerp(old.g, target, strength);
                    byte b = (byte)Mathf.Lerp(old.b, Mathf.Min(255, target + 6), strength);
                    px[py_ * size + px_] = new Color32(r, g, b, 255);
                }
            }
        }
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

        if (look.faceCircles)
        {
            GameObject circles = new GameObject("Face Circles", typeof(MeshFilter), typeof(MeshRenderer));
            circles.transform.SetParent(root.transform, false);
            circles.transform.localPosition = centre;
            circles.layer = root.layer;
            circles.GetComponent<MeshFilter>().sharedMesh = CircleMesh(size, look.faceCircleRadius, look.faceCircleColor);
            MeshRenderer cr = circles.GetComponent<MeshRenderer>();
            cr.sharedMaterial = NeonMaterial();
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;
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

    private static readonly Dictionary<int, Mesh> circleMeshes = new Dictionary<int, Mesh>();

    /// <summary>Six flat discs, one just outside each face of a box of this size.</summary>
    private static Mesh CircleMesh(Vector3 size, float radiusFraction, Color colour)
    {
        int key = unchecked(size.GetHashCode() * 31 + radiusFraction.GetHashCode() * 17 + colour.GetHashCode() * 13);
        if (circleMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

        const int Segments = 32;
        float gap = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.004f; // just off the face so it never z-fights
        List<Vector3> v = new List<Vector3>();
        List<Color> col = new List<Color>();
        List<int> tri = new List<int>();

        for (int axis = 0; axis < 3; axis++)
        {
            int a = (axis + 1) % 3, b = (axis + 2) % 3;
            float radius = Mathf.Min(size[a], size[b]) * radiusFraction;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 centre = Vector3.zero;
                centre[axis] = sign * (size[axis] * 0.5f + gap);
                int start = v.Count;
                v.Add(centre); col.Add(colour);
                for (int i = 0; i < Segments; i++)
                {
                    float ang = i * Mathf.PI * 2f / Segments;
                    Vector3 p = centre;
                    p[a] += Mathf.Cos(ang) * radius;
                    p[b] += Mathf.Sin(ang) * radius;
                    v.Add(p); col.Add(colour);
                }
                for (int i = 0; i < Segments; i++)
                {
                    int p1 = start + 1 + i, p2 = start + 1 + (i + 1) % Segments;
                    tri.Add(start); tri.Add(p1); tri.Add(p2);   // both windings, so it shows from either side
                    tri.Add(start); tri.Add(p2); tri.Add(p1);
                }
            }
        }

        Mesh mesh = new Mesh { name = "FaceCircles" };
        mesh.SetVertices(v);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        circleMeshes[key] = mesh;
        return mesh;
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
        ScaledGravity gravity = GetComponent<ScaledGravity>();
        if (gravity != null) gravity.scale = -lift; // gently upwards from now on
        foreach (Collider c in GetComponents<Collider>()) c.enabled = false; // nothing stops it any more

        Camera cam = Camera.main;
        Vector3 side = cam != null ? cam.transform.right : Vector3.right;
        float sign = cam != null && cam.WorldToViewportPoint(transform.position).x < 0.5f ? -1f : 1f;
        if (UnityEngine.Random.value < 0.25f) sign = -sign;
        if (body != null) body.AddForce(side * (sign * drift) + Vector3.up * (drift * 0.4f), ForceMode.VelocityChange);
    }

    private void Update()
    {
        if (!floating) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(transform.position);
        if (v.z < 0f || v.x < -0.4f || v.x > 1.4f || v.y > 1.4f || v.y < -0.4f)
        {
            if (clicker != null && body != null) clicker.ReleaseOldPixel(body, false);
            Destroy(gameObject);
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
            if (rb != null)
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
