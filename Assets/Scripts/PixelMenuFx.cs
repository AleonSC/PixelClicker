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
//   * PixelDockFx on every docked HUD button (PixelHud.Dock / DockInGroup): the button MATERIALIZES out of pixels each time it slides out (own toggle: 'Pixel-assemble menu buttons', PixelMenuFx.HudEnabled, PlayerPrefs PixelClicker.Setting.FancyHud).
//   Everything runs on unscaled time (the game is paused while the menu shows).
// ---------------------------------------------------
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PixelMenuFx
{
    private const string Pref = "PixelClicker.Setting.FancyMenu";
    private static int cache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { cache = -1; hudCache = -1; vignette = null; }

    /// <summary>Settings > Interface > Fancy pause menu (off = the classic menu).</summary>
    public static bool Enabled
    {
        get { if (cache < 0) cache = PlayerPrefs.GetInt(Pref, 1); return cache != 0; }
        set { cache = value ? 1 : 0; PlayerPrefs.SetInt(Pref, cache); PlayerPrefs.Save(); }
    }

    private const string HudPref = "PixelClicker.Setting.FancyHud";
    private static int hudCache = -1;

    /// <summary>Settings > Interface > 'Pixel-assemble menu buttons': the docked HUD buttons materialize out of pixels (PixelDockFx). On by default.</summary>
    public static bool HudEnabled
    {
        get { if (hudCache < 0) hudCache = PlayerPrefs.GetInt(HudPref, 1); return hudCache != 0; }
        set { hudCache = value ? 1 : 0; PlayerPrefs.SetInt(HudPref, hudCache); PlayerPrefs.Save(); }
    }

    public static void ReloadFromPrefs() { cache = -1; hudCache = -1; }

    /// <summary>The light animation (brighten, shine, press) for every button under a screen (Settings, Stats).</summary>
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

    /// <summary>A sliding light segment under the title.</summary>
    public static void BuildTitleLine(RectTransform panel, float x, float y, float width)
    {
        GameObject line = new GameObject("Fancy Title Line", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(PixelMenuTitleLine));
        line.transform.SetParent(panel, false);
        Image im = line.GetComponent<Image>();
        im.color = new Color(1f, 1f, 1f, 0.12f);
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
    private bool light;
    private Button button;
    private Image image;
    private RectTransform rect, labelRect;
    private CanvasGroup group;
    private Color baseColor;
    private Vector2 basePos, baseLabelPos;
    private bool built, hover, press;
    private float hoverT, enterT = 1f, shineT = 2f, idleShine;
    private GameObject fxRoot;
    private Image stripe;
    private RectTransform shine;
    private float shineRange;

    public void Setup(Color accentColor, Texture2D iconTexture, int i, bool lighter = false) { accent = accentColor; icon = iconTexture; index = i; light = lighter; }

    private void Build()
    {
        built = true;
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        if (!light)
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
        si.color = new Color(1f, 1f, 1f, 0.22f);
        si.raycastTarget = false;
        shine = s.GetComponent<RectTransform>();
        shine.anchorMin = shine.anchorMax = new Vector2(0f, 0.5f);
        shine.pivot = new Vector2(0.5f, 0.5f);
        shine.sizeDelta = new Vector2(Mathf.Clamp(rect.rect.width * 0.18f, 14f, 46f), rect.rect.height * 2.4f);
        shine.localRotation = Quaternion.Euler(0f, 0f, 18f);
        shineRange = rect.rect.width;

        if (!light)
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
        else idleShine = 1e9f;
    }

    private void OnEnable() { if (built) enterT = 0f; else enterT = -1f; hoverT = 0f; press = hover = false; }

    private void Update()
    {
        if (!built) Build();
        if (enterT < 0f) enterT = 0f;
        bool on = PixelMenuFx.Enabled;
        if (fxRoot.activeSelf != on) fxRoot.SetActive(on);
        float dt = Time.unscaledDeltaTime;
        if (!on)
        {
            if (!light) { rect.localScale = Vector3.one; rect.anchoredPosition = basePos; group.alpha = 1f; image.color = baseColor; if (labelRect != null) labelRect.anchoredPosition = baseLabelPos; }
            else if (rect.localScale != Vector3.one) rect.localScale = Vector3.one;
            return;
        }

        if (!light)
        {
            // Entrance: slide in from the left one after the other.
            enterT += dt;
            float e = Mathf.Clamp01((enterT - index * 0.055f) / 0.32f);
            float ease = 1f - (1f - e) * (1f - e) * (1f - e);
            group.alpha = ease;
            rect.anchoredPosition = basePos + new Vector2(-70f * (1f - ease), 0f);
        }

        hoverT = Mathf.MoveTowards(hoverT, hover ? 1f : 0f, dt * 7f);
        float h = hoverT * hoverT * (3f - 2f * hoverT);
        float scale = (light ? 1f : Mathf.Lerp(1f, 1.04f, h)) * (press ? 0.965f : 1f);
        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * scale, 1f - Mathf.Exp(-dt * 20f));

        if (light && hoverT <= 0.001f) baseColor = image.color;   // (follow outside colour changes while nothing is happening)
        else image.color = Color.Lerp(baseColor, new Color(Mathf.Min(1f, baseColor.r * 1.28f + 0.05f), Mathf.Min(1f, baseColor.g * 1.28f + 0.05f), Mathf.Min(1f, baseColor.b * 1.28f + 0.05f), baseColor.a), h);

        if (!light)
        {
            if (labelRect != null) labelRect.anchoredPosition = baseLabelPos + new Vector2(10f * h, 0f);
            stripe.rectTransform.sizeDelta = new Vector2(6f + 10f * h, 0f);
            idleShine -= dt;
            if (idleShine <= 0f) { idleShine = Random.Range(6f, 9f); shineT = 0f; }
        }
        if (shineT <= 1.2f)
        {
            shineT += dt * 1.9f;
            float tt = Mathf.Clamp01(shineT);
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-60f, shineRange + 60f, tt), 0f);
            Image si = shine.GetComponent<Image>();
            Color c = si.color; c.a = 0.24f * Mathf.Sin(tt * Mathf.PI); si.color = c;
        }
    }

    public void OnPointerEnter(PointerEventData d) { hover = true; shineT = 0f; }
    public void OnPointerExit(PointerEventData d) { hover = false; press = false; }
    public void OnPointerDown(PointerEventData d) { press = true; }
    public void OnPointerUp(PointerEventData d) { press = false; }
}

