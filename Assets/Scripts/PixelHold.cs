// ---- System notes (moved here from CLAUDE.md) ----
// PixelHold.cs
//   LOOK (PixelHoldMeter): the meter sits to the RIGHT of the pointer (left when there is no room), bigger (ring 104, bar 300 x 30): dark plate, thick ring with a bright spark at its end and the percentage in the middle,
//   a pulsing glow that strengthens as it fills, a pop-in; the bar version has a glossy fill, a rim and a spark. First person pry puts it beside the crosshair.
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
        if (target == null || PixelGameMode.PauseMenuOpen)
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

/// <summary>
/// The meter that sits to the RIGHT of the pointer while a hold is in progress (to the left when there is no room): a ring (or, with a label, a bar) that fills as you hold. Bigger and clearer than before:
/// a dark plate behind it, a thick ring with a bright spark at the leading edge, the percentage in the middle, a soft glow that pulses (stronger as it nears the end) and a little pop when it appears.
/// </summary>
public class PixelHoldMeter : MonoBehaviour
{
    private const float BarWidth = 300f;
    private const float BarHeight = 30f;
    private const float RingSize = 104f;
    private const float SideGap = 30f;      // space between the pointer and the meter

    private GameObject canvasRoot;
    private RectTransform canvasRect, barRect, fill, ringRect, sparkRect, glowRect, barSpark;
    private TMP_Text label, percent;
    private Image fillImage, ringFill, glowImage, sparkImage, barSparkImage;
    private static Sprite ringSprite, discSprite, softSprite;
    private bool visible;
    private float shownAt = -10f;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRing() { ringSprite = discSprite = softSprite = null; }

