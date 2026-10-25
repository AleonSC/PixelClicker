// ---- System notes (moved here from CLAUDE.md) ----
// PixelShopTutorial.cs
//   Shop tour (added by PixelShop). Since 2026-10-25 it is OFFERED, not forced:
//   - After Cubie's second visit, until the shop is opened for the first time (stat OpenedKey 'guide.shopOpened', per save), the Shop
//     button gets the gold frame and Cubie peeks under it (PixelGuideVendor.ShopHold, PixelShopNudge). Time keeps running.
//   - The first time the shop window opens (tips on, no other tutorial dimming the screen) a box over the shop asks 'Want a quick tour?'
//     (PixelShop.AskBox -> the shared PixelUIKit.AskBox, the same box as the Woodcutting guide; texts offerTitle / offerQuestion).
//     YES = the tour; NO = one small box pointing at the blue '?' button (offerLaterText), framed gold until Got it. Either answer sets
//     DoneKey; the Toggles / Skills tutorials wait only while the offer is up or the tour plays (Busy). Tips off: nothing is asked, DoneKey is just set.
//   - The tour = small "Got it" boxes beside the shop (the shared PixelTutorialRunner: dim + pause, Cubie narrates and points at the window,
//     one box height from boxHeight), each step opening a rail entry via PixelShop.TutorialOpen(tab, subTab). 5 steps for the catalog
//     layout (stepsVersion 3): the category rail + currency in the Inventory, tiles + detail panel + Buy, Save up for this / the Saving up
//     for card, Only what I can buy + search, the sections + the '?' button. Texts: Inspector list steps (empty = built-ins), overridden by
//     Assets/Resources/Dialogue.txt [Tutorial: Shop] / [Offer: Shop tour].
//   - The shop's blue '?' hold button (PixelHoldButton, helpHoldSeconds on PixelShop) replays it any time (Replay); remembered as hint id
//     shop_tutorial (PixelHints.SetTipSeen; Settings > Replay tutorial clears it). enableTutorial off = never offered (DoneKey set).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shop tour: the first time the shop opens a box asks whether the player wants a quick tour (Yes = small "Got it" boxes beside the
/// shop, each opening the rail entry it describes; No = a pointer to the blue '?' button that replays it). Added by <see cref="PixelShop"/>.
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

    [Tooltip("Offer the shop tour the first time the shop is opened. Off = never offered (the '?' button still plays it).")]
    [SerializeField] private bool enableTutorial = true;

    [Header("Tour offer (first opening)")]
    [Tooltip("Title of the box that offers the tour the first time the shop opens.")]
    [SerializeField] private string offerTitle = "CUBIE'S SHOP";
    [Tooltip("The question in that box.")]
    [SerializeField] private string offerQuestion = "First time in my shop? Want a quick tour of how it works?";
    [Tooltip("Shown when the player says No: where to find the tour later.")]
    [SerializeField] private string offerLaterText = "No problem! Whenever you want the tour, hold the blue <b>?</b> button at the top of the shop.";

    [Tooltip("Every tutorial box is at least this tall (UI units), so the Got it button stays in the same place from step to step. Raise it if a long text makes a box grow.")]
    [SerializeField] private float boxHeight = 260f;

    [Tooltip("The steps, in order. Each shows its text in a small box beside the shop and opens its tab. Empty = the built-in steps.")]
    [SerializeField] private List<Step> steps = new List<Step>();
    [SerializeField, HideInInspector] private int stepsVersion;   // 1 = the 5-step tutorial (2026-10-24), 2 = reworded for the catalog layout, 3 = rewritten as the offered tour (2026-10-25)

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
    private GameObject offerBox;          // the 'Want a quick tour?' box while it is up
    private static PixelShopTutorial instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>The tour offer is up or the tour is playing: the Toggles / Skills tutorials wait.</summary>
    public static bool Busy => instance != null && (instance.offerBox != null || instance.running);

    private void Awake()
    {
        if (steps == null || steps.Count == 0 || stepsVersion < 3) { steps = DefaultSteps(); stepsVersion = 3; }   // (2026-10-25: rewritten for the catalog layout, offered on the first opening)
        PixelDialogue.Steps("Tutorial: Shop", steps, st => st.text, (st, t) => st.text = t);   // (the user's texts: Assets/Resources/Dialogue.txt)
        PixelDialogue.File.Apply("Offer: Shop tour", "title", ref offerTitle);
        PixelDialogue.File.Apply("Offer: Shop tour", "question", ref offerQuestion);
        PixelDialogue.File.Apply("Offer: Shop tour", "later", ref offerLaterText);
        PixelDialogue.File.Flush();
        shop = GetComponent<PixelShop>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
        instance = this;
    }

    /// <summary>Stat counter (saved with the game) that says this save has seen the shop tutorial; Cubie's Replay clears it.</summary>
    public const string DoneKey = "guide.shopTutorial";

    /// <summary>Stat counter (per save): the shop window has been opened at least once. Until then the Shop button is nudged (PixelShopNudge, Cubie's peek).</summary>
    public const string OpenedKey = "guide.shopOpened";

    private static List<Step> DefaultSteps()
    {
        return new List<Step>
        {
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Welcome to my Shop! Pick a section from the list on the left. Your currency shows in the Inventory beside the shop: prices are paid in pixels." },
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Every tile is something to buy. Click one and its price and details show on the right. If you can't afford it yet, the Buy button tells you what you're short of." },
            new Step { tab = PixelShop.ShopTab.Upgrades, subTab = 1, text = "Saving up? Pick the item and press <b>Save up for this</b>. It goes on the Saving up for card at the top, with a bar that fills as you earn. Click the card to jump back to it. Things bought in numbers have x1, x5, x10 and Max." },
            new Step { tab = PixelShop.ShopTab.Consumables, text = "Too much to look at? Switch on <b>Only what I can buy</b> to hide the rest, or type in the search box at the top to find anything by name." },
            new Step { tab = PixelShop.ShopTab.Minigames, text = "Pixels unlock new pixel types, Consumables are used from your Inventory, Upgrades make things better, and Minigames start on their own once bought. Hold the blue <b>?</b> any time to see this tour again." },
        };
    }

    private void BeginRun()
    {
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

    private void OnDestroy() { PixelHighlight.Clear(this); if (instance == this) instance = null; }

    private void Update()
    {
        if (shop == null || !shop.ShopAvailable || PixelStats.Total(OpenedKey) >= 1d) return;
        if (!shop.WindowOpen || running || offerBox != null || PixelTutorialDim.Active) return;   // (another tutorial dimming the screen: ask at a later opening)
        PixelStats.Best(OpenedKey, 1d);
        if (PixelStats.Total(DoneKey) >= 1d) return;
        if (!enableTutorial || !PixelHints.TipsEnabled) { PixelStats.Best(DoneKey, 1d); return; }
        offerBox = shop.AskBox(offerTitle, offerQuestion, "Yes, show me", "No thanks",
                               () => { offerBox = null; PixelStats.Best(DoneKey, 1d); Replay(); },
                               () => { offerBox = null; PixelStats.Best(DoneKey, 1d); PointAtHelp(); });
    }

    /// <summary>'No': one box pointing at the blue '?' button, which glows gold until Got it.</summary>
    private void PointAtHelp()
    {
        RectTransform help = shop != null ? shop.HelpButtonRect : null;
        if (help != null) PixelHighlight.Set(this, help);
        PixelNotice.Show(offerLaterText, 0f, small: true, beside: shop != null ? shop.PanelRect : null, onClosed: () => PixelHighlight.Clear(this));
    }

    /// <summary>Plays the tour now (Yes in the first-time box, or the shop's blue ? button), whether or not it was seen.</summary>
    public void Replay()
    {
        if (running || shop == null) return;
        if (offerBox != null) { Destroy(offerBox); offerBox = null; PixelStats.Best(DoneKey, 1d); }
        if (steps == null || steps.Count == 0) steps = DefaultSteps();
        PixelNotice.Hide();
        BeginRun();
    }
}
