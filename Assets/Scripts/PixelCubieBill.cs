// ---- System notes ----
// PixelCubieBill.cs
//   CUBIE'S BILL - the progression spine (design: docs/GameDesign.md section 6 'Cubie's bill - the progression spine', agreed 2026-10-23).
//   Cubie owns the only shop in existence and the player owes him for everything: ONE huge bill per PURE colour (White, Gray, Black, Red,
//   Green, Blue; billPerColour, shown as an absurd 25,000,000,000 each - the joke). He takes money only. The player pays any amount at any
//   time in the Bill window (PixelCubieBillWindow: one row per colour, the bar shows progress to the NEXT milestone, not to the absurd total).
//   - MILESTONES (an Inspector list, filled with the built-in ones when empty): an amount PER COLOUR (25K / 2.5M / 250M / 25B, spaced x100
//     because income grows exponentially), an optional light condition (a skill level), what it opens, and Cubie's lines. Reaching
//     milestone n = TRUST TIER n: CurrentTier is the ONE query everything asks; TierUnlocked(tier) is raised once per tier (Cubie talks:
//     PixelGuideVendor.RequestTalk; the event log announces it). The last milestone = the bill is settled (prestige later, not built).
//   - TRUST = MACHINES AND STOCK ONLY (decided 2026-10-24): every skill pack is on sale from Tier 0 (gathering skills in the first hour; making /
//     operation skills open by their input skills' levels, not by trust), and trust NEVER caps a skill's level.
//   - TRUST OPENS HIS SHOP: itemStock (device kind -> tier) and packStock (a pack by its reward pixel type or skill id -> tier) are the ONE
//     table of what each tier puts on sale. The shop asks TierListed (hidden until the bill is revealed, so the early shop shows no
//     mystery locks) and TierMet (on sale) through its shared requirement checks (PixelShop.IsPackRequirementMet / IsRequirementMet,
//     PixelShopConsumables.ItemLocked); a locked row says 'Cubie's trust: Tier N'. lockShopByTier off = everything on sale.
//   - THE TAG AND THE REVEAL: until the bill is revealed every Recycler wears Cubie's paper price tag ('Property of Cubie.', device card:
//     'Terms: see reverse. Amount owed: ???'; PixelRecycler builds it while TagShown). The reveal is Cubie's visit 3, the reward of a goal
//     (PixelGoals rewardCubieVisit 3, built-in row 'billreveal'; without such a row: revealAfterMinutes of play after visit 2). He yanks
//     the tag, it unrolls into an absurd invoice, he explains the monopoly, and the Bill window opens (Reveal). With Cubie or tips off
//     the bill still arrives at the same point, quietly. Interest is only talk: the amounts never grow by themselves.
//   - The Log's Goals tab pins the next milestone (PixelLog.CollectGoals -> NextGoal). The shop's header has an 'IOU' button once revealed.
//   - PROGRESS (paid per colour by pixel type, tier, revealed) lives in PixelGameState.Current.cubieBill (GameState.cs, moved 2026-10-24): saved / loaded /
//     reset with it; it is in place at the START of a load, so Recyclers rebuilt during the load already see TagShown right. Dev Hub widget 'Cubie's bill': Reveal / next tier / reset.
// ---------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

public class PixelCubieBill : MonoBehaviour
{
    [Serializable]
    public class Milestone
    {
        [Tooltip("Short name of the trust tier this milestone opens (shown in the Bill window and the Log's goal).")]
        public string name = "A regular";
        [Min(1f)]
        [Tooltip("How much of EACH pure colour must have been paid in total to reach it.")]
        public double perColour = 25000d;
        [Tooltip("Optional condition: this skill (id, e.g. standard) must also be at least conditionLevel. Empty = none.")]
        public string conditionSkillId = "";
        [Min(0)]
        [Tooltip("The level the condition skill needs.")]
        public int conditionLevel = 0;
        [TextArea(1, 3)]
        [Tooltip("What it opens, in one line (Bill window, Log goal).")]
        public string opens = "";
        [TextArea(2, 4)]
        [Tooltip("Cubie's pages when the tier opens (one speech box page each).")]
        public string[] cubieLines = new string[0];
    }

