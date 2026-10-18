// ---- System notes ----
// PixelMenuFx.cs
//   The FANCY PAUSE MENU (Settings > Interface > 'Fancy pause menu', PixelMenuFx.Enabled, PlayerPrefs PixelClicker.Setting.FancyMenu, default ON, saved per profile). It is a layer on top of
//   the classic menu: nothing of the classic menu's code or layout was changed, so switching the setting off (live, no restart) goes back to exactly the old menu - that is the backup.
//   What it adds (all components check Enabled every frame and hide / reset themselves when it is off):
//   * PixelFancyButton on every main-menu button (PixelPauseMenu.LayoutMainButtons -> PixelMenuFx.Decorate): hover grows + brightens the button and slides its label, a coloured accent
//     stripe on the left widens, a light shine sweeps across (on hover and every few seconds, offset per button), press squishes it, a small pixel icon (Floppy / Cog / Door) sits on the
//     left, and every time the menu opens the buttons slide in one after the other (entranceStagger).
//   * PixelMenuBackdrop (PixelPauseMenu.BuildUI -> BuildBackdrop): soft pixel motes drifting up behind the panel + a darkening vignette over the dimmer.
//   * PixelMenuPanelGlow: a rounded glow behind the main panel that slowly cycles colour and breathes; PixelMenuTitleLine: a light segment sliding along a line under the title.
//   * The SETTINGS and STATS screens get the lighter button animation (PixelMenuFx.DecorateAll: brighten, shine, press; no scaling inside scroll lists).
//   * The docked HUD buttons also get the light animation WITH the idle shine (DecorateHud), and the SHOP gets the same treatment (its buttons, a glow behind the panel and the sliding title line; PixelShop.BuildPanel / CreateButton).
//   * PixelQuitFx: the Quit to... box darkens the screen and pours flickering red neon light round the window (PixelPauseMenu.BuildQuitPanel).
//   * PixelMenuCubeFall (PixelPauseMenu.BuildTitlePlate -> AddCubeFall): every so often a small cube in an unlocked pixel's colour slips out from behind a menu
//     window's title plate and falls (gravity, a little spin) with a glowing light trail, behind the buttons, fading out before the window's bottom (pooled, clipped to the window).
//   Everything runs on unscaled time (the game is paused while the menu shows).
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PixelMenuFx
{
    /// <summary>Small cubes with light trails that drop from behind a window's title plate now and then, behind everything else in the window.</summary>
    public static void AddCubeFall(RectTransform panel, RectTransform plate)
    {
        if (panel == null || plate == null || panel.GetComponentInChildren<PixelMenuCubeFall>(true) != null) return;
        GameObject layer = new GameObject("Cube Fall", typeof(RectTransform), typeof(RectMask2D), typeof(PixelMenuCubeFall));
        layer.transform.SetParent(panel, false);
        layer.transform.SetAsFirstSibling();   // behind the plate, the buttons and everything else in the window
        RectTransform lr = layer.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(8f, 8f); lr.offsetMax = new Vector2(-8f, -8f);   // (clipped just inside the window's rounded edge)
        layer.GetComponent<PixelMenuCubeFall>().plate = plate;
    }

    private const string Pref = "PixelClicker.Setting.FancyMenu";
    private static int cache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache = -1; vignette = null; }

    /// <summary>Settings > Interface > Fancy pause menu (off = the classic menu).</summary>
    public static bool Enabled
    {
        get { if (cache < 0) cache = PixelPrefs.GetInt(Pref, 1); return cache != 0; }
        set { cache = value ? 1 : 0; PixelPrefs.SetInt(Pref, cache); PixelPrefs.Save(); }
    }

    public static void ReloadFromPrefs() { cache = -1; }

    /// <summary>The light animation for one docked HUD button (brighten, shine on hover, press); (the shine, hover and press).</summary>
    public static void DecorateHud(Button button)
    {
        if (button == null || button.GetComponent<PixelFancyButton>() != null || button.GetComponent<Image>() == null) return;
        button.gameObject.AddComponent<PixelFancyButton>().Setup(Color.white, null, 0, true, true, true);   // (+ the shine every few seconds, like the main menu's buttons)
    }

    /// <summary>The main menu's Play / Load / Settings / Quit buttons: brighten, press and the shine (also every few seconds). Follows 'Fancy pause menu'.</summary>
    public static void DecorateTitle(Button button)
    {
        if (button == null || button.GetComponent<PixelFancyButton>() != null || button.GetComponent<Image>() == null) return;
        button.gameObject.AddComponent<PixelFancyButton>().Setup(Color.white, null, 0, true, false, true);
    }

    /// <summary>The light animation (brighten, shine, press) for every button under a screen (Settings, Stats, Save / Load).</summary>
    public static void DecorateAll(Transform root)
    {
        if (root == null) return;
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            if (b.GetComponent<PixelFancyButton>() != null || b.GetComponent<Image>() == null) continue;
            b.gameObject.AddComponent<PixelFancyButton>().Setup(Color.white, null, 0, true);
        }
    }

    /// <summary>Makes a main-menu button animated.</summary>
    public static void Decorate(Button button, Color accent, Texture2D icon, int index)
    {
        if (button == null || button.GetComponent<PixelFancyButton>() != null) return;
        button.gameObject.AddComponent<PixelFancyButton>().Setup(accent, icon, index);
    }

    private static Texture2D vignette;
    public static Texture2D Vignette()
    {
        if (vignette != null) return vignette;
        const int n = 128;
        vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.SmoothStep(0.35f, 1.25f, Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(255f * a * 0.75f));
            }
        vignette.SetPixels32(px);
        vignette.Apply();
        return vignette;
    }

    /// <summary>The motes + vignette behind the panel (child 0 of the dimmer) and the glow behind the main panel.</summary>
    public static void BuildBackdrop(Transform menuRoot, RectTransform mainPanel)
    {
        GameObject back = new GameObject("Fancy Backdrop", typeof(RectTransform), typeof(PixelMenuBackdrop));
        back.transform.SetParent(menuRoot, false);
        back.transform.SetAsFirstSibling();
        RectTransform r = back.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;

        if (mainPanel != null)
        {
            GameObject glow = new GameObject("Fancy Panel Glow", typeof(RectTransform), typeof(Image), typeof(PixelMenuPanelGlow));
            glow.transform.SetParent(menuRoot, false);
            glow.transform.SetSiblingIndex(mainPanel.transform.GetSiblingIndex());   // just behind the panel
            glow.GetComponent<Image>().raycastTarget = false;
            glow.GetComponent<PixelMenuPanelGlow>().target = mainPanel;
            PixelUIKit.StyleBox(glow.GetComponent<Image>());
        }
    }

    /// <summary>The same sliding light line under a window's title, stretched across the window ('y' = distance from the window's top edge, 'margin' = space at both ends).</summary>
    public static void AddWindowLine(Transform window, float y, float margin = 24f)
    {
        if (window == null) return;
        GameObject line = new GameObject("Fancy Title Line", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(PixelMenuTitleLine));
        line.transform.SetParent(window, false);
        Image im = line.GetComponent<Image>();
        im.color = new Color(1f, 1f, 1f, 0.12f);
        im.raycastTarget = false;
        RectTransform r = line.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-2f * margin, 4f);
        r.anchoredPosition = new Vector2(0f, -y);
    }

    /// <summary>A sliding light segment under the title.</summary>
    public static void BuildTitleLine(RectTransform panel, float x, float y, float width, bool neon = false)
    {
        GameObject line = new GameObject("Fancy Title Line", typeof(RectTransform), typeof(Image), typeof(PixelMenuTitleLine));
        if (!neon) line.AddComponent<RectMask2D>();   // (a neon line lets its glow reach past the thin line)
        line.GetComponent<PixelMenuTitleLine>().neon = neon;
        line.transform.SetParent(panel, false);
        Image im = line.GetComponent<Image>();
        im.color = neon ? new Color(0.3f, 0.8f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.12f);
        im.raycastTarget = false;
        RectTransform r = line.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.sizeDelta = new Vector2(width, 4f);
        r.anchoredPosition = new Vector2(x, y);
    }
}

