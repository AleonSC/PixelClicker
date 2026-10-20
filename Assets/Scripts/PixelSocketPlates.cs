// ---- System notes ----
// PixelSocketPlates.cs
//   Shows every FREE socket on a placed device as a small PORT PLATE on its opening, all the time (not only while a coupling is being placed), so the
//   player can see where couplings go and which way husks flow there: a flat plate at the socket's port with a chevron - green pointing INTO the device =
//   In, orange pointing OUT = Out, teal = either way (Both). A plate is a child of the device (it moves / turns with it) and goes away when a coupling is
//   fitted on that socket (couplings fit themselves when a belt / hose attaches) and comes back when the socket is free again.
//   Checked every refreshSeconds over PixelCouplings.Devices (PixelCouplings.FreeSockets); fitted (built-in) sockets get no plate. Plates keep their size
//   when the device rescales (PixelCouplings.KeepSize). The same refresh runs the AUTO-FIT pass (PixelCouplings.AutoFitAll), also with plates off. Parts are named "Coupling Plate ..."
//   so a device's own hover / remove check (PixelPlacedDevice.CountsForHit) leaves them out. disablePlates turns them off. In PixelSystems.All (World).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelSocketPlates : MonoBehaviour
{
    [Tooltip("Turn the port plates off (free sockets then only show while a coupling is being placed).")]
    [SerializeField] private bool disablePlates = false;
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of which sockets are free.")]
    [SerializeField] private float refreshSeconds = 0.3f;
    [Tooltip("Plate colour of an In socket (husks go into the device).")]
    [SerializeField] private Color inColor = new Color(0.35f, 1f, 0.45f, 1f);
    [Tooltip("Plate colour of an Out socket (husks come out of the device).")]
    [SerializeField] private Color outColor = new Color(1f, 0.6f, 0.15f, 1f);
    [Tooltip("Plate colour of a socket that works either way.")]
    [SerializeField] private Color bothColor = new Color(0.3f, 0.95f, 0.95f, 1f);

    private PixelClicker clicker;
    private float next;
    private readonly Dictionary<string, GameObject> plates = new Dictionary<string, GameObject>();
    private readonly HashSet<string> seen = new HashSet<string>();
    private readonly List<string> stale = new List<string>();

    private void Awake()
    {
        clicker = GetComponent<PixelClicker>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + Mathf.Max(0.05f, refreshSeconds);
        PixelCouplings.AutoFitAll();   // a belt end / start at a free socket gets its coupling (before the plates look, so its plate goes now)
        seen.Clear();
        if (!disablePlates)
            foreach (IPixelSocketDevice d in PixelCouplings.Devices)
                foreach (PixelSocket s in PixelCouplings.FreeSockets(d))
                {
                    string key = PixelObjectId.Of((Object)d) + "/" + s.id;
                    seen.Add(key);
                    if (plates.TryGetValue(key, out GameObject g) && g != null) { PixelCouplings.KeepSize(g.transform); continue; }
                    plates[key] = Build(d, s);
                }
        // Plates whose socket is taken (or whose device went) are removed.
        stale.Clear();
        foreach (KeyValuePair<string, GameObject> kv in plates) if (!seen.Contains(kv.Key) || kv.Value == null) stale.Add(kv.Key);
        foreach (string k in stale)
        {
            if (plates.TryGetValue(k, out GameObject g) && g != null) Destroy(g);
            plates.Remove(k);
        }
    }

    /// <summary>A flat plate at the socket's port with a chevron showing the flow (a child of the device, the same size on any device).</summary>
    private GameObject Build(IPixelSocketDevice d, PixelSocket s)
    {
        float u = PixelCouplings.HuskWidth;
        GameObject go = new GameObject("Coupling Plate (" + s.id + ")");
        go.transform.SetParent(d.SocketRoot, false);
        go.transform.localPosition = s.localPosition;
        Vector3 f = s.localFacing; f.y = 0f;
        go.transform.localRotation = Quaternion.LookRotation(f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward, Vector3.up);
        PixelCouplings.KeepSize(go.transform);
        Color c = s.flow == PixelSocketFlow.Intake ? inColor : s.flow == PixelSocketFlow.Output ? outColor : bothColor;
        PixelFactoryKit kit = new PixelFactoryKit(clicker, false);
        Transform t = go.transform;
        kit.Box("Coupling Plate Base", t, new Vector3(0f, 0.02f * u, 0.2f * u), new Vector3(0.8f * u, 0.04f * u, 0.45f * u), new Color(0.14f, 0.15f, 0.17f, 1f), default, false, 0.5f, 0.5f);
        kit.Box("Coupling Plate Rim", t, new Vector3(0f, 0.045f * u, 0.2f * u), new Vector3(0.7f * u, 0.012f * u, 0.36f * u), c * 0.6f, default, false, 0.2f, 0.6f);
        if (s.flow != PixelSocketFlow.Output) Chevron(kit, t, u, -1f, c);   // into the device
        if (s.flow != PixelSocketFlow.Intake) Chevron(kit, t, u, 1f, c);    // out of it
        return go;
    }

    private static void Chevron(PixelFactoryKit kit, Transform t, float u, float dir, Color c)
    {
        float z = 0.2f * u + dir * 0.06f * u;
        for (int sx = -1; sx <= 1; sx += 2)
            kit.Box("Coupling Plate Arrow", t, new Vector3(sx * 0.09f * u, 0.06f * u, z - dir * 0.06f * u), new Vector3(0.06f * u, 0.015f * u, 0.24f * u), c, new Vector3(0f, sx * dir * -40f, 0f), false, 0f, 0.6f, c);
    }
}
