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
    [Tooltip("The player's height as a fraction of an old pixel's edge. 0 = the coded default (1.5: one and a half old pixels tall, three times the old size).")]
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
    [Tooltip("The fastest an old pixel is shoved when you run into it, in old-pixel widths per second (it is pushed along gently and never sent flying). 0 = the coded default (3).")]
    [SerializeField] private float pushSpeed = 0f;

    /// <summary>True while first person mode is on (other scripts step aside: cube clicks, grabbing, the hose, Time Slow's S key).</summary>
    [Min(0)]
    [Tooltip("(Not used any more: you hold ONE cube, which you can move away / closer with the wheel, turn with the right button and stick to other cubes.)")]
    [SerializeField] private int maxCarried = 0;

    [Min(0f)]
    [Tooltip("How far away you can pick a cube up, in your own heights. 0 = the coded default (3.5).")]
    [SerializeField] private float carryReach = 0f;

    [Min(0f)]
    [Tooltip("Speed of a thrown cube, in old-pixel widths per second. 0 = the coded default (7).")]
    [SerializeField] private float throwSpeed = 0f;

    [Header("Build controls (holding a cube)")]
    [Min(0f)]
    [Tooltip("How far one wheel notch moves the held cube, in old-pixel widths. 0 = the coded default (0.35).")]
    [SerializeField] private float wheelStep = 0f;

    [Min(0f)]
    [Tooltip("How fast the held cube turns while the right button is held, in degrees per pixel the mouse moves. 0 = the coded default (0.35).")]
    [SerializeField] private float turnSensitivity = 0f;

    [Range(5f, 60f)]
    [Tooltip("How close to a grid-aligned orientation the held cube must be (degrees) for its attach ghost to show.")]
    [SerializeField] private float attachAngle = 28f;

    [Min(0f)]
    [Tooltip("How close (in old-pixel widths) the held cube must be to a spot on another cube's face for the attach ghost to show. 0 = the coded default (0.9).")]
    [SerializeField] private float attachDistance = 0f;

    [Min(0f)]
    [Tooltip("Seconds the right button is held on a stuck cube to pry it loose. 0 = the coded default (0.7).")]
    [SerializeField] private float pryHoldSeconds = 0f;

    public static bool Active { get; private set; }

    private static PixelFirstPerson instance;

    /// <summary>Where the first person player stands (false when not in first person mode). The floor fades out in a circle round it.</summary>
    public static bool TryGetPlayerPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (!Active || instance == null || instance.player == null) return false;
        position = instance.player.transform.position;
        return true;
    }

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
    private static void ResetStatics() { Active = false; instance = null; }

    private static float V(float value, float fallback) => value > 0f ? value : fallback;

    /// <summary>How much bigger than the original (half an old pixel tall) player this one is: walking and jumping scale with it.</summary>
    private float SizeScale => V(heightFraction, 1.5f) / 0.5f;

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.First<PixelClicker>();
        PixelWindows.Register(this, 120, () => Active, Exit); // Escape leaves first person mode before it would open the pause menu
        windowRegistered = true;
    }

    private void OnDestroy()
    {
        if (windowRegistered) PixelWindows.Unregister(this);
        if (Active) Exit();
    }

    /// <summary>The View window switches first person mode on. False when it can't start now.</summary>
    public static bool TryEnter()
    {
        if (instance == null || Active || !instance.CanEnter()) return Active;
        instance.Enter();
        return Active;
    }

    /// <summary>The View window switches first person mode off.</summary>
    public static void Leave()
    {
        if (instance != null && Active) instance.Exit();
    }

    private bool CanEnter()
    {
        if (disableFirstPerson || clicker == null || clicker.PixelTransform == null) return false;
        if (PixelPauseMenu.IsPaused || PixelTitleScreen.Showing || PixelMinigame.TakeoverActive || PixelBank.HoseOn || PixelRobotWorker.Piloting) return false;
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
        if (!Active) return;   // (the View window switches first person on and off: TryEnter / Leave)

        if (player == null || fpCamera == null) { Exit(); return; }
        if (PixelMinigame.TakeoverActive || PixelTitleScreen.Showing) { Exit(); return; }
        // Paused, a popup, a visitor... : give the mouse back until play resumes.
        bool playable = !PixelPauseMenu.IsPaused && (Time.timeScale > 0f || PixelTimeStop.IsStopped) && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting && !PixelViewWindow.IsOpen;   // (the View window needs the mouse)
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
        PlaceCarried();
    }

    // ------------------------------------------------------------------
    // Entering and leaving
    // ------------------------------------------------------------------

    private void Enter()
    {
        PixelZoom.ResetNow();
        PixelOrthoView.ForcePerspectiveNow();   // (first person is always the normal 3D view)
        mainCamera = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (mainCamera == null) return;
        PixelWindows.CloseAllExcept(this);

        float unit = clicker.OldPixelWorldSize;
        height = unit * V(heightFraction, 1.5f);
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
        player.AddComponent<PixelFirstPersonPush>().Speed = V(pushSpeed, 3f) * unit;

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
        DropAll();
        Active = false;
        if (mainCamera != null) mainCamera.cullingMask = savedMask;
        if (fpCamera != null) Destroy(fpCamera.gameObject);
        if (player != null) Destroy(player);
        if (canvasRoot != null) Destroy(canvasRoot);
        if (attachGhost != null) Destroy(attachGhost);
        attachGhost = null; hintLabel = null;
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
        if (carried.Count > 0 && PixelInput.RightHeld())
        {
            // Right button held while holding a cube: the mouse turns the CUBE (in the view's own up / right directions), not the view.
            float ts = V(turnSensitivity, 0.35f);
            Transform ct = fpCamera.transform;
            heldRotation = Quaternion.AngleAxis(delta.x * ts, ct.up) * Quaternion.AngleAxis(-delta.y * ts, ct.right) * heldRotation;
            return;
        }
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
        float speed = unit * SizeScale * V(walkSpeed, 5f) * (Held(KeyCode.LeftShift, KeyCode.RightShift) ? V(runFactor, 1.8f) : 1f);

        bool grounded = controller.isGrounded;
        if (grounded && verticalSpeed < 0f) verticalSpeed = -1f;
        if (grounded && PixelKeys.KeyPressed(KeyCode.Space)) verticalSpeed = Mathf.Sqrt(2f * -Physics.gravity.y * unit * SizeScale * V(jumpHeight, 0.7f));
        float dt = Time.unscaledDeltaTime; // so you can still run about while time is stopped
        verticalSpeed += Physics.gravity.y * dt;

        controller.Move((dir * speed + Vector3.up * verticalSpeed) * dt);
    }

    private void UpdateClick()
    {
        bool aiming = AimingAtCube();
        bool holding = carried.Count > 0;
        Rigidbody target = !holding ? FindPickup(false) : null;
        Rigidbody stuck = !holding && target == null ? FindPickup(true) : null;   // a cube that is glued to others (pry it off with the right button)

        if (holding) UpdateHolding();
        else
        {
            HideAttachGhost();
            if (PixelInput.LeftPressed())
            {
                if (target != null) PickUp(target);       // left click grabs a cube...
                else if (aiming) clicker.ManualClickFromAfar();   // ...or clicks the clicker
            }
            // Hold the right button on a stuck cube to pry it loose.
            if (PixelHold.Update(this, stuck, V(pryHoldSeconds, 0.7f), "", new Color(1f, 0.85f, 0.3f), true) && stuck != null)
            {
                PixelAttachment.Detach(stuck);
                PixelAudio.Play("device_remove");
            }
        }

        if (crosshair != null)
            crosshair.color = holding ? new Color(1f, 1f, 1f, 0.5f) : aiming && target == null ? new Color(0.4f, 1f, 0.5f, 0.95f)
                            : target != null ? new Color(1f, 0.85f, 0.25f, 0.95f) : stuck != null ? new Color(0.4f, 0.85f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
        UpdateHint(holding, target != null, stuck != null, aiming);
    }

    /// <summary>While a cube is in hand: the wheel moves it, the right button turns it (see Look), and left click attaches it (when its white ghost shows) or sets it down.</summary>
    private void UpdateHolding()
    {
        Rigidbody body = carried[0];
        if (body == null) { carried.Clear(); return; }
        float unit = clicker.OldPixelWorldSize;
        float scroll = PixelInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float reach = height * V(carryReach, 3.5f);
            holdDistance = Mathf.Clamp(holdDistance + Mathf.Sign(scroll) * unit * V(wheelStep, 0.35f), unit * 1.1f, reach);
        }

        // The white ghost where it would stick.
        bool snapped = PixelAttachment.TrySnap(body, heldHalf, clicker.OldPixels, unit, attachAngle, unit * V(attachDistance, 0.9f), out PixelAttachment.Snap snap);
        UpdateAttachGhost(snapped, snap);

        if (PixelInput.LeftPressed())
        {
            if (snapped) AttachHeld(body, snap);
            else Release(body, false);
        }
    }

    private void AttachHeld(Rigidbody body, PixelAttachment.Snap snap)
    {
        carried.Remove(body);
        foreach (Collider c in body.GetComponentsInChildren<Collider>()) c.enabled = true;
        body.transform.SetPositionAndRotation(snap.position, snap.rotation);
        body.isKinematic = false;
        SetBodyVelocity(body, Vector3.zero);
        ScaledGravity gravity = body.GetComponent<ScaledGravity>();
        if (gravity != null) gravity.enabled = true;
        OldPixelShatter shatter = body.GetComponent<OldPixelShatter>();
        if (shatter != null) shatter.Released(true, 3f);   // (a glass cube is set down gently)
        PixelAttachment.Attach(body, snap.target);
        HideAttachGhost();
        PixelAudio.Play("device_place");
    }

    // ---- the white attach ghost ----

    private GameObject attachGhost;
    private Material attachGhostMat;
    private Vector3 heldHalf = Vector3.one * 0.5f;
    private Quaternion heldRotation = Quaternion.identity;
    private float holdDistance;

    private void UpdateAttachGhost(bool show, PixelAttachment.Snap snap)
    {
        if (!show) { HideAttachGhost(); return; }
        if (attachGhost == null)
        {
            attachGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            attachGhost.name = "Attach Ghost";
            Collider c = attachGhost.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Shader sh = PixelShaders.SpriteDefault();
            attachGhostMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            Renderer r = attachGhost.GetComponent<Renderer>();
            r.sharedMaterial = attachGhostMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        attachGhost.SetActive(true);
        attachGhost.transform.SetPositionAndRotation(snap.position, snap.rotation);
        attachGhost.transform.localScale = heldHalf * 2f * 1.02f;
        attachGhostMat.color = new Color(1f, 1f, 1f, 0.3f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
    }

    private void HideAttachGhost() { if (attachGhost != null && attachGhost.activeSelf) attachGhost.SetActive(false); }

    // ---- a short hint line ----

    private TMP_Text hintLabel;

    private void UpdateHint(bool holding, bool canGrab, bool canPry, bool aimingCube)
    {
        if (hintLabel == null) return;
        string text = holding ? "Wheel: closer / farther     Hold right + mouse: turn     Left: stick it where the white ghost shows, or put it down"
                    : canGrab ? "Left click: pick up this cube"
                    : canPry ? "Hold right click: pry this cube loose"
                    : aimingCube ? "Left click: click the cube" : "";
        if (hintLabel.text != text) hintLabel.text = text;
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
    // Carrying cubes
    // ------------------------------------------------------------------

    private readonly System.Collections.Generic.List<Rigidbody> carried = new System.Collections.Generic.List<Rigidbody>();
    private int CarryMax => 1;   // one cube in hand now (the build controls: scroll = distance, hold right = turn, left = attach / put down)

    private static void SetBodyVelocity(Rigidbody rb, Vector3 v)
    {
        if (rb == null || rb.isKinematic) return;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>The old pixel the crosshair is nearest to (within reach and a small angle), or null.</summary>
    private Rigidbody FindPickup(bool attachedOnly)
    {
        if (fpCamera == null) return null;
        float reach = height * V(carryReach, 3.5f);
        Vector3 origin = fpCamera.transform.position, forward = fpCamera.transform.forward;
        Rigidbody best = null;
        float bestScore = float.MaxValue;
        var list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody body = list[i];
            if (body == null || body.isKinematic || carried.Contains(body) || clicker.IsFlyingPixel(body)) continue;
            if (PixelAttachment.IsAttached(body) != attachedOnly) continue;   // free cubes are picked up, stuck ones are pried off
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && (despawn.IsDespawning || despawn.Held)) continue;
            Vector3 to = body.worldCenterOfMass - origin;
            float distance = to.magnitude;
            if (distance > reach || distance < 0.0001f) continue;
            float radius = clicker.OldPixelWorldSize * 0.75f;
            float limit = Mathf.Atan2(radius, distance) * Mathf.Rad2Deg + 4f;
            float angle = Vector3.Angle(forward, to);
            if (angle > limit) continue;
            float score = angle + distance * 0.05f;
            if (score < bestScore) { bestScore = score; best = body; }
        }
        return best;
    }

    private void PickUp(Rigidbody body)
    {
        PixelStats.Count("old.grabbed");
        clicker.LandFlyingPixel(body);
        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.Held = true;
        ScaledGravity gravity = body.GetComponent<ScaledGravity>();
        if (gravity != null) gravity.enabled = false;
        OldPixelShatter shatter = body.GetComponent<OldPixelShatter>();
        if (shatter != null) shatter.Grabbed();
        foreach (Collider c in body.GetComponentsInChildren<Collider>()) c.enabled = false;   // a carried cube pushes nothing and nothing pushes it
        SetBodyVelocity(body, Vector3.zero);
        body.isKinematic = true;
        carried.Add(body);
        heldRotation = body.transform.rotation;
        heldHalf = PixelAttachment.HalfExtents(body, clicker.OldPixelWorldSize);
        holdDistance = Mathf.Clamp(Vector3.Distance(fpCamera.transform.position, body.transform.position), clicker.OldPixelWorldSize * 1.1f, height * V(carryReach, 3.5f));
        PixelAudio.Play("grab");
    }

    /// <summary>Lets go of the cube on top of the stack; 'throwIt' sends it ahead, else it is set down in front of you.</summary>
    private void Release(Rigidbody body, bool throwIt)
    {
        if (body == null) return;
        carried.Remove(body);
        foreach (Collider c in body.GetComponentsInChildren<Collider>()) c.enabled = true;
        body.isKinematic = false;
        ScaledGravity gravity = body.GetComponent<ScaledGravity>();
        if (gravity != null) gravity.enabled = true;
        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.Held = false;
        Vector3 forward = fpCamera != null ? fpCamera.transform.forward : Vector3.forward;
        float unit = clicker.OldPixelWorldSize;
        Vector3 velocity = throwIt ? (forward + Vector3.up * 0.25f).normalized * unit * V(throwSpeed, 7f) : forward * unit * 0.6f;
        SetBodyVelocity(body, velocity);
        OldPixelShatter shatter = body.GetComponent<OldPixelShatter>();
        if (shatter != null) shatter.Released(!throwIt, 3f);
        PixelAudio.Play("drop");
    }

    private void DropTop(bool throwIt) { if (carried.Count > 0) Release(carried[carried.Count - 1], throwIt); }
    private void ThrowTop() => DropTop(true);

    private void DropAll()
    {
        for (int i = carried.Count - 1; i >= 0; i--) Release(carried[i], false);
        carried.Clear();
    }

    /// <summary>Holds the cube straight ahead along the view at the held distance, turned the way the player turned it.</summary>
    private void PlaceCarried()
    {
        if (fpCamera == null || carried.Count == 0) return;
        Rigidbody body = carried[0];
        if (body == null) { carried.Clear(); return; }
        Transform ct = fpCamera.transform;
        Vector3 target = ct.position + ct.forward * holdDistance;
        float follow = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        body.transform.position = Vector3.Lerp(body.transform.position, target, follow);
        body.transform.rotation = heldRotation;
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

        hintLabel = PixelUIKit.CreateText(clicker.UIFont, canvasRoot.transform, "Hint", "", 26f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.9f));
        hintLabel.raycastTarget = false;
        RectTransform hr = hintLabel.rectTransform;
        hr.anchorMin = new Vector2(0.1f, 0f); hr.anchorMax = new Vector2(0.9f, 0f); hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 44f);
        hr.anchoredPosition = new Vector2(0f, 110f);
    }
}