/// <summary>An animated menu button (see the notes at the top of PixelMenuFx.cs). 'light' = a lighter version for the Settings / Stats screens: brighten + shine + press only.</summary>
public class PixelFancyButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Color accent;
    private Texture2D icon;
    private int index;
    private bool lightMode, hudSetting, idleSheen;
    private Button button;
    private Image image;
    private RectTransform rect, labelRect;
    private CanvasGroup group;
    private Color baseColor;
    private Color writtenColor;   // the colour this script last gave the image (anything else = someone else recoloured it)
    private bool wroteColor;
    private Vector2 basePos, baseLabelPos;
    private bool built, hover, press;
    private float hoverT, enterT = 1f, shineT = 2f, idleShine, pressVis, pressUntil;
    private GameObject fxRoot;
    private Image stripe;
    private RectTransform shine;
    private float shineRange;

    public void Setup(Color accentColor, Texture2D iconTexture, int i, bool lighter = false, bool followHudSetting = false, bool sheenOften = false) { accent = accentColor; icon = iconTexture; index = i; lightMode = lighter; hudSetting = followHudSetting; idleSheen = sheenOften; }

    private void Build()
    {
        built = true;
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        if (!lightMode)
        {
            group = gameObject.GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }
        baseColor = image.color;
        basePos = rect.anchoredPosition;
        Transform label = transform.Find("Label");
        if (label != null) { labelRect = label as RectTransform; baseLabelPos = labelRect.anchoredPosition; }

        fxRoot = new GameObject("Fancy FX", typeof(RectTransform), typeof(RectMask2D));
        fxRoot.transform.SetParent(transform, false);
        fxRoot.transform.SetAsLastSibling();
        PixelUIKit.Stretch(fxRoot.GetComponent<RectTransform>());

        // The light shine: a slanted bar that sweeps across.
        GameObject s = new GameObject("Shine", typeof(RectTransform), typeof(Image));
        s.transform.SetParent(fxRoot.transform, false);
        Image si = s.GetComponent<Image>();
        si.color = new Color(1f, 1f, 1f, 0f);   // invisible until a sweep starts (it used to sit at the left edge as a pale slanted bar)
        si.raycastTarget = false;
        shine = s.GetComponent<RectTransform>();
        shine.anchoredPosition = new Vector2(-200f, 0f);
        shine.anchorMin = shine.anchorMax = new Vector2(0f, 0.5f);
        shine.pivot = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(Mathf.Clamp(rect.rect.width * 0.18f, 14f, hudSetting ? 70f : 46f), rect.rect.height * 2.4f);
        shine.localRotation = Quaternion.Euler(0f, 0f, 18f);
        shineRange = rect.rect.width;

        if (!lightMode)
        {
            // The accent stripe on the left.
            GameObject st = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            st.transform.SetParent(fxRoot.transform, false);
            stripe = st.GetComponent<Image>();
            stripe.color = accent;
            stripe.raycastTarget = false;
            RectTransform sr = stripe.rectTransform;
            sr.anchorMin = new Vector2(0f, 0.12f); sr.anchorMax = new Vector2(0f, 0.88f); sr.pivot = new Vector2(0f, 0.5f);
            sr.anchoredPosition = new Vector2(6f, 0f);
            sr.sizeDelta = new Vector2(6f, 0f);

            if (icon != null)
            {
                GameObject ig = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                ig.transform.SetParent(fxRoot.transform, false);
                RawImage ri = ig.GetComponent<RawImage>();
                ri.texture = icon;
                ri.raycastTarget = false;
                RectTransform irect = ri.rectTransform;
                float side = Mathf.Min(rect.rect.height * 0.55f, 44f);
                irect.anchorMin = irect.anchorMax = new Vector2(0f, 0.5f);
                irect.pivot = new Vector2(0f, 0.5f);
                irect.sizeDelta = new Vector2(side, side);
                irect.anchoredPosition = new Vector2(26f, 0f);
            }
            idleShine = Random.Range(2f, 7f) + index * 0.9f;
        }
        else idleShine = idleSheen ? Random.Range(2f, 8f) : 1e9f;
    }

    private void OnEnable() { if (built) enterT = 0f; else enterT = -1f; hoverT = 0f; press = hover = false; }

    private void Update()
    {
        if (!built) Build();
        if (enterT < 0f) enterT = 0f;
        bool on = hudSetting || PixelMenuFx.Enabled;   // (the docked HUD buttons always get their hover sheen and sink)
        if (fxRoot.activeSelf != on) fxRoot.SetActive(on);
        float dt = Time.unscaledDeltaTime;
        if (!on)
        {
            if (!lightMode) { rect.localScale = Vector3.one; rect.anchoredPosition = basePos; group.alpha = 1f; image.color = baseColor; wroteColor = false; if (labelRect != null) labelRect.anchoredPosition = baseLabelPos; }
            else if (rect.localScale != Vector3.one) rect.localScale = Vector3.one;
            return;
        }

        if (!lightMode)
        {
            // Entrance: slide in from the left one after the other.
            enterT += dt;
            float e = Mathf.Clamp01((enterT - index * 0.055f) / 0.32f);
            float ease = 1f - (1f - e) * (1f - e) * (1f - e);
            group.alpha = ease;
            rect.anchoredPosition = basePos + new Vector2(-70f * (1f - ease), 0f);
        }

        // Someone else recoloured the button while the glow was on (the Shop's tab turning blue on a click): take that as the new
        // base at once, instead of painting over it until the mouse leaves.
        if (wroteColor && image.color != writtenColor) baseColor = image.color;

        hoverT = Mathf.MoveTowards(hoverT, hover ? 1f : 0f, dt * 7f);
        float h = hoverT * hoverT * (3f - 2f * hoverT);
        float scale = (lightMode ? 1f : Mathf.Lerp(1f, 1.04f, h)) * (press ? 0.965f : 1f);
        if (!hudSetting && (button == null || button.interactable)) EaseScale(scale, dt);   // (a button something else is animating - the title's shrink - is left alone)

        if (hudSetting)
        {
            // The docked HUD buttons SINK: a little when the mouse is over them, all the way while pressed (and for a moment after, so a quick click still shows it).
            pressVis = Mathf.MoveTowards(pressVis, press || Time.unscaledTime < pressUntil ? 1f : 0f, dt * 14f);
            float hoverDepth = h * 0.35f;
            float depth = Mathf.Max(hoverDepth, pressVis);
            float dark = Mathf.Lerp(1f, 0.7f, depth);
            if (hoverT <= 0.001f && pressVis <= 0.001f) { baseColor = image.color; wroteColor = false; }
            else { image.color = new Color(baseColor.r * dark, baseColor.g * dark, baseColor.b * dark, baseColor.a); writtenColor = image.color; wroteColor = true; }
            SetLabelPos(baseLabelPos + new Vector2(0f, -(1.5f * h + 3.5f * pressVis)));
            float s2 = Mathf.Lerp(1f, 0.978f, h) * Mathf.Lerp(1f, 0.96f, pressVis);
            if (button == null || button.interactable) EaseScale(s2, dt);
        }
        else if (lightMode && hoverT <= 0.001f) { baseColor = image.color; wroteColor = false; }   // (follow outside colour changes while nothing is happening)
        else
        {
            image.color = Color.Lerp(baseColor, new Color(Mathf.Min(1f, baseColor.r * 1.28f + 0.05f), Mathf.Min(1f, baseColor.g * 1.28f + 0.05f), Mathf.Min(1f, baseColor.b * 1.28f + 0.05f), baseColor.a), h);
            writtenColor = image.color; wroteColor = true;
        }

        if (!lightMode)
        {
            SetLabelPos(baseLabelPos + new Vector2(10f * h, 0f));
            stripe.rectTransform.sizeDelta = new Vector2(6f + 10f * h, 0f);
        }
        if (!lightMode || idleSheen)
        {
            idleShine -= dt;
            if (idleShine <= 0f) { idleShine = Random.Range(6f, 9f); shineT = 0f; }
        }
        if (shineT <= 1.2f)
        {
            shineT += dt * 1.9f;
            float tt = Mathf.Clamp01(shineT);
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-60f, shineRange + 60f, tt), 0f);
            Image si = ShineImage();
            Color c = si.color; c.a = (hudSetting ? 0.5f : 0.24f) * Mathf.Sin(tt * Mathf.PI); si.color = c;
        }
        else if (shine.anchoredPosition.x > -150f) { shine.anchoredPosition = new Vector2(-200f, 0f); Image si2 = ShineImage(); Color c2 = si2.color; c2.a = 0f; si2.color = c2; }   // (parked, invisible)
    }

    // A button at rest kept rewriting its scale and label position every frame (each write makes the HUD canvas redo its layout): write only on a change.
    private void EaseScale(float target, float dt)
    {
        Vector3 want = Vector3.one * target;
        Vector3 now = rect.localScale;
        if (now == want) return;
        Vector3 next = Vector3.Lerp(now, want, 1f - Mathf.Exp(-dt * 20f));
        rect.localScale = (next - want).sqrMagnitude < 1e-8f ? want : next;   // (snaps the last tiny step so it really comes to rest)
    }

    private void SetLabelPos(Vector2 pos)
    {
        if (labelRect != null && labelRect.anchoredPosition != pos) labelRect.anchoredPosition = pos;
    }

    private Image shineImage;
    private Image ShineImage() => shineImage != null ? shineImage : (shineImage = shine.GetComponent<Image>());

    public void OnPointerEnter(PointerEventData d)
    {
        hover = true; shineT = 0f;
        if (hudSetting) PixelAudio.Play("ui_click", 1.7f, 0.22f);   // a very quiet tick as the button sinks
    }
    public void OnPointerExit(PointerEventData d) { hover = false; press = false; }
    public void OnPointerDown(PointerEventData d) { press = true; }
    public void OnPointerUp(PointerEventData d) { press = false; pressUntil = Time.unscaledTime + 0.18f; }
}

