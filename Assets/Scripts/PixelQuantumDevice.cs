// ---- System notes (moved here from CLAUDE.md) ----
// PixelQuantumDevice.cs
//   (extends PixelPlacedDevice) Quantum Organizer (DeviceKind.QuantumOrganizer 12, auto-added like the fan by EnsureDefaultPotions,
//   Utilities card; needs Electric unlocked, 60 Electric, hold 3, durationSeconds 600, radius 9 = reach, boxCapacity 60 stored pixels,
//   bodyDiameter 2.4 = the pad's inner side, bodyHeight 0.5 = wall height; model PixelConsumables.BuildQuantumBody: a glowing pad
//   inside four low walls with corner posts). Placed like the Magnet, starts disarmed (click to switch on). While working, every 0.1 s
//   it scans the old pixels: a loose pixel lying on the pad binds its type (up to 3 types, MaxTypes); every loose old pixel of a bound
//   type within reach (not held / flying / despawning / kinematic, not one it just released) is taken out of the old-pixel list
//   (ReleaseOldPixel(body, false)) and dematerialized with a glitch (GlitchOut: scripts and colliders off, jittering, stretching
//   slivers, flickering, then destroyed) while a small display model of that type glitches into the box (GlitchIn, AddModel: up to
//   MaxVisible 18 models per type on a lane of the pad; the stored COUNT and total payout per type are kept in Slot). A floating label
//   shows the bound types and counts (or 'Drop a pixel on the pad'). Left-click the organizer (HandleHot blocks cube clicks via
//   ExternalClickBlock) to release ReleasePerClick (6) stored pixels as ordinary old pixels with the average stored payout
//   (SpawnStoredPixel, ignored by the organizer for 10 s); when its time runs out or it is removed (Ended) everything stored comes
//   out. Saved in PlacedState.extra (ExportState/ImportState, 'Q,tier,count,total;'). It must not define its own Update / LateUpdate:
//   per-frame work runs in OnTick. Tip device_QuantumOrganizer. Not part of the Operation tab or the UFO's placeable list yet.
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// A placed Quantum Organizer (device <c>kind</c> = QuantumOrganizer): a pad with walls around it. Once switched on, the first old pixels that come to
/// rest on the pad bind their type to it (up to three types). From then on every loose old pixel of a bound type, anywhere within its reach, is
/// dematerialized with a glitchy effect where it lies and rematerializes (glitching in) on the pad as storage. Left-click the organizer to let some of
/// the stored pixels out again (they keep their value); when its time runs out, or it is removed, everything stored is released. The countdown, timer
/// text and shrink-away come from <see cref="PixelPlacedDevice"/>; the model is built by <see cref="PixelConsumables"/>.
/// NOTE: it must not define its own Update / LateUpdate (they would hide the base's); per-frame work runs in <c>OnTick</c>.
/// </summary>
public class PixelQuantumDevice : PixelPlacedDevice
{
    private const int MaxTypes = 3;          // pixel types one organizer can hold
    private const int MaxVisible = 18;       // little pixel models shown per type (the stored count can be higher)
    private const int Columns = 6;
    private const int ReleasePerClick = 6;
    private const float GlitchOutSeconds = 0.4f;
    private const float GlitchInSeconds = 0.45f;

    private static int hotFrame = -10;

    /// <summary>True while the mouse is over a working organizer (a click then lets stored pixels out instead of clicking the cube).</summary>
    public static bool HandleHot => hotFrame >= Time.frameCount - 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { hotFrame = -10; }

    private class Slot
    {
        public int tier;
        public int count;
        public double total;
        public readonly List<Transform> models = new List<Transform>();
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly Dictionary<Rigidbody, float> ignoreUntil = new Dictionary<Rigidbody, float>();
    private float reach, half, wallHeight;
    private int capacity, stored;
    private float nextScan;
    private TextMeshPro label;
    private bool labelDirty = true;

    public void Init(PixelClicker owner, Camera camera, PixelConsumables.Device settings, TextMeshPro timer, string format, float duration, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        reach = Mathf.Max(1f, settings.radius);
        half = Mathf.Max(0.3f, settings.bodyDiameter * 0.5f);
        wallHeight = settings.bodyHeight;
        capacity = Mathf.Max(1, settings.boxCapacity);

        GameObject go = new GameObject("Contents");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, wallHeight + 0.55f, 0f);
        label = go.AddComponent<TextMeshPro>();
        label.fontSize = 2.4f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = true;
        label.color = Color.white;
        label.rectTransform.sizeDelta = new Vector2(6f, 2f);
        if (clicker.UIFont != null) label.font = clicker.UIFont;

