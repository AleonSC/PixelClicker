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

    [Min(0f)]
    [Tooltip("Seconds after the Shop becomes available (and nothing else is on screen) before the tutorial starts.")]
    [SerializeField] private float startDelay = 2f;

    [Tooltip("Every tutorial box is at least this tall (UI units), so the Got it button stays in the same place from step to step. Raise it if a long text makes a box grow.")]
    [SerializeField] private float boxHeight = 260f;

    [Tooltip("The steps, in order. Each shows its text in a small box beside the shop and opens its tab. Empty = the built-in steps.")]
    [SerializeField] private List<Step> steps = new List<Step>();

    private PixelShop shop;
    private bool running;
    private float readyTimer;

    private void Awake()
    {
        shop = GetComponent<PixelShop>();
        if (shop == null) shop = PixelFind.First<PixelShop>();
    }

    private static List<Step> DefaultSteps()
    {
        return new List<Step>
        {
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Welcome to the Shop! The list on the left shows every pixel you hold - prices are paid in those pixels. The tabs along the top split up what is for sale." },
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Pixels: packs that unlock new pixel types. Every new type is its own currency, and some need goals from the minigames before you can buy them." },
            new Step { tab = PixelShop.ShopTab.Upgrades, subTab = 0, text = "Upgrades - Features: one-time unlocks such as the Auto Clicker, Crafting, Pixel Grabbing, Time Stop and the Pixel Bank, plus upgrades for them." },
            new Step { tab = PixelShop.ShopTab.Upgrades, subTab = 1, text = "Upgrades - Value: makes a pixel type pay more, paid in that same pixel. Each level costs a little more than the last. Shift-click buys as many levels as you can afford." },
            new Step { tab = PixelShop.ShopTab.Upgrades, subTab = 2, text = "Upgrades - Operation: improves your consumables. Each one has a spinning picture and an orange arrow that opens its upgrades: longer lasting, bigger reach, more uses, more storage." },
            new Step { tab = PixelShop.ShopTab.Consumables, text = "Consumables: things with a one-time use - potions, devices, seeds and supplies. Buy them here, then use them from the Consumables tab of your Inventory." },
            new Step { tab = PixelShop.ShopTab.Minigames, text = "Minigames: events that start on their own once you buy them, from ghosts and meteors to Snake and Breakout. They pay rewards and unlock rare pixels. Every minigame can be switched on or off in the Toggles window, so you can customize your gameplay experience." },
            new Step { tab = PixelShop.ShopTab.Pixels, text = "Anything greyed out shows what it still needs. Open the Shop again any time with the Shop button at the top right." },
        };
    }

    private void Update()
    {
        if (running || !enableTutorial || shop == null || !shop.ShopAvailable) { readyTimer = 0f; return; }
        if (PixelHints.IsTipSeen(SeenId) || !PixelHints.TipsEnabled) return;

        // Wait for a calm moment: title screen over, the new-game intro finished, no other tip, no minigame takeover, not paused.
        if (PixelTitleScreen.Showing || PixelHints.IntroHoldsGuide || PixelGuideVendor.Pending || PixelGuideVendor.Visiting || PixelNotice.IsShowing || PixelPauseMenu.IsPaused ||
            PixelMinigame.TakeoverActive || Time.timeScale <= 0f)
        {
            readyTimer = 0f;
            return;
        }
        readyTimer += Time.unscaledDeltaTime;
        if (readyTimer < startDelay) return;

        if (steps == null || steps.Count == 0) steps = DefaultSteps();
        running = true;
        ShowStep(0);
    }

    /// <summary>Plays the tutorial again now (the shop's blue ? button), whether or not it was seen.</summary>
    public void Replay()
    {
        if (running || shop == null) return;
        if (steps == null || steps.Count == 0) steps = DefaultSteps();
        PixelNotice.Hide();
        running = true;
        ShowStep(0);
    }

    /// <summary>One height for every box: tall enough for the longest step (a rough characters-per-line estimate), never under boxHeight, so Got it never moves.</summary>
    private float UniformHeight()
    {
        int longest = 0;
        foreach (Step st in steps) if (st != null && st.text != null) longest = Mathf.Max(longest, st.text.Length);
        int lines = Mathf.CeilToInt(longest / 28f) + 1;
        return Mathf.Max(boxHeight, 84f + lines * 30f);
    }

    private void ShowStep(int index)
    {
        if (index >= steps.Count || shop == null)
        {
            running = false;
            PixelGuideVendor.EndNarration();
            PixelHints.SetTipSeen(SeenId);
            return;
        }

        if (index > 0) PixelGuideVendor.NarratorReact();
        if (index == 0) PixelGuideVendor.BeginNarration();   // Cubie narrates his shop from the bottom right
        Step step = steps[index];
        shop.TutorialOpen(step.tab, step.subTab);
        PixelNotice.Show(step.text, 0f, small: true, beside: shop.PanelRect, onClosed: () => ShowStep(index + 1), minHeight: UniformHeight());
    }
}
