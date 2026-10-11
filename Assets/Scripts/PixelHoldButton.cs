using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Put on a UI button to make it a "hold to use" button: the button's own click is ignored, and while the pointer stays pressed on it a bright
/// bar fills over <see cref="holdSeconds"/>; when it is full <see cref="onComplete"/> runs. Letting go or moving off resets it, so a stray click does nothing.
/// Uses unscaled time (the shop works while time is stopped).
/// </summary>
public class PixelHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float holdSeconds = 0.7f;
    public System.Action onComplete;

    private RectTransform fill;
    private bool holding;
    private float held;

    private void Awake()
    {
        GameObject f = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(transform, false);
        fill = f.GetComponent<RectTransform>();
        Image img = f.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.45f);
        img.raycastTarget = false;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.SetSiblingIndex(1);   // above the button's face, under its label
        fill.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { holding = true; held = 0f; fill.gameObject.SetActive(true); } }
    public void OnPointerUp(PointerEventData e) { Stop(); }
    public void OnPointerExit(PointerEventData e) { Stop(); }
    private void OnDisable() { Stop(); }

    private void Stop()
    {
        holding = false;
        held = 0f;
        if (fill != null) { fill.anchorMax = new Vector2(0f, 1f); fill.gameObject.SetActive(false); }
    }

    private void Update()
    {
        if (!holding) return;
        held += Time.unscaledDeltaTime;
        fill.anchorMax = new Vector2(Mathf.Clamp01(held / Mathf.Max(0.05f, holdSeconds)), 1f);
        if (held >= holdSeconds)
        {
            Stop();
            onComplete?.Invoke();
        }
    }
}
