// ---- System notes ----
// PixelSockets.cs
//   SOCKETS: the ONE way husks get into and out of devices (design: docs/systems/PixelCouplings.md). Sockets carry HUSKS only - power is a separate
//   mechanic and will not use these ports. A device declares its sockets as data (PixelSocket: id, port = local position where a belt end / hose end
//   meets it, outward facing, flow Intake / Output / Both, fitted) and implements IPixelSocketDevice: its sockets, a tint for its couplings, and ONE hook
//   for husks coming in (AcceptHusk: take it or refuse when full). Devices register in OnEnable / unregister in OnDisable (PixelCouplings.Register).
//   A PixelCoupling (PixelCoupling.cs) sits on a socket; belts and hoses only ever talk to couplings:
//   - AUTO-FIT: couplings are not bought. A belt END at a free socket's port gets an In coupling, a belt START there an Out one, and a hose end gets one
//     the way the hose runs (AutoFitAt / AutoFitAll - belts placed, devices carried next to belts, every PixelSocketPlates refresh). An Intake socket only
//     takes In, an Output socket only Out, a Both socket follows what attached. Placing snaps to free socket ports too (Ports).
//   - a belt END / hose end at an In coupling hands its husk in: PixelCouplings.HandIn(at, husk) -> the device's AcceptHusk;
//   - a device putting a husk out calls PixelCouplings.Emit(device, socketId, husk): an Out coupling on that socket puts it on the belt that starts at
//     its port (PixelConveyorSystem.StartHasRoom), or hands it to an In coupling there (device to device); false = nothing attached or the belt there
//     is full - the device does its own thing. OutputHasRoom = may the device let one out now. EmitStored = spawn a STORED husk and Emit it (crate,
//     organizer feeding a belt).
//   - TYPE-AWARE INTAKE: a device that takes only some husks implements IPixelHuskFilter (Takes(socket, tier)); PixelCouplings.Takes asks it (true when
//     it doesn't). A source that can pick WHICH husk to hand out implements IPixelHuskPicker, so a hose only takes out husks its target takes.
//   - CONNECTION RESOLVER: Attached(coupling) says what a coupling is joined to (a belt into / out of it, a hose pushing / pulling, another device's
//     coupling, nothing) - the device card's socket rows, the Junction Box's auto direction and the hose's direction re-check all use it.
//   - ONE reach for every link check (LinkReach, in husk widths); FreeSockets / HuskWidth are the shared helpers.
//   Optional extras: IPixelHuskSource (a hose may pull stored husks out of an Out coupling), IPixelHuskRoom (how many more husks an In coupling's
//   device takes now). Couplings are saved with their device: Export / Import (socket ids + direction) into PlacedState.couplings / TesterSave.couplings.
//   An AUTO-DIRECTION socket (autoDirection, the Junction Box) has no fixed way: its device sets its coupling In / Out / idle from Attached.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>What flows through a socket. Sockets carry husks only (power is a separate mechanic, not ports); kept as a field so saved data stays readable.</summary>
public enum PixelSocketKind
{
    /// <summary>Husks (belts, hoses).</summary>
    Husk = 0,
}

/// <summary>Which way things may go through a socket. A coupling on a Both socket is set to In or Out (click it to switch).</summary>
public enum PixelSocketFlow { Intake = 0, Output = 1, Both = 2 }

/// <summary>One opening on a device that a coupling can be fitted to (data only; positions are in the device's own space).</summary>
[System.Serializable]
public class PixelSocket
{
    [Tooltip("Name of the socket on its device (saved with the couplings, so keep it the same).")]
    public string id;
    [Tooltip("The PORT, in the device's own space: where a belt end / hose end meets the socket (at belt-underside height).")]
    public Vector3 localPosition;
    [Tooltip("The way the opening faces, out of the device (in the device's own space, flat).")]
    public Vector3 localFacing = Vector3.forward;
    [Tooltip("Intake = husks go in, Output = husks come out, Both = the coupling chooses (click it).")]
    public PixelSocketFlow flow;
    [Tooltip("What flows through it: husks (the only kind).")]
    public PixelSocketKind kind;
    [Tooltip("A coupling comes fitted with the device (the belt-fitting pieces: Elevator, Belt Sorter); not saved, not removable on its own.")]
    public bool fitted;
    [Tooltip("A fitted coupling with no model of its own (the piece's own shape is the fitting: the Belt Sorter's chute).")]
    public bool bare;
    [Tooltip("(Both sockets) The coupling's direction comes from what is attached to it - a belt ending at it / a hose pushing in = In, a belt starting at it / a hose pulling out = Out, nothing = idle (the device sets it: PixelCoupling.SetAuto). No teal switch dot. The Junction Box.")]
    public bool autoDirection;

