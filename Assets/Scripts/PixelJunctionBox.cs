// ---- System notes ----
// PixelJunctionBox.cs
//   JUNCTION BOX (PixelConsumables DeviceKind.JunctionBox 26; design agreed in the Design Discussion chat, built 2026-10-18; docs/systems/PixelCouplings.md):
//   a closed box like the Recycler's (PixelFactoryKit: plinth, walls, metal pillars and rim, a teal glowing band, a lid with a junction cross and a small
//   top hatch; the shared carry handles on its +/-x ends) with EIGHT SOCKETS - two on each side (f0 f1 front -z, b0 b1 back +z, l0 l1 left -x, r0 r1 right +x),
//   all flow Both, kind Husk, autoDirection. Placed and saved as a permanent device (the PixelChipperDevice stub next to it; PlacedState.extra = filters + buffer).
//   - DIRECTION from what is attached (UpdateDirections, every routeSeconds): a belt that ENDS at a coupling's port or a hose that PUSHES into it = In; a belt
//     that STARTS at it or a hose that PULLS from it = Out; nothing = idle (PixelCoupling.SetAuto; its arrow shows it, idle shows none).
//   - TOP OPENING (2026-10-18): a square hole in the lid (HoleHusks) takes husks that drop in (and any husk resting on the lid: nothing piles up on top), and a gentle suction (suckRadiusHusks / suckHeightHusks /
//     suckStrength) draws husks just above the box towards it (FixedUpdate, Swallow).
//   - IN = DELETED, OUT = NEW (2026-10-18): every husk that goes in (a coupling, the top opening, the Sweeper) is deleted - it shrinks into the box - and its
//     type + value come out as a NEW husk spawned at an output coupling onto its belt (TakeData / SpawnOut), or wait in the buffer.
//   - ROUTING a husk that comes in (AcceptHusk, from a belt / hose / the Sweeper / a Sorting Hopper's feed pipe / the top opening):
//       1) to an OUT coupling whose FILTER is the husk's type (it waits in the buffer while that output is full);
//       2) otherwise ROUND ROBIN across the unfiltered Out couplings (PixelCouplings.Emit onto the belt starting there, only while it has room);
//       3) otherwise into the BUFFER (bufferSize, 40): kept as data, retried every routeSeconds; a hose pulling from an Out coupling takes from it
//          (IPixelHuskSource: the filter type there, or any type no filtered output wants). Buffer full = refused (a belt throws it on, as at any full device).
//   - FILTERS (one per coupling, used while it is an output): drop a husk you were holding onto a coupling (within filterDropReach) to set it to that type;
//     left-click / scroll a coupling to step through the unlocked types (and none). A small block of the filter type spins over the coupling; hovering the
//     coupling shows the block card (PixelBlockTooltip.ShowFor). Clicks on a coupling are claimed (PixelPointerClaim.ClaimButton).
//   - A SORTING HOPPER latches onto its back like onto a Recycler (IPixelHopperHost: it occupies b0 / b1, its feed pipe reaches over the top opening -> TakeFeed
//     routes the husk). The SWEEPER treats it as a container with room (IPixelStrayTarget + IPixelHuskRoom = buffer room).
//   - Device card / panel (FillDeviceInfo): buffer n / size, each coupling's way and filter; panel actions Clear filters / Empty buffer (the buffer's husks
//     pop out on top).
//   OVERLAP: it covers the old tester Splitter (round robin; removed 2026-10-18) and much of the tester Belt Sorter (one type to one side) - see PixelCouplings.md.
//   Stats junction.routed, junction.buffered.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public class PixelJunctionBox : MonoBehaviour, IPixelSocketDevice, IPixelHuskRoom, IPixelHuskSource, IPixelStrayTarget, IPixelHopperHost, IPixelDeviceInfo
{
    [Header("Junction Box")]
    [Min(0)]
    [Tooltip("How many husks it keeps when no output can take them (kept as data; a hose pulling from an Out coupling takes from here). Full = it refuses more.")]
    [SerializeField] private int bufferSize = 40;
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of the couplings' directions and the buffer's retries.")]
    [SerializeField] private float routeSeconds = 0.25f;
    [Min(0.2f)]
    [Tooltip("A husk the player was holding that lands this close to a coupling's port (husk widths) sets that coupling's filter to its type.")]
    [SerializeField] private float filterDropReach = 1.2f;
    [Header("Top opening")]
    [Tooltip("The square opening in the lid takes husks that fall into it, and gently draws in husks just above the box.")]
    [SerializeField] private bool topOpening = true;
    [Min(0f)]
    [Tooltip("How far from the opening's middle (husk widths, sideways) the gentle suction reaches, for husks above the lid.")]
    [SerializeField] private float suckRadiusHusks = 3.5f;
    [Min(0.5f)]
    [Tooltip("How high above the lid (husk widths) the suction reaches.")]
    [SerializeField] private float suckHeightHusks = 5f;
    [Min(0f)]
    [Tooltip("How strong the pull towards the opening is (an acceleration in husk widths per second squared; slight on purpose).")]
    [SerializeField] private float suckStrength = 7f;

    [Tooltip("Colour of the glowing band (it flashes brighter for a moment when a husk is routed).")]
    [SerializeField] private Color bandColor = new Color(0.3f, 0.95f, 0.95f, 1f);

    private struct Held { public int tier; public double amount; }

    private static readonly List<PixelJunctionBox> all = new List<PixelJunctionBox>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    /// <summary>Every placed Junction Box's root (the tester lists them; they are saved and removed as placed devices).</summary>
    public static IEnumerable<GameObject> Placed() { foreach (PixelJunctionBox j in all) if (j != null) yield return j.gameObject; }

    private PixelClicker clicker;
    private PixelCarryHandles carry;
    private PixelPlacedDevice device;
    private Vector3 baseScale = Vector3.one;
    private PixelSocket[] sockets = new PixelSocket[0];
    private readonly Dictionary<string, int> filters = new Dictionary<string, int>();      // socket id -> filter tier (-1 / missing = none)
    private readonly Dictionary<string, GameObject> filterModels = new Dictionary<string, GameObject>();
    private readonly List<Held> buffer = new List<Held>();
    private readonly HashSet<Rigidbody> dropsSeen = new HashSet<Rigidbody>();
    private int roundRobin = -1;
    private float nextRoute, bandFlash;
    private int sessionRouted;
    private readonly List<Renderer> bandRenderers = new List<Renderer>();

    // ---- Building ----

    // In husk widths (u): the box's side (square) and height, and how far apart the two sockets on a side are.
    private const float SideHusks = 4f, HeightHusks = 2.6f, SocketSpread = 1f, HoleHusks = 1.6f;
    private static readonly Color BodyColor = new Color(0.22f, 0.36f, 0.42f, 1f), LidColor = new Color(0.18f, 0.3f, 0.35f, 1f),
                                  Metal = new Color(0.72f, 0.75f, 0.78f, 1f), Dark = new Color(0.12f, 0.14f, 0.15f, 1f);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    /// <summary>Builds a placed Junction Box at 'point' (with its behaviour and carry handles).</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw)
    {
        GameObject root = Build(clicker, false, true);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelJunctionBox j = root.AddComponent<PixelJunctionBox>();
        j.Init(clicker);
        j.carry = root.AddComponent<PixelCarryHandles>();
        j.carry.Init(() => clicker != null ? clicker.TargetCamera : null);
        return root;
    }

    /// <summary>A see-through copy that follows the mouse while placing.</summary>
    public static GameObject CreateGhost(PixelClicker clicker) => Build(clicker, true, false);

    /// <summary>A model for the shop / inventory pictures (no colliders, no behaviour).</summary>
    public static GameObject CreateDisplay(PixelClicker clicker) => Build(clicker, false, false);

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders)
    {
        float u = Unit(clicker);
        float w = SideHusks * u, h = HeightHusks * u, t = 0.25f * u, half = w * 0.5f;
        PixelFactoryKit kit = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Junction Box (Preview)" : "Junction Box");
        Transform tr = root.transform;
        Color glow = new Color(0.3f, 0.95f, 0.95f, 1f);

        // Plinth, feet, the closed body (four walls and a floor) and the lid.
        kit.Box("Plinth", tr, new Vector3(0f, t * 0.6f, 0f), new Vector3(w + 3f * t, t * 0.8f, w + 3f * t), Dark, default, colliders, 0.5f, 0.6f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                kit.Box("Foot", tr, new Vector3(sx * (half + t), t * 0.1f, sz * (half + t)), new Vector3(t * 1.2f, t * 0.4f, t * 1.2f), Metal, default, false, 0.8f, 0.6f);
        kit.Box("Body", tr, new Vector3(0f, h * 0.5f, 0f), new Vector3(w + 2f * t, h, w + 2f * t), BodyColor, default, colliders, 0.3f, 0.55f);
        // The lid with a square OPENING in the middle (four pieces round it), a dark throat inside and a glowing metal ring round it: husks dropped in
        // (or drawn in by its gentle suction) go into the box.
        float lw = w + 2.2f * t, hole = HoleHusks * u, side = (lw - hole) * 0.5f, ly = h + t * 0.15f;
        kit.Box("Lid Front", tr, new Vector3(0f, ly, -(hole + side) * 0.5f), new Vector3(lw, t * 0.3f, side), LidColor, default, colliders, 0.45f, 0.6f);
        kit.Box("Lid Back", tr, new Vector3(0f, ly, (hole + side) * 0.5f), new Vector3(lw, t * 0.3f, side), LidColor, default, colliders, 0.45f, 0.6f);
        kit.Box("Lid Left", tr, new Vector3(-(hole + side) * 0.5f, ly, 0f), new Vector3(side, t * 0.3f, hole), LidColor, default, colliders, 0.45f, 0.6f);
        kit.Box("Lid Right", tr, new Vector3((hole + side) * 0.5f, ly, 0f), new Vector3(side, t * 0.3f, hole), LidColor, default, colliders, 0.45f, 0.6f);
        kit.Box("Throat", tr, new Vector3(0f, h + t * 0.02f, 0f), new Vector3(hole, t * 0.04f, hole), Color.black, default, false, 0f, 0.1f);
        for (int k = 0; k < 4; k++)
        {
            bool alongX = k < 2;
            float sgn = k % 2 == 0 ? -1f : 1f;
            Vector3 at = alongX ? new Vector3(0f, h + t * 0.36f, sgn * (hole + t * 0.3f) * 0.5f) : new Vector3(sgn * (hole + t * 0.3f) * 0.5f, h + t * 0.36f, 0f);
            Vector3 size = alongX ? new Vector3(hole + t * 0.6f, t * 0.14f, t * 0.3f) : new Vector3(t * 0.3f, t * 0.14f, hole + t * 0.6f);
            kit.Box("Opening Ring", tr, at, size, glow, default, false, 0.3f, 0.8f, glow * 0.5f);
        }
        // Brushed-metal corner pillars and a rim round the lid.
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                kit.Box("Pillar", tr, new Vector3(sx * (half + t * 0.5f), h * 0.52f, sz * (half + t * 0.5f)), new Vector3(t * 1.5f, h * 1.04f, t * 1.5f), Metal, default, false, 0.85f, 0.7f);
        // A glowing band round the body (it flashes when a husk is routed).
        float bandY = h * 0.62f, bandT = t * 0.18f;
        kit.Box("Band Front", tr, new Vector3(0f, bandY, -(w + 2f * t) * 0.5f - bandT * 0.5f), new Vector3(w + 2f * t, t * 0.22f, bandT), glow, default, false, 0f, 0.8f, glow);
        kit.Box("Band Back", tr, new Vector3(0f, bandY, (w + 2f * t) * 0.5f + bandT * 0.5f), new Vector3(w + 2f * t, t * 0.22f, bandT), glow, default, false, 0f, 0.8f, glow);
        kit.Box("Band Left", tr, new Vector3(-(w + 2f * t) * 0.5f - bandT * 0.5f, bandY, 0f), new Vector3(bandT, t * 0.22f, w + 2f * t), glow, default, false, 0f, 0.8f, glow);
        kit.Box("Band Right", tr, new Vector3((w + 2f * t) * 0.5f + bandT * 0.5f, bandY, 0f), new Vector3(bandT, t * 0.22f, w + 2f * t), glow, default, false, 0f, 0.8f, glow);
        // A dark socket plate under each of the eight sockets (where a coupling bolts on).
        foreach (SocketSpec s in Specs(u))
        {
            Vector3 f = s.facing;
            Vector3 at = s.port - f * 0.3f * u + Vector3.up * 0.5f * u;
            Vector3 size = Mathf.Abs(f.x) > 0.5f ? new Vector3(t * 0.2f, u * 0.9f, u * 0.9f) : new Vector3(u * 0.9f, u * 0.9f, t * 0.2f);
            kit.Box("Socket Plate", tr, at, size, Dark, default, false, 0.3f, 0.4f);
        }
        // Carrying handles on the +/-x ends, the same as the Recycler's (shared PixelCarryHandles), above the sockets.
        PixelCarryHandles.BuildParts((n, p, size, c) => kit.Box(n, tr, p, size, c), half + t * 1.25f, w * 0.7f, h * 0.7f, 0.6f * u);
        return root;
    }

    private struct SocketSpec { public string id; public Vector3 port, facing; }

    /// <summary>The eight sockets: two on each side, at belt height just outside the body.</summary>
    private static IEnumerable<SocketSpec> Specs(float u)
    {
        float out_ = SideHusks * u * 0.5f + 0.25f * u + 0.35f * u, spread = SocketSpread * u;
        string[] sides = { "f", "b", "l", "r" };
        Vector3[] facing = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
        for (int s = 0; s < 4; s++)
            for (int k = 0; k < 2; k++)
            {
                float along = (k == 0 ? -1f : 1f) * spread;
                Vector3 f = facing[s];
                Vector3 side = new Vector3(f.z, 0f, -f.x);   // (along the side)
                yield return new SocketSpec { id = sides[s] + k, port = f * out_ + side * along, facing = f };
            }
    }

    private void Init(PixelClicker owner)
    {
        clicker = owner;
        float u = Unit(clicker);
        List<PixelSocket> list = new List<PixelSocket>();
        foreach (SocketSpec s in Specs(u)) list.Add(new PixelSocket(s.id, s.port, s.facing, PixelSocketFlow.Both) { autoDirection = true });
        sockets = list.ToArray();
        baseScale = transform.localScale;
        foreach (string band in new[] { "Band Front", "Band Back", "Band Left", "Band Right" })
        {
            Transform b = transform.Find(band);
            if (b != null && b.GetComponent<Renderer>() != null) bandRenderers.Add(b.GetComponent<Renderer>());
        }
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); PixelCouplings.Register(this); }
    private void OnDisable() { all.Remove(this); PixelCouplings.Unregister(this); }
    private void OnDestroy() { PixelCouplings.Unregister(this); foreach (GameObject m in filterModels.Values) if (m != null) Destroy(m); }

    // ---- Sockets (PixelSockets.cs) ----
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => BodyColor;

    /// <summary>How many more husks it takes (the buffer's room; a hose / the Sweeper only bring what fits).</summary>
    public int HuskRoom(PixelSocket socket) => Mathf.Max(0, bufferSize - buffer.Count);

    private bool Fresh => (transform.localScale - baseScale).sqrMagnitude <= 0.0001f;

    /// <summary>
    /// A husk arrives (kinematic, still on the husk list): it is TAKEN IN and deleted (it shrinks into the box) and a NEW husk of the same type and value
    /// comes out of an output (TakeData) - or waits in the buffer. Refused when neither has room.
    /// </summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk) => Swallow(husk, socket, transform.position + Vector3.up * HeightHusks * Unit(clicker) * transform.lossyScale.x * 0.5f);

    /// <summary>Takes a husk off the floor into the box (deleted: it shrinks to 'into') and routes its type + value. False = no room (it stays a husk).</summary>
    private bool Swallow(Rigidbody husk, PixelSocket from, Vector3 into)
    {
        if (husk == null || clicker == null) return false;
        OldPixelInfo info = PixelHuskQuery.Get(husk).info;
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || !HasRoomFor(info.tierIndex, from)) return false;
        int tier = info.tierIndex;
        double amount = info.amount;
        if (!clicker.ReleaseOldPixel(husk, false)) return false;
        StartCoroutine(PixelHuskHolder.FlyIn(husk.gameObject, null, into, false, 0.15f));
        TakeData(tier, amount, from);
        return true;
    }

    /// <summary>Can a husk of this type go in now: an output for it, or room in the buffer?</summary>
    private bool HasRoomFor(int tier, PixelSocket from) => buffer.Count < bufferSize || (Fresh && PickOutput(tier, from, false) != null);

    /// <summary>A husk's type + value inside the box: a NEW husk comes out of an output now (SpawnOut), else it waits in the buffer. False = no room at all.</summary>
    private bool TakeData(int tier, double amount, PixelSocket from)
    {
        PixelCoupling outlet = Fresh ? PickOutput(tier, from) : null;
        if (outlet != null && SpawnOut(outlet, tier, amount)) return true;
        if (buffer.Count >= bufferSize) return false;
        buffer.Add(new Held { tier = tier, amount = amount });
        PixelStats.Count("junction.buffered");
        return true;
    }

    /// <summary>Spits a new husk out of this Out coupling onto the belt starting there. False if it could not be made.</summary>
    private bool SpawnOut(PixelCoupling outlet, int tier, double amount)
    {
        int before = clicker.OldPixels.Count;
        if (!clicker.SpawnStoredPixel(tier, amount, outlet.PortWorld + Vector3.up * 0.6f * Unit(clicker), Vector3.zero) || clicker.OldPixels.Count <= before) return false;
        Rigidbody body = clicker.OldPixels[clicker.OldPixels.Count - 1];
        if (body == null) return false;
        OldPixelPopIn pop = body.GetComponent<OldPixelPopIn>();
        if (pop != null) pop.Finish();
        PixelCouplings.Emit(this, outlet.Socket.id, body);   // (onto the belt; if that fails it just drops by the coupling)
        Routed();
        return true;
    }

    // ---- Routing ----

    private int FilterOf(PixelSocket s) => s != null && filters.TryGetValue(s.id, out int f) ? f : -1;

    /// <summary>Does an Out coupling filter for this type (then the husk only ever goes there)?</summary>
    private bool FilteredOutputFor(int tier)
    {
        foreach (PixelSocket s in sockets)
        {
            PixelCoupling c = PixelCoupling.On(this, s);
            if (c != null && c.Direction == PixelCoupling.AutoState.Out && FilterOf(s) == tier) return true;
        }
        return false;
    }

    /// <summary>A belt output that can take this husk now: its filter output (only it), else the next unfiltered one in turn. Null = none (buffer it).</summary>
    private PixelCoupling PickOutput(int tier, PixelSocket from, bool advance = true)
    {
        if (FilteredOutputFor(tier))
        {
            foreach (PixelSocket s in sockets)
            {
                if (s == from || FilterOf(s) != tier) continue;
                PixelCoupling c = BeltOutput(s);
                if (c != null) return c;
            }
            return null;   // (its own output is full or is a hose: it waits in the buffer)
        }
        for (int k = 1; k <= sockets.Length; k++)
        {
            int i = (roundRobin + k) % sockets.Length;
            PixelSocket s = sockets[i];
            if (s == from || FilterOf(s) >= 0) continue;
            PixelCoupling c = BeltOutput(s);
            if (c == null) continue;
            if (advance) roundRobin = i;
            return c;
        }
        return null;
    }

    /// <summary>The Out coupling on this socket when a belt starts at it with room; else null.</summary>
    private PixelCoupling BeltOutput(PixelSocket s)
    {
        PixelCoupling c = PixelCoupling.On(this, s);
        if (c == null || c.Direction != PixelCoupling.AutoState.Out || PixelCouplings.BeltStartingAt(c.PortWorld) == null) return null;
        return PixelCouplings.OutputHasRoom(this, s.id) ? c : null;
    }

    private void Routed()
    {
        sessionRouted++;
        bandFlash = 1f;
        PixelStats.Count("junction.routed");
    }

    /// <summary>A hose pulls from an Out coupling: the oldest buffered husk of its filter type (unfiltered: any type no filtered output wants).</summary>
    public bool TakeHuskOut(PixelSocket socket, out int tier, out double amount)
    {
        tier = -1; amount = 0d;
        int f = FilterOf(socket);
        for (int i = 0; i < buffer.Count; i++)
        {
            Held h = buffer[i];
            if (f >= 0 ? h.tier != f : FilteredOutputFor(h.tier)) continue;
            buffer.RemoveAt(i);
            tier = h.tier; amount = h.amount;
            Routed();
            return true;
        }
        return false;
    }

    // ---- The top opening ----

    private static readonly List<PixelHuskQuery.Husk> nearScratch = new List<PixelHuskQuery.Husk>();

    /// <summary>
    /// Husks just above the box are drawn gently towards the opening in the lid; one that reaches it (over the hole, at lid height) is swallowed:
    /// deleted, and a new one comes out of an output (or waits in the buffer). Only husks nobody else owns (PixelHuskHolder.Takeable).
    /// </summary>
    private void FixedUpdate()
    {
        if (!topOpening || clicker == null || !Fresh || PixelCalm.InMenu || (carry != null && carry.Dragging) || (device != null && device.IsEnding)) return;
        float u = Unit(clicker), s = transform.lossyScale.x, h = HeightHusks * u, holeHalf = HoleHusks * u * 0.5f, reach = Mathf.Max(holeHalf, suckRadiusHusks * u);
        Vector3 hole = transform.TransformPoint(new Vector3(0f, h, 0f));
        PixelHuskQuery.Near(clicker, hole, (reach + suckHeightHusks * u) * s, nearScratch);
        float dt = Time.fixedDeltaTime;
        foreach (PixelHuskQuery.Husk q in nearScratch)
        {
            Rigidbody b = q.body;
            if (b == null || !PixelHuskHolder.Takeable(clicker, b)) continue;
            Vector3 local = transform.InverseTransformPoint(b.position);
            if (local.y < h - 0.3f * u || local.y > h + suckHeightHusks * u) continue;
            float d = new Vector2(local.x, local.z).magnitude;
            float lidHalf = SideHusks * u * 0.5f + 0.3f * u;   // (anything resting ON the lid is taken too: nothing piles up on top)
            if ((Mathf.Abs(local.x) <= holeHalf && Mathf.Abs(local.z) <= holeHalf && local.y <= h + 0.7f * u) || (Mathf.Abs(local.x) <= lidHalf && Mathf.Abs(local.z) <= lidHalf && local.y <= h + 0.9f * u))
            {
                Swallow(b, null, transform.TransformPoint(new Vector3(0f, h * 0.5f, 0f)));   // (in through the opening)
                continue;
            }
            if (d > reach || d < 1e-4f || suckStrength <= 0f) continue;
            float k = 1f - d / reach;
            Vector3 toHole = transform.TransformDirection(new Vector3(-local.x, 0f, -local.z) / d);
            Vector3 dv = (toHole * (0.4f + 0.6f * k) + Vector3.down * 0.3f * k) * suckStrength * u * s * dt;
#if UNITY_6000_0_OR_NEWER
            b.linearVelocity += dv;
#else
            b.velocity += dv;
#endif
        }
    }

    // ---- Every frame ----

    private void Update()
    {
        if (clicker == null) return;
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        if (device != null && device.IsEnding) return;
        SpinFilterModels();
        UpdateBand();
        if (!Fresh || PixelCalm.InMenu || Time.timeScale <= 0f) return;
        HandleFilterClicks(clicker.ViewCamera);
        if (Time.time < nextRoute) return;
        nextRoute = Time.time + routeSeconds;
        UpdateDirections();
        if (carry == null || !carry.Dragging) RetryBuffer();
        CatchFilterDrops();
    }

    /// <summary>Each coupling's way from what is attached: a belt ending at it / a hose pushing in = In, a belt starting at it / a hose pulling out = Out, nothing = idle.</summary>
    private void UpdateDirections()
    {
        foreach (PixelSocket s in sockets)
        {
            PixelCoupling c = PixelCoupling.On(this, s);
            if (c == null) continue;
            Vector3 port = c.PortWorld;
            bool into = PixelCouplings.BeltEndingAt(port) != null || PixelContainerHose.Pushes(c);
            bool outOf = PixelCouplings.BeltStartingAt(port) != null || PixelContainerHose.Pulls(c);
            c.SetAuto(into ? PixelCoupling.AutoState.In : outOf ? PixelCoupling.AutoState.Out : PixelCoupling.AutoState.Idle);
        }
    }

    /// <summary>Buffered husks go out as soon as an output can take them (at most one per output each check).</summary>
    private void RetryBuffer()
    {
        if (buffer.Count == 0) return;
        HashSet<PixelCoupling> used = null;
        for (int i = 0; i < buffer.Count; i++)
        {
            PixelCoupling c = PickOutput(buffer[i].tier, null);
            if (c == null || (used != null && used.Contains(c))) continue;
            if (!SpawnOut(c, buffer[i].tier, buffer[i].amount)) continue;
            buffer.RemoveAt(i--);
            if (used == null) used = new HashSet<PixelCoupling>();
            used.Add(c);
        }
    }

    // ---- Filters ----

    private void SetFilter(PixelSocket s, int tier)
    {
        if (s == null) return;
        if (clicker == null || !clicker.IsValidTierIndex(tier)) tier = -1;
        if (tier < 0) filters.Remove(s.id); else filters[s.id] = tier;
        if (filterModels.TryGetValue(s.id, out GameObject old) && old != null) Destroy(old);
        filterModels.Remove(s.id);
        if (tier < 0) return;
        float u = Unit(clicker);
        GameObject holder = new GameObject("Filter " + s.id);
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = s.localPosition + Vector3.up * 1.75f * u;
        GameObject model = clicker.CreateDisplayPixel(tier, holder.transform, u * 0.45f);
        if (model != null) model.transform.localPosition = Vector3.zero;
        filterModels[s.id] = holder;
    }

    private void SpinFilterModels()
    {
        foreach (GameObject m in filterModels.Values) if (m != null) m.transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
    }

    /// <summary>The pointer on a coupling: claims the click, shows its filter's card; left-click / scroll steps the filter through the unlocked types (and none).</summary>
    private void HandleFilterClicks(Camera cam)
    {
        if (cam == null || PixelInput.PointerOverUI() || PixelCarryHandles.Hot || PixelFeatureTester.Placing) return;
        foreach (PixelSocket s in sockets)
        {
            PixelCoupling c = PixelCoupling.On(this, s);
            if (c == null || !c.PointerOver(cam)) continue;
            PixelPointerClaim.ClaimButton();
            int f = FilterOf(s);
            if (f >= 0) PixelBlockTooltip.ShowFor(f, "<b>Filter</b> on this coupling: while it is an OUT coupling, every " + Name(f) + " husk goes here. Click or scroll to change it; drop a husk on it to set its type.");
            float scroll = PixelInput.ScrollY();
            if (Mathf.Abs(scroll) > 0.01f) StepFilter(s, scroll > 0f ? 1 : -1);
            else if (PixelInput.LeftPressed()) StepFilter(s, 1);
            return;
        }
    }

    private void StepFilter(PixelSocket s, int step)
    {
        List<int> options = new List<int> { -1 };
        for (int t = 0; t < clicker.Tiers.Length; t++)
        {
            PixelClicker.PixelTier tier = clicker.Tiers[t];
            if (tier != null && tier.unlocked && !tier.flyAway) options.Add(t);
        }
        int at = options.IndexOf(FilterOf(s));
        at = ((at < 0 ? 0 : at) + step + options.Count) % options.Count;
        SetFilter(s, options[at]);
        PixelAudio.Play("ui_click");
        PixelHints.Announce(options[at] < 0 ? "Coupling filter cleared" : "Coupling filter: " + Name(options[at]));
    }

    /// <summary>A husk the player was holding that lands right by a coupling sets that coupling's filter to its type.</summary>
    private void CatchFilterDrops()
    {
        if (dropsSeen.Count > 64) dropsSeen.Clear();
        float u = Unit(clicker), reach = filterDropReach * u, held = PixelGrab.RecentlyHeldSeconds;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody b = pixels[i];
            if (b == null || dropsSeen.Contains(b)) continue;
            PixelHuskQuery.Husk h = PixelHuskQuery.Get(b);
            if (h.info == null || h.info.heldByPlayer || !h.info.RecentlyHeld(held) || !clicker.IsValidTierIndex(h.Tier)) continue;
            foreach (PixelSocket s in sockets)
            {
                PixelCoupling c = PixelCoupling.On(this, s);
                if (c == null) continue;
                Vector3 d = b.position - c.PortWorld;
                if (Mathf.Abs(d.y) > 1.6f * u) continue;
                d.y = 0f;
                if (d.sqrMagnitude > reach * reach) continue;
                dropsSeen.Add(b);
                if (FilterOf(s) == h.Tier) break;
                SetFilter(s, h.Tier);
                PixelAudio.Play("ui_click");
                PixelHints.Announce("Coupling filter: " + Name(h.Tier));
                break;
            }
        }
    }

    private string Name(int tier) => clicker.Tiers[tier].displayName.Replace(" Pixels", "");

    private void UpdateBand()
    {
        if (bandRenderers.Count == 0) return;
        if (bandFlash <= 0f && bandShown <= 0f) return;
        bandFlash = Mathf.MoveTowards(bandFlash, 0f, Time.deltaTime * 3f);
        bandShown = bandFlash;
        Color e = bandColor * (1f + 1.5f * bandFlash);
        foreach (Renderer r in bandRenderers)
        {
            if (r == null) continue;
            Material m = r.material;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", e); }
        }
    }

    private float bandShown;

    // ---- IPixelHopperHost (a Sorting Hopper on the back: b0 / b1) ----
    public string HostName => "Junction Box";
    public IEnumerable<string> HopperSockets { get { yield return "b0"; yield return "b1"; } }
    public float BackEdgeLocalZ => Unit(clicker) * (SideHusks * 0.5f + 0.45f);
    public Vector3 FeedDropPoint => transform.TransformPoint(new Vector3(0f, HeightHusks * Unit(clicker) + 0.35f * Unit(clicker), 0f));   // (over the top opening)
    public bool Fading => !Fresh;
    public bool CanTakeFeed => Fresh && (carry == null || !carry.Dragging) && buffer.Count < bufferSize;

    /// <summary>A hopper feeds a husk into the top hatch: routed like any husk coming in (to an output now, else the buffer). False = full.</summary>
    public bool TakeFeed(int tier, double amount, Vector3 at, Vector3 velocity)
    {
        return clicker != null && clicker.IsValidTierIndex(tier) && TakeData(tier, amount, null);
    }

    // ---- The device card / panel ----

    public void FillDeviceInfo(PixelDeviceInfo info)
    {
        int ins = 0, outs = 0;
        foreach (PixelSocket s in sockets)
        {
            PixelCoupling c = PixelCoupling.On(this, s);
            if (c == null) continue;
            if (c.Direction == PixelCoupling.AutoState.In) ins++;
            else if (c.Direction == PixelCoupling.AutoState.Out) outs++;
        }
        info.Add("Couplings", ins + " in, " + outs + " out");
        info.Add("Buffer", buffer.Count + " / " + bufferSize);
        info.Progress = bufferSize > 0 ? buffer.Count / (float)bufferSize : -1f;
        info.ProgressLabel = buffer.Count >= bufferSize ? "full - it refuses husks" : "";
        if (!info.Full) return;
        foreach (PixelSocket s in sockets)
        {
            PixelCoupling c = PixelCoupling.On(this, s);
            int f = FilterOf(s);
            if (c == null && f < 0) continue;
            string way = c == null ? "no coupling" : c.Direction == PixelCoupling.AutoState.In ? "in" : c.Direction == PixelCoupling.AutoState.Out ? "out" : "idle";
            info.Add("  " + s.id.ToUpperInvariant(), way + (f >= 0 ? ", filter " + Name(f) : ""));
        }
        info.Heading("This session");
        info.Add("Husks routed", sessionRouted.ToString());
        info.AddAction("Clear filters", () => { foreach (PixelSocket s in sockets) SetFilter(s, -1); });
        info.AddAction("Empty buffer", EmptyBuffer, ButtonRole.Danger);
    }

    /// <summary>The buffered husks pop out on top of the box.</summary>
    public void EmptyBuffer()
    {
        if (clicker == null || buffer.Count == 0) return;
        float u = Unit(clicker), s = transform.lossyScale.x;
        Vector3 top = transform.position + Vector3.up * (HeightHusks + 0.8f) * u * s;
        foreach (Held h in buffer)
            clicker.SpawnStoredPixel(h.tier, h.amount, top, new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2f, 3f), Random.Range(-1.5f, 1.5f)));
        buffer.Clear();
        PixelAudio.Play("device_place");
    }

    // ---- Saving (PixelConsumables puts this in PlacedState.extra) ----

    /// <summary>'f,socketId,Type' per filter and 'b,Type,amount' per buffered husk, ';'-separated (by pixel type name).</summary>
    public string ExportState()
    {
        if (clicker == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (KeyValuePair<string, int> kv in filters)
            if (clicker.IsValidTierIndex(kv.Value)) sb.Append("f,").Append(kv.Key).Append(',').Append(clicker.Tiers[kv.Value].type).Append(';');
        foreach (Held h in buffer)
            if (clicker.IsValidTierIndex(h.tier)) sb.Append("b,").Append(clicker.Tiers[h.tier].type).Append(',').Append(h.amount.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        return sb.ToString();
    }

    public void ImportState(string data)
    {
        if (string.IsNullOrEmpty(data) || clicker == null) return;
        foreach (string entry in data.Split(';'))
        {
            string[] f = entry.Split(',');
            if (f.Length < 3) continue;
            if (f[0] == "f")
            {
                PixelSocket s = PixelCouplings.Find(this, f[1]);
                if (s != null && System.Enum.TryParse(f[2], out PixelClicker.PixelType ft)) SetFilter(s, clicker.IndexOf(ft));
            }
            else if (f[0] == "b" && System.Enum.TryParse(f[1], out PixelClicker.PixelType bt))
            {
                int tier = clicker.IndexOf(bt);
                double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double amount);
                if (tier >= 0 && buffer.Count < bufferSize) buffer.Add(new Held { tier = tier, amount = amount });
            }
        }
    }
}
