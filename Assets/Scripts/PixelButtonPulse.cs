// ---- System notes (moved here from CLAUDE.md) ----
// PixelButtonPulse.cs
//   Button pulse: every shop button (the shop's own CreateButton adds PixelButtonPulse) sends a clean neon outline out from under it
//   when pressed - a thin glowing ring (a code-drawn 9-sliced rounded-rectangle outline sprite, RingSprite, the same on every button)
//   is placed directly behind the button, centred on it whatever its anchors / pivot are (PixelButtonPulseRunner: grows 12 units
//   beyond the edge on every side and fades over 0.45 s on unscaled time). The colour is the button's colour stretched to a neon
//   (Neon); a button that turns from dark grey to bright when clicked (a tab) uses the bright colour one frame later.
//   PixelButtonPulse.Disabled switches it off. Other windows' buttons (PixelUIKit.CreateButton) don't have it yet.
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Every time the button it sits on is pressed, a clean neon outline in the button's colour comes out from under it: a thin glowing ring (a
/// code-drawn rounded-rectangle outline, so it looks the same on every button) is placed just behind the button, grows evenly outwards on every
/// side and fades away. Unscaled time, so it plays while the game is paused. Added to the shop's buttons by <see cref="PixelShop"/>.
/// </summary>
[RequireComponent(typeof(Button))]
public class PixelButtonPulse : MonoBehaviour
{
    /// <summary>Turn the effect off for every button.</summary>
    public static bool Disabled;

    private static Sprite ring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Disabled = false; ring = null; }

    private const float Seconds = 0.45f;
    private const float Reach = 12f;      // how far the ring travels beyond the button's edge (canvas units)

    private Button button;
    private Image source;

    private void Awake()
    {
        button = GetComponent<Button>();
        source = GetComponent<Image>();
        button.onClick.AddListener(Pulse);
    }

    /// <summary>A 9-sliced rounded-rectangle outline: a thin bright line with a soft glow either side, white so it can be tinted.</summary>
    private static Sprite RingSprite()
    {
        if (ring != null) return ring;
        const int size = 64;
        const float radius = 14f, lineAt = 4f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Button Pulse Ring", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance inwards from the rounded rectangle's edge.
                float px = Mathf.Abs(x + 0.5f - half) - (half - radius), py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f);
                float d = radius - outside;                       // 0 at the edge, grows inwards
                float off = Mathf.Abs(d - lineAt);
                float core = 1f - Mathf.SmoothStep(0.8f, 1.9f, off);
                float glow = 0.35f * (1f - Mathf.SmoothStep(0f, 5f, off));
                float a = Mathf.Max(core, glow) * (d > -2f ? 1f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(22f, 22f, 22f, 22f));
        ring.name = "Button Pulse Ring";
        return ring;
    }

    private void Pulse()
    {
        if (Disabled || source == null || !isActiveAndEnabled) return;
        RectTransform rect = (RectTransform)transform;
        Transform parent = rect.parent;
        if (parent == null) return;

        GameObject go = new GameObject("Button Pulse", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);   // centred on the button whatever its anchors / pivot are
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = rect.rect.size;
        r.localRotation = rect.localRotation;
        r.localScale = rect.localScale;
        r.position = rect.TransformPoint(rect.rect.center);
        go.transform.SetSiblingIndex(rect.GetSiblingIndex());   // directly behind the button
        Image im = go.GetComponent<Image>();
        im.sprite = RingSprite();
        im.type = Image.Type.Sliced;
        im.raycastTarget = false;
        im.color = new Color(1f, 1f, 1f, 0f);

        PixelButtonPulseRunner run = go.AddComponent<PixelButtonPulseRunner>();
        run.Begin(im, rect, source, source.color, Seconds, Reach);
    }

    /// <summary>The neon colour of a button colour: its brightest channel stretched to full, lightened a little.</summary>
    public static Color Neon(Color c)
    {
        float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        Color n = peak > 0.01f ? new Color(c.r / peak, c.g / peak, c.b / peak, 1f) : Color.white;
        return Color.Lerp(n, Color.white, 0.15f);
    }
}

/// <summary>Plays one pulse: grows the ring evenly round the button and fades it, then destroys itself. Lives on the pulse object so it ends with it.</summary>
public class PixelButtonPulseRunner : MonoBehaviour
{
    private Image image;
    private RectTransform rect, follow;
    private Image sourceImage;
    private Color colour;
    private float seconds, reach, t;
    private bool recoloured;

    public void Begin(Image im, RectTransform buttonRect, Image source, Color atClick, float duration, float reachUnits)
    {
        image = im; follow = buttonRect; sourceImage = source; colour = atClick; seconds = duration; reach = reachUnits;
        rect = (RectTransform)transform;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        // A tab turns from dark grey to its bright colour the moment it is clicked: after one frame use the colour it ended up with, so the ring is
        // blue for a tab that just became active instead of a dull grey-white. (Other buttons keep the colour they had when pressed.)
        if (!recoloured && t > 0f && sourceImage != null)
        {
            recoloured = true;
            Color.RGBToHSV(colour, out _, out float s0, out float v0);
            Color.RGBToHSV(sourceImage.color, out _, out float s1, out float v1);
            if (s0 < 0.25f && s1 > s0 + 0.15f && v1 > v0) colour = sourceImage.color;
        }
        float k = Mathf.Clamp01(t / seconds);
        float e = 1f - (1f - k) * (1f - k);                    // eases out
        if (follow != null)
        {
            rect.position = follow.TransformPoint(follow.rect.center);   // stays under the button if it moves (a scrolling list)
            // The ring's line sits ~4 units inside its own edge, so start 4 units out: at the start it is hidden right under the button's rim.
            rect.sizeDelta = follow.rect.size + Vector2.one * (2f * (4f + reach * e));
        }
        Color n = PixelButtonPulse.Neon(colour);
        float a = Mathf.Clamp01(t / 0.04f) * (1f - k) * (1f - k);
        image.color = new Color(n.r, n.g, n.b, a);
        if (k >= 1f) Destroy(gameObject);
    }
}
