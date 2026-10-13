// ---- System notes ----
// PixelViewWindow.cs
//   The View window (component added by PixelClicker.Awake; opens ONLY with its key, PixelAction.ViewWindow, default V; closes with the
//   key, its X or Escape (PixelWindows 130); a small draggable window that does not pause the game; disableViewWindow). It replaces the
//   old F (view) and X (zoom) keys, which are retired: View tick boxes - Default (3D), First person, Orbital, Top down - where ticking
//   one unticks the others (PixelViewWindow.SetView: leaves the current view, enters the new one; an orbital <-> top down switch just
//   re-aims the same camera, PixelOrbitCamera.SetTopDown); an 'Orthographic (flat) view' tick box (PixelOrthoView.Enabled, the same
//   setting as Settings > Flat view; greyed out in first person, which is always 3D); a Zoom slider (the normal view's PixelZoom.SetTarget,
//   or the orbit / top down camera's DistanceFraction; not in first person); and, in Orbital / Top down, the orbit controls: Auto orbit tick
//   box + speed slider, and hold buttons for turning (and, in Orbital only, tilting). While the window is open first person gives the
//   mouse back (PixelFirstPerson checks PixelViewWindow.IsOpen).
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PixelViewKind { Default, FirstPerson, Orbital, TopDown }

/// <summary>Hold-to-repeat helper for the View window's buttons.</summary>
public class PixelHoldPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool Held { get; private set; }
    public void OnPointerDown(PointerEventData e) { Held = true; }
    public void OnPointerUp(PointerEventData e) { Held = false; }
    public void OnPointerExit(PointerEventData e) { Held = false; }
    private void OnDisable() { Held = false; }
}

public class PixelViewWindow : MonoBehaviour
{
    [Header("View Window")]
    [Tooltip("Turn the View window (and its key) off.")]
    [SerializeField] private bool disableViewWindow = false;

    [Tooltip("Colour of the window.")]
    [SerializeField] private Color panelColor = new Color(0.1f, 0.13f, 0.2f, 0.97f);

    [Tooltip("Colour of the rows.")]
    [SerializeField] private Color rowColor = new Color(0.18f, 0.22f, 0.32f, 1f);

    [Tooltip("Colour of the tick boxes and sliders' tracks.")]
    [SerializeField] private Color boxColor = new Color(0.1f, 0.12f, 0.18f, 1f);

    [Tooltip("Colour of the ticks and the slider fills.")]
    [SerializeField] private Color accentColor = new Color(0.3f, 0.85f, 0.5f, 1f);

    [Tooltip("Colour of the hold buttons.")]
    [SerializeField] private Color buttonColor = new Color(0.25f, 0.4f, 0.7f, 1f);

    private static PixelViewWindow instance;
    private static Vector2 savedPosition = new Vector2(-40f, 0f);

