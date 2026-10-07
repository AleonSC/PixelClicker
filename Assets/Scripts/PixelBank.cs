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
    [Tooltip("Key that equips / puts away the hose.")]
    [SerializeField] private KeyCode hoseKey = KeyCode.B;

    [Tooltip("Where the hose comes in from, as a point on the screen (0 = left edge, 1 = right edge; 0 = bottom, 1 = top). Slightly negative x starts it just off screen.")]
    [SerializeField] private Vector2 hoseAnchor = new Vector2(-0.03f, 0.3f);

    [Min(0.01f)]
    [Tooltip("Thickness of the hose (world units).")]
    [SerializeField] private float hoseRadius = 0.09f;

    [Tooltip("Colour of the hose.")]
    [SerializeField] private Color hoseColor = new Color(0.25f, 0.27f, 0.32f, 1f);

    [Range(0f, 1.5f)]
    [Tooltip("How much the hose sags between its two ends (as a fraction of its length).")]
    [SerializeField] private float sag = 0.35f;

    [Min(0.5f)]
    [Tooltip("How quickly the hose's middle follows the mouse (low = lazy, swingy hose; high = stiff).")]
    [SerializeField] private float followSharpness = 6f;

    [Range(6, 60)]
    [Tooltip("How many pieces the hose is drawn from (more = smoother bends).")]
    [SerializeField] private int hoseSegments = 28;

    [Range(3, 16)]
    [Tooltip("How many sides the round hose has.")]
    [SerializeField] private int hoseSides = 10;

    [Min(0.05f)]
    [Tooltip("Length of the nozzle at the end of the hose (world units).")]
    [SerializeField] private float nozzleLength = 0.4f;

    [Min(0.02f)]
    [Tooltip("Radius of the nozzle (world units).")]
    [SerializeField] private float nozzleRadius = 0.17f;

    [Tooltip("Colour of the nozzle.")]
    [SerializeField] private Color nozzleColor = new Color(0.8f, 0.8f, 0.85f, 1f);

    [Header("Sucking and Spitting")]
    [Min(0.05f)]
    [Tooltip("How close (world units) an old pixel has to be to the mouse for a right-click to suck it up.")]
    [SerializeField] private float suckRadius = 0.7f;

    [Min(0.05f)]
    [Tooltip("Seconds a sucked-up pixel takes to fly into the nozzle.")]
    [SerializeField] private float suckSeconds = 0.3f;

    [Min(0f)]
    [Tooltip("How fast a spat-out pixel leaves the nozzle (world units per second).")]
    [SerializeField] private float spitSpeed = 7f;

    [Range(0f, 30f)]
    [Tooltip("Random wobble (degrees) of the direction a pixel is spat out.")]
    [SerializeField] private float spitSpread = 4f;

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

    [Tooltip("Text next to the display. {0} = pixel name, {1} = how many are stored.")]
    [SerializeField] private string displayTextFormat = "{0} x{1}";

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
    private Transform nozzle;
    private Transform displayCube;
    private Renderer displayRenderer;
    private TextMeshPro displayText;
    private Mesh hoseMesh;
    private Vector3[] hosePoints;
    private Vector3 control;
    private bool controlReady;
    private float nozzleDepth;
    private float kick;           // brief nozzle punch when spitting / sucking
    private float fullTimer;
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

    /// <summary>How many pixels the bank can hold.</summary>
    public int Capacity => capacity;

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
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(hoseKey);
#endif
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
        if (hoseRoot != null) hoseRoot.SetActive(on);
        controlReady = false;
        PixelAudio.Play("hose_toggle");
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
        Material hoseMat = clicker.CreateVisualMaterial(hoseColor, false);
        if (hoseMat != null) tube.GetComponent<MeshRenderer>().sharedMaterial = hoseMat;
        hosePoints = new Vector3[hoseSegments + 1];

        GameObject nozzleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        nozzleObject.name = "Nozzle";
        Destroy(nozzleObject.GetComponent<Collider>());
        nozzleObject.transform.SetParent(hoseRoot.transform, false);
        Material nozzleMat = clicker.CreateVisualMaterial(nozzleColor, false);
        if (nozzleMat != null) nozzleObject.GetComponent<Renderer>().sharedMaterial = nozzleMat;
        nozzle = nozzleObject.transform;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Selected Pixel";
        Destroy(cube.GetComponent<Collider>());
        cube.transform.SetParent(hoseRoot.transform, false);
        displayCube = cube.transform;
        displayRenderer = cube.GetComponent<Renderer>();

        GameObject textObject = new GameObject("Selected Pixel Text");
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

        Ray ray = cam.ScreenPointToRay(PointerPosition());
        Rigidbody candidate = FindSuckCandidate(ray, out float candidateDepth);

        // The nozzle sits at the depth of the pixel it is over (so the hose reaches the floor), else at the cube's depth.
        float baseDepth = Mathf.Max(cam.nearClipPlane + 0.5f,
                                    clicker.PixelTransform != null ? Vector3.Dot(clicker.PixelTransform.position - cam.transform.position, cam.transform.forward) : 8f);
        float wantedDepth = candidate != null ? candidateDepth : baseDepth;
        if (nozzleDepth <= 0f) nozzleDepth = wantedDepth;
        float dt = Time.unscaledDeltaTime;
        nozzleDepth = Mathf.Lerp(nozzleDepth, wantedDepth, 1f - Mathf.Exp(-12f * dt));

        float along = nozzleDepth / Mathf.Max(0.1f, Vector3.Dot(ray.direction, cam.transform.forward));
        Vector3 tip = ray.origin + ray.direction * along;
        Vector3 anchor = cam.ViewportToWorldPoint(new Vector3(hoseAnchor.x, hoseAnchor.y, baseDepth));

        // The hose's middle trails the ends and sags, so it bends and swings as the mouse moves.
        Vector3 wantedControl = (anchor + tip) * 0.5f - cam.transform.up * ((tip - anchor).magnitude * sag);
        if (!controlReady) { control = wantedControl; controlReady = true; }
        control = Vector3.Lerp(control, wantedControl, 1f - Mathf.Exp(-followSharpness * dt));

        int segments = hosePoints.Length - 1;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            hosePoints[i] = (1f - t) * (1f - t) * anchor + 2f * (1f - t) * t * control + t * t * tip;
        }
        hoseMesh = PixelTube.Build(hosePoints, hoseRadius, hoseSides, hoseMesh);
        hoseFilter.sharedMesh = hoseMesh;

        // Nozzle: a short cylinder along the end of the hose; its open end is where pixels come out.
        Vector3 heading = (hosePoints[segments] - hosePoints[segments - 2]).normalized;
        if (heading.sqrMagnitude < 0.0001f) heading = cam.transform.right;
        kick = Mathf.MoveTowards(kick, 0f, dt * 4f);
        float punch = 1f + kick * 0.35f;
        nozzle.rotation = Quaternion.FromToRotation(Vector3.up, heading);
        nozzle.localScale = new Vector3(nozzleRadius * 2f * punch, nozzleLength * 0.5f, nozzleRadius * 2f * punch);
        nozzle.position = tip - heading * (nozzleLength * 0.5f - 0.02f);
        mouthPosition = tip + heading * 0.05f;
        mouthDirection = heading;

        UpdateDisplay(cam, tip);
        HandleInput(cam, candidate);
    }

    private Vector3 mouthPosition, mouthDirection;

    /// <summary>The nearest suckable old pixel to the mouse ray (within the suck radius), and its depth from the camera.</summary>
    private Rigidbody FindSuckCandidate(Ray ray, out float depth)
    {
        depth = 0f;
        Rigidbody best = null;
        float bestDistance = float.MaxValue;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;

        var pixels = clicker.OldPixels;
        for (int i = 0; i < pixels.Count; i++)
        {
            Rigidbody body = pixels[i];
            if (body == null || body.isKinematic) continue;
            OldPixelInfo info = body.GetComponent<OldPixelInfo>();
            if (info == null || !clicker.IsValidTierIndex(info.tierIndex) || clicker.Tiers[info.tierIndex].flyAway) continue;
            OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
            if (despawn != null && despawn.IsDespawning) continue;

            Vector3 to = body.position - ray.origin;
            float onRay = Vector3.Dot(to, ray.direction);
            if (onRay <= 0f) continue;
            float off = Vector3.Cross(ray.direction, to).magnitude;
            if (off > suckRadius || off >= bestDistance) continue;

            bestDistance = off;
            best = body;
            depth = Vector3.Dot(to, cam.transform.forward) + Vector3.Dot(ray.origin - cam.transform.position, cam.transform.forward);
        }
        return best;
    }

    // ------------------------------------------------------------------
    // Input: scroll to choose, left-click to spit, right-click to suck
    // ------------------------------------------------------------------

    private void HandleInput(Camera cam, Rigidbody candidate)
    {
        if (PixelPauseMenu.IsPaused || Time.timeScale <= 0f || PointerOverUI()) return;

        int scrollSteps = ScrollSteps();
        if (scrollSteps != 0) SelectNext(scrollSteps);

        if (LeftPressed()) Spit();
        else if (RightPressed()) Suck(candidate);
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
            if (selected < 0) { PixelAudio.Play("bank_empty"); fullTimer = 0.9f; emptyFlash = true; }
            return;
        }

        int tier = selected;
        double amount = values[tier] / counts[tier];
        counts[tier]--;
        values[tier] = counts[tier] > 0 ? Math.Max(0d, values[tier] - amount) : 0d;

        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        Quaternion wobble = Quaternion.AngleAxis(UnityEngine.Random.Range(-spitSpread, spitSpread), cam.transform.forward);
        clicker.SpawnStoredPixel(tier, amount, mouthPosition, wobble * mouthDirection * spitSpeed);

        kick = 1f;
        PixelAudio.Play("bank_spit");
        if (counts[tier] <= 0) selected = NextStocked(tier, 1);
        refreshTimer = 0f;
    }

    private bool emptyFlash;

    private void Suck(Rigidbody candidate)
    {
        if (candidate == null) return;

        if (Total >= capacity)
        {
            fullTimer = 0.9f;
            emptyFlash = false;
            PixelAudio.Play("bank_full");
            return;
        }

        OldPixelInfo info = candidate.GetComponent<OldPixelInfo>();
        if (info == null || !clicker.ReleaseOldPixel(candidate, false)) return;

        counts[info.tierIndex]++;
        values[info.tierIndex] += info.amount;
        if (selected < 0) selected = info.tierIndex;

        kick = 1f;
        PixelAudio.Play("bank_suck");
        StartCoroutine(FlyIntoNozzle(candidate));
        refreshTimer = 0f;
    }

    private IEnumerator FlyIntoNozzle(Rigidbody body)
    {
        if (body == null) yield break;
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
            t.position = Vector3.Lerp(start, mouthPosition, ease);
            t.localScale = startScale * Mathf.Lerp(1f, 0.05f, ease);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    // ------------------------------------------------------------------
    // The little display of the selected pixel
    // ------------------------------------------------------------------

    private void UpdateDisplay(Camera cam, Vector3 tip)
    {
        bool hasSelection = selected >= 0 && selected < counts.Length && counts[selected] > 0;
        if (selected >= 0 && !hasSelection) { selected = NextStocked(selected, 1); hasSelection = selected >= 0; }

        Vector3 basePosition = tip + cam.transform.right * displayOffset.x + cam.transform.up * displayOffset.y;
        displayCube.gameObject.SetActive(hasSelection);
        if (hasSelection)
        {
            if (displayTier != selected)
            {
                displayTier = selected;
                PixelClicker.PixelTier t = clicker.Tiers[selected];
                Material m = clicker.CreateVisualMaterial(t.color, t.translucent);
                if (m != null) displayRenderer.sharedMaterial = m;
                else displayRenderer.material.color = t.color;
            }
            displayCube.position = basePosition;
            displayCube.rotation = Quaternion.Euler(20f, Time.unscaledTime * displaySpin, 0f);
            displayCube.localScale = Vector3.one * displaySize;
        }

        fullTimer = Mathf.Max(0f, fullTimer - Time.unscaledDeltaTime);
        string text;
        if (fullTimer > 0f) text = emptyFlash ? emptyDisplayText : fullText;
        else if (hasSelection) text = string.Format(displayTextFormat, clicker.Tiers[selected].displayName, counts[selected]);
        else text = emptyDisplayText;

        displayText.text = text;
        displayText.transform.position = basePosition + cam.transform.right * (hasSelection ? displaySize * 0.9f : 0f);
        displayText.transform.rotation = cam.transform.rotation;
        displayText.alignment = TextAlignmentOptions.Left;
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
                 -(hud.ButtonSize.y + buttonGap));
        bankDock = bank.GetComponent<PixelDockedButton>();
        hoseDock = hoseButton.GetComponent<PixelDockedButton>();

        bank.onClick.AddListener(ToggleWindow);
        hoseButton.onClick.AddListener(() => SetHose(!HoseOn));
        RefreshHoseButton();

        // The window, just right of the tab.
        windowObject = new GameObject("Bank Window", typeof(RectTransform), typeof(Image));
        windowObject.transform.SetParent(canvasRoot.transform, false);
        windowObject.GetComponent<Image>().color = panelColor;
        RectTransform wr = windowObject.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0f, 0.5f);
        wr.sizeDelta = windowSize;
        wr.anchoredPosition = new Vector2(hud.ButtonSize.x + gapToButton + hud.SideMargin * 0.35f, 0f);

        float y = 16f;
        TMP_Text title = PixelUIKit.CreateText(font, windowObject.transform, "Title", windowTitle, titleFontSize,
                                               TextAlignmentOptions.Center, FontStyles.Bold, textColor);
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

    /// <summary>Both buttons stay out while the window is open or the mouse is near either of them.</summary>
    private bool KeepButtonsOut()
    {
        if (windowObject != null && windowObject.activeSelf) return true;
        return (bankDock != null && bankDock.PointerNear()) || (hoseDock != null && hoseDock.PointerNear());
    }

    private void RefreshHoseButton()
    {
        if (hoseButtonLabel == null) return;
        hoseButtonLabel.text = HoseOn ? hoseOnText : string.Format(hoseOffText, hoseKey);
        hoseButtonImage.color = HoseOn ? hoseButtonOnColor : hoseButtonColor;
    }

    private void ToggleWindow()
    {
        if (windowObject.activeSelf) CloseWindow();
        else
        {
            windowObject.SetActive(true);
            refreshTimer = 0f;
            RefreshWindow();
        }
    }

    private void CloseWindow() => windowObject.SetActive(false);

    private void RefreshWindow()
    {
        capacityLabel.text = string.Format(capacityFormat, PixelClicker.FormatNumber(Total), PixelClicker.FormatNumber(capacity));

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
