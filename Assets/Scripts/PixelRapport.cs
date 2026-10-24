// ---- System notes ----
// PixelRapport.cs
//   VENDOR RAPPORT, the warm side of the economy (docs/GameDesign.md 'Vendor rapport'; Cubie's trust is the cold side): one meter per vendor,
//   five steps (Stranger -> Customer -> Regular -> Friend -> Partner). Progress lives in GameState.rapport (RapportState), keyed by vendor id.
//   - ONE step table (steps, Inspector) for every vendor: the points to reach a step and what it gives -
//       priceBonus     better sale prices (PixelMarket.Quote multiplies by PriceFactor),
//       recoverySpeed  his market recovers faster (PixelMarket divides its half-life by it),
//       visitWait      more visits (x the wait between his visits),
//       extraWares / extraStock / rareChance   bigger / rarer stock (more kinds, more of each, x a rare ware's chance: the Farmer's Dragon Seed).
//   - EARNING (Earn, called by PixelVisitorMinigameRapport.cs): accepting a visit (visitPoints); DEALING - selling (by the clicks-worth sold,
//     specialty goods x specialtyWeight), buying a ware, a trade - at most dealPointsPerVisit a visit (dealing, not grinding); GIFTS from the
//     Sell view (a loved good lovedGiftPoints, any other giftPoints; only giftsPerVisit count a visit; a gift = giftClicks of effort).
//   - NO DECAY: a vendor you haven't dealt with for trickleAfterMinutes of play slowly loses points (tricklePerHour) but never drops below the
//     step he has reached (Entry.reached never falls), so a reward is never lost.
//   - Per-vendor parts are DATA (vendors, Inspector): his loves (gift goods) and one greeting line per step (added to his greeting).
//   - Shown: the step in his greeting box title, and on his Game Database Character entry. A step up is announced.
//   - Rapport never gates core progression (Cubie's bill stays the spine). The unique unlockable per vendor is still a proposal (Todo.txt).
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Vendor rapport: one meter per vendor, five steps, one table of what each step gives.</summary>
public class PixelRapport : MonoBehaviour
{
    /// <summary>One step of the meter and what it gives.</summary>
    [Serializable]
    public class Step
    {
        [Tooltip("The step's name (shown in the greeting box and the Game Database).")]
        public string name = "Stranger";
        [Min(0f)]
        [Tooltip("Rapport points needed to reach this step.")]
        public float points;
        [Range(0f, 1f)]
        [Tooltip("Extra on what he pays when you sell to him (0.1 = +10%).")]
        public float priceBonus;
        [Min(1f)]
        [Tooltip("His market recovers this many times faster after you sell to him (PixelMarket's half-life is divided by it).")]
        public float recoverySpeed = 1f;
        [Range(0.3f, 1f)]
        [Tooltip("x the wait between his visits (0.8 = he comes 20% sooner).")]
        public float visitWait = 1f;
        [Min(0)]
        [Tooltip("More kinds of wares he brings (vendors who bring a few at random: Wizard, Tinkerer, Farmer).")]
        public int extraWares;
        [Min(0)]
        [Tooltip("More of each ware he brings.")]
        public int extraStock;
        [Min(1f)]
        [Tooltip("x his chance to bring a rare ware (the Farmer's Dragon Seed).")]
        public float rareChance = 1f;
    }

    /// <summary>A good a vendor loves as a gift.</summary>
    [Serializable]
    public class Love
    {
        [Tooltip("A pixel type name (Heartwood, Pearl, Gold...) or a material / minigame id (charcoal, ghost = Ectoplasm, eviltree = Magic Wood, stardust, meteor, bomb) - the ids PixelMaterials.CostHeld reads.")]
        public string goodId = "";
        [Min(0.1f)]
        [Tooltip("For a material: the clicks of effort one unit stands for (pixel types use their own value). Sets how many make one gift.")]
        public float effortClicksPerUnit = 5f;
    }

