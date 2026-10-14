// ---- System notes ----
// PixelHintBox.cs
//   A bottom-of-screen instruction line inside a window-style box (same look as PixelStatusBox, which does the same job at the top). PixelHintBox.Create(font, canvasTransform, fontSize,
//   bottomOffset) returns the TMP label: set its .text as before (PixelUIKit.SetText also works). The box grows / shrinks with the text (wraps at MaxWidth) and is invisible
//   while the text is empty, so callers need no show / hide code. Used by the first person hint line (PixelFirstPerson). Look values are consts at the top (no component of its own in the scene).
// ----------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelHintBox : MonoBehaviour
{
    private const float MaxWidth = 1500f;
    private static readonly Vector2 Padding = new Vector2(30f, 10f);
    private static readonly Color BoxColor = new Color(0.07f, 0.09f, 0.14f, 0.92f);

    private TMP_Text label;
    private RectTransform box;
    private Image boxImage;
    private string sizedFor;

    /// <summary>Makes the box (centred above the bottom edge, 'bottomOffset' canvas units up) and returns its text label.</summary>
    public static TMP_Text Create(TMP_FontAsset font, Transform canvas, float fontSize, float bottomOffset)
    {
        GameObject go = new GameObject("Hint Box", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvas, false);
        Image bg = go.GetComponent<Image>();
        bg.raycastTarget = false;
        PixelUIKit.StyleWindow(bg, BoxColor);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0f, bottomOffset);

        TMP_Text text = PixelUIKit.CreateText(font, go.transform, "Hint", "", fontSize, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        text.raycastTarget = false;
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Padding; tr.offsetMax = -Padding;

        PixelHintBox hb = go.AddComponent<PixelHintBox>();
        hb.label = text; hb.box = r; hb.boxImage = bg;
        bg.enabled = false;
        return text;
    }

    private void LateUpdate()
    {
        if (label == null) return;
        string text = label.text;
        bool has = !string.IsNullOrEmpty(text);
        if (boxImage.enabled != has) boxImage.enabled = has;
        if (!has || text == sizedFor) return;
        sizedFor = text;
        Vector2 one = label.GetPreferredValues(text, 0f, 0f);   // width of the text on one line
        float w = Mathf.Min(one.x + Padding.x * 2f, MaxWidth);
        float h = label.GetPreferredValues(text, w - Padding.x * 2f, 0f).y + Padding.y * 2f;
        box.sizeDelta = new Vector2(w, h);
    }
}
