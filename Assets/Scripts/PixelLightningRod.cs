using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A placed Lightning Rod (device kind LightningRod). It lasts a number of lightning strikes (not time). Every <c>strikeSeconds</c>,
/// if there are old pixels of another type in its radius, lightning hits the rod and turns EVERY such pixel into an Electric pixel
/// (same payout, thrown up a little). Strikes only happen when something can be converted, so none is wasted.
/// </summary>
public class PixelLightningRod : PixelPlacedDevice
{
    private PixelBoltLayer bolts;
    private float radius;
    private float strikeSeconds;
    private float strikeTimer;
    private float height = 2.5f;
    private float fxLeft;
    private float redrawTimer;
    private Vector3 skyPoint;
    private readonly List<Vector3> hitPoints = new List<Vector3>();
    private Light flash;

    private static readonly Color Core = new Color(1f, 1f, 0.9f, 1f);
    private static readonly Color Glow = new Color(0.55f, 0.8f, 1f, 0.6f);

    public void Init(PixelClicker owner, Camera camera, TextMeshPro timer, string format, int uses, float shrinkTime,
                     PixelConsumables.Device d, float bodyHeight, float strikeStart)
    {
        InitCommon(owner, camera, timer, format, 0f, shrinkTime);
        InitUses(uses);
        radius = Mathf.Max(0.5f, d.radius);
        strikeSeconds = Mathf.Max(0.2f, d.strikeSeconds);
        strikeTimer = strikeStart > 0f ? strikeStart : strikeSeconds;
        height = bodyHeight;

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        Destroy(tip.GetComponent<Collider>());
        tip.transform.SetParent(transform, false);
        tip.transform.localPosition = new Vector3(0f, height, 0f);
        tip.transform.localScale = Vector3.one * (d.bodyDiameter * 1.8f);
        tip.GetComponent<Renderer>().sharedMaterial = PixelLooks.OverlayMaterial();
        tip.GetComponent<Renderer>().material.color = new Color(1f, 1f, 0.8f, 1f);

        GameObject lg = new GameObject("Flash");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, height, 0f);
        flash = lg.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(0.7f, 0.85f, 1f);
        flash.range = 6f;
        flash.intensity = 0f;

