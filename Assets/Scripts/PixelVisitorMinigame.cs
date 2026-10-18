using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class of the "visitor" minigames (Tinkerer, Wizard). Every few minutes a little code-drawn visitor slides in from the right side of the screen, time stops and they greet you with an offer: every
/// consumable DEVICE (vacuum device, fan, sorter, combo fuel, ghost bait, pet treat...) at a big discount. Accept to see the wares
/// and buy what you like, or reject and they slide away again. Time resumes when they leave (Escape counts as rejecting).
/// Extends <see cref="PixelMinigame"/>, so it needs no changes to the shop or the save system.
/// The greeting, wares and aside boxes are the standard window as a dialog (PixelUIKit.CreateWindow, no X), set at the right edge.
/// </summary>
public abstract class PixelVisitorMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker whose pixels pay for the wares. Found automatically if left empty.")]
    [SerializeField] protected PixelClicker clicker;

    [Tooltip("The consumables that supply the wares (every device). Found automatically if left empty.")]
    [SerializeField] protected PixelConsumables consumables;

    [Header("State")]
    [Tooltip("Start running without buying the pack (testing).")]
    [SerializeField] private bool startRunning = false;

    [Tooltip("Is the minigame running? (The shop turns this on when the Tinkerer pack is bought.)")]
    [SerializeField] private bool running = false;

    [Header("Timing")]
    [Min(0f)]
    [Tooltip("Seconds before the first visit after the minigame starts.")]
    [SerializeField] private float firstVisitDelay = 150f;

    [Min(1f)]
    [Tooltip("Fewest seconds between this vendor's visits (8 minutes).")]
    [SerializeField] private float visitMinSeconds = 480f;

    [Min(1f)]
    [Tooltip("Most seconds between this vendor's visits (15 minutes).")]
    [SerializeField] private float visitMaxSeconds = 900f;

    [Min(0f)]
    [Tooltip("After ANY vendor leaves, no vendor (this one included) comes for this many seconds, so visits never come back to back.")]
    [SerializeField] private float sharedCooldownSeconds = 180f;

    [Range(0f, 1f)]
    [Tooltip("How dark the screen gets while a vendor is here (time is frozen too).")]
    [SerializeField] private float visitDarkness = 0.6f;

    [Header("Peek (they look in first; the game keeps running)")]
    [Tooltip("Visitors first PEEK in from the right edge with a small bubble: click them to open the full visit (dark screen, time stops), or press X / ignore them and they leave. Off = the full visit at once.")]
    [SerializeField] private bool usePeek = true;
    [Min(1f)]
    [Tooltip("Seconds a peeking visitor waits for a click before leaving (paused time doesn't count).")]
    [SerializeField] private float peekSeconds = 20f;
    [Range(0.1f, 1f)]
    [Tooltip("A visitor you dismissed or ignored comes back after this share of the normal wait (0.5 = half as long).")]
    [SerializeField] private float missedReturnFactor = 0.5f;
    [Tooltip("The peek bubble's text. {0} = the visitor's name.")]
    [SerializeField] private string peekText = "Psst! {0} is here. Click me to see what I've got!";
    [Tooltip("Background colour of the peek bubble (the text is white).")]
    [SerializeField] private Color peekBubbleColor = new Color(0.08f, 0.09f, 0.13f, 0.96f);
    [Tooltip("Event log line when a peeking visitor leaves unanswered. {0} = the visitor's name.")]
    [SerializeField] private string missedLog = "{0} stopped by and left.";

    [Header("Offer")]
    [Range(0f, 0.99f)]
    [Tooltip("How much cheaper the devices are than in the shop (0.8 = 80% off).")]
    [SerializeField] private float discountFraction = 0.8f;

    [Header("Visitor")]
    [Min(0.1f)]
    [Tooltip("Seconds the slide in (and out) takes.")]
    [SerializeField] private float slideSeconds = 1.4f;

    [Min(100f)]
    [Tooltip("Height of the visitor (canvas units).")]
    [SerializeField] private float visitorHeight = 420f;

    [Header("Texts")]
    [Tooltip("Name shown at the top of the speech box. Empty = this visitor's own name.")]
    [SerializeField] private string visitorName = "";

    [TextArea(2, 5)]
    [Tooltip("What the visitor says on arrival. {0} = the discount in percent. Empty = this visitor's own greeting.")]
    [SerializeField] private string greeting = "";

    [Tooltip("Label of the button that accepts the offer.")]
    [SerializeField] private string acceptText = "Show me!";

    [Tooltip("Label of the button that rejects the offer.")]
    [SerializeField] private string rejectText = "No thanks";

    [Tooltip("Title of the wares window. Empty = this visitor's own title.")]
    [SerializeField] private string waresTitle = "";

    [Tooltip("Label of the button that closes the wares window.")]
    [SerializeField] private string doneText = "Done";

    [Tooltip("Label of a buy button.")]
    [SerializeField] private string buyText = "Buy";

    [Tooltip("Shown instead of the buy button when you can hold no more of that item.")]
    [SerializeField] private string fullText = "Full";

    [Tooltip("Event-log line when the visitor arrives. Empty = the default.")]
    [SerializeField] private string arrivedLog = "";

    // ------------------------------------------------------------------

    // --- What a concrete visitor provides ---
    /// <summary>Does this visitor sell this item (an index into PixelConsumables' items)?</summary>
    protected abstract bool IsWare(int item);
    /// <summary>Draws the visitor from blocks inside 'art' (a 300 x 400 design space, origin bottom centre; see <see cref="Block"/>).</summary>
    protected abstract void DrawVisitor(Transform art);
    protected abstract string DefaultName { get; }
    protected abstract string DefaultGreeting { get; }   // {0} = the discount in percent
    protected abstract string DefaultWaresTitle { get; }
    protected virtual string SaleFormat => "{0}% off";   // {0} = the discount in percent
    protected virtual string DefaultArrivedLog => DefaultName + " is here!";
    /// <summary>Extra seconds before the first visit (so two visitors don't arrive together).</summary>
    protected virtual float StartDelayOffset => 0f;

    // --- Rare, one-off visitors (Egg, Bean): they give a gift instead of selling, and never come back ---
    /// <summary>True for a visitor who gives something away instead of selling: accepting runs <see cref="OnGift"/> (after the optional second speech box).</summary>
    protected virtual bool IsGift => false;
    /// <summary>Called when the player accepts the gift (the visitor is still on screen).</summary>
    protected virtual void OnGift() { }
    /// <summary>True for a visitor who remembers having met you: the first real visit is recorded in the counter '&lt;id&gt;.met' (<see cref="FirstVisitNow"/> tells which visit this is).</summary>
    protected virtual bool TracksMeeting => false;
    /// <summary>True during the visitor's first visit ever (only meaningful for visitors that track meeting you).</summary>
    protected bool FirstVisitNow { get; private set; }

    /// <summary>
    /// A cat's "gift": a few random unlocked pixel types, each worth 'minClicks'..'maxClicks' normal clicks of that pixel (so it grows with your Value upgrades).
    /// Pays at once, announces it in the event log and returns the text.
    /// </summary>
    protected string GivePixelGift(string giverName, int types, int minClicks, int maxClicks)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < clicker.Tiers.Length; i++)
        {
            PixelClicker.PixelTier t = clicker.Tiers[i];
            if (t.unlocked && !t.rareDrop && !t.flyAway && !PixelClicker.IsStandalone(t.type)) candidates.Add(i);
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int n = 0; n < types && candidates.Count > 0; n++)
        {
            int pick = Random.Range(0, candidates.Count);
            int tier = candidates[pick];
            candidates.RemoveAt(pick);
            double amount = System.Math.Max(1d, System.Math.Floor(clicker.RollOldPixelAmount(tier) * Random.Range(Mathf.Max(1, minClicks), Mathf.Max(minClicks, maxClicks) + 1)));
            clicker.AddCurrency(tier, amount);
            if (sb.Length > 0) sb.Append(", ");
            sb.Append('+').Append(PixelClicker.FormatNumberShort(amount)).Append(' ').Append(clicker.Tiers[tier].displayName);
        }
        PixelAudio.Play("purchase");
        PixelHints.Announce(giverName + " brought you a gift!", giverName + " brought you: " + sb);
        return sb.ToString();
    }
    /// <summary>Chance (0-1) that a visit actually happens each time the visit timer runs out (a failed roll just waits for the next timer).</summary>
    protected virtual float VisitChance => 1f;
    /// <summary>Can the visit happen now? (Checked together with the chance roll; e.g. the Egg needs a pet you don't have.)</summary>
    /// <summary>A vendor that only visits while one skill is in training (its id; null = any time). It also writes the vendor's Toggles note.</summary>
    protected virtual string VisitSkillId => null;
    protected virtual bool CanVisit => VisitSkillId == null || PixelSkills.IsTraining(VisitSkillId);

    /// <summary>The orange '!' note on the vendor's Toggles row: when it visits (from VisitSkillId).</summary>
    public override string ToggleNote
    {
        get
        {
            if (VisitSkillId == null) return null;
            PixelSkills.SkillPack pack = PixelSkills.Instance != null ? PixelSkills.Instance.Find(VisitSkillId) : null;
            return Pick(visitorName, DefaultName) + " only visits while you are training " + (pack != null ? pack.displayName : VisitSkillId) + ".";
        }
    }
    protected virtual string AcceptLabel => acceptText;
    protected virtual string RejectLabel => rejectText;

    /// <summary>Has this one-off visitor already come?</summary>
    public bool HasMet => PixelStats.Total(Id + ".met") >= 1d;
    protected virtual Color TitleColor => new Color(1f, 0.85f, 0.4f);

    private static string Pick(string set, string fallback) => string.IsNullOrEmpty(set) ? fallback : set;
    public override bool Running => running;
    public override bool Busy => visiting;

    private bool visiting;
    private float spawnTimer;
    private Image dim;

    // Shared by every vendor: nobody comes while another is here or soon after one left.
    private static float nextAnyVisitTime;
    private static bool anyVisiting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVisitStatics()
    {
        nextAnyVisitTime = 0f; anyVisiting = false;
        // (Restart reloads the scene: a visit cut short left anyVisiting on and no vendor ever came again in the new game)
        PixelStatics.ResetOnRestart(OnVisitSceneUnloaded);
    }

    private static void OnVisitSceneUnloaded() { nextAnyVisitTime = 0f; anyVisiting = false; }

    /// <summary>Is it a calm moment for a vendor to walk in? Never over a tip box (or tips waiting), the dragon show / wish, a pet popup, a pause or the title screen.</summary>
    private static bool CalmForVisit()
    {
        if (Time.timeScale <= 0f) return false;                       // paused or Time Stop
        if (PixelNotice.IsShowing || PixelHints.TipsPending) return false;
        if (PixelDragonFind.Playing || PixelDragonWish.Active || PixelPets.PopupOpen) return false;
        if (PixelCalm.InMenu) return false;
        return true;
    }
    private float savedTimeScale = 1f;
    private bool froze;

    private GameObject canvasRoot;
    private RectTransform visitorRect, greetingPanel, waresPanel;
    private RectTransform waresContent;
    private GameObject waresBar;
    private ScrollRect waresScroll;
    private readonly List<WareRow> rows = new List<WareRow>();
    private TMP_Text greetMessage, asideMessage, typedLabel;
    private bool asideBack;
    private float typeShown;
    private int typeTotal;
    private const float TypeCharsPerSecond = 70f;

    private bool Typing => typedLabel != null && typeShown < typeTotal;

    private void StartType(TMP_Text label)
    {
        typedLabel = label;
        if (label == null) return;
        label.ForceMeshUpdate();
        typeTotal = label.textInfo.characterCount;
        typeShown = 0f;
        label.maxVisibleCharacters = 0;
    }

    private void TickType()
    {
        if (!Typing) return;
        typeShown += Time.unscaledDeltaTime * TypeCharsPerSecond;
        if (typeShown >= typeTotal) FinishType();
        else typedLabel.maxVisibleCharacters = (int)typeShown;
    }

    private void FinishType()
    {
        typeShown = typeTotal;
        if (typedLabel != null) typedLabel.maxVisibleCharacters = int.MaxValue;
    }

    private int decision; // 0 = waiting, 1 = accept, 2 = reject / leave
    private bool waresDone;
    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    /// <summary>One pixel-for-pixel deal (a visitor that trades instead of selling items): you pay 'pay' of tier 'payTier' and get 'get' of tier 'getTier'.</summary>
    protected class Trade
    {
        public int payTier, getTier;
        public double pay, get;
        public bool done;
    }

    /// <summary>Visitors that trade pixels return their offers here when they arrive (null = a normal wares visitor).</summary>
    protected virtual List<Trade> RollTrades() => null;

    /// <summary>True for a vendor that both SELLS wares and BUYS pixels: the window gets a Buy tab (the wares) and a Sell tab (the trades from <see cref="RollTrades"/>).</summary>
    protected virtual bool BuySellTabs => false;

    /// <summary>A vendor with its OWN Sell tab (BuySellTabs): BuildSellPanel fills a container in place of the trade rows (e.g. the Lumberjack's 'choose an amount, get an offer' panel).</summary>
    protected virtual bool CustomSellPanel => false;
    /// <summary>Height the custom Sell panel needs (the window grows to fit it).</summary>
    protected virtual float SellPanelHeight => 0f;
    /// <summary>Builds the custom Sell tab into 'parent' (stretched over the list area). Called once per visit when the window is built.</summary>
    protected virtual void BuildSellPanel(RectTransform parent) { }
    /// <summary>The custom Sell tab became visible (refresh what you hold, etc.).</summary>
    protected virtual void OnSellPanelShown() { }
    /// <summary>The shared UI font, for a subclass that builds its own UI.</summary>
    protected TMP_FontAsset VendorFont => Font;
    private RectTransform sellPanelRoot;

    /// <summary>Label of a trade's button.</summary>
    protected virtual string TradeText => "Trade";

    /// <summary>The word before what a trade costs you in its row ("Pay 20 Copper"); a buyer says "Sell".</summary>
    protected virtual string TradePayWord => "Pay";

    private List<Trade> trades;

    /// <summary>How many different wares this visitor brings per visit (0 = everything it can sell).</summary>
    protected virtual int OfferedWares => 0;
    /// <summary>Fewest of each ware in stock (with <see cref="StockMax"/> 0 = unlimited stock).</summary>
    protected virtual int StockMin => 0;
    /// <summary>Most of each ware in stock (0 = unlimited).</summary>
    protected virtual int StockMax => 0;
    /// <summary>How many of this ware he has this visit (default: random between <see cref="StockMin"/> and <see cref="StockMax"/>).</summary>
    /// <summary>Lets a visitor change this visit's wares after the random selection (e.g. the Farmer's rare Dragon Seed). Called before the stock is rolled.</summary>
    protected virtual void ModifyWares(List<int> wares) { }

    /// <summary>Set by a subclass (in <see cref="ModifyWares"/>) to give the visitor a second speech box that shows after you accept, before the wares window.</summary>
    protected string AsideText;
    private RectTransform asidePanel;
    private bool asideDone;
    /// <summary>A ware shown glowing yellow in the list (something important).</summary>
    protected virtual bool Highlighted(int item) => false;
    /// <summary>A visitor's OWN price for a ware this visit (rolled once when it arrives, e.g. the Lumberjack's potions cost their material + a random pure pixel). Null = the item's normal costs. The visit discount still applies.</summary>
    protected virtual PixelShop.PackCost[] RollVisitPrice(int item) => null;
    private readonly Dictionary<int, PixelShop.PackCost[]> visitPrice = new Dictionary<int, PixelShop.PackCost[]>();
    protected virtual int StockFor(int item) => Random.Range(Mathf.Max(1, StockMin), Mathf.Max(StockMin, StockMax) + 1);

    private readonly Dictionary<int, int> stock = new Dictionary<int, int>(); // ware -> how many are left this visit (only when the stock is limited)

    private class WareRow
    {
        public int tab;   // 0 = Buy tab (a ware), 1 = Sell tab (a trade); only used with BuySellTabs
        public Trade trade;
        public int item;
        public RectTransform rect;
        public TMP_Text name, cost, owned, stockLabel, buyLabel;
        public Button buy;
        public Image buyImage;
    }

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (startRunning) running = true;
        spawnTimer = firstVisitDelay + StartDelayOffset;
    }

    protected override void OnDestroy()
    {
        Unfreeze();
        base.OnDestroy();
    }

    private bool rolled;

    private void Update()
    {
        if (!running || visiting || clicker == null) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        if (!rolled)
        {
            // A rare visitor: one roll per timer, and only when the visit would make sense.
            rolled = true;
            if (!CanVisit || Random.value >= VisitChance)
            {
                rolled = false;
                spawnTimer = Random.Range(visitMinSeconds, visitMaxSeconds);
                return;
            }
        }
        if (anyVisiting || Time.unscaledTime < nextAnyVisitTime || !CalmForVisit()) return;   // waits (checked every frame) for a quiet moment
        if (PixelMinigameLimits.AllowAnother(this)) { rolled = false; StartCoroutine(VisitRoutine(false)); }
        else spawnTimer = PixelMinigameLimits.RetrySeconds;
    }

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstVisitDelay + StartDelayOffset;
    }

    public override void Deactivate() => running = false;

    /// <summary>Sets the seconds until the next visit (the guide uses it to bring a newly started vendor soon).</summary>
    public void ScheduleFirstVisit(float seconds) => spawnTimer = Mathf.Max(1f, seconds);

    [ContextMenu("Spawn Tinkerer Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !visiting && clicker != null) StartCoroutine(VisitRoutine(true));
    }

    protected override void OnDespawned()
    {
        visiting = false;
        anyVisiting = false;
        nextAnyVisitTime = Time.unscaledTime + sharedCooldownSeconds;
        Unfreeze();
        PixelWindows.Unregister(this);
        spawnTimer = Random.Range(visitMinSeconds, visitMaxSeconds);
    }

    // ------------------------------------------------------------------
    // The visit
    // ------------------------------------------------------------------

    private IEnumerator VisitRoutine(bool dev)
    {
        FirstVisitNow = !HasMet;
        if (TracksMeeting && !dev) PixelStats.Best(Id + ".met", 1d);   // remembered, whatever you decide
        visiting = true;
        anyVisiting = true;
        decision = 0;
        asideDone = false;
        AsideText = null;
        waresDone = false;
        Report(MinigameEvent.Spawned);
        PixelHints.Announce(Pick(arrivedLog, DefaultArrivedLog));
        PixelAudio.Play(Id + "_spawn");
        BuildUI();

        // The screen darkens and time stops for the whole visit (Escape = send them away).
        dim = new GameObject("Dim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        dim.transform.SetParent(canvasRoot.transform, false);
        dim.color = new Color(0f, 0f, 0f, 0f);
        dim.raycastTarget = false;
        PixelUIKit.Stretch(dim.rectTransform);
        dim.transform.SetAsFirstSibling();

        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float visitorW = visitorRect.sizeDelta.x;
        float restX = -(visitorW * 0.5f + 50f);
        float hiddenX = visitorW * 0.5f + 40f;
        visitorRect.anchoredPosition = new Vector2(hiddenX, barH + 24f);
        float startX = hiddenX;

        if (usePeek)
        {
            // THE PEEK: they lean in from the right edge with a small bubble while the game keeps running. Click = the full visit; X / ignored = they leave.
            int choice = 0;   // 1 = come in, 2 = go away
            GameObject bubble = BuildPeek(() => { if (choice == 0) choice = 1; }, () => { if (choice == 0) choice = 2; });
            float peekX = visitorW * 0.08f;   // about 40% of them shows
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                visitorRect.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, peekX, k), barH + 24f);
                visitorRect.localRotation = Quaternion.Euler(0f, 0f, 9f * k);   // leaning in
                yield return null;
            }
            if (bubble != null) bubble.SetActive(true);
            float waited = 0f;
            while (choice == 0 && waited < peekSeconds)
            {
                if (!PixelCalm.InMenu) waited += Time.unscaledDeltaTime;
                visitorRect.anchoredPosition = new Vector2(peekX, barH + 24f + Mathf.Sin(Time.unscaledTime * 3f) * 4f);
                yield return null;
            }
            if (bubble != null) Destroy(bubble);
            if (peekButton != null) Destroy(peekButton);   // (the click catcher only belongs to the peek)
            if (peekCatcher != null) Destroy(peekCatcher);
            peekButton = null; peekCatcher = null;
            if (choice != 1)
            {
                // Left unanswered: they duck out, and come back sooner than usual.
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                    visitorRect.anchoredPosition = new Vector2(Mathf.Lerp(peekX, hiddenX, k), barH + 24f);
                    yield return null;
                }
                PixelHints.Announce(string.Format(missedLog, Pick(visitorName, DefaultName)));
                if (canvasRoot != null) Destroy(canvasRoot);
                canvasRoot = null;
                dim = null;
                visiting = false;
                anyVisiting = false;
                nextAnyVisitTime = Time.unscaledTime + sharedCooldownSeconds * missedReturnFactor;
                spawnTimer = Random.Range(visitMinSeconds, visitMaxSeconds) * missedReturnFactor;
                yield break;
            }
            visitorRect.localRotation = Quaternion.identity;
            startX = peekX;
        }

        Freeze();   // (only now: during the peek the game kept running)
        PixelWindows.Register(this, 150, () => visiting, Leave);

        // Slide in from the right (the game keeps running while they walk in).
        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            visitorRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, restX, k), barH + 24f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
            SetDim(k);
            yield return null;
        }
        visitorRect.anchoredPosition = new Vector2(restX, barH + 24f);
        SetDim(1f);

        // They greet you (the text types itself; the first press of a button shows the rest, the next one answers / goes on).
        bool again;
        do
        {
            again = false;
            greetingPanel.gameObject.SetActive(true);
            PixelPop.Show(greetingPanel.gameObject);
            StartType(greetMessage);
            while (decision == 0) { TickType(); Bob(restX, barH); yield return null; }
            PixelPop.Hide(greetingPanel.gameObject);

            if (decision == 1 && asidePanel != null)
            {
                asidePanel.gameObject.SetActive(true);
                PixelPop.Show(asidePanel.gameObject);
                asideDone = false;
                asideBack = false;
                StartType(asideMessage);
                while (!asideDone && decision == 1 && !asideBack) { TickType(); Bob(restX, barH); yield return null; }
                PixelPop.Hide(asidePanel.gameObject);
                if (asideBack) { decision = 0; again = true; }   // back to the greeting
                else if (decision == 0) decision = 2;
            }
        } while (again);

        if (decision == 1 && IsGift) OnGift();
        if (decision == 1 && !IsGift)
        {
            RefreshWares();
            waresPanel.gameObject.SetActive(true);
            while (!waresDone) { Bob(restX, barH); yield return null; }
            PixelPop.Hide(waresPanel.gameObject);
        }

        // Time resumes and they leave.
        PixelWindows.Unregister(this);
        Unfreeze();
        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            visitorRect.anchoredPosition = new Vector2(Mathf.Lerp(restX, hiddenX, k), barH + 24f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
            SetDim(1f - k);
            yield return null;
        }

        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null;
        dim = null;
        visiting = false;
        anyVisiting = false;
        nextAnyVisitTime = Time.unscaledTime + sharedCooldownSeconds;
        spawnTimer = Random.Range(visitMinSeconds, visitMaxSeconds);
    }

    private Image peekCatcher;
    private Button peekButton;

    /// <summary>The peek's pieces: a click catcher over the visitor and a small speech bubble with an X (shown once they have leaned in).</summary>
    private GameObject BuildPeek(System.Action accept, System.Action dismiss)
    {
        // Clicking the visitor (any part of the art) brings them in.
        Image catcher = visitorRect.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        Button inButton = PixelUIKit.MakeClickable(visitorRect.gameObject, () => accept(), pressTint: false);
        peekCatcher = catcher;
        peekButton = inButton;

        // The bubble, left of the peeking visitor.
        GameObject b = new GameObject("Peek Bubble", typeof(RectTransform), typeof(Image));
        b.transform.SetParent(canvasRoot.transform, false);
        Image bg = b.GetComponent<Image>();
        bg.color = peekBubbleColor;   // (a dark box: the Image's default white made the white text unreadable)
        PixelUIKit.StyleBox(bg);
        PixelUIKit.MakeClickable(b, () => accept());
        RectTransform br = b.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(1f, 0f);
        br.sizeDelta = new Vector2(420f, 120f);
        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        br.anchoredPosition = new Vector2(-visitorRect.sizeDelta.x * 0.42f - 16f, barH + 24f + visitorRect.sizeDelta.y * 0.62f);
        TMP_Text t = PixelUIKit.CreateText(Font, b.transform, "Text", string.Format(peekText, Pick(visitorName, DefaultName)), 26f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        t.enableAutoSizing = true; t.fontSizeMax = 26f; t.fontSizeMin = 16f;
        t.raycastTarget = false;
        PixelUIKit.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(16f, 10f);
        t.rectTransform.offsetMax = new Vector2(-46f, -10f);
        Button x = PixelUIKit.CreateButton(Font, b.transform, "Dismiss", "X", new Vector2(36f, 36f), ButtonRole.Neutral, 22f);
        RectTransform xr = x.GetComponent<RectTransform>();
        xr.anchorMin = xr.anchorMax = xr.pivot = new Vector2(1f, 1f);
        xr.anchoredPosition = new Vector2(-6f, -6f);
        x.onClick.AddListener(() => dismiss());
        b.SetActive(false);
        return b;
    }

    private void SetDim(float amount)
    {
        if (dim != null) dim.color = new Color(0f, 0f, 0f, visitDarkness * Mathf.Clamp01(amount));
    }

    private void Bob(float restX, float barH)
    {
        visitorRect.anchoredPosition = new Vector2(restX, barH + 24f + Mathf.Sin(Time.unscaledTime * 3f) * 4f);
    }

    private void Leave()
    {
        if (decision == 0) decision = 2;
        else waresDone = true;
    }

    private void Freeze()
    {
        if (froze || Time.timeScale <= 0f) return; // already stopped (Time Stop / pause): leave it alone
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        froze = true;
    }

    private void Unfreeze()
    {
        if (!froze) return;
        froze = false;
        if (Time.timeScale <= 0f) Time.timeScale = savedTimeScale;
    }

    // ------------------------------------------------------------------
    // Shopping
    // ------------------------------------------------------------------

    private List<int> Wares()
    {
        List<int> list = new List<int>();
        if (consumables == null) return list;
        for (int i = 0; i < consumables.ItemCount; i++)
            if (!consumables.ItemCraftOnly(i) && !consumables.ItemNotSold(i) && IsWare(i)) list.Add(i);
        return list;
    }

    /// <summary>The names of the wares this visitor could sell right now (the Game Database lists them).</summary>
    public List<string> WareNames()
    {
        List<string> names = new List<string>();
        if (consumables == null) return names;
        foreach (int i in Wares()) names.Add(consumables.ItemName(i));
        return names;
    }

    private PixelShop.PackCost[] PriceOf(int item)
    {
        PixelShop.PackCost[] baseCosts = visitPrice.TryGetValue(item, out PixelShop.PackCost[] own) ? own : consumables.ItemCosts(item);
        if (baseCosts == null) return new PixelShop.PackCost[0];
        List<PixelShop.PackCost> list = new List<PixelShop.PackCost>();
        foreach (PixelShop.PackCost c in baseCosts)
        {
            if (c == null) continue;
            list.Add(new PixelShop.PackCost
            {
                type = c.type,
                minigameCurrency = c.minigameCurrency,
                amount = System.Math.Max(1d, System.Math.Ceiling(c.amount * (1f - discountFraction))),
            });
        }
        return list.ToArray();
    }

    private bool CanAfford(PixelShop.PackCost c)
    {
        if (string.IsNullOrEmpty(c.minigameCurrency)) return clicker.CanAfford(c.type, c.amount);
        if (PixelClicker.InfiniteResources) return true;
        PixelMinigame m = Find(c.minigameCurrency);
        return m != null && m.HasTracker && m.SpendableCount >= c.amount;
    }

    private void Spend(PixelShop.PackCost c)
    {
        if (string.IsNullOrEmpty(c.minigameCurrency)) { clicker.TrySpend(c.type, c.amount); return; }
        if (PixelClicker.InfiniteResources) return;
        PixelMinigame m = Find(c.minigameCurrency);
        if (m != null) m.TrySpendTracker(c.amount);
    }

    private string CostName(PixelShop.PackCost c)
    {
        if (!string.IsNullOrEmpty(c.minigameCurrency))
        {
            PixelMinigame m = Find(c.minigameCurrency);
            return m != null && !string.IsNullOrEmpty(m.TrackerTitle) ? m.TrackerTitle : c.minigameCurrency;
        }
        int t = clicker.IndexOf(c.type);
        return t >= 0 ? clicker.Tiers[t].displayName : c.type.ToString();
    }

    private void Buy(int item)
    {
        if (consumables.ItemRoom(item) < 1) return;
        if (stock.TryGetValue(item, out int left) && left < 1) return;
        PixelShop.PackCost[] price = PriceOf(item);
        foreach (PixelShop.PackCost c in price) if (!CanAfford(c)) return;
        foreach (PixelShop.PackCost c in price) Spend(c);
        consumables.AddItem(item, 1);
        if (stock.ContainsKey(item)) stock[item]--;
        PixelStats.Count("shop.items");
        PixelStats.Count(Id + ".bought");
        PixelAudio.Play("purchase");
        RefreshWares();
    }

    private void DoTrade(Trade t)
    {
        if (t == null || t.done || !clicker.CanAfford(clicker.Tiers[t.payTier].type, t.pay)) return;
        if (!clicker.TrySpend(t.payTier, t.pay)) return;
        clicker.AddCurrency(t.getTier, t.get);
        t.done = true;
        PixelStats.Count(Id + ".bought");
        PixelAudio.Play("purchase");
        RefreshWares();
    }

    private void RefreshTradeRow(WareRow row)
    {
        Trade t = row.trade;
        string payName = clicker.Tiers[t.payTier].displayName;
        bool afford = clicker.CanAfford(clicker.Tiers[t.payTier].type, t.pay);
        Color col = afford ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.45f, 0.45f);
        PixelUIKit.SetText(row.cost, TradePayWord + " <color=#" + ColorUtility.ToHtmlStringRGB(col) + ">" + PixelClicker.FormatNumberShort(t.pay) + " " + payName + "</color>");
        PixelUIKit.SetText(row.owned, "You own " + PixelClicker.FormatNumberShort(clicker.GetCount(clicker.Tiers[t.payTier].type)));
        PixelUIKit.SetText(row.stockLabel, "");
        row.buy.interactable = !t.done && afford;
        PixelUIKit.SetText(row.buyLabel, t.done ? "Sold" : TradeText);
        PixelUIKit.SetRole(row.buyImage, t.done || !afford ? ButtonRole.Neutral : ButtonRole.Success);
    }

    private void RefreshWares()
    {
        foreach (WareRow row in rows)
        {
            if (row.trade != null) { RefreshTradeRow(row); continue; }
            int item = row.item;
            PixelShop.PackCost[] price = PriceOf(item);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (PixelShop.PackCost c in price)
            {
                Color col = CanAfford(c) ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.45f, 0.45f);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(col)).Append('>')
                  .Append(PixelClicker.FormatNumberShort(c.amount)).Append(' ').Append(CostName(c)).Append("</color>  ");
            }
            PixelUIKit.SetText(row.cost, sb.ToString());

            // Two separate, simple facts: what the vendor still has (gold, top right) and what you already hold (dim, bottom right).
            bool soldOut = stock.TryGetValue(item, out int stockLeft) && stockLeft < 1;
            PixelUIKit.SetText(row.owned, "You own " + PixelConsumables.OwnedText(consumables.ItemOwned(item)));
            PixelUIKit.SetText(row.stockLabel, stock.ContainsKey(item) ? (soldOut ? "Sold out" : "Stock " + stockLeft) : "");

            bool full = consumables.ItemRoom(item) < 1;
            bool afford = true;
            foreach (PixelShop.PackCost c in price) if (!CanAfford(c)) afford = false;
            row.buy.interactable = !full && !soldOut && afford;
            PixelUIKit.SetText(row.buyLabel, soldOut ? "Sold out" : full ? fullText : buyText);
            PixelUIKit.SetRole(row.buyImage, full || soldOut || !afford ? ButtonRole.Neutral : ButtonRole.Success);
        }
    }

    // ------------------------------------------------------------------
    // Building the visitor and the boxes (all drawn in code)
    // ------------------------------------------------------------------

    /// <summary>
    /// The shared cube body of every vendor (Cubie's shape): legs, shoes, arms, and a cube drawn as a shaded side, a front face and a top.
    /// Each vendor adds their own face, hat and props on top. The front face is 200 x 200 centred at (0, 180).
    /// </summary>
    protected void CubeBody(Transform a, Color face, Color top, Color side, Color shoe, bool arms = true)
    {
        Block(a, "Leg L", side, new Vector2(40f, 50f), new Vector2(-45f, 28f));
        Block(a, "Leg R", side, new Vector2(40f, 50f), new Vector2(45f, 28f));
        Block(a, "Shoe L", shoe, new Vector2(58f, 20f), new Vector2(-48f, 10f));
        Block(a, "Shoe R", shoe, new Vector2(58f, 20f), new Vector2(48f, 10f));
        if (arms)
        {
            Block(a, "Arm L", side, new Vector2(34f, 100f), new Vector2(-122f, 150f), 8f);
            Block(a, "Arm R", side, new Vector2(34f, 100f), new Vector2(122f, 150f), -8f);
        }
        Block(a, "Side", side, new Vector2(200f, 200f), new Vector2(12f, 190f));
        Block(a, "Face", face, new Vector2(200f, 200f), new Vector2(0f, 180f));
        Block(a, "Top", top, new Vector2(200f, 26f), new Vector2(6f, 290f));
    }

    /// <summary>Two round-ish eyes on a cube's face: white blocks with dark pupils ('look' shifts the pupils sideways).</summary>
    protected void CubeEyes(Transform a, Color white, Color pupil, float y = 200f, float spread = 42f, float look = 0f, float size = 1f)
    {
        Block(a, "Eye L", white, new Vector2(52f, 40f) * size, new Vector2(-spread, y));
        Block(a, "Eye R", white, new Vector2(52f, 40f) * size, new Vector2(spread, y));
        Block(a, "Pupil L", pupil, new Vector2(18f, 26f) * size, new Vector2(-spread + look, y - 2f));
        Block(a, "Pupil R", pupil, new Vector2(18f, 26f) * size, new Vector2(spread + look, y - 2f));
    }

    protected RectTransform Block(Transform parent, string name, Color color, Vector2 size, Vector2 pos, float rotation = 0f)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        r.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return r;
    }

    /// <summary>Draws this vendor (for a menu to walk about) under 'parent' at the given height: a holder anchored at the bottom centre with its art as the first child.</summary>
    public RectTransform BuildWalker(Transform parent, float height)
    {
        GameObject holder = new GameObject(DisplayName + " Walker", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        RectTransform r = holder.GetComponent<RectTransform>();
        float scale = height / 400f;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(300f * scale, 400f * scale);
        GameObject art = new GameObject("Art", typeof(RectTransform));
        art.transform.SetParent(holder.transform, false);
        RectTransform ar = art.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0f);
        ar.pivot = new Vector2(0.5f, 0f);
        ar.sizeDelta = new Vector2(300f, 400f);
        ar.localScale = Vector3.one * scale;
        DrawVisitor(art.transform);
        return r;
    }

    private void BuildUI()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
        PixelUIKit.EnsureEventSystem();
        canvasRoot = PixelUIKit.CreateCanvas("PixelVisitor Canvas", 700, new Vector2(1920f, 1080f), true);
        Track(canvasRoot);

        // --- The visitor: a little pixel-art person made of blocks (300 x 400 design space, scaled to visitorHeight).
        GameObject holder = new GameObject("Visitor", typeof(RectTransform));
        holder.transform.SetParent(canvasRoot.transform, false);
        visitorRect = holder.GetComponent<RectTransform>();
        float scale = visitorHeight / 400f;
        visitorRect.anchorMin = visitorRect.anchorMax = new Vector2(1f, 0f);
        visitorRect.pivot = new Vector2(0.5f, 0f);
        visitorRect.sizeDelta = new Vector2(300f * scale, 400f * scale);

        GameObject art = new GameObject("Art", typeof(RectTransform));
        art.transform.SetParent(holder.transform, false);
        RectTransform artRect = art.GetComponent<RectTransform>();
        artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0f);
        artRect.pivot = new Vector2(0.5f, 0f);
        artRect.sizeDelta = new Vector2(300f, 400f);
        artRect.localScale = Vector3.one * scale;

        DrawVisitor(art.transform);

        // --- Greeting box (left of the visitor).
        PixelWindowParts greetBox = Panel("Greeting", Pick(visitorName, DefaultName), new Vector2(720f, 470f), visitorRect.sizeDelta.x + 90f);
        greetingPanel = greetBox.Rect;

        TMP_Text message = PixelUIKit.CreateText(Font, greetingPanel, "Message", string.Format(Pick(greeting, DefaultGreeting), Mathf.RoundToInt(discountFraction * 100f)), 34f,
                                                 TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        message.enableAutoSizing = true;
        message.fontSizeMax = 34f;
        message.fontSizeMin = 14f;   // (a long greeting shrinks instead of being cut off)
        greetMessage = message;
        Anchor(message.rectTransform, 0f, 0f, 1f, 1f, new Vector2(36f, 130f), new Vector2(-36f, -(greetBox.HeaderHeight + 2f)));

        Button accept = PixelUIKit.CreateButton(Font, greetingPanel, "Accept", AcceptLabel, new Vector2(300f, 80f), ButtonRole.Success, 36f);
        PixelUIKit.Caps(accept);
        Corner(accept.GetComponent<RectTransform>(), new Vector2(0.25f, 0f), new Vector2(0f, 36f));
        accept.onClick.AddListener(() => { if (Typing) { FinishType(); return; } if (decision == 0) decision = 1; });
        Button reject = PixelUIKit.CreateButton(Font, greetingPanel, "Reject", RejectLabel, new Vector2(300f, 80f), ButtonRole.Danger, 36f);
        PixelUIKit.Caps(reject);
        Corner(reject.GetComponent<RectTransform>(), new Vector2(0.75f, 0f), new Vector2(0f, 36f));
        reject.onClick.AddListener(() => { if (Typing) { FinishType(); return; } if (decision == 0) decision = 2; });
        greetingPanel.gameObject.SetActive(false);

        // --- Wares window.
        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        trades = RollTrades();
        tabsMode = BuySellTabs;
        bool tradeMode = trades != null && !tabsMode;
        List<int> wares = tradeMode ? new List<int>() : Wares();
        stock.Clear();
        if (!tradeMode && OfferedWares > 0)
            while (wares.Count > OfferedWares) wares.RemoveAt(Random.Range(0, wares.Count)); // a small random selection
        if (!tradeMode) ModifyWares(wares);
        if (!tradeMode && StockMax > 0)
            foreach (int w in wares) stock[w] = StockFor(w);
        if (!tradeMode && StockMax > 0) wares.RemoveAll(w => stock.TryGetValue(w, out int n) && n <= 0);   // a stock of 0 = he didn't bring that one this time
        visitPrice.Clear();
        if (!tradeMode)
            foreach (int w in wares) { PixelShop.PackCost[] own = RollVisitPrice(w); if (own != null) visitPrice[w] = own; }

        // The rows: (item, trade). A buy / sell vendor lists the wares first, then the trades; the tabs show one group at a time.
        List<KeyValuePair<int, Trade>> entries = new List<KeyValuePair<int, Trade>>();
        if (tradeMode) foreach (Trade t in trades) entries.Add(new KeyValuePair<int, Trade>(-1, t));
        else
        {
            foreach (int w in wares) entries.Add(new KeyValuePair<int, Trade>(w, null));
            if (tabsMode && trades != null) foreach (Trade t in trades) entries.Add(new KeyValuePair<int, Trade>(-1, t));
        }
        int rowCount = entries.Count;
        int tallest = tabsMode ? Mathf.Max(wares.Count, trades != null ? trades.Count : 0) : rowCount;
        float rowH = 104f;
        float tabsH = tabsMode ? 64f : 0f;
        float listH = Mathf.Max(tallest * (rowH + 8f), tabsMode && CustomSellPanel ? SellPanelHeight : 0f);
        float top = PixelUIKit.WindowHeaderHeight;   // the window header: the sale line, tabs and list sit below it
        float height = Mathf.Min(1080f - 2f * barH - 30f, top + 106f + tabsH + listH + 110f);
        waresPanel = Panel("Wares", Pick(waresTitle, DefaultWaresTitle), new Vector2(900f, Mathf.Max(420f, height)), visitorRect.sizeDelta.x + 90f).Rect;

        TMP_Text sale = PixelUIKit.CreateText(Font, waresPanel, "Sale", string.Format(SaleFormat, Mathf.RoundToInt(discountFraction * 100f)), 26f,
                                              TextAlignmentOptions.Center, FontStyles.Italic, new Color(0.7f, 1f, 0.75f));
        Anchor(sale.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -(top + 38f)), new Vector2(-20f, -top));

        // Buy / Sell tabs (only for a vendor that does both).
        tabButtons = null;
        if (tabsMode)
        {
            tabButtons = new Button[2];
            string[] names = { "Buy", "Sell" };
            for (int i = 0; i < 2; i++)
            {
                int captured = i;
                Button tb = PixelUIKit.CreateButton(Font, waresPanel, "Tab " + names[i], names[i], new Vector2(220f, 52f), ButtonRole.Neutral, 30f);
                PixelUIKit.Caps(tb);
                RectTransform tr = tb.GetComponent<RectTransform>();
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
                tr.anchoredPosition = new Vector2(i == 0 ? -118f : 118f, -(top + 42f));
                tb.onClick.AddListener(() => SetShopTab(captured));
                tabButtons[i] = tb;
            }
        }

        waresScroll = PixelUIKit.CreateScrollView(waresPanel, "Wares List", new Color(1f, 1f, 1f, 0.35f), 12f, rowH * 0.6f, out waresContent, out waresBar);
        RectTransform vr = waresScroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(24f, 110f);
        vr.offsetMax = new Vector2(-24f, -(top + 48f) - tabsH);

        // A vendor's own Sell tab (in place of the trade rows), over the same area as the list.
        sellPanelRoot = null;
        if (tabsMode && CustomSellPanel)
        {
            GameObject sp = new GameObject("Sell Panel", typeof(RectTransform));
            sp.transform.SetParent(waresPanel, false);
            sellPanelRoot = sp.GetComponent<RectTransform>();
            sellPanelRoot.anchorMin = Vector2.zero; sellPanelRoot.anchorMax = Vector2.one;
            sellPanelRoot.offsetMin = vr.offsetMin; sellPanelRoot.offsetMax = vr.offsetMax;
            BuildSellPanel(sellPanelRoot);
            sp.SetActive(false);
        }

        rows.Clear();
        float y = 0f;
        for (int n = 0; n < rowCount; n++)
        {
            int item = entries[n].Key;
            Trade trade = entries[n].Value;
            bool isTrade = trade != null;
            WareRow row = new WareRow { item = item, trade = trade, tab = isTrade ? 1 : 0 };
            GameObject go = new GameObject("Ware", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(waresContent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f, 1f);
            PixelUIKit.StyleButtonLook(go.GetComponent<Image>());   // rounded, like the buttons (the look only: a row is not recoloured as a button)
            go.GetComponent<Image>().raycastTarget = false;
            row.rect = go.GetComponent<RectTransform>();
            row.rect.anchorMin = new Vector2(0f, 1f);
            row.rect.anchorMax = new Vector2(1f, 1f);
            row.rect.pivot = new Vector2(0.5f, 1f);
            row.rect.sizeDelta = new Vector2(-20f, rowH);
            row.rect.anchoredPosition = new Vector2(0f, -y);
            Image rowBack = go.GetComponent<Image>();
            Outline rowGlow = null;
            if (!isTrade && Highlighted(item))
            {
                rowGlow = go.AddComponent<Outline>();
                rowGlow.effectDistance = new Vector2(4f, -4f);
                rowGlow.useGraphicAlpha = false;
            }

            row.name = PixelUIKit.CreateText(Font, go.transform, "Name", isTrade ? TradeGetText(trade) : consumables.ItemName(item), 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
            row.name.enableAutoSizing = true; row.name.fontSizeMax = 32f; row.name.fontSizeMin = 14f;
            Anchor(row.name.rectTransform, 0f, 0.55f, 0.62f, 1f, new Vector2(16f, 0f), new Vector2(0f, -6f));

            row.cost = PixelUIKit.CreateText(Font, go.transform, "Cost", "", 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Color.white);
            row.cost.richText = true;
            row.cost.enableAutoSizing = true; row.cost.fontSizeMax = 24f; row.cost.fontSizeMin = 11f;
            Anchor(row.cost.rectTransform, 0f, 0.05f, 0.62f, 0.55f, new Vector2(16f, 0f), new Vector2(0f, 0f));

            row.owned = PixelUIKit.CreateText(Font, go.transform, "Owned", "", 22f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, new Color(1f, 1f, 1f, 0.6f));
            row.owned.enableAutoSizing = true; row.owned.fontSizeMax = 22f; row.owned.fontSizeMin = 11f;
            Anchor(row.owned.rectTransform, 0.62f, 0.05f, 1f, 0.55f, new Vector2(0f, 0f), new Vector2(-190f, 0f));

            row.stockLabel = PixelUIKit.CreateText(Font, go.transform, "Stock", "", 24f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));
            row.stockLabel.enableAutoSizing = true; row.stockLabel.fontSizeMax = 24f; row.stockLabel.fontSizeMin = 11f;
            Anchor(row.stockLabel.rectTransform, 0.62f, 0.55f, 1f, 1f, new Vector2(0f, 0f), new Vector2(-190f, -6f));

            row.buy = PixelUIKit.CreateButton(Font, go.transform, "Buy", isTrade ? TradeText : buyText, new Vector2(150f, 70f), ButtonRole.Success, 32f);
            row.buyImage = row.buy.GetComponent<Image>();
            row.buyLabel = row.buy.GetComponentInChildren<TMP_Text>();
            RectTransform br = row.buy.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
            br.anchoredPosition = new Vector2(-14f, 0f);
            if (rowGlow != null) go.AddComponent<PixelPulseGlow>().Setup(rowBack, rowGlow, row.name);
            int captured = item;
            Trade capturedTrade = trade;
            row.buy.onClick.AddListener(() => { if (capturedTrade != null) DoTrade(capturedTrade); else Buy(captured); });
            rows.Add(row);
            y += rowH + 8f;
        }
        waresViewHeight = vr.rect.height > 1f ? vr.rect.height : (waresPanel.sizeDelta.y - (top + 158f) - tabsH);
        shopTab = 0;
        LayoutRows(rowH);

        Button done = PixelUIKit.CreateButton(Font, waresPanel, "Done", doneText, new Vector2(320f, 80f), ButtonRole.Neutral, 36f);
        PixelUIKit.Caps(done);
        Corner(done.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 20f));
        done.onClick.AddListener(() => waresDone = true);
        waresPanel.gameObject.SetActive(false);

        // --- A second speech box (only when the subclass set AsideText for this visit).
        asidePanel = null;
        if (!string.IsNullOrEmpty(AsideText))
        {
            PixelWindowParts aside = Panel("Aside", Pick(visitorName, DefaultName), new Vector2(720f, 400f), visitorRect.sizeDelta.x + 90f);
            asidePanel = aside.Rect;
            TMP_Text am = PixelUIKit.CreateText(Font, asidePanel, "Message", AsideText, 34f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
            am.enableAutoSizing = true;
            am.fontSizeMax = 34f;
            am.fontSizeMin = 18f;
            asideMessage = am;
            Anchor(am.rectTransform, 0f, 0f, 1f, 1f, new Vector2(36f, 130f), new Vector2(-36f, -(aside.HeaderHeight + 2f)));
            Button next = PixelUIKit.CreateButton(Font, asidePanel, "Next", "Next", new Vector2(300f, 80f), ButtonRole.Success, 36f);
            PixelUIKit.Caps(next);
            Corner(next.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 36f));
            next.onClick.AddListener(() => { if (Typing) { FinishType(); return; } asideDone = true; });
            // a small back arrow: hold it to return to the greeting
            Button back = PixelUIKit.CreateButton(Font, asidePanel, "Back", "<", new Vector2(64f, 56f), ButtonRole.Neutral, 32f);
            PixelUIKit.UseArrowGlyph(back, true);
            RectTransform bkr = back.GetComponent<RectTransform>();
            bkr.anchorMin = bkr.anchorMax = Vector2.zero;
            bkr.pivot = Vector2.zero;
            bkr.anchoredPosition = new Vector2(24f, 40f);
            GameObject fillGo = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(back.transform, false);
            fillGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
            fillGo.GetComponent<Image>().raycastTarget = false;
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            fillRect.SetSiblingIndex(1);
            PixelHoldButton hold = back.gameObject.AddComponent<PixelHoldButton>();
            hold.Setup(fillRect, 0.6f, () => { hold.ResetProgress(); asideBack = true; });
            asidePanel.gameObject.SetActive(false);
        }
    }

    private bool tabsMode;
    private int shopTab;
    private Button[] tabButtons;
    private float waresViewHeight;
    private float rowHeight = 104f;

    /// <summary>Lays the rows out in the scroll list: all of them (one group), or only the shown tab's rows (Buy / Sell).</summary>
    private void LayoutRows(float rowH)
    {
        rowHeight = rowH;
        float y = 0f;
        foreach (WareRow row in rows)
        {
            bool show = !tabsMode || row.tab == shopTab;
            row.rect.gameObject.SetActive(show);
            if (!show) continue;
            row.rect.anchoredPosition = new Vector2(0f, -y);
            y += rowH + 8f;
        }
        PixelUIKit.UpdateScrollView(waresScroll, waresBar, Mathf.Max(0f, y - 8f), waresViewHeight);
        if (sellPanelRoot != null)
        {
            bool sellShown = shopTab == 1;
            waresScroll.gameObject.SetActive(!sellShown);
            if (sellPanelRoot.gameObject.activeSelf != sellShown) { sellPanelRoot.gameObject.SetActive(sellShown); if (sellShown) OnSellPanelShown(); }
        }
        if (tabButtons != null)
            for (int i = 0; i < tabButtons.Length; i++)
                PixelUIKit.SetRole(tabButtons[i], i == shopTab ? ButtonRole.Selected : ButtonRole.Neutral);
        RefreshWares();
    }

    private void SetShopTab(int tab)
    {
        if (shopTab == tab) return;
        shopTab = tab;
        PixelAudio.Play("ui_click");
        LayoutRows(rowHeight);
    }

    private string TradeGetText(Trade t) =>
        "Get " + PixelClicker.FormatNumberShort(t.get) + " " + clicker.Tiers[t.getTier].displayName;

    /// <summary>
    /// A speech / wares box: the standard window as a dialog (PixelUIKit.CreateWindow, no X - its own buttons close it; its PixelPop grows it in
    /// and shrinks it away), title in the visitor's colour. Moved from the centre to the right edge, just left of the visitor.
    /// </summary>
    private PixelWindowParts Panel(string name, string title, Vector2 size, float rightOffset)
    {
        PixelWindowParts w = PixelUIKit.CreateWindow(Font, canvasRoot.transform, name, title, size, new Color(0.08f, 0.1f, 0.16f, 0.97f),
                                                     PixelUIKit.WindowKind.Fixed, null, closeButton: false);
        w.Title.color = TitleColor;
        FitName(w.Title);   // (a long name stays on one line)
        RectTransform r = w.Rect;
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.anchoredPosition = new Vector2(-(rightOffset + 40f), 0f);
        return w;
    }

    /// <summary>A visitor's name stays on one line and shrinks to fit the box (long names like 'Big Tim the Lumberjack' used to wrap over the message).</summary>
    private static void FitName(TMP_Text t) => PixelUIKit.FitOneLine(t, 24f);   // (the title is made at 48: it shrinks to 24 at most)

    private static void Anchor(RectTransform r, float minX, float minY, float maxX, float maxY, Vector2 offsetMin, Vector2 offsetMax)
    {
        r.anchorMin = new Vector2(minX, minY);
        r.anchorMax = new Vector2(maxX, maxY);
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    /// <summary>Anchors a button to the bottom edge at a horizontal fraction, 'offset' up from the bottom.</summary>
    private static void Corner(RectTransform r, Vector2 anchor, Vector2 offset)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = offset;
    }
}

/// <summary>Makes a ware row glow yellow: the row's background and outline pulse between dark and bright gold and the name turns yellow (unscaled time: the game is stopped during a visit).</summary>
public class PixelPulseGlow : MonoBehaviour
{
    private Image back;
    private Outline outline;
    private TMP_Text label;

    public void Setup(Image background, Outline glow, TMP_Text name)
    {
        back = background;
        outline = glow;
        label = name;
    }

    private void Update()
    {
        if (back == null) return;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        back.color = Color.Lerp(new Color(0.3f, 0.24f, 0.06f, 1f), new Color(0.52f, 0.42f, 0.08f, 1f), t);
        if (outline != null) outline.effectColor = Color.Lerp(new Color(1f, 0.8f, 0.1f, 0.35f), new Color(1f, 0.92f, 0.3f, 1f), t);
        if (label != null) label.color = Color.Lerp(new Color(1f, 0.9f, 0.35f), new Color(1f, 1f, 0.7f), t);
    }
}
