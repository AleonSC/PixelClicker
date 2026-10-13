// ---- System notes (moved here from CLAUDE.md) ----
// PixelOrbitCamera.cs
//   Orbital camera (added by PixelClicker.Awake; chosen in the View window (PixelViewWindow, key PixelAction.ViewWindow) as 'Orbital', or
//   'Top down' = Enter(topDown: true): the same camera with the pitch locked at 89 degrees; disableOrbit): the REAL camera swings round the cube (Enter captures
//   the home pose; yaw / pitch / distance round CubeBasePosition, rotation = look-at-cube x aimOffset so the cube keeps its on-screen
//   spot): middle-mouse drag (PixelInput.MiddleHeld, dragDegreesPerPixel 0 = 0.25) or arrow keys (keyDegreesPerSecond 0 = 90) turn /
//   tilt (pitch min minPitch 0 = 6 so it never looks from under the floor), the wheel (or Shift + wheel) and the View window's zoom slider (DistanceFraction, MinFraction 0.35 .. MaxFraction 2) zoom, the
//   window's Auto orbit tick box (AutoOrbit, AutoSpeed = autoOrbitDegreesPerSecond 0 = 10) turns the camera slowly round the cube by itself,
//   and its hold buttons (HoldTurn / HoldTilt) add to the arrow keys; dragging / arrow keys still work, followSharpness smoothing, a hint line for hintSeconds. Clicks need no change (raycasts go through
//   TargetCamera). While PixelOrbitCamera.Active, PixelOrbitCamera.HomeCamera (a hidden disabled copy at the home pose) is what
//   PixelViewBounds and PixelFloor.FitToView read, so old pixels stay inside the NORMAL view and fitted floors don't re-fit. Leaving
//   glides back over returnSeconds (0 = 0.7); the title screen, the camera intro and takeover / zooming minigames (any Busy + Takeover
//   one) end it or block it.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Orbital camera (one of the views of the View window, <c>PixelViewWindow</c>: Default / First person / Orbital / Top down; top down is this camera with the tilt locked straight down).
/// The real camera swings round the clicker cube, so you can click it from any direction (clicks are ordinary raycasts through the camera).
/// Drag with the MIDDLE mouse button, or use the arrow keys, to turn round it and tilt; Shift + mouse wheel zooms. Leaving glides back to the
/// view the camera had. While it is on, old pixels stay inside the NORMAL view (<see cref="HomeCamera"/>, a hidden copy of the camera at its
/// home pose, is what the view bounds and the fitted floors use), and takeover minigames / the title screen end it.
/// </summary>
public class PixelOrbitCamera : MonoBehaviour
{
    [Header("Orbital Camera")]
    [Tooltip("Turn the orbital camera off completely (the view key then only switches first person mode).")]
    [SerializeField] private bool disableOrbit = false;

    [Min(0f)]
    [Tooltip("Degrees turned per screen pixel of middle-mouse drag. 0 = the default (0.25).")]
    [SerializeField] private float dragDegreesPerPixel = 0f;

    [Min(0f)]
    [Tooltip("Degrees per second when turning with the arrow keys. 0 = the default (90).")]
    [SerializeField] private float keyDegreesPerSecond = 0f;

    [Min(0f)]
    [Tooltip("Closest the camera may come, as a fraction of its normal distance from the cube. 0 = the default (0.35).")]
    [SerializeField] private float minDistanceFraction = 0f;

    [Min(0f)]
    [Tooltip("Furthest the camera may go, as a fraction of its normal distance. 0 = the default (2).")]
    [SerializeField] private float maxDistanceFraction = 0f;

    [Min(0f)]
    [Tooltip("Lowest the camera may go (degrees above the floor). 0 = the default (6): the floor is a flat sheet, so it never looks from underneath.")]
    [SerializeField] private float minPitch = 0f;

    [Min(0f)]
    [Tooltip("How quickly the camera follows the controls (bigger = snappier). 0 = the default (12).")]
    [SerializeField] private float followSharpness = 0f;

