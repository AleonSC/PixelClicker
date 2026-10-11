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
    public float fadeHeight = 44f;

    [Tooltip("Never show the bar itself (the bottom fade and the mouse wheel still work).")]
    public bool hideBar;

    private CanvasGroup group;
    private ScrollRect scroll;
    private bool lookedForScroll;
    private RectMask2D softMask;
    private int appliedSoft = -1;
    private float fadeAmount;
    private RectTransform chevron;
    private CanvasGroup chevronGroup;
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
        group.alpha = hideBar ? 0f : Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        if (hideBar) group.blocksRaycasts = false;
        UpdateBottomFade();
    }

    /// <summary>
    /// The content itself fades into nothing at the bottom of a list that scrolls (the mask's soft edge, so the window and whatever sits behind the list are untouched),
    /// with a little bobbing chevron below it. Both go away when the list fits or is scrolled to the end. The mask is widened upwards by the same amount, so the first
    /// row is never faded at the top.
    /// </summary>
    private void UpdateBottomFade()
    {
        if (!lookedForScroll)
        {
            lookedForScroll = true;
            foreach (ScrollRect sr in GetComponentsInParent<ScrollRect>(true)) if (sr.verticalScrollbar == bar) { scroll = sr; break; }
            if (scroll == null && transform.parent != null)
                foreach (ScrollRect sr in transform.parent.GetComponentsInChildren<ScrollRect>(true)) if (sr.verticalScrollbar == bar) { scroll = sr; break; }
            if (scroll != null && scroll.GetComponentInParent<TMPro.TMP_Dropdown>() == null)
            {
                RectTransform vp = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
                softMask = vp.GetComponent<RectMask2D>();
                if (softMask != null) BuildChevron(vp);
            }
        }
        if (softMask == null || scroll == null || scroll.content == null) return;

        RectTransform view = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        bool scrolls = scroll.content.rect.height > view.rect.height + 1f;
        bool more = scrolls && scroll.verticalNormalizedPosition > 0.02f;
        fadeAmount = Mathf.MoveTowards(fadeAmount, more ? 1f : 0f, Time.unscaledDeltaTime / 0.2f);

        int soft = Mathf.RoundToInt(fadeHeight * fadeAmount);
        if (soft != appliedSoft)
        {
            appliedSoft = soft;
            softMask.softness = new Vector2Int(0, soft);
            softMask.padding = new Vector4(0f, 0f, 0f, -soft);   // widened upwards (z = right, w = top): nothing is faded at the top edge
        }

        if (chevron != null)
        {
            chevron.gameObject.SetActive(fadeAmount > 0.01f);
            if (fadeAmount > 0.01f)
            {
                Vector3[] corners = new Vector3[4];
                view.GetWorldCorners(corners);
                float scale = view.lossyScale.y;
                chevronGroup.alpha = fadeAmount;
                chevron.position = new Vector3((corners[0].x + corners[2].x) * 0.5f, corners[0].y + (16f + Mathf.Sin(Time.unscaledTime * 3.2f) * 5f) * scale, corners[0].z);
            }
        }
    }

    private void BuildChevron(RectTransform view)
    {
        Transform parent = view.parent != null ? view.parent : view;   // outside the soft mask, so the arrow itself isn't faded
        GameObject holder = new GameObject("Scroll Chevron", typeof(RectTransform), typeof(CanvasGroup));
        holder.transform.SetParent(parent, false);
        holder.transform.SetAsLastSibling();
        chevron = holder.GetComponent<RectTransform>();
        chevronGroup = holder.GetComponent<CanvasGroup>();
        chevronGroup.blocksRaycasts = false;
        chevronGroup.interactable = false;
        chevron.anchorMin = chevron.anchorMax = new Vector2(0.5f, 0.5f);
        chevron.pivot = new Vector2(0.5f, 0.5f);
        chevron.sizeDelta = new Vector2(1f, 1f);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject arm = new GameObject("Arm", typeof(RectTransform), typeof(Image));
            arm.transform.SetParent(holder.transform, false);
            Image ai = arm.GetComponent<Image>();
            ai.color = new Color(1f, 1f, 1f, 0.6f);
            ai.raycastTarget = false;
            RectTransform ar = arm.GetComponent<RectTransform>();
            ar.sizeDelta = new Vector2(22f, 4f);
            ar.anchoredPosition = new Vector2(side * 8f, 0f);
            ar.localRotation = Quaternion.Euler(0f, 0f, -side * 38f);
        }
        holder.SetActive(false);
    }

    private void OnDestroy() { if (chevron != null) Destroy(chevron.gameObject); }

    public void OnPointerEnter(PointerEventData e) { hovering = true; }
    public void OnPointerExit(PointerEventData e) { hovering = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
}
