// ---- System notes ----
// PixelTrashCan.cs
//   TRASH CAN (a Dev Hub > Tester feature, PixelFeatureTester 'Trash Can'): a plain cylinder you put on the floor. Within suctionRadiusCubes (default 2.5 cube widths) of it every loose old pixel - things thrown out of a Funnel hose, a machine or the end of a conveyor belt,
//   or simply lying there - is pulled towards its mouth and, once it gets there, shrinks away and is DELETED (PixelClicker.ClaimOldPixel, no credit). SOCKETS (PixelSockets.cs): 'front' and 'back' (In): a belt, connector or hose ending at an In coupling there hands its husks straight in (AcceptHusk). Pixels the player holds,
//   ones riding a belt / carried by a machine (kinematic), flying pixels and attached ones are left alone. Saved with the tester save (position).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class PixelTrashCan : MonoBehaviour, IPixelSocketDevice, IPixelHuskRoom
{
    [Min(0f)]
    [Tooltip("How far from the can (in cube widths) loose old pixels are pulled in. 0 = the coded default (2.5).")]
    [SerializeField] private float suctionRadiusCubes = 0f;

    [Min(0f)]
    [Tooltip("How hard the pull is, in cube widths per second at the can's mouth. 0 = the coded default (7).")]
    [SerializeField] private float pullSpeedCubes = 0f;

    private float Radius => (suctionRadiusCubes > 0f ? suctionRadiusCubes : 2.5f) * unit;
    private float Pull => (pullSpeedCubes > 0f ? pullSpeedCubes : 7f) * unit;

    private static readonly List<PixelTrashCan> all = new List<PixelTrashCan>();
    /// <summary>Every Trash Can (a Container Hose can end at one).</summary>
    public static IReadOnlyList<PixelTrashCan> All => all;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { all.Clear(); }

    private PixelClicker clicker;
    private bool ghost;
    private float unit = 1f;
    private Transform ring;

    private float MouthY => 1.1f * unit;

    public static GameObject CreateGhost(PixelClicker c) { return Build(c, true, "Trash Can (Ghost)"); }

    public static GameObject Place(PixelClicker c, Vector3 point, float yaw)
    {
        GameObject root = Build(c, false, "Trash Can");
        root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
        PixelConveyorSystem.Ensure(c);
        PixelAudio.Play("device_place");
        return root;
    }

    private static GameObject Build(PixelClicker c, bool ghostMode, string name)
    {
        GameObject root = new GameObject(name);
        PixelTrashCan t = root.AddComponent<PixelTrashCan>();
        t.clicker = c; t.ghost = ghostMode; t.unit = Mathf.Max(0.05f, c.OldPixelWorldSize);
        // Sockets (PixelSockets.cs): two sides (front / back) where a belt, connector or hose can end at an In coupling. A ghost has none.
        if (!ghostMode)
            t.sockets = new[]
            {
                new PixelSocket("front", new Vector3(0f, 0f, -0.72f * t.unit), Vector3.back, PixelSocketFlow.Intake),
                new PixelSocket("back", new Vector3(0f, 0f, 0.72f * t.unit), Vector3.forward, PixelSocketFlow.Intake),
            };
        t.BuildModel();
        return root;
    }

    private void BuildModel()
    {
        float u = unit;
        PixelFactoryKit k = new PixelFactoryKit(clicker, ghost);
        Color steel = new Color(0.45f, 0.48f, 0.54f, 1f), dark = new Color(0.12f, 0.13f, 0.16f, 1f), teal = new Color(0.2f, 0.9f, 0.85f, 1f);
        k.Cyl("Body", transform, new Vector3(0f, 0.5f * u, 0f), 0.62f * u, 1.0f * u, steel, default, 0.7f, 0.5f, !ghost);
        k.Cyl("Band", transform, new Vector3(0f, 0.3f * u, 0f), 0.65f * u, 0.12f * u, dark, default, 0.4f, 0.4f);
        k.Cyl("Rim", transform, new Vector3(0f, 1.0f * u, 0f), 0.68f * u, 0.1f * u, dark, default, 0.6f, 0.5f);
        k.Cyl("Mouth", transform, new Vector3(0f, 1.045f * u, 0f), 0.5f * u, 0.02f * u, Color.black, default, 0f, 0.1f);
        k.Cyl("Glow", transform, new Vector3(0f, 1.06f * u, 0f), 0.52f * u, 0.008f * u, new Color(0.1f, 0.5f, 0.55f, 1f), default, 0f, 0.9f);
        // A thin teal ring on the floor showing how far the suction reaches (it pulses).
        GameObject r = k.Cyl("Reach Ring", transform, new Vector3(0f, 0.012f * u, 0f), 2.5f * u, 0.012f * u, new Color(teal.r, teal.g, teal.b, 0.35f), default, 0f, 0.9f);
        ring = r.transform;
    }

    private void OnEnable() { if (clicker == null) clicker = PixelFind.First<PixelClicker>(); PixelCouplings.Register(this); if (!all.Contains(this)) all.Add(this); }
    private void OnDisable() { PixelCouplings.Unregister(this); all.Remove(this); }

    // ---- Sockets ----
    private PixelSocket[] sockets = new PixelSocket[0];
    public Transform SocketRoot => transform;
    public IReadOnlyList<PixelSocket> Sockets => sockets;
    public Color SocketTint => new Color(0.45f, 0.48f, 0.54f, 1f);
    public int HuskRoom(PixelSocket socket) => 1000;   // (it never fills up)

    private void Update()
    {
        if (ring != null) { float s = 1f + 0.03f * Mathf.Sin(Time.time * 3f); ring.localScale = new Vector3(s * (Radius / (2.5f * unit)), 1f, s * (Radius / (2.5f * unit))); }
    }

    /// <summary>A husk comes in through an In coupling: it vanishes like one pulled into the mouth.</summary>
    public bool AcceptHusk(PixelSocket socket, Rigidbody pixel)
    {
        if (ghost || pixel == null || clicker == null || !clicker.ClaimOldPixel(pixel)) return false;
        StartCoroutine(Vanish(pixel));
        return true;
    }

    private void FixedUpdate()
    {
        if (ghost || clicker == null) return;
        Vector3 mouth = transform.position + Vector3.up * MouthY;
        IReadOnlyList<Rigidbody> pixels = clicker.OldPixels;
        float r = Radius;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = pixels[i];
            if (!PixelHuskHolder.Takeable(clicker, rb, true) || PixelAttachment.IsAttached(rb)) continue;   // (one owner per husk)
            Vector3 to = mouth - rb.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.magnitude > r || rb.position.y > mouth.y + 3f * unit) continue;
            if (flat.magnitude < 0.75f * unit && rb.position.y < mouth.y + 0.9f * unit)
            {
                if (clicker.ClaimOldPixel(rb)) StartCoroutine(Vanish(rb));
                continue;
            }
            // Pulled in: the closer, the harder (it lifts a little so it can climb over the rim).
            float k = 1f - Mathf.Clamp01(flat.magnitude / r);
            Vector3 want = (flat.normalized * (0.35f + 0.65f * k) * Pull) + Vector3.up * Mathf.Clamp(to.y, -0.5f * unit, 1.5f * unit) * 2f;
            Vector3 v = VelocityOf(rb);
            PixelPathCarrier.SetVelocity(rb, Vector3.Lerp(v, want, 0.25f + 0.35f * k));
        }
    }

    private static Vector3 VelocityOf(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private System.Collections.IEnumerator Vanish(Rigidbody rb)
    {
        if (rb == null) yield break;
        GameObject go = rb.gameObject;
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>()) if (mb != null) mb.enabled = false;
        rb.isKinematic = true;
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Vector3 start = go.transform.position, scale = go.transform.localScale;
        Vector3 end = transform.position + Vector3.up * (MouthY - 0.1f * unit);
        float t = 0f;
        while (t < 0.28f)
        {
            if (go == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
            go.transform.position = Vector3.Lerp(start, end, k);
            go.transform.localScale = scale * (1f - k);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    [System.Serializable]
    private class Save { public Vector3 position; public float yaw; }

    public static string ExportState(GameObject root)
    {
        PixelTrashCan t = root != null ? root.GetComponent<PixelTrashCan>() : null;
        return t == null ? null : JsonUtility.ToJson(new Save { position = root.transform.position, yaw = root.transform.eulerAngles.y });
    }

    public static GameObject ImportState(PixelClicker c, string json)
    {
        Save s = JsonUtility.FromJson<Save>(json);
        return s == null ? null : Place(c, s.position, s.yaw);
    }
}
