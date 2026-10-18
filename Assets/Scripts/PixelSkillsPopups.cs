// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsPopups.cs
//   Part of partial class PixelSkills: '+n <Skill> XP' popups while a skill trains (AddXp calls QueueXpPopup; XP is gathered
//   xpPopupGatherSeconds (0 = 0.6 s) and shown as whole numbers, fractions of auto clicks carry over; UpdateXpPopups runs from the
//   window's Update; XP from the auto clicker is gathered separately and appears beside the cube instead (xpFromAuto set by
//   OnCollected, FlushAutoXpPopup, CubeScreenPoint, rises beside the cube and follows it); text in the pack's accent colour, hangs
//   just under the mouse cursor and follows it (drifting a little further down as it fades), on its own canvas 106 so it never mixes
//   with the other +n texts; hideXpPopups, xpPopupSize 0 = 40). LOOK (2026-10-18, easier to see): a dark rounded plate behind the text (xpPopupPlate,
//   xpPopupPlateColor), the skill colour lightened towards white (xpPopupLighten), a quick pop as it appears, stays xpPopupSeconds (2). A Husk Crush (AddHuskXp crushed) marks its XP 'show decimals' (xpPopupDecimals ->
//   AddHuskXp(popupText) shows a short text INSTEAD of the '+n XP' line (Husk Crush's bonuses, ShowPopupText). xpPendingDecimals): that popup shows the gathered amount even under 1, rounded to 2 decimals ('+0.25 Standard XP'), so every crush is seen.
// ---------------------------------------------------
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Part of <see cref="PixelSkills"/>: a rising '+n Skill XP' popup in the skill's own colour whenever the skill in training gains XP. XP that comes
/// in small pieces (auto clicks give a fraction) is gathered for a moment and shown as whole numbers, so there is one clean popup instead of a flood.
/// XP from your own clicks hangs just under the mouse cursor and follows it; XP from the auto clicker appears beside the cube instead (so it isn't stuck to a
/// mouse that is elsewhere). Uses its own canvas so it never mixes with the other +n texts.
/// </summary>
public partial class PixelSkills
{
    [Header("XP Popups")]
    [Tooltip("Turn off the '+n XP' popups that rise while a skill trains.")]
    [SerializeField] private bool hideXpPopups = false;

    [Min(0f)]
    [Tooltip("Seconds XP is gathered before it is shown as one popup. 0 = 0.6.")]
    [SerializeField] private float xpPopupGatherSeconds = 0f;

    [Min(0f)]
    [Tooltip("Text size of the popup. 0 = 40.")]
    [SerializeField] private float xpPopupSize = 0f;

    [Tooltip("A dark rounded plate behind each XP popup so it reads over any floor or sky.")]
    [SerializeField] private bool xpPopupPlate = true;
    [Tooltip("Colour of the plate behind the XP popups (alpha = how dark).")]
    [SerializeField] private Color xpPopupPlateColor = new Color(0.03f, 0.04f, 0.07f, 0.78f);
    [Range(0f, 0.8f)]
    [Tooltip("How far the skill's colour is lightened towards white for the popup text (brighter = easier to read).")]
    [SerializeField] private float xpPopupLighten = 0.3f;
    [Min(0.3f)]
    [Tooltip("Seconds an XP popup stays (it fades over the second half).")]
    [SerializeField] private float xpPopupSeconds = 2f;

    private GameObject xpCanvas;
    private double xpPending;
    private SkillPack xpPendingPack;
    private float xpGatherTimer;

    private bool xpPopupDecimals;    // set by AddHuskXp for a crush: the XP queued next shows even under 1, with decimals
    private bool xpPendingDecimals;  // the XP gathered now holds such a crush

    // XP that came from the auto clicker is gathered separately and shown beside the cube.
    private bool xpFromAuto;
    private double autoPending;
    private SkillPack autoPendingPack;
    private float autoGatherTimer;

    private void QueueXpPopup(SkillPack pack, double xp)
    {
        if (hideXpPopups || pack == null || xp <= 0d) return;
        if (xpFromAuto)
        {
            if (autoPendingPack != null && autoPendingPack != pack) FlushAutoXpPopup(true);
            autoPendingPack = pack;
            autoPending += xp;
            return;
        }
        if (xpPendingPack != null && xpPendingPack != pack) FlushXpPopup(true);   // the skill changed: show what was gathered for the old one
        if (xpPopupDecimals) { xpPendingDecimals = true; xpPopupDecimals = false; }
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
        if (autoPendingPack != null)
        {
            autoGatherTimer += Time.unscaledDeltaTime;
            if (autoGatherTimer >= gather) FlushAutoXpPopup(false);
        }
    }

    private void FlushXpPopup(bool force)
    {
        xpGatherTimer = 0f;
        int whole = (int)System.Math.Floor(xpPending);
        if (xpPendingDecimals && xpPending >= 0.005d && xpPendingPack != null && !PixelTitleScreen.Showing)
        {
            // A crush is in it: show everything gathered, decimals and all (+0.25), and start again from nothing.
            SkillPack crushPack = xpPendingPack;
            double shown = System.Math.Round(xpPending, 2);
            xpPending = 0d;
            xpPendingDecimals = false;
            if (force) xpPendingPack = null;
            ShowXpPopup(crushPack, shown, false);
            return;
        }
        if (whole < 1 || xpPendingPack == null || PixelTitleScreen.Showing)
        {
            if (force) { xpPending = 0d; xpPendingPack = null; xpPendingDecimals = false; }
            return;
        }
        xpPending -= whole;
        SkillPack pack = xpPendingPack;
        if (force) { xpPending = 0d; xpPendingPack = null; }
        ShowXpPopup(pack, whole, false);
    }

