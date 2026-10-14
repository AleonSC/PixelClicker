// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopNudge.cs (+ PixelGuideVendor.ShopHold)
//   Shop hold after Cubie's second visit (added by PixelClicker.Awake): if the shop tutorial is still due (PixelShopTutorial.DoneKey,
//   tips on) the visit does NOT unfreeze time when he leaves: PixelGuideVendor.ShopHold keeps Time.timeScale 0 (UpdateHold re-freezes
//   after a pause-menu resume) until the shop window opens (PixelShop.WindowOpen), then time resumes and
//   PixelShopTutorial.RequestStart() starts the tutorial without its start delay (leaves the hold on the title screen / tips off).
//   While held the Shop dock button stays slid out (ShopHold in its keep-out lambda), the tier guide line (PixelUI.UpdateGuide, forced
//   to shopGuideText) pulses gold and PixelShopNudge (canvas 640) draws a pulsing gold frame with spreading rings round the Shop
//   button (PixelShop.ShopButtonRect), a bobbing 'Open the Shop!' label and chevrons (7) flowing from the guide line
//   (PixelUI.GuideRect) to the button; disableNudge.
// ---------------------------------------------------
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
    [SerializeField, Tooltip("The colour of the soft glow round the Shop button.")]
    private Color glowColor = new Color(1f, 0.82f, 0.25f, 1f);

    private PixelShop shop;
    private GameObject canvasObject;
    private Canvas canvas;
    private Image frame;
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
        float beat = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
        frame.rectTransform.anchoredPosition = target;
        frame.rectTransform.sizeDelta = size + Vector2.one * (10f + 6f * beat);
        frame.color = new Color(glowColor.r, glowColor.g, glowColor.b, (0.18f + 0.17f * beat) * visible);   // a soft, slow glow (Cubie does the pointing)
    }

    private static Vector2 ScreenOf(RectTransform rt)
    {
        Vector3 world = rt.TransformPoint(rt.rect.center);
        return RectTransformUtility.WorldToScreenPoint(null, world);
    }

    private void Build()
    {
        canvasObject = PixelUIKit.CreateCanvas("Shop Nudge", 640, new Vector2(1920f, 1080f), false);
        canvas = canvasObject.GetComponent<Canvas>();

        frame = NewFrame("Frame");
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