/// <summary>
/// Lets the first person player shove old pixels it runs into - gently: a pixel is only brought up to a speed about as fast as the player walks
/// (never more than <see cref="Speed"/>), straight along the way the player moves, with no lift and no spin, so nothing goes flying.
/// </summary>
public class PixelFirstPersonPush : MonoBehaviour
{
    /// <summary>The fastest a shoved pixel is made to go (world units per second).</summary>
    public float Speed = 3f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider != null ? hit.collider.attachedRigidbody : null;
        if (body == null || body.isKinematic) return;
        if (hit.moveDirection.y < -0.3f) return;   // standing on it: no shove
        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
#if UNITY_6000_0_OR_NEWER
        Vector3 v = body.linearVelocity;
#else
        Vector3 v = body.velocity;
#endif
        float along = Vector3.Dot(new Vector3(v.x, 0f, v.z), dir);
        if (along < Speed)
        {
            // bring it up towards the cap smoothly (a part of the gap each contact), keep whatever else it is doing (falling, sliding sideways)
            v += dir * (Speed - along) * 0.35f;
        }
        // never faster than the cap sideways, and no extra spin
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.magnitude > Speed) { flat = flat.normalized * Speed; v = new Vector3(flat.x, v.y, flat.z); }
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = v;
#else
        body.velocity = v;
#endif
        body.angularVelocity *= 0.6f;
    }
}
