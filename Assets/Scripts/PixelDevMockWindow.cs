// ---- System notes ----
// PixelDevMockWindow.cs
//   'Mock up mode' (Dev Tools > Tools tick box): ONE draggable window that holds the Shape placer (tab 0, PixelDevShapes) and the UI mockup builder
//   (tab 1, PixelDevMockup). Both builders put their controls into this window's tab roots (TabRoot) instead of making their own canvases.
//   Header = drag handle + title + minimize (turns the window into a thin header; the active tab keeps working) + X (mock up mode off).
//   Only the visible tab is 'active' (PixelDevShapes.SetActive / PixelDevMockup.SetActive), so clicking the floor never drops shapes while
//   you are on the UI tab; mockup pieces stay visible while the window is open (PixelDevMockup.SetWindowOpen). Saved in PlayerPrefs
//   PixelClicker.Dev.Mock* (open, tab, minimized, position) - not part of the game save.
// ---------------------------------------------------
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelDevMockWindow : MonoBehaviour
{
    public const float Width = 500f, ContentHeight = 780f, HeaderHeight = 52f, TabsHeight = 46f;

    private const string PrefOpen = "PixelClicker.Dev.MockOn", PrefTab = "PixelClicker.Dev.MockTab", PrefMin = "PixelClicker.Dev.MockMin",
                         PrefX = "PixelClicker.Dev.MockX", PrefY = "PixelClicker.Dev.MockY";

    private static PixelDevMockWindow instance;

    /// <summary>Mock up mode is on (the window is showing, minimized or not).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Raised when mock up mode turns on / off (the Dev Tools tick box follows it).</summary>
    public static event Action<bool> OpenChanged;

    public static bool SavedOpen => PlayerPrefs.GetInt(PrefOpen, 0) != 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; IsOpen = false; OpenChanged = null; }

    public static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("Dev Mock Up Window").AddComponent<PixelDevMockWindow>();
    }

    /// <summary>Opens / closes mock up mode (the Dev Tools tick box).</summary>
    public static void SetOpen(bool on)
    {
        Boot();
        instance.Apply(on);
    }

    /// <summary>The root a tab's controls are built into (0 = Shape placer, 1 = UI mockup): 'Width' wide, 'ContentHeight' tall, origin top-left.</summary>
    public static RectTransform TabRoot(int tab)
    {
        Boot();
        return instance.roots[tab];
    }

    private readonly RectTransform[] roots = new RectTransform[2];
    private readonly Image[] tabImages = new Image[2];
    private GameObject canvasRoot, body;
    private RectTransform windowRect;
    private TMP_Text minLabel;
    private int tab;
    private bool minimized;
    private TMP_FontAsset font;

    private static readonly Color TabOn = new Color(0.25f, 0.45f, 0.8f, 1f), TabOff = new Color(0.2f, 0.24f, 0.34f, 1f);

    private void Awake()
    {
        instance = this;
        PixelClicker clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        tab = Mathf.Clamp(PlayerPrefs.GetInt(PrefTab, 0), 0, 1);
        minimized = PlayerPrefs.GetInt(PrefMin, 0) != 0;
        Build();
        canvasRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this) { instance = null; IsOpen = false; }
    }

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private void Build()
    {
        canvasRoot = PixelUIKit.CreateCanvas("Dev Mock Up Window UI", 560, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();

        GameObject windowGo = new GameObject("Window", typeof(RectTransform), typeof(Image));
        windowGo.transform.SetParent(canvasRoot.transform, false);
        windowRect = windowGo.GetComponent<RectTransform>();
        Vector2 saved = new Vector2(PlayerPrefs.GetFloat(PrefX, 20f), PlayerPrefs.GetFloat(PrefY, 120f));
        saved.x = Mathf.Clamp(saved.x, 0f, 1920f - 160f);   // keep at least the header reachable
        saved.y = Mathf.Clamp(saved.y, 0f, 1080f - HeaderHeight);
        PlaceTL(windowRect, saved.x, saved.y, Width, 0f);
        PixelUIKit.StyleWindow(windowGo.GetComponent<Image>(), new Color(0.08f, 0.1f, 0.16f, 0.97f));

        Color text = new Color(0.92f, 0.94f, 1f, 1f);

        // Header: the whole strip drags the window.
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image), typeof(PixelPopoutDrag));
        header.transform.SetParent(windowGo.transform, false);
        PlaceTL(header.GetComponent<RectTransform>(), 0f, 0f, Width, HeaderHeight);
        header.GetComponent<Image>().color = new Color(0.14f, 0.18f, 0.28f, 1f);
        PixelPopoutDrag drag = header.GetComponent<PixelPopoutDrag>();
        drag.target = windowRect;
        drag.canvas = canvas;
        drag.onMoved = SavePosition;
        TMP_Text title = PixelUIKit.CreateText(font, header.transform, "Title", "MOCK UP MODE", 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, text);
        PlaceTL(title.rectTransform, 16f, 0f, Width - 130f, HeaderHeight);
        title.raycastTarget = false;
        Button min = PixelUIKit.CreateButton(font, header.transform, "Minimize", "-", new Vector2(44f, 40f), new Color(0.3f, 0.36f, 0.5f, 1f), text, 30f);
        PlaceTL(min.GetComponent<RectTransform>(), Width - 100f, 6f, 44f, 40f);
        minLabel = min.GetComponentInChildren<TMP_Text>();
        min.onClick.AddListener(() => { minimized = !minimized; PlayerPrefs.SetInt(PrefMin, minimized ? 1 : 0); Layout(); });
        Button close = PixelUIKit.CreateButton(font, header.transform, "Close", "X", new Vector2(44f, 40f), new Color(0.6f, 0.2f, 0.2f, 1f), text, 26f);
        PlaceTL(close.GetComponent<RectTransform>(), Width - 50f, 6f, 44f, 40f);
        close.onClick.AddListener(() => Apply(false));

        // Body: the tab strip and the two tab roots.
        GameObject bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(windowGo.transform, false);
        body = bodyGo;
        PlaceTL(bodyGo.GetComponent<RectTransform>(), 0f, HeaderHeight, Width, TabsHeight + ContentHeight);
        string[] names = { "Shapes", "UI pieces" };
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            Button b = PixelUIKit.CreateButton(font, bodyGo.transform, names[i] + " Tab", names[i], new Vector2(Width / 2f - 12f, 38f), TabOff, text, 24f);
            PlaceTL(b.GetComponent<RectTransform>(), 8f + i * (Width / 2f - 4f), 4f, Width / 2f - 12f, 38f);
            tabImages[i] = b.GetComponent<Image>();
            b.onClick.AddListener(() => SetTab(index));

            GameObject root = new GameObject(names[i] + " Root", typeof(RectTransform));
            root.transform.SetParent(bodyGo.transform, false);
            roots[i] = root.GetComponent<RectTransform>();
            PlaceTL(roots[i], 0f, TabsHeight, Width, ContentHeight);
        }
        Layout();
    }

    private void SavePosition()
    {
        PlayerPrefs.SetFloat(PrefX, windowRect.anchoredPosition.x);
        PlayerPrefs.SetFloat(PrefY, -windowRect.anchoredPosition.y);
    }

    private void SetTab(int index)
    {
        tab = index;
        PlayerPrefs.SetInt(PrefTab, tab);
        Layout();
        RefreshModes();
    }

    /// <summary>Shows the tab, the minimized (header only) or full window height and the minimize button's symbol.</summary>
    private void Layout()
    {
        body.SetActive(!minimized);
        windowRect.sizeDelta = new Vector2(Width, minimized ? HeaderHeight : HeaderHeight + TabsHeight + ContentHeight);
        minLabel.text = minimized ? "+" : "-";
        for (int i = 0; i < 2; i++)
        {
            if (roots[i] != null) roots[i].gameObject.SetActive(i == tab);
            if (tabImages[i] != null) tabImages[i].color = i == tab ? TabOn : TabOff;
        }
    }

    private void Apply(bool on)
    {
        bool changed = IsOpen != on;
        IsOpen = on;
        canvasRoot.SetActive(on);
        PlayerPrefs.SetInt(PrefOpen, on ? 1 : 0);
        Layout();
        RefreshModes();
        if (changed) OpenChanged?.Invoke(on);
    }

    /// <summary>Only the visible tab's tool is active; the window being open keeps the mockup pieces showing.</summary>
    private void RefreshModes()
    {
        PixelDevMockup.SetWindowOpen(IsOpen);
        PixelDevShapes.SetActive(IsOpen && tab == 0);
        PixelDevMockup.SetActive(IsOpen && tab == 1);
    }
}
