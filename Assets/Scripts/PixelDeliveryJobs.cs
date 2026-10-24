// ---- System notes ----
// PixelDeliveryJobs.cs
//   DELIVERY JOBS (2026-10-24, Devices session; design: docs/systems/PixelRules.md 'Delivery jobs'): vendors buy HUSKS. A husk never pays by
//   itself; a vendor buying husks in a job is a trade (like selling skill goods), limited by balance: one running job per vendor, at most
//   jobsPerHour jobs taken per play hour.
//   - DOCKS: every vendor has his own FIXED spot at the floor's edge (the 'docks' table: an angle round the floor's centre, at rimFraction of the
//     floor's half width). The player can't move it - the point is to route husks to it, and a line built to Big Tim's dock pays off on every later
//     job. A faint DOCK MARKER (a disc + ring in his colour with his initial) shows on the floor once he has POSTED a job (offered one at a visit),
//     so lines can be planned before accepting.
//   - THE OFFER (PixelVisitorMinigameJobs.cs): during a visit, beside his wares / Sell view, a 'Delivery job' button opens the offer (RollOffer):
//     husks of HIS skill (his dock's huskTypes, only unlocked types), a SIZE from the sizes table (bigger / mixed ones need Cubie's trust tier and
//     his rapport step; weights pick among the allowed ones), the time limit, the pay and maybe a reward item (one of his wares).
//   - PAY (fixed when offered): pure currency = PixelMarket.JobQuote (husksRate x what a click on pure blocks earns x the clicks behind each husk,
//     x his rapport price bonus), in the unlocked pure colour you hold least of; + RAPPORT (the size's points: the biggest rapport source) + sometimes
//     an ITEM. Expired: no penalty; the husks pop back out and he leaves a share of the rapport (expiredRapportShare x how full the crate was).
//   - ACCEPTED: the job goes into GameState.delivery (DeliveryState) and his DELIVERY CRATE (PixelDeliveryCrate) drops onto his dock. The crate is
//     only a view of the job: this component rebuilds missing crates (after a load) every check, so the state is the one truth.
//   - FILLED: the crate seals and floats away on a balloon (PixelDeliveryCrate.Haul); the pay is given here (Complete).
//   - Shown in the Log's Goals tab (AddGoalLines): one row per running job with the vendor, the dock and the time left.
//   Texts: section [Delivery jobs] of Dialogue.txt (PixelDialogue); each vendor's own 'job offer' line is in his [Visitor: id] section.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Delivery jobs: vendors post jobs for husks; the crate sits on their fixed dock at the floor's edge.</summary>
public class PixelDeliveryJobs : MonoBehaviour
{
    /// <summary>One vendor's dock and the husks his jobs ask for.</summary>
    [Serializable]
    public class Dock
    {
        [Tooltip("The vendor's id (lumberjack, prospector, farmer, forager, angler, wizard, tinkerer, entrepreneur).")]
        public string vendorId = "";
        [Range(0f, 360f)]
        [Tooltip("Where his dock is: degrees round the floor's centre, clockwise from the far side (+Z). Never changes during play.")]
        public float angle;
        [Tooltip("His colour (the dock marker and the crate's trim).")]
        public Color color = Color.white;
        [Tooltip("The letter on his dock marker.")]
        public string initial = "?";
        [Tooltip("The husk types his jobs ask for, his skill's blocks (PixelType names, comma separated). Only unlocked ones are asked for; the first ones are asked for most.")]
        public string huskTypes = "";
    }

    /// <summary>A job size: how many husk types, how many of each, the time limit and the rapport.</summary>
    [Serializable]
    public class Size
    {
        [Tooltip("Its name (only shown in the Inspector and the Game Database).")]
        public string name = "Small";
        [Tooltip("How many of each husk type, one entry per type (2 entries = a mixed job of 2 types).")]
        public int[] amounts = { 15 };
        [Min(1f)]
        [Tooltip("The time limit, in play minutes.")]
        public float minutes = 8f;
        [Min(0f)]
        [Tooltip("Rapport points when the crate is filled (the biggest rapport source: a visit's deals give 8 at most).")]
        public float rapportPoints = 8f;
        [Range(0f, 1f)]
        [Tooltip("Chance he adds one of his wares as a reward.")]
        public float itemChance = 0.15f;
        [Min(0)]
        [Tooltip("Cubie's trust tier needed (belts open at tier 1: bigger jobs only after that).")]
        public int minTrustTier;
        [Min(0)]
        [Tooltip("His rapport step needed (0 Stranger, 1 Customer, 2 Regular, 3 Friend, 4 Partner).")]
        public int minRapportStep;
        [Min(0f)]
        [Tooltip("How likely this size is, among the sizes allowed right now.")]
        public float weight = 1f;
    }

