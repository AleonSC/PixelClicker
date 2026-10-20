// ---- System notes ----
// PixelSockets.cs
//   SOCKETS: the ONE way belts and hoses connect to devices (design: docs/systems/PixelCouplings.md). A device declares its sockets as data
//   (PixelSocket: id, port = local position where a belt end / hose end meets it, outward facing, flow Intake / Output / Both, kind, fitted) and
//   implements IPixelSocketDevice: its sockets, a tint for its couplings, and ONE hook for husks coming in (AcceptHusk: take it or refuse when full).
//   Devices register in OnEnable / unregister in OnDisable (PixelCouplings.Register). A PixelCoupling (PixelCoupling.cs) snaps onto a socket; belts,
//   hoses and connectors only ever talk to couplings:
//   - a belt END / hose end at an In coupling hands its husk in: PixelCouplings.HandIn(at, husk) -> the device's AcceptHusk;
//   - a device putting a husk out calls PixelCouplings.Emit(device, socketId, husk): an Out coupling on that socket puts it on the belt that starts at
//     its port (PixelConveyorSystem.StartHasRoom), or hands it to an In coupling there (device to device); false = nothing attached, the device does
//     its own thing (fling / drop). OutputHasRoom = may the device let one out now (the Funnel waits when a belt there is full). Emit never puts a husk
//     on a FULL belt (false, like nothing attached): a device that must not drop it asks OutputHasRoom first and holds it until there is room.
//   - ONE reach for every link check (LinkReach, in husk widths): a belt end hands in to a coupling, and a coupling finds the belt ending / starting at
//     it, at the same distance - so a belt that shows as joined really is (the nearest one wins).
//   - placing belts / hoses snaps to coupling ports (Ports).
//   Optional extras: IPixelHuskSource (a hose may pull stored husks out of an Out coupling: Storage Crate, Quantum Organizer), IPixelHuskRoom (how many
//   more husks an In coupling's device takes now: a hose only pumps what fits). A socket can be OCCUPIED by something that is not a coupling (the
//   a device standing on a socket: Occupy / Vacate). Couplings are saved with their device: Export / Import (socket ids +
//   direction) into PlacedState.couplings / ... An AUTO-DIRECTION socket (autoDirection, the Junction Box) has no fixed way: its device sets its coupling
//   In / Out / idle from what is attached (PixelCoupling.SetAuto; BeltEndingAt / BeltStartingAt, PixelContainerHose.Pulls / Pushes). Saved / TesterSave.couplings. Socket KINDS are an open list (PixelSocketKind, appended only): Husk is built;
//   Power (Electrician) and Signal (Automation) are planned, Water / Heat / Link are ideas - each = a new value + its own coupling look + its own hook.
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>What flows through a socket. An open list: append new kinds at the end (Unity stores enums as numbers).</summary>
public enum PixelSocketKind
{
    /// <summary>Husks (belts, hoses). The only kind built so far.</summary>
    Husk = 0,
    /// <summary>Planned (Electrician): electricity that speeds machines up or keeps them running.</summary>
    Power = 1,
    /// <summary>Planned (Automation): on / off triggers between devices.</summary>
    Signal = 2,
    /// <summary>Idea (Farming / Fishing): water piped between devices.</summary>
    Water = 3,
    /// <summary>Idea (Firemaking): fuel and heat.</summary>
    Heat = 4,
    /// <summary>Idea (late game): a wireless pair that passes a husk or effect along.</summary>
    Link = 5,
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
    [Tooltip("What flows through it. Only Husk is built.")]
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

/// <summary>
/// A device that catches husks handed in NEAR it, with no coupling needed (the Conveyor Elevator: a belt ending near its base, a device's Out coupling or a
/// Belt Sorter's chute beside it). PixelCouplings.HandIn asks these after the In couplings (RegisterAutoIntake / UnregisterAutoIntake).
/// </summary>
public interface IPixelAutoIntake
{
    /// <summary>A husk (kinematic, still on the husk list) is handed in at 'at': take it (true) if it is near enough and there is room.</summary>
    bool CatchNear(Vector3 at, Rigidbody husk);
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

/// <summary>The socket registry and the shared connect steps (hand a husk in, put one out, snap points, saving).</summary>
public static class PixelCouplings
{
    private static readonly List<IPixelSocketDevice> devices = new List<IPixelSocketDevice>();
    private static readonly List<IPixelAutoIntake> autoIntakes = new List<IPixelAutoIntake>();

