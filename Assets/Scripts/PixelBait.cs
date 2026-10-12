using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bait pellets (Fishing). Collecting a Bait block drops a handful of small pellets on the floor round the cube; while they lie there Fish blocks show up
/// more often (<see cref="FishMultiplier"/>, read by the spawn weights). Pellets rot away after a while, and a Jellyfish that goes off zaps every pellet
/// out of existence - so leave the jellyfish alone. Numbers are Inspector fields on PixelClicker (Fishing group).
/// </summary>
public static class PixelBait
{
    private static readonly List<PixelBaitPellet> pellets = new List<PixelBaitPellet>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { pellets.Clear(); }

    internal static void Register(PixelBaitPellet p) { pellets.Add(p); }
    internal static void Unregister(PixelBaitPellet p) { pellets.Remove(p); }

    /// <summary>How many pellets are lying around.</summary>
    public static int Count => pellets.Count;

    private static float perPellet = 0.35f;

    /// <summary>The factor on a Fish block's spawn weight: 1 with no bait, more with every pellet.</summary>
    public static float FishMultiplier => pellets.Count == 0 ? 1f : 1f + pellets.Count * perPellet;

    /// <summary>Scatters pellets round 'from' (the cube). Returns how many were dropped.</summary>
    public static int Drop(PixelClicker clicker, Vector3 from)
    {
        if (clicker == null || clicker.DisableBait) return 0;
        perPellet = clicker.BaitPerPellet;
        int count = clicker.BaitPellets, max = clicker.BaitMaxPellets;
        float size = clicker.PixelBaseSize;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        int dropped = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 land = default;
            bool ok = false;
            for (int attempt = 0; attempt < 10 && !ok; attempt++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.3f, 4.2f) * size;
                Vector3 probe = from + new Vector3(r.x, 0f, r.y);
                if (!FindGround(clicker, probe, size, out land)) continue;
                if (cam != null)
                {
                    Vector3 vp = cam.WorldToViewportPoint(land);
                    if (vp.z <= 0f || vp.x < 0.06f || vp.x > 0.94f || vp.y < 0.12f || vp.y > 0.88f) continue;
                }
                ok = true;
            }
            if (!ok) continue;
            while (pellets.Count >= max && pellets.Count > 0) pellets[0].Remove(false);   // out with the oldest

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bait Pellet";
            Object.Destroy(go.GetComponent<Collider>());
            Material m = clicker.CreateVisualMaterial(new Color(0.82f, 0.55f, 0.26f, 1f), false);
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PixelBaitPellet pellet = go.AddComponent<PixelBaitPellet>();
            pellet.Launch(from, land + Vector3.up * size * 0.07f, size * 0.14f, clicker.BaitLifetime);
            dropped++;
        }
        if (dropped > 0) PixelAudio.Play("farm_hit");
        return dropped;
    }

    /// <summary>Every pellet is gone at once. 'zap' = a Jellyfish went off: each one cracks with electricity first.</summary>
    public static void DestroyAll(bool zap)
    {
        int fx = 0;
        foreach (PixelBaitPellet p in new List<PixelBaitPellet>(pellets))
        {
            if (p == null) continue;
            if (zap && fx++ < 10) FractureFx.Play(p.transform.position, p.transform.localScale.x * 3f, 3, 1.4f, 0.25f, new Color(1f, 0.9f, 1f, 1f), new Color(0.95f, 0.4f, 1f, 1f));
            p.Remove(zap);
        }
        pellets.Clear();
    }

    internal static bool FindGround(PixelClicker clicker, Vector3 from, float size, out Vector3 point)
    {
        point = default;
        RaycastHit[] hits = Physics.RaycastAll(from + Vector3.up * size * 4f, Vector3.down, size * 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            if (h.collider == null || h.normal.y < 0.7f) continue;
            if (clicker.PixelTransform != null && h.collider.transform.IsChildOf(clicker.PixelTransform)) continue;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) continue;
            if (h.distance < best) { best = h.distance; point = h.point; found = true; }
        }
        return found;
    }
}

/// <summary>One bait pellet: hops out to its spot, lies there for a while, then shrinks away (or is zapped).</summary>
public class PixelBaitPellet : MonoBehaviour
{
    private Vector3 start, end;
    private float t, flight, life, size, fadeLeft = -1f;
    private bool landed;

    private void OnEnable() { PixelBait.Register(this); }
    private void OnDisable() { PixelBait.Unregister(this); }

    public void Launch(Vector3 from, Vector3 to, float pelletSize, float lifetime)
    {
        start = from; end = to; size = pelletSize; life = lifetime;
        flight = Random.Range(0.45f, 0.7f);
        transform.position = from;
        transform.localScale = Vector3.one * size;
    }

    /// <summary>Takes the pellet away: a quick shrink (zapped ones flicker).</summary>
    public void Remove(bool zapped)
    {
        PixelBait.Unregister(this);
        life = -1f;
        fadeLeft = zapped ? 0.25f : 0.4f;
        landed = true;
    }

    private void Update()
    {
        if (fadeLeft >= 0f)
        {
            fadeLeft -= Time.deltaTime;
            if (fadeLeft <= 0f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * size * Mathf.Clamp01(fadeLeft * 3f) * (Random.value > 0.3f ? 1f : 0.4f);
            return;
        }
        if (!landed)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flight);
            Vector3 p = Vector3.Lerp(start, end, k);
            p.y += Mathf.Sin(k * Mathf.PI) * size * 5f;
            transform.position = p;
            if (k >= 1f) landed = true;
            return;
        }
        life -= Time.deltaTime;
        if (life <= 0f) { Remove(false); return; }
        if (life < 5f) transform.localScale = Vector3.one * size * Mathf.Clamp01(life / 5f) * (life < 2.5f && Mathf.Sin(life * 14f) > 0.6f ? 0.5f : 1f);
    }
}
