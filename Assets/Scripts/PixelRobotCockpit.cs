using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// "Take control" for the UFO Helper (part of <see cref="PixelRobotWorker"/>): choose it in the UFO's window and you sit in a real 3D cockpit
/// built round the saucer: a seat, struts and a roof framing a wide window, a sloped console in front of you with a screen that shows the view
/// straight down (a camera drawing into a RenderTexture), lamps, buttons, two levers and 3D readouts. Your head is the camera: hold the right mouse
/// button and move the mouse to look around (out of the windows, at the console, over your shoulder). WASD fly the saucer with a little momentum
/// (the whole cabin leans into the turn), the rotate keys (Q / E) turn it; you can only fly inside the screen's bounds and never up or down.
/// Point at a pixel on the console screen and click to abduct it: the beam switches on and the pixel is pulled into the Pixel Bank (or, without a
/// bank or when it is full, collected for its value). Escape or the Leave button climbs out and the UFO carries on from where you left it.
/// </summary>
public partial class PixelRobotWorker
{
    [Header("Cockpit (Take control)")]
    [Tooltip("Row in the UFO's window that puts you in the cockpit.")]
    [SerializeField] private string takeControlText = "Take control";

    [Tooltip("Flying speed while you pilot, in pixel widths per second.")]
    [SerializeField] private float pilotSpeed = 4f;

    [Tooltip("How fast the Q / E keys turn the saucer (degrees per second).")]
    [SerializeField] private float pilotTurnDegrees = 80f;

    [Tooltip("How quickly the saucer speeds up and slows down (higher = snappier).")]
    [SerializeField] private float pilotMomentum = 3.5f;

    [Tooltip("How far the whole cabin leans into a turn or a push (degrees).")]
    [SerializeField] private float cabinLeanDegrees = 5f;

    [Tooltip("Field of view of the view out of the window (degrees).")]
    [SerializeField] private float cockpitFov = 75f;

    [Tooltip("Mouse look speed (degrees per mouse count) while the right mouse button is held.")]
    [SerializeField] private float cockpitLookSpeed = 0.15f;

    [Tooltip("How far the pilot can turn their head to either side (degrees).")]
    [SerializeField] private float cockpitMaxHeadYaw = 115f;

    [Tooltip("Field of view of the downward camera that feeds the console screen (degrees). Smaller = a closer, more zoomed view.")]
    [SerializeField] private float screenFov = 34f;

    [Tooltip("Seconds the beam holds a pixel before it is pulled up when you click it from the cockpit.")]
    [SerializeField] private float pilotAbductSeconds = 0.6f;

    [TextArea(1, 3)]
    [SerializeField] private string pilotHint = "Hold right mouse: look around  -  {0} / {1}: turn  -  WASD: fly  -  click a pixel on the console screen to abduct it  -  Esc: leave";

    [SerializeField] private string leaveText = "Leave";

    /// <summary>True while you pilot the UFO (cube clicks, Pixel Grabbing and first person mode step aside).</summary>
    public static bool Piloting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPilotStatics() { Piloting = false; }

    private Camera cockpitCamera, downCamera, mainCam;
    private int savedMask;
    private RenderTexture downTexture;
    private GameObject cockpitCanvas, cockpitRig;
    private Transform headT, screenT, markerT;
    private TMP_Text infoText, statusText;
    private Renderer[] lampRenderers;
    private readonly List<Material> cockpitMaterials = new List<Material>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private readonly List<bool> hiddenStates = new List<bool>();
    private float pilotYaw, lockedY, lookYaw, lookPitch = -8f;
    private Vector3 pilotVel;
    private bool lookActive, cursorLocked;
    private Rigidbody hoverBody;
    private bool abducting;
    private Coroutine abductRoutine;
    private int enterFrame;
    private readonly object cockpitKey = new object();

