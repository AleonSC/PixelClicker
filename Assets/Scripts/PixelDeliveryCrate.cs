// ---- System notes ----
// PixelDeliveryCrate.cs
//   THE DELIVERY CRATE (2026-10-24, Devices session): the crate a vendor drops onto his DOCK when you accept his delivery job (PixelDeliveryJobs).
//   It is only a VIEW of the job in GameState.delivery: what is in it is written into the job's lines the moment a husk goes in, and PixelDeliveryJobs
//   builds it again from the job after a load (so it is not a placed device and not a tester object: nothing else saves it).
//   - MODEL (PixelFactoryKit): an open-topped slatted wooden crate on skids, his colour on the rim, ONE SECTION PER HUSK TYPE of the job (dividers),
//     each with a tab in that type's colour and a fill block that rises as the section fills; a big LABEL over it ('Bark 18 / 30', a line per
//     type, and the time left) that always faces the camera.
//   - IN: an In SOCKET on its front (towards the floor's centre; PixelSockets / PixelCoupling: a coupling fits itself when a belt or hose comes to it)
//     - AcceptHusk takes a wanted husk; IPixelHuskFilter / IPixelHuskRoom tell belts and hoses which types and how many it still takes (others are
//     thrown on, never queued). And the OPEN TOP: a husk thrown or dropped in (within its footprint) is taken if wanted, else BOUNCED back out.
//     Taking uses the shared steps: PixelHuskHolder.Takeable, ReleaseOldPixel, PixelHuskHolder.FlyIn into its section.
//   - CARD: hover = the shared device card (PixelDeviceCard.HoverSubject: each type have / need, time left, a bar); a right TAP = the shared panel
//     (PixelDeviceCard.OpenSubject: also the pay and how to fill it). No Remove button (info.NoRemove) and no carry handles: it can't be moved or
//     removed while the job runs - he takes it away himself.
//   - FULL: PixelDeliveryJobs.Complete pays, then Haul(): the lid drops on, a balloon in his colour inflates and the crate floats off and shrinks away.
//   - OUT OF TIME: SpillAndGo(): every husk in it pops back out onto the floor (with its own worth), the crate sinks into the floor and goes.
//   - DROP: when accepted it falls onto the dock from above (unscaled time: the visit has stopped the game).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PixelDeliveryCrate : MonoBehaviour, IPixelSocketDevice, IPixelHuskFilter, IPixelHuskRoom, IPixelPanelSubject
{
    private const float SectionHusks = 1.3f, DepthHusks = 1.6f, HeightHusks = 1f;
    private static readonly Color Wood = new Color(0.62f, 0.45f, 0.27f, 1f), DarkWood = new Color(0.36f, 0.24f, 0.14f, 1f),
                                  Iron = new Color(0.3f, 0.31f, 0.34f, 1f), BalloonString = new Color(0.9f, 0.9f, 0.85f, 1f);

    private PixelClicker clicker;
    private PixelDeliveryJobs jobs;
    private DeliveryState.Job job;
    private Color vendorColor;
    private float unit = 0.5f, width, depth, height, nextScan;
    private bool leaving;
    private PixelSocket[] sockets = new PixelSocket[0];
    private readonly List<Collider> body = new List<Collider>();
    private readonly List<Transform> fills = new List<Transform>();
    private TextMeshPro label;
    private Transform labelRoot;
    private readonly PixelRightTap tap = new PixelRightTap();
    private string vendorName = "";

    /// <summary>It is sealing / floating off or spilling and sinking (no longer part of a job).</summary>
    public bool Leaving => leaving;

    // ------------------------------------------------------------------
    // Building
    // ------------------------------------------------------------------

    /// <summary>A crate for a running job on its dock ('drop' = it falls onto the dock from above).</summary>
    public static PixelDeliveryCrate Create(PixelClicker c, PixelDeliveryJobs owner, DeliveryState.Job job, Vector3 point, Quaternion facing, Color color, bool drop)
    {
        GameObject root = new GameObject("Delivery Crate: " + job.vendor);
        root.transform.SetPositionAndRotation(point, facing);
        PixelDeliveryCrate crate = root.AddComponent<PixelDeliveryCrate>();
        crate.clicker = c; crate.jobs = owner; crate.job = job; crate.vendorColor = color;
        crate.vendorName = PixelDeliveryJobs.VendorName(job.vendor);
        crate.Build();
        if (drop) crate.StartCoroutine(crate.DropIn(point));
        return crate;
    }

    private void Build()
    {
        unit = Mathf.Max(0.05f, clicker != null ? clicker.OldPixelWorldSize : 0.5f);
        float u = unit;
        int n = Mathf.Max(1, job.lines.Count);
        width = Mathf.Max(2.2f * u, n * SectionHusks * u + 0.3f * u);
        depth = DepthHusks * u;
        height = HeightHusks * u;
        float wall = 0.1f * u;
        Transform t = transform;
        PixelFactoryKit k = new PixelFactoryKit(clicker, false, true);

        // Skids, the floor and four slatted walls (with colliders: husks dropped in land inside), corner posts, a rim in the vendor's colour.
        for (int s = -1; s <= 1; s += 2) k.Box("Skid", t, new Vector3(0f, 0.06f * u, s * depth * 0.32f), new Vector3(width + 0.1f * u, 0.12f * u, 0.18f * u), DarkWood, default, true, 0f, 0.3f);
        k.Box("Floor", t, new Vector3(0f, 0.15f * u, 0f), new Vector3(width, 0.08f * u, depth), Wood, default, true, 0f, 0.35f);
        for (int s = -1; s <= 1; s += 2)
        {
            Wall(k, t, new Vector3(0f, 0f, s * (depth - wall) * 0.5f), new Vector3(width, 0f, wall));
            Wall(k, t, new Vector3(s * (width - wall) * 0.5f, 0f, 0f), new Vector3(wall, 0f, depth));
        }
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
                k.Box("Post", t, new Vector3(a * (width * 0.5f - 0.06f * u), height * 0.5f + 0.1f * u, b * (depth * 0.5f - 0.06f * u)), new Vector3(0.14f * u, height + 0.05f * u, 0.14f * u), Iron, default, false, 0.7f, 0.5f);
        for (int s = -1; s <= 1; s += 2)
        {
            k.Box("Rim", t, new Vector3(0f, height + 0.12f * u, s * (depth - wall) * 0.5f), new Vector3(width + 0.04f * u, 0.07f * u, wall + 0.04f * u), vendorColor, default, false, 0.2f, 0.5f);
            k.Box("Rim", t, new Vector3(s * (width - wall) * 0.5f, height + 0.12f * u, 0f), new Vector3(wall + 0.04f * u, 0.07f * u, depth + 0.04f * u), vendorColor, default, false, 0.2f, 0.5f);
        }

        // One section per husk type: a divider between sections, a tab in the type's colour on the front rim, a fill block that rises as it fills.
        float inner = width - 2f * wall, section = inner / n;
        for (int i = 0; i < n; i++)
        {
            float x = -inner * 0.5f + section * (i + 0.5f);
            Color col = TypeColor(job.lines[i].type);
            if (i > 0) k.Box("Divider", t, new Vector3(x - section * 0.5f, height * 0.55f + 0.1f * u, 0f), new Vector3(0.05f * u, height * 0.9f, depth - 2f * wall), DarkWood, default, false, 0f, 0.3f);
            k.Box("Tab", t, new Vector3(x, height * 0.82f, depth * 0.5f + 0.01f * u), new Vector3(section * 0.55f, 0.22f * u, 0.03f * u), col, default, false, 0.1f, 0.6f, col * 0.4f);
            GameObject fill = k.Box("Fill", t, new Vector3(x, 0.2f * u, 0f), new Vector3(section - 0.12f * u, 0.01f * u, depth - 2f * wall - 0.12f * u), col, default, false, 0f, 0.4f);
            fills.Add(fill.transform);
        }

        foreach (Collider c in GetComponentsInChildren<Collider>()) body.Add(c);
        sockets = new[] { new PixelSocket("in", new Vector3(0f, 0.45f * u, depth * 0.5f + 0.35f * u), Vector3.forward, PixelSocketFlow.Intake) };
        PixelCouplings.Register(this);
        BuildLabel();
        RefreshLooks();
    }

    /// <summary>A slatted wall: three planks with gaps, the full height (a collider on each).</summary>
    private void Wall(PixelFactoryKit k, Transform t, Vector3 centre, Vector3 size)
    {
        float plank = height / 3f;
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = centre + Vector3.up * (0.2f * unit + plank * (i + 0.5f));
            Vector3 s = new Vector3(size.x, plank * 0.86f, size.z);
            k.Box("Plank", t, p, s, i == 1 ? DarkWood : Wood, default, true, 0f, 0.35f);
        }
    }

    private void BuildLabel()
    {
        GameObject lg = new GameObject("Label");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, height + 1.4f * unit, 0f);
        labelRoot = lg.transform;
        label = lg.AddComponent<TextMeshPro>();
        label.fontSize = 4.2f * unit / 0.5f;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = true;
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color32(0, 0, 0, 220);
        label.rectTransform.sizeDelta = new Vector2(14f, 6f);
        if (clicker != null && clicker.UIFont != null) label.font = clicker.UIFont;
    }

    private Color TypeColor(int type)
    {
        int tier = clicker != null ? clicker.IndexOf((PixelClicker.PixelType)type) : -1;
        return tier >= 0 ? clicker.Tiers[tier].UIColor : Color.white;
    }

    // ------------------------------------------------------------------
    // Sockets (In coupling on its front)
    // ------------------------------------------------------------------

    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => Wood;
    public bool AcceptHusk(PixelSocket socket, Rigidbody husk) => TryTake(husk);
    public bool Takes(PixelSocket socket, int tier) => LineFor(tier) != null;
    public int HuskRoom(PixelSocket socket)
    {
        int room = 0;
        if (job != null && !leaving) foreach (DeliveryState.Line l in job.lines) room += Mathf.Max(0, l.need - l.have);
        return room;
    }

    private void OnEnable() { if (sockets.Length > 0) PixelCouplings.Register(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); }

    // ------------------------------------------------------------------
    // Taking husks
    // ------------------------------------------------------------------

    /// <summary>The job's line this husk type goes into (with room left), or null.</summary>
    private DeliveryState.Line LineFor(int tier)
    {
        if (job == null || leaving || clicker == null || !clicker.IsValidTierIndex(tier)) return null;
        int type = (int)clicker.Tiers[tier].type;
        foreach (DeliveryState.Line l in job.lines) if (l.type == type && l.have < l.need) return l;
        return null;
    }

    private bool TryTake(Rigidbody husk)
    {
        if (husk == null) return false;
        OldPixelInfo info = PixelHuskQuery.Get(husk).info;
        DeliveryState.Line line = info != null ? LineFor(info.tierIndex) : null;
        if (line == null) return false;
        double amount = System.Math.Max(0d, info.amount);
        if (!clicker.ReleaseOldPixel(husk, false)) return false;
        int index = job.lines.IndexOf(line);
        Vector3 into = index >= 0 && index < fills.Count ? fills[index].position + Vector3.up * 0.3f * unit : transform.position + Vector3.up * height;
        StartCoroutine(PixelHuskHolder.FlyIn(husk.gameObject, null, into, false, 0.25f));
        line.have++;
        line.value += amount;
        PixelAudio.Play("ui_click", 1.3f + 0.2f * line.have / Mathf.Max(1, line.need));
        PixelStats.Count("delivery.husks");
        RefreshLooks();
        if (HuskRoom(null) <= 0 && jobs != null) jobs.Complete(job);
        return true;
    }

    /// <summary>The open top: husks in the crate's footprint are taken if wanted, else bounced back out (one look every 0.1 s).</summary>
    private void ScanTop()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.1f;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        float hx = width * 0.5f, hz = depth * 0.5f;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            if (i >= pixels.Count) continue;
            Rigidbody b = pixels[i];
            if (b == null || !PixelHuskHolder.Takeable(clicker, b)) continue;
            Vector3 p = transform.InverseTransformPoint(b.position);
            if (Mathf.Abs(p.x) > hx || Mathf.Abs(p.z) > hz || p.y < 0.05f * unit || p.y > height + 1.5f * unit) continue;
            if (TryTake(b) || leaving) continue;
            // Not wanted (another type, or that section is full): it bounces back out over the nearest wall.
            Vector3 outward = transform.TransformDirection(new Vector3(p.x, 0f, p.z)).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = transform.forward;
            PixelBody.SetVelocity(b, outward * 3f * unit / 0.5f + Vector3.up * 4.5f * unit / 0.5f);
        }
    }

    // ------------------------------------------------------------------
    // Every frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (clicker == null || leaving) return;
        Camera cam = clicker.ViewCamera;
        if (labelRoot != null && cam != null) labelRoot.rotation = Quaternion.LookRotation(labelRoot.position - cam.transform.position, Vector3.up);
        RefreshLabel();
        if (PixelCalm.InMenu) return;
        bool hovered = !PixelFirstPerson.Active && PixelPointerClaim.PointerOn(clicker, body);
        if (hovered) PixelDeviceCard.HoverSubject(this);
        // A right TAP opens the shared panel on it.
        if (PixelInput.RightPressed() || PixelInput.RightHeld() || tap.Pending)
        {
            bool usable = !PixelClicker.ExternalClickBlock && !PixelInput.PointerOverUI() && !PixelFirstPerson.Active && !PixelFeatureTester.Placing;
            if (tap.Update(usable && hovered ? (object)this : null)) PixelDeviceCard.OpenSubject(this);
        }
        ScanTop();
    }

    private float nextLabel;

    /// <summary>The label: a line per type ('Bark 18 / 30', ticked when full) and the time left (refreshed a few times a second).</summary>
    private void RefreshLabel()
    {
        if (label == null || job == null || Time.unscaledTime < nextLabel) return;
        nextLabel = Time.unscaledTime + 0.25f;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (DeliveryState.Line l in job.lines)
            sb.Append(jobs != null ? jobs.ColoredName(l.type) : ((PixelClicker.PixelType)l.type).ToString()).Append(' ')
              .Append(Mathf.Min(l.have, l.need)).Append(" / ").Append(l.need).Append(l.have >= l.need ? " <color=#7CFF8A>✓</color>" : "").Append('\n');
        double left = PixelDeliveryJobs.TimeLeft(job);
        sb.Append(left < 60d ? "<color=#FF7A6A>" : "<color=#D8E2F0>").Append(PixelDeliveryJobs.TimeText(left)).Append("</color>");
        label.text = sb.ToString();
    }

    /// <summary>Each section's fill block rises with how full it is.</summary>
    private void RefreshLooks()
    {
        for (int i = 0; i < fills.Count && i < job.lines.Count; i++)
        {
            DeliveryState.Line l = job.lines[i];
            float f = Mathf.Clamp01(l.need > 0 ? (float)l.have / l.need : 0f);
            float h = Mathf.Max(0.01f * unit, f * height * 0.85f);
            Transform fill = fills[i];
            Vector3 s = fill.localScale;
            fill.localScale = new Vector3(s.x, h, s.z);
            fill.localPosition = new Vector3(fill.localPosition.x, 0.19f * unit + h * 0.5f, fill.localPosition.z);
        }
        nextLabel = 0f;
    }

    // ------------------------------------------------------------------
    // The shared card / panel
    // ------------------------------------------------------------------

    public bool PanelAlive => this != null && !leaving && job != null;

    public void BuildPanelInfo(PixelDeviceInfo info)
    {
        if (job == null) return;
        info.Title = vendorName + "'s Crate";
        info.NoRemove = true;
        int need = 0, have = 0;
        foreach (DeliveryState.Line l in job.lines)
        {
            need += l.need; have += Mathf.Min(l.have, l.need);
            info.Add(jobs != null ? jobs.ColoredName(l.type) : ((PixelClicker.PixelType)l.type).ToString(), Mathf.Min(l.have, l.need) + " / " + l.need + (l.have >= l.need ? "  (full)" : ""));
        }
        info.Add("Time left", PixelDeliveryJobs.TimeText(PixelDeliveryJobs.TimeLeft(job)));
        info.Progress = need > 0 ? Mathf.Clamp01((float)have / need) : 0f;
        info.ProgressLabel = have + " / " + need;
        if (!info.Full) return;
        if (jobs != null) info.Add("Pays", jobs.PayText(job.pay, job.payType, job.rewardItem));
        info.Note("<color=#9FB0CC>Fill it through its In coupling (belts, hoses) or drop husks in the top. Only the asked types go in; others bounce off.</color>");
        info.Note("<color=#9FB0CC>It stays on " + vendorName + "'s dock until the job is done or runs out. Out of time: the husks come back out, no penalty.</color>");
    }

    public void PanelRemove() { }   // (no Remove button: info.NoRemove)
    public void PanelOpened() { PixelAudio.Play("ui_click", 1.1f); }
    public void PanelClosed() { }

    // ------------------------------------------------------------------
    // Arriving and leaving
    // ------------------------------------------------------------------

    /// <summary>It falls onto the dock from above (unscaled: a visit stops the game).</summary>
    private IEnumerator DropIn(Vector3 point)
    {
        float t = 0f, duration = 0.7f;
        Vector3 from = point + Vector3.up * 14f * unit;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            transform.position = Vector3.LerpUnclamped(from, point, k * k);
            yield return null;
        }
        transform.position = point;
        PixelAudio.Play("device_place");
        // A little squash on landing.
        for (float s = 0f; s < 0.25f; s += Time.unscaledDeltaTime)
        {
            float q = Mathf.Sin(s / 0.25f * Mathf.PI) * 0.12f;
            transform.localScale = new Vector3(1f + q, 1f - q, 1f + q);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    /// <summary>Full: the lid drops on, a balloon in his colour inflates and the crate floats off, shrinking away.</summary>
    public void Haul()
    {
        if (leaving) return;
        leaving = true;
        PixelCouplings.Unregister(this);
        if (label != null) label.text = "<color=#7CFF8A>Delivered!</color>";
        StartCoroutine(HaulRoutine());
    }

    private IEnumerator HaulRoutine()
    {
        float u = unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, false, true);
        foreach (Collider c in body) if (c != null) c.enabled = false;
        // The lid: drops onto the rim.
        GameObject lid = k.Box("Lid", transform, new Vector3(0f, height + 1.2f * u, 0f), new Vector3(width + 0.1f * u, 0.1f * u, depth + 0.1f * u), DarkWood, default, false, 0f, 0.3f);
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            lid.transform.localPosition = new Vector3(0f, Mathf.Lerp(height + 1.2f * u, height + 0.2f * u, (t / 0.35f) * (t / 0.35f)), 0f);
            yield return null;
        }
        lid.transform.localPosition = new Vector3(0f, height + 0.2f * u, 0f);
        PixelAudio.Play("device_place", 1.3f);
        // The balloon: a string and a ball in his colour that inflates.
        float top = height + 0.25f * u, stringLength = 2.2f * u;
        k.Strut("String", transform, new Vector3(0f, top, 0f), new Vector3(0f, top + stringLength, 0f), 0.02f * u, BalloonString, 0f, 0.3f);
        GameObject balloon = k.Ball("Balloon", transform, new Vector3(0f, top + stringLength + 0.9f * u, 0f), 0.9f * u, vendorColor, 0f, 0.85f);
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            float s = Mathf.SmoothStep(0.05f, 1f, t / 0.6f);
            balloon.transform.localScale = new Vector3(1.8f * u * s, 2.1f * u * s, 1.8f * u * s);
            yield return null;
        }
        // Up and away, swaying, shrinking at the end.
        Vector3 start = transform.position;
        float rise = 2.6f;
        for (float t = 0f; t < rise; t += Time.unscaledDeltaTime)
        {
            float k2 = t / rise;
            transform.position = start + Vector3.up * (k2 * k2 * 22f * u) + transform.right * Mathf.Sin(t * 2.4f) * 0.4f * u;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, Mathf.Sin(t * 2.4f) * 6f);
            if (k2 > 0.7f) transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, (k2 - 0.7f) / 0.3f);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Out of time: every husk in it pops back out onto the floor (with its worth), then it sinks into the floor and goes.</summary>
    public void SpillAndGo()
    {
        if (leaving) return;
        leaving = true;
        PixelCouplings.Unregister(this);
        if (label != null) label.text = "<color=#FF7A6A>Time's up</color>";
        foreach (Collider c in body) if (c != null) c.enabled = false;
        if (job != null && clicker != null)
        {
            foreach (DeliveryState.Line l in job.lines)
            {
                int tier = clicker.IndexOf((PixelClicker.PixelType)l.type);
                if (tier < 0 || l.have <= 0) continue;
                double each = l.value / l.have;
                for (int i = 0; i < l.have; i++)
                {
                    Vector3 at = transform.TransformPoint(new Vector3(Random.Range(-0.35f, 0.35f) * width, height + 0.3f * unit, Random.Range(-0.3f, 0.3f) * depth));
                    Vector2 side = Random.insideUnitCircle * 2.5f * unit / 0.5f;
                    clicker.SpawnStoredPixel(tier, each, at, new Vector3(side.x, Random.Range(4f, 6.5f) * unit / 0.5f, side.y));
                }
            }
        }
        PixelAudio.Play("device_remove");
        StartCoroutine(SinkRoutine());
    }

    private IEnumerator SinkRoutine()
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
        {
            float k = t / 1.2f;
            transform.position = start - Vector3.up * k * (height + 0.4f * unit);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy() { PixelCouplings.Unregister(this); }
}