        bolts = new PixelBoltLayer("Rod Bolts");
    }

    protected override string UsesLabel() => UsesLeft + " x";

    private Vector3 Tip => transform.position + Vector3.up * height;

    protected override void OnTick()
    {
        strikeTimer -= Time.deltaTime;
        if (strikeTimer <= 0f)
        {
            strikeTimer = strikeSeconds;
            Strike();
        }

        if (fxLeft > 0f)
        {
            fxLeft -= Time.unscaledDeltaTime;
            flash.intensity = Mathf.Max(0f, fxLeft / 0.35f) * 6f;
            redrawTimer -= Time.unscaledDeltaTime;
            if (redrawTimer <= 0f)
            {
                redrawTimer = 0.04f;
                Redraw();
            }
            if (fxLeft <= 0f) { bolts.Begin(); bolts.End(1f); }
        }
    }

    private void Strike()
    {
        int electric = clicker.IndexOf(PixelClicker.PixelType.Electric);
        if (electric < 0) return;

        List<Rigidbody> targets = new List<Rigidbody>();
        foreach (Rigidbody body in clicker.OldPixels)
        {
            if (body == null || clicker.IsFlyingPixel(body)) continue;
            if ((body.position - transform.position).sqrMagnitude > radius * radius) continue;
            OldPixelInfo info = PixelHuskQuery.Get(body).info;
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex)) continue;
            PixelClicker.PixelTier tier = clicker.Tiers[info.tierIndex];
            if (tier.type == PixelClicker.PixelType.Electric || tier.flyAway || PixelClicker.IsRareCube(tier.type)) continue;
            OldPixelDespawn dd = PixelHuskQuery.Get(body).despawn;
            if (dd != null && dd.IsDespawning) continue;
            targets.Add(body);
        }
        if (targets.Count == 0) return;

        hitPoints.Clear();
        foreach (Rigidbody body in targets)
        {
            OldPixelInfo info = PixelHuskQuery.Get(body).info;
            double amount = info.amount;
            Vector3 pos = body.position;
            if (!clicker.ReleaseOldPixel(body, false)) continue;
            Destroy(body.gameObject);
            clicker.SpawnStoredPixel(electric, amount, pos, Vector3.up * 2.5f + Random.insideUnitSphere);
            hitPoints.Add(pos);
        }

        skyPoint = Tip + new Vector3(Random.Range(-1f, 1f), 9f, Random.Range(-1f, 1f));
        fxLeft = 0.35f;
        redrawTimer = 0f;
        PixelAudio.Play("overcharge");
        PixelAudio.PlayScaled("pixel_bounce_electric", 1f);
        PixelStats.Count("rod.strikes");
        PixelStats.Count("rod.converted", hitPoints.Count);
        SpendUse();
    }

    private void Redraw()
    {
        float unit = Mathf.Max(0.1f, clicker.PixelBaseSize);
        bolts.Begin();
        bolts.Bolt(skyPoint, Tip, unit * 1.6f, Core, Glow, 0.1f);
        foreach (Vector3 p in hitPoints) bolts.Bolt(Tip, p, unit, Core, Glow, 0.15f);
        bolts.End(unit);
    }

    private void OnDestroy()
    {
        if (bolts != null) bolts.Destroy();
    }

    // ---- the device card (PixelDeviceCard) ----
    protected override void AddInfo(PixelDeviceInfo info)
    {
        if (Armed) info.Add("Next strike", Mathf.Max(0f, strikeTimer).ToString("0") + " s");
        if (!info.Full) return;
        info.Add("Strikes within", radius.ToString("0.#") + " units, every " + strikeSeconds.ToString("0") + " s");
    }

    // ---- The model (moved here from PixelConsumables 2026-10-24: each device builds its own look; PixelConsumables.BuildDeviceObject calls it) ----

    /// <summary>
    /// The Lightning Rod's look (2026-10-19): a two-step stone plinth with a copper ground ring and glowing studs, three steel guy struts, a tapering copper
    /// mast (steel collars at the joints) with a copper coil low down and a stack of white ceramic insulators in the middle, a glowing band near the top and
    /// a crown of outward-leaning spikes round the tip. The glowing tip ball itself is added at bodyHeight by PixelLightningRod (DecorateRodModel for the shop).
    /// </summary>
    public static void BuildModel(PixelClicker clicker, GameObject root, PixelConsumables.Device d, bool isPreview, float previewOpacity)
    {
        PixelFactoryKit k = new PixelFactoryKit(clicker, isPreview) { alpha = previewOpacity };
        Transform t = root.transform;
        float H = d.bodyHeight, r = Mathf.Max(0.04f, d.bodyDiameter * 0.5f);
        Color stone = new Color(0.3f, 0.31f, 0.34f, 1f), stoneTop = new Color(0.4f, 0.41f, 0.45f, 1f), copper = new Color(0.86f, 0.5f, 0.27f, 1f),
              steel = new Color(0.72f, 0.76f, 0.82f, 1f), ceramic = new Color(0.93f, 0.95f, 1f, 1f), glow = Color.Lerp(d.color, Color.white, 0.25f);

        // The plinth: two stone steps, a copper ground ring between them and six glowing studs round the lower step.
        k.Disc("Plinth", t, 0.06f, 0.42f, 0.12f, stone, 0.05f, 0.2f);
        k.Disc("Ground Ring", t, 0.125f, 0.35f, 0.025f, copper, 0.85f, 0.7f);
        k.Disc("Plinth Top", t, 0.17f, 0.28f, 0.08f, stoneTop, 0.05f, 0.25f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            k.Prim(PrimitiveType.Sphere, "Stud", t, new Vector3(Mathf.Cos(a) * 0.41f, 0.07f, Mathf.Sin(a) * 0.41f), Vector3.one * 0.05f, glow, default, 0f, 0.8f, false, glow);
        }

        // The mast: three copper pieces, thinner going up, with steel collars where they meet; a thin spike on top up to the tip.
        float[] joints = { 0.21f, H * 0.36f, H * 0.68f, H - 0.2f };
        float[] radii = { r * 1.6f, r * 1.2f, r * 0.85f };
        for (int i = 0; i < 3; i++)
        {
            k.Cyl("Mast " + i, t, new Vector3(0f, (joints[i] + joints[i + 1]) * 0.5f, 0f), radii[i], joints[i + 1] - joints[i], copper, default, 0.85f, 0.65f);
            k.Disc("Collar " + i, t, joints[i] + 0.02f, radii[i] * 1.5f, 0.05f, steel, 0.9f, 0.75f);
        }
        k.Cyl("Spike", t, new Vector3(0f, H - 0.1f, 0f), r * 0.45f, 0.2f, steel, default, 0.95f, 0.85f);

        // A copper coil wound round the lowest piece (thin rings, every other one tipped a little so it reads as a winding).
        for (int i = 0; i < 9; i++)
            k.Prim(PrimitiveType.Cylinder, "Coil", t, new Vector3(0f, 0.26f + i * 0.05f, 0f), new Vector3(r * 4.2f, 0.012f, r * 4.2f), copper,
                   new Vector3(i % 2 == 0 ? 5f : -5f, 0f, 0f), 0.9f, 0.7f);

        // White ceramic insulators stacked on the middle piece.
        for (int i = 0; i < 4; i++) k.Disc("Insulator", t, H * 0.42f + i * 0.09f, r * (3.4f - i * 0.25f), 0.045f, ceramic, 0.1f, 0.9f);

        // Three guy struts from the plinth edge up to the mast, with a steel clamp where they meet it.
        float clampY = H * 0.3f;
        k.Disc("Clamp", t, clampY, r * 2.4f, 0.06f, steel, 0.9f, 0.7f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 30f) * Mathf.Deg2Rad;
            Vector3 foot = new Vector3(Mathf.Cos(a) * 0.36f, 0.19f, Mathf.Sin(a) * 0.36f);
            k.Strut("Guy Strut", t, foot, new Vector3(Mathf.Cos(a) * r * 2.2f, clampY, Mathf.Sin(a) * r * 2.2f), 0.014f, steel);
            k.Prim(PrimitiveType.Sphere, "Strut Foot", t, foot, Vector3.one * 0.05f, steel, default, 0.9f, 0.7f);
        }

        // A glowing band near the top and a crown of spikes leaning out round the tip.
        k.Disc("Glow Band", t, H * 0.86f, r * 1.4f, 0.07f, glow, 0f, 0.8f, glow);
        k.Disc("Crown Ring", t, H - 0.24f, r * 2.2f, 0.035f, steel, 0.9f, 0.8f);
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 from = new Vector3(0f, H - 0.24f, 0f) + outward * r * 1.9f;
            k.Strut("Crown Spike", t, from, from + (outward * 0.35f + Vector3.up).normalized * 0.22f, 0.011f, steel, 0.95f, 0.85f);
        }
    }
}
