// ---- System notes ----
// PixelTogglesTutorial.cs
//   Toggles tutorial (added by PixelToggles.Start; part of the "Show first-time tips" intro together with the skills tutorial): when the Auto Clicker is bought with tips on, PixelShop.ApplyPurchase calls
//   PixelTogglesTutorial.Request() (stat guide.togglesDue, saved) INSTEAD OF the plain hint box beside the Toggles tab (with tips off the old hint box still shows). At the next calm moment (title screen over,
//   no Cubie visit, no other tip / window / tutorial, shop closed, not paused, startDelay 2 s) the screen dims and pauses (PixelTutorialDim) and Cubie narrates like the shop tutorial (BeginNarration, stick
//   at the part each step describes via PixelGuideVendor.PointOverride, small Got it boxes that follow the Toggles window - PixelNotice nearScreen): the Toggles tab, the Upgrades group (where the Auto Clicker
//   is ticked on), the other groups, then a HINT AT SKILLS: skills train from the pixels you collect and the first one comes with the Woodcutter's Pack (the lumberjack's set) in the Shop's Pixels tab, which makes
//   the Skills tab appear (the Skills tutorial then plays by itself; it waits while this one is due or running). Steps are an Inspector list (empty = the built-ins). Done = stat guide.togglesTutorial
//   (Settings > Replay tutorial clears it and, when the Toggles tab is there, asks for it again). Skipped entirely when TipsEnabled is off.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelTogglesTutorial : MonoBehaviour
{
    public enum Focus { Tab, Window, ShopButton }

    [System.Serializable]
    public class Step
    {
        [TextArea(2, 5)] public string text;
        [Tooltip("What Cubie's stick points at.")] public Focus focus = Focus.Window;
        [Tooltip("The Toggles group to show for this step (0 Pixels, 1 Upgrades, 2 Minigames, 3 Vendors, 4 Pets); -1 = leave the window as it is, -2 = close it.")] public int group = -1;
    }

    public const string DoneKey = "guide.togglesTutorial";
    public const string DueKey = "guide.togglesDue";

    [Tooltip("Run the Toggles tutorial after the Auto Clicker is bought.")]
    [SerializeField] private bool enableTutorial = true;

    [Min(0f)]
    [Tooltip("Seconds of calm before it starts (0 = 2).")]
    [SerializeField] private float startDelay = 0f;

    [Tooltip("The steps, in order. Empty = the built-in steps.")]
    [SerializeField] private List<Step> steps = new List<Step>();

    private static PixelTogglesTutorial instance;
    private bool running;
    private float ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>True while it runs (the skills tutorial waits).</summary>
    public static bool Running => instance != null && instance.running;

    /// <summary>True when it is due but has not been shown yet.</summary>
    public static bool Due => instance != null && instance.enableTutorial && PixelHints.TipsEnabled && PixelStats.Total(DueKey) >= 1d && PixelStats.Total(DoneKey) < 1d;

    /// <summary>Would a purchase of the Auto Clicker right now start the tutorial (so the plain hint box is not needed)?</summary>
    public static bool WillRun => instance != null && instance.enableTutorial && PixelHints.TipsEnabled && PixelStats.Total(DoneKey) < 1d;

    /// <summary>The Auto Clicker was bought: play the tutorial at the next calm moment.</summary>
    public static void Request() { if (WillRun) PixelStats.Best(DueKey, 1d); }

    /// <summary>Settings > Replay tutorial: plays again if the Toggles tab exists.</summary>
    public static void Replay()
    {
        PixelStats.Clear(DoneKey);
        PixelStats.Clear(DueKey);
        if (PixelToggles.TabVisible) PixelStats.Best(DueKey, 1d);
    }

    private void Awake() { instance = this; }
    private void OnDestroy() { if (instance == this) instance = null; }

    private static List<Step> DefaultSteps()
    {
        return new List<Step>
        {
            new Step { focus = Focus.Tab, group = -2, text = "Psst - you bought the Auto Clicker! It starts switched OFF. See the Toggles tab on the right edge of the screen? Every on / off switch in the game lives there. Let me open it." },
            new Step { focus = Focus.Window, group = 1, text = "Upgrades: here is your Auto Clicker. Tick it to start it, untick it to stop. Pixel Grabbing and Time Stop will show up here as soon as you own them." },
            new Step { focus = Focus.Window, group = 0, text = "Only things you own are listed. Pixels: some special pixels (like Glass) spawn on their own, so switch off the ones you don't want. Minigames, Vendors and Pets get their own groups, so you can shape your own game." },
            new Step { focus = Focus.ShopButton, group = -2, text = "One more thing, between us... Everything you collect trains SKILLS. Open the Shop, go to the Pixels tab and buy the Woodcutter's Pack - a lumberjack's starter set. It unlocks Woodcutting, your first skill, and a Skills tab appears on the left edge." },
        };
    }

    private void Update()
    {
        if (running || !Due) { ready = 0f; return; }
        PixelShop shop = PixelFind.First<PixelShop>();
        bool shopTutorialDue = shop != null && shop.ShopAvailable && PixelStats.Total(PixelShopTutorial.DoneKey) < 1d;
        if (shopTutorialDue || (shop != null && shop.WindowOpen) || !PixelToggles.TabVisible || PixelTutorialDim.Active || PixelTitleScreen.Showing || PixelHints.IntroHoldsGuide ||
            PixelGuideVendor.Pending || PixelGuideVendor.Visiting || PixelNotice.IsShowing || PixelPauseMenu.IsPaused || PixelMinigame.TakeoverActive || Time.timeScale <= 0f ||
            PixelWindows.AnyOpen() || PixelFind.First<PixelSkills>() is PixelSkills sk && sk.TutorialRunning)
        {
            ready = 0f;
            return;
        }
        ready += Time.unscaledDeltaTime;
        if (ready < (startDelay > 0f ? startDelay : 2f)) return;
        if (steps == null || steps.Count == 0) steps = DefaultSteps();
        running = true;
        PixelTutorialDim.Begin(() => ShowStep(0));
    }

    private float BoxHeight()
    {
        int longest = 0;
        foreach (Step st in steps) if (st != null && st.text != null) longest = Mathf.Max(longest, st.text.Length);
        return Mathf.Max(260f, 84f + (Mathf.CeilToInt(longest / 28f) + 1) * 30f);
    }

    private static Vector3? Centre(RectTransform r)
    {
        if (r == null || !r.gameObject.activeInHierarchy) return null;
        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        return (c[0] + c[2]) * 0.5f;
    }

    /// <summary>Where the Got it box follows: the middle of the Toggles window's left edge when it is open, else the Toggles tab.</summary>
    private static Vector2? BoxAnchor()
    {
        RectTransform w = PixelToggles.WindowRect;
        if (w != null && w.gameObject.activeInHierarchy)
        {
            Vector3[] c = new Vector3[4];
            w.GetWorldCorners(c);
            return (Vector2)((c[0] + c[1]) * 0.5f);
        }
        Vector3? t = Centre(PixelToggles.TabRect);
        return t.HasValue ? (Vector2?)(Vector2)t.Value : null;
    }

    private void ShowStep(int index)
    {
        if (index >= steps.Count)
        {
            running = false;
            PixelGuideVendor.PointOverride = null;
            PixelGuideVendor.EndNarration();
            PixelToggles.TutorialClose();
            PixelTutorialDim.End();
            PixelStats.Best(DoneKey, 1d);
            PixelStats.Clear(DueKey);
            return;
        }
        if (index == 0) PixelGuideVendor.BeginNarration();
        else PixelGuideVendor.NarratorReact();

        Step step = steps[index];
        if (step.group >= 0) PixelToggles.TutorialOpen(step.group);
        else if (step.group == -2) PixelToggles.TutorialClose();
        PixelShop shop = PixelFind.First<PixelShop>();
        PixelGuideVendor.PointOverride = () =>
        {
            switch (step.focus)
            {
                case Focus.Tab: return Centre(PixelToggles.TabRect);
                case Focus.ShopButton: return shop != null ? Centre(shop.ShopButtonRect) : null;
                default: return Centre(PixelToggles.WindowRect) ?? Centre(PixelToggles.TabRect);
            }
        };
        PixelNotice.Show(step.text, 0f, small: true, nearScreen: BoxAnchor, onClosed: () => ShowStep(index + 1), minHeight: BoxHeight(), onBack: index > 0 ? (System.Action)(() => ShowStep(index - 1)) : null);
    }
}