    private void FlushAutoXpPopup(bool force)
    {
        autoGatherTimer = 0f;
        int whole = (int)System.Math.Floor(autoPending);
        if (whole < 1 || autoPendingPack == null || PixelTitleScreen.Showing)
        {
            if (force) { autoPending = 0d; autoPendingPack = null; }
            return;
        }
        autoPending -= whole;
        SkillPack pack = autoPendingPack;
        if (force) { autoPending = 0d; autoPendingPack = null; }
        ShowXpPopup(pack, whole, true);
    }

    /// <summary>Where the cube is on the screen (screen pixels), or null if there is no camera / cube.</summary>
    private Vector2? CubeScreenPoint()
    {
        Camera cam = Camera.main;
        if (cam == null || clicker == null || clicker.PixelTransform == null) return null;
        Vector3 sp = cam.WorldToScreenPoint(clicker.PixelTransform.position);
        if (sp.z <= 0f) return null;
        // Beside the cube: to the right of it, about as far as its apparent half-width plus a margin.
        Vector3 edge = cam.WorldToScreenPoint(clicker.PixelTransform.position + cam.transform.right * clicker.PixelBaseSize * 0.5f);
        float half = Mathf.Abs(edge.x - sp.x);
        return new Vector2(sp.x + half + 70f, sp.y + 30f);
    }

    private void ShowXpPopup(SkillPack pack, double amount, bool nearCube) =>
        ShowPopupText(pack, "+" + amount.ToString(amount == System.Math.Floor(amount) ? "0" : "0.##") + " " + ShownName(pack) + " XP", nearCube);

    /// <summary>The popup itself, with any text (the '+n XP' line, or a Husk Crush bonus text that replaces it).</summary>
    private void ShowPopupText(SkillPack pack, string text, bool nearCube)
    {
        if (xpCanvas == null)
        {
            xpCanvas = PixelUIKit.CreateCanvas("PixelSkills XP Popups", 106, new Vector2(1920f, 1080f), false);
            xpCanvas.transform.SetParent(transform, false);
        }
        RectTransform canvasRect = xpCanvas.GetComponent<RectTransform>();
        float xJitter = Random.Range(-24f, 24f);

        float size = xpPopupSize > 1f ? xpPopupSize : 40f;
        Color textColor = Color.Lerp(pack.accent, Color.white, xpPopupLighten);
        textColor.a = 1f;
        // A dark rounded plate (sized to the text) holds the text, so the popup stands out over a bright floor or sky; the whole popup moves together.
        GameObject holder = new GameObject("Skill XP", typeof(RectTransform), typeof(CanvasGroup));
        holder.transform.SetParent(xpCanvas.transform, false);
        RectTransform r = holder.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        TMP_Text t = PixelUIKit.CreateText(Font, holder.transform, "Text", text, size, TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        t.raycastTarget = false;
        Vector2 pref = t.GetPreferredValues(text, 900f, 0f);
        r.sizeDelta = new Vector2(pref.x + size * 0.9f, size * 1.45f);
        if (xpPopupPlate)
        {
            GameObject plate = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(holder.transform, false);
            plate.transform.SetAsFirstSibling();
            Image img = plate.GetComponent<Image>();
            PixelUIKit.StyleBox(img);
            img.color = xpPopupPlateColor;
            img.raycastTarget = false;
            PixelUIKit.Stretch(img.rectTransform);
        }
        PixelUIKit.Stretch(t.rectTransform);
        StartCoroutine(RiseXpPopup(holder.GetComponent<CanvasGroup>(), r, canvasRect, xJitter, nearCube));
    }

    /// <summary>The popup hangs just under the mouse cursor, follows it, drifts a little further down and fades.</summary>
    private IEnumerator RiseXpPopup(CanvasGroup g, RectTransform r, RectTransform canvasRect, float xJitter, bool nearCube)
    {
        float time = 0f;
        float life = Mathf.Max(0.3f, xpPopupSeconds);
        Transform t = g.transform;
        Vector2 lastCube = Vector2.zero;
        while (time < life && g != null)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / life);
            if (nearCube)
            {
                // Auto clicker XP rises beside the cube (and follows it if it moves).
                Vector2? cube = CubeScreenPoint();
                if (cube.HasValue) lastCube = cube.Value;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, lastCube, null, out Vector2 cubeLocal);
                r.anchoredPosition = cubeLocal + new Vector2(xJitter * 0.5f, 90f * k);
            }
            else
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local);
                r.anchoredPosition = local + new Vector2(xJitter, -56f - 50f * k);
            }
            g.alpha = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            float pop = time < 0.18f ? 1f + 0.35f * Mathf.Sin(time / 0.18f * Mathf.PI) : 1f;   // (a quick pop as it appears, so the eye catches it)
            t.localScale = new Vector3(pop, pop, 1f);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }
}
