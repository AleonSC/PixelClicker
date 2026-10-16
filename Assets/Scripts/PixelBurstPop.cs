// ---- System notes ----
// PixelBurstPop.cs
//   Where a clicked pixel's old copy lands (part of partial class PixelClicker, used by SpawnFallingCopyInner): when the ground is a real
//   fall away (more than 2 pixel widths under the cube) the copy is not just nudged out at a random heading - TryBurstVelocity works out
//   how long it will be in the air (gravity x gravityScale, no upward kick: burstUpSpeed 0 = -0.4..0, never above 0, and the copy appears at the cube's side popSideOffset 0 = 0.45 pixel widths out) and aims it at a random point on
//   a ring round the cube (any direction, burstLandRadius 0 = 1.2..3.5 pixel widths), so a click throws pixels OUT in all directions and
//   they land spread over the floor (usable as physics items) instead of dropping straight down in one stack. Short falls and
//   disableBurstPop keep the old pop (popSpeedRange / popVerticalRange).
//   EDGE-CLICK AIMING (edgeAiming, 2026-10-16): a MANUAL click near the cube's edge (on the screen: aimDeadZone 0.3 .. aimFullAt 0.85 of the way from its
//   centre to its edge) throws the husk out of that side, within aimSpread (25 deg) either way; the middle stays random (spread 180) and it blends in
//   between. Works for long falls (burst) and short ones (pop) alike. The auto clicker and first person always scatter. AimedDirection, set up by
//   CollectInternal (aimThisCopy). Lets the player feed a Recycler / crate while clicking.
// ---------------------------------------------------
using UnityEngine;

public partial class PixelClicker
{
    [Header("Burst Pop (a long fall)")]
    [Tooltip("Use the old random pop (popSpeedRange / popVerticalRange) even when the ground is far below.")]
    [SerializeField] private bool disableBurstPop = false;

    [Tooltip("Vertical speed of a burst pixel (min, max, world units per second). Never upward: anything above 0 counts as 0, so pixels leave through the SIDES of the cube and never rise over its top. (0, 0) = the default (-0.4, 0).")]
    [SerializeField] private Vector2 burstUpSpeed = Vector2.zero;

    [Min(0f)]
    [Tooltip("How far out from the cube's centre a clicked pixel's copy appears, towards the side it is thrown to, in pixel widths (0.5 = right at the side face). 0 = the default (0.45).")]
    [SerializeField] private float popSideOffset = 0f;

    [Tooltip("How far from the cube a burst pixel lands, in pixel widths (min, max); the direction is random. (0, 0) = the default (1.2, 3.5).")]
    [SerializeField] private Vector2 burstLandRadius = Vector2.zero;

    [Header("Edge-click aiming")]
    [Tooltip("A manual click near the EDGE of the cube throws its husk out of that side (where you clicked on the screen); a click in the middle scatters it at random as before. The auto clicker always scatters.")]
    [SerializeField] private bool edgeAiming = true;
    [Range(0f, 1f)]
    [Tooltip("How far from the cube's centre (0 = centre, 1 = the edge of the cube on screen) aiming STARTS; nearer the middle is fully random.")]
    [SerializeField] private float aimDeadZone = 0.3f;
    [Range(0f, 1.5f)]
    [Tooltip("How far from the centre the aim is at its sharpest (the cube's edge on screen is 1).")]
    [SerializeField] private float aimFullAt = 0.85f;
    [Range(0f, 180f)]
    [Tooltip("Spread either side of the aimed direction at the sharpest aim, in degrees.")]
    [SerializeField] private float aimSpread = 25f;

    private bool aimThisCopy;   // set by CollectInternal around a manual click's SpawnFallingCopy

