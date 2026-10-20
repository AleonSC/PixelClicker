using Unity.Profiling;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Electric old pixels link up. Every Electric pixel lying around arcs to the nearest placed device (vacuum device, fan, magnet...)
/// or to a nearer already-linked Electric pixel, so they form chains, within <see cref="linkRange"/>. Electric pixels that
/// are not near a device still arc to each other (just for looks).
/// Pixels linked to a timed device POWER it: the device runs on batteries, so its countdown pauses while any are linked. Every linked
/// pixel carries <see cref="pixelCharge"/> charge and the group of a device holds the total; the group drains it
/// (<see cref="drainPerSecond"/>) one pixel at a time, the one FURTHEST along its daisy chain first, which fizzles out and vanishes when empty. New pixels add to the
/// total without interrupting the drain. Pixels that arc to each other (device or not) also carry charge and never despawn while linked;
/// at most <see cref="maxLinkedTotal"/> pixels are linked in total (all groups together): beyond that the oldest are despawned.
/// They stay when the device ends (they just stop being held).
/// The Electric pet is a rechargeable battery bank: it links like one more node, and while it is part of a device's group the group drains the
/// pet's own charge (<see cref="petCapacity"/>) before any pixel; it recharges slowly (<see cref="petRechargePerSecond"/>) while unused, and a tiny
/// meter floats over it. Added by PixelClicker.Awake.
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

    [Min(1f)]
    [Tooltip("Charge every Electric pixel brings when it links to a device. A group of 5 holds 5x this.")]
    [SerializeField] private float pixelCharge = 100f;

    [Min(0.1f)]
    [Tooltip("Charge the group of a device drains per second while it powers the device (taken from one pixel at a time, the oldest first). With 100 charge per pixel, 25 = 4 seconds of power per pixel.")]
    [SerializeField] private float drainPerSecond = 25f;

    [Range(0.05f, 0.9f)]
    [Tooltip("A pixel below this fraction of its charge fizzles: extra sparks and a flickering arc.")]
    [SerializeField] private float fizzleFraction = 0.3f;

    [Min(0.02f)]
    [Tooltip("How often (seconds) the links are recalculated.")]
    [SerializeField] private float scanInterval = 0.15f;

    [Min(1)]
    [Tooltip("At most this many Electric pixels can be linked in total, counting every group together (3 here + 10 there = 13). When more link up, the OLDEST ones are despawned (out with the old, in with the new).")]
    [SerializeField] private int maxLinkedTotal = 30;

    [Header("Electric pet battery")]
    [Min(1f)]
    [Tooltip("Charge the Electric pet stores. While it is linked into a device's group this is drained first (at the same rate as pixels), then the pixels' charge.")]
    [SerializeField] private float petCapacity = 600f;

    [Min(0f)]
    [Tooltip("Charge the pet regains per second while it is not powering anything. 2 = 5 minutes for a full recharge.")]
    [SerializeField] private float petRechargePerSecond = 2f;

    [Tooltip("Show the tiny charge meter over the Electric pet.")]
    [SerializeField] private bool showPetMeter = true;

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

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { PetConnected = false; }

    /// <summary>True while the Electric pet has at least one Electric pixel linked to it (the pet stands still then).</summary>
    public static bool PetConnected { get; private set; }

    private class Link
    {
        public Rigidbody body;
        public Vector3 parentPoint;       // where the arc ends (a device or another linked pixel), refreshed every frame
        public Rigidbody parentBody;      // null when the parent is a device
        public PixelPlacedDevice device;  // the device at the root of the chain
        public PixelPlacedDevice parentDevice;
        public Transform anchor;          // the Electric pet at the root of the chain (a perpetual source: no timer, its pixels never despawn)
        public Transform parentAnchor;    // the pet, when it is this pixel's direct partner
    }

    private readonly Dictionary<Rigidbody, Link> links = new Dictionary<Rigidbody, Link>();
    private const int ScanCeiling = 150;                                          // most pixels linked in one scan (a safety limit)
    private readonly Dictionary<Rigidbody, float> charge = new Dictionary<Rigidbody, float>();  // charge left in each pixel that ever linked to a device
    private readonly Dictionary<Rigidbody, int> order = new Dictionary<Rigidbody, int>();       // when it first linked (lower = older)
    private readonly HashSet<Rigidbody> fizzling = new HashSet<Rigidbody>();
    private readonly HashSet<Rigidbody> chainSeeds = new HashSet<Rigidbody>();   // the first pixel of a free chain (not a key of links)
    private int orderCounter;
    private Rigidbody petBody;          // the Electric pet's body while it is out (it links like a pixel but is never despawned)
    private float petCharge = -1f;      // -1 = not set up yet (starts full)
    private bool petDrained;            // the pet powered a device this frame (so it does not recharge)
    private GameObject meterRoot;
    private Transform meterFill;
    private Material meterBack, meterFront;
    private readonly List<Rigidbody> scratch = new List<Rigidbody>();
    private float scanTimer, redrawTimer;
    private static readonly ProfilerMarker scanMarker = new ProfilerMarker("Pixel.ElectricLinks.Scan");
    private GameObject boltObject;
    private Mesh boltMesh;
    private readonly PixelBolts.Builder builder = new PixelBolts.Builder();

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        PixelHuskPool.HuskReleasing += OnHuskReleasing;
    }

    // (PixelHuskPool: the husk is going away and may be reused - let go of it, and of the links that hang off it; the next scan links again)
    private readonly List<Rigidbody> releaseScratch = new List<Rigidbody>();
    private void OnHuskReleasing(Rigidbody body)
    {
        links.Remove(body);
        charge.Remove(body);
        order.Remove(body);
        fizzling.Remove(body);
        chainSeeds.Remove(body);
        releaseScratch.Clear();
        foreach (KeyValuePair<Rigidbody, Link> kv in links) if (kv.Value.parentBody == body) releaseScratch.Add(kv.Key);
        foreach (Rigidbody child in releaseScratch) links.Remove(child);
    }

    private void OnDestroy()
    {
        PixelHuskPool.HuskReleasing -= OnHuskReleasing;
        if (boltMesh != null) Destroy(boltMesh);
        if (boltObject != null) Destroy(boltObject);
        if (meterRoot != null) Destroy(meterRoot);
        if (meterBack != null) Destroy(meterBack);
        if (meterFront != null) Destroy(meterFront);
    }

    private void Update()
    {
        if (!enableLinks || clicker == null) return;

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            using (scanMarker.Auto()) Scan();
        }

        UpdatePet();
        DrainAndHold();

        redrawTimer -= Time.unscaledDeltaTime;
        if (redrawTimer <= 0f)
        {
            redrawTimer = redrawInterval;
            Redraw();
        }
    }

    private float PetMax => Mathf.Max(1f, petCapacity);

    /// <summary>Recharges the Electric pet when it is not powering anything and keeps its tiny meter over it.</summary>
    private void UpdatePet()
    {
        if (petCharge < 0f) petCharge = PetMax;
        if (petBody == null || !petBody.gameObject.activeInHierarchy) { if (meterRoot != null && meterRoot.activeSelf) meterRoot.SetActive(false); petBody = null; return; }
        if (!petDrained && petCharge < PetMax) petCharge = Mathf.Min(PetMax, petCharge + petRechargePerSecond * Time.deltaTime);
        petDrained = false;
        DrawPetMeter();
    }

    private void DrawPetMeter()
    {
        if (!showPetMeter || petBody == null) { if (meterRoot != null && meterRoot.activeSelf) meterRoot.SetActive(false); return; }
        Camera cam = clicker.ViewCamera;
        if (cam == null) return;
        if (meterRoot == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            if (sh == null) return;
            meterRoot = new GameObject("Electric Pet Meter");
            meterBack = new Material(sh) { color = new Color(0.05f, 0.07f, 0.1f, 0.85f), renderQueue = 3500 };
            meterFront = new Material(sh) { color = new Color(0.4f, 0.85f, 1f, 1f), renderQueue = 3510 };
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Back";
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(meterRoot.transform, false);
            back.transform.localScale = new Vector3(1.04f, 0.2f, 1f);
            back.GetComponent<Renderer>().sharedMaterial = meterBack;
            GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fill.name = "Fill";
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(meterRoot.transform, false);
            fill.GetComponent<Renderer>().sharedMaterial = meterFront;
            meterFill = fill.transform;
        }
        if (!meterRoot.activeSelf) meterRoot.SetActive(true);
        float unit = Mathf.Max(0.1f, clicker.PixelBaseSize);
        meterRoot.transform.position = petBody.position + Vector3.up * (unit * 0.95f);
        meterRoot.transform.rotation = cam.transform.rotation;   // always faces the camera
        meterRoot.transform.localScale = new Vector3(unit * 0.8f, unit * 0.8f, 1f);
        float f = Mathf.Clamp01(petCharge / PetMax);
        meterFill.localScale = new Vector3(Mathf.Max(0.001f, f), 0.12f, 1f);
        meterFill.localPosition = new Vector3(-0.5f + f * 0.5f, 0f, -0.001f);
        meterFront.color = f > 0.25f ? new Color(0.4f, 0.85f, 1f, 1f) : new Color(1f, 0.6f, 0.3f, 1f);
    }

    /// <summary>Linked pixels don't age; every powered device's group drains its oldest pixel and holds the device's countdown.</summary>
    private void DrainAndHold()
    {
        fizzling.Clear();
        // Pet-linked pixels never despawn; device-linked ones are held while the device can use power.
        foreach (KeyValuePair<Rigidbody, Link> kv in links)
        {
            if (kv.Key == null) continue;
            OldPixelDespawn d = PixelHuskQuery.Get(kv.Key).despawn;
            if (d != null) d.KeepAlive();   // linked pixels (to a device, the pet or each other) never age
        }
        foreach (Rigidbody seed in chainSeeds)
        {
            if (seed == null) continue;
            OldPixelDespawn d = PixelHuskQuery.Get(seed).despawn;
            if (d != null) d.KeepAlive();
        }

        // One focus pixel per powered device: the one FURTHEST along its daisy chain (the tail), so the chain burns away from the far end
        // and the pixel touching the machine is the last to go. Ties go to the older pixel.
        Dictionary<PixelPlacedDevice, Rigidbody> focus = new Dictionary<PixelPlacedDevice, Rigidbody>();
        Dictionary<PixelPlacedDevice, int> focusDepth = new Dictionary<PixelPlacedDevice, int>();
        foreach (KeyValuePair<Rigidbody, Link> kv in links)
        {
            PixelPlacedDevice dev = kv.Value.device;
            if (kv.Key == null || dev == null || dev.UsesMode || dev.IsEnding || !dev.Armed) continue;
            if (kv.Key == petBody) continue;   // the pet is handled separately (first while it has charge, never dissipated)
            int depth = ChainDepth(kv.Key);
            if (!focus.TryGetValue(dev, out Rigidbody cur) || depth > focusDepth[dev] || (depth == focusDepth[dev] && order[kv.Key] < order[cur]))
            {
                focus[dev] = kv.Key;
                focusDepth[dev] = depth;
            }
        }
        // The Electric pet, when it is part of a device's group (even alone with it), is drained before any pixel until its own store is empty.
        if (petBody != null && petCharge > 0f && links.TryGetValue(petBody, out Link petLink) && petLink.device != null)
        {
            PixelPlacedDevice pd = petLink.device;
            if (!pd.UsesMode && !pd.IsEnding && pd.Armed) focus[pd] = petBody;
        }
        List<Rigidbody> empty = null;
        foreach (KeyValuePair<PixelPlacedDevice, Rigidbody> kv in focus)
        {
            kv.Key.HoldPower();
            Rigidbody body = kv.Value;
            if (body == petBody)
            {
                petCharge = Mathf.Max(0f, petCharge - drainPerSecond * Time.deltaTime);
                petDrained = true;
                if (petCharge <= PetMax * fizzleFraction) fizzling.Add(body);
                continue;
            }
            float left = charge.TryGetValue(body, out float c) ? c : pixelCharge;
            left -= drainPerSecond * Time.deltaTime;
            charge[body] = left;
            if (left <= pixelCharge * fizzleFraction) fizzling.Add(body);
            if (left <= 0f) { if (empty == null) empty = new List<Rigidbody>(); empty.Add(body); }
        }
        if (empty != null)
            foreach (Rigidbody body in empty) Dissipate(body);
    }

    /// <summary>How many arcs lie between this pixel and the device at the root of its chain (1 = touching the device).</summary>
    private int ChainDepth(Rigidbody body)
    {
        int depth = 0;
        Rigidbody cur = body;
        while (cur != null && depth < 80 && links.TryGetValue(cur, out Link l))
        {
            depth++;
            cur = l.parentBody;
        }
        return depth;
    }

    /// <summary>A pixel's charge ran out (or it was pushed out by newer ones): it swells and vanishes.</summary>
    private void Dissipate(Rigidbody body)
    {
        if (body == null) return;
        OldPixelDespawn d = PixelHuskQuery.Get(body).despawn;
        if (d != null) d.Begin();
        links.Remove(body);
        charge.Remove(body);
        order.Remove(body);
        fizzling.Remove(body);
        chainSeeds.Remove(body);
    }

    private static Vector3 DeviceAnchor(PixelPlacedDevice d)
    {
        Renderer r = d.GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : d.transform.position;
    }

    /// <summary>Electric pixels and the Electrician skill's Wire and Battery pixels all carry the arcs.</summary>
    private static bool IsConductor(PixelClicker.PixelType t) => PixelClicker.IsConductorType(t);

    private bool IsElectric(Rigidbody body, out OldPixelDespawn despawn)
    {
        despawn = null;
        if (body == null) return false;
        OldPixelInfo info = PixelHuskQuery.Get(body).info;
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || !IsConductor(clicker.Tiers[info.tierIndex].type)) return false;
        despawn = PixelHuskQuery.Get(body).despawn;
        return despawn == null || !despawn.IsDespawning;
    }

    private void Scan()
    {
        PetConnected = false;
        // Devices that are working.
        List<PixelPlacedDevice> devices = new List<PixelPlacedDevice>();
        foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
            if (d != null && !d.IsEnding && d.Armed) devices.Add(d);

        // Electric old pixels lying around (not flying away, not vanishing).
        scratch.Clear();
        petBody = null;
        if (PixelPets.TryGetActivePetBody(PixelClicker.PixelType.Electric, out Transform petT)) petBody = petT.GetComponent<Rigidbody>();
        Camera cam = clicker.ViewCamera;
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

        // The Electric pet joins the network as one more node (a battery with its own charge).
        if (petBody != null)
        {
            Vector3 pv = cam != null ? cam.WorldToViewportPoint(petBody.position) : new Vector3(0.5f, 0.5f, 1f);
            if (pv.z > 0f && pv.x >= 0f && pv.x <= 1f && pv.y >= 0f && pv.y <= 1f) scratch.Add(petBody);
        }

        Dictionary<Rigidbody, Link> previous = new Dictionary<Rigidbody, Link>(links); // last scan's partners: kept while still in reach
        links.Clear();
        chainSeeds.Clear();
        if (scratch.Count == 0) { PetConnected = false; PruneCredits(); return; }

        float range = linkRange * Mathf.Max(0.1f, clicker.PixelBaseSize);
        float rangeSqr = range * range;

        // Daisy chains: from each device a chain runs pixel to pixel (every pixel links to the nearest unlinked one in reach, then that
        // one to the next...), and a new chain starts from the device while more pixels are in reach.
        List<Rigidbody> unlinked = new List<Rigidbody>(scratch);
        foreach (PixelPlacedDevice device in devices)
        {
            while (unlinked.Count > 0 && links.Count < ScanCeiling)
            {
                int index = NearestUnlinked(DeviceAnchor(device), null, device, unlinked, previous, rangeSqr, null, device is PixelChargeBooster ? petBody : null);
                if (index < 0) break;
                if (device is PixelChargeBooster booster)
                {
                    // A booster eats the pixel: it gives its charge and is dissipated (no chain through it).
                    if (!ConsumeForBooster(booster, unlinked, index)) break;
                    continue;
                }
                Rigidbody tail = Attach(unlinked, index, null, device, device);
                while (links.Count < ScanCeiling)
                {
                    index = NearestUnlinked(tail.position, tail, null, unlinked, previous, rangeSqr);
                    if (index < 0) break;
                    tail = Attach(unlinked, index, tail, null, device);
                }
            }
        }

        // Electric pixels out of reach of any device still daisy-chain to each other: a chain grows from both of its ends.
        while (unlinked.Count > 0 && links.Count < ScanCeiling)
        {
            Rigidbody seed = unlinked[0], head = seed, tail = seed;
            unlinked.RemoveAt(0);
            int length = 1;
            while (links.Count < ScanCeiling)
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
            if (length >= 2) { chainSeeds.Add(seed); Register(seed); }   // a lone pixel is not a chain
        }

        PetConnected = petBody != null && (links.ContainsKey(petBody) || chainSeeds.Contains(petBody));
        EvictOldest();
        PruneCredits();
    }

    /// <summary>The nearest unlinked pixel to 'from' within reach (a pixel keeps its old partner while that is still in reach), or -1.</summary>
    private int NearestUnlinked(Vector3 from, Rigidbody fromBody, PixelPlacedDevice fromDevice, List<Rigidbody> unlinked,
                                Dictionary<Rigidbody, Link> previous, float rangeSqr, Transform fromAnchor = null, Rigidbody exclude = null)
    {
        int best = -1;
        float bestDist = rangeSqr;
        for (int i = 0; i < unlinked.Count; i++)
        {
            if (exclude != null && unlinked[i] == exclude) continue;
            float dist = (unlinked[i].position - from).sqrMagnitude;
            if (previous.TryGetValue(unlinked[i], out Link old) && old != null &&
                ((fromBody != null && old.parentBody == fromBody) || (fromDevice != null && old.parentDevice == fromDevice) ||
                 (fromAnchor != null && old.parentAnchor == fromAnchor)))
                dist *= stickiness;
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }

    /// <summary>Links unlinked[index] to its partner (a pixel or a device) as the next link of a chain, pays the device its second, and returns the pixel.</summary>
    private Rigidbody Attach(List<Rigidbody> unlinked, int index, Rigidbody parentBody, PixelPlacedDevice parentDevice, PixelPlacedDevice rootDevice,
                             Transform parentAnchor = null, Transform rootAnchor = null)
    {
        Rigidbody body = unlinked[index];
        unlinked.RemoveAt(index);
        PixelPlacedDevice root = rootDevice;
        Vector3 point = parentBody != null ? parentBody.position : parentDevice != null ? DeviceAnchor(parentDevice)
                      : parentAnchor != null ? parentAnchor.position : body.position;
        links[body] = new Link { body = body, device = root, parentBody = parentBody, parentDevice = parentDevice, parentPoint = point,
                                 anchor = rootAnchor, parentAnchor = parentAnchor };

        if (rootAnchor == null) Register(body);   // the pet's pixels are perpetual: no charge, not counted
        return body;
    }

    /// <summary>A pixel that links up (to a device or to other Electric pixels) gets its charge and its place in the order of arrival.</summary>
    private void Register(Rigidbody body)
    {
        if (body == petBody) return;   // the pet has its own battery and is never despawned or counted
        if (!order.ContainsKey(body)) { order[body] = ++orderCounter; PixelAudio.PlayScaled("pixel_bounce_electric", 0.35f); }
        if (!charge.ContainsKey(body)) charge[body] = pixelCharge;
    }

    /// <summary>Feeds unlinked[index] to a Charge Booster as charge and dissipates it. False when the booster can take no more.</summary>
    private bool ConsumeForBooster(PixelChargeBooster booster, List<Rigidbody> unlinked, int index)
    {
        Rigidbody body = unlinked[index];
        if (!booster.AddCharge(1f)) return false;
        unlinked.RemoveAt(index);
        links.Remove(body);
        OldPixelDespawn d = PixelHuskQuery.Get(body).despawn;
        if (d != null) d.Begin();
        return true;
    }

    /// <summary>At most maxLinkedTotal pixels are linked in total (all groups together): when more are, the oldest are despawned.</summary>
    private void EvictOldest()
    {
        List<Rigidbody> members = new List<Rigidbody>();
        foreach (KeyValuePair<Rigidbody, Link> kv in links)
            if (kv.Key != null && kv.Value.anchor == null && order.ContainsKey(kv.Key)) members.Add(kv.Key);
        foreach (Rigidbody seed in chainSeeds)
            if (seed != null && order.ContainsKey(seed)) members.Add(seed);
        int extra = members.Count - maxLinkedTotal;
        if (extra <= 0) return;
        members.Sort((x, y) => order[x].CompareTo(order[y]));
        for (int i = 0; i < extra; i++) Dissipate(members[i]);
    }

    /// <summary>Forgets the charge / order of pixels that no longer exist.</summary>
    private void PruneCredits()
    {
        List<Rigidbody> dead = null;
        foreach (Rigidbody b in charge.Keys) if (b == null) { if (dead == null) dead = new List<Rigidbody>(); dead.Add(b); }
        if (dead != null) foreach (Rigidbody b in dead) { charge.Remove(b); order.Remove(b); }
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
            // NOT parented: this component sits on the cube's object, which spins, pulses and moves - the arcs are in world space.
            boltObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            boltObject.transform.localScale = Vector3.one;
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
            bool fizzle = fizzling.Contains(kv.Key);
            if (fizzle && Random.value < 0.45f) continue;   // a fizzling pixel's arc flickers
            Vector3 from = kv.Key.position;
            Vector3 to = link.parentBody != null ? link.parentBody.position
                       : link.parentDevice != null ? DeviceAnchor(link.parentDevice)
                       : link.parentAnchor != null ? link.parentAnchor.position : link.parentPoint;
            float length = Vector3.Distance(from, to);
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / (unit * 0.25f)), 3, 8);
            float pixelSize = Mathf.Max(0.05f, kv.Key.transform.lossyScale.x); // the arcs are as thick as the old pixels they join, not the big cube
            PixelBolts.Jagged(builder, from, to, segments, length * arcWander, pixelSize * 0.09f, pixelSize * 0.3f, true, coreColor, glowColor);
            if (fizzle) Spark(from, pixelSize * 0.9f, pixelSize);
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
