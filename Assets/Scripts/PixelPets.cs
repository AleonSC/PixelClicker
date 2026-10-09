using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PixelInput;

/// <summary>
/// Pets. Every click on a pixel has a very small chance to give you a "pet" version of that pixel type (one pet per type).
/// Finding one freezes the game behind a popup. The pet is a cube the size of the main pixel that roams the floor by itself,
/// hopping, rolling and bouncing around. Each pet has a tick box in the Toggles window's "Pets" tab. Pets are saved with the game.
///
/// Added by PixelClicker.Awake.
/// </summary>
public class PixelPets : MonoBehaviour
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // keeps static state clean when Enter Play Mode skips the domain reload
    {
        Instance = null;
        PopupOpen = false;
        HoveringPet = false;
    }

    [Header("References")]
    [Tooltip("The PixelClicker whose clicks can give pets. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Fallback font, used only when the PixelClicker's 'UI Font' is empty.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Finding pets")]
    [Tooltip("Master switch: can clicks give pets at all? Pets you already have keep roaming.")]
    [SerializeField] private bool petsEnabled = true;

    [Range(0f, 0.05f)]
    [Tooltip("Chance that a click gives a pet (0.0003 = about 1 in 3300). Only pixel types you don't have a pet for yet roll.")]
    [SerializeField] private float chancePerClick = 0.0003f;

    [Range(0f, 1f)]
    [Tooltip("Auto clicker clicks roll at this fraction of the normal chance (1 = the same, 0 = never).")]
    [SerializeField] private float autoClickChanceFactor = 0.5f;

    [Header("Roaming")]
    [Min(0.1f)]
    [Tooltip("Mass of a pet (heavier pets shove old pixels around more).")]
    [SerializeField] private float petMass = 2f;

    [Range(0f, 1.5f)]
    [Tooltip("How bouncy a pet is.")]
    [SerializeField] private float bounciness = 0.6f;

    [Range(0f, 1f)]
    [Tooltip("Friction of a pet against the floor (lower = rolls on, higher = grips).")]
    [SerializeField] private float friction = 0.25f;

    [Tooltip("Seconds between a pet's hops (a random time between the two numbers).")]
    [SerializeField] private Vector2 hopInterval = new Vector2(0.8f, 2.4f);

    [Min(0f)]
    [Tooltip("How fast a hop sends the pet sideways (world units per second).")]
    [SerializeField] private float rollSpeed = 3.2f;

    [Min(0f)]
    [Tooltip("How fast a hop sends the pet upwards (world units per second).")]
    [SerializeField] private float hopSpeed = 3.4f;

    [Min(0f)]
    [Tooltip("Extra spin added to a hop, in the direction it rolls (radians per second).")]
    [SerializeField] private float spinSpeed = 5f;

    [Min(0.5f)]
    [Tooltip("A pet drifts back towards the cube when it is further than this many pixel-widths from it.")]
    [SerializeField] private float roamRadius = 7f;

    [Min(1f)]
    [Tooltip("Fastest a pet may move sideways (world units per second).")]
    [SerializeField] private float maxSpeed = 7f;

    [Min(0f)]
    [Tooltip("How quickly a pet comes to a stop while the mouse is over it (higher = stops at once).")]
    [SerializeField] private float hoverStopDamping = 12f;

    [Header("Picking up and throwing")]
    [Min(1f)]
    [Tooltip("How tightly a held pet follows the mouse.")]
    [SerializeField] private float followSharpness = 18f;

    [Min(1f)]
    [Tooltip("Fastest a held pet moves (world units per second).")]
    [SerializeField] private float maxFollowSpeed = 40f;

    [Tooltip("How much of the mouse's speed a pet keeps when you let go (0 = it just drops).")]
    [SerializeField] private float throwStrength = 1f;

    [Min(0f)]
    [Tooltip("Fastest a thrown pet can leave your hand (world units per second).")]
    [SerializeField] private float maxThrowSpeed = 25f;

    [Header("Ghost pet")]
    [Min(0f)]
    [Tooltip("How high the Ghost pet hovers above the floor, in pixel-widths.")]
    [SerializeField] private float ghostHoverHeight = 1.6f;

    [Min(0f)]
    [Tooltip("How fast the Ghost pet drifts (world units per second).")]
    [SerializeField] private float ghostSpeed = 1.2f;

    [Header("Vacuum pet")]
    [Min(0.5f)]
    [Tooltip("How far the Vacuum Pet reaches for old pixels, in pixel-widths.")]
    [SerializeField] private float vacuumRadius = 2.5f;

    [Min(0.02f)]
    [Tooltip("Seconds between the old pixels it sucks up (one at a time).")]
    [SerializeField] private float vacuumInterval = 0.15f;

    [Min(0.05f)]
    [Tooltip("Seconds an old pixel takes to fly into the Vacuum Pet while shrinking.")]
    [SerializeField] private float vacuumFlySeconds = 0.25f;

    [Min(0.1f)]
    [Tooltip("Size of the number above the Vacuum Pet (how many old pixels it has sucked up in total).")]
    [SerializeField] private float counterTextSize = 3f;

    [Min(0f)]
    [Tooltip("How far above the Vacuum Pet its number floats, in pixel-widths.")]
    [SerializeField] private float counterHeight = 1f;

    [Tooltip("Colour of the number above the Vacuum Pet.")]
    [SerializeField] private Color counterColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Glass pet")]
    [Min(0f)]
    [Tooltip("Random breaking: the Glass Pet's chance per second of shattering while it moves at 'Glass Break Speed' (faster = likelier). 0 = it never breaks.")]
    [SerializeField] private float glassBreakChance = 0.12f;

    [Min(0.1f)]
    [Tooltip("The speed (world units per second) at which the break chance applies in full.")]
    [SerializeField] private float glassBreakSpeed = 3f;

    [Min(0.1f)]
    [Tooltip("Seconds the shards fly about before they reverse back together.")]
    [SerializeField] private float glassHoldSeconds = 1.4f;

    [Min(0.05f)]
    [Tooltip("Seconds the shards take to fly back and re-form the pet.")]
    [SerializeField] private float glassReformSeconds = 0.6f;

    [Min(0f)]
    [Tooltip("Seconds after re-forming before the Glass Pet can break again.")]
    [SerializeField] private float glassCooldown = 3f;

    [Range(3, 40)]
    [Tooltip("How many shards the Glass Pet breaks into.")]
    [SerializeField] private int glassShards = 14;

    [Min(0f)]
    [Tooltip("How fast the shards fly apart.")]
    [SerializeField] private float glassShardSpeed = 3.5f;

    [Range(0.05f, 1f)]
    [Tooltip("Size of a shard compared to the pet.")]
    [SerializeField] private float glassShardSize = 0.4f;

    [Tooltip("Sound played when the Glass Pet breaks (give the id clips in PixelAudio; the reform plays the same id + _reform).")]
    [SerializeField] private string glassSoundId = "glass_shatter";

    [Header("Popup")]
    [Tooltip("Popup title.")]
    [SerializeField] private string popupTitle = "Pet Found!";

    [TextArea(2, 5)]
    [Tooltip("Popup text. {0} = the pet's name.")]
    [SerializeField] private string popupText = "A {0} has joined you!\nIt roams the floor by itself. You can switch it on or off in the Toggles window, under Pets.";

    [Tooltip("Popup button text.")]
    [SerializeField] private string popupButton = "Awesome!";

    [Tooltip("Event log line when a pet is found. {0} = the pet's name.")]
    [SerializeField] private string foundFormat = "Pet found: {0}!";

    [Header("Popup look")]
    [Tooltip("Colour of the popup box.")]
    [SerializeField] private Color panelColor = new Color(0.12f, 0.12f, 0.16f, 0.98f);

    [Tooltip("Colour of the dark screen behind the popup.")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.7f);

    [Tooltip("Colour of the popup title.")]
    [SerializeField] private Color titleColor = new Color(1f, 0.85f, 0.25f, 1f);

    [Tooltip("Colour of the popup button.")]
    [SerializeField] private Color buttonColor = new Color(0.2f, 0.65f, 0.35f, 1f);

    [Tooltip("Sorting order of the popup's canvas (above every other window, including the dev tools at 700).")]
    [SerializeField] private int popupSortingOrder = 750;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    /// <summary>The one PixelPets in the scene.</summary>
    public static PixelPets Instance { get; private set; }

    /// <summary>True while the "pet found" popup freezes the game.</summary>
    public static bool PopupOpen { get; private set; }

    private class Pet
    {
        public PixelClicker.PixelType type;
        public bool off;
        public GameObject body;
        public float hopTimer;
        public bool ghost;       // the Ghost pet only hovers
        public float glassNextBreak;  // Glass Pet: no breaking before this time
        public bool broken;           // Glass Pet: shards are out right now
        public float suckTimer;       // Vacuum Pet
        public GameObject counterGo;  // Vacuum Pet's floating number
        public TextMeshPro counterText;
        public double shownCount = -1d;
        public Vector3 target;   // where a hovering pet is drifting to
        public float targetTimer, phase;
    }

    private readonly List<Pet> pets = new List<Pet>();
    private readonly Queue<PixelClicker.PixelType> waiting = new Queue<PixelClicker.PixelType>();
    private GameObject popupRoot;
    private float savedTimeScale = 1f;

    /// <summary>How many pets you have.</summary>
    public int OwnedCount => pets.Count;

    public bool IsOwned(PixelClicker.PixelType type) => pets.Exists(p => p.type == type);

    /// <summary>A pet's tick-box state in the Toggles window (true = roaming).</summary>
    public bool IsOn(PixelClicker.PixelType type)
    {
        Pet p = pets.Find(x => x.type == type);
        return p != null && !p.off;
    }

    public void SetOn(PixelClicker.PixelType type, bool on)
    {
        Pet p = pets.Find(x => x.type == type);
        if (p == null) return;
        p.off = !on;
        Refresh(p);
    }

    /// <summary>The owned pets as (pixel type, display name), in the order they were found.</summary>
    public List<KeyValuePair<PixelClicker.PixelType, string>> Owned()
    {
        List<KeyValuePair<PixelClicker.PixelType, string>> list = new List<KeyValuePair<PixelClicker.PixelType, string>>();
        foreach (Pet p in pets) list.Add(new KeyValuePair<PixelClicker.PixelType, string>(p.type, PetName(p.type)));
        return list;
    }

    private void Awake()
    {
        Instance = this;
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker != null && clicker.UIFont != null) font = clicker.UIFont;
    }

    private void OnEnable()
    {
        if (clicker == null) clicker = PixelFind.First<PixelClicker>();
        if (clicker != null) clicker.PixelCollected += OnCollected;
    }

    private void OnDisable()
    {
        if (held != null) EndHold(false);
        if (blockingClicks) { blockingClicks = false; PixelClicker.ExternalClickBlock = PixelBank.HoseOn; }
        HoveringPet = false;
        if (clicker != null) clicker.PixelCollected -= OnCollected;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (PopupOpen) ClosePopup();
        foreach (Pet p in pets) { if (p.body != null) Destroy(p.body); DestroyCounter(p); }
        if (popupRoot != null) Destroy(popupRoot);
    }

    // ------------------------------------------------------------------
    // Finding pets
    // ------------------------------------------------------------------

    private void OnCollected(int tierIndex, double amount, bool automatic)
    {
        if (!petsEnabled || PopupOpen || clicker == null || !clicker.IsValidTierIndex(tierIndex)) return;
        PixelClicker.PixelType type = clicker.Tiers[tierIndex].type;
        if (IsOwned(type) || waiting.Contains(type)) return;

        float chance = chancePerClick * (automatic ? autoClickChanceFactor : 1f);
        if (chance > 0f && Random.value < chance) Award(type);
    }

    /// <summary>Gives a pet (dev tools use this too). 'popup' false adds it quietly.</summary>
    public void Award(PixelClicker.PixelType type, bool popup = true)
    {
        if (IsOwned(type)) return;
        if (!popup)
        {
            AddPet(type, false);
            return;
        }
        if (PopupOpen) { if (!waiting.Contains(type)) waiting.Enqueue(type); return; }
        ShowPopup(type);
    }

    private void AddPet(PixelClicker.PixelType type, bool off)
    {
        Pet p = new Pet { type = type, off = off, hopTimer = Random.Range(0.3f, 1f), ghost = type == PixelClicker.PixelType.Ghost, phase = Random.Range(0f, 6.28f) };
        pets.Add(p);
        PixelStats.Count("pets.found");
        Refresh(p);
    }

    private string PetName(PixelClicker.PixelType type)
    {
        int tier = TierOf(type);
        return (tier >= 0 ? clicker.Tiers[tier].displayName : type.ToString()) + " Pet";
    }

    private int TierOf(PixelClicker.PixelType type)
    {
        if (clicker == null) return -1;
        for (int i = 0; i < clicker.Tiers.Length; i++) if (clicker.Tiers[i].type == type) return i;
        return -1;
    }

    // ------------------------------------------------------------------
    // The popup (freezes the game until it is closed)
    // ------------------------------------------------------------------

    private PixelClicker.PixelType popupType;

    private void ShowPopup(PixelClicker.PixelType type)
    {
        popupType = type;
        PopupOpen = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        PixelAudio.Play("achievement");

        popupRoot = PixelUIKit.CreateCanvas("PixelPets Popup Canvas", popupSortingOrder, referenceResolution, true);
        PixelUIKit.EnsureEventSystem();

        GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(popupRoot.transform, false);
        dim.GetComponent<Image>().color = dimColor;
        PixelUIKit.Stretch(dim.GetComponent<RectTransform>());

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        box.GetComponent<Image>().color = panelColor;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
        br.sizeDelta = new Vector2(760f, 660f);

        TMP_Text title = PixelUIKit.CreateText(font, box.transform, "Title", popupTitle, 64f, TextAlignmentOptions.Center,
                                               FontStyles.Bold, titleColor);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(0f, 90f);
        tr.anchoredPosition = new Vector2(0f, -24f);

        // A big spinning cube in the pet's colour.
        int tier = TierOf(type);
        Color color = tier >= 0 ? clicker.Tiers[tier].UIColor : Color.white;
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(PixelCubeIcon));
        iconGo.transform.SetParent(box.transform, false);
        PixelCubeIcon icon = iconGo.GetComponent<PixelCubeIcon>();
        icon.color = color;
        icon.fill = 0.8f;
        RectTransform ir = iconGo.GetComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 1f);
        ir.sizeDelta = new Vector2(190f, 190f);
        ir.anchoredPosition = new Vector2(0f, -120f);

        TMP_Text message = PixelUIKit.CreateText(font, box.transform, "Message", string.Format(popupText, PetName(type)), 32f,
                                                 TextAlignmentOptions.Top, FontStyles.Normal, Color.white);
        RectTransform mr = message.rectTransform;
        mr.anchorMin = new Vector2(0f, 1f); mr.anchorMax = new Vector2(1f, 1f); mr.pivot = new Vector2(0.5f, 1f);
        mr.sizeDelta = new Vector2(-80f, 190f);
        mr.anchoredPosition = new Vector2(0f, -320f);
        message.enableAutoSizing = true; // a long text shrinks to stay clear of the button
        message.fontSizeMax = 32f;
        message.fontSizeMin = 18f;

        Button ok = PixelUIKit.CreateButton(font, box.transform, "Button", popupButton, new Vector2(300f, 76f), buttonColor, Color.white, 36f);
        RectTransform or = ok.GetComponent<RectTransform>();
        or.anchorMin = or.anchorMax = or.pivot = new Vector2(0.5f, 0f);
        or.anchoredPosition = new Vector2(0f, 34f);
        ok.onClick.AddListener(ClosePopup);

        PixelWindows.Register(this, 150, () => PopupOpen, ClosePopup); // Escape closes it too
    }

    private void ClosePopup()
    {
        if (!PopupOpen) return;
        PopupOpen = false;
        PixelWindows.Unregister(this);
        if (popupRoot != null) Destroy(popupRoot);
        popupRoot = null;
        Time.timeScale = savedTimeScale;

        if (!IsOwned(popupType))
        {
            AddPet(popupType, false);
            PixelHints.Announce(string.Format(foundFormat, PetName(popupType)));
        }
        if (waiting.Count > 0) ShowPopup(waiting.Dequeue());
    }

    // ------------------------------------------------------------------
    // The pet itself
    // ------------------------------------------------------------------

    private void Refresh(Pet p)
    {
        bool want = !p.off;
        if (want && p.body == null) Spawn(p);
        else if (!want && p.body != null) { Destroy(p.body); p.body = null; DestroyCounter(p); }
    }

    private void Spawn(Pet p)
    {
        int tier = TierOf(p.type);
        if (clicker == null || tier < 0 || clicker.PixelTransform == null) return;

        float size = clicker.PixelBaseSize;
        GameObject go = new GameObject("Pet " + p.type);
        go.transform.position = clicker.PixelTransform.position + new Vector3(Random.Range(-2f, 2f), size * 3f, Random.Range(-2f, 2f));
        go.transform.rotation = Random.rotation;

        GameObject model = clicker.CreateDisplayPixel(tier, go.transform, size);
        if (model == null) { Destroy(go); return; }
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.size = Vector3.one * size;
#if UNITY_6000_0_OR_NEWER
        PhysicsMaterial material = new PhysicsMaterial("Pet")
        {
            bounciness = bounciness, dynamicFriction = friction, staticFriction = friction,
            bounceCombine = PhysicsMaterialCombine.Maximum,
        };
#else
        PhysicMaterial material = new PhysicMaterial("Pet")
        {
            bounciness = bounciness, dynamicFriction = friction, staticFriction = friction,
            bounceCombine = PhysicMaterialCombine.Maximum,
        };
#endif
        box.sharedMaterial = material;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = petMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
#if UNITY_6000_0_OR_NEWER
        rb.angularDamping = 0.3f;
        rb.linearDamping = 0.05f;
#else
        rb.angularDrag = 0.3f;
        rb.drag = 0.05f;
#endif
        if (p.ghost)
        {
            rb.useGravity = false;     // the Ghost pet hovers: no gravity, and its collider is a trigger so it never shoves anything
            box.isTrigger = true;
            p.targetTimer = 0f;
        }
        if (p.type == PixelClicker.PixelType.Vacuum) BuildCounter(p, size);
        p.body = go;
        p.hopTimer = Random.Range(hopInterval.x, hopInterval.y);
    }

    private void Update()
    {
        if (PopupOpen || Time.timeScale <= 0f) return;
        bool hide = PixelMinigame.TakeoverActive; // Snake, Sorting Race and Breakout have the floor to themselves
        if (hide && held != null) EndHold(false);
        UpdatePointer(hide);
        foreach (Pet p in pets)
        {
            if (p.body == null) continue;
            if (p.body.activeSelf == hide) p.body.SetActive(!hide);
            if (hide) continue;
            if (p.ghost) RoamGhost(p, p == hovered || p == held);
            else Roam(p, p == hovered || p == held);
            if (p.type == PixelClicker.PixelType.Vacuum) SuckAround(p);
            else if (p.type == PixelClicker.PixelType.Glass && p != held) MaybeBreak(p);
        }
    }

    // ------------------------------------------------------------------
    // The mouse: hovering a pet stops it, clicking picks it up, letting go throws it
    // ------------------------------------------------------------------

    private Pet hovered, held;
    private Plane dragPlane;
    private Vector3 dragOffset;
    private Camera dragCamera;
    private bool blockingClicks;

    /// <summary>True while the mouse is over a pet or holding one (Pixel Grabbing and the cube leave the click alone).</summary>
    public static bool HoveringPet { get; private set; }

    private void UpdatePointer(bool hide)
    {
        bool allowed = !hide && !PixelPauseMenu.IsPaused && !PixelBank.HoseOn && !PixelClicker.GodMode && !PixelPlacingBlocked();
        if (held != null)
        {
            if (!LeftHeld() || !allowed) EndHold(true);
            hovered = held;
        }
        else
        {
            hovered = allowed && !PointerOverUI() ? PetUnderPointer() : null;
            if (hovered != null && LeftPressed()) BeginHold(hovered);
        }

        HoveringPet = hovered != null || held != null;
        if (HoveringPet && !blockingClicks) { blockingClicks = true; PixelClicker.ExternalClickBlock = true; }
        else if (!HoveringPet && blockingClicks) { blockingClicks = false; PixelClicker.ExternalClickBlock = PixelBank.HoseOn; }
    }

    private static bool PixelPlacingBlocked() => PixelMinigame.TakeoverActive;

    private Pet PetUnderPointer()
    {
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return null;
        Ray ray = cam.ScreenPointToRay(PointerPosition());
        Pet best = null;
        float bestDistance = float.PositiveInfinity;
        foreach (Pet p in pets)
        {
            if (p.body == null || !p.body.activeInHierarchy) continue;
            Collider c = p.body.GetComponent<Collider>();
            if (c != null && c.Raycast(ray, out RaycastHit hit, 1000f) && hit.distance < bestDistance)
            {
                best = p;
                bestDistance = hit.distance;
            }
        }
        return best;
    }

    private void BeginHold(Pet p)
    {
        Rigidbody rb = p.body.GetComponent<Rigidbody>();
        dragCamera = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (rb == null || dragCamera == null) return;
        held = p;
        rb.useGravity = false;
        dragPlane = new Plane(-dragCamera.transform.forward, rb.position);
        Ray ray = dragCamera.ScreenPointToRay(PointerPosition());
        dragOffset = dragPlane.Raycast(ray, out float enter) ? rb.position - ray.GetPoint(enter) : Vector3.zero;
        PixelAudio.Play("grab");
    }

    private void EndHold(bool throwIt)
    {
        Pet p = held;
        held = null;
        if (p == null || p.body == null) return;
        Rigidbody rb = p.body.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.useGravity = !p.ghost;
        if (throwIt)
        {
            Vector3 v = GetVelocity(rb) * throwStrength * PixelTimeStop.SlowFactor; // back to what the mouse did
            if (v.magnitude > maxThrowSpeed) v = v.normalized * maxThrowSpeed;
            SetVelocity(rb, v);
            PixelAudio.Play("drop");
        }
        p.hopTimer = Random.Range(hopInterval.x, hopInterval.y);
    }

    private void FixedUpdate()
    {
        if (held == null || held.body == null || dragCamera == null) return;
        Rigidbody rb = held.body.GetComponent<Rigidbody>();
        if (rb == null) return;
        Ray ray = dragCamera.ScreenPointToRay(PointerPosition());
        if (!dragPlane.Raycast(ray, out float enter)) return;
        Vector3 target = ray.GetPoint(enter) + dragOffset;

        float slow = PixelTimeStop.SlowFactor; // velocities are per slowed second while time is slowed
        Vector3 velocity = (target - rb.position) * followSharpness / slow;
        if (velocity.magnitude > maxFollowSpeed / slow) velocity = velocity.normalized * (maxFollowSpeed / slow);
        SetVelocity(rb, velocity);
        rb.angularVelocity *= 0.9f;
    }

    private static Vector3 GetVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private static void SetVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    // ------------------------------------------------------------------
    // Roaming
    // ------------------------------------------------------------------

    private void Roam(Pet p, bool stopped)
    {
        Rigidbody rb = p.body.GetComponent<Rigidbody>();
        if (rb == null || clicker.PixelTransform == null) return;
        if (held == p) return; // being carried
        float size = clicker.PixelBaseSize;
        Vector3 pos = rb.position;
        Vector3 cube = clicker.PixelTransform.position;

        // Fell out of the world: back above the cube.
        if (pos.y < cube.y - 25f)
        {
            rb.position = cube + Vector3.up * size * 3f;
            SetVelocity(rb, Vector3.zero);
            return;
        }

        Vector3 v = GetVelocity(rb);

        // The mouse is over it: it settles down and stands still (gravity still works).
        if (stopped)
        {
            float k = Mathf.Exp(-hoverStopDamping * Time.deltaTime);
            SetVelocity(rb, new Vector3(v.x * k, v.y, v.z * k));
            rb.angularVelocity *= k;
            p.hopTimer = Mathf.Max(p.hopTimer, 0.4f);
            return;
        }

        // Cap the sideways speed.
        Vector2 flat = new Vector2(v.x, v.z);
        if (flat.magnitude > maxSpeed)
        {
            flat = flat.normalized * maxSpeed;
            SetVelocity(rb, new Vector3(flat.x, v.y, flat.y));
        }

        p.hopTimer -= Time.deltaTime;
        if (p.hopTimer > 0f) return;
        // Only hop from the ground.
        if (!Physics.Raycast(pos, Vector3.down, size * 0.8f, ~0, QueryTriggerInteraction.Ignore)) { p.hopTimer = 0.15f; return; }
        p.hopTimer = Random.Range(Mathf.Min(hopInterval.x, hopInterval.y), Mathf.Max(hopInterval.x, hopInterval.y));

        Vector2 circle = Random.insideUnitCircle.normalized;
        Vector3 dir = new Vector3(circle.x, 0f, circle.y);

        // Too far from the cube, or about to leave the screen: head back.
        Vector3 toCube = cube - pos;
        toCube.y = 0f;
        bool outside = OutsideView(pos);
        if (toCube.magnitude > roamRadius * size || outside)
            dir = toCube.sqrMagnitude > 0.0001f ? Vector3.Slerp(dir, toCube.normalized, outside ? 0.95f : 0.7f).normalized : dir;

        rb.AddForce(dir * (rb.mass * rollSpeed) + Vector3.up * (rb.mass * hopSpeed), ForceMode.Impulse);
        rb.AddTorque(Vector3.Cross(Vector3.up, dir) * spinSpeed, ForceMode.VelocityChange);
    }

    private bool OutsideView(Vector3 pos)
    {
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam == null) return false;
        Vector3 vp = cam.WorldToViewportPoint(pos);
        float bars = PixelHud.Instance != null
            ? PixelHud.Instance.RawBarHeight * Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f) / Mathf.Max(1, Screen.height) : 0f;
        return vp.z <= 0f || vp.x < 0.08f || vp.x > 0.92f || vp.y < bars + 0.06f || vp.y > 0.94f;
    }

    // The Ghost pet never touches the ground: it hovers, drifts and bobs around the floor.
    private void RoamGhost(Pet p, bool stopped)
    {
        Rigidbody rb = p.body.GetComponent<Rigidbody>();
        if (rb == null || clicker.PixelTransform == null) return;
        if (held == p) return;
        float size = clicker.PixelBaseSize;
        Vector3 pos = rb.position;
        Vector3 cube = clicker.PixelTransform.position;
        Vector3 v = GetVelocity(rb);

        if (stopped)
        {
            float k = Mathf.Exp(-hoverStopDamping * Time.deltaTime);
            SetVelocity(rb, v * k);
            rb.angularVelocity *= k;
            return;
        }

        p.targetTimer -= Time.deltaTime;
        if (p.targetTimer <= 0f || (p.target - pos).magnitude < size * 0.4f || OutsideView(pos))
        {
            p.targetTimer = Random.Range(3f, 6f);
            Vector2 c = Random.insideUnitCircle * roamRadius * size * 0.8f;
            Vector3 spot = new Vector3(cube.x + c.x, cube.y + 12f, cube.z + c.y);
            float floorY = cube.y - size; // fall back to just under the cube when nothing is below
            if (Physics.Raycast(spot, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            spot.y = floorY + size * (ghostHoverHeight + Random.Range(0f, 0.8f));
            p.target = spot;
        }

        Vector3 to = p.target - pos;
        Vector3 desired = to.normalized * Mathf.Min(ghostSpeed, to.magnitude * 1.2f);
        desired.y += Mathf.Sin(Time.time * 2f + p.phase) * 0.25f; // bobbing
        SetVelocity(rb, Vector3.Lerp(v, desired, 1f - Mathf.Exp(-2f * Time.deltaTime))); // a throw fades into the drift
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, new Vector3(Mathf.Sin(Time.time + p.phase) * 0.3f, 0.4f, Mathf.Cos(Time.time * 0.7f + p.phase) * 0.3f),
                                          1f - Mathf.Exp(-1.5f * Time.deltaTime));
    }

    // ------------------------------------------------------------------
    // The Glass Pet: shatters now and then while it moves, and the shards reverse back together.
    // ------------------------------------------------------------------

    private void MaybeBreak(Pet p)
    {
        if (p.broken || glassBreakChance <= 0f || Time.time < p.glassNextBreak) return;
        Rigidbody rb = p.body.GetComponent<Rigidbody>();
        if (rb == null) return;
        float speed = GetVelocity(rb).magnitude;
        if (speed < 0.6f) return; // standing still: nothing to break it
        float k = Mathf.Min(speed / glassBreakSpeed, 2f);
        if (Random.value < glassBreakChance * k * Time.deltaTime) StartCoroutine(BreakAndReform(p));
    }

    private System.Collections.IEnumerator BreakAndReform(Pet p)
    {
        GameObject body = p.body;
        MeshRenderer source = body != null ? body.GetComponentInChildren<MeshRenderer>() : null;
        MeshFilter filter = source != null ? source.GetComponent<MeshFilter>() : null;
        if (filter == null) yield break;
        p.broken = true;
        if (!string.IsNullOrEmpty(glassSoundId)) PixelAudio.Play(glassSoundId);

        // Hide the pet (and its glow children); it stays where it is, invisible and solid.
        Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
        bool[] wasOn = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) { wasOn[i] = renderers[i].enabled; renderers[i].enabled = false; }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        source.GetPropertyBlock(block);
        float size = clicker.PixelBaseSize;
        Vector3 origin = body.transform.position;
        List<PetReformShard> shards = new List<PetReformShard>();
        for (int i = 0; i < glassShards; i++)
        {
            GameObject shard = new GameObject("PetGlassShard");
            shard.transform.position = origin + Random.insideUnitSphere * size * 0.35f;
            shard.transform.rotation = Random.rotation;
            shard.transform.localScale = new Vector3(
                size * glassShardSize * Random.Range(0.6f, 1.2f),
                size * glassShardSize * Random.Range(0.12f, 0.3f),
                size * glassShardSize * Random.Range(0.5f, 1f));
            shard.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            MeshRenderer smr = shard.AddComponent<MeshRenderer>();
            smr.sharedMaterial = source.sharedMaterial;
            smr.SetPropertyBlock(block);
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shard.AddComponent<BoxCollider>();
            Rigidbody srb = shard.AddComponent<Rigidbody>();
            srb.mass = 0.05f;
            Vector3 push = Random.onUnitSphere * glassShardSpeed;
            push.y = Mathf.Abs(push.y);
            srb.AddForce(push, ForceMode.VelocityChange);
            srb.AddTorque(Random.onUnitSphere * Random.Range(5f, 15f), ForceMode.VelocityChange);
            PetReformShard reform = shard.AddComponent<PetReformShard>();
            reform.Setup(body.transform);
            shards.Add(reform);
        }

        yield return new WaitForSeconds(glassHoldSeconds);
        if (!string.IsNullOrEmpty(glassSoundId)) PixelAudio.Play(glassSoundId + "_reform");
        foreach (PetReformShard shard in shards) if (shard != null) shard.FlyBack(glassReformSeconds);
        yield return new WaitForSeconds(glassReformSeconds);

        foreach (PetReformShard shard in shards) if (shard != null) Destroy(shard.gameObject);
        if (body != null) // (the pet may have been switched off meanwhile)
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = wasOn[i];
        p.broken = false;
        p.glassNextBreak = Time.time + glassCooldown;
    }

    // ------------------------------------------------------------------
    // The Vacuum Pet: sucks up old pixels near it. They pay nothing: they only count for the stat and the number above it.
    // ------------------------------------------------------------------

    private void BuildCounter(Pet p, float size)
    {
        p.counterGo = new GameObject("Vacuum Pet Counter");
        p.counterGo.transform.localScale = Vector3.one * size;
        p.counterText = p.counterGo.AddComponent<TextMeshPro>();
        p.counterText.fontSize = counterTextSize;
        p.counterText.fontStyle = FontStyles.Bold;
        p.counterText.alignment = TextAlignmentOptions.Center;
        p.counterText.color = counterColor;
        p.counterText.rectTransform.sizeDelta = new Vector2(8f, 2f);
        if (font != null) p.counterText.font = font;
        p.shownCount = -1d;
        p.counterGo.SetActive(false);
    }

    private static void DestroyCounter(Pet p)
    {
        if (p.counterGo != null) Destroy(p.counterGo);
        p.counterGo = null;
        p.counterText = null;
    }

    private void SuckAround(Pet p)
    {
        Rigidbody pet = p.body.GetComponent<Rigidbody>();
        if (pet == null || clicker == null) return;
        p.suckTimer -= Time.deltaTime;
        if (p.suckTimer > 0f) return;
        p.suckTimer = vacuumInterval;

        float reach = vacuumRadius * clicker.PixelBaseSize;
        Rigidbody nearest = null;
        float best = reach * reach;
        System.Collections.Generic.IReadOnlyList<Rigidbody> list = clicker.OldPixels;
        for (int i = 0; i < list.Count; i++)
        {
            Rigidbody b = list[i];
            if (b == null || b.isKinematic) continue;
            OldPixelDespawn d = b.GetComponent<OldPixelDespawn>();
            if (d != null && (d.IsDespawning || d.Held)) continue; // vanishing or in someone's hand
            if (clicker.IsFlyingPixel(b)) continue;
            float sq = (b.position - pet.position).sqrMagnitude;
            if (sq < best) { best = sq; nearest = b; }
        }
        if (nearest == null || !clicker.ReleaseOldPixel(nearest, false)) return; // false: no payout
        PixelStats.Count("pet.vacuum");
        StartCoroutine(FlyIntoPet(nearest, pet.transform));
    }

    private System.Collections.IEnumerator FlyIntoPet(Rigidbody body, Transform target)
    {
        if (body == null) yield break;
        foreach (Collider c in body.GetComponents<Collider>()) c.enabled = false;
        body.isKinematic = true;
        OldPixelDespawn despawn = body.GetComponent<OldPixelDespawn>();
        if (despawn != null) despawn.Held = true;

        Transform t = body.transform;
        Vector3 startScale = t.localScale;
        Vector3 start = t.position;
        float elapsed = 0f;
        while (elapsed < vacuumFlySeconds && t != null)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / vacuumFlySeconds);
            Vector3 to = target != null ? target.position : start;
            t.position = Vector3.Lerp(start, to, k * k);
            t.localScale = startScale * Mathf.Lerp(1f, 0.05f, k * k);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    private void LateUpdate()
    {
        Camera cam = clicker != null ? (clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main) : null;
        foreach (Pet p in pets)
        {
            if (p.counterGo == null) continue;
            double count = PixelStats.Total("pet.vacuum");
            bool show = p.body != null && p.body.activeSelf && count > 0d && cam != null;
            if (p.counterGo.activeSelf != show) p.counterGo.SetActive(show);
            if (!show) continue;
            if (count != p.shownCount)
            {
                p.shownCount = count;
                p.counterText.text = PixelClicker.FormatNumber(count);
            }
            float size = clicker.PixelBaseSize;
            p.counterGo.transform.position = p.body.transform.position + Vector3.up * (size * (0.5f + counterHeight));
            p.counterGo.transform.rotation = cam.transform.rotation;
        }
    }

    // ------------------------------------------------------------------
    // Saving
    // ------------------------------------------------------------------

    /// <summary>The pets for the save file: one "type:off" string per pet.</summary>
    public string[] Export()
    {
        string[] list = new string[pets.Count];
        for (int i = 0; i < list.Length; i++) list[i] = (int)pets[i].type + ":" + (pets[i].off ? 1 : 0);
        return list;
    }

    /// <summary>Restores the pets from a save (null = none).</summary>
    public void Import(string[] list)
    {
        foreach (Pet p in pets) { if (p.body != null) Destroy(p.body); DestroyCounter(p); }
        pets.Clear();
        waiting.Clear();
        if (list == null) return;
        foreach (string entry in list)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            string[] parts = entry.Split(':');
            if (parts.Length < 1 || !int.TryParse(parts[0], out int t) || !System.Enum.IsDefined(typeof(PixelClicker.PixelType), t)) continue;
            PixelClicker.PixelType type = (PixelClicker.PixelType)t;
            if (IsOwned(type)) continue;
            Pet p = new Pet { type = type, off = parts.Length > 1 && parts[1] == "1", hopTimer = Random.Range(0.3f, 1f), ghost = type == PixelClicker.PixelType.Ghost, phase = Random.Range(0f, 6.28f) };
            pets.Add(p);
            Refresh(p);
        }
    }
}

