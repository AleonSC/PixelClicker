using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the "ui_scroll" sound (PixelAudio) while a scroll view is being scrolled - mouse wheel or dragging its scroll
/// bar - as a tick every time the content has moved a set distance. Only counts while the mouse is over the view and
/// only when the content is actually taller than the view, so content that is re-laid out by the game stays silent.
/// Added automatically to every scroll view the game builds.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class PixelScrollSound : MonoBehaviour
{
    [Min(1f)]
    [Tooltip("Distance the content must move (canvas units) for one tick of sound.")]
    [SerializeField] private float tickDistance = 45f;

    private ScrollRect scroll;
    private float lastY;
    private float travelled;

    private void Awake()
    {
        scroll = GetComponent<ScrollRect>();
        scroll.onValueChanged.AddListener(OnScrolled);
    }

    private void OnEnable()
    {
        lastY = scroll != null && scroll.content != null ? scroll.content.anchoredPosition.y : 0f;
        travelled = 0f;
    }

    private void OnDestroy()
    {
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnScrolled);
    }

    private void OnScrolled(Vector2 position)
    {
        if (scroll.content == null) return;
        RectTransform view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform; // no viewport set = the scroll view itself

        float y = scroll.content.anchoredPosition.y;
        float moved = Mathf.Abs(y - lastY);
        lastY = y;

        bool scrollable = scroll.content.rect.height > view.rect.height + 0.5f;
        bool pointerOver = RectTransformUtility.RectangleContainsScreenPoint(view, PixelInput.PointerPosition(), null);
        if (!scrollable || !pointerOver) { travelled = 0f; return; }

        travelled += moved;
        if (travelled < tickDistance) return;
        travelled = 0f;
        PixelAudio.Play("ui_scroll");
    }
}
