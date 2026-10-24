// ---- System notes ----
// PixelSwarmKit.cs
//   ONE place for the building bits of the code-built critter swarms (the Pest's bugs PixelPestSwarm, the Termite Hive's termites
//   PixelTermiteSwarm; 2026-10-24: was a copied block in each):
//   - GroundY(clicker, at): the floor under a point (the lowest surface without a rigid body - the cube and husks are higher).
//   - Material(clicker, colour): a plain lit material (the clicker's visual material, else a sprite / Standard one).
//   - Part(parent, name, localPos, localScale, unit, mat, euler): one colliderless, shadowless cube part, sized in 'unit's.
// ---------------------------------------------------
using UnityEngine;

public static class PixelSwarmKit
{
    /// <summary>The floor under a point: the lowest surface that is not a rigid body (the cube and husks are higher).</summary>
    public static float GroundY(PixelClicker clicker, Vector3 at)
    {
        float size = Mathf.Max(0.01f, clicker.PixelBaseSize);
        float best = at.y - size * 1.5f;
        float lowest = float.MaxValue;
        RaycastHit[] hits = Physics.RaycastAll(at + Vector3.up * size, Vector3.down, size * 60f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
            if (h.rigidbody == null && h.point.y < lowest) lowest = h.point.y;
        return lowest < float.MaxValue ? lowest : best;
    }

    /// <summary>A plain material of one colour.</summary>
    public static Material Material(PixelClicker clicker, Color c)
    {
        Material m = clicker != null ? clicker.CreateVisualMaterial(c, false) : null;
        if (m == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            m = sh != null ? new Material(sh) : new Material(Shader.Find("Standard"));
            m.color = c;
        }
        return m;
    }

    /// <summary>One cube part (no collider, no shadow); position and scale are in 'unit's.</summary>
    public static Transform Part(Transform parent, string name, Vector3 localPos, Vector3 localScale, float unit, Material mat, Vector3 euler = default)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos * unit;
        g.transform.localScale = localScale * unit;
        g.transform.localRotation = Quaternion.Euler(euler);
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g.transform;
    }
}
