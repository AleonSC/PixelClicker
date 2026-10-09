using TMPro;
using UnityEngine;
using static PixelInput;

/// <summary>A world-space dynamic bolt mesh a device redraws (jagged crossed-ribbon bolts, glow + core). Not parented, so it never inherits a transform.</summary>
internal class PixelBoltLayer
{
    private readonly GameObject go;
    private readonly Mesh mesh;
    public readonly PixelBolts.Builder builder = new PixelBolts.Builder();
    private Bounds bounds;
    private bool any;

    public PixelBoltLayer(string name)
    {
        go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = PixelLooks.OverlayMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    public void Begin() { builder.Clear(); any = false; }

    public void Bolt(Vector3 a, Vector3 b, float unit, Color core, Color glow, float wander = 0.12f)
    {
        float length = Vector3.Distance(a, b);
        int segments = Mathf.Clamp(Mathf.CeilToInt(length / (unit * 0.3f)), 3, 10);
        PixelBolts.Jagged(builder, a, b, segments, length * wander, unit * 0.07f, unit * 0.25f, true, core, glow);
        if (!any) { bounds = new Bounds(a, Vector3.zero); any = true; }
        bounds.Encapsulate(a);
        bounds.Encapsulate(b);
    }

    public void End(float unit)
    {
        go.SetActive(any);
        if (!any) return;
        bounds.Expand(unit * 2f);
        builder.Apply(mesh, bounds);
    }

    public void Destroy()
    {
        if (go != null) Object.Destroy(go);
        if (mesh != null) Object.Destroy(mesh);
    }
}

/// <summary>
/// A placed Charge Booster (device kind ChargeBooster). It lasts a number of auto-clicker clicks (not time). Every Electric old pixel
/// that links straight to it adds charge to its meter and is dissipated (<see cref="PixelElectricLinks"/> calls <see cref="AddCharge"/>).
/// When the meter is full, clicking the booster starts a super charge: for a few seconds it fires extra auto-clicker clicks, with
/// lightning crackling around it. Super-charge clicks don't use up the booster.
/// </summary>
public class PixelChargeBooster : PixelPlacedDevice
{
    private Transform fill;
    private Renderer fillRenderer;
    private Renderer bodyRenderer;
    private Light glow;
    private PixelBoltLayer bolts;
    private TextMeshPro label;

    private float capacity = 10f;
    private float charge;
    private float superSeconds = 6f;
    private float superClicksPerSecond = 12f;
    private float superLeft;
    private float clickAccumulator;
    private float boltTimer;
    private bool bursting;
    private float meterHeight = 1f;
    private float clickRadiusPixels = 70f;
    private Color color = Color.yellow;

    /// <summary>Charge as a fraction of the meter (0-1).</summary>
    public float ChargeFraction => capacity > 0f ? Mathf.Clamp01(charge / capacity) : 0f;

    /// <summary>The meter is full and the booster waits to be clicked.</summary>
    public bool Ready => charge >= capacity && superLeft <= 0f;

    /// <summary>True while the super charge runs.</summary>
    public bool Supercharged => superLeft > 0f;

    /// <summary>Set by the save system after Init.</summary>
    public void SetCharge(float amount) => charge = Mathf.Clamp(amount, 0f, capacity);

    /// <summary>Current charge in meter units (saved).</summary>
    public float Charge => charge;

    public void Init(PixelClicker owner, Camera camera, TextMeshPro timer, string format, int uses, float shrinkTime,
                     PixelConsumables.Device d, float bodyHeight)
    {
        InitCommon(owner, camera, timer, format, 0f, shrinkTime);
        InitUses(uses);
        capacity = Mathf.Max(1f, d.chargeCapacity);
        superSeconds = Mathf.Max(0.5f, d.superChargeSeconds);
        superClicksPerSecond = Mathf.Max(1f, d.superClicksPerSecond);
        color = d.color;
        meterHeight = bodyHeight;
        label = timer;
        clicker.PixelCollected += OnCollected;
        BuildVisuals(d);
    }

    private void BuildVisuals(PixelConsumables.Device d)
    {
        bodyRenderer = GetComponentInChildren<Renderer>();
        // A tall thin meter beside the body that fills with charge.
        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
        back.name = "Meter Back";
        Destroy(back.GetComponent<Collider>());
        back.transform.SetParent(transform, false);
        back.transform.localScale = new Vector3(0.14f, meterHeight * 1.1f, 0.14f);
        back.transform.localPosition = new Vector3(d.bodyDiameter * 0.75f, meterHeight * 0.55f, 0f);
        back.GetComponent<Renderer>().sharedMaterial = PixelLooks.OverlayMaterial();
        back.GetComponent<Renderer>().material.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject f = GameObject.CreatePrimitive(PrimitiveType.Cube);
        f.name = "Meter Fill";
        Destroy(f.GetComponent<Collider>());
        f.transform.SetParent(transform, false);
        fill = f.transform;
        fillRenderer = f.GetComponent<Renderer>();
        fillRenderer.sharedMaterial = PixelLooks.OverlayMaterial();
        fillRenderer.material.color = color;

        GameObject lg = new GameObject("Glow");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, meterHeight * 0.7f, 0f);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 3f;
        glow.intensity = 0f;

