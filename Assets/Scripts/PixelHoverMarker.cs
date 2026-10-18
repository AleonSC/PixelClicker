// ---- System notes ----
// PixelHoverMarker.cs
//   A small floating DOWN ARROW over a placed device that shows only while the pointer is on the device (pops in / out, bobs, faces the camera), with
//   an optional short text beside it (the arrow is ONE solid mesh - shaft + triangle head - since 2026-10-18; three bars read as a trident) (the Recycler's 'm:ss' left). ONE shared piece: the Recycler and the Sorting Hopper use it (Attach); a new device
//   that wants the same 'this is where it goes' hint attaches one too. 'isOver' says whether a pointer ray is on the device (usually its
//   PixelPlacedDevice.HitTest); the marker's own parts are named "Hover ..." so they read as part of the device.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;

public class PixelHoverMarker : MonoBehaviour
{
    [Tooltip("Colour of the arrow and the text beside it.")]
    [SerializeField] private Color color = new Color(0.55f, 1f, 0.6f, 1f);
    [Min(0.01f)]
    [Tooltip("Seconds it takes to pop in / out.")]
    [SerializeField] private float popSeconds = 0.12f;

    private Func<Ray, bool> isOver;
    private Func<string> text;
    private Transform arrow, body;
    private TextMeshPro label;
    private float show, u = 0.5f;

    /// <summary>Puts a hover arrow (and optional text beside it) at 'localPos' under 'parent'. 'u' = a husk width, the arrow's size unit.</summary>
    public static PixelHoverMarker Attach(Transform parent, Vector3 localPos, float u, Color color, Func<Ray, bool> isOver, Func<string> text = null)
    {
        GameObject m = new GameObject("Hover Marker");
        m.transform.SetParent(parent, false);
        m.transform.localPosition = localPos;
        PixelHoverMarker h = m.AddComponent<PixelHoverMarker>();
        h.color = color; h.isOver = isOver; h.text = text; h.u = u;
        h.Build();
        return h;
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        body = new GameObject("Hover Body").transform;
        body.SetParent(transform, false);
        arrow = new GameObject("Hover Arrow").transform;
        arrow.SetParent(body, false);
        PixelFactoryKit kit = new PixelFactoryKit(clicker, false);
        Color c = color;
        // ONE solid arrow: the classic outline (a shaft and a wide triangle head pointing down), extruded a little so it has depth.
        GameObject a = new GameObject("Hover Arrow Shape");
        a.transform.SetParent(arrow, false);
        a.AddComponent<MeshFilter>().sharedMesh = ArrowMesh(u);
        a.AddComponent<MeshRenderer>().sharedMaterial = kit.Mat(c, 0f, 0.6f, c * 0.6f);
        if (text != null)
        {
            GameObject tg = new GameObject("Hover Text");
            tg.transform.SetParent(body, false);
            tg.transform.localPosition = new Vector3(0.55f * u, 0.1f * u, 0f);
            label = tg.AddComponent<TextMeshPro>();
            label.fontSize = 2.6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(4f, 1f);
            label.color = c;
            if (clicker != null && clicker.UIFont != null) label.font = clicker.UIFont;
        }
        body.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (body == null) return;
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        Camera cam = clicker != null ? clicker.ViewCamera : null;
        bool over = cam != null && isOver != null && !PixelInput.PointerOverUI() && !PixelCalm.InMenu && isOver(cam.ScreenPointToRay(PixelInput.PointerPosition()));
        show = Mathf.MoveTowards(show, over ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, popSeconds));
        bool visible = show > 0.001f;
        if (body.gameObject.activeSelf != visible) body.gameObject.SetActive(visible);
        if (!visible) return;
        // The parent may be scaled (a bigger Recycler): keep the marker its own size.
        float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        body.localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, show) / Mathf.Max(0.05f, parentScale));
        if (cam != null) body.rotation = Quaternion.LookRotation(body.position - cam.transform.position, Vector3.up);
        if (arrow != null) arrow.localPosition = new Vector3(0f, Mathf.Sin(Time.unscaledTime * 4f) * 0.08f * u, 0f);
        if (label != null && text != null)
        {
            string t = text();
            if (label.text != t) label.text = t;
        }
    }

    private static Mesh arrowMesh;
    private static float arrowMeshU = -1f;

    /// <summary>A flat arrow pointing down (tip at y = 0), 'u' sized, with front, back and side faces (shared by every marker of that size).</summary>
    private static Mesh ArrowMesh(float u)
    {
        if (arrowMesh != null && Mathf.Approximately(arrowMeshU, u)) return arrowMesh;
        // The outline, going round: the tip, the head's right corner, into the shaft, up the shaft, across, down, the head's left corner.
        Vector2[] o =
        {
            new Vector2(0f, 0f), new Vector2(0.3f, 0.3f), new Vector2(0.11f, 0.3f), new Vector2(0.11f, 0.72f),
            new Vector2(-0.11f, 0.72f), new Vector2(-0.11f, 0.3f), new Vector2(-0.3f, 0.3f),
        };
        float d = 0.04f * u;
        var v = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        // Front and back faces: the head triangle (0,1,6) and the shaft quad (2,3,4,5).
        int[][] faces = { new[] { 0, 6, 1 }, new[] { 2, 5, 4 }, new[] { 2, 4, 3 } };
        // Every face is added twice, once each way round with its own corners, so it shows from both sides and lights properly.
        for (int side = -1; side <= 1; side += 2)
            for (int way = 0; way < 2; way++)
            {
                int b = v.Count;
                foreach (Vector2 p in o) v.Add(new Vector3(p.x * u, p.y * u, side * d));
                foreach (int[] f in faces)
                {
                    t.Add(b + f[0]);
                    t.Add(b + (way == 0 ? f[1] : f[2]));
                    t.Add(b + (way == 0 ? f[2] : f[1]));
                }
            }
        // The rim: one quad for every edge of the outline.
        for (int i = 0; i < o.Length; i++)
        {
            Vector2 p0 = o[i] * u, p1 = o[(i + 1) % o.Length] * u;
            for (int way = 0; way < 2; way++)
            {
                int b = v.Count;
                v.Add(new Vector3(p0.x, p0.y, -d)); v.Add(new Vector3(p1.x, p1.y, -d));
                v.Add(new Vector3(p1.x, p1.y, d)); v.Add(new Vector3(p0.x, p0.y, d));
                if (way == 0) { t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2); }
                else { t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3); }
            }
        }
        arrowMesh = new Mesh { name = "Hover Arrow" };
        arrowMesh.SetVertices(v);
        arrowMesh.SetTriangles(t, 0);
        arrowMesh.RecalculateNormals();
        arrowMesh.RecalculateBounds();
        arrowMeshU = u;
        return arrowMesh;
    }
}