    [Serializable]
    public class ItemStock
    {
        [Tooltip("The shop item (a device / consumable kind).")]
        public PixelConsumables.DeviceKind kind = PixelConsumables.DeviceKind.ConveyorBelt;
        [Min(0)]
        [Tooltip("The trust tier that puts it on sale in Cubie's shop (0 = from the start).")]
        public int tier = 1;
    }

    [Serializable]
    public class PackStock
    {
        [Tooltip("Just a label for the Inspector (which pack this row means).")]
        public string label = "";
        [Tooltip("Matches a pack that makes this skill available (its Skill Id). Empty = match by the reward pixel type below.")]
        public string skillId = "";
        [Tooltip("Matches a pack that rewards this pixel type (when Skill Id is empty).")]
        public PixelClicker.PixelType rewardType = PixelClicker.PixelType.Berry;
        [Min(0)]
        [Tooltip("The trust tier that puts the pack on sale.")]
        public int tier = 1;
    }

    [Header("The bill")]
    [Tooltip("Turns the bill off: no reveal, no tiers, the shop sells everything as before.")]
    [SerializeField] private bool disableBill = false;
    [Tooltip("The colours Cubie takes (the pure currencies). One bill row each.")]
    [SerializeField] private PixelClicker.PixelType[] colours =
    {
        PixelClicker.PixelType.White, PixelClicker.PixelType.Gray, PixelClicker.PixelType.Black,
        PixelClicker.PixelType.Red, PixelClicker.PixelType.Green, PixelClicker.PixelType.Blue,
    };
    [Min(1f)]
    [Tooltip("The absurd total owed per colour (the joke on top of the Bill window). Paying stops here.")]
    [SerializeField] private double billPerColour = 25000000000d;
    [Tooltip("The milestones in order: reaching milestone n = trust tier n. An empty list is filled with the built-in four on start.")]
    [SerializeField] private List<Milestone> milestones = new List<Milestone>();

    [Header("The reveal")]
    [Min(0f)]
    [Tooltip("FALLBACK (only when no PixelGoals row grants Cubie's visit 3): the bill is revealed after this many minutes of play (and after visit 2).")]
    [SerializeField] private float revealAfterMinutes = 60f;
    [Tooltip("Event log line when the bill arrives without Cubie's visit (Cubie or first-time tips off).")]
    [SerializeField] private string quietRevealText = "Cubie left you a bill. Open it from the IOU button in the shop.";
    [Tooltip("Event log line when a trust tier opens. {0} = tier number, {1} = its name.")]
    [SerializeField] private string tierAnnounceFormat = "Cubie's trust: Tier {0} - {1}. New stock in his shop!";
    [Tooltip("Sound when a trust tier opens (a PixelAudio id).")]
    [SerializeField] private string tierSoundId = "achievement";
    [Tooltip("Sound when paying (a PixelAudio id).")]
    [SerializeField] private string paySoundId = "purchase";

    [Header("Cubie's stock per trust tier (the shop asks this table)")]
    [Tooltip("Off = everything is on sale whatever the tier (the table is ignored).")]
    [SerializeField] private bool lockShopByTier = true;
    [Tooltip("Shop items and the tier that puts each on sale. Items not listed are on sale from the start. An empty list is filled with the built-in draft.")]
    [SerializeField] private List<ItemStock> itemStock = new List<ItemStock>();
    [Tooltip("Shop packs and the tier that puts each on sale. Packs not listed are on sale from the start. An empty list is filled with the built-in draft. Never list SKILL packs here: every skill is open from Tier 0 (trust gates machines and stock, not skills).")]
    [SerializeField] private List<PackStock> packStock = new List<PackStock>();
    [Tooltip("Text on a locked shop row. {0} = the tier needed.")]
    [SerializeField] private string lockedFormat = "Cubie's trust: Tier {0}";

    public static PixelCubieBill Instance { get; private set; }

