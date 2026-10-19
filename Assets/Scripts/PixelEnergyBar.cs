// ---- System notes ----
// PixelEnergyBar.cs
//   A small camera-facing horizontal BAR floating over a placed thing: a dark track and a coloured fill from the left (0..1). The Biomass Generators show
//   their charge with it (blue; flashing red when empty), and every device a generator powers shows the SAME bar over itself, tracking that generator's
//   charge (PixelPlacedDevice.HoldPower(fraction)). Two quads with the shared Sprites/Default material (colours by property block, no material copies);
//   it keeps its own size on a scaled parent. PixelEnergyBar.Attach(parent, localPosition, width, colour); Set(fraction, colour) - below 0 hides it.
// ---------------------------------------------------
using UnityEngine;

public sealed class PixelEnergyBar : MonoBehaviour
{
    private static Material sharedMaterial;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform body, fill;
    private Renderer trackRenderer, fillRenderer;
    private MaterialPropertyBlock block;
    private float width, shown = -2f;
    private Color shownColor;

    /// <summary>Adds a bar at 'localPosition' under 'parent' ('width' in world units; a fifth of it high). Hidden until Set.</summary>
    public static PixelEnergyBar Attach(Transform parent, Vector3 localPosition, float width, Color color)
    {
        GameObject go = new GameObject("Energy Bar");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        PixelEnergyBar b = go.AddComponent<PixelEnergyBar>();
        b.width = Mathf.Max(0.05f, width);
        b.Build();
        b.Set(-1f, color);
        return b;
    }

    private void Build()
    {
        if (sharedMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            sharedMaterial = new Material(s != null ? s : Shader.Find("Unlit/Color")) { name = "Energy Bar" };
        }
        block = new MaterialPropertyBlock();
        body = new GameObject("Bar").transform;
        body.SetParent(transform, false);
        float h = width * 0.2f;
        trackRenderer = Quad("Track", body, new Vector3(0f, 0f, 0f), new Vector3(width + h * 0.3f, h * 1.3f, 1f));
        fill = Quad("Fill", body, new Vector3(0f, 0f, -0.002f), new Vector3(width, h, 1f)).transform;
        fillRenderer = fill.GetComponent<Renderer>();
        Paint(trackRenderer, new Color(0.05f, 0.08f, 0.14f, 0.85f));
    }

    private static Renderer Quad(string name, Transform parent, Vector3 pos, Vector3 scale)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Collider c = q.GetComponent<Collider>();
        if (c != null) Destroy(c);
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = sharedMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    private void Paint(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor(ColorId, c);
        r.SetPropertyBlock(block);
    }

    /// <summary>Shows the bar 'fraction' full (0..1) in 'color'; a fraction below 0 hides it.</summary>
    public void Set(float fraction, Color color)
    {
        bool visible = fraction >= 0f;
        if (body != null && body.gameObject.activeSelf != visible) body.gameObject.SetActive(visible);
        if (!visible) { shown = -2f; return; }
        fraction = Mathf.Clamp01(fraction);
        if (Mathf.Abs(fraction - shown) > 0.002f)
        {
            shown = fraction;
            float h = width * 0.2f;
            fill.localScale = new Vector3(Mathf.Max(0.0001f, width * fraction), h, 1f);
            fill.localPosition = new Vector3(-width * 0.5f + width * fraction * 0.5f, 0f, -0.002f);
        }
        if (color != shownColor) { shownColor = color; Paint(fillRenderer, color); }
    }

    private void LateUpdate()
    {
        if (body == null || !body.gameObject.activeSelf) return;
        Transform parent = transform.parent;
        float s = parent != null ? Mathf.Max(0.05f, parent.lossyScale.x) : 1f;
        transform.localScale = Vector3.one / s;   // (its own size on a scaled device)
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        Camera cam = clicker != null ? clicker.ViewCamera : Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation;   // (faces the camera, like the device timers)
    }
}
