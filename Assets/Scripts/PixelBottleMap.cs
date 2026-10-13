// ---- System notes ----
// PixelBottleMap.cs
//   The Message in a Bottle block (PixelType.Bottle 73, Fishing): PixelLook.bottleMap draws a clear, pale green GLASS CUBE (look: forceTranslucent, bevelled glass rim) with a small
//   3D treasure map floating inside - a parchment sheet, two rolled ends, a red ribbon and a red X (PixelBottleMap.Attach, called from PixelLooks.AddExtras so the cube, the old
//   copies and the display models all get it). PixelBottleMapSpin turns it slowly and bobs it. looksVersion 51 replaced the saved Bottle look (the old painted-bottle texture,
//   PixelBlockTextures.BottleGlass, is no longer used by it).
// ---------------------------------------------------
using UnityEngine;

public static class PixelBottleMap
{
    private static Material parchment, roll, ribbon, ink;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { parchment = roll = ribbon = ink = null; }

    private static Material Make(Material source, Color color, float smoothness)
    {
        Material m = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    private static GameObject Piece(Transform parent, PrimitiveType shape, string name, Material material, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        GameObject g = GameObject.CreatePrimitive(shape);
        g.name = name;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Object.Destroy(c);
        g.transform.SetParent(parent, false);
        g.layer = parent.gameObject.layer;
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return g;
    }

    /// <summary>Puts the floating treasure map inside a cube whose mesh bounds are 'size' around 'centre'.</summary>
    public static void Attach(Transform parent, Vector3 centre, Vector3 size, Material baseMaterial)
    {
        if (parchment == null) parchment = Make(baseMaterial, new Color(0.93f, 0.84f, 0.6f, 1f), 0.15f);
        if (roll == null) roll = Make(baseMaterial, new Color(0.78f, 0.66f, 0.4f, 1f), 0.2f);
        if (ribbon == null) ribbon = Make(baseMaterial, new Color(0.8f, 0.12f, 0.12f, 1f), 0.4f);
        if (ink == null) ink = Make(baseMaterial, new Color(0.7f, 0.08f, 0.08f, 1f), 0.1f);

        float s = Mathf.Max(size.x, size.y, size.z);
        GameObject root = new GameObject("Bottle Map");
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        root.transform.localPosition = centre;
        root.AddComponent<PixelBottleMapSpin>().Setup(s);

        // The map lies flat (XZ plane) inside its own pivot; the pivot is tilted by the spin component.
        Transform m = new GameObject("Map").transform;
        m.SetParent(root.transform, false);
        m.gameObject.layer = root.layer;
        m.localRotation = Quaternion.Euler(-24f, 0f, 0f);

        Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
        Piece(m, PrimitiveType.Cube, "Sheet", parchment, Vector3.zero, new Vector3(0.46f, 0.018f, 0.34f) * s, Quaternion.identity);
        Piece(m, PrimitiveType.Cylinder, "Roll L", roll, new Vector3(-0.25f, 0.012f, 0f) * s, new Vector3(0.085f, 0.185f, 0.085f) * s, alongZ);
        Piece(m, PrimitiveType.Cylinder, "Roll R", roll, new Vector3(0.25f, 0.012f, 0f) * s, new Vector3(0.085f, 0.185f, 0.085f) * s, alongZ);
        Piece(m, PrimitiveType.Cylinder, "Ribbon", ribbon, new Vector3(-0.25f, 0.012f, 0f) * s, new Vector3(0.105f, 0.03f, 0.105f) * s, alongZ);
        // The red X.
        Piece(m, PrimitiveType.Cube, "X 1", ink, new Vector3(0.07f, 0.014f, 0.02f) * s, new Vector3(0.13f, 0.008f, 0.022f) * s, Quaternion.Euler(0f, 40f, 0f));
        Piece(m, PrimitiveType.Cube, "X 2", ink, new Vector3(0.07f, 0.014f, 0.02f) * s, new Vector3(0.13f, 0.008f, 0.022f) * s, Quaternion.Euler(0f, -40f, 0f));
        // A dotted trail to it.
        for (int i = 0; i < 4; i++)
            Piece(m, PrimitiveType.Cube, "Dot " + i, ink, new Vector3(-0.1f + i * 0.035f, 0.014f, -0.06f + i * 0.02f) * s, new Vector3(0.016f, 0.008f, 0.016f) * s, Quaternion.identity);
    }
}

/// <summary>Turns the map inside the bottle slowly and bobs it up and down.</summary>
public class PixelBottleMapSpin : MonoBehaviour
{
    private float scale = 1f, phase;
    private Vector3 basePos;

    public void Setup(float s) { scale = s; phase = Random.value * 6.28f; basePos = transform.localPosition; }

    private void Update()
    {
        transform.localRotation = Quaternion.Euler(0f, Time.time * 38f + phase * 20f, 0f);
        transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(Time.time * 1.6f + phase) * 0.03f * scale, 0f);
    }
}