    /// <summary>One vendor's own parts: what he loves and what he says at each step.</summary>
    [Serializable]
    public class Vendor
    {
        [Tooltip("The vendor's id (wizard, lumberjack, tinkerer, prospector, forager, angler, farmer, entrepreneur, egg, bean).")]
        public string vendorId = "";
        [Tooltip("Goods he loves as a gift (they count much more than other gifts).")]
        public List<Love> loves = new List<Love>();
        [Tooltip("One line per step, Stranger first, added to his greeting. Empty = nothing extra.")]
        public string[] stepLines = new string[0];
    }

    [Header("Steps (one table for every vendor)")]
    [Tooltip("The five steps, lowest first. An empty list is filled with the built-in Stranger / Customer / Regular / Friend / Partner.")]
    [SerializeField] private List<Step> steps = new List<Step>();

    [Header("Earning rapport")]
    [Min(0f)]
    [Tooltip("Points for opening a vendor's visit (clicking him when he peeks in).")]
    [SerializeField] private float visitPoints = 2f;
    [Min(1f)]
    [Tooltip("Selling this many clicks' worth of goods earns 1 point.")]
    [SerializeField] private float saleClicksPerPoint = 150f;
    [Min(1f)]
    [Tooltip("Selling him one of his specialty goods counts this many times more.")]
    [SerializeField] private float specialtyWeight = 2f;
    [Min(0f)]
    [Tooltip("Points for buying one of his wares.")]
    [SerializeField] private float purchasePoints = 1f;
    [Min(0f)]
    [Tooltip("Points for a trade with him.")]
    [SerializeField] private float tradePoints = 2f;
    [Min(0f)]
    [Tooltip("Most points DEALING (selling, buying, trading) earns in one visit, so dumping a stack doesn't buy friendship.")]
    [SerializeField] private float dealPointsPerVisit = 8f;

    [Header("Gifts (Sell view)")]
    [Min(1f)]
    [Tooltip("A gift is this many clicks' worth of the good (at least 1).")]
    [SerializeField] private float giftClicks = 120f;
    [Min(0f)]
    [Tooltip("Points for a gift he loves.")]
    [SerializeField] private float lovedGiftPoints = 5f;
    [Min(0f)]
    [Tooltip("Points for any other gift.")]
    [SerializeField] private float giftPoints = 1f;
    [Min(1)]
    [Tooltip("Only this many gifts count a visit (he won't take more).")]
    [SerializeField] private int giftsPerVisit = 3;

    [Header("Trickle (no decay below a step)")]
    [Min(1f)]
    [Tooltip("Play minutes without dealing with a vendor before his rapport starts to trickle down.")]
    [SerializeField] private float trickleAfterMinutes = 60f;
    [Min(0f)]
    [Tooltip("Points lost per play hour after that - never below the step he has reached.")]
    [SerializeField] private float tricklePerHour = 4f;

    [Header("Vendors (their own parts)")]
    [Tooltip("Each vendor's loves and step lines. Missing built-in vendors are added back at start.")]
    [SerializeField] private List<Vendor> vendors = new List<Vendor>();

    private static RapportState R => PixelGameState.Current.rapport;

    /// <summary>The scene's rapport system (null if there is none).</summary>
    public static PixelRapport Instance => PixelFind.Cached<PixelRapport>();

    public float GiftClicks => giftClicks;
    public int GiftsPerVisit => giftsPerVisit;
    public float DealPointsPerVisit => dealPointsPerVisit;

    private bool ensured;   // (the built-in rows are checked once, not on every lookup)

    private void Awake() => EnsureDefaults();
    private void OnValidate() { ensured = false; EnsureDefaults(); }

    // ------------------------------------------------------------------
    // Built-in table + vendor data
    // ------------------------------------------------------------------

