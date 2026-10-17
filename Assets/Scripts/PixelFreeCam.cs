// ---- System notes ----
// PixelFreeCam.cs
//   FREE CAM (View window tick box 'Free cam', PixelViewKind.FreeCam): the scene camera is detached and flies about by hand while the game carries on and stays clickable. W / S move forward / back along the view, A / D strafe, Space up, C down,
//   Shift = fast; look round with the middle mouse button dragged (or the arrow keys); Ctrl + mouse wheel changes the flying speed. Like the orbit camera it keeps a hidden, disabled copy of the camera at its normal pose (PixelFreeCam.Home, read through
//   PixelOrbitCamera.HomeCamera) so the view bounds and fitted floors do not follow the wandering camera, and leaving it (another view tick, a takeover minigame, the title screen, the camera intro) snaps the camera back to that pose. It is never saved as the
//   player's view (PixelViewWindow.SaveKind skips it): the saved view (Orbital / Top down) is what comes back. The flat (orthographic) view is turned off while it is on (PixelOrthoView), like in first person.
// ---------------------------------------------------
using UnityEngine;

public class PixelFreeCam : MonoBehaviour
{
    [Min(0f)]
    [Tooltip("Flying speed in world units per second at the start. 0 = the coded default (6).")]
    [SerializeField] private float flySpeed = 0f;

    [Min(0f)]
    [Tooltip("Degrees per pixel of mouse drag while looking round (middle mouse button). 0 = the coded default (0.2).")]
    [SerializeField] private float lookDegreesPerPixel = 0f;

    [Min(0f)]
    [Tooltip("Degrees per second the arrow keys turn the view. 0 = the coded default (90).")]
    [SerializeField] private float keyDegreesPerSecond = 0f;

    public static bool Active { get; private set; }
    public static PixelFreeCam Instance { get; private set; }

    /// <summary>The hidden copy of the camera at its normal pose while the free cam is on (null otherwise).</summary>
    public static Camera Home { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = false; Instance = null; Home = null; }

    private PixelClicker clicker;
    private Camera cam;
    private Vector3 homePos;
    private Quaternion homeRot;
    private float homeFov, homeOrtho;
    private bool homeOrthographic;
    private float yaw, pitch, speed;
    private Vector2 lastPointer;

    private static float V(float value, float fallback) => value > 0f ? value : fallback;

    /// <summary>Starts the free cam from the camera's current (normal) pose. False when it can't now (title screen, intro, a takeover minigame...).</summary>
    public static bool TryEnter()
    {
        if (Active) return true;
        PixelClicker c = PixelFind.First<PixelClicker>();
        if (c == null || c.PixelTransform == null) return false;
        if (Instance == null) Instance = new GameObject("Free Cam").AddComponent<PixelFreeCam>();
        return Instance.Enter(c);
    }

    public static void Leave() { if (Active && Instance != null) Instance.Finish(); }

    private bool Enter(PixelClicker c)
    {
        clicker = c;
        PixelZoom.ResetNow();
        if (PixelPauseMenu.IsPaused || PixelCalm.CameraBusy) return false;
        foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Busy && m.NeedsNormalCamera) return false;
        cam = clicker.ViewCamera;
        if (cam == null) return false;
        homePos = cam.transform.position; homeRot = cam.transform.rotation;
        homeFov = cam.fieldOfView; homeOrtho = cam.orthographicSize; homeOrthographic = cam.orthographic;
        GameObject go = new GameObject("Free Cam Home Camera") { hideFlags = HideFlags.HideAndDontSave };
        Home = go.AddComponent<Camera>();
        Home.CopyFrom(cam);
        Home.enabled = false;
        go.transform.SetPositionAndRotation(homePos, homeRot);
        Vector3 e = homeRot.eulerAngles;
        yaw = e.y; pitch = e.x > 180f ? e.x - 360f : e.x;
        speed = V(flySpeed, 6f);
        lastPointer = PixelInput.PointerPosition();
        Active = true;
        return true;
    }

    private void Finish()
    {
        if (cam != null)
        {
            cam.transform.SetPositionAndRotation(homePos, homeRot);
            cam.fieldOfView = homeFov;
            if (homeOrthographic) cam.orthographicSize = homeOrtho;
        }
        if (Home != null) Destroy(Home.gameObject);
        Home = null;
        Active = false;
    }

    private void OnDestroy() { if (Instance == this) { if (Active) Finish(); Instance = null; } }

    private void Update()
    {
        if (!Active) return;
        if (cam == null || clicker == null || PixelCalm.CameraBusy) { Finish(); return; }
        if (Home != null) Home.aspect = cam.aspect;
        Vector2 pointer = PixelInput.PointerPosition();
        bool playable = !PixelPauseMenu.IsPaused && !PixelKeys.Typing && (Time.timeScale > 0f || PixelTimeStop.IsStopped);
        if (playable)
        {
            float dt = Time.unscaledDeltaTime;
            if (PixelInput.MiddleHeld() && !PixelInput.PointerOverUI())
            {
                Vector2 d = pointer - lastPointer;
                float k = V(lookDegreesPerPixel, 0.2f);
                yaw += d.x * k;
                pitch -= d.y * k;
            }
            float ks = V(keyDegreesPerSecond, 90f) * dt;
            yaw += ((PixelKeys.KeyHeld(KeyCode.RightArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.LeftArrow) ? 1f : 0f)) * ks;
            pitch += ((PixelKeys.KeyHeld(KeyCode.UpArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.DownArrow) ? 1f : 0f)) * ks;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            if (PixelInput.ControlHeld() && !PixelInput.PointerOverUI())
            {
                float scroll = PixelInput.ScrollY();
                if (Mathf.Abs(scroll) > 0.01f) speed = Mathf.Clamp(speed * (scroll > 0f ? 1.2f : 1f / 1.2f), 0.5f, 120f);
            }
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 move = Vector3.zero;
            if (PixelKeys.KeyHeld(KeyCode.W)) move += rot * Vector3.forward;
            if (PixelKeys.KeyHeld(KeyCode.S)) move -= rot * Vector3.forward;
            if (PixelKeys.KeyHeld(KeyCode.D)) move += rot * Vector3.right;
            if (PixelKeys.KeyHeld(KeyCode.A)) move -= rot * Vector3.right;
            if (PixelKeys.KeyHeld(KeyCode.Space)) move += Vector3.up;
            if (PixelKeys.KeyHeld(KeyCode.C)) move -= Vector3.up;
            float fast = PixelKeys.KeyHeld(KeyCode.LeftShift) || PixelKeys.KeyHeld(KeyCode.RightShift) ? 3f : 1f;
            if (move.sqrMagnitude > 0f) cam.transform.position += move.normalized * speed * fast * dt;
            cam.transform.rotation = rot;
        }
        lastPointer = pointer;
    }
}
