// ---- System notes (moved here from CLAUDE.md) ----
// PixelViewBounds.cs
//   Keeps old pixels inside the camera's view with no scene objects: each FixedUpdate builds planes through the camera along the
//   left/right/top/bottom screen edges (top/bottom inside the black bars) plus a near limit; a pixel past a plane is pushed back and
//   its velocity reflected (bounciness, friction). The border follows each pixel's real size: the distance to a plane is compared with
//   the pixel's reach towards it (box support function: the sum of |normal . axis| x half size along the pixel's own axes, from its
//   BoxCollider and lossyScale), plus extraEdgeMargin (default 0), so a pixel's outer surface - not its centre - meets the screen edge
//   (the old fixed 0.15 margin let big pixels poke off screen and kept small ones too far in). Skips kinematic pixels and fly-away
//   tiers (Meteor). CIRCLE MODE (circleAroundFloor, default on): instead of the four screen-edge planes pixels are kept inside a vertical cylinder round the floor's middle whose radius is where
//   the floor fades out (PixelFloor.TryGetEdgeCircle), by the pixel's own reach; the near-camera limit stays. Added by PixelClicker.Awake if missing. (An earlier version with big invisible wall slabs was reverted: the slabs
//   tinted the screen and acted as a false floor.)
// ---------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps old pixels inside what the camera can see. There are no walls or any objects in the scene: every physics
/// step, any old pixel that has crossed the left, right, top or bottom edge of the view (or got too close to the
/// camera) is pushed back inside and its speed is reflected, so it bounces off the edge of the screen.
///
/// The edges follow the camera's real view (including perspective) and can stay inside the black bars. Pixels that are
/// meant to fly away (Meteor) are left alone, and so are pixels being sucked in by a black hole or vacuum.
///
/// Added automatically by PixelClicker. Add it to any GameObject yourself to change its settings in the Inspector.
/// </summary>
public class PixelViewBounds : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker whose old pixels are kept in view. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Bounds")]
    [Tooltip("Turn the screen-edge limit on or off.")]
    [SerializeField] private bool boundsActive = true;

    [Tooltip("Keep old pixels inside a CIRCLE round the edge of the floor (where the floor fades out) instead of the four screen edges. Falls back to the screen edges while there is no floor.")]
    [SerializeField] private bool circleAroundFloor = true;

    [Tooltip("Keep the left and right edges.")]
    [SerializeField] private bool sideEdges = true;

    [Tooltip("Keep the top edge.")]
    [SerializeField] private bool topEdge = true;

    [Tooltip("Keep the bottom edge (the floor normally does this already).")]
    [SerializeField] private bool bottomEdge = true;

    [Tooltip("Put the top and bottom edges at the inner edge of the black bars instead of the very edge of the screen.")]
    [SerializeField] private bool insideBlackBars = true;

    [Range(0f, 0.3f)]
    [Tooltip("Moves every edge further in, as a fraction of the screen (0 = the very edge).")]
    [SerializeField] private float inset = 0f;

    [Min(0f)]
    [Tooltip("Extra room (world units) on top of the pixel's own size between it and an edge. The border is worked out from each pixel's real size and rotation, so a pixel's outer edge touches the screen edge exactly with 0 here.")]
    [SerializeField] private float extraEdgeMargin = 0f;

    [Min(0f)]
    [Tooltip("Pixels can't come closer to the camera than this (world units). 0 = no limit.")]
    [SerializeField] private float nearLimit = 1.5f;

    [Header("Surface")]
    [Range(0f, 1f)]
    [Tooltip("How bouncy the screen edges are (0 = pixels stop dead, 1 = perfect bounce).")]
    [SerializeField] private float bounciness = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("How much sideways speed a pixel loses when it hits an edge.")]
    [SerializeField] private float friction = 0.1f;

    private readonly Plane[] planes = new Plane[5];
    private readonly bool[] used = new bool[5];

    // Planes only change when the camera or the bars do; infos are looked up once per old pixel.
    private struct Entry { public OldPixelInfo info; public BoxCollider box; public bool ignores; }   // ignores = its look ignores the view bounds (worked out once per husk)
    private readonly Dictionary<Rigidbody, Entry> infoCache = new Dictionary<Rigidbody, Entry>();

    private void OnDestroy() => PixelHuskPool.HuskReleasing -= OnHuskReleasing;
    // (PixelHuskPool: the husk is going away and may be reused - let go of it)
    private void OnHuskReleasing(Rigidbody body) => infoCache.Remove(body);
    private Vector3 planesPosition, planesForward;
    private float planesFov = -1f, planesAspect, planesBars = -1f, planesOrtho;
    private bool planesIsOrtho;
    private int planesWidth, planesHeight;

    private void Awake()
    {
        PixelHuskPool.HuskReleasing += OnHuskReleasing;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelViewBounds: no PixelClicker found in the scene.", this);
            enabled = false;
        }
    }

    private void OnValidate() { planesFov = -1f; } // settings changed in the Inspector: rebuild the planes

    private void FixedUpdate()
    {
        if (!boundsActive) return;
        if (PixelCameraIntro.Moving) return; // the close-up view is tiny: don't squeeze loaded / resting pixels into it

        Camera cam = clicker.ViewCamera;
        if (PixelOrbitCamera.HomeCamera != null) cam = PixelOrbitCamera.HomeCamera;   // orbiting: pixels stay inside the NORMAL view
        if (cam == null) return;
        float barNow = PixelHud.Instance != null ? PixelHud.Instance.RawBarHeight : 0f;
        Transform ct = cam.transform;
        if (planesFov < 0f || ct.position != planesPosition || ct.forward != planesForward || cam.fieldOfView != planesFov ||
            cam.aspect != planesAspect || cam.orthographic != planesIsOrtho || cam.orthographicSize != planesOrtho || Screen.width != planesWidth || Screen.height != planesHeight || barNow != planesBars)
        {
            BuildPlanes(cam);
            planesPosition = ct.position;
            planesForward = ct.forward;
            planesFov = cam.fieldOfView;
            planesAspect = cam.aspect;
            planesIsOrtho = cam.orthographic;
            planesOrtho = cam.orthographicSize;
            planesWidth = Screen.width;
            planesHeight = Screen.height;
            planesBars = barNow;
        }

        bool useCircle = false;
        Vector3 circleCenter = Vector3.zero;
        float circleRadius = 0f;
        if (circleAroundFloor && PixelFloor.Instance != null) useCircle = PixelFloor.Instance.TryGetEdgeCircle(out circleCenter, out circleRadius);

        var pixels = clicker.OldPixels;
        if (infoCache.Count > pixels.Count + 64) infoCache.Clear(); // drop entries of destroyed pixels
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic) continue;

            if (!infoCache.TryGetValue(body, out Entry entry) || entry.info == null)   // (looked again while the husk is still getting its cards)
            {
                entry = new Entry { info = body.GetComponent<OldPixelInfo>(), box = body.GetComponent<BoxCollider>() };
                entry.ignores = entry.info != null && clicker.IgnoresViewBounds(entry.info.tierIndex);
                infoCache[body] = entry;
            }
            OldPixelInfo info = entry.info;
            if (info != null && info.tierIndex >= 0 && info.tierIndex < clicker.Tiers.Length &&
                (entry.ignores || clicker.IsFlyingPixel(body))) continue;

            Vector3 position = body.position;
            Vector3 velocity = GetVelocity(body);
            bool changed = false;

            // The pixel's half size along each of its own axes (it swells, shrinks and tumbles, so this is read every step).
            Transform tr = body.transform;
            Vector3 lossy = tr.lossyScale;
            Vector3 half = entry.box != null
                ? Vector3.Scale(entry.box.size, new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z))) * 0.5f
                : Vector3.one * (Mathf.Max(Mathf.Abs(lossy.x), Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z))) * 0.5f);

            if (useCircle)
            {
                // The circle: a vertical cylinder round the floor's middle. The pixel's own horizontal reach keeps its outer surface (not its centre) inside it.
                float dx = position.x - circleCenter.x, dz = position.z - circleCenter.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                float reachC = Mathf.Max(half.x, Mathf.Max(half.y, half.z)) + extraEdgeMargin;
                float maxDist = Mathf.Max(0.1f, circleRadius - reachC);
                if (dist > maxDist && dist > 0.0001f)
                {
                    Vector3 outward = new Vector3(dx / dist, 0f, dz / dist);   // points out of the circle
                    position.x = circleCenter.x + outward.x * maxDist;
                    position.z = circleCenter.z + outward.z * maxDist;
                    float outSpeed = Vector3.Dot(velocity, outward);
                    if (outSpeed > 0f)
                    {
                        Vector3 tangent = velocity - outward * outSpeed;
                        velocity = tangent * (1f - friction) - outward * outSpeed * bounciness;
                    }
                    changed = true;
                }
            }

            for (int p = 0; p < planes.Length; p++)
            {
                if (!used[p] || (useCircle && p < 4)) continue;   // (the circle replaces the four screen edges; the near limit stays)
                float distance = planes[p].GetDistanceToPoint(position);
                Vector3 normal = planes[p].normal; // points into the view
                // How far a box of this size and rotation reaches towards the edge from its centre (exact for a box), so the pixel's
                // outer surface - not its centre - is what meets the edge of the view.
                float reach = Mathf.Abs(Vector3.Dot(normal, tr.right)) * half.x + Mathf.Abs(Vector3.Dot(normal, tr.up)) * half.y
                            + Mathf.Abs(Vector3.Dot(normal, tr.forward)) * half.z + extraEdgeMargin;
                if (distance >= reach) continue;

                position += normal * (reach - distance);

                float into = Vector3.Dot(velocity, normal);
                if (into < 0f)
                {
                    Vector3 along = velocity - normal * into;
                    velocity = along * (1f - friction) - normal * into * bounciness;
                }
                changed = true;
            }

            if (changed)
            {
                body.position = position;
                SetVelocity(body, velocity);
            }
        }
    }

    /// <summary>Writes this component's settings and its current five planes (point on plane = normal * -distance) for the bounds snapshot tool.</summary>
    public void Describe(System.Text.StringBuilder sb)
    {
        sb.AppendLine("boundsActive=" + boundsActive + " sideEdges=" + sideEdges + " topEdge=" + topEdge + " bottomEdge=" + bottomEdge + " insideBlackBars=" + insideBlackBars);
        sb.AppendLine("inset=" + inset + " extraEdgeMargin=" + extraEdgeMargin + " nearLimit=" + nearLimit + " bounciness=" + bounciness + " friction=" + friction);
        sb.AppendLine("barFractionUsed=" + (insideBlackBars && PixelHud.Instance != null && PixelHud.Instance.BarHeight > 0f ? BarFraction() : 0f));
        string[] names = { "left", "right", "top", "bottom", "near" };
        for (int i = 0; i < planes.Length && i < names.Length; i++)
            sb.AppendLine("plane " + names[i] + " used=" + used[i] + " normal=" + planes[i].normal.ToString("F4") + " distance=" + planes[i].distance.ToString("F4"));
    }

    /// <summary>Builds the planes of the view's edges (all normals point into the view).</summary>
    private void BuildPlanes(Camera cam)
    {
        float bars = insideBlackBars && PixelHud.Instance != null && PixelHud.Instance.BarHeight > 0f ? BarFraction() : 0f;
        float left = inset, right = 1f - inset;
        float bottom = bars + inset, top = 1f - bars - inset;
        Vector3 origin = cam.transform.position;
        Vector3 inward = cam.transform.forward;

        used[0] = used[1] = sideEdges;
        used[2] = topEdge;
        used[3] = bottomEdge;
        used[4] = nearLimit > 0f;

        // Each edge is a plane through the camera and two rays along that edge.
        planes[0] = EdgePlane(cam, origin, new Vector2(left, 0.5f), new Vector2(left, 1f), new Vector2(left, 0f), new Vector2(0.5f, 0.5f));
        planes[1] = EdgePlane(cam, origin, new Vector2(right, 0.5f), new Vector2(right, 1f), new Vector2(right, 0f), new Vector2(0.5f, 0.5f));
        planes[2] = EdgePlane(cam, origin, new Vector2(0.5f, top), new Vector2(1f, top), new Vector2(0f, top), new Vector2(0.5f, 0.5f));
        planes[3] = EdgePlane(cam, origin, new Vector2(0.5f, bottom), new Vector2(1f, bottom), new Vector2(0f, bottom), new Vector2(0.5f, 0.5f));
        planes[4] = new Plane(inward, origin + inward * nearLimit);
    }

    /// <summary>A plane through the camera along a screen edge, facing the middle of the screen.</summary>
    private static Plane EdgePlane(Camera cam, Vector3 origin, Vector2 edgeMid, Vector2 edgeA, Vector2 edgeB, Vector2 centre)
    {
        Ray rayA = cam.ViewportPointToRay(edgeA);
        Ray rayB = cam.ViewportPointToRay(edgeB);
        if (cam.orthographic)
        {
            // Flat view: the rays are parallel, so the edge is a plane along the view direction through the edge's line.
            Vector3 flatNormal = Vector3.Cross(rayA.direction, rayB.origin - rayA.origin).normalized;
            Vector3 centreOrigin = cam.ViewportPointToRay(centre).origin;
            if (Vector3.Dot(flatNormal, centreOrigin - rayA.origin) < 0f) flatNormal = -flatNormal;
            return new Plane(flatNormal, rayA.origin);
        }
        Vector3 normal = Vector3.Cross(rayA.direction, rayB.direction).normalized;
        Vector3 toCentre = cam.ViewportPointToRay(centre).direction;
        if (Vector3.Dot(normal, toCentre) < 0f) normal = -normal;
        return new Plane(normal, origin);
    }

    /// <summary>The bars' thickness as a fraction of the screen's height.</summary>
    private float BarFraction()
    {
        float scale = Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f); // the UI's canvas scaling
        return Mathf.Clamp01(PixelHud.Instance.RawBarHeight * scale / Mathf.Max(1, Screen.height)); // the bars are drawn on an unscaled canvas
    }

    private static Vector3 GetVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody rb, Vector3 v)
    {
        if (rb == null || rb.isKinematic) return;   // (a kinematic body cannot be steered: Unity warns)
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
