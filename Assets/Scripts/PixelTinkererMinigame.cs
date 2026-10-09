using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Tinkerer: a "visitor" minigame (id "tinkerer", pack with <c>unlocksMinigame = "tinkerer"</c>, Minigames tab). Every few minutes a
/// little code-drawn tinkerer slides in from the right side of the screen, time stops and they greet you with an offer: every
/// consumable DEVICE (vacuum device, fan, sorter, combo fuel, ghost bait, pet treat...) at a big discount. Accept to see the wares
/// and buy what you like, or reject and they slide away again. Time resumes when they leave (Escape counts as rejecting).
/// Extends <see cref="PixelMinigame"/>, so it needs no changes to the shop or the save system.
/// </summary>
public class PixelTinkererMinigame : PixelMinigame
{
    [Header("References")]
    [Tooltip("The PixelClicker whose pixels pay for the wares. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("The consumables that supply the wares (every device). Found automatically if left empty.")]
    [SerializeField] private PixelConsumables consumables;

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
    [Tooltip("Fewest seconds between visits.")]
    [SerializeField] private float minInterval = 300f;

    [Min(1f)]
    [Tooltip("Most seconds between visits.")]
    [SerializeField] private float maxInterval = 600f;

    [Header("Offer")]
    [Range(0f, 0.99f)]
    [Tooltip("How much cheaper the devices are than in the shop (0.8 = 80% off).")]
    [SerializeField] private float discountFraction = 0.8f;

    [Header("Visitor")]
    [Min(0.1f)]
    [Tooltip("Seconds the slide in (and out) takes.")]
    [SerializeField] private float slideSeconds = 1.4f;

    [Min(100f)]
    [Tooltip("Height of the tinkerer (canvas units).")]
    [SerializeField] private float visitorHeight = 420f;

    [Header("Texts")]
    [Tooltip("Name shown at the top of the speech box.")]
    [SerializeField] private string visitorName = "Tinkerer";

    [TextArea(2, 5)]
    [Tooltip("What the tinkerer says on arrival. {0} = the discount in percent.")]
    [SerializeField] private string greeting = "Well hello there! I'm the Tinkerer. I've got gadgets for every occasion, and for you I'll knock {0}% off every one. Interested?";

    [Tooltip("Label of the button that accepts the offer.")]
    [SerializeField] private string acceptText = "Show me!";

    [Tooltip("Label of the button that rejects the offer.")]
    [SerializeField] private string rejectText = "No thanks";

    [Tooltip("Title of the wares window.")]
    [SerializeField] private string waresTitle = "Tinkerer's Wares";

    [Tooltip("Label of the button that closes the wares window.")]
    [SerializeField] private string doneText = "Done";

    [Tooltip("Label of a buy button.")]
    [SerializeField] private string buyText = "Buy";

    [Tooltip("Shown instead of the buy button when you can hold no more of that item.")]
    [SerializeField] private string fullText = "Full";

    [Tooltip("Event-log line when the tinkerer arrives.")]
    [SerializeField] private string arrivedLog = "The Tinkerer is here!";

    // ------------------------------------------------------------------

    public override string Id => "tinkerer";
    public override string DisplayName => "Tinkerer";
    public override bool Running => running;
    public override bool Busy => visiting;

    private bool visiting;
    private float spawnTimer;
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
    private bool dialogOpen;
    private TMP_FontAsset Font => clicker != null ? clicker.UIFont : null;

    private class WareRow
    {
        public int item;
        public RectTransform rect;
        public TMP_Text name, cost, owned, buyLabel;
        public Button buy;
        public Image buyImage;
    }

    protected override void Awake()
    {
        base.Awake();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (startRunning) running = true;
        spawnTimer = firstVisitDelay;
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
        if (PixelMinigameLimits.AllowAnother(this)) StartCoroutine(VisitRoutine());
        else spawnTimer = PixelMinigameLimits.RetrySeconds;
    }

    public override void Activate()
    {
        if (running) return;
        running = true;
        spawnTimer = firstVisitDelay;
    }

    public override void Deactivate() => running = false;

    [ContextMenu("Spawn Tinkerer Now")]
    public override void SpawnNow()
    {
        if (Application.isPlaying && !visiting && clicker != null) StartCoroutine(VisitRoutine());
    }

    protected override void OnDespawned()
    {
        visiting = false;
        dialogOpen = false;
        Unfreeze();
        PixelWindows.Unregister(this);
        spawnTimer = Random.Range(minInterval, maxInterval);
    }

    // ------------------------------------------------------------------
    // The visit
    // ------------------------------------------------------------------

    private IEnumerator VisitRoutine()
    {
        visiting = true;
        decision = 0;
        waresDone = false;
        Report(MinigameEvent.Spawned);
        PixelHints.Announce(arrivedLog);
        PixelAudio.Play("tinkerer_spawn");
        BuildUI();

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
            yield return null;
        }
        visitorRect.anchoredPosition = new Vector2(restX, barH + 24f);

        // Time stops and they greet you.
        Freeze();
        dialogOpen = true;
        PixelWindows.Register(this, 150, () => dialogOpen, Leave); // Escape = no thanks
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
        dialogOpen = false;
        PixelWindows.Unregister(this);
        Unfreeze();
        for (float t = 0f; t < slideSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / slideSeconds);
            visitorRect.anchoredPosition = new Vector2(Mathf.Lerp(restX, hiddenX, k), barH + 24f + Mathf.Abs(Mathf.Sin(t * 9f)) * 10f);
            yield return null;
        }

        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null;
        visiting = false;
        spawnTimer = Random.Range(minInterval, maxInterval);
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
            if (consumables.IsDevice(i) && !consumables.ItemCraftOnly(i)) list.Add(i);
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
        PixelShop.PackCost[] price = PriceOf(item);
        foreach (PixelShop.PackCost c in price) if (!CanAfford(c)) return;
        foreach (PixelShop.PackCost c in price) Spend(c);
        consumables.AddItem(item, 1);
        PixelStats.Count("shop.items");
        PixelStats.Count("tinkerer.bought");
        PixelAudio.Play("purchase");
        RefreshWares();
    }

