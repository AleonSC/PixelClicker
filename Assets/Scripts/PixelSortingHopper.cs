// ---- System notes ----
// PixelSortingHopper.cs
//   SORTING HOPPER (PixelConsumables DeviceKind.SortingHopper 22, Utilities; design: docs/GameDesign.md section 6, 'The early husk setup'):
//   the Recycler's partner. REBUILT 2026-10-18 like a MINECRAFT HOPPER: a big square funnel bowl on four legs that stands behind the Recycler and
//   rises well above its wall (bowl rim at BowlTopHusks), narrowing underneath (two tapered blocks and a little spout). Built here by Place /
//   CreateGhost / CreateDisplay with PixelFactoryKit; the placed device is the permanent PixelPermanentDevice stub on the same object - bought, kept,
//   placed, saved, removed like every device.
//   - SNAPS ONTO A RECYCLER'S BACK: placed within 'radius' (the Device entry's Reach, 6 world units) of a free Recycler it latches on behind it
//     (TrySnap, also while placing: the ghost jumps there), turned like the bin, its bowl's front edge just behind the bin's back (BackEdgeLocalZ).
//     SIZE = a default Recycler's (PixelRecycler.DefaultSize) x its OWN Operation 'Size' (SizeMul) - the Recycler's Size upgrades do not grow it
//     (2026-10-18: a maxed bin made it bigger than the cube). The FEED PIPE keeps its own straight shape (2026-10-20: it used to re-aim at the host's
//     drop point, which bent it down onto a Junction Box's top); its mouth reaches over the bin's back wall and the husk is handed to the host there. It follows the bin when the bin is carried. One hopper per Recycler
//     (it OCCUPIES the bin's 'hatch' socket). When the Recycler goes, the hopper stays and latches onto the next free one within reach (every half second).
//     Not on a Recycler it is sized for a default one (UnlatchedSize).
//   - IT HOLDS HUSKS: a husk that drops into the bowl (or is handed in through its back coupling / by the Sweeper) is taken in as DATA (type + amount,
//     PixelHuskHolder FlyIn into the bowl's throat; no fade timer) - up to holdRoom (20). Full = husks just lie in the bowl and fade.
//   - SORTING: every FeedInterval (0.35 s; Operation 'Speed' shortens it) the oldest held husk that can go somewhere goes:
//       the FILTER type -> out of the FEED PIPE that reaches over the Recycler's back wall, dropping into the bin (waits while there is no Recycler or
//       it can't take one);
//       every OTHER type -> straight down out of the BOTTOM SPOUT (2.2 husks off the floor, between the legs; behind the bin, where the Recycler's
//       suction leaves it alone: BehindHopper). (A side tube until 2026-10-18.)
//     No filter chosen = it just holds everything.
//   - THE DEVICE CARD / PANEL (PixelDeviceCard, FillDeviceInfo): hover = filter, feeds a Recycler or not, holding n / room; right-click tap = the panel
//     with what it holds per type, the feed gap, this session's totals and two buttons of its own (PixelDeviceInfo.AddAction): SELECT FILTER (opens
//     PixelSortingHopperWindow, a grid of every unlocked husk type + None) and EMPTY OUT (everything out of the bottom spout).
//   - The filter shows on a little flag on the bowl's back-left corner (tinted its colour, the block spinning on top). A floating down arrow
//     (PixelHoverMarker) shows on hover.
//   Removing it lets everything it holds out of the bottom spout first.
//   OUTPUT NAMES + ON / OFF (2026-10-20): outputs are named by their mouth ring's colour (Green = the feed pipe, Orange / Blue / Pink = the side pipes;
//   OutputName / OutputLabel). Each has a tick box in the device panel (PixelDeviceInfo.AddToggle): OFF hides its pipe and nothing goes out of it
//   (its filter type goes on to the other outputs or the bottom spout); saved as ';O<n>,off'.
//   OUTPUTS (2026-10-19, Operation 'Outputs' +1 a level, 3 levels): side pipes like the feed pipe on the LEFT, RIGHT and then BACK, each with its own filter
//   (a block of that type spins over its mouth); a held husk of a side filter's type drops out of that pipe's mouth (SendOutSide). Order: the front filter
//   (into the host) first, then the side filters, else the bottom spout; no filter on any output = it holds. The device panel has one filter button per output.
//   HOSTS (2026-10-18): it latches onto any IPixelHopperHost (interface below) - the Recycler (occupies 'hatch') and the JUNCTION BOX (occupies b0 / b1;
//   the feed pipe drops into its top hatch and TakeFeed routes the husk like any other). 'Recycler' in the notes above means 'its host'.
//   Unlock: vendorOnly + shopSkillId 'standard' / level 3 = it appears in the shop at Standard level 3 (no vendor sells it); bought for White.
//   SOCKETS (PixelSockets.cs): its own 'back' socket (In) takes belts / hoses: AcceptHusk takes the husk in (refused when full).
//   Saved in PlacedState.extra: 'F,Type' (or 'F,-') then ';Type,amount' per held husk.
//   Stats hopper.husks (into a bin), hopper.rejected (out of the bottom spout), hopper.ejected (Empty out). Tip device_SortingHopper.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// A device a Sorting Hopper latches onto (the Recycler, the Junction Box): the hopper stands behind its back (+z), occupies its HopperSockets and feeds
/// the filter type in through its feed pipe (TakeFeed). A new host implements this; nothing in the hopper changes.
/// </summary>
public interface IPixelHopperHost : IPixelSocketDevice
{
    /// <summary>The name the hopper's card shows ('Recycler').</summary>
    string HostName { get; }
    /// <summary>The sockets a latched hopper occupies (no coupling may go there meanwhile; a coupling there keeps hoppers off).</summary>
    IEnumerable<string> HopperSockets { get; }
    /// <summary>How far behind the host's middle (its own unscaled units, +z) its back ends: the hopper's bowl starts just behind it.</summary>
    float BackEdgeLocalZ { get; }
    /// <summary>Where the hopper's feed pipe lets a husk go (world).</summary>
    Vector3 FeedDropPoint { get; }
    /// <summary>True while the floor's props fade scales the host (the hopper waits).</summary>
    bool Fading { get; }
    /// <summary>May a husk be fed in now?</summary>
    bool CanTakeFeed { get; }
    /// <summary>Feeds one husk in (it leaves the pipe at 'at' moving at 'velocity'). False = refused (the hopper keeps it).</summary>
    bool TakeFeed(int tier, double amount, Vector3 at, Vector3 velocity);
}

