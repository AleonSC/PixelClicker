using TMPro;
using UnityEngine;

/// <summary>
/// A placed fan. Created and configured by <see cref="PixelConsumables"/>.
///
/// While it is active it gently blows every old pixel inside a cone-shaped area in front of it: the area is a wedge
/// on the floor (opening angle and length are adjustable) that reaches up to a set height. The blades spin.
/// The countdown, timer text and shrink-away come from <see cref="PixelPlacedDevice"/>.
/// </summary>
public class PixelFanDevice : PixelPlacedDevice
{
    private Transform blades;
    private float range;
    private float halfAngle;
    private float height;
    private float blow;
    private float lift;
    private float spinDegrees;
    private float bladeAngle;

    public void Init(PixelClicker owner, Camera camera, Transform bladesTransform, TextMeshPro timer, string format,
                     float duration, float reach, float coneAngle, float coneHeight, float blowAcceleration,
                     float liftAcceleration, float bladeSpin, float shrinkTime)
    {
        InitCommon(owner, camera, timer, format, duration, shrinkTime);
        blades = bladesTransform;
        range = reach;
        halfAngle = coneAngle * 0.5f;
        height = coneHeight;
        blow = blowAcceleration;
        lift = liftAcceleration;
        spinDegrees = bladeSpin;
    }

    protected override void OnTick()
    {
        if (blades == null) return;
        bladeAngle += spinDegrees * Time.deltaTime;
        blades.localRotation = Quaternion.Euler(0f, 0f, bladeAngle);
    }

    private void FixedUpdate()
    {
        if (IsDying || !Armed || clicker == null) return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        var pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic) continue;

            Vector3 to = body.position - origin;
            if (to.y < -0.3f || to.y > height) continue; // above / below the wedge

            to.y = 0f;
            float distance = to.magnitude;
            if (distance > range || distance < 0.05f) continue;
            if (Vector3.Angle(forward, to) > halfAngle) continue; // outside the cone

            // Strongest right at the fan, fading toward the far end.
            float strength = Mathf.Lerp(1f, 0.25f, distance / range);
            body.AddForce((forward * blow + Vector3.up * lift) * strength, ForceMode.Acceleration);
        }
    }
}
