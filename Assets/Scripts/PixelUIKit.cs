using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small shared helpers for the code-built UI (canvas, event system, text, buttons), so every UI script
/// builds things the same way instead of carrying its own copy.
/// </summary>
public static class PixelUIKit
{
    /// <summary>Makes sure the scene has an EventSystem (with the right input module for the active input system).</summary>
    public static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    /// <summary>A screen-space overlay canvas that scales with the screen. 'clickable' adds a GraphicRaycaster.</summary>
    public static GameObject CreateCanvas(string objectName, int sortingOrder, Vector2 referenceResolution, bool clickable)
    {
        GameObject root = new GameObject(objectName);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        if (clickable) root.AddComponent<GraphicRaycaster>();
        return root;
    }

    /// <summary>A TextMeshPro UI text that doesn't catch clicks. A null font = the TMP default.</summary>
    public static TMP_Text CreateText(TMP_FontAsset font, Transform parent, string objectName, string text, float size,
                                      TextAlignmentOptions alignment, FontStyles style, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        return tmp;
    }

    /// <summary>A coloured button with a centred bold label that stretches over it.</summary>
    public static Button CreateButton(TMP_FontAsset font, Transform parent, string objectName, string label, Vector2 size,
                                      Color color, Color labelColor, float labelSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.color = color;
        go.GetComponent<Button>().targetGraphic = image;

        TMP_Text text = CreateText(font, go.transform, "Label", label, labelSize, TextAlignmentOptions.Center,
                                   FontStyles.Bold, labelColor);
        Stretch(text.rectTransform);
        return go.GetComponent<Button>();
    }

    /// <summary>Makes a rect fill its parent.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
