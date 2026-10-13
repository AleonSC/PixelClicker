// ---- System notes (moved here from CLAUDE.md) ----
// PixelStorageCrate.cs
//   (extends PixelPlacedDevice) Storage Crate (DeviceKind.StorageCrate 13, auto-added like the fan, Utilities card; 300 Gray, hold 3,
//   bodyDiameter 2.5 = width, bodyHeight 0.3; model PixelConsumables.BuildCrateBody: a wooden tray with two rows of five
//   compartments): permanent and never disarmed (no countdown: duration 1e9, timer text hidden; PlaceDevice / RestorePlacedDevice skip
//   Disarm). Drag old pixels over it with Pixel Grabbing and left-click the crate: loose old pixels lying within its footprint
//   (FindLoose, nearest first) are stored in the next free section (row 1 left to right, then row 2; ReleaseOldPixel + FlyIn, their
//   payout is kept, a spinning CreateDisplayPixel model sits in the compartment). Left-click a stored pixel (PickSection, screen
//   distance) to take it out: SpawnStoredPixel at the compartment and PixelGrab.BeginHold puts it in the mouse's hand. A pixel you
//   hold and let go of above the crate is stored at once (any pixel tagged 'recently held by the player' - OldPixelInfo.RecentlyHeld, PixelGrab.recentlyHeldSeconds - that ends up in the footprint is stored too, from ANY height: CaptureRecentlyHeld) (TryStoreReleased: PixelGrab.HeldBody was a pixel last frame and is not now,
//   and it is inside the footprint and below ~3 units above it). Two grip handles on the short (+/-X) ends (Handle parts built by
//   BuildCrateBody): hovering one shows the open-hand cursor (PixelHandCursor: code-drawn open hand / fist textures set with
//   Cursor.SetCursor, requested per frame, normal cursor returns when nobody asks), left-drag moves the crate over the floor plane
//   (DragTo, kept on screen) with the fist cursor, and while dragging scroll / Q / E turn it (PixelConsumables.TurnInput; also when placing it; the yaw is saved in PlacedState). PixelStorageCrate.ClaimsPointer() makes PixelGrab step aside when the click belongs
//   to the crate; HandleHot blocks cube clicks. Removing it (Ended) lets everything out. Saved in PlacedState.extra
//   ('S,slot,tier,amount;'). Tip device_StorageCrate.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// A placed Storage Crate (device <c>kind</c> = StorageCrate): a sectioned container with two rows of five compartments (10 pixels). It needs no switching on and
/// never runs out. Drag old pixels over it (Pixel Grabbing) and left-click the crate: the loose pixels lying on it are stored in the next free sections (their
/// value is kept). Left-click a stored pixel to take it out again - it is handed to the mouse like a grabbed pixel. Removing the crate lets everything out.
/// NOTE: it must not define its own Update / LateUpdate (they would hide the base's); per-frame work runs in <c>OnTick</c>.
/// </summary>
public class PixelStorageCrate : PixelPlacedDevice
{
    private const int Columns = 5, Rows = 2, Sections = Columns * Rows;

    private static int hotFrame = -10;

    /// <summary>True while the mouse is over a crate (a click then belongs to the crate, not the cube).</summary>
    public static bool HandleHot => hotFrame >= Time.frameCount - 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { hotFrame = -10; }

    private class Section
    {
        public int tier = -1;          // -1 = empty
        public double amount;
        public Transform model;
    }

    private readonly Section[] sections = new Section[Sections];
    private readonly Dictionary<Rigidbody, float> ignoreUntil = new Dictionary<Rigidbody, float>();
    private float width, depth, wallHeight, cell;
    private readonly List<Renderer> handles = new List<Renderer>();
    private PixelGrab grab;
    private Rigidbody lastHeld;
    private bool dragging;
    private PixelConsumables consumables;
    private Vector3 dragOffset;

