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

    private CanvasGroup group;
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
    }

    public void OnPointerEnter(PointerEventData e) { hovering = true; }
    public void OnPointerExit(PointerEventData e) { hovering = false; }
    public void OnPointerDown(PointerEventData e) { pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
}
