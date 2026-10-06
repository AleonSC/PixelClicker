using TMPro;
using UnityEngine;

/// <summary>
/// A placed vacuum device (the cylinder). Created and configured by <see cref="PixelConsumables"/>.
///
/// While it is active it pulls every old pixel inside its radius toward the top of the cylinder and,
/// when a pixel gets close enough, collects it again (the original reward is re-added, like the Vacuum
/// pixel). The countdown, timer text and shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelVacuumDevice : PixelPlacedDevice
{
    private Transform suckPoint;
    private float radius;
    private float pullAcceleration;
    private float absorbDistance;

    public void Init(PixelClicker owner, Camera camera, Transform pullTarget, TextMeshPro timer, string format,
                     float duration, float suctionRadius, float pull, float absorb, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        suckPoint = pullTarget;
        radius = suctionRadius;
        pullAcceleration = pull;
        absorbDistance = absorb;
    }

    private void FixedUpdate()
    {
        if (IsDying || clicker == null) return;

        var pixels = clicker.OldPixels;
        Vector3 target = suckPoint.position;

        // Backwards: collecting a pixel removes it from the list.
        for (int i = pixels.Count - 1; i >= 0; i--)
        {
            Rigidbody body = pixels[i];
            if (body == null) continue;

            Vector3 to = target - body.position;
            float distance = to.magnitude;
            if (distance > radius) continue;

            if (distance <= absorbDistance) clicker.AbsorbOldPixel(body, suckPoint);
            else body.AddForce(to / distance * pullAcceleration, ForceMode.Acceleration);
        }
    }
}