/// <summary>Soft pixel motes drifting up behind the pause menu + a vignette.</summary>
public class PixelMenuBackdrop : MonoBehaviour
{
    private const int Count = 30;
    private RectTransform[] motes;
    private Image[] images;
    private float[] speed, drift, phase, baseAlpha;
    private RawImage vig;
    private bool built;

    private void Build()
    {
        built = true;
        GameObject v = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage));
        v.transform.SetParent(transform, false);
        vig = v.GetComponent<RawImage>();
        vig.texture = PixelMenuFx.Vignette();
        vig.raycastTarget = false;
        PixelUIKit.Stretch(v.GetComponent<RectTransform>());

        motes = new RectTransform[Count]; images = new Image[Count];
        speed = new float[Count]; drift = new float[Count]; phase = new float[Count]; baseAlpha = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            GameObject g = new GameObject("Mote", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(transform, false);
            images[i] = g.GetComponent<Image>();
            images[i].raycastTarget = false;
            motes[i] = g.GetComponent<RectTransform>();
            motes[i].anchorMin = motes[i].anchorMax = motes[i].pivot = new Vector2(0f, 0f);
            float size = Random.Range(6f, 20f);
            motes[i].sizeDelta = new Vector2(size, size);
            motes[i].anchoredPosition = new Vector2(Random.value * 1920f, Random.value * 1080f);
            speed[i] = Random.Range(18f, 60f) * (size < 11f ? 0.7f : 1f);
            drift[i] = Random.Range(-12f, 12f);
            phase[i] = Random.value * 6.28f;
            baseAlpha[i] = Random.Range(0.06f, 0.2f);
            Color c = Color.HSVToRGB(Random.Range(0.5f, 0.95f), 0.35f, 1f);
            images[i].color = new Color(c.r, c.g, c.b, baseAlpha[i]);
        }
    }

    private void Update()
    {
        if (!built) Build();
        bool on = PixelMenuFx.Enabled;
        if (vig.enabled != on) { vig.enabled = on; foreach (Image i in images) i.enabled = on; }
        if (!on) return;
        float dt = Time.unscaledDeltaTime, t = Time.unscaledTime;
        RectTransform parent = (RectTransform)transform;
        float w = Mathf.Max(400f, parent.rect.width), h = Mathf.Max(300f, parent.rect.height);
        for (int i = 0; i < Count; i++)
        {
            Vector2 p = motes[i].anchoredPosition;
            p.y += speed[i] * dt;
            p.x += (drift[i] + Mathf.Sin(t * 0.6f + phase[i]) * 10f) * dt;
            if (p.y > h + 30f) { p.y = -30f; p.x = Random.value * w; }
            if (p.x > w + 30f) p.x = -30f; else if (p.x < -30f) p.x = w + 30f;
            motes[i].anchoredPosition = p;
            motes[i].localRotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(t + phase[i]) * 20f);
            Color c = images[i].color; c.a = baseAlpha[i] * (0.65f + 0.35f * Mathf.Sin(t * 1.4f + phase[i])); images[i].color = c;
        }
    }
}

