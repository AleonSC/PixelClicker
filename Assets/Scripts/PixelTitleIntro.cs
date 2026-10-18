// ---- System notes ----
// PixelTitleIntro.cs
//   Part of partial class PixelTitleScreen: the main menu's INTRO when the game launches (Open(false), not when coming back from the game, not when a
//   New game auto-starts). The screen starts fully black; once the blurred picture behind is ready (and introHoldSeconds more) the black FOLDS DOWN
//   from the top and bottom into the middle bar (introCollapseSeconds, easing in and out, its colour turning into the bar's), the bar gives a small
//   thump, then everything else comes in: the bar's glow fades up, the logo drops in from above, the Play / Load / Settings / Quit buttons and the
//   other widgets pop in one after another with a little overshoot (introStagger apart), the rest of the foreground fades in. Buttons work once it
//   is done. Any key or click skips straight to the end. playIntro off = the old instant screen.
// ---------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelTitleScreen
{
    [Header("Launch intro")]
    [Tooltip("When the game launches, the black screen folds down into the menu bar and the menu appears piece by piece. Off = the menu shows at once.")]
    [SerializeField] private bool playIntro = true;
    [Tooltip("Seconds the screen stays black after the background picture is ready.")]
    [SerializeField] private float introHoldSeconds = 0.35f;
    [Tooltip("Seconds the black screen takes to fold down into the bar.")]
    [SerializeField] private float introCollapseSeconds = 0.9f;
    [Tooltip("Seconds between one button / widget popping in and the next.")]
    [SerializeField] private float introStagger = 0.08f;
    [Tooltip("Seconds each button / widget takes to pop in.")]
    [SerializeField] private float introPopSeconds = 0.35f;
    [Tooltip("How far above its spot the logo starts (canvas units) before it drops in.")]
    [SerializeField] private float introLogoDrop = 260f;

    private float logoIntroOffset;   // added to the logo's position by Update while it drops in
    private bool introSkip;

    private IEnumerator IntroRoutine()
    {
        introSkip = false;
        foreach (Button b in buttons) if (b != null) b.interactable = false;
        CanvasGroup fg = foreground.GetComponent<CanvasGroup>();
        if (fg == null) fg = foreground.AddComponent<CanvasGroup>();

        // The black panel on top of everything; it becomes the bar.
        GameObject panelGo = new GameObject("Intro Black", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasRoot.transform, false);
        panelGo.transform.SetAsLastSibling();
        Image panel = panelGo.GetComponent<Image>();
        panel.color = Color.black;
        panel.raycastTarget = true;   // (nothing behind it can be clicked while it covers the screen)
        RectTransform pr = panelGo.GetComponent<RectTransform>();
        pr.pivot = new Vector2(0.5f, 0.5f);
        Color barTint = PixelHud.BarTint(barColor);

        void Hidden()
        {
            foreach (RectTransform r in shrinkRects) if (r != null) r.localScale = Vector3.zero;
            if (logoView != null) logoView.SetAlpha(0f);
            logoIntroOffset = introLogoDrop;
            glowFade = 0f;
            fg.alpha = 0f;
        }
        void SetPanel(float e)
        {
            pr.anchorMin = new Vector2(0f, Mathf.Lerp(0f, 0.5f, e));
            pr.anchorMax = new Vector2(1f, Mathf.Lerp(1f, 0.5f, e));
            pr.sizeDelta = new Vector2(0f, barHeight * e);
            pr.anchoredPosition = Vector2.zero;
            panel.color = Color.Lerp(Color.black, barTint, e * e);
        }
        bool Skipping() => introSkip || (introSkip = PixelInput.AnyInputPressed());

        Hidden();
        SetPanel(0f);
        // The Galactic Arcade studio intro plays first (once per launch, on its own top canvas; nothing when it is off).
        yield return PixelStudioIntro.PlayAtLaunch();

        // 1. Black until the blurred picture exists, then a short hold.
        float wait = 0f;
        while ((!pictureReady || wait < introHoldSeconds) && wait < 3f && !Skipping())
        {
            if (pictureReady) wait += Time.unscaledDeltaTime; else wait += Time.unscaledDeltaTime * 0.25f;
            Hidden();
            yield return null;
        }

        // 2. The black folds down into the bar.
        float t = 0f, dur = Mathf.Max(0.05f, introCollapseSeconds);
        while (t < dur && !Skipping())
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            SetPanel(k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f);   // ease in-out
            Hidden();
            yield return null;
        }
        Destroy(panelGo);

        // 3. A little thump of the bar, then everything pops in one after another.
        int count = shrinkRects.Count;
        float total = 0.2f + introStagger * count + introPopSeconds;
        t = 0f;
        while (t < total && !Skipping())
        {
            t += Time.unscaledDeltaTime;
            float thump = t < 0.18f ? Mathf.Sin(t / 0.18f * Mathf.PI) * 0.12f : 0f;
            barRect.sizeDelta = new Vector2(0f, barHeight * (1f + thump));
            glowFade = Mathf.Clamp01(t / 0.5f);
            fg.alpha = Mathf.Clamp01((t - 0.1f) / 0.3f);
            float logo = Mathf.Clamp01((t - 0.05f) / 0.55f);
            if (logoView != null) logoView.SetAlpha(Mathf.Clamp01(logo * 2f));
            logoIntroOffset = introLogoDrop * (1f - EaseOutBack(logo));
            for (int i = 0; i < count; i++)
            {
                if (shrinkRects[i] == null) continue;
                float p = Mathf.Clamp01((t - 0.2f - introStagger * i) / Mathf.Max(0.05f, introPopSeconds));
                shrinkRects[i].localScale = Vector3.one * (p <= 0f ? 0f : EaseOutBack(p));
            }
            yield return null;
        }

        // Done (or skipped): the final look.
        if (panelGo != null) Destroy(panelGo);
        barRect.sizeDelta = new Vector2(0f, barHeight);
        logoIntroOffset = 0f;
        if (!playing)
        {
            ApplyTransition(0f);
            glowFade = 1f;
            foreach (Button b in buttons) if (b != null) b.interactable = true;
        }
        fg.alpha = 1f;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }
}
