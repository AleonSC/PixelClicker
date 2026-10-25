// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsPopups.cs
//   Part of partial class PixelSkills: the '+n <Skill> XP' popups (AddXp calls QueueXpPopup). Three places, so they never pile on top of each other
//   (2026-10-19, the user's layout):
//   * THE XP DOCK - every skill XP popup (clicks, the auto clicker, the Recycler, rewards...): XP is gathered xpPopupGatherSeconds (0 = 0.6 s) per
//     skill and shown as whole numbers (fractions carry over) in a small stack at the BOTTOM LEFT of the screen (since 2026-10-25; xpDockSideMargin from the edge, popups lined
//     up on their LEFT edges), just above the black bar (xpDockGap); WHILE THE SKILL TRACKER SHOWS FULL (the early '???' one or the pinned one, not the
//     small version) the dock sits just above the tracker instead and follows it (TrackerCarriesXpDock; no dragging the dock then); the newest at the bottom, older ones slide up and fade. HOLD the left mouse button on one (xpDockHoldSeconds, the shared ring,
//     PixelHold) and drag to move the dock; the spot is saved PER SAVE (PixelClicker.Setting.XpDock2X / Y, PixelPrefs.DeclarePerSave); right-click
//     a dock popup to put the dock back in its default spot. The dock popups catch the mouse (so a click on one never reaches the cube).
//   * HUSK CRUSH SCORES: a crush's '+0.25 Standard XP' (AddHuskXp crushed, 'at' = where the husk broke) shows at once, decimals and all, AT THAT
//     SPOT in the world (projected every frame, so it stays put when the camera moves), rising a little as it fades.
//   * CRUSH BONUSES ('Air crush bonus!', 'Last-gasp bonus!', 'Crush streak x1.4!': ShowMouseText) beside the mouse pointer, each in the lowest
//     free row of a small stack, so two never overlap.
//   LOOK: a dark rounded plate behind the text (xpPopupPlate, xpPopupPlateColor), the skill colour lightened towards white (xpPopupLighten),
//   xpPopupSize (0 = 24), xpPopupOpacity (0.8: the game shows through a little), a quick pop as each appears, stays xpPopupSeconds (2; fades over the second half). Own canvas 106. hideXpPopups.
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Part of <see cref="PixelSkills"/>: the '+n Skill XP' popups - the XP dock above the black bar, husk crush scores where the husk broke, crush bonuses by the mouse.</summary>
public partial class PixelSkills
{
    [Header("XP Popups")]
    [Tooltip("Turn off the '+n XP' popups.")]
    [SerializeField] private bool hideXpPopups = false;

    [Min(0f)]
    [Tooltip("Seconds XP is gathered before it is shown as one popup in the XP dock. 0 = 0.6.")]
    [SerializeField] private float xpPopupGatherSeconds = 0f;

    [Min(0f)]
    [Tooltip("Text size of the popups. 0 = 24.")]
    [SerializeField] private float xpPopupSize = 0f;

    [Tooltip("A dark rounded plate behind each XP popup so it reads over any floor or sky.")]
    [SerializeField] private bool xpPopupPlate = true;
    [Tooltip("Colour of the plate behind the XP popups (alpha = how dark).")]
    [SerializeField] private Color xpPopupPlateColor = new Color(0.03f, 0.04f, 0.07f, 0.78f);
    [Range(0f, 0.8f)]
    [Tooltip("How far the skill's colour is lightened towards white for the popup text (brighter = easier to read).")]
    [SerializeField] private float xpPopupLighten = 0.3f;
    [Range(0.2f, 1f)]
    [Tooltip("How opaque the popups are at their strongest (1 = solid; lower lets the game show through).")]
    [SerializeField] private float xpPopupOpacity = 0.8f;
    [Min(0.3f)]
    [Tooltip("Seconds an XP popup stays (it fades over the second half).")]
    [SerializeField] private float xpPopupSeconds = 2f;

