using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vendors wandering the main menu (added to the title screen by <see cref="PixelTitleScreen"/>): while the small toggle at the bottom left is ON,
/// the game's vendors (Cubie, the Wizard, Tinkerer, Farmer, Entrepreneur and the Egg and Bean cats) walk in from either side along the bottom
/// of the screen, wander back and forth with a pause here and there, and wander off again; now and then one stays for a good while. The toggle is
/// invisible until the mouse is over it. Drawn with each vendor's own block art (<c>BuildWalker</c>); unscaled time.
/// </summary>
public class PixelTitleWanderers : MonoBehaviour
{
    [Header("Wanderers")]
    [Tooltip("Height of a wandering vendor in canvas units (the canvas is 1080 high).")]
    [SerializeField] private float walkerHeight = 230f;

    [Tooltip("Walking speed in canvas units per second.")]
    [SerializeField] private float walkSpeed = 150f;

    [Tooltip("Most vendors on screen at once.")]
    [SerializeField] private int maxAtOnce = 2;

    [Tooltip("Seconds between new vendors arriving (min, max).")]
    [SerializeField] private Vector2 arriveEvery = new Vector2(3f, 9f);

    [Tooltip("How many stops a vendor makes before leaving (min, max).")]
    [SerializeField] private Vector2Int stops = new Vector2Int(2, 5);

    [Tooltip("Seconds a vendor stands still at each stop (min, max).")]
    [SerializeField] private Vector2 pauseRange = new Vector2(0.8f, 3f);

    [Range(0f, 1f)]
    [Tooltip("Chance that a vendor is one who stays for a good while (many more stops and longer pauses).")]
    [SerializeField] private float stayChance = 0.25f;

    [Tooltip("Distance from the bottom edge of the screen to the vendors' feet (canvas units).")]
    [SerializeField] private float feetHeight = 14f;

    [Header("Pets")]
    [Tooltip("Size of a pet hopping along the bottom of the screen (canvas units).")]
    [SerializeField] private float petSize = 150f;

    [Tooltip("Most pets shown at once (a random few of the save's pets).")]
    [SerializeField] private int maxPets = 6;

    [Tooltip("Seconds a pet rests between hops (min, max).")]
    [SerializeField] private Vector2 petRest = new Vector2(0.5f, 2.2f);

    [Tooltip("How far a hop carries a pet sideways (min, max, canvas units).")]
    [SerializeField] private Vector2 petHopDistance = new Vector2(90f, 300f);

    [Tooltip("How high a hop goes (canvas units).")]
    [SerializeField] private float petHopHeight = 110f;

    [Tooltip("Width of the UFO helper flying along the top of the screen (canvas units).")]
    [SerializeField] private float ufoWidth = 280f;

    [Tooltip("Flying speed of the UFO helper (canvas units per second).")]
    [SerializeField] private float ufoSpeed = 150f;

    [Tooltip("How far below the top of the screen the UFO flies (min, max, canvas units).")]
    [SerializeField] private Vector2 ufoHeightRange = new Vector2(70f, 250f);

    [Header("UFO abductions")]
    [Tooltip("Turn off the UFO abducting walking vendors and pets on the main menu.")]
    [SerializeField] private bool disableAbduction = false;

    [Tooltip("Seconds between two abductions (min, max; 0 = 14 and 32).")]
    [SerializeField] private Vector2 abductEvery = Vector2.zero;

    [Min(0f)]
    [Tooltip("Seconds the beam holds its victim while pulling it up (0 = 2.4).")]
    [SerializeField] private float abductSeconds = 0f;

    [Tooltip("Text on the pets toggle (above the 'display floor' one).")]
    [SerializeField] private string petsWanderText = "Pets Wander";

    [Header("Toggle")]
    [Tooltip("Size of the toggle at the bottom left.")]
    [SerializeField] private Vector2 toggleSize = new Vector2(300f, 56f);

    [Tooltip("Text on the vendors toggle.")]
    [SerializeField] private string toggleText = "Vendors wander";

    [Tooltip("Text on the live background toggle (above the vendors one).")]
    [SerializeField] private string liveText = "Live background";

    [Tooltip("Text on the blur toggle (above the live background one).")]
    [SerializeField] private string blurText = "Blur background";

    private const string Pref = "PixelClicker.Setting.TitleWanderers";
    private const string LivePref = "PixelClicker.Setting.TitleLiveBackground";

    /// <summary>True (the default) = the title screen shows the live, blurred game scene behind it; false = the floor style's picture fills the background, blurred.</summary>
    public static bool LiveBackground
    {
        get
        {
            if (liveCache < 0) liveCache = PlayerPrefs.GetInt(LivePref, 1);
            return liveCache == 1;
        }
        set
        {
            liveCache = value ? 1 : 0;
            PlayerPrefs.SetInt(LivePref, liveCache);
        }
    }

    private static int liveCache = -1;

    private const string BlurPref = "PixelClicker.Setting.TitleBlurBackground";
    private static int blurCache = -1;

