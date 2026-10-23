// ---- System notes (moved here from CLAUDE.md) ----
// PixelCameraIntro.cs
//   Camera intro (added by PixelClicker.Awake): the camera holds a close-up side view of the cube (closeDistance (1.8 cubes),
//   closeHeightInCubes, side, optional startFieldOfView) while the title screen shows - the blurred title picture is that view - then
//   after startDelay it swings round the cube on an arc and settles into the scene's own camera pose over introSeconds (the end pose
//   is whatever the camera has in the scene, nothing to set up; orthographic cameras zoom instead). A mouse click skips it
//   (skipOnClick, off by default); clicks are locked during the zoom-out of a brand-new game (ClicksLocked, lockClicksOnNewGame).
//   PlayReverse(onDone) plays the zoom backwards (reverseSeconds) for Quit > Main menu. Player switch: Settings > Camera intro
//   (PixelCameraIntro.Enabled, applies next start).
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Camera intro: the game starts with a close-up side view of the cube and, once Play is pressed (after the title screen), the
/// camera swings up and back out to the normal view the scene is set up with. The scene's own camera pose is the end point,
/// so there is nothing to set up. A mouse click skips it. Players can switch it off in Settings.
/// Added automatically by PixelClicker; add it to any GameObject yourself to change the settings in the Inspector.
/// </summary>
public class PixelCameraIntro : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PixelClicker whose cube the camera looks at. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Header("Intro")]
    [Tooltip("Play the camera intro when the game starts. (The player can also switch it off in Settings.)")]
    [SerializeField] private bool playIntro = true;

    [Min(0f)]
    [Tooltip("Seconds to wait after Play is pressed (or after the game starts, without a title screen) before the camera starts moving.")]
    [SerializeField] private float startDelay = 0.5f;

    [Min(0.5f)]
    [Tooltip("Seconds the camera takes to move out to the normal view.")]
    [SerializeField] private float introSeconds = 3.4f;

    [Header("Start Position (close-up from the side)")]
    [Min(0.5f)]
    [Tooltip("How far from the cube the camera starts, in cube sizes.")]
    [SerializeField] private float closeDistance = 1.8f;

    [Tooltip("How high the camera starts, in cube sizes above the middle of the cube (0 = level with it).")]
    [SerializeField] private float closeHeightInCubes = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Which side the camera starts on. 0 = the camera's left, 1 = its right.")]
    [SerializeField] private float side = 1f;

    [Min(0f)]
    [Tooltip("Field of view at the start (degrees). 0 = the camera's normal field of view.")]
    [SerializeField] private float startFieldOfView = 0f;

    [Header("Back to the main menu")]
    [Min(0.5f)]
    [Tooltip("Seconds the camera takes to zoom back into the cube when the player quits to the main menu.")]
    [SerializeField] private float reverseSeconds = 2.4f;

    [Header("Skipping")]
    [Tooltip("On a brand-new (or restarted) game the cube can't be clicked during the opening zoom-out, so the intro tutorial tips can start cleanly.")]
    [SerializeField] private bool lockClicksOnNewGame = true;

    [Tooltip("A mouse click jumps straight to the normal view. Off by default so the cube can be clicked during the zoom-out without disturbing it.")]
    [SerializeField] private bool skipOnClick = false;

    private const string PrefIntro = "PixelClicker.Setting.CameraIntro";

    /// <summary>The player's Settings choice: play the camera intro at the start of the game.</summary>
    public static bool Enabled
    {
        get => PixelPrefs.GetInt(PrefIntro, 1) != 0;
        set { PixelPrefs.SetInt(PrefIntro, value ? 1 : 0); PixelPrefs.Save(); }
    }

    private Camera cam;
    private Vector3 finalPosition;
    private Quaternion finalRotation;
    private float finalFov, finalOrtho;
    private Vector3 pivot;
    private Transform cubeRotation;
    private float cubeSize = 1f;

    /// <summary>How far the cube reaches from its middle towards a camera in direction 'dir' (the box's support function; 'inv' = the inverse of the cube's rotation).</summary>
    private float BoxReach(Quaternion inv, Vector3 dir)
    {
        Vector3 l = inv * dir.normalized;
        return 0.5f * cubeSize * (Mathf.Abs(l.x) + Mathf.Abs(l.y) + Mathf.Abs(l.z));
    }
    private Vector3 startOffset, finalOffset;
    private float startOrtho, startFov;
    private bool prepared, done, canReverse, reversing;
    private float reverseTime;
    private System.Action reverseDone;
    private float waited, moved;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void Prepare()
    {
        prepared = true;
        if (clicker == null || !playIntro || !Enabled) { done = true; return; }
        cam = clicker.ViewCamera;
        if (cam == null || clicker.PixelTransform == null) { done = true; return; }

        // The scene's own pose is where the camera ends up.
        finalPosition = cam.transform.position;
        finalRotation = cam.transform.rotation;
        finalFov = cam.fieldOfView;
        finalOrtho = cam.orthographicSize;

        pivot = clicker.PixelTransform.position;
        float cube = Mathf.Max(0.1f, clicker.PixelBaseSize);
        cubeSize = cube;
        cubeRotation = clicker.PixelTransform;

        // Start to one side of the cube (horizontally), a little above its middle, looking at it.
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();
        float sign = Mathf.Lerp(-1f, 1f, side);
        startOffset = right * (sign * cube * closeDistance) + Vector3.up * (cube * closeHeightInCubes);
        finalOffset = finalPosition - pivot;

        startFov = startFieldOfView > 0f ? startFieldOfView : finalFov;
        startOrtho = finalOrtho * 0.3f;
        canReverse = true;
    }

    /// <summary>True while the opening zoom-out runs on a brand-new game: the cube can't be clicked yet.</summary>
    public static bool ClicksLocked { get; private set; }

    /// <summary>True while the camera is in the close-up or moving between it and the normal view (the view-bounds planes are wrong then).</summary>
    public static bool Moving { get => moving; private set { if (moving == value) return; moving = value; PixelGameMode.Refresh(); } }   // (set every frame while it runs: only a change refreshes)
    private static bool moving;

    /// <summary>How far the camera is from the title close-up (0) to its normal view (1); 1 whenever it is at rest. The fitted floors use it to blend their picture.</summary>
    public static float Progress { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ClicksLocked = false; Moving = false; Progress = 1f; }

    private bool fresh = true;

    /// <summary>
    /// While the camera is in the close-up or swinging between it and the normal view, gives the pose it will settle into (what the floor
    /// styles that fit the view should be fitted to, so they don't swim and stretch during the move). False once the camera is at rest.
    /// </summary>
    public bool TryGetFinalPose(out Vector3 position, out Quaternion rotation, out float fov, out float orthoSize)
    {
        position = finalPosition; rotation = finalRotation; fov = finalFov; orthoSize = finalOrtho;
        return (reversing || !done) && prepared;
    }

    /// <summary>
    /// Zooms the camera back to the close-up (the intro in reverse), then calls 'onDone'. Returns false (and does nothing)
    /// if there is no intro to reverse (switched off in Settings, or no camera) - the caller carries on without it.
    /// </summary>
    public bool PlayReverse(System.Action onDone)
    {
        if (!canReverse || reversing || cam == null || !Enabled || !PixelTitleWanderers.LiveBackground) return false;   // no live background: no camera swing back
        reversing = true;
        reverseTime = 0f;
        reverseDone = onDone;
        ClicksLocked = true;
        return true;
    }

    private void LateUpdate()
    {
        if (!PixelTitleWanderers.LiveBackground && prepared)
        {
            // Without the live background the title shows a picture of the floor instead of the scene, so there is no camera intro: the game simply fades in at its normal view.
            if (reversing) { reversing = false; ClicksLocked = false; System.Action callback = reverseDone; reverseDone = null; callback?.Invoke(); }
            if (!done) Finish();
            Moving = false;
            Progress = 1f;
            ClicksLocked = false;
            return;
        }
        Moving = reversing || !done;
        if (reversing)
        {
            reverseTime += Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // a long frame can't make the camera jump
            float rk = Mathf.Clamp01(reverseTime / reverseSeconds);
            Apply(1f - Smoother(rk));
            if (rk >= 1f)
            {
                reversing = false;
                done = false; // the title screen holds the close-up pose now and the next Play zooms out again
                moved = 0f;
                waited = 0f;
                ClicksLocked = false;
                System.Action callback = reverseDone;
                reverseDone = null;
                callback?.Invoke();
            }
            return;
        }
        if (done && canReverse && PixelTitleScreen.Showing) { done = false; moved = 0f; waited = 0f; }
        Moving = !done;
        if (done) { ClicksLocked = false; return; }
        if (!prepared) Prepare();
        if (done) return;

        // Wait on the title screen (and a moment after) in the close-up pose.
        if (PixelTitleScreen.Showing)
        {
            fresh = !AnyCollected();
            ClicksLocked = lockClicksOnNewGame && fresh;
            waited = 0f; Apply(0f); return;
        }
        if (waited < startDelay) { waited += Time.unscaledDeltaTime; Apply(0f); return; }

        if (skipOnClick && (PixelInput.LeftPressed() || PixelInput.RightPressed())) { Finish(); return; }

        moved += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float k = Mathf.Clamp01(moved / introSeconds);
        Apply(Smoother(k));
        if (k >= 1f) Finish();
    }

    /// <summary>A gentler ease than SmoothStep: zero speed AND zero acceleration at both ends (quintic).</summary>
    private static float Smoother(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    /// <summary>Puts the camera at 'k' of the way from the close-up (0) to the normal view (1).</summary>
    private void Apply(float k)
    {
        Progress = k;
        // Swing round the cube on an arc, worked out from yaw / pitch / distance (steady even when the start and end views are nearly opposite,
        // where a straight rotation between the two offsets can flip): the heading turns by the shorter way, the height and distance blend.
        float startLen = Mathf.Max(0.001f, startOffset.magnitude), endLen = Mathf.Max(0.001f, finalOffset.magnitude);
        float yaw0 = Mathf.Atan2(startOffset.x, startOffset.z) * Mathf.Rad2Deg, yaw1 = Mathf.Atan2(finalOffset.x, finalOffset.z) * Mathf.Rad2Deg;
        float pitch0 = Mathf.Asin(Mathf.Clamp(startOffset.y / startLen, -1f, 1f)) * Mathf.Rad2Deg, pitch1 = Mathf.Asin(Mathf.Clamp(finalOffset.y / endLen, -1f, 1f)) * Mathf.Rad2Deg;
        float yaw = (yaw0 + Mathf.DeltaAngle(yaw0, yaw1) * k) * Mathf.Deg2Rad;
        float pitch = Mathf.Lerp(pitch0, pitch1, k) * Mathf.Deg2Rad;
        // The distance is measured from the cube's NEAREST EXTENT, not its middle: a box seen corner-on reaches further towards the camera than
        // one seen face-on, so with a swing round it the picture used to swell a little. Stepping the surface distance evenly (plus the box's reach
        // in the viewing direction) keeps the pull-back clean.
        Quaternion inv = cubeRotation != null ? Quaternion.Inverse(cubeRotation.rotation) : Quaternion.identity;
        float reachStart = BoxReach(inv, startOffset), reachEnd = BoxReach(inv, finalOffset);
        Vector3 dirNow = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
        float len = Mathf.Lerp(startLen - reachStart, endLen - reachEnd, k) + BoxReach(inv, dirNow);
        Vector3 offset = dirNow * len;
        cam.transform.position = pivot + offset;

        // Aim at a point that slides from the cube to the spot the scene's own view axis passes at the cube's depth: the cube stays near the middle
        // for most of the swing and the end is exactly the scene's view, with no sudden turn (blending two rotations spun the picture).
        float depth = Mathf.Max(0.01f, finalOffset.magnitude);
        Vector3 finalAim = finalPosition + finalRotation * Vector3.forward * depth;
        float w = Smoother(k);
        w *= w;
        Vector3 aim = Vector3.Lerp(pivot, finalAim, w);
        Vector3 dir = aim - cam.transform.position;
        // The 'up' the camera is turned against: world up at the start, the scene camera's own up at the end. With a camera that looks straight
        // down (a top-down view) world up is useless there - the roll comes out arbitrary and the whole picture used to flip 180 degrees
        // in the last moment, when the camera snapped to its real pose. Blending to the final up makes the end of the swing exactly that pose.
        Vector3 upRef = Vector3.Slerp(Vector3.up, finalRotation * Vector3.up, Smoother(k));
        Vector3 lookDir = dir.sqrMagnitude > 1e-6f ? dir : Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(lookDir.normalized, upRef.normalized)) > 0.9999f) upRef = finalRotation * Vector3.right;   // (never parallel to the view direction)
        cam.transform.rotation = k >= 0.9999f ? finalRotation : Quaternion.LookRotation(lookDir, upRef);

        cam.fieldOfView = Mathf.Lerp(startFov, finalFov, k);
        if (cam.orthographic) cam.orthographicSize = Mathf.Lerp(startOrtho, finalOrtho, k);
    }

    private static bool AnyCollected()
    {
        PixelClicker c = PixelFind.First<PixelClicker>();
        if (c == null) return false;
        foreach (PixelClicker.PixelTier t in c.Tiers)
            if (t.totalCollected > 0d) return true;
        return false;
    }

    private void Finish()
    {
        ClicksLocked = false;
        Moving = false;
        Progress = 1f;
        cam.transform.SetPositionAndRotation(finalPosition, finalRotation);
        cam.fieldOfView = finalFov;
        if (cam.orthographic) cam.orthographicSize = finalOrtho;
        done = true;
    }

    private void OnDestroy()
    {
        ClicksLocked = false;
        Moving = false;
        Progress = 1f;
        // Never leave the camera stuck mid-move.
        if (cam != null && !done && prepared) Finish();
    }
}