    private void RefreshWares()
    {
        foreach (WareRow row in rows)
        {
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

            int cap = consumables.ItemCapacity(item);
            PixelUIKit.SetText(row.owned, cap > 0 && cap < 100000 ? "Owned " + consumables.ItemOwned(item) + " / " + cap : "Owned " + consumables.ItemOwned(item));

            bool full = consumables.ItemRoom(item) < 1;
            bool afford = true;
            foreach (PixelShop.PackCost c in price) if (!CanAfford(c)) afford = false;
            row.buy.interactable = !full && afford;
            PixelUIKit.SetText(row.buyLabel, full ? fullText : buyText);
            row.buyImage.color = full || !afford ? new Color(0.3f, 0.3f, 0.35f, 1f) : new Color(0.2f, 0.55f, 0.3f, 1f);
        }
    }

    // ------------------------------------------------------------------
    // Building the visitor and the boxes (all drawn in code)
    // ------------------------------------------------------------------

    private RectTransform Block(Transform parent, string name, Color color, Vector2 size, Vector2 pos, float rotation = 0f)
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
        canvasRoot = PixelUIKit.CreateCanvas("PixelTinkerer Canvas", 700, new Vector2(1920f, 1080f), true);
        Track(canvasRoot);

        // --- The tinkerer: a little pixel-art person made of blocks (300 x 400 design space, scaled to visitorHeight).
        GameObject holder = new GameObject("Tinkerer", typeof(RectTransform));
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

        Color coat = new Color(0.55f, 0.33f, 0.15f), dark = new Color(0.16f, 0.1f, 0.07f), skin = new Color(0.95f, 0.78f, 0.6f);
        Color metal = new Color(0.58f, 0.62f, 0.68f), yellow = new Color(1f, 0.82f, 0.25f), orange = new Color(0.92f, 0.5f, 0.12f);
        Transform a = art.transform;
        Block(a, "Pack", new Color(0.33f, 0.36f, 0.42f), new Vector2(110f, 140f), new Vector2(-72f, 170f));
        Block(a, "Antenna", metal, new Vector2(8f, 60f), new Vector2(-100f, 275f));
        Block(a, "Antenna Knob", yellow, new Vector2(20f, 20f), new Vector2(-100f, 308f), 45f);
        Block(a, "Gear 1", orange, new Vector2(34f, 34f), new Vector2(-84f, 195f), 45f);
        Block(a, "Gear 2", yellow, new Vector2(24f, 24f), new Vector2(-62f, 150f), 20f);
        Block(a, "Leg L", new Color(0.25f, 0.2f, 0.28f), new Vector2(46f, 80f), new Vector2(-34f, 50f));
        Block(a, "Leg R", new Color(0.25f, 0.2f, 0.28f), new Vector2(46f, 80f), new Vector2(34f, 50f));
        Block(a, "Boot L", dark, new Vector2(62f, 26f), new Vector2(-38f, 13f));
        Block(a, "Boot R", dark, new Vector2(62f, 26f), new Vector2(38f, 13f));
        Block(a, "Arm L", coat, new Vector2(38f, 120f), new Vector2(-98f, 165f), 8f);
        Block(a, "Body", coat, new Vector2(150f, 170f), new Vector2(0f, 175f));
        Block(a, "Pocket", new Color(0.45f, 0.26f, 0.11f), new Vector2(54f, 40f), new Vector2(-30f, 150f));
        Block(a, "Belt", dark, new Vector2(150f, 20f), new Vector2(0f, 118f));
        Block(a, "Buckle", yellow, new Vector2(28f, 20f), new Vector2(0f, 118f));
        Block(a, "Arm R", coat, new Vector2(38f, 120f), new Vector2(100f, 185f), -28f);
        Block(a, "Hand R", skin, new Vector2(34f, 34f), new Vector2(128f, 238f));
        Block(a, "Wrench Handle", metal, new Vector2(14f, 100f), new Vector2(148f, 285f), -28f);
        Block(a, "Wrench Head", metal, new Vector2(46f, 26f), new Vector2(172f, 335f), -28f);
        Block(a, "Head", skin, new Vector2(112f, 100f), new Vector2(0f, 292f));
        Block(a, "Hat Brim", orange, new Vector2(138f, 20f), new Vector2(0f, 346f));
        Block(a, "Hat Top", orange, new Vector2(96f, 44f), new Vector2(0f, 372f));
        Block(a, "Strap", dark, new Vector2(118f, 16f), new Vector2(0f, 322f));
        Block(a, "Lens L Frame", dark, new Vector2(46f, 46f), new Vector2(-28f, 322f));
        Block(a, "Lens R Frame", dark, new Vector2(46f, 46f), new Vector2(28f, 322f));
        Block(a, "Lens L", new Color(0.55f, 0.88f, 1f), new Vector2(34f, 34f), new Vector2(-28f, 322f));
        Block(a, "Lens R", new Color(0.55f, 0.88f, 1f), new Vector2(34f, 34f), new Vector2(28f, 322f));
        Block(a, "Mouth", dark, new Vector2(38f, 8f), new Vector2(0f, 262f));

