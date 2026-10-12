using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Every time the button it sits on is pressed, a neon outline in the button's own colour comes out from under it: a brightened copy of the
/// button's picture is placed just behind it, grows outwards and fades away, so only a glowing rim shows round the edge. Unscaled time, so it
/// plays while the game is paused. Added to the shop's buttons by <see cref="PixelShop"/>.
/// </summary>
[RequireComponent(typeof(Button))]
public class PixelButtonPulse : MonoBehaviour
{
    /// <summary>Turn the effect off for every button (a player switch could drive this later).</summary>
    public static bool Disabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Disabled = false; }

    private const float Seconds = 0.4f;
    private const float Grow = 0.22f;     // the rim ends up this much bigger than the button (each side, as a fraction of its size)

    private Button button;
    private Image source;

    private void Awake()
    {
        button = GetComponent<Button>();
        source = GetComponent<Image>();
        button.onClick.AddListener(Pulse);
    }

    private void Pulse()
    {
        if (Disabled || source == null || !isActiveAndEnabled) return;
        RectTransform rect = (RectTransform)transform;
        Transform parent = rect.parent;
        if (parent == null) return;

        Color c = source.color;
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        // Neon: stretch the colour so its brightest channel is full, then lighten it a little.
        Color neon = peak > 0.01f ? new Color(c.r / peak, c.g / peak, c.b / peak, 1f) : Color.white;
        neon = Color.Lerp(neon, Color.white, 0.2f);

        GameObject go = new GameObject("Button Pulse", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = rect.anchorMin; r.anchorMax = rect.anchorMax; r.pivot = rect.pivot;
        r.anchoredPosition = rect.anchoredPosition;
        r.sizeDelta = rect.sizeDelta;
        r.localRotation = rect.localRotation;
        r.localScale = rect.localScale;
        go.transform.SetSiblingIndex(rect.GetSiblingIndex());   // directly behind the button
        Image im = go.GetComponent<Image>();
        im.sprite = source.sprite;
        im.type = source.type;
        im.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        im.raycastTarget = false;
        im.color = new Color(neon.r, neon.g, neon.b, 0.9f);

        PixelButtonPulseRunner run = go.AddComponent<PixelButtonPulseRunner>();
        run.Begin(im, rect, neon, Seconds, Grow);
    }
}

/// <summary>Plays one pulse: grows the rim and fades it, then destroys itself. Lives on the pulse object so it ends with it.</summary>
public class PixelButtonPulseRunner : MonoBehaviour
{
    private Image image;
    private RectTransform rect, follow;
    private Color colour;
    private float seconds, grow, t;
    private Vector2 baseSize;

    public void Begin(Image im, RectTransform buttonRect, Color neon, float duration, float growth)
    {
        image = im; follow = buttonRect; colour = neon; seconds = duration; grow = growth;
        rect = (RectTransform)transform;
        baseSize = rect.sizeDelta;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(t / seconds);
        float e = 1f - (1f - k) * (1f - k);                    // eases out
        if (follow != null) { rect.anchoredPosition = follow.anchoredPosition; }   // stays under the button if it moves (a scrolling list)
        rect.sizeDelta = baseSize + new Vector2(baseSize.x, baseSize.y) * (grow * e * 2f) * 0.5f + Vector2.one * (10f * e);
        image.color = new Color(colour.r, colour.g, colour.b, 0.9f * (1f - k) * (1f - k));
        if (k >= 1f) Destroy(gameObject);
    }
}