    /// <summary>True while the window is open (first person gives the mouse back meanwhile).</summary>
    public static bool IsOpen => instance != null && PixelPop.IsOpen(instance.window);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; savedPosition = new Vector2(-40f, 0f); }

    /// <summary>The view in use right now.</summary>
    public static PixelViewKind CurrentView =>
        PixelFirstPerson.Active ? PixelViewKind.FirstPerson :
        PixelOrbitCamera.Active ? (PixelOrbitCamera.TopDown ? PixelViewKind.TopDown : PixelViewKind.Orbital) : PixelViewKind.Default;

    /// <summary>Switches to a view: leaves the current one and enters the new one. Returns false if the new view can't start now (the game is back to the Default view then).</summary>
    public static bool SetView(PixelViewKind kind)
    {
        PixelViewKind cur = CurrentView;
        if (cur == kind) return true;
        PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
        if ((cur == PixelViewKind.Orbital || cur == PixelViewKind.TopDown) && (kind == PixelViewKind.Orbital || kind == PixelViewKind.TopDown) && orbit != null)
        {
            orbit.SetTopDown(kind == PixelViewKind.TopDown);   // the same camera, re-aimed
            return true;
        }
        if (cur == PixelViewKind.FirstPerson) PixelFirstPerson.Leave();
        else if (cur != PixelViewKind.Default && orbit != null) orbit.Cancel();

        bool ok = true;
        switch (kind)
        {
            case PixelViewKind.FirstPerson: ok = PixelFirstPerson.TryEnter(); break;
            case PixelViewKind.Orbital: ok = orbit != null && orbit.Enter(false); break;
            case PixelViewKind.TopDown: ok = orbit != null && orbit.Enter(true); break;
        }
        if (!ok) PixelHints.Announce("That view can't start right now");
        return ok;
    }

    private PixelClicker clicker;
    private GameObject canvasRoot, window;
    private RectTransform windowRect;
    private TMP_FontAsset font;
    private readonly Toggle[] viewTicks = new Toggle[4];
    private Toggle orthoTick, autoTick;
    private TMP_Text orthoNote, zoomValue, speedValue;
    private Slider zoomSlider, speedSlider;
    private GameObject zoomRow, orbitSection, tiltRow;
    private PixelHoldPress leftBtn, rightBtn, upBtn, downBtn;
    private bool building;

    private const float RowH = 50f, Pad = 18f, Width = 600f;

    private void Awake()
    {
        instance = this;
        clicker = PixelFind.First<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelWindows.Register(this, 130, () => IsOpen, Close);
    }

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        if (instance == this) instance = null;
    }

    // ------------------------------------------------------------------

    private void Update()
    {
        if (disableViewWindow || clicker == null) return;
        bool blocked = PixelPauseMenu.IsPaused || PixelTitleScreen.Showing || PixelTitleScreen.Reversing;
        if (IsOpen && blocked) { Close(); return; }
        if (!blocked && !PixelKeys.Typing && !PixelTutorialDim.Active && PixelKeys.Pressed(PixelAction.ViewWindow))
        {
            if (IsOpen) Close(); else Open();
        }
        if (!IsOpen) return;
        Sync();
    }

    private void Open()
    {
        if (window == null) Build();
        if (window == null) return;
        windowRect.anchoredPosition = savedPosition;
        PixelPop.Show(window);
        Sync();
    }

    private void Close()
    {
        if (window != null) PixelPop.Hide(window);
        PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
        if (orbit != null) orbit.HoldTurn = orbit.HoldTilt = 0f;
    }

    // ------------------------------------------------------------------
    // Keeping the controls in step with the game
    // ------------------------------------------------------------------

    private bool Dragging(Slider s)
    {
        if (s == null || !PixelInput.LeftHeld()) return false;
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)s.transform, PixelInput.PointerPosition());
    }

    private void Sync()
    {
        building = true;
        PixelViewKind cur = CurrentView;
        for (int i = 0; i < viewTicks.Length; i++) if (viewTicks[i] != null) viewTicks[i].SetIsOnWithoutNotify(i == (int)cur);

        bool fp = cur == PixelViewKind.FirstPerson;
        orthoTick.SetIsOnWithoutNotify(PixelOrthoView.Enabled && !fp);
        orthoTick.interactable = !fp;
        orthoNote.gameObject.SetActive(fp);

        bool orbiting = cur == PixelViewKind.Orbital || cur == PixelViewKind.TopDown;
        zoomRow.SetActive(!fp);
        if (!fp)
        {
            PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
            float min, max, value;
            if (orbiting && orbit != null) { min = orbit.MinFraction; max = orbit.MaxFraction; value = orbit.DistanceFraction; }
            else { min = PixelZoom.ClosestFraction; max = PixelZoom.FurthestFraction; value = PixelZoom.Target; }
            zoomSlider.minValue = min;
            zoomSlider.maxValue = max;
            if (!Dragging(zoomSlider)) zoomSlider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            PixelUIKit.SetText(zoomValue, zoomSlider.value.ToString("0.00") + "x");
        }

        orbitSection.SetActive(orbiting);
        if (orbiting)
        {
            PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
            tiltRow.SetActive(cur == PixelViewKind.Orbital);
            if (orbit != null)
            {
                autoTick.SetIsOnWithoutNotify(orbit.AutoOrbit);
                if (!Dragging(speedSlider)) speedSlider.SetValueWithoutNotify(Mathf.Clamp(orbit.AutoSpeed, speedSlider.minValue, speedSlider.maxValue));
                PixelUIKit.SetText(speedValue, Mathf.RoundToInt(speedSlider.value) + " deg/s");
                orbit.HoldTurn = (rightBtn.Held ? 1f : 0f) - (leftBtn.Held ? 1f : 0f);
                orbit.HoldTilt = cur == PixelViewKind.Orbital ? (upBtn.Held ? 1f : 0f) - (downBtn.Held ? 1f : 0f) : 0f;
            }
        }
        building = false;
    }

    // ------------------------------------------------------------------
    // Building the window
    // ------------------------------------------------------------------

    private TMP_Text Label(Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        TMP_Text t = PixelUIKit.CreateText(font, parent, "Label", text, size, align, style, color);
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = 12f;
        t.overflowMode = TextOverflowModes.Truncate;
        return t;
    }

    private static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    private GameObject RowBox(Transform parent, float y, float h)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = rowColor;
        PixelUIKit.StyleBox(row.GetComponent<Image>());
        Place(row.GetComponent<RectTransform>(), Pad, y, Width - Pad * 2f, h);
        return row;
    }

    private Toggle TickRow(Transform parent, float y, string text, System.Action<bool> onChange, out GameObject row)
    {
        row = RowBox(parent, y, RowH);
        TMP_Text label = Label(row.transform, text, 26f, FontStyles.Normal, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(16f, 0f); lr.offsetMax = new Vector2(-(RowH + 16f), 0f);

        GameObject box = new GameObject("Tick Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
        box.transform.SetParent(row.transform, false);
        Image bg = box.GetComponent<Image>();
        bg.color = boxColor;
        PixelUIKit.StyleBox(bg, true);
        float size = RowH * 0.72f;
        RectTransform br = box.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(1f, 0.5f);
        br.sizeDelta = new Vector2(size, size);
        br.anchoredPosition = new Vector2(-12f, 0f);

        GameObject tick = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        tick.transform.SetParent(box.transform, false);
        Image ti = tick.GetComponent<Image>();
        ti.sprite = PixelUIKit.TickSprite();
        ti.color = accentColor;
        ti.raycastTarget = false;
        RectTransform kr = tick.GetComponent<RectTransform>();
        PixelUIKit.Stretch(kr);
        kr.offsetMin = new Vector2(size * 0.1f, size * 0.1f);
        kr.offsetMax = new Vector2(-size * 0.1f, -size * 0.1f);

        Toggle toggle = box.GetComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.graphic = ti;
        toggle.onValueChanged.AddListener(on => { if (!building) { onChange(on); PixelAudio.Play("ui_click"); } });
        return toggle;
    }

    private Slider SliderRow(Transform parent, float y, string text, float min, float max, System.Action<float> onChange, out GameObject row, out TMP_Text valueText)
    {
        row = RowBox(parent, y, RowH + 6f);
        TMP_Text label = Label(row.transform, text, 24f, FontStyles.Normal, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0.3f, 1f);
        lr.offsetMin = new Vector2(16f, 0f); lr.offsetMax = Vector2.zero;

        Slider slider = PixelUIKit.CreateSlider(row.transform, "Slider", boxColor, accentColor, Color.white);
        RectTransform sr = slider.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.31f, 0.1f); sr.anchorMax = new Vector2(0.82f, 0.9f);
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        slider.minValue = min;
        slider.maxValue = max;
        slider.onValueChanged.AddListener(v => { if (!building) onChange(v); });

        valueText = Label(row.transform, "", 22f, FontStyles.Bold, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.MidlineRight);
        RectTransform vr = valueText.rectTransform;
        vr.anchorMin = new Vector2(0.83f, 0f); vr.anchorMax = Vector2.one;
        vr.offsetMin = Vector2.zero; vr.offsetMax = new Vector2(-12f, 0f);
        return slider;
    }

    private PixelHoldPress HoldButton(Transform parent, string text, float x, float y, float w)
    {
        Button b = PixelUIKit.CreateButton(font, parent, text, text, new Vector2(w, 46f), buttonColor, Color.white, 24f);
        Place(b.GetComponent<RectTransform>(), x, y, w, 46f);
        return b.gameObject.AddComponent<PixelHoldPress>();
    }

    private void Build()
    {
        canvasRoot = PixelUIKit.CreateCanvas("View Window Canvas", 135, new Vector2(1920f, 1080f), true);
        canvasRoot.transform.SetParent(transform, false);

        window = new GameObject("View Window", typeof(RectTransform), typeof(Image), typeof(PixelPop));
        window.transform.SetParent(canvasRoot.transform, false);
        PixelUIKit.StyleWindow(window.GetComponent<Image>(), panelColor);
        windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(1f, 0.5f);

        float y = 12f;
        // Header: the whole strip drags the window; X closes it.
        GameObject handle = new GameObject("Drag Handle", typeof(RectTransform), typeof(Image), typeof(PixelPopoutDrag));
        handle.transform.SetParent(window.transform, false);
        handle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        Place(handle.GetComponent<RectTransform>(), 8f, 8f, Width - 16f, 56f);
        PixelPopoutDrag drag = handle.GetComponent<PixelPopoutDrag>();
        drag.target = windowRect;
        drag.canvas = canvasRoot.GetComponent<Canvas>();
        drag.onMoved = () => savedPosition = windowRect.anchoredPosition;
        TMP_Text title = Label(handle.transform, "VIEW", 32f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        PixelUIKit.Stretch(title.rectTransform);
        title.rectTransform.offsetMax = new Vector2(-60f, 0f);

        Button close = PixelUIKit.CreateButton(font, window.transform, "Close", "X", new Vector2(46f, 46f), new Color(0.6f, 0.25f, 0.25f, 1f), Color.white, 26f);
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-14f, -13f);
        close.onClick.AddListener(Close);
        y = 78f;

        // --- Views: ticking one unticks the others.
        string[] names = { "Default (3D)", "First person", "Orbital", "Top down" };
        for (int i = 0; i < names.Length; i++)
        {
            PixelViewKind kind = (PixelViewKind)i;
            viewTicks[i] = TickRow(window.transform, y, names[i], on =>
            {
                if (on) SetView(kind);
                Sync();   // (a tick box can't be switched off by clicking it: one view is always in use)
            }, out _);
            y += RowH + 6f;
        }
        y += 8f;

        // --- Flat (orthographic) view: not in first person.
        orthoTick = TickRow(window.transform, y, "Orthographic (flat) view", on => { PixelOrthoView.Enabled = on; Sync(); }, out GameObject orthoRow);
        orthoNote = Label(orthoRow.transform, "(not in first person)", 17f, FontStyles.Italic, new Color(1f, 0.8f, 0.4f), TextAlignmentOptions.MidlineRight);
        RectTransform nr = orthoNote.rectTransform;
        nr.anchorMin = Vector2.zero; nr.anchorMax = Vector2.one;
        nr.offsetMin = new Vector2(Width * 0.5f, 0f); nr.offsetMax = new Vector2(-(RowH + 20f), 0f);
        y += RowH + 14f;

        // --- Zoom slider (the normal view's zoom, or the orbit / top down camera's distance).
        zoomSlider = SliderRow(window.transform, y, "Zoom", 0.35f, 2f, v =>
        {
            PixelOrbitCamera orbit = PixelOrbitCamera.Instance;
            if (PixelOrbitCamera.Active && orbit != null) orbit.DistanceFraction = v;
            else PixelZoom.SetTarget(v);
        }, out zoomRow, out zoomValue);
        y += RowH + 14f;

        // --- Orbit controls (Orbital and Top down).
        orbitSection = new GameObject("Orbit Controls", typeof(RectTransform));
        orbitSection.transform.SetParent(window.transform, false);
        Place(orbitSection.GetComponent<RectTransform>(), 0f, y, Width, 250f);
        float oy = 0f;
        TMP_Text head = Label(orbitSection.transform, "ORBIT CONTROLS", 22f, FontStyles.Bold, new Color(0.7f, 0.8f, 1f));
        Place(head.rectTransform, Pad + 6f, oy, Width - Pad * 2f, 30f);
        oy += 36f;
        autoTick = TickRow(orbitSection.transform, oy, "Auto orbit", on => { if (PixelOrbitCamera.Instance != null) PixelOrbitCamera.Instance.AutoOrbit = on; }, out _);
        oy += RowH + 6f;
        speedSlider = SliderRow(orbitSection.transform, oy, "Auto speed", 2f, 60f, v => { if (PixelOrbitCamera.Instance != null) PixelOrbitCamera.Instance.AutoSpeed = v; }, out _, out speedValue);
        oy += RowH + 14f;
        float bw = (Width - Pad * 2f - 12f) / 2f;
        leftBtn = HoldButton(orbitSection.transform, "Turn left", Pad, oy, bw);
        rightBtn = HoldButton(orbitSection.transform, "Turn right", Pad + bw + 12f, oy, bw);
        tiltRow = new GameObject("Tilt Row", typeof(RectTransform));
        tiltRow.transform.SetParent(orbitSection.transform, false);
        Place(tiltRow.GetComponent<RectTransform>(), 0f, oy + 54f, Width, 50f);
        upBtn = HoldButton(tiltRow.transform, "Tilt up", Pad, 0f, bw);
        downBtn = HoldButton(tiltRow.transform, "Tilt down", Pad + bw + 12f, 0f, bw);
        orbitSection.GetComponent<RectTransform>().sizeDelta = new Vector2(Width, oy + 110f);
        y += oy + 116f;

        TMP_Text hint = Label(window.transform, "Press " + PixelKeys.Name(PixelAction.ViewWindow) + " again to close", 18f, FontStyles.Italic, new Color(0.7f, 0.75f, 0.85f), TextAlignmentOptions.Center);
        windowRect.sizeDelta = new Vector2(Width, y + 52f);
        Place(hint.rectTransform, Pad, y + 12f, Width - Pad * 2f, 28f);

        window.SetActive(false);
    }
}