    public PixelSocket(string id, Vector3 localPosition, Vector3 localFacing, PixelSocketFlow flow, bool fitted = false, PixelSocketKind kind = PixelSocketKind.Husk)
    {
        this.id = id; this.localPosition = localPosition; this.localFacing = localFacing; this.flow = flow; this.fitted = fitted; this.kind = kind;
    }

    public bool CanTakeIn => flow != PixelSocketFlow.Output;
    public bool CanGiveOut => flow != PixelSocketFlow.Intake;
}

/// <summary>A device with sockets. ONE hook for husks coming in through an In coupling.</summary>
public interface IPixelSocketDevice
{
    /// <summary>The transform the sockets are placed in (the device's root; couplings are parented to it, so they move with it).</summary>
    Transform SocketRoot { get; }
    /// <summary>The device's sockets (the same list every call; ports may move if the device is rebuilt).</summary>
    IReadOnlyList<PixelSocket> Sockets { get; }
    /// <summary>The device's main colour: its couplings are tinted a little with it.</summary>
    Color SocketTint { get; }
    /// <summary>A husk arrives through an In coupling on 'socket' (kinematic, still on the husk list). Take it (true) or refuse (false: full) - a refused husk is thrown on by whatever brought it.</summary>
    bool AcceptHusk(PixelSocket socket, Rigidbody husk);
}

/// <summary>A device a hose can pull stored husks out of, through an Out coupling (Storage Crate, Quantum Organizer).</summary>
public interface IPixelHuskSource
{
    bool TakeHuskOut(PixelSocket socket, out int tier, out double amount);
}

/// <summary>A device that says how many more husks it takes through an In coupling right now (a hose only pumps what fits).</summary>
public interface IPixelHuskRoom
{
    int HuskRoom(PixelSocket socket);
}

/// <summary>A device that takes only SOME husks through a socket (the Biomass Generator: wood). Asked before a husk is moved to it (PixelCouplings.Takes).</summary>
public interface IPixelHuskFilter
{
    bool Takes(PixelSocket socket, int tier);
}

/// <summary>A source that can PICK which stored husk it hands out (the first one 'want' accepts), so a hose never pulls a husk its target refuses.</summary>
public interface IPixelHuskPicker
{
    bool TakeHuskOut(PixelSocket socket, System.Predicate<int> want, out int tier, out double amount);
}

/// <summary>What a coupling is joined to (PixelCouplings.Attached).</summary>
public enum PixelLink { Nothing, BeltIn, BeltOut, HoseIn, HoseOut, DeviceOut }

/// <summary>The socket registry and the shared connect steps (hand a husk in, put one out, snap points, saving).</summary>
public static class PixelCouplings
{
    private static readonly List<IPixelSocketDevice> devices = new List<IPixelSocketDevice>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { devices.Clear(); PixelStatics.ResetOnRestart(() => devices.Clear()); }

    public static IReadOnlyList<IPixelSocketDevice> Devices => devices;

    /// <summary>A device with sockets came into the world (call in OnEnable; ghosts / pictures do not register).</summary>
    public static void Register(IPixelSocketDevice d) { if (d != null && !devices.Contains(d)) devices.Add(d); }
    public static void Unregister(IPixelSocketDevice d) { devices.Remove(d); }

    /// <summary>Is the device still in the world (registered devices can be destroyed without unregistering first)?</summary>
    public static bool Alive(IPixelSocketDevice d) => d != null && !(d is Object o && o == null) && d.SocketRoot != null;

    /// <summary>The socket's port in the world.</summary>
    public static Vector3 Port(IPixelSocketDevice d, PixelSocket s) => d.SocketRoot.TransformPoint(s.localPosition);

    /// <summary>The way the socket faces, in the world (flat).</summary>
    public static Vector3 Facing(IPixelSocketDevice d, PixelSocket s)
    {
        Vector3 f = d.SocketRoot.TransformDirection(s.localFacing); f.y = 0f;
        return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
    }