    /// <summary>A trust tier opened (raised once per tier, with the tier number).</summary>
    public static event Action<int> TierUnlocked;
    /// <summary>The bill was revealed (the Recyclers lose their tag).</summary>
    public static event Action Revealed;
    /// <summary>Something was paid (the Bill window and the Log's goal refresh).</summary>
    public static event Action Paid;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null; TierUnlocked = null; Revealed = null; Paid = null;
        PixelStatics.ResetOnRestart(() => { TierUnlocked = null; Revealed = null; Paid = null; });   // restart-reset-not-progress: listener lists of the old scene
    }

    private PixelClicker clicker;

    // ---- progress: lives in the GameState (PixelGameState.Current.cubieBill) ----
    private static CubieBillState S => PixelGameState.Current.cubieBill;
    private int tier { get => S.tier; set => S.tier = value; }
    private bool revealed { get => S.revealed; set => S.revealed = value; }

    private const string Visit3DueKey = "guide.visit3.due";

    // ---- the ONE query (shop, Log, window...) ----

    /// <summary>The player's trust tier (0 = no milestone yet). With the bill off: as high as it goes.</summary>
    public static int CurrentTier => Instance == null || Instance.disableBill ? int.MaxValue : Instance.tier;
    /// <summary>Has Cubie handed over the bill (the Bill window exists)?</summary>
    public static bool IsRevealed => Instance != null && !Instance.disableBill && Instance.revealed;
    /// <summary>Do the Recyclers still wear Cubie's price tag (until the reveal)?</summary>
    public static bool TagShown => Instance != null && !Instance.disableBill && !Instance.revealed;
    /// <summary>Is the reveal due now (its goal done, or the fallback time) and still to happen? PixelGuideVendor plays visit 3 then.</summary>
    public static bool RevealDue => Instance != null && Instance.RevealDueNow();

    /// <summary>Is a shop row with this tier on sale?</summary>
    public static bool TierMet(int needed) => needed <= 0 || Instance == null || Instance.disableBill || !Instance.lockShopByTier || needed <= Instance.tier;
    /// <summary>Is a shop row with this tier listed at all? (Before the reveal locked rows stay hidden, so the early shop shows no mystery.)</summary>
    public static bool TierListed(int needed) => TierMet(needed) || IsRevealed;
    /// <summary>The locked row's text ('Cubie's trust: Tier 2').</summary>
    public static string LockedText(int needed) => string.Format(Instance != null ? Instance.lockedFormat : "Cubie's trust: Tier {0}", needed);

    /// <summary>The tier that puts this shop item on sale (0 = from the start).</summary>
    public static int ItemTier(PixelConsumables.DeviceKind kind)
    {
        PixelCubieBill b = Instance;
        if (b == null || b.itemStock == null) return 0;
        int t = 0;
        for (int i = 0; i < b.itemStock.Count; i++)   // (a plain loop: asked for every shop row)
            if (b.itemStock[i] != null && b.itemStock[i].kind == kind) t = Mathf.Max(t, b.itemStock[i].tier);
        return t;
    }

    /// <summary>The tier that puts this shop pack on sale (0 = from the start).</summary>
    public static int PackTier(PixelShop.ShopPack pack)
    {
        PixelCubieBill b = Instance;
        if (b == null || b.packStock == null || pack == null) return 0;
        int t = 0;
        for (int i = 0; i < b.packStock.Count; i++)
        {
            PackStock s = b.packStock[i];
            if (s == null) continue;
            bool match = !string.IsNullOrEmpty(s.skillId) ? pack.skillId == s.skillId
                                                          : pack.rewardTiers != null && Array.Exists(pack.rewardTiers, r => r.type == s.rewardType);
            if (match) t = Mathf.Max(t, s.tier);
        }
        return t;
    }

    // ---- the bill ----

    public IReadOnlyList<PixelClicker.PixelType> Colours => colours;
    public IReadOnlyList<Milestone> Milestones => milestones;
    public double BillPerColour => billPerColour;
    public int Tier => tier;
    public double PaidOf(int colour) => colour >= 0 && colour < colours.Length ? S.PaidOf((int)colours[colour]) : 0d;
    /// <summary>The next milestone to reach (null = the bill is settled).</summary>
    public Milestone Next => milestones != null && tier < milestones.Count ? milestones[tier] : null;
    /// <summary>Is the next milestone's condition (a skill level) met?</summary>
    public bool ConditionMet(Milestone m) => m == null || string.IsNullOrEmpty(m.conditionSkillId) || m.conditionLevel <= 0 || PixelSkills.LevelOfId(m.conditionSkillId) >= m.conditionLevel;

    /// <summary>The Log's pinned goal: paid towards the next milestone over all colours (each capped at the milestone) and the target. False = none.</summary>
    public static bool NextGoal(out string title, out string description, out double have, out double need)
    {
        title = description = ""; have = need = 0d;
        PixelCubieBill b = Instance;
        if (!IsRevealed || b.Next == null) return false;
        Milestone m = b.Next;
        for (int i = 0; i < b.colours.Length; i++) have += Math.Min(b.PaidOf(i), m.perColour);
        need = m.perColour * b.colours.Length;
        title = "Cubie's trust: Tier " + (b.tier + 1);
        description = "Pay Cubie " + PixelClicker.FormatNumberShort(m.perColour) + " of every pure colour (IOU button in the shop)."
                    + (string.IsNullOrEmpty(m.conditionSkillId) || m.conditionLevel <= 0 ? "" : " Also: " + m.conditionSkillId + " level " + m.conditionLevel + ".")
                    + (string.IsNullOrEmpty(m.opens) ? "" : "\nOpens: " + m.opens);
        return true;
    }

    /// <summary>Pays Cubie up to 'amount' of a colour (row index): spends what the player holds, never more than the bill. Returns the amount paid.</summary>
    public double Pay(int colour, double amount)
    {
        if (disableBill || clicker == null || colour < 0 || colour >= colours.Length) return 0d;
        int t = clicker.IndexOf(colours[colour]);
        if (t < 0) return 0d;
        amount = Math.Floor(Math.Min(amount, Math.Min(clicker.Tiers[t].count, billPerColour - PaidOf(colour))));
        if (amount <= 0d || !clicker.TrySpend(t, amount)) return 0d;
        S.SetPaid((int)colours[colour], PaidOf(colour) + amount);
        PixelStats.Count("cubie.bill.paid", amount);
        if (!string.IsNullOrEmpty(paySoundId)) PixelAudio.Play(paySoundId);
        CheckTiers();
        Paid?.Invoke();
        return amount;
    }

    /// <summary>What is left of a colour's share of the NEXT milestone (0 = met or settled).</summary>
    public double LeftForNext(int colour) => Next != null ? Math.Max(0d, Next.perColour - PaidOf(colour)) : 0d;

    private void CheckTiers()
    {
        while (Next != null)
        {
            Milestone m = Next;
            for (int i = 0; i < colours.Length; i++) if (PaidOf(i) < m.perColour) return;
            if (!ConditionMet(m)) return;
            tier++;
            PixelHints.Announce(string.Format(tierAnnounceFormat, tier, m.name));
            if (!string.IsNullOrEmpty(tierSoundId)) PixelAudio.Play(tierSoundId);
            PixelSessionRecorder.Note("Cubie's trust: Tier " + tier);
            if (m.cubieLines != null && m.cubieLines.Length > 0) PixelGuideVendor.RequestTalk(m.cubieLines);
            TierUnlocked?.Invoke(tier);
        }
    }

    /// <summary>Cubie hands over the bill (the end of his visit 3, or quietly with Cubie / tips off): the tags come off, the Bill window opens.</summary>
    public static void Reveal(bool quiet)
    {
        PixelCubieBill b = Instance;
        if (b == null || b.disableBill || b.revealed) return;
        b.revealed = true;
        PixelStats.Best(Visit3DueKey, 1d);
        PixelRecycler.RemoveTags();
        PixelSessionRecorder.Note("Cubie's bill revealed");
        if (quiet) PixelHints.Announce(b.quietRevealText);
        Revealed?.Invoke();
        if (!quiet) PixelCubieBillWindow.Open();
    }

    private bool RevealDueNow()
    {
        if (disableBill || revealed || !PixelGuideVendor.SecondVisitDone) return false;
        if (PixelStats.Total(Visit3DueKey) >= 1d) return true;   // (its goal is done, or the Dev Hub asked)
        if (PixelGoals.GrantsVisit(3)) return false;
        PixelStats stats = PixelFind.Cached<PixelStats>();
        return stats != null && stats.PlaySeconds >= revealAfterMinutes * 60d;
    }

    // ---- built-in data ----

    private const string OpensTier1 = "belts, sorters, the Junction Box and other factory parts";
    private const string Tier1Line = "I've put a few more things on the shelf for you. Belts, sorters, proper machinery. Don't make me regret it.";

    private static List<Milestone> DefaultMilestones() => new List<Milestone>
    {
        new Milestone { name = "A regular", perColour = 25000d, opens = OpensTier1,
            cubieLines = new[] { "Well, look who pays their bills. A regular, then.", Tier1Line } },
        new Milestone { name = "Back room", perColour = 2500000d, opens = "generators, bigger upgrades, rarer stock",
            cubieLines = new[] { "Back room's open. Mind the boxes. They're... inventory.", "Generators, the bigger upgrades, the good stuff. Trusted customers only. That's you. For now." } },
        new Milestone { name = "Inner circle", perColour = 250000000d, opens = "premium stock",
            cubieLines = new[] { "Don't tell the others.", "This is the premium shelf. If anyone asks, you found it on the floor." } },
        new Milestone { name = "Settled", perColour = 25000000000d, opens = "the bill is paid off",
            cubieLines = new[] { "...You actually paid it. All of it.", "Honestly? I didn't think the number went that high either. We'll talk about what comes next." } },
    };

    /// <summary>The DRAFT of Cubie's stock per tier (to be approved / tuned; all Inspector values).</summary>
    private static List<ItemStock> DefaultItemStock() => new List<ItemStock>
    {
        // Tier 1 - factory parts
        new ItemStock { kind = PixelConsumables.DeviceKind.ConveyorBelt, tier = 1 },
        new ItemStock { kind = PixelConsumables.DeviceKind.BeltSorter, tier = 1 },
        new ItemStock { kind = PixelConsumables.DeviceKind.ConveyorElevator, tier = 1 },
        new ItemStock { kind = PixelConsumables.DeviceKind.BeltJump, tier = 1 },
        new ItemStock { kind = PixelConsumables.DeviceKind.ConveyorCombiner, tier = 1 },
        new ItemStock { kind = PixelConsumables.DeviceKind.JunctionBox, tier = 1 },
        // Tier 2 - power and bigger machines
        new ItemStock { kind = PixelConsumables.DeviceKind.BiomassGenerator, tier = 2 },
        new ItemStock { kind = PixelConsumables.DeviceKind.PocketBiomassGenerator, tier = 2 },
        new ItemStock { kind = PixelConsumables.DeviceKind.SolarPanel, tier = 2 },
        new ItemStock { kind = PixelConsumables.DeviceKind.PowerBank, tier = 2 },
        new ItemStock { kind = PixelConsumables.DeviceKind.QuantumOrganizer, tier = 2 },
    };

    private static List<PackStock> DefaultPackStock() => new List<PackStock>
    {
        // (NO skill packs here - decided 2026-10-24: every skill is open from Tier 0; trust gates machines and stock only, and never caps skill levels)
        // Tier 2 - bigger blocks
        new PackStock { label = "Fire Pixel Pack", rewardType = PixelClicker.PixelType.Fire, tier = 2 },
        new PackStock { label = "Nuclear Pixel Pack", rewardType = PixelClicker.PixelType.Nuclear, tier = 2 },
        // Tier 3 - rare hunting
        new PackStock { label = "Treasure Pixel Pack", rewardType = PixelClicker.PixelType.Treasure, tier = 3 },
    };

    [SerializeField, HideInInspector] private int stockDataVersion;   // 1 = skill packs taken out of the trust table (2026-10-24)

    private void EnsureDefaults()
    {
        if (stockDataVersion < 1)
        {
            // 2026-10-24: every skill is open from Tier 0 - the first draft put the gathering / Electrician packs at Tier 1; a saved list drops those rows.
            if (packStock != null)
                packStock.RemoveAll(r => r != null && (r.skillId == PixelSkills.ElectricianId ||
                    (string.IsNullOrEmpty(r.skillId) && (r.rewardType == PixelClicker.PixelType.Berry || r.rewardType == PixelClicker.PixelType.Fish ||
                                                         r.rewardType == PixelClicker.PixelType.Dirt || r.rewardType == PixelClicker.PixelType.Copper))));
            if (milestones != null && milestones.Count > 0 && milestones[0] != null && milestones[0].opens == "skill packs and factory parts")
            {
                milestones[0].opens = OpensTier1;
                if (milestones[0].cubieLines != null && milestones[0].cubieLines.Length > 1) milestones[0].cubieLines[1] = Tier1Line;
            }
            stockDataVersion = 1;
        }
        if (milestones == null || milestones.Count == 0) milestones = DefaultMilestones();
        if (itemStock == null || itemStock.Count == 0) itemStock = DefaultItemStock();
        if (packStock == null || packStock.Count == 0) packStock = DefaultPackStock();
    }


    private void Awake()
    {
        Instance = this;
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (colours == null || colours.Length == 0) colours = new[] { PixelClicker.PixelType.White, PixelClicker.PixelType.Gray, PixelClicker.PixelType.Black, PixelClicker.PixelType.Red, PixelClicker.PixelType.Green, PixelClicker.PixelType.Blue };
        EnsureDefaults();
        PixelGameState.Loaded += OnLoaded;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            int before = (milestones?.Count ?? 0) + (itemStock?.Count ?? 0) + (packStock?.Count ?? 0);
            EnsureDefaults();
            if (milestones.Count + itemStock.Count + packStock.Count != before) UnityEditor.EditorUtility.SetDirty(this);
        };
    }
