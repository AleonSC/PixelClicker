using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vendors wandering the main menu (added to the title screen by <see cref="PixelTitleScreen"/>): while the small toggle at the bottom left is ON,
/// the game's vendors (Cubie, the Wizard, Tinkerer, Farmer, Entrepreneur and, once met, the Egg and Bean cats) walk in from either side along the bottom
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

    [Header("Toggle")]
    [Tooltip("Size of the toggle at the bottom left.")]
    [SerializeField] private Vector2 toggleSize = new Vector2(300f, 56f);

    [Tooltip("Text on the vendors toggle.")]
    [SerializeField] private string toggleText = "Vendors wander";

    [Tooltip("Text on the live background toggle (above the vendors one).")]
    [SerializeField] private string liveText = "Live background";

    private const string Pref = "PixelClicker.Setting.TitleWanderers";
    private const string LivePref = "PixelClicker.Setting.TitleLiveBackground";

    /// <summary>True (the default) = the title screen shows the live, blurred game scene behind it; false = the floor style's picture fills the background, blurred.</summary>
    public static bool LiveBackground
    {
        get { return PlayerPrefs.GetInt(LivePref, 1) == 1; }
        set { PlayerPrefs.SetInt(LivePref, value ? 1 : 0); }
    }

    private class Walker
    {
        public RectTransform rect, art;
        public float x, targetX, speed, pause, bobPhase, facing = 1f;
        public int stopsLeft;
        public bool leaving, staying;
        public float exitX;
    }

    private readonly List<Walker> walkers = new List<Walker>();
    private Transform layer;
    private class Toggle { public RectTransform rect; public CanvasGroup group; public Image tick; public System.Func<bool> get; public System.Action<bool> set; }
    private readonly List<Toggle> toggles = new List<Toggle>();
    private bool on, built;
    private float nextArrival;
    private RectTransform canvasRect;

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

        MakeToggle(font, "Wander Toggle", toggleText, 24f, () => on, v => { on = v; Enabled = v; });
        MakeToggle(font, "Live Background Toggle", liveText, 24f + toggleSize.y + 10f, () => LiveBackground, v => LiveBackground = v);
        built = true;
        Refresh();
        nextArrival = Time.unscaledTime + 1.5f;
    }

    private void Refresh()
    {
        foreach (Toggle t in toggles) if (t.tick != null) t.tick.gameObject.SetActive(t.get());
    }

    private void MakeToggle(TMP_FontAsset font, string name, string text, float y, System.Func<bool> get, System.Action<bool> set)
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
        t.GetComponent<Button>().onClick.AddListener(() => { toggle.set(!toggle.get()); Refresh(); });

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
        toggle.tick.color = new Color(0.35f, 0.85f, 0.5f, 1f);
        toggle.tick.raycastTarget = false;
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(6f, 6f); fr.offsetMax = new Vector2(-6f, -6f);

        TMP_Text label = PixelUIKit.CreateText(font, t.transform, "Label", text, 26f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        PixelUIKit.Caps(label);
        label.raycastTarget = false;
        RectTransform lrr = label.rectTransform;
        lrr.anchorMin = Vector2.zero; lrr.anchorMax = Vector2.one;
        lrr.offsetMin = new Vector2(58f, 0f); lrr.offsetMax = new Vector2(-10f, 0f);
        toggles.Add(toggle);
    }

    private void Update()
    {
        if (!built) return;
        if (!PixelTitleScreen.Showing)
        {
            if (walkers.Count > 0) ClearAll();
            return;
        }

        // The toggle is invisible until the mouse is over it.
        // Both toggles appear while the mouse is over either of them (they stack in one corner).
        bool over = false;
        foreach (Toggle t in toggles) if (RectTransformUtility.RectangleContainsScreenPoint(t.rect, PixelInput.PointerPosition(), null)) over = true;
        foreach (Toggle t in toggles) t.group.alpha = Mathf.MoveTowards(t.group.alpha, over ? 1f : 0f, Time.unscaledDeltaTime / (over ? 0.12f : 0.4f));

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

    private void ClearAll()
    {
        foreach (Walker w in walkers) if (w.rect != null) Destroy(w.rect.gameObject);
        walkers.Clear();
    }

    /// <summary>Everyone who can walk in: Cubie always, the other vendors if they exist, the cats only once met.</summary>
    private void Spawn(float half)
    {
        List<System.Func<float, RectTransform>> makers = new List<System.Func<float, RectTransform>>();
        PixelGuideVendor guide = PixelFind.First<PixelGuideVendor>();
        if (guide != null) makers.Add(h => guide.BuildWalker(layer, h));
        foreach (PixelMinigame m in PixelMinigame.All)
        {
            PixelVisitorMinigame v = m as PixelVisitorMinigame;
            if (v == null) continue;
            string id = v.Id;
            if ((id == "egg" || id == "bean") && PixelStats.Total(id + ".met") < 1d) continue;
            makers.Add(h => v.BuildWalker(layer, h));
        }
        if (makers.Count == 0) return;
        RectTransform rect = makers[Random.Range(0, makers.Count)](walkerHeight);
        if (rect == null) return;

        Walker w = new Walker { rect = rect, art = rect.GetChild(0) as RectTransform };
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
}