/// <summary>A soft rounded glow behind the main panel that cycles colour and breathes.</summary>
public class PixelMenuPanelGlow : MonoBehaviour
{
    public RectTransform target;
    private Image image;
    private RectTransform rect;

    private void Update()
    {
        if (image == null) { image = GetComponent<Image>(); rect = GetComponent<RectTransform>(); }
        bool on = PixelMenuFx.Enabled && target != null && target.gameObject.activeInHierarchy;
        if (image.enabled != on) image.enabled = on;
        if (!on) return;
        float t = Time.unscaledTime;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        float grow = 26f + 8f * Mathf.Sin(t * 1.7f);
        rect.sizeDelta = target.rect.size + new Vector2(grow, grow);
        rect.anchoredPosition = target.anchoredPosition;
        rect.localScale = target.localScale;   // (follows a window's pop-open animation)
        Color c = Color.HSVToRGB(Mathf.Repeat(t * 0.04f, 1f), 0.55f, 1f);
        c.a = 0.32f + 0.1f * Mathf.Sin(t * 1.7f);
        image.color = c;
    }
}

/// <summary>A light segment sliding along a thin line (under the pause menu's title).</summary>
public class PixelMenuTitleLine : MonoBehaviour
{
    /// <summary>A bright neon light spot (a soft halo round a hot core) instead of the plain sliding segment.</summary>
    public bool neon;
    private RectTransform seg;
    private Image line;
    private static Sprite haloSprite;

