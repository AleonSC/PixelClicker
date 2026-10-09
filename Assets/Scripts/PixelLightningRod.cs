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
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex)) continue;
            PixelClicker.PixelTier tier = clicker.Tiers[info.tierIndex];
            if (tier.type == PixelClicker.PixelType.Electric || tier.flyAway || PixelClicker.IsDragonCube(tier.type)) continue;
            OldPixelDespawn dd = body.GetComponent<OldPixelDespawn>();
            if (dd != null && dd.IsDespawning) continue;
            targets.Add(body);
        }
        if (targets.Count == 0) return;

        hitPoints.Clear();
        foreach (Rigidbody body in targets)
        {
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
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
}
