using UnityEngine;
using static PixelInput;

/// <summary>
/// UFO Helper "screen saver": after a few minutes without input (and no job to do) it flies up out of view above the
/// camera. From then on it checks in every few minutes - a fly-by across the screen, or coming in to wander about
/// for a while - and leaves again. Any input brings it back down to its normal wandering. Switching it off and on
/// in Toggles puts it back on screen.
/// </summary>
public partial class PixelRobotWorker
{
    private enum SaverMode { None, Leaving, Away, FlyBy, Roam }

    [Header("Screen Saver")]
    [SerializeField, Tooltip("Turns the screen saver behaviour off (the UFO just wanders as always). Default: off.")]
    private bool disableScreenSaver;
    [SerializeField, Tooltip("Minutes without any input before the UFO flies away above the camera. 0 = 3.")]
    private float saverIdleMinutes;
    [SerializeField, Tooltip("Shortest time in minutes between check-ins while it is away. 0 = 2.")]
    private float saverCheckInMin;
    [SerializeField, Tooltip("Longest time in minutes between check-ins while it is away. 0 = 5.")]
    private float saverCheckInMax;
    [SerializeField, Tooltip("How long a visit lasts when it comes in to fly around, in seconds. 0 = 20.")]
    private float saverRoamSeconds;
    [SerializeField, Tooltip("Flight speed multiplier while the screen saver is flying. 0 = 1.6.")]
    private float saverSpeedFactor;

    private SaverMode saverMode;
    private float saverIdle;
    private float saverNextCheckIn;
    private float saverUntil;
    private Vector3 saverTarget;
    private Vector2 saverLastPointer;
    private bool saverHavePointer;

    /// <summary>True while the screen saver steers the UFO (the normal job / wander logic is paused).</summary>
    private bool SaverFlying => saverMode != SaverMode.None && saverMode != SaverMode.Roam;

    private bool SaverBusy => saverMode != SaverMode.None;

    /// <summary>Forgets the saver state: the UFO comes back as if just built (used when it is switched off and on).</summary>
    private void ResetSaver()
    {
        saverMode = SaverMode.None;
        saverIdle = 0f;
        saverHavePointer = false;
        hasPos = false;
        nextWander = 0f;
        wanderTarget = Vector3.zero;
    }

    private bool SaverActivity()
    {
        Vector2 p = PointerPosition();
        bool moved = saverHavePointer && (p - saverLastPointer).sqrMagnitude > 9f;
        saverLastPointer = p;
        saverHavePointer = true;
        return moved || AnyInputHeld() || ScrollY() != 0f;
    }

    private Vector3 SaverPoint(float vx, float vy)
    {
        Camera cam = Cam;
        float depth = 10f;
        if (cam != null && clicker.PixelTransform != null)
            depth = Mathf.Max(1f, Vector3.Dot(clicker.PixelTransform.position - cam.transform.position, cam.transform.forward));
        return cam != null ? cam.ViewportToWorldPoint(new Vector3(vx, vy, depth)) : Vector3.up * 20f;
    }

    private void StartLeaving()
    {
        saverMode = SaverMode.Leaving;
        saverTarget = SaverPoint(UnityEngine.Random.Range(0.35f, 0.65f), 1.6f);
    }

    private void ScheduleCheckIn()
    {
        float lo = (saverCheckInMin > 0f ? saverCheckInMin : 2f) * 60f;
        float hi = Mathf.Max(lo, (saverCheckInMax > 0f ? saverCheckInMax : 5f) * 60f);
        saverNextCheckIn = Time.unscaledTime + UnityEngine.Random.Range(lo, hi);
    }

    private void UpdateSaver()
    {
        if (disableScreenSaver)
        {
            if (saverMode != SaverMode.None) { saverMode = SaverMode.None; nextWander = 0f; }
            return;
        }
        if (Time.timeScale <= 0f || PixelPauseMenu.IsPaused) return;   // paused: the clock stands still too
        bool activity = SaverActivity();
        bool busy = WorkingSpot() || (windowRoot != null && windowRoot.activeSelf) || hovering;

        if (saverMode == SaverMode.None)
        {
            if (activity || busy) { saverIdle = 0f; return; }
            saverIdle += Time.unscaledDeltaTime;
            if (saverIdle >= (saverIdleMinutes > 0f ? saverIdleMinutes : 3f) * 60f && hasPos) StartLeaving();
            return;
        }

        if (activity || busy)
        {
            // Welcome back: it drops down to its normal wandering from wherever it is.
            saverMode = SaverMode.None;
            saverIdle = 0f;
            nextWander = 0f;
            return;
        }

        float pb = clicker.PixelBaseSize;
        switch (saverMode)
        {
            case SaverMode.Leaving:
                if ((saverTarget - pos).magnitude < 0.6f * pb)
                {
                    saverMode = SaverMode.Away;
                    ScheduleCheckIn();
                }
                break;
            case SaverMode.Away:
                if (Time.unscaledTime >= saverNextCheckIn)
                {
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        // Fly-by: in from one side up high, straight across, out the other.
                        bool fromLeft = UnityEngine.Random.value < 0.5f;
                        float y = UnityEngine.Random.Range(0.72f, 0.92f);
                        pos = SaverPoint(fromLeft ? -0.25f : 1.25f, y);
                        velocity = Vector3.zero;
                        saverTarget = SaverPoint(fromLeft ? 1.3f : -0.3f, y + UnityEngine.Random.Range(-0.08f, 0.08f));
                        saverMode = SaverMode.FlyBy;
                    }
                    else
                    {
                        // Visit: drops in from above and wanders about for a while.
                        pos = SaverPoint(UnityEngine.Random.Range(0.3f, 0.7f), 1.4f);
                        velocity = Vector3.zero;
                        wanderTarget = Vector3.zero;
                        nextWander = 0f;
                        saverUntil = Time.unscaledTime + (saverRoamSeconds > 0f ? saverRoamSeconds : 20f);
                        saverMode = SaverMode.Roam;
                    }
                }
                break;
            case SaverMode.FlyBy:
                if ((saverTarget - pos).magnitude < 0.8f * pb)
                {
                    StartLeaving();
                    ScheduleCheckIn();
                }
                break;
            case SaverMode.Roam:
                if (Time.unscaledTime >= saverUntil)
                {
                    StartLeaving();
                    ScheduleCheckIn();
                }
                break;
        }
    }

    /// <summary>The spot the saver wants it at, and how fast to fly (a multiple of the normal speed).</summary>
    private bool SaverSteer(out Vector3 target, out float speedMul)
    {
        target = pos;
        speedMul = 1f;
        if (!SaverFlying) return false;
        target = saverTarget;
        speedMul = saverSpeedFactor > 0f ? saverSpeedFactor : 1.6f;
        return true;
    }

    /// <summary>True while the UFO is outside the screen during the saver (its speech bubble stays hidden).</summary>
    private bool SaverOffScreen(Vector3 screenPoint)
    {
        if (!SaverBusy) return false;
        return screenPoint.z < 0f || screenPoint.y > Screen.height || screenPoint.x < 0f || screenPoint.x > Screen.width;
    }
}
