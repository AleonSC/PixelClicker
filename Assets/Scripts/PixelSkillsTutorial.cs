// ---- System notes (moved here from CLAUDE.md) ----
// PixelSkillsTutorial.cs / PixelTutorialDim.cs
//   Skills tutorial (part of partial class PixelSkills, started from the window's Update by UpdateTutorialStart the first time a skill
//   pack is available and the screen is calm: not while the shop tutorial is still due, no tip / visitor / pause / takeover;
//   enableTutorial, tutorialStartDelay 0 = 2 s, tutorialSteps empty = the built-ins): same format as the shop tutorial -
//   PixelTutorialDim.Begin fades the screen darker and pauses the game (a click-blocking overlay on canvas 600;
//   PixelTutorialDim.Active is true meanwhile and the Skills tab / Shop button ignore clicks), Cubie narrates
//   (PixelGuideVendor.BeginNarration) and his stick points at the part each step describes (PixelGuideVendor.PointOverride,
//   TutorialFocus Window / Header / Cards / Train / Tree / Mastery / Pin), each step is a small Got it box beside the Skills window
//   (PixelNotice beside: windowRect); the Train / Tree steps look at the first real pack with a tree and the player's own selection
//   comes back at the end. The flow is the shared PixelTutorialRunner. Remembered per save as stat guide.skillsTutorial (TutorialDoneKey, cleared by Settings > Replay tutorial).
//   It replaced the old one-box introText / skills_intro tip. PixelTutorialDim is shared with the shop tutorial (dimAmount /
//   dimSeconds still live on PixelShopTutorial).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Part of <c>partial class PixelSkills</c>: the skills tutorial. The first time a skill pack becomes available (and the screen is calm)
/// the screen fades a little darker and pauses, Cubie stands at the bottom right and points at the part of the Skills window each small
/// "Got it" box describes - just like the shop tutorial. The Skills tab ignores clicks until it is over. Settings > Replay tutorial
/// plays it again.
/// </summary>
public partial class PixelSkills
{
    /// <summary>Which part of the Skills window a step points at.</summary>
    public enum TutorialFocus { Window, Header, Cards, Train, Tree, Mastery, Pin }

    [System.Serializable]
    public class TutorialStep
    {
        [TextArea(2, 5)] public string text;
        public TutorialFocus focus = TutorialFocus.Window;
    }

    /// <summary>Stat counter (saved with the game) that says this save has seen the skills tutorial; Cubie's Replay clears it.</summary>
    public const string TutorialDoneKey = "guide.skillsTutorial";

    [Header("Skills tutorial")]
    [Tooltip("Run the skills tutorial the first time a skill pack becomes available.")]
    [SerializeField] private bool enableTutorial = true;
    [Tooltip("Cubie walks through the Skills window by himself the first time a skill pack is available. Off: skills are introduced by Standard's level-2 popup instead (Settings > Replay tutorial still plays the walk-through).")]
    [SerializeField] private bool autoStartTutorial = false;

    [Min(0f)]
    [Tooltip("Seconds of calm (no tip, no visitor, not paused) before the tutorial starts (0 = 2).")]
    [SerializeField] private float tutorialStartDelay = 0f;

    [Tooltip("The steps, in order. Empty = the built-in steps. Each shows its text beside the Skills window while Cubie points at its focus.")]
    [SerializeField] private List<TutorialStep> tutorialSteps = new List<TutorialStep>();

    private PixelTutorialRunner tutorialRunner;   // the shared tutorial flow
    private bool tutorialRunning => tutorialRunner != null && tutorialRunner.Running;
    private float tutorialReady;
    private bool tutorialWanted;   // its start conditions have held long enough: PixelTipQueue starts it at its turn
    private int tutorialSavedSelection = -1;

    /// <summary>True while the tutorial runs.</summary>
    public bool TutorialRunning => tutorialRunning;

    private static List<TutorialStep> DefaultTutorialSteps()
    {
        return new List<TutorialStep>
        {
            new TutorialStep { focus = TutorialFocus.Window, text = "Welcome to Skills! Every pixel you collect now trains a skill. Standard levels from the very first click, and the skill packs you buy add skills of their own." },
            new TutorialStep { focus = TutorialFocus.Header, text = "The bar along the top shows the skill you are training, its level and its XP. Fill it and the skill levels up." },
            new TutorialStep { focus = TutorialFocus.Cards, text = "Pick a skill on the left. Standard is always there, every pack you unlock adds a card, and locked ones show ???. The orange ! on a card says what it swaps out and what it spawns." },
            new TutorialStep { focus = TutorialFocus.Train, text = "Press Train to put a skill in play. A pack swaps the basic cubes in your rotation for its own pixels, and only that skill earns XP. XP is kept per skill, so you can switch back and forth whenever you like." },
            new TutorialStep { focus = TutorialFocus.Train, text = "Each level unlocks a perk, listed on the right. Standard's perks work in every rotation, like longer-lasting old pixels and a roomier floor." },
            new TutorialStep { focus = TutorialFocus.Tree, text = "Every level above 1 also gives a skill point. Spend it in the Skill tree: open it with the Skill tree button on a skill's page." },
            new TutorialStep { focus = TutorialFocus.Window, text = "Careful: skill packs bring bad blocks too. Right-click a hazard to defuse it safely, or it goes off and costs you XP. What you do with old pixels matters as well: clearing out the useful ones earns bonus XP, junk costs it." },
            new TutorialStep { focus = TutorialFocus.Mastery, text = "The Mastery bar along the bottom adds up all your skills. It only fills completely when every skill is maxed out." },
            new TutorialStep { focus = TutorialFocus.Pin, text = "The pin button turns your current skill into a small window with an XP bar you can move around. Open Skills any time with the tab on the left edge of the screen." },
        };
    }

