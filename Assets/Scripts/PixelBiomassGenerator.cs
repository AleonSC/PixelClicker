// ---- System notes ----
// PixelBiomassGenerator.cs
//   BIOMASS GENERATORS (2026-10-19, Devices session): two placed devices built by this one component (the permanent PixelChipperDevice stub sits next to it):
//   - POCKET BIOMASS GENERATOR (DeviceKind.PocketBiomassGenerator 27): a small one from the shop's Consumables tab;
//   - BIOMASS GENERATOR (DeviceKind.BiomassGenerator 28): the big one, on sale at Woodcutting level 10 (shopSkillId 'woodcutting').
//   They differ only in their Device entry: radius (how far the power reaches), generatorMaxCharge (seconds of power when full) and generatorChargePerHusk
//   (seconds one wood husk adds), and the model's size (bodyDiameter).
//   - FUEL: husks of WOODCUTTING (PixelSkills.HuskSkillId == woodcutting) that land in the open firebox on top (dropped by hand or by physics) - or come
//     in through its intake coupling socket - are burnt: taken off the floor (FlyIn), + generatorChargePerHusk, up to the max. Other husks are left alone.
//     The firebox is never narrower than 1.8 husk widths (the pocket model is small) and a husk resting on its rim counts. A SMALL SUCTION (suckRadiusHusks,
//     suckStrength) draws loose Woodcutting husks nearby up and in towards it while it has room for fuel (FixedUpdate).
//   - POWER: every placed device within 'radius' (generators and Power Banks excepted: IsPowerSource) is powered (PixelPlacedDevice.HoldPower(fraction, colour)): its countdown stops
//     and the generator's BLUE ENERGY BAR (PixelEnergyBar) shows over it, tracking the generator's charge. A faint blue ring on the floor shows the radius.
//   - CHARGE: it comes FULL and drains 1 a second (drainPerSecond). Empty -> a FLASHING RED state for redSeconds (no power, the bar and the glow flash
//     red), then it shuts down and disappears (RemoveNow). Feeding a wood husk in the red state cancels it and adds the charge.
//   POWER BANK (2026-10-20): a PixelPowerBank in range draws its SPARE charge (GiveCharge: only above a kept fraction), with a blue arc from TopPoint.
//   Saved: PlacedState.extra = the charge left. Device card: charge, radius, devices powered.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class PixelBiomassGenerator : MonoBehaviour, IPixelSocketDevice, IPixelHuskRoom, IPixelDeviceInfo
{
    [Header("Biomass Generator")]
    [Min(0f)]
    [Tooltip("Charge used per second (1 = the charge is in seconds of power).")]
    [SerializeField] private float drainPerSecond = 1f;
    [Min(0.5f)]
    [Tooltip("Seconds it flashes red when empty before it shuts down and disappears (a wood husk fed meanwhile saves it).")]
    [SerializeField] private float redSeconds = 8f;
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of what is in its radius / in its firebox.")]
    [SerializeField] private float checkSeconds = 0.1f;
    [Header("Suction (wood husks only)")]
    [Min(0f)]
    [Tooltip("How far from the firebox (husk widths, sideways) loose Woodcutting husks are gently drawn in. 0 = off.")]
    [SerializeField] private float suckRadiusHusks = 3f;
    [Min(0f)]
    [Tooltip("How strong that pull is (husk widths per second squared). Small on purpose.")]
    [SerializeField] private float suckStrength = 7f;

    [Tooltip("Colour of the energy bar (and the radius ring).")]
    [SerializeField] private Color energyColor = new Color(0.35f, 0.7f, 1f, 1f);
    [Tooltip("Colour the bar and the glow flash when it is empty.")]
    [SerializeField] private Color emptyColor = new Color(1f, 0.2f, 0.15f, 1f);

    private static readonly List<PixelBiomassGenerator> all = new List<PixelBiomassGenerator>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    /// <summary>Every generator standing in the world (the Power Bank draws from those in its radius).</summary>
    public static IReadOnlyList<PixelBiomassGenerator> All => all;

    /// <summary>Is this placed device a generator (generators are never powered by each other)?</summary>
    public static bool IsGenerator(PixelPlacedDevice d) => d != null && d.GetComponent<PixelBiomassGenerator>() != null;

    /// <summary>Does this placed device GIVE power (a generator or a Power Bank)? Power sources are never powered themselves.</summary>
    public static bool IsPowerSource(PixelPlacedDevice d) => IsGenerator(d) || PixelPowerBank.IsBank(d);

    /// <summary>Has fuel and is not shutting down (a Power Bank only draws from running generators).</summary>
    public bool Running => clicker != null && charge > 0f && (device == null || !device.IsEnding);

    /// <summary>The top of the chimney (world): where a Power Bank's charging arc starts.</summary>
    public Vector3 TopPoint => transform.TransformPoint(new Vector3(0.55f * Unit(clicker) * size, 3.45f * Unit(clicker) * size, 0.75f * Unit(clicker) * size));

    /// <summary>
    /// A Power Bank asks for up to 'want' charge: it gets only what this generator holds ABOVE 'keepFraction' of its max (so it keeps running).
    /// Returns what was given (taken off this generator's charge).
    /// </summary>
    public float GiveCharge(float want, float keepFraction)
    {
        float spare = charge - Mathf.Clamp01(keepFraction) * maxCharge;
        float give = Mathf.Max(0f, Mathf.Min(want, spare));
        charge -= give;
        return give;
    }

    private PixelClicker clicker;
    private PixelPlacedDevice device;
    private float radius = 4f, maxCharge = 60f, perHusk = 12f, charge, redLeft = -1f, nextCheck, size = 1f;
    private int powering;
    private PixelEnergyBar bar;
    private LineRenderer radiusRing;
    private readonly List<Renderer> glowRenderers = new List<Renderer>();
    private PixelSocket[] sockets = new PixelSocket[0];

    // ---- Building (in husk widths u, times 'size': the Pocket one is smaller) ----

    private static readonly Color TankColor = new Color(0.33f, 0.45f, 0.24f, 1f), Metal = new Color(0.62f, 0.64f, 0.66f, 1f),
                                  Dark = new Color(0.12f, 0.12f, 0.12f, 1f), Wood = new Color(0.5f, 0.33f, 0.18f, 1f), Glow = new Color(0.45f, 1f, 0.5f, 1f);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    /// <summary>Builds a placed generator at 'point'. 'size' scales the model (Pocket ~0.7, big 1.2); the numbers come from Init.</summary>
    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw, float size)
    {
        GameObject root = Build(clicker, false, true, size);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return root;
    }

    public static GameObject CreateGhost(PixelClicker clicker, float size) => Build(clicker, true, false, size);
    public static GameObject CreateDisplay(PixelClicker clicker, float size) => Build(clicker, false, false, size);

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders, float size)
    {
        float u = Unit(clicker) * Mathf.Max(0.3f, size);
        PixelFactoryKit kit = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Biomass Generator (Preview)" : "Biomass Generator");
        Transform tr = root.transform;
        // Base plate and the round tank.
        kit.Box("Base", tr, new Vector3(0f, 0.15f * u, 0f), new Vector3(2.6f * u, 0.3f * u, 2.6f * u), Dark, default, colliders, 0.5f, 0.5f);
        kit.Cyl("Tank", tr, new Vector3(0f, 1.3f * u, 0f), 1f * u, 2f * u, TankColor, default, 0.4f, 0.5f, colliders);
        // A glowing band (green while running, flashing red when empty) and two blue coils on the sides.
        kit.Cyl("Glow Band", tr, new Vector3(0f, 1.15f * u, 0f), 1.03f * u, 0.35f * u, Glow, default, 0f, 0.8f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            kit.Cyl("Coil", tr, new Vector3(sx * 1.15f * u, 1.3f * u, 0f), 0.22f * u, 1.4f * u, new Color(0.3f, 0.6f, 1f, 1f), default, 0.6f, 0.6f);
            kit.Ball("Coil Cap", tr, new Vector3(sx * 1.15f * u, 2.05f * u, 0f), 0.24f * u, Metal, 0.8f, 0.6f);
        }
        // The open FIREBOX on top: a square box with a dark floor (husks rest on it and burn) and wooden slats on its rim.
        float fy = 2.3f * u, fw = FireboxWidth(clicker, u), fh = 0.7f * u, t = 0.12f * u;   // (never narrower than a husk fits through: the pocket model is small)
        kit.Box("Firebox Floor", tr, new Vector3(0f, fy, 0f), new Vector3(fw, t, fw), Dark, default, colliders, 0.1f, 0.2f);
        kit.Box("Firebox Front", tr, new Vector3(0f, fy + fh * 0.5f, -fw * 0.5f), new Vector3(fw + t, fh, t), Metal, default, colliders, 0.7f, 0.6f);
        kit.Box("Firebox Back", tr, new Vector3(0f, fy + fh * 0.5f, fw * 0.5f), new Vector3(fw + t, fh, t), Metal, default, colliders, 0.7f, 0.6f);
        kit.Box("Firebox Left", tr, new Vector3(-fw * 0.5f, fy + fh * 0.5f, 0f), new Vector3(t, fh, fw), Metal, default, colliders, 0.7f, 0.6f);
        kit.Box("Firebox Right", tr, new Vector3(fw * 0.5f, fy + fh * 0.5f, 0f), new Vector3(t, fh, fw), Metal, default, colliders, 0.7f, 0.6f);
        kit.Box("Firebox Embers", tr, new Vector3(0f, fy + t * 0.6f, 0f), new Vector3(fw * 0.8f, t * 0.2f, fw * 0.8f), new Color(1f, 0.45f, 0.1f, 1f), default, false, 0f, 0.6f, new Color(1f, 0.4f, 0.1f, 1f));
        for (int i = -1; i <= 1; i++)
            kit.Box("Slat", tr, new Vector3(i * fw * 0.3f, fy + fh + t * 0.4f, -fw * 0.5f), new Vector3(fw * 0.22f, t * 0.6f, t * 1.4f), Wood, default, false, 0.1f, 0.4f);
        // A short chimney at the back.
        kit.Cyl("Chimney", tr, new Vector3(0.55f * u, 2.9f * u, 0.75f * u), 0.16f * u, 1.1f * u, Dark, default, 0.5f, 0.5f);
        return root;
    }

    /// <summary>The firebox's inner width: 1.5 model units, but at least 1.8 husk widths so a husk always drops in (the pocket model is small).</summary>
    private static float FireboxWidth(PixelClicker clicker, float u) => Mathf.Max(1.5f * u, 1.8f * Unit(clicker));

    /// <summary>Sets the numbers from its Device entry (placing and loading).</summary>
    public void Init(PixelClicker owner, float powerRadius, float maxChargeSeconds, float chargePerHusk, float modelSize)
    {
        clicker = owner;
        radius = Mathf.Max(0.5f, powerRadius);
        maxCharge = Mathf.Max(1f, maxChargeSeconds);
        perHusk = Mathf.Max(0.1f, chargePerHusk);
        size = Mathf.Max(0.3f, modelSize);
        charge = maxCharge;   // (it comes full)
        float u = Unit(clicker) * size;
        sockets = new[] { new PixelSocket("intake", new Vector3(0f, 0f, -1.65f * u), Vector3.back, PixelSocketFlow.Intake) };
        foreach (string n in new[] { "Glow Band" })
        {
            Transform g = transform.Find(n);
            if (g != null && g.GetComponent<Renderer>() != null) glowRenderers.Add(g.GetComponent<Renderer>());
        }
        bar = PixelEnergyBar.Attach(transform, new Vector3(0f, 3.8f * u, 0f), 1.6f * u, energyColor);
        BuildRadiusRing();
    }

    private void BuildRadiusRing()
    {
        GameObject go = new GameObject("Power Radius");
        go.transform.SetParent(transform, false);
        radiusRing = go.AddComponent<LineRenderer>();
        Shader s = Shader.Find("Sprites/Default");
        radiusRing.material = new Material(s != null ? s : Shader.Find("Unlit/Color"));
        radiusRing.useWorldSpace = false;
        radiusRing.loop = true;
        radiusRing.widthMultiplier = Unit(clicker) * 0.12f;
        radiusRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        radiusRing.receiveShadows = false;
        const int n = 64;
        radiusRing.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            radiusRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius));
        }
        SetRingColor(energyColor);
    }

    private void SetRingColor(Color c)
    {
        if (radiusRing == null) return;
        Color faint = new Color(c.r, c.g, c.b, 0.35f);
        radiusRing.startColor = radiusRing.endColor = faint;
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); PixelCouplings.Register(this); }
    private void OnDisable() { all.Remove(this); PixelCouplings.Unregister(this); }

    // ---- The intake socket (a belt / hose can feed it wood husks) ----
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => TankColor;
    public int HuskRoom(PixelSocket socket) => charge < maxCharge ? Mathf.Max(1, Mathf.CeilToInt((maxCharge - charge) / perHusk)) : 0;

    public bool AcceptHusk(PixelSocket socket, Rigidbody husk)
    {
        if (husk == null || !IsWood(husk) || charge >= maxCharge) return false;
        Burn(husk);
        return true;
    }

    private bool IsWood(Rigidbody body)
    {
        int tier = PixelHuskQuery.Get(body).Tier;
        PixelSkills skills = PixelSkills.Instance;
        return clicker != null && clicker.IsValidTierIndex(tier) && skills != null && skills.HuskSkillId(tier) == PixelSkills.WoodcuttingId;
    }

    /// <summary>A wood husk goes in the fire: taken off the floor (it shrinks into the embers), the charge rises, an empty generator is saved.</summary>
    private void Burn(Rigidbody body)
    {
        if (clicker == null || !clicker.ReleaseOldPixel(body, false)) return;
        float u = Unit(clicker) * size;
        StartCoroutine(PixelHuskHolder.FlyIn(body.gameObject, null, transform.TransformPoint(new Vector3(0f, 2.35f * u, 0f)), false, 0.25f));
        charge = Mathf.Min(maxCharge, Mathf.Max(0f, charge) + perHusk);
        redLeft = -1f;   // (fed in the red state: saved)
        PixelAudio.Play("fire_crackle");
        PixelStats.Count("generator.husks");
    }

    private static readonly List<PixelHuskQuery.Husk> nearScratch = new List<PixelHuskQuery.Husk>();

    /// <summary>A small suction: loose Woodcutting husks near the generator are drawn up and in towards the firebox (only while it has room for fuel).</summary>
    private void FixedUpdate()
    {
        if (clicker == null || suckRadiusHusks <= 0f || suckStrength <= 0f || charge >= maxCharge || PixelCalm.InMenu || (device != null && device.IsEnding)) return;
        float u = Unit(clicker) * size, hw = Unit(clicker), reach = suckRadiusHusks * hw + FireboxWidth(clicker, u) * 0.5f;
        Vector3 mouth = transform.TransformPoint(new Vector3(0f, 3.2f * u + 0.6f * hw, 0f));   // (just above the firebox)
        PixelHuskQuery.Near(clicker, transform.position, reach, nearScratch);
        float dt = Time.fixedDeltaTime;
        foreach (PixelHuskQuery.Husk q in nearScratch)
        {
            Rigidbody b = q.body;
            if (b == null || !IsWood(b) || !PixelHuskHolder.Takeable(clicker, b)) continue;
            Vector3 to = mouth - b.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float d = flat.magnitude;
            if (d > reach) continue;
            float k = 1f - d / Mathf.Max(0.01f, reach);
            Vector3 dv = (flat.normalized * (0.4f + 0.6f * k) + Vector3.up * (to.y > 0f ? 0.9f * k : -0.3f)) * suckStrength * hw * dt;
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
        if (device != null && device.IsEnding) { if (bar != null) bar.Set(-1f, energyColor); return; }
        bool calm = !PixelCalm.InMenu && Time.timeScale > 0f;
        bool empty = charge <= 0f;
        if (calm)
        {
            if (!empty) charge = Mathf.Max(0f, charge - drainPerSecond * Time.deltaTime);
            else
            {
                if (redLeft < 0f) { redLeft = redSeconds; PixelAudio.Play("ui_click"); }
                redLeft -= Time.deltaTime;
                if (redLeft <= 0f && device != null) { device.RemoveNow(); PixelHints.Announce("A Biomass Generator ran out of fuel and shut down"); return; }
            }
        }
        empty = charge <= 0f;
        float fraction = charge / maxCharge;
        Color c = empty ? Color.Lerp(emptyColor, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f)) : energyColor;
        if (bar != null) bar.Set(empty ? 1f : fraction, c);
        SetGlow(empty ? (Mathf.Sin(Time.time * 14f) > 0f ? emptyColor : Color.black) : Glow);
        SetRingColor(empty ? emptyColor : energyColor);

        if (!calm || Time.time < nextCheck) return;
        nextCheck = Time.time + checkSeconds;
        CatchFuel();
        if (!empty) PowerAround(fraction);
        else powering = 0;
    }

    private Color glowShown = new Color(-1f, 0f, 0f, 0f);

    private void SetGlow(Color c)
    {
        if (c == glowShown) return;
        glowShown = c;
        foreach (Renderer r in glowRenderers)
        {
            if (r == null) continue;
            Material m = r.material;
            m.color = c == Color.black ? new Color(0.25f, 0.05f, 0.05f, 1f) : c;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.2f); }
        }
    }

    /// <summary>Wood husks lying in the firebox (dropped in by hand or falling in) are burnt.</summary>
    private void CatchFuel()
    {
        if (charge >= maxCharge) return;
        float u = Unit(clicker) * size, hw = Unit(clicker), half = FireboxWidth(clicker, u) * 0.5f + 0.3f * hw;
        float bottom = 2.1f * u, top = 2.3f * u + 0.7f * u + 1.6f * hw;   // (from the firebox floor to a husk's height over its rim: one resting on the rim counts too)
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0 && charge < maxCharge; i--)
        {
            if (i >= pixels.Count) continue;
            Rigidbody b = pixels[i];
            if (b == null) continue;
            Vector3 p = transform.InverseTransformPoint(b.position) * transform.lossyScale.x;   // (in world-size units: the root is not scaled)
            if (Mathf.Abs(p.x) > half || Mathf.Abs(p.z) > half || p.y < bottom || p.y > top) continue;
            if (!IsWood(b) || !PixelHuskHolder.Takeable(clicker, b)) continue;
            Burn(b);
        }
    }

    /// <summary>Every placed device within the radius (not other generators) runs on this generator's power: its countdown stops and it shows this bar.</summary>
    private void PowerAround(float fraction)
    {
        powering = 0;
        Vector3 me = transform.position;
        foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
        {
            if (d == null || d == device || d.IsEnding || IsPowerSource(d)) continue;
            Vector3 dv = d.transform.position - me; dv.y = 0f;
            if (dv.sqrMagnitude > radius * radius) continue;
            d.HoldPower(fraction, energyColor);
            powering++;
        }
    }

    // ---- The device card / panel ----

    public void FillDeviceInfo(PixelDeviceInfo info)
    {
        info.Add("Charge", Mathf.CeilToInt(charge) + " / " + Mathf.CeilToInt(maxCharge) + " s");
        info.Progress = charge / maxCharge;
        info.ProgressLabel = charge <= 0f ? "EMPTY - feed it wood husks!" : "";
        info.Add("Powering", powering + " device" + (powering == 1 ? "" : "s"));
        if (charge <= 0f) info.Note("<color=#FF6B5B>Out of fuel: it shuts down in " + Mathf.CeilToInt(Mathf.Max(0f, redLeft)) + " s unless it gets a wood husk.</color>");
        if (!info.Full) return;
        info.Add("Reach", radius.ToString("0.#") + " units");
        info.Add("Per wood husk", "+" + perHusk.ToString("0.#") + " s");
        info.Note("<color=#9FB0CC>Drop Woodcutting husks into the firebox on top (or feed them in through its intake coupling). Every device in the blue ring runs on its power: its timer stops.</color>");
    }

    // ---- Saving (PixelConsumables puts this in PlacedState.extra) ----

    public string ExportState() => charge.ToString("R", CultureInfo.InvariantCulture);

    public void ImportState(string data)
    {
        if (!string.IsNullOrEmpty(data) && float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float c)) charge = Mathf.Clamp(c, 0f, maxCharge);
    }
}
