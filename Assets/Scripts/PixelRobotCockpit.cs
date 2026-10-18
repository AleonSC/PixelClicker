using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// "Ride along" for the UFO Helper (part of <see cref="PixelRobotWorker"/>): choose it in the UFO's window and you are inside the saucer's glass dome as it flies about on its own.
/// The dome holds a small cabin (a floor, a ring console with blinking buttons and a little alien at its station; see BuildUfo). Your head is a camera inside it: move the mouse to look around, WASD walk about the cabin (kept inside the ring of the console and out of the alien's way), and the whole cabin leans and bobs
/// with the saucer's flight, so you see the cube and the floor from up there. The UFO keeps doing its job (dropping devices, banking pixels) and wandering; you are only a passenger.
/// Escape climbs out.
/// </summary>
public partial class PixelRobotWorker
{
    [Header("Ride along (inside the dome)")]
    [Tooltip("Row in the UFO's window that puts you in the cabin.")]
    [SerializeField] private string boardText = "Ride along";

    [Tooltip("How fast you walk inside the cabin (model units per second; the cabin floor is about 1 unit wide).")]
    [SerializeField] private float walkSpeed = 0.33f;

    [Tooltip("How far from the middle of the cabin floor you can walk (model units; the console ring starts at about 0.4).")]
    [SerializeField] private float walkRadius = 0.3f;

    [Tooltip("Height of your eyes above the cabin floor (model units; the alien is about 0.14 tall).")]
    [SerializeField] private float eyeHeight = 0.17f;

    [Tooltip("Field of view of your view from the cabin (degrees).")]
    [SerializeField] private float cockpitFov = 80f;

    [Tooltip("Mouse look speed (degrees per mouse count). The mouse always looks around while you ride.")]
    [SerializeField] private float cockpitLookSpeed = 0.35f;

    [TextArea(1, 3)]
    [SerializeField] private string rideHint = "Mouse: look around  -  WASD: walk about the cabin  -  Esc: climb out";

    /// <summary>True while you ride inside the UFO (cube clicks, Pixel Grabbing, the block tooltip and first person mode step aside).</summary>
    public static bool Piloting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPilotStatics() { Piloting = false; }

    private Camera cockpitCamera, mainCam;
    private int savedMask;
    private GameObject cockpitCanvas, cockpitRig;
    private Vector2 walkPos;   // where you stand on the cabin floor (model units from its middle: x = right, y = forward)
    private float lookYaw, lookPitch = -6f, walkBob;
    private bool lookActive, cursorLocked;
    private readonly object cockpitKey = new object();

    private bool CanPilot()
    {
        if (!Active || clicker == null || consumables == null || Piloting) return false;
        if (PixelCalm.InMenu || PixelMinigame.TakeoverActive || PixelFirstPerson.Active || PixelBank.HoseOn) return false;
        if (PixelCameraIntro.Moving || (Time.timeScale <= 0f && !PixelTimeStop.IsStopped)) return false;
        if (PixelPets.PopupOpen || PixelGuideVendor.Visiting) return false;
        return root != null && bodyT != null;
    }

    private void EnterCockpit()
    {
        if (!CanPilot()) return;
        mainCam = Cam;
        if (mainCam == null) return;
        PixelWindows.CloseAllExcept(this);

        walkPos = new Vector2(0.02f, -0.08f);
        lookYaw = 0f; lookPitch = -6f;
        savedMask = mainCam.cullingMask;

        // The passenger's head: a camera inside the dome (a root object: a camera under a scaled parent culls wrongly). The normal camera keeps its pose for the view bounds but draws nothing.
        cockpitRig = new GameObject("UFO Passenger");
        cockpitCamera = cockpitRig.AddComponent<Camera>();
        cockpitCamera.CopyFrom(mainCam);
        cockpitCamera.depth = mainCam.depth + 1f;
        cockpitCamera.fieldOfView = cockpitFov;
        cockpitCamera.farClipPlane = Mathf.Min(mainCam.farClipPlane, 400f);
        cockpitCamera.nearClipPlane = Mathf.Max(0.0005f, 0.012f * bodyT.lossyScale.x);
        if (cockpitRig.GetComponent<AudioListener>() != null) Destroy(cockpitRig.GetComponent<AudioListener>());
        mainCam.cullingMask = 0;

        if (cabinLamp != null) cabinLamp.enabled = true;
        BuildCockpitUi();
        if (bubbleCanvas != null) bubbleCanvas.SetActive(false);
        Piloting = true;
        PixelWindows.Register(cockpitKey, 125, () => Piloting, ExitCockpit);   // escape-only: leaves the cockpit
        PositionPassenger();
        PixelAudio.Play("ui_click");
    }

