using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// UFO Helper (shop upgrade <c>unlocksRobot</c>, Upgrades tab; the class keeps its old name so saves and the shop keep working): a small
/// flying saucer idly hovers about the map, never straight above the cube, with a speech bubble over it (an alien face) so you can always
/// find it. Click it and it asks which consumable to use; pick one (Vacuum, Fan, Sorter, Charge Booster, Lightning Rod, Sprinkler) and
/// place its "ghost" where you want it (nothing is used up yet). From then on, every time that device runs out, the UFO flies over the
/// ghost and its beam drops a fresh one down, already switched on, as long as you have one to spare. With the "bank a pixel" job it flies
/// over the nearest loose pixel of the chosen type and abducts it into your Pixel Bank with its beam.
/// </summary>
public class PixelRobotWorker : MonoBehaviour
{
    [Header("UFO")]
    [Tooltip("Turn the UFO Helper off completely.")]
    [SerializeField] private bool disableRobot = false;

    [Tooltip("How fast it flies, in main-pixel widths per second.")]
    [SerializeField] private float flySpeed = 6f;

    [Tooltip("How high it hovers while idling, in main-pixel widths above the floor.")]
    [SerializeField] private float hoverHeight = 3.4f;

    [Tooltip("How high it hovers while working (dropping a device / abducting a pixel), in main-pixel widths above the floor.")]
    [SerializeField] private float workHeight = 2.6f;

    [Tooltip("When idling it never goes closer to the cube than this, in main-pixel widths (measured on the floor).")]
    [SerializeField] private float minDistanceFromCube = 2.5f;

    [Tooltip("When idling it never goes further from the cube than this, in main-pixel widths (measured on the floor).")]
    [SerializeField] private float maxRoamDistance = 8f;

    [Tooltip("When idling it stays at least this far from the cube ON SCREEN, as a fraction of the screen height, so it is never right over the cube.")]
    [SerializeField] private float minScreenGapFraction = 0.2f;

    [Tooltip("Seconds between picking a new spot to drift to while idling (min).")]
    [SerializeField] private float wanderMinSeconds = 2.5f;

    [Tooltip("Seconds between picking a new spot to drift to while idling (max).")]
    [SerializeField] private float wanderMaxSeconds = 5.5f;

    [Tooltip("How far to the side of the cube it hovers when it has to work on the Sorter (which sits round the cube), in main-pixel widths.")]
    [SerializeField] private float sorterStandOff = 3f;

    [Tooltip("Size of the speech bubble with its face (canvas units).")]
    [SerializeField] private float bubbleSize = 96f;

    [Tooltip("Turn off the bubble over the UFO.")]
    [SerializeField] private bool hideBubble = false;

    [Tooltip("Its size, as a multiple of the main pixel: 2.5 makes it about one and a half cubes wide. Fixed - it does not change with the floor style.")]
    [SerializeField] private float robotScale = 2.5f;

    [Tooltip("Seconds the beam works before a new device appears on the ghost.")]
    [SerializeField] private float placeDelay = 1.2f;

    [Tooltip("Seconds the beam holds a loose pixel before it is pulled into the Pixel Bank.")]
    [SerializeField] private float abductSeconds = 0.7f;

    [Tooltip("Seconds a device takes to drop down the beam onto its spot.")]
    [SerializeField] private float dropSeconds = 0.5f;

    [Tooltip("Seconds between looks at whether the device is still there.")]
    [SerializeField] private float checkInterval = 0.3f;

    [Tooltip("Width of the beam's lower end, as a multiple of the UFO's size.")]
    [SerializeField] private float beamRadius = 0.9f;

    [Header("Colours")]
    [SerializeField] private Color bodyColor = new Color(0.62f, 0.66f, 0.74f, 1f);
    [SerializeField] private Color accentColor = new Color(1f, 0.55f, 0.12f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.3f, 0.95f, 1f, 1f);
    [SerializeField] private Color darkColor = new Color(0.12f, 0.13f, 0.18f, 1f);
    [SerializeField] private Color alienColor = new Color(0.45f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color beamColor = new Color(0.55f, 1f, 0.7f, 1f);

    [Header("Texts")]
    [SerializeField] private string ufoTitle = "UFO Helper";
    [SerializeField] private string askText = "Which device should I keep running? Pick one, then choose where it goes.";
    [SerializeField] private string noDevicesText = "You have no devices to work with yet.";
    [SerializeField] private string clearJobText = "Stop working";
    [SerializeField] private string ufoJobSetFormat = "The UFO will keep a {0} running there";
    [SerializeField] private string bankPixelsText = "Abduct a pixel into the bank";
    [SerializeField] private string pickPixelText = "Which pixel should I abduct and put in your Pixel Bank?";
    [SerializeField] private string ufoBankJobFormat = "The UFO will abduct {0} and bank them";

    private PixelClicker clicker;
    private PixelConsumables consumables;
    private bool active;

    // The job.
    private int jobDevice = -1;
    private Vector3 jobPos;
    private float jobYaw, jobBend;
    private GameObject ghost;
    private bool occupied;
    private float nextCheck, waitT;

    // The UFO.
    private GameObject root;
    private Transform bodyT, lightsPivot, alienHead;
    private Renderer[] lightRenderers;
    private Renderer domeRenderer;
    private BoxCollider box;
    private Vector3 pos, velocity, wanderTarget;
    private bool hasPos;
    private float nextWander, floorY, nextFloorCheck;
    private bool hovering, flying;
    private Quaternion tilt = Quaternion.identity;
    private float fadeAmount, fadeAlpha = 1f;
    private Image bubbleImage;

    private class Part
    {
        public Renderer renderer;
        public Color color;
        public Material solidMaterial, ghostMaterial;
        public bool transparentBase, isLight;
    }
    private readonly System.Collections.Generic.List<Part> parts = new System.Collections.Generic.List<Part>();

    // The beam.
    private GameObject beam;
    private Mesh beamMesh;
    private Color[] beamColors;
    private Vector3 beamTarget;
    private bool beamWanted;
    private float beamAmount, beamHold;
    private const int BeamSegments = 18;

    // The bubble with its face.
    private GameObject bubbleCanvas;
    private Canvas bubbleCanvasComponent;
    private RectTransform bubbleRect;
    private Sprite bubbleSprite;

    // The bank job: it abducts one pixel type and stores it in the Pixel Bank.
    private bool bankJob;
    private PixelClicker.PixelType bankType;
    private Rigidbody bankTarget;
    private float bankRetarget, bankCooldown, abductT;
    private PixelBank bankSys;

    // The window.
    private GameObject windowRoot;
    private RectTransform windowPanel;
    private int ignoreClickFrame = -1;

    /// <summary>Kept for the click-block check: always false now, because the UFO is click-through (left clicks pass straight through it).</summary>
    public static bool Hovering;

    [Tooltip("How see-through the UFO gets while the mouse is over it (0 = invisible, 1 = solid). Left clicks go through it; right-click opens its window.")]
    [SerializeField] private float hoverAlpha = 0.25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Hovering = false; }