        bolts = new PixelBoltLayer("Booster Bolts");
        UpdateMeter();
    }

    private Vector3 Anchor() => transform.position + Vector3.up * (meterHeight * 0.6f);

    private void OnCollected(int tier, double amount, bool automatic)
    {
        if (!automatic || bursting || IsDying || Supercharged) return;
        SpendUse();
    }

    /// <summary>An Electric pixel gave its charge. Returns false when the meter is full or the booster is busy (the pixel is left alone).</summary>
    public bool AddCharge(float amount)
    {
        if (IsDying || Supercharged || charge >= capacity) return false;
        charge = Mathf.Min(capacity, charge + amount);
        PixelAudio.PlayScaled("pixel_bounce_electric", 0.5f);
        if (charge >= capacity) PixelAudio.Play("overcharge");
        return true;
    }

    protected override string UsesLabel() => Supercharged ? "SUPER!" : Ready ? "CLICK!" : UsesLeft.ToString();

    protected override void OnTick()
    {
        if (cam == null) cam = Camera.main;

        if (Ready && cam != null && Time.timeScale > 0f)
        {
            Vector2 screen = cam.WorldToScreenPoint(Anchor());
            bool over = Vector2.Distance(screen, PointerPosition()) < clickRadiusPixels && !PointerOverUI();
            if (over) PixelClicker.ExternalClickBlock = true;
            if (over && LeftPressed()) StartSuper();
        }

        if (Supercharged)
        {
            superLeft -= Time.deltaTime;
            clickAccumulator += Time.deltaTime * superClicksPerSecond;
            int shots = Mathf.FloorToInt(clickAccumulator);
            clickAccumulator -= shots;
            if (shots > 0 && !PixelMinigame.TakeoverActive && !clicker.CubeHidden)
            {
                bursting = true;
                for (int i = 0; i < shots; i++) clicker.AutoCollect();
                bursting = false;
            }
            if (superLeft <= 0f) { superLeft = 0f; PixelAudio.Play("time_resume"); }
        }

        UpdateMeter();
        DrawBolts();
    }

    private void StartSuper()
    {
        charge = 0f;
        superLeft = superSeconds;
        clickAccumulator = 0f;
        PixelAudio.Play("overcharge");
        PixelStats.Count("booster.supercharges");
        PixelHints.Announce("SUPER CHARGE!");
    }

    private void UpdateMeter()
    {
        if (fill == null) return;
        float f = Supercharged ? Mathf.Clamp01(superLeft / superSeconds) : ChargeFraction;
        float h = Mathf.Max(0.001f, meterHeight * 1.1f * f);
        fill.localScale = new Vector3(0.18f, h, 0.18f);
        PlaceFill(h);
        float pulse = Ready || Supercharged ? 0.6f + 0.4f * Mathf.Sin(Time.time * 14f) : 1f;
        Color c = Supercharged ? Color.white : Color.Lerp(color * 0.7f, color, ChargeFraction);
        c.a = 1f;
        fillRenderer.material.color = Color.Lerp(Color.white, c, pulse);
        glow.intensity = Supercharged ? 4f + 2f * Mathf.Sin(Time.time * 30f) : Ready ? 1.5f * pulse : ChargeFraction * 0.6f;
        if (label != null) label.color = Ready ? Color.yellow : Supercharged ? Color.white : label.color;
    }

    private void PlaceFill(float h)
    {
        Vector3 back = fill.parent.Find("Meter Back").localPosition;
        fill.localPosition = new Vector3(back.x, h * 0.5f, 0f);
    }

    private void DrawBolts()
    {
        boltTimer -= Time.unscaledDeltaTime;
        if (boltTimer > 0f) return;
        boltTimer = 0.07f;
        float unit = Mathf.Max(0.1f, clicker.PixelBaseSize);
        bolts.Begin();
        if (Supercharged || Ready)
        {
            Color core = new Color(1f, 1f, 0.85f, 1f);
            Color gl = new Color(color.r, color.g, color.b, 0.55f);
            int count = Supercharged ? 5 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = Anchor() + Random.onUnitSphere * (unit * 0.15f);
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f;
                float len = unit * (Supercharged ? Random.Range(1.2f, 2.8f) : Random.Range(0.5f, 1f));
                bolts.Bolt(a, a + dir.normalized * len, unit, core, gl);
            }
        }
        bolts.End(unit);
    }

    private void OnDestroy()
    {
        if (clicker != null) clicker.PixelCollected -= OnCollected;
        if (bolts != null) bolts.Destroy();
    }
}