    /// <summary>True (the default) = the title background is blurred; false = it is shown sharp (the live scene itself, or the floor picture).</summary>
    public static bool BlurBackground
    {
        get
        {
            if (blurCache < 0) blurCache = PlayerPrefs.GetInt(BlurPref, 1);
            return blurCache == 1;
        }
        set
        {
            blurCache = value ? 1 : 0;
            PlayerPrefs.SetInt(BlurPref, blurCache);
        }
    }

    private const string BlurAmountPref = "PixelClicker.Setting.TitleBlurAmount";

    /// <summary>How strong the title blur is, 0 (barely blurred) .. 1 (the full strong blur, the default).</summary>
    public static float BlurAmount
    {
        get { return PlayerPrefs.GetFloat(BlurAmountPref, 1f); }
        set { PlayerPrefs.SetFloat(BlurAmountPref, Mathf.Clamp01(value)); }
    }

    private const string PetsPref = "PixelClicker.Setting.TitlePets";

    /// <summary>On = the current save's pets hop along the bottom of the main menu and its UFO helper (if bought and switched on) flies along the top.</summary>
    public static bool PetsEnabled
    {
        get { return PlayerPrefs.GetInt(PetsPref, 0) == 1; }
        set { PlayerPrefs.SetInt(PetsPref, value ? 1 : 0); }
    }

    private const string FloorPref = "PixelClicker.Setting.DisplayFloor";
    private static int floorCache = -1;

    /// <summary>True = the floor is drawn in the game; false (the default) = only the sky shows and an invisible floor (its collider) lets pixels land.</summary>
    public static bool DisplayFloor
    {
        get
        {
            if (floorCache < 0) floorCache = PlayerPrefs.GetInt(FloorPref, 0);
            return floorCache == 1;
        }
        set
        {
            floorCache = value ? 1 : 0;
            PlayerPrefs.SetInt(FloorPref, floorCache);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { liveCache = -1; blurCache = -1; floorCache = -1; }

    private class Walker
    {
        public RectTransform rect, art;
        public float x, targetX, speed, pause, bobPhase, facing = 1f;
        public int stopsLeft;
        public bool leaving, staying, abducted;
        public float exitX;
        public int key;
    }

    private readonly List<Walker> walkers = new List<Walker>();
    private Transform layer;
    private class Toggle { public RectTransform rect; public CanvasGroup group; public Image tick; public System.Func<bool> get; public System.Action<bool> set; public TMP_Text label; public Slider slider; public float sliderUsed = -99f; }
    private readonly List<Toggle> toggles = new List<Toggle>();
    private bool on, built;
    private float nextArrival;
    private RectTransform canvasRect;
    private CanvasGroup layerGroup;
    private bool fadingOut;

    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(Pref, 0) == 1; }
        set { PlayerPrefs.SetInt(Pref, value ? 1 : 0); }
    }

    /// <summary>Builds the layer the vendors walk on (behind the title's widgets) and the toggle, under 'foreground'.</summary>
    public void Setup(Transform foreground, TMP_FontAsset font, RectTransform canvas)
    {
        canvasRect = canvas;
        on = Enabled;
        GameObject l = new GameObject("Wanderers", typeof(RectTransform));
        l.transform.SetParent(foreground, false);
        l.transform.SetAsFirstSibling();   // behind the bar, the logo and the buttons
        RectTransform lr = l.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        layer = l.transform;
        layerGroup = l.AddComponent<CanvasGroup>();
        layerGroup.blocksRaycasts = false;

        MakeToggle(font, "Wander Toggle", toggleText, 24f, () => on, v => { on = v; Enabled = v; });
        MakeToggle(font, "Pets Toggle", petsWanderText, 24f + toggleSize.y + 10f, () => PetsEnabled, v => PetsEnabled = v);
        MakeToggle(font, "Live Background Toggle", liveText, 24f + (toggleSize.y + 10f) * 2f, () => LiveBackground, v => LiveBackground = v);
        MakeToggle(font, "Blur Background Toggle", blurText, 24f + (toggleSize.y + 10f) * 3f, () => BlurBackground, v => BlurBackground = v, true);
        built = true;
        Refresh();
        nextArrival = Time.unscaledTime + 1.5f;
    }

    private void Refresh()
    {
        foreach (Toggle t in toggles)
        {
            if (t.tick != null) t.tick.gameObject.SetActive(t.get());
            // a toggle with a slider shows the slider instead of its text while it is ticked
            if (t.slider != null)
            {
                bool slide = t.get();
                t.slider.gameObject.SetActive(slide);
                if (t.label != null) t.label.gameObject.SetActive(!slide);
            }
        }
    }

    private void MakeToggle(TMP_FontAsset font, string name, string text, float y, System.Func<bool> get, System.Action<bool> set, bool withSlider = false)
    {
        GameObject t = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(Button));
        t.transform.SetParent(layer.parent, false);
        Image bg = t.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
        RectTransform rect = t.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = toggleSize;
        rect.anchoredPosition = new Vector2(24f, y);
        CanvasGroup group = t.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        Toggle toggle = new Toggle { rect = rect, group = group, get = get, set = set };
        t.GetComponent<Button>().onClick.AddListener(() =>
        {
            // Letting go of the blur slider must not count as a click on the toggle behind it.
            if (toggle.slider != null && toggle.slider.gameObject.activeInHierarchy &&
                (Time.unscaledTime - toggle.sliderUsed < 0.3f || RectTransformUtility.RectangleContainsScreenPoint((RectTransform)toggle.slider.transform, PixelInput.PointerPosition(), null))) return;
            toggle.set(!toggle.get());
            Refresh();
        });