        // --- Greeting box (left of the tinkerer).
        greetingPanel = Panel("Greeting", new Vector2(720f, 470f), visitorRect.sizeDelta.x + 90f);
        TMP_Text title = PixelUIKit.CreateText(Font, greetingPanel, "Name", visitorName, 54f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));
        PixelUIKit.Caps(title);
        Anchor(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -78f), new Vector2(-20f, -16f));

        TMP_Text message = PixelUIKit.CreateText(Font, greetingPanel, "Message", string.Format(greeting, Mathf.RoundToInt(discountFraction * 100f)), 34f,
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
        List<int> wares = Wares();
        float rowH = 104f;
        float height = Mathf.Min(1080f - 2f * barH - 30f, 190f + wares.Count * (rowH + 8f) + 110f);
        waresPanel = Panel("Wares", new Vector2(900f, Mathf.Max(420f, height)), visitorRect.sizeDelta.x + 90f);
        TMP_Text wt = PixelUIKit.CreateText(Font, waresPanel, "Title", waresTitle, 50f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.85f, 0.4f));
        PixelUIKit.Caps(wt);
        Anchor(wt.rectTransform, 0f, 1f, 1f, 1f, new Vector2(20f, -84f), new Vector2(-20f, -16f));

        TMP_Text sale = PixelUIKit.CreateText(Font, waresPanel, "Sale", Mathf.RoundToInt(discountFraction * 100f) + "% off every device", 26f,
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
        foreach (int item in wares)
        {
            WareRow row = new WareRow { item = item };
            GameObject go = new GameObject("Ware", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(waresContent, false);
            go.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f, 1f);
            go.GetComponent<Image>().raycastTarget = false;
            row.rect = go.GetComponent<RectTransform>();
            row.rect.anchorMin = new Vector2(0f, 1f);
            row.rect.anchorMax = new Vector2(1f, 1f);
            row.rect.pivot = new Vector2(0.5f, 1f);
            row.rect.sizeDelta = new Vector2(-20f, rowH);
            row.rect.anchoredPosition = new Vector2(0f, -y);

            row.name = PixelUIKit.CreateText(Font, go.transform, "Name", consumables.ItemName(item), 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
            row.name.enableAutoSizing = true; row.name.fontSizeMax = 32f; row.name.fontSizeMin = 14f;
            Anchor(row.name.rectTransform, 0f, 0.55f, 1f, 1f, new Vector2(16f, 0f), new Vector2(-190f, -6f));

            row.cost = PixelUIKit.CreateText(Font, go.transform, "Cost", "", 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, Color.white);
            row.cost.richText = true;
            row.cost.enableAutoSizing = true; row.cost.fontSizeMax = 24f; row.cost.fontSizeMin = 11f;
            Anchor(row.cost.rectTransform, 0f, 0.05f, 1f, 0.55f, new Vector2(16f, 0f), new Vector2(-190f, 0f));

            row.owned = PixelUIKit.CreateText(Font, go.transform, "Owned", "", 20f, TextAlignmentOptions.MidlineRight, FontStyles.Normal, new Color(1f, 1f, 1f, 0.6f));
            Anchor(row.owned.rectTransform, 0.5f, 0.55f, 1f, 1f, new Vector2(0f, 0f), new Vector2(-190f, -6f));

            row.buy = PixelUIKit.CreateButton(Font, go.transform, "Buy", buyText, new Vector2(150f, 70f), new Color(0.2f, 0.55f, 0.3f, 1f), Color.white, 32f);
            row.buyImage = row.buy.GetComponent<Image>();
            row.buyLabel = row.buy.GetComponentInChildren<TMP_Text>();
            RectTransform br = row.buy.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
            br.anchoredPosition = new Vector2(-14f, 0f);
            int captured = item;
            row.buy.onClick.AddListener(() => Buy(captured));
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

    private RectTransform Panel(string name, Vector2 size, float rightOffset)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(canvasRoot.transform, false);
        g.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.97f);
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
