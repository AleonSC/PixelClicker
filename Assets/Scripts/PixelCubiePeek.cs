// ---- System notes (moved here from CLAUDE.md) ----
// PixelCubiePeek.cs
//   Cubie peeks out from behind the Save / Load window (component added to the pause menu by PixelPauseMenu.BuildSaveLoadPanel,
//   Setup(panelRect, isActive)): his head (a trimmed copy of the guide's drawing) is a sibling placed directly UNDER the window in the
//   canvas order, so the box covers the rest of him; while the window is open and quiet (no rename / overwrite / result overlay) and
//   there has been no mouse movement or click for idleSeconds (1, to be tuned later) plus a random extraDelay, he slides out of a
//   random edge (left / right / top, random spot along it, showFraction of his head visible, slideSeconds), his pupils dart about
//   (Glance) for stayRange seconds, then he ducks back; any mouse movement / click makes him duck at once; cooldownRange seconds
//   before the next peek. Unscaled time. The slot rows in that window are now exactly as wide as the Save / Load buttons (the scroll
//   bar sits in the margin).
// PixelCubiePeek.cs (more)
//   The Cubie peek is now also on the Settings screen and the Key Bindings screen (PixelPauseMenu.AddCubiePeek(panel): one
//   PixelCubiePeek component per panel, active while that panel is open).
// ---------------------------------------------------
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cubie sneaking a look: while the Save / Load window sits idle (no mouse movement or clicks for <see cref="idleSeconds"/>) he occasionally slides
/// his head out from BEHIND the window's edge (a random side), glances about with darting eyes, then ducks back. Any mouse movement or click makes
/// him duck away. Built by <c>PixelPauseMenu.BuildSaveLoadPanel</c>; drawn as a sibling just under the window so the box hides the rest of him.
/// All timing is unscaled (the game is paused while the menu shows).
/// </summary>
public class PixelCubiePeek : MonoBehaviour
{
    [Header("When he peeks")]
    [Tooltip("Seconds without mouse movement or clicks before Cubie may peek out.")]
    [SerializeField] private float idleSeconds = 1f;

    [Tooltip("After the idle time he waits a random extra moment (min, max seconds) before peeking.")]
    [SerializeField] private Vector2 extraDelay = new Vector2(0f, 2.5f);

    [Tooltip("Seconds he stays out looking around (min, max).")]
    [SerializeField] private Vector2 stayRange = new Vector2(1.6f, 3.2f);

    [Tooltip("Seconds before he may peek again after ducking back (min, max).")]
    [SerializeField] private Vector2 cooldownRange = new Vector2(4f, 10f);

    [Header("Look")]
    [Tooltip("Size of his head peeking out, as a multiple of the drawn size (the head is 200 canvas units wide at 1).")]
    [SerializeField] private float headScale = 0.62f;

    [Tooltip("How much of his head shows when fully out (fraction of its width).")]
    [SerializeField] [Range(0.2f, 0.9f)] private float showFraction = 0.6f;

    [Tooltip("Seconds he takes to slide out and to duck back.")]
    [SerializeField] private float slideSeconds = 0.35f;

    private RectTransform panel, holder, art, pupilL, pupilR;
    private CanvasGroup group;
    private Func<bool> active;
    private Vector2 lastMouse;
    private float lastActivity, nextPeekAt, peekStart, peekLength, glanceTimer, retreatFrom = -1f;
    private float side, tilt;           // which edge (-1 left, 1 right, 2 top) and the lean
    private float spot;                 // where along the edge (-0.35..0.35 of its length)
    private bool peeking, waiting;
    private Vector2 glance;
    private float outAmount;            // 0 hidden .. 1 out

    /// <summary>Builds Cubie behind 'panelRect' (a sibling just before it, so the window covers him). 'isActive' says when the window is open and quiet.</summary>
    public void Setup(RectTransform panelRect, Func<bool> isActive)
    {
        panel = panelRect;
        active = isActive;
        if (holder != null) Destroy(holder.gameObject);

        GameObject h = new GameObject("Cubie Peek", typeof(RectTransform));
        h.transform.SetParent(panel.parent, false);
        holder = h.GetComponent<RectTransform>();
        group = h.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        PlaceUnderPanel();
        holder.anchorMin = panel.anchorMin; holder.anchorMax = panel.anchorMin;
        holder.pivot = new Vector2(0.5f, 0.5f);
        holder.sizeDelta = new Vector2(300f, 300f);

        GameObject a = new GameObject("Art", typeof(RectTransform));
        a.transform.SetParent(holder, false);
        art = a.GetComponent<RectTransform>();
        art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
        art.pivot = new Vector2(0.5f, 0.5f);
        art.sizeDelta = new Vector2(300f, 300f);
        art.localScale = Vector3.one * headScale;
        DrawHead(art);
        holder.gameObject.SetActive(false);
        lastActivity = Time.unscaledTime;
        lastMouse = MouseNow();
    }

