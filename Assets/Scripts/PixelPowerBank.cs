// ---- System notes ----
// PixelPowerBank.cs
//   POWER BANK (2026-10-20, Devices session; DeviceKind.PowerBank 34, a permanent placed device with the PixelPermanentDevice stub, like the generators):
//   a big battery that STORES power and hands it out over a LARGE radius.
//   - CHARGE IN: every Biomass Generator standing inside its radius feeds it (PixelBiomassGenerator.GiveCharge): up to chargeInPerSecond per generator,
//     but only the generator's charge above keepInGenerator (a fraction of its max) - so a fed generator keeps running and never drains itself to death
//     for the bank. A blue arc (a LineRenderer per feeding generator) runs from each feeding generator to the bank's top terminal while it flows.
//   - POWER OUT: while it holds charge, every TIMED, switched-on placed device within its radius (not permanent ones, not generators / banks) is powered
//     (PixelPlacedDevice.HoldPower(fraction, colour)): its countdown stops and the bank's ENERGY BAR shows over it. The charge drains drainPerDevice a second
//     per device it powers (an idle bank keeps its charge). Empty = it just waits (it never shuts down or disappears).
//   - Comes EMPTY. Radius / capacity come from its Device entry (radius, generatorMaxCharge). A faint ring on the floor shows the radius.
//   Model: a plinth, three tall glass cells whose glowing cores fill with the charge, a steel top with terminals and a lit band. Built with PixelFactoryKit.
//   Saved: PlacedState.extra = the charge. Device card: charge, devices powered, generators feeding.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class PixelPowerBank : MonoBehaviour, IPixelDeviceInfo
{
    [Header("Power Bank")]
    [Min(0f)]
    [Tooltip("Charge used per second for EACH device it powers (1 = one second of power per device per second). An idle bank keeps its charge.")]
    [SerializeField] private float drainPerDevice = 0.25f;
    [Min(0f)]
    [Tooltip("Most charge (seconds) one generator in range can push into the bank per second.")]
    [SerializeField] private float chargeInPerSecond = 6f;
    [Range(0f, 1f)]
    [Tooltip("A generator only gives the charge it holds ABOVE this fraction of its maximum (so it keeps running and never empties itself into the bank).")]
    [SerializeField] private float keepInGenerator = 0.35f;
    [Min(0.05f)]
    [Tooltip("Seconds between two checks of what is in its radius.")]
    [SerializeField] private float checkSeconds = 0.1f;
    [Tooltip("Colour of the energy bar, the radius ring and the arcs from generators.")]
    [SerializeField] private Color energyColor = new Color(0.45f, 0.85f, 1f, 1f);

    private static readonly List<PixelPowerBank> all = new List<PixelPowerBank>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    /// <summary>Is this placed device a Power Bank?</summary>
    public static bool IsBank(PixelPlacedDevice d) => d != null && d.GetComponent<PixelPowerBank>() != null;

    private PixelClicker clicker;
    private PixelPlacedDevice device;
    private float radius = 14f, capacity = 1800f, charge, nextCheck;
    private int powering, feeding;
    private PixelEnergyBar bar;
    private LineRenderer radiusRing;
    private readonly List<Transform> cores = new List<Transform>();
    private readonly List<Renderer> glowRenderers = new List<Renderer>();
    private readonly List<LineRenderer> arcs = new List<LineRenderer>();
    private Material arcMaterial;

    // ---- Building (in husk widths u) ----

    private const float CellHeight = 3.2f;
    private static readonly Color Plinth = new Color(0.14f, 0.15f, 0.18f, 1f), Steel = new Color(0.72f, 0.76f, 0.82f, 1f),
                                  Glass = new Color(0.7f, 0.9f, 1f, 0.35f), Body = new Color(0.2f, 0.27f, 0.36f, 1f);

    private static float Unit(PixelClicker clicker) => clicker != null && clicker.OldPixelWorldSize > 0f ? clicker.OldPixelWorldSize : 0.5f;

    /// <summary>The three cells' centres (x, z) in husk widths.</summary>
    private static readonly Vector2[] CellSpots = { new Vector2(-1.05f, 0.45f), new Vector2(1.05f, 0.45f), new Vector2(0f, -0.9f) };

    public static GameObject Place(PixelClicker clicker, Vector3 point, float yaw)
    {
        GameObject root = Build(clicker, false, true);
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return root;
    }

    public static GameObject CreateGhost(PixelClicker clicker) => Build(clicker, true, false);
    public static GameObject CreateDisplay(PixelClicker clicker) => Build(clicker, false, false);

    private static GameObject Build(PixelClicker clicker, bool ghost, bool colliders)
    {
        float u = Unit(clicker);
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        GameObject root = new GameObject(ghost ? "Power Bank (Preview)" : "Power Bank");
        Transform t = root.transform;
        Color glow = new Color(0.45f, 0.85f, 1f, 1f);
        float baseH = 0.5f * u, top = baseH + CellHeight * u;

        // A heavy round plinth with a lit band round it.
        k.Cyl("Plinth", t, new Vector3(0f, baseH * 0.5f, 0f), 2.3f * u, baseH, Plinth, default, 0.5f, 0.45f, colliders);
        k.Prim(PrimitiveType.Cylinder, "Plinth Band", t, new Vector3(0f, baseH * 0.7f, 0f), new Vector3(4.66f * u, 0.06f * u, 4.66f * u), glow, default, 0f, 0.8f, false, glow);
        // A central steel spine holding the cells together.
        k.Cyl("Spine", t, new Vector3(0f, baseH + CellHeight * u * 0.5f, 0f), 0.45f * u, CellHeight * u, Body, default, 0.6f, 0.55f, colliders);

        // Three tall glass cells: a steel foot and cap, a see-through tube and a glowing core inside that rises with the charge ("Core 0..2").
        for (int i = 0; i < CellSpots.Length; i++)
        {
            Vector3 c = new Vector3(CellSpots[i].x * u, 0f, CellSpots[i].y * u);
            k.Cyl("Cell Foot", t, c + new Vector3(0f, baseH + 0.15f * u, 0f), 0.78f * u, 0.3f * u, Steel, default, 0.85f, 0.7f, colliders);
            k.Cyl("Cell Cap", t, c + new Vector3(0f, top - 0.15f * u, 0f), 0.78f * u, 0.3f * u, Steel, default, 0.85f, 0.7f, colliders);
            GameObject tube = k.Cyl("Cell Glass", t, c + new Vector3(0f, baseH + CellHeight * u * 0.5f, 0f), 0.7f * u, (CellHeight - 0.6f) * u, Glass, default, 0.1f, 0.95f, colliders);
            Renderer tr = tube.GetComponent<Renderer>();
            if (!ghost && tr != null && clicker != null) { Material gm = clicker.CreateVisualMaterial(Glass, true); if (gm != null) tr.sharedMaterial = gm; }   // (see-through)
            GameObject core = k.Prim(PrimitiveType.Cylinder, "Core " + i, t, c + new Vector3(0f, baseH + 0.3f * u, 0f), new Vector3(1.1f * u, 0.001f, 1.1f * u), glow, default, 0f, 0.8f, false, glow);
            core.SetActive(!ghost);
            for (int r = 0; r < 4; r++)   // steel ribs up the glass
            {
                float a = (r * 90f + 45f) * Mathf.Deg2Rad;
                k.Box("Cell Rib", t, c + new Vector3(Mathf.Cos(a) * 0.72f * u, baseH + CellHeight * u * 0.5f, Mathf.Sin(a) * 0.72f * u), new Vector3(0.08f * u, (CellHeight - 0.6f) * u, 0.08f * u), Steel, default, false, 0.85f, 0.7f);
            }
        }

        // The top: a steel lid, a lit ring and a pair of terminals with a glowing ball between them (the generators' arcs land here).
        k.Cyl("Lid", t, new Vector3(0f, top + 0.12f * u, 0f), 2.05f * u, 0.24f * u, Steel, default, 0.9f, 0.75f, colliders);
        k.Prim(PrimitiveType.Cylinder, "Lid Glow", t, new Vector3(0f, top + 0.26f * u, 0f), new Vector3(2.4f * u, 0.04f * u, 2.4f * u), glow, default, 0f, 0.8f, false, glow);
        for (int s = -1; s <= 1; s += 2)
        {
            k.Cyl("Terminal", t, new Vector3(s * 0.55f * u, top + 0.6f * u, 0f), 0.14f * u, 0.7f * u, Steel, default, 0.9f, 0.8f);
            k.Ball("Terminal Cap", t, new Vector3(s * 0.55f * u, top + 0.98f * u, 0f), 0.2f * u, s < 0 ? new Color(0.85f, 0.15f, 0.12f, 1f) : new Color(0.1f, 0.1f, 0.12f, 1f), 0.3f, 0.7f);
        }
        k.Prim(PrimitiveType.Sphere, "Terminal Glow", t, new Vector3(0f, top + 0.75f * u, 0f), Vector3.one * 0.4f * u, glow, default, 0f, 0.8f, false, glow);
        return root;
    }

    /// <summary>Where the generators' arcs land (world).</summary>
    private Vector3 TopPoint => transform.TransformPoint(new Vector3(0f, (0.5f + CellHeight + 0.75f) * Unit(clicker), 0f));

    /// <summary>Sets the numbers from its Device entry (placing and loading). It comes empty.</summary>
    public void Init(PixelClicker owner, float powerRadius, float capacitySeconds)
    {
        clicker = owner;
        radius = Mathf.Max(1f, powerRadius);
        capacity = Mathf.Max(1f, capacitySeconds);
        charge = 0f;
        float u = Unit(clicker);
        for (int i = 0; i < CellSpots.Length; i++) { Transform c = transform.Find("Core " + i); if (c != null) cores.Add(c); }
        foreach (string n in new[] { "Plinth Band", "Lid Glow", "Terminal Glow" })
        {
            Transform g = transform.Find(n);
            if (g != null && g.GetComponent<Renderer>() != null) glowRenderers.Add(g.GetComponent<Renderer>());
        }
        bar = PixelEnergyBar.Attach(transform, new Vector3(0f, (0.5f + CellHeight + 1.7f) * u, 0f), 2.6f * u, energyColor);
        BuildRadiusRing();
        ShowCharge();
    }

    private void BuildRadiusRing()
    {
        GameObject go = new GameObject("Power Radius");
        go.transform.SetParent(transform, false);
        radiusRing = go.AddComponent<LineRenderer>();
        radiusRing.material = ArcMaterial();
        radiusRing.useWorldSpace = false;
        radiusRing.loop = true;
        radiusRing.widthMultiplier = Unit(clicker) * 0.14f;
        radiusRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        radiusRing.receiveShadows = false;
        const int n = 96;
        radiusRing.positionCount = n;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            radiusRing.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.04f, Mathf.Sin(a) * radius));
        }
        Color faint = new Color(energyColor.r, energyColor.g, energyColor.b, 0.35f);
        radiusRing.startColor = radiusRing.endColor = faint;
    }

    private Material ArcMaterial()
    {
        if (arcMaterial != null) return arcMaterial;
        Shader s = Shader.Find("Sprites/Default");
        arcMaterial = new Material(s != null ? s : Shader.Find("Unlit/Color"));
        return arcMaterial;
    }

    private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { all.Remove(this); }
    private void OnDestroy() { if (arcMaterial != null) Destroy(arcMaterial); }

    // ---- Every frame ----

    private void Update()
    {
        if (clicker == null) return;
        if (device == null) device = GetComponent<PixelPlacedDevice>();
        if (device != null && device.IsEnding) { if (bar != null) bar.Set(-1f, energyColor); HideArcs(0); return; }
        bool calm = !PixelCalm.InMenu && Time.timeScale > 0f;
        if (calm && powering > 0) charge = Mathf.Max(0f, charge - drainPerDevice * powering * Time.deltaTime);
        ShowCharge();
        if (!calm || Time.time < nextCheck) return;
        float dt = checkSeconds + Mathf.Max(0f, Time.time - nextCheck);   // (the time since the last check)
        nextCheck = Time.time + checkSeconds;
        DrawFromGenerators(Mathf.Min(dt, 0.5f));
        if (charge > 0f) PowerAround(charge / capacity);
        else powering = 0;
    }

    /// <summary>The bar, the glowing cores (they rise with the charge) and the lit parts (dim when empty).</summary>
    private void ShowCharge()
    {
        float f = Mathf.Clamp01(charge / capacity);
        if (bar != null) bar.Set(f, energyColor);
        float u = Unit(clicker), h = (CellHeight - 0.6f) * u * f;
        foreach (Transform c in cores)
        {
            if (c == null) continue;
            c.localScale = new Vector3(1.1f * u, Mathf.Max(0.0005f, h * 0.5f), 1.1f * u);
            Vector3 p = c.localPosition;
            c.localPosition = new Vector3(p.x, 0.5f * u + 0.3f * u + h * 0.5f, p.z);
            c.gameObject.SetActive(f > 0.001f);
        }
        float pulse = feeding > 0 ? 0.75f + 0.25f * Mathf.Sin(Time.time * 8f) : 1f;
        SetGlow(charge > 0f ? energyColor * pulse : new Color(0.15f, 0.2f, 0.25f, 1f));
    }

    private Color glowShown = new Color(-1f, 0f, 0f, 0f);

    private void SetGlow(Color c)
    {
        c.a = 1f;
        if (c == glowShown) return;
        glowShown = c;
        foreach (Renderer r in glowRenderers)
        {
            if (r == null) continue;
            Material m = r.material;
            m.color = c;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.2f); }
        }
    }

    /// <summary>Every Biomass Generator inside the radius pushes its spare charge in (above keepInGenerator of its max), with a blue arc while it flows.</summary>
    private void DrawFromGenerators(float dt)
    {
        feeding = 0;
        Vector3 me = transform.position;
        foreach (PixelBiomassGenerator g in PixelBiomassGenerator.All)
        {
            if (g == null || !g.Running) continue;
            Vector3 dv = g.transform.position - me; dv.y = 0f;
            if (dv.sqrMagnitude > radius * radius) continue;
            float room = capacity - charge;
            if (room <= 0.001f) break;
            float got = g.GiveCharge(Mathf.Min(room, chargeInPerSecond * dt), keepInGenerator);
            if (got <= 0f) continue;
            charge += got;
            Arc(feeding++, g.TopPoint, TopPoint);
            PixelStats.Count("bank.chargedIn", got);
        }
        HideArcs(feeding);
    }

    /// <summary>Arc 'i' from a generator to the bank: a slightly sagging, flickering line.</summary>
    private void Arc(int i, Vector3 from, Vector3 to)
    {
        while (arcs.Count <= i)
        {
            GameObject go = new GameObject("Charge Arc");
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = ArcMaterial();
            lr.useWorldSpace = true;
            lr.positionCount = 12;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            arcs.Add(lr);
        }
        LineRenderer a = arcs[i];
        a.gameObject.SetActive(true);
        float u = Unit(clicker), len = Vector3.Distance(from, to);
        a.widthMultiplier = u * Random.Range(0.06f, 0.11f);
        Color c = new Color(energyColor.r, energyColor.g, energyColor.b, 0.85f);
        a.startColor = c; a.endColor = Color.Lerp(c, Color.white, 0.5f);
        for (int p = 0; p < a.positionCount; p++)
        {
            float t = p / (float)(a.positionCount - 1);
            Vector3 pos = Vector3.Lerp(from, to, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * len * 0.08f;
            if (p > 0 && p < a.positionCount - 1) pos += Random.insideUnitSphere * u * 0.12f;   // (a little crackle)
            a.SetPosition(p, pos);
        }
    }

    private void HideArcs(int from) { for (int i = from; i < arcs.Count; i++) if (arcs[i] != null) arcs[i].gameObject.SetActive(false); }

    /// <summary>Every timed, switched-on placed device within the radius (not generators, not banks) runs on the bank's power: its countdown stops and it shows this bar.</summary>
    private void PowerAround(float fraction)
    {
        powering = 0;
        Vector3 me = transform.position;
        foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
        {
            if (d == null || d == device || d.IsEnding || PixelBiomassGenerator.IsPowerSource(d)) continue;
            if (!d.NeedsPower) continue;   // (no countdown to stop: a permanent device, or one not switched on yet - no energy bar, no charge spent; PixelPlacedDevice.NeedsPower, shared with the Biomass Generator)
            Vector3 dv = d.transform.position - me; dv.y = 0f;
            if (dv.sqrMagnitude > radius * radius) continue;
            d.HoldPower(fraction, energyColor);
            powering++;
        }
    }

    // ---- The device card / panel ----

    public void FillDeviceInfo(PixelDeviceInfo info)
    {
        info.Add("Charge", Mathf.FloorToInt(charge) + " / " + Mathf.CeilToInt(capacity) + " s");
        info.Progress = charge / capacity;
        info.ProgressLabel = charge <= 0f ? "EMPTY" : "";
        info.Add("Powering", powering + " device" + (powering == 1 ? "" : "s"));
        info.Add("Charging from", feeding + " generator" + (feeding == 1 ? "" : "s"));
        if (charge <= 0f && feeding == 0) info.Note("<color=#FF8A7A>Empty: put a running Biomass Generator inside its ring.</color>");
        if (!info.Full) return;
        info.Add("Reach", radius.ToString("0.#") + " units");
        info.Add("Use", (drainPerDevice * Mathf.Max(1, powering)).ToString("0.##") + " s of charge a second");
        info.Note("<color=#9FB0CC>Biomass Generators inside its ring charge it with their spare power (they keep " + Mathf.RoundToInt(keepInGenerator * 100f) +
                  "% for themselves). While it holds charge, every device in the ring runs on it: its timer stops.</color>");
    }

    // ---- Saving (PixelConsumables puts this in PlacedState.extra) ----

    public string ExportState() => charge.ToString("R", CultureInfo.InvariantCulture);

    public void ImportState(string data)
    {
        if (!string.IsNullOrEmpty(data) && float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float c)) charge = Mathf.Clamp(c, 0f, capacity);
        ShowCharge();
    }
}
