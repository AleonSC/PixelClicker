// ---- System notes ----
// PixelRisingText.cs
//   ONE place for the floating 3D message of a minigame round (Breakout, Snake, Sorting Race, Stardust; 2026-10-24: was a copied block
//   in each): a bold, black-outlined TextMeshPro in the world that rises and fades away, then destroys itself.
//   - Create(name, message, fontSize, font, boxSize): makes the text object (centred, bold, outline 0.2, the shared UI font when given).
//     The minigame passes the result's gameObject to its Track(...) so the dev tools' Despawn removes it.
//   - Fade(text, seconds, place, tint): the coroutine (run it with the minigame's StartCoroutine). Every frame (unscaled time) it calls
//     place(text, k) with k = 0..1 - the caller moves / turns the text there (each round rises its own way) - then fades it out
//     (alpha 1 - k*k; with a tint the colour is set too). Destroys the object at the end.
//   (Other one-off world texts - Black Hole, Meteor... - have their own looks and keep their own code.)
// ---------------------------------------------------
using System.Collections;
using TMPro;
using UnityEngine;

public static class PixelRisingText
{
    /// <summary>Moves / turns the text for this frame; k runs from 0 (start) to 1 (gone).</summary>
    public delegate void Place(TextMeshPro text, float k);

    /// <summary>Makes the message text object (not yet moving: start Fade with it).</summary>
    public static TextMeshPro Create(string name, string message, float fontSize, TMP_FontAsset font, Vector2 boxSize)
    {
        GameObject go = new GameObject(name);
        TextMeshPro text = go.AddComponent<TextMeshPro>();
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        if (font != null) text.font = font;
        text.rectTransform.sizeDelta = boxSize;
        return text;
    }

    /// <summary>Rises (via 'place') and fades the text over 'seconds' of unscaled time, then destroys it. 'tint' = also set this colour (its alpha fades).</summary>
    public static IEnumerator Fade(TextMeshPro text, float seconds, Place place, Color? tint = null)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / seconds);
            place(text, k);
            if (tint.HasValue)
            {
                Color c = tint.Value;
                c.a *= 1f - k * k;
                text.alpha = c.a;
                text.color = c;
            }
            else text.alpha = 1f - k * k;
            yield return null;
        }
        Object.Destroy(text.gameObject);
    }
}
