using TMPro;
using UnityEngine;

/// <summary>
/// A placed vacuum device (the cylinder). Created and configured by <see cref="PixelConsumables"/>.
///
/// While it is active it pulls every old pixel inside its radius toward the top of the cylinder and,
/// when a pixel gets close enough, collects it again (the original reward is re-added, like the Vacuum
/// pixel). A timer floats above it showing the seconds left. When time runs out it shrinks away.
/// </summary>
public class PixelVacuumDevice : MonoBehaviour
{
    private PixelClicker clicker;
    private Camera cam;
    private TextMeshPro timerText;
    private string timerFormat;
    private Transform suckPoint;

    private float remaining;
    private float radius;
    private float pullAcceleration;
    private float absorbDistance;
    private float shrinkSeconds;
    private bool dying;

    /// <summary>Seconds left.</summary>
    public float Remaining => remaining;

    public void Init(PixelClicker owner, Camera camera, Transform pullTarget, TextMeshPro timer, string format,
                     float duration, float suctionRadius, float pull, float absorb, float shrinkTime)
    {
        clicker = owner;
        cam = camera;
        suckPoint = pullTarget;
        timerText = timer;
        timerFormat = format;
        remaining = duration;
        radius = suctionRadius;
        pullAcceleration = pull;
        absorbDistance = absorb;
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