/// <summary>
/// A shard of the Glass Pet: flies about with physics, then (FlyBack) is pulled smoothly back into the pet, turning and
/// shrinking into it. If the pet disappears meanwhile (switched off), the shard shrinks away.
/// </summary>
public class PetReformShard : MonoBehaviour
{
    private Transform target;
    private bool returning;
    private float seconds = 0.6f, age, orphanAge;
    private Vector3 startPos, startScale;
    private Quaternion startRot;

    public void Setup(Transform pet)
    {
        target = pet;
        startScale = transform.localScale;
    }

    public void FlyBack(float flySeconds)
    {
        returning = true;
        seconds = Mathf.Max(0.05f, flySeconds);
        age = 0f;
        startPos = transform.position;
        startRot = transform.rotation;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        Collider c = GetComponent<Collider>();
        if (c != null) c.enabled = false;
    }

    private void Update()
    {
        if (target == null)
        {
            orphanAge += Time.deltaTime;
            float shrink = 1f - orphanAge / 0.3f;
            if (shrink <= 0f) { Destroy(gameObject); return; }
            transform.localScale = startScale * shrink;
            return;
        }
        if (!returning) return;

        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / seconds);
        float e = k * k * (3f - 2f * k); // smooth in and out
        transform.position = Vector3.Lerp(startPos, target.position, e);
        transform.rotation = Quaternion.Slerp(startRot, target.rotation, e);
        transform.localScale = Vector3.Lerp(startScale, startScale * 0.35f, e);
    }
}