    private static Sprite HaloSprite()
    {
        if (haloSprite != null) return haloSprite;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(false, true);
        haloSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return haloSprite;
    }

    private void Update()
    {
        if (seg == null)
        {
            line = GetComponent<Image>();
            if (neon)
            {
                GameObject root = new GameObject("Neon Spot", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                seg = root.GetComponent<RectTransform>();
                seg.anchorMin = seg.anchorMax = new Vector2(0f, 0.5f); seg.pivot = new Vector2(0.5f, 0.5f);
                seg.sizeDelta = Vector2.zero;
                GameObject halo = new GameObject("Halo", typeof(RectTransform), typeof(Image));
                halo.transform.SetParent(root.transform, false);
                Image hi = halo.GetComponent<Image>();
                hi.sprite = HaloSprite(); hi.color = new Color(0.2f, 0.85f, 1f, 0.95f); hi.raycastTarget = false;
                halo.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 34f);
                GameObject core = new GameObject("Core", typeof(RectTransform), typeof(Image));
                core.transform.SetParent(root.transform, false);
                Image ci = core.GetComponent<Image>();
                ci.sprite = HaloSprite(); ci.color = new Color(0.9f, 1f, 1f, 1f); ci.raycastTarget = false;
                core.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 9f);
            }
            else
            {
                GameObject s = new GameObject("Segment", typeof(RectTransform), typeof(Image));
                s.transform.SetParent(transform, false);
                s.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
                s.GetComponent<Image>().raycastTarget = false;
                seg = s.GetComponent<RectTransform>();
                seg.anchorMin = new Vector2(0f, 0f); seg.anchorMax = new Vector2(0f, 1f); seg.pivot = new Vector2(0.5f, 0.5f);
                seg.sizeDelta = new Vector2(90f, 0f);
            }
        }
        bool on = PixelMenuFx.Enabled;
        if (line.enabled != on) { line.enabled = on; seg.gameObject.SetActive(on); }
        if (!on) return;
        float w = ((RectTransform)transform).rect.width;
        float t = Mathf.Repeat(Time.unscaledTime / (neon ? 1.8f : 2.4f), 1f);
        seg.anchoredPosition = new Vector2(Mathf.Lerp(-120f, w + 120f, neon ? t : t * t * (3f - 2f * t)), 0f);
    }
}

