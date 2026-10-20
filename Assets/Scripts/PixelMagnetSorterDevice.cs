// ---- System notes (moved here from CLAUDE.md) ----
// PixelMagnetSorterDevice.cs
//   (extends PixelPlacedDevice) Magnetized Sorter (DeviceKind.MagnetSorter 11, Device.craftOnly = never sold:
//   PixelConsumables.ItemNotSold is checked by the shop cards / search / TryBuyItems and the visitors' wares; made in Crafting from a
//   Sorter + Magnet: ItemKind.Device ingredients (Ingredient.device, Item.device, listed among the materials only for device kinds
//   some recipe uses, gold-rimmed cube icon), default recipe added by EnsureDefaultRecipes; settings arcDegrees 110, radius (arc
//   length), boxCapacity 60, shootInterval, pipeReach, sorterExitSpeed, durationSeconds 180). Placed like the fan (turn with scroll /
//   Q / E, starts disarmed). A grey box with a red horseshoe magnet on a short tube at the front (+Z): loose husks inside the
//   floor arc in front (BuildMagnetSorterBody, translucent fan named 'Range') are pulled to the magnet mouth and, within
//   absorbDistance, swallowed (ReleaseOldPixel(body,false), fly-in) and stored per type with their payout (Stock: count + total
//   amount; shot pixels carry the average). Three exit pipes (cubic-Bezier PixelTube from ports on the box's back to a nozzle on the
//   floor, Rebuild): hover one (screen distance to its points), left-click grabs it, it follows the mouse on the floor plane (clamped
//   to pipeReach, never inside the box), left-click puts it down, right-click puts it back; the mouse wheel over a pipe cycles which
//   stored type it carries (floating model + 'xN'); every shootInterval a pipe with stock fires one pixel of its type from the nozzle
//   (PixelClicker.SpawnStreamedPixel = SpawnStoredPixel with the Sorter's no-collision OldPixelStream); shot pixels are ignored by the
//   magnet for 6 s. When it ends (time up or removed) the stored stock comes back out onto the floor (ReleaseStock). PixelPointerClaim (hovering / grabbing a pipe) blocks cube clicks via ExternalClickBlock. Saved in PlacedState.extra
//   (ExportState/ImportState). NOTE: it must not define its own Update / LateUpdate (they would hide the base's); per-frame work runs
//   in OnTick.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using static PixelInput;

