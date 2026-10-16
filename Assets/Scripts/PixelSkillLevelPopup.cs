// ---- System notes ----
// PixelSkillLevelPopup.cs
//   A small LEVEL-UP popup for every skill (PixelSkills.AddXpFlat calls Show when a level is reached): a round badge in the skill's accent colour with the
//   skill's icon spinning inside it (the real model of the skill's first block, PixelTierPicture; Standard / Offline use the White pixel), the text
//   'WOODCUTTING  LEVEL 5' with '+1 skill point' on a pill beside it, and tiny FIREWORKS - three little bursts of coloured sparks around the badge that fly
//   out, fall and fade. It pops in (overshoot), stays holdSeconds and fades; several level-ups queue one after another. Sits above the bottom bar
//   (anchor / offset in the Inspector), waits while a menu is open, plays the 'achievement' sound. All on unscaled time, own canvas, no physics.
//   A system component (PixelSystems, UI group); disablePopup switches it off.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelSkillLevelPopup : MonoBehaviour
{
    [Tooltip("Switch the level-up popups off.")]
    [SerializeField] private bool disablePopup = false;
    [Tooltip("Where the popup sits: anchor on the screen (0.5, 0 = bottom middle).")]
    [SerializeField] private Vector2 anchor = new Vector2(0.5f, 0f);
    [Tooltip("Offset from the anchor in pixels at 1080p (the bottom black bar is below it).")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 190f);
    [Tooltip("Size of the round badge, in pixels at 1080p.")]
    [SerializeField] private float badgeSize = 96f;
    [Tooltip("Seconds the popup stays before it fades.")]
    [SerializeField] private float holdSeconds = 2.4f;
    [Tooltip("Sparks per firework burst.")]
    [SerializeField] private int sparksPerBurst = 14;

    private static PixelSkillLevelPopup instance;
    private struct Pending { public string name; public int level, points, tier; public Color accent; }
    private readonly Queue<Pending> queue = new Queue<Pending>();

    private PixelClicker clicker;
    private GameObject canvasObject;
    private RectTransform root, badgeRect, sparkLayer;
    private Image badgeImage, ringImage, pillImage;
    private RawImage iconImage;
    private TMP_Text titleLabel, subLabel;
    private CanvasGroup group;
    private float t = -1f, spin;
    private int showingTier = -1, burstsFired;
    private Color accent;
    private static Sprite discSprite, ringSprite, sparkSprite;

    private class Spark { public RectTransform rect; public Image image; public Vector2 vel; public float life, age; }
    private readonly List<Spark> sparks = new List<Spark>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; discSprite = ringSprite = sparkSprite = null; }

    private void Awake() { instance = this; clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }
    private void OnDestroy() { if (instance == this) instance = null; if (canvasObject != null) Destroy(canvasObject); }

    /// <summary>A skill reached a new level: queue its popup.</summary>
    public static void Show(PixelSkills.SkillPack pack, int level, int points)
    {
        if (instance == null || instance.disablePopup || pack == null) return;
        PixelClicker c = instance.clicker != null ? instance.clicker : PixelFind.Cached<PixelClicker>();
        int tier = -1;
        if (c != null)
        {
            if (pack.types != null && pack.types.Length > 0) tier = c.IndexOf(pack.types[0]);
            if (tier < 0) tier = c.IndexOf(PixelClicker.PixelType.White);
        }
        instance.queue.Enqueue(new Pending { name = pack.displayName, level = level, points = points, tier = tier, accent = pack.accent });
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (t < 0f)
        {
            if (queue.Count == 0 || PixelCalm.InMenu) return;
            Begin(queue.Dequeue());
        }
        if (PixelCalm.InMenu) { if (canvasObject != null) canvasObject.SetActive(false); return; }
        if (!canvasObject.activeSelf) canvasObject.SetActive(true);
        t += dt;
        float popIn = 0.28f, fade = 0.4f, total = popIn + holdSeconds + fade;

        float scale = t < popIn ? EaseOutBack(t / popIn) : 1f;
        root.localScale = Vector3.one * scale;
        group.alpha = t > popIn + holdSeconds ? 1f - Mathf.Clamp01((t - popIn - holdSeconds) / fade) : 1f;

        // The badge breathes a little and its ring glows; the icon spins.
        float pulse = 1f + 0.05f * Mathf.Sin(t * 7f);
        badgeRect.localScale = Vector3.one * pulse;
        ringImage.color = Color.Lerp(accent, Color.white, 0.4f + 0.3f * Mathf.Sin(t * 5f));
        spin += dt * 140f;
        if (showingTier >= 0) PixelTierPicture.Render(showingTier, spin);

        // Three tiny fireworks around the badge.
        float[] at = { 0.12f, 0.45f, 0.85f };
        while (burstsFired < at.Length && t >= at[burstsFired]) { Burst(burstsFired); burstsFired++; }
        UpdateSparks(dt);

        if (t >= total && sparks.Count == 0) { t = -1f; canvasObject.SetActive(false); }
    }

    private void Begin(Pending p)
    {
        if (canvasObject == null) Build();
        canvasObject.SetActive(true);
        t = 0f; burstsFired = 0; spin = 0f;
        accent = p.accent.a > 0f ? new Color(p.accent.r, p.accent.g, p.accent.b, 1f) : new Color(0.4f, 0.8f, 1f, 1f);
        badgeImage.color = Color.Lerp(accent, Color.black, 0.55f);
        pillImage.color = new Color(0.05f, 0.07f, 0.12f, 0.92f);
        showingTier = p.tier;
        iconImage.texture = p.tier >= 0 && clicker != null ? PixelTierPicture.Get(clicker, p.tier, 128) : null;
        iconImage.enabled = iconImage.texture != null;
        PixelUIKit.SetText(titleLabel, "<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(accent, Color.white, 0.35f)) + ">" + p.name.ToUpperInvariant() + "</color>  LEVEL " + p.level);
        PixelUIKit.SetText(subLabel, p.points > 0 ? "+" + p.points + " skill point" + (p.points == 1 ? "" : "s") : "Level up!");
        PixelAudio.Play("achievement");
    }

    private void Build()
    {
        canvasObject = PixelUIKit.CreateCanvas("Skill Level Popup", 960, new Vector2(1920f, 1080f), false);
        root = new GameObject("Popup", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        root.SetParent(canvasObject.transform, false);
        root.anchorMin = root.anchorMax = anchor;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = offset;
        root.sizeDelta = new Vector2(badgeSize * 5.2f, badgeSize);
        group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        TMP_FontAsset font = clicker != null ? clicker.UIFont : null;

        // The pill with the text (to the right of the badge).
        GameObject pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(root, false);
        pillImage = pill.GetComponent<Image>();
        PixelUIKit.StyleBox(pillImage);
        pillImage.raycastTarget = false;
        RectTransform pr = pill.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0.5f); pr.anchorMax = new Vector2(1f, 0.5f);
        pr.pivot = new Vector2(0f, 0.5f);
        pr.offsetMin = new Vector2(badgeSize * 0.5f, -badgeSize * 0.36f); pr.offsetMax = new Vector2(0f, badgeSize * 0.36f);
        titleLabel = PixelUIKit.CreateText(font, pill.transform, "Title", "", badgeSize * 0.26f, TextAlignmentOptions.Left, FontStyles.Bold, Color.white);
        RectTransform tr = titleLabel.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.45f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(badgeSize * 0.62f, 0f); tr.offsetMax = new Vector2(-12f, -4f);
        titleLabel.enableAutoSizing = true; titleLabel.fontSizeMax = badgeSize * 0.26f; titleLabel.fontSizeMin = 12f;
        subLabel = PixelUIKit.CreateText(font, pill.transform, "Sub", "", badgeSize * 0.19f, TextAlignmentOptions.Left, FontStyles.Normal, new Color(1f, 0.9f, 0.55f, 1f));
        RectTransform sr = subLabel.rectTransform;
        sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0.45f);
        sr.offsetMin = new Vector2(badgeSize * 0.62f, 4f); sr.offsetMax = new Vector2(-12f, 0f);

        // Sparks fly behind the badge but over the pill.
        sparkLayer = new GameObject("Sparks", typeof(RectTransform)).GetComponent<RectTransform>();
        sparkLayer.SetParent(root, false);
        sparkLayer.anchorMin = sparkLayer.anchorMax = new Vector2(0f, 0.5f);
        sparkLayer.anchoredPosition = new Vector2(badgeSize * 0.5f, 0f);
        sparkLayer.sizeDelta = Vector2.zero;

        // The round badge: a dark disc in the skill's colour, a glowing ring, the icon inside.
        badgeRect = new GameObject("Badge", typeof(RectTransform)).GetComponent<RectTransform>();
        badgeRect.SetParent(root, false);
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(badgeSize * 0.5f, 0f);
        badgeRect.sizeDelta = Vector2.one * badgeSize;
        badgeImage = MakeImage("Disc", badgeRect, Disc(), Vector2.one * badgeSize);
        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
        icon.transform.SetParent(badgeRect, false);
        iconImage = icon.GetComponent<RawImage>();
        iconImage.raycastTarget = false;
        icon.GetComponent<RectTransform>().sizeDelta = Vector2.one * badgeSize * 0.78f;
        ringImage = MakeImage("Ring", badgeRect, Ring(), Vector2.one * badgeSize * 1.06f);
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = size;
        return img;
    }

    // ---- fireworks ----

    private void Burst(int index)
    {
        float r = badgeSize * 0.9f;
        float a = (index * 2.3f + Random.value) * Mathf.PI;
        Vector2 centre = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.3f) * r;
        Color[] palette = { accent, Color.Lerp(accent, Color.white, 0.6f), new Color(1f, 0.85f, 0.3f, 1f), Color.white };
        int n = Mathf.Max(4, sparksPerBurst);
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            float speed = Random.Range(0.9f, 1.5f) * badgeSize * 1.6f;
            Spark s = GetSpark();
            s.rect.anchoredPosition = centre;
            s.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
            s.life = Random.Range(0.45f, 0.75f);
            s.age = 0f;
            s.image.color = palette[i % palette.Length];
            s.rect.sizeDelta = Vector2.one * Random.Range(4f, 7f) * badgeSize / 96f;
            s.rect.gameObject.SetActive(true);
        }
    }

    private readonly List<Spark> sparkPool = new List<Spark>();

    private Spark GetSpark()
    {
        Spark s;
        if (sparkPool.Count > 0) { s = sparkPool[sparkPool.Count - 1]; sparkPool.RemoveAt(sparkPool.Count - 1); }
        else
        {
            s = new Spark();
            GameObject go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(sparkLayer, false);
            s.rect = go.GetComponent<RectTransform>();
            s.image = go.GetComponent<Image>();
            s.image.sprite = SparkSprite();
            s.image.raycastTarget = false;
        }
        sparks.Add(s);
        return s;
    }

    private void UpdateSparks(float dt)
    {
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Spark s = sparks[i];
            s.age += dt;
            if (s.age >= s.life) { s.rect.gameObject.SetActive(false); sparks.RemoveAt(i); sparkPool.Add(s); continue; }
            s.vel *= Mathf.Exp(-3.2f * dt);                       // air drag: a quick pop, then they hang
            s.vel += Vector2.down * badgeSize * 2.2f * dt;         // and fall
            s.rect.anchoredPosition += s.vel * dt;
            Color c = s.image.color;
            c.a = 1f - (s.age / s.life) * (s.age / s.life);
            s.image.color = c;
        }
    }

    // ---- code-drawn sprites ----

    private static Sprite Disc() => discSprite != null ? discSprite : discSprite = Draw(64, (d) => d <= 0.98f ? 1f : 0f);
    private static Sprite Ring() => ringSprite != null ? ringSprite : ringSprite = Draw(64, (d) => d >= 0.84f && d <= 0.98f ? 1f : 0f);
    private static Sprite SparkSprite() => sparkSprite != null ? sparkSprite : sparkSprite = Draw(16, (d) => Mathf.Clamp01(1.2f - d * 1.2f));

    /// <summary>A round white sprite: 'alpha' gets the distance from the centre (0..1 at the edge).</summary>
    private static Sprite Draw(int n, System.Func<float, float> alpha)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(d)) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }
}