        // A tick box and the text.
        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(t.transform, false);
        box.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 1f);
        box.GetComponent<Image>().raycastTarget = false;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 0.5f);
        br.sizeDelta = new Vector2(32f, 32f);
        br.anchoredPosition = new Vector2(14f, 0f);
        GameObject fill = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(box.transform, false);
        toggle.tick = fill.GetComponent<Image>();
        toggle.tick.sprite = PixelUIKit.TickSprite();
        toggle.tick.color = new Color(0.35f, 0.85f, 0.5f, 1f);
        toggle.tick.raycastTarget = false;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(3f, 3f); fr.offsetMax = new Vector2(-3f, -3f);

        TMP_Text label = PixelUIKit.CreateText(font, t.transform, "Label", text, 26f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        PixelUIKit.Caps(label);
        label.raycastTarget = false;
        RectTransform lrr = label.rectTransform;
        lrr.anchorMin = Vector2.zero; lrr.anchorMax = Vector2.one;
        lrr.offsetMin = new Vector2(58f, 0f); lrr.offsetMax = new Vector2(-10f, 0f);
        toggle.label = label;
        if (withSlider)
        {
            Slider sl = PixelUIKit.CreateSlider(t.transform, "Blur Slider", new Color(0.02f, 0.03f, 0.05f, 1f), new Color(0.35f, 0.85f, 0.5f, 1f), Color.white);
            RectTransform sr = sl.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(58f, 6f); sr.offsetMax = new Vector2(-10f, -6f);
            sl.minValue = 0f; sl.maxValue = 1f;
            sl.SetValueWithoutNotify(BlurAmount);
            sl.onValueChanged.AddListener(v => { BlurAmount = v; toggle.sliderUsed = Time.unscaledTime; });
            toggle.slider = sl;
        }
        toggles.Add(toggle);
    }

    private void Update()
    {
        if (!built) return;
        if (!PixelTitleScreen.Showing)
        {
            if (walkers.Count > 0) ClearAll();
            if (hoppers.Count > 0) ClearPets();
            if (ufoArt != null) ClearUfo();
            fadingOut = false;
            if (layerGroup != null) layerGroup.alpha = 1f;
            return;
        }
        if (fadingOut)
        {
            // Play was pressed: the vendors fade away at once (whatever the toggles say) and no new ones arrive.
            layerGroup.alpha = Mathf.MoveTowards(layerGroup.alpha, 0f, Time.unscaledDeltaTime / 0.3f);
            foreach (Toggle t in toggles) t.group.alpha = Mathf.MoveTowards(t.group.alpha, 0f, Time.unscaledDeltaTime / 0.2f);
            return;
        }

        // The toggle is invisible until the mouse is over it.
        // Both toggles appear while the mouse is over either of them (they stack in one corner).
        bool over = false;
        foreach (Toggle t in toggles) if (RectTransformUtility.RectangleContainsScreenPoint(t.rect, PixelInput.PointerPosition(), null)) over = true;
        foreach (Toggle t in toggles) t.group.alpha = Mathf.MoveTowards(t.group.alpha, over ? 1f : 0f, Time.unscaledDeltaTime / (over ? 0.12f : 0.4f));

        if (!on && walkers.Count > 0) ClearAll();   // switched off: everyone goes
        UpdatePets(Time.unscaledDeltaTime);
        UpdateUfo(Time.unscaledDeltaTime);

        float dt = Time.unscaledDeltaTime;
        float half = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;
        for (int i = walkers.Count - 1; i >= 0; i--)
        {
            Walker w = walkers[i];
            if (w.rect == null) { walkers.RemoveAt(i); continue; }
            if (Step(w, dt, half)) { Destroy(w.rect.gameObject); walkers.RemoveAt(i); }
        }

        if (on && walkers.Count < maxAtOnce && Time.unscaledTime >= nextArrival)
        {
            Spawn(half);
            nextArrival = Time.unscaledTime + Random.Range(arriveEvery.x, Mathf.Max(arriveEvery.x, arriveEvery.y));
        }
    }

    /// <summary>Called when Play is pressed: the wandering vendors fade out immediately.</summary>
    public void FadeOut() { fadingOut = true; }

    private void ClearAll()
    {
        foreach (Walker w in walkers) if (w.rect != null) Destroy(w.rect.gameObject);
        walkers.Clear();
    }

    /// <summary>Everyone who can walk in: Cubie, the other vendors and the Egg and Bean cats (whether or not you have met them).</summary>
    private void Spawn(float half)
    {
        List<System.Func<float, RectTransform>> makers = new List<System.Func<float, RectTransform>>();
        List<int> keys = new List<int>();
        PixelGuideVendor guide = PixelFind.First<PixelGuideVendor>();
        if (guide != null) { makers.Add(h => guide.BuildWalker(layer, h)); keys.Add(-1); }
        foreach (PixelMinigame m in PixelMinigame.All)
        {
            PixelVisitorMinigame v = m as PixelVisitorMinigame;
            if (v == null) continue;
            makers.Add(h => v.BuildWalker(layer, h));
            keys.Add(v.Id.GetHashCode());
        }
        // the same NPC never walks twice at once: only those not already on screen are candidates
        List<int> free = new List<int>();
        for (int i = 0; i < makers.Count; i++)
        {
            bool busy = false;
            foreach (Walker o in walkers) if (o.key == keys[i]) { busy = true; break; }
            if (!busy) free.Add(i);
        }
        if (free.Count == 0) return;
        int pick = free[Random.Range(0, free.Count)];
        RectTransform rect = makers[pick](walkerHeight);
        if (rect == null) return;

        Walker w = new Walker { rect = rect, art = rect.GetChild(0) as RectTransform, key = keys[pick] };
        bool fromLeft = Random.value < 0.5f;
        w.x = fromLeft ? -half - 200f : half + 200f;
        w.exitX = w.x;
        w.speed = walkSpeed * Random.Range(0.85f, 1.15f);
        w.staying = Random.value < stayChance;
        w.stopsLeft = Random.Range(stops.x, Mathf.Max(stops.x, stops.y) + 1) * (w.staying ? 3 : 1);
        w.bobPhase = Random.value * 6.28f;
        w.facing = fromLeft ? 1f : -1f;
        w.targetX = PickStop(half, w.x);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(w.x, feetHeight);   // placed at once: left where the builder made him he would show in the screen's middle for a frame
        rect.localScale = new Vector3(w.facing, 1f, 1f);
        walkers.Add(w);
    }

    private float PickStop(float half, float from)
    {
        float x;
        int guard = 0;
        do { x = Random.Range(-half * 0.85f, half * 0.85f); } while (Mathf.Abs(x - from) < 160f && ++guard < 8);
        return x;
    }

    /// <summary>Moves one vendor; true when he has left the screen.</summary>
    private bool Step(Walker w, float dt, float half)
    {
        if (w.abducted) return false;   // the UFO has him
        float dx = w.targetX - w.x;
        bool walking = false;
        if (w.pause > 0f)
        {
            w.pause -= dt;
        }
        else if (Mathf.Abs(dx) > 4f)
        {
            float move = Mathf.Min(Mathf.Abs(dx), w.speed * dt);
            w.x += Mathf.Sign(dx) * move;
            w.facing = Mathf.Sign(dx);
            walking = true;
        }
        else if (w.leaving) return true;
        else if (w.stopsLeft > 0)
        {
            w.stopsLeft--;
            w.pause = Random.Range(pauseRange.x, Mathf.Max(pauseRange.x, pauseRange.y)) * (w.staying ? 2f : 1f);
            w.targetX = PickStop(half, w.x);
            if (Random.value < 0.3f) w.facing = -w.facing;   // glances round while standing
        }
        else
        {
            w.leaving = true;
            w.targetX = Random.value < 0.5f ? -half - 220f : half + 220f;
        }

        float t = Time.unscaledTime + w.bobPhase;
        float bob = walking ? Mathf.Abs(Mathf.Sin(t * 9f)) * 12f : Mathf.Sin(t * 2f) * 2f;
        float sway = walking ? Mathf.Sin(t * 9f) * 3f : 0f;
        w.rect.anchoredPosition = new Vector2(w.x, feetHeight + bob);
        w.rect.localRotation = Quaternion.Euler(0f, 0f, sway);
        w.rect.localScale = new Vector3(w.facing, 1f, 1f);
        return false;
    }

    // ------------------------------------------------------------------
    // Pets hopping along the bottom and the UFO helper flying along the top (current save, behind the 'Pets & UFO' toggle)
    // ------------------------------------------------------------------

    private class Hopper
    {
        public RectTransform rect;
        public RawImage image;
        public RenderTexture texture;
        public Camera camera;
        public Transform pivot;
        public GameObject studio;
        public float x, y, vy, vx, rest, facing = 1f, squash, hopT, hopLen, phase, appear;
        public bool inAir, abducted;
    }

    private readonly List<Hopper> hoppers = new List<Hopper>();
    private string petSignature = "";
    private float petRespawnAt;
    private float petCheckTimer;
    private const float StudioHeight = -5200f;
    private int studioCount;

    private static string SignatureOf(List<PixelPets.TitlePet> list)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (PixelPets.TitlePet p in list) sb.Append((int)p.type).Append(':').Append(p.outfit).Append(',');
        return sb.ToString();
    }

    private void UpdatePets(float dt)
    {
        bool want = PetsEnabled && !fadingOut;
        if (!want) { if (hoppers.Count > 0) ClearPets(); return; }

        // (Re)build whenever the save's pets change (a slot was loaded from the menu, the first frames after start...).
        petCheckTimer -= dt;
        if (petCheckTimer <= 0f)
        {
            petCheckTimer = 0.5f;
            PixelPets pets = PixelPets.Instance;
            List<PixelPets.TitlePet> owned = pets != null ? pets.TitlePets() : new List<PixelPets.TitlePet>();
            string sig = SignatureOf(owned);
            if (sig != petSignature || (owned.Count > 0 && hoppers.Count == 0 && Time.unscaledTime >= petRespawnAt))
            {
                petSignature = sig;
                ClearPets();
                BuildPets(pets, owned);
            }
        }

        float half = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;
        foreach (Hopper h in hoppers) if (!h.abducted) StepHopper(h, dt, half);
    }

    private void BuildPets(PixelPets pets, List<PixelPets.TitlePet> owned)
    {
        if (pets == null || owned.Count == 0) return;
        // A random few (every pet if there are not many).
        List<PixelPets.TitlePet> pool = new List<PixelPets.TitlePet>(owned);
        float half = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;
        for (int n = 0; n < Mathf.Max(1, maxPets) && pool.Count > 0; n++)
        {
            int pick = Random.Range(0, pool.Count);
            PixelPets.TitlePet pet = pool[pick];
            pool.RemoveAt(pick);
            Hopper h = MakeHopper(pets, pet);
            if (h == null) continue;
            h.x = Random.Range(-half * 0.8f, half * 0.8f);
            h.rest = Random.Range(0f, petRest.y);
            h.phase = Random.value * 6.28f;
            hoppers.Add(h);
        }
    }

    /// <summary>One pet: a hidden studio (its model and outfit under a pivot, an orthographic camera into a transparent RenderTexture) shown by a RawImage on the title.</summary>
    private Hopper MakeHopper(PixelPets pets, PixelPets.TitlePet pet)
    {
        GameObject studio = new GameObject("Title Pet Studio");
        studio.transform.position = new Vector3(studioCount++ * 40f, StudioHeight, 0f);
        GameObject pivotGo = new GameObject("Pivot");
        pivotGo.transform.SetParent(studio.transform, false);
        GameObject model = pets.BuildTitleModel(pet, pivotGo.transform);
        if (model == null) { Destroy(studio); return null; }

        Hopper h = new Hopper { studio = studio, pivot = pivotGo.transform };
        h.texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { name = "Title Pet" };
        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(studio.transform, false);
        h.camera = camGo.AddComponent<Camera>();
        h.camera.enabled = false;
        h.camera.clearFlags = CameraClearFlags.SolidColor;
        h.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        h.camera.orthographic = true;
        h.camera.orthographicSize = 1.15f;           // room for hats and capes round the unit cube
        h.camera.nearClipPlane = 0.1f;
        h.camera.farClipPlane = 30f;
        h.camera.allowHDR = false;
        h.camera.targetTexture = h.texture;
        camGo.transform.localPosition = new Vector3(0f, 0.28f, 4.4f);
        camGo.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);   // the camera sits in front (+Z) of the pet and looks back at it

        GameObject lightGo = new GameObject("Light");
        lightGo.transform.SetParent(studio.transform, false);
        lightGo.transform.localPosition = new Vector3(2f, 3f, 5f);
        Light l = lightGo.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 25f;
        l.intensity = 6f;
        l.shadows = LightShadows.None;

        GameObject img = new GameObject("Title Pet", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(layer, false);
        h.image = img.GetComponent<RawImage>();
        h.image.texture = h.texture;
        h.image.raycastTarget = false;
        h.rect = img.GetComponent<RectTransform>();
        h.rect.anchorMin = h.rect.anchorMax = new Vector2(0.5f, 0f);
        h.rect.pivot = new Vector2(0.5f, 0.16f);                      // the cube's bottom edge sits 16% up the picture
        h.rect.sizeDelta = Vector2.one * petSize * 2.3f;              // the picture spans 2.3 world units for a 1 unit cube
        return h;
    }

    private void StepHopper(Hopper h, float dt, float half)
    {
        if (h.rect == null) return;
        h.appear = Mathf.MoveTowards(h.appear, 1f, dt / 0.4f);
        const float flight = 0.55f;
        if (!h.inAir)
        {
            h.rest -= dt;
            h.squash = Mathf.Lerp(h.squash, 0f, dt * 12f);
            if (h.rest <= 0f)
            {
                float dist = Random.Range(petHopDistance.x, Mathf.Max(petHopDistance.x, petHopDistance.y)) * (Random.value < 0.5f ? -1f : 1f);
                if (h.x + dist > half * 0.88f || h.x + dist < -half * 0.88f) dist = -dist;   // never off the edge
                h.facing = Mathf.Sign(dist);
                h.vx = dist / flight;
                h.vy = 4f * petHopHeight / flight;     // up and back down in 'flight' seconds: g = 8 H / t^2
                h.inAir = true;
                h.hopT = 0f;
                h.squash = -0.18f;                      // stretches as it leaves
            }
        }
        else
        {
            h.hopT += dt;
            float g = 8f * petHopHeight / (flight * flight);
            h.vy -= g * dt;
            h.y += h.vy * dt;
            h.x += h.vx * dt;
            if (h.y <= 0f && h.vy < 0f)
            {
                h.y = 0f;
                h.inAir = false;
                h.squash = 0.3f;                        // lands with a squash
                h.rest = Random.Range(petRest.x, Mathf.Max(petRest.x, petRest.y));
            }
        }

        float tilt = h.inAir ? Mathf.Clamp(h.vy / (4f * petHopHeight / flight), -1f, 1f) : 0f;
        h.pivot.localRotation = Quaternion.Euler(14f - tilt * 14f, h.facing * 28f + Mathf.Sin(Time.unscaledTime * 1.6f + h.phase) * 4f, -h.facing * tilt * 8f);
        h.rect.localScale = new Vector3(h.appear * (1f + h.squash * 0.5f), h.appear * (1f - h.squash * 0.5f), 1f);
        h.rect.anchoredPosition = new Vector2(h.x, feetHeight + h.y);
        h.camera.Render();
    }

    private void ClearPets()
    {
        foreach (Hopper h in hoppers)
        {
            if (h.rect != null) Destroy(h.rect.gameObject);
            if (h.studio != null) Destroy(h.studio);
            if (h.texture != null) { h.texture.Release(); Destroy(h.texture); }
        }
        hoppers.Clear();
        petSignature = "";
        petCheckTimer = 0f;
    }

    // ---- The UFO helper: a block-built flying saucer drifting about the top of the screen ----

    private RectTransform ufoArt;
    private readonly List<Image> ufoLights = new List<Image>();
    private float ufoX, ufoY, ufoVx, ufoVy, ufoTargetX, ufoTargetY, ufoPause, ufoPhase, ufoAppear;
    private float ufoCheckTimer;

    private static Image UfoBlock(Transform parent, string name, Vector2 centre, Vector2 size, Color color)
    {
        GameObject g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        Image im = g.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = centre;
        r.sizeDelta = size;
        return im;
    }

    /// <summary>Is the UFO helper bought in the current save and not switched off in its Toggles window?</summary>
    private static bool UfoAvailable()
    {
        PixelRobotWorker robot = PixelFind.First<PixelRobotWorker>();
        return robot != null && robot.Active;
    }

    private void UpdateUfo(float dt)
    {
        bool want = !fadingOut;   // the UFO depends only on the save: bought, and not switched off in its own Toggles tab
        if (!want) { if (ufoArt != null) ClearUfo(); return; }
        ufoCheckTimer -= dt;
        if (ufoCheckTimer <= 0f)
        {
            ufoCheckTimer = 0.5f;
            bool available = UfoAvailable();
            if (available && ufoArt == null) BuildUfoArt();
            else if (!available && ufoArt != null) ClearUfo();
        }
        if (ufoArt == null) return;

        float half = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;
        ufoAppear = Mathf.MoveTowards(ufoAppear, 1f, dt / 0.6f);
        ufoPause -= dt;
        float dx = ufoTargetX - ufoX, dy = ufoTargetY - ufoY;
        UpdateAbduction(dt, half);
        dx = ufoTargetX - ufoX; dy = ufoTargetY - ufoY;
        if (abState == Ab.Idle && ufoPause <= 0f && dx * dx + dy * dy < 40f * 40f)
        {
            ufoPause = Random.Range(0.8f, 2.8f);
            ufoTargetX = Random.Range(-half * 0.85f, half * 0.85f);
            ufoTargetY = -Random.Range(ufoHeightRange.x, Mathf.Max(ufoHeightRange.x, ufoHeightRange.y));
        }
        Vector2 want2 = ufoPause > 0f ? Vector2.zero : new Vector2(dx, dy).normalized * ufoSpeed * (abState == Ab.Fly ? 1.8f : 1f);
        ufoVx = Mathf.Lerp(ufoVx, want2.x, dt * 1.6f);
        ufoVy = Mathf.Lerp(ufoVy, want2.y, dt * 1.6f);
        ufoX += ufoVx * dt;
        ufoY += ufoVy * dt;

        float t = Time.unscaledTime + ufoPhase;
        ufoArt.anchoredPosition = new Vector2(ufoX, ufoY + Mathf.Sin(t * 2.2f) * 7f);
        ufoArt.localRotation = Quaternion.Euler(0f, 0f, -ufoVx * 0.06f);   // leans into its flight
        ufoArt.localScale = Vector3.one * ((ufoWidth / 280f) * ufoAppear);
        for (int i = 0; i < ufoLights.Count; i++)
            ufoLights[i].color = Color.Lerp(new Color(0.25f, 0.2f, 0.05f), new Color(1f, 0.9f, 0.3f), Mathf.Clamp01(Mathf.Sin(t * 7f - i * 0.9f) * 0.5f + 0.5f));
    }

    private void BuildUfoArt()
    {
        GameObject g = new GameObject("Title UFO", typeof(RectTransform));
        g.transform.SetParent(layer, false);
        ufoArt = g.GetComponent<RectTransform>();
        ufoArt.anchorMin = ufoArt.anchorMax = new Vector2(0.5f, 1f);
        ufoArt.pivot = new Vector2(0.5f, 0.5f);
        ufoArt.sizeDelta = new Vector2(280f, 170f);
        Transform t = ufoArt;
        // The dome and the alien under it.
        UfoBlock(t, "Dome Top", new Vector2(0f, 62f), new Vector2(76f, 26f), new Color(0.6f, 0.95f, 1f, 0.55f));
        UfoBlock(t, "Dome", new Vector2(0f, 40f), new Vector2(112f, 30f), new Color(0.6f, 0.95f, 1f, 0.5f));
        UfoBlock(t, "Alien", new Vector2(0f, 38f), new Vector2(44f, 36f), new Color(0.45f, 0.85f, 0.4f, 1f));
        UfoBlock(t, "Eye L", new Vector2(-10f, 40f), new Vector2(10f, 15f), Color.black);
        UfoBlock(t, "Eye R", new Vector2(10f, 40f), new Vector2(10f, 15f), Color.black);
        // The hull: stepped like pixel art.
        UfoBlock(t, "Hull Top", new Vector2(0f, 14f), new Vector2(190f, 22f), new Color(0.62f, 0.66f, 0.74f, 1f));
        UfoBlock(t, "Hull", new Vector2(0f, -8f), new Vector2(262f, 28f), new Color(0.5f, 0.54f, 0.62f, 1f));
        UfoBlock(t, "Rim", new Vector2(0f, -8f), new Vector2(280f, 8f), new Color(0.95f, 0.62f, 0.2f, 1f));
        UfoBlock(t, "Underside", new Vector2(0f, -32f), new Vector2(150f, 22f), new Color(0.3f, 0.32f, 0.4f, 1f));
        UfoBlock(t, "Emitter", new Vector2(0f, -48f), new Vector2(64f, 8f), new Color(0.95f, 0.62f, 0.2f, 1f));
        ufoLights.Clear();
        for (int i = 0; i < 7; i++)
            ufoLights.Add(UfoBlock(t, "Light " + i, new Vector2(-96f + i * 32f, -8f), new Vector2(14f, 14f), new Color(1f, 0.9f, 0.3f, 1f)));

        float half = canvasRect != null ? canvasRect.rect.width * 0.5f : 960f;
        ufoX = Random.value < 0.5f ? -half - 200f : half + 200f;   // flies in from a side
        ufoY = -Random.Range(ufoHeightRange.x, Mathf.Max(ufoHeightRange.x, ufoHeightRange.y));
        ufoTargetX = Random.Range(-half * 0.6f, half * 0.6f);
        ufoTargetY = ufoY;
        ufoVx = ufoVy = 0f;
        ufoPause = 0f;
        ufoPhase = Random.value * 6.28f;
        ufoAppear = 1f;
        ufoArt.anchoredPosition = new Vector2(ufoX, ufoY);
    }

    // ---- The UFO abducts one of the walking vendors / hopping pets now and then ----

    private enum Ab { Idle, Fly, Beam }
    private Ab abState = Ab.Idle;
    private float abNext = -1f, abTimer;
    private Walker abWalker;
    private Hopper abHopper;
    private Vector3 abStart;
    private RectTransform beamRect;

    private RectTransform AbTargetRect => abWalker != null ? abWalker.rect : (abHopper != null ? abHopper.rect : null);

    /// <summary>Called every frame by the UFO: waits, picks a victim, flies over, beams it up.</summary>
    private void UpdateAbduction(float dt, float half)
    {
        if (disableAbduction || ufoArt == null || fadingOut) { if (abState != Ab.Idle) AbortAbduction(); return; }
        if (abNext < 0f) abNext = Time.unscaledTime + RollAbductWait();

        switch (abState)
        {
            case Ab.Idle:
                if (Time.unscaledTime < abNext || ufoAppear < 1f) return;
                if (!PickVictim(half)) { abNext = Time.unscaledTime + 4f; return; }
                abState = Ab.Fly;
                abTimer = 0f;
                break;

            case Ab.Fly:
            {
                RectTransform r = AbTargetRect;
                if (r == null) { AbortAbduction(); return; }
                abTimer += dt;
                float tx = TargetCanvasX(r);
                ufoTargetX = tx;
                ufoTargetY = ufoY;
                ufoPause = 0f;
                if (Mathf.Abs(ufoX - tx) < 24f)
                {
                    // Above him: the beam comes on and he is held.
                    if (abWalker != null) abWalker.abducted = true; else abHopper.abducted = true;
                    abStart = r.position;
                    abState = Ab.Beam;
                    abTimer = 0f;
                    BuildBeam();
                }
                else if (abTimer > 12f) AbortAbduction();   // he got away
                break;
            }

            case Ab.Beam:
            {
                RectTransform r = AbTargetRect;
                if (r == null) { AbortAbduction(); return; }
                abTimer += dt;
                float dur = abductSeconds > 0f ? abductSeconds : 2.4f;
                float k = Mathf.Clamp01(abTimer / dur);
                float e = k * k * (3f - 2f * k);
                ufoTargetX = ufoX; ufoTargetY = ufoY; ufoPause = 999f;   // hover still while the beam works
                Vector3 emitter = ufoArt.TransformPoint(new Vector3(0f, -56f, 0f));
                Vector3 pos = Vector3.Lerp(abStart, emitter, e);
                pos.x += Mathf.Sin(abTimer * 11f) * 6f * (1f - e);
                r.position = pos;
                float sc = Mathf.Lerp(1f, 0.12f, e);
                float face = Mathf.Sign(r.localScale.x == 0f ? 1f : r.localScale.x);
                r.localScale = new Vector3(face * sc, sc, 1f);
                r.localRotation = Quaternion.Euler(0f, 0f, abTimer * 260f * e);
                UpdateBeam(r);
                if (k >= 1f) FinishAbduction();
                break;
            }
        }
    }

    private float RollAbductWait()
    {
        float lo = abductEvery.x > 0f ? abductEvery.x : 14f, hi = abductEvery.y > 0f ? abductEvery.y : 32f;
        return Random.Range(lo, Mathf.Max(lo, hi));
    }

    /// <summary>The canvas x (from the screen's middle) of a bottom-anchored title character.</summary>
    private float TargetCanvasX(RectTransform r) => r.anchoredPosition.x;

    private bool PickVictim(float half)
    {
        List<Walker> w = walkers.FindAll(x => x.rect != null && !x.abducted && Mathf.Abs(x.x) < half * 0.85f);
        List<Hopper> h = hoppers.FindAll(x => x.rect != null && !x.abducted && x.appear >= 1f && !x.inAir && Mathf.Abs(x.x) < half * 0.85f);
        int total = w.Count + h.Count;
        if (total == 0) return false;
        int pick = Random.Range(0, total);
        abWalker = null; abHopper = null;
        if (pick < w.Count) abWalker = w[pick]; else abHopper = h[pick - w.Count];
        return true;
    }

    private void BuildBeam()
    {
        if (beamRect != null) return;
        PixelAudio.Play("ufo_beam");
        GameObject g = new GameObject("Abduction Beam", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ufoArt, false);
        g.transform.SetAsFirstSibling();   // behind the saucer's parts
        Image im = g.GetComponent<Image>();
        im.color = new Color(0.6f, 1f, 0.55f, 0.32f);
        im.raycastTarget = false;
        beamRect = g.GetComponent<RectTransform>();
        beamRect.anchorMin = beamRect.anchorMax = new Vector2(0.5f, 0.5f);
        beamRect.pivot = new Vector2(0.5f, 1f);
        beamRect.sizeDelta = new Vector2(110f, 10f);
        GameObject core = new GameObject("Core", typeof(RectTransform), typeof(Image));
        core.transform.SetParent(g.transform, false);
        Image ci = core.GetComponent<Image>();
        ci.color = new Color(0.9f, 1f, 0.8f, 0.4f);
        ci.raycastTarget = false;
        RectTransform cr = core.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0f); cr.anchorMax = new Vector2(0.5f, 1f);
        cr.sizeDelta = new Vector2(46f, 0f);
    }

    private void UpdateBeam(RectTransform target)
    {
        if (beamRect == null) return;
        Vector3 local = ufoArt.InverseTransformPoint(target.position);
        float dy = -50f - local.y;
        float len = Mathf.Max(8f, Mathf.Sqrt(local.x * local.x + dy * dy));
        beamRect.anchoredPosition = new Vector2(0f, -50f);
        beamRect.sizeDelta = new Vector2(110f * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 18f)), len);
        beamRect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(local.x, Mathf.Max(1f, dy)) * Mathf.Rad2Deg);
    }

    private void FinishAbduction()
    {
        if (abWalker != null)
        {
            if (abWalker.rect != null) Destroy(abWalker.rect.gameObject);
            walkers.Remove(abWalker);
        }
        if (abHopper != null)
        {
            if (abHopper.rect != null) Destroy(abHopper.rect.gameObject);
            if (abHopper.studio != null) Destroy(abHopper.studio);
            if (abHopper.texture != null) { abHopper.texture.Release(); Destroy(abHopper.texture); }
            hoppers.Remove(abHopper);
            petRespawnAt = Time.unscaledTime + 20f;   // the pets come back after a while
        }
        EndAbduction();
    }

    /// <summary>Cancels an abduction in progress (the victim walks / hops on again).</summary>
    private void AbortAbduction()
    {
        PixelAudio.Stop("ufo_beam");
        if (abWalker != null) { abWalker.abducted = false; if (abWalker.rect != null) { abWalker.rect.localScale = new Vector3(abWalker.facing, 1f, 1f); abWalker.rect.localRotation = Quaternion.identity; } }
        if (abHopper != null) { abHopper.abducted = false; if (abHopper.rect != null) abHopper.rect.localRotation = Quaternion.identity; }
        EndAbduction();
    }

    private void EndAbduction()
    {
        abWalker = null; abHopper = null;
        abState = Ab.Idle;
        if (beamRect != null) { Destroy(beamRect.gameObject); beamRect = null; }
        ufoPause = 0.6f;
        abNext = Time.unscaledTime + RollAbductWait();
    }

    private void ClearUfo()
    {
        if (abState != Ab.Idle) AbortAbduction();
        if (ufoArt != null) Destroy(ufoArt.gameObject);
        ufoArt = null;
        ufoLights.Clear();
    }

    private void OnDestroy()
    {
        ClearPets();
        ClearUfo();
    }
}