/// <summary>
/// A placed Magnetized Sorter (device <c>kind</c> = MagnetSorter; made by crafting a Sorter with a Magnet). A magnet head sucks the loose old
/// pixels lying in an arc in front of it and sends them down a tube into a box, where they are kept by type (with the payout each carried).
/// Three exit pipes lead out of the box. Click a pipe to grab it, move the mouse and click again to put its end down anywhere in reach (the
/// pipe bends to follow); right-click or Escape-less cancel puts it back. Scroll the mouse wheel over a pipe to choose which stored type
/// it carries: it then shoots out only pixels of that type, one after another, as 'magnetized' pixels (no collisions while in the air,
/// like a Sorter's stream). The countdown, timer text and shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelMagnetSorterDevice : PixelPlacedDevice
{



    private class Stock { public int tier; public int count; public double total; }

    private class Pipe
    {
        public Vector3 endLocal;                  // where the nozzle sits, in the device's local space (on the floor)
        public Vector3 previousEnd;
        public int tier = -1;                     // which stored type it shoots (-1 = none)
        public int shownHave = -2;                // the count its label shows (the text is only rebuilt when it changes)
        public float nextShot;
        public MeshFilter filter;
        public Mesh mesh;
        public Transform nozzle;
        public Transform holder;                  // floats above the nozzle: the model of the chosen type + its count
        public GameObject model;
        public int modelTier = -2;
        public TMP_Text label;
        public Vector3[] points = new Vector3[0];
        public Vector3 heading = Vector3.back;    // horizontal direction pixels leave in (local)
    }

    private PixelConsumables.Device settings;
    private Transform mouth;
    private float boxSize, boxHeight, pipeRadius;
    private readonly List<Stock> stock = new List<Stock>();
    private readonly Pipe[] pipes = new Pipe[3];
    private readonly Dictionary<Rigidbody, float> ignoreUntil = new Dictionary<Rigidbody, float>();

    private void Awake()
    {
        PixelHuskPool.HuskReleasing += OnHuskReleasing;
        Ended += _ => ReleaseStock();   // when its time is up (or it is removed) the box's stock comes out again, like the Crate / Quantum Organizer
    }

    /// <summary>Every stored husk back onto the floor around the box (at most MaxReleased bodies; the value of any beyond that is shared out over them).</summary>
    private void ReleaseStock()
    {
        if (clicker == null) return;
        const int MaxReleased = 80;
        int all = 0;
        foreach (Stock s in stock) all += Mathf.Max(0, s.count);
        if (all <= 0) return;
        float share = all > MaxReleased ? (float)MaxReleased / all : 1f;
        Vector3 centre = transform.position + Vector3.up * 0.8f;
        foreach (Stock s in stock)
        {
            if (s.count <= 0) continue;
            int bodies = Mathf.Max(1, Mathf.RoundToInt(s.count * share));
            double each = s.total / bodies;
            for (int i = 0; i < bodies; i++)
            {
                Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3f), Random.Range(-1.2f, 1.2f));
                clicker.SpawnStoredPixel(s.tier, each, centre + Random.insideUnitSphere * 0.3f, v);
            }
            s.count = 0; s.total = 0d;
        }
        total = 0;
    }
    private void OnDestroy() => PixelHuskPool.HuskReleasing -= OnHuskReleasing;
    // (PixelHuskPool: the husk is going away and may be reused - let go of it)
    private void OnHuskReleasing(Rigidbody body) => ignoreUntil.Remove(body);
    private TMP_Text boxLabel;
    private Pipe grabbed;
    private Material pipeMaterial;
    private int total;

    private Vector3 PortLocal(int i) => new Vector3((i - 1) * boxSize * 0.3f, boxHeight * 0.55f, -boxSize * 0.5f);

    public void Init(PixelClicker owner, Camera camera, PixelConsumables.Device device, TextMeshPro timer, string format, float duration,
                     float shrinkTime, Transform mouthPoint, float boxWidth, float boxTall)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        settings = device;
        mouth = mouthPoint;
        boxSize = boxWidth;
        boxHeight = boxTall;
        pipeRadius = Mathf.Max(0.05f, boxWidth * 0.07f);
        pipeMaterial = clicker.CreateVisualMaterial(new Color(0.7f, 0.72f, 0.78f, 1f), false);

        boxLabel = MakeLabel("Box Count", new Vector3(0f, boxHeight + 0.2f, 0f), 2.2f);
        for (int i = 0; i < pipes.Length; i++)
        {
            Pipe p = new Pipe();
            float side = (i - 1);
            p.endLocal = new Vector3(side * boxSize * 1.5f, 0.28f, -boxSize * (1.7f + (i == 1 ? 0.5f : 0f)));
            GameObject go = new GameObject("Exit Pipe " + i, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            p.filter = go.GetComponent<MeshFilter>();
            p.mesh = new Mesh { name = "Exit Pipe" };
            p.filter.sharedMesh = p.mesh;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (pipeMaterial != null) mr.sharedMaterial = pipeMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            nozzle.name = "Nozzle";
            Destroy(nozzle.GetComponent<Collider>());
            nozzle.transform.SetParent(transform, false);
            nozzle.GetComponent<Renderer>().sharedMaterial = clicker.CreateVisualMaterial(new Color(0.95f, 0.55f, 0.15f, 1f), false);
            nozzle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.nozzle = nozzle.transform;

            p.holder = new GameObject("Holder").transform;
            p.holder.SetParent(transform, false);
            p.label = MakeLabel("Pipe Count", Vector3.zero, 1.5f, p.holder);
            pipes[i] = p;
            Rebuild(p);
        }
    }

    private TMP_Text MakeLabel(string name, Vector3 localPos, float size, Transform parent = null)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent != null ? parent : transform, false);
        g.transform.localPosition = localPos;
        TextMeshPro t = g.AddComponent<TextMeshPro>();
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        if (clicker.UIFont != null) t.font = clicker.UIFont;
        return t;
    }

    // ------------------------------------------------------------------
    // Pipes
    // ------------------------------------------------------------------

    private void Rebuild(Pipe p)
    {
        int index = System.Array.IndexOf(pipes, p);
        Vector3 port = PortLocal(index);
        Vector3 flat = p.endLocal - new Vector3(port.x, p.endLocal.y, port.z);
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.back;
        p.heading = flat.normalized;

        Vector3 p0 = port;
        Vector3 p1 = port + Vector3.back * boxSize * 0.6f;
        Vector3 p2 = p.endLocal - p.heading * Mathf.Min(1.1f, flat.magnitude * 0.5f) + Vector3.up * 0.1f;
        Vector3 p3 = p.endLocal;

        const int n = 22;
        if (p.points.Length != n + 1) p.points = new Vector3[n + 1];
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n, u = 1f - t;
            p.points[k] = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }
        PixelTube.Build(p.points, pipeRadius, 8, p.mesh);

        p.nozzle.localPosition = p.endLocal;
        p.nozzle.localRotation = Quaternion.LookRotation(p.heading, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        p.nozzle.localScale = new Vector3(pipeRadius * 3f, pipeRadius * 1.1f, pipeRadius * 3f);
        p.holder.localPosition = p.endLocal + Vector3.up * 0.75f;
    }

    private float ScreenDistanceToPipe(Pipe p, Vector2 pointer)
    {
        float best = float.MaxValue;
        for (int k = 0; k < p.points.Length; k++)
        {
            Vector3 s = cam.WorldToScreenPoint(transform.TransformPoint(p.points[k]));
            if (s.z <= 0f) continue;
            best = Mathf.Min(best, ((Vector2)s - pointer).magnitude);
        }
        return best;
    }

    private Pipe PipeUnderPointer(Vector2 pointer)
    {
        Pipe found = null;
        float best = 26f;
        foreach (Pipe p in pipes)
        {
            float d = ScreenDistanceToPipe(p, pointer);
            if (d < best) { best = d; found = p; }
        }
        return found;
    }

    private bool PointerOnFloor(out Vector3 world)
    {
        world = Vector3.zero;
        Plane plane = new Plane(Vector3.up, transform.position);
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        if (!plane.Raycast(ray, out float e)) return false;
        world = ray.GetPoint(e);
        return true;
    }

    protected override void OnTick()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        UpdateLabels();
        Shoot();
        if (PixelPauseMenu.IsPaused || Time.timeScale <= 0f) { grabbed = null; return; }

        if (grabbed != null)
        {
            PixelPointerClaim.Claim();
            if (RightPressed()) { grabbed.endLocal = grabbed.previousEnd; Rebuild(grabbed); grabbed = null; return; }
            if (PointerOnFloor(out Vector3 w))
            {
                Vector3 local = transform.InverseTransformPoint(w);
                local.y = 0.28f;
                Vector3 port = PortLocal(System.Array.IndexOf(pipes, grabbed));
                Vector3 off = local - new Vector3(port.x, local.y, port.z);
                float reach = Mathf.Max(2f, settings.pipeReach);
                if (off.magnitude > reach) local = new Vector3(port.x, local.y, port.z) + off.normalized * reach;
                // Never inside the box.
                float half = boxSize * 0.62f;
                if (Mathf.Abs(local.x) < half && local.z > -half && local.z < boxSize) local.z = -half;
                grabbed.endLocal = local;
                Rebuild(grabbed);
            }
            if (LeftPressed()) grabbed = null;       // put it down here
            return;
        }

        if (PointerOverUI()) return;
        Pipe hover = PipeUnderPointer(PointerPosition());
        if (hover == null) return;
        PixelPointerClaim.Claim();

        float scroll = ScrollY();
        if (Mathf.Abs(scroll) > 0.01f) CycleType(hover, scroll > 0f ? 1 : -1);
        else if (LeftPressed())
        {
            grabbed = hover;
            grabbed.previousEnd = grabbed.endLocal;
            PixelAudio.Play("sorter_button");
        }
    }

    private void CycleType(Pipe p, int step)
    {
        List<int> options = new List<int> { -1 };
        foreach (Stock s in stock) if (s.count > 0 || s.tier == p.tier) options.Add(s.tier);
        options.Sort();
        int at = Mathf.Max(0, options.IndexOf(p.tier));
        p.tier = options[(at + step + options.Count) % options.Count];
        PixelAudio.Play("sorter_button");
    }

    // ------------------------------------------------------------------
    // Sucking, storing and shooting
    // ------------------------------------------------------------------

    private bool ValidTier(int tier) => tier >= 0 && tier < clicker.Tiers.Length;

    private Stock StockOf(int tier)
    {
        foreach (Stock s in stock) if (s.tier == tier) return s;
        Stock added = new Stock { tier = tier };
        stock.Add(added);
        return added;
    }

    private void FixedUpdate()
    {
        if (IsDying || !Armed || clicker == null || mouth == null) return;

        // Suck the old pixels lying in the arc in front of the magnet.
        if (total < settings.boxCapacity)
        {
            Vector3 target = mouth.position;
            Vector3 forward = transform.forward;
            float half = Mathf.Clamp(settings.arcDegrees, 20f, 170f) * 0.5f;
            var pixels = clicker.OldPixels;
            for (int i = pixels.Count - 1; i >= 0; i--)
            {
                Rigidbody body = pixels[i];
                if (!PixelHuskHolder.Takeable(clicker, body, true)) continue;   // (one owner per husk; it now also leaves the husk in the player's hand alone)
                if (ignoreUntil.TryGetValue(body, out float until) && Time.time < until) continue;
                OldPixelInfo info = PixelHuskQuery.Get(body).info;

                Vector3 to = target - body.position;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                float distance = to.magnitude;
                if (distance > settings.radius) continue;
                if (flat.sqrMagnitude > 0.04f && Vector3.Angle(-flat, forward) > half) continue;   // outside the arc

                if (distance <= settings.absorbDistance) { Swallow(body, info); continue; }
                body.AddForce(to / distance * settings.pullAcceleration, ForceMode.Acceleration);
            }
        }
    }

    private void Swallow(Rigidbody body, OldPixelInfo info)
    {
        if (!clicker.ReleaseOldPixel(body, false)) return;
        Stock s = StockOf(info.tierIndex);
        s.count++;
        s.total += info.amount;
        total++;
        PixelStats.Count("magsorter.stored");
        StartCoroutine(Fly(body));
    }

    private IEnumerator Fly(Rigidbody body)
    {
        if (body == null) yield break;
        Collider[] colliders = body.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;
        OldPixelDespawn d = PixelHuskQuery.Get(body).despawn;
        if (d != null) Destroy(d);
        body.isKinematic = true;
        Transform t = body.transform;
        Vector3 from = t.position, startScale = t.localScale;
        for (float e = 0f; e < 0.25f && t != null; e += Time.deltaTime)
        {
            float k = e / 0.25f;
            if (mouth != null) t.position = Vector3.Lerp(from, mouth.position, k);
            t.localScale = startScale * (1f - k);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    private void UpdateLabels()
    {
        Quaternion face = cam.transform.rotation;
        if (boxLabel != null)
        {
            boxLabel.transform.rotation = face;
            if (total != shownTotal || settings.boxCapacity != shownCapacity)   // (rebuilt only when a number changes, not every frame)
            {
                shownTotal = total; shownCapacity = settings.boxCapacity;
                boxLabel.text = total + " / " + settings.boxCapacity;
            }
        }

        foreach (Pipe p in pipes)
        {
            Stock s = p.tier >= 0 ? FindStock(p.tier) : null;
            int have = s != null ? s.count : 0;
            if (p.modelTier != p.tier)
            {
                p.modelTier = p.tier;
                if (p.model != null) Destroy(p.model);
                p.model = p.tier >= 0 ? clicker.CreateDisplayPixel(p.tier, p.holder, 0.4f) : null;
            }
            if (p.model != null) p.model.transform.localRotation = Quaternion.Euler(20f, Time.time * 60f, 0f);
            p.holder.rotation = face;
            if (p.model != null) p.model.transform.position = p.holder.position + Vector3.up * 0.25f;
            p.label.transform.localPosition = Vector3.down * 0.15f;
            int shown = p.tier < 0 ? -1 : have;
            if (shown != p.shownHave) { p.shownHave = shown; p.label.text = p.tier < 0 ? "scroll to pick" : "x" + have; }
        }
    }

    private int shownTotal = -1, shownCapacity = -1;

    /// <summary>The stored stock of a type, or null - without adding one (a plain loop: runs per pipe every frame).</summary>
    private Stock FindStock(int tier)
    {
        for (int i = 0; i < stock.Count; i++) if (stock[i].tier == tier) return stock[i];
        return null;
    }

    private void Shoot()
    {
        if (IsDying || !Armed || clicker == null || Time.timeScale <= 0f) return;
        foreach (Pipe p in pipes)
        {
            if (p.tier < 0 || Time.time < p.nextShot) continue;
            Stock s = FindStock(p.tier);
            if (s == null || s.count <= 0) continue;
            p.nextShot = Time.time + Mathf.Max(0.05f, settings.shootInterval);

            double amount = s.total / s.count;
            s.count--;
            s.total -= amount;
            total--;
            if (s.count <= 0) { s.count = 0; s.total = 0d; }

            Vector3 position = transform.TransformPoint(p.endLocal + Vector3.up * 0.1f);
            Vector3 dir = transform.TransformDirection(p.heading);
            Vector3 velocity = dir * settings.sorterExitSpeed + Vector3.up * 1.5f + Random.insideUnitSphere * 0.3f;
            Rigidbody shot = clicker.SpawnStreamedPixel(p.tier, amount, position, velocity);
            if (shot != null) ignoreUntil[shot] = Time.time + 6f;
        }
        if (ignoreUntil.Count > 40) ignoreUntil.Clear();
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>The box contents and the pipes as text (stored in the save file).</summary>
    public string ExportState()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        foreach (Stock s in stock)
            if (s.count > 0) sb.Append("S,").Append(s.tier).Append(',').Append(s.count).Append(',').Append(s.total.ToString("R", ci)).Append(';');
        foreach (Pipe p in pipes)
            sb.Append("P,").Append(p.endLocal.x.ToString("R", ci)).Append(',').Append(p.endLocal.z.ToString("R", ci)).Append(',').Append(p.tier).Append(';');
        return sb.ToString();
    }

    public void ImportState(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        CultureInfo ci = CultureInfo.InvariantCulture;
        int pipeIndex = 0;
        foreach (string entry in text.Split(';'))
        {
            string[] f = entry.Split(',');
            if (f.Length < 4) continue;
            if (f[0] == "S" && int.TryParse(f[1], out int tier) && int.TryParse(f[2], out int count) && double.TryParse(f[3], NumberStyles.Float, ci, out double sum))
            {
                if (!ValidTier(tier) || count <= 0) continue;
                Stock s = StockOf(tier);
                s.count += count;
                s.total += sum;
                total += count;
            }
            else if (f[0] == "P" && pipeIndex < pipes.Length && float.TryParse(f[1], NumberStyles.Float, ci, out float x) && float.TryParse(f[2], NumberStyles.Float, ci, out float z) && int.TryParse(f[3], out int sel))
            {
                Pipe p = pipes[pipeIndex++];
                p.endLocal = new Vector3(x, 0.28f, z);
                p.tier = ValidTier(sel) ? sel : -1;
                Rebuild(p);
            }
        }
    }

    // ---- the device card (PixelDeviceCard) ----
    protected override void AddInfo(PixelDeviceInfo info)
    {
        info.Add("Stored", total.ToString());
        if (!info.Full || clicker == null) return;
        foreach (Stock s in stock)
            if (s.count > 0 && clicker.IsValidTierIndex(s.tier)) info.Add(clicker.Tiers[s.tier].displayName, s.count.ToString());
        int aimed = 0;
        foreach (Pipe p in pipes) if (p != null && p.tier >= 0) aimed++;
        info.Add("Pipes set", aimed + " / " + pipes.Length);
    }
}
