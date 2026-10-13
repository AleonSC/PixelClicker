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
    [Tooltip("How high a jump goes, in old-pixel widths. 0 = the coded default (0.45).")]
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
    [Tooltip("Speed of a full-power throw, in old-pixel widths per second. 0 = the coded default (14).")]
    [SerializeField] private float throwSpeed = 0f;

    [Min(0f)]
    [Tooltip("Seconds of holding the left button (after the first quarter second) to reach full throwing power. 0 = the coded default (1.2).")]
    [SerializeField] private float throwChargeSeconds = 0f;

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

    [Header("Weight and head bob")]
    [Min(0f)]
    [Tooltip("How heavy the player feels: gravity is multiplied by this (2 = double the mass, a snappier fall and a shorter jump air time). 0 = the coded default (2).")]
    [SerializeField] private float gravityFactor = 0f;

    [Min(0f)]
    [Tooltip("How far the view bobs up and down while walking, as a fraction of eye height. 0 = the coded default (0.022); use a tiny number like 0.0001 to turn it almost off.")]
    [SerializeField] private float headBobAmount = 0f;

    [Min(0f)]
    [Tooltip("Head bob steps per walking-speed unit: bigger = quicker bobbing. 0 = the coded default (0.8).")]
    [SerializeField] private float headBobRate = 0f;

    [Header("Neon floor (first person)")]
    [Tooltip("Colour of the neon grid that replaces the floor in first person.")]
    [SerializeField] private Color gridColor = new Color(0.2f, 1f, 0.9f, 1f);

    [Min(0f)]
    [Tooltip("Distance between the grid lines, in cube widths. 0 = the coded default (1).")]
    [SerializeField] private float gridSpacingCubes = 0f;

    [Min(0f)]
    [Tooltip("How far the grid shows round the player and the clicker cube, in cube widths. 0 = the coded default (7).")]
    [SerializeField] private float gridPlayerRadiusCubes = 0f;

    [Min(0f)]
    [Tooltip("How far the grid shows round each old pixel lying on the ground, in cube widths. 0 = the coded default (2.5).")]
    [SerializeField] private float gridPixelRadiusCubes = 0f;

    [Min(0f)]
    [Tooltip("Brightness of the grid. 0 = the coded default (1.2).")]
    [SerializeField] private float gridBrightness = 0f;

    private PixelFirstPersonGrid grid;

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
        bool playable = !PixelPauseMenu.IsPaused && (Time.timeScale > 0f || PixelTimeStop.IsStopped) && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting && !PixelViewWindow.IsOpen && !PixelDevTools.IsOpen;   // (the View window and the Dev Hub need the mouse)
        SetCursor(playable);
        if (!playable) return;

        Look();
        Move();
        UpdateClick();
    }

    private float bobPhase, bobStrength;

    private void LateUpdate()
    {
        if (!Active || player == null || fpCamera == null) return;
        float bob = Mathf.Sin(bobPhase * Mathf.PI) * bobStrength * eyeHeight * V(headBobAmount, 0.022f);
        float sway = Mathf.Cos(bobPhase * Mathf.PI * 0.5f) * bobStrength * eyeHeight * V(headBobAmount, 0.022f) * 0.5f;
        Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
        fpCamera.transform.SetPositionAndRotation(player.transform.position + Vector3.up * (eyeHeight + bob) + look * Vector3.right * sway, look);
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
        Debug.Log("PixelFirstPerson: entered - gravity x" + V(gravityFactor, 2f) + ", jump height " + V(jumpHeight, 0.45f) + ", walk speed " + V(walkSpeed, 5f) + ", head bob " + V(headBobAmount, 0.022f) + " (Inspector values: gravityFactor " + gravityFactor + ", jumpHeight " + jumpHeight + ", headBobAmount " + headBobAmount + ")");
        float gridUnit = clicker.OldPixelWorldSize;
        grid = new GameObject("First Person Neon Grid").AddComponent<PixelFirstPersonGrid>();
        grid.Setup(clicker, gridColor, gridUnit * V(gridSpacingCubes, 1f), gridUnit * V(gridPlayerRadiusCubes, 7f), gridUnit * V(gridPixelRadiusCubes, 2.5f), V(gridBrightness, 1.2f));
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
        if (throwLine != null) Destroy(throwLine.gameObject);
        throwLine = null; charging = false;
        if (grid != null) Destroy(grid.gameObject);
        grid = null;
        foreach (GameObject g in attachGhosts) if (g != null) Destroy(g);
        attachGhosts.Clear(); hintLabel = null;
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
        if (grounded && PixelKeys.KeyPressed(KeyCode.Space)) verticalSpeed = Mathf.Sqrt(2f * -Physics.gravity.y * V(gravityFactor, 2f) * unit * SizeScale * V(jumpHeight, 0.45f));
        float dt = Time.unscaledDeltaTime; // so you can still run about while time is stopped
        verticalSpeed += Physics.gravity.y * V(gravityFactor, 2f) * dt;

        controller.Move((dir * speed + Vector3.up * verticalSpeed) * dt);

        // Head bob: a phase that advances with the walking speed, faded in only while walking on the ground.
        float walking = grounded && dir.sqrMagnitude > 0.01f ? 1f : 0f;
        bobStrength = Mathf.MoveTowards(bobStrength, walking, dt * 6f);
        bobPhase += (dir.magnitude * speed / Mathf.Max(0.0001f, unit * SizeScale)) * V(headBobRate, 0.8f) * dt;
    }

    private void UpdateClick()
    {
        bool aiming = AimingAtCube();
        bool holding = carried.Count > 0;
        Rigidbody target = !holding ? FindPickup() : null;
        bool stuck = target != null && PixelAttachment.IsAttached(target);   // part of a build: left drags the whole build, a right hold pries this one cube off

        if (holding) UpdateHolding();
        else
        {
            HideAttachGhost();
            if (PixelInput.LeftPressed())
            {
                if (target != null) PickUp(target);       // left click grabs a cube (or a whole build)...
                else if (aiming) clicker.ManualClickFromAfar();   // ...or clicks the clicker
            }
            // Hold the right button on a stuck cube to pry it loose (the ring sits on the crosshair; the cursor is locked).
            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (PixelHold.Update(this, stuck ? target : null, V(pryHoldSeconds, 0.7f), "", new Color(1f, 0.85f, 0.3f), true, null, false, centre) && stuck && target != null)
            {
                PixelAttachment.Detach(target);
                PixelAudio.Play("device_remove");
            }
        }

        if (crosshair != null)
            crosshair.color = holding ? new Color(1f, 1f, 1f, 0.5f) : aiming && target == null ? new Color(0.4f, 1f, 0.5f, 0.95f)
                            : stuck ? new Color(0.4f, 0.85f, 1f, 0.95f) : target != null ? new Color(1f, 0.85f, 0.25f, 0.95f) : new Color(1f, 1f, 1f, 0.7f);
        UpdateHint(holding, target != null && !stuck, stuck, aiming);
    }

    /// <summary>While a cube or build is in hand: the wheel moves it, the right button turns it (see Look), and left click attaches it (cube faces or the ground, where the white ghost shows) or sets it down.</summary>
    private void UpdateHolding()
    {
        if (carried.Count == 0 || carried[0] == null) { carried.Clear(); return; }
        float unit = clicker.OldPixelWorldSize;
        float scroll = PixelInput.ScrollY();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            float reach = height * V(carryReach, 3.5f);
            holdDistance = Mathf.Clamp(holdDistance + Mathf.Sign(scroll) * unit * V(wheelStep, 0.35f), unit * 1.1f, reach);
        }

        // The white ghost where it would stick.
        bool snapped = FindCarriedSnap(unit, out Vector3 hPos, out Quaternion hRot, out int member, out Rigidbody target);
        UpdateAttachGhosts(snapped, hPos, hRot);

        // Left button: a quick tap sticks it (white ghost) or sets it down; HOLD it to charge a throw (the ghost line shows the flight) and let go to throw.
        if (PixelInput.LeftPressed()) { charging = true; chargeTime = 0f; }
        if (charging)
        {
            if (PixelInput.LeftHeld())
            {
                chargeTime += Time.unscaledDeltaTime;
                if (chargeTime > TapSeconds)
                {
                    UpdateAttachGhosts(false, Vector3.zero, Quaternion.identity);
                    UpdateThrowLine(true);
                }
            }
            else
            {
                charging = false;
                bool threw = chargeTime > TapSeconds;
                HideThrowLine();
                if (threw) DropCarried(ThrowVelocity());
                else if (snapped) AttachCarried(hPos, hRot, member, target);
                else DropCarried();
            }
        }
    }

    private const float TapSeconds = 0.25f;
    private bool charging;
    private float chargeTime;
    private LineRenderer throwLine;
    private Material throwLineMat;

    private float ThrowPower01 => Mathf.Clamp01((chargeTime - TapSeconds) / V(throwChargeSeconds, 1.2f));

    private Vector3 ThrowVelocity()
    {
        Vector3 dir = (fpCamera.transform.forward + Vector3.up * 0.12f).normalized;
        return dir * clicker.OldPixelWorldSize * V(throwSpeed, 14f) * (0.25f + 0.75f * ThrowPower01);
    }

    /// <summary>The ghost line: the arc the held cube will fly, drawn up to where it lands - longer the longer the button is held.</summary>
    private void UpdateThrowLine(bool show)
    {
        if (!show || carried.Count == 0 || carried[0] == null) { HideThrowLine(); return; }
        if (throwLine == null)
        {
            GameObject go = new GameObject("Throw Line");
            throwLine = go.AddComponent<LineRenderer>();
            Shader sh = PixelShaders.SpriteDefault();
            throwLineMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            throwLine.sharedMaterial = throwLineMat;
            throwLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            throwLine.receiveShadows = false;
            throwLine.numCapVertices = 4;
        }
        throwLine.gameObject.SetActive(true);
        float unit = clicker.OldPixelWorldSize;
        Vector3 p = carried[0].transform.position;
        Vector3 v = ThrowVelocity();
        ScaledGravity sg = carried[0].GetComponent<ScaledGravity>();
        Vector3 g = Physics.gravity * (sg != null ? sg.scale : 1f);
        const float dt = 0.04f;
        const int maxSteps = 90;
        var pts = throwPoints;
        pts.Clear();
        pts.Add(p);
        for (int i = 0; i < maxSteps; i++)
        {
            Vector3 np = p + v * dt + 0.5f * g * dt * dt;
            v += g * dt;
            Vector3 seg = np - p;
            bool hit = false;
            if (Physics.Raycast(p, seg.normalized, out RaycastHit h, seg.magnitude, ~0, QueryTriggerInteraction.Ignore)
                && h.collider.GetComponentInParent<OldPixelInfo>() == null)
            { np = h.point; hit = true; }
            pts.Add(np);
            p = np;
            if (hit) break;
        }
        throwLine.positionCount = pts.Count;
        throwLine.SetPositions(pts.ToArray());
        float w = unit * 0.07f;
        throwLine.startWidth = w; throwLine.endWidth = w * 0.5f;
        float t = ThrowPower01;
        Color c = Color.Lerp(new Color(1f, 1f, 1f, 0.55f), new Color(1f, 0.55f, 0.15f, 0.9f), t);
        throwLine.startColor = c; throwLine.endColor = new Color(c.r, c.g, c.b, 0.15f);
    }

    private readonly System.Collections.Generic.List<Vector3> throwPoints = new System.Collections.Generic.List<Vector3>();

    private void HideThrowLine() { if (throwLine != null && throwLine.gameObject.activeSelf) throwLine.gameObject.SetActive(false); }

    /// <summary>Best spot for the carried cube / build: on another cube's face, or flat on the ground. Returns the handle's pose, which carried cube makes the contact and what it sticks to (null = ground).</summary>
    private bool FindCarriedSnap(float unit, out Vector3 hPos, out Quaternion hRot, out int member, out Rigidbody target)
    {
        hPos = Vector3.zero; hRot = Quaternion.identity; member = 0; target = null;
        float maxDist = unit * V(attachDistance, 0.9f);
        float angle = attachAngle > 0.5f ? attachAngle : 28f;
        PixelAttachment.Snap best = default;
        int bestMember = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i] == null) continue;
            if (PixelAttachment.TrySnap(carried[i], heldHalves[i], clicker.OldPixels, unit, angle, maxDist, out PixelAttachment.Snap s) && s.distance < bestDist)
            { best = s; bestMember = i; bestDist = s.distance; }
        }
        // The ground: of the carried cubes that can rest on it, the one that has to lift the most (the lowest) makes the contact.
        PixelAttachment.Snap ground = default;
        int groundMember = -1;
        float groundLift = float.MinValue;
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i] == null) continue;
            if (PixelAttachment.TryGroundSnap(carried[i], heldHalves[i], unit, angle, maxDist, clicker.PixelTransform, out PixelAttachment.Snap g))
            {
                float lift = g.position.y - carried[i].transform.position.y;
                if (lift > groundLift) { groundLift = lift; ground = g; groundMember = i; }
            }
        }
        bool useGround = groundMember >= 0 && (bestMember < 0 || ground.distance < bestDist);
        if (useGround) { best = ground; bestMember = groundMember; }
        if (bestMember < 0) return false;
        member = bestMember;
        target = best.target;
        hRot = best.rotation * Quaternion.Inverse(relRot[bestMember]);
        hPos = best.position - hRot * relPos[bestMember];
        return true;
    }

    private void AttachCarried(Vector3 hPos, Quaternion hRot, int member, Rigidbody target)
    {
        Rigidbody contact = carried[member];
        for (int i = 0; i < carried.Count; i++)
        {
            Rigidbody body = carried[i];
            if (body == null) continue;
            foreach (Collider c in body.GetComponentsInChildren<Collider>()) c.enabled = true;
            body.transform.SetPositionAndRotation(hPos + hRot * relPos[i], hRot * relRot[i]);
            body.isKinematic = false;
            SetBodyVelocity(body, Vector3.zero);
            ScaledGravity gravity = body.GetComponent<ScaledGravity>();
            if (gravity != null) gravity.enabled = true;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.Held = false;
            OldPixelShatter shatter = body.GetComponent<OldPixelShatter>();
            if (shatter != null) shatter.Released(true, 3f);   // (a glass cube is set down gently)
        }
        foreach (PixelAttachment.Edge e in carriedEdges) PixelAttachment.Attach(e.a, e.b);
        if (target != null) PixelAttachment.Attach(contact, target);
        else PixelAttachment.AttachToWorld(contact);          // a foundation: pinned where it rests on the ground
        ClearCarried();
        HideAttachGhost();
        PixelAudio.Play("device_place");
    }

    // ---- the white attach ghost (one per carried cube) ----

    private readonly System.Collections.Generic.List<GameObject> attachGhosts = new System.Collections.Generic.List<GameObject>();
    private Material attachGhostMat;
    private Vector3 heldHalf = Vector3.one * 0.5f;
    private Quaternion heldRotation = Quaternion.identity;
    private float holdDistance;
    private readonly System.Collections.Generic.List<Vector3> relPos = new System.Collections.Generic.List<Vector3>();
    private readonly System.Collections.Generic.List<Quaternion> relRot = new System.Collections.Generic.List<Quaternion>();
    private readonly System.Collections.Generic.List<Vector3> heldHalves = new System.Collections.Generic.List<Vector3>();
    private System.Collections.Generic.List<PixelAttachment.Edge> carriedEdges = new System.Collections.Generic.List<PixelAttachment.Edge>();

    private void UpdateAttachGhosts(bool show, Vector3 hPos, Quaternion hRot)
    {
        if (!show) { HideAttachGhost(); return; }
        if (attachGhostMat == null)
        {
            Shader sh = PixelShaders.SpriteDefault();
            attachGhostMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
        }
        attachGhostMat.color = new Color(1f, 1f, 1f, 0.3f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
        while (attachGhosts.Count < carried.Count)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Attach Ghost";
            Collider c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
            Renderer r = g.GetComponent<Renderer>();
            r.sharedMaterial = attachGhostMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            attachGhosts.Add(g);
        }
        for (int i = 0; i < attachGhosts.Count; i++)
        {
            bool on = i < carried.Count;
            attachGhosts[i].SetActive(on);
            if (!on) continue;
            attachGhosts[i].transform.SetPositionAndRotation(hPos + hRot * relPos[i], hRot * relRot[i]);
            attachGhosts[i].transform.localScale = heldHalves[i] * 2f * 1.02f;
        }
    }

    private void HideAttachGhost() { foreach (GameObject g in attachGhosts) if (g != null && g.activeSelf) g.SetActive(false); }

    // ---- a short hint line ----

    private TMP_Text hintLabel;

    private void UpdateHint(bool holding, bool canGrab, bool canPry, bool aimingCube)
    {
        if (hintLabel == null) return;
        string text = holding ? "Wheel: closer / farther     Hold right + mouse: turn     Tap left: stick it where the white ghost shows (cube or ground) or put it down     Hold left, release: throw"
                    : canGrab ? "Left click: pick up this cube"
                    : canPry ? "Left click: move the whole build     Hold right click: pry this cube loose"
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

    /// <summary>The old pixel the crosshair is nearest to (within reach and a small angle), or null. Cubes glued into a build count too.</summary>
    private Rigidbody FindPickup()
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
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            // (a cube in a build always has its lifetime frozen - that must not hide it)
            if (despawn != null && (despawn.IsDespawning || (despawn.Held && !PixelAttachment.IsAttached(body)))) continue;
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

    /// <summary>Picks up 'handle' - and, if it is part of a build, every cube glued to it (they keep their places relative to the handle).</summary>
    private void PickUp(Rigidbody handle)
    {
        clicker.LandFlyingPixel(handle);
        System.Collections.Generic.List<Rigidbody> members = PixelAttachment.Group(handle);
        members.Remove(handle);
        members.Insert(0, handle);
        carriedEdges = PixelAttachment.TakeApart(members);
        Quaternion inv = Quaternion.Inverse(handle.transform.rotation);
        foreach (Rigidbody body in members)
        {
            PixelStats.Count("old.grabbed");
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
            relPos.Add(inv * (body.transform.position - handle.transform.position));
            relRot.Add(inv * body.transform.rotation);
            heldHalves.Add(PixelAttachment.HalfExtents(body, clicker.OldPixelWorldSize));
        }
        heldRotation = handle.transform.rotation;
        heldHalf = heldHalves[0];
        holdDistance = Mathf.Clamp(Vector3.Distance(fpCamera.transform.position, handle.transform.position), clicker.OldPixelWorldSize * 1.1f, height * V(carryReach, 3.5f));
        PixelAudio.Play("grab");
    }

    private void ClearCarried()
    {
        carried.Clear(); relPos.Clear(); relRot.Clear(); heldHalves.Clear(); carriedEdges.Clear();
    }

    /// <summary>Sets what is in hand down in front of you (a build keeps its glue).</summary>
    private void DropCarried(Vector3? throwVelocity = null)
    {
        if (carried.Count == 0) return;
        Vector3 forward = fpCamera != null ? fpCamera.transform.forward : Vector3.forward;
        float unit = clicker.OldPixelWorldSize;
        foreach (Rigidbody body in carried)
        {
            if (body == null) continue;
            foreach (Collider c in body.GetComponentsInChildren<Collider>()) c.enabled = true;
            body.isKinematic = false;
            ScaledGravity gravity = body.GetComponent<ScaledGravity>();
            if (gravity != null) gravity.enabled = true;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null) despawn.Held = false;
            SetBodyVelocity(body, throwVelocity ?? forward * unit * 0.6f);
            OldPixelShatter shatter = body.GetComponent<OldPixelShatter>();
            if (shatter != null) shatter.Released(true, 3f);
        }
        foreach (PixelAttachment.Edge e in carriedEdges) PixelAttachment.Attach(e.a, e.b);   // (also freezes their lifetime again)
        ClearCarried();
        charging = false;
        HideAttachGhost();
        HideThrowLine();
        PixelAudio.Play("drop");
    }

    private void DropAll() { DropCarried(); }

    /// <summary>Holds the cube (or build) straight ahead along the view at the held distance, turned the way the player turned it.</summary>
    private void PlaceCarried()
    {
        if (fpCamera == null || carried.Count == 0) return;
        Rigidbody handle = carried[0];
        if (handle == null) { ClearCarried(); return; }
        Transform ct = fpCamera.transform;
        Vector3 target = ct.position + ct.forward * holdDistance;
        float follow = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
        Vector3 pos = Vector3.Lerp(handle.transform.position, target, follow);
        for (int i = 0; i < carried.Count; i++)
        {
            if (carried[i] == null) continue;
            carried[i].transform.SetPositionAndRotation(pos + heldRotation * relPos[i], heldRotation * relRot[i]);
        }
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