    public bool Active => active && !disableRobot && !userDisabled;

    /// <summary>The upgrade was bought (even if the player switched it off in Toggles).</summary>
    public bool Bought => active && !disableRobot;

    private bool userDisabled;

    /// <summary>The player switched the UFO off with the Toggles window. Saved.</summary>
    public bool UserDisabled
    {
        get => userDisabled;
        set
        {
            userDisabled = value;
            if (userDisabled) { Hovering = false; HideAll(); CloseWindow(); }
        }
    }

    /// <summary>The shop upgrade was bought.</summary>
    public void Activate() { active = true; }

    /// <summary>The upgrade is not owned (the job, if any, is kept for when it is).</summary>
    public void Deactivate()
    {
        active = false;
        Hovering = false;
        HideAll();
        CloseWindow();
    }

    private void HideAll()
    {
        if (root != null && root.activeSelf) root.SetActive(false);
        if (beam != null && beam.activeSelf) beam.SetActive(false);
        if (bubbleCanvas != null && bubbleCanvas.activeSelf) bubbleCanvas.SetActive(false);
        if (ghost != null && ghost.activeSelf) ghost.SetActive(false);
    }

    private void Start()
    {
        clicker = PixelFind.First<PixelClicker>();
        consumables = PixelFind.First<PixelConsumables>();
        bankSys = PixelFind.First<PixelBank>();
        PixelWindows.Register(this, 55, () => windowRoot != null && windowRoot.activeSelf, CloseWindow);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        Hovering = false;
        if (root != null) Destroy(root);
        if (beam != null) Destroy(beam);
        if (bubbleCanvas != null) Destroy(bubbleCanvas);
        if (ghost != null) Destroy(ghost);
        if (windowRoot != null) Destroy(windowRoot);
    }

    // ------------------------------------------------------------------
    // Per frame
    // ------------------------------------------------------------------

    private void Update()
    {
        if (clicker == null || consumables == null) return;
        bool show = Active && !PixelTitleScreen.Showing && !PixelMinigame.TakeoverActive && !PixelCameraIntro.Moving;
        if (!show)
        {
            Hovering = false;
            if (PixelCameraIntro.Moving) { hasPos = false; nextWander = 0f; }   // the camera is swinging about: it reappears once it settles
            HideAll();
            return;
        }

        if (root == null) BuildUfo();
        if (!root.activeSelf) root.SetActive(true);

        UpdateFloor();
        UpdateMovement();
        UpdateHoverAndClick();
        UpdateJob();
        UpdateBeam();
        Animate();
        UpdateBubble();
    }

    private Camera Cam => clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

    /// <summary>One model unit in world units (a fixed size relative to the main pixel).</summary>
    private float UnitScale => clicker.PixelBaseSize * robotScale / 2.5f;

    private bool HasJob => jobDevice >= 0 && jobDevice < consumables.DeviceCount;

