using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The nudge toward the Shop (while <see cref="PixelGuideVendor.ShopHold"/> is on, i.e. time stays frozen after Cubie's second visit until the player opens the shop):
/// a stream of gold chevrons flows from the pulsing 'Buy the RGB Pack in the shop' line to the Shop button, the button gets a pulsing gold frame with rings
/// spreading out of it, and a bobbing 'Open the Shop!' label sits under it. Clicking the Shop button ends the hold and starts the shop tutorial.
/// </summary>
public class PixelShopNudge : MonoBehaviour
{
    [Header("Shop nudge")]
    [SerializeField, Tooltip("Turns the whole nudge off (time still stays frozen until the shop is opened).")]
    private bool disableNudge;
    [SerializeField, Tooltip("How many chevrons flow from the guide text to the Shop button. 0 = 7.")]
    private int chevrons;
    [SerializeField, Tooltip("How fast the chevrons flow (trips per second). 0 = 0.55.")]
    private float flowSpeed;
    [SerializeField, Tooltip("Text under the Shop button.")]
    private string labelText = "Open the Shop!";
    [SerializeField, Tooltip("The colour of the glow, rings and chevrons.")]
    private Color glowColor = new Color(1f, 0.82f, 0.25f, 1f);

    private const int RingCount = 3;

    private PixelShop shop;
    private PixelUI ui;
    private GameObject canvasObject;
    private Canvas canvas;
    private Image frame;
    private readonly Image[] rings = new Image[RingCount];
    private TMP_Text[] arrows;
    private TMP_Text label;
    private float visible;

    private void Update()
    {
        bool want = !disableNudge && PixelGuideVendor.ShopHold;
        visible = Mathf.MoveTowards(visible, want ? 1f : 0f, Time.unscaledDeltaTime * (want ? 2.5f : 6f));
        if (visible <= 0f)
        {
            if (canvasObject != null && canvasObject.activeSelf) canvasObject.SetActive(false);
            return;
        }
        if (shop == null) shop = PixelFind.First<PixelShop>();
        if (ui == null) ui = PixelFind.First<PixelUI>();
        RectTransform button = shop != null ? shop.ShopButtonRect : null;
        if (button == null) return;
        if (canvasObject == null) Build();
        if (!canvasObject.activeSelf) canvasObject.SetActive(true);

        float scale = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
        float t = Time.unscaledTime;
        Vector2 target = ScreenOf(button) / scale;
        Vector2 size = button.rect.size * button.lossyScale;
        size /= scale;

        // The pulsing frame round the Shop button.
        float beat = 0.5f + 0.5f * Mathf.Sin(t * 5f);
        frame.rectTransform.anchoredPosition = target;
        frame.rectTransform.sizeDelta = size + Vector2.one * (16f + 10f * beat);
        frame.color = new Color(glowColor.r, glowColor.g, glowColor.b, (0.45f + 0.5f * beat) * visible);

        // Rings spreading out of it.
        for (int i = 0; i < RingCount; i++)
        {
            float k = Mathf.Repeat(t * 0.8f + i / (float)RingCount, 1f);
            rings[i].rectTransform.anchoredPosition = target;
            rings[i].rectTransform.sizeDelta = size + Vector2.one * (20f + 110f * k);
            rings[i].color = new Color(glowColor.r, glowColor.g, glowColor.b, (1f - k) * 0.7f * visible);
        }

        // The label under the button.
        label.rectTransform.anchoredPosition = target + new Vector2(0f, -size.y * 0.5f - 34f + Mathf.Sin(t * 4f) * 6f);
        label.color = new Color(glowColor.r, glowColor.g, glowColor.b, visible);

        // Chevrons flowing from the guide line to the button.
        RectTransform guide = ui != null ? ui.GuideRect : null;
        bool haveGuide = guide != null;
        Vector2 start = haveGuide ? ScreenOf(guide) / scale : target + new Vector2(-500f, -300f);
        Vector2 dir = target - start;
        float length = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float speed = flowSpeed > 0f ? flowSpeed : 0.55f;
        for (int i = 0; i < arrows.Length; i++)
        {
            float k = Mathf.Repeat(t * speed + i / (float)arrows.Length, 1f);
            float fade = Mathf.Sin(k * Mathf.PI);
            // The stream leaves the line's edge and stops short of the button.
            Vector2 p = Vector2.Lerp(start + dir.normalized * 90f, target - dir.normalized * (size.magnitude * 0.5f + 28f), k);
            if (length < 150f) fade = 0f;
            arrows[i].rectTransform.anchoredPosition = p;
            arrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            arrows[i].rectTransform.localScale = Vector3.one * (0.8f + 0.5f * fade);
            arrows[i].color = new Color(glowColor.r, glowColor.g, glowColor.b, fade * visible);
        }
    }

    private static Vector2 ScreenOf(RectTransform rt)
    {
        Vector3 world = rt.TransformPoint(rt.rect.center);
        return RectTransformUtility.WorldToScreenPoint(null, world);
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;
        canvasObject = PixelUIKit.CreateCanvas("Shop Nudge", 640, new Vector2(1920f, 1080f), false);
        canvas = canvasObject.GetComponent<Canvas>();

        for (int i = 0; i < RingCount; i++) rings[i] = NewFrame("Ring " + i);
        frame = NewFrame("Frame");

        int count = chevrons > 0 ? chevrons : 7;
        arrows = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            TMP_Text a = PixelUIKit.CreateText(font, canvasObject.transform, "Chevron " + i, ">", 64f, TextAlignmentOptions.Center, FontStyles.Bold, glowColor);
            Anchor(a.rectTransform, new Vector2(60f, 70f));
            arrows[i] = a;
        }
        label = PixelUIKit.CreateText(font, canvasObject.transform, "Label", labelText, 34f, TextAlignmentOptions.Center, FontStyles.Bold, glowColor);
        Anchor(label.rectTransform, new Vector2(500f, 50f));
        canvasObject.SetActive(false);
    }

    private Image NewFrame(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasObject.transform, false);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        PixelUIKit.StyleBox(image, true);
        Anchor(image.rectTransform, new Vector2(200f, 80f));
        return image;
    }

    private static void Anchor(RectTransform r, Vector2 size)
    {
        r.anchorMin = r.anchorMax = Vector2.zero;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
    }
}
