// ---- System notes ----
// PixelDevUI.cs
//   Shared building blocks for the Dev Hub's tool tabs (Look Studio, Setups, Events, Sounds, Console): one set of colours, top-left placing (PlaceTL), a button
//   with a click sound, a one-line label, a wrapping text, a drop-down and a tick box, all in the shared font. New dev tabs use these instead of their own copies
//   (older tabs - Docs, Inspector, Tester - still have private ones: move them over when they are next touched).
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PixelDevUI
{
    public const float FontSize = 21f;

    public static readonly Color Box = new Color(0.07f, 0.09f, 0.14f, 1f), RowColor = new Color(1f, 1f, 1f, 0.035f), HeaderColor = new Color(0.2f, 0.25f, 0.36f, 1f),
                                 ButtonColor = new Color(0.25f, 0.32f, 0.48f, 1f), Green = new Color(0.22f, 0.55f, 0.36f, 1f), Red = new Color(0.62f, 0.22f, 0.24f, 1f),
                                 TextColor = new Color(0.92f, 0.94f, 1f, 1f), DimText = new Color(0.68f, 0.72f, 0.82f, 1f), Accent = new Color(0.4f, 0.7f, 1f, 1f);

    /// <summary>The game's shared UI font (null before PixelClicker exists: TextMeshPro's default is used then).</summary>
    public static TMP_FontAsset Font
    {
        get { PixelClicker c = PixelFind.Cached<PixelClicker>(); return c != null ? c.UIFont : null; }
    }

    /// <summary>Anchors 'rt' at the top left of its parent and puts it at (x, y) down from there, w x h big.</summary>
    public static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>A button with a centred label that plays the click sound and then runs 'onClick'.</summary>
    public static Button Button(Transform parent, string label, float x, float y, float w, float h, Color color, Action onClick, float size = FontSize)
    {
        Button b = PixelUIKit.CreateButton(Font, parent, label, label, new Vector2(w, h), color, TextColor, size);
        PlaceTL(b.GetComponent<RectTransform>(), x, y, w, h);
        if (onClick != null) b.onClick.AddListener(() => onClick());
        return b;
    }

    /// <summary>A one-line label (left aligned unless told otherwise).</summary>
    public static TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, float size, Color color,
                                 TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        TMP_Text t = PixelUIKit.CreateText(Font, parent, text, text, size, align, FontStyles.Normal, color);
        PlaceTL(t.rectTransform, x, y, w, h);
        return t;
    }

    /// <summary>A text that wraps onto more lines inside its box (status lines, notes).</summary>
    public static TMP_Text WrapText(Transform parent, string name, float x, float y, float w, float h, float size, Color color)
    {
        TMP_Text t = PixelUIKit.CreateText(Font, parent, name, "", size, TextAlignmentOptions.TopLeft, FontStyles.Normal, color);
        PlaceTL(t.rectTransform, x, y, w, h);
#if UNITY_2023_1_OR_NEWER
        t.textWrappingMode = TextWrappingModes.Normal;
#else
        t.enableWordWrapping = true;
#endif
        return t;
    }

    /// <summary>A drop-down in the dev colours.</summary>
    public static TMP_Dropdown Dropdown(Transform parent, string name, float x, float y, float w, float h, float size = FontSize, bool escapeMasks = false)
    {
        TMP_Dropdown dd = PixelUIKit.CreateDropdown(Font, parent, name, new Vector2(w, h), Box, Box, TextColor, size, escapeMasks: escapeMasks);
        PlaceTL(dd.GetComponent<RectTransform>(), x, y, w, h);
        return dd;
    }

    /// <summary>A tick box in the dev colours (square, 'size' wide).</summary>
    public static Toggle Tick(Transform parent, float x, float y, float size, bool on)
    {
        Toggle t = PixelUIKit.CreateTickBox(parent, size, Box, Accent);
        PlaceTL(t.GetComponent<RectTransform>(), x, y, size, size);
        t.SetIsOnWithoutNotify(on);
        return t;
    }

    /// <summary>A text box in the dev colours.</summary>
    public static TMP_InputField Input(Transform parent, string name, float x, float y, float w, float h, string placeholder, float size = FontSize - 1f)
    {
        TMP_InputField f = PixelUIKit.CreateInputField(Font, parent, name, new Vector2(w, h), Box, TextColor, size, placeholder);
        PlaceTL(f.GetComponent<RectTransform>(), x, y, w, h);
        return f;
    }

    /// <summary>A section title line (bold, accent colour).</summary>
    public static TMP_Text Heading(Transform parent, string text, float x, float y, float w)
    {
        TMP_Text t = Label(parent, text, x, y, w, 34f, FontSize + 1f, Accent);
        t.fontStyle = FontStyles.Bold;
        return t;
    }
}