    /// <summary>A device that catches husks handed in near it without a coupling (IPixelAutoIntake) came into the world / went.</summary>
    public static void RegisterAutoIntake(IPixelAutoIntake d) { if (d != null && !autoIntakes.Contains(d)) autoIntakes.Add(d); }
    public static void UnregisterAutoIntake(IPixelAutoIntake d) { autoIntakes.Remove(d); }
    private static readonly Dictionary<string, Object> occupants = new Dictionary<string, Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { devices.Clear(); occupants.Clear(); autoIntakes.Clear(); PixelStatics.ResetOnRestart(() => { devices.Clear(); occupants.Clear(); autoIntakes.Clear(); }); }

    public static IReadOnlyList<IPixelSocketDevice> Devices => devices;

    /// <summary>A device with sockets came into the world (call in OnEnable; ghosts / pictures do not register).</summary>
    public static void Register(IPixelSocketDevice d) { if (d != null && !devices.Contains(d)) devices.Add(d); }
    public static void Unregister(IPixelSocketDevice d) { devices.Remove(d); }

    private static bool Alive(IPixelSocketDevice d) => d != null && !(d is Object o && o == null) && d.SocketRoot != null;

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

    private static string Key(IPixelSocketDevice d, string id) => ((Object)d).GetInstanceID() + "/" + id;

    /// <summary>Something that is not a coupling sits on this socket (the Sorting Hopper on the Recycler's back hatch): no coupling may be fitted there.</summary>
    public static void Occupy(IPixelSocketDevice d, string id, Object by) { if (Alive(d)) occupants[Key(d, id)] = by; }
    public static void Vacate(IPixelSocketDevice d, string id, Object by)
    {
        if (!Alive(d)) return;
        string k = Key(d, id);
        if (occupants.TryGetValue(k, out Object o) && o == by) occupants.Remove(k);
    }
    public static bool Occupied(IPixelSocketDevice d, string id) => Alive(d) && occupants.TryGetValue(Key(d, id), out Object o) && o != null;

    /// <summary>Is the socket free for a coupling (none on it, nothing else sitting there)?</summary>
    public static bool IsFree(IPixelSocketDevice d, PixelSocket s) => Alive(d) && s != null && PixelCoupling.On(d, s) == null && !Occupied(d, s.id);

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
        if (c != null && c.TakeIn(husk)) return true;
        for (int i = autoIntakes.Count - 1; i >= 0; i--)   // no coupling took it: a device that catches husks near it (the Conveyor Elevator)
        {
            IPixelAutoIntake d = autoIntakes[i];
            if (d == null || (d is Object o && o == null)) { autoIntakes.RemoveAt(i); continue; }
            if (ReferenceEquals(d, exclude)) continue;
            if (d.CatchNear(at, husk)) return true;
        }
        return false;
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

    /// <summary>Calls 'visit' with the port of every coupling (in = In couplings, out = Out couplings): belts, connectors and hoses snap to them.</summary>
    public static void Ports(System.Action<Vector3> visit, bool ins = true, bool outs = true)
    {
        foreach (PixelCoupling c in PixelCoupling.All)
            if (c != null && Alive(c.Device) && ((ins && c.IsIn) || (outs && c.IsOut))) visit(c.PortWorld);
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

    private static float HuskWidth
    {
        get { PixelClicker c = PixelFind.Cached<PixelClicker>(); return c != null && c.OldPixelWorldSize > 0f ? c.OldPixelWorldSize : 0.5f; }
    }
}
