using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First person mode (added by <see cref="PixelClicker"/>.Awake). Press the First Person key (F, rebindable) to shrink to half the size of an
/// old pixel and run about on the floor: WASD / arrows to move, mouse to look, Space to jump, Shift to run, Escape or F to leave. Left-click
/// while the crosshair is on the clicker cube - however far away it is - clicks it. The game keeps running (old pixels still fall and
/// pile up; you can shoulder them along).
/// How it works: the normal camera keeps its pose (so the view bounds, fog and floor fitting are unaffected) but stops drawing; a second
/// camera at the player's eyes draws instead. The player is a CharacterController.
/// </summary>
public class PixelFirstPerson : MonoBehaviour
{
    [Header("First Person Mode")]
    [Tooltip("Tick to switch first person mode off completely.")]
    [SerializeField] private bool disableFirstPerson = false;

    [Min(0f)]
    [Tooltip("The player's height as a fraction of an old pixel's edge. 0 = the coded default (0.5: half the size of an old pixel).")]
    [SerializeField] private float heightFraction = 0f;

    [Min(0f)]
    [Tooltip("Walking speed in old-pixel widths per second. 0 = the coded default (5).")]
    [SerializeField] private float walkSpeed = 0f;

    [Min(0f)]
    [Tooltip("Running (hold Shift) speed multiplier. 0 = the coded default (1.8).")]
    [SerializeField] private float runFactor = 0f;

    [Min(0f)]
    [Tooltip("How high a jump goes, in old-pixel widths. 0 = the coded default (0.7).")]
    [SerializeField] private float jumpHeight = 0f;

    [Min(0f)]
    [Tooltip("Mouse look speed (degrees per pixel the mouse moves). 0 = the coded default (0.12).")]
    [SerializeField] private float lookSensitivity = 0f;

    [Range(0f, 120f)]
    [Tooltip("Field of view of the first person camera (degrees). 0 = the coded default (75).")]
    [SerializeField] private float fieldOfView = 0f;

    [Min(0f)]
    [Tooltip("Aim assist for the long-range click (degrees): the crosshair counts as on the cube when it is this close to the cube's edge. 0 = the coded default (3).")]
    [SerializeField] private float aimAssistDegrees = 0f;

    [Min(0f)]
    [Tooltip("How hard you shove old pixels you run into (speed given to them). 0 = the coded default (2.5).")]
    [SerializeField] private float pushSpeed = 0f;

    [Tooltip("Text shown at the bottom of the screen while in first person mode. {0} = the First Person key.")]
    [SerializeField] private string hintText = "WASD move  -  Shift run  -  Space jump  -  click while aiming at the cube to click it  -  {0} or Esc to leave";

    /// <summary>True while first person mode is on (other scripts step aside: cube clicks, grabbing, the hose, Time Slow's S key).</summary>
    public static bool Active { get; private set; }

