// ---- System notes ----
// PixelSapField.cs
//   SAP FIELDS (a system component, PixelSystems World group): every Sap HUSK lying on the field has a soft transparent YELLOW CIRCLE round it (radiusHusks,
//   4 husk widths) and keeps the other WOODCUTTING husks inside it fresh:
//     - a woodcutting husk (any block of the Woodcutting pack except Sap itself) that comes inside a sap circle gets +huskBonusSeconds (5 s) of lifetime,
//       ONCE ever (a husk that leaves and comes back, or sits in two circles, still only gets it once);
//     - the sap itself gets +sapBonusPerHusk (1 s) for every different husk that has been inside its circle (once per husk, per sap);
//     - while a husk is inside a circle it wears a faint yellow glow shell (a slightly bigger see-through yellow cube, no collider).
//   Checked every checkSeconds (0.2); the circles follow their sap every frame (LateUpdate), lying flat at the bottom of the sap. Not saved (the bonuses are just
//   lifetime already added). disableSapField switches it off.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelSapField : MonoBehaviour
{
    [Tooltip("Switch the Sap fields off.")]
    [SerializeField] private bool disableSapField = false;
    [Min(0.5f)]
    [Tooltip("Radius of a sap's circle, in husk widths.")]
    [SerializeField] private float radiusHusks = 4f;
    [Min(0f)]
    [Tooltip("Lifetime a woodcutting husk gets the first time it is inside a sap's circle (seconds, once per husk).")]
    [SerializeField] private float huskBonusSeconds = 5f;
    [Min(0f)]
    [Tooltip("Lifetime the sap gets for every different husk that has been inside its circle (seconds, once per husk).")]
    [SerializeField] private float sapBonusPerHusk = 1f;
    [Tooltip("Colour of the circle (alpha = how see-through).")]
    [SerializeField] private Color circleColor = new Color(1f, 0.85f, 0.2f, 0.22f);
    [Tooltip("Colour of the glow shell on husks inside a circle (alpha = how strong).")]
    [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.25f, 0.28f);
    [Min(0.05f)]
    [Tooltip("Seconds between checks of which husks are inside the circles.")]
    [SerializeField] private float checkSeconds = 0.2f;

    private class Sap { public Rigidbody body; public GameObject circle; public readonly HashSet<Rigidbody> counted = new HashSet<Rigidbody>(); }

    private PixelClicker clicker;
    private readonly Dictionary<Rigidbody, Sap> saps = new Dictionary<Rigidbody, Sap>();
    private readonly HashSet<Rigidbody> boosted = new HashSet<Rigidbody>();             // husks that already got their one-time bonus
    private readonly Dictionary<Rigidbody, GameObject> glows = new Dictionary<Rigidbody, GameObject>();
    private readonly HashSet<Rigidbody> insideNow = new HashSet<Rigidbody>();
    private readonly List<Rigidbody> scratch = new List<Rigidbody>();
    private float timer;
    private Material circleMaterial, glowMaterial;
    private static Mesh discMesh;
    private int sapIndex = -2;

    private void Awake() { clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }

    private void OnDestroy()
    {
        foreach (Sap s in saps.Values) if (s.circle != null) Destroy(s.circle);
        foreach (GameObject g in glows.Values) if (g != null) Destroy(g);
        if (circleMaterial != null) Destroy(circleMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }

    private float Radius => radiusHusks * (clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f);

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); return; }
        if (disableSapField) { ClearAll(); return; }
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = checkSeconds;
        Check();
    }

    private void LateUpdate()
    {
        // The circles lie flat under their sap and follow it.
        foreach (Sap s in saps.Values)
        {
            if (s.body == null || s.circle == null) continue;
            Collider c = s.body.GetComponent<Collider>();
            float y = c != null ? c.bounds.min.y + 0.01f : s.body.position.y;
            s.circle.transform.position = new Vector3(s.body.position.x, y, s.body.position.z);
        }
        foreach (KeyValuePair<Rigidbody, GameObject> g in glows)
            if (g.Key != null && g.Value != null) g.Value.transform.SetPositionAndRotation(g.Key.position, g.Key.rotation);
    }

    private void Check()
    {
        if (sapIndex == -2 || !clicker.IsValidTierIndex(sapIndex)) sapIndex = clicker.IndexOf(PixelClicker.PixelType.Sap);
        PixelSkills.SkillPack wood = PixelSkills.Instance != null ? PixelSkills.Instance.Find(PixelSkills.WoodcuttingId) : null;
        var husks = clicker.OldPixels;

        // 1) the saps on the field (new ones get a circle, gone ones lose theirs)
        scratch.Clear();
        for (int i = 0; i < husks.Count; i++)
        {
            Rigidbody b = husks[i];
            if (b == null || TierOf(b) != sapIndex || sapIndex < 0) continue;
            OldPixelDespawn d = b.GetComponent<OldPixelDespawn>();
            if (d != null && d.IsDespawning) continue;
            scratch.Add(b);
            if (!saps.ContainsKey(b)) saps[b] = new Sap { body = b, circle = MakeCircle() };
        }
        List<Rigidbody> goneSaps = null;
        foreach (Rigidbody key in saps.Keys) if (key == null || !scratch.Contains(key)) (goneSaps ?? (goneSaps = new List<Rigidbody>())).Add(key);
        if (goneSaps != null) foreach (Rigidbody key in goneSaps) { if (saps[key].circle != null) Destroy(saps[key].circle); saps.Remove(key); }

        // 2) the woodcutting husks inside a circle: one-time bonus, the sap's bonus, the glow
        insideNow.Clear();
        float r = Radius, r2 = r * r;
        if (saps.Count > 0 && wood != null && wood.types != null)
            for (int i = 0; i < husks.Count; i++)
            {
                Rigidbody b = husks[i];
                if (b == null || saps.ContainsKey(b)) continue;
                int tier = TierOf(b);
                if (tier < 0 || tier == sapIndex || System.Array.IndexOf(wood.types, clicker.Tiers[tier].type) < 0) continue;
                OldPixelDespawn d = b.GetComponent<OldPixelDespawn>();
                if (d == null || d.IsDespawning) continue;
                foreach (Sap s in saps.Values)
                {
                    if (s.body == null) continue;
                    Vector3 off = b.position - s.body.position; off.y = 0f;
                    if (off.sqrMagnitude > r2) continue;
                    insideNow.Add(b);
                    if (boosted.Add(b)) d.AddLifetime(huskBonusSeconds);   // once ever per husk
                    if (s.counted.Add(b))                                    // once per husk, per sap
                    {
                        OldPixelDespawn sd = s.body.GetComponent<OldPixelDespawn>();
                        if (sd != null) sd.AddLifetime(sapBonusPerHusk);
                    }
                }
            }

        // glow shells on (only) the husks inside a circle right now
        foreach (Rigidbody b in insideNow) if (!glows.ContainsKey(b)) glows[b] = MakeGlow(b);
        scratch.Clear();
        foreach (Rigidbody key in glows.Keys) if (key == null || !insideNow.Contains(key)) scratch.Add(key);
        foreach (Rigidbody key in scratch) { if (glows[key] != null) Destroy(glows[key]); glows.Remove(key); }
        boosted.RemoveWhere(x => x == null);
    }

    private int TierOf(Rigidbody b)
    {
        OldPixelInfo info = b.GetComponent<OldPixelInfo>();
        return info != null && clicker.IsValidTierIndex(info.tierIndex) ? info.tierIndex : -1;
    }

    private void ClearAll()
    {
        foreach (Sap s in saps.Values) if (s.circle != null) Destroy(s.circle);
        saps.Clear();
        foreach (GameObject g in glows.Values) if (g != null) Destroy(g);
        glows.Clear();
    }

    // ---- looks ----

    private GameObject MakeCircle()
    {
        if (circleMaterial == null)
        {
            circleMaterial = new Material(PixelShaders.SpriteDefault()) { mainTexture = CircleTexture(), renderQueue = 3000 };
            circleMaterial.color = circleColor;
        }
        GameObject go = new GameObject("Sap Circle", typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = Disc();
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = circleMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        go.transform.localScale = new Vector3(Radius * 2f, 1f, Radius * 2f);
        return go;
    }

    private GameObject MakeGlow(Rigidbody b)
    {
        if (glowMaterial == null) { glowMaterial = new Material(PixelShaders.SpriteDefault()) { renderQueue = 3001 }; glowMaterial.color = glowColor; }
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Sap Glow";
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = glowMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Collider hc = b.GetComponent<Collider>();
        float size = hc is BoxCollider box ? Mathf.Max(box.size.x * b.transform.lossyScale.x, box.size.y * b.transform.lossyScale.y) : (hc != null ? hc.bounds.size.y : 0.3f);
        go.transform.localScale = Vector3.one * size * 1.12f;
        go.transform.SetPositionAndRotation(b.position, b.rotation);
        return go;
    }

    /// <summary>A flat unit disc (diameter 1) lying in the XZ plane, facing up, with UVs for the circle texture.</summary>
    private static Mesh Disc()
    {
        if (discMesh != null) return discMesh;
        discMesh = new Mesh { name = "Sap Disc" };
        discMesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        discMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        discMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        discMesh.RecalculateNormals();
        discMesh.RecalculateBounds();
        return discMesh;
    }

    /// <summary>A soft filled circle with a brighter rim (white; the material colour tints it).</summary>
    private static Texture2D CircleTexture()
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float fill = d < 1f ? 0.45f : 0f;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.95f) / 0.05f);
                float a = Mathf.Clamp01(fill + rim) * Mathf.Clamp01((1f - d) / 0.02f + (d < 1f ? 1f : 0f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d <= 1f ? a : 0f) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
