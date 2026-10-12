// ---- System notes ----
// PixelZoom.cs
//   Zoom key (added by PixelClicker.Awake; rebindable key PixelAction.Zoom, default X; disableZoom): PRESS it to step through the zoom
//   levels (zoomLevels, empty = 1 / 0.6 / 0.35 = fractions of the normal camera distance; the step happens when the key is let go, and
//   only if the wheel was not used meanwhile), HOLD it and use the mouse wheel to zoom smoothly anywhere between the closest and the
//   furthest level. The real camera slides along the line from the cube to its normal position (the cube keeps its spot on screen;
//   orthographic cameras scale their size); `followSharpness` smooths it. While zoomed PixelZoom.HomeCamera (a hidden copy at the normal
//   pose, also returned by PixelOrbitCamera.HomeCamera) is what PixelViewBounds / PixelFloor.FitToView read, so old pixels stay inside
//   the NORMAL view. The zoom snaps back to normal (ResetNow) when the title screen, the camera intro, first person, the orbit camera
//   or a takeover minigame needs the camera.
// ---------------------------------------------------
using UnityEngine;

/// <summary>
/// Zoom with the X key: press to step through three zoom levels, hold and scroll the mouse wheel to zoom to any level between the closest and furthest.
/// </summary>
public class PixelZoom : MonoBehaviour
{
    [Header("Zoom")]
    [Tooltip("Turn the zoom key off.")]
    [SerializeField] private bool disableZoom = false;

    [Tooltip("The zoom levels as fractions of the normal camera distance (1 = normal, smaller = closer). Press the key to step through them in this order; holding the key + the wheel moves between the smallest and the biggest. Empty = 1, 0.6, 0.35.")]
    [SerializeField] private float[] zoomLevels = { };

    [Min(0f)]
    [Tooltip("How far OUT the hold-the-key + wheel zoom may go, as a fraction of the normal camera distance (2 = twice as far). The press-to-step levels above are not affected. 0 = the default (2.2).")]
    [SerializeField] private float maxZoomOut = 0f;

    [Min(0f)]
    [Tooltip("How quickly the camera follows the zoom (bigger = snappier). 0 = the default (10).")]
    [SerializeField] private float followSharpness = 0f;

    [Min(0f)]
    [Tooltip("How much one notch of the wheel changes the zoom while the key is held (0.1 = 10%). 0 = the default (0.1).")]
    [SerializeField] private float wheelStep = 0f;

    /// <summary>True while the camera is zoomed away from its normal view.</summary>
    public static bool Zoomed { get; private set; }

    /// <summary>A hidden, disabled copy of the camera at its normal pose while zoomed (null otherwise).</summary>
    public static Camera HomeCamera { get; private set; }