        RefreshLabel();
        Ended += _ => ReleaseSome(1000);   // when its time is up (or it is removed) everything stored comes out again
    }

    private static float Speed(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity.magnitude;
#else
        return body.velocity.magnitude;
#endif
    }

    private Slot SlotOf(int tier)
    {
        foreach (Slot s in slots) if (s.tier == tier) return s;
        return null;
    }

    private string TierName(int tier)
    {
        string n = clicker.Tiers[tier].displayName;
        return n != null && n.EndsWith(" Pixels") ? n.Substring(0, n.Length - 7) : n;
    }

    // ------------------------------------------------------------------
    // Per frame
    // ------------------------------------------------------------------

    protected override void OnTick()
    {
        if (cam == null) cam = Camera.main;
        if (label != null && cam != null) label.transform.rotation = cam.transform.rotation;
        if (labelDirty) RefreshLabel();
        if (Time.timeScale <= 0f || IsDying) return;

        // Mouse over the organizer: a click lets stored pixels out.
        if (cam != null && !PixelInput.PointerOverUI())
        {
            Ray ray = cam.ScreenPointToRay(PixelInput.PointerPosition());
            if (HitTest(ray, out _))
            {
                hotFrame = Time.frameCount;
                if (PixelInput.LeftPressed()) ReleaseSome(ReleasePerClick);
            }
        }

        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.1f;
            Scan();
        }
    }

    private void Scan()
    {
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        int captured = 0;
        for (int i = pixels.Count - 1; i >= 0 && captured < 4; i--)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic || clicker.IsFlyingPixel(body)) continue;
            if (ignoreUntil.TryGetValue(body, out float until) && Time.time < until) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.Held || despawn.IsDespawning)) continue;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].flyAway) continue;

            Slot slot = SlotOf(info.tierIndex);
            if (slot == null)
            {
                // A pixel lying on the pad binds its type.
                Vector3 local = transform.InverseTransformPoint(body.position);
                bool inside = Mathf.Abs(local.x) < half && Mathf.Abs(local.z) < half && local.y < wallHeight + 1f;
                if (!inside || slots.Count >= MaxTypes || Speed(body) > 2f) continue;
                slot = new Slot { tier = info.tierIndex };
                slots.Add(slot);
                labelDirty = true;
            }
            if (stored >= capacity) continue;
            if ((body.position - transform.position).sqrMagnitude > reach * reach) continue;

            Capture(body, info, slot);
            captured++;
        }
        if (ignoreUntil.Count > 60) ignoreUntil.Clear();
    }

    private void Capture(Rigidbody body, OldPixelInfo info, Slot slot)
    {
        GameObject go = body.gameObject;
        double amount = info.amount;
        if (!clicker.ReleaseOldPixel(body, false)) return;   // out of the old-pixel list: nothing else touches it
        slot.count++;
        slot.total += amount;
        stored++;
        labelDirty = true;
        StartCoroutine(GlitchOut(go));
        AddModel(slot, true);
    }

    // ------------------------------------------------------------------
    // The stored pixels
    // ------------------------------------------------------------------

    private Vector3 ModelPosition(int lane, int index, float cell)
    {
        float laneWidth = half * 2f / MaxTypes;
        float x = -half + ((index % Columns) + 0.5f) * cell;
        float z = -half + (lane + 0.5f) * laneWidth;
        float y = 0.09f + (index / Columns) * cell * 0.9f + cell * 0.4f;
        return new Vector3(x, y, z);
    }

    private void AddModel(Slot slot, bool glitch)
    {
        if (slot.models.Count >= MaxVisible || slot.models.Count >= slot.count) return;
        int lane = slots.IndexOf(slot);
        float cell = half * 2f / Columns;
        GameObject model = clicker.CreateDisplayPixel(slot.tier, transform, cell * 0.8f);
        if (model == null) return;
        model.transform.localPosition = ModelPosition(lane, slot.models.Count, cell);
        model.transform.localRotation = Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f);
        slot.models.Add(model.transform);
        if (glitch) StartCoroutine(GlitchIn(model.transform));
    }

    private void RefreshLabel()
    {
        labelDirty = false;
        if (label == null) return;
        if (slots.Count == 0) { label.text = "<alpha=#CC>Drop a pixel on the pad\nto bind its type"; return; }
        StringBuilder sb = new StringBuilder();
        foreach (Slot s in slots)
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(clicker.Tiers[s.tier].color)).Append('>').Append(TierName(s.tier)).Append("</color> x")
              .Append(s.count).Append('\n');
        sb.Append("<alpha=#99>").Append(stored).Append(" / ").Append(capacity);
        label.text = sb.ToString();
    }

    /// <summary>Lets up to 'count' stored pixels out (they keep their stored value and drop onto the floor round the pad).</summary>
    private void ReleaseSome(int count)
    {
        if (clicker == null || stored <= 0) return;
        int released = 0;
        foreach (Slot s in slots)
        {
            while (s.count > 0 && released < count && released < 120)
            {
                double each = s.total / s.count;
                s.count--;
                s.total -= each;
                stored--;
                released++;
                if (s.models.Count > s.count && s.models.Count > 0)
                {
                    Transform last = s.models[s.models.Count - 1];
                    s.models.RemoveAt(s.models.Count - 1);
                    if (last != null) { last.SetParent(null, true); StartCoroutine(GlitchOut(last.gameObject)); }
                }

                Vector3 p = transform.TransformPoint(new Vector3(Random.Range(-half * 0.6f, half * 0.6f), wallHeight + 0.9f, Random.Range(-half * 0.6f, half * 0.6f)));
                Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3f), Random.Range(-1.2f, 1.2f));
                if (clicker.SpawnStoredPixel(s.tier, each, p, v))
                {
                    IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
                    if (pixels.Count > 0 && pixels[pixels.Count - 1] != null) ignoreUntil[pixels[pixels.Count - 1]] = Time.time + 10f;   // not straight back in
                }
            }
            if (released >= count) break;
        }
        if (released > 0) labelDirty = true;
    }

    // ------------------------------------------------------------------
    // The glitch effects
    // ------------------------------------------------------------------

    /// <summary>The old pixel stutters, stretches into slivers and flickers out where it lay.</summary>
    private IEnumerator GlitchOut(GameObject go)
    {
        if (go == null) yield break;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (mb != null) mb.enabled = false;   // nothing else may move or remove it now
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        Vector3 start = go.transform.position, scale = go.transform.localScale;

        float t = 0f;
        while (t < GlitchOutSeconds)
        {
            if (go == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / GlitchOutSeconds);
            go.transform.position = start + Random.insideUnitSphere * 0.14f * k;
            go.transform.localScale = new Vector3(scale.x * Random.Range(0.4f, 1.8f), scale.y * Random.Range(0.08f, 1.5f), scale.z * Random.Range(0.4f, 1.8f)) * (1f - k * 0.55f);
            bool on = Random.value > 0.2f + 0.6f * k;
            foreach (Renderer r in renderers) if (r != null) r.enabled = on;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>A stored pixel's model glitches into being: stretched, jittery and flickering, settling into its normal shape.</summary>
    private IEnumerator GlitchIn(Transform model)
    {
        if (model == null) yield break;
        Vector3 finalScale = model.localScale, finalPos = model.localPosition;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        float t = 0f;
        while (t < GlitchInSeconds)
        {
            if (model == null) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / GlitchInSeconds);
            float inv = 1f - k;
            model.localScale = new Vector3(finalScale.x * Mathf.Lerp(Random.Range(0.3f, 1.8f), 1f, k), finalScale.y * Mathf.Lerp(Random.Range(0.1f, 2.4f), 1f, k), finalScale.z * Mathf.Lerp(Random.Range(0.3f, 1.8f), 1f, k));
            model.localPosition = finalPos + Random.insideUnitSphere * 0.1f * inv;
            bool on = Random.value < 0.35f + 0.65f * k;
            foreach (Renderer r in renderers) if (r != null) r.enabled = on;
            yield return null;
        }
        if (model == null) yield break;
        model.localScale = finalScale;
        model.localPosition = finalPos;
        foreach (Renderer r in renderers) if (r != null) r.enabled = true;
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>The bound types and what is stored, as text (kept in the save file).</summary>
    public string ExportState()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        StringBuilder sb = new StringBuilder();
        foreach (Slot s in slots) sb.Append("Q,").Append(s.tier).Append(',').Append(s.count).Append(',').Append(s.total.ToString("R", ci)).Append(';');
        return sb.ToString();
    }

    public void ImportState(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        CultureInfo ci = CultureInfo.InvariantCulture;
        foreach (string entry in text.Split(';'))
        {
            string[] f = entry.Split(',');
            if (f.Length < 4 || f[0] != "Q") continue;
            if (!int.TryParse(f[1], out int tier) || !int.TryParse(f[2], out int count) || !double.TryParse(f[3], NumberStyles.Float, ci, out double sum)) continue;
            if (!clicker.IsValidTierIndex(tier) || slots.Count >= MaxTypes || SlotOf(tier) != null) continue;
            Slot s = new Slot { tier = tier, count = Mathf.Max(0, count), total = sum };
            slots.Add(s);
            stored += s.count;
            for (int k = 0; k < Mathf.Min(s.count, MaxVisible); k++) AddModel(s, false);
        }
        labelDirty = true;
    }
}
