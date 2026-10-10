using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The animated swatch of the Ultra pixel row in the Inventory: a rounded chip whose colour slowly cycles through the rainbow,
/// with a soft glow breathing behind it, a bright diagonal shine that sweeps across now and then, and a few twinkling sparkles.
/// Put it on the swatch Image (it adds its own mask, glow, shine and sparkles). Uses unscaled time, so it keeps moving while paused.
/// </summary>
public class PixelUltraSwatch : MonoBehaviour
{
    private Image image;
    private Image glow;
    private RectTransform shine;
    private Image shineImage;
    private Image[] sparkles;
    private float[] sparkleOffset;
    private RectTransform rect;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        Mask mask = gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // A glow behind the chip (a sibling placed just before it, so the mask doesn't clip it).
        GameObject glowGo = new GameObject("Ultra Glow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(transform.parent, false);
        glowGo.transform.SetSiblingIndex(transform.GetSiblingIndex());
        glow = glowGo.GetComponent<Image>();
        glow.sprite = image.sprite;
        glow.type = image.type;
        glow.raycastTarget = false;
        RectTransform gr = glowGo.GetComponent<RectTransform>();
        gr.anchorMin = rect.anchorMin; gr.anchorMax = rect.anchorMax; gr.pivot = rect.pivot;
        gr.sizeDelta = rect.sizeDelta + new Vector2(10f, 10f);
        gr.anchoredPosition = rect.anchoredPosition;

        GameObject shineGo = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        shineGo.transform.SetParent(transform, false);
        shineImage = shineGo.GetComponent<Image>();
        shineImage.raycastTarget = false;
        shine = shineGo.GetComponent<RectTransform>();
        shine.sizeDelta = new Vector2(rect.sizeDelta.x * 0.3f, rect.sizeDelta.y * 1.8f);
        shine.localRotation = Quaternion.Euler(0f, 0f, 22f);

        sparkles = new Image[3];
        sparkleOffset = new float[3];
        for (int i = 0; i < sparkles.Length; i++)
        {
            GameObject sp = new GameObject("Sparkle " + i, typeof(RectTransform), typeof(Image));
            sp.transform.SetParent(transform, false);
            sparkles[i] = sp.GetComponent<Image>();
            sparkles[i].raycastTarget = false;
            RectTransform sr = sp.GetComponent<RectTransform>();
            float size = Mathf.Max(3f, rect.sizeDelta.x * 0.14f);
            sr.sizeDelta = new Vector2(size, size);
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(Mathf.Lerp(-0.28f, 0.28f, (i * 0.37f) % 1f) * rect.sizeDelta.x, Mathf.Lerp(-0.28f, 0.28f, (i * 0.61f + 0.2f) % 1f) * rect.sizeDelta.y);
            sr.localRotation = Quaternion.Euler(0f, 0f, 45f);
            sparkleOffset[i] = i * 1.7f;
        }
    }

    private void OnDestroy()
    {
        if (glow != null) Destroy(glow.gameObject);
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        Color c = Color.HSVToRGB(Mathf.Repeat(t * 0.22f, 1f), 0.62f, 1f);
        image.color = c;

        if (glow != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            glow.color = new Color(c.r, c.g, c.b, 0.25f + 0.3f * pulse);
        }

        // The shine sweeps left to right every ~2.2 s, then rests.
        float cycle = Mathf.Repeat(t, 2.2f) / 1.0f;
        float sweep = Mathf.Clamp01(cycle);                       // 0..1 over the first second, then parked off to the right
        float w = rect.sizeDelta.x;
        shine.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.9f, w * 0.9f, sweep), 0f);
        shineImage.color = new Color(1f, 1f, 1f, cycle < 1f ? 0.55f : 0f);

        for (int i = 0; i < sparkles.Length; i++)
        {
            float tw = Mathf.Max(0f, Mathf.Sin(t * 3.1f + sparkleOffset[i]));
            sparkles[i].color = new Color(1f, 1f, 1f, tw * tw);
        }
    }
}