    public static PixelSocket Find(IPixelSocketDevice d, string id)
    {
        if (!Alive(d)) return null;
        foreach (PixelSocket s in d.Sockets) if (s != null && s.id == id) return s;
        return null;
    }

    /// <summary>Is the socket free for a coupling (none on it)?</summary>
    public static bool IsFree(IPixelSocketDevice d, PixelSocket s) => Alive(d) && s != null && PixelCoupling.On(d, s) == null;

    private static readonly List<PixelSocket> freeScratch = new List<PixelSocket>();

    /// <summary>
    /// The device's FREE sockets a coupling can go on (not built-in, nothing fitted). The returned list is shared and reused: read it right away.
    /// (The one 'free socket' loop: placing couplings, the socket plates, auto-fit.)
    /// </summary>
    public static List<PixelSocket> FreeSockets(IPixelSocketDevice d)
    {
        freeScratch.Clear();
        if (!Alive(d)) return freeScratch;
        foreach (PixelSocket s in d.Sockets) if (s != null && !s.fitted && IsFree(d, s)) freeScratch.Add(s);
        return freeScratch;
    }

    /// <summary>
    /// Keeps a socket-hung piece (coupling, port plate) at the same world size whatever its device's scale (the Recycler grows with Bin Size):
    /// rescales it when its parent's scale changed. Cheap enough to call every frame.
    /// </summary>
    public static void KeepSize(Transform t)
    {
        if (t == null || t.parent == null) return;
        float parentScale = t.parent.lossyScale.x;
        if (parentScale <= 0.2f || Mathf.Abs(t.localScale.x * parentScale - 1f) < 0.001f) return;
        t.localScale = Vector3.one / parentScale;
    }

    /// <summary>The edge length of a husk in the world (the unit every socket distance is measured in).</summary>
    public static float HuskWidth { get { PixelClicker c = PixelFind.Cached<PixelClicker>(); return c != null && c.OldPixelWorldSize > 0f ? c.OldPixelWorldSize : 0.5f; } }

    /// <summary>Does the device take a husk of this tier through this socket (IPixelHuskFilter)? True when it doesn't filter.</summary>
    public static bool Takes(IPixelSocketDevice d, PixelSocket s, int tier) => !(d is IPixelHuskFilter f) || f.Takes(s, tier);

    // ---- auto-fit: couplings appear where a belt / hose meets a free socket ----

    /// <summary>
    /// The free socket whose port is nearest 'at' (within LinkReach widths sideways, 1.2 up / down) and that can work the wanted way
    /// (intake = husks go into the device there). False when none.
    /// </summary>
    public static bool FreeSocketAt(Vector3 at, bool intake, out IPixelSocketDevice device, out PixelSocket socket, float reachHusks = LinkReach)
    {
        device = null; socket = null;
        float w = HuskWidth, best = (reachHusks * w) * (reachHusks * w), up = 1.2f * w;
        for (int i = devices.Count - 1; i >= 0; i--)
        {
            IPixelSocketDevice d = devices[i];
            if (!Alive(d)) continue;
            foreach (PixelSocket s in d.Sockets)
            {
                if (s == null || s.fitted || (intake ? !s.CanTakeIn : !s.CanGiveOut) || !IsFree(d, s)) continue;
                Vector3 dp = Port(d, s) - at;
                if (Mathf.Abs(dp.y) > up) continue;
                dp.y = 0f;
                if (dp.sqrMagnitude <= best) { best = dp.sqrMagnitude; device = d; socket = s; }
            }
        }
        return device != null;
    }

    /// <summary>
    /// Fits a coupling on the free socket at 'at' that can work the wanted way, set that way (a Both socket follows it; an auto socket works it out).
    /// Returns the coupling, or null when there is no free socket there. Nothing happens when a coupling already sits there.
    /// </summary>
    public static PixelCoupling AutoFitAt(Vector3 at, bool intake, float reachHusks = LinkReach)
    {
        if (!FreeSocketAt(at, intake, out IPixelSocketDevice d, out PixelSocket s, reachHusks)) return null;
        PixelCoupling c = PixelCoupling.Fit(d, s);
        if (c == null) return null;
        if (s.flow == PixelSocketFlow.Both && !s.autoDirection) c.SetDirection(intake);
        return c;
    }