/// <summary>
/// The docked HUD buttons (Inventory, Shop, Log, Crafting...) MATERIALIZE out of pixels when they slide out: a grid of little squares flies in from the screen edge, settles into the
/// button's shape and fades while the button itself fades in. Triggered when the button slides out (SlideAmount passes 0.18) and when it first shows. Toggle: PixelMenuFx.HudEnabled.
/// </summary>
public class PixelDockFx : MonoBehaviour
{
    private const int Cols = 12, Rows = 3;
    private const float Total = 0.62f;

    private PixelDockedButton dock;
    private RectTransform rect, container;
    private CanvasGroup group;
    private Image buttonImage;
    private RectTransform[] cells;
    private Image[] cellImages;
    private float[] delay, jitterX, jitterY;
    private float t = 99f, lastStart = -9f;
    private bool wasVisible, alphaTouched;
    private Vector2 fromDir = new Vector2(-1f, 0f);

    private void Awake()
    {
        dock = GetComponent<PixelDockedButton>();
        rect = GetComponent<RectTransform>();
        buttonImage = GetComponent<Image>();
    }

    private void OnEnable() { wasVisible = false; }

    private void OnDisable() { if (container != null) container.gameObject.SetActive(false); ResetAlpha(); }

    private void ResetAlpha() { if (alphaTouched && group != null) { group.alpha = 1f; alphaTouched = false; } }