    [Min(0f)]
    [Tooltip("Seconds the glide back to the normal view takes. 0 = the default (0.7).")]
    [SerializeField] private float returnSeconds = 0f;

    [Min(0f)]
    [Tooltip("Degrees per second the camera turns round the cube while the Auto button is on. 0 = the default (10).")]
    [SerializeField] private float autoOrbitDegreesPerSecond = 0f;

    [Tooltip("Hint shown at the bottom of the screen while orbiting. {0} = the view key.")]
    [SerializeField] private string hintTextV2 = "Orbit: middle mouse drag or arrow keys  -  mouse wheel zooms  -  {0} opens the View window (zoom slider, auto orbit, other views)";

    [Min(0f)]
    [Tooltip("How long the hint stays on screen (seconds). 0 = the default (6).")]
    [SerializeField] private float hintSeconds = 0f;

    /// <summary>True while the camera is orbiting (or gliding back to its normal view).</summary>
    public static bool Active { get; private set; }

    public static PixelOrbitCamera Instance { get; private set; }

    /// <summary>A hidden, disabled copy of the camera at its normal pose while orbiting (null otherwise): the view bounds and fitted floors read it.</summary>
    public static Camera HomeCamera => orbitHome != null ? orbitHome : PixelZoom.HomeCamera;   // (the zoom key's copy counts too)
    private static Camera orbitHome;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Active = false; Instance = null; orbitHome = null; }

    private PixelClicker clicker;
    private Camera cam;
    private Vector3 homePos, pivot;
    private Quaternion homeRot, aimOffset;
    private float homeFov, homeOrtho, homeYaw, homePitch, homeDistance;
    private float yaw, pitch, distance, targetYaw, targetPitch, targetDistance;
    private bool returning;
    private float returnT;
    private float returnFromYaw, returnFromPitch, returnFromDistance;
    private Vector2 lastPointer;
    private GameObject hintRoot;
    private bool autoOrbit;

    /// <summary>True while the camera looks straight down at the cube (the 'top down' view; same component, pitch locked).</summary>
    public static bool TopDown { get; private set; }

    /// <summary>Camera distance as a fraction of the normal one (1 = normal); setting it moves the camera closer or further.</summary>
    public float DistanceFraction
    {
        get => homeDistance > 0.01f ? targetDistance / homeDistance : 1f;
        set { if (Active && homeDistance > 0.01f) targetDistance = homeDistance * Mathf.Clamp(value, MinFraction, MaxFraction); }
    }

    public float MinFraction => V(minDistanceFraction, 0.35f);
    public float MaxFraction => V(maxDistanceFraction, 2f);

    /// <summary>The camera turns slowly round the cube by itself.</summary>
    public bool AutoOrbit { get => autoOrbit; set => autoOrbit = value; }

    /// <summary>Degrees per second of the automatic turn.</summary>
    public float AutoSpeed { get => V(autoOrbitDegreesPerSecond, 10f); set => autoOrbitDegreesPerSecond = Mathf.Max(0.5f, value); }

    /// <summary>Held by the View window's buttons: -1..1 turn / tilt, added to the keys.</summary>
    public float HoldTurn, HoldTilt;

    /// <summary>Switches between the orbital and the top down view while orbiting (the camera glides to the new angle).</summary>
    public void SetTopDown(bool on)
    {
        if (!Active) return;
        TopDown = on;
        targetPitch = on ? 89f : Mathf.Clamp(homePitch, V(minPitch, 6f), 88f);
    }

    /// <summary>Ends orbiting at once (back to the normal view without the glide); used when another view takes over.</summary>
    public void Cancel() { if (Active) Finish(true); }
    private float hintTimer;

    private static float V(float value, float fallback) => value > 0f ? value : fallback;

    private void Awake()
    {
        Instance = this;
        clicker = PixelFind.First<PixelClicker>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (Active) Finish(true);
    }

    /// <summary>The view key was pressed while first person mode ended or no mode was on: start orbiting. Returns false if it can't.</summary>
    public bool Enter(bool topDown = false)
    {
        if (disableOrbit || Active || clicker == null || clicker.PixelTransform == null) return false;
        PixelZoom.ResetNow();   // (the orbit starts from the normal view, not a zoomed one)
        if (PixelPauseMenu.IsPaused || PixelTitleScreen.Showing || PixelTitleScreen.Reversing || PixelCameraIntro.Moving || PixelMinigame.TakeoverActive) return false;
        foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Busy && m.Takeover) return false;
        cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;

        homePos = cam.transform.position;
        homeRot = cam.transform.rotation;
        homeFov = cam.fieldOfView;
        homeOrtho = cam.orthographicSize;
        pivot = clicker.CubeBasePosition;
        Vector3 offset = homePos - pivot;
        homeDistance = Mathf.Max(0.5f, offset.magnitude);
        homeYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        homePitch = Mathf.Asin(Mathf.Clamp(offset.y / homeDistance, -1f, 1f)) * Mathf.Rad2Deg;
        // How far the normal view is turned from looking straight at the cube, kept while orbiting (so the cube stays where it is on screen).
        Quaternion lookAtCube = Quaternion.LookRotation(-offset.normalized, Vector3.up);
        aimOffset = Quaternion.Inverse(lookAtCube) * homeRot;

        yaw = targetYaw = homeYaw;
        pitch = targetPitch = homePitch;
        distance = targetDistance = homeDistance;
        TopDown = topDown;
        if (topDown) targetPitch = 89f;   // looks straight down (the camera glides there)

        GameObject go = new GameObject("Orbit Home Camera");
        go.hideFlags = HideFlags.HideAndDontSave;
        orbitHome = go.AddComponent<Camera>();
        orbitHome.CopyFrom(cam);
        orbitHome.enabled = false;
        go.transform.SetPositionAndRotation(homePos, homeRot);

        returning = false;
        Active = true;
        autoOrbit = false;
        HoldTurn = HoldTilt = 0f;
        lastPointer = PixelInput.PointerPosition();
        BuildHint();
        return true;
    }

    /// <summary>Glides back to the normal view and ends (the view key went round again).</summary>
    public void Exit()
    {
        if (!Active || returning) return;
        returning = true;
        returnT = 0f;
        returnFromYaw = yaw; returnFromPitch = pitch; returnFromDistance = distance;
        HideHint();
    }

    private void Finish(bool snap)
    {
        if (cam != null && snap) { cam.transform.SetPositionAndRotation(homePos, homeRot); cam.fieldOfView = homeFov; if (cam.orthographic) cam.orthographicSize = homeOrtho; }
        if (orbitHome != null) Destroy(orbitHome.gameObject);
        orbitHome = null;
        Active = false;
        returning = false;
        TopDown = false;
        HideHint();
    }

    private void Update()
    {
        if (!Active) return;
        if (cam == null || clicker == null || clicker.PixelTransform == null) { Finish(true); return; }
        if (PixelTitleScreen.Showing || PixelTitleScreen.Reversing || PixelCameraIntro.Moving || PixelMinigame.TakeoverActive) { Finish(true); return; }
        if (!returning) foreach (PixelMinigame m in PixelMinigame.All) if (m != null && m.Busy && m.Takeover) { Exit(); break; }   // (a round that moves the cube about on screen needs the normal view)
        if (orbitHome != null) orbitHome.aspect = cam.aspect;
        if (hintRoot != null)
        {
            hintTimer -= Time.unscaledDeltaTime;
            if (hintTimer <= 0f) HideHint();
        }
        if (returning) return;

        bool playable = !PixelPauseMenu.IsPaused && !PixelKeys.Typing && (Time.timeScale > 0f || PixelTimeStop.IsStopped);
        Vector2 pointer = PixelInput.PointerPosition();
        if (playable)
        {
            float dt = Time.unscaledDeltaTime;
            if (PixelInput.MiddleHeld() && !PixelInput.PointerOverUI())
            {
                Vector2 d = pointer - lastPointer;
                float k = V(dragDegreesPerPixel, 0.25f);
                targetYaw -= d.x * k;
                if (!TopDown) targetPitch += d.y * k;
            }
            float turn = (PixelKeys.KeyHeld(KeyCode.RightArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.LeftArrow) ? 1f : 0f) + HoldTurn;
            float tilt = TopDown ? 0f : (PixelKeys.KeyHeld(KeyCode.UpArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.DownArrow) ? 1f : 0f) + HoldTilt;
            float ks = V(keyDegreesPerSecond, 90f) * dt;
            targetYaw -= turn * ks;
            targetPitch += tilt * ks;
            if (!PixelInput.PointerOverUI())   // (the plain wheel zooms now; Shift + wheel still does too)
            {
                float scroll = PixelInput.ScrollY();
                if (Mathf.Abs(scroll) > 0.01f) targetDistance *= scroll > 0f ? 0.9f : 1.1f;
            }
            if (autoOrbit) targetYaw -= V(autoOrbitDegreesPerSecond, 10f) * dt;
            targetPitch = TopDown ? 89f : Mathf.Clamp(targetPitch, V(minPitch, 6f), 88f);
            targetDistance = Mathf.Clamp(targetDistance, homeDistance * V(minDistanceFraction, 0.35f), homeDistance * V(maxDistanceFraction, 2f));
        }
        lastPointer = pointer;
    }

    private void LateUpdate()
    {
        if (!Active || cam == null) return;
        float dt = Time.unscaledDeltaTime;
        if (returning)
        {
            returnT += dt / V(returnSeconds, 0.7f);
            float k = Mathf.Clamp01(returnT);
            k = k * k * (3f - 2f * k);
            yaw = returnFromYaw + Mathf.DeltaAngle(returnFromYaw, homeYaw) * k;
            pitch = Mathf.Lerp(returnFromPitch, homePitch, k);
            distance = Mathf.Lerp(returnFromDistance, homeDistance, k);
            Place();
            if (returnT >= 1f) { Finish(true); }
            return;
        }
        float f = 1f - Mathf.Exp(-V(followSharpness, 12f) * dt);
        yaw = Mathf.LerpAngle(yaw, targetYaw, f);
        pitch = Mathf.Lerp(pitch, targetPitch, f);
        distance = Mathf.Lerp(distance, targetDistance, f);
        pivot = clicker.CubeBasePosition;
        Place();
    }

    private void Place()
    {
        float y = yaw * Mathf.Deg2Rad, p = pitch * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
        Vector3 position = pivot + dir * distance;
        cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-dir, Vector3.up) * aimOffset);
    }

    // ------------------------------------------------------------------

    private void BuildHint()
    {
        HideHint();
        hintRoot = PixelUIKit.CreateCanvas("Orbit Hint", 300, new Vector2(1920f, 1080f), false);
        hintRoot.transform.SetParent(transform, false);
        TMP_Text hint = PixelUIKit.CreateText(clicker.UIFont, hintRoot.transform, "Hint", PixelKeys.Replace(string.Format(hintTextV2, PixelKeys.Name(PixelAction.ViewWindow))),
                                              26f, TextAlignmentOptions.Center, FontStyles.Normal, Color.white);
        hint.raycastTarget = false;
        hint.outlineWidth = 0.3f;
        hint.outlineColor = new Color32(0, 0, 0, 255);
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 44f);
        hr.anchoredPosition = new Vector2(0f, (PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f) + 14f);
        hintTimer = V(hintSeconds, 6f);
    }

    private void HideHint()
    {
        if (hintRoot != null) Destroy(hintRoot);
        hintRoot = null;
    }
}
