// ---- System notes ----
// PixelInputCaret.cs
//   A blinking text cursor drawn by us for an input field (the built-in one did not show in the dev tools' fields). Add it to a TMP_InputField's object: it hides the built-in caret and draws a white
//   bar at the caret position while the field has focus; works for single-line and multi-line fields. Used by the Inspector's fields and the Dev Hub's Notes.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelInputCaret : MonoBehaviour
{
    private TMP_InputField field;
    private RectTransform bar;

    private void LateUpdate()
    {
        if (field == null)
        {
            field = GetComponent<TMP_InputField>();
            if (field == null || field.textComponent == null) return;
            field.customCaretColor = true;
            field.caretColor = Color.clear;
            GameObject g = new GameObject("Caret", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(field.textComponent.transform, false);
            g.GetComponent<Image>().color = Color.white;
            g.GetComponent<Image>().raycastTarget = false;
            bar = g.GetComponent<RectTransform>();
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
            g.SetActive(false);
        }
        bool show = field.isFocused && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;
        if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
        if (!show) return;
        TMP_Text text = field.textComponent;
        TMP_TextInfo info = text.textInfo;
        int count = info.characterCount;
        int pos = Mathf.Clamp(field.stringPosition, 0, count);
        float x, y;
        float left = text.rectTransform.rect.xMin + text.margin.x;
        if (count == 0 || info.lineCount == 0)
        {
            x = left;
            y = info.lineCount > 0 ? (info.lineInfo[0].ascender + info.lineInfo[0].descender) * 0.5f : 0f;
        }
        else if (pos == 0)
        {
            TMP_CharacterInfo c0 = info.characterInfo[0];
            x = c0.origin;
            y = (info.lineInfo[c0.lineNumber].ascender + info.lineInfo[c0.lineNumber].descender) * 0.5f;
        }
        else
        {
            TMP_CharacterInfo prev = info.characterInfo[pos - 1];
            TMP_LineInfo line = info.lineInfo[Mathf.Clamp(prev.lineNumber, 0, info.lineCount - 1)];
            if (prev.character == '\n' || prev.character == '\r')
            {
                // The caret sits at the start of the next line.
                x = left;
                y = (line.ascender + line.descender) * 0.5f - line.lineHeight;
                if (pos < count) { TMP_CharacterInfo next = info.characterInfo[pos]; TMP_LineInfo nl = info.lineInfo[Mathf.Clamp(next.lineNumber, 0, info.lineCount - 1)]; y = (nl.ascender + nl.descender) * 0.5f; }
            }
            else
            {
                x = prev.xAdvance;
                y = (line.ascender + line.descender) * 0.5f;
            }
        }
        float h = Mathf.Max(14f, text.fontSize * 1.1f);
        bar.sizeDelta = new Vector2(2f, h);
        bar.anchoredPosition = new Vector2(x, y);
        bar.SetAsLastSibling();
    }
}