    [Header("XP Dock (skill XP, bottom left of the screen)")]
    [Min(0f)]
    [Tooltip("Gap between the bottom black bar and the XP dock's newest popup (canvas units).")]
    [SerializeField] private float xpDockGap = 14f;
    [Min(0f)]
    [Tooltip("Gap between the left edge of the screen and the XP dock (its popups line up on their left edges).")]
    [SerializeField] private float xpDockSideMargin = 24f;
    [Min(0.05f)]
    [Tooltip("Seconds the left button is held on a dock popup (a ring fills) before the dock can be dragged.")]
    [SerializeField] private float xpDockHoldSeconds = 0.4f;
    [Range(1, 8)]
    [Tooltip("The most popups stacked in the dock at once (the oldest goes first).")]
    [SerializeField] private int xpDockMax = 4;
    [Tooltip("Colour of the ring that fills while holding a dock popup to move it.")]
    [SerializeField] private Color xpDockRingColor = new Color(0.6f, 0.85f, 1f, 1f);

    [Header("Husk crush popups")]
    [Tooltip("How far a crush score rises from where the husk broke while it fades (canvas units).")]
    [SerializeField] private float crushPopupRise = 40f;
    [Tooltip("Where the crush bonus texts sit from the mouse pointer (canvas units; right / up).")]
    [SerializeField] private Vector2 bonusTextOffset = new Vector2(34f, -40f);

    private enum PopupKind { Dock, World, Mouse }

    private sealed class XpPopup
    {
        public PopupKind kind;
        public GameObject root;
        public RectTransform rect;
        public CanvasGroup group;
        public float age, height, dockY;
        public Vector3 world;
        public int row;
    }

    private const string PrefDockX = "PixelClicker.Setting.XpDock3X", PrefDockY = "PixelClicker.Setting.XpDock3Y";   // (3: the bottom-left dock; its saved spot is the LEFT edge)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void DeclareXpDockPerSave()
    {
        PixelPrefs.DeclarePerSave(PrefDockX, PixelPrefs.Kind.Float);
        PixelPrefs.DeclarePerSave(PrefDockY, PixelPrefs.Kind.Float);
    }

    private GameObject xpCanvas;
    private RectTransform xpCanvasRect;
    private readonly List<XpPopup> xpPopups = new List<XpPopup>();
    private double xpPending;
    private SkillPack xpPendingPack;
    private float xpGatherTimer;
    private bool xpDockDragging;
    private readonly object xpDockTarget = new object();

    private bool xpPopupDecimals;     // set by AddHuskXp for a crush: its popup shows at once, decimals and all
    private Vector3? xpPopupAt;       // set by AddHuskXp for a crush: where the husk broke (the popup stays there)

    private float PopupSize => xpPopupSize > 1f ? xpPopupSize : 24f;

    private void QueueXpPopup(SkillPack pack, double xp)
    {
        if (hideXpPopups || pack == null || xp <= 0d || PixelTitleScreen.Showing) return;
        if (xpPopupDecimals)
        {
            // A husk crush: its own popup right away, where the husk broke (small amounts with decimals, so every crush is seen).
            double shown = System.Math.Round(xp, 2);
            if (shown >= 0.005d) ShowPopup(pack, XpText(pack, shown), xpPopupAt.HasValue ? PopupKind.World : PopupKind.Mouse, xpPopupAt ?? Vector3.zero);
            return;
        }
        if (xpPendingPack != null && xpPendingPack != pack) FlushXpPopup(true);   // the skill changed: show what was gathered for the old one
        xpPendingPack = pack;
        xpPending += xp;
    }

    private void UpdateXpPopups()
    {
        float gather = xpPopupGatherSeconds > 0.05f ? xpPopupGatherSeconds : 0.6f;
        if (xpPendingPack != null)
        {
            xpGatherTimer += Time.unscaledDeltaTime;
            if (xpGatherTimer >= gather) FlushXpPopup(false);
        }
        TickXpPopups();
    }

    private void FlushXpPopup(bool force)
    {
        xpGatherTimer = 0f;
        int whole = (int)System.Math.Floor(xpPending);
        if (whole < 1 || xpPendingPack == null || PixelTitleScreen.Showing)
        {
            if (force) { xpPending = 0d; xpPendingPack = null; }
            return;
        }
        xpPending -= whole;
        SkillPack pack = xpPendingPack;
        if (force) { xpPending = 0d; xpPendingPack = null; }
        ShowPopup(pack, XpText(pack, whole), PopupKind.Dock, Vector3.zero);
    }

    private string XpText(SkillPack pack, double amount) => "+" + amount.ToString(amount == System.Math.Floor(amount) ? "0" : "0.##") + " " + ShownName(pack) + " XP";

