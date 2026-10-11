using TMPro;
using UnityEngine;

/// <summary>
/// A placed Magnet (device <c>kind</c> = Magnet): works like the Vacuum Device but never consumes anything. While it is switched on it pulls
/// every loose old pixel inside its radius towards the gap of its horseshoe and holds them there in a heap (they keep their value and keep
/// ageing normally). Pixels that are held, flying away or frozen by a minigame are left alone. The countdown, timer text and shrink-away come
/// from <see cref="PixelPlacedDevice"/>; the model is built by <see cref="PixelConsumables"/>.
/// </summary>
public class PixelMagnetDevice : PixelPlacedDevice
{
    private Transform pullPoint;
    private float radius;
    private float pull;
    private float holdDistance;
    private float drop;

    public void Init(PixelClicker owner, Camera camera, Transform topPoint, TextMeshPro timer, string format, float duration,
                     float reach, float pullAcceleration, float holdRadius, float shrinkTime, float bodyHeight)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        pullPoint = topPoint;
        radius = reach;
        pull = pullAcceleration;
        holdDistance = Mathf.Max(0.1f, holdRadius);
        drop = bodyHeight * 0.55f;   // the pull centre sits in the gap of the horseshoe, not at its top
    }

    private void FixedUpdate()
    {
        if (IsDying || !Armed || clicker == null || pullPoint == null) return;
        Vector3 target = pullPoint.position + Vector3.down * drop;
        var pixels = clicker.OldPixels;
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic || clicker.IsFlyingPixel(body)) continue;
            OldPixelDespawn d = body.GetComponent<OldPixelDespawn>();
            if (d != null && (d.Held || d.IsDespawning)) continue;

            Vector3 to = target - body.position;
            float distance = to.magnitude;
            if (distance > radius) continue;

            if (distance <= holdDistance)
            {
                // Close to the gap: settle and stay (a drag instead of more pull, so the heap doesn't jitter).
#if UNITY_6000_0_OR_NEWER
                body.linearVelocity *= 0.9f;
#else
                body.velocity *= 0.9f;
#endif
            }
            else body.AddForce(to / distance * pull, ForceMode.Acceleration);
        }
    }
}