    public void Init(PixelClicker owner, Camera camera, PixelConsumables.Device settings, float shrinkTime)
    {
        InitCommon(owner, camera, null, "{0}", 1e9f, shrinkTime);
        width = Mathf.Max(1f, settings.bodyDiameter);
        cell = width / Columns;
        depth = cell * Rows;
        wallHeight = settings.bodyHeight;
        for (int i = 0; i < Sections; i++) sections[i] = new Section();
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) if (r.name == "Handle") handles.Add(r);
        Ended += _ => ReleaseAll();
    }

    private Vector3 SectionPosition(int i)
    {
        float x = -width * 0.5f + ((i % Columns) + 0.5f) * cell;
        float z = -depth * 0.5f + ((i / Columns) + 0.5f) * cell;
        return new Vector3(x, 0.07f + cell * 0.38f, z);
    }

    private int FreeCount()
    {
        int n = 0;
        foreach (Section s in sections) if (s.tier < 0) n++;
        return n;
    }

    private int NextFree()
    {
        for (int i = 0; i < Sections; i++) if (sections[i].tier < 0) return i;
        return -1;
    }

    // ------------------------------------------------------------------
    // Finding things
    // ------------------------------------------------------------------

    private List<Rigidbody> FindLoose()
    {
        List<Rigidbody> found = new List<Rigidbody>();
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic || clicker.IsFlyingPixel(body)) continue;
            if (ignoreUntil.TryGetValue(body, out float until) && Time.time < until) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].flyAway) continue;
            Vector3 local = transform.InverseTransformPoint(body.position);
            if (Mathf.Abs(local.x) > width * 0.5f + cell || Mathf.Abs(local.z) > depth * 0.5f + cell || local.y < -0.6f || local.y > 3.2f) continue;
            found.Add(body);
        }
        found.Sort((a, b) => (a.position - transform.position).sqrMagnitude.CompareTo((b.position - transform.position).sqrMagnitude));
        return found;
    }

    /// <summary>The filled section whose pixel is under the pointer (-1 = none).</summary>
    private int PickSection(Vector2 pointer)
    {
        if (cam == null) return -1;
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Sections; i++)
        {
            Section s = sections[i];
            if (s.tier < 0 || s.model == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(s.model.position);
            if (sp.z <= 0f) continue;
            Vector3 edge = cam.WorldToScreenPoint(s.model.position + cam.transform.right * cell * 0.5f);
            float radius = Mathf.Max(16f, Mathf.Abs(edge.x - sp.x));
            float d = Vector2.Distance(pointer, new Vector2(sp.x, sp.y));
            if (d <= radius && d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    /// <summary>Would a click at the pointer do something here (take a stored pixel out, or store loose ones)? Pixel Grabbing steps aside then.</summary>
    public static bool ClaimsPointer()
    {
        foreach (PixelPlacedDevice p in All)
        {
            PixelStorageCrate crate = p as PixelStorageCrate;
            if (crate != null && crate.WouldAct()) return true;
        }
        return false;
    }

    private bool WouldAct()
    {
        if (IsDying || clicker == null || cam == null) return false;
        Vector2 pointer = PixelInput.PointerPosition();
        Ray ray = cam.ScreenPointToRay(pointer);
        if (dragging || OverHandle(ray)) return true;
        if (PickSection(pointer) >= 0) return true;
        return HitTest(ray, out _) && FreeCount() > 0 && FindLoose().Count > 0;
    }

    // ------------------------------------------------------------------
    // Per frame
    // ------------------------------------------------------------------

    protected override void OnTick()
    {
        if (cam == null) cam = Camera.main;
        float spin = 35f * Time.deltaTime;
        foreach (Section s in sections) if (s.model != null) s.model.Rotate(0f, spin, 0f, Space.Self);
        if (Time.timeScale <= 0f || IsDying || cam == null) return;

        // A pixel you were holding and just let go of over the crate goes into the next free section.
        if (grab == null) grab = PixelFind.First<PixelGrab>();
        Rigidbody heldNow = grab != null ? grab.HeldBody : null;
        if (lastHeld != null && heldNow != lastHeld) TryStoreReleased(lastHeld);
        lastHeld = heldNow;
        CaptureRecentlyHeld();

        if (dragging)
        {
            if (!PixelInput.LeftHeld()) dragging = false;
            else
            {
                hotFrame = Time.frameCount;
                PixelHandCursor.Request(PixelHandCursor.Shape.Closed);
                // Scroll or Q / E turns the crate while it is being moved (the same turning as when placing it).
                if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
                float turn = consumables != null ? consumables.TurnInput() : 0f;
                if (!Mathf.Approximately(turn, 0f))
                {
                    transform.Rotate(0f, turn, 0f, Space.World);
                    dragOffset = Quaternion.Euler(0f, turn, 0f) * dragOffset;   // the grabbed point swings round the centre with it
                }
                DragTo(cam.ScreenPointToRay(PixelInput.PointerPosition()));
                return;
            }
        }
        if (PixelInput.PointerOverUI()) return;

        Vector2 pointer = PixelInput.PointerPosition();
        Ray ray = cam.ScreenPointToRay(pointer);
        if (OverHandle(ray))
        {
            hotFrame = Time.frameCount;
            PixelHandCursor.Request(PixelHandCursor.Shape.Open);
            if (PixelInput.LeftPressed() && FloorPoint(ray, out Vector3 hit))
            {
                dragging = true;
                dragOffset = transform.position - hit;
            }
            return;
        }
        int picked = PickSection(pointer);
        bool over = picked >= 0 || HitTest(ray, out _);
        if (!over) return;
        hotFrame = Time.frameCount;
        if (!PixelInput.LeftPressed()) return;

        if (picked >= 0) TakeOut(picked);
        else StoreLoose();
    }

    private bool OverHandle(Ray ray)
    {
        foreach (Renderer r in handles)
            if (r != null && r.enabled && r.bounds.IntersectRay(ray, out _)) return true;
        return false;
    }

    private bool FloorPoint(Ray ray, out Vector3 point)
    {
        point = default;
        Plane plane = new Plane(Vector3.up, transform.position);
        if (!plane.Raycast(ray, out float enter)) return false;
        point = ray.GetPoint(enter);
        return true;
    }

    private void DragTo(Ray ray)
    {
        if (!FloorPoint(ray, out Vector3 hit)) return;
        Vector3 target = hit + dragOffset;
        target.y = transform.position.y;
        Vector3 vp = cam.WorldToViewportPoint(target);
        if (vp.z <= 0f || vp.x < 0.04f || vp.x > 0.96f || vp.y < 0.1f || vp.y > 0.9f) return;   // never off the screen
        transform.position = target;
    }

    /// <summary>A held pixel was let go: if it is above the crate's top, store it.</summary>
    private void TryStoreReleased(Rigidbody body)
    {
        if (body == null || NextFree() < 0) return;
        bool listed = false;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count && !listed; i++) listed = pixels[i] == body;
        if (!listed) return;
        Vector3 local = transform.InverseTransformPoint(body.position);
        OldPixelInfo tagged = body.GetComponent<OldPixelInfo>();
        bool fromAnyHeight = tagged != null && tagged.RecentlyHeld(PixelGrab.RecentlyHeldSeconds);   // let go by the player a moment ago: dropped from the very top still counts
        if (Mathf.Abs(local.x) > width * 0.5f + cell * 0.4f || Mathf.Abs(local.z) > depth * 0.5f + cell * 0.4f || local.y < -0.3f || (!fromAnyHeight && local.y > 3.2f)) return;
        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].flyAway) return;
        StoreBody(body, info);
    }

    private float nextCapture;

    /// <summary>Old pixels tagged 'recently held by the player' that end up inside the crate's footprint (above it or on it, from any height) are stored, like a pixel let go right above it.</summary>
    private void CaptureRecentlyHeld()
    {
        if (Time.time < nextCapture || NextFree() < 0) return;
        nextCapture = Time.time + 0.1f;
        float window = PixelGrab.RecentlyHeldSeconds;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic || (grab != null && body == grab.HeldBody)) continue;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || info.heldByPlayer || !info.RecentlyHeld(window)) continue;
            if (ignoreUntil.TryGetValue(body, out float until) && Time.time < until) continue;
            if (!clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].flyAway || clicker.IsFlyingPixel(body)) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue;
            Vector3 local = transform.InverseTransformPoint(body.position);
            if (Mathf.Abs(local.x) > width * 0.5f + cell * 0.4f || Mathf.Abs(local.z) > depth * 0.5f + cell * 0.4f || local.y < -0.3f) continue;
            StoreBody(body, info);
            if (NextFree() < 0) return;
        }
    }

    private void StoreBody(Rigidbody body, OldPixelInfo info)
    {
        int slot = NextFree();
        if (slot < 0) return;
        GameObject go = body.gameObject;
        int tier = info.tierIndex;
        double amount = info.amount;
        if (!clicker.ReleaseOldPixel(body, false)) return;
        Section s = sections[slot];
        s.tier = tier;
        s.amount = amount;
        MakeModel(s, slot);
        StartCoroutine(FlyIn(go, s.model));
        PixelAudio.Play("device_place");
    }

    private void StoreLoose()
    {
        foreach (Rigidbody body in FindLoose())
        {
            if (NextFree() < 0) break;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null) StoreBody(body, info);
        }
    }

    private void MakeModel(Section s, int slot)
    {
        GameObject model = clicker.CreateDisplayPixel(s.tier, transform, cell * 0.72f);
        if (model == null) return;
        model.transform.localPosition = SectionPosition(slot);
        model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
        s.model = model.transform;
    }

    private void TakeOut(int slot)
    {
        Section s = sections[slot];
        if (s.tier < 0) return;
        Vector3 at = s.model != null ? s.model.position : transform.TransformPoint(SectionPosition(slot));
        int tier = s.tier;
        double amount = s.amount;
        if (s.model != null) Destroy(s.model.gameObject);
        s.model = null; s.tier = -1; s.amount = 0;

        if (!clicker.SpawnStoredPixel(tier, amount, at, Vector3.zero)) return;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        Rigidbody body = pixels.Count > 0 ? pixels[pixels.Count - 1] : null;
        if (body == null) return;
        ignoreUntil[body] = Time.time + 1.5f;
        PixelGrab grab = PixelFind.First<PixelGrab>();
        if (grab != null) grab.BeginHold(body);
    }

    private void ReleaseAll()
    {
        if (clicker == null) return;
        for (int i = 0; i < Sections; i++)
        {
            Section s = sections[i];
            if (s.tier < 0) continue;
            Vector3 p = transform.TransformPoint(SectionPosition(i)) + Vector3.up * 0.6f;
            Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3f), Random.Range(-1.2f, 1.2f));
            clicker.SpawnStoredPixel(s.tier, s.amount, p, v);
            if (s.model != null) Destroy(s.model.gameObject);
            s.model = null; s.tier = -1;
        }
    }

    /// <summary>The old pixel shrinks and slides into its compartment while the stored model pops up.</summary>
    private IEnumerator FlyIn(GameObject go, Transform target)
    {
        if (go == null) yield break;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (mb != null) mb.enabled = false;
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Vector3 start = go.transform.position, scale = go.transform.localScale;
        Vector3 finalScale = target != null ? target.localScale : Vector3.one;
        if (target != null) target.localScale = Vector3.zero;
        float t = 0f;
        const float Seconds = 0.28f;
        while (t < Seconds)
        {
            if (go == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Seconds));
            if (target != null) go.transform.position = Vector3.Lerp(start, target.position, k);
            go.transform.localScale = scale * (1f - k);
            if (target != null) target.localScale = finalScale * k;
            yield return null;
        }
        if (target != null) target.localScale = finalScale;
        if (go != null) Destroy(go);
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    public string ExportState()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < Sections; i++)
            if (sections[i].tier >= 0) sb.Append("S,").Append(i).Append(',').Append(sections[i].tier).Append(',').Append(sections[i].amount.ToString("R", ci)).Append(';');
        return sb.ToString();
    }

    public void ImportState(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        CultureInfo ci = CultureInfo.InvariantCulture;
        foreach (string entry in text.Split(';'))
        {
            string[] f = entry.Split(',');
            if (f.Length < 4 || f[0] != "S") continue;
            if (!int.TryParse(f[1], out int slot) || !int.TryParse(f[2], out int tier) || !double.TryParse(f[3], NumberStyles.Float, ci, out double amount)) continue;
            if (slot < 0 || slot >= Sections || sections[slot].tier >= 0 || !clicker.IsValidTierIndex(tier)) continue;
            sections[slot].tier = tier;
            sections[slot].amount = amount;
            MakeModel(sections[slot], slot);
        }
    }
}