public class PixelSortingHopper : MonoBehaviour, IPixelSocketDevice, IPixelHuskRoom, IPixelStrayTarget, IPixelDeviceInfo, IPixelTierPickTarget
{
    [Header("Sorting Hopper")]
    [Tooltip("Seconds between two husks leaving the hopper (into the Recycler or out of the bottom spout).")]
    [SerializeField] private float feedInterval = 0.35f;

    /// <summary>The feed gap before any upgrade (the Operation tooltip starts from it).</summary>
    public const float DefaultFeedInterval = 0.35f;

    /// <summary>The feed gap with the Operation 'Speed' upgrades.</summary>
    private float FeedInterval
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Max(0.06f, feedInterval + (c != null ? c.OperationBonus(PixelConsumables.DeviceKind.SortingHopper, PixelOperationStat.Speed) : 0f));
        }
    }

    /// <summary>The hopper's own Size upgrades (Operation 'Size': 1 = a default Recycler's size). The Recycler's own Size upgrades do NOT grow it.</summary>
    private static float SizeMul
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Max(0.5f, 1f + (c != null ? c.OperationBonus(PixelConsumables.DeviceKind.SortingHopper, PixelOperationStat.Size) : 0f));
        }
    }

    /// <summary>Its size, on a Recycler or not: a default Recycler's (PixelRecycler.DefaultSize) times its own Size upgrades.</summary>
    public static float UnlatchedSize => PixelRecycler.DefaultSize * SizeMul;

    [Min(1)]
    [Tooltip("How many husks the hopper holds. Full = husks just lie in the bowl and fade.")]
    [SerializeField] private int holdRoom = 20;
    [Tooltip("How fast a husk drops out of the bottom spout (world units per second, downwards; Empty out scatters them a little).")]
    [SerializeField] private float rejectPush = 0.9f;
    [Tooltip("How fast a husk leaves the feed pipe into the Recycler (world units per second, downwards).")]
    [SerializeField] private float feedPush = 0.8f;
    [Tooltip("Colour of the floating arrow that shows while the pointer is on the hopper.")]
    [SerializeField] private Color hoverArrowColor = new Color(0.55f, 1f, 0.6f, 1f);

    /// <summary>One held husk (kept as data).</summary>
    public struct Held { public int tier; public double amount; }

    private static readonly List<PixelSortingHopper> all = new List<PixelSortingHopper>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private PixelPlacedDevice device;
    private IPixelHopperHost recycler;       // what it is latched onto - a Recycler or a Junction Box (null = none: filtered husks wait)
    private float snapRange = 6f;
    private Vector3 baseScale = Vector3.one;
    private int filterTier = -1;                  // the FRONT output's filter (into the host)
    private readonly int[] sideFilters = { -1, -1, -1 };   // the side outputs' filters (left, right, back)
    private readonly GameObject[] sideHolders = new GameObject[SideCount];
    private readonly GameObject[] sideModels = new GameObject[SideCount];
    private int sidesShown = -1;
    private float nextSideCheck;

    /// <summary>How many side outputs are unlocked (Operation 'Outputs', the level in use): 0..3.</summary>
    public static int SideOutputs
    {
        get
        {
            PixelConsumables c = PixelFind.Cached<PixelConsumables>();
            return Mathf.Clamp(Mathf.RoundToInt(c != null ? c.OperationBonus(PixelConsumables.DeviceKind.SortingHopper, PixelOperationStat.Outputs) : 0f), 0, SideCount);
        }
    }

    /// <summary>How many outputs it has now: the front feed pipe plus the unlocked side pipes.</summary>
    public int OutputCount => 1 + SideOutputs;

    /// <summary>'Front', 'Left', 'Right', 'Back'.</summary>
    public static string OutputName(int output) => output >= 0 && output < OutputNames.Length ? OutputNames[output] : "?";

    /// <summary>The output's colour (its mouth ring): 0 = the green feed pipe, 1..3 = the side pipes.</summary>
    public static Color OutputColor(int output) => output <= 0 ? Stripe : output - 1 < OutputColors.Length ? OutputColors[output - 1] : Color.white;

    /// <summary>The output's name in its own colour (rich text) for the device panel.</summary>
    public static string OutputLabel(int output) => "<color=#" + ColorUtility.ToHtmlStringRGB(OutputColor(output)) + ">" + OutputName(output) + "</color>";

    // Outputs switched OFF in the device panel (index = output: 0 the feed pipe, 1..3 the side pipes): its pipe is hidden and nothing goes out of it.
    private readonly bool[] outputOff = new bool[4];

    /// <summary>Is this output switched on (the device panel's tick box)?</summary>
    public bool OutputOn(int output) => output >= 0 && output < outputOff.Length && !outputOff[output];

    /// <summary>Switches an output on / off: its pipe shows / hides and husks of its filter go out of it / stay or fall through to the others.</summary>
    public void SetOutputOn(int output, bool on)
    {
        if (output < 0 || output >= outputOff.Length || outputOff[output] == !on) return;
        outputOff[output] = !on;
        sidesShown = -1;   // (ShowSides lays the pipes out again)
        ShowSides();
        PixelAudio.Play("ui_click", on ? 1.2f : 0.85f);
    }

    /// <summary>The filter of output 0 (front, into the host) .. 3 (back); -1 = none.</summary>
    public int FilterOf(int output) => output <= 0 ? filterTier : output - 1 < SideCount ? sideFilters[output - 1] : -1;
    private Transform flagTop;
    private Renderer flagRenderer;
    private GameObject flagModel;
    private float tick, nextFeed, nextLook;
    private readonly List<Held> held = new List<Held>();

    // ---- What the device window reads ----
    public int FilterTier => filterTier;
    public IReadOnlyList<Held> Contents => held;
    public int Room => Mathf.Max(1, holdRoom);
    public bool OnRecycler => recycler != null;
    public PixelClicker Clicker => clicker;

    // ---- Building ----

    // In husk widths (u): the bowl's outer width (square), its floor and rim heights (the Recycler's wall is 3.6 high), the feed pipe's height and reach.
    private const float BowlHusks = 4.4f, BowlFloorHusks = 4.4f, BowlTopHusks = 5.8f, WallT = 0.2f;
    // The side outputs (Operation 'Outputs'): 0 = left (-x), 1 = right (+x), 2 = back (+z); pipes from SideStart to SideEnd husk widths out of the middle.
    private const int SideCount = 3;
    private const float SideStart = 1.4f, SideEnd = 3.3f;
    private static Vector3 SideDir(int k) => k == 0 ? Vector3.left : k == 1 ? Vector3.right : Vector3.forward;
    // Outputs are named by the colour of their mouth ring (2026-10-20): the front feed pipe's ring is the green Stripe, the side pipes' rings are OutputColors.
    private static readonly string[] OutputNames = { "Green", "Orange", "Blue", "Pink" };
    private static readonly Color[] OutputColors = { new Color(1f, 0.75f, 0.3f, 1f), new Color(0.45f, 0.75f, 1f, 1f), new Color(1f, 0.5f, 0.85f, 1f) };
    private const float PipeY = 4.05f, PipeRadius = 0.3f, PipeEndZ = -3.6f, SpoutBottomHusks = 2f;   // (the spout's bottom sits SpoutBottomHusks below the bowl's base: about 2.2 husks off the floor)
    private static readonly Color BodyColor = new Color(0.3f, 0.32f, 0.36f, 1f), Inner = new Color(0.17f, 0.18f, 0.2f, 1f),
                                  Metal = new Color(0.72f, 0.75f, 0.78f, 1f), Dark = new Color(0.1f, 0.11f, 0.12f, 1f),
                                  Stripe = new Color(0.35f, 1f, 0.55f, 1f);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    /// <summary>Builds a placed hopper at 'point' (with its behaviour). 'reach' = how far from a Recycler's back it latches on.</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw, float reach)
    {
        GameObject root = Build(clicker, false, true);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        root.transform.localScale = Vector3.one * UnlatchedSize;   // (sized for a default Recycler until it latches onto one)
        PixelSortingHopper h = root.AddComponent<PixelSortingHopper>();
        h.Init(clicker, reach);
        return root;
    }

    /// <summary>A see-through copy that follows the mouse while placing.</summary>
    public static GameObject CreateGhost(PixelClicker clicker) => Build(clicker, true, false);

    /// <summary>A model for the shop / inventory pictures (no colliders, no behaviour).</summary>
    public static GameObject CreateDisplay(PixelClicker clicker) => Build(clicker, false, false);

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders)
    {
        float u = Unit(clicker);
        float bw = BowlHusks * u, half = bw * 0.5f, yB = BowlFloorHusks * u, yT = BowlTopHusks * u, t = WallT * u, wallH = yT - (yB - t);
        PixelFactoryKit kit = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Sorting Hopper (Preview)" : "Sorting Hopper");
        Transform tr = root.transform;

        // THE BOWL: a square box open on top (its floor and four walls), with a metal rim and a green stripe round the outside.
        kit.Box("Bowl Floor", tr, new Vector3(0f, yB - t * 0.5f, 0f), new Vector3(bw, t, bw), BodyColor, default, colliders, 0.5f, 0.5f);
        float wy = (yB - t + yT) * 0.5f;
        kit.Box("Bowl Wall Front", tr, new Vector3(0f, wy, -(half - t * 0.5f)), new Vector3(bw, wallH, t), BodyColor, default, colliders, 0.5f, 0.5f);
        kit.Box("Bowl Wall Back", tr, new Vector3(0f, wy, half - t * 0.5f), new Vector3(bw, wallH, t), BodyColor, default, colliders, 0.5f, 0.5f);
        kit.Box("Bowl Wall Left", tr, new Vector3(-(half - t * 0.5f), wy, 0f), new Vector3(t, wallH, bw), BodyColor, default, colliders, 0.5f, 0.5f);
        kit.Box("Bowl Wall Right", tr, new Vector3(half - t * 0.5f, wy, 0f), new Vector3(t, wallH, bw), BodyColor, default, colliders, 0.5f, 0.5f);
        kit.Box("Rim Front", tr, new Vector3(0f, yT + t * 0.2f, -(half - t * 0.5f)), new Vector3(bw + t * 0.4f, t * 0.4f, t * 1.4f), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Back", tr, new Vector3(0f, yT + t * 0.2f, half - t * 0.5f), new Vector3(bw + t * 0.4f, t * 0.4f, t * 1.4f), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Left", tr, new Vector3(-(half - t * 0.5f), yT + t * 0.2f, 0f), new Vector3(t * 1.4f, t * 0.4f, bw), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Rim Right", tr, new Vector3(half - t * 0.5f, yT + t * 0.2f, 0f), new Vector3(t * 1.4f, t * 0.4f, bw), Metal, default, false, 0.85f, 0.7f);
        float sy = yB + wallH * 0.35f;
        kit.Box("Stripe Front", tr, new Vector3(0f, sy, -half - t * 0.05f), new Vector3(bw, t * 0.5f, t * 0.1f), Stripe, default, false, 0f, 0.8f, Stripe);
        kit.Box("Stripe Back", tr, new Vector3(0f, sy, half + t * 0.05f), new Vector3(bw, t * 0.5f, t * 0.1f), Stripe, default, false, 0f, 0.8f, Stripe);
        kit.Box("Stripe Left", tr, new Vector3(-half - t * 0.05f, sy, 0f), new Vector3(t * 0.1f, t * 0.5f, bw), Stripe, default, false, 0f, 0.8f, Stripe);
        kit.Box("Stripe Right", tr, new Vector3(half + t * 0.05f, sy, 0f), new Vector3(t * 0.1f, t * 0.5f, bw), Stripe, default, false, 0f, 0.8f, Stripe);
        // Inside: four sloped plates down to a dark square throat in the middle - the funnel look (husks that land on them slide to the throat).
        float throat = 0.7f * u, inner = half - t, run = inner - throat, drop = yT - t * 0.75f - (yB + t * 0.25f), slope = Mathf.Atan2(drop, run) * Mathf.Rad2Deg;
        float len = Mathf.Sqrt(run * run + drop * drop), mid = (inner + throat) * 0.5f, my = yB + t * 0.25f + drop * 0.5f;
        kit.Box("Funnel Plate Front", tr, new Vector3(0f, my, -mid), new Vector3(inner * 2f, t * 0.6f, len), Inner, new Vector3(slope, 0f, 0f), colliders, 0.4f, 0.5f);
        kit.Box("Funnel Plate Back", tr, new Vector3(0f, my, mid), new Vector3(inner * 2f, t * 0.6f, len), Inner, new Vector3(-slope, 0f, 0f), colliders, 0.4f, 0.5f);
        kit.Box("Funnel Plate Left", tr, new Vector3(-mid, my, 0f), new Vector3(len, t * 0.6f, inner * 2f), Inner, new Vector3(0f, 0f, -slope), colliders, 0.4f, 0.5f);
        kit.Box("Funnel Plate Right", tr, new Vector3(mid, my, 0f), new Vector3(len, t * 0.6f, inner * 2f), Inner, new Vector3(0f, 0f, slope), colliders, 0.4f, 0.5f);
        kit.Box("Throat", tr, new Vector3(0f, yB + t * 0.05f, 0f), new Vector3(throat * 2f, t * 0.1f, throat * 2f), Dark, default, false, 0f, 0.3f);

        // UNDER THE BOWL: two tapered blocks and a little spout, the classic hopper shape.
        kit.Box("Body Upper", tr, new Vector3(0f, yB - t - 0.4f * u, 0f), new Vector3(3f * u, 0.8f * u, 3f * u), BodyColor, default, false, 0.5f, 0.5f);
        kit.Box("Body Lower", tr, new Vector3(0f, yB - t - 1.15f * u, 0f), new Vector3(2f * u, 0.7f * u, 2f * u), BodyColor, default, false, 0.5f, 0.5f);
        kit.Box("Spout", tr, new Vector3(0f, yB - t - 1.75f * u, 0f), new Vector3(0.9f * u, 0.5f * u, 0.9f * u), Inner, default, false, 0.5f, 0.5f);
        // Four legs from the bowl's corners down to the floor.
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 at = new Vector3(sx * (half - t), 0f, sz * (half - t));
                kit.Box("Leg", tr, at + Vector3.up * (yB - t) * 0.5f, new Vector3(t * 1.2f, yB - t, t * 1.2f), Metal, default, false, 0.85f, 0.7f);
                kit.Box("Foot", tr, at + Vector3.up * t * 0.1f, new Vector3(t * 2f, t * 0.2f, t * 2f), Dark, default, false, 0.5f, 0.6f);
            }

        // THE FEED PIPE (towards -z, the Recycler): out of the bowl's base, over the bin's back wall, then a short mouth pointing down into the bin.
        float pipeStart = -1.4f * u, pipeEnd = PipeEndZ * u, py = PipeY * u, pr = PipeRadius * u;
        kit.Cyl("Feed Pipe", tr, new Vector3(0f, py, (pipeStart + pipeEnd) * 0.5f), pr, pipeStart - pipeEnd, Metal, new Vector3(90f, 0f, 0f), 0.85f, 0.6f);
        kit.Cyl("Feed Mouth", tr, new Vector3(0f, py - pr * 0.9f, pipeEnd), pr * 1.1f, pr * 2.6f, Metal, default, 0.85f, 0.6f);
        kit.Cyl("Feed Mouth Ring", tr, new Vector3(0f, py - pr * 2.2f, pipeEnd), pr * 1.3f, pr * 0.4f, Stripe, default, 0f, 0.8f);
        // THE SIDE OUTPUTS (Operation 'Outputs': left, right, back): pipes like the feed pipe, each in its own holder (shown only when unlocked;
        // a husk of that output's filter drops out of its mouth). Hidden on the ghost and the shop picture.
        for (int k = 0; k < SideCount; k++)
        {
            GameObject holder = new GameObject("Side Output " + k);
            holder.transform.SetParent(tr, false);
            holder.transform.localRotation = Quaternion.LookRotation(SideDir(k), Vector3.up);   // (its local +z points out of that side)
            Transform ht = holder.transform;
            kit.Cyl("Side Pipe", ht, new Vector3(0f, py, (SideStart + SideEnd) * 0.5f * u), pr, (SideEnd - SideStart) * u, Metal, new Vector3(90f, 0f, 0f), 0.85f, 0.6f);
            kit.Cyl("Side Mouth", ht, new Vector3(0f, py - pr * 0.9f, SideEnd * u), pr * 1.1f, pr * 2.6f, Metal, default, 0.85f, 0.6f);
            kit.Cyl("Side Mouth Ring", ht, new Vector3(0f, py - pr * 2.2f, SideEnd * u), pr * 1.3f, pr * 0.4f, OutputColors[k], default, 0f, 0.8f);
            GameObject top = new GameObject("Side Filter");
            top.transform.SetParent(ht, false);
            top.transform.localPosition = new Vector3(0f, py + pr * 2.8f, SideEnd * u);
            holder.SetActive(false);
        }

        // THE BOTTOM SPOUT's mouth: everything that is not the filter type drops out of here, straight down between the legs (the spout stays this high off the floor).
        kit.Box("Spout Mouth", tr, new Vector3(0f, yB - t - SpoutBottomHusks * u + 0.04f * u, 0f), new Vector3(0.62f * u, 0.1f * u, 0.62f * u), Dark, default, false, 0f, 0.3f);

        // The filter flag on a pole at the bowl's back-left corner: tinted the chosen type's colour, with a little block of that type spinning on top.
        float poleH = u * 2f;
        Vector3 poleBase = new Vector3(-(half - t * 0.5f), yT, half - t * 0.5f);
        kit.Box("Flag Pole", tr, poleBase + new Vector3(0f, poleH * 0.5f, 0f), new Vector3(t * 0.5f, poleH, t * 0.5f), Metal, default, false, 0.85f, 0.7f);
        kit.Box("Flag", tr, poleBase + new Vector3(u * 0.55f, poleH - u * 0.35f, 0f), new Vector3(u * 1f, u * 0.6f, t * 0.25f), Color.white, default, false, 0.1f, 0.5f);
        GameObject flagTop = new GameObject("Flag Top");
        flagTop.transform.SetParent(tr, false);
        flagTop.transform.localPosition = poleBase + new Vector3(0f, poleH + u * 0.45f, 0f);
        return root;
    }

    private void Init(PixelClicker owner, float reach)
    {
        clicker = owner;
        snapRange = Mathf.Max(0.5f, reach);
        float su = Unit(clicker);
        sockets = new[] { new PixelSocket("back", new Vector3(0f, 0f, BowlHusks * su * 0.5f + 0.35f * su), Vector3.forward, PixelSocketFlow.Intake) };
        baseScale = transform.localScale;
        flagTop = transform.Find("Flag Top");
        Transform flag = transform.Find("Flag");
        flagRenderer = flag != null ? flag.GetComponent<Renderer>() : null;
        SetFilter(-1);
        for (int k = 0; k < SideCount; k++) { Transform hk = transform.Find("Side Output " + k); sideHolders[k] = hk != null ? hk.gameObject : null; }
        PixelHoverMarker.Attach(transform, new Vector3(0f, BowlTopHusks * su + 1.1f * su, 0f), su, hoverArrowColor, PointerOnHopper);   // (the numbers are on the device card)
    }

    /// <summary>Is a pointer ray on the hopper (its parts, as the device's own hover / remove check sees them)?</summary>
    private bool PointerOnHopper(Ray ray)
    {
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        return device != null && device.HitTest(ray, out _);
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); PixelCouplings.Register(this); }
    private void OnDisable() { all.Remove(this); PixelCouplings.Unregister(this); }

    // ---- Socket (PixelSockets.cs): the BACK, an In socket - a belt / hose ending at a coupling there hands husks in ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => BodyColor;

    public int HuskRoom(PixelSocket socket) => Mathf.Max(0, Room - held.Count);

    /// <summary>A husk comes in through the back coupling (or from the Sweeper): taken in at once. Refused when full.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk) => husk != null && clicker != null && held.Count < Room && TakeIn(husk);

    private int sessionFed, sessionRejected, sessionEjected, sessionSided;   // since it was placed / loaded (the device panel)

    /// <summary>The shared device card / panel: filter, the Recycler it feeds, what it holds; the panel adds each type held, the feed gap, this session's totals and its buttons.</summary>
    public void FillDeviceInfo(PixelDeviceInfo info)
    {
        int outputs = OutputCount;
        for (int o = 0; o < outputs; o++)
        {
            int f = FilterOf(o);
            string what = clicker != null && clicker.IsValidTierIndex(f) ? Name(f) : o == 0 && outputs == 1 ? "none (it just holds)" : "none";
            info.Add(outputs > 1 ? OutputLabel(o) + " output" : "Filter", OutputOn(o) ? what : what + " <color=#FF8A7A>(off)</color>");
        }
        info.Add("Feeds", recycler != null ? "the " + recycler.HostName + " in front of it" : "<color=#FFD27F>nothing - the filter type waits</color>");
        info.Add("Holding", held.Count + " / " + Room);
        info.Progress = held.Count / (float)Room;
        info.ProgressLabel = held.Count >= Room ? "full" : "";
        if (!info.Full) return;
        SortedDictionary<int, int> counts = new SortedDictionary<int, int>();
        foreach (Held h in held) { counts.TryGetValue(h.tier, out int n); counts[h.tier] = n + 1; }
        foreach (KeyValuePair<int, int> kv in counts) if (clicker.IsValidTierIndex(kv.Key)) info.Add("  " + Name(kv.Key), "x" + kv.Value + (kv.Key == filterTier ? "  (filter)" : ""));
        info.Add("Feed gap", FeedInterval.ToString("0.00") + " s");
        info.Heading("This session");
        info.Add("Fed through the pipe", sessionFed.ToString());
        if (outputs > 1) info.Add("Out of the side pipes", sessionSided.ToString());
        info.Add("Out of the bottom spout", (sessionRejected + sessionEjected).ToString());
        for (int o = 0; o < outputs; o++)
        {
            int output = o, f = FilterOf(o);
            string what = clicker.IsValidTierIndex(f) ? Name(f) : "no filter";
            info.AddToggle(OutputLabel(o) + " output  <size=80%><color=#9FB0CC>" + what + "</color></size>", OutputOn(o), on => SetOutputOn(output, on));
        }
        if (outputs == 1) info.AddAction("Select filter", () => PixelSortingHopperWindow.Open(this, 0));
        else for (int o = 0; o < outputs; o++) { int output = o; info.AddAction(OutputLabel(o) + " filter", () => PixelSortingHopperWindow.Open(this, output)); }
        info.AddAction("Empty out", EmptyOut, ButtonRole.Danger);
    }

    private string Name(int tier) => clicker.Tiers[tier].displayName.Replace(" Pixels", "");

    // ---- The picker window (PixelSortingHopperWindow, IPixelTierPickTarget): slot = output ----
    public int PickedTier(int slot) => FilterOf(slot);
    public void SetPickedTier(int slot, int tier) => SetFilter(slot, tier);
    public string PickTitle(int slot) => OutputCount > 1 ? OutputName(slot) + " filter" : "Select filter";
    public string PickHint(int slot) => "Husks of this type leave through this output; types no output filters drop out of the bottom spout.";
    public bool PickAllowsNone => true;
    public bool PickAllTiers => false;
    public bool PickAlive => this != null;

    private void OnDestroy()
    {
        PixelCouplings.Unregister(this);
        Vacate();
        PixelSortingHopperWindow.Forget(this);
    }

    // ---- Snapping onto a host (a Recycler, a Junction Box: IPixelHopperHost) ----

    private static bool Alive(IPixelHopperHost h) => h != null && !(h is Object o && o == null) && h.SocketRoot != null;

    /// <summary>Is this host free for a hopper (none latched on, no coupling on its hopper sockets, not ending)? 'except' = the asking hopper.</summary>
    private static bool Free(IPixelHopperHost r, PixelSortingHopper except)
    {
        if (!Alive(r) || Ending(r)) return false;
        foreach (string id in r.HopperSockets) if (PixelCoupling.On(r, id) != null) return false;   // (a coupling there takes the hopper's place)
        foreach (PixelSortingHopper h in all) if (h != null && h != except && h.recycler == r) return false;
        return true;
    }

    private static bool Ending(IPixelHopperHost r)
    {
        PixelPlacedDevice d = r.SocketRoot.GetComponent<PixelPlacedDevice>();
        return d != null && d.IsEnding;
    }

    private void Vacate()
    {
        if (!Alive(recycler)) return;
        foreach (string id in recycler.HopperSockets) PixelCouplings.Vacate(recycler, id, this);
    }

    /// <summary>Where a hopper sits on this host's back (position, turn and size): the bowl's front edge just behind the host's back (+z).</summary>
    private static void SpotOn(PixelClicker clicker, IPixelHopperHost r, out Vector3 point, out Quaternion rotation, out float size)
    {
        float u = Unit(clicker);
        Transform rt = r.SocketRoot;
        size = UnlatchedSize;   // (its OWN size: a bigger Recycler moves it back, it does not grow it - the feed pipe re-aims to reach over the bin's wall instead)
        Vector3 back = rt.TransformDirection(Vector3.forward); back.y = 0f;
        point = rt.TransformPoint(new Vector3(0f, 0f, r.BackEdgeLocalZ)) + back.normalized * (BowlHusks * 0.5f + 0.3f) * u * size;
        point.y = rt.position.y;
        rotation = rt.rotation;
    }

    /// <summary>
    /// While placing: the free host (Recycler / Junction Box) whose back spot is within 'reach' of the pointer's floor point. The ghost jumps there (position, turn, size).
    /// False = none near: the hopper is placed where it is (it latches on later if a host is put down near it).
    /// </summary>
    public static bool TrySnap(PixelClicker clicker, Vector3 point, float reach, out Vector3 snapPoint, out float snapYaw, out float size)
    {
        snapPoint = point; snapYaw = 0f; size = 1f;
        IPixelHopperHost best = Nearest(clicker, point, reach, null);
        if (best == null) return false;
        SpotOn(clicker, best, out snapPoint, out Quaternion rot, out size);
        snapYaw = rot.eulerAngles.y;
        return true;
    }

    private static IPixelHopperHost Nearest(PixelClicker clicker, Vector3 point, float reach, PixelSortingHopper except)
    {
        IPixelHopperHost best = null;
        float bestD = reach * reach;
        foreach (IPixelSocketDevice d in PixelCouplings.Devices)
        {
            if (!(d is IPixelHopperHost r) || !Free(r, except) || r.Fading) continue;
            SpotOn(clicker, r, out Vector3 spot, out _, out _);
            Vector3 dv = spot - point;
            dv.y = 0f;
            if (dv.sqrMagnitude <= bestD) { bestD = dv.sqrMagnitude; best = r; }
        }
        return best;
    }

    // ---- Every frame ----

    private void Update()
    {
        if (clicker == null) return;
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        if (device != null && device.IsEnding)   // being removed (shrinking away): what it holds is let out first
        {
            if (held.Count > 0 && !PixelCalm.InMenu) EmptyOut();
            return;
        }
        if (flagModel != null) flagModel.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        foreach (GameObject m in sideModels) if (m != null) m.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        if (Time.unscaledTime >= nextSideCheck) { nextSideCheck = Time.unscaledTime + 0.5f; ShowSides(); }

        bool fading = (transform.localScale - baseScale).sqrMagnitude > 0.0001f;   // the floor's props fade (main menu, after a load): wait
        if (fading || PixelCalm.InMenu || Time.timeScale <= 0f) return;

        KeepLatched();
        Feed();

        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.1f;
        Catch();
    }

    /// <summary>Latches onto a free Recycler within reach (every half second when it has none) and follows it when it is carried.</summary>
    private void KeepLatched()
    {
        if (recycler != null && (!Alive(recycler) || Ending(recycler) || !((Behaviour)recycler).isActiveAndEnabled)) { Vacate(); recycler = null; AimPipe(DefaultPipeExit(Unit(clicker))); }
        if (recycler == null)
        {
            if (Time.time < nextLook) return;
            nextLook = Time.time + 0.5f;
            recycler = Nearest(clicker, transform.position, snapRange, this);
            if (recycler == null) return;
            foreach (string id in recycler.HopperSockets) PixelCouplings.Occupy(recycler, id, this);   // (one hopper per host; no coupling goes on those sockets meanwhile)
            PixelAudio.Play("device_place");
        }
        if (recycler.Fading) return;
        SpotOn(clicker, recycler, out Vector3 spot, out Quaternion rot, out float size);
        if ((transform.position - spot).sqrMagnitude > 0.000001f || Quaternion.Angle(transform.rotation, rot) > 0.05f) transform.SetPositionAndRotation(spot, rot);
        if (Mathf.Abs(baseScale.x - size) > 0.0001f) { baseScale = Vector3.one * size; transform.localScale = baseScale; }
        AimPipe(DefaultPipeExit(Unit(clicker)));   // (2026-10-20: the pipes keep their own shape - they no longer bend towards the host's drop point)
    }

    private Transform pipe, mouth, mouthRing;
    private Vector3 pipeExit = new Vector3(float.NaN, 0f, 0f);   // where a husk leaves the feed pipe (local); NaN = not aimed yet

    /// <summary>Where the feed pipe ends when it is not on a Recycler (as built), in local units.</summary>
    private Vector3 DefaultPipeExit(float u) => new Vector3(0f, (PipeY - PipeRadius * 3f) * u, PipeEndZ * u);

    /// <summary>
    /// Re-aims the feed pipe so its mouth lets husks go at 'exit' (local): the pipe runs from the bowl's base to just above it (sloping up when the bin is
    /// taller than the hopper), the mouth pointing down. Only redrawn when the exit moves.
    /// </summary>
    private void AimPipe(Vector3 exit)
    {
        if ((exit - pipeExit).sqrMagnitude < 1e-8f) return;
        pipeExit = exit;
        if (pipe == null) { pipe = transform.Find("Feed Pipe"); mouth = transform.Find("Feed Mouth"); mouthRing = transform.Find("Feed Mouth Ring"); }
        if (pipe == null) return;
        float u = Unit(clicker), pr = PipeRadius * u;
        Vector3 start = new Vector3(0f, PipeY * u, -1.4f * u), end = exit + Vector3.up * pr * 3f;
        Vector3 dir = end - start;
        float len = Mathf.Max(0.01f, dir.magnitude);
        pipe.localPosition = (start + end) * 0.5f;
        pipe.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
        pipe.localScale = new Vector3(pr * 2f, len * 0.5f, pr * 2f);
        if (mouth != null) mouth.localPosition = end - Vector3.up * pr * 0.9f;
        if (mouthRing != null) mouthRing.localPosition = end - Vector3.up * pr * 2.2f;
    }

    // ---- Holding ----

    /// <summary>Husks that dropped into the bowl are taken in (while there is room).</summary>
    private void Catch()
    {
        if (held.Count >= Room) return;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0 && held.Count < Room; i--)
        {
            if (i >= pixels.Count) continue;
            Rigidbody b = pixels[i];
            if (b == null || !InBowl(b.position) || !PixelHuskHolder.Takeable(clicker, b)) continue;
            TakeIn(b);
        }
    }

    /// <summary>Is this point inside the bowl (in the hopper's own units)?</summary>
    private bool InBowl(Vector3 world)
    {
        float u = Unit(clicker), inner = (BowlHusks * 0.5f - WallT) * u;
        Vector3 p = transform.InverseTransformPoint(world);
        return Mathf.Abs(p.x) <= inner && Mathf.Abs(p.z) <= inner && p.y >= (BowlFloorHusks - 0.3f) * u && p.y <= (BowlTopHusks + 0.1f) * u;
    }

    /// <summary>Takes a husk in as data (type + amount): off the floor's husk list, shrinking into the bowl's throat.</summary>
    private bool TakeIn(Rigidbody body)
    {
        OldPixelInfo info = PixelHuskQuery.Get(body).info;
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || !clicker.ReleaseOldPixel(body, false)) return false;
        held.Add(new Held { tier = info.tierIndex, amount = info.amount });
        float u = Unit(clicker);
        StartCoroutine(PixelHuskHolder.FlyIn(body.gameObject, null, transform.TransformPoint(new Vector3(0f, BowlFloorHusks * u, 0f)), false));
        PixelAudio.Play("click");
        PixelSortingHopperWindow.Changed(this);
        return true;
    }

    // ---- Sorting ----

    /// <summary>Every FeedInterval: the oldest held husk that can go somewhere goes - the filter type into the Recycler, any other out of the bottom spout.</summary>
    private void Feed()
    {
        if (held.Count == 0 || Time.time < nextFeed) return;
        int sides = SideOutputs;
        bool anyFilter = filterTier >= 0 && OutputOn(0);
        for (int k = 0; k < sides; k++) if (sideFilters[k] >= 0 && OutputOn(k + 1)) anyFilter = true;
        if (!anyFilter) return;   // (no filter chosen on any output: it just holds)
        bool binReady = Alive(recycler) && !recycler.Fading && recycler.CanTakeFeed;
        for (int i = 0; i < held.Count; i++)
        {
            Held h = held[i];
            bool filtered = h.tier == filterTier && OutputOn(0);
            int side = -1;
            if (!filtered) for (int k = 0; k < sides; k++) if (sideFilters[k] == h.tier && OutputOn(k + 1)) { side = k; break; }
            if (filtered && !binReady) continue;   // (it waits for the bin)
            if (filtered)
            {
                if (!SendIntoBin(h)) continue;   // (the host refused it after all: it stays held)
                PixelStats.Count("hopper.husks"); sessionFed++;
            }
            else if (side >= 0) { SendOutSide(side, h); PixelStats.Count("hopper.sided"); sessionSided++; }
            else { SendOutBottom(h, 1f); PixelStats.Count("hopper.rejected"); sessionRejected++; }
            held.RemoveAt(i);
            nextFeed = Time.time + FeedInterval;
            PixelSortingHopperWindow.Changed(this);
            return;
        }
    }

    /// <summary>One husk out of the feed pipe into the host (the Recycler lets it fall into the bin, the Junction Box routes it). False = refused.</summary>
    private bool SendIntoBin(Held h)
    {
        float u = Unit(clicker);
        Vector3 at = transform.TransformPoint(float.IsNaN(pipeExit.x) ? DefaultPipeExit(u) : pipeExit);
        return Alive(recycler) && recycler.TakeFeed(h.tier, h.amount, at, Vector3.down * feedPush);
    }

    /// <summary>A husk of a side output's filter drops out of that side pipe's mouth.</summary>
    private void SendOutSide(int k, Held h)
    {
        float u = Unit(clicker);
        Vector3 local = SideDir(k) * SideEnd * u + Vector3.up * (PipeY - PipeRadius * 3f) * u;
        clicker.SpawnStoredPixel(h.tier, h.amount, transform.TransformPoint(local), Vector3.down * feedPush);
    }

    /// <summary>Shows the unlocked side pipes (the Outputs level in use can change at any time: bought, or turned down in the device panel).</summary>
    private void ShowSides()
    {
        int n = SideOutputs;
        if (n == sidesShown) return;
        sidesShown = n;
        for (int k = 0; k < SideCount; k++) if (sideHolders[k] != null) sideHolders[k].SetActive(k < n && OutputOn(k + 1));
        foreach (string part in new[] { "Feed Pipe", "Feed Mouth", "Feed Mouth Ring" })
        {
            Transform p = transform.Find(part);
            if (p != null && p.gameObject.activeSelf != OutputOn(0)) p.gameObject.SetActive(OutputOn(0));
        }
    }

    private void SendOutBottom(Held h, float spread)
    {
        float u = Unit(clicker);
        Vector3 at = transform.TransformPoint(new Vector3(0f, (BowlFloorHusks - WallT - SpoutBottomHusks - 0.3f) * u, 0f));   // (just under the bottom spout)
        Vector3 v = transform.TransformDirection(new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f))) * rejectPush * 0.35f * spread + Vector3.down * rejectPush;
        clicker.SpawnStoredPixel(h.tier, h.amount, at, v);
    }

    /// <summary>Everything it holds goes out of the bottom spout at once (the window's 'Empty out', and when the hopper is removed).</summary>
    public void EmptyOut()
    {
        if (clicker == null || held.Count == 0) return;
        int n = held.Count;
        foreach (Held h in held) SendOutBottom(h, 2.5f);
        held.Clear();
        PixelStats.Count("hopper.ejected", n);
        sessionEjected += n;
        PixelAudio.Play("device_place");
        PixelSortingHopperWindow.Changed(this);
    }

    /// <summary>Chooses one output's filter (0 = front, into the host; 1..3 = left, right, back). A side filter shows as a block over its pipe.</summary>
    public void SetFilter(int output, int tier)
    {
        if (output <= 0) { SetFilter(tier); return; }
        int k = output - 1;
        if (k >= SideCount) return;
        sideFilters[k] = clicker != null && clicker.IsValidTierIndex(tier) ? tier : -1;
        if (sideModels[k] != null) Destroy(sideModels[k]);
        sideModels[k] = null;
        Transform top = sideHolders[k] != null ? sideHolders[k].transform.Find("Side Filter") : null;
        if (sideFilters[k] >= 0 && top != null) sideModels[k] = clicker.CreateDisplayPixel(sideFilters[k], top, Unit(clicker) * 0.45f);
        if (sideModels[k] != null) sideModels[k].transform.localPosition = Vector3.zero;
        PixelSortingHopperWindow.Changed(this);
    }

    /// <summary>Chooses the FRONT output's filter (-1 = none). The flag shows it.</summary>
    public void SetFilter(int tier)
    {
        filterTier = clicker != null && clicker.IsValidTierIndex(tier) ? tier : -1;
        if (flagRenderer != null)
        {
            Color c = filterTier >= 0 ? clicker.Tiers[filterTier].UIColor : Color.gray;
            Material m = flagRenderer.material;
            m.color = c;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.35f); }
        }
        if (flagModel != null) Destroy(flagModel);
        flagModel = null;
        if (filterTier >= 0 && flagTop != null) flagModel = clicker.CreateDisplayPixel(filterTier, flagTop, Unit(clicker) * 0.6f);
        if (flagModel != null) flagModel.transform.localPosition = Vector3.zero;
        PixelSortingHopperWindow.Changed(this);
    }

    // ---- Saving (PixelConsumables puts this in PlacedState.extra) ----

    /// <summary>'F,Type' (or 'F,-') then ';Type,amount' per held husk (by pixel type name, so it survives tier list changes).</summary>
    public string ExportState()
    {
        if (clicker == null) return "";
        StringBuilder sb = new StringBuilder("F,");
        sb.Append(filterTier >= 0 ? clicker.Tiers[filterTier].type.ToString() : "-");
        for (int k = 0; k < SideCount; k++)
            if (clicker.IsValidTierIndex(sideFilters[k])) sb.Append(";S").Append(k).Append(',').Append(clicker.Tiers[sideFilters[k]].type);
        for (int o = 0; o < outputOff.Length; o++) if (outputOff[o]) sb.Append(";O").Append(o).Append(",off");
        foreach (Held h in held)
            if (clicker.IsValidTierIndex(h.tier)) sb.Append(';').Append(clicker.Tiers[h.tier].type).Append(',').Append(h.amount.ToString("R", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    public void ImportState(string data)
    {
        if (string.IsNullOrEmpty(data) || clicker == null) return;
        held.Clear();
        foreach (string entry in data.Split(';'))
        {
            string[] f = entry.Split(',');
            if (f.Length < 2) continue;
            if (f[0] == "F")
            {
                if (System.Enum.TryParse(f[1], out PixelClicker.PixelType ft)) SetFilter(clicker.IndexOf(ft));
                continue;
            }
            if (f[0].Length == 2 && f[0][0] == 'O' && int.TryParse(f[0].Substring(1), out int offOutput))
            {
                SetOutputOn(offOutput, false);
                continue;
            }
            if (f[0].Length == 2 && f[0][0] == 'S' && int.TryParse(f[0].Substring(1), out int side))
            {
                if (System.Enum.TryParse(f[1], out PixelClicker.PixelType st)) SetFilter(side + 1, clicker.IndexOf(st));
                continue;
            }
            if (!System.Enum.TryParse(f[0], out PixelClicker.PixelType type)) continue;
            int tier = clicker.IndexOf(type);
            if (tier < 0) continue;
            double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double amount);
            if (held.Count < Room) held.Add(new Held { tier = tier, amount = amount });
        }
    }
}
