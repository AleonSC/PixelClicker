// ---- System notes ----
// PixelCoupling.cs
//   The COUPLING: the ONE snap-on piece that joins belts and hoses to a device's SOCKET (PixelSockets.cs; design docs/systems/PixelCouplings.md).
//   - AUTO-FIT: couplings are not bought - one is fitted when a belt end / hose end attaches at a free socket (PixelCouplings.AutoFitAt / AutoFitAll).
//   - Fit(device, socket): a child of the device's SocketRoot at the socket's port, facing out of it, so it moves / turns with the device (carry handles)
//     and goes when the device goes. Built with PixelFactoryKit: a flange tinted a little with the device's colour (tintShare of SocketTint), a collar
//     with a dark opening, a lip at belt height, and a flow ARROW over it that drifts the way husks go - green pointing INTO the device = In, orange
//     pointing OUT = Out. A coupling on a Both socket (Storage Crate, Quantum Organizer) shows a small teal dot: LEFT-CLICK the DOT to switch In / Out
//     (only the dot is hit-tested; PixelPointerClaim.ClaimButton keeps the cube and Pixel Grabbing out of that click). Its parts are named "Coupling ..." so the device's own
//     hover / remove check (PixelPlacedDevice.CountsForHit) leaves them out.
//   - AUTO DIRECTION (socket.autoDirection, the Junction Box): no teal dot and no click switch; the device calls SetAuto(In / Out / Idle) from what is
//     attached; idle shows no arrow and counts both ways (IsIn and IsOut) so belts and hoses can snap to it.
//   - TakeIn(husk) = the device's ONE hook (IPixelSocketDevice.AcceptHusk); PortWorld / FacingWorld = where belts meet it.
//   - Fitted couplings (socket.fitted: the Elevator and Belt Sorter, which are belt fittings themselves) are built with the piece (FitBuiltIn),
//     are not saved and cannot be removed on their own.
//   - HOSE ADAPTER (2026-10-24): while a Container Hose ends on it (SetHoseFitting, called by the hose), the coupling grows a form-fitting
//     adapter: a rubber sleeve over the collar, a steel bell flaring from the collar out to the hose's own width (a PixelTube with a growing
//     radius) and a clamp band with its screw where the hose goes in; the belt lip and the dark opening hide. HoseMouthWorld / HoseMouth(...) =
//     where the hose itself starts (inside the bell, under the clamp). It goes again when the last hose on it is removed.
//   - Keeps the same world size when its device rescales (the Recycler's Bin Size): KeepSize every frame.
//   - PLACING by hand (dev only: Dev Hub > Tester > 'Coupling'): the ghost (CreateGhost) shows a teal ring on every FREE socket in view and magnetizes to the nearest one
//     (TrySnapGhost: by screen distance, its arrow shows that socket's direction); a click fits it there (PlaceAt). Hold the right button on a placed
//     coupling to remove it (the tester's delete). Saved with the device (PixelCouplings.Export / Import).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelCoupling : MonoBehaviour
{
    [Header("Look")]
    [Range(0f, 1f)]
    [Tooltip("How much of the device's own colour the flange takes (0 = plain steel, 1 = the device's colour).")]
    [SerializeField] private float tintShare = 0.35f;
    [Tooltip("Arrow colour of an In coupling (husks go into the device).")]
    [SerializeField] private Color inColor = new Color(0.35f, 1f, 0.45f, 1f);
    [Tooltip("Arrow colour of an Out coupling (husks come out of the device).")]
    [SerializeField] private Color outColor = new Color(1f, 0.6f, 0.15f, 1f);
    [Tooltip("How far the flow arrow drifts back and forth (husk widths).")]
    [SerializeField] private float arrowDrift = 0.12f;

    private static readonly List<PixelCoupling> all = new List<PixelCoupling>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    /// <summary>Every coupling in the world (fitted ones too; not the placing ghost).</summary>
    public static IReadOnlyList<PixelCoupling> All => all;

    private IPixelSocketDevice device;
    private PixelSocket socket;
    private bool outward;          // true = Out (husks leave the device here)
    private bool ghost;
    private Transform arrowIn, arrowOut;
    private Renderer[] parts;
    private Renderer switchDot;    // the teal dot of a Both coupling: the only part a click switches on
    private float u = 0.5f;

    public IPixelSocketDevice Device => device;
    public PixelSocket Socket => socket;
    public bool Fitted => socket != null && socket.fitted;
    public bool IsIn => socket != null && (auto ? autoState != AutoState.Out : !outward && socket.CanTakeIn);   // (an idle auto coupling counts both ways: belts / hoses can snap to it)
    public bool IsOut => socket != null && (auto ? autoState != AutoState.In : outward && socket.CanGiveOut);

    /// <summary>An auto-direction coupling's state (PixelSocket.autoDirection): set by its device from what is attached.</summary>
    public enum AutoState { Idle, In, Out }
    private bool auto;
    private AutoState autoState;
    public bool Auto => auto;
    public AutoState Direction => auto ? autoState : outward ? AutoState.Out : AutoState.In;

    /// <summary>An auto-direction coupling: In / Out / idle from what is attached (its device works it out). The arrow shows it; idle = no arrow.</summary>
    public void SetAuto(AutoState state)
    {
        if (!auto || autoState == state) return;
        autoState = state;
        outward = state == AutoState.Out;
        ShowArrow();
    }

    /// <summary>Is the pointer ray on this coupling's parts?</summary>
    public bool PointerOver(Camera cam) => cam != null && PointerOn(cam);
    public Vector3 PortWorld => device != null && device.SocketRoot != null ? PixelCouplings.Port(device, socket) : transform.position;
    public Vector3 FacingWorld => device != null && device.SocketRoot != null ? PixelCouplings.Facing(device, socket) : transform.forward;

    /// <summary>The coupling on this socket of this device (null if none).</summary>
    public static PixelCoupling On(IPixelSocketDevice d, PixelSocket s)
    {
        if (d == null || s == null) return null;
        foreach (PixelCoupling c in all) if (c != null && c.device == d && c.socket == s) return c;
        return null;
    }

    public static PixelCoupling On(IPixelSocketDevice d, string socketId)
    {
        if (d == null) return null;
        foreach (PixelCoupling c in all) if (c != null && c.device == d && c.socket != null && c.socket.id == socketId) return c;
        return null;
    }

    /// <summary>A husk arrives at this In coupling: the device's one hook takes it or refuses it.</summary>
    public bool TakeIn(Rigidbody husk) => IsIn && husk != null && device.AcceptHusk(socket, husk);

    // ---- fitting ----

    private static PixelClicker Clicker => PixelFind.Cached<PixelClicker>();
    private static float HuskWidth => PixelCouplings.HuskWidth;

    /// <summary>Fits a coupling onto a socket (a child of the device, facing out of the socket). Null if the socket is taken.</summary>
    public static PixelCoupling Fit(IPixelSocketDevice d, PixelSocket s)
    {
        if (d == null || s == null || d.SocketRoot == null || !PixelCouplings.IsFree(d, s)) return null;
        GameObject go = new GameObject("Coupling (" + s.id + ")");
        go.transform.SetParent(d.SocketRoot, false);
        go.transform.localPosition = s.localPosition;
        Vector3 f = s.localFacing; f.y = 0f;
        go.transform.localRotation = Quaternion.LookRotation(f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward, Vector3.up);
        PixelCoupling c = go.AddComponent<PixelCoupling>();
        PixelCouplings.KeepSize(go.transform);   // the same size on every device (a scaled machine - the Wood Chipper, a bigger Recycler - does not blow it up)
        c.device = d;
        c.socket = s;
        c.outward = s.flow == PixelSocketFlow.Output;
        c.auto = s.autoDirection && s.flow == PixelSocketFlow.Both;
        if (!s.bare) c.Build(false, d.SocketTint);   // (a bare fitted socket is logic only: the piece's own shape shows the way)
        all.Add(c);
        return c;
    }

    /// <summary>Fits the device's built-in couplings (sockets marked fitted). Call once the piece is built (not for ghosts).</summary>
    public static void FitBuiltIn(IPixelSocketDevice d)
    {
        if (d == null) return;
        foreach (PixelSocket s in d.Sockets) if (s != null && s.fitted && On(d, s) == null) Fit(d, s);
    }

    /// <summary>Takes every coupling off this device at once (a piece that rebuilds its model: its fitted couplings are made again after).</summary>
    public static void RemoveAll(IPixelSocketDevice d)
    {
        for (int i = all.Count - 1; i >= 0; i--)
        {
            PixelCoupling c = all[i];
            if (c == null) { all.RemoveAt(i); continue; }
            if (c.device != d) continue;
            all.RemoveAt(i);
            c.gameObject.SetActive(false);
            Destroy(c.gameObject);
        }
    }

    /// <summary>On a Both socket: In (true) or Out (false). Other sockets keep their own direction.</summary>
    public void SetDirection(bool intake)
    {
        if (socket == null || socket.flow != PixelSocketFlow.Both || auto) return;   // (an auto coupling follows what is attached)
        outward = !intake;
        ShowArrow();
    }

    private void OnDestroy() { all.Remove(this); if (bellMesh != null) Destroy(bellMesh); }

    // ---- the hose adapter (a Container Hose ends here) ----

    /// <summary>How far up from the port, and out along its facing, a hose starts (inside the adapter's bell, under its clamp), in husk widths.</summary>
    public const float HoseMouthUp = 0.5f, HoseMouthOut = 0.72f;
    private const float BellStart = 0.36f, BellEnd = 0.82f, SleeveRadius = 0.39f;

    /// <summary>Where a hose ending on a coupling at this port starts (also for a free socket that will get one).</summary>
    public static Vector3 HoseMouth(Vector3 port, Vector3 facing, float u) => port + Vector3.up * HoseMouthUp * u + facing * HoseMouthOut * u;
    public Vector3 HoseMouthWorld => HoseMouth(PortWorld, FacingWorld, u);
    public bool HoseFitted => hoseAdapter != null;

    private GameObject hoseAdapter;
    private Mesh bellMesh;
    private Color flangeColor = new Color(0.62f, 0.66f, 0.7f, 1f);
    private float fittedRadius;

    /// <summary>Grows (on) or removes (off) the form-fitting hose adapter for a hose of 'hoseRadius' (world units).</summary>
    public void SetHoseFitting(bool on, float hoseRadius = 0f)
    {
        if (ghost) return;
        if (!on)
        {
            if (hoseAdapter == null) return;
            Destroy(hoseAdapter);
            hoseAdapter = null;
            if (bellMesh != null) { Destroy(bellMesh); bellMesh = null; }
            ShowBeltParts(true);
            parts = GetComponentsInChildren<Renderer>(true);
            return;
        }
        if (hoseAdapter != null && Mathf.Abs(fittedRadius - hoseRadius) < 0.001f) return;
        if (hoseAdapter != null) SetHoseFitting(false);
        fittedRadius = hoseRadius;
        float r = Mathf.Max(0.05f * u, hoseRadius), y = HoseMouthUp * u;
        PixelFactoryKit kit = new PixelFactoryKit(Clicker, false, true);
        Color rubber = new Color(0.14f, 0.15f, 0.17f, 1f), steel = new Color(0.78f, 0.8f, 0.84f, 1f);
        Vector3 axis = new Vector3(90f, 0f, 0f);
        hoseAdapter = new GameObject("Coupling Hose Adapter");
        Transform t = hoseAdapter.transform;
        t.SetParent(transform, false);
        // A rubber sleeve pulled over the collar, with a thin steel ring at its outer end.
        kit.Cyl("Coupling Hose Sleeve", t, new Vector3(0f, y, 0.21f * u), SleeveRadius * u, 0.32f * u, rubber, axis, 0f, 0.35f);
        kit.Cyl("Coupling Hose Ring", t, new Vector3(0f, y, BellStart * u), (SleeveRadius + 0.03f) * u, 0.06f * u, steel, axis, 0.9f, 0.7f);
        // The bell: a steel reducer flaring from the sleeve out to the hose's width (a tube along +z whose radius eases from one to the other).
        Vector3[] line = new Vector3[10];
        for (int i = 0; i < line.Length; i++) line[i] = new Vector3(0f, y, Mathf.Lerp(BellStart, BellEnd, i / (float)(line.Length - 1)) * u);
        float from = SleeveRadius * u, to = r + 0.04f * u;
        bellMesh = PixelTube.Build(line, 1f, 20, null, null, i => { float k = i / (float)(line.Length - 1); return Mathf.Lerp(from, to, k * k * (3f - 2f * k)); });
        GameObject bell = new GameObject("Coupling Hose Bell", typeof(MeshFilter), typeof(MeshRenderer));
        bell.transform.SetParent(t, false);
        bell.GetComponent<MeshFilter>().sharedMesh = bellMesh;
        bell.GetComponent<MeshRenderer>().sharedMaterial = kit.Mat(Color.Lerp(steel, flangeColor, 0.5f), 0.8f, 0.6f);
        // The clamp band over the joint, with its screw housing on top.
        kit.Cyl("Coupling Hose Clamp", t, new Vector3(0f, y, (BellEnd - 0.04f) * u), r + 0.08f * u, 0.13f * u, steel, axis, 0.95f, 0.75f);
        kit.Box("Coupling Hose Clamp Screw", t, new Vector3(0f, y + r + 0.11f * u, (BellEnd - 0.04f) * u), new Vector3(0.13f * u, 0.1f * u, 0.2f * u), steel, default, false, 0.95f, 0.75f);
        kit.Cyl("Coupling Hose Clamp Bolt", t, new Vector3(0f, y + r + 0.11f * u, (BellEnd - 0.04f) * u), 0.035f * u, 0.32f * u, new Color(0.5f, 0.52f, 0.55f, 1f), axis, 0.9f, 0.6f);
        ShowBeltParts(false);
        parts = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>The belt-height lip and the dark opening (hidden under a hose adapter).</summary>
    private void ShowBeltParts(bool show)
    {
        foreach (Transform c in transform)
            if (c.name == "Coupling Lip" || c.name == "Coupling Opening") c.gameObject.SetActive(show);
    }

    // ---- the model ----

    private void Build(bool asGhost, Color tint)
    {
        ghost = asGhost;
        u = HuskWidth;
        PixelFactoryKit kit = new PixelFactoryKit(Clicker, asGhost);
        Transform t = transform;
        Color steel = new Color(0.62f, 0.66f, 0.7f, 1f), dark = new Color(0.12f, 0.13f, 0.15f, 1f);
        Color flange = Color.Lerp(steel, tint, Mathf.Clamp01(tintShare));
        flangeColor = flange;
        Vector3 axis = new Vector3(90f, 0f, 0f);   // cylinders lie along the socket's facing (+z)
        kit.Cyl("Coupling Flange", t, new Vector3(0f, 0.5f * u, 0.04f * u), 0.46f * u, 0.14f * u, flange, axis, 0.6f, 0.55f);
        kit.Cyl("Coupling Collar", t, new Vector3(0f, 0.5f * u, 0.2f * u), 0.34f * u, 0.3f * u, Color.Lerp(dark, flange, 0.45f), axis, 0.7f, 0.6f);
        kit.Cyl("Coupling Opening", t, new Vector3(0f, 0.5f * u, 0.22f * u), 0.24f * u, 0.32f * u, Color.black, axis, 0f, 0.1f);
        kit.Box("Coupling Lip", t, new Vector3(0f, 0.05f * u, 0.22f * u), new Vector3(0.95f * u, 0.1f * u, 0.5f * u), Color.Lerp(dark, flange, 0.3f), default, false, 0.5f, 0.5f);
        for (int sx = -1; sx <= 1; sx += 2)   // two bolts on the flange
            kit.Cyl("Coupling Bolt", t, new Vector3(sx * 0.34f * u, 0.82f * u, 0.12f * u), 0.05f * u, 0.06f * u, steel, axis, 0.9f, 0.7f);
        arrowIn = BuildArrow(kit, "Coupling Arrow In", false, inColor);
        arrowOut = BuildArrow(kit, "Coupling Arrow Out", true, outColor);
        if (socket != null && socket.flow == PixelSocketFlow.Both && !socket.autoDirection && !asGhost)   // a teal dot: click to switch In / Out
        {
            GameObject dot = kit.Ball("Coupling Switch", t, new Vector3(0f, 0.5f * u, -0.06f * u), 0.09f * u, new Color(0.3f, 0.95f, 0.95f, 1f), 0.2f, 0.8f);
            switchDot = dot != null ? dot.GetComponent<Renderer>() : null;
        }
        ShowArrow();
        parts = GetComponentsInChildren<Renderer>(true);
    }

    /// <summary>A flat chevron over the collar; 'outward' = pointing out of the device (+z), else into it.</summary>
    private Transform BuildArrow(PixelFactoryKit kit, string name, bool pointsOut, Color c)
    {
        GameObject a = new GameObject(name);
        a.transform.SetParent(transform, false);
        a.transform.localPosition = new Vector3(0f, 1.05f * u, 0.2f * u);
        float dir = pointsOut ? 1f : -1f;
        for (int sx = -1; sx <= 1; sx += 2)
            kit.Box(name + " Arm", a.transform, new Vector3(sx * 0.13f * u, 0f, -dir * 0.1f * u), new Vector3(0.09f * u, 0.07f * u, 0.38f * u), c, new Vector3(0f, sx * dir * -40f, 0f), false, 0f, 0.6f, c * 1.2f);
        return a.transform;
    }

    private void ShowArrow()
    {
        bool idle = auto && autoState == AutoState.Idle;
        if (arrowIn != null) arrowIn.gameObject.SetActive(!idle && !outward);
        if (arrowOut != null) arrowOut.gameObject.SetActive(!idle && outward);
    }

    // ---- every frame: the arrow drifts the way husks go; a Both coupling switches on a click ----

    private void Update()
    {
        if (!ghost) PixelCouplings.KeepSize(transform);
        Transform arrow = outward ? arrowOut : arrowIn;
        if (arrow != null && arrow.gameObject.activeSelf)
        {
            float k = Mathf.Repeat(Time.time * 1.2f, 1f);
            arrow.localPosition = new Vector3(0f, 1.05f * u, 0.2f * u + (outward ? 1f : -1f) * (k - 0.5f) * 2f * Mathf.Max(0f, arrowDrift) * u);
        }
        if (ghost || auto || socket == null || socket.flow != PixelSocketFlow.Both || PixelCalm.InMenu || PixelInput.PointerOverUI()) return;
        PixelClicker clicker = Clicker;
        Camera cam = clicker != null ? clicker.ViewCamera : null;
        if (cam == null || !OnRenderer(switchDot, cam.ScreenPointToRay(PixelInput.PointerPosition()))) return;   // the teal dot only (C5: not the whole coupling)
        PixelPointerClaim.ClaimButton();
        if (!PixelInput.LeftPressed()) return;
        outward = !outward;
        ShowArrow();
        PixelAudio.Play("ui_click");
        PixelHints.Announce(outward ? "Coupling set to OUT: husks leave the device here" : "Coupling set to IN: husks go into the device here");
    }

    private bool PointerOn(Camera cam)
    {
        if (parts == null) return false;
        Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
        foreach (Renderer r in parts) if (OnRenderer(r, ray)) return true;
        return false;
    }

    private static bool OnRenderer(Renderer r, Ray ray) => r != null && r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(ray);

    // ---- placing (Dev Hub > Tester > 'Coupling') ----

    /// <summary>The ghost that follows the mouse while placing: both arrows (the snapped socket's direction shows), plus teal rings on the free sockets.</summary>
    public static GameObject CreateGhost(PixelClicker c)
    {
        GameObject go = new GameObject("Coupling (Preview)");
        PixelCoupling g = go.AddComponent<PixelCoupling>();
        g.Build(true, new Color(0.62f, 0.66f, 0.7f, 1f));
        if (g.arrowIn != null) g.arrowIn.gameObject.SetActive(false);
        if (g.arrowOut != null) g.arrowOut.gameObject.SetActive(false);
        go.AddComponent<PixelCouplingMarkers>();
        return go;
    }

    /// <summary>The free socket nearest the pointer (within ~80 screen pixels of its port, or 1.5 husk widths on the ground). False if none.</summary>
    private static bool NearestFree(PixelClicker c, Vector3 groundPoint, out IPixelSocketDevice bestDevice, out PixelSocket bestSocket)
    {
        bestDevice = null; bestSocket = null;
        Camera cam = c != null ? c.ViewCamera : null;
        Vector2 mouse = PixelInput.PointerPosition();
        float best = 80f * 80f, reach = 1.5f * HuskWidth;
        foreach (IPixelSocketDevice d in PixelCouplings.Devices)
        {
            foreach (PixelSocket s in PixelCouplings.FreeSockets(d))
            {
                Vector3 port = PixelCouplings.Port(d, s);
                float score = float.MaxValue;
                if (cam != null) { Vector3 sp = cam.WorldToScreenPoint(port + Vector3.up * 0.5f * HuskWidth); if (sp.z > 0f) score = (new Vector2(sp.x, sp.y) - mouse).sqrMagnitude; }
                Vector3 flat = port - groundPoint; flat.y = 0f;
                if (flat.sqrMagnitude < reach * reach) score = Mathf.Min(score, 0f);
                if (score < best) { best = score; bestDevice = d; bestSocket = s; }
            }
        }
        return bestDevice != null;
    }

    /// <summary>Placing: the ghost magnetizes to the nearest free socket (turned out of it) and its arrow shows that socket's direction.</summary>
    public static bool TrySnapGhost(PixelClicker c, Vector3 point, float yaw, GameObject ghostObject, out Vector3 snappedPoint, out float snappedYaw)
    {
        snappedPoint = point; snappedYaw = yaw;
        PixelCoupling g = ghostObject != null ? ghostObject.GetComponent<PixelCoupling>() : null;
        bool found = NearestFree(c, point, out IPixelSocketDevice d, out PixelSocket s);
        if (g != null)
        {
            g.outward = found && s.flow == PixelSocketFlow.Output;
            if (g.arrowIn != null) g.arrowIn.gameObject.SetActive(found && s.flow != PixelSocketFlow.Output);
            if (g.arrowOut != null) g.arrowOut.gameObject.SetActive(found && s.flow == PixelSocketFlow.Output);
        }
        if (!found) return false;
        snappedPoint = PixelCouplings.Port(d, s);
        Vector3 f = PixelCouplings.Facing(d, s);
        snappedYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        return true;
    }

    /// <summary>Placing: fits a coupling on the free socket at this point (where the ghost snapped). Null (with a hint) when no socket is there.</summary>
    public static GameObject PlaceAt(PixelClicker c, Vector3 point, float yaw)
    {
        IPixelSocketDevice bestDevice = null;
        PixelSocket bestSocket = null;
        float best = (0.6f * HuskWidth) * (0.6f * HuskWidth);
        foreach (IPixelSocketDevice d in PixelCouplings.Devices)
        {
            foreach (PixelSocket s in PixelCouplings.FreeSockets(d))
            {
                float dist = (PixelCouplings.Port(d, s) - point).sqrMagnitude;
                if (dist < best) { best = dist; bestDevice = d; bestSocket = s; }
            }
        }
        if (bestDevice == null) { PixelHints.Announce("A coupling goes on a device's socket: bring it near one of the teal rings"); return null; }
        PixelCoupling made = Fit(bestDevice, bestSocket);
        if (made == null) return null;
        PixelAudio.Play("device_place");
        return made.gameObject;
    }

    /// <summary>The placed couplings the player can remove (the tester lists these): every coupling that is not built into its piece.</summary>
    public static IEnumerable<GameObject> Removable()
    {
        foreach (PixelCoupling c in all) if (c != null && !c.Fitted) yield return c.gameObject;
    }
}

/// <summary>On the coupling ghost while placing: a teal ring on every free socket, so the player sees where couplings go.</summary>
public class PixelCouplingMarkers : MonoBehaviour
{
    [Tooltip("Colour of the rings that mark free sockets while a coupling is being placed.")]
    [SerializeField] private Color markerColor = new Color(0.3f, 0.95f, 0.95f, 0.45f);

    private readonly List<GameObject> markers = new List<GameObject>();
    private float next;

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 0.25f;
        PixelClicker c = PixelFind.Cached<PixelClicker>();
        float u = PixelCouplings.HuskWidth;
        int used = 0;
        foreach (IPixelSocketDevice d in PixelCouplings.Devices)
        {
            foreach (PixelSocket s in PixelCouplings.FreeSockets(d))
            {
                if (used >= markers.Count)
                {
                    PixelFactoryKit kit = new PixelFactoryKit(c, true);
                    markers.Add(kit.Cyl("Socket Marker", null, Vector3.zero, 0.42f * u, 0.03f * u, markerColor, new Vector3(90f, 0f, 0f), 0f, 0.9f));
                }
                GameObject m = markers[used++];
                Vector3 f = PixelCouplings.Facing(d, s);
                m.transform.SetPositionAndRotation(PixelCouplings.Port(d, s) + Vector3.up * 0.5f * u, Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(90f, 0f, 0f));
                m.SetActive(true);
            }
        }
        for (int i = used; i < markers.Count; i++) if (markers[i] != null) markers[i].SetActive(false);
    }

    private void OnDisable() { foreach (GameObject m in markers) if (m != null) m.SetActive(false); next = 0f; }
    private void OnDestroy() { foreach (GameObject m in markers) if (m != null) Destroy(m); markers.Clear(); }
}
