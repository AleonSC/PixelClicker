using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Part of <see cref="PixelSkills"/>: a rising '+n Skill XP' popup in the skill's own colour whenever the skill in training gains XP. XP that comes
/// in small pieces (auto clicks give a fraction) is gathered for a moment and shown as whole numbers, so there is one clean popup instead of a flood.
/// It rises from the pinned tracker when that is showing, else from beside the cube, and uses its own canvas so it never mixes with the other +n texts.
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

    private void QueueXpPopup(SkillPack pack, double xp)
    {
        if (hideXpPopups || pack == null || xp <= 0d) return;
        if (xpPendingPack != null && xpPendingPack != pack) FlushXpPopup(true);   // the skill changed: show what was gathered for the old one
        xpPendingPack = pack;
        xpPending += xp;
    }

    private void UpdateXpPopups()
    {
        if (xpPendingPack == null) return;
        xpGatherTimer += Time.unscaledDeltaTime;
        if (xpGatherTimer >= (xpPopupGatherSeconds > 0.05f ? xpPopupGatherSeconds : 0.6f)) FlushXpPopup(false);
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
        ShowXpPopup(pack, whole);
    }

    private void ShowXpPopup(SkillPack pack, int amount)
    {
        if (xpCanvas == null)
        {
            xpCanvas = PixelUIKit.CreateCanvas("PixelSkills XP Popups", 106, new Vector2(1920f, 1080f), false);
            xpCanvas.transform.SetParent(transform, false);
        }
        RectTransform canvasRect = xpCanvas.GetComponent<RectTransform>();

        Vector2 at;
        if (trackerBuilt && trackerCanvas != null && trackerCanvas.activeSelf && trackerOn)
            at = trackerRect.anchoredPosition + new Vector2(0f, trackerRect.sizeDelta.y * 0.5f + 8f);   // rises from the pinned tracker
        else
        {
            Camera cam = clicker != null && clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
            Vector2 screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (cam != null && clicker != null && clicker.PixelTransform != null) screen = cam.WorldToScreenPoint(clicker.PixelTransform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out at);
            at += new Vector2(160f, 110f);   // beside the cube
        }
        at += new Vector2(Random.Range(-30f, 30f), 0f);

        float size = xpPopupSize > 1f ? xpPopupSize : 34f;
        TMP_Text t = PixelUIKit.CreateText(Font, xpCanvas.transform, "Skill XP", "+" + amount + " " + pack.displayName + " XP", size, TextAlignmentOptions.Center, FontStyles.Bold, pack.accent);
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(520f, size * 1.6f);
        r.anchoredPosition = at;
        StartCoroutine(RiseXpPopup(t, r, pack.accent));
    }

    private IEnumerator RiseXpPopup(TMP_Text t, RectTransform r, Color color)
    {
        Vector2 start = r.anchoredPosition;
        float time = 0f;
        const float life = 1.5f;
        while (time < life && t != null)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / life);
            r.anchoredPosition = start + new Vector2(0f, 110f * (1f - (1f - k) * (1f - k)));
            Color c = color;
            c.a = k < 0.5f ? 1f : 1f - (k - 0.5f) / 0.5f;
            t.color = c;
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }
}