    [Header("Docks (one per vendor; fixed spots at the floor's edge)")]
    [Tooltip("Each vendor's dock and the husks he asks for. Missing built-in vendors are added back at start.")]
    [SerializeField] private List<Dock> docks = new List<Dock>();
    [Range(0.3f, 1f)]
    [Tooltip("How far out the docks are: this fraction of the floor's half width (1 = right at the edge).")]
    [SerializeField] private float rimFraction = 0.82f;
    [Range(0f, 1f)]
    [Tooltip("How see-through a dock marker is (0 = invisible).")]
    [SerializeField] private float markerOpacity = 0.28f;
    [Min(0.5f)]
    [Tooltip("Radius of a dock marker, in husk widths.")]
    [SerializeField] private float markerRadiusHusks = 2.6f;

    [Header("Job sizes (the first allowed one is the small, early job)")]
    [Tooltip("The sizes a job can have. Built-in ones are added back at start when the list is empty.")]
    [SerializeField] private List<Size> sizes = new List<Size>();

    [Header("Limits")]
    [Min(1)]
    [Tooltip("At most this many jobs taken per play hour (all vendors together). Each vendor has one running job at most.")]
    [SerializeField] private int jobsPerHour = 3;
    [Range(0f, 1f)]
    [Tooltip("Chance a vendor has a job to offer at a visit (when the limits allow one).")]
    [SerializeField] private float offerChance = 0.8f;

    [Header("Pay")]
    [Min(0.1f)]
    [Tooltip("Husks in a job pay this many times what the clicks behind them would earn on pure blocks (selling the goods pays 1.2x; products in a crate will be ~3x later).")]
    [SerializeField] private float husksRate = 2f;
    [Range(0f, 1f)]
    [Tooltip("Out of time: he still gives this share of the rapport, times how full the crate was.")]
    [SerializeField] private float expiredRapportShare = 0.5f;

    [Header("Timing")]
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of the running jobs (time limits, crates to rebuild).")]
    [SerializeField] private float checkSeconds = 0.5f;

    [Header("Texts ({0} {1} {2} as noted; Dialogue.txt [Delivery jobs] wins)")]
    [Tooltip("When a job is accepted. {0} = the vendor, {1} = what to deliver, {2} = the time limit.")]
    [SerializeField] private string acceptedText = "{0} dropped a crate on his dock. Fill it with {1} within {2}!";
    [Tooltip("When the crate is full. {0} = the vendor, {1} = the pay.")]
    [SerializeField] private string doneText = "{0} hauls his crate away: {1}!";
    [Tooltip("When time runs out. {0} = the vendor.")]
    [SerializeField] private string expiredText = "{0}'s delivery job ran out. The husks tumble back out of his crate - no hard feelings.";
    [Tooltip("The Goals tab's title for a running job. {0} = the vendor.")]
    [SerializeField] private string goalTitle = "Delivery for {0}";
    [Tooltip("The Goals tab's line under it. {0} = what is still needed, {1} = the time left.")]
    [SerializeField] private string goalText = "Fill the crate on his dock: {0}. {1} left.";

    /// <summary>An offer made at a visit (not saved: it is rolled again next visit). Accept turns it into a running job.</summary>
    public sealed class Offer
    {
        public string vendor, vendorName;
        public int size;
        public readonly List<DeliveryState.Line> lines = new List<DeliveryState.Line>();
        public float seconds, rapport;
        public int payType;
        public double pay;
        public string rewardItem = "";
    }

    /// <summary>One row for the Log's Goals tab.</summary>
    public struct GoalLine { public string title, description; public double count, goal; public Color color; }

    private static DeliveryState D => PixelGameState.Current.delivery;
    public static PixelDeliveryJobs Instance => PixelFind.Cached<PixelDeliveryJobs>();