    private bool CanPilot()
    {
        if (!Active || clicker == null || consumables == null || Piloting) return false;
        if (PixelTitleScreen.Showing || PixelPauseMenu.IsPaused || PixelMinigame.TakeoverActive || PixelFirstPerson.Active || PixelBank.HoseOn) return false;
        if (PixelCameraIntro.Moving || (Time.timeScale <= 0f && !PixelTimeStop.IsStopped)) return false;
        if (PixelPets.PopupOpen || PixelGuideVendor.Visiting) return false;
        return root != null;
    }

    private void EnterCockpit()
    {
        if (!CanPilot()) return;
        mainCam = Cam;
        if (mainCam == null) return;
        PixelWindows.CloseAllExcept(this);

        // Start where the UFO is (or where it would go next), at its hover height, looking at the cube.
        if (!hasPos)
        {
            PickWander();
            if (wanderTarget == Vector3.zero) return;
            pos = wanderTarget;
            hasPos = true;
        }
        lockedY = pos.y;
        Vector3 cube = clicker.PixelTransform != null ? clicker.PixelTransform.position : pos + Vector3.forward;
        Vector3 look = cube - pos;
        pilotYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
        lookYaw = 0f; lookPitch = -8f; pilotVel = Vector3.zero;

        savedMask = mainCam.cullingMask;
        float unit = UnitScale;

        // The cabin: one rig at the saucer, scaled so the model's units are the saucer's units.
        cockpitRig = new GameObject("UFO Cockpit");
        cockpitRig.transform.localScale = Vector3.one * unit;
        BuildInterior(cockpitRig.transform);

        // The pilot's head (the camera).
        GameObject head = new GameObject("UFO Pilot Head");
        headT = head.transform;
        headT.SetParent(cockpitRig.transform, false);
        headT.localPosition = new Vector3(0f, 0.42f, -0.2f);
        cockpitCamera = head.AddComponent<Camera>();
        cockpitCamera.CopyFrom(mainCam);
        cockpitCamera.depth = mainCam.depth + 1f;
        cockpitCamera.fieldOfView = cockpitFov;
        cockpitCamera.nearClipPlane = Mathf.Max(0.001f, 0.015f * unit);
        if (head.GetComponent<AudioListener>() != null) Destroy(head.GetComponent<AudioListener>());

        // The console screen's camera: straight down, into a square texture.
        downTexture = new RenderTexture(512, 512, 16) { name = "UFO Console Screen", filterMode = FilterMode.Bilinear };
        GameObject downObject = new GameObject("UFO Down Camera");
        downCamera = downObject.AddComponent<Camera>();
        downCamera.CopyFrom(mainCam);
        downCamera.cullingMask = savedMask;
        downCamera.targetTexture = downTexture;
        downCamera.depth = mainCam.depth - 5f;
        downCamera.fieldOfView = screenFov;
        downCamera.aspect = 1f;
        downCamera.clearFlags = CameraClearFlags.SolidColor;
        downCamera.backgroundColor = new Color(0.03f, 0.05f, 0.08f, 1f);
        downCamera.nearClipPlane = Mathf.Max(0.001f, unit * 0.1f);
        if (downObject.GetComponent<AudioListener>() != null) Destroy(downObject.GetComponent<AudioListener>());

        // The screen shows the downward camera.
        if (screenT != null)
        {
            Renderer sr = screenT.GetComponent<Renderer>();
            if (sr != null && sr.sharedMaterial != null) sr.sharedMaterial.mainTexture = downTexture;
        }

        mainCam.cullingMask = 0;   // the normal camera keeps its pose (view bounds, fog, floor fitting) but draws nothing

        // The saucer itself is hidden while you sit inside it.
        hiddenRenderers.Clear(); hiddenStates.Clear();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) { hiddenRenderers.Add(r); hiddenStates.Add(r.enabled); r.enabled = false; }

