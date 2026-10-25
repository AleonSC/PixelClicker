// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopNudge.cs (+ PixelGuideVendor.ShopHold)
//   Shop nudge after Cubie's second visit (added by PixelClicker.Awake): until the shop window is opened for the first time
//   (PixelShopTutorial.OpenedKey, per save; tips on) PixelGuideVendor.ShopHold is on - time keeps running (no freeze since 2026-10-25).
//   Meanwhile the Shop dock button stays slid out (ShopHold in its keep-out lambda), Cubie peeks under it pointing his stick up
//   (PixelGuideVendor.PeekRoutine) and the Shop button (PixelShop.ShopButtonRect) gets the shared pulsing gold frame (PixelHighlight.Set,
//   glowColor; disableNudge). The first opening offers the shop tour (PixelShopTutorial).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// The nudge toward the Shop (while <see cref="PixelGuideVendor.ShopHold"/> is on, i.e. after Cubie's second visit until the shop is first opened):
/// the Shop button gets the shared pulsing gold frame (<see cref="PixelHighlight"/>). Opening the shop ends it and offers the tour.
/// </summary>
public class PixelShopNudge : MonoBehaviour
{
    [Header("Shop nudge")]
    [SerializeField, Tooltip("Turns the gold frame off (Cubie still peeks under the Shop button until it is first opened).")]
    private bool disableNudge;
    [SerializeField, Tooltip("The colour of the soft glow round the Shop button.")]
    private Color glowColor = new Color(1f, 0.82f, 0.25f, 1f);

    private PixelShop shop;

    private void Update()
    {
        bool want = !disableNudge && PixelGuideVendor.ShopHold;
        if (want && shop == null) shop = PixelFind.Cached<PixelShop>();
        RectTransform button = want && shop != null ? shop.ShopButtonRect : null;
        if (button != null) PixelHighlight.Set(this, button, glowColor);
        else PixelHighlight.Clear(this);
    }
}
