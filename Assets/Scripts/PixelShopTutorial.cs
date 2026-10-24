// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopTutorial.cs
//   Shop tutorial (added by PixelShop). ONLY the shop's blue '?' hold button plays it now (autoStart off: when the shop becomes available it is just marked
//   done, DoneKey, so the tutorials waiting for it carry on; Cubie's shop hold no longer starts it). With autoStart on (old behaviour): the first time the Shop button is available (PixelShop.ShopAvailable) and the screen is calm
//   (title screen over, PixelHints.IntroHoldsGuide false, no other tip, no takeover minigame, not paused; startDelay 2 s) a series of
//   small "Got it" boxes (PixelNotice.Show(small: true, beside: shop.PanelRect), 440 wide, just right of the shop window) walks
//   through the shop, each step opening its tab via PixelShop.TutorialOpen(tab, subTab): 5 steps since 2026-10-24 - the currency list +
//   greyed-out note, Pixels, Upgrades (Features / Value / Operation in one box), Consumables, Minigames. Before the first box the screen fades
//   darker and the game pauses (BeginRun: a click-blocking black overlay on canvas 600, below the tip boxes, dimAmount 0 = 0.4 /
//   dimSeconds 0 = 0.7; time is restored and the overlay fades out at the end) and PixelShopTutorial.Active makes the Shop button (and
//   its key) ignore clicks until the tutorial is over. Steps are an Inspector list (steps, empty = the built-in ones, enableTutorial);
//   Cubie narrates it (PixelGuideVendor.BeginNarration/EndNarration: he slides in at the bottom right at 0.85 scale, stands still and
//   points at the shop window with a stick (pointer: arm + stick + gold tip turned towards the shop's on-screen centre every frame,
//   ShopPointTarget; his left arm hides meanwhile) and taps it on each new step (NarratorReact), no speech box, and leaves when the
//   tutorial ends); every box has one height (from the longest step, min boxHeight) so Got it never moves - the flow is the shared PixelTutorialRunner; the shop
//   window has a blue '?' hold button left of its X (the pause menu's PixelHoldButton with a fill bar, helpHoldSeconds 0.7 on
//   PixelShop) that calls Replay(); remembered as hint id shop_tutorial (PixelHints.IsTipSeen/SetTipSeen; Settings > Replay tutorial
//   clears it, so it replays after the intro if the shop is unlocked).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shop tutorial: the first time the Shop button is available, a series of small "Got it" boxes walks through the shop. Each box
/// sits just to the right of the shop window and the shop opens the tab / section it is describing (the currency list on the left,
/// Pixels, Upgrades > Features, Upgrades > Value, Consumables, Minigames). Shown once (remembered like the other tips), and again
/// with Settings > Replay tutorial. Added by <see cref="PixelShop"/>.
/// </summary>
public class PixelShopTutorial : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [TextArea(2, 5)] public string text;
        public PixelShop.ShopTab tab = PixelShop.ShopTab.Pixels;
        [Tooltip("Upgrades tab only: 0 = Features, 1 = Value, 2 = Operation.")] public int subTab;
    }

    private const string SeenId = "shop_tutorial";

    [Tooltip("Run the shop tutorial the first time the Shop button is available.")]
    [SerializeField] private bool enableTutorial = true;
    [Tooltip("Play the tutorial by itself the first time the shop is available (and when Cubie's shop hold ends). Off (default): ONLY the shop's blue ? button plays it.")]
    [SerializeField] private bool autoStart = false;

    [Min(0f)]
    [Tooltip("Seconds after the Shop becomes available (and nothing else is on screen) before the tutorial starts.")]
    [SerializeField] private float startDelay = 2f;

    [Tooltip("Every tutorial box is at least this tall (UI units), so the Got it button stays in the same place from step to step. Raise it if a long text makes a box grow.")]
    [SerializeField] private float boxHeight = 260f;

    [Tooltip("The steps, in order. Each shows its text in a small box beside the shop and opens its tab. Empty = the built-in steps.")]
    [SerializeField] private List<Step> steps = new List<Step>();
    [SerializeField, HideInInspector] private int stepsVersion;   // 1 = the 5-step tutorial (2026-10-24), 2 = reworded for the catalog layout: each replaced saved step lists once

    [Header("Dim and pause")]
    [Range(0f, 1f)]
    [Tooltip("How dark the screen gets before the tutorial starts (0 = 0.4). The game is paused meanwhile.")]
    [SerializeField] private float dimAmount = 0f;

    [Min(0f)]
    [Tooltip("Seconds the screen takes to fade darker before the first box (0 = 0.7) and to fade back at the end.")]
    [SerializeField] private float dimSeconds = 0f;

    private PixelShop shop;
    private PixelTutorialRunner runner;   // the shared tutorial flow (dim, Cubie, Got it boxes, done key)
    private bool running => runner != null && runner.Running;
    private float readyTimer;
    private bool requested;
    private static PixelShopTutorial instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Starts the tutorial at the next calm moment without the usual start delay (Cubie's hold ends when the player opens the shop).</summary>
    public static void RequestStart() { if (instance != null) instance.requested = true; }

    private bool wanted;   // its start conditions have held long enough: PixelTipQueue starts it at its turn

    private void Awake()
    {
        PixelTipQueue.Register(this, PixelTipQueue.Priority.Tutorial, "Shop tutorial", () => wanted && !running, () =>
        {
            wanted = false;
            if (steps == null || steps.Count == 0) steps = DefaultSteps();
            BeginRun();
        });
        instance = this;
        if (steps == null || steps.Count == 0 || stepsVersion < 2) { steps = DefaultSteps(); stepsVersion = 2; }   // (2026-10-24: 8 steps cut to 5; then reworded for the catalog layout)
        PixelDialogue.Steps("Tutorial: Shop", steps, st => st.text, (st, t) => st.text = t);   // (the user's texts: Assets/Resources/Dialogue.txt)
        PixelDialogue.File.Flush();
        shop = GetComponent<PixelShop>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
    }

    /// <summary>Stat counter (saved with the game) that says this save has seen the shop tutorial; Cubie's Replay clears it.</summary>
    public const string DoneKey = "guide.shopTutorial";

    private static List<Step> DefaultSteps()
    {
        return new List<Step>
        {
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Welcome to the Shop! Your pixels are listed along the top: prices are paid in them. Pick a section on the left; anything greyed out shows what it still needs." },
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Pixels: packs that unlock new pixel types. Every new type is its own currency. The card at the top shows what you are saving up for: pick any item and press Save up for this." },
            new Step { tab = PixelShop.ShopTab.Upgrades, subTab = 0, text = "Upgrades: Features are one-time unlocks like the Auto Clicker and Time Stop. Value makes a pixel type pay more; buy one level or x5, x10 or Max at once. Operation improves your consumables." },
            new Step { tab = PixelShop.ShopTab.Consumables, text = "Consumables: potions, devices, seeds and supplies. Choose how many with the amount buttons on the right, then use them from your Inventory." },
            new Step { tab = PixelShop.ShopTab.Minigames, text = "Minigames: events that start on their own once bought. They pay rewards and unlock rare pixels. Switch any of them off in the Toggles window." },
        };
    }

    private void BeginRun()
    {
        requested = false;
        if (runner == null)
            runner = new PixelTutorialRunner
            {
                doneKey = DoneKey,
                beside = () => shop != null ? shop.PanelRect : null,   // the boxes sit just right of the shop window
                minBoxHeight = boxHeight, dimAmount = dimAmount, dimSeconds = dimSeconds,
                onFinish = () => PixelHints.SetTipSeen(SeenId),
            };
        runner.minBoxHeight = boxHeight;
        // Each step opens the tab / section it describes; Cubie points at the shop window (his default, no override).
        List<PixelTutorialRunner.Step> list = new List<PixelTutorialRunner.Step>();
        foreach (Step st in steps)
        {
            Step step = st;
            list.Add(new PixelTutorialRunner.Step { text = step.text, enter = () => { if (shop != null) shop.TutorialOpen(step.tab, step.subTab); } });
        }
        runner.Run(list);
    }

    private void OnDestroy() { PixelTipQueue.Unregister(this); }

    private void Update()
    {
        wanted = false;
        if (running || !enableTutorial || shop == null || !shop.ShopAvailable) { readyTimer = 0f; return; }
        if (PixelStats.Total(DoneKey) >= 1d || !PixelHints.TipsEnabled) return;   // remembered per save (the tip flag in PlayerPrefs is global, so a new game never replayed it)
        if (!autoStart)
        {
            // Only the blue ? button plays it: count it as seen, so the Toggles / Skills tutorials that wait for it carry on.
            PixelStats.Best(DoneKey, 1d);
            requested = false;
            return;
        }

        // Wait for a calm moment: title screen over, the new-game intro finished, no other tip, no minigame takeover, not paused.
        if (!PixelCalm.ForTip)
        {
            readyTimer = 0f;
            return;
        }
        readyTimer += Time.unscaledDeltaTime;
        if (readyTimer < startDelay && !requested) return;
        wanted = true;   // (PixelTipQueue starts it when it is its turn)
    }

    /// <summary>Plays the tutorial again now (the shop's blue ? button), whether or not it was seen.</summary>
    public void Replay()
    {
        if (running || shop == null) return;
        if (steps == null || steps.Count == 0) steps = DefaultSteps();
        BeginRun();
    }
}
