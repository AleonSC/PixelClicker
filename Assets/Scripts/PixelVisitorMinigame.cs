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
    private static void ResetVisitStatics() { nextAnyVisitTime = 0f; anyVisiting = false; }

    /// <summary>Is it a calm moment for a vendor to walk in? Never over a tip box (or tips waiting), the dragon show / wish, a pet popup, a pause or the title screen.</summary>
    private static bool CalmForVisit()
    {
        if (Time.timeScale <= 0f) return false;                       // paused or Time Stop
        if (PixelNotice.IsShowing || PixelHints.TipsPending) return false;
        if (PixelDragonFind.Playing || PixelDragonWish.Active || PixelPets.PopupOpen) return false;
        if (PixelPauseMenu.IsPaused || PixelTitleScreen.Showing) return false;
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

    /// <summary>Label of a trade's button.</summary>
    protected virtual string TradeText => "Trade";

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
    /// <summary>A ware shown glowing yellow in the list (something important).</summary>
    protected virtual bool Highlighted(int item) => false;
    protected virtual int StockFor(int item) => Random.Range(Mathf.Max(1, StockMin), Mathf.Max(StockMin, StockMax) + 1);

    private readonly Dictionary<int, int> stock = new Dictionary<int, int>(); // ware -> how many are left this visit (only when the stock is limited)

    private class WareRow
    {
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

    private void Update()
    {
        if (!running || visiting || clicker == null) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;
        if (anyVisiting || Time.unscaledTime < nextAnyVisitTime || !CalmForVisit()) return;   // waits (checked every frame) for a quiet moment
        if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(VisitRoutine());
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
        if (Application.isPlaying && !visiting && clicker != null) StartCoroutine(VisitRoutine());
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

    private IEnumerator VisitRoutine()
    {
        visiting = true;
        anyVisiting = true;
        decision = 0;
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
        Freeze();
        PixelWindows.Register(this, 150, () => visiting, Leave);

        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        float visitorW = visitorRect.sizeDelta.x;
        float restX = -(visitorW * 0.5f + 50f);
        float hiddenX = visitorW * 0.5f + 40f;
        visitorRect.anchoredPosition = new Vector2(hiddenX, barH + 24f);

        // Slide in from the right (the game keeps running while they walk in).
        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            visitorRect.anchoredPosition = new Vector2(Mathf.Lerp(hiddenX, restX, k), barH + 24f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
            SetDim(k);
            yield return null;
        }
        visitorRect.anchoredPosition = new Vector2(restX, barH + 24f);
        SetDim(1f);

        // They greet you.
        greetingPanel.gameObject.SetActive(true);

        while (decision == 0) { Bob(restX, barH); yield return null; }
        greetingPanel.gameObject.SetActive(false);

        if (decision == 1)
        {
            RefreshWares();
            waresPanel.gameObject.SetActive(true);
            while (!waresDone) { Bob(restX, barH); yield return null; }
            waresPanel.gameObject.SetActive(false);
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
            if (!consumables.ItemCraftOnly(i) && IsWare(i)) list.Add(i);
        return list;
    }

    private PixelShop.PackCost[] PriceOf(int item)
    {
        PixelShop.PackCost[] baseCosts = consumables.ItemCosts(item);
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
        PixelUIKit.SetText(row.cost, "Pay <color=#" + ColorUtility.ToHtmlStringRGB(col) + ">" + PixelClicker.FormatNumberShort(t.pay) + " " + payName + "</color>");
        PixelUIKit.SetText(row.owned, "You own " + PixelClicker.FormatNumberShort(clicker.GetCount(clicker.Tiers[t.payTier].type)));
        PixelUIKit.SetText(row.stockLabel, "");
        row.buy.interactable = !t.done && afford;
        PixelUIKit.SetText(row.buyLabel, t.done ? "Sold" : TradeText);
        row.buyImage.color = t.done || !afford ? new Color(0.3f, 0.3f, 0.35f, 1f) : new Color(0.2f, 0.55f, 0.3f, 1f);
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
            row.buyImage.color = full || soldOut || !afford ? new Color(0.3f, 0.3f, 0.35f, 1f) : new Color(0.2f, 0.55f, 0.3f, 1f);
        }
    }

    // ------------------------------------------------------------------
    // Building the visitor and the boxes (all drawn in code)
    // ------------------------------------------------------------------

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
        greetingPanel = Panel("Greeting", new Vector2(720f, 470f), visitorRect.sizeDelta.x + 90f);
        TMP_Text title = PixelUIKit.CreateText(Font, greetingPanel, "Name", Pick(visitorName, DefaultName), 54f, TextAlignmentOptions.Center, FontStyles.Bold, TitleColor);
        PixelUIKit.Caps(title);
        Anchor(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -78f), new Vector2(-20f, -16f));

        TMP_Text message = PixelUIKit.CreateText(Font, greetingPanel, "Message", string.Format(Pick(greeting, DefaultGreeting), Mathf.RoundToInt(discountFraction * 100f)), 34f,
                                                 TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        message.enableAutoSizing = true;
        message.fontSizeMax = 34f;
        message.fontSizeMin = 18f;
        Anchor(message.rectTransform, 0f, 0f, 1f, 1f, new Vector2(36f, 130f), new Vector2(-36f, -92f));

        Button accept = PixelUIKit.CreateButton(Font, greetingPanel, "Accept", acceptText, new Vector2(300f, 80f), new Color(0.2f, 0.55f, 0.3f, 1f), Color.white, 36f);
        PixelUIKit.Caps(accept);
        Corner(accept.GetComponent<RectTransform>(), new Vector2(0.25f, 0f), new Vector2(0f, 36f));
        accept.onClick.AddListener(() => { if (decision == 0) decision = 1; });
        Button reject = PixelUIKit.CreateButton(Font, greetingPanel, "Reject", rejectText, new Vector2(300f, 80f), new Color(0.5f, 0.22f, 0.22f, 1f), Color.white, 36f);
        PixelUIKit.Caps(reject);
        Corner(reject.GetComponent<RectTransform>(), new Vector2(0.75f, 0f), new Vector2(0f, 36f));
        reject.onClick.AddListener(() => { if (decision == 0) decision = 2; });
        greetingPanel.gameObject.SetActive(false);

        // --- Wares window.
        float barH = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        trades = RollTrades();
        bool tradeMode = trades != null;
        List<int> wares = tradeMode ? new List<int>() : Wares();
        stock.Clear();
        if (!tradeMode && OfferedWares > 0)
            while (wares.Count > OfferedWares) wares.RemoveAt(Random.Range(0, wares.Count)); // a small random selection
        if (!tradeMode) ModifyWares(wares);
        if (!tradeMode && StockMax > 0)
            foreach (int w in wares) stock[w] = StockFor(w);
        int rowCount = tradeMode ? trades.Count : wares.Count;
        float rowH = 104f;
        float height = Mathf.Min(1080f - 2f * barH - 30f, 190f + rowCount * (rowH + 8f) + 110f);
        waresPanel = Panel("Wares", new Vector2(900f, Mathf.Max(420f, height)), visitorRect.sizeDelta.x + 90f);
        TMP_Text wt = PixelUIKit.CreateText(Font, waresPanel, "Title", Pick(waresTitle, DefaultWaresTitle), 50f, TextAlignmentOptions.Center, FontStyles.Bold, TitleColor);
        PixelUIKit.Caps(wt);
        Anchor(wt.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -84f), new Vector2(-20f, -16f));

        TMP_Text sale = PixelUIKit.CreateText(Font, waresPanel, "Sale", string.Format(SaleFormat, Mathf.RoundToInt(discountFraction * 100f)), 26f,
                                              TextAlignmentOptions.Center, FontStyles.Italic, new Color(0.7f, 1f, 0.75f));
        Anchor(sale.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -122f), new Vector2(-20f, -84f));

        waresScroll = PixelUIKit.CreateScrollView(waresPanel, "Wares List", new Color(1f, 1f, 1f, 0.35f), 12f, rowH * 0.6f, out waresContent, out waresBar);
        RectTransform vr = waresScroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(24f, 110f);
        vr.offsetMax = new Vector2(-24f, -132f);

        rows.Clear();
        float y = 0f;
        for (int n = 0; n < rowCount; n++)
        {
            int item = tradeMode ? -1 : wares[n];
            Trade trade = tradeMode ? trades[n] : null;
            WareRow row = new WareRow { item = item, trade = trade };
            GameObject go = new GameObject("Ware", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(waresContent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f, 1f);
            PixelUIKit.StyleButton(go.GetComponent<Image>());   // rounded, like the buttons
            go.GetComponent<Image>().raycastTarget = false;
            row.rect = go.GetComponent<RectTransform>();
            row.rect.anchorMin = new Vector2(0f, 1f);
            row.rect.anchorMax = new Vector2(1f, 1f);
            row.rect.pivot = new Vector2(0.5f, 1f);
            row.rect.sizeDelta = new Vector2(-20f, rowH);
            row.rect.anchoredPosition = new Vector2(0f, -y);
            Image rowBack = go.GetComponent<Image>();
            Outline rowGlow = null;
            if (!tradeMode && Highlighted(item))
            {
                rowGlow = go.AddComponent<Outline>();
                rowGlow.effectDistance = new Vector2(4f, -4f);
                rowGlow.useGraphicAlpha = false;
            }

            row.name = PixelUIKit.CreateText(Font, go.transform, "Name", tradeMode ? TradeGetText(trade) : consumables.ItemName(item), 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
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

            row.buy = PixelUIKit.CreateButton(Font, go.transform, "Buy", buyText, new Vector2(150f, 70f), new Color(0.2f, 0.55f, 0.3f, 1f), Color.white, 32f);
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
        PixelUIKit.UpdateScrollView(waresScroll, waresBar, Mathf.Max(0f, y - 8f), vr.rect.height > 1f ? vr.rect.height : (waresPanel.sizeDelta.y - 242f));

        Button done = PixelUIKit.CreateButton(Font, waresPanel, "Done", doneText, new Vector2(320f, 80f), new Color(0.3f, 0.3f, 0.38f, 1f), Color.white, 36f);
        PixelUIKit.Caps(done);
        Corner(done.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 20f));
        done.onClick.AddListener(() => waresDone = true);
        waresPanel.gameObject.SetActive(false);
    }

    private string TradeGetText(Trade t) =>
        "Get " + PixelClicker.FormatNumberShort(t.get) + " " + clicker.Tiers[t.getTier].displayName;

    private RectTransform Panel(string name, Vector2 size, float rightOffset)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(g.GetComponent<Image>(), new Color(0.08f, 0.1f, 0.16f, 0.97f));   // the same rounded glowing frame as every window
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = new Vector2(-(rightOffset + 40f), 0f);
        return r;
    }

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
