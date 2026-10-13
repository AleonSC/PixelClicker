// ---- System notes ----
// PixelFloorModes.cs
//   What the floor does while 'Display floor' is OFF (Settings > Display, 'When floor is off': PixelFloor.OffMode, PlayerPrefs
//   PixelClicker.Setting.FloorOffMode, global): 0 Invisible (only the sky, the default), 1 Flat plane (PixelFloor.UpdatePlane: a flat
//   opaque lit sheet in the floor style's average colour just under the floor top, receives shadows; the edge fade melts its rim into
//   the sky), 2 Landing pulse (PixelFloorPulse.Landed, called from OldPixelImpact: a soft rounded-square ripple, tinted by the pixel's
//   colour, lies on the surface the pixel landed on - turned to the contact normal and the pixel's heading, sized by the pixel and the
//   impact - grows and fades in about 0.7 s; at most 24 at once, so it also works on slopes / shapes / any other solid surface).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public partial class PixelFloor
{
    public static readonly string[] OffModeNames = { "Invisible", "Flat plane", "Landing pulse" };
    private const string OffModePref = "PixelClicker.Setting.FloorOffMode";
    private static int offModeCache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOffMode() { offModeCache = -1; }

    /// <summary>What the floor shows while 'Display floor' is off: 0 invisible, 1 flat plane, 2 landing pulse.</summary>
    public static int OffMode
    {
        get
        {
            if (offModeCache < 0) offModeCache = Mathf.Clamp(PlayerPrefs.GetInt(OffModePref, 0), 0, OffModeNames.Length - 1);
            return offModeCache;
        }
        set
        {
            offModeCache = Mathf.Clamp(value, 0, OffModeNames.Length - 1);
            PlayerPrefs.SetInt(OffModePref, offModeCache);
        }
    }

    /// <summary>The floor is off and its landing pulses are switched on.</summary>
    public static bool PulseActive => !PixelTitleWanderers.DisplayFloor && OffMode == 2;

    private bool PlaneMode => !PixelTitleWanderers.DisplayFloor && OffMode == 1;

    private GameObject plane;
    private Material planeMat;
    private int planeStyle = -2;

    /// <summary>True while the flat plane is showing (the edge fade then rims it).</summary>
    private bool PlaneVisible => plane != null && plane.activeSelf;

    private void UpdatePlane()
    {
        bool hideForTitle = HidesOnTitle && PixelTitleWanderers.LiveBackground && (PixelTitleScreen.Showing || PixelCameraIntro.Moving || exitHold);
        bool want = PlaneMode && floorRenderer != null && !hideForTitle;
        if (!want)
        {
            if (plane != null && plane.activeSelf) plane.SetActive(false);
            return;
        }
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        Bounds fb = floorRenderer.bounds;
        if (plane == null)
        {
            float half = Mathf.Max(fb.extents.x, fb.extents.z);
            plane = new GameObject("Floor Plane");
            Mesh mesh = new Mesh { name = "Floor Plane" };
            mesh.vertices = new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            plane.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = plane.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            planeMat = clicker.CreateVisualMaterial(Color.gray, false);
            if (planeMat != null) mr.sharedMaterial = planeMat;
            planeStyle = -2;
        }
        plane.transform.position = new Vector3(fb.center.x, fb.max.y - 0.004f, fb.center.z);
        if (planeMat != null && planeStyle != current)
        {
            planeStyle = current;
            Color c = AverageColor();
            c.a = 1f;
            SetColor(planeMat, "_BaseColor", c);
            SetColor(planeMat, "_Color", c);
        }
        if (!plane.activeSelf) plane.SetActive(true);
    }
}

/// <summary>Landing pulses: a small ripple on the surface an old pixel lands on (used while the floor is invisible and 'Landing pulse' is chosen).</summary>
public class PixelFloorPulse : MonoBehaviour
{
    private const int MaxRipples = 24;
    private const float Life = 0.7f;

    private class Ripple { public Transform t; public Material m; public float age, start, end; public Color color; }

    private static PixelFloorPulse instance;
    private static Mesh quad;
    private static Texture2D ringTexture;
    private static PixelClicker clicker;
    private readonly List<Ripple> ripples = new List<Ripple>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; quad = null; ringTexture = null; clicker = null; }

    /// <summary>An old pixel hit a surface: puts a ripple there (if the landing pulse is switched on).</summary>
    public static void Landed(Collision collision, Transform pixel, int tierIndex, float intensity)
    {
        if (!PixelFloor.PulseActive || collision.contactCount == 0 || pixel == null) return;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        if (instance == null)
        {
            GameObject go = new GameObject("Floor Pulses");
            instance = go.AddComponent<PixelFloorPulse>();
        }
        ContactPoint contact = collision.GetContact(0);
        Color color = clicker.IsValidTierIndex(tierIndex) ? clicker.Tiers[tierIndex].UIColor : Color.white;
        instance.Add(contact.point, contact.normal, pixel.eulerAngles.y, clicker.OldPixelWorldSize, intensity, color);
    }

    private void Add(Vector3 point, Vector3 normal, float yaw, float pixelSize, float intensity, Color color)
    {
        if (quad == null) BuildAssets();
        Shader shader = PixelShaders.SpriteDefault();
        if (shader == null) return;
        while (ripples.Count >= MaxRipples) Remove(0);
        GameObject go = new GameObject("Ripple");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Material m = new Material(shader) { mainTexture = ringTexture };
        mr.sharedMaterial = m;
        Quaternion onSurface = Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up);
        go.transform.rotation = onSurface * Quaternion.Euler(0f, yaw, 0f);
        go.transform.position = point + normal.normalized * 0.012f;
        float size = Mathf.Max(0.2f, pixelSize);
        ripples.Add(new Ripple { t = go.transform, m = m, start = size * (1.1f + 0.3f * intensity), end = size * (2.0f + 1.6f * intensity), color = color });
        go.transform.localScale = Vector3.one * ripples[ripples.Count - 1].start;
        m.color = new Color(color.r, color.g, color.b, 0f);
    }

    private void Remove(int i)
    {
        Ripple r = ripples[i];
        if (r.t != null) Destroy(r.t.gameObject);
        if (r.m != null) Destroy(r.m);
        ripples.RemoveAt(i);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Ripple r = ripples[i];
            r.age += dt;
            float k = r.age / Life;
            if (k >= 1f || r.t == null) { Remove(i); continue; }
            float grow = 1f - (1f - k) * (1f - k);
            r.t.localScale = Vector3.one * Mathf.Lerp(r.start, r.end, grow);
            float alpha = Mathf.Sin(Mathf.Min(1f, k * 6f) * Mathf.PI * 0.5f) * (1f - k) * 0.8f;
            r.m.color = new Color(r.color.r, r.color.g, r.color.b, alpha);
        }
    }

    private void OnDestroy()
    {
        foreach (Ripple r in ripples) if (r.m != null) Destroy(r.m);
        ripples.Clear();
        if (instance == this) instance = null;
    }

    private static void BuildAssets()
    {
        quad = new Mesh { name = "Ripple Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
        quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quad.RecalculateBounds();

        // A soft rounded-square outline (the pixel's own footprint) with a faint fill.
        const int n = 128;
        ringTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Ripple Ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);   // a rounded square
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.07f, 2f));
                float fill = d < 0.82f ? 0.12f * (1f - d / 0.82f) : 0f;
                float a = Mathf.Clamp01(ring + fill) * (d > 1f ? 0f : 1f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        ringTexture.SetPixels32(px);
        ringTexture.Apply(false);
    }
}
