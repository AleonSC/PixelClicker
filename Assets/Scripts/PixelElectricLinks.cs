using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Electric old pixels link up. Every Electric pixel lying around arcs to the nearest placed device (vacuum device, fan, sorter...)
/// or to a nearer already-linked Electric pixel, so they form chains, within <see cref="linkRange"/>. Electric pixels that
/// are not near a device still arc to each other in chains of their own. Every pixel that is part of a chain gets
/// <see cref="chainLifeSeconds"/> extra seconds before it despawns (once per pixel), which helps players gather electricity.
/// Pixels linked to a device also add <see cref="secondsPerPixel"/> seconds to it (once per pixel) and don't age while linked;
/// when the device runs out (or is removed) every pixel linked to it ends too. The arcs are drawn as crackling bolts.
/// Added by PixelClicker.Awake.
/// </summary>
public class PixelElectricLinks : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker whose old pixels are scanned. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Linking")]
    [Tooltip("Master switch.")]
    [SerializeField] private bool enableLinks = true;

    [Min(0.5f)]
    [Tooltip("How far (in main-pixel widths) an Electric pixel reaches to link to a device or another linked Electric pixel.")]
    [SerializeField] private float linkRange = 3f;

    [Min(0f)]
    [Tooltip("Seconds added to a device for every Electric pixel that links to it (each pixel counts once).")]
    [SerializeField] private float secondsPerPixel = 1f;

    [Min(0f)]
    [Tooltip("Seconds added to an Electric pixel's despawn timer when it becomes part of a chain of Electric pixels (once per pixel). Chains need no device.")]
    [SerializeField] private float chainLifeSeconds = 10f;

    [Min(0.02f)]
    [Tooltip("How often (seconds) the links are recalculated.")]
    [SerializeField] private float scanInterval = 0.15f;

    [Min(1)]
    [Tooltip("At most this many Electric pixels are linked at once.")]
    [SerializeField] private int maxLinks = 30;

    [Header("Look")]
    [Tooltip("Colour of the bright centre of each arc.")]
    [SerializeField] private Color coreColor = new Color(0.92f, 1f, 1f, 1f);

    [Tooltip("Colour of the glow around each arc.")]
    [SerializeField] private Color glowColor = new Color(0.25f, 0.7f, 1f, 1f);

    [Min(0.02f)]
    [Tooltip("Seconds between redraws of the arcs (the bolts jump around each time).")]
    [SerializeField] private float redrawInterval = 0.07f;

    [Min(0f)]
    [Tooltip("How far an arc wanders off a straight line, as a fraction of its length. Small = a clear line from one pixel to the next.")]
    [SerializeField] private float arcWander = 0.05f;

    [Min(0f)]
    [Tooltip("Size of the bright spark burst where an arc attaches to a pixel or device, in main-pixel widths (0 = none).")]
    [SerializeField] private float endSparkSize = 0.45f;

    [Range(0.1f, 1f)]
    [Tooltip("How strongly a link keeps its old partner while it is still in reach: lower = stickier, so arcs stay connected to the same neighbour instead of flipping around.")]
    [SerializeField] private float stickiness = 0.4f;

    private class Link
    {
        public Rigidbody body;
        public Vector3 parentPoint;       // where the arc ends (a device or another linked pixel), refreshed every frame
        public Rigidbody parentBody;      // null when the parent is a device
        public PixelPlacedDevice device;  // the device at the root of the chain
        public PixelPlacedDevice parentDevice;
    }

    private readonly Dictionary<Rigidbody, Link> links = new Dictionary<Rigidbody, Link>();
    private readonly HashSet<Rigidbody> credited = new HashSet<Rigidbody>();      // already added their second to a device
    private readonly HashSet<Rigidbody> lifeCredited = new HashSet<Rigidbody>();  // already got the chain's extra despawn time
    private readonly HashSet<PixelPlacedDevice> watched = new HashSet<PixelPlacedDevice>();
    private readonly List<Rigidbody> scratch = new List<Rigidbody>();
    private float scanTimer, redrawTimer;
    private GameObject boltObject;
    private Mesh boltMesh;
    private readonly PixelBolts.Builder builder = new PixelBolts.Builder();

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        if (boltMesh != null) Destroy(boltMesh);
    }

    private void Update()
    {
        if (!enableLinks || clicker == null) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            Scan();
        }

        // Pixels linked to a device don't age while linked (chains without a device only get the extra time).
        foreach (KeyValuePair<Rigidbody, Link> kv in links)
        {
            if (kv.Key == null || kv.Value.device == null) continue;
            OldPixelDespawn d = kv.Key.GetComponent<OldPixelDespawn>();
            if (d != null) d.KeepAlive();
        }

        redrawTimer -= Time.unscaledDeltaTime;
        if (redrawTimer <= 0f)
        {
            redrawTimer = redrawInterval;
            Redraw();
        }
    }

    private static Vector3 DeviceAnchor(PixelPlacedDevice d)
    {
        Renderer r = d.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : d.transform.position;
    }

    private bool IsElectric(Rigidbody body, out OldPixelDespawn despawn)
    {
        despawn = null;
        if (body == null) return false;
        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].type != PixelClicker.PixelType.Electric) return false;
        despawn = body.GetComponent<OldPixelDespawn>();
        return despawn == null || !despawn.IsDespawning;
    }

    private void Scan()
    {
        // Devices that are working.
        List<PixelPlacedDevice> devices = new List<PixelPlacedDevice>();
        foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
            if (d != null && !d.IsRemoving) devices.Add(d);

        // Electric old pixels lying around (not flying away, not vanishing).
        scratch.Clear();
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        foreach (Rigidbody body in clicker.OldPixels)
        {
            if (!IsElectric(body, out _) || clicker.IsFlyingPixel(body)) continue;
            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(body.position); // only pixels in view link up, so no arcs run off the screen
                if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) continue;
            }
            scratch.Add(body);
        }

        Dictionary<Rigidbody, Link> previous = new Dictionary<Rigidbody, Link>(links); // last scan's partners: kept while still in reach
        links.Clear();
        if (scratch.Count == 0) { PruneCredits(); return; }

        foreach (PixelPlacedDevice d in devices)
            if (watched.Add(d)) d.Ended += OnDeviceEnded;

        float range = linkRange * Mathf.Max(0.1f, clicker.PixelBaseSize);
        float rangeSqr = range * range;

        // Daisy chains: from each device a chain runs pixel to pixel (every pixel links to the nearest unlinked one in reach, then that
        // one to the next...), and a new chain starts from the device while more pixels are in reach.
        List<Rigidbody> unlinked = new List<Rigidbody>(scratch);
        foreach (PixelPlacedDevice device in devices)
        {
            while (unlinked.Count > 0 && links.Count < maxLinks)
            {
                int index = NearestUnlinked(DeviceAnchor(device), null, device, unlinked, previous, rangeSqr);
                if (index < 0) break;
                Rigidbody tail = Attach(unlinked, index, null, device, device);
                while (links.Count < maxLinks)
                {
                    index = NearestUnlinked(tail.position, tail, null, unlinked, previous, rangeSqr);
                    if (index < 0) break;
                    tail = Attach(unlinked, index, tail, null, device);
                }
            }
        }

        // Electric pixels out of reach of any device still daisy-chain to each other: a chain grows from both of its ends.
        List<Rigidbody> chainSeeds = new List<Rigidbody>();
        while (unlinked.Count > 0 && links.Count < maxLinks)
        {
            Rigidbody seed = unlinked[0], head = seed, tail = seed;
            unlinked.RemoveAt(0);
            int length = 1;
            while (links.Count < maxLinks)
            {
                int tailIndex = NearestUnlinked(tail.position, tail, null, unlinked, previous, rangeSqr);
                int headIndex = head == tail ? -1 : NearestUnlinked(head.position, head, null, unlinked, previous, rangeSqr);
                if (tailIndex < 0 && headIndex < 0) break;
                bool useHead = headIndex >= 0 && (tailIndex < 0 ||
                    (unlinked[headIndex].position - head.position).sqrMagnitude < (unlinked[tailIndex].position - tail.position).sqrMagnitude);
                if (useHead) head = Attach(unlinked, headIndex, head, null, null);
                else tail = Attach(unlinked, tailIndex, tail, null, null);
                length++;
            }
            if (length >= 2) chainSeeds.Add(seed); // a lone pixel is not a chain
        }

        // Everything that is part of a chain (linked to a device or to another Electric pixel) gets extra despawn time, once.
        foreach (KeyValuePair<Rigidbody, Link> kv in links) GiveChainLife(kv.Key);
        foreach (Rigidbody seed in chainSeeds) GiveChainLife(seed);
        PruneCredits();
    }

    /// <summary>The nearest unlinked pixel to 'from' within reach (a pixel keeps its old partner while that is still in reach), or -1.</summary>
    private int NearestUnlinked(Vector3 from, Rigidbody fromBody, PixelPlacedDevice fromDevice, List<Rigidbody> unlinked,
                                Dictionary<Rigidbody, Link> previous, float rangeSqr)
    {
        int best = -1;
        float bestDist = rangeSqr;
        for (int i = 0; i < unlinked.Count; i++)
        {
            float dist = (unlinked[i].position - from).sqrMagnitude;
            if (previous.TryGetValue(unlinked[i], out Link old) && old != null &&
                ((fromBody != null && old.parentBody == fromBody) || (fromDevice != null && old.parentDevice == fromDevice)))
                dist *= stickiness;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }

    /// <summary>Links unlinked[index] to its partner (a pixel or a device) as the next link of a chain, pays the device its second, and returns the pixel.</summary>
    private Rigidbody Attach(List<Rigidbody> unlinked, int index, Rigidbody parentBody, PixelPlacedDevice parentDevice, PixelPlacedDevice rootDevice)
    {
        Rigidbody body = unlinked[index];
        unlinked.RemoveAt(index);
        PixelPlacedDevice root = rootDevice;
        Vector3 point = parentBody != null ? parentBody.position : parentDevice != null ? DeviceAnchor(parentDevice) : body.position;
        links[body] = new Link { body = body, device = root, parentBody = parentBody, parentDevice = parentDevice, parentPoint = point };

        if (root != null && credited.Add(body) && secondsPerPixel > 0f)
        {
            root.AddSeconds(secondsPerPixel); // +1 s for every electric pixel that links up
            PixelAudio.PlayScaled("pixel_bounce_electric", 0.35f);
        }
        return body;
    }

    private void GiveChainLife(Rigidbody body)
    {
        if (body == null || chainLifeSeconds <= 0f || !lifeCredited.Add(body)) return;
        OldPixelDespawn d = body.GetComponent<OldPixelDespawn>();
        if (d != null) d.AddLifetime(chainLifeSeconds);
    }

    /// <summary>Forgets credits of pixels that no longer exist.</summary>
    private void PruneCredits()
    {
        credited.RemoveWhere(b => b == null);
        lifeCredited.RemoveWhere(b => b == null);
        watched.RemoveWhere(d => d == null);
    }

    /// <summary>A device ran out: every Electric pixel linked to it ends too.</summary>
    private void OnDeviceEnded(PixelPlacedDevice device)
    {
        watched.Remove(device);
        List<Rigidbody> ending = new List<Rigidbody>();
        foreach (KeyValuePair<Rigidbody, Link> kv in links)
            if (kv.Value.device == device && kv.Key != null) ending.Add(kv.Key);
        foreach (Rigidbody body in ending)
        {
            OldPixelDespawn d = body.GetComponent<OldPixelDespawn>();
            if (d != null) d.Begin();
            links.Remove(body);
        }
        Redraw();
    }

    /// <summary>A little burst of short bright lines where an arc attaches, so the connection reads clearly.</summary>
    private void Spark(Vector3 at, float size, float unit)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 end = at + Random.onUnitSphere * (size * Random.Range(0.5f, 1f));
            PixelBolts.Ribbon(builder, at, end, unit * 0.05f, new Color(coreColor.r, coreColor.g, coreColor.b, Random.Range(0.6f, 1f)));
        }
        PixelBolts.Ribbon(builder, at - Vector3.up * (size * 0.18f), at + Vector3.up * (size * 0.18f), unit * 0.22f, new Color(glowColor.r, glowColor.g, glowColor.b, 0.5f));
    }

    private void Redraw()
    {
        if (links.Count == 0)
        {
            if (boltObject != null && boltObject.activeSelf) boltObject.SetActive(false);
            return;
        }
        if (boltObject == null)
        {
            boltObject = new GameObject("Electric Links", typeof(MeshFilter), typeof(MeshRenderer));
            boltObject.transform.SetParent(transform, false);
            boltMesh = new Mesh { name = "Electric Links" };
            boltMesh.MarkDynamic();
            boltObject.GetComponent<MeshFilter>().sharedMesh = boltMesh;
            MeshRenderer mr = boltObject.GetComponent<MeshRenderer>();
            mr.sharedMaterial = PixelLooks.OverlayMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        if (!boltObject.activeSelf) boltObject.SetActive(true);

        float unit = Mathf.Max(0.1f, clicker.PixelBaseSize);
        builder.Clear();
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        bool first = true;
        foreach (KeyValuePair<Rigidbody, Link> kv in links)
        {
            Link link = kv.Value;
            if (kv.Key == null) continue;
            Vector3 from = kv.Key.position;
            Vector3 to = link.parentBody != null ? link.parentBody.position
                       : link.parentDevice != null ? DeviceAnchor(link.parentDevice) : link.parentPoint;
            float length = Vector3.Distance(from, to);
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / (unit * 0.25f)), 3, 8);
            float pixelSize = Mathf.Max(0.05f, kv.Key.transform.lossyScale.x); // the arcs are as thick as the old pixels they join, not the big cube
            PixelBolts.Jagged(builder, from, to, segments, length * arcWander, pixelSize * 0.09f, pixelSize * 0.3f, true, coreColor, glowColor);
            if (endSparkSize > 0f)
            {
                Spark(from, pixelSize * endSparkSize * 1.6f, pixelSize);
                Spark(to, pixelSize * endSparkSize * 1.6f, pixelSize);
            }
            if (first) { bounds = new Bounds(from, Vector3.zero); first = false; }
            bounds.Encapsulate(from);
            bounds.Encapsulate(to);
        }
        bounds.Expand(unit * 2f);
        builder.Apply(boltMesh, bounds);
    }
}