    /// <summary>Remembers the height of the floor (the UFO hovers a set number of pixel widths above it).</summary>
    private void UpdateFloor()
    {
        if (Time.unscaledTime < nextFloorCheck) return;
        nextFloorCheck = Time.unscaledTime + 1f;
        if (consumables.TryGetFloorPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.3f), out Vector3 p)) floorY = p.y;
        else if (clicker.PixelTransform != null) floorY = clicker.PixelTransform.position.y - clicker.PixelBaseSize * 0.5f;
    }

    /// <summary>A point above 'flat' at the given height over the floor, but never lower than a little above the top of the cube.</summary>
    private Vector3 Hover(Vector3 flat, float heightInPixels)
    {
        float y = floorY + heightInPixels * clicker.PixelBaseSize;
        if (clicker.PixelTransform != null) y = Mathf.Max(y, clicker.PixelTransform.position.y + clicker.PixelBaseSize * 1.4f);
        return new Vector3(flat.x, y, flat.z);
    }

    /// <summary>Picks a new spot to drift to: round the cube, on screen, never right over it.</summary>
    private void PickWander()
    {
        Camera cam = Cam;
        if (cam == null || clicker.PixelTransform == null) return;
        float pb = clicker.PixelBaseSize;
        Vector3 c = clicker.PixelTransform.position;
        Vector3 cubeScreen = cam.WorldToScreenPoint(c);
        for (int i = 0; i < 16; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r = UnityEngine.Random.Range(minDistanceFromCube, Mathf.Max(minDistanceFromCube + 0.1f, maxRoamDistance)) * pb;
            Vector3 p = Hover(c + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r, hoverHeight);
            Vector3 sp = cam.WorldToScreenPoint(p);
            if (sp.z <= 0f) continue;
            if (sp.x < Screen.width * 0.08f || sp.x > Screen.width * 0.92f || sp.y < Screen.height * 0.16f || sp.y > Screen.height * 0.86f) continue;
            if (new Vector2(sp.x - cubeScreen.x, sp.y - cubeScreen.y).magnitude < Screen.height * minScreenGapFraction) continue;
            if (sp.y < cubeScreen.y + Screen.height * 0.06f) continue;   // always higher up the screen than the cube
            if ((p - pos).magnitude < 1.5f * pb) continue;   // worth the trip
            wanderTarget = p;
            return;
        }
        if (wanderTarget == Vector3.zero) wanderTarget = Hover(c + cam.transform.right * 3f * pb, hoverHeight);
    }

    /// <summary>The flat direction to the right on screen (for hovering beside the cube).</summary>
    private Vector3 ScreenRight()
    {
        Camera cam = Cam;
        Vector3 r = cam != null ? cam.transform.right : Vector3.right;
        r.y = 0f;
        return r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;
    }

    /// <summary>Where the beam should end for the current job: the spot on the floor (for the Sorter, the foot of the cube).</summary>
    private Vector3 JobFloorPoint()
    {
        if (consumables.GetDevice(jobDevice).kind == PixelConsumables.DeviceKind.Sorter && clicker.PixelTransform != null)
        {
            Vector3 c = clicker.PixelTransform.position;
            return new Vector3(c.x, floorY, c.z);
        }
        return new Vector3(jobPos.x, floorY, jobPos.z);
    }

    /// <summary>Where it wants to be: over its work, else drifting about.</summary>
    private Vector3 WantedSpot()
    {
        if (bankJob)
        {
            if (bankTarget != null)
            {
                Vector3 p = bankTarget.position;
                return Hover(new Vector3(p.x, 0f, p.z), workHeight);
            }
        }
        else if (HasJob && !occupied && consumables.DeviceOwned(jobDevice) > 0)
        {
            Vector3 f = JobFloorPoint();
            bool sorter = consumables.GetDevice(jobDevice).kind == PixelConsumables.DeviceKind.Sorter;
            if (sorter) f += ScreenRight() * sorterStandOff * clicker.PixelBaseSize;   // not right above the cube
            return Hover(f, workHeight);
        }
        return wanderTarget;
    }

    private bool WorkingSpot()
    {
        if (bankJob) return bankTarget != null;
        return HasJob && !occupied && consumables.DeviceOwned(jobDevice) > 0;
    }

    private bool AtWork()
    {
        if (!hasPos || !WorkingSpot()) return false;
        Vector3 d = WantedSpot() - pos; d.y = 0f;
        return d.magnitude < 0.3f * clicker.PixelBaseSize && velocity.magnitude < 0.8f * clicker.PixelBaseSize;
    }

    private void UpdateMovement()
    {
        float pb = clicker.PixelBaseSize;
        bool working = WorkingSpot();
        if (!working && (Time.time >= nextWander || wanderTarget == Vector3.zero))
        {
            nextWander = Time.time + UnityEngine.Random.Range(wanderMinSeconds, Mathf.Max(wanderMinSeconds, wanderMaxSeconds));
            PickWander();
        }
        if (!hasPos)
        {
            if (wanderTarget == Vector3.zero) PickWander();
            if (wanderTarget == Vector3.zero) return;
            hasPos = true;
            pos = wanderTarget;
            velocity = Vector3.zero;
        }

        Vector3 target = WantedSpot();
        if (target == Vector3.zero) target = pos;
        Vector3 delta = target - pos;
        float dist = delta.magnitude;
        Vector3 desired = dist > 0.001f ? delta / dist * Mathf.Min(flySpeed * pb, dist * 2.5f) : Vector3.zero;
        if (hovering) desired = Vector3.zero;   // hold still so it can be clicked
        velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-4f * Time.deltaTime));
        pos += velocity * Time.deltaTime;
        flying = velocity.magnitude > 0.3f * pb;

        float unit = UnitScale;
        float bob = Mathf.Sin(Time.time * 1.7f) * 0.09f * unit;
        root.transform.position = pos + Vector3.up * bob;
        root.transform.localScale = Vector3.one * unit * (hovering ? 1.06f : 1f);
    }

    private void UpdateHoverAndClick()
    {
        Camera cam = Cam;
        bool blocked = cam == null || Time.timeScale <= 0f || PixelPauseMenu.IsPaused || consumables.IsPlacing ||
                       (windowRoot != null && windowRoot.activeSelf) || PixelFirstPerson.Active || PixelClicker.GodMode;
        hovering = false;
        if (!blocked && !PointerOverUI())
        {
            Ray ray = cam.ScreenPointToRay(PointerPosition());
            hovering = box != null && box.Raycast(ray, out _, 1000f);
            if (!hovering && bubbleRect != null && bubbleCanvas != null && bubbleCanvas.activeSelf)
                hovering = RectTransformUtility.RectangleContainsScreenPoint(bubbleRect, PointerPosition(), null);
        }
        // Left clicks pass through it (nothing is blocked); a right-click opens the window.
        if (hovering && RightPressed() && Time.frameCount != ignoreClickFrame) OpenWindow();
        UpdateFade();
    }

    /// <summary>The UFO and its bubble go see-through while hovered: every part swaps to a transparent material and fades to 'hoverAlpha'.</summary>
    private void UpdateFade()
    {
        float target = hovering ? 1f : 0f;
        float before = fadeAmount;
        fadeAmount = Mathf.MoveTowards(fadeAmount, target, Time.unscaledDeltaTime * 6f);
        fadeAlpha = Mathf.Lerp(1f, Mathf.Clamp01(hoverAlpha), fadeAmount);
        if (bubbleImage != null) bubbleImage.color = new Color(1f, 1f, 1f, fadeAlpha);
        if (fadeAmount == before && fadeAmount != 1f) return;   // nothing to update (while fully faded the lights still change every frame, parts don't)
        bool ghost = fadeAmount > 0.001f;
        foreach (Part p in parts)
        {
            if (p.renderer == null) continue;
            if (!p.isLight)
            {
                if (ghost)
                {
                    if (p.ghostMaterial == null) p.ghostMaterial = p.transparentBase ? p.solidMaterial : clicker.CreateVisualMaterial(new Color(p.color.r, p.color.g, p.color.b, 1f), true);
                    if (p.ghostMaterial != null && p.renderer.sharedMaterial != p.ghostMaterial) p.renderer.sharedMaterial = p.ghostMaterial;
                    Color c = p.color; c.a = p.color.a * fadeAlpha;
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", c);
                    block.SetColor("_Color", c);
                    p.renderer.SetPropertyBlock(block);
                }
                else
                {
                    if (p.solidMaterial != null && p.renderer.sharedMaterial != p.solidMaterial) p.renderer.sharedMaterial = p.solidMaterial;
                    p.renderer.SetPropertyBlock(null);
                }
            }
            else if (p.ghostMaterial == null && ghost) p.ghostMaterial = clicker.CreateVisualMaterial(new Color(p.color.r, p.color.g, p.color.b, 1f), true);
            if (p.isLight)
            {
                Material want = ghost ? p.ghostMaterial : p.solidMaterial;
                if (want != null && p.renderer.sharedMaterial != want) p.renderer.sharedMaterial = want;
            }
        }
    }

    private void UpdateJob()
    {
        beamWanted = false;
        if (beamHold > 0f) { beamHold -= Time.deltaTime; beamWanted = true; }
        if (bankJob) { UpdateBankJob(); return; }
        if (!HasJob)
        {
            if (ghost != null) ghost.SetActive(false);
            waitT = 0f;
            return;
        }

        if (Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            occupied = IsOccupied();
        }
        if (ghost != null && ghost.activeSelf == occupied) ghost.SetActive(!occupied);

        PixelConsumables.Device d = consumables.GetDevice(jobDevice);
        bool stock = consumables.DeviceOwned(jobDevice) > 0;
        if (occupied) { waitT = 0f; return; }
        if (!stock)
        {
            waitT = 0f;
            return;
        }

        if (!AtWork()) { waitT = 0f; return; }   // still flying over

        beamWanted = true;
        beamTarget = JobFloorPoint();
        waitT += Time.deltaTime;
        if (waitT >= placeDelay)
        {
            waitT = 0f;
            PixelPlacedDevice placed = consumables.RobotPlace(jobDevice, jobPos, jobYaw, jobBend);
            if (placed != null)
            {
                occupied = true;
                nextCheck = Time.time + checkInterval;
                PixelStats.Count("robot.placed");
                if (ghost != null) ghost.SetActive(false);
                beamHold = 0.9f;
                if (d.kind != PixelConsumables.DeviceKind.Sorter) StartCoroutine(DropDevice(placed.transform, placed.transform.position, UnderSide()));
            }
        }
    }

    /// <summary>The new device slides down the beam from the saucer to its spot.</summary>
    private IEnumerator DropDevice(Transform t, Vector3 final, Vector3 from)
    {
        float e = 0f, dur = Mathf.Max(0.05f, dropSeconds);
        while (e < dur && t != null)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / dur);
            t.position = Vector3.Lerp(from, final, k * k);
            yield return null;
        }
        if (t != null) t.position = final;
    }

    private Vector3 UnderSide() => root.transform.position + Vector3.down * 0.25f * UnitScale;

    // ------------------------------------------------------------------
    // The bank job
    // ------------------------------------------------------------------

    private bool ValidBankTarget(Rigidbody body, int tierIndex)
    {
        if (body == null || !body.gameObject.activeInHierarchy || body.isKinematic) return false;
        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info == null || info.tierIndex != tierIndex) return false;
        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        return despawn == null || !despawn.Held;
    }

    private Rigidbody FindBankTarget(int tierIndex)
    {
        Rigidbody best = null;
        float bestD = float.MaxValue;
        Vector3 from = root.transform.position;
        foreach (Rigidbody body in clicker.OldPixels)
        {
            if (!ValidBankTarget(body, tierIndex)) continue;
            Vector3 d = body.position - from; d.y = 0f;
            float m = d.sqrMagnitude;
            if (m < bestD) { bestD = m; best = body; }
        }
        return best;
    }

    private void UpdateBankJob()
    {
        if (ghost != null) ghost.SetActive(false);
        int idx = clicker.IndexOf(bankType);
        if (bankSys == null) bankSys = PixelFind.First<PixelBank>();
        if (bankSys == null || !bankSys.Active || idx < 0)
        {
            bankTarget = null; abductT = 0f;
            return;
        }
        if (bankSys.IsFull)
        {
            bankTarget = null; abductT = 0f;
            return;
        }

        bankCooldown -= Time.deltaTime;
        if (bankTarget != null && !ValidBankTarget(bankTarget, idx)) { bankTarget = null; abductT = 0f; }
        if (bankTarget == null && Time.time >= bankRetarget)
        {
            bankRetarget = Time.time + 0.4f;
            bankTarget = FindBankTarget(idx);
        }
        if (bankTarget == null || bankCooldown > 0f) { abductT = 0f; return; }

        // Hovering over it: the beam switches on, then the pixel is pulled up into the saucer and banked.
        if (!AtWork()) { abductT = 0f; return; }
        beamWanted = true;
        beamTarget = bankTarget.position;
        abductT += Time.deltaTime;
        if (abductT < abductSeconds) return;
        abductT = 0f;
        if (bankSys.StoreOldPixel(bankTarget, UnderSide())) PixelStats.Count("robot.banked");
        bankTarget = null;
        bankCooldown = 0.35f;
        beamHold = 0.5f;
    }

    /// <summary>Is a device of the job's kind already standing there (or, for the sorter, anywhere)?</summary>
    private bool IsOccupied()
    {
        bool sorter = consumables.GetDevice(jobDevice).kind == PixelConsumables.DeviceKind.Sorter;
        float reach = clicker.PixelBaseSize * 1.5f;
        foreach (PixelPlacedDevice p in PixelPlacedDevice.All)
        {
            if (p == null || p.DeviceIndex != jobDevice || p.IsEnding) continue;
            if (sorter) return true;
            Vector3 a = p.transform.position - jobPos; a.y = 0f;
            if (a.magnitude <= reach) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // The saucer
    // ------------------------------------------------------------------

    private GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color color, bool transparent = false)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Material m = clicker.CreateVisualMaterial(color, transparent);
        Renderer r = g.GetComponent<Renderer>();
        if (m != null) r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        parts.Add(new Part { renderer = r, color = color, solidMaterial = m, transparentBase = transparent, isLight = name.StartsWith("Light") });
        return g;
    }

    private Transform Pivot(Transform parent, string name, Vector3 pos)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        return g.transform;
    }

    /// <summary>A little flying saucer about 1.6 units wide, centred on the origin (the beam leaves from just under it).</summary>
    private void BuildUfo()
    {
        root = new GameObject("UFO Helper");
        bodyT = Pivot(root.transform, "Body", Vector3.zero);
        Transform b = bodyT;

        Prim(PrimitiveType.Sphere, b, "Hull", new Vector3(0f, 0.05f, 0f), new Vector3(1.6f, 0.34f, 1.6f), bodyColor);
        Prim(PrimitiveType.Sphere, b, "Underside", new Vector3(0f, -0.1f, 0f), new Vector3(1.05f, 0.24f, 1.05f), darkColor);
        Prim(PrimitiveType.Cylinder, b, "Emitter", new Vector3(0f, -0.22f, 0f), new Vector3(0.42f, 0.03f, 0.42f), accentColor);
        Prim(PrimitiveType.Sphere, b, "Rim", new Vector3(0f, 0.05f, 0f), new Vector3(1.72f, 0.09f, 1.72f), accentColor);

        domeRenderer = Prim(PrimitiveType.Sphere, b, "Dome", new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.6f, 0.95f, 1f, 0.45f), true).GetComponent<Renderer>();
        alienHead = Pivot(b, "Alien", new Vector3(0f, 0.3f, 0f));
        Prim(PrimitiveType.Sphere, alienHead, "Head", Vector3.zero, new Vector3(0.36f, 0.32f, 0.34f), alienColor);
        Prim(PrimitiveType.Sphere, alienHead, "Eye L", new Vector3(-0.09f, 0.02f, -0.12f), new Vector3(0.11f, 0.15f, 0.06f), Color.black);
        Prim(PrimitiveType.Sphere, alienHead, "Eye R", new Vector3(0.09f, 0.02f, -0.12f), new Vector3(0.11f, 0.15f, 0.06f), Color.black);

        lightsPivot = Pivot(b, "Lights", new Vector3(0f, 0.05f, 0f));
        const int lights = 8;
        lightRenderers = new Renderer[lights];
        for (int i = 0; i < lights; i++)
        {
            float a = i / (float)lights * Mathf.PI * 2f;
            lightRenderers[i] = Prim(PrimitiveType.Sphere, lightsPivot, "Light " + i, new Vector3(Mathf.Cos(a) * 0.78f, 0f, Mathf.Sin(a) * 0.78f), Vector3.one * 0.13f, eyeColor).GetComponent<Renderer>();
        }

        box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.2f, 0f);
        box.size = new Vector3(1.9f, 1.0f, 1.9f);
        box.isTrigger = true;

        BuildBeam();
        BuildBubble();
        if (clicker.PixelTransform != null) floorY = clicker.PixelTransform.position.y - clicker.PixelBaseSize * 0.5f;
        nextFloorCheck = 0f;
    }

    /// <summary>The abduction beam: a see-through cone (narrow at the saucer, wide at the target), unlit vertex colours, drawn along +Z.</summary>
    private void BuildBeam()
    {
        beam = new GameObject("UFO Beam");
        MeshFilter mf = beam.AddComponent<MeshFilter>();
        MeshRenderer mr = beam.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        Material m = PixelLooks.OverlayMaterial();
        if (m != null) mr.sharedMaterial = m;

        int n = BeamSegments;
        Vector3[] v = new Vector3[n * 2];
        beamColors = new Color[n * 2];
        int[] tri = new int[n * 12];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            v[i] = new Vector3(d.x * 0.3f, d.y * 0.3f, 0f);
            v[n + i] = new Vector3(d.x, d.y, 1f);
            int j = (i + 1) % n;
            int t = i * 12;
            tri[t] = i; tri[t + 1] = n + i; tri[t + 2] = n + j;
            tri[t + 3] = i; tri[t + 4] = n + j; tri[t + 5] = j;
            tri[t + 6] = i; tri[t + 7] = n + j; tri[t + 8] = n + i;   // and the back faces, so it shows from inside too
            tri[t + 9] = i; tri[t + 10] = j; tri[t + 11] = n + j;
        }
        beamMesh = new Mesh { name = "UFO Beam" };
        beamMesh.vertices = v;
        beamMesh.triangles = tri;
        beamMesh.colors = beamColors;
        beamMesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2.4f, 2.4f, 2f));
        mf.sharedMesh = beamMesh;
        beam.SetActive(false);
    }

    private void UpdateBeam()
    {
        if (beam == null) return;
        beamAmount = Mathf.MoveTowards(beamAmount, beamWanted ? 1f : 0f, Time.deltaTime * 4f);
        bool on = beamAmount > 0.01f;
        if (beam.activeSelf != on) beam.SetActive(on);
        if (!on) return;

        Vector3 from = UnderSide();
        Vector3 dir = beamTarget - from;
        float len = dir.magnitude;
        if (len < 0.05f) { beam.SetActive(false); return; }
        float unit = UnitScale;
        beam.transform.position = from;
        beam.transform.rotation = Quaternion.LookRotation(dir / len, Mathf.Abs(Vector3.Dot(dir / len, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up);
        beam.transform.localScale = new Vector3(unit * beamRadius, unit * beamRadius, len);

        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f);
        int n = BeamSegments;
        for (int i = 0; i < n; i++)
        {
            Color top = beamColor; top.a = 0.7f * beamAmount * flicker;
            Color bottom = beamColor; bottom.a = 0.16f * beamAmount * flicker;
            beamColors[i] = top;
            beamColors[n + i] = bottom;
        }
        beamMesh.colors = beamColors;
    }

    /// <summary>The speech bubble with an alien face that floats over the UFO (a button: click it like the UFO).</summary>
    private void BuildBubble()
    {
        if (hideBubble) return;
        bubbleCanvas = PixelUIKit.CreateCanvas("UFO Bubble", 280, new Vector2(1920f, 1080f), true);
        bubbleCanvasComponent = bubbleCanvas.GetComponent<Canvas>();
        if (bubbleSprite == null) bubbleSprite = BuildBubbleSprite();
        GameObject go = new GameObject("Bubble", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(bubbleCanvas.transform, false);
        Image img = go.GetComponent<Image>();
        img.sprite = bubbleSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;   // click-through, like the UFO: right-click it (UpdateHoverAndClick) to open the window
        bubbleImage = img;
        bubbleRect = go.GetComponent<RectTransform>();
        bubbleRect.anchorMin = bubbleRect.anchorMax = Vector2.zero;
        bubbleRect.pivot = new Vector2(0.5f, 0f);
        bubbleRect.sizeDelta = new Vector2(bubbleSize * 48f / 56f, bubbleSize);
    }

    /// <summary>A round speech bubble with a pointer at the bottom and a pixel-art alien head in it (drawn in code).</summary>
    private Sprite BuildBubbleSprite()
    {
        const int w = 48, h = 56;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color32 clear = new Color32(0, 0, 0, 0), border = new Color32(25, 28, 40, 255), fill = new Color32(225, 238, 250, 255);
        Color32 skin = alienColor, dark = darkColor, white = new Color32(255, 255, 255, 255);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, clear);
        float cx = 23.5f, cy = 33f, r = 22.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r) tex.SetPixel(x, y, d > r - 2.2f ? border : fill);
            }
        // The pointer: a small triangle under the circle, tipped at the bottom row.
        for (int row = 1; row <= 12; row++)
        {
            int half = Mathf.Clamp((row - 1) / 2, 0, 5);
            for (int x = 24 - half - 1; x <= 24 + half; x++)
            {
                bool edge = x == 24 - half - 1 || x == 24 + half || row == 1;
                tex.SetPixel(x, row, edge ? border : fill);
            }
        }
        void Fill(int x0, int y0, int x1, int y1, Color32 c) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) tex.SetPixel(x, y, c); }
        // The alien: a wide head that narrows to the chin, big slanted black eyes with a glint, a tiny mouth.
        Fill(13, 36, 34, 44, skin);
        Fill(15, 29, 32, 35, skin);
        Fill(18, 24, 29, 28, skin);
        Fill(15, 33, 21, 40, dark); Fill(26, 33, 32, 40, dark);
        Fill(17, 37, 18, 38, white); Fill(28, 37, 29, 38, white);
        Fill(22, 26, 25, 26, dark);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    private void UpdateBubble()
    {
        if (bubbleCanvas == null) return;
        Camera cam = Cam;
        bool windowOpen = windowRoot != null && windowRoot.activeSelf;
        if (cam == null || windowOpen) { if (bubbleCanvas.activeSelf) bubbleCanvas.SetActive(false); return; }
        if (!bubbleCanvas.activeSelf) bubbleCanvas.SetActive(true);

        Vector3 world = root.transform.position + Vector3.up * (0.8f * UnitScale);
        Vector3 sp = cam.WorldToScreenPoint(world);
        float scaleFactor = bubbleCanvasComponent.scaleFactor > 0f ? bubbleCanvasComponent.scaleFactor : 1f;
        Vector2 canvasSize = new Vector2(Screen.width, Screen.height) / scaleFactor;
        float bar = PixelHud.Instance != null ? PixelHud.Instance.BarHeight : 0f;
        Vector2 p = new Vector2(sp.x, sp.y) / scaleFactor;
        if (sp.z < 0f) p = new Vector2(canvasSize.x * 0.5f, bar);   // behind the camera: park it
        p.x = Mathf.Clamp(p.x, bubbleRect.sizeDelta.x * 0.5f, canvasSize.x - bubbleRect.sizeDelta.x * 0.5f);
        p.y = Mathf.Clamp(p.y, bar, canvasSize.y - bar - bubbleRect.sizeDelta.y);
        p.y += Mathf.Sin(Time.time * 3f) * 5f;
        bubbleRect.anchoredPosition = p;
        bubbleRect.localScale = Vector3.one * (hovering ? 1.1f : 1f);
    }

    private void Animate()
    {
        if (root == null || bodyT == null) return;
        float t = Time.time;
        bool working = beamAmount > 0.05f;

        // Tilts into its flight and wobbles a little.
        Vector3 flat = velocity; flat.y = 0f;
        float lean = Mathf.Clamp01(flat.magnitude / Mathf.Max(0.01f, flySpeed * clicker.PixelBaseSize)) * 16f;
        Quaternion want = flat.sqrMagnitude > 0.0001f ? Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, flat.normalized)) : Quaternion.identity;
        want *= Quaternion.Euler(Mathf.Sin(t * 1.3f) * 2.5f, 0f, Mathf.Sin(t * 1.1f + 1f) * 2.5f);
        tilt = Quaternion.Slerp(tilt, want, 1f - Mathf.Exp(-6f * Time.deltaTime));
        bodyT.localRotation = tilt;

        lightsPivot.Rotate(0f, (working ? 360f : flying ? 200f : 90f) * Time.deltaTime, 0f, Space.Self);
        for (int i = 0; i < lightRenderers.Length; i++)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (working ? 12f : 4f) + i * 0.8f);
            SetRendererColor(lightRenderers[i], Color.Lerp(eyeColor * 0.35f, working ? Color.white : eyeColor, pulse), fadeAlpha);
        }
        if (alienHead != null) alienHead.localRotation = Quaternion.Euler(0f, hovering ? Mathf.Sin(t * 6f) * 35f : Mathf.Sin(t * 0.8f) * 20f, Mathf.Sin(t * 1.6f) * 5f);
    }

    private static void SetRendererColor(Renderer r, Color c, float alpha)
    {
        if (r == null) return;
        c.a = alpha;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }

    // ------------------------------------------------------------------
    // The job window
    // ------------------------------------------------------------------

    private void OpenWindow()
    {
        PixelWindows.CloseAllExcept(this);
        windowPage = 0;
        BuildWindow();
        windowRoot.SetActive(true);
        PixelAudio.Play("ui_click");
    }

    private void CloseWindow()
    {
        if (windowRoot != null && windowRoot.activeSelf)
        {
            windowRoot.SetActive(false);
            ignoreClickFrame = Time.frameCount;
        }
    }

    private int windowPage;   // 0 = devices to keep running, 1 = pixel types to pick up and bank

    private Button AddRow(RectTransform content, float y, float rowH, string name, string countText, Color countColor, bool current, System.Action onClick)
    {
        TMP_FontAsset font = clicker.UIFont;
        Button b = PixelUIKit.CreateButton(font, content, "Row " + name, "", new Vector2(0f, rowH),
                                           current ? new Color(0.75f, 0.42f, 0.1f, 1f) : new Color(0.2f, 0.22f, 0.3f, 1f), Color.white, 30f);
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(8f, -(y + rowH)); rt.offsetMax = new Vector2(-8f, -y);
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;

        TMP_Text left = PixelUIKit.CreateText(font, b.transform, "Name", name, 32f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        left.rectTransform.anchorMin = Vector2.zero; left.rectTransform.anchorMax = new Vector2(0.68f, 1f);
        left.rectTransform.offsetMin = new Vector2(22f, 0f); left.rectTransform.offsetMax = Vector2.zero;
        left.enableAutoSizing = true; left.fontSizeMax = 32f; left.fontSizeMin = 18f;
        TMP_Text right = PixelUIKit.CreateText(font, b.transform, "Count", countText, 28f, TextAlignmentOptions.MidlineRight, FontStyles.Bold, countColor);
        right.rectTransform.anchorMin = new Vector2(0.55f, 0f); right.rectTransform.anchorMax = Vector2.one;
        right.rectTransform.offsetMin = Vector2.zero; right.rectTransform.offsetMax = new Vector2(-22f, 0f);
        right.enableAutoSizing = true; right.fontSizeMax = 28f; right.fontSizeMin = 16f;
        b.onClick.AddListener(() => onClick());
        return b;
    }

    private void BuildWindow()
    {
        if (windowRoot != null) Destroy(windowRoot);
        TMP_FontAsset font = clicker.UIFont;
        windowRoot = PixelUIKit.CreateCanvas("UFO Helper Window", 520, new Vector2(1920f, 1080f), true);
        PixelUIKit.EnsureEventSystem();

        const float width = 780f, height = 780f, pad = 24f, rowH = 78f, gap = 10f, headerH = 170f, footerH = 96f;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(windowRoot.transform, false);
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), new Color(0.09f, 0.1f, 0.14f, 0.97f));
        windowPanel = panel.GetComponent<RectTransform>();
        windowPanel.anchorMin = windowPanel.anchorMax = windowPanel.pivot = new Vector2(0.5f, 0.5f);
        windowPanel.sizeDelta = new Vector2(width, height);

        bool pixels = windowPage == 1;
        TMP_Text title = PixelUIKit.CreateText(font, panel.transform, "Title", ufoTitle, 44f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-150f, 70f); tr.anchoredPosition = new Vector2(0f, -pad * 0.5f);

        Button close = PixelUIKit.CreateButton(font, panel.transform, "Close", "X", new Vector2(56f, 56f), new Color(0.55f, 0.2f, 0.2f, 1f), Color.white, 30f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -14f);
        close.onClick.AddListener(CloseWindow);

        TMP_Text ask = PixelUIKit.CreateText(font, panel.transform, "Ask", pixels ? pickPixelText : askText, 28f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(1f, 1f, 1f, 0.85f));
        ask.enableAutoSizing = true; ask.fontSizeMax = 28f; ask.fontSizeMin = 18f;
        RectTransform ar = ask.rectTransform;
        ar.anchorMin = new Vector2(0f, 1f); ar.anchorMax = new Vector2(1f, 1f); ar.pivot = new Vector2(0.5f, 1f);
        ar.sizeDelta = new Vector2(-pad * 2f, 84f); ar.anchoredPosition = new Vector2(0f, -(pad * 0.5f + 70f));

        // The list: a scrolling view between the header and the footer buttons.
        ScrollRect scroll = PixelUIKit.CreateScrollView(panel.transform, "List", new Color(1f, 1f, 1f, 0.35f), 12f, rowH, out RectTransform content, out GameObject bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = new Vector2(0f, 1f); vr.anchorMax = new Vector2(1f, 1f); vr.pivot = new Vector2(0.5f, 1f);
        float listH = height - headerH - footerH;
        vr.offsetMin = new Vector2(pad, -(headerH + listH)); vr.offsetMax = new Vector2(-pad, -headerH);

        float y = 0f;
        Color dim = new Color(1f, 1f, 1f, 0.7f), bad = new Color(1f, 0.45f, 0.4f, 1f);
        if (!pixels)
        {
            for (int i = 0; i < consumables.DeviceCount; i++)
            {
                PixelConsumables.Device d = consumables.GetDevice(i);
                if (!PixelConsumables.IsPlaceableKind(d.kind) || !consumables.OperationListed(d.kind)) continue;
                int index = i, owned = consumables.DeviceOwned(i);
                AddRow(content, y, rowH, d.displayName, owned >= 99999 ? "Unlimited" : "x" + owned, owned > 0 ? dim : bad, !bankJob && i == jobDevice, () => ChooseDevice(index));
                y += rowH + gap;
            }
            if (bankSys != null && bankSys.Active)
            {
                AddRow(content, y, rowH, bankPixelsText, bankJob ? clicker.Tiers[Mathf.Max(0, clicker.IndexOf(bankType))].displayName : "", dim, bankJob, () => { windowPage = 1; BuildWindow(); windowRoot.SetActive(true); });
                y += rowH + gap;
            }
            if (y <= 0f)
            {
                TMP_Text none = PixelUIKit.CreateText(font, content, "None", noDevicesText, 28f, TextAlignmentOptions.Center, FontStyles.Normal, dim);
                none.rectTransform.anchorMin = new Vector2(0f, 1f); none.rectTransform.anchorMax = new Vector2(1f, 1f); none.rectTransform.pivot = new Vector2(0.5f, 1f);
                none.rectTransform.sizeDelta = new Vector2(0f, 80f);
                y = 90f;
            }
        }
        else
        {
            for (int i = 0; i < clicker.Tiers.Length; i++)
            {
                PixelClicker.PixelTier t = clicker.Tiers[i];
                if (!t.unlocked || t.flyAway || t.rareDrop) continue;
                PixelClicker.PixelType type = t.type;
                AddRow(content, y, rowH, t.displayName, "Banked: " + PixelClicker.FormatNumberShort(bankSys != null ? bankSys.Stored(i) : 0L), dim, bankJob && bankType == type, () => ChoosePixel(type));
                y += rowH + gap;
            }
        }
        PixelUIKit.UpdateScrollView(scroll, bar, y, listH);

        // Footer: back (pixel page) and stop working.
        float half = (width - pad * 2f - gap) * 0.5f;
        bool anyJob = bankJob || jobDevice >= 0;
        if (pixels)
        {
            Button back = PixelUIKit.CreateButton(font, panel.transform, "Back", "Back", new Vector2(anyJob ? half : width - pad * 2f, rowH),
                                                  new Color(0.25f, 0.3f, 0.45f, 1f), Color.white, 30f);
            PlaceBottomLeft(back.GetComponent<RectTransform>(), pad, 14f);
            back.onClick.AddListener(() => { windowPage = 0; BuildWindow(); windowRoot.SetActive(true); });
        }
        if (anyJob)
        {
            Button clear = PixelUIKit.CreateButton(font, panel.transform, "Clear", clearJobText, new Vector2(pixels ? half : width - pad * 2f, rowH),
                                                   new Color(0.45f, 0.18f, 0.18f, 1f), Color.white, 30f);
            PlaceBottomLeft(clear.GetComponent<RectTransform>(), pixels ? pad + half + gap : pad, 14f);
            clear.onClick.AddListener(() => { ClearJob(); CloseWindow(); });
        }
    }

    private static void PlaceBottomLeft(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private void ChoosePixel(PixelClicker.PixelType type)
    {
        CloseWindow();
        SetBankJob(type);
        PixelHints.Announce(string.Format(ufoBankJobFormat, clicker.Tiers[Mathf.Max(0, clicker.IndexOf(type))].displayName));
    }

    private void ChooseDevice(int index)
    {
        CloseWindow();
        if (consumables.BeginRobotPlacement(index, OnSpotChosen))
            PixelHints.Announce("Choose where the UFO should drop it: click the floor");
    }

    private void OnSpotChosen(int device, Vector3 point, float yaw, float bend)
    {
        ignoreClickFrame = Time.frameCount;
        SetJob(device, point, yaw, bend);
        PixelHints.Announce(string.Format(ufoJobSetFormat, consumables.GetDevice(device).displayName));
    }

    // ------------------------------------------------------------------
    // Job + saving
    // ------------------------------------------------------------------

    private void SetJob(int device, Vector3 point, float yaw, float bend)
    {
        bankJob = false;
        bankTarget = null;
        jobDevice = device; jobPos = point; jobYaw = yaw; jobBend = bend;
        waitT = 0f;
        occupied = false;
        nextCheck = 0f;
        if (ghost != null) Destroy(ghost);
        ghost = device >= 0 ? consumables.CreateGhost(device, point, yaw, bend) : null;
        if (ghost != null) ghost.SetActive(false);   // shown by UpdateJob while the spot is empty
    }

    private void ClearJob() { SetJob(-1, Vector3.zero, 0f, 0f); }

    /// <summary>The job as text for the save file ("" = none).</summary>
    public string Export()
    {
        if (bankJob) return "bank|" + bankType;
        if (jobDevice < 0 || consumables == null) return "";
        CultureInfo c = CultureInfo.InvariantCulture;
        return consumables.GetDevice(jobDevice).displayName + "|" + jobPos.x.ToString("R", c) + "|" + jobPos.y.ToString("R", c) + "|" +
               jobPos.z.ToString("R", c) + "|" + jobYaw.ToString("R", c) + "|" + jobBend.ToString("R", c);
    }

    /// <summary>Restores a saved job (an empty or unknown text clears it).</summary>
    public void Import(string text)
    {
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        ClearJobQuiet();
        if (string.IsNullOrEmpty(text) || consumables == null) return;
        string[] p = text.Split('|');
        if (p.Length == 2 && p[0] == "bank")
        {
            if (System.Enum.TryParse(p[1], out PixelClicker.PixelType saved)) SetBankJob(saved);
            return;
        }
        if (p.Length < 6) return;
        CultureInfo c = CultureInfo.InvariantCulture;
        int index = -1;
        for (int i = 0; i < consumables.DeviceCount; i++) if (consumables.GetDevice(i).displayName == p[0]) { index = i; break; }
        if (index < 0 || !PixelConsumables.IsPlaceableKind(consumables.GetDevice(index).kind)) return;
        if (!float.TryParse(p[1], NumberStyles.Float, c, out float x) || !float.TryParse(p[2], NumberStyles.Float, c, out float y) ||
            !float.TryParse(p[3], NumberStyles.Float, c, out float z) || !float.TryParse(p[4], NumberStyles.Float, c, out float yaw) ||
            !float.TryParse(p[5], NumberStyles.Float, c, out float bend)) return;
        SetJob(index, new Vector3(x, y, z), yaw, bend);
    }

    private void SetBankJob(PixelClicker.PixelType type)
    {
        SetJob(-1, Vector3.zero, 0f, 0f);
        bankJob = true;
        bankType = type;
        bankRetarget = 0f;
    }

    private void ClearJobQuiet()
    {
        bankJob = false;
        bankTarget = null;
        jobDevice = -1;
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }
}
