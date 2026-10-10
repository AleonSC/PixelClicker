using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared drawing helpers for the neon "LED strip behind a panel" glow along the edges of the black bars (the title screen's bar and
/// the HUD's top / bottom bars): soft gradient strips outside and inside an edge plus a bright thin line on it.
/// </summary>
public static class PixelBarGlow
{
    private static Sprite spriteUp, spriteDown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { spriteUp = null; spriteDown = null; }

    /// <summary>A soft vertical fade: alpha 1 on one edge falling to 0 on the other. 'up' = strongest at the bottom row.</summary>
    public static Sprite Sprite(bool up)
    {
        Sprite cached = up ? spriteUp : spriteDown;
        if (cached != null) return cached;
        const int h = 64;
        Texture2D tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float k = up ? 1f - v : v;
            float a = k * k * k;   // a soft, quickly fading falloff like light bleeding round a panel
            for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply(false, true);
        Sprite sp = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 100f);
        if (up) spriteUp = sp; else spriteDown = sp;
        return sp;
    }

    /// <summary>A gradient strip along the top or bottom edge of 'parent' (stretched across it), reaching outside or inside the edge.</summary>
    public static Image AddStrip(Transform parent, string name, bool topEdge, bool outward, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        bool extendsUp = topEdge == outward;   // outside the top edge / inside the bottom edge reach upwards
        img.sprite = Sprite(extendsUp);
        RectTransform rt = go.GetComponent<RectTransform>();
        float y = topEdge ? 1f : 0f;
        rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y);
        rt.pivot = new Vector2(0.5f, extendsUp ? 0f : 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, size);
        return img;
    }

    /// <summary>The thin bright LED line on the top or bottom edge of 'parent'.</summary>
    public static Image AddLine(Transform parent, string name, bool topEdge)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        float y = topEdge ? 1f : 0f;
        rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 3f);
        return img;
    }

    /// <summary>The colour of one edge: hue = (the player's chosen hue + time x speed + offset), vivid. Speed 0 = a fixed colour.</summary>
    public static Color EdgeColor(float baseHue, float time, float speed, float offset, float saturation)
    {
        return Color.HSVToRGB(Mathf.Repeat(baseHue + time * speed + offset, 1f), Mathf.Clamp01(saturation), 1f);
    }

    /// <summary>Colours the three images of one edge (outer glow, inner glow, line) with 'c' at overall strength 'a'.</summary>
    public static void Colorize(Image outer, Image inner, Image line, Color c, float a)
    {
        outer.color = new Color(c.r, c.g, c.b, a);
        inner.color = new Color(c.r, c.g, c.b, a * 0.55f);
        Color l = Color.Lerp(c, Color.white, 0.55f);
        l.a = Mathf.Clamp01(a * 1.2f);
        line.color = l;
    }
}