/// <summary>
/// The 'Quit to...' box: while it shows, the screen goes almost black (with a faint red vignette) and red neon light pours out from around the window - two soft glow layers that
/// breathe, a thin neon tube round the edge, and a flicker as it switches on and now and then afterwards. Created by PixelPauseMenu.BuildQuitPanel (PixelQuitFx.Build), follows the
/// panel's rect and pop animation, runs on unscaled time and is off when 'Fancy pause menu' is off.
/// </summary>
public class PixelQuitFx : MonoBehaviour
{
    private RectTransform target, glowOuter, glowInner, tube;
    private Image darkImage, vignetteImage, outerImage, innerImage, tubeImage;
    private float t, flickerUntil, flickerLevel = 1f, nextFlicker = 3f, onTime;
    private bool wasOn;

    private static Sprite glowSprite, tubeSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { glowSprite = null; tubeSprite = null; }

    /// <summary>Distance from point (x, y) to a rounded rectangle [lo, hi] x [lo, hi] with corner radius r (negative inside).</summary>
    private static float RoundedDistance(float x, float y, float lo, float hi, float r)
    {
        float cx = (lo + hi) * 0.5f, half = (hi - lo) * 0.5f - r;
        float dx = Mathf.Abs(x - cx) - half, dy = Mathf.Abs(y - cx) - half;
        float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
        return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
    }

