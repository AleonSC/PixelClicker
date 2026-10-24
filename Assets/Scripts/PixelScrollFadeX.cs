using UnityEngine;

/// <summary>
/// Soft fade-out at the left / right edge of a SIDEWAYS scroll view (the skill tree): every child of 'content' that has a <see cref="CanvasGroup"/> gets an alpha
/// that falls to 0 towards an edge - but only an edge that has more content behind it (the left one once scrolled away from the start, the right one until the
/// end is reached). Children are positioned by anchoredPosition from the content's top-left corner (anchor and pivot (0,1)). Works on the elements, never over
/// the window, like the vertical bottom fade of the lists.
/// </summary>
public class PixelScrollFadeX : MonoBehaviour
{
    public RectTransform view;
    public RectTransform content;
    [Min(1f)] public float fadeWidth = 110f;

    private readonly System.Collections.Generic.Dictionary<Transform, CanvasGroup> groups = new System.Collections.Generic.Dictionary<Transform, CanvasGroup>();

    /// <summary>The child's CanvasGroup, remembered once found (looked up again while it has none, so one added later is still picked up).</summary>
    private CanvasGroup GroupOf(Transform t)
    {
        if (groups.TryGetValue(t, out CanvasGroup g) && g != null) return g;
        if (groups.Count > content.childCount * 2 + 16) groups.Clear();   // (children rebuilt: forget the old ones)
        g = t.GetComponent<CanvasGroup>();
        if (g != null) groups[t] = g;
        return g;
    }

    private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    private void LateUpdate()
    {
        if (view == null || content == null) return;
        float viewW = view.rect.width, contentW = content.rect.width;
        float shift = content.anchoredPosition.x;            // <= 0 when scrolled to the right
        bool fadeLeft = shift < -1f;
        bool fadeRight = contentW + shift > viewW + 1f;
        for (int i = 0; i < content.childCount; i++)
        {
            Transform t = content.GetChild(i);
            if (!t.gameObject.activeSelf) continue;
            CanvasGroup g = GroupOf(t);
            RectTransform r = t as RectTransform;
            if (g == null || r == null) continue;
            float centre = r.anchoredPosition.x + shift + r.sizeDelta.x * 0.5f;   // where the element's middle is inside the view
            float a = 1f;
            if (fadeLeft) a = Mathf.Min(a, Smooth(centre / fadeWidth));
            if (fadeRight) a = Mathf.Min(a, Smooth((viewW - centre) / fadeWidth));
            g.alpha = a;
            g.blocksRaycasts = a > 0.15f;
        }
    }
}