    private PixelClicker clicker;
    private float nextCheck;
    private readonly Dictionary<string, PixelDeliveryCrate> crates = new Dictionary<string, PixelDeliveryCrate>();
    private readonly Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();
    private static readonly List<string> scratch = new List<string>();

    public float HusksRate => husksRate;

    // ------------------------------------------------------------------
    // Set-up
    // ------------------------------------------------------------------

    private void Awake()
    {
        clicker = PixelFind.Cached<PixelClicker>();
        EnsureDefaults();
        PixelTextFile f = PixelDialogue.File;
        const string section = "Delivery jobs";
        f.Apply(section, "accepted", ref acceptedText);
        f.Apply(section, "done", ref doneText);
        f.Apply(section, "expired", ref expiredText);
        f.Apply(section, "goal title", ref goalTitle);
        f.Apply(section, "goal text", ref goalText);
        f.Flush();
        PixelGameState.Loaded += OnLoaded;
    }

    private void OnDestroy() { PixelGameState.Loaded -= OnLoaded; }

    private void OnValidate() { EnsureDefaults(); }

    /// <summary>The built-in docks (8 vendors, 45 degrees apart, none straight in front of / behind the cube) and sizes.</summary>
    private void EnsureDefaults()
    {
        AddDock("lumberjack", 22.5f, new Color(0.62f, 0.4f, 0.2f), "T", "Wood, Bark, Sap, Leaf, Heartwood");
        AddDock("prospector", 67.5f, new Color(0.75f, 0.72f, 0.66f), "P", "Rock, Copper, Tin, Iron, Silver, Gold");
        AddDock("farmer", 112.5f, new Color(0.95f, 0.8f, 0.3f), "F", "Wheat, Seed, Water, Fertilizer");
        AddDock("forager", 157.5f, new Color(0.75f, 0.3f, 0.55f), "H", "Berry, Mushroom, Herb, Truffle");
        AddDock("angler", 202.5f, new Color(0.3f, 0.6f, 0.95f), "S", "Fish, Seaweed, Shell, Pearl");
        AddDock("wizard", 247.5f, new Color(0.6f, 0.4f, 1f), "W", "Glass, Luminescent");
        AddDock("tinkerer", 292.5f, new Color(1f, 0.6f, 0.2f), "K", "Electric, Wire, Battery, Gray");
        AddDock("entrepreneur", 337.5f, new Color(0.35f, 0.85f, 0.55f), "E", "Red, Green, Blue");
        if (sizes.Count > 0) return;
        sizes.Add(new Size { name = "Small", amounts = new[] { 15 }, minutes = 8f, rapportPoints = 8f, itemChance = 0.1f, minTrustTier = 0, minRapportStep = 0, weight = 1f });
        sizes.Add(new Size { name = "Large", amounts = new[] { 30 }, minutes = 10f, rapportPoints = 12f, itemChance = 0.15f, minTrustTier = 1, minRapportStep = 0, weight = 1.2f });
        sizes.Add(new Size { name = "Mixed", amounts = new[] { 30, 10 }, minutes = 12f, rapportPoints = 16f, itemChance = 0.25f, minTrustTier = 1, minRapportStep = 2, weight = 1.5f });
        sizes.Add(new Size { name = "Big mixed", amounts = new[] { 40, 20, 10 }, minutes = 15f, rapportPoints = 22f, itemChance = 0.4f, minTrustTier = 2, minRapportStep = 3, weight = 1.5f });
    }

    private void AddDock(string id, float angle, Color color, string initial, string types)
    {
        foreach (Dock d in docks) if (d != null && d.vendorId == id) return;
        docks.Add(new Dock { vendorId = id, angle = angle, color = color, initial = initial, huskTypes = types });
    }

    private Dock DockOf(string vendor)
    {
        foreach (Dock d in docks) if (d != null && d.vendorId == vendor) return d;
        return null;
    }

    // ------------------------------------------------------------------
    // Where a dock is
    // ------------------------------------------------------------------

