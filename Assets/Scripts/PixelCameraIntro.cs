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

    [Header("Skipping")]
    [Tooltip("A mouse click jumps straight to the normal view.")]
    [SerializeField] private bool skipOnInput = true;

    private const string PrefIntro = "PixelClicker.Setting.CameraIntro";

    /// <summary>The player's Settings choice: play the camera intro at the start of the game.</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefIntro, 1) != 0;
        set { PlayerPrefs.SetInt(PrefIntro, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    private Camera cam;
    private Vector3 finalPosition;
    private Quaternion finalRotation;
    private float finalFov, finalOrtho;
    private Vector3 pivot;
    private Vector3 startOffset, finalOffset;
    private float startOrtho, startFov;
    private bool prepared, done;
    private float waited, moved;

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
    }

    private void Prepare()
    {
        prepared = true;
        if (clicker == null || !playIntro || !Enabled) { done = true; return; }
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null || clicker.PixelTransform == null) { done = true; return; }

        // The scene's own pose is where the camera ends up.
        finalPosition = cam.transform.position;
        finalRotation = cam.transform.rotation;
        finalFov = cam.fieldOfView;
        finalOrtho = cam.orthographicSize;

        pivot = clicker.PixelTransform.position;
        float cube = Mathf.Max(0.1f, clicker.PixelBaseSize);

        // Start to one side of the cube (horizontally), a little above its middle, looking at it.
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        right.Normalize();
        float sign = Mathf.Lerp(-1f, 1f, side);
        startOffset = right * (sign * cube * closeDistance) + Vector3.up * (cube * closeHeightInCubes);
        finalOffset = finalPosition - pivot;

        startFov = startFieldOfView > 0f ? startFieldOfView : finalFov;
        startOrtho = finalOrtho * 0.3f;
    }

    private void LateUpdate()
    {
        if (done) return;
        if (!prepared) Prepare();
        if (done) return;

        // Wait on the title screen (and a moment after) in the close-up pose.
        if (PixelTitleScreen.Showing) { waited = 0f; Apply(0f); return; }
        if (waited < startDelay) { waited += Time.unscaledDeltaTime; Apply(0f); return; }

        if (skipOnInput && (PixelInput.LeftPressed() || PixelInput.RightPressed())) { Finish(); return; }

        moved += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(moved / introSeconds);
        Apply(Mathf.SmoothStep(0f, 1f, k));
        if (k >= 1f) Finish();
    }

    /// <summary>Puts the camera at 'k' of the way from the close-up (0) to the normal view (1).</summary>
    private void Apply(float k)
    {
        // Swing round the cube on an arc: the offset turns and its length changes smoothly.
        Vector3 offset = Vector3.Slerp(startOffset, finalOffset, k);
        offset = offset.normalized * Mathf.Lerp(startOffset.magnitude, finalOffset.magnitude, k);
        cam.transform.position = pivot + offset;

        // Look at the cube at first, then settle into the scene's own view direction.
        Quaternion lookAtCube = Quaternion.LookRotation(pivot - cam.transform.position, Vector3.up);
        cam.transform.rotation = Quaternion.Slerp(lookAtCube, finalRotation, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, k)));

        cam.fieldOfView = Mathf.Lerp(startFov, finalFov, k);
        if (cam.orthographic) cam.orthographicSize = Mathf.Lerp(startOrtho, finalOrtho, k);
    }

    private void Finish()
    {
        cam.transform.SetPositionAndRotation(finalPosition, finalRotation);
        cam.fieldOfView = finalFov;
        if (cam.orthographic) cam.orthographicSize = finalOrtho;
        done = true;
    }

    private void OnDestroy()
    {
        // Never leave the camera stuck mid-move.
        if (cam != null && !done && prepared) Finish();
    }
}
