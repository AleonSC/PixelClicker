// ---- System notes ----
// PixelNextUp.cs
//   The window is a standard Tool window (PixelUIKit.CreateWindow, no X): drag it by the header, minimize it to the header ('-'), dock it against the left / right screen side (slides out on hover); remembered in PlayerPrefs PixelClicker.NextUp.Window.*.
//   UNLOCK (2026-10-16): the window is the Standard skill tree node 'What's Next' (nextup, needs BOTH Block Hold and Power Click; requireTreeNode off = from the start).
//   Its extensions 'Look Further' (nextup2) and 'Foresight' (nextup3) each add one more block AHEAD (up to three: LATER / THEN / NEXT above NOW and BEFORE); the window
//   rebuilds taller when that count changes. The blocks ahead come from PixelClicker.PeekTierAhead (a queue of pre-rolled blocks that RollTier hands out in order).
//   The 'Next up' window (component added by PixelClicker.Awake; shown while Settings > Interface > 'Next-up window' is on, PlayerPrefs
//   PixelClicker.Setting.NextUp, default on): a slim vertical window at the left edge with THREE spinning cubes - the block coming NEXT on top, the block on the cube NOW in the middle
//   (bigger) and the block BEFORE it at the bottom (dim). When the block changes the cubes slide down one slot (the new next slides in from above, the old 'before' slides out below);
//   any other change (a Block Hold swap) pops the changed cubes in and out. Data: PixelClicker.PeekTierAhead(k) (blocks put back by Block Hold that waits in line, else the pre-rolled
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
    [Tooltip("Show the window only once the Standard skill tree node 'What's Next' is unlocked (it needs Block Hold and Power Click). Off = from the start of the game, with every block ahead it can show.")]
    [SerializeField] private bool requireTreeNode = true;
    [Min(0f)]
    [Tooltip("Distance between two cubes (pixels). 0 = the default (112).")]
    [SerializeField] private float slotSpacing = 0f;
    [Min(0f)]
    [Tooltip("Size of a cube (pixels). 0 = the default (86).")]
    [SerializeField] private float iconSize = 0f;
    [Min(0f)]
    [Tooltip("How fast the cubes slide (bigger = snappier). 0 = the default (13).")]
    [SerializeField] private float slideSpeed = 0f;
    private Color windowColor => PixelUITheme.ToolWindowColor;   // (the theme: PixelUITheme)
    [Tooltip("Colour of the small captions (NEXT / NOW / BEFORE).")]
    [SerializeField] private Color captionColor = new Color(0.8f, 0.85f, 0.95f, 0.85f);
    [Tooltip("Sorting order of the window's canvas.")]
    [SerializeField] private int sortingOrder = 125;

    private const string Pref = "PixelClicker.Setting.NextUp";
    private static int enabledCache = -1;
    public static bool Enabled
    {
        get { if (enabledCache < 0) enabledCache = PixelPrefs.GetInt(Pref, 1); return enabledCache != 0; }
        set { enabledCache = value ? 1 : 0; PixelPrefs.SetInt(Pref, enabledCache); PixelPrefs.Save(); }
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
    private const float HeaderH = PixelUIKit.ToolHeaderHeight, Width = 260f;   // (the standard Tool header; wide enough for its centred title between the - and the edge)
    private readonly List<Chip> chips = new List<Chip>();
    private const int MaxAhead = 3;
    private Chip[] slot = new Chip[3];   // top to bottom: the blocks ahead (farthest first), now, before
    private int ahead = 1;               // how many blocks ahead the window shows (1 + the tree extensions)
    private bool built, synced;

    /// <summary>Blocks ahead to show: 1, +1 for 'Look Further', +1 for 'Foresight' (all three without the tree rule).</summary>
    private int AheadWanted()
    {
        if (!requireTreeNode) return MaxAhead;
        int n = 1;
        if (PixelSkills.HasStandardNode("nextup2")) n++;
        if (PixelSkills.HasStandardNode("nextup3")) n++;
        return Mathf.Min(n, MaxAhead);
    }

    private void Awake() { clicker = GetComponent<PixelClicker>(); if (clicker == null) clicker = PixelFind.First<PixelClicker>(); }

    private void Update()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        bool show = clicker != null && Enabled && (!requireTreeNode || PixelSkills.HasStandardNode("nextup"))
                    && !PixelTitleScreen.Showing && !PixelTitleScreen.Reversing && clicker.Tiers != null && clicker.Tiers.Length > 0;
        if (!show)
        {
            if (canvasRoot != null && canvasRoot.activeSelf) canvasRoot.SetActive(false);
            synced = false;   // (when it shows again the cubes are placed at once)
            return;
        }
        int wantAhead = AheadWanted();
        if (built && wantAhead != ahead) Rebuild();   // a tree extension was bought: the window grows a slot
        ahead = wantAhead;
        if (!built) Build();
        if (!built) return;
        if (!canvasRoot.activeSelf) canvasRoot.SetActive(true);

        int n = SlotCount;
        if (wantScratch == null || wantScratch.Length != n) wantScratch = new int[n];   // (reused every frame; Sync reads its length)
        int[] want = wantScratch;
        for (int k = 0; k < ahead; k++) want[ahead - 1 - k] = clicker.PeekTierAhead(k);   // farthest ahead at the top
        want[ahead] = clicker.CurrentClickTierIndex;
        want[ahead + 1] = clicker.PreviousTierIndex;
        Sync(want);
        Animate(Time.unscaledDeltaTime);
    }

    private int[] wantScratch;

    private int SlotCount => ahead + 2;
    private float SlotY(int i) => ((SlotCount - 1) * 0.5f - i) * Spacing;

    private int TierOf(int i) => slot[i] != null && !slot[i].dying ? slot[i].tier : -1;

    private void Sync(int[] want)
    {
        int n = want.Length;
        if (!synced)
        {
            for (int i = 0; i < n; i++) { Retire(i, true); slot[i] = want[i] >= 0 ? NewChip(want[i], i, false) : null; }
            synced = true;
            return;
        }
        bool same = true;
        for (int i = 0; i < n; i++) if (want[i] != TierOf(i)) { same = false; break; }
        if (same) return;

        // The ordinary step: every block moved one place on (next -> now -> before) -> everything slides down a slot and a new one comes in at the top.
        bool step = TierOf(0) >= 0;
        for (int i = 1; i < n && step; i++) if (want[i] != TierOf(i - 1)) step = false;
        if (step)
        {
            if (slot[n - 1] != null) Kill(slot[n - 1], -1);
            for (int i = n - 1; i > 0; i--) slot[i] = slot[i - 1];
            slot[0] = want[0] >= 0 ? NewChip(want[0], 0, true) : null;
            Retarget();
            return;
        }

        // Anything else (a block put back by Block Hold, a potion...): the changed cubes pop out and in.
        for (int i = 0; i < n; i++)
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
        if (direction < 0) { c.targetY = SlotY(SlotCount - 1) - Spacing * 0.9f; c.targetScale = 0.6f; }
        else c.targetScale = 0f;
    }

    private void SetTargets(Chip c, int i)
    {
        c.targetY = SlotY(i);
        if (i == ahead) { c.targetScale = 1.05f; c.targetAlpha = 1f; }                 // now
        else if (i > ahead) { c.targetScale = 0.78f; c.targetAlpha = 0.5f; }           // before
        else { int k = ahead - 1 - i; c.targetScale = 0.78f - 0.07f * k; c.targetAlpha = 0.92f - 0.15f * k; }   // ahead: farther = smaller, dimmer
    }

    private void Retarget() { for (int i = 0; i < SlotCount; i++) if (slot[i] != null) SetTargets(slot[i], i); }

    /// <summary>Throws the window away so Build makes it again with the new number of slots (its place is remembered by PixelWindowChrome).</summary>
    private void Rebuild()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null; panel = null; chrome = null;
        chips.Clear();
        built = false; synced = false;
    }

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
        c.y = fromAbove ? SlotY(0) + Spacing * 0.9f : c.targetY;
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
        slot = new Chip[SlotCount];
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("Next Up", sortingOrder, new Vector2(1920f, 1080f), true);
        // A standard Tool window (PixelUIKit.CreateWindow): drag by the header, minimize with '-', dock against a screen side; no X (it is switched off in Settings).
        // The prefKey is the one it always had, so its saved place carries over.
        PixelWindowParts parts = PixelUIKit.CreateWindow(font, canvasRoot.transform, "Window", "Next up", new Vector2(Width, HeaderH + Spacing * SlotCount + 8f), PixelUIKit.WindowKind.Tool, null, "PixelClicker.NextUp.Window", closeButton: false,
                                                         pop: false);   // (it just shows with its canvas, e.g. after the title screen - no grow-in / sound each time)
        panel = parts.Root;
        RectTransform pr = parts.Rect;
        chrome = parts.Chrome;

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

        string[] aheadNames = { "NEXT", "THEN", "LATER" };
        for (int i = 0; i < SlotCount; i++)
        {
            string name = i == ahead ? "NOW" : i > ahead ? "BEFORE" : aheadNames[Mathf.Min(ahead - 1 - i, aheadNames.Length - 1)];
            TMP_Text t = PixelUIKit.CreateText(font, body, "Caption " + name, name, i == ahead ? 24f : 18f, TextAlignmentOptions.Center, FontStyles.Bold, captionColor);
            t.raycastTarget = false;
            RectTransform r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(100f, 30f);
            r.anchoredPosition = new Vector2(52f, SlotY(i));
        }

        chrome.defaultPlacer = cs => new Vector2(position.x, -(cs.y - pr.sizeDelta.y) * 0.5f - position.y);
        built = true;
    }
}
