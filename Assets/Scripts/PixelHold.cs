using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Hold the right mouse button" helper with a filling meter next to the pointer. Used to remove placed devices and to
/// cancel the running potion. Each frame the owner says what the pointer is on (or null); the meter fills while the
/// right button stays down on that same target, starts over if the pointer moves off it, and Update returns true on
/// the frame the hold completes. Uses unscaled time, so it also works while time is stopped.
/// </summary>
public static class PixelHold
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        ownerCaller = null;
        ownerTarget = null;
        held = 0f;
        meter = null;
    }

    private static object ownerCaller;
    private static object ownerTarget;
    private static float held;
    private static PixelHoldMeter meter;

    /// <summary>Call every frame. 'target' = what the pointer is over right now (null = nothing). True when the hold completes.</summary>
    public static bool Update(object caller, object target, float seconds, string label, Color color)
    {
        if (target == null || PixelPauseMenu.IsPaused)
        {
            if (ownerCaller == caller) Reset();
            return false;
        }

        if (ownerCaller != caller || ownerTarget != target)
        {
            // A hold only starts with a fresh right-press on the target.
            if (!PixelInput.RightPressed())
            {
                if (ownerCaller == caller) Reset();
                return false;
            }
            Reset();
            ownerCaller = caller;
            ownerTarget = target;
        }

        if (!PixelInput.RightHeld())
        {
            Reset();
            return false;
        }

        held += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(held / Mathf.Max(0.05f, seconds));
        if (held >= seconds)
        {
            Reset();
            return true;
        }

        if (meter == null) meter = new GameObject("Hold Meter").AddComponent<PixelHoldMeter>();
        meter.Show(PixelInput.PointerPosition(), progress, label, color);
        return false;
    }

    private static void Reset()
    {
        ownerCaller = null;
        ownerTarget = null;
        held = 0f;
        if (meter != null) meter.Hide();
    }
}

/// <summary>The little bar that follows the pointer while a right-hold is in progress.</summary>
public class PixelHoldMeter : MonoBehaviour
{
    private const float BarWidth = 240f;
    private const float BarHeight = 22f;

    private GameObject canvasRoot;
    private RectTransform canvasRect, barRect, fill;
    private TMP_Text label;
    private Image fillImage;

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;

        canvasRoot = PixelUIKit.CreateCanvas("Hold Meter Canvas", 650, new Vector2(1920f, 1080f), false);
        canvasRoot.transform.SetParent(transform, false);
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(canvasRoot.transform, false);
        bar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        bar.GetComponent<Image>().raycastTarget = false;
        barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(BarWidth, BarHeight);

        GameObject f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(bar.transform, false);
        fillImage = f.GetComponent<Image>();
        fillImage.raycastTarget = false;
        fill = f.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        fill.pivot = new Vector2(0f, 0.5f);

        label = PixelUIKit.CreateText(font, bar.transform, "Label", "", 24f, TextAlignmentOptions.Bottom, FontStyles.Bold, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.sizeDelta = new Vector2(0f, 34f);
        lr.anchoredPosition = new Vector2(0f, 2f);
    }

    public void Show(Vector2 screenPosition, float progress, string text, Color color)
    {
        if (canvasRoot == null) Build();
        canvasRoot.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local);
        barRect.anchoredPosition = local + new Vector2(0f, -34f);

        fillImage.color = color;
        fill.anchorMax = new Vector2(progress, 1f);
        label.text = text;
    }

    public void Hide()
    {
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }
}
