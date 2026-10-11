using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// "Take control" for the UFO Helper (part of <see cref="PixelRobotWorker"/>): choose it in the UFO's window and you sit in the cockpit.
/// You see out through the canopy from the saucer (a second camera, like first person mode) and fly it with WASD, turning with the rotate keys (Q / E);
/// you can only fly inside the screen's bounds and never up or down. On the console in front of you a screen shows the view straight down
/// (a third camera drawing into a RenderTexture); point at a pixel on that screen and click it to abduct it: the beam switches on and the pixel is
/// pulled into the Pixel Bank (or, without a bank or when it is full, collected for its value). Escape or the Leave button climbs out; the UFO then
/// carries on from where you left it.
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

    [Tooltip("Field of view of the view out of the canopy (degrees).")]
    [SerializeField] private float cockpitFov = 70f;

    [Tooltip("Field of view of the downward camera that feeds the console screen (degrees). Smaller = a closer, more zoomed view.")]
    [SerializeField] private float screenFov = 34f;

    [Tooltip("Seconds the beam holds a pixel before it is pulled up when you click it from the cockpit.")]
    [SerializeField] private float pilotAbductSeconds = 0.6f;

    [TextArea(1, 3)]
    [SerializeField] private string pilotHint = "{0} / {1} turn  -  WASD fly  -  click a pixel on the screen to abduct it  -  Esc leaves";

    [SerializeField] private string leaveText = "Leave";

    /// <summary>True while you pilot the UFO (cube clicks, Pixel Grabbing and first person mode step aside).</summary>
    public static bool Piloting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPilotStatics() { Piloting = false; }

    private Camera cockpitCamera, downCamera, mainCam;
    private int savedMask;
    private RenderTexture downTexture;
    private GameObject cockpitCanvas;
    private RectTransform screenRect, markerRect;
    private TMP_Text infoLabel, statusLabel;
    private Image[] lamps;
    private float pilotYaw, lockedY, pilotPitch = 24f;
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

        savedMask = mainCam.cullingMask;

        GameObject camObject = new GameObject("UFO Cockpit Camera");
        cockpitCamera = camObject.AddComponent<Camera>();
        cockpitCamera.CopyFrom(mainCam);
        cockpitCamera.depth = mainCam.depth + 1f;
        cockpitCamera.fieldOfView = cockpitFov;
        cockpitCamera.nearClipPlane = Mathf.Max(0.001f, UnitScale * 0.05f);

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
        downCamera.nearClipPlane = Mathf.Max(0.001f, UnitScale * 0.1f);

        mainCam.cullingMask = 0;   // the normal camera keeps its pose (view bounds, fog, floor fitting) but draws nothing

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
        if (mainCam != null) mainCam.cullingMask = savedMask;
        if (cockpitCamera != null) Destroy(cockpitCamera.gameObject);
        if (downCamera != null) { downCamera.targetTexture = null; Destroy(downCamera.gameObject); }
        if (downTexture != null) { downTexture.Release(); Destroy(downTexture); }
        if (cockpitCanvas != null) Destroy(cockpitCanvas);
        cockpitCamera = downCamera = null; downTexture = null; cockpitCanvas = null; hoverBody = null;
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

    /// <summary>Called every frame instead of the UFO's own behaviour while you pilot it.</summary>
    private void UpdateCockpit()
    {
        if (cockpitCamera == null || downCamera == null || cockpitCanvas == null) { ExitCockpit(); return; }
        float dt = Time.deltaTime;
        bool playable = !PixelPauseMenu.IsPaused && Time.timeScale > 0f && !PixelPets.PopupOpen && !PixelGuideVendor.Visiting;
        float pb = clicker.PixelBaseSize;

        if (playable && !PixelKeys.Typing)
        {
            float turn = (PixelKeys.Held(PixelAction.RotateRight) ? 1f : 0f) - (PixelKeys.Held(PixelAction.RotateLeft) ? 1f : 0f);
            pilotYaw += turn * pilotTurnDegrees * dt;

            float forward = (PixelKeys.KeyHeld(KeyCode.W) || PixelKeys.KeyHeld(KeyCode.UpArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.S) || PixelKeys.KeyHeld(KeyCode.DownArrow) ? 1f : 0f);
            float side = (PixelKeys.KeyHeld(KeyCode.D) || PixelKeys.KeyHeld(KeyCode.RightArrow) ? 1f : 0f) - (PixelKeys.KeyHeld(KeyCode.A) || PixelKeys.KeyHeld(KeyCode.LeftArrow) ? 1f : 0f);
            Vector3 fwd = Quaternion.Euler(0f, pilotYaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, pilotYaw, 0f) * Vector3.right;
            Vector3 wish = fwd * forward + right * side;
            if (wish.sqrMagnitude > 0.0001f)
            {
                Vector3 step = wish.normalized * pilotSpeed * pb * dt;
                Vector3 a = pos + step;
                Vector3 bx = pos + new Vector3(step.x, 0f, 0f);
                Vector3 bz = pos + new Vector3(0f, 0f, step.z);
                if (InFlightBounds(a)) pos = a;
                else if (InFlightBounds(bx)) pos = bx;
                else if (InFlightBounds(bz)) pos = bz;
            }
        }
        pos.y = lockedY;   // no climbing or diving

        float unit = UnitScale;
        root.transform.position = pos;
        cockpitCamera.transform.SetPositionAndRotation(pos + Vector3.up * 0.2f * unit, Quaternion.Euler(pilotPitch, pilotYaw, 0f));
        downCamera.transform.SetPositionAndRotation(UnderSide(), Quaternion.Euler(90f, pilotYaw, 0f));

        UpdateConsole(playable);

        beamWanted = abducting || beamHold > 0f;
        if (beamHold > 0f) beamHold -= dt;
        UpdateBeam();
    }

    /// <summary>The console: which pixel the pointer is on (on the screen), its marker, the click, the gauges.</summary>
    private void UpdateConsole(bool playable)
    {
        hoverBody = null;
        bool onScreen = false;
        Vector2 local = Vector2.zero;
        if (playable && screenRect != null)
        {
            onScreen = RectTransformUtility.ScreenPointToLocalPointInRectangle(screenRect, PointerPosition(), null, out local) &&
                       screenRect.rect.Contains(local);
        }
        if (onScreen)
        {
            Rect r = screenRect.rect;
            Vector2 vp = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            Ray ray = downCamera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
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
        if (hoverBody != null)
        {
            Vector3 vpt = downCamera.WorldToViewportPoint(hoverBody.position);
            if (vpt.z > 0f && vpt.x > 0f && vpt.x < 1f && vpt.y > 0f && vpt.y < 1f)
            {
                Rect r = screenRect.rect;
                markerRect.anchoredPosition = new Vector2(r.xMin + vpt.x * r.width, r.yMin + vpt.y * r.height);
                marked = true;
            }
        }
        if (markerRect.gameObject.activeSelf != marked) markerRect.gameObject.SetActive(marked);

        if (hoverBody != null && !abducting && LeftPressed() && Time.frameCount != enterFrame)
            abductRoutine = StartCoroutine(AbductFromCockpit(hoverBody));

        // Gauges.
        PixelClicker.PixelTier tier = null;
        if (hoverBody != null)
        {
            OldPixelInfo info = hoverBody.GetComponent<OldPixelInfo>();
            if (info != null && info.tierIndex >= 0 && info.tierIndex < clicker.Tiers.Length) tier = clicker.Tiers[info.tierIndex];
        }
        string target = abducting ? "ABDUCTING..." : tier != null ? "TARGET: " + tier.displayName.ToUpperInvariant() : "NO TARGET";
        float heading = (pilotYaw % 360f + 360f) % 360f;
        PixelUIKit.SetText(infoLabel, "HDG " + Mathf.RoundToInt(heading).ToString("000") + "\n" + target);

        bool bankReady = bankSys != null && bankSys.Active && !bankSys.IsFull;
        PixelUIKit.SetText(statusLabel, bankReady ? "BANK READY" : (bankSys != null && bankSys.Active ? "BANK FULL\nCOLLECTING FOR VALUE" : "NO BANK\nCOLLECTING FOR VALUE"));
        if (lamps != null)
            for (int i = 0; i < lamps.Length; i++)
            {
                bool lit = abducting ? (Mathf.FloorToInt(Time.unscaledTime * 8f) % lamps.Length == i) : (hoverBody != null ? i < lamps.Length : i == 0);
                lamps[i].color = lit ? new Color(0.4f, 1f, 0.55f, 1f) : new Color(0.12f, 0.2f, 0.14f, 1f);
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
    // The cockpit's screen furniture
    // ------------------------------------------------------------------

    private static Image Bar(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        return im;
    }

    private void BuildCockpitUi()
    {
        TMP_FontAsset font = clicker.UIFont;
        cockpitCanvas = PixelUIKit.CreateCanvas("UFO Cockpit", 500, new Vector2(1920f, 1080f), true);
        Transform c = cockpitCanvas.transform;
        Color frame = new Color(0.06f, 0.07f, 0.1f, 1f), trim = new Color(1f, 0.55f, 0.12f, 1f);

        // The canopy: dark struts round the edge of the view.
        Bar(c, "Top Strut", frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), Vector2.zero);
        Bar(c, "Top Trim", trim, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -76f), new Vector2(0f, -70f));
        Bar(c, "Left Strut", frame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(54f, 0f));
        Bar(c, "Right Strut", frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-54f, 0f), Vector2.zero);
        Bar(c, "Left Trim", trim, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(54f, 0f), new Vector2(60f, 0f));
        Bar(c, "Right Trim", trim, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-60f, 0f), new Vector2(-54f, 0f));

        // The console along the bottom.
        const float consoleH = 340f;
        Image console = Bar(c, "Console", new Color(0.09f, 0.1f, 0.14f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, consoleH));
        console.raycastTarget = true;   // clicks on the console never reach the game behind it
        Bar(c, "Console Trim", trim, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, consoleH), new Vector2(0f, consoleH + 6f));
        Bar(c, "Console Shade", new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, consoleH - 40f), new Vector2(0f, consoleH));

        // The screen: the view straight down.
        const float screenSize = 290f;
        Image bezel = Bar(c, "Screen Bezel", new Color(0.02f, 0.02f, 0.03f, 1f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                          new Vector2(-screenSize * 0.5f - 12f, 18f), new Vector2(screenSize * 0.5f + 12f, 18f + screenSize + 24f));
        PixelUIKit.StyleBox(bezel, true);
        GameObject screenGo = new GameObject("Screen", typeof(RectTransform), typeof(RawImage));
        screenGo.transform.SetParent(c, false);
        RawImage raw = screenGo.GetComponent<RawImage>();
        raw.texture = downTexture;
        raw.raycastTarget = false;
        screenRect = screenGo.GetComponent<RectTransform>();
        screenRect.anchorMin = screenRect.anchorMax = new Vector2(0.5f, 0f);
        screenRect.pivot = new Vector2(0.5f, 0f);
        screenRect.sizeDelta = new Vector2(screenSize, screenSize);
        screenRect.anchoredPosition = new Vector2(0f, 30f);

        // Crosshair on the screen (the beam comes straight down the middle) + the hover marker.
        Image h1 = Bar(screenRect, "Cross H", new Color(1f, 1f, 1f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-18f, -1f), new Vector2(18f, 1f));
        Image v1 = Bar(screenRect, "Cross V", new Color(1f, 1f, 1f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1f, -18f), new Vector2(1f, 18f));
        h1.rectTransform.SetAsLastSibling(); v1.rectTransform.SetAsLastSibling();

        GameObject marker = new GameObject("Marker", typeof(RectTransform));
        marker.transform.SetParent(screenRect, false);
        markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0f, 0f);   // positioned from the screen's lower-left corner
        markerRect.sizeDelta = new Vector2(64f, 64f);
        Color mc = new Color(0.4f, 1f, 0.55f, 1f);
        Bar(markerRect, "T", mc, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -3f), Vector2.zero);
        Bar(markerRect, "B", mc, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));
        Bar(markerRect, "L", mc, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
        Bar(markerRect, "R", mc, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 0f), Vector2.zero);
        marker.SetActive(false);
        // The marker is anchored to the screen's centre-based coordinates: make the screen rect's lower-left the origin.
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);

        // Left: gauges. Right: bank status and the Leave button.
        infoLabel = PixelUIKit.CreateText(font, c, "Info", "", 30f, TextAlignmentOptions.TopLeft, FontStyles.Bold, new Color(0.5f, 1f, 0.65f, 1f));
        infoLabel.raycastTarget = false;
        RectTransform ir = infoLabel.rectTransform;
        ir.anchorMin = ir.anchorMax = new Vector2(0f, 0f); ir.pivot = new Vector2(0f, 0f);
        ir.sizeDelta = new Vector2(520f, 120f); ir.anchoredPosition = new Vector2(110f, consoleH - 150f);

        lamps = new Image[6];
        for (int i = 0; i < lamps.Length; i++)
            lamps[i] = Bar(c, "Lamp " + i, new Color(0.12f, 0.2f, 0.14f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                           new Vector2(110f + i * 46f, 40f), new Vector2(110f + i * 46f + 32f, 72f));

        statusLabel = PixelUIKit.CreateText(font, c, "Status", "", 28f, TextAlignmentOptions.TopRight, FontStyles.Bold, new Color(1f, 0.85f, 0.4f, 1f));
        statusLabel.raycastTarget = false;
        RectTransform sr = statusLabel.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(1f, 0f); sr.pivot = new Vector2(1f, 0f);
        sr.sizeDelta = new Vector2(520f, 100f); sr.anchoredPosition = new Vector2(-110f, consoleH - 130f);

        Button leave = PixelUIKit.CreateButton(font, c, "Leave", leaveText, new Vector2(260f, 70f), new Color(0.5f, 0.2f, 0.2f, 1f), Color.white, 30f);
        PixelUIKit.Caps(leave);
        RectTransform lr = leave.GetComponent<RectTransform>();
        lr.anchorMin = lr.anchorMax = new Vector2(1f, 0f); lr.pivot = new Vector2(1f, 0f);
        lr.anchoredPosition = new Vector2(-110f, 40f);
        leave.onClick.AddListener(ExitCockpit);

        TMP_Text hint = PixelUIKit.CreateText(font, c, "Hint", string.Format(pilotHint, PixelKeys.Name(PixelAction.RotateLeft), PixelKeys.Name(PixelAction.RotateRight)),
                                              24f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 1f, 1f, 0.85f));
        hint.raycastTarget = false;
        hint.enableAutoSizing = true; hint.fontSizeMax = 24f; hint.fontSizeMin = 14f;
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(0.5f, 0f);
        hr.offsetMin = new Vector2(80f, consoleH + 14f); hr.offsetMax = new Vector2(-80f, consoleH + 54f);
    }
}
