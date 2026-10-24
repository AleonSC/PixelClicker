// ---- System notes ----
// PixelShowScreen.cs
//   ONE place for the screen layers the full-screen 'find' shows (Dragon Find, Egg Hatch) build on their canvas (2026-10-24: was a copied
//   block in each). Goes with PixelShowStudio (their 3D photo studio) and PixelUISparks (their sparkles).
//   - NewImage(name, parent, colour): a plain Image with no raycasts.
//   - DimAndBackpack(canvas, backpackSize, out dim, out backpack): the full-screen 'Dark' layer (starts clear; it BLOCKS clicks while the show
//     plays) and the 'Backpack' picture (starts clear, anchored bottom left, backpackSize square) the find flies into.
//   - Titles(font, canvas, titleColour, out title, out subtitle): the big caps title (64) and the white subtitle (40) across the top, below the HUD bar.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PixelShowScreen
{
    /// <summary>A plain Image under 'parent' (no raycasts).</summary>
    public static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>The clear full-screen 'Dark' layer that blocks clicks while the show plays, then the clear 'Backpack' picture (bottom-left anchored).</summary>
    public static void DimAndBackpack(Transform canvas, float backpackSize, out Image dim, out Image backpack)
    {
        dim = NewImage("Dark", canvas, new Color(0f, 0f, 0f, 0f));
        PixelUIKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;   // blocks clicks on the cube and the UI while it plays

        backpack = NewImage("Backpack", canvas, new Color(1f, 1f, 1f, 0f));
        backpack.sprite = PixelGhostMinigame.BuildBackpackSprite();
        backpack.preserveAspect = true;
        RectTransform backpackRect = backpack.rectTransform;
        backpackRect.anchorMin = backpackRect.anchorMax = new Vector2(0f, 0f);
        backpackRect.pivot = new Vector2(0.5f, 0.5f);
        backpackRect.sizeDelta = new Vector2(backpackSize, backpackSize);
    }

    /// <summary>The show's big caps title and white subtitle across the top of the screen, below the HUD bar.</summary>
    public static void Titles(TMP_FontAsset font, Transform canvas, Color titleColor, out TMP_Text title, out TMP_Text subtitle)
    {
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        title = PixelUIKit.CreateText(font, canvas, "Title", "", 64f, TextAlignmentOptions.Center, FontStyles.Bold, titleColor);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 100f); tr.anchoredPosition = new Vector2(0f, -(bar + 40f));
        subtitle = PixelUIKit.CreateText(font, canvas, "Subtitle", "", 40f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        RectTransform sr = subtitle.rectTransform;
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
        sr.sizeDelta = new Vector2(0f, 70f); sr.anchoredPosition = new Vector2(0f, -(bar + 140f));
    }
}
