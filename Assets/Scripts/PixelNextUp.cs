// ---- System notes ----
// PixelNextUp.cs
//   The 'Next up' window (component added by PixelClicker.Awake; shown while the Block Hold upgrade is owned and Settings > Interface > 'Next-up window' is on, PlayerPrefs
//   PixelClicker.Setting.NextUp, default on): a slim vertical window at the left edge with THREE spinning cubes - the block coming NEXT on top, the block on the cube NOW in the middle
//   (bigger) and the block BEFORE it at the bottom (dim). When the block changes the cubes slide down one slot (the new next slides in from above, the old 'before' slides out below);
//   any other change (a Block Hold swap) pops the changed cubes in and out. Data: PixelClicker.PeekNextTier() (a block put back by Block Hold that waits in line, else the pre-rolled
//   'upcoming' tier: PixelClicker.RollTier hands out that same tier, so the preview is true; it is re-rolled if a potion / skill / switch makes it invalid; nothing is pre-rolled while
//   this window is hidden, so normal play rolls exactly as before), CurrentClickTierIndex and PreviousTierIndex. The cubes are PixelCubeIcon (UI) in the tier colours.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelNextUp : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("Where the window sits: offset from the middle of the left screen edge (pixels, canvas units).")]
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
        public PixelCubeIcon icon;
        public int tier;
        public Color baseColor;
        public float y, alpha, scale;
        public float targetY, targetAlpha, targetScale;
        public bool dying;
    }

    private PixelClicker clicker;
    private GameObject canvasRoot, panel;
    private RectTransform chipHost;
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
        Color col = clicker.Tiers[tier].color;   // (glass-like types stay a little see-through)
        col.a = Mathf.Max(0.55f, col.a);
        GameObject g = new GameObject("Cube " + tier, typeof(RectTransform));
        g.transform.SetParent(chipHost, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(IconSize, IconSize);
        PixelCubeIcon icon = g.AddComponent<PixelCubeIcon>();
        icon.spinDegreesPerSecond = 45f;
        icon.fill = 0.74f;
        Chip c = new Chip { rect = r, icon = icon, tier = tier, baseColor = col };
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
        c.icon.color = col;
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
        canvasRoot = PixelUIKit.CreateCanvas("Next Up", sortingOrder, new Vector2(1920f, 1080f), false);
        panel = new GameObject("Window", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasRoot.transform, false);
        Image bg = panel.GetComponent<Image>();
        bg.raycastTarget = false;
        PixelUIKit.StyleWindow(bg, windowColor);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0f, 0.5f);
        pr.pivot = new Vector2(0f, 0.5f);
        pr.sizeDelta = new Vector2(200f, Spacing * 3f + 36f);
        pr.anchoredPosition = position;

        GameObject host = new GameObject("Cubes", typeof(RectTransform));
        host.transform.SetParent(panel.transform, false);
        chipHost = host.GetComponent<RectTransform>();
        chipHost.anchorMin = chipHost.anchorMax = chipHost.pivot = new Vector2(0.5f, 0.5f);
        chipHost.sizeDelta = Vector2.zero;
        chipHost.anchoredPosition = Vector2.zero;
        if (panel.GetComponent<RectMask2D>() == null) panel.AddComponent<RectMask2D>();   // cubes slide in and out inside the window

        string[] names = { "NEXT", "NOW", "BEFORE" };
        for (int i = 0; i < 3; i++)
        {
            TMP_Text t = PixelUIKit.CreateText(font, panel.transform, "Caption " + names[i], names[i], i == 1 ? 24f : 18f, TextAlignmentOptions.Center, FontStyles.Bold, captionColor);
            t.raycastTarget = false;
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(100f, 30f);
            r.anchoredPosition = new Vector2(52f, i == 0 ? Spacing : i == 1 ? 0f : -Spacing);
        }
        built = true;
    }
}
