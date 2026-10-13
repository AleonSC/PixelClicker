// ---- System notes ----
// PixelNextUp.cs
//   The window has a header (title + '-' button) and is a PixelWindowChrome window: drag it by the header, minimize it to the header, dock it against the left / right screen side (slides out on hover); remembered in PlayerPrefs PixelClicker.NextUp.Window.*.
//   The 'Next up' window (component added by PixelClicker.Awake; shown while the Block Hold upgrade is owned and Settings > Interface > 'Next-up window' is on, PlayerPrefs
//   PixelClicker.Setting.NextUp, default on): a slim vertical window at the left edge with THREE spinning cubes - the block coming NEXT on top, the block on the cube NOW in the middle
//   (bigger) and the block BEFORE it at the bottom (dim). When the block changes the cubes slide down one slot (the new next slides in from above, the old 'before' slides out below);
//   any other change (a Block Hold swap) pops the changed cubes in and out. Data: PixelClicker.PeekNextTier() (a block put back by Block Hold that waits in line, else the pre-rolled
//   'upcoming' tier: PixelClicker.RollTier hands out that same tier, so the preview is true; it is re-rolled if a potion / skill / switch makes it invalid; nothing is pre-rolled while
//   this window is hidden, so normal play rolls exactly as before), CurrentClickTierIndex and PreviousTierIndex. The cubes are the blocks' REAL models (PixelTierPicture: a hidden studio camera per block rendering into a RenderTexture, shown by a RawImage), not flat coloured cubes.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelNextUp : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("Where the window sits the first time: offset from the middle of the left screen edge (pixels, canvas units). After that the player drags it (it remembers), minimizes it with '-' or docks it by dragging it against a screen side.")]
    [SerializeField] private Vector2 position = new Vector2(24f, 0f);
    [Min(0f)]
    [Tooltip("Distance between two cubes (pixels). 0 = the default (112).")]
    [SerializeField] private float slotSpacing = 0f;
    [Min(0f)]
    [Tooltip("Size of a cube (pixels). 0 = the default (86).")]
    [SerializeField] private float iconSize = 0f;
    [Min(0f)]
    [Tooltip("How fast the cubes slide (bigger = snappier). 0 = the default (13).")]
    [SerializeField] private float slideSpeed = 0f;
    [Tooltip("Colour of the window.")]
    [SerializeField] private Color windowColor = new Color(0.1f, 0.12f, 0.18f, 0.92f);
    [Tooltip("Colour of the small captions (NEXT / NOW / BEFORE).")]
    [SerializeField] private Color captionColor = new Color(0.8f, 0.85f, 0.95f, 0.85f);
    [Tooltip("Sorting order of the window's canvas.")]
    [SerializeField] private int sortingOrder = 125;

    private const string Pref = "PixelClicker.Setting.NextUp";
    private static int enabledCache = -1;
    public static bool Enabled
    {
        get { if (enabledCache < 0) enabledCache = PlayerPrefs.GetInt(Pref, 1); return enabledCache != 0; }
        set { enabledCache = value ? 1 : 0; PlayerPrefs.SetInt(Pref, enabledCache); PlayerPrefs.Save(); }
    }
    public static void ReloadFromPrefs() { enabledCache = -1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { enabledCache = -1; }

    private float Spacing => slotSpacing > 0f ? slotSpacing : 112f;
    private float IconSize => iconSize > 0f ? iconSize : 86f;
    private float Speed => slideSpeed > 0f ? slideSpeed : 13f;

    private class Chip
    {
        public RectTransform rect;
        public RawImage pic;   // the block's real model (PixelTierPicture)
        public int tier;
        public Color baseColor;
        public float y, alpha, scale;
        public float targetY, targetAlpha, targetScale;
        public bool dying;
    }

    private PixelClicker clicker;
    private GameObject canvasRoot, panel;
    private RectTransform chipHost, body;
    private PixelWindowChrome chrome;
    private const float HeaderH = 36f;
    private readonly List<Chip> chips = new List<Chip>();
    private readonly Chip[] slot = new Chip[3];   // 0 = next (top), 1 = now, 2 = before (bottom)
    private bool built, synced;

    private void Awake() { clicker = GetComponent<PixelClicker>(); }

    private void Update()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        bool show = clicker != null && Enabled && PixelBlockHold.Instance != null && PixelBlockHold.Instance.Active
                    && !PixelTitleScreen.Showing && !PixelTitleScreen.Reversing && clicker.Tiers != null && clicker.Tiers.Length > 0;
        if (!show)
        {
            if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false);
            synced = false;   // (when it shows again the cubes are placed at once)
            return;
        }
        if (!built) Build();
        if (!built) return;
        if (!canvasRoot.activeSelf) canvasRoot.SetActive(true);

        int next = clicker.PeekNextTier(), now = clicker.CurrentClickTierIndex, before = clicker.PreviousTierIndex;
        Sync(next, now, before);
        Animate(Time.unscaledDeltaTime);
    }

    private int TierOf(int i) => slot[i] != null && !slot[i].dying ? slot[i].tier : -1;

    private void Sync(int next, int now, int before)
    {
        int[] want = { next, now, before };
        if (!synced)
        {
            for (int i = 0; i < 3; i++) { Retire(i, true); slot[i] = want[i] >= 0 ? NewChip(want[i], i, false) : null; }
            synced = true;
            return;
        }
        if (want[0] == TierOf(0) && want[1] == TierOf(1) && want[2] == TierOf(2)) return;

        // The ordinary step: the next block became the current one and the current one became 'before' -> everything slides down a slot.
        if (TierOf(0) >= 0 && want[1] == TierOf(0) && want[2] == TierOf(1))
        {
            if (slot[2] != null) Kill(slot[2], -1);
            slot[2] = slot[1];
            slot[1] = slot[0];
            slot[0] = want[0] >= 0 ? NewChip(want[0], 0, true) : null;
            Retarget();
            return;
        }

        // Anything else (a block put back by Block Hold, a potion...): the changed cubes pop out and in.
        for (int i = 0; i < 3; i++)
        {
            if (want[i] == TierOf(i)) continue;
            if (slot[i] != null) Kill(slot[i], 0);
            slot[i] = want[i] >= 0 ? NewChip(want[i], i, false, true) : null;
        }
    }

    private void Retire(int i, bool instant)
    {
        if (slot[i] == null) return;
        if (instant) { Destroy(slot[i].rect.gameObject); chips.Remove(slot[i]); }
        slot[i] = null;
    }

    /// <summary>Makes a cube leave: direction -1 = slide down and fade, 0 = shrink away.</summary>
    private void Kill(Chip c, int direction)
    {
        c.dying = true;
        c.targetAlpha = 0f;
        if (direction < 0) { c.targetY = -Spacing * 1.9f; c.targetScale = 0.6f; }
        else c.targetScale = 0f;
    }

    private void SetTargets(Chip c, int i)
    {
        c.targetY = i == 0 ? Spacing : i == 1 ? 0f : -Spacing;
        c.targetScale = i == 1 ? 1.05f : 0.78f;
        c.targetAlpha = i == 1 ? 1f : i == 0 ? 0.92f : 0.5f;
    }

    private void Retarget() { for (int i = 0; i < 3; i++) if (slot[i] != null) SetTargets(slot[i], i); }

    private Chip NewChip(int tier, int i, bool fromAbove, bool grow = false)
    {
        GameObject g = new GameObject("Cube " + tier, typeof(RectTransform));
        g.transform.SetParent(chipHost, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(IconSize, IconSize);
        RawImage pic = g.AddComponent<RawImage>();
        pic.raycastTarget = false;
        pic.texture = PixelTierPicture.Get(clicker, tier);
        Chip c = new Chip { rect = r, pic = pic, tier = tier, baseColor = Color.white };
        SetTargets(c, i);
        c.y = fromAbove ? Spacing * 1.9f : c.targetY;
        c.alpha = fromAbove || grow ? 0f : c.targetAlpha;
        c.scale = grow ? 0f : c.targetScale;
        chips.Add(c);
        Apply(c);
        return c;
    }

    private static void Apply(Chip c)
    {
        c.rect.anchoredPosition = new Vector2(-28f, c.y);
        c.rect.localScale = Vector3.one * Mathf.Max(0.001f, c.scale);
        Color col = c.baseColor; col.a = Mathf.Clamp01(c.baseColor.a * c.alpha);
        c.pic.color = col;
    }

    private void LateUpdate()
    {
        if (!built || canvasRoot == null || !canvasRoot.activeInHierarchy) return;
        if (chrome != null && chrome.Minimized) return;   // (nothing to draw while it is folded up)
        float yaw = Time.unscaledTime * 45f;
        for (int i = 0; i < chips.Count; i++) if (chips[i] != null && chips[i].pic != null && chips[i].pic.texture != null) PixelTierPicture.Render(chips[i].tier, yaw);
    }

    private void Animate(float dt)
    {
        float k = 1f - Mathf.Exp(-dt * Speed);
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            Chip c = chips[i];
            c.y = Mathf.Lerp(c.y, c.targetY, k);
            c.alpha = Mathf.Lerp(c.alpha, c.targetAlpha, k);
            c.scale = Mathf.Lerp(c.scale, c.targetScale, k);
            Apply(c);
            if (c.dying && c.alpha < 0.02f) { Destroy(c.rect.gameObject); chips.RemoveAt(i); }
        }
    }

    private void Build()
    {
        if (clicker.UIFont == null) return;
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("Next Up", sortingOrder, new Vector2(1920f, 1080f), true);
        panel = new GameObject("Window", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasRoot.transform, false);
        Image bg = panel.GetComponent<Image>();
        PixelUIKit.StyleWindow(bg, windowColor);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0f, 1f);   // top left (what PixelWindowChrome wants)
        pr.sizeDelta = new Vector2(200f, HeaderH + Spacing * 3f + 8f);

        TMP_Text title = PixelUIKit.CreateText(font, panel.transform, "Title", "NEXT UP", 22f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, captionColor);
        title.raycastTarget = false;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-50f, HeaderH);
        tr.anchoredPosition = new Vector2(-18f, 0f);

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(RectMask2D));   // cubes slide in and out inside the body
        bodyGo.transform.SetParent(panel.transform, false);
        body = bodyGo.GetComponent<RectTransform>();
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = Vector2.zero; body.offsetMax = new Vector2(0f, -HeaderH);

        GameObject host = new GameObject("Cubes", typeof(RectTransform));
        host.transform.SetParent(body, false);
        chipHost = host.GetComponent<RectTransform>();
        chipHost.anchorMin = chipHost.anchorMax = chipHost.pivot = new Vector2(0.5f, 0.5f);
        chipHost.sizeDelta = Vector2.zero;
        chipHost.anchoredPosition = Vector2.zero;

        string[] names = { "NEXT", "NOW", "BEFORE" };
        for (int i = 0; i < 3; i++)
        {
            TMP_Text t = PixelUIKit.CreateText(font, body, "Caption " + names[i], names[i], i == 1 ? 24f : 18f, TextAlignmentOptions.Center, FontStyles.Bold, captionColor);
            t.raycastTarget = false;
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(100f, 30f);
            r.anchoredPosition = new Vector2(52f, i == 0 ? Spacing : i == 1 ? 0f : -Spacing);
        }

        // Drag by the header, minimize with '-', dock against a screen side (PixelWindowChrome; remembered between sessions).
        chrome = PixelWindowChrome.Attach(pr, font, "PixelClicker.NextUp.Window", HeaderH, 6f, 5f, 26f, new Color(0.3f, 0.36f, 0.5f, 1f), new Color(0.92f, 0.94f, 1f, 1f), 20f);
        chrome.HidePin();
        chrome.Keep(title.gameObject);
        chrome.defaultPlacer = cs => new Vector2(position.x, -(cs.y - pr.sizeDelta.y) * 0.5f - position.y);
        built = true;
    }
}
