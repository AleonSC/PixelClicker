using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Electric old pixels link up. Every Electric pixel lying around arcs to the nearest placed device (vacuum device, fan, sorter...)
/// or to a nearer already-linked Electric pixel, so they form chains, within <see cref="linkRangePixels"/>. Electric pixels that
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
    [SerializeField] private float linkRangePixels = 7f;

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
        foreach (Rigidbody body in clicker.OldPixels)
            if (IsElectric(body, out _) && !clicker.IsFlyingPixel(body)) scratch.Add(body);

        Dictionary<Rigidbody, Link> previous = new Dictionary<Rigidbody, Link>(links); // last scan's partners: kept while still in reach
        links.Clear();
        if (scratch.Count == 0) { PruneCredits(); return; }

        foreach (PixelPlacedDevice d in devices)
            if (watched.Add(d)) d.Ended += OnDeviceEnded;

        float range = linkRangePixels * Mathf.Max(0.1f, clicker.PixelBaseSize);
        float rangeSqr = range * range;

        // Grow the network outwards from the devices: each step links the unlinked pixel that is closest to the network.
        List<Rigidbody> unlinked = new List<Rigidbody>(scratch);
        while (devices.Count > 0 && unlinked.Count > 0 && links.Count < maxLinks)
        {
            int bestIndex = -1;
            float best = rangeSqr;
            Link bestLink = null;
            for (int i = 0; i < unlinked.Count; i++)
            {
                Vector3 p = unlinked[i].position;
                previous.TryGetValue(unlinked[i], out Link old);
                foreach (PixelPlacedDevice d in devices)
                {
                    float dist = (DeviceAnchor(d) - p).sqrMagnitude;
                    if (old != null && old.parentDevice == d) dist *= stickiness;
                    if (dist < best)
                    {
                        best = dist; bestIndex = i;
                        bestLink = new Link { body = unlinked[i], device = d, parentDevice = d, parentPoint = DeviceAnchor(d) };
                    }
                }
                foreach (KeyValuePair<Rigidbody, Link> kv in links)
                {
                    float dist = (kv.Key.position - p).sqrMagnitude;
                    if (old != null && old.parentBody == kv.Key) dist *= stickiness;
                    if (dist < best)
                    {
                        best = dist; bestIndex = i;
                        bestLink = new Link { body = unlinked[i], device = kv.Value.device, parentBody = kv.Key, parentPoint = kv.Key.position };
                    }
                }
            }
            if (bestIndex < 0) break;
            links[bestLink.body] = bestLink;
            unlinked.RemoveAt(bestIndex);

            if (credited.Add(bestLink.body) && secondsPerPixel > 0f && bestLink.device != null)
            {
                bestLink.device.AddSeconds(secondsPerPixel); // +1 s for every electric pixel that links up
                PixelAudio.PlayScaled("pixel_bounce_electric", 0.35f);
            }
        }

        // Electric pixels out of reach of any device still arc to each other in chains of their own.
        List<Rigidbody> chainSeeds = new List<Rigidbody>();
        while (unlinked.Count > 0 && links.Count < maxLinks)
        {
            Rigidbody seed = unlinked[0];
            unlinked.RemoveAt(0);
            List<Rigidbody> chain = new List<Rigidbody> { seed };
            while (links.Count < maxLinks)
            {
                int bestIndex = -1, bestParent = -1;
                float best = rangeSqr;
                for (int i = 0; i < unlinked.Count; i++)
                {
                    previous.TryGetValue(unlinked[i], out Link old);
                    for (int c = 0; c < chain.Count; c++)
                    {
                        float dist = (chain[c].position - unlinked[i].position).sqrMagnitude;
                        if (old != null && old.parentBody == chain[c]) dist *= stickiness;
                        if (dist < best) { best = dist; bestIndex = i; bestParent = c; }
                    }
                }
                if (bestIndex < 0) break;
                Rigidbody joiner = unlinked[bestIndex];
                links[joiner] = new Link { body = joiner, parentBody = chain[bestParent], parentPoint = chain[bestParent].position };
                chain.Add(joiner);
                unlinked.RemoveAt(bestIndex);
            }
            if (chain.Count >= 2) chainSeeds.Add(seed); // a lone pixel is not a chain
        }

        // Everything that is part of a chain (linked to a device or to another Electric pixel) gets extra despawn time, once.
        foreach (KeyValuePair<Rigidbody, Link> kv in links) GiveChainLife(kv.Key);
        foreach (Rigidbody seed in chainSeeds) GiveChainLife(seed);
        PruneCredits();
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
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / (unit * 0.6f)), 3, 9);
            PixelBolts.Jagged(builder, from, to, segments, length * arcWander, unit * 0.06f, unit * 0.2f, true, coreColor, glowColor);
            if (endSparkSize > 0f)
            {
                Spark(from, unit * endSparkSize, unit);
                Spark(to, unit * endSparkSize, unit);
            }
            if (first) { bounds = new Bounds(from, Vector3.zero); first = false; }
            bounds.Encapsulate(from);
            bounds.Encapsulate(to);
        }
        bounds.Expand(unit * 2f);
        builder.Apply(boltMesh, bounds);
    }
}
