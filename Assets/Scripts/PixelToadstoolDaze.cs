// ---- System notes ----
// PixelToadstoolDaze.cs
//   The Toadstool's spores (Foraging hazard, set off by one left click): a temporary status effect. For 'seconds' (12) every one of the player's own
//   clicks on the cube makes the cube JERK AWAY from the mouse cursor (a jump of jumpSize cube widths straight away from the pointer on the screen,
//   kept within maxDistance of home), then it drifts slowly back towards its home spot. Uses PixelClicker.SetCubeOffset (like Cube Dash) and gives way to
//   a takeover minigame. A top status line counts the time down. Component created on demand (PixelToadstoolDaze.Begin from TriggerHazard).
// ---------------------------------------------------
using UnityEngine;

public class PixelToadstoolDaze : MonoBehaviour
{
    [Tooltip("Seconds the dizziness lasts.")]
    [SerializeField] private float seconds = 12f;
    [Tooltip("How far the cube jumps away from the cursor after each click, in cube widths.")]
    [SerializeField] private float jumpSize = 1.1f;
    [Tooltip("Furthest the cube can get from its home spot, in cube widths.")]
    [SerializeField] private float maxDistance = 2.4f;
    [Tooltip("How fast the cube drifts back home between clicks (higher = faster).")]
    [SerializeField] private float returnSpeed = 0.6f;
    [Tooltip("How fast a jump happens (higher = snappier).")]
    [SerializeField] private float jumpSpeed = 18f;

    private static PixelToadstoolDaze instance;
    private PixelClicker clicker;
    private float until;
    private Vector3 offset, target;
    private bool active;
    private int shownLeft = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    /// <summary>Is the player dizzy right now?</summary>
    public static bool Active => instance != null && instance.active;

    /// <summary>Starts (or restarts) the dizziness.</summary>
    public static void Begin(PixelClicker owner)
    {
        if (owner == null) return;
        if (instance == null) instance = owner.gameObject.AddComponent<PixelToadstoolDaze>();
        instance.Restart(owner);
    }

    private void Restart(PixelClicker owner)
    {
        if (clicker == null)
        {
            clicker = owner;
            clicker.PixelCollected += OnCollected;
            clicker.PixelHit += OnHit;
        }
        until = Time.time + Mathf.Max(1f, seconds);
        active = true;
        enabled = true;
    }

    private void OnDestroy()
    {
        if (clicker != null) { clicker.PixelCollected -= OnCollected; clicker.PixelHit -= OnHit; }
        if (instance == this) instance = null;
    }

    private void OnCollected(int tier, double amount, bool automatic) { if (!automatic) Jerk(); }
    private void OnHit(int tier, int hit, int needed, bool automatic) { if (!automatic) Jerk(); }

    /// <summary>A click landed: the cube jumps straight away from the cursor (on the screen), staying near home.</summary>
    private void Jerk()
    {
        if (!active || clicker == null || clicker.PixelTransform == null || PixelMinigame.TakeoverActive) return;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;
        float unit = Mathf.Max(0.1f, clicker.PixelBaseSize);
        Vector3 home = clicker.CubeBasePosition;
        Vector3 cubeScreen = cam.WorldToScreenPoint(home + offset);
        Vector2 away = (Vector2)cubeScreen - PixelInput.PointerPosition();
        if (away.sqrMagnitude < 1f) away = Random.insideUnitCircle.normalized;
        away.Normalize();
        // Screen direction -> world direction on the camera's view plane (right / up), so it always moves visibly away from the cursor.
        Vector3 dir = (cam.transform.right * away.x + cam.transform.up * away.y);
        dir.y *= 0.4f;   // mostly sideways: keep it from flying up or into the floor
        target = offset + dir.normalized * jumpSize * unit;
        if (target.magnitude > maxDistance * unit) target = target.normalized * maxDistance * unit;
    }

    private void Update()
    {
        if (clicker == null) return;
        if (active && (Time.time >= until || PixelMinigame.TakeoverActive))
        {
            active = false;
            shownLeft = -1;
            PixelStatusLines.Clear("toadstool");
        }
        float dt = Time.deltaTime;
        if (active)
        {
            target = Vector3.MoveTowards(target, Vector3.zero, returnSpeed * Mathf.Max(0.1f, clicker.PixelBaseSize) * dt);   // drifts back home slowly
            int left = Mathf.CeilToInt(until - Time.time);
            if (left != shownLeft) { shownLeft = left; PixelStatusLines.Set("toadstool", "Dizzy from toadstool spores: " + left + " s", new Color(0.85f, 0.55f, 1f, 1f)); }
        }
        else target = Vector3.zero;
        offset = Vector3.Lerp(offset, target, 1f - Mathf.Exp(-jumpSpeed * dt));
        if (!active && offset.sqrMagnitude < 0.0001f)
        {
            offset = Vector3.zero;
            if (!PixelMinigame.TakeoverActive) clicker.SetCubeOffset(null);
            enabled = false;   // (Begin turns it back on)
            return;
        }
        if (!PixelMinigame.TakeoverActive) clicker.SetCubeOffset(offset);
    }

}