    private void Build()
    {
        if (container != null || rect == null || rect.parent == null) return;
        GameObject c = new GameObject("Pixel FX", typeof(RectTransform));
        c.transform.SetParent(rect.parent, false);
        container = c.GetComponent<RectTransform>();
        container.anchorMin = rect.anchorMin; container.anchorMax = rect.anchorMax; container.pivot = rect.pivot;
        cells = new RectTransform[Cols * Rows];
        cellImages = new Image[Cols * Rows];
        delay = new float[Cols * Rows]; jitterX = new float[Cols * Rows]; jitterY = new float[Cols * Rows];
        for (int i = 0; i < cells.Length; i++)
        {
            GameObject g = new GameObject("Pixel", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(container, false);
            cellImages[i] = g.GetComponent<Image>();
            cellImages[i].raycastTarget = false;
            cells[i] = g.GetComponent<RectTransform>();
            cells[i].anchorMin = cells[i].anchorMax = cells[i].pivot = new Vector2(0.5f, 0.5f);
        }
        group = gameObject.GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void StartAssemble()
    {
        Build();
        if (container == null) return;
        t = 0f;
        lastStart = Time.unscaledTime;
        container.SetAsLastSibling();
        container.gameObject.SetActive(true);
        Vector2 anchor = rect.anchorMin;
        fromDir = new Vector2(anchor.x < 0.25f ? -1f : anchor.x > 0.75f ? 1f : 0f, anchor.y > 0.75f ? 1f : anchor.y < 0.25f ? -1f : 0f);
        if (fromDir == Vector2.zero) fromDir = Vector2.left;
        Color baseCol = buttonImage != null ? buttonImage.color : Color.white;
        for (int i = 0; i < cells.Length; i++)
        {
            int col = i % Cols;
            delay[i] = Random.value * 0.3f + (fromDir.x != 0f ? (fromDir.x < 0f ? col : Cols - 1 - col) / (float)Cols * 0.1f : 0f);
            jitterX[i] = Random.Range(-60f, 60f); jitterY[i] = Random.Range(-50f, 50f);
            Color c = Color.Lerp(baseCol, Color.white, Random.Range(0.1f, 0.65f));
            c.a = 1f;
            cellImages[i].color = c;
        }
    }

    private void LateUpdate()
    {
        if (dock == null) return;
        if (!PixelMenuFx.HudEnabled)
        {
            if (container != null && container.gameObject.activeSelf) container.gameObject.SetActive(false);
            ResetAlpha();
            return;
        }
        bool visible = dock.SlideAmount >= 0.18f;
        if (visible && !wasVisible && Time.unscaledTime - lastStart > 0.8f) StartAssemble();
        wasVisible = visible;
        if (container == null || t > Total) { if (t > Total) { ResetAlpha(); if (container != null && container.gameObject.activeSelf) container.gameObject.SetActive(false); } return; }

        t += Time.unscaledDeltaTime;
        float u = Mathf.Clamp01(t / Total);
        // The container follows the button (which slides about).
        container.anchoredPosition = rect.anchoredPosition;
        container.sizeDelta = rect.sizeDelta;
        container.localScale = rect.localScale;
        float w = rect.rect.width, h = rect.rect.height;
        float cw = w / Cols, ch = h / Rows;
        for (int i = 0; i < cells.Length; i++)
        {
            int col = i % Cols, row = i / Cols;
            float local = Mathf.Clamp01((u - delay[i]) / 0.55f);
            float ease = 1f - (1f - local) * (1f - local) * (1f - local);
            Vector2 target = new Vector2((col + 0.5f) * cw - rect.pivot.x * w, (row + 0.5f) * ch - rect.pivot.y * h);
            Vector2 start = target + fromDir * (90f + Mathf.Abs(jitterX[i])) + new Vector2(jitterX[i] * 0.4f, jitterY[i]);
            cells[i].anchoredPosition = Vector2.Lerp(start, target, ease);
            float size = Mathf.Lerp(0.3f, 1f, ease);
            cells[i].sizeDelta = new Vector2(cw + 1f, ch + 1f) * size;
            Color c = cellImages[i].color;
            c.a = (local <= 0f ? 0f : Mathf.Clamp01(local * 3f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.62f) / 0.38f)));
            cellImages[i].color = c;
        }
        group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.3f) / 0.5f));
        alphaTouched = true;
        if (u >= 1f) { group.alpha = 1f; alphaTouched = false; container.gameObject.SetActive(false); }
    }
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
        Color c = Color.HSVToRGB(Mathf.Repeat(t * 0.04f, 1f), 0.55f, 1f);
        c.a = 0.32f + 0.1f * Mathf.Sin(t * 1.7f);
        image.color = c;
    }
}

/// <summary>A light segment sliding along a thin line (under the pause menu's title).</summary>
public class PixelMenuTitleLine : MonoBehaviour
{
    private RectTransform seg;
    private Image line;

    private void Update()
    {
        if (seg == null)
        {
            line = GetComponent<Image>();
            GameObject s = new GameObject("Segment", typeof(RectTransform), typeof(Image));
            s.transform.SetParent(transform, false);
            s.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            s.GetComponent<Image>().raycastTarget = false;
            seg = s.GetComponent<RectTransform>();
            seg.anchorMin = new Vector2(0f, 0f); seg.anchorMax = new Vector2(0f, 1f); seg.pivot = new Vector2(0.5f, 0.5f);
            seg.sizeDelta = new Vector2(90f, 0f);
        }
        bool on = PixelMenuFx.Enabled;
        if (line.enabled != on) { line.enabled = on; seg.gameObject.SetActive(on); }
        if (!on) return;
        float w = ((RectTransform)transform).rect.width;
        float t = Mathf.Repeat(Time.unscaledTime / 2.4f, 1f);
        seg.anchoredPosition = new Vector2(Mathf.Lerp(-60f, w + 60f, t * t * (3f - 2f * t)), 0f);
    }
}