    /// <summary>
    /// Fits couplings on every free socket a belt end / start meets (belt END = In, belt START = Out): belts placed before a device, a device carried next
    /// to a belt, loading. Called every few tenths of a second (PixelSocketPlates) - cheap: free sockets x belt ends.
    /// </summary>
    private static readonly List<PixelSocket> fitScratch = new List<PixelSocket>();

    public static void AutoFitAll()
    {
        for (int i = devices.Count - 1; i >= 0; i--)
        {
            IPixelSocketDevice d = devices[i];
            if (!Alive(d) || (d is PixelPlacedDevice pd && pd.IsEnding)) continue;
            fitScratch.Clear();
            fitScratch.AddRange(FreeSockets(d));   // (a copy: fitting changes which sockets are free)
            foreach (PixelSocket s in fitScratch)
            {
                Vector3 port = Port(d, s);
                bool into = s.CanTakeIn && BeltEndingAt(port) != null;
                bool outOf = !into && s.CanGiveOut && BeltStartingAt(port) != null;
                if (!into && !outOf) continue;
                PixelCoupling c = PixelCoupling.Fit(d, s);
                if (c != null && s.flow == PixelSocketFlow.Both && !s.autoDirection) c.SetDirection(into);
            }
        }
    }

    // ---- what a coupling is joined to ----

    /// <summary>
    /// What this coupling is joined to: a belt ending at it (BeltIn) / starting at it (BeltOut), a hose pushing in (HoseIn) / pulling out (HoseOut),
    /// another device's In coupling right in front of it (DeviceOut), or nothing. Worked out on demand (card rows, Junction Box, hose checks).
    /// </summary>
    public static PixelLink Attached(PixelCoupling c)
    {
        if (c == null || !Alive(c.Device)) return PixelLink.Nothing;
        Vector3 port = c.PortWorld;
        if (BeltEndingAt(port) != null) return PixelLink.BeltIn;
        if (BeltStartingAt(port) != null) return PixelLink.BeltOut;
        if (PixelContainerHose.Pushes(c)) return PixelLink.HoseIn;
        if (PixelContainerHose.Pulls(c)) return PixelLink.HoseOut;
        if (c.IsOut && IntakeAt(port + c.FacingWorld * 0.5f * HuskWidth, HuskWidth, c.Device) != null) return PixelLink.DeviceOut;
        return PixelLink.Nothing;
    }

    /// <summary>The device panel's port rows: one per socket a coupling can go on ("Port side: Out: feeds a belt", "Port in: free").</summary>
    public static void AddPortRows(PixelDeviceInfo info, IPixelSocketDevice d)
    {
        if (info == null || !Alive(d)) return;
        foreach (PixelSocket s in d.Sockets)
        {
            if (s == null || s.fitted) continue;
            PixelCoupling c = PixelCoupling.On(d, s);
            info.Add("Port " + s.id, c != null ? Describe(c) : "free - a belt or hose attaches here");
        }
    }

    /// <summary>One line for a coupling on a device card: "In: a belt", "Out: idle - nothing attached"...</summary>
    public static string Describe(PixelCoupling c)
    {
        if (c == null) return "";
        string way = c.Direction == PixelCoupling.AutoState.Idle ? "Idle" : c.Direction == PixelCoupling.AutoState.Out ? "Out" : "In";
        switch (Attached(c))
        {
            case PixelLink.BeltIn: return way + ": a belt brings husks in";
            case PixelLink.BeltOut: return way + ": feeds a belt";
            case PixelLink.HoseIn: return way + ": a hose pumps husks in";
            case PixelLink.HoseOut: return way + ": a hose pulls husks out";
            case PixelLink.DeviceOut: return way + ": hands husks to the device in front";
            default: return way + ": idle - nothing attached";
        }
    }

    /// <summary>How far (in husk widths, flat) a belt end may be from a coupling's port and still count as joined to it. One value for every link check.</summary>
    public const float LinkReach = 1.3f;

    // ---- husks in ----