    /// <summary>Called every frame from the window's Update: starts the tutorial when it is due.</summary>
    private void UpdateTutorialStart(bool available, bool open)
    {
        tutorialWanted = false;
        if (tutorialRunning || !enableTutorial || !autoStartTutorial || !available) { tutorialReady = 0f; return; }
        if (PixelStats.Total(TutorialDoneKey) >= 1d || !PixelHints.TipsEnabled) return;
        PixelShop shop = PixelFind.Cached<PixelShop>();
        bool shopTutorialDue = shop != null && shop.ShopAvailable && PixelStats.Total(PixelShopTutorial.DoneKey) < 1d;
        if (shopTutorialDue || PixelTogglesTutorial.Due || PixelTogglesTutorial.Running || !PixelCalm.ForTip)
        {
            tutorialReady = 0f;
            return;
        }
        tutorialReady += Time.unscaledDeltaTime;
        if (tutorialReady < (tutorialStartDelay > 0f ? tutorialStartDelay : 2f)) return;
        tutorialWanted = true;   // (PixelTipQueue starts it when it is its turn)
    }

    /// <summary>Plays the tutorial now, whether or not it was seen.</summary>
    public void StartTutorial()
    {
        if (tutorialRunning || !built) return;
        if (tutorialSteps == null || tutorialSteps.Count == 0) tutorialSteps = DefaultTutorialSteps();
        if (tutorialRunner == null)
            tutorialRunner = new PixelTutorialRunner
            {
                doneKey = TutorialDoneKey,
                beside = () => windowRect,   // the boxes sit just right of the Skills window
                onBegin = OpenWindow,
                onFinish = () => { if (tutorialSavedSelection >= 0) { selected = tutorialSavedSelection; tutorialSavedSelection = -1; RefreshWindow(); } },   // the player's own selection comes back
            };
        List<PixelTutorialRunner.Step> list = new List<PixelTutorialRunner.Step>();
        foreach (TutorialStep st in tutorialSteps)
        {
            TutorialStep step = st;
            list.Add(new PixelTutorialRunner.Step
            {
                text = step.text,
                enter = () =>
                {
                    // The Tree / Train steps need a pack with a tree on screen: look at the first real pack.
                    if (step.focus != TutorialFocus.Tree && step.focus != TutorialFocus.Train) return;
                    if (tutorialSavedSelection < 0) tutorialSavedSelection = selected;
                    int pick = packs.FindIndex(p => p.id != StandardId && IsAvailable(p) && p.nodes != null && p.nodes.Length > 0);
                    if (pick >= 0) selected = pick;
                    RefreshWindow();
                },
                point = () => TutorialTarget(step.focus),
            });
        }
        tutorialRunner.Run(list);
    }

    /// <summary>The middle of the focused part of the window, in screen pixels (null = the whole window).</summary>
    private Vector3? TutorialTarget(TutorialFocus focus)
    {
        if (windowObject == null || !windowObject.activeInHierarchy) return null;
        RectTransform r = windowRect;
        switch (focus)
        {
            case TutorialFocus.Header: r = headerLabel != null ? headerLabel.rectTransform : r; break;
            case TutorialFocus.Cards: r = listScroll != null ? listScroll.GetComponent<RectTransform>() : r; break;
            case TutorialFocus.Train: r = trainButton != null ? trainButton.GetComponent<RectTransform>() : r; break;
            case TutorialFocus.Tree: r = treeToggle != null && treeToggle.gameObject.activeInHierarchy ? treeToggle.GetComponent<RectTransform>() : r; break;
            case TutorialFocus.Mastery: { Transform t = windowObject.transform.Find("Mastery Bar"); if (t != null) r = t as RectTransform; break; }
            case TutorialFocus.Pin: { Transform t = windowObject.transform.Find("Pin Button"); if (t != null) r = t as RectTransform; break; }
        }
        if (r == null) return null;
        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        return (c[0] + c[2]) * 0.5f;
    }
}
