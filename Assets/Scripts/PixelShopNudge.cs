// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopNudge.cs (+ PixelGuideVendor.ShopHold)
//   Shop hold after Cubie's second visit (added by PixelClicker.Awake): if the shop tutorial is still due (PixelShopTutorial.DoneKey,
//   tips on) the visit does NOT unfreeze time when he leaves: PixelGuideVendor.ShopHold keeps Time.timeScale 0 (UpdateHold re-freezes
//   after a pause-menu resume) until the shop window opens (PixelShop.WindowOpen), then time resumes and
//   PixelShopTutorial.RequestStart() starts the tutorial without its start delay (leaves the hold on the title screen / tips off).
//   While held the Shop dock button stays slid out (ShopHold in its keep-out lambda), the tier guide line (PixelUI.UpdateGuide, forced
//   to shopGuideText) pulses gold and the Shop button (PixelShop.ShopButtonRect) gets the shared pulsing gold frame (PixelHighlight.Set,
//   glowColor); disableNudge.
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// The nudge toward the Shop (while <see cref="PixelGuideVendor.ShopHold"/> is on, i.e. time stays frozen after Cubie's second visit until the player opens the shop):
/// the Shop button gets the shared pulsing gold frame (<see cref="PixelHighlight"/>). Clicking the Shop button ends the hold and starts the shop tutorial.
/// </summary>
public class PixelShopNudge : MonoBehaviour
{
    [Header("Shop nudge")]
    [SerializeField, Tooltip("Turns the whole nudge off (time still stays frozen until the shop is opened).")]
    private bool disableNudge;
    [SerializeField, Tooltip("The colour of the soft glow round the Shop button.")]
    private Color glowColor = new Color(1f, 0.82f, 0.25f, 1f);

    private PixelShop shop;

    private void Update()
    {
        bool want = !disableNudge && PixelGuideVendor.ShopHold;
        if (want && shop == null) shop = PixelFind.First<PixelShop>();
        RectTransform button = want && shop != null ? shop.ShopButtonRect : null;
        if (button != null) PixelHighlight.Set(this, button, glowColor);
        else PixelHighlight.Clear(this);
    }
}