    /// <summary>
    /// Edge-click aiming: where the click landed on the cube (on the SCREEN, so the cube's spin does not matter) picks the husk's heading on the floor.
    /// Near the middle the random heading is kept; towards the edge it narrows to that side (aimSpread either way). Returns a normalized flat (x, z) direction.
    /// </summary>
    /// <summary>
    /// Edge-click aiming as seen from a screen point: the on-screen direction from the cube's centre to the point and how sharp the aim is there
    /// (0 = the random middle, 1 = fully aimed). Shared by the throw (AimedDirection) and the aim cursor (PixelAimCursor). False when aiming is off.
    /// </summary>
    public bool TryGetAim(Vector2 screen, out Vector2 screenDir, out float sharp)
    {
        screenDir = Vector2.right;
        sharp = 0f;
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (!edgeAiming || cam == null || pixelTransform == null || PixelFirstPerson.Active) return false;
        Vector3 centre = cam.WorldToScreenPoint(pixelTransform.position);
        Vector3 edge = cam.WorldToScreenPoint(pixelTransform.position + cam.transform.right * (PixelBaseSize * 0.5f));
        float halfPx = Mathf.Abs(edge.x - centre.x);
        if (centre.z <= 0f || halfPx < 1f) return false;
        Vector2 offset = (screen - new Vector2(centre.x, centre.y)) / halfPx;
        if (offset.sqrMagnitude > 0.0001f) screenDir = offset.normalized;
        sharp = Mathf.InverseLerp(aimDeadZone, Mathf.Max(aimDeadZone + 0.01f, aimFullAt), offset.magnitude);
        return true;
    }

    private Vector2 AimedDirection(Vector2 random)
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (!TryGetAim(PointerPosition(), out Vector2 screenDir, out float sharp) || sharp <= 0f) return random;
        Vector2 offset = screenDir;
        // The screen direction of the click, laid flat on the floor (camera right / up projected onto the ground plane).
        Vector3 world = cam.transform.right * offset.x + cam.transform.up * offset.y;
        Vector2 aim = new Vector2(world.x, world.z);
        if (aim.sqrMagnitude < 0.0001f) return random;
        aim.Normalize();
        float spread = Mathf.Lerp(180f, aimSpread, sharp * sharp * (3f - 2f * sharp));
        float angle = UnityEngine.Random.Range(-spread, spread) * Mathf.Deg2Rad;
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        return new Vector2(aim.x * c - aim.y * s, aim.x * s + aim.y * c);
    }

    private readonly RaycastHit[] burstHits = new RaycastHit[12];

    /// <summary>The height of the first solid surface under 'from' (ignoring the cube and every rigidbody: old pixels, pets, dynamic shapes). False when there is none.</summary>
    private bool TryGroundBelow(Vector3 from, out float groundY)
    {
        groundY = 0f;
        float size = Mathf.Max(0.05f, PixelBaseSize);
        int n = Physics.RaycastNonAlloc(from + Vector3.down * size, Vector3.down, burstHits, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = burstHits[i];
            if (h.collider == null || h.rigidbody != null) continue;
            if (pixelTransform != null && h.collider.transform.IsChildOf(pixelTransform)) continue;
            if (h.distance < best) { best = h.distance; groundY = h.point.y; }
        }
        return !float.IsPositiveInfinity(best);
    }

    private bool TryBurstVelocity(Vector3 from, Vector2 flatDirection, out Vector3 velocity)
    {
        velocity = default;
        if (disableBurstPop || !TryGroundBelow(from, out float groundY)) return false;
        float size = Mathf.Max(0.05f, PixelBaseSize);
        float drop = from.y - groundY;
        if (drop < size * 2f) return false;   // a short fall: the ordinary pop is enough

        float g = Mathf.Abs(Physics.gravity.y) * Mathf.Max(0.01f, gravityScale);
        Vector2 up = burstUpSpeed.sqrMagnitude > 0f ? burstUpSpeed : new Vector2(-0.4f, 0f);
        float v0 = Mathf.Min(0f, UnityEngine.Random.Range(Mathf.Min(up.x, up.y), Mathf.Max(up.x, up.y)));   // never upward: out of the sides, not over the top
        float airTime = (v0 + Mathf.Sqrt(v0 * v0 + 2f * g * drop)) / g;

        Vector2 ring = burstLandRadius.sqrMagnitude > 0f ? burstLandRadius : new Vector2(1.2f, 3.5f);
        float radius = UnityEngine.Random.Range(Mathf.Min(ring.x, ring.y), Mathf.Max(ring.x, ring.y)) * size;
        float sideways = Mathf.Min(radius / Mathf.Max(0.05f, airTime), 14f);
        velocity = new Vector3(flatDirection.x * sideways, v0, flatDirection.y * sideways);
        return true;
    }
}
