using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        if (clicker != null) clicker.PixelCollected -= OnCollected;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (PopupOpen) ClosePopup();
        foreach (Pet p in pets) if (p.body != null) Destroy(p.body);
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
        Pet p = new Pet { type = type, off = off, hopTimer = Random.Range(0.3f, 1f) };
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
        br.sizeDelta = new Vector2(760f, 560f);

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
        mr.sizeDelta = new Vector2(-80f, 150f);
        mr.anchoredPosition = new Vector2(0f, -330f);

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
        else if (!want && p.body != null) { Destroy(p.body); p.body = null; }
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
        p.body = go;
        p.hopTimer = Random.Range(hopInterval.x, hopInterval.y);
    }

    private void Update()
    {
        if (PopupOpen || Time.timeScale <= 0f) return;
        bool hide = PixelMinigame.TakeoverActive; // Snake, Sorting Race and Breakout have the floor to themselves
        foreach (Pet p in pets)
        {
            if (p.body == null) continue;
            if (p.body.activeSelf == hide) p.body.SetActive(!hide);
            if (hide) continue;
            Roam(p);
        }
    }

    private void Roam(Pet p)
    {
        Rigidbody rb = p.body.GetComponent<Rigidbody>();
        if (rb == null || clicker.PixelTransform == null) return;
        float size = clicker.PixelBaseSize;
        Vector3 pos = rb.position;
        Vector3 cube = clicker.PixelTransform.position;

        // Fell out of the world: back above the cube.
        if (pos.y < cube.y - 25f)
        {
            rb.position = cube + Vector3.up * size * 3f;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            return;
        }

        // Cap the sideways speed.
#if UNITY_6000_0_OR_NEWER
        Vector3 v = rb.linearVelocity;
#else
        Vector3 v = rb.velocity;
#endif
        Vector2 flat = new Vector2(v.x, v.z);
        if (flat.magnitude > maxSpeed)
        {
            flat = flat.normalized * maxSpeed;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.y);
#else
            rb.velocity = new Vector3(flat.x, v.y, flat.y);
#endif
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
        bool outside = false;
        Camera cam = clicker.TargetCamera != null ? clicker.TargetCamera : Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(pos);
            float bars = PixelHud.Instance != null
                ? PixelHud.Instance.RawBarHeight * Mathf.Lerp(Screen.width / 1920f, Screen.height / 1080f, 0.5f) / Mathf.Max(1, Screen.height) : 0f;
            outside = vp.z <= 0f || vp.x < 0.08f || vp.x > 0.92f || vp.y < bars + 0.06f || vp.y > 0.94f;
        }
        if (toCube.magnitude > roamRadius * size || outside)
            dir = toCube.sqrMagnitude > 0.0001f ? Vector3.Slerp(dir, toCube.normalized, outside ? 0.95f : 0.7f).normalized : dir;

        rb.AddForce(dir * (rb.mass * rollSpeed) + Vector3.up * (rb.mass * hopSpeed), ForceMode.Impulse);
        rb.AddTorque(Vector3.Cross(Vector3.up, dir) * spinSpeed, ForceMode.VelocityChange);
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
        foreach (Pet p in pets) if (p.body != null) Destroy(p.body);
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
            Pet p = new Pet { type = type, off = parts.Length > 1 && parts[1] == "1", hopTimer = Random.Range(0.3f, 1f) };
            pets.Add(p);
            Refresh(p);
        }
    }
}
