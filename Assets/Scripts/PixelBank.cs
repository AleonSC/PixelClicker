using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static PixelInput;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The Pixel Bank (bought as an upgrade in the shop). A tab on the left edge of the screen (opposite the Toggles tab)
/// opens a window with everything you have stored; a second button under it, or the B key, equips a flexible hose that
/// comes in from the left side of the screen and follows the mouse.
///
/// With the hose equipped:
///   - the mouse wheel changes which stored pixel type is selected (shown as a small spinning cube by the nozzle),
///   - left-click spits one pixel of the selected type out of the nozzle,
///   - right-click sucks up the old pixel under the nozzle (any type) and stores it, if there is room.
/// Clicks on the cube are ignored while the hose is equipped.
///
/// Add this to any GameObject. PixelShop adds it automatically if it is missing.
/// </summary>
public class PixelBank : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        HoseOn = false;
    }

    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The PixelClicker whose old pixels are stored. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("State")]
    [Tooltip("Is the bank available? (The shop turns this on when the Pixel Bank upgrade is bought. Tick it to test.)")]
    [SerializeField] private bool bankActive = false;

    [Min(1)]
    [Tooltip("How many pixels the bank can hold in total.")]
    [SerializeField] private int capacity = 60;

    [Header("Hose")]
    [Min(0f)]
    [Tooltip("How high above the spot it points at the nozzle hangs, in widths of the main pixel. 0 = the coded default (2.5). It is lowered automatically when that would push the nozzle off the top of the screen.")]
    [SerializeField] private float nozzleHeightCubes = 0f;

    [Min(0.01f)]
    [Tooltip("Thickness of the hose (world units).")]
    [SerializeField] private float hoseRadius = 0.09f;

    [Tooltip("Colour of the hose.")]
    [SerializeField] private Color hoseColor = new Color(0.25f, 0.27f, 0.32f, 1f);

    [Min(0.5f)]
    [Tooltip("How quickly the hose swings after the nozzle (low = lazy, swingy hose; high = stiff).")]
    [SerializeField] private float followSharpness = 6f;

    [Range(6, 60)]
    [Tooltip("How many pieces the hose is drawn from (more = smoother bends).")]
    [SerializeField] private int hoseSegments = 28;

    [Min(0f)]
    [Tooltip("How far (world units) the hose carries on up beyond the top of the screen (it hangs from the ceiling), so its shadow doesn't suddenly appear in view. 0 = the coded default (14).")]
    [SerializeField] private float hoseOffscreenExtra = 0f;

    [Min(0f)]
    [Tooltip("The hose starts to fade out this many world units above the top of the nozzle, so it never gets cut off by the camera. 0 = the coded default (3).")]
    [SerializeField] private float hoseFadeStart = 0f;

    [Min(0f)]
    [Tooltip("Over how many world units the hose fades to nothing. 0 = the coded default (4).")]
    [SerializeField] private float hoseFadeLength = 0f;

    [Range(3, 16)]
    [Tooltip("How many sides the round hose has.")]
    [SerializeField] private int hoseSides = 10;

    [Min(0.05f)]
    [Tooltip("Length of the nozzle at the end of the hose (world units).")]
    [SerializeField] private float nozzleLength = 0.4f;

    [Min(0f)]
    [Tooltip("Scales the whole nozzle assembly (nozzle, glass cube and count plate) together. 0 = the coded default (1.6).")]
    [SerializeField] private float nozzleScale = 0f;

    [Min(0.02f)]
    [Tooltip("Radius of the nozzle (world units).")]
    [SerializeField] private float nozzleRadius = 0.17f;

    [Tooltip("Colour of the nozzle.")]
    [SerializeField] private Color nozzleColor = new Color(0.8f, 0.8f, 0.85f, 1f);

    [Header("Laser")]
    [Tooltip("Tick to hide the red laser beam and dot that show where the nozzle is pointing.")]
    [SerializeField] private bool hideAimLine = false;

    [Range(0f, 1f)]
    [Tooltip("How see-through the laser beam is. 0 = the coded default (0.55).")]
    [SerializeField] private float aimLineAlpha = 0f;

    [Min(0f)]
    [Tooltip("Width of the laser beam (world units). 0 = the coded default (0.025).")]
    [SerializeField] private float aimLineWidth = 0f;

    [Header("Sucking and Spitting")]
    [Min(0.05f)]
    [Tooltip("The laser beam is this wide (half of it, as a radius) when it looks for an old pixel to suck up, so a pixel just beside the red dot still counts.")]
    [SerializeField] private float suckRadius = 0.7f;

    [Min(0.05f)]
    [Tooltip("Seconds a sucked-up pixel takes to fly into the nozzle.")]
    [SerializeField] private float suckSeconds = 0.3f;

    [Min(0f)]
    [Tooltip("How fast a spat-out pixel is dropped down the beam (world units per second).")]
    [SerializeField] private float spitSpeed = 7f;

    [Header("Area Suction (hold the right mouse button)")]
    [Min(0.05f)]
    [Tooltip("How long (seconds) you hold the right button before the hose switches from sucking up one pixel to sucking up an area.")]
    [SerializeField] private float areaHoldSeconds = 0.4f;

    [Min(0.1f)]
    [Tooltip("Radius of the area (world units, measured across the screen around the nozzle). Only old pixels of the selected type inside it are pulled in.")]
    [SerializeField] private float areaRadius = 2f;

    [Min(0.5f)]
    [Tooltip("How many pixels the area suction pulls in per second.")]
    [SerializeField] private float areaRate = 8f;

    [Range(0f, 1f)]
    [Tooltip("How see-through the ring that shows the area is.")]
    [SerializeField] private float areaRingOpacity = 0.75f;

    [Range(0.02f, 0.4f)]
    [Tooltip("Thickness of the area ring, as a fraction of its radius.")]
    [SerializeField] private float areaRingThickness = 0.08f;

    [Tooltip("Text near the nozzle when you hold for area suction but there is no pixel type to match (nothing selected and no old pixel under the nozzle).")]
    [SerializeField] private string areaNoTypeText = "No pixel type to match";

    [Header("Selected Pixel Display")]
    [Min(0.05f)]
    [Tooltip("Size of the little spinning cube that shows the selected pixel (world units).")]
    [SerializeField] private float displaySize = 0.32f;

    [Tooltip("Where the display floats relative to the nozzle, in screen directions (x = right, y = up) in world units.")]
    [SerializeField] private Vector2 displayOffset = new Vector2(0.55f, 0.6f);

    [Tooltip("How fast the display cube spins (degrees per second).")]
    [SerializeField] private float displaySpin = 90f;

    [Min(1f)]
    [Tooltip("Size of the count text next to the display.")]
    [SerializeField] private float displayTextSize = 3f;

    [Tooltip("Text near the nozzle when nothing is stored.")]
    [SerializeField] private string emptyDisplayText = "Bank empty";

    [Tooltip("Text near the nozzle when the bank is full and you try to suck up another pixel.")]
    [SerializeField] private string fullText = "Bank full";

    [Header("Buttons (docked to the left edge)")]
    [Tooltip("Text on the Bank tab.")]
    [SerializeField] private string bankButtonText = "Bank";

    [Tooltip("Text on the hose button while the hose is put away. {0} = the key.")]
    [SerializeField] private string hoseOffText = "Hose ({0})";

    [Tooltip("Text on the hose button while the hose is equipped.")]
    [SerializeField] private string hoseOnText = "Hose: ON";

    [Tooltip("Colour of the Bank tab.")]
    [SerializeField] private Color bankButtonColor = new Color(0.25f, 0.55f, 0.6f, 1f);

    [Tooltip("Colour of the hose button while the hose is put away.")]
    [SerializeField] private Color hoseButtonColor = new Color(0.3f, 0.3f, 0.36f, 1f);

    [Tooltip("Colour of the hose button while the hose is equipped.")]
    [SerializeField] private Color hoseButtonOnColor = new Color(0.3f, 0.7f, 0.4f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color buttonTextColor = Color.white;

    [Tooltip("Gap between the two buttons.")]
    [SerializeField] private float buttonGap = 12f;

    [Range(0.2f, 1f)]
    [Tooltip("Width of the Hose button compared with the Bank tab (0.5 = half as wide, centred under it so it stays hidden while the tab is tucked away).")]
    [SerializeField] private float hoseButtonWidthFraction = 0.72f;

    [Header("Window")]
    [Tooltip("Window title.")]
    [SerializeField] private string windowTitle = "Pixel Bank";

    [Tooltip("Capacity line. {0} = stored, {1} = capacity.")]
    [SerializeField] private string capacityFormat = "Stored: {0} / {1}";

    [Tooltip("Hint at the bottom of the window.")]
    [SerializeField] private string hintText = "Hose: scroll = choose, left-click = spit, right-click = suck";

    [Tooltip("Window size (canvas units).")]
    [SerializeField] private Vector2 windowSize = new Vector2(560f, 760f);

    [Tooltip("Gap between the buttons and the window.")]
    [SerializeField] private float gapToButton = 14f;

    [Tooltip("Height of one row in the window.")]
    [SerializeField] private float rowHeight = 60f;

    [Tooltip("Text size.")]
    [SerializeField] private float fontSize = 30f;

    [Tooltip("Title text size.")]
    [SerializeField] private float titleFontSize = 44f;

    [Tooltip("Window background colour.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);

    [Tooltip("Row background colour.")]
    [SerializeField] private Color rowColor = new Color(0.17f, 0.17f, 0.21f, 1f);

    [Tooltip("Row colour of the selected pixel (while the hose is equipped).")]
    [SerializeField] private Color rowSelectedColor = new Color(0.25f, 0.5f, 0.55f, 1f);

    [Tooltip("Text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Scroll bar colour.")]
    [SerializeField] private Color scrollbarColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Sorting order of the buttons' canvas (the same layer as the Toggles).")]
    [SerializeField] private int sortingOrder = 130;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    /// <summary>One stored pixel type, as the save system keeps it.</summary>
    [Serializable]
    public class Entry
    {
        public int type;     // PixelClicker.PixelType
        public long count;
        public double value; // total payout of the stored pixels (so a spat-out pixel keeps its worth)
    }

    /// <summary>True while the hose is equipped. Other scripts (Pixel Grabbing, device removal) step aside then.</summary>
    public static bool HoseOn { get; private set; }

    private long[] counts = new long[0];
    private double[] values = new double[0];
    private int selected = -1;

    // Hose
    private GameObject hoseRoot;
    private MeshFilter hoseFilter;
    private Transform nozzle;          // the nozzle assembly: its origin is the mouth, +Y points the way pixels leave, +X is the side the glass cube sits on
    private Transform glassCube;       // the glass cube on the nozzle
    private GameObject glassPixel;     // the selected pixel's model inside the glass
    private int glassTier = -1;
    private Transform counterPlate;    // the small plate on the nozzle with the stored count
    private TextMeshPro counterText;
    private TextMeshPro displayText;   // floating message only ("Bank full" / "Bank empty")
    private LineRenderer aimLine;
    private Transform aimMarker;
    private Material aimMaterial;
    private readonly System.Collections.Generic.List<Vector3> aimPoints = new System.Collections.Generic.List<Vector3>();
    private static readonly RaycastHit[] aimHits = new RaycastHit[8];
    private Mesh hoseMesh;
    private Vector3[] hosePoints;
    private Vector3 control;
    private bool controlReady;
    private float kick;           // brief nozzle punch when spitting / sucking
    private float fullTimer;
    private string flashMessage = "";

    // Nozzle aim
    private Vector3 beamPoint;                 // where the laser beam ends (the red dot)
    private Rigidbody beamTarget;               // the old pixel the beam is on (what a click sucks up), or null
    private Vector3 swayTop;                    // where the hose hangs from, high above the nozzle (lags behind it so the hose swings)
    private bool swayReady;
    private Material dotMaterial;
    private static readonly RaycastHit[] beamHits = new RaycastHit[12];

    // Area suction
    private bool rightDown, areaActive, areaFinished;
    private float rightHeldTime, areaTimer;
    private int areaTier = -1;
    private Vector3 tipPosition;
    private Transform areaRing;
    private Renderer areaRingRenderer;
    private Material areaRingMaterial;
    private int areaRingTier = -2;
    private int displayTier = -1;
    private PixelConsumables consumables;

    // UI
    private GameObject canvasRoot, windowObject;
    private Button hoseButton;
    private Image hoseButtonImage;
    private TMP_Text hoseButtonLabel, capacityLabel;
    private PixelDockedButton bankDock, hoseDock;
    private ScrollRect scroll;
    private RectTransform content;
    private GameObject bar;
    private readonly List<RowUI> rows = new List<RowUI>();
    private float refreshTimer;
    private bool built;

    private class RowUI
    {
        public GameObject go;
        public Image background;
        public Image swatch;
        public TMP_Text label, count;
        public int tier;
    }

    /// <summary>Is the bank unlocked (bought)?</summary>
    public bool Active => bankActive;

    /// <summary>Total number of stored pixels.</summary>
    public long Total
    {
        get
        {
            long total = 0;
            for (int i = 0; i < counts.Length; i++) total += counts[i];
            return total;
        }
    }

    /// <summary>How many pixels the bank can hold (the Bank Storage upgrade raises it).</summary>
    public int Capacity => capacityOverride > 0 ? capacityOverride : capacity;

    private int capacityOverride;

    /// <summary>Sets the capacity from the Bank Storage upgrade (0 = back to the base capacity). The shop calls this.</summary>
    public void SetCapacityOverride(int value) => capacityOverride = Mathf.Max(0, value);

    /// <summary>Stored pixels of a tier.</summary>
    public long Stored(int tierIndex) => tierIndex >= 0 && tierIndex < counts.Length ? counts[tierIndex] : 0;

    /// <summary>Called by the shop when the upgrade is bought.</summary>
    public void Activate() => bankActive = true;

    /// <summary>Called by the shop (e.g. when a save without the upgrade is loaded). Stored pixels are kept.</summary>
    public void Deactivate()
    {
        bankActive = false;
        SetHose(false);
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>The stored pixels, for the save system.</summary>
    public List<Entry> GetState()
    {
        List<Entry> list = new List<Entry>();
        EnsureArrays();
        for (int i = 0; i < counts.Length; i++)
            if (counts[i] > 0) list.Add(new Entry { type = (int)clicker.Tiers[i].type, count = counts[i], value = values[i] });
        return list;
    }

    /// <summary>Replaces what is stored (used when loading a save).</summary>
    public void SetState(Entry[] state)
    {
        EnsureArrays();
        Array.Clear(counts, 0, counts.Length);
        Array.Clear(values, 0, values.Length);
        if (state != null)
        {
            foreach (Entry e in state)
            {
                int index = clicker.IndexOf((PixelClicker.PixelType)e.type);
                if (index < 0 || e.count <= 0) continue;
                counts[index] = e.count;
                values[index] = Math.Max(0d, e.value);
            }
        }
        if (selected < 0 || selected >= counts.Length || counts[selected] <= 0) selected = NextStocked(-1, 1);
        refreshTimer = 0f;
    }

    private void EnsureArrays()
    {
        int n = clicker != null ? clicker.Tiers.Length : 0;
        if (counts.Length == n) return;
        Array.Resize(ref counts, n);
        Array.Resize(ref values, n);
    }

    // ------------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker == null)
        {
            Debug.LogError("PixelBank: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }
        if (clicker.UIFont != null) font = clicker.UIFont;
        EnsureArrays();
    }

    private void Start()
    {
        PixelUIKit.EnsureEventSystem();
        BuildUI();
        BuildHose();
        built = true;
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        HoseOn = false;
        PixelClicker.ExternalClickBlock = false;
        if (canvasRoot != null) Destroy(canvasRoot);
        if (hoseRoot != null) Destroy(hoseRoot);
        if (hoseMesh != null) Destroy(hoseMesh);
        if (aimMaterial != null) Destroy(aimMaterial);
        if (dotMaterial != null) Destroy(dotMaterial);
        if (hoseMaterial != null) Destroy(hoseMaterial);
    }

    private void Update()
    {
        if (!built) return;
        EnsureArrays();

        if (canvasRoot.activeSelf != bankActive) canvasRoot.SetActive(bankActive);
        if (!bankActive)
        {
            if (HoseOn) SetHose(false);
            return;
        }

        // Equip / put away the hose.
        if (!PixelPauseMenu.IsPaused && !IsTyping() && HoseKeyPressed()) SetHose(!HoseOn);

        if (windowObject.activeSelf)
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.2f;
                RefreshWindow();
            }
        }

        UpdateHose();
    }

    private bool HoseKeyPressed()
    {
        return !PixelFirstPerson.Active && PixelKeys.Pressed(PixelAction.Hose); // rebindable in Settings
    }

    private static bool IsTyping()
    {
        EventSystem es = EventSystem.current;
        return es != null && es.currentSelectedGameObject != null &&
               es.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    private void SetHose(bool on)
    {
        if (on && !bankActive) return;
        if (HoseOn == on) return;
        HoseOn = on;
        PixelClicker.ExternalClickBlock = on;
        if (!on) StopArea();
        if (hoseRoot != null) hoseRoot.SetActive(on);
        controlReady = false;
        swayReady = false;
        PixelAudio.Play("hose_toggle");
        if (on) PixelHints.Trigger("bank_hose");
        RefreshHoseButton();
    }

    // ------------------------------------------------------------------
    // The hose
    // ------------------------------------------------------------------

    private void BuildHose()
    {
        hoseRoot = new GameObject("Pixel Bank Hose");
        hoseRoot.SetActive(false);

        GameObject tube = new GameObject("Hose", typeof(MeshFilter), typeof(MeshRenderer));
        tube.transform.SetParent(hoseRoot.transform, false);
        hoseFilter = tube.GetComponent<MeshFilter>();
        // The hose is drawn unlit with vertex colours (shaded by hand, see HoseVertexColor) so its top end can fade to nothing instead of being cut off by the camera.
        hoseMaterial = new Material(PixelShaders.SpriteDefault());
        tube.GetComponent<MeshRenderer>().sharedMaterial = hoseMaterial;
        hoseColorOf = HoseVertexColor;
        MeshRenderer hoseRenderer = tube.GetComponent<MeshRenderer>();
        hoseRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // the hose casts no shadow
        hoseRenderer.receiveShadows = false;
        hosePoints = new Vector3[hoseSegments * 2 + 1]; // twice as many points: the hose now also covers a long stretch off screen

        BuildNozzle();

        GameObject textObject = new GameObject("Bank Message Text");
        textObject.transform.SetParent(hoseRoot.transform, false);
        displayText = textObject.AddComponent<TextMeshPro>();
        displayText.fontSize = displayTextSize;
        displayText.fontStyle = FontStyles.Bold;
        displayText.alignment = TextAlignmentOptions.Left;
        displayText.color = Color.white;
        displayText.overflowMode = TextOverflowModes.Overflow;
        displayText.rectTransform.sizeDelta = new Vector2(6f, 1f);
        displayText.rectTransform.pivot = new Vector2(0f, 0.5f); // the text starts at its position and runs to the right
        if (clicker.UIFont != null) displayText.font = clicker.UIFont;

        // The laser: a thin red beam straight down from the nozzle with a red dot where it lands.
        GameObject lineObject = new GameObject("Aim Line", typeof(LineRenderer));
        lineObject.transform.SetParent(hoseRoot.transform, false);
        aimLine = lineObject.GetComponent<LineRenderer>();
        aimMaterial = new Material(PixelShaders.SpriteDefault());
        aimLine.sharedMaterial = aimMaterial;
        aimLine.useWorldSpace = true;
        aimLine.numCapVertices = 4;
        aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        aimLine.receiveShadows = false;
        aimLine.positionCount = 0;
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Laser Dot";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.SetParent(hoseRoot.transform, false);
        dotMaterial = new Material(PixelShaders.SpriteDefault()) { color = new Color(1f, 0.1f, 0.08f, 1f) };
        Renderer markerRenderer = marker.GetComponent<MeshRenderer>();
        markerRenderer.sharedMaterial = dotMaterial;
        markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerRenderer.receiveShadows = false;
        aimMarker = marker.transform;
        aimMarker.gameObject.SetActive(false);

        // The ring that shows the area (a unit-radius ring, scaled to the area's radius).
        GameObject ring = new GameObject("Area Ring", typeof(MeshFilter), typeof(MeshRenderer));
        ring.transform.SetParent(hoseRoot.transform, false);
        ring.GetComponent<MeshFilter>().sharedMesh = PixelSorterDevice.BuildRingMesh(1f - areaRingThickness, 1f, 0.02f, 56);
        areaRing = ring.transform;
        areaRingRenderer = ring.GetComponent<Renderer>();
        areaRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        areaRing.gameObject.SetActive(false);
    }

    private static readonly Color CollarColor = new Color(0.22f, 0.24f, 0.3f, 1f);
    private static readonly Color AccentColor = new Color(0.95f, 0.6f, 0.15f, 1f);
    private static readonly Color DarkColor = new Color(0.05f, 0.05f, 0.07f, 1f);

    private Transform Part(string name, PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Color color, bool translucent = false)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Material m = clicker.CreateVisualMaterial(color, translucent);
        if (m != null) r.sharedMaterial = m; else r.material.color = color;
        return go.transform;
    }

    /// <summary>
    /// The nozzle: a hose collar with an orange ring, a metal body that narrows to a flared lip with a dark opening, a glass cube mounted
    /// on its upper side (the selected pixel spins inside it) and a small plate on the camera side that carries the stored count.
    /// Built along -Y from the mouth at the origin (+Y = the way pixels leave).
    /// </summary>
    private void BuildNozzle()
    {
        float L = nozzleLength, R = nozzleRadius;
        GameObject root = new GameObject("Nozzle");
        root.transform.SetParent(hoseRoot.transform, false);
        nozzle = root.transform;

        // Cylinder primitives are 2 tall: y scale = half the height.
        Part("Swivel Ball", PrimitiveType.Sphere, nozzle, new Vector3(0f, -0.95f * L, 0f), Vector3.one * (2.35f * R), CollarColor); // the hose ends in here, so any angle looks connected
        Part("Collar", PrimitiveType.Cylinder, nozzle, new Vector3(0f, -0.9f * L, 0f), new Vector3(2.1f * R, 0.1f * L, 2.1f * R), CollarColor);
        Part("Collar Ring", PrimitiveType.Cylinder, nozzle, new Vector3(0f, -0.77f * L, 0f), new Vector3(2.45f * R, 0.035f * L, 2.45f * R), AccentColor);
        Part("Body", PrimitiveType.Cylinder, nozzle, new Vector3(0f, -0.46f * L, 0f), new Vector3(2f * R, 0.29f * L, 2f * R), nozzleColor);
        Part("Grip Ring", PrimitiveType.Cylinder, nozzle, new Vector3(0f, -0.3f * L, 0f), new Vector3(2.2f * R, 0.025f * L, 2.2f * R), CollarColor);
        Part("Front", PrimitiveType.Cylinder, nozzle, new Vector3(0f, -0.14f * L, 0f), new Vector3(1.85f * R, 0.14f * L, 1.85f * R), Color.Lerp(nozzleColor, CollarColor, 0.35f));
        Part("Lip", PrimitiveType.Cylinder, nozzle, new Vector3(0f, -0.025f * L, 0f), new Vector3(2.5f * R, 0.025f * L, 2.5f * R), AccentColor);
        Part("Opening", PrimitiveType.Cylinder, nozzle, new Vector3(0f, 0.002f * L, 0f), new Vector3(1.7f * R, 0.01f * L, 1.7f * R), DarkColor);

        // The glass cube sits on the upper (+X) side of the body, on a small mount.
        float g = Mathf.Max(0.1f, displaySize);
        Part("Glass Mount", PrimitiveType.Cube, nozzle, new Vector3(R + 0.04f, -0.45f * L, 0f), new Vector3(0.1f, 0.2f * L, 0.14f), CollarColor);
        Part("Glass Base", PrimitiveType.Cube, nozzle, new Vector3(R + 0.09f, -0.45f * L, 0f), new Vector3(0.05f, g * 1.1f, g * 1.1f), CollarColor);
        glassCube = Part("Glass Cube", PrimitiveType.Cube, nozzle, new Vector3(R + 0.115f + g * 0.5f, -0.45f * L, 0f), Vector3.one * g, new Color(0.7f, 0.9f, 1f, 0.26f), true);
        // Thin metal corner bars make the glass read as a case.
        float bar = 0.018f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Part("Glass Edge", PrimitiveType.Cube, glassCube, new Vector3(sx * 0.5f, 0f, sz * 0.5f), new Vector3(bar / g, 1.02f, bar / g), CollarColor);

        // The count plate (on the camera side; UpdateNozzle flips it to whichever side faces the camera).
        counterPlate = Part("Counter Plate", PrimitiveType.Cube, nozzle, new Vector3(0f, -0.46f * L, 0f), new Vector3(0.3f, 0.2f, 0.025f), DarkColor);
        GameObject textObject = new GameObject("Counter Text");
        textObject.transform.SetParent(counterPlate, false);
        textObject.transform.localPosition = new Vector3(0f, 0f, -0.52f);
        counterText = textObject.AddComponent<TextMeshPro>();
        counterText.fontSize = 7f;
        counterText.fontStyle = FontStyles.Bold;
        counterText.alignment = TextAlignmentOptions.Center;
        counterText.color = new Color(0.55f, 1f, 0.65f, 1f);
        counterText.overflowMode = TextOverflowModes.Overflow;
        counterText.rectTransform.sizeDelta = new Vector2(1f, 1f);
        counterText.enableAutoSizing = false;
        if (clicker.UIFont != null) counterText.font = clicker.UIFont;
        // The text is scaled with the plate (a child), so undo the plate's squash.
        textObject.transform.localScale = new Vector3(1f / 0.3f, 1f / 0.2f, 1f / 0.025f) * 0.2f;
    }

    private void UpdateHose()
    {
        if (!HoseOn || hoseRoot == null) return;

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (consumables == null) consumables = PixelFind.First<PixelConsumables>();
        bool placing = consumables != null && consumables.IsPlacing;
        bool shown = cam != null && !placing;
        if (hoseRoot.activeSelf != shown) hoseRoot.SetActive(shown);
        if (!shown) return;

        float dt = Time.unscaledDeltaTime;
        Ray ray = cam.ScreenPointToRay(PointerPosition());

        // Where the mouse points in the world (the first thing its ray hits, ignoring the main cube); the nozzle hangs straight above that spot.
        Vector3 point;
        bool found = false;
        point = ray.origin + ray.direction * 10f;
        int count = Physics.RaycastNonAlloc(ray, beamHits, 200f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (!BlocksBeam(beamHits[i].collider, out _) || beamHits[i].distance >= nearest) continue;
            nearest = beamHits[i].distance;
            point = beamHits[i].point;
            found = true;
        }
        if (!found)
        {
            float floorY = clicker.PixelTransform != null ? clicker.PixelTransform.position.y - clicker.PixelBaseSize * 0.5f : 0f;
            if (new Plane(Vector3.up, new Vector3(0f, floorY, 0f)).Raycast(ray, out float enter)) point = ray.GetPoint(enter);
        }

        // The nozzle hangs straight above that spot, lowered if it would go off the top of the screen.
        Vector3 up = Vector3.up;
        float unit = Mathf.Max(0.05f, clicker.PixelBaseSize);
        float height = (nozzleHeightCubes > 0f ? nozzleHeightCubes : 2.5f) * unit;
        for (int i = 0; i < 7; i++)
        {
            Vector3 view = cam.WorldToViewportPoint(point + up * (height + nozzleLength * NozzleScale * 1.3f));
            if (view.z <= 0f || view.y <= 0.9f) break;
            height *= 0.75f;
        }
        height = Mathf.Max(height, unit * 0.6f);
        Vector3 tip = point + up * height;   // the nozzle's mouth

        // The laser: straight down from the mouth. What it lands on is where a click sucks from and where a spat pixel is dropped.
        FindBeam(tip);

        // The hose hangs from far above the top of the screen, arriving straight down into the back of the nozzle. Its top and middle trail
        // behind the nozzle so it swings when you move the mouse.
        Vector3 hoseEnd = tip + up * (nozzleLength * NozzleScale * 0.93f);
        float arm = Mathf.Clamp(height * 0.5f, 0.4f, 2.5f);
        Vector3 endHandle = hoseEnd + up * arm;
        float extra = hoseOffscreenExtra > 0f ? hoseOffscreenExtra : 14f;
        Vector3 wantedTop = hoseEnd + up * extra;
        if (!swayReady) { swayTop = wantedTop; swayReady = true; }
        swayTop = Vector3.Lerp(swayTop, wantedTop, 1f - Mathf.Exp(-followSharpness * 0.5f * dt));
        Vector3 start = swayTop;
        Vector3 wantedControl = endHandle + up * (arm * 1.5f);
        if (!controlReady) { control = wantedControl; controlReady = true; }
        control = Vector3.Lerp(control, wantedControl, 1f - Mathf.Exp(-followSharpness * dt));
        Vector3 heading = Vector3.down;

        int segments = hosePoints.Length - 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments, u = 1f - t;
            hosePoints[i] = u * u * u * start + 3f * u * u * t * control + 3f * u * t * t * endHandle + t * t * t * hoseEnd;
        }
        fadeBaseY = hoseEnd.y;
        Light sun = RenderSettings.sun;
        hoseLightDir = sun != null ? -sun.transform.forward : new Vector3(0.3f, 1f, -0.4f).normalized;
        hoseMesh = PixelTube.Build(hosePoints, hoseRadius, hoseSides, hoseMesh, hoseColorOf);
        hoseFilter.sharedMesh = hoseMesh;

        kick = Mathf.MoveTowards(kick, 0f, dt * 4f);
        PlaceNozzle(cam, tip, heading, 1f + kick * 0.15f);
        mouthPosition = tip + heading * 0.05f;
        mouthDirection = heading;
        tipPosition = beamPoint;

        UpdateDisplay(cam, tip);
        HandleInput(cam, beamTarget);
    }

    private Vector3 mouthPosition, mouthDirection;

    private Material hoseMaterial;
    private System.Func<int, Vector3, Vector3, Color> hoseColorOf;
    private float fadeBaseY;
    private Vector3 hoseLightDir = Vector3.up;

    /// <summary>Hose vertex colour: the hose colour shaded by a simple light, fading out towards the top.</summary>
    private Color HoseVertexColor(int ring, Vector3 position, Vector3 normal)
    {
        float light = 0.5f + 0.5f * Mathf.Max(0f, Vector3.Dot(normal, hoseLightDir));
        float start = hoseFadeStart > 0f ? hoseFadeStart : 3f, length = hoseFadeLength > 0f ? hoseFadeLength : 4f;
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((position.y - fadeBaseY - start) / length));
        return new Color(hoseColor.r * light, hoseColor.g * light, hoseColor.b * light, alpha);
    }
    private float NozzleScale => nozzleScale > 0f ? nozzleScale : 1.6f;

    // ------------------------------------------------------------------
    // The laser beam: straight down from the nozzle
    // ------------------------------------------------------------------

    /// <summary>True when this collider stops the beam (everything except the main cube). 'target' = the old pixel it belongs to if a click could take it.</summary>
    private bool BlocksBeam(Collider c, out Rigidbody target)
    {
        target = null;
        if (c == null) return false;
        if (clicker.PixelTransform != null && c.transform.IsChildOf(clicker.PixelTransform)) return false;
        if (hoseRoot != null && c.transform.IsChildOf(hoseRoot.transform)) return false;
        Rigidbody body = c.attachedRigidbody;
        if (body == null) return true;
        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info == null || !clicker.IsValidTierIndex(info.tierIndex)) return true;
        if (body.isKinematic || clicker.IsFlyingPixel(body)) return true;
        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null && despawn.IsDespawning) return true;
        target = body;
        return true;
    }

    /// <summary>Works out where the beam from 'mouth' ends (<see cref="beamPoint"/>) and which old pixel it is on (<see cref="beamTarget"/>).</summary>
    private void FindBeam(Vector3 mouth)
    {
        // The exact line straight down: the first thing it touches.
        beamPoint = mouth + Vector3.down * 40f;
        float best = float.MaxValue;
        Rigidbody lineBody = null;
        int count = Physics.RaycastNonAlloc(mouth, Vector3.down, beamHits, 80f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!BlocksBeam(beamHits[i].collider, out Rigidbody body) || beamHits[i].distance >= best) continue;
            best = beamHits[i].distance;
            beamPoint = beamHits[i].point;
            lineBody = body;
        }

        // A little forgiving: a pixel the (thicker) beam brushes before it reaches that point counts too, and the dot snaps onto its top.
        beamTarget = lineBody;
        if (beamTarget == null)
        {
            float radius = Mathf.Max(0.05f, suckRadius * 0.5f);
            count = Physics.SphereCastNonAlloc(mouth, radius, Vector3.down, beamHits, best == float.MaxValue ? 80f : best + radius, ~0, QueryTriggerInteraction.Ignore);
            float nearestSphere = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!BlocksBeam(beamHits[i].collider, out Rigidbody body) || body == null || beamHits[i].distance >= nearestSphere) continue;
                nearestSphere = beamHits[i].distance;
                beamTarget = body;
            }
            if (beamTarget != null)
            {
                Collider c = beamTarget.GetComponent<Collider>();
                Bounds bounds = c != null ? c.bounds : new Bounds(beamTarget.position, Vector3.one * 0.2f);
                beamPoint = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }
        }
    }

    // ------------------------------------------------------------------
    // Input: scroll to choose, left-click to spit, right-click to suck
    // ------------------------------------------------------------------

    private void HandleInput(Camera cam, Rigidbody candidate)
    {
        if (PixelPauseMenu.IsPaused || Time.timeScale <= 0f)
        {
            StopArea();
            return;
        }

        bool overUI = PointerOverUI();
        if (!overUI)
        {
            int scrollSteps = ScrollSteps();
            if (scrollSteps != 0) SelectNext(scrollSteps);
            if (LeftPressed()) Spit();
        }

        // Right button: a click sucks up one pixel; holding on turns that into area suction.
        if (!overUI && RightPressed())
        {
            rightDown = true;
            rightHeldTime = 0f;
            areaActive = areaFinished = false;
            Suck(candidate);
        }

        if (rightDown)
        {
            if (!RightHeld() || overUI) StopArea();
            else
            {
                rightHeldTime += Time.unscaledDeltaTime;
                UpdateArea(cam);
            }
        }
        else UpdateAreaRing(cam, false, 0f);
    }

    private void StopArea()
    {
        rightDown = false;
        areaActive = false;
        areaFinished = false;
        rightHeldTime = 0f;
        if (areaRing != null && areaRing.gameObject.activeSelf) areaRing.gameObject.SetActive(false);
    }

    /// <summary>The hold has gone on long enough: pull in old pixels of the selected type around the nozzle, a few a second.</summary>
    private void UpdateArea(Camera cam)
    {
        float charge = Mathf.Clamp01(rightHeldTime / areaHoldSeconds);

        if (!areaActive && !areaFinished && rightHeldTime >= areaHoldSeconds)
        {
            // The type to take: the selected one, or (if nothing is selected) the type of the old pixel under the beam.
            areaTier = selected >= 0 && selected < counts.Length ? selected : -1;
            if (areaTier < 0)
            {
                OldPixelInfo info = beamTarget != null ? beamTarget.GetComponent<OldPixelInfo>() : null;
                if (info != null) areaTier = info.tierIndex;
            }

            if (areaTier < 0)
            {
                flashMessage = areaNoTypeText;
                fullTimer = 0.9f;
                PixelAudio.Play("bank_empty");
                areaFinished = true; // nothing to match: no area suction for the rest of this hold
            }
            else
            {
                areaActive = true;
                areaTimer = 0f;
            }
        }

        if (areaActive)
        {
            areaTimer -= Time.unscaledDeltaTime;
            while (areaTimer <= 0f && areaActive)
            {
                areaTimer += 1f / Mathf.Max(0.5f, areaRate * (PixelClicker.InfiniteResources ? 10f : 1f)); // infinite resources: area suction is ten times faster
                Rigidbody target = FindAreaTarget(cam);
                if (target == null) { areaTimer = 0.05f; break; }
                if (!TakeIntoBank(target))
                {
                    areaActive = false;   // the bank is full
                    areaFinished = true;
                }
            }
        }

        bool showRing = rightHeldTime > 0.08f && !areaFinished;
        UpdateAreaRing(cam, showRing, areaActive ? 1f : charge);
    }

    /// <summary>The nearest old pixel of the area's type inside the ring (measured across the screen around the nozzle).</summary>
    private Rigidbody FindAreaTarget(Camera cam)
    {
        Rigidbody best = null;
        float bestDistance = areaRadius;
        var pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic) continue;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || info.tierIndex != areaTier || clicker.IsFlyingPixel(body)) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && despawn.IsDespawning) continue;

            Vector3 to = body.position - tipPosition;
            float across = new Vector2(to.x, to.z).magnitude; // distance across the floor round the red dot
            if (across <= bestDistance && Mathf.Abs(to.y) < areaRadius) { bestDistance = across; best = body; }
        }
        return best;
    }

    private void UpdateAreaRing(Camera cam, bool show, float fraction)
    {
        if (areaRing == null) return;
        if (areaRing.gameObject.activeSelf != show) areaRing.gameObject.SetActive(show);
        if (!show) return;

        // The ring takes the selected pixel's colour (white while nothing is selected).
        int tier = areaActive ? areaTier : selected;
        if (areaRingTier != tier)
        {
            areaRingTier = tier;
            Color c = tier >= 0 && tier < clicker.Tiers.Length ? clicker.Tiers[tier].color : Color.white;
            c.a = areaRingOpacity;
            if (areaRingMaterial != null) Destroy(areaRingMaterial);
            areaRingMaterial = clicker.CreateVisualMaterial(c, true);
            if (areaRingMaterial != null) areaRingRenderer.sharedMaterial = areaRingMaterial;
        }

        float pulse = areaActive ? 1f + Mathf.Sin(Time.unscaledTime * 12f) * 0.03f : 1f;
        areaRing.position = tipPosition + Vector3.up * 0.03f;
        areaRing.rotation = Quaternion.Euler(90f, 0f, 0f);   // lies flat on the floor round the red dot
        areaRing.localScale = Vector3.one * (areaRadius * Mathf.Lerp(0.15f, 1f, fraction) * pulse);
    }

    private static int ScrollSteps()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        float wheel = Input.mouseScrollDelta.y;
