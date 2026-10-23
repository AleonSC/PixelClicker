// ---- System notes ----
// PixelFactoryKit.cs
//   Small shared helper for building machines out of primitives in code (conveyor belt, kiln): materials copied from the cube's material (PixelClicker.CreateVisualMaterial, emission off
//   unless asked), boxes / cylinders / balls, see-through ghost versions (alpha) with no colliders. The Wood Chipper still has its own private copy of this (its Kit class): move it onto
//   this helper when it is next touched. DestroyMaterials() frees the materials it made (a temporary build like the studio intro).
//   SHARED MATERIALS (2026-10-20): a kit made with shared = true takes its materials from ONE static cache (keyed by colour incl. ghost alpha, metal,
//   smoothness, emission), so pieces that are rebuilt often (belts while placing, highlights, corner joints, elevator troughs) stop making new materials
//   every time - they used to leak about 8 a frame while a belt was being placed. Only for builds that never change a material they got from the kit
//   (use a MaterialPropertyBlock for per-object colour); DestroyMaterials leaves shared ones alone. PixelOwnedMesh frees a code-made Mesh with its object.
//   Also Strut (a cylinder from a to b), Disc (a flat band round Y) and static PlainPart (a collider-less primitive in the game's material) - used by the
//   device models (each device's own BuildModel, e.g. PixelFanDevice / PixelLightningRod) and the potion models.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public sealed class PixelFactoryKit
{
    public PixelClicker clicker;
    public bool ghost;
    public float alpha = 0.35f;
    private readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    private readonly bool shared;
    private static readonly Dictionary<string, Material> sharedMats = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => sharedMats.Clear();   // (the materials themselves are not scene objects: they stay valid across a Restart)

    public PixelFactoryKit(PixelClicker owner, bool forGhost) { clicker = owner; ghost = forGhost; }

    /// <summary>'sharedMaterials' = take materials from the one static cache (see the notes at the top): for builds rebuilt often that never change their materials.</summary>
    public PixelFactoryKit(PixelClicker owner, bool forGhost, bool sharedMaterials) { clicker = owner; ghost = forGhost; shared = sharedMaterials; }

    public Material Mat(Color c, float metal = 0.2f, float smooth = 0.4f, Color? emission = null)
    {
        Color col = c;
        if (ghost) col.a = alpha;
        string key = col.ToString() + metal + smooth + (emission.HasValue ? emission.Value.ToString() : "") + (ghost ? "g" : "");
        Dictionary<string, Material> cache = shared ? sharedMats : mats;
        if (cache.TryGetValue(key, out Material cached) && cached != null) return cached;
        Material m = clicker != null ? clicker.CreateVisualMaterial(col, ghost) : null;
        if (m == null)
        {
            Shader sh = Shader.Find("Standard");
            m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            m.color = col;
        }
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_EmissionColor"))
        {
            if (emission.HasValue && !ghost) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            else { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }
        }
        cache[key] = m;
        return m;
    }

    public GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default, float metal = 0.2f, float smooth = 0.4f, bool collider = false, Color? emission = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Collider col = go.GetComponent<Collider>();
        if (col != null && (ghost || !collider)) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = Mat(c, metal, smooth, emission);
        r.shadowCastingMode = ghost ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        r.receiveShadows = !ghost;
        return go;
    }

    public GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Color c, Vector3 euler = default, bool collider = false, float metal = 0.2f, float smooth = 0.4f, Color? emission = null)
        => Prim(PrimitiveType.Cube, name, parent, pos, size, c, euler, metal, smooth, collider, emission);

    /// <summary>A cylinder of 'radius' and 'length' along the axis given by 'euler' (0 = along Y).</summary>
    public GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float length, Color c, Vector3 euler = default, float metal = 0.2f, float smooth = 0.4f, bool collider = false)
        => Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), c, euler, metal, smooth, collider);

    public GameObject Ball(string name, Transform parent, Vector3 pos, float radius, Color c, float metal = 0.2f, float smooth = 0.5f)
        => Prim(PrimitiveType.Sphere, name, parent, pos, Vector3.one * radius * 2f, c, default, metal, smooth, false);

    /// <summary>A thin cylinder from a to b (a strut, a wire).</summary>
    public GameObject Strut(string name, Transform parent, Vector3 a, Vector3 b, float radius, Color c, float metal = 0.7f, float smooth = 0.6f)
    {
        Vector3 dir = b - a;
        return Prim(PrimitiveType.Cylinder, name, parent, (a + b) * 0.5f, new Vector3(radius * 2f, dir.magnitude * 0.5f, radius * 2f), c,
                    Quaternion.FromToRotation(Vector3.up, dir).eulerAngles, metal, smooth);
    }

    /// <summary>A flat disc / band round the Y axis at height y (a cylinder 'radius' wide and 'thick' tall), optionally glowing.</summary>
    public GameObject Disc(string name, Transform parent, float y, float radius, float thick, Color c, float metal = 0.6f, float smooth = 0.6f, Color? glow = null)
        => Prim(PrimitiveType.Cylinder, name, parent, new Vector3(0f, y, 0f), new Vector3(radius * 2f, thick * 0.5f, radius * 2f), c, default, metal, smooth, false, glow);

    /// <summary>A plain primitive with no collider and the game's material tinted with 'color' (no kit needed: the potion models, the Vacuum's turbine).</summary>
    public static GameObject PlainPart(PixelClicker clicker, PrimitiveType type, string name, Transform parent, Color color, bool transparent)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        Renderer r = go.GetComponent<Renderer>();
        Material mat = clicker != null ? clicker.CreateVisualMaterial(color, transparent) : null;
        if (mat != null) r.sharedMaterial = mat;
        else r.material.color = color;
        return go;
    }

    /// <summary>Destroys every material this kit made for itself (call when what it built is destroyed for good, e.g. the studio intro's set). Shared materials are kept.</summary>
    public void DestroyMaterials()
    {
        foreach (Material m in mats.Values) if (m != null) Object.Destroy(m);
        mats.Clear();
    }
}

/// <summary>Destroys a Mesh made in code when the object showing it is destroyed (a destroyed GameObject does not free its meshes). Add it next to the MeshFilter.</summary>
public sealed class PixelOwnedMesh : MonoBehaviour
{
    public Mesh mesh;

    /// <summary>Puts 'mesh' on the object's MeshFilter and makes the object free it when it goes.</summary>
    public static void Own(GameObject go, Mesh mesh)
    {
        if (go == null || mesh == null) return;
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null) mf.sharedMesh = mesh;
        PixelOwnedMesh o = go.GetComponent<PixelOwnedMesh>();
        if (o == null) o = go.AddComponent<PixelOwnedMesh>();
        o.mesh = mesh;
    }

    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
