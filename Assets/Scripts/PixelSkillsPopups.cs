// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsPopups.cs
//   Part of partial class PixelSkills: '+n <Skill> XP' popups while a skill trains (AddXp calls QueueXpPopup; XP is gathered
//   xpPopupGatherSeconds (0 = 0.6 s) and shown as whole numbers, fractions of auto clicks carry over; UpdateXpPopups runs from the
//   window's Update; XP from the auto clicker is gathered separately and appears beside the cube instead (xpFromAuto set by
//   OnCollected, FlushAutoXpPopup, CubeScreenPoint, rises beside the cube and follows it); text in the pack's accent colour, hangs
//   just under the mouse cursor and follows it (drifting a little further down as it fades), on its own canvas 106 so it never mixes
//   with the other +n texts; hideXpPopups, xpPopupSize). A Husk Crush (AddHuskXp crushed) marks its XP 'show decimals' (xpPopupDecimals ->
//   AddHuskXp(popupText) shows a short text INSTEAD of the '+n XP' line (Husk Crush's bonuses, ShowPopupText). xpPendingDecimals): that popup shows the gathered amount even under 1, rounded to 2 decimals ('+0.25 Standard XP'), so every crush is seen.
// ---------------------------------------------------
using System.Collections;
using TMPro;
using UnityEngine;

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
    [Tooltip("Text size of the popup. 0 = 34.")]
    [SerializeField] private float xpPopupSize = 0f;

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
        ShowPopupText(pack, "+" + amount.ToString(amount == System.Math.Floor(amount) ? "0" : "0.##") + " " + pack.displayName + " XP", nearCube);

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

        float size = xpPopupSize > 1f ? xpPopupSize : 34f;
        TMP_Text t = PixelUIKit.CreateText(Font, xpCanvas.transform, "Skill XP", text, size, TextAlignmentOptions.Center, FontStyles.Bold, pack.accent);
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(520f, size * 1.6f);
        StartCoroutine(RiseXpPopup(t, r, canvasRect, xJitter, pack.accent, nearCube));
    }

    /// <summary>The popup hangs just under the mouse cursor, follows it, drifts a little further down and fades.</summary>
    private IEnumerator RiseXpPopup(TMP_Text t, RectTransform r, RectTransform canvasRect, float xJitter, Color color, bool nearCube)
    {
        float time = 0f;
        const float life = 1.5f;
        Vector2 lastCube = Vector2.zero;
        while (time < life && t != null)
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
            Color c = color;
            c.a = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            t.color = c;
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }
}