    private void EnsureDefaults()
    {
        if (ensured && steps != null && steps.Count > 0 && vendors != null) return;
        ensured = true;
        if (steps == null) steps = new List<Step>();
        if (steps.Count == 0)
        {
            steps.Add(new Step { name = "Stranger", points = 0f });
            steps.Add(new Step { name = "Customer", points = 10f, priceBonus = 0.03f, recoverySpeed = 1.15f, visitWait = 0.95f });
            steps.Add(new Step { name = "Regular", points = 35f, priceBonus = 0.06f, recoverySpeed = 1.3f, visitWait = 0.9f, extraWares = 1, rareChance = 1.25f });
            steps.Add(new Step { name = "Friend", points = 80f, priceBonus = 0.1f, recoverySpeed = 1.6f, visitWait = 0.8f, extraWares = 1, extraStock = 1, rareChance = 1.5f });
            steps.Add(new Step { name = "Partner", points = 150f, priceBonus = 0.15f, recoverySpeed = 2f, visitWait = 0.7f, extraWares = 2, extraStock = 2, rareChance = 2f });
        }
        if (vendors == null) vendors = new List<Vendor>();
        foreach (Vendor d in BuiltInVendors())
            if (!vendors.Exists(v => v != null && v.vendorId == d.vendorId)) vendors.Add(d);
    }

    private static Love L(string id, float effort = 5f) => new Love { goodId = id, effortClicksPerUnit = effort };

    /// <summary>The built-in loves and step lines (Stranger, Customer, Regular, Friend, Partner). At Partner they let slip something about Cubie.</summary>
    private static Vendor[] BuiltInVendors() => new[]
    {
        new Vendor { vendorId = "lumberjack", loves = new List<Love> { L("Heartwood"), L("eviltree") }, stepLines = new[] { "",
            "Back again? Good. I like a steady hand.", "My regular! I saved you the good logs.", "Pull up a stump, friend. Coffee's on the fire.",
            "Partner, between us: Cubie once tried to sell me a tree. It was MY tree. He'd painted it teal." } },
        new Vendor { vendorId = "wizard", loves = new List<Love> { L("ghost", 6f), L("stardust", 20f) }, stepLines = new[] { "",
            "Ah, you again. The stars said you'd come.", "My favourite customer! The stars agree.", "A friend of the arcane is a friend of mine.",
            "Partner, a secret: Cubie wasn't always a cube. Don't ask what he was. I still have nightmares about the corners." } },
        new Vendor { vendorId = "tinkerer", loves = new List<Love> { L("bomb", 10f), L("charcoal", 4f) }, stepLines = new[] { "",
            "Oh! You're the one with the clicking. Hello again.", "Regular customer! I tightened every bolt twice for you.", "Friend! I've got blueprints I don't show just anyone.",
            "Partner... Cubie's bill machine? I built it. He said it was a 'friendship counter'." } },
        new Vendor { vendorId = "prospector", loves = new List<Love> { L("Gold"), L("meteor", 1.5f) }, stepLines = new[] { "",
            "You again! Still digging, I hope.", "My regular! I polished the good pickaxe for you.", "Friend, I'll tell you where the good seams are. Some of them.",
            "Partner, I've seen Cubie's basement. It goes down further than any mine. Further than it should." } },
        new Vendor { vendorId = "forager", loves = new List<Love> { L("Truffle") }, stepLines = new[] { "",
            "Oh, hello again! The mushrooms remember you.", "A regular! I picked an extra basket.", "Friend, I'll show you my secret patch. Well, its neighbour.",
            "Partner: Cubie came into the woods once. Every mushroom closed. They know something." } },
        new Vendor { vendorId = "angler", loves = new List<Love> { L("Pearl") }, stepLines = new[] { "",
            "Ahoy again.", "A regular on my dock! Rare as a calm sea.", "Friend, sit. I'll tell you about the one that got away.",
            "Partner: Cubie can't swim. Cubes sink. He tried to bill the sea for it." } },
        new Vendor { vendorId = "farmer", loves = new List<Love> { L("GoldenFruit") }, stepLines = new[] { "",
            "Back for more seeds? Good soil, you've got.", "A regular! I set some seeds aside.", "Friend, come by the farm any time. Mind the shotgun.",
            "Partner: Cubie's bill grows faster than my wheat. I've seen him water it." } },
        new Vendor { vendorId = "entrepreneur", stepLines = new[] { "",
            "A returning client. Excellent.", "A regular! I'll put in a word for you. With myself.", "A friend in business is worth two in the ledger.",
            "Partner, off the record: Cubie's 'only shop' was mine first. I sold it to him. For one pixel. I'm still bitter." } },
        new Vendor { vendorId = "egg", stepLines = new[] { "", "Mrrp?", "Mrrrp! (She remembers you.)", "Purrrr. (She sits on your foot.)", "Mrrow. (She glares at Cubie's shop. She knows.)" } },
        new Vendor { vendorId = "bean", stepLines = new[] { "", "Mew?", "Mew! (He brought his best bean.)", "Prrrt. (He headbutts your hand.)", "Hsss... (at Cubie's shop, not you.)" } },
    };

