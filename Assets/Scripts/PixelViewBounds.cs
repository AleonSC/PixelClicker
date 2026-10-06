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
    [Tooltip("Extra room (world units) between a pixel's centre and an edge, so the whole pixel stays on screen.")]
    [SerializeField] private float edgeMargin = 0.15f;

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

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelViewBounds: no PixelClicker found in the scene.", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (!boundsActive) return;

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        BuildPlanes(cam);

        var pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic) continue;

            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info != null && info.tierIndex >= 0 && info.tierIndex < clicker.Tiers.Length && clicker.Tiers[info.tierIndex].flyAway) continue;

            Vector3 position = body.position;
            Vector3 velocity = GetVelocity(body);
            bool changed = false;

            for (int p = 0; p < planes.Length; p++)
            {
                if (!used[p]) continue;
                float distance = planes[p].GetDistanceToPoint(position);
                if (distance >= edgeMargin) continue;

                Vector3 normal = planes[p].normal; // points into the view
                position += normal * (edgeMargin - distance);

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
        Vector3 a = cam.ViewportPointToRay(edgeA).direction;
        Vector3 b = cam.ViewportPointToRay(edgeB).direction;
        Vector3 normal = Vector3.Cross(a, b).normalized;
        Vector3 toCentre = cam.ViewportPointToRay(centre).direction;
        if (Vector3.Dot(normal, toCentre) < 0f) normal = -normal;
        return new Plane(normal, origin);
    }

    /// <summary>The bars' thickness as a fraction of the screen's height.</summary>
    private float BarFraction()
    {
        float scale = Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f); // the UI's canvas scaling
        return Mathf.Clamp01(PixelHud.Instance.BarHeight * scale / Mathf.Max(1, Screen.height));
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
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }
}