    /// <summary>A soft glow: solid inside a rounded rectangle, fading out over the rest of the texture (9-sliced).</summary>
    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int n = 96;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, 40f, n - 40f, 14f);
                float a = d <= 0f ? 1f : Mathf.Pow(Mathf.Clamp01(1f - d / 40f), 2.2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
        tex.SetPixels32(px);
        tex.Apply();
        glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(46f, 46f, 46f, 46f));
        return glowSprite;
    }

    /// <summary>A thin bright ring (the neon tube), 9-sliced.</summary>
    private static Sprite TubeSprite()
    {
        if (tubeSprite != null) return tubeSprite;
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color32[] px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(RoundedDistance(x + 0.5f, y + 0.5f, 6f, n - 6f, 20f));
                float a = Mathf.Clamp01(1.6f - d * 0.8f);   // ~2 px of tube with soft sides
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * a));
            }
        tex.SetPixels32(px);
        tex.Apply();
        tubeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(26f, 26f, 26f, 26f));
        return tubeSprite;
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Image.Type type)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.sprite = sprite;
        im.type = type;
        im.raycastTarget = false;
        im.color = new Color(1f, 1f, 1f, 0f);
        return im;
    }

    /// <summary>Puts the effect behind 'panel' (inside the panel's parent, just under it).</summary>
    public static void Build(GameObject panel)
    {
        if (panel == null || panel.transform.parent == null) return;
        Transform parent = panel.transform.parent;
        GameObject root = new GameObject("Quit Fx", typeof(RectTransform), typeof(PixelQuitFx));
        root.transform.SetParent(parent, false);
        PixelQuitFx fx = root.GetComponent<PixelQuitFx>();
        fx.target = panel.GetComponent<RectTransform>();
        RectTransform rr = root.GetComponent<RectTransform>();
        rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
        fx.darkImage = MakeImage(root.transform, "Dark", null, Image.Type.Simple);
        PixelUIKit.Stretch(fx.darkImage.rectTransform);
        fx.vignetteImage = MakeImage(root.transform, "Red Vignette", null, Image.Type.Simple);
        PixelUIKit.Stretch(fx.vignetteImage.rectTransform);
        fx.outerImage = MakeImage(root.transform, "Glow Outer", GlowSprite(), Image.Type.Sliced);
        fx.innerImage = MakeImage(root.transform, "Glow Inner", GlowSprite(), Image.Type.Sliced);
        fx.tubeImage = MakeImage(root.transform, "Neon Tube", TubeSprite(), Image.Type.Sliced);
        fx.glowOuter = fx.outerImage.rectTransform; fx.glowInner = fx.innerImage.rectTransform; fx.tube = fx.tubeImage.rectTransform;
        // Everything sits just behind the panel.
        root.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());
        fx.Hide();
    }

    private void Hide() { darkImage.enabled = vignetteImage.enabled = outerImage.enabled = innerImage.enabled = tubeImage.enabled = false; }

    private void Update()
    {
        bool on = PixelMenuFx.Enabled && target != null && target.gameObject.activeInHierarchy;
        float dt = Time.unscaledDeltaTime;
        if (on && !wasOn) { onTime = 0f; t = 0f; nextFlicker = Random.Range(2.5f, 5f); }
        wasOn = on;
        t = Mathf.MoveTowards(t, on ? 1f : 0f, dt / (on ? 0.4f : 0.18f));
        if (t <= 0.001f) { if (darkImage.enabled) Hide(); return; }
        if (!darkImage.enabled) darkImage.enabled = vignetteImage.enabled = outerImage.enabled = innerImage.enabled = tubeImage.enabled = true;
        if (vignetteImage.sprite == null) { Texture2D v = PixelMenuFx.Vignette(); vignetteImage.sprite = Sprite.Create(v, new Rect(0, 0, v.width, v.height), new Vector2(0.5f, 0.5f)); }
        onTime += dt;

        // Neon flicker: erratic as it switches on, then an occasional quick stutter.
        float flicker = 1f;
        if (onTime < 0.9f) flicker = Mathf.Sin(onTime * 90f) * Mathf.Sin(onTime * 37f) > -0.1f ? Mathf.Clamp01(onTime / 0.9f + 0.25f) : 0.12f;
        else if (Time.unscaledTime > nextFlicker) { flickerUntil = Time.unscaledTime + 0.22f; nextFlicker = Time.unscaledTime + Random.Range(3f, 7f); }
        if (Time.unscaledTime < flickerUntil) flickerLevel = Mathf.Sin(Time.unscaledTime * 80f) > 0f ? 1f : 0.3f; else flickerLevel = 1f;
        flicker = onTime < 0.9f ? flicker : flickerLevel;

        float ease = t * t * (3f - 2f * t);
        float breathe = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
        darkImage.color = new Color(0f, 0f, 0f, 0.9f * ease);
        vignetteImage.color = new Color(0.8f, 0.02f, 0.02f, 0.35f * ease * flicker);

        FollowPanel(glowOuter, 30f + 22f * breathe + 16f * ease);
        outerImage.color = new Color(1f, 0.05f, 0.05f, (0.45f + 0.2f * breathe) * ease * flicker);
        FollowPanel(glowInner, 6f + 8f * breathe);
        innerImage.color = new Color(1f, 0.18f, 0.12f, (0.7f + 0.15f * breathe) * ease * flicker);
        FollowPanel(tube, 5f);
        tubeImage.color = new Color(1f, 0.55f, 0.5f, ease * flicker);
    }

    /// <summary>Makes 'r' as big as the panel plus 'grow' on every side, at the panel's place and scale (the panel pops open).</summary>
    private void FollowPanel(RectTransform r, float grow)
    {
        r.anchorMin = target.anchorMin; r.anchorMax = target.anchorMax; r.pivot = target.pivot;
        r.anchoredPosition = target.anchoredPosition;
        r.localScale = target.localScale;
        r.sizeDelta = target.sizeDelta + new Vector2(grow * 2f, grow * 2f);
        if (target.anchorMin != target.anchorMax) { r.offsetMin = target.offsetMin - new Vector2(grow, grow); r.offsetMax = target.offsetMax + new Vector2(grow, grow); }
    }
}


/// <summary>
/// The falling cubes behind a menu window (see PixelMenuFx.AddCubeFall): now and then a small cube in an unlocked pixel's colour slips out from under
/// the title plate's bottom edge and falls with a glowing trail, spinning a little, fading out before the window's bottom. Pooled; fancy menu only.
/// </summary>
public class PixelMenuCubeFall : MonoBehaviour
{
    public RectTransform plate;

    private const int MaxCubes = 10;
    private const float Gravity = 900f, MinGap = 0.8f, MaxGap = 2.2f;

    private sealed class Cube
    {
        public RectTransform body, trail;
        public Image bodyImage, trailImage;
        public Vector2 pos, vel;
        public float angle, spin, size, age;
        public Color color;
        public bool alive;
    }

    private readonly Cube[] cubes = new Cube[MaxCubes];
    private RectTransform rect;
    private float nextSpawn = 0.4f;
    private static Sprite trailSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { trailSprite = null; }