    /// <summary>A vendor's dock on the floor (its top surface) and the turn that faces it towards the floor's centre. False if he has no dock.</summary>
    public bool DockPoint(string vendor, out Vector3 point, out Quaternion facing)
    {
        point = Vector3.zero; facing = Quaternion.identity;
        Dock d = DockOf(vendor);
        if (d == null) return false;
        PixelFloor floor = PixelFloor.Instance;
        Vector3 centre = Vector3.zero;
        float half = 12f;
        if (floor != null && floor.FloorRenderer != null)
        {
            Bounds b = floor.FloorRenderer.bounds;
            centre = b.center;
            half = Mathf.Min(b.extents.x, b.extents.z);
        }
        Vector3 dir = Quaternion.Euler(0f, d.angle, 0f) * Vector3.forward;
        point = centre + dir * half * rimFraction;
        point.y = PixelConveyor.FloorY(clicker);
        facing = Quaternion.LookRotation(-dir, Vector3.up);   // (its front, with the In coupling, faces the centre)
        return true;
    }

    public Color DockColor(string vendor) { Dock d = DockOf(vendor); return d != null ? d.color : Color.white; }
    public bool HasDock(string vendor) => DockOf(vendor) != null;

    // ------------------------------------------------------------------
    // Offers (a visit)
    // ------------------------------------------------------------------

    private static double PlaySeconds => PixelGameState.Current.stats.playSeconds;

    /// <summary>Jobs taken in the last play hour (old stamps are dropped).</summary>
    private static int TakenThisHour()
    {
        double now = PlaySeconds;
        D.taken.RemoveAll(t => now - t > 3600d || t > now);
        return D.taken.Count;
    }

    /// <summary>His running job (null = none).</summary>
    public static DeliveryState.Job JobOf(string vendor) => D.Of(vendor);

    /// <summary>Can he post a job now? (He has a dock, no running job, and the hourly limit isn't reached.) 'why' says why not.</summary>
    public bool CanOffer(string vendor, out string why)
    {
        why = "";
        if (!HasDock(vendor)) { why = "He has no jobs."; return false; }
        if (D.Of(vendor) != null) { why = "His crate is still on his dock."; return false; }
        if (TakenThisHour() >= jobsPerHour) { why = "No more jobs this hour - come back later."; return false; }
        return true;
    }

    /// <summary>Rolls this visit's offer for a vendor (null when he has none: the limits, the chance, or no unlocked husk type of his).</summary>
    public Offer RollOffer(string vendor, string vendorName, Func<string> rollReward)
    {
        if (clicker == null || !CanOffer(vendor, out _) || UnityEngine.Random.value > offerChance) return null;
        List<int> types = UnlockedTypes(vendor);
        if (types.Count == 0) return null;
        int size = PickSize(vendor, types.Count);
        if (size < 0) return null;
        Size s = sizes[size];
        Offer o = new Offer { vendor = vendor, vendorName = vendorName, size = size, seconds = s.minutes * 60f, rapport = s.rapportPoints };
        // The types: the first ones listed are asked for most (each pick favours the front of what is left).
        List<int> left = new List<int>(types);
        PixelMarket market = PixelMarket.Instance;
        for (int i = 0; i < s.amounts.Length && left.Count > 0; i++)
        {
            int pick = Mathf.Min(left.Count - 1, Mathf.FloorToInt(Mathf.Pow(UnityEngine.Random.value, 1.8f) * left.Count));
            int tier = left[pick];
            left.RemoveAt(pick);
            int need = Mathf.Max(1, s.amounts[i]);
            o.lines.Add(new DeliveryState.Line { type = (int)clicker.Tiers[tier].type, need = need });
            if (market != null) o.pay += market.JobQuote(vendor, tier, need, husksRate);
        }
        o.pay = System.Math.Max(1d, System.Math.Round(o.pay));
        o.payType = (int)LeastHeldPure();
        if (rollReward != null && UnityEngine.Random.value < s.itemChance) o.rewardItem = rollReward() ?? "";
        if (!D.docks.Contains(vendor)) { D.docks.Add(vendor); RefreshMarkers(); }   // (he has posted a job: his dock shows from now on)
        return o;
    }

    /// <summary>His husk types that are unlocked now, as tier indices, in his list's order.</summary>
    private List<int> UnlockedTypes(string vendor)
    {
        List<int> list = new List<int>();
        Dock d = DockOf(vendor);
        if (d == null || string.IsNullOrEmpty(d.huskTypes)) return list;
        foreach (string part in d.huskTypes.Split(','))
        {
            if (!Enum.TryParse(part.Trim(), out PixelClicker.PixelType type)) continue;
            int tier = clicker.IndexOf(type);
            if (tier >= 0 && clicker.Tiers[tier].unlocked && !list.Contains(tier)) list.Add(tier);
        }
        return list;
    }

