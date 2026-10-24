// ---- System notes ----
// PixelBlockArt.cs
//   ONE place for the code-drawn 'block art' of the characters (2026-10-24: was a copied block in each): every vendor / visitor
//   (PixelVisitorMinigame and its subclasses), Cubie (PixelGuideVendor, PixelCubiePeek) and the Dragon Wish scene draw their figures out of
//   plain coloured UI squares.
//   - Block(parent, name, colour, size, pos, rotation, anchor): one square Image (no raycasts), pivot in its middle, placed at 'pos' from
//     'anchor' on the parent (BottomCentre (0.5, 0) for the standing characters, Middle (0.5, 0.5) for Cubie's peeking head and the wish scene).
//     Each script keeps a one-line Block / Sq wrapper with its own anchor, so its drawing code reads the same as before.
//   - WalkerHolder(parent, name, height, out art): the holder a menu walks about (PixelTitleWanderers): anchored at the bottom centre,
//     'height' tall, with a 300 x 400 'Art' child (its first child) scaled to fit; the caller draws the character into 'art'.
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

public static class PixelBlockArt
{
    /// <summary>The anchor of the standing characters: the bottom centre of the parent.</summary>
    public static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
    /// <summary>The anchor of drawings centred on their parent.</summary>
    public static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

    /// <summary>One plain coloured square (no raycasts) under 'parent', its middle at 'pos' from 'anchor', turned by 'rotation' degrees.</summary>
    public static RectTransform Block(Transform parent, string name, Color color, Vector2 size, Vector2 pos, float rotation, Vector2 anchor)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        r.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return r;
    }

    /// <summary>A holder for a menu to walk about: anchored at the bottom centre of 'parent', 'height' tall, with a 300 x 400 'Art' child
    /// (its first child) scaled to fit. The caller draws the character into 'art'.</summary>
    public static RectTransform WalkerHolder(Transform parent, string name, float height, out Transform art)
    {
        GameObject holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        RectTransform r = holder.GetComponent<RectTransform>();
        float scale = height / 400f;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(300f * scale, 400f * scale);
        GameObject artGo = new GameObject("Art", typeof(RectTransform));
        artGo.transform.SetParent(holder.transform, false);
        RectTransform ar = artGo.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0f);
        ar.pivot = new Vector2(0.5f, 0f);
        ar.sizeDelta = new Vector2(300f, 400f);
        ar.localScale = Vector3.one * scale;
        art = artGo.transform;
        return r;
    }
}
