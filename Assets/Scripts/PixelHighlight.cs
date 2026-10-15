// ---- System notes ----
// PixelHighlight.cs
//   Shared pulsing gold frame round ANY UI element (the Shop button's highlight, made reusable): PixelHighlight.Set(owner, rect, colour)
//   frames 'rect' on its own canvas (sorting order 615: above the windows, below the tip boxes at 620), Clear(owner) fades it out (only the
//   owner that set it can clear it). One frame at a time; it follows the rect every frame (windows popping in / moving) and hides while the
//   rect is inactive. Used by PixelShopNudge (the Shop button while the shop hold is on) and PixelSkills' perk popups (the skill's card,
//   e.g. Standard level 2). Created on demand, no scene setup.
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

/// <summary>A soft, slowly pulsing gold frame drawn round one UI element to point the player at it (PixelHighlight.Set / Clear).</summary>
public class PixelHighlight : MonoBehaviour
{
    [Tooltip("The frame's colour when Set is called without one.")]
    [SerializeField] private Color defaultColor = new Color(1f, 0.82f, 0.25f, 1f);
    [Tooltip("Sorting order of the frame's canvas (above the windows, below the tip boxes at 620).")]
    [SerializeField] private int sortingOrder = 615;
    [Tooltip("How far the frame reaches past the element's edges (canvas units), before the pulse.")]
    [SerializeField] private float padding = 10f;
    [Tooltip("Extra size the pulse adds to the frame (canvas units).")]
    [SerializeField] private float pulseGrow = 6f;
    [Tooltip("Pulses per second times 2 pi (the beat's speed).")]
    [SerializeField] private float pulseSpeed = 2.2f;

    private static PixelHighlight instance;
    private object owner;
    private RectTransform target;
    private Color color;
    private GameObject canvasObject;
    private Canvas canvas;
    private Image frame;
    private float visible;

    /// <summary>Frames 'rect' (replacing any other highlight). 'who' is the owner: only it can Clear the frame again.</summary>
    public static void Set(object who, RectTransform rect, Color? tint = null)
    {
        if (instance == null) instance = new GameObject("Pixel Highlight").AddComponent<PixelHighlight>();
        instance.owner = who;
        instance.target = rect;
        instance.color = tint ?? instance.defaultColor;
    }

    /// <summary>Fades the frame out if 'who' set it.</summary>
    public static void Clear(object who)
    {
        if (instance != null && instance.owner == who) { instance.owner = null; instance.target = null; }
    }

    /// <summary>Is 'who' highlighting something right now?</summary>
    public static bool IsSetBy(object who) => instance != null && who != null && instance.owner == who;

    private void Update()
    {
        bool want = target != null && target.gameObject.activeInHierarchy;
        visible = Mathf.MoveTowards(visible, want ? 1f : 0f, Time.unscaledDeltaTime * (want ? 2.5f : 6f));
        if (visible <= 0f)
        {
            if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false);
            return;
        }
        if (canvasObject == null) Build();
        if (!canvasObject.activeSelf) canvasObject.SetActive(true);
        if (target == null || !target.gameObject.activeInHierarchy) { frame.color = new Color(color.r, color.g, color.b, 0.3f * visible); return; }   // fading out where it was

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        Vector3 world = target.TransformPoint(target.rect.center);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
        Vector2 size = target.rect.size * (Vector2)target.lossyScale / scale;
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed);
        frame.rectTransform.anchoredPosition = screen / scale;
        frame.rectTransform.sizeDelta = size + Vector2.one * (padding + pulseGrow * beat);
        frame.color = new Color(color.r, color.g, color.b, (0.18f + 0.17f * beat) * visible);
    }

    private void Build()
    {
        canvasObject = PixelUIKit.CreateCanvas("Pixel Highlight", sortingOrder, new Vector2(1920f, 1080f), false);
        canvas = canvasObject.GetComponent<Canvas>();
        GameObject go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasObject.transform, false);
        frame = go.GetComponent<Image>();
        frame.raycastTarget = false;
        PixelUIKit.StyleBox(frame, true);
        RectTransform r = frame.rectTransform;
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(200f, 80f);
    }

    private void OnDestroy() { if (instance == this) instance = null; }
}
