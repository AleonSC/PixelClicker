using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes a scroll bar fade away as soon as it is not in use: it shows while the list scrolls (mouse wheel, dragging, the handle), while the
/// mouse is over the bar or the handle is held, stays a moment after the last movement, then fades out quickly. Added to every scroll bar by
/// <see cref="PixelUIKit.StyleScrollBar"/>. A faded bar still catches the mouse, so hovering where it was brings it back.
/// </summary>
[RequireComponent(typeof(Scrollbar))]
public class PixelScrollBarFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    /// <summary>Seconds the bar stays fully visible after the list last moved.</summary>
    public float holdSeconds = 0.45f;

    /// <summary>Seconds the fade-out (and fade-in) takes.</summary>
    public float fadeSeconds = 0.18f;

    [Tooltip("Height (canvas units) of the fade at the bottom of the list.")]
    public float fadeHeight = 70f;

    private CanvasGroup group;
    private ScrollRect scroll;
    private bool lookedForScroll;
    private CanvasGroup fadeGroup;
    private RectTransform chevron;
    private static Sprite gradientSprite;
    private Scrollbar bar;
    private float lastMove = -100f;
    private bool hovering, pressed;

    private void Awake()
    {
        bar = GetComponent<Scrollbar>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        bar.onValueChanged.AddListener(_ => lastMove = Time.unscaledTime);
    }

    private void OnEnable()
    {
        if (group != null) group.alpha = 0f;   // starts hidden; a scroll or a hover shows it
        hovering = pressed = false;
    }

    private void Update()
    {
        if (group == null) return;
        bool active = hovering || pressed || Time.unscaledTime - lastMove < holdSeconds;
        group.alpha = Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        UpdateBottomFade();
    }

    /// <summary>
    /// The content fades into nothing at the bottom of a list that scrolls (an overlay in the window's own colour, so the window stays as it is),
    /// with a little bobbing chevron; it goes away when the list fits or is scrolled to the end.
    /// </summary>
    private void UpdateBottomFade()
    {
        if (!lookedForScroll)
        {
            lookedForScroll = true;
            foreach (ScrollRect sr in GetComponentsInParent<ScrollRect>(true)) if (sr.verticalScrollbar == bar) { scroll = sr; break; }
            if (scroll == null && transform.parent != null)
                foreach (ScrollRect sr in transform.parent.GetComponentsInChildren<ScrollRect>(true)) if (sr.verticalScrollbar == bar) { scroll = sr; break; }
            if (scroll != null && scroll.GetComponentInParent<TMPro.TMP_Dropdown>() == null) BuildBottomFade();
        }
        if (fadeGroup == null || scroll == null || scroll.content == null) return;
        RectTransform view = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        bool scrolls = scroll.content.rect.height > view.rect.height + 1f;
        bool more = scrolls && scroll.verticalNormalizedPosition > 0.02f;
        fadeGroup.alpha = Mathf.MoveTowards(fadeGroup.alpha, more ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);
        if (chevron != null) chevron.anchoredPosition = new Vector2(0f, 14f + Mathf.Sin(Time.unscaledTime * 3.2f) * 5f);
    }

    private void BuildBottomFade()
    {
        Transform parent = scroll.viewport != null ? scroll.viewport : scroll.transform;
        if (parent.Find("Bottom Fade") != null) return;
        if (gradientSprite == null)
        {
            Texture2D tex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Scroll Fade" };
            for (int y = 0; y < 32; y++)
            {
                float k = 1f - y / 31f;                       // y = 0 is the bottom edge: fully covered, fading to clear towards the top
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, k * k * (3f - 2f * k)));
            }
            tex.Apply(false, true);
            gradientSprite = Sprite.Create(tex, new Rect(0, 0, 1, 32), new Vector2(0.5f, 0.5f), 100f);
        }

        GameObject go = new GameObject("Bottom Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        Image img = go.GetComponent<Image>();
        img.sprite = gradientSprite;
        img.color = PixelUIKit.WindowBodyColorAbove(parent);
        img.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(0f, fadeHeight);
        r.anchoredPosition = Vector2.zero;
        fadeGroup = go.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        // A little chevron ("there is more below") that bobs.
        GameObject holder = new GameObject("Chevron", typeof(RectTransform));
        holder.transform.SetParent(go.transform, false);
        chevron = holder.GetComponent<RectTransform>();
        chevron.anchorMin = chevron.anchorMax = new Vector2(0.5f, 0f);
        chevron.pivot = new Vector2(0.5f, 0.5f);
        chevron.sizeDelta = new Vector2(1f, 1f);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject arm = new GameObject("Arm", typeof(RectTransform), typeof(Image));
            arm.transform.SetParent(holder.transform, false);
            Image ai = arm.GetComponent<Image>();
            ai.color = new Color(1f, 1f, 1f, 0.55f);
            ai.raycastTarget = false;
            RectTransform ar = arm.GetComponent<RectTransform>();
            ar.sizeDelta = new Vector2(22f, 4f);
            ar.anchoredPosition = new Vector2(side * 8f, 0f);
            ar.localRotation = Quaternion.Euler(0f, 0f, -side * 38f);
        }
    }

    public void OnPointerEnter(PointerEventData e) { hovering = true; }
    public void OnPointerExit(PointerEventData e) { hovering = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
}
