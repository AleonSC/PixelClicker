// ---- System notes ----
// PixelSolarPanel.cs
//   The SOLAR PANEL (a Dev Hub > Tester feature, PixelFeatureTester 'Solar Panel'): a flat panel of blue cells on the ground with an indicator post. It is POWERED while Solar old pixels
//   (PixelType.Solar) lie on it (its pad, a little bigger than the panel). It links to ONE placed device (PixelPlacedDevice: vacuum device, fan, sorter...): right after placing, the
//   tester shows a ring of linkRadiusCubes (0 = 8) cube widths round the panel and you click a device inside it to connect; a right click on a placed panel (not while placing) starts the
//   linking again. A straight BLUE LINE (with a bead running along it while powered) joins the panel to the device; while the panel is powered it calls the device's HoldPower() every frame,
//   so the device's countdown stops. SOLAR USE: a Solar pixel on the pad that is NOT part of a complete group (5+ touching, the clicker's UpdateSolarClusters keeps those alive:
//   OldPixelDespawn.IsKept) is slowly USED UP - one every consumeSeconds (0 = 8) - while a group of 5 on the pad is never used and powers the panel for good. The indicator is blue
//   (powered), blue-white (powered by a complete group, lasts) or dim red (no solar). Saved with the Tester objects (PixelFeatureTester Export / Import: position, turn, link position).
// ---------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelSolarPanel : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("How far (in cube widths) from the panel a device can be linked. 0 = the coded default (8).")]
    [SerializeField] private float linkRadiusCubes = 0f;

    [Min(0f)]
    [Tooltip("Seconds between two Solar pixels being used up (only Solar pixels that are not in a complete group of 5). 0 = the coded default (8).")]
    [SerializeField] private float consumeSeconds = 0f;

    [Tooltip("Colour of the link line.")]
    [SerializeField] private Color lineColor = new Color(0.25f, 0.65f, 1f, 1f);

    private PixelClicker clicker;
    private float unit;
    private PixelPlacedDevice linked;
    private LineRenderer line;
    private Material lineMat;
    private Transform bead;
    private Renderer lamp;
    private Material lampMat;
    private GameObject rangeRing;
    private bool powered, lasting;
    private float scanTimer, consumeTimer;
    private readonly List<Rigidbody> scratch = new List<Rigidbody>();

    private static readonly List<PixelSolarPanel> all = new List<PixelSolarPanel>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    public float LinkRadius => unit * (linkRadiusCubes > 0f ? linkRadiusCubes : 8f);
    public PixelPlacedDevice Linked => linked;
    public bool Powered => powered;

    // Panel size in cube widths (half extents of the pad on the ground).
    private const float HalfX = 1.7f, HalfZ = 1.2f, PadMargin = 0.6f;

    // ---------------- placement (PixelFeatureTester) ----------------

    public static GameObject CreateGhost(PixelClicker c) { GameObject g = Build(c, true); g.name = "Solar Panel (Ghost)"; return g; }
    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false);
        root.name = "Solar Panel";
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghost)
    {
        float u = c.OldPixelWorldSize;
        PixelFactoryKit k = new PixelFactoryKit(c, ghost);
        GameObject root = new GameObject("Solar Panel");
        Transform t = root.transform;
        Color frame = new Color(0.55f, 0.58f, 0.64f, 1f), cell = new Color(0.08f, 0.18f, 0.55f, 1f), line = new Color(0.8f, 0.85f, 0.95f, 1f), dark = new Color(0.12f, 0.13f, 0.17f, 1f);
        k.Box("Plate", t, new Vector3(0f, 0.08f * u, 0f), new Vector3(HalfX * 2f * u, 0.16f * u, HalfZ * 2f * u), dark, default, true, 0.3f, 0.3f);
        k.Box("Frame", t, new Vector3(0f, 0.17f * u, 0f), new Vector3((HalfX * 2f - 0.1f) * u, 0.04f * u, (HalfZ * 2f - 0.1f) * u), frame, default, false, 0.8f, 0.6f);
        // A 4 x 3 grid of blue cells with thin dividers.
        const int cols = 4, rowsN = 3;
        float cw = (HalfX * 2f - 0.3f) / cols, cd = (HalfZ * 2f - 0.3f) / rowsN;
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rowsN; j++)
                k.Box("Cell", t, new Vector3((-HalfX + 0.15f + cw * (i + 0.5f)) * u, 0.2f * u, (-HalfZ + 0.15f + cd * (j + 0.5f)) * u), new Vector3((cw - 0.07f) * u, 0.03f * u, (cd - 0.07f) * u), cell, default, false, 0.85f, 0.9f);
        for (int i = 1; i < cols; i++) k.Box("Divider", t, new Vector3((-HalfX + 0.15f + cw * i) * u, 0.215f * u, 0f), new Vector3(0.025f * u, 0.015f * u, (HalfZ * 2f - 0.3f) * u), line, default, false, 0.6f, 0.5f);
        for (int j = 1; j < rowsN; j++) k.Box("Divider", t, new Vector3(0f, 0.215f * u, (-HalfZ + 0.15f + cd * j) * u), new Vector3((HalfX * 2f - 0.3f) * u, 0.015f * u, 0.025f * u), line, default, false, 0.6f, 0.5f);
        // The indicator post at the back-right corner.
        k.Cyl("Post", t, new Vector3((HalfX - 0.1f) * u, 0.55f * u, (HalfZ - 0.1f) * u), 0.07f * u, 0.8f * u, frame, default, 0.7f, 0.5f);
        GameObject lampGo = k.Ball("Lamp", t, new Vector3((HalfX - 0.1f) * u, 1.0f * u, (HalfZ - 0.1f) * u), 0.14f * u, new Color(0.5f, 0.1f, 0.1f, 1f), 0.1f, 0.8f);
        lampGo.name = "Lamp";
        if (ghost) return root;

        PixelSolarPanel panel = root.AddComponent<PixelSolarPanel>();
        panel.clicker = c;
        panel.unit = u;
        panel.lamp = lampGo.GetComponent<Renderer>();
        panel.lampMat = panel.lamp.material;   // its own instance (it changes colour)
        return root;
    }

    private void OnEnable() { all.Add(this); }
    private void OnDisable() { all.Remove(this); }

    // ---------------- the link ----------------

    public Vector3 LampPoint => transform.TransformPoint(new Vector3((HalfX - 0.1f) * unit, 1.0f * unit, (HalfZ - 0.1f) * unit));

    /// <summary>Connects the panel to one device (replacing an earlier link). Null cuts the link.</summary>
    public void LinkTo(PixelPlacedDevice device)
    {
        linked = device;
        EnsureLine();
        PixelAudio.Play("device_place", 1.2f);
    }

    /// <summary>Shows / hides the ring that marks how far a device can be linked (while the tester waits for you to click one).</summary>
    public void ShowRange(bool show)
    {
        if (!show) { if (rangeRing != null) rangeRing.SetActive(false); return; }
        if (rangeRing == null)
        {
            rangeRing = new GameObject("Link Range");
            rangeRing.transform.SetParent(transform, false);
            LineRenderer lr = rangeRing.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 72;
            float r = LinkRadius / Mathf.Max(0.0001f, transform.lossyScale.x);
            for (int i = 0; i < 72; i++) { float a = i * Mathf.PI * 2f / 72f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r)); }
            lr.startWidth = lr.endWidth = 0.06f * unit;
            lr.sharedMaterial = new Material(PixelShaders.SpriteDefault());
            lr.startColor = lr.endColor = new Color(0.3f, 0.7f, 1f, 0.7f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        rangeRing.SetActive(true);
    }

    private void EnsureLine()
    {
        if (line != null) return;
        GameObject go = new GameObject("Link Line");
        go.transform.SetParent(transform, false);
        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        lineMat = new Material(PixelShaders.SpriteDefault());
        line.sharedMaterial = lineMat;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(b.GetComponent<Collider>());
        b.name = "Bead";
        b.transform.SetParent(go.transform, false);
        b.GetComponent<Renderer>().sharedMaterial = lineMat;
        bead = b.transform;
    }

    // ---------------- behaviour ----------------

    private bool InPad(Vector3 world)
    {
        Vector3 l = transform.InverseTransformPoint(world) / Mathf.Max(0.0001f, unit);
        return Mathf.Abs(l.x) < HalfX + PadMargin && Mathf.Abs(l.z) < HalfZ + PadMargin && l.y > -0.5f && l.y < 2.5f;
    }

    private void Update()
    {
        if (clicker == null) return;
        float dt = Time.deltaTime;
        scanTimer -= dt;
        if (scanTimer <= 0f) { scanTimer = 0.2f; Scan(); }

        if (powered && linked != null && !linked.IsRemoving) linked.HoldPower();

        // The indicator lamp and the link line.
        Color lampColor = powered ? (lasting ? new Color(0.7f, 0.95f, 1f, 1f) : new Color(0.25f, 0.65f, 1f, 1f)) : new Color(0.5f, 0.1f, 0.1f, 1f);
        if (lampMat != null)
        {
            lampMat.color = lampColor;
            if (lampMat.HasProperty("_BaseColor")) lampMat.SetColor("_BaseColor", lampColor);
            if (lampMat.HasProperty("_EmissionColor")) { if (powered) { lampMat.EnableKeyword("_EMISSION"); lampMat.SetColor("_EmissionColor", lampColor * 1.6f); } else lampMat.SetColor("_EmissionColor", Color.black); }
        }
        if (line != null)
        {
            bool show = linked != null && !linked.IsRemoving;
            line.gameObject.SetActive(show);
            if (show)
            {
                Vector3 a = LampPoint, b = linked.transform.position + Vector3.up * unit * 0.6f;
                line.SetPosition(0, a); line.SetPosition(1, b);
                float w = unit * (powered ? 0.07f : 0.035f);
                line.startWidth = line.endWidth = w;
                Color c = lineColor; c.a = powered ? 0.85f + 0.15f * Mathf.Sin(Time.time * 6f) : 0.3f;
                line.startColor = line.endColor = c;
                if (bead != null)
                {
                    bead.gameObject.SetActive(powered);
                    bead.position = Vector3.Lerp(a, b, Mathf.Repeat(Time.time * 0.7f, 1f));
                    bead.localScale = Vector3.one * unit * 0.16f;
                }
            }
        }
        // A linked device that vanished cuts the link.
        if (linked == null && line != null && line.gameObject.activeSelf) line.gameObject.SetActive(false);
    }

    private void Scan()
    {
        int solar = clicker.IndexOf(PixelClicker.PixelType.Solar);
        int count = 0, keptCount = 0;
        scratch.Clear();   // the Solar pixels on the pad that are not part of a complete group (these get used up)
        if (solar >= 0)
        {
            var list = clicker.OldPixels;
            for (int i = 0; i < list.Count; i++)
            {
                Rigidbody rb = list[i];
                if (rb == null) continue;
                OldPixelInfo info = PixelHuskQuery.Get(rb).info;
                if (info == null || info.tierIndex != solar || !InPad(rb.worldCenterOfMass)) continue;
                OldPixelDespawn d = PixelHuskQuery.Get(rb).despawn;
                if (d != null && d.IsDespawning) continue;
                count++;
                if (d != null && d.IsKept) keptCount++; else scratch.Add(rb);
            }
        }
        powered = count > 0;
        lasting = keptCount >= 5 || (keptCount > 0 && scratch.Count == 0);
        // Slowly used up: one stray Solar pixel every consumeSeconds while the panel is powered (a complete group is never used).
        if (powered && scratch.Count > 0)
        {
            consumeTimer += 0.2f;
            if (consumeTimer >= (consumeSeconds > 0f ? consumeSeconds : 8f))
            {
                consumeTimer = 0f;
                UseUp(scratch[0]);
            }
        }
        else consumeTimer = 0f;
    }

    private void UseUp(Rigidbody rb)
    {
        if (rb == null || !clicker.ClaimOldPixel(rb)) return;
        StartCoroutine(Fade(rb.gameObject));
        PixelAudio.Play("pixel_land", 1.6f, 0.4f);
    }

    private IEnumerator Fade(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        OldPixelDespawn d = go.GetComponent<OldPixelDespawn>();
        if (d != null) d.enabled = false;
        Transform t = go != null ? go.transform : null;
        Vector3 start = t != null ? t.localScale : Vector3.one;
        Vector3 p0 = t != null ? t.position : Vector3.zero;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.8f)
        {
            if (t == null) yield break;
            t.localScale = start * Mathf.Lerp(1f, 0.05f, k);
            t.position = p0 + Vector3.up * unit * 0.4f * k;
            t.Rotate(0f, 300f * Time.deltaTime, 0f, Space.World);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ---------------- right click on a placed panel: link again ----------------

    private readonly PixelRightTap relinkTap = new PixelRightTap();   // (a right HOLD on a tester object deletes it, so only a quick click re-links)

    private void LateUpdate()
    {
        if (clicker == null || !(PixelInput.RightPressed() || PixelInput.RightHeld() || relinkTap.Pending)) return;
        bool usable = !PixelClicker.ExternalClickBlock && !PixelPauseMenu.IsPaused && !PixelInput.PointerOverUI() && !PixelFirstPerson.Active;
        if (relinkTap.Update(usable && PointerOnThis() ? (object)this : null)) PixelFeatureTester.BeginLink(gameObject);
    }

    /// <summary>Is the pointer on this panel (nothing nearer in the way: the cube, an old pixel)?</summary>
    private bool PointerOnThis()
    {
        Camera cam = clicker.ViewCamera;
        if (cam == null) return false;
        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(PixelInput.PointerPosition()), 1000f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (clicker.PixelTransform != null && h.transform.IsChildOf(clicker.PixelTransform)) return false;
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null) return false;
            if (h.collider.GetComponentInParent<PixelSolarPanel>() == this) return true;
        }
        return false;
    }

    // ---------------- saving (PixelFeatureTester) ----------------

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; public bool hasLink; public Vector3 linkPosition; }

    public static string ExportState(GameObject root)
    {
        PixelSolarPanel p = root != null ? root.GetComponent<PixelSolarPanel>() : null;
        if (p == null) return null;
        Save s = new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y };
        if (p.linked != null && !p.linked.IsRemoving) { s.hasLink = true; s.linkPosition = p.linked.transform.position; }
        return JsonUtility.ToJson(s);
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        if (s == null) return null;
        GameObject root = Place(c, s.position, s.yaw);
        PixelSolarPanel p = root.GetComponent<PixelSolarPanel>();
        if (p != null && s.hasLink)
        {
            PixelPlacedDevice best = null;
            float bestD = (c.OldPixelWorldSize * 0.8f) * (c.OldPixelWorldSize * 0.8f);
            foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
            {
                if (d == null || d.IsRemoving) continue;
                float dd = (d.transform.position - s.linkPosition).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = d; }
            }
            if (best != null) p.LinkTo(best);
        }
        return root;
    }

    /// <summary>The placed device whose body the ray hits within the panel's link radius (nearest first), or null.</summary>
    public static PixelPlacedDevice DeviceUnderRay(GameObject panelRoot, Ray ray)
    {
        PixelSolarPanel p = panelRoot != null ? panelRoot.GetComponent<PixelSolarPanel>() : null;
        if (p == null) return null;
        PixelPlacedDevice best = null;
        float bestDist = float.MaxValue;
        foreach (PixelPlacedDevice d in PixelPlacedDevice.All)
        {
            if (d == null || d.IsRemoving || d.IsRemoving) continue;
            if (Vector3.Distance(d.transform.position, panelRoot.transform.position) > p.LinkRadius) continue;
            if (d.HitTest(ray, out float dist) && dist < bestDist) { bestDist = dist; best = d; }
        }
        return best;
    }
}
