// ---- System notes ----
// PixelClickerAura.cs
//   A soft glow BEHIND the clicker cube to make it easier to find without touching the pixels themselves (Settings > Interface > 'Clicker glow aura', PixelClickerAura.Enabled,
//   PlayerPrefs PixelClicker.Setting.ClickerAura, default ON, saved per profile). A camera-facing quad with a radial fade (Sprites/Default, alpha blended) sits just behind the cube as seen
//   from the camera, so the cube hides the middle and a halo shows round it from any view. Its colour is the colour of the pixel being clicked (washed towards white), fades smoothly when the
//   tier changes and breathes slowly. The cube is CUT OUT of it: PixelDepthMask.shader (Resources) draws a copy of the cube's main mesh that writes only depth, in queue 3001 between the cube and the aura (3002), so a transparent cube doesn't show the glow through itself. Added by PixelClicker.Awake. Tunables are [SerializeField] fields.
// ---------------------------------------------------
using UnityEngine;

public class PixelClickerAura : MonoBehaviour
{
    private const string Pref = "PixelClicker.Setting.ClickerAura";
    private static int cache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache = -1; }

    /// <summary>Settings > Interface > Clicker glow aura (default on).</summary>
    public static bool Enabled
    {
        get { if (cache < 0) cache = PlayerPrefs.GetInt(Pref, 1); return cache != 0; }
        set { cache = value ? 1 : 0; PlayerPrefs.SetInt(Pref, cache); PlayerPrefs.Save(); }
    }

    public static void ReloadFromPrefs() { cache = -1; }

    [Header("Look")]
    [Tooltip("Width of the halo as a multiple of the cube's size.")]
    [SerializeField] private float sizeInCubes = 3.6f;

    [Range(0f, 1f)]
    [Tooltip("How visible the halo is (its strongest alpha).")]
    [SerializeField] private float strength = 0.38f;

    [Range(0f, 1f)]
    [Tooltip("How much the pixel's colour is washed towards white (0 = the pixel's own colour, 1 = white).")]
    [SerializeField] private float whiten = 0.3f;

    [Tooltip("How far behind the cube (as a multiple of its size, away from the camera) the halo sits.")]
    [SerializeField] private float behindCubes = 0.6f;

    [Header("Motion")]
    [Tooltip("How fast the halo breathes (cycles per second). 0 = steady.")]
    [SerializeField] private float breatheSpeed = 0.35f;

    [Range(0f, 0.5f)]
    [Tooltip("How much the halo's strength and size breathe.")]
    [SerializeField] private float breatheAmount = 0.2f;

    [Tooltip("How fast the colour follows when the pixel changes (per second).")]
    [SerializeField] private float colorFollow = 6f;

    private PixelClicker clicker;
    private Transform quad;
    private MeshRenderer quadRenderer;
    private Material material;
    private Texture2D texture;
    private Color color = Color.white;
    private Shader maskShader;
    private Material maskMaterial;
    private MeshFilter maskSource;
    private Transform mask;
    private MeshFilter maskFilter;
    private MeshRenderer maskRenderer;
    private float maskSearchAt;

    private void Awake() { clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }

    private void Build()
    {
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return;
        const int n = 128;
        texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Clicker Aura" };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.1f) * (0.55f + 0.45f * Mathf.Clamp01(d * 3f));   // dim in the very middle (the cube covers it), bright just outside it, fading to nothing
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        texture.Apply(false, true);
        material = new Material(shader) { mainTexture = texture };
        material.renderQueue = 3002;   // after the cube (even a transparent one) and after the depth mask (3001) that cuts the cube's shape out of it
        maskShader = Resources.Load<Shader>("PixelDepthMask");

        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "Clicker Aura";
        Collider c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        quad = g.transform;
        quadRenderer = g.GetComponent<MeshRenderer>();
        quadRenderer.sharedMaterial = material;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
    }

    /// <summary>The cube's silhouette: a copy of its main mesh that writes only depth, so the aura (drawn behind it) is hidden wherever the cube is.</summary>
    private void UpdateMask(bool on)
    {
        if (maskShader == null) return;
        if (on && (maskSource == null || !maskSource.gameObject.activeInHierarchy) && Time.unscaledTime >= maskSearchAt)
        {
            maskSearchAt = Time.unscaledTime + 0.25f;
            maskSource = null;
            float best = 0f;
            foreach (MeshFilter mf in clicker.PixelTransform.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf == maskFilter || mf.sharedMesh == null) continue;
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                float size = mf.sharedMesh.bounds.size.sqrMagnitude * mf.transform.lossyScale.sqrMagnitude;
                if (size > best) { best = size; maskSource = mf; }
            }
        }
        bool show = on && maskSource != null && maskSource.gameObject.activeInHierarchy;
        if (!show) { if (mask != null && mask.gameObject.activeSelf) mask.gameObject.SetActive(false); return; }
        if (mask == null)
        {
            GameObject g = new GameObject("Clicker Aura Mask", typeof(MeshFilter), typeof(MeshRenderer));
            mask = g.transform;
            maskFilter = g.GetComponent<MeshFilter>();
            maskRenderer = g.GetComponent<MeshRenderer>();
            maskMaterial = new Material(maskShader);
            maskMaterial.renderQueue = 3001;
            maskRenderer.sharedMaterial = maskMaterial;
            maskRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            maskRenderer.receiveShadows = false;
        }
        if (!mask.gameObject.activeSelf) mask.gameObject.SetActive(true);
        if (maskFilter.sharedMesh != maskSource.sharedMesh) maskFilter.sharedMesh = maskSource.sharedMesh;
        mask.SetPositionAndRotation(maskSource.transform.position, maskSource.transform.rotation);
        mask.localScale = maskSource.transform.lossyScale;
        mask.gameObject.layer = maskSource.gameObject.layer;
    }

    private void OnDestroy()
    {
        if (mask != null) Destroy(mask.gameObject);
        if (maskMaterial != null) Destroy(maskMaterial);
        if (quad != null) Destroy(quad.gameObject);
        if (material != null) Destroy(material);
        if (texture != null) Destroy(texture);
    }

    private void LateUpdate()
    {
        if (clicker == null || clicker.PixelTransform == null) return;
        bool on = Enabled && clicker.PixelTransform.gameObject.activeInHierarchy;
        if (quad == null) { if (!on) return; Build(); if (quad == null) return; }
        if (quadRenderer.enabled != on) quadRenderer.enabled = on;
        UpdateMask(on);
        if (!on) return;

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        float baseSize = clicker.PixelBaseSize;
        float t = Time.unscaledTime * breatheSpeed * Mathf.PI * 2f;
        float breathe = breatheSpeed > 0f ? Mathf.Sin(t) : 0f;

        Vector3 toCam = (cam.transform.position - clicker.PixelTransform.position).normalized;
        quad.position = clicker.PixelTransform.position - toCam * baseSize * behindCubes;
        quad.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        quad.localScale = Vector3.one * baseSize * sizeInCubes * (1f + breathe * breatheAmount * 0.3f);
        quad.gameObject.layer = clicker.PixelTransform.gameObject.layer;

        Color target = Color.white;
        int tier = clicker.CurrentClickTierIndex;
        if (clicker.IsValidTierIndex(tier)) target = clicker.Tiers[tier].color;
        target = Color.Lerp(target, Color.white, whiten);
        color = Color.Lerp(color, target, 1f - Mathf.Exp(-colorFollow * Time.unscaledDeltaTime));
        color.a = strength * (1f + breathe * breatheAmount);
        material.color = color;
    }
}