    /// <summary>The In coupling whose port is at this point (within 'reach' sideways, 1.2 husk widths up / down); null if none. 'exclude' = the asking device.</summary>
    public static PixelCoupling IntakeAt(Vector3 at, float reach, IPixelSocketDevice exclude = null)
    {
        PixelCoupling best = null;
        float bestD = reach * reach, up = 1.2f * HuskWidth;
        foreach (PixelCoupling c in PixelCoupling.All)
        {
            if (c == null || !c.IsIn || c.Device == exclude || !Alive(c.Device)) continue;
            Vector3 d = c.PortWorld - at;
            if (Mathf.Abs(d.y) > up) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= bestD) { bestD = d.sqrMagnitude; best = c; }
        }
        return best;
    }

    /// <summary>Hands a husk (kinematic, still on the husk list) to the In coupling at this point. False = none there, or the device refused it.</summary>
    public static bool HandIn(Vector3 at, Rigidbody husk, IPixelSocketDevice exclude = null, float reachHusks = LinkReach)
    {
        if (husk == null) return false;
        PixelCoupling c = IntakeAt(at, reachHusks * HuskWidth, exclude);
        return c != null && c.TakeIn(husk);   // (sockets are the ONLY way in: the old 'catch husks near it' path is gone since 2026-10-20)
    }

    // ---- husks out ----

    /// <summary>The belt that ENDS nearest this point (its exit within LinkReach widths sideways and 1.2 up / down), or null.</summary>
    public static PixelConveyor BeltEndingAt(Vector3 port) => NearestBeltEnd(port, true);

    /// <summary>The belt that STARTS nearest this point (within LinkReach widths sideways and 1.2 up / down), or null.</summary>
    public static PixelConveyor BeltStartingAt(Vector3 port) => NearestBeltEnd(port, false);

    private static PixelConveyor NearestBeltEnd(Vector3 port, bool exit)
    {
        PixelConveyor best = null;
        float bestD = float.MaxValue;
        foreach (PixelConveyor belt in PixelConveyor.All)
        {
            if (belt == null || belt.IsGhost) continue;
            Vector3 d = (exit ? belt.ExitPoint : belt.StartPoint) - port;
            if (Mathf.Abs(d.y) > 1.2f * belt.Unit) continue;
            float flat = new Vector3(d.x, 0f, d.z).magnitude;
            if (flat <= LinkReach * belt.Unit && flat < bestD) { bestD = flat; best = belt; }
        }
        return best;
    }

    /// <summary>Puts a husk on the start of a belt (just above its first stretch, moving its way); the belt system locks it on. Low level: it does not check
    /// for room - callers go through Emit, or check PixelConveyorSystem.StartHasRoom first.</summary>
    public static void PutOnBelt(PixelConveyor belt, Rigidbody husk)
    {
        float size = HuskWidth;
        Vector3 over = belt.PointAt(0.6f * size);
        husk.position = new Vector3(over.x, over.y + belt.SurfaceHeight + size * 0.7f, over.z);
        husk.isKinematic = false;
        PixelHuskHolder.SetVelocity(husk, belt.Direction * belt.Speed * 0.5f);
    }

    /// <summary>
    /// A device puts a husk out of a socket: with an Out coupling there, onto the belt that starts at its port, or into an In coupling right there (device to
    /// device). False = nothing attached (no coupling, no belt) OR the belt there is full: the device does its own thing with it. Never puts a husk on
    /// a full belt - a device that must not drop it checks OutputHasRoom first and holds it until there is room.
    /// </summary>
    public static bool Emit(IPixelSocketDevice d, string socketId, Rigidbody husk)
    {
        PixelCoupling c = PixelCoupling.On(d, socketId);
        if (c == null || !c.IsOut || husk == null) return false;
        PixelConveyor belt = BeltStartingAt(c.PortWorld);
        if (belt != null)
        {
            if (!PixelConveyorSystem.StartHasRoom(belt)) return false;   // (full: the one room check for every device putting husks out)
            PutOnBelt(belt, husk);
            return true;
        }
        husk.isKinematic = true;
        if (HandIn(c.PortWorld + c.FacingWorld * 0.5f * HuskWidth, husk, d)) return true;
        husk.isKinematic = false;
        return false;
    }

    /// <summary>
    /// A storing device (crate, organizer) lets one STORED husk out of an Out socket onto what is attached there: 'take' hands out the stored husk
    /// (its tier and value, or false when empty), it is made at the port and Emitted. Only when a belt / device there has room. True = one went out.
    /// </summary>
    public static bool EmitStored(IPixelSocketDevice d, string socketId, PixelClicker clicker, TakeStored take)
    {
        if (clicker == null || take == null || !OutputConnected(d, socketId) || !OutputHasRoom(d, socketId)) return false;
        PixelCoupling c = PixelCoupling.On(d, socketId);
        if (c == null || !take(out int tier, out double amount)) return false;
        int before = clicker.OldPixels.Count;
        clicker.SpawnStoredPixel(tier, amount, c.PortWorld + Vector3.up * clicker.OldPixelWorldSize, Vector3.zero);
        if (clicker.OldPixels.Count <= before) return false;
        Emit(d, socketId, clicker.OldPixels[clicker.OldPixels.Count - 1]);
        return true;
    }

    /// <summary>Hands out one stored husk (EmitStored): its tier and value, false when empty.</summary>
    public delegate bool TakeStored(out int tier, out double amount);

    /// <summary>May the device let a husk out of this socket now? False only when an Out coupling there feeds a belt with no room at its start.</summary>
    public static bool OutputHasRoom(IPixelSocketDevice d, string socketId)
    {
        PixelCoupling c = PixelCoupling.On(d, socketId);
        if (c == null || !c.IsOut) return true;
        PixelConveyor belt = BeltStartingAt(c.PortWorld);
        return belt == null || PixelConveyorSystem.StartHasRoom(belt);
    }

    /// <summary>Is there an Out coupling with a belt starting at it on this socket (the device then feeds the belt instead of flinging)?</summary>
    public static bool OutputConnected(IPixelSocketDevice d, string socketId)
    {
        PixelCoupling c = PixelCoupling.On(d, socketId);
        return c != null && c.IsOut && BeltStartingAt(c.PortWorld) != null;
    }

    // ---- placing: snap points ----

    /// <summary>
    /// Calls 'visit' with the port of every coupling (in = In couplings, out = Out couplings) and of every FREE socket that can work that way (a coupling is
    /// fitted there when a belt / hose attaches - AutoFitAt): belts and hoses snap to them.
    /// </summary>
    public static void Ports(System.Action<Vector3> visit, bool ins = true, bool outs = true)
    {
        foreach (PixelCoupling c in PixelCoupling.All)
            if (c != null && Alive(c.Device) && ((ins && c.IsIn) || (outs && c.IsOut))) visit(c.PortWorld);
        for (int i = devices.Count - 1; i >= 0; i--)
        {
            IPixelSocketDevice d = devices[i];
            if (!Alive(d)) continue;
            foreach (PixelSocket s in FreeSockets(d)) if ((ins && s.CanTakeIn) || (outs && s.CanGiveOut)) visit(Port(d, s));
        }
    }

    /// <summary>The coupling whose port is nearest this point (within 'reach', flat distance); 'wantOut' / 'wantIn' filter its direction.</summary>
    public static PixelCoupling NearestCoupling(Vector3 at, float reach, bool wantIn, bool wantOut)
    {
        PixelCoupling best = null;
        float bestD = reach * reach;
        foreach (PixelCoupling c in PixelCoupling.All)
        {
            if (c == null || !Alive(c.Device) || !((wantIn && c.IsIn) || (wantOut && c.IsOut))) continue;
            Vector3 d = c.PortWorld - at;
            if (Mathf.Abs(d.y) > 1.5f * HuskWidth) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= bestD) { bestD = d.sqrMagnitude; best = c; }
        }
        return best;
    }

    // ---- saving (couplings are saved with their device) ----

    /// <summary>The couplings fitted by the player on this device (not the built-in ones), as 'socketId:I;socketId:O;'. Empty when none.</summary>
    public static string Export(GameObject root)
    {
        if (root == null) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (PixelCoupling c in PixelCoupling.All)
        {
            if (c == null || c.Fitted || c.Socket == null || !Alive(c.Device) || c.Device.SocketRoot != root.transform) continue;
            sb.Append(c.Socket.id).Append(':').Append(c.IsIn ? 'I' : 'O').Append(';');
        }
        return sb.ToString();
    }

    /// <summary>Fits the saved couplings back onto the device under 'root'.</summary>
    public static void Import(GameObject root, string data)
    {
        if (root == null || string.IsNullOrEmpty(data)) return;
        IPixelSocketDevice d = root.GetComponentInChildren<IPixelSocketDevice>();
        if (!Alive(d)) return;
        foreach (string entry in data.Split(';'))
        {
            string[] f = entry.Split(':');
            PixelSocket s = f.Length > 0 ? Find(d, f[0]) : null;
            if (s == null || !IsFree(d, s)) continue;
            PixelCoupling c = PixelCoupling.Fit(d, s);
            if (c != null && f.Length > 1) c.SetDirection(f[1] == "I");
        }
    }

}