    /// <summary>A Husk Crush bonus text by the mouse (stacked so it never overlaps another).</summary>
    private void ShowMouseText(SkillPack pack, string text) => ShowPopup(pack, text, PopupKind.Mouse, Vector3.zero);

    private void ShowPopup(SkillPack pack, string text, PopupKind kind, Vector3 world)
    {
        if (xpCanvas == null)
        {
            xpCanvas = PixelUIKit.CreateCanvas("PixelSkills XP Popups", 106, new Vector2(1920f, 1080f), true);   // (clickable: the dock popups can be held and moved)
            xpCanvas.transform.SetParent(transform, false);
            xpCanvasRect = xpCanvas.GetComponent<RectTransform>();
        }
        float size = PopupSize;
        Color textColor = Color.Lerp(pack.accent, Color.white, xpPopupLighten);
        textColor.a = 1f;

        XpPopup p = new XpPopup { kind = kind, world = world };
        p.root = new GameObject(kind + " XP", typeof(RectTransform), typeof(CanvasGroup));
        p.root.transform.SetParent(xpCanvas.transform, false);
        p.rect = p.root.GetComponent<RectTransform>();
        p.rect.anchorMin = p.rect.anchorMax = p.rect.pivot = new Vector2(0.5f, 0.5f);
        p.group = p.root.GetComponent<CanvasGroup>();
        p.group.blocksRaycasts = kind == PopupKind.Dock;   // (only the dock catches the mouse: a crush score under the pointer must not block the next crush)
        TMP_Text t = PixelUIKit.CreateText(Font, p.root.transform, "Text", text, size, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        t.raycastTarget = false;
        Vector2 pref = t.GetPreferredValues(text, 900f, 0f);
        p.height = size * 1.45f;
        p.rect.sizeDelta = new Vector2(pref.x + size * 0.9f, p.height);
        GameObject plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
        plate.transform.SetParent(p.root.transform, false);
        plate.transform.SetAsFirstSibling();
        Image img = plate.GetComponent<Image>();
        PixelUIKit.StyleBox(img);
        img.color = xpPopupPlate ? xpPopupPlateColor : Color.clear;
        img.raycastTarget = kind == PopupKind.Dock;
        PixelUIKit.Stretch(img.rectTransform);
        PixelUIKit.Stretch(t.rectTransform);

        if (kind == PopupKind.Mouse)
        {
            // The lowest row no other bonus text is using.
            int row = 0;
            while (xpPopups.Exists(o => o.kind == PopupKind.Mouse && o.row == row)) row++;
            p.row = row;
        }
        else if (kind == PopupKind.Dock)
        {
            p.dockY = -p.height;   // (slides up into the bottom slot)
            int docked = 0;
            for (int i = xpPopups.Count - 1; i >= 0; i--)
                if (xpPopups[i].kind == PopupKind.Dock && ++docked >= Mathf.Max(1, xpDockMax)) { Destroy(xpPopups[i].root); xpPopups.RemoveAt(i); }
        }
        xpPopups.Add(p);
        PlacePopup(p, 0f);
    }

    private void TickXpPopups()
    {
        if (xpPopups.Count == 0) { xpDockDragging = false; return; }
        float life = Mathf.Max(0.3f, xpPopupSeconds);
        UpdateDockMove();

        // Dock rows: the newest at the bottom, each older one above it.
        float stackY = 0f;
        for (int i = xpPopups.Count - 1; i >= 0; i--)
        {
            XpPopup p = xpPopups[i];
            if (p.kind != PopupKind.Dock) continue;
            p.dockY = Mathf.Lerp(p.dockY, stackY, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            stackY += p.height + 6f;
        }

        for (int i = xpPopups.Count - 1; i >= 0; i--)
        {
            XpPopup p = xpPopups[i];
            if (p.root == null) { xpPopups.RemoveAt(i); continue; }
            if (!(xpDockDragging && p.kind == PopupKind.Dock)) p.age += Time.unscaledDeltaTime;   // (a dock being moved does not fade)
            float k = Mathf.Clamp01(p.age / life);
            if (k >= 1f) { Destroy(p.root); xpPopups.RemoveAt(i); continue; }
            p.group.alpha = (k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f) * xpPopupOpacity;
            float pop = p.age < 0.18f ? 1f + 0.3f * Mathf.Sin(p.age / 0.18f * Mathf.PI) : 1f;   // (a quick pop as it appears)
            p.rect.localScale = new Vector3(pop, pop, 1f);
            PlacePopup(p, k);
        }
    }

    private void PlacePopup(XpPopup p, float k)
    {
        if (xpCanvasRect == null) return;
        switch (p.kind)
        {
            case PopupKind.Dock:
                p.rect.pivot = new Vector2(0f, 0.5f);   // (the dock's popups line up on their left edges)
                p.rect.anchoredPosition = DockPosition() + new Vector2(0f, p.dockY + p.height * 0.5f);
                break;
            case PopupKind.World:
            {
                Camera cam = clicker != null ? clicker.ViewCamera : Camera.main;
                if (cam == null) break;
                Vector3 sp = cam.WorldToScreenPoint(p.world);
                if (sp.z <= 0f) { p.group.alpha = 0f; break; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(xpCanvasRect, sp, null, out Vector2 local);
                p.rect.anchoredPosition = local + new Vector2(0f, 30f + crushPopupRise * k);
                break;
            }
            case PopupKind.Mouse:
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(xpCanvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
                p.rect.pivot = new Vector2(0f, 0.5f);   // (its left edge by the pointer)
                p.rect.anchoredPosition = local + bonusTextOffset - new Vector2(0f, p.row * (p.height + 6f));
                break;
            }
        }
    }

    // ---- The dock's spot (on the skill tracker while it shows full; else bottom left above the black bar, or where it was dragged, saved per save) ----

    /// <summary>The dock's bottom LEFT corner (canvas units from the middle of the screen): on top of the skill tracker while it shows full, else the
    /// saved spot, else the bottom left above the bar.</summary>
    private Vector2 DockPosition()
    {
        if (TrackerCarriesXpDock(out Vector2 trackerTop)) return trackerTop + new Vector2(0f, xpDockGap);   // (riding on the skill tracker, from its top left corner)
        Rect r = xpCanvasRect.rect;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 fallback = new Vector2(-r.width * 0.5f + xpDockSideMargin, -r.height * 0.5f + bar + xpDockGap);
        if (!PixelPrefs.HasKey(PrefDockX) || !PixelPrefs.HasKey(PrefDockY)) return fallback;
        Vector2 p = new Vector2(PixelPrefs.GetFloat(PrefDockX, 0f), PixelPrefs.GetFloat(PrefDockY, fallback.y));
        float hx = r.width * 0.5f - 12f, lo = -r.height * 0.5f + bar + 4f, hi = r.height * 0.5f - bar - 60f;
        return new Vector2(Mathf.Clamp(p.x, -hx, hx), Mathf.Clamp(p.y, lo, Mathf.Max(lo, hi)));
    }

    private bool PointerOnDock()
    {
        Vector2 pointer = PixelInput.PointerPosition();
        foreach (XpPopup p in xpPopups)
            if (p.kind == PopupKind.Dock && p.root != null && RectTransformUtility.RectangleContainsScreenPoint(p.rect, pointer, null)) return true;
        return false;
    }

    private void UpdateDockMove()
    {
        if (TrackerCarriesXpDock(out _)) { xpDockDragging = false; return; }   // (on the tracker: it moves with the tracker, not by itself)
        if (xpDockDragging)
        {
            if (!PixelInput.LeftHeld()) { xpDockDragging = false; return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(xpCanvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
            float halfWidth = 0f;
            for (int i = xpPopups.Count - 1; i >= 0; i--) if (xpPopups[i].kind == PopupKind.Dock) { halfWidth = xpPopups[i].rect.sizeDelta.x * 0.5f; break; }
            PixelPrefs.SetFloat(PrefDockX, local.x - halfWidth);   // (the pointer holds the newest popup by its middle; the saved spot is its left edge)
            PixelPrefs.SetFloat(PrefDockY, local.y - PopupSize * 0.7f);   // (the pointer holds the newest popup)
            return;
        }
        bool over = PointerOnDock();
        if (over && PixelInput.RightPressed())
        {
            PixelPrefs.DeleteKey(PrefDockX);   // right-click: back to the default spot
            PixelPrefs.DeleteKey(PrefDockY);
            PixelAudio.Play("ui_click");
            return;
        }
        if (PixelHold.Update(this, over ? xpDockTarget : null, xpDockHoldSeconds, "", xpDockRingColor, true, null, true))
        {
            xpDockDragging = true;
            PixelAudio.Play("ui_click");
        }
    }
}