        BuildCockpitUi();
        if (bubbleCanvas != null) bubbleCanvas.SetActive(false);
        Piloting = true;
        PixelWindows.Register(cockpitKey, 125, () => Piloting, ExitCockpit);
        enterFrame = Time.frameCount;
        PixelAudio.Play("ui_click");
    }

    private void ExitCockpit()
    {
        if (!Piloting && cockpitCamera == null) return;
        Piloting = false;
        PixelWindows.Unregister(cockpitKey);
        if (abductRoutine != null) { StopCoroutine(abductRoutine); abductRoutine = null; }
        abducting = false;
        SetPilotCursor(false);
        if (mainCam != null) mainCam.cullingMask = savedMask;
        if (downCamera != null) { downCamera.targetTexture = null; Destroy(downCamera.gameObject); }
        if (downTexture != null) { downTexture.Release(); Destroy(downTexture); }
        if (cockpitRig != null) Destroy(cockpitRig);   // the head and its camera are children of the rig
        if (cockpitCanvas != null) Destroy(cockpitCanvas);
        foreach (Material m in cockpitMaterials) if (m != null) Destroy(m);
        cockpitMaterials.Clear();
        for (int i = 0; i < hiddenRenderers.Count; i++) if (hiddenRenderers[i] != null) hiddenRenderers[i].enabled = hiddenStates[i];
        hiddenRenderers.Clear(); hiddenStates.Clear();
        cockpitCamera = downCamera = null; downTexture = null; cockpitCanvas = null; cockpitRig = null;
        headT = screenT = markerT = null; hoverBody = null; lampRenderers = null;
        beamHold = 0f;
        beamWanted = false;
        hasPos = true;
        wanderTarget = pos;
        nextWander = Time.time + 4f;   // sits where you left it for a moment, then carries on
        velocity = Vector3.zero;
        ignoreClickFrame = Time.frameCount;
    }

    /// <summary>Is this point still inside the area the UFO may fly in (the part of the floor on screen, between the black bars)?</summary>
    private bool InFlightBounds(Vector3 p)
    {
        if (mainCam == null) return true;
        Vector3 sp = mainCam.WorldToScreenPoint(p);
        if (sp.z <= 0f) return false;
        return sp.x >= Screen.width * 0.06f && sp.x <= Screen.width * 0.94f && sp.y >= Screen.height * 0.16f && sp.y <= Screen.height * 0.86f;
    }

    private bool ValidAbductTarget(Rigidbody body)
    {
        if (body == null || !body.gameObject.activeInHierarchy || body.isKinematic) return false;
        if (body.GetComponent<OldPixelInfo>() == null || clicker.IsFlyingPixel(body)) return false;
        OldPixelDespawn d = body.GetComponent<OldPixelDespawn>();
        return d == null || (!d.Held && !d.IsDespawning);
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

    /// <summary>Called every frame instead of the UFO's own behaviour while you pilot it.</summary>
    private void UpdateCockpit()
    {
        if (cockpitCamera == null || downCamera == null || cockpitRig == null) { ExitCockpit(); return; }
        float dt = Time.deltaTime;
        bool playable = !PixelPauseMenu.IsPaused && Time.timeScale > 0f && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting;
        float pb = clicker.PixelBaseSize;

        // Looking round: hold the right mouse button (the cursor is hidden meanwhile); release it to use the console.
        if (playable && RightPressed() && !PointerOverUI()) lookActive = true;
        if (!RightHeld() || !playable) lookActive = false;
        SetPilotCursor(lookActive);
        if (lookActive)
        {
            Vector2 d = PilotMouseDelta();
            lookYaw = Mathf.Clamp(lookYaw + d.x * cockpitLookSpeed, -cockpitMaxHeadYaw, cockpitMaxHeadYaw);
            lookPitch = Mathf.Clamp(lookPitch - d.y * cockpitLookSpeed, -75f, 65f);
        }

        // Flying: momentum, bounded to the visible floor, height locked.
        Vector3 wish = Vector3.zero;
        if (playable && !PixelKeys.Typing)
        {
            float turn = (PixelKeys.Held(PixelAction.RotateRight) ? 1f : 0f) - (PixelKeys.Held(PixelAction.RotateLeft) ? 1f : 0f);
            pilotYaw += turn * pilotTurnDegrees * dt;
            float forward = (PixelKeys.KeyHeld(KeyCode.W) || PixelKeys.KeyHeld(KeyCode.UpArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.S) || PixelKeys.KeyHeld(KeyCode.DownArrow) ? 1f : 0f);
            float side = (PixelKeys.KeyHeld(KeyCode.D) || PixelKeys.KeyHeld(KeyCode.RightArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.A) || PixelKeys.KeyHeld(KeyCode.LeftArrow) ? 1f : 0f);
            Quaternion yawRot = Quaternion.Euler(0f, pilotYaw, 0f);
            wish = yawRot * new Vector3(side, 0f, forward);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
        }
        float speed = pilotSpeed * pb;
        pilotVel = Vector3.Lerp(pilotVel, wish * speed, 1f - Mathf.Exp(-pilotMomentum * dt));
        if (pilotVel.sqrMagnitude > 0.0001f)
        {
            Vector3 step = pilotVel * dt;
            Vector3 a = pos + step;
            Vector3 bx = pos + new Vector3(step.x, 0f, 0f);
            Vector3 bz = pos + new Vector3(0f, 0f, step.z);
            if (InFlightBounds(a)) pos = a;
            else if (InFlightBounds(bx)) { pos = bx; pilotVel.z = 0f; }
            else if (InFlightBounds(bz)) { pos = bz; pilotVel.x = 0f; }
            else pilotVel = Vector3.zero;
        }
        pos.y = lockedY;   // no climbing or diving

        // The cabin leans into the flight and bobs a little.
        float unit = UnitScale;
        Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, pilotYaw, 0f)) * pilotVel / Mathf.Max(0.001f, speed);
        float roll = -local.x * cabinLeanDegrees;
        float pitchLean = local.z * cabinLeanDegrees * 0.6f;
        float t = Time.unscaledTime;
        root.transform.position = pos;
        cockpitRig.transform.SetPositionAndRotation(pos + Vector3.up * (Mathf.Sin(t * 1.7f) * 0.012f * unit),
                                                   Quaternion.Euler(pitchLean, pilotYaw, roll));
        headT.localRotation = Quaternion.Euler(lookPitch, lookYaw, 0f);
        downCamera.transform.SetPositionAndRotation(UnderSide(), Quaternion.Euler(90f, pilotYaw, 0f));

        UpdateConsole(playable);

        beamWanted = abducting || beamHold > 0f;
        if (beamHold > 0f) beamHold -= dt;
        UpdateBeam();
    }

    /// <summary>Where the pointer is on the console screen (0..1 each way), if it is on it.</summary>
    private bool PointerOnScreen(out Vector2 uv)
    {
        uv = default;
        if (lookActive || screenT == null || PointerOverUI()) return false;
        Ray ray = cockpitCamera.ScreenPointToRay(PointerPosition());
        Plane plane = new Plane(-screenT.forward, screenT.position);
        if (!plane.Raycast(ray, out float enter)) return false;
        Vector3 local = screenT.InverseTransformPoint(ray.GetPoint(enter));
        if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.y) > 0.5f) return false;
        uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
        return true;
    }

    private static void SetMaterialColor(Renderer r, Color c)
    {
        if (r == null || r.sharedMaterial == null) return;
        if (r.sharedMaterial.HasProperty("_BaseColor")) r.sharedMaterial.SetColor("_BaseColor", c);
        if (r.sharedMaterial.HasProperty("_Color")) r.sharedMaterial.SetColor("_Color", c);
    }

    /// <summary>The console: which pixel the pointer is on (on the screen), its marker, the click, the gauges.</summary>
    private void UpdateConsole(bool playable)
    {
        hoverBody = null;
        if (playable && PointerOnScreen(out Vector2 uv))
        {
            Ray ray = downCamera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                Rigidbody body = h.rigidbody;
                if (body != null && ValidAbductTarget(body)) { hoverBody = body; break; }
            }
        }

        // The marker sits on the hovered pixel as the console screen draws it.
        bool marked = false;
        if (hoverBody != null && markerT != null)
        {
            Vector3 vpt = downCamera.WorldToViewportPoint(hoverBody.position);
            if (vpt.z > 0f && vpt.x > 0f && vpt.x < 1f && vpt.y > 0f && vpt.y < 1f)
            {
                markerT.localPosition = new Vector3(vpt.x - 0.5f, vpt.y - 0.5f, -0.003f);
                marked = true;
            }
        }
        if (markerT != null && markerT.gameObject.activeSelf != marked) markerT.gameObject.SetActive(marked);

        if (hoverBody != null && !abducting && LeftPressed() && Time.frameCount != enterFrame)
            abductRoutine = StartCoroutine(AbductFromCockpit(hoverBody));

        // Readouts and lamps.
        string target = "NONE";
        if (hoverBody != null)
        {
            OldPixelInfo info = hoverBody.GetComponent<OldPixelInfo>();
            if (info != null && info.tierIndex >= 0 && info.tierIndex < clicker.Tiers.Length)
                target = clicker.Tiers[info.tierIndex].displayName.Replace(" Pixels", "").Replace(" Pixel", "").ToUpperInvariant();
        }
        if (abducting) target = "ABDUCTING";
        float heading = (pilotYaw % 360f + 360f) % 360f;
        PixelUIKit.SetText(infoText, "HDG " + Mathf.RoundToInt(heading).ToString("000") + "\nTARGET\n" + target);

        bool bankOn = bankSys != null && bankSys.Active;
        PixelUIKit.SetText(statusText, bankOn ? (bankSys.IsFull ? "BANK FULL\nVALUE MODE" : "BANK READY") : "NO BANK\nVALUE MODE");
        if (lampRenderers != null)
            for (int i = 0; i < lampRenderers.Length; i++)
            {
                bool lit = abducting ? (Mathf.FloorToInt(Time.unscaledTime * 8f) % lampRenderers.Length == i) : (hoverBody != null || i == 0);
                SetMaterialColor(lampRenderers[i], lit ? new Color(0.4f, 1f, 0.55f, 1f) : new Color(0.1f, 0.22f, 0.14f, 1f));
            }
    }

    private IEnumerator AbductFromCockpit(Rigidbody body)
    {
        abducting = true;
        PixelAudio.Play("ui_click");
        OldPixelDespawn despawn = body != null ? body.GetComponent<OldPixelDespawn>() : null;
        float t = 0f;
        while (t < pilotAbductSeconds && Piloting)
        {
            if (!ValidAbductTarget(body)) break;
            beamTarget = body.position;
            if (despawn != null) despawn.KeepAlive();   // it doesn't age while the beam holds it
            t += Time.deltaTime;
            yield return null;
        }
        if (ValidAbductTarget(body))
        {
            if (bankSys != null && bankSys.Active && !bankSys.IsFull)
            {
                if (bankSys.StoreOldPixel(body, UnderSide())) PixelStats.Count("robot.piloted.banked");
            }
            else if (clicker.ReleaseOldPixel(body, true))
            {
                if (despawn != null) despawn.Begin();   // it shrinks away; its value was paid
                PixelStats.Count("robot.piloted.collected");
            }
        }
        beamHold = 0.4f;
        abducting = false;
        abductRoutine = null;
    }

    // ------------------------------------------------------------------
    // The cabin
    // ------------------------------------------------------------------

    private Renderer CabinPart(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, Quaternion? rot = null)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot ?? Quaternion.identity;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        Material m = clicker.CreateVisualMaterial(color, false);
        if (m != null) { r.sharedMaterial = m; cockpitMaterials.Add(m); }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    /// <summary>A square beam between two points.</summary>
    private void CabinStrut(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Color color)
    {
        Vector3 d = b - a;
        Vector3 up = Mathf.Abs(d.normalized.y) > 0.98f ? Vector3.forward : Vector3.up;
        CabinPart(parent, PrimitiveType.Cube, name, (a + b) * 0.5f, new Vector3(thickness, thickness, d.magnitude), color, Quaternion.LookRotation(d, up));
    }

    private TMP_Text CabinText(Transform parent, Vector3 pos, Color color)
    {
        GameObject go = new GameObject("Console Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * 0.1f;
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.fontSize = 3.2f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.overflowMode = TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = new Vector2(4.2f, 2.4f);
        if (clicker.UIFont != null) t.font = clicker.UIFont;
        return t;
    }

    private Material UnlitMaterial(Color c, int queue)
    {
        Shader s = PixelShaders.SpriteDefault();
        Material m = s != null ? new Material(s) : clicker.CreateVisualMaterial(c, false);
        if (m == null) return null;
        m.color = c;
        m.renderQueue = queue;
        cockpitMaterials.Add(m);
        return m;
    }

    private Renderer UnlitQuad(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = name;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Destroy(c);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    /// <summary>Seat, floor, struts, roof and the sloped console with its screen, built round the origin (forward +Z, up +Y, the eyes at 0, 0.42, -0.2).</summary>
    private void BuildInterior(Transform rig)
    {
        Color steel = new Color(0.17f, 0.19f, 0.24f), dark = new Color(0.09f, 0.1f, 0.13f), trim = new Color(1f, 0.55f, 0.12f),
              seat = new Color(0.42f, 0.13f, 0.13f), light = new Color(0.55f, 0.58f, 0.64f);

        // Shell.
        CabinPart(rig, PrimitiveType.Cube, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(1.7f, 0.04f, 1.9f), dark);
        CabinPart(rig, PrimitiveType.Cube, "Rear Wall", new Vector3(0f, 0.45f, -0.92f), new Vector3(1.7f, 1.0f, 0.05f), steel);
        CabinPart(rig, PrimitiveType.Cube, "Roof", new Vector3(0f, 0.99f, -0.3f), new Vector3(1.8f, 0.05f, 1.2f), steel);
        CabinPart(rig, PrimitiveType.Cube, "Roof Trim", new Vector3(0f, 0.955f, 0.29f), new Vector3(1.1f, 0.025f, 0.03f), trim);
        for (int s = -1; s <= 1; s += 2)
        {
            CabinPart(rig, PrimitiveType.Cube, "Side Wall", new Vector3(s * 0.86f, 0.15f, 0f), new Vector3(0.05f, 0.4f, 1.85f), steel);
            CabinStrut(rig, "Front Pillar", new Vector3(s * 0.8f, 0.3f, 0.7f), new Vector3(s * 0.55f, 0.97f, 0.25f), 0.075f, steel);
            CabinStrut(rig, "Mid Pillar", new Vector3(s * 0.85f, 0.3f, -0.2f), new Vector3(s * 0.85f, 0.97f, -0.2f), 0.06f, steel);
            CabinStrut(rig, "Rear Pillar", new Vector3(s * 0.85f, 0.3f, -0.88f), new Vector3(s * 0.85f, 0.97f, -0.88f), 0.06f, steel);
            CabinStrut(rig, "Roof Rail", new Vector3(s * 0.85f, 0.97f, -0.88f), new Vector3(s * 0.85f, 0.97f, 0f), 0.05f, light);
            CabinStrut(rig, "Roof Rail Front", new Vector3(s * 0.85f, 0.97f, 0f), new Vector3(s * 0.55f, 0.97f, 0.25f), 0.05f, light);
        }
        CabinStrut(rig, "Front Beam", new Vector3(-0.55f, 0.97f, 0.25f), new Vector3(0.55f, 0.97f, 0.25f), 0.07f, steel);

        // Overhead switch panel.
        CabinPart(rig, PrimitiveType.Cube, "Overhead Panel", new Vector3(0f, 0.945f, 0.05f), new Vector3(0.6f, 0.03f, 0.3f), dark);
        Color[] switchColors = { new Color(0.9f, 0.3f, 0.2f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.9f, 0.4f), new Color(0.3f, 0.7f, 1f), new Color(0.9f, 0.3f, 0.2f) };
        for (int i = 0; i < switchColors.Length; i++)
            CabinPart(rig, PrimitiveType.Cube, "Switch", new Vector3(-0.22f + i * 0.11f, 0.925f, 0.05f), new Vector3(0.05f, 0.025f, 0.06f), switchColors[i]);

        // The seat behind the pilot.
        CabinPart(rig, PrimitiveType.Cube, "Seat Cushion", new Vector3(0f, 0.08f, -0.3f), new Vector3(0.5f, 0.1f, 0.5f), seat);
        CabinPart(rig, PrimitiveType.Cube, "Seat Back", new Vector3(0f, 0.42f, -0.58f), new Vector3(0.5f, 0.75f, 0.1f), seat);
        CabinPart(rig, PrimitiveType.Cube, "Headrest", new Vector3(0f, 0.86f, -0.6f), new Vector3(0.3f, 0.18f, 0.1f), seat);

        // The console: a panel sloping up and away, facing the pilot. Its local Z points into the console, so things on the front sit at negative Z.
        GameObject consoleGo = new GameObject("Console");
        consoleGo.transform.SetParent(rig, false);
        consoleGo.transform.localPosition = new Vector3(0f, 0.08f, 0.5f);
        consoleGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.8f, 0.6f), new Vector3(0f, 0.6f, 0.8f));
        Transform con = consoleGo.transform;
        CabinPart(con, PrimitiveType.Cube, "Console Body", new Vector3(0f, 0f, 0.4f), new Vector3(1.6f, 0.9f, 0.7f), dark);
        CabinPart(con, PrimitiveType.Cube, "Console Face", new Vector3(0f, 0f, 0.02f), new Vector3(1.6f, 0.9f, 0.04f), steel);
        CabinPart(con, PrimitiveType.Cube, "Console Top Trim", new Vector3(0f, 0.455f, -0.01f), new Vector3(1.6f, 0.03f, 0.08f), trim);
        CabinPart(con, PrimitiveType.Cube, "Console Bottom Trim", new Vector3(0f, -0.455f, -0.01f), new Vector3(1.6f, 0.03f, 0.08f), trim);

        // Screen with a bezel.
        CabinPart(con, PrimitiveType.Cube, "Bezel", new Vector3(0f, 0.05f, -0.004f), new Vector3(0.58f, 0.58f, 0.02f), new Color(0.02f, 0.02f, 0.03f));
        Material screenMat = UnlitMaterial(Color.white, 3050);
        Renderer sr = UnlitQuad(con, "Screen", new Vector3(0f, 0.05f, -0.016f), new Vector3(0.5f, 0.5f, 1f), screenMat);
        screenT = sr.transform;
        Material white = UnlitMaterial(new Color(1f, 1f, 1f, 0.4f), 3100);
        UnlitQuad(screenT, "Cross H", new Vector3(0f, 0f, -0.002f), new Vector3(0.07f, 0.004f, 1f), white);
        UnlitQuad(screenT, "Cross V", new Vector3(0f, 0f, -0.002f), new Vector3(0.004f, 0.07f, 1f), white);
        GameObject marker = new GameObject("Marker");
        marker.transform.SetParent(screenT, false);
        markerT = marker.transform;
        Material green = UnlitMaterial(new Color(0.4f, 1f, 0.55f, 1f), 3110);
        UnlitQuad(markerT, "T", new Vector3(0f, 0.065f, 0f), new Vector3(0.13f, 0.012f, 1f), green);
        UnlitQuad(markerT, "B", new Vector3(0f, -0.065f, 0f), new Vector3(0.13f, 0.012f, 1f), green);
        UnlitQuad(markerT, "L", new Vector3(-0.065f, 0f, 0f), new Vector3(0.012f, 0.13f, 1f), green);
        UnlitQuad(markerT, "R", new Vector3(0.065f, 0f, 0f), new Vector3(0.012f, 0.13f, 1f), green);
        marker.SetActive(false);

        // Lamps under the screen.
        lampRenderers = new Renderer[6];
        for (int i = 0; i < lampRenderers.Length; i++)
            lampRenderers[i] = CabinPart(con, PrimitiveType.Cube, "Lamp", new Vector3(-0.25f + i * 0.1f, -0.3f, -0.015f), new Vector3(0.06f, 0.04f, 0.03f), new Color(0.1f, 0.22f, 0.14f));

        // Side panels: readouts, button grids and two levers.
        infoText = CabinText(con, new Vector3(-0.52f, 0.17f, -0.03f), new Color(0.5f, 1f, 0.65f, 1f));
        statusText = CabinText(con, new Vector3(0.52f, 0.17f, -0.03f), new Color(1f, 0.85f, 0.4f, 1f));
        Color[] btn = { new Color(0.85f, 0.25f, 0.2f), new Color(1f, 0.75f, 0.2f), new Color(0.3f, 0.85f, 0.4f), new Color(0.3f, 0.65f, 1f) };
        for (int s = -1; s <= 1; s += 2)
        {
            for (int i = 0; i < 6; i++)
                CabinPart(con, PrimitiveType.Cube, "Button", new Vector3(s * (0.43f + (i % 3) * 0.1f), -0.08f - (i / 3) * 0.1f, -0.02f), new Vector3(0.06f, 0.06f, 0.03f), btn[(i + (s > 0 ? 2 : 0)) % btn.Length]);
            Vector3 basePos = new Vector3(s * 0.66f, -0.3f, -0.03f);
            CabinPart(con, PrimitiveType.Cylinder, "Lever", basePos + new Vector3(0f, 0f, -0.08f), new Vector3(0.035f, 0.08f, 0.035f), light, Quaternion.Euler(90f, 0f, 0f));
            CabinPart(con, PrimitiveType.Sphere, "Lever Knob", basePos + new Vector3(0f, 0f, -0.17f), new Vector3(0.08f, 0.08f, 0.08f), trim);
        }
    }

    /// <summary>The little screen-space bits: a hint line and the Leave button (everything else is in the cabin).</summary>
    private void BuildCockpitUi()
    {
        TMP_FontAsset font = clicker.UIFont;
        cockpitCanvas = PixelUIKit.CreateCanvas("UFO Cockpit", 500, new Vector2(1920f, 1080f), true);
        Transform c = cockpitCanvas.transform;

        TMP_Text hint = PixelUIKit.CreateText(font, c, "Hint", string.Format(pilotHint, PixelKeys.Name(PixelAction.RotateLeft), PixelKeys.Name(PixelAction.RotateRight)),
                                              26f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.9f));
        hint.raycastTarget = false;
        hint.enableAutoSizing = true; hint.fontSizeMax = 26f; hint.fontSizeMin = 14f;
        RectTransform hr = hint.rectTransform;
        float top = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
        hr.offsetMin = new Vector2(360f, -(top + 70f)); hr.offsetMax = new Vector2(-360f, -(top + 14f));

        Button leave = PixelUIKit.CreateButton(font, c, "Leave", leaveText, new Vector2(220f, 64f), new Color(0.5f, 0.2f, 0.2f, 1f), Color.white, 28f);
        PixelUIKit.Caps(leave);
        RectTransform lr = leave.GetComponent<RectTransform>();
        lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(1f, 1f);
        lr.anchoredPosition = new Vector2(-30f, -(top + 14f));
        leave.onClick.AddListener(ExitCockpit);
    }
}
