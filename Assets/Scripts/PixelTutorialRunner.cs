// ---- System notes ----
// PixelTutorialRunner.cs
//   The ONE guided-tutorial flow (shop, Toggles and skills tutorials all run on it): a tutorial is a list of steps (PixelTutorialRunner.Step:
//   text, enter = what to open / select for it, point = where Cubie's stick points, null = his default) plus where its boxes sit (beside a
//   window rect, or nearScreen = a point that follows something), a done key and optional onBegin / onFinish. Run():
//   PixelTutorialDim fades the screen darker and pauses the game, Cubie narrates (PixelGuideVendor.BeginNarration, NarratorReact on each
//   new step, EndNarration), each step is a small PixelNotice "Got it" box with Back / Skip, every box the same height (BoxHeight: from the
//   longest step, never under minBoxHeight) so Got it never moves; at the end the dim fades back, the stick override clears, onFinish runs
//   and the done stat (PixelStats, per save) is set. WHEN a tutorial may start stays in each tutorial (their conditions differ).
//   Adding a tutorial = a step list + one PixelTutorialRunner.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public sealed class PixelTutorialRunner
{
    public sealed class Step
    {
        public string text;
        /// <summary>Called when the step shows (open a tab / window, select something). May be null.</summary>
        public System.Action enter;
        /// <summary>Where Cubie's stick points (screen pixels; null result = the whole window / his default). Null = no override (his default target).</summary>
        public System.Func<Vector3?> point;
    }

    /// <summary>Stat (saved with the game) set to 1 when the tutorial ends; the tutorial's own start check reads it.</summary>
    public string doneKey;
    /// <summary>The boxes sit just to the right of this window (or use nearScreen).</summary>
    public System.Func<RectTransform> beside;
    /// <summary>The boxes follow this screen point instead (when beside is null).</summary>
    public System.Func<Vector2?> nearScreen;
    /// <summary>Every box is at least this tall (UI units).</summary>
    public float minBoxHeight = 260f;
    /// <summary>The dim's darkness and fade time (0 = PixelTutorialDim's defaults).</summary>
    public float dimAmount, dimSeconds;
    /// <summary>Called once as the first box shows (e.g. open the window it is about).</summary>
    public System.Action onBegin;
    /// <summary>Called when it ends (after the last step or Skip): put things back.</summary>
    public System.Action onFinish;

    private List<Step> steps;
    public bool Running { get; private set; }

    /// <summary>Plays the steps now (the caller has checked it may start).</summary>
    public void Run(List<Step> list)
    {
        if (Running || list == null || list.Count == 0) return;
        steps = list;
        Running = true;
        PixelNotice.Hide();
        PixelTutorialDim.Begin(() => Show(0), dimAmount, dimSeconds);
    }

    /// <summary>One height for every box: tall enough for the longest step (a rough characters-per-line estimate), never under minBoxHeight.</summary>
    private float BoxHeight()
    {
        int longest = 0;
        foreach (Step st in steps) if (st != null && st.text != null) longest = Mathf.Max(longest, st.text.Length);
        return Mathf.Max(minBoxHeight, 124f + (Mathf.CeilToInt(longest / 28f) + 1) * 30f);
    }

    private void Show(int index)
    {
        if (index >= steps.Count) { Finish(); return; }
        if (index == 0) { PixelGuideVendor.BeginNarration(); onBegin?.Invoke(); }
        else PixelGuideVendor.NarratorReact();

        Step step = steps[index];
        step.enter?.Invoke();
        PixelGuideVendor.PointOverride = step.point;
        System.Action back = index > 0 ? (System.Action)(() => Show(index - 1)) : null;
        if (beside != null)
            PixelNotice.Show(step.text, 0f, small: true, beside: beside(), onClosed: () => Show(index + 1), minHeight: BoxHeight(), onBack: back, onSkip: () => Show(steps.Count));
        else
            PixelNotice.Show(step.text, 0f, small: true, nearScreen: nearScreen, onClosed: () => Show(index + 1), minHeight: BoxHeight(), onBack: back, onSkip: () => Show(steps.Count));
    }

    private void Finish()
    {
        Running = false;
        PixelGuideVendor.PointOverride = null;
        PixelGuideVendor.EndNarration();
        onFinish?.Invoke();
        PixelTutorialDim.End();
        if (!string.IsNullOrEmpty(doneKey)) PixelStats.Best(doneKey, 1d);
    }
}