    private static Vector2 MouseNow() => PixelInput.PointerPosition();

    private void OnDisable() { if (holder != null) holder.gameObject.SetActive(false); peeking = waiting = false; outAmount = 0f; }

    private void Update()
    {
        if (panel == null || holder == null || active == null) return;
        if (!active())
        {
            if (peeking || holder.gameObject.activeSelf) { peeking = waiting = false; outAmount = 0f; holder.gameObject.SetActive(false); }
            lastActivity = Time.unscaledTime;
            lastMouse = MouseNow();
            return;
        }

        // Activity: the mouse moved or a button was pressed.
        Vector2 mouse = MouseNow();
        bool moved = (mouse - lastMouse).sqrMagnitude > 9f || PixelInput.LeftPressed() || PixelInput.RightPressed();
        lastMouse = mouse;
        if (moved)
        {
            lastActivity = Time.unscaledTime;
            waiting = false;
            if (peeking && retreatFrom < 0f) retreatFrom = Time.unscaledTime;   // caught: duck back at once
        }

        float now = Time.unscaledTime;
        if (!peeking)
        {
            if (now - lastActivity >= idleSeconds && now >= nextPeekAt)
            {
                if (!waiting) { waiting = true; peekStart = now + UnityEngine.Random.Range(extraDelay.x, Mathf.Max(extraDelay.x, extraDelay.y)); }
                else if (now >= peekStart) BeginPeek(now);
            }
            return;
        }

        // Peeking: slide out, glance about, slide back.
        float t = now - peekStart;
        float stay = peekLength;
        float slide = Mathf.Max(0.05f, slideSeconds);
        float target;
        if (retreatFrom >= 0f) target = 1f - Mathf.Clamp01((now - retreatFrom) / slide);
        else if (t < slide) target = Mathf.Clamp01(t / slide);
        else if (t < slide + stay) target = 1f;
        else target = 1f - Mathf.Clamp01((t - slide - stay) / slide);
        if (retreatFrom >= 0f) target = Mathf.Min(target, outAmount);
        outAmount = target;
        bool finished = (retreatFrom < 0f && t >= slide * 2f + stay) || (retreatFrom >= 0f && outAmount <= 0.001f);
        if (finished)
        {
            peeking = false; retreatFrom = -1f; outAmount = 0f;
            holder.gameObject.SetActive(false);
            nextPeekAt = now + UnityEngine.Random.Range(cooldownRange.x, Mathf.Max(cooldownRange.x, cooldownRange.y));
            waiting = false;
            return;
        }
        Place(outAmount, now);
        Glance(now);
    }

    private void BeginPeek(float now)
    {
        waiting = false;
        peeking = true;
        retreatFrom = -1f;
        peekStart = now;
        peekLength = UnityEngine.Random.Range(stayRange.x, Mathf.Max(stayRange.x, stayRange.y));
        float roll = UnityEngine.Random.value;
        side = roll < 0.4f ? -1f : roll < 0.8f ? 1f : 2f;   // left, right or top
        spot = UnityEngine.Random.Range(-0.32f, 0.32f);
        tilt = UnityEngine.Random.Range(6f, 14f);
        outAmount = 0f;
        holder.gameObject.SetActive(true);
        PlaceUnderPanel();
        Place(0f, now);
        PixelAudio.Play("pixel_land");
    }

    /// <summary>Keeps Cubie directly BELOW the window in the canvas order (SetSiblingIndex counts the holder's own old slot, hence the correction).</summary>
    private void PlaceUnderPanel()
    {
        int target = panel.GetSiblingIndex();
        if (holder.GetSiblingIndex() < target) target -= 1;
        holder.SetSiblingIndex(Mathf.Max(0, target));
    }

