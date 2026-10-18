// ---- System notes ----
// PixelRemoveTint.cs
//   The "being cancelled" look: while the player holds the right button to remove something, it turns RED (colour towards red, a red glow in its emission, a red point light) as the hold ring fills,
//   exactly like a placed device (PixelPlacedDevice.SetRemoveGlow). PixelRemoveTint.Set(gameObject, 0..1) does it for ANY object (added to it on demand; 0 puts everything back and removes itself):
//   used by PixelFeatureTester.UpdateDelete (conveyor belts, kilns, elevators, solar panels, the tester's chipper). Placed devices keep their own SetRemoveGlow (it also dulls them when disarmed).
//   The running potion line (PixelUI.UpdateActiveHud) uses Shake() / RedText() from here: the text reddens and trembles very slightly as the hold fills.
// ----------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PixelRemoveTint : MonoBehaviour
{
    private static readonly Color Red = new Color(1f, 0.1f, 0.06f, 1f);
    private static readonly int ColorId = Shader.PropertyToID("_Color"), BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionId = Shader.PropertyToID("_EmissionColor");

    private struct Entry { public Renderer renderer; public Color baseColor, baseEmission; public bool hasColor, hasBaseColorProp, hasEmission; }

    private List<Entry> entries;
    private MaterialPropertyBlock block;
    private Light glowLight;
    private float amount;

    /// <summary>Turns the object red by 'k' (0 = normal again, 1 = fully red). Safe to call every frame with the same value.</summary>
    public static void Set(GameObject target, float k)
    {
        if (target == null) return;
        PixelRemoveTint t = target.GetComponent<PixelRemoveTint>();
        k = Mathf.Clamp01(k);
        if (t == null)
        {
            if (k <= 0f) return;
            t = target.AddComponent<PixelRemoveTint>();
        }
        t.Apply(k);
    }

    private void Apply(float k)
    {
        if (Mathf.Approximately(k, amount) && k > 0f) return;
        amount = k;
        if (entries == null) Collect();
        foreach (Entry e in entries)
        {
            if (e.renderer == null) continue;
            if (k <= 0f) { e.renderer.SetPropertyBlock(null); continue; }
            e.renderer.GetPropertyBlock(block);
            if (e.hasColor)
            {
                Color c = Color.Lerp(e.baseColor, new Color(Red.r, Red.g * 0.5f, Red.b * 0.5f, e.baseColor.a), k * 0.85f);
                block.SetColor(e.hasBaseColorProp ? BaseColorId : ColorId, c);
                if (e.hasBaseColorProp) block.SetColor(ColorId, c);
            }
            block.SetColor(EmissionId, e.baseEmission + Red * (k * k * 2.5f));
            e.renderer.SetPropertyBlock(block);
        }
        if (k > 0f)
        {
            if (glowLight == null)
            {
                GameObject lg = new GameObject("Remove Glow");
                lg.transform.SetParent(transform, false);
                Bounds b = Bounds();
                lg.transform.position = b.center + Vector3.up * b.extents.y;
                glowLight = lg.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.color = Red;
                glowLight.range = Mathf.Max(3f, b.size.magnitude * 1.5f);
                glowLight.shadows = LightShadows.None;
            }
            glowLight.intensity = k * k * 4f;
        }
        else
        {
            if (glowLight != null) Destroy(glowLight.gameObject);
            Destroy(this);   // (nothing left to undo)
        }
    }

    private Bounds Bounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponent<TMP_Text>() != null) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void Collect()
    {
        entries = new List<Entry>();
        block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TMP_Text>() != null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Material m = r.sharedMaterial;
            if (m == null) continue;
            Entry e = new Entry { renderer = r, baseColor = Color.white, baseEmission = Color.black };
            if (m.HasProperty(BaseColorId)) { e.baseColor = m.GetColor(BaseColorId); e.hasColor = true; e.hasBaseColorProp = true; }
            else if (m.HasProperty(ColorId)) { e.baseColor = m.GetColor(ColorId); e.hasColor = true; }
            if (m.HasProperty(EmissionId) && m.IsKeywordEnabled("_EMISSION")) { e.baseEmission = m.GetColor(EmissionId); e.hasEmission = true; }
            entries.Add(e);
        }
    }

    private void OnDestroy()
    {
        if (entries != null) foreach (Entry e in entries) if (e.renderer != null) e.renderer.SetPropertyBlock(null);
        if (glowLight != null) Destroy(glowLight.gameObject);
    }

    // ---- text (the potion timer line) ----

    /// <summary>A very slight tremble: the transform rotates / scales a hair at random, more as k (0..1) grows. k = 0 puts it back exactly.</summary>
    public static void Shake(Transform t, float k)
    {
        if (t == null) return;
        if (k <= 0.001f) { t.localRotation = Quaternion.identity; t.localScale = Vector3.one; return; }
        t.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1f, 1f) * (0.3f + 1.2f * k));
        float s = 1f + Random.Range(-0.5f, 1f) * 0.025f * k;
        t.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>The colour of a line being cancelled: its own colour moving towards red as k grows.</summary>
    public static Color RedText(Color own, float k) => Color.Lerp(own, new Color(1f, 0.18f, 0.12f, own.a), Mathf.Clamp01(k) * 0.9f);
}