    /// <summary>A size allowed now (trust tier, his rapport step, enough husk types of his unlocked), by weight; -1 = none.</summary>
    private int PickSize(string vendor, int typesAvailable)
    {
        int trust = PixelCubieBill.CurrentTier;
        PixelRapport rapport = PixelRapport.Instance;
        int step = rapport != null ? rapport.StepIndex(vendor) : 0;
        float total = 0f;
        for (int i = 0; i < sizes.Count; i++) if (Allowed(i, trust, step, typesAvailable)) total += Mathf.Max(0f, sizes[i].weight);
        if (total <= 0f) return -1;
        float r = UnityEngine.Random.value * total;
        for (int i = 0; i < sizes.Count; i++)
        {
            if (!Allowed(i, trust, step, typesAvailable)) continue;
            r -= Mathf.Max(0f, sizes[i].weight);
            if (r <= 0f) return i;
        }
        for (int i = sizes.Count - 1; i >= 0; i--) if (Allowed(i, trust, step, typesAvailable)) return i;
        return -1;
    }

    private bool Allowed(int i, int trust, int step, int typesAvailable)
    {
        Size s = sizes[i];
        return s != null && s.amounts != null && s.amounts.Length > 0 && s.amounts.Length <= typesAvailable && trust >= s.minTrustTier && step >= s.minRapportStep;
    }

    /// <summary>The unlocked pure colour you hold least of (a job helps where you are short, e.g. Cubie's bill).</summary>
    private PixelClicker.PixelType LeastHeldPure()
    {
        PixelClicker.PixelType best = PixelClicker.PixelType.White;
        double least = double.MaxValue;
        foreach (PixelClicker.PixelTier t in clicker.Tiers)
            if (t != null && t.unlocked && PixelMarket.IsPure(t.type) && t.count < least) { least = t.count; best = t.type; }
        return best;
    }

    /// <summary>What a job asks for, in words ('30 Bark + 10 Sap husks').</summary>
    public string NeedText(IReadOnlyList<DeliveryState.Line> lines, bool remaining = false)
    {
        List<string> parts = new List<string>();
        foreach (DeliveryState.Line l in lines)
        {
            int n = remaining ? l.need - l.have : l.need;
            if (n <= 0) continue;
            parts.Add(n + " " + ColoredName(l.type));
        }
        return parts.Count == 0 ? "nothing" : string.Join(" + ", parts) + " husks";
    }

    /// <summary>A pixel type's name in its colour (rich text).</summary>
    public string ColoredName(int type)
    {
        int tier = clicker != null ? clicker.IndexOf((PixelClicker.PixelType)type) : -1;
        if (tier < 0) return ((PixelClicker.PixelType)type).ToString();
        PixelClicker.PixelTier t = clicker.Tiers[tier];
        return "<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(t.UIColor, Color.white, 0.25f)) + ">" + t.displayName + "</color>";
    }

    /// <summary>The pay in words ('4.2K Red + rapport + a Fan').</summary>
    public string PayText(double pay, int payType, string rewardItem)
    {
        string s = PixelClicker.FormatNumberShort(pay) + " " + ColoredName(payType) + " + rapport";
        if (!string.IsNullOrEmpty(rewardItem)) s += " + a " + rewardItem;
        return s;
    }