#endif

    private void OnDestroy()
    {
        PixelGameState.Loaded -= OnLoaded;
        if (Instance == this) Instance = null;
    }

    private float checkTimer;

    private void Update()
    {
        if (clicker == null) { clicker = PixelFind.Cached<PixelClicker>(); if (clicker == null) return; }
        checkTimer -= Time.unscaledDeltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 1f;
        if (disableBill || PixelCalm.InMenu) return;
        if (revealed) CheckTiers();   // (a condition skill may level up after the money is in)
        else if (RevealDueNow() && !PixelGuideVendor.WillReveal) Reveal(true);   // (Cubie or tips off: the bill still arrives, quietly)
    }

    // A save was loaded: its bill is in the state now (from the START of the load, so rebuilt Recyclers saw the tag rule already).
    private void OnLoaded()
    {
        tier = Mathf.Clamp(tier, 0, milestones.Count);
        if (revealed) PixelRecycler.RemoveTags();
        Paid?.Invoke();
    }

    // ---- Dev Hub ('Cubie's bill' widget) ----

    /// <summary>Dev: reveal the bill now (with Cubie's visit when he can come, else quietly).</summary>
    public void DevReveal()
    {
        if (revealed) { PixelCubieBillWindow.Open(); return; }
        PixelStats.Best(Visit3DueKey, 1d);
        if (!PixelGuideVendor.WillReveal || !PixelGuideVendor.SecondVisitDone) Reveal(true);   // (else Cubie comes with his invoice at the next calm moment)
    }

    /// <summary>Dev: pays every colour up to the next milestone for free (ignores the condition) - the next tier opens.</summary>
    public void DevNextTier()
    {
        Milestone m = Next;
        if (m == null) return;
        revealed = true;
        for (int i = 0; i < colours.Length; i++) S.SetPaid((int)colours[i], Math.Max(PaidOf(i), m.perColour));
        string skill = m.conditionSkillId; int level = m.conditionLevel;
        m.conditionSkillId = "";   // (dev: skip the condition for this one step)
        CheckTiers();
        m.conditionSkillId = skill; m.conditionLevel = level;
        Paid?.Invoke();
    }

    /// <summary>Dev: back to no bill (nothing paid, tier 0, not revealed: the tags come back on new Recyclers).</summary>
    public void DevReset()
    {
        S.paid.Clear();
        tier = 0;
        revealed = false;
        PixelStats.Clear(Visit3DueKey);
        Paid?.Invoke();
    }
}
