// ---- System notes ----
// PixelArcRing.cs
//   ONE place for making a full ring (a PixelArcGraphic from 0 to 359.5 degrees) on a canvas (2026-10-24: was a copied block in
//   PixelGoldenSnitch (target / closing rings) and PixelPowerClick (charge / cooldown rings, shockwave)).
//   - Create(parent, name, colour, halfWidth, anchor, out rect): the ring (radius 0.9 of its rect, 'halfWidth' thick, 1.5 px feather,
//     no raycasts), pivot in its middle, anchored at 'anchor', switched OFF - the caller sizes, places and shows it.
// ---------------------------------------------------
using UnityEngine;

public static class PixelArcRing
{
    /// <summary>A full ring under 'parent', anchored at 'anchor' with its pivot in the middle; starts switched off.</summary>
    public static PixelArcGraphic Create(Transform parent, string name, Color c, float halfWidth, Vector2 anchor, out RectTransform rect)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        g.transform.SetParent(parent, false);
        PixelArcGraphic arc = g.AddComponent<PixelArcGraphic>();
        arc.Setup(0.9f, halfWidth, 1.5f);
        arc.StartDegrees = 0f;
        arc.EndDegrees = 359.5f;
        arc.color = c;
        arc.raycastTarget = false;
        rect = arc.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        g.SetActive(false);
        return arc;
    }
}