    private PixelClicker clicker;
    private Camera mainCamera, fpCamera;
    private int savedMask;
    private GameObject player;
    private CharacterController controller;
    private float yaw, pitch, verticalSpeed, height, eyeHeight;
    private GameObject canvasRoot;
    private Image crosshair;
    private bool windowRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = false; }

    private static float V(float value, float fallback) => value > 0f ? value : fallback;

    private void Awake()
    {
        clicker = PixelFind.First<PixelClicker>();
        PixelWindows.Register(this, 120, () => Active, Exit); // Escape leaves first person mode before it would open the pause menu
        windowRegistered = true;
    }

    private void OnDestroy()
    {
        if (windowRegistered) PixelWindows.Unregister(this);
        if (Active) Exit();
    }

    private bool CanEnter()
    {
        if (disableFirstPerson || clicker == null || clicker.PixelTransform == null) return false;
        if (PixelPauseMenu.IsPaused || PixelTitleScreen.Showing || PixelMinigame.TakeoverActive || PixelBank.HoseOn) return false;
        if (Time.timeScale <= 0f && !PixelTimeStop.IsStopped) return false;
        if (PixelPets.PopupOpen || PixelGuideVendor.Visiting) return false;
        return true;
    }

    private static bool Typing()
    {
        if (EventSystem.current == null) return false;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        return selected != null && selected.GetComponent<TMP_InputField>() != null;
    }

    private void Update()
    {
        if (!Active)
        {
            if (!Typing() && PixelKeys.Pressed(PixelAction.FirstPerson) && CanEnter()) Enter();
            return;
        }

        if (player == null || fpCamera == null) { Exit(); return; }
        if (PixelMinigame.TakeoverActive || PixelTitleScreen.Showing) { Exit(); return; }
        if (!Typing() && PixelKeys.Pressed(PixelAction.FirstPerson)) { Exit(); return; }

        // Paused, a popup, a visitor... : give the mouse back until play resumes.
        bool playable = !PixelPauseMenu.IsPaused && (Time.timeScale > 0f || PixelTimeStop.IsStopped) && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting;
        SetCursor(playable);
        if (!playable) return;

        Look();
        Move();
        UpdateClick();
    }

    private void LateUpdate()
    {
        if (!Active || player == null || fpCamera == null) return;
        fpCamera.transform.SetPositionAndRotation(player.transform.position + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0f));
    }

    // ------------------------------------------------------------------
    // Entering and leaving
    // ------------------------------------------------------------------

    private void Enter()
    {
        mainCamera = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (mainCamera == null) return;
        PixelWindows.CloseAllExcept(this);

        float unit = clicker.OldPixelWorldSize;
        height = unit * V(heightFraction, 0.5f);
        eyeHeight = height * 0.9f;

        // Start on the floor in front of the cube, on the side the camera is on, looking at it.
        Vector3 cube = clicker.PixelTransform.position;
        Vector3 toCamera = mainCamera.transform.position - cube;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f) toCamera = Vector3.back;
        Vector3 spot = cube + toCamera.normalized * clicker.PixelBaseSize * 3f;
        float floorY = cube.y - clicker.PixelBaseSize;
        RaycastHit[] hits = Physics.RaycastAll(spot + Vector3.up * 30f, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<OldPixelInfo>() != null || h.collider.transform.IsChildOf(clicker.PixelTransform) || h.normal.y < 0.5f) continue;
            if (h.distance < best) { best = h.distance; floorY = h.point.y; }
        }
        spot.y = floorY + 0.01f;

        player = new GameObject("First Person Player");
        player.transform.position = spot;
        controller = player.AddComponent<CharacterController>();
        controller.height = height;
        controller.radius = height * 0.3f;
        controller.center = new Vector3(0f, height * 0.5f, 0f);
        controller.stepOffset = height * 0.25f;
        controller.skinWidth = Mathf.Max(0.001f, controller.radius * 0.08f);
        controller.minMoveDistance = 0f;
        player.AddComponent<PixelFirstPersonPush>().Speed = V(pushSpeed, 2.5f);

        Vector3 look = cube - (spot + Vector3.up * eyeHeight);
        yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
        pitch = 0f;
        verticalSpeed = 0f;

        // The normal camera stays put but draws nothing; a second one at the player's eyes draws the scene.
        GameObject cameraObject = new GameObject("First Person Camera");
        fpCamera = cameraObject.AddComponent<Camera>();
        fpCamera.CopyFrom(mainCamera);
        fpCamera.depth = mainCamera.depth + 1f;
        fpCamera.fieldOfView = V(fieldOfView, 75f);
        fpCamera.nearClipPlane = Mathf.Max(0.001f, height * 0.06f);
        fpCamera.transform.SetPositionAndRotation(spot + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0f));
        savedMask = mainCamera.cullingMask;
        mainCamera.cullingMask = 0;

        BuildHud();
        Active = true;
        SetCursor(true);
    }

    private void Exit()
    {
        bool was = Active;
        Active = false;
        if (mainCamera != null) mainCamera.cullingMask = savedMask;
        if (fpCamera != null) Destroy(fpCamera.gameObject);
        if (player != null) Destroy(player);
        if (canvasRoot != null) Destroy(canvasRoot);
        fpCamera = null; player = null; controller = null; canvasRoot = null;
        if (was) SetCursor(false);
    }

    private static void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ------------------------------------------------------------------
    // Look, move, click
    // ------------------------------------------------------------------

    private void Look()
    {
        Vector2 delta;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
        float s = V(lookSensitivity, 0.12f);
        yaw += delta.x * s;
        pitch = Mathf.Clamp(pitch - delta.y * s, -85f, 85f);
    }

    private static bool Held(KeyCode a, KeyCode b = KeyCode.None) => PixelKeys.KeyHeld(a) || (b != KeyCode.None && PixelKeys.KeyHeld(b));

    private void Move()
    {
        float unit = clicker.OldPixelWorldSize;
        float forward = (Held(KeyCode.W, KeyCode.UpArrow) ? 1f : 0f) - (Held(KeyCode.S, KeyCode.DownArrow) ? 1f : 0f);
        float right = (Held(KeyCode.D, KeyCode.RightArrow) ? 1f : 0f) - (Held(KeyCode.A, KeyCode.LeftArrow) ? 1f : 0f);
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 dir = flat * new Vector3(right, 0f, forward);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        float speed = unit * V(walkSpeed, 5f) * (Held(KeyCode.LeftShift, KeyCode.RightShift) ? V(runFactor, 1.8f) : 1f);

        bool grounded = controller.isGrounded;
        if (grounded && verticalSpeed < 0f) verticalSpeed = -1f;
        if (grounded && PixelKeys.KeyPressed(KeyCode.Space)) verticalSpeed = Mathf.Sqrt(2f * -Physics.gravity.y * unit * V(jumpHeight, 0.7f));
        float dt = Time.unscaledDeltaTime; // so you can still run about while time is stopped
        verticalSpeed += Physics.gravity.y * dt;

        controller.Move((dir * speed + Vector3.up * verticalSpeed) * dt);
    }

    private void UpdateClick()
    {
        bool aiming = AimingAtCube();
        if (crosshair != null) crosshair.color = aiming ? new Color(0.4f, 1f, 0.5f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
        if (aiming && PixelInput.LeftPressed()) clicker.ManualClickFromAfar();
    }

    /// <summary>Is the crosshair on the clicker cube (any distance, a little aim assist, whatever stands in the way)?</summary>
    private bool AimingAtCube()
    {
        Transform cube = clicker.PixelTransform;
        if (cube == null || fpCamera == null) return false;
        Vector3 to = cube.position - fpCamera.transform.position;
        float distance = to.magnitude;
        if (distance < 0.0001f) return true;
        float radius = clicker.PixelBaseSize * 0.87f; // about the cube's half diagonal
        float limit = Mathf.Atan2(radius, distance) * Mathf.Rad2Deg + V(aimAssistDegrees, 3f);
        return Vector3.Angle(fpCamera.transform.forward, to) <= limit;
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------

    private void BuildHud()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelFirstPerson Canvas", 300, new Vector2(1920f, 1080f), false);
        GameObject dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(canvasRoot.transform, false);
        crosshair = dot.GetComponent<Image>();
        crosshair.color = new Color(1f, 1f, 1f, 0.7f);
        crosshair.raycastTarget = false;
        RectTransform rt = dot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(10f, 10f);

        TMP_Text hint = PixelUIKit.CreateText(clicker.UIFont, canvasRoot.transform, "Hint", PixelKeys.Replace(string.Format(hintText, PixelKeys.Name(PixelAction.FirstPerson))),
                                              26f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(1f, 1f, 1f, 0.75f));
        hint.raycastTarget = false;
        hint.color = Color.white;
        hint.outlineWidth = 0.3f; // a dark edge so it stays readable over pale floors
        hint.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 44f);
        hr.anchoredPosition = new Vector2(0f, (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f) + 14f);
    }
}

/// <summary>Lets the first person player shove old pixels it runs into.</summary>
public class PixelFirstPersonPush : MonoBehaviour
{
    public float Speed = 2.5f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider != null ? hit.collider.attachedRigidbody : null;
        if (body == null || body.isKinematic) return;
        Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (push.sqrMagnitude < 0.0001f) return;
        body.AddForce(push.normalized * Speed, ForceMode.VelocityChange);
    }
}