    private static PixelZoom instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Zoomed = false; HomeCamera = null; instance = null; }

    private PixelClicker clicker;
    private Camera cam;
    private Vector3 homePos, pivot;
    private Quaternion homeRot;
    private float homeOrtho;
    private float factor = 1f, target = 1f;
    private bool wasHeld, scrolled;

    private static float V(float value, float fallback) => value > 0f ? value : fallback;

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        if (instance == this) { RestoreCamera(); instance = null; }
    }

    /// <summary>Puts the camera back to its normal view at once (the orbit camera, first person and the title screen start from the normal view).</summary>
    public static void ResetNow()
    {
        if (instance != null) instance.Snap();
    }

    private float Closest { get { float m = float.MaxValue; foreach (float f in Levels()) m = Mathf.Min(m, f); return m; } }
    private float Furthest { get { float m = maxZoomOut > 0f ? maxZoomOut : 2.2f; foreach (float f in Levels()) m = Mathf.Max(m, f); return m; } }

    private float[] Levels() => zoomLevels != null && zoomLevels.Length >= 2 ? zoomLevels : new[] { 1f, 0.6f, 0.35f };

    private bool NeedsNormalView()
    {
        if (PixelTitleScreen.Showing || PixelTitleScreen.Reversing || PixelCameraIntro.Moving || PixelMinigame.TakeoverActive) return true;
        if (PixelFirstPerson.Active || PixelOrbitCamera.Active) return true;
        foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Busy && m.Takeover) return true;
        return false;
    }

    private void Update()
    {
        if (disableZoom || clicker == null) return;
        if (NeedsNormalView())
        {
            if (Zoomed || Mathf.Abs(target - 1f) > 0.0001f) Snap();
            wasHeld = false;
            return;
        }

        bool playable = !PixelPauseMenu.IsPaused && !PixelKeys.Typing && (Time.timeScale > 0f || PixelTimeStop.IsStopped)
                        && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting && !PixelTutorialDim.Active;
        bool held = playable && PixelKeys.Held(PixelAction.Zoom);
        if (held)
        {
            if (!wasHeld) scrolled = false;
            float scroll = PixelInput.ScrollY();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                scrolled = true;
                float step = V(wheelStep, 0.1f);
                target *= scroll > 0f ? 1f - step : 1f + step;
                target = Mathf.Clamp(target, Closest, Furthest);
            }
        }
        else if (wasHeld && playable && !scrolled)
        {
            // A plain press (the key came up without any wheel use): the next level.
            float[] levels = Levels();
            int nearest = 0;
            for (int i = 1; i < levels.Length; i++) if (Mathf.Abs(levels[i] - target) < Mathf.Abs(levels[nearest] - target)) nearest = i;
            target = levels[(nearest + 1) % levels.Length];
        }
        wasHeld = held;
    }

    private void LateUpdate()
    {
        if (disableZoom || clicker == null || clicker.PixelTransform == null) return;
        if (NeedsNormalView()) return;
        if (cam == null) cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return;

        bool atNormal = !Zoomed && Mathf.Approximately(factor, 1f);
        if (atNormal)
        {
            // Idle at the normal view: remember where it is (the cube keeps its spot on screen while we slide towards it).
            homePos = cam.transform.position;
            homeRot = cam.transform.rotation;
            homeOrtho = cam.orthographicSize;
            pivot = clicker.CubeBasePosition;
            if (Mathf.Abs(target - 1f) < 0.0001f) return;
        }

        float f = 1f - Mathf.Exp(-V(followSharpness, 10f) * Time.unscaledDeltaTime);
        factor = Mathf.Lerp(factor, target, f);
        if (Mathf.Abs(factor - target) < 0.0005f) factor = target;

        if (Mathf.Approximately(factor, 1f) && Mathf.Approximately(target, 1f))
        {
            RestoreCamera();
            return;
        }

        if (!Zoomed) BeginZoomed();
        if (HomeCamera != null) HomeCamera.aspect = cam.aspect;
        cam.transform.position = pivot + (homePos - pivot) * factor;
        if (cam.orthographic) cam.orthographicSize = homeOrtho * factor;
    }

    private void BeginZoomed()
    {
        Zoomed = true;
        GameObject go = new GameObject("Zoom Home Camera") { hideFlags = HideFlags.HideAndDontSave };
        HomeCamera = go.AddComponent<Camera>();
        HomeCamera.CopyFrom(cam);
        HomeCamera.enabled = false;
        go.transform.SetPositionAndRotation(homePos, homeRot);
    }

    /// <summary>Back to the normal view at once, and forget the zoom.</summary>
    private void Snap()
    {
        target = 1f;
        if (Zoomed || !Mathf.Approximately(factor, 1f))
        {
            if (cam != null && Zoomed && !PixelCameraIntro.Moving)
            {
                cam.transform.position = homePos;
                if (cam.orthographic) cam.orthographicSize = homeOrtho;
            }
        }
        RestoreCamera(false);
    }

    private void RestoreCamera(bool putBack = true)
    {
        if (putBack && cam != null && Zoomed)
        {
            cam.transform.position = homePos;
            if (cam.orthographic) cam.orthographicSize = homeOrtho;
        }
        factor = target = 1f;
        Zoomed = false;
        if (HomeCamera != null) Destroy(HomeCamera.gameObject);
        HomeCamera = null;
    }
}
