// ---- System notes ----
// PixelStatusBox.cs
//   The shared top-of-screen status box: potion timer, device placing line and the takeover minigames' status lines (Blink, Breakout, Dash, Evade, Snake, Sorting,
//   Traffic, Weak Spots, Ultra Holes). PixelStatusBox.Create(font, canvasTransform, fontSize) returns the TMP label (use it as before: PixelUIKit.SetText, .color);
//   the label sits in a rounded window-style box that SLIDES OUT FROM UNDER THE BLACK BAR (a RectMask2D strip starts at the bar's inner edge, so the box is
//   clipped until it has slid below it and is drawn OVER the bar's edge effect). It slides back under the bar when the text is empty or Show(label, false).
//   Show(label, bool) / IsShown(label). Box size follows the text (boxPadding, maxWidth). A thin accent line along the bottom takes the label's colour.
//   Fixed look values live in the consts at the top (not Inspector fields: the box has no component of its own in the scene).
// ----------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One styled box for the on-screen status lines (see the header). Created at run time by <see cref="Create"/>.</summary>
public class PixelStatusBox : MonoBehaviour
{
    private const float SlideSeconds = 0.4f, GapBelowBar = 10f, MaxWidth = 1500f, AccentHeight = 4f;
    private static readonly Vector2 Padding = new Vector2(34f, 12f);
    private static readonly Color BoxColor = new Color(0.07f, 0.09f, 0.14f, 0.92f);

    private TMP_Text label;
    private RectTransform clip, box;
    private Image boxImage, accent;
    private bool wanted = true;
    private float slide;   // 0 = hidden under the bar, 1 = fully out

    /// <summary>Makes the box (inside a clipping strip under the top bar) and returns its text label.</summary>
    public static TMP_Text Create(TMP_FontAsset font, Transform canvas, float fontSize)
    {
        GameObject clipGo = new GameObject("Status Strip", typeof(RectTransform), typeof(RectMask2D));
        clipGo.transform.SetParent(canvas, false);
        GameObject boxGo = new GameObject("Status Box", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(clipGo.transform, false);
        Image bg = boxGo.GetComponent<Image>();
        bg.raycastTarget = false;
        PixelUIKit.StyleWindow(bg, BoxColor);

        TMP_Text text = PixelUIKit.CreateText(font, boxGo.transform, "Status", "", fontSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        text.raycastTarget = false;
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(Padding.x, Padding.y + AccentHeight); tr.offsetMax = new Vector2(-Padding.x, -Padding.y);

        GameObject accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGo.transform.SetParent(boxGo.transform, false);
        Image acc = accentGo.GetComponent<Image>();
        acc.raycastTarget = false;
        RectTransform ar = accentGo.GetComponent<RectTransform>();
        ar.anchorMin = new Vector2(0f, 0f); ar.anchorMax = new Vector2(1f, 0f); ar.pivot = new Vector2(0.5f, 0f);
        ar.offsetMin = new Vector2(14f, 6f); ar.offsetMax = new Vector2(-14f, 6f + AccentHeight);

        PixelStatusBox sb = clipGo.AddComponent<PixelStatusBox>();
        sb.label = text; sb.clip = clipGo.GetComponent<RectTransform>(); sb.box = boxGo.GetComponent<RectTransform>();
        sb.boxImage = bg; sb.accent = acc;
        sb.Layout(0f);
        return text;
    }

    /// <summary>Slides the box out (true) or back under the bar (false). An empty text also keeps it hidden.</summary>
    public static void Show(TMP_Text label, bool shown)
    {
        PixelStatusBox sb = label != null ? label.GetComponentInParent<PixelStatusBox>() : null;
        if (sb != null) sb.wanted = shown;
    }

    /// <summary>Is the box showing (mostly slid out)?</summary>
    public static bool IsShown(TMP_Text label)
    {
        PixelStatusBox sb = label != null ? label.GetComponentInParent<PixelStatusBox>() : null;
        return sb != null && sb.wanted && sb.slide > 0.5f;
    }

    private void LateUpdate()
    {
        if (label == null) return;
        bool visible = wanted && !string.IsNullOrEmpty(label.text);
        slide = Mathf.MoveTowards(slide, visible ? 1f : 0f, Time.unscaledDeltaTime / SlideSeconds);
        Layout(slide);
    }

    private void Layout(float t)
    {
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 pref = label.GetPreferredValues(string.IsNullOrEmpty(label.text) ? " " : label.text);
        float w = Mathf.Min(pref.x + Padding.x * 2f, MaxWidth), h = pref.y + Padding.y * 2f + AccentHeight;

        clip.anchorMin = new Vector2(0f, 1f); clip.anchorMax = new Vector2(1f, 1f); clip.pivot = new Vector2(0.5f, 1f);
        clip.sizeDelta = new Vector2(0f, h + GapBelowBar + 24f);
        clip.anchoredPosition = new Vector2(0f, -bar);

        float e = 1f - (1f - t) * (1f - t) * (1f - t);   // ease out
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
        box.sizeDelta = new Vector2(w, h);
        box.anchoredPosition = new Vector2(0f, Mathf.Lerp(h + 4f, -GapBelowBar, e));
        box.gameObject.SetActive(t > 0.001f);

        Color c = label.color;
        accent.color = new Color(c.r, c.g, c.b, 0.85f * c.a);
    }
}