    private void ExitCockpit()
    {
        if (!Piloting && cockpitCamera == null) return;
        Piloting = false;
        PixelWindows.Unregister(cockpitKey);
        SetPilotCursor(false);
        if (mainCam != null) mainCam.cullingMask = savedMask;
        if (cockpitRig != null) Destroy(cockpitRig);
        if (cockpitCanvas != null) Destroy(cockpitCanvas);
        if (cabinLamp != null) cabinLamp.enabled = false;
        cockpitCamera = null; cockpitRig = null; cockpitCanvas = null;
        ignoreClickFrame = Time.frameCount;   // (the click that pressed Leave is not a click on the saucer)
    }

    private static Vector2 PilotMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
    }

    private void SetPilotCursor(bool locked)
    {
        if (cursorLocked == locked) return;
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>Called every frame after the saucer has moved while you ride along: look, walk, and put the camera where your head is.</summary>
    private void UpdateCockpit()
    {
        if (cockpitCamera == null || cockpitRig == null || bodyT == null) { ExitCockpit(); return; }
        float dt = Time.deltaTime;
        bool playable = !PixelPauseMenu.IsPaused && Time.timeScale > 0f && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting;

        // Looking round: the mouse always turns your head (the cursor is locked and hidden while you ride; Esc climbs out, a paused game frees the cursor).
        lookActive = playable;
        SetPilotCursor(lookActive);
        if (lookActive)
        {
            Vector2 d = PilotMouseDelta();
            lookYaw += d.x * cockpitLookSpeed;
            lookPitch = Mathf.Clamp(lookPitch - d.y * cockpitLookSpeed, -80f, 70f);
        }

        // Walking about the cabin, relative to where you look.
        Vector2 wish = Vector2.zero;
        if (playable && !PixelKeys.Typing)
        {
            float forward = (PixelKeys.KeyHeld(KeyCode.W) || PixelKeys.KeyHeld(KeyCode.UpArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.S) || PixelKeys.KeyHeld(KeyCode.DownArrow) ? 1f : 0f);
            float side = (PixelKeys.KeyHeld(KeyCode.D) || PixelKeys.KeyHeld(KeyCode.RightArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.A) || PixelKeys.KeyHeld(KeyCode.LeftArrow) ? 1f : 0f);
            float yawRad = lookYaw * Mathf.Deg2Rad;
            Vector2 fwd = new Vector2(Mathf.Sin(yawRad), Mathf.Cos(yawRad)), right = new Vector2(Mathf.Cos(yawRad), -Mathf.Sin(yawRad));
            wish = fwd * forward + right * side;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
        }
        if (wish.sqrMagnitude > 0.0001f)
        {
            walkPos += wish * walkSpeed * dt;
            walkBob += dt * 9f;
        }
        // Stay on the floor inside the console ring, and out of the alien's way.
        if (walkPos.magnitude > walkRadius) walkPos = walkPos.normalized * walkRadius;
        Vector2 alien = new Vector2(-0.2f, 0.23f);
        Vector2 away = walkPos - alien;
        if (away.magnitude < 0.09f) walkPos = alien + (away.sqrMagnitude > 0.00001f ? away.normalized : Vector2.right) * 0.09f;

        PositionPassenger();
    }

    /// <summary>The camera goes where your head is in the cabin (the saucer's pose carries it: it leans, bobs and flies with the cabin).</summary>
    private void PositionPassenger()
    {
        float bob = Mathf.Sin(walkBob) * 0.004f;
        Vector3 local = new Vector3(walkPos.x, CabinFloorY + eyeHeight + bob, walkPos.y);
        cockpitRig.transform.SetPositionAndRotation(bodyT.TransformPoint(local), bodyT.rotation * Quaternion.Euler(lookPitch, lookYaw, 0f));
        cockpitCamera.nearClipPlane = Mathf.Max(0.0005f, 0.012f * bodyT.lossyScale.x);
    }

    private void BuildCockpitUi()
    {
        TMP_FontAsset font = clicker.UIFont;
        cockpitCanvas = PixelUIKit.CreateCanvas("UFO Cabin", 500, new Vector2(1920f, 1080f), false);
        Transform c = cockpitCanvas.transform;

        TMP_Text hint = PixelStatusBox.Create(font, c, 28f);   // the same box that slides out from under the top bar
        hint.text = rideHint;
    }
}
