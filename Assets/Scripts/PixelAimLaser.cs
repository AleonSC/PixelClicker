// ---- System notes ----
// PixelAimLaser.cs
//   AIM LASERS (2026-10-20, Devices session): a see-through RED beam out of a device's coupling holes while you PLACE it (its ghost) or CARRY it by its
//   handles, so free placement lines up exactly: the beam runs far past the whole floor (maxLengthHusks) and is CUT OFF by the first thing in its way,
//   where a bright red DOT shows precisely what it lines up with. Husks and the device itself never stop a beam.
//   - One manager (this component, made on demand) draws every beam asked for this frame in LateUpdate from pooled LineRenderers + dots, so nothing is
//     built per frame and unused beams just hide.
//   - Asking for beams (every frame you want them):
//       Ports(root, sockets)          - one beam out of each socket's coupling hole (port + half a husk up, along its facing). PixelCarryHandles calls it
//                                       while a device is carried.
//       Attach(ghost, sockets)        - gives a placing ghost a PixelAimLaserPorts component that calls Ports every frame while the ghost is shown.
//                                       Each device's CreateGhost does this with its own Ports(...) layout (the same sockets the real device gets).
//       Beam(origin, dir, width, ignore) - ONE beam by hand (world space; width in world units, < 0 = the default): for later uses such as lining up
//                                       a Junction Box's top opening (a wider beam).
//   Colours, widths and length are Inspector settings on the manager ("Aim Lasers" object).
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public sealed class PixelAimLaser : MonoBehaviour
{
    [Tooltip("Colour of the beam (keep it see-through).")]
    [SerializeField] private Color beamColor = new Color(1f, 0.12f, 0.08f, 0.35f);
    [Tooltip("Colour of the dot where a beam is cut off.")]
    [SerializeField] private Color dotColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Min(0.005f)]
    [Tooltip("Default beam width, in husk widths.")]
    [SerializeField] private float beamWidthHusks = 0.07f;
    [Min(0.01f)]
    [Tooltip("Size of the dot, in husk widths (a wider beam gets a bigger dot).")]
    [SerializeField] private float dotSizeHusks = 0.22f;
    [Min(1f)]
    [Tooltip("How far a beam reaches when nothing is in its way, in husk widths (well past the whole floor).")]
    [SerializeField] private float maxLengthHusks = 600f;
    [Tooltip("How high above a socket's port its coupling hole is, in husk widths (the beam starts there).")]
    [SerializeField] private float holeHeightHusks = 0.5f;

    private struct Request { public Vector3 origin, dir; public float width; public Transform ignore; }

    private static PixelAimLaser instance;
    private static readonly List<Request> requests = new List<Request>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; requests.Clear(); }

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<Transform> dots = new List<Transform>();
    private Material beamMaterial, dotMaterial;
    private static readonly RaycastHit[] hits = new RaycastHit[64];

    private static float Unit()
    {
        PixelClicker c = PixelFind.Cached<PixelClicker>();
        return c != null && c.OldPixelWorldSize > 0f ? c.OldPixelWorldSize : 0.5f;
    }

    private static PixelAimLaser Ensure()
    {
        if (instance == null) instance = new GameObject("Aim Lasers").AddComponent<PixelAimLaser>();
        return instance;
    }

    // ---------------- asking for beams (every frame) ----------------

    /// <summary>One beam by hand this frame (world space). 'width' in world units (&lt; 0 = the default); 'ignore' = a root whose parts never stop it.</summary>
    public static void Beam(Vector3 origin, Vector3 direction, float width = -1f, Transform ignore = null)
    {
        if (direction.sqrMagnitude < 1e-6f) return;
        Ensure();
        requests.Add(new Request { origin = origin, dir = direction.normalized, width = width, ignore = ignore });
    }

    /// <summary>A beam out of every socket's coupling hole this frame (the device's root and its sockets; the device itself never stops them).</summary>
    public static void Ports(Transform root, IReadOnlyList<PixelSocket> sockets)
    {
        if (root == null || sockets == null || sockets.Count == 0) return;
        PixelAimLaser m = Ensure();
        float lift = m.holeHeightHusks * Unit() * root.lossyScale.y;
        foreach (PixelSocket s in sockets)
        {
            if (s == null || s.kind != PixelSocketKind.Husk) continue;
            Vector3 dir = root.TransformDirection(s.localFacing);
            dir.y = 0f;
            Beam(root.TransformPoint(s.localPosition) + Vector3.up * lift, dir, -1f, root);
        }
    }

    /// <summary>Gives a placing ghost beams out of these sockets (its own layout) for as long as the ghost is shown.</summary>
    public static GameObject Attach(GameObject ghost, PixelSocket[] sockets)
    {
        if (ghost == null || sockets == null || sockets.Length == 0) return ghost;
        PixelAimLaserPorts p = ghost.GetComponent<PixelAimLaserPorts>();
        if (p == null) p = ghost.AddComponent<PixelAimLaserPorts>();
        p.sockets = sockets;
        return ghost;
    }

    // ---------------- drawing ----------------

    private void LateUpdate()
    {
        float u = Unit();
        int used = 0;
        foreach (Request r in requests)
        {
            float width = r.width > 0f ? r.width : beamWidthHusks * u;
            float reach = maxLengthHusks * u;
            float length = reach;
            bool cut = false;
            int n = Physics.RaycastNonAlloc(r.origin, r.dir, hits, reach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.distance < 0.02f || h.distance >= length) continue;
                if (r.ignore != null && h.transform.IsChildOf(r.ignore)) continue;   // (the device itself)
                if (h.collider.GetComponentInParent<OldPixelInfo>() != null) continue; // (husks lying about don't count)
                length = h.distance;
                cut = true;
            }
            LineRenderer line = Line(used);
            line.widthMultiplier = width;
            line.SetPosition(0, r.origin);
            line.SetPosition(1, r.origin + r.dir * length);
            if (!line.gameObject.activeSelf) line.gameObject.SetActive(true);
            Transform dot = Dot(used);
            if (dot.gameObject.activeSelf != cut) dot.gameObject.SetActive(cut);
            if (cut)
            {
                dot.position = r.origin + r.dir * length - r.dir * 0.01f;
                dot.localScale = Vector3.one * Mathf.Max(dotSizeHusks * u, width * 1.6f);
            }
            used++;
        }
        requests.Clear();
        for (int i = used; i < lines.Count; i++) if (lines[i].gameObject.activeSelf) lines[i].gameObject.SetActive(false);
        for (int i = used; i < dots.Count; i++) if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
    }

    private LineRenderer Line(int i)
    {
        while (lines.Count <= i)
        {
            if (beamMaterial == null) beamMaterial = NewMaterial(beamColor);
            GameObject go = new GameObject("Beam", typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = beamMaterial;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startColor = lr.endColor = beamColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lines.Add(lr);
        }
        return lines[i];
    }

    private Transform Dot(int i)
    {
        while (dots.Count <= i)
        {
            if (dotMaterial == null) dotMaterial = NewMaterial(dotColor);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Beam Dot";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = dotMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            dots.Add(go.transform);
        }
        return dots[i];
    }

    private static Material NewMaterial(Color c)
    {
        Shader s = Shader.Find("Sprites/Default");
        Material m = new Material(s != null ? s : Shader.Find("Unlit/Color"));
        m.color = c;
        return m;
    }

    private void OnDestroy()
    {
        if (beamMaterial != null) Destroy(beamMaterial);
        if (dotMaterial != null) Destroy(dotMaterial);
        if (instance == this) instance = null;
    }
}