    private static Sprite Make(string name, System.Func<float, float> alphaAtDistance)
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color[] px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);   // 0 centre .. 1 edge
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alphaAtDistance(d)));
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>A thick ring (soft edges): the shape the radial fill is cut from.</summary>
    private static Sprite Ring() { return ringSprite != null ? ringSprite : (ringSprite = Make("HoldRing", d => Mathf.Min((1f - d) * 64f, (d - 0.66f) * 64f))); }
    /// <summary>A filled disc with a crisp, slightly soft edge.</summary>
    private static Sprite Disc() { return discSprite != null ? discSprite : (discSprite = Make("HoldDisc", d => (1f - d) * 64f)); }
    /// <summary>A soft radial blob (glow, spark).</summary>
    private static Sprite Soft() { return softSprite != null ? softSprite : (softSprite = Make("HoldSoft", d => Mathf.Pow(1f - d, 2f))); }

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image im = go.GetComponent<Image>();
        im.sprite = sprite; im.color = color; im.raycastTarget = false;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        return im;
    }

    private void Build()
    {
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;

        canvasRoot = PixelUIKit.CreateCanvas("Hold Meter Canvas", 650, new Vector2(1920f, 1080f), false);
        canvasRoot.transform.SetParent(transform, false);
        canvasRect = canvasRoot.GetComponent<RectTransform>();

        // ---- The bar (a hold with a label): a dark plate with a light rim, a glossy fill and a spark at its end.
        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(Outline));
        bar.transform.SetParent(canvasRoot.transform, false);
        Image barBg = bar.GetComponent<Image>();
        barBg.color = new Color(0.04f, 0.05f, 0.09f, 0.88f);
        barBg.raycastTarget = false;
        Outline rim = bar.GetComponent<Outline>();
        rim.effectColor = new Color(1f, 1f, 1f, 0.4f); rim.effectDistance = new Vector2(2f, -2f);
        barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(BarWidth, BarHeight);

        GameObject f = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        f.transform.SetParent(bar.transform, false);
        fillImage = f.GetComponent<Image>();
        fillImage.raycastTarget = false;
        fill = f.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(4f, 4f);
        fill.offsetMax = new Vector2(-4f, -4f);
        fill.pivot = new Vector2(0f, 0.5f);
        GameObject gloss = new GameObject("Gloss", typeof(RectTransform), typeof(Image));
        gloss.transform.SetParent(f.transform, false);
        gloss.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.28f);
        gloss.GetComponent<Image>().raycastTarget = false;
        RectTransform gr = gloss.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0f, 0.5f); gr.anchorMax = Vector2.one; gr.offsetMin = gr.offsetMax = Vector2.zero;
        barSparkImage = NewImage("Spark", bar.transform, Soft(), new Color(1f, 1f, 1f, 0.9f));
        barSpark = barSparkImage.rectTransform;
        barSpark.anchorMin = barSpark.anchorMax = new Vector2(0f, 0.5f);
        barSpark.sizeDelta = new Vector2(46f, 46f);

        label = PixelUIKit.CreateText(font, bar.transform, "Label", "", 28f, TextAlignmentOptions.Left, FontStyles.Bold, Color.white);
        label.outlineColor = new Color(0f, 0f, 0f, 0.9f); label.outlineWidth = 0.18f;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0f, 0f);
        lr.sizeDelta = new Vector2(0f, 38f);
        lr.anchoredPosition = new Vector2(4f, 4f);

        // ---- The ring: glow, dark plate, a pale track, the coloured ring filling clockwise, a spark at its end and the percentage in the middle.
        GameObject ringGo = new GameObject("Ring", typeof(RectTransform));
        ringGo.transform.SetParent(canvasRoot.transform, false);
        ringRect = ringGo.GetComponent<RectTransform>();
        ringRect.anchorMin = ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(RingSize, RingSize);
        glowImage = NewImage("Glow", ringRect, Soft(), new Color(1f, 1f, 1f, 0.3f));
        glowRect = glowImage.rectTransform; glowRect.sizeDelta = new Vector2(RingSize * 1.9f, RingSize * 1.9f);
        Image plate = NewImage("Plate", ringRect, Disc(), new Color(0.03f, 0.04f, 0.08f, 0.82f));
        plate.rectTransform.sizeDelta = new Vector2(RingSize, RingSize);
        Image track = NewImage("Track", ringRect, Ring(), new Color(1f, 1f, 1f, 0.14f));
        track.rectTransform.sizeDelta = new Vector2(RingSize, RingSize);
        ringFill = NewImage("Ring Fill", ringRect, Ring(), Color.white);
        ringFill.rectTransform.sizeDelta = new Vector2(RingSize, RingSize);
        ringFill.type = Image.Type.Filled;
        ringFill.fillMethod = Image.FillMethod.Radial360;
        ringFill.fillOrigin = (int)Image.Origin360.Top;
        ringFill.fillClockwise = true;
        sparkImage = NewImage("Spark", ringRect, Soft(), new Color(1f, 1f, 1f, 0.95f));
        sparkRect = sparkImage.rectTransform; sparkRect.sizeDelta = new Vector2(38f, 38f);
        percent = PixelUIKit.CreateText(font, ringRect, "Percent", "", 30f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        percent.raycastTarget = false;
        PixelUIKit.Stretch(percent.rectTransform);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public void Show(Vector2 screenPosition, float progress, string text, Color color, bool circle = false)
    {
        if (canvasRoot == null) Build();
        canvasRoot.SetActive(true);
        if (!visible) { visible = true; shownAt = Time.unscaledTime; }
        float pop = Mathf.Lerp(0.6f, 1f, EaseOutBack((Time.unscaledTime - shownAt) / 0.16f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (6f + 10f * progress));   // faster as it fills

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local);
        barRect.gameObject.SetActive(!circle);
        ringRect.gameObject.SetActive(circle);
        Color hot = Color.Lerp(color, new Color(1f, 0.25f, 0.15f, 1f), progress);
        float edge = canvasRect.rect.xMax - 12f;
        if (circle)
        {
            // To the right of the pointer; to its left when there is no room.
            bool right = local.x + SideGap + RingSize <= edge;
            ringRect.anchoredPosition = local + new Vector2(right ? SideGap + RingSize * 0.5f : -(SideGap + RingSize * 0.5f), 0f);
            ringRect.localScale = Vector3.one * pop;
            ringFill.color = hot;
            ringFill.fillAmount = progress;
            glowImage.color = new Color(hot.r, hot.g, hot.b, (0.14f + 0.1f * pulse) * (0.6f + 0.8f * progress));
            glowRect.localScale = Vector3.one * (1f + 0.06f * pulse);
            float ang = progress * Mathf.PI * 2f, mid = RingSize * 0.5f * 0.83f;
            sparkRect.anchoredPosition = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * mid;
            sparkImage.color = new Color(1f, 1f, 1f, progress > 0.01f ? 0.95f : 0f);
            percent.text = Mathf.RoundToInt(progress * 100f) + "%";
            percent.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.8f, 1f), progress);
            return;
        }
        bool barRight = local.x + SideGap + BarWidth <= edge;
        barRect.pivot = new Vector2(barRight ? 0f : 1f, 0.5f);
        barRect.anchoredPosition = local + new Vector2(barRight ? SideGap : -SideGap, 0f);
        barRect.localScale = Vector3.one * pop;
        fillImage.color = hot;
        fill.anchorMax = new Vector2(progress, 1f);
        barSpark.anchorMin = barSpark.anchorMax = new Vector2(progress, 0.5f);
        barSpark.anchoredPosition = Vector2.zero;
        barSparkImage.color = new Color(1f, 1f, 1f, progress > 0.01f ? 0.55f + 0.4f * pulse : 0f);
        label.text = text;
    }

    public void Hide()
    {
        visible = false;
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }
}