    private Vendor DataOf(string vendorId)
    {
        EnsureDefaults();
        return vendors.Find(v => v != null && v.vendorId == vendorId);
    }

    /// <summary>The goods this vendor loves (empty when none).</summary>
    public List<Love> LovesOf(string vendorId)
    {
        Vendor v = DataOf(vendorId);
        return v != null && v.loves != null ? v.loves.FindAll(l => l != null && !string.IsNullOrEmpty(l.goodId)) : new List<Love>();
    }

    /// <summary>Does he love this good? (A pixel type name or a material id, as in his loves list.)</summary>
    public bool Loves(string vendorId, string goodId)
    {
        Vendor v = DataOf(vendorId);
        return v != null && v.loves != null && v.loves.Exists(l => l != null && l.goodId == goodId);
    }

    /// <summary>His line for the step he is at ("" = none).</summary>
    public string StepLine(string vendorId)
    {
        Vendor v = DataOf(vendorId);
        int s = StepIndex(vendorId);
        return v != null && v.stepLines != null && s < v.stepLines.Length && v.stepLines[s] != null ? v.stepLines[s] : "";
    }

    // ------------------------------------------------------------------
    // The meter
    // ------------------------------------------------------------------

    private static double PlaySeconds { get { PixelStats s = PixelFind.Cached<PixelStats>(); return s != null ? s.PlaySeconds : Time.time; } }

    /// <summary>His entry with the trickle applied up to now (null for a vendor you've never dealt with, when not creating).</summary>
    private RapportState.Entry Entry(string vendorId, bool create)
    {
        if (string.IsNullOrEmpty(vendorId)) return null;
        RapportState.Entry e = R.Get(vendorId, create);
        if (e != null) Trickle(e);
        return e;
    }

    /// <summary>An ignored vendor slowly loses points, never below the floor of the step he has reached.</summary>
    private void Trickle(RapportState.Entry e)
    {
        double now = PlaySeconds;
        double from = System.Math.Max(e.lastDeal + trickleAfterMinutes * 60d, e.lastTrickle);
        if (now <= from) return;
        double floor = StepAt(e.reached).points;
        if (e.points > floor) e.points = System.Math.Max(floor, e.points - (now - from) / 3600d * tricklePerHour);
        e.lastTrickle = now;
    }

    private Step StepAt(int index)
    {
        EnsureDefaults();
        return steps[Mathf.Clamp(index, 0, steps.Count - 1)];
    }

    /// <summary>The step he is at (0 = Stranger). It never drops.</summary>
    public int StepIndex(string vendorId)
    {
        EnsureDefaults();
        RapportState.Entry e = R.Get(vendorId, false);
        return e != null ? Mathf.Clamp(e.reached, 0, steps.Count - 1) : 0;
    }

    public Step StepOf(string vendorId) => StepAt(StepIndex(vendorId));

