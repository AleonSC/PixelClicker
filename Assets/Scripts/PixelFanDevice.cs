using TMPro;
using UnityEngine;

/// <summary>
/// A placed fan. Created and configured by <see cref="PixelConsumables"/>.
///
/// While it is active it gently blows every old pixel inside a cone-shaped area in front of it: the area is a wedge
/// on the floor (opening angle and length are adjustable) that reaches up to a set height. The blades spin, a timer
/// floats above it, and when time runs out it shrinks away.
/// </summary>
public class PixelFanDevice : MonoBehaviour
{
    private PixelClicker clicker;
    private Camera cam;
    private TextMeshPro timerText;
    private string timerFormat;
    private Transform blades;

    private float remaining;
    private float range;
    private float halfAngle;
    private float height;
    private float blow;
    private float lift;
    private float spinDegrees;
    private float shrinkSeconds;
    private float bladeAngle;
    private bool dying;

    public float Remaining => remaining;

    public void Init(PixelClicker owner, Camera camera, Transform bladesTransform, TextMeshPro timer, string format,
                     float duration, float reach, float coneAngle, float coneHeight, float blowAcceleration,
                     float liftAcceleration, float bladeSpin, float shrinkTime)
    {
        clicker = owner;
        cam = camera;
        blades = bladesTransform;
        timerText = timer;
        timerFormat = format;
        remaining = duration;
        range = reach;
        halfAngle = coneAngle * 0.5f;
        height = coneHeight;
        blow = blowAcceleration;
        lift = liftAcceleration;
        spinDegrees = bladeSpin;
        shrinkSeconds = Mathf.Max(0.01f, shrinkTime);
    }

    private void Update()
    {
        if (dying) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            StartCoroutine(ShrinkAway());
            return;
        }

        if (blades != null)
        {
            bladeAngle += spinDegrees * Time.deltaTime;
            blades.localRotation = Quaternion.Euler(0f, 0f, bladeAngle);
        }
        if (timerText != null) timerText.text = string.Format(timerFormat, Mathf.CeilToInt(remaining));
    }

    private void LateUpdate()
    {
        // The timer always faces the camera.
        if (timerText == null) return;
        if (cam == null) cam = Camera.main;
        if (cam != null) timerText.transform.rotation = cam.transform.rotation;
    }

    private void FixedUpdate()
    {
        if (dying || clicker == null) return;

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

    private System.Collections.IEnumerator ShrinkAway()
    {
        dying = true;
        if (timerText != null) timerText.gameObject.SetActive(false);

        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < shrinkSeconds)
        {
            t += Time.deltaTime;
            transform.localScale = start * (1f - Mathf.Clamp01(t / shrinkSeconds));
            yield return null;
        }
        Destroy(gameObject);
    }
}
