// ---- System notes ----
// PixelRecycler.cs
//   TIMED + UPGRADES: a placed Recycler lasts durationSeconds (300 s; PixelRecyclerDevice counts down; hovering the bin shows a small floating DOWN ARROW - the
//   shared PixelHoverMarker, gone when the pointer leaves; the time left is on the shared device card, PixelDeviceCard),
//   it shrinks away with whatever is inside when time is up). Upgrades > Operation (2026-10-18: one of each): SPEED (-0.08 s of RecycleDelay a level;
//   3x that off the button cycle's shake) and SIZE (+0.3 to the size factor a level - the whole bin, its catching area and its capacity grow; Place sizeFactor = the device's
//   radius; a placed bin follows the level IN USE live, FollowSizeUpgrade - the device panel's - / + turns a bought level down). Size is +0.135 a level since
//   2026-10-18 (max 2.475, 25% smaller than with +0.3). (Run Time, Streak Power and Suction rows were removed; their stats still work at their base values.)
//   RECYCLER (PixelConsumables DeviceKind.Recycler 19, Utilities card - listed FIRST there - and Cubie's gift on visit 1 via the {recycler} page token): a
//   permanent open-topped bin (PixelChipperDevice stub for removal / saving its spot; built here by Place / CreateGhost / CreateDisplay with PixelFactoryKit:
//   plinth + feet, green walls with a dark liner, brushed-metal corner pillars and rim, a glowing band round the body and a status lamp - green idle,
//   amber while working - recycling arrows on the front, the shared carry handles and a big red button).
//   Drop old pixels (husks) into it - Pixel Grabbing, or anything that lands inside: a husk that comes to rest inside the bin's walls is CAPTURED
//   (PixelClicker.ReleaseOldPixel without credit, so it leaves the floor's old-pixel list: no despawn count, no cap, no vacuum, no termites; its
//   OldPixelDespawn is Held so it never shrinks away). The husks stay in the bin as real physical pixels until it is closed. Up to 'capacity' (10)
//   can be inside; a small RING above the bin fills as they go in (PixelWorldRing; amber when full) and the device card says 'n / 25'. Husks inside never despawn (their OldPixelDespawn.Held is re-set every tick, so poking one with
//   Pixel Grabbing cannot start its timer; one taken out of the bin is let go again; with the bin full, extra husks lying in it get KeepAlive). Click the
//   red BUTTON on its front: a slatted LID extends out of the inner back edge and STRETCHES over the opening (Lid Pivot z scale 0.02 -> 1, lidSeconds), the
//   bin SHAKES harder and harder for processSeconds (2 s, shakeStrength) while the band pulses amber, then every husk inside is recycled into
//   payoutShare (50%) of what a click on that block pays NOW (CurrentValue: base payout x the current Value / Ultra / wish multiplier, so Value upgrades
//   bought after the husk dropped count) as currency of its own type; the shake plays recycler_shake (PixelSynth.RecyclerShake: motor wind-up, faster rattles, clunk) (pixel rules: the Recycler is the one machine that turns husks back into
//   money, at a loss), the husks are destroyed and the lid pulls back. Nothing is captured while it works, and the shut lid (it has colliders) keeps
//   husks out: ones dropped on it fall in once it opens. ButtonHot (the pointer is over a button) is part of
//   PixelClicker.ExternalClickBlock and PixelGrab's guard, so pressing it neither clicks the cube nor grabs a pixel.
//   Captured husks that lie still for 0.3 s are frozen in place (kinematic + parented to the bin: Settle / Freeze), so the pile cannot jitter or bounce
//   and the husks scale with the bin when the floor's props fade (main menu, after a load: while the bin is scaled everything inside is frozen into it
//   and nothing is captured or let go). Grabbing one takes it OUT (TakeOut: unfrozen, unparented, back on the old-pixel list, so it is a normal husk again
//   and is captured anew if dropped back in). Husks above or in the opening get KeepAlive every check, and PixelGrab calls KeepIfOver on letting go, so an
//   old husk's timer cannot run out in the moment before the bin captures it (that used to make a dropped husk vanish uncounted). Loaded husks are put straight into the bin, frozen (ImportState).
//   SAVED: the husks inside go into PlacedState.extra ('Type,amount;', ExportState / ImportState via PixelConsumables) and are spawned back into the bin on
//   load. HANDLES: the Storage Crate's grip handles on both short ends (shared PixelCarryHandles: BuildParts in Build, the component added by Place):
//   drag one to carry the bin over the floor (scroll / Q / E turn it); everything inside is frozen into the bin while it is carried; not while processing.
//   BACK HATCH: a small door in the back wall (the wall is built in four pieces round the hole, metal frame, slatted door with a handle). In first
//   person, click it with the crosshair (within hatchReachHusks) and it slides up into the hole's top edge the same way the lid stretches
//   (Hatch Pivot y scale 1 = shut, 0.03 = open; aim at the hole to shut it again; the door and frame glow hatchHighlightColor while the crosshair is on it), so husks can be tossed in by hand. It shuts while the bin works.
//   MOVING: the carry handles move the bin through a kinematic Rigidbody (PixelCarryHandles), so loose husks are pushed instead of glitching through;
//   while carried every husk inside is frozen into the bin each frame and any that still ends up outside is let go and uncounted at once
//   (KeepContentsWhileCarried). A husk that leaves the bin is handed back to the floor's husk list (AdoptOldPixel), never left orphaned.
//   Pressing the button freezes every counted husk into the bin first (a still-settling one could be jostled out by the shake, let go and caught
//   again after the cycle) and POPS any UNCOUNTED husk lying in the bin (OVERFILLED past capacity) up and out over the front / back rim (EjectUncounted,
//   overfillPopSpeed; only the front with a hopper on the back), also again before paying.
//   AUTO RECYCLING (back on 2026-10-18 after a day of button-only): every husk that is captured is recycled on its own RecycleDelay (0.5 s; Operation:
//   Speed -0.08 s a level) later (RecycleDue from Capture, RecycleOne), with a same-type STREAK that lasts while husks keep coming (streakTimeout 6 s). The
//   red button is HIDDEN (showButton off; the lid / shake / pay-the-batch cycle and the overfill pop still work with autoRecycle off + showButton on).
//   CURRENCY ONLY (2026-10-17 rework, docs/GameDesign.md section 6 'Husk routing rework'): each husk pays payoutShare (50%) of what a click on it pays now
//   as BONUS currency (AddBonusCurrency: not towards the next tier's unlock), times the same-type STREAK within that batch (in the order the husks went
//   in: +streakStep 0.1 per same-type husk, a different type keeps streakKeepOnSwitch of it, cap streakMaxMultiplier 1.6), never more than
//   maxShareOfClick (95%) of a click; the device card shows the streak (FillDeviceInfo: inside, streak; the panel adds pay share, recycle delay, size and this session's husks / paid). NO skill XP (xpPerHusk 0).
//   SIZE (2026-10-18): placed 1.8x as wide and deep as the first bin by default (DefaultSize 1.8 = the Device's radius - 2 for a day, then 10% smaller;
//   Operation: Size adds +0.3 a level); it holds capacity (25) husks at that size, more by floor area when bigger (Capacity). A floating down arrow shows on hover (the time left is on the device card).
//   SEVERAL RECYCLERS: any number can stand at once; the shop price rises with every one held or standing (Device.priceGrowthPerOwned 1.8, PixelConsumables
//   Laddered). ONE OWNER PER HUSK: Capture, the suction and the suck-in only take husks PixelHuskHolder.Takeable says are free (a husk the Sweeper / hopper /
//   another bin has reserved or captured is left alone).
//   Stats recycler.husks / paid / xp, Best recycler.streak. The idea is that husks reach it WITHOUT the player stopping (bin placed where husks land; edge-click
//   aiming is planned). The button / capacity / lid cycle below is the old manual mode (autoRecycle off) and still works in auto mode for anything left inside.
//   A FULL bin makes its button flash and pulse (UpdateButtonFlash, flashColor). Removing the Recycler destroys what is inside. Not a belt acceptor yet.
//   SORTING HOPPER (2026-10-18 funnel version): a PixelSortingHopper stands behind the back wall (BackEdgeLocalZ, SizeFactor), occupying the hatch socket;
//   its feed pipe reaches over the back wall and drops its filter type into the bin, where Capture takes it as usual.
//   Capturing / letting go / freezing use the shared PixelHuskHolder steps.
//   FROM ABOVE ONLY (2026-10-18, suctionFromAboveOnly): the ring and the suck-in only take husks higher than the rim (falling / flying over it, up to
//   suctionHeightHusks); nothing lying beside the bin is drawn in. Each recycled husk shows a '+n' rising over it and adds to the currency's '+n' (PixelUI.ShowWorldGain).
//   SUCTION RING + SUCK-IN (2026-10-17): a modest ring (suctionRadius 2 husk widths past the rim + the Suction upgrades, suctionStrength) draws LOOSE
//   husks nearby towards the bin (a little lift by the wall); a husk over the opening or right by the rim (suckReach) is SUCKED IN like the Storage
//   Crate - it glides into the middle of the bin (SuckOne, suckSeconds) and is caught as usual. Modest on purpose: one bin catches some of a random
//   scatter, not all of it. Behind the back wall of a bin with a Sorting Hopper (where its bottom spout drops husks) is left alone (BehindHopper).
//   SOCKET (PixelSockets.cs): the back hatch is an In socket ('hatch'). With no hopper latched (a latched hopper OCCUPIES it), a coupling there takes belts / connectors / hoses:
//   AcceptHusk counts the husk at once and lets it fall in behind the back wall (HatchInside); refused while working or full (HuskRoom).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class PixelRecycler : MonoBehaviour, IPixelSocketDevice, IPixelHuskRoom, IPixelStrayTarget, IPixelHopperHost, IPixelDeviceInfo
{
    [Tooltip("How many husks fit in the bin at the default size (2). A bigger bin (Operation: Size) holds more - by its floor area.")]
    [SerializeField] private int capacity = 25;
    [Range(0f, 1f)]
    [Tooltip("Share of each husk's stored value paid back as currency of its type when the bin is processed (0.5 = 50%).")]
    [SerializeField] private float payoutShare = 0.5f;
    [Tooltip("Seconds the lid takes to stretch shut / pull back open.")]
    [SerializeField] private float lidSeconds = 0.45f;
    [Tooltip("Seconds the bin shakes (growing stronger) with the lid shut before it pays (Operation: Speed takes some off).")]
    [SerializeField] private float processSeconds = 2f;

    /// <summary>The default size factor of a placed Recycler (the Device's radius; Operation: Size adds to it). 1.8 = nearly twice as wide and deep as the first bin (2 until 2026-10-18, then 10% smaller).</summary>
    public const float DefaultSize = 1.8f;

    /// <summary>The processing time before any upgrade (the Operation tooltip starts from it).</summary>
    public const float DefaultProcessSeconds = 2f;

    /// <summary>How many husks fit now: capacity at the default size, scaled by the bin's floor area.</summary>
    private int Capacity => Mathf.Max(1, Mathf.RoundToInt(capacity * Mathf.Pow(SizeFactor / DefaultSize, 2f)));

    /// <summary>The shake time with the Operation 'Speed' upgrades.</summary>
    private float ProcessSeconds
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Max(0.3f, processSeconds + (c != null ? c.OperationBonus(PixelConsumables.DeviceKind.Recycler, PixelOperationStat.Speed) * 3f : 0f));   // (Speed is tuned for the auto recycle delay; the button cycle takes 3x it off the shake)
        }
    }
    [Tooltip("Strongest shake at the end of processing, in husk widths.")]
    [SerializeField] private float shakeStrength = 0.12f;
    [Header("Suck-in (2026-10-17)")]
    [Tooltip("Like the Storage Crate: a husk in the air over the opening or just past the rim (a near miss) is SUCKED IN - it glides into the bin and is caught. Off = only husks that fall in by themselves.")]
    [SerializeField] private bool suckIn = true;
    [Min(0f)]
    [Tooltip("How far past the rim (sideways, in husk widths) a near miss is still sucked in. Bigger = fewer misses.")]
    [SerializeField] private float suckReach = 0.85f;
    [Min(0.05f)]
    [Tooltip("Seconds a sucked husk takes to glide into the bin.")]
    [SerializeField] private float suckSeconds = 0.3f;

    [Header("Suction ring (2026-10-17 rework)")]
    [Tooltip("A modest pull round the rim: LOOSE husks lying or flying near the bin are drawn towards it, and the suck-in takes them once they reach the rim. Off = only husks that land in (or near-miss) it.")]
    [SerializeField] private bool suction = true;
    [Min(0f)]
    [Tooltip("How far past the rim (in husk widths) the suction ring reaches, before the Operation 'Suction' upgrades. Small on purpose: one bin catches SOME of a random scatter, not all of it.")]
    [SerializeField] private float suctionRadius = 2f;
    [Min(0f)]
    [Tooltip("How strong the ring's pull is (an acceleration in husk widths per second squared; stronger close to the rim, weaker at the edge of the ring).")]
    [SerializeField] private float suctionStrength = 5f;
    [Tooltip("The suction only reaches husks ABOVE the bin (higher than its rim, falling or flying over it): nothing lying beside it on the floor is drawn in. Off = the old ring round the bin too.")]
    [SerializeField] private bool suctionFromAboveOnly = true;
    [Min(0.5f)]
    [Tooltip("How high above the rim the suction reaches (husk widths).")]
    [SerializeField] private float suctionHeightHusks = 5f;

    [Header("Auto recycling (back on 2026-10-18)")]
    [Tooltip("Every husk that lands in the bin is recycled on its own a moment later - no button. Off = fill the bin and press the button (turn showButton on too).")]
    [SerializeField] private bool autoRecycle = true;
    [Min(0f)]
    [Tooltip("Seconds a husk lies in the bin before it is recycled (Operation: Speed takes some off).")]
    [SerializeField] private float recycleDelay = 0.5f;
    [Min(0.5f)]
    [Tooltip("Seconds without a recycled husk before the same-type streak ends.")]
    [SerializeField] private float streakTimeout = 6f;
    [Tooltip("Show the red button on the front (hidden while auto recycling; kept in case the button cycle comes back).")]
    [SerializeField] private bool showButton = false;

    /// <summary>The recycle delay before any upgrade (the Operation tooltip starts from it).</summary>
    public const float DefaultRecycleDelay = 0.5f;

    /// <summary>The recycle delay with the Operation 'Speed' upgrades.</summary>
    private float RecycleDelay
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Max(0.05f, recycleDelay + (c != null ? c.OperationBonus(PixelConsumables.DeviceKind.Recycler, PixelOperationStat.Speed) : 0f));
        }
    }

    private readonly Dictionary<Rigidbody, float> capturedAt = new Dictionary<Rigidbody, float>();   // auto recycling: when each husk was caught
    private float lastRecycleTime = -100f;

    [Header("Overfilling")]
    [Min(0f)]
    [Tooltip("When the button is pressed, husks lying in the bin past its capacity POP out over the rim at this speed (world units per second; a little random spread).")]
    [SerializeField] private float overfillPopSpeed = 3.2f;

    [Header("Paying out (press the button)")]
    [Tooltip("Skill XP per recycled husk (it trains the husk's own skill; pure husks train Standard). 0 since the 2026-10-17 rework: the Recycler is CURRENCY ONLY - husk XP comes from Husk Crush and the skill machines. The streak does not multiply it.")]
    [SerializeField] private float xpPerHusk = 0f;
    [Min(0f)]
    [Tooltip("Share of that XP a PURE husk gives Standard (skill husks keep all of theirs). The Recycler's streak XP levelled Standard about twice as fast as planned (2026-10-17 report), so Standard gets half. Even at half, a streak husk (~1.3 clicks' XP + currency) still beats the best crush (capped at 1 click). 0 = the coded default (0.5).")]
    [SerializeField] private float standardXpShare = 0f;
    [Tooltip("STREAK (within one press of the button): each husk of the SAME type as the one before it in the bin raises the CURRENCY multiplier by this much (0.1 = x1.1, x1.2...). A different type keeps only part of it.")]
    [SerializeField] private float streakStep = 0.1f;
    [Range(0f, 1f)]
    [Tooltip("Share of the streak KEPT when a husk of a different type comes in (0.5 = it halves; 0 = it starts again). Testing showed a full reset makes streaks impossible once Gray is in the mix.")]
    [SerializeField] private float streakKeepOnSwitch = 0.5f;
    [Tooltip("Highest streak multiplier on the currency (before the Streak Power upgrades). 1.6 x the 50% share = 80% of a click.")]
    [SerializeField] private float streakMaxMultiplier = 1.6f;
    [Range(0.1f, 1f)]
    [Tooltip("The most a recycled husk may ever pay, as a share of what a click on that block pays (streak and upgrades included): it never out-earns clicking.")]
    [SerializeField] private float maxShareOfClick = 0.95f;

    [Tooltip("Colour of the small ring above the bin that fills as husks go in (the device card shows the numbers).")]
    [SerializeField] private Color counterColor = new Color(0.75f, 1f, 0.75f, 1f);

    private static readonly List<PixelRecycler> all = new List<PixelRecycler>();

    /// <summary>Every Recycler in the scene (a Container Hose finds its end here).</summary>
    public static IReadOnlyList<PixelRecycler> All => all;

    /// <summary>Can a husk be put in now (a Sorting Hopper slide, a belt / hose through the hatch coupling): not working and not full.</summary>
    public bool HoseCanTake => !processing && inside.Count < Capacity;

    /// <summary>How many more husks fit in the bin.</summary>
    public int HoseRoom => Mathf.Max(0, Capacity - inside.Count);

    // ---- Socket (PixelSockets.cs): the BACK HATCH, an In socket. A Sorting Hopper latched on the back occupies it (PixelCouplings.Occupy); otherwise a coupling
    // there takes belts / connectors / hoses ----
    public const string HatchSocket = "hatch";
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => BodyColor;
    public int HuskRoom(PixelSocket socket) => HoseCanTake ? HoseRoom : 0;

    /// <summary>A husk comes in through the hatch coupling: it is counted at once (so a hose never overfills the bin) and let fall in behind the back wall. Refused while working or full.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk)
    {
        if (husk == null || !HoseCanTake || Busy || Fading || !PixelHuskHolder.Capture(clicker, husk)) return false;
        husk.isKinematic = false;
        husk.position = HatchInside;
        PixelHuskHolder.SetVelocity(husk, transform.TransformDirection(Vector3.back) * 1.2f + Vector3.down);
        inside.Add(husk);
        capturedAt[husk] = Time.time;
        RefreshCounter();
        PixelAudio.Play("click");
        return true;
    }

    // ---- The Sorting Hopper's hookup (PixelSortingHopper snaps onto the back wall with its exit at the back hatch) ----

    /// <summary>The bin's size factor (Operation: Bin Size); a Sorting Hopper on its back is built to the same scale.</summary>
    public float SizeFactor => baseScale.x;

    /// <summary>True while the floor's props fade scales the bin (main menu, after a load): a hopper on it waits.</summary>
    public bool Fading => (transform.localScale - baseScale).sqrMagnitude > 0.0001f;

    /// <summary>True while the bin works (lid shut) or is being carried: a hopper feeds nothing in then.</summary>
    public bool Busy => processing || (carry != null && carry.Dragging);

    /// <summary>How far behind the bin's middle (its own unscaled units, +z) the back wall, pillars and plinth end: a hopper's exit edge goes here.</summary>
    public float BackEdgeLocalZ => Unit(clicker) * (DepthHusks * 0.5f + 0.4f);

    /// <summary>Just inside the back wall, behind the hatch: a husk slid in here falls into the bin (world).</summary>
    // ---- IPixelHopperHost (a Sorting Hopper on the back) ----
    public string HostName => "Recycler";
    public IEnumerable<string> HopperSockets { get { yield return HatchSocket; } }
    public bool CanTakeFeed => !Busy && !Fading && HoseCanTake;

    /// <summary>A hopper feeds a husk in: it is spawned at the pipe's mouth and falls into the bin (Capture takes it from there).</summary>
    public bool TakeFeed(int tier, double amount, Vector3 at, Vector3 velocity) => clicker != null && clicker.SpawnStoredPixel(tier, amount, at, velocity);

    /// <summary>Where a Sorting Hopper's feed pipe lets a husk go: at rim height, just inside the back wall (world; follows the bin's size).</summary>
    public Vector3 FeedDropPoint => transform.TransformPoint(new Vector3(0f, wallH - Unit(clicker) * 0.45f, innerD * 0.5f - Unit(clicker) * 0.9f));   // (a default bin + hopper: the pipe runs level)

    public Vector3 HatchInside => transform.TransformPoint(new Vector3(0f, HatchPassY, innerD * 0.5f - Unit(clicker) * 0.9f));

    private float HatchPassY => HatchBottom(Unit(clicker)) + HatchHeight(Unit(clicker)) * 0.45f;

    private static int hotFrame = -10;

    /// <summary>True while the pointer is over a Recycler's button (clicks there belong to the Recycler, not the cube or Pixel Grabbing).</summary>
    public static bool ButtonHot => Time.frameCount - hotFrame <= 1;

    /// <summary>Is this husk being kept in a Recycler (taken off the floor's old-pixel list)? The block tooltip still shows it.</summary>
    public static bool Holds(Rigidbody body)
    {
        if (body == null) return false;
        foreach (PixelRecycler r in all) if (r != null && r.inside.Contains(body)) return true;
        return false;
    }

    /// <summary>Takes a husk out of the Recycler that holds it (Pixel Grabbing picking it up): loose, unparented and back on the floor's old-pixel list. False if no Recycler holds it.</summary>
    public static bool TakeOut(Rigidbody body)
    {
        if (body == null) return false;
        foreach (PixelRecycler r in all)
        {
            if (r == null || !r.inside.Remove(body)) continue;
            r.capturedAt.Remove(body);
            r.Unfreeze(body);
            if (r.clicker != null) r.clicker.AdoptOldPixel(body);
            r.RefreshCounter();
            return true;
        }
        return false;
    }

    /// <summary>A husk let go above or inside a Recycler's opening gets a moment more life, so an old husk's timer can't run out in the instant before the bin captures it.</summary>
    public static void KeepIfOver(Rigidbody body)
    {
        if (body == null) return;
        foreach (PixelRecycler r in all)
        {
            if (r == null || !r.Over(body.position)) continue;
            OldPixelDespawn d = PixelHuskQuery.Get(body).despawn;
            if (d != null && !d.IsDespawning) d.KeepAlive();
            return;
        }
    }

    private PixelClicker clicker;
    private PixelCarryHandles carry;   // the grip handles (shared with the Storage Crate)
    private Vector3 baseScale = Vector3.one;
    private Transform lidPivot, button, hatchPivot;
    private readonly List<Collider> hatchColliders = new List<Collider>();
    private bool hatchOpen, hatchMoving;
    [Tooltip("How far away (in husk widths) the back hatch can be clicked from in first person.")]
    [SerializeField] private float hatchReachHusks = 12f;
    private Renderer lampRenderer;
    private readonly List<Renderer> bandRenderers = new List<Renderer>();
    private static readonly Color IdleGlow = new Color(0.35f, 1f, 0.55f, 1f), WorkGlow = new Color(1f, 0.7f, 0.2f, 1f);
    private Collider buttonCollider;
    private Renderer buttonRenderer;
    private Vector3 buttonScale;
    private PixelWorldRing ring;   // (how full the bin is; the numbers are on the device card)
    private int sessionHusks, sessionCycles, lastBatchRun;   // what this bin did since it was placed / loaded (the device panel)
    private double sessionPaid;
    private readonly List<Rigidbody> inside = new List<Rigidbody>();
    private int streakTier = -1, streakCount;
    private readonly Dictionary<Rigidbody, float> restSeconds = new Dictionary<Rigidbody, float>();   // how long each captured husk has lain still
    private float tick;
    private bool processing;
    private float innerW, innerD, wallH;

    // ---- Building ----

    private const float WidthHusks = 5f, DepthHusks = 4f, HeightHusks = 3.6f;   // (height 3 -> 3.6 on 2026-10-17: a bit taller)
    private static readonly Color BodyColor = new Color(0.25f, 0.55f, 0.32f, 1f), LidColor = new Color(0.2f, 0.45f, 0.27f, 1f), ButtonColor = new Color(0.9f, 0.2f, 0.18f, 1f);

    /// <summary>Builds a placed Recycler at 'point' (with its behaviour).</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw, float sizeFactor = 1f)
    {
        GameObject root = Build(clicker, false, true);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        root.transform.localScale = Vector3.one * Mathf.Max(0.5f, sizeFactor);   // (Operation: Bin Size - everything, the catching area too, scales with it)
        PixelRecycler r = root.AddComponent<PixelRecycler>();
        r.Init(clicker);
        r.carry = root.AddComponent<PixelCarryHandles>();
        r.carry.Init(() => clicker != null ? clicker.TargetCamera : null);
        r.carry.Blocked = () => r.processing;   // no carrying while the lid is shut and the husks are being processed
        return root;
    }

    /// <summary>A see-through copy that follows the mouse while placing.</summary>
    public static GameObject CreateGhost(PixelClicker clicker) => Build(clicker, true, false);

    /// <summary>A model for the shop / inventory pictures (no colliders, no behaviour).</summary>
    public static GameObject CreateDisplay(PixelClicker clicker) => Build(clicker, false, false);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders)
    {
        float u = Unit(clicker);
        float w = WidthHusks * u, d = DepthHusks * u, h = HeightHusks * u, t = 0.25f * u;
        PixelFactoryKit kit = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Recycler (Preview)" : "Recycler");
        Transform tr = root.transform;
        Color metal = new Color(0.72f, 0.75f, 0.78f, 1f), dark = new Color(0.12f, 0.14f, 0.15f, 1f), liner = new Color(0.16f, 0.32f, 0.2f, 1f);
        Color glow = new Color(0.35f, 1f, 0.55f, 1f);

        // Plinth and four feet.
        kit.Box("Plinth", tr, new Vector3(0f, t * 0.6f, 0f), new Vector3(w + 3f * t, t * 0.8f, d + 3f * t), dark, default, colliders, 0.5f, 0.6f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                kit.Box("Foot", tr, new Vector3(sx * (w * 0.5f + t), t * 0.1f, sz * (d * 0.5f + t)), new Vector3(t * 1.2f, t * 0.4f, t * 1.2f), metal, default, false, 0.8f, 0.6f);
        // Floor and walls (inner liner a darker green), with the opening on top.
        kit.Box("Base", tr, new Vector3(0f, t * 1.1f, 0f), new Vector3(w + 2f * t, t * 0.4f, d + 2f * t), liner, default, colliders);
        kit.Box("Wall Front", tr, new Vector3(0f, h * 0.5f, -(d + t) * 0.5f), new Vector3(w + 2f * t, h, t), BodyColor, default, colliders, 0.3f, 0.55f);
        // The back wall has a small HATCH in it (opened from first person to toss husks in by hand): the wall is four pieces round the hole.
        float zb = (d + t) * 0.5f, hw2 = HatchWidth(u) * 0.5f, hb = HatchBottom(u), ht = hb + HatchHeight(u), fullW = w + 2f * t;
        float sideW = fullW * 0.5f - hw2;
        kit.Box("Wall Back L", tr, new Vector3(-(hw2 + sideW * 0.5f), h * 0.5f, zb), new Vector3(sideW, h, t), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Back R", tr, new Vector3(hw2 + sideW * 0.5f, h * 0.5f, zb), new Vector3(sideW, h, t), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Back Low", tr, new Vector3(0f, hb * 0.5f, zb), new Vector3(hw2 * 2f, hb, t), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Back High", tr, new Vector3(0f, (ht + h) * 0.5f, zb), new Vector3(hw2 * 2f, h - ht, t), BodyColor, default, colliders, 0.3f, 0.55f);
        // A metal frame round the hole.
        kit.Box("Hatch Frame Top", tr, new Vector3(0f, ht + t * 0.12f, zb + t * 0.55f), new Vector3(hw2 * 2f + t * 0.5f, t * 0.25f, t * 0.15f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Hatch Frame Bottom", tr, new Vector3(0f, hb - t * 0.12f, zb + t * 0.55f), new Vector3(hw2 * 2f + t * 0.5f, t * 0.25f, t * 0.15f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Hatch Frame L", tr, new Vector3(-hw2 - t * 0.12f, (hb + ht) * 0.5f, zb + t * 0.55f), new Vector3(t * 0.25f, ht - hb + t * 0.5f, t * 0.15f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Hatch Frame R", tr, new Vector3(hw2 + t * 0.12f, (hb + ht) * 0.5f, zb + t * 0.55f), new Vector3(t * 0.25f, ht - hb + t * 0.5f, t * 0.15f), metal, default, false, 0.85f, 0.7f);
        // The hatch door: slats tucked under the hole's top edge that stretch DOWN over it, the same way the lid stretches over the top
        // (Hatch Pivot y scale: HatchOpenScale = tucked away, 1 = shut). It starts shut.
        GameObject hatch = new GameObject("Hatch Pivot");
        hatch.transform.SetParent(tr, false);
        hatch.transform.localPosition = new Vector3(0f, ht, zb);
        const int hatchSlats = 4;
        float holeH = ht - hb;
        for (int i = 0; i < hatchSlats; i++)
        {
            Color c = i % 2 == 0 ? LidColor : Color.Lerp(LidColor, metal, 0.35f);
            kit.Box("Hatch Slat " + i, hatch.transform, new Vector3(0f, -(i + 0.5f) / hatchSlats * holeH, 0f), new Vector3(hw2 * 2f, holeH / hatchSlats * 0.94f, t * 0.6f), c, default, colliders, 0.45f, 0.6f);
        }
        kit.Box("Hatch Handle", hatch.transform, new Vector3(0f, -holeH * 0.85f, t * 0.45f), new Vector3(hw2 * 0.6f, t * 0.18f, t * 0.25f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Wall Left", tr, new Vector3(-(w + t) * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), BodyColor, default, colliders, 0.3f, 0.55f);
        kit.Box("Wall Right", tr, new Vector3((w + t) * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), BodyColor, default, colliders, 0.3f, 0.55f);
        // Brushed-metal corner pillars, a little taller than the walls, and a metal rim round the opening.
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                kit.Box("Pillar", tr, new Vector3(sx * (w + t) * 0.5f, h * 0.55f, sz * (d + t) * 0.5f), new Vector3(t * 1.5f, h * 1.1f, t * 1.5f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Front", tr, new Vector3(0f, h + t * 0.12f, -(d + t) * 0.5f), new Vector3(w + 2f * t, t * 0.25f, t * 1.3f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Back", tr, new Vector3(0f, h + t * 0.12f, (d + t) * 0.5f), new Vector3(w + 2f * t, t * 0.25f, t * 1.3f), metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Left", tr, new Vector3(-(w + t) * 0.5f, h + t * 0.12f, 0f), new Vector3(t * 1.3f, t * 0.25f, d), metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Right", tr, new Vector3((w + t) * 0.5f, h + t * 0.12f, 0f), new Vector3(t * 1.3f, t * 0.25f, d), metal, default, false, 0.85f, 0.7f);
        // A glowing band round the body (it pulses while the bin works).
        float bandY = h * 0.32f, bandT = t * 0.18f;
        kit.Box("Band Front", tr, new Vector3(0f, bandY, -(d + 2f * t) * 0.5f - bandT * 0.5f), new Vector3(w + 2f * t, t * 0.22f, bandT), glow, default, false, 0f, 0.8f, glow);
        kit.Box("Band Back", tr, new Vector3(0f, bandY, (d + 2f * t) * 0.5f + bandT * 0.5f), new Vector3(w + 2f * t, t * 0.22f, bandT), glow, default, false, 0f, 0.8f, glow);
        kit.Box("Band Left", tr, new Vector3(-(w + 2f * t) * 0.5f - bandT * 0.5f, bandY, 0f), new Vector3(bandT, t * 0.22f, d + 2f * t), glow, default, false, 0f, 0.8f, glow);
        kit.Box("Band Right", tr, new Vector3((w + 2f * t) * 0.5f + bandT * 0.5f, bandY, 0f), new Vector3(bandT, t * 0.22f, d + 2f * t), glow, default, false, 0f, 0.8f, glow);
        // The recycling arrows on the front: three pale bars in a triangle.
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            Vector3 p = new Vector3(-w * 0.12f + Mathf.Cos(a * Mathf.Deg2Rad) * u * 0.55f, h * 0.62f + Mathf.Sin(a * Mathf.Deg2Rad) * u * 0.55f, -(d + t) * 0.5f - t * 0.55f);
            kit.Box("Arrow " + i, tr, p, new Vector3(u * 0.9f, u * 0.18f, t * 0.2f), new Color(0.85f, 1f, 0.85f, 1f), new Vector3(0f, 0f, a + 90f), false, 0f, 0.6f, new Color(0.3f, 0.6f, 0.35f, 1f));
        }
        // The status lamp on the back-left pillar: green while open, amber while working.
        kit.Cyl("Lamp Base", tr, new Vector3(-(w + t) * 0.5f, h * 1.1f + t * 0.1f, (d + t) * 0.5f), t * 0.6f, t * 0.2f, dark);
        kit.Box("Lamp", tr, new Vector3(-(w + t) * 0.5f, h * 1.1f + t * 0.45f, (d + t) * 0.5f), Vector3.one * t * 0.7f, glow, default, false, 0f, 0.9f, glow);

        // The lid: a slatted panel tucked under the BACK rim. It extends forward out of the inner back edge and stretches over the opening
        // (its pivot sits on that edge and scales along z: 0 = tucked away, 1 = covering the opening). It has colliders, so husks dropped on a
        // shut lid rest on top until it pulls back.
        GameObject pivot = new GameObject("Lid Pivot");
        pivot.transform.SetParent(tr, false);
        pivot.transform.localPosition = new Vector3(0f, h - t * 0.15f, d * 0.5f);
        const int slats = 6;
        for (int i = 0; i < slats; i++)
        {
            float z0 = -(i + 0.5f) / slats;
            Color c = i % 2 == 0 ? LidColor : Color.Lerp(LidColor, metal, 0.35f);
            kit.Box("Lid Slat " + i, pivot.transform, new Vector3(0f, 0f, z0 * d), new Vector3(w, t * 0.35f, d / slats * 0.94f), c, default, colliders, 0.45f, 0.6f);
        }
        pivot.transform.localScale = new Vector3(1f, 1f, LidOpenScale);

        // Carrying handles on the short ends, the same as the Storage Crate's (shared PixelCarryHandles): drag one to move the bin, scroll / Q / E turns it.
        PixelCarryHandles.BuildParts((n, p, size, c) => kit.Box(n, tr, p, size, c), w * 0.5f + t * 1.25f, d, h * 0.55f, 0.68f * u);
        // The big button on top of the front wall.
        if (!colliders) return root;   // (the ghost and the shop picture have no button: it is hidden on the placed bin too unless showButton)
        kit.Cyl("Button Base", tr, new Vector3(w * 0.32f, h + t * 0.4f, -(d + t) * 0.5f), u * 0.34f, t * 0.8f, new Color(0.15f, 0.15f, 0.15f, 1f));
        kit.Cyl("Button", tr, new Vector3(w * 0.32f, h + t * 1.1f, -(d + t) * 0.5f), u * 0.24f, t * 0.9f, ButtonColor, default, 0.1f, 0.6f, colliders);
        return root;
    }

    // The back hatch's hole, in husk widths (u): its width, how high its bottom edge sits, its height.
    private static float HatchWidth(float u) => 1.7f * u;
    private static float HatchBottom(float u) => 0.9f * u;
    private static float HatchHeight(float u) => 1.2f * u;
    private const float HatchOpenScale = 0.03f;

    /// <summary>The lid pivot's z scale: tucked away under the back rim, or stretched over the whole opening.</summary>
    private const float LidOpenScale = 0.02f, LidClosedScale = 1f;

    private void Init(PixelClicker owner)
    {
        clicker = owner;
        float u = Unit(clicker);
        innerW = WidthHusks * u; innerD = DepthHusks * u; wallH = HeightHusks * u;
        baseScale = transform.localScale;
        sockets = new[] { new PixelSocket(HatchSocket, new Vector3(0f, 0f, BackEdgeLocalZ), Vector3.forward, PixelSocketFlow.Intake) };
        lidPivot = transform.Find("Lid Pivot");
        hatchPivot = transform.Find("Hatch Pivot");
        if (hatchPivot != null) foreach (Collider c in hatchPivot.GetComponentsInChildren<Collider>()) hatchColliders.Add(c);
        Transform lamp = transform.Find("Lamp");
        lampRenderer = lamp != null ? lamp.GetComponent<Renderer>() : null;
        foreach (string band in new[] { "Band Front", "Band Back", "Band Left", "Band Right" })
        {
            Transform b = transform.Find(band);
            if (b != null && b.GetComponent<Renderer>() != null) bandRenderers.Add(b.GetComponent<Renderer>());
        }
        button = transform.Find("Button");
        buttonCollider = button != null ? button.GetComponent<Collider>() : null;
        buttonRenderer = button != null ? button.GetComponent<Renderer>() : null;
        if (button != null) buttonScale = button.localScale;
        if (!showButton)   // (hidden while auto recycling: kept so the button cycle can come back)
        {
            if (button != null) button.gameObject.SetActive(false);
            Transform buttonBase = transform.Find("Button Base");
            if (buttonBase != null) buttonBase.gameObject.SetActive(false);
        }

        ring = PixelWorldRing.Attach(transform, new Vector3(0f, wallH + u * 1.6f, 0f), u * 0.45f, counterColor);
        PixelHoverMarker.Attach(transform, new Vector3(0f, wallH + u * 1.1f, 0f), u, hoverArrowColor, PointerOnBin);   // (shared with the Sorting Hopper; the time left is on the device card)
        RefreshCounter();
    }

    [Tooltip("Colour of the floating arrow (and the time beside it) that shows while the pointer is on the Recycler.")]
    [SerializeField] private Color hoverArrowColor = new Color(0.55f, 1f, 0.6f, 1f);

    /// <summary>Is a pointer ray on the bin (its parts, as the device's own hover / remove check sees them)?</summary>
    private bool PointerOnBin(Ray ray)
    {
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        return device != null && device.HitTest(ray, out _);
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); PixelCouplings.Register(this); }
    private void OnDisable() { all.Remove(this); PixelCouplings.Unregister(this); }

    private void Awake() => PixelHuskPool.HuskReleasing += OnHuskReleasing;

    // (PixelHuskPool: the husk is going away and may be reused - let go of it; SuckOne sees it and stops)
    private void OnHuskReleasing(Rigidbody body) => sucking.Remove(body);

    private void OnDestroy()
    {
        PixelHuskPool.HuskReleasing -= OnHuskReleasing;
        foreach (Rigidbody b in sucking) if (b != null) PixelHuskHolder.Unreserve(b, Vector3.zero);   // (half way in when the bin went: it falls where it is)
        sucking.Clear();
        foreach (Rigidbody b in inside) if (b != null) Destroy(b.gameObject);   // (not saved: what was inside is lost with the bin)
        inside.Clear();
    }

    // ---- Every frame ----

    private static readonly List<PixelHuskQuery.Husk> pullScratch = new List<PixelHuskQuery.Husk>();

    private readonly HashSet<Rigidbody> sucking = new HashSet<Rigidbody>();

    /// <summary>
    /// The suck-in: a husk over the opening (above the rim) or right by the rim (within suckReach - in the air, or low by the wall where the suction ring
    /// brings it) is taken - kept from anything else (PixelHuskHolder.Reserve) - and glides down into the middle of the bin, where the usual Capture catches
    /// it. Only husks nobody else owns (Takeable); never behind a latched Sorting Hopper (its bottom spout's husks).
    /// </summary>
    private void SuckIn()
    {
        if (!suckIn || clicker == null || processing || Fading || (carry != null && carry.Dragging)) return;
        float u = Unit(clicker);
        float s = transform.lossyScale.x;
        float halfDiag = (Mathf.Sqrt(innerW * innerW + innerD * innerD) * 0.5f + suckReach * u) * s;
        PixelHuskQuery.Near(clicker, transform.position, halfDiag, pullScratch);
        foreach (PixelHuskQuery.Husk h in pullScratch)
        {
            Rigidbody b = h.body;
            if (b == null || inside.Contains(b) || sucking.Contains(b) || !PixelHuskHolder.Takeable(clicker, b)) continue;   // (one owner per husk: Takeable)
            Vector3 local = transform.InverseTransformPoint(b.position);
            bool overOpening = Mathf.Abs(local.x) <= innerW * 0.5f && Mathf.Abs(local.z) <= innerD * 0.5f;
            if (overOpening && local.y < wallH) continue;   // (already inside the walls: Capture takes it)
            if (!overOpening && local.y < wallH * 0.5f && !suction) continue;   // (lying low beside the bin: only taken with the suction ring on)
            if (suctionFromAboveOnly && local.y < wallH) continue;   // (from above only: nothing beside the bin is sucked in)
            if (BehindHopper(local)) continue;   // (behind a Sorting Hopper: its bottom spout's husks)
            if (local.y > wallH + 4f * u) continue;
            if (Mathf.Abs(local.x) > innerW * 0.5f + suckReach * u || Mathf.Abs(local.z) > innerD * 0.5f + suckReach * u) continue;
            StartCoroutine(SuckOne(b));
        }
    }

    private IEnumerator SuckOne(Rigidbody b)
    {
        sucking.Add(b);
        PixelHuskHolder.Reserve(b);
        Vector3 start = b.position, startScale = b.transform.localScale;
        float t = 0f, length = Mathf.Max(0.05f, suckSeconds);
        while (t < 1f)
        {
            if (b == null) { sucking.Remove(b); yield break; }
            if (!sucking.Contains(b)) yield break;   // (it went away mid-way: PixelHuskPool)
            if (PixelHuskQuery.Get(b).HeldByPlayer) { sucking.Remove(b); yield break; }   // (the player grabbed it on the way: theirs)
            if (processing) break;
            t += Time.deltaTime / length;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 end = transform.TransformPoint(new Vector3(0f, wallH * 0.55f, 0f));
            Vector3 p = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f * Unit(clicker);   // a little arc over the rim
            b.position = p;
            b.transform.localScale = startScale * (1f - 0.25f * Mathf.Sin(k * Mathf.PI));   // squeezed as it is drawn in
            yield return null;
        }
        sucking.Remove(b);
        if (b == null) yield break;
        b.transform.localScale = startScale;
        Vector3 down = Vector3.down * 0.5f;
        if (inside.Contains(b)) { b.isKinematic = false; PixelHuskHolder.SetVelocity(b, down); }   // (the bin already caught it)
        else PixelHuskHolder.Unreserve(b, down);
        PixelAudio.Play("click", 1.3f, 0.5f);
    }

    /// <summary>How far past the rim the suction ring reaches now: suctionRadius + the Operation 'Suction' upgrades (husk widths).</summary>
    private float SuctionReach
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Max(0f, suctionRadius) + (c != null ? c.OperationBonus(PixelConsumables.DeviceKind.Recycler, PixelOperationStat.RecyclerSuction) : 0f);
        }
    }

    /// <summary>The suction base before any upgrade (the Operation tooltip starts from it).</summary>
    public static float DefaultSuctionRadius => 2f;

    /// <summary>Behind the back wall while a Sorting Hopper is latched there: the hopper's bottom spout drops husks there, the bin leaves them alone.</summary>
    private bool BehindHopper(Vector3 local) => local.z > innerD * 0.5f && PixelCouplings.Occupied(this, HatchSocket);

    /// <summary>
    /// The SUCTION RING: loose husks (on the floor or in the air) outside the walls within SuctionReach of the rim are pulled towards the bin - harder
    /// near the rim - and get a little lift close to the wall so they hop up to it, where the suck-in takes them. Modest: a far husk drifts, it is not
    /// yanked. Only husks nobody else owns (PixelHuskHolder.Takeable): one owner per husk.
    /// </summary>
    private void FixedUpdate()
    {
        SuckIn();
        if (!suction || clicker == null || processing || Fading || (carry != null && carry.Dragging) || suctionStrength <= 0f) return;
        float u = Unit(clicker), s = transform.lossyScale.x, reach = SuctionReach;
        if (reach <= 0f) return;
        PixelHuskQuery.Near(clicker, transform.position, (Mathf.Sqrt(innerW * innerW + innerD * innerD) * 0.5f + reach * u) * s, pullScratch);
        float dt = Time.fixedDeltaTime;
        foreach (PixelHuskQuery.Husk h in pullScratch)
        {
            Rigidbody b = h.body;
            if (b == null || inside.Contains(b) || sucking.Contains(b) || !PixelHuskHolder.Takeable(clicker, b)) continue;
            Vector3 local = transform.InverseTransformPoint(b.position);
            if (local.y > wallH + Mathf.Max(0.5f, suctionHeightHusks) * u || BehindHopper(local)) continue;
            if (suctionFromAboveOnly && local.y < wallH + 0.1f * u) continue;   // (from above only: husks beside the bin on the floor are left alone)
            float ox = Mathf.Max(0f, Mathf.Abs(local.x) - innerW * 0.5f), oz = Mathf.Max(0f, Mathf.Abs(local.z) - innerD * 0.5f);
            float past = Mathf.Sqrt(ox * ox + oz * oz) / u;   // husk widths past the rim (0 = over the opening)
            if (past <= 0f || past > reach) continue;
            float k = 1f - past / Mathf.Max(0.01f, reach);   // 1 at the rim, 0 at the edge of the ring
            Vector3 toMiddle = transform.TransformPoint(new Vector3(0f, local.y, 0f)) - b.position;
            toMiddle.y = 0f;
            if (toMiddle.sqrMagnitude < 1e-6f) continue;
            Vector3 dv = toMiddle.normalized * suctionStrength * u * s * (0.3f + 0.7f * k) * dt;
            if (suctionFromAboveOnly) dv += Vector3.down * suctionStrength * u * s * 0.25f * k * dt;   // (drawn down into the opening)
            else if (local.y < wallH * 0.5f && past < 1.5f) dv += Vector3.up * suctionStrength * u * s * 0.6f * k * dt;   // (near the wall: a little lift towards the rim)
#if UNITY_6000_0_OR_NEWER
            b.linearVelocity += dv;
#else
            b.velocity += dv;
#endif
        }
    }

    private void Update()
    {
        if (clicker == null) return;
        Camera cam = clicker.ViewCamera;

        UpdateButtonFlash();
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        FollowSizeUpgrade();
        foreach (Rigidbody b in inside)   // a frozen husk you start to grab is let loose at once (Pixel Grabbing steers it with its velocity)
        {
            if (b == null || !b.isKinematic) continue;
            OldPixelInfo i = PixelHuskQuery.Get(b).info;
            if (i != null && i.heldByPlayer) Unfreeze(b);
        }
        bool hot = showButton && !processing && (carry == null || !carry.Dragging) && buttonCollider != null && cam != null && !PixelInput.PointerOverUI() && PointerOnButton(cam);
        if (hot) hotFrame = PixelPointerClaim.Claim();
        if (hot && PixelInput.LeftPressed() && Time.timeScale > 0f && !PixelPauseMenu.IsPaused) StartCoroutine(Process());

        UpdateHatch();
        if (carry != null && carry.Dragging) KeepContentsWhileCarried();

        tick -= Time.deltaTime;
        if (tick > 0f || processing) return;
        tick = 0.1f;
        Capture();
    }

    private float sizeTarget = -1f, nextSizeLook;

    /// <summary>
    /// A placed bin follows the Size level IN USE (bought, or turned down in the device panel's - / + stepper): it grows / shrinks smoothly to it.
    /// Not while it works, is carried, is ending or the floor's props fade it.
    /// </summary>
    private void FollowSizeUpgrade()
    {
        if (Time.unscaledTime >= nextSizeLook)
        {
            nextSizeLook = Time.unscaledTime + 0.25f;
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            sizeTarget = c != null ? c.EffectiveRadiusOf(PixelConsumables.DeviceKind.Recycler) : -1f;
        }
        if (sizeTarget <= 0f || processing || Fading || (carry != null && carry.Dragging) || (device != null && device.IsEnding)) return;
        float now = baseScale.x, next = Mathf.MoveTowards(now, Mathf.Max(0.5f, sizeTarget), Time.deltaTime * 2f);
        if (Mathf.Abs(next - now) < 0.0001f) return;
        baseScale = Vector3.one * next;
        transform.localScale = baseScale;   // (everything inside is parented and frozen: it scales with the bin)
    }

    [Tooltip("Colour the button flashes towards while there is a husk in the bin.")]
    [SerializeField] private Color flashColor = new Color(1f, 0.95f, 0.45f, 1f);

    /// <summary>As soon as a husk is in the bin its button flashes and pulses, so the player knows to press it.</summary>
    private void UpdateButtonFlash()
    {
        if (!showButton || button == null || buttonRenderer == null) return;
        bool full = !processing && inside.Count >= 1;   // (any husk in the bin: press me)
        float wave = full ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
        Material m = buttonRenderer.material;
        m.color = Color.Lerp(ButtonColor, flashColor, wave);
        if (m.HasProperty("_EmissionColor"))
        {
            if (full) m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", flashColor * (wave * 1.5f));
        }
        button.localScale = new Vector3(buttonScale.x, buttonScale.y, buttonScale.z) * (1f + 0.12f * wave);
    }

    // ---- Saving (PixelConsumables puts this in PlacedState.extra) ----

    /// <summary>The husks in the bin, as 'Type,amount;' (by pixel type name, so it survives tier list changes).</summary>
    public string ExportState()
    {
        StringBuilder sb = new StringBuilder();
        foreach (Rigidbody b in inside)
        {
            if (b == null) continue;
            OldPixelInfo info = PixelHuskQuery.Get(b).info;
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex)) continue;
            sb.Append(clicker.Tiers[info.tierIndex].type).Append(',').Append(info.amount.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
        }
        return sb.ToString();
    }

    /// <summary>Puts saved husks back in the bin (they are captured again on the next check).</summary>
    public void ImportState(string data)
    {
        if (string.IsNullOrEmpty(data) || clicker == null) return;
        float u = Unit(clicker);
        int n = 0, perRow = Mathf.Max(1, Mathf.FloorToInt(WidthHusks));
        foreach (string entry in data.Split(';'))
        {
            string[] parts = entry.Split(',');
            if (parts.Length < 2 || !System.Enum.TryParse(parts[0], out PixelClicker.PixelType type)) continue;
            int tier = clicker.IndexOf(type);
            if (tier < 0) continue;
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double amount);
            int col = n % perRow, row = (n / perRow) % Mathf.Max(1, Mathf.FloorToInt(DepthHusks)), layer = n / (perRow * Mathf.Max(1, Mathf.FloorToInt(DepthHusks)));
            Vector3 local = new Vector3((col - (perRow - 1) * 0.5f) * u, u * (0.8f + layer), (row - (DepthHusks - 1f) * 0.5f) * u);
            int before = clicker.OldPixels.Count;
            clicker.SpawnStoredPixel(tier, amount, transform.TransformPoint(local), Vector3.zero);
            n++;
            if (clicker.OldPixels.Count <= before) continue;
            Rigidbody b = clicker.OldPixels[clicker.OldPixels.Count - 1];
            if (b == null) continue;
            OldPixelPopIn pop = b.GetComponent<OldPixelPopIn>();
            if (pop != null) pop.Finish();   // full size and solid at once
            Vector3 size = b.transform.localScale;   // (its world size: it had no parent)
            if (!PixelHuskHolder.Capture(clicker, b)) continue;
            b.isKinematic = true;
            b.transform.SetParent(transform, false);   // placed straight into the bin in its own space (the bin may still be scaled by the load fade)
            b.transform.localPosition = local;
            b.transform.localRotation = Quaternion.identity;
            b.transform.localScale = size;
            inside.Add(b);
        }
        RefreshCounter();
    }

    private bool PointerOnButton(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        return buttonCollider.Raycast(ray, out _, 500f);
    }

    /// <summary>Husks resting inside the walls are taken off the floor's old-pixel list and kept in the bin.</summary>
    private void Capture()
    {
        bool changed = false;
        inside.RemoveAll(b => b == null);
        // While the floor's props fade (main menu, after a load) the bin is being scaled: everything inside is frozen into it and moves with it; no capturing or letting go.
        bool fading = (transform.localScale - baseScale).sqrMagnitude > 0.0001f;
        if (fading || (carry != null && carry.Dragging))   // (being carried by a handle: the husks ride along frozen in the bin)
        {
            foreach (Rigidbody b in inside) Freeze(b);
            return;
        }
        // Husks in the bin never despawn: their timer stays held every tick (Pixel Grabbing lets go of 'Held' when you poke one); one taken out of the bin is let go again.
        for (int i = inside.Count - 1; i >= 0; i--)
        {
            OldPixelDespawn hold = PixelHuskQuery.Get(inside[i]).despawn;
            OldPixelInfo heldInfo = PixelHuskQuery.Get(inside[i]).info;
            bool inHand = heldInfo != null && heldInfo.heldByPlayer;
            // A husk in the player's hand is never counted; one bouncing up a little above the walls stays counted (KeepsHold), only one that left the bin is let go.
            if (!inHand && KeepsHold(inside[i].position)) { if (hold != null) hold.Held = true; Settle(inside[i], false); continue; }
            LetGo(inside[i]);   // back on the floor's husk list (grabbable, despawns, can be caught again)
            inside.RemoveAt(i);
            changed = true;
        }
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody b = pixels[i];
            if (b == null || !Over(b.position)) continue;
            OldPixelDespawn alive = PixelHuskQuery.Get(b).despawn;
            if (alive != null && !alive.IsDespawning) alive.KeepAlive();   // falling into the bin (or resting in it): its timer waits until it is captured
            if (!Inside(b.position)) continue;
            if (inside.Count >= Capacity)   // the bin is full: husks lying in it anyway still never fade while they are inside
            {
                OldPixelDespawn keep = PixelHuskQuery.Get(b).despawn;
                if (keep != null && !keep.IsDespawning) keep.KeepAlive();
                continue;
            }
            if (!PixelHuskHolder.Takeable(clicker, b)) continue;      // one owner per husk: in the player's hand, vanishing or reserved by another machine (a hopper / Sweeper on its way) = not ours
            if (!PixelHuskHolder.Capture(clicker, b)) continue;      // (off the floor's list, its timer held: it never shrinks away while it is in the bin)
            inside.Add(b);
            capturedAt[b] = Time.time;
            changed = true;
        }
        if (changed) { RefreshCounter(); PixelAudio.Play("click"); }
        if (autoRecycle) RecycleDue();
    }

    // ---- Auto recycling + streak ----

    /// <summary>The streak cap before any upgrade (the Operation tooltip starts from it).</summary>
    public static float DefaultStreakCap => 1.6f;

    /// <summary>The streak cap with the 'Streak Power' upgrades (Upgrades > Operation).</summary>
    private float StreakCap
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Max(1f, streakMaxMultiplier) + (c != null ? c.OperationBonus(PixelConsumables.DeviceKind.Recycler, PixelOperationStat.RecyclerStreak) : 0f);
        }
    }

    private float StreakMultiplier => Mathf.Min(StreakCap, 1f + Mathf.Max(0f, streakStep) * streakCount);

    /// <summary>Auto mode: every husk that has lain in the bin for RecycleDelay is recycled now (not one in the player's hand).</summary>
    private void RecycleDue()
    {
        if (Time.time - lastRecycleTime > streakTimeout && streakTier >= 0) { streakCount = 0; streakTier = -1; SetGlow(IdleGlow, 1f); RefreshCounter(); }
        float delay = RecycleDelay;
        for (int i = inside.Count - 1; i >= 0; i--)
        {
            Rigidbody b = inside[i];
            if (b == null) { inside.RemoveAt(i); continue; }
            if (!capturedAt.TryGetValue(b, out float at)) { capturedAt[b] = Time.time; continue; }   // (loaded husks: their timer starts now)
            if (Time.time - at < delay) continue;
            OldPixelInfo info = PixelHuskQuery.Get(b).info;
            if (info != null && info.heldByPlayer) continue;
            inside.RemoveAt(i);
            capturedAt.Remove(b);
            restSeconds.Remove(b);
            RecycleOne(b, info);
        }
    }

    /// <summary>One husk: currency back (payoutShare of what a click on it pays now x the same-type STREAK, never more than maxShareOfClick of a click); the husk is gone. XP only if xpPerHusk is set (0 by default).</summary>
    private void RecycleOne(Rigidbody b, OldPixelInfo info)
    {
        Vector3 at = b.position;
        Destroy(b.gameObject);
        if (info == null || clicker == null || !clicker.IsValidTierIndex(info.tierIndex)) return;
        int tier = info.tierIndex;
        bool alive = Time.time - lastRecycleTime <= streakTimeout;
        if (!alive) streakCount = 0;
        else if (tier == streakTier) streakCount++;
        else streakCount = Mathf.FloorToInt(streakCount * Mathf.Clamp01(streakKeepOnSwitch));   // another type: the streak shrinks, it does not vanish
        streakTier = tier;
        lastRecycleTime = Time.time;
        double share = System.Math.Min(payoutShare * StreakMultiplier, Mathf.Clamp(maxShareOfClick, 0.1f, 1f));   // (the streak boosts the CURRENCY, capped below a click)
        double pay = CurrentValue(tier) * share;
        if (pay > 0d) { clicker.AddBonusCurrency(tier, pay); PixelUI.ShowWorldGain(tier, pay, at + Vector3.up * Unit(clicker) * transform.lossyScale.x); }   // (the currency's '+n' adds up and a '+n' rises over the recycled husk)
        double xp = Mathf.Max(0f, xpPerHusk);
        PixelSkills skills = PixelSkills.Instance;
        if (xp > 0d && skills != null)
        {
            if (skills.HuskSkillId(tier) == PixelSkills.StandardId) xp *= standardXpShare > 0f ? standardXpShare : 0.5f;
            skills.AddHuskXp(tier, xp);
            PixelStats.Count("recycler.xp", xp);
        }
        PixelStats.Count("recycler.husks");
        PixelStats.Count("recycler.paid", pay);
        sessionHusks++; sessionPaid += pay;
        PixelStats.Best("recycler.streak", streakCount + 1);
        PixelAudio.Play("ui_click", 0.6f + 0.05f * Mathf.Min(streakCount, 12));   // (the pitch climbs with the streak)
        SetGlow(Color.Lerp(IdleGlow, WorkGlow, Mathf.Clamp01(streakCount / 10f)), 1.4f);
        RefreshCounter();
    }

    /// <summary>
    /// While the bin is carried: every husk in it is frozen into it AT ONCE (not on the next 0.1 s check, so none is left behind as the bin starts to
    /// move), and any husk that still ends up outside the walls is let go and taken off the count straight away.
    /// </summary>
    private void KeepContentsWhileCarried()
    {
        bool changed = false;
        for (int i = inside.Count - 1; i >= 0; i--)
        {
            Rigidbody b = inside[i];
            if (b == null) { inside.RemoveAt(i); changed = true; continue; }
            OldPixelInfo carriedInfo = PixelHuskQuery.Get(b).info;
            if ((carriedInfo == null || !carriedInfo.heldByPlayer) && KeepsHold(b.position)) { Freeze(b); continue; }
            inside.RemoveAt(i);
            LetGo(b);   // a normal husk on the floor again
            changed = true;
        }
        if (changed) RefreshCounter();
    }

    /// <summary>A captured husk that has lain still for a moment is frozen in place (kinematic), so the pile can't jitter or bounce; picking it up unfreezes it.</summary>
    private void Settle(Rigidbody b, bool inHand)
    {
        if (inHand) { Unfreeze(b); return; }
        if (b.isKinematic) { Freeze(b); return; }
#if UNITY_6000_0_OR_NEWER
        float speed = b.linearVelocity.sqrMagnitude;
#else
        float speed = b.velocity.sqrMagnitude;
#endif
        restSeconds.TryGetValue(b, out float rest);
        rest = speed < 0.04f ? rest + 0.1f : 0f;
        restSeconds[b] = rest;
        if (rest < 0.3f) return;
        Freeze(b);
    }

    /// <summary>Freezes a husk into the bin: kinematic and parented to it, so it scales and moves with the Recycler (the menu fade shrinks the bin with its contents).</summary>
    private void Freeze(Rigidbody b) => PixelHuskHolder.Freeze(b, transform);

    private void Unfreeze(Rigidbody b)
    {
        restSeconds.Remove(b);
        PixelHuskHolder.Unfreeze(b, transform);
    }

    /// <summary>A husk leaves the bin: back to the floor as a normal husk (shared PixelHuskHolder.LetGo).</summary>
    private void LetGo(Rigidbody b)
    {
        restSeconds.Remove(b);
        PixelHuskHolder.LetGo(clicker, b, transform);
    }

    /// <summary>Is a husk that is already counted still in the bin? Like Inside, but it may bounce up to two wall heights above the floor of the bin.</summary>
    private bool KeepsHold(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= innerW * 0.5f && Mathf.Abs(p.z) <= innerD * 0.5f && p.y >= -0.05f && p.y <= wallH * 2f;
    }

    /// <summary>Takes out anything that should not be recycled right now: husks in the player's hand or no longer in the bin go back to the floor; the counter is redone.</summary>
    private void DropStrays()
    {
        bool changed = false;
        for (int i = inside.Count - 1; i >= 0; i--)
        {
            Rigidbody b = inside[i];
            if (b == null) { inside.RemoveAt(i); changed = true; continue; }
            OldPixelInfo info = PixelHuskQuery.Get(b).info;
            if (info != null && !info.heldByPlayer && KeepsHold(b.position)) continue;
            LetGo(b);
            inside.RemoveAt(i);
            changed = true;
        }
        if (changed) RefreshCounter();
    }

    /// <summary>
    /// Husks lying in the bin that are NOT counted (the bin was OVERFILLED past its capacity, or one fell in as the lid closed) POP out when the button
    /// is pressed: lifted just over the rim and launched up and out with a little spin, so the bin holds exactly what is recycled. Out over the front /
    /// back rim (the handles stick out on the X ends); only the front while a Sorting Hopper sits on the back. The suction may draw them back in after
    /// the cycle, which is fine - they are counted then.
    /// </summary>
    private void EjectUncounted()
    {
        if (clicker == null) return;
        bool hopperBehind = PixelCouplings.Occupied(this, HatchSocket);
        int popped = 0;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody b = pixels[i];
            if (b == null || b.isKinematic || inside.Contains(b) || sucking.Contains(b) || !KeepsHold(b.position)) continue;
            OldPixelInfo info = PixelHuskQuery.Get(b).info;
            if (info != null && info.heldByPlayer) continue;
            Vector3 p = transform.InverseTransformPoint(b.position);
            float side = hopperBehind ? -1f : (p.z >= 0f ? 1f : -1f);
            float size = b.transform.lossyScale.x;
            b.position = transform.TransformPoint(new Vector3(p.x, wallH, side * innerD * 0.3f)) + Vector3.up * size;   // (just over the rim, so the walls and the closing lid don't catch it)
            Vector3 dir = transform.TransformDirection(new Vector3(Random.Range(-0.35f, 0.35f), 1.3f, side)).normalized;
            PixelHuskHolder.SetVelocity(b, dir * overfillPopSpeed * Random.Range(0.85f, 1.15f));
            b.angularVelocity = Random.insideUnitSphere * 8f;
            popped++;
        }
        if (popped > 0) PixelAudio.Play("click");
    }

    private bool Inside(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= innerW * 0.5f && Mathf.Abs(p.z) <= innerD * 0.5f && p.y >= -0.05f && p.y <= wallH;
    }

    /// <summary>Inside the walls or in the column of air above the opening (a husk falling in).</summary>
    private bool Over(Vector3 world)
    {
        Vector3 p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= innerW * 0.5f + 0.1f && Mathf.Abs(p.z) <= innerD * 0.5f + 0.1f && p.y >= -0.05f && p.y <= wallH * 4f;
    }

    private void RefreshCounter()
    {
        if (ring == null) return;
        int cap = Capacity;
        ring.Set(inside.Count == 0 ? -1f : inside.Count / (float)cap, inside.Count >= cap ? flashColor : counterColor);
    }

    private PixelPlacedDevice device;

    /// <summary>The shared device card / panel (PixelDeviceCard): what is inside, the last batch's streak; the panel adds this session's totals.</summary>
    public void FillDeviceInfo(PixelDeviceInfo info)
    {
        int cap = Capacity;
        bool streaking = clicker != null && clicker.IsValidTierIndex(streakTier) && streakCount > 0 && Time.time - lastRecycleTime <= streakTimeout;
        if (autoRecycle)
        {
            info.Add("Streak", streaking ? clicker.Tiers[streakTier].displayName.Replace(" Pixels", "") + " x" + StreakMultiplier.ToString("0.0") + " pay" : "none");
            if (inside.Count > 0) info.Add("Recycling", inside.Count.ToString());
        }
        else
        {
            info.Add("Inside", inside.Count + " / " + cap + (inside.Count >= cap ? "  <color=#FFE27F>full - press the button</color>" : ""));
            if (processing) info.Note("<color=#FFC266>Recycling...</color>");
            else if (inside.Count > 0 && showButton) info.Note("<color=#9FB0CC>Press the red button to recycle them.</color>");
            if (lastBatchRun > 1 && clicker != null && clicker.IsValidTierIndex(streakTier))
                info.Add("Last streak", lastBatchRun + " " + clicker.Tiers[streakTier].displayName.Replace(" Pixels", "") + " in a row");
        }
        if (!info.Full) return;
        info.Add("Pays", Mathf.RoundToInt(payoutShare * 100f) + "% of a click, up to x" + StreakCap.ToString("0.0") + " with a streak");
        if (autoRecycle) info.Add("Recycle delay", RecycleDelay.ToString("0.00") + " s");
        else info.Add("Shake time", ProcessSeconds.ToString("0.0") + " s");
        info.Add("Bin size", "x" + SizeFactor.ToString("0.0"));
        info.Heading("This session");
        info.Add("Husks recycled", sessionHusks.ToString());
        if (!autoRecycle) info.Add("Batches", sessionCycles.ToString());
        info.Add("Paid", PixelClicker.FormatNumberShort(sessionPaid) + " (all types)");
    }

    // ---- Processing ----

    private IEnumerator Process()
    {
        DropStrays();   // what is counted is exactly what gets recycled
        processing = true;   // (nothing is captured or let go until the bin is done; a shut lid also stops husks falling in)
        // Every counted husk is frozen into the bin NOW: one still settling would be jostled by the shake (the bin's walls jump into it), could end
        // up below the bin's floor, get let go uncounted and then be caught again after the cycle - a husk that seemed to survive recycling.
        foreach (Rigidbody b in inside) Freeze(b);
        EjectUncounted();
        PixelAudio.Play("ui_click");
        SetGlow(WorkGlow, 1f);
        if (hatchOpen) StartCoroutine(MoveHatch(false));   // the back hatch shuts too while it works
        yield return StretchLid(LidOpenScale, LidClosedScale);

        // Shake, harder and harder, for processSeconds; the band pulses faster with it.
        Vector3 home = transform.localPosition;
        Quaternion homeRot = transform.localRotation;
        float u = Unit(clicker), seconds = ProcessSeconds, tShake = 0f;
        PixelAudio.Play("recycler_shake");   // (its own code-made sound: motor wind-up, rattles getting faster, a clunk at the end)
        while (tShake < seconds)
        {
            tShake += Time.deltaTime;
            float k = Mathf.Clamp01(tShake / seconds), strength = shakeStrength * u * k * k;
            transform.localPosition = home + new Vector3(Random.Range(-1f, 1f), Random.Range(0f, 0.5f), Random.Range(-1f, 1f)) * strength;
            transform.localRotation = homeRot * Quaternion.Euler(Random.Range(-1f, 1f) * 4f * k * k, 0f, Random.Range(-1f, 1f) * 4f * k * k);
            SetGlow(WorkGlow, 0.6f + 0.4f * Mathf.Sin(Time.time * (6f + 30f * k)));
            yield return null;
        }
        transform.localPosition = home;
        transform.localRotation = homeRot;

        DropStrays();   // (one pulled out during the shake is not paid for)
        EjectUncounted();   // (and nothing uncounted is left lying in the bin when the lid opens)
        // Pay out and empty the bin.
        // Each husk pays payoutShare of what a click on that block pays NOW (Value upgrades bought since count), times the same-type STREAK built
        // up in the order the husks went in (a single-colour batch - the Sorting Hopper's - climbs to the cap), never more than maxShareOfClick.
        Dictionary<int, double> paid = new Dictionary<int, double>();
        int count = 0;
        double xpTotal = 0d;
        streakTier = -1; streakCount = 0;
        int bestRun = 0;
        PixelSkills skills = PixelSkills.Instance;
        foreach (Rigidbody b in inside)
        {
            if (b == null) continue;
            OldPixelInfo info = PixelHuskQuery.Get(b).info;
            if (info != null && clicker.IsValidTierIndex(info.tierIndex))
            {
                int tier = info.tierIndex;
                if (tier == streakTier) streakCount++;
                else if (streakTier >= 0) streakCount = Mathf.FloorToInt(streakCount * Mathf.Clamp01(streakKeepOnSwitch));   // another type: the streak shrinks, it does not vanish
                streakTier = tier;
                bestRun = Mathf.Max(bestRun, streakCount + 1);
                double share = System.Math.Min(payoutShare * StreakMultiplier, Mathf.Clamp(maxShareOfClick, 0.1f, 1f));
                double pay = CurrentValue(tier) * share;
                paid.TryGetValue(tier, out double sum);
                paid[tier] = sum + pay;
                double xp = Mathf.Max(0f, xpPerHusk);   // (0 by default: the Recycler is currency only)
                if (xp > 0d && skills != null)
                {
                    if (skills.HuskSkillId(tier) == PixelSkills.StandardId) xp *= standardXpShare > 0f ? standardXpShare : 0.5f;
                    skills.AddHuskXp(tier, xp);
                    xpTotal += xp;
                }
            }
            Destroy(b.gameObject);
            count++;
        }
        if (bestRun > 0) PixelStats.Best("recycler.streak", bestRun);
        lastBatchRun = bestRun;
        if (xpTotal > 0d) PixelStats.Count("recycler.xp", xpTotal);
        inside.Clear();
        restSeconds.Clear();
        capturedAt.Clear();
        RefreshCounter();
        if (count > 0)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, double> kv in paid)
            {
                if (kv.Value <= 0d) continue;
                clicker.AddBonusCurrency(kv.Key, kv.Value);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append('+').Append(PixelClicker.FormatNumberShort(kv.Value)).Append(' ').Append(clicker.Tiers[kv.Key].displayName);
            }
            PixelStats.Count("recycler.husks", count);
            PixelStats.Count("recycler.cycles");
            double paidTotal = 0d;
            foreach (KeyValuePair<int, double> kv in paid) if (kv.Value > 0d) paidTotal += kv.Value;
            PixelStats.Count("recycler.paid", paidTotal);   // (pixels of every type added up)
            sessionHusks += count; sessionCycles++; sessionPaid += paidTotal;
            PixelAudio.Play("purchase");
            PixelHints.Announce("Recycled " + count + " husk" + (count == 1 ? "" : "s") + (sb.Length > 0 ? ": " + sb : "."));
        }
        yield return StretchLid(LidClosedScale, LidOpenScale);
        SetGlow(IdleGlow, 1f);
        processing = false;
    }

    /// <summary>What one click on this pixel type pays right now: its base payout times the current Value (and Ultra / wish) multiplier.</summary>
    private double CurrentValue(int tierIndex)
    {
        PixelClicker.PixelTier t = clicker.Tiers[tierIndex];
        return System.Math.Max(0d, t.amountPerClick) * clicker.PayoutMultiplier(tierIndex);
    }

    /// <summary>First person: click the back hatch (crosshair on it, within reach) to open / shut it, so husks can be tossed in by hand.</summary>
    private void UpdateHatch()
    {
        bool aimed = !processing && PixelFirstPerson.Active && !PixelPauseMenu.IsPaused && HatchAimed();
        UpdateHatchHighlight(aimed);
        if (!aimed || hatchMoving || !PixelInput.LeftPressed() || Time.timeScale <= 0f) return;
        StartCoroutine(MoveHatch(!hatchOpen));
    }

    /// <summary>Is the first person crosshair on the hatch (its door, or the open hole) within reach?</summary>
    private bool HatchAimed()
    {
        if (hatchPivot == null || !PixelFirstPerson.TryGetViewRay(out Ray ray)) return false;
        float reach = Unit(clicker) * Mathf.Max(1f, hatchReachHusks);
        foreach (Collider c in hatchColliders) if (c != null && c.Raycast(ray, out _, reach)) return true;
        // An open hatch has its slats tucked away: the hole itself counts (aim at it to shut it again).
        Vector3 local = transform.InverseTransformPoint(ray.origin), dir = transform.InverseTransformDirection(ray.direction);
        float u = Unit(clicker), zb = (DepthHusks * u + 0.25f * u) * 0.5f;
        if (Mathf.Abs(dir.z) < 0.001f) return false;
        float k = (zb - local.z) / dir.z;
        Vector3 p = local + dir * k;
        return k > 0f && k * transform.lossyScale.x < reach && Mathf.Abs(p.x) <= HatchWidth(u) * 0.5f && p.y >= HatchBottom(u) && p.y <= HatchBottom(u) + HatchHeight(u);
    }

    [Tooltip("Colour the back hatch and its frame glow while the first person crosshair is on it.")]
    [SerializeField] private Color hatchHighlightColor = new Color(1f, 0.9f, 0.4f, 1f);

    private readonly List<Renderer> hatchRenderers = new List<Renderer>();
    private readonly List<Color> hatchBaseColors = new List<Color>();
    private float hatchGlow;

    /// <summary>The hatch's door and frame glow (pulsing gently) while aimed at, fading back when not.</summary>
    private void UpdateHatchHighlight(bool aimed)
    {
        if (hatchRenderers.Count == 0)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Hatch")) { hatchRenderers.Add(r); hatchBaseColors.Add(r.material.color); }
            if (hatchRenderers.Count == 0) return;
        }
        float target = aimed ? 1f : 0f;
        if (Mathf.Approximately(hatchGlow, target) && !aimed) return;
        hatchGlow = Mathf.MoveTowards(hatchGlow, target, Time.unscaledDeltaTime / 0.12f);
        float pulse = hatchGlow * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
        for (int i = 0; i < hatchRenderers.Count; i++)
        {
            Renderer r = hatchRenderers[i];
            if (r == null) continue;
            Material m = r.material;
            m.color = Color.Lerp(hatchBaseColors[i], hatchHighlightColor, pulse * 0.55f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", hatchHighlightColor * (pulse * 0.9f));
            }
        }
    }

    private IEnumerator MoveHatch(bool open)
    {
        if (hatchPivot == null) yield break;
        hatchMoving = true;
        hatchOpen = open;
        PixelAudio.Play("ui_click");
        float from = hatchPivot.localScale.y, to = open ? HatchOpenScale : 1f, t = 0f, seconds = Mathf.Max(0.05f, lidSeconds);
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            Vector3 sc = hatchPivot.localScale;
            sc.y = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            hatchPivot.localScale = sc;
            yield return null;
        }
        hatchMoving = false;
    }

    /// <summary>Stretches the lid out of the inner back edge over the opening (or pulls it back in).</summary>
    private IEnumerator StretchLid(float from, float to)
    {
        if (lidPivot == null) yield break;
        float t = 0f, seconds = Mathf.Max(0.05f, lidSeconds);
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            Vector3 sc = lidPivot.localScale;
            sc.z = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            lidPivot.localScale = sc;
            yield return null;
        }
    }

    /// <summary>Colours the status lamp and the glowing band (green idle, amber working); 'level' dims / brightens the glow.</summary>
    private void SetGlow(Color c, float level)
    {
        Color e = c * Mathf.Max(0f, level) * 1.6f;
        foreach (Renderer r in bandRenderers) Tint(r, c, e);
        Tint(lampRenderer, c, e);
    }

    private static void Tint(Renderer r, Color c, Color emission)
    {
        if (r == null) return;
        Material m = r.material;
        m.color = c;
        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
    }
}
