// ---- System notes ----
// PixelCoupling.cs
//   The COUPLING: the ONE snap-on piece that joins belts, hoses and connectors to a device's SOCKET (PixelSockets.cs; design docs/systems/PixelCouplings.md).
//   - Fit(device, socket): a child of the device's SocketRoot at the socket's port, facing out of it, so it moves / turns with the device (carry handles)
//     and goes when the device goes. Built with PixelFactoryKit: a flange tinted a little with the device's colour (tintShare of SocketTint), a collar
//     with a dark opening, a lip at belt height, and a flow ARROW over it that drifts the way husks go - green pointing INTO the device = In, orange
//     pointing OUT = Out. A coupling on a Both socket (Storage Crate, Quantum Organizer) shows a small teal dot: LEFT-CLICK it to switch In / Out
//     (PixelPointerClaim.ClaimButton keeps the cube and Pixel Grabbing out of that click). Its parts are named "Coupling ..." so the device's own
//     hover / remove check (PixelPlacedDevice.CountsForHit) leaves them out.
//   - TakeIn(husk) = the device's ONE hook (IPixelSocketDevice.AcceptHusk); PortWorld / FacingWorld = where belts meet it.
//   - Fitted couplings (socket.fitted: the Elevator, Splitter and Connector, which are belt fittings themselves) are built with the piece (FitBuiltIn),
//     are not saved and cannot be removed on their own.
//   - PLACING (Dev Hub > Tester > 'Coupling'): the ghost (CreateGhost) shows a teal ring on every FREE socket in view and magnetizes to the nearest one
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
    private float u = 0.5f;

    public IPixelSocketDevice Device => device;
    public PixelSocket Socket => socket;
    public bool Fitted => socket != null && socket.fitted;
    public bool IsIn => socket != null && !outward && socket.CanTakeIn;
    public bool IsOut => socket != null && outward && socket.CanGiveOut;
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
    private static float HuskWidth { get { PixelClicker c = Clicker; return c != null && c.OldPixelWorldSize > 0f ? c.OldPixelWorldSize : 0.5f; } }

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
        c.device = d;
        c.socket = s;
        c.outward = s.flow == PixelSocketFlow.Output;
        c.Build(false, d.SocketTint);
        all.Add(c);
        return c;
    }

    /// <summary>Fits the device's built-in couplings (sockets marked fitted). Call once the piece is built (not for ghosts).</summary>
    public static void FitBuiltIn(IPixelSocketDevice d)
    {
        if (d == null) return;
        foreach (PixelSocket s in d.Sockets) if (s != null && s.fitted && On(d, s) == null) Fit(d, s);
    }

    /// <summary>On a Both socket: In (true) or Out (false). Other sockets keep their own direction.</summary>
    public void SetDirection(bool intake)
    {
        if (socket == null || socket.flow != PixelSocketFlow.Both) return;
        outward = !intake;
        ShowArrow();
    }

    private void OnDestroy() { all.Remove(this); }

    // ---- the model ----

    private void Build(bool asGhost, Color tint)
    {
        ghost = asGhost;
        u = HuskWidth;
        PixelFactoryKit kit = new PixelFactoryKit(Clicker, asGhost);
        Transform t = transform;
        Color steel = new Color(0.62f, 0.66f, 0.7f, 1f), dark = new Color(0.12f, 0.13f, 0.15f, 1f);
        Color flange = Color.Lerp(steel, tint, Mathf.Clamp01(tintShare));
        Vector3 axis = new Vector3(90f, 0f, 0f);   // cylinders lie along the socket's facing (+z)
        kit.Cyl("Coupling Flange", t, new Vector3(0f, 0.5f * u, 0.04f * u), 0.46f * u, 0.14f * u, flange, axis, 0.6f, 0.55f);
        kit.Cyl("Coupling Collar", t, new Vector3(0f, 0.5f * u, 0.2f * u), 0.34f * u, 0.3f * u, Color.Lerp(dark, flange, 0.45f), axis, 0.7f, 0.6f);
        kit.Cyl("Coupling Opening", t, new Vector3(0f, 0.5f * u, 0.22f * u), 0.24f * u, 0.32f * u, Color.black, axis, 0f, 0.1f);
        kit.Box("Coupling Lip", t, new Vector3(0f, 0.05f * u, 0.22f * u), new Vector3(0.95f * u, 0.1f * u, 0.5f * u), Color.Lerp(dark, flange, 0.3f), default, false, 0.5f, 0.5f);
        for (int sx = -1; sx <= 1; sx += 2)   // two bolts on the flange
            kit.Cyl("Coupling Bolt", t, new Vector3(sx * 0.34f * u, 0.82f * u, 0.12f * u), 0.05f * u, 0.06f * u, steel, axis, 0.9f, 0.7f);
        arrowIn = BuildArrow(kit, "Coupling Arrow In", false, inColor);
        arrowOut = BuildArrow(kit, "Coupling Arrow Out", true, outColor);
        if (socket != null && socket.flow == PixelSocketFlow.Both && !asGhost)   // a teal dot: click to switch In / Out
            kit.Ball("Coupling Switch", t, new Vector3(0f, 0.5f * u, -0.06f * u), 0.09f * u, new Color(0.3f, 0.95f, 0.95f, 1f), 0.2f, 0.8f);
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
        if (arrowIn != null) arrowIn.gameObject.SetActive(!outward);
        if (arrowOut != null) arrowOut.gameObject.SetActive(outward);
    }

    // ---- every frame: the arrow drifts the way husks go; a Both coupling switches on a click ----

    private void Update()
    {
        Transform arrow = outward ? arrowOut : arrowIn;
        if (arrow != null && arrow.gameObject.activeSelf)
        {
            float k = Mathf.Repeat(Time.time * 1.2f, 1f);
            arrow.localPosition = new Vector3(0f, 1.05f * u, 0.2f * u + (outward ? 1f : -1f) * (k - 0.5f) * 2f * Mathf.Max(0f, arrowDrift) * u);
        }
        if (ghost || socket == null || socket.flow != PixelSocketFlow.Both || PixelCalm.InMenu || PixelInput.PointerOverUI()) return;
        PixelClicker clicker = Clicker;
        Camera cam = clicker != null ? clicker.ViewCamera : null;
        if (cam == null || !PointerOn(cam)) return;
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
        foreach (Renderer r in parts) if (r != null && r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(ray)) return true;
        return false;
    }

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
            if (d == null || (d is Object o && o == null) || d.SocketRoot == null) continue;
            foreach (PixelSocket s in d.Sockets)
            {
                if (s == null || s.fitted || s.kind != PixelSocketKind.Husk || !PixelCouplings.IsFree(d, s)) continue;
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
            if (d == null || (d is Object o && o == null) || d.SocketRoot == null) continue;
            foreach (PixelSocket s in d.Sockets)
            {
                if (s == null || s.fitted || s.kind != PixelSocketKind.Husk || !PixelCouplings.IsFree(d, s)) continue;
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
        float u = c != null && c.OldPixelWorldSize > 0f ? c.OldPixelWorldSize : 0.5f;
        int used = 0;
        foreach (IPixelSocketDevice d in PixelCouplings.Devices)
        {
            if (d == null || (d is Object o && o == null) || d.SocketRoot == null) continue;
            foreach (PixelSocket s in d.Sockets)
            {
                if (s == null || s.fitted || s.kind != PixelSocketKind.Husk || !PixelCouplings.IsFree(d, s)) continue;
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