#endif
        return Mathf.Abs(wheel) > 0.01f ? (wheel > 0f ? 1 : -1) : 0;
    }

    /// <summary>Selects the next stored pixel type in the given direction (wraps around).</summary>
    private void SelectNext(int direction)
    {
        int next = NextStocked(selected, direction);
        if (next >= 0 && next != selected) { selected = next; PixelAudio.Play("bank_select"); }
        refreshTimer = 0f;
    }

    /// <summary>The next tier with stored pixels after 'from' in the given direction, or -1 if there is none.</summary>
    private int NextStocked(int from, int direction)
    {
        int n = counts.Length;
        for (int step = 1; step <= n; step++)
        {
            int i = ((from + direction * step) % n + n) % n;
            if (counts[i] > 0) return i;
        }
        return -1;
    }

    private void Spit()
    {
        if (selected < 0 || selected >= counts.Length || counts[selected] <= 0)
        {
            selected = NextStocked(selected, 1);
            if (selected < 0) { PixelAudio.Play("bank_empty"); fullTimer = 0.9f; flashMessage = emptyDisplayText; }
            return;
        }

        int tier = selected;
        double amount = values[tier] / counts[tier];
        if (!PixelClicker.InfiniteResources) // dev tools "Infinite resources": the bank never runs out of what it holds
        {
            counts[tier]--;
            values[tier] = counts[tier] > 0 ? Math.Max(0d, values[tier] - amount) : 0d;
        }

        // It drops straight down the beam onto the red dot.
        clicker.SpawnStoredPixel(tier, amount, mouthPosition, mouthDirection * spitSpeed);

        // A vacuum pixel works again when it is spat out: every other old pixel swirls into IT (not into the cube) and pays out once more.
        if (clicker.Tiers[tier].vacuum)
        {
            var olds = clicker.OldPixels;
            Rigidbody spat = olds.Count > 0 ? olds[olds.Count - 1] : null; // the pixel just spawned is the last one listed
            clicker.Vacuum(tier, spat != null ? spat.transform : null, spat);
        }

        kick = 1f;
        PixelAudio.Play("bank_spit");
        PixelHints.Trigger("bank_spit");
        if (counts[tier] <= 0) selected = NextStocked(tier, 1);
        refreshTimer = 0f;
    }


    private void Suck(Rigidbody candidate)
    {
        if (candidate != null) TakeIntoBank(candidate);
    }

    /// <summary>Stores one old pixel (it flies into the nozzle). Returns false, and says so, if the bank is full.</summary>
    /// <summary>Is the bank full (never with infinite resources)?</summary>
    public bool IsFull => !PixelClicker.InfiniteResources && Total >= Capacity;

    /// <summary>Stores an old pixel for the Robot Worker; it flies to 'flyTo' (his hand) instead of the nozzle. False if the bank is full.</summary>
    public bool StoreOldPixel(Rigidbody body, Vector3 flyTo) => body != null && TakeIntoBank(body, flyTo);

    private bool TakeIntoBank(Rigidbody body, Vector3? flyTo = null)
    {
        if (!PixelClicker.InfiniteResources && Total >= Capacity) // infinite resources: the bank never fills up
        {
            fullTimer = 0.9f;
            flashMessage = fullText;
            PixelAudio.Play("bank_full");
            PixelHints.Trigger("bank_full");
            return false;
        }

        OldPixelInfo info = body.GetComponent<OldPixelInfo>();
        if (info == null || !clicker.ReleaseOldPixel(body, false)) return true; // gone already: not a reason to stop

        counts[info.tierIndex]++;
        values[info.tierIndex] += info.amount;
        if (selected < 0) selected = info.tierIndex;

        kick = 1f;
        PixelAudio.Play("bank_suck");
        PixelHints.Trigger("bank_suck");
        StartCoroutine(FlyIntoNozzle(body, flyTo));
        refreshTimer = 0f;
        return true;
    }

    private IEnumerator FlyIntoNozzle(Rigidbody body, Vector3? flyTo = null)
    {
        if (body == null) yield break;
        PixelStats.Count("old.banked");
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;
        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.Held = true; // no despawn swell while it flies in

        Transform t = body.transform;
        Vector3 start = t.position, startScale = t.localScale;
        float elapsed = 0f;
        while (elapsed < suckSeconds && t != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / suckSeconds);
            float ease = k * k;
            t.position = Vector3.Lerp(start, flyTo ?? mouthPosition, ease);
            t.localScale = startScale * Mathf.Lerp(1f, 0.05f, ease);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    // ------------------------------------------------------------------
    // The little display of the selected pixel
    // ------------------------------------------------------------------

    /// <summary>Orients the nozzle assembly: +Y along the aim, +X towards the top of the screen (so the glass cube sits on top).</summary>
    private void PlaceNozzle(Camera cam, Vector3 tip, Vector3 heading, float punch)
    {
        Vector3 up = cam.transform.up - heading * Vector3.Dot(cam.transform.up, heading);
        if (up.sqrMagnitude < 0.0001f) up = cam.transform.right - heading * Vector3.Dot(cam.transform.right, heading);
        up.Normalize();
        Vector3 forward = Vector3.Cross(up, heading); // local +Z
        nozzle.rotation = Quaternion.LookRotation(forward, heading);
        nozzle.position = tip;
        nozzle.localScale = Vector3.one * (punch * NozzleScale);

        // The count plate goes on the side that faces the camera and is read upright (its up is the screen-up side of the nozzle).
        float side = Vector3.Dot(forward, cam.transform.forward) > 0f ? 1f : -1f; // +1 when local +Z points away from the camera (so the camera-facing side is -Z)
        float r = nozzleRadius * 0.92f;
        counterPlate.localPosition = new Vector3(0f, counterPlate.localPosition.y, -side * r);
        counterPlate.rotation = Quaternion.LookRotation(cam.transform.forward, up);
    }

    private void UpdateDisplay(Camera cam, Vector3 tip)
    {
        bool hasSelection = selected >= 0 && selected < counts.Length && counts[selected] > 0;
        if (selected >= 0 && !hasSelection) { selected = NextStocked(selected, 1); hasSelection = selected >= 0; }

        // The glass cube holds a spinning model of the selected pixel (its real look).
        if (!hasSelection)
        {
            if (glassPixel != null) { Destroy(glassPixel); glassPixel = null; }
            glassTier = -1;
        }
        else
        {
            if (glassTier != selected || glassPixel == null)
            {
                if (glassPixel != null) Destroy(glassPixel);
                glassPixel = clicker.CreateDisplayPixel(selected, glassCube, 0.52f);
                glassTier = selected;
                displayTier = selected;
                if (glassPixel != null) glassPixel.transform.localPosition = Vector3.zero;
            }
            if (glassPixel != null) glassPixel.transform.localRotation = Quaternion.Euler(20f, Time.unscaledTime * displaySpin, 0f);
        }

        // The stored count is written on the plate on the nozzle.
        counterPlate.gameObject.SetActive(true);
        string number = hasSelection ? PixelClicker.FormatNumber(counts[selected]) : "0";
        if (counterText.text != number) counterText.text = number;
        counterText.color = hasSelection ? new Color(0.55f, 1f, 0.65f, 1f) : new Color(1f, 0.45f, 0.4f, 1f);

        // A floating message only when there is something to say ("Bank full" / "Bank empty").
        fullTimer = Mathf.Max(0f, fullTimer - Time.unscaledDeltaTime);
        string text = fullTimer > 0f ? flashMessage : !hasSelection ? emptyDisplayText : "";
        displayText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        if (!string.IsNullOrEmpty(text))
        {
            displayText.text = text;
            displayText.transform.position = tip + cam.transform.right * displayOffset.x + cam.transform.up * displayOffset.y;
            displayText.transform.rotation = cam.transform.rotation;
            displayText.alignment = TextAlignmentOptions.Left;
        }

        UpdateAimLine(hasSelection);
    }

    /// <summary>The laser: a thin red beam straight down from the nozzle to the red dot where it lands.</summary>
    private void UpdateAimLine(bool hasSelection)
    {
        if (aimLine == null) return;
        if (hideAimLine)
        {
            if (aimLine.positionCount != 0) aimLine.positionCount = 0;
            if (aimMarker.gameObject.activeSelf) aimMarker.gameObject.SetActive(false);
            return;
        }

        float alpha = aimLineAlpha > 0f ? aimLineAlpha : 0.55f;
        float width = aimLineWidth > 0f ? aimLineWidth : 0.025f;
        aimLine.positionCount = 2;
        aimLine.SetPosition(0, mouthPosition);
        aimLine.SetPosition(1, beamPoint);
        aimLine.widthMultiplier = width;
        aimLine.startColor = new Color(1f, 0.15f, 0.1f, alpha);
        aimLine.endColor = new Color(1f, 0.15f, 0.1f, alpha);
        aimMaterial.color = Color.white;

        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 9f);
        aimMarker.gameObject.SetActive(true);
        aimMarker.position = beamPoint;
        aimMarker.localScale = Vector3.one * (Mathf.Max(0.05f, clicker.PixelBaseSize * 0.16f) * pulse);
    }

    // ------------------------------------------------------------------
    // UI: the tab, the hose button and the window
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasRoot = PixelUIKit.CreateCanvas("PixelBank Canvas", sortingOrder, referenceResolution, true);
        PixelHud hud = PixelHud.Ensure(gameObject);

        // The Bank tab, docked to the middle of the left edge (opposite the Toggles tab).
        Button bank = PixelUIKit.CreateButton(font, canvasRoot.transform, "Bank Button", bankButtonText, hud.ButtonSize,
                                              bankButtonColor, buttonTextColor, 30f);
        // The hose button slides out with it, underneath.
        hoseButton = PixelUIKit.CreateButton(font, canvasRoot.transform, "Hose Button", "", hud.ButtonSize, hoseButtonColor,
                                             buttonTextColor, 30f);
        hoseButtonImage = hoseButton.GetComponent<Image>();
        hoseButtonLabel = hoseButton.GetComponentInChildren<TMP_Text>();

        hud.Dock(bank.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), KeepButtonsOut);
        hud.Dock(hoseButton.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), KeepButtonsOut,
                 -(hud.ButtonSize.y + buttonGap), hoseButtonWidthFraction);
        bankDock = bank.GetComponent<PixelDockedButton>();
        hoseDock = hoseButton.GetComponent<PixelDockedButton>();
        bankGroup = bank.gameObject.AddComponent<CanvasGroup>();
        hoseGroup = hoseButton.gameObject.AddComponent<CanvasGroup>();

        bank.onClick.AddListener(ToggleWindow);
        hoseButton.onClick.AddListener(() => SetHose(!HoseOn));
        RefreshHoseButton();

        // The window, just right of the tab.
        windowObject = new GameObject("Bank Window", typeof(RectTransform), typeof(Image));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(windowObject.GetComponent<Image>(), panelColor);
        RectTransform wr = windowObject.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0f, 0.5f);
        wr.sizeDelta = windowSize;
        wr.anchoredPosition = new Vector2(gapToButton + hud.SideMargin * 0.35f, 0f);   // the window takes the place of the tab and hose buttons while it is open

        BuildWindowHoseButton();
        RefreshHoseButton();

        float y = 16f;
        TMP_Text title = PixelUIKit.CreateText(font, windowObject.transform, "Title", windowTitle, titleFontSize,
                                               TextAlignmentOptions.Center, FontStyles.Bold, textColor);
        PixelUIKit.Caps(title);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-170f, titleFontSize * 1.4f);
        tr.anchoredPosition = new Vector2(0f, -y);

        Button close = PixelUIKit.CreateButton(font, windowObject.transform, "Close", "X", new Vector2(64f, 64f),
                                               new Color(0.3f, 0.3f, 0.35f, 1f), textColor, 34f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -12f);
        close.onClick.AddListener(CloseWindow);
        y += titleFontSize * 1.4f + 10f;

        capacityLabel = PixelUIKit.CreateText(font, windowObject.transform, "Capacity", "", fontSize, TextAlignmentOptions.Center,
                                              FontStyles.Normal, new Color(textColor.r, textColor.g, textColor.b, 0.8f));
        RectTransform pr = capacityLabel.rectTransform;
        pr.anchorMin = new Vector2(0f, 1f);
        pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(-40f, fontSize * 1.4f);
        pr.anchoredPosition = new Vector2(0f, -y);
        y += fontSize * 1.4f + 10f;

        float hintHeight = fontSize * 1.9f;
        scroll = PixelUIKit.CreateScrollView(windowObject.transform, "Bank List", scrollbarColor, 12f, rowHeight, out content, out bar);
        RectTransform vr = scroll.GetComponent<RectTransform>();
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(14f, 14f + hintHeight);
        vr.offsetMax = new Vector2(-14f, -y);

        TMP_Text hint = PixelUIKit.CreateText(font, windowObject.transform, "Hint", hintText, fontSize * 0.7f, TextAlignmentOptions.Center,
                                              FontStyles.Italic, new Color(textColor.r, textColor.g, textColor.b, 0.6f));
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(-40f, hintHeight);
        hr.anchoredPosition = new Vector2(0f, 10f);

        windowObject.SetActive(false);
        PixelWindows.Register(this, 14, () => windowObject != null && windowObject.activeSelf, CloseWindow);
        canvasRoot.SetActive(bankActive);
    }

    private CanvasGroup bankGroup, hoseGroup;
    private Image windowHoseImage;

    [Tooltip("Seconds the Bank and Hose tabs take to fade back in (already tucked away) after the Bank window closes.")]
    [SerializeField] private float buttonFadeSeconds = 1.2f;

    /// <summary>A hand-drawn hose icon button at the top left of the Bank window: switches the hose on / off (like the B key).</summary>
    private void BuildWindowHoseButton()
    {
        Button b = PixelUIKit.CreateButton(font, windowObject.transform, "Window Hose Button", "", new Vector2(64f, 64f),
                                           hoseButtonColor, buttonTextColor, 20f);
        windowHoseImage = b.GetComponent<Image>();
        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(14f, -12f);
        b.onClick.AddListener(() => SetHose(!HoseOn));

        Image Part(string name, Color color, Vector2 size, Vector2 pos)
        {
            GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(b.transform, false);
            Image im = g.GetComponent<Image>();
            im.color = color;
            im.raycastTarget = false;
            RectTransform pr = g.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = size;
            pr.anchoredPosition = pos;
            return im;
        }
        // A hose hanging from the top with a nozzle pointing down and a red laser dot.
        Part("Hose", new Color(0.12f, 0.13f, 0.17f, 1f), new Vector2(9f, 26f), new Vector2(0f, 17f));
        Part("Collar", AccentColor, new Vector2(17f, 5f), new Vector2(0f, 3f));
        Part("Body", new Color(0.85f, 0.86f, 0.9f, 1f), new Vector2(17f, 16f), new Vector2(0f, -6f));
        Part("Front", new Color(0.7f, 0.72f, 0.78f, 1f), new Vector2(11f, 6f), new Vector2(0f, -17f));
        Part("Lip", AccentColor, new Vector2(15f, 3f), new Vector2(0f, -21f));
        Part("Laser Dot", new Color(1f, 0.15f, 0.1f, 1f), new Vector2(6f, 6f), new Vector2(0f, -27f));
    }

    private void LateUpdate()
    {
        if (!built || windowObject == null || bankGroup == null) return;
        bool open = windowObject.activeSelf;
        FadeTab(bankDock, bankGroup, open);
        FadeTab(hoseDock, hoseGroup, open);
    }

    /// <summary>The tab is tucked away and invisible while the window is up; when it closes the tab fades back in (already tucked away), like the Toggles tab.</summary>
    private void FadeTab(PixelDockedButton dock, CanvasGroup group, bool windowOpen)
    {
        if (dock != null) dock.ForceHidden = windowOpen || group.alpha < 0.05f;
        if (windowOpen) { group.alpha = 0f; group.blocksRaycasts = false; return; }
        group.blocksRaycasts = true;
        group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / Mathf.Max(0.05f, buttonFadeSeconds));
    }

    /// <summary>Both buttons stay out while the window is open or the mouse is near either of them.</summary>
    private bool KeepButtonsOut()
    {
        if (windowObject != null && windowObject.activeSelf) return true;
        return (bankDock != null && bankDock.PointerNear()) || (hoseDock != null && hoseDock.PointerNear());
    }

    private void RefreshHoseButton()
    {
        if (hoseButtonLabel == null) return;
        hoseButtonLabel.text = HoseOn ? hoseOnText : string.Format(hoseOffText, PixelKeys.Name(PixelAction.Hose));
        hoseButtonImage.color = HoseOn ? hoseButtonOnColor : hoseButtonColor;
        if (windowHoseImage != null) windowHoseImage.color = HoseOn ? hoseButtonOnColor : hoseButtonColor;
    }

    private void ToggleWindow()
    {
        if (windowObject.activeSelf) CloseWindow();
        else
        {
            PixelUI.SetInventoryOpen(false);   // the Bank window replaces the Inventory and the Log (and they replace it)
            PixelLog.SetLogOpen(false);
            windowObject.SetActive(true);
            refreshTimer = 0f;
            RefreshWindow();
        }
    }

    private void CloseWindow() => windowObject.SetActive(false);

    /// <summary>Closes the Bank window if it is open (the Inventory and the Log call this when they open).</summary>
    public static void CloseWindowIfOpen()
    {
        PixelBank bank = PixelFind.First<PixelBank>();
        if (bank != null && bank.windowObject != null && bank.windowObject.activeSelf) bank.CloseWindow();
    }

    private void RefreshWindow()
    {
        capacityLabel.text = string.Format(capacityFormat, PixelClicker.FormatNumber(Total), PixelClicker.InfiniteResources ? "Infinite" : PixelClicker.FormatNumber(Capacity));

        // One row per unlocked pixel type that can be stored.
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        int used = 0;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!tiers[i].unlocked || tiers[i].flyAway) continue;
            if (used >= rows.Count) rows.Add(BuildRow());
            RowUI row = rows[used];
            row.tier = i;
            row.go.SetActive(true);
            row.go.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -used * (rowHeight + 8f));
            row.swatch.color = new Color(tiers[i].color.r, tiers[i].color.g, tiers[i].color.b, 1f);
            row.label.text = tiers[i].displayName;
            row.count.text = PixelClicker.FormatNumber(counts[i]);
            row.background.color = HoseOn && i == selected ? rowSelectedColor : rowColor;
            float dim = counts[i] > 0 ? 1f : 0.45f;
            row.label.color = new Color(textColor.r, textColor.g, textColor.b, dim);
            row.count.color = new Color(textColor.r, textColor.g, textColor.b, dim);
            used++;
        }
        for (int i = used; i < rows.Count; i++) rows[i].go.SetActive(false);

        float viewHeight = scroll.GetComponent<RectTransform>().rect.height;
        PixelUIKit.UpdateScrollView(scroll, bar, Mathf.Max(used * (rowHeight + 8f) - 8f, 0f), viewHeight);
    }

    private RowUI BuildRow()
    {
        RowUI row = new RowUI();
        row.go = new GameObject("Bank Row", typeof(RectTransform), typeof(Image), typeof(Button));
        row.go.transform.SetParent(content, false);
        row.background = row.go.GetComponent<Image>();
        row.background.color = rowColor;
        row.go.GetComponent<Button>().targetGraphic = row.background;
        RectTransform rr = row.go.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f);
        rr.sizeDelta = new Vector2(-24f, rowHeight);

        // Clicking a row selects that pixel type (when there are some stored).
        row.go.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (row.tier >= 0 && row.tier < counts.Length && counts[row.tier] > 0)
            {
                selected = row.tier;
                refreshTimer = 0f;
                PixelAudio.Play("bank_select");
            }
        });

        GameObject swatchObject = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
        swatchObject.transform.SetParent(row.go.transform, false);
        row.swatch = swatchObject.GetComponent<Image>();
        row.swatch.raycastTarget = false;
        RectTransform sr = swatchObject.GetComponent<RectTransform>();
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(rowHeight * 0.55f, rowHeight * 0.55f);
        sr.anchoredPosition = new Vector2(16f, 0f);

        row.label = PixelUIKit.CreateText(font, row.go.transform, "Label", "", fontSize, TextAlignmentOptions.MidlineLeft,
                                          FontStyles.Normal, textColor);
        row.label.enableAutoSizing = true;
        row.label.fontSizeMax = fontSize;
        row.label.fontSizeMin = 14f;
        RectTransform lr = row.label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(rowHeight * 0.55f + 32f, 0f);
        lr.offsetMax = new Vector2(-170f, 0f);

        row.count = PixelUIKit.CreateText(font, row.go.transform, "Count", "", fontSize, TextAlignmentOptions.MidlineRight,
                                          FontStyles.Bold, textColor);
        RectTransform cr = row.count.rectTransform;
        cr.anchorMin = new Vector2(1f, 0f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(1f, 0.5f);
        cr.sizeDelta = new Vector2(150f, 0f);
        cr.anchoredPosition = new Vector2(-16f, 0f);
        return row;
    }
}