    public static string TimeText(double seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
        return (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>Accepts an offer: the job starts and his crate drops onto his dock.</summary>
    public bool Accept(Offer o)
    {
        if (o == null || !CanOffer(o.vendor, out _)) return false;
        DeliveryState.Job job = new DeliveryState.Job
        {
            vendor = o.vendor, size = o.size, started = PlaySeconds, seconds = o.seconds, payType = o.payType, pay = o.pay,
            rapport = o.rapport, rewardItem = o.rewardItem ?? "",
        };
        foreach (DeliveryState.Line l in o.lines) job.lines.Add(new DeliveryState.Line { type = l.type, need = l.need });
        D.jobs.Add(job);
        D.taken.Add(PlaySeconds);
        if (!D.docks.Contains(o.vendor)) D.docks.Add(o.vendor);
        RefreshMarkers();
        SpawnCrate(job, true);
        PixelStats.Count("delivery.accepted");
        PixelHints.Announce(string.Format(acceptedText, o.vendorName, NeedText(job.lines), TimeText(job.seconds)));
        return true;
    }

    // ------------------------------------------------------------------
    // Running jobs
    // ------------------------------------------------------------------

    /// <summary>Seconds left on a job.</summary>
    public static double TimeLeft(DeliveryState.Job job) => job == null ? 0d : System.Math.Max(0d, job.started + job.seconds - PlaySeconds);

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); return; }
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkSeconds;
        // Every running job has its crate (rebuilt after a load); a crate whose job is gone (a new game) goes.
        scratch.Clear();
        foreach (KeyValuePair<string, PixelDeliveryCrate> kv in crates) if (kv.Value == null || (D.Of(kv.Key) == null && !kv.Value.Leaving)) scratch.Add(kv.Key);
        foreach (string id in scratch) { if (crates[id] != null && !crates[id].Leaving) Destroy(crates[id].gameObject); crates.Remove(id); }
        for (int i = D.jobs.Count - 1; i >= 0; i--)
        {
            DeliveryState.Job job = D.jobs[i];
            if (!crates.TryGetValue(job.vendor, out PixelDeliveryCrate crate) || crate == null) crate = SpawnCrate(job, false);
            if (TimeLeft(job) <= 0d) Expire(job, crate);
        }
        if (markers.Count != D.docks.Count) RefreshMarkers();
        else foreach (KeyValuePair<string, GameObject> kv in markers) if (kv.Value != null && DockPoint(kv.Key, out Vector3 p, out _)) kv.Value.transform.position = p;   // (follows a resized floor)
    }

    private PixelDeliveryCrate SpawnCrate(DeliveryState.Job job, bool drop)
    {
        if (!DockPoint(job.vendor, out Vector3 point, out Quaternion facing)) return null;
        if (crates.TryGetValue(job.vendor, out PixelDeliveryCrate old) && old != null && !old.Leaving) Destroy(old.gameObject);
        PixelDeliveryCrate crate = PixelDeliveryCrate.Create(clicker, this, job, point, facing, DockColor(job.vendor), drop);
        crates[job.vendor] = crate;
        return crate;
    }

    /// <summary>The crate is full (PixelDeliveryCrate): he pays and hauls it away.</summary>
    public void Complete(DeliveryState.Job job)
    {
        if (job == null || !D.jobs.Contains(job)) return;
        D.jobs.Remove(job);
        string name = VendorName(job.vendor);
        int payTier = clicker.IndexOf((PixelClicker.PixelType)job.payType);
        if (payTier >= 0) clicker.AddCurrency(payTier, job.pay);
        PixelRapport rapport = PixelRapport.Instance;
        if (rapport != null) rapport.Earn(job.vendor, name, job.rapport);
        string reward = "";
        if (!string.IsNullOrEmpty(job.rewardItem) && GiveItem(job.rewardItem)) reward = job.rewardItem;
        PixelStats.Count("delivery.done");
        PixelAudio.Play("purchase");
        PixelHints.Announce(string.Format(doneText, name, PayText(job.pay, job.payType, reward)));
        if (crates.TryGetValue(job.vendor, out PixelDeliveryCrate crate) && crate != null) crate.Haul();
        crates.Remove(job.vendor);
    }

    /// <summary>Out of time: no penalty. The husks pop back out, a share of the rapport for how full the crate was, the crate goes.</summary>
    private void Expire(DeliveryState.Job job, PixelDeliveryCrate crate)
    {
        D.jobs.Remove(job);
        string name = VendorName(job.vendor);
        int need = 0, have = 0;
        foreach (DeliveryState.Line l in job.lines) { need += l.need; have += Mathf.Min(l.have, l.need); }
        PixelRapport rapport = PixelRapport.Instance;
        if (rapport != null && need > 0) rapport.Earn(job.vendor, name, job.rapport * expiredRapportShare * have / need);
        if (crate != null) crate.SpillAndGo();
        crates.Remove(job.vendor);
        PixelStats.Count("delivery.expired");
        PixelHints.Announce(string.Format(expiredText, name));
    }

    /// <summary>Adds one of the named consumable to the inventory (if there is room).</summary>
    private static bool GiveItem(string displayName)
    {
        PixelConsumables c = PixelFind.Cached<PixelConsumables>();
        if (c == null) return false;
        for (int i = 0; i < c.ItemCount; i++)
            if (c.ItemName(i) == displayName && c.ItemRoom(i) >= 1) { c.AddItem(i, 1); return true; }
        return false;
    }

    /// <summary>A vendor's display name (his visitor's name), or his id.</summary>
    public static string VendorName(string vendor)
    {
        foreach (PixelMinigame m in PixelMinigame.All)
            if (m is PixelVisitorMinigame v && m.Id == vendor) return v.VendorName;
        return vendor;
    }

    // ------------------------------------------------------------------
    // Dock markers
    // ------------------------------------------------------------------

    /// <summary>One faint marker per posted dock (a disc and a ring in his colour with his initial); rebuilt when the list changes.</summary>
    private void RefreshMarkers()
    {
        foreach (GameObject g in markers.Values) if (g != null) Destroy(g);
        markers.Clear();
        if (clicker == null) return;
        foreach (string vendor in D.docks)
        {
            Dock d = DockOf(vendor);
            if (d == null || markers.ContainsKey(vendor) || !DockPoint(vendor, out Vector3 point, out Quaternion facing)) continue;
            markers[vendor] = BuildMarker(d, point, facing);
        }
    }

    private GameObject BuildMarker(Dock d, Vector3 point, Quaternion facing)
    {
        float u = Mathf.Max(0.05f, clicker.OldPixelWorldSize), r = markerRadiusHusks * u;
        GameObject root = new GameObject("Dock: " + d.vendorId);
        root.transform.SetPositionAndRotation(point, facing);
        PixelFactoryKit k = new PixelFactoryKit(clicker, true, true) { alpha = markerOpacity };
        k.Cyl("Pad", root.transform, new Vector3(0f, 0.01f * u, 0f), r, 0.02f * u, d.color);
        LineRenderer ring = PixelFloorRing.Create("Rim", root.transform);
        if (ring != null) PixelFloorRing.Draw(ring, point + Vector3.up * 0.03f * u, r, 48, 1f, new Color(d.color.r, d.color.g, d.color.b, Mathf.Clamp01(markerOpacity * 2.4f)), 0.08f * u);
        GameObject t = new GameObject("Initial");
        t.transform.SetParent(root.transform, false);
        t.transform.localPosition = new Vector3(0f, 0.04f * u, -r * 0.55f);   // (on the side towards the floor's centre: readable from the cube)
        t.transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
        TMPro.TextMeshPro label = t.AddComponent<TMPro.TextMeshPro>();
        label.text = d.initial;
        label.fontSize = 6f * u / 0.5f;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = new Color(d.color.r, d.color.g, d.color.b, Mathf.Clamp01(markerOpacity * 2.4f));
        label.rectTransform.sizeDelta = new Vector2(4f, 2f);
        if (clicker.UIFont != null) label.font = clicker.UIFont;
        return root;
    }

    private void OnLoaded()
    {
        // The state was swapped: crates and markers are made again from it on the next check.
        foreach (PixelDeliveryCrate c in crates.Values) if (c != null && !c.Leaving) Destroy(c.gameObject);
        crates.Clear();
        RefreshMarkers();
        nextCheck = 0f;
    }

    // ------------------------------------------------------------------
    // The Log's Goals tab
    // ------------------------------------------------------------------

    /// <summary>One Goals row per running job: the vendor, what is still needed at his dock, the time left.</summary>
    public static void AddGoalLines(List<GoalLine> into)
    {
        PixelDeliveryJobs jobs = Instance;
        if (jobs == null) return;
        foreach (DeliveryState.Job job in D.jobs)
        {
            int need = 0, have = 0;
            foreach (DeliveryState.Line l in job.lines) { need += l.need; have += Mathf.Min(l.have, l.need); }
            into.Add(new GoalLine
            {
                title = string.Format(jobs.goalTitle, VendorName(job.vendor)),
                description = string.Format(jobs.goalText, jobs.NeedText(job.lines, true), TimeText(TimeLeft(job))),
                count = have, goal = System.Math.Max(1, need), color = jobs.DockColor(job.vendor),
            });
        }
    }
}
