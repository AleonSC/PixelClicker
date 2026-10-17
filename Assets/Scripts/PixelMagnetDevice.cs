// ---- System notes (moved here from CLAUDE.md) ----
// PixelMagnetDevice.cs
//   (extends PixelPlacedDevice) Magnet (DeviceKind.Magnet 10, auto-added like the fan by EnsureDefaultPotions, Utilities card; needs
//   Electric unlocked, 20 Electric pixels, durationSeconds 30, radius 5, pullAcceleration 24, absorbDistance 0.9 = the 'settle'
//   distance): like the Vacuum Device it pulls loose old pixels in radius towards a point in the gap of its horseshoe
//   (BuildMagnetBody: red arch + legs, silver tips, base plate), but never collects them: pixels inside the settle distance just get
//   their speed damped, so they sit in a heap (held / flying / vanishing / kinematic pixels are skipped, ageing is unchanged); placed
//   like the vacuum (starts disarmed, click to switch on), robot-placeable (IsPlaceableKind), tip device_Magnet, stat
//   device.placed.Magnet, not part of the Operation tab yet.
// ---------------------------------------------------
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
            if (!PixelHuskHolder.Takeable(clicker, body) || !Accepts(body)) continue;   // (one owner per husk: held, reserved or kept by another machine = not ours)

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