    /// <summary>Puts the head on the chosen edge: hidden inside the window at 0, 'showFraction' of it outside at 1.</summary>
    private void Place(float amount, float now)
    {
        Vector2 size = panel.rect.size, centre = (Vector2)panel.localPosition + panel.rect.center;
        float head = 200f * headScale;                       // the face is 200 units wide in the art
        float slide = -head * 0.5f + amount * head * showFraction + head * 0.5f * 0f;
        Vector2 pos;
        float rot;
        if (side == 2f)
        {
            pos = new Vector2(centre.x + spot * size.x, centre.y + size.y * 0.5f + slide);
            rot = -tilt * Mathf.Sign(spot == 0f ? 1f : spot) * 0.7f;
        }
        else
        {
            pos = new Vector2(centre.x + side * (size.x * 0.5f + slide), centre.y + spot * size.y);
            rot = -side * tilt;
        }
        pos += new Vector2(0f, Mathf.Sin(now * 5f) * 3f * amount);   // a little bob
        holder.localPosition = pos;
        holder.localRotation = Quaternion.Euler(0f, 0f, rot);
        if (group != null) group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount * 1.6f));   // fades in as he slides out and fades away as he ducks, so he never visibly vanishes behind the box
    }

    private void Glance(float now)
    {
        glanceTimer -= Time.unscaledDeltaTime;
        if (glanceTimer <= 0f)
        {
            glanceTimer = UnityEngine.Random.Range(0.35f, 0.9f);
            glance = new Vector2(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(-4f, 4f));
            if (UnityEngine.Random.value < 0.5f) glance.x = Mathf.Sign(glance.x) * 9f;   // often a hard sideways look
        }
        if (pupilL != null) pupilL.anchoredPosition = new Vector2(-42f + glance.x, 18f + glance.y);
        if (pupilR != null) pupilR.anchoredPosition = new Vector2(42f + glance.x, 18f + glance.y);
    }

    // ---- drawing: Cubie's head (teal cube, narrowed eyes, brows, smirk, hat), centred on the holder ----

    private static RectTransform Block(Transform parent, string name, Color color, Vector2 size, Vector2 pos, float rotation = 0f) =>
        PixelBlockArt.Block(parent, name, color, size, pos, rotation, PixelBlockArt.Middle);

    private void DrawHead(Transform a)
    {
        Color face = new Color(0.22f, 0.62f, 0.6f), top = new Color(0.45f, 0.82f, 0.78f), side = new Color(0.14f, 0.42f, 0.42f);
        Color dark = new Color(0.06f, 0.1f, 0.12f), white = new Color(0.97f, 0.97f, 0.9f), red = new Color(0.85f, 0.22f, 0.25f);
        Block(a, "Side", side, new Vector2(200f, 200f), new Vector2(12f, 10f));
        Block(a, "Face", face, new Vector2(200f, 200f), new Vector2(0f, 0f));
        Block(a, "Top", top, new Vector2(200f, 26f), new Vector2(6f, 110f));
        Block(a, "Hat Brim", dark, new Vector2(150f, 14f), new Vector2(0f, 120f));
        Block(a, "Hat Top", dark, new Vector2(100f, 40f), new Vector2(0f, 146f));
        Block(a, "Hat Band", red, new Vector2(100f, 10f), new Vector2(0f, 132f));
        Block(a, "Eye L", white, new Vector2(56f, 38f), new Vector2(-42f, 20f));
        Block(a, "Eye R", white, new Vector2(56f, 38f), new Vector2(42f, 20f));
        pupilL = Block(a, "Pupil L", dark, new Vector2(18f, 26f), new Vector2(-42f, 18f));
        pupilR = Block(a, "Pupil R", dark, new Vector2(18f, 26f), new Vector2(42f, 18f));
        Block(a, "Lid L", face, new Vector2(60f, 20f), new Vector2(-42f, 33f));
        Block(a, "Lid R", face, new Vector2(60f, 20f), new Vector2(42f, 33f));
        Block(a, "Brow L", dark, new Vector2(70f, 10f), new Vector2(-42f, 48f), -14f);
        Block(a, "Brow R", dark, new Vector2(70f, 10f), new Vector2(42f, 48f), 14f);
        Block(a, "Mouth", dark, new Vector2(70f, 9f), new Vector2(-6f, -40f), 6f);
        Block(a, "Smirk", dark, new Vector2(10f, 18f), new Vector2(34f, -30f));
    }
}