    /// <summary>A soft vertical fade (clear at the top, solid at the bottom) for the light trails.</summary>
    private static Sprite TrailSprite()
    {
        if (trailSprite != null) return trailSprite;
        const int h = 32;
        Texture2D tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[4 * h];
        for (int y = 0; y < h; y++)
        {
            float a = Mathf.Pow(y / (h - 1f), 1.6f);   // (y = 0 is the bottom of the texture: the end at the cube)
            a = 1f - a;
            for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a * (x == 0 || x == 3 ? 0.45f : 1f));   // (softer edges)
        }
        tex.SetPixels(px);
        tex.Apply();
        trailSprite = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0f), 100f);
        return trailSprite;
    }

    private void Awake() { rect = GetComponent<RectTransform>(); }

    private void OnDisable()
    {
        foreach (Cube c in cubes) if (c != null && c.alive) Kill(c);   // (a fresh rain each time the window opens)
        nextSpawn = 0.4f;
    }

    private Cube Make()
    {
        Cube c = new Cube();
        GameObject t = new GameObject("Trail", typeof(RectTransform), typeof(Image));
        t.transform.SetParent(transform, false);
        c.trail = t.GetComponent<RectTransform>();
        c.trailImage = t.GetComponent<Image>();
        c.trailImage.sprite = TrailSprite();
        c.trailImage.raycastTarget = false;
        c.trail.pivot = new Vector2(0.5f, 0f);
        GameObject b = new GameObject("Cube", typeof(RectTransform), typeof(Image));
        b.transform.SetParent(transform, false);
        c.body = b.GetComponent<RectTransform>();
        c.bodyImage = b.GetComponent<Image>();
        c.bodyImage.raycastTarget = false;
        c.trail.anchorMin = c.trail.anchorMax = c.body.anchorMin = c.body.anchorMax = new Vector2(0.5f, 0.5f);
        return c;
    }

    private static void Kill(Cube c)
    {
        c.alive = false;
        c.body.gameObject.SetActive(false);
        c.trail.gameObject.SetActive(false);
    }

    private static Color RandomPixelColor()
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        if (clicker != null && clicker.Tiers != null && clicker.Tiers.Length > 0)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                PixelClicker.PixelTier t = clicker.Tiers[Random.Range(0, clicker.Tiers.Length)];
                if (t != null && t.unlocked) return t.UIColor;
            }
        }
        return Color.HSVToRGB(Random.value, 0.55f, 1f);
    }

    private void Spawn()
    {
        if (plate == null || rect == null) return;
        Cube c = null;
        for (int i = 0; i < cubes.Length; i++)
        {
            if (cubes[i] == null) cubes[i] = Make();
            if (!cubes[i].alive) { c = cubes[i]; break; }
        }
        if (c == null) return;   // (all ten are falling)
        // Start just above the plate's bottom edge (hidden behind it), somewhere along its width.
        Vector3[] w = new Vector3[4];
        plate.GetWorldCorners(w);
        Vector2 bl = rect.InverseTransformPoint(w[0]), br = rect.InverseTransformPoint(w[3]);
        float inset = (br.x - bl.x) * 0.08f;
        c.size = Random.Range(9f, 16f);
        c.pos = new Vector2(Random.Range(bl.x + inset, br.x - inset), bl.y + c.size);
        c.vel = new Vector2(Random.Range(-30f, 30f), Random.Range(-60f, -20f));
        c.angle = Random.Range(0f, 90f);
        c.spin = Random.Range(-160f, 160f);
        c.age = 0f;
        c.color = RandomPixelColor();
        c.bodyImage.color = c.color;
        c.body.sizeDelta = new Vector2(c.size, c.size);
        c.alive = true;
        c.body.gameObject.SetActive(true);
        c.trail.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (rect == null) return;
        bool on = PixelMenuFx.Enabled;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (on)
        {
            nextSpawn -= dt;
            if (nextSpawn <= 0f) { Spawn(); nextSpawn = Random.Range(MinGap, MaxGap); }
        }
        float bottom = rect.rect.yMin;
        for (int i = 0; i < cubes.Length; i++)
        {
            Cube c = cubes[i];
            if (c == null || !c.alive) continue;
            if (!on) { Kill(c); continue; }
            c.age += dt;
            c.vel.y -= Gravity * dt;
            c.pos += c.vel * dt;
            c.angle += c.spin * dt;
            if (c.pos.y < bottom - 20f) { Kill(c); continue; }
            float fade = Mathf.Clamp01((c.pos.y - bottom) / 120f) * Mathf.Clamp01(c.age / 0.12f);
            c.body.anchoredPosition = c.pos;
            c.body.localRotation = Quaternion.Euler(0f, 0f, c.angle);
            c.bodyImage.color = new Color(c.color.r, c.color.g, c.color.b, fade);
            // The light trail: points back along the way it came, longer the faster it falls, a brighter tint of its colour.
            float speed = c.vel.magnitude;
            c.trail.anchoredPosition = c.pos;
            c.trail.sizeDelta = new Vector2(c.size * 0.6f, Mathf.Clamp(speed * 0.12f, 8f, 140f));
            c.trail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-c.vel.x, -c.vel.y) * -Mathf.Rad2Deg);
            Color glow = Color.Lerp(c.color, Color.white, 0.35f);
            c.trailImage.color = new Color(glow.r, glow.g, glow.b, 0.75f * fade);
        }
    }
}