    /// <summary>His rapport points right now (the trickle applied).</summary>
    public double Points(string vendorId) { RapportState.Entry e = Entry(vendorId, false); return e != null ? e.points : 0d; }

    /// <summary>How many steps there are, and the points each one needs (for the meter's ticks).</summary>
    public int StepCount { get { EnsureDefaults(); return steps.Count; } }
    public float StepPoints(int index) => StepAt(index).points;
    public string StepNameAt(int index) => StepAt(index).name;
    public string StepName(string vendorId) => StepOf(vendorId).name;

    /// <summary>"Regular (42 / 80 to Friend)", or "Partner" at the top.</summary>
    public string ProgressText(string vendorId)
    {
        RapportState.Entry e = Entry(vendorId, false);
        int s = StepIndex(vendorId);
        if (s >= steps.Count - 1) return StepAt(s).name;
        Step next = StepAt(s + 1);
        return StepAt(s).name + " (" + Mathf.FloorToInt((float)(e != null ? e.points : 0d)) + " / " + Mathf.CeilToInt(next.points) + " to " + next.name + ")";
    }

    // ---- What the step gives (read by PixelMarket and the visitors) ----

    public float PriceFactor(string vendorId) => 1f + StepOf(vendorId).priceBonus;
    public float RecoverySpeed(string vendorId) => Mathf.Max(1f, StepOf(vendorId).recoverySpeed);
    public float VisitWait(string vendorId) => Mathf.Clamp(StepOf(vendorId).visitWait, 0.3f, 1f);
    public int ExtraWares(string vendorId) => Mathf.Max(0, StepOf(vendorId).extraWares);
    public int ExtraStock(string vendorId) => Mathf.Max(0, StepOf(vendorId).extraStock);
    public float RareChance(string vendorId) => Mathf.Max(1f, StepOf(vendorId).rareChance);

    // ---- Points (the visitor caps the per-visit ones) ----

    public float VisitPoints => visitPoints;
    public float PurchasePoints => purchasePoints;
    public float TradePoints => tradePoints;
    public double SalePoints(double clicksWorth, bool specialty) => System.Math.Max(0d, clicksWorth) / saleClicksPerPoint * (specialty ? specialtyWeight : 1f);
    public float GiftPoints(bool loved) => loved ? lovedGiftPoints : giftPoints;

    /// <summary>Adds rapport with a vendor (any source). Announces a new step. 'name' = his display name for the announcement.</summary>
    public void Earn(string vendorId, string name, double points)
    {
        RapportState.Entry e = Entry(vendorId, true);
        if (e == null) return;
        e.lastDeal = PlaySeconds;
        e.lastTrickle = e.lastDeal;
        if (points <= 0d) return;
        e.points += points;
        int before = e.reached;
        while (e.reached < steps.Count - 1 && e.points >= StepAt(e.reached + 1).points) e.reached++;
        if (e.reached <= before) return;
        Step s = StepAt(e.reached);
        PixelHints.Announce(name + " now sees you as a " + s.name + "! " + RewardText(s));
        PixelAudio.Play("achievement");
    }

    /// <summary>What a step gives, in one plain line.</summary>
    public static string RewardText(Step s)
    {
        List<string> parts = new List<string>();
        if (s.priceBonus > 0f) parts.Add("+" + Mathf.RoundToInt(s.priceBonus * 100f) + "% when you sell");
        if (s.recoverySpeed > 1f) parts.Add("his market recovers " + s.recoverySpeed.ToString("0.##") + "x faster");
        if (s.visitWait < 1f) parts.Add("visits " + Mathf.RoundToInt((1f - s.visitWait) * 100f) + "% more often");
        if (s.extraWares > 0 || s.extraStock > 0 || s.rareChance > 1f) parts.Add("bigger, rarer stock");
        return parts.Count > 0 ? string.Join(", ", parts) + "." : "";
    }
}
