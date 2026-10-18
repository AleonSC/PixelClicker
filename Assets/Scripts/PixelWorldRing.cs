// ---- System notes ----
// PixelWorldRing.cs
//   A small camera-facing RING floating over a placed device: a dim full track and a bright arc that fills clockwise from the top (0..1). The
//   compact sign that replaced the devices' floating text once the shared device card (PixelDeviceCard) took over the details: the Recycler's
//   contents, the Quantum Organizer's fill, the Sweeper's wind left, every timed device's time left. Two LineRenderers (one shared Sprites/Default
//   material); the arc's points are only recomputed when the fill changes by a visible step, never every frame. Set(fraction, colour); a
//   fraction below 0 hides it. PixelWorldRing.Attach(parent, localPosition, radius, colour).
// ---------------------------------------------------
using UnityEngine;

public sealed class PixelWorldRing : MonoBehaviour
{
    private const int Segments = 40;
    private static Material sharedMaterial;
    private static PixelClicker clicker;

    private LineRenderer track, arc;
    private readonly Vector3[] points = new Vector3[Segments + 1];
    private float radius, shown = -2f;
    private Color color, trackColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { clicker = null; }

    /// <summary>Adds a ring at 'localPosition' under 'parent' (world-unit radius; the line is a quarter of it thick). Hidden until Set.</summary>
    public static PixelWorldRing Attach(Transform parent, Vector3 localPosition, float radius, Color color)
    {
        GameObject go = new GameObject("Status Ring");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        PixelWorldRing r = go.AddComponent<PixelWorldRing>();
        r.radius = Mathf.Max(0.01f, radius);
        r.color = color;
        r.track = r.Line("Track", 0);
        r.arc = r.Line("Arc", 1);
        r.Set(-1f, color);
        return r;
    }

    private LineRenderer Line(string objectName, int order)
    {
        if (sharedMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            sharedMaterial = new Material(s != null ? s : Shader.Find("Unlit/Color")) { name = "World Ring" };
        }
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        LineRenderer l = go.AddComponent<LineRenderer>();
        l.useWorldSpace = false;
        l.sharedMaterial = sharedMaterial;
        l.widthMultiplier = radius * 0.28f;
        l.numCapVertices = 2;
        l.alignment = LineAlignment.TransformZ;
        l.sortingOrder = order;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.receiveShadows = false;
        return l;
    }

    /// <summary>Shows the ring filled to 'fraction' (0..1) in 'c'; below 0 hides it.</summary>
    public void Set(float fraction, Color c)
    {
        bool on = fraction >= 0f;
        if (track.enabled != on) { track.enabled = on; arc.enabled = on; }
        if (!on) { shown = -2f; return; }
        if (c != color || shown < -1f)
        {
            color = c;
            trackColor = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.55f);
            track.startColor = track.endColor = trackColor;
            arc.startColor = arc.endColor = c;
            if (shown < -1f) { Fill(track, 1f); }
        }
        fraction = Mathf.Clamp01(fraction);
        if (Mathf.Abs(fraction - shown) < 0.5f / Segments) return;   // (only a visible step redraws the arc)
        shown = fraction;
        Fill(arc, fraction);
    }

    private void Fill(LineRenderer l, float fraction)
    {
        int n = Mathf.Max(2, Mathf.CeilToInt(Segments * fraction) + 1);
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.PI * 0.5f - fraction * Mathf.PI * 2f * i / (n - 1);   // clockwise from the top
            points[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        l.positionCount = n;
        for (int i = 0; i < n; i++) l.SetPosition(i, points[i]);
    }

    private void LateUpdate()
    {
        if (!track.enabled) return;
        if (clicker == null) clicker = PixelFind.Cached<PixelClicker>();
        Camera cam = clicker != null ? clicker.ViewCamera : null;
        if (cam != null) transform.rotation = cam.transform.rotation;
    }
}
