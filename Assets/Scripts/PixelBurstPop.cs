// ---- System notes ----
// PixelBurstPop.cs
//   Where a clicked pixel's old copy lands (part of partial class PixelClicker, used by SpawnFallingCopyInner): when the ground is a real
//   fall away (more than 2 pixel widths under the cube) the copy is not just nudged out at a random heading - TryBurstVelocity works out
//   how long it will be in the air (gravity x gravityScale, a small upward kick burstUpSpeed 0 = 1..3) and aims it at a random point on
//   a ring round the cube (any direction, burstLandRadius 0 = 1.2..3.5 pixel widths), so a click throws pixels OUT in all directions and
//   they land spread over the floor (usable as physics items) instead of dropping straight down in one stack. Short falls and
//   disableBurstPop keep the old pop (popSpeedRange / popVerticalRange).
// ---------------------------------------------------
using UnityEngine;

public partial class PixelClicker
{
    [Header("Burst Pop (a long fall)")]
    [Tooltip("Use the old random pop (popSpeedRange / popVerticalRange) even when the ground is far below.")]
    [SerializeField] private bool disableBurstPop = false;

    [Tooltip("Upward kick of a burst pixel (min, max, world units per second). (0, 0) = the default (1, 3).")]
    [SerializeField] private Vector2 burstUpSpeed = Vector2.zero;

    [Tooltip("How far from the cube a burst pixel lands, in pixel widths (min, max); the direction is random. (0, 0) = the default (1.2, 3.5).")]
    [SerializeField] private Vector2 burstLandRadius = Vector2.zero;

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
        Vector2 up = burstUpSpeed.sqrMagnitude > 0f ? burstUpSpeed : new Vector2(1f, 3f);
        float v0 = UnityEngine.Random.Range(Mathf.Min(up.x, up.y), Mathf.Max(up.x, up.y));
        float airTime = (v0 + Mathf.Sqrt(v0 * v0 + 2f * g * drop)) / g;

        Vector2 ring = burstLandRadius.sqrMagnitude > 0f ? burstLandRadius : new Vector2(1.2f, 3.5f);
        float radius = UnityEngine.Random.Range(Mathf.Min(ring.x, ring.y), Mathf.Max(ring.x, ring.y)) * size;
        float sideways = Mathf.Min(radius / Mathf.Max(0.05f, airTime), 14f);
        velocity = new Vector3(flatDirection.x * sideways, v0, flatDirection.y * sideways);
        return true;
    }
}
