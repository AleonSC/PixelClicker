// ---- System notes ----
// PixelHoverMarker.cs
//   A small floating DOWN ARROW over a placed device that shows only while the pointer is on the device (pops in / out, bobs, faces the camera), with
//   an optional short text beside it (the Recycler's 'm:ss' left). ONE shared piece: the Recycler and the Sorting Hopper use it (Attach); a new device
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
        // A short shaft and a head of two slanted bars meeting at the bottom.
        kit.Box("Hover Arrow Shaft", arrow, new Vector3(0f, 0.22f * u, 0f), new Vector3(0.14f * u, 0.42f * u, 0.06f * u), c, default, false, 0f, 0.6f, c);
        for (int sx = -1; sx <= 1; sx += 2)
            kit.Box("Hover Arrow Head", arrow, new Vector3(sx * 0.11f * u, -0.04f * u, 0f), new Vector3(0.12f * u, 0.36f * u, 0.06f * u), c, new Vector3(0f, 0f, sx * 40f), false, 0f, 0.6f, c);
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
}
