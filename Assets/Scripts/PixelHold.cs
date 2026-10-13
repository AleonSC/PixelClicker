// ---- System notes (moved here from CLAUDE.md) ----
// PixelHold.cs
//   PixelHold.Update(caller, target, seconds, label, colour, circle, buildSound, useLeft): hold-the-right (or, useLeft, the LEFT) mouse-button helper with a filling bar next to the
//   pointer (PixelHoldMeter, built lazily) or, with circle = true, ONLY a ring that fills round the cursor (no text) and a rising 'buildup' sound that stops when the hold ends
//   (PixelHold.ProgressOf(target) lets the owner react, e.g. the removed device glows red); true when the hold completes. Used by PixelConsumables.UpdateRemoval (hold on a placed
//   device -> PixelPlacedDevice.RemoveNow(), no refund; hit-test = renderer bounds, ignoring the range disc / blow area) and
//   PixelUI.UpdateActiveHud (hold on the active-potion line -> PixelConsumables.CancelActive). Settings (removeHoldSeconds, texts,
//   colour) are in PixelConsumables; sound device_remove.
// ---------------------------------------------------
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
        lastProgress = 0f;
        playingSound = null;
        meter = null;
    }

    private static object ownerCaller;
    private static object ownerTarget;
    private static float held;
    private static float lastProgress;
    private static string playingSound;
    private static PixelHoldMeter meter;

    /// <summary>How far the hold on 'target' has got right now (0..1; 0 when nothing is being held on it).</summary>
    public static float ProgressOf(object target) => target != null && ownerTarget == target ? lastProgress : 0f;

    /// <summary>Call every frame. 'target' = what the pointer is over right now (null = nothing). True when the hold completes.</summary>
    public static bool Update(object caller, object target, float seconds, string label, Color color, bool circle = false, string buildSound = null, bool useLeft = false, Vector2? at = null)
    {
        if (target == null || PixelPauseMenu.IsPaused)
        {
            if (ownerCaller == caller) Reset();
            return false;
        }

        if (ownerCaller != caller || ownerTarget != target)
        {
            // A hold only starts with a fresh right-press on the target.
            if (!(useLeft ? PixelInput.LeftPressed() : PixelInput.RightPressed()))
            {
                if (ownerCaller == caller) Reset();
                return false;
            }
            Reset();
            ownerCaller = caller;
            ownerTarget = target;
            if (!string.IsNullOrEmpty(buildSound))
            {
                playingSound = buildSound;
                PixelAudio.Play(buildSound, PixelSynth.BuildupSeconds / Mathf.Max(0.05f, seconds));   // the clip is stretched to last as long as the hold
            }
        }

        if (!(useLeft ? PixelInput.LeftHeld() : PixelInput.RightHeld()))
        {
            Reset();
            return false;
        }

        held += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(held / Mathf.Max(0.05f, seconds));
        lastProgress = progress;
        if (held >= seconds)
        {
            Reset();
            return true;
        }

        if (meter == null) meter = new GameObject("Hold Meter").AddComponent<PixelHoldMeter>();
        meter.Show(at ?? PixelInput.PointerPosition(), progress, label, color, circle);
        return false;
    }

    private static void Reset()
    {
        ownerCaller = null;
        ownerTarget = null;
        held = 0f;
        lastProgress = 0f;
        if (playingSound != null) { PixelAudio.Stop(playingSound); playingSound = null; }
        if (meter != null) meter.Hide();
    }
}

/// <summary>The little bar that follows the pointer while a right-hold is in progress.</summary>
public class PixelHoldMeter : MonoBehaviour
{
    private const float BarWidth = 240f;
    private const float BarHeight = 22f;

    private GameObject canvasRoot;
    private RectTransform canvasRect, barRect, fill, ringRect;
    private TMP_Text label;
    private Image fillImage, ringFill;
    private static Sprite ringSprite;

    private const float RingSize = 72f;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRing() { ringSprite = null; }

    /// <summary>A white ring (soft edges), the shape the radial fill is cut from.</summary>
    private static Sprite Ring()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "HoldRing", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);   // 0 centre .. 1 edge
                float outer = Mathf.Clamp01((1f - d) * 0.5f * n);
                float inner = Mathf.Clamp01((d - 0.74f) * 0.5f * n);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Min(outer, inner));
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return ringSprite;
    }

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

        // The ring: a dark ring with a coloured ring filling clockwise on top of it, centred on the cursor.
        GameObject ring = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ring.transform.SetParent(canvasRoot.transform, false);
        Image ringBack = ring.GetComponent<Image>();
        ringBack.sprite = Ring();
        ringBack.color = new Color(0f, 0f, 0f, 0.55f);
        ringBack.raycastTarget = false;
        ringRect = ring.GetComponent<RectTransform>();
        ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(RingSize, RingSize);
        GameObject ringFillGo = new GameObject("Ring Fill", typeof(RectTransform), typeof(Image));
        ringFillGo.transform.SetParent(ring.transform, false);
        ringFill = ringFillGo.GetComponent<Image>();
        ringFill.sprite = Ring();
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        ringFill.raycastTarget = false;
        RectTransform frr = ringFillGo.GetComponent<RectTransform>();
        frr.anchorMin = Vector2.zero;
        frr.anchorMax = Vector2.one;
        frr.offsetMin = frr.offsetMax = Vector2.zero;

        label = PixelUIKit.CreateText(font, bar.transform, "Label", "", 24f, TextAlignmentOptions.Bottom, FontStyles.Bold, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.sizeDelta = new Vector2(0f, 34f);
        lr.anchoredPosition = new Vector2(0f, 2f);
    }

    public void Show(Vector2 screenPosition, float progress, string text, Color color, bool circle = false)
    {
        if (canvasRoot == null) Build();
        canvasRoot.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local);
        barRect.gameObject.SetActive(!circle);
        ringRect.gameObject.SetActive(circle);
        if (circle)
        {
            ringRect.anchoredPosition = local;
            ringFill.color = Color.Lerp(color, new Color(1f, 0.25f, 0.15f, 1f), progress);
            ringFill.fillAmount = progress;
            return;
        }
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
