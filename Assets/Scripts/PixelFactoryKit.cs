// ---- System notes ----
// PixelFactoryKit.cs
//   Small shared helper for building machines out of primitives in code (conveyor belt, kiln): materials copied from the cube's material (PixelClicker.CreateVisualMaterial, emission off
//   unless asked), boxes / cylinders / balls, see-through ghost versions (alpha) with no colliders. The Wood Chipper still has its own private copy of this (its Kit class): move it onto
//   this helper when it is next touched. DestroyMaterials() frees the materials it made (a temporary build like the studio intro).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public sealed class PixelFactoryKit
{
    public PixelClicker clicker;
    public bool ghost;
    public float alpha = 0.35f;
    private readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    public PixelFactoryKit(PixelClicker owner, bool forGhost) { clicker = owner; ghost = forGhost; }

    public Material Mat(Color c, float metal = 0.2f, float smooth = 0.4f, Color? emission = null)
    {
        Color col = c;
        if (ghost) col.a = alpha;
        string key = col.ToString() + metal + smooth + (emission.HasValue ? emission.Value.ToString() : "");
        if (mats.TryGetValue(key, out Material cached) && cached != null) return cached;
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
        mats[key] = m;
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

    /// <summary>Destroys every material this kit made (call when what it built is destroyed for good, e.g. the studio intro's set).</summary>
    public void DestroyMaterials()
    {
        foreach (Material m in mats.Values) if (m != null) Object.Destroy(m);
        mats.Clear();
    }
}
